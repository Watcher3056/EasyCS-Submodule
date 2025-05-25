using System.Collections.Generic;

namespace EasyCS.Groups
{
    
    public class Group<TValue> : IGroup
    {
        public IEnumerable<Entity> Entities => Dictionary.Keys;
        public IReadOnlyDictionary<Entity, TValue> Dictionary => _dictionary;

        private Dictionary<Entity, TValue> _dictionary;

        internal Group()
        {
            _dictionary = new Dictionary<Entity, TValue>();
        }

        internal Group(Dictionary<Entity, TValue> dictionary)
        {
            _dictionary = dictionary;
        }

        internal void Add(Entity entity, TValue value)
        {
            _dictionary.Add(entity, value);
        }

        internal void Remove(Entity entity)
        {
            _dictionary.Remove(entity);
        }
    }

}