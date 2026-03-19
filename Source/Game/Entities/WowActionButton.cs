// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.Entities
{
    public struct WowActionButton
    {
        uint _packedData;

        public WowActionButton(uint packedData)
        {
            _packedData = packedData;
        }

        public WowActionButton(int type, int value)
        {
            _packedData = MakePackedData(type, value);
        }

        public uint PackedData => _packedData;

        public int Type
        {
            get => GetType(_packedData);
            set => _packedData = MakePackedData(value, Value);
        }

        public int Value
        {
            get => GetValue(_packedData);
            set => _packedData = MakePackedData(Type, value);
        }

        public static uint MakePackedData(int type, int value)
        {
            uint packedData = (uint)((type << 23) | value);

            Cypher.Assert(type == GetType(packedData), "'Type' value out of range");
            Cypher.Assert(value == GetValue(packedData), "'Value' value out of range");

            return packedData;
        }

        static int GetValue(uint packedData)
        {
            return (int)(packedData & 0x007FFFFF);
        }

        static int GetType(uint packedData)
        {
            return (int)((packedData & 0xFF800000) >>> 23);
        }

        public static implicit operator uint(WowActionButton button)
        {
            return button._packedData;
        }

        public static bool operator ==(WowActionButton left, WowActionButton right)
        {
            return left._packedData == right._packedData;
        }

        public static bool operator !=(WowActionButton left, WowActionButton right)
        {
            return !(left == right);
        }

        public bool Equals(WowActionButton other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return obj is WowActionButton other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _packedData.GetHashCode();
        }
    }
}
