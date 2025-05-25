using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace EasyCS
{

    public static class EntityDataProviderFinder
    {
        private static readonly Dictionary<Type, List<Type>> _matchCache = new();

        static EntityDataProviderFinder()
        {
            InitializeCache();
        }

        public static Type FindEntityDataProviderMatching(Type targetType)
        {
            if (_matchCache.TryGetValue(targetType, out var matches))
            {
                if (matches.Count > 1)
                {
                    Debug.LogError($"Multiple matching EntityDataProviders found for {targetType}: {string.Join(", ", matches.Select(x => x.Name))}");
                }

                return matches.FirstOrDefault();
            }

            return null;
        }

        private static void InitializeCache()
        {
            _matchCache.Clear();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null || !type.IsClass || type.IsAbstract)
                        continue;

                    if (!type.IsSubclassOfRawGeneric(typeof(EntityDataProvider<,>)))
                        continue;

                    var genericBase = GetGenericBase(type, typeof(EntityDataProvider<,>));
                    if (genericBase == null)
                        continue;

                    var genericArguments = genericBase.GetGenericArguments();
                    var tFactory = genericArguments[0];
                    var tComponent = genericArguments[1];

                    AddToCache(tFactory, type);
                    AddToCache(tComponent, type);
                }
            }
        }

        private static void AddToCache(Type key, Type providerType)
        {
            if (!_matchCache.TryGetValue(key, out var list))
            {
                list = new List<Type>();
                _matchCache[key] = list;
            }
            list.Add(providerType);
        }

        private static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        private static Type GetGenericBase(Type type, Type generic)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == generic)
                {
                    return type;
                }
                type = type.BaseType;
            }
            return null;
        }
    }

}
