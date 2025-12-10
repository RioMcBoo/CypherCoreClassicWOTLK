// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System.IO;

namespace System.Collections.Generic
{
    public class Array<T> : List<T>
    {
        public Array(int size) : base(size) { }

        public Array(params T[] args) : base(args) { }

        public Array(int size, T defaultFillValue) : base(size)
        {
            Fill(defaultFillValue);
        }

        public void Fill(T value)
        {
            for (var i = 0; i < base.Capacity; ++i)
                Add(value);
        }
       
        public new void Add(T item)
        {
            if (Count >= base.Capacity)
                throw new InternalBufferOverflowException($"Attempted to read more array elements from packet {Count + 1} than allowed {base.Capacity}");

            base.Add(item);
        }

        public new T this[int index]
        {
            get
            {
                return base[index];
            }
            set
            {
                if (index >= Count)
                {
                    if (Count >= base.Capacity)
                        throw new InternalBufferOverflowException($"Attempted to read more array elements from packet {Count + 1} than allowed {base.Capacity}");

                    Insert(index, value);
                }
                else
                    base[index] = value;
            }
        }

        public int GetLimit() { return base.Capacity; }

        public static implicit operator T[] (Array<T> array)
        {
            return array.ToArray();
        }
    }
}
