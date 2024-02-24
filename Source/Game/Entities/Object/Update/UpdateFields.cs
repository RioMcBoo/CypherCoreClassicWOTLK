// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.AI;
using Game.DataStorage;
using Game.Networking;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Entities
{
    public class ObjectFieldData : HasChangesMask
    {
        public UpdateField<int> EntryId = new(0, 1);
        public UpdateField<uint> DynamicFlags = new(0, 2);
        public UpdateField<float> Scale = new(0, 3);
        static int ChangeMaskLength = 4;

        public ObjectFieldData() : base(0, TypeId.Object, ChangeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, WorldObject owner, Player receiver)
        {
            data.WriteInt32(GetViewerDependentEntryId(this, owner, receiver));
            data.WriteUInt32(GetViewerDependentDynamicFlags(this, owner, receiver));
            data.WriteFloat(Scale);
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, WorldObject owner, Player receiver)
        {
            WriteUpdate(data, _changesMask, false, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, WorldObject owner, Player receiver)
        {
            data.WriteBits(changesMask.GetBlock(0), ChangeMaskLength);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteInt32(GetViewerDependentEntryId(this, owner, receiver));
                }
                if (changesMask[2])
                {
                    data.WriteUInt32(GetViewerDependentDynamicFlags(this, owner, receiver));
                }
                if (changesMask[3])
                {
                    data.WriteFloat(Scale);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(EntryId);
            ClearChangesMask(DynamicFlags);
            ClearChangesMask(Scale);
            _changesMask.ResetAll();
        }

        int GetViewerDependentEntryId(ObjectFieldData objectData, WorldObject obj, Player receiver)
        {
            int entryId = objectData.EntryId;

            if (obj.ToUnit() is Unit unit)
            {
                if (unit.ToTempSummon() is TempSummon summon)
                    if (summon.GetSummonerGUID() == receiver.GetGUID() && summon.GetCreatureIdVisibleToSummoner().HasValue)
                        entryId = (int)summon.GetCreatureIdVisibleToSummoner().Value;
            }

            return entryId;
        }

        uint GetViewerDependentDynamicFlags(ObjectFieldData objectData, WorldObject obj, Player receiver)
        {
            uint unitDynFlags = objectData.DynamicFlags;

            if (obj.ToUnit() is Unit unit)
            {
                if (obj.ToCreature() is Creature creature)
                {
                    if ((unitDynFlags & (uint)UnitDynFlags.Tapped) != 0 && !creature.IsTappedBy(receiver))
                        unitDynFlags &= ~(uint)UnitDynFlags.Tapped;

                    if ((unitDynFlags & (uint)UnitDynFlags.Lootable) != 0 && !receiver.IsAllowedToLoot(creature))
                        unitDynFlags &= ~(uint)UnitDynFlags.Lootable;

                    if ((unitDynFlags & (uint)UnitDynFlags.CanSkin) != 0 && creature.IsSkinnedBy(receiver))
                        unitDynFlags &= ~(uint)UnitDynFlags.CanSkin;
                }

                // unit UNIT_DYNFLAG_TRACK_UNIT should only be sent to caster of SPELL_AURA_MOD_STALKED auras
                if (unitDynFlags.HasAnyFlag((uint)UnitDynFlags.TrackUnit))
                    if (!unit.HasAuraTypeWithCaster(AuraType.ModStalked, receiver.GetGUID()))
                        unitDynFlags &= ~(uint)UnitDynFlags.TrackUnit;
            }
            else
            {
                if (obj.ToGameObject() is GameObject gameObject)
                {
                    GameObjectDynamicLowFlags dynFlags = 0;
                    ushort pathProgress = 0xFFFF;
                    switch (gameObject.GetGoType())
                    {
                        case GameObjectTypes.QuestGiver:
                            if (gameObject.ActivateToQuest(receiver))
                                dynFlags |= GameObjectDynamicLowFlags.Activate;
                            break;
                        case GameObjectTypes.Chest:
                            if (gameObject.ActivateToQuest(receiver))
                                dynFlags |= GameObjectDynamicLowFlags.Activate | GameObjectDynamicLowFlags.Sparkle | GameObjectDynamicLowFlags.Highlight;
                            else if (receiver.IsGameMaster())
                                dynFlags |= GameObjectDynamicLowFlags.Activate;
                            break;
                        case GameObjectTypes.Goober:
                            if (gameObject.ActivateToQuest(receiver))
                            {
                                dynFlags |= GameObjectDynamicLowFlags.Highlight;
                                if (gameObject.GetGoStateFor(receiver.GetGUID()) != GameObjectState.Active)
                                    dynFlags |= GameObjectDynamicLowFlags.Activate;
                            }
                            else if (receiver.IsGameMaster())
                                dynFlags |= GameObjectDynamicLowFlags.Activate;
                            break;
                        case GameObjectTypes.Generic:
                            if (gameObject.ActivateToQuest(receiver))
                                dynFlags |= GameObjectDynamicLowFlags.Sparkle | GameObjectDynamicLowFlags.Highlight;
                            break;
                        case GameObjectTypes.Transport:
                        case GameObjectTypes.MapObjTransport:
                        {
                            dynFlags = (GameObjectDynamicLowFlags)((int)unitDynFlags & 0xFFFF);
                            pathProgress = (ushort)((int)unitDynFlags >> 16);
                            break;
                        }
                        case GameObjectTypes.CapturePoint:
                            if (!gameObject.CanInteractWithCapturePoint(receiver))
                                dynFlags |= GameObjectDynamicLowFlags.NoInterract;
                            else
                                dynFlags &= ~GameObjectDynamicLowFlags.NoInterract;
                            break;
                        case GameObjectTypes.GatheringNode:
                            if (gameObject.ActivateToQuest(receiver))
                                dynFlags |= GameObjectDynamicLowFlags.Activate | GameObjectDynamicLowFlags.Sparkle | GameObjectDynamicLowFlags.Highlight;
                            if (gameObject.GetGoStateFor(receiver.GetGUID()) == GameObjectState.Active)
                                dynFlags |= GameObjectDynamicLowFlags.Depleted;
                            break;
                        default:
                            break;
                    }

                    if (!gameObject.MeetsInteractCondition(receiver))
                        dynFlags |= GameObjectDynamicLowFlags.NoInterract;

                    unitDynFlags = ((uint)pathProgress << 16) | (uint)dynFlags;
                }
            }

            return unitDynFlags;
        }
    }

    public class ItemEnchantment : HasChangesMask
    {
        public UpdateField<int> ID = new(0, 1);
        public UpdateField<uint> Duration = new(0, 2);
        public UpdateField<short> Charges = new(0, 3);
        public UpdateField<byte> Field_A = new(0, 4);
        public UpdateField<byte> Field_B = new(0, 5);
        static int ChangeMaskLength = 6;

        public ItemEnchantment() : base(ChangeMaskLength) { }

        public void WriteCreate(WorldPacket data, Item owner, Player receiver)
        {
            data.WriteInt32(ID);
            data.WriteUInt32(Duration);
            data.WriteInt16(Charges);
            data.WriteUInt8(Field_A);
            data.WriteUInt8(Field_B);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Item owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), ChangeMaskLength);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteInt32(ID);
                }
                if (changesMask[2])
                {
                    data.WriteUInt32(Duration);
                }
                if (changesMask[3])
                {
                    data.WriteInt16(Charges);
                }
                if (changesMask[4])
                {
                    data.WriteUInt8(Field_A);
                }
                if (changesMask[5])
                {
                    data.WriteUInt8(Field_B);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(ID);
            ClearChangesMask(Duration);
            ClearChangesMask(Charges);
            ClearChangesMask(Field_A);
            ClearChangesMask(Field_B);
            _changesMask.ResetAll();
        }
    }

    public class ItemMod
    {
        public int Value;
        public byte Type;

        public void WriteCreate(WorldPacket data, Item owner, Player receiver)
        {
            data.WriteInt32(Value);
            data.WriteUInt8(Type);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Item owner, Player receiver)
        {
            data.WriteInt32(Value);
            data.WriteUInt8(Type);
        }
    }

    public class ItemModList : HasChangesMask
    {
        public DynamicUpdateField<ItemMod> Values = new(-1, 0);

        public ItemModList() : base(1) { }

        public void WriteCreate(WorldPacket data, Item owner, Player receiver)
        {
            data.WriteBits(Values.Size(), 6);
            for (int i = 0; i < Values.Size(); ++i)
            {
                Values[i].WriteCreate(data, owner, receiver);
            }
            data.FlushBits();
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Item owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), 1);

            if (changesMask[0])
            {
                if (!ignoreChangesMask)
                    Values.WriteUpdateMask(data, 6);
                else
                    WriteCompleteDynamicFieldUpdateMask(Values.Size(), data, 6);
            }

            data.FlushBits();

            if (changesMask[0])
            {
                for (int i = 0; i < Values.Size(); ++i)
                {
                    if (Values.HasChanged(i) || ignoreChangesMask)
                    {
                        Values[i].WriteUpdate(data, ignoreChangesMask, owner, receiver);
                    }
                }
            }

            data.FlushBits();
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Values);
            _changesMask.ResetAll();
        }
    }

    public class ArtifactPower
    {
        public short ArtifactPowerId;
        public byte PurchasedRank;
        public byte CurrentRankWithBonus;

        public void WriteCreate(WorldPacket data, Item owner, Player receiver)
        {
            data.WriteInt16(ArtifactPowerId);
            data.WriteUInt8(PurchasedRank);
            data.WriteUInt8(CurrentRankWithBonus);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Item owner, Player receiver)
        {
            data.WriteInt16(ArtifactPowerId);
            data.WriteUInt8(PurchasedRank);
            data.WriteUInt8(CurrentRankWithBonus);
        }
    }

    public class SocketedGem : HasChangesMask
    {
        public UpdateField<int> ItemId = new(0, 1);
        public UpdateField<byte> Context = new(0, 2);
        public UpdateFieldArray<ushort> BonusListIDs = new(16, 3, 4);
        static int ChangeMaskLength = 20;

        public SocketedGem() : base(ChangeMaskLength) { }

        public void WriteCreate(WorldPacket data, Item owner, Player receiver)
        {
            data.WriteInt32(ItemId);
            for (int i = 0; i < 16; ++i)
                data.WriteUInt16(BonusListIDs[i]);

            data.WriteUInt8(Context);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Item owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlocksMask(0), 1);
            if (changesMask.GetBlock(0) != 0)
                data.WriteBits(changesMask.GetBlock(0), 32);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteInt32(ItemId);
                }
                if (changesMask[2])
                {
                    data.WriteUInt8(Context);
                }
            }
            if (changesMask[3])
            {
                for (int i = 0; i < 16; ++i)
                {
                    if (changesMask[4 + i])
                    {
                        data.WriteUInt16(BonusListIDs[i]);
                    }
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(ItemId);
            ClearChangesMask(Context);
            ClearChangesMask(BonusListIDs);
            _changesMask.ResetAll();
        }
    }

    public class ItemData : HasChangesMask
    {
        public DynamicUpdateField<ArtifactPower> ArtifactPowers = new(0, 1);
        public DynamicUpdateField<SocketedGem> Gems = new(0, 2);
        public UpdateField<ObjectGuid> Owner = new(0, 3);
        public UpdateField<ObjectGuid> ContainedIn = new(0, 4);
        public UpdateField<ObjectGuid> Creator = new(0, 5);
        public UpdateField<ObjectGuid> GiftCreator = new(0, 6);
        public UpdateField<uint> StackCount = new(0, 7);
        public UpdateField<uint> Expiration = new(0, 8);
        public UpdateField<uint> DynamicFlags = new(0, 9);
        public UpdateField<int> PropertySeed = new(0, 10);
        public UpdateField<int> RandomPropertiesID = new(0, 11);
        public UpdateField<uint> Durability = new(0, 12);
        public UpdateField<uint> MaxDurability = new(0, 13);
        public UpdateField<uint> CreatePlayedTime = new(0, 14);
        public UpdateField<int> Context = new(0, 15);
        public UpdateField<long> CreateTime = new(0, 16);
        public UpdateField<ulong> ArtifactXP = new(0, 17);
        public UpdateField<byte> ItemAppearanceModID = new(0, 18);
        public UpdateField<ItemModList> Modifiers = new(0, 19);
        public UpdateField<uint> DynamicFlags2 = new(0, 20);
        public UpdateField<ItemBonusKey> ItemBonusKey = new(0, 21);
        public UpdateField<ushort> DEBUGItemLevel = new(0, 22);
        public UpdateFieldArray<int> SpellCharges = new(5, 23, 24);
        public UpdateFieldArray<ItemEnchantment> Enchantment = new(13, 29, 30);
        static int ChangeMaskLength = 43;

        public ItemData() : base(0, TypeId.Item, ChangeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Item owner, Player receiver)
        {
            data.WritePackedGuid(Owner);
            data.WritePackedGuid(ContainedIn);
            data.WritePackedGuid(Creator);
            data.WritePackedGuid(GiftCreator);
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
            {
                data.WriteUInt32(StackCount);
                data.WriteUInt32(Expiration);
                for (int i = 0; i < 5; ++i)
                {
                    data.WriteInt32(SpellCharges[i]);
                }
            }
            data.WriteUInt32(DynamicFlags);
            for (int i = 0; i < 13; ++i)
            {
                Enchantment[i].WriteCreate(data, owner, receiver);
            }
            data.WriteInt32(PropertySeed);
            data.WriteInt32(RandomPropertiesID);
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
            {
                data.WriteUInt32(Durability);
                data.WriteUInt32(MaxDurability);
            }
            data.WriteUInt32(CreatePlayedTime);
            data.WriteInt32(Context);
            data.WriteInt64(CreateTime);
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
            {
                data.WriteUInt64(ArtifactXP);
                data.WriteUInt8(ItemAppearanceModID);
            }
            data.WriteInt32(ArtifactPowers.Size());
            data.WriteInt32(Gems.Size());
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
            {
                data.WriteUInt32(DynamicFlags2);
            }
            ItemBonusKey.GetValue().Write(data);
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
            {
                data.WriteUInt16(DEBUGItemLevel);
            }
            for (int i = 0; i < ArtifactPowers.Size(); ++i)
            {
                ArtifactPowers[i].WriteCreate(data, owner, receiver);
            }
            for (int i = 0; i < Gems.Size(); ++i)
            {
                Gems[i].WriteCreate(data, owner, receiver);
            }
            Modifiers.GetValue().WriteCreate(data, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Item owner, Player receiver)
        {
            UpdateMask allowedMaskForTarget = new(ChangeMaskLength, [0xE029CE7Fu, 0x000007FFu]);
            AppendAllowedFieldsMaskForFlag(allowedMaskForTarget, fieldVisibilityFlags);
            WriteUpdate(data, _changesMask & allowedMaskForTarget, false, owner, receiver);
        }

        public void AppendAllowedFieldsMaskForFlag(UpdateMask allowedMaskForTarget, UpdateFieldFlag fieldVisibilityFlags)
        {
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
                allowedMaskForTarget.OR(new UpdateMask(ChangeMaskLength, [0x1FD63180u, 0x00000000u]));
        }

        public void FilterDisallowedFieldsMaskForFlag(UpdateMask changesMask, UpdateFieldFlag fieldVisibilityFlags)
        {
            UpdateMask allowedMaskForTarget = new(ChangeMaskLength, [0xE029CE7Fu, 0x000007FFu]);
            AppendAllowedFieldsMaskForFlag(allowedMaskForTarget, fieldVisibilityFlags);
            changesMask.AND(allowedMaskForTarget);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, Item owner, Player receiver)
        {
            data.WriteBits(changesMask.GetBlocksMask(0), 2);
            for (uint i = 0; i < 2; ++i)
                if (changesMask.GetBlock(i) != 0)
                    data.WriteBits(changesMask.GetBlock(i), 32);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    if (!ignoreNestedChangesMask)
                        ArtifactPowers.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(ArtifactPowers.Size(), data);
                }
                if (changesMask[2])
                {
                    if (!ignoreNestedChangesMask)
                        Gems.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Gems.Size(), data);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    for (int i = 0; i < ArtifactPowers.Size(); ++i)
                    {
                        if (ArtifactPowers.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            ArtifactPowers[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[2])
                {
                    for (int i = 0; i < Gems.Size(); ++i)
                    {
                        if (Gems.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            Gems[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[3])
                {
                    data.WritePackedGuid(Owner);
                }
                if (changesMask[4])
                {
                    data.WritePackedGuid(ContainedIn);
                }
                if (changesMask[5])
                {
                    data.WritePackedGuid(Creator);
                }
                if (changesMask[6])
                {
                    data.WritePackedGuid(GiftCreator);
                }
                if (changesMask[7])
                {
                    data.WriteUInt32(StackCount);
                }
                if (changesMask[8])
                {
                    data.WriteUInt32(Expiration);
                }
                if (changesMask[9])
                {
                    data.WriteUInt32(DynamicFlags);
                }
                if (changesMask[10])
                {
                    data.WriteInt32(PropertySeed);
                }
                if (changesMask[11])
                {
                    data.WriteInt32(RandomPropertiesID);
                }
                if (changesMask[12])
                {
                    data.WriteUInt32(Durability);
                }
                if (changesMask[13])
                {
                    data.WriteUInt32(MaxDurability);
                }
                if (changesMask[14])
                {
                    data.WriteUInt32(CreatePlayedTime);
                }
                if (changesMask[15])
                {
                    data.WriteInt32(Context);
                }
                if (changesMask[16])
                {
                    data.WriteInt64(CreateTime);
                }
                if (changesMask[17])
                {
                    data.WriteUInt64(ArtifactXP);
                }
                if (changesMask[18])
                {
                    data.WriteUInt8(ItemAppearanceModID);
                }
                if (changesMask[20])
                {
                    data.WriteUInt32(DynamicFlags2);
                }
                if (changesMask[21])
                {
                    ItemBonusKey.GetValue().Write(data);
                }
                if (changesMask[22])
                {
                    data.WriteUInt16(DEBUGItemLevel);
                }
                if (changesMask[19])
                {
                    Modifiers.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
            }
            if (changesMask[23])
            {
                for (int i = 0; i < 5; ++i)
                {
                    if (changesMask[24 + i])
                    {
                        data.WriteInt32(SpellCharges[i]);
                    }
                }
            }
            if (changesMask[29])
            {
                for (int i = 0; i < 13; ++i)
                {
                    if (changesMask[30 + i])
                    {
                        Enchantment[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                    }
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(ArtifactPowers);
            ClearChangesMask(Gems);
            ClearChangesMask(Owner);
            ClearChangesMask(ContainedIn);
            ClearChangesMask(Creator);
            ClearChangesMask(GiftCreator);
            ClearChangesMask(StackCount);
            ClearChangesMask(Expiration);
            ClearChangesMask(DynamicFlags);
            ClearChangesMask(PropertySeed);
            ClearChangesMask(RandomPropertiesID);
            ClearChangesMask(Durability);
            ClearChangesMask(MaxDurability);
            ClearChangesMask(CreatePlayedTime);
            ClearChangesMask(Context);
            ClearChangesMask(CreateTime);
            ClearChangesMask(ArtifactXP);
            ClearChangesMask(ItemAppearanceModID);
            ClearChangesMask(Modifiers);
            ClearChangesMask(DynamicFlags2);
            ClearChangesMask(ItemBonusKey);
            ClearChangesMask(DEBUGItemLevel);
            ClearChangesMask(SpellCharges);
            ClearChangesMask(Enchantment);
            _changesMask.ResetAll();
        }
    }

    public class ContainerData : HasChangesMask
    {
        public UpdateField<uint> NumSlots = new(0, 1);
        public UpdateFieldArray<ObjectGuid> Slots = new(36, 2, 3);
        static int ChangeMaskLength = 39;

        public ContainerData() : base(0, TypeId.Container, ChangeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Bag owner, Player receiver)
        {
            for (int i = 0; i < 36; ++i)
            {
                data.WritePackedGuid(Slots[i]);
            }
            data.WriteUInt32(NumSlots);
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Bag owner, Player receiver)
        {
            WriteUpdate(data, _changesMask, false, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, Bag owner, Player receiver)
        {
            data.WriteBits(_changesMask.GetBlocksMask(0), 2);
            for (uint i = 0; i < 2; ++i)
                if (_changesMask.GetBlock(i) != 0)
                    data.WriteBits(_changesMask.GetBlock(i), 32);

            data.FlushBits();
            if (_changesMask[0])
            {
                if (_changesMask[1])
                {
                    data.WriteUInt32(NumSlots);
                }
            }
            if (_changesMask[2])
            {
                for (int i = 0; i < 36; ++i)
                {
                    if (_changesMask[3 + i])
                    {
                        data.WritePackedGuid(Slots[i]);
                    }
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(NumSlots);
            ClearChangesMask(Slots);
            _changesMask.ResetAll();
        }
    }

    public class UnitChannel
    {
        public int SpellID;
        public int SpellXSpellVisualID;

        public void WriteCreate(WorldPacket data, Unit owner, Player receiver)
        {
            data.WriteInt32(SpellID);
            data.WriteInt32(SpellXSpellVisualID);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Unit owner, Player receiver)
        {
            data.WriteInt32(SpellID);
            data.WriteInt32(SpellXSpellVisualID);
        }
    }

    public class VisibleItem : HasChangesMask
    {
        public UpdateField<int> ItemID = new(0, 1);
        public UpdateField<ushort> ItemAppearanceModID = new(0, 2);
        public UpdateField<ushort> ItemVisual = new(0, 3);
        static int ChangeMaskLength = 4;

        public VisibleItem() : base(ChangeMaskLength) { }

        public void WriteCreate(WorldPacket data, Unit owner, Player receiver)
        {
            data.WriteInt32(ItemID);
            data.WriteUInt16(ItemAppearanceModID);
            data.WriteUInt16(ItemVisual);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Unit owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), ChangeMaskLength);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteInt32(ItemID);
                }
                if (changesMask[2])
                {
                    data.WriteUInt16(ItemAppearanceModID);
                }
                if (changesMask[3])
                {
                    data.WriteUInt16(ItemVisual);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(ItemID);
            ClearChangesMask(ItemAppearanceModID);
            ClearChangesMask(ItemVisual);
            _changesMask.ResetAll();
        }
    }

    public class PassiveSpellHistory
    {
        public int SpellID;
        public int AuraSpellID;

        public void WriteCreate(WorldPacket data, Unit owner, Player receiver)
        {
            data.WriteInt32(SpellID);
            data.WriteInt32(AuraSpellID);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Unit owner, Player receiver)
        {
            data.WriteInt32(SpellID);
            data.WriteInt32(AuraSpellID);
        }
    }

    public class UnitData : HasChangesMask
    {
        public UpdateField<List<uint>> StateWorldEffectIDs = new(0, 1);
        public DynamicUpdateField<PassiveSpellHistory> PassiveSpells = new(0, 2);
        public DynamicUpdateField<int> WorldEffects = new(0, 3);
        public DynamicUpdateField<ObjectGuid> ChannelObjects = new(0, 4);
        public UpdateField<long> Health = new(0, 5);
        public UpdateField<long> MaxHealth = new(0, 6);
        public UpdateField<int> DisplayID = new(0, 7);
        public UpdateField<uint> StateSpellVisualID = new(0, 8);
        public UpdateField<uint> StateAnimID = new(0, 9);
        public UpdateField<uint> StateAnimKitID = new(0, 10);
        public UpdateField<ObjectGuid> Charm = new(0, 11);
        public UpdateField<ObjectGuid> Summon = new(0, 12);
        public UpdateField<ObjectGuid> Critter = new(0, 13);
        public UpdateField<ObjectGuid> CharmedBy = new(0, 14);
        public UpdateField<ObjectGuid> SummonedBy = new(0, 15);
        public UpdateField<ObjectGuid> CreatedBy = new(0, 16);
        public UpdateField<ObjectGuid> DemonCreator = new(0, 17);
        public UpdateField<ObjectGuid> LookAtControllerTarget = new(0, 18);
        public UpdateField<ObjectGuid> Target = new(0, 19);
        public UpdateField<ObjectGuid> BattlePetCompanionGUID = new(0, 20);
        public UpdateField<ulong> BattlePetDBID = new(0, 21);
        public UpdateField<UnitChannel> ChannelData = new(0, 22);
        public UpdateField<uint> SummonedByHomeRealm = new(0, 23);
        public UpdateField<byte> Race = new(0, 24);
        public UpdateField<byte> ClassId = new(0, 25);
        public UpdateField<byte> PlayerClassId = new(0, 26);
        public UpdateField<byte> Sex = new(0, 27);
        public UpdateField<byte> DisplayPower = new(0, 28);
        public UpdateField<uint> OverrideDisplayPowerID = new(0, 29);
        public UpdateField<int> Level = new(0, 30);
        public UpdateField<int> EffectiveLevel = new(0, 31);
        public UpdateField<int> ContentTuningID = new(32, 33);
        public UpdateField<int> ScalingLevelMin = new(32, 34);
        public UpdateField<int> ScalingLevelMax = new(32, 35);
        public UpdateField<int> ScalingLevelDelta = new(32, 36);
        public UpdateField<int> ScalingFactionGroup = new(32, 37);
        public UpdateField<int> ScalingHealthItemLevelCurveID = new(32, 38);
        public UpdateField<int> ScalingDamageItemLevelCurveID = new(32, 39);
        public UpdateField<int> FactionTemplate = new(32, 40);
        public UpdateField<uint> Flags = new(32, 41);
        public UpdateField<uint> Flags2 = new(32, 42);
        public UpdateField<uint> Flags3 = new(32, 43);
        public UpdateField<uint> AuraState = new(32, 44);
        public UpdateField<uint> RangedAttackRoundBaseTime = new(32, 45);
        public UpdateField<float> BoundingRadius = new(32, 46);
        public UpdateField<float> CombatReach = new(32, 47);
        public UpdateField<float> DisplayScale = new(32, 48);
        public UpdateField<int> NativeDisplayID = new(32, 49);
        public UpdateField<float> NativeXDisplayScale = new(32, 50);
        public UpdateField<int> MountDisplayID = new(32, 51);
        public UpdateField<float> MinDamage = new(32, 52);
        public UpdateField<float> MaxDamage = new(32, 53);
        public UpdateField<float> MinOffHandDamage = new(32, 54);
        public UpdateField<float> MaxOffHandDamage = new(32, 55);
        public UpdateField<byte> StandState = new(32, 56);
        public UpdateField<byte> PetTalentPoints = new(32, 57);
        public UpdateField<byte> VisFlags = new(32, 58);
        public UpdateField<byte> AnimTier = new(32, 59);
        public UpdateField<uint> PetNumber = new(32, 60);
        public UpdateField<uint> PetNameTimestamp = new(32, 61);
        public UpdateField<int> PetExperience = new(32, 62);
        public UpdateField<int> PetNextLevelExperience = new(32, 63);
        public UpdateField<float> ModCastingSpeed = new(64, 65);
        public UpdateField<float> ModSpellHaste = new(64, 66);
        public UpdateField<float> ModHaste = new(64, 67);
        public UpdateField<float> ModRangedHaste = new(64, 68);
        public UpdateField<float> ModHasteRegen = new(64, 69);
        public UpdateField<float> ModTimeRate = new(64, 70);
        public UpdateField<int> CreatedBySpell = new(64, 71);
        public UpdateField<int> EmoteState = new(64, 72);
        public UpdateField<short> TrainingPointsUsed = new(64, 73);
        public UpdateField<short> TrainingPointsTotal = new(64, 74);
        public UpdateField<int> BaseMana = new(64, 75);
        public UpdateField<int> BaseHealth = new(64, 76);
        public UpdateField<byte> SheatheState = new(64, 77);
        public UpdateField<byte> PvpFlags = new(64, 78);
        public UpdateField<byte> PetFlags = new(64, 79);
        public UpdateField<byte> ShapeshiftForm = new(64, 80);
        public UpdateField<int> AttackPower = new(64, 81);
        public UpdateField<int> AttackPowerModPos = new(64, 82);
        public UpdateField<int> AttackPowerModNeg = new(64, 83);
        public UpdateField<float> AttackPowerMultiplier = new(64, 84);
        public UpdateField<int> RangedAttackPower = new(64, 85);
        public UpdateField<int> RangedAttackPowerModPos = new(64, 86);
        public UpdateField<int> RangedAttackPowerModNeg = new(64, 87);
        public UpdateField<float> RangedAttackPowerMultiplier = new(64, 88);
        public UpdateField<int> SetAttackSpeedAura = new(64, 89);
        public UpdateField<float> Lifesteal = new(64, 90);
        public UpdateField<float> MinRangedDamage = new(64, 91);
        public UpdateField<float> MaxRangedDamage = new(64, 92);
        public UpdateField<float> MaxHealthModifier = new(64, 93);
        public UpdateField<float> HoverHeight = new(64, 94);
        public UpdateField<int> MinItemLevelCutoff = new(64, 95);
        public UpdateField<int> MinItemLevel = new(96, 97);
        public UpdateField<int> MaxItemLevel = new(96, 98);
        public UpdateField<int> WildBattlePetLevel = new(96, 99);
        public UpdateField<uint> BattlePetCompanionNameTimestamp = new(96, 100);
        public UpdateField<int> InteractSpellID = new(96, 101);
        public UpdateField<int> ScaleDuration = new(96, 102);
        public UpdateField<int> LooksLikeMountID = new(96, 103);
        public UpdateField<int> LooksLikeCreatureID = new(96, 104);
        public UpdateField<int> LookAtControllerID = new(96, 105);
        public UpdateField<int> PerksVendorItemID = new(96, 106);
        public UpdateField<ObjectGuid> GuildGUID = new(96, 107);
        public UpdateField<ObjectGuid> SkinningOwnerGUID = new(96, 108);
        public UpdateField<int> FlightCapabilityID = new(96, 109);
        public UpdateField<float> GlideEventSpeedDivisor = new(96, 110);                         // Movement speed gets divided by this value when evaluating what GlideEvents to use        
        public UpdateField<uint> CurrentAreaID = new(96, 111);
        public UpdateField<ObjectGuid> ComboTarget = new(96, 112);
        public UpdateFieldArray<uint> NpcFlags = new(2, 113, 114);
        public UpdateFieldArray<float> PowerRegenFlatModifier = new(10, 116, 117);
        public UpdateFieldArray<float> PowerRegenInterruptedFlatModifier = new(10, 116, 127);
        public UpdateFieldArray<int> Power = new(10, 116, 137);
        public UpdateFieldArray<int> MaxPower = new(10, 116, 147);
        public UpdateFieldArray<float> ModPowerRegen = new(10, 116, 157);                        // Applies to power regen only if expansion < 2, hidden from lua
        public UpdateFieldArray<VisibleItem> VirtualItems = new(3, 167, 168);
        public UpdateFieldArray<uint> AttackRoundBaseTime = new(2, 171, 172);
        public UpdateFieldArray<int> Stats = new(5, 174, 175);
        public UpdateFieldArray<int> StatPosBuff = new(5, 174, 180);
        public UpdateFieldArray<int> StatNegBuff = new(5, 174, 185);
        public UpdateFieldArray<int> Resistances = new(7, 190, 191);
        public UpdateFieldArray<int> PowerCostModifier = new(7, 190, 198);
        public UpdateFieldArray<float> PowerCostMultiplier = new(7, 190, 205);
        public UpdateFieldArray<int> ResistanceBuffModsPositive = new(7, 212, 213);
        public UpdateFieldArray<int> ResistanceBuffModsNegative = new(7, 212, 220);
        static int ChangeMaskLength = 227;

        public UnitData() : base(0, TypeId.Unit, ChangeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Unit owner, Player receiver)
        {
            data.WriteInt64(Health);
            data.WriteInt64(MaxHealth);
            data.WriteInt32(GetViewerDependentDisplayId(this, owner, receiver));
            for (int i = 0; i < 2; ++i)
                data.WriteUInt32(GetViewerDependentNpcFlags(this, i, owner, receiver));

            data.WriteUInt32(StateSpellVisualID);
            data.WriteUInt32(StateAnimID);
            data.WriteUInt32(StateAnimKitID);
            data.WriteInt32(StateWorldEffectIDs.GetValue().Count);
            for (int i = 0; i < StateWorldEffectIDs.GetValue().Count; ++i)
                data.WriteUInt32(StateWorldEffectIDs.GetValue()[i]);

            data.WritePackedGuid(Charm);
            data.WritePackedGuid(Summon);
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
                data.WritePackedGuid(Critter);

            data.WritePackedGuid(CharmedBy);
            data.WritePackedGuid(SummonedBy);
            data.WritePackedGuid(CreatedBy);
            data.WritePackedGuid(DemonCreator);
            data.WritePackedGuid(LookAtControllerTarget);
            data.WritePackedGuid(Target);
            data.WritePackedGuid(BattlePetCompanionGUID);
            data.WriteUInt64(BattlePetDBID);
            ChannelData.GetValue().WriteCreate(data, owner, receiver);
            data.WriteUInt32(SummonedByHomeRealm);
            data.WriteUInt8(Race);
            data.WriteUInt8(ClassId);
            data.WriteUInt8(PlayerClassId);
            data.WriteUInt8(Sex);
            data.WriteUInt8(DisplayPower);
            data.WriteUInt32(OverrideDisplayPowerID);            
            if (fieldVisibilityFlags.HasAnyFlag(UpdateFieldFlag.Owner | UpdateFieldFlag.UnitAll))
            {
                for (int i = 0; i < 10; ++i)
                {
                    data.WriteFloat(PowerRegenFlatModifier[i]);
                    data.WriteFloat(PowerRegenInterruptedFlatModifier[i]);
                }
            }
            for (int i = 0; i < 10; ++i)
            {
                data.WriteInt32(Power[i]);
                data.WriteInt32(MaxPower[i]);
                data.WriteFloat(ModPowerRegen[i]);
            }
            data.WriteInt32(Level);
            data.WriteInt32(EffectiveLevel);
            data.WriteInt32(ContentTuningID);
            data.WriteInt32(ScalingLevelMin);
            data.WriteInt32(ScalingLevelMax);
            data.WriteInt32(ScalingLevelDelta);
            data.WriteInt32(ScalingFactionGroup);
            data.WriteInt32(ScalingHealthItemLevelCurveID);
            data.WriteInt32(ScalingDamageItemLevelCurveID);
            data.WriteInt32(GetViewerDependentFactionTemplate(this, owner, receiver));
            for (int i = 0; i < 3; ++i)
                VirtualItems[i].WriteCreate(data, owner, receiver);

            data.WriteUInt32(GetViewerDependentFlags(this, owner, receiver));
            data.WriteUInt32(Flags2);
            data.WriteUInt32(GetViewerDependentFlags3(this, owner, receiver));
            data.WriteUInt32(GetViewerDependentAuraState(this, owner, receiver));
            for (int i = 0; i < 2; ++i)
                data.WriteUInt32(AttackRoundBaseTime[i]);

            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
                data.WriteUInt32(RangedAttackRoundBaseTime);

            data.WriteFloat(BoundingRadius);
            data.WriteFloat(CombatReach);
            data.WriteFloat(DisplayScale);
            data.WriteInt32(NativeDisplayID);
            data.WriteFloat(NativeXDisplayScale);
            data.WriteInt32(MountDisplayID);
            if (fieldVisibilityFlags.HasAnyFlag(UpdateFieldFlag.Owner | UpdateFieldFlag.Empath))
            {
                data.WriteFloat(MinDamage);
                data.WriteFloat(MaxDamage);
                data.WriteFloat(MinOffHandDamage);
                data.WriteFloat(MaxOffHandDamage);
            }
            data.WriteUInt8(StandState);
            data.WriteUInt8(PetTalentPoints);
            data.WriteUInt8(VisFlags);
            data.WriteUInt8(AnimTier);
            data.WriteUInt32(PetNumber);
            data.WriteUInt32(PetNameTimestamp);
            data.WriteUInt32(PetExperience);
            data.WriteUInt32(PetNextLevelExperience);
            data.WriteFloat(ModCastingSpeed);
            data.WriteFloat(ModSpellHaste);
            data.WriteFloat(ModHaste);
            data.WriteFloat(ModRangedHaste);
            data.WriteFloat(ModHasteRegen);
            data.WriteFloat(ModTimeRate);
            data.WriteInt32(CreatedBySpell);
            data.WriteInt32(EmoteState);
            data.WriteInt16(TrainingPointsUsed);
            data.WriteInt16(TrainingPointsTotal);
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
            {
                for (int i = 0; i < 5; ++i)
                {
                    data.WriteInt32(Stats[i]);
                    data.WriteInt32(StatPosBuff[i]);
                    data.WriteInt32(StatNegBuff[i]);
                }
            }
            if (fieldVisibilityFlags.HasAnyFlag(UpdateFieldFlag.Owner | UpdateFieldFlag.Empath))
            {
                for (int i = 0; i < 7; ++i)
                {
                    data.WriteInt32(Resistances[i]);
                }
            }
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
            {
                for (int i = 0; i < 7; ++i)
                {
                    data.WriteInt32(PowerCostModifier[i]);
                    data.WriteFloat(PowerCostMultiplier[i]);
                }
            }
            for (int i = 0; i < 7; ++i)
            {
                data.WriteInt32(ResistanceBuffModsPositive[i]);
                data.WriteInt32(ResistanceBuffModsNegative[i]);
            }
            data.WriteInt32(BaseMana);
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
                data.WriteInt32(BaseHealth);

            data.WriteUInt8(SheatheState);
            data.WriteUInt8(GetViewerDependentPvpFlags(this, owner, receiver));
            data.WriteUInt8(PetFlags);
            data.WriteUInt8(ShapeshiftForm);
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
            {
                data.WriteInt32(AttackPower);
                data.WriteInt32(AttackPowerModPos);
                data.WriteInt32(AttackPowerModNeg);
                data.WriteFloat(AttackPowerMultiplier);
                data.WriteInt32(RangedAttackPower);
                data.WriteInt32(RangedAttackPowerModPos);
                data.WriteInt32(RangedAttackPowerModNeg);
                data.WriteFloat(RangedAttackPowerMultiplier);
                data.WriteInt32(SetAttackSpeedAura);
                data.WriteFloat(Lifesteal);
                data.WriteFloat(MinRangedDamage);
                data.WriteFloat(MaxRangedDamage);
                data.WriteFloat(MaxHealthModifier);
            }
            data.WriteFloat(HoverHeight);
            data.WriteInt32(MinItemLevelCutoff);
            data.WriteInt32(MinItemLevel);
            data.WriteInt32(MaxItemLevel);
            data.WriteInt32(WildBattlePetLevel);
            data.WriteUInt32(BattlePetCompanionNameTimestamp);
            data.WriteInt32(InteractSpellID);
            data.WriteInt32(ScaleDuration);
            data.WriteInt32(LooksLikeMountID);
            data.WriteInt32(LooksLikeCreatureID);
            data.WriteInt32(LookAtControllerID);
            data.WriteInt32(PerksVendorItemID);
            data.WritePackedGuid(GuildGUID);
            data.WriteInt32(PassiveSpells.Size());
            data.WriteInt32(WorldEffects.Size());
            data.WriteInt32(ChannelObjects.Size());
            data.WritePackedGuid(SkinningOwnerGUID);
            data.WriteInt32(FlightCapabilityID);
            data.WriteFloat(GlideEventSpeedDivisor);
            data.WriteUInt32(CurrentAreaID);

            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
                data.WritePackedGuid(ComboTarget);

            for (int i = 0; i < PassiveSpells.Size(); ++i)
                PassiveSpells[i].WriteCreate(data, owner, receiver);

            for (int i = 0; i < WorldEffects.Size(); ++i)
                data.WriteInt32(WorldEffects[i]);

            for (int i = 0; i < ChannelObjects.Size(); ++i)
                data.WritePackedGuid(ChannelObjects[i]);
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Unit owner, Player receiver)
        {
            UpdateMask allowedMaskForTarget = new(ChangeMaskLength, [0xFFFFDFFFu, 0xFF0FDFFFu, 0xC001EFFFu, 0x001EFFFFu, 0xFFFFFE00u, 0x00003FFFu, 0xFFF00000u, 0x00000007u]);
            AppendAllowedFieldsMaskForFlag(allowedMaskForTarget, fieldVisibilityFlags);
            WriteUpdate(data, _changesMask & allowedMaskForTarget, false, owner, receiver);
        }

        public void AppendAllowedFieldsMaskForFlag(UpdateMask allowedMaskForTarget, UpdateFieldFlag fieldVisibilityFlags)
        {
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
                allowedMaskForTarget.OR(new UpdateMask(ChangeMaskLength, [0x00002000u, 0x00F02000u, 0x3FFE1000u, 0xFFF10000u, 0x000001FFu, 0xFFFFC000u, 0x000FFFFFu, 0x00000000u]));
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.UnitAll))
                allowedMaskForTarget.OR(new UpdateMask(ChangeMaskLength, [0x00000000u, 0x00000000u, 0x00000000u, 0xFFF00000u, 0x000001FFu, 0x00000000u, 0x00000000u, 0x00000000u]));
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Empath))
                allowedMaskForTarget.OR(new UpdateMask(ChangeMaskLength, [0x00000000u, 0x00F00000u, 0x00000000u, 0x00000000u, 0x00000000u, 0xC0000000u, 0x0000003Fu, 0x00000000u]));
        }

        public void FilterDisallowedFieldsMaskForFlag(UpdateMask changesMask, UpdateFieldFlag fieldVisibilityFlags)
        {
            UpdateMask allowedMaskForTarget = new(ChangeMaskLength, [0xFFFFDFFFu, 0xFF0FDFFFu, 0xC001EFFFu, 0x001EFFFFu, 0xFFFFFE00u, 0x00003FFFu, 0xFFF00000u, 0x00000007u]);
            AppendAllowedFieldsMaskForFlag(allowedMaskForTarget, fieldVisibilityFlags);
            changesMask.AND(allowedMaskForTarget);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, Unit owner, Player receiver)
        {
            data.WriteBits(changesMask.GetBlocksMask(0), 8);
            for (uint i = 0; i < 8; ++i)
                if (changesMask.GetBlock(i) != 0)
                    data.WriteBits(changesMask.GetBlock(i), 32);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteBits(StateWorldEffectIDs.GetValue().Count, 32);
                    for (int i = 0; i < StateWorldEffectIDs.GetValue().Count; ++i)
                    {
                        data.WriteUInt32(StateWorldEffectIDs.GetValue()[i]);
                    }
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[2])
                {
                    if (!ignoreNestedChangesMask)
                        PassiveSpells.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(PassiveSpells.Size(), data);
                }
                if (changesMask[3])
                {
                    if (!ignoreNestedChangesMask)
                        WorldEffects.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(WorldEffects.Size(), data);
                }
                if (changesMask[4])
                {
                    if (!ignoreNestedChangesMask)
                        ChannelObjects.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(ChannelObjects.Size(), data);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[2])
                {
                    for (int i = 0; i < PassiveSpells.Size(); ++i)
                    {
                        if (PassiveSpells.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            PassiveSpells[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[3])
                {
                    for (int i = 0; i < WorldEffects.Size(); ++i)
                    {
                        if (WorldEffects.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(WorldEffects[i]);
                        }
                    }
                }
                if (changesMask[4])
                {
                    for (int i = 0; i < ChannelObjects.Size(); ++i)
                    {
                        if (ChannelObjects.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WritePackedGuid(ChannelObjects[i]);
                        }
                    }
                }
                if (changesMask[5])
                {
                    data.WriteInt64(Health);
                }
                if (changesMask[6])
                {
                    data.WriteInt64(MaxHealth);
                }
                if (changesMask[7])
                {
                    data.WriteInt32(GetViewerDependentDisplayId(this, owner, receiver));
                }
                if (changesMask[8])
                {
                    data.WriteUInt32(StateSpellVisualID);
                }
                if (changesMask[9])
                {
                    data.WriteUInt32(StateAnimID);
                }
                if (changesMask[10])
                {
                    data.WriteUInt32(StateAnimKitID);
                }
                if (changesMask[11])
                {
                    data.WritePackedGuid(Charm);
                }
                if (changesMask[12])
                {
                    data.WritePackedGuid(Summon);
                }
                if (changesMask[13])
                {
                    data.WritePackedGuid(Critter);
                }
                if (changesMask[14])
                {
                    data.WritePackedGuid(CharmedBy);
                }
                if (changesMask[15])
                {
                    data.WritePackedGuid(SummonedBy);
                }
                if (changesMask[16])
                {
                    data.WritePackedGuid(CreatedBy);
                }
                if (changesMask[17])
                {
                    data.WritePackedGuid(DemonCreator);
                }
                if (changesMask[18])
                {
                    data.WritePackedGuid(LookAtControllerTarget);
                }
                if (changesMask[19])
                {
                    data.WritePackedGuid(Target);
                }
                if (changesMask[20])
                {
                    data.WritePackedGuid(BattlePetCompanionGUID);
                }
                if (changesMask[21])
                {
                    data.WriteUInt64(BattlePetDBID);
                }
                if (changesMask[22])
                {
                    ChannelData.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
                if (changesMask[23])
                {
                    data.WriteUInt32(SummonedByHomeRealm);
                }
                if (changesMask[24])
                {
                    data.WriteUInt8(Race);
                }
                if (changesMask[25])
                {
                    data.WriteUInt8(ClassId);
                }
                if (changesMask[26])
                {
                    data.WriteUInt8(PlayerClassId);
                }
                if (changesMask[27])
                {
                    data.WriteUInt8(Sex);
                }
                if (changesMask[28])
                {
                    data.WriteUInt8(DisplayPower);
                }
                if (changesMask[29])
                {
                    data.WriteUInt32(OverrideDisplayPowerID);
                }
                if (changesMask[30])
                {
                    data.WriteInt32(Level);
                }
                if (changesMask[31])
                {
                    data.WriteInt32(EffectiveLevel);
                }
            }
            if (changesMask[32])
            {
                if (changesMask[33])
                {
                    data.WriteInt32(ContentTuningID);
                }
                if (changesMask[34])
                {
                    data.WriteInt32(ScalingLevelMin);
                }
                if (changesMask[35])
                {
                    data.WriteInt32(ScalingLevelMax);
                }
                if (changesMask[36])
                {
                    data.WriteInt32(ScalingLevelDelta);
                }
                if (changesMask[37])
                {
                    data.WriteInt32(ScalingFactionGroup);
                }
                if (changesMask[38])
                {
                    data.WriteInt32(ScalingHealthItemLevelCurveID);
                }
                if (changesMask[39])
                {
                    data.WriteInt32(ScalingDamageItemLevelCurveID);
                }
                if (changesMask[40])
                {
                    data.WriteInt32(GetViewerDependentFactionTemplate(this, owner, receiver));
                }
                if (changesMask[41])
                {
                    data.WriteUInt32(GetViewerDependentFlags(this, owner, receiver));
                }
                if (changesMask[42])
                {
                    data.WriteUInt32(Flags2);
                }
                if (changesMask[43])
                {
                    data.WriteUInt32(GetViewerDependentFlags3(this, owner, receiver));
                }
                if (changesMask[44])
                {
                    data.WriteUInt32(GetViewerDependentAuraState(this, owner, receiver));
                }
                if (changesMask[45])
                {
                    data.WriteUInt32(RangedAttackRoundBaseTime);
                }
                if (changesMask[46])
                {
                    data.WriteFloat(BoundingRadius);
                }
                if (changesMask[47])
                {
                    data.WriteFloat(CombatReach);
                }
                if (changesMask[48])
                {
                    data.WriteFloat(DisplayScale);
                }
                if (changesMask[49])
                {
                    data.WriteInt32(NativeDisplayID);
                }
                if (changesMask[50])
                {
                    data.WriteFloat(NativeXDisplayScale);
                }
                if (changesMask[51])
                {
                    data.WriteInt32(MountDisplayID);
                }
                if (changesMask[52])                
                {
                    data.WriteFloat(MinDamage);
                }
                if (changesMask[53])
                {
                    data.WriteFloat(MaxDamage);
                }
                if (changesMask[54])
                {
                    data.WriteFloat(MinOffHandDamage);
                }
                if (changesMask[55])
                {
                    data.WriteFloat(MaxOffHandDamage);
                }
                if (changesMask[56])
                {
                    data.WriteUInt8(StandState);
                }
                if (changesMask[57])
                {
                    data.WriteUInt8(PetTalentPoints);
                }
                if (changesMask[58])
                {
                    data.WriteUInt8(VisFlags);
                }
                if (changesMask[59])
                {
                    data.WriteUInt8(AnimTier);
                }
                if (changesMask[60])
                {
                    data.WriteUInt32(PetNumber);
                }
                if (changesMask[61])
                {
                    data.WriteUInt32(PetNameTimestamp);
                }
                if (changesMask[62])
                {
                    data.WriteUInt32(PetExperience);
                }
                if (changesMask[63])
                {
                    data.WriteUInt32(PetNextLevelExperience);
                }
            }
            if (changesMask[64])
            {
                if (changesMask[65])
                {
                    data.WriteFloat(ModCastingSpeed);
                }
                if (changesMask[66])
                {
                    data.WriteFloat(ModSpellHaste);
                }
                if (changesMask[67])
                {
                    data.WriteFloat(ModHaste);
                }
                if (changesMask[68])
                {
                    data.WriteFloat(ModRangedHaste);
                }
                if (changesMask[69])
                {
                    data.WriteFloat(ModHasteRegen);
                }
                if (changesMask[70])
                {
                    data.WriteFloat(ModTimeRate);
                }
                if (changesMask[71])
                {
                    data.WriteInt32(CreatedBySpell);
                }
                if (changesMask[72])
                {
                    data.WriteInt32(EmoteState);
                }
                if (changesMask[73])
                {
                    data.WriteInt16(TrainingPointsUsed);
                }
                if (changesMask[74])
                {
                    data.WriteInt16(TrainingPointsTotal);
                }
                if (changesMask[75])
                {
                    data.WriteInt32(BaseMana);
                }
                if (changesMask[76])
                {
                    data.WriteInt32(BaseHealth);
                }
                if (changesMask[77])
                {
                    data.WriteUInt8(SheatheState);
                }
                if (changesMask[78])
                {
                    data.WriteUInt8(GetViewerDependentPvpFlags(this, owner, receiver));
                }
                if (changesMask[79])
                {
                    data.WriteUInt8(PetFlags);
                }
                if (changesMask[80])
                {
                    data.WriteUInt8(ShapeshiftForm);
                }
                if (changesMask[81])
                {
                    data.WriteInt32(AttackPower);
                }
                if (changesMask[82])
                {
                    data.WriteInt32(AttackPowerModPos);
                }
                if (changesMask[83])
                {
                    data.WriteInt32(AttackPowerModNeg);
                }
                if (changesMask[84])
                {
                    data.WriteFloat(AttackPowerMultiplier);
                }
                if (changesMask[85])
                {
                    data.WriteInt32(RangedAttackPower);
                }
                if (changesMask[86])
                {
                    data.WriteInt32(RangedAttackPowerModPos);
                }
                if (changesMask[87])
                {
                    data.WriteFloat(RangedAttackPowerModNeg);
                }
                if (changesMask[88])
                {
                    data.WriteFloat(RangedAttackPowerMultiplier);
                }
                if (changesMask[89])
                {
                    data.WriteFloat(SetAttackSpeedAura);
                }
                if (changesMask[90])
                {
                    data.WriteFloat(Lifesteal);
                }
                if (changesMask[91])
                {
                    data.WriteFloat(MinRangedDamage);
                }
                if (changesMask[92])
                {
                    data.WriteFloat(MaxRangedDamage);
                }
                if (changesMask[93])
                {
                    data.WriteFloat(MaxHealthModifier);
                }
                if (changesMask[94])
                {
                    data.WriteFloat(HoverHeight);
                }
                if (changesMask[95])
                {
                    data.WriteInt32(MinItemLevelCutoff);
                }
            }
            if (changesMask[96])
            {
                if (changesMask[97])
                {
                    data.WriteInt32(MinItemLevel);
                }
                if (changesMask[98])
                {
                    data.WriteInt32(MaxItemLevel);
                }
                if (changesMask[99])
                {
                    data.WriteInt32(WildBattlePetLevel);
                }
                if (changesMask[100])
                {
                    data.WriteUInt32(BattlePetCompanionNameTimestamp);
                }
                if (changesMask[101])
                {
                    data.WriteInt32(InteractSpellID);
                }
                if (changesMask[102])
                {
                    data.WriteInt32(ScaleDuration);
                }
                if (changesMask[103])
                {
                    data.WriteInt32(LooksLikeMountID);
                }
                if (changesMask[104])
                {
                    data.WriteInt32(LooksLikeCreatureID);
                }
                if (changesMask[105])
                {
                    data.WriteInt32(LookAtControllerID);
                }
                if (changesMask[106])
                {
                    data.WriteInt32(PerksVendorItemID);
                }
                if (changesMask[107])
                {
                    data.WritePackedGuid(GuildGUID);
                }
                if (changesMask[108])
                {
                    data.WritePackedGuid(SkinningOwnerGUID);
                }
                if (changesMask[109])
                {
                    data.WriteInt32(FlightCapabilityID);
                }
                if (changesMask[110])
                {
                    data.WriteFloat(GlideEventSpeedDivisor);
                }
                if (changesMask[111])
                {
                    data.WriteUInt32(CurrentAreaID);
                }
                if (changesMask[112])
                {
                    data.WritePackedGuid(ComboTarget);
                }
            }
            if (changesMask[113])
            {
                for (int i = 0; i < 2; ++i)
                {
                    if (changesMask[114 + i])
                    {
                        data.WriteUInt32(GetViewerDependentNpcFlags(this, i, owner, receiver));
                    }
                }
            }
            if (changesMask[116])
            {
                for (int i = 0; i < 10; ++i)
                {
                    if (changesMask[117 + i])
                    {
                        data.WriteFloat(PowerRegenFlatModifier[i]);
                    }
                    if (changesMask[127 + i])
                    {
                        data.WriteFloat(PowerRegenInterruptedFlatModifier[i]);
                    }
                    if (changesMask[137 + i])
                    {
                        data.WriteInt32(Power[i]);
                    }
                    if (changesMask[147 + i])
                    {
                        data.WriteInt32(MaxPower[i]);
                    }
                    if (changesMask[157 + i])
                    {
                        data.WriteFloat(ModPowerRegen[i]);
                    }
                }
            }
            if (changesMask[167])
            {
                for (int i = 0; i < 3; ++i)
                {
                    if (changesMask[168 + i])
                    {
                        VirtualItems[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                    }
                }
            }
            if (changesMask[171])
            {
                for (int i = 0; i < 2; ++i)
                {
                    if (changesMask[172 + i])
                    {
                        data.WriteUInt32(AttackRoundBaseTime[i]);
                    }
                }
            }
            if (changesMask[174])
            {
                for (int i = 0; i < 5; ++i)
                {
                    if (changesMask[175 + i])
                    {
                        data.WriteInt32(Stats[i]);
                    }
                    if (changesMask[180 + i])
                    {
                        data.WriteInt32(StatPosBuff[i]);
                    }
                    if (changesMask[185 + i])
                    {
                        data.WriteInt32(StatNegBuff[i]);
                    }
                }
            }
            if (changesMask[190])
            {
                for (int i = 0; i < 7; ++i)
                {
                    if (changesMask[191 + i])
                    {
                        data.WriteInt32(Resistances[i]);
                    }
                    if (changesMask[198 + i])
                    {
                        data.WriteInt32(PowerCostModifier[i]);
                    }
                    if (changesMask[205 + i])
                    {
                        data.WriteFloat(PowerCostMultiplier[i]);
                    }
                }
            }
            if (changesMask[212])
            {
                for (int i = 0; i < 7; ++i)
                {
                    if (changesMask[213 + i])
                    {
                        data.WriteInt32(ResistanceBuffModsPositive[i]);
                    }
                    if (changesMask[220 + i])
                    {
                        data.WriteInt32(ResistanceBuffModsNegative[i]);
                    }
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(StateWorldEffectIDs);
            ClearChangesMask(PassiveSpells);
            ClearChangesMask(WorldEffects);
            ClearChangesMask(ChannelObjects);
            ClearChangesMask(Health);
            ClearChangesMask(MaxHealth);
            ClearChangesMask(DisplayID);

            ClearChangesMask(StateSpellVisualID);
            ClearChangesMask(StateAnimID);
            ClearChangesMask(StateAnimKitID);
            ClearChangesMask(Charm);
            ClearChangesMask(Summon);
            ClearChangesMask(Critter);
            ClearChangesMask(CharmedBy);

            ClearChangesMask(SummonedBy);
            ClearChangesMask(CreatedBy);
            ClearChangesMask(DemonCreator);
            ClearChangesMask(LookAtControllerTarget);
            ClearChangesMask(Target);
            ClearChangesMask(BattlePetCompanionGUID);
            ClearChangesMask(BattlePetDBID);

            ClearChangesMask(ChannelData);
            ClearChangesMask(SummonedByHomeRealm);
            ClearChangesMask(Race);
            ClearChangesMask(ClassId);
            ClearChangesMask(PlayerClassId);
            ClearChangesMask(Sex);
            ClearChangesMask(DisplayPower);

            ClearChangesMask(OverrideDisplayPowerID);
            ClearChangesMask(Level);
            ClearChangesMask(EffectiveLevel);
            ClearChangesMask(ContentTuningID);
            ClearChangesMask(ScalingLevelMin);
            ClearChangesMask(ScalingLevelMax);
            ClearChangesMask(ScalingLevelDelta);

            ClearChangesMask(ScalingFactionGroup);
            ClearChangesMask(ScalingHealthItemLevelCurveID);
            ClearChangesMask(ScalingDamageItemLevelCurveID);
            ClearChangesMask(FactionTemplate);
            ClearChangesMask(Flags);
            ClearChangesMask(Flags2);
            ClearChangesMask(Flags3);

            ClearChangesMask(AuraState);
            ClearChangesMask(RangedAttackRoundBaseTime);
            ClearChangesMask(BoundingRadius);
            ClearChangesMask(CombatReach);
            ClearChangesMask(DisplayScale);
            ClearChangesMask(NativeDisplayID);
            ClearChangesMask(NativeXDisplayScale);

            ClearChangesMask(MountDisplayID);
            ClearChangesMask(MinDamage);
            ClearChangesMask(MaxDamage);
            ClearChangesMask(MinOffHandDamage);
            ClearChangesMask(MaxOffHandDamage);
            ClearChangesMask(StandState);
            ClearChangesMask(PetTalentPoints);

            ClearChangesMask(VisFlags);
            ClearChangesMask(AnimTier);
            ClearChangesMask(PetNumber);
            ClearChangesMask(PetNameTimestamp);
            ClearChangesMask(PetExperience);
            ClearChangesMask(PetNextLevelExperience);
            ClearChangesMask(ModCastingSpeed);

            ClearChangesMask(ModSpellHaste);
            ClearChangesMask(ModHaste);
            ClearChangesMask(ModRangedHaste);
            ClearChangesMask(ModHasteRegen);
            ClearChangesMask(ModTimeRate);
            ClearChangesMask(CreatedBySpell);
            ClearChangesMask(EmoteState);

            ClearChangesMask(TrainingPointsUsed);
            ClearChangesMask(TrainingPointsTotal);
            ClearChangesMask(BaseMana);
            ClearChangesMask(BaseHealth);
            ClearChangesMask(SheatheState);
            ClearChangesMask(PvpFlags);
            ClearChangesMask(PetFlags);

            ClearChangesMask(ShapeshiftForm);
            ClearChangesMask(AttackPower);
            ClearChangesMask(AttackPowerModPos);
            ClearChangesMask(AttackPowerModNeg);
            ClearChangesMask(AttackPowerMultiplier);
            ClearChangesMask(RangedAttackPower);
            ClearChangesMask(RangedAttackPowerModPos);
            ClearChangesMask(RangedAttackPowerModNeg);
            ClearChangesMask(RangedAttackPowerMultiplier);

            ClearChangesMask(SetAttackSpeedAura);
            ClearChangesMask(Lifesteal);
            ClearChangesMask(MinRangedDamage);
            ClearChangesMask(MaxRangedDamage);
            ClearChangesMask(MaxHealthModifier);
            ClearChangesMask(HoverHeight);
            ClearChangesMask(MinItemLevelCutoff);

            ClearChangesMask(MinItemLevel);
            ClearChangesMask(MaxItemLevel);
            ClearChangesMask(WildBattlePetLevel);
            ClearChangesMask(BattlePetCompanionNameTimestamp);
            ClearChangesMask(InteractSpellID);
            ClearChangesMask(ScaleDuration);
            ClearChangesMask(LooksLikeMountID);

            ClearChangesMask(LooksLikeCreatureID);
            ClearChangesMask(LookAtControllerID);
            ClearChangesMask(PerksVendorItemID);
            ClearChangesMask(GuildGUID);
            ClearChangesMask(SkinningOwnerGUID);
            ClearChangesMask(FlightCapabilityID);
            ClearChangesMask(GlideEventSpeedDivisor);

            ClearChangesMask(CurrentAreaID);
            ClearChangesMask(ComboTarget);
            ClearChangesMask(NpcFlags);
            ClearChangesMask(PowerRegenFlatModifier);
            ClearChangesMask(PowerRegenInterruptedFlatModifier);
            ClearChangesMask(Power);
            ClearChangesMask(MaxPower);

            ClearChangesMask(ModPowerRegen);
            ClearChangesMask(VirtualItems);
            ClearChangesMask(AttackRoundBaseTime);
            ClearChangesMask(Stats);
            ClearChangesMask(StatPosBuff);
            ClearChangesMask(StatNegBuff);
            ClearChangesMask(Resistances);

            ClearChangesMask(PowerCostModifier);
            ClearChangesMask(PowerCostMultiplier);
            ClearChangesMask(ResistanceBuffModsPositive);
            ClearChangesMask(ResistanceBuffModsNegative);
            _changesMask.ResetAll();
        }

        int GetViewerDependentDisplayId(UnitData unitData, Unit unit, Player receiver)
        {
            int displayId = unitData.DisplayID;
            if (unit.IsCreature())
            {
                CreatureTemplate cinfo = unit.ToCreature().GetCreatureTemplate();

                if (unit.ToTempSummon() is TempSummon summon)
                {
                    if (summon.GetSummonerGUID() == receiver.GetGUID())
                    {
                        if (summon.GetCreatureIdVisibleToSummoner().HasValue)
                            cinfo = Global.ObjectMgr.GetCreatureTemplate(summon.GetCreatureIdVisibleToSummoner().Value);

                        if (summon.GetDisplayIdVisibleToSummoner().HasValue)
                            displayId = (int)summon.GetDisplayIdVisibleToSummoner().Value;
                    }
                }

                // this also applies for transform auras
                SpellInfo transform = Global.SpellMgr.GetSpellInfo(unit.GetTransformSpell(), unit.GetMap().GetDifficultyID());
                if (transform != null)
                {
                    foreach (var spellEffectInfo in transform.GetEffects())
                    {
                        if (spellEffectInfo.IsAura(AuraType.Transform))
                        {
                            CreatureTemplate transformInfo = Global.ObjectMgr.GetCreatureTemplate((uint)spellEffectInfo.MiscValue);
                            if (transformInfo != null)
                            {
                                cinfo = transformInfo;
                                break;
                            }
                        }
                    }
                }

                if (cinfo.FlagsExtra.HasAnyFlag(CreatureFlagsExtra.Trigger))
                    if (receiver.IsGameMaster())
                        displayId = (int)cinfo.GetFirstVisibleModel().CreatureDisplayID;
            }

            return displayId;
        }

        uint GetViewerDependentNpcFlags(UnitData unitData, int i, Unit unit, Player receiver)
        {
            uint npcFlag = unitData.NpcFlags[i];
            if (i == 0 && unit.IsCreature() && !receiver.CanSeeSpellClickOn(unit.ToCreature()))
                npcFlag &= ~(uint)NPCFlags.SpellClick;

            return npcFlag;
        }

        int GetViewerDependentFactionTemplate(UnitData unitData, Unit unit, Player receiver)
        {
            int factionTemplate = unitData.FactionTemplate;
            if (unit.IsControlledByPlayer() && receiver != unit && WorldConfig.GetBoolValue(WorldCfg.AllowTwoSideInteractionGroup) && unit.IsInRaidWith(receiver))
            {
                FactionTemplateRecord ft1 = unit.GetFactionTemplateEntry();
                FactionTemplateRecord ft2 = receiver.GetFactionTemplateEntry();
                if (ft1 != null && ft2 != null && !ft1.IsFriendlyTo(ft2))
                    // pretend that all other HOSTILE players have own faction, to allow follow, heal, rezz (trade wont work)
                    factionTemplate = (int)receiver.GetFaction();
            }

            return factionTemplate;
        }

        uint GetViewerDependentFlags(UnitData unitData, Unit unit, Player receiver)
        {
            uint flags = unitData.Flags;
            // Update fields of triggers, transformed units or uninteractible units (values dependent on GM state)
            if (receiver.IsGameMaster())
                flags &= ~(uint)UnitFlags.Uninteractible;

            return flags;
        }

        uint GetViewerDependentFlags3(UnitData unitData, Unit unit, Player receiver)
        {
            uint flags = unitData.Flags3;
            if ((flags & (uint)UnitFlags3.AlreadySkinned) != 0 && unit.IsCreature() && !unit.ToCreature().IsSkinnedBy(receiver))
                flags &= ~(uint)UnitFlags3.AlreadySkinned;

            return flags;
        }

        uint GetViewerDependentAuraState(UnitData unitData, Unit unit, Player receiver)
        {
            // Check per caster aura states to not enable using a spell in client if specified aura is not by target
            return unit.BuildAuraStateUpdateForTarget(receiver);
        }
        byte GetViewerDependentPvpFlags(UnitData unitData, Unit unit, Player receiver)
        {
            byte pvpFlags = unitData.PvpFlags;
            if (unit.IsControlledByPlayer() && receiver != unit && WorldConfig.GetBoolValue(WorldCfg.AllowTwoSideInteractionGroup) && unit.IsInRaidWith(receiver))
            {
                FactionTemplateRecord ft1 = unit.GetFactionTemplateEntry();
                FactionTemplateRecord ft2 = receiver.GetFactionTemplateEntry();
                if (ft1 != null && ft2 != null && !ft1.IsFriendlyTo(ft2))
                    // Allow targeting opposite faction in party when enabled in config
                    pvpFlags &= (byte)UnitPVPStateFlags.Sanctuary;
            }

            return pvpFlags;
        }
    }

    public class ChrCustomizationChoice : IComparable<ChrCustomizationChoice>
    {
        public ChrCustomizationChoice() { }

        public ChrCustomizationChoice(WorldPacket data)
        {
            ChrCustomizationOptionID = data.ReadInt32();
            ChrCustomizationChoiceID = data.ReadInt32();
        }

        public void WriteCreate(WorldPacket data, WorldObject owner, Player receiver)
        {
            data.WriteInt32(ChrCustomizationOptionID);
            data.WriteInt32(ChrCustomizationChoiceID);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, WorldObject owner, Player receiver)
        {
            data.WriteInt32(ChrCustomizationOptionID);
            data.WriteInt32(ChrCustomizationChoiceID);
        }        

        public int CompareTo(ChrCustomizationChoice other)
        {
            return ChrCustomizationOptionID.CompareTo(other.ChrCustomizationOptionID);
        }

        public int ChrCustomizationOptionID;
        public int ChrCustomizationChoiceID;
    }

    public class QuestLog : HasChangesMask
    {
        public UpdateField<long> EndTime = new(0, 1);
        public UpdateField<int> QuestID = new(0, 2);
        public UpdateField<uint> StateFlags = new(0, 3);
        public UpdateFieldArray<ushort> ObjectiveProgress = new(24, 4, 5);
        static int changeMaskLength = 29;

        public QuestLog() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt64(EndTime);
            data.WriteInt32(QuestID);
            data.WriteUInt32(StateFlags);
            for (int i = 0; i < 24; ++i)
            {
                data.WriteUInt16(ObjectiveProgress[i]);
            }
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlocksMask(0), 1);
            if (changesMask.GetBlock(0) != 0)
                data.WriteBits(changesMask.GetBlock(0), 32);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteInt64(EndTime);
                }
                if (changesMask[2])
                {
                    data.WriteInt32(QuestID);
                }
                if (changesMask[3])
                {
                    data.WriteUInt32(StateFlags);
                }
            }
            if (changesMask[4])
            {
                for (int i = 0; i < 24; ++i)
                {
                    if (changesMask[5 + i])
                    {
                        data.WriteUInt16(ObjectiveProgress[i]);
                    }
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(EndTime);
            ClearChangesMask(QuestID);
            ClearChangesMask(StateFlags);
            ClearChangesMask(ObjectiveProgress);
            _changesMask.ResetAll();
        }
    }

    public class ArenaCooldown : HasChangesMask
    {
        public UpdateField<int> SpellID = new(0, 1);
        public UpdateField<int> ItemID = new(0, 2);
        public UpdateField<int> Charges = new(0, 3);
        public UpdateField<uint> Flags = new(0, 4);
        public UpdateField<uint> StartTime = new(0, 5);
        public UpdateField<uint> EndTime = new(0, 6);
        public UpdateField<uint> NextChargeTime = new(0, 7);
        public UpdateField<byte> MaxCharges = new(0, 8);
        static int changeMaskLength = 9;

        public ArenaCooldown() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(SpellID);
            data.WriteInt32(ItemID);
            data.WriteInt32(Charges);            
            data.WriteUInt32(Flags);
            data.WriteUInt32(StartTime);
            data.WriteUInt32(EndTime);
            data.WriteUInt32(NextChargeTime);
            data.WriteUInt8(MaxCharges);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteInt32(SpellID);
                }
                if (changesMask[2])
                {
                    data.WriteInt32(ItemID);
                }
                if (changesMask[3])
                {
                    data.WriteInt32(Charges);
                }                
                if (changesMask[4])
                {
                    data.WriteUInt32(Flags);
                }
                if (changesMask[5])
                {
                    data.WriteUInt32(StartTime);
                }
                if (changesMask[6])
                {
                    data.WriteUInt32(EndTime);
                }
                if (changesMask[7])
                {
                    data.WriteUInt32(NextChargeTime);
                }
                if (changesMask[8])
                {
                    data.WriteUInt8(MaxCharges);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(SpellID);
            ClearChangesMask(ItemID);
            ClearChangesMask(Charges);            
            ClearChangesMask(Flags);
            ClearChangesMask(StartTime);
            ClearChangesMask(EndTime);
            ClearChangesMask(NextChargeTime);
            ClearChangesMask(MaxCharges);
            _changesMask.ResetAll();
        }
    }

    public class PlayerData : HasChangesMask
    {
        public DynamicUpdateField<ChrCustomizationChoice> Customizations = new(0, 1);
        public DynamicUpdateField<ArenaCooldown> ArenaCooldowns = new(0, 2);
        public DynamicUpdateField<int> VisualItemReplacements = new(0, 3);
        public UpdateField<ObjectGuid> DuelArbiter = new(0, 4);
        public UpdateField<ObjectGuid> WowAccount = new(0, 5);
        public UpdateField<ObjectGuid> LootTargetGUID = new(0, 6);
        public UpdateField<PlayerFlags> PlayerFlags = new(0, 7);
        public UpdateField<PlayerFlagsEx> PlayerFlagsEx = new(0, 8);
        public UpdateField<uint> GuildRankID = new(0, 9);
        public UpdateField<uint> GuildDeleteDate = new(0, 10);
        public UpdateField<int> GuildLevel = new(0, 11);
        public UpdateField<byte> NumBankSlots = new(0, 12);
        public UpdateField<byte> NativeSex = new(0, 13);
        public UpdateField<byte> Inebriation = new(0, 14);
        public UpdateField<byte> PvpTitle = new(0, 15);
        public UpdateField<byte> ArenaFaction = new(0, 16);
        public UpdateField<byte> PvpRank = new(0, 17);
        public UpdateField<int> Field_88 = new(0, 18);
        public UpdateField<uint> DuelTeam = new(0, 19);
        public UpdateField<int> GuildTimeStamp = new(0, 20);
        public UpdateField<int> PlayerTitle = new(0, 21);
        public UpdateField<int> FakeInebriation = new(0, 22);
        public UpdateField<uint> VirtualPlayerRealm = new(0, 23);
        public UpdateField<uint> CurrentSpecID = new(0, 24);
        public UpdateField<int> TaxiMountAnimKitID = new(0, 25);
        public UpdateField<byte> CurrentBattlePetBreedQuality = new(0, 26);
        public UpdateField<int> HonorLevel = new(0, 27);
        public UpdateField<long> LogoutTime = new(0, 28);
        public UpdateField<int> CurrentBattlePetSpeciesID = new(0, 29);
        public UpdateField<ObjectGuid> BnetAccount = new(0, 30);                    // For telemetry
        public UpdateField<DungeonScoreSummary> DungeonScore = new(0, 31);
        public UpdateFieldArray<byte> PartyType = new(2, 32, 33);
        public UpdateFieldArray<QuestLog> QuestLog = new(25, 35, 36);
        public UpdateFieldArray<VisibleItem> VisibleItems = new(19, 61, 62);
        public UpdateFieldArray<float> AvgItemLevel = new(6, 81, 82);
        public UpdateFieldArray<uint> Field_3120 = new(19, 88, 89);
        static int changeMaskLength = 108;

        public PlayerData() : base(0, TypeId.Player, changeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Player owner, Player receiver)
        {
            data.WritePackedGuid(DuelArbiter);
            data.WritePackedGuid(WowAccount);
            data.WritePackedGuid(LootTargetGUID);
            data.WriteUInt32((uint)PlayerFlags.GetValue());
            data.WriteUInt32((uint)PlayerFlagsEx.GetValue());
            data.WriteUInt32(GuildRankID);
            data.WriteUInt32(GuildDeleteDate);
            data.WriteInt32(GuildLevel);
            data.WriteInt32(Customizations.Size());
            for (int i = 0; i < 2; ++i)
            {
                data.WriteUInt8(PartyType[i]);
            }
            data.WriteUInt8(NumBankSlots);
            data.WriteUInt8(NativeSex);
            data.WriteUInt8(Inebriation);
            data.WriteUInt8(PvpTitle);
            data.WriteUInt8(ArenaFaction);
            data.WriteUInt8(PvpRank);
            data.WriteInt32(Field_88);
            data.WriteUInt32(DuelTeam);
            data.WriteInt32(GuildTimeStamp);
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.PartyMember))
            {
                for (int i = 0; i < 25; ++i)
                    QuestLog[i].WriteCreate(data, owner, receiver);
            }
            for (int i = 0; i < 19; ++i)
            {
                VisibleItems[i].WriteCreate(data, owner, receiver);
            }
            data.WriteInt32(PlayerTitle);
            data.WriteInt32(FakeInebriation);
            data.WriteUInt32(VirtualPlayerRealm);
            data.WriteUInt32(CurrentSpecID);
            data.WriteInt32(TaxiMountAnimKitID);
            for (int i = 0; i < 6; ++i)
            {
                data.WriteFloat(AvgItemLevel[i]);
            }
            data.WriteUInt8(CurrentBattlePetBreedQuality);
            data.WriteInt32(HonorLevel);
            data.WriteInt64(LogoutTime);
            data.WriteInt32(ArenaCooldowns.Size());
            data.WriteInt32(CurrentBattlePetSpeciesID);
            data.WritePackedGuid(BnetAccount);
            data.WriteInt32(VisualItemReplacements.Size());
            for (int i = 0; i < 19; ++i)
            {
                data.WriteUInt32(Field_3120[i]);
            }
            for (int i = 0; i < Customizations.Size(); ++i)
            {
                Customizations[i].WriteCreate(data, owner, receiver);
            }
            for (int i = 0; i < ArenaCooldowns.Size(); ++i)
            {
                ArenaCooldowns[i].WriteCreate(data, owner, receiver);
            }
            for (int i = 0; i < VisualItemReplacements.Size(); ++i)
            {
                data.WriteInt32(VisualItemReplacements[i]);
            }            
            DungeonScore._value.Write(data);
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Player owner, Player receiver)
        {
            UpdateMask allowedMaskForTarget = new(changeMaskLength, [0xFFFFFFFFu, 0xE0000007u, 0xFFFFFFFFu, 0x00000FFFu]);
            AppendAllowedFieldsMaskForFlag(allowedMaskForTarget, fieldVisibilityFlags);
            WriteUpdate(data, _changesMask & allowedMaskForTarget, false, owner, receiver);
        }

        public void AppendAllowedFieldsMaskForFlag(UpdateMask allowedMaskForTarget, UpdateFieldFlag fieldVisibilityFlags)
        {
            if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.PartyMember))
                allowedMaskForTarget.OR(new UpdateMask(changeMaskLength, [0x00000000u, 0x1FFFFFF8u, 0x00000000u, 0x00000000u]));
        }

        public void FilterDisallowedFieldsMaskForFlag(UpdateMask changesMask, UpdateFieldFlag fieldVisibilityFlags)
        {
            UpdateMask allowedMaskForTarget = new(changeMaskLength, [0xFFFFFFFFu, 0xE0000007u, 0xFFFFFFFFu, 0x00000FFFu]);
            AppendAllowedFieldsMaskForFlag(allowedMaskForTarget, fieldVisibilityFlags);
            changesMask.AND(allowedMaskForTarget);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, Player owner, Player receiver)
        {
            data.WriteBits(changesMask.GetBlocksMask(0), 4);
            for (uint i = 0; i < 4; ++i)
                if (changesMask.GetBlock(i) != 0)
                    data.WriteBits(changesMask.GetBlock(i), 32);

            bool noQuestLogChangesMask = data.WriteBit(IsQuestLogChangesMaskSkipped());
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    if (!ignoreNestedChangesMask)
                        Customizations.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Customizations.Size(), data);
                }
                if (changesMask[2])
                {
                    if (!ignoreNestedChangesMask)
                        ArenaCooldowns.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(ArenaCooldowns.Size(), data);
                }
                if (changesMask[3])
                {
                    if (!ignoreNestedChangesMask)
                        VisualItemReplacements.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(VisualItemReplacements.Size(), data);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    for (int i = 0; i < Customizations.Size(); ++i)
                    {
                        if (Customizations.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            Customizations[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[2])
                {
                    for (int i = 0; i < ArenaCooldowns.Size(); ++i)
                    {
                        if (ArenaCooldowns.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            ArenaCooldowns[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[3])
                {
                    for (int i = 0; i < VisualItemReplacements.Size(); ++i)
                    {
                        if (VisualItemReplacements.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(VisualItemReplacements[i]);
                        }
                    }
                }
                if (changesMask[4])
                {
                    data.WritePackedGuid(DuelArbiter);
                }
                if (changesMask[5])
                {
                    data.WritePackedGuid(WowAccount);
                }
                if (changesMask[6])
                {
                    data.WritePackedGuid(LootTargetGUID);
                }                
                if (changesMask[7])
                {
                    data.WriteUInt32((uint)PlayerFlags.GetValue());
                }
                if (changesMask[8])
                {
                    data.WriteUInt32((uint)PlayerFlagsEx.GetValue());
                }
                if (changesMask[9])
                {
                    data.WriteUInt32(GuildRankID);
                }
                if (changesMask[10])
                {
                    data.WriteUInt32(GuildDeleteDate);
                }
                if (changesMask[11])
                {
                    data.WriteInt32(GuildLevel);
                }
                if (changesMask[12])
                {
                    data.WriteUInt8(NumBankSlots);
                }
                if (changesMask[13])
                {
                    data.WriteUInt8(NativeSex);
                }
                if (changesMask[14])
                {
                    data.WriteUInt8(Inebriation);
                }
                if (changesMask[15])
                {
                    data.WriteUInt8(PvpTitle);
                }
                if (changesMask[16])
                {
                    data.WriteUInt8(ArenaFaction);
                }
                if (changesMask[17])
                {
                    data.WriteUInt8(PvpRank);
                }
                if (changesMask[18])
                {
                    data.WriteInt32(Field_88);
                }
                if (changesMask[19])
                {
                    data.WriteUInt32(DuelTeam);
                }
                if (changesMask[20])
                {
                    data.WriteInt32(GuildTimeStamp);
                }
                if (changesMask[21])
                {
                    data.WriteInt32(PlayerTitle);
                }
                if (changesMask[22])
                {
                    data.WriteInt32(FakeInebriation);
                }
                if (changesMask[23])
                {
                    data.WriteUInt32(VirtualPlayerRealm);
                }
                if (changesMask[24])
                {
                    data.WriteUInt32(CurrentSpecID);
                }
                if (changesMask[25])
                {
                    data.WriteInt32(TaxiMountAnimKitID);
                }
                if (changesMask[26])
                {
                    data.WriteUInt8(CurrentBattlePetBreedQuality);
                }
                if (changesMask[27])
                {
                    data.WriteInt32(HonorLevel);
                }
                if (changesMask[28])
                {
                    data.WriteInt64(LogoutTime);
                }
                if (changesMask[29])
                {
                    data.WriteInt32(CurrentBattlePetSpeciesID);
                }
                if (changesMask[30])
                {
                    data.WritePackedGuid(BnetAccount);
                }
                if (changesMask[31])
                {
                    DungeonScore.GetValue().Write(data);
                }
            }
            if (changesMask[32])
            {
                for (int i = 0; i < 2; ++i)
                {
                    if (changesMask[33 + i])
                    {
                        data.WriteUInt8(PartyType[i]);
                    }
                }
            }
            if (changesMask[35])
            {
                for (int i = 0; i < 25; ++i)
                {
                    if (changesMask[36 + i])
                    {
                        if (noQuestLogChangesMask)
                            QuestLog[i].WriteCreate(data, owner, receiver);
                        else
                            QuestLog[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                    }
                }
            }
            if (changesMask[61])
            {
                for (int i = 0; i < 19; ++i)
                {
                    if (changesMask[62 + i])
                    {
                        VisibleItems[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                    }
                }
            }
            if (changesMask[81])
            {
                for (int i = 0; i < 6; ++i)
                {
                    if (changesMask[82 + i])
                    {
                        data.WriteFloat(AvgItemLevel[i]);
                    }
                }
            }
            if (changesMask[88])
            {
                for (int i = 0; i < 19; ++i)
                {
                    if (changesMask[89 + i])
                    {
                        data.WriteUInt32(Field_3120[i]);
                    }
                }
            }
            data.FlushBits();
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Customizations);
            ClearChangesMask(ArenaCooldowns);
            ClearChangesMask(VisualItemReplacements);
            ClearChangesMask(DuelArbiter);
            ClearChangesMask(WowAccount);
            ClearChangesMask(LootTargetGUID);
            ClearChangesMask(PlayerFlags);
            ClearChangesMask(PlayerFlagsEx);

            ClearChangesMask(GuildRankID);
            ClearChangesMask(GuildDeleteDate);
            ClearChangesMask(GuildLevel);
            ClearChangesMask(NumBankSlots);
            ClearChangesMask(NativeSex);
            ClearChangesMask(Inebriation);
            ClearChangesMask(PvpTitle);

            ClearChangesMask(ArenaFaction);
            ClearChangesMask(PvpRank);
            ClearChangesMask(Field_88);
            ClearChangesMask(DuelTeam);
            ClearChangesMask(GuildTimeStamp);
            ClearChangesMask(PlayerTitle);
            ClearChangesMask(FakeInebriation);

            ClearChangesMask(VirtualPlayerRealm);
            ClearChangesMask(CurrentSpecID);
            ClearChangesMask(TaxiMountAnimKitID);
            ClearChangesMask(CurrentBattlePetBreedQuality);
            ClearChangesMask(HonorLevel);
            ClearChangesMask(LogoutTime);
            ClearChangesMask(CurrentBattlePetSpeciesID);

            ClearChangesMask(BnetAccount);
            ClearChangesMask(DungeonScore);
            ClearChangesMask(PartyType);
            ClearChangesMask(QuestLog);
            ClearChangesMask(VisibleItems);
            ClearChangesMask(AvgItemLevel);
            ClearChangesMask(Field_3120);
            _changesMask.ResetAll();
        }

        bool IsQuestLogChangesMaskSkipped() { return false; } // bandwidth savings aren't worth the cpu time
    }

    public class SkillInfo : HasChangesMask
    {
        public UpdateFieldArray<ushort> SkillLineID = new(256, 0, 1);
        public UpdateFieldArray<ushort> SkillStep = new(256, 0, 257);
        public UpdateFieldArray<ushort> SkillRank = new(256, 0, 513);
        public UpdateFieldArray<ushort> SkillStartingRank = new(256, 0, 769);
        public UpdateFieldArray<ushort> SkillMaxRank = new(256, 0, 1025);
        public UpdateFieldArray<short> SkillTempBonus = new(256, 0, 1281);
        public UpdateFieldArray<ushort> SkillPermBonus = new(256, 0, 1537);
        static int changeMaskLength = 1793;

        public SkillInfo() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            for (int i = 0; i < 256; ++i)
            {
                data.WriteUInt16(SkillLineID[i]);
                data.WriteUInt16(SkillStep[i]);
                data.WriteUInt16(SkillRank[i]);
                data.WriteUInt16(SkillStartingRank[i]);
                data.WriteUInt16(SkillMaxRank[i]);
                data.WriteInt16(SkillTempBonus[i]);
                data.WriteUInt16(SkillPermBonus[i]);
            }
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            for (uint i = 0; i < 1; ++i)
                data.WriteUInt32(changesMask.GetBlocksMask(i));
            data.WriteBits(changesMask.GetBlocksMask(1), 25);
            for (uint i = 0; i < 57; ++i)
                if (changesMask.GetBlock(i) != 0)
                    data.WriteBits(changesMask.GetBlock(i), 32);

            data.FlushBits();
            if (changesMask[0])
            {
                for (int i = 0; i < 256; ++i)
                {
                    if (changesMask[1 + i])
                    {
                        data.WriteUInt16(SkillLineID[i]);
                    }
                    if (changesMask[257 + i])
                    {
                        data.WriteUInt16(SkillStep[i]);
                    }
                    if (changesMask[513 + i])
                    {
                        data.WriteUInt16(SkillRank[i]);
                    }
                    if (changesMask[769 + i])
                    {
                        data.WriteUInt16(SkillStartingRank[i]);
                    }
                    if (changesMask[1025 + i])
                    {
                        data.WriteUInt16(SkillMaxRank[i]);
                    }
                    if (changesMask[1281 + i])
                    {
                        data.WriteInt16(SkillTempBonus[i]);
                    }
                    if (changesMask[1537 + i])
                    {
                        data.WriteUInt16(SkillPermBonus[i]);
                    }
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(SkillLineID);
            ClearChangesMask(SkillStep);
            ClearChangesMask(SkillRank);
            ClearChangesMask(SkillStartingRank);
            ClearChangesMask(SkillMaxRank);
            ClearChangesMask(SkillTempBonus);
            ClearChangesMask(SkillPermBonus);
            _changesMask.ResetAll();
        }
    }

    public class RestInfo : HasChangesMask
    {
        public UpdateField<uint> Threshold = new(0, 1);
        public UpdateField<byte> StateID = new(0, 2);
        static int changeMaskLength = 3;

        public RestInfo() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteUInt32(Threshold);
            data.WriteUInt8(StateID);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteUInt32(Threshold);
                }
                if (changesMask[2])
                {
                    data.WriteUInt8(StateID);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Threshold);
            ClearChangesMask(StateID);
            _changesMask.ResetAll();
        }
    }

    public class PVPInfo : HasChangesMask
    {
        public UpdateField<bool> Disqualified = new(0, 1);
        public UpdateField<sbyte> Bracket = new(0, 2);
        public UpdateField<int> PvpRatingID = new(0, 3);
        public UpdateField<uint> WeeklyPlayed = new(0, 4);
        public UpdateField<uint> WeeklyWon = new(0, 5);
        public UpdateField<uint> SeasonPlayed = new(0, 6);
        public UpdateField<uint> SeasonWon = new(0, 7);
        public UpdateField<uint> Rating = new(0, 8);
        public UpdateField<uint> WeeklyBestRating = new(0, 9);
        public UpdateField<uint> SeasonBestRating = new(0, 10);
        public UpdateField<uint> PvpTierID = new(0, 11);
        public UpdateField<uint> WeeklyBestWinPvpTierID = new(0, 12);
        public UpdateField<uint> Field_28 = new(0, 13);
        public UpdateField<uint> Field_2C = new(0, 14);
        public UpdateField<uint> WeeklyRoundsPlayed = new(0, 15);
        public UpdateField<uint> WeeklyRoundsWon = new(0, 16);
        public UpdateField<uint> SeasonRoundsPlayed = new(0, 17);
        public UpdateField<uint> SeasonRoundsWon = new(0, 18);
        static int changeMaskLength = 19;

        public PVPInfo() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt8(Bracket);
            data.WriteInt32(PvpRatingID);
            data.WriteUInt32(WeeklyPlayed);
            data.WriteUInt32(WeeklyWon);
            data.WriteUInt32(SeasonPlayed);
            data.WriteUInt32(SeasonWon);
            data.WriteUInt32(Rating);
            data.WriteUInt32(WeeklyBestRating);
            data.WriteUInt32(SeasonBestRating);
            data.WriteUInt32(PvpTierID);
            data.WriteUInt32(WeeklyBestWinPvpTierID);
            data.WriteUInt32(Field_28);
            data.WriteUInt32(Field_2C);
            data.WriteUInt32(WeeklyRoundsPlayed);
            data.WriteUInt32(WeeklyRoundsWon);
            data.WriteUInt32(SeasonRoundsPlayed);
            data.WriteUInt32(SeasonRoundsWon);
            data.WriteBit(Disqualified);
            data.FlushBits();
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteBit(Disqualified);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[2])
                {
                    data.WriteInt8(Bracket);
                }
                if (changesMask[3])
                {
                    data.WriteInt32(PvpRatingID);
                }
                if (changesMask[4])
                {
                    data.WriteUInt32(WeeklyPlayed);
                }
                if (changesMask[5])
                {
                    data.WriteUInt32(WeeklyWon);
                }
                if (changesMask[6])
                {
                    data.WriteUInt32(SeasonPlayed);
                }
                if (changesMask[7])
                {
                    data.WriteUInt32(SeasonWon);
                }
                if (changesMask[8])
                {
                    data.WriteUInt32(Rating);
                }
                if (changesMask[9])
                {
                    data.WriteUInt32(WeeklyBestRating);
                }
                if (changesMask[10])
                {
                    data.WriteUInt32(SeasonBestRating);
                }
                if (changesMask[11])
                {
                    data.WriteUInt32(PvpTierID);
                }
                if (changesMask[12])
                {
                    data.WriteUInt32(WeeklyBestWinPvpTierID);
                }
                if (changesMask[13])
                {
                    data.WriteUInt32(Field_28);
                }
                if (changesMask[14])
                {
                    data.WriteUInt32(Field_2C);
                }
                if (changesMask[15])
                {
                    data.WriteUInt32(WeeklyRoundsPlayed);
                }
                if (changesMask[16])
                {
                    data.WriteUInt32(WeeklyRoundsWon);
                }
                if (changesMask[17])
                {
                    data.WriteUInt32(SeasonRoundsPlayed);
                }
                if (changesMask[18])
                {
                    data.WriteUInt32(SeasonRoundsWon);
                }
            }
            data.FlushBits();
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Disqualified);
            ClearChangesMask(Bracket);
            ClearChangesMask(PvpRatingID);
            ClearChangesMask(WeeklyPlayed);
            ClearChangesMask(WeeklyWon);
            ClearChangesMask(SeasonPlayed);
            ClearChangesMask(SeasonWon);
            ClearChangesMask(Rating);
            ClearChangesMask(WeeklyBestRating);
            ClearChangesMask(SeasonBestRating);
            ClearChangesMask(PvpTierID);
            ClearChangesMask(WeeklyBestWinPvpTierID);
            ClearChangesMask(Field_28);
            ClearChangesMask(Field_2C);
            ClearChangesMask(WeeklyRoundsPlayed);
            ClearChangesMask(WeeklyRoundsWon);
            ClearChangesMask(SeasonRoundsPlayed);
            ClearChangesMask(SeasonRoundsWon);
            _changesMask.ResetAll();
        }
    }

    public class CharacterRestriction
    {
        public int Field_0;
        public int Field_4;
        public int Field_8;
        public uint Type;

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(Field_0);
            data.WriteInt32(Field_4);
            data.WriteInt32(Field_8);
            data.WriteBits(Type, 5);
            data.FlushBits();
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            data.WriteInt32(Field_0);
            data.WriteInt32(Field_4);
            data.WriteInt32(Field_8);
            data.WriteBits(Type, 5);
            data.FlushBits();
        }
    }

    public class SpellPctModByLabel
    {
        public int ModIndex;
        public float ModifierValue;
        public int LabelID;

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(ModIndex);
            data.WriteFloat(ModifierValue);
            data.WriteInt32(LabelID);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            data.WriteInt32(ModIndex);
            data.WriteFloat(ModifierValue);
            data.WriteInt32(LabelID);
        }

    }

    public class SpellFlatModByLabel
    {
        public int ModIndex;
        public int ModifierValue;
        public int LabelID;

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(ModIndex);
            data.WriteInt32(ModifierValue);
            data.WriteInt32(LabelID);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            data.WriteInt32(ModIndex);
            data.WriteInt32(ModifierValue);
            data.WriteInt32(LabelID);
        }
    }

    public class CompletedProject : HasChangesMask
    {
        public UpdateField<uint> ProjectID = new(0, 1);
        public UpdateField<long> FirstCompleted = new(0, 2);
        public UpdateField<uint> CompletionCount = new(0, 3);
        static int changeMaskLength = 4;

        public CompletedProject() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteUInt32(ProjectID);
            data.WriteInt64(FirstCompleted);
            data.WriteUInt32(CompletionCount);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteUInt32(ProjectID);
                }
                if (changesMask[2])
                {
                    data.WriteInt64(FirstCompleted);
                }
                if (changesMask[3])
                {
                    data.WriteUInt32(CompletionCount);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(ProjectID);
            ClearChangesMask(FirstCompleted);
            ClearChangesMask(CompletionCount);
            _changesMask.ResetAll();
        }
    }

    public class ResearchHistory : HasChangesMask
    {
        public DynamicUpdateField<CompletedProject> CompletedProjects = new(0, 1);
        static int changeMaskLength = 2;

        public ResearchHistory() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(CompletedProjects.Size());
            for (int i = 0; i < CompletedProjects.Size(); ++i)
            {
                CompletedProjects[i].WriteCreate(data, owner, receiver);
            }
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    if (!ignoreChangesMask)
                        CompletedProjects.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(CompletedProjects.Size(), data);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    for (int i = 0; i < CompletedProjects.Size(); ++i)
                    {
                        if (CompletedProjects.HasChanged(i) || ignoreChangesMask)
                        {
                            CompletedProjects[i].WriteUpdate(data, ignoreChangesMask, owner, receiver);
                        }
                    }
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(CompletedProjects);
            _changesMask.ResetAll();
        }
    }

    public class TraitEntry : IEquatable<TraitEntry>
    {
        public int TraitNodeID;
        public int TraitNodeEntryID;
        public int Rank;
        public int GrantedRanks;

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(TraitNodeID);
            data.WriteInt32(TraitNodeEntryID);
            data.WriteInt32(Rank);
            data.WriteInt32(GrantedRanks);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            data.WriteInt32(TraitNodeID);
            data.WriteInt32(TraitNodeEntryID);
            data.WriteInt32(Rank);
            data.WriteInt32(GrantedRanks);
        }

        public bool Equals(TraitEntry right)
        {
            return TraitNodeID == right.TraitNodeID
                && TraitNodeEntryID == right.TraitNodeEntryID
                && Rank == right.Rank
                && GrantedRanks == right.GrantedRanks;
        }
    }

    public class TraitConfig : HasChangesMask
    {
        public DynamicUpdateField<TraitEntry> Entries = new(0, 1);
        public UpdateField<int> ID = new(0, 2);
        public UpdateFieldString Name = new(0, 3);
        public UpdateField<int> Type = new(4, 5);
        public UpdateField<int> SkillLineID = new(4, 6);
        public UpdateField<int> ChrSpecializationID = new(4, 7);
        public UpdateField<int> CombatConfigFlags = new(8, 9);
        public UpdateField<int> LocalIdentifier = new(8, 10);
        public UpdateField<int> TraitSystemID = new(8, 11);
        static int changeMaskLength = 12;

        public TraitConfig() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(ID);
            data.WriteInt32(Type);
            data.WriteInt32(Entries.Size());
            if (Type == 2)
            {
                data.WriteInt32(SkillLineID);
            }
            if (Type == 1)
            {
                data.WriteInt32(ChrSpecializationID);
                data.WriteInt32(CombatConfigFlags);
                data.WriteInt32(LocalIdentifier);
            }
            if (Type == 3)
            {
                data.WriteInt32(TraitSystemID);
            }
            for (int i = 0; i < Entries.Size(); ++i)
            {
                Entries[i].WriteCreate(data, owner, receiver);
            }
            data.WriteBits(Name.GetValue().GetByteCount(), 9);
            data.WriteString(Name);
            data.FlushBits();
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    if (!ignoreChangesMask)
                        Entries.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Entries.Size(), data);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    for (int i = 0; i < Entries.Size(); ++i)
                    {
                        if (Entries.HasChanged(i) || ignoreChangesMask)
                        {
                            Entries[i].WriteUpdate(data, ignoreChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[2])
                {
                    data.WriteInt32(ID);
                }
            }
            if (changesMask[4])
            {
                if (changesMask[5])
                {
                    data.WriteInt32(Type);
                }
                if (changesMask[6])
                {
                    if (Type == 2)
                    {
                        data.WriteInt32(SkillLineID);
                    }
                }
                if (changesMask[7])
                {
                    if (Type == 1)
                    {
                        data.WriteInt32(ChrSpecializationID);
                    }
                }
            }
            if (changesMask[8])
            {
                if (changesMask[9])
                {
                    if (Type == 1)
                    {
                        data.WriteInt32(CombatConfigFlags);
                    }
                }
                if (changesMask[10])
                {
                    if (Type == 1)
                    {
                        data.WriteInt32(LocalIdentifier);
                    }
                }
                if (changesMask[11])
                {
                    if (Type == 3)
                    {
                        data.WriteInt32(TraitSystemID);
                    }
                }
            }
            if (changesMask[0])
            {
                if (changesMask[3])
                {
                    data.WriteBits(Name.GetValue().GetByteCount(), 9);
                    data.WriteString(Name);
                }
            }
            data.FlushBits();
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Entries);
            ClearChangesMask(ID);
            ClearChangesMask(Name);
            ClearChangesMask(Type);
            ClearChangesMask(SkillLineID);
            ClearChangesMask(ChrSpecializationID);
            ClearChangesMask(CombatConfigFlags);
            ClearChangesMask(LocalIdentifier);
            ClearChangesMask(TraitSystemID);
            _changesMask.ResetAll();
        }
    }

    public struct CategoryCooldownMod
    {
        public int SpellCategoryID;
        public int ModCooldown;

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(SpellCategoryID);
            data.WriteInt32(ModCooldown);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            data.WriteInt32(SpellCategoryID);
            data.WriteInt32(ModCooldown);
        }
    }

    public struct WeeklySpellUse
    {
        public int SpellCategoryID;
        public byte Uses;

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(SpellCategoryID);
            data.WriteUInt8(Uses);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            data.WriteInt32(SpellCategoryID);
            data.WriteUInt8(Uses);
        }
    }

    public class StablePetInfo : HasChangesMask
    {
        public UpdateField<uint> PetSlot = new(0, 1);
        public UpdateField<uint> PetNumber = new(0, 2);
        public UpdateField<uint> CreatureID = new(0, 3);
        public UpdateField<uint> DisplayID = new(0, 4);
        public UpdateField<uint> ExperienceLevel = new(0, 5);
        public UpdateFieldString Name = new(0, 6);
        public UpdateField<byte> PetFlags = new(0, 7);
        static int changeMaskLength = 8;

        public StablePetInfo() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteUInt32(PetSlot);
            data.WriteUInt32(PetNumber);
            data.WriteUInt32(CreatureID);
            data.WriteUInt32(DisplayID);
            data.WriteUInt32(ExperienceLevel);
            data.WriteUInt8(PetFlags);
            data.WriteBits(Name.GetValue().GetByteCount(), 8);
            data.WriteString(Name);
            data.FlushBits();
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteUInt32(PetSlot);
                }
                if (changesMask[2])
                {
                    data.WriteUInt32(PetNumber);
                }
                if (changesMask[3])
                {
                    data.WriteUInt32(CreatureID);
                }
                if (changesMask[4])
                {
                    data.WriteUInt32(DisplayID);
                }
                if (changesMask[5])
                {
                    data.WriteUInt32(ExperienceLevel);
                }
                if (changesMask[7])
                {
                    data.WriteUInt8(PetFlags);
                }
                if (changesMask[6])
                {
                    data.WriteBits(Name.GetValue().GetByteCount(), 8);
                    data.WriteString(Name);
                }
            }
            data.FlushBits();
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(PetSlot);
            ClearChangesMask(PetNumber);
            ClearChangesMask(CreatureID);
            ClearChangesMask(DisplayID);
            ClearChangesMask(ExperienceLevel);
            ClearChangesMask(Name);
            ClearChangesMask(PetFlags);
            _changesMask.ResetAll();
        }
    }

    public class StableInfo : HasChangesMask
    {
        public DynamicUpdateField<StablePetInfo> Pets = new(0, 1);
        public UpdateField<ObjectGuid> StableMaster = new(0, 2);
        static int changeMaskLength = 3;

        public StableInfo() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt32(Pets.Size());
            data.WritePackedGuid(StableMaster);
            for (int i = 0; i < Pets.Size(); ++i)
            {
                Pets[i].WriteCreate(data, owner, receiver);
            }
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    if (!ignoreChangesMask)
                        Pets.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Pets.Size(), data);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    for (int i = 0; i < Pets.Size(); ++i)
                    {
                        if (Pets.HasChanged(i) || ignoreChangesMask)
                        {
                            Pets[i].WriteUpdate(data, ignoreChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[2])
                {
                    data.WritePackedGuid(StableMaster);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Pets);
            ClearChangesMask(StableMaster);
            _changesMask.ResetAll();
        }
    }    

    public struct Research
    {
        public short ResearchProjectID;

        public void WriteCreate(WorldPacket data, Player owner, Player receiver)
        {
            data.WriteInt16(ResearchProjectID);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
        {
            data.WriteInt16(ResearchProjectID);
        }
    }

    public class ActivePlayerData : HasChangesMask
    {
        
        public UpdateField<bool> SortBagsRightToLeft = new(0, 1);
        public UpdateField<bool> InsertItemsLeftToRight = new(0, 2);
        public UpdateFieldArray<DynamicUpdateField<ushort>> ResearchSites = new(1, 20, 21);
        public UpdateFieldArray<DynamicUpdateField<uint>> ResearchSiteProgress = new(1, 22, 23);
        public UpdateFieldArray<DynamicUpdateField<Research>> Research = new(1, 24, 25);
        public DynamicUpdateField<ulong> KnownTitles = new(0, 3);
        public DynamicUpdateField<int> DailyQuestsCompleted = new(0, 4);
        public DynamicUpdateField<int> AvailableQuestLineXQuestIDs = new(0, 5);
        public DynamicUpdateField<int> Field_1000 = new(0, 6);
        public DynamicUpdateField<int> Heirlooms = new(0, 7);
        public DynamicUpdateField<uint> HeirloomFlags = new(0, 8);
        public DynamicUpdateField<int> Toys = new(0, 9);
        public DynamicUpdateField<uint> Transmog = new(0, 10);
        public DynamicUpdateField<int> ConditionalTransmog = new(0, 11);
        public DynamicUpdateField<int> SelfResSpells = new(0, 12);
        public DynamicUpdateField<SpellPctModByLabel> SpellPctModByLabel = new(0, 14);
        public DynamicUpdateField<SpellFlatModByLabel> SpellFlatModByLabel = new(0, 15);
        public DynamicUpdateField<QuestLog> TaskQuests = new(0, 16);
        public DynamicUpdateField<CategoryCooldownMod> CategoryCooldownMods = new(0, 18);
        public DynamicUpdateField<WeeklySpellUse> WeeklySpellUses = new(0, 19);
        public DynamicUpdateField<CharacterRestriction> CharacterRestrictions = new(0, 13);
        public DynamicUpdateField<TraitConfig> TraitConfigs = new(0, 17);
        public UpdateField<ObjectGuid> FarsightObject = new(0, 26);
        public UpdateField<ObjectGuid> SummonedBattlePetGUID = new(0, 27);
        public UpdateField<ulong> Coinage = new(0, 28);
        public UpdateField<int> XP = new(0, 29);
        public UpdateField<int> NextLevelXP = new(0, 30);
        public UpdateField<int> TrialXP = new(0, 31);
        public UpdateField<SkillInfo> Skill = new(0, 32);
        public UpdateField<int> CharacterPoints = new(0, 33);
        public UpdateField<int> MaxTalentTiers = new(0, 34);
        public UpdateField<uint> TrackCreatureMask = new(0, 35);
        public UpdateField<float> MainhandExpertise = new(0, 36);
        public UpdateField<float> OffhandExpertise = new(0, 37);
        public UpdateField<float> RangedExpertise = new(38, 39);
        public UpdateField<float> CombatRatingExpertise = new(38, 40);
        public UpdateField<float> BlockPercentage = new(38, 41);
        public UpdateField<float> DodgePercentage = new(38, 42);
        public UpdateField<float> DodgePercentageFromAttribute = new(38, 43);
        public UpdateField<float> ParryPercentage = new(38, 44);
        public UpdateField<float> ParryPercentageFromAttribute = new(38, 45);
        public UpdateField<float> CritPercentage = new(38, 46);
        public UpdateField<float> RangedCritPercentage = new(38, 47);
        public UpdateField<float> OffhandCritPercentage = new(38, 48);
        public UpdateField<int> ShieldBlock = new(38, 49);
        public UpdateField<float> ShieldBlockCritPercentage = new(38, 50);
        public UpdateField<float> Mastery = new(38, 51);
        public UpdateField<float> Speed = new(38, 52);
        public UpdateField<float> Avoidance = new(38, 53);
        public UpdateField<float> Sturdiness = new(38, 54);
        public UpdateField<int> Versatility = new(38, 55);
        public UpdateField<float> VersatilityBonus = new(38, 56);
        public UpdateField<float> PvpPowerDamage = new(38, 57);
        public UpdateField<float> PvpPowerHealing = new(38, 58);
        public UpdateField<int> ModHealingDonePos = new(38, 59);
        public UpdateField<float> ModHealingPercent = new(38, 60);
        public UpdateField<float> ModHealingDonePercent = new(38, 61);
        public UpdateField<float> ModPeriodicHealingDonePercent = new(38, 62);
        public UpdateField<float> ModSpellPowerPercent = new(38, 63);
        public UpdateField<float> ModResiliencePercent = new(38, 64);
        public UpdateField<float> OverrideSpellPowerByAPPercent = new(38, 65);
        public UpdateField<float> OverrideAPBySpellPowerPercent = new(38, 66);
        public UpdateField<int> ModTargetResistance = new(38, 67);
        public UpdateField<int> ModTargetPhysicalResistance = new(38, 68);
        public UpdateField<uint> LocalFlags = new(38, 69);
        public UpdateField<byte> GrantableLevels = new(70, 71);
        public UpdateField<byte> MultiActionBars = new(70, 72);
        public UpdateField<byte> LifetimeMaxRank = new(70, 73);
        public UpdateField<byte> NumRespecs = new(70, 74);
        public UpdateField<int> AmmoID = new(70, 75);
        public UpdateField<uint> PvpMedals = new(70, 76);
        public UpdateField<ushort> TodayHonorableKills = new(70, 77);
        public UpdateField<ushort> TodayDishonorableKills = new(70, 78);
        public UpdateField<ushort> YesterdayHonorableKills = new(70, 79);
        public UpdateField<ushort> YesterdayDishonorableKills = new(70, 80);
        public UpdateField<ushort> LastWeekHonorableKills = new(70, 81);
        public UpdateField<ushort> LastWeekDishonorableKills = new(70, 82);
        public UpdateField<ushort> ThisWeekHonorableKills = new(70, 83);
        public UpdateField<ushort> ThisWeekDishonorableKills = new(70, 84);
        public UpdateField<uint> ThisWeekContribution = new(70, 85);
        public UpdateField<uint> LifetimeHonorableKills = new(70, 86);
        public UpdateField<uint> LifetimeDishonorableKills = new(70, 87);
        public UpdateField<uint> Field_F24 = new(70, 88);
        public UpdateField<uint> YesterdayContribution = new(70, 89);
        public UpdateField<uint> LastWeekContribution = new(70, 90);
        public UpdateField<uint> LastWeekRank = new(70, 91);
        public UpdateField<int> WatchedFactionIndex = new(70, 92);
        public UpdateField<int> MaxLevel = new(70, 93);
        public UpdateField<int> ScalingPlayerLevelDelta = new(70, 94);
        public UpdateField<int> MaxCreatureScalingLevel = new(70, 95);
        public UpdateField<int> PetSpellPower = new(70, 96);
        public UpdateField<float> UiHitModifier = new(70, 97);
        public UpdateField<float> UiSpellHitModifier = new(70, 98);
        public UpdateField<int> HomeRealmTimeOffset = new(70, 99);
        public UpdateField<float> ModPetHaste = new(70, 100);
        public UpdateField<byte> LocalRegenFlags = new(70, 101);
        public UpdateField<byte> AuraVision = new(102, 103);
        public UpdateField<byte> NumBackpackSlots = new(102, 104);
        public UpdateField<int> OverrideSpellsID = new(102, 105);
        public UpdateField<int> LfgBonusFactionID = new(102, 106);
        public UpdateField<ushort> LootSpecID = new(102, 107);
        public UpdateField<uint> OverrideZonePVPType = new(102, 108);
        public UpdateField<int> Honor = new(102, 109);
        public UpdateField<int> HonorNextLevel = new(102, 110);
        public UpdateField<int> Field_F74 = new(102, 111);
        public UpdateField<int> PvpTierMaxFromWins = new(102, 112);
        public UpdateField<int> PvpLastWeeksTierMaxFromWins = new(102, 113);
        public UpdateField<byte> PvpRankProgress = new(102, 114);
        public UpdateField<int> PerksProgramCurrency = new(102, 115);
        public UpdateField<ResearchHistory> ResearchHistory = new(102, 116);
        public UpdateField<PerksVendorItem> FrozenPerksVendorItem = new(102, 117);
        public UpdateField<int> TransportServerTime = new(102, 118);
        public UpdateField<uint> ActiveCombatTraitConfigID = new(102, 119);
        public UpdateField<byte> GlyphsEnabled = new(102, 120);
        public UpdateField<byte> LfgRoles = new(102, 121);
        public OptionalUpdateField<StableInfo> PetStable = new(102, 122);
        public UpdateField<byte> NumStableSlots = new(102, 123);
        public UpdateFieldArray<ObjectGuid> InvSlots = new(141, 124, 125);
        public UpdateFieldArray<uint> TrackResourceMask = new(2, 266, 267);
        public UpdateFieldArray<float> SpellCritPercentage = new(7, 269, 270);
        public UpdateFieldArray<int> ModDamageDonePos = new(7, 269, 277);
        public UpdateFieldArray<int> ModDamageDoneNeg = new(7, 269, 284);
        public UpdateFieldArray<float> ModDamageDonePercent = new(7, 269, 291);
        public UpdateFieldArray<ulong> ExploredZones = new(240, 298, 299);
        public UpdateFieldArray<RestInfo> RestInfo = new(2, 539, 540);
        public UpdateFieldArray<float> WeaponDmgMultipliers = new(3, 542, 543);
        public UpdateFieldArray<float> WeaponAtkSpeedMultipliers = new(3, 542, 546);
        public UpdateFieldArray<uint> BuybackPrice = new(12, 549, 550);
        public UpdateFieldArray<long> BuybackTimestamp = new(12, 549, 562);
        public UpdateFieldArray<int> CombatRatings = new(32, 574, 575);
        public UpdateFieldArray<PVPInfo> PvpInfo = new(7, 607, 608);
        public UpdateFieldArray<uint> NoReagentCostMask = new(4, 615, 616);
        public UpdateFieldArray<int> ProfessionSkillLine = new(2, 620, 621);
        public UpdateFieldArray<uint> BagSlotFlags = new(4, 623, 624);
        public UpdateFieldArray<uint> BankBagSlotFlags = new(7, 628, 629);
        public UpdateFieldArray<ulong> QuestCompleted = new(875, 636, 637);
        public UpdateFieldArray<uint> GlyphSlots = new(6, 1512, 1513);
        public UpdateFieldArray<uint> Glyphs = new(6, 1512, 1519);
        static int changeMaskLength = 1525;

        public static int ExploredZonesSize;
        public static int ExploredZonesBits;
        public static int QuestCompletedBitsSize;
        public static int QuestCompletedBitsPerBlock;

        public ActivePlayerData() : base(0, TypeId.ActivePlayer, changeMaskLength)
        {
            ExploredZonesSize = ExploredZones.GetSize();
            ExploredZonesBits = sizeof(ulong) * 8;

            QuestCompletedBitsSize = QuestCompleted.GetSize();
            QuestCompletedBitsPerBlock = sizeof(ulong) * 8;
        }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Player owner, Player receiver)
        {
            for (int i = 0; i < 141; ++i)
            {
                data.WritePackedGuid(InvSlots[i]);
            }
            data.WritePackedGuid(FarsightObject);
            data.WritePackedGuid(SummonedBattlePetGUID);
            data.WriteUInt32((uint)KnownTitles.Size());
            data.WriteUInt64(Coinage);
            data.WriteInt32(XP);
            data.WriteInt32(NextLevelXP);
            data.WriteInt32(TrialXP);
            Skill.GetValue().WriteCreate(data, owner, receiver);
            data.WriteInt32(CharacterPoints);
            data.WriteInt32(MaxTalentTiers);
            data.WriteUInt32(TrackCreatureMask);
            for (int i = 0; i < 2; ++i)
            {
                data.WriteUInt32(TrackResourceMask[i]);
            }
            data.WriteFloat(MainhandExpertise);
            data.WriteFloat(OffhandExpertise);
            data.WriteFloat(RangedExpertise);
            data.WriteFloat(CombatRatingExpertise);
            data.WriteFloat(BlockPercentage);
            data.WriteFloat(DodgePercentage);
            data.WriteFloat(DodgePercentageFromAttribute);
            data.WriteFloat(ParryPercentage);
            data.WriteFloat(ParryPercentageFromAttribute);
            data.WriteFloat(CritPercentage);
            data.WriteFloat(RangedCritPercentage);
            data.WriteFloat(OffhandCritPercentage);
            for (int i = 0; i < 7; ++i)
            {
                data.WriteFloat(SpellCritPercentage[i]);
                data.WriteInt32(ModDamageDonePos[i]);
                data.WriteInt32(ModDamageDoneNeg[i]);
                data.WriteFloat(ModDamageDonePercent[i]);
            }
            data.WriteInt32(ShieldBlock);
            data.WriteFloat(ShieldBlockCritPercentage);
            data.WriteFloat(Mastery);
            data.WriteFloat(Speed);
            data.WriteFloat(Avoidance);
            data.WriteFloat(Sturdiness);
            data.WriteInt32(Versatility);
            data.WriteFloat(VersatilityBonus);
            data.WriteFloat(PvpPowerDamage);
            data.WriteFloat(PvpPowerHealing);
            for (int i = 0; i < 240; ++i)
            {
                data.WriteUInt64(ExploredZones[i]);
            }
            for (int i = 0; i < 2; ++i)
            {
                RestInfo[i].WriteCreate(data, owner, receiver);
            }
            data.WriteInt32(ModHealingDonePos);
            data.WriteFloat(ModHealingPercent);
            data.WriteFloat(ModHealingDonePercent);
            data.WriteFloat(ModPeriodicHealingDonePercent);
            for (int i = 0; i < 3; ++i)
            {
                data.WriteFloat(WeaponDmgMultipliers[i]);
                data.WriteFloat(WeaponAtkSpeedMultipliers[i]);
            }
            data.WriteFloat(ModSpellPowerPercent);
            data.WriteFloat(ModResiliencePercent);
            data.WriteFloat(OverrideSpellPowerByAPPercent);
            data.WriteFloat(OverrideAPBySpellPowerPercent);
            data.WriteInt32(ModTargetResistance);
            data.WriteInt32(ModTargetPhysicalResistance);
            data.WriteUInt32(LocalFlags);
            data.WriteUInt8(GrantableLevels);
            data.WriteUInt8(MultiActionBars);
            data.WriteUInt8(LifetimeMaxRank);
            data.WriteUInt8(NumRespecs);
            data.WriteInt32(AmmoID);
            data.WriteUInt32(PvpMedals);
            for (int i = 0; i < 12; ++i)
            {
                data.WriteUInt32(BuybackPrice[i]);
                data.WriteInt64(BuybackTimestamp[i]);
            }
            data.WriteUInt16(TodayHonorableKills);
            data.WriteUInt16(TodayDishonorableKills);
            data.WriteUInt16(YesterdayHonorableKills);
            data.WriteUInt16(YesterdayDishonorableKills);
            data.WriteUInt16(LastWeekHonorableKills);
            data.WriteUInt16(LastWeekDishonorableKills);
            data.WriteUInt16(ThisWeekHonorableKills);
            data.WriteUInt16(ThisWeekDishonorableKills);
            data.WriteUInt32(ThisWeekContribution);
            data.WriteUInt32(LifetimeHonorableKills);
            data.WriteUInt32(LifetimeDishonorableKills);
            data.WriteUInt32(Field_F24);
            data.WriteUInt32(YesterdayContribution);
            data.WriteUInt32(LastWeekContribution);
            data.WriteUInt32(LastWeekRank);
            data.WriteInt32(WatchedFactionIndex);
            for (int i = 0; i < 32; ++i)
            {
                data.WriteInt32(CombatRatings[i]);
            }
            data.WriteInt32(MaxLevel);
            data.WriteInt32(ScalingPlayerLevelDelta);
            data.WriteInt32(MaxCreatureScalingLevel);
            for (int i = 0; i < 4; ++i)
            {
                data.WriteUInt32(NoReagentCostMask[i]);
            }
            data.WriteInt32(PetSpellPower);
            for (int i = 0; i < 2; ++i)
            {
                data.WriteInt32(ProfessionSkillLine[i]);
            }
            data.WriteFloat(UiHitModifier);
            data.WriteFloat(UiSpellHitModifier);
            data.WriteInt32(HomeRealmTimeOffset);
            data.WriteFloat(ModPetHaste);
            data.WriteUInt8(LocalRegenFlags);
            data.WriteUInt8(AuraVision);
            data.WriteUInt8(NumBackpackSlots);
            data.WriteInt32(OverrideSpellsID);
            data.WriteInt32(LfgBonusFactionID);
            data.WriteUInt16(LootSpecID);
            data.WriteUInt32(OverrideZonePVPType);
            for (int i = 0; i < 4; ++i)
            {
                data.WriteUInt32(BagSlotFlags[i]);
            }
            for (int i = 0; i < 7; ++i)
            {
                data.WriteUInt32(BankBagSlotFlags[i]);
            }
            for (int i = 0; i < 875; ++i)
            {
                data.WriteUInt64(QuestCompleted[i]);
            }
            data.WriteInt32(Honor);
            data.WriteInt32(HonorNextLevel);
            data.WriteInt32(Field_F74);
            data.WriteInt32(PvpTierMaxFromWins);
            data.WriteInt32(PvpLastWeeksTierMaxFromWins);
            data.WriteUInt8(PvpRankProgress);
            data.WriteInt32(PerksProgramCurrency);
            for (int i = 0; i < 1; ++i)
            {
                data.WriteInt32(ResearchSites[i].Size());
                data.WriteInt32(ResearchSiteProgress[i].Size());
                data.WriteInt32(Research[i].Size());
                for (int j = 0; j < ResearchSites[i].Size(); ++j)
                {
                    data.WriteUInt16(ResearchSites[i][j]);
                }
                for (int j = 0; j < ResearchSiteProgress[i].Size(); ++j)
                {
                    data.WriteUInt32(ResearchSiteProgress[i][j]);
                }
                for (int j = 0; j < Research[i].Size(); ++j)
                {
                    Research[i][j].WriteCreate(data, owner, receiver);
                }
            }
            data.WriteInt32(DailyQuestsCompleted.Size());
            data.WriteInt32(AvailableQuestLineXQuestIDs.Size());
            data.WriteInt32(Field_1000.Size());
            data.WriteInt32(Heirlooms.Size());
            data.WriteInt32(HeirloomFlags.Size());
            data.WriteInt32(Toys.Size());
            data.WriteInt32(Transmog.Size());
            data.WriteInt32(ConditionalTransmog.Size());
            data.WriteInt32(SelfResSpells.Size());
            data.WriteInt32(CharacterRestrictions.Size());
            data.WriteInt32(SpellPctModByLabel.Size());
            data.WriteInt32(SpellFlatModByLabel.Size());
            data.WriteInt32(TaskQuests.Size());
            data.WriteInt32(TransportServerTime);
            data.WriteInt32(TraitConfigs.Size());
            data.WriteUInt32(ActiveCombatTraitConfigID);            
            for (int i = 0; i < 6; ++i)
            {
                data.WriteUInt32(GlyphSlots[i]);
                data.WriteUInt32(Glyphs[i]);
            }
            data.WriteUInt8(GlyphsEnabled);
            data.WriteUInt8(LfgRoles);
            data.WriteInt32(CategoryCooldownMods.Size());
            data.WriteInt32(WeeklySpellUses.Size());
            data.WriteUInt8(NumStableSlots);
            for (int i = 0; i < KnownTitles.Size(); ++i)
            {
                data.WriteUInt64(KnownTitles[i]);
            }
            for (int i = 0; i < DailyQuestsCompleted.Size(); ++i)
            {
                data.WriteInt32(DailyQuestsCompleted[i]);
            }
            for (int i = 0; i < AvailableQuestLineXQuestIDs.Size(); ++i)
            {
                data.WriteInt32(AvailableQuestLineXQuestIDs[i]);
            }
            for (int i = 0; i < Field_1000.Size(); ++i)
            {
                data.WriteInt32(Field_1000[i]);
            }
            for (int i = 0; i < Heirlooms.Size(); ++i)
            {
                data.WriteInt32(Heirlooms[i]);
            }
            for (int i = 0; i < HeirloomFlags.Size(); ++i)
            {
                data.WriteUInt32(HeirloomFlags[i]);
            }
            for (int i = 0; i < Toys.Size(); ++i)
            {
                data.WriteInt32(Toys[i]);
            }
            for (int i = 0; i < Transmog.Size(); ++i)
            {
                data.WriteUInt32(Transmog[i]);
            }
            for (int i = 0; i < ConditionalTransmog.Size(); ++i)
            {
                data.WriteInt32(ConditionalTransmog[i]);
            }
            for (int i = 0; i < SelfResSpells.Size(); ++i)
            {
                data.WriteInt32(SelfResSpells[i]);
            }
            for (int i = 0; i < SpellPctModByLabel.Size(); ++i)
            {
                SpellPctModByLabel[i].WriteCreate(data, owner, receiver);
            }
            for (int i = 0; i < SpellFlatModByLabel.Size(); ++i)
            {
                SpellFlatModByLabel[i].WriteCreate(data, owner, receiver);
            }            
            for (int i = 0; i < TaskQuests.Size(); ++i)
            {
                TaskQuests[i].WriteCreate(data, owner, receiver);
            }
            for (int i = 0; i < CategoryCooldownMods.Size(); ++i)
            {
                CategoryCooldownMods[i].WriteCreate(data, owner, receiver);
            }
            for (int i = 0; i < WeeklySpellUses.Size(); ++i)
            {
                WeeklySpellUses[i].WriteCreate(data, owner, receiver);
            }
            for (int i = 0; i < 7; ++i)
            {
                PvpInfo[i].WriteCreate(data, owner, receiver);
            }
            data.FlushBits();
            data.WriteBit(SortBagsRightToLeft);
            data.WriteBit(InsertItemsLeftToRight);
            data.WriteBits(PetStable.HasValue() ? 1 : 0, 1);
            data.FlushBits();
            ResearchHistory.GetValue().WriteCreate(data, owner, receiver);
            FrozenPerksVendorItem.GetValue().Write(data);
            for (int i = 0; i < CharacterRestrictions.Size(); ++i)
            {
                CharacterRestrictions[i].WriteCreate(data, owner, receiver);
            }
            for (int i = 0; i < TraitConfigs.Size(); ++i)
            {
                TraitConfigs[i].WriteCreate(data, owner, receiver);
            }
            if (PetStable.HasValue())
            {
                PetStable.GetValue().WriteCreate(data, owner, receiver);
            }
            data.FlushBits();
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Player owner, Player receiver)
        {
            WriteUpdate(data, _changesMask, false, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, Player owner, Player receiver)
        {
            for (uint i = 0; i < 1; ++i)
                data.WriteUInt32(changesMask.GetBlocksMask(i));
            data.WriteBits(changesMask.GetBlocksMask(1), 16);
            for (uint i = 0; i < 48; ++i)
                if (changesMask.GetBlock(i) != 0)
                    data.WriteBits(changesMask.GetBlock(i), 32);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteBit(SortBagsRightToLeft);
                }
                if (changesMask[2])
                {
                    data.WriteBit(InsertItemsLeftToRight);
                }
                if (changesMask[3])
                {
                    if (!ignoreNestedChangesMask)
                        KnownTitles.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(KnownTitles.Size(), data);
                }
            }
            if (changesMask[20])
            {
                for (int i = 0; i < 1; ++i)
                {
                    if (changesMask[21 + i])
                    {
                        if (!ignoreNestedChangesMask)
                            ResearchSites[i].WriteUpdateMask(data);
                        else
                            WriteCompleteDynamicFieldUpdateMask(ResearchSites[i].Size(), data);
                    }
                }
            }
            if (changesMask[22])
            {
                for (int i = 0; i < 1; ++i)
                {
                    if (changesMask[23 + i])
                    {
                        if (!ignoreNestedChangesMask)
                            ResearchSiteProgress[i].WriteUpdateMask(data);
                        else
                            WriteCompleteDynamicFieldUpdateMask(ResearchSiteProgress[i].Size(), data);
                    }
                }
            }
            if (changesMask[24])
            {
                for (int i = 0; i < 1; ++i)
                {
                    if (changesMask[25 + i])
                    {
                        if (!ignoreNestedChangesMask)
                            Research[i].WriteUpdateMask(data);
                        else
                            WriteCompleteDynamicFieldUpdateMask(Research[i].Size(), data);
                    }
                }
            }
            if (changesMask[20])
            {
                for (int i = 0; i < 1; ++i)
                {
                    if (changesMask[21 + i])
                    {
                        for (int j = 0; j < ResearchSites[i].Size(); ++j)
                        {
                            if (ResearchSites[i].HasChanged(j) || ignoreNestedChangesMask)
                            {
                                data.WriteUInt16(ResearchSites[i][j]);
                            }
                        }
                    }
                }
            }
            if (changesMask[22])
            {
                for (int i = 0; i < 1; ++i)
                {
                    if (changesMask[23 + i])
                    {
                        for (int j = 0; j < ResearchSiteProgress[i].Size(); ++j)
                        {
                            if (ResearchSiteProgress[i].HasChanged(j) || ignoreNestedChangesMask)
                            {
                                data.WriteUInt32(ResearchSiteProgress[i][j]);
                            }
                        }
                    }
                }
            }
            if (changesMask[24])
            {
                for (int i = 0; i < 1; ++i)
                {
                    if (changesMask[25 + i])
                    {
                        for (int j = 0; j < Research[i].Size(); ++j)
                        {
                            if (Research[i].HasChanged(j) || ignoreNestedChangesMask)
                            {
                                Research[i][j].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                            }
                        }
                    }
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[4])
                {
                    if (!ignoreNestedChangesMask)
                        DailyQuestsCompleted.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(DailyQuestsCompleted.Size(), data);
                }
                if (changesMask[5])
                {
                    if (!ignoreNestedChangesMask)
                        AvailableQuestLineXQuestIDs.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(AvailableQuestLineXQuestIDs.Size(), data);
                }
                if (changesMask[6])
                {
                    if (!ignoreNestedChangesMask)
                        Field_1000.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Field_1000.Size(), data);
                }
                if (changesMask[7])
                {
                    if (!ignoreNestedChangesMask)
                        Heirlooms.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Heirlooms.Size(), data);
                }
                if (changesMask[8])
                {
                    if (!ignoreNestedChangesMask)
                        HeirloomFlags.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(HeirloomFlags.Size(), data);
                }
                if (changesMask[9])
                {
                    if (!ignoreNestedChangesMask)
                        Toys.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Toys.Size(), data);
                }
                if (changesMask[10])
                {
                    if (!ignoreNestedChangesMask)
                        Transmog.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Transmog.Size(), data);
                }
                if (changesMask[11])
                {
                    if (!ignoreNestedChangesMask)
                        ConditionalTransmog.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(ConditionalTransmog.Size(), data);
                }
                if (changesMask[12])
                {
                    if (!ignoreNestedChangesMask)
                        SelfResSpells.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(SelfResSpells.Size(), data);
                }
                if (changesMask[13])
                {
                    if (!ignoreNestedChangesMask)
                        CharacterRestrictions.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(CharacterRestrictions.Size(), data);
                }
                if (changesMask[14])
                {
                    if (!ignoreNestedChangesMask)
                        SpellPctModByLabel.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(SpellPctModByLabel.Size(), data);
                }
                if (changesMask[15])
                {
                    if (!ignoreNestedChangesMask)
                        SpellFlatModByLabel.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(SpellFlatModByLabel.Size(), data);
                }
                if (changesMask[16])
                {
                    if (!ignoreNestedChangesMask)
                        TaskQuests.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(TaskQuests.Size(), data);
                }
                if (changesMask[17])
                {
                    if (!ignoreNestedChangesMask)
                        TraitConfigs.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(TraitConfigs.Size(), data);
                }
                if (changesMask[18])
                {
                    if (!ignoreNestedChangesMask)
                        CategoryCooldownMods.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(CategoryCooldownMods.Size(), data);
                }
                if (changesMask[19])
                {
                    if (!ignoreNestedChangesMask)
                        WeeklySpellUses.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(WeeklySpellUses.Size(), data);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[3])
                {
                    for (int i = 0; i < KnownTitles.Size(); ++i)
                    {
                        if (KnownTitles.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteUInt64(KnownTitles[i]);
                        }
                    }
                }
                if (changesMask[4])
                {
                    for (int i = 0; i < DailyQuestsCompleted.Size(); ++i)
                    {
                        if (DailyQuestsCompleted.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(DailyQuestsCompleted[i]);
                        }
                    }
                }
                if (changesMask[5])
                {
                    for (int i = 0; i < AvailableQuestLineXQuestIDs.Size(); ++i)
                    {
                        if (AvailableQuestLineXQuestIDs.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(AvailableQuestLineXQuestIDs[i]);
                        }
                    }
                }
                if (changesMask[6])
                {
                    for (int i = 0; i < Field_1000.Size(); ++i)
                    {
                        if (Field_1000.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(Field_1000[i]);
                        }
                    }
                }
                if (changesMask[7])
                {
                    for (int i = 0; i < Heirlooms.Size(); ++i)
                    {
                        if (Heirlooms.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(Heirlooms[i]);
                        }
                    }
                }
                if (changesMask[8])
                {
                    for (int i = 0; i < HeirloomFlags.Size(); ++i)
                    {
                        if (HeirloomFlags.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteUInt32(HeirloomFlags[i]);
                        }
                    }
                }
                if (changesMask[9])
                {
                    for (int i = 0; i < Toys.Size(); ++i)
                    {
                        if (Toys.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(Toys[i]);
                        }
                    }
                }
                if (changesMask[10])
                {
                    for (int i = 0; i < Transmog.Size(); ++i)
                    {
                        if (Transmog.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteUInt32(Transmog[i]);
                        }
                    }
                }
                if (changesMask[11])
                {
                    for (int i = 0; i < ConditionalTransmog.Size(); ++i)
                    {
                        if (ConditionalTransmog.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(ConditionalTransmog[i]);
                        }
                    }
                }
                if (changesMask[12])
                {
                    for (int i = 0; i < SelfResSpells.Size(); ++i)
                    {
                        if (SelfResSpells.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(SelfResSpells[i]);
                        }
                    }
                }
                if (changesMask[14])
                {
                    for (int i = 0; i < SpellPctModByLabel.Size(); ++i)
                    {
                        if (SpellPctModByLabel.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            SpellPctModByLabel[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[15])
                {
                    for (int i = 0; i < SpellFlatModByLabel.Size(); ++i)
                    {
                        if (SpellFlatModByLabel.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            SpellFlatModByLabel[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[16])
                {
                    for (int i = 0; i < TaskQuests.Size(); ++i)
                    {
                        if (TaskQuests.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            TaskQuests[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[18])
                {
                    for (int i = 0; i < CategoryCooldownMods.Size(); ++i)
                    {
                        if (CategoryCooldownMods.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            CategoryCooldownMods[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[19])
                {
                    for (int i = 0; i < WeeklySpellUses.Size(); ++i)
                    {
                        if (WeeklySpellUses.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            WeeklySpellUses[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[13])
                {
                    for (int i = 0; i < CharacterRestrictions.Size(); ++i)
                    {
                        if (CharacterRestrictions.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            CharacterRestrictions[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[17])
                {
                    for (int i = 0; i < TraitConfigs.Size(); ++i)
                    {
                        if (TraitConfigs.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            TraitConfigs[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[26])
                {
                    data.WritePackedGuid(FarsightObject);
                }
                if (changesMask[27])
                {
                    data.WritePackedGuid(SummonedBattlePetGUID);
                }
                if (changesMask[28])
                {
                    data.WriteUInt64(Coinage);
                }
                if (changesMask[29])
                {
                    data.WriteInt32(XP);
                }
                if (changesMask[30])
                {
                    data.WriteInt32(NextLevelXP);
                }
                if (changesMask[31])
                {
                    data.WriteInt32(TrialXP);
                }
                if (changesMask[32])
                {
                    Skill.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
                if (changesMask[33])
                {
                    data.WriteInt32(CharacterPoints);
                }
                if (changesMask[34])
                {
                    data.WriteInt32(MaxTalentTiers);
                }
                if (changesMask[35])
                {
                    data.WriteUInt32(TrackCreatureMask);
                }
                if (changesMask[36])
                {
                    data.WriteFloat(MainhandExpertise);
                }
                if (changesMask[37])
                {
                    data.WriteFloat(OffhandExpertise);
                }
            }
            if (changesMask[38])
            {
                if (changesMask[39])
                {
                    data.WriteFloat(RangedExpertise);
                }
                if (changesMask[40])
                {
                    data.WriteFloat(CombatRatingExpertise);
                }
                if (changesMask[41])
                {
                    data.WriteFloat(BlockPercentage);
                }
                if (changesMask[42])
                {
                    data.WriteFloat(DodgePercentage);
                }
                if (changesMask[43])
                {
                    data.WriteFloat(DodgePercentageFromAttribute);
                }
                if (changesMask[44])
                {
                    data.WriteFloat(ParryPercentage);
                }
                if (changesMask[45])
                {
                    data.WriteFloat(ParryPercentageFromAttribute);
                }
                if (changesMask[46])
                {
                    data.WriteFloat(CritPercentage);
                }
                if (changesMask[47])
                {
                    data.WriteFloat(RangedCritPercentage);
                }
                if (changesMask[48])
                {
                    data.WriteFloat(OffhandCritPercentage);
                }
                if (changesMask[49])
                {
                    data.WriteInt32(ShieldBlock);
                }
                if (changesMask[50])
                {
                    data.WriteFloat(ShieldBlockCritPercentage);
                }
                if (changesMask[51])
                {
                    data.WriteFloat(Mastery);
                }
                if (changesMask[52])
                {
                    data.WriteFloat(Speed);
                }
                if (changesMask[53])
                {
                    data.WriteFloat(Avoidance);
                }
                if (changesMask[54])
                {
                    data.WriteFloat(Sturdiness);
                }
                if (changesMask[55])
                {
                    data.WriteInt32(Versatility);
                }
                if (changesMask[56])
                {
                    data.WriteFloat(VersatilityBonus);
                }
                if (changesMask[57])
                {
                    data.WriteFloat(PvpPowerDamage);
                }
                if (changesMask[58])
                {
                    data.WriteFloat(PvpPowerHealing);
                }
                if (changesMask[59])
                {
                    data.WriteInt32(ModHealingDonePos);
                }
                if (changesMask[60])
                {
                    data.WriteFloat(ModHealingPercent);
                }
                if (changesMask[61])
                {
                    data.WriteFloat(ModHealingDonePercent);
                }
                if (changesMask[62])
                {
                    data.WriteFloat(ModPeriodicHealingDonePercent);
                }
                if (changesMask[63])
                {
                    data.WriteFloat(ModSpellPowerPercent);
                }
                if (changesMask[64])
                {
                    data.WriteFloat(ModResiliencePercent);
                }
                if (changesMask[65])
                {
                    data.WriteFloat(OverrideSpellPowerByAPPercent);
                }
                if (changesMask[66])
                {
                    data.WriteFloat(OverrideAPBySpellPowerPercent);
                }
                if (changesMask[67])
                {
                    data.WriteInt32(ModTargetResistance);
                }
                if (changesMask[68])
                {
                    data.WriteInt32(ModTargetPhysicalResistance);
                }
                if (changesMask[69])
                {
                    data.WriteUInt32(LocalFlags);
                }
            }
            if (changesMask[70])
            {
                if (changesMask[71])
                {
                    data.WriteUInt8(GrantableLevels);
                }
                if (changesMask[72])
                {
                    data.WriteUInt8(MultiActionBars);
                }
                if (changesMask[73])
                {
                    data.WriteUInt8(LifetimeMaxRank);
                }
                if (changesMask[74])
                {
                    data.WriteUInt8(NumRespecs);
                }
                if (changesMask[75])
                {
                    data.WriteInt32(AmmoID);
                }
                if (changesMask[76])
                {
                    data.WriteUInt32(PvpMedals);
                }
                if (changesMask[77])
                {
                    data.WriteUInt16(TodayHonorableKills);
                }
                if (changesMask[78])
                {
                    data.WriteUInt16(TodayDishonorableKills);
                }
                if (changesMask[79])
                {
                    data.WriteUInt16(YesterdayHonorableKills);
                }
                if (changesMask[80])
                {
                    data.WriteUInt16(YesterdayDishonorableKills);
                }
                if (changesMask[81])
                {
                    data.WriteUInt16(LastWeekHonorableKills);
                }
                if (changesMask[82])
                {
                    data.WriteUInt16(LastWeekDishonorableKills);
                }
                if (changesMask[83])
                {
                    data.WriteUInt16(ThisWeekHonorableKills);
                }
                if (changesMask[84])
                {
                    data.WriteUInt16(ThisWeekDishonorableKills);
                }
                if (changesMask[85])
                {
                    data.WriteUInt32(ThisWeekContribution);
                }
                if (changesMask[86])
                {
                    data.WriteUInt32(LifetimeHonorableKills);
                }
                if (changesMask[87])
                {
                    data.WriteUInt32(LifetimeDishonorableKills);
                }
                if (changesMask[88])
                {
                    data.WriteUInt32(Field_F24);
                }
                if (changesMask[89])
                {
                    data.WriteUInt32(YesterdayContribution);
                }
                if (changesMask[90])
                {
                    data.WriteUInt32(LastWeekContribution);
                }
                if (changesMask[91])
                {
                    data.WriteUInt32(LastWeekRank);
                }
                if (changesMask[92])
                {
                    data.WriteInt32(WatchedFactionIndex);
                }
                if (changesMask[93])
                {
                    data.WriteInt32(MaxLevel);
                }
                if (changesMask[94])
                {
                    data.WriteInt32(ScalingPlayerLevelDelta);
                }
                if (changesMask[95])
                {
                    data.WriteInt32(MaxCreatureScalingLevel);
                }
                if (changesMask[96])
                {
                    data.WriteInt32(PetSpellPower);
                }
                if (changesMask[97])
                {
                    data.WriteFloat(UiHitModifier);
                }
                if (changesMask[98])
                {
                    data.WriteFloat(UiSpellHitModifier);
                }
                if (changesMask[99])
                {
                    data.WriteInt32(HomeRealmTimeOffset);
                }
                if (changesMask[100])
                {
                    data.WriteFloat(ModPetHaste);
                }
                if (changesMask[101])
                {
                    data.WriteUInt8(LocalRegenFlags);
                }
            }
            if (changesMask[102])
            {
                if (changesMask[103])
                {
                    data.WriteUInt8(AuraVision);
                }
                if (changesMask[104])
                {
                    data.WriteUInt8(NumBackpackSlots);
                }
                if (changesMask[105])
                {
                    data.WriteInt32(OverrideSpellsID);
                }
                if (changesMask[106])
                {
                    data.WriteInt32(LfgBonusFactionID);
                }
                if (changesMask[107])
                {
                    data.WriteUInt16(LootSpecID);
                }
                if (changesMask[108])
                {
                    data.WriteUInt32(OverrideZonePVPType);
                }
                if (changesMask[109])
                {
                    data.WriteInt32(Honor);
                }
                if (changesMask[110])
                {
                    data.WriteInt32(HonorNextLevel);
                }
                if (changesMask[111])
                {
                    data.WriteInt32(Field_F74);
                }
                if (changesMask[112])
                {
                    data.WriteInt32(PvpTierMaxFromWins);
                }
                if (changesMask[113])
                {
                    data.WriteInt32(PvpLastWeeksTierMaxFromWins);
                }
                if (changesMask[114])
                {
                    data.WriteUInt8(PvpRankProgress);
                }
                if (changesMask[115])
                {
                    data.WriteInt32(PerksProgramCurrency);
                }
                if (changesMask[118])
                { 
                    data.WriteInt32(TransportServerTime);
                }
                if (changesMask[119])
                {
                    data.WriteUInt32(ActiveCombatTraitConfigID);
                }
                if (changesMask[120])
                {
                    data.WriteUInt8(GlyphsEnabled);
                }
                if (changesMask[121])
                {
                    data.WriteUInt8(LfgRoles);
                }
                if (changesMask[123])
                {
                    data.WriteUInt8(NumStableSlots);
                }
            }
            data.FlushBits();
            if (changesMask[102])
            {
                data.WriteBits(PetStable.HasValue() ? 1 : 0, 1);
                if (changesMask[116])
                {
                    ResearchHistory.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
                if (changesMask[117])
                {
                    FrozenPerksVendorItem.GetValue().Write(data);
                }
                if (changesMask[122])
                {
                    if (PetStable.HasValue())
                    {
                        PetStable.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                    }
                }
            }
            if (changesMask[124])
            {
                for (int i = 0; i < 141; ++i)
                {
                    if (changesMask[125 + i])
                    {
                        data.WritePackedGuid(InvSlots[i]);
                    }
                }
            }
            if (changesMask[266])
            {
                for (int i = 0; i < 2; ++i)
                {
                    if (changesMask[267 + i])
                    {
                        data.WriteUInt32(TrackResourceMask[i]);
                    }
                }
            }
            if (changesMask[269])
            {
                for (int i = 0; i < 7; ++i)
                {
                    if (changesMask[270 + i])
                    {
                        data.WriteFloat(SpellCritPercentage[i]);
                    }
                    if (changesMask[277 + i])
                    {
                        data.WriteInt32(ModDamageDonePos[i]);
                    }
                    if (changesMask[284 + i])
                    {
                        data.WriteInt32(ModDamageDoneNeg[i]);
                    }
                    if (changesMask[291 + i])
                    {
                        data.WriteFloat(ModDamageDonePercent[i]);
                    }
                }
            }
            if (changesMask[298])
            {
                for (int i = 0; i < 240; ++i)
                {
                    if (changesMask[299 + i])
                    {
                        data.WriteUInt64(ExploredZones[i]);
                    }
                }                
            }
            if (changesMask[539])
            {
                for (int i = 0; i < 2; ++i)
                {
                    if (changesMask[540 + i])
                    {
                        RestInfo[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                    }
                }
            }
            if (changesMask[542])
            {
                for (int i = 0; i < 3; ++i)
                {
                    if (changesMask[543 + i])
                    {
                        data.WriteFloat(WeaponDmgMultipliers[i]);
                    }
                    if (changesMask[546 + i])
                    {
                        data.WriteFloat(WeaponAtkSpeedMultipliers[i]);
                    }
                }
            }
            if (changesMask[549])
            {
                for (int i = 0; i < 12; ++i)
                {
                    if (changesMask[550 + i])
                    {
                        data.WriteUInt32(BuybackPrice[i]);
                    }
                    if (changesMask[562 + i])
                    {
                        data.WriteInt64(BuybackTimestamp[i]);
                    }
                }
            }
            if (changesMask[574])
            {
                for (int i = 0; i < 32; ++i)
                {
                    if (changesMask[575 + i])
                    {
                        data.WriteInt32(CombatRatings[i]);
                    }
                }
            }
            if (changesMask[615])
            {
                for (int i = 0; i < 4; ++i)
                {
                    if (changesMask[616 + i])
                    {
                        data.WriteUInt32(NoReagentCostMask[i]);
                    }
                }
            }
            if (changesMask[708])
            {
                for (int i = 0; i < 2; ++i)
                {
                    if (changesMask[709 + i])
                    {
                        data.WriteInt32(ProfessionSkillLine[i]);
                    }
                }
            }
            if (changesMask[623])
            {
                for (int i = 0; i < 4; ++i)
                {
                    if (changesMask[624 + i])
                    {
                        data.WriteUInt32(BagSlotFlags[i]);
                    }
                }
            }
            if (changesMask[628])
            {
                for (int i = 0; i < 7; ++i)
                {
                    if (changesMask[629 + i])
                    {
                        data.WriteUInt32(BankBagSlotFlags[i]);
                    }
                }
            }
            if (changesMask[636])
            {
                for (int i = 0; i < 875; ++i)
                {
                    if (changesMask[637 + i])
                    {
                        data.WriteUInt64(QuestCompleted[i]);
                    }
                }
            }
            if (changesMask[1512])
            {
                for (int i = 0; i < 6; ++i)
                {
                    if (changesMask[1513 + i])
                    {
                        data.WriteUInt32(GlyphSlots[i]);
                    }
                    if (changesMask[1519 + i])
                    {
                        data.WriteUInt32(Glyphs[i]);
                    }
                }
            }
            if (changesMask[607])
            {
                for (int i = 0; i < 7; ++i)
                {
                    if (changesMask[608 + i])
                    {
                        PvpInfo[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                    }
                }
            }
            data.FlushBits();
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(SortBagsRightToLeft);
            ClearChangesMask(InsertItemsLeftToRight);
            ClearChangesMask(ResearchSites);
            ClearChangesMask(ResearchSiteProgress);
            ClearChangesMask(Research);
            ClearChangesMask(KnownTitles);
            ClearChangesMask(DailyQuestsCompleted);

            ClearChangesMask(AvailableQuestLineXQuestIDs);
            ClearChangesMask(Field_1000);
            ClearChangesMask(Heirlooms);
            ClearChangesMask(HeirloomFlags);
            ClearChangesMask(Toys);
            ClearChangesMask(Transmog);
            ClearChangesMask(ConditionalTransmog);

            ClearChangesMask(SelfResSpells);
            ClearChangesMask(SpellPctModByLabel);
            ClearChangesMask(SpellFlatModByLabel);
            ClearChangesMask(TaskQuests);
            ClearChangesMask(CategoryCooldownMods);
            ClearChangesMask(WeeklySpellUses);
            ClearChangesMask(CharacterRestrictions);
            ClearChangesMask(TraitConfigs);

            ClearChangesMask(FarsightObject);
            ClearChangesMask(SummonedBattlePetGUID);
            ClearChangesMask(Coinage);
            ClearChangesMask(XP);
            ClearChangesMask(NextLevelXP);
            ClearChangesMask(TrialXP);
            ClearChangesMask(Skill);
            ClearChangesMask(CharacterPoints);

            ClearChangesMask(MaxTalentTiers);
            ClearChangesMask(TrackCreatureMask);
            ClearChangesMask(MainhandExpertise);
            ClearChangesMask(OffhandExpertise);
            ClearChangesMask(RangedExpertise);
            ClearChangesMask(CombatRatingExpertise);
            ClearChangesMask(BlockPercentage);
            ClearChangesMask(DodgePercentage);
            ClearChangesMask(DodgePercentageFromAttribute);

            ClearChangesMask(ParryPercentage);
            ClearChangesMask(ParryPercentageFromAttribute);
            ClearChangesMask(CritPercentage);
            ClearChangesMask(RangedCritPercentage);
            ClearChangesMask(OffhandCritPercentage);
            ClearChangesMask(ShieldBlock);
            ClearChangesMask(ShieldBlockCritPercentage);
            ClearChangesMask(Mastery);
            ClearChangesMask(Speed);

            ClearChangesMask(Avoidance);
            ClearChangesMask(Sturdiness);
            ClearChangesMask(Versatility);
            ClearChangesMask(VersatilityBonus);
            ClearChangesMask(PvpPowerDamage);
            ClearChangesMask(PvpPowerHealing);
            ClearChangesMask(ModHealingDonePos);
            ClearChangesMask(ModHealingPercent);
            ClearChangesMask(ModHealingDonePercent);
            ClearChangesMask(ModPeriodicHealingDonePercent);

            ClearChangesMask(ModSpellPowerPercent);
            ClearChangesMask(ModResiliencePercent);
            ClearChangesMask(OverrideSpellPowerByAPPercent);
            ClearChangesMask(OverrideAPBySpellPowerPercent);
            ClearChangesMask(ModTargetResistance);
            ClearChangesMask(ModTargetPhysicalResistance);
            ClearChangesMask(LocalFlags);

            ClearChangesMask(GrantableLevels);
            ClearChangesMask(MultiActionBars);
            ClearChangesMask(LifetimeMaxRank);
            ClearChangesMask(NumRespecs);
            ClearChangesMask(AmmoID);
            ClearChangesMask(PvpMedals);
            ClearChangesMask(TodayHonorableKills);
            ClearChangesMask(TodayDishonorableKills);
            ClearChangesMask(YesterdayHonorableKills);
            ClearChangesMask(YesterdayDishonorableKills);

            ClearChangesMask(LastWeekHonorableKills);
            ClearChangesMask(LastWeekDishonorableKills);
            ClearChangesMask(ThisWeekHonorableKills);
            ClearChangesMask(ThisWeekDishonorableKills);
            ClearChangesMask(ThisWeekContribution);
            ClearChangesMask(LifetimeHonorableKills);
            ClearChangesMask(LifetimeDishonorableKills);
            ClearChangesMask(Field_F24);

            ClearChangesMask(YesterdayContribution);
            ClearChangesMask(LastWeekContribution);
            ClearChangesMask(LastWeekRank);
            ClearChangesMask(WatchedFactionIndex);
            ClearChangesMask(MaxLevel);
            ClearChangesMask(ScalingPlayerLevelDelta);
            ClearChangesMask(MaxCreatureScalingLevel);
            ClearChangesMask(PetSpellPower);
            ClearChangesMask(UiHitModifier);
            ClearChangesMask(UiSpellHitModifier);

            ClearChangesMask(HomeRealmTimeOffset);
            ClearChangesMask(ModPetHaste);
            ClearChangesMask(LocalRegenFlags);
            ClearChangesMask(AuraVision);
            ClearChangesMask(NumBackpackSlots);
            ClearChangesMask(OverrideSpellsID);
            ClearChangesMask(LfgBonusFactionID);
            ClearChangesMask(LootSpecID);
            ClearChangesMask(OverrideZonePVPType);
            ClearChangesMask(Honor);
            ClearChangesMask(HonorNextLevel);
            ClearChangesMask(Field_F74);

            ClearChangesMask(PvpTierMaxFromWins);
            ClearChangesMask(PvpLastWeeksTierMaxFromWins);
            ClearChangesMask(PvpRankProgress);
            ClearChangesMask(PerksProgramCurrency);
            ClearChangesMask(ResearchHistory);
            ClearChangesMask(FrozenPerksVendorItem);
            ClearChangesMask(TransportServerTime);
            ClearChangesMask(ActiveCombatTraitConfigID);
            ClearChangesMask(GlyphsEnabled);
            ClearChangesMask(LfgRoles);

            ClearChangesMask(PetStable);
            ClearChangesMask(NumStableSlots);
            ClearChangesMask(InvSlots);
            ClearChangesMask(TrackResourceMask);
            ClearChangesMask(SpellCritPercentage);
            ClearChangesMask(ModDamageDonePos);
            ClearChangesMask(ModDamageDoneNeg);
            ClearChangesMask(ModDamageDonePercent);

            ClearChangesMask(ExploredZones);
            ClearChangesMask(RestInfo);
            ClearChangesMask(WeaponDmgMultipliers);
            ClearChangesMask(WeaponAtkSpeedMultipliers);
            ClearChangesMask(BuybackPrice);
            ClearChangesMask(BuybackTimestamp);
            ClearChangesMask(CombatRatings);

            ClearChangesMask(PvpInfo);
            ClearChangesMask(NoReagentCostMask);
            ClearChangesMask(ProfessionSkillLine);
            ClearChangesMask(BagSlotFlags);
            ClearChangesMask(BankBagSlotFlags);
            ClearChangesMask(QuestCompleted);
            ClearChangesMask(GlyphSlots);
            ClearChangesMask(Glyphs);
            _changesMask.ResetAll();
        }
    }

    public class GameObjectFieldData : HasChangesMask
    {
        public UpdateField<List<uint>> StateWorldEffectIDs = new(0, 1);
        public DynamicUpdateField<int> EnableDoodadSets = new(0, 2);
        public DynamicUpdateField<int> WorldEffects = new(0, 3);
        public UpdateField<int> DisplayID = new(0, 4);
        public UpdateField<uint> SpellVisualID = new(0, 5);
        public UpdateField<uint> StateSpellVisualID = new(0, 6);
        public UpdateField<uint> SpawnTrackingStateAnimID = new(0, 7);
        public UpdateField<uint> SpawnTrackingStateAnimKitID = new(0, 8);
        public UpdateField<ObjectGuid> CreatedBy = new(0, 9);
        public UpdateField<ObjectGuid> GuildGUID = new(0, 10);
        public UpdateField<uint> Flags = new(0, 11);
        public UpdateField<Quaternion> ParentRotation = new(0, 12);
        public UpdateField<int> FactionTemplate = new(0, 13);
        public UpdateField<int> Level = new(0, 14);
        public UpdateField<sbyte> State = new(0, 15);
        public UpdateField<sbyte> TypeID = new(0, 16);
        public UpdateField<byte> PercentHealth = new(0, 17);
        public UpdateField<uint> ArtKit = new(0, 18);
        public UpdateField<uint> CustomParam = new(0, 19);
        static int changeMaskLength = 20;

        public GameObjectFieldData() : base(0, TypeId.GameObject, changeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, GameObject owner, Player receiver)
        {
            data.WriteInt32(DisplayID);
            data.WriteUInt32(SpellVisualID);
            data.WriteUInt32(StateSpellVisualID);
            data.WriteUInt32(SpawnTrackingStateAnimID);
            data.WriteUInt32(SpawnTrackingStateAnimKitID);
            data.WriteInt32(StateWorldEffectIDs.GetValue().Count);
            for (int i = 0; i < StateWorldEffectIDs.GetValue().Count; ++i)
            {
                data.WriteUInt32(StateWorldEffectIDs.GetValue()[i]);
            }
            data.WritePackedGuid(CreatedBy);
            data.WritePackedGuid(GuildGUID);
            data.WriteUInt32(GetViewerGameObjectFlags(this, owner, receiver));
            data.WriteQuaternion(ParentRotation);
            data.WriteInt32(FactionTemplate);
            data.WriteInt32(Level);
            data.WriteInt8(GetViewerGameObjectState(this, owner, receiver));
            data.WriteInt8(TypeID);
            data.WriteUInt8(PercentHealth);
            data.WriteUInt32(ArtKit);
            data.WriteInt32(EnableDoodadSets.Size());
            data.WriteUInt32(CustomParam);
            data.WriteInt32(WorldEffects.Size());
            
            for (int i = 0; i < EnableDoodadSets.Size(); ++i)
            {
                data.WriteInt32(EnableDoodadSets[i]);
            }
            for (int i = 0; i < WorldEffects.Size(); ++i)
            {
                data.WriteInt32(WorldEffects[i]);
            }
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, GameObject owner, Player receiver)
        {
            WriteUpdate(data, _changesMask, false, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, GameObject owner, Player receiver)
        {
            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteBits(StateWorldEffectIDs.GetValue().Count, 32);
                    for (int i = 0; i < StateWorldEffectIDs.GetValue().Count; ++i)
                    {
                        data.WriteUInt32(StateWorldEffectIDs.GetValue()[i]);
                    }
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[2])
                {
                    if (!ignoreNestedChangesMask)
                        EnableDoodadSets.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(EnableDoodadSets.Size(), data);
                }
                if (changesMask[3])
                {
                    if (!ignoreNestedChangesMask)
                        WorldEffects.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(WorldEffects.Size(), data);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[2])
                {
                    for (int i = 0; i < EnableDoodadSets.Size(); ++i)
                    {
                        if (EnableDoodadSets.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(EnableDoodadSets[i]);
                        }
                    }
                }
                if (changesMask[3])
                {
                    for (int i = 0; i < WorldEffects.Size(); ++i)
                    {
                        if (WorldEffects.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            data.WriteInt32(WorldEffects[i]);
                        }
                    }
                }
                if (changesMask[4])
                {
                    data.WriteInt32(DisplayID);
                }
                if (changesMask[5])
                {
                    data.WriteUInt32(SpellVisualID);
                }
                if (changesMask[6])
                {
                    data.WriteUInt32(StateSpellVisualID);
                }
                if (changesMask[7])
                {
                    data.WriteUInt32(SpawnTrackingStateAnimID);
                }
                if (changesMask[8])
                {
                    data.WriteUInt32(SpawnTrackingStateAnimKitID);
                }
                if (changesMask[9])
                {
                    data.WritePackedGuid(CreatedBy);
                }
                if (changesMask[10])
                {
                    data.WritePackedGuid(GuildGUID);
                }
                if (changesMask[11])
                {
                    data.WriteUInt32(GetViewerGameObjectFlags(this, owner, receiver));
                }
                if (changesMask[12])
                {
                    data.WriteQuaternion(ParentRotation);
                }
                if (changesMask[13])
                {
                    data.WriteInt32(FactionTemplate);
                }
                if (changesMask[14])
                {
                    data.WriteInt32(Level);
                }
                if (changesMask[15])
                {
                    data.WriteInt8(GetViewerGameObjectState(this, owner, receiver));
                }
                if (changesMask[16])
                {
                    data.WriteInt8(TypeID);
                }
                if (changesMask[17])
                {
                    data.WriteUInt8(PercentHealth);
                }
                if (changesMask[18])
                {
                    data.WriteUInt32(ArtKit);
                }
                if (changesMask[19])
                {
                    data.WriteUInt32(CustomParam);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(StateWorldEffectIDs);
            ClearChangesMask(EnableDoodadSets);
            ClearChangesMask(WorldEffects);
            ClearChangesMask(DisplayID);
            ClearChangesMask(SpellVisualID);
            ClearChangesMask(StateSpellVisualID);
            ClearChangesMask(SpawnTrackingStateAnimID);
            ClearChangesMask(SpawnTrackingStateAnimKitID);
            ClearChangesMask(CreatedBy);
            ClearChangesMask(GuildGUID);
            ClearChangesMask(Flags);
            ClearChangesMask(ParentRotation);
            ClearChangesMask(FactionTemplate);
            ClearChangesMask(Level);
            ClearChangesMask(State);
            ClearChangesMask(TypeID);
            ClearChangesMask(PercentHealth);
            ClearChangesMask(ArtKit);
            ClearChangesMask(CustomParam);
            _changesMask.ResetAll();
        }

        uint GetViewerGameObjectFlags(GameObjectFieldData gameObjectData, GameObject gameObject, Player receiver)
        {
            uint flags = gameObjectData.Flags;
            if (gameObject.GetGoType() == GameObjectTypes.Chest)
                if (gameObject.GetGoInfo().Chest.usegrouplootrules != 0 && !gameObject.IsLootAllowedFor(receiver))
                    flags |= (uint)(GameObjectFlags.Locked | GameObjectFlags.NotSelectable);

            return flags;
        }

        sbyte GetViewerGameObjectState(GameObjectFieldData gameObjectData, GameObject gameObject, Player receiver)
        {
            return (sbyte)gameObject.GetGoStateFor(receiver.GetGUID());
        }
    }

    public class DynamicObjectData : HasChangesMask
    {
        public UpdateField<ObjectGuid> Caster = new(0, 1);
        public UpdateField<byte> Type = new(0, 2);
        public UpdateField<int> SpellXSpellVisualID = new(0, 3);
        public UpdateField<int> SpellID = new(0, 4);
        public UpdateField<float> Radius = new(0, 5);
        public UpdateField<uint> CastTime = new(0, 6);
        static int changeMaskLength = 7;

        public DynamicObjectData() : base(0, TypeId.DynamicObject, changeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, DynamicObject owner, Player receiver)
        {
            data.WritePackedGuid(Caster);
            data.WriteUInt8(Type);
            data.WriteInt32(SpellXSpellVisualID);
            data.WriteInt32(SpellID);
            data.WriteFloat(Radius);
            data.WriteUInt32(CastTime);
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, DynamicObject owner, Player receiver)
        {
            WriteUpdate(data, _changesMask, false, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, DynamicObject owner, Player receiver)
        {
            data.WriteBits(_changesMask.GetBlock(0), changeMaskLength);

            data.FlushBits();
            if (_changesMask[0])
            {
                if (_changesMask[1])
                {
                    data.WritePackedGuid(Caster);
                }
                if (_changesMask[2])
                {
                    data.WriteUInt8(Type);
                }
                if (_changesMask[3])
                {
                    data.WriteInt32(SpellXSpellVisualID);
                }
                if (_changesMask[4])
                {
                    data.WriteInt32(SpellID);
                }
                if (_changesMask[5])
                {
                    data.WriteFloat(Radius);
                }
                if (_changesMask[6])
                {
                    data.WriteUInt32(CastTime);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Caster);
            ClearChangesMask(Type);
            ClearChangesMask(SpellXSpellVisualID);
            ClearChangesMask(SpellID);
            ClearChangesMask(Radius);
            ClearChangesMask(CastTime);
            _changesMask.ResetAll();
        }
    }

    public class CorpseData : HasChangesMask
    {
        public DynamicUpdateField<ChrCustomizationChoice> Customizations = new(0, 1);
        public UpdateField<uint> DynamicFlags = new(0, 2);
        public UpdateField<ObjectGuid> Owner = new(0, 3);
        public UpdateField<ObjectGuid> PartyGUID = new(0, 4);
        public UpdateField<ObjectGuid> GuildGUID = new(0, 5);
        public UpdateField<uint> DisplayID = new(0, 6);
        public UpdateField<byte> RaceID = new(0, 7);
        public UpdateField<byte> Sex = new(0, 8);
        public UpdateField<byte> Class = new(0, 9);
        public UpdateField<uint> Flags = new(0, 10);
        public UpdateField<int> FactionTemplate = new(0, 11);
        public UpdateFieldArray<uint> Items = new(19, 12, 13);
        static int changeMaskLength = 32;

        public CorpseData() : base(0, TypeId.Corpse, changeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Corpse owner, Player receiver)
        {
            data.WriteUInt32(DynamicFlags);
            data.WritePackedGuid(Owner);
            data.WritePackedGuid(PartyGUID);
            data.WritePackedGuid(GuildGUID);
            data.WriteUInt32(DisplayID);
            for (int i = 0; i < 19; ++i)
            {
                data.WriteUInt32(Items[i]);
            }
            data.WriteUInt8(RaceID);
            data.WriteUInt8(Sex);
            data.WriteUInt8(Class);
            data.WriteInt32(Customizations.Size());
            data.WriteUInt32(Flags);
            data.WriteInt32(FactionTemplate);
            for (int i = 0; i < Customizations.Size(); ++i)
            {
                Customizations[i].WriteCreate(data, owner, receiver);
            }
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Corpse owner, Player receiver)
        {
            WriteUpdate(data, _changesMask, false, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, Corpse owner, Player receiver)
        {
            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);                    

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    if (!ignoreNestedChangesMask)
                        Customizations.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Customizations.Size(), data);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    for (int i = 0; i < Customizations.Size(); ++i)
                    {
                        if (Customizations.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            Customizations[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (changesMask[2])
                {
                    data.WriteUInt32(DynamicFlags);
                }
                if (changesMask[3])
                {
                    data.WritePackedGuid(Owner);
                }
                if (changesMask[4])
                {
                    data.WritePackedGuid(PartyGUID);
                }
                if (changesMask[5])
                {
                    data.WritePackedGuid(GuildGUID);
                }
                if (changesMask[6])
                {
                    data.WriteUInt32(DisplayID);
                }
                if (changesMask[7])
                {
                    data.WriteUInt8(RaceID);
                }
                if (changesMask[8])
                {
                    data.WriteUInt8(Sex);
                }
                if (changesMask[9])
                {
                    data.WriteUInt8(Class);
                }
                if (changesMask[10])
                {
                    data.WriteUInt32(Flags);
                }
                if (changesMask[11])
                {
                    data.WriteInt32(FactionTemplate);
                }
            }
            if (changesMask[12])
            {
                for (int i = 0; i < 19; ++i)
                {
                    if (changesMask[13 + i])
                    {
                        data.WriteUInt32(Items[i]);
                    }
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Customizations);
            ClearChangesMask(DynamicFlags);
            ClearChangesMask(Owner);
            ClearChangesMask(PartyGUID);
            ClearChangesMask(GuildGUID);
            ClearChangesMask(DisplayID);
            ClearChangesMask(RaceID);
            ClearChangesMask(Sex);
            ClearChangesMask(Class);
            ClearChangesMask(Flags);
            ClearChangesMask(FactionTemplate);
            ClearChangesMask(Items);
            _changesMask.ResetAll();
        }
    }

    public class ScaleCurve : HasChangesMask
    {
        public UpdateField<bool> OverrideActive = new(0, 1);
        public UpdateField<uint> StartTimeOffset = new(0, 2);
        public UpdateField<uint> ParameterCurve = new(0, 3);
        public UpdateFieldArray<Vector2> Points = new(2, 4, 5);
        static int changeMaskLength = 7;

        public ScaleCurve() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, AreaTrigger owner, Player receiver)
        {
            data.WriteUInt32(StartTimeOffset);
            for (int i = 0; i < 2; ++i)
            {
                data.WriteVector2(Points[i]);
            }
            data.WriteUInt32(ParameterCurve);
            data.WriteBit((bool)OverrideActive);
            data.FlushBits();
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, AreaTrigger owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteBit(OverrideActive);
                }
            }

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[2])
                {
                    data.WriteUInt32(StartTimeOffset);
                }
                if (changesMask[3])
                {
                    data.WriteUInt32(ParameterCurve);
                }
            }
            if (changesMask[4])
            {
                for (int i = 0; i < 2; ++i)
                {
                    if (changesMask[5 + i])
                    {
                        data.WriteVector2(Points[i]);
                    }
                }
            }
            data.FlushBits();
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(OverrideActive);
            ClearChangesMask(StartTimeOffset);
            ClearChangesMask(ParameterCurve);
            ClearChangesMask(Points);
            _changesMask.ResetAll();
        }
    }

    public class VisualAnim : HasChangesMask
    {
        public UpdateField<bool> Field_C = new(0, 1);
        public UpdateField<uint> AnimationDataID = new(0, 2);
        public UpdateField<uint> AnimKitID = new(0, 3);
        public UpdateField<uint> AnimProgress = new(0, 4);
        static int changeMaskLength = 5;

        public VisualAnim() : base(0, TypeId.AreaTrigger, changeMaskLength) { }

        public void WriteCreate(WorldPacket data, AreaTrigger owner, Player receiver)
        {
            data.WriteUInt32(AnimationDataID);
            data.WriteUInt32(AnimKitID);
            data.WriteUInt32(AnimProgress);
            data.WriteBit(Field_C);
            data.FlushBits();
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, AreaTrigger owner, Player receiver)
        {
            UpdateMask changesMask = _changesMask;
            if (ignoreChangesMask)
                changesMask.SetAll();

            data.WriteBits(changesMask.GetBlock(0), changeMaskLength);

            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    data.WriteBit(Field_C);
                }
            }
            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[2])
                {
                    data.WriteUInt32(AnimationDataID);
                }
                if (changesMask[3])
                {
                    data.WriteUInt32(AnimKitID);
                }
                if (changesMask[4])
                {
                    data.WriteUInt32(AnimProgress);
                }
            }
            data.FlushBits();
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Field_C);
            ClearChangesMask(AnimationDataID);
            ClearChangesMask(AnimKitID);
            ClearChangesMask(AnimProgress);
            _changesMask.ResetAll();
        }
    }

    public class AreaTriggerFieldData : HasChangesMask
    {
        public UpdateField<ScaleCurve> OverrideScaleCurve = new(0, 1);
        public UpdateField<ScaleCurve> ExtraScaleCurve = new(0, 2);
        public UpdateField<ScaleCurve> OverrideMoveCurveX = new(0, 3);
        public UpdateField<ScaleCurve> OverrideMoveCurveY = new(0, 4);
        public UpdateField<ScaleCurve> OverrideMoveCurveZ = new(0, 5);
        public UpdateField<ObjectGuid> Caster = new(0, 6);
        public UpdateField<uint> Duration = new(0, 7);
        public UpdateField<uint> TimeToTarget = new(0, 8);
        public UpdateField<uint> TimeToTargetScale = new(0, 9);
        public UpdateField<uint> TimeToTargetExtraScale = new(0, 10);
        public UpdateField<uint> TimeToTargetPos = new(0, 11); // Linked to m_overrideMoveCurve
        public UpdateField<int> SpellID = new(0, 12);
        public UpdateField<int> SpellForVisuals = new(0, 13);
        public UpdateField<int> SpellXSpellVisualID = new(0, 14);
        public UpdateField<float> BoundsRadius2D = new(0, 15);
        public UpdateField<uint> DecalPropertiesID = new(0, 16);
        public UpdateField<ObjectGuid> CreatingEffectGUID = new(0, 17);
        public UpdateField<ObjectGuid> OrbitPathTarget = new(0, 18);
        public UpdateField<VisualAnim> VisualAnim = new(0, 19);
        static int changeMaskLength = 20;

        public AreaTriggerFieldData() : base(0, TypeId.AreaTrigger, changeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, AreaTrigger owner, Player receiver)
        {
            OverrideScaleCurve.GetValue().WriteCreate(data, owner, receiver);
            data.WritePackedGuid(Caster);
            data.WriteUInt32(Duration);
            data.WriteUInt32(TimeToTarget);
            data.WriteUInt32(TimeToTargetScale);
            data.WriteUInt32(TimeToTargetExtraScale);
            data.WriteUInt32(TimeToTargetPos);
            data.WriteInt32(SpellID);
            data.WriteInt32(SpellForVisuals);
            data.WriteFloat(BoundsRadius2D);
            data.WriteUInt32(DecalPropertiesID);
            data.WritePackedGuid(CreatingEffectGUID);
            data.WritePackedGuid(OrbitPathTarget);
            ExtraScaleCurve.GetValue().WriteCreate(data, owner, receiver);
            OverrideMoveCurveX.GetValue().WriteCreate(data, owner, receiver);
            OverrideMoveCurveY.GetValue().WriteCreate(data, owner, receiver);
            OverrideMoveCurveZ.GetValue().WriteCreate(data, owner, receiver);
            VisualAnim.GetValue().WriteCreate(data, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, AreaTrigger owner, Player receiver)
        {
            WriteUpdate(data, _changesMask, false, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, AreaTrigger owner, Player receiver)
        {
            data.WriteBits(_changesMask.GetBlock(0), changeMaskLength);

            data.FlushBits();
            if (changesMask[0])
            {
                if (changesMask[1])
                {
                    OverrideScaleCurve.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
                if (_changesMask[6])
                {
                    data.WritePackedGuid(Caster);
                }
                if (_changesMask[7])
                {
                    data.WriteUInt32(Duration);
                }
                if (_changesMask[8])
                {
                    data.WriteUInt32(TimeToTarget);
                }
                if (_changesMask[9])
                {
                    data.WriteUInt32(TimeToTargetScale);
                }
                if (_changesMask[10])
                {
                    data.WriteUInt32(TimeToTargetExtraScale);
                }
                if (_changesMask[11])
                {
                    data.WriteUInt32(TimeToTargetPos);
                }
                if (changesMask[12])
                {
                    data.WriteInt32(SpellID);
                }
                if (_changesMask[13])
                {
                    data.WriteInt32(SpellForVisuals);
                }
                if (_changesMask[14])
                {
                    data.WriteInt32(SpellXSpellVisualID);
                }
                if (_changesMask[15])
                {
                    data.WriteFloat(BoundsRadius2D);
                }
                if (_changesMask[16])
                {
                    data.WriteUInt32(DecalPropertiesID);
                }
                if (_changesMask[17])
                {
                    data.WritePackedGuid(CreatingEffectGUID);
                }
                if (changesMask[18])                
                {
                    data.WritePackedGuid(OrbitPathTarget);
                }                
                if (_changesMask[2])
                {
                    ExtraScaleCurve.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
                if (changesMask[3])
                {
                    OverrideMoveCurveX.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
                if (changesMask[4])
                {
                    OverrideMoveCurveY.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
                if (changesMask[5])
                {
                    OverrideMoveCurveZ.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
                if (changesMask[19])
                {
                    VisualAnim.GetValue().WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(OverrideScaleCurve);
            ClearChangesMask(ExtraScaleCurve);
            ClearChangesMask(OverrideMoveCurveX);
            ClearChangesMask(OverrideMoveCurveY);
            ClearChangesMask(OverrideMoveCurveZ);
            ClearChangesMask(Caster);
            ClearChangesMask(Duration);
            ClearChangesMask(TimeToTarget);
            ClearChangesMask(TimeToTargetScale);
            ClearChangesMask(TimeToTargetExtraScale);
            ClearChangesMask(TimeToTargetPos);
            ClearChangesMask(SpellID);
            ClearChangesMask(SpellForVisuals);
            ClearChangesMask(SpellXSpellVisualID);
            ClearChangesMask(BoundsRadius2D);
            ClearChangesMask(DecalPropertiesID);
            ClearChangesMask(CreatingEffectGUID);
            ClearChangesMask(OrbitPathTarget);
            ClearChangesMask(VisualAnim);
            _changesMask.ResetAll();
        }
    }

    public class SceneObjectData : HasChangesMask
    {
        public UpdateField<int> ScriptPackageID = new(0, 1);
        public UpdateField<uint> RndSeedVal = new(0, 2);
        public UpdateField<ObjectGuid> CreatedBy = new(0, 3);
        public UpdateField<uint> SceneType = new(0, 4);
        static int changeMaskLength = 5;

        public SceneObjectData() : base(changeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, WorldObject owner, Player receiver)
        {
            data.WriteInt32(ScriptPackageID);
            data.WriteUInt32(RndSeedVal);
            data.WritePackedGuid(CreatedBy);
            data.WriteUInt32(SceneType);
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, WorldObject owner, Player receiver)
        {
            WriteUpdate(data, _changesMask, false, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, WorldObject owner, Player receiver)
        {
            data.WriteBits(_changesMask.GetBlock(0), changeMaskLength);

            data.FlushBits();
            if (_changesMask[0])
            {
                if (_changesMask[1])
                {
                    data.WriteInt32(ScriptPackageID);
                }
                if (_changesMask[2])
                {
                    data.WriteUInt32(RndSeedVal);
                }
                if (_changesMask[3])
                {
                    data.WritePackedGuid(CreatedBy);
                }
                if (_changesMask[4])
                {
                    data.WriteUInt32(SceneType);
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(ScriptPackageID);
            ClearChangesMask(RndSeedVal);
            ClearChangesMask(CreatedBy);
            ClearChangesMask(SceneType);
            _changesMask.ResetAll();
        }
    }

    public class ConversationLine
    {
        public int ConversationLineID;
        public uint StartTime;
        public int UiCameraID;
        public byte ActorIndex;
        public byte Flags;

        public void WriteCreate(WorldPacket data, Conversation owner, Player receiver)
        {
            data.WriteInt32(ConversationLineID);
            data.WriteUInt32(GetViewerStartTime(this, owner, receiver));
            data.WriteInt32(UiCameraID);
            data.WriteUInt8(ActorIndex);
            data.WriteUInt8(Flags);
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Conversation owner, Player receiver)
        {
            data.WriteInt32(ConversationLineID);
            data.WriteUInt32(GetViewerStartTime(this, owner, receiver));
            data.WriteInt32(UiCameraID);
            data.WriteUInt8(ActorIndex);
            data.WriteUInt8(Flags);
        }

        public uint GetViewerStartTime(ConversationLine conversationLine, Conversation conversation, Player receiver)
        {
            uint startTime = conversationLine.StartTime;
            Locale locale = receiver.GetSession().GetSessionDbLocaleIndex();

            TimeSpan localizedStartTime = conversation.GetLineStartTime(locale, conversationLine.ConversationLineID);
            if (localizedStartTime != TimeSpan.Zero)
                startTime = (uint)localizedStartTime.TotalMilliseconds;

            return startTime;
        }
    }

    public class ConversationActorField
    {
        public ConversationActorType Type;
        public int Id;        
        public uint CreatureID;
        public uint CreatureDisplayInfoID;
        public ObjectGuid ActorGUID;

        public void WriteCreate(WorldPacket data, Conversation owner, Player receiver)
        {
            data.WriteBits((uint)Type, 1);
            data.WriteInt32(Id);
            if (Type == ConversationActorType.TalkingHead)
            {
                data.WriteUInt32(CreatureID);
                data.WriteUInt32(CreatureDisplayInfoID);
            }
            if (Type == ConversationActorType.WorldObject)
            {
                data.WritePackedGuid(ActorGUID);
            }
            data.FlushBits();
        }

        public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Conversation owner, Player receiver)
        {
            data.WriteBits((uint)Type, 1);
            data.WriteInt32(Id);
            if (Type == ConversationActorType.TalkingHead)
            {
                data.WriteUInt32(CreatureID);
                data.WriteUInt32(CreatureDisplayInfoID);
            }
            if (Type == ConversationActorType.WorldObject)
            {
                data.WritePackedGuid(ActorGUID);
            }
            data.FlushBits();
        }
    }

    public class ConversationData : HasChangesMask
    {
        public UpdateField<List<ConversationLine>> Lines = new(0, 1);
        public DynamicUpdateField<ConversationActorField> Actors = new(0, 2);
        public UpdateField<int> LastLineEndTime = new(0, 3);
        static int changeMaskLength = 4;

        public ConversationData() : base(0, TypeId.Conversation, changeMaskLength) { }

        public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Conversation owner, Player receiver)
        {
            data.WriteInt32(Lines.GetValue().Count);
            data.WriteInt32(GetViewerLastLineEndTime(this, owner, receiver));
            for (int i = 0; i < Lines.GetValue().Count; ++i)
            {
                Lines.GetValue()[i].WriteCreate(data, owner, receiver);
            }
            data.WriteInt32(Actors.Size());
            for (int i = 0; i < Actors.Size(); ++i)
            {
                Actors[i].WriteCreate(data, owner, receiver);
            }
        }

        public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Conversation owner, Player receiver)
        {
            WriteUpdate(data, _changesMask, false, owner, receiver);
        }

        public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, Conversation owner, Player receiver)
        {
            data.WriteBits(_changesMask.GetBlock(0), changeMaskLength);

            if (_changesMask[0])
            {
                if (_changesMask[1])
                {
                    List<ConversationLine> list = Lines;
                    data.WriteBits(list.Count, 32);
                    for (int i = 0; i < list.Count; ++i)
                    {
                        list[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                    }
                }
            }
            data.FlushBits();
            if (_changesMask[0])
            {
                if (_changesMask[2])
                {
                    if (!ignoreNestedChangesMask)
                        Actors.WriteUpdateMask(data);
                    else
                        WriteCompleteDynamicFieldUpdateMask(Actors.Size(), data);
                }
            }
            data.FlushBits();
            if (_changesMask[0])
            {
                if (_changesMask[2])
                {
                    for (int i = 0; i < Actors.Size(); ++i)
                    {
                        if (Actors.HasChanged(i) || ignoreNestedChangesMask)
                        {
                            Actors[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
                        }
                    }
                }
                if (_changesMask[3])
                {
                    data.WriteInt32(GetViewerLastLineEndTime(this, owner, receiver));
                }
            }
        }

        public override void ClearChangesMask()
        {
            ClearChangesMask(Lines);
            ClearChangesMask(Actors);
            ClearChangesMask(LastLineEndTime);
            _changesMask.ResetAll();
        }

        public int GetViewerLastLineEndTime(ConversationData conversationLineData, Conversation conversation, Player receiver)
        {
            Locale locale = receiver.GetSession().GetSessionDbLocaleIndex();
            return (int)conversation.GetLastLineEndTime(locale).TotalMilliseconds;
        }
    }
    
    //public class AzeriteEmpoweredItemData : HasChangesMask
    //{
    //    public UpdateFieldArray<int> Selections = new(5, 0, 1);

    //    public AzeriteEmpoweredItemData() : base(0, TypeId.AzeriteEmpoweredItem, 6) { }

    //    public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, Item owner, Player receiver)
    //    {
    //        for (int i = 0; i < 5; ++i)
    //        {
    //            data.WriteInt32(Selections[i]);
    //        }
    //    }

    //    public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, AzeriteEmpoweredItem owner, Player receiver)
    //    {
    //        WriteUpdate(data, _changesMask, false, owner, receiver);
    //    }

    //    public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, AzeriteEmpoweredItem owner, Player receiver)
    //    {
    //        data.WriteBits(_changesMask.GetBlocksMask(0), 1);
    //        if (_changesMask.GetBlock(0) != 0)
    //            data.WriteBits(_changesMask.GetBlock(0), 32);

    //        data.FlushBits();
    //        if (_changesMask[0])
    //        {
    //            for (int i = 0; i < 5; ++i)
    //            {
    //                if (_changesMask[1 + i])
    //                {
    //                    data.WriteInt32(Selections[i]);
    //                }
    //            }
    //        }
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(Selections);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class UnlockedAzeriteEssence
    //{
    //    public uint AzeriteEssenceID;
    //    public uint Rank;

    //    public void WriteCreate(WorldPacket data, AzeriteItem owner, Player receiver)
    //    {
    //        data.WriteUInt32(AzeriteEssenceID);
    //        data.WriteUInt32(Rank);
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, AzeriteItem owner, Player receiver)
    //    {
    //        data.WriteUInt32(AzeriteEssenceID);
    //        data.WriteUInt32(Rank);
    //    }
    //}

    //public class SelectedAzeriteEssences : HasChangesMask
    //{
    //    public UpdateField<bool> Enabled = new(0, 1);
    //    public UpdateField<uint> SpecializationID = new(0, 2);
    //    public UpdateFieldArray<uint> AzeriteEssenceID = new(4, 3, 4);

    //    public SelectedAzeriteEssences() : base(8) { }

    //    public void WriteCreate(WorldPacket data, AzeriteItem owner, Player receiver)
    //    {
    //        for (int i = 0; i < 4; ++i)
    //        {
    //            data.WriteUInt32(AzeriteEssenceID[i]);
    //        }
    //        data.WriteUInt32(SpecializationID);
    //        data.WriteBit(Enabled);
    //        data.FlushBits();
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, AzeriteItem owner, Player receiver)
    //    {
    //        UpdateMask changesMask = _changesMask;
    //        if (ignoreChangesMask)
    //            changesMask.SetAll();

    //        data.WriteBits(changesMask.GetBlocksMask(0), 1);
    //        if (changesMask.GetBlock(0) != 0)
    //            data.WriteBits(changesMask.GetBlock(0), 32);

    //        if (changesMask[0])
    //        {
    //            if (changesMask[1])
    //            {
    //                data.WriteBit(Enabled);
    //            }
    //        }
    //        data.FlushBits();
    //        if (changesMask[0])
    //        {
    //            if (changesMask[2])
    //            {
    //                data.WriteUInt32(SpecializationID);
    //            }
    //        }
    //        if (changesMask[3])
    //        {
    //            for (int i = 0; i < 4; ++i)
    //            {
    //                if (changesMask[4 + i])
    //                {
    //                    data.WriteUInt32(AzeriteEssenceID[i]);
    //                }
    //            }
    //        }

    //        data.FlushBits();
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(Enabled);
    //        ClearChangesMask(SpecializationID);
    //        ClearChangesMask(AzeriteEssenceID);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class AzeriteItemData : HasChangesMask
    //{
    //    public UpdateField<bool> Enabled = new(0, 1);
    //    public DynamicUpdateField<UnlockedAzeriteEssence> UnlockedEssences = new(0, 2);
    //    public DynamicUpdateField<uint> UnlockedEssenceMilestones = new(0, 4);
    //    public DynamicUpdateField<SelectedAzeriteEssences> SelectedEssences = new(0, 3);
    //    public UpdateField<ulong> Xp = new(0, 5);
    //    public UpdateField<uint> Level = new(0, 6);
    //    public UpdateField<uint> AuraLevel = new(0, 7);
    //    public UpdateField<uint> KnowledgeLevel = new(0, 8);
    //    public UpdateField<int> DEBUGknowledgeWeek = new(0, 9);

    //    public AzeriteItemData() : base(10) { }

    //    public void WriteCreate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, AzeriteItem owner, Player receiver)
    //    {
    //        if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
    //        {
    //            data.WriteUInt64(Xp);
    //            data.WriteUInt32(Level);
    //            data.WriteUInt32(AuraLevel);
    //            data.WriteUInt32(KnowledgeLevel);
    //            data.WriteInt32(DEBUGknowledgeWeek);
    //        }
    //        data.WriteInt32(UnlockedEssences.Size());
    //        data.WriteInt32(SelectedEssences.Size());
    //        data.WriteInt32(UnlockedEssenceMilestones.Size());
    //        for (int i = 0; i < UnlockedEssences.Size(); ++i)
    //        {
    //            UnlockedEssences[i].WriteCreate(data, owner, receiver);
    //        }
    //        for (int i = 0; i < UnlockedEssenceMilestones.Size(); ++i)
    //        {
    //            data.WriteUInt32(UnlockedEssenceMilestones[i]);
    //        }
    //        if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
    //        {
    //            data.WriteBit(Enabled);
    //        }
    //        for (int i = 0; i < SelectedEssences.Size(); ++i)
    //        {
    //            SelectedEssences[i].WriteCreate(data, owner, receiver);
    //        }
    //        data.FlushBits();
    //    }

    //    public void WriteUpdate(WorldPacket data, UpdateFieldFlag fieldVisibilityFlags, AzeriteItem owner, Player receiver)
    //    {
    //        UpdateMask allowedMaskForTarget = new(10, new[] { 0x0000001Du });
    //        AppendAllowedFieldsMaskForFlag(allowedMaskForTarget, fieldVisibilityFlags);
    //        WriteUpdate(data, _changesMask & allowedMaskForTarget, false, owner, receiver);
    //    }

    //    public void AppendAllowedFieldsMaskForFlag(UpdateMask allowedMaskForTarget, UpdateFieldFlag fieldVisibilityFlags)
    //    {
    //        if (fieldVisibilityFlags.HasFlag(UpdateFieldFlag.Owner))
    //            allowedMaskForTarget.OR(new UpdateMask(10, new[] { 0x000003E2u }));
    //    }

    //    public void FilterDisallowedFieldsMaskForFlag(UpdateMask changesMask, UpdateFieldFlag fieldVisibilityFlags)
    //    {
    //        UpdateMask allowedMaskForTarget = new(10, new[] { 0x0000001Du });
    //        AppendAllowedFieldsMaskForFlag(allowedMaskForTarget, fieldVisibilityFlags);
    //        changesMask.AND(allowedMaskForTarget);
    //    }

    //    public void WriteUpdate(WorldPacket data, UpdateMask changesMask, bool ignoreNestedChangesMask, AzeriteItem owner, Player receiver)
    //    {
    //        data.WriteBits(changesMask.GetBlock(0), 10);

    //        if (changesMask[0])
    //        {
    //            if (changesMask[1])
    //            {
    //                data.WriteBit(Enabled);
    //            }
    //            if (changesMask[2])
    //            {
    //                if (!ignoreNestedChangesMask)
    //                    UnlockedEssences.WriteUpdateMask(data);
    //                else
    //                    WriteCompleteDynamicFieldUpdateMask(UnlockedEssences.Size(), data);
    //            }
    //            if (changesMask[3])
    //            {
    //                if (!ignoreNestedChangesMask)
    //                    SelectedEssences.WriteUpdateMask(data);
    //                else
    //                    WriteCompleteDynamicFieldUpdateMask(SelectedEssences.Size(), data);
    //            }
    //            if (changesMask[4])
    //            {
    //                if (!ignoreNestedChangesMask)
    //                    UnlockedEssenceMilestones.WriteUpdateMask(data);
    //                else
    //                    WriteCompleteDynamicFieldUpdateMask(UnlockedEssenceMilestones.Size(), data);
    //            }
    //        }
    //        data.FlushBits();
    //        if (changesMask[0])
    //        {
    //            if (changesMask[2])
    //            {
    //                for (int i = 0; i < UnlockedEssences.Size(); ++i)
    //                {
    //                    if (UnlockedEssences.HasChanged(i) || ignoreNestedChangesMask)
    //                    {
    //                        UnlockedEssences[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
    //                    }
    //                }
    //            }
    //            if (changesMask[4])
    //            {
    //                for (int i = 0; i < UnlockedEssenceMilestones.Size(); ++i)
    //                {
    //                    if (UnlockedEssenceMilestones.HasChanged(i) || ignoreNestedChangesMask)
    //                    {
    //                        data.WriteUInt32(UnlockedEssenceMilestones[i]);
    //                    }
    //                }
    //            }
    //            if (changesMask[3])
    //            {
    //                for (int i = 0; i < SelectedEssences.Size(); ++i)
    //                {
    //                    if (SelectedEssences.HasChanged(i) || ignoreNestedChangesMask)
    //                    {
    //                        SelectedEssences[i].WriteUpdate(data, ignoreNestedChangesMask, owner, receiver);
    //                    }
    //                }
    //            }
    //            if (changesMask[5])
    //            {
    //                data.WriteUInt64(Xp);
    //            }
    //            if (changesMask[6])
    //            {
    //                data.WriteUInt32(Level);
    //            }
    //            if (changesMask[7])
    //            {
    //                data.WriteUInt32(AuraLevel);
    //            }
    //            if (changesMask[8])
    //            {
    //                data.WriteUInt32(KnowledgeLevel);
    //            }
    //            if (changesMask[9])
    //            {
    //                data.WriteInt32(DEBUGknowledgeWeek);
    //            }
    //        }
    //        data.FlushBits();
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(Enabled);
    //        ClearChangesMask(UnlockedEssences);
    //        ClearChangesMask(UnlockedEssenceMilestones);
    //        ClearChangesMask(SelectedEssences);
    //        ClearChangesMask(Xp);
    //        ClearChangesMask(Level);
    //        ClearChangesMask(AuraLevel);
    //        ClearChangesMask(KnowledgeLevel);
    //        ClearChangesMask(DEBUGknowledgeWeek);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class SpellCastVisualField
    //{
    //    public uint SpellXSpellVisualID;
    //    public uint ScriptVisualID;

    //    public void WriteCreate(WorldPacket data, WorldObject owner, Player receiver)
    //    {
    //        data.WriteUInt32(SpellXSpellVisualID);
    //        data.WriteUInt32(ScriptVisualID);
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, WorldObject owner, Player receiver)
    //    {
    //        data.WriteUInt32(SpellXSpellVisualID);
    //        data.WriteUInt32(ScriptVisualID);
    //    }
    //}

    //public class MawPower
    //{
    //    public int Field_0;
    //    public int Field_4;
    //    public int Field_8;

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WriteInt32(Field_0);
    //        data.WriteInt32(Field_4);
    //        data.WriteInt32(Field_8);
    //    }

    //}

    //public class GlyphInfo : BaseUpdateData<Player>
    //{
    //    public UpdateField<uint> GlyphSlot = new(0, 1);
    //    public UpdateField<uint> Glyph = new(0, 2);

    //    public GlyphInfo() : base(3) { }

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WriteUInt32(GlyphSlot);
    //        data.WriteUInt32(Glyph);
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        data.WriteInt32(WorldMapOverlayIDs.Count);
    //        for (int i = 0; i < WorldMapOverlayIDs.Count; ++i)
    //        {
    //            data.WriteInt32(WorldMapOverlayIDs[i]);
    //        }
    //        data.FlushBits();
    //    }
    //}

    //public class RecipeProgressionInfo
    //{
    //    public ushort RecipeProgressionGroupID;
    //    public ushort Experience;

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WriteUInt16(RecipeProgressionGroupID);
    //        data.WriteUInt16(Experience);
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        data.WriteUInt16(RecipeProgressionGroupID);
    //        data.WriteUInt16(Experience);
    //    }
    //}

    //public class ActivePlayerUnk901 : HasChangesMask
    //{
    //    public UpdateField<ObjectGuid> Field_0 = new(0, 1);
    //    public UpdateField<int> Field_10 = new(0, 2);

    //    public ActivePlayerUnk901() : base(3) { }

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WritePackedGuid(Field_0);
    //        data.WriteInt32(Field_10);
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        UpdateMask changesMask = _changesMask;
    //        if (ignoreChangesMask)
    //            changesMask.SetAll();

    //        data.WriteBits(changesMask.GetBlock(0), 3);

    //        data.FlushBits();
    //        if (changesMask[0])
    //        {
    //            if (changesMask[1])
    //            {
    //                data.WriteUInt32(GlyphSlot);
    //            }
    //            if (changesMask[2])
    //            {
    //                data.WriteUInt32(Glyph);
    //            }
    //        }
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(GlyphSlot);
    //        ClearChangesMask(Glyph);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class QuestSession : HasChangesMask
    //{
    //    public UpdateField<ObjectGuid> Owner = new(0, 1);
    //    public UpdateFieldArray<ulong> QuestCompleted = new(875, 2, 3);

    //    public QuestSession() : base(878) { }

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WritePackedGuid(Owner);
    //        for (int i = 0; i < 875; ++i)
    //        {
    //            data.WriteUInt64(QuestCompleted[i]);
    //        }
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        UpdateMask changesMask = _changesMask;
    //        if (ignoreChangesMask)
    //            changesMask.SetAll();

    //        data.WriteBits(changesMask.GetBlocksMask(0), 28);
    //        for (uint i = 0; i < 28; ++i)
    //            if (changesMask.GetBlock(i) != 0)
    //                data.WriteBits(changesMask.GetBlock(i), 32);

    //        data.FlushBits();
    //        if (changesMask[0])
    //        {
    //            if (changesMask[1])
    //            {
    //                data.WritePackedGuid(Owner);
    //            }
    //        }
    //        if (changesMask[2])
    //        {
    //            for (int i = 0; i < 875; ++i)
    //            {
    //                if (changesMask[3 + i])
    //                {
    //                    data.WriteUInt64(QuestCompleted[i]);
    //                }
    //            }
    //        }
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(Owner);
    //        ClearChangesMask(QuestCompleted);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class ReplayedQuest : HasChangesMask
    //{
    //    public UpdateField<int> QuestID = new(0, 1);
    //    public UpdateField<uint> ReplayTime = new(0, 2);

    //    public ReplayedQuest() : base(3) { }

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WriteInt32(QuestID);
    //        data.WriteUInt32(ReplayTime);
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        UpdateMask changesMask = _changesMask;
    //        if (ignoreChangesMask)
    //            changesMask.SetAll();

    //        data.WriteBits(changesMask.GetBlock(0), 3);

    //        data.FlushBits();
    //        if (changesMask[0])
    //        {
    //            if (changesMask[1])
    //            {
    //                data.WriteInt32(QuestID);
    //            }
    //            if (changesMask[2])
    //            {
    //                data.WriteUInt32(ReplayTime);
    //            }
    //        }
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(QuestID);
    //        ClearChangesMask(ReplayTime);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class CollectableSourceTrackedData : HasChangesMask
    //{
    //    public UpdateField<int> TargetType = new(0, 1);
    //    public UpdateField<int> TargetID = new(0, 2);
    //    public UpdateField<int> CollectableSourceInfoID = new(0, 3);

    //    public CollectableSourceTrackedData() : base(4) { }

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WriteInt32(TargetType);
    //        data.WriteInt32(TargetID);
    //        data.WriteInt32(CollectableSourceInfoID);
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        UpdateMask changesMask = _changesMask;
    //        if (ignoreChangesMask)
    //            changesMask.SetAll();

    //        data.WriteBits(changesMask.GetBlock(0), 4);

    //        data.FlushBits();
    //        if (changesMask[0])
    //        {
    //            if (changesMask[1])
    //            {
    //                data.WriteInt32(TargetType);
    //            }
    //            if (changesMask[2])
    //            {
    //                data.WriteInt32(TargetID);
    //            }
    //            if (changesMask[3])
    //            {
    //                data.WriteInt32(CollectableSourceInfoID);
    //            }
    //        }
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(TargetType);
    //        ClearChangesMask(TargetID);
    //        ClearChangesMask(CollectableSourceInfoID);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class CraftingOrderItem : HasChangesMask
    //{
    //    public UpdateField<ulong> Field_0 = new(-1, 0);
    //    public UpdateField<ObjectGuid> ItemGUID = new(-1, 1);
    //    public UpdateField<ObjectGuid> OwnerGUID = new(-1, 2);
    //    public UpdateField<int> ItemID = new(-1, 3);
    //    public UpdateField<uint> Quantity = new(-1, 4);
    //    public UpdateField<int> ReagentQuality = new(-1, 5);
    //    public OptionalUpdateField<byte> DataSlotIndex = new(-1, 6);

    //    public CraftingOrderItem() : base(7) { }

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WriteUInt64(Field_0);
    //        data.WritePackedGuid(ItemGUID);
    //        data.WritePackedGuid(OwnerGUID);
    //        data.WriteInt32(ItemID);
    //        data.WriteUInt32(Quantity);
    //        data.WriteInt32(ReagentQuality);
    //        data.WriteBits(DataSlotIndex.HasValue(), 1);
    //        data.FlushBits();
    //        if (DataSlotIndex.HasValue())
    //        {
    //            data.WriteUInt8(DataSlotIndex);
    //        }
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        UpdateMask changesMask = _changesMask;
    //        if (ignoreChangesMask)
    //            changesMask.SetAll();

    //        data.WriteBits(changesMask.GetBlock(0), 7);

    //        data.FlushBits();
    //        if (changesMask[0])
    //        {
    //            data.WriteUInt64(Field_0);
    //        }
    //        if (changesMask[1])
    //        {
    //            data.WritePackedGuid(ItemGUID);
    //        }
    //        if (changesMask[2])
    //        {
    //            data.WritePackedGuid(OwnerGUID);
    //        }
    //        if (changesMask[3])
    //        {
    //            data.WriteInt32(ItemID);
    //        }
    //        if (changesMask[4])
    //        {
    //            data.WriteUInt32(Quantity);
    //        }
    //        if (changesMask[5])
    //        {
    //            data.WriteInt32(ReagentQuality);
    //        }
    //        data.WriteBits(DataSlotIndex.HasValue(), 1);
    //        data.FlushBits();
    //        if (changesMask[6])
    //        {
    //            if (DataSlotIndex.HasValue())
    //            {
    //                data.WriteUInt8(DataSlotIndex);
    //            }
    //        }
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(Field_0);
    //        ClearChangesMask(ItemGUID);
    //        ClearChangesMask(OwnerGUID);
    //        ClearChangesMask(ItemID);
    //        ClearChangesMask(Quantity);
    //        ClearChangesMask(ReagentQuality);
    //        ClearChangesMask(DataSlotIndex);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class CraftingOrderData : HasChangesMask
    //{
    //    public DynamicUpdateField<CraftingOrderItem> Reagents = new(0, 1);
    //    public UpdateField<int> Field_0 = new(0, 2);
    //    public UpdateField<ulong> OrderID = new(0, 3);
    //    public UpdateField<int> SkillLineAbilityID = new(0, 4);
    //    public UpdateField<byte> OrderState = new(5, 6);
    //    public UpdateField<byte> OrderType = new(5, 7);
    //    public UpdateField<byte> MinQuality = new(5, 8);
    //    public UpdateField<long> ExpirationTime = new(5, 9);
    //    public UpdateField<long> ClaimEndTime = new(10, 11);
    //    public UpdateField<long> TipAmount = new(10, 12);
    //    public UpdateField<long> ConsortiumCut = new(10, 13);
    //    public UpdateField<uint> Flags = new(10, 14);
    //    public UpdateField<ObjectGuid> CustomerGUID = new(15, 16);
    //    public UpdateField<ObjectGuid> CustomerAccountGUID = new(15, 17);
    //    public UpdateField<ObjectGuid> CrafterGUID = new(15, 18);
    //    public UpdateField<ObjectGuid> PersonalCrafterGUID = new(15, 19);
    //    public UpdateFieldString CustomerNotes = new(20, 21);
    //    public OptionalUpdateField<CraftingOrderItem> OutputItem = new(20, 22);
    //    public OptionalUpdateField<ItemInstance> OutputItemData = new(20, 23);

    //    public CraftingOrderData() : base(24) { }

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WriteInt32(Field_0);
    //        data.WriteUInt64(OrderID);
    //        data.WriteInt32(SkillLineAbilityID);
    //        data.WriteUInt8(OrderState);
    //        data.WriteUInt8(OrderType);
    //        data.WriteUInt8(MinQuality);
    //        data.WriteInt64(ExpirationTime);
    //        data.WriteInt64(ClaimEndTime);
    //        data.WriteInt64(TipAmount);
    //        data.WriteInt64(ConsortiumCut);
    //        data.WriteUInt32(Flags);
    //        data.WritePackedGuid(CustomerGUID);
    //        data.WritePackedGuid(CustomerAccountGUID);
    //        data.WritePackedGuid(CrafterGUID);
    //        data.WritePackedGuid(PersonalCrafterGUID);
    //        data.WriteInt32(Reagents.Size());
    //        data.WriteBits(CustomerNotes.GetValue().GetByteCount(), 10);
    //        data.WriteBits(OutputItem.HasValue(), 1);
    //        data.WriteBits(OutputItemData.HasValue(), 1);
    //        data.FlushBits();
    //        for (int i = 0; i < Reagents.Size(); ++i)
    //        {
    //            Reagents[i].WriteCreate(data, owner, receiver);
    //        }
    //        data.WriteString(CustomerNotes);
    //        if (OutputItem.HasValue())
    //        {
    //            OutputItem.GetValue().WriteCreate(data, owner, receiver);
    //        }
    //        if (OutputItemData.HasValue())
    //        {
    //            OutputItemData.GetValue().Write(data);
    //        }
    //        data.FlushBits();
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        UpdateMask changesMask = _changesMask;
    //        if (ignoreChangesMask)
    //            changesMask.SetAll();

    //        data.WriteBits(changesMask.GetBlock(0), 24);

    //        if (changesMask[0])
    //        {
    //            if (changesMask[1])
    //            {
    //                if (!ignoreChangesMask)
    //                    Reagents.WriteUpdateMask(data);
    //                else
    //                    WriteCompleteDynamicFieldUpdateMask(Reagents.Size(), data);
    //            }
    //        }
    //        data.FlushBits();
    //        if (changesMask[0])
    //        {
    //            if (changesMask[1])
    //            {
    //                for (int i = 0; i < Reagents.Size(); ++i)
    //                {
    //                    if (Reagents.HasChanged(i) || ignoreChangesMask)
    //                    {
    //                        Reagents[i].WriteUpdate(data, ignoreChangesMask, owner, receiver);
    //                    }
    //                }
    //            }
    //            if (changesMask[2])
    //            {
    //                data.WriteInt32(Field_0);
    //            }
    //            if (changesMask[3])
    //            {
    //                data.WriteUInt64(OrderID);
    //            }
    //            if (changesMask[4])
    //            {
    //                data.WriteInt32(SkillLineAbilityID);
    //            }
    //        }
    //        if (changesMask[5])
    //        {
    //            if (changesMask[6])
    //            {
    //                data.WriteUInt8(OrderState);
    //            }
    //            if (changesMask[7])
    //            {
    //                data.WriteUInt8(OrderType);
    //            }
    //            if (changesMask[8])
    //            {
    //                data.WriteUInt8(MinQuality);
    //            }
    //            if (changesMask[9])
    //            {
    //                data.WriteInt64(ExpirationTime);
    //            }
    //        }
    //        if (changesMask[10])
    //        {
    //            if (changesMask[11])
    //            {
    //                data.WriteInt64(ClaimEndTime);
    //            }
    //            if (changesMask[12])
    //            {
    //                data.WriteInt64(TipAmount);
    //            }
    //            if (changesMask[13])
    //            {
    //                data.WriteInt64(ConsortiumCut);
    //            }
    //            if (changesMask[14])
    //            {
    //                data.WriteUInt32(Flags);
    //            }
    //        }
    //        if (changesMask[15])
    //        {
    //            if (changesMask[16])
    //            {
    //                data.WritePackedGuid(CustomerGUID);
    //            }
    //            if (changesMask[17])
    //            {
    //                data.WritePackedGuid(CustomerAccountGUID);
    //            }
    //            if (changesMask[18])
    //            {
    //                data.WritePackedGuid(CrafterGUID);
    //            }
    //            if (changesMask[19])
    //            {
    //                data.WritePackedGuid(PersonalCrafterGUID);
    //            }
    //        }
    //        if (changesMask[20])
    //        {
    //            if (changesMask[21])
    //            {
    //                data.WriteBits(CustomerNotes.GetValue().GetByteCount(), 10);
    //                data.WriteString(CustomerNotes);
    //            }
    //            data.WriteBits(OutputItem.HasValue(), 1);
    //            data.WriteBits(OutputItemData.HasValue(), 1);
    //            data.FlushBits();
    //            if (changesMask[22])
    //            {
    //                if (OutputItem.HasValue())
    //                {
    //                    OutputItem.GetValue().WriteUpdate(data, ignoreChangesMask, owner, receiver);
    //                }
    //            }

    //            if (changesMask[23])
    //            {

    //                if (OutputItemData.HasValue())
    //                {
    //                    OutputItemData.GetValue().Write(data);
    //                }
    //            }
    //        }
    //        data.FlushBits();
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(Reagents);
    //        ClearChangesMask(Field_0);
    //        ClearChangesMask(OrderID);
    //        ClearChangesMask(SkillLineAbilityID);
    //        ClearChangesMask(OrderState);
    //        ClearChangesMask(OrderType);
    //        ClearChangesMask(MinQuality);
    //        ClearChangesMask(ExpirationTime);
    //        ClearChangesMask(ClaimEndTime);
    //        ClearChangesMask(TipAmount);
    //        ClearChangesMask(ConsortiumCut);
    //        ClearChangesMask(Flags);
    //        ClearChangesMask(CustomerGUID);
    //        ClearChangesMask(CustomerAccountGUID);
    //        ClearChangesMask(CrafterGUID);
    //        ClearChangesMask(PersonalCrafterGUID);
    //        ClearChangesMask(CustomerNotes);
    //        ClearChangesMask(OutputItem);
    //        ClearChangesMask(OutputItemData);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class CraftingOrder : HasChangesMask
    //{
    //    public DynamicUpdateField<ItemEnchantData> Enchantments = new(-1, 0);
    //    public DynamicUpdateField<ItemGemData> Gems = new(-1, 1);
    //    public UpdateField<CraftingOrderData> Data = new(-1, 2);
    //    public OptionalUpdateField<ItemInstance> RecraftItemInfo = new(-1, 3);

    //    public CraftingOrder() : base(4) { }

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        Data.GetValue().WriteCreate(data, owner, receiver);
    //        data.WriteBits(RecraftItemInfo.HasValue(), 1);
    //        data.WriteBits(Enchantments.Size(), 4);
    //        data.WriteBits(Gems.Size(), 2);
    //        data.FlushBits();
    //        if (RecraftItemInfo.HasValue())
    //        {
    //            RecraftItemInfo.GetValue().Write(data);
    //        }
    //        for (int i = 0; i < Enchantments.Size(); ++i)
    //        {
    //            Enchantments[i].Write(data);
    //        }
    //        for (int i = 0; i < Gems.Size(); ++i)
    //        {
    //            Gems[i].Write(data);
    //        }
    //        data.FlushBits();
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        UpdateMask changesMask = _changesMask;
    //        if (ignoreChangesMask)
    //            changesMask.SetAll();

    //        data.WriteBits(changesMask.GetBlock(0), 4);

    //        if (changesMask[0])
    //        {
    //            if (!ignoreChangesMask)
    //                Enchantments.WriteUpdateMask(data, 4);
    //            else
    //                WriteCompleteDynamicFieldUpdateMask(Enchantments.Size(), data, 4);
    //        }
    //        if (changesMask[1])
    //        {
    //            if (!ignoreChangesMask)
    //                Gems.WriteUpdateMask(data, 2);
    //            else
    //                WriteCompleteDynamicFieldUpdateMask(Gems.Size(), data, 2);
    //        }
    //        data.FlushBits();
    //        if (changesMask[0])
    //        {
    //            for (int i = 0; i < Enchantments.Size(); ++i)
    //            {
    //                if (Enchantments.HasChanged(i) || ignoreChangesMask)
    //                {
    //                    Enchantments[i].Write(data);
    //                }
    //            }
    //        }
    //        if (changesMask[1])
    //        {
    //            for (int i = 0; i < Gems.Size(); ++i)
    //            {
    //                if (Gems.HasChanged(i) || ignoreChangesMask)
    //                {
    //                    Gems[i].Write(data);
    //                }
    //            }
    //        }

    //        if (changesMask[2])
    //        {
    //            Data.GetValue().WriteUpdate(data, ignoreChangesMask, owner, receiver);
    //        }
    //        data.WriteBits(RecraftItemInfo.HasValue(), 1);
    //        data.FlushBits();
    //        if (changesMask[3])
    //        {
    //            if (RecraftItemInfo.HasValue())
    //            {
    //                RecraftItemInfo.GetValue().Write(data);
    //            }
    //        }
    //        data.FlushBits();
    //    }

    //    public override void ClearChangesMask()
    //    {
    //        ClearChangesMask(Enchantments);
    //        ClearChangesMask(Gems);
    //        ClearChangesMask(Data);
    //        ClearChangesMask(RecraftItemInfo);
    //        _changesMask.ResetAll();
    //    }
    //}

    //public class PersonalCraftingOrderCount : IEquatable<PersonalCraftingOrderCount>
    //{
    //    public int ProfessionID;
    //    public uint Count;

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WriteInt32(ProfessionID);
    //        data.WriteUInt32(Count);
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        data.WriteInt32(ProfessionID);
    //        data.WriteUInt32(Count);
    //    }

    //    public bool Equals(PersonalCraftingOrderCount right)
    //    {
    //        return ProfessionID == right.ProfessionID && Count == right.Count;
    //    }
    //}

    //public class CTROptions
    //{
    //    public uint ContentTuningConditionMask;
    //    public uint Field_4;
    //    public uint ExpansionLevelMask;

    //    public void WriteCreate(WorldPacket data, Player owner, Player receiver)
    //    {
    //        data.WriteUInt32(ContentTuningConditionMask);
    //        data.WriteUInt32(Field_4);
    //        data.WriteUInt32(ExpansionLevelMask);
    //    }

    //    public void WriteUpdate(WorldPacket data, bool ignoreChangesMask, Player owner, Player receiver)
    //    {
    //        data.WriteUInt32(ContentTuningConditionMask);
    //        data.WriteUInt32(Field_4);
    //        data.WriteUInt32(ExpansionLevelMask);
    //    }
    //}
}
