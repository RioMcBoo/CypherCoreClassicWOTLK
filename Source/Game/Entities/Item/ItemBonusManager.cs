// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Game.Entities
{
    class ItemBonusMgr
    {
        static MultiMap<int /*itemBonusListId*/, ItemBonusRecord> _itemBonusLists = new();
        static Dictionary<short /*itemLevelDelta*/, int /*itemBonusListId*/> _itemLevelDeltaToBonusListContainer = new();
        static SortedMultiMap<int /*itemLevelSelectorQualitySetId*/, ItemLevelSelectorQualityRecord> _itemLevelQualitySelectorQualities = new();
        static MultiMap<int /*itemBonusTreeId*/, ItemBonusTreeNodeRecord> _itemBonusTrees = new();
        static MultiMap<int /*itemId*/, int /*itemBonusTreeId*/> _itemToBonusTree = new();

        public static void Load()
        {
            foreach (var bonus in CliDB.ItemBonusStorage.Values)
                _itemBonusLists.Add(bonus.ParentItemBonusListID, bonus);

            foreach (var itemBonusListLevelDelta in CliDB.ItemBonusListLevelDeltaStorage.Values)
                _itemLevelDeltaToBonusListContainer[itemBonusListLevelDelta.ItemLevelDelta] = itemBonusListLevelDelta.Id;

            foreach (var itemLevelSelectorQuality in CliDB.ItemLevelSelectorQualityStorage.Values)
                _itemLevelQualitySelectorQualities.Add(itemLevelSelectorQuality.ParentILSQualitySetID, itemLevelSelectorQuality);

            foreach (var bonusTreeNode in CliDB.ItemBonusTreeNodeStorage.Values)
                _itemBonusTrees.Add(bonusTreeNode.ParentItemBonusTreeID, bonusTreeNode);

            foreach (var itemBonusTreeAssignment in CliDB.ItemXBonusTreeStorage.Values)
                _itemToBonusTree.Add(itemBonusTreeAssignment.ItemID, itemBonusTreeAssignment.ItemBonusTreeID);
        }

        public static ItemContext GetContextForPlayer(MapDifficultyRecord mapDifficulty, Player player)
        {
            ItemContext evalContext(ItemContext currentContext, ItemContext newContext)
            {
                if (newContext == ItemContext.None)
                    newContext = currentContext;
                else if (newContext == ItemContext.ForceToNone)
                    newContext = ItemContext.None;
                return newContext;
            }

            ItemContext context = ItemContext.None;
            var difficulty = CliDB.DifficultyStorage.LookupByKey((int)mapDifficulty.DifficultyID);
            if (difficulty != null)
                context = evalContext(context, (ItemContext)difficulty.ItemContext);

            context = evalContext(context, (ItemContext)mapDifficulty.ItemContext);

            if (mapDifficulty.ItemContextPickerID != 0)
            {
                int contentTuningId = mapDifficulty.ContentTuningID;

                ItemContextPickerEntryRecord selectedPickerEntry = null;
                foreach (var itemContextPickerEntry in CliDB.ItemContextPickerEntryStorage.Values)
                {
                    if (itemContextPickerEntry.ItemContextPickerID != mapDifficulty.ItemContextPickerID)
                        continue;

                    if (itemContextPickerEntry.PVal <= 0)
                        continue;

                    bool meetsPlayerCondition = false;
                    if (player != null)
                    {
                        var playerCondition = CliDB.PlayerConditionStorage.LookupByKey(itemContextPickerEntry.PlayerConditionID);
                        if (playerCondition != null)
                            meetsPlayerCondition = ConditionManager.IsPlayerMeetingCondition(player, playerCondition);
                    }

                    if ((itemContextPickerEntry.Flags & 0x1) != 0)
                        meetsPlayerCondition = !meetsPlayerCondition;

                    if (!meetsPlayerCondition)
                        continue;

                    if (selectedPickerEntry == null || selectedPickerEntry.OrderIndex < itemContextPickerEntry.OrderIndex)
                        selectedPickerEntry = itemContextPickerEntry;
                }

                if (selectedPickerEntry != null)
                    context = evalContext(context, (ItemContext)selectedPickerEntry.ItemCreationContext);
            }

            return context;
        }

        public static List<ItemBonusRecord> GetItemBonuses(int bonusListId)
        {
            return _itemBonusLists.LookupByKey(bonusListId);
        }

        public static int GetItemBonusListForItemLevelDelta(short delta)
        {
            return _itemLevelDeltaToBonusListContainer.LookupByKey(delta);
        }

        public static bool CanApplyBonusTreeToItem(ItemTemplate itemTemplate, int itemBonusTreeId, ItemBonusGenerationParams generationParams)
        {
            var bonusTreeNodes = _itemBonusTrees.LookupByKey(itemBonusTreeId);
            if (!bonusTreeNodes.Empty())
            {
                bool anyNodeMatched = false;
                foreach (var bonusTreeNode in bonusTreeNodes)
                {
                    ItemContext nodeContext = (ItemContext)bonusTreeNode.ItemContext;
                    if (nodeContext == ItemContext.None || nodeContext == generationParams.Context)
                    {
                        if (anyNodeMatched)
                            return false;

                        anyNodeMatched = true;
                    }
                }
            }

            return true;
        }

        public static int GetBonusTreeIdOverride(int itemBonusTreeId, ItemBonusGenerationParams generationParams)
        {
            return itemBonusTreeId;
        }

        public static void ApplyBonusTreeHelper(ItemTemplate itemTemplate, int itemBonusTreeId, ItemBonusGenerationParams generationParams, int sequenceLevel, ref int itemLevelSelectorId, List<int> bonusListIDs)
        {
            int originalItemBonusTreeId = itemBonusTreeId;

            // override bonus tree with season specific values
            itemBonusTreeId = GetBonusTreeIdOverride(itemBonusTreeId, generationParams);

            if (!CanApplyBonusTreeToItem(itemTemplate, itemBonusTreeId, generationParams))
                return;

            var treeList = _itemBonusTrees.LookupByKey(itemBonusTreeId);
            if (treeList.Empty())
                return;

            foreach (var bonusTreeNode in treeList)
            {
                ItemContext nodeContext = (ItemContext)bonusTreeNode.ItemContext;
                ItemContext requiredContext = nodeContext != ItemContext.ForceToNone ? nodeContext : ItemContext.None;
                if (nodeContext != ItemContext.None && generationParams.Context != requiredContext)
                    continue;               

                if (bonusTreeNode.ChildItemBonusTreeID != 0)
                    ApplyBonusTreeHelper(itemTemplate, bonusTreeNode.ChildItemBonusTreeID, generationParams, sequenceLevel, ref itemLevelSelectorId, bonusListIDs);
                else if (bonusTreeNode.ChildItemBonusListID != 0)
                    bonusListIDs.Add(bonusTreeNode.ChildItemBonusListID);
                else if (bonusTreeNode.ChildItemLevelSelectorID != 0)
                    itemLevelSelectorId = bonusTreeNode.ChildItemLevelSelectorID;                
            }
        }

        public static int GetAzeriteUnlockBonusList(ushort azeriteUnlockMappingSetId, ushort minItemLevel, InventoryType inventoryType)
        {
            return 0;
        }

        public static List<int> GetBonusListsForItem(int itemId, ItemBonusGenerationParams generationParams)
        {
            List<int> bonusListIDs = new();

            ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(itemId);
            if (itemTemplate == null)
                return bonusListIDs;

            int itemLevelSelectorId = 0;

            foreach (var itemBonusTreeId in _itemToBonusTree.LookupByKey(itemId))
                ApplyBonusTreeHelper(itemTemplate, itemBonusTreeId, generationParams, 0, ref itemLevelSelectorId, bonusListIDs);

            var selector = CliDB.ItemLevelSelectorStorage.LookupByKey(itemLevelSelectorId);
            if (selector != null)
            {
                short delta = (short)(selector.MinItemLevel - itemTemplate.GetBaseItemLevel());

                int bonus = GetItemBonusListForItemLevelDelta(delta);
                if (bonus != 0)
                    bonusListIDs.Add(bonus);

                var selectorQualitySet = CliDB.ItemLevelSelectorQualitySetStorage.LookupByKey(selector.ItemLevelSelectorQualitySetID);
                if (selectorQualitySet != null)
                {
                    var itemSelectorQualities = _itemLevelQualitySelectorQualities.LookupByKey(selector.ItemLevelSelectorQualitySetID);
                    if (!itemSelectorQualities.Empty())
                    {
                        ItemQuality quality = ItemQuality.Uncommon;
                        if (selector.MinItemLevel >= selectorQualitySet.IlvlEpic)
                            quality = ItemQuality.Epic;
                        else if (selector.MinItemLevel >= selectorQualitySet.IlvlRare)
                            quality = ItemQuality.Rare;

                        var itemSelectorQuality = itemSelectorQualities.First(record => record.Quality < quality);

                        if (itemSelectorQuality != null)
                            bonusListIDs.Add(itemSelectorQuality.QualityItemBonusListID);
                    }
                }
            }

            return bonusListIDs;
        }

        public static void VisitItemBonusTree(int itemBonusTreeId, Action<ItemBonusTreeNodeRecord> visitor)
        {
            var treeItr = _itemBonusTrees.LookupByKey(itemBonusTreeId);
            if (treeItr.Empty())
                return;

            foreach (var bonusTreeNode in treeItr)
            {
                visitor(bonusTreeNode);
                if (bonusTreeNode.ChildItemBonusTreeID != 0)
                    VisitItemBonusTree(bonusTreeNode.ChildItemBonusTreeID, visitor);
            }
        }

        public static List<int> GetAllBonusListsForTree(int itemBonusTreeId)
        {
            List<int> bonusListIDs = new();
            VisitItemBonusTree(itemBonusTreeId, bonusTreeNode =>
            {
                if (bonusTreeNode.ChildItemBonusListID != 0)
                    bonusListIDs.Add(bonusTreeNode.ChildItemBonusListID);
            });

            return bonusListIDs;
        }

        public struct ItemBonusGenerationParams
        {
            public ItemBonusGenerationParams(ItemContext context, int? mythicPlusKeystoneLevel = null, int? pvpTier = null)
            {
                Context = context;
                MythicPlusKeystoneLevel = mythicPlusKeystoneLevel;
                PvpTier = pvpTier;
            }

            public ItemContext Context;
            public int? MythicPlusKeystoneLevel;
            public int? PvpTier;
        }
    }
}
