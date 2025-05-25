using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace EasyCS
{
    public class DefferedSparseIndexedSet<T> : IEnumerable<T>, IDisposable
        where T : class
    {
        private readonly List<T> _list;
        private readonly Dictionary<T, int> _dictionary;

        private readonly HashSet<T> _pendingAdd;
        private readonly List<T> _pendingAddSnapshot;
        private readonly Stack<int> _freeIndexes;

        public int CountAll => _list.Count;
        public int CountActive => _dictionary.Count;

        public DefferedSparseIndexedSet(int capacity = 1000)
        {
            _list = ListPool<T>.Get();
            _list.Capacity = capacity;

            _pendingAddSnapshot = ListPool<T>.Get();
            _pendingAddSnapshot.Capacity = capacity;

            _dictionary = DictionaryPool<T, int>.Get();
            _dictionary.EnsureCapacity(capacity);

            _pendingAdd = HashSetPool<T>.Get();
            _pendingAdd.EnsureCapacity(capacity);

            _freeIndexes = new Stack<int>(capacity); // Unity has no StackPool yet
        }

        public void Dispose()
        {
            ListPool<T>.Release(_list);
            ListPool<T>.Release(_pendingAddSnapshot);
            DictionaryPool<T, int>.Release(_dictionary);
            HashSetPool<T>.Release(_pendingAdd);
        }

        public bool Add(T item)
        {
            if (_dictionary.ContainsKey(item) || _pendingAdd.Contains(item))
                return false;

            _pendingAdd.Add(item);
            return true;
        }

        public bool Remove(T item)
        {
            if (_pendingAdd.Remove(item))
                return true;

            if (!_dictionary.TryGetValue(item, out int index))
                return false;

            _list[index] = null;
            _dictionary.Remove(item);
            _freeIndexes.Push(index);

            return true;
        }

        public void ApplyChanges()
        {
            if (_pendingAdd.Count == 0)
                return;

            var enumerator = _pendingAdd.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var item = enumerator.Current;
                if (_freeIndexes.Count > 0)
                {
                    int freeIndex = _freeIndexes.Pop();
                    _list[freeIndex] = item;
                    _dictionary[item] = freeIndex;
                }
                else
                {
                    _list.Add(item);
                    _dictionary[item] = _list.Count - 1;
                }
            }
            _pendingAdd.Clear();
        }

        public bool Contains(T item) => _dictionary.ContainsKey(item) || _pendingAdd.Contains(item);

        public void Clear()
        {
            _list.Clear();
            _dictionary.Clear();
            _pendingAdd.Clear();
            _freeIndexes.Clear();
        }

        public T this[int index] => _list[index];

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _list.Count; i++)
            {
                var item = _list[i];
                if (item != null)
                    yield return item;
            }
        }

        public IEnumerator<T> GetEnumeratorConcurrent()
        {
            IEnumerator<T> enumerator = GetEnumerator();

            // Phase 1: Iterate with default enumerator
            while (enumerator.MoveNext())
                yield return enumerator.Current;


            if (_pendingAdd.Count == 0)
                yield break;

            // Phase 2: Iterate a snapshot of pending items
            // This prevents InvalidOperationException if _pendingAdd is modified during this phase.
            // Limitation: Items added to _pendingAdd *after* this snapshot is created
            // will not be yielded in this specific enumeration pass.

            // Create a snapshot
            _pendingAddSnapshot.AddRange(_pendingAdd);

            foreach (T pendingItemInSnapshot in _pendingAddSnapshot)
            {
                // Check if the item is *still* in the live _pendingAdd set.
                // This handles cases where an item was in the snapshot but got removed
                // from _pendingAdd (e.g., by a Remove() call or by ApplyChanges() on another thread)
                // before this point in the loop was reached for this specific item.
                if (_pendingAdd.Contains(pendingItemInSnapshot))
                {
                    yield return pendingItemInSnapshot;
                }
            }

            _pendingAddSnapshot.Clear();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // Internal getters for testing
        internal IEnumerable<T> GetCommittedItems() => _dictionary.Keys;
        internal IEnumerable<T> GetPendingAddItems() => _pendingAdd;
    }
}
