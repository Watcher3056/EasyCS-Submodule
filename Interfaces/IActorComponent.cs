using EasyCS.EventSystem;
using UnityEngine;

namespace EasyCS
{
    public interface IActorComponent : IEasyCSBehavior, IEventListener<EventEntityKilled>
    {
        Actor Actor { get; }
        Entity Entity { get; }

        public void SetActor(Actor actor);
        
        void InternalHandleAttachToEntity(Entity curEntity);
        void InternalHandleDetachFromEntity(Entity prevEntity);
    }
}


