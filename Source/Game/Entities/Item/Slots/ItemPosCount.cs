// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.Entities
{
    public record struct ItemPosCount
    {
        /// <summary>ItemPos Hash size is 4 bytes.</summary>
        public const int HashBitSize = 32;

        public ItemPosCount(ItemPos position, int count)
        {
            Position = position;
            Count = count;
        }

        public override int GetHashCode()
        {
            return Count | Position.GetHashCode() << ItemPos.HashBitSize;
        }

        public ItemPos Position;
        public int Count;
    }
}
