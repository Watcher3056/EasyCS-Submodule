using UnityEngine;
using IResolver = EasyCS.IEasyCSObjectResolver;

namespace EasyCS
{

    public static class EasyCSContainerExtensions
    {

        public static T InstantiateWithEntity<T>(this IResolver resolver, T prefab, Vector3 position, Quaternion rotation, Transform parent = null, bool parentEntity = true)
            where T : Component
        {
            T result = resolver.InstantiateWithEntity(prefab, parent, parentEntity);
            result.transform.SetPositionAndRotation(position, rotation);
            return result;
        }

        public static T InstantiateWithEntity<T>(this IResolver resolver, T prefab, Transform parent = null, bool parentEntity = true)
            where T : Component
        {
            GameObject go = resolver.InstantiateWithEntity(prefab.gameObject, parent, parentEntity);
            return go.GetComponent<T>();
        }

        public static GameObject InstantiateWithEntity(this IResolver resolver, GameObject prefab, Transform parent = null, bool parentEntity = true)
        {
            EntityContainer entityContainer = resolver.Resolve<EntityContainer>();
            GameObject instance = resolver.Instantiate(prefab, parent);

            PrefabRootData rootData = instance.GetComponent<PrefabRootData>();
            if (rootData != null && rootData.RootRelation != null)
            {
                RegisterActorRecursively(rootData.RootRelation, Entity.Empty, resolver, entityContainer, parentEntity);
            }

            return instance;
        }

        private static void RegisterActorRecursively(
            PrefabRootData.ActorRelation relation,
            Entity parentEntity,
            IResolver resolver,
            EntityContainer container,
            bool parentEntityEnabled)
        {
            if (relation == null || relation.Actor == null)
                return;

            // Create or fetch entity
            Entity entity = relation.Actor.EntityProvider == null
                ? container.CreateNew()
                : relation.Actor.EntityProvider.Entity;

            if (relation.Actor.EntityProvider != null)
            {
                container.RegisterEntity(entity);
            }

            relation.Actor.SetEntity(entity);

            if (parentEntityEnabled && parentEntity.IsAlive)
            {
                entity.SetParent(parentEntity);
            }

            foreach (var child in relation.Childs)
            {
                RegisterActorRecursively(child, entity, resolver, container, parentEntityEnabled);
            }
        }
    }
}
