using TriInspector;
using UnityEngine;

namespace EasyCS
{
    public abstract class EntityDataBase<T> : IEntityData where T : struct
    {
        public ref T ValueRef => ref _value;
        public T Value
        {
            get => _value;
            set => _value = value;
        }

        [SerializeField, ShowInInspector]
        private T _value;

        public object Clone()
        {
            var clone = (EntityDataBase<T>)MemberwiseClone();
            clone._value = _value;
            return clone;
        }

    }
}