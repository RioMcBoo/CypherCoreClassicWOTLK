// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Loots;
using Game.Networking;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Game.Entities
{
    public class Item : WorldObject
    {
        public Item() : base(false)
        {
            ObjectTypeMask |= TypeMask.Item;
            ObjectTypeId = TypeId.Item;

            m_itemData = new ItemData();

            uState = ItemUpdateState.New;
            uQueuePos = -1;
            m_lastPlayedTimeUpdate = GameTime.GetGameTime();
        }

        public virtual bool Create(long guidlow, int itemId, ItemContext context, Player owner)
        {
            _Create(ObjectGuid.Create(HighGuid.Item, guidlow));

            SetEntry(itemId);
            SetObjectScale(1.0f);

            if (owner != null)
            {
                SetOwnerGUID(owner.GetGUID());
                SetContainedIn(owner.GetGUID());
            }

            ItemTemplate itemProto = Global.ObjectMgr.GetItemTemplate(itemId);
            if (itemProto == null)
                return false;

            _bonusData = new BonusData(itemProto);
            SetCount(1);
            SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.MaxDurability), itemProto.MaxDurability);
            SetDurability(itemProto.MaxDurability);

            for (int i = 0; i < itemProto.Effects.Count; ++i)
                if (itemProto.Effects[i].LegacySlotIndex < 5)
                    SetSpellCharges(itemProto.Effects[i].LegacySlotIndex, itemProto.Effects[i].Charges);

            SetExpiration(itemProto.GetDuration());
            SetCreatePlayedTime(0);
            SetContext(context);
            
            return true;
        }

        public override string GetName(Locale locale = Locale.enUS)
        {
            ItemTemplate itemTemplate = GetTemplate();
            var suffix = CliDB.ItemNameDescriptionStorage.LookupByKey(_bonusData.Suffix);
            if (suffix != null)
                return $"{itemTemplate.GetName(locale)} {suffix.Description[locale]}";

            return itemTemplate.GetName(locale);
        }

        public bool IsNotEmptyBag()
        {
            Bag bag = ToBag();
            if (bag != null)
                return !bag.IsEmpty();

            return false;
        }

        public void UpdateDuration(Player owner, uint diff)
        {
            uint duration = m_itemData.Expiration;
            if (duration == 0)
                return;

            Log.outDebug(LogFilter.Player, $"Item.UpdateDuration Item (Entry: {GetEntry()} Duration {duration} Diff {diff})");

            if (duration <= diff)
            {
                Global.ScriptMgr.OnItemExpire(owner, GetTemplate());
                owner.DestroyItem(InventoryPosition, true);
                return;
            }

            SetExpiration(duration - diff);
            SetState(ItemUpdateState.Changed, owner);                          // save new time in database
        }

        public virtual void SaveToDB(SQLTransaction trans)
        {
            PreparedStatement stmt;
            switch (uState)
            {
                case ItemUpdateState.New:
                case ItemUpdateState.Changed:
                {
                    byte index = 0;
                    stmt = CharacterDatabase.GetPreparedStatement(uState == ItemUpdateState.New ? CharStatements.REP_ITEM_INSTANCE : CharStatements.UPD_ITEM_INSTANCE);
                    stmt.AddValue(index, GetEntry());
                    stmt.AddValue(++index, GetOwnerGUID().GetCounter());
                    stmt.AddValue(++index, GetCreator().GetCounter());
                    stmt.AddValue(++index, GetGiftCreator().GetCounter());
                    stmt.AddValue(++index, GetCount());
                    stmt.AddValue(++index, (uint)m_itemData.Expiration);

                    StringBuilder ss = new();
                    for (byte i = 0; i < m_itemData.SpellCharges.GetSize() && i < _bonusData.EffectCount; ++i)
                        ss.AppendFormat($"{GetSpellCharges(i)}");

                    stmt.AddValue(++index, ss.ToString());
                    stmt.AddValue(++index, (uint)m_itemData.DynamicFlags);

                    ss.Clear();
                    for (EnchantmentSlot slot = 0; slot < EnchantmentSlot.Max; ++slot)
                    {
                        var enchantment = CliDB.SpellItemEnchantmentStorage.LookupByKey(GetEnchantmentId(slot));
                        if (enchantment != null && !enchantment.HasFlag(SpellItemEnchantmentFlags.DoNotSaveToDB))
                            ss.Append($"{GetEnchantmentId(slot)} {GetEnchantmentDuration(slot)} {GetEnchantmentCharges(slot)} ");
                        else
                            ss.Append("0 0 0 ");
                    }

                    stmt.AddValue(++index, ss.ToString());

                    stmt.AddValue(++index, m_itemData.Durability);
                    stmt.AddValue(++index, m_itemData.CreatePlayedTime);
                    stmt.AddValue(++index, m_text);
                    stmt.AddValue(++index, GetModifier(ItemModifier.BattlePetSpeciesId));
                    stmt.AddValue(++index, GetModifier(ItemModifier.BattlePetBreedData));
                    stmt.AddValue(++index, GetModifier(ItemModifier.BattlePetLevel));
                    stmt.AddValue(++index, GetModifier(ItemModifier.BattlePetDisplayId));
                    stmt.AddValue(++index, m_itemData.RandomPropertiesID);
                    stmt.AddValue(++index, m_itemData.PropertySeed);
                    stmt.AddValue(++index, (byte)m_itemData.Context);

                    stmt.AddValue(++index, GetGUID().GetCounter());

                    trans.Append(stmt);

                    if ((uState == ItemUpdateState.Changed) && IsWrapped())
                    {
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GIFT_OWNER);
                        stmt.AddValue(0, GetOwnerGUID().GetCounter());
                        stmt.AddValue(1, GetGUID().GetCounter());
                        trans.Append(stmt);
                    }

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_GEMS);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    if (m_itemData.Gems.Size() != 0)
                    {
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_ITEM_INSTANCE_GEMS);
                        stmt.AddValue(0, GetGUID().GetCounter());
                        int i = 0;
                        int gemFields = 3;

                        foreach (var gemData in m_itemData.Gems)
                        {
                            if (gemData.ItemId != 0)
                            {
                                stmt.AddValue(1 + i * gemFields, (int)gemData.ItemId);
                                StringBuilder gemBonusListIDs = new();
                                foreach (var bonusListID in gemData.BonusListIDs)
                                {
                                    if (bonusListID != 0)
                                        gemBonusListIDs.AppendFormat($"{bonusListID} ");
                                }

                                stmt.AddValue(2 + i * gemFields, gemBonusListIDs.ToString());
                                stmt.AddValue(3 + i * gemFields, (byte)gemData.Context);
                            }
                            else
                            {
                                stmt.AddValue(1 + i * gemFields, 0);
                                stmt.AddValue(2 + i * gemFields, "");
                                stmt.AddValue(3 + i * gemFields, 0);
                            }
                            ++i;
                        }

                        for (; i < ItemConst.MaxGemSockets; ++i)
                        {
                            stmt.AddValue(1 + i * gemFields, 0);
                            stmt.AddValue(2 + i * gemFields, "");
                            stmt.AddValue(3 + i * gemFields, 0);
                        }
                        trans.Append(stmt);
                    }

                    ItemModifier[] transmogMods =
                    [
                        ItemModifier.TransmogAppearanceAllSpecs,
                        ItemModifier.TransmogAppearanceSpec1,
                        ItemModifier.TransmogAppearanceSpec2,
                        ItemModifier.TransmogAppearanceSpec3,
                        ItemModifier.TransmogAppearanceSpec4,
                        ItemModifier.TransmogAppearanceSpec5,

                        ItemModifier.EnchantIllusionAllSpecs,
                        ItemModifier.EnchantIllusionSpec1,
                        ItemModifier.EnchantIllusionSpec2,
                        ItemModifier.EnchantIllusionSpec3,
                        ItemModifier.EnchantIllusionSpec4,
                        ItemModifier.EnchantIllusionSpec5,

                        ItemModifier.TransmogSecondaryAppearanceAllSpecs,
                        ItemModifier.TransmogSecondaryAppearanceSpec1,
                        ItemModifier.TransmogSecondaryAppearanceSpec2,
                        ItemModifier.TransmogSecondaryAppearanceSpec3,
                        ItemModifier.TransmogSecondaryAppearanceSpec4,
                        ItemModifier.TransmogSecondaryAppearanceSpec5
                    ];

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_TRANSMOG);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    if (transmogMods.Any(modifier => GetModifier(modifier) != 0))
                    {
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_ITEM_INSTANCE_TRANSMOG);
                        stmt.AddValue(0, GetGUID().GetCounter());
                        stmt.AddValue(1, GetModifier(ItemModifier.TransmogAppearanceAllSpecs));
                        stmt.AddValue(2, GetModifier(ItemModifier.TransmogAppearanceSpec1));
                        stmt.AddValue(3, GetModifier(ItemModifier.TransmogAppearanceSpec2));
                        stmt.AddValue(4, GetModifier(ItemModifier.TransmogAppearanceSpec3));
                        stmt.AddValue(5, GetModifier(ItemModifier.TransmogAppearanceSpec4));
                        stmt.AddValue(6, GetModifier(ItemModifier.TransmogAppearanceSpec5));
                        stmt.AddValue(7, GetModifier(ItemModifier.EnchantIllusionAllSpecs));
                        stmt.AddValue(8, GetModifier(ItemModifier.EnchantIllusionSpec1));
                        stmt.AddValue(9, GetModifier(ItemModifier.EnchantIllusionSpec2));
                        stmt.AddValue(10, GetModifier(ItemModifier.EnchantIllusionSpec3));
                        stmt.AddValue(11, GetModifier(ItemModifier.EnchantIllusionSpec4));
                        stmt.AddValue(12, GetModifier(ItemModifier.EnchantIllusionSpec5));
                        stmt.AddValue(13, GetModifier(ItemModifier.TransmogSecondaryAppearanceAllSpecs));
                        stmt.AddValue(14, GetModifier(ItemModifier.TransmogSecondaryAppearanceSpec1));
                        stmt.AddValue(15, GetModifier(ItemModifier.TransmogSecondaryAppearanceSpec2));
                        stmt.AddValue(16, GetModifier(ItemModifier.TransmogSecondaryAppearanceSpec3));
                        stmt.AddValue(17, GetModifier(ItemModifier.TransmogSecondaryAppearanceSpec4));
                        stmt.AddValue(18, GetModifier(ItemModifier.TransmogSecondaryAppearanceSpec5));
                        trans.Append(stmt);
                    }

                    break;
                }
                case ItemUpdateState.Removed:
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_GEMS);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_TRANSMOG);
                    stmt.AddValue(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    if (IsWrapped())
                    {
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GIFT);
                        stmt.AddValue(0, GetGUID().GetCounter());
                        trans.Append(stmt);
                    }

                    // Delete the items if this is a container
                    if (loot != null && !loot.IsLooted())
                        Global.LootItemStorage.RemoveStoredLootForContainer(GetGUID().GetCounter());

                    Dispose();
                    return;
                }
                case ItemUpdateState.Unchanged:
                    break;
            }

            SetState(ItemUpdateState.Unchanged);
        }

        public virtual bool LoadFromDB(long guid, ObjectGuid ownerGuid, SQLFields fields, int entry)
        {
            // create item before any checks for store correct guid
            // and allow use "FSetState(ITEM_REMOVED); SaveToDB();" for deleting item from DB
            _Create(ObjectGuid.Create(HighGuid.Item, guid));

            SetEntry(entry);
            SetObjectScale(1.0f);

            ItemTemplate proto = GetTemplate();
            if (proto == null)
            {
                Log.outError(LogFilter.PlayerItems, $"Invalid entry {GetEntry()} for item {GetGUID()}. Refusing to load.");
                return false;
            }

            _bonusData = new BonusData(proto);

            // set owner (not if item is only loaded for gbank/auction/mail
            if (!ownerGuid.IsEmpty())
                SetOwnerGUID(ownerGuid);

            var itemFlags = (ItemFieldFlags)fields.Read<int>(7);
            bool need_save = false;
            long creator = fields.Read<long>(2);
            if (creator != 0)
            {
                if (!itemFlags.HasAnyFlag(ItemFieldFlags.Child))
                    SetCreator(ObjectGuid.Create(HighGuid.Player, creator));
                else
                    SetCreator(ObjectGuid.Create(HighGuid.Item, creator));
            }

            long giftCreator = fields.Read<long>(3);
            if (giftCreator != 0)
                SetGiftCreator(ObjectGuid.Create(HighGuid.Player, giftCreator));

            SetCount(fields.Read<int>(4));

            uint duration = fields.Read<uint>(5);
            SetExpiration(duration);
            // update duration if need, and remove if not need
            if ((proto.GetDuration() == 0) != (duration == 0))
            {
                SetExpiration(proto.GetDuration());
                need_save = true;
            }

            ReplaceAllItemFlags(itemFlags);

            int durability = fields.Read<int>(9);
            SetDurability(durability);
            // update max durability (and durability) if need
            SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.MaxDurability), proto.MaxDurability);

            // do not overwrite durability for wrapped items
            if (durability > proto.MaxDurability && !IsWrapped())
            {
                SetDurability(proto.MaxDurability);
                need_save = true;
            }

            SetCreatePlayedTime(fields.Read<uint>(10));
            SetText(fields.Read<string>(11));

            SetModifier(ItemModifier.BattlePetSpeciesId, fields.Read<int>(12));
            SetModifier(ItemModifier.BattlePetBreedData, fields.Read<int>(13));
            SetModifier(ItemModifier.BattlePetLevel, fields.Read<ushort>(14));
            SetModifier(ItemModifier.BattlePetDisplayId, fields.Read<int>(15));

            SetItemRandomProperties(new ItemRandomProperties(fields.Read<int>(16), fields.Read<int>(17)));
            SetContext((ItemContext)fields.Read<byte>(18));

            // load charges after bonuses, they can add more item effects
            var tokens = new StringArray(fields.Read<string>(6), ' ');
            for (byte i = 0; i < m_itemData.SpellCharges.GetSize() && i < _bonusData.EffectCount && i < tokens.Length; ++i)
            {
                if (int.TryParse(tokens[i], out int value))
                    SetSpellCharges(i, value);
            }

            SetModifier(ItemModifier.TransmogAppearanceAllSpecs, fields.Read<int>(19));
            SetModifier(ItemModifier.TransmogAppearanceSpec1, fields.Read<int>(20));
            SetModifier(ItemModifier.TransmogAppearanceSpec2, fields.Read<int>(21));
            SetModifier(ItemModifier.TransmogAppearanceSpec3, fields.Read<int>(22));
            SetModifier(ItemModifier.TransmogAppearanceSpec4, fields.Read<int>(23));
            SetModifier(ItemModifier.TransmogAppearanceSpec5, fields.Read<int>(24));

            SetModifier(ItemModifier.EnchantIllusionAllSpecs, fields.Read<int>(25));
            SetModifier(ItemModifier.EnchantIllusionSpec1, fields.Read<int>(26));
            SetModifier(ItemModifier.EnchantIllusionSpec2, fields.Read<int>(27));
            SetModifier(ItemModifier.EnchantIllusionSpec3, fields.Read<int>(28));
            SetModifier(ItemModifier.EnchantIllusionSpec4, fields.Read<int>(39));
            SetModifier(ItemModifier.EnchantIllusionSpec4, fields.Read<int>(30));

            SetModifier(ItemModifier.TransmogSecondaryAppearanceAllSpecs, fields.Read<int>(31));
            SetModifier(ItemModifier.TransmogSecondaryAppearanceSpec1, fields.Read<int>(32));
            SetModifier(ItemModifier.TransmogSecondaryAppearanceSpec2, fields.Read<int>(33));
            SetModifier(ItemModifier.TransmogSecondaryAppearanceSpec3, fields.Read<int>(34));
            SetModifier(ItemModifier.TransmogSecondaryAppearanceSpec4, fields.Read<int>(35));
            SetModifier(ItemModifier.TransmogSecondaryAppearanceSpec5, fields.Read<int>(36));

            int gemFields = 3;
            ItemDynamicFieldGems[] gemData = new ItemDynamicFieldGems[ItemConst.MaxGemSockets];
            for (int i = 0; i < ItemConst.MaxGemSockets; ++i)
            {
                gemData[i] = new ItemDynamicFieldGems();
                gemData[i].ItemId = fields.Read<int>(37 + i * gemFields);
                var gemBonusListIDs = new StringArray(fields.Read<string>(38 + i * gemFields), ' ');
                if (!gemBonusListIDs.IsEmpty())
                {
                    var b = 0;
                    foreach (string token in gemBonusListIDs)
                    {
                        if (int.TryParse(token, out int bonusListID) && bonusListID != 0)
                            gemData[i].BonusListIDs[b++] = (ushort)bonusListID;
                    }
                }

                gemData[i].Context = fields.Read<byte>(39 + i * gemFields);
                if (gemData[i].ItemId != 0)
                    SetGem(i, gemData[i]);
            }

            // Enchants must be loaded after all other bonus/scaling data
            var enchantmentTokens = new StringArray(fields.Read<string>(8), ' ');
            if (enchantmentTokens.Length == (int)EnchantmentSlot.Max * 3)
            {
                for (int i = 0; i < (int)EnchantmentSlot.Max; ++i)
                {
                    ItemEnchantment enchantmentField = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Enchantment, i);
                    SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.ID), int.Parse(enchantmentTokens[i * 3 + 0]));
                    SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Duration), uint.Parse(enchantmentTokens[i * 3 + 1]));
                    SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Charges), short.Parse(enchantmentTokens[i * 3 + 2]));
                }
            }

            // Remove bind flag for items vs NO_BIND set
            if (IsSoulBound() && GetBonding() == ItemBondingType.None)
            {
                RemoveItemFlag(ItemFieldFlags.Soulbound);
                need_save = true;
            }

            if (need_save)                                           // normal item changed state set not work at loading
            {
                byte index = 0;
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ITEM_INSTANCE_ON_LOAD);
                stmt.AddValue(index++, (uint)m_itemData.Expiration);
                stmt.AddValue(index++, (uint)m_itemData.DynamicFlags);
                stmt.AddValue(index++, m_itemData.Durability);
                stmt.AddValue(index++, guid);
                DB.Characters.Execute(stmt);
            }
            return true;
        }

        public static void DeleteFromDB(SQLTransaction trans, long itemGuid)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE);
            stmt.AddValue(0, itemGuid);
            DB.Characters.ExecuteOrAppend(trans, stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_GEMS);
            stmt.AddValue(0, itemGuid);
            DB.Characters.ExecuteOrAppend(trans, stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_TRANSMOG);
            stmt.AddValue(0, itemGuid);
            DB.Characters.ExecuteOrAppend(trans, stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GIFT);
            stmt.AddValue(0, itemGuid);
            DB.Characters.ExecuteOrAppend(trans, stmt);
        }

        public virtual void DeleteFromDB(SQLTransaction trans)
        {
            DeleteFromDB(trans, GetGUID().GetCounter());

            // Delete the items if this is a container
            if (loot != null && !loot.IsLooted())
                Global.LootItemStorage.RemoveStoredLootForContainer(GetGUID().GetCounter());
        }

        public static void DeleteFromInventoryDB(SQLTransaction trans, long itemGuid)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_INVENTORY_BY_ITEM);
            stmt.AddValue(0, itemGuid);
            trans.Append(stmt);
        }

        public void DeleteFromInventoryDB(SQLTransaction trans)
        {
            DeleteFromInventoryDB(trans, GetGUID().GetCounter());
        }

        public ItemTemplate GetTemplate()
        {
            return Global.ObjectMgr.GetItemTemplate(GetEntry());
        }

        public override Player GetOwner()
        {
            return Global.ObjAccessor.FindPlayer(GetOwnerGUID());
        }

        public SkillType GetSkill()
        {
            ItemTemplate proto = GetTemplate();
            return proto.GetSkill();
        }

        public void SetItemRandomProperties(ItemRandomProperties randomProperties)
        {
            if (randomProperties.RandomPropertiesID == 0)
                return;

            if (randomProperties.RandomPropertiesID > 0)
            {
                var randomPropertiesEntry = CliDB.ItemRandomPropertiesStorage.LookupByKey(randomProperties.RandomPropertiesID);
                if (randomPropertiesEntry != null)
                {
                    if (m_itemData.RandomPropertiesID != randomProperties.RandomPropertiesID)
                    {
                        SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.RandomPropertiesID), randomProperties.RandomPropertiesID);
                        SetState(ItemUpdateState.Changed, GetOwner());
                    }
                    for (EnchantmentSlot i = EnchantmentSlot.Property2; i < EnchantmentSlot.Property2 + 3; ++i)
                        SetEnchantment(i, randomPropertiesEntry.Enchantment[i - EnchantmentSlot.Property2], 0, 0);
                }
            }
            else
            {
                var randomSuffixEntry = CliDB.ItemRandomSuffixStorage.LookupByKey(Math.Abs(randomProperties.RandomPropertiesID));
                if (randomSuffixEntry != null)
                {
                    if (m_itemData.RandomPropertiesID != randomProperties.RandomPropertiesID || m_itemData.PropertySeed != 0)
                    {
                        SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.RandomPropertiesID), randomProperties.RandomPropertiesID);
                        SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.PropertySeed), randomProperties.RandomPropertiesSeed);
                        SetState(ItemUpdateState.Changed, GetOwner());
                    }

                    for (EnchantmentSlot i = EnchantmentSlot.Property0; i < EnchantmentSlot.Property0 + 3; ++i)
                        SetEnchantment(i, randomSuffixEntry.Enchantment[i - EnchantmentSlot.Property0], 0, 0);
                }
            }
        }

        public void SetState(ItemUpdateState state, Player forplayer = null)
        {
            if (uState == ItemUpdateState.New && state == ItemUpdateState.Removed)
            {
                // pretend the item never existed
                if (forplayer != null)
                {
                    RemoveItemFromUpdateQueueOf(this, forplayer);
                    forplayer.DeleteRefundReference(GetGUID());
                }
                return;
            }
            if (state != ItemUpdateState.Unchanged)
            {
                // new items must stay in new state until saved
                if (uState != ItemUpdateState.New)
                    uState = state;

                if (forplayer != null)
                    AddItemToUpdateQueueOf(this, forplayer);
            }
            else
            {
                // unset in queue
                // the item must be removed from the queue manually
                uQueuePos = -1;
                uState = ItemUpdateState.Unchanged;
            }
        }

        static void AddItemToUpdateQueueOf(Item item, Player player)
        {
            if (item.IsInUpdateQueue())
                return;

            Cypher.Assert(player != null);

            if (player.GetGUID() != item.GetOwnerGUID())
            {
                Log.outError(LogFilter.Player, "Item.AddToUpdateQueueOf - Owner's guid ({0}) and player's guid ({1}) don't match!", item.GetOwnerGUID(), player.GetGUID().ToString());
                return;
            }

            if (player.m_itemUpdateQueueBlocked)
                return;

            player.ItemUpdateQueue.Add(item);
            item.uQueuePos = player.ItemUpdateQueue.Count - 1;
        }

        public static void RemoveItemFromUpdateQueueOf(Item item, Player player)
        {
            if (!item.IsInUpdateQueue())
                return;

            Cypher.Assert(player != null);

            if (player.GetGUID() != item.GetOwnerGUID())
            {
                Log.outError(LogFilter.Player, "Item.RemoveFromUpdateQueueOf - Owner's guid ({0}) and player's guid ({1}) don't match!", item.GetOwnerGUID().ToString(), player.GetGUID().ToString());
                return;
            }

            if (player.m_itemUpdateQueueBlocked)
                return;

            player.ItemUpdateQueue[item.uQueuePos] = null;
            item.uQueuePos = -1;
        }

        public bool IsEquipped() { return !IsInBag() && m_slot < EquipmentSlot.End
                || (m_slot >= ProfessionSlots.Start && m_slot < ProfessionSlots.End); }

        public bool CanBeTraded(bool mail = false, bool trade = false)
        {
            if (m_lootGenerated)
                return false;

            if ((!mail || !IsBoundAccountWide()) && (IsSoulBound() && (!IsBOPTradeable() || !trade)))
                return false;

            if (IsBag() && (InventoryPosition.IsBagSlotPos || !ToBag().IsEmpty()))
                return false;

            Player owner = GetOwner();
            if (owner != null)
            {
                if (owner.CanUnequipItem(InventoryPosition, false) != InventoryResult.Ok)
                    return false;
                if (owner.GetLootGUID() == GetGUID())
                    return false;
            }

            if (IsBoundByEnchant())
                return false;

            return true;
        }

        public void SetCount(int value)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.StackCount), value);

            Player player = GetOwner();
            if (player != null)
            {
                TradeData tradeData = player.GetTradeData();
                if (tradeData != null)
                {
                    TradeSlots slot = tradeData.GetTradeSlotForItem(GetGUID());

                    if (slot != TradeSlots.Invalid)
                        tradeData.SetItem(slot, this, true);
                }
            }
        }

        public long CalculateDurabilityRepairCost(float discount)
        {
            var maxDurability = m_itemData.MaxDurability;
            if (maxDurability == 0)
                return 0;

            var curDurability = m_itemData.Durability;
            Cypher.Assert(maxDurability >= curDurability);

            var lostDurability = maxDurability - curDurability;
            if (lostDurability == 0)
                return 0;

            ItemTemplate itemTemplate = GetTemplate();

            var durabilityCost = CliDB.DurabilityCostsStorage.LookupByKey(GetItemLevel(GetOwner()));
            if (durabilityCost == null)
                return 0;

            var durabilityQualityEntryId = ((int)GetQuality() + 1) * 2;
            var durabilityQualityEntry = CliDB.DurabilityQualityStorage.LookupByKey(durabilityQualityEntryId);
            if (durabilityQualityEntry == null)
                return 0;

            int dmultiplier = 0;
            if (itemTemplate.GetClass() == ItemClass.Weapon)
                dmultiplier = durabilityCost.WeaponSubClassCost[itemTemplate.GetSubClass()];
            else if (itemTemplate.GetClass() == ItemClass.Armor)
                dmultiplier = durabilityCost.ArmorSubClassCost[itemTemplate.GetSubClass()];

            var cost = (long)Math.Round(lostDurability * dmultiplier * durabilityQualityEntry.Data * GetRepairCostMultiplier());
            cost = (long)(cost * discount * WorldConfig.GetFloatValue(WorldCfg.RateRepaircost));

            if (cost == 0) // Fix for ITEM_QUALITY_ARTIFACT
                cost = 1;

            return cost;
        }

        bool HasEnchantRequiredSkill(Player player)
        {
            // Check all enchants for required skill
            for (var enchant_slot = EnchantmentSlot.EnhancementPermanent; enchant_slot < EnchantmentSlot.Max; ++enchant_slot)
            {
                var enchant_id = GetEnchantmentId(enchant_slot);
                if (enchant_id != 0)
                {
                    SpellItemEnchantmentRecord enchantEntry = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                    if (enchantEntry != null)
                        if (enchantEntry.RequiredSkillID != 0 && player.GetSkillValue(enchantEntry.RequiredSkillID) < enchantEntry.RequiredSkillRank)
                            return false;
                }
            }

            return true;
        }

        int GetEnchantRequiredLevel()
        {
            int level = 0;

            // Check all enchants for required level
            for (var enchant_slot = EnchantmentSlot.EnhancementPermanent; enchant_slot < EnchantmentSlot.Max; ++enchant_slot)
            {
                var enchant_id = GetEnchantmentId(enchant_slot);
                if (enchant_id != 0)
                {
                    var enchantEntry = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                    if (enchantEntry != null)
                        if (enchantEntry.MinLevel > level)
                            level = enchantEntry.MinLevel;
                }
            }

            return level;
        }

        bool IsBoundByEnchant()
        {
            // Check all enchants for soulbound
            for (var enchant_slot = EnchantmentSlot.EnhancementPermanent; enchant_slot < EnchantmentSlot.Max; ++enchant_slot)
            {
                var enchant_id = GetEnchantmentId(enchant_slot);
                if (enchant_id != 0)
                {
                    var enchantEntry = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                    if (enchantEntry != null)
                        if (enchantEntry.HasFlag(SpellItemEnchantmentFlags.Soulbound))
                            return true;
                }
            }

            return false;
        }

        public InventoryResult CanBeMergedWith(ItemTemplate proto)
        {
            // not allow merge trading currently items
            if (IsInTrade())
                return InventoryResult.TradeBoundItem;

            // not allow merge looting currently items
            if (m_lootGenerated)
                return InventoryResult.LootGone;

            // check item Type
            if (GetEntry() != proto.GetId())
                return InventoryResult.CantStack;

            return InventoryResult.Ok;
        }

        public bool IsFitToSpellRequirements(SpellInfo spellInfo)
        {
            ItemTemplate proto = GetTemplate();

            bool isEnchantSpell = spellInfo.HasEffect(SpellEffectName.EnchantItem) || spellInfo.HasEffect(SpellEffectName.EnchantItemTemporary) || spellInfo.HasEffect(SpellEffectName.EnchantItemPrismatic);
            if ((int)spellInfo.EquippedItemClass != -1)                 // -1 == any item class
            {
                if (isEnchantSpell && proto.HasFlag(ItemFlags3.CanStoreEnchants))
                    return true;

                if (spellInfo.EquippedItemClass != proto.GetClass())
                    return false;                                   //  wrong item class

                if (spellInfo.EquippedItemSubClassMask != 0)        // 0 == any subclass
                {
                    if ((spellInfo.EquippedItemSubClassMask & (1 << (int)proto.GetSubClass())) == 0)
                        return false;                               // subclass not present in mask
                }
            }

            if (isEnchantSpell && spellInfo.EquippedItemInventoryTypeMask != 0)       // 0 == any inventory Type
            {
                // Special case - accept weapon Type for main and offhand requirements
                if (proto.GetInventoryType() == InventoryType.Weapon &&
                    Convert.ToBoolean(spellInfo.EquippedItemInventoryTypeMask & (1 << (int)InventoryType.WeaponMainhand)) ||
                     Convert.ToBoolean(spellInfo.EquippedItemInventoryTypeMask & (1 << (int)InventoryType.WeaponOffhand)))
                    return true;
                else if ((spellInfo.EquippedItemInventoryTypeMask & (1 << (int)proto.GetInventoryType())) == 0)
                    return false;                                   // inventory Type not present in mask
            }

            return true;
        }

        public void SetEnchantment(EnchantmentSlot slot, int id, uint duration, int charges, ObjectGuid caster = default)
        {
            // Better lost small time at check in comparison lost time at item save to DB.
            if ((GetEnchantmentId(slot) == id) && (GetEnchantmentDuration(slot) == duration) && (GetEnchantmentCharges(slot) == charges))
                return;

            Player owner = GetOwner();
            if (slot < EnchantmentSlot.EnhancementMax)
            {
                var oldEnchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(GetEnchantmentId(slot));
                if (oldEnchant != null && !oldEnchant.HasFlag(SpellItemEnchantmentFlags.DoNotLog))
                    owner.GetSession().SendEnchantmentLog(GetOwnerGUID(), ObjectGuid.Empty, GetGUID(), GetEntry(), oldEnchant.Id, slot);

                var newEnchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(id);
                if (newEnchant != null && !newEnchant.HasFlag(SpellItemEnchantmentFlags.DoNotLog))
                    owner.GetSession().SendEnchantmentLog(GetOwnerGUID(), caster, GetGUID(), GetEntry(), id, slot);
            }

            ItemEnchantment enchantmentField = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Enchantment, (int)slot);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.ID), id);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Duration), duration);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Charges), (short)charges);
            SetState(ItemUpdateState.Changed, owner);
        }

        public void SetEnchantmentDuration(EnchantmentSlot slot, uint duration, Player owner)
        {
            if (GetEnchantmentDuration(slot) == duration)
                return;

            ItemEnchantment enchantmentField = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Enchantment, (int)slot);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Duration), duration);
            SetState(ItemUpdateState.Changed, owner);
            // Cannot use GetOwner() here, has to be passed as an argument to avoid freeze due to hashtable locking
        }

        public void SetEnchantmentCharges(EnchantmentSlot slot, uint charges)
        {
            if (GetEnchantmentCharges(slot) == charges)
                return;

            ItemEnchantment enchantmentField = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Enchantment, (int)slot);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Charges), (short)charges);
            SetState(ItemUpdateState.Changed, GetOwner());
        }

        public void ClearEnchantment(EnchantmentSlot slot)
        {
            if (GetEnchantmentId(slot) == 0)
                return;

            ItemEnchantment enchantmentField = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Enchantment, (int)slot);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.ID), 0);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Duration), 0u);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Charges), (short)0);
            SetState(ItemUpdateState.Changed, GetOwner());
        }

        public SocketedGem GetGem(int slot)
        {
            //ASSERT(slot < MAX_GEM_SOCKETS);
            return slot < m_itemData.Gems.Size() ? m_itemData.Gems[slot] : null;
        }

        public void SetGem(int slot, ItemDynamicFieldGems gem)
        {
            Cypher.Assert(slot < ItemConst.MaxGemSockets);
            var gemField = (SocketedGem)m_values.ModifyValue(m_itemData.ModifyValue(m_itemData.Gems, slot));
            
            SetUpdateFieldValue(gemField.ModifyValue(gemField.ItemId), gem.ItemId);
            SetUpdateFieldValue(gemField.ModifyValue(gemField.Context), gem.Context);

            for (int i = 0; i < 16; ++i)
                SetUpdateFieldValue(ref gemField.ModifyValue(gemField.BonusListIDs, i), gem.BonusListIDs[i]);
        }

        public bool HasAllSocketsFilledWithMatchingColors()
        {
            var gemSlots = new Dictionary<int, SocketType>(ItemConst.MaxGemSockets);
            for (int i = 0; i < ItemConst.MaxGemSockets; ++i)
            {
                var socketType = GetTemplate().GetSocketType(i);
                if (socketType != SocketType.None)  // no socket slot
                    gemSlots.Add(i, socketType);
            }

            if (gemSlots.Count > m_itemData.Gems.Size())
                return false;

            var gemSlot = 0;
            foreach (var gemData in m_itemData.Gems)
            {
                if (gemSlots.TryGetValue(gemSlot, out var socketType))
                {
                    gemSlot++;
                    continue;
                }                    

                var gemColor = SocketColor.None;

                var gemProto = Global.ObjectMgr.GetItemTemplate(gemData.ItemId);
                if (gemProto != null)
                {
                    var gemProperty = CliDB.GemPropertiesStorage.LookupByKey(gemProto.GetGemProperties());
                    if (gemProperty != null)
                        gemColor = gemProperty.Color;
                }

                if (!gemColor.DoesMatchColor(socketType)) // bad gem color on this socket
                    return false;
            }
            return true;
        }

        public byte GetGemCountWithID(int GemID)
        {
            var list = (List<SocketedGem>)m_itemData.Gems;
            return (byte)list.Count(gemData => gemData.ItemId == GemID);
        }

        public byte GetGemCountWithLimitCategory(int limitCategory)
        {
            var list = (List<SocketedGem>)m_itemData.Gems;
            return (byte)list.Count(gemData =>
            {
                ItemTemplate gemProto = Global.ObjectMgr.GetItemTemplate(gemData.ItemId.GetValue());
                if (gemProto == null)
                    return false;

                return gemProto.GetItemLimitCategory() == limitCategory;
            });
        }

        public bool IsLimitedToAnotherMapOrZone(int cur_mapId, int cur_zoneId)
        {
            ItemTemplate proto = GetTemplate();
            return proto != null && ((proto.GetMap() != 0 && proto.GetMap() != cur_mapId) ||
                ((proto.GetArea(0) != 0 && proto.GetArea(0) != cur_zoneId) && (proto.GetArea(1) != 0 && proto.GetArea(1) != cur_zoneId)));
        }

        public void SendUpdateSockets()
        {
            SocketGemsSuccess socketGems = new();
            socketGems.Item = GetGUID();

            GetOwner().SendPacket(socketGems);
        }

        public void SendTimeUpdate(Player owner)
        {
            uint duration = m_itemData.Expiration;
            if (duration == 0)
                return;

            ItemTimeUpdate itemTimeUpdate = new();
            itemTimeUpdate.ItemGuid = GetGUID();
            itemTimeUpdate.DurationLeft = duration;
            owner.SendPacket(itemTimeUpdate);
        }

        public static Item CreateItem(int itemEntry, int count, ItemContext context, Player player = null)
        {
            if (count < 1)
                return null;                                        //don't create item at zero count

            var pProto = Global.ObjectMgr.GetItemTemplate(itemEntry);
            if (pProto != null)
            {
                if (count > pProto.GetMaxStackSize())
                    count = pProto.GetMaxStackSize();

                Item item = NewItemOrBag(pProto);
                if (item.Create(Global.ObjectMgr.GetGenerator(HighGuid.Item).Generate(), itemEntry, context, player))
                {
                    item.SetCount(count);
                    return item;
                }
            }

            return null;
        }

        public Item CloneItem(int count, Player player = null)
        {
            var newItem = CreateItem(GetEntry(), count, GetContext(), player);
            if (newItem == null)
                return null;

            newItem.SetCreator(GetCreator());
            newItem.SetGiftCreator(GetGiftCreator());
            newItem.ReplaceAllItemFlags((ItemFieldFlags)(m_itemData.DynamicFlags & ~(uint)(ItemFieldFlags.Refundable | ItemFieldFlags.BopTradeable)));
            newItem.SetExpiration(m_itemData.Expiration);
            return newItem;
        }

        public bool IsBindedNotWith(Player player)
        {
            // not binded item
            if (!IsSoulBound())
                return false;

            // own item
            if (GetOwnerGUID() == player.GetGUID())
                return false;

            if (IsBOPTradeable())
                if (allowedGUIDs.Contains(player.GetGUID()))
                    return false;

            // BOA item case
            if (IsBoundAccountWide())
                return false;

            return true;
        }

        public override void BuildUpdate(Dictionary<Player, UpdateData> data)
        {
            Player owner = GetOwner();
            if (owner != null)
                BuildFieldsUpdate(owner, data);
            ClearUpdateMask(false);
        }

        public override UpdateFieldFlag GetUpdateFieldFlagsFor(Player target)
        {
            if (target.GetGUID() == GetOwnerGUID())
                return UpdateFieldFlag.Owner;

            return UpdateFieldFlag.None;
        }

        public override void BuildValuesCreate(WorldPacket data, Player target)
        {
            UpdateFieldFlag flags = GetUpdateFieldFlagsFor(target);
            WorldPacket buffer = new();

            m_objectData.WriteCreate(buffer, flags, this, target);
            m_itemData.WriteCreate(buffer, flags, this, target);

            data.WriteUInt32(buffer.GetSize() + 1);
            data.WriteUInt8((byte)flags);
            data.WriteBytes(buffer);
        }

        public override void BuildValuesUpdate(WorldPacket data, Player target)
        {
            UpdateFieldFlag flags = GetUpdateFieldFlagsFor(target);
            WorldPacket buffer = new();

            if (m_values.HasChanged(TypeId.Object))
                m_objectData.WriteUpdate(buffer, flags, this, target);

            if (m_values.HasChanged(TypeId.Item))
                m_itemData.WriteUpdate(buffer, flags, this, target);


            data.WriteUInt32(buffer.GetSize());
            data.WriteUInt32(m_values.GetChangedObjectTypeMask());
            data.WriteBytes(buffer);
        }

        public override void BuildValuesUpdateWithFlag(WorldPacket data, UpdateFieldFlag flags, Player target)
        {
            UpdateMask valuesMask = new((int)TypeId.Max);
            valuesMask.Set((int)TypeId.Item);

            WorldPacket buffer = new();
            UpdateMask mask = new(43);

            buffer.WriteUInt32(valuesMask.GetBlock(0));
            m_itemData.AppendAllowedFieldsMaskForFlag(mask, flags);
            m_itemData.WriteUpdate(buffer, mask, true, this, target);

            data.WriteUInt32(buffer.GetSize());
            data.WriteBytes(buffer);
        }

        void BuildValuesUpdateForPlayerWithMask(UpdateData data, UpdateMask requestedObjectMask, UpdateMask requestedItemMask, Player target)
        {
            UpdateFieldFlag flags = GetUpdateFieldFlagsFor(target);
            UpdateMask valuesMask = new((int)TypeId.Max);
            if (requestedObjectMask.IsAnySet())
                valuesMask.Set((int)TypeId.Object);

            m_itemData.FilterDisallowedFieldsMaskForFlag(requestedItemMask, flags);
            if (requestedItemMask.IsAnySet())
                valuesMask.Set((int)TypeId.Item);

            WorldPacket buffer = new();
            buffer.WriteUInt32(valuesMask.GetBlock(0));

            if (valuesMask[(int)TypeId.Object])
                m_objectData.WriteUpdate(buffer, requestedObjectMask, true, this, target);

            if (valuesMask[(int)TypeId.Item])
                m_itemData.WriteUpdate(buffer, requestedItemMask, true, this, target);

            WorldPacket buffer1 = new();
            buffer1.WriteUInt8((byte)UpdateType.Values);
            buffer1.WritePackedGuid(GetGUID());
            buffer1.WriteUInt32(buffer.GetSize());
            buffer1.WriteBytes(buffer.GetData());

            data.AddUpdateBlock(buffer1);
        }

        public override void ClearUpdateMask(bool remove)
        {
            m_values.ClearChangesMask(m_itemData);
            base.ClearUpdateMask(remove);
        }

        public override bool AddToObjectUpdate()
        {
            Player owner = GetOwner();
            if (owner != null)
            {
                owner.GetMap().AddUpdateObject(this);
                return true;
            }

            return false;
        }

        public override void RemoveFromObjectUpdate()
        {
            Player owner = GetOwner();
            if (owner != null)
                owner.GetMap().RemoveUpdateObject(this);
        }

        public void SaveRefundDataToDB()
        {
            DeleteRefundDataFromDB();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_ITEM_REFUND_INSTANCE);
            stmt.AddValue(0, GetGUID().GetCounter());
            stmt.AddValue(1, GetRefundRecipient().GetCounter());
            stmt.AddValue(2, GetPaidMoney());
            stmt.AddValue(3, (ushort)GetPaidExtendedCost());
            DB.Characters.Execute(stmt);
        }

        public void DeleteRefundDataFromDB(SQLTransaction trans = null)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_REFUND_INSTANCE);
            stmt.AddValue(0, GetGUID().GetCounter());
            if (trans != null)
                trans.Append(stmt);
            else
                DB.Characters.Execute(stmt);
        }

        public void SetNotRefundable(Player owner, bool changestate = true, SQLTransaction trans = null, bool addToCollection = true)
        {
            if (!IsRefundable())
                return;

            ItemExpirePurchaseRefund itemExpirePurchaseRefund = new();
            itemExpirePurchaseRefund.ItemGUID = GetGUID();
            owner.SendPacket(itemExpirePurchaseRefund);

            RemoveItemFlag(ItemFieldFlags.Refundable);
            // Following is not applicable in the trading procedure
            if (changestate)
                SetState(ItemUpdateState.Changed, owner);

            SetRefundRecipient(ObjectGuid.Empty);
            SetPaidMoney(0);
            SetPaidExtendedCost(0);
            DeleteRefundDataFromDB(trans);

            owner.DeleteRefundReference(GetGUID());
            if (addToCollection)
                owner.GetSession().GetCollectionMgr().AddItemAppearance(this);
        }

        public void UpdatePlayedTime(Player owner)
        {
            // Get current played time
            uint current_playtime = m_itemData.CreatePlayedTime;
            // Calculate time elapsed since last played time update
            long curtime = GameTime.GetGameTime();
            uint elapsed = (uint)(curtime - m_lastPlayedTimeUpdate);
            uint new_playtime = current_playtime + elapsed;
            // Check if the refund timer has expired yet
            if (new_playtime <= 2 * Time.Hour)
            {
                // No? Proceed.
                // Update the data field
                SetCreatePlayedTime(new_playtime);
                // Flag as changed to get saved to DB
                SetState(ItemUpdateState.Changed, owner);
                // Speaks for itself
                m_lastPlayedTimeUpdate = curtime;
                return;
            }
            // Yes
            SetNotRefundable(owner);
        }

        public uint GetPlayedTime()
        {
            long curtime = GameTime.GetGameTime();
            uint elapsed = (uint)(curtime - m_lastPlayedTimeUpdate);
            return m_itemData.CreatePlayedTime + elapsed;
        }

        public bool IsRefundExpired()
        {
            return (GetPlayedTime() > 2 * Time.Hour);
        }

        public void SetSoulboundTradeable(List<ObjectGuid> allowedLooters)
        {
            SetItemFlag(ItemFieldFlags.BopTradeable);
            allowedGUIDs = allowedLooters;
        }

        public void ClearSoulboundTradeable(Player currentOwner)
        {
            RemoveItemFlag(ItemFieldFlags.BopTradeable);
            if (allowedGUIDs.Empty())
                return;

            currentOwner.GetSession().GetCollectionMgr().AddItemAppearance(this);
            allowedGUIDs.Clear();
            SetState(ItemUpdateState.Changed, currentOwner);
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_BOP_TRADE);
            stmt.AddValue(0, GetGUID().GetCounter());
            DB.Characters.Execute(stmt);
        }

        public bool CheckSoulboundTradeExpire()
        {
            // called from owner's update - GetOwner() MUST be valid
            if (m_itemData.CreatePlayedTime + 2 * Time.Hour < GetOwner().GetTotalPlayedTime())
            {
                ClearSoulboundTradeable(GetOwner());
                return true; // remove from tradeable list
            }

            return false;
        }

        bool IsValidTransmogrificationTarget()
        {
            ItemTemplate proto = GetTemplate();
            if (proto == null)
                return false;

            if (proto.GetClass() != ItemClass.Armor &&
                proto.GetClass() != ItemClass.Weapon)
                return false;

            if (proto.GetClass() == ItemClass.Weapon && proto.GetSubClass() == (uint)ItemSubClassWeapon.FishingPole)
                return false;

            if (proto.HasFlag(ItemFlags2.NoAlterItemVisual))
                return false;

            if (!HasStats())
                return false;

            return true;
        }

        bool HasStats()
        {
            ItemTemplate proto = GetTemplate();
            for (int i = 0; i < ItemConst.MaxStats; ++i)
                if (proto.GetStatModifierBonusAmount(i) != 0)
                    return true;

            return false;
        }

        static ItemTransmogrificationWeaponCategory GetTransmogrificationWeaponCategory(ItemTemplate proto)
        {
            if (proto.GetClass() == ItemClass.Weapon)
            {
                switch (proto.GetSubClass().Weapon)
                {
                    case ItemSubClassWeapon.Axe2:
                    case ItemSubClassWeapon.Mace2:
                    case ItemSubClassWeapon.Sword2:
                    case ItemSubClassWeapon.Staff:
                    case ItemSubClassWeapon.Polearm:
                        return ItemTransmogrificationWeaponCategory.Melee2H;
                    case ItemSubClassWeapon.Bow:
                    case ItemSubClassWeapon.Gun:
                    case ItemSubClassWeapon.Crossbow:
                        return ItemTransmogrificationWeaponCategory.Ranged;
                    case ItemSubClassWeapon.Axe:
                    case ItemSubClassWeapon.Mace:
                    case ItemSubClassWeapon.Sword:
                    case ItemSubClassWeapon.Warglaives:
                        return ItemTransmogrificationWeaponCategory.AxeMaceSword1H;
                    case ItemSubClassWeapon.Dagger:
                        return ItemTransmogrificationWeaponCategory.Dagger;
                    case ItemSubClassWeapon.Fist:
                        return ItemTransmogrificationWeaponCategory.Fist;
                    default:
                        break;
                }
            }

            return ItemTransmogrificationWeaponCategory.Invalid;
        }

        public static int[] ItemTransmogrificationSlots =
        [
            -1,                                                     // INVTYPE_NON_EQUIP
            EquipmentSlot.Head,                                    // INVTYPE_HEAD
            -1,                                                    // INVTYPE_NECK
            EquipmentSlot.Shoulders,                               // INVTYPE_SHOULDERS
            EquipmentSlot.Shirt,                                    // INVTYPE_BODY
            EquipmentSlot.Chest,                                   // INVTYPE_CHEST
            EquipmentSlot.Waist,                                   // INVTYPE_WAIST
            EquipmentSlot.Legs,                                    // INVTYPE_LEGS
            EquipmentSlot.Feet,                                    // INVTYPE_FEET
            EquipmentSlot.Wrist,                                  // INVTYPE_WRISTS
            EquipmentSlot.Hands,                                   // INVTYPE_HANDS
            -1,                                                     // INVTYPE_FINGER
            -1,                                                     // INVTYPE_TRINKET
            -1,                                                     // INVTYPE_WEAPON
            EquipmentSlot.OffHand,                                 // INVTYPE_SHIELD
            EquipmentSlot.MainHand,                                // INVTYPE_RANGED
            EquipmentSlot.Cloak,                                    // INVTYPE_CLOAK
            EquipmentSlot.MainHand,                                 // INVTYPE_2HWEAPON
            -1,                                                     // INVTYPE_BAG
            EquipmentSlot.Tabard,                                  // INVTYPE_TABARD
            EquipmentSlot.Chest,                                   // INVTYPE_ROBE
            EquipmentSlot.MainHand,                                // INVTYPE_WEAPONMAINHAND
            EquipmentSlot.MainHand,                                 // INVTYPE_WEAPONOFFHAND
            EquipmentSlot.OffHand,                                 // INVTYPE_HOLDABLE
            -1,                                                     // INVTYPE_AMMO
            -1,                                                     // INVTYPE_THROWN
            EquipmentSlot.MainHand,                                // INVTYPE_RANGEDRIGHT
            -1,                                                     // INVTYPE_QUIVER
            -1,                                                      // INVTYPE_RELIC
            -1,                                                     // INVTYPE_PROFESSION_TOOL
            -1,                                                     // INVTYPE_PROFESSION_GEAR
            -1,                                                     // INVTYPE_EQUIPABLE_SPELL_OFFENSIVE
            -1,                                                     // INVTYPE_EQUIPABLE_SPELL_UTILITY
            -1,                                                     // INVTYPE_EQUIPABLE_SPELL_DEFENSIVE
            -1                                                      // INVTYPE_EQUIPABLE_SPELL_MOBILITY
        ];

        public static bool CanTransmogrifyItemWithItem(Item item, ItemModifiedAppearanceRecord itemModifiedAppearance)
        {
            ItemTemplate source = Global.ObjectMgr.GetItemTemplate(itemModifiedAppearance.ItemID); // source
            ItemTemplate target = item.GetTemplate(); // dest

            if (source == null || target == null)
                return false;

            if (itemModifiedAppearance == item.GetItemModifiedAppearance())
                return false;

            if (!item.IsValidTransmogrificationTarget())
                return false;

            if (source.GetClass() != target.GetClass())
                return false;

            if (source.GetInventoryType() == InventoryType.Bag ||
                source.GetInventoryType() == InventoryType.Relic ||
                source.GetInventoryType() == InventoryType.Finger ||
                source.GetInventoryType() == InventoryType.Trinket ||
                source.GetInventoryType() == InventoryType.Ammo ||
                source.GetInventoryType() == InventoryType.Quiver)
                return false;

            if (source.GetSubClass() != target.GetSubClass())
            {
                switch (source.GetClass())
                {
                    case ItemClass.Weapon:
                        if (GetTransmogrificationWeaponCategory(source) != GetTransmogrificationWeaponCategory(target))
                            return false;
                        break;
                    case ItemClass.Armor:
                        if (source.GetSubClass().Armor != ItemSubClassArmor.Cosmetic)
                            return false;
                        if (source.GetInventoryType() != target.GetInventoryType())
                            if (ItemTransmogrificationSlots[(int)source.GetInventoryType()] != ItemTransmogrificationSlots[(int)target.GetInventoryType()])
                                return false;
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

        uint GetBuyPrice(Player owner, out bool standardPrice)
        {
            return GetBuyPrice(GetTemplate(), GetQuality(), GetItemLevel(owner), out standardPrice);
        }

        static uint GetBuyPrice(ItemTemplate proto, ItemQuality quality, int itemLevel, out bool standardPrice)
        {
            standardPrice = true;

            if (proto.HasFlag(ItemFlags2.OverrideGoldCost))
                return proto.GetBuyPrice();

            var qualityPrice = CliDB.ImportPriceQualityStorage.LookupByKey((int)quality + 1);
            if (qualityPrice == null)
                return 0;

            var basePrice = CliDB.ItemPriceBaseStorage.LookupByKey(proto.GetItemLevel());
            if (basePrice == null)
                return 0;

            float qualityFactor = qualityPrice.Data;
            float baseFactor;

            var inventoryType = proto.GetInventoryType();

            if (inventoryType == InventoryType.Weapon ||
                inventoryType == InventoryType.Weapon2Hand ||
                inventoryType == InventoryType.WeaponMainhand ||
                inventoryType == InventoryType.WeaponOffhand ||
                inventoryType == InventoryType.Ranged ||
                inventoryType == InventoryType.Thrown ||
                inventoryType == InventoryType.RangedRight)
                baseFactor = basePrice.Weapon;
            else
                baseFactor = basePrice.Armor;

            if (inventoryType == InventoryType.Robe)
                inventoryType = InventoryType.Chest;

            if (proto.GetClass() == ItemClass.Gem && proto.GetSubClass().Gem == ItemSubClassGem.ArtifactRelic)
            {
                inventoryType = InventoryType.Weapon;
                baseFactor = basePrice.Weapon / 3.0f;
            }


            float typeFactor = 0.0f;
            sbyte weapType = -1;

            switch (inventoryType)
            {
                case InventoryType.Head:
                case InventoryType.Neck:
                case InventoryType.Shoulders:
                case InventoryType.Chest:
                case InventoryType.Waist:
                case InventoryType.Legs:
                case InventoryType.Feet:
                case InventoryType.Wrists:
                case InventoryType.Hands:
                case InventoryType.Finger:
                case InventoryType.Trinket:
                case InventoryType.Cloak:
                case InventoryType.Holdable:
                {
                    var armorPrice = CliDB.ImportPriceArmorStorage.LookupByKey((int)inventoryType);
                    if (armorPrice == null)
                        return 0;

                    switch (proto.GetSubClass().Armor)
                    {
                        case ItemSubClassArmor.Miscellaneous:
                        case ItemSubClassArmor.Cloth:
                            typeFactor = armorPrice.ClothModifier;
                            break;
                        case ItemSubClassArmor.Leather:
                            typeFactor = armorPrice.LeatherModifier;
                            break;
                        case ItemSubClassArmor.Mail:
                            typeFactor = armorPrice.ChainModifier;
                            break;
                        case ItemSubClassArmor.Plate:
                            typeFactor = armorPrice.PlateModifier;
                            break;
                        default:
                            typeFactor = 1.0f;
                            break;
                    }

                    break;
                }
                case InventoryType.Shield:
                {
                    var shieldPrice = CliDB.ImportPriceShieldStorage.LookupByKey(2); // it only has two rows, it's unclear which is the one used
                    if (shieldPrice == null)
                        return 0;

                    typeFactor = shieldPrice.Data;
                    break;
                }
                case InventoryType.WeaponMainhand:
                    weapType = 0;
                    break;
                case InventoryType.WeaponOffhand:
                    weapType = 1;
                    break;
                case InventoryType.Weapon:
                    weapType = 2;
                    break;
                case InventoryType.Weapon2Hand:
                    weapType = 3;
                    break;
                case InventoryType.Ranged:
                case InventoryType.RangedRight:
                case InventoryType.Relic:
                    weapType = 4;
                    break;
                default:
                    return proto.GetBuyPrice();
            }

            if (weapType != -1)
            {
                var weaponPrice = CliDB.ImportPriceWeaponStorage.LookupByKey(weapType + 1);
                if (weaponPrice == null)
                    return 0;

                typeFactor = weaponPrice.Data;
            }

            standardPrice = false;
            return (uint)(proto.GetPriceVariance() * typeFactor * baseFactor * qualityFactor * proto.GetPriceRandomValue());
        }

        public uint GetSellPrice(Player owner)
        {
            return GetSellPrice(GetTemplate(), GetQuality(), GetItemLevel(owner));
        }

        public static uint GetSellPrice(ItemTemplate proto, ItemQuality quality, int itemLevel)
        {
            if (proto.HasFlag(ItemFlags2.OverrideGoldCost))
                return proto.GetSellPrice();

            bool standardPrice;
            uint cost = GetBuyPrice(proto, quality, itemLevel, out standardPrice);

            if (standardPrice)
            {
                ItemClassRecord classEntry = Global.DB2Mgr.GetItemClassByOldEnum(proto.GetClass());
                if (classEntry != null)
                {
                    int buyCount = Math.Max(proto.GetBuyCount(), 1);
                    return (uint)(cost * classEntry.PriceModifier / buyCount);
                }

                return 0;
            }
            else
                return proto.GetSellPrice();
        }

        public int GetItemLevel(Player owner)
        {
            ItemTemplate itemTemplate = GetTemplate();
            int minItemLevel = owner.m_unitData.MinItemLevel;
            int minItemLevelCutoff = owner.m_unitData.MinItemLevelCutoff;
            int maxItemLevel = itemTemplate.HasFlag(ItemFlags3.IgnoreItemLevelCapInPvp) ? 0 : owner.m_unitData.MaxItemLevel;
            bool pvpBonus = owner.IsUsingPvpItemLevels();

            return GetItemLevel(itemTemplate, _bonusData, owner.GetLevel(), GetModifier(ItemModifier.TimewalkerLevel),
                minItemLevel, minItemLevelCutoff, maxItemLevel, pvpBonus);
        }

        public static int GetItemLevel(ItemTemplate itemTemplate, BonusData bonusData, int level, int fixedLevel, int minItemLevel, int minItemLevelCutoff, int maxItemLevel, bool pvpBonus)
        {
            if (itemTemplate == null)
                return ItemConst.MinItemLevel;

            var itemLevel = itemTemplate.GetItemLevel();

            if (bonusData.PlayerLevelToItemLevelCurveId != 0)
            {
                if (fixedLevel != 0)
                    level = fixedLevel;
                else
                {
                    var levels = Global.DB2Mgr.GetContentTuningData(bonusData.ContentTuningId, 0, true);
                    if (levels.HasValue)
                        level = Math.Min(Math.Max(level, levels.Value.MinLevel), levels.Value.MaxLevel);
                }

                itemLevel = (int)Global.DB2Mgr.GetCurveValueAt(bonusData.PlayerLevelToItemLevelCurveId, level);
            }

            itemLevel += bonusData.ItemLevelBonus;

            int itemLevelBeforeUpgrades = itemLevel;

            if (pvpBonus)
                itemLevel += Global.DB2Mgr.GetPvpItemLevelBonus(itemTemplate.GetId());

            if (itemTemplate.GetInventoryType() != InventoryType.NonEquip)
            {
                if (minItemLevel != 0 && (minItemLevelCutoff == 0 || itemLevelBeforeUpgrades >= minItemLevelCutoff) && itemLevel < minItemLevel)
                    itemLevel = minItemLevel;

                if (maxItemLevel != 0 && itemLevel > maxItemLevel)
                    itemLevel = maxItemLevel;
            }

            return Math.Min(Math.Max(itemLevel, ItemConst.MinItemLevel), ItemConst.MaxItemLevel);
        }

        public ItemDisenchantLootRecord GetDisenchantLoot(Player owner)
        {
            if (!_bonusData.CanDisenchant)
                return null;

            return GetDisenchantLoot(GetTemplate(), GetQuality(), GetItemLevel(owner));
        }

        public static ItemDisenchantLootRecord GetDisenchantLoot(ItemTemplate itemTemplate, ItemQuality quality, int itemLevel)
        {
            if (itemTemplate.HasFlag(ItemFlags.Conjured) || itemTemplate.HasFlag(ItemFlags.NoDisenchant) || itemTemplate.GetBonding() == ItemBondingType.Quest)
                return null;

            if (itemTemplate.GetArea(0) != 0 || itemTemplate.GetArea(1) != 0 || itemTemplate.GetMap() != 0 || itemTemplate.GetMaxStackSize() > 1)
                return null;

            if (GetSellPrice(itemTemplate, quality, itemLevel) == 0 && !Global.DB2Mgr.HasItemCurrencyCost(itemTemplate.GetId()))
                return null;

            var itemClass = itemTemplate.GetClass();
            var itemSubClass = itemTemplate.GetSubClass();
            var expansion = itemTemplate.GetRequiredExpansion();
            foreach (ItemDisenchantLootRecord disenchant in CliDB.ItemDisenchantLootStorage.Values)
            {
                if (disenchant.Class != itemClass)
                    continue;

                if (disenchant.Subclass >= 0 && itemSubClass != 0)
                    continue;

                if (disenchant.Quality != quality)
                    continue;

                if (disenchant.MinLevel > itemLevel || disenchant.MaxLevel < itemLevel)
                    continue;

                if (disenchant.ExpansionID != Expansion.Unk && disenchant.ExpansionID != expansion)
                    continue;

                return disenchant;
            }

            return null;
        }

        public int GetDisplayId(Player owner)
        {
            var itemModifiedAppearanceId = GetModifier(ItemConst.AppearanceModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (itemModifiedAppearanceId == 0)
                itemModifiedAppearanceId = GetModifier(ItemModifier.TransmogAppearanceAllSpecs);

            ItemModifiedAppearanceRecord transmog = CliDB.ItemModifiedAppearanceStorage.LookupByKey(itemModifiedAppearanceId);
            if (transmog != null)
            {
                ItemAppearanceRecord itemAppearance = CliDB.ItemAppearanceStorage.LookupByKey(transmog.ItemAppearanceID);
                if (itemAppearance != null)
                    return itemAppearance.ItemDisplayInfoID;
            }

            return Global.DB2Mgr.GetItemDisplayId(GetEntry(), GetAppearanceModId());
        }

        public ItemModifiedAppearanceRecord GetItemModifiedAppearance()
        {
            return Global.DB2Mgr.GetItemModifiedAppearance(GetEntry(), _bonusData.AppearanceModID);
        }

        public int GetModifier(ItemModifier modifier)
        {
            int modifierIndex = m_itemData.Modifiers._value.Values.FindIndexIf(mod =>
            {
                return mod.Type == (byte)modifier;
            });

            if (modifierIndex != -1)
                return m_itemData.Modifiers._value.Values[modifierIndex].Value;

            return 0;
        }

        public void SetModifier(ItemModifier modifier, int value)
        {
            int modifierIndex = m_itemData.Modifiers._value.Values.FindIndexIf(mod =>
            {
                return mod.Type == (byte)modifier;
            });

            if (value != 0)
            {
                if (modifierIndex == -1)
                {
                    ItemMod mod = new();
                    mod.Value = value;
                    mod.Type = (byte)modifier;

                    AddDynamicUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Modifiers)._value.ModifyValue(m_itemData.Modifiers._value.Values), mod);
                }
                else
                {
                    ItemModList itemModList = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Modifiers);
                    itemModList.ModifyValue(itemModList.Values, modifierIndex);
                    SetUpdateFieldValue(ref itemModList.ModifyValue(itemModList.Values, modifierIndex).GetValue().Value, value);
                }
            }
            else
            {
                if (modifierIndex == -1)
                    return;

                RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Modifiers)._value.ModifyValue(m_itemData.Modifiers._value.Values), modifierIndex);
            }
        }

        public int GetVisibleEntry(Player owner)
        {
            int itemModifiedAppearanceId = GetModifier(ItemConst.AppearanceModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (itemModifiedAppearanceId == 0)
                itemModifiedAppearanceId = GetModifier(ItemModifier.TransmogAppearanceAllSpecs);

            ItemModifiedAppearanceRecord transmog = CliDB.ItemModifiedAppearanceStorage.LookupByKey(itemModifiedAppearanceId);
            if (transmog != null)
                return transmog.ItemID;

            return GetEntry();
        }

        public ushort GetVisibleAppearanceModId(Player owner)
        {
            int itemModifiedAppearanceId = GetModifier(ItemConst.AppearanceModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (itemModifiedAppearanceId == 0)
                itemModifiedAppearanceId = GetModifier(ItemModifier.TransmogAppearanceAllSpecs);

            ItemModifiedAppearanceRecord transmog = CliDB.ItemModifiedAppearanceStorage.LookupByKey(itemModifiedAppearanceId);
            if (transmog != null)
                return (ushort)transmog.ItemAppearanceModifierID;

            return (ushort)GetAppearanceModId();
        }

        int GetVisibleModifiedAppearanceId(Player owner)
        {
            int itemModifiedAppearanceId = GetModifier(ItemConst.AppearanceModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (itemModifiedAppearanceId == 0)
                itemModifiedAppearanceId = GetModifier(ItemModifier.TransmogAppearanceAllSpecs);

            if (itemModifiedAppearanceId == 0)
            {
                var itemModifiedAppearance = GetItemModifiedAppearance();
                if (itemModifiedAppearance != null)
                    itemModifiedAppearanceId = itemModifiedAppearance.Id;
            }

            return itemModifiedAppearanceId;
        }
        
        public int GetVisibleSecondaryModifiedAppearanceId(Player owner)
        {
            int itemModifiedAppearanceId = GetModifier(ItemConst.SecondaryAppearanceModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (itemModifiedAppearanceId == 0)
                itemModifiedAppearanceId = GetModifier(ItemModifier.TransmogSecondaryAppearanceAllSpecs);

            return itemModifiedAppearanceId;
        }

        public int GetVisibleEnchantmentId(Player owner)
        {
            int enchantmentId = GetModifier(ItemConst.IllusionModifierSlotBySpec[owner.GetActiveTalentGroup()]);
            if (enchantmentId == 0)
                enchantmentId = GetModifier(ItemModifier.EnchantIllusionAllSpecs);

            if (enchantmentId == 0)
                enchantmentId = GetEnchantmentId(EnchantmentSlot.EnhancementPermanent);

            return enchantmentId;
        }

        public ushort GetVisibleItemVisual(Player owner)
        {
            SpellItemEnchantmentRecord enchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(GetVisibleEnchantmentId(owner));
            if (enchant != null)
                return enchant.ItemVisual;

            return 0;
        }                

        public ItemContext GetContext() { return (ItemContext)(int)m_itemData.Context; }
        public void SetContext(ItemContext context) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Context), (int)context); }

        public void SetPetitionId(int petitionId)
        {
            ItemEnchantment enchantmentField = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Enchantment, 0);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.ID), petitionId);
        }
        public void SetPetitionNumSignatures(uint signatures)
        {
            ItemEnchantment enchantmentField = m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Enchantment, 0);
            SetUpdateFieldValue(enchantmentField.ModifyValue(enchantmentField.Duration), signatures);
        }

        public void SetFixedLevel(int level)
        {
            if (!_bonusData.HasFixedLevel || GetModifier(ItemModifier.TimewalkerLevel) != 0)
                return;

            if (_bonusData.PlayerLevelToItemLevelCurveId != 0)
            {
                var levels = Global.DB2Mgr.GetContentTuningData(_bonusData.ContentTuningId, 0, true);
                if (levels.HasValue)
                    level = Math.Min(Math.Max((short)level, levels.Value.MinLevel), levels.Value.MaxLevel);

                SetModifier(ItemModifier.TimewalkerLevel, level);
            }
        }

        public int GetRequiredLevel()
        {
            int fixedLevel = GetModifier(ItemModifier.TimewalkerLevel);
            if (_bonusData.RequiredLevelCurve != 0)
                return (int)Global.DB2Mgr.GetCurveValueAt(_bonusData.RequiredLevelCurve, fixedLevel);
            if (_bonusData.RequiredLevelOverride != 0)
                return _bonusData.RequiredLevelOverride;
            if (_bonusData.HasFixedLevel && _bonusData.PlayerLevelToItemLevelCurveId != 0)
                return fixedLevel;
            return _bonusData.RequiredLevel;
        }

        public override string GetDebugInfo()
        {
            return $"{base.GetDebugInfo()}\nOwner: {GetOwnerGUID()} Count: {GetCount()} BagSlot: {InventoryBagSlot} Slot: {InventorySlot} Equipped: {IsEquipped()}";
        }

        public static Item NewItemOrBag(ItemTemplate proto)
        {
            if (proto.GetInventoryType() == InventoryType.Bag)
                return new Bag();

            return new Item();
        }

        public static void AddItemsSetItem(Player player, Item item)
        {
            ItemTemplate proto = item.GetTemplate();
            int setid = proto.GetItemSet();

            ItemSetRecord set = CliDB.ItemSetStorage.LookupByKey(setid);
            if (set == null)
            {
                Log.outError(LogFilter.Sql, "Item set {0} for item (id {1}) not found, mods not applied.", setid, proto.GetId());
                return;
            }

            if (set.RequiredSkill != 0 && player.GetSkillValue((SkillType)set.RequiredSkill) < set.RequiredSkillRank)
                return;

            if (set.SetFlags.HasAnyFlag(ItemSetFlags.LegacyInactive))
                return;

            // Check player level for heirlooms
            if (Global.DB2Mgr.GetHeirloomByItemId(item.GetEntry()) != null)
            {
                if (item.GetBonus().PlayerLevelToItemLevelCurveId != 0)
                {
                    uint maxLevel = (uint)Global.DB2Mgr.GetCurveXAxisRange(item.GetBonus().PlayerLevelToItemLevelCurveId).Item2;

                    var contentTuning = Global.DB2Mgr.GetContentTuningData(item.GetBonus().ContentTuningId, 0, true);
                    if (contentTuning.HasValue)
                        maxLevel = Math.Min(maxLevel, (uint)contentTuning.Value.MaxLevel);

                    if (player.GetLevel() > maxLevel)
                        return;
                }
            }

            ItemSetEffect eff = null;
            for (int x = 0; x < player.ItemSetEff.Count; ++x)
            {
                if (player.ItemSetEff[x]?.ItemSetID == setid)
                {
                    eff = player.ItemSetEff[x];
                    break;
                }
            }

            if (eff == null)
            {
                eff = new ItemSetEffect();
                eff.ItemSetID = setid;

                int x = 0;
                for (; x < player.ItemSetEff.Count; ++x)
                    if (player.ItemSetEff[x] == null)
                        break;

                if (x < player.ItemSetEff.Count)
                    player.ItemSetEff[x] = eff;
                else
                    player.ItemSetEff.Add(eff);
            }

            eff.EquippedItems.Add(item);

            List<ItemSetSpellRecord> itemSetSpells = Global.DB2Mgr.GetItemSetSpells(setid);
            foreach (var itemSetSpell in itemSetSpells)
            {
                //not enough for  spell
                if (itemSetSpell.Threshold > eff.EquippedItems.Count)
                    continue;

                if (eff.SetBonuses.Contains(itemSetSpell))
                    continue;

                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(itemSetSpell.SpellID, Difficulty.None);
                if (spellInfo == null)
                {
                    Log.outError(LogFilter.Player, "WORLD: unknown spell id {0} in items set {1} effects", itemSetSpell.SpellID, setid);
                    continue;
                }

                eff.SetBonuses.Add(itemSetSpell);
                // spell cast only if fit form requirement, in other case will cast at form change
                if (itemSetSpell.ChrSpecID == 0 || (ChrSpecialization)itemSetSpell.ChrSpecID == player.GetPrimarySpecialization())
                    player.ApplyEquipSpell(spellInfo, null, true);
            }
        }

        public static void RemoveItemsSetItem(Player player, Item item)
        {
            int setid = item.GetTemplate().GetItemSet();

            ItemSetRecord set = CliDB.ItemSetStorage.LookupByKey(setid);
            if (set == null)
            {
                Log.outError(LogFilter.Sql, $"Item set {setid} for item {item.GetEntry()} not found, mods not removed.");
                return;
            }

            ItemSetEffect eff = null;
            int setindex = 0;
            for (; setindex < player.ItemSetEff.Count; setindex++)
            {
                if (player.ItemSetEff[setindex] != null && player.ItemSetEff[setindex].ItemSetID == setid)
                {
                    eff = player.ItemSetEff[setindex];
                    break;
                }
            }

            // can be in case now enough skill requirement for set appling but set has been appliend when skill requirement not enough
            if (eff == null)
                return;

            eff.EquippedItems.Remove(item);

            List<ItemSetSpellRecord> itemSetSpells = Global.DB2Mgr.GetItemSetSpells(setid);
            foreach (ItemSetSpellRecord itemSetSpell in itemSetSpells)
            {
                // enough for spell
                if (itemSetSpell.Threshold <= eff.EquippedItems.Count)
                    continue;

                if (!eff.SetBonuses.Contains(itemSetSpell))
                    continue;

                player.ApplyEquipSpell(Global.SpellMgr.GetSpellInfo(itemSetSpell.SpellID, Difficulty.None), null, false);
                eff.SetBonuses.Remove(itemSetSpell);
            }

            if (eff.EquippedItems.Empty())                                    //all items of a set were removed
            {
                Cypher.Assert(eff == player.ItemSetEff[setindex]);
                player.ItemSetEff[setindex] = null;
            }
        }

        public BonusData GetBonus() { return _bonusData; }

        public override ObjectGuid GetOwnerGUID() { return m_itemData.Owner; }
        public void SetOwnerGUID(ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Owner), guid); }
        public ObjectGuid GetContainedIn() { return m_itemData.ContainedIn; }
        public void SetContainedIn(ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.ContainedIn), guid); }
        public ObjectGuid GetCreator() { return m_itemData.Creator; }
        public void SetCreator(ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Creator), guid); }
        public ObjectGuid GetGiftCreator() { return m_itemData.GiftCreator; }
        public void SetGiftCreator(ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.GiftCreator), guid); }

        void SetExpiration(uint expiration) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Expiration), expiration); }

        public ItemBondingType GetBonding() { return _bonusData.Bonding; }
        public void SetBinding(bool val)
        {
            if (val)
                SetItemFlag(ItemFieldFlags.Soulbound);
            else
                RemoveItemFlag(ItemFieldFlags.Soulbound);
        }

        public bool IsSoulBound() { return HasItemFlag(ItemFieldFlags.Soulbound); }
        public bool IsBoundAccountWide() { return GetTemplate().HasFlag(ItemFlags.IsBoundToAccount); }
        public bool IsBattlenetAccountBound() { return GetTemplate().HasFlag(ItemFlags2.BnetAccountTradeOk); }

        public bool HasItemFlag(ItemFieldFlags flag) { return (m_itemData.DynamicFlags & (uint)flag) != 0; }
        public void SetItemFlag(ItemFieldFlags flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.DynamicFlags), (uint)flags); }
        public void RemoveItemFlag(ItemFieldFlags flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.DynamicFlags), (uint)flags); }
        public void ReplaceAllItemFlags(ItemFieldFlags flags) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.DynamicFlags), (uint)flags); }
        public bool HasItemFlag2(ItemFieldFlags2 flag) { return (m_itemData.DynamicFlags2 & (uint)flag) != 0; }
        public void SetItemFlag2(ItemFieldFlags2 flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.DynamicFlags2), (uint)flags); }
        public void RemoveItemFlag2(ItemFieldFlags2 flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.DynamicFlags2), (uint)flags); }
        public void ReplaceAllItemFlags2(ItemFieldFlags2 flags) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.DynamicFlags2), (uint)flags); }

        public Bag ToBag() { return IsBag() ? this as Bag : null; }

        public bool IsRefundable() { return HasItemFlag(ItemFieldFlags.Refundable); }
        public bool IsBOPTradeable() { return HasItemFlag(ItemFieldFlags.BopTradeable); }
        public bool IsWrapped() { return HasItemFlag(ItemFieldFlags.Wrapped); }
        public bool IsLocked() { return !HasItemFlag(ItemFieldFlags.Unlocked); }
        public bool IsBag() { return GetTemplate().GetInventoryType() == InventoryType.Bag; }        
        public bool IsCurrencyToken() { return GetTemplate().IsCurrencyToken(); }
        public bool IsBroken() { return m_itemData.MaxDurability > 0 && m_itemData.Durability == 0; }
        public void SetDurability(int durability) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.Durability), durability); }
        public void SetMaxDurability(int maxDurability) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.MaxDurability), maxDurability); }
        public void SetInTrade(bool b = true) { if (b) mb_in_trade = -1; else mb_in_trade = 0; }
        public void SetInTradeHolded() { if (mb_in_trade == -1) mb_in_trade = 1; }
        public bool IsInTrade() { return mb_in_trade != 0; }
        public bool IsInTradeHolded() { return mb_in_trade == 1; }

        public int GetCount() { return m_itemData.StackCount; }
        public int GetMaxStackCount() { return GetTemplate().GetMaxStackSize(); }

        public byte InventorySlot { get => m_slot; set => m_slot = value; }
        public byte InventoryBagSlot { get => m_container != null ? m_container.InventorySlot : ItemSlot.Null; }
        public ItemPos InventoryPosition { get => new(InventorySlot, InventoryBagSlot); }
        public Bag GetContainer() { return m_container; }
        
        public void SetContainer(Bag container) { m_container = container; }

        bool IsInBag() { return m_container != null; }

        // ItemRandomPropertyId (signed but stored as unsigned)
        public int GetItemRandomPropertyId() { return m_itemData.RandomPropertiesID; }
        public int GetItemSuffixFactor() { return m_itemData.PropertySeed; }

        public int GetEnchantmentId(EnchantmentSlot slot) { return m_itemData.Enchantment[(int)slot].ID.GetValue(); }
        public uint GetEnchantmentDuration(EnchantmentSlot slot) { return m_itemData.Enchantment[(int)slot].Duration; }
        public int GetEnchantmentCharges(EnchantmentSlot slot) { return m_itemData.Enchantment[(int)slot].Charges; }

        public void SetCreatePlayedTime(uint createPlayedTime) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.CreatePlayedTime), createPlayedTime); }

        public string GetText() { return m_text; }
        public void SetText(string text) { m_text = text; }

        public int GetSpellCharges(int index = 0) { return m_itemData.SpellCharges[index]; }
        public void SetSpellCharges(int index, int value) { SetUpdateFieldValue(ref m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.SpellCharges, index), value); }

        public ItemUpdateState GetState() { return uState; }

        public bool IsInUpdateQueue() { return uQueuePos != -1; }
        public int GetQueuePos() { return uQueuePos; }
        public void FSetState(ItemUpdateState state)// forced
        {
            uState = state;
        }

        public override bool HasQuest(int quest_id) { return GetTemplate().GetStartQuest() == quest_id; }
        public override bool HasInvolvedQuest(int quest_id) { return false; }
        public bool IsPotion() { return GetTemplate().IsPotion(); }
        public bool IsVellum() { return GetTemplate().IsVellum(); }
        public bool IsConjuredConsumable() { return GetTemplate().IsConjuredConsumable(); }
        public bool IsRangedWeapon() { return GetTemplate().IsRangedWeapon(); }
        public ItemQuality GetQuality() { return _bonusData.Quality; }

        public SocketType GetSocketType(int index)
        {
            Cypher.Assert(index < ItemConst.MaxGemSockets && index > -1);
            return _bonusData.socketType[index];
        }

        public int GetAppearanceModId() { return m_itemData.ItemAppearanceModID; }
        public void SetAppearanceModId(int appearanceModId) { SetUpdateFieldValue(m_values.ModifyValue(m_itemData).ModifyValue(m_itemData.ItemAppearanceModID), (byte)appearanceModId); }
        public float GetRepairCostMultiplier() { return _bonusData.RepairCostMultiplier; }
        public int GetScalingContentTuningId() { return _bonusData.ContentTuningId; }

        public void SetRefundRecipient(ObjectGuid guid) { m_refundRecipient = guid; }
        public void SetPaidMoney(long money) { m_paidMoney = money; }
        public void SetPaidExtendedCost(int iece) { m_paidExtendedCost = iece; }

        public ObjectGuid GetRefundRecipient() { return m_refundRecipient; }
        public long GetPaidMoney() { return m_paidMoney; }
        public int GetPaidExtendedCost() { return m_paidExtendedCost; }

        public int GetScriptId() { return GetTemplate().ScriptId; }

        public ItemEffectRecord[] GetEffects() { return _bonusData.Effects[0.._bonusData.EffectCount]; }

        public override Loot GetLootForPlayer(Player player) { return loot; }

        //Static
        public static bool ItemCanGoIntoBag(ItemTemplate pProto, ItemTemplate pBagProto)
        {
            if (pProto == null || pBagProto == null)
                return false;

            switch (pBagProto.GetClass())
            {
                case ItemClass.Container:
                    switch (pBagProto.GetSubClass().Container)
                    {
                        case ItemSubClassContainer.Container:
                            return true;
                        case ItemSubClassContainer.SoulContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.SoulShards))
                                return false;
                            return true;
                        case ItemSubClassContainer.HerbContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.Herbs))
                                return false;
                            return true;
                        case ItemSubClassContainer.EnchantingContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.EnchantingSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.MiningContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.MiningSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.EngineeringContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.EngineeringSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.GemContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.Gems))
                                return false;
                            return true;
                        case ItemSubClassContainer.LeatherworkingContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.LeatherworkingSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.InscriptionContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.InscriptionSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.TackleContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.FishingSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.CookingContainer:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.CookingSupp))
                                return false;
                            return true;
                        case ItemSubClassContainer.ReagentContainer:
                            return pProto.IsCraftingReagent();
                        default:
                            return false;
                    }
                //can remove?
                case ItemClass.Quiver:
                    switch (pBagProto.GetSubClass().Quiver)
                    {
                        case ItemSubClassQuiver.Quiver:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.Arrows))
                                return false;
                            return true;
                        case ItemSubClassQuiver.AmmoPouch:
                            if (!pProto.GetBagFamily().HasAnyFlag(BagFamilyMask.Bullets))
                                return false;
                            return true;
                        default:
                            return false;
                    }
            }
            return false;
        }

        public static uint ItemSubClassToDurabilityMultiplierId(ItemClass ItemClass, uint ItemSubClass)
        {
            switch (ItemClass)
            {
                case ItemClass.Weapon: return ItemSubClass;
                case ItemClass.Armor: return ItemSubClass + 21;
            }
            return 0;
        }

        #region Fields
        public ItemData m_itemData;

        public bool m_lootGenerated;
        public Loot loot;
        internal BonusData _bonusData;

        ItemUpdateState uState;
        int m_paidExtendedCost;
        long m_paidMoney;
        ObjectGuid m_refundRecipient;
        byte m_slot;
        Bag m_container;
        int uQueuePos;
        string m_text;
        /// <summary>[-1] - in trade non-holded<br/>[1] - in trade holded<br/>[0] - not in trade</summary>
        int mb_in_trade;
        long m_lastPlayedTimeUpdate;
        List<ObjectGuid> allowedGUIDs = new();
        #endregion

        class ValuesUpdateForPlayerWithMaskSender : IDoWork<Player>
        {
            Item Owner;
            ObjectFieldData ObjectMask = new();
            ItemData ItemMask = new();

            public ValuesUpdateForPlayerWithMaskSender(Item owner)
            {
                Owner = owner;
            }

            public void Invoke(Player player)
            {
                UpdateData udata = new(Owner.GetMapId());

                Owner.BuildValuesUpdateForPlayerWithMask(udata, ObjectMask.GetUpdateMask(), ItemMask.GetUpdateMask(), player);

                udata.BuildPacket(out UpdateObject packet);
                player.SendPacket(packet);
            }
        }
    }

    public class ItemSetEffect
    {
        public int ItemSetID;
        public List<Item> EquippedItems = new();
        public List<ItemSetSpellRecord> SetBonuses = new();
    }

    public class BonusData
    {
        public BonusData(ItemTemplate proto)
        {
            if (proto == null)
                return;

            Quality = proto.GetQuality();
            ItemLevelBonus = 0;
            RequiredLevel = proto.GetBaseRequiredLevel();

            for (int i = 0; i < ItemConst.MaxStats; ++i)
                ItemStatSocketCostMultiplier[i] = proto.GetStatPercentageOfSocket(i);

            for (int i = 0; i < ItemConst.MaxGemSockets; ++i)           
                socketType[i] = proto.GetSocketType(i);

            Bonding = proto.GetBonding();

            AppearanceModID = 0;
            RepairCostMultiplier = 1.0f;
            ContentTuningId = proto.GetScalingStatContentTuning();
            PlayerLevelToItemLevelCurveId = proto.GetPlayerLevelToItemLevelCurveId();
            RelicType = -1;
            HasFixedLevel = false;
            RequiredLevelOverride = 0;

            for (int i = 0; i < Effects.Length; ++i)
            {
                if (i < proto.Effects.Count)
                    Effects[i] = proto.Effects[i];
                else
                    Effects[i] = null;
            }

            CanDisenchant = !proto.HasFlag(ItemFlags.NoDisenchant);
            CanScrap = proto.HasFlag(ItemFlags4.Scrapable);

            _state.SuffixPriority = int.MaxValue;
            _state.AppearanceModPriority = int.MaxValue;
            _state.ScalingStatDistributionPriority = int.MaxValue;
            _state.RequiredLevelCurvePriority = int.MaxValue;
            _state.HasQualityBonus = false;
        }

        public BonusData(ItemInstance itemInstance) : this(Global.ObjectMgr.GetItemTemplate(itemInstance.ItemID)) { }

        public ItemQuality Quality;
        public int ItemLevelBonus;
        public int RequiredLevel;
        public float[] ItemStatSocketCostMultiplier = new float[ItemConst.MaxStats];
        public SocketType[] socketType = new SocketType[ItemConst.MaxGemSockets];
        public ItemBondingType Bonding;
        public int AppearanceModID;
        public float RepairCostMultiplier;
        public int ContentTuningId;
        public int PlayerLevelToItemLevelCurveId;
        public int DisenchantLootId;
        public int RelicType;
        public int RequiredLevelOverride;
        public int Suffix;
        public int RequiredLevelCurve;
        public ItemEffectRecord[] Effects = new ItemEffectRecord[13];
        public int EffectCount;
        public bool CanDisenchant;
        public bool CanScrap;
        public bool HasFixedLevel;
        State _state;

        struct State
        {
            public int SuffixPriority;
            public int AppearanceModPriority;
            public int ScalingStatDistributionPriority;
            public int RequiredLevelCurvePriority;
            public bool HasQualityBonus;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class ItemDynamicFieldGems
    {
        public int ItemId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public ushort[] BonusListIDs = new ushort[16];
        public byte Context;
    }
}
