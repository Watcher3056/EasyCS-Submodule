using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EasyCS
{
    public static class Injector
    {
        private static readonly BindingFlags _bindingFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly Dictionary<Type, List<FieldInfo>> _injectionCache = new();


        static Injector()
        {
            InitializeInjectionCache();
        }

        private static void InitializeInjectionCache()
        {
            var entityComponentType = typeof(IEntityComponent);
            var actorDataType = typeof(IActorData);
            var actorComponentInterfaceType = typeof(IActorComponent);

            var allTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); } // skip problematic assemblies
                });

            foreach (var type in allTypes)
            {
                if (type.IsInterface || type.IsAbstract)
                    continue;

                // Only scan types that can be injected into
                bool isInjectable =
                    typeof(IEntityBehavior).IsAssignableFrom(type) ||
                    typeof(IActorComponent).IsAssignableFrom(type);

                if (!isInjectable)
                    continue;

                var fields = type.GetFields(_bindingFlags);
                var dataFields = new List<FieldInfo>();

                foreach (var field in fields)
                {
                    if (!Attribute.IsDefined(field, typeof(BindAttribute)))
                        continue;

                    if (entityComponentType.IsAssignableFrom(field.FieldType))
                        dataFields.Add(field);
                    else if (actorDataType.IsAssignableFrom(field.FieldType))
                        dataFields.Add(field);
                    else if (actorComponentInterfaceType.IsAssignableFrom(field.FieldType))
                        dataFields.Add(field);
                }

                if (dataFields.Count > 0)
                    _injectionCache[type] = dataFields;
            }
        }


        private static List<FieldInfo> GetInjectableFields(Type type)
        {
            if (_injectionCache.TryGetValue(type, out var cached))
                return cached;

            return new List<FieldInfo>();
        }

        public static void InjectDependencies(IEntityBehavior behavior,
            Dictionary<Type, IEntityComponent> dataComponents)
        {
            InjectIntoObject(behavior, dataComponents);
        }

        public static void InjectEntityDependencies(IActorComponent component,
            Dictionary<Type, IEntityComponent> dataComponents)
        {
            InjectIntoObject(component, dataComponents);
        }

        public static void InjectActorDataDependencies(object target, IReadOnlyDictionary<Type, object> actorDataProviders)
        {
            var type = target.GetType();
            if (!_injectionCache.TryGetValue(type, out var fields))
                return;

            foreach (var field in fields)
            {
                if (typeof(IEntityComponent).IsAssignableFrom(field.FieldType))
                    continue;

                if (!actorDataProviders.TryGetValue(field.FieldType, out var provider))
                {
                    throw new Exception(string.Format(
                        Messages.MissingActorDataComponent,
                        field.FieldType.Name,
                        field.Name,
                        type.Name
                    ));
                }

                field.SetValue(target, provider);
            }
        }

        public static List<Type> GetRequiredInjectionTypes(Type type)
        {
            var fields = GetInjectableFields(type);
            return fields.Select(f => f.FieldType).ToList();
        }

        public static List<Type> GetRequiredInjectionTypes(IEntityBehavior behavior)
        {
            return GetRequiredInjectionTypes(behavior.GetType());
        }

        private static void InjectIntoObject(object target, IReadOnlyDictionary<Type, IEntityComponent> dataComponents)
        {
            var type = target.GetType();
            var fields = GetInjectableFields(type);

            foreach (var field in fields)
            {
                if (typeof(IEntityComponent).IsAssignableFrom(field.FieldType) == false)
                    continue;

                if (!dataComponents.TryGetValue(field.FieldType, out var dataComponent))
                {
                    throw new Exception(string.Format(
                        Messages.MissingDataComponent,
                        field.FieldType.Name,
                        field.Name,
                        type.Name
                    ));
                }

                field.SetValue(target, dataComponent);
            }
        }

        private static class Messages
        {
            public const string InvalidFieldType =
                "Field '{0}' in '{1}' is marked with [Bind] but is not an IEntityData.";

            public const string MissingDataComponent =
                "No data component of type '{0}' found for injection into field '{1}' in '{2}'.";

            public const string MissingActorDataComponent =
                "No actor data provider of type '{0}' found for injection into field '{1}' in '{2}'.";

        }
    }
}
