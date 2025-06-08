using System;
using System.Collections.Generic;
using TriInspector;
using UnityEditor;
using UnityEngine;

namespace EasyCS
{
    [IconClass(ConstantsIcons.IconEntity)]
    public abstract class EntityBehaviorProvider<T> : ActorComponent, IEntityBehaviorProvider
        where T : IEntityBehavior
    {
        [SerializeField, HideInInspector]
        private T _behavior;

        public T BehaviorConcrete => _behavior;
        public IEntityBehavior Behavior => _behavior;
        public static Type EntityComponentType { get; } = typeof(T);
        public static List<Type> RequiredTypesCached { get; } = Injector.GetRequiredInjectionTypes(EntityComponentType);
        public List<Type> RequiredTypes => RequiredTypesCached;

        public IEntityComponent GetEntityComponent() => _behavior;

        public Type GetEntityComponentType() => EntityComponentType;

#if UNITY_EDITOR
        public void EditorSetBehavior(IEntityBehavior behavior)
        {
            _behavior = (T)behavior;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
