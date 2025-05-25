using System;
using System.Collections.Generic;

namespace EasyCS
{
    public interface IEntityBehaviorProvider : IEntityComponentProvider
    {
        public IEntityBehavior Behavior { get; }
        public List<Type> RequiredTypes { get; }

#if UNITY_EDITOR
        public void EditorSetBehavior(IEntityBehavior behavior);
#endif
    }
}
