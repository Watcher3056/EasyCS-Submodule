using System;
using EasyCS.EventSystem;

namespace EasyCS
{
    public interface IEasyCSBehavior : IMonobehavior, IHasContainer, IAwake, IActive
    {
        IEventSystem EventSystem { get; }
        event Action<IEasyCSBehavior> OnBeforeDestroy;

        void InternalOnEnable();
    }
}


