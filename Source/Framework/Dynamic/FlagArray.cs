// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

namespace Framework.Dynamic
{
    public sealed class FlagsArray<T> where T : IBinaryInteger<T>
    {
        private T[] _storage;

        private void CopyArray(T[] parts)
        {
            for (var i = 0; i < _storage.Length; ++i)
                _storage[i] = parts[i];
        }

        public FlagsArray(int length)
        {
            _storage = new T[length];
        }

        public FlagsArray(T[] parts)
        {
            _storage = new T[parts.Length];
            CopyArray(parts);
        }

        public FlagsArray(T[] parts, int length)
        {
            if (parts.Length != length)
                throw new ArgumentException();

            _storage = new T[length];
            CopyArray(parts);
        }

        public static bool operator <(FlagsArray<T> left, FlagsArray<T> right)
        {
            if (left.GetSize() != right.GetSize())
                throw new ArgumentException();

            for (int i = left.GetSize(); i > 0; --i)
            {
                if (left[i - 1] < right[i - 1])
                    return true;
                else if (left[i - 1] > right[i - 1])
                    return false;
            }

            return false;
        }

        public static bool operator >(FlagsArray<T> left, FlagsArray<T> right)
        {
            if (left.GetSize() != right.GetSize())
                throw new ArgumentException();

            for (int i = left.GetSize(); i > 0; --i)
            {
                if (left[i - 1] > right[i - 1])
                    return true;
                else if (left[i - 1] < right[i - 1])
                    return false;
            }

            return false;
        }

        public static FlagsArray<T> operator &(FlagsArray<T> left, FlagsArray<T> right)
        {
            if (left.GetSize() != right.GetSize())
                throw new ArgumentException();

            FlagsArray<T> fl = new(left.GetSize());
            for (var i = 0; i < left.GetSize(); ++i)
                fl[i] = left[i] & right[i];

            return fl;
        }

        public static FlagsArray<T> operator |(FlagsArray<T> left, FlagsArray<T> right)
        {
            if (left.GetSize() != right.GetSize())
                throw new ArgumentException();

            FlagsArray<T> fl = new(left.GetSize());
            for (var i = 0; i < left.GetSize(); ++i)
                fl[i] = left[i] | right[i];

            return fl;
        }

        public static FlagsArray<T> operator ^(FlagsArray<T> left, FlagsArray<T> right)
        {
            if (left.GetSize() != right.GetSize())
                throw new ArgumentException();

            FlagsArray<T> fl = new(left.GetSize());
            for (var i = 0; i < left.GetSize(); ++i)
                fl[i] = left[i] ^ right[i];

            return fl;
        }

        public static implicit operator bool(FlagsArray<T> left)
        {
            for (var i = 0; i < left.GetSize(); ++i)
            {
                if (left[i] != T.Zero)
                    return true;
            }

            return false;
        }

        public int GetSize() => _storage.Length;

        public T this[int i]
        {
            get
            {
                return _storage[i];
            }
            set
            {
                _storage[i] = value;
            }
        }
    }

    public class FlagArray128
    {
        private static readonly int ArraySize = 4;        

        public FlagArray128(uint p1 = 0, uint p2 = 0, uint p3 = 0, uint p4 = 0)
        {
            _storage[0] = p1;
            _storage[1] = p2;
            _storage[2] = p3;
            _storage[3] = p4;
        }

        public FlagArray128(uint[] parts)
        {
            _storage = new(parts, ArraySize);
        }

        public uint this[int i]
        {
            get
            {
                return _storage[i];
            }
            set
            {
                _storage[i] = value;
            }
        }

        public static FlagArray128 operator &(FlagArray128 left, FlagArray128 right)
        {
            return new(left._storage & right._storage);
        }

        public static FlagArray128 operator |(FlagArray128 left, FlagArray128 right)
        {
            return new(left._storage | right._storage);
        }

        public static FlagArray128 operator ^(FlagArray128 left, FlagArray128 right)
        {
            return new(left._storage ^ right._storage);
        }

        public static implicit operator bool(FlagArray128 left)
        {
            return left._storage;
        }

        private FlagArray128(FlagsArray<uint> flagsArray)
        {
            if (flagsArray.GetSize() != ArraySize)
                throw new ArgumentException();

            _storage = flagsArray;
        }

        private FlagsArray<uint> _storage = new(ArraySize);
    }

    public class FlaggedArray32<T> where T : struct
    {
        int[] m_values;
        uint m_flags;

        public FlaggedArray32(byte arraysize)
        {
            m_values = new int[4 * arraysize];
        }

        public uint GetFlags() { return m_flags; }
        public bool HasFlag(T flag) { return (m_flags & 1 << Convert.ToInt32(flag)) != 0; }
        public void AddFlag(T flag) { m_flags |= (dynamic)(1 << Convert.ToInt32(flag)); }
        public void DelFlag(T flag) { m_flags &= ~(dynamic)(1 << Convert.ToInt32(flag)); }

        public int GetValue(T flag) { return m_values[Convert.ToInt32(flag)]; }
        public void SetValue(T flag, object value) { m_values[Convert.ToInt32(flag)] = Convert.ToInt32(value); }
        public void AddValue(T flag, object value) { m_values[Convert.ToInt32(flag)] += Convert.ToInt32(value); }
    }

    public class FlaggedArray64<T> where T : struct
    {
        int[] m_values;
        ulong m_flags;

        public FlaggedArray64(byte arraysize)
        {
            m_values = new int[4 * arraysize];
        }

        public ulong GetFlags() { return m_flags; }
        public bool HasFlag(T flag) { return (m_flags & 1ul << Convert.ToInt32(flag)) != 0; }
        public void AddFlag(T flag) { m_flags |= (dynamic)(1ul << Convert.ToInt32(flag)); }
        public void DelFlag(T flag) { m_flags &= ~(dynamic)(1ul << Convert.ToInt32(flag)); }

        public int GetValue(T flag) { return m_values[Convert.ToInt32(flag)]; }
        public void SetValue(T flag, object value) { m_values[Convert.ToInt32(flag)] = Convert.ToInt32(value); }
        public void AddValue(T flag, object value) { m_values[Convert.ToInt32(flag)] += Convert.ToInt32(value); }
    }
}
