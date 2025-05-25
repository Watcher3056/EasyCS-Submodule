using System;
using System.Collections.Generic;

namespace EasyCS.Groups
{
    public class GroupsSystem : IAwake, IHasContainer, IDisposable
    {
        public IEasyCSObjectResolver EasyCsContainer { get; private set; }

        private Group<Actor> _groupActors;
        private Dictionary<Type, Group<Actor>> _groupsByActorType;
        private Dictionary<Type, Group<IEntityComponent>> _groupsByEntityComponentType;
        private Dictionary<Type, Group<ActorComponent>> _groupsByActorComponentType;
        private Dictionary<CustomGroupBuilder, CustomGroup> _customGroups;
        private EntityContainer _entityContainer;

        public void SetupContainer(IEasyCSObjectResolver container)
        {
            EasyCsContainer = container;
            _entityContainer = container.Resolve<EntityContainer>();
        }

        public void OnAwake()
        {
            _groupActors = new Group<Actor>();
            _groupsByActorType = new Dictionary<Type, Group<Actor>>();
            _groupsByEntityComponentType = new Dictionary<Type, Group<IEntityComponent>>();
            _groupsByActorComponentType = new Dictionary<Type, Group<ActorComponent>>();
            _customGroups = new Dictionary<CustomGroupBuilder, CustomGroup>();

            foreach (Actor actor in _entityContainer.ActorsAll)
                HandleActorAdded(actor.Entity, actor);

            foreach (Entity entity in _entityContainer.EntitiesAll)
                HandleEntityAdded(_entityContainer, entity);

            _entityContainer.OnActorAdded += HandleActorAdded;
            _entityContainer.OnActorRemoved += HandleActorRemoved;
            _entityContainer.OnEntityAdded += HandleEntityAdded;
            _entityContainer.OnBeforeEntityRemoved += HandleEntityRemoved;
            _entityContainer.OnEntityComponentAdded += HandleEntityComponentAdded;
            _entityContainer.OnEntityComponentRemoved += HandleEntityComponentRemoved;
        }

        public void Dispose()
        {
            _entityContainer.OnActorAdded -= HandleActorAdded;
            _entityContainer.OnActorRemoved -= HandleActorRemoved;
            _entityContainer.OnEntityAdded -= HandleEntityAdded;
            _entityContainer.OnBeforeEntityRemoved -= HandleEntityRemoved;
            _entityContainer.OnEntityComponentAdded -= HandleEntityComponentAdded;
            _entityContainer.OnEntityComponentRemoved -= HandleEntityComponentRemoved;
        }

        private void HandleEntityComponentRemoved(Entity entity, IEntityComponent component)
        {
            Type componentType = component.GetType();
            var group = GetGroupWithComponent(componentType);
            group.Remove(entity);
            ScoreEntityInCustomGroupsByGroup(entity, group, -1);
        }

        private void HandleEntityComponentAdded(Entity entity, IEntityComponent component)
        {
            Type componentType = component.GetType();
            var group = GetGroupWithComponent(componentType);
            group.Add(entity, component);
            ScoreEntityInCustomGroupsByGroup(entity, group, 1);
        }

        private void HandleEntityRemoved(EntityContainer _, Entity entity)
        {
            _groupActors.Remove(entity);
            var components = _entityContainer.GetAllComponents(entity);
            foreach (var component in components)
            {
                HandleEntityComponentRemoved(entity, component);
            }

            foreach (var group in _customGroups.Values)
                group.Remove(entity);
        }

        private void HandleEntityAdded(EntityContainer _, Entity entity)
        {
            var components = _entityContainer.GetAllComponents(entity);
            foreach (var component in components)
                HandleEntityComponentAdded(entity, component);
        }

        private void HandleActorAdded(Entity entity, Actor actor)
        {

            Type actorType = actor.GetType();
            var group = GetGroupWithActor(actorType);

            group.Add(entity, actor);
            ScoreEntityInCustomGroupsByGroup(entity, group, 1);

            if (group != _groupActors)
            {
                _groupActors.Add(entity, actor);
                ScoreEntityInCustomGroupsByGroup(entity, _groupActors, 1);
            }

            var components = actor.GetAllComponents();
            foreach (var component in components)
                HandleActorComponentAdded(actor, component);

            actor.OnComponentAdded += HandleActorComponentAdded;
            actor.OnComponentRemoved += HandleActorComponentRemoved;
        }

        private void HandleActorRemoved(Entity entity, Actor actor)
        {
            _groupActors.Remove(entity);

            Type actorType = actor.GetType();
            var group = GetGroupWithActor(actorType);
            group.Remove(entity);

            ScoreEntityInCustomGroupsByGroup(entity, group, -1);
            ScoreEntityInCustomGroupsByGroup(entity, _groupActors, -1);

            var components = actor.GetAllComponents();
            foreach (var component in components)
                HandleActorComponentRemoved(actor, component);

            actor.OnComponentAdded -= HandleActorComponentAdded;
            actor.OnComponentRemoved -= HandleActorComponentRemoved;
        }

        private void HandleActorComponentRemoved(Actor actor, ActorComponent component)
        {
            Type componentType = component.GetType();
            var group = GetGroupWithActorComponent(componentType);
            group.Remove(actor.Entity);
            ScoreEntityInCustomGroupsByGroup(actor.Entity, group, -1);
        }

        private void HandleActorComponentAdded(Actor actor, ActorComponent component)
        {
            Type componentType = component.GetType();
            var group = GetGroupWithActorComponent(componentType);
            group.Add(actor.Entity, component);
            ScoreEntityInCustomGroupsByGroup(actor.Entity, group, 1);
        }

        public void AddCustomGroup(CustomGroupBuilder groupBuilder, CustomGroup group)
        {
            if (!_customGroups.ContainsKey(groupBuilder))
                _customGroups.Add(groupBuilder, group);
        }

        public bool TryGetCustomGroup(CustomGroupBuilder builder, out CustomGroup group)
        {
            bool success = _customGroups.TryGetValue(builder, out group);
            return success;
        }

        public Group<IEntityComponent> GetGroupWithComponent<T>() where T : IEntityComponent
            => GetGroupWithComponent(typeof(T));

        public Group<IEntityComponent> GetGroupWithComponent(Type type)
            => GetOrCreateGroup(type, _groupsByEntityComponentType, () => new Group<IEntityComponent>());

        public Group<Actor> GetGroupWithActor<T>() where T : Actor
            => GetGroupWithActor(typeof(T));

        public Group<Actor> GetGroupWithActor(Type type)
            => type == typeof(Actor)
                ? _groupActors
                : GetOrCreateGroup(type, _groupsByActorType, () => new Group<Actor>());

        public Group<ActorComponent> GetGroupWithActorComponent<T>() where T : ActorComponent
            => GetGroupWithActorComponent(typeof(T));

        public Group<ActorComponent> GetGroupWithActorComponent(Type type)
            => GetOrCreateGroup(type, _groupsByActorComponentType, () => new Group<ActorComponent>());

        private Group<T> GetOrCreateGroup<T>(
            Type type,
            Dictionary<Type, Group<T>> dictionary,
            Func<Group<T>> groupFactory)
        {
            if (!dictionary.TryGetValue(type, out var group))
            {
                group = groupFactory();
                dictionary[type] = group;
            }

            return group;
        }

        private void ScoreEntityInCustomGroupsByGroup(Entity entity, IGroup group, int scoreDelta)
        {
            foreach (var customGroup in _customGroups.Values)
            {
                if (customGroup.ContainsGroup(group))
                {
                    customGroup.ScoreEntity(entity, scoreDelta);
                }
            }
        }
    }
}