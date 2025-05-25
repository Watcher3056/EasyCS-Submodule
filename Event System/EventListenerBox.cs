
namespace EasyCS.EventSystem
{
    public class EventListenerBox<T> : IEventListenerBox where T : IEvent
    {
        public void Subscribe(DefaultEventSystem system, object listener, Entity entity)
        {
            system.Subscribe((IEventListener<T>)listener, entity);
        }

        public void Unsubscribe(DefaultEventSystem system, object listener, Entity entity)
        {
            system.Unsubscribe((IEventListener<T>)listener, entity);
        }
    }
}
