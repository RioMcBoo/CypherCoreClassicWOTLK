﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Configuration;
using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using Game.Groups;
using Game.Maps;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.DungeonFinding
{
    public class LFGManager : Singleton<LFGManager>
    {
        LFGManager()
        {
            m_lfgProposalId = 1;
            m_options = (LfgOptions)ConfigMgr.GetDefaultValue("DungeonFinder.OptionsMask", 1);

            new LFGPlayerScript();
            new LFGGroupScript();
        }

        public string ConcatenateDungeons(List<int> dungeons)
        {
            StringBuilder dungeonstr = new();
            if (!dungeons.Empty())
            {
                foreach (var id in dungeons)
                {
                    if (dungeonstr.Capacity != 0)
                        dungeonstr.AppendFormat(", {0}", id);
                    else
                        dungeonstr.AppendFormat("{0}", id);
                }
            }
            return dungeonstr.ToString();
        }

        public void _LoadFromDB(SQLFields field, ObjectGuid guid)
        {
            if (field == null)
                return;

            if (!guid.IsParty())
                return;

            SetLeader(guid, ObjectGuid.Create(HighGuid.Player, field.Read<long>(0)));

            int dungeon = field.Read<int>(18);
            LfgState state = (LfgState)field.Read<byte>(19);

            if (dungeon == 0 || state == 0)
                return;

            SetDungeon(guid, dungeon);

            switch (state)
            {
                case LfgState.Dungeon:
                case LfgState.FinishedDungeon:
                    SetState(guid, state);
                    break;
                default:
                    break;
            }
        }

        void _SaveToDB(ObjectGuid guid, int db_guid)
        {
            if (!guid.IsParty())
                return;

            SQLTransaction trans = new();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_LFG_DATA);
            stmt.SetInt32(0, db_guid);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_LFG_DATA);
            stmt.SetInt32(0, db_guid);
            stmt.SetInt32(1, GetDungeon(guid));
            stmt.SetUInt32(2, (uint)GetState(guid));
            trans.Append(stmt);

            DB.Characters.CommitTransaction(trans);
        }

        public void LoadRewards()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            RewardMapStore.Clear();

            // ORDER BY is very important for GetRandomDungeonReward!
            SQLResult result = DB.World.Query("SELECT dungeonId, maxLevel, firstQuestId, otherQuestId FROM lfg_dungeon_rewards ORDER BY dungeonId, maxLevel ASC");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 lfg dungeon rewards. DB table `lfg_dungeon_rewards` is empty!");
                return;
            }

            uint count = 0;

            do
            {
                int dungeonId = result.Read<int>(0);
                int maxLevel = result.Read<byte>(1);
                int firstQuestId = result.Read<int>(2);
                int otherQuestId = result.Read<int>(3);

                if (GetLFGDungeonEntry(dungeonId) == 0)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Dungeon {dungeonId} specified in table `lfg_dungeon_rewards` does not exist!");
                    continue;
                }

                if (maxLevel == 0 || maxLevel > WorldConfig.Values[WorldCfg.MaxPlayerLevel].Int32)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Level {maxLevel} specified for dungeon {dungeonId} " +
                        $"in table `lfg_dungeon_rewards` can never be reached!");

                    maxLevel = WorldConfig.Values[WorldCfg.MaxPlayerLevel].Int32;
                }

                if (firstQuestId == 0 || Global.ObjectMgr.GetQuestTemplate(firstQuestId) == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"First quest {firstQuestId} specified for dungeon {dungeonId} " +
                        $"in table `lfg_dungeon_rewards` does not exist!");
                    continue;
                }

                if (otherQuestId != 0 && Global.ObjectMgr.GetQuestTemplate(otherQuestId) == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Other quest {otherQuestId} specified for dungeon {dungeonId} " +
                        $"in table `lfg_dungeon_rewards` does not exist!");

                    otherQuestId = 0;
                }

                RewardMapStore.Add(dungeonId, new LfgReward(maxLevel, firstQuestId, otherQuestId));
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {count} lfg dungeon rewards in {Time.Diff(oldMSTime)} ms.");
        }

        LFGDungeonData GetLFGDungeon(int id)
        {
            return LfgDungeonStore.LookupByKey(id);
        }

        public void LoadLFGDungeons(bool reload = false)
        {
            RelativeTime oldMSTime = Time.NowRelative;

            LfgDungeonStore.Clear();

            // Initialize Dungeon map with data from dbcs
            foreach (var dungeon in CliDB.LFGDungeonsStorage.Values)
            {
                if (Global.DB2Mgr.GetMapDifficultyData(dungeon.MapID, dungeon.DifficultyID) == null)
                    continue;

                switch (dungeon.TypeID)
                {
                    case LfgType.Dungeon:
                    case LfgType.Raid:
                    case LfgType.Random:
                    case LfgType.Zone:
                        LfgDungeonStore[dungeon.Id] = new LFGDungeonData(dungeon);
                        break;
                }
            }

            // Fill teleport locations from DB
            SQLResult result = DB.World.Query("SELECT dungeonId, position_x, position_y, position_z, orientation, requiredItemLevel FROM lfg_dungeon_template");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 lfg dungeon templates. DB table `lfg_dungeon_template` is empty!");
                return;
            }

            uint count = 0;

            do
            {
                int dungeonId = result.Read<int>(0);
                if (!LfgDungeonStore.ContainsKey(dungeonId))
                {
                    Log.outError(LogFilter.Sql, 
                        $"table `lfg_entrances` contains coordinates for wrong dungeon {dungeonId}");
                    continue;
                }

                var data = LfgDungeonStore[dungeonId];
                data.x = result.Read<float>(1);
                data.y = result.Read<float>(2);
                data.z = result.Read<float>(3);
                data.o = result.Read<float>(4);
                data.requiredItemLevel = result.Read<ushort>(5);

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading,
                $"Loaded {count} lfg dungeon templates in {Time.Diff(oldMSTime)} ms.");

            // Fill all other teleport coords from areatriggers
            foreach (var pair in LfgDungeonStore)
            {
                LFGDungeonData dungeon = pair.Value;

                // No teleport coords in database, load from areatriggers
                if (dungeon.type != LfgType.Random && dungeon.x == 0.0f && dungeon.y == 0.0f && dungeon.z == 0.0f)
                {
                    AreaTriggerStruct at = Global.ObjectMgr.GetMapEntranceTrigger(dungeon.map);
                    if (at == null)
                    {
                        Log.outError(LogFilter.Lfg, 
                            $"LoadLFGDungeons: Failed to load dungeon {dungeon.name} (Id: {dungeon.id}), " +
                            $"cant find areatrigger for map {dungeon.map}");
                        continue;
                    }

                    dungeon.map = at.target_mapId;
                    dungeon.x = at.target_X;
                    dungeon.y = at.target_Y;
                    dungeon.z = at.target_Z;
                    dungeon.o = at.target_Orientation;
                }

                if (dungeon.type != LfgType.Random)
                    CachedDungeonMapStore.Add((byte)dungeon.group, dungeon.id);
                CachedDungeonMapStore.Add(0, dungeon.id);
            }

            if (reload)
                CachedDungeonMapStore.Clear();
        }

        public void Update(TimeSpan diff)
        {
            if (!IsOptionEnabled(LfgOptions.EnableDungeonFinder | LfgOptions.EnableRaidBrowser))
                return;

            ServerTime currTime = LoopTime.ServerTime;

            // Remove obsolete role checks
            foreach (var pairCheck in RoleChecksStore)
            {
                LfgRoleCheck roleCheck = pairCheck.Value;
                if (currTime < roleCheck.cancelTime)
                    continue;
                roleCheck.state = LfgRoleCheckState.MissingRole;

                foreach (var pairRole in roleCheck.roles)
                {
                    ObjectGuid guid = pairRole.Key;
                    RestoreState(guid, "Remove Obsolete RoleCheck");
                    SendLfgRoleCheckUpdate(guid, roleCheck);
                    if (guid == roleCheck.leader)
                        SendLfgJoinResult(guid, new LfgJoinResultData(LfgJoinResult.RoleCheckFailed, LfgRoleCheckState.MissingRole));
                }

                RestoreState(pairCheck.Key, "Remove Obsolete RoleCheck");
                RoleChecksStore.Remove(pairCheck.Key);
            }

            // Remove obsolete proposals
            foreach (var removePair in ProposalsStore.ToList())
            {
                if (removePair.Value.cancelTime < currTime)
                    RemoveProposal(removePair, LfgUpdateType.ProposalFailed);
            }

            // Remove obsolete kicks
            foreach (var itBoot in BootsStore)
            {
                LfgPlayerBoot boot = itBoot.Value;
                if (boot.cancelTime < currTime)
                {
                    boot.inProgress = false;
                    foreach (var itVotes in boot.votes)
                    {
                        ObjectGuid pguid = itVotes.Key;
                        if (pguid != boot.victim)
                            SendLfgBootProposalUpdate(pguid, boot);
                    }
                    SetVoteKick(itBoot.Key, false);
                    BootsStore.Remove(itBoot.Key);
                }
            }

            int lastProposalId = m_lfgProposalId;
            // Check if a proposal can be formed with the new groups being added
            foreach (var it in QueuesStore)
            {
                byte newProposals = it.Value.FindGroups();
                if (newProposals != 0)
                {
                    Log.outDebug(LogFilter.Lfg, 
                        $"Update: Found {newProposals} new groups in queue {it.Key}");
                }
            }

            if (lastProposalId != m_lfgProposalId)
            {
                // FIXME lastProposalId ? lastProposalId +1 ?
                foreach (var itProposal in ProposalsStore.SkipWhile(p => p.Key == m_lfgProposalId))
                {
                    int proposalId = itProposal.Key;
                    LfgProposal proposal = ProposalsStore[proposalId];

                    ObjectGuid guid = ObjectGuid.Empty;
                    foreach (var itPlayers in proposal.players)
                    {
                        guid = itPlayers.Key;
                        SetState(guid, LfgState.Proposal);
                        ObjectGuid gguid = GetGroup(guid);
                        if (!gguid.IsEmpty())
                        {
                            SetState(gguid, LfgState.Proposal);
                            SendLfgUpdateStatus(guid, new LfgUpdateData(LfgUpdateType.ProposalBegin, GetSelectedDungeons(guid)), true);
                        }
                        else
                            SendLfgUpdateStatus(guid, new LfgUpdateData(LfgUpdateType.ProposalBegin, GetSelectedDungeons(guid)), false);
                        SendLfgUpdateProposal(guid, proposal);
                    }

                    if (proposal.state == LfgProposalState.Success)
                        UpdateProposal(proposalId, guid, true);
                }
            }

            // Update all players status queue info
            if (m_QueueTimer > SharedConst.LFGQueueUpdateInterval)
            {
                m_QueueTimer = TimeSpan.Zero;
                foreach (var it in QueuesStore)
                    it.Value.UpdateQueueTimers(it.Key, currTime);
            }
            else
                m_QueueTimer += diff;
        }

        public void JoinLfg(Player player, LfgRoles roles, List<int> dungeons)
        {
            if (player == null || player.GetSession() == null || dungeons.Empty())
                return;

            // Sanitize input roles
            roles &= LfgRoles.Any;
            roles = FilterClassRoles(player, roles);

            // At least 1 role must be selected
            if ((roles & (LfgRoles.Tank | LfgRoles.Healer | LfgRoles.Damage)) == 0)
                return;

            Group grp = player.GetGroup();
            ObjectGuid guid = player.GetGUID();
            ObjectGuid gguid = grp != null ? grp.GetGUID() : guid;
            LfgJoinResultData joinData = new();
            List<ObjectGuid> players = new();
            int rDungeonId = 0;
            bool isContinue = grp != null && grp.IsLFGGroup() && GetState(gguid) != LfgState.FinishedDungeon;

            // Do not allow to change dungeon in the middle of a current dungeon
            if (isContinue)
            {
                dungeons.Clear();
                dungeons.Add(GetDungeon(gguid));
            }

            // Already in queue?
            LfgState state = GetState(gguid);
            if (state == LfgState.Queued)
            {
                LFGQueue queue = GetQueue(gguid);
                queue.RemoveFromQueue(gguid);
            }

            // Check player or group member restrictions
            if (!player.GetSession().HasPermission(RBACPermissions.JoinDungeonFinder))
                joinData.result = LfgJoinResult.NoSlots;
            else if (player.InBattleground() || player.InArena() || player.InBattlegroundQueue())
                joinData.result = LfgJoinResult.CantUseDungeons;
            else if (player.HasAura(SharedConst.LFGSpellDungeonDeserter))
                joinData.result = LfgJoinResult.DeserterPlayer;
            else if (!isContinue && player.HasAura(SharedConst.LFGSpellDungeonCooldown))
                joinData.result = LfgJoinResult.RandomCooldownPlayer;
            else if (dungeons.Empty())
                joinData.result = LfgJoinResult.NoSlots;
            else if (player.HasAura(9454)) // check Freeze debuff
                joinData.result = LfgJoinResult.NoSlots;
            else if (grp != null)
            {
                if (grp.GetMembersCount() > MapConst.MaxGroupSize)
                    joinData.result = LfgJoinResult.TooManyMembers;
                else
                {
                    byte memberCount = 0;
                    for (GroupReference refe = grp.GetFirstMember(); refe != null && joinData.result == LfgJoinResult.Ok; refe = refe.Next())
                    {
                        Player groupPlayer = refe.GetSource();
                        if (groupPlayer != null)
                        {
                            if (!groupPlayer.GetSession().HasPermission(RBACPermissions.JoinDungeonFinder))
                                joinData.result = LfgJoinResult.NoLfgObject;
                            if (groupPlayer.HasAura(SharedConst.LFGSpellDungeonDeserter))
                                joinData.result = LfgJoinResult.DeserterParty;
                            else if (!isContinue && groupPlayer.HasAura(SharedConst.LFGSpellDungeonCooldown))
                                joinData.result = LfgJoinResult.RandomCooldownParty;
                            else if (groupPlayer.InBattleground() || groupPlayer.InArena() || groupPlayer.InBattlegroundQueue())
                                joinData.result = LfgJoinResult.CantUseDungeons;
                            else if (groupPlayer.HasAura(9454)) // check Freeze debuff
                            {
                                joinData.result = LfgJoinResult.NoSlots;
                                joinData.playersMissingRequirement.Add(groupPlayer.GetName());
                            }
                            ++memberCount;
                            players.Add(groupPlayer.GetGUID());
                        }
                    }

                    if (joinData.result == LfgJoinResult.Ok && memberCount != grp.GetMembersCount())
                        joinData.result = LfgJoinResult.MembersNotPresent;
                }
            }
            else
                players.Add(player.GetGUID());

            // Check if all dungeons are valid
            bool isRaid = false;
            if (joinData.result == LfgJoinResult.Ok)
            {
                bool isDungeon = false;
                foreach (var it in dungeons)
                {
                    if (joinData.result != LfgJoinResult.Ok)
                        break;

                    LfgType type = GetDungeonType(it);
                    switch (type)
                    {
                        case LfgType.Random:
                            if (dungeons.Count > 1)               // Only allow 1 random dungeon
                                joinData.result = LfgJoinResult.InvalidSlot;
                            else
                                rDungeonId = dungeons.First();
                            goto case LfgType.Dungeon;
                        case LfgType.Dungeon:
                            if (isRaid)
                                joinData.result = LfgJoinResult.MismatchedSlots;
                            isDungeon = true;
                            break;
                        case LfgType.Raid:
                            if (isDungeon)
                                joinData.result = LfgJoinResult.MismatchedSlots;
                            isRaid = true;
                            break;
                        default:
                            Log.outError(LogFilter.Lfg, $"Wrong dungeon Type {type} for dungeon {it}");
                            joinData.result = LfgJoinResult.InvalidSlot;
                            break;
                    }
                }

                // it could be changed
                if (joinData.result == LfgJoinResult.Ok)
                {
                    // Expand random dungeons and check restrictions
                    if (rDungeonId != 0)
                        dungeons = GetDungeonsByRandom(rDungeonId).ToList();

                    // if we have lockmap then there are no compatible dungeons
                    GetCompatibleDungeons(dungeons, players, joinData.lockmap, joinData.playersMissingRequirement, isContinue);
                    if (dungeons.Empty())
                        joinData.result = LfgJoinResult.NoSlots;
                }
            }

            // Can't join. Send result
            if (joinData.result != LfgJoinResult.Ok)
            {
                Log.outDebug(LogFilter.Lfg, 
                    $"Join: [{guid}] joining with {(grp != null ? grp.GetMembersCount() : 1)} members. " +
                    $"Result: {joinData.result}");

                if (!dungeons.Empty())                             // Only should show lockmap when have no dungeons available
                    joinData.lockmap.Clear();
                player.GetSession().SendLfgJoinResult(joinData);
                return;
            }

            if (isRaid)
            {
                Log.outDebug(LogFilter.Lfg, 
                    $"Join: [{guid}] trying to join raid browser and it's disabled.");
                return;
            }

            RideTicket ticket = new();
            ticket.RequesterGuid = guid;
            ticket.Id = GetQueueId(gguid);
            ticket.Type = RideType.Lfg;
            ticket.JoinTime = LoopTime.ServerTime;

            string debugNames = "";
            if (grp != null)                                               // Begin rolecheck
            {
                // Create new rolecheck
                LfgRoleCheck roleCheck = new();
                roleCheck.cancelTime = LoopTime.ServerTime + SharedConst.LFGTimeRolecheck;
                roleCheck.state = LfgRoleCheckState.Initialiting;
                roleCheck.leader = guid;
                roleCheck.dungeons = dungeons;
                roleCheck.rDungeonId = rDungeonId;

                RoleChecksStore[gguid] = roleCheck;

                if (rDungeonId != 0)
                {
                    dungeons.Clear();
                    dungeons.Add(rDungeonId);
                }

                SetState(gguid, LfgState.Rolecheck);
                // Send update to player
                LfgUpdateData updateData = new(LfgUpdateType.JoinQueue, dungeons);
                for (GroupReference refe = grp.GetFirstMember(); refe != null; refe = refe.Next())
                {
                    Player plrg = refe.GetSource();
                    if (plrg != null)
                    {
                        ObjectGuid pguid = plrg.GetGUID();
                        plrg.GetSession().SendLfgUpdateStatus(updateData, true);
                        SetState(pguid, LfgState.Rolecheck);
                        SetTicket(pguid, ticket);
                        if (!isContinue)
                            SetSelectedDungeons(pguid, dungeons);
                        roleCheck.roles[pguid] = 0;
                        if (!string.IsNullOrEmpty(debugNames))
                            debugNames += ", ";
                        debugNames += plrg.GetName();
                    }
                }
                // Update leader role
                UpdateRoleCheck(gguid, guid, roles);
            }
            else                                                   // Add player to queue
            {
                Dictionary<ObjectGuid, LfgRoles> rolesMap = new();
                rolesMap[guid] = roles;
                LFGQueue queue = GetQueue(guid);
                queue.AddQueueData(guid, LoopTime.ServerTime, dungeons, rolesMap);

                if (!isContinue)
                {
                    if (rDungeonId != 0)
                    {
                        dungeons.Clear();
                        dungeons.Add(rDungeonId);
                    }
                    SetSelectedDungeons(guid, dungeons);
                }
                // Send update to player
                SetTicket(guid, ticket);
                SetRoles(guid, roles);
                player.GetSession().SendLfgUpdateStatus(new LfgUpdateData(LfgUpdateType.JoinQueueInitial, dungeons), false);
                SetState(gguid, LfgState.Queued);
                player.GetSession().SendLfgUpdateStatus(new LfgUpdateData(LfgUpdateType.AddedToQueue, dungeons), false);
                player.GetSession().SendLfgJoinResult(joinData);
                debugNames += player.GetName();
            }
            StringBuilder o = new();

            o.AppendFormat(
                $"Join: [{guid}] joined ({(grp != null ? "group" : "player")}{debugNames}) " +
                $"Members: {dungeons.Count}. " +
                $"Dungeons ({ConcatenateDungeons(dungeons)}): ");
            
            Log.outDebug(LogFilter.Lfg, o.ToString());
        }

        public void LeaveLfg(ObjectGuid guid, bool disconnected = false)
        {
            Log.outDebug(LogFilter.Lfg, $"LeaveLfg: [{guid}]");

            ObjectGuid gguid = guid.IsParty() ? guid : GetGroup(guid);
            LfgState state = GetState(guid);
            switch (state)
            {
                case LfgState.Queued:
                    if (!gguid.IsEmpty())
                    {
                        LfgState newState = LfgState.None;
                        LfgState oldState = GetOldState(gguid);

                        // Set the new state to LFG_STATE_DUNGEON/LFG_STATE_FINISHED_DUNGEON if the group is already in a dungeon
                        // This is required in case a LFG group vote-kicks a player in a dungeon, queues, then leaves the queue (maybe to queue later again)
                        Group group = Global.GroupMgr.GetGroupByGUID(gguid);
                        if (group != null)
                            if (group.IsLFGGroup() && GetDungeon(gguid) != 0 && (oldState == LfgState.Dungeon || oldState == LfgState.FinishedDungeon))
                                newState = oldState;

                        LFGQueue queue = GetQueue(gguid);
                        queue.RemoveFromQueue(gguid);
                        SetState(gguid, newState);
                        List<ObjectGuid> players = GetPlayers(gguid);
                        foreach (var it in players)
                        {
                            SetState(it, newState);
                            SendLfgUpdateStatus(it, new LfgUpdateData(LfgUpdateType.RemovedFromQueue), true);
                        }
                    }
                    else
                    {
                        SendLfgUpdateStatus(guid, new LfgUpdateData(LfgUpdateType.RemovedFromQueue), false);
                        LFGQueue queue = GetQueue(guid);
                        queue.RemoveFromQueue(guid);
                        SetState(guid, LfgState.None);
                    }
                    break;
                case LfgState.Rolecheck:
                    if (!gguid.IsEmpty())
                        UpdateRoleCheck(gguid);                    // No player to update role = LFG_ROLECHECK_ABORTED
                    break;
                case LfgState.Proposal:
                    {
                        // Remove from Proposals
                        KeyValuePair<int, LfgProposal> it = new();
                        ObjectGuid pguid = gguid == guid ? GetLeader(gguid) : guid;
                        foreach (var test in ProposalsStore)
                        {
                            it = test;
                            var itPlayer = it.Value.players.LookupByKey(pguid);
                            if (itPlayer != null)
                            {
                                // Mark the player/leader of group who left as didn't accept the proposal
                                itPlayer.accept = LfgAnswer.Deny;
                                break;
                            }
                        }

                        // Remove from queue - if proposal is found, RemoveProposal will call RemoveFromQueue
                        if (it.Value != null)
                            RemoveProposal(it, LfgUpdateType.ProposalDeclined);
                        break;
                    }
                case LfgState.None:
                case LfgState.Raidbrowser:
                    break;
                case LfgState.Dungeon:
                case LfgState.FinishedDungeon:
                    if (guid != gguid && !disconnected) // Player
                        SetState(guid, LfgState.None);
                    break;
            }
        }

        public RideTicket GetTicket(ObjectGuid guid)
        {
            var palyerData = PlayersStore.LookupByKey(guid);
            if (palyerData != null)
                return palyerData.GetTicket();

            return null;
        }

        public void UpdateRoleCheck(ObjectGuid gguid, ObjectGuid guid = default, LfgRoles roles = LfgRoles.None)
        {
            if (gguid.IsEmpty())
                return;

            Dictionary<ObjectGuid, LfgRoles> check_roles;
            var roleCheck = RoleChecksStore.LookupByKey(gguid);
            if (roleCheck == null)
                return;

            // Sanitize input roles
            roles &= LfgRoles.Any;

            if (!guid.IsEmpty())
            {
                Player player = Global.ObjAccessor.FindPlayer(guid);
                if (player != null)
                    roles = FilterClassRoles(player, roles);
                else
                    return;
            }

            bool sendRoleChosen = roleCheck.state != LfgRoleCheckState.Default && !guid.IsEmpty();

            if (guid.IsEmpty())
                roleCheck.state = LfgRoleCheckState.Aborted;
            else if (roles < LfgRoles.Tank)                            // Player selected no role.
                roleCheck.state = LfgRoleCheckState.NoRole;
            else
            {
                roleCheck.roles[guid] = roles;

                // Check if all players have selected a role
                bool done = false;
                foreach (var rolePair in roleCheck.roles)
                {
                    if (rolePair.Value != LfgRoles.None)
                        continue;
                    done = true;
                }

                if (done)
                {
                    // use temporal var to check roles, CheckGroupRoles modifies the roles
                    check_roles = roleCheck.roles;
                    roleCheck.state = CheckGroupRoles(check_roles) ? LfgRoleCheckState.Finished : LfgRoleCheckState.WrongRoles;
                }
            }

            List<int> dungeons = new();
            if (roleCheck.rDungeonId != 0)
                dungeons.Add(roleCheck.rDungeonId);
            else
                dungeons = roleCheck.dungeons;

            LfgJoinResultData joinData = new(LfgJoinResult.RoleCheckFailed, roleCheck.state);
            foreach (var it in roleCheck.roles)
            {
                ObjectGuid pguid = it.Key;

                if (sendRoleChosen)
                    SendLfgRoleChosen(pguid, guid, roles);

                SendLfgRoleCheckUpdate(pguid, roleCheck);
                switch (roleCheck.state)
                {
                    case LfgRoleCheckState.Initialiting:
                        continue;
                    case LfgRoleCheckState.Finished:
                        SetState(pguid, LfgState.Queued);
                        SetRoles(pguid, it.Value);
                        SendLfgUpdateStatus(pguid, new LfgUpdateData(LfgUpdateType.AddedToQueue, dungeons), true);
                        break;
                    default:
                        if (roleCheck.leader == pguid)
                            SendLfgJoinResult(pguid, joinData);
                        SendLfgUpdateStatus(pguid, new LfgUpdateData(LfgUpdateType.RolecheckFailed), true);
                        RestoreState(pguid, "Rolecheck Failed");
                        break;
                }
            }

            if (roleCheck.state == LfgRoleCheckState.Finished)
            {
                SetState(gguid, LfgState.Queued);
                LFGQueue queue = GetQueue(gguid);
                queue.AddQueueData(gguid, LoopTime.ServerTime, roleCheck.dungeons, roleCheck.roles);
                RoleChecksStore.Remove(gguid);
            }
            else if (roleCheck.state != LfgRoleCheckState.Initialiting)
            {
                RestoreState(gguid, "Rolecheck Failed");
                RoleChecksStore.Remove(gguid);
            }
        }

        void GetCompatibleDungeons(List<int> dungeons, List<ObjectGuid> players, Dictionary<ObjectGuid, Dictionary<int, LfgLockInfoData>> lockMap, List<string> playersMissingRequirement, bool isContinue)
        {
            lockMap.Clear();
            Dictionary<int, int> lockedDungeons = new();
            List<int> dungeonsToRemove = new();

            foreach (var guid in players)
            {
                if (dungeons.Empty())
                    break;

                var cachedLockMap = GetLockedDungeons(guid);
                Player player = Global.ObjAccessor.FindConnectedPlayer(guid);
                foreach (var it2 in cachedLockMap)
                {
                    if (dungeons.Empty())
                        break;

                    int dungeonId = (it2.Key & 0x00FFFFFF); // Compare dungeon ids
                    if (dungeons.Contains(dungeonId))
                    {
                        bool eraseDungeon = true;

                        // Don't remove the dungeon if team members are trying to continue a locked instance
                        if (it2.Value.lockStatus == LfgLockStatusType.RaidLocked && isContinue)
                        {
                            LFGDungeonData dungeon = GetLFGDungeon(dungeonId);
                            Cypher.Assert(dungeon != null);
                            Cypher.Assert(player != null);
                            MapDb2Entries entries = new(dungeon.map, dungeon.difficulty);
                            InstanceLock playerBind = Global.InstanceLockMgr.FindActiveInstanceLock(guid, entries);
                            if (playerBind != null)
                            {
                                int dungeonInstanceId = playerBind.GetInstanceId();
                                if (!lockedDungeons.TryGetValue(dungeonId, out int lockedDungeon) || lockedDungeon == dungeonInstanceId)
                                    eraseDungeon = false;

                                lockedDungeons[dungeonId] = dungeonInstanceId;
                            }
                        }

                        if (eraseDungeon)
                            dungeonsToRemove.Add(dungeonId);

                        if (!lockMap.ContainsKey(guid))
                            lockMap[guid] = new Dictionary<int, LfgLockInfoData>();

                        lockMap[guid][it2.Key] = it2.Value;
                        playersMissingRequirement.Add(player.GetName());
                    }
                }
            }

            foreach (var dungeonIdToRemove in dungeonsToRemove)
                dungeons.Remove(dungeonIdToRemove);

            if (!dungeons.Empty())
                lockMap.Clear();
        }

        public bool CheckGroupRoles(Dictionary<ObjectGuid, LfgRoles> groles)
        {
            if (groles.Empty())
                return false;

            byte damage = 0;
            byte tank = 0;
            byte healer = 0;

            List<ObjectGuid> keys = new(groles.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                LfgRoles role = groles[keys[i]] & ~LfgRoles.Leader;
                if (role == LfgRoles.None)
                    return false;

                if (role.HasAnyFlag(LfgRoles.Damage))
                {
                    if (role != LfgRoles.Damage)
                    {
                        groles[keys[i]] -= LfgRoles.Damage;
                        if (CheckGroupRoles(groles))
                            return true;

                        groles[keys[i]] += (byte)LfgRoles.Damage;
                    }
                    else if (damage == SharedConst.LFGDPSNeeded)
                        return false;
                    else
                        damage++;
                }

                if (role.HasAnyFlag(LfgRoles.Healer))
                {
                    if (role != LfgRoles.Healer)
                    {
                        groles[keys[i]] -= LfgRoles.Healer;
                        if (CheckGroupRoles(groles))
                            return true;

                        groles[keys[i]] += (byte)LfgRoles.Healer;
                    }
                    else if (healer == SharedConst.LFGHealersNeeded)
                        return false;
                    else
                        healer++;
                }

                if (role.HasAnyFlag(LfgRoles.Tank))
                {
                    if (role != LfgRoles.Tank)
                    {
                        groles[keys[i]] -= LfgRoles.Tank;
                        if (CheckGroupRoles(groles))
                            return true;

                        groles[keys[i]] += (byte)LfgRoles.Tank;
                    }
                    else if (tank == SharedConst.LFGTanksNeeded)
                        return false;
                    else
                        tank++;
                }
            }
            return (tank + healer + damage) == (byte)groles.Count;
        }

        void MakeNewGroup(LfgProposal proposal)
        {
            List<ObjectGuid> players = new();
            List<ObjectGuid> tankPlayers = new();
            List<ObjectGuid> healPlayers = new();
            List<ObjectGuid> dpsPlayers = new();
            List<ObjectGuid> playersToTeleport = new();

            foreach (var it in proposal.players)
            {
                ObjectGuid guid = it.Key;
                if (guid == proposal.leader)
                    players.Add(guid);
                else
                {
                    switch (it.Value.role & ~LfgRoles.Leader)
                    {
                        case LfgRoles.Tank:
                            tankPlayers.Add(guid);
                            break;
                        case LfgRoles.Healer:
                            healPlayers.Add(guid);
                            break;
                        case LfgRoles.Damage:
                            dpsPlayers.Add(guid);
                            break;
                        default:
                            Cypher.Assert(false, $"Invalid LFG role {it.Value.role}");
                            break;
                    }
                }
                if (proposal.isNew || GetGroup(guid) != proposal.group)
                    playersToTeleport.Add(guid);
            }

            players.AddRange(tankPlayers);
            players.AddRange(healPlayers);
            players.AddRange(dpsPlayers);

            // Set the dungeon difficulty
            LFGDungeonData dungeon = GetLFGDungeon(proposal.dungeonId);
            Cypher.Assert(dungeon != null);

            Group grp = !proposal.group.IsEmpty() ? Global.GroupMgr.GetGroupByGUID(proposal.group) : null;
            foreach (var pguid in players)
            {
                Player player = Global.ObjAccessor.FindConnectedPlayer(pguid);
                if (player == null)
                    continue;

                Group group = player.GetGroup();
                if (group != null && group != grp)
                    group.RemoveMember(player.GetGUID());

                if (grp == null)
                {
                    grp = new Group();
                    grp.ConvertToLFG();
                    grp.Create(player);
                    ObjectGuid gguid = grp.GetGUID();
                    SetState(gguid, LfgState.Proposal);
                    Global.GroupMgr.AddGroup(grp);
                }
                else if (group != grp)
                    grp.AddMember(player);

                grp.SetLfgRoles(pguid, proposal.players.LookupByKey(pguid).role);

                // Add the cooldown spell if queued for a random dungeon
                var dungeons = GetSelectedDungeons(player.GetGUID());
                if (!dungeons.Empty())
                {
                    int rDungeonId = dungeons[0];
                    LFGDungeonData rDungeon = GetLFGDungeon(rDungeonId);
                    if (rDungeon != null && rDungeon.type == LfgType.Random)
                        player.CastSpell(player, SharedConst.LFGSpellDungeonCooldown, false);
                }
            }

            grp.SetDungeonDifficultyID(dungeon.difficulty);
            ObjectGuid _guid = grp.GetGUID();
            SetDungeon(_guid, dungeon.Entry());
            SetState(_guid, LfgState.Dungeon);

            _SaveToDB(_guid, grp.GetDbStoreId());

            // Teleport Player
            foreach (var it in playersToTeleport)
            {
                Player player = Global.ObjAccessor.FindPlayer(it);
                if (player != null)
                    TeleportPlayer(player, false);
            }

            // Update group info
            grp.SendUpdate();
        }

        public int AddProposal(LfgProposal proposal)
        {
            proposal.id = ++m_lfgProposalId;
            ProposalsStore[m_lfgProposalId] = proposal;
            return m_lfgProposalId;
        }

        public void UpdateProposal(int proposalId, ObjectGuid guid, bool accept)
        {
            // Check if the proposal exists
            var proposal = ProposalsStore.LookupByKey(proposalId);
            if (proposal == null)
                return;

            // Check if proposal have the current player
            var player = proposal.players.LookupByKey(guid);
            if (player == null)
                return;

            player.accept = (LfgAnswer)Convert.ToInt32(accept);

            Log.outDebug(LogFilter.Lfg, 
                $"UpdateProposal: Player [{guid}] of proposal {proposalId} selected: {accept}");

            if (!accept)
            {
                RemoveProposal(new KeyValuePair<int, LfgProposal>(proposalId, proposal), LfgUpdateType.ProposalDeclined);
                return;
            }

            // check if all have answered and reorder players (leader first)
            bool allAnswered = true;
            foreach (var itPlayers in proposal.players)
                if (itPlayers.Value.accept != LfgAnswer.Agree)   // No answer (-1) or not accepted (0)
                    allAnswered = false;

            if (!allAnswered)
            {
                foreach (var it in proposal.players)
                    SendLfgUpdateProposal(it.Key, proposal);

                return;
            }

            bool sendUpdate = proposal.state != LfgProposalState.Success;
            proposal.state = LfgProposalState.Success;
            ServerTime joinTime = LoopTime.ServerTime;

            LFGQueue queue = GetQueue(guid);
            LfgUpdateData updateData = new(LfgUpdateType.GroupFound);
            foreach (var it in proposal.players)
            {
                ObjectGuid pguid = it.Key;
                ObjectGuid gguid = it.Value.group;
                int dungeonId = GetSelectedDungeons(pguid).First();
                TimeSpan waitTime;
                if (sendUpdate)
                    SendLfgUpdateProposal(pguid, proposal);

                if (!gguid.IsEmpty())
                {
                    waitTime = joinTime - queue.GetJoinTime(gguid);
                    SendLfgUpdateStatus(pguid, updateData, false);
                }
                else
                {
                    waitTime = joinTime - queue.GetJoinTime(pguid);
                    SendLfgUpdateStatus(pguid, updateData, false);
                }
                updateData.updateType = LfgUpdateType.RemovedFromQueue;
                SendLfgUpdateStatus(pguid, updateData, true);
                SendLfgUpdateStatus(pguid, updateData, false);

                // Update timers
                LfgRoles role = GetRoles(pguid);
                role &= ~LfgRoles.Leader;
                switch (role)
                {
                    case LfgRoles.Damage:
                        queue.UpdateWaitTimeDps(waitTime, dungeonId);
                        break;
                    case LfgRoles.Healer:
                        queue.UpdateWaitTimeHealer(waitTime, dungeonId);
                        break;
                    case LfgRoles.Tank:
                        queue.UpdateWaitTimeTank(waitTime, dungeonId);
                        break;
                    default:
                        queue.UpdateWaitTimeAvg(waitTime, dungeonId);
                        break;
                }

                // Store the number of players that were present in group when joining RFD, used for achievement purposes
                Player _player = Global.ObjAccessor.FindConnectedPlayer(pguid);
                if (_player != null)
                {
                    Group group = _player.GetGroup();
                    if (group != null)
                        PlayersStore[pguid].SetNumberOfPartyMembersAtJoin((byte)group.GetMembersCount());
                }

                SetState(pguid, LfgState.Dungeon);
            }

            // Remove players/groups from Queue
            foreach (var it in proposal.queues)
                queue.RemoveFromQueue(it);

            MakeNewGroup(proposal);
            ProposalsStore.Remove(proposalId);
        }

        void RemoveProposal(KeyValuePair<int, LfgProposal> itProposal, LfgUpdateType type)
        {
            LfgProposal proposal = itProposal.Value;
            proposal.state = LfgProposalState.Failed;

            Log.outDebug(LogFilter.Lfg, 
                $"RemoveProposal: Proposal {itProposal.Key}, state FAILED, UpdateType {type}");

            // Mark all people that didn't answered as no accept
            if (type == LfgUpdateType.ProposalFailed)
                foreach (var it in proposal.players)
                    if (it.Value.accept == LfgAnswer.Pending)
                        it.Value.accept = LfgAnswer.Deny;

            // Mark players/groups to be removed
            List<ObjectGuid> toRemove = new();
            foreach (var it in proposal.players)
            {
                if (it.Value.accept == LfgAnswer.Agree)
                    continue;

                ObjectGuid guid = !it.Value.group.IsEmpty() ? it.Value.group : it.Key;
                // Player didn't accept or still pending when no secs left
                if (it.Value.accept == LfgAnswer.Deny || type == LfgUpdateType.ProposalFailed)
                {
                    it.Value.accept = LfgAnswer.Deny;
                    toRemove.Add(guid);
                }
            }

            // Notify players
            foreach (var it in proposal.players)
            {
                ObjectGuid guid = it.Key;
                ObjectGuid gguid = !it.Value.group.IsEmpty() ? it.Value.group : guid;

                SendLfgUpdateProposal(guid, proposal);

                if (toRemove.Contains(gguid))         // Didn't accept or in same group that someone that didn't accept
                {
                    LfgUpdateData updateData = new();
                    if (it.Value.accept == LfgAnswer.Deny)
                    {
                        updateData.updateType = type;
                        Log.outDebug(LogFilter.Lfg, 
                            $"RemoveProposal: [{guid}] didn't accept. " +
                            $"Removing from queue and compatible cache");
                    }
                    else
                    {
                        updateData.updateType = LfgUpdateType.RemovedFromQueue;
                        Log.outDebug(LogFilter.Lfg, 
                            $"RemoveProposal: [{guid}] in same group that someone that didn't accept. " +
                            $"Removing from queue and compatible cache");
                    }

                    RestoreState(guid, "Proposal Fail (didn't accepted or in group with someone that didn't accept");
                    if (gguid != guid)
                    {
                        RestoreState(it.Value.group, "Proposal Fail (someone in group didn't accepted)");
                        SendLfgUpdateStatus(guid, updateData, true);
                    }
                    else
                        SendLfgUpdateStatus(guid, updateData, false);
                }
                else
                {
                    Log.outDebug(LogFilter.Lfg, $"RemoveProposal: Readding [{guid}] to queue.");
                    SetState(guid, LfgState.Queued);
                    if (gguid != guid)
                    {
                        SetState(gguid, LfgState.Queued);
                        SendLfgUpdateStatus(guid, new LfgUpdateData(LfgUpdateType.AddedToQueue, GetSelectedDungeons(guid)), true);
                    }
                    else
                        SendLfgUpdateStatus(guid, new LfgUpdateData(LfgUpdateType.AddedToQueue, GetSelectedDungeons(guid)), false);
                }
            }

            LFGQueue queue = GetQueue(proposal.players.First().Key);
            // Remove players/groups from queue
            foreach (var guid in toRemove)
            {
                queue.RemoveFromQueue(guid);
                proposal.queues.Remove(guid);
            }

            // Readd to queue
            foreach (var guid in proposal.queues)
                queue.AddToQueue(guid, true);

            ProposalsStore.Remove(itProposal.Key);
        }

        public void InitBoot(ObjectGuid gguid, ObjectGuid kicker, ObjectGuid victim, string reason)
        {
            SetVoteKick(gguid, true);

            LfgPlayerBoot boot = BootsStore[gguid];
            boot.inProgress = true;
            boot.cancelTime = LoopTime.ServerTime + SharedConst.LFGTimeBoot;
            boot.reason = reason;
            boot.victim = victim;

            List<ObjectGuid> players = GetPlayers(gguid);

            // Set votes
            foreach (var guid in players)
                boot.votes[guid] = LfgAnswer.Pending;

            boot.votes[victim] = LfgAnswer.Deny;                  // Victim auto vote NO
            boot.votes[kicker] = LfgAnswer.Agree;                 // Kicker auto vote YES

            // Notify players
            foreach (var it in players)
                SendLfgBootProposalUpdate(it, boot);
        }

        public void UpdateBoot(ObjectGuid guid, bool accept)
        {
            ObjectGuid gguid = GetGroup(guid);
            if (gguid.IsEmpty())
                return;

            var boot = BootsStore.LookupByKey(gguid);
            if (boot == null)
                return;

            if (boot.votes[guid] != LfgAnswer.Pending)    // Cheat check: Player can't vote twice
                return;

            boot.votes[guid] = (LfgAnswer)Convert.ToInt32(accept);

            byte agreeNum = 0;
            byte denyNum = 0;
            foreach (var (_, answer) in boot.votes)
            {
                switch (answer)
                {
                    case LfgAnswer.Pending:
                        break;
                    case LfgAnswer.Agree:
                        ++agreeNum;
                        break;
                    case LfgAnswer.Deny:
                        ++denyNum;
                        break;
                }
            }

            // if we don't have enough votes (agree or deny) do nothing
            if (agreeNum < SharedConst.LFGKickVotesNeeded && (boot.votes.Count - denyNum) >= SharedConst.LFGKickVotesNeeded)
                return;

            // Send update info to all players
            boot.inProgress = false;
            foreach (var itVotes in boot.votes)
            {
                ObjectGuid pguid = itVotes.Key;
                if (pguid != boot.victim)
                    SendLfgBootProposalUpdate(pguid, boot);
            }

            SetVoteKick(gguid, false);
            if (agreeNum == SharedConst.LFGKickVotesNeeded)           // Vote passed - Kick player
            {
                Group group = Global.GroupMgr.GetGroupByGUID(gguid);
                if (group != null)
                    Player.RemoveFromGroup(group, boot.victim, RemoveMethod.KickLFG);
                DecreaseKicksLeft(gguid);
            }
            BootsStore.Remove(gguid);
        }

        public void TeleportPlayer(Player player, bool outt, bool fromOpcode = false)
        {
            LFGDungeonData dungeon = null;
            Group group = player.GetGroup();

            if (group != null && group.IsLFGGroup())
                dungeon = GetLFGDungeon(GetDungeon(group.GetGUID()));

            if (dungeon == null)
            {
                Log.outDebug(LogFilter.Lfg, 
                    $"TeleportPlayer: Player {player.GetName()} not in group/lfggroup or dungeon not found!");

                player.GetSession().SendLfgTeleportError(LfgTeleportResult.NoReturnLocation);
                return;
            }

            if (outt)
            {
                Log.outDebug(LogFilter.Lfg, 
                    $"TeleportPlayer: Player {player.GetName()} is being teleported out. " +
                    $"Current Map {player.GetMapId()} - Expected Map {dungeon.map}");
                
                if (player.GetMapId() == dungeon.map)
                    player.TeleportToBGEntryPoint();

                return;
            }

            LfgTeleportResult error = LfgTeleportResult.None;
            if (!player.IsAlive())
                error = LfgTeleportResult.Dead;
            else if (player.IsFalling() || player.HasUnitState(UnitState.Jumping))
                error = LfgTeleportResult.Falling;
            else if (player.IsMirrorTimerActive(MirrorTimerType.Fatigue))
                error = LfgTeleportResult.Exhaustion;
            else if (player.GetVehicle() != null)
                error = LfgTeleportResult.OnTransport;
            else if (!player.GetCharmedGUID().IsEmpty())
                error = LfgTeleportResult.ImmuneToSummons;
            else if (player.HasAura(9454)) // check Freeze debuff
                error = LfgTeleportResult.NoReturnLocation;
            else if (player.GetMapId() != dungeon.map)  // Do not teleport players in dungeon to the entrance
            {
                int mapid = dungeon.map;
                float x = dungeon.x;
                float y = dungeon.y;
                float z = dungeon.z;
                float orientation = dungeon.o;

                if (!fromOpcode)
                {
                    // Select a player inside to be teleported to
                    for (GroupReference refe = group.GetFirstMember(); refe != null; refe = refe.Next())
                    {
                        Player plrg = refe.GetSource();
                        if (plrg != null && plrg != player && plrg.GetMapId() == dungeon.map)
                        {
                            mapid = plrg.GetMapId();
                            x = plrg.GetPositionX();
                            y = plrg.GetPositionY();
                            z = plrg.GetPositionZ();
                            orientation = plrg.GetOrientation();
                            break;
                        }
                    }
                }

                if (!player.GetMap().IsDungeon())
                    player.SetBattlegroundEntryPoint();

                player.FinishTaxiFlight();

                if (!player.TeleportTo(mapid, x, y, z, orientation))
                    error = LfgTeleportResult.NoReturnLocation;
            }
            else
                error = LfgTeleportResult.NoReturnLocation;

            if (error != LfgTeleportResult.None)
                player.GetSession().SendLfgTeleportError(error);

            Log.outDebug(LogFilter.Lfg, 
                $"TeleportPlayer: Player {player.GetName()} is being teleported " +
                $"in to map {dungeon.map} (x: {dungeon.x}, y: {dungeon.y}, z: {dungeon.z}) Result: {error}");
        }

        /// <summary>
        /// Check if dungeon can be rewarded, if any.
        /// </summary>
        /// <param name="gguid">Group guid</param>
        /// <param name="dungeonEncounterIds">DungeonEncounter that was just completed</param>
        /// <param name="currMap">Map of the instance where encounter was completed</param>
        public void OnDungeonEncounterDone(ObjectGuid gguid, int[] dungeonEncounterIds, Map currMap)
        {
            if (GetState(gguid) == LfgState.FinishedDungeon) // Shouldn't happen. Do not reward multiple times
            {
                Log.outDebug(LogFilter.Lfg, $"Group: {gguid} already rewarded");
                return;
            }

            int gDungeonId = GetDungeon(gguid);
            LFGDungeonData dungeonDone = GetLFGDungeon(gDungeonId);
            // LFGDungeons can point to a DungeonEncounter from any difficulty so we need this kind of lenient check
            if (!dungeonEncounterIds.Contains(dungeonDone.finalDungeonEncounterId))
                return;

            FinishDungeon(gguid, gDungeonId, currMap);
        }

        /// <summary>
        /// Finish a dungeon and give reward, if any.
        /// </summary>
        /// <param name="gguid">Group guid</param>
        /// <param name="dungeonId">Dungeonid</param>
        /// <param name="currMap">Map of the instance where encounter was completed</param>
        public void FinishDungeon(ObjectGuid gguid, int dungeonId, Map currMap)
        {
            int gDungeonId = GetDungeon(gguid);
            if (gDungeonId != dungeonId)
            {
                Log.outDebug(LogFilter.Lfg, 
                    $"Group {gguid} finished dungeon {dungeonId} " +
                    $"but queued for {gDungeonId}. Ignoring");

                return;
            }

            if (GetState(gguid) == LfgState.FinishedDungeon) // Shouldn't happen. Do not reward multiple times
            {
                Log.outDebug(LogFilter.Lfg, $"Group {gguid} already rewarded");
                return;
            }

            SetState(gguid, LfgState.FinishedDungeon);

            List<ObjectGuid> players = GetPlayers(gguid);
            foreach (var guid in players)
            {
                if (GetState(guid) == LfgState.FinishedDungeon)
                {
                    Log.outDebug(LogFilter.Lfg, $"Group: {gguid}, Player: {guid} already rewarded");
                    continue;
                }

                int rDungeonId = 0;
                List<int> dungeons = GetSelectedDungeons(guid);
                if (!dungeons.Empty())
                    rDungeonId = dungeons.First();

                SetState(guid, LfgState.FinishedDungeon);

                // Give rewards only if its a random dungeon
                LFGDungeonData dungeon = GetLFGDungeon(rDungeonId);

                if (dungeon == null || (dungeon.type != LfgType.Random && !dungeon.seasonal))
                {
                    Log.outDebug(LogFilter.Lfg, 
                        $"Group: {gguid}, Player: {guid} dungeon {rDungeonId} is not random or seasonal");
                    continue;
                }

                Player player = Global.ObjAccessor.FindPlayer(guid);
                if (player == null)
                {
                    Log.outDebug(LogFilter.Lfg, $"Group: {gguid}, Player: {guid} not found in world");
                    continue;
                }

                if (player.GetMap() != currMap)
                {
                    Log.outDebug(LogFilter.Lfg, $"Group: {gguid}, Player: {guid} is in a different map");
                    continue;
                }

                player.RemoveAurasDueToSpell(SharedConst.LFGSpellDungeonCooldown);

                LFGDungeonData dungeonDone = GetLFGDungeon(dungeonId);
                int mapId = dungeonDone != null ? dungeonDone.map : 0;

                if (player.GetMapId() != mapId)
                {
                    Log.outDebug(LogFilter.Lfg, 
                        $"Group: {gguid}, Player: {guid} is in map {player.GetMapId()} " +
                        $"and should be in {mapId} to get reward");

                    continue;
                }

                // Update achievements
                if (dungeon.difficulty == Difficulty.Heroic)
                {
                    byte lfdRandomPlayers = 0;
                    byte numParty = PlayersStore[guid].GetNumberOfPartyMembersAtJoin();
                    if (numParty != 0)
                        lfdRandomPlayers = (byte)(5 - numParty);
                    else
                        lfdRandomPlayers = 4;
                    player.UpdateCriteria(CriteriaType.CompletedLFGDungeonWithStrangers, lfdRandomPlayers);
                }

                LfgReward reward = GetRandomDungeonReward(rDungeonId, player.GetLevel());
                if (reward == null)
                    continue;

                bool done = false;
                Quest quest = Global.ObjectMgr.GetQuestTemplate(reward.firstQuest);
                if (quest == null)
                    continue;

                // if we can take the quest, means that we haven't done this kind of "run", IE: First Heroic Random of Day.
                if (player.CanRewardQuest(quest, false))
                    player.RewardQuest(quest, LootItemType.Item, 0, null, false);
                else
                {
                    done = true;
                    quest = Global.ObjectMgr.GetQuestTemplate(reward.otherQuest);
                    if (quest == null)
                        continue;
                    // we give reward without informing client (retail does this)
                    player.RewardQuest(quest, LootItemType.Item, 0, null, false);
                }

                // Give rewards
                Log.outDebug(LogFilter.Lfg, 
                    $"Group: {gguid}, Player: {guid} done dungeon {GetDungeon(gguid)}, " +
                    $"{(done ? "" : "not")} previously done.");

                LfgPlayerRewardData data = new(dungeon.Entry(), GetDungeon(gguid, false), done, quest);
                player.GetSession().SendLfgPlayerReward(data);
            }
        }

        IReadOnlyList<int> GetDungeonsByRandom(int randomdungeon)
        {
            LFGDungeonData dungeon = GetLFGDungeon(randomdungeon);
            byte group = (byte)(dungeon != null ? dungeon.group : 0);
            return CachedDungeonMapStore[group];
        }

        public LfgReward GetRandomDungeonReward(int dungeon, int level)
        {
            LfgReward reward = null;
            var bounds = RewardMapStore[dungeon & 0x00FFFFFF];
            foreach (var rew in bounds)
            {
                reward = rew;
                // ordered properly at loading
                if (rew.maxLevel >= level)
                    break;
            }

            return reward;
        }

        public LfgType GetDungeonType(int dungeonId)
        {
            LFGDungeonData dungeon = GetLFGDungeon(dungeonId);
            if (dungeon == null)
                return LfgType.None;

            return dungeon.type;
        }

        public LfgState GetState(ObjectGuid guid)
        {
            LfgState state;
            if (guid.IsParty())
            {
                if (!GroupsStore.ContainsKey(guid))
                    return LfgState.None;

                state = GroupsStore[guid].GetState();
            }
            else
            {
                AddPlayerData(guid);
                state = PlayersStore[guid].GetState();
            }

            Log.outDebug(LogFilter.Lfg, $"GetState: [{guid}] = {state}");
            return state;
        }

        public LfgState GetOldState(ObjectGuid guid)
        {
            LfgState state;
            if (guid.IsParty())
                state = GroupsStore[guid].GetOldState();
            else
            {
                AddPlayerData(guid);
                state = PlayersStore[guid].GetOldState();
            }

            Log.outDebug(LogFilter.Lfg, $"GetOldState: [{guid}] = {state}");
            return state;
        }

        public bool IsVoteKickActive(ObjectGuid gguid)
        {
            Cypher.Assert(gguid.IsParty());

            bool active = GroupsStore[gguid].IsVoteKickActive();
            Log.outInfo(LogFilter.Lfg, $"Group: {gguid}, Active: {active}");

            return active;
        }

        public int GetDungeon(ObjectGuid guid, bool asId = true)
        {
            if (!GroupsStore.ContainsKey(guid))
                return 0;

            int dungeon = GroupsStore[guid].GetDungeon(asId);
            Log.outDebug(LogFilter.Lfg, $"GetDungeon: [{guid}] asId: {asId} = {dungeon}");
            return dungeon;
        }

        public int GetDungeonMapId(ObjectGuid guid)
        {
            if (!GroupsStore.ContainsKey(guid))
                return 0;

            int dungeonId = GroupsStore[guid].GetDungeon(true);
            int mapId = 0;
            if (dungeonId != 0)
            {
                LFGDungeonData dungeon = GetLFGDungeon(dungeonId);
                if (dungeon != null)
                    mapId = dungeon.map;
            }

            Log.outError(LogFilter.Lfg, $"GetDungeonMapId: [{guid}] = {mapId} (DungeonId = {dungeonId})");
            return mapId;
        }

        public LfgRoles GetRoles(ObjectGuid guid)
        {
            LfgRoles roles = PlayersStore[guid].GetRoles();
            Log.outDebug(LogFilter.Lfg, $"GetRoles: [{guid}] = {roles}");
            return roles;
        }

        public List<int> GetSelectedDungeons(ObjectGuid guid)
        {
            Log.outDebug(LogFilter.Lfg, $"GetSelectedDungeons: [{guid}]");
            return PlayersStore[guid].GetSelectedDungeons();
        }

        public int GetSelectedRandomDungeon(ObjectGuid guid)
        {
            if (GetState(guid) != LfgState.None)
            {
                var dungeons = GetSelectedDungeons(guid);
                if (!dungeons.Empty())
                {
                    LFGDungeonData dungeon = GetLFGDungeon(dungeons.First());
                    if (dungeon != null && dungeon.type == LfgType.Raid)
                        return dungeons.First();
                }
            }

            return 0;
        }

        public Dictionary<int, LfgLockInfoData> GetLockedDungeons(ObjectGuid guid)
        {
            Dictionary<int, LfgLockInfoData> lockDic = new();
            Player player = Global.ObjAccessor.FindConnectedPlayer(guid);
            if (player == null)
            {
                Log.outWarn(LogFilter.Lfg, $"{guid} not ingame while retrieving his LockedDungeons.");
                return lockDic;
            }

            int level = player.GetLevel();
            Expansion expansion = player.GetSession().GetExpansion();
            var dungeons = GetDungeonsByRandom(0);
            bool denyJoin = !player.GetSession().HasPermission(RBACPermissions.JoinDungeonFinder);

            foreach (var it in dungeons)
            {
                LFGDungeonData dungeon = GetLFGDungeon(it);
                if (dungeon == null) // should never happen - We provide a list from sLFGDungeonStore
                    continue;

                LfgLockStatusType lockStatus = 0;
                AccessRequirement ar;
                if (denyJoin)
                    lockStatus = LfgLockStatusType.RaidLocked;
                else if (dungeon.expansion > expansion)
                    lockStatus = LfgLockStatusType.InsufficientExpansion;
                else if (Global.DisableMgr.IsDisabledFor(DisableType.Map, dungeon.map, player))
                    lockStatus = LfgLockStatusType.NotInSeason;
                else if (Global.DisableMgr.IsDisabledFor(DisableType.LFGMap, dungeon.map, player))
                    lockStatus = LfgLockStatusType.RaidLocked;
                else if (Global.InstanceLockMgr.FindActiveInstanceLock(guid, new MapDb2Entries(dungeon.map, dungeon.difficulty)) != null)
                    lockStatus = LfgLockStatusType.RaidLocked;
                else if (dungeon.seasonal && !IsSeasonActive(dungeon.id))
                    lockStatus = LfgLockStatusType.NotInSeason;
                else if (dungeon.requiredItemLevel > player.GetAverageItemLevel())
                    lockStatus = LfgLockStatusType.TooLowGearScore;
                else if ((ar = Global.ObjectMgr.GetAccessRequirement(dungeon.map, dungeon.difficulty)) != null)
                {
                    if (ar.achievement != 0 && !player.HasAchieved(ar.achievement))
                        lockStatus = LfgLockStatusType.MissingAchievement;
                    else if (player.GetTeam() == Team.Alliance && ar.quest_A != 0 && !player.GetQuestRewardStatus(ar.quest_A))
                        lockStatus = LfgLockStatusType.QuestNotCompleted;
                    else if (player.GetTeam() == Team.Horde && ar.quest_H != 0 && !player.GetQuestRewardStatus(ar.quest_H))
                        lockStatus = LfgLockStatusType.QuestNotCompleted;
                    else
                        if (ar.item != 0)
                    {
                        if (!player.HasItemCount(ar.item) && (ar.item2 == 0 || !player.HasItemCount(ar.item2)))
                            lockStatus = LfgLockStatusType.MissingItem;
                    }
                    else if (ar.item2 != 0 && !player.HasItemCount(ar.item2))
                        lockStatus = LfgLockStatusType.MissingItem;
                }
                else if (dungeon.minlevel > level)                
                    lockStatus = LfgLockStatusType.TooLowLevel;
                else if (dungeon.maxlevel < level)
                    lockStatus = LfgLockStatusType.TooHighLevel;


                /* @todo VoA closed if WG is not under team control (LFG_LOCKSTATUS_RAID_LOCKED)
                lockData = LFG_LOCKSTATUS_TOO_HIGH_GEAR_SCORE;
                lockData = LFG_LOCKSTATUS_ATTUNEMENT_TOO_LOW_LEVEL;
                lockData = LFG_LOCKSTATUS_ATTUNEMENT_TOO_HIGH_LEVEL;
                */
                if (lockStatus != 0)
                    lockDic[dungeon.Entry()] = new LfgLockInfoData(lockStatus, dungeon.requiredItemLevel, player.GetAverageItemLevel());
            }

            return lockDic;
        }

        public byte GetKicksLeft(ObjectGuid guid)
        {
            byte kicks = GroupsStore[guid].GetKicksLeft();
            Log.outDebug(LogFilter.Lfg, $"GetKicksLeft: [{guid}] = {kicks}");
            return kicks;
        }

        void RestoreState(ObjectGuid guid, string debugMsg)
        {
            if (guid.IsParty())
            {
                var data = GroupsStore[guid];
                data.RestoreState();
            }
            else
            {
                var data = PlayersStore[guid];
                data.RestoreState();
            }
        }

        public void SetState(ObjectGuid guid, LfgState state)
        {
            if (guid.IsParty())
            {
                if (!GroupsStore.ContainsKey(guid))
                    GroupsStore[guid] = new LFGGroupData();
                var data = GroupsStore[guid];
                data.SetState(state);
            }
            else
            {
                var data = PlayersStore[guid];
                data.SetState(state);
            }
        }

        void SetVoteKick(ObjectGuid gguid, bool active)
        {
            Cypher.Assert(gguid.IsParty());

            var data = GroupsStore[gguid];
            Log.outInfo(LogFilter.Lfg, 
                $"Group: {gguid}, New state: {active}, Previous: {data.IsVoteKickActive()}");

            data.SetVoteKick(active);
        }

        void SetDungeon(ObjectGuid guid, int dungeon)
        {
            AddPlayerData(guid);
            Log.outDebug(LogFilter.Lfg, $"SetDungeon: [{guid}] dungeon {dungeon}");
            GroupsStore[guid].SetDungeon(dungeon);
        }

        void SetRoles(ObjectGuid guid, LfgRoles roles)
        {
            AddPlayerData(guid);
            Log.outDebug(LogFilter.Lfg, $"SetRoles: [{guid}] roles: {roles}");
            PlayersStore[guid].SetRoles(roles);
        }

        public void SetSelectedDungeons(ObjectGuid guid, List<int> dungeons)
        {
            AddPlayerData(guid);
            Log.outDebug(LogFilter.Lfg, $"SetSelectedDungeons: [{guid}] " +
                $"Dungeons: {ConcatenateDungeons(dungeons)}");

            PlayersStore[guid].SetSelectedDungeons(dungeons);
        }

        void DecreaseKicksLeft(ObjectGuid guid)
        {
            Log.outDebug(LogFilter.Lfg, $"DecreaseKicksLeft: [{guid}]");
            GroupsStore[guid].DecreaseKicksLeft();
        }

        void AddPlayerData(ObjectGuid guid)
        {
            if (PlayersStore.ContainsKey(guid))
                return;

            PlayersStore[guid] = new LFGPlayerData();
        }

        void SetTicket(ObjectGuid guid, RideTicket ticket)
        {
            PlayersStore[guid].SetTicket(ticket);
        }

        void RemovePlayerData(ObjectGuid guid)
        {
            Log.outDebug(LogFilter.Lfg, $"RemovePlayerData: [{guid}]");
            PlayersStore.Remove(guid);
        }

        public void RemoveGroupData(ObjectGuid guid)
        {
            Log.outDebug(LogFilter.Lfg, $"RemoveGroupData: [{guid}]");
            var it = GroupsStore.LookupByKey(guid);
            if (it == null)
                return;

            LfgState state = GetState(guid);
            // If group is being formed after proposal success do nothing more
            List<ObjectGuid> players = it.GetPlayers();
            foreach (var _guid in players)
            {
                SetGroup(_guid, ObjectGuid.Empty);
                if (state != LfgState.Proposal)
                {
                    SetState(_guid, LfgState.None);
                    SendLfgUpdateStatus(_guid, new LfgUpdateData(LfgUpdateType.RemovedFromQueue), true);
                }
            }
            GroupsStore.Remove(guid);
        }

        Team GetTeam(ObjectGuid guid)
        {
            return PlayersStore[guid].GetTeam();
        }

        LfgRoles FilterClassRoles(Player player, LfgRoles roles)
        {
            uint allowedRoles = (uint)LfgRoles.Leader;
            for (int i = 0; i < PlayerConst.MaxSpecializations; ++i)
            {
                var specialization = Global.DB2Mgr.GetChrSpecializationByIndex(player.GetClass(), i);
                if (specialization != null)
                    allowedRoles |= (1u << ((int)specialization.Role + 1));
            }

            return roles & (LfgRoles)allowedRoles;
        }

        public byte RemovePlayerFromGroup(ObjectGuid gguid, ObjectGuid guid)
        {
            return GroupsStore[gguid].RemovePlayer(guid);
        }

        public void AddPlayerToGroup(ObjectGuid gguid, ObjectGuid guid)
        {
            if (!GroupsStore.ContainsKey(gguid))
                GroupsStore[gguid] = new LFGGroupData();

            GroupsStore[gguid].AddPlayer(guid);
        }

        public void SetLeader(ObjectGuid gguid, ObjectGuid leader)
        {
            if (!GroupsStore.ContainsKey(gguid))
                GroupsStore[gguid] = new LFGGroupData();

            GroupsStore[gguid].SetLeader(leader);
        }

        public void SetTeam(ObjectGuid guid, Team team)
        {
            if (WorldConfig.Values[WorldCfg.AllowTwoSideInteractionGroup].Bool)
                team = 0;

            PlayersStore[guid].SetTeam(team);
        }

        public ObjectGuid GetGroup(ObjectGuid guid)
        {
            AddPlayerData(guid);
            return PlayersStore[guid].GetGroup();
        }

        public void SetGroup(ObjectGuid guid, ObjectGuid group)
        {
            AddPlayerData(guid);
            PlayersStore[guid].SetGroup(group);
        }

        List<ObjectGuid> GetPlayers(ObjectGuid guid)
        {
            return GroupsStore[guid].GetPlayers();
        }

        public byte GetPlayerCount(ObjectGuid guid)
        {
            return GroupsStore[guid].GetPlayerCount();
        }

        public ObjectGuid GetLeader(ObjectGuid guid)
        {
            return GroupsStore[guid].GetLeader();
        }

        public bool HasIgnore(ObjectGuid guid1, ObjectGuid guid2)
        {
            Player plr1 = Global.ObjAccessor.FindPlayer(guid1);
            Player plr2 = Global.ObjAccessor.FindPlayer(guid2);
            return plr1 != null && plr2 != null
                && (plr1.GetSocial().HasIgnore(guid2, plr2.GetSession().GetAccountGUID())
                    || plr2.GetSocial().HasIgnore(guid1, plr1.GetSession().GetAccountGUID()));
        }

        public void SendLfgRoleChosen(ObjectGuid guid, ObjectGuid pguid, LfgRoles roles)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
                player.GetSession().SendLfgRoleChosen(pguid, roles);
        }

        public void SendLfgRoleCheckUpdate(ObjectGuid guid, LfgRoleCheck roleCheck)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
                player.GetSession().SendLfgRoleCheckUpdate(roleCheck);
        }

        public void SendLfgUpdateStatus(ObjectGuid guid, LfgUpdateData data, bool party)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
                player.GetSession().SendLfgUpdateStatus(data, party);
        }

        public void SendLfgJoinResult(ObjectGuid guid, LfgJoinResultData data)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
                player.GetSession().SendLfgJoinResult(data);
        }

        public void SendLfgBootProposalUpdate(ObjectGuid guid, LfgPlayerBoot boot)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
                player.GetSession().SendLfgBootProposalUpdate(boot);
        }

        public void SendLfgUpdateProposal(ObjectGuid guid, LfgProposal proposal)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
                player.GetSession().SendLfgProposalUpdate(proposal);
        }

        public void SendLfgQueueStatus(ObjectGuid guid, LfgQueueStatusData data)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
                player.GetSession().SendLfgQueueStatus(data);
        }

        public bool IsLfgGroup(ObjectGuid guid)
        {
            return !guid.IsEmpty() && guid.IsParty() && GroupsStore[guid].IsLfgGroup();
        }

        public byte GetQueueId(ObjectGuid guid)
        {
            if (guid.IsParty())
            {
                List<ObjectGuid> players = GetPlayers(guid);
                ObjectGuid pguid = players.Empty() ? ObjectGuid.Empty : players.First();
                if (!pguid.IsEmpty())
                    return (byte)GetTeam(pguid);
            }

            return (byte)GetTeam(guid);
        }

        public LFGQueue GetQueue(ObjectGuid guid)
        {
            byte queueId = GetQueueId(guid);
            if (!QueuesStore.ContainsKey(queueId))
                QueuesStore[queueId] = new LFGQueue();

            return QueuesStore[queueId];
        }

        public bool AllQueued(List<ObjectGuid> check)
        {
            if (check.Empty())
                return false;

            foreach (var guid in check)
            {
                LfgState state = GetState(guid);
                if (state != LfgState.Queued)
                {
                    if (state != LfgState.Proposal)
                    {
                        Log.outDebug(LogFilter.Lfg,
                            $"Unexpected state found while trying to form new group. " +
                            $"Guid: {guid}, State: {state}");
                    }

                    return false;
                }
            }
            return true;
        }

        public ServerTime GetQueueJoinTime(ObjectGuid guid)
        {
            byte queueId = GetQueueId(guid);
            var lfgQueue = QueuesStore.LookupByKey(queueId);
            if (lfgQueue != null)
                return lfgQueue.GetJoinTime(guid);

            return ServerTime.Zero;
        }

        // Only for debugging purposes
        public void Clean()
        {
            QueuesStore.Clear();
        }

        public bool IsOptionEnabled(LfgOptions option)
        {
            return m_options.HasAnyFlag(option);
        }

        public LfgOptions GetOptions()
        {
            return m_options;
        }

        public void SetOptions(LfgOptions options)
        {
            m_options = options;
        }

        public LfgUpdateData GetLfgStatus(ObjectGuid guid)
        {
            var playerData = PlayersStore[guid];
            return new LfgUpdateData(LfgUpdateType.UpdateStatus, playerData.GetState(), playerData.GetSelectedDungeons());
        }

        bool IsSeasonActive(int dungeonId)
        {
            switch (dungeonId)
            {
                case 285: // The Headless Horseman
                    return Global.GameEventMgr.IsHolidayActive(HolidayIds.HallowsEnd);
                case 286: // The Frost Lord Ahune
                    return Global.GameEventMgr.IsHolidayActive(HolidayIds.MidsummerFireFestival);
                case 287: // Coren Direbrew
                    return Global.GameEventMgr.IsHolidayActive(HolidayIds.Brewfest);
                case 288: // The Crown Chemical Co.
                    return Global.GameEventMgr.IsHolidayActive(HolidayIds.LoveIsInTheAir);
                case 744: // Random Timewalking Dungeon (Burning Crusade)
                    return Global.GameEventMgr.IsHolidayActive(HolidayIds.TimewalkingDungeonEventBcDefault);
                case 995: // Random Timewalking Dungeon (Wrath of the Lich King)
                    return Global.GameEventMgr.IsHolidayActive(HolidayIds.TimewalkingDungeonEventLkDefault);
                case 1146: // Random Timewalking Dungeon (Cataclysm)
                    return Global.GameEventMgr.IsHolidayActive(HolidayIds.TimewalkingDungeonEventCataDefault);
            }
            return false;
        }

        public string DumpQueueInfo(bool full)
        {

            int size = QueuesStore.Count;

            string str = "Number of Queues: " + size + "\n";
            foreach (var pair in QueuesStore)
            {
                string queued = pair.Value.DumpQueueInfo();
                string compatibles = pair.Value.DumpCompatibleInfo(full);
                str += queued + compatibles;
            }

            return str;
        }

        public void SetupGroupMember(ObjectGuid guid, ObjectGuid gguid)
        {
            List<int> dungeons = new();
            dungeons.Add(GetDungeon(gguid));
            SetSelectedDungeons(guid, dungeons);
            SetState(guid, GetState(gguid));
            SetGroup(guid, gguid);
            AddPlayerToGroup(gguid, guid);
        }

        public bool SelectedRandomLfgDungeon(ObjectGuid guid)
        {
            if (GetState(guid) != LfgState.None)
            {
                List<int> dungeons = GetSelectedDungeons(guid);
                if (!dungeons.Empty())
                {
                    LFGDungeonData dungeon = GetLFGDungeon(dungeons.First());
                    if (dungeon != null && (dungeon.type == LfgType.Random || dungeon.seasonal))
                        return true;
                }
            }

            return false;
        }

        public bool InLfgDungeonMap(ObjectGuid guid, int map, Difficulty difficulty)
        {
            if (!guid.IsParty())
                guid = GetGroup(guid);

            int dungeonId = GetDungeon(guid, true);
            if (dungeonId != 0)
            {
                LFGDungeonData dungeon = GetLFGDungeon(dungeonId);
                if (dungeon != null)
                {
                    if (dungeon.map == map && dungeon.difficulty == difficulty)
                        return true;
                }
            }

            return false;
        }

        public int GetLFGDungeonEntry(int id)
        {
            if (id != 0)
            {
                LFGDungeonData dungeon = GetLFGDungeon(id);
                if (dungeon != null)
                    return dungeon.Entry();
            }

            return 0;
        }

        public List<int> GetRandomAndSeasonalDungeons(int level, Expansion expansion)
        {
            List<int> randomDungeons = new();
            foreach (var dungeon in LfgDungeonStore.Values)
            {
                if (!(dungeon.type == LfgType.Random || (dungeon.seasonal && Global.LFGMgr.IsSeasonActive(dungeon.id))))
                    continue;

                if (dungeon.expansion > expansion)
                    continue;

                if (level < dungeon.minlevel && level > dungeon.maxlevel)
                    continue;

                randomDungeons.Add(dungeon.Entry());
            }
            return randomDungeons;
        }

        // General variables
        TimeSpan m_QueueTimer;     //< used to check interval of update
        int m_lfgProposalId;  //< used as internal counter for proposals
        LfgOptions m_options;        //< Stores config options

        Dictionary<byte, LFGQueue> QueuesStore = new();                     //< Queues
        MultiMap<byte, int> CachedDungeonMapStore = new(); //< Stores all dungeons by groupType
        // Reward System
        MultiMap<int, LfgReward> RewardMapStore = new();                    //< Stores rewards for random dungeons
        Dictionary<int, LFGDungeonData> LfgDungeonStore = new();
        // Rolecheck - Proposal - Vote Kicks
        Dictionary<ObjectGuid, LfgRoleCheck> RoleChecksStore = new();       //< Current Role checks
        Dictionary<int, LfgProposal> ProposalsStore = new();            //< Current Proposals
        Dictionary<ObjectGuid, LfgPlayerBoot> BootsStore = new();          //< Current player kicks
        Dictionary<ObjectGuid, LFGPlayerData> PlayersStore = new();        //< Player data
        Dictionary<ObjectGuid, LFGGroupData> GroupsStore = new();           //< Group data
    }

    public class LfgJoinResultData
    {
        public LfgJoinResultData(LfgJoinResult _result = LfgJoinResult.Ok, LfgRoleCheckState _state = LfgRoleCheckState.Default)
        {
            result = _result;
            state = _state;
        }

        public LfgJoinResult result;
        public LfgRoleCheckState state;
        public Dictionary<ObjectGuid, Dictionary<int, LfgLockInfoData>> lockmap = new();
        public List<string> playersMissingRequirement = new();
    }

    public class LfgUpdateData
    {
        public LfgUpdateData(LfgUpdateType _type = LfgUpdateType.Default)
        {
            updateType = _type;
            state = LfgState.None;
        }
        public LfgUpdateData(LfgUpdateType _type, List<int> _dungeons)
        {
            updateType = _type;
            state = LfgState.None;
            dungeons = _dungeons;
        }
        public LfgUpdateData(LfgUpdateType _type, LfgState _state, List<int> _dungeons)
        {
            updateType = _type;
            state = _state;
            dungeons = _dungeons;
        }

        public LfgUpdateType updateType;
        public LfgState state;
        public List<int> dungeons = new();
    }

    public class LfgQueueStatusData
    {
        public LfgQueueStatusData(byte _queueId = 0, int _dungeonId = 0, int _waitTime = -1, int _waitTimeAvg = -1, int _waitTimeTank = -1, int _waitTimeHealer = -1,
            int _waitTimeDps = -1, Seconds _queuedTime = default, byte _tanks = 0, byte _healers = 0, byte _dps = 0)
        {
            queueId = _queueId;
            dungeonId = _dungeonId;
            waitTime = _waitTime;
            waitTimeAvg = _waitTimeAvg;
            waitTimeTank = _waitTimeTank;
            waitTimeHealer = _waitTimeHealer;
            waitTimeDps = _waitTimeDps;
            queuedTime = _queuedTime;
            tanks = _tanks;
            healers = _healers;
            dps = _dps;
        }

        public byte queueId;
        public int dungeonId;
        public int waitTime;
        public int waitTimeAvg;
        public int waitTimeTank;
        public int waitTimeHealer;
        public int waitTimeDps;
        public Seconds queuedTime;
        public byte tanks;
        public byte healers;
        public byte dps;
    }

    public class LfgPlayerRewardData
    {
        public LfgPlayerRewardData(int random, int current, bool _done, Quest _quest)
        {
            rdungeonEntry = random;
            sdungeonEntry = current;
            done = _done;
            quest = _quest;
        }

        public int rdungeonEntry;
        public int sdungeonEntry;
        public bool done;
        public Quest quest;
    }

    public class LfgReward
    {
        public LfgReward(int _maxLevel = 0, int _firstQuest = 0, int _otherQuest = 0)
        {
            maxLevel = _maxLevel;
            firstQuest = _firstQuest;
            otherQuest = _otherQuest;
        }

        public int maxLevel;
        public int firstQuest;
        public int otherQuest;
    }

    public class LfgProposalPlayer
    {
        public LfgProposalPlayer()
        {
            role = 0;
            accept = LfgAnswer.Pending;
            group = ObjectGuid.Empty;
        }

        public LfgRoles role;
        public LfgAnswer accept;
        public ObjectGuid group;
    }

    public class LfgProposal
    {
        public LfgProposal(int dungeon = 0)
        {
            id = 0;
            dungeonId = dungeon;
            state = LfgProposalState.Initiating;
            group = ObjectGuid.Empty;
            leader = ObjectGuid.Empty;
            cancelTime = ServerTime.Zero;
            encounters = 0;
            isNew = true;
        }

        public int id;
        public int dungeonId;
        public LfgProposalState state;
        public ObjectGuid group;
        public ObjectGuid leader;
        public ServerTime cancelTime;
        public uint encounters;
        public bool isNew;
        public List<ObjectGuid> queues = new();
        public List<ulong> showorder = new();
        public Dictionary<ObjectGuid, LfgProposalPlayer> players = new();                  // Players data
    }

    public class LfgRoleCheck
    {
        public ServerTime cancelTime;
        public Dictionary<ObjectGuid, LfgRoles> roles = new();
        public LfgRoleCheckState state;
        public List<int> dungeons = new();
        public int rDungeonId;
        public ObjectGuid leader;
    }

    public class LfgPlayerBoot
    {
        public ServerTime cancelTime;
        public bool inProgress;
        public Dictionary<ObjectGuid, LfgAnswer> votes = new();
        public ObjectGuid victim;
        public string reason;
    }

    public class LFGDungeonData
    {
        public LFGDungeonData(LFGDungeonsRecord dbc)
        {
            id = dbc.Id;
            name = dbc.Name[Global.WorldMgr.GetDefaultDbcLocale()];
            map = dbc.MapID;
            type = dbc.TypeID;
            expansion = dbc.ExpansionLevel;
            group = dbc.GroupID;
            minlevel = dbc.MinLevel;
            maxlevel = (byte)dbc.MaxLevel;
            difficulty = dbc.DifficultyID;
            seasonal = dbc.HasAnyFlag(LfgFlags.Seasonal);
        }

        public int id;
        public string name;
        public int map;
        public LfgType type;
        public Expansion expansion;
        public uint group;
        public byte minlevel;
        public byte maxlevel;
        public Difficulty difficulty;
        public bool seasonal;
        public float x, y, z, o;
        public ushort requiredItemLevel;
        public int finalDungeonEncounterId;

        // Helpers
        public int Entry() { return id + ((int)type << 24); }
    }

    public class LfgLockInfoData
    {
        public LfgLockInfoData(LfgLockStatusType _lockStatus = 0, int _requiredItemLevel = 0, int _currentItemLevel = 0)
        {
            lockStatus = _lockStatus;
            requiredItemLevel = (ushort)_requiredItemLevel;
            currentItemLevel = (ushort)_currentItemLevel;
        }

        public LfgLockStatusType lockStatus;
        public ushort requiredItemLevel;
        public ushort currentItemLevel;
    }
}