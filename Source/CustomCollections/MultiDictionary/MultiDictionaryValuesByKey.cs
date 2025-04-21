// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class MultiDictionaryValuesByKey<TKey, TValue> : IReadOnlyList<TValue>, IReadOnlyCollection<TValue>
    {
        private MultiDictionary<TKey, TValue> _dictionary;
        private TKey _key;

        public MultiDictionaryValuesByKey(MultiDictionary<TKey, TValue> dictionary, TKey key)
        {
            _dictionary = dictionary;
            _key = key;
        }

        public int Count
        {
            get
            {
                if (_dictionary._storage.TryGetValue(_key, out var list))
                {
                    return list.Count;
                }
                else
                {
                    return 0;
                }
            }
        }

        public bool IsReadOnly => true;

        public TValue this[int index]
        {
            get
            {
                if (_dictionary._storage.TryGetValue(_key, out var list))
                {
                    return list[index];
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            if (_dictionary._storage.TryGetValue(_key, out var list))
            {
                return list.GetEnumerator();
            }
            else
            {
                return new BinarySortedList<TValue>(0).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int IndexOf(TValue item)
        {
            if (_dictionary._storage.TryGetValue(_key, out var list))
            {
                return list.FindFirstIndex(item);
            }
            else
            {
                return -1;
            }
        }

        public bool Contains(TValue item)
        {
            return _dictionary.Contains(_key, item);
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            if (_dictionary._storage.TryGetValue(_key, out var list))
            {
                list.CopyTo(array, arrayIndex);
            }
        }

        public bool Empty()
        {
            if (_dictionary._storage.TryGetValue(_key, out var list))
                return list.Count == 0;

            return true;
        }
    }
}
