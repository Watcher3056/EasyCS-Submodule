using System.Collections.Generic;
using UnityEngine;

namespace EasyCS
{
    public static partial class Extensions
    {
        public static void GetComponentsInChildrenUntil<T1, T2>(this Transform root, List<T1> results, bool ignoreRootCheck)
        {
            if (ignoreRootCheck == false)
            {
                if (root.TryGetComponent<T2>(out _))
                    return;
            }

            results.AddRange(root.GetComponents<T1>());

            for (int i = 0; i < root.childCount; i++)
            {
                root.GetChild(i).GetComponentsInChildrenUntil<T1, T2>(results, false);
            }
        }

        public static List<T1> GetComponentsInChildrenUntil<T1, T2>(this Transform root)
        {
            var results = new List<T1>();
            GetComponentsInChildrenUntil<T1, T2>(root, results, true);
            return results;
        }
    }
}
