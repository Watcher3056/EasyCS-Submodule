using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCS
{
    public class LifetimeLoopSystem : IUpdate, IFixedUpdate, ILateUpdate, IAwake, IStart, IDisposable, IHasContainer
    {
        private readonly DefferedSparseIndexedSet<IUpdate> _updates = new();
        private readonly DefferedSparseIndexedSet<IFixedUpdate> _fixedUpdates = new();
        private readonly DefferedSparseIndexedSet<ILateUpdate> _lateUpdates = new();
        private readonly DefferedSparseIndexedSet<IAwake> _awakes = new();
        private readonly DefferedSparseIndexedSet<IStart> _starts = new();
        private readonly Queue<IStart> _pendingStarts = new(1000);
        private readonly HashSet<IStart> _removedPendingStarts = new(1000);

        public IEasyCSObjectResolver EasyCsContainer { get; private set; }
        public bool HasAwakeBeenCalled => _hasAwakeBeenCalled;
        public bool HasStartBeenCalled => _hasStartBeenCalled;
        private bool _hasAwakeBeenCalled;
        private bool _hasStartBeenCalled;
        private EntityContainer _entityContainer;

        public LifetimeLoopSystem()
        {
        }

        public void SetupContainer(IEasyCSObjectResolver container)
        {
            EasyCsContainer = container;
            _entityContainer = container.Resolve<EntityContainer>();

            _entityContainer.OnEntityComponentAdded += HandleComponentAdded;
            _entityContainer.OnEntityComponentRemoved += HandleComponentRemoved;
        }


        private void HandleComponentAdded(Entity entity, IEntityComponent component)
        {
            if (component is IEntityBehavior behavior)
                TryAdd(behavior);
        }

        private void HandleComponentRemoved(Entity entity, IEntityComponent component)
        {
            if (component is IEntityBehavior behavior)
                TryRemove(behavior);
        }

        public void Add(IUpdate update) => _updates.Add(update);
        public void Remove(IUpdate update) => _updates.Remove(update);

        public void Add(IFixedUpdate fixedUpdate) => _fixedUpdates.Add(fixedUpdate);
        public void Remove(IFixedUpdate fixedUpdate) => _fixedUpdates.Remove(fixedUpdate);

        public void Add(ILateUpdate lateUpdate) => _lateUpdates.Add(lateUpdate);
        public void Remove(ILateUpdate lateUpdate) => _lateUpdates.Remove(lateUpdate);

        public void Add(IAwake awake)
        {
            _awakes.Add(awake);
            if (_hasAwakeBeenCalled)
            {
                try
                {
                    awake.OnAwake();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _awakes.Remove(awake);
                }
            }
        }

        public void Remove(IAwake awake) => _awakes.Remove(awake);

        public void Add(IStart start)
        {
            _starts.Add(start);
            if (_hasStartBeenCalled)
                _pendingStarts.Enqueue(start);
        }

        public void Remove(IStart start)
        {
            _starts.Remove(start);
            if (_hasStartBeenCalled)
                _removedPendingStarts.Add(start);
        }

        public void TryAdd(object component)
        {
            if (component is IUpdate update) Add(update);
            if (component is IFixedUpdate fixedUpdate) Add(fixedUpdate);
            if (component is ILateUpdate lateUpdate) Add(lateUpdate);
            if (component is IAwake awake) Add(awake);
            if (component is IStart start) Add(start);
        }

        public void TryRemove(object component)
        {
            if (component is IUpdate update) Remove(update);
            if (component is IFixedUpdate fixedUpdate) Remove(fixedUpdate);
            if (component is ILateUpdate lateUpdate) Remove(lateUpdate);
            if (component is IAwake awake) Remove(awake);
            if (component is IStart start) Remove(start);
        }

        public void OnAwake()
        {
            _awakes.ApplyChanges();

            _hasAwakeBeenCalled = true;
            for (int i = 0; i < _awakes.CountAll; i++)
            {
                var awake = _awakes[i];

                if (awake == null)
                    continue;
                if (awake is Behaviour behaviour && !(behaviour.enabled && behaviour.gameObject.activeInHierarchy))
                    continue;

                try
                {
                    awake?.OnAwake();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _awakes.Remove(awake);
                    i--;
                }
            }
        }

        public void OnStart()
        {
            _starts.ApplyChanges();

            _hasStartBeenCalled = true;
            for (int i = 0; i < _starts.CountAll; i++)
            {
                var start = _starts[i];

                if (start == null)
                    continue;
                if (start is Behaviour behaviour && !(behaviour.enabled && behaviour.gameObject.activeInHierarchy))
                    continue;

                try
                {
                    start.OnStart();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _starts.Remove(start);
                    i--;
                }
            }
        }

        public void OnUpdate(float deltaTime)
        {
            _updates.ApplyChanges();

            while (_pendingStarts.Count > 0)
            {
                IStart start = _pendingStarts.Peek();

                if (_removedPendingStarts.Remove(start))
                {
                    _pendingStarts.Dequeue();
                    continue;
                }

                if (start is Behaviour behaviour && !(behaviour.enabled && behaviour.gameObject.activeInHierarchy))
                    continue;

                _pendingStarts.Dequeue();

                try
                {
                    start.OnStart();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            for (int i = 0; i < _updates.CountAll; i++)
            {
                try
                {
                    var update = _updates[i];

                    if (update == null)
                        continue;
                    if (update is Behaviour behaviour && !(behaviour.enabled && behaviour.gameObject.activeInHierarchy))
                        continue;

                    update.OnUpdate(deltaTime);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _updates.Remove(_updates[i]);
                    i--;
                }
            }
        }

        public void OnFixedUpdate(float deltaTime)
        {
            _fixedUpdates.ApplyChanges();

            for (int i = 0; i < _fixedUpdates.CountAll; i++)
            {
                try
                {
                    var fixedUpdate = _fixedUpdates[i];

                    if (fixedUpdate == null)
                        continue;
                    if (fixedUpdate is Behaviour behaviour && !(behaviour.enabled && behaviour.gameObject.activeInHierarchy))
                        continue;

                    fixedUpdate.OnFixedUpdate(deltaTime);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _fixedUpdates.Remove(_fixedUpdates[i]);
                    i--;
                }
            }
        }

        public void OnLateUpdate(float deltaTime)
        {
            _lateUpdates.ApplyChanges();

            for (int i = 0; i < _lateUpdates.CountAll; i++)
            {
                try
                {
                    var lateUpdate = _lateUpdates[i];

                    if (lateUpdate == null)
                        continue;
                    if (lateUpdate is Behaviour behaviour && !(behaviour.enabled && behaviour.gameObject.activeInHierarchy))
                        continue;

                    lateUpdate.OnLateUpdate(deltaTime);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _lateUpdates.Remove(_lateUpdates[i]);
                    i--;
                }
            }
        }

        public void Dispose()
        {
            _updates.Clear();
            _fixedUpdates.Clear();
            _lateUpdates.Clear();
            _awakes.Clear();
            _starts.Clear();
            _pendingStarts.Clear();
            _removedPendingStarts.Clear();

            _entityContainer.OnEntityComponentAdded -= HandleComponentAdded;
            _entityContainer.OnEntityComponentRemoved -= HandleComponentRemoved;
        }
    }
}