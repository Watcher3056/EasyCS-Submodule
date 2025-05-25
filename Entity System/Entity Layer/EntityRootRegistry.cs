using System;
using System.Collections.Generic;

namespace EasyCS
{
    public class EntityRootRegistry
    {
        public static EntityRootRegistry Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new EntityRootRegistry();

                return _instance;
            }
        }
        
        private static EntityRootRegistry _instance;

        // Get Containers by its own Entity
        private Dictionary<Entity, EntityContainer> _entityContainerBySelfEntityLoaded = 
            new Dictionary<Entity, EntityContainer>();
        // Get Container Entity by Child Entity
        private Dictionary<Entity, Entity> _entityContainerByChildEntity = 
            new Dictionary<Entity, Entity>();
        // List of all Containers
        private HashSet<Entity> _entityContainers = 
            new HashSet<Entity>();

        public EntityRootRegistry()
        {
            if (_instance != null)
                throw new Exception("Only one instance of EntityRootRegistry can exist");
            
            _entityContainerByChildEntity = new Dictionary<Entity, Entity>();

            _entityContainers = new HashSet<Entity>();
        }
        
        public EntityContainer GetOrCreateContainer(Entity entity)
        {
            EntityContainer container = null;

            if (_entityContainerBySelfEntityLoaded.TryGetValue(entity, out container) == false)
            {
                container = new EntityContainer(entity);
            }

            return container;
        }

        public void RegisterContainerIfNeeded(EntityContainer container)
        {
            if (_entityContainerBySelfEntityLoaded.ContainsKey(container.Entity) == false)
            {
                if (_entityContainers.Contains(container.Entity) == false)
                {
                    _entityContainers.Add(container.Entity);
                }

                _entityContainerBySelfEntityLoaded.Add(container.Entity, container);

                container.OnDispose += HandleContainerDispose;
                container.OnEntityAdded += HandleEntityAdded;
                container.OnEntityRemoved += HandleEntityRemoved;
            }
        }

        private void HandleEntityAdded(EntityContainer entityContainer, Entity entity)
        {
            _entityContainerByChildEntity.Add(entity, entityContainer.Entity);
        }

        private void HandleEntityRemoved(EntityContainer entityContainer, Entity entity)
        {
            _entityContainerByChildEntity.Remove(entity);
        }

        private void HandleContainerDispose(EntityContainer container)
        {
            _entityContainerBySelfEntityLoaded.Remove(container.Entity);
        }

        public EntityContainer GetContainerByEntity(Entity entity)
        {
            EntityContainer container = null;

            Entity containerSelfEntity = default;
            if (_entityContainerByChildEntity.TryGetValue(entity, out containerSelfEntity))
            {
                if (_entityContainerBySelfEntityLoaded.TryGetValue(containerSelfEntity, out container) == false)
                {
                    container = new EntityContainer(containerSelfEntity);
                    _entityContainerBySelfEntityLoaded.Add(containerSelfEntity, container);
                }
            }

            return container;
        }
    }
}
