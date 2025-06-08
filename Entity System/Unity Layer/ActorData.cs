using System;
using TriInspector;

namespace EasyCS
{
    [IconClass(ConstantsIcons.IconActor)]
    public abstract class ActorData : ActorComponent, IActorData, IActorDataProvider
    {
        public IActorData GetActorData() => this;

        public Type GetActorDataType() => GetType();
    }
}