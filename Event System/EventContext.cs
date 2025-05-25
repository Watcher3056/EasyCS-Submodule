namespace EasyCS.EventSystem
{
    public interface IEventContext
    {

    }

    public struct EventContext<TEvent> : IEventContext
        where TEvent : IEvent
    {
        public readonly TEvent Event;
        public readonly Entity Entity;

        public EventContext(in TEvent evt, in Entity entity)
        {
            Event = evt;
            Entity = entity;
        }
    }
}