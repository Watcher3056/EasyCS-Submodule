using System;
using UnityEngine;

namespace EasyCS
{
    public static partial class Functions
    {
        public static void ForEachRecursive(this Transform root, Action<Transform> action, bool includeRoot = false)
        {
            if (root == null || action == null)
                return;

            if (includeRoot)
                action(root);

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null)
                {
                    action(child);
                    child.ForEachRecursive(action, includeRoot: true); // Always include children recursively
                }
            }
        }

        public static T TryGetElseSetComponent<T>(this GameObject go) where T : Component
        {
            return go.TryGetElseSetComponent<T>(out _);
        }

        public static Component TryGetElseSetComponent(this GameObject go, Type type, out bool added)
        {
            Component result = null;
            if (go.TryGetComponent(type, out result) == false)
            {
                result = go.AddComponent(type);
                added = true;
            }
            else
                added = false;

            return result;
        }

        public static T TryGetElseSetComponent<T>(this GameObject go, out bool added) where T : Component
        {
            T result = null;
            if (go.TryGetComponent<T>(out result) == false)
            {
                result = go.AddComponent<T>();
                added = true;
            }
            else
                added = false;

            return result;
        }

        public static T GetComponentInParent<T>
            (this Component component, bool includeInactive = false, bool includeThis = true) where T : Component
            => GetComponentInParent<T>(component.gameObject, includeInactive, includeThis);
        public static T GetComponentInParent<T>
            (this GameObject go, bool includeInactive = false, bool includeThis = true) where T : Component
        {
            T result = null;
            if (includeThis)
                result = go.GetComponentInParent<T>(includeInactive);
            else if (go.transform.parent != null)
                result = go.transform.parent.GetComponentInParent<T>(includeInactive);
            return result;
        }
    }
}