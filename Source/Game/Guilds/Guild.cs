﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Achievements;
using Game.DataStorage;
using Game.Entities;
using Game.Groups;
using Game.Networking;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Guilds
{
    public class Guild
    {
        public Guild()
        {
            m_achievementSys = new GuildAchievementMgr(this);

            for (var i = 0; i < m_bankEventLog.Length; ++i)
                m_bankEventLog[i] = new LogHolder<BankEventLogEntry>();
        }

        public bool Create(Player pLeader, string name)
        {
            // Check if guild with such name already exists
            if (Global.GuildMgr.GetGuildByName(name) != null)
                return false;

            WorldSession pLeaderSession = pLeader.GetSession();
            if (pLeaderSession == null)
                return false;

            m_id = Global.GuildMgr.GenerateGuildId();
            m_leaderGuid = pLeader.GetGUID();
            m_name = name;
            m_info = "";
            m_motd = "No message set.";
            m_bankMoney = 0;
            m_createdDate = LoopTime.ServerTime;

            Log.outDebug(LogFilter.Guild,
                $"GUILD: creating guild [{name}] for leader {pLeader.GetName()} ({m_leaderGuid})");

            SQLTransaction trans = new();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_MEMBERS);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            byte index = 0;
            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD);
            stmt.SetInt64(index, m_id);
            stmt.SetString(++index, name);
            stmt.SetInt64(++index, m_leaderGuid.GetCounter());
            stmt.SetString(++index, m_info);
            stmt.SetString(++index, m_motd);
            stmt.SetInt64(++index, (UnixTime64)m_createdDate);
            stmt.SetInt32(++index, m_emblemInfo.GetStyle());
            stmt.SetInt32(++index, m_emblemInfo.GetColor());
            stmt.SetInt32(++index, m_emblemInfo.GetBorderStyle());
            stmt.SetInt32(++index, m_emblemInfo.GetBorderColor());
            stmt.SetInt32(++index, m_emblemInfo.GetBackgroundColor());
            stmt.SetInt64(++index, m_bankMoney);
            trans.Append(stmt);

            _CreateDefaultGuildRanks(trans, pLeaderSession.GetSessionDbLocaleIndex()); // Create default ranks
            bool ret = AddMember(trans, m_leaderGuid, GuildRankId.GuildMaster);                  // Add guildmaster

            DB.Characters.CommitTransaction(trans);
            if (ret)
            {
                Member leader = GetMember(m_leaderGuid);
                if (leader != null)
                    SendEventNewLeader(leader, null);

                Global.ScriptMgr.OnGuildCreate(this, pLeader, name);
            }

            return ret;
        }

        public void Disband()
        {
            Global.ScriptMgr.OnGuildDisband(this);

            BroadcastPacket(new GuildEventDisbanded());

            SQLTransaction trans = new();
            while (!m_members.Empty())
            {
                var member = m_members.First();
                DeleteMember(trans, member.Value.GetGUID(), true);
            }

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_RANKS);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_TABS);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            // Free bank tab used memory and delete items stored in them
            _DeleteBankItems(trans, true);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_ITEMS);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_RIGHTS);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_EVENTLOGS);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_EVENTLOGS);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            DB.Characters.CommitTransaction(trans);

            Global.GuildMgr.RemoveGuild(m_id);
        }

        public void SaveToDB()
        {
            SQLTransaction trans = new();

            GetAchievementMgr().SaveToDB(trans);

            DB.Characters.CommitTransaction(trans);
        }

        public void UpdateMemberData(Player player, GuildMemberData dataid, int value)
        {
            Member member = GetMember(player.GetGUID());
            if (member != null)
            {
                switch (dataid)
                {
                    case GuildMemberData.ZoneId:
                        member.SetZoneId(value);
                        break;
                    case GuildMemberData.AchievementPoints:
                        member.SetAchievementPoints(value);
                        break;
                    case GuildMemberData.Level:
                        member.SetLevel(value);
                        break;
                    default:
                        Log.outError(LogFilter.Guild,
                            $"Guild.UpdateMemberData: Called with incorrect DATAID {dataid} (value {value})");
                        return;
                }
            }
        }

        void OnPlayerStatusChange(Player player, GuildMemberFlags flag, bool state)
        {
            Member member = GetMember(player.GetGUID());
            if (member != null)
            {
                if (state)
                    member.AddFlag(flag);
                else
                    member.RemoveFlag(flag);
            }
        }

        public bool SetName(string name)
        {
            if (m_name == name || string.IsNullOrEmpty(name) || name.Length > 24
                || Global.ObjectMgr.IsReservedName(name) || !ObjectManager.IsValidCharterName(name))
            {
                return false;
            }

            m_name = name;
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_NAME);
            stmt.SetString(0, m_name);
            stmt.SetInt64(1, GetId());
            DB.Characters.Execute(stmt);

            GuildNameChanged guildNameChanged = new();
            guildNameChanged.GuildGUID = GetGUID();
            guildNameChanged.GuildName = m_name;
            BroadcastPacket(guildNameChanged);

            return true;
        }

        public void HandleRoster(WorldSession session = null)
        {
            GuildRoster roster = new();
            roster.NumAccounts = m_accountsNumber;
            roster.CreateDate = (RealmTime)m_createdDate;
            //roster.CreateDate += session.GetTimezoneOffset();
            roster.GuildFlags = 0;

            bool sendOfficerNote = _HasRankRight(session.GetPlayer(), GuildRankRights.ViewOffNote);
            foreach (var member in m_members.Values)
            {
                GuildRosterMemberData memberData = new();

                memberData.Guid = member.GetGUID();
                memberData.RankID = (int)member.GetRankId();
                memberData.AreaID = member.GetZoneId();
                memberData.PersonalAchievementPoints = member.GetAchievementPoints();
                memberData.GuildReputation = member.GetTotalReputation();
                memberData.InactiveDays = member.GetInactiveDays();

                memberData.VirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
                memberData.Status = (byte)member.GetFlags();
                memberData.Level = member.GetLevel();
                memberData.ClassID = (byte)member.GetClass();
                memberData.Gender = (byte)member.GetGender();
                memberData.RaceID = (byte)member.GetRace();

                memberData.Authenticated = false;
                memberData.SorEligible = false;

                memberData.Name = member.GetName();
                memberData.Note = member.GetPublicNote();
                if (sendOfficerNote)
                    memberData.OfficerNote = member.GetOfficerNote();

                roster.MemberData.Add(memberData);
            }

            roster.WelcomeText = m_motd;
            roster.InfoText = m_info;

            if (session != null)
                session.SendPacket(roster);
        }

        public void SendQueryResponse(WorldSession session)
        {
            QueryGuildInfoResponse response = new();
            response.GuildGUID = GetGUID();

            response.Info = new();
            response.Info.GuildGuid = GetGUID();
            response.Info.VirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();

            response.Info.EmblemStyle = m_emblemInfo.GetStyle();
            response.Info.EmblemColor = m_emblemInfo.GetColor();
            response.Info.BorderStyle = m_emblemInfo.GetBorderStyle();
            response.Info.BorderColor = m_emblemInfo.GetBorderColor();
            response.Info.BackgroundColor = m_emblemInfo.GetBackgroundColor();

            foreach (RankInfo rankInfo in m_ranks)
                response.Info.Ranks.Add(new QueryGuildInfoResponse.GuildInfo.RankInfo((byte)rankInfo.GetId(), (byte)rankInfo.GetOrder(), rankInfo.GetName()));

            response.Info.GuildName = m_name;

            session.SendPacket(response);
        }

        public void SendGuildRankInfo(WorldSession session)
        {
            GuildRanks ranks = new();

            foreach (RankInfo rankInfo in m_ranks)
            {
                GuildRankData rankData = new();

                rankData.RankID = rankInfo.GetId();
                rankData.RankOrder = rankInfo.GetOrder();
                rankData.Flags = rankInfo.GetRights();
                rankData.WithdrawGoldLimit = _GetRankBankGoldPerDay(rankInfo.GetId());
                rankData.RankName = rankInfo.GetName();

                for (byte j = 0; j < _GetPurchasedTabsSize(); ++j)
                {
                    rankData.TabFlags[j] = rankInfo.GetBankTabRights(j);
                    rankData.TabWithdrawItemLimit[j] = rankInfo.GetBankTabSlotsPerDay(j);
                }

                ranks.Ranks.Add(rankData);
            }

            session.SendPacket(ranks);
        }

        public void HandleSetAchievementTracking(WorldSession session, List<int> achievementIds)
        {
            Player player = session.GetPlayer();

            Member member = GetMember(player.GetGUID());
            if (member != null)
            {
                List<int> criteriaIds = new();
                foreach (var achievementId in achievementIds)
                {
                    var achievement = CliDB.AchievementStorage.LookupByKey(achievementId);
                    if (achievement != null)
                    {
                        CriteriaTree tree = Global.CriteriaMgr.GetCriteriaTree(achievement.CriteriaTree);
                        if (tree != null)
                        {
                            CriteriaManager.WalkCriteriaTree(tree, node =>
                            {
                                if (node.Criteria != null)
                                    criteriaIds.Add(node.Criteria.Id);
                            });
                        }
                    }
                }

                GetAchievementMgr().SendAllTrackedCriterias(player, criteriaIds);
                member.SetTrackedCriteriaIds(criteriaIds);
            }
        }

        public void HandleGetAchievementMembers(WorldSession session, int achievementId)
        {
            GetAchievementMgr().SendAchievementMembers(session.GetPlayer(), achievementId);
        }

        public void HandleSetMOTD(WorldSession session, string motd)
        {
            if (m_motd == motd)
                return;

            // Player must have rights to set MOTD
            if (!_HasRankRight(session.GetPlayer(), GuildRankRights.SetMotd))
            {
                SendCommandResult(session, GuildCommandType.EditMOTD, GuildCommandError.Permissions);
                return;
            }

            m_motd = motd;

            Global.ScriptMgr.OnGuildMOTDChanged(this, motd);

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_MOTD);
            stmt.SetString(0, motd);
            stmt.SetInt64(1, m_id);
            DB.Characters.Execute(stmt);

            SendEventMOTD(session, true);
        }

        public void HandleSetInfo(WorldSession session, string info)
        {
            if (m_info == info)
                return;

            // Player must have rights to set guild's info
            if (_HasRankRight(session.GetPlayer(), GuildRankRights.ModifyGuildInfo))
            {
                m_info = info;

                Global.ScriptMgr.OnGuildInfoChanged(this, info);

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_INFO);
                stmt.SetString(0, info);
                stmt.SetInt64(1, m_id);
                DB.Characters.Execute(stmt);
            }
        }

        public void HandleSetEmblem(WorldSession session, EmblemInfo emblemInfo)
        {
            Player player = session.GetPlayer();
            if (!_IsLeader(player))
                SendSaveEmblemResult(session, GuildEmblemError.NotGuildMaster); // "Only guild leaders can create emblems."
            else if (!player.HasEnoughMoney(10 * MoneyConstants.Gold))
                SendSaveEmblemResult(session, GuildEmblemError.NotEnoughMoney); // "You can't afford to do that."
            else
            {
                player.ModifyMoney(-(long)10 * MoneyConstants.Gold);

                m_emblemInfo = emblemInfo;
                m_emblemInfo.SaveToDB(m_id);

                SendSaveEmblemResult(session, GuildEmblemError.Success); // "Guild Emblem saved."

                SendQueryResponse(session);
            }
        }

        public void HandleSetNewGuildMaster(WorldSession session, string name, bool isSelfPromote)
        {
            Player player = session.GetPlayer();
            Member oldGuildMaster = GetMember(GetLeaderGUID());

            Member newGuildMaster;
            if (isSelfPromote)
            {
                newGuildMaster = GetMember(player.GetGUID());
                if (newGuildMaster == null)
                    return;

                RankInfo oldRank = GetRankInfo(newGuildMaster.GetRankId());

                // only second highest rank can take over guild
                if (oldRank.GetOrder() != (GuildRankOrder)1 || oldGuildMaster.GetInactiveDays() < GuildConst.MasterDethroneInactiveDays)
                {
                    SendCommandResult(session, GuildCommandType.ChangeLeader, GuildCommandError.Permissions);
                    return;
                }
            }
            else
            {
                if (!_IsLeader(player))
                {
                    SendCommandResult(session, GuildCommandType.ChangeLeader, GuildCommandError.Permissions);
                    return;
                }

                newGuildMaster = GetMember(name);
                if (newGuildMaster == null)
                    return;
            }

            SQLTransaction trans = new();

            _SetLeader(trans, newGuildMaster);
            oldGuildMaster.ChangeRank(trans, _GetLowestRankId());

            SendEventNewLeader(newGuildMaster, oldGuildMaster, isSelfPromote);

            DB.Characters.CommitTransaction(trans);
        }

        public void UpdateBankQueryResult(int tabId, BankTab tabInfo)
        {
            for (byte i = 0; i < _GetPurchasedTabsSize(); i++)
                m_bankTabs[i].UpdateTabInfoInBankQueryResult(tabId, tabInfo);
        }

        public void HandleSetBankTabInfo(WorldSession session, byte tabId, string name, string icon)
        {
            BankTab tab = GetBankTab(tabId);
            if (tab == null)
            {
                Log.outError(LogFilter.Guild,
                    $"Guild.HandleSetBankTabInfo: " +
                    $"Player {session.GetPlayer().GetName()} trying to change bank tab info " +
                    $"from unexisting tab {tabId}.");
                return;
            }

            tab.SetInfo(name, icon);

            GuildEventTabModified packet = new();
            packet.Tab = tabId;
            packet.Name = name;
            packet.Icon = icon;
            UpdateBankQueryResult(tabId, tab);
            BroadcastPacket(packet);
        }

        public void HandleSetMemberNote(WorldSession session, string note, ObjectGuid guid, bool isPublic)
        {
            // Player must have rights to set public/officer note
            if (!_HasRankRight(session.GetPlayer(), isPublic ? GuildRankRights.EditPublicNote : GuildRankRights.EditOffNote))
            {
                SendCommandResult(session, GuildCommandType.EditPublicNote, GuildCommandError.Permissions);
                return;
            }

            Member member = GetMember(guid);
            if (member != null)
            {
                if (isPublic)
                    member.SetPublicNote(note);
                else
                    member.SetOfficerNote(note);

                GuildMemberUpdateNote updateNote = new();
                updateNote.Member = guid;
                updateNote.IsPublic = isPublic;
                updateNote.Note = note;
                BroadcastPacket(updateNote);
            }
        }

        public void HandleSetRankInfo(WorldSession session, GuildRankId rankId, string name, GuildRankRights rights, int goldPerDay, GuildBankRightsAndSlots[] rightsAndSlots)
        {
            // Only leader can modify ranks
            if (!_IsLeader(session.GetPlayer()))
            {
                SendCommandResult(session, GuildCommandType.ChangeRank, GuildCommandError.Permissions);
                return;
            }

            RankInfo rankInfo = GetRankInfo(rankId);
            if (rankInfo != null)
            {
                rankInfo.SetName(name);
                rankInfo.SetRights(rights);
                _SetRankBankGoldPerDay(rankId, goldPerDay);

                foreach (var rightsAndSlot in rightsAndSlots)
                {
                    if (rightsAndSlot.GetTabId() < _GetPurchasedTabsSize())                       
                        _SetRankBankTabRightsAndSlots(rankId, rightsAndSlot);
                }

                GuildEventRankChanged packet = new();
                packet.RankID = rankId;
                BroadcastPacket(packet);
            }
        }

        public void HandleBuyBankTab(WorldSession session, byte tabId)
        {
            Player player = session.GetPlayer();
            if (player == null)
                return;

            Member member = GetMember(player.GetGUID());
            if (member == null)
                return;

            if (_GetPurchasedTabsSize() >= GuildConst.MaxBankTabs)
                return;

            if (tabId != _GetPurchasedTabsSize())
                return;

            if (tabId >= GuildConst.MaxBankTabs)
                return;

            long tabCost = GetGuildBankTabPrice(tabId) * MoneyConstants.Gold;
            if (!player.HasEnoughMoney(tabCost))                   // Should not happen, this is checked by client
                return;

            player.ModifyMoney(-tabCost);

            var newTabInfo = _CreateNewBankTab();
            UpdateBankQueryResult(newTabInfo.newId, newTabInfo.newTab);

            SendPermissions(session); // Hack to force guildmaster-client to update permissions            

            // BroadcastPacket(new GuildEventTabAdded()); // WOTLK_CLASSIC client does not respond to this packet
            BroadcastPacket(new GuildEventBankContentsChanged()); // Hack to force all clients to update TabInfo            
        }

        public void HandleInviteMember(WorldSession session, string name)
        {
            Player pInvitee = Global.ObjAccessor.FindPlayerByName(name);
            if (pInvitee == null)
            {
                SendCommandResult(session, GuildCommandType.InvitePlayer, GuildCommandError.PlayerNotFound_S, name);
                return;
            }

            Player player = session.GetPlayer();
            // Do not show invitations from ignored players
            if (pInvitee.GetSocial().HasIgnore(player.GetGUID(), player.GetSession().GetAccountGUID()))
                return;

            if (!WorldConfig.Values[WorldCfg.AllowTwoSideInteractionGuild].Bool && pInvitee.GetTeam() != player.GetTeam())
            {
                SendCommandResult(session, GuildCommandType.InvitePlayer, GuildCommandError.NotAllied, name);
                return;
            }

            // Invited player cannot be in another guild
            if (pInvitee.GetGuildId() != 0)
            {
                SendCommandResult(session, GuildCommandType.InvitePlayer, GuildCommandError.AlreadyInGuild_S, name);
                return;
            }

            // Invited player cannot be invited
            if (pInvitee.GetGuildIdInvited() != 0)
            {
                SendCommandResult(session, GuildCommandType.InvitePlayer, GuildCommandError.AlreadyInvitedToGuild_S, name);
                return;
            }
            // Inviting player must have rights to invite
            if (!_HasRankRight(player, GuildRankRights.Invite))
            {
                SendCommandResult(session, GuildCommandType.InvitePlayer, GuildCommandError.Permissions);
                return;
            }

            SendCommandResult(session, GuildCommandType.InvitePlayer, GuildCommandError.Success, name);

            Log.outDebug(LogFilter.Guild,
                $"Player {player.GetName()} invited {name} to join his Guild");

            pInvitee.SetGuildIdInvited(m_id);
            _LogEvent(GuildEventLogTypes.InvitePlayer, player.GetGUID().GetCounter(), pInvitee.GetGUID().GetCounter());

            GuildInvite invite = new();

            invite.InviterVirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
            invite.GuildVirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
            invite.GuildGUID = GetGUID();

            invite.EmblemStyle = m_emblemInfo.GetStyle();
            invite.EmblemColor = m_emblemInfo.GetColor();
            invite.BorderStyle = m_emblemInfo.GetBorderStyle();
            invite.BorderColor = m_emblemInfo.GetBorderColor();
            invite.Background = m_emblemInfo.GetBackgroundColor();
            invite.AchievementPoints = GetAchievementMgr().GetAchievementPoints();

            invite.InviterName = player.GetName();
            invite.GuildName = GetName();

            Guild oldGuild = pInvitee.GetGuild();
            if (oldGuild != null)
            {
                invite.OldGuildGUID = oldGuild.GetGUID();
                invite.OldGuildName = oldGuild.GetName();
                invite.OldGuildVirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
            }

            pInvitee.SendPacket(invite);
        }

        public void HandleAcceptMember(WorldSession session)
        {
            Player player = session.GetPlayer();
            if (!WorldConfig.Values[WorldCfg.AllowTwoSideInteractionGuild].Bool &&
                player.GetTeam() != Global.CharacterCacheStorage.GetCharacterTeamByGuid(GetLeaderGUID()))
            {
                return;
            }

            AddMember(null, player.GetGUID());
        }

        public void HandleLeaveMember(WorldSession session)
        {
            Player player = session.GetPlayer();

            // If leader is leaving
            if (_IsLeader(player))
            {
                if (m_members.Count > 1)
                {
                    // Leader cannot leave if he is not the last member
                    SendCommandResult(session, GuildCommandType.LeaveGuild, GuildCommandError.LeaderLeave);
                    return;
                }
                else
                {
                    // Guild is disbanded if leader leaves.
                    Disband();
                }
            }
            else
            {
                DeleteMember(null, player.GetGUID(), false, false);

                _LogEvent(GuildEventLogTypes.LeaveGuild, player.GetGUID().GetCounter());
                SendEventPlayerLeft(player);

                SendCommandResult(session, GuildCommandType.LeaveGuild, GuildCommandError.Success, m_name);
            }

            Global.CalendarMgr.RemovePlayerGuildEventsAndSignups(player.GetGUID(), GetId());
        }

        public void HandleRemoveMember(WorldSession session, ObjectGuid guid)
        {
            Player player = session.GetPlayer();

            // Player must have rights to remove members
            if (!_HasRankRight(player, GuildRankRights.Remove))
            {
                SendCommandResult(session, GuildCommandType.RemovePlayer, GuildCommandError.Permissions);
                return;
            }

            Member member = GetMember(guid);
            if (member == null)
            {
                SendCommandResult(session, GuildCommandType.RemovePlayer, GuildCommandError.PlayerNotFound_S);
                return;
            }

            string name = member.GetName();

            // Guild masters cannot be removed
            if (member.IsRank(GuildRankId.GuildMaster))
            {
                SendCommandResult(session, GuildCommandType.RemovePlayer, GuildCommandError.LeaderLeave);
                return;
            }

            // Do not allow to remove player with the same rank or higher
            Member memberMe = GetMember(player.GetGUID());
            RankInfo myRank = GetRankInfo(memberMe.GetRankId());
            RankInfo targetRank = GetRankInfo(member.GetRankId());

            if (memberMe == member)
            {
                SendCommandResult(session, GuildCommandType.RemovePlayer, GuildCommandError.NameInvalid);
                return;
            }

            if (memberMe == null || targetRank.GetOrder() <= myRank.GetOrder())
            {
                SendCommandResult(session, GuildCommandType.RemovePlayer, GuildCommandError.RankTooHigh_S, name);
                return;
            }

            _LogEvent(GuildEventLogTypes.UninvitePlayer, player.GetGUID().GetCounter(), guid.GetCounter());

            Player pMember = Global.ObjAccessor.FindConnectedPlayer(guid);
            SendEventPlayerLeft(pMember, player, true);

            // After call to DeleteMember pointer to member becomes invalid
            DeleteMember(null, guid, false, true);

            SendCommandResult(session, GuildCommandType.RemovePlayer, GuildCommandError.Success, name);
        }

        public void HandleUpdateMemberRank(WorldSession session, ObjectGuid guid, bool demote)
        {
            Player player = session.GetPlayer();
            GuildCommandType type = demote ? GuildCommandType.DemotePlayer : GuildCommandType.PromotePlayer;

            // Player must have rights to promote
            Member member;
            if (!_HasRankRight(player, demote ? GuildRankRights.Demote : GuildRankRights.Promote))
            {
                SendCommandResult(session, type, GuildCommandError.LeaderLeave);
                return;
            }

            // Promoted player must be a member of guild
            if ((member = GetMember(guid)) != null)
            {
                string name = member.GetName();
                // Player cannot promote himself
                if (member.IsSamePlayer(player.GetGUID()))
                {
                    SendCommandResult(session, type, GuildCommandError.NameInvalid);
                    return;
                }

                Member memberMe = GetMember(player.GetGUID());
                RankInfo myRank = GetRankInfo(memberMe.GetRankId());
                RankInfo oldRank = GetRankInfo(member.GetRankId());
                GuildRankId newRankId;

                if (demote)
                {
                    // Player can demote only lower rank members
                    if (oldRank.GetOrder() <= myRank.GetOrder())
                    {
                        SendCommandResult(session, type, GuildCommandError.RankTooHigh_S, name);
                        return;
                    }

                    // Lowest rank cannot be demoted
                    RankInfo newRank = GetRankInfo(oldRank.GetOrder() + 1);
                    if (newRank == null)
                    {
                        SendCommandResult(session, type, GuildCommandError.RankTooLow_S, name);
                        return;
                    }

                    newRankId = newRank.GetId();
                }
                else
                {
                    // Allow to promote only to lower rank than member's rank
                    // memberMe.GetRankId() + 1 is the highest rank that current player can promote to
                    if ((oldRank.GetOrder() - 1) <= myRank.GetOrder())
                    {
                        SendCommandResult(session, type, GuildCommandError.RankTooHigh_S, name);
                        return;
                    }

                    newRankId = GetRankInfo((oldRank.GetOrder() - 1)).GetId();
                }

                member.ChangeRank(null, newRankId);

                _LogEvent(demote ? GuildEventLogTypes.DemotePlayer : GuildEventLogTypes.PromotePlayer,
                    player.GetGUID().GetCounter(), member.GetGUID().GetCounter(), (byte)newRankId);

                SendGuildRanksUpdate(player.GetGUID(), guid, oldRank.GetId(), newRankId);
            }
        }

        public void HandleSetMemberRank(WorldSession session, ObjectGuid targetGuid, ObjectGuid setterGuid, GuildRankOrder rank)
        {
            Player player = session.GetPlayer();
            Member member = GetMember(targetGuid);
            GuildRankRights rights = GuildRankRights.Promote;
            GuildCommandType type = GuildCommandType.PromotePlayer;

            RankInfo oldRank = GetRankInfo(member.GetRankId());
            RankInfo newRank = GetRankInfo(rank);
            if (oldRank == null || newRank == null)
                return;

            if (rank > oldRank.GetOrder())
            {
                rights = GuildRankRights.Demote;
                type = GuildCommandType.DemotePlayer;
            }

            // Promoted player must be a member of guild
            if (!_HasRankRight(player, rights))
            {
                SendCommandResult(session, type, GuildCommandError.Permissions);
                return;
            }

            // Player cannot promote himself
            if (member.IsSamePlayer(player.GetGUID()))
            {
                SendCommandResult(session, type, GuildCommandError.NameInvalid);
                return;
            }

            member.ChangeRank(null, newRank.GetId());

            SendGuildRanksUpdate(setterGuid, targetGuid, oldRank.GetId(), newRank.GetId());
        }

        public void HandleAddNewRank(WorldSession session, string name)
        {
            byte size = _GetRanksSize();
            if (size >= GuildConst.MaxRanks)
                return;

            // Only leader can add new rank
            if (_IsLeader(session.GetPlayer()))
            {
                if (_CreateRank(null, name, GuildRankRights.GChatListen | GuildRankRights.GChatSpeak))
                    BroadcastPacket(new GuildEventRanksUpdated());
            }
        }

        public void HandleRemoveRank(WorldSession session, GuildRankOrder rankOrder)
        {
            // Cannot remove rank if total count is minimum allowed by the client or is not leader
            if (_GetRanksSize() <= GuildConst.MinRanks || !_IsLeader(session.GetPlayer()))
                return;

            var rankInfo = m_ranks.Find(rank => rank.GetOrder() == rankOrder);

            if (rankInfo == null)
                return;

            SQLTransaction trans = new SQLTransaction();

            // Delete bank rights for rank
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_RIGHTS_FOR_RANK);
            stmt.SetInt64(0, m_id);
            stmt.SetUInt8(1, (byte)rankInfo.GetId());
            trans.Append(stmt);

            // Delete rank
            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_RANK);
            stmt.SetInt64(0, m_id);
            stmt.SetUInt8(1, (byte)rankInfo.GetId());
            trans.Append(stmt);

            m_ranks.Remove(rankInfo);

            // correct order of other ranks
            foreach (RankInfo otherRank in m_ranks)
            {
                if (otherRank.GetOrder() < rankOrder)
                    continue;

                otherRank.SetOrder(otherRank.GetOrder() - 1);

                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_RANK_ORDER);
                stmt.SetUInt8(0, (byte)otherRank.GetOrder());
                stmt.SetUInt8(1, (byte)otherRank.GetId());
                stmt.SetInt64(2, m_id);
                trans.Append(stmt);
            }

            DB.Characters.CommitTransaction(trans);

            BroadcastPacket(new GuildEventRanksUpdated());
        }

        public void HandleShiftRank(WorldSession session, GuildRankOrder rankOrder, bool shiftUp)
        {
            // Only leader can modify ranks
            if (!_IsLeader(session.GetPlayer()))
                return;

            GuildRankOrder otherRankOrder = rankOrder + (shiftUp ? -1 : 1);

            RankInfo rankInfo = GetRankInfo(rankOrder);
            RankInfo otherRankInfo = GetRankInfo(otherRankOrder);
            if (rankInfo == null || otherRankInfo == null)
                return;

            // can't shift guild master rank (rank id = 0) - there's already a client-side limitation for it so that's just a safe-guard
            if (rankInfo.GetId() == GuildRankId.GuildMaster || otherRankInfo.GetId() == GuildRankId.GuildMaster)
                return;

            rankInfo.SetOrder(otherRankOrder);
            otherRankInfo.SetOrder(rankOrder);

            SQLTransaction trans = new SQLTransaction();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_RANK_ORDER);
            stmt.SetUInt8(0, (byte)rankInfo.GetOrder());
            stmt.SetUInt8(1, (byte)rankInfo.GetId());
            stmt.SetInt64(2, m_id);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_RANK_ORDER);
            stmt.SetUInt8(0, (byte)otherRankInfo.GetOrder());
            stmt.SetUInt8(1, (byte)otherRankInfo.GetId());
            stmt.SetInt64(2, m_id);
            trans.Append(stmt);

            DB.Characters.CommitTransaction(trans);

            // force client to re-request SMSG_GUILD_RANKS
            BroadcastPacket(new GuildEventRanksUpdated());
        }

        public void HandleMemberDepositMoney(WorldSession session, long amount, bool cashFlow = false)
        {
            // guild bank cannot have more than MAX_MONEY_AMOUNT
            amount = Math.Min(amount, PlayerConst.MaxMoneyAmount - m_bankMoney);
            if (amount == 0)
                return;

            Player player = session.GetPlayer();

            // Call script after validation and before money transfer.
            Global.ScriptMgr.OnGuildMemberDepositMoney(this, player, amount);

            if (m_bankMoney > GuildConst.MoneyLimit - amount)
            {
                if (!cashFlow)
                    SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.TooMuchMoney);
                return;
            }

            SQLTransaction trans = new();
            _ModifyBankMoney(trans, amount, true);
            if (!cashFlow)
            {
                player.ModifyMoney(-amount);
                player.SaveInventoryAndGoldToDB(trans);
            }

            _LogBankEvent(trans, cashFlow ? GuildBankEventLogTypes.CashFlowDeposit : GuildBankEventLogTypes.DepositMoney, 0, player.GetGUID().GetCounter(), (int)amount);
            DB.Characters.CommitTransaction(trans);

            SendEventBankMoneyChanged();

            if (player.GetSession().HasPermission(RBACPermissions.LogGmTrade))
            {
                Log.outCommand(player.GetSession().GetAccountId(),
                    $"GM {player.GetName()} (Account: {player.GetSession().GetAccountId()}) " +
                    $"deposit money (Amount: {amount}) to guild bank (Guild ID {m_id})");
            }
        }

        public bool HandleMemberWithdrawMoney(WorldSession session, long amount, bool repair = false)
        {
            // clamp amount to MAX_MONEY_AMOUNT, Players can't hold more than that anyway
            amount = Math.Min(amount, PlayerConst.MaxMoneyAmount);

            Player player = session.GetPlayer();
            Member member = GetMember(player.GetGUID());

            if (member == null)
                return false;

            if (!_HasRankRight(player, repair ? GuildRankRights.WithdrawRepair : GuildRankRights.WithdrawGold))
            {
                SendCommandResult(session, GuildCommandType.Repair, GuildCommandError.Permissions);
                return false;
            }

            if (_GetMemberRemainingMoney(member) < amount)   // Check if we have enough slot/money today
            {
                SendCommandResult(session, GuildCommandType.Repair, GuildCommandError.WithdrawLimit);
                return false;
            }

            if (m_bankMoney < amount)                               // Not enough goldPerDay in bank
            {
                SendCommandResult(session, GuildCommandType.Repair, GuildCommandError.NotEnoughMoney);
                return false;
            }

            // Call script after validation and before money transfer.
            Global.ScriptMgr.OnGuildMemberWitdrawMoney(this, player, amount, repair);

            SQLTransaction trans = new();
            // Add money to player (if required)
            if (!repair)
            {
                if (!player.ModifyMoney(amount))
                    return false;

                player.SaveInventoryAndGoldToDB(trans);
            }

            // Update remaining money amount
            member.UpdateBankMoneyWithdrawValue(trans, amount);
            // Remove money from bank
            _ModifyBankMoney(trans, amount, false);

            // Log guild bank event
            _LogBankEvent(trans, repair ? GuildBankEventLogTypes.RepairMoney : GuildBankEventLogTypes.WithdrawMoney,
                0, player.GetGUID().GetCounter(), (int)amount);

            DB.Characters.CommitTransaction(trans);

            SendEventBankMoneyChanged();
            return true;
        }

        public void HandleMemberLogout(WorldSession session)
        {
            Player player = session.GetPlayer();
            Member member = GetMember(player.GetGUID());
            if (member != null)
            {
                member.SetStats(player);
                member.UpdateLogoutTime();
                member.ResetFlags();
            }

            SendEventPresenceChanged(session, false, true);
            SaveToDB();
        }

        public void HandleDelete(WorldSession session)
        {
            // Only leader can disband guild
            if (_IsLeader(session.GetPlayer()))
            {
                Disband();
                Log.outDebug(LogFilter.Guild, $"Guild {GetName()}[{GetId()}] successfully Disbanded");
            }
            else
            {
                SendCommandResult(session, GuildCommandType.CreateGuild, GuildCommandError.Permissions);
                return;
            }
        }

        public void HandleGuildPartyRequest(WorldSession session)
        {
            Player player = session.GetPlayer();
            Group group = player.GetGroup();

            // Make sure player is a member of the guild and that he is in a group.
            if (!IsMember(player.GetGUID()) || group == null)
                return;

            GuildPartyState partyStateResponse = new();
            partyStateResponse.InGuildParty = (player.GetMap().GetOwnerGuildId(player.GetTeam()) == GetId());
            partyStateResponse.NumMembers = 0;
            partyStateResponse.NumRequired = 0;
            partyStateResponse.GuildXPEarnedMult = 0.0f;
            session.SendPacket(partyStateResponse);
        }

        public void HandleGuildRequestChallengeUpdate(WorldSession session)
        {
            GuildChallengeUpdate updatePacket = new();

            for (int i = 0; i < GuildConst.ChallengesTypes; ++i)
                updatePacket.CurrentCount[i] = 0; // @todo current count

            for (int i = 0; i < GuildConst.ChallengesTypes; ++i)
                updatePacket.MaxCount[i] = GuildConst.ChallengesMaxCount[i];

            for (int i = 0; i < GuildConst.ChallengesTypes; ++i)
                updatePacket.MaxLevelGold[i] = GuildConst.ChallengeMaxLevelGoldReward[i];

            for (int i = 0; i < GuildConst.ChallengesTypes; ++i)
                updatePacket.Gold[i] = GuildConst.ChallengeGoldReward[i];

            session.SendPacket(updatePacket);
        }

        public void SendEventLog(WorldSession session)
        {
            var eventLog = m_eventLog.GetGuildLog();

            GuildEventLogQueryResults packet = new();
            foreach (var entry in eventLog)
                entry.WritePacket(packet);

            session.SendPacket(packet);
        }

        public void SendNewsUpdate(WorldSession session)
        {
            var newsLog = m_newsLog.GetGuildLog();

            GuildNewsPkt packet = new();
            foreach (var newsLogEntry in newsLog)
            {
                newsLogEntry.WritePacket(packet);
                //packet.NewsEvents.Last().CompletedDate += session.GetTimezoneOffset();
            }

            session.SendPacket(packet);
        }

        public void SendBankLog(WorldSession session, byte tabId)
        {
            // GuildConst.MaxBankTabs send by client for money log
            if (tabId < _GetPurchasedTabsSize() || tabId == GuildConst.MaxBankTabs)
            {
                var bankEventLog = m_bankEventLog[tabId].GetGuildLog();

                GuildBankLogQueryResults packet = new();
                packet.Tab = tabId;

                //if (tabId == GUILD_BANK_MAX_TABS && hasCashFlow)
                //    packet.WeeklyBonusMoney.Set(uint64(weeklyBonusMoney));

                foreach (var entry in bankEventLog)
                    entry.WritePacket(packet);

                session.SendPacket(packet);
            }
        }

        public void SendBankTabText(WorldSession session, byte tabId)
        {
            Player player = session.GetPlayer();
            Member member = GetMember(player.GetGUID());

            if (member == null)
                return;

            BankTab tab = GetBankTab(tabId);
            if (tab != null)
                tab.SendText(this, session);
        }

        public void SendPermissions(WorldSession session)
        {
            Member member = GetMember(session.GetPlayer().GetGUID());
            if (member == null)
                return;

            GuildRankId rankId = member.GetRankId();

            GuildPermissionsQueryResults queryResult = new();
            queryResult.RankID = rankId;
            queryResult.WithdrawGoldLimit = _GetRankBankGoldPerDay(rankId);
            queryResult.Flags = _GetRankRights(rankId);
            queryResult.NumTabs = _GetPurchasedTabsSize();

            for (byte tabId = 0; tabId < queryResult.NumTabs; ++tabId)
            {
                GuildPermissionsQueryResults.GuildRankTabPermissions tabPerm;
                tabPerm.Flags = _GetRankBankTabRights(rankId, tabId);
                tabPerm.WithdrawItemLimit = _GetRankBankTabSlotsPerDay(rankId, tabId);
                queryResult.Tab.Add(tabPerm);
            }

            session.SendPacket(queryResult);
        }

        public void SendMoneyInfo(WorldSession session)
        {
            Member member = GetMember(session.GetPlayer().GetGUID());
            if (member == null)
                return;

            long amount = _GetMemberRemainingMoney(member);

            GuildBankRemainingWithdrawMoney packet = new();
            packet.RemainingWithdrawMoney = amount;
            session.SendPacket(packet);
        }

        public void SendLoginInfo(WorldSession session)
        {
            Player player = session.GetPlayer();
            Member member = GetMember(player.GetGUID());
            if (member == null)
                return;

            SendEventMOTD(session);
            SendGuildRankInfo(session);
            SendEventPresenceChanged(session, true, true);      // Broadcast

            // Send to self separately, player is not in world yet and is not found by _BroadcastEvent
            SendEventPresenceChanged(session, true);

            if (member.GetGUID() == GetLeaderGUID())
            {
                GuildFlaggedForRename renameFlag = new();
                renameFlag.FlagSet = false;
                player.SendPacket(renameFlag);
            }

            foreach (var entry in CliDB.GuildPerkSpellsStorage.Values)
                player.SpellBook.Learn(entry.SpellID, true);

            GetAchievementMgr().SendAllData(player);

            // tells the client to request bank withdrawal limit
            player.SendPacket(new GuildMemberDailyReset());

            member.SetStats(player);
            member.AddFlag(GuildMemberFlags.Online);
        }

        public void SendEventAwayChanged(ObjectGuid memberGuid, bool afk, bool dnd)
        {
            Member member = GetMember(memberGuid);
            if (member == null)
                return;

            if (afk)
                member.AddFlag(GuildMemberFlags.AFK);
            else
                member.RemoveFlag(GuildMemberFlags.AFK);

            if (dnd)
                member.AddFlag(GuildMemberFlags.DND);
            else
                member.RemoveFlag(GuildMemberFlags.DND);

            GuildEventStatusChange statusChange = new();
            statusChange.Guid = memberGuid;
            statusChange.AFK = afk;
            statusChange.DND = dnd;
            BroadcastPacket(statusChange);
        }

        void SendEventBankMoneyChanged()
        {
            GuildEventBankMoneyChanged eventPacket = new();
            eventPacket.Money = GetBankMoney();
            BroadcastPacket(eventPacket);
        }

        void SendEventMOTD(WorldSession session, bool broadcast = false)
        {
            GuildEventMotd eventPacket = new();
            eventPacket.MotdText = GetMOTD();

            if (broadcast)
                BroadcastPacket(eventPacket);
            else
            {
                session.SendPacket(eventPacket);
                Log.outDebug(LogFilter.Guild, $"SMSG_GUILD_EVENT_MOTD [{session.GetPlayerInfo()}] ");
            }
        }

        void SendEventNewLeader(Member newLeader, Member oldLeader, bool isSelfPromoted = false)
        {
            GuildEventNewLeader eventPacket = new();
            eventPacket.SelfPromoted = isSelfPromoted;
            if (newLeader != null)
            {
                eventPacket.NewLeaderGUID = newLeader.GetGUID();
                eventPacket.NewLeaderName = newLeader.GetName();
                eventPacket.NewLeaderVirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
            }

            if (oldLeader != null)
            {
                eventPacket.OldLeaderGUID = oldLeader.GetGUID();
                eventPacket.OldLeaderName = oldLeader.GetName();
                eventPacket.OldLeaderVirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
            }

            BroadcastPacket(eventPacket);
        }

        void SendEventPlayerLeft(Player leaver, Player remover = null, bool isRemoved = false)
        {
            GuildEventPlayerLeft eventPacket = new();
            eventPacket.Removed = isRemoved;
            eventPacket.LeaverGUID = leaver.GetGUID();
            eventPacket.LeaverName = leaver.GetName();
            eventPacket.LeaverVirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();

            if (isRemoved && remover != null)
            {
                eventPacket.RemoverGUID = remover.GetGUID();
                eventPacket.RemoverName = remover.GetName();
                eventPacket.RemoverVirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
            }

            BroadcastPacket(eventPacket);
        }

        void SendEventPresenceChanged(WorldSession session, bool loggedOn, bool broadcast = false)
        {
            Player player = session.GetPlayer();

            GuildEventPresenceChange eventPacket = new();
            eventPacket.Guid = player.GetGUID();
            eventPacket.Name = player.GetName();
            eventPacket.VirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
            eventPacket.LoggedOn = loggedOn;
            eventPacket.Mobile = false;

            if (broadcast)
                BroadcastPacket(eventPacket);
            else
                session.SendPacket(eventPacket);
        }

        public bool LoadFromDB(SQLFields fields)
        {
            m_id = fields.Read<uint>(0);
            m_name = fields.Read<string>(1);
            m_leaderGuid = ObjectGuid.Create(HighGuid.Player, fields.Read<long>(2));

            if (!m_emblemInfo.LoadFromDB(fields))
            {
                Log.outError(LogFilter.Guild,
                    $"Guild {m_id} has invalid emblem colors (Background: {m_emblemInfo.GetBackgroundColor()}, " +
                    $"Border: {m_emblemInfo.GetBorderColor()}, Emblem: {m_emblemInfo.GetColor()}), skipped.");
                return false;
            }

            m_info = fields.Read<string>(8);
            m_motd = fields.Read<string>(9);
            m_createdDate = (ServerTime)(UnixTime)fields.Read<int>(10);
            m_bankMoney = fields.Read<long>(11);

            byte purchasedTabs = (byte)fields.Read<uint>(12);
            if (purchasedTabs > GuildConst.MaxBankTabs)
                purchasedTabs = GuildConst.MaxBankTabs;

            m_bankTabs.Clear();
            for (byte i = 0; i < purchasedTabs; ++i)
                m_bankTabs.Add(new BankTab(this, i));

            return true;
        }

        public void LoadRankFromDB(SQLFields field)
        {
            RankInfo rankInfo = new(m_id);

            rankInfo.LoadFromDB(field);

            m_ranks.Add(rankInfo);
        }

        public bool LoadMemberFromDB(SQLFields field)
        {
            long lowguid = field.Read<long>(1);
            ObjectGuid playerGuid = ObjectGuid.Create(HighGuid.Player, lowguid);

            Member member = new(m_id, playerGuid, (GuildRankId)field.Read<byte>(2));
            bool isNew = m_members.TryAdd(playerGuid, member);
            if (!isNew)
            {
                Log.outError(LogFilter.Guild,
                    $"Tried to add {playerGuid} to guild '{m_name}'. Member already exists.");
                return false;
            }

            if (!member.LoadFromDB(field))
            {
                _DeleteMemberFromDB(null, lowguid);
                return false;
            }

            Global.CharacterCacheStorage.UpdateCharacterGuildId(playerGuid, GetId());
            m_members[member.GetGUID()] = member;
            return true;
        }

        public void LoadBankRightFromDB(SQLFields field)
        {
            //                                                          tabId              rights                slots
            GuildBankRightsAndSlots rightsAndSlots = new(field.Read<byte>(1), field.Read<sbyte>(3), field.Read<int>(4));
            //                                                   rankId
            _SetRankBankTabRightsAndSlots((GuildRankId)field.Read<byte>(2), rightsAndSlots, false);
        }

        public bool LoadEventLogFromDB(SQLFields field)
        {
            if (m_eventLog.CanInsert())
            {
                m_eventLog.LoadEvent(new EventLogEntry(
                    m_id,                                       // guild id
                    field.Read<int>(1),                         // guid
                    (ServerTime)(UnixTime64)field.Read<long>(6),// timestamp
                    (GuildEventLogTypes)field.Read<byte>(2),    // event Type
                    field.Read<long>(3),                        // player guid 1
                    field.Read<long>(4),                        // player guid 2
                    field.Read<byte>(5)));                      // rank
                return true;
            }
            return false;
        }

        public bool LoadBankEventLogFromDB(SQLFields field)
        {
            byte dbTabId = field.Read<byte>(1);
            bool isMoneyTab = (dbTabId == GuildConst.BankMoneyLogsTab);
            if (dbTabId < _GetPurchasedTabsSize() || isMoneyTab)
            {
                byte tabId = isMoneyTab ? (byte)GuildConst.MaxBankTabs : dbTabId;
                var pLog = m_bankEventLog[tabId];
                if (pLog.CanInsert())
                {
                    int guid = field.Read<int>(2);
                    GuildBankEventLogTypes eventType = (GuildBankEventLogTypes)field.Read<byte>(3);
                    if (BankEventLogEntry.IsMoneyEvent(eventType))
                    {
                        if (!isMoneyTab)
                        {
                            Log.outError(LogFilter.Guild,
                                $"GuildBankEventLog ERROR: MoneyEvent(LogGuid: {guid}, Guild: {m_id}) " +
                                $"does not belong to money tab ({dbTabId}), ignoring...");
                            return false;
                        }
                    }
                    else if (isMoneyTab)
                    {
                        Log.outError(LogFilter.Guild,
                            $"GuildBankEventLog ERROR: non-money event (LogGuid: {guid}, Guild: {m_id}) " +
                            $"belongs to money tab, ignoring...");
                        return false;
                    }

                    pLog.LoadEvent(new BankEventLogEntry(
                        m_id,                                           // guild id
                        guid,                                           // guid
                        (ServerTime)(UnixTime64)field.Read<long>(8),    // timestamp
                        dbTabId,                                        // tab id
                        eventType,                                      // event Type
                        field.Read<long>(4),                            // player guid
                        field.Read<long>(5),                            // item or money
                        field.Read<ushort>(6),                          // itam stack count
                        field.Read<byte>(7)));                          // dest tab id
                }
            }
            return true;
        }

        public void LoadGuildNewsLogFromDB(SQLFields field)
        {
            if (!m_newsLog.CanInsert())
                return;

            var news = new NewsLogEntry(
                m_id,                                                       // guild id
                field.Read<int>(1),                                         // guid
                (ServerTime)(UnixTime64)field.Read<long>(6),                // timestamp
                (GuildNews)field.Read<byte>(2),                             // Type
                ObjectGuid.Create(HighGuid.Player, field.Read<long>(3)),    // player guid
                field.Read<uint>(4),                                        // Flags
                field.Read<int>(5));                                        // value)

            m_newsLog.LoadEvent(news);

        }

        public void LoadBankTabFromDB(SQLFields field)
        {
            byte tabId = field.Read<byte>(1);
            if (tabId >= _GetPurchasedTabsSize())
            {
                Log.outError(LogFilter.Guild, $"Invalid tab (tabId: {tabId}) in guild bank, skipped.");
            }
            else
            {
                m_bankTabs[tabId].LoadFromDB(field);
            }
        }

        public void LoadBankItemFromDB(SQLFields field)
        {
            var tabId = field.Read<byte>(47);
            if (tabId >= _GetPurchasedTabsSize())
            {
                Log.outError(LogFilter.Guild,
                    $"Invalid tab for item (GUID: {field.Read<long>(0)}, " +
                    $"id: {field.Read<int>(1)}) in guild bank, skipped.");
            }
            else
            {
                m_bankTabs[tabId].LoadItemFromDB(field);
            }
        }

        public bool Validate()
        {
            // Validate ranks data
            // GUILD RANKS represent a sequence starting from 0 = GUILD_MASTER (ALL PRIVILEGES) to max 9 (lowest privileges).
            // The lower rank id is considered higher rank - so promotion does rank-- and demotion does rank++
            // Between ranks in sequence cannot be gaps - so 0, 1, 2, 4 is impossible
            // Min ranks count is 2 and max is 10.
            bool broken_ranks = false;
            byte ranks = _GetRanksSize();

            SQLTransaction trans = new();
            if (ranks < GuildConst.MinRanks || ranks > GuildConst.MaxRanks)
            {
                Log.outError(LogFilter.Guild,
                    $"Guild {m_id} has invalid number of ranks, creating new...");
                broken_ranks = true;
            }
            else
            {
                for (byte rankId = 0; rankId < ranks; ++rankId)
                {
                    RankInfo rankInfo = GetRankInfo((GuildRankId)rankId);
                    if (rankInfo.GetId() != (GuildRankId)rankId)
                    {
                        Log.outError(LogFilter.Guild,
                            $"Guild {m_id} has broken rank id {rankId}, creating default set of ranks...");
                        broken_ranks = true;
                    }
                    else
                    {
                        rankInfo.CreateMissingTabsIfNeeded(_GetPurchasedTabsSize(), trans, true);
                    }
                }
            }

            if (broken_ranks)
            {
                m_ranks.Clear();
                _CreateDefaultGuildRanks(trans, SharedConst.DefaultLocale);
            }

            // Validate members' data
            foreach (var member in m_members.Values)
            {
                if (GetRankInfo(member.GetRankId()) == null)
                    member.ChangeRank(trans, _GetLowestRankId());
            }

            // Repair the structure of the guild.
            // If the guildmaster doesn't exist or isn't member of the guild
            // attempt to promote another member.
            Member leader = GetMember(m_leaderGuid);
            if (leader == null)
            {
                DeleteMember(trans, m_leaderGuid);
                // If no more members left, disband guild
                if (m_members.Empty())
                {
                    Disband();
                    return false;
                }
            }
            else if (!leader.IsRank(GuildRankId.GuildMaster))
                _SetLeader(trans, leader);

            if (trans.commands.Count > 0)
                DB.Characters.CommitTransaction(trans);

            _UpdateAccountsNumber();

            return true;
        }

        public void BroadcastToGuild(WorldSession session, bool officerOnly, string msg, Language language)
        {
            if (session != null && session.GetPlayer() != null
                && _HasRankRight(session.GetPlayer(), officerOnly ? GuildRankRights.OffChatSpeak : GuildRankRights.GChatSpeak))
            {
                ChatPkt data = new();
                data.Initialize(officerOnly ? ChatMsg.Officer : ChatMsg.Guild, language, session.GetPlayer(), null, msg);
                foreach (var member in m_members.Values)
                {
                    Player player = member.FindPlayer();
                    if (player != null)
                    {
                        if (player.GetSession() != null
                            && _HasRankRight(player, officerOnly ? GuildRankRights.OffChatListen : GuildRankRights.GChatListen)
                            && !player.GetSocial().HasIgnore(session.GetPlayer().GetGUID(), session.GetAccountGUID()))
                        {
                            player.SendPacket(data);
                        }
                    }
                }
            }
        }

        public void BroadcastAddonToGuild(WorldSession session, bool officerOnly, string msg, string prefix, bool isLogged)
        {
            if (session != null && session.GetPlayer() != null
                && _HasRankRight(session.GetPlayer(), officerOnly ? GuildRankRights.OffChatSpeak : GuildRankRights.GChatSpeak))
            {
                ChatPkt data = new();
                data.Initialize(officerOnly ? ChatMsg.Officer : ChatMsg.Guild, isLogged ? Language.AddonLogged : Language.Addon, session.GetPlayer(), null, msg, 0, "", Locale.enUS, prefix);
                foreach (var member in m_members.Values)
                {
                    Player player = member.FindPlayer();
                    if (player != null)
                    {
                        if (player.GetSession() != null
                            && _HasRankRight(player, officerOnly ? GuildRankRights.OffChatListen : GuildRankRights.GChatListen)
                            && !player.GetSocial().HasIgnore(session.GetPlayer().GetGUID(), session.GetAccountGUID())
                            && player.GetSession().IsAddonRegistered(prefix))
                        {
                            player.SendPacket(data);
                        }
                    }
                }
            }
        }

        public void BroadcastPacketToRank(ServerPacket packet, GuildRankId rankId)
        {
            foreach (var member in m_members.Values)
            {
                if (member.IsRank(rankId))
                {
                    Player player = member.FindPlayer();
                    if (player != null)
                        player.SendPacket(packet);
                }
            }
        }

        public void BroadcastPacket(ServerPacket packet)
        {
            foreach (var member in m_members.Values)
            {
                Player player = member.FindPlayer();
                if (player != null)
                    player.SendPacket(packet);
            }
        }

        public List<Player> GetMembersTrackingCriteria(int criteriaId)
        {
            List<Player> members = new();
            foreach (var (_, member) in m_members)
            {
                if (member.IsTrackingCriteriaId(criteriaId))
                {
                    Player player = member.FindPlayer();
                    if (player != null)
                        members.Add(player);
                }
            }

            return members;
        }

        public void MassInviteToEvent(WorldSession session, int minLevel, int maxLevel, GuildRankOrder minRank)
        {
            CalendarCommunityInvite packet = new();

            foreach (var (guid, member) in m_members)
            {
                // not sure if needed, maybe client checks it as well
                if (packet.Invites.Count >= SharedConst.CalendarMaxInvites)
                {
                    Player player = session.GetPlayer();
                    if (player != null)
                        Global.CalendarMgr.SendCalendarCommandResult(player.GetGUID(), CalendarError.InvitesExceeded);
                    return;
                }

                if (guid == session.GetPlayer().GetGUID())
                    continue;

                uint level = Global.CharacterCacheStorage.GetCharacterLevelByGuid(guid);
                if (level < minLevel || level > maxLevel)
                    continue;

                RankInfo rank = GetRankInfo(member.GetRankId());
                if (rank.GetOrder() > minRank)
                    continue;

                packet.Invites.Add(new CalendarEventInitialInviteInfo(guid, (byte)level));
            }

            session.SendPacket(packet);
        }

        public bool AddMember(SQLTransaction trans, ObjectGuid guid, GuildRankId? rankId = null)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            // Player cannot be in guild
            if (player != null)
            {
                if (player.GetGuildId() != 0)
                    return false;
            }
            else if (Global.CharacterCacheStorage.GetCharacterGuildIdByGuid(guid) != 0)
                return false;

            // Remove all player signs from another petitions
            // This will be prevent attempt to join many guilds and corrupt guild data integrity
            Player.RemovePetitionsAndSigns(guid);

            long lowguid = guid.GetCounter();

            // If rank was not passed, assign lowest possible rank
            if (!rankId.HasValue)
                rankId = _GetLowestRankId();

            Member member = new(m_id, guid, rankId.Value);
            bool isNew = m_members.TryAdd(guid, member);
            if (!isNew)
            {
                Log.outError(LogFilter.Guild,
                    $"Tried to add {guid} to guild '{m_name}'. Member already exists.");
                return false;
            }

            string name = "";
            if (player != null)
            {
                m_members[guid] = member;
                player.SetInGuild(m_id);
                player.SetGuildIdInvited(0);
                player.SetGuildRank((byte)rankId);
                player.SetGuildLevel(GetLevel());
                member.SetStats(player);
                SendLoginInfo(player.GetSession());
                name = player.GetName();
            }
            else
            {
                member.ResetFlags();

                bool ok = false;
                // Player must exist
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_DATA_FOR_GUILD);
                stmt.SetInt64(0, lowguid);
                SQLResult result = DB.Characters.Query(stmt);
                if (!result.IsEmpty())
                {
                    name = result.Read<string>(0);
                    member.SetStats(
                        name,
                        result.Read<byte>(1),
                        (Race)result.Read<byte>(2),
                        (Class)result.Read<byte>(3),
                        (Gender)result.Read<byte>(4),
                        result.Read<ushort>(5),
                        result.Read<int>(6),
                        0);

                    ok = member.CheckStats();
                }

                if (!ok)
                    return false;

                m_members[guid] = member;
                Global.CharacterCacheStorage.UpdateCharacterGuildId(guid, GetId());
            }

            member.SaveToDB(trans);

            _UpdateAccountsNumber();
            _LogEvent(GuildEventLogTypes.JoinGuild, lowguid);

            GuildEventPlayerJoined joinNotificationPacket = new();
            joinNotificationPacket.Guid = guid;
            joinNotificationPacket.Name = name;
            joinNotificationPacket.VirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
            BroadcastPacket(joinNotificationPacket);

            // Call scripts if member was succesfully added (and stored to database)
            Global.ScriptMgr.OnGuildAddMember(this, player, (byte)rankId);

            return true;
        }

        public void DeleteMember(SQLTransaction trans, ObjectGuid guid, bool isDisbanding = false, bool isKicked = false, bool canDeleteGuild = false)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);

            // Guild master can be deleted when loading guild and guid doesn't exist in characters table
            // or when he is removed from guild by gm command
            if (m_leaderGuid == guid && !isDisbanding)
            {
                Member oldLeader = null;
                Member newLeader = null;
                foreach (var (memberGuid, member) in m_members)
                {
                    if (memberGuid == guid)
                        oldLeader = member;
                    else if (newLeader == null || newLeader.GetRankId() > member.GetRankId())
                        newLeader = member;
                }

                if (newLeader == null)
                {
                    Disband();
                    return;
                }

                _SetLeader(trans, newLeader);

                // If leader does not exist (at guild loading with deleted leader) do not send broadcasts
                if (oldLeader != null)
                {
                    SendEventNewLeader(newLeader, oldLeader, true);
                    SendEventPlayerLeft(player);
                }
            }
            // Call script on remove before member is actually removed from guild (and database)
            Global.ScriptMgr.OnGuildRemoveMember(this, player, isDisbanding, isKicked);

            m_members.Remove(guid);

            // If player not online data in data field will be loaded from guild tabs no need to update it !!
            if (player != null)
            {
                player.SetInGuild(0);
                player.SetGuildRank(0);
                player.SetGuildLevel(0);

                foreach (var entry in CliDB.GuildPerkSpellsStorage.Values)
                    player.SpellBook.Remove(entry.SpellID, false, false);
            }
            else
                Global.CharacterCacheStorage.UpdateCharacterGuildId(guid, 0);

            _DeleteMemberFromDB(trans, guid.GetCounter());
            if (!isDisbanding)
                _UpdateAccountsNumber();
        }

        public bool ChangeMemberRank(SQLTransaction trans, ObjectGuid guid, GuildRankId newRank)
        {
            if (GetRankInfo(newRank) != null)                    // Validate rank (allow only existing ranks)
            {
                Member member = GetMember(guid);
                if (member != null)
                {
                    member.ChangeRank(trans, newRank);
                    return true;
                }
            }
            return false;
        }

        public bool IsMember(ObjectGuid guid)
        {
            return m_members.ContainsKey(guid);
        }

        public long GetMemberAvailableMoneyForRepairItems(ObjectGuid guid)
        {
            Member member = GetMember(guid);
            if (member == null)
                return 0;

            return Math.Min(m_bankMoney, _GetMemberRemainingMoney(member));
        }

        public void SwapItems(Player player, ItemPos src, ItemPos dest, int splitedAmount)
        {
            if (src.Container >= _GetPurchasedTabsSize() || src.Slot >= GuildConst.MaxBankSlots ||
                dest.Container >= _GetPurchasedTabsSize() || dest.Slot >= GuildConst.MaxBankSlots)
                return;

            if (src == dest)
                return;

            BankMoveItemData from = new(this, player, src);
            BankMoveItemData to = new(this, player, dest);
            _MoveItems(from, to, splitedAmount);
        }

        public void SwapItemsWithInventory(Player player, bool toChar, ItemPos source, ItemPos playerInvPos, int splitedAmount)
        {
            if ((source.Slot >= GuildConst.MaxBankSlots && source.Slot != ItemSlot.Null) || source.Container >= _GetPurchasedTabsSize())
                return;

            BankMoveItemData bankData = new(this, player, source);
            PlayerMoveItemData charData = new(this, player, playerInvPos);
            if (toChar)
                _MoveItems(bankData, charData, splitedAmount);
            else
                _MoveItems(charData, bankData, splitedAmount);
        }

        public void SetBankTabText(WorldSession session, byte tabId, string text)
        {
            Player player = session.GetPlayer();
            Member member = GetMember(player.GetGUID());

            if (member == null)
                return;

            BankTab pTab = GetBankTab(tabId);
            if (pTab != null)
            {
                GuildEventTabTextChanged eventPacket = new();
                eventPacket.Tab = tabId;

                if (_MemberHasTabRights(member, tabId, GuildBankRights.ModifyTabText))
                {
                    pTab.SetText(text);                    
                BroadcastPacket(eventPacket);
            }
                else
                {
                    // The WOTLK_CLASSIC client allows you to always change the text of the tab (bug or feature)
                    // and constantly sends a request to save the text if the text in the tab-window has been changed, regardless of permissions.
                    // Therefore, we force update the text to the original for this player.
                    player.SendPacket(eventPacket);
                }                
            }
        }

        RankInfo GetRankInfo(GuildRankId rankId)
        {
            return m_ranks.Find(rank => rank.GetId() == rankId);
        }

        RankInfo GetRankInfo(GuildRankOrder rankOrder)
        {
            return m_ranks.Find(rank => rank.GetOrder() == rankOrder);
        }

        // Private methods
        (int newId, BankTab newTab) _CreateNewBankTab()
        {
            byte newtabId = _GetPurchasedTabsSize();                      // Next free id
            BankTab newTab = new BankTab(this, newtabId);
            m_bankTabs.Add(newTab);

            SQLTransaction trans = new();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_TAB);
            stmt.SetInt64(0, m_id);
            stmt.SetUInt8(1, newtabId);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_BANK_TAB);
            stmt.SetInt64(0, m_id);
            stmt.SetUInt8(1, newtabId);
            trans.Append(stmt);

            foreach (var rank in m_ranks)
                rank.CreateMissingTabsIfNeeded(_GetPurchasedTabsSize(), trans, false);

            DB.Characters.CommitTransaction(trans);

            return (newtabId, newTab);
        }

        void _CreateDefaultGuildRanks(SQLTransaction trans, Locale loc = Locale.enUS)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_RANKS);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_RIGHTS);
            stmt.SetInt64(0, m_id);
            trans.Append(stmt);

            _CreateRank(trans, Global.ObjectMgr.GetCypherString(CypherStrings.GuildMaster, loc));
            _CreateRank(trans, Global.ObjectMgr.GetCypherString(CypherStrings.GuildOfficer, loc), GuildRankRights.All);
            _CreateRank(trans, Global.ObjectMgr.GetCypherString(CypherStrings.GuildVeteran, loc), GuildRankRights.GChatListen | GuildRankRights.GChatSpeak);
            _CreateRank(trans, Global.ObjectMgr.GetCypherString(CypherStrings.GuildMember, loc), GuildRankRights.GChatListen | GuildRankRights.GChatSpeak);
            _CreateRank(trans, Global.ObjectMgr.GetCypherString(CypherStrings.GuildInitiate, loc), GuildRankRights.GChatListen | GuildRankRights.GChatSpeak);
        }

        bool _CreateRank(SQLTransaction trans, string name, GuildRankRights rights = GuildRankRights.None)
        {
            if (m_ranks.Count >= GuildConst.MaxRanks)
                return false;

            // Ranks represent sequence 0, 1, 2, ... where 0 means guildmaster
            GuildRankId newRankId = GuildRankId.GuildMaster;
            while (GetRankInfo(newRankId) != null)
                ++newRankId;

            RankInfo info;

            if (newRankId == GuildRankId.GuildMaster)
                info = new(m_id, newRankId, (GuildRankOrder)m_ranks.Count, name, GuildRankRights.AllPermanent, GuildConst.WithdrawMoneyUnlimited);
            else
                info = new(m_id, newRankId, (GuildRankOrder)m_ranks.Count, name, rights, 0);

            m_ranks.Add(info);

            bool isInTransaction = trans != null;
            if (!isInTransaction)
                trans = new SQLTransaction();

            info.CreateMissingTabsIfNeeded(_GetPurchasedTabsSize(), trans);
            info.SaveToDB(trans);

            if (!isInTransaction)
                DB.Characters.CommitTransaction(trans);

            return true;
        }

        void _UpdateAccountsNumber()
        {
            // We use a set to be sure each element will be unique
            List<int> accountsIdSet = new();
            foreach (var member in m_members.Values)
                accountsIdSet.Add(member.GetAccountId());

            m_accountsNumber = accountsIdSet.Count;
        }

        bool _IsLeader(Player player)
        {
            if (player.GetGUID() == m_leaderGuid)
                return true;

            Member member = GetMember(player.GetGUID());
            if (member != null)
                return member.IsRank(GuildRankId.GuildMaster);

            return false;
        }

        void _DeleteBankItems(SQLTransaction trans, bool removeItemsFromDB)
        {
            for (byte tabId = 0; tabId < _GetPurchasedTabsSize(); ++tabId)
            {
                m_bankTabs[tabId].Delete(trans, removeItemsFromDB);
                m_bankTabs[tabId] = null;
            }
            m_bankTabs.Clear();
        }

        bool _ModifyBankMoney(SQLTransaction trans, long amount, bool add)
        {
            if (add)
                m_bankMoney += amount;
            else
            {
                // Check if there is enough money in bank.
                if (m_bankMoney < amount)
                    return false;

                m_bankMoney -= amount;
            }

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_BANK_MONEY);
            stmt.SetInt64(0, m_bankMoney);
            stmt.SetInt64(1, m_id);
            trans.Append(stmt);
            return true;
        }

        void _SetLeader(SQLTransaction trans, Member leader)
        {
            bool isInTransaction = trans != null;
            if (!isInTransaction)
                trans = new SQLTransaction();

            m_leaderGuid = leader.GetGUID();
            leader.ChangeRank(trans, GuildRankId.GuildMaster);

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_LEADER);
            stmt.SetInt64(0, m_leaderGuid.GetCounter());
            stmt.SetInt64(1, m_id);
            trans.Append(stmt);

            if (!isInTransaction)
                DB.Characters.CommitTransaction(trans);
        }

        void _SetRankBankGoldPerDay(GuildRankId rankId, int goldPerDay)
        {
            GetRankInfo(rankId).SetBankGoldPerDay(goldPerDay);
        }

        void _SetRankBankTabRightsAndSlots(GuildRankId rankId, GuildBankRightsAndSlots rightsAndSlots, bool saveToDB = true)
        {
            GetRankInfo(rankId).SetBankTabSlotsAndRights(rightsAndSlots, saveToDB);
        }

        string _GetRankName(GuildRankId rankId)
        {
            return GetRankInfo(rankId).GetName();
        }

        GuildRankRights _GetRankRights(GuildRankId rankId)
        {
            return GetRankInfo(rankId).GetRights();
        }

        int _GetRankBankGoldPerDay(GuildRankId rankId)
        {
            return GetRankInfo(rankId).GetBankGoldPerDay();
        }

        int _GetRankBankTabSlotsPerDay(GuildRankId rankId, byte tabId)
        {
            return GetRankInfo(rankId).GetBankTabSlotsPerDay(tabId);
        }

        GuildBankRights _GetRankBankTabRights(GuildRankId rankId, byte tabId)
        {
            return GetRankInfo(rankId).GetBankTabRights(tabId);
        }

        int _GetMemberRemainingSlots(Member member, byte tabId)
        {
            GuildRankId rankId = member.GetRankId();

            if ((_GetRankBankTabRights(rankId, tabId) & GuildBankRights.ViewTab) != 0)
            {
                int slotsPerDay = _GetRankBankTabSlotsPerDay(rankId, tabId);
                if (slotsPerDay <= GuildConst.WithdrawSlotUnlimited)
                    return GuildConst.WithdrawSlotUnlimited;

                int remaining = _GetRankBankTabSlotsPerDay(rankId, tabId) - member.GetBankTabWithdrawValue(tabId);
                if (remaining > 0)
                    return remaining;
            }

            return 0;
        }

        long _GetMemberRemainingMoney(Member member)
        {
            GuildRankId rankId = member.GetRankId();

            if ((_GetRankRights(rankId) & (GuildRankRights.WithdrawRepair | GuildRankRights.WithdrawGold)) != 0)
            {
                int goldPerDay = _GetRankBankGoldPerDay(rankId);
                if (goldPerDay <= GuildConst.WithdrawMoneyUnlimited)
                    return m_bankMoney;

                long remaining = (goldPerDay * MoneyConstants.Gold) - member.GetBankMoneyWithdrawValue();
                if (remaining > 0)
                    return remaining;
            }

            return 0;
        }

        void _UpdateMemberWithdrawSlots(SQLTransaction trans, ObjectGuid guid, byte tabId)
        {
            Member member = GetMember(guid);

            if (member != null)
                member.UpdateBankTabWithdrawValue(trans, tabId, 1);
        }

        bool _MemberHasTabRights(ObjectGuid guid, byte tabId, GuildBankRights rights)
        {
            Member member = GetMember(guid);

            if (member != null)
            {
                return _MemberHasTabRights(member, tabId, rights);
            }

            return false;
        }

        bool _MemberHasTabRights(Member member, byte tabId, GuildBankRights rights)
        {
            if (member != null)
            {
                return _GetRankBankTabRights(member.GetRankId(), tabId).HasFlag(rights);
            }

            return false;
        }

        void _LogEvent(GuildEventLogTypes eventType, long playerGuid1, long playerGuid2 = 0, byte newRank = 0)
        {
            SQLTransaction trans = new();
            m_eventLog.AddEvent(trans, new EventLogEntry(m_id, m_eventLog.GetNextGUID(), eventType, playerGuid1, playerGuid2, newRank));
            DB.Characters.CommitTransaction(trans);

            Global.ScriptMgr.OnGuildEvent(this, (byte)eventType, playerGuid1, playerGuid2, newRank);
        }

        void _LogBankEvent(SQLTransaction trans, GuildBankEventLogTypes eventType, byte tabId, long lowguid, int itemOrMoney, ushort itemStackCount = 0, byte destTabId = 0)
        {
            if (tabId > GuildConst.MaxBankTabs)
                return;

            // not logging moves within the same tab
            if (eventType == GuildBankEventLogTypes.MoveItem && tabId == destTabId)
                return;

            byte dbTabId = tabId;
            if (BankEventLogEntry.IsMoneyEvent(eventType))
            {
                tabId = GuildConst.MaxBankTabs;
                dbTabId = GuildConst.BankMoneyLogsTab;
            }

            var pLog = m_bankEventLog[tabId];

            pLog.AddEvent(trans,
                new BankEventLogEntry(m_id, pLog.GetNextGUID(), eventType, dbTabId, lowguid, itemOrMoney, itemStackCount, destTabId));

            Global.ScriptMgr.OnGuildBankEvent(this, (byte)eventType, tabId, lowguid, itemOrMoney, itemStackCount, destTabId);
        }

        Item _GetItem(ItemPos pos)
        {
            BankTab tab = GetBankTab(pos.Container);
            if (tab != null)
                return tab.GetItem(pos.Slot);
            return null;
        }

        void _RemoveItem(SQLTransaction trans, ItemPos itemPos)
        {
            BankTab pTab = GetBankTab(itemPos.Container);
            if (pTab != null)
                pTab.SetItem(trans, itemPos.Slot, null);
        }

        void _MoveItems(MoveItemData pSrc, MoveItemData pDest, int splitedAmount)
        {
            // 1. Initialize source item
            if (!pSrc.InitItem())
            {
                SendEquipError(InventoryResult.ItemNotFound);
                return;
            }

            // 2. Check source item
            if (!pSrc.CheckItem(ref splitedAmount))
            {
                SendEquipError(InventoryResult.TooFewToSplit);
                return;
            }

            WorldSession session = pSrc.Session;

            // 3. Common permission checks
            if (pSrc.Container == pDest.Container)
            {
                // All operations with items are prohibited, since their count in the slot and position
                // can be predetermined by the player in charge.
                if (!pSrc.HasModifyRights)
                {
                    SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.Permissions);
                    return;
                }
            }
            else
            {
                if (!pSrc.HasWithdrawRights)
                {
                    SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.Permissions);
                    return;
                }

                if (!pSrc.HasWithdrawAmount)
                {
                    SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.WithdrawLimit);
                    return;
                }

                if (!pDest.HasDepositRights)
                {
                    SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.Permissions);
                    return;
                }
            }

            // 4. Check split
            if (splitedAmount != 0)
            {
                // 4.1. Clone source item
                if (!pSrc.CloneItem(splitedAmount))
                {
                    SendEquipError(InventoryResult.ItemNotFound);
                    return;
                }

                // 4.2. Move splited item to destination
                InventoryResult splitAttemptResult = _DoItemsMove(pSrc, pDest, splitedAmount);
                if (splitAttemptResult != InventoryResult.Ok)
                {
                    SendEquipError(splitAttemptResult);
                    return;
                }
            }
            else // 5.No split
            {
                // 5.1 Try to merge items in destination (pDest.GetItem() == NULL)
                InventoryResult mergeAttemptResult = _DoItemsMove(pSrc, pDest);

                if (mergeAttemptResult == InventoryResult.ItemLocked) // You do not have permission to merge
                {
                    SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.Permissions);
                    return;
                }

                if (mergeAttemptResult == InventoryResult.BankFull)
                {
                    SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.BankFull);
                    return;
                }

                if (mergeAttemptResult != InventoryResult.Ok) // Item could not be merged
                {
                    // 5.2 Try to swap items
                    // 5.2.1 Initialize destination item
                    if (!pDest.InitItem())
                    {
                        SendEquipError(mergeAttemptResult);
                        return;
                    }

                    // 5.2.2 Swap items (pDest.GetItem() != NULL)

                    // Check rights to store item in source(opposite direction)
                    if (pSrc.Container != pDest.Container)
                    {
                        if (!pSrc.HasDepositRights)
                        {
                            SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.Permissions);
                            return;
                        }

                        if (!pDest.HasWithdrawRights)
                        {
                            SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.Permissions);
                            return;
                        }

                        if (!pDest.HasWithdrawAmount)
                        {
                            SendCommandResult(session, GuildCommandType.MoveItem, GuildCommandError.WithdrawLimit);
                            return;
                        }
                    }

                    InventoryResult swapAttemptResult = _DoItemsMove(pSrc, pDest);
                    if (swapAttemptResult != InventoryResult.Ok)
                    {
                        SendEquipError(swapAttemptResult);
                        return;
                    }
                }
            }
                        
            UpdateBankQueryResult(pSrc, pDest);
            BroadcastPacket(new GuildEventBankContentsChanged());

            #region Helpers
            void SendEquipError(InventoryResult msg)
            {
                pSrc.SendEquipError(msg, pSrc.GetItem(false));
            }
            #endregion
        }

        InventoryResult _DoItemsMove(MoveItemData pSrc, MoveItemData pDest, int splitedAmount = 0)
        {
            Item pDestItem = pDest.GetItem();
            bool swap = (pDestItem != null);

            Item pSrcItem = pSrc.GetItem(splitedAmount != 0);
            // 1. Can store source item in destination
            InventoryResult destResult = pDest.CanStore(pSrcItem, swap);
            if (destResult != InventoryResult.Ok)
                return destResult;

            // 2. Can store destination item in source
            if (swap)
            {
                InventoryResult srcResult = pSrc.CanStore(pDestItem, true);
                if (srcResult != InventoryResult.Ok)
                    return srcResult;
            }

            // GM LOG (@todo move to scripts)
            pDest.LogAction(pSrc);
            if (swap)
                pSrc.LogAction(pDest);

            SQLTransaction trans = new();
            // 3. Log bank events
            pDest.LogBankEvent(trans, pSrc, pSrcItem.GetCount());
            if (swap)
                pSrc.LogBankEvent(trans, pDest, pDestItem.GetCount());

            // 4. Remove item from source
            pSrc.RemoveItem(trans, pDest, splitedAmount);

            // 5. Remove item from destination
            if (swap)
                pDest.RemoveItem(trans, pSrc);

            // 6. Store item in destination
            pDest.StoreItem(trans, pSrcItem);

            // 7. Store item in source
            if (swap)
                pSrc.StoreItem(trans, pDestItem);

            DB.Characters.CommitTransaction(trans);
            return InventoryResult.Ok;
        }

        void UpdateBankQueryResult(MoveItemData pSrc, MoveItemData pDest)
        {
            Cypher.Assert(pSrc.IsBank || pDest.IsBank);

            ItemSlot tabId = 0;
            List<ItemSlot> slotsForUpdate = new();

            if (pSrc.IsBank) // Bank |
            {
                tabId = pSrc.Container;
                slotsForUpdate.Insert(0, pSrc.Slot);

                if (pDest.IsBank) // Bank | Bank
                {                    
                    if (pDest.Container == pSrc.Container)
                    {
                        // Same tab - add destination slots to collection
                        pDest.CopyDestinationSlotsTo(slotsForUpdate);
                    }
                    else
                    {
                        // Different tabs - send second message
                        List<ItemSlot> destSlots = new();
                        pDest.CopyDestinationSlotsTo(destSlots);
                        UpdateBankQueryResult(pDest.Container, destSlots);
                    }
                }
            }
            else if (pDest.IsBank) // Character | Bank
            {
                tabId = pDest.Container;
                pDest.CopyDestinationSlotsTo(slotsForUpdate);
            }

            UpdateBankQueryResult(tabId, slotsForUpdate);
        }

        void UpdateBankQueryResult(byte tabId, List<ItemSlot> slotsForUpdate)
        {
            GetBankTab(tabId).UpdateItemInBankQueryResult(slotsForUpdate);
        }

        public void SendBankList(WorldSession session, byte tabId, bool fullUpdate)
        {
            Member member = GetMember(session.GetPlayer().GetGUID());
            if (member == null) // Shouldn't happen, just in case
                return;

            if (tabId >= _GetPurchasedTabsSize())
                return;

            GuildBankQueryResults packet = m_bankTabs[tabId].GetPreparedBankQueryResultPacket();

            packet.Money = m_bankMoney;
            packet.WithdrawalsRemaining = _GetMemberRemainingSlots(member, tabId);
            packet.UpdateMoneyAndWithdrawalsInBuffer();

            session.SendPacket(packet);
        }

        void SendGuildRanksUpdate(ObjectGuid setterGuid, ObjectGuid targetGuid, GuildRankId oldRank, GuildRankId newRank)
        {
            Member member = GetMember(targetGuid);
            Cypher.Assert(member != null);

            GuildSendRankChange rankChange = new();
            rankChange.Officer = setterGuid;
            rankChange.Other = targetGuid;
            rankChange.RankID = (byte)newRank;
            rankChange.Promote = newRank < oldRank;
            BroadcastPacket(rankChange);

            Log.outDebug(LogFilter.Network,
                $"SMSG_GUILD_RANKS_UPDATE [Broadcast] Target: {targetGuid}, " +
                $"Issuer: {setterGuid}, RankId: {newRank}");
        }

        public void ResetTimes(bool weekly)
        {
            foreach (var member in m_members.Values)
            {
                member.ResetValues(weekly);
                Player player = member.FindPlayer();
                if (player != null)
                {
                    // tells the client to request bank withdrawal limit
                    player.SendPacket(new GuildMemberDailyReset());
                }
            }
        }

        public void AddGuildNews(GuildNews type, ObjectGuid guid, uint flags, int value)
        {
            SQLTransaction trans = new();
            NewsLogEntry news = m_newsLog.AddEvent(trans, new NewsLogEntry(m_id, m_newsLog.GetNextGUID(), type, guid, flags, value));
            DB.Characters.CommitTransaction(trans);

            var packetBuilder = (Player receiver) =>
            {
                GuildNewsPkt newsPacket = new();
                news.WritePacket(newsPacket);
                //newsPacket.NewsEvents.Last().CompletedDate += receiver.GetSession().GetTimezoneOffset();

                receiver.SendPacket(newsPacket);
            };

            BroadcastWorker(packetBuilder);
        }

        bool HasAchieved(int achievementId)
        {
            return GetAchievementMgr().HasAchieved(achievementId);
        }

        public void UpdateCriteria(CriteriaType type, long miscValue1, long miscValue2, long miscValue3, WorldObject refe, Player player)
        {
            GetAchievementMgr().UpdateCriteria(type, miscValue1, miscValue2, miscValue3, refe, player);
        }

        public void HandleNewsSetSticky(WorldSession session, int newsId, bool sticky)
        {
            var newsLog = m_newsLog.GetGuildLog().Find(p => p.GetGUID() == newsId);
            if (newsLog == null)
            {
                Log.outDebug(LogFilter.Guild,
                    $"HandleNewsSetSticky: [{session.GetPlayerInfo()}] " +
                    $"requested unknown newsId {newsId} - Sticky: {sticky}");
                return;
            }

            newsLog.SetSticky(sticky);

            GuildNewsPkt newsPacket = new();
            newsLog.WritePacket(newsPacket);
            //newsPacket.NewsEvents.Last().CompletedDate += session.GetTimezoneOffset();
            session.SendPacket(newsPacket);
        }

        public long GetId() { return m_id; }
        public ObjectGuid GetGUID() { return ObjectGuid.Create(HighGuid.Guild, m_id); }
        public ObjectGuid GetLeaderGUID() { return m_leaderGuid; }
        public string GetName() { return m_name; }
        public string GetMOTD() { return m_motd; }
        public string GetInfo() { return m_info; }
        public ServerTime GetCreatedDate() { return m_createdDate; }
        public long GetBankMoney() { return m_bankMoney; }

        public void BroadcastWorker(IDoWork<Player> _do, Player except = null)
        {
            foreach (var member in m_members.Values)
            {
                Player player = member.FindPlayer();
                if (player != null)
                {
                    if (player != except)
                        _do.Invoke(player);
                }
            }
        }

        public void BroadcastWorker(Action<Player> _do, Player except = null)
        {
            foreach (var member in m_members.Values)
            {
                Player player = member.FindPlayer();
                if (player != null)
                {
                    if (player != except)
                        _do.Invoke(player);
                }
            }
        }

        public int GetMembersCount() { return m_members.Count; }

        public GuildAchievementMgr GetAchievementMgr() { return m_achievementSys; }

        // Pre-6.x guild leveling
        public byte GetLevel() { return GuildConst.OldMaxLevel; }

        public EmblemInfo GetEmblemInfo() { return m_emblemInfo; }

        byte _GetRanksSize() { return (byte)m_ranks.Count; }

        RankInfo GetRankInfo(uint rankId) { return rankId < _GetRanksSize() ? m_ranks[(int)rankId] : null; }

        bool _HasRankRight(Player player, GuildRankRights right)
        {
            if (player != null)
            {
                Member member = GetMember(player.GetGUID());

                if (member != null)
                    return _GetRankRights(member.GetRankId()).HasFlag(right);

                return false;
            }

            return false;
        }

        GuildRankId _GetLowestRankId() { return m_ranks.Last().GetId(); }

        byte _GetPurchasedTabsSize() { return (byte)m_bankTabs.Count; }

        BankTab GetBankTab(byte tabId) { return tabId < m_bankTabs.Count ? m_bankTabs[tabId] : null; }

        public Member GetMember(ObjectGuid guid)
        {
            return m_members.LookupByKey(guid);
        }

        public Member GetMember(string name)
        {
            foreach (var member in m_members.Values)
            {
                if (member.GetName() == name)
                    return member;
            }
            return null;
        }

        void _DeleteMemberFromDB(SQLTransaction trans, long lowguid)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_MEMBER);
            stmt.SetInt64(0, lowguid);
            DB.Characters.ExecuteOrAppend(trans, stmt);
        }

        long GetGuildBankTabPrice(byte tabId)
        {
            // these prices are in gold units, not copper
            switch (tabId)
            {
                case 0: return 100;
                case 1: return 250;
                case 2: return 500;
                case 3: return 1000;
                case 4: return 2500;
                case 5: return 5000;
                default: return 0;
            }
        }

        public static void SendCommandResult(WorldSession session, GuildCommandType type, GuildCommandError errCode, string param = "")
        {
            GuildCommandResult resultPacket = new();
            resultPacket.Command = type;
            resultPacket.Result = errCode;
            resultPacket.Name = param;
            session.SendPacket(resultPacket);
        }

        public static void SendSaveEmblemResult(WorldSession session, GuildEmblemError errCode)
        {
            PlayerSaveGuildEmblem saveResponse = new();
            saveResponse.Error = errCode;
            session.SendPacket(saveResponse);
        }

        #region Fields
        long m_id;
        string m_name;
        ObjectGuid m_leaderGuid;
        string m_motd;
        string m_info;
        ServerTime m_createdDate;

        EmblemInfo m_emblemInfo = new();
        int m_accountsNumber;
        long m_bankMoney;

        List<RankInfo> m_ranks = new();
        Dictionary<ObjectGuid, Member> m_members = new();
        List<BankTab> m_bankTabs = new();

        // These are actually ordered lists. The first element is the oldest entry.
        LogHolder<EventLogEntry> m_eventLog = new();
        LogHolder<BankEventLogEntry>[] m_bankEventLog = new LogHolder<BankEventLogEntry>[GuildConst.MaxBankTabs + 1];
        LogHolder<NewsLogEntry> m_newsLog = new();
        GuildAchievementMgr m_achievementSys;
        #endregion

        #region Classes
        public class Member
        {
            public Member(long guildId, ObjectGuid guid, GuildRankId rankId)
            {
                m_guildId = guildId;
                m_guid = guid;
                m_zoneId = 0;
                m_level = 0;
                m_class = 0;
                m_flags = GuildMemberFlags.None;
                m_logoutTime = ServerTime.Zero;
                m_accountId = 0;
                m_rankId = rankId;
                m_achievementPoints = 0;
                m_totalActivity = 0;
                m_weekActivity = 0;
                m_totalReputation = 0;
                m_weekReputation = 0;
            }

            public void SetStats(Player player)
            {
                m_name = player.GetName();
                m_level = (byte)player.GetLevel();
                m_race = player.GetRace();
                m_class = player.GetClass();
                _gender = player.GetNativeGender();
                m_zoneId = player.GetZoneId();
                m_accountId = player.GetSession().GetAccountId();
                m_achievementPoints = player.GetAchievementPoints();
            }

            public void SetStats(string name, byte level, Race race, Class _class, Gender gender, int zoneId, int accountId, int reputation)
            {
                m_name = name;
                m_level = level;
                m_race = race;
                m_class = _class;
                _gender = gender;
                m_zoneId = zoneId;
                m_accountId = accountId;
                m_totalReputation = reputation;
            }

            public void SetPublicNote(string publicNote)
            {
                if (m_publicNote == publicNote)
                    return;

                m_publicNote = publicNote;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_MEMBER_PNOTE);
                stmt.SetString(0, publicNote);
                stmt.SetInt64(1, m_guid.GetCounter());
                DB.Characters.Execute(stmt);
            }

            public void SetOfficerNote(string officerNote)
            {
                if (m_officerNote == officerNote)
                    return;

                m_officerNote = officerNote;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_MEMBER_OFFNOTE);
                stmt.SetString(0, officerNote);
                stmt.SetInt64(1, m_guid.GetCounter());
                DB.Characters.Execute(stmt);
            }

            public void ChangeRank(SQLTransaction trans, GuildRankId newRank)
            {
                m_rankId = newRank;

                // Update rank information in player's field, if he is online.
                Player player = FindConnectedPlayer();
                if (player != null)
                    player.SetGuildRank((byte)newRank);

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_MEMBER_RANK);
                stmt.SetUInt8(0, (byte)newRank);
                stmt.SetInt64(1, m_guid.GetCounter());
                DB.Characters.ExecuteOrAppend(trans, stmt);
            }

            public void SaveToDB(SQLTransaction trans)
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_MEMBER);
                stmt.SetInt64(0, m_guildId);
                stmt.SetInt64(1, m_guid.GetCounter());
                stmt.SetUInt8(2, (byte)m_rankId);
                stmt.SetString(3, m_publicNote);
                stmt.SetString(4, m_officerNote);
                DB.Characters.ExecuteOrAppend(trans, stmt);
            }

            public bool LoadFromDB(SQLFields field)
            {
                m_publicNote = field.Read<string>(3);
                m_officerNote = field.Read<string>(4);

                for (byte i = 0; i < GuildConst.MaxBankTabs; ++i)
                    m_bankWithdraw[i] = field.Read<int>(5 + i);

                m_bankWithdrawMoney = field.Read<long>(13);

                SetStats(field.Read<string>(14),
                         field.Read<byte>(15),                                  // characters.level
                         (Race)field.Read<byte>(16),                            // characters.race
                         (Class)field.Read<byte>(17),                           // characters.class
                         (Gender)field.Read<byte>(18),                          // characters.gender
                         field.Read<ushort>(19),                                // characters.zone
                         field.Read<int>(20),                                   // characters.account
                         0);
                m_logoutTime = (ServerTime)(UnixTime64)field.Read<long>(21);    // characters.logout_time
                m_totalActivity = 0;
                m_weekActivity = 0;
                m_weekReputation = 0;

                if (!CheckStats())
                    return false;

                if (m_zoneId == 0)
                {
                    Log.outError(LogFilter.Guild, $"Player ({m_guid}) has broken zone-data");
                    m_zoneId = Player.GetZoneIdFromDB(m_guid);
                }
                ResetFlags();
                return true;
            }

            public bool CheckStats()
            {
                if (m_level < 1)
                {
                    Log.outError(LogFilter.Guild,
                        $"{m_guid} has a broken data in field `characters`.`level`, deleting him from guild!");
                    return false;
                }

                if (!CliDB.ChrRacesStorage.ContainsKey((int)m_race))
                {
                    Log.outError(LogFilter.Guild, 
                        $"{m_guid} has a broken data in field `characters`.`race`, deleting him from guild!");
                    return false;
                }

                if (!CliDB.ChrClassesStorage.ContainsKey(m_class))
                {
                    Log.outError(LogFilter.Guild, 
                        $"{m_guid} has a broken data in field `characters`.`class`, deleting him from guild!");
                    return false;
                }
                return true;
            }

            public float GetInactiveDays()
            {
                if (IsOnline())
                    return 0.0f;
                return (float)(LoopTime.ServerTime - GetLogoutTime()).TotalDays;
            }

            // Decreases amount of slots left for today.
            public void UpdateBankTabWithdrawValue(SQLTransaction trans, byte tabId, int amount)
            {
                m_bankWithdraw[tabId] += amount;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_MEMBER_WITHDRAW_TABS);
                stmt.SetInt64(0, m_guid.GetCounter());
                for (byte i = 0; i < GuildConst.MaxBankTabs;)
                {
                    int withdraw = m_bankWithdraw[i++];
                    stmt.SetInt32(i, withdraw);
                }

                DB.Characters.ExecuteOrAppend(trans, stmt);
            }

            // Decreases amount of money left for today.
            public void UpdateBankMoneyWithdrawValue(SQLTransaction trans, long amount)
            {
                m_bankWithdrawMoney += amount;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_MEMBER_WITHDRAW_MONEY);
                stmt.SetInt64(0, m_guid.GetCounter());
                stmt.SetInt64(1, m_bankWithdrawMoney);
                DB.Characters.ExecuteOrAppend(trans, stmt);
            }

            public void ResetValues(bool weekly = false)
            {
                for (byte tabId = 0; tabId < GuildConst.MaxBankTabs; ++tabId)
                    m_bankWithdraw[tabId] = 0;

                m_bankWithdrawMoney = 0;

                if (weekly)
                {
                    m_weekActivity = 0;
                    m_weekReputation = 0;
                }
            }

            public void SetZoneId(int id) { m_zoneId = id; }

            public void SetAchievementPoints(int val) { m_achievementPoints = val; }

            public void SetLevel(int var) { m_level = (byte)var; }

            public void AddFlag(GuildMemberFlags var) { m_flags |= var; }

            public void RemoveFlag(GuildMemberFlags var) { m_flags &= ~var; }

            public void ResetFlags() { m_flags = GuildMemberFlags.None; }

            public ObjectGuid GetGUID() { return m_guid; }
            public string GetName() { return m_name; }
            public int GetAccountId() { return m_accountId; }
            public GuildRankId GetRankId() { return m_rankId; }
            public ServerTime GetLogoutTime() { return m_logoutTime; }
            public string GetPublicNote() { return m_publicNote; }
            public string GetOfficerNote() { return m_officerNote; }
            public Race GetRace() { return m_race; }
            public Class GetClass() { return m_class; }
            public Gender GetGender() { return _gender; }
            public byte GetLevel() { return m_level; }
            public GuildMemberFlags GetFlags() { return m_flags; }
            public int GetZoneId() { return m_zoneId; }
            public int GetAchievementPoints() { return m_achievementPoints; }
            public ulong GetTotalActivity() { return m_totalActivity; }
            public ulong GetWeekActivity() { return m_weekActivity; }
            public int GetTotalReputation() { return m_totalReputation; }
            public int GetWeekReputation() { return m_weekReputation; }

            public void SetTrackedCriteriaIds(List<int> criteriaIds) { m_trackedCriteriaIds = criteriaIds; }
            public bool IsTrackingCriteriaId(int criteriaId) { return m_trackedCriteriaIds.Contains(criteriaId); }
            public bool IsOnline() { return m_flags.HasFlag(GuildMemberFlags.Online); }

            public void UpdateLogoutTime() { m_logoutTime = LoopTime.ServerTime; }
            public bool IsRank(GuildRankId rankId) { return m_rankId == rankId; }
            public bool IsSamePlayer(ObjectGuid guid) { return m_guid == guid; }

            public int GetBankTabWithdrawValue(byte tabId) { return m_bankWithdraw[tabId]; }
            public long GetBankMoneyWithdrawValue() { return m_bankWithdrawMoney; }

            public Player FindPlayer() { return Global.ObjAccessor.FindPlayer(m_guid); }
            Player FindConnectedPlayer() { return Global.ObjAccessor.FindConnectedPlayer(m_guid); }

            #region Fields
            long m_guildId;
            ObjectGuid m_guid;
            string m_name;
            int m_zoneId;
            byte m_level;
            Race m_race;
            Class m_class;
            Gender _gender;
            GuildMemberFlags m_flags;
            ServerTime m_logoutTime;
            int m_accountId;
            GuildRankId m_rankId;
            string m_publicNote = "";
            string m_officerNote = "";

            List<int> m_trackedCriteriaIds = new();

            int[] m_bankWithdraw = new int[GuildConst.MaxBankTabs];
            long m_bankWithdrawMoney;
            int m_achievementPoints;
            ulong m_totalActivity;
            ulong m_weekActivity;
            int m_totalReputation;
            int m_weekReputation;
            #endregion
        }

        public class LogEntry
        {
            public LogEntry(long guildId, int guid)
            {
                m_guildId = guildId;
                m_guid = guid;
                m_timestamp = LoopTime.ServerTime;
            }

            public LogEntry(long guildId, int guid, ServerTime timestamp)
            {
                m_guildId = guildId;
                m_guid = guid;
                m_timestamp = timestamp;
            }

            public int GetGUID() { return m_guid; }
            public ServerTime GetTimestamp() { return m_timestamp; }

            public virtual void SaveToDB(SQLTransaction trans) { }

            public long m_guildId;
            public int m_guid;
            public ServerTime m_timestamp;
        }

        public class EventLogEntry : LogEntry
        {
            public EventLogEntry(long guildId, int guid, GuildEventLogTypes eventType, long playerGuid1, long playerGuid2, byte newRank)
                : base(guildId, guid)
            {
                m_eventType = eventType;
                m_playerGuid1 = playerGuid1;
                m_playerGuid2 = playerGuid2;
                m_newRank = newRank;
            }

            public EventLogEntry(long guildId, int guid, ServerTime timestamp, GuildEventLogTypes eventType, long playerGuid1, long playerGuid2, byte newRank)
                : base(guildId, guid, timestamp)
            {
                m_eventType = eventType;
                m_playerGuid1 = playerGuid1;
                m_playerGuid2 = playerGuid2;
                m_newRank = newRank;
            }

            public override void SaveToDB(SQLTransaction trans)
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_EVENTLOG);
                stmt.SetInt64(0, m_guildId);
                stmt.SetInt32(1, m_guid);
                trans.Append(stmt);

                byte index = 0;
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_EVENTLOG);
                stmt.SetInt64(index, m_guildId);
                stmt.SetInt32(++index, m_guid);
                stmt.SetUInt8(++index, (byte)m_eventType);
                stmt.SetInt64(++index, m_playerGuid1);
                stmt.SetInt64(++index, m_playerGuid2);
                stmt.SetUInt8(++index, m_newRank);
                stmt.SetInt64(++index, (UnixTime64)m_timestamp);
                trans.Append(stmt);
            }

            public void WritePacket(GuildEventLogQueryResults packet)
            {
                ObjectGuid playerGUID = ObjectGuid.Create(HighGuid.Player, m_playerGuid1);
                ObjectGuid otherGUID = ObjectGuid.Create(HighGuid.Player, m_playerGuid2);

                GuildEventEntry eventEntry = new();
                eventEntry.PlayerGUID = playerGUID;
                eventEntry.OtherGUID = otherGUID;
                eventEntry.TransactionType = (byte)m_eventType;
                eventEntry.TransactionDate = (Seconds)(LoopTime.ServerTime - m_timestamp);
                eventEntry.RankID = m_newRank;
                packet.Entry.Add(eventEntry);
            }

            GuildEventLogTypes m_eventType;
            long m_playerGuid1;
            long m_playerGuid2;
            byte m_newRank;
        }

        public class BankEventLogEntry : LogEntry
        {
            public BankEventLogEntry(long guildId, int guid, GuildBankEventLogTypes eventType, byte tabId, long playerGuid, long itemOrMoney, ushort itemStackCount, byte destTabId)
                : base(guildId, guid)
            {
                m_eventType = eventType;
                m_bankTabId = tabId;
                m_playerGuid = playerGuid;
                m_itemOrMoney = itemOrMoney;
                m_itemStackCount = itemStackCount;
                m_destTabId = destTabId;
            }

            public BankEventLogEntry(long guildId, int guid, ServerTime timestamp, byte tabId, GuildBankEventLogTypes eventType, long playerGuid, long itemOrMoney, ushort itemStackCount, byte destTabId)
                : base(guildId, guid, timestamp)
            {
                m_eventType = eventType;
                m_bankTabId = tabId;
                m_playerGuid = playerGuid;
                m_itemOrMoney = itemOrMoney;
                m_itemStackCount = itemStackCount;
                m_destTabId = destTabId;
            }

            public static bool IsMoneyEvent(GuildBankEventLogTypes eventType)
            {
                return
                    eventType == GuildBankEventLogTypes.DepositMoney ||
                    eventType == GuildBankEventLogTypes.WithdrawMoney ||
                    eventType == GuildBankEventLogTypes.RepairMoney ||
                    eventType == GuildBankEventLogTypes.CashFlowDeposit;
            }

            bool IsMoneyEvent()
            {
                return IsMoneyEvent(m_eventType);
            }

            public override void SaveToDB(SQLTransaction trans)
            {
                byte index = 0;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_EVENTLOG);
                stmt.SetInt64(index, m_guildId);
                stmt.SetInt32(++index, m_guid);
                stmt.SetUInt8(++index, m_bankTabId);
                trans.Append(stmt);

                index = 0;
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_BANK_EVENTLOG);
                stmt.SetInt64(index, m_guildId);
                stmt.SetInt32(++index, m_guid);
                stmt.SetUInt8(++index, m_bankTabId);
                stmt.SetUInt8(++index, (byte)m_eventType);
                stmt.SetInt64(++index, m_playerGuid);
                stmt.SetInt64(++index, m_itemOrMoney);
                stmt.SetUInt16(++index, m_itemStackCount);
                stmt.SetUInt8(++index, m_destTabId);
                stmt.SetInt64(++index, (UnixTime64)m_timestamp);
                trans.Append(stmt);
            }

            public void WritePacket(GuildBankLogQueryResults packet)
            {
                ObjectGuid logGuid = ObjectGuid.Create(HighGuid.Player, m_playerGuid);

                bool hasItem = m_eventType == GuildBankEventLogTypes.DepositItem || m_eventType == GuildBankEventLogTypes.WithdrawItem ||
                               m_eventType == GuildBankEventLogTypes.MoveItem || m_eventType == GuildBankEventLogTypes.MoveItem2;

                bool itemMoved = (m_eventType == GuildBankEventLogTypes.MoveItem || m_eventType == GuildBankEventLogTypes.MoveItem2);

                bool hasStack = (hasItem && m_itemStackCount > 1) || itemMoved;

                GuildBankLogEntry bankLogEntry = new();
                bankLogEntry.PlayerGUID = logGuid;
                bankLogEntry.TimeOffset = (Seconds)(LoopTime.ServerTime - m_timestamp);
                bankLogEntry.EntryType = (sbyte)m_eventType;

                if (hasStack)
                    bankLogEntry.Count = m_itemStackCount;

                if (IsMoneyEvent())
                    bankLogEntry.Money = m_itemOrMoney;

                if (hasItem)
                    bankLogEntry.ItemID = (int)m_itemOrMoney;

                if (itemMoved)
                    bankLogEntry.OtherTab = (sbyte)m_destTabId;

                packet.Entry.Add(bankLogEntry);
            }

            GuildBankEventLogTypes m_eventType;
            byte m_bankTabId;
            long m_playerGuid;
            long m_itemOrMoney;
            ushort m_itemStackCount;
            byte m_destTabId;
        }

        public class NewsLogEntry : LogEntry
        {
            public NewsLogEntry(long guildId, int guid, GuildNews type, ObjectGuid playerGuid, uint flags, int value)
                : base(guildId, guid)
            {
                m_type = type;
                m_playerGuid = playerGuid;
                m_flags = (int)flags;
                m_value = value;
            }

            public NewsLogEntry(long guildId, int guid, ServerTime timestamp, GuildNews type, ObjectGuid playerGuid, uint flags, int value)
                : base(guildId, guid, timestamp)
            {
                m_type = type;
                m_playerGuid = playerGuid;
                m_flags = (int)flags;
                m_value = value;
            }

            public GuildNews GetNewsType() { return m_type; }

            public ObjectGuid GetPlayerGuid() { return m_playerGuid; }

            public int GetValue() { return m_value; }

            public int GetFlags() { return m_flags; }

            public void SetSticky(bool sticky)
            {
                if (sticky)
                    m_flags |= 1;
                else
                    m_flags &= ~1;
            }

            public override void SaveToDB(SQLTransaction trans)
            {
                byte index = 0;
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_NEWS);
                stmt.SetInt64(index, m_guildId);
                stmt.SetInt32(++index, GetGUID());
                stmt.SetUInt8(++index, (byte)GetNewsType());
                stmt.SetInt64(++index, GetPlayerGuid().GetCounter());
                stmt.SetInt32(++index, GetFlags());
                stmt.SetInt32(++index, GetValue());
                stmt.SetInt64(++index, (UnixTime64)GetTimestamp());
                DB.Characters.ExecuteOrAppend(trans, stmt);
            }

            public void WritePacket(GuildNewsPkt newsPacket)
            {
                GuildNewsEvent newsEvent = new();
                newsEvent.Id = GetGUID();
                newsEvent.MemberGuid = GetPlayerGuid();
                newsEvent.CompletedDate = (RealmTime)GetTimestamp();
                newsEvent.Flags = GetFlags();
                newsEvent.Type = (int)GetNewsType();

                //for (public byte i = 0; i < 2; i++)
                //    newsEvent.Data[i] =

                //newsEvent.MemberList.push_back(MemberGuid);

                if (GetNewsType() == GuildNews.ItemLooted || GetNewsType() == GuildNews.ItemCrafted || GetNewsType() == GuildNews.ItemPurchased)
                {
                    ItemInstance itemInstance = new();
                    itemInstance.ItemID = GetValue();
                    newsEvent.Item = itemInstance;
                }

                newsPacket.NewsEvents.Add(newsEvent);
            }

            GuildNews m_type;
            ObjectGuid m_playerGuid;
            int m_flags;
            int m_value;
        }

        public class LogHolder<T> where T : LogEntry
        {
            List<T> m_log = new();
            int m_maxRecords;
            int m_nextGUID;

            public LogHolder()
            {
                m_maxRecords = WorldConfig.Values[typeof(T) == typeof(BankEventLogEntry) ? WorldCfg.GuildBankEventLogCount : WorldCfg.GuildEventLogCount].Int32;
                m_nextGUID = GuildConst.EventLogGuidUndefined;
            }

            // Checks if new log entry can be added to holder
            public bool CanInsert() { return m_log.Count < m_maxRecords; }

            public byte GetSize() { return (byte)m_log.Count; }

            public void LoadEvent(T entry)
            {
                if (m_nextGUID == GuildConst.EventLogGuidUndefined)
                    m_nextGUID = entry.GetGUID();

                m_log.Insert(0, entry);
            }

            public T AddEvent(SQLTransaction trans, T entry)
            {
                // Check max records limit
                if (!CanInsert())
                    m_log.RemoveAt(0);

                // Add event to list
                m_log.Add(entry);

                // Save to DB
                entry.SaveToDB(trans);

                return entry;
            }

            public int GetNextGUID()
            {
                if (m_nextGUID == GuildConst.EventLogGuidUndefined)
                    m_nextGUID = 0;
                else
                    m_nextGUID = (m_nextGUID + 1) % m_maxRecords;

                return m_nextGUID;
            }

            public List<T> GetGuildLog() { return m_log; }
        }

        public class RankInfo
        {
            public RankInfo(long guildId = 0)
            {
                m_guildId = guildId;
                m_rankId = (GuildRankId)0xFF;
                m_rankOrder = 0;
                m_rights = GuildRankRights.None;
                m_bankGoldPerDay = 0;

                for (var i = 0; i < GuildConst.MaxBankTabs; ++i)
                    m_bankTabRightsAndSlots[i] = new GuildBankRightsAndSlots();
            }

            public RankInfo(long guildId, GuildRankId rankId, GuildRankOrder rankOrder, string name, GuildRankRights rights, int money)
            {
                m_guildId = guildId;
                m_rankId = rankId;
                m_rankOrder = rankOrder;
                m_name = name;
                m_rights = rights;
                m_bankGoldPerDay = money;

                for (var i = 0; i < GuildConst.MaxBankTabs; ++i)
                    m_bankTabRightsAndSlots[i] = new GuildBankRightsAndSlots();
            }

            public void LoadFromDB(SQLFields field)
            {
                m_rankId = (GuildRankId)field.Read<byte>(1);
                m_rankOrder = (GuildRankOrder)field.Read<byte>(2);
                m_name = field.Read<string>(3);
                m_rights = (GuildRankRights)field.Read<uint>(4);//TODO: Change to singed in DB
                m_bankGoldPerDay = (int)field.Read<uint>(5); //TODO: Change to singed in DB
            }

            public void SaveToDB(SQLTransaction trans)
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_RANK);
                stmt.SetInt64(0, m_guildId);
                stmt.SetUInt8(1, (byte)m_rankId);
                stmt.SetUInt8(2, (byte)m_rankOrder);
                stmt.SetString(3, m_name);
                stmt.SetUInt32(4, (uint)m_rights);
                stmt.SetUInt32(5, (uint)m_bankGoldPerDay);
                DB.Characters.ExecuteOrAppend(trans, stmt);
            }

            public void CreateMissingTabsIfNeeded(byte tabs, SQLTransaction trans, bool logOnCreate = false)
            {
                for (byte i = 0; i < tabs; ++i)
                {
                    GuildBankRightsAndSlots rightsAndSlots = m_bankTabRightsAndSlots[i];
                    if (rightsAndSlots.GetTabId() == i)
                        continue;

                    rightsAndSlots.SetTabId(i);
                    if (m_rankId == GuildRankId.GuildMaster)
                        rightsAndSlots.SetGuildMasterValues();

                    if (logOnCreate)
                    {
                        Log.outError(LogFilter.Guild, 
                            $"Guild {m_guildId} has broken Tab {i} for rank {m_rankId}. Created default tab.");
                    }

                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_BANK_RIGHT);
                    stmt.SetInt64(0, m_guildId);
                    stmt.SetUInt8(1, i);
                    stmt.SetUInt8(2, (byte)m_rankId);
                    stmt.SetInt8(3, (sbyte)rightsAndSlots.GetRights());
                    stmt.SetInt32(4, rightsAndSlots.GetSlots());
                    trans.Append(stmt);
                }
            }

            public void SetName(string name)
            {
                if (m_name == name)
                    return;

                m_name = name;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_RANK_NAME);
                stmt.SetString(0, m_name);
                stmt.SetUInt8(1, (byte)m_rankId);
                stmt.SetInt64(2, m_guildId);
                DB.Characters.Execute(stmt);
            }

            public void SetRights(GuildRankRights rights)
            {
                if (m_rankId == GuildRankId.GuildMaster)                     // Prevent loss of leader rights
                    rights = GuildRankRights.AllPermanent;

                if (m_rights == rights)
                    return;

                m_rights = rights;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_RANK_RIGHTS);
                stmt.SetUInt32(0, (uint)m_rights);
                stmt.SetUInt8(1, (byte)m_rankId);
                stmt.SetInt64(2, m_guildId);
                DB.Characters.Execute(stmt);
            }

            public void SetBankGoldPerDay(int goldPerDay)
            {
                if (m_bankGoldPerDay == goldPerDay)
                    return;

                if (goldPerDay > GuildConst.WithdrawGoldPerDayLimit)
                    goldPerDay = GuildConst.WithdrawGoldPerDayLimit;

                if (goldPerDay < 0)
                    goldPerDay = 0;

                m_bankGoldPerDay = goldPerDay;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_RANK_BANK_MONEY);
                stmt.SetUInt32(0, (uint)goldPerDay);
                stmt.SetUInt8(1, (byte)m_rankId);
                stmt.SetInt64(2, m_guildId);
                DB.Characters.Execute(stmt);
            }

            public void SetBankTabSlotsAndRights(GuildBankRightsAndSlots rightsAndSlots, bool saveToDB)
            {
                if (m_rankId == GuildRankId.GuildMaster)                     // Prevent loss of leader rights
                {
                    rightsAndSlots.SetGuildMasterValues();
                }
                else
                {
                    if (rightsAndSlots.GetSlots() > GuildConst.WithdrawSlotsLimit)
                        rightsAndSlots.SetSlots(GuildConst.WithdrawSlotsLimit);

                    if (rightsAndSlots.GetSlots() < 0)
                        rightsAndSlots.SetSlots(0);
                }                

                m_bankTabRightsAndSlots[rightsAndSlots.GetTabId()] = rightsAndSlots;

                if (saveToDB)
                {
                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_BANK_RIGHT);
                    stmt.SetInt64(0, m_guildId);
                    stmt.SetUInt8(1, rightsAndSlots.GetTabId());
                    stmt.SetUInt8(2, (byte)m_rankId);
                    stmt.SetInt8(3, (sbyte)rightsAndSlots.GetRights());
                    stmt.SetInt32(4, rightsAndSlots.GetSlots());
                    DB.Characters.Execute(stmt);
                }
            }

            public GuildRankId GetId() { return m_rankId; }

            public GuildRankOrder GetOrder() { return m_rankOrder; }

            public void SetOrder(GuildRankOrder rankOrder) { m_rankOrder = rankOrder; }

            public string GetName() { return m_name; }

            public GuildRankRights GetRights() { return m_rights; }

            public int GetBankGoldPerDay()
            {
                return m_bankGoldPerDay;
            }

            public GuildBankRights GetBankTabRights(byte tabId)
            {
                return m_bankTabRightsAndSlots[tabId].GetRights();
            }

            public int GetBankTabSlotsPerDay(byte tabId)
            {
                return m_bankTabRightsAndSlots[tabId].GetSlots();
            }

            long m_guildId;
            GuildRankId m_rankId;
            GuildRankOrder m_rankOrder;
            string m_name;
            GuildRankRights m_rights;
            int m_bankGoldPerDay;
            GuildBankRightsAndSlots[] m_bankTabRightsAndSlots = new GuildBankRightsAndSlots[GuildConst.MaxBankTabs];
        }

        public class BankTab
        {
            public BankTab(Guild guild, byte tabId)
            {
                m_guild = guild;
                m_tabId = tabId;
                m_items = new Item[GuildConst.MaxBankSlots];
            }

            public void LoadFromDB(SQLFields field)
            {
                m_name = field.Read<string>(2);
                m_icon = field.Read<string>(3);
                m_text = field.Read<string>(4);
            }

            public bool LoadItemFromDB(SQLFields field)
            {
                var slotId = field.Read<byte>(48);
                var itemGuid = field.Read<long>(0);
                var itemEntry = field.Read<int>(1);
                if (slotId >= GuildConst.MaxBankSlots)
                {
                    Log.outError(LogFilter.Guild, 
                        $"Invalid slot for item (GUID: {itemGuid}, id: {itemEntry}) in guild bank, skipped.");
                    return false;
                }

                var proto = Global.ObjectMgr.GetItemTemplate(itemEntry);
                if (proto == null)
                {
                    Log.outError(LogFilter.Guild, 
                        $"Unknown item (GUID: {itemGuid}, id: {itemEntry}) in guild bank, skipped.");
                    return false;
                }

                var pItem = Item.NewItemOrBag(proto);
                if (!pItem.LoadFromDB(itemGuid, ObjectGuid.Empty, field, itemEntry))
                {
                    Log.outError(LogFilter.Guild,
                        $"Item (GUID {itemGuid}, id: {itemEntry}) " +
                        $"not found in item_instance, deleting from guild bank!");

                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_NONEXISTENT_GUILD_BANK_ITEM);
                    stmt.SetInt64(0, m_guild.GetId());
                    stmt.SetUInt8(1, m_tabId);
                    stmt.SetUInt8(2, slotId);
                    DB.Characters.Execute(stmt);
                    return false;
                }

                pItem.AddToWorld();
                m_items[slotId] = pItem;
                return true;
            }

            public void Delete(SQLTransaction trans, bool removeItemsFromDB = false)
            {
                for (byte slotId = 0; slotId < GuildConst.MaxBankSlots; ++slotId)
                {
                    Item pItem = m_items[slotId];
                    if (pItem != null)
                    {
                        pItem.RemoveFromWorld();
                        if (removeItemsFromDB)
                            pItem.DeleteFromDB(trans);
                    }
                }
            }

            public void SetInfo(string name, string icon)
            {
                if (m_name == name && m_icon == icon)
                    return;

                m_name = name;
                m_icon = icon;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_BANK_TAB_INFO);
                stmt.SetString(0, m_name);
                stmt.SetString(1, m_icon);
                stmt.SetInt64(2, m_guild.GetId());
                stmt.SetUInt8(3, m_tabId);
                DB.Characters.Execute(stmt);
            }

            public void SetText(string text)
            {
                if (m_text == text)
                    return;

                m_text = text;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_BANK_TAB_TEXT);
                stmt.SetString(0, m_text);
                stmt.SetInt64(1, m_guild.GetId());
                stmt.SetUInt8(2, m_tabId);
                DB.Characters.Execute(stmt);
            }

            public void SendText(Guild guild, WorldSession session = null)
            {
                GuildBankTextQueryResult textQuery = new();
                textQuery.Tab = m_tabId;
                textQuery.Text = m_text;

                if (session != null)
                {
                    Log.outDebug(LogFilter.Guild, 
                        $"SMSG_GUILD_BANK_QUERY_TEXT_RESULT [{session.GetPlayerInfo()}]: Tabid: {m_tabId}, Text: {m_text}");
                    session.SendPacket(textQuery);
                }
                else
                {
                    Log.outDebug(LogFilter.Guild, 
                        $"SMSG_GUILD_BANK_QUERY_TEXT_RESULT [Broadcast]: Tabid: {m_tabId}, Text: {m_text}");
                    guild.BroadcastPacket(textQuery);
                }
            }

            public string GetName() { return m_name; }

            public string GetIcon() { return m_icon; }

            public string GetText() { return m_text; }

            public Item GetItem(byte slotId) { return slotId < GuildConst.MaxBankSlots ? m_items[slotId] : null; }

            public bool SetItem(SQLTransaction trans, byte slotId, Item item)
            {
                if (slotId >= GuildConst.MaxBankSlots)
                    return false;

                m_items[slotId] = item;

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_ITEM);
                stmt.SetInt64(0, m_guild.GetId());
                stmt.SetUInt8(1, m_tabId);
                stmt.SetUInt8(2, slotId);
                trans.Append(stmt);

                if (item != null)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_GUILD_BANK_ITEM);
                    stmt.SetInt64(0, m_guild.GetId());
                    stmt.SetUInt8(1, m_tabId);
                    stmt.SetUInt8(2, slotId);
                    stmt.SetInt64(3, item.GetGUID().GetCounter());
                    trans.Append(stmt);

                    item.SetContainedIn(ObjectGuid.Empty);
                    item.SetOwnerGUID(ObjectGuid.Empty);
                    item.FSetState(ItemUpdateState.New);
                    item.SaveToDB(trans);                                 // Not in inventory and can be saved standalone
                }

                return true;
            }        

            private void InitializeBankQueryResult(Guild guild)
            {
                if (IsBankQueryResultInitialized)
                    return;

                m_preparedBankQueryResult = new(m_tabId);

                // TabInfo
                for (byte tabId = 0; tabId < guild._GetPurchasedTabsSize(); ++tabId)
                {
                    GuildBankTabInfo tabInfo = new(tabId, guild.GetBankTab(tabId));
                    m_preparedBankQueryResult.TabInfo[tabId] = tabInfo;
                }

                // ItemInfo
                for (byte slotId = 0; slotId < m_items.Length; ++slotId)
                {
                    Item tabItem = m_items[slotId];
                    if (tabItem != null)
                    {
                        GuildBankItemInfo itemInfo = new(slotId, tabItem);
                        m_preparedBankQueryResult.ItemInfo[slotId] = itemInfo;
                    }
                }
            }

            private void PrepareBankQueryResultPacket()
            {
                m_preparedBankQueryResult.Write();
            }

            public GuildBankQueryResults GetPreparedBankQueryResultPacket()
            {
                if (!IsBankQueryResultInitialized)
                    InitializeBankQueryResult(m_guild);

                if (IsBankQueryResultUpdated)
                    PrepareBankQueryResultPacket();

                IsBankQueryResultUpdated = false;

                return m_preparedBankQueryResult;
            }

            public void UpdateItemInBankQueryResult(List<ItemSlot> slotsForUpdate)
            {
                if (!IsBankQueryResultInitialized)
                    return;

                foreach (var slot in slotsForUpdate)
                {
                    m_preparedBankQueryResult.ItemInfo[slot] = new(slot, m_items[slot]);
                }

                IsBankQueryResultUpdated = true;
            }

            public void UpdateTabInfoInBankQueryResult(int tabId, BankTab tabInfo)
            {
                if (!IsBankQueryResultInitialized)
                    return;

                m_preparedBankQueryResult.TabInfo[tabId] = new(tabId, tabInfo);
                IsBankQueryResultUpdated = true;
            }

            Guild m_guild;
            byte m_tabId;
            Item[] m_items;
            string m_name;
            string m_icon;
            string m_text;
            GuildBankQueryResults m_preparedBankQueryResult;
            bool IsBankQueryResultUpdated;
            bool IsBankQueryResultInitialized => m_preparedBankQueryResult != null;
        }

        public class GuildBankRightsAndSlots
        {
            public GuildBankRightsAndSlots(byte _tabId = 0xFF, sbyte _rights = 0, int _slots = 0)
            {
                tabId = _tabId;
                rights = (GuildBankRights)_rights;
                slots = _slots;
            }

            public void SetGuildMasterValues()
            {
                rights = GuildBankRights.AllPermanent;
                slots = GuildConst.WithdrawSlotUnlimited;
            }

            public void SetTabId(byte _tabId) { tabId = _tabId; }
            public void SetSlots(int _slots) { slots = _slots; }
            public void SetRights(GuildBankRights _rights) { rights = _rights; }

            public byte GetTabId() { return tabId; }
            public int GetSlots() { return slots; }
            public GuildBankRights GetRights() { return rights; }

            byte tabId;
            GuildBankRights rights;
            int slots;
        }

        public class EmblemInfo
        {
            public EmblemInfo()
            {
                m_style = 0;
                m_color = 0;
                m_borderStyle = 0;
                m_borderColor = 0;
                m_backgroundColor = 0;
            }

            public void ReadPacket(SaveGuildEmblem packet)
            {
                m_style = packet.EStyle;
                m_color = packet.EColor;
                m_borderStyle = packet.BStyle;
                m_borderColor = packet.BColor;
                m_backgroundColor = packet.Bg;
            }

            public bool ValidateEmblemColors()
            {
                return true; //Temporary hardcoded (need Hotfix update)
                //return ValidateEmblemColors(m_style, m_color, m_borderStyle, m_borderColor, m_backgroundColor);
            }

            public static bool ValidateEmblemColors(int style, int color, int borderStyle, int borderColor, int backgroundColor)
            {
                return CliDB.GuildColorBackgroundStorage.ContainsKey(backgroundColor) &&
                       CliDB.GuildColorBorderStorage.ContainsKey(borderColor) &&
                       CliDB.GuildColorEmblemStorage.ContainsKey(color);
            }

            public bool LoadFromDB(SQLFields field)
            {
                m_style = field.Read<byte>(3);
                m_color = field.Read<byte>(4);
                m_borderStyle = field.Read<byte>(5);
                m_borderColor = field.Read<byte>(6);
                m_backgroundColor = field.Read<byte>(7);

                return ValidateEmblemColors();
            }

            public void SaveToDB(long guildId)
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_GUILD_EMBLEM_INFO);
                stmt.SetInt32(0, m_style);
                stmt.SetInt32(1, m_color);
                stmt.SetInt32(2, m_borderStyle);
                stmt.SetInt32(3, m_borderColor);
                stmt.SetInt32(4, m_backgroundColor);
                stmt.SetInt64(5, guildId);
                DB.Characters.Execute(stmt);
            }

            public int GetStyle() { return m_style; }

            public int GetColor() { return m_color; }

            public int GetBorderStyle() { return m_borderStyle; }

            public int GetBorderColor() { return m_borderColor; }

            public int GetBackgroundColor() { return m_backgroundColor; }

            int m_style;
            int m_color;
            int m_borderStyle;
            int m_borderColor;
            int m_backgroundColor;
        }

        public abstract class MoveItemData
        {
            protected MoveItemData(Guild guild, Player player, ItemPos itemPos)
            {
                m_pGuild = guild;
                m_pPlayer = player;
                m_itemPos = itemPos;
                m_pItem = null;
                m_pClonedItem = null;
            }

            public virtual bool CheckItem(ref int splitedAmount)
            {
                Cypher.Assert(m_pItem != null);
                if (splitedAmount > m_pItem.GetCount())
                    return false;
                if (splitedAmount == m_pItem.GetCount())
                    splitedAmount = 0;
                return true;
            }

            public InventoryResult CanStore(Item pItem, bool swap)
            {
                m_vec.Clear();
                
                return _CanStore(pItem, swap);
            }

            public bool CloneItem(int count)
            {
                Cypher.Assert(m_pItem != null);
                m_pClonedItem = m_pItem.CloneItem(count);
                if (m_pClonedItem == null)
                {
                    return false;
                }
                return true;
            }

            public virtual void LogAction(MoveItemData pFrom)
            {
                Cypher.Assert(pFrom.GetItem() != null);

                Global.ScriptMgr.OnGuildItemMove(m_pGuild, m_pPlayer, pFrom.GetItem(),
                     pFrom.IsBank, pFrom.Position,
                     IsBank, Position);
            }

            public void CopyDestinationSlotsTo(List<ItemSlot> myList)
            {
                foreach (var item in m_vec)
                    myList.Add(item.Position.Slot);
            }

            public void SendEquipError(InventoryResult result, Item item)
            {
                m_pPlayer.SendEquipError(result, item);
            }

            public abstract bool IsBank { get; }
            // Initializes item. Returns true, if item exists, false otherwise.
            public abstract bool InitItem();
            // Checks splited amount against item. Splited amount cannot be more that number of items in stack.
            // Defines if player has rights to save item in container (into free slot only)
            public virtual bool HasDepositRights => true;
            // Defines if player has rights to withdraw item from container
            public virtual bool HasWithdrawRights => true;
            // Defines if player has amount to withdraw item from container
            public virtual bool HasWithdrawAmount => true;
            // Defines if player has rights to split/merge/move the item from container
            public virtual bool HasModifyRights => true;
            // Remove item from container (if splited update items fields)
            public abstract void RemoveItem(SQLTransaction trans, MoveItemData pOther, int splitedAmount = 0);
            // Saves item to container
            public abstract Item StoreItem(SQLTransaction trans, Item pItem);
            // Log bank event
            public abstract void LogBankEvent(SQLTransaction trans, MoveItemData pFrom, int count);

            protected abstract InventoryResult _CanStore(Item pItem, bool swap);

            public Item GetItem(bool isCloned = false) { return isCloned ? m_pClonedItem : m_pItem; }
            public ItemSlot Container => m_itemPos.Container; 
            public ItemSlot Slot => m_itemPos.Slot;
            public ItemPos Position => m_itemPos;
            public WorldSession Session => m_pPlayer.GetSession();

            protected Guild m_pGuild;
            protected Player m_pPlayer;
            protected ItemPos m_itemPos;
            protected Item m_pItem;
            protected Item m_pClonedItem;
            protected List<ItemPosCount> m_vec = new();
        }

        public class PlayerMoveItemData : MoveItemData
        {
            public PlayerMoveItemData(Guild guild, Player player, ItemPos itemPos)
                : base(guild, player, itemPos) { }

            public override bool IsBank => false;

            public override bool InitItem()
            {
                m_pItem = m_pPlayer.GetItemByPos(Position);
                if (m_pItem != null)
                {
                    // Anti-WPE protection. Do not move non-empty bags to bank.
                    if (m_pItem.IsNotEmptyBag())
                    {
                        SendEquipError(InventoryResult.DestroyNonemptyBag, m_pItem);
                        m_pItem = null;
                    }
                    // Bound items cannot be put into bank.
                    else if (!m_pItem.CanBeTraded())
                    {
                        SendEquipError(InventoryResult.CantSwap, m_pItem);
                        m_pItem = null;
                    }
                }
                return (m_pItem != null);
            }

            public override void RemoveItem(SQLTransaction trans, MoveItemData pOther, int splitedAmount = 0)
            {
                if (splitedAmount != 0)
                {
                    m_pItem.SetCount(m_pItem.GetCount() - splitedAmount);
                    m_pItem.SetState(ItemUpdateState.Changed, m_pPlayer);
                    m_pPlayer.SaveInventoryAndGoldToDB(trans);
                }
                else
                {
                    m_pPlayer.MoveItemFromInventory(Position, true);
                    m_pItem.DeleteFromInventoryDB(trans);
                    m_pItem = null;
                }
            }

            public override Item StoreItem(SQLTransaction trans, Item pItem)
            {
                Cypher.Assert(pItem != null);
                m_pPlayer.MoveItemToInventory(m_vec, pItem, true);
                m_pPlayer.SaveInventoryAndGoldToDB(trans);
                return pItem;
            }

            public override void LogBankEvent(SQLTransaction trans, MoveItemData pFrom, int count)
            {
                Cypher.Assert(pFrom != null);
                // Bank . Char
                m_pGuild._LogBankEvent(trans, GuildBankEventLogTypes.WithdrawItem, pFrom.Container, m_pPlayer.GetGUID().GetCounter(),
                    pFrom.GetItem().GetEntry(), (ushort)count);
            }

            protected override InventoryResult _CanStore(Item pItem, bool swap)
            {
                return m_pPlayer.CanStoreItem(Position, out m_vec, pItem, forSwap: swap);
            }
        }

        public class BankMoveItemData : MoveItemData
        {
            public BankMoveItemData(Guild guild, Player player, ItemPos itemPos)
                : base(guild, player, itemPos) { }

            public override bool IsBank => true;

            public override bool InitItem()
            {
                m_pItem = m_pGuild._GetItem(Position);
                return (m_pItem != null);
            }

            public override bool HasDepositRights
            {
                get => m_pGuild._MemberHasTabRights(m_pPlayer.GetGUID(), Container, GuildBankRights.DepositItem);
            }

            public override bool HasWithdrawRights
            {
                get
                {
                    int slots = 0;
                    Member member = m_pGuild.GetMember(m_pPlayer.GetGUID());
                    if (member != null)
                    {
                        GuildRankId rankId = member.GetRankId();
                        slots = m_pGuild._GetRankBankTabSlotsPerDay(rankId, Container);
                    }

                    return slots != 0;
                }
            }

            public override bool HasModifyRights
            {
                get => m_pGuild._MemberHasTabRights(m_pPlayer.GetGUID(), Container, GuildBankRights.ModifyItemOrder) &&
                    (HasDepositRights || HasWithdrawRights); // WOTLK_CLASSIC client requirements
            }

            public override bool HasWithdrawAmount
            {
                get
                {
                    int slots = 0;
                    Member member = m_pGuild.GetMember(m_pPlayer.GetGUID());
                    if (member != null)
                        slots = m_pGuild._GetMemberRemainingSlots(member, Container);

                    return slots != 0;
                }
            }

            public override void RemoveItem(SQLTransaction trans, MoveItemData pOther, int splitedAmount = 0)
            {
                Cypher.Assert(m_pItem != null);
                if (splitedAmount != 0)
                {
                    m_pItem.SetCount(m_pItem.GetCount() - splitedAmount);
                    m_pItem.FSetState(ItemUpdateState.Changed);
                    m_pItem.SaveToDB(trans);
                }
                else
                {
                    m_pGuild._RemoveItem(trans, Position);
                    m_pItem = null;
                }
                // Decrease amount of player's remaining items (if item is moved to different tab or to player)
                if (!pOther.IsBank || pOther.Container != Container)
                    m_pGuild._UpdateMemberWithdrawSlots(trans, m_pPlayer.GetGUID(), Container);
            }

            public override Item StoreItem(SQLTransaction trans, Item pItem)
            {
                if (pItem == null)
                    return null;

                BankTab pTab = m_pGuild.GetBankTab(Container);
                if (pTab == null)
                    return null;

                Item pLastItem = pItem;
                foreach (var pos in m_vec)
                {
                    Log.outDebug(LogFilter.Guild, 
                        $"GUILD STORAGE: StoreItem tab = {Container}, slot = {Slot}, " +
                        $"item = {pItem.GetEntry()}, count = {pItem.GetCount()}");

                    pLastItem = _StoreItem(trans, pTab, pItem, pos, pos != m_vec.Last());
                }
                return pLastItem;
            }

            public override void LogBankEvent(SQLTransaction trans, MoveItemData pFrom, int count)
            {
                Cypher.Assert(pFrom.GetItem() != null);
                if (pFrom.IsBank)
                    // Bank . Bank
                    m_pGuild._LogBankEvent(trans, GuildBankEventLogTypes.MoveItem, pFrom.Container, m_pPlayer.GetGUID().GetCounter(),
                        pFrom.GetItem().GetEntry(), (ushort)count, Container);
                else
                    // Char . Bank
                    m_pGuild._LogBankEvent(trans, GuildBankEventLogTypes.DepositItem, Container, m_pPlayer.GetGUID().GetCounter(),
                        pFrom.GetItem().GetEntry(), (ushort)count);
            }

            public override void LogAction(MoveItemData pFrom)
            {
                base.LogAction(pFrom);
                if (!pFrom.IsBank && m_pPlayer.GetSession().HasPermission(RBACPermissions.LogGmTrade)) // @todo Move this to scripts
                {
                    Log.outCommand(m_pPlayer.GetSession().GetAccountId(), 
                        $"GM {m_pPlayer.GetName()} ({m_pPlayer.GetGUID()}) (Account: {m_pPlayer.GetSession().GetAccountId()}) " +
                        $"deposit item: {pFrom.GetItem().GetTemplate().GetName()} (Entry: {pFrom.GetItem().GetEntry()} " +
                        $"Count: {pFrom.GetItem().GetCount()}) " +
                        $"to guild bank named: {m_pGuild.GetName()} (Guild ID: {m_pGuild.GetId()})");
                }
            }

            Item _StoreItem(SQLTransaction trans, BankTab pTab, Item pItem, ItemPosCount pos, bool clone)
            {
                byte slotId = pos.Position.Slot;
                int count = pos.Count;
                Item pItemDest = pTab.GetItem(slotId);
                if (pItemDest != null)
                {
                    pItemDest.SetCount(pItemDest.GetCount() + count);
                    pItemDest.FSetState(ItemUpdateState.Changed);
                    pItemDest.SaveToDB(trans);

                    if (!clone)
                    {
                        pItem.RemoveFromWorld();
                        pItem.DeleteFromDB(trans);
                    }

                    return pItemDest;
                }

                if (clone)
                    pItem = pItem.CloneItem(count);
                else
                    pItem.SetCount(count);

                if (pItem != null && pTab.SetItem(trans, slotId, pItem))
                    return pItem;

                return null;
            }

            bool _ReserveSpace(byte slotId, Item pItem, Item pItemDest, ref int count)
            {
                int requiredSpace = pItem.GetMaxStackCount();
                if (pItemDest != null)
                {
                    // Make sure source and destination items match and destination item has space for more stacks.
                    if (pItemDest.GetEntry() != pItem.GetEntry() || pItemDest.GetCount() >= pItem.GetMaxStackCount())
                        return false;
                    requiredSpace -= pItemDest.GetCount();
                }
                // Let's not be greedy, reserve only required space
                requiredSpace = Math.Min(requiredSpace, count);

                // Reserve space
                ItemPosCount pos = new(slotId, requiredSpace);
                if (!m_vec.Contains(pos))
                {
                    m_vec.Add(pos);
                    count -= requiredSpace;
                }
                return true;
            }

            void CanStoreItemInTab(Item pItem, byte skipSlotId, bool merge, ref int count)
            {
                for (byte slotId = 0; (slotId < GuildConst.MaxBankSlots) && (count > 0); ++slotId)
                {
                    // Skip slot already processed in CanStore (when destination slot was specified)
                    if (slotId == skipSlotId)
                        continue;

                    Item pItemDest = m_pGuild._GetItem(Position);
                    if (pItemDest == pItem)
                        pItemDest = null;

                    // If merge skip empty, if not merge skip non-empty
                    if ((pItemDest != null) != merge)
                        continue;

                    _ReserveSpace(slotId, pItem, pItemDest, ref count);
                }
            }

            protected override InventoryResult _CanStore(Item pItem, bool swap)
            {
                Log.outDebug(LogFilter.Guild, 
                    $"GUILD STORAGE: CanStore() tab = {Container}, slot = {Slot}, " +
                    $"item = {pItem.GetEntry()}, count = {pItem.GetCount()}");

                int count = pItem.GetCount();
                // Soulbound items cannot be moved
                if (pItem.IsSoulBound())
                    return InventoryResult.DropBoundItem;

                // Make sure destination bank tab exists
                if (Container >= m_pGuild._GetPurchasedTabsSize())
                    return InventoryResult.WrongBagType;

                // Slot explicitely specified. Check it.
                if (Slot != ItemSlot.Null)
                {
                    Item pItemDest = m_pGuild._GetItem(Position);
                    // Ignore swapped item (this slot will be empty after move)
                    if ((pItemDest == pItem) || swap)
                        pItemDest = null;                    

                    if (!_ReserveSpace(Slot, pItem, pItemDest, ref count))
                        return InventoryResult.CantStack;

                    // Just always store item in a free slot, as the count of items in a slot
                    // can be predetermined by the person in charge.
                    if (pItemDest != null && !HasModifyRights)
                        return InventoryResult.ItemLocked;

                    if (count == 0)
                        return InventoryResult.Ok;
                }

                // Slot was not specified or it has not enough space for all the items in stack
                // Search for stacks to merge with
                if (pItem.GetMaxStackCount() > 1)
                {
                    CanStoreItemInTab(pItem, Slot, true, ref count);
                    if (count == 0)
                        return InventoryResult.Ok;
                }

                // Search free slot for item
                CanStoreItemInTab(pItem, Slot, false, ref count);
                if (count == 0)
                    return InventoryResult.Ok;

                return InventoryResult.BankFull;
            }
        }
        #endregion
    }
}
