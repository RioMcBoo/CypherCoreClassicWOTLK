// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class MultiMapValuesCollection<TKey, TValue> : IReadOnlyCollection<TValue>
    {
        private MultiMap<TKey, TValue> _map;

        public MultiMapValuesCollection(MultiMap<TKey, TValue> map)
        {
            _map = map;
        }

        public int Count => _map.Count;

        public IEnumerator<TValue> GetEnumerator()
        {
            int currentVersion = _map._version;

            foreach (var list in _map._storage.Values)
            {
                foreach (var item in list)
                {
                    if (currentVersion == _map._version)
                        yield return item;
                    else
                        throw new Exception("MultiMap was changed");
                }
                    
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
