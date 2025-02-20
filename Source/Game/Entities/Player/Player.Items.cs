﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.BattleGrounds;
using Game.DataStorage;
using Game.Guilds;
using Game.Loots;
using Game.Mails;
using Game.Maps;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Entities
{
    public partial class Player
    {
        //Refund
        void AddRefundReference(ObjectGuid it)
        {
            m_refundableItems.Add(it);
        }

        public void DeleteRefundReference(ObjectGuid it)
        {
            m_refundableItems.Remove(it);
        }

        public void RefundItem(Item item)
        {
            if (!item.IsRefundable())
            {
                Log.outDebug(LogFilter.Player, "Item refund: item not refundable!");
                return;
            }

            if (item.IsRefundExpired())    // item refund has expired
            {
                item.SetNotRefundable(this);
                SendItemRefundResult(item, null, 10);
                return;
            }

            if (GetGUID() != item.GetRefundRecipient()) // Formerly refundable item got traded
            {
                Log.outDebug(LogFilter.Player, "Item refund: item was traded!");
                item.SetNotRefundable(this);
                return;
            }

            ItemExtendedCostRecord iece = CliDB.ItemExtendedCostStorage.LookupByKey(item.GetPaidExtendedCost());
            if (iece == null)
            {
                Log.outDebug(LogFilter.Player, "Item refund: cannot find extendedcost data.");
                return;
            }

            bool store_error = false;
            for (byte i = 0; i < ItemConst.MaxItemExtCostItems; ++i)
            {
                int count = iece.ItemCount[i];
                int itemid = iece.ItemID[i];

                if (count != 0 && itemid != 0)
                {
                    InventoryResult msg = CanStoreNewItem(ItemPos.Undefined, out List<ItemPosCount> dest, itemid, count);
                    if (msg != InventoryResult.Ok)
                    {
                        store_error = true;
                        break;
                    }
                }
            }

            if (store_error)
            {
                SendItemRefundResult(item, iece, 10);
                return;
            }

            SendItemRefundResult(item, iece, 0);

            var moneyRefund = item.GetPaidMoney();  // item. will be invalidated in DestroyItem

            // Save all relevant data to DB to prevent desynchronisation exploits
            SQLTransaction trans = new();

            // Delete any references to the refund data
            item.SetNotRefundable(this, true, trans, false);
            GetSession().GetCollectionMgr().RemoveTemporaryAppearance(item);

            // Destroy item
            DestroyItem(item.InventoryPosition, true);

            // Grant back extendedcost items
            for (byte i = 0; i < ItemConst.MaxItemExtCostItems; ++i)
            {
                int count = iece.ItemCount[i];
                int itemid = iece.ItemID[i];
                if (count != 0 && itemid != 0)
                {
                    InventoryResult msg = CanStoreNewItem(ItemPos.Undefined, out var dest, itemid, count);
                    Cypher.Assert(msg == InventoryResult.Ok); // Already checked before
                    Item it = StoreNewItem(dest, itemid, true);
                    SendNewItem(it, count, true, false, true);
                }
            }

            // Grant back currencies
            for (byte i = 0; i < ItemConst.MaxItemExtCostCurrencies; ++i)
            {
                if (iece.Flags.HasAnyFlag((byte)((int)ItemExtendedCostFlags.RequireSeasonEarned1 << i)))
                    continue;

                int count = iece.CurrencyCount[i];
                int currencyid = iece.CurrencyID[i];
                if (count != 0 && currencyid != 0)
                    AddCurrency(currencyid, count, CurrencyGainSource.ItemRefund);
            }

            // Grant back money
            if (moneyRefund != 0)
                ModifyMoney(moneyRefund); // Saved in SaveInventoryAndGoldToDB

            SaveInventoryAndGoldToDB(trans);

            DB.Characters.CommitTransaction(trans);
        }

        public void SendRefundInfo(Item item)
        {
            // This function call unsets ITEM_FLAGS_REFUNDABLE if played time is over 2 hours.
            item.UpdatePlayedTime(this);

            if (!item.IsRefundable())
            {
                Log.outDebug(LogFilter.Player, "Item refund: item not refundable!");
                return;
            }

            if (GetGUID() != item.GetRefundRecipient()) // Formerly refundable item got traded
            {
                Log.outDebug(LogFilter.Player, "Item refund: item was traded!");
                item.SetNotRefundable(this);
                return;
            }

            ItemExtendedCostRecord iece = CliDB.ItemExtendedCostStorage.LookupByKey(item.GetPaidExtendedCost());
            if (iece == null)
            {
                Log.outDebug(LogFilter.Player, "Item refund: cannot find extendedcost data.");
                return;
            }
            SetItemPurchaseData setItemPurchaseData = new();
            setItemPurchaseData.ItemGUID = item.GetGUID();
            setItemPurchaseData.PurchaseTime = GetTotalPlayedTime() - item.GetPlayedTime();
            setItemPurchaseData.Contents.Money = item.GetPaidMoney();

            for (byte i = 0; i < ItemConst.MaxItemExtCostItems; ++i)                             // item cost data
            {
                setItemPurchaseData.Contents.Items[i].ItemCount = iece.ItemCount[i];
                setItemPurchaseData.Contents.Items[i].ItemID = iece.ItemID[i];
            }

            for (byte i = 0; i < ItemConst.MaxItemExtCostCurrencies; ++i)                       // currency cost data
            {
                if (iece.Flags.HasAnyFlag((byte)((int)ItemExtendedCostFlags.RequireSeasonEarned1 << i)))
                    continue;

                setItemPurchaseData.Contents.Currencies[i].CurrencyCount = iece.CurrencyCount[i];
                setItemPurchaseData.Contents.Currencies[i].CurrencyID = iece.CurrencyID[i];
            }

            SendPacket(setItemPurchaseData);
        }

        public void SendItemRefundResult(Item item, ItemExtendedCostRecord iece, byte error)
        {
            ItemPurchaseRefundResult itemPurchaseRefundResult = new();
            itemPurchaseRefundResult.ItemGUID = item.GetGUID();
            itemPurchaseRefundResult.Result = error;
            if (error == 0)
            {
                itemPurchaseRefundResult.Contents = new();
                itemPurchaseRefundResult.Contents.Money = item.GetPaidMoney();
                for (byte i = 0; i < ItemConst.MaxItemExtCostItems; ++i) // item cost data
                {
                    itemPurchaseRefundResult.Contents.Items[i].ItemCount = iece.ItemCount[i];
                    itemPurchaseRefundResult.Contents.Items[i].ItemID = iece.ItemID[i];
                }

                for (byte i = 0; i < ItemConst.MaxItemExtCostCurrencies; ++i) // currency cost data
                {
                    if (iece.Flags.HasAnyFlag((byte)((uint)ItemExtendedCostFlags.RequireSeasonEarned1 << i)))
                        continue;

                    itemPurchaseRefundResult.Contents.Currencies[i].CurrencyCount = iece.CurrencyCount[i];
                    itemPurchaseRefundResult.Contents.Currencies[i].CurrencyID = iece.CurrencyID[i];
                }
            }

            SendPacket(itemPurchaseRefundResult);
        }

        //Trade 
        void AddTradeableItem(Item item)
        {
            m_itemSoulboundTradeable.Add(item.GetGUID());
        }

        public void RemoveTradeableItem(Item item)
        {
            m_itemSoulboundTradeable.Remove(item.GetGUID());
        }

        void UpdateSoulboundTradeItems()
        {
            // also checks for garbage data
            foreach (var guid in m_itemSoulboundTradeable.ToList())
            {
                Item item = GetItemByGuid(guid);
                if (item == null || item.GetOwnerGUID() != GetGUID() || item.CheckSoulboundTradeExpire())
                    m_itemSoulboundTradeable.Remove(guid);
            }
        }

        public void SetTradeData(TradeData data) { m_trade = data; }

        public Player GetTrader() { return m_trade?.GetTrader(); }

        public TradeData GetTradeData() { return m_trade; }

        public void TradeCancel(bool sendback, TradeStatus status = TradeStatus.Cancelled)
        {
            if (m_trade != null)
            {
                Player trader = m_trade.GetTrader();

                if (sendback)
                    GetSession().SendCancelTrade(status);

                trader.GetSession().SendCancelTrade(status);

                // cleanup
                m_trade = null;
                trader.m_trade = null;
            }
        }

        //Durability
        public void DurabilityLossAll(double percent, bool inventory)
        {
            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; i++)
            {
                Item pItem = GetItemByPos(i);
                if (pItem != null)
                    DurabilityLoss(pItem, percent);
            }

            if (inventory)
            {
                int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
                for (byte i = InventorySlots.ItemStart; i < inventoryEnd; i++)
                {
                    Item pItem = GetItemByPos(i);
                    if (pItem != null)
                        DurabilityLoss(pItem, percent);
                }

                for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; i++)
                {
                    Bag pBag = GetBagByPos(i);
                    if (pBag != null)
                    {
                        for (byte j = 0; j < pBag.GetBagSize(); j++)
                        {
                            Item pItem = GetItemByPos(new(j, i));
                            if (pItem != null)
                                DurabilityLoss(pItem, percent);
                        }
                    }
                }
            }
        }

        public void DurabilityLoss(Item item, double percent)
        {
            if (item == null)
                return;

            var pMaxDurability = item.m_itemData.MaxDurability;

            if (pMaxDurability == 0)
                return;

            percent /= GetTotalAuraMultiplier(AuraType.ModDurabilityLoss);

            int pDurabilityLoss = (int)(pMaxDurability * percent);

            if (pDurabilityLoss < 1)
                pDurabilityLoss = 1;

            DurabilityPointsLoss(item, pDurabilityLoss);
        }

        public void DurabilityPointsLossAll(int points, bool inventory)
        {
            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; i++)
            {
                Item pItem = GetItemByPos(i);
                if (pItem != null)
                    DurabilityPointsLoss(pItem, points);
            }

            if (inventory)
            {
                int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
                for (byte i = InventorySlots.ItemStart; i < inventoryEnd; i++)
                {
                    Item pItem = GetItemByPos(i);
                    if (pItem != null)
                        DurabilityPointsLoss(pItem, points);
                }

                for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; i++)
                {
                    Bag pBag = (Bag)GetItemByPos(i);
                    if (pBag != null)
                        for (byte j = 0; j < pBag.GetBagSize(); j++)
                        {
                            Item pItem = GetItemByPos(new(j, i));
                            if (pItem != null)
                                DurabilityPointsLoss(pItem, points);
                        }
                }
            }
        }

        public void DurabilityPointsLoss(Item item, int points)
        {
            if (HasAuraType(AuraType.PreventDurabilityLoss))
                return;

            var pMaxDurability = item.m_itemData.MaxDurability;
            var pOldDurability = item.m_itemData.Durability;
            var pNewDurability = pOldDurability - points;

            if (pNewDurability < 0)
                pNewDurability = 0;
            else if (pNewDurability > pMaxDurability)
                pNewDurability = pMaxDurability;

            if (pOldDurability != pNewDurability)
            {
                // modify item stats _before_ Durability set to 0 to pass _ApplyItemMods internal check
                if (pNewDurability == 0 && pOldDurability > 0 && item.IsEquipped())
                    _ApplyItemMods(item, item.InventorySlot, false);

                item.SetDurability(pNewDurability);

                // modify item stats _after_ restore durability to pass _ApplyItemMods internal check
                if (pNewDurability > 0 && pOldDurability == 0 && item.IsEquipped())
                    _ApplyItemMods(item, item.InventorySlot, true);

                item.SetState(ItemUpdateState.Changed, this);
            }
        }

        public void DurabilityPointLossForEquipSlot(byte slot)
        {
            if (HasAuraType(AuraType.PreventDurabilityLossFromCombat))
                return;

            Item pItem = GetItemByPos(slot);
            if (pItem != null)
                DurabilityPointsLoss(pItem, 1);
        }

        public void DurabilityRepairAll(bool takeCost, float discountMod, bool guildBank)
        {
            // Collecting all items that can be repaired and repair costs
            List<(Item item, long cost)> itemRepairCostStore = new();

            // equipped, backpack, bags itself
            int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
            for (byte i = EquipmentSlot.Start; i < inventoryEnd; i++)
            {
                Item item = GetItemByPos(i);
                if (item != null)
                {
                    long cost = item.CalculateDurabilityRepairCost(discountMod);
                    if (cost != 0)
                        itemRepairCostStore.Add((item, cost));
                }
            }

            // items in inventory bags
            for (byte j = InventorySlots.BagStart; j < InventorySlots.BagEnd; j++)
            {
                for (byte i = 0; i < ItemConst.MaxBagSize; i++)
                {
                    Item item = GetItemByPos(i);
                    if (item != null)
                    {
                        long cost = item.CalculateDurabilityRepairCost(discountMod);
                        if (cost != 0)
                            itemRepairCostStore.Add((item, cost));
                    }
                }
            }

            // Handling a free repair case - just repair every item without taking cost.
            if (!takeCost)
            {
                foreach (var (item, _) in itemRepairCostStore)
                    DurabilityRepair(item.InventoryPosition, false, 0.0f);
                return;
            }

            if (guildBank)
            {
                // Handling a repair for guild money case.
                // We have to repair items one by one until the guild bank has enough money available for withdrawal or until all items are repaired.

                Guild guild = GetGuild();
                if (guild == null)
                    return; // silent return, client shouldn't display this button for players without guild.

                long availableGuildMoney = guild.GetMemberAvailableMoneyForRepairItems(GetGUID());
                if (availableGuildMoney == 0)
                    return;

                // Sort the items by repair cost from lowest to highest
                itemRepairCostStore.OrderByDescending(a => a.cost);

                // We must calculate total repair cost and take money once to avoid spam in the guild bank log and reduce number of transactions in the database
                long totalCost = 0;

                foreach (var (item, cost) in itemRepairCostStore)
                {
                    long newTotalCost = totalCost + cost;
                    if (newTotalCost > availableGuildMoney || newTotalCost > PlayerConst.MaxMoneyAmount)
                        break;

                    totalCost = newTotalCost;
                    // Repair item without taking cost. We'll do it later.
                    DurabilityRepair(item.InventoryPosition, false, 0.0f);
                }
                // Take money for repairs from the guild bank
                guild.HandleMemberWithdrawMoney(GetSession(), totalCost, true);
            }
            else
            {
                // Handling a repair for player's money case.
                // Unlike repairing for guild money, in this case we must first check if player has enough money to repair all the items at once.

                long totalCost = 0;
                foreach (var (_, cost) in itemRepairCostStore)
                    totalCost += cost;

                if (!HasEnoughMoney(totalCost))
                    return; // silent return, client should display error by itself and not send opcode.

                ModifyMoney(-(int)totalCost);

                // Payment for repair has already been taken, so just repair every item without taking cost.
                foreach (var (item, cost) in itemRepairCostStore)
                    DurabilityRepair(item.InventoryPosition, false, 0.0f);
            }
        }

        public void DurabilityRepair(ItemPos pos, bool takeCost, float discountMod)
        {
            Item item = GetItemByPos(pos);
            if (item == null)
                return;


            if (takeCost)
            {
                var cost = item.CalculateDurabilityRepairCost(discountMod);
                if (!HasEnoughMoney(cost))
                {
                    Log.outDebug(LogFilter.PlayerItems,
                        $"Player::DurabilityRepair: Player '{GetName()}' ({GetGUID()}) " +
                        $"has not enough money to repair item");
                    return;
                }

                ModifyMoney(-(int)cost);
            }

            bool isBroken = item.IsBroken();

            item.SetDurability(item.m_itemData.MaxDurability);
            item.SetState(ItemUpdateState.Changed, this);

            // reapply mods for total broken and repaired item if equipped
            if (pos.IsEquipmentPos && isBroken)
                _ApplyItemMods(item, pos.Slot, true);
        }

        //Store Item
        public InventoryResult CanStoreNewItem(ItemPos pos, out List<ItemPosCount> dest, ItemTemplate itemProto, int count, out int no_space_count)
        {
            return CanStoreItem(pos, out dest, itemProto, count, null, out no_space_count);
        }

        public InventoryResult CanStoreNewItem(ItemPos pos, out List<ItemPosCount> dest, int itemID, int count)
        {
            ItemTemplate itemProto = Global.ObjectMgr.GetItemTemplate(itemID);

            if (itemProto == null)
            {
                dest = null;
                return InventoryResult.ItemNotFound;
            }

            return CanStoreNewItem(pos, out dest, itemProto, count, out _);
        }

        public InventoryResult CanStoreItem(ItemPos pos, out List<ItemPosCount> dest, Item pItem, bool forSwap = false)
        {
            return CanStoreItem(pos, out dest, out _, pItem, forSwap ? pos : null);
        }

        public InventoryResult CanStoreItem(ItemPos pos, out List<ItemPosCount> dest, Item pItem, ItemPresetMap presetMap)
        {
            return CanStoreItem(pos, out dest, out _, pItem, presetMap);
        }

        public InventoryResult CanStoreItem(ItemPos pos, out List<ItemPosCount> dest, out int no_space_count, Item pItem, ItemPresetMap presetMap, int? count = null)
        {
            dest = null;
            no_space_count = count.HasValue ? count.Value : pItem.GetCount();

            if (pItem == null)
                return InventoryResult.ItemNotFound;

            ItemTemplate itemTemplate = pItem.GetTemplate();
            if (itemTemplate == null)
            {
                return InventoryResult.ItemNotFound;
            }

            if (pItem.m_lootGenerated)
            {
                return InventoryResult.LootGone;
            }

            if (pItem.IsBindedNotWith(this))
            {
                return InventoryResult.NotOwner;
            }

            return CanStoreItem(pos, out dest, itemTemplate, no_space_count, pItem, out no_space_count, presetMap);
        }

        enum CheckBagSpecilization
        {
            Ignore = -1,
            NonSpecialized = 0,
            Specialized = 1,

        };

        enum MergeOption
        {
            FirstAvaible = -1,
            TryToMerge = 1,
            DontMerge = 0,
        }

        InventoryResult CanStoreItem(ItemPos pos, out List<ItemPosCount> dest, ItemTemplate pProto, int count, Item pItem, out int no_space_count, ItemPresetMap presetMap = null /*bool forSwap = false*/)
        {
            dest = new();
            InventoryResult res;

            Log.outDebug(LogFilter.Player,
                $"STORAGE: CanStoreItem bag = {pos.Container}, slot = {pos.Slot}, item = {pProto.GetId()}, count = {count}");

            #region Check for similar items 
            // can't store this amount similar items
            // The search for similar items occurs in all character slots, including the bank. Therefore, there is no need to use the 'ItemPresetMap' here. We have only these possible options:
            //      1) Moving or swapping an item within the character's inventory(also in the bank) - then this does not matter, because if this one is found, it will be skipped.
            //      2) Receiving an item from outside(by trading or other methods) - this item will not be found anyway)
            //      3) Exchanging similar items when trading, which have a quantity limit (this case is quite rare if there are no custom items, if possible at all)
            InventoryResult TakeMoreSimilarRes = CanTakeMoreSimilarItems(pProto, count, pItem, out no_space_count);
            if (TakeMoreSimilarRes != InventoryResult.Ok)
            {
                if (count == no_space_count)
                    return TakeMoreSimilarRes;

                //We will try store as much as we can.
                count -= no_space_count;
            }
            #endregion

            const byte TryAuto = 0;
            const byte TryInSpecificSlot = 1;
            const byte TryInSpecificContainer = 2;
            byte stage = pos.IsExplicitPos ? TryInSpecificSlot : (pos.IsContainerPos ? TryInSpecificContainer : TryAuto);

            //needs for not specific Positions
            MergeOption mergeOption;

            switch (stage)
            {
                case TryInSpecificSlot:

                    res = CanStoreItem_InSpecificSlot(pos, dest, pProto, ref count, pItem, presetMap);
                    if (res != InventoryResult.Ok)
                    {
                        no_space_count += count;
                        return res;
                    }

                    if (count == 0)
                        return TakeMoreSimilarRes;

                    if (pos.IsContainerPos)
                    {
                        goto case TryInSpecificContainer;
                    }
                    else
                    {
                        goto case TryAuto;
                    }

                case TryInSpecificContainer:

                    var beginCount = count;
                    mergeOption = pProto.IsMergeable ? MergeOption.TryToMerge : MergeOption.DontMerge;

                    for (; mergeOption >= MergeOption.DontMerge; mergeOption--)
                    {
                        // we need check 2 time (ignore specialized/non_specialized)
                        res = CanStoreItem_InBagSlot(pos.Container, dest, pProto, ref count, CheckBagSpecilization.Ignore, pItem, pos.Slot, mergeOption, presetMap);
                        if (res != InventoryResult.Ok)
                        {
                            no_space_count += count;
                            return res;
                        }

                        if (count == 0)
                            return TakeMoreSimilarRes;
                    }

                    // This only happens if no items have been stored.
                    if (beginCount == count && dest.Empty())
                    {
                        no_space_count += count;
                        return InventoryResult.BagFull;
                    }

                    goto case TryAuto;

                case TryAuto:

                    mergeOption = pProto.IsMergeable ? MergeOption.TryToMerge : MergeOption.DontMerge;

                    for (; mergeOption >= MergeOption.DontMerge; mergeOption--)
                    {
                        ///Try Store into suitable slot in Inventory
                        {
                            // new bags can be directly equipped
                            if (pItem == null && pProto.GetClass() == ItemClass.Container && pProto.GetSubClass().Container == ItemSubClassContainer.Container &&
                                (pProto.GetBonding() == ItemBondingType.None || pProto.GetBonding() == ItemBondingType.OnAcquire))
                            {
                                CanStoreItem_InInventorySlots(InventorySlots.BagStart, InventorySlots.BagEnd, dest, pProto, ref count, pItem, pos, mergeOption, presetMap);
                                if (count == 0)
                                    return TakeMoreSimilarRes;
                            }

                            //Let's skip the redundant steps (you can only move non-empty bags to the corresponding slots that have just been checked above)
                            if (pItem != null && pItem.IsNotEmptyBag())
                                continue;

                            // search free slot - keyring case
                            if ((pProto.GetBagFamily() & BagFamilyMask.Keys) != 0)
                            {
                                CanStoreItem_InInventorySlots(InventorySlots.KeyringStart, InventorySlots.KeyringEnd, dest, pProto, ref count, pItem, pos, mergeOption, presetMap);
                                if (count == 0)
                                    return TakeMoreSimilarRes;
                            }

                            // search free slot - ChildEquipment case
                            if (pItem != null && pItem.HasItemFlag(ItemFieldFlags.Child))
                            {
                                CanStoreItem_InInventorySlots(InventorySlots.ChildEquipmentStart, InventorySlots.ChildEquipmentEnd, dest, pProto, ref count, pItem, pos, mergeOption, presetMap);
                                if (count == 0)
                                    return TakeMoreSimilarRes;
                            }

                            //search specialized bag
                            if (pProto.GetBagFamily() != 0)
                            {
                                for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; i++)
                                {
                                    // we need checkonly specialized
                                    CanStoreItem_InBagSlot(i, dest, pProto, ref count, CheckBagSpecilization.Specialized, pItem, pos, mergeOption, presetMap);
                                    if (count == 0)
                                        return TakeMoreSimilarRes;
                                }
                            }
                        }

                        ///Try Store in any free slot in Inventory
                        {
                            //Determining the maximum capacity of a backpack
                            byte inventoryEnd = (byte)(InventorySlots.ItemStart + GetInventorySlotCount());
                            CanStoreItem_InInventorySlots(InventorySlots.ItemStart, inventoryEnd, dest, pProto, ref count, pItem, pos, mergeOption, presetMap);
                            if (count == 0)
                                return TakeMoreSimilarRes;
                        }

                        ///Try Store in any free slot into a Bag
                        {
                            for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; i++)
                            {
                                // we need checkonly non-specialized (Specialized already checked above)
                                CanStoreItem_InBagSlot(i, dest, pProto, ref count, CheckBagSpecilization.NonSpecialized, pItem, pos, mergeOption, presetMap);
                                if (count == 0)
                                    return TakeMoreSimilarRes;
                            }
                        }
                    }

                    no_space_count += count;
                    return InventoryResult.InvFull;
            }

            no_space_count += count;
            return InventoryResult.InternalBagError;
        }

        public InventoryResult CanStoreTradeItems(List<Item> itemsForStore, out int offendingItemId, out List<List<ItemPosCount>> dest, List<Item> ignoreSwapItems)
        {
            offendingItemId = 0;

            dest = new(itemsForStore.Count);

            for (int i = 0; i < itemsForStore.Count; i++)
            {
                Log.outDebug(LogFilter.Player,
                    $"STORAGE: CanStoreTradeItems {i + 1} (empty slots is avoided). item = {itemsForStore[i].GetEntry()}, count = {itemsForStore[i].GetCount()}");

                ItemTemplate pProto = itemsForStore[i].GetTemplate();

                // strange item
                if (pProto == null)
                    return InventoryResult.ItemNotFound;

                // item used
                if (itemsForStore[i].m_lootGenerated)
                    return InventoryResult.LootGone;

                // item it 'bind'
                if (itemsForStore[i].IsBindedNotWith(this))
                    return InventoryResult.NotOwner;
            }

            // Using presetMap to have correct free space diagnostics for several different items
            // We ignore items that will be swapped during the exchange.
            ItemPresetMap swapPreset = new(ignoreSwapItems);

            foreach (var item in itemsForStore)
            {
                InventoryResult res = CanStoreItem(ItemPos.Undefined, out var destination, item.GetTemplate(), item.GetCount(), item, out _, swapPreset);
                dest.Add(destination);

                if (res != InventoryResult.Ok)
                {
                    offendingItemId = item.GetTemplate().GetId();
                    return res;
                }
            }

            return InventoryResult.Ok;
        }

        Item _StoreItem(ItemPos pos, Item pItem, int count, bool clone, bool update)
        {
            if (pItem == null)
                return null;

            Log.outDebug(LogFilter.Player,
                $"STORAGE: StoreItem bag = {pos.Container}, slot = {pos.Slot}, " +
                $"item = {pItem.GetEntry()}, count = {count}, guid = {pItem.GetGUID()}");

            Item pItem2 = GetItemByPos(pos);

            if (pItem2 == null)
            {
                if (clone)
                    pItem = pItem.CloneItem(count, this);
                else
                    pItem.SetCount(count);

                if (pItem == null)
                    return null;

                if (pItem.GetBonding() == ItemBondingType.OnAcquire ||
                    pItem.GetBonding() == ItemBondingType.Quest ||
                    (pItem.GetBonding() == ItemBondingType.OnEquip && pos.IsBagSlotPos))
                    pItem.SetBinding(true);

                Bag pBag = null;

                if (pos.IsContainerPos)
                    pBag = GetBagByPos(pos.Container);

                if (pBag == null)
                {
                    m_items[pos.Slot] = pItem;
                    SetInvSlot(pos.Slot, pItem.GetGUID());
                    pItem.SetContainedIn(GetGUID());
                    pItem.SetOwnerGUID(GetGUID());

                    pItem.InventorySlot = pos.Slot;
                    pItem.SetContainer(null);
                }
                else
                    pBag.StoreItem(pos.Slot, pItem, update);

                if (IsInWorld && update)
                {
                    pItem.AddToWorld();
                    pItem.SendUpdateToPlayer(this);
                }

                pItem.SetState(ItemUpdateState.Changed, this);
                if (pBag != null)
                    pBag.SetState(ItemUpdateState.Changed, this);

                AddEnchantmentDurations(pItem);
                AddItemDurations(pItem);

                if (!pos.IsContainerPos || pos.Container.IsBagSlot)
                    ApplyItemObtainSpells(pItem, true);

                return pItem;
            }
            else
            {
                if (pItem2.GetBonding() == ItemBondingType.OnAcquire ||
                    pItem2.GetBonding() == ItemBondingType.Quest ||
                    (pItem2.GetBonding() == ItemBondingType.OnEquip && pos.IsBagSlotPos))
                    pItem2.SetBinding(true);

                pItem2.SetCount(pItem2.GetCount() + count);
                if (IsInWorld && update)
                    pItem2.SendUpdateToPlayer(this);

                if (!clone)
                {
                    // delete item (it not in any slot currently)
                    if (IsInWorld && update)
                    {
                        pItem.RemoveFromWorld();
                        pItem.DestroyForPlayer(this);
                    }

                    RemoveEnchantmentDurations(pItem);
                    RemoveItemDurations(pItem);

                    pItem.SetOwnerGUID(GetGUID());                 // prevent error at next SetState in case trade/mail/buy from vendor
                    pItem.SetNotRefundable(this);
                    pItem.ClearSoulboundTradeable(this);
                    RemoveTradeableItem(pItem);
                    pItem.SetState(ItemUpdateState.Removed, this);
                }

                AddEnchantmentDurations(pItem2);

                pItem2.SetState(ItemUpdateState.Changed, this);

                if (!pos.IsContainerPos || pos.Container.IsBagSlot)
                    ApplyItemObtainSpells(pItem2, true);

                return pItem2;
            }
        }

        public Item StoreItem(List<ItemPosCount> dest, Item pItem, bool update)
        {
            if (pItem == null)
                return null;

            var lastItem = pItem;
            for (var i = 0; i < dest.Count; i++)
            {
                var itemPosCount = dest[i];

                if (i == dest.Count - 1)
                {
                    lastItem = _StoreItem(itemPosCount.Position, pItem, itemPosCount.Count, false, update);
                    break;
                }

                lastItem = _StoreItem(itemPosCount.Position, pItem, itemPosCount.Count, true, update);
            }

            return lastItem;
        }

        bool StoreNewItemInBestSlots(int itemId, int amount, ItemContext itemContext)
        {
            Log.outDebug(LogFilter.Player,
                $"Player.StoreNewItemInBestSlots:" +
                $" Creating initial item, itemId = {itemId}, count = {amount}.");

            List<ItemPosCount> Dest;
            InventoryResult msg;

            // attempt equip by one
            while (amount > 0)
            {
                msg = CanEquipNewItem(ItemSlot.Null, out Dest, itemId, false);
                if (msg != InventoryResult.Ok)
                    break;

                EquipNewItem(Dest, itemId, itemContext, true);
                AutoUnequipOffhandIfNeed();
                --amount;
            }

            if (amount == 0)
                return true;                                        // equipped

            // attempt store

            // store in main bag to simplify second pass (special bags can be not equipped yet at this moment)
            msg = CanStoreNewItem(ItemPos.Undefined, out Dest, itemId, amount);
            if (msg == InventoryResult.Ok)
            {
                StoreNewItem(Dest, itemId, true, ItemEnchantmentManager.GenerateRandomProperties(itemId), null, itemContext);
                return true;                                        // stored
            }

            // item can't be added
            Log.outError(LogFilter.Player,
                $"Player.StoreNewItemInBestSlots: Player '{GetName()}' ({GetGUID()}) " +
                $"can't equip or store initial item " +
                $"(ItemID: {itemId}, Race: {GetRace()}, Class: {GetClass()}, InventoryResult: {msg}).");
            return false;
        }

        public Item StoreNewItem(List<ItemPosCount> pos, int itemId, bool update, ItemRandomProperties randomProperties = new(), List<ObjectGuid> allowedLooters = null, ItemContext context = 0, bool addToCollection = true)
        {
            var count = 0;
            foreach (var ipc in pos)
                count += ipc.Count;

            // quest objectives must be processed twice - QUEST_OBJECTIVE_FLAG_2_QUEST_BOUND_ITEM prevents item creation
            ItemAddedQuestCheck(itemId, count, true, out bool hadBoundItemObjective);
            if (hadBoundItemObjective)
                return null;

            Item item = Item.CreateItem(itemId, count, context, this);
            if (item != null)
            {
                item.SetItemFlag(ItemFieldFlags.NewItem);

                item = StoreItem(pos, item, update);

                ItemAddedQuestCheck(itemId, count);
                UpdateCriteria(CriteriaType.ObtainAnyItem, itemId, count);
                UpdateCriteria(CriteriaType.AcquireItem, itemId, count);

                item.SetFixedLevel(GetLevel());
                item.SetItemRandomProperties(randomProperties);

                if (allowedLooters != null && allowedLooters.Count > 1 && item.GetTemplate().GetMaxStackSize() == 1 && item.IsSoulBound())
                {
                    item.SetSoulboundTradeable(allowedLooters);
                    item.SetCreatePlayedTime(GetTotalPlayedTime());
                    AddTradeableItem(item);

                    // save data
                    StringBuilder ss = new();
                    foreach (var guid in allowedLooters)
                        ss.AppendFormat($"{guid} ");

                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_ITEM_BOP_TRADE);
                    stmt.SetInt64(0, item.GetGUID().GetCounter());
                    stmt.SetString(1, ss.ToString());
                    DB.Characters.Execute(stmt);
                }

                if (addToCollection)
                    GetSession().GetCollectionMgr().OnItemAdded(item);

                if (item.GetTemplate().GetInventoryType() != InventoryType.NonEquip)
                    UpdateAverageItemLevelTotal();
            }
            return item;
        }

        //Move Item
        InventoryResult CanTakeMoreSimilarItems(Item pItem)
        {
            return CanTakeMoreSimilarItems(pItem.GetTemplate(), pItem.GetCount(), pItem, out _);
        }

        InventoryResult CanTakeMoreSimilarItems(ItemTemplate pProto, int count, Item pItem, out int no_space_count)
        {
            // no maximum
            if ((pProto.GetMaxCount() <= 0 && pProto.GetItemLimitCategory() == 0) || pProto.GetMaxCount() == int.MaxValue)
            {
                no_space_count = 0;
                return InventoryResult.Ok;
            }

            if (pProto.GetMaxCount() > 0)
            {
                var curcount = GetItemCount(pProto.GetId(), true, pItem);
                if (curcount + count > pProto.GetMaxCount())
                {
                    no_space_count = count + curcount - pProto.GetMaxCount();
                    return InventoryResult.ItemMaxCount;
                }
            }

            // check unique-equipped limit
            if (pProto.GetItemLimitCategory() != 0)
            {
                ItemLimitCategoryRecord limitEntry = CliDB.ItemLimitCategoryStorage.LookupByKey(pProto.GetItemLimitCategory());
                if (limitEntry == null)
                {
                    no_space_count = count;
                    return InventoryResult.NotEquippable;
                }

                if (limitEntry.Flags == 0)
                {
                    byte limitQuantity = GetItemLimitCategoryQuantity(limitEntry);
                    var curcount = GetItemCountWithLimitCategory(pProto.GetItemLimitCategory(), pItem);
                    if (curcount + count > limitQuantity)
                    {
                        no_space_count = count + curcount - limitQuantity;
                        return InventoryResult.ItemMaxLimitCategoryCountExceededIs;
                    }
                }
            }

            no_space_count = 0;
            return InventoryResult.Ok;
        }

        //UseItem
        public InventoryResult CanUseItem(Item pItem, bool not_loading = true)
        {
            if (pItem != null)
            {
                Log.outDebug(LogFilter.Player, $"ItemStorage: CanUseItem item = {pItem.GetEntry()}");

                if (!IsAlive() && not_loading)
                    return InventoryResult.PlayerDead;

                ItemTemplate pProto = pItem.GetTemplate();
                if (pProto != null)
                {
                    if (pItem.IsBindedNotWith(this))
                        return InventoryResult.NotOwner;

                    if (GetLevel() < pItem.GetRequiredLevel())
                        return InventoryResult.CantEquipLevelI;

                    InventoryResult res = CanUseItem(pProto);
                    if (res != InventoryResult.Ok)
                        return res;

                    if (pItem.GetSkill() != 0)
                    {
                        bool allowEquip = false;
                        SkillType itemSkill = pItem.GetSkill();
                        // Armor that is binded to account can "morph" from plate to mail, etc. if skill is not learned yet.
                        if (pProto.GetQuality() == ItemQuality.Heirloom && pProto.GetClass() == ItemClass.Armor && !HasSkill(itemSkill))
                        {
                            // TODO: when you right-click already equipped item it throws EQUIP_ERR_PROFICIENCY_NEEDED.

                            // In fact it's a visual bug, everything works properly... I need sniffs of operations with
                            // binded to account items from off server.

                            switch (GetClass())
                            {
                                case Class.Hunter:
                                case Class.Shaman:
                                    allowEquip = (itemSkill == SkillType.Mail);
                                    break;
                                case Class.Paladin:
                                case Class.Warrior:
                                    allowEquip = (itemSkill == SkillType.PlateMail);
                                    break;
                            }
                        }
                        if (!allowEquip && GetSkillValue(itemSkill) == 0)
                            return InventoryResult.ProficiencyNeeded;
                    }

                    return InventoryResult.Ok;
                }
            }
            return InventoryResult.ItemNotFound;
        }

        public InventoryResult CanUseItem(ItemTemplate proto, bool skipRequiredLevelCheck = false)
        {
            // Used by group, function GroupLoot, to know if a prototype can be used by a player

            if (proto == null)
                return InventoryResult.ItemNotFound;

            if (proto.HasFlag(ItemFlags2.InternalItem))
                return InventoryResult.CantEquipEver;

            if (proto.HasFlag(ItemFlags2.FactionHorde) && GetTeam() != Team.Horde)
                return InventoryResult.CantEquipEver;

            if (proto.HasFlag(ItemFlags2.FactionAlliance) && GetTeam() != Team.Alliance)
                return InventoryResult.CantEquipEver;

            if (!proto.GetAllowableClass().HasClass(GetClass()) || !proto.GetAllowableRace().HasRace(GetRace()))
                return InventoryResult.CantEquipEver;

            if (proto.GetRequiredSkill() != 0)
            {
                if (GetSkillValue(proto.GetRequiredSkill()) == 0)
                    return InventoryResult.ProficiencyNeeded;
                else if (GetSkillValue(proto.GetRequiredSkill()) < proto.GetRequiredSkillRank())
                    return InventoryResult.CantEquipSkill;
            }

            if (proto.GetRequiredSpell() != 0 && !HasSpell(proto.GetRequiredSpell()))
                return InventoryResult.ProficiencyNeeded;

            if (!skipRequiredLevelCheck && GetLevel() < proto.GetBaseRequiredLevel())
                return InventoryResult.CantEquipLevelI;

            // If World Event is not active, prevent using event dependant items
            if (proto.GetHolidayID() != 0 && !Global.GameEventMgr.IsHolidayActive(proto.GetHolidayID()))
                return InventoryResult.ClientLockedOut;

            if (proto.GetRequiredReputationFaction() != 0 && GetReputationRank(proto.GetRequiredReputationFaction()) < proto.GetRequiredReputationRank())
                return InventoryResult.CantEquipReputation;

            // learning (recipes, mounts, pets, etc.)
            if (proto.Effects.Count >= 2)
            {
                if (proto.Effects[0].SpellID == 483 || proto.Effects[0].SpellID == 55884)
                {
                    if (HasSpell(proto.Effects[1].SpellID))
                        return InventoryResult.InternalBagError;
                }
            }

            ArtifactRecord artifact = CliDB.ArtifactStorage.LookupByKey(proto.GetArtifactID());
            if (artifact != null)
                if ((ChrSpecialization)artifact.ChrSpecializationID != GetPrimarySpecialization())
                    return InventoryResult.CantUseItem;

            return InventoryResult.Ok;
        }

        //Equip/Unequip Item
        InventoryResult CanUnequipItems(int item, int count)
        {
            InventoryResult res = InventoryResult.Ok;

            int tempcount = 0;
            bool result = ForEachItem(ItemSearchLocation.Equipment, pItem =>
            {
                if (pItem.GetEntry() == item)
                {
                    InventoryResult ires = CanUnequipItem(pItem.InventoryPosition, false);
                    if (ires == InventoryResult.Ok)
                    {
                        tempcount += pItem.GetCount();
                        if (tempcount >= count)
                            return false;
                    }
                    else
                        res = ires;
                }
                return true;
            });

            if (!result) // we stopped early due to a sucess
                return InventoryResult.Ok;

            return res; // return latest error if any
        }

        Item EquipNewItem(List<ItemPosCount> pos, int item, ItemContext context, bool update)
        {
            return EquipNewItem(pos.FirstOrDefault().Position, item, context, update);
        }

        Item EquipNewItem(ItemPos pos, int item, ItemContext context, bool update)
        {
            Item pItem = Item.CreateItem(item, 1, context, this);
            if (pItem != null)
            {
                UpdateCriteria(CriteriaType.ObtainAnyItem, item, 1);
                Item equippedItem = EquipItem(pos, pItem, update);
                ItemAddedQuestCheck(item, 1);
                return equippedItem;
            }

            return null;
        }

        public Item EquipItem(List<ItemPosCount> posList, Item pItem, bool update)
        {
           if (posList == null || posList.Empty())
                return EquipItem(ItemPos.Undefined, pItem, update);

            return EquipItem(posList[0].Position, pItem, update);
        }

        public Item EquipItem(ItemPos pos, Item pItem, bool update)
        {
            if (pItem == null)
                return pItem;

            AddEnchantmentDurations(pItem);
            AddItemDurations(pItem);

            Item pItem2 = GetItemByPos(pos);

            if (pItem2 == null)
            {
                VisualizeItem(pos.Slot, pItem);

                if (IsAlive())
                {
                    ItemTemplate pProto = pItem.GetTemplate();

                    // item set bonuses applied only at equip and removed at unequip, and still active for broken items
                    if (pProto != null && pProto.GetItemSet() != 0)
                        Item.AddItemsSetItem(this, pItem);

                    _ApplyItemMods(pItem, pos.Slot, true);

                    if (pProto != null && IsInCombat() &&
                        (pProto.GetClass() == ItemClass.Weapon || pProto.GetInventoryType() == InventoryType.Relic)
                        && m_weaponChangeTimer == 0)
                    {
                        var cooldownSpell = GetClass() == Class.Rogue ? 6123 : 6119;
                        var spellProto = Global.SpellMgr.GetSpellInfo(cooldownSpell, Difficulty.None);

                        if (spellProto == null)
                        {
                            Log.outError(LogFilter.Player,
                                $"Weapon switch cooldown spell {cooldownSpell} couldn't be found in Spell.dbc");
                        }
                        else
                        {
                            m_weaponChangeTimer = spellProto.StartRecoveryTime;

                            GetSpellHistory().AddGlobalCooldown(spellProto, m_weaponChangeTimer);

                            SpellCooldownPkt spellCooldown = new();
                            spellCooldown.Caster = GetGUID();
                            spellCooldown.Flags = SpellCooldownFlags.IncludeGCD;
                            spellCooldown.SpellCooldowns.Add(new SpellCooldownStruct(cooldownSpell, default));
                            SendPacket(spellCooldown);
                        }
                    }
                }

                pItem.SetItemFlag2(ItemFieldFlags2.Equipped);

                if (IsInWorld && update)
                {
                    pItem.AddToWorld();
                    pItem.SendUpdateToPlayer(this);
                }

                ApplyEquipCooldown(pItem);

                // update expertise and armor penetration - passive auras may need it

                if (pos.Slot == EquipmentSlot.MainHand)
                    UpdateExpertise(WeaponAttackType.BaseAttack);
                else if (pos.Slot == EquipmentSlot.OffHand)
                    UpdateExpertise(WeaponAttackType.OffAttack);

                switch (pos.Slot)
                {
                    case EquipmentSlot.MainHand:
                    case EquipmentSlot.OffHand:
                        RecalculateRating(CombatRating.ArmorPenetration);
                        break;
                }
            }
            else
            {
                pItem2.SetCount(pItem2.GetCount() + pItem.GetCount());
                if (IsInWorld && update)
                    pItem2.SendUpdateToPlayer(this);

                if (IsInWorld && update)
                {
                    pItem.RemoveFromWorld();
                    pItem.DestroyForPlayer(this);
                }

                RemoveEnchantmentDurations(pItem);
                RemoveItemDurations(pItem);

                pItem.SetOwnerGUID(GetGUID());                     // prevent error at next SetState in case trade/mail/buy from vendor
                pItem.SetNotRefundable(this);
                pItem.ClearSoulboundTradeable(this);
                RemoveTradeableItem(pItem);
                pItem.SetState(ItemUpdateState.Removed, this);
                pItem2.SetState(ItemUpdateState.Changed, this);

                ApplyEquipCooldown(pItem2);

                return pItem2;
            }

            var attType = GetAttackBySlot(pos.Slot);
            if (attType.HasValue)
            {
                UpdateCritPercentage(attType.Value);

                if (attType == WeaponAttackType.BaseAttack || attType == WeaponAttackType.OffAttack)
                    CheckTitanGripPenalty();
            }

            // only for full equip instead adding to stack
            UpdateCriteria(CriteriaType.EquipItem, pItem.GetEntry());
            UpdateCriteria(CriteriaType.EquipItemInSlot, pos.Slot, pItem.GetEntry());

            UpdateAverageItemLevelEquipped();

            return pItem;
        }

        void QuickEquipItem(List<ItemPosCount> pos, Item pItem)
        {
            QuickEquipItem(pos.FirstOrDefault().Position, pItem);
        }

        void QuickEquipItem(ItemPos pos, Item pItem)
        {
            if (pItem != null)
            {
                AddEnchantmentDurations(pItem);
                AddItemDurations(pItem);

                VisualizeItem(pos.Slot, pItem);

                pItem.SetItemFlag2(ItemFieldFlags2.Equipped);

                if (IsInWorld)
                {
                    pItem.AddToWorld();
                    pItem.SendUpdateToPlayer(this);
                }

                if (pos.Slot == EquipmentSlot.MainHand || pos.Slot == EquipmentSlot.OffHand)
                    CheckTitanGripPenalty();

                UpdateCriteria(CriteriaType.EquipItem, pItem.GetEntry());
                UpdateCriteria(CriteriaType.EquipItemInSlot, pos.Slot, pItem.GetEntry());
            }
        }

        public void SendEquipError(InventoryResult msg, Item item1 = null, Item item2 = null, int itemId = 0)
        {
            InventoryChangeFailure failure = new();
            failure.BagResult = msg;

            if (msg != InventoryResult.Ok)
            {
                if (item1 != null)
                    failure.Item[0] = item1.GetGUID();

                if (item2 != null)
                    failure.Item[1] = item2.GetGUID();

                failure.ContainerBSlot = 0; // bag equip slot, used with EQUIP_ERR_EVENT_AUTOEQUIP_BIND_CONFIRM and EQUIP_ERR_ITEM_DOESNT_GO_INTO_BAG2

                switch (msg)
                {
                    case InventoryResult.CantEquipLevelI:
                    case InventoryResult.PurchaseLevelTooLow:
                    {
                        failure.Level = (item1 != null ? item1.GetRequiredLevel() : 0);
                        break;
                    }
                    case InventoryResult.EventAutoequipBindConfirm:    // no idea about this one...
                    {
                        //failure.SrcContainer
                        //failure.SrcSlot
                        //failure.DstContainer
                        break;
                    }
                    case InventoryResult.ItemMaxLimitCategoryCountExceededIs:
                    case InventoryResult.ItemMaxLimitCategorySocketedExceededIs:
                    case InventoryResult.ItemMaxLimitCategoryEquippedExceededIs:
                    {
                        ItemTemplate proto = item1 != null ? item1.GetTemplate() : Global.ObjectMgr.GetItemTemplate(itemId);
                        failure.LimitCategory = proto != null ? proto.GetItemLimitCategory() : 0;
                        break;
                    }
                    default:
                        break;
                }
            }

            SendPacket(failure);
        }

        //Add/Remove/Misc Item 
        public bool AddItem(int itemId, int count)
        {
            ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(itemId);
            if (itemTemplate == null)
                return false;

            InventoryResult msg = CanStoreNewItem(ItemPos.Undefined, out var dest, itemTemplate, count, out int noSpaceForCount);
            if (msg != InventoryResult.Ok)
                count -= noSpaceForCount;

            if (count == 0 || dest.Empty())
            {
                // @todo Send to mailbox if no space
                SendSysMessage("You don't have any space in your bags.");
                return false;
            }

            Item item = StoreNewItem(dest, itemId, true, ItemEnchantmentManager.GenerateRandomProperties(itemId));
            if (item != null)
                SendNewItem(item, count, true, false);
            else
                return false;
            return true;
        }

        public void RemoveItem(ItemPos pos, bool update)
        {
            // note: removeitem does not actually change the item
            // it only takes the item out of storage temporarily
            // note2: if removeitem is to be used for delinking
            // the item must be removed from the player's updatequeue

            Item pItem = GetItemByPos(pos);
            if (pItem != null)
            {
                Log.outDebug(LogFilter.Player,
                    $"Player.RemoveItem: RemoveItem bag = {pos.Container}, slot = {pos.Slot}, item = {pItem.GetEntry()}");

                RemoveEnchantmentDurations(pItem);
                RemoveItemDurations(pItem);
                RemoveTradeableItem(pItem);

                if (!pos.IsContainerPos)
                {
                    if (pos.Slot.IsBagSlot || pos.Slot.IsEquipSlot)
                    {
                        // item set bonuses applied only at equip and removed at unequip, and still active for broken items
                        ItemTemplate pProto = pItem.GetTemplate();
                        if (pProto != null && pProto.GetItemSet() != 0)
                            Item.RemoveItemsSetItem(this, pItem);

                        _ApplyItemMods(pItem, pos.Slot, false, update);

                        pItem.RemoveItemFlag2(ItemFieldFlags2.Equipped);

                        // remove item dependent auras and casts (only weapon and armor slots)
                        if (pos.Slot.IsEquipSlot)
                        {
                            // update expertise
                            if (pos.Slot == EquipmentSlot.MainHand)
                            {
                                // clear main hand only enchantments
                                for (EnchantmentSlot enchantSlot = 0; enchantSlot < EnchantmentSlot.Max; ++enchantSlot)
                                {
                                    var enchantment = CliDB.SpellItemEnchantmentStorage.LookupByKey(pItem.GetEnchantmentId(enchantSlot));
                                    if (enchantment != null && enchantment.HasFlag(SpellItemEnchantmentFlags.MainhandOnly))
                                        pItem.ClearEnchantment(enchantSlot);
                                }

                                UpdateExpertise(WeaponAttackType.BaseAttack);
                            }
                            else if (pos.Slot == EquipmentSlot.OffHand)
                                UpdateExpertise(WeaponAttackType.OffAttack);
                            // update armor penetration - passive auras may need it
                            switch (pos.Slot)
                            {
                                case EquipmentSlot.MainHand:
                                case EquipmentSlot.OffHand:
                                    RecalculateRating(CombatRating.ArmorPenetration);
                                    break;
                            }
                        }
                    }

                    m_items[pos.Slot] = null;
                    SetInvSlot(pos.Slot, ObjectGuid.Empty);

                    if (pos.Slot.IsEquipSlot)
                    {
                        SetVisibleItemSlot(pos.Slot, null);

                        var attType = GetAttackBySlot(pos.Slot);
                        if (attType.HasValue)
                        {
                            UpdateCritPercentage(attType.Value);

                            if (attType == WeaponAttackType.BaseAttack || attType == WeaponAttackType.OffAttack)
                                CheckTitanGripPenalty();
                        }                        
                    }
                }

                Bag pBag = GetBagByPos(pos.Container);
                if (pBag != null)
                    pBag.RemoveItem(pos.Slot, update);

                pItem.SetContainedIn(ObjectGuid.Empty);
                pItem.InventorySlot = ItemSlot.Null;
                if (IsInWorld && update)
                    pItem.SendUpdateToPlayer(this);

                if (pos.IsEquipmentPos && !pos.IsBagSlotPos)
                    UpdateAverageItemLevelEquipped();
            }
        }

        public void SplitItem(ItemPos src, ItemPos dst, int count)
        {
            Item pSrcItem = GetItemByPos(src);
            if (pSrcItem == null)
            {
                SendEquipError(InventoryResult.ItemNotFound, pSrcItem);
                return;
            }

            if (pSrcItem.m_lootGenerated)                           // prevent split looting item (item
            {
                //best error message found for attempting to split while looting
                SendEquipError(InventoryResult.SplitFailed, pSrcItem);
                return;
            }

            // not let split all items (can be only at cheating)
            if (pSrcItem.GetCount() == count)
            {
                SendEquipError(InventoryResult.SplitFailed, pSrcItem);
                return;
            }

            // not let split more existed items (can be only at cheating)
            if (pSrcItem.GetCount() < count)
            {
                SendEquipError(InventoryResult.TooFewToSplit, pSrcItem);
                return;
            }

            //! If trading
            TradeData tradeData = GetTradeData();
            if (tradeData != null)
            {
                //! If current item is in trade window (only possible with packet spoofing - silent return)
                if (tradeData.GetTradeSlotForItem(pSrcItem.GetGUID()) != TradeSlots.Invalid)
                    return;
            }

            Log.outDebug(LogFilter.Player,
                $"STORAGE: SplitItem bag = {dst.Container}, slot = {dst.Slot}, item = {pSrcItem.GetEntry()}, count = {count}");

            Item pNewItem = pSrcItem.CloneItem(count, this);
            if (pNewItem == null)
            {
                SendEquipError(InventoryResult.ItemNotFound, pSrcItem);
                return;
            }

            if (dst.IsInventoryPos)
            {
                // change item amount before check (for unique max count check)
                pSrcItem.SetCount(pSrcItem.GetCount() - count);
                InventoryResult msg = CanStoreItem(dst, out var dest, pNewItem);
                if (msg != InventoryResult.Ok)
                {
                    pSrcItem.SetCount(pSrcItem.GetCount() + count);
                    SendEquipError(msg, pSrcItem);
                    return;
                }

                if (IsInWorld)
                    pSrcItem.SendUpdateToPlayer(this);
                pSrcItem.SetState(ItemUpdateState.Changed, this);
                StoreItem(dest, pNewItem, true);
            }
            else if (dst.IsBankPos)
            {
                // change item amount before check (for unique max count check)
                pSrcItem.SetCount(pSrcItem.GetCount() - count);
                InventoryResult msg = CanBankItem(dst, out var dest, pNewItem, false);
                if (msg != InventoryResult.Ok)
                {
                    pSrcItem.SetCount(pSrcItem.GetCount() + count);
                    SendEquipError(msg, pSrcItem);
                    return;
                }

                if (IsInWorld)
                    pSrcItem.SendUpdateToPlayer(this);
                pSrcItem.SetState(ItemUpdateState.Changed, this);
                BankItem(dest, pNewItem, true);
            }
            else if (dst.IsEquipmentPos)
            {
                // change item amount before check (for unique max count check), provide space for splitted items
                pSrcItem.SetCount(pSrcItem.GetCount() - count);

                InventoryResult msg = CanEquipItem(dst.Slot, out List<ItemPosCount> dest, pNewItem, false);
                if (msg != InventoryResult.Ok)
                {
                    pSrcItem.SetCount(pSrcItem.GetCount() + count);
                    SendEquipError(msg, pSrcItem);
                    return;
                }

                if (IsInWorld)
                    pSrcItem.SendUpdateToPlayer(this);

                pSrcItem.SetState(ItemUpdateState.Changed, this);
                EquipItem(dest, pNewItem, true);
                AutoUnequipOffhandIfNeed();
            }
        }

        private void SwapBagWithItemExchange(ItemPos equippedItem, ItemPos storedItem, Bag equippedBag, Bag storedBag)
        {
            List<Item> exchangeItems = null;
            Dictionary<Item, List<ItemPosCount>> exchangeResult = null;

            InventoryResult result = InventoryResult.Ok;

            // Save Items for exchange
            exchangeItems = equippedBag.GetItemsCopy();

            // Use the sorted collection for the best match when searching for storage space
            exchangeItems.Sort(new ItemStorageComparer<Item>());

            exchangeResult = new(exchangeItems.Count);

            // Try to store in the same source bag with preset
            ItemPresetMap exchangePresetMap = new(exchangeItems);
            exchangePresetMap[equippedItem] = new(storedBag, storedBag.GetCount());
            exchangePresetMap[storedItem] = new(equippedBag, equippedBag.GetCount());

            // Try to store items to the bag first
            foreach (var item in exchangeItems)
            {
                Item bagItem = item;
                if (!Item.ItemCanGoIntoBag(bagItem.GetTemplate(), storedBag.GetTemplate()))
                {
                    // Try to store unsuitable items to inventory
                    if (equippedItem.IsBankPos)
                    {
                        result = CanBankItem(ItemPos.Undefined, out var dest, bagItem, exchangePresetMap);
                        exchangeResult[item] = dest;
                    }
                    else
                    {
                        result = CanStoreItem(ItemPos.Undefined, out var dest, bagItem, exchangePresetMap);
                        exchangeResult[item] = dest;
                    }
                }
                else
                {
                    if (equippedItem.IsBankPos)
                    {
                        result = CanBankItem(ItemPos.UndefinedBag(equippedItem), out var dest, bagItem, exchangePresetMap);
                        exchangeResult[item] = dest;

                        // This only happens if no items have been stored in a bag.
                        if (result == InventoryResult.BagFull)
                        {
                            result = CanBankItem(ItemPos.Undefined, out var dest2, bagItem, exchangePresetMap);
                            exchangeResult[item] = dest2;
                        }
                    }
                    else
                    {
                        result = CanStoreItem(ItemPos.UndefinedBag(equippedItem), out var dest, bagItem, exchangePresetMap);
                        exchangeResult[item] = dest;

                        // This only happens if no items have been stored in a bag.
                        if (result == InventoryResult.BagFull)
                        {
                            result = CanStoreItem(ItemPos.Undefined, out var dest2, bagItem, exchangePresetMap);
                            exchangeResult[item] = dest2;
                        }
                    }
                }

                if (result != InventoryResult.Ok)
                {
                    SendEquipError(InventoryResult.CantSwap, equippedBag, storedBag);
                    return;
                }
            }           

            // We will save/restore these items below
            foreach (var row in exchangeItems)
            {
                equippedBag.RemoveItem(row.InventorySlot, false);
            }

            #region Swap Bags

            // check src->dest move possibility
            InventoryResult _msgSrc = CheckMovePossibility(equippedBag, storedItem, out var sDest1);
            if (_msgSrc != InventoryResult.Ok)
            {
                SendEquipError(_msgSrc, equippedBag, storedBag);
                RestoreExchangeItems(exchangeItems, equippedBag);
                return;
            }

            // check dest->src move possibility
            InventoryResult _msgDst = CheckMovePossibility(storedBag, equippedItem, out var sDest2);
            if (_msgDst != InventoryResult.Ok)
            {
                SendEquipError(_msgDst, storedBag, equippedBag);
                RestoreExchangeItems(exchangeItems, equippedBag);
                return;
            }

            // now do moves, remove...
            RemoveItem(storedItem, false);
            RemoveItem(equippedItem, false);

            // add to dest
            if (storedItem.IsInventoryPos)
                StoreItem(sDest1, equippedBag, true);
            else if (storedItem.IsBankPos)
                BankItem(sDest1, equippedBag, true);
            else if (storedItem.IsEquipmentPos)
                EquipItem(sDest1, equippedBag, true);

            // add to src
            if (equippedItem.IsInventoryPos)
                StoreItem(sDest2, storedBag, true);
            else if (equippedItem.IsBankPos)
                BankItem(sDest2, storedBag, true);
            else if (equippedItem.IsEquipmentPos)
                EquipItem(sDest2, storedBag, true);

            // if inventory item was moved, check if we can remove dependent auras, because they were not removed in Player::RemoveItem (update was set to false)
            // do this after swaps are done, we pass nullptr because both bags could be swapped and none of them should be ignored
            if (equippedItem.IsEquipmentPos || storedItem.IsEquipmentPos)
                ApplyItemDependentAuras(null, false);

            if (equippedItem.IsBankPos && !storedItem.IsBankPos)
            {
                ItemAddedQuestCheck(equippedBag.GetEntry(), equippedBag.GetCount());
                ItemRemovedQuestCheck(storedBag.GetEntry(), storedBag.GetCount());
            }

            if (!equippedItem.IsBankPos && storedItem.IsBankPos)
            {
                ItemAddedQuestCheck(storedBag.GetEntry(), storedBag.GetCount());
                ItemRemovedQuestCheck(equippedBag.GetEntry(), equippedBag.GetCount());
            }

            // Move bag Exchanged items
            if (exchangeItems != null && exchangeResult != null)
            {
                foreach (var row in exchangeResult)
                {
                    StoreItem(row.Value, row.Key, true);                                      
                }
            }
            #endregion

            #region Helpers

            void RestoreExchangeItems(List<Item> items, Item pSrcItem)
            {
                if (items == null)
                    return;

                if (pSrcItem is Bag itemBag)
                {
                    foreach (var i in items)
                    {
                        Item item = i;
                        itemBag.StoreItem(i.InventorySlot, item, true);
                        item.SetState(ItemUpdateState.Changed, this);
                    }
                }
            }

            InventoryResult CheckMovePossibility(Item thisItem, ItemPos inPosition, out List<ItemPosCount> moveResult)
            {
                InventoryResult answer = InventoryResult.Ok;

                if (inPosition.IsInventoryPos)
                    answer = CanStoreItem(inPosition, out moveResult, thisItem, true);
                else if (inPosition.IsBankPos)
                    answer = CanBankItem(inPosition, out moveResult, thisItem, true);
                else if (inPosition.IsEquipmentPos)
                {
                    answer = CanEquipItem(inPosition.Slot, out moveResult, thisItem, true);
                    if (answer == InventoryResult.Ok)
                        answer = CanUnequipItem(moveResult, true);
                }
                else
                    moveResult = new();

                return answer;
            }
            #endregion
        }

        private void EmptyBag(ItemPos src, ItemPos dst, Bag srcBag)
        {
            List<Item> bagItems = null;
            InventoryResult msg = InventoryResult.Ok;
            InventoryResult prevErrorMsg = msg;

            // Save bag items we will drop
            bagItems = srcBag.GetItemsCopy();

            // Use the sorted collection for the best match when searching for storage space
            bagItems.Sort(new ItemStorageComparer<Item>());

            // Engage all bag's slots to avoid participating them in process
            ItemPresetMap dropPresetMap = new(bagItems.Count);
            foreach(var slot in srcBag)
            {
                dropPresetMap[slot.Item1] = null;
            }

            // Try to store items to the destination first
            Bag dstBag = null;            

            if (dst.IsContainerPos)
            {
                dstBag = GetItemByPos(dst.Container).ToBag();
            }

            if (dstBag != null)
            {
                foreach (var item in bagItems)
                {
                    List<ItemPosCount> result = new();
                    int no_space_count = item.GetCount();

                    if (!Item.ItemCanGoIntoBag(item.GetTemplate(), dstBag.GetTemplate()))
                    {
                        // Try to store unsuitable items to inventory
                        if (dst.IsBankPos)
                        {
                            msg = CanBankItem(ItemPos.Undefined, out result, out no_space_count, item, dropPresetMap);
                        }
                        else
                        {
                            msg = CanStoreItem(ItemPos.Undefined, out result, out no_space_count, item, dropPresetMap);
                        }
                    }
                    else
                    {
                        if (dst.IsBankPos)
                        {
                            msg = CanBankItem(ItemPos.UndefinedBag(dst.Container), out result, out no_space_count, item, dropPresetMap);

                            // This only happens if no items have been stored in a bag.
                            if (msg == InventoryResult.BagFull)
                            {
                                msg = CanBankItem(ItemPos.Undefined, out result, out no_space_count, item, dropPresetMap);
                            }
                        }
                        else
                        {
                            msg = CanStoreItem(ItemPos.UndefinedBag(dst.Container), out result, out no_space_count, item, dropPresetMap);

                            // This only happens if no items have been stored in a bag.
                            if (msg == InventoryResult.BagFull)
                            {
                                msg = CanStoreItem(ItemPos.Undefined, out result, out no_space_count, item, dropPresetMap);
                            }
                        }
                    }

                    ProcessItem(item, result, no_space_count);
                }                
            }
            else
            {
                foreach (var item in bagItems)
                {
                    List<ItemPosCount> result = new();
                    int no_space_count = item.GetCount();

                    if (dst.IsBankPos)
                    {
                        msg = CanBankItem(ItemPos.Undefined, out result, out no_space_count, item, dropPresetMap);                        
                    }
                    else
                    {
                        msg = CanStoreItem(ItemPos.Undefined, out result, out no_space_count, item, dropPresetMap);
                    }

                    ProcessItem(item, result, no_space_count);
                }
            }

            // just remove grey item state (because client don't support this process)
            SendEquipError(InventoryResult.EquipNone3, srcBag);

            #region Helpers
            void MoveItem(Item currentItem, List<ItemPosCount> destination, int movedAmount)
            {
                // Remove bag's item
                srcBag.RemoveItem(currentItem.InventorySlot, false);

                // Move bag's items
                if (dst.IsBankPos)
                    BankItem(destination, currentItem, true);
                else
                    StoreItem(destination, currentItem, true);

                if (src.IsBankPos && !dst.IsBankPos)
                {
                    ItemAddedQuestCheck(currentItem.GetEntry(), movedAmount);
                }

                if (!src.IsBankPos && dst.IsBankPos)
                {
                    ItemRemovedQuestCheck(currentItem.GetEntry(), movedAmount);
                }
            }

            void ProcessItem(Item currentItem, List<ItemPosCount> destination, int no_space_count)
            {
                if (msg != InventoryResult.Ok)
                {
                    if (msg != prevErrorMsg)
                    {
                        SendEquipError(msg, srcBag, dstBag);
                        prevErrorMsg = msg;
                    }

                    // The moved part of the stack
                    int movedAmount = currentItem.GetCount() - no_space_count;

                    // Try to store part of stackable item
                    if (movedAmount != 0)
                    {
                        // Add rest of item to store with a new GUID
                        destination.Add(new(currentItem.InventoryPosition, no_space_count));

                        MoveItem(currentItem, destination, movedAmount);                        
                    }
                }
                else
                {
                    MoveItem(currentItem, destination, currentItem.GetCount());
                }
            }
            #endregion
        }

        private void SwapBag(ItemPos src, ItemPos dst, Bag srcBag, Item dstItem)
        {
            InventoryResult msg;
            Bag dstBag = dstItem?.ToBag();

            // check src->dest move possibility
            List<ItemPosCount> sDest1 = null;
            msg = CheckMovePossibility(srcBag, dst, out sDest1);
            if (msg != InventoryResult.Ok)
            {
                SendEquipError(msg, srcBag, dstBag);
                return;
            }

            // check dest->src move possibility
            List<ItemPosCount> sDest2 = null;
            if (dstBag != null)
            {
                msg = CheckMovePossibility(dstBag, src, out sDest2);
                if (msg != InventoryResult.Ok)
                {
                    SendEquipError(msg, dstBag, srcBag);
                    return;
                }
            }

            // now do moves, remove...            
            RemoveItem(src, false);
            if (dstBag != null)
            {
                RemoveItem(dst, false);
            }

            // add to dest
            if (dst.IsInventoryPos)
                StoreItem(sDest1, srcBag, true);
            else if (dst.IsBankPos)
                BankItem(sDest1, srcBag, true);
            else if (dst.IsEquipmentPos)
                EquipItem(sDest1, srcBag, true);

            if (dstBag != null)
            {
                // add to src
                if (src.IsInventoryPos)
                    StoreItem(sDest2, dstBag, true);
                else if (src.IsBankPos)
                    BankItem(sDest2, dstBag, true);
                else if (src.IsEquipmentPos)
                    EquipItem(sDest2, dstBag, true);
            }

            // if inventory item was moved, check if we can remove dependent auras, because they were not removed in Player::RemoveItem (update was set to false)
            // do this after swaps are done, we pass nullptr because both bags could be swapped and none of them should be ignored
            if (src.IsEquipmentPos || dst.IsEquipmentPos)
                ApplyItemDependentAuras(null, false);

            if (src.IsBankPos && !dst.IsBankPos)
            {
                ItemAddedQuestCheck(srcBag.GetEntry(), srcBag.GetCount());
                if (dstBag != null)
                {
                    ItemRemovedQuestCheck(dstBag.GetEntry(), dstBag.GetCount());
                }

                foreach (var item in srcBag.GetItems())
                {
                    ItemAddedQuestCheck(item.GetEntry(), item.GetCount());
                }
            }

            if (!src.IsBankPos && dst.IsBankPos)
            {
                ItemRemovedQuestCheck(srcBag.GetEntry(), srcBag.GetCount());                
                if (dstBag != null)
                {
                    ItemAddedQuestCheck(dstBag.GetEntry(), dstBag.GetCount());
                }

                foreach (var item in srcBag.GetItems())
                {
                    ItemRemovedQuestCheck(item.GetEntry(), item.GetCount());
                }
            }

            // if player is moving bags and is looting an item inside this bag
            // release the loot
            if (!GetAELootView().Empty())
            {
                bool released = false;
                if (src.IsBagSlotPos)
                {
                    foreach (var item in srcBag.GetItems())
                    {
                        if (GetLootByWorldObjectGUID(item.GetGUID()) != null)
                        {
                            GetSession().DoLootReleaseAll();
                            released = true;                    // so we don't need to look at dstBag
                            break;
                        }
                    }
                }

                if (!released && dst.IsBagSlotPos && dstBag != null)
                {
                    foreach (var item in dstBag.GetItems())
                    {
                        if (GetLootByWorldObjectGUID(item.GetGUID()) != null)
                        {
                            GetSession().DoLootReleaseAll();
                            break;
                        }
                    }
                }
            }

            #region Helpers
            InventoryResult CheckMovePossibility(Item thisItem, ItemPos inPosition, out List<ItemPosCount> moveResult)
            {
                InventoryResult answer = InventoryResult.Ok;

                if (inPosition.IsInventoryPos)
                    answer = CanStoreItem(inPosition, out moveResult, thisItem, true);
                else if (inPosition.IsBankPos)
                    answer = CanBankItem(inPosition, out moveResult, thisItem, true);
                else if (inPosition.IsEquipmentPos)
                {
                    answer = CanEquipItem(inPosition.Slot, out moveResult, thisItem, true);
                    if (answer == InventoryResult.Ok)
                        answer = CanUnequipItem(moveResult, true);
                }
                else
                    moveResult = new();

                return answer;
            }
            #endregion
        }

        public void SwapItem(ItemPos src, ItemPos dst)
        {
            Item pSrcItem = GetItemByPos(src);
            Item pDstItem = GetItemByPos(dst);

            if (pSrcItem == null)
                return;

            Log.outDebug(LogFilter.Player,
                $"Player.SwapItem: SwapItem bag = {dst.Container}, slot = {dst.Slot}, item = {pSrcItem.GetEntry()}");

            if (!IsAlive())
            {
                SendEquipError(InventoryResult.PlayerDead, pSrcItem, pDstItem);
                return;
            }

            // prevent put equipped/bank bag in self
            if (src.IsBagSlotPos && src.Slot == dst.Container)
            {
                SendEquipError(InventoryResult.ItemLocked, pSrcItem, pDstItem);
                return;
            }

            InventoryResult msg;

            // check unequip potability for equipped items and bank bags            
            msg = CanUnequipItem(pSrcItem, true);
            if (msg != InventoryResult.Ok)
            {
                SendEquipError(msg, pSrcItem, pDstItem);
                return;
            }

            msg = CanUnequipItem(pDstItem, true);
            if (msg != InventoryResult.Ok)
            {
                SendEquipError(msg, pSrcItem, pDstItem);
                return;
            }

            #region Cases with equipped bags are best considered separately.

            // Swap equipped bags
            if (src.IsBagSlotPos && dst.IsBagSlotPos)
            {
                if (pSrcItem is Bag srcSwapBag)
                {
                    SwapBag(src, dst, srcSwapBag, pDstItem);
                }

                return;
            }

            // Swap bags with item exchange.
            if (!src.IsBagSlotPos && dst.IsBagSlotPos)
            {
                if (pSrcItem is Bag srcExchangeBag && pDstItem is Bag dstExchangeBag)
                {
                    SwapBagWithItemExchange(dst, src, dstExchangeBag, srcExchangeBag);
                    return;
                }
            }

            // Empty the bag or swap with item exchange.
            if (src.IsBagSlotPos && !dst.IsBagSlotPos && pSrcItem is Bag srcBag)
            {
                if (pDstItem is Bag dstBag)
                {
                    SwapBagWithItemExchange(src, dst, srcBag, dstBag);
                    return;
                }

                if (!srcBag.IsEmpty())
                {
                    if (pDstItem is null)
                        EmptyBag(src, dst, srcBag);
                    else
                        SendEquipError(InventoryResult.CantSwap, pSrcItem, pDstItem);

                    return;
                }
            }
            
            #endregion

            // NOW this is or item move (swap with empty), or swap with another item (including bags in bag possitions)

            #region Move case
            if (pDstItem == null)
            {
                if (dst.IsInventoryPos)
                {
                    msg = CanStoreItem(dst, out var dest, pSrcItem);
                    if (msg != InventoryResult.Ok)
                    {
                        SendEquipError(msg, pSrcItem);
                        return;
                    }

                    RemoveItem(src, true);
                    StoreItem(dest, pSrcItem, true);
                }
                else if (dst.IsBankPos)
                {
                    msg = CanBankItem(dst, out var dest, pSrcItem, false);
                    if (msg != InventoryResult.Ok)
                    {
                        SendEquipError(msg, pSrcItem);
                        return;
                    }

                    RemoveItem(src, true);
                    BankItem(dest, pSrcItem, true);
                }
                else if (dst.IsEquipmentPos)
                {
                    msg = CanEquipItem(dst.Slot, out List<ItemPosCount> dest, pSrcItem, false);
                    if (msg != InventoryResult.Ok)
                    {
                        SendEquipError(msg, pSrcItem);
                        return;
                    }

                    RemoveItem(src, true);
                    EquipItem(dest, pSrcItem, true);
                    AutoUnequipOffhandIfNeed();
                }

                if (src.IsBankPos && !dst.IsBankPos)
                    ItemAddedQuestCheck(pSrcItem.GetEntry(), pSrcItem.GetCount());

                if (!src.IsBankPos && dst.IsBankPos)
                    ItemRemovedQuestCheck(pSrcItem.GetEntry(), pSrcItem.GetCount());

                return;
            }
            #endregion

            #region attempt merge to / fill target item
            if (!pSrcItem.IsBag() && !pDstItem.IsBag())
            {
                List<ItemPosCount> dest;
                if (dst.IsInventoryPos)
                    msg = CanStoreItem(dst, out dest, pSrcItem);
                else if (dst.IsBankPos)
                    msg = CanBankItem(dst, out dest, pSrcItem, false);
                else if (dst.IsEquipmentPos)
                    msg = CanEquipItem(dst.Slot, out dest, pSrcItem, false);
                else
                    return;

                // can be merge/fill
                if (msg == InventoryResult.Ok)
                {
                    if (pSrcItem.GetCount() + pDstItem.GetCount() <= pSrcItem.GetTemplate().GetMaxStackSize())
                    {
                        RemoveItem(src, true);

                        if (dst.IsInventoryPos)
                            StoreItem(dest, pSrcItem, true);
                        else if (dst.IsBankPos)
                            BankItem(dest, pSrcItem, true);
                        else if (dst.IsEquipmentPos)
                        {
                            EquipItem(dest, pSrcItem, true);
                            AutoUnequipOffhandIfNeed();
                        }

                        if (src.IsBankPos && !dst.IsBankPos)
                            ItemAddedQuestCheck(pSrcItem.GetEntry(), pSrcItem.GetCount());

                        if (!src.IsBankPos && dst.IsBankPos)
                            ItemRemovedQuestCheck(pSrcItem.GetEntry(), pSrcItem.GetCount());
                    }
                    else
                    {
                        int srcOldCount = pSrcItem.GetCount();
                        int dstOldCount = pDstItem.GetCount();

                        pSrcItem.SetCount(srcOldCount + dstOldCount - pSrcItem.GetTemplate().GetMaxStackSize());
                        pDstItem.SetCount(pSrcItem.GetTemplate().GetMaxStackSize());
                        pSrcItem.SetState(ItemUpdateState.Changed, this);
                        pDstItem.SetState(ItemUpdateState.Changed, this);

                        if (IsInWorld)
                        {
                            pSrcItem.SendUpdateToPlayer(this);
                            pDstItem.SendUpdateToPlayer(this);
                        }

                        int transferredCount = srcOldCount - pSrcItem.GetCount();

                        if (src.IsBankPos && !dst.IsBankPos)
                            ItemAddedQuestCheck(pSrcItem.GetEntry(), transferredCount);

                        if (!src.IsBankPos && dst.IsBankPos)
                            ItemRemovedQuestCheck(pSrcItem.GetEntry(), transferredCount);
                    }

                    SendRefundInfo(pDstItem);
                    return;
                }
            }
            #endregion            

            #region impossible merge/fill, do real swap
            // check src->dest move possibility
            InventoryResult _msgSrc = CheckMovePossibility(pSrcItem, dst, out var sDest1);
            if (_msgSrc != InventoryResult.Ok)
            {
                SendEquipError(_msgSrc, pSrcItem, pDstItem);
                return;
            }

            // check dest->src move possibility
            InventoryResult _msgDst = CheckMovePossibility(pDstItem, src, out var sDest2);
            if (_msgDst != InventoryResult.Ok)
            {
                SendEquipError(_msgDst, pDstItem, pSrcItem);
                return;
            }

            // now do moves, remove...
            RemoveItem(dst, false);
            RemoveItem(src, false);

            // add to dest
            if (dst.IsInventoryPos)
                StoreItem(sDest1, pSrcItem, true);
            else if (dst.IsBankPos)
                BankItem(sDest1, pSrcItem, true);
            else if (dst.IsEquipmentPos)
                EquipItem(sDest1, pSrcItem, true);

            // add to src
            if (src.IsInventoryPos)
                StoreItem(sDest2, pDstItem, true);
            else if (src.IsBankPos)
                BankItem(sDest2, pDstItem, true);
            else if (src.IsEquipmentPos)
                EquipItem(sDest2, pDstItem, true);

            // if inventory item was moved, check if we can remove dependent auras, because they were not removed in Player::RemoveItem (update was set to false)
            // do this after swaps are done, we pass nullptr because both weapons could be swapped and none of them should be ignored
            if (src.IsEquipmentPos || dst.IsEquipmentPos)
                ApplyItemDependentAuras(null, false);

            if (src.IsBankPos && !dst.IsBankPos)
            {
                ItemAddedQuestCheck(pSrcItem.GetEntry(), pSrcItem.GetCount());
                ItemRemovedQuestCheck(pDstItem.GetEntry(), pDstItem.GetCount());
            }

            if (!src.IsBankPos && dst.IsBankPos)
            {
                ItemAddedQuestCheck(pDstItem.GetEntry(), pDstItem.GetCount());
                ItemRemovedQuestCheck(pSrcItem.GetEntry(), pSrcItem.GetCount());
            }

            AutoUnequipOffhandIfNeed();
            #endregion

            #region Helpers
            InventoryResult CheckMovePossibility(Item thisItem, ItemPos inPosition, out List<ItemPosCount> moveResult)
            {
                InventoryResult answer = InventoryResult.Ok;

                if (inPosition.IsInventoryPos)
                    answer = CanStoreItem(inPosition, out moveResult, thisItem, true);
                else if (inPosition.IsBankPos)
                    answer = CanBankItem(inPosition, out moveResult, thisItem, true);
                else if (inPosition.IsEquipmentPos)
                {
                    answer = CanEquipItem(inPosition.Slot, out moveResult, thisItem, true);
                    if (answer == InventoryResult.Ok)
                        answer = CanUnequipItem(moveResult, true);
                }
                else
                    moveResult = new();

                return answer;
            }
            #endregion
        }

        bool _StoreOrEquipNewItem(int vendorslot, int item, byte count, ItemPos pos, long price, ItemTemplate pProto, Creature pVendor, VendorItem crItem, bool bStore)
        {
            var stacks = count / pProto.GetBuyCount();
            List<ItemPosCount> dest = new();
            InventoryResult msg;

            if (bStore)
                msg = CanStoreNewItem(pos, out dest, pProto, count, out _);
            else
                msg = CanEquipNewItem(pos.Slot, out dest, item, false);

            if (msg != InventoryResult.Ok)
            {
                SendEquipError(msg, null, null, item);
                return false;
            }

            ModifyMoney(-price);

            if (crItem.ExtendedCost != 0) // case for new honor system
            {
                var iece = CliDB.ItemExtendedCostStorage.LookupByKey(crItem.ExtendedCost);
                Cypher.Assert(iece != null);

                for (int i = 0; i < ItemConst.MaxItemExtCostItems; ++i)
                {
                    if (iece.ItemID[i] != 0)
                        DestroyItemCount(iece.ItemID[i], iece.ItemCount[i] * stacks, true);
                }

                for (int i = 0; i < ItemConst.MaxItemExtCostCurrencies; ++i)
                {
                    if (iece.Flags.HasAnyFlag((byte)((int)ItemExtendedCostFlags.RequireSeasonEarned1 << i)))
                        continue;

                    if (iece.CurrencyID[i] != 0)
                        RemoveCurrency(iece.CurrencyID[i], (iece.CurrencyCount[i] * stacks), CurrencyDestroyReason.Vendor);
                }
            }

            Item it;
            if (bStore)
                it = StoreNewItem(dest, item, true, ItemEnchantmentManager.GenerateRandomProperties(item), null, ItemContext.Vendor, false);
            else
                it = EquipNewItem(dest, item, ItemContext.Vendor, true);

            if (it != null)
            {
                var new_count = pVendor.UpdateVendorItemCurrentCount(crItem, count);

                BuySucceeded packet = new();
                packet.VendorGUID = pVendor.GetGUID();
                packet.Muid = vendorslot + 1;
                packet.NewQuantity = crItem.maxcount > 0 ? new_count : -1;
                packet.QuantityBought = count;
                SendPacket(packet);

                SendNewItem(it, count, true, false, false);

                if (!bStore)
                    AutoUnequipOffhandIfNeed();

                if (pProto.HasFlag(ItemFlags.ItemPurchaseRecord) && crItem.ExtendedCost != 0 && pProto.GetMaxStackSize() == 1)
                {
                    it.SetItemFlag(ItemFieldFlags.Refundable);
                    it.SetRefundRecipient(GetGUID());
                    it.SetPaidMoney(price);
                    it.SetPaidExtendedCost(crItem.ExtendedCost);
                    it.SaveRefundDataToDB();
                    AddRefundReference(it.GetGUID());
                }

                GetSession().GetCollectionMgr().OnItemAdded(it);
            }
            return true;
        }

        public void SendNewItem(Item item, int quantity, bool pushed, bool created, bool broadcast = false, int dungeonEncounterId = 0)
        {
            if (item == null) // prevent crash
                return;

            ItemPushResult packet = new();

            packet.PlayerGUID = GetGUID();

            packet.Slot = item.InventoryBagSlot;
            packet.SlotInBag = item.GetCount() == quantity ? item.InventorySlot : -1;

            packet.Item = new ItemInstance(item);

            packet.QuestLogItemID = item.GetTemplate().QuestLogItemId;
            packet.Quantity = quantity;
            packet.QuantityInInventory = GetItemCount(item.GetEntry());
            packet.BattlePetSpeciesID = item.GetModifier(ItemModifier.BattlePetSpeciesId);
            packet.BattlePetBreedID = item.GetModifier(ItemModifier.BattlePetBreedData) & 0xFFFFFF;
            packet.BattlePetBreedQuality = (uint)(item.GetModifier(ItemModifier.BattlePetBreedData) >> 24) & 0xFF;
            packet.BattlePetLevel = item.GetModifier(ItemModifier.BattlePetLevel);

            packet.ItemGUID = item.GetGUID();

            packet.Pushed = pushed;
            packet.DisplayText = ItemPushResult.DisplayType.Normal;
            packet.Created = created;
            //packet.IsBonusRoll;

            if (dungeonEncounterId != 0)
            {
                packet.DisplayText = ItemPushResult.DisplayType.EncounterLoot;
                packet.DungeonEncounterID = dungeonEncounterId;
                packet.IsEncounterLoot = true;
            }

            if (broadcast && GetGroup() != null && !item.GetTemplate().HasFlag(ItemFlags3.DontReportLootLogToParty))
                GetGroup().BroadcastPacket(packet, true);
            else
                SendPacket(packet);
        }

        //Item Durations
        void RemoveItemDurations(Item item)
        {
            m_itemDuration.Remove(item);
        }

        void AddItemDurations(Item item)
        {
            if (item.m_itemData.Expiration.GetValue() != 0)
            {
                m_itemDuration.Add(item);
                item.SendTimeUpdate(this);
            }
        }

        void UpdateItemDuration(Seconds time, bool realtimeonly = false)
        {
            if (m_itemDuration.Empty())
                return;

            Log.outDebug(LogFilter.Player, $"Player:UpdateItemDuration({time}, {realtimeonly})");

            foreach (var item in m_itemDuration)
            {
                if (!realtimeonly || item.GetTemplate().HasFlag(ItemFlags.RealDuration))
                    item.UpdateDuration(this, time);
            }
        }

        void SendEnchantmentDurations()
        {
            foreach (var enchantDuration in m_enchantDuration)
                GetSession().SendItemEnchantTimeUpdate(GetGUID(), enchantDuration.item.GetGUID(), enchantDuration.slot, enchantDuration.leftduration);
        }

        void SendItemDurations()
        {
            foreach (var item in m_itemDuration)
                item.SendTimeUpdate(this);
        }

        public void ToggleMetaGemsActive(int exceptslot, bool apply)
        {
            //cycle all equipped items
            for (byte slot = EquipmentSlot.Start; slot < EquipmentSlot.End; ++slot)
            {
                //enchants for the slot being socketed are handled by WorldSession.HandleSocketOpcode(WorldPacket& recvData)
                if (slot == exceptslot)
                    continue;

                Item pItem = GetItemByPos(slot);

                if (pItem == null || pItem.GetSocketType(0) == 0)   //if item has no sockets or no item is equipped go to next item
                    continue;

                //cycle all (gem)enchants
                for (EnchantmentSlot enchant_slot = EnchantmentSlot.EnhancementSocket; enchant_slot < EnchantmentSlot.EnhancementSocket + 3; ++enchant_slot)
                {
                    int enchant_id = pItem.GetEnchantmentId(enchant_slot);
                    if (enchant_id == 0)                                 //if no enchant go to next enchant(slot)
                        continue;

                    SpellItemEnchantmentRecord enchantEntry = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                    if (enchantEntry == null)
                        continue;

                    //only metagems to be (de)activated, so only enchants with condition
                    int condition = enchantEntry.ConditionID;
                    if (condition != 0)
                        ApplyEnchantment(pItem, enchant_slot, apply);
                }
            }
        }

        public int GetAverageItemLevel()
        {
            var sum = 0;
            var count = 0;

            for (int i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
            {
                // don't check tabard, ranged, offhand or shirt
                if (i == EquipmentSlot.Tabard || i == EquipmentSlot.Ranged || i == EquipmentSlot.OffHand || i == EquipmentSlot.Shirt)
                    continue;

                if (m_items[i] != null)
                    sum += m_items[i].GetItemLevel(this);

                ++count;
            }

            return sum / count;
        }

        public List<Item> GetCraftingReagentItemsToDeposit()
        {
            List<Item> itemList = new();
            ForEachItem(ItemSearchLocation.Inventory, item =>
            {
                if (item.GetTemplate().IsCraftingReagent())
                    itemList.Add(item);

                return true;
            });

            return itemList;
        }

        public Item GetItemByGuid(ObjectGuid guid)
        {
            Item result = null;
            ForEachItem(ItemSearchLocation.Everywhere, item =>
            {
                if (item.GetGUID() == guid)
                {
                    result = item;
                    return false;
                }

                return true;
            });

            return result;
        }

        public int GetItemCount(int item, bool inBankAlso = false, Item skipItem = null)
        {
            bool countGems = skipItem != null && skipItem.GetTemplate().GetGemProperties() != 0;

            ItemSearchLocation location = ItemSearchLocation.Equipment | ItemSearchLocation.Inventory | ItemSearchLocation.ReagentBank;
            if (inBankAlso)
                location |= ItemSearchLocation.Bank;

            int count = 0;
            ForEachItem(location, pItem =>
            {
                if (pItem != skipItem)
                {
                    if (pItem.GetEntry() == item)
                        count += pItem.GetCount();

                    if (countGems)
                        count += pItem.GetGemCountWithID(item);
                }
                return true;
            });

            return count;
        }

        public Item GetUseableItemByPos(ItemPos pos)
        {
            Item item = GetItemByPos(pos);
            if (item == null)
                return null;

            if (!CanUseAttackType(GetAttackBySlot(pos.Slot)))
                return null;

            return item;
        }

        public Item GetItemByPos(ItemPos pos)
        {
            return GetItemPresetByPos(pos, null).Value.Item;            
        }

        public ItemPreset? GetItemPresetByPos(ItemPos pos, ItemPresetMap presetMap)
        {
            Item item = null;

            if (!pos.IsContainerPos && pos.Slot <= m_items.Length)
            {
                item = m_items[pos.Slot];                 
            }
            else
            {
                Bag pBag = GetBagByPos(pos.Container, presetMap);
                if (pBag != null)
                {
                    item = pBag.GetItemByPos(pos.Slot);
                }
            }

            if (presetMap != null)
            {
                if (presetMap.TryGetValue(pos, out var foundPreset))
                {
                    return foundPreset;
                }
            }

            return MakeNewPreset(item);

            ItemPreset MakeNewPreset(Item item)
            {
                ItemPreset newPreset = ItemPreset.FreeSlot;

                newPreset.Item = item;

                if (item != null)
                    newPreset.Count = item.GetCount();

                return newPreset;
            }
        }

        public Item GetItemByEntry(int entry, ItemSearchLocation where = ItemSearchLocation.Default)
        {
            Item result = null;
            ForEachItem(where, item =>
            {
                if (item.GetEntry() == entry)
                {
                    result = item;
                    return false;
                }

                return true;
            });

            return result;
        }

        public List<Item> GetItemListByEntry(int entry, bool inBankAlso = false)
        {
            ItemSearchLocation location = ItemSearchLocation.Equipment | ItemSearchLocation.Inventory | ItemSearchLocation.ReagentBank;
            if (inBankAlso)
                location |= ItemSearchLocation.Bank;

            List<Item> itemList = new();
            ForEachItem(location, item =>
            {
                if (item.GetEntry() == entry)
                    itemList.Add(item);

                return true;
            });

            return itemList;
        }

        public bool HasItemCount(int item, int count = 1, bool inBankAlso = false)
        {
            ItemSearchLocation location = ItemSearchLocation.Equipment | ItemSearchLocation.Inventory | ItemSearchLocation.ReagentBank;
            if (inBankAlso)
                location |= ItemSearchLocation.Bank;

            int currentCount = 0;
            return !ForEachItem(location, pItem =>
            {
                if (pItem != null && pItem.GetEntry() == item && !pItem.IsInTrade())
                {
                    currentCount += pItem.GetCount();
                    if (currentCount >= count)
                        return false;
                }

                return true;
            });
        }

        public bool IsValidPos(ItemPos pos, bool explicit_pos)
        {
            return pos.IsValid(GetInventorySlotCount(), explicit_pos, (bag) =>
            {
                if (GetBagByPos(bag) is Bag pBag)
                    return pBag.GetBagSize();

                return default;
            });
        }

        int GetItemCountWithLimitCategory(int limitCategory, Item skipItem)
        {
            int count = 0;
            ForEachItem(ItemSearchLocation.Everywhere, item =>
            {
                if (item != skipItem)
                {
                    ItemTemplate pProto = item.GetTemplate();
                    if (pProto != null)
                        if (pProto.GetItemLimitCategory() == limitCategory)
                            count += item.GetCount();
                }
                return true;
            });

            return count;
        }

        public byte GetItemLimitCategoryQuantity(ItemLimitCategoryRecord limitEntry)
        {
            byte limit = limitEntry.Quantity;

            var limitConditions = Global.DB2Mgr.GetItemLimitCategoryConditions(limitEntry.Id);
            foreach (ItemLimitCategoryConditionRecord limitCondition in limitConditions)
            {
                PlayerConditionRecord playerCondition = CliDB.PlayerConditionStorage.LookupByKey(limitCondition.PlayerConditionID);
                if (playerCondition == null || ConditionManager.IsPlayerMeetingCondition(this, playerCondition))
                    limit += (byte)limitCondition.AddQuantity;
            }

            return limit;
        }

        public void DestroyConjuredItems(bool update)
        {
            // used when entering arena
            // destroys all conjured items
            Log.outDebug(LogFilter.Player, "STORAGE: DestroyConjuredItems");

            // in inventory
            int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
            for (byte i = InventorySlots.ItemStart; i < inventoryEnd; i++)
            {
                Item pItem = GetItemByPos(i);
                if (pItem != null)
                {
                    if (pItem.IsConjuredConsumable())
                        DestroyItem(i, update);
                }
            }

            // in inventory bags
            for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; i++)
            {
                Bag pBag = GetBagByPos(i);
                if (pBag != null)
                {
                    for (byte j = 0; j < pBag.GetBagSize(); j++)
                    {
                        Item pItem = pBag.GetItemByPos(j);
                        if (pItem != null)
                            if (pItem.IsConjuredConsumable())
                                DestroyItem(new(j, i), update);
                    }
                }
            }

            // in equipment and bag list
            for (byte i = EquipmentSlot.Start; i < InventorySlots.BagEnd; i++)
            {
                Item pItem = GetItemByPos(i);
                if (pItem != null)
                    if (pItem.IsConjuredConsumable())
                        DestroyItem(i, update);
            }
        }
        void DestroyZoneLimitedItem(bool update, int new_zone)
        {
            Log.outDebug(LogFilter.Player, $"STORAGE: DestroyZoneLimitedItem in map {GetMapId()} and area {new_zone}.");

            // in inventory
            int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
            for (byte i = InventorySlots.ItemStart; i < inventoryEnd; i++)
            {
                Item pItem = GetItemByPos(i);
                if (pItem != null)
                    if (pItem.IsLimitedToAnotherMapOrZone(GetMapId(), new_zone))
                        DestroyItem(i, update);
            }

            for (byte i = InventorySlots.KeyringStart; i < InventorySlots.KeyringEnd; ++i)
            {
                Item pItem = GetItemByPos(i);
                if (pItem != null)
                    if (pItem.IsLimitedToAnotherMapOrZone(GetMapId(), new_zone))
                        DestroyItem(i, update);
            }

            // in inventory bags
            for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; i++)
            {
                Bag pBag = GetBagByPos(i);
                if (pBag != null)
                {
                    for (byte j = 0; j < pBag.GetBagSize(); j++)
                    {
                        Item pItem = pBag.GetItemByPos(j);
                        if (pItem != null)
                            if (pItem.IsLimitedToAnotherMapOrZone(GetMapId(), new_zone))
                                DestroyItem(new(j, i), update);
                    }
                }
            }

            // in equipment and bag list
            for (byte i = EquipmentSlot.Start; i < InventorySlots.BagEnd; i++)
            {
                Item pItem = GetItemByPos(i);
                if (pItem != null)
                    if (pItem.IsLimitedToAnotherMapOrZone(GetMapId(), new_zone))
                        DestroyItem(i, update);
            }
        }

        public InventoryResult CanRollNeedForItem(ItemTemplate proto, Map map, bool restrictOnlyLfg)
        {
            if (restrictOnlyLfg)
            {
                if (GetGroup() == null || !GetGroup().IsLFGGroup())
                    return InventoryResult.Ok;    // not in LFG group

                // check if looted object is inside the lfg dungeon
                if (!Global.LFGMgr.InLfgDungeonMap(GetGroup().GetGUID(), map.GetId(), map.GetDifficultyID()))
                    return InventoryResult.Ok;
            }

            if (proto == null)
                return InventoryResult.ItemNotFound;

            // Used by group, function GroupLoot, to know if a prototype can be used by a player
            if (proto.GetAllowableClass().HasClass(GetClass()) || !proto.GetAllowableRace().HasRace(GetRace()))
                return InventoryResult.CantEquipEver;

            if (proto.GetRequiredSpell() != 0 && !HasSpell(proto.GetRequiredSpell()))
                return InventoryResult.ProficiencyNeeded;

            if (proto.GetRequiredSkill() != 0)
            {
                if (GetSkillValue(proto.GetRequiredSkill()) == 0)
                    return InventoryResult.ProficiencyNeeded;
                else if (GetSkillValue(proto.GetRequiredSkill()) < proto.GetRequiredSkillRank())
                    return InventoryResult.CantEquipSkill;
            }

            if (proto.GetClass() == ItemClass.Weapon && GetSkillValue(proto.GetSkill()) == 0)
                return InventoryResult.ProficiencyNeeded;

            if (proto.GetClass() == ItemClass.Armor && proto.GetInventoryType() != InventoryType.Cloak)
            {
                ChrClassesRecord classesEntry = CliDB.ChrClassesStorage.LookupByKey((int)GetClass());
                if ((classesEntry.ArmorTypeMask & 1 << (int)proto.GetSubClass()) == 0)
                    return InventoryResult.ClientLockedOut;
            }

            return InventoryResult.Ok;
        }

        public void AddItemToBuyBackSlot(Item pItem)
        {
            if (pItem != null)
            {
                var slot = m_currentBuybackSlot;
                // if current back slot non-empty search oldest or free
                if (m_items[slot] != null)
                {
                    TimeSpan oldest_time = m_activePlayerData.BuybackTimestamp[0];
                    var oldest_slot = InventorySlots.BuyBackStart;

                    for (byte i = InventorySlots.BuyBackStart + 1; i < InventorySlots.BuyBackEnd; ++i)
                    {
                        // found empty
                        if (m_items[i] == null)
                        {
                            oldest_slot = i;
                            break;
                        }

                        TimeSpan i_time = m_activePlayerData.BuybackTimestamp[i - InventorySlots.BuyBackStart];
                        if (oldest_time > i_time)
                        {
                            oldest_time = i_time;
                            oldest_slot = i;
                        }
                    }

                    // find oldest
                    slot = oldest_slot;
                }

                RemoveItemFromBuyBackSlot(slot, true);
                Log.outDebug(LogFilter.Player,
                    $"STORAGE: AddItemToBuyBackSlot item = {pItem.GetEntry()}, slot = {slot}");

                m_items[slot] = pItem;
                ServerTime time = LoopTime.ServerTime;
                TimeSpan etime = time - m_logintime + (Hours)30;
                var eslot = (byte)(slot - InventorySlots.BuyBackStart);

                SetInvSlot(slot, pItem.GetGUID());
                ItemTemplate proto = pItem.GetTemplate();
                if (proto != null)
                    SetBuybackPrice(eslot, (uint)(proto.GetSellPrice() * pItem.GetCount()));
                else
                    SetBuybackPrice(eslot, 0);

                SetBuybackTimestamp(eslot, etime);

                // move to next (for non filled list is move most optimized choice)
                if (m_currentBuybackSlot < InventorySlots.BuyBackEnd - 1)
                    ++m_currentBuybackSlot;
            }
        }

        public bool BuyCurrencyFromVendorSlot(ObjectGuid vendorGuid, int vendorSlot, int currency, int count)
        {
            // cheating attempt
            if (count < 1)
                count = 1;

            if (!IsAlive())
                return false;

            CurrencyTypesRecord proto = CliDB.CurrencyTypesStorage.LookupByKey(currency);
            if (proto == null)
            {
                SendBuyError(BuyResult.CantFindItem, null, currency);
                return false;
            }

            Creature creature = GetNPCIfCanInteractWith(vendorGuid, NPCFlags1.Vendor, NPCFlags2.None);
            if (creature == null)
            {
                Log.outDebug(LogFilter.Network,
                    $"WORLD: BuyCurrencyFromVendorSlot - {vendorGuid} not found or you can't interact with him.");

                SendBuyError(BuyResult.DistanceTooFar, null, currency);
                return false;
            }

            VendorItemData vItems = creature.GetVendorItems();
            if (vItems == null || vItems.Empty())
            {
                SendBuyError(BuyResult.CantFindItem, creature, currency);
                return false;
            }

            if (vendorSlot >= vItems.GetItemCount())
            {
                SendBuyError(BuyResult.CantFindItem, creature, currency);
                return false;
            }

            VendorItem crItem = vItems.GetItem(vendorSlot);
            // store diff item (cheating)
            if (crItem == null || crItem.item != currency || crItem.Type != ItemVendorType.Currency)
            {
                SendBuyError(BuyResult.CantFindItem, creature, currency);
                return false;
            }

            if ((count % crItem.maxcount) != 0)
            {
                SendEquipError(InventoryResult.CantBuyQuantity);
                return false;
            }

            var stacks = count / crItem.maxcount;
            ItemExtendedCostRecord iece;
            if (crItem.ExtendedCost != 0)
            {
                iece = CliDB.ItemExtendedCostStorage.LookupByKey(crItem.ExtendedCost);
                if (iece == null)
                {
                    Log.outError(LogFilter.Player,
                        $"Currency {currency} have wrong ExtendedCost field value {crItem.ExtendedCost}");
                    return false;
                }

                for (byte i = 0; i < ItemConst.MaxItemExtCostItems; ++i)
                {
                    if (iece.ItemID[i] != 0 && !HasItemCount(iece.ItemID[i], (iece.ItemCount[i] * stacks)))
                    {
                        SendEquipError(InventoryResult.VendorMissingTurnins);
                        return false;
                    }
                }

                for (byte i = 0; i < ItemConst.MaxItemExtCostCurrencies; ++i)
                {
                    if (iece.CurrencyID[i] == 0)
                        continue;

                    CurrencyTypesRecord entry = CliDB.CurrencyTypesStorage.LookupByKey(iece.CurrencyID[i]);
                    if (entry == null)
                    {
                        SendBuyError(BuyResult.CantFindItem, creature, currency); // Find correct error
                        return false;
                    }

                    if (iece.Flags.HasAnyFlag((byte)((uint)ItemExtendedCostFlags.RequireSeasonEarned1 << i)))
                    {
                        // Not implemented
                        SendEquipError(InventoryResult.VendorMissingTurnins); // Find correct error
                        return false;
                    }
                    else if (!HasCurrency(iece.CurrencyID[i], (iece.CurrencyCount[i] * stacks)))
                    {
                        SendEquipError(InventoryResult.VendorMissingTurnins); // Find correct error
                        return false;
                    }
                }

                // check for personal arena rating requirement
                if (GetMaxPersonalArenaRatingRequirement(iece.ArenaBracket) < iece.RequiredArenaRating)
                {
                    // probably not the proper equip err
                    SendEquipError(InventoryResult.CantEquipRank);
                    return false;
                }

                if (iece.MinFactionID != 0 && (uint)GetReputationRank(iece.MinFactionID) < iece.RequiredAchievement)
                {
                    SendBuyError(BuyResult.ReputationRequire, creature, currency);
                    return false;
                }

                if (iece.Flags.HasAnyFlag((byte)ItemExtendedCostFlags.RequireGuild) && GetGuildId() == 0)
                {
                    SendEquipError(InventoryResult.VendorMissingTurnins); // Find correct error
                    return false;
                }

                if (iece.RequiredAchievement != 0 && !HasAchieved(iece.RequiredAchievement))
                {
                    SendEquipError(InventoryResult.VendorMissingTurnins); // Find correct error
                    return false;
                }
            }
            else // currencies have no price defined, can only be bought with ExtendedCost
            {
                SendBuyError(BuyResult.CantFindItem, null, currency);
                return false;
            }

            AddCurrency(currency, count, CurrencyGainSource.Vendor);
            if (iece != null)
            {
                for (byte i = 0; i < ItemConst.MaxItemExtCostItems; ++i)
                {
                    if (iece.ItemID[i] == 0)
                        continue;

                    DestroyItemCount(iece.ItemID[i], iece.ItemCount[i] * stacks, true);
                }

                for (byte i = 0; i < ItemConst.MaxItemExtCostCurrencies; ++i)
                {
                    if (iece.CurrencyID[i] == 0)
                        continue;

                    if (iece.Flags.HasAnyFlag((byte)((uint)ItemExtendedCostFlags.RequireSeasonEarned1 << i)))
                        continue;

                    RemoveCurrency(iece.CurrencyID[i], iece.CurrencyCount[i] * stacks, CurrencyDestroyReason.Vendor);
                }
            }

            return true;
        }

        public bool BuyItemFromVendorSlot(ObjectGuid vendorguid, int vendorslot, int item, byte count, ItemPos pos)
        {
            // cheating attempt
            if (count < 1)
                count = 1;

            if (!IsAlive())
                return false;

            ItemTemplate pProto = Global.ObjectMgr.GetItemTemplate(item);
            if (pProto == null)
            {
                SendBuyError(BuyResult.CantFindItem, null, item);
                return false;
            }

            if (!pProto.GetAllowableClass().HasClass(GetClass()) && pProto.GetBonding() == ItemBondingType.OnAcquire && !IsGameMaster())
            {
                SendBuyError(BuyResult.CantFindItem, null, item);
                return false;
            }

            if (!IsGameMaster() && ((pProto.HasFlag(ItemFlags2.FactionHorde) && GetTeam() == Team.Alliance) || (pProto.HasFlag(ItemFlags2.FactionAlliance) && GetTeam() == Team.Horde)))
                return false;

            Creature creature = GetNPCIfCanInteractWith(vendorguid, NPCFlags1.Vendor, NPCFlags2.None);
            if (creature == null)
            {
                Log.outDebug(LogFilter.Network,
                    $"Player.BuyItemFromVendorSlot: Vendor {vendorguid} not found or you can't interact with him.");

                SendBuyError(BuyResult.DistanceTooFar, null, item);
                return false;
            }

            if (!Global.ConditionMgr.IsObjectMeetingVendorItemConditions(creature.GetEntry(), item, this, creature))
            {
                Log.outDebug(LogFilter.Condition,
                    $"Player.BuyItemFromVendorSlot: Player ({GetName()}) " +
                    $"doesn't met for creature entry {creature.GetEntry()} item {item}.");

                SendBuyError(BuyResult.CantFindItem, creature, item);
                return false;
            }

            VendorItemData vItems = creature.GetVendorItems();
            if (vItems == null || vItems.Empty())
            {
                SendBuyError(BuyResult.CantFindItem, creature, item);
                return false;
            }

            if (vendorslot >= vItems.GetItemCount())
            {
                SendBuyError(BuyResult.CantFindItem, creature, item);
                return false;
            }

            VendorItem crItem = vItems.GetItem(vendorslot);
            // store diff item (cheating)
            if (crItem == null || crItem.item != item)
            {
                SendBuyError(BuyResult.CantFindItem, creature, item);
                return false;
            }

            PlayerConditionRecord playerCondition = CliDB.PlayerConditionStorage.LookupByKey(crItem.PlayerConditionId);
            if (playerCondition != null)
            {
                if (!ConditionManager.IsPlayerMeetingCondition(this, playerCondition))
                {
                    SendEquipError(InventoryResult.ItemLocked);
                    return false;
                }
            }

            // check current item amount if it limited
            if (crItem.maxcount != 0)
            {
                if (creature.GetVendorItemCurrentCount(crItem) < count)
                {
                    SendBuyError(BuyResult.ItemAlreadySold, creature, item);
                    return false;
                }
            }

            if (pProto.GetRequiredReputationFaction() != 0
                && (GetReputationRank(pProto.GetRequiredReputationFaction()) < pProto.GetRequiredReputationRank()))
            {
                SendBuyError(BuyResult.ReputationRequire, creature, item);
                return false;
            }

            if (crItem.ExtendedCost != 0)
            {
                // Can only buy full stacks for extended cost
                if ((count % pProto.GetBuyCount()) != 0)
                {
                    SendEquipError(InventoryResult.CantBuyQuantity);
                    return false;
                }

                var stacks = count / pProto.GetBuyCount();
                var iece = CliDB.ItemExtendedCostStorage.LookupByKey(crItem.ExtendedCost);
                if (iece == null)
                {
                    Log.outError(LogFilter.Player,
                        $"Player.BuyItemFromVendorSlot: Item {pProto.GetId()} " +
                        $"have wrong ExtendedCost field value {crItem.ExtendedCost}");
                    return false;
                }

                for (byte i = 0; i < ItemConst.MaxItemExtCostItems; ++i)
                {
                    if (iece.ItemID[i] != 0 && !HasItemCount(iece.ItemID[i], iece.ItemCount[i] * stacks))
                    {
                        SendEquipError(InventoryResult.VendorMissingTurnins);
                        return false;
                    }
                }

                for (byte i = 0; i < ItemConst.MaxItemExtCostCurrencies; ++i)
                {
                    if (iece.CurrencyID[i] == 0)
                        continue;

                    var entry = CliDB.CurrencyTypesStorage.LookupByKey(iece.CurrencyID[i]);
                    if (entry == null)
                    {
                        SendBuyError(BuyResult.CantFindItem, creature, item);
                        return false;
                    }

                    if (iece.Flags.HasAnyFlag((byte)((uint)ItemExtendedCostFlags.RequireSeasonEarned1 << i)))
                    {
                        SendEquipError(InventoryResult.VendorMissingTurnins); // Find correct error
                        return false;
                    }
                    else if (!HasCurrency(iece.CurrencyID[i], iece.CurrencyCount[i] * stacks))
                    {
                        SendEquipError(InventoryResult.VendorMissingTurnins);
                        return false;
                    }
                }

                // check for personal arena rating requirement
                if (GetMaxPersonalArenaRatingRequirement(iece.ArenaBracket) < iece.RequiredArenaRating)
                {
                    // probably not the proper equip err
                    SendEquipError(InventoryResult.CantEquipRank);
                    return false;
                }

                if (iece.MinFactionID != 0 && GetReputationRank(iece.MinFactionID) < iece.MinReputation)
                {
                    SendBuyError(BuyResult.ReputationRequire, creature, item);
                    return false;
                }

                if (iece.Flags.HasAnyFlag((byte)ItemExtendedCostFlags.RequireGuild) && GetGuildId() == 0)
                {
                    SendEquipError(InventoryResult.VendorMissingTurnins); // Find correct error
                    return false;
                }

                if (iece.RequiredAchievement != 0 && !HasAchieved(iece.RequiredAchievement))
                {
                    SendEquipError(InventoryResult.VendorMissingTurnins); // Find correct error
                    return false;
                }
            }

            long price = 0;
            if (pProto.GetBuyPrice() > 0) //Assume price cannot be negative (do not know why it is int32)
            {
                float buyPricePerItem = (float)pProto.GetBuyPrice() / pProto.GetBuyCount();
                var maxCount = (long)(PlayerConst.MaxMoneyAmount / buyPricePerItem);
                if (count > maxCount)
                {
                    Log.outError(LogFilter.Player,
                        $"Player.BuyItemFromVendorSlot: " +
                        $"Player {GetName()} tried to buy {count} item id {pProto.GetId()}, causing overflow.");

                    count = (byte)maxCount;
                }
                price = (long)(buyPricePerItem * count); //it should not exceed MAX_MONEY_AMOUNT

                // reputation discount
                price = (long)Math.Floor(price * GetReputationPriceDiscount(creature));
                price = pProto.GetBuyPrice() > 0 ? Math.Max(1L, price) : price;

                int priceMod = GetTotalAuraModifier(AuraType.ModVendorItemsPrices);
                if (priceMod != 0)
                    price -= MathFunctions.CalculatePct(price, priceMod);

                if (!HasEnoughMoney(price))
                {
                    SendBuyError(BuyResult.NotEnoughtMoney, creature, item);
                    return false;
                }
            }

            if (pos.IsInventoryPos || !pos.IsExplicitPos)
            {
                if (!_StoreOrEquipNewItem(vendorslot, item, count, pos, price, pProto, creature, crItem, true))
                    return false;
            }
            else if (pos.IsEquipmentPos)
            {
                if (count != 1)
                {
                    SendEquipError(InventoryResult.NotEquippable);
                    return false;
                }
                if (!_StoreOrEquipNewItem(vendorslot, item, count, pos, price, pProto, creature, crItem, false))
                    return false;
            }
            else
            {
                SendEquipError(InventoryResult.WrongSlot);
                return false;
            }

            UpdateCriteria(CriteriaType.BuyItemsFromVendors, 1);

            if (crItem.maxcount != 0) // bought
            {
                if (pProto.GetQuality() > ItemQuality.Epic || (pProto.GetQuality() == ItemQuality.Epic
                    && pProto.GetItemLevel() >= GuildConst.MinNewsItemLevel))
                {
                    Guild guild = GetGuild();
                    if (guild != null)
                        guild.AddGuildNews(GuildNews.ItemPurchased, GetGUID(), 0, item);
                }

                return true;
            }

            return false;
        }

        public int GetMaxPersonalArenaRatingRequirement(int minarenaslot)
        {
            // returns the maximal personal arena rating that can be used to purchase items requiring this condition
            // so return max[in arenateams](personalrating[teamtype])
            int max_personal_rating = 0;
            for (byte i = (byte)minarenaslot; i < SharedConst.MaxArenaSlot; ++i)
            {
                int p_rating = GetArenaPersonalRating(i);
                if (max_personal_rating < p_rating)
                    max_personal_rating = p_rating;
            }
            return max_personal_rating;
        }

        public void SendItemRetrievalMail(int itemEntry, int count, ItemContext context)
        {
            MailSender sender = new(MailMessageType.Creature, 34337);
            MailDraft draft = new("Recovered Item", "We recovered a lost item in the twisting nether and noted that it was yours.$B$BPlease find said object enclosed."); // This is the text used in Cataclysm, it probably wasn't changed.
            SQLTransaction trans = new();

            Item item = Item.CreateItem(itemEntry, count, context, null);
            if (item != null)
            {
                item.SaveToDB(trans);
                draft.AddItem(item);
            }

            draft.SendMailTo(trans, new MailReceiver(this, GetGUID().GetCounter()), sender);
            DB.Characters.CommitTransaction(trans);
        }

        public void SetBuybackPrice(int slot, uint price)
        {
            SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.BuybackPrice, slot), price);
        }

        public void SetBuybackTimestamp(int slot, TimeSpan timestamp)
        {
            SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.BuybackTimestamp, slot), timestamp);
        }

        public Item GetItemFromBuyBackSlot(int slot)
        {
            Log.outDebug(LogFilter.Player, $"STORAGE: GetItemFromBuyBackSlot slot = {slot}");
            if (slot >= InventorySlots.BuyBackStart && slot < InventorySlots.BuyBackEnd)
                return m_items[slot];
            return null;
        }

        public void RemoveItemFromBuyBackSlot(int slot, bool del)
        {
            Log.outDebug(LogFilter.Player, $"STORAGE: RemoveItemFromBuyBackSlot slot = {slot}");
            if (slot >= InventorySlots.BuyBackStart && slot < InventorySlots.BuyBackEnd)
            {
                Item pItem = m_items[slot];
                if (pItem != null)
                {
                    pItem.RemoveFromWorld();
                    if (del)
                    {
                        ItemTemplate itemTemplate = pItem.GetTemplate();
                        if (itemTemplate != null)
                            if (itemTemplate.HasFlag(ItemFlags.HasLoot))
                                Global.LootItemStorage.RemoveStoredLootForContainer(pItem.GetGUID().GetCounter());

                        pItem.SetState(ItemUpdateState.Removed, this);
                    }
                }

                m_items[slot] = null;

                var eslot = slot - InventorySlots.BuyBackStart;
                SetInvSlot(slot, ObjectGuid.Empty);
                SetBuybackPrice(eslot, 0);
                SetBuybackTimestamp(eslot, TimeSpan.Zero);

                // if current backslot is filled set to now free slot
                if (m_items[m_currentBuybackSlot] != null)
                    m_currentBuybackSlot = (byte)slot;
            }
        }

        public bool HasItemTotemCategory(int TotemCategory)
        {
            foreach (AuraEffect providedTotemCategory in GetAuraEffectsByType(AuraType.ProvideTotemCategory))
            {
                if (Global.DB2Mgr.IsTotemCategoryCompatibleWith(providedTotemCategory.GetMiscValueB(), TotemCategory))
                    return true;
            }

            int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
            for (byte i = EquipmentSlot.Start; i < inventoryEnd; ++i)
            {
                Item item = GetUseableItemByPos(i);
                if (item != null && Global.DB2Mgr.IsTotemCategoryCompatibleWith(item.GetTemplate().GetTotemCategory(), TotemCategory))
                    return true;
            }

            for (byte i = InventorySlots.KeyringStart; i < InventorySlots.KeyringEnd; ++i)
            {
                Item item = GetUseableItemByPos(i);
                if (item != null && Global.DB2Mgr.IsTotemCategoryCompatibleWith(item.GetTemplate().GetTotemCategory(), TotemCategory))
                    return true;
            }

            for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; ++i)
            {
                Bag bag = GetBagByPos(i);
                if (bag != null)
                {
                    for (byte j = 0; j < bag.GetBagSize(); ++j)
                    {
                        Item item = GetUseableItemByPos(new(j, i));
                        if (item != null && Global.DB2Mgr.IsTotemCategoryCompatibleWith(item.GetTemplate().GetTotemCategory(), TotemCategory))
                            return true;
                    }
                }
            }

            for (byte i = InventorySlots.ChildEquipmentStart; i < InventorySlots.ChildEquipmentEnd; ++i)
            {
                Item item = GetUseableItemByPos(i);
                if (item != null && Global.DB2Mgr.IsTotemCategoryCompatibleWith(item.GetTemplate().GetTotemCategory(), TotemCategory))
                    return true;
            }

            return false;
        }

        public bool CheckAmmoCompatibility(ItemTemplate ammo_proto)
        {
            if (ammo_proto == null)
                return false;

            // check ranged weapon
            Item weapon = GetWeaponForAttack(WeaponAttackType.RangedAttack);
            if (weapon == null || weapon.IsBroken())
                return false;

            ItemTemplate weapon_proto = weapon.GetTemplate();
            if (weapon_proto == null || weapon_proto.GetClass() != ItemClass.Weapon)
                return false;

            // check ammo ws. weapon compatibility
            switch (weapon_proto.GetSubClass().Weapon)
            {
                case ItemSubClassWeapon.Bow:
                case ItemSubClassWeapon.Crossbow:
                    if (ammo_proto.GetSubClass().Projectile != ItemSubClassProjectile.Arrow)
                        return false;
                    break;
                case ItemSubClassWeapon.Gun:
                    if (ammo_proto.GetSubClass().Projectile != ItemSubClassProjectile.Bullet)
                        return false;
                    break;
                default:
                    return false;
            }

            return true;
        }

        public void _ApplyAmmoBonuses()
        {
            // check ammo
            int ammo_id = GetUsedAmmoId();

            if (ammo_id == 0)
                return;

            float currentAmmoDPS;

            ItemTemplate ammo_proto = Global.ObjectMgr.GetItemTemplate(ammo_id);
            if (ammo_proto == null || ammo_proto.GetClass() != ItemClass.Projectile || !CheckAmmoCompatibility(ammo_proto))
                currentAmmoDPS = 0.0f;
            else
                currentAmmoDPS = (ammo_proto.GetMinDamage(0) + ammo_proto.GetMaxDamage(0)) / 2;

            if (currentAmmoDPS == AmmoDPS)
                return;

            AmmoDPS = currentAmmoDPS;

            if (CanModifyStats())
                UpdateDamagePhysical(WeaponAttackType.RangedAttack);
        }

        public void _ApplyItemMods(Item item, byte slot, bool apply, bool updateItemAuras = true, bool onlyForScalingItems = false)
        {
            if (slot >= InventorySlots.BagEnd || item == null)
                return;

            ItemTemplate proto = item.GetTemplate();

            if (proto == null)
                return;

            // not apply/remove mods for broken item
            if (item.IsBroken())
                return;

            Log.outInfo(LogFilter.Player,
                $"Player._ApplyItemMods: Applying mods for item {item.GetGUID()}.");

            if (item.GetSocketType(0) != 0)                              //only (un)equipping of items with sockets can influence metagems, so no need to waste time with normal items
                CorrectMetaGemEnchants(slot, apply);

            _ApplyItemBonuses(item, slot, apply, onlyForScalingItems);
            ApplyItemEquipSpell(item, apply);

            if (updateItemAuras)
            {
                ApplyItemDependentAuras(item, apply);
                var attackType = GetAttackBySlot(slot);
                if (attackType.HasValue)
                    UpdateWeaponDependentAuras(attackType.Value);
            }

            ApplyEnchantment(item, apply);

            Log.outDebug(LogFilter.Player, "Player._ApplyItemMods: complete.");
        }

        public void _ApplyItemBonuses(Item item, byte slot, bool apply, bool onlyForScalingItems = false)
        {
            ItemTemplate proto = item.GetTemplate();
            if (slot >= InventorySlots.BagEnd || proto == null)
                return;

            var ssd = proto.GetScalingStatDistribution();
            var ssv = proto.GetScalingStatValue(GetLevel());

            if (onlyForScalingItems && (ssd == null || ssv == null))
                return;

            for (byte i = 0; i < ItemConst.MaxStats; ++i)
            {
                ItemModType statType = 0;
                var val = 0;

                if (ssd != null && ssv != null)
                {
                    statType = ssd.StatID(i);
                    if (statType == ItemModType.None)
                        continue;

                    val = (ssv.getSSDMultiplier(proto.GetScalingStatValueID()) * ssd.Bonus[i]) / 10000;
                }
                else
                {
                    statType = proto.GetStatModifierBonusStat(i);
                    if (statType == ItemModType.None)
                        continue;

                    val = proto.GetStatModifierBonusAmount(i);
                }

                if (val == 0)
                    continue;

                float combatRatingMultiplier = 1.0f;

                switch (statType)
                {
                    case ItemModType.Mana:
                        StatMods.ModifyFlat(UnitMods.Mana, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.Health:                           // modify HP
                        StatMods.ModifyFlat(UnitMods.Health, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.Agility:                          // modify agility
                        StatMods.ModifyFlat(UnitMods.StatAgility, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.Strength:                         //modify strength
                        StatMods.ModifyFlat(UnitMods.StatStrength, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.Intellect:                        //modify intellect
                        StatMods.ModifyFlat(UnitMods.StatIntellect, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.Spirit:                           //modify spirit
                        StatMods.ModifyFlat(UnitMods.StatSpirit, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.Stamina:                          //modify stamina
                        StatMods.ModifyFlat(UnitMods.StatStamina, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.DefenseSkillRating:
                        ApplyRatingMod(CombatRating.DefenseSkill, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.DodgeRating:
                        ApplyRatingMod(CombatRating.Dodge, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.ParryRating:
                        ApplyRatingMod(CombatRating.Parry, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.BlockRating:
                        ApplyRatingMod(CombatRating.Block, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.HitMeleeRating:
                        ApplyRatingMod(CombatRating.HitMelee, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.HitRangedRating:
                        ApplyRatingMod(CombatRating.HitRanged, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.HitSpellRating:
                        ApplyRatingMod(CombatRating.HitSpell, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.CritMeleeRating:
                        ApplyRatingMod(CombatRating.CritMelee, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.CritRangedRating:
                        ApplyRatingMod(CombatRating.CritRanged, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.CritSpellRating:
                        ApplyRatingMod(CombatRating.CritSpell, (int)(val * combatRatingMultiplier), apply);
                        break;
                    //case ItemModType.HitTakenMeleeRating:
                    //    ApplyRatingMod(CombatRating.HitTakenMelee, (int)val, apply);
                    //    break;
                    //case ItemModType.HitTakenRangedRating:
                    //    ApplyRatingMod(CombatRating.HitTakenRanged, (int)val, apply);
                    //    break;
                    //case ItemModType.HitTakenSpellRating:
                    //    ApplyRatingMod(CombatRating.HitTakenSpell, (int)val, apply);
                    //    break;
                    //case ItemModType.CritTakenMeleeRating:
                    //    ApplyRatingMod(CombatRating.CritTakenMelee, (int)val, apply);
                    //    break;
                    //case ItemModType.CritTakenRangedRating:
                    //    ApplyRatingMod(CombatRating.CritTakenRanged, (int)val, apply);
                    //    break;
                    //case ItemModType.CritTakenSpellRating:
                    //    ApplyRatingMod(CombatRating.CritTakenSpell, (int)val, apply);
                    //    break;
                    case ItemModType.HasteMeleeRating:
                        ApplyRatingMod(CombatRating.HasteMelee, val, apply);
                        break;
                    case ItemModType.HasteRangedRating:
                        ApplyRatingMod(CombatRating.HasteRanged, val, apply);
                        break;
                    case ItemModType.HasteSpellRating:
                        ApplyRatingMod(CombatRating.HasteSpell, val, apply);
                        break;
                    case ItemModType.HitRating:
                        ApplyRatingMod(CombatRating.HitMelee, (int)(val * combatRatingMultiplier), apply);
                        ApplyRatingMod(CombatRating.HitRanged, (int)(val * combatRatingMultiplier), apply);
                        ApplyRatingMod(CombatRating.HitSpell, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.CritRating:
                        ApplyRatingMod(CombatRating.CritMelee, (int)(val * combatRatingMultiplier), apply);
                        ApplyRatingMod(CombatRating.CritRanged, (int)(val * combatRatingMultiplier), apply);
                        ApplyRatingMod(CombatRating.CritSpell, (int)(val * combatRatingMultiplier), apply);
                        break;
                    //case ItemModType.HitTakenRating: // Unused since 3.3.5
                    //    ApplyRatingMod(CombatRating.HitTakenMelee, (int)val, apply);
                    //    ApplyRatingMod(CombatRating.HitTakenRanged, (int)val, apply);
                    //    ApplyRatingMod(CombatRating.HitTakenSpell, (int)val, apply);
                    //    break;
                    //case ItemModType.CritTakenRating: // Unused since 3.3.5
                    //    ApplyRatingMod(CombatRating.CritTakenMelee, (int)val, apply);
                    //    ApplyRatingMod(CombatRating.CritTakenRanged, (int)val, apply);
                    //    ApplyRatingMod(CombatRating.CritTakenSpell, (int)val, apply);
                    //    break;
                    //case ItemModType.ResilienceRating:
                    //    ApplyRatingMod(CombatRating.ResiliencePlayerDamage, (int)(val * combatRatingMultiplier), apply);
                    //    break;
                    case ItemModType.HasteRating:
                        ApplyRatingMod(CombatRating.HasteMelee, (int)(val * combatRatingMultiplier), apply);
                        ApplyRatingMod(CombatRating.HasteRanged, (int)(val * combatRatingMultiplier), apply);
                        ApplyRatingMod(CombatRating.HasteSpell, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.ExpertiseRating:
                        ApplyRatingMod(CombatRating.Expertise, (int)(val * combatRatingMultiplier), apply);
                        break;
                    case ItemModType.AttackPower:
                        StatMods.ModifyFlat(UnitMods.AttackPowerMelee, val, apply, UnitModType.TotalPermanent);
                        StatMods.ModifyFlat(UnitMods.AttackPowerRanged, val, apply, UnitModType.TotalPermanent);
                        break;
                    case ItemModType.RangedAttackPower:
                        StatMods.ModifyFlat(UnitMods.AttackPowerRanged, val, apply, UnitModType.TotalPermanent);
                        break;
                    case ItemModType.ManaRegeneration:
                        ApplyManaRegenBonus(val, apply);
                        break;
                    case ItemModType.ArmorPenetrationRating:
                        ApplyRatingMod(CombatRating.ArmorPenetration, val, apply);
                        break;
                    case ItemModType.SpellPower:
                        ApplySpellPowerBonus(val, apply);
                        break;
                    case ItemModType.HealthRegen:
                        ApplyHealthRegenBonus(val, apply);
                        break;
                    case ItemModType.SpellPenetration:
                        ApplySpellPenetrationBonus(val, apply);
                        break;
                    case ItemModType.BlockValue:
                        HandleBaseModFlatValue(BaseModGroup.ShieldBlockValue, val, apply);
                        break;
                    //case ItemModType.MasteryRating:
                    //    ApplyRatingMod(CombatRating.Mastery, (int)(val * combatRatingMultiplier), apply);
                    //    break;
                    case ItemModType.ExtraArmor:
                        StatMods.ModifyFlat(UnitMods.Armor, val, apply, UnitModType.TotalPermanent);
                        break;
                    case ItemModType.FireResistance:
                        StatMods.ModifyFlat(UnitMods.ResistanceFire, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.FrostResistance:
                        StatMods.ModifyFlat(UnitMods.ResistanceFrost, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.HolyResistance:
                        StatMods.ModifyFlat(UnitMods.ResistanceHoly, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.ShadowResistance:
                        StatMods.ModifyFlat(UnitMods.ResistanceShadow, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.NatureResistance:
                        StatMods.ModifyFlat(UnitMods.ResistanceNature, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.ArcaneResistance:
                        StatMods.ModifyFlat(UnitMods.ResistanceArcane, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.AgiStrInt:
                        StatMods.ModifyFlat(UnitMods.StatAgility, val, apply, UnitModType.BasePermanent);
                        StatMods.ModifyFlat(UnitMods.StatStrength, val, apply, UnitModType.BasePermanent);
                        StatMods.ModifyFlat(UnitMods.StatIntellect, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.AgiStr:
                        StatMods.ModifyFlat(UnitMods.StatAgility, val, apply, UnitModType.BasePermanent);
                        StatMods.ModifyFlat(UnitMods.StatStrength, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.AgiInt:
                        StatMods.ModifyFlat(UnitMods.StatAgility, val, apply, UnitModType.BasePermanent);
                        StatMods.ModifyFlat(UnitMods.StatIntellect, val, apply, UnitModType.BasePermanent);
                        break;
                    case ItemModType.StrInt:
                        StatMods.ModifyFlat(UnitMods.StatStrength, val, apply, UnitModType.BasePermanent);
                        StatMods.ModifyFlat(UnitMods.StatIntellect, val, apply, UnitModType.BasePermanent);
                        break;
                }
            }

            // Apply Spell Power from ScalingStatValue if set
            if (ssv != null)
            {
                if (ssv.getSpellBonus(proto.GetScalingStatValueID()) is int spellbonus && spellbonus != 0)
                    ApplySpellPowerBonus(spellbonus, apply);
            }

            for (SpellSchools i = 0; i < SpellSchools.Max; ++i)
            {
                var school = i;
                var resistance = proto.GetResistance(school);

                if (school == SpellSchools.Normal && ssv != null)
                {
                    if (ssv.getArmorMod(proto.GetScalingStatValueID()) is int ssvarmor && ssvarmor != 0)
                        resistance = ssvarmor;
                }

                if (resistance != 0)
                    StatMods.ModifyFlat(UnitMods.ResistanceStart + (int)i, resistance, apply, UnitModType.BasePermanent);
            }

            if (proto.GetShieldBlockValue(proto.GetItemLevel()) is int shieldBlockValue && shieldBlockValue != 0)
            {
                if (proto.GetClass() == ItemClass.Armor && proto.GetSubClass().Armor == ItemSubClassArmor.Shield)
                    SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ShieldBlock), apply ? shieldBlockValue : 0);
            }

            var attType = GetAttackBySlot(slot);
            if (attType.HasValue)
            {
                _ApplyWeaponDamage(slot, item, apply);
            }

            if (IsInFeralForm())
                ApplyFeralAPBonus(proto.GetFeralBonus(GetLevel()), apply);                
        }

        void ApplyItemEquipSpell(Item item, bool apply, bool formChange = false)
        {
            if (item == null || item.GetTemplate().HasFlag(ItemFlags.Legacy))
                return;

            foreach (ItemEffectRecord effectData in item.GetEffects())
            {
                // wrong triggering Type
                if (apply && effectData.TriggerType != ItemSpelltriggerType.OnEquip)
                    continue;

                // check if it is valid spell
                SpellInfo spellproto = Global.SpellMgr.GetSpellInfo(effectData.SpellID, Difficulty.None);
                if (spellproto == null)
                    continue;

                if (effectData.ChrSpecializationID != 0 && (ChrSpecialization)effectData.ChrSpecializationID != GetPrimarySpecialization())
                    continue;

                ApplyEquipSpell(spellproto, item, apply, formChange);
            }
        }

        public void ApplyEquipSpell(SpellInfo spellInfo, Item item, bool apply, bool formChange = false)
        {
            if (apply)
            {
                // Cannot be used in this stance/form
                if (spellInfo.CheckShapeshift(GetShapeshiftForm()) != SpellCastResult.SpellCastOk)
                    return;

                if (formChange)                                    // check aura active state from other form
                {
                    foreach (var pair in GetAppliedAurasCopy())
                    {
                        if (pair.Key != spellInfo.Id)
                            continue;

                        if (item == null || pair.Value.GetBase().GetCastItemGUID() == item.GetGUID())
                            return;
                    }
                }

                Log.outDebug(LogFilter.Player,
                    $"WORLD: cast {(item != null ? "item" : "itemset")} Equip spellId - {spellInfo.Id}");

                CastSpell(this, spellInfo.Id, new CastSpellExtraArgs(item));
            }
            else
            {
                if (formChange)                                     // check aura compatibility
                {
                    // Cannot be used in this stance/form
                    if (spellInfo.CheckShapeshift(GetShapeshiftForm()) == SpellCastResult.SpellCastOk)
                        return;                                     // and remove only not compatible at form change
                }

                if (item != null)
                    RemoveAurasDueToItemSpell(spellInfo.Id, item.GetGUID());  // un-apply all spells, not only at-equipped
                else
                    RemoveAurasDueToSpell(spellInfo.Id);           // un-apply spell (item set case)
            }
        }

        void ApplyEquipCooldown(Item pItem)
        {
            if (pItem.GetTemplate().HasFlag(ItemFlags.NoEquipCooldown))
                return;

            ServerTime now = LoopTime.ServerTime;
            TimeSpan equipCooldown = (Seconds)30;  //Always 30secs?

            foreach (ItemEffectRecord effectData in pItem.GetEffects())
            {
                SpellInfo effectSpellInfo = Global.SpellMgr.GetSpellInfo(effectData.SpellID, Difficulty.None);
                if (effectSpellInfo == null)
                    continue;

                // apply proc cooldown to equip auras if we have any
                if (effectData.TriggerType == ItemSpelltriggerType.OnEquip)
                {
                    SpellProcEntry procEntry = Global.SpellMgr.GetSpellProcEntry(effectSpellInfo);
                    if (procEntry == null)
                        continue;

                    Aura itemAura = GetAura(effectData.SpellID, GetGUID(), pItem.GetGUID());
                    if (itemAura != null)
                        itemAura.AddProcCooldown(procEntry, now);

                    continue;
                }

                // no spell
                if (effectData.SpellID == 0)
                    continue;

                // wrong triggering Type
                if (effectData.TriggerType != ItemSpelltriggerType.OnUse)
                    continue;

                // Don't replace longer cooldowns by equip cooldown if we have any.
                if (GetSpellHistory().GetRemainingCooldown(effectSpellInfo) > equipCooldown)
                    continue;

                GetSpellHistory().AddCooldown(effectData.SpellID, pItem.GetEntry(), equipCooldown);

                ItemCooldown data = new();
                data.ItemGuid = pItem.GetGUID();
                data.SpellID = effectData.SpellID;
                data.Cooldown = (Milliseconds)equipCooldown;
                SendPacket(data);
            }
        }

        public void ApplyItemLootedSpell(Item item, bool apply)
        {
            if (item.GetTemplate().HasFlag(ItemFlags.Legacy))
                return;

            var lootedEffect = item.GetEffects().FirstOrDefault(effectData => effectData.TriggerType == ItemSpelltriggerType.OnLooted);
            if (lootedEffect != null)
            {
                if (apply)
                    CastSpell(this, lootedEffect.SpellID, item);
                else
                    RemoveAurasDueToItemSpell(lootedEffect.SpellID, item.GetGUID());
            }
        }

        public void ApplyItemLootedSpell(ItemTemplate itemTemplate)
        {
            if (itemTemplate.HasFlag(ItemFlags.Legacy))
                return;

            foreach (var effect in itemTemplate.Effects)
            {
                if (effect.TriggerType != ItemSpelltriggerType.OnLooted)
                    continue;

                CastSpell(this, effect.SpellID, true);
            }
        }

        void _RemoveAllItemMods()
        {
            Log.outDebug(LogFilter.Player, "_RemoveAllItemMods start.");

            for (byte i = 0; i < InventorySlots.BagEnd; ++i)
            {
                if (m_items[i] != null)
                {
                    ItemTemplate proto = m_items[i].GetTemplate();
                    if (proto == null)
                        continue;

                    // item set bonuses not dependent from item broken state
                    if (proto.GetItemSet() != 0)
                        Item.RemoveItemsSetItem(this, m_items[i]);

                    if (m_items[i].IsBroken() || !CanUseAttackType(GetAttackBySlot(i)))
                        continue;

                    ApplyItemEquipSpell(m_items[i], false);
                    ApplyEnchantment(m_items[i], false);
                }
            }

            for (byte i = 0; i < InventorySlots.BagEnd; ++i)
            {
                if (m_items[i] != null)
                {
                    if (m_items[i].IsBroken() || !CanUseAttackType(GetAttackBySlot(i)))
                        continue;

                    ApplyItemDependentAuras(m_items[i], false);
                    _ApplyItemBonuses(m_items[i], i, false);
                }
            }

            Log.outDebug(LogFilter.Player, "_RemoveAllItemMods complete.");
        }

        void _ApplyAllItemMods()
        {
            Log.outDebug(LogFilter.Player, "_ApplyAllItemMods start.");

            for (byte i = 0; i < InventorySlots.BagEnd; ++i)
            {
                if (m_items[i] != null)
                {
                    if (m_items[i].IsBroken() || !CanUseAttackType(GetAttackBySlot(i)))
                        continue;

                    ApplyItemDependentAuras(m_items[i], true);
                    _ApplyItemBonuses(m_items[i], i, true);

                    var attackType = GetAttackBySlot(i);
                    if (attackType.HasValue)
                        UpdateWeaponDependentAuras(attackType.Value);
                }
            }

            for (byte i = 0; i < InventorySlots.BagEnd; ++i)
            {
                if (m_items[i] != null)
                {
                    ItemTemplate proto = m_items[i].GetTemplate();
                    if (proto == null)
                        continue;

                    // item set bonuses not dependent from item broken state
                    if (proto.GetItemSet() != 0)
                        Item.AddItemsSetItem(this, m_items[i]);

                    if (m_items[i].IsBroken() || !CanUseAttackType(GetAttackBySlot(i)))
                        continue;

                    ApplyItemEquipSpell(m_items[i], true);
                    ApplyEnchantment(m_items[i], true);
                }
            }

            Log.outDebug(LogFilter.Player, "_ApplyAllItemMods complete.");
        }

        public void _ApplyAllLevelScaleItemMods(bool apply)
        {
            for (byte i = 0; i < InventorySlots.BagEnd; ++i)
            {
                if (m_items[i] != null)
                {
                    if (!CanUseAttackType(GetAttackBySlot(i)))
                        continue;

                    _ApplyItemMods(m_items[i], i, apply, true);

                    // Update item sets for heirlooms
                    if (Global.DB2Mgr.GetHeirloomByItemId(m_items[i].GetEntry()) != null && m_items[i].GetTemplate().GetItemSet() != 0)
                    {
                        if (apply)
                            Item.AddItemsSetItem(this, m_items[i]);
                        else
                            Item.RemoveItemsSetItem(this, m_items[i]);
                    }
                }
            }
        }

        public Loot GetLootByWorldObjectGUID(ObjectGuid lootWorldObjectGuid)
        {
            return m_AELootView.FirstOrDefault(pair => pair.Value.GetOwnerGUID() == lootWorldObjectGuid).Value;
        }

        public LootRoll GetLootRoll(ObjectGuid lootObjectGuid, byte lootListId)
        {
            return m_lootRolls.Find(roll => roll.IsLootItem(lootObjectGuid, lootListId));
        }

        public void AddLootRoll(LootRoll roll) { m_lootRolls.Add(roll); }

        public void RemoveLootRoll(LootRoll roll)
        {
            m_lootRolls.Remove(roll);
        }

        //Inventory       
        InventoryResult CanStoreItem_InInventorySlots(byte slot_begin, byte slot_end, List<ItemPosCount> dest, ItemTemplate pProto, ref int count, Item pSrcItem, ItemPos skip, MergeOption mergeOption, ItemPresetMap presetMap)
        {
            //this is never called for non-bag slots so we can do this
            if (pSrcItem != null && pSrcItem.IsNotEmptyBag())
                return InventoryResult.DestroyNonemptyBag;

            for (var j = slot_begin; j < slot_end; j++)
            {
                ItemPos current = j;

                // skip specific slot already processed in first called CanStoreItem_InSpecificSlot
                if (current == skip)
                    continue;

                // Reagent bags are not supported in Classic
                if (new ItemSlot(j).IsReagentBagSlot)
                    continue;

                InventoryResult res = CanStoreItem_InSlot(null, current, dest, pProto, ref count, pSrcItem, mergeOption, presetMap);

                if (res == InventoryResult.WrongSlot)
                    continue;

                if (res == InventoryResult.Ok)
                    return res;
            }

            return InventoryResult.Ok;
        }

        delegate InventoryResult StoreItemPredicate(ItemPos pos, Item sourceItem, Item destItem, ItemTemplate proto);
                
        InventoryResult CanStoreItem_InSlot(StoreItemPredicate predicate, ItemPos pos, List<ItemPosCount> dest, ItemTemplate pProto, ref int count, Item pSrcItem, MergeOption mergeOption, ItemPresetMap presetMap)
        {            
            var itemPreset = GetItemPresetByPos(pos, presetMap);

            // Skip engaged slot
            if (!itemPreset.HasValue)
                return InventoryResult.WrongSlot;

            // consider history of preset items
            ItemPreset preset = itemPreset.Value;

            InventoryResult res = predicate?.Invoke(pos, pSrcItem, preset.Item, pProto) ?? InventoryResult.Ok;
            if (res != InventoryResult.Ok)
                return res;

            // ignore move item (this slot will be empty at move)
            if (preset.Item == pSrcItem)
                preset.Item = null;

            // if merge skip empty, if !merge skip non-empty
            if (mergeOption != MergeOption.FirstAvaible)
            {
                if ((preset.Item != null) != (mergeOption == MergeOption.TryToMerge))
                    return InventoryResult.WrongSlot;
            }

            if (preset.Item != null)
            {
                res = preset.Item.CanBeMergedWith(pProto);
                if (res != InventoryResult.Ok)
                    return InventoryResult.CantStack;
            }

            int need_space = pProto.GetMaxStackSize();
            need_space -= preset.Count;

            //if full stack
            if (need_space == 0)
                return InventoryResult.CantStack;

            if (need_space > count)
                need_space = count;

            dest.Add(new(pos, need_space));

            if (presetMap != null)
            {
                preset.Count += need_space;
                if (preset.Item == null)
                    preset.Item = pSrcItem;

                presetMap[pos] = preset;
            }

            count -= need_space;

            if (count == 0)
                return InventoryResult.Ok;

            return InventoryResult.WrongSlot;
        }

        InventoryResult CanStoreItem_InSpecificSlot(ItemPos pos, List<ItemPosCount> dest, ItemTemplate pProto, ref int count, Item pSrcItem, ItemPresetMap presetMap)
        {
            if (pSrcItem != null)
            {
                if (pSrcItem.IsNotEmptyBag() && !pos.IsBagSlotPos)
                    return InventoryResult.DestroyNonemptyBag;

                if (pSrcItem.HasItemFlag(ItemFieldFlags.Child) && !pos.IsEquipmentPos && !pos.IsChildEquipmentPos)
                    return InventoryResult.WrongBagType3;

                if (!pSrcItem.HasItemFlag(ItemFieldFlags.Child) && pos.IsChildEquipmentPos)
                    return InventoryResult.WrongBagType3;
            }

            StoreItemPredicate predicate = (pos, sourceItem, destItem, proto) =>
            {
                // empty specific slot | ignore move items
                if (destItem == null)
                {
                    // Reagent bags are not supported in Classic
                    if (pos.Slot.IsReagentBagSlot)
                        return InventoryResult.WrongBagType;

                    if (!pos.IsContainerPos)
                    {
                        // keyring case
                        if (pos.Slot.IsKeyringSlot && !proto.GetBagFamily().HasAnyFlag(BagFamilyMask.Keys))
                            return InventoryResult.WrongBagType;

                        // prevent cheating
                        if (pos.Slot.IsBuyBackSlot || pos.Slot >= (byte)PlayerSlots.End)
                            return InventoryResult.WrongBagType;
                    }
                    else
                    {
                        Bag pBag = GetBagByPos(pos.Container, presetMap);
                        if (pBag == null)
                            return InventoryResult.WrongBagType;

                        ItemTemplate pBagProto = pBag.GetTemplate();
                        if (pBagProto == null)
                            return InventoryResult.WrongBagType;

                        if (pos.Slot >= pBagProto.GetContainerSlots())
                            return InventoryResult.WrongBagType;

                        if (!Item.ItemCanGoIntoBag(proto, pBagProto))
                            return InventoryResult.WrongBagType;
                    }

                }
                return InventoryResult.Ok;
            };

            InventoryResult res = CanStoreItem_InSlot(predicate, pos, dest, pProto, ref count, pSrcItem, MergeOption.FirstAvaible, presetMap);

            if (res == InventoryResult.WrongSlot)
                res = InventoryResult.Ok;

            return res;
        }

        InventoryResult CanBankItem_InSpecificSlot(ItemPos pos, List<ItemPosCount> dest, ItemTemplate pProto, ref int count, Item pSrcItem, ItemPresetMap presetMap, bool not_loading)
        {
            if (pSrcItem != null)
            {
                if (pSrcItem.IsNotEmptyBag() && !pos.IsBagSlotPos)
                    return InventoryResult.DestroyNonemptyBag;

                if (pSrcItem.HasItemFlag(ItemFieldFlags.Child) && !pos.IsEquipmentPos && !pos.IsChildEquipmentPos)
                    return InventoryResult.WrongBagType3;

                if (!pSrcItem.HasItemFlag(ItemFieldFlags.Child) && pos.IsChildEquipmentPos)
                    return InventoryResult.WrongBagType3;
            }

            StoreItemPredicate predicate = (pos, sourceItem, destItem, proto) =>
            {
                // empty specific slot | ignore move items
                if (destItem == null)
                {
                    // Reagent bags are not supported in Classic
                    if (pos.Slot.IsReagentBagSlot)
                        return InventoryResult.WrongBagType;

                    if (!pos.IsBankPos)
                        return InventoryResult.NoBankSlot;

                    if (!pos.IsContainerPos)
                    {
                        if (pos.IsBagSlotPos)
                        {
                            if (!sourceItem.IsBag())
                                return InventoryResult.NotABag;

                            if (pos.Slot - InventorySlots.BankBagStart >= GetBankBagSlotCount())
                                return InventoryResult.NoBankSlot;

                            return CanUseItem(sourceItem, not_loading);
                        }
                    }
                    else
                    {
                        Bag pBag = GetBagByPos(pos.Container, presetMap);
                        if (pBag == null)
                            return InventoryResult.WrongBagType;

                        ItemTemplate pBagProto = pBag.GetTemplate();
                        if (pBagProto == null)
                            return InventoryResult.WrongBagType;

                        if (pos.Slot >= pBagProto.GetContainerSlots())
                            return InventoryResult.WrongBagType;

                        if (!Item.ItemCanGoIntoBag(proto, pBagProto))
                            return InventoryResult.WrongBagType;
                    }

                }
                return InventoryResult.Ok;
            };

            InventoryResult res = CanStoreItem_InSlot(predicate, pos, dest, pProto, ref count, pSrcItem, MergeOption.FirstAvaible, presetMap);

            if (res == InventoryResult.WrongSlot)
                res = InventoryResult.Ok;

            return res;
        }

        public void MoveItemFromInventory(ItemPos pos, bool update)
        {
            Item it = GetItemByPos(pos);
            if (it != null)
            {
                RemoveItem(pos, update);
                ItemRemovedQuestCheck(it.GetEntry(), it.GetCount());
                it.SetNotRefundable(this, false, null, false);
                Item.RemoveItemFromUpdateQueueOf(it, this);
                GetSession().GetCollectionMgr().RemoveTemporaryAppearance(it);
                if (it.IsInWorld)
                {
                    it.RemoveFromWorld();
                    it.DestroyForPlayer(this);
                }
            }
        }

        public void MoveItemToInventory(List<ItemPosCount> dest, Item pItem, bool update, bool in_characterInventoryDB = false)
        {
            var itemId = pItem.GetEntry();
            var count = 0;
            dest.ForEach(i => count += i.Count);

            // store item
            Item pLastItem = StoreItem(dest, pItem, update);

            // only set if not merged to existed stack
            if (pLastItem == pItem)
            {
                // update owner for last item (this can be original item with wrong owner
                if (pLastItem.GetOwnerGUID() != GetGUID())
                    pLastItem.SetOwnerGUID(GetGUID());

                // if this original item then it need create record in inventory
                // in case trade we already have item in other player inventory
                pLastItem.SetState(in_characterInventoryDB ? ItemUpdateState.Changed : ItemUpdateState.New, this);

                if (pLastItem.IsBOPTradeable())
                    AddTradeableItem(pLastItem);
            }

            // update quest counters
            ItemAddedQuestCheck(itemId, count);
            UpdateCriteria(CriteriaType.ObtainAnyItem, itemId, count);
        }

        //Bank
        public InventoryResult CanBankItem(ItemPos pos, out List<ItemPosCount> dest, Item pItem, bool forSwap, bool not_loading = true)
        {
            return CanBankItem(pos, out dest, out _, pItem, forSwap ? pos : null, not_loading);
        }

        public InventoryResult CanBankItem(ItemPos pos, out List<ItemPosCount> dest, Item pItem, ItemPresetMap presetMap = null, bool not_loading = true)
        {
            return CanBankItem(pos, out dest, out _, pItem, presetMap, not_loading);
        }

        public InventoryResult CanBankItem(ItemPos pos, out List<ItemPosCount> dest, out int no_space_count, Item pItem, ItemPresetMap presetMap = null, bool not_loading = true, int? count = null)
        {
            dest = null;
            no_space_count = count.HasValue ? count.Value : pItem.GetCount();

            if (pItem == null)
                return InventoryResult.ItemNotFound;

            ItemTemplate itemTemplate = pItem.GetTemplate();
            if (itemTemplate == null)
            {
                return InventoryResult.ItemNotFound;
            }

            if (pItem.m_lootGenerated)
            {
                return InventoryResult.LootGone;
            }

            if (pItem.IsBindedNotWith(this))
            {
                return InventoryResult.NotOwner;
            }

            return CanBankItem(pos, out dest, itemTemplate, no_space_count, pItem, out no_space_count, presetMap, not_loading);
        }

        InventoryResult CanBankItem(ItemPos pos, out List<ItemPosCount> dest, ItemTemplate pProto, int count, Item pItem, out int no_space_count, ItemPresetMap presetMap = null, bool not_loading = true)
        {
            dest = new();
            InventoryResult res;

            Log.outDebug(LogFilter.Player,
                $"STORAGE: CanStoreItem bag = {pos.Container}, slot = {pos.Slot}, item = {pProto.GetId()}, count = {count}");

            #region Check for similar items 
            // can't store this amount similar items
            // The search for similar items occurs in all character slots, including the bank. Therefore, there is no need to use the 'ItemPresetMap' here. We have only these possible options:
            //      1) Moving or swapping an item within the character's inventory(also in the bank) - then this does not matter, because if this one is found, it will be skipped.
            //      2) Receiving an item from outside(by trading or other methods) - this item will not be found anyway)
            //      3) Exchanging similar items when trading, which have a quantity limit (this case is quite rare if there are no custom items, if possible at all)
            InventoryResult TakeMoreSimilarRes = CanTakeMoreSimilarItems(pProto, count, pItem, out no_space_count);
            if (TakeMoreSimilarRes != InventoryResult.Ok)
            {
                if (count == no_space_count)
                    return TakeMoreSimilarRes;

                // We will try store as much as we can.
                count -= no_space_count;
            }
            #endregion

            const byte TryAuto = 0;
            const byte TryInSpecificSlot = 1;
            const byte TryInSpecificContainer = 2;
            byte stage = pos.IsExplicitPos ? TryInSpecificSlot : (pos.IsContainerPos ? TryInSpecificContainer : TryAuto);

            MergeOption mergeOption; // needs for not specific Positions

            switch (stage)
            {
                case TryInSpecificSlot:

                    res = CanBankItem_InSpecificSlot(pos, dest, pProto, ref count, pItem, presetMap, not_loading);
                    if (res != InventoryResult.Ok)
                    {
                        no_space_count += count;
                        return res;
                    }

                    if (count == 0)
                        return TakeMoreSimilarRes;

                    if (pos.IsContainerPos)
                    {
                        goto case TryInSpecificContainer;
                    }
                    else
                    {
                        goto case TryAuto;
                    }

                case TryInSpecificContainer:

                    var beginCount = count;

                    mergeOption = pProto.IsMergeable ? MergeOption.TryToMerge : MergeOption.DontMerge;

                    for (; mergeOption >= MergeOption.DontMerge; mergeOption--)
                    {
                        // we need check 2 time (ignore specialized/non_specialized)
                        res = CanStoreItem_InBagSlot(pos.Container, dest, pProto, ref count, CheckBagSpecilization.Ignore, pItem, pos.Slot, mergeOption, presetMap);
                        if (res != InventoryResult.Ok)
                        {
                            no_space_count += count;
                            return res;
                        }

                        if (count == 0)
                            return TakeMoreSimilarRes;
                    }

                    // This only happens if no items have been stored.
                    if (beginCount == count && dest.Empty())
                    {
                        no_space_count += count;
                        return InventoryResult.BagFull;
                    }

                    goto case TryAuto;

                case TryAuto:

                    mergeOption = pProto.IsMergeable ? MergeOption.TryToMerge : MergeOption.DontMerge;

                    for (; mergeOption >= MergeOption.DontMerge; mergeOption--)
                    {
                        ///Try Store into suitable slot in Inventory
                        {
                            // new bags can be directly equipped
                            if (pItem == null && pProto.GetClass() == ItemClass.Container && pProto.GetSubClass().Container == ItemSubClassContainer.Container &&
                                (pProto.GetBonding() == ItemBondingType.None || pProto.GetBonding() == ItemBondingType.OnAcquire))
                            {
                                CanStoreItem_InInventorySlots(InventorySlots.BankBagStart, InventorySlots.BankBagEnd, dest, pProto, ref count, pItem, pos, mergeOption, presetMap);
                                if (count == 0)
                                    return TakeMoreSimilarRes;
                            }

                            //Let's skip the redundant steps (you can only move non-empty bags to the corresponding slots that have just been checked above)
                            if (pItem != null && pItem.IsNotEmptyBag())
                                continue;

                            //search specialized bag
                            if (pProto.GetBagFamily() != 0)
                            {
                                for (byte i = InventorySlots.BankBagStart; i < InventorySlots.BankBagEnd; i++)
                                {
                                    // we need checkonly specialized
                                    CanStoreItem_InBagSlot(i, dest, pProto, ref count, CheckBagSpecilization.Specialized, pItem, pos, mergeOption, presetMap);
                                    if (count == 0)
                                        return TakeMoreSimilarRes;
                                }
                            }
                        }

                        ///Try Store in any free slot in Bank
                        {
                            //Determining the maximum capacity of a backpack
                            CanStoreItem_InInventorySlots(InventorySlots.BankItemStart, InventorySlots.BankItemEnd, dest, pProto, ref count, pItem, pos, mergeOption, presetMap);
                            if (count == 0)
                                return TakeMoreSimilarRes;
                        }

                        ///Try Store in any free slot into a Bank Bag
                        {
                            for (byte i = InventorySlots.BankBagStart; i < InventorySlots.BankBagEnd; i++)
                            {
                                // we need checkonly non-specialized (Specialized already checked above)
                                CanStoreItem_InBagSlot(i, dest, pProto, ref count, CheckBagSpecilization.NonSpecialized, pItem, pos, mergeOption, presetMap);
                                if (count == 0)
                                    return TakeMoreSimilarRes;
                            }
                        }
                    }

                    no_space_count += count;
                    return InventoryResult.BankFull;
            }

            no_space_count += count;
            return InventoryResult.InternalBagError;
        }

        public Item BankItem(List<ItemPosCount> dest, Item pItem, bool update)
        {
            return StoreItem(dest, pItem, update);
        }

        public uint GetFreeInventorySlotCount(ItemSearchLocation location = ItemSearchLocation.Inventory)
        {
            uint freeSlotCount = 0;

            if (location.HasFlag(ItemSearchLocation.Equipment))
            {
                for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
                {
                    if (GetItemByPos(i) == null)
                        ++freeSlotCount;
                }

                for (byte i = ProfessionSlots.Start; i < ProfessionSlots.End; ++i)
                {
                    if (GetItemByPos(i) == null)
                        ++freeSlotCount;
                }
            }

            if (location.HasFlag(ItemSearchLocation.Inventory))
            {
                int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
                for (byte i = InventorySlots.ItemStart; i < inventoryEnd; ++i)
                {
                    if (GetItemByPos(i) == null)
                        ++freeSlotCount;
                }

                for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; ++i)
                {
                    Bag bag = GetBagByPos(i);
                    if (bag != null)
                    {
                        for (byte j = 0; j < bag.GetBagSize(); ++j)
                        {
                            if (bag.GetItemByPos(j) == null)
                                ++freeSlotCount;
                        }
                    }
                }
            }

            if (location.HasFlag(ItemSearchLocation.Bank))
            {
                for (byte i = InventorySlots.BankItemStart; i < InventorySlots.BankItemEnd; ++i)
                {
                    if (GetItemByPos(i) == null)
                        ++freeSlotCount;
                }

                for (byte i = InventorySlots.BankBagStart; i < InventorySlots.BankBagEnd; ++i)
                {
                    Bag bag = GetBagByPos(i);
                    if (bag != null)
                    {
                        for (byte j = 0; j < bag.GetBagSize(); ++j)
                        {
                            if (bag.GetItemByPos(j) == null)
                                ++freeSlotCount;
                        }
                    }
                }
            }

            if (location.HasFlag(ItemSearchLocation.ReagentBank))
            {
                for (byte i = InventorySlots.ReagentBagStart; i < InventorySlots.ReagentBagEnd; ++i)
                {
                    if (GetItemByPos(i) == null)
                        ++freeSlotCount;
                }
            }

            return freeSlotCount;
        }

        public int GetFreeInventorySpace()
        {
            int freeSpace = 0;

            // Check backpack
            for (byte slot = InventorySlots.ItemStart; slot < InventorySlots.ItemEnd; ++slot)
            {
                Item item = GetItemByPos(slot);
                if (item == null)
                    freeSpace += 1;
            }

            // Check bags
            for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; i++)
            {
                Bag bag = GetBagByPos(i);
                if (bag != null)
                    freeSpace += bag.GetFreeSlots();
            }

            return freeSpace;
        }

        //Bags
        public Bag GetBagByPos(ItemSlot bag, ItemPresetMap presetMap = null)
        {
            Item item = null;

            if (bag.IsBagSlot || bag.IsBankBagSlot || bag.IsReagentBagSlot)
            {
                item = GetItemByPos(bag);

                if (presetMap != null)
                {
                    if (presetMap.TryGetValue(bag, out var preset))
                    {
                        if (preset.HasValue)
                        {
                            item = preset.Value.Item;
                        }
                    }                   
                }

                if (item != null)
                    return item.ToBag();
            }

            return null;
        }
        
        InventoryResult CanStoreItem_InBagSlot(ItemSlot bag, List<ItemPosCount> dest, ItemTemplate pProto, ref int count, CheckBagSpecilization check, Item pSrcItem, ItemPos skip, MergeOption mergeOption, ItemPresetMap presetMap)
        {
            // skip specific bag already processed in first called CanStoreItem_InBag
            if (bag == skip.Container)
                return InventoryResult.WrongBagType;

            // skip not existed bag or self targeted bag
            Bag pBag = GetBagByPos(bag, presetMap);
            if (pBag == null || pBag == pSrcItem)
                return InventoryResult.WrongBagType;

            if (pSrcItem != null)
            {
                if (pSrcItem.IsNotEmptyBag())
                    return InventoryResult.DestroyNonemptyBag;

                if (pSrcItem.HasItemFlag(ItemFieldFlags.Child))
                    return InventoryResult.WrongBagType3;
            }

            ItemTemplate pBagProto = pBag.GetTemplate();
            if (pBagProto == null)
                return InventoryResult.WrongBagType;

            // specialized bag mode or non-specilized
            if (check != CheckBagSpecilization.Ignore)
            {
                if (check > 0 == (pBagProto.GetClass() == ItemClass.Container && pBagProto.GetSubClass().Container == ItemSubClassContainer.Container))
                    return InventoryResult.WrongBagType;
            }

            if (!Item.ItemCanGoIntoBag(pProto, pBagProto))
                return InventoryResult.WrongBagType;

            for (byte j = 0; j < pBag.GetBagSize(); j++)
            {
                // skip specific slot already processed in first called CanStoreItem_InSpecificSlot
                if (j == skip.Slot)
                    continue;

                InventoryResult res = InventoryResult.Ok;

                ItemPos currentPos = new(j, bag);
                res = CanStoreItem_InSlot(null, currentPos, dest, pProto, ref count, pSrcItem, mergeOption, presetMap);
                
                if (res == InventoryResult.Ok)
                    return res;
            }

            return InventoryResult.Ok;
        }

        public int GetUsedAmmoId()
        {
            return m_activePlayerData.AmmoID;
        }

        public void SetUsedAmmoId(int ammoId)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.AmmoID), ammoId);   
        }

        public void SetAmmo(int ammoId)
        {
            // already set

            if (GetUsedAmmoId() == ammoId)
                return;

            // check ammo
            InventoryResult msg = CanUseAmmo(ammoId);
            if (msg != InventoryResult.Ok)
            {
                SendEquipError(msg, null, null, ammoId);
                return;
            }

            SetUsedAmmoId(ammoId);

            _ApplyAmmoBonuses();
        }

        public void RemoveAmmo()
        {
            SetUsedAmmoId(0);

            AmmoDPS = 0.0f;

            if (CanModifyStats())
                UpdateDamagePhysical(WeaponAttackType.RangedAttack);
        }

        public InventoryResult CanUseAmmo(int ammoId)
        {
            Log.outDebug(LogFilter.PlayerItems, $"STORAGE: CanUseAmmo item = {ammoId}");
            if (!IsAlive())
                return InventoryResult.PlayerDead;

            //if (IsStunned())
            //    return InventoryResult.GenericStunned;

            ItemTemplate pProto = Global.ObjectMgr.GetItemTemplate(ammoId);
            if (pProto != null)
            {
                if (pProto.GetInventoryType() != InventoryType.Ammo)
                    return InventoryResult.AmmoOnly;

                InventoryResult res = CanUseItem(pProto);
                if (res != InventoryResult.Ok)
                    return res;

                /*if (GetReputationMgr().GetReputation() < pProto->RequiredReputation)
                    return InventoryResult.CantEquipReputation;
                */

                // Requires No Ammo
                if (HasAura(46699))
                    return InventoryResult.BagFull6; // CantDoThatRightNow?

                return InventoryResult.Ok;
            }

            return InventoryResult.ItemNotFound;
        }

        //Equipment        
        byte FindEquipSlot(ItemTemplate proto, byte slot, bool swap)
        {
            Class playerClass = GetClass();
            byte[] slots = [ItemSlot.Null, ItemSlot.Null, ItemSlot.Null, ItemSlot.Null];
            switch (proto.GetInventoryType())
            {
                case InventoryType.Head:
                    slots[0] = EquipmentSlot.Head;
                    break;
                case InventoryType.Neck:
                    slots[0] = EquipmentSlot.Neck;
                    break;
                case InventoryType.Shoulders:
                    slots[0] = EquipmentSlot.Shoulders;
                    break;
                case InventoryType.Body:
                    slots[0] = EquipmentSlot.Shirt;
                    break;
                case InventoryType.Chest:
                    slots[0] = EquipmentSlot.Chest;
                    break;
                case InventoryType.Robe:
                    slots[0] = EquipmentSlot.Chest;
                    break;
                case InventoryType.Waist:
                    slots[0] = EquipmentSlot.Waist;
                    break;
                case InventoryType.Legs:
                    slots[0] = EquipmentSlot.Legs;
                    break;
                case InventoryType.Feet:
                    slots[0] = EquipmentSlot.Feet;
                    break;
                case InventoryType.Wrists:
                    slots[0] = EquipmentSlot.Wrist;
                    break;
                case InventoryType.Hands:
                    slots[0] = EquipmentSlot.Hands;
                    break;
                case InventoryType.Finger:
                    slots[0] = EquipmentSlot.Finger1;
                    slots[1] = EquipmentSlot.Finger2;
                    break;
                case InventoryType.Trinket:
                    slots[0] = EquipmentSlot.Trinket1;
                    slots[1] = EquipmentSlot.Trinket2;
                    break;
                case InventoryType.Cloak:
                    slots[0] = EquipmentSlot.Cloak;
                    break;
                case InventoryType.Weapon:
                {
                    slots[0] = EquipmentSlot.MainHand;

                    // suggest offhand slot only if know dual wielding
                    // (this will be replace mainhand weapon at auto equip instead unwonted "you don't known dual wielding" ...
                    if (CanDualWield())
                        slots[1] = EquipmentSlot.OffHand;
                    break;
                }
                case InventoryType.Shield:
                    slots[0] = EquipmentSlot.OffHand;
                    break;
                case InventoryType.Ranged:
                    slots[0] = EquipmentSlot.Ranged;
                    break;
                case InventoryType.Weapon2Hand:
                    slots[0] = EquipmentSlot.MainHand;
                    if (CanDualWield() && CanTitanGrip())
                        slots[1] = EquipmentSlot.OffHand;
                    break;
                case InventoryType.Tabard:
                    slots[0] = EquipmentSlot.Tabard;
                    break;
                case InventoryType.WeaponMainhand:
                    slots[0] = EquipmentSlot.MainHand;
                    break;
                case InventoryType.WeaponOffhand:
                    slots[0] = EquipmentSlot.OffHand;
                    break;
                case InventoryType.Holdable:
                    slots[0] = EquipmentSlot.OffHand;
                    break;
                case InventoryType.Thrown:
                    slots[0] = EquipmentSlot.Ranged;
                    break;
                case InventoryType.RangedRight:
                    slots[0] = EquipmentSlot.Ranged;
                    break;
                case InventoryType.Bag:
                    slots[0] = InventorySlots.BagStart + 0;
                    slots[1] = InventorySlots.BagStart + 1;
                    slots[2] = InventorySlots.BagStart + 2;
                    slots[3] = InventorySlots.BagStart + 3;
                    break;
                case InventoryType.Relic:
                {
                    switch (proto.GetSubClass().Armor)
                    {
                        case ItemSubClassArmor.Libram:
                            if (playerClass == Class.Paladin)
                                slots[0] = EquipmentSlot.Ranged;
                            break;
                        case ItemSubClassArmor.Idol:
                            if (playerClass == Class.Druid)
                                slots[0] = EquipmentSlot.Ranged;
                            break;
                        case ItemSubClassArmor.Totem:
                            if (playerClass == Class.Shaman)
                                slots[0] = EquipmentSlot.Ranged;
                            break;
                        case ItemSubClassArmor.Miscellaneous:
                            if (playerClass == Class.Warlock)
                                slots[0] = EquipmentSlot.Ranged;
                            break;
                        case ItemSubClassArmor.Sigil:
                            if (playerClass == Class.DeathKnight)
                                slots[0] = EquipmentSlot.Ranged;
                            break;
                    }
                    break;
                }
                default:
                    return ItemSlot.Null;
            }

            if (slot != ItemSlot.Null)
            {
                if (swap || GetItemByPos(slot) == null)
                {
                    for (byte i = 0; i < 4; ++i)
                    {
                        if (slots[i] == slot)
                            return slot;
                    }
                }
            }
            else
            {
                // search free slot at first
                for (byte i = 0; i < 4; ++i)
                {
                    if (slots[i] != ItemSlot.Null && GetItemByPos(slots[i]) == null)
                    {
                        // in case 2hand equipped weapon (without titan grip) offhand slot empty but not free
                        if (slots[i] != EquipmentSlot.OffHand || !IsTwoHandUsed())
                            return slots[i];
                    }
                }

                // if not found free and can swap return slot with lower item level equipped
                if (swap)
                {
                    var minItemLevel = int.MaxValue;
                    byte minItemLevelIndex = 0;
                    for (byte i = 0; i < 4; ++i)
                    {
                        if (slots[i] != ItemSlot.Null)
                        {
                            Item equipped = GetItemByPos(slots[i]);
                            if (equipped != null)
                            {
                                var itemLevel = equipped.GetItemLevel(this);
                                if (itemLevel < minItemLevel)
                                {
                                    minItemLevel = itemLevel;
                                    minItemLevelIndex = i;
                                }
                            }
                        }
                    }

                    return slots[minItemLevelIndex];
                }
            }

            // no free position
            return ItemSlot.Null;
        }

        InventoryResult CanEquipNewItem(byte slot, out List<ItemPosCount> dest, int item, bool swap)
        {
            Item pItem = Item.CreateItem(item, 1, ItemContext.None, this);
            if (pItem != null)
            {
                InventoryResult result = CanEquipItem(slot, out dest, pItem, swap);
                return result;
            }

            dest = new();
            return InventoryResult.ItemNotFound;
        }

        public InventoryResult CanEquipItem(byte slot, out List<ItemPosCount> dest, Item pItem, bool swap, bool not_loading = true)
        {
            var result = CanEquipItem(slot, out ItemPos destOne, pItem, swap, not_loading);

            dest = new() { new(destOne, 1) };
            return result;
        }

        public InventoryResult CanEquipItem(byte slot, out ItemPos dest, Item pItem, bool swap, bool not_loading = true)
        {
            dest = new();
            if (pItem != null)
            {
                Log.outDebug(LogFilter.Player,
                    $"STORAGE: CanEquipItem slot = {slot}, item = {pItem.GetEntry()}, count = {pItem.GetCount()}");

                ItemTemplate pProto = pItem.GetTemplate();
                if (pProto != null)
                {
                    // item used
                    if (pItem.m_lootGenerated)
                        return InventoryResult.LootGone;

                    if (pItem.IsBindedNotWith(this))
                        return InventoryResult.NotOwner;

                    // check count of items (skip for auto move for same player from bank)
                    InventoryResult res = CanTakeMoreSimilarItems(pItem);
                    if (res != InventoryResult.Ok)
                        return res;

                    // check this only in game
                    if (not_loading)
                    {
                        // May be here should be more stronger checks; STUNNED checked
                        // ROOT, CONFUSED, DISTRACTED, FLEEING this needs to be checked.
                        if (HasUnitState(UnitState.Stunned))
                            return InventoryResult.GenericStunned;

                        if (IsCharmed())
                            return InventoryResult.CantDoThatRightNow; // @todo is this the correct error?

                        // do not allow equipping gear except weapons, offhands, projectiles, relics in
                        // - combat
                        // - in-progress arenas
                        if (!pProto.CanChangeEquipStateInCombat())
                        {
                            if (IsInCombat())
                                return InventoryResult.NotInCombat;

                            Battleground bg = GetBattleground();
                            if (bg != null)
                            {
                                if (bg.IsArena() && bg.GetStatus() == BattlegroundStatus.InProgress)
                                    return InventoryResult.NotDuringArenaMatch;
                            }
                        }

                        if (IsInCombat() && (pProto.GetClass() == ItemClass.Weapon || pProto.GetInventoryType() == InventoryType.Relic) && m_weaponChangeTimer != 0)
                            return InventoryResult.ItemCooldown;

                        Spell currentGenericSpell = GetCurrentSpell(CurrentSpellTypes.Generic);
                        if (currentGenericSpell != null)
                        {
                            if (!currentGenericSpell.GetSpellInfo().HasAttribute(SpellAttr6.AllowEquipWhileCasting))
                                return InventoryResult.ClientLockedOut;
                        }

                        Spell currentChanneledSpell = GetCurrentSpell(CurrentSpellTypes.Channeled);
                        if (currentChanneledSpell != null)
                        {
                            if (!currentChanneledSpell.GetSpellInfo().HasAttribute(SpellAttr6.AllowEquipWhileCasting))
                                return InventoryResult.ClientLockedOut;
                        }
                    }

                    ContentTuningLevels? requiredLevels = null;
                    // check allowed level (extend range to upper values if MaxLevel more or equal max player level, this let GM set high level with 1...max range items)
                    if (pItem.GetQuality() == ItemQuality.Heirloom)
                        requiredLevels = Global.DB2Mgr.GetContentTuningData(pItem.GetScalingContentTuningId(), 0, true);

                    if (requiredLevels.HasValue
                        && requiredLevels.Value.MaxLevel < SharedConst.DefaultMaxPlayerLevel
                        && requiredLevels.Value.MaxLevel < GetLevel()
                        && Global.DB2Mgr.GetHeirloomByItemId(pProto.GetId()) == null)
                    {
                        return InventoryResult.NotEquippable;
                    }

                    byte eslot = FindEquipSlot(pProto, slot, swap);
                    if (eslot == ItemSlot.Null)
                        return InventoryResult.NotEquippable;

                    res = CanUseItem(pItem, not_loading);
                    if (res != InventoryResult.Ok)
                        return res;

                    if (!swap && GetItemByPos(eslot) != null)
                        return InventoryResult.NoSlotAvailable;

                    // if swap ignore item (equipped also)
                    InventoryResult res2 = CanEquipUniqueItem(pItem, swap ? eslot : ItemSlot.Null);
                    if (res2 != InventoryResult.Ok)
                        return res2;

                    // check unique-equipped special item classes
                    if (pProto.GetClass() == ItemClass.Quiver)
                    {
                        for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; ++i)
                        {
                            Item pBag = GetItemByPos(i);
                            if (pBag != null)
                            {
                                if (pBag != pItem)
                                {
                                    ItemTemplate pBagProto = pBag.GetTemplate();
                                    if (pBagProto != null)
                                    {
                                        if (pBagProto.GetClass() == pProto.GetClass() && (!swap || pBag.InventorySlot != eslot))
                                        {
                                            return (pBagProto.GetSubClass() == (uint)ItemSubClassQuiver.AmmoPouch)
                                                ? InventoryResult.OnlyOneAmmo
                                                : InventoryResult.OnlyOneQuiver;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    InventoryType type = pProto.GetInventoryType();

                    if (eslot == EquipmentSlot.OffHand)
                    {
                        // Do not allow polearm to be equipped in the offhand (rare case for the only 1h polearm 41750)
                        if (type == InventoryType.Weapon && pProto.GetSubClass() == (uint)ItemSubClassWeapon.Polearm)
                            return InventoryResult.TwoHandSkillNotFound;
                        else if (type == InventoryType.Weapon)
                        {
                            if (!CanDualWield())
                                return InventoryResult.TwoHandSkillNotFound;
                        }
                        else if (type == InventoryType.WeaponOffhand)
                        {
                            if (!CanDualWield() && !pProto.HasFlag(ItemFlags3.AlwaysAllowDualWield))
                                return InventoryResult.TwoHandSkillNotFound;
                        }
                        else if (type == InventoryType.Weapon2Hand)
                        {
                            if (!CanDualWield() || !CanTitanGrip())
                                return InventoryResult.TwoHandSkillNotFound;
                        }

                        if (IsTwoHandUsed())
                            return InventoryResult.Equipped2handed;
                    }

                    // equip two-hand weapon case (with possible unequip 2 items)
                    if (type == InventoryType.Weapon2Hand)
                    {
                        if (eslot == EquipmentSlot.OffHand)
                        {
                            if (!CanTitanGrip())
                                return InventoryResult.NotEquippable;
                        }
                        else if (eslot != EquipmentSlot.MainHand)
                            return InventoryResult.NotEquippable;

                        if (!CanTitanGrip())
                        {
                            // offhand item must can be stored in inventory for offhand item and it also must be unequipped
                            ItemPos offHandPos = new(EquipmentSlot.OffHand);
                            Item offItem = GetItemByPos(offHandPos);
                            if (offItem != null && (!not_loading || CanUnequipItem(offHandPos, false) != InventoryResult.Ok ||
                                CanStoreItem(ItemPos.Undefined, out _, offItem) != InventoryResult.Ok))
                            {
                                return swap ? InventoryResult.CantSwap : InventoryResult.InvFull;
                            }
                        }
                    }

                    dest = eslot;
                    return InventoryResult.Ok;
                }
            }
            return !swap ? InventoryResult.ItemNotFound : InventoryResult.CantSwap;
        }

        public InventoryResult CanEquipUniqueItem(Item pItem, byte eslot = ItemSlot.Null, int limit_count = 1)
        {
            ItemTemplate pProto = pItem.GetTemplate();

            // proto based limitations
            InventoryResult res = CanEquipUniqueItem(pProto, eslot, limit_count);
            if (res != InventoryResult.Ok)
                return res;

            // check unique-equipped on gems
            foreach (SocketedGem gemData in pItem.m_itemData.Gems)
            {
                if (gemData == null)
                    continue;

                ItemTemplate pGem = Global.ObjectMgr.GetItemTemplate(gemData.ItemId.GetValue());
                if (pGem == null)
                    continue;

                // include for check equip another gems with same limit category for not equipped item (and then not counted)
                var gem_limit_count = (!pItem.IsEquipped() && pGem.GetItemLimitCategory() != 0 ? pItem.GetGemCountWithLimitCategory(pGem.GetItemLimitCategory()) : 1);

                InventoryResult ress = CanEquipUniqueItem(pGem, eslot, gem_limit_count);
                if (ress != InventoryResult.Ok)
                    return ress;
            }

            return InventoryResult.Ok;
        }
        public InventoryResult CanEquipUniqueItem(ItemTemplate itemProto, byte except_slot = ItemSlot.Null, int limit_count = 1)
        {
            // check unique-equipped on item
            if (itemProto.HasFlag(ItemFlags.UniqueEquippable))
            {
                // there is an equip limit on this item
                if (HasItemOrGemWithIdEquipped(itemProto.GetId(), 1, except_slot))
                    return InventoryResult.ItemUniqueEquippable;
            }

            // check unique-equipped limit
            if (itemProto.GetItemLimitCategory() != 0)
            {
                ItemLimitCategoryRecord limitEntry = CliDB.ItemLimitCategoryStorage.LookupByKey(itemProto.GetItemLimitCategory());
                if (limitEntry == null)
                    return InventoryResult.NotEquippable;

                // NOTE: limitEntry.mode not checked because if item have have-limit then it applied and to equip case
                byte limitQuantity = GetItemLimitCategoryQuantity(limitEntry);

                if (limit_count > limitQuantity)
                    return InventoryResult.ItemMaxLimitCategoryEquippedExceededIs;

                // there is an equip limit on this item
                if (HasItemWithLimitCategoryEquipped(itemProto.GetItemLimitCategory(), limitQuantity - limit_count + 1, except_slot))
                    return InventoryResult.ItemMaxLimitCategoryEquippedExceededIs;
                else if (HasGemWithLimitCategoryEquipped(itemProto.GetItemLimitCategory(), limitQuantity - limit_count + 1, except_slot))
                    return InventoryResult.ItemMaxCountEquippedSocketed;
            }

            return InventoryResult.Ok;
        }

        public InventoryResult CanUnequipItem(List<ItemPosCount> pos, bool swap)
        {
            return CanUnequipItem(pos.FirstOrDefault().Position, swap);
        }        

        public InventoryResult CanUnequipItem(ItemPos pos, bool swap)
        {
            Item item = GetItemByPos(pos);

            return CanUnequipItem(item, swap);
        }

        public InventoryResult CanUnequipItem(Item item, bool swap)
        {
            // Applied only to existed equipped item
            if (item == null)
                return InventoryResult.Ok;

            ItemPos position = item.InventoryPosition;

            // Applied only to equipped items and bank bags
            if (!position.IsEquipmentPos && !position.IsBagSlotPos)
                return InventoryResult.Ok;
                        
            Log.outDebug(LogFilter.Player,
                $"STORAGE: CanUnequipItem slot = {position}, item = {item.GetEntry()}, count = {item.GetCount()}");

            ItemTemplate pProto = item.GetTemplate();
            if (pProto == null)
                return InventoryResult.ItemNotFound;

            // item used
            if (item.m_lootGenerated)
                return InventoryResult.LootGone;

            if (IsCharmed())
                return InventoryResult.CantDoThatRightNow; // @todo is this the correct error?

            // do not allow unequipping gear except weapons, offhands, projectiles, relics in
            // - combat
            // - in-progress arenas
            if (!pProto.CanChangeEquipStateInCombat())
            {
                if (IsInCombat())
                    return InventoryResult.NotInCombat;

                Battleground bg = GetBattleground();
                if (bg != null)
                {
                    if (bg.IsArena() && bg.GetStatus() == BattlegroundStatus.InProgress)
                        return InventoryResult.NotDuringArenaMatch;
                }
            }

            if (!swap && item.IsNotEmptyBag())
                return InventoryResult.DestroyNonemptyBag;

            return InventoryResult.Ok;
        }

        public bool HasItemOrGemWithIdEquipped(int item, int count, byte except_slot = ItemSlot.Null)
        {
            int tempcount = 0;

            ItemTemplate pProto = Global.ObjectMgr.GetItemTemplate(item);
            bool includeGems = pProto?.GetGemProperties() != 0;
            return !ForEachItem(ItemSearchLocation.Equipment, pItem =>
            {
                if (pItem.InventorySlot != except_slot)
                {
                    if (pItem.GetEntry() == item)
                        tempcount += pItem.GetCount();

                    if (includeGems)
                        tempcount += pItem.GetGemCountWithID(item);

                    if (tempcount >= count)
                        return false;
                }
                return true;
            });
        }
        bool HasItemWithLimitCategoryEquipped(int limitCategory, int count, byte except_slot)
        {
            int tempcount = 0;
            return !ForEachItem(ItemSearchLocation.Equipment, pItem =>
            {
                if (pItem.InventorySlot == except_slot)
                    return true;

                if (pItem.GetTemplate().GetItemLimitCategory() != limitCategory)
                    return true;

                tempcount += pItem.GetCount();
                if (tempcount >= count)
                    return false;

                return true;
            });
        }

        bool HasGemWithLimitCategoryEquipped(int limitCategory, int count, byte except_slot)
        {
            uint tempcount = 0;
            return !ForEachItem(ItemSearchLocation.Equipment, pItem =>
            {
                if (pItem.InventorySlot == except_slot)
                    return true;

                ItemTemplate pProto = pItem.GetTemplate();
                if (pProto == null)
                    return true;

                tempcount += pItem.GetGemCountWithLimitCategory(limitCategory);
                if (tempcount >= count)
                    return false;

                return true;
            });
        }

        //Visual
        public void SetVisibleItemSlot(int slot, Item pItem)
        {
            var itemField = m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.VisibleItems, slot);
            if (pItem != null)
            {
                SetUpdateFieldValue(itemField.ModifyValue(itemField.ItemID), pItem.GetVisibleEntry(this));
                SetUpdateFieldValue(itemField.ModifyValue(itemField.ItemAppearanceModID), pItem.GetVisibleAppearanceModId(this));
                SetUpdateFieldValue(itemField.ModifyValue(itemField.ItemVisual), pItem.GetVisibleItemVisual(this));
            }
            else
            {
                SetUpdateFieldValue(itemField.ModifyValue(itemField.ItemID), 0);
                SetUpdateFieldValue(itemField.ModifyValue(itemField.ItemAppearanceModID), (ushort)0);
                SetUpdateFieldValue(itemField.ModifyValue(itemField.ItemVisual), (ushort)0);
            }
        }

        void VisualizeItem(int slot, Item pItem)
        {
            if (pItem == null)
                return;

            // check also  BIND_WHEN_PICKED_UP and BIND_QUEST_ITEM for .additem or .additemset case by GM
            // (not binded at adding to inventory)
            if (pItem.GetBonding() == ItemBondingType.OnEquip
                || pItem.GetBonding() == ItemBondingType.OnAcquire
                || pItem.GetBonding() == ItemBondingType.Quest)
            {
                pItem.SetBinding(true);
                if (IsInWorld)
                    GetSession().GetCollectionMgr().AddItemAppearance(pItem);
            }

            Log.outDebug(LogFilter.Player,
                $"STORAGE: EquipItem slot = {slot}, item = {pItem.GetEntry()}");

            m_items[slot] = pItem;
            SetInvSlot(slot, pItem.GetGUID());
            pItem.SetContainedIn(GetGUID());
            pItem.SetOwnerGUID(GetGUID());
            pItem.InventorySlot = (byte)slot;
            pItem.SetContainer(null);

            if (slot < EquipmentSlot.End)
                SetVisibleItemSlot(slot, pItem);

            pItem.SetState(ItemUpdateState.Changed, this);
        }

        public void DestroyItem(ItemPos itemPos, bool update)
        {
            Item pItem = GetItemByPos(itemPos);
            if (pItem != null)
            {
                Log.outDebug(LogFilter.Player,
                    $"STORAGE: DestroyItem bag = {itemPos.Container}, slot = {itemPos.Slot}, item = {pItem.GetEntry()}");

                // Also remove all contained items if the item is a bag.
                // This if () prevents item saving crashes if the condition for a bag
                // to be empty before being destroyed was bypassed somehow.
                if (pItem.IsNotEmptyBag())
                    for (byte i = 0; i < ItemConst.MaxBagSize; ++i)
                        DestroyItem(new(i, itemPos.Slot), update);

                if (pItem.IsWrapped())
                {
                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GIFT);
                    stmt.SetInt64(0, pItem.GetGUID().GetCounter());
                    DB.Characters.Execute(stmt);
                }

                RemoveEnchantmentDurations(pItem);
                RemoveItemDurations(pItem);

                pItem.SetNotRefundable(this);
                pItem.ClearSoulboundTradeable(this);
                RemoveTradeableItem(pItem);

                ApplyItemObtainSpells(pItem, false);
                ApplyItemLootedSpell(pItem, false);

                Global.ScriptMgr.OnItemRemove(this, pItem);

                Bag pBag;
                ItemTemplate pProto = pItem.GetTemplate();
                if (!itemPos.IsContainerPos)
                {
                    SetInvSlot(itemPos.Slot, ObjectGuid.Empty);

                    // equipment and equipped bags can have applied bonuses
                    if (itemPos.Slot < InventorySlots.BagEnd)
                    {
                        // item set bonuses applied only at equip and removed at unequip, and still active for broken items
                        if (pProto != null && pProto.GetItemSet() != 0)
                            Item.RemoveItemsSetItem(this, pItem);

                        _ApplyItemMods(pItem, itemPos.Slot, false);
                    }

                    if (itemPos.Slot < EquipmentSlot.End)
                    {
                        // update expertise and armor penetration - passive auras may need it
                        switch (itemPos.Slot)
                        {
                            case EquipmentSlot.MainHand:
                            case EquipmentSlot.OffHand:
                                RecalculateRating(CombatRating.ArmorPenetration);
                                break;
                            default:
                                break;
                        }

                        if (itemPos.Slot == EquipmentSlot.MainHand)
                            UpdateExpertise(WeaponAttackType.BaseAttack);
                        else if (itemPos.Slot == EquipmentSlot.OffHand)
                            UpdateExpertise(WeaponAttackType.OffAttack);

                        // equipment visual show
                        SetVisibleItemSlot(itemPos.Slot, null);
                    }

                    m_items[itemPos.Slot] = null;
                }
                else if ((pBag = GetBagByPos(itemPos.Container)) != null)
                    pBag.RemoveItem(itemPos.Slot, update);

                // Delete rolled money / loot from db.
                // MUST be done before RemoveFromWorld() or GetTemplate() fails
                if (pProto.HasFlag(ItemFlags.HasLoot))
                    Global.LootItemStorage.RemoveStoredLootForContainer(pItem.GetGUID().GetCounter());

                ItemRemovedQuestCheck(pItem.GetEntry(), pItem.GetCount());

                if (IsInWorld && update)
                {
                    pItem.RemoveFromWorld();
                    pItem.DestroyForPlayer(this);
                }

                //pItem.SetOwnerGUID(ObjectGuid.Empty);
                pItem.SetContainedIn(ObjectGuid.Empty);
                pItem.InventorySlot = ItemSlot.Null;
                pItem.SetState(ItemUpdateState.Removed, this);

                if (pProto.GetInventoryType() != InventoryType.NonEquip)
                    UpdateAverageItemLevelTotal();

                if (!itemPos.IsContainerPos)
                    UpdateAverageItemLevelEquipped();
            }
        }

        public int DestroyItemCount(int itemEntry, int count, bool update, bool unequip_check = true)
        {
            Log.outDebug(LogFilter.Player,
                $"STORAGE: DestroyItemCount item = {itemEntry}, count = {count}");

            int remcount = 0;

            // in inventory
            int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
            for (byte i = InventorySlots.ItemStart; i < inventoryEnd; ++i)
            {
                Item item = GetItemByPos(i);
                if (item != null)
                {
                    if (item.GetEntry() == itemEntry && !item.IsInTrade())
                    {
                        if (item.GetCount() + remcount <= count)
                        {
                            // all items in inventory can unequipped
                            remcount += item.GetCount();
                            DestroyItem(new(i), update);

                            if (remcount >= count)
                                return remcount;
                        }
                        else
                        {
                            item.SetCount(item.GetCount() - count + remcount);
                            ItemRemovedQuestCheck(item.GetEntry(), count - remcount);
                            if (IsInWorld && update)
                                item.SendUpdateToPlayer(this);
                            item.SetState(ItemUpdateState.Changed, this);
                            return count;
                        }
                    }
                }
            }

            // in inventory bags
            for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; i++)
            {
                Bag bag = GetBagByPos(i);
                if (bag != null)
                {
                    for (byte j = 0; j < bag.GetBagSize(); j++)
                    {
                        Item item = bag.GetItemByPos(j);
                        if (item != null)
                        {
                            if (item.GetEntry() == itemEntry && !item.IsInTrade())
                            {
                                // all items in bags can be unequipped
                                if (item.GetCount() + remcount <= count)
                                {
                                    remcount += item.GetCount();
                                    DestroyItem(new(j, i), update);

                                    if (remcount >= count)
                                        return remcount;
                                }
                                else
                                {
                                    item.SetCount(item.GetCount() - count + remcount);
                                    ItemRemovedQuestCheck(item.GetEntry(), count - remcount);
                                    if (IsInWorld && update)
                                        item.SendUpdateToPlayer(this);
                                    item.SetState(ItemUpdateState.Changed, this);
                                    return count;
                                }
                            }
                        }
                    }
                }
            }

            for (byte i = InventorySlots.KeyringStart; i < InventorySlots.KeyringEnd; ++i)
            {
                ItemPos pos = new(i);
                Item item = GetItemByPos(pos);
                if (item != null)
                {
                    if (item.GetEntry() == itemEntry && !item.IsInTrade())
                    {
                        if (item.GetCount() + remcount <= count)
                        {
                            // all keys can be unequipped
                            remcount += item.GetCount();
                            DestroyItem(pos, update);

                            if (remcount >= count)
                                return remcount;
                        }
                        else
                        {
                            item.SetCount(item.GetCount() - count + remcount);
                            ItemRemovedQuestCheck(item.GetEntry(), count - remcount);
                            if (IsInWorld && update)
                                item.SendUpdateToPlayer(this);
                            item.SetState(ItemUpdateState.Changed, this);
                            return count;
                        }
                    }
                }
            }

            // in equipment and bag list
            for (byte i = EquipmentSlot.Start; i < InventorySlots.BagEnd; i++)
            {
                ItemPos pos = new(i);
                Item item = GetItemByPos(pos);
                if (item != null)
                {
                    if (item.GetEntry() == itemEntry && !item.IsInTrade())
                    {
                        if (item.GetCount() + remcount <= count)
                        {
                            if (!unequip_check || CanUnequipItem(pos, false) == InventoryResult.Ok)
                            {
                                remcount += item.GetCount();
                                DestroyItem(pos, update);

                                if (remcount >= count)
                                    return remcount;
                            }
                        }
                        else
                        {
                            item.SetCount(item.GetCount() - count + remcount);
                            ItemRemovedQuestCheck(item.GetEntry(), count - remcount);
                            if (IsInWorld && update)
                                item.SendUpdateToPlayer(this);
                            item.SetState(ItemUpdateState.Changed, this);
                            return count;
                        }
                    }
                }
            }

            // in bank
            for (byte i = InventorySlots.BankItemStart; i < InventorySlots.BankItemEnd; i++)
            {
                ItemPos pos = new(i);
                Item item = GetItemByPos(pos);
                if (item != null)
                {
                    if (item.GetEntry() == itemEntry && !item.IsInTrade())
                    {
                        if (item.GetCount() + remcount <= count)
                        {
                            remcount += item.GetCount();
                            DestroyItem(pos, update);
                            if (remcount >= count)
                                return remcount;
                        }
                        else
                        {
                            item.SetCount(item.GetCount() - count + remcount);
                            ItemRemovedQuestCheck(item.GetEntry(), count - remcount);
                            if (IsInWorld && update)
                                item.SendUpdateToPlayer(this);
                            item.SetState(ItemUpdateState.Changed, this);
                            return count;
                        }
                    }
                }
            }

            // in bank bags
            for (byte i = InventorySlots.BankBagStart; i < InventorySlots.BankBagEnd; i++)
            {
                Bag bag = GetBagByPos(i);
                if (bag != null)
                {
                    for (byte j = 0; j < bag.GetBagSize(); j++)
                    {
                        Item item = bag.GetItemByPos(j);
                        if (item != null)
                        {
                            if (item.GetEntry() == itemEntry && !item.IsInTrade())
                            {
                                // all items in bags can be unequipped
                                if (item.GetCount() + remcount <= count)
                                {
                                    remcount += item.GetCount();
                                    DestroyItem(new(j, i), update);

                                    if (remcount >= count)
                                        return remcount;
                                }
                                else
                                {
                                    item.SetCount(item.GetCount() - count + remcount);
                                    ItemRemovedQuestCheck(item.GetEntry(), count - remcount);
                                    if (IsInWorld && update)
                                        item.SendUpdateToPlayer(this);
                                    item.SetState(ItemUpdateState.Changed, this);
                                    return count;
                                }
                            }
                        }
                    }
                }
            }

            // in bank bag list
            for (byte i = InventorySlots.BankBagStart; i < InventorySlots.BankBagEnd; i++)
            {
                ItemPos pos = new(i);
                Item item = GetItemByPos(pos);
                if (item != null)
                {
                    if (item.GetEntry() == itemEntry && !item.IsInTrade())
                    {
                        if (item.GetCount() + remcount <= count)
                        {
                            if (!unequip_check || CanUnequipItem(pos, false) == InventoryResult.Ok)
                            {
                                remcount += item.GetCount();
                                DestroyItem(pos, update);
                                if (remcount >= count)
                                    return remcount;
                            }
                        }
                        else
                        {
                            item.SetCount(item.GetCount() - count + remcount);
                            ItemRemovedQuestCheck(item.GetEntry(), count - remcount);
                            if (IsInWorld && update)
                                item.SendUpdateToPlayer(this);
                            item.SetState(ItemUpdateState.Changed, this);
                            return count;
                        }
                    }
                }
            }

            for (byte i = InventorySlots.ChildEquipmentStart; i < InventorySlots.ChildEquipmentEnd; ++i)
            {
                ItemPos pos = new(i);
                Item item = GetItemByPos(pos);
                if (item != null)
                {
                    if (item.GetEntry() == itemEntry && !item.IsInTrade())
                    {
                        if (item.GetCount() + remcount <= count)
                        {
                            // all keys can be unequipped
                            remcount += item.GetCount();
                            DestroyItem(pos, update);

                            if (remcount >= count)
                                return remcount;
                        }
                        else
                        {
                            item.SetCount(item.GetCount() - count + remcount);
                            ItemRemovedQuestCheck(item.GetEntry(), count - remcount);
                            if (IsInWorld && update)
                                item.SendUpdateToPlayer(this);
                            item.SetState(ItemUpdateState.Changed, this);
                            return count;
                        }
                    }
                }
            }

            return remcount;
        }

        public void DestroyItemCount(Item pItem, ref int count, bool update)
        {
            if (pItem == null)
                return;

            Log.outDebug(LogFilter.Player,
                $"STORAGE: DestroyItemCount item (GUID: {pItem.GetGUID()}, Entry: {pItem.GetEntry()}) count = {count}");

            if (pItem.GetCount() <= count)
            {
                count -= pItem.GetCount();

                DestroyItem(pItem.InventoryPosition, update);
            }
            else
            {
                ItemRemovedQuestCheck(pItem.GetEntry(), count);
                pItem.SetCount(pItem.GetCount() - count);
                count = 0;
                if (IsInWorld && update)
                    pItem.SendUpdateToPlayer(this);
                pItem.SetState(ItemUpdateState.Changed, this);
            }
        }

        public void AutoStoreLoot(int loot_id, LootStore store, ItemContext context = 0, bool broadcast = false, bool createdByPlayer = false)
        {
            Loot loot = new(null, ObjectGuid.Empty, LootType.None, null);
            loot.FillLoot(loot_id, store, this, true, false, LootModes.Default);

            loot.AutoStore(this, broadcast, createdByPlayer);
            Unit.ProcSkillsAndAuras(this, null, new ProcFlagsInit(ProcFlags.Looted), new ProcFlagsInit(ProcFlags.None), ProcFlagsSpellType.MaskAll, ProcFlagsSpellPhase.None, ProcFlagsHit.None, null, null, null);
        }

        public byte GetInventorySlotCount() { return m_activePlayerData.NumBackpackSlots; }

        public void SetInventorySlotCount(byte slots)
        {
            //ASSERT(slots <= (INVENTORY_SLOT_ITEM_END - INVENTORY_SLOT_ITEM_START));

            if (slots < GetInventorySlotCount())
            {
                List<Item> unstorableItems = new();

                for (byte slot = (byte)(InventorySlots.ItemStart + slots); slot < InventorySlots.ItemEnd; ++slot)
                {
                    Item unstorableItem = GetItemByPos(slot);
                    if (unstorableItem != null)
                        unstorableItems.Add(unstorableItem);
                }

                if (!unstorableItems.Empty())
                {
                    int fullBatches = unstorableItems.Count / SharedConst.MaxMailItems;
                    int remainder = unstorableItems.Count % SharedConst.MaxMailItems;
                    SQLTransaction trans = new();

                    var sendItemsBatch = new Action<int, int>((batchNumber, batchSize) =>
                    {
                        MailDraft draft = new(Global.ObjectMgr.GetCypherString(CypherStrings.NotEquippedItem), "There were problems with equipping item(s).");
                        for (int j = 0; j < batchSize; ++j)
                            draft.AddItem(unstorableItems[batchNumber * SharedConst.MaxMailItems + j]);

                        draft.SendMailTo(trans, this, new MailSender(this, MailStationery.Gm), MailCheckFlags.Copied);
                    });

                    for (int batch = 0; batch < fullBatches; ++batch)
                        sendItemsBatch(batch, SharedConst.MaxMailItems);

                    if (remainder != 0)
                        sendItemsBatch(fullBatches, remainder);

                    DB.Characters.CommitTransaction(trans);

                    SendPacket(new InventoryFullOverflow());
                }
            }

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.NumBackpackSlots), slots);
        }

        public byte GetBankBagSlotCount()
        {
            return m_playerData.NumBankSlots;
        }

        public void SetBankBagSlotCount(byte count)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.NumBankSlots), count);
        }

        //Loot
        public ObjectGuid GetLootGUID()
        {
            return m_playerData.LootTargetGUID;
        }

        public void SetLootGUID(ObjectGuid guid)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.LootTargetGUID), guid);
        }

        public void StoreLootItem(ObjectGuid lootWorldObjectGuid, byte lootSlot, Loot loot, AELootResult aeResult = null)
        {
            LootItem item = loot.LootItemInSlot(lootSlot, this, out var ffaItem);
            if (item == null || item.is_looted)
            {
                SendEquipError(InventoryResult.LootGone);
                return;
            }

            if (!item.HasAllowedLooter(GetGUID()))
            {
                SendLootReleaseAll();
                return;
            }

            if (item.is_blocked)
            {
                SendLootReleaseAll();
                return;
            }

            // dont allow protected item to be looted by someone else
            if (!item.rollWinnerGUID.IsEmpty() && item.rollWinnerGUID != GetGUID())
            {
                SendLootReleaseAll();
                return;
            }

            InventoryResult msg = CanStoreNewItem(ItemPos.Undefined, out var dest, item.itemid, item.count);
            if (msg == InventoryResult.Ok)
            {
                Item newitem = StoreNewItem(dest, item.itemid, true, ItemEnchantmentManager.GenerateRandomProperties(item.itemid), item.GetAllowedLooters(), item.context);
                if (ffaItem != null)
                {
                    //freeforall case, notify only one player of the removal
                    ffaItem.is_looted = true;
                    SendNotifyLootItemRemoved(loot.GetGUID(), loot.GetOwnerGUID(), lootSlot);
                }
                else    //not freeforall, notify everyone
                    loot.NotifyItemRemoved(lootSlot, GetMap());

                //if only one person is supposed to loot the item, then set it to looted
                if (!item.freeforall)
                    item.is_looted = true;

                --loot.unlootedCount;

                if (newitem != null &&
                    (newitem.GetQuality() > ItemQuality.Epic || (newitem.GetQuality() == ItemQuality.Epic && newitem.GetItemLevel(this) >= GuildConst.MinNewsItemLevel)))
                {
                    Guild guild = GetGuild();
                    if (guild != null)
                        guild.AddGuildNews(GuildNews.ItemLooted, GetGUID(), 0, item.itemid);
                }

                // if aeLooting then we must delay sending out item so that it appears properly stacked in chat
                if (aeResult == null || newitem == null)
                {
                    SendNewItem(newitem, item.count, false, false, true, loot.GetDungeonEncounterId());
                    UpdateCriteria(CriteriaType.LootItem, item.itemid, item.count);
                    UpdateCriteria(CriteriaType.GetLootByType, item.itemid, item.count, (long)SharedConst.GetLootTypeForClient(loot.loot_type));
                    UpdateCriteria(CriteriaType.LootAnyItem, item.itemid, item.count);
                }
                else
                    aeResult.Add(newitem, item.count, SharedConst.GetLootTypeForClient(loot.loot_type), loot.GetDungeonEncounterId());

                // LootItem is being removed (looted) from the container, delete it from the DB.
                if (loot.loot_type == LootType.Item)
                    Global.LootItemStorage.RemoveStoredLootItemForContainer(lootWorldObjectGuid.GetCounter(), item.itemid, item.count, item.LootListId);

                if (newitem != null)
                    ApplyItemLootedSpell(newitem, true);
                else
                    ApplyItemLootedSpell(Global.ObjectMgr.GetItemTemplate(item.itemid));
            }
            else
                SendEquipError(msg, null, null, item.itemid);
        }

        public Dictionary<ObjectGuid, Loot> GetAELootView() { return m_AELootView; }

        /// <summary>
        /// if in a Battleground a player dies, and an enemy removes the insignia, the player's bones is lootable
        /// Called by remove insignia spell effect
        /// </summary>
        /// <param name="looterPlr"></param>
        public void RemovedInsignia(Player looterPlr)
        {
            // If player is not in battleground and not in worldpvpzone
            if (GetBattlegroundId() == 0 && !IsInWorldPvpZone())
                return;

            // If not released spirit, do it !
            if (m_deathTimer > 0)
            {
                m_deathTimer = Milliseconds.Zero;
                BuildPlayerRepop();
                RepopAtGraveyard();
            }

            _corpseLocation = new WorldLocation();

            // We have to convert player corpse to bones, not to be able to resurrect there
            // SpawnCorpseBones isn't handy, 'cos it saves player while he in BG
            Corpse bones = GetMap().ConvertCorpseToBones(GetGUID(), true);
            if (bones == null)
                return;

            // Now we must make bones lootable, and send player loot
            bones.SetCorpseDynamicFlag(CorpseDynFlags.Lootable);

            bones.loot = new Loot(GetMap(), bones.GetGUID(), LootType.Insignia, looterPlr.GetGroup());

            // For AV Achievement
            Battleground bg = GetBattleground();
            if (bg != null)
            {
                if (bg.GetTypeID() == BattlegroundTypeId.AV)
                    bones.loot.FillLoot(1, LootStorage.Creature, this, true);
            }
            // For wintergrasp Quests
            else if (GetZoneId() == (uint)AreaId.Wintergrasp)
                bones.loot.FillLoot(1, LootStorage.Creature, this, true);

            // It may need a better formula
            // Now it works like this: lvl10: ~6copper, lvl70: ~9silver
            bones.loot.gold = (uint)(RandomHelper.IRand(50, 150) * 0.016f * Math.Pow(GetLevel() / 5.76f, 2.5f)
                * WorldConfig.Values[WorldCfg.RateDropMoney].Float);

            bones.lootRecipient = looterPlr;
            looterPlr.SendLoot(bones.loot);
        }

        public void SendLootRelease(ObjectGuid guid)
        {
            LootReleaseResponse packet = new();
            packet.LootObj = guid;
            packet.Owner = GetGUID();
            SendPacket(packet);
        }

        public void SendLootReleaseAll()
        {
            SendPacket(new LootReleaseAll());
        }

        public void SendLoot(Loot loot, bool aeLooting = false)
        {
            if (!GetLootGUID().IsEmpty() && !aeLooting)
                _session.DoLootReleaseAll();

            Log.outDebug(LogFilter.Loot,
                $"Player::SendLoot: Player: '{GetName()}' ({GetGUID()}), Loot: {loot.GetOwnerGUID()}");

            if (!loot.GetOwnerGUID().IsItem() && !aeLooting)
                SetLootGUID(loot.GetOwnerGUID());

            LootResponse packet = new();
            packet.Owner = loot.GetOwnerGUID();
            packet.LootObj = loot.GetGUID();
            packet.LootMethod = loot.GetLootMethod();
            packet.AcquireReason = (byte)SharedConst.GetLootTypeForClient(loot.loot_type);
            packet.Acquired = true; // false == No Loot (this too^^)
            packet.AELooting = aeLooting;
            loot.BuildLootResponse(packet, this);
            SendPacket(packet);

            // add 'this' player as one of the players that are looting 'loot'
            loot.OnLootOpened(GetMap(), GetGUID());
            m_AELootView[loot.GetGUID()] = loot;

            if (loot.loot_type == LootType.Corpse && !loot.GetOwnerGUID().IsItem())
                SetUnitFlag(UnitFlags.Looting);
        }

        public void SendLootError(ObjectGuid lootObj, ObjectGuid owner, LootError error)
        {
            LootResponse packet = new();
            packet.LootObj = lootObj;
            packet.Owner = owner;
            packet.Acquired = false;
            packet.FailureReason = error;
            SendPacket(packet);
        }

        public void SendNotifyLootMoneyRemoved(ObjectGuid lootObj)
        {
            CoinRemoved packet = new();
            packet.LootObj = lootObj;
            SendPacket(packet);
        }

        public void SendNotifyLootItemRemoved(ObjectGuid lootObj, ObjectGuid owner, byte lootListId)
        {
            LootRemoved packet = new();
            packet.LootObj = lootObj;
            packet.Owner = owner;
            packet.LootListID = lootListId;
            SendPacket(packet);
        }

        void SendEquipmentSetList()
        {
            LoadEquipmentSet data = new();

            foreach (var pair in _equipmentSets)
            {
                if (pair.Value.state == EquipmentSetUpdateState.Deleted)
                    continue;

                data.SetData.Add(pair.Value.Data);
            }

            SendPacket(data);
        }

        public void SetEquipmentSet(EquipmentSetInfo.EquipmentSetData newEqSet)
        {
            if (newEqSet.Guid != 0)
            {
                // something wrong...
                var equipmentSetInfo = _equipmentSets.LookupByKey(newEqSet.Guid);
                if (equipmentSetInfo == null || equipmentSetInfo.Data.Guid != newEqSet.Guid)
                {
                    Log.outError(LogFilter.Player,
                        $"Player {GetName()} tried to save equipment set {newEqSet.Guid} " +
                        $"(index: {newEqSet.SetID}), but that equipment set not found!");
                    return;
                }
            }

            var setGuid = (newEqSet.Guid != 0) ? newEqSet.Guid : Global.ObjectMgr.GenerateEquipmentSetGuid();

            if (!_equipmentSets.ContainsKey(setGuid))
                _equipmentSets[setGuid] = new EquipmentSetInfo();

            EquipmentSetInfo eqSlot = _equipmentSets[setGuid];
            eqSlot.Data = newEqSet;

            if (eqSlot.Data.Guid == 0)
            {
                eqSlot.Data.Guid = setGuid;

                EquipmentSetID data = new();
                data.GUID = eqSlot.Data.Guid;
                data.Type = (int)eqSlot.Data.Type;
                data.SetID = eqSlot.Data.SetID;
                SendPacket(data);
            }

            eqSlot.state =
                eqSlot.state == EquipmentSetUpdateState.New
                ? EquipmentSetUpdateState.New
                : EquipmentSetUpdateState.Changed;
        }

        public void DeleteEquipmentSet(long id)
        {
            foreach (var pair in _equipmentSets)
            {
                if (pair.Value.Data.Guid == id)
                {
                    if (pair.Value.state == EquipmentSetUpdateState.New)
                        _equipmentSets.Remove(pair.Key);
                    else
                        pair.Value.state = EquipmentSetUpdateState.Deleted;
                    break;
                }
            }
        }

        //Void Storage
        public bool IsVoidStorageUnlocked() { return HasPlayerFlag(PlayerFlags.VoidUnlocked); }
        public void UnlockVoidStorage() { SetPlayerFlag(PlayerFlags.VoidUnlocked); }
        public void LockVoidStorage() { RemovePlayerFlag(PlayerFlags.VoidUnlocked); }

        public byte GetNextVoidStorageFreeSlot()
        {
            for (byte i = 0; i < SharedConst.VoidStorageMaxSlot; ++i)
            {
                if (_voidStorageItems[i] == null) // unused item
                    return i;
            }

            return SharedConst.VoidStorageMaxSlot;
        }

        public byte GetNumOfVoidStorageFreeSlots()
        {
            byte count = 0;

            for (byte i = 0; i < SharedConst.VoidStorageMaxSlot; ++i)
            {
                if (_voidStorageItems[i] == null)
                    count++;
            }

            return count;
        }

        public byte AddVoidStorageItem(VoidStorageItem item)
        {
            byte slot = GetNextVoidStorageFreeSlot();

            if (slot >= SharedConst.VoidStorageMaxSlot)
            {
                GetSession().SendVoidStorageTransferResult(VoidTransferError.Full);
                return 255;
            }

            _voidStorageItems[slot] = item;
            return slot;
        }

        public void DeleteVoidStorageItem(byte slot)
        {
            if (slot >= SharedConst.VoidStorageMaxSlot)
            {
                GetSession().SendVoidStorageTransferResult(VoidTransferError.InternalError1);
                return;
            }

            _voidStorageItems[slot] = null;
        }

        public bool SwapVoidStorageItem(byte oldSlot, byte newSlot)
        {
            if (oldSlot >= SharedConst.VoidStorageMaxSlot || newSlot >= SharedConst.VoidStorageMaxSlot || oldSlot == newSlot)
                return false;

            _voidStorageItems.Swap(newSlot, oldSlot);
            return true;
        }

        public VoidStorageItem GetVoidStorageItem(byte slot)
        {
            if (slot >= SharedConst.VoidStorageMaxSlot)
            {
                GetSession().SendVoidStorageTransferResult(VoidTransferError.InternalError1);
                return null;
            }

            return _voidStorageItems[slot];
        }

        public VoidStorageItem GetVoidStorageItem(long id, out byte slot)
        {
            slot = 0;
            for (byte i = 0; i < SharedConst.VoidStorageMaxSlot; ++i)
            {
                if (_voidStorageItems[i] != null && _voidStorageItems[i].ItemId == id)
                {
                    slot = i;
                    return _voidStorageItems[i];
                }
            }

            return null;
        }

        //Misc
        void UpdateItemLevelAreaBasedScaling()
        {
            // @todo Activate pvp item levels during world pvp
            Map map = GetMap();
            bool pvpActivity = map.IsBattlegroundOrArena() || map.GetEntry().HasAnyFlag(MapFlags2.PVP) || HasPvpRulesEnabled();

            if (_usePvpItemLevels != pvpActivity)
            {
                float healthPct = GetHealthPct();
                _RemoveAllItemMods();
                ActivatePvpItemLevels(pvpActivity);
                _ApplyAllItemMods();
                SetHealth(MathFunctions.CalculatePct(GetMaxHealth(), healthPct));
            }
            // @todo other types of power scaling such as timewalking
        }

        public bool ForEachItem(ItemSearchLocation location, Func<Item, bool> callback)
        {
            if (location.HasAnyFlag(ItemSearchLocation.Equipment))
            {
                for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; i++)
                {
                    Item item = GetItemByPos(i);
                    if (item != null)
                    {
                        if (!callback(item))
                            return false;
                    }
                }

                for (byte i = ProfessionSlots.Start; i < ProfessionSlots.End; ++i)
                {
                    Item pItem = GetItemByPos(i);
                    if (pItem != null)
                    {
                        if (!callback(pItem))
                            return false;
                    }
                }
            }

            if (location.HasAnyFlag(ItemSearchLocation.Inventory))
            {
                int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
                for (byte i = InventorySlots.BagStart; i < inventoryEnd; i++)
                {
                    Item item = GetItemByPos(i);
                    if (item != null)
                    {
                        if (!callback(item))
                            return false;
                    }
                }

                for (byte i = InventorySlots.KeyringStart; i < InventorySlots.KeyringEnd; i++)
                {
                    Item item = GetItemByPos(i);
                    if (item != null)
                    {
                        if (!callback(item))
                            return false;
                    }
                }
;
                for (byte i = InventorySlots.ChildEquipmentStart; i < InventorySlots.ChildEquipmentEnd; i++)
                {
                    Item item = GetItemByPos(i);
                    if (item != null)
                    {
                        if (!callback(item))
                            return false;
                    }
                }

                for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; i++)
                {
                    Bag bag = GetBagByPos(i);
                    if (bag != null)
                    {
                        for (byte j = 0; j < bag.GetBagSize(); ++j)
                        {
                            Item pItem = bag.GetItemByPos(j);
                            if (pItem != null)
                            {
                                if (!callback(pItem))
                                    return false;
                            }
                        }
                    }
                }
            }

            if (location.HasAnyFlag(ItemSearchLocation.Bank))
            {
                for (byte i = InventorySlots.BankItemStart; i < InventorySlots.BankItemEnd; ++i)
                {
                    Item item = GetItemByPos(i);
                    if (item != null)
                    {
                        if (!callback(item))
                            return false;
                    }
                }

                for (byte i = InventorySlots.BankBagStart; i < InventorySlots.BankBagEnd; ++i)
                {
                    Bag bag = GetBagByPos(i);
                    if (bag != null)
                    {
                        for (byte j = 0; j < bag.GetBagSize(); ++j)
                        {
                            Item pItem = bag.GetItemByPos(j);
                            if (pItem != null)
                            {
                                if (!callback(pItem))
                                    return false;
                            }
                        }
                    }
                }
            }

            if (location.HasAnyFlag(ItemSearchLocation.ReagentBank))
            {
                for (byte i = InventorySlots.ReagentBagStart; i < InventorySlots.ReagentBagEnd; ++i)
                {
                    Item item = GetItemByPos(i);
                    if (item != null)
                    {
                        if (!callback(item))
                            return false;
                    }
                }
            }

            return true;
        }

        delegate void EquipmentSlotDelegate(byte equipmentSlot, bool checkDuplicateGuid = false);
        bool ForEachEquipmentSlot(InventoryType inventoryType, bool canDualWield, bool canTitanGrip, EquipmentSlotDelegate callback)
        {
            switch (inventoryType)
            {
                case InventoryType.Head:
                    callback(EquipmentSlot.Head);
                    return true;
                case InventoryType.Neck:
                    callback(EquipmentSlot.Neck);
                    return true;
                case InventoryType.Shoulders:
                    callback(EquipmentSlot.Shoulders);
                    return true;
                case InventoryType.Body:
                    callback(EquipmentSlot.Shirt);
                    return true;
                case InventoryType.Robe:
                case InventoryType.Chest:
                    callback(EquipmentSlot.Chest);
                    return true;
                case InventoryType.Waist:
                    callback(EquipmentSlot.Waist);
                    return true;
                case InventoryType.Legs:
                    callback(EquipmentSlot.Legs);
                    return true;
                case InventoryType.Feet:
                    callback(EquipmentSlot.Feet);
                    return true;
                case InventoryType.Wrists:
                    callback(EquipmentSlot.Wrist);
                    return true;
                case InventoryType.Hands:
                    callback(EquipmentSlot.Hands);
                    return true;
                case InventoryType.Cloak:
                    callback(EquipmentSlot.Cloak);
                    return true;
                case InventoryType.Finger:
                    callback(EquipmentSlot.Finger1);
                    callback(EquipmentSlot.Finger2, true);
                    return true;
                case InventoryType.Trinket:
                    callback(EquipmentSlot.Trinket1);
                    callback(EquipmentSlot.Trinket2, true);
                    return true;
                case InventoryType.Weapon:
                    callback(EquipmentSlot.MainHand);
                    if (canDualWield)
                        callback(EquipmentSlot.OffHand, true);
                    return true;
                case InventoryType.Weapon2Hand:
                    callback(EquipmentSlot.MainHand);
                    if (canDualWield && canTitanGrip)
                        callback(EquipmentSlot.OffHand, true);
                    return true;
                case InventoryType.Ranged:
                case InventoryType.RangedRight:
                case InventoryType.WeaponMainhand:
                    callback(EquipmentSlot.MainHand);
                    return true;
                case InventoryType.Shield:
                case InventoryType.Holdable:
                case InventoryType.WeaponOffhand:
                    callback(EquipmentSlot.OffHand);
                    return true;
                default:
                    return false;
            }
        }

        public void UpdateAverageItemLevelTotal()
        {
            (InventoryType inventoryType, int itemLevel, ObjectGuid guid)[] bestItemLevels = new (InventoryType inventoryType, int itemLevel, ObjectGuid guid)[EquipmentSlot.End];
            float sum = 0;

            ForEachItem(ItemSearchLocation.Everywhere, item =>
            {
                ItemTemplate itemTemplate = item.GetTemplate();
                if (itemTemplate != null && itemTemplate.GetInventoryType() < InventoryType.ProfessionTool && itemTemplate.GetInventoryType() > InventoryType.NonEquip)
                {
                    if (item.IsEquipped())
                    {
                        var itemLevel = item.GetItemLevel(this);
                        InventoryType inventoryType = itemTemplate.GetInventoryType();
                        ref var slotData = ref bestItemLevels[item.InventorySlot];
                        if (itemLevel > slotData.Item2)
                        {
                            sum += itemLevel - slotData.Item2;
                            slotData = (inventoryType, itemLevel, item.GetGUID());
                        }
                    }
                    else if (CanEquipItem(ItemSlot.Null, out List<ItemPosCount> dest, item, true, false) == InventoryResult.Ok)
                    {
                        var itemLevel = item.GetItemLevel(this);
                        InventoryType inventoryType = itemTemplate.GetInventoryType();
                        ForEachEquipmentSlot(inventoryType, m_canDualWield, m_canTitanGrip, (slot, checkDuplicateGuid) =>
                        {
                            if (checkDuplicateGuid)
                            {
                                foreach (var slotData1 in bestItemLevels)
                                {
                                    if (slotData1.guid == item.GetGUID())
                                        return;
                                }
                            }

                            ref var slotData = ref bestItemLevels[slot];
                            if (itemLevel > slotData.itemLevel)
                            {
                                sum += itemLevel - slotData.itemLevel;
                                slotData = (inventoryType, itemLevel, item.GetGUID());
                            }
                        });
                    }
                }
                return true;
            });

            // If main hand is a 2h weapon, count it twice
            var mainHand = bestItemLevels[EquipmentSlot.MainHand];
            if (!m_canTitanGrip && mainHand.inventoryType == InventoryType.Weapon2Hand)
                sum += mainHand.itemLevel;

            sum /= 16.0f;
            SetAverageItemLevelTotal(sum);
        }

        public void UpdateAverageItemLevelEquipped()
        {
            float totalItemLevel = 0;
            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; i++)
            {
                Item item = GetItemByPos(i);
                if (item != null)
                {
                    var itemLevel = item.GetItemLevel(this);
                    totalItemLevel += itemLevel;
                    if (!m_canTitanGrip && i == EquipmentSlot.MainHand
                        && item.GetTemplate().GetInventoryType() == InventoryType.Weapon2Hand) // 2h weapon counts twice
                    {
                        totalItemLevel += itemLevel;
                    }
                }
            }

            totalItemLevel /= 16.0f;
            SetAverageItemLevelEquipped(totalItemLevel);
        }
    }
}
