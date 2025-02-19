// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class MultiMapKeysCollection<TKey, TValue> : IReadOnlyCollection<TKey>
    {
        IDictionary<TKey, List<TValue>> _dictionary;

        public MultiMapKeysCollection(IDictionary<TKey, List<TValue>> dictionary)
        {
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;

        public IEnumerator<TKey> GetEnumerator()
        {
            return _dictionary.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
