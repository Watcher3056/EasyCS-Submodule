using System;

namespace EasyCS
{
    [Serializable]
    public class EntityDataName : EntityDataCustomBase
    {
        public string name;

        public override object Clone()
        {
            return new EntityDataName { name = name };
        }
    }
}
