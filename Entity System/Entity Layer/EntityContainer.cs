using EasyCS.EventSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCS
{
    public class EntityContainer : IHasEntity, IDisposable, IHasContainer
    {
        public static class Messages
        {
            public const string ERROR_ADD_COMPONENT_ENTITY_NOT_REGISTERED =
                "Cannot add component {0} in entity {1}. Reason: not registered. Aborted.";

            public const string ERROR_ADD_COMPONENT_WRONG_TYPE =
                "Cannot add component {0} in entity {1}. Reason: wrong type. Aborted.";

            public const string ERROR_ADD_COMPONENT_COMPONENT_ALREADY_EXISTS =
                "Cannot add component {0} in entity {1}. Reason: component already exist. Aborted.";

            public const string ERROR_REMOVE_COMPONENT_ENTITY_NOT_REGISTERED =
                "Cannot remove component {0} in entity {1}. Reason: not registered. Aborted.";

            public const string ERROR_HAS_COMPONENT_ENTITY_NOT_REGISTERED =
                "Cannot check for component {0} in entity {1}. Reason: not registered. Aborted.";

            public const string ERROR_GET_COMPONENT_ENTITY_NOT_REGISTERED =
                "Cannot get component {0} in entity {1}. Reason: not registered. Aborted.";

            public const string ERROR_GET_ALL_COMPONENTS_ENTITY_NOT_REGISTERED =
                "Cannot get components in entity {0}. Reason: not registered. Aborted.";

            public const string ERROR_SET_INCLUDE_TO_SAVE_ENTITY_NOT_REGISTERED =
                "Cannot set include to save entity {0}. Reason: not registered. Aborted.";

            public const string ERROR_IS_INCLUDED_TO_SAVE_ENTITY_NOT_REGISTERED =
                "Cannot check if entity {0} is included to save. Reason: not registered. Aborted.";

            public const string ERROR_REGISTER_ACTOR_ENTITY_NOT_REGISTERED =
                "Cannot register actor {0} with entity {1}. Reason: not registered. Aborted.";

            public const string ERROR_COMPONENT_TYPE_NULL =
                "Cannot add component. Reason: type is null. Aborted.";

            public const string ERROR_COMPONENT_TYPE_INVALID =
                "Cannot add component. Reason: type '{0}' does not implement IEntityComponent. Aborted.";

            public const string ERROR_COMPONENT_NULL =
                "Cannot add component. Reason: component is null. Aborted.";
        }

        public class EntityData
        {
            public readonly Entity _entity;

            public readonly Dictionary<Type, IEntityComponent> _componentsByType =
                new Dictionary<Type, IEntityComponent>();

            public readonly HashSet<IEntityData> _componentsData = new HashSet<IEntityData>();
            public readonly HashSet<IEntityBehavior> _componentsBehaviors = new HashSet<IEntityBehavior>();

            public EntityData(Entity entity)
            {
                this._entity = entity;
            }
        }

        public event Action<EntityContainer, Entity> OnEntityAdded = (self, entity) => { };
        public event Action<EntityContainer, Entity> OnBeforeEntityRemoved = (self, entity) => { };
        public event Action<EntityContainer, Entity> OnEntityRemoved = (self, entity) => { };
        public event Action<Entity, IEntityComponent> OnEntityComponentAdded = (entity, component) => { };
        public event Action<Entity, IEntityComponent> OnEntityComponentRemoved = (entity, component) => { };
        public event Action<Entity, Actor> OnActorAdded = (entity, actor) => { };
        public event Action<Entity, Actor> OnActorRemoved = (entity, actor) => { };
        public event Action<EntityContainer> OnDispose = (container) => { };

        public IEnumerable<Entity> EntitiesAll => _dataByEntity.Keys;
        public IEnumerable<Actor> ActorsAll => _actorByEntity.Values;
        private Dictionary<Entity, EntityData> _dataByEntity = new Dictionary<Entity, EntityData>();
        private Dictionary<Entity, Actor> _actorByEntity = new Dictionary<Entity, Actor>();

        public Entity Entity { get; private set; }
        public IEasyCSObjectResolver EasyCsContainer => _container;

        private IEasyCSObjectResolver _container;
        private EntityRootRegistry _rootRegistry;
        private IEventSystem _eventSystem;

        public EntityContainer(Entity entity)
        {
            this.Entity = entity;

            EntityRootRegistry.Instance.RegisterContainerIfNeeded(this);
        }


        public void SetupContainer(IEasyCSObjectResolver container)
        {
            _container = container;
            _rootRegistry = EntityRootRegistry.Instance;
            _eventSystem = container.Resolve<DefaultEventSystem>();
        }

        public Entity CreateNew()
        {
            Entity entity = Entity.CreateNew();
            RegisterEntity(entity);

            return entity;
        }

        public Entity CreateNew(Guid id)
        {
            Entity entity = new Entity(id);
            RegisterEntity(entity);

            return entity;
        }

        public void RegisterEntity(Entity entity)
        {
            if (IsEntityRegistered(entity) == false)
            {
                EntityData entityData = new EntityData(entity);

                _dataByEntity.Add(entity, entityData);

                OnEntityAdded.Invoke(this, entity);
            }
        }

        public void RemoveEntity(Entity entity)
        {
            EntityData entityData = GetDataByEntity(entity);

            if (entityData != null)
            {
                try
                {
                    OnBeforeEntityRemoved.Invoke(this, entity);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                foreach (var component in entityData._componentsBehaviors)
                    if (component is IDisposable disposable)
                        disposable.Dispose();

                _dataByEntity.Remove(entity);
                _eventSystem.Raise(new EventEntityKilled(), entity);
                OnEntityRemoved.Invoke(this, entity);
            }
        }

        public bool IsEntityRegistered(Entity entity)
        {
            bool result = _dataByEntity.ContainsKey(entity);

            return result;
        }

        public IEntityComponent AddComponent(Entity entity, IEntityComponent component)
        {
            if (component == null)
            {
                this.LogError(Messages.ERROR_COMPONENT_NULL);
                return null;
            }

            Type type = component.GetType();

            if (!CheckComponentCanBeAdded(entity, type))
                return null;

            EasyCsContainer.HandleInstantiate(component);

            AddComponentInternal(entity, component, type);
            return component;
        }

        public T AddComponent<T>(Entity entity)
            where T : IEntityComponent
        {
            Type type = typeof(T);

            var component = AddComponent(entity, type);

            return (T)component;
        }

        public IEntityComponent AddComponent(Entity entity, Type type)
        {
            if (!CheckComponentCanBeAdded(entity, type))
                return null;

            var component = (IEntityComponent)_container.Instantiate(type);
            AddComponentInternal(entity, component, type);
            return component;
        }

        public IEntityBehavior AddBehavior<T>(Entity entity) where T : IEntityBehavior, new()
        {
            IEntityBehavior result = null;

            Type type = typeof(T);

            if (CheckComponentCanBeAdded(entity, type) == false)
                return null;

            result = new T();

            AddComponentInternal(entity, result, type);

            return result;
        }

        public IEntityBehavior AddBehavior(Entity entity, Type type)
        {
            IEntityBehavior result = null;

            if (CheckComponentCanBeAdded(entity, type) == false)
                return null;

            result = (IEntityBehavior)_container.Instantiate(type);

            AddComponentInternal(entity, result, type);

            return result;
        }

        public IEntityComponent AddData(Entity entity, IEntityDataFactory dataFactory)
        {
            IEntityComponent result = null;

            Type type = dataFactory.GetProductType();

            if (CheckComponentCanBeAdded(entity, type) == false)
                return null;

            result = dataFactory.GetProduct(entity);

            AddComponentInternal(entity, result, type);

            return result;
        }

        public void AddData<T>(Entity entity, T data)
            where T : IEntityData
        {
            Type type = data.GetType();

            if (CheckComponentCanBeAdded(entity, type) == false)
                return;

            AddComponentInternal(entity, data, type);
        }

        public IEntityData SetData<T>(Entity entity, T data)
            where T : IEntityData
        {
            RemoveComponent(entity, data.GetType());
            AddData(entity, data);

            return data;
        }

        public IEntityData SetData(Entity entity, IEntityDataFactory entityDataFactory)
        {
            IEntityData data = entityDataFactory.GetProduct();

            SetData(entity, data);

            return data;
        }

        public T TryGetOrAddBehavior<T>(Entity entity) where T : class, IEntityBehavior, new()
        {
            if (HasComponent<T>(entity))
                return GetComponent<T>(entity);

            return AddBehavior<T>(entity) as T;
        }

        public T TryGetOrAddData<T>(Entity entity) where T : class, IEntityData, new()
        {
            if (HasComponent<T>(entity))
                return GetComponent<T>(entity);

            var data = new T();
            AddData(entity, data);
            return data;
        }

        public T TryGetOrAddComponent<T>(Entity entity) where T : class, IEntityComponent, new()
        {
            if (HasComponent<T>(entity))
                return GetComponent<T>(entity);

            var instance = new T();
            AddComponentInternal(entity, instance, typeof(T));
            return instance;
        }

        public IEntityBehavior TryGetOrAddBehavior(Entity entity, Type type)
        {
            if (HasComponent(entity, type))
                return GetComponent(entity, type) as IEntityBehavior;

            return AddBehavior(entity, type);
        }

        public IEntityData TryGetOrSetData(Entity entity, IEntityDataFactory factory)
        {
            var type = factory.GetProductType();

            if (HasComponent(entity, type))
                return GetComponent(entity, type) as IEntityData;

            var component = factory.GetProduct(entity);
            AddComponentInternal(entity, component, type);
            return component as IEntityData;
        }

        public IEntityComponent TryGetOrAddComponent(Entity entity, Type type)
        {
            if (HasComponent(entity, type))
                return GetComponent(entity, type);

            var instance = (IEntityComponent)_container.Instantiate(type);
            AddComponentInternal(entity, instance, type);
            return instance;
        }

        private void AddComponentInternal(Entity entity, IEntityComponent component, Type type)
        {
            EntityData entityData = GetDataByEntity(entity);

            if (component is IEntityData data)
            {
                entityData._componentsData.Add(data);
            }
            else if (component is IEntityBehavior behavior)
            {
                Dictionary<Type, IEntityComponent> components = entityData._componentsByType;
                Injector.InjectDependencies(behavior, components);
                entityData._componentsBehaviors.Add(behavior);
            }
            else
            {
                this.LogError(string.Format(Messages.ERROR_ADD_COMPONENT_WRONG_TYPE, entity, component));
                return;
            }


            if (component is IRequireEntity hasEntity)
                hasEntity.SetEntity(entity);

            entityData._componentsByType.Add(type, component);

            OnEntityComponentAdded.Invoke(entity, component);
        }

        private bool CheckComponentCanBeAdded<T>(Entity entity, bool logErrorIfNotFound = true)
            where T : IEntityComponent, new()
        {
            Type type = typeof(T);

            bool result = CheckComponentCanBeAdded(entity, type, logErrorIfNotFound);

            return result;
        }

        private bool CheckComponentCanBeAdded(Entity entity, Type type, bool logErrorIfNotFound = true)
        {
            if (IsEntityRegistered(entity) == false)
            {
                if (logErrorIfNotFound)
                    this.LogError(string.Format(Messages.ERROR_ADD_COMPONENT_ENTITY_NOT_REGISTERED, type, entity));
                return false;
            }

            bool hasComponent = HasComponent(entity, type);

            if (hasComponent)
            {
                if (logErrorIfNotFound)
                    this.LogError(string.Format(Messages.ERROR_ADD_COMPONENT_COMPONENT_ALREADY_EXISTS, type, entity));
                return false;
            }

            return true;
        }

        public void RemoveComponent(Entity entity, IEntityComponent component)
        {
            RemoveComponent(entity, component.GetType());
        }

        public void RemoveComponent<T>(Entity entity)
            where T : IEntityComponent
        {
            Type type = typeof(T);
            RemoveComponent(entity, type);
        }

        public void RemoveComponent(Entity entity, Type type)
        {
            if (IsEntityRegistered(entity) == false)
            {
                this.LogError(string.Format(Messages.ERROR_REMOVE_COMPONENT_ENTITY_NOT_REGISTERED, type, entity));
                return;
            }

            IEntityComponent component = GetComponent(entity, type);

            if (component == null)
            {
                return;
            }

            EntityData entityData = GetDataByEntity(entity);

            entityData._componentsByType.Remove(type);

            OnEntityComponentRemoved.Invoke(entity, component);
        }

        public bool HasComponent<T>(Entity entity) where T : class, IEntityComponent
        {
            if (IsEntityRegistered(entity) == false)
            {
                this.LogError(string.Format(Messages.ERROR_HAS_COMPONENT_ENTITY_NOT_REGISTERED, typeof(T), entity));
                return false;
            }

            Type type = typeof(T);
            bool result = HasComponent(entity, type);

            return result;
        }

        public bool HasComponent(Entity entity, Type type)
        {
            if (IsEntityRegistered(entity) == false)
            {
                this.LogError(string.Format(Messages.ERROR_HAS_COMPONENT_ENTITY_NOT_REGISTERED, type, entity));
                return false;
            }

            EntityData entityData = GetDataByEntity(entity);

            if (entityData != null)
            {
                IEntityComponent component = GetComponent(entity, type);

                if (component != null)
                {
                    return true;
                }
            }

            return false;
        }

        public T GetComponent<T>(Entity entity) where T : class, IEntityComponent
        {
            if (IsEntityRegistered(entity) == false)
            {
                this.LogError(string.Format(Messages.ERROR_GET_COMPONENT_ENTITY_NOT_REGISTERED, typeof(T), entity));
                return null;
            }

            Type type = typeof(T);
            IEntityComponent result = GetComponent(entity, type);

            return (T)result;
        }

        public IEntityComponent GetComponent(Entity entity, Type type)
        {
            if (IsEntityRegistered(entity) == false)
            {
                this.LogError(string.Format(Messages.ERROR_GET_COMPONENT_ENTITY_NOT_REGISTERED, type, entity));
                return null;
            }

            IEntityComponent result = null;

            EntityData entityData = GetDataByEntity(entity);

            if (entityData != null)
            {
                entityData._componentsByType.TryGetValue(type, out result);
            }

            return result;
        }

        public IReadOnlyCollection<IEntityComponent> GetAllComponents(Entity entity, bool ignoreChecks = false)
        {
            if (ignoreChecks == false)
                if (IsEntityRegistered(entity) == false)
                {
                    this.LogError(string.Format(Messages.ERROR_GET_ALL_COMPONENTS_ENTITY_NOT_REGISTERED, entity));
                    return null;
                }

            EntityData entityData = GetDataByEntity(entity);
            IReadOnlyCollection<IEntityComponent> result = entityData._componentsByType.Values;

            return result;
        }

        public IReadOnlyCollection<IEntityBehavior> GetAllBehaviors(Entity entity, bool ignoreChecks = false)
        {
            if (ignoreChecks == false)
                if (IsEntityRegistered(entity) == false)
                {
                    this.LogError(string.Format(Messages.ERROR_GET_ALL_COMPONENTS_ENTITY_NOT_REGISTERED, entity));
                    return null;
                }

            EntityData entityData = GetDataByEntity(entity);
            IReadOnlyCollection<IEntityBehavior> result = entityData._componentsBehaviors;

            return result;
        }

        public IReadOnlyCollection<IEntityData> GetAllData(Entity entity, bool ignoreChecks = false)
        {
            if (ignoreChecks == false)
                if (IsEntityRegistered(entity) == false)
                {
                    this.LogError(string.Format(Messages.ERROR_GET_ALL_COMPONENTS_ENTITY_NOT_REGISTERED, entity));
                    return null;
                }

            EntityData entityData = GetDataByEntity(entity);
            IReadOnlyCollection<IEntityData> result = entityData._componentsData;

            return result;
        }

        internal void InternalAttachActorToEntity(Entity entity, Actor actor)
        {
            if (!IsEntityRegistered(entity))
            {
                this.LogError(string.Format(Messages.ERROR_REGISTER_ACTOR_ENTITY_NOT_REGISTERED, actor, entity));
                return;
            }

            // If actor was already attached to a different entity, detach first
            if (_actorByEntity.TryGetValue(actor.Entity, out var existingActor) && existingActor == actor)
            {
                InternalDetachActorFromEntity(actor.Entity, actor);
            }

            EntityData entityData = GetDataByEntity(entity);

            foreach (IActorComponent actorComponent in actor.GetAllComponents())
            {
                Injector.InjectEntityDependencies(actorComponent, entityData._componentsByType);
                Injector.InjectActorDataDependencies(actorComponent, actor.GetAllActorComponentsAndData());
            }

            // Attach to the new entity
            _actorByEntity[entity] = actor;

            OnActorAdded.Invoke(entity, actor);
        }

        internal void InternalDetachActorFromEntity(Entity entity, Actor actor)
        {
            if (!_actorByEntity.TryGetValue(entity, out var registeredActor) || registeredActor != actor)
                return;

            _actorByEntity.Remove(entity);

            OnActorRemoved.Invoke(entity, actor);
        }

        public Actor GetActorByEntity(Entity entity)
        {
            _actorByEntity.TryGetValue(entity, out var result);
            return result;
        }

        private EntityData GetDataByEntity(Entity entity)
        {
            EntityData result = null;

            _dataByEntity.TryGetValue(entity, out result);

            return result;
        }

        public void Dispose()
        {
            foreach (Actor actor in _actorByEntity.Values)
            {
                GameObject.Destroy(actor.gameObject);
            }

            OnDispose.Invoke(this);
        }
    }
}