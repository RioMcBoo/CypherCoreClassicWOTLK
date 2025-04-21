// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class MultiMapEditableValuesByKey<TKey, TValue> : IList<TValue>, ICollection<TValue>
    {
        private MultiMap<TKey, TValue> _map;
        private TKey _key;

        public MultiMapEditableValuesByKey(MultiMap<TKey, TValue> map, TKey key)
        {
            _map = map;
            _key = key;
        }

        public int Count
        {
            get
            {
                if (_map._storage.TryGetValue(_key, out var list))
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
                if (_map._storage.TryGetValue(_key, out var list))
                {
                    return list[index];
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }

            set
            {
                if (_map._storage.TryGetValue(_key, out var list))
                {
                    list[index] = value;
                    _map._version = unchecked(_map._version++);
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            if (_map._storage.TryGetValue(_key, out var list))
            {
                return list.GetEnumerator();
            }
            else
            {
                return new List<TValue>(0).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void RemoveAt(int index)
        {
            if (_map._storage.TryGetValue(_key, out var list))
            {
                list.RemoveAt(index);
                _map._version = unchecked(_map._version++);
                _map.ChangeCount(-1);

                if (list.Count == 0)
                    _map._storage.Remove(_key);
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        public void RemoveRange(int index, int count)
        {
            if (_map._storage.TryGetValue(_key, out var list))
            {
                int valuesCount = list.Count;

                list.RemoveRange(index, count);
                _map._version = unchecked(_map._version++);

                _map.ChangeCount(list.Count - valuesCount);

                if (list.Count == 0)
                {
                    _map._storage.Remove(_key);
                }
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        public bool Remove(TValue item)
        {
            return _map.Remove(_key, item);
        }

        public void Insert(int index, TValue item)
        {
            if (_map._storage.TryGetValue(_key, out var list))
            {
                list.Insert(index, item);
                _map._version = unchecked(_map._version++);
                _map.ChangeCount(1);
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        public void Add(TValue item)
        {
            _map.Add(_key, item);
        }

        public void Sort(Comparison<TValue> comparsion)
        {
            if (_map._storage.TryGetValue(_key, out var list))
            {
                _map._version = unchecked(_map._version++);
                list.Sort(comparsion);
            }
        }

        public void Sort(IComparer<TValue>? comparer)
        {
            if (_map._storage.TryGetValue(_key, out var list))
            {
                _map._version = unchecked(_map._version++);
                list.Sort(comparer);
            }
        }

        public void Sort(int index, int count, IComparer<TValue>? comparer)
        {
            if (_map._storage.TryGetValue(_key, out var list))
            {
                _map._version = unchecked(_map._version++);
                list.Sort(index, count, comparer);
            }
        }

        public int IndexOf(TValue item)
        {
            if (_map._storage.TryGetValue(_key, out var list))
            {
                return list.IndexOf(item);
            }
            else
            {
                return -1;
            }
        }

        public void Clear()
        {
            _map.Remove(_key);
        }

        public bool Contains(TValue item)
        {
            return _map.Contains(_key, item);
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            if (_map._storage.TryGetValue(_key, out var list))
            {
                list.CopyTo(array, arrayIndex);
            }
            else
            {
                throw new KeyNotFoundException();
            }
        }

        public bool Empty()
        {
            if (_map._storage.TryGetValue(_key, out var list))
                return list.Count == 0;

            return true;
        }
    }
}
