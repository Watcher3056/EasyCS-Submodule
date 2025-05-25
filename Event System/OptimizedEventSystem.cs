using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

// --- Placeholder for external types for compilation ---
// (Same as before, ensuring the code is runnable)

namespace EasyCS.EventSystem.Experimental
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

    public struct Entity : IEquatable<Entity>
    {
        public int Id { get; }
        public Entity(int id) { Id = id; }
        public bool Equals(Entity other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Entity other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => $"Entity:{Id}";
        public static Entity Default => new Entity(0);
    }

    public struct EventContext<T> where T : IEvent
    {
        public T Event { get; }
        public Entity Entity { get; }
        public EventContext(T evt, Entity entity)
        {
            Event = evt;
            Entity = entity;
        }
    }

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

        // Caches to determine which EventTypes a listener handles
        private static readonly Dictionary<Type, List<Type>> _listenerToEventTypes = new();

        // NEW CACHES: Maps ListenerType to a function that resolves relevant collections for a given entity.
        // This function will perform the _subscribers dictionary lookups internally.
        private static readonly Dictionary<Type, Func<DefaultEventSystem, Entity, IEnumerable<DefferedSparseIndexedSet<IEventListener>>>> _collectionsResolverForSubscribe = new();
        private static readonly Dictionary<Type, Func<DefaultEventSystem, Entity, IEnumerable<DefferedSparseIndexedSet<IEventListener>>>> _collectionsResolverForUnsubscribe = new();

        // Main storage for subscribers
        private readonly Dictionary<EventKey, DefferedSparseIndexedSet<IEventListener>> _subscribers = new();
        public event Action<IEvent> OnEventRaised;

        static DefaultEventSystem()
        {
            BuildCache();
        }

        private static void BuildCache()
        {
            _listenerToEventTypes.Clear();
            _collectionsResolverForSubscribe.Clear();
            _collectionsResolverForUnsubscribe.Clear();

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

                    // --- Generate and cache Collections Resolver for Subscribe ---
                    if (!_collectionsResolverForSubscribe.ContainsKey(type))
                    {
                        var instanceParam = Expression.Parameter(typeof(DefaultEventSystem), "instance");
                        var entityParam = Expression.Parameter(typeof(Entity), "entity");

                        var resultListVar = Expression.Variable(typeof(List<DefferedSparseIndexedSet<IEventListener>>), "resultCollections");
                        var newResultList = Expression.New(typeof(List<DefferedSparseIndexedSet<IEventListener>>));
                        var assignResultList = Expression.Assign(resultListVar, newResultList);

                        var subscribersField = Expression.Field(instanceParam, "_subscribers");
                        var collectionVar = Expression.Variable(typeof(DefferedSparseIndexedSet<IEventListener>), "collection");
                        var tryGetValueMethod = typeof(Dictionary<EventKey, DefferedSparseIndexedSet<IEventListener>>).GetMethod("TryGetValue", new[] { typeof(EventKey), typeof(DefferedSparseIndexedSet<IEventListener>).MakeByRefType() });
                        var newCollectionCtor = typeof(DefferedSparseIndexedSet<IEventListener>).GetConstructor(Type.EmptyTypes);
                        var eventKeyCtor = typeof(EventKey).GetConstructor(new[] { typeof(Type), typeof(Entity) });
                        var addToListMethod = typeof(List<DefferedSparseIndexedSet<IEventListener>>).GetMethod(nameof(List<DefferedSparseIndexedSet<IEventListener>>.Add));

                        var loopBodyExpressions = new List<Expression>();
                        foreach (var eventType in handledEventTypes)
                        {
                            var newEventKey = Expression.New(eventKeyCtor, Expression.Constant(eventType), entityParam);
                            var callTryGetValue = Expression.Call(subscribersField, tryGetValueMethod, newEventKey, collectionVar);

                            var newCollectionExpr = Expression.New(newCollectionCtor);
                            var assignNewCollection = Expression.Assign(collectionVar, newCollectionExpr);
                            var assignToDictionary = Expression.Assign(Expression.Property(subscribersField, "Item", newEventKey), collectionVar);
                            var createAndAssignCollection = Expression.IfThen(Expression.Not(callTryGetValue), Expression.Block(assignNewCollection, assignToDictionary));

                            loopBodyExpressions.Add(createAndAssignCollection); // Ensure collection exists
                            loopBodyExpressions.Add(Expression.Call(resultListVar, addToListMethod, collectionVar)); // Add to the result list
                        }

                        var finalBlock = Expression.Block(
                            new[] { resultListVar, collectionVar },
                            assignResultList,
                            Expression.Block(loopBodyExpressions),
                            resultListVar
                        );

                        var subscribeResolverLambda = Expression.Lambda<Func<DefaultEventSystem, Entity, IEnumerable<DefferedSparseIndexedSet<IEventListener>>>>(
                            finalBlock, instanceParam, entityParam);

                        _collectionsResolverForSubscribe[type] = subscribeResolverLambda.Compile();
                    }

                    // --- Generate and cache Collections Resolver for Unsubscribe ---
                    if (!_collectionsResolverForUnsubscribe.ContainsKey(type))
                    {
                        var instanceParam = Expression.Parameter(typeof(DefaultEventSystem), "instance");
                        var entityParam = Expression.Parameter(typeof(Entity), "entity");

                        var resultListVar = Expression.Variable(typeof(List<DefferedSparseIndexedSet<IEventListener>>), "resultCollections");
                        var newResultList = Expression.New(typeof(List<DefferedSparseIndexedSet<IEventListener>>));
                        var assignResultList = Expression.Assign(resultListVar, newResultList);

                        var subscribersField = Expression.Field(instanceParam, "_subscribers");
                        var collectionVar = Expression.Variable(typeof(DefferedSparseIndexedSet<IEventListener>), "collection");
                        var tryGetValueMethod = typeof(Dictionary<EventKey, DefferedSparseIndexedSet<IEventListener>>).GetMethod("TryGetValue", new[] { typeof(EventKey), typeof(DefferedSparseIndexedSet<IEventListener>).MakeByRefType() });
                        var eventKeyCtor = typeof(EventKey).GetConstructor(new[] { typeof(Type), typeof(Entity) });
                        var addToListMethod = typeof(List<DefferedSparseIndexedSet<IEventListener>>).GetMethod(nameof(List<DefferedSparseIndexedSet<IEventListener>>.Add));

                        var loopBodyExpressions = new List<Expression>();
                        foreach (var eventType in handledEventTypes)
                        {
                            var newEventKey = Expression.New(eventKeyCtor, Expression.Constant(eventType), entityParam);
                            var callTryGetValue = Expression.Call(subscribersField, tryGetValueMethod, newEventKey, collectionVar);

                            // If found, add to result list. If not found, there's no collection to remove from.
                            var ifFoundAndAdd = Expression.IfThen(callTryGetValue, Expression.Call(resultListVar, addToListMethod, collectionVar));
                            loopBodyExpressions.Add(ifFoundAndAdd);
                        }

                        var finalBlock = Expression.Block(
                            new[] { resultListVar, collectionVar },
                            assignResultList,
                            Expression.Block(loopBodyExpressions),
                            resultListVar
                        );

                        var unsubscribeResolverLambda = Expression.Lambda<Func<DefaultEventSystem, Entity, IEnumerable<DefferedSparseIndexedSet<IEventListener>>>>(
                            finalBlock, instanceParam, entityParam);

                        _collectionsResolverForUnsubscribe[type] = unsubscribeResolverLambda.Compile();
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to subscribe a listener that implements one or more IEventListener<T> interfaces
        /// for events associated with a specific entity. Performs a single lookup for the resolver
        /// and then iterates over the pre-resolved collections.
        /// </summary>
        public void TrySubscribe(object listener, Entity entity)
        {
            if (listener is not IEventListener baseListener) return;
            Type listenerType = listener.GetType();

            if (_collectionsResolverForSubscribe.TryGetValue(listenerType, out var resolverFunc))
            {
                // Execute the pre-compiled resolver function to get all relevant collections
                // This function internally performs the necessary _subscribers dictionary lookups.
                IEnumerable<DefferedSparseIndexedSet<IEventListener>> relevantCollections = resolverFunc(this, entity);

                // Iterate over the resolved collections and add the listener
                foreach (var collection in relevantCollections)
                {
                    // No need for generic T here, as DefferedSparseIndexedSet<IEventListener> accepts IEventListener
                    SubscribeInternalOptimized(baseListener, collection);
                }
            }
        }

        /// <summary>
        /// Attempts to unsubscribe a listener that implements one or more IEventListener<T> interfaces
        /// from events associated with a specific entity. Performs a single lookup for the resolver
        /// and then iterates over the pre-resolved collections.
        /// </summary>
        public void TryUnsubscribe(object listener, Entity entity)
        {
            if (listener is not IEventListener baseListener) return;
            Type listenerType = listener.GetType();

            if (_collectionsResolverForUnsubscribe.TryGetValue(listenerType, out var resolverFunc))
            {
                // Execute the pre-compiled resolver function to get all relevant collections
                IEnumerable<DefferedSparseIndexedSet<IEventListener>> relevantCollections = resolverFunc(this, entity);

                // Iterate over the resolved collections and remove the listener
                foreach (var collection in relevantCollections)
                {
                    // No need for generic T here, as DefferedSparseIndexedSet<IEventListener> accepts IEventListener
                    UnsubscribeInternalOptimized(baseListener, collection);
                }
            }
        }

        /// <summary>
        /// Subscribes a strongly-typed listener for a specific event type and entity.
        /// This method handles the collection lookup before calling the internal add method.
        /// </summary>
        public void Subscribe<T>(IEventListener<T> listener, Entity entity)
            where T : IEvent
        {
            var key = new EventKey(typeof(T), entity);

            if (!_subscribers.TryGetValue(key, out var collection))
            {
                collection = new DefferedSparseIndexedSet<IEventListener>();
                _subscribers[key] = collection;
            }
            // IEventListener<T> converts implicitly to IEventListener when passed to non-generic method
            SubscribeInternalOptimized(listener, collection);
        }

        /// <summary>
        /// Optimized internal method for adding a listener to a known collection.
        /// It does not perform any collection lookup.
        /// (No longer generic, as it accepts IEventListener directly)
        /// </summary>
        private void SubscribeInternalOptimized(IEventListener listener, DefferedSparseIndexedSet<IEventListener> collection)
        {
            collection.Add(listener);
        }

        /// <summary>
        /// Unsubscribes a strongly-typed listener from a specific event type and entity.
        /// This method handles the collection lookup before calling the internal remove method.
        /// </summary>
        public void Unsubscribe<T>(IEventListener<T> listener, Entity entity) where T : IEvent
        {
            var key = new EventKey(typeof(T), entity);
            if (_subscribers.TryGetValue(key, out var collection))
            {
                // IEventListener<T> converts implicitly to IEventListener when passed to non-generic method
                UnsubscribeInternalOptimized(listener, collection);
            }
        }

        /// <summary>
        /// Optimized internal method for removing a listener from a known collection.
        /// It does not perform any collection lookup.
        /// (No longer generic, as it accepts IEventListener directly)
        /// </summary>
        private void UnsubscribeInternalOptimized(IEventListener listener, DefferedSparseIndexedSet<IEventListener> collection)
        {
            collection.Remove(listener);
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

                    // Cast is safe here because collection stores IEventListener, and we're dispatching specific T
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