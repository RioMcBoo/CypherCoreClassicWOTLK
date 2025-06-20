﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Networking.Packets
{
    public class BuyBackItem : ClientPacket
    {
        public BuyBackItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            VendorGUID = _worldPacket.ReadPackedGuid();
            Slot = _worldPacket.ReadInt32();
        }

        public ObjectGuid VendorGUID;
        public int Slot;
    }

    public class BuyItem : ClientPacket
    {
        public BuyItem(WorldPacket packet) : base(packet)
        {
            Item = new ItemInstance();
        }

        public override void Read()
        {
            VendorGUID = _worldPacket.ReadPackedGuid();
            ContainerGUID = _worldPacket.ReadPackedGuid();
            Quantity = _worldPacket.ReadInt32();
            Muid = _worldPacket.ReadInt32();
            Slot = _worldPacket.ReadInt32();
            ItemType = (ItemVendorType)_worldPacket.ReadInt32();
            Item.Read(_worldPacket);
        }

        public ObjectGuid VendorGUID;
        public ItemInstance Item;
        public int Muid;
        public int Slot;
        public ItemVendorType ItemType;
        public int Quantity;
        public ObjectGuid ContainerGUID;
    }

    public class BuySucceeded : ServerPacket
    {
        public BuySucceeded() : base(ServerOpcodes.BuySucceeded) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(VendorGUID);
            _worldPacket.WriteInt32(Muid);
            _worldPacket.WriteInt32(NewQuantity);
            _worldPacket.WriteInt32(QuantityBought);
        }

        public ObjectGuid VendorGUID;
        public int Muid;
        public int QuantityBought;
        public int NewQuantity;
    }

    public class BuyFailed : ServerPacket
    {
        public BuyFailed() : base(ServerOpcodes.BuyFailed) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(VendorGUID);
            _worldPacket.WriteInt32(Muid);
            _worldPacket.WriteUInt8((byte)Reason);
        }

        public ObjectGuid VendorGUID;
        public int Muid;
        public BuyResult Reason = BuyResult.CantFindItem;
    }

    public class GetItemPurchaseData : ClientPacket
    {
        public GetItemPurchaseData(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ItemGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid ItemGUID;
    }

    class SetItemPurchaseData : ServerPacket
    {
        public SetItemPurchaseData() : base(ServerOpcodes.SetItemPurchaseData, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(ItemGUID);
            Contents.Write(_worldPacket);
            _worldPacket.WriteUInt32(Flags);
            _worldPacket.WriteInt32(PurchaseTime);
        }

        public Seconds PurchaseTime;
        public uint Flags;
        public ItemPurchaseContents Contents = new();
        public ObjectGuid ItemGUID;
    }

    class ItemPurchaseRefund : ClientPacket
    {
        public ItemPurchaseRefund(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ItemGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid ItemGUID;
    }

    class ItemPurchaseRefundResult : ServerPacket
    {
        public ItemPurchaseRefundResult() : base(ServerOpcodes.ItemPurchaseRefundResult, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(ItemGUID);
            _worldPacket.WriteUInt8(Result);
            _worldPacket.WriteBit(Contents != null);
            _worldPacket.FlushBits();
            if (Contents != null)
                Contents.Write(_worldPacket);
        }

        public byte Result;
        public ObjectGuid ItemGUID;
        public ItemPurchaseContents Contents;
    }

    class ItemExpirePurchaseRefund : ServerPacket
    {
        public ItemExpirePurchaseRefund() : base(ServerOpcodes.ItemExpirePurchaseRefund, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(ItemGUID);
        }

        public ObjectGuid ItemGUID;
    }

    public class RepairItem : ClientPacket
    {
        public RepairItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            NpcGUID = _worldPacket.ReadPackedGuid();
            ItemGUID = _worldPacket.ReadPackedGuid();
            UseGuildBank = _worldPacket.HasBit();
        }

        public ObjectGuid NpcGUID;
        public ObjectGuid ItemGUID;
        public bool UseGuildBank;
    }

    public class SellItem : ClientPacket
    {
        public SellItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            VendorGUID = _worldPacket.ReadPackedGuid();
            ItemGUID = _worldPacket.ReadPackedGuid();
            Amount = _worldPacket.ReadInt32();
        }

        public ObjectGuid VendorGUID;
        public ObjectGuid ItemGUID;
        public int Amount;
    }

    public class ItemTimeUpdate : ServerPacket
    {
        public ItemTimeUpdate() : base(ServerOpcodes.ItemTimeUpdate) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(ItemGuid);
            _worldPacket.WriteInt32(DurationLeft);
        }

        public ObjectGuid ItemGuid;
        public Seconds DurationLeft;
    }

    public class SetProficiency : ServerPacket
    {
        public SetProficiency() : base(ServerOpcodes.SetProficiency, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32((int)ProficiencyMask);
            _worldPacket.WriteUInt8((byte)ProficiencyClass);
        }

        public ItemSubClassMask ProficiencyMask;
        public ItemClass ProficiencyClass;
    }

    public class InventoryChangeFailure : ServerPacket
    {
        public InventoryChangeFailure() : base(ServerOpcodes.InventoryChangeFailure) { }

        public override void Write()
        {
            _worldPacket.WriteInt32((int)BagResult);
            _worldPacket.WritePackedGuid(Item[0]);
            _worldPacket.WritePackedGuid(Item[1]);
            _worldPacket.WriteUInt8(ContainerBSlot); // bag Type subclass, used with EQUIP_ERR_EVENT_AUTOEQUIP_BIND_CONFIRM and EQUIP_ERR_WRONG_BAG_TYPE_2

            switch (BagResult)
            {
                case InventoryResult.CantEquipLevelI:
                case InventoryResult.PurchaseLevelTooLow:
                    _worldPacket.WriteInt32(Level);
                    break;
                case InventoryResult.EventAutoequipBindConfirm:
                    _worldPacket.WritePackedGuid(SrcContainer);
                    _worldPacket.WriteInt32(SrcSlot);
                    _worldPacket.WritePackedGuid(DstContainer);
                    break;
                case InventoryResult.ItemMaxLimitCategoryCountExceededIs:
                case InventoryResult.ItemMaxLimitCategorySocketedExceededIs:
                case InventoryResult.ItemMaxLimitCategoryEquippedExceededIs:
                    _worldPacket.WriteInt32(LimitCategory);
                    break;
            }
        }

        public InventoryResult BagResult;
        public byte ContainerBSlot;
        public ObjectGuid SrcContainer;
        public ObjectGuid DstContainer;
        public int SrcSlot;
        public int LimitCategory;
        public int Level;
        public ObjectGuid[] Item = new ObjectGuid[2];
    }

    public class SplitItem : ClientPacket
    {
        public SplitItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Inv = new InvUpdate(_worldPacket);
            FromPackSlot = _worldPacket.ReadUInt8();
            FromSlot = _worldPacket.ReadUInt8();
            ToPackSlot = _worldPacket.ReadUInt8();
            ToSlot = _worldPacket.ReadUInt8();
            Quantity = _worldPacket.ReadInt32();
        }

        public byte ToSlot;
        public byte ToPackSlot;
        public byte FromPackSlot;
        public int Quantity;
        public InvUpdate Inv;
        public byte FromSlot;
    }

    public class SwapInvItem : ClientPacket
    {
        public SwapInvItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Inv = new InvUpdate(_worldPacket);
            DestinationSlot = _worldPacket.ReadUInt8();
            SourceSlot = _worldPacket.ReadUInt8();
        }

        public InvUpdate Inv;
        public byte SourceSlot;
        public byte DestinationSlot;
    }

    public class SwapItem : ClientPacket
    {
        public SwapItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Inv = new InvUpdate(_worldPacket);
            ContainerSlotB = _worldPacket.ReadUInt8();
            ContainerSlotA = _worldPacket.ReadUInt8();
            SlotB = _worldPacket.ReadUInt8();
            SlotA = _worldPacket.ReadUInt8();
        }

        public InvUpdate Inv;        
        public byte ContainerSlotB;
        public byte ContainerSlotA;
        public byte SlotB;
        public byte SlotA;

    }

    public class SetAmmoPacket : ClientPacket
    {
        public SetAmmoPacket(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Ammo = _worldPacket.ReadInt32();
        }

        public int Ammo;
    }

    public class AutoEquipItem : ClientPacket
    {
        public AutoEquipItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Inv = new InvUpdate(_worldPacket);
            PackSlot = _worldPacket.ReadUInt8();
            Slot = _worldPacket.ReadUInt8();
        }

        public InvUpdate Inv;
        public byte PackSlot;
        public byte Slot;     
    }

    class AutoEquipItemSlot : ClientPacket
    {
        public AutoEquipItemSlot(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Inv = new InvUpdate(_worldPacket);
            Item = _worldPacket.ReadPackedGuid();
            ItemDstSlot = _worldPacket.ReadUInt8();
        }

        public InvUpdate Inv;
        public ObjectGuid Item;
        public byte ItemDstSlot;        
    }

    public class AutoStoreBagItem : ClientPacket
    {
        public AutoStoreBagItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Inv = new InvUpdate(_worldPacket);
            ContainerSlotA = _worldPacket.ReadUInt8();
            ContainerSlotB = _worldPacket.ReadUInt8();
            SlotA = _worldPacket.ReadUInt8();
        }

        public InvUpdate Inv;
        public byte ContainerSlotB;        
        public byte ContainerSlotA;
        public byte SlotA;
    }

    public class DestroyItem : ClientPacket
    {
        public DestroyItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Count = _worldPacket.ReadInt32();
            ContainerId = _worldPacket.ReadUInt8();
            SlotNum = _worldPacket.ReadUInt8();
        }

        public int Count;
        public byte SlotNum;
        public byte ContainerId;
    }

    public class SellResponse : ServerPacket
    {
        public SellResponse() : base(ServerOpcodes.SellResponse) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(VendorGUID);
            _worldPacket.WriteInt32(ItemGUIDs.Count);
            _worldPacket.WriteInt32((int)Reason);
            foreach (ObjectGuid itemGuid in ItemGUIDs)
                _worldPacket.WritePackedGuid(itemGuid);
        }

        public ObjectGuid VendorGUID;
        public List<ObjectGuid> ItemGUIDs = new();
        public SellResult Reason = SellResult.Unk;
    }

    class ItemPushResult : ServerPacket
    {
        public ItemPushResult() : base(ServerOpcodes.ItemPushResult) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(PlayerGUID);
            _worldPacket.WriteUInt8(Slot);
            _worldPacket.WriteInt32(SlotInBag);
            _worldPacket.WriteInt32(QuestLogItemID);
            _worldPacket.WriteInt32(Quantity);
            _worldPacket.WriteInt32(QuantityInInventory);
            _worldPacket.WriteInt32(DungeonEncounterID);
            _worldPacket.WriteInt32(BattlePetSpeciesID);
            _worldPacket.WriteInt32(BattlePetBreedID);
            _worldPacket.WriteUInt32(BattlePetBreedQuality);
            _worldPacket.WriteInt32(BattlePetLevel);
            _worldPacket.WritePackedGuid(ItemGUID);

            _worldPacket.WriteBit(Pushed);
            _worldPacket.WriteBit(Created);
            _worldPacket.WriteBits((uint)DisplayText, 3);
            _worldPacket.WriteBit(IsBonusRoll);
            _worldPacket.WriteBit(IsEncounterLoot);
            _worldPacket.FlushBits();

            Item.Write(_worldPacket);
        }

        public ObjectGuid PlayerGUID;
        public byte Slot;
        public int SlotInBag;
        public ItemInstance Item = new();
        public int QuestLogItemID;// Item ID used for updating quest progress
                                  // only set if different than real ID (similar to CreatureTemplate.KillCredit)
        public int Quantity;
        public int QuantityInInventory;
        public int DungeonEncounterID;
        public int BattlePetSpeciesID;
        public int BattlePetBreedID;
        public uint BattlePetBreedQuality;
        public int BattlePetLevel;
        public ObjectGuid ItemGUID;
        public bool Pushed;
        public DisplayType DisplayText;
        public bool Created;
        public bool IsBonusRoll;
        public bool IsEncounterLoot;

        public enum DisplayType
        {
            Hidden = 0,
            Normal = 1,
            EncounterLoot = 2
        }
    }

    class ReadItem : ClientPacket
    {
        public ReadItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            PackSlot = _worldPacket.ReadUInt8();
            Slot = _worldPacket.ReadUInt8();
        }

        public byte PackSlot;
        public byte Slot;
    }

    class ReadItemResultFailed : ServerPacket
    {
        public ReadItemResultFailed() : base(ServerOpcodes.ReadItemResultFailed) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Item);
            _worldPacket.WriteUInt32(Delay);
            _worldPacket.WriteBits(Subcode, 2);
            _worldPacket.FlushBits();
        }

        public ObjectGuid Item;
        public byte Subcode;
        public uint Delay;
    }

    class ReadItemResultOK : ServerPacket
    {
        public ReadItemResultOK() : base(ServerOpcodes.ReadItemResultOk) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Item);
        }

        public ObjectGuid Item;
    }

    class WrapItem : ClientPacket
    {
        public WrapItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Inv = new InvUpdate(_worldPacket);
        }

        public InvUpdate Inv;
    }

    class CancelTempEnchantment : ClientPacket
    {
        public CancelTempEnchantment(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Slot = _worldPacket.ReadInt32();
        }

        public int Slot;
    }

    class ItemCooldown : ServerPacket
    {
        public ItemCooldown() : base(ServerOpcodes.ItemCooldown) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(ItemGuid);
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteInt32(Cooldown);
        }

        public ObjectGuid ItemGuid;
        public int SpellID;
        public Milliseconds Cooldown;
    }

    class EnchantmentLog : ServerPacket
    {
        public EnchantmentLog() : base(ServerOpcodes.EnchantmentLog, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Owner);
            _worldPacket.WritePackedGuid(Caster);
            _worldPacket.WritePackedGuid(ItemGUID);
            _worldPacket.WriteInt32(ItemID);
            _worldPacket.WriteInt32(Enchantment);
            _worldPacket.WriteInt32((int)EnchantSlot);
        }

        public ObjectGuid Owner;
        public ObjectGuid Caster;
        public ObjectGuid ItemGUID;
        public int ItemID;
        public int Enchantment;
        public EnchantmentSlot EnchantSlot;
    }

    class ItemEnchantTimeUpdate : ServerPacket
    {
        public ItemEnchantTimeUpdate() : base(ServerOpcodes.ItemEnchantTimeUpdate, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(ItemGuid);
            _worldPacket.WriteInt32(DurationLeft);
            _worldPacket.WriteInt32((int)Slot);
            _worldPacket.WritePackedGuid(OwnerGuid);
        }

        public ObjectGuid OwnerGuid;
        public ObjectGuid ItemGuid;
        public Seconds DurationLeft;
        public EnchantmentSlot Slot;
    }

    class UseCritterItem : ClientPacket
    {
        public UseCritterItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ItemGuid = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid ItemGuid;
    }

    class SocketGems : ClientPacket
    {
        public SocketGems(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ItemGuid = _worldPacket.ReadPackedGuid();
            for (int i = 0; i < ItemConst.MaxGemSockets; ++i)
                GemItem[i] = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid ItemGuid;
        public ObjectGuid[] GemItem = new ObjectGuid[ItemConst.MaxGemSockets];
    }

    class SocketGemsSuccess : ServerPacket
    {
        public SocketGemsSuccess() : base(ServerOpcodes.SocketGemsSuccess, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Item);
        }

        public ObjectGuid Item;
    }

    class SortBags : ClientPacket
    {
        public SortBags(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class SortBankBags : ClientPacket
    {
        public SortBankBags(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class SortReagentBankBags : ClientPacket
    {
        public SortReagentBankBags(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class BagCleanupFinished : ServerPacket
    {
        public BagCleanupFinished() : base(ServerOpcodes.BagCleanupFinished, ConnectionType.Instance) { }

        public override void Write() { }
    }

    class RemoveNewItem : ClientPacket
    {
        public RemoveNewItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ItemGuid = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid ItemGuid;
    }

    class InventoryFullOverflow : ServerPacket
    {
        public InventoryFullOverflow() : base(ServerOpcodes.InventoryFullOverflow) { }

        public override void Write() { }
    }

    //Structs
    public class ItemBonuses
    {
        public void Write(WorldPacket data)
        {
            data.WriteUInt8((byte)Context);
            data.WriteInt32(BonusListIDs.Count);
            foreach (uint bonusID in BonusListIDs)
                data.WriteUInt32(bonusID);
        }

        public void Read(WorldPacket data)
        {
            Context = (ItemContext)data.ReadUInt8();
            uint bonusListIdSize = data.ReadUInt32();

            BonusListIDs = new List<int>();
            for (uint i = 0u; i < bonusListIdSize; ++i)
            {
                int bonusId = (int)data.ReadUInt32();
                BonusListIDs.Add(bonusId);
            }
        }

        public override int GetHashCode()
        {
            return Context.GetHashCode() ^ BonusListIDs.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is ItemBonuses)
                return (ItemBonuses)obj == this;

            return false;
        }

        public static bool operator ==(ItemBonuses left, ItemBonuses right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            if (left.Context != right.Context)
                return false;

            if (left.BonusListIDs.Count != right.BonusListIDs.Count)
                return false;

            return left.BonusListIDs.SequenceEqual(right.BonusListIDs);
        }

        public static bool operator !=(ItemBonuses left, ItemBonuses right)
        {
            return !(left == right);
        }

        public ItemContext Context;
        public List<int> BonusListIDs = new();
    }

    public class ItemMod
    {
        public int Value;
        public ItemModifier Type;

        public ItemMod()
        {
            Type = ItemModifier.Max;
        }
        public ItemMod(int value, ItemModifier type)
        {
            Value = value;
            Type = type;
        }

        public void Read(WorldPacket data)
        {
            Value = data.ReadInt32();
            Type = (ItemModifier)data.ReadUInt8();
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32(Value);
            data.WriteUInt8((byte)Type);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ Type.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is ItemMod)
                return (ItemMod)obj == this;

            return false;
        }

        public static bool operator ==(ItemMod left, ItemMod right)
        {
            if (left.Value != right.Value)
                return false;

            return left.Type != right.Type;
        }

        public static bool operator !=(ItemMod left, ItemMod right)
        {
            return !(left == right);
        }
    }

    public class ItemModList
    {
        public Array<ItemMod> Values = new((int)ItemModifier.Max);

        public void Read(WorldPacket data)
        {
            var itemModListCount = data.ReadBits<uint>(6);
            data.ResetBitPos();

            for (var i = 0; i < itemModListCount; ++i)
            {
                var itemMod = new ItemMod();
                itemMod.Read(data);
                Values[i] = itemMod;
            }
        }

        public void Write(WorldPacket data)
        {
            data.WriteBits(Values.Count, 6);
            data.FlushBits();

            foreach (ItemMod itemMod in Values)
                itemMod.Write(data);
        }

        public override int GetHashCode()
        {
            return Values.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is ItemModList)
                return (ItemModList)obj == this;

            return false;
        }

        public static bool operator ==(ItemModList left, ItemModList right)
        {
            if (left.Values.Count != right.Values.Count)
                return false;

            return !left.Values.Except(right.Values).Any();
        }

        public static bool operator !=(ItemModList left, ItemModList right)
        {
            return !(left == right);
        }
    }

    public class ItemInstance
    {
        public int ItemID;
        public int RandomPropertiesSeed;
        public int RandomPropertiesID;
        public ItemBonuses ItemBonus;
        public ItemModList Modifications = new();

        public ItemInstance() { }

        public ItemInstance(Item item)
        {
            if (item != null)
            {
                ItemID = item.GetEntry();

                RandomPropertiesSeed = item.GetItemSuffixFactor();
                RandomPropertiesID = item.GetItemRandomPropertyId();
            }
        }

        public ItemInstance(SocketedGem gem)
        {
            ItemID = gem.ItemId.GetValue();

            ItemBonuses bonus = new();
            bonus.Context = (ItemContext)(byte)gem.Context;
            foreach (ushort bonusListId in gem.BonusListIDs)
                if (bonusListId != 0)
                    bonus.BonusListIDs.Add(bonusListId);

            if (bonus.Context != 0 || !bonus.BonusListIDs.Empty())
                ItemBonus = bonus;
        }

        public ItemInstance(Loots.LootItem lootItem)
        {
            ItemID = lootItem.itemid;
        }

        public ItemInstance(VoidStorageItem voidItem)
        {
            ItemID = voidItem.ItemEntry;

            if (voidItem.FixedScalingLevel != 0)
                Modifications.Values.Add(new ItemMod(voidItem.FixedScalingLevel, ItemModifier.TimewalkerLevel));
        }        

        public void Write(WorldPacket data)
        {
            data.WriteInt32(ItemID);
            data.WriteInt32(RandomPropertiesSeed);
            data.WriteInt32(RandomPropertiesID);

            data.WriteBit(ItemBonus != null);
            data.FlushBits();

            Modifications.Write(data);

            if (ItemBonus != null)
                ItemBonus.Write(data);
        }

        public void Read(WorldPacket data)
        {
            ItemID = data.ReadInt32();
            RandomPropertiesSeed = data.ReadInt32();
            RandomPropertiesID = data.ReadInt32();

            if (data.HasBit())
                ItemBonus = new();

            data.ResetBitPos();

            Modifications.Read(data);

            if (ItemBonus != null)
                ItemBonus.Read(data);
        }

        public override int GetHashCode()
        {
            return ItemID.GetHashCode() ^ ItemBonus.GetHashCode() ^ Modifications.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is ItemInstance)
                return (ItemInstance)obj == this;

            return false;
        }

        public static bool operator ==(ItemInstance left, ItemInstance right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null))
                return false;
            if (ReferenceEquals(right, null))
                return false;

            if (left.ItemID != right.ItemID || left.RandomPropertiesID != right.RandomPropertiesID || left.RandomPropertiesSeed != right.RandomPropertiesSeed)
                return false;

            if (left.ItemBonus != null && right.ItemBonus != null && left.ItemBonus != right.ItemBonus)
                return false;

            if (left.Modifications != right.Modifications)
                return false;

            return true;
        }

        public static bool operator !=(ItemInstance left, ItemInstance right)
        {
            return !(left == right);
        }
    }

    public class ItemBonusKey : IEquatable<ItemBonusKey>
    {
        public int ItemID;
        public List<int> BonusListIDs = new();

        public void Write(WorldPacket data)
        {
            data.WriteInt32(ItemID);
            data.WriteInt32(BonusListIDs.Count);

            foreach (var id in BonusListIDs)
                    data.WriteInt32(id);
        }

        public bool Equals(ItemBonusKey right)
        {
            if (ItemID != right.ItemID)
                return false;

            if (BonusListIDs != right.BonusListIDs)
                return false;

            return true;
        }
    }

    public class ItemEnchantData
    {
        public ItemEnchantData() { }
        public ItemEnchantData(int id, Milliseconds expiration, int charges, byte slot)
        {
            ID = id;
            Expiration = expiration;
            Charges = charges;
            Slot = slot;
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32(ID);
            data.WriteInt32(Expiration);
            data.WriteInt32(Charges);
            data.WriteUInt8(Slot);
        }

        public int ID;
        public Milliseconds Expiration;
        public int Charges;
        public byte Slot;
    }

    public class ItemGemData
    {
        public void Write(WorldPacket data)
        {
            data.WriteUInt8(Slot);
            Item.Write(data);
        }

        public void Read(WorldPacket data)
        {
            Slot = data.ReadUInt8();
            Item.Read(data);
        }

        public byte Slot;
        public ItemInstance Item = new();
    }

    public struct InvUpdate
    {
        public InvUpdate(WorldPacket data)
        {
            Items = new List<ItemPos>();
            int size = data.ReadBits<int>(2);
            data.ResetBitPos();
            for (int i = 0; i < size; ++i)
            {
                byte containerSlot = data.ReadUInt8();
                byte slot = data.ReadUInt8();

                Items.Add(new(slot, containerSlot));
            }
        }

        public List<ItemPos> Items;
    }

    struct ItemPurchaseRefundItem
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(ItemID);
            data.WriteInt32(ItemCount);
        }

        public int ItemID;
        public int ItemCount;
    }

    struct ItemPurchaseRefundCurrency
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(CurrencyID);
            data.WriteInt32(CurrencyCount);
        }

        public int CurrencyID;
        public int CurrencyCount;
    }

    class ItemPurchaseContents
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt64(Money);
            for (int i = 0; i < 5; ++i)
                Items[i].Write(data);

            for (int i = 0; i < 5; ++i)
                Currencies[i].Write(data);
        }

        public long Money;
        public ItemPurchaseRefundItem[] Items = new ItemPurchaseRefundItem[5];
        public ItemPurchaseRefundCurrency[] Currencies = new ItemPurchaseRefundCurrency[5];
    }

    public struct UiEventToast
    {
        public int UiEventToastID;
        public int Asset;

        public void Write(WorldPacket data)
        {
            data.WriteInt32(UiEventToastID);
            data.WriteInt32(Asset);
        }
    }
}
