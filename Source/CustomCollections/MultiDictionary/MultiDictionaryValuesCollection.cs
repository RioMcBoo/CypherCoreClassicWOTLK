// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class MultiDictionaryValuesCollection<TKey, TValue> : IReadOnlyCollection<TValue>
    {
        private MultiDictionary<TKey, TValue> _dictionary;

        public MultiDictionaryValuesCollection(MultiDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;

        public IEnumerator<TValue> GetEnumerator()
        {
            int currentVersion = _dictionary._version;

            foreach (var list in _dictionary._storage.Values)
            {
                foreach (var item in list)
                {
                    if (currentVersion == _dictionary._version)
                        yield return item;
                    else
                        throw new Exception("MultiDictionary was changed");
                }
                    
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
