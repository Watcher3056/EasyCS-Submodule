using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCS
{
    public partial class EasyCSContainer : IEasyCSObjectResolver, IUpdate, IFixedUpdate, ILateUpdate, IAwake, IStart, IDisposable
    {
        protected readonly Dictionary<Type, object> _fallbackServices = new();
        protected readonly List<IHasContainer> _hasContainers = new();
        protected readonly List<IAwake> _awakeServices = new();
        protected readonly List<IStart> _startServices = new();
        protected readonly List<IUpdate> _updateServices = new();
        protected readonly List<IFixedUpdate> _fixedUpdateServices = new();
        protected readonly List<ILateUpdate> _lateUpdateServices = new();
        protected readonly List<IDisposable> _disposableServices = new();
        private bool _firstUpdatePassed;
        private bool _firstFixedUpdatePassed;
        private bool _firstLateUpdatePassed;

        protected void RegisterInternal<T>(T instance)
        {
            Type type = typeof(T);
            _fallbackServices[type] = instance;

            if (instance is IHasContainer hasContainer) _hasContainers.Add(hasContainer);
            if (instance is IAwake awake) _awakeServices.Add(awake);
            if (instance is IStart start) _startServices.Add(start);
            if (instance is IUpdate update) _updateServices.Add(update);
            if (instance is IFixedUpdate fixedUpdate) _fixedUpdateServices.Add(fixedUpdate);
            if (instance is ILateUpdate lateUpdate) _lateUpdateServices.Add(lateUpdate);
            if (instance is IDisposable disposable) _disposableServices.Add(disposable);
        }

        public void HandleInstantiate<T>(T instance)
        {
            if (instance is IHasContainer hasContainer)
                hasContainer.SetupContainer(this);
        }

        public void HandleInstantiate(GameObject instance)
        {
            PrefabRootData rootData = instance.GetComponent<PrefabRootData>();
            if (rootData != null)
            {
                for (int i = 0; i < rootData.HasContainers.Count; i++)
                {
                    IHasContainer hasContainer = (IHasContainer)rootData.HasContainers[i];
                    HandleInstantiate(hasContainer);
                }
            }
        }

        private void SetupServicesDependencies()
        {
            foreach (var hasContainer in _hasContainers)
            {
                hasContainer.SetupContainer(this);
            }
        }

        public void OnAwake()
        {
            Debug.Log("[EasyCS] Awake has been called");

            SetupServicesDependencies();

            foreach (var service in _awakeServices)
                service.OnAwake();

            Debug.Log("[EasyCS] Awake has been completed");
        }

        public void OnStart()
        {
            Debug.Log("[EasyCS] Start has been called");

            foreach (var service in _startServices)
                service.OnStart();

            Debug.Log("[EasyCS] Start has been completed");
        }

        public void OnUpdate(float deltaTime)
        {
            bool logCompletion = false;

            if (!_firstUpdatePassed)
            {
                Debug.Log("[EasyCS] First Update has been called");
                _firstUpdatePassed = true;
                logCompletion = true;
            }

            foreach (var service in _updateServices)
                service.OnUpdate(deltaTime);

            if (logCompletion)
            {
                Debug.Log("[EasyCS] First Update has been completed");
            }
        }

        public void OnFixedUpdate(float deltaTime)
        {
            bool logCompletion = false;

            if (!_firstFixedUpdatePassed)
            {
                Debug.Log("[EasyCS] First Fixed Update has been called");
                _firstFixedUpdatePassed = true;
                logCompletion = true;
            }

            foreach (var service in _fixedUpdateServices)
                service.OnFixedUpdate(deltaTime);

            if (logCompletion)
            {
                Debug.Log("[EasyCS] First Fixed Update has been completed");
            }
        }

        public void OnLateUpdate(float deltaTime)
        {
            bool logCompletion = false;

            if (!_firstLateUpdatePassed)
            {
                Debug.Log("[EasyCS] First Late Update has been called");
                _firstLateUpdatePassed = true;
                logCompletion = true;
            }

            foreach (var service in _lateUpdateServices)
                service.OnLateUpdate(deltaTime);

            if (logCompletion)
            {
                Debug.Log("[EasyCS] First Late Update has been completed");
            }
        }

        public void Dispose()
        {
            foreach (var disposable in _disposableServices)
                disposable.Dispose();

            _awakeServices.Clear();
            _startServices.Clear();
            _updateServices.Clear();
            _fixedUpdateServices.Clear();
            _lateUpdateServices.Clear();
            _disposableServices.Clear();
            _fallbackServices.Clear();
        }
    }
}
