using System;
using System.Collections.Generic;
using System.Linq;

namespace EasyCS
{
    [Serializable, RuntimeOnly]
    public class EntityDataChilds : EntityDataCustomBase
    {
        public HashSet<Entity> childs;

        public override object Clone()
        {
            return new EntityDataChilds { childs = childs.ToHashSet() };
        }
    }
}
