using System;
using System.Collections.Generic;

namespace EasyCS.Groups
{
    public class CustomGroupBuilder
    {
        private const string ErrorAddEntityComponentAlreadyBuilt =
            "Cannot add entity component. Reason: already built. Aborted.";
        private const string ErrorAddActorComponentAlreadyBuilt =
            "Cannot add actor component. Reason: already built. Aborted.";
        private const string ErrorSetActorAlreadyBuilt = 
            "Cannot set Actor. Reason: already built. Aborted.";
        private const string ErrorActorAlreadySet = 
            "Cannot set Actor. Reason: already set. Aborted.";
        private const string ErrorBuildGroupAlreadyBuilt = 
            "Cannot build group. Reason: already built. Aborted.";

        private readonly GroupsSystem _groupsSystem;
        private readonly List<Type> _filtersEntityComponents = new();
        private readonly List<Type> _filtersActorComponents = new();
        private HashCode _hash = new();
        private Type _filterActor;
        private HashSet<IGroup> _groups;

        private CustomGroup _groupResult;
        private bool _hasBeenBuilt;

        public CustomGroupBuilder(GroupsSystem groupsSystem)
        {
            _groupsSystem = groupsSystem ?? throw new ArgumentNullException(nameof(groupsSystem));
            _hash.Add(_groupsSystem);
        }

        public static CustomGroupBuilder Create(GroupsSystem groupsSystem)
        {
            return new CustomGroupBuilder(groupsSystem);
        }

        public CustomGroupBuilder WithActor<TActor>() where TActor : Actor
        {
            if (_hasBeenBuilt)
            {
                this.LogError(ErrorSetActorAlreadyBuilt);
                return this;
            }

            if (_filterActor != null)
            {
                this.LogError(ErrorActorAlreadySet);
                return this;
            }

            _filterActor = typeof(TActor);
            _hash.Add(_filterActor);
            return this;
        }

        public CustomGroupBuilder WithComponent<TComponent>() where TComponent : IEntityComponent
        {
            if (_hasBeenBuilt)
            {
                this.LogError(ErrorAddEntityComponentAlreadyBuilt);
                return this;
            }

            var type = typeof(TComponent);
            if (!_filtersEntityComponents.Contains(type))
            {
                _filtersEntityComponents.Add(type);
                _hash.Add(type);
            }

            return this;
        }

        public CustomGroupBuilder WithActorComponent<TComponent>() where TComponent : ActorComponent
        {
            if (_hasBeenBuilt)
            {
                this.LogError(ErrorAddActorComponentAlreadyBuilt);
                return this;
            }

            var type = typeof(TComponent);
            if (!_filtersActorComponents.Contains(type))
            {
                _filtersActorComponents.Add(type);
                _hash.Add(type);
            }

            return this;
        }

        public CustomGroup Build()
        {
            if (_hasBeenBuilt)
            {
                this.LogError(ErrorBuildGroupAlreadyBuilt);
                return _groupResult;
            }

            if (!_groupsSystem.TryGetCustomGroup(this, out _groupResult))
            {
                _groups = new HashSet<IGroup>();

                if (_filterActor != null)
                {
                    _groups.Add(_groupsSystem.GetGroupWithActor(_filterActor));
                }

                foreach (var filter in _filtersEntityComponents)
                {
                    _groups.Add(_groupsSystem.GetGroupWithComponent(filter));
                }

                foreach (var filter in _filtersActorComponents)
                {
                    _groups.Add(_groupsSystem.GetGroupWithActorComponent(filter));
                }

                _groupResult = new CustomGroup(_groups);
                _groupsSystem.AddCustomGroup(this, _groupResult);
            }

            _hasBeenBuilt = true;
            return _groupResult;
        }

        public override bool Equals(object obj)
        {
            return obj is CustomGroupBuilder other && GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            return _hash.ToHashCode();
        }
    }
}