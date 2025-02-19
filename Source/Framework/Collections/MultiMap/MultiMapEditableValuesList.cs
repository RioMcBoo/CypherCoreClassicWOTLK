// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System.Linq;

namespace System.Collections.Generic
{
    public sealed class MultiMapEditableValuesList<TKey, TValue> : IList<TValue>
    {
        IDictionary<TKey, List<TValue>> _dictionary;
        TKey _key;

        public MultiMapEditableValuesList(IDictionary<TKey, List<TValue>> dictionary, TKey key)
        {
            _dictionary = dictionary;
            _key = key;
        }

        public int Count => _dictionary[_key].Count;

        public bool IsReadOnly => throw new NotImplementedException();

        public TValue this[int index] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IEnumerator<TValue> GetEnumerator()
        {
            return _dictionary[_key].GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void RemoveRange(int index, int count)
        {
            if (_dictionary.TryGetValue(_key, out var list))
            {
                list.RemoveRange(index, count);
                if (list.Count == 0)
                {
                    _dictionary.Remove(_key);
                }
            }
        }

        public void Sort(Comparison<TValue> comparsion)
        {
            if (_dictionary.TryGetValue(_key, out var list))
            {
                list.Sort(comparsion);
            }
        }

        public void Sort(IComparer<TValue>? comparer)
        {
            if (_dictionary.TryGetValue(_key, out var list))
            {
                list.Sort(comparer);
            }
        }

        public void Sort(int index, int count, IComparer<TValue>? comparer)
        {
            if (_dictionary.TryGetValue(_key, out var list))
            {
                list.Sort(index, count, comparer);
            }
        }

        public int IndexOf(TValue item)
        {
            if (_dictionary.TryGetValue(_key, out var list))
            {
                return list.IndexOf(item);
            }

            return -1;
        }

        public void Insert(int index, TValue item)
        {
            if (_dictionary.TryGetValue(_key, out var list))
            {
                list.Insert(index, item);
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        public void RemoveAt(int index)
        {
            if (_dictionary.TryGetValue(_key, out var list))
            {
                list.RemoveAt(index);
                if (list.Count == 0)
                    _dictionary.Remove(_key);
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        public void Add(TValue item)
        {
            if (!_dictionary.TryGetValue(_key, out var list))
            {
                _dictionary[_key] = new List<TValue>();
            }

            _dictionary[_key].Add(item);
        }

        public void Clear()
        {
            _dictionary.Remove(_key);
        }

        public bool Contains(TValue item)
        {
            if (_dictionary.TryGetValue(_key, out var list))
            {
                return list.Contains(item);
            }

            return false;
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            if (_dictionary.TryGetValue(_key, out var list))
            {
                list.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(TValue item)
        {
            bool result = false;
            if (_dictionary.TryGetValue(_key, out var list))
            {
                result = list.Remove(item);
                if (list.Count == 0)
                {
                    _dictionary.Remove(_key);
                }
            }

            return result;
        }

        public bool Empty()
        {
            if (_dictionary.TryGetValue(_key, out var list))
                return list.Empty();

            return true;
        }
    }
}
