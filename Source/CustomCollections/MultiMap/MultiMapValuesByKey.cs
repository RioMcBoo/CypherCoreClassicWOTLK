// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace System.Collections.Generic
{
    public sealed class MultiMapValuesByKey<TKey, TValue> : IReadOnlyList<TValue>
    {
        private MultiMap<TKey, TValue> _map;
        private TKey _key;

        public MultiMapValuesByKey(MultiMap<TKey, TValue> map, TKey key)
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

        public bool IsReadOnly => true;

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
        }

        public bool Empty()
        {
            if (_map._storage.TryGetValue(_key, out var list))
                return list.Count == 0;

            return true;
        }
    }
}
