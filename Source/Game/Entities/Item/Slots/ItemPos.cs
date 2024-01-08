// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.Entities
{
    public readonly record struct ItemPos
    {
        /// <summary>ItemPos Hash size is 2 bytes.</summary>
        public const int HashBitSize = 16;
        public static readonly ItemPos Undefined = new(ItemSlot.Null, ItemSlot.Null); 

        public ItemPos(byte slot, byte bagSlot = ItemSlot.Null)
        {
            Slot = slot;
            BagSlot = bagSlot;
        }

        public ItemPos(byte slot, byte? bagSlot)
        {
            Slot = slot;
            BagSlot = bagSlot.HasValue ? bagSlot.Value : ItemSlot.Null;
        }

        public override int GetHashCode()
        {
            return Slot.GetHashCode() | BagSlot.GetHashCode() << ItemSlot.HashBitSize;
        }

        #region Helpers
        /// <summary>Is the position clearly determined?</summary>
        public bool IsSpecificPos => Slot != ItemSlot.Null;

        /// <summary>Is the bag clearly determined?</summary>
        public bool IsSpecificBag => BagSlot != ItemSlot.Null;

        /// <summary>Points to any place in the player's default inventory</summary>
        public bool IsInventoryPos
        {
            get
            {
                if (!IsSpecificBag && Slot.IsItemSlot)
                    return true;
                if (BagSlot.IsEquipBagSlot && Slot.IsValidBagSlot)
                    return true;
                if (!IsSpecificBag && Slot.IsKeyringSlot)
                    return true;
                if (!IsSpecificBag && Slot.IsChildEquipmentSlot)
                    return true;
                return false;
            }
        }

        /// <summary>Points to any place in the player's bank</summary>
        public bool IsBankPos
        {
            get
            {
                if (!IsSpecificBag && Slot.IsBankItemSlot)
                    return true;
                if (!IsSpecificBag && Slot.IsBankBagSlot)
                    return true;
                if (BagSlot.IsBankBagSlot)
                    return true;
                return false;
            }
        }

        /// <summary>Points to any bag slot (equip/bank)</summary>
        public bool IsBagSlotPos
        {
            get
            {
                if (!IsSpecificBag && Slot.IsEquipBagSlot)
                    return true;
                if (!IsSpecificBag && Slot.IsBankBagSlot)
                    return true;
                return false;
            }
        }

        /// <summary>Can be equip on a character</summary>
        public bool IsEquipmentPos
        {
            get
            {
                if (!IsSpecificBag && Slot.IsEquipSlot)
                    return true;
                if (!IsSpecificBag && Slot.IsEquipBagSlot)
                    return true;
                return false;
            }
        }

        /// <summary>TODO: I don't know what is it</summary>
        public bool IsChildEquipmentPos => !IsSpecificBag && Slot.IsChildEquipmentSlot;

        /// <summary>Points to BuyBack slot</summary>
        public bool IsBuyBackPos => !IsSpecificBag && Slot.IsBuyBackSlot;

        public static implicit operator ItemPos(byte slot) => new(slot);
        public static implicit operator ItemPos(ItemSlot slot) => new(slot);
        #endregion  

        public readonly ItemSlot Slot;
        public readonly ItemSlot BagSlot;
    }
}
