using TriInspector;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace EasyCS
{
    public class PrefabRootData : MonoBehaviour, IGUID
    {
        [Serializable]
        public class ActorRelation
        {
            [ReadOnly] public Actor Actor;

            [SerializeReference]
            public ActorRelation Parent;

            [SerializeReference, ListDrawerSettings(AlwaysExpanded = true)]
            public List<ActorRelation> Childs = new();
        }

        [SerializeField]
        private ComponentGUID _guid;

        [SerializeField, ReadOnly]
        private List<Actor> _actors;

        [SerializeField, ReadOnly]
        private List<Component> _hasContainers;

        [SerializeReference, ReadOnly]
        private ActorRelation _rootRelation;

        public IReadOnlyList<Actor> Actors => _actors;
        public IReadOnlyList<Component> HasContainers => _hasContainers;
        public ActorRelation RootRelation => _rootRelation;

        public ComponentGUID GUID => _guid;

        private void OnValidate()
        {
            _actors = new List<Actor>(gameObject.GetComponentsInChildren<Actor>(true));
            _hasContainers = new List<Component>(gameObject.GetComponentsInChildren<IHasContainer>(true).Cast<Component>());
            BuildActorRelations();
            EnsureInitializationOrder();
        }

        private void BuildActorRelations()
        {
            var actorToRelation = new Dictionary<Actor, ActorRelation>();
            foreach (var actor in _actors)
            {
                actorToRelation[actor] = new ActorRelation
                {
                    Actor = actor,
                    Childs = new List<ActorRelation>()
                };
            }

            ActorRelation root = null;

            foreach (var actor in _actors)
            {
                var currentRelation = actorToRelation[actor];
                Actor parentActor = actor.transform.parent != null
                    ? actor.transform.parent.GetComponentInParent<Actor>()
                    : null;

                if (parentActor != null && actorToRelation.TryGetValue(parentActor, out var parentRelation))
                {
                    currentRelation.Parent = parentRelation;
                    parentRelation.Childs.Add(currentRelation);
                }
                else
                {
                    root ??= currentRelation;
                }
            }

            _rootRelation = root;
        }

        private void EnsureInitializationOrder()
        {
            InitializationHelper.SortForInitialization(_hasContainers);
        }
    }
}
