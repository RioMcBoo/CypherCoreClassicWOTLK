﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace System.Collections.Generic
{
    public sealed class SortedMultiDictionary<TKey, TValue> : MultiDictionary<TKey, TValue>
    {
        public SortedMultiDictionary() : base(new SortedDictionary<TKey, BinarySortedList<TValue>>()) { }

        public SortedMultiDictionary(IEnumerable<KeyValuePair<TKey, TValue>> initialData) : base(new SortedDictionary<TKey, BinarySortedList<TValue>>())
        {
            foreach (var item in initialData)
            {
                Add(item);
            }
        }
    }
}
