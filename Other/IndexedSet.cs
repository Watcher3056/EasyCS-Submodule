using System.Collections;
using System.Collections.Generic;

namespace EasyCS
{
    public class IndexedSet<T> : IEnumerable<T>
    {
        private readonly List<T> _list = new();
        private readonly Dictionary<T, int> _dictionary = new();

        public int Count => _list.Count;

        public bool Add(T item)
        {
            if (_dictionary.ContainsKey(item)) return false;
            _dictionary[item] = _list.Count;
            _list.Add(item);
            return true;
        }

        public bool Remove(T item)
        {
            if (!_dictionary.TryGetValue(item, out var index)) return false;

            var last = _list[^1];
            _list[index] = last;
            _dictionary[last] = index;

            _list.RemoveAt(_list.Count - 1);
            _dictionary.Remove(item);

            return true;
        }

        public bool Contains(T item) => _dictionary.ContainsKey(item);

        public void Clear()
        {
            _list.Clear();
            _dictionary.Clear();
        }

        public T this[int index] => _list[index];

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}