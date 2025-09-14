using System;

namespace EasyCS
{
    public interface IActorDataComponent : IActorComponent, IActorData, IActorDataProvider
    {
        new IActorData GetActorData();
        new Type GetActorDataType();
    }
}


