﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Achievements;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;

namespace Game
{
    class QuestObjectiveCriteriaManager : CriteriaHandler
    {
        public QuestObjectiveCriteriaManager(Player owner)
        {
            _owner = owner;
        }

        public void CheckAllQuestObjectiveCriteria(Player referencePlayer)
        {
            // suppress sending packets
            for (CriteriaType i = 0; i < CriteriaType.Count; ++i)
                UpdateCriteria(i, 0, 0, 0, null, referencePlayer);
        }

        public override void Reset()
        {
            foreach (var pair in _criteriaProgress)
                SendCriteriaProgressRemoved(pair.Key);

            _criteriaProgress.Clear();

            DeleteFromDB(_owner.GetGUID());

            // re-fill data
            CheckAllQuestObjectiveCriteria(_owner);
        }

        public static void DeleteFromDB(ObjectGuid guid)
        {
            SQLTransaction trans = new();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_OBJECTIVES_CRITERIA);
            stmt.SetInt64(0, guid.GetCounter());
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_OBJECTIVES_CRITERIA_PROGRESS);
            stmt.SetInt64(0, guid.GetCounter());
            trans.Append(stmt);

            DB.Characters.CommitTransaction(trans);
        }

        public void LoadFromDB(SQLResult objectiveResult, SQLResult criteriaResult)
        {
            if (!objectiveResult.IsEmpty())
            {
                do
                {
                    int objectiveId = objectiveResult.Read<int>(0);

                    QuestObjective objective = Global.ObjectMgr.GetQuestObjective(objectiveId);
                    if (objective == null)
                        continue;

                    _completedObjectives.Add(objectiveId);

                } while (objectiveResult.NextRow());
            }

            if (!criteriaResult.IsEmpty())
            {
                ServerTime now = LoopTime.ServerTime;
                do
                {
                    int criteriaId = criteriaResult.Read<int>(0);
                    long counter = criteriaResult.Read<long>(1);
                    ServerTime date = (ServerTime)(UnixTime64)criteriaResult.Read<long>(2);

                    Criteria criteria = Global.CriteriaMgr.GetCriteria(criteriaId);
                    if (criteria == null)
                    {
                        // Removing non-existing criteria data for all characters
                        Log.outError(LogFilter.Player, 
                            $"Non-existing quest objective criteria {criteriaId} data " +
                            $"has been removed from the table `character_queststatus_objectives_criteria_progress`.");

                        PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_INVALID_QUEST_PROGRESS_CRITERIA);
                        stmt.SetInt32(0, criteriaId);
                        DB.Characters.Execute(stmt);

                        continue;
                    }

                    if (criteria.Entry.StartTimer != TimeSpan.Zero && date + criteria.Entry.StartTimer < now)
                        continue;

                    CriteriaProgress progress = new();
                    progress.Counter = counter;
                    progress.Date = date;
                    progress.Changed = false;

                    _criteriaProgress[criteriaId] = progress;
                }
                while (criteriaResult.NextRow());
            }
        }

        public void SaveToDB(SQLTransaction trans)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_OBJECTIVES_CRITERIA);
            stmt.SetInt64(0, _owner.GetGUID().GetCounter());
            trans.Append(stmt);

            if (!_completedObjectives.Empty())
            {
                foreach (var completedObjectiveId in _completedObjectives)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_QUESTSTATUS_OBJECTIVES_CRITERIA);
                    stmt.SetInt64(0, _owner.GetGUID().GetCounter());
                    stmt.SetInt32(1, completedObjectiveId);
                    trans.Append(stmt);
                }
            }

            if (!_criteriaProgress.Empty())
            {
                foreach (var pair in _criteriaProgress)
                {
                    if (!pair.Value.Changed)
                        continue;

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_OBJECTIVES_CRITERIA_PROGRESS_BY_CRITERIA);
                    stmt.SetInt64(0, _owner.GetGUID().GetCounter());
                    stmt.SetInt32(1, pair.Key);
                    trans.Append(stmt);

                    if (pair.Value.Counter != 0)
                    {
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_QUESTSTATUS_OBJECTIVES_CRITERIA_PROGRESS);
                        stmt.SetInt64(0, _owner.GetGUID().GetCounter());
                        stmt.SetInt32(1, pair.Key);
                        stmt.SetInt64(2, pair.Value.Counter);
                        stmt.SetInt64(3, (UnixTime64)pair.Value.Date);
                        trans.Append(stmt);
                    }

                    pair.Value.Changed = false;
                }
            }
        }

        public void ResetCriteriaTree(int criteriaTreeId)
        {
            CriteriaTree tree = Global.CriteriaMgr.GetCriteriaTree(criteriaTreeId);
            if (tree == null)
                return;

            CriteriaManager.WalkCriteriaTree(tree, criteriaTree =>
            {
                RemoveCriteriaProgress(criteriaTree.Criteria);
            });
        }

        public override void SendAllData(Player receiver)
        {
            foreach (var (id, criteriaProgres) in _criteriaProgress)
            {
                CriteriaUpdate criteriaUpdate = new();

                criteriaUpdate.CriteriaID = id;
                criteriaUpdate.Quantity = criteriaProgres.Counter;
                criteriaUpdate.PlayerGUID = _owner.GetGUID();
                criteriaUpdate.Flags = 0;

                criteriaUpdate.CurrentTime = (RealmTime)criteriaProgres.Date;
                //criteriaUpdate.CurrentTime += _owner.GetSession().GetTimezoneOffset();
                criteriaUpdate.CreationTime = 0;

                SendPacket(criteriaUpdate);
            }
        }

        void CompletedObjective(QuestObjective questObjective, Player referencePlayer)
        {
            if (HasCompletedObjective(questObjective))
                return;

            _owner.KillCreditCriteriaTreeObjective(questObjective);

            Log.outInfo(LogFilter.Player, $"QuestObjectiveCriteriaMgr.CompletedObjective({questObjective.Id}). {GetOwnerInfo()}");

            _completedObjectives.Add(questObjective.Id);
        }

        public bool HasCompletedObjective(QuestObjective questObjective)
        {
            return _completedObjectives.Contains(questObjective.Id);
        }

        public override void SendCriteriaUpdate(Criteria criteria, CriteriaProgress progress, TimeSpan timeElapsed, bool timedCompleted)
        {
            CriteriaUpdate criteriaUpdate = new();

            criteriaUpdate.CriteriaID = criteria.Id;
            criteriaUpdate.Quantity = progress.Counter;
            criteriaUpdate.PlayerGUID = _owner.GetGUID();
            criteriaUpdate.Flags = 0;
            if (criteria.Entry.StartTimer != TimeSpan.Zero)
                criteriaUpdate.Flags = timedCompleted ? 1u : 0u; // 1 is for keeping the counter at 0 in client

            criteriaUpdate.CurrentTime = (RealmTime)progress.Date;
            //criteriaUpdate.CurrentTime += _owner.GetSession().GetTimezoneOffset();
            criteriaUpdate.ElapsedTime = (uint)timeElapsed.TotalSeconds;
            criteriaUpdate.CreationTime = 0;

            SendPacket(criteriaUpdate);
        }

        public override void SendCriteriaProgressRemoved(int criteriaId)
        {
            CriteriaDeleted criteriaDeleted = new();
            criteriaDeleted.CriteriaID = criteriaId;
            SendPacket(criteriaDeleted);
        }

        public override bool CanUpdateCriteriaTree(Criteria criteria, CriteriaTree tree, Player referencePlayer)
        {
            QuestObjective objective = tree.QuestObjective;
            if (objective == null)
                return false;

            if (HasCompletedObjective(objective))
            {
                Log.outTrace(LogFilter.Player, 
                    $"QuestObjectiveCriteriaMgr.CanUpdateCriteriaTree: " +
                    $"(Id: {criteria.Id} Type {criteria.Entry.Type} " +
                    $"Quest Objective {objective.Id}) Objective already completed");
                return false;
            }

            if (_owner.GetQuestStatus(objective.QuestID) != QuestStatus.Incomplete)
            {
                Log.outTrace(LogFilter.Achievement, 
                    $"QuestObjectiveCriteriaMgr.CanUpdateCriteriaTree: " +
                    $"(Id: {criteria.Id} Type {criteria.Entry.Type} " +
                    $"Quest Objective {objective.Id}) Not on quest");
                return false;
            }

            Quest quest = Global.ObjectMgr.GetQuestTemplate(objective.QuestID);
            if (_owner.GetGroup() != null && _owner.GetGroup().IsRaidGroup() && !quest.IsAllowedInRaid(referencePlayer.GetMap().GetDifficultyID()))
            {
                Log.outTrace(LogFilter.Achievement, 
                    $"QuestObjectiveCriteriaMgr.CanUpdateCriteriaTree: " +
                    $"(Id: {criteria.Id} Type {criteria.Entry.Type} " +
                    $"Quest Objective {objective.Id}) Quest cannot be completed in raid group");
                return false;
            }

            ushort slot = _owner.FindQuestSlot(objective.QuestID);
            if (slot >= SharedConst.MaxQuestLogSize || !_owner.IsQuestObjectiveCompletable(slot, quest, objective))
            {
                Log.outTrace(LogFilter.Achievement, 
                    $"QuestObjectiveCriteriaMgr.CanUpdateCriteriaTree: " +
                    $"(Id: {criteria.Id} Type {criteria.Entry.Type} " +
                    $"Quest Objective {objective.Id}) Objective not completable");
                return false;
            }

            return base.CanUpdateCriteriaTree(criteria, tree, referencePlayer);
        }

        public override bool CanCompleteCriteriaTree(CriteriaTree tree)
        {
            QuestObjective objective = tree.QuestObjective;
            if (objective == null)
                return false;

            return base.CanCompleteCriteriaTree(tree);
        }

        public override void CompletedCriteriaTree(CriteriaTree tree, Player referencePlayer)
        {
            QuestObjective objective = tree.QuestObjective;
            if (objective == null)
                return;

            CompletedObjective(objective, referencePlayer);
        }

        public override void SendPacket(ServerPacket data)
        {
            _owner.SendPacket(data);
        }

        public override string GetOwnerInfo()
        {
            return $"{_owner.GetGUID()} {_owner.GetName()}";
        }

        public override IReadOnlyList<Criteria> GetCriteriaByType(CriteriaType type, int asset)
        {
            return Global.CriteriaMgr.GetQuestObjectiveCriteriaByType(type);
        }

        public override bool RequiredAchievementSatisfied(int achievementId)
        {
            return _owner.HasAchieved(achievementId);
        }

        Player _owner;
        List<int> _completedObjectives = new();
    }
}
