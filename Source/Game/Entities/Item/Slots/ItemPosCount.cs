// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Game.Entities
{
    public readonly record struct ItemPosCount
    {
        public ItemPosCount(ItemPos pos, ushort count = 1)
        {
            Pos = pos;
            Count = count;
        }

        public ItemPosCount(byte slot, byte containerSlot, ushort count = 1)
        {
            Pos = new ItemPos(slot, containerSlot);
            Count = count;
        }

        public ItemPosCount(byte slot, ushort count = 1)
        {
            Pos = new ItemPos(slot);
            Count = count;
        }

        public override int GetHashCode()
        {
            return Pos.GetHashCode() | (Count << ItemPos.HashBitSize);
        }

        //TODO: should use Dictionary
        public bool IsContainedIn(List<ItemPosCount> vec)
        {
            foreach (var posCount in vec)
                if (posCount.Pos == Pos)
                    return true;
            return false;
        }

        public readonly ItemPos Pos;
        public readonly ushort Count;
    }
}
