// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class MultiMapValuesCollection<TKey, TValue> : IReadOnlyCollection<TValue>
    {
        IDictionary<TKey, List<TValue>> _dictionary;

        public MultiMapValuesCollection(IDictionary<TKey, List<TValue>> dictionary)
        {
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;

        public IEnumerator<TValue> GetEnumerator()
        {
            foreach (var list in _dictionary.Values)
            {
                foreach (var item in list)
                    yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
