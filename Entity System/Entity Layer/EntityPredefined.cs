using System;
using UnityEngine;

namespace EasyCS
{
    [Serializable]
    public class EntityPredefined : IHasEntity
    {
        [SerializeField]
        private ComponentGUID _componentGUID;
        public Entity Entity
        {
            get
            {
                if (_entity.IsAlive == false)
                {
                    Guid guid = _componentGUID.GetGuid();
                    _entity = new Entity(guid);
                }

                return _entity;
            }
        }
        private Entity _entity;
    }
}
