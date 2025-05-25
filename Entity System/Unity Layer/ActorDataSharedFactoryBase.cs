
using System;
using TriInspector;
using UnityEngine;

namespace EasyCS
{
    public abstract class ActorDataSharedFactoryBase : DataFactory, IActorDataFactory
    {
        public abstract IActorData GetActorData();

        public override object GetProduct() => GetActorData();

        IActorData IActorDataFactory.GetProduct() => GetActorData();
    }

    public abstract class ActorDataSharedFactory<T> : ActorDataSharedFactoryBase
        where T: IActorData
    {
        [SerializeField, HideLabel, InlineProperty]
        private T _data;

        public override IActorData GetActorData() => _data;
        public override Type GetProductType() => typeof(T);

#if UNITY_EDITOR
        public void EditorSetData(T data)
        {
            _data = data;

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
