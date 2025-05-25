#if VCONTAINER_ENABLED && !ZENJECT_ENABLED
using System;
using VContainer;
using UnityEngine;
using VContainer.Unity;

namespace EasyCS
{
    public partial class EasyCSContainer : 
        IInitializable, IStartable,
        ITickable, IFixedTickable, ILateTickable
    {
        [Inject]
        private readonly IObjectResolver _resolver;
        private readonly IContainerBuilder _builder;

        public EasyCSContainer(IContainerBuilder builder)
        {
            _builder = builder;
        }

        public void Register<T>(T instance)
        {
            RegisterInternal(instance);
            _builder.RegisterInstance(instance);
        }

        public T Resolve<T>() => _resolver.Resolve<T>();

        public object Resolve(Type type) => _resolver.Resolve(type);

        public T TryResolve<T>() => _resolver.TryResolve(out T result) ? result : default;

        public object TryResolve(Type type) => _resolver.TryResolve(type, out var result) ? result : null;

        public T Instantiate<T>() where T : new()
        {
            var instance = new T();
            _resolver.Inject(instance);
            HandleInstantiate(instance);
            return instance;
        }

        public object Instantiate(Type type)
        {
            var instance = Activator.CreateInstance(type);
            _resolver.Inject(instance);
            HandleInstantiate(instance);
            return instance;
        }

        public GameObject Instantiate(GameObject prefab)
        {
            var instance = _resolver.Instantiate(prefab);
            HandleInstantiate(instance);
            return instance;
        }

        public GameObject Instantiate(GameObject prefab, Transform parent)
        {
            var instance = _resolver.Instantiate(prefab, parent);
            HandleInstantiate(instance);
            return instance;
        }

        public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = _resolver.Instantiate(prefab, position, rotation);
            HandleInstantiate(instance);
            return instance;
        }

        public void Initialize()
        {
            OnAwake();
        }

        public void Start()
        {
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