// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;

namespace Game.Entities
{
    public record struct ItemPreset
    {
        public static readonly ItemPreset EmptyValue = new();

        public ItemPreset(Item item = null, int count = 0)
        {
            if (count < 0)
            {
                throw new ArgumentException();
            }

            if (item == null && count > 0)
            {
                throw new ArgumentException();
            }

            Item = item;
            Count = count;            
        }

        public Item Item;
        public int Count;
    }
}
