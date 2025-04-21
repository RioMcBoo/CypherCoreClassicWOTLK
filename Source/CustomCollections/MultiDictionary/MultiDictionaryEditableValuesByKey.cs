// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class MultiDictionaryEditableValuesByKey<TKey, TValue> : IReadOnlyList<TValue>, ICollection<TValue>
    {
        private MultiDictionary<TKey, TValue> _dictionary;
        private TKey _key;

        public MultiDictionaryEditableValuesByKey(MultiDictionary<TKey, TValue> dictionary, TKey key)
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

        public bool IsReadOnly => false;

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

        public void RemoveAt(int index)
        {
            if (_dictionary._storage.TryGetValue(_key, out var list))
            {
                list.RemoveAt(index);
                _dictionary._version = unchecked(_dictionary._version++);
                _dictionary.ChangeCount(-1);

                if (list.Count == 0)
                    _dictionary._storage.Remove(_key);
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        public bool Remove(TValue item)
        {
            return _dictionary.Remove(_key, item);
        }

        public void Add(TValue item)
        {
            _dictionary.Add(_key, item);
        }

        public void Clear()
        {
            _dictionary.Remove(_key);
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
            else
            {
                throw new KeyNotFoundException();
            }
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

        public bool Empty()
        {
            if (_dictionary._storage.TryGetValue(_key, out var list))
                return list.Count == 0;

            return true;
        }
    }
}
