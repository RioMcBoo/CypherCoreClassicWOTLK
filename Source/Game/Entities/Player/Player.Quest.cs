﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.AI;
using Game.DataStorage;
using Game.Groups;
using Game.Mails;
using Game.Maps;
using Game.Misc;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public partial class Player
    {
        public int GetSharedQuestID() { return m_sharedQuestId; }
        public ObjectGuid GetPlayerSharingQuest() { return m_playerSharingQuest; }
        public void SetQuestSharingInfo(ObjectGuid guid, int id) { m_playerSharingQuest = guid; m_sharedQuestId = id; }
        public void ClearQuestSharingInfo() { m_playerSharingQuest = ObjectGuid.Empty; m_sharedQuestId = 0; }

        RelativeTime GetInGameTime() { return m_ingametime; }
        public void SetInGameTime(RelativeTime time) { m_ingametime = time; }

        void AddTimedQuest(int questId) { m_timedquests.Add(questId); }
        public void RemoveTimedQuest(int questId) { m_timedquests.Remove(questId); }
        public IReadOnlyList<int> GetTimedQuests() { return m_timedquests; }

        public List<int> GetRewardedQuests() { return m_RewardedQuests; }
        Dictionary<int, QuestStatusData> GetQuestStatusMap() { return m_QuestStatus; }

        public int GetQuestLevel(Quest quest)
        {
            if (quest == null)
                return GetLevel();
            return quest.Level > 0 ? quest.Level : Math.Min(GetLevel(), quest.MaxScalingLevel);
        }

        public int GetRewardedQuestCount() { return m_RewardedQuests.Count; }

        public void LearnQuestRewardedSpells(Quest quest)
        {
            //wtf why is rewardspell a uint if it can me -1
            int spell_id = quest.RewardSpell;
            int src_spell_id = quest.SourceSpellID;

            // skip quests without rewarded spell
            if (spell_id == 0)
                return;

            // if RewSpellCast = -1 we remove aura do to SrcSpell from player.
            if (spell_id == -1 && src_spell_id != 0)
            {
                RemoveAurasDueToSpell(src_spell_id);
                return;
            }

            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spell_id, Difficulty.None);
            if (spellInfo == null)
                return;

            // check learned spells state
            bool found = false;
            foreach (var spellEffectInfo in spellInfo.GetEffects())
            {
                if (spellEffectInfo.IsEffect(SpellEffectName.LearnSpell) && !HasSpell(spellEffectInfo.TriggerSpell))
                {
                    found = true;
                    break;
                }
            }

            // skip quests with not teaching spell or already known spell
            if (!found)
                return;

            SpellEffectInfo effect = spellInfo.GetEffect(0);
            int learned_0 = effect.TriggerSpell;
            if (!HasSpell(learned_0))
            {
                found = false;
                var skills = Global.SpellMgr.GetSkillLineAbilityMapBounds(learned_0);
                foreach (var skillLine in skills)
                {
                    if (skillLine.AcquireMethod == AbilityLearnType.RewardedFromQuest)
                    {
                        found = true;
                        break;
                    }
                }

                // profession specialization can be re-learned from npc
                if (!found)
                    return;
            }

            CastSpell(this, spell_id, true);
        }

        public void LearnQuestRewardedSpells()
        {
            // learn spells received from quest completing
            foreach (var questId in m_RewardedQuests)
            {
                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest == null)
                    continue;

                LearnQuestRewardedSpells(quest);
            }
        }

        public void DailyReset()
        {
            foreach (int questId in m_activePlayerData.DailyQuestsCompleted)
            {
                uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(questId);
                if (questBit != 0)
                    SetQuestCompletedBit(questBit, false);
            }

            DailyQuestsReset dailyQuestsReset = new();
            dailyQuestsReset.Count = m_activePlayerData.DailyQuestsCompleted.Size();
            SendPacket(dailyQuestsReset);

            ClearDynamicUpdateFieldValues(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.DailyQuestsCompleted));

            m_DFQuests.Clear(); // Dungeon Finder Quests.

            for (ushort slot = 0; slot < SharedConst.MaxQuestLogSize; ++slot)
            {
                int questId = GetQuestSlotQuestId(slot);
                if (questId == 0)
                    continue;

                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest == null || !quest.IsDaily() || !quest.HasAnyFlag(QuestFlagsEx.RemoveOnPeriodicReset))
                    continue;

                SetQuestSlot(slot, 0);
                AbandonQuest(questId);
                RemoveActiveQuest(questId);
                DespawnPersonalSummonsForQuest(questId);

                if (quest.LimitTime != TimeSpan.Zero)
                    RemoveTimedQuest(questId);

                SendPacket(new QuestForceRemoved(questId));
            }

            // DB data deleted in caller
            m_DailyQuestChanged = false;
            m_lastDailyQuestTime = ServerTime.Zero;

            FailCriteria(CriteriaFailEvent.DailyQuestsCleared, 0);
        }

        public void ResetWeeklyQuestStatus()
        {
            if (m_weeklyquests.Empty())
                return;

            foreach (var questId in m_weeklyquests)
            {
                uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(questId);
                if (questBit != 0)
                    SetQuestCompletedBit(questBit, false);
            }

            for (ushort slot = 0; slot < SharedConst.MaxQuestLogSize; ++slot)
            {
                var questId = GetQuestSlotQuestId(slot);
                if (questId == 0)
                    continue;

                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest == null || !quest.IsWeekly() || !quest.HasAnyFlag(QuestFlagsEx.RemoveOnWeeklyReset))
                    continue;

                SetQuestSlot(slot, 0);
                AbandonQuest(questId);
                RemoveActiveQuest(questId);
                DespawnPersonalSummonsForQuest(questId);

                if (quest.LimitTime != TimeSpan.Zero)
                    RemoveTimedQuest(questId);

                SendPacket(new QuestForceRemoved(questId));
            }

            m_weeklyquests.Clear();
            // DB data deleted in caller
            m_WeeklyQuestChanged = false;

        }

        public void ResetSeasonalQuestStatus(ushort event_id, RealmTime eventStartTime)
        {
            // DB data deleted in caller
            m_SeasonalQuestChanged = false;

            var eventList = m_seasonalquests.LookupByKey(event_id);
            if (eventList == null)
                return;

            foreach (var (questId, completedTime) in eventList.ToList())
            {
                if (completedTime < eventStartTime)
                {
                    uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(questId);
                    if (questBit != 0)
                        SetQuestCompletedBit(questBit, false);

                    eventList.Remove(questId);
                }
            }

            if (eventList.Empty())
                m_seasonalquests.Remove(event_id);
        }

        public void ResetMonthlyQuestStatus()
        {
            if (m_monthlyquests.Empty())
                return;

            foreach (var questId in m_monthlyquests)
            {
                uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(questId);
                if (questBit != 0)
                    SetQuestCompletedBit(questBit, false);
            }

            m_monthlyquests.Clear();
            // DB data deleted in caller
            m_MonthlyQuestChanged = false;
        }

        public bool CanInteractWithQuestGiver(WorldObject questGiver)
        {
            switch (questGiver.GetTypeId())
            {
                case TypeId.Unit:
                    return GetNPCIfCanInteractWith(questGiver.GetGUID(), NPCFlags1.QuestGiver, NPCFlags2.None) != null;
                case TypeId.GameObject:
                    return GetGameObjectIfCanInteractWith(questGiver.GetGUID(), GameObjectTypes.QuestGiver) != null;
                case TypeId.Player:
                    return IsAlive() && questGiver.ToPlayer().IsAlive();
                case TypeId.Item:
                    return IsAlive();
                default:
                    break;
            }
            return false;
        }

        public bool IsQuestRewarded(int quest_id)
        {
            return m_RewardedQuests.Contains(quest_id);
        }

        public void PrepareQuestMenu(ObjectGuid guid)
        {
            QuestRelationResult questRelations;
            QuestRelationResult questInvolvedRelations;

            // pets also can have quests
            Creature creature = ObjectAccessor.GetCreatureOrPetOrVehicle(this, guid);
            if (creature != null)
            {
                questRelations = Global.ObjectMgr.GetCreatureQuestRelations(creature.GetEntry());
                questInvolvedRelations = Global.ObjectMgr.GetCreatureQuestInvolvedRelations(creature.GetEntry());
            }
            else
            {
                //we should obtain map from GetMap() in 99% of cases. Special case
                //only for quests which cast teleport spells on player
                Map _map = IsInWorld ? GetMap() : Global.MapMgr.FindMap(GetMapId(), GetInstanceId());
                Cypher.Assert(_map != null);
                GameObject gameObject = _map.GetGameObject(guid);
                if (gameObject != null)
                {
                    questRelations = Global.ObjectMgr.GetGOQuestRelations(gameObject.GetEntry());
                    questInvolvedRelations = Global.ObjectMgr.GetGOQuestInvolvedRelations(gameObject.GetEntry());
                }
                else
                    return;
            }

            QuestMenu qm = PlayerTalkClass.GetQuestMenu();
            qm.ClearMenu();

            foreach (var questId in questInvolvedRelations)
            {
                QuestStatus status = GetQuestStatus(questId);
                if (status == QuestStatus.Complete)
                    qm.AddMenuItem(questId, 4);
                else if (status == QuestStatus.Incomplete)
                    qm.AddMenuItem(questId, 4);
            }

            foreach (var questId in questRelations)
            {
                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest == null)
                    continue;

                if (!CanTakeQuest(quest, false))
                    continue;

                if (quest.IsTurnIn() && (!quest.IsRepeatable() || quest.IsDaily() || quest.IsWeekly() || quest.IsMonthly()))
                    qm.AddMenuItem(questId, 0);
                else if (quest.IsTurnIn())
                    qm.AddMenuItem(questId, 4);
                else if (GetQuestStatus(questId) == QuestStatus.None)
                    qm.AddMenuItem(questId, 2);
            }
        }

        public void SendPreparedQuest(WorldObject source)
        {
            QuestMenu questMenu = PlayerTalkClass.GetQuestMenu();
            if (questMenu.IsEmpty())
                return;

            // single element case
            if (questMenu.GetMenuItemCount() == 1)
            {
                QuestMenuItem qmi0 = questMenu.GetItem(0);
                var questId = qmi0.QuestId;

                // Auto open
                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest != null)
                {
                    if (qmi0.QuestIcon == 4)
                        PlayerTalkClass.SendQuestGiverRequestItems(quest, source.GetGUID(), CanRewardQuest(quest, false), true);

                    // Send completable on repeatable and autoCompletable quest if player don't have quest
                    // @todo verify if check for !quest.IsDaily() is really correct (possibly not)
                    else if (!source.HasQuest(questId) && !source.HasInvolvedQuest(questId))
                        PlayerTalkClass.SendCloseGossip();
                    else
                    {
                        if (quest.IsAutoAccept() && CanAddQuest(quest, true) && CanTakeQuest(quest, true))
                            AddQuestAndCheckCompletion(quest, source);

                        if (quest.IsTurnIn() && quest.IsRepeatable() && !quest.IsDailyOrWeekly() && !quest.IsMonthly())
                            PlayerTalkClass.SendQuestGiverRequestItems(quest, source.GetGUID(), CanCompleteRepeatableQuest(quest), true);
                        else if (quest.IsTurnIn() && !quest.IsDailyOrWeekly() && !quest.IsMonthly())
                            PlayerTalkClass.SendQuestGiverRequestItems(quest, source.GetGUID(), CanRewardQuest(quest, false), true);
                        else
                            PlayerTalkClass.SendQuestGiverQuestDetails(quest, source.GetGUID(), true, false);

                    }

                    return;
                }
            }

            PlayerTalkClass.SendQuestGiverQuestListMessage(source);
        }

        public bool IsActiveQuest(int quest_id)
        {
            return m_QuestStatus.ContainsKey(quest_id);
        }

        public Quest GetNextQuest(WorldObject questGiver, Quest quest)
        {
            var nextQuestID = quest.NextQuestInChain;
            if (nextQuestID == 0)
                return null;

            if (questGiver == this)
            {
                if (!quest.HasAnyFlag(QuestFlags.AutoComplete))
                    return null;

                return Global.ObjectMgr.GetQuestTemplate(nextQuestID);
            }

            //we should obtain map pointer from GetMap() in 99% of cases. Special case
            //only for quests which cast teleport spells on player
            if (!IsInMap(questGiver))
                return null;

            if (!questGiver.HasQuest(nextQuestID))
                return null;

            return Global.ObjectMgr.GetQuestTemplate(nextQuestID);
        }

        public bool CanSeeStartQuest(Quest quest)
        {
            if (!Global.DisableMgr.IsDisabledFor(DisableType.Quest, quest.Id, this) && SatisfyQuestClass(quest, false) && SatisfyQuestRace(quest, false) &&
                SatisfyQuestSkill(quest, false) && SatisfyQuestExclusiveGroup(quest, false) && SatisfyQuestReputation(quest, false) &&
                SatisfyQuestDependentQuests(quest, false) && SatisfyQuestDay(quest, false) && SatisfyQuestWeek(quest, false) &&
                SatisfyQuestMonth(quest, false) && SatisfyQuestSeasonal(quest, false))
            {
                return GetLevel() + WorldConfig.Values[WorldCfg.QuestHighLevelHideDiff].Int32 >= quest.MinLevel;
            }

            return false;
        }

        public bool CanTakeQuest(Quest quest, bool msg)
        {
            return !Global.DisableMgr.IsDisabledFor(DisableType.Quest, quest.Id, this)
                && SatisfyQuestStatus(quest, msg) && SatisfyQuestExclusiveGroup(quest, msg)
                && SatisfyQuestClass(quest, msg) && SatisfyQuestRace(quest, msg) && SatisfyQuestLevel(quest, msg)
                && SatisfyQuestSkill(quest, msg) && SatisfyQuestReputation(quest, msg)
                && SatisfyQuestDependentQuests(quest, msg) && SatisfyQuestTimed(quest, msg)
                && SatisfyQuestDay(quest, msg)
                && SatisfyQuestWeek(quest, msg) && SatisfyQuestMonth(quest, msg)
                && SatisfyQuestSeasonal(quest, msg) && SatisfyQuestConditions(quest, msg);
        }

        public bool CanAddQuest(Quest quest, bool msg)
        {
            if (!SatisfyQuestLog(msg))
                return false;

            var srcitem = quest.SourceItemId;
            if (srcitem > 0)
            {
                var count = quest.SourceItemIdCount;
                InventoryResult msg2 = CanStoreNewItem(ItemPos.Undefined, out _, srcitem, count);

                // player already have max number (in most case 1) source item, no additional item needed and quest can be added.
                if (msg2 == InventoryResult.ItemMaxCount)
                    return true;

                if (msg2 != InventoryResult.Ok)
                {
                    SendEquipError(msg2, null, null, srcitem);
                    return false;
                }
            }
            return true;
        }

        public bool CanCompleteQuest(int questId, int ignoredQuestObjectiveId = 0)
        {
            if (questId != 0)
            {
                Quest qInfo = Global.ObjectMgr.GetQuestTemplate(questId);
                if (qInfo == null)
                    return false;

                if (!qInfo.IsRepeatable() && GetQuestRewardStatus(questId))
                    return false;                                   // not allow re-complete quest

                // auto complete quest
                if (qInfo.IsTurnIn() && CanTakeQuest(qInfo, false))
                    return true;

                var q_status = m_QuestStatus.LookupByKey(questId);
                if (q_status == null)
                    return false;

                if (q_status.Status == QuestStatus.Incomplete)
                {
                    foreach (QuestObjective obj in qInfo.Objectives)
                    {
                        if (ignoredQuestObjectiveId != 0 && obj.Id == ignoredQuestObjectiveId)
                            continue;

                        if (!obj.Flags.HasAnyFlag(QuestObjectiveFlags.Optional) 
                            && !obj.Flags.HasAnyFlag(QuestObjectiveFlags.PartOfProgressBar))
                        {
                            if (!IsQuestObjectiveComplete(q_status.Slot, qInfo, obj))
                                return false;
                        }
                    }

                    if ((qInfo.HasAnyFlag(QuestFlags.CompletionEvent) || qInfo.HasAnyFlag(QuestFlags.CompletionAreaTrigger))
                        && !q_status.Explored)
                    {
                        return false;
                    }

                    if (qInfo.LimitTime != TimeSpan.Zero && q_status.Timer == TimeSpan.Zero)
                        return false;

                    return true;
                }
            }
            return false;
        }

        public bool CanCompleteRepeatableQuest(Quest quest)
        {
            // Solve problem that player don't have the quest and try complete it.
            // if repeatable she must be able to complete event if player don't have it.
            // Seem that all repeatable quest are DELIVER Flag so, no need to add more.
            if (!CanTakeQuest(quest, false))
                return false;

            if (!CanRewardQuest(quest, false))
                return false;

            return true;
        }

        public bool CanRewardQuest(Quest quest, bool msg)
        {
            // quest is disabled
            if (Global.DisableMgr.IsDisabledFor(DisableType.Quest, quest.Id, this))
                return false;

            // not auto complete quest and not completed quest (only cheating case, then ignore without message)
            if (!quest.IsDFQuest() && !quest.IsTurnIn() && GetQuestStatus(quest.Id) != QuestStatus.Complete)
                return false;

            // daily quest can't be rewarded (25 daily quest already completed)
            if (!SatisfyQuestDay(quest, msg) || !SatisfyQuestWeek(quest, msg) || !SatisfyQuestMonth(quest, msg) || !SatisfyQuestSeasonal(quest, msg))
                return false;

            // player no longer satisfies the quest's requirements (skill level etc.)
            if (!SatisfyQuestLevel(quest, msg) || !SatisfyQuestSkill(quest, msg) || !SatisfyQuestReputation(quest, msg))
                return false;

            // rewarded and not repeatable quest (only cheating case, then ignore without message)
            if (GetQuestRewardStatus(quest.Id))
                return false;

            // prevent receive reward with quest items in bank
            if (quest.HasQuestObjectiveType(QuestObjectiveType.Item))
            {
                foreach (QuestObjective obj in quest.Objectives)
                {
                    if (obj.Type != QuestObjectiveType.Item || obj.Flags2.HasAnyFlag(QuestObjectiveFlags2.QuestBoundItem))
                        continue;

                    if (GetItemCount(obj.ObjectID) < obj.Amount)
                    {
                        if (msg)
                            SendEquipError(InventoryResult.ItemNotFound, null, null, obj.ObjectID);
                        return false;
                    }
                }
            }

            foreach (QuestObjective obj in quest.Objectives)
            {
                switch (obj.Type)
                {
                    case QuestObjectiveType.Currency:
                        if (!HasCurrency(obj.ObjectID, obj.Amount))
                            return false;
                        break;
                    case QuestObjectiveType.Money:
                        if (!HasEnoughMoney(obj.Amount))
                            return false;
                        break;
                }
            }

            return true;
        }

        public bool CanRewardQuest(Quest quest, LootItemType rewardType, int rewardId, bool msg)
        {
            if (quest.GetRewChoiceItemsCount() > 0)
            {
                switch (rewardType)
                {
                    case LootItemType.Item:
                        for (int i = 0; i < SharedConst.QuestRewardChoicesCount; ++i)
                        {
                            if (quest.RewardChoiceItemId[i] != 0 && quest.RewardChoiceItemType[i] == LootItemType.Item && quest.RewardChoiceItemId[i] == rewardId)
                            {
                                InventoryResult res = CanStoreNewItem(ItemPos.Undefined, out _, quest.RewardChoiceItemId[i], quest.RewardChoiceItemCount[i]);
                                if (res != InventoryResult.Ok)
                                {
                                    if (msg)
                                        SendQuestFailed(quest.Id, res);

                                    return false;
                                }
                            }
                        }
                        break;
                    case LootItemType.Currency:
                        break;
                }
            }

            if (quest.GetRewItemsCount() > 0)
            {
                for (int i = 0; i < quest.GetRewItemsCount(); ++i)
                {
                    if (quest.RewardItemId[i] != 0)
                    {
                        InventoryResult res = CanStoreNewItem(ItemPos.Undefined, out _, quest.RewardItemId[i], quest.RewardItemCount[i]);
                        if (res != InventoryResult.Ok)
                        {
                            if (msg)
                                SendQuestFailed(quest.Id, res);

                            return false;

                        }
                    }
                }
            }

            // QuestPackageItem.db2
            if (quest.PackageID != 0)
            {
                bool hasFilteredQuestPackageReward = false;
                var questPackageItems = Global.DB2Mgr.GetQuestPackageItems(quest.PackageID);
                if (questPackageItems != null)
                {
                    foreach (var questPackageItem in questPackageItems)
                    {
                        if (questPackageItem.ItemID != rewardId)
                            continue;

                        if (CanSelectQuestPackageItem(questPackageItem))
                        {
                            hasFilteredQuestPackageReward = true;
                            InventoryResult res = CanStoreNewItem(ItemPos.Undefined, out _, questPackageItem.ItemID, questPackageItem.ItemQuantity);
                            if (res != InventoryResult.Ok)
                            {
                                SendEquipError(res, null, null, questPackageItem.ItemID);
                                return false;
                            }
                        }
                    }
                }

                if (!hasFilteredQuestPackageReward)
                {
                    List<QuestPackageItemRecord> questPackageItems1 = Global.DB2Mgr.GetQuestPackageItemsFallback(quest.PackageID);
                    if (questPackageItems1 != null)
                    {
                        foreach (QuestPackageItemRecord questPackageItem in questPackageItems1)
                        {
                            if (questPackageItem.ItemID != rewardId)
                                continue;
                                InventoryResult res = CanStoreNewItem(ItemPos.Undefined, out _, questPackageItem.ItemID, questPackageItem.ItemQuantity);
                            if (res != InventoryResult.Ok)
                            {
                                SendEquipError(res, null, null, questPackageItem.ItemID);
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        public void AddQuestAndCheckCompletion(Quest quest, WorldObject questGiver)
        {
            AddQuest(quest, questGiver);

            if (CanCompleteQuest(quest.Id))
                CompleteQuest(quest.Id);

            if (questGiver == null)
                return;

            switch (questGiver.GetTypeId())
            {
                case TypeId.Unit:
                    PlayerTalkClass.ClearMenus();
                    questGiver.ToCreature().GetAI().OnQuestAccept(this, quest);
                    break;
                case TypeId.Item:
                case TypeId.Container:
                case TypeId.AzeriteItem:
                case TypeId.AzeriteEmpoweredItem:
                {
                    Item item = (Item)questGiver;
                    Global.ScriptMgr.OnQuestAccept(this, item, quest);

                    // There are two cases where the source item is not destroyed when the quest is accepted:
                    // - It is required to finish the quest, and is an unique item
                    // - It is the same item present in the source item field (item that would be given on quest accept)
                    bool destroyItem = true;
                    foreach (QuestObjective obj in quest.Objectives)
                    {
                        if (obj.Type == QuestObjectiveType.Item && obj.ObjectID == item.GetEntry() && item.GetTemplate().GetMaxCount() > 0)
                        {
                            destroyItem = false;
                            break;
                        }
                    }

                    if (quest.SourceItemId == item.GetEntry())
                        destroyItem = false;

                    if (destroyItem)
                        DestroyItem(item.InventoryPosition, true);

                    break;
                }
                case TypeId.GameObject:
                    PlayerTalkClass.ClearMenus();
                    questGiver.ToGameObject().GetAI().OnQuestAccept(this, quest);
                    break;
                default:
                    break;
            }
        }

        public void AddQuest(Quest quest, WorldObject questGiver)
        {
            ushort logSlot = FindQuestSlot(0);
            if (logSlot >= SharedConst.MaxQuestLogSize) // Player does not have any free slot in the quest log
                return;

            var questId = quest.Id;

            // if not exist then created with set uState == NEW and rewarded=false
            if (!m_QuestStatus.ContainsKey(questId))
                m_QuestStatus[questId] = new QuestStatusData();

            QuestStatusData questStatusData = m_QuestStatus.LookupByKey(questId);
            QuestStatus oldStatus = questStatusData.Status;

            // check for repeatable quests status reset
            SetQuestSlot(logSlot, questId);
            questStatusData.Slot = logSlot;
            questStatusData.Status = QuestStatus.Incomplete;
            questStatusData.Explored = false;

            foreach (QuestObjective obj in quest.Objectives)
            {
                m_questObjectiveStatus.Add((obj.Type, obj.ObjectID), 
                    new QuestObjectiveStatusData() { QuestStatusPair = (questId, questStatusData), ObjectiveId = obj.Id });
                
                switch (obj.Type)
                {
                    case QuestObjectiveType.MinReputation:
                    case QuestObjectiveType.MaxReputation:
                        FactionRecord factionEntry = CliDB.FactionStorage.LookupByKey(obj.ObjectID);
                        if (factionEntry != null)
                            GetReputationMgr().SetVisible(factionEntry);
                        break;
                    case QuestObjectiveType.CriteriaTree:
                        m_questObjectiveCriteriaMgr.ResetCriteriaTree(obj.ObjectID);
                        break;
                    default:
                        break;
                }
            }

            GiveQuestSourceItem(quest);
            AdjustQuestObjectiveProgress(quest);

            ServerTime endTime = ServerTime.Zero;
            TimeSpan limittime = quest.LimitTime;

            if (limittime != TimeSpan.Zero)
            {
                // shared timed quest
                if (questGiver != null && questGiver.IsTypeId(TypeId.Player))
                    limittime = questGiver.ToPlayer().m_QuestStatus[questId].Timer;

                AddTimedQuest(questId);
                questStatusData.Timer = limittime;
                endTime = LoopTime.ServerTime + limittime;
            }
            else
                questStatusData.Timer = TimeSpan.Zero;

            if (quest.HasAnyFlag(QuestFlags.Pvp))
            {
                pvpInfo.IsHostile = true;
                UpdatePvPState();
            }

            if (quest.SourceSpellID > 0)
            {
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(quest.SourceSpellID, GetMap().GetDifficultyID());
                Unit caster = this;
                if (questGiver != null && questGiver.IsUnit() && !quest.HasFlag(QuestFlags.PlayerCastAccept) && !spellInfo.HasTargetType(Targets.UnitCaster) && !spellInfo.HasTargetType(Targets.DestCasterSummon))
                    caster = questGiver.ToUnit();

                caster.CastSpell(this, spellInfo.Id, new CastSpellExtraArgs(TriggerCastFlags.FullMask).SetCastDifficulty(spellInfo.Difficulty));
            }

            SetQuestSlotEndTime(logSlot, endTime);
            questStatusData.AcceptTime = LoopTime.ServerTime;

            m_QuestStatusSave[questId] = QuestSaveType.Default;

            StartCriteria(CriteriaStartEvent.AcceptQuest, questId);

            SendQuestUpdate(questId);

            Global.ScriptMgr.OnQuestStatusChange(this, questId);
            Global.ScriptMgr.OnQuestStatusChange(this, quest, oldStatus, questStatusData.Status);
        }

        public void CompleteQuest(int quest_id)
        {
            if (quest_id != 0)
            {
                SetQuestStatus(quest_id, QuestStatus.Complete);

                QuestStatusData questStatus = m_QuestStatus.LookupByKey(quest_id);
                if (questStatus != null)
                    SetQuestSlotState(questStatus.Slot, QuestSlotStateMask.Complete);

                Quest qInfo = Global.ObjectMgr.GetQuestTemplate(quest_id);
                if (qInfo != null)
                {
                    if (qInfo.HasAnyFlag(QuestFlags.TrackingEvent))
                        RewardQuest(qInfo, LootItemType.Item, 0, this, false);
                }
            }
        }

        public void IncompleteQuest(int quest_id)
        {
            if (quest_id != 0)
            {
                SetQuestStatus(quest_id, QuestStatus.Incomplete);

                ushort log_slot = FindQuestSlot(quest_id);
                if (log_slot < SharedConst.MaxQuestLogSize)
                    RemoveQuestSlotState(log_slot, QuestSlotStateMask.Complete);
            }
        }

        public int GetQuestMoneyReward(Quest quest)
        {
            return (int)(quest.MoneyValue(this) * WorldConfig.Values[WorldCfg.RateMoneyQuest].Float);
        }

        public int GetQuestXPReward(Quest quest)
        {
            bool rewarded = IsQuestRewarded(quest.Id) && !quest.IsDFQuest();

            // Not give XP in case already completed once repeatable quest
            if (rewarded)
                return 0;

            var XP = (int)(quest.XPValue(this) * WorldConfig.Values[WorldCfg.RateXpQuest].Float);

            // handle SPELL_AURA_MOD_XP_QUEST_PCT auras
            var ModXPPctAuras = GetAuraEffectsByType(AuraType.ModXpQuestPct);
            foreach (var eff in ModXPPctAuras)
                MathFunctions.AddPct(ref XP, eff.GetAmount());

            return XP;
        }

        public bool CanSelectQuestPackageItem(QuestPackageItemRecord questPackageItem)
        {
            ItemTemplate rewardProto = Global.ObjectMgr.GetItemTemplate(questPackageItem.ItemID);
            if (rewardProto == null)
                return false;

            if ((rewardProto.HasFlag(ItemFlags2.FactionAlliance) && GetTeam() != Team.Alliance) ||
                (rewardProto.HasFlag(ItemFlags2.FactionHorde) && GetTeam() != Team.Horde))
                return false;

            switch (questPackageItem.DisplayType)
            {
                case QuestPackageFilter.LootSpecialization:
                    return rewardProto.IsUsableByLootSpecialization(this, true);
                case QuestPackageFilter.Class:
                    return rewardProto.ItemSpecClassMask == 0 || rewardProto.ItemSpecClassMask.HasClass(GetClass());
                case QuestPackageFilter.Everyone:
                    return true;
                default:
                    break;
            }

            return false;
        }

        public void RewardQuestPackage(int questPackageId, ItemContext context, int onlyItemId = 0)
        {
            bool hasFilteredQuestPackageReward = false;
            var questPackageItems = Global.DB2Mgr.GetQuestPackageItems(questPackageId);
            if (questPackageItems != null)
            {
                foreach (var questPackageItem in questPackageItems)
                {
                    if (onlyItemId != 0 && questPackageItem.ItemID != onlyItemId)
                        continue;

                    if (CanSelectQuestPackageItem(questPackageItem))
                    {
                        hasFilteredQuestPackageReward = true;
                        if (CanStoreNewItem(ItemPos.Undefined, out var dest, questPackageItem.ItemID, questPackageItem.ItemQuantity) == InventoryResult.Ok)
                        {
                            var item = StoreNewItem(dest, questPackageItem.ItemID, true, ItemEnchantmentManager.GenerateRandomProperties(questPackageItem.ItemID));
                            SendNewItem(item, questPackageItem.ItemQuantity, true, false);
                        }
                    }
                }
            }

            if (!hasFilteredQuestPackageReward)
            {
                var questPackageItemsFallback = Global.DB2Mgr.GetQuestPackageItemsFallback(questPackageId);
                if (questPackageItemsFallback != null)
                {
                    foreach (var questPackageItem in questPackageItemsFallback)
                    {
                        if (onlyItemId != 0 && questPackageItem.ItemID != onlyItemId)
                            continue;

                        if (CanStoreNewItem(ItemPos.Undefined, out var dest, questPackageItem.ItemID, questPackageItem.ItemQuantity) == InventoryResult.Ok)
                        {
                            var item = StoreNewItem(dest, questPackageItem.ItemID, true, ItemEnchantmentManager.GenerateRandomProperties(questPackageItem.ItemID));
                            SendNewItem(item, questPackageItem.ItemQuantity, true, false);
                        }
                    }                    
                }
            }
        }

        public void RewardQuest(Quest quest, LootItemType rewardType, int rewardId, WorldObject questGiver, bool announce = true)
        {
            //this THING should be here to protect code from quest, which cast on player far teleport as a reward
            //should work fine, cause far teleport will be executed in Update()
            SetCanDelayTeleport(true);

            var questId = quest.Id;
            var oldStatus = GetQuestStatus(questId);

            foreach (var obj in quest.Objectives)
            {
                switch (obj.Type)
                {
                    case QuestObjectiveType.Item:
                    {
                        int amountToDestroy = obj.Amount;
                        if (quest.HasAnyFlag(QuestFlags.RemoveSurplusItems))
                            amountToDestroy = int.MaxValue;
                        DestroyItemCount(obj.ObjectID, amountToDestroy, true);
                        break;
                    }
                    case QuestObjectiveType.Currency:
                        RemoveCurrency(obj.ObjectID, obj.Amount, CurrencyDestroyReason.QuestTurnin);
                        break;
                }
            }

            if (!quest.HasAnyFlag(QuestFlagsEx.NoItemRemoval))
            {
                for (byte i = 0; i < SharedConst.QuestItemDropCount; ++i)
                {
                    if (quest.ItemDrop[i] != 0)
                    {
                        int count = quest.ItemDropQuantity[i];
                        if (count == 0)
                            count = int.MaxValue;
                        DestroyItemCount(quest.ItemDrop[i], count, true);
                    }
                }
            }

            RemoveTimedQuest(questId);

            if (quest.GetRewItemsCount() > 0)
            {
                for (int i = 0; i < quest.GetRewItemsCount(); ++i)
                {
                    var itemId = quest.RewardItemId[i];
                    if (itemId != 0)
                    {
                        if (CanStoreNewItem(ItemPos.Undefined, out var dest, itemId, quest.RewardItemCount[i]) == InventoryResult.Ok)
                        {
                            Item item = StoreNewItem(dest, itemId, true, ItemEnchantmentManager.GenerateRandomProperties(itemId), null, ItemContext.QuestReward);
                            SendNewItem(item, quest.RewardItemCount[i], true, false);
                        }
                        else if (quest.IsDFQuest())
                            SendItemRetrievalMail(itemId, quest.RewardItemCount[i], ItemContext.QuestReward);
                    }
                }
            }

            var currencyGainSource = CurrencyGainSource.QuestReward;

            if (quest.HasAnyFlag(QuestFlagsEx.RewardsIgnoreCaps))
            {
                if (quest.IsWorldQuest())
                    currencyGainSource = CurrencyGainSource.WorldQuestRewardIgnoreCaps;
                else
                    currencyGainSource = CurrencyGainSource.QuestRewardIgnoreCaps;
            }
            else if (quest.IsDaily())
                currencyGainSource = CurrencyGainSource.DailyQuestReward;
            else if (quest.IsWeekly())
                currencyGainSource = CurrencyGainSource.WeeklyQuestReward;
            else if (quest.IsWorldQuest())
                currencyGainSource = CurrencyGainSource.WorldQuestReward;

            switch (rewardType)
            {
                case LootItemType.Item:
                    var rewardProto = Global.ObjectMgr.GetItemTemplate(rewardId);
                    if (rewardProto != null && quest.GetRewChoiceItemsCount() != 0)
                    {
                        for (int i = 0; i < SharedConst.QuestRewardChoicesCount; ++i)
                        {
                            if (quest.RewardChoiceItemId[i] != 0 && quest.RewardChoiceItemType[i] == LootItemType.Item && quest.RewardChoiceItemId[i] == rewardId)
                            {
                                if (CanStoreNewItem(ItemPos.Undefined, out var dest, rewardId, quest.RewardChoiceItemCount[i]) == InventoryResult.Ok)
                                {
                                    var item = StoreNewItem(dest, rewardId, true, ItemEnchantmentManager.GenerateRandomProperties(rewardId), null, ItemContext.QuestReward);
                                    SendNewItem(item, quest.RewardChoiceItemCount[i], true, false);
                                }
                            }
                        }
                    }

                    // QuestPackageItem.db2
                    if (rewardProto != null && quest.PackageID != 0)
                        RewardQuestPackage(quest.PackageID, ItemContext.QuestReward, rewardId);
                    break;
                case LootItemType.Currency:
                    if (CliDB.CurrencyTypesStorage.HasRecord(rewardId) && quest.GetRewChoiceItemsCount() != 0)
                    {
                        for (int i = 0; i < SharedConst.QuestRewardChoicesCount; ++i)
                        {
                            if (quest.RewardChoiceItemId[i] != 0 && quest.RewardChoiceItemType[i] == LootItemType.Currency && quest.RewardChoiceItemId[i] == rewardId)
                                AddCurrency(quest.RewardChoiceItemId[i], quest.RewardChoiceItemCount[i], currencyGainSource);
                        }
                    }

                    break;
            }

            for (byte i = 0; i < SharedConst.QuestRewardCurrencyCount; ++i)
            {
                if (quest.RewardCurrencyId[i] != 0)
                    AddCurrency(quest.RewardCurrencyId[i], quest.RewardCurrencyCount[i], currencyGainSource);
            }

            var skill = quest.RewardSkillId;
            if (skill != 0)
                UpdateSkillPro(skill, 1000, quest.RewardSkillPoints);

            var log_slot = FindQuestSlot(questId);
            if (log_slot < SharedConst.MaxQuestLogSize)
                SetQuestSlot(log_slot, 0);

            var XP = GetQuestXPReward(quest);

            int moneyRew = 0;
            if (!IsMaxLevel())
                GiveXP(XP, null);
            else
                moneyRew = (int)(quest.GetRewMoneyMaxLevel() * WorldConfig.Values[WorldCfg.RateDropMoney].Float);

            moneyRew += GetQuestMoneyReward(quest);

            if (moneyRew != 0)
            {
                ModifyMoney(moneyRew);

                if (moneyRew > 0)
                    UpdateCriteria(CriteriaType.MoneyEarnedFromQuesting, moneyRew);

                SendDisplayToast(0, DisplayToastType.Money, false, moneyRew, DisplayToastMethod.QuestComplete, questId);
            }

            // honor reward
            var honor = quest.CalculateHonorGain(GetLevel());
            if (honor != 0)
                RewardHonor(null, 0, honor);

            // title reward
            if (quest.RewardTitleId != 0)
            {
                var titleEntry = CliDB.CharTitlesStorage.LookupByKey(quest.RewardTitleId);
                if (titleEntry != null)
                    SetTitle(titleEntry);
            }

            if (quest.RewardSkillPoints != 0)
            {
                m_questRewardedTalentPoints += quest.RewardSkillPoints;
                InitTalentForLevel();
            }

            // Send reward mail
            var mail_template_id = quest.RewardMailTemplateId;
            if (mail_template_id != 0)
            {
                SQLTransaction trans = new();
                // @todo Poor design of mail system
                var questMailSender = quest.RewardMailSenderEntry;
                if (questMailSender != 0)
                    new MailDraft(mail_template_id).SendMailTo(trans, this, new MailSender(questMailSender), MailCheckFlags.HasBody, quest.RewardMailDelay);
                else
                    new MailDraft(mail_template_id).SendMailTo(trans, this, new MailSender(questGiver), MailCheckFlags.HasBody, quest.RewardMailDelay);
                DB.Characters.CommitTransaction(trans);
            }

            if (quest.IsDaily() || quest.IsDFQuest())
            {
                SetDailyQuestStatus(questId);
                if (quest.IsDaily())
                {
                    StartCriteria(CriteriaStartEvent.CompleteDailyQuest, 0);
                    UpdateCriteria(CriteriaType.CompleteDailyQuest, questId);
                    UpdateCriteria(CriteriaType.CompleteAnyDailyQuestPerDay, questId);
                }
            }
            else if (quest.IsWeekly())
                SetWeeklyQuestStatus(questId);
            else if (quest.IsMonthly())
                SetMonthlyQuestStatus(questId);
            else if (quest.IsSeasonal())
                SetSeasonalQuestStatus(questId);

            RemoveActiveQuest(questId, false);
            if (quest.CanIncreaseRewardedQuestCounters())
                SetRewardedQuest(questId);

            SendQuestReward(quest, questGiver?.ToCreature(), XP, !announce);

            RewardReputation(quest);

            // cast spells after mark quest complete (some spells have quest completed state requirements in spell_area data)
            if (quest.RewardSpell > 0)
            {
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(quest.RewardSpell, GetMap().GetDifficultyID());
                Unit caster = this;
                if (questGiver != null && questGiver.IsUnit() && !quest.HasFlag(QuestFlags.PlayerCastComplete) && !spellInfo.HasTargetType(Targets.UnitCaster))
                    caster = questGiver.ToUnit();

                caster.CastSpell(this, spellInfo.Id, new CastSpellExtraArgs(TriggerCastFlags.FullMask).SetCastDifficulty(spellInfo.Difficulty));
            }
            else
            {
                for (int i = 0; i < SharedConst.QuestRewardDisplaySpellCount; ++i)
                {
                    if (quest.RewardDisplaySpell[i] == 0)
                        continue;

                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(quest.RewardDisplaySpell[i], GetMap().GetDifficultyID());
                    Unit caster = this;
                    if (questGiver != null && questGiver.IsUnit() && !quest.HasFlag(QuestFlags.PlayerCastComplete) && !spellInfo.HasTargetType(Targets.UnitCaster))
                        caster = questGiver.ToUnit();

                    caster.CastSpell(this, spellInfo.Id, new CastSpellExtraArgs(TriggerCastFlags.FullMask).SetCastDifficulty(spellInfo.Difficulty));
                }
            }

            if (quest.QuestSortID > 0)
                UpdateCriteria(CriteriaType.CompleteQuestsInZone, quest.Id);

            UpdateCriteria(CriteriaType.CompleteQuestsCount);
            UpdateCriteria(CriteriaType.CompleteQuest, quest.Id);
            UpdateCriteria(CriteriaType.CompleteAnyReplayQuest, 1);

            // make full db save
            SaveToDB(false);

            var questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(questId);
            if (questBit != 0)
                SetQuestCompletedBit(questBit, true);

            if (quest.HasAnyFlag(QuestFlags.Pvp))
            {
                pvpInfo.IsHostile = pvpInfo.IsInHostileArea || HasPvPForcingQuest();
                UpdatePvPState();
            }

            SendQuestUpdate(questId, true, true);

            bool updateVisibility = false;
            if (quest.HasAnyFlag(QuestFlags.UpdatePhaseshift))
                updateVisibility = PhasingHandler.OnConditionChange(this, false);

            //lets remove flag for delayed teleports
            SetCanDelayTeleport(false);

            if (questGiver != null && questGiver.IsWorldObject())
            {
                //For AutoSubmition was added plr case there as it almost same exclute AI script cases.
                // Send next quest
                Quest nextQuest = GetNextQuest(questGiver, quest);
                if (nextQuest != null)
                {
                    // Only send the quest to the player if the conditions are met
                    if (CanTakeQuest(nextQuest, false))
                    {
                        if (nextQuest.IsAutoAccept() && CanAddQuest(nextQuest, true))
                            AddQuestAndCheckCompletion(nextQuest, questGiver);

                        PlayerTalkClass.SendQuestGiverQuestDetails(nextQuest, questGiver.GetGUID(), true, false);
                    }
                }

                PlayerTalkClass.ClearMenus();
                questGiver.ToCreature()?.GetAI().OnQuestReward(this, quest, rewardType, rewardId);
                questGiver.ToGameObject()?.GetAI().OnQuestReward(this, quest, rewardType, rewardId);
            }

            Global.ScriptMgr.OnQuestStatusChange(this, questId);
            Global.ScriptMgr.OnQuestStatusChange(this, quest, oldStatus, QuestStatus.Rewarded);

            if (updateVisibility)
                UpdateObjectVisibility();
        }

        public void SetRewardedQuest(int questId)
        {
            m_RewardedQuests.Add(questId);
            m_RewardedQuestsSave[questId] = QuestSaveType.Default;

            var questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(questId);
            if (questBit != 0)
                SetQuestCompletedBit(questBit, true);
        }

        public void FailQuest(int questId)
        {
            Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
            if (quest != null)
            {
                QuestStatus qStatus = GetQuestStatus(questId);

                // we can only fail incomplete quest or...
                if (qStatus != QuestStatus.Incomplete)
                {
                    // completed timed quest with no requirements
                    if (qStatus != QuestStatus.Complete || quest.LimitTime == TimeSpan.Zero || !quest.Objectives.Empty())
                        return;
                }

                SetQuestStatus(questId, QuestStatus.Failed);

                ushort log_slot = FindQuestSlot(questId);

                if (log_slot < SharedConst.MaxQuestLogSize)
                    SetQuestSlotState(log_slot, QuestSlotStateMask.Fail);

                if (quest.LimitTime != TimeSpan.Zero)
                {
                    QuestStatusData q_status = m_QuestStatus[questId];

                    RemoveTimedQuest(questId);
                    q_status.Timer = Milliseconds.Zero;

                    SendQuestTimerFailed(questId);
                }
                else
                    SendQuestFailed(questId);

                // Destroy quest items on quest failure.
                foreach (QuestObjective obj in quest.Objectives)
                {
                    if (obj.Type == QuestObjectiveType.Item)
                    {
                        ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(obj.ObjectID);
                        if (itemTemplate != null)
                        {
                            if (itemTemplate.GetBonding() == ItemBondingType.Quest)
                                DestroyItemCount(obj.ObjectID, obj.Amount, true, true);
                        }
                    }
                }

                // Destroy items received during the quest.
                for (byte i = 0; i < SharedConst.QuestItemDropCount; ++i)
                {
                    ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(quest.ItemDrop[i]);
                    if (itemTemplate != null)
                    {
                        if (quest.ItemDropQuantity[i] != 0 && itemTemplate.GetBonding() == ItemBondingType.Quest)
                            DestroyItemCount(quest.ItemDrop[i], quest.ItemDropQuantity[i], true, true);
                    }
                }
            }
        }

        public void FailQuestsWithFlag(QuestFlags flag)
        {
            for (ushort slot = 0; slot < SharedConst.MaxQuestLogSize; ++slot)
            {
                var questId = GetQuestSlotQuestId(slot);
                if (questId == 0)
                    continue;

                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest != null)
                    if (quest.HasAnyFlag(flag))
                        FailQuest(questId);
            }
        }
        
        public void AbandonQuest(int questId)
        {
            Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
            if (quest != null)
            {
                // Destroy quest items on quest abandon.
                foreach (QuestObjective obj in quest.Objectives)
                {
                    if (obj.Type == QuestObjectiveType.Item)
                    {
                        ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(obj.ObjectID);
                        if (itemTemplate != null)
                        {
                            if (itemTemplate.GetBonding() == ItemBondingType.Quest)
                                DestroyItemCount(obj.ObjectID, obj.Amount, true, true);
                        }
                    }
                }

                // Destroy items received during the quest.
                for (byte i = 0; i < SharedConst.QuestItemDropCount; ++i)
                {
                    ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(quest.ItemDrop[i]);
                    if (itemTemplate != null)
                    {
                        if (quest.ItemDropQuantity[i] != 0 && itemTemplate.GetBonding() == ItemBondingType.Quest)
                            DestroyItemCount(quest.ItemDrop[i], quest.ItemDropQuantity[i], true, true);
                    }
                }
            }
        }

        public bool SatisfyQuestSkill(Quest qInfo, bool msg)
        {
            var skill = qInfo.RequiredSkillId;

            // skip 0 case RequiredSkill
            if (skill == 0)
                return true;

            // check skill value
            if (GetSkillValue(skill) < qInfo.RequiredSkillPoints)
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.None);
                    Log.outDebug(LogFilter.Server, 
                        $"SatisfyQuestSkill: Sent QuestFailedReason.None (questId: {qInfo.Id}) " +
                        $"because player does not have required skill value.");
                }

                return false;
            }

            return true;
        }

        bool SatisfyQuestLevel(Quest qInfo, bool msg)
        {
            return SatisfyQuestMinLevel(qInfo, msg) && SatisfyQuestMaxLevel(qInfo, msg);
        }

        public bool SatisfyQuestMinLevel(Quest qInfo, bool msg)
        {
            if (qInfo.MinLevel > 0 && GetLevel() < qInfo.MinLevel)
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.FailedLowLevel);
                    Log.outDebug(LogFilter.Server, 
                        $"SatisfyQuestMinLevel: Sent QuestFailedReasons.FailedLowLevel (questId: {qInfo.Id}) " +
                        $"because player does not have required (min) level.");
                }
                return false;
            }
            return true;
        }

        public bool SatisfyQuestMaxLevel(Quest qInfo, bool msg)
        {
            if (qInfo.MaxLevel > 0 && GetLevel() > qInfo.MaxLevel)
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.None); // There doesn't seem to be a specific response for too high player level
                    Log.outDebug(LogFilter.Server, "" +
                        $"SatisfyQuestMaxLevel: Sent QuestFailedReasons.None (questId: {qInfo.Id}) " +
                        "because player does not have required (max) level.");
                }
                return false;
            }
            return true;
        }

        public bool SatisfyQuestLog(bool msg)
        {
            // exist free slot
            if (FindQuestSlot(0) < SharedConst.MaxQuestLogSize)
                return true;

            if (msg)
                SendPacket(new QuestLogFull());

            return false;
        }

        public bool SatisfyQuestDependentQuests(Quest qInfo, bool msg)
        {
            return SatisfyQuestPreviousQuest(qInfo, msg) && SatisfyQuestDependentPreviousQuests(qInfo, msg) &&
                SatisfyQuestBreadcrumbQuest(qInfo, msg) && SatisfyQuestDependentBreadcrumbQuests(qInfo, msg);
        }

        public bool SatisfyQuestPreviousQuest(Quest qInfo, bool msg)
        {
            // No previous quest (might be first quest in a series)
            if (qInfo.PrevQuestId == 0)
                return true;

            var prevId = Math.Abs(qInfo.PrevQuestId);
            // If positive previous quest rewarded, return true
            if (qInfo.PrevQuestId > 0 && m_RewardedQuests.Contains(prevId))
                return true;

            // If negative previous quest active, return true
            if (qInfo.PrevQuestId < 0 && GetQuestStatus(prevId) == QuestStatus.Incomplete)
                return true;

            // Has positive prev. quest in non-rewarded state
            // and negative prev. quest in non-active state
            if (msg)
            {
                SendCanTakeQuestResponse(QuestFailedReasons.None);
                Log.outDebug(LogFilter.Misc, 
                    $"Player.SatisfyQuestPreviousQuest: Sent QUEST_ERR_NONE (QuestID: {qInfo.Id}) " +
                    $"because player '{GetName()}' ({GetGUID()}) doesn't have required quest {prevId}.");
            }

            return false;
        }

        bool SatisfyQuestDependentPreviousQuests(Quest qInfo, bool msg)
        {
            // No previous quest (might be first quest in a series)
            if (qInfo.DependentPreviousQuests.Empty())
                return true;

            foreach (var prevId in qInfo.DependentPreviousQuests)
            {
                // checked in startup
                Quest questInfo = Global.ObjectMgr.GetQuestTemplate(prevId);

                // If any of the previous quests completed, return true
                if (IsQuestRewarded(prevId))
                {
                    // skip one-from-all exclusive group
                    if (questInfo.ExclusiveGroup >= 0)
                        return true;

                    // each-from-all exclusive group (< 0)
                    // can be start if only all quests in prev quest exclusive group completed and rewarded
                    var bounds = Global.ObjectMgr.GetExclusiveQuestGroupBounds(questInfo.ExclusiveGroup);
                    foreach (var exclusiveQuestId in bounds)
                    {
                        // skip checked quest id, only state of other quests in group is interesting
                        if (exclusiveQuestId == prevId)
                            continue;

                        // alternative quest from group also must be completed and rewarded (reported)
                        if (!IsQuestRewarded(exclusiveQuestId))
                        {
                            if (msg)
                            {
                                SendCanTakeQuestResponse(QuestFailedReasons.None);
                                Log.outDebug(LogFilter.Misc, 
                                    $"Player.SatisfyQuestDependentPreviousQuests: " +
                                    $"Sent QUEST_ERR_NONE (QuestID: {qInfo.Id}) " +
                                    $"because player '{GetName()}' ({GetGUID()}) " +
                                    $"doesn't have the required quest (1).");
                            }

                            return false;
                        }
                    }

                    return true;
                }
            }

            // Has only prev. quests in non-rewarded state
            if (msg)
            {
                SendCanTakeQuestResponse(QuestFailedReasons.None);
                Log.outDebug(LogFilter.Misc, 
                    $"Player.SatisfyQuestDependentPreviousQuests: Sent QUEST_ERR_NONE " +
                    $"(QuestID: {qInfo.Id}) because player '{GetName()}' ({GetGUID()}) " +
                    $"doesn't have required quest (2).");
            }

            return false;
        }

        bool SatisfyQuestBreadcrumbQuest(Quest qInfo, bool msg)
        {
            var breadcrumbTargetQuestId = Math.Abs(qInfo.BreadcrumbForQuestId);

            //If this is not a breadcrumb quest.
            if (breadcrumbTargetQuestId == 0)
                return true;

            // If the target quest is not available
            if (!CanTakeQuest(Global.ObjectMgr.GetQuestTemplate(breadcrumbTargetQuestId), false))
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.None);
                    Log.outDebug(LogFilter.Misc, 
                        $"Player.SatisfyQuestBreadcrumbQuest: Sent INVALIDREASON_DONT_HAVE_REQ " +
                        $"(QuestID: {qInfo.Id}) because target quest (QuestID: {breadcrumbTargetQuestId}) " +
                        $"is not available to player '{GetName()}' ({GetGUID()}).");
                }

                return false;
            }

            return true;
        }

        bool SatisfyQuestDependentBreadcrumbQuests(Quest qInfo, bool msg)
        {
            foreach (var breadcrumbQuestId in qInfo.DependentBreadcrumbQuests)
            {
                QuestStatus status = GetQuestStatus(breadcrumbQuestId);
                // If any of the breadcrumb quests are in the quest log, return false.
                if (status == QuestStatus.Incomplete || status == QuestStatus.Complete || status == QuestStatus.Failed)
                {
                    if (msg)
                    {
                        SendCanTakeQuestResponse(QuestFailedReasons.None);
                        Log.outDebug(LogFilter.Misc, 
                            $"Player.SatisfyQuestDependentBreadcrumbQuests: " +
                            $"Sent INVALIDREASON_DONT_HAVE_REQ (QuestID: {qInfo.Id}) " +
                            $"because player '{GetName()}' ({GetGUID()}) has a breadcrumb quest " +
                            $"towards this quest in the quest log.");
                    }

                    return false;
                }
            }
            return true;
        }

        public bool SatisfyQuestClass(Quest qInfo, bool msg)
        {
            var reqClass = qInfo.AllowableClasses;

            if (reqClass == 0)
                return true;

            if (reqClass.HasClass(GetClass()))
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.None);
                    Log.outDebug(LogFilter.Server, 
                        $"SatisfyQuestClass: Sent QuestFailedReason.None (questId: {qInfo.Id}) " +
                        $"because player does not have required class.");
                }

                return false;
            }

            return true;
        }

        public bool SatisfyQuestRace(Quest qInfo, bool msg)
        {
            if (!qInfo.AllowableRaces.HasRace(GetRace()))
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.FailedWrongRace);
                    Log.outDebug(LogFilter.Server, 
                        $"SatisfyQuestRace: Sent QuestFailedReasons.FailedWrongRace (questId: {qInfo.Id}) " +
                        $"because player '{GetName()}' ({GetGUID()}) does not have required race.");
                }
                return false;
            }
            return true;
        }

        public bool SatisfyQuestReputation(Quest qInfo, bool msg)
        {
            var fIdMin = qInfo.RequiredMinRepFaction;      //Min required rep
            if (fIdMin != 0 && GetReputationMgr().GetReputation(fIdMin) < qInfo.RequiredMinRepValue)
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.None);
                    Log.outDebug(LogFilter.Server, 
                        $"SatisfyQuestReputation: Sent QuestFailedReason.None (questId: {qInfo.Id}) " +
                        $"because player does not have required reputation (min).");
                }
                return false;
            }

            var fIdMax = qInfo.RequiredMaxRepFaction;      //Max required rep
            if (fIdMax != 0 && GetReputationMgr().GetReputation(fIdMax) >= qInfo.RequiredMaxRepValue)
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.None);
                    Log.outDebug(LogFilter.Server, 
                        $"SatisfyQuestReputation: Sent QuestFailedReason.None (questId: {qInfo.Id}) " +
                        $"because player does not have required reputation (max).");
                }
                return false;
            }

            return true;
        }

        public bool SatisfyQuestStatus(Quest qInfo, bool msg)
        {
            if (GetQuestStatus(qInfo.Id) == QuestStatus.Rewarded)
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.AlreadyDone);
                    Log.outDebug(LogFilter.Misc, 
                        $"Player.SatisfyQuestStatus: Sent QUEST_STATUS_REWARDED (QuestID: {qInfo.Id}) " +
                        $"because player '{GetName()}' ({GetGUID()}) quest status is already REWARDED.");
                }
                return false;
            }

            if (GetQuestStatus(qInfo.Id) != QuestStatus.None)
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.AlreadyOn1);
                    Log.outDebug(LogFilter.Server, 
                        $"SatisfyQuestStatus: Sent QuestFailedReasons.AlreadyOn1 (questId: {qInfo.Id}) " +
                        $"because player quest status is not NONE.");
                }
                return false;
            }
            return true;
        }

        public bool SatisfyQuestConditions(Quest qInfo, bool msg)
        {
            if (!Global.ConditionMgr.IsObjectMeetingNotGroupedConditions(ConditionSourceType.QuestAvailable, qInfo.Id, this))
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.None);
                    Log.outDebug(LogFilter.Server, 
                        $"SatisfyQuestConditions: Sent QuestFailedReason.None (questId: {qInfo.Id}) " +
                        $"because player does not meet conditions.");
                }

                Log.outDebug(LogFilter.Condition, 
                    $"SatisfyQuestConditions: conditions not met for quest {qInfo.Id}");

                return false;
            }
            return true;
        }

        public bool SatisfyQuestTimed(Quest qInfo, bool msg)
        {
            if (!m_timedquests.Empty() && qInfo.LimitTime != TimeSpan.Zero)
            {
                if (msg)
                {
                    SendCanTakeQuestResponse(QuestFailedReasons.OnlyOneTimed);
                    Log.outDebug(LogFilter.Server, 
                        $"SatisfyQuestTimed: Sent QuestFailedReasons.OnlyOneTimed (questId: {qInfo.Id}) " +
                        $"because player is already on a timed quest.");
                }
                return false;
            }
            return true;
        }

        public bool SatisfyQuestExclusiveGroup(Quest qInfo, bool msg)
        {
            // non positive exclusive group, if > 0 then can be start if any other quest in exclusive group already started/completed
            if (qInfo.ExclusiveGroup <= 0)
                return true;

            var range = Global.ObjectMgr.GetExclusiveQuestGroupBounds(qInfo.ExclusiveGroup);
            // always must be found if qInfo.ExclusiveGroup != 0

            foreach (var exclude_Id in range)
            {
                // skip checked quest id, only state of other quests in group is interesting
                if (exclude_Id == qInfo.Id)
                    continue;

                // not allow have daily quest if daily quest from exclusive group already recently completed
                Quest Nquest = Global.ObjectMgr.GetQuestTemplate(exclude_Id);
                if (!SatisfyQuestDay(Nquest, false) || !SatisfyQuestWeek(Nquest, false) || !SatisfyQuestSeasonal(Nquest, false))
                {
                    if (msg)
                    {
                        SendCanTakeQuestResponse(QuestFailedReasons.None);
                        Log.outDebug(LogFilter.Server, 
                            $"SatisfyQuestExclusiveGroup: Sent QuestFailedReason.None (questId: {qInfo.Id}) " +
                            $"because player already did daily quests in exclusive group.");
                    }

                    return false;
                }

                // alternative quest already started or completed - but don't check rewarded states if both are repeatable
                if (GetQuestStatus(exclude_Id) != QuestStatus.None 
                    || (!(qInfo.IsRepeatable() && Nquest.IsRepeatable()) && GetQuestRewardStatus(exclude_Id)))
                {
                    if (msg)
                    {
                        SendCanTakeQuestResponse(QuestFailedReasons.None);
                        Log.outDebug(LogFilter.Server, 
                            $"SatisfyQuestExclusiveGroup: Sent QuestFailedReason.None (questId: {qInfo.Id}) " +
                            $"because player already did quest in exclusive group.");
                    }
                    return false;
                }
            }
            return true;
        }

        public bool SatisfyQuestDay(Quest qInfo, bool msg)
        {
            if (!qInfo.IsDaily() && !qInfo.IsDFQuest())
                return true;

            if (qInfo.IsDFQuest())
            {
                if (m_DFQuests.Contains(qInfo.Id))
                    return false;

                return true;
            }

            return m_activePlayerData.DailyQuestsCompleted.FindIndex(qInfo.Id) == -1;
        }

        public bool SatisfyQuestWeek(Quest qInfo, bool msg)
        {
            if (!qInfo.IsWeekly() || m_weeklyquests.Empty())
                return true;

            // if not found in cooldown list
            return !m_weeklyquests.Contains(qInfo.Id);
        }

        public bool SatisfyQuestSeasonal(Quest qInfo, bool msg)
        {
            if (!qInfo.IsSeasonal() || m_seasonalquests.Empty())
                return true;

            var list = m_seasonalquests.LookupByKey(qInfo.GetEventIdForQuest());
            if (list == null || list.Empty())
                return true;

            // if not found in cooldown list
            return !list.ContainsKey(qInfo.Id);
        }

        public bool SatisfyQuestExpansion(Quest qInfo, bool msg)
        {
            if ((int)GetSession().GetExpansion() < qInfo.Expansion)
            {
                if (msg)
                    SendCanTakeQuestResponse(QuestFailedReasons.FailedExpansion);

                Log.outDebug(LogFilter.Misc, 
                    $"Player.SatisfyQuestExpansion: Sent QUEST_ERR_FAILED_EXPANSION " +
                    $"(QuestID: {qInfo.Id}) because player '{GetName()}' ({GetGUID()}) " +
                    $"does not have required expansion.");
                return false;
            }
            return true;
        }

        public bool SatisfyQuestMonth(Quest qInfo, bool msg)
        {
            if (!qInfo.IsMonthly() || m_monthlyquests.Empty())
                return true;

            // if not found in cooldown list
            return !m_monthlyquests.Contains(qInfo.Id);
        }

        public bool GiveQuestSourceItem(Quest quest)
        {
            var srcitem = quest.SourceItemId;
            if (srcitem > 0)
            {
                // Don't give source item if it is the same item used to start the quest
                ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(srcitem);
                if (quest.Id == itemTemplate.GetStartQuest())
                    return true;

                var count = quest.SourceItemIdCount;
                if (count <= 0)
                    count = 1;

                InventoryResult msg = CanStoreNewItem(ItemPos.Undefined, out var dest, itemTemplate, count, out _);
                if (msg == InventoryResult.Ok)
                {
                    Item item = StoreNewItem(dest, srcitem, true);
                    SendNewItem(item, count, true, false);
                    return true;
                }
                // player already have max amount required item, just report success
                if (msg == InventoryResult.ItemMaxCount)
                    return true;

                SendEquipError(msg, null, null, srcitem);
                return false;
            }

            return true;
        }

        public bool TakeQuestSourceItem(int questId, bool msg)
        {
            Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
            if (quest != null)
            {
                var srcItemId = quest.SourceItemId;
                ItemTemplate item = Global.ObjectMgr.GetItemTemplate(srcItemId);

                if (srcItemId > 0)
                {
                    var count = quest.SourceItemIdCount;
                    if (count <= 0)
                        count = 1;

                    // There are two cases where the source item is not destroyed:
                    // - Item cannot be unequipped (example: non-empty bags)
                    // - The source item is the item that started the quest,
                    // so the player is supposed to keep it
                    // (otherwise it was already destroyed in AddQuestAndCheckCompletion())
                    InventoryResult res = CanUnequipItems(srcItemId, count);
                    if (res != InventoryResult.Ok)
                    {
                        if (msg)
                            SendEquipError(res, null, null, srcItemId);
                        return false;
                    }

                    if (item.GetStartQuest() != questId)
                        DestroyItemCount(srcItemId, count, true, true);
                }
            }

            return true;
        }

        public bool GetQuestRewardStatus(int quest_id)
        {
            Quest qInfo = Global.ObjectMgr.GetQuestTemplate(quest_id);
            if (qInfo != null)
            {
                if (qInfo.IsSeasonal() && !qInfo.IsRepeatable())
                    return !SatisfyQuestSeasonal(qInfo, false);

                // for repeatable quests: rewarded field is set after first reward only to prevent getting XP more than once
                if (!qInfo.IsRepeatable())
                    return IsQuestRewarded(quest_id);

                return false;
            }
            return false;
        }

        public QuestStatus GetQuestStatus(int questId)
        {
            if (questId != 0)
            {
                var questStatusData = m_QuestStatus.LookupByKey(questId);
                if (questStatusData != null)
                    return questStatusData.Status;

                if (GetQuestRewardStatus(questId))
                    return QuestStatus.Rewarded;
            }
            return QuestStatus.None;
        }

        public bool CanShareQuest(int quest_id)
        {
            Quest qInfo = Global.ObjectMgr.GetQuestTemplate(quest_id);
            if (qInfo != null && qInfo.HasAnyFlag(QuestFlags.Sharable))
            {
                var questStatusData = m_QuestStatus.LookupByKey(quest_id);
                return questStatusData != null;
            }
            return false;
        }

        public void SetQuestStatus(int questId, QuestStatus status, bool update = true)
        {
            Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
            if (quest != null)
            {
                if (!m_QuestStatus.ContainsKey(questId))
                    m_QuestStatus[questId] = new QuestStatusData();

                QuestStatus oldStatus = m_QuestStatus[questId].Status;
                m_QuestStatus[questId].Status = status;
                if (!quest.IsTurnIn())
                    m_QuestStatusSave[questId] = QuestSaveType.Default;

                Global.ScriptMgr.OnQuestStatusChange(this, questId);
                Global.ScriptMgr.OnQuestStatusChange(this, quest, oldStatus, status);
            }

            if (update)
                SendQuestUpdate(questId);
        }

        public void RemoveActiveQuest(int questId, bool update = true)
        {
            var questStatus = m_QuestStatus.LookupByKey(questId);
            if (questStatus != null)
            {
                foreach (var objective in m_questObjectiveStatus.ToList())
                {
                    if (objective.Value.QuestStatusPair.Status == questStatus)
                        m_questObjectiveStatus.Remove(objective);
                }
                m_QuestStatus.Remove(questId);
                m_QuestStatusSave[questId] = QuestSaveType.Delete;
            }

            if (update)
                SendQuestUpdate(questId);
        }

        public void RemoveRewardedQuest(int questId, bool update = true)
        {
            if (m_RewardedQuests.Contains(questId))
            {
                m_RewardedQuests.Remove(questId);
                m_RewardedQuestsSave[questId] = QuestSaveType.ForceDelete;
            }

            uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(questId);
            if (questBit != 0)
                SetQuestCompletedBit(questBit, false);

            // Remove seasonal quest also
            Quest qInfo = Global.ObjectMgr.GetQuestTemplate(questId);
            if (qInfo.IsSeasonal())
            {
                ushort eventId = qInfo.GetEventIdForQuest();
                if (m_seasonalquests.ContainsKey(eventId))
                {
                    m_seasonalquests[eventId].Remove(questId);
                    m_SeasonalQuestChanged = true;
                }
            }

            if (update)
                SendQuestUpdate(questId);
        }

        void SendQuestUpdate(int questId, bool updateInteractions = true, bool updateGameObjectQuestGiverStatus = false)
        {
            var saBounds = Global.SpellMgr.GetSpellAreaForQuestMapBounds(questId);
            if (!saBounds.Empty())
            {
                List<int> aurasToRemove = new();
                List<int> aurasToCast = new();
                GetZoneAndAreaId(out int zone, out int area);

                foreach (var spell in saBounds)
                {
                    if (spell.flags.HasAnyFlag(SpellAreaFlag.AutoRemove) && !spell.IsFitToRequirements(this, zone, area))
                        aurasToRemove.Add(spell.spellId);
                    else if (spell.flags.HasAnyFlag(SpellAreaFlag.AutoCast)
                        && !spell.flags.HasAnyFlag(SpellAreaFlag.IgnoreAutocastOnQuestStatusChange))
                    {
                        aurasToCast.Add(spell.spellId);
                    }
                }

                // Auras matching the requirements will be inside the aurasToCast container.
                // Auras not matching the requirements may prevent using auras matching the requirements.
                // aurasToCast will erase conflicting auras in aurasToRemove container to handle spells used by multiple quests.

                for (var c = 0; c < aurasToRemove.Count;)
                {
                    bool auraRemoved = false;

                    foreach (var i in aurasToCast)
                    {
                        if (aurasToRemove[c] == i)
                        {
                            aurasToRemove.Remove(aurasToRemove[c]);
                            auraRemoved = true;
                            break;
                        }
                    }

                    if (!auraRemoved)
                        ++c;
                }

                foreach (var spellId in aurasToCast)
                {
                    if (!HasAura(spellId))
                        CastSpell(this, spellId, true);
                }

                foreach (var spellId in aurasToRemove)
                    RemoveAurasDueToSpell(spellId);
            }

            if (updateInteractions)
                UpdateVisibleObjectInteractions(true, false, updateGameObjectQuestGiverStatus, true);
        }

        public QuestGiverStatus GetQuestDialogStatus(WorldObject questgiver)
        {
            QuestRelationResult questRelations;
            QuestRelationResult questInvolvedRelations;

            switch (questgiver.GetTypeId())
            {
                case TypeId.GameObject:
                {
                    GameObjectAI ai = questgiver.ToGameObject().GetAI();
                    if (ai != null)
                    {
                        var questStatus = ai.GetDialogStatus(this);
                        if (questStatus.HasValue)
                            return questStatus.Value;
                    }

                    questRelations = Global.ObjectMgr.GetGOQuestRelations(questgiver.GetEntry());
                    questInvolvedRelations = Global.ObjectMgr.GetGOQuestInvolvedRelations(questgiver.GetEntry());
                    break;
                }
                case TypeId.Unit:
                {
                    Creature questGiverCreature = questgiver.ToCreature();
                    if (!questGiverCreature.IsInteractionAllowedWhileHostile() && questGiverCreature.IsHostileTo(this))
                        return QuestGiverStatus.None;

                    if (!questGiverCreature.IsInteractionAllowedInCombat() && questGiverCreature.IsInCombat())
                        return QuestGiverStatus.None;


                    CreatureAI ai = questgiver.ToCreature().GetAI();
                    if (ai != null)
                    {
                        QuestGiverStatus? questStatus = ai.GetDialogStatus(this);
                        if (questStatus.HasValue)
                            return questStatus.Value;
                    }

                    questRelations = Global.ObjectMgr.GetCreatureQuestRelations(questgiver.GetEntry());
                    questInvolvedRelations = Global.ObjectMgr.GetCreatureQuestInvolvedRelations(questgiver.GetEntry());
                    break;
                }
                default:
                    // it's impossible, but check
                    Log.outError(LogFilter.Player, 
                        $"GetQuestDialogStatus called for unexpected Type {questgiver.GetTypeId()}");
                    return QuestGiverStatus.None;
            }

            QuestGiverStatus result = QuestGiverStatus.None;

            foreach (var questId in questInvolvedRelations)
            {
                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest == null)
                    continue;

                switch (GetQuestStatus(questId))
                {
                    case QuestStatus.Complete:
                        if (quest.IsImportant())
                            result |= quest.HasAnyFlag(QuestFlags.HideRewardPoi) ? QuestGiverStatus.ImportantQuestRewardCompleteNoPOI : QuestGiverStatus.ImportantQuestRewardCompletePOI;
                        else if (quest.GetQuestTag() == QuestTagType.CovenantCalling)
                            result |= quest.HasAnyFlag(QuestFlags.HideRewardPoi) ? QuestGiverStatus.CovenantCallingRewardCompleteNoPOI : QuestGiverStatus.CovenantCallingRewardCompletePOI;
                        else if (quest.HasAnyFlag(QuestFlagsEx.Legendary))
                            result |= quest.HasAnyFlag(QuestFlags.HideRewardPoi) ? QuestGiverStatus.LegendaryRewardCompleteNoPOI : QuestGiverStatus.LegendaryRewardCompletePOI;
                        else
                            result |= quest.HasAnyFlag(QuestFlags.HideRewardPoi) ? QuestGiverStatus.RewardCompleteNoPOI : QuestGiverStatus.RewardCompletePOI;
                        break;
                    case QuestStatus.Incomplete:
                        if (quest.IsImportant())
                            result |= QuestGiverStatus.ImportantReward;
                        else if (quest.GetQuestTag() == QuestTagType.CovenantCalling)
                            result |= QuestGiverStatus.CovenantCallingReward;
                        else if (quest.HasAnyFlag(QuestFlagsEx.Legendary))
                            result |= QuestGiverStatus.LegendaryReward;
                        else
                            result |= QuestGiverStatus.Reward;
                        break;
                    default:
                        break;
                }

                if (quest.IsTurnIn() && CanTakeQuest(quest, false) && quest.IsRepeatable() && !quest.IsDailyOrWeekly() && !quest.IsMonthly())
                {
                    if (GetLevel() > (GetQuestLevel(quest) + WorldConfig.Values[WorldCfg.QuestLowLevelHideDiff].Int32))
                        result |= QuestGiverStatus.RepeatableTurnin;
                    else
                        result |= QuestGiverStatus.TrivialRepeatableTurnin;
                }
            }

            foreach (var questId in questRelations)
            {
                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest == null)
                    continue;

                if (!Global.ConditionMgr.IsObjectMeetingNotGroupedConditions(ConditionSourceType.QuestAvailable, quest.Id, this))
                    continue;

                if (GetQuestStatus(questId) == QuestStatus.None)
                {
                    if (CanSeeStartQuest(quest))
                    {
                        if (SatisfyQuestLevel(quest, false))
                        {
                            bool isTrivial = GetLevel() > (GetQuestLevel(quest) + WorldConfig.Values[WorldCfg.QuestLowLevelHideDiff].Int32);
                            if (quest.IsImportant())
                                result |= isTrivial ? QuestGiverStatus.TrivialImportantQuest : QuestGiverStatus.ImportantQuest;
                            else if (quest.GetQuestTag() == QuestTagType.CovenantCalling)
                                result |= QuestGiverStatus.CovenantCallingQuest;
                            else if (quest.HasAnyFlag(QuestFlagsEx.Legendary))
                                result |= isTrivial ? QuestGiverStatus.TrivialLegendaryQuest : QuestGiverStatus.LegendaryQuest;
                            else if (quest.IsDaily())
                                result |= isTrivial ? QuestGiverStatus.TrivialDailyQuest : QuestGiverStatus.DailyQuest;
                            else
                                result |= isTrivial ? QuestGiverStatus.Trivial : QuestGiverStatus.Quest;
                        }
                        else if (quest.IsImportant())
                            result |= QuestGiverStatus.FutureImportantQuest;
                        else if (quest.HasAnyFlag(QuestFlagsEx.Legendary))
                            result |= QuestGiverStatus.FutureLegendaryQuest;
                        else
                            result |= QuestGiverStatus.Future;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Removes quest from log, flags rewarded, but does not give any rewards to player
        /// </summary>
        /// <param name="questIds"></param>
        public void SkipQuests(List<int> questIds)
        {
            bool updateVisibility = false;
            foreach (var questId in questIds)
            {
                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest == null)
                    return;

                ushort questSlot = FindQuestSlot(questId);
                QuestStatus oldStatus = GetQuestStatus(questSlot);

                if (questSlot != SharedConst.MaxQuestLogSize)
                {
                    if (quest.LimitTime != TimeSpan.Zero)
                        RemoveTimedQuest(questId);

                    if (quest.HasFlag(QuestFlags.Pvp))
                    {
                        pvpInfo.IsHostile = pvpInfo.IsInHostileArea || HasPvPForcingQuest();
                        UpdatePvPState();
                    }

                    SetQuestSlot(questSlot, 0);
                    TakeQuestSourceItem(questId, true); // remove quest src item from player
                    AbandonQuest(questId); // remove all quest items player received before abandoning quest. Note, this does not remove normal drop items that happen to be quest requirements.
                    RemoveActiveQuest(questId);
                }

                SetRewardedQuest(questId);
                SendQuestUpdate(questId, false);

                if (!updateVisibility && quest.HasFlag(QuestFlags.UpdatePhaseshift))
                    updateVisibility = PhasingHandler.OnConditionChange(this, false);

                Global.ScriptMgr.OnQuestStatusChange(this, questId);
                Global.ScriptMgr.OnQuestStatusChange(this, quest, oldStatus, QuestStatus.Rewarded);
            }

            UpdateVisibleObjectInteractions(true, false, true, true);

            // make full db save
            SaveToDB(false);

            if (updateVisibility)
                UpdateObjectVisibility();
        }

        public void DespawnPersonalSummonsForQuest(int questId)
        {
            List<Creature> creatureList = GetCreatureListWithOptionsInGrid(100.0f, new FindCreatureOptions() { IgnorePhases = true, PrivateObjectOwnerGuid = GetGUID() }); // we might want to replace this with SummonList in Player at some point

            foreach (Creature creature in creatureList)
            {
                CreatureSummonedData summonedData = Global.ObjectMgr.GetCreatureSummonedData(creature.GetEntry());
                if (summonedData == null)
                    continue;

                if (summonedData.DespawnOnQuestsRemoved != null)
                {
                    if (summonedData.DespawnOnQuestsRemoved.Contains(questId))
                        creature.DespawnOrUnsummon();
                }
            }
        }

        public ushort GetReqKillOrCastCurrentCount(int quest_id, int entry)
        {
            Quest qInfo = Global.ObjectMgr.GetQuestTemplate(quest_id);
            if (qInfo == null)
                return 0;

            ushort slot = FindQuestSlot(quest_id);
            if (slot >= SharedConst.MaxQuestLogSize)
                return 0;

            foreach (QuestObjective obj in qInfo.Objectives)
            {
                if (obj.ObjectID == entry)
                    return (ushort)GetQuestSlotObjectiveData(slot, obj);
            }

            return 0;
        }

        public void AdjustQuestObjectiveProgress(Quest quest)
        {
            // adjust progress of quest objectives that rely on external counters, like items
            foreach (QuestObjective obj in quest.Objectives)
            {
                switch (obj.Type)
                {
                    case QuestObjectiveType.Item:
                        if (!obj.Flags2.HasFlag(QuestObjectiveFlags2.QuestBoundItem))
                        {
                            int reqItemCount = obj.Amount;
                            int curItemCount = GetItemCount(obj.ObjectID, true);
                            SetQuestObjectiveData(obj, Math.Min(curItemCount, reqItemCount));
                        }
                        break;
                    case QuestObjectiveType.Currency:
                        int reqCurrencyCount = obj.Amount;
                        int curCurrencyCount = GetCurrencyQuantity(obj.ObjectID);
                        SetQuestObjectiveData(obj, Math.Min(reqCurrencyCount, curCurrencyCount));
                        break;
                    case QuestObjectiveType.CriteriaTree:
                        if (m_questObjectiveCriteriaMgr.HasCompletedObjective(obj))
                            SetQuestObjectiveData(obj, 1);
                        break;
                }
            }
        }

        public ushort FindQuestSlot(int quest_id)
        {
            for (ushort i = 0; i < SharedConst.MaxQuestLogSize; ++i)
            {
                if (GetQuestSlotQuestId(i) == quest_id)
                    return i;
            }

            return SharedConst.MaxQuestLogSize;
        }

        public int GetQuestSlotQuestId(ushort slot)
        {
            return m_playerData.QuestLog[slot].QuestID;
        }

        public uint GetQuestSlotState(ushort slot)
        {
            return m_playerData.QuestLog[slot].StateFlags;
        }

        public ushort GetQuestSlotCounter(ushort slot, byte counter)
        {
            if (counter < SharedConst.MaxQuestCounts)
                return m_playerData.QuestLog[slot].ObjectiveProgress[counter];

            return 0;
        }

        public UnixTime64 GetQuestSlotEndTime(ushort slot)
        {
            return m_playerData.QuestLog[slot].EndTime;
        }

        public int GetQuestSlotObjectiveData(ushort slot, QuestObjective objective)
        {
            if (objective.StorageIndex < 0)
            {
                Log.outError(LogFilter.Player, 
                    $"Player.GetQuestObjectiveData: Called for quest {objective.QuestID} with invalid " +
                    $"StorageIndex {objective.StorageIndex} (objective data is not tracked)");
                return 0;
            }

            if (objective.StorageIndex >= SharedConst.MaxQuestCounts)
            {
                Log.outError(LogFilter.Player, 
                    $"Player.GetQuestObjectiveData: Player '{GetName()}' ({GetGUID()}) quest {objective.QuestID} " +
                    $"out of range StorageIndex {objective.StorageIndex}");
                return 0;
            }

            if (!objective.IsStoringFlag())
                return GetQuestSlotCounter(slot, (byte)objective.StorageIndex);

            return ((GetQuestSlotState(slot) & objective.StorageIndex) != 0) ? 1 : 0;
        }

        int GetQuestObjectiveData(int questId, int objectiveId)
        {
            ushort slot = FindQuestSlot(questId);
            if (slot >= SharedConst.MaxQuestLogSize)
                return 0;

            QuestObjective obj = Global.ObjectMgr.GetQuestObjective(objectiveId);
            if (obj == null)
                return 0;

            return GetQuestSlotObjectiveData(slot, obj);
        }

        public void SetQuestSlot(ushort slot, int quest_id)
        {
            var questLogField = m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.QuestLog, slot);
            SetUpdateFieldValue(questLogField.ModifyValue(questLogField.QuestID), quest_id);
            SetUpdateFieldValue(questLogField.ModifyValue(questLogField.StateFlags), 0u);
            SetUpdateFieldValue(questLogField.ModifyValue(questLogField.EndTime), UnixTime64.Zero);

            for (int i = 0; i < SharedConst.MaxQuestCounts; ++i)
                SetUpdateFieldValue(ref questLogField.ModifyValue(questLogField.ObjectiveProgress, i), (ushort)0);

        }

        public void SetQuestSlotCounter(ushort slot, byte counter, ushort count)
        {
            if (counter >= SharedConst.MaxQuestCounts)
                return;

            QuestLog questLog = m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.QuestLog, slot);
            SetUpdateFieldValue(ref questLog.ModifyValue(questLog.ObjectiveProgress, counter), count);
        }

        public void SetQuestSlotState(ushort slot, QuestSlotStateMask state)
        {
            QuestLog questLogField = m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.QuestLog, slot);
            SetUpdateFieldFlagValue(questLogField.ModifyValue(questLogField.StateFlags), (uint)state);
        }

        public void RemoveQuestSlotState(ushort slot, QuestSlotStateMask state)
        {
            QuestLog questLogField = m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.QuestLog, slot);
            RemoveUpdateFieldFlagValue(questLogField.ModifyValue(questLogField.StateFlags), (uint)state);
        }

        public void SetQuestSlotEndTime(ushort slot, ServerTime endTime)
        {
            QuestLog questLog = m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.QuestLog, slot);
            SetUpdateFieldValue(questLog.ModifyValue(questLog.EndTime), (UnixTime64)endTime);
        }

        void SetQuestCompletedBit(uint questBit, bool completed)
        {
            if (questBit == 0)
                return;

            uint fieldOffset = (uint)((questBit - 1) / ActivePlayerData.QuestCompletedBitsPerBlock);
            if (fieldOffset >= ActivePlayerData.QuestCompletedBitsSize)
                return;

            ulong flag = 1ul << (((int)questBit - 1) % ActivePlayerData.QuestCompletedBitsPerBlock);
            if (completed)
                SetUpdateFieldFlagValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.QuestCompleted, (int)fieldOffset), flag);
            else
                RemoveUpdateFieldFlagValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.QuestCompleted, (int)fieldOffset), flag);
        }

        public void AreaExploredOrEventHappens(int questId)
        {
            if (questId != 0)
            {
                QuestStatusData status = m_QuestStatus.LookupByKey(questId);
                if (status != null)
                {
                    // Dont complete failed quest
                    if (!status.Explored && status.Status != QuestStatus.Failed)
                    {
                        status.Explored = true;
                        m_QuestStatusSave[questId] = QuestSaveType.Default;

                        SendQuestComplete(questId);
                    }
                }
                if (CanCompleteQuest(questId))
                    CompleteQuest(questId);
            }
        }

        public void GroupEventHappens(int questId, WorldObject pEventObject)
        {
            var group = GetGroup();
            if (group != null)
            {
                for (GroupReference refe = group.GetFirstMember(); refe != null; refe = refe.Next())
                {
                    Player player = refe.GetSource();

                    // for any leave or dead (with not released body) group member at appropriate distance
                    if (player != null && player.IsAtGroupRewardDistance(pEventObject) && player.GetCorpse() == null)
                        player.AreaExploredOrEventHappens(questId);
                }
            }
            else
                AreaExploredOrEventHappens(questId);
        }

        Func<QuestObjective, bool> QuestBoundItemFunc = objective => { return objective.Flags2.HasFlag(QuestObjectiveFlags2.QuestBoundItem); };
        Func<QuestObjective, bool> NotQuestBoundItemFunc = objective => { return !objective.Flags2.HasFlag(QuestObjectiveFlags2.QuestBoundItem); };

        public void ItemAddedQuestCheck(int entry, int count, bool boundItemFlagRequirement = false)
        {
            ItemAddedQuestCheck(entry, count, boundItemFlagRequirement, out _);
        }

        public void ItemAddedQuestCheck(int entry, int count, bool boundItemFlagRequirement, out bool hadBoundItemObjective)
        {
            hadBoundItemObjective = false;

            List<QuestObjective> updatedObjectives = new();
            Func<QuestObjective, bool> objectiveFilter = null;
            
            objectiveFilter = boundItemFlagRequirement ? QuestBoundItemFunc : NotQuestBoundItemFunc;

            ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(entry);
            UpdateQuestObjectiveProgress(QuestObjectiveType.Item, itemTemplate.GetId(), count, ObjectGuid.Empty, updatedObjectives, objectiveFilter);
            if (itemTemplate.QuestLogItemId != 0 && (updatedObjectives.Count != 1 || !updatedObjectives[0].Flags2.HasFlag(QuestObjectiveFlags2.QuestBoundItem)))
                UpdateQuestObjectiveProgress(QuestObjectiveType.Item, itemTemplate.QuestLogItemId, count, ObjectGuid.Empty, updatedObjectives, objectiveFilter);

            if (updatedObjectives.Count == 1 && updatedObjectives[0].Flags2.HasFlag(QuestObjectiveFlags2.QuestBoundItem))
            {
                hadBoundItemObjective = updatedObjectives.Count == 1 && updatedObjectives[0].Flags2.HasFlag(QuestObjectiveFlags2.QuestBoundItem);

                SendQuestUpdateAddItem(itemTemplate, updatedObjectives[0], (ushort)count);
            }
        }

        public void ItemRemovedQuestCheck(int entry, int count)
        {
            foreach (var objectiveStatusData in m_questObjectiveStatus[(QuestObjectiveType.Item, entry)])
            {
                int questId = objectiveStatusData.QuestStatusPair.QuestID;
                ushort logSlot = objectiveStatusData.QuestStatusPair.Status.Slot;
                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                QuestObjective objective = Global.ObjectMgr.GetQuestObjective(objectiveStatusData.ObjectiveId);

                if (quest == null || objective == null || !IsQuestObjectiveCompletable(logSlot, quest, objective))
                    continue;

                int currentAmount = GetQuestObjectiveData(questId, objectiveStatusData.ObjectiveId);
                if (currentAmount < count)
                {
                    currentAmount = count;
                }
                                
                SetQuestObjectiveData(objective, currentAmount - count);
                IncompleteQuest(questId);
            }

            UpdateVisibleObjectInteractions(true, false, false, true);
        }

        public void KilledMonster(CreatureTemplate cInfo, ObjectGuid guid)
        {
            Cypher.Assert(cInfo != null);

            if (cInfo.Entry != 0)
                KilledMonsterCredit(cInfo.Entry, guid);

            for (byte i = 0; i < 2; ++i)
            {
                if (cInfo.KillCredit[i] != 0)
                    KilledMonsterCredit(cInfo.KillCredit[i]);
            }
        }

        public void KilledMonsterCredit(int entry, ObjectGuid guid = default)
        {
            ushort addKillCount = 1;
            int real_entry = entry;
            Creature killed = null;
            if (!guid.IsEmpty())
            {
                killed = GetMap().GetCreature(guid);
                if (killed != null && killed.GetEntry() != 0)
                    real_entry = killed.GetEntry();
            }

            StartCriteria(CriteriaStartEvent.KillNPC, real_entry);   // MUST BE CALLED FIRST
            UpdateCriteria(CriteriaType.KillCreature, real_entry, addKillCount, 0, killed);

            UpdateQuestObjectiveProgress(QuestObjectiveType.Monster, entry, 1, guid);
        }

        public void KilledPlayerCredit(ObjectGuid victimGuid)
        {
            StartCriteria(CriteriaStartEvent.KillPlayer, 0);
            UpdateQuestObjectiveProgress(QuestObjectiveType.PlayerKills, 0, 1, victimGuid);
        }

        public void KillCreditGO(int entry, ObjectGuid guid = default)
        {
            UpdateQuestObjectiveProgress(QuestObjectiveType.GameObject, entry, 1, guid);
        }

        public void KillCreditCriteriaTreeObjective(QuestObjective questObjective)
        {
            UpdateQuestObjectiveProgress(QuestObjectiveType.CriteriaTree, questObjective.ObjectID, 1);
        }

        public void TalkedToCreature(int entry, ObjectGuid guid)
        {
            UpdateQuestObjectiveProgress(QuestObjectiveType.TalkTo, entry, 1, guid);
        }

        public void MoneyChanged(long value)
        {
            UpdateQuestObjectiveProgress(QuestObjectiveType.Money, 0, value - GetMoney());
        }

        public void ReputationChanged(FactionRecord FactionRecord, int change)
        {
            UpdateQuestObjectiveProgress(QuestObjectiveType.MinReputation, FactionRecord.Id, change);
            UpdateQuestObjectiveProgress(QuestObjectiveType.MaxReputation, FactionRecord.Id, change);
            UpdateQuestObjectiveProgress(QuestObjectiveType.IncreaseReputation, FactionRecord.Id, change);
        }

        void CurrencyChanged(int currencyId, int change)
        {
            UpdateQuestObjectiveProgress(QuestObjectiveType.Currency, currencyId, change);
            UpdateQuestObjectiveProgress(QuestObjectiveType.HaveCurrency, currencyId, change);
            UpdateQuestObjectiveProgress(QuestObjectiveType.ObtainCurrency, currencyId, change);
        }

        public void UpdateQuestObjectiveProgress(QuestObjectiveType objectiveType, int objectId, long addCount, ObjectGuid victimGuid = default, List<QuestObjective> updatedObjectives = null, Func<QuestObjective, bool> objectiveFilter = null)
        {
            bool anyObjectiveChangedCompletionState = false;
            bool updatePhaseShift = false;
            bool updateZoneAuras = false;

            foreach (var objectiveStatusData in m_questObjectiveStatus[(objectiveType, objectId)])
            {
                int questId = objectiveStatusData.QuestStatusPair.QuestID;
                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest == null)
                    continue;

                if (!QuestObjective.CanAlwaysBeProgressedInRaid(objectiveType))
                {
                    if (GetGroup() != null && GetGroup().IsRaidGroup() && !quest.IsAllowedInRaid(GetMap().GetDifficultyID()))
                        continue;
                }

                ushort logSlot = objectiveStatusData.QuestStatusPair.Status.Slot;
                QuestObjective objective = Global.ObjectMgr.GetQuestObjective(objectiveStatusData.ObjectiveId);
                if (objective == null || !IsQuestObjectiveCompletable(logSlot, quest, objective))
                    continue;

                if (quest.HasAnyFlag(QuestFlagsEx.NoCreditForProxy))
                {
                    if (objective.Type == QuestObjectiveType.Monster && victimGuid.IsEmpty())
                        continue;
                }

                bool objectiveWasComplete = IsQuestObjectiveComplete(logSlot, quest, objective);
                if (objectiveWasComplete && addCount >= 0)
                    continue;

                if (objectiveFilter != null && !objectiveFilter(objective))
                    continue;

                bool objectiveIsNowComplete = false;
                if (objective.IsStoringValue())
                {
                    if (objectiveType == QuestObjectiveType.PlayerKills && objective.Flags.HasAnyFlag(QuestObjectiveFlags.KillPlayersSameFaction))
                    {
                        Player victim = Global.ObjAccessor.GetPlayer(GetMap(), victimGuid);
                        if (victim?.GetEffectiveTeam() != GetEffectiveTeam())
                            continue;
                    }

                    int currentProgress = GetQuestSlotObjectiveData(logSlot, objective);
                    if (addCount > 0 ? (currentProgress < objective.Amount) : (currentProgress > 0))
                    {
                        int newProgress = (int)Math.Clamp(currentProgress + addCount, 0, objective.Amount);
                        SetQuestObjectiveData(objective, newProgress);
                        if (addCount > 0 && !objective.Flags.HasFlag(QuestObjectiveFlags.HideCreditMsg))
                        {
                            switch (objectiveType)
                            {
                                case QuestObjectiveType.Item:
                                    break; // case handled by SMSG_ITEM_PUSH_RESULT
                                case QuestObjectiveType.PlayerKills:
                                    SendQuestUpdateAddPlayer(quest, newProgress);
                                    break;
                                default:
                                    SendQuestUpdateAddCredit(quest, victimGuid, objective, newProgress);
                                    break;
                            }
                        }

                        objectiveIsNowComplete = IsQuestObjectiveComplete(logSlot, quest, objective);
                    }
                }
                else if (objective.IsStoringFlag())
                {
                    SetQuestObjectiveData(objective, addCount > 0 ? 1 : 0);

                    if (addCount > 0 && !objective.Flags.HasFlag(QuestObjectiveFlags.HideCreditMsg))
                        SendQuestUpdateAddCreditSimple(objective);

                    objectiveIsNowComplete = IsQuestObjectiveComplete(logSlot, quest, objective);
                }
                else
                {
                    switch (objectiveType)
                    {
                        case QuestObjectiveType.Currency:
                            objectiveIsNowComplete = GetCurrencyQuantity(objectId) + addCount >= objective.Amount;
                            break;
                        case QuestObjectiveType.LearnSpell:
                            objectiveIsNowComplete = addCount != 0;
                            break;
                        case QuestObjectiveType.MinReputation:
                            objectiveIsNowComplete = GetReputationMgr().GetReputation(objectId) + addCount >= objective.Amount;
                            break;
                        case QuestObjectiveType.MaxReputation:
                            objectiveIsNowComplete = GetReputationMgr().GetReputation(objectId) + addCount <= objective.Amount;
                            break;
                        case QuestObjectiveType.Money:
                            objectiveIsNowComplete = GetMoney() + addCount >= objective.Amount;
                            break;
                        case QuestObjectiveType.ProgressBar:
                            objectiveIsNowComplete = IsQuestObjectiveProgressBarComplete(logSlot, quest);
                            break;
                        default:
                            Cypher.Assert(false, $"Unhandled quest objective type {objectiveType}");
                            break;
                    }
                }

                if (objective.Flags.HasAnyFlag(QuestObjectiveFlags.PartOfProgressBar))
                {
                    if (IsQuestObjectiveProgressBarComplete(logSlot, quest))
                    {
                        var progressBarObjective = quest.Objectives.Find(otherObjective =>
                        otherObjective.Type == QuestObjectiveType.ProgressBar
                        && !otherObjective.Flags.HasFlag(QuestObjectiveFlags.PartOfProgressBar)
                        );

                        if (progressBarObjective != null)
                            SendQuestUpdateAddCreditSimple(progressBarObjective);

                        objectiveIsNowComplete = true;
                    }
                }

                if (objectiveWasComplete != objectiveIsNowComplete)
                    anyObjectiveChangedCompletionState = true;

                if (objectiveIsNowComplete && objective.CompletionEffect != null)
                {
                    if (objective.CompletionEffect.GameEventId.HasValue)
                        GameEvents.Trigger(objective.CompletionEffect.GameEventId.Value, this, null);
                    if (objective.CompletionEffect.SpellId.HasValue)
                        CastSpell(this, objective.CompletionEffect.SpellId.Value, true);
                    if (objective.CompletionEffect.ConversationId.HasValue)
                        Conversation.CreateConversation(objective.CompletionEffect.ConversationId.Value, this, GetPosition(), GetGUID());
                    if (objective.CompletionEffect.UpdatePhaseShift)
                        updatePhaseShift = true;
                    if (objective.CompletionEffect.UpdateZoneAuras)
                        updateZoneAuras = true;
                }

                if (objectiveIsNowComplete)
                {
                    if (CanCompleteQuest(questId, objective.Id))
                        CompleteQuest(questId);
                }
                else if (!objective.Flags.HasAnyFlag(QuestObjectiveFlags.Optional)
                    && objectiveStatusData.QuestStatusPair.Status.Status == QuestStatus.Complete)
                {
                    IncompleteQuest(questId);
                }

                if (updatedObjectives != null)
                    updatedObjectives.Add(objective);

                if (objective.Type == QuestObjectiveType.Item && addCount >= 0
                    && objective.Flags2.HasFlag(QuestObjectiveFlags2.QuestBoundItem))
                {
                    break;
                }
            }

            if (anyObjectiveChangedCompletionState)
                UpdateVisibleObjectInteractions(true, false, false, true);

            if (updatePhaseShift)
                PhasingHandler.OnConditionChange(this);

            if (updateZoneAuras)
            {
                UpdateZoneDependentAuras(GetZoneId());
                UpdateAreaDependentAuras(GetAreaId());
            }
        }

        public bool HasQuestForItem(int itemid)
        {
            // Search incomplete objective first
            if (GetQuestObjectiveForItem(itemid, true) != null)
                return true;

            // This part - for ItemDrop
            foreach (var questStatus in m_QuestStatus)
            {
                if (questStatus.Value.Status != QuestStatus.Incomplete)
                    continue;

                Quest qInfo = Global.ObjectMgr.GetQuestTemplate(questStatus.Key);
                // hide quest if player is in raid-group and quest is no raid quest
                if (GetGroup() != null && GetGroup().IsRaidGroup() && !qInfo.IsAllowedInRaid(GetMap().GetDifficultyID()))
                {
                    if (!InBattleground())
                        continue;
                }

                for (byte j = 0; j < SharedConst.QuestItemDropCount; ++j)
                {
                    // examined item is a source item
                    if (qInfo.ItemDrop[j] != itemid)
                        continue;

                    ItemTemplate pProto = Global.ObjectMgr.GetItemTemplate(itemid);

                    // allows custom amount drop when not 0
                    int maxAllowedCount = qInfo.ItemDropQuantity[j] != 0 ? qInfo.ItemDropQuantity[j] : pProto.GetMaxStackSize();

                    // 'unique' item
                    if (pProto.GetMaxCount() != 0 && pProto.GetMaxCount() < maxAllowedCount)
                        maxAllowedCount = pProto.GetMaxCount();

                    if (GetItemCount(itemid, true) < maxAllowedCount)
                        return true;
                }
            }

            return false;
        }

        public bool CanSeeGossipOn(Creature creature)
        {
            if (creature.HasNpcFlag(NPCFlags1.Gossip))
            {
                if (GetGossipMenuForSource(creature) != 0)
                    return true;
            }

            // for cases with questgiver/ender without gossip menus
            if (creature.HasNpcFlag(NPCFlags1.QuestGiver))
            {
                QuestRelationResult objectQIR = Global.ObjectMgr.GetCreatureQuestInvolvedRelations(creature.GetEntry());
                foreach (var quest_id in objectQIR)
                {
                    QuestStatus status = GetQuestStatus(quest_id);
                    if (status == QuestStatus.Complete || status == QuestStatus.Incomplete)
                        return true;
                }

                QuestRelationResult objectQR = Global.ObjectMgr.GetCreatureQuestRelations(creature.GetEntry());
                foreach (var quest_id in objectQR)
                {
                    Quest quest = Global.ObjectMgr.GetQuestTemplate(quest_id);
                    if (quest == null)
                        continue;

                    if (CanTakeQuest(quest, false))
                        return true;
                }
            }
            return false;
        }

        QuestObjective GetQuestObjectiveForItem(int itemId, bool onlyIncomplete)
        {
            QuestObjective findObjectiveForItem(int tempItemId)
            {
                foreach (var objectiveItr in m_questObjectiveStatus[(QuestObjectiveType.Item, tempItemId)])
                {
                    Quest qInfo = Global.ObjectMgr.GetQuestTemplate(objectiveItr.QuestStatusPair.QuestID);
                    QuestObjective objective = Global.ObjectMgr.GetQuestObjective(objectiveItr.ObjectiveId);
                    if (qInfo == null || objective == null || !IsQuestObjectiveCompletable(objectiveItr.QuestStatusPair.Status.Slot, qInfo, objective))
                        continue;

                    // hide quest if player is in raid-group and quest is no raid quest
                    if (GetGroup() != null && GetGroup().IsRaidGroup() && !qInfo.IsAllowedInRaid(GetMap().GetDifficultyID()))
                    {
                        if (!InBattleground()) //there are two ways.. we can make every bg-quest a raidquest, or add this code here.. i don't know if this can be exploited by other quests, but i think all other quests depend on a specific area.. but keep this in mind, if something strange happens later
                            continue;
                    }

                    if (!onlyIncomplete || !IsQuestObjectiveComplete(objectiveItr.QuestStatusPair.Status.Slot, qInfo, objective))
                        return objective;
                }
                return null;
            };

            QuestObjective objective = findObjectiveForItem(itemId);
            if (objective != null)
                return objective;

            ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(itemId);
            if (itemTemplate != null && itemTemplate.QuestLogItemId != 0)
            {
                objective = findObjectiveForItem(itemTemplate.QuestLogItemId);
                if (objective != null)
                    return objective;
            }

            return null;
        }

        public int GetQuestObjectiveData(QuestObjective objective)
        {
            ushort slot = FindQuestSlot(objective.QuestID);
            if (slot >= SharedConst.MaxQuestLogSize)
                return 0;

            return GetQuestSlotObjectiveData(slot, objective);
        }

        public void SetQuestObjectiveData(QuestObjective objective, int data)
        {
            if (objective.StorageIndex < 0)
            {
                Log.outError(LogFilter.Player, 
                    $"Player.SetQuestObjectiveData: called for quest {objective.QuestID} with invalid " +
                    $"StorageIndex {objective.StorageIndex} (objective data is not tracked)");
                return;
            }

            var status = m_QuestStatus.LookupByKey(objective.QuestID);
            if (status == null)
            {
                Log.outError(LogFilter.Player, 
                    $"Player.SetQuestObjectiveData: player '{GetName()}' ({GetGUID()}) doesn't have " +
                    $"quest status data (QuestID: {objective.QuestID})");
                return;
            }
            if (objective.StorageIndex >= SharedConst.MaxQuestCounts)
            {
                Log.outError(LogFilter.Player, 
                    $"Player.SetQuestObjectiveData: player '{GetName()}' ({GetGUID()}) quest {objective.QuestID} " +
                    $"out of range StorageIndex {objective.StorageIndex}");
                return;
            }

            if (status.Slot >= SharedConst.MaxQuestLogSize)
                return;

            // No change
            int oldData = GetQuestSlotObjectiveData(status.Slot, objective);
            if (oldData == data)
                return;

            Quest quest = Global.ObjectMgr.GetQuestTemplate(objective.QuestID);
            if (quest != null)
                Global.ScriptMgr.OnQuestObjectiveChange(this, quest, objective, oldData, data);

            // Add to save
            m_QuestStatusSave[objective.QuestID] = QuestSaveType.Default;

            // Update quest fields
            if (!objective.IsStoringFlag())
                SetQuestSlotCounter(status.Slot, (byte)objective.StorageIndex, (ushort)data);
            else if (data != 0)
                SetQuestSlotState(status.Slot, QuestSlotStateMask.None.SetSlot(objective.StorageIndex));
            else
                RemoveQuestSlotState(status.Slot, QuestSlotStateMask.None.SetSlot(objective.StorageIndex));
        }

        public bool IsQuestObjectiveCompletable(ushort slot, Quest quest, QuestObjective objective)
        {
            Cypher.Assert(objective.QuestID == quest.Id);

            if (objective.Flags.HasAnyFlag(QuestObjectiveFlags.PartOfProgressBar))
            {
                // delegate check to actual progress bar objective
                var progressBarObjective = quest.Objectives.Find(otherObjective => 
                otherObjective.Type == QuestObjectiveType.ProgressBar 
                && !otherObjective.Flags.HasAnyFlag(QuestObjectiveFlags.PartOfProgressBar)
                );

                if (progressBarObjective == null)
                    return false;

                return IsQuestObjectiveCompletable(slot, quest, progressBarObjective) && !IsQuestObjectiveComplete(slot, quest, progressBarObjective);
            }

            int objectiveIndex = quest.Objectives.IndexOf(objective);
            if (objectiveIndex == 0)
                return true;

            // check sequenced objectives
            int previousIndex = objectiveIndex - 1;
            bool objectiveSequenceSatisfied = true;
            bool previousSequencedObjectiveComplete = false;
            int previousSequencedObjectiveIndex = -1;
            do
            {
                QuestObjective previousObjective = quest.Objectives[previousIndex];
                if (previousObjective.Flags.HasAnyFlag(QuestObjectiveFlags.Sequenced))
                {
                    previousSequencedObjectiveIndex = previousIndex;
                    previousSequencedObjectiveComplete = IsQuestObjectiveComplete(slot, quest, previousObjective);
                    break;
                }

                if (objectiveSequenceSatisfied)
                    objectiveSequenceSatisfied = IsQuestObjectiveComplete(slot, quest, previousObjective) || previousObjective.Flags.HasAnyFlag(QuestObjectiveFlags.Optional | QuestObjectiveFlags.PartOfProgressBar);

                --previousIndex;
            } while (previousIndex >= 0);

            if (objective.Flags.HasAnyFlag(QuestObjectiveFlags.Sequenced))
            {
                if (previousSequencedObjectiveIndex == -1)
                    return objectiveSequenceSatisfied;
                if (!previousSequencedObjectiveComplete || !objectiveSequenceSatisfied)
                    return false;
            }
            else if (!previousSequencedObjectiveComplete && previousSequencedObjectiveIndex != -1)
            {
                if (!IsQuestObjectiveCompletable(slot, quest, quest.Objectives[previousSequencedObjectiveIndex]))
                    return false;
            }

            return true;
        }

        public bool IsQuestObjectiveComplete(ushort slot, Quest quest, QuestObjective objective)
        {
            switch (objective.Type)
            {
                case QuestObjectiveType.Monster:
                case QuestObjectiveType.Item:
                case QuestObjectiveType.GameObject:
                case QuestObjectiveType.TalkTo:
                case QuestObjectiveType.PlayerKills:
                case QuestObjectiveType.WinPvpPetBattles:
                case QuestObjectiveType.HaveCurrency:
                case QuestObjectiveType.ObtainCurrency:
                case QuestObjectiveType.IncreaseReputation:
                    if (GetQuestSlotObjectiveData(slot, objective) < objective.Amount)
                        return false;
                    break;
                case QuestObjectiveType.MinReputation:
                    if (GetReputationMgr().GetReputation(objective.ObjectID) < objective.Amount)
                        return false;
                    break;
                case QuestObjectiveType.MaxReputation:
                    if (GetReputationMgr().GetReputation(objective.ObjectID) > objective.Amount)
                        return false;
                    break;
                case QuestObjectiveType.Money:
                    if (!HasEnoughMoney(objective.Amount))
                        return false;
                    break;
                case QuestObjectiveType.AreaTrigger:
                case QuestObjectiveType.WinPetBattleAgainstNpc:
                case QuestObjectiveType.DefeatBattlePet:
                case QuestObjectiveType.CriteriaTree:
                case QuestObjectiveType.AreaTriggerEnter:
                case QuestObjectiveType.AreaTriggerExit:
                    if (GetQuestSlotObjectiveData(slot, objective) == 0)
                        return false;
                    break;
                case QuestObjectiveType.LearnSpell:
                    if (!HasSpell(objective.ObjectID))
                        return false;
                    break;
                case QuestObjectiveType.Currency:
                    if (!HasCurrency(objective.ObjectID, objective.Amount))
                        return false;
                    break;
                case QuestObjectiveType.ProgressBar:
                    if (!IsQuestObjectiveProgressBarComplete(slot, quest))
                        return false;
                    break;
                default:
                    Log.outError(LogFilter.Player, 
                        $"Player.CanCompleteQuest: Player '{GetName()}' ({GetGUID()}) tried to complete " +
                        $"a quest (ID: {objective.QuestID}) with an unknown objective Type {objective.Type}");
                    return false;
            }

            return true;
        }

        bool IsQuestObjectiveComplete(int questId, int objectiveId)
        {
            Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
            if (quest == null)
                return false;

            ushort slot = FindQuestSlot(questId);
            if (slot >= SharedConst.MaxQuestLogSize)
                return false;

            QuestObjective obj = Global.ObjectMgr.GetQuestObjective(objectiveId);
            if (obj == null)
                return false;

            return IsQuestObjectiveComplete(slot, quest, obj);
        }

        public bool IsQuestObjectiveProgressBarComplete(ushort slot, Quest quest)
        {
            float progress = 0.0f;
            foreach (QuestObjective obj in quest.Objectives)
            {
                if (obj.Flags.HasAnyFlag(QuestObjectiveFlags.PartOfProgressBar))
                {
                    progress += GetQuestSlotObjectiveData(slot, obj) * obj.ProgressBarWeight;
                    if (progress >= 100.0f)
                        return true;
                }
            }

            return false;
        }

        public void SendQuestComplete(int questId)
        {
            if (questId != 0)
            {
                QuestUpdateComplete data = new();
                data.QuestID = questId;
                SendPacket(data);
            }
        }

        public void SendQuestReward(Quest quest, Creature questGiver, int xp, bool hideChatMessage)
        {
            var questId = quest.Id;
            Global.GameEventMgr.HandleQuestComplete(questId);

            int moneyReward;

            if (!IsMaxLevel())
            {
                moneyReward = GetQuestMoneyReward(quest);
            }
            else // At max level, increase gold reward
            {
                xp = 0;
                moneyReward = GetQuestMoneyReward(quest) + (int)(quest.GetRewMoneyMaxLevel() * WorldConfig.Values[WorldCfg.RateDropMoney].Float);
            }

            QuestGiverQuestComplete packet = new();

            packet.QuestID = questId;
            packet.MoneyReward = moneyReward;
            packet.XPReward = xp;
            packet.SkillLineIDReward = quest.RewardSkillId;
            packet.NumSkillUpsReward = quest.RewardSkillPoints;

            if (questGiver != null)
            {
                if (questGiver.IsGossip())
                    packet.LaunchGossip = quest.HasAnyFlag(QuestFlags.LaunchGossipComplete);

                if (questGiver.IsQuestGiver())
                    packet.LaunchQuest = (GetQuestDialogStatus(questGiver) & ~QuestGiverStatus.Future) != QuestGiverStatus.None;

                if (!quest.HasAnyFlag(QuestFlags.AutoComplete))
                {
                    Quest rewardQuest = GetNextQuest(questGiver, quest);
                    if (rewardQuest != null)
                        packet.UseQuestReward = CanTakeQuest(rewardQuest, false);
                }
            }

            packet.HideChatMessage = hideChatMessage;

            SendPacket(packet);
        }

        public void SendQuestFailed(int questId, InventoryResult reason = InventoryResult.Ok)
        {
            if (questId != 0)
            {
                QuestGiverQuestFailed questGiverQuestFailed = new();
                questGiverQuestFailed.QuestID = questId;
                questGiverQuestFailed.Reason = reason; // failed reason (valid reasons: 4, 16, 50, 17, other values show default message)
                SendPacket(questGiverQuestFailed);
            }
        }

        public void SendQuestTimerFailed(int questId)
        {
            if (questId != 0)
            {
                QuestUpdateFailedTimer questUpdateFailedTimer = new();
                questUpdateFailedTimer.QuestID = questId;
                SendPacket(questUpdateFailedTimer);
            }
        }

        public void SendCanTakeQuestResponse(QuestFailedReasons reason, bool sendErrorMessage = true, string reasonText = "")
        {
            QuestGiverInvalidQuest questGiverInvalidQuest = new();

            questGiverInvalidQuest.Reason = reason;
            questGiverInvalidQuest.SendErrorMessage = sendErrorMessage;
            questGiverInvalidQuest.ReasonText = reasonText;

            SendPacket(questGiverInvalidQuest);
        }

        public void SendQuestConfirmAccept(Quest quest, Player receiver)
        {
            if (receiver == null)
                return;

            QuestConfirmAcceptResponse packet = new();

            packet.QuestTitle = quest.LogTitle;

            Locale loc_idx = receiver.GetSession().GetSessionDbLocaleIndex();
            if (loc_idx != Locale.enUS)
            {
                QuestTemplateLocale questLocale = Global.ObjectMgr.GetQuestLocale(quest.Id);
                if (questLocale != null)
                    ObjectManager.GetLocaleString(questLocale.LogTitle, loc_idx, ref packet.QuestTitle);
            }

            packet.QuestID = quest.Id;
            packet.InitiatedBy = GetGUID();

            receiver.SendPacket(packet);
        }

        public void SendPushToPartyResponse(Player player, QuestPushReason reason, Quest quest = null)
        {
            if (player != null)
            {
                QuestPushResultResponse response = new();
                response.SenderGUID = player.GetGUID();
                response.Result = reason;
                if (quest != null)
                {
                    response.QuestTitle = quest.LogTitle;
                    Locale localeConstant = GetSession().GetSessionDbLocaleIndex();
                    if (localeConstant != Locale.enUS)
                    {
                        QuestTemplateLocale questTemplateLocale = Global.ObjectMgr.GetQuestLocale(quest.Id);
                        if (questTemplateLocale != null)
                            ObjectManager.GetLocaleString(questTemplateLocale.LogTitle, localeConstant, ref response.QuestTitle);
                    }
                }

                SendPacket(response);
            }
        }

        void SendQuestUpdateAddCredit(Quest quest, ObjectGuid guid, QuestObjective obj, int count)
        {
            QuestUpdateAddCredit packet = new();
            packet.VictimGUID = guid;
            packet.QuestID = quest.Id;
            packet.ObjectID = obj.ObjectID;
            packet.Count = (ushort)count;
            packet.Required = (ushort)obj.Amount;
            packet.ObjectiveType = (byte)obj.Type;
            SendPacket(packet);
        }

        public void SendQuestUpdateAddCreditSimple(QuestObjective obj)
        {
            QuestUpdateAddCreditSimple packet = new();
            packet.QuestID = obj.QuestID;
            packet.ObjectID = obj.ObjectID;
            packet.ObjectiveType = obj.Type;
            SendPacket(packet);
        }

        void SendQuestUpdateAddItem(ItemTemplate itemTemplate, QuestObjective obj, ushort count)
        {
            ItemPushResult packet = new();

            packet.PlayerGUID = GetGUID();

            packet.Slot = InventorySlots.Bag0;
            packet.SlotInBag = 0;
            packet.Item.ItemID = itemTemplate.GetId();
            packet.QuestLogItemID = itemTemplate.QuestLogItemId;
            packet.Quantity = count;
            packet.QuantityInInventory = GetQuestObjectiveData(obj);
            packet.DisplayText = ItemPushResult.DisplayType.EncounterLoot;

            if (GetGroup() != null && !itemTemplate.HasFlag(ItemFlags3.DontReportLootLogToParty))
                GetGroup().BroadcastPacket(packet, true);
            else
                SendPacket(packet);
        }

        public void SendQuestUpdateAddPlayer(Quest quest, int newCount)
        {
            QuestUpdateAddPvPCredit packet = new();
            packet.QuestID = quest.Id;
            packet.Count = (ushort)newCount;
            SendPacket(packet);
        }

        public void SendQuestGiverStatusMultiple()
        {
            SendQuestGiverStatusMultiple(m_clientGUIDs);
        }

        public void SendQuestGiverStatusMultiple(List<ObjectGuid> guids)
        {
            QuestGiverStatusMultiple response = new();

            foreach (var itr in guids)
            {
                if (itr.IsAnyTypeCreature())
                {
                    // need also pet quests case support
                    Creature questgiver = ObjectAccessor.GetCreatureOrPetOrVehicle(this, itr);
                    if (questgiver == null)
                        continue;

                    if (!questgiver.HasNpcFlag(NPCFlags1.QuestGiver))
                        continue;

                    response.QuestGiver.Add(new QuestGiverInfo(questgiver.GetGUID(), GetQuestDialogStatus(questgiver)));
                }
                else if (itr.IsGameObject())
                {
                    GameObject questgiver = GetMap().GetGameObject(itr);
                    if (questgiver == null || questgiver.GetGoType() != GameObjectTypes.QuestGiver)
                        continue;

                    response.QuestGiver.Add(new QuestGiverInfo(questgiver.GetGUID(), GetQuestDialogStatus(questgiver)));
                }
            }

            SendPacket(response);
        }

        public bool HasPvPForcingQuest()
        {
            for (byte i = 0; i < SharedConst.MaxQuestLogSize; ++i)
            {
                var questId = GetQuestSlotQuestId(i);
                if (questId == 0)
                    continue;

                Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                if (quest == null)
                    continue;

                if (quest.HasAnyFlag(QuestFlags.Pvp))
                    return true;
            }

            return false;
        }

        public bool HasQuestForGO(int GOId)
        {
            foreach (var objectiveStatusData in m_questObjectiveStatus[(QuestObjectiveType.GameObject, GOId)])
            {
                Quest qInfo = Global.ObjectMgr.GetQuestTemplate(objectiveStatusData.QuestStatusPair.QuestID);
                QuestObjective objective = Global.ObjectMgr.GetQuestObjective(objectiveStatusData.ObjectiveId);
                if (qInfo == null || objective == null || !IsQuestObjectiveCompletable(objectiveStatusData.QuestStatusPair.Status.Slot, qInfo, objective))
                    continue;

                // hide quest if player is in raid-group and quest is no raid quest
                if (GetGroup() != null && GetGroup().IsRaidGroup() && !qInfo.IsAllowedInRaid(GetMap().GetDifficultyID()))
                {
                    if (!InBattleground()) //there are two ways.. we can make every bg-quest a raidquest, or add this code here.. i don't know if this can be exploited by other quests, but i think all other quests depend on a specific area.. but keep this in mind, if something strange happens later
                        continue;
                }

                if (!IsQuestObjectiveComplete(objectiveStatusData.QuestStatusPair.Status.Slot, qInfo, objective))
                    return true;
            }

            return false;
        }

        public void UpdateVisibleObjectInteractions(bool allUnits, bool onlySpellClicks, bool gameObjectQuestGiverStatus, bool questObjectiveGameObjects)
        {
            QuestGiverStatusMultiple giverStatusMultiple = new();
            UpdateData udata = new(GetMapId());
            foreach (var visibleObjectGuid in m_clientGUIDs)
            {
                if (visibleObjectGuid.IsGameObject() && (gameObjectQuestGiverStatus || questObjectiveGameObjects))
                {
                    GameObject gameObject = ObjectAccessor.GetGameObject(this, visibleObjectGuid);
                    if (gameObject == null)
                        continue;

                    if (gameObjectQuestGiverStatus && gameObject.GetGoType() == GameObjectTypes.QuestGiver)
                        giverStatusMultiple.QuestGiver.Add(new QuestGiverInfo(visibleObjectGuid, GetQuestDialogStatus(gameObject)));

                    if (questObjectiveGameObjects)
                    {
                        ObjectFieldData objMask = new();
                        GameObjectFieldData goMask = new();

                        if (m_questObjectiveStatus.ContainsKey((QuestObjectiveType.GameObject, gameObject.GetEntry())))
                            objMask.MarkChanged(gameObject.m_objectData.DynamicFlags);

                        switch (gameObject.GetGoType())
                        {
                            case GameObjectTypes.QuestGiver:
                            case GameObjectTypes.Chest:
                            case GameObjectTypes.Goober:
                            case GameObjectTypes.Generic:
                            case GameObjectTypes.GatheringNode:
                                if (Global.ObjectMgr.IsGameObjectForQuests(gameObject.GetEntry()))
                                    objMask.MarkChanged(gameObject.m_objectData.DynamicFlags);
                                break;
                            default:
                                break;
                        }

                        if (objMask.GetUpdateMask().IsAnySet() || goMask.GetUpdateMask().IsAnySet())
                            gameObject.BuildValuesUpdateForPlayerWithMask(udata, objMask.GetUpdateMask(), goMask.GetUpdateMask(), this);
                    }

                }
                else if (visibleObjectGuid.IsCreatureOrVehicle() && (allUnits || onlySpellClicks))
                {
                    Creature creature = ObjectAccessor.GetCreatureOrPetOrVehicle(this, visibleObjectGuid);
                    if (creature == null)
                        continue;

                    if (allUnits)
                    {
                        ObjectFieldData objMask = new();
                        UnitData unitMask = new();
                        for (int i = 0; i < creature.m_unitData.NpcFlags.GetSize(); ++i)
                        {
                            if (creature.m_unitData.NpcFlags[i] != 0)
                                unitMask.MarkChanged(creature.m_unitData.NpcFlags, i);
                        }

                        if (objMask.GetUpdateMask().IsAnySet() || unitMask.GetUpdateMask().IsAnySet())
                            creature.BuildValuesUpdateForPlayerWithMask(udata, objMask.GetUpdateMask(), unitMask.GetUpdateMask(), this);

                        if (creature.IsQuestGiver())
                            giverStatusMultiple.QuestGiver.Add(new QuestGiverInfo(visibleObjectGuid, GetQuestDialogStatus(creature)));
                    }
                    else if (onlySpellClicks)
                    {
                        // check if this unit requires quest specific flags
                        if (!creature.HasNpcFlag(NPCFlags1.SpellClick))
                            continue;

                        var clickBounds = Global.ObjectMgr.GetSpellClickInfoMapBounds(creature.GetEntry());
                        foreach (var spellClickInfo in clickBounds)
                        {
                            if (Global.ConditionMgr.HasConditionsForSpellClickEvent(creature.GetEntry(), spellClickInfo.spellId))
                            {
                                ObjectFieldData objMask = new();
                                UnitData unitMask = new();
                                unitMask.MarkChanged(m_unitData.NpcFlags, 0); // NpcFlags[0] has UNIT_NPC_FLAG_SPELLCLICK
                                creature.BuildValuesUpdateForPlayerWithMask(udata, objMask.GetUpdateMask(), unitMask.GetUpdateMask(), this);
                                break;
                            }
                        }
                    }
                }
            }

            // If as a result of npcflag updates we stop seeing UNIT_NPC_FLAG_QUESTGIVER then
            // we must also send SMSG_QUEST_GIVER_STATUS_MULTIPLE because client will not request it automatically
            if (!giverStatusMultiple.QuestGiver.Empty())
                SendPacket(giverStatusMultiple);

            if (udata.HasData())
            {
                udata.BuildPacket(out var packet);
                SendPacket(packet);
            }
        }

        void SetDailyQuestStatus(int quest_id)
        {
            Quest qQuest = Global.ObjectMgr.GetQuestTemplate(quest_id);
            if (qQuest != null)
            {
                if (!qQuest.IsDFQuest())
                {
                    AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.DailyQuestsCompleted), quest_id);
                    m_lastDailyQuestTime = LoopTime.ServerTime;              // last daily quest time
                    m_DailyQuestChanged = true;

                }
                else
                {
                    m_DFQuests.Add(quest_id);
                    m_lastDailyQuestTime = LoopTime.ServerTime;
                    m_DailyQuestChanged = true;
                }
            }
        }

        public bool IsDailyQuestDone(int quest_id)
        {
            return m_activePlayerData.DailyQuestsCompleted.FindIndex(quest_id) >= 0;
        }

        void SetWeeklyQuestStatus(int quest_id)
        {
            m_weeklyquests.Add(quest_id);
            m_WeeklyQuestChanged = true;
        }

        void SetSeasonalQuestStatus(int quest_id)
        {
            Quest quest = Global.ObjectMgr.GetQuestTemplate(quest_id);
            if (quest == null)
                return;

            if (!m_seasonalquests.ContainsKey(quest.GetEventIdForQuest()))
                m_seasonalquests[quest.GetEventIdForQuest()] = new();

            m_seasonalquests[quest.GetEventIdForQuest()][quest_id] = LoopTime.RealmTime;
            m_SeasonalQuestChanged = true;
        }

        void SetMonthlyQuestStatus(int quest_id)
        {
            m_monthlyquests.Add(quest_id);
            m_MonthlyQuestChanged = true;
        }

        void PushQuests()
        {
            foreach (Quest quest in Global.ObjectMgr.GetQuestTemplatesAutoPush())
            {
                if (quest.GetQuestTag() != 0 && quest.GetQuestTag() != QuestTagType.Tag)
                    continue;

                if (!quest.IsUnavailable() && CanTakeQuest(quest, false))
                    AddQuestAndCheckCompletion(quest, null);
            }
        }

        void SendDisplayToast(int entry, DisplayToastType type, bool isBonusRoll, int quantity, DisplayToastMethod method, int questId, Item item = null)
        {
            DisplayToast displayToast = new();
            displayToast.Quantity = quantity;
            displayToast.DisplayToastMethod = method;
            displayToast.QuestID = questId;
            displayToast.Type = type;

            switch (type)
            {
                case DisplayToastType.NewItem:
                {
                    if (item == null)
                        return;

                    displayToast.BonusRoll = isBonusRoll;
                    displayToast.Item = new(item);
                    displayToast.LootSpec = 0; // loot spec that was selected when loot was generated (not at loot time)
                    displayToast.Gender = GetNativeGender();
                    break;
                }
                case DisplayToastType.NewCurrency:
                    displayToast.CurrencyID = entry;
                    break;
                default:
                    break;
            }

            SendPacket(displayToast);
        }
    }
}
