﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    /// <summary>
    /// 'ItemSwapPresetMap' is typically used to optimize calculations <br/>
    /// of available inventory space when swapping multiple items in the inventory at once <br/>
    /// (for example when trading or swapping bags)
    /// </summary>
    public class ItemSwapPresetMap
    {
        private static int sizePerItem = 6; // Just an approximate value
        private Dictionary<ItemPos, ItemPreset?> map;

        public ItemSwapPresetMap() { }

        public ItemSwapPresetMap(IEnumerable<Item> itemsToSwap)
        {
            map = new(itemsToSwap.Count() * sizePerItem);

            foreach (var item in itemsToSwap)
            {
                if (item != null)
                {
                    map.Add(item.InventoryPosition, ItemPreset.FreeSlot);
                }
            }
        }

        public ItemSwapPresetMap(SortedList<Item,ItemPos> itemsToSwap)
        {
            map = new(itemsToSwap.Count() * sizePerItem);

            foreach (var item in itemsToSwap)
            {
                map.Add(item.Value, ItemPreset.FreeSlot);
            }
        }

        public ItemSwapPresetMap(Item itemToSwap)
        {
            map = new(sizePerItem);

            if (itemToSwap != null)
            {
                map.Add(itemToSwap.InventoryPosition, ItemPreset.FreeSlot);
            }
        }

        public ItemSwapPresetMap(ItemPos positionToSwap)
        {
            map = new(sizePerItem);

            if (positionToSwap.IsExplicitPos)
            {
                map.Add(positionToSwap, ItemPreset.FreeSlot);
            }
        }

        public ItemPreset? this[ItemPos key]
        {
            get => map[key];
            set => map[key] = value;
        }

        public bool TryAdd(ItemPos key, ItemPreset val)
        {
            return map.TryAdd(key, val);
        }

        public bool TryGetValue(ItemPos key, out ItemPreset? val)
        {
            return map.TryGetValue(key, out val);
        }

        public static implicit operator ItemSwapPresetMap(Item itemToSwap)
        {
            return new(itemToSwap);
        }

        public static implicit operator ItemSwapPresetMap(ItemPos positionToSwap)
        {
            return new(positionToSwap);
        }

        public bool IsEmpty => map.Count == 0;
        public bool ContainsKey(ItemPos pos) => map.ContainsKey(pos);
    }
}
