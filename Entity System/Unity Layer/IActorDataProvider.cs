
using System;

namespace EasyCS
{
    public interface IActorDataProvider
    {
        public IActorData GetActorData();
        public Type GetActorDataType();
    }
}
