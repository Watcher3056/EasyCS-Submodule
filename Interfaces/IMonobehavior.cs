using System.Collections;
using UnityEngine;

namespace EasyCS
{
    /// <summary>
    /// Abstraction of Unity's MonoBehaviour public API so implementors inheriting from MonoBehaviour
    /// satisfy this interface without additional method implementations.
    /// Note: Only public instance members are included (no Unity magic message methods).
    /// </summary>
    public interface IMonobehavior
    {
        // From UnityEngine.Object / Component hierarchy
        string name { get; set; }
        HideFlags hideFlags { get; set; }

        // From UnityEngine.Component
        Transform transform { get; }
        GameObject gameObject { get; }
        string tag { get; set; }
        bool CompareTag(string tag);

        // From UnityEngine.Behaviour
        bool enabled { get; set; }
        bool isActiveAndEnabled { get; }

        // From UnityEngine.MonoBehaviour
        bool useGUILayout { get; set; }

        void CancelInvoke();
        void CancelInvoke(string methodName);
        bool IsInvoking();
        bool IsInvoking(string methodName);
        void Invoke(string methodName, float time);
        void InvokeRepeating(string methodName, float time, float repeatRate);

        Coroutine StartCoroutine(string methodName);
        Coroutine StartCoroutine(string methodName, object value);
        Coroutine StartCoroutine(IEnumerator routine);
        void StopCoroutine(IEnumerator routine);
        void StopCoroutine(Coroutine routine);
        void StopCoroutine(string methodName);
        void StopAllCoroutines();

        // Component messaging helpers
        void BroadcastMessage(string methodName);
        void BroadcastMessage(string methodName, object parameter);
        void BroadcastMessage(string methodName, object parameter, SendMessageOptions options);
        void SendMessage(string methodName);
        void SendMessage(string methodName, object value);
        void SendMessage(string methodName, object value, SendMessageOptions options);
        void SendMessageUpwards(string methodName);
        void SendMessageUpwards(string methodName, object value);
        void SendMessageUpwards(string methodName, object value, SendMessageOptions options);
    }
}


