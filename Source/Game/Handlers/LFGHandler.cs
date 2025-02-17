﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DungeonFinding;
using Game.Entities;
using Game.Groups;
using Game.Networking;
using Game.Networking.Packets;
using System.Collections.Generic;
using System.Linq;
using Game.DataStorage;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.DfJoin)]
        void HandleLfgJoin(DFJoin dfJoin)
        {
            if (!Global.LFGMgr.IsOptionEnabled(LfgOptions.EnableDungeonFinder | LfgOptions.EnableRaidBrowser) ||
                (GetPlayer().GetGroup() != null && GetPlayer().GetGroup().GetLeaderGUID() != GetPlayer().GetGUID() &&
                (GetPlayer().GetGroup().GetMembersCount() == MapConst.MaxGroupSize || !GetPlayer().GetGroup().IsLFGGroup())))
                return;

            if (dfJoin.Slots.Empty())
            {
                Log.outDebug(LogFilter.Lfg, 
                    $"CMSG_DF_JOIN {GetPlayerInfo()} no dungeons selected");
                return;
            }

            List<int> newDungeons = new();
            foreach (var slot in dfJoin.Slots)
            {
                int dungeon = slot & 0x00FFFFFF;
                if (CliDB.LFGDungeonsStorage.ContainsKey(dungeon))
                    newDungeons.Add(dungeon);
            }

            Log.outDebug(LogFilter.Lfg, 
                $"CMSG_DF_JOIN {GetPlayerInfo()} " +
                $"roles: {dfJoin.Roles}, " +
                $"Dungeons: {newDungeons.Count}");

            Global.LFGMgr.JoinLfg(GetPlayer(), dfJoin.Roles, newDungeons);
        }

        [WorldPacketHandler(ClientOpcodes.DfLeave)]
        void HandleLfgLeave(DFLeave dfLeave)
        {
            Group group = GetPlayer().GetGroup();
            int groupNumb = group != null ? 1 : 0;

            Log.outDebug(LogFilter.Lfg, 
                $"CMSG_DF_LEAVE {GetPlayerInfo()} in group: " +
                $"{groupNumb} sent guid {dfLeave.Ticket.RequesterGuid}.");

            // Check cheating - only leader can leave the queue
            if (group == null || group.GetLeaderGUID() == dfLeave.Ticket.RequesterGuid)
                Global.LFGMgr.LeaveLfg(dfLeave.Ticket.RequesterGuid);
        }

        [WorldPacketHandler(ClientOpcodes.DfProposalResponse)]
        void HandleLfgProposalResult(DFProposalResponse dfProposalResponse)
        {
            Log.outDebug(LogFilter.Lfg, 
                $"CMSG_LFG_PROPOSAL_RESULT {GetPlayerInfo()} proposal: " +
                $"{dfProposalResponse.ProposalID} accept: {dfProposalResponse.Accepted}");
            
            Global.LFGMgr.UpdateProposal(dfProposalResponse.ProposalID, GetPlayer().GetGUID(), dfProposalResponse.Accepted);
        }

        [WorldPacketHandler(ClientOpcodes.DfSetRoles)]
        void HandleLfgSetRoles(DFSetRoles dfSetRoles)
        {
            ObjectGuid guid = GetPlayer().GetGUID();
            Group group = GetPlayer().GetGroup();
            if (group == null)
            {
                Log.outDebug(LogFilter.Lfg, 
                    $"CMSG_DF_SET_ROLES {GetPlayerInfo()} Not in group");
                return;
            }
            ObjectGuid gguid = group.GetGUID();

            Log.outDebug(LogFilter.Lfg, 
                $"CMSG_DF_SET_ROLES: Group {gguid}, " +
                $"Player {GetPlayerInfo()}, " +
                $"Roles: {dfSetRoles.RolesDesired}");

            Global.LFGMgr.UpdateRoleCheck(gguid, guid, dfSetRoles.RolesDesired);
        }

        [WorldPacketHandler(ClientOpcodes.DfBootPlayerVote)]
        void HandleLfgSetBootVote(DFBootPlayerVote dfBootPlayerVote)
        {
            ObjectGuid guid = GetPlayer().GetGUID();
            Log.outDebug(LogFilter.Lfg, 
                $"CMSG_LFG_SET_BOOT_VOTE {GetPlayerInfo()} agree: {dfBootPlayerVote.Vote}");

            Global.LFGMgr.UpdateBoot(guid, dfBootPlayerVote.Vote);
        }

        [WorldPacketHandler(ClientOpcodes.DfTeleport)]
        void HandleLfgTeleport(DFTeleport dfTeleport)
        {
            Log.outDebug(LogFilter.Lfg, 
                $"CMSG_DF_TELEPORT {GetPlayerInfo()} out: {dfTeleport.TeleportOut}");

            Global.LFGMgr.TeleportPlayer(GetPlayer(), dfTeleport.TeleportOut, true);
        }

        [WorldPacketHandler(ClientOpcodes.DfGetSystemInfo, Processing = PacketProcessing.ThreadSafe)]
        void HandleDfGetSystemInfo(DFGetSystemInfo dfGetSystemInfo)
        {
            Log.outDebug(LogFilter.Lfg, 
                $"CMSG_LFG_Lock_INFO_REQUEST {GetPlayerInfo()} " +
                $"for {(dfGetSystemInfo.Player ? "player" : "party")}");

            if (dfGetSystemInfo.Player)
                SendLfgPlayerLockInfo();
            else
                SendLfgPartyLockInfo();
        }

        [WorldPacketHandler(ClientOpcodes.DfGetJoinStatus, Processing = PacketProcessing.ThreadSafe)]
        void HandleDfGetJoinStatus(DFGetJoinStatus packet)
        {
            if (!GetPlayer().IsUsingLfg())
                return;

            ObjectGuid guid = GetPlayer().GetGUID();
            LfgUpdateData updateData = Global.LFGMgr.GetLfgStatus(guid);

            if (GetPlayer().GetGroup() != null)
            {
                SendLfgUpdateStatus(updateData, true);
                updateData.dungeons.Clear();
                SendLfgUpdateStatus(updateData, false);
            }
            else
            {
                SendLfgUpdateStatus(updateData, false);
                updateData.dungeons.Clear();
                SendLfgUpdateStatus(updateData, true);
            }
        }

        public void SendLfgPlayerLockInfo()
        {
            // Get Random dungeons that can be done at a certain level and expansion
            int level = GetPlayer().GetLevel();
            var randomDungeons = Global.LFGMgr.GetRandomAndSeasonalDungeons(level, GetExpansion());

            LfgPlayerInfo lfgPlayerInfo = new();

            // Get player locked Dungeons
            foreach (var locked in Global.LFGMgr.GetLockedDungeons(_player.GetGUID()))
                lfgPlayerInfo.BlackList.Slot.Add(new LFGBlackListSlot(locked.Key, locked.Value.lockStatus, locked.Value.requiredItemLevel, locked.Value.currentItemLevel, 0));

            foreach (var slot in randomDungeons)
            {
                var playerDungeonInfo = new LfgPlayerDungeonInfo();
                playerDungeonInfo.Slot = slot;
                playerDungeonInfo.CompletionQuantity = 1;
                playerDungeonInfo.CompletionLimit = 1;
                playerDungeonInfo.CompletionCurrencyID = 0;
                playerDungeonInfo.SpecificQuantity = 0;
                playerDungeonInfo.SpecificLimit = 1;
                playerDungeonInfo.OverallQuantity = 0;
                playerDungeonInfo.OverallLimit = 1;
                playerDungeonInfo.PurseWeeklyQuantity = 0;
                playerDungeonInfo.PurseWeeklyLimit = 0;
                playerDungeonInfo.PurseQuantity = 0;
                playerDungeonInfo.PurseLimit = 0;
                playerDungeonInfo.Quantity = 1;
                playerDungeonInfo.CompletedMask = 0;
                playerDungeonInfo.EncounterMask = 0;

                LfgReward reward = Global.LFGMgr.GetRandomDungeonReward(slot, level);
                if (reward != null)
                {
                    Quest quest = Global.ObjectMgr.GetQuestTemplate(reward.firstQuest);
                    if (quest != null)
                    {
                        playerDungeonInfo.FirstReward = !GetPlayer().CanRewardQuest(quest, false);
                        if (!playerDungeonInfo.FirstReward)
                            quest = Global.ObjectMgr.GetQuestTemplate(reward.otherQuest);

                        if (quest != null)
                        {
                            playerDungeonInfo.Rewards.RewardMoney = _player.GetQuestMoneyReward(quest);
                            playerDungeonInfo.Rewards.RewardXP = _player.GetQuestXPReward(quest);
                            for (byte i = 0; i < SharedConst.QuestRewardItemCount; ++i)
                            {
                                int itemId = quest.RewardItemId[i];
                                if (itemId != 0)
                                    playerDungeonInfo.Rewards.Item.Add(new LfgPlayerQuestRewardItem(itemId, quest.RewardItemCount[i]));
                            }

                            for (byte i = 0; i < SharedConst.QuestRewardCurrencyCount; ++i)
                            {
                                int curencyId = quest.RewardCurrencyId[i];
                                if (curencyId != 0)
                                    playerDungeonInfo.Rewards.Currency.Add(new LfgPlayerQuestRewardCurrency(curencyId, quest.RewardCurrencyCount[i]));
                            }
                        }
                    }
                }

                lfgPlayerInfo.Dungeons.Add(playerDungeonInfo);
            }

            SendPacket(lfgPlayerInfo);
        }

        public void SendLfgPartyLockInfo()
        {
            ObjectGuid guid = GetPlayer().GetGUID();
            Group group = GetPlayer().GetGroup();
            if (group == null)
                return;

            LfgPartyInfo lfgPartyInfo = new();

            // Get the Locked dungeons of the other party members
            for (GroupReference refe = group.GetFirstMember(); refe != null; refe = refe.Next())
            {
                Player plrg = refe.GetSource();
                if (plrg == null)
                    continue;

                ObjectGuid pguid = plrg.GetGUID();
                if (pguid == guid)
                    continue;

                LFGBlackList lfgBlackList = new();
                lfgBlackList.PlayerGuid = pguid;
                foreach (var locked in Global.LFGMgr.GetLockedDungeons(pguid))
                    lfgBlackList.Slot.Add(new LFGBlackListSlot(locked.Key, locked.Value.lockStatus, locked.Value.requiredItemLevel, locked.Value.currentItemLevel, 0));

                lfgPartyInfo.Player.Add(lfgBlackList);
            }

            Log.outDebug(LogFilter.Lfg, $"SMSG_LFG_PARTY_INFO {GetPlayerInfo()}");
            SendPacket(lfgPartyInfo);
        }

        public void SendLfgUpdateStatus(LfgUpdateData updateData, bool party)
        {
            bool join = false;
            bool queued = false;

            switch (updateData.updateType)
            {
                case LfgUpdateType.JoinQueueInitial:            // Joined queue outside the dungeon
                    join = true;
                    break;
                case LfgUpdateType.JoinQueue:
                case LfgUpdateType.AddedToQueue:                // Rolecheck Success
                    join = true;
                    queued = true;
                    break;
                case LfgUpdateType.ProposalBegin:
                    join = true;
                    break;
                case LfgUpdateType.UpdateStatus:
                    join = updateData.state != LfgState.Rolecheck && updateData.state != LfgState.None;
                    queued = updateData.state == LfgState.Queued;
                    break;
                default:
                    break;
            }

            LFGUpdateStatus lfgUpdateStatus = new();

            RideTicket ticket = Global.LFGMgr.GetTicket(_player.GetGUID());
            if (ticket != null)
                lfgUpdateStatus.Ticket = ticket;

            lfgUpdateStatus.SubType = (byte)LfgQueueType.Dungeon; // other types not implemented
            lfgUpdateStatus.Reason = (byte)updateData.updateType;

            foreach (var dungeonId in updateData.dungeons)
                lfgUpdateStatus.Slots.Add(Global.LFGMgr.GetLFGDungeonEntry(dungeonId));

            lfgUpdateStatus.RequestedRoles = (byte)Global.LFGMgr.GetRoles(_player.GetGUID());
            //lfgUpdateStatus.SuspendedPlayers;
            lfgUpdateStatus.IsParty = party;
            lfgUpdateStatus.NotifyUI = true;
            lfgUpdateStatus.Joined = join;
            lfgUpdateStatus.LfgJoined = updateData.updateType != LfgUpdateType.RemovedFromQueue;
            lfgUpdateStatus.Queued = queued;
            lfgUpdateStatus.QueueMapID = Global.LFGMgr.GetDungeonMapId(_player.GetGUID());

            SendPacket(lfgUpdateStatus);
        }

        public void SendLfgRoleChosen(ObjectGuid guid, LfgRoles roles)
        {
            RoleChosen roleChosen = new();
            roleChosen.Player = guid;
            roleChosen.RoleMask = roles;
            roleChosen.Accepted = roles > 0;
            SendPacket(roleChosen);
        }

        public void SendLfgRoleCheckUpdate(LfgRoleCheck roleCheck)
        {
            List<int> dungeons = new();
            if (roleCheck.rDungeonId != 0)
                dungeons.Add(roleCheck.rDungeonId);
            else
                dungeons = roleCheck.dungeons;

            Log.outDebug(LogFilter.Lfg, $"SMSG_LFG_ROLE_CHECK_UPDATE {GetPlayerInfo()}");

            LFGRoleCheckUpdate lfgRoleCheckUpdate = new();
            lfgRoleCheckUpdate.PartyIndex = 127;
            lfgRoleCheckUpdate.RoleCheckStatus = (byte)roleCheck.state;
            lfgRoleCheckUpdate.IsBeginning = roleCheck.state == LfgRoleCheckState.Initialiting;

            foreach (var dungeonId in dungeons)
                lfgRoleCheckUpdate.JoinSlots.Add(Global.LFGMgr.GetLFGDungeonEntry(dungeonId));

            lfgRoleCheckUpdate.GroupFinderActivityID = 0;
            if (!roleCheck.roles.Empty())
            {
                // Leader info MUST be sent 1st :S
                byte roles = (byte)roleCheck.roles.Find(roleCheck.leader).Value;

                lfgRoleCheckUpdate.Members.Add(new LFGRoleCheckUpdateMember(roleCheck.leader, roles, 
                    Global.CharacterCacheStorage.GetCharacterCacheByGuid(roleCheck.leader).Level, roles > 0));

                foreach (var it in roleCheck.roles)
                {
                    if (it.Key == roleCheck.leader)
                        continue;

                    roles = (byte)it.Value;

                    lfgRoleCheckUpdate.Members.Add(new LFGRoleCheckUpdateMember(it.Key, roles, 
                        Global.CharacterCacheStorage.GetCharacterCacheByGuid(it.Key).Level, roles > 0));
                }
            }

            SendPacket(lfgRoleCheckUpdate);
        }

        public void SendLfgJoinResult(LfgJoinResultData joinData)
        {
            LFGJoinResult lfgJoinResult = new();

            RideTicket ticket = Global.LFGMgr.GetTicket(GetPlayer().GetGUID());
            if (ticket != null)
                lfgJoinResult.Ticket = ticket;

            lfgJoinResult.Result = (byte)joinData.result;
            if (joinData.result == LfgJoinResult.RoleCheckFailed)
                lfgJoinResult.ResultDetail = (byte)joinData.state;
            else if (joinData.result == LfgJoinResult.NoSlots)
                lfgJoinResult.BlackListNames = joinData.playersMissingRequirement;

            foreach (var it in joinData.lockmap)
            {
                var blackList = new LFGBlackListPkt();
                blackList.PlayerGuid = it.Key;

                foreach (var lockInfo in it.Value)
                {
                    Log.outTrace(LogFilter.Lfg, 
                        $"SendLfgJoinResult:: {it.Key} DungeonID: {lockInfo.Key & 0x00FFFFFF} " +
                        $"Lock status: {lockInfo.Value.lockStatus} " +
                        $"Required itemLevel: {lockInfo.Value.requiredItemLevel} " +
                        $"Current itemLevel: {lockInfo.Value.currentItemLevel}");

                    blackList.Slot.Add(new LFGBlackListSlot(lockInfo.Key, lockInfo.Value.lockStatus, lockInfo.Value.requiredItemLevel, lockInfo.Value.currentItemLevel, 0));
                }

                lfgJoinResult.BlackList.Add(blackList);
            }

            SendPacket(lfgJoinResult);
        }

        public void SendLfgQueueStatus(LfgQueueStatusData queueData)
        {
            Log.outDebug(LogFilter.Lfg, 
                $"SMSG_LFG_QUEUE_STATUS {GetPlayerInfo()} " +
                $"state: {Global.LFGMgr.GetState(GetPlayer().GetGUID())} " +
                $"dungeon: {queueData.dungeonId}, " +
                $"waitTime: {queueData.waitTime}, " +
                $"avgWaitTime: {queueData.waitTimeAvg}, " +
                $"waitTimeTanks: {queueData.waitTimeTank}, " +
                $"waitTimeHealer: {queueData.waitTimeHealer}, " +
                $"waitTimeDps: {queueData.waitTimeDps}, " +
                $"queuedTime: {queueData.queuedTime}, " +
                $"tanks: {queueData.tanks}, " +
                $"healers: {queueData.healers}, " +
                $"dps: {queueData.dps}");

            LFGQueueStatus lfgQueueStatus = new();

            RideTicket ticket = Global.LFGMgr.GetTicket(GetPlayer().GetGUID());
            if (ticket != null)
                lfgQueueStatus.Ticket = ticket;
            lfgQueueStatus.Slot = queueData.queueId;
            lfgQueueStatus.AvgWaitTimeMe = (uint)queueData.waitTime;
            lfgQueueStatus.AvgWaitTime = (uint)queueData.waitTimeAvg;
            lfgQueueStatus.AvgWaitTimeByRole[0] = (uint)queueData.waitTimeTank;
            lfgQueueStatus.AvgWaitTimeByRole[1] = (uint)queueData.waitTimeHealer;
            lfgQueueStatus.AvgWaitTimeByRole[2] = (uint)queueData.waitTimeDps;
            lfgQueueStatus.LastNeeded[0] = queueData.tanks;
            lfgQueueStatus.LastNeeded[1] = queueData.healers;
            lfgQueueStatus.LastNeeded[2] = queueData.dps;
            lfgQueueStatus.QueuedTime = queueData.queuedTime;

            SendPacket(lfgQueueStatus);
        }

        public void SendLfgPlayerReward(LfgPlayerRewardData rewardData)
        {
            if (rewardData.rdungeonEntry == 0 || rewardData.sdungeonEntry == 0 || rewardData.quest == null)
                return;

            Log.outDebug(LogFilter.Lfg, 
                $"SMSG_LFG_PLAYER_REWARD {GetPlayerInfo()} " +
                $"rdungeonEntry: {rewardData.rdungeonEntry}, " +
                $"sdungeonEntry: {rewardData.sdungeonEntry}, " +
                $"done: {rewardData.done}");

            LFGPlayerReward lfgPlayerReward = new();
            lfgPlayerReward.QueuedSlot = rewardData.rdungeonEntry;
            lfgPlayerReward.ActualSlot = rewardData.sdungeonEntry;
            lfgPlayerReward.RewardMoney = GetPlayer().GetQuestMoneyReward(rewardData.quest);
            lfgPlayerReward.AddedXP = GetPlayer().GetQuestXPReward(rewardData.quest);

            for (byte i = 0; i < SharedConst.QuestRewardItemCount; ++i)
            {
                int itemId = rewardData.quest.RewardItemId[i];
                if (itemId != 0)
                    lfgPlayerReward.Rewards.Add(new LFGPlayerRewards(itemId, rewardData.quest.RewardItemCount[i], 0, false));
            }

            for (byte i = 0; i < SharedConst.QuestRewardCurrencyCount; ++i)
            {
                int currencyId = rewardData.quest.RewardCurrencyId[i];
                if (currencyId != 0)
                    lfgPlayerReward.Rewards.Add(new LFGPlayerRewards(currencyId, rewardData.quest.RewardCurrencyCount[i], 0, true));
            }

            SendPacket(lfgPlayerReward);
        }

        public void SendLfgBootProposalUpdate(LfgPlayerBoot boot)
        {
            LfgAnswer playerVote = boot.votes.LookupByKey(GetPlayer().GetGUID());
            byte votesNum = 0;
            byte agreeNum = 0;
            Seconds secsleft = (Seconds)(boot.cancelTime - LoopTime.ServerTime);
            foreach (var it in boot.votes)
            {
                if (it.Value != LfgAnswer.Pending)
                {
                    ++votesNum;
                    if (it.Value == LfgAnswer.Agree)
                        ++agreeNum;
                }
            }
            Log.outDebug(LogFilter.Lfg, 
                $"SMSG_LFG_BOOT_PROPOSAL_UPDATE {GetPlayerInfo()} " +
                $"inProgress: {boot.inProgress} - " +
                $"didVote: {playerVote != LfgAnswer.Pending} - " +
                $"agree: {playerVote == LfgAnswer.Agree} - " +
                $"victim: {boot.victim} " +
                $"votes: {votesNum} - " +
                $"agrees: {agreeNum} - " +
                $"left: {secsleft} - " +
                $"needed: {SharedConst.LFGKickVotesNeeded} - " +
                $"reason {boot.reason}");

            LfgBootPlayer lfgBootPlayer = new();
            lfgBootPlayer.Info.VoteInProgress = boot.inProgress;                                 // Vote in progress
            lfgBootPlayer.Info.VotePassed = agreeNum >= SharedConst.LFGKickVotesNeeded;    // Did succeed
            lfgBootPlayer.Info.MyVoteCompleted = playerVote != LfgAnswer.Pending;           // Did Vote
            lfgBootPlayer.Info.MyVote = playerVote == LfgAnswer.Agree;             // Agree
            lfgBootPlayer.Info.Target = boot.victim;                                    // Victim GUID
            lfgBootPlayer.Info.TotalVotes = votesNum;                                       // Total Votes
            lfgBootPlayer.Info.BootVotes = agreeNum;                                       // Agree Count
            lfgBootPlayer.Info.TimeLeft = secsleft;                                       // Time Left
            lfgBootPlayer.Info.VotesNeeded = SharedConst.LFGKickVotesNeeded;               // Needed Votes
            lfgBootPlayer.Info.Reason = boot.reason;                                    // Kick reason
            SendPacket(lfgBootPlayer);
        }

        public void SendLfgProposalUpdate(LfgProposal proposal)
        {
            ObjectGuid playerGuid = GetPlayer().GetGUID();
            ObjectGuid guildGuid = proposal.players.LookupByKey(playerGuid).group;
            bool silent = !proposal.isNew && guildGuid == proposal.group;
            int dungeonEntry = proposal.dungeonId;

            Log.outDebug(LogFilter.Lfg,
                $"SMSG_LFG_PROPOSAL_UPDATE {GetPlayerInfo()} state: {proposal.state}");

            // show random dungeon if player selected random dungeon and it's not lfg group
            if (!silent)
            {
                List<int> playerDungeons = Global.LFGMgr.GetSelectedDungeons(playerGuid);
                if (!playerDungeons.Contains(proposal.dungeonId))
                    dungeonEntry = playerDungeons.First();
            }

            LFGProposalUpdate lfgProposalUpdate = new();

            RideTicket ticket = Global.LFGMgr.GetTicket(GetPlayer().GetGUID());
            if (ticket != null)
                lfgProposalUpdate.Ticket = ticket;
            lfgProposalUpdate.InstanceID = 0;
            lfgProposalUpdate.ProposalID = proposal.id;
            lfgProposalUpdate.Slot = Global.LFGMgr.GetLFGDungeonEntry(dungeonEntry);
            lfgProposalUpdate.State = proposal.state;
            lfgProposalUpdate.CompletedMask = proposal.encounters;
            lfgProposalUpdate.ValidCompletedMask = true;
            lfgProposalUpdate.ProposalSilent = silent;
            lfgProposalUpdate.IsRequeue = !proposal.isNew;

            foreach (var pair in proposal.players)
            {
                var proposalPlayer = new LFGProposalUpdatePlayer();
                proposalPlayer.Roles = (byte)pair.Value.role;
                proposalPlayer.Me = (pair.Key == playerGuid);
                proposalPlayer.MyParty = !pair.Value.group.IsEmpty() && pair.Value.group == proposal.group;
                proposalPlayer.SameParty = !pair.Value.group.IsEmpty() && pair.Value.group == guildGuid;
                proposalPlayer.Responded = (pair.Value.accept != LfgAnswer.Pending);
                proposalPlayer.Accepted = (pair.Value.accept == LfgAnswer.Agree);

                lfgProposalUpdate.Players.Add(proposalPlayer);
            }

            SendPacket(lfgProposalUpdate);
        }

        public void SendLfgDisabled()
        {
            SendPacket(new LfgDisabled());
        }

        public void SendLfgOfferContinue(int dungeonEntry)
        {
            Log.outDebug(LogFilter.Lfg, 
                $"SMSG_LFG_OFFER_CONTINUE {GetPlayerInfo()} dungeon entry: {dungeonEntry}");
            SendPacket(new LfgOfferContinue(Global.LFGMgr.GetLFGDungeonEntry(dungeonEntry)));
        }

        public void SendLfgTeleportError(LfgTeleportResult err)
        {
            Log.outDebug(LogFilter.Lfg, 
                $"SMSG_LFG_TELEPORT_DENIED {GetPlayerInfo()} reason: {err}");
            SendPacket(new LfgTeleportDenied(err));
        }
    }
}