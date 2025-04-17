// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class MultiMapKeysCollection<TKey, TValue> : IReadOnlyCollection<TKey>
    {
        private MultiMap<TKey, TValue> _map;

        public MultiMapKeysCollection(MultiMap<TKey, TValue> map)
        {
            _map = map;
        }

        public int Count => _map._storage.Count;

        public IEnumerator<TKey> GetEnumerator()
        {
            return _map._storage.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
