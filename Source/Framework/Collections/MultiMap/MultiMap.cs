// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System.Linq;

namespace System.Collections.Generic
{
    public class MultiMap<TKey, TValue> : IMultiMap<TKey, TValue>
    {
        protected MultiMap(IDictionary<TKey, List<TValue>> dictionary)
        {
            if (dictionary.Count > 0)
                throw new ArgumentException();

            _interalStorage = dictionary;
        }

        public MultiMap()
        {
            _interalStorage = new Dictionary<TKey, List<TValue>>();
        }

        public MultiMap(int capacity)
        {
            _interalStorage = new Dictionary<TKey, List<TValue>>(capacity);
        }

        public MultiMap(IEnumerable<KeyValuePair<TKey, TValue>> initialData)
        {
            _interalStorage = new Dictionary<TKey, List<TValue>>();

            foreach (var item in initialData)
            {
                Add(item);
            }
        }

        public void Add(TKey key, TValue value)
        {
            if (!_interalStorage.ContainsKey(key))
                _interalStorage.Add(key, new List<TValue>());

            _interalStorage[key].Add(value);
        }

        public void AddRange(TKey key, IEnumerable<TValue> valueList)
        {
            if (!_interalStorage.ContainsKey(key))
            {
                _interalStorage.Add(key, new List<TValue>());
            }

            foreach (TValue value in valueList)
            {
                _interalStorage[key].Add(value);
            }

            if (!_interalStorage.ContainsKey(key))
            {
                _interalStorage.Remove(key);
            }

        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public bool Remove(TKey key)
        {
            return _interalStorage.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key, item.Value);
        }

        public bool Remove(TKey key, TValue value)
        {
            if (!ContainsKey(key))
                return false;

            bool val = _interalStorage[key].Remove(value);
            if (!val)
                return false;

            if (_interalStorage[key].Empty())
                _interalStorage.Remove(key);

            return true;
        }

        public bool ContainsKey(TKey key)
        {
            return _interalStorage.ContainsKey(key);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return Contains(item.Key, item.Value);
        }

        public bool Contains(TKey key, TValue item)
        {
            if (!_interalStorage.ContainsKey(key))
                return false;

            return _interalStorage[key].Contains(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (!_interalStorage.ContainsKey(key))
            {
                value = default;
                return false;
            }

            value = _interalStorage[key].Last();

            return true;
        }

        public IReadOnlyList<TValue> this[TKey key]
        {
            get
            {
                if (!_interalStorage.ContainsKey(key))
                    return new List<TValue>();

                return _interalStorage[key];
            }
        }

        public void SetValues(TKey key, List<TValue> valueCollection)
        {
            if (valueCollection.Count == 0)
                _interalStorage.Remove(key);
            else
                _interalStorage[key] = valueCollection;
        }

        public MultiMapEditableValuesList<TKey, TValue> GetValues(TKey key) => new(_interalStorage, key);

        public IReadOnlyCollection<TKey> Keys => new MultiMapKeysCollection<TKey, TValue>(_interalStorage);

        public IReadOnlyCollection<TValue> Values => new MultiMapValuesCollection<TKey,TValue>(_interalStorage);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var pair in _interalStorage)
            {
                foreach (var value in pair.Value)
                    yield return new(pair.Key, value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            _interalStorage.Clear();
        }

        public int Count => _interalStorage.Aggregate(0, (summ, list) =>
        {
            return summ += list.Value.Count;
        });

        public bool Empty() => Count == 0;

        public List<TValue> Extract(TKey key)
        {
            if (_interalStorage.TryGetValue(key, out var extract))
            {
                _interalStorage.Remove(key);
            }
            else
            {
                extract = new();
            }

            return extract;
        }

        private IDictionary<TKey, List<TValue>> _interalStorage;
    }
}
