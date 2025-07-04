﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.BattleGrounds.Zones;
using Game.BattleGrounds.Zones.AlteracValley;
using Game.BattleGrounds.Zones.ArathisBasin;
using Game.BattleGrounds.Zones.EyeofStorm;
using Game.BattleGrounds.Zones.WarsongGluch;
using Game.DataStorage;
using Game.Entities;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.BattleGrounds
{
    public class BattlegroundManager : Singleton<BattlegroundManager>
    {
        BattlegroundManager()
        {
            m_NextRatedArenaUpdate = WorldConfig.Values[WorldCfg.ArenaRatedUpdateTimer].TimeSpan;
        }

        public void DeleteAllBattlegrounds()
        {
            foreach (var data in bgDataStore.Values.ToList())
                while (!data.m_Battlegrounds.Empty())
                    data.m_Battlegrounds.First().Value.Dispose();

            bgDataStore.Clear();

            foreach (var bg in m_BGFreeSlotQueue.Values.ToList())
                bg.Dispose();

            m_BGFreeSlotQueue.Clear();
        }

        public void Update(TimeSpan diff)
        {
            m_UpdateTimer += diff;
            if (m_UpdateTimer > (Seconds)1)
            {
                foreach (var data in bgDataStore.Values)
                {
                    var bgs = data.m_Battlegrounds;

                    // first one is template and should not be deleted
                    foreach (var pair in bgs.ToList())
                    {
                        Battleground bg = pair.Value;
                        bg.Update(m_UpdateTimer);

                        if (bg.ToBeDeleted())
                        {
                            bgs.Remove(pair.Key);
                            var clients = data.m_ClientBattlegroundIds[(int)bg.GetBracketId()];
                            if (!clients.Empty())
                                clients.Remove(bg.GetClientInstanceID());

                            bg.Dispose();
                        }
                    }
                }

                m_UpdateTimer = TimeSpan.Zero;
            }
            // update events timer
            foreach (var pair in m_BattlegroundQueues)
                pair.Value.UpdateEvents(diff);

            // update scheduled queues
            if (!m_QueueUpdateScheduler.Empty())
            {
                List<ScheduledQueueUpdate> scheduled = new();
                Extensions.Swap(ref scheduled, ref m_QueueUpdateScheduler);

                for (byte i = 0; i < scheduled.Count; i++)
                {
                    int arenaMMRating = scheduled[i].ArenaMatchmakerRating;
                    BattlegroundQueueTypeId bgQueueTypeId = scheduled[i].QueueId;
                    BattlegroundBracketId bracket_id = scheduled[i].BracketId;
                    GetBattlegroundQueue(bgQueueTypeId).BattlegroundQueueUpdate(diff, bracket_id, arenaMMRating);
                }
            }

            // if rating difference counts, maybe force-update queues
            if (WorldConfig.Values[WorldCfg.ArenaMaxRatingDifference].Int32 != 0 
                && WorldConfig.Values[WorldCfg.ArenaRatedUpdateTimer].TimeSpan != TimeSpan.Zero)
            {
                // it's time to force update
                if (m_NextRatedArenaUpdate < diff)
                {
                    // forced update for rated arenas (scan all, but skipped non rated)
                    Log.outDebug(LogFilter.Arena, "BattlegroundMgr: UPDATING ARENA QUEUES");
                    foreach (ArenaTypes teamSize in new[] { ArenaTypes.Team2v2, ArenaTypes.Team3v3, ArenaTypes.Team5v5 })
                    {
                        BattlegroundQueueTypeId ratedArenaQueueId = BGQueueTypeId(BattlegroundTypeId.AA, BattlegroundQueueIdType.Arena, true, teamSize);
                        for (var bracket = BattlegroundBracketId.First; bracket < BattlegroundBracketId.Max; ++bracket)
                            GetBattlegroundQueue(ratedArenaQueueId).BattlegroundQueueUpdate(diff, bracket, 0);
                    }

                    m_NextRatedArenaUpdate = WorldConfig.Values[WorldCfg.ArenaRatedUpdateTimer].TimeSpan;
                }
                else
                    m_NextRatedArenaUpdate -= diff;
            }
        }

        void BuildBattlegroundStatusHeader(BattlefieldStatusHeader header, Player player, int ticketId, ServerTime joinTime, BattlegroundQueueTypeId queueId)
        {
            header.Ticket = new RideTicket();
            header.Ticket.RequesterGuid = player.GetGUID();
            header.Ticket.Id = ticketId;
            header.Ticket.Type = RideType.Battlegrounds;
            header.Ticket.JoinTime = joinTime;
            header.QueueID.Add(queueId.GetPacked());
            header.RangeMin = 0; // seems to always be 0
            header.RangeMax = SharedConst.DefaultMaxPlayerLevel; // alwyas max level of current expansion. Might be limited to account
            header.TeamSize = queueId.TeamSize;
            header.InstanceID = 0; // seems to always be 0
            header.RegisteredMatch = queueId.Rated;
            header.TournamentRules = false;
        }

        public void BuildBattlegroundStatusNone(out BattlefieldStatusNone battlefieldStatus, Player player, int ticketId, ServerTime joinTime)
        {
            battlefieldStatus = new BattlefieldStatusNone();
            battlefieldStatus.Ticket.RequesterGuid = player.GetGUID();
            battlefieldStatus.Ticket.Id = ticketId;
            battlefieldStatus.Ticket.Type = RideType.Battlegrounds;
            battlefieldStatus.Ticket.JoinTime = joinTime;
        }

        public void BuildBattlegroundStatusNeedConfirmation(out BattlefieldStatusNeedConfirmation battlefieldStatus, Battleground bg, Player player, int ticketId, ServerTime joinTime, TimeSpan timeout, BattlegroundQueueTypeId queueId)
        {
            battlefieldStatus = new BattlefieldStatusNeedConfirmation();
            BuildBattlegroundStatusHeader(battlefieldStatus.Hdr, player, ticketId, joinTime, queueId);
            battlefieldStatus.Mapid = bg.GetMapId();
            battlefieldStatus.Timeout = timeout;
            battlefieldStatus.Role = 0;
        }

        public void BuildBattlegroundStatusActive(out BattlefieldStatusActive battlefieldStatus, Battleground bg, Player player, int ticketId, ServerTime joinTime, BattlegroundQueueTypeId queueId)
        {
            battlefieldStatus = new BattlefieldStatusActive();
            BuildBattlegroundStatusHeader(battlefieldStatus.Hdr, player, ticketId, joinTime, queueId);
            battlefieldStatus.ShutdownTimer = bg.GetRemainingTime();
            battlefieldStatus.ArenaFaction = (byte)(player.GetBGTeam() == Team.Horde ? BattleGroundTeamId.Horde : BattleGroundTeamId.Alliance);
            battlefieldStatus.LeftEarly = false;
            battlefieldStatus.StartTimer = bg.GetElapsedTime();
            battlefieldStatus.Mapid = bg.GetMapId();
        }

        public void BuildBattlegroundStatusQueued(out BattlefieldStatusQueued battlefieldStatus, Player player, int ticketId, ServerTime joinTime, BattlegroundQueueTypeId queueId, TimeSpan avgWaitTime, bool asGroup)
        {
            battlefieldStatus = new BattlefieldStatusQueued();
            BuildBattlegroundStatusHeader(battlefieldStatus.Hdr, player, ticketId, joinTime, queueId);
            battlefieldStatus.AverageWaitTime = avgWaitTime;
            battlefieldStatus.AsGroup = asGroup;
            battlefieldStatus.SuspendedQueue = false;
            battlefieldStatus.EligibleForMatchmaking = true;
            battlefieldStatus.WaitTime = Time.Diff(joinTime);
        }

        public void BuildBattlegroundStatusFailed(out BattlefieldStatusFailed battlefieldStatus, BattlegroundQueueTypeId queueId, Player pPlayer, int ticketId, GroupJoinBattlegroundResult result, ObjectGuid errorGuid = default)
        {
            battlefieldStatus = new BattlefieldStatusFailed();
            battlefieldStatus.Ticket.RequesterGuid = pPlayer.GetGUID();
            battlefieldStatus.Ticket.Id = ticketId;
            battlefieldStatus.Ticket.Type = RideType.Battlegrounds;
            battlefieldStatus.Ticket.JoinTime = pPlayer.GetBattlegroundQueueJoinTime(queueId);
            battlefieldStatus.QueueID = queueId.GetPacked();
            battlefieldStatus.Reason = result;
            if (!errorGuid.IsEmpty() && (result == GroupJoinBattlegroundResult.NotInBattleground || result == GroupJoinBattlegroundResult.JoinTimedOut))
                battlefieldStatus.ClientID = errorGuid;
        }

        public Battleground GetBattleground(int instanceId, BattlegroundTypeId bgTypeId)
        {
            if (instanceId == 0)
                return null;

            if (bgTypeId != BattlegroundTypeId.None || IsRandomBattleground(bgTypeId))
            {
                var data = bgDataStore.LookupByKey(bgTypeId);
                return data.m_Battlegrounds.LookupByKey(instanceId);
            }

            foreach (var it in bgDataStore)
            {
                var bgs = it.Value.m_Battlegrounds;
                var bg = bgs.LookupByKey(instanceId);
                if (bg != null)
                    return bg;
            }

            return null;
        }

        int CreateClientVisibleInstanceId(BattlegroundTypeId bgTypeId, BattlegroundBracketId bracket_id)
        {
            if (IsArenaType(bgTypeId))
                return 0;                                           //arenas don't have client-instanceids

            // we create here an instanceid, which is just for
            // displaying this to the client and without any other use..
            // the client-instanceIds are unique for each Battleground-Type
            // the instance-id just needs to be as low as possible, beginning with 1
            // the following works, because std.set is default ordered with "<"
            // the optimalization would be to use as bitmask std.vector<uint32> - but that would only make code unreadable

            var clientIds = bgDataStore[bgTypeId].m_ClientBattlegroundIds[(int)bracket_id];
            int lastId = 0;
            foreach (var id in clientIds)
            {
                if (++lastId != id)                             //if there is a gap between the ids, we will break..
                    break;
                lastId = id;
            }

            clientIds.Add(++lastId);
            return lastId;
        }

        // create a new Battleground that will really be used to play
        public Battleground CreateNewBattleground(BattlegroundQueueTypeId queueId, BattlegroundBracketId bracketId)
        {
            BattlegroundTypeId bgTypeId = GetRandomBG(queueId.BattlemasterListId);

            // get the template BG
            BattlegroundTemplate bg_template = GetBattlegroundTemplateByTypeId(bgTypeId);
            if (bg_template == null)
            {
                Log.outError(LogFilter.Battleground, 
                    $"Battleground: CreateNewBattleground - bg template not found for {bgTypeId}");
                return null;
            }

            if (bgTypeId == BattlegroundTypeId.RB || bgTypeId == BattlegroundTypeId.AA || bgTypeId == BattlegroundTypeId.RandomEpic)
                return null;

            PvpDifficultyRecord bracketEntry = Global.DB2Mgr.GetBattlegroundBracketById(bg_template.BattlemasterEntry.MapId[0], bracketId);
            if (bracketEntry == null)
            {
                Log.outError(LogFilter.Battleground, 
                    $"Battleground: CreateNewBattleground: bg bracket entry not found " +
                    $"for map {bg_template.BattlemasterEntry.MapId[0]} bracket id {bracketId}");
                return null;
            }

            Battleground bg = null;
            // create a copy of the BG template
            switch (bgTypeId)
            {
                case BattlegroundTypeId.AV:
                    bg = new BgAlteracValley(bg_template);
                    break;
                case BattlegroundTypeId.WS:
                case BattlegroundTypeId.WgCtf:
                    bg = new BgWarsongGluch(bg_template);
                    break;
                case BattlegroundTypeId.AB:
                case BattlegroundTypeId.DomAb:
                    bg = new BgArathiBasin(bg_template);
                    break;
                case BattlegroundTypeId.NA:
                    bg = new BgNagrandArena(bg_template);
                    break;
                case BattlegroundTypeId.BE:
                    bg = new BgBladesEdgeArena(bg_template);
                    break;
                case BattlegroundTypeId.EY:
                    bg = new BgEyeofStorm(bg_template);
                    break;
                case BattlegroundTypeId.RL:
                    bg = new BgRuinsOfLordaernon(bg_template);
                    break;
                case BattlegroundTypeId.SA:
                    bg = new BgStrandOfAncients(bg_template);
                    break;
                case BattlegroundTypeId.DS:
                    bg = new BgDalaranSewers(bg_template);
                    break;
                case BattlegroundTypeId.RV:
                    bg = new BgTheRingOfValor(bg_template);
                    break;
                case BattlegroundTypeId.IC:
                    bg = new BgIsleofConquest(bg_template);
                    break;
                case BattlegroundTypeId.TP:
                    bg = new BgTwinPeaks(bg_template);
                    break;
                case BattlegroundTypeId.BFG:
                    bg = new BgBattleforGilneas(bg_template);
                    break;
                case BattlegroundTypeId.RB:
                case BattlegroundTypeId.AA:
                case BattlegroundTypeId.RandomEpic:
                default:
                    return null;
            }

            bg.SetBracket(bracketEntry);
            bg.SetInstanceID(Global.MapMgr.GenerateInstanceId());
            bg.SetClientInstanceID(CreateClientVisibleInstanceId(queueId.BattlemasterListId, bracketEntry.BracketId));
            // reset the new bg (set status to status_wait_queue from status_none)
            // this shouldn't be needed anymore as a new Battleground instance is created each time. But some bg sub classes still depend on it.
            bg.Reset();
            bg.SetStatus(BattlegroundStatus.WaitJoin); // start the joining of the bg
            bg.SetArenaType((ArenaTypes)queueId.TeamSize);
            bg.SetRated(queueId.Rated);

            return bg;
        }

        public void LoadBattlegroundTemplates()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            //                                         0   1                 2              3             4       5
            SQLResult result = DB.World.Query("SELECT ID, AllianceStartLoc, HordeStartLoc, StartMaxDist, Weight, ScriptName FROM battleground_template");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 Battlegrounds. DB table `Battleground_template` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                var bgTypeId = (BattlegroundTypeId)result.Read<int>(0);
                if (Global.DisableMgr.IsDisabledFor(DisableType.Battleground, (int)bgTypeId, null))
                    continue;

                // can be overwrite by values from DB
                BattlemasterListRecord bl = CliDB.BattlemasterListStorage.LookupByKey((int)bgTypeId);
                if (bl == null)
                {
                    Log.outError(LogFilter.Battleground, 
                        $"Battleground ID {bgTypeId} not found in BattlemasterList.dbc. " +
                        $"Battleground not created.");
                    continue;
                }

                BattlegroundTemplate bgTemplate = new();
                bgTemplate.Id = bgTypeId;
                float dist = result.Read<float>(3);
                bgTemplate.MaxStartDistSq = dist * dist;
                bgTemplate.Weight = result.Read<byte>(4);

                bgTemplate.ScriptId = Global.ObjectMgr.GetScriptId(result.Read<string>(5));
                bgTemplate.BattlemasterEntry = bl;

                if (bgTemplate.Id != BattlegroundTypeId.AA && !IsRandomBattleground(bgTemplate.Id))
                {
                    int startId = result.Read<int>(1);
                    WorldSafeLocsEntry start = Global.ObjectMgr.GetWorldSafeLoc(startId);
                    if (start != null)
                        bgTemplate.StartLocation[BattleGroundTeamId.Alliance] = start;
                    else if (bgTemplate.StartLocation[BattleGroundTeamId.Alliance] != null) // reload case
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `battleground_template` for id {bgTemplate.Id} " +
                            $"contains a non-existing WorldSafeLocs.dbc id {startId} in field `AllianceStartLoc`. Ignoring.");
                    }
                    else
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `Battleground_template` for Id {bgTemplate.Id} " +
                            $"has a non-existed WorldSafeLocs.dbc id {startId} in field `AllianceStartLoc`. BG not created.");
                        continue;
                    }

                    startId = result.Read<int>(2);
                    start = Global.ObjectMgr.GetWorldSafeLoc(startId);
                    if (start != null)
                        bgTemplate.StartLocation[BattleGroundTeamId.Horde] = start;
                    else if (bgTemplate.StartLocation[BattleGroundTeamId.Horde] != null) // reload case
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `battleground_template` for id {bgTemplate.Id} " +
                            $"contains a non-existing WorldSafeLocs.dbc id {startId} in field `HordeStartLoc`. Ignoring.");
                    }
                    else
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `Battleground_template` for Id {bgTemplate.Id} " +
                            $"has a non-existed WorldSafeLocs.dbc id {startId} in field `HordeStartLoc`. BG not created.");
                        continue;
                    }
                }

                _battlegroundTemplates[bgTypeId] = bgTemplate;

                if (bgTemplate.BattlemasterEntry.MapId[1] == -1) // in this case we have only one mapId
                    _battlegroundMapTemplates[bgTemplate.BattlemasterEntry.MapId[0]] = _battlegroundTemplates[bgTypeId];

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Battlegrounds in {Time.Diff(oldMSTime)} ms.");
        }

        public void SendBattlegroundList(Player player, ObjectGuid guid, BattlegroundTypeId bgTypeId)
        {
            BattlegroundTemplate bgTemplate = GetBattlegroundTemplateByTypeId(bgTypeId);
            if (bgTemplate == null)
                return;

            BattlefieldList battlefieldList = new();
            battlefieldList.BattlemasterGuid = guid;
            battlefieldList.BattlemasterListID = (int)bgTypeId;
            battlefieldList.MinLevel = bgTemplate.GetMinLevel();
            battlefieldList.MaxLevel = bgTemplate.GetMaxLevel();
            battlefieldList.PvpAnywhere = guid.IsEmpty();
            battlefieldList.HasRandomWinToday = player.GetRandomWinner();
            player.SendPacket(battlefieldList);
        }

        public void SendToBattleground(Player player, int instanceId, BattlegroundTypeId bgTypeId)
        {
            Battleground bg = GetBattleground(instanceId, bgTypeId);
            if (bg != null)
            {
                var mapid = bg.GetMapId();
                Team team = player.GetBGTeam();

                WorldSafeLocsEntry pos = bg.GetTeamStartPosition(Battleground.GetTeamIndexByTeamId(team));
                Log.outDebug(LogFilter.Battleground,
                    $"BattlegroundMgr.SendToBattleground: " +
                    $"Sending {player.GetName()} to map {mapid}, {pos.Loc} (bgType {bgTypeId})");
                player.TeleportTo(pos.Loc);
            }
            else
            {
                Log.outError(LogFilter.Battleground, 
                    $"BattlegroundMgr.SendToBattleground: Instance {instanceId} (bgType {bgTypeId}) " +
                    $"not found while trying to teleport player {player.GetName()}");
            }
        }

        bool IsArenaType(BattlegroundTypeId bgTypeId)
        {
            return bgTypeId == BattlegroundTypeId.AA || bgTypeId == BattlegroundTypeId.BE || bgTypeId == BattlegroundTypeId.NA
                || bgTypeId == BattlegroundTypeId.DS || bgTypeId == BattlegroundTypeId.RV || bgTypeId == BattlegroundTypeId.RL;
        }

        public bool IsRandomBattleground(BattlegroundTypeId battlemasterListId)
        {
            return battlemasterListId == BattlegroundTypeId.RB || battlemasterListId == BattlegroundTypeId.RandomEpic;
        }

        public BattlegroundQueueTypeId BGQueueTypeId(BattlegroundTypeId battlemasterListId, BattlegroundQueueIdType type, bool rated, ArenaTypes teamSize)
        {
            return new BattlegroundQueueTypeId(battlemasterListId, (byte)type, rated, (byte)teamSize);
        }

        public void ToggleTesting()
        {
            m_Testing = !m_Testing;
            Global.WorldMgr.SendWorldText(m_Testing ? CypherStrings.DebugBgOn : CypherStrings.DebugBgOff);
        }

        public void ToggleArenaTesting()
        {
            m_ArenaTesting = !m_ArenaTesting;
            Global.WorldMgr.SendWorldText(m_ArenaTesting ? CypherStrings.DebugArenaOn : CypherStrings.DebugArenaOff);
        }

        public bool IsValidQueueId(BattlegroundQueueTypeId bgQueueTypeId)
        {
            BattlemasterListRecord battlemasterList = CliDB.BattlemasterListStorage.LookupByKey((int)bgQueueTypeId.BattlemasterListId);
            if (battlemasterList == null)
                return false;

            switch ((BattlegroundQueueIdType)bgQueueTypeId.BgType)
            {
                case BattlegroundQueueIdType.Battleground:
                    if (battlemasterList.InstanceType != (int)MapTypes.Battleground)
                        return false;
                    if (bgQueueTypeId.TeamSize != 0)
                        return false;
                    break;
                case BattlegroundQueueIdType.Arena:
                    if (battlemasterList.InstanceType != (int)MapTypes.Arena)
                        return false;
                    if (!bgQueueTypeId.Rated)
                        return false;
                    if (bgQueueTypeId.TeamSize == 0)
                        return false;
                    break;
                case BattlegroundQueueIdType.Wargame:
                    if (bgQueueTypeId.Rated)
                        return false;
                    break;
                case BattlegroundQueueIdType.ArenaSkirmish:
                    if (battlemasterList.InstanceType != (int)MapTypes.Arena)
                        return false;
                    if (!bgQueueTypeId.Rated)
                        return false;
                    if (bgQueueTypeId.TeamSize != (int)ArenaTypes.Team3v3)
                        return false;
                    break;
                default:
                    return false;
            }

            return true;
        }

        public void ScheduleQueueUpdate(int arenaMatchmakerRating, BattlegroundQueueTypeId bgQueueTypeId, BattlegroundBracketId bracket_id)
        {
            //we will use only 1 number created of bgTypeId and bracket_id
            ScheduledQueueUpdate scheduleId = new(arenaMatchmakerRating, bgQueueTypeId, bracket_id);
            if (!m_QueueUpdateScheduler.Contains(scheduleId))
                m_QueueUpdateScheduler.Add(scheduleId);
        }

        public int GetMaxRatingDifference()
        {
            int diff = WorldConfig.Values[WorldCfg.ArenaMaxRatingDifference].Int32;
            if (diff <= 0)
                diff = 5000;
            return diff;
        }

        public TimeSpan GetRatingDiscardTimer()
        {
            return WorldConfig.Values[WorldCfg.ArenaRatingDiscardTimer].TimeSpan;
        }

        public TimeSpan GetPrematureFinishTime()
        {
            return WorldConfig.Values[WorldCfg.BattlegroundPrematureFinishTimer].TimeSpan;
        }

        public void LoadBattleMastersEntry()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mBattleMastersMap.Clear();                                  // need for reload case

            SQLResult result = DB.World.Query("SELECT entry, bg_template FROM battlemaster_entry");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 battlemaster entries. DB table `battlemaster_entry` is empty!");
                return;
            }

            uint count = 0;

            do
            {
                int entry = result.Read<int>(0);
                CreatureTemplate cInfo = Global.ObjectMgr.GetCreatureTemplate(entry);
                if (cInfo != null)
                {
                    if (!cInfo.Npcflag.HasAnyFlag(NPCFlags1.BattleMaster))
                    {
                        Log.outError(LogFilter.Sql,
                            $"Creature (Entry: {entry}) listed in `battlemaster_entry` " +
                            $"is not a battlemaster.");
                    }
                }
                else
                {
                    Log.outError(LogFilter.Sql, 
                        $"Creature (Entry: {entry}) listed in `battlemaster_entry` " +
                        $"does not exist.");
                    continue;
                }

                int bgTypeId = result.Read<int>(1);
                if (!CliDB.BattlemasterListStorage.ContainsKey(bgTypeId))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table `battlemaster_entry` contain entry {entry} " +
                        $"for not existed Battleground Type {bgTypeId}, ignored.");
                    continue;
                }

                ++count;
                mBattleMastersMap[entry] = (BattlegroundTypeId)bgTypeId;
            }
            while (result.NextRow());

            CheckBattleMasters();

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} battlemaster entries in {Time.Diff(oldMSTime)} ms.");
        }

        void CheckBattleMasters()
        {
            var templates = Global.ObjectMgr.GetCreatureTemplates();
            foreach (var creature in templates)
            {
                if (creature.Value.Npcflag.HasAnyFlag(NPCFlags1.BattleMaster) && !mBattleMastersMap.ContainsKey(creature.Value.Entry))
                {
                    Log.outError(LogFilter.Sql, 
                        $"CreatureTemplate (Entry: {creature.Value.Entry}) has UNIT_NPC_FLAG_BATTLEMASTER " +
                        $"but no data in `battlemaster_entry` table. Removing flag!");
                    templates[creature.Key].Npcflag &= ~NPCFlags1.BattleMaster;
                }
            }
        }

        HolidayIds BGTypeToWeekendHolidayId(BattlegroundTypeId bgTypeId)
        {
            switch (bgTypeId)
            {
                case BattlegroundTypeId.AV:
                    return HolidayIds.CallToArmsAv;
                case BattlegroundTypeId.EY:
                    return HolidayIds.CallToArmsEs;
                case BattlegroundTypeId.WS:
                    return HolidayIds.CallToArmsWg;
                case BattlegroundTypeId.SA:
                    return HolidayIds.CallToArmsSa;
                case BattlegroundTypeId.AB:
                    return HolidayIds.CallToArmsAb;
                case BattlegroundTypeId.IC:
                    return HolidayIds.CallToArmsIc;
                case BattlegroundTypeId.TP:
                    return HolidayIds.CallToArmsTp;
                case BattlegroundTypeId.BFG:
                    return HolidayIds.CallToArmsBg;
                default:
                    return HolidayIds.None;
            }
        }

        public BattlegroundTypeId WeekendHolidayIdToBGType(HolidayIds holiday)
        {
            switch (holiday)
            {
                case HolidayIds.CallToArmsAv:
                    return BattlegroundTypeId.AV;
                case HolidayIds.CallToArmsEs:
                    return BattlegroundTypeId.EY;
                case HolidayIds.CallToArmsWg:
                    return BattlegroundTypeId.WS;
                case HolidayIds.CallToArmsSa:
                    return BattlegroundTypeId.SA;
                case HolidayIds.CallToArmsAb:
                    return BattlegroundTypeId.AB;
                case HolidayIds.CallToArmsIc:
                    return BattlegroundTypeId.IC;
                case HolidayIds.CallToArmsTp:
                    return BattlegroundTypeId.TP;
                case HolidayIds.CallToArmsBg:
                    return BattlegroundTypeId.BFG;
                default:
                    return BattlegroundTypeId.None;
            }
        }

        public bool IsBGWeekend(BattlegroundTypeId bgTypeId)
        {
            return Global.GameEventMgr.IsHolidayActive(BGTypeToWeekendHolidayId(bgTypeId));
        }

        BattlegroundTypeId GetRandomBG(BattlegroundTypeId bgTypeId)
        {
            BattlegroundTemplate bgTemplate = GetBattlegroundTemplateByTypeId(bgTypeId);
            if (bgTemplate != null)
            {
                Dictionary<BattlegroundTypeId, float> selectionWeights = new();

                foreach (var mapId in bgTemplate.BattlemasterEntry.MapId)
                {
                    if (mapId == -1)
                        break;

                    BattlegroundTemplate bg = GetBattlegroundTemplateByMapId(mapId);
                    if (bg != null)
                    {
                        selectionWeights.Add(bg.Id, bg.Weight);
                    }
                }

                return selectionWeights.SelectRandomElementByWeight(i => i.Value).Key;
            }

            return BattlegroundTypeId.None;
        }

        public IReadOnlyList<Battleground> GetBGFreeSlotQueueStore(int mapId)
        {
            return m_BGFreeSlotQueue[mapId];
        }

        public void AddToBGFreeSlotQueue(Battleground bg)
        {
            m_BGFreeSlotQueue.Add(bg.GetMapId(), bg);
        }

        public void RemoveFromBGFreeSlotQueue(int mapId, int instanceId)
        {
            var queues = m_BGFreeSlotQueue[mapId].ToList();
            foreach (var bg in queues)
            {
                if (bg.GetInstanceID() == instanceId)
                {
                    queues.Remove(bg);
                    return;
                }
            }
        }

        public void AddBattleground(Battleground bg)
        {
            if (bg != null)
                bgDataStore[bg.GetTypeID()].m_Battlegrounds[bg.GetInstanceID()] = bg;
        }

        public void RemoveBattleground(BattlegroundTypeId bgTypeId, int instanceId)
        {
            bgDataStore[bgTypeId].m_Battlegrounds.Remove(instanceId);
        }

        public BattlegroundQueue GetBattlegroundQueue(BattlegroundQueueTypeId bgQueueTypeId)
        {
            if (!m_BattlegroundQueues.ContainsKey(bgQueueTypeId))
                m_BattlegroundQueues[bgQueueTypeId] = new BattlegroundQueue(bgQueueTypeId);

            return m_BattlegroundQueues[bgQueueTypeId];
        }

        public bool IsArenaTesting() { return m_ArenaTesting; }
        public bool IsTesting() { return m_Testing; }

        public BattlegroundTypeId GetBattleMasterBG(int entry)
        {
            return mBattleMastersMap.LookupByKey(entry);
        }

        public BattlegroundTemplate GetBattlegroundTemplateByTypeId(BattlegroundTypeId id)
        {
            return _battlegroundTemplates.LookupByKey(id);
        }

        BattlegroundTemplate GetBattlegroundTemplateByMapId(int mapId)
        {
            return _battlegroundMapTemplates.LookupByKey(mapId);
        }

        Dictionary<BattlegroundTypeId, BattlegroundData> bgDataStore = new();
        Dictionary<BattlegroundQueueTypeId, BattlegroundQueue> m_BattlegroundQueues = new();
        MultiMap<int, Battleground> m_BGFreeSlotQueue = new();
        Dictionary<int, BattlegroundTypeId> mBattleMastersMap = new();
        Dictionary<BattlegroundTypeId, BattlegroundTemplate> _battlegroundTemplates = new();
        Dictionary<int, BattlegroundTemplate> _battlegroundMapTemplates = new();

        struct ScheduledQueueUpdate
        {
            public ScheduledQueueUpdate(int arenaMatchmakerRating, BattlegroundQueueTypeId queueId, BattlegroundBracketId bracketId)
            {
                ArenaMatchmakerRating = arenaMatchmakerRating;
                QueueId = queueId;
                BracketId = bracketId;
            }

            public int ArenaMatchmakerRating;
            public BattlegroundQueueTypeId QueueId;
            public BattlegroundBracketId BracketId;

            public static bool operator ==(ScheduledQueueUpdate right, ScheduledQueueUpdate left)
            {
                return left.ArenaMatchmakerRating == right.ArenaMatchmakerRating && left.QueueId == right.QueueId && left.BracketId == right.BracketId;
            }

            public static bool operator !=(ScheduledQueueUpdate right, ScheduledQueueUpdate left)
            {
                return !(right == left);
            }

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return ArenaMatchmakerRating.GetHashCode() ^ QueueId.GetHashCode() ^ BracketId.GetHashCode();
            }
        }
        List<ScheduledQueueUpdate> m_QueueUpdateScheduler = new();
        TimeSpan m_NextRatedArenaUpdate;
        TimeSpan m_UpdateTimer;
        bool m_ArenaTesting;
        bool m_Testing;
    }

    public class BattlegroundData
    {
        public BattlegroundData()
        {
            for (var i = 0; i < (int)BattlegroundBracketId.Max; ++i)
                m_ClientBattlegroundIds[i] = new List<int>();
        }

        public Dictionary<int, Battleground> m_Battlegrounds = new();
        public List<int>[] m_ClientBattlegroundIds = new List<int>[(int)BattlegroundBracketId.Max];
        public Battleground Template;
    }

    public class BattlegroundTemplate
    {
        public BattlegroundTypeId Id;
        public WorldSafeLocsEntry[] StartLocation = new WorldSafeLocsEntry[SharedConst.PvpTeamsCount];
        public float MaxStartDistSq;
        public byte Weight;
        public int ScriptId;
        public BattlemasterListRecord BattlemasterEntry;

        public bool IsArena() { return BattlemasterEntry.InstanceType == (uint)MapTypes.Arena; }

        public ushort GetMinPlayersPerTeam()
        {
            return BattlemasterEntry.MinPlayers;
        }

        public ushort GetMaxPlayersPerTeam()
        {
            return BattlemasterEntry.MaxPlayers;
        }

        public byte GetMinLevel()
        {
            return BattlemasterEntry.MinLevel;
        }

        public byte GetMaxLevel()
        {
            return BattlemasterEntry.MaxLevel;
        }
    }
}
