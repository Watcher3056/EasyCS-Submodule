#if ZENJECT_ENABLED
using System;
using Zenject;
using UnityEngine;

namespace EasyCS
{
    public partial class EasyCSContainer : 
        IInitializable, 
        ITickable, IFixedTickable, ILateTickable
    {
        private readonly DiContainer _container;

        public EasyCSContainer(DiContainer container)
        {
            _container = container;
        }

        public void Register<T>(T instance)
        {
            RegisterInternal(instance);
            _container.Bind<T>().FromInstance(instance).AsSingle();
        }

        public T Resolve<T>() => _container.Resolve<T>();

        public object Resolve(Type type) => _container.Resolve(type);

        public T TryResolve<T>() => _container.HasBinding<T>() ? _container.Resolve<T>() : default;

        public object TryResolve(Type type) => _container.HasBinding(type) ? _container.Resolve(type) : null;

        public T Instantiate<T>() where T : new()
        {
            var instance = _container.Instantiate<T>();
            HandleInstantiate(instance);
            return instance;
        }

        public object Instantiate(Type type)
        {
            var instance = _container.Instantiate(type);
            HandleInstantiate(instance);
            return instance;
        }

        public GameObject Instantiate(GameObject prefab)
        {
            var instance = _container.InstantiatePrefab(prefab);
            HandleInstantiate(instance);
            return instance;
        }

        public GameObject Instantiate(GameObject prefab, Transform parent)
        {
            var instance = _container.InstantiatePrefab(prefab, parent);
            HandleInstantiate(instance);
            return instance;
        }

        public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = _container.InstantiatePrefab(prefab, position, rotation, null);
            HandleInstantiate(instance);
            return instance;
        }

        public void Initialize()
        {
            OnAwake();
            OnStart();
        }

        public void Tick()
        {
            OnUpdate(Time.deltaTime);
        }

        public void FixedTick()
        {
            OnFixedUpdate(Time.fixedDeltaTime);
        }

        public void LateTick()
        {
            OnLateUpdate(Time.deltaTime);
        }
    }
}
#endif