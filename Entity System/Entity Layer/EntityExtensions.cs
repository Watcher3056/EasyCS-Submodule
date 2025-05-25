using System;
using EasyCS.EventSystem;
using UnityEngine;

namespace EasyCS
{
    public static class EntityExtensions
    {
        private static class Messages
        {
            public const string ErrorCannotKillAlreadyKilled =
                "Cannot kill entity {0}. Reason: Already killed. Aborted.";
        }

        public static void Kill(this Entity entity)
        {
            if (!entity.IsAlive)
            {
                entity.LogError(string.Format(Messages.ErrorCannotKillAlreadyKilled, entity));
                return;
            }

            entity.EntityContainer.RemoveEntity(entity);
        }

        public static void KillWithActors(this Entity entity)
        {
            if (!entity.IsAlive)
            {
                entity.LogError(string.Format(Messages.ErrorCannotKillAlreadyKilled, entity));
                return;
            }

            Actor actor = entity.Actor;

            GameObject.Destroy(actor.gameObject);
            entity.Kill();
        }

        public static T GetComponent<T>(this Entity entity) where T : IEntityComponent
        {
            Type type = typeof(T);
            IEntityComponent result = entity.EntityContainer?.GetComponent(entity, type);
            T casted = result != null ? (T)result : default;
            return casted;
        }

        public static IEntityComponent GetComponent(this Entity entity, Type type)
        {
            IEntityComponent result = entity.EntityContainer?.GetComponent(entity, type);
            return result;
        }

        public static T AddComponent<T>(this Entity entity) where T : class, IEntityComponent, new()
        {
            T result = entity.EntityContainer.AddComponent<T>(entity);
            return result;
        }

        public static IEntityComponent AddComponent(this Entity entity, IEntityComponent component)
        {
            IEntityComponent result = entity.EntityContainer.AddComponent(entity, component);
            return result;
        }

        public static IEntityComponent AddComponent(this Entity entity, Type type)
        {
            IEntityComponent result = entity.EntityContainer.AddComponent(entity, type);
            return result;
        }

        public static bool HasComponent<T>(this Entity entity) where T : class, IEntityComponent
        {
            bool result = entity.EntityContainer.HasComponent<T>(entity);
            return result;
        }

        public static bool HasComponent(this Entity entity, Type type)
        {
            bool result = entity.EntityContainer.HasComponent(entity, type);
            return result;
        }

        public static void RemoveComponent(this Entity entity, IEntityComponent component)
        {
            entity.EntityContainer.RemoveComponent(entity, component);
        }

        public static void RemoveComponent<T>(this Entity entity)
            where T : IEntityComponent
        {
            entity.EntityContainer.RemoveComponent<T>(entity);
        }

        public static void RemoveComponent(this Entity entity, Type type)
        {
            entity.EntityContainer.RemoveComponent(entity, type);
        }

        public static IEntityBehavior AddBehavior<T>(this Entity entity)
            where T : IEntityBehavior, new()
        {
            IEntityBehavior result = entity.EntityContainer.AddBehavior<T>(entity);

            return result;
        }

        public static IEntityBehavior AddBehavior(this Entity entity, Type type)
        {
            IEntityBehavior result = entity.EntityContainer.AddBehavior(entity, type);

            return result;
        }

        public static IEntityComponent AddData(this Entity entity, IEntityDataFactory dataFactory)
        {
            IEntityComponent result = entity.EntityContainer.AddData(entity, dataFactory);

            return result;
        }

        public static void AddData<T>(this Entity entity, T data)
            where T : IEntityData
        {
            entity.EntityContainer.AddData(entity, data);
        }

        public static IEntityComponent SetData<T>(this Entity entity, T data)
            where T : IEntityData
        {
            entity.EntityContainer.SetData(entity, data);

            return data;
        }

        public static IEntityData SetData(this Entity entity, IEntityDataFactory entityDataFactory)
        {
            IEntityData data = entity.EntityContainer.SetData(entity, entityDataFactory);

            return data;
        }

        public static IEntityBehavior TryGetOrAddBehavior(this Entity entity, Type type)
        {
            IEntityBehavior result = entity.EntityContainer.TryGetOrAddBehavior(entity, type);
            return result;
        }

        public static T TryGetOrAddBehavior<T>(this Entity entity) where T : class, IEntityBehavior, new()
        {
            T result = entity.EntityContainer.TryGetOrAddBehavior<T>(entity);
            return result;
        }

        public static T TryGetOrAddData<T>(this Entity entity) where T : class, IEntityData, new()
        {
            T result = entity.EntityContainer.TryGetOrAddData<T>(entity);
            return result;
        }

        public static IEntityData TryGetOrAddData(this Entity entity, IEntityDataFactory factory)
        {
            IEntityData result = entity.EntityContainer.TryGetOrSetData(entity, factory);
            return result;
        }

        public static T TryGetOrAddComponent<T>(this Entity entity) where T : class, IEntityComponent, new()
        {
            T result = entity.EntityContainer.TryGetOrAddComponent<T>(entity);
            return result;
        }

        public static IEntityComponent TryGetOrAddComponent(this Entity entity, Type type)
        {
            IEntityComponent result = entity.EntityContainer.TryGetOrAddComponent(entity, type);
            return result;
        }

        public static bool TryGetComponent<T>(this Entity entity, out T component) where T : class, IEntityComponent
        {
            if (entity.HasComponent<T>())
            {
                component = entity.GetComponent<T>();
                return true;
            }

            component = null;
            return false;
        }

        public static bool TryGetComponent(this Entity entity, Type type, out IEntityComponent component)
        {
            if (entity.HasComponent(type))
            {
                component = entity.GetComponent(type);
                return true;
            }

            component = null;
            return false;
        }

        public static void RaiseEventOn<T>(this Entity entity, T eventData, params Entity[] targets)
            where T : IEvent
        {
            IEventSystem eventSystem =
                entity.EntityContainer.EasyCsContainer.Resolve<DefaultEventSystem>();

            for (int i = 0; i < targets.Length; i++)
            {
                eventSystem.Raise(eventData, targets[i]);
            }
        }

        public static void RaiseEvent<T>(this Entity entity, T eventData)
            where T : IEvent
        {
            IEventSystem eventSystem =
                entity.EntityContainer.EasyCsContainer.Resolve<DefaultEventSystem>();

            eventSystem.Raise(eventData, entity);
        }

        public static void SetParent(this Entity entity, Entity newParent)
        {
            // Get current parent
            var parentComponent = entity.TryGetOrAddComponent<EntityDataParent>();

            // If already set to this parent, do nothing
            if (parentComponent.Value.Equals(newParent))
                return;

            // Remove from old parent's child list
            if (parentComponent.Value.IsAlive)
            {
                var oldParent = parentComponent.Value;

                var oldChilds = oldParent.TryGetOrAddComponent<EntityDataChilds>();
                oldChilds.childs.Remove(entity);
            }

            // Set new parent
            EntityDataParent dataParent = entity.TryGetOrAddData<EntityDataParent>();
            dataParent.Value = newParent;

            // Add to new parent's child list
            var newChilds = newParent.TryGetOrAddData<EntityDataChilds>();

            if (!newChilds.childs.Contains(entity))
                newChilds.childs.Add(entity);
        }
    }
}