// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public interface IMultiDictionary<TKey, TValue> : IReadOnlyMultiDictionary<TKey, TValue>
    {
        void Add(TKey key, TValue value);
        void AddRange(TKey key, IEnumerable<TValue> valueCollection);
        void Add(KeyValuePair<TKey, TValue> item);

        BinarySortedList<TValue> Extract(TKey key);
        bool Remove(TKey key);
        bool Remove(TKey key, TValue value);
        bool Remove(KeyValuePair<TKey, TValue> item);

        void SetValues(TKey key, BinarySortedList<TValue> valueList);
        MultiDictionaryEditableValuesByKey<TKey, TValue> GetValuesToChange(TKey key);

        void Clear();
    }

    public interface IReadOnlyMultiDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        bool ContainsKey(TKey key);
        bool Contains(TKey key, TValue item);
        bool Contains(KeyValuePair<TKey, TValue> item);

        bool Empty();

        IReadOnlyCollection<TKey> Keys { get; }
        IReadOnlyCollection<TValue> Values { get; }

        IReadOnlyList<TValue> this[TKey key] { get; init; }

        int Count { get; }

        bool TryGetValue(TKey key, out TValue value);
    }
}
