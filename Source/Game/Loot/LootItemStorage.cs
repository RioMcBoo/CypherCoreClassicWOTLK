// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Framework.Database;
using Game.Entities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Game.Loots
{
    public class LootItemStorage : Singleton<LootItemStorage>
    {
        LootItemStorage() { }

        public void LoadStorageFromDB()
        {
            uint oldMSTime = Time.GetMSTime();
            _lootItemStorage.Clear();
            uint count = 0;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_ITEMCONTAINER_ITEMS);
            using (var result = DB.Characters.Query(stmt))
            {
                if (!result.IsEmpty())
                {
                    do
                    {
                        var key = result.Read<long>(0);
                        if (!_lootItemStorage.ContainsKey(key))
                            _lootItemStorage[key] = new StoredLootContainer(key);

                        StoredLootContainer storedContainer = _lootItemStorage[key];

                        LootItem lootItem = new();
                        lootItem.itemid = result.Read<int>(1);
                        lootItem.count = result.Read<byte>(2);
                        lootItem.LootListId = result.Read<int>(3);
                        lootItem.follow_loot_rules = result.Read<bool>(4);
                        lootItem.freeforall = result.Read<bool>(5);
                        lootItem.is_blocked = result.Read<bool>(6);
                        lootItem.is_counted = result.Read<bool>(7);
                        lootItem.is_underthreshold = result.Read<bool>(8);
                        lootItem.needs_quest = result.Read<bool>(9);
                        lootItem.randomProperties.RandomPropertiesID = result.Read<int>(10);
                        lootItem.randomProperties.RandomPropertiesSeed = result.Read<int>(11);
                        lootItem.context = (ItemContext)result.Read<byte>(12);

                        storedContainer.AddLootItem(lootItem, null);

                        ++count;
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} stored item loots in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
                }
                else
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 stored item loots.");
            }

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_ITEMCONTAINER_MONEY);
            using (var result = DB.Characters.Query(stmt))
            {
                if (!result.IsEmpty())
                {
                    count = 0;
                    do
                    {
                        var key = result.Read<long>(0);
                        if (!_lootItemStorage.ContainsKey(key))
                            _lootItemStorage.TryAdd(key, new StoredLootContainer(key));

                        StoredLootContainer storedContainer = _lootItemStorage[key];
                        storedContainer.AddMoney(result.Read<uint>(1), null);

                        ++count;
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} stored item money in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
                }
                else
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 stored item money");
            }
        }

        public bool LoadStoredLoot(Item item, Player player)
        {
            if (!_lootItemStorage.ContainsKey(item.GetGUID().GetCounter()))
                return false;

            var container = _lootItemStorage[item.GetGUID().GetCounter()];

            Loot loot = new(player.GetMap(), item.GetGUID(), LootType.Item, null);
            loot.gold = container.GetMoney();

            LootTemplate lt = LootStorage.Items.GetLootFor(item.GetEntry());
            if (lt != null)
            {
                foreach (var (id, storedItem) in container.GetLootItems())
                {
                    LootItem li = new();
                    li.itemid = id;
                    li.count = (byte)storedItem.Count;
                    li.LootListId = storedItem.ItemIndex;
                    li.follow_loot_rules = storedItem.FollowRules;
                    li.freeforall = storedItem.FFA;
                    li.is_blocked = storedItem.Blocked;
                    li.is_counted = storedItem.Counted;
                    li.is_underthreshold = storedItem.UnderThreshold;
                    li.needs_quest = storedItem.NeedsQuest;
                    li.randomProperties = storedItem.RandomProperties;
                    li.context = storedItem.Context;

                    // Copy the extra loot conditions from the item in the loot template
                    lt.CopyConditions(li);

                    // If container item is in a bag, add that player as an allowed looter
                    if (item.InventoryBagSlot != 0)
                        li.AddAllowedLooter(player);

                    // Finally add the LootItem to the container
                    loot.items.Add(li);

                    // Increment unlooted count
                    ++loot.unlootedCount;
                }
            }

            // Mark the item if it has loot so it won't be generated again on open
            item.loot = loot;
            item.m_lootGenerated = true;
            return true;
        }

        public void RemoveStoredMoneyForContainer(long containerId)
        {
            if (!_lootItemStorage.ContainsKey(containerId))
                return;

            _lootItemStorage[containerId].RemoveMoney();
        }

        public void RemoveStoredLootForContainer(long containerId)
        {
            _lootItemStorage.TryRemove(containerId, out _);

            SQLTransaction trans = new();
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEMCONTAINER_ITEMS);
            stmt.AddValue(0, containerId);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEMCONTAINER_MONEY);
            stmt.AddValue(0, containerId);
            trans.Append(stmt);

            DB.Characters.CommitTransaction(trans);
        }

        public void RemoveStoredLootItemForContainer(long containerId, int itemId, int count, int itemIndex)
        {
            if (!_lootItemStorage.ContainsKey(containerId))
                return;

            _lootItemStorage[containerId].RemoveItem(itemId, count, itemIndex);
        }

        public void AddNewStoredLoot(long containerId, Loot loot, Player player)
        {
            // Saves the money and item loot associated with an openable item to the DB
            if (loot.IsLooted()) // no money and no loot
                return;
            
            if (_lootItemStorage.ContainsKey(containerId))
            {
                Log.outError(LogFilter.Misc, $"Trying to store item loot by player: {player.GetGUID()} for container id: {containerId} that is already in storage!");
                return;
            }

            StoredLootContainer container = new(containerId);

            SQLTransaction trans = new();
            if (loot.gold != 0)
                container.AddMoney(loot.gold, trans);

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEMCONTAINER_ITEMS);
            stmt.AddValue(0, containerId);
            trans.Append(stmt);

            foreach (LootItem li in loot.items)
            {
                // Conditions are not checked when loot is generated, it is checked when loot is sent to a player.
                // For items that are lootable, loot is saved to the DB immediately, that means that loot can be
                // saved to the DB that the player never should have gotten. This check prevents that, so that only
                // items that the player should get in loot are in the DB.
                // IE: Horde items are not saved to the DB for Ally players.
                if (!li.AllowedForPlayer(player, loot))
                    continue;

                // Don't save currency tokens
                ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(li.itemid);
                if (itemTemplate == null || itemTemplate.IsCurrencyToken())
                    continue;

                container.AddLootItem(li, trans);
            }

            DB.Characters.CommitTransaction(trans);

            _lootItemStorage.TryAdd(containerId, container);
        }

        ConcurrentDictionary<long, StoredLootContainer> _lootItemStorage = new();
    }

    class StoredLootContainer
    {
        public StoredLootContainer(long containerId)
        {
            _containerId = containerId;
        }

        public void AddLootItem(LootItem lootItem, SQLTransaction trans)
        {
            _lootItems.Add(lootItem.itemid, new StoredLootItem(lootItem));
            if (trans == null)
                return;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_ITEMCONTAINER_ITEMS);
            //   0              1       2           3               4       5       6       7           8               9               10                      11               12
            //container_id, item_id, item_count, item_index, follow_rules, ffa, blocked, counted, under_threshold, needs_quest, random_properties_id, random_properties_seed, context            
            stmt.AddValue(0, _containerId);
            stmt.AddValue(1, lootItem.itemid);
            stmt.AddValue(2, lootItem.count);
            stmt.AddValue(3, lootItem.LootListId);
            stmt.AddValue(4, lootItem.follow_loot_rules);
            stmt.AddValue(5, lootItem.freeforall);
            stmt.AddValue(6, lootItem.is_blocked);
            stmt.AddValue(7, lootItem.is_counted);
            stmt.AddValue(8, lootItem.is_underthreshold);
            stmt.AddValue(9, lootItem.needs_quest);
            stmt.AddValue(10, lootItem.randomProperties.RandomPropertiesID);
            stmt.AddValue(11, lootItem.randomProperties.RandomPropertiesSeed);
            stmt.AddValue(12, (byte)lootItem.context);
            trans.Append(stmt);
        }

        public void AddMoney(uint money, SQLTransaction trans)
        {
            _money = money;
            if (trans == null)
                return;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEMCONTAINER_MONEY);
            stmt.AddValue(0, _containerId);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_ITEMCONTAINER_MONEY);
            stmt.AddValue(0, _containerId);
            stmt.AddValue(1, _money);
            trans.Append(stmt);
        }

        public void RemoveMoney()
        {
            _money = 0;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEMCONTAINER_MONEY);
            stmt.AddValue(0, _containerId);
            DB.Characters.Execute(stmt);
        }

        public void RemoveItem(int itemId, int count, int itemIndex)
        {
            var bounds = _lootItems.LookupByKey(itemId);
            foreach (var itr in bounds)
            {
                if (itr.Count == count)
                {
                    _lootItems.Remove(itr.ItemId);
                    break;
                }
            }

            // Deletes a single item associated with an openable item from the DB
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEMCONTAINER_ITEM);
            stmt.AddValue(0, _containerId);
            stmt.AddValue(1, itemId);
            stmt.AddValue(2, count);
            stmt.AddValue(3, itemIndex);
            DB.Characters.Execute(stmt);
        }

        long GetContainer() { return _containerId; }

        public uint GetMoney() { return _money; }

        public MultiMap<int, StoredLootItem> GetLootItems() { return _lootItems; }

        MultiMap<int, StoredLootItem> _lootItems = new();
        long _containerId;
        uint _money;
    }

    class StoredLootItem
    {
        public StoredLootItem(LootItem lootItem)
        {
            ItemId = lootItem.itemid;
            Count = lootItem.count;
            ItemIndex = lootItem.LootListId;
            FollowRules = lootItem.follow_loot_rules;
            FFA = lootItem.freeforall;
            Blocked = lootItem.is_blocked;
            Counted = lootItem.is_counted;
            UnderThreshold = lootItem.is_underthreshold;
            NeedsQuest = lootItem.needs_quest;
            RandomProperties = lootItem.randomProperties;
            Context = lootItem.context;
        }

        public int ItemId;
        public int Count;
        public int ItemIndex;
        public bool FollowRules;
        public bool FFA;
        public bool Blocked;
        public bool Counted;
        public bool UnderThreshold;
        public bool NeedsQuest;
        public ItemRandomProperties RandomProperties;
        public ItemContext Context;
    }
}
