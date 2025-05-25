using System;
using UnityEngine;

namespace EasyCS
{

    public interface IEasyCSObjectResolver
    {
        T Resolve<T>();
        object Resolve(Type type);

        T TryResolve<T>();
        object TryResolve(Type type);

        T Instantiate<T>() where T : new();
        object Instantiate(Type type);

        public void HandleInstantiate<T>(T instance);

        public void HandleInstantiate(GameObject instance);

        GameObject Instantiate(GameObject prefab);
        GameObject Instantiate(GameObject prefab, Transform parent);
        GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation);
    }

}