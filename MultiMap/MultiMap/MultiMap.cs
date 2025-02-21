﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public class MultiMap<TKey, TValue> : IMultiMap<TKey, TValue>
    {
        protected MultiMap(IDictionary<TKey, List<TValue>> dictionary)
        {
            if (dictionary.Count > 0)
                throw new ArgumentException();

            _storage = dictionary;
        }

        public MultiMap()
        {
            _storage = new Dictionary<TKey, List<TValue>>();
        }

        public MultiMap(int capacity)
        {
            _storage = new Dictionary<TKey, List<TValue>>(capacity);
        }

        public MultiMap(IEnumerable<KeyValuePair<TKey, TValue>> initialData)
        {
            _storage = new Dictionary<TKey, List<TValue>>();

            foreach (var item in initialData)
            {
                Add(item);
            }
        }

        public void Add(TKey key, TValue value)
        {
            _version = unchecked(++_version);

            if (!_storage.ContainsKey(key))
                _storage.Add(key, new List<TValue>());

            _storage[key].Add(value);
            ChangeCount(1);
        }

        public void AddRange(TKey key, IEnumerable<TValue> valueList)
        {
            if (!_storage.ContainsKey(key))
            {
                _storage.Add(key, new List<TValue>());
            }

            foreach (TValue value in valueList)
            {
                _version = unchecked(++_version);
                _storage[key].Add(value);
                ChangeCount(1);
            }

            if (!_storage.ContainsKey(key))
            {
                _storage.Remove(key);
            }

        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public bool Remove(TKey key)
        {
            bool result = false;

            if (_storage.TryGetValue(key, out var values))
            {
                int valuesCount = values.Count; 
                _version = unchecked(++_version);
                result = _storage.Remove(key);
                ChangeCount(-valuesCount);
            }

            return result;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key, item.Value);
        }

        public bool Remove(TKey key, TValue value)
        {
            if (!ContainsKey(key))
                return false;

            _version = unchecked(++_version);

            if (_storage[key].Remove(value))
            {
                ChangeCount(-1);

                if (_storage[key].Count == 0)
                    _storage.Remove(key);

                return true;
            }

            return false;
        }

        public bool ContainsKey(TKey key)
        {
            return _storage.ContainsKey(key);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return Contains(item.Key, item.Value);
        }

        public bool Contains(TKey key, TValue item)
        {
            if (!ContainsKey(key))
                return false;

            return _storage[key].Contains(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (!ContainsKey(key))
            {
                value = default;
                return false;
            }

            value = _storage[key].Last();

            return true;
        }

        public IReadOnlyList<TValue> this[TKey key]
        {
            get => new MultiMapValuesByKey<TKey, TValue>(this, key);
            init { }
        }

        public void SetValues(TKey key, List<TValue> valueCollection)
        {
            if (valueCollection.Count == 0)
            {
                Remove(key);
            }
            else
            {
                _version = unchecked(++_version);
                _storage[key] = valueCollection;
                ChangeCount(valueCollection.Count);
            }
        }

        public MultiMapEditableValuesByKey<TKey, TValue> GetValuesToChange(TKey key) => new(this, key);

        public IReadOnlyCollection<TKey> Keys => new MultiMapKeysCollection<TKey, TValue>(this);

        public IReadOnlyCollection<TValue> Values => new MultiMapValuesCollection<TKey,TValue>(this);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            int currentVersion = _version;

            foreach (var pair in _storage)
            {
                foreach (var value in pair.Value)
                {
                    if (currentVersion == _version)
                        yield return new(pair.Key, value);
                    else
                        throw new Exception("MultiMap was changed");
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            _version = unchecked(++_version);
            _storage.Clear();            
            _count = 0;
        }

        public int Count => _count;        

        public bool Empty() => Count == 0;

        public List<TValue> Extract(TKey key)
        {
            if (_storage.TryGetValue(key, out var extract))
            {
                int valuesCount = extract.Count;

                _version = unchecked(++_version);
                _storage.Remove(key);
                ChangeCount(-valuesCount);
            }
            else
            {
                extract = new();
            }

            return extract;
        }

        internal void ChangeCount(int delta)
        {
            _count = unchecked(_count += delta);

            if (_count < 0)
                throw new Exception("MultiMap reached max count of values");

        }

        internal readonly IDictionary<TKey, List<TValue>> _storage;
        private int _count;
        internal int _version;
    }
}
