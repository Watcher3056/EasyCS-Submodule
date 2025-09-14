using System.Collections.Generic;
using UnityEngine;

namespace EasyCS
{
    public static class InitializationHelper
    {
        public static void SortForInitialization(List<Component> hasContainers)
        {
            hasContainers.Sort((a, b) =>
            {
                int orderA = GetInitializationOrder((IHasContainer)a);
                int orderB = GetInitializationOrder((IHasContainer)b);

                return orderA.CompareTo(orderB);
            });
        }

        public static void SortForInitialization(List<IHasContainer> hasContainers)
        {
            hasContainers.Sort((a, b) =>
            {
                int orderA = GetInitializationOrder(a);
                int orderB = GetInitializationOrder(b);

                return orderA.CompareTo(orderB);
            });
        }

        public static int GetInitializationOrder(IHasContainer container)
        {
            var comp = container as Component;

            if (comp is EntityProvider) return 0;
            if (comp is Actor) return 1;
            if (comp is IEntityDataProvider) return 2;
            if (comp is IActorData) return 3;
            if (comp is IEntityBehaviorProvider) return 4;
            if (comp is IActorComponent) return 5;
            if (comp is IEasyCSBehavior) return 6;

            return 7; // fallback: other IHasContainer implementors
        }
    }
}
