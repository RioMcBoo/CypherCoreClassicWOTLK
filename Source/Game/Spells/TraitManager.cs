﻿using Framework.Constants;
using Game.DataStorage;
using Game.Entities;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    class TraitMgr
    {
        public static int COMMIT_COMBAT_TRAIT_CONFIG_CHANGES_SPELL_ID = 384255;
        public static int MAX_COMBAT_TRAIT_CONFIGS = 10;

        static Dictionary<int, NodeGroup> _traitGroups = new();
        static Dictionary<int, Node> _traitNodes = new();
        static Dictionary<int, Tree> _traitTrees = new();
        static SkillType[] _skillLinesByClass = new SkillType[(int)Class.Max];
        static MultiMap<SkillType, Tree> _traitTreesBySkillLine = new();
        static MultiMap<int, Tree> _traitTreesByTraitSystem = new();
        static int _configIdGenerator;
        static MultiMap<int, TraitCurrencySourceRecord> _traitCurrencySourcesByCurrency = new();
        static MultiMap<int, TraitDefinitionEffectPointsRecord> _traitDefinitionEffectPointModifiers = new();
        static MultiMap<int, TraitTreeLoadoutEntryRecord> _traitTreeLoadoutsByChrSpecialization = new();

        public static void Load()
        {
            _configIdGenerator = int.MaxValue;

            MultiMap<int, TraitCondRecord> nodeEntryConditions = new();
            foreach (TraitNodeEntryXTraitCondRecord traitNodeEntryXTraitCondEntry in CliDB.TraitNodeEntryXTraitCondStorage.Values)
            {
                TraitCondRecord traitCondEntry = CliDB.TraitCondStorage.LookupByKey(traitNodeEntryXTraitCondEntry.TraitCondID);
                if (traitCondEntry != null)
                    nodeEntryConditions.Add(traitNodeEntryXTraitCondEntry.TraitNodeEntryID, traitCondEntry);
            }

            MultiMap<int, TraitCostRecord> nodeEntryCosts = new();
            foreach (TraitNodeEntryXTraitCostRecord traitNodeEntryXTraitCostEntry in CliDB.TraitNodeEntryXTraitCostStorage.Values)
            {
                TraitCostRecord traitCostEntry = CliDB.TraitCostStorage.LookupByKey(traitNodeEntryXTraitCostEntry.TraitCostID);
                if (traitCostEntry != null)
                    nodeEntryCosts.Add(traitNodeEntryXTraitCostEntry.TraitNodeEntryID, traitCostEntry);
            }

            MultiMap<int, TraitCondRecord> nodeGroupConditions = new();
            foreach (TraitNodeGroupXTraitCondRecord traitNodeGroupXTraitCondEntry in CliDB.TraitNodeGroupXTraitCondStorage.Values)
            {
                TraitCondRecord traitCondEntry = CliDB.TraitCondStorage.LookupByKey(traitNodeGroupXTraitCondEntry.TraitCondID);
                if (traitCondEntry != null)
                    nodeGroupConditions.Add(traitNodeGroupXTraitCondEntry.TraitNodeGroupID, traitCondEntry);
            }

            MultiMap<int, TraitCostRecord> nodeGroupCosts = new();
            foreach (TraitNodeGroupXTraitCostRecord traitNodeGroupXTraitCostEntry in CliDB.TraitNodeGroupXTraitCostStorage.Values)
            {
                TraitCostRecord traitCondEntry = CliDB.TraitCostStorage.LookupByKey(traitNodeGroupXTraitCostEntry.TraitCostID);
                if (traitCondEntry != null)
                    nodeGroupCosts.Add(traitNodeGroupXTraitCostEntry.TraitNodeGroupID, traitCondEntry);
            }

            MultiMap<int, int> nodeGroups = new();
            foreach (TraitNodeGroupXTraitNodeRecord traitNodeGroupXTraitNodeEntry in CliDB.TraitNodeGroupXTraitNodeStorage.Values)
                nodeGroups.Add(traitNodeGroupXTraitNodeEntry.TraitNodeID, traitNodeGroupXTraitNodeEntry.TraitNodeGroupID);

            MultiMap<int, TraitCondRecord> nodeConditions = new();
            foreach (TraitNodeXTraitCondRecord traitNodeXTraitCondEntry in CliDB.TraitNodeXTraitCondStorage.Values)
            {
                TraitCondRecord traitCondEntry = CliDB.TraitCondStorage.LookupByKey(traitNodeXTraitCondEntry.TraitCondID);
                if (traitCondEntry != null)
                    nodeConditions.Add(traitNodeXTraitCondEntry.TraitNodeID, traitCondEntry);
            }

            MultiMap<int, TraitCostRecord> nodeCosts = new();
            foreach (TraitNodeXTraitCostRecord traitNodeXTraitCostEntry in CliDB.TraitNodeXTraitCostStorage.Values)
            {
                TraitCostRecord traitCostEntry = CliDB.TraitCostStorage.LookupByKey(traitNodeXTraitCostEntry.TraitCostID);
                if (traitCostEntry != null)
                    nodeCosts.Add(traitNodeXTraitCostEntry.TraitNodeID, traitCostEntry);
            }

            MultiMap<int, TraitNodeEntryRecord> nodeEntries = new();
            foreach (TraitNodeXTraitNodeEntryRecord traitNodeXTraitNodeEntryEntry in CliDB.TraitNodeXTraitNodeEntryStorage.Values)
            {
                TraitNodeEntryRecord traitNodeEntryEntry = CliDB.TraitNodeEntryStorage.LookupByKey(traitNodeXTraitNodeEntryEntry.TraitNodeEntryID);
                if (traitNodeEntryEntry != null)
                    nodeEntries.Add(traitNodeXTraitNodeEntryEntry.TraitNodeID, traitNodeEntryEntry);
            }

            MultiMap<int, TraitCostRecord> treeCosts = new();
            foreach (TraitTreeXTraitCostRecord traitTreeXTraitCostEntry in CliDB.TraitTreeXTraitCostStorage.Values)
            {
                TraitCostRecord traitCostEntry = CliDB.TraitCostStorage.LookupByKey(traitTreeXTraitCostEntry.TraitCostID);
                if (traitCostEntry != null)
                    treeCosts.Add(traitTreeXTraitCostEntry.TraitTreeID, traitCostEntry);
            }

            MultiMap<int, TraitCurrencyRecord> treeCurrencies = new();
            foreach (TraitTreeXTraitCurrencyRecord traitTreeXTraitCurrencyEntry in CliDB.TraitTreeXTraitCurrencyStorage.Values)
            {
                TraitCurrencyRecord traitCurrencyEntry = CliDB.TraitCurrencyStorage.LookupByKey(traitTreeXTraitCurrencyEntry.TraitCurrencyID);
                if (traitCurrencyEntry != null)
                    treeCurrencies.Add(traitTreeXTraitCurrencyEntry.TraitTreeID, traitCurrencyEntry);
            }

            MultiMap<int, int> traitTreesIdsByTraitSystem = new();
            foreach (TraitTreeRecord traitTree in CliDB.TraitTreeStorage.Values)
            {
                Tree tree = new();
                tree.Data = traitTree;

                tree.Costs = treeCosts[traitTree.Id];
                tree.Currencies = treeCurrencies[traitTree.Id];

                if (traitTree.TraitSystemID != 0)
                {
                    traitTreesIdsByTraitSystem.Add(traitTree.TraitSystemID, traitTree.Id);
                    tree.ConfigType = TraitConfigType.Generic;
                }

                _traitTrees[traitTree.Id] = tree;
            }

            foreach (TraitNodeGroupRecord traitNodeGroup in CliDB.TraitNodeGroupStorage.Values)
            {
                NodeGroup nodeGroup = new();
                nodeGroup.Data = traitNodeGroup;

                nodeGroup.Conditions = nodeGroupConditions[traitNodeGroup.Id];
                nodeGroup.Costs = nodeGroupCosts[traitNodeGroup.Id];

                _traitGroups[traitNodeGroup.Id] = nodeGroup;
            }

            foreach (TraitNodeRecord traitNode in CliDB.TraitNodeStorage.Values)
            {
                Node node = new();
                node.Data = traitNode;

                Tree tree = _traitTrees.LookupByKey(traitNode.TraitTreeID);
                if (tree != null)
                    tree.Nodes.Add(node);

                foreach (var traitNodeEntry in nodeEntries[traitNode.Id])
                {
                    NodeEntry entry = new();
                    entry.Data = traitNodeEntry;

                    entry.Conditions = nodeEntryConditions[traitNodeEntry.Id];
                    entry.Costs = nodeEntryCosts[traitNodeEntry.Id];

                    node.Entries.Add(entry);
                }

                foreach (var nodeGroupId in nodeGroups[traitNode.Id])
                {
                    NodeGroup nodeGroup = _traitGroups.LookupByKey(nodeGroupId);
                    if (nodeGroup == null)
                        continue;

                    nodeGroup.Nodes.Add(node);
                    node.Groups.Add(nodeGroup);
                }

                node.Conditions = nodeConditions[traitNode.Id];
                node.Costs = nodeCosts[traitNode.Id];

                _traitNodes[traitNode.Id] = node;
            }

            foreach (TraitEdgeRecord traitEdgeEntry in CliDB.TraitEdgeStorage.Values)
            {
                Node left = _traitNodes.LookupByKey(traitEdgeEntry.LeftTraitNodeID);
                Node right = _traitNodes.LookupByKey(traitEdgeEntry.RightTraitNodeID);
                if (left == null || right == null)
                    continue;

                right.ParentNodes.Add(Tuple.Create(left, (TraitEdgeType)traitEdgeEntry.Type));
            }

            foreach (SkillLineXTraitTreeRecord skillLineXTraitTreeEntry in CliDB.SkillLineXTraitTreeStorage.Values)
            {
                Tree tree = _traitTrees.LookupByKey(skillLineXTraitTreeEntry.TraitTreeID);
                if (tree == null)
                    continue;

                SkillLineRecord skillLineEntry = CliDB.SkillLineStorage.LookupByKey(skillLineXTraitTreeEntry.SkillLineID);
                if (skillLineEntry == null)
                    continue;

                _traitTreesBySkillLine.Add(skillLineXTraitTreeEntry.SkillLineID, tree);
                if (skillLineEntry.CategoryID == SkillCategory.Class)
                {
                    foreach (SkillRaceClassInfoRecord skillRaceClassInfo in Global.DB2Mgr.GetSkillRaceClassInfo(skillLineEntry.Id))
                    {
                        for (var i = Class.None; i < Class.Max; ++i)
                        {
                            if (skillRaceClassInfo.ClassMask.HasClass(i))
                                _skillLinesByClass[(int)i] = skillLineXTraitTreeEntry.SkillLineID;
                        }
                    }
                    tree.ConfigType = TraitConfigType.Combat;
                }
                else
                    tree.ConfigType = TraitConfigType.Profession;
            }

            foreach (var (traitSystemId, traitTreeId) in traitTreesIdsByTraitSystem)
            {
                Tree tree = _traitTrees.LookupByKey(traitTreeId);
                if (tree != null)
                    _traitTreesByTraitSystem.Add(traitSystemId, tree);
            }

            foreach (TraitCurrencySourceRecord traitCurrencySource in CliDB.TraitCurrencySourceStorage.Values)
                _traitCurrencySourcesByCurrency.Add(traitCurrencySource.TraitCurrencyID, traitCurrencySource);

            foreach (TraitDefinitionEffectPointsRecord traitDefinitionEffectPoints in CliDB.TraitDefinitionEffectPointsStorage.Values)
                _traitDefinitionEffectPointModifiers.Add(traitDefinitionEffectPoints.TraitDefinitionID, traitDefinitionEffectPoints);

            MultiMap<int, TraitTreeLoadoutEntryRecord> traitTreeLoadoutEntries = new();
            foreach (TraitTreeLoadoutEntryRecord traitTreeLoadoutEntry in CliDB.TraitTreeLoadoutEntryStorage.Values)
                traitTreeLoadoutEntries.Add(traitTreeLoadoutEntry.TraitTreeLoadoutID, traitTreeLoadoutEntry);

            foreach (TraitTreeLoadoutRecord traitTreeLoadout in CliDB.TraitTreeLoadoutStorage.Values)
            {
                var entries = traitTreeLoadoutEntries[traitTreeLoadout.Id].ToList();
                if (!entries.Empty())
                {
                    entries.Sort((left, right) => { return left.OrderIndex.CompareTo(right.OrderIndex); });
                    // there should be only one loadout per spec, we take last one encountered
                    _traitTreeLoadoutsByChrSpecialization.SetValues(traitTreeLoadout.ChrSpecializationID, entries);
                }
            }
        }

        /**
         * Generates new TraitConfig identifier.
         * Because this only needs to be unique for each character we let it overflow
*/
        public static int GenerateNewTraitConfigId()
        {
            if (_configIdGenerator == int.MaxValue)
                _configIdGenerator = 0;

            return ++_configIdGenerator;
        }

        public static TraitConfigType GetConfigTypeForTree(int traitTreeId)
        {
            Tree tree = _traitTrees.LookupByKey(traitTreeId);
            if (tree == null)
                return TraitConfigType.Invalid;

            return tree.ConfigType;
        }

        /**
         * @brief Finds relevant TraitTree identifiers
         * @param traitConfig config data
         * @return Trait tree data
*/
        public static IReadOnlyList<Tree> GetTreesForConfig(TraitConfigPacket traitConfig)
        {
            switch (traitConfig.Type)
            {
                case TraitConfigType.Combat:
                    ChrSpecializationRecord chrSpecializationEntry = CliDB.ChrSpecializationStorage.LookupByKey((int)traitConfig.ChrSpecializationID);
                    if (chrSpecializationEntry != null)
                        return _traitTreesBySkillLine[_skillLinesByClass[(int)chrSpecializationEntry.ClassID]];
                    break;
                case TraitConfigType.Profession:
                    return _traitTreesBySkillLine[traitConfig.SkillLineID];
                case TraitConfigType.Generic:
                    return _traitTreesByTraitSystem[traitConfig.TraitSystemID];
                default:
                    break;
            }

            return null;
        }

        public static bool HasEnoughCurrency(TraitEntryPacket entry, Dictionary<int, int> currencies)
        {
            int getCurrencyCount(int currencyId)
            {
                return currencies.LookupByKey(currencyId);
            }

            Node node = _traitNodes.LookupByKey(entry.TraitNodeID);
            foreach (NodeGroup group in node.Groups)
                foreach (TraitCostRecord cost in group.Costs)
                    if (getCurrencyCount(cost.TraitCurrencyID) < cost.Amount * entry.Rank)
                        return false;

            var nodeEntryItr = node.Entries.Find(nodeEntry => nodeEntry.Data.Id == entry.TraitNodeEntryID);
            if (nodeEntryItr != null)
                foreach (TraitCostRecord cost in nodeEntryItr.Costs)
                    if (getCurrencyCount(cost.TraitCurrencyID) < cost.Amount * entry.Rank)
                        return false;

            foreach (TraitCostRecord cost in node.Costs)
                if (getCurrencyCount(cost.TraitCurrencyID) < cost.Amount * entry.Rank)
                    return false;

            Tree tree = _traitTrees.LookupByKey(node.Data.TraitTreeID);
            if (tree != null)
                foreach (TraitCostRecord cost in tree.Costs)
                    if (getCurrencyCount(cost.TraitCurrencyID) < cost.Amount * entry.Rank)
                        return false;

            return true;
        }

        public static void TakeCurrencyCost(TraitEntryPacket entry, Dictionary<int, int> currencies)
        {
            Node node = _traitNodes.LookupByKey(entry.TraitNodeID);
            foreach (NodeGroup group in node.Groups)
                foreach (TraitCostRecord cost in group.Costs)
                    currencies[cost.TraitCurrencyID] -= cost.Amount * entry.Rank;

            var nodeEntryItr = node.Entries.Find(nodeEntry => nodeEntry.Data.Id == entry.TraitNodeEntryID);
            if (nodeEntryItr != null)
                foreach (TraitCostRecord cost in nodeEntryItr.Costs)
                    currencies[cost.TraitCurrencyID] -= cost.Amount * entry.Rank;

            foreach (TraitCostRecord cost in node.Costs)
                currencies[cost.TraitCurrencyID] -= cost.Amount * entry.Rank;

            Tree tree = _traitTrees.LookupByKey(node.Data.TraitTreeID);
            if (tree != null)
                foreach (TraitCostRecord cost in tree.Costs)
                    currencies[cost.TraitCurrencyID] -= cost.Amount * entry.Rank;
        }

        public static void FillOwnedCurrenciesMap(TraitConfigPacket traitConfig, Player player, Dictionary<int, int> currencies)
        {
            var trees = GetTreesForConfig(traitConfig);

            bool hasTraitNodeEntry(int traitNodeEntryId)
            {
                return traitConfig.Entries.Any(traitEntry => traitEntry.TraitNodeEntryID == traitNodeEntryId && (traitEntry.Rank > 0 || traitEntry.GrantedRanks > 0));
            }

            foreach (Tree tree in trees)
            {
                foreach (TraitCurrencyRecord currency in tree.Currencies)
                {
                    switch (currency.CurrencyType)
                    {
                        case TraitCurrencyType.Gold:
                        {
                            if (!currencies.ContainsKey(currency.Id))
                                currencies[currency.Id] = 0;

                            int amount = currencies[currency.Id];
                            if (player.GetMoney() > (int.MaxValue - amount))
                                amount = int.MaxValue;
                            else
                                amount += (int)player.GetMoney();
                            break;
                        }
                        case TraitCurrencyType.CurrencyTypesBased:
                            if (!currencies.ContainsKey(currency.Id))
                                currencies[currency.Id] = 0;

                            currencies[currency.Id] += player.GetCurrencyQuantity(currency.CurrencyTypesID);
                            break;
                        case TraitCurrencyType.TraitSourced:
                            var currencySources = _traitCurrencySourcesByCurrency[currency.Id];

                            foreach (TraitCurrencySourceRecord currencySource in currencySources)
                            {
                                if (currencySource.QuestID != 0 && !player.IsQuestRewarded(currencySource.QuestID))
                                    continue;

                                if (currencySource.AchievementID != 0 && !player.HasAchieved(currencySource.AchievementID))
                                    continue;

                                if (currencySource.PlayerLevel != 0 && player.GetLevel() < currencySource.PlayerLevel)
                                    continue;

                                if (currencySource.TraitNodeEntryID != 0 && !hasTraitNodeEntry(currencySource.TraitNodeEntryID))
                                    continue;

                                if (!currencies.ContainsKey(currencySource.TraitCurrencyID))
                                    currencies[currencySource.TraitCurrencyID] = 0;

                                currencies[currencySource.TraitCurrencyID] += currencySource.Amount;
                            }

                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public static void FillSpentCurrenciesMap(TraitEntryPacket entry, Dictionary<int, int> cachedCurrencies)
        {
            Node node = _traitNodes.LookupByKey(entry.TraitNodeID);
            foreach (NodeGroup group in node.Groups)
            {
                foreach (TraitCostRecord cost in group.Costs)
                {
                    if (!cachedCurrencies.ContainsKey(cost.TraitCurrencyID))
                        cachedCurrencies[cost.TraitCurrencyID] = 0;

                    cachedCurrencies[cost.TraitCurrencyID] += cost.Amount * entry.Rank;
                }
            }

            var nodeEntryItr = node.Entries.Find(nodeEntry => nodeEntry.Data.Id == entry.TraitNodeEntryID);
            if (nodeEntryItr != null)
            {
                foreach (TraitCostRecord cost in nodeEntryItr.Costs)
                {
                    if (!cachedCurrencies.ContainsKey(cost.TraitCurrencyID))
                        cachedCurrencies[cost.TraitCurrencyID] = 0;

                    cachedCurrencies[cost.TraitCurrencyID] += cost.Amount * entry.Rank;
                }
            }

            foreach (TraitCostRecord cost in node.Costs)
            {
                if (!cachedCurrencies.ContainsKey(cost.TraitCurrencyID))
                    cachedCurrencies[cost.TraitCurrencyID] = 0;

                cachedCurrencies[cost.TraitCurrencyID] += cost.Amount * entry.Rank;
            }

            Tree tree = _traitTrees.LookupByKey(node.Data.TraitTreeID);
            if (tree != null)
            {
                foreach (TraitCostRecord cost in tree.Costs)
                {
                    if (!cachedCurrencies.ContainsKey(cost.TraitCurrencyID))
                        cachedCurrencies[cost.TraitCurrencyID] = 0;

                    cachedCurrencies[cost.TraitCurrencyID] += cost.Amount * entry.Rank;
                }
            }
        }

        public static void FillSpentCurrenciesMap(TraitConfigPacket traitConfig, Dictionary<int, int> cachedCurrencies)
        {
            foreach (TraitEntryPacket entry in traitConfig.Entries)
                FillSpentCurrenciesMap(entry, cachedCurrencies);
        }

        public static bool MeetsTraitCondition(TraitConfigPacket traitConfig, Player player, TraitCondRecord condition, ref Dictionary<int, int> cachedCurrencies)
        {
            if (condition.QuestID != 0 && !player.IsQuestRewarded(condition.QuestID))
                return false;

            if (condition.AchievementID != 0 && !player.HasAchieved(condition.AchievementID))
                return false;

            if (condition.SpecSetID != 0)
            {
                var chrSpecializationId = player.GetPrimarySpecialization();
                if (traitConfig.Type == TraitConfigType.Combat)
                    chrSpecializationId = traitConfig.ChrSpecializationID;

                if (!Global.DB2Mgr.IsSpecSetMember(condition.SpecSetID, chrSpecializationId))
                    return false;
            }

            if (condition.TraitCurrencyID != 0 && condition.SpentAmountRequired != 0)
            {
                if (cachedCurrencies == null)
                {
                    cachedCurrencies = new();
                    FillSpentCurrenciesMap(traitConfig, cachedCurrencies);
                }

                if (condition.TraitNodeGroupID != 0)
                {
                    cachedCurrencies.TryAdd(condition.TraitCurrencyID, 0);
                    if (cachedCurrencies[condition.TraitCurrencyID] < condition.SpentAmountRequired)
                        return false;
                }
                else if (condition.TraitNodeID != 0)
                {
                    cachedCurrencies.TryAdd(condition.TraitCurrencyID, 0);
                    if (cachedCurrencies[condition.TraitCurrencyID] < condition.SpentAmountRequired)
                        return false;
                }
            }

            if (condition.RequiredLevel != 0 && player.GetLevel() < condition.RequiredLevel)
                return false;

            return true;
        }

        public static List<TraitEntry> GetGrantedTraitEntriesForConfig(TraitConfigPacket traitConfig, Player player)
        {
            List<TraitEntry> entries = new();
            var trees = GetTreesForConfig(traitConfig);
            if (trees == null)
                return entries;

            TraitEntry getOrCreateEntry(int nodeId, int entryId)
            {
                TraitEntry foundTraitEntry = entries.Find(traitEntry => traitEntry.TraitNodeID == nodeId && traitEntry.TraitNodeEntryID == entryId);
                if (foundTraitEntry == null)
                {
                    foundTraitEntry = new();
                    foundTraitEntry.TraitNodeID = nodeId;
                    foundTraitEntry.TraitNodeEntryID = entryId;
                    foundTraitEntry.Rank = 0;
                    foundTraitEntry.GrantedRanks = 0;
                    entries.Add(foundTraitEntry);
                }
                return foundTraitEntry;
            }

            Dictionary<int, int> cachedCurrencies = null;

            foreach (Tree tree in trees)
            {
                foreach (Node node in tree.Nodes)
                {
                    foreach (NodeEntry entry in node.Entries)
                        foreach (TraitCondRecord condition in entry.Conditions)
                            if (condition.CondType == TraitConditionType.Granted && MeetsTraitCondition(traitConfig, player, condition, ref cachedCurrencies))
                                getOrCreateEntry(node.Data.Id, entry.Data.Id).GrantedRanks += condition.GrantedRanks;

                    foreach (TraitCondRecord condition in node.Conditions)
                        if (condition.CondType == TraitConditionType.Granted && MeetsTraitCondition(traitConfig, player, condition, ref cachedCurrencies))
                            foreach (NodeEntry entry in node.Entries)
                                getOrCreateEntry(node.Data.Id, entry.Data.Id).GrantedRanks += condition.GrantedRanks;

                    foreach (NodeGroup group in node.Groups)
                        foreach (TraitCondRecord condition in group.Conditions)
                            if (condition.CondType == TraitConditionType.Granted && MeetsTraitCondition(traitConfig, player, condition, ref cachedCurrencies))
                                foreach (NodeEntry entry in node.Entries)
                                    getOrCreateEntry(node.Data.Id, entry.Data.Id).GrantedRanks += condition.GrantedRanks;
                }
            }

            return entries;
        }

        public static bool IsValidEntry(TraitEntryPacket traitEntry)
        {
            Node node = _traitNodes.LookupByKey(traitEntry.TraitNodeID);
            if (node == null)
                return false;

            var entryItr = node.Entries.Find(entry => entry.Data.Id == traitEntry.TraitNodeEntryID);
            if (entryItr == null)
                return false;

            if (entryItr.Data.MaxRanks < traitEntry.Rank + traitEntry.GrantedRanks)
                return false;

            return true;
        }

        public static TalentLearnResult ValidateConfig(TraitConfigPacket traitConfig, Player player, bool requireSpendingAllCurrencies = false)
        {
            int getNodeEntryCount(int traitNodeId)
            {
                return traitConfig.Entries.Count(traitEntry => traitEntry.TraitNodeID == traitNodeId);
            }

            TraitEntryPacket getNodeEntry(int traitNodeId, int traitNodeEntryId)
            {
                return traitConfig.Entries.Find(traitEntry => traitEntry.TraitNodeID == traitNodeId && traitEntry.TraitNodeEntryID == traitNodeEntryId);
            }

            bool isNodeFullyFilled(Node node)
            {
                if (node.Data.NodeType == TraitNodeType.Selection)
                    return node.Entries.Any(nodeEntry =>
                    {
                        TraitEntryPacket traitEntry = getNodeEntry(node.Data.Id, nodeEntry.Data.Id);
                        return traitEntry != null && (traitEntry.Rank + traitEntry.GrantedRanks) == nodeEntry.Data.MaxRanks;
                    });

                return node.Entries.All(nodeEntry =>
                {
                    TraitEntryPacket traitEntry = getNodeEntry(node.Data.Id, nodeEntry.Data.Id);
                    return traitEntry != null && (traitEntry.Rank + traitEntry.GrantedRanks) == nodeEntry.Data.MaxRanks;
                });
            }

            Dictionary<int, int> spentCurrencies = new();
            FillSpentCurrenciesMap(traitConfig, spentCurrencies);

            bool meetsConditions(IReadOnlyList<TraitCondRecord> conditions)
            {
                bool hasConditions = false;
                foreach (TraitCondRecord condition in conditions)
                {
                    if (condition.CondType == TraitConditionType.Available || condition.CondType == TraitConditionType.Visible)
                    {
                        if (MeetsTraitCondition(traitConfig, player, condition, ref spentCurrencies))
                            return true;

                        hasConditions = true;
                    }
                }

                return !hasConditions;
            }

            foreach (TraitEntryPacket traitEntry in traitConfig.Entries)
            {
                if (!IsValidEntry(traitEntry))
                    return TalentLearnResult.FailedUnknown;

                Node node = _traitNodes.LookupByKey(traitEntry.TraitNodeID);
                if (node.Data.NodeType == TraitNodeType.Selection)
                    if (getNodeEntryCount(traitEntry.TraitNodeID) != 1)
                        return TalentLearnResult.FailedUnknown;

                foreach (NodeEntry entry in node.Entries)
                    if (!meetsConditions(entry.Conditions))
                        return TalentLearnResult.FailedUnknown;

                if (!meetsConditions(node.Conditions))
                    return TalentLearnResult.FailedUnknown;

                foreach (NodeGroup group in node.Groups)
                    if (!meetsConditions(group.Conditions))
                        return TalentLearnResult.FailedUnknown;

                if (!node.ParentNodes.Empty())
                {
                    bool hasAnyParentTrait = false;
                    foreach (var (parentNode, edgeType) in node.ParentNodes)
                    {
                        if (!isNodeFullyFilled(parentNode))
                        {
                            if (edgeType == TraitEdgeType.RequiredForAvailability)
                                return TalentLearnResult.FailedNotEnoughTalentsInPrimaryTree;

                            continue;
                        }

                        hasAnyParentTrait = true;
                    }

                    if (!hasAnyParentTrait)
                        return TalentLearnResult.FailedNotEnoughTalentsInPrimaryTree;
                }
            }

            Dictionary<int, int> grantedCurrencies = new();
            FillOwnedCurrenciesMap(traitConfig, player, grantedCurrencies);

            foreach (var (traitCurrencyId, spentAmount) in spentCurrencies)
            {
                if (CliDB.TraitCurrencyStorage.LookupByKey(traitCurrencyId).CurrencyType != TraitCurrencyType.TraitSourced)
                    continue;

                if (spentAmount == 0)
                    continue;

                int grantedCount = grantedCurrencies.LookupByKey(traitCurrencyId);
                if (grantedCount == 0 || grantedCount < spentAmount)
                    return TalentLearnResult.FailedNotEnoughTalentsInPrimaryTree;

            }

            if (requireSpendingAllCurrencies && traitConfig.Type == TraitConfigType.Combat)
            {
                foreach (var (traitCurrencyId, grantedAmount) in grantedCurrencies)
                {
                    if (grantedAmount == 0)
                        continue;

                    int spentAmount = spentCurrencies.LookupByKey(traitCurrencyId);
                    if (spentAmount == 0 || spentAmount != grantedAmount)
                        return TalentLearnResult.UnspentTalentPoints;
                }
            }

            return TalentLearnResult.LearnOk;
        }

        public static IReadOnlyList<TraitDefinitionEffectPointsRecord> GetTraitDefinitionEffectPointModifiers(int traitDefinitionId)
        {
            return _traitDefinitionEffectPointModifiers[traitDefinitionId];
        }

        public static void InitializeStarterBuildTraitConfig(TraitConfigPacket traitConfig, Player player)
        {
            traitConfig.Entries.Clear();
            var trees = GetTreesForConfig(traitConfig);
            if (trees == null)
                return;

            foreach (TraitEntry grant in GetGrantedTraitEntriesForConfig(traitConfig, player))
            {
                TraitEntryPacket newEntry = new();
                newEntry.TraitNodeID = grant.TraitNodeID;
                newEntry.TraitNodeEntryID = grant.TraitNodeEntryID;
                newEntry.GrantedRanks = grant.GrantedRanks;
                traitConfig.Entries.Add(newEntry);
            }

            Dictionary<int, int> currencies = new();
            FillOwnedCurrenciesMap(traitConfig, player, currencies);

            var loadoutEntries = _traitTreeLoadoutsByChrSpecialization[(int)traitConfig.ChrSpecializationID];
            TraitEntryPacket findEntry(TraitConfigPacket config, int traitNodeId, int traitNodeEntryId)
            {
                return config.Entries.Find(traitEntry => traitEntry.TraitNodeID == traitNodeId && traitEntry.TraitNodeEntryID == traitNodeEntryId);
            }

            foreach (TraitTreeLoadoutEntryRecord loadoutEntry in loadoutEntries)
            {
                int addedRanks = loadoutEntry.NumPoints;
                Node node = _traitNodes.LookupByKey(loadoutEntry.SelectedTraitNodeID);

                TraitEntryPacket newEntry = new();
                newEntry.TraitNodeID = loadoutEntry.SelectedTraitNodeID;
                newEntry.TraitNodeEntryID = loadoutEntry.SelectedTraitNodeEntryID;
                if (newEntry.TraitNodeEntryID == 0)
                    newEntry.TraitNodeEntryID = node.Entries[0].Data.Id;

                TraitEntryPacket entryInConfig = findEntry(traitConfig, newEntry.TraitNodeID, newEntry.TraitNodeEntryID);

                if (entryInConfig != null)
                    addedRanks -= entryInConfig.Rank;

                newEntry.Rank = addedRanks;

                if (!HasEnoughCurrency(newEntry, currencies))
                    continue;

                if (entryInConfig != null)
                    entryInConfig.Rank += addedRanks;
                else
                    traitConfig.Entries.Add(newEntry);

                TakeCurrencyCost(newEntry, currencies);
            }
        }
    }

    class NodeEntry
    {
        public TraitNodeEntryRecord Data;
        public IReadOnlyList<TraitCondRecord> Conditions;
        public IReadOnlyList<TraitCostRecord> Costs;
    }

    class Node
    {
        public TraitNodeRecord Data;
        public List<NodeEntry> Entries = new();
        public List<NodeGroup> Groups = new();
        public List<Tuple<Node, TraitEdgeType>> ParentNodes = new(); // TraitEdge::LeftTraitNodeID
        public IReadOnlyList<TraitCondRecord> Conditions;
        public IReadOnlyList<TraitCostRecord> Costs;
    }

    class NodeGroup
    {
        public TraitNodeGroupRecord Data;
        public IReadOnlyList<TraitCondRecord> Conditions;
        public IReadOnlyList<TraitCostRecord> Costs;
        public List<Node> Nodes = new();
    }

    class Tree
    {
        public TraitTreeRecord Data;
        public List<Node> Nodes = new();
        public IReadOnlyList<TraitCostRecord> Costs;
        public IReadOnlyList<TraitCurrencyRecord> Currencies;
        public TraitConfigType ConfigType;
    }
}
