// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class SortedMultiMap<TKey, TValue> : MultiMap<TKey, TValue>
    {
        public SortedMultiMap() : base(new SortedDictionary<TKey, List<TValue>>()) { }

        public SortedMultiMap(IEnumerable<KeyValuePair<TKey, TValue>> initialData) : base(new SortedDictionary<TKey, List<TValue>>())
        {
            foreach (var item in initialData)
            {
                Add(item);
            }
        }
    }
}
