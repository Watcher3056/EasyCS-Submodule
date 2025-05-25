
using System;
using TriInspector;
using UnityEngine;

namespace EasyCS
{
    public abstract class ActorDataSharedProviderBase<T1, T2> : ActorComponent, IActorDataProvider
        where T1 : IActorData
        where T2 : IActorDataFactory
    {
        [field: SerializeField, Required]
        public T2 DataFactory { get; private set; }
        public T1 Data => (T1)DataFactory.GetProduct();

        public IActorData GetActorData() => Data;

        public Type GetActorDataType() => typeof(T1);
    }
}
