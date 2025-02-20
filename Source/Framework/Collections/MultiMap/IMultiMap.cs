// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public interface IMultiMap<TKey, TValue> : IReadOnlyMultiMap<TKey, TValue>
    {
        void Add(TKey key, TValue value);
        void AddRange(TKey key, IEnumerable<TValue> valueList);
        void Add(KeyValuePair<TKey, TValue> item);

        List<TValue> Extract(TKey key);
        bool Remove(TKey key);
        bool Remove(TKey key, TValue value);
        bool Remove(KeyValuePair<TKey, TValue> item);

        bool ContainsKey(TKey key);
        bool Contains(TKey key, TValue item);
        bool Contains(KeyValuePair<TKey, TValue> item);

        bool Empty();

        IReadOnlyCollection<TKey> Keys { get; }
        IReadOnlyCollection<TValue> Values { get; }

        IReadOnlyList<TValue> this[TKey key] { get; }

        void SetValues(TKey key, List<TValue> valueCollection);
        MultiMapEditableValuesList<TKey, TValue> GetValuesToChange(TKey key);

        void Clear();
        int Count { get; }

        bool TryGetValue(TKey key, out TValue value);
    }

    public interface IReadOnlyMultiMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        bool ContainsKey(TKey key);
        bool Contains(TKey key, TValue item);
        bool Contains(KeyValuePair<TKey, TValue> item);

        bool Empty();

        IReadOnlyCollection<TKey> Keys { get; }
        IReadOnlyCollection<TValue> Values { get; }

        IReadOnlyList<TValue> this[TKey key] { get; }

        void SetValues(TKey key, List<TValue> valueCollection);

        int Count { get; }

        bool TryGetValue(TKey key, out TValue value);
    }
}
