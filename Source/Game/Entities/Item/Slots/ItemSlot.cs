// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;

namespace Game.Entities
{
    public readonly record struct ItemSlot
    {
        /// <summary>ItemSlot Hash size is byte.</summary>
        public const int HashBitSize = 8;
        public const byte Null = byte.MaxValue;           
        
        public ItemSlot(byte slot) { Value = slot; }
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        #region Helpers
        public bool IsSpecific => Value != Null;
        public bool IsEquipSlot => Value < EquipmentSlot.End;
        public bool IsEquipBagSlot => Value >= InventorySlots.BagStart && Value < InventorySlots.BagEnd;
        public bool IsItemSlot => Value >= InventorySlots.ItemStart && Value < InventorySlots.ItemEnd;
        public bool IsValidItemSlot(byte backPackCapacity) => Value >= InventorySlots.ItemStart && Value < InventorySlots.ItemStart + backPackCapacity;
        public bool IsValidBagSlot => Value <= ItemConst.MaxBagSize || Value == Null;
        public bool IsBankItemSlot => Value >= InventorySlots.BankItemStart && Value < InventorySlots.BankItemEnd;
        public bool IsBankBagSlot => Value >= InventorySlots.BankBagStart && Value < InventorySlots.BankBagEnd;
        public bool IsKeyringSlot => Value >= InventorySlots.KeyringStart && Value < InventorySlots.KeyringEnd;
        public bool IsChildEquipmentSlot => Value >= InventorySlots.ChildEquipmentStart && Value < InventorySlots.ChildEquipmentEnd;
        public bool IsBuyBackSlot => Value >= InventorySlots.BuyBackStart && Value < InventorySlots.BuyBackEnd;

        public static implicit operator ItemSlot(byte slot) => new(slot);
        public static implicit operator byte(ItemSlot slot) => slot.Value;
        #endregion

        public readonly byte Value;
    }
}
