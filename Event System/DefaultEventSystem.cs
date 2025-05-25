using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace EasyCS.EventSystem
{

    public class DefaultEventSystem : IEventSystem
    {
        public struct EventKey : IEquatable<EventKey>
        {
            public readonly Type EventType;
            public readonly Entity Entity;

            public EventKey(Type eventType, Entity entity)
            {
                EventType = eventType;
                Entity = entity;
            }

            public bool Equals(EventKey other) => EventType == other.EventType && Entity.Equals(other.Entity);
            public override bool Equals(object obj) => obj is EventKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(EventType, Entity);
            public override string ToString() => $"{EventType.Name} | Entity: {Entity}";
        }

        private static readonly Dictionary<Type, List<Type>> _listenerToEventTypes = new(); // Maps listener type to event types it handles
        private static readonly Dictionary<Type, Action<DefaultEventSystem, IEventListener, Entity>> _cachedSubscribeActions = new(); // Global cache for compiled actions per EventType
        private static readonly Dictionary<Type, Action<DefaultEventSystem, IEventListener, Entity>> _cachedUnsubscribeActions = new(); // Global cache for compiled actions per EventType

        // Maps a concrete listener Type to a list of pre-compiled actions it should execute.
        private static readonly Dictionary<Type, List<Action<DefaultEventSystem, IEventListener, Entity>>> _listenerSpecificSubscribeActions = new();
        private static readonly Dictionary<Type, List<Action<DefaultEventSystem, IEventListener, Entity>>> _listenerSpecificUnsubscribeActions = new();

        private readonly Dictionary<EventKey, DefferedSparseIndexedSet<IEventListener>> _subscribers = new();
        public event Action<IEvent> OnEventRaised;


        static DefaultEventSystem()
        {
            BuildCache();
        }

        private static void BuildCache()
        {
            _listenerToEventTypes.Clear();
            _cachedSubscribeActions.Clear();
            _cachedUnsubscribeActions.Clear();
            _listenerSpecificSubscribeActions.Clear(); // Clear new caches
            _listenerSpecificUnsubscribeActions.Clear(); // Clear new caches

            foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                }))
            {
                if (type == null || !typeof(IEventListener).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
                    continue;

                var eventInterfaces = type
                    .GetInterfaces()
                    .Where(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IEventListener<>))
                    .ToList();

                if (eventInterfaces.Count > 0)
                {
                    var handledEventTypes = eventInterfaces
                        .Select(i => i.GetGenericArguments()[0])
                        .ToList();
                    _listenerToEventTypes[type] = handledEventTypes;

                    // Initialize lists for the new listener-specific caches
                    var subscribeActionsForListener = new List<Action<DefaultEventSystem, IEventListener, Entity>>();
                    var unsubscribeActionsForListener = new List<Action<DefaultEventSystem, IEventListener, Entity>>();

                    foreach (var eventType in handledEventTypes)
                    {
                        // --- Ensure Subscribe Action for this EventType is compiled and cached globally (once) ---
                        if (!_cachedSubscribeActions.TryGetValue(eventType, out var compiledSubscribeAction))
                        {
                            var subscribeInternalMethodInfo = typeof(DefaultEventSystem)
                                .GetMethod(nameof(SubscribeInternal), BindingFlags.NonPublic | BindingFlags.Instance);

                            if (subscribeInternalMethodInfo == null)
                                throw new InvalidOperationException($"Method '{nameof(SubscribeInternal)}' not found. Ensure it's private and correctly named.");

                            var typedSubscribeMethod = subscribeInternalMethodInfo.MakeGenericMethod(eventType);

                            var instanceParam = Expression.Parameter(typeof(DefaultEventSystem), "instance");
                            var listenerParam = Expression.Parameter(typeof(IEventListener), "listener");
                            var entityParam = Expression.Parameter(typeof(Entity), "entity");

                            var castListener = Expression.Convert(listenerParam, typeof(IEventListener<>).MakeGenericType(eventType));

                            var callSubscribe = Expression.Call(instanceParam, typedSubscribeMethod, castListener, entityParam);

                            var subscribeLambda = Expression.Lambda<Action<DefaultEventSystem, IEventListener, Entity>>(
                                callSubscribe, instanceParam, listenerParam, entityParam);

                            compiledSubscribeAction = subscribeLambda.Compile();
                            _cachedSubscribeActions[eventType] = compiledSubscribeAction; // Cache globally
                        }
                        // Add the globally compiled action to the current listener's specific list
                        subscribeActionsForListener.Add(compiledSubscribeAction);

                        // --- Ensure Unsubscribe Action for this EventType is compiled and cached globally (once) ---
                        if (!_cachedUnsubscribeActions.TryGetValue(eventType, out var compiledUnsubscribeAction))
                        {
                            var unsubscribeMethodInfo = typeof(DefaultEventSystem)
                                .GetMethod(nameof(Unsubscribe), BindingFlags.Public | BindingFlags.Instance);

                            if (unsubscribeMethodInfo == null)
                                throw new InvalidOperationException($"Method '{nameof(Unsubscribe)}' not found. Ensure it's public and correctly named.");

                            var typedUnsubscribeMethod = unsubscribeMethodInfo.MakeGenericMethod(eventType);

                            var instanceParam = Expression.Parameter(typeof(DefaultEventSystem), "instance");
                            var listenerParam = Expression.Parameter(typeof(IEventListener), "listener");
                            var entityParam = Expression.Parameter(typeof(Entity), "entity");

                            var castListener = Expression.Convert(listenerParam, typeof(IEventListener<>).MakeGenericType(eventType));

                            var callUnsubscribe = Expression.Call(instanceParam, typedUnsubscribeMethod, castListener, entityParam);

                            var unsubscribeLambda = Expression.Lambda<Action<DefaultEventSystem, IEventListener, Entity>>(
                                callUnsubscribe, instanceParam, listenerParam, entityParam);

                            compiledUnsubscribeAction = unsubscribeLambda.Compile();
                            _cachedUnsubscribeActions[eventType] = compiledUnsubscribeAction; // Cache globally
                        }
                        // Add the globally compiled action to the current listener's specific list
                        unsubscribeActionsForListener.Add(compiledUnsubscribeAction);
                    }
                    // Store the aggregated lists for this listener type
                    _listenerSpecificSubscribeActions[type] = subscribeActionsForListener;
                    _listenerSpecificUnsubscribeActions[type] = unsubscribeActionsForListener;
                }
            }
        }

        /// <summary>
        /// Attempts to subscribe a listener that implements one or more IEventListener<T> interfaces
        /// for events associated with a specific entity. This method uses pre-compiled delegates
        /// stored per listener type to avoid runtime reflection and inner loop lookups.
        /// </summary>
        public void TrySubscribe(object listener, Entity entity)
        {
            if (listener is not IEventListener baseListener) return;

            Type listenerType = listener.GetType();

            // Direct lookup for the list of actions relevant to this specific listener type
            if (_listenerSpecificSubscribeActions.TryGetValue(listenerType, out var specificActions))
            {
                // Iterate and execute each pre-compiled action without further lookups
                foreach (var action in specificActions)
                {
                    action(this, baseListener, entity);
                }
            }
        }

        /// <summary>
        /// Attempts to unsubscribe a listener that implements one or more IEventListener<T> interfaces
        /// from events associated with a specific entity. This method uses pre-compiled delegates
        /// stored per listener type to avoid runtime reflection and inner loop lookups.
        /// </summary>
        public void TryUnsubscribe(object listener, Entity entity)
        {
            if (listener is not IEventListener baseListener) return;

            Type listenerType = listener.GetType();

            // Direct lookup for the list of actions relevant to this specific listener type
            if (_listenerSpecificUnsubscribeActions.TryGetValue(listenerType, out var specificActions))
            {
                // Iterate and execute each pre-compiled action without further lookups
                foreach (var action in specificActions)
                {
                    action(this, baseListener, entity);
                }
            }
        }


        public void Subscribe<T>(IEventListener<T> listener, Entity entity)
            where T : IEvent
        {
            SubscribeInternal(listener, entity);
        }

        private void SubscribeInternal<T>(IEventListener<T> listener, Entity entity)
             where T : IEvent
        {
            var key = new EventKey(typeof(T), entity);

            if (!_subscribers.TryGetValue(key, out var collection))
            {
                collection = new DefferedSparseIndexedSet<IEventListener>();
                _subscribers[key] = collection;
            }

            collection.Add(listener);
        }

        public void Unsubscribe<T>(IEventListener<T> listener, Entity entity) where T : IEvent
        {
            var key = new EventKey(typeof(T), entity);
            if (_subscribers.TryGetValue(key, out var collection))
            {
                collection.Remove(listener);
            }
        }

        public void Raise<T>(T evt, Entity entity) where T : IEvent
        {
            var key = new EventKey(typeof(T), entity);
            if (_subscribers.TryGetValue(key, out var collection))
            {
                IEnumerator<IEventListener> subscribers = collection.GetEnumeratorConcurrent();

                while (subscribers.MoveNext())
                {
                    var subscriber = subscribers.Current;

                    if (subscriber is Behaviour behaviour && !(behaviour.enabled && behaviour.gameObject.activeInHierarchy))
                        continue;

                    if (subscriber is IEventListener<T> typed)
                    {
                        EventContext<T> context = new EventContext<T>(evt, entity);

                        typed.HandleEvent(in context);
                    }
                }
            }

            OnEventRaised?.Invoke(evt);
        }

        public void SubscribeGlobal<T>(IEventListener<T> listener) where T : IEvent => Subscribe(listener, default);
        public void UnsubscribeGlobal<T>(IEventListener<T> listener) where T : IEvent => Unsubscribe(listener, default);
        public void RaiseGlobal<T>(T evt) where T : IEvent => Raise(evt, default);

        public Dictionary<EventKey, DefferedSparseIndexedSet<IEventListener>> GetSubscriberMap() => _subscribers;
    }
}