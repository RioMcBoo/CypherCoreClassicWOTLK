// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;

namespace Game.Entities
{
    public readonly record struct ItemPos
    {
        /// <summary>ItemPos Hash size is 2 bytes.</summary>
        public const int HashBitSize = 16;
        public static readonly ItemPos Undefined = new(ItemSlot.Null, ItemSlot.Null);

        public ItemPos(byte slot, byte container = ItemSlot.Null)
        {
            Slot = slot;
            Container = container;
        }

        public ItemPos(byte slot, byte? bagSlot)
        {
            Slot = slot;
            Container = bagSlot.HasValue ? bagSlot.Value : ItemSlot.Null;
        }

        public override int GetHashCode()
        {
            return Slot.GetHashCode() | Container.GetHashCode() << ItemSlot.HashBitSize;
        }

        #region Helpers

        /// <summary>Can be stored in container</summary>
        public bool IsContainerPos => Container != ItemSlot.Null;

        /// <summary>Can be stored in any slot</summary>
        public bool IsAnyPos => this == Undefined;

        /// <summary>Can be stored in any slot in specific Container</summary>
        public bool IsAnyContainerPos => Slot == ItemSlot.Null && IsContainerPos;

        /// <summary>Is the Position clearly determined?</summary>
        public bool IsExplicitPos => !IsAnyPos && !IsAnyContainerPos;

        /// <summary>Can be stored in character's personal inventory</summary>
        public bool IsInventoryPos
        {
            get
            {
                if (IsContainerPos)
                {
                    if (Container.IsBagSlot)
                        return true;
                }
                else
                {
                    if (!Slot.IsExplicitSlot)
                        return true;
                    if (Slot.IsItemSlot)
                        return true;
                    if (Slot.IsKeyringSlot)
                        return true;
                    if (Slot.IsChildEquipmentSlot)
                        return true;
                }
                return false;
            }
        }

        /// <summary>Can be equipped in character's special slot</summary>
        public bool IsEquipmentPos
        {
            get
            {
                if (IsContainerPos)
                    return false;
                if (Slot.IsEquipSlot)
                    return true;
                if (Slot.IsProfessionSlot)
                    return true;
                if (Slot.IsBagSlot)
                    return true;
                if (Slot.IsReagentBagSlot)
                    return true;

                return false;
            }
        }

        /// <summary>Can be equipped in character's special slot</summary>
        public bool IsChildEquipmentPos
        {
            get
            {
                if (IsContainerPos)
                    return false;
                if (Slot.IsChildEquipmentSlot)
                    return true;

                return false;
            }
        }

        /// <summary>Can be stored in bank</summary>
        public bool IsBankPos
        {
            get
            {
                if (IsContainerPos)
                {
                    if (Container.IsBankBagSlot)
                        return true;
                }
                else
                {
                    if (Slot.IsBankItemSlot)
                        return true;
                    if (Slot.IsBankBagSlot)
                        return true;
                }
                return false;
            }
        }

        /// <summary>Can be put in special slot for bags</summary>
        public bool IsBagSlotPos
        {
            get
            {
                if (IsContainerPos)
                    return false;
                if (Slot.IsBagSlot)
                    return true;
                if (Slot.IsBankBagSlot)
                    return true;
                if (Slot.IsReagentBagSlot)
                    return true;
                return false;
            }
        }

        /// <summary>Points to BuyBack slot</summary>
        public bool IsBuyBackPos
        {
            get
            {
                if (IsContainerPos)
                    return false;
                if (Slot.IsBuyBackSlot)
                    return true;

                return false;
            }
        }

        public bool IsValid(byte backPackCapacity, bool onlyExplicitPos, ContainerCapacity getCapacity)
        {
            if (!IsExplicitPos && onlyExplicitPos)
                return false;

            if (IsAnyPos)
                return true;

            if (!IsContainerPos)
            {
                // equipment
                if (Slot.IsEquipSlot)
                    return true;

                // profession equipment
                if (Slot.IsProfessionSlot)
                    return true;

                // bag equip slots
                if (Slot.IsBagSlot)
                    return true;

                // reagent bag equip slots
                if (Slot.IsReagentBagSlot)
                    return true;

                // backpack slots
                if (Slot.IsItemSlotExactly(backPackCapacity))
                    return true;

                // bank main slots
                if (Slot.IsBankItemSlot)
                    return true;

                // bank bag slots
                if (Slot.IsBankBagSlot)
                    return true;

                // keyring slots
                if (Slot.IsKeyringSlot)
                    return true;

                return false;
            }

            
            // Containers (bags, bank's bags)
            var capacity = getCapacity(Container);

            //No value = no bag
            if (!capacity.HasValue)
                return false;

            if (IsAnyContainerPos)
                return true;

            if (Slot <= ItemConst.MaxBagSize && Slot < capacity)
                return true;
            
            return false;
        }

        public static implicit operator ItemPos(byte slot) => new(slot);
        public static implicit operator ItemPos(ItemSlot slot) => new(slot);
        public delegate byte? ContainerCapacity(ItemSlot container);
        #endregion

        public readonly ItemSlot Slot;
        public readonly ItemSlot Container;
    }
}
