using System;
using TriInspector;
using UnityEngine;

namespace EasyCS
{
    public abstract class EntityDataFactoryBase : DataFactory, IEntityDataFactory
    {
        public abstract IEntityData GetProduct(Entity entity);
    }
    public abstract class EntityDataFactory<T> : EntityDataFactoryBase
        where T : IEntityData
    {
        [SerializeField, HideLabel, InlineProperty]
        private T _data;

        public override object GetProduct() => ((IEntityDataFactory)this).GetProduct();
        public override IEntityData GetProduct(Entity entity) => (T)_data.Clone();
        public override Type GetProductType() => typeof(T);

#if UNITY_EDITOR
        public void EditorSetData(T data)
        {
            _data = data;

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
