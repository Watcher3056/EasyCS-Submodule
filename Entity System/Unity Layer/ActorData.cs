using System;
using TriInspector;

namespace EasyCS
{
    [IconClass(ConstantsIcons.IconActor)]
    public abstract class ActorData : ActorComponent, IActorDataComponent
    {
        public IActorData GetActorData() => this;

        public Type GetActorDataType() => GetType();
    }
}