using System;
using System.Collections.Generic;

namespace EasyCS.EventSystem
{

    public interface IEvent { }
    public interface IEventListener { }

    public interface IEventListener<T> : IEventListener where T : IEvent
    {
        void HandleEvent(in EventContext<T> ctx);
    }

    public interface IEventSystem
    {
        void TrySubscribe(object listener, Entity entity);
        void TryUnsubscribe(object listener, Entity entity);
        void Subscribe<T>(IEventListener<T> listener, Entity entity) where T : IEvent;
        void Unsubscribe<T>(IEventListener<T> listener, Entity entity) where T : IEvent;
        void SubscribeGlobal<T>(IEventListener<T> listener) where T : IEvent;
        void UnsubscribeGlobal<T>(IEventListener<T> listener) where T : IEvent;
        void Raise<T>(T evt, Entity entity) where T : IEvent;
        void RaiseGlobal<T>(T evt) where T : IEvent;
        Dictionary<DefaultEventSystem.EventKey, DefferedSparseIndexedSet<IEventListener>> GetSubscriberMap();
        event Action<IEvent> OnEventRaised;
    }
}