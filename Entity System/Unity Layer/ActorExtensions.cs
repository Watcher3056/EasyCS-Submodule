using UnityEngine;

namespace EasyCS
{
    public static class ActorExtensions
    {
        public static void Destroy(this Actor actor)
        {
            GameObject.Destroy(actor);
        }

        public static void DestroyGameObject(this Actor actor)
        {
            GameObject.Destroy(actor.gameObject);
        }

        public static void DestroyGameObjectWithoutEntities(this Actor actor)
        {
            DestroyInternal(actor, isRoot: true);
        }

        private static void DestroyInternal(Actor actor, bool isRoot)
        {
            foreach (var child in actor.Childs)
            {
                if (child != null)
                    DestroyInternal(child, isRoot: false);
            }

            actor.EntityContainer.InternalDetachActorFromEntity(actor.Entity, actor);

            if (isRoot)
            {
                GameObject.Destroy(actor.gameObject);
            }
        }

        public static void DestroyActorWithoutEntity(this Actor actor)
        {
            actor.EntityContainer.InternalDetachActorFromEntity(actor.Entity, actor);
            GameObject.Destroy(actor);
        }
    }
}
