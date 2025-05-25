using System;

namespace EasyCS
{
    public abstract class ActorData : ActorComponent, IActorData, IActorDataProvider
    {
        public IActorData GetActorData() => this;

        public Type GetActorDataType() => GetType();
    }
}