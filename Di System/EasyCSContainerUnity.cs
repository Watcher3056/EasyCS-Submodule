using System;
using UnityEngine;

namespace EasyCS
{
#if !VCONTAINER_ENABLED && !ZENJECT_ENABLED
    public partial class EasyCSContainer
    {
        public void Register<T>(T instance)
        {
            RegisterInternal(instance);
        }

        public T Resolve<T>() => (T)_fallbackServices[typeof(T)];

        public object Resolve(Type type) => _fallbackServices[type];

        public T TryResolve<T>() => _fallbackServices.TryGetValue(typeof(T), out var value) ? (T)value : default;

        public object TryResolve(Type type) => _fallbackServices.TryGetValue(type, out var value) ? value : null;

        public T Instantiate<T>() where T : new()
        {
            T instance = new T();
            HandleInstantiate(instance);
            return instance;
        }

        public object Instantiate(Type type)
        {
            object instance = Activator.CreateInstance(type);
            HandleInstantiate(instance);
            return instance;
        }

        public GameObject Instantiate(GameObject prefab)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            HandleInstantiate(instance);
            return instance;
        }

        public GameObject Instantiate(GameObject prefab, Transform parent)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
            HandleInstantiate(instance);
            return instance;
        }

        public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            HandleInstantiate(instance);
            return instance;
        }

    }
#endif
}