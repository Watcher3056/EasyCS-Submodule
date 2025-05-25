
namespace EasyCS.EventSystem
{
    public interface IEventListenerBox
    {
        void Subscribe(DefaultEventSystem system, object listener, Entity entity);
        void Unsubscribe(DefaultEventSystem system, object listener, Entity entity);
    }

}
