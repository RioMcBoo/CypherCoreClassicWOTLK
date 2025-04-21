// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class MultiDictionaryKeysCollection<TKey, TValue> : IReadOnlyCollection<TKey>
    {
        private MultiDictionary<TKey, TValue> _dictionary;

        public MultiDictionaryKeysCollection(MultiDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
        }

        public int Count => _dictionary._storage.Count;

        public IEnumerator<TKey> GetEnumerator()
        {
            return _dictionary._storage.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
