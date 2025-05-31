using System;
using UnityEngine;

namespace EasyCS
{
    public static class EntityComponentProviderFinder
    {
        public static Type FindEntityComponentProviderMatching(Type targetType)
        {
            if (typeof(IEntityData).IsAssignableFrom(targetType))
            {
                return EntityDataProviderFinder.FindEntityDataProviderMatching(targetType);
            }
            else if (typeof(IEntityBehavior).IsAssignableFrom(targetType))
            {
                return EntityBehaviorProviderFinder.FindEntityBehaviorProviderMatching(targetType);
            }
            else
            {
                Debug.LogError(string.Format("Not found provider for type {type}", targetType));
                return null;
            }
        }
    }
}
