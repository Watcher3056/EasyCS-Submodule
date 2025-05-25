using System;
using System.Collections.Generic;
using System.Linq;
using TriInspector;

namespace EasyCS
{
    public struct Entity : IEquatable<Entity>
    {
        public static Entity Empty => _empty;
        public static IReadOnlyCollection<Entity> EmptyChilds { get; } = new List<Entity>();
        private static Entity _empty = new Entity(Guid.Empty);

        public Guid ID => _id;
        private Guid _id;
        [ReadOnly, ShowInInspector]
        public bool IsAlive => IsEmpty == false && EntityContainer != null;
        [ReadOnly, ShowInInspector]
        public bool IsEmpty => this.Equals(_empty);
        public EntityContainer EntityContainer => EntityRootRegistry.Instance.GetContainerByEntity(this);
        [ReadOnly, ShowInInspector, HideIf("IsEmpty")]
        public Actor Actor =>
            EntityContainer?.GetActorByEntity(this);

        public IReadOnlyCollection<IEntityData> DataComponents => EntityContainer?.GetAllData(this);
        public IReadOnlyCollection<IEntityBehavior> BehaviorComponents => EntityContainer?.GetAllBehaviors(this);

#if UNITY_EDITOR
        [ReadOnly, ShowInInspector, LabelText("Guid"), HideIf("IsEmpty")]
        private string EditorId => _id.ToString();
        [ReadOnly, ShowInInspector, LabelText("Data Components"), HideIf("IsEmpty")]
        private List<IEntityData> EditorDataComponents => DataComponents?.ToList();
        [ReadOnly, ShowInInspector, LabelText("Behavior Components"), HideIf("IsEmpty")]
        private List<IEntityBehavior> EditorBehaviorComponents => BehaviorComponents?.ToList();

        [ReadOnly, ShowInInspector, LabelText("Root"), HideIf("IsEmpty")]
        private Entity? EditorRoot
        {
            get
            {
                Entity? root = this;
                Entity? current = this;
                while (true)
                {
                    current = ((Entity)current).EditorParent;
                    if (current != null)
                        root = current;
                    else
                        break;
                }

                if (root.Equals(this))
                    return null;

                return root;
            }
        }
        [ReadOnly, ShowInInspector, LabelText("Parent"), HideIf("IsEmpty")]
        private Entity? EditorParent => this.GetComponent<EntityDataParent>()?.Value ?? null;

        [ReadOnly, ShowInInspector, LabelText("Childs"), HideIf("IsEmpty")]
        private List<Entity> EditorChilds => Childs?.ToList();
#endif

        public Entity Root
        {
            get
            {
                Entity root = this;
                Entity current = this;
                while (true)
                {
                    current = current.Parent;
                    if (current.Equals(Empty) == false)
                        root = current;
                    else
                        break;
                }

                return root;
            }
        }
        public Entity Parent => this.GetComponent<EntityDataParent>()?.Value ?? Empty;

        public IReadOnlyCollection<Entity> Childs => this.GetComponent<EntityDataChilds>()?.childs ?? EmptyChilds;

        public static Entity CreateNew()
        {
            Entity result = new Entity(Guid.NewGuid());

            return result;
        }

        public Entity(Guid id)
        {
            _id = id;
        }

        public bool Equals(Entity other)
        {
            return other.ID.Equals(this.ID);
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is Entity)
                return Equals((Entity)obj);
            else
                return false;
        }

        public override string ToString()
        {
            return _id.ToString();
        }

        public static bool operator ==(Entity e1, Entity e2)
        {
            return e1.Equals(e2);
        }

        public static bool operator !=(Entity e1, Entity e2)
        {
            return !e1.Equals(e2);
        }
    }
}
