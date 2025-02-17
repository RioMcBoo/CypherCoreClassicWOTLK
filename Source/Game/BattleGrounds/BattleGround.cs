﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Chat;
using Game.DataStorage;
using Game.Entities;
using Game.Groups;
using Game.Guilds;
using Game.Maps;
using Game.Networking;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Game.BattleGrounds
{
    public class Battleground : ZoneScript, IDisposable
    {
        public Battleground(BattlegroundTemplate battlegroundTemplate)
        {
            _battlegroundTemplate = battlegroundTemplate;
            m_Status = BattlegroundStatus.None;
            _winnerTeamId = PvPTeamId.Neutral;

            StartDelayTimes[BattlegroundConst.EventIdFirst] = (Minutes)2;
            StartDelayTimes[BattlegroundConst.EventIdSecond] = (Minutes)1;
            StartDelayTimes[BattlegroundConst.EventIdThird] = (Seconds)30;
            StartDelayTimes[BattlegroundConst.EventIdFourth] = TimeSpan.Zero;

            StartMessageIds[BattlegroundConst.EventIdFirst] = BattlegroundBroadcastTexts.StartTwoMinutes;
            StartMessageIds[BattlegroundConst.EventIdSecond] = BattlegroundBroadcastTexts.StartOneMinute;
            StartMessageIds[BattlegroundConst.EventIdThird] = BattlegroundBroadcastTexts.StartHalfMinute;
            StartMessageIds[BattlegroundConst.EventIdFourth] = BattlegroundBroadcastTexts.HasBegun;
        }

        public virtual void Dispose()
        {
            // remove objects and creatures
            // (this is done automatically in mapmanager update, when the instance is reset after the reset time)
            for (var i = 0; i < BgCreatures.Length; ++i)
                DelCreature(i);

            for (var i = 0; i < BgObjects.Length; ++i)
                DelObject(i);

            Global.BattlegroundMgr.RemoveBattleground(GetTypeID(), GetInstanceID());
            // unload map
            if (m_Map != null)
            {
                m_Map.UnloadAll(); // unload all objects (they may hold a reference to bg in their ZoneScript pointer)
                m_Map.SetUnload(); // mark for deletion by MapManager

                //unlink to prevent crash, always unlink all pointer reference before destruction
                m_Map.SetBG(null);
                m_Map = null;
            }

            // remove from bg free slot queue
            RemoveFromBGFreeSlotQueue();
        }

        public Battleground GetCopy()
        {
            return (Battleground)MemberwiseClone();
        }

        public void Update(TimeSpan diff)
        {
            if (!PreUpdateImpl(diff))
                return;

            if (GetPlayersSize() == 0)
            {
                //BG is empty
                // if there are no players invited, delete BG
                // this will delete arena or bg object, where any player entered
                // [[   but if you use Battleground object again (more battles possible to be played on 1 instance)
                //      then this condition should be removed and code:
                //      if (!GetInvitedCount(Team.Horde) && !GetInvitedCount(Team.Alliance))
                //          this.AddToFreeBGObjectsQueue(); // not yet implemented
                //      should be used instead of current
                // ]]
                // Battleground Template instance cannot be updated, because it would be deleted
                if (GetInvitedCount(Team.Horde) == 0 && GetInvitedCount(Team.Alliance) == 0)
                    m_SetDeleteThis = true;
                return;
            }

            switch (GetStatus())
            {
                case BattlegroundStatus.WaitJoin:
                    if (GetPlayersSize() != 0)
                    {
                        _ProcessJoin(diff);
                        _CheckSafePositions(diff);
                    }
                    break;
                case BattlegroundStatus.InProgress:
                    _ProcessOfflineQueue();
                    _ProcessPlayerPositionBroadcast(diff);
                    // after 47 Time.Minutes without one team losing, the arena closes with no winner and no rating change
                    if (IsArena())
                    {
                        if (GetElapsedTime() >= (Minutes)47)
                        {
                            EndBattleground(Team.Other);
                            return;
                        }
                    }
                    else
                    {
                        if (Global.BattlegroundMgr.GetPrematureFinishTime() != TimeSpan.Zero
                            && (GetPlayersCountByTeam(Team.Alliance) < GetMinPlayersPerTeam() || GetPlayersCountByTeam(Team.Horde) < GetMinPlayersPerTeam()))
                        {
                            _ProcessProgress(diff);
                        }
                        else if (m_PrematureCountDown)
                            m_PrematureCountDown = false;
                    }
                    break;
                case BattlegroundStatus.WaitLeave:
                    _ProcessLeave(diff);
                    break;
                default:
                    break;
            }

            // Update start time and reset stats timer
            SetElapsedTime(GetElapsedTime() + diff);
            if (GetStatus() == BattlegroundStatus.WaitJoin)
                m_ResetStatTimer += diff;

            PostUpdateImpl(diff);
        }

        void _CheckSafePositions(TimeSpan diff)
        {
            float maxDist = GetStartMaxDist();
            if (maxDist == 0.0f)
                return;

            m_ValidStartPositionTimer += diff;
            if (m_ValidStartPositionTimer >= BattlegroundConst.CheckPlayerPositionInverval)
            {
                m_ValidStartPositionTimer = TimeSpan.Zero;
                
                foreach (var guid in GetPlayers().Keys)
                {
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                    {
                        if (player.IsGameMaster())
                            continue;

                        Position pos = player.GetPosition();
                        WorldSafeLocsEntry startPos = GetTeamStartPosition(GetTeamIndexByTeamId(player.GetBGTeam()));
                        if (pos.GetExactDistSq(startPos.Loc) > maxDist)
                        {
                            Log.outDebug(LogFilter.Battleground,
                                $"Battleground: Sending {player.GetName()} " +
                                $"back to start location (map: {GetMapId()}) " +
                                $"(possible exploit)");
                            player.TeleportTo(startPos.Loc);
                        }
                    }
                }
            }
        }

        void _ProcessPlayerPositionBroadcast(TimeSpan diff)
        {
            m_LastPlayerPositionBroadcast += diff;
            if (m_LastPlayerPositionBroadcast >= BattlegroundConst.PlayerPositionUpdateInterval)
            {
                m_LastPlayerPositionBroadcast = TimeSpan.Zero;

                BattlegroundPlayerPositions playerPositions = new();
                for (var i =0; i < _playerPositions.Count; ++i)
                {
                    var playerPosition = _playerPositions[i];
                    // Update position data if we found player.
                    Player player = Global.ObjAccessor.GetPlayer(GetBgMap(), playerPosition.Guid);
                    if (player != null)
                        playerPosition.Pos = player.GetPosition();

                    playerPositions.FlagCarriers.Add(playerPosition);
                }

                SendPacketToAll(playerPositions);
            }
        }

        void _ProcessOfflineQueue()
        {
            // remove offline players from bg after 5 Time.Minutes
            if (!m_OfflineQueue.Empty())
            {
                var guid = m_OfflineQueue.FirstOrDefault();
                var bgPlayer = m_Players.LookupByKey(guid);
                if (bgPlayer != null)
                {
                    if (bgPlayer.OfflineRemoveTime <= LoopTime.ServerTime)
                    {
                        RemovePlayerAtLeave(guid, true, true);// remove player from BG
                        m_OfflineQueue.RemoveAt(0);                 // remove from offline queue
                    }
                }
            }
        }

        public virtual Team GetPrematureWinner()
        {
            Team winner = Team.Other;
            if (GetPlayersCountByTeam(Team.Alliance) >= GetMinPlayersPerTeam())
                winner = Team.Alliance;
            else if (GetPlayersCountByTeam(Team.Horde) >= GetMinPlayersPerTeam())
                winner = Team.Horde;

            return winner;
        }

        void _ProcessProgress(TimeSpan diff)
        {
            // *********************************************************
            // ***           Battleground BALLANCE SYSTEM            ***
            // *********************************************************
            // if less then minimum players are in on one side, then start premature finish timer
            if (!m_PrematureCountDown)
            {
                m_PrematureCountDown = true;
                m_PrematureCountDownTimer = Global.BattlegroundMgr.GetPrematureFinishTime();
            }
            else if (m_PrematureCountDownTimer < diff)
            {
                // time's up!
                EndBattleground(GetPrematureWinner());
                m_PrematureCountDown = false;
            }
            else if (!Global.BattlegroundMgr.IsTesting())
            {
                TimeSpan newtime = m_PrematureCountDownTimer - diff;
                // announce every Minute
                if (newtime > (Minutes)1)
                {
                    if (newtime.ToMinutes() != m_PrematureCountDownTimer.ToMinutes())
                        SendMessageToAll(CypherStrings.BattlegroundPrematureFinishWarning, ChatMsg.System, null, m_PrematureCountDownTimer.ToMinutes());
                }
                else
                {
                    //announce every 15 seconds
                    if (newtime.ToSeconds() / 15 != m_PrematureCountDownTimer.ToSeconds() / 15)
                        SendMessageToAll(CypherStrings.BattlegroundPrematureFinishWarningSecs, ChatMsg.System, null, m_PrematureCountDownTimer.ToSeconds());
                }
                m_PrematureCountDownTimer = newtime;
            }
        }

        void _ProcessJoin(TimeSpan diff)
        {
            // *********************************************************
            // ***           Battleground STARTING SYSTEM            ***
            // *********************************************************
            ModifyStartDelayTime(diff);

            if (!IsArena())
                SetRemainingTime((Minutes)5);

            if (m_ResetStatTimer > (Seconds)5)
            {
                m_ResetStatTimer = TimeSpan.Zero;
                foreach (var guid in GetPlayers().Keys)
                {
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                        player.ResetAllPowers();
                }
            }

            if (!m_Events.HasAnyFlag(BattlegroundEventFlags.Event1))
            {
                m_Events |= BattlegroundEventFlags.Event1;

                if (FindBgMap() == null)
                {
                    Log.outError(LogFilter.Battleground, 
                        $"Battleground._ProcessJoin: map (map id: {GetMapId()}, " +
                        $"instance id: {m_InstanceID}) is not created!");
                    EndNow();
                    return;
                }

                // Setup here, only when at least one player has ported to the map
                if (!SetupBattleground())
                {
                    EndNow();
                    return;
                }

                _preparationStartTime = LoopTime.ServerTime;
                foreach (Group group in m_BgRaids)
                {
                    if (group != null)
                        group.StartCountdown(CountdownTimerType.Pvp, StartDelayTimes[BattlegroundConst.EventIdFirst], _preparationStartTime);
                }

                StartingEventCloseDoors();
                SetStartDelayTime(StartDelayTimes[BattlegroundConst.EventIdFirst]);
                // First start warning - 2 or 1 Minute
                if (StartMessageIds[BattlegroundConst.EventIdFirst] != 0)
                    SendBroadcastText(StartMessageIds[BattlegroundConst.EventIdFirst], ChatMsg.BgSystemNeutral);
            }
            // After 1 Time.Minute or 30 seconds, warning is signaled
            else if (GetStartDelayTime() <= StartDelayTimes[BattlegroundConst.EventIdSecond] && !m_Events.HasAnyFlag(BattlegroundEventFlags.Event2))
            {
                m_Events |= BattlegroundEventFlags.Event2;
                if (StartMessageIds[BattlegroundConst.EventIdSecond] != 0)
                    SendBroadcastText(StartMessageIds[BattlegroundConst.EventIdSecond], ChatMsg.BgSystemNeutral);
            }
            // After 30 or 15 seconds, warning is signaled
            else if (GetStartDelayTime() <= StartDelayTimes[BattlegroundConst.EventIdThird] && !m_Events.HasAnyFlag(BattlegroundEventFlags.Event3))
            {
                m_Events |= BattlegroundEventFlags.Event3;
                if (StartMessageIds[BattlegroundConst.EventIdThird] != 0)
                    SendBroadcastText(StartMessageIds[BattlegroundConst.EventIdThird], ChatMsg.BgSystemNeutral);
            }
            // Delay expired (after 2 or 1 Time.Minute)
            else if (GetStartDelayTime() <= TimeSpan.Zero && !m_Events.HasAnyFlag(BattlegroundEventFlags.Event4))
            {
                m_Events |= BattlegroundEventFlags.Event4;

                StartingEventOpenDoors();

                if (StartMessageIds[BattlegroundConst.EventIdFourth] != 0)
                    SendBroadcastText(StartMessageIds[BattlegroundConst.EventIdFourth], ChatMsg.RaidBossEmote);
                
                SetStatus(BattlegroundStatus.InProgress);
                SetStartDelayTime(StartDelayTimes[BattlegroundConst.EventIdFourth]);

                SendPacketToAll(new PVPMatchSetState(PVPMatchState.Engaged));

                foreach (var (guid, _) in GetPlayers())
                {
                    Player player = Global.ObjAccessor.GetPlayer(GetBgMap(), guid);
                    if (player != null)
                    {
                        player.StartCriteria(CriteriaStartEvent.StartBattleground, GetBgMap().GetId());
                        player.AtStartOfEncounter(EncounterType.Battleground);
                    }
                }

                // Remove preparation
                if (IsArena())
                {
                    //todo add arena sound PlaySoundToAll(SOUND_ARENA_START);
                    foreach (var guid in GetPlayers().Keys)
                    {
                        Player player = Global.ObjAccessor.FindPlayer(guid);
                        if (player != null)
                        {
                            // Correctly display EnemyUnitFrame
                            player.SetArenaFaction((byte)player.GetBGTeam());

                            player.RemoveAurasDueToSpell(BattlegroundConst.SpellArenaPreparation);
                            player.ResetAllPowers();
                            if (!player.IsGameMaster())
                            {
                                // remove auras with duration lower than 30s
                                player.RemoveAppliedAuras(aurApp =>
                                {
                                    Aura aura = aurApp.GetBase();
                                    return !aura.IsPermanent() && aura.GetDuration() <= (Seconds)30 && aurApp.IsPositive()
                                    && !aura.GetSpellInfo().HasAttribute(SpellAttr0.NoImmunities) && !aura.HasEffectType(AuraType.ModInvisibility);
                                });
                            }
                        }
                    }

                    CheckWinConditions();
                }
                else
                {
                    PlaySoundToAll((int)BattlegroundSounds.BgStart);

                    foreach (var guid in GetPlayers().Keys)
                    {
                        Player player = Global.ObjAccessor.FindPlayer(guid);
                        if (player != null)
                        {
                            player.RemoveAurasDueToSpell(BattlegroundConst.SpellPreparation);
                            player.ResetAllPowers();
                        }
                    }
                    // Announce BG starting
                    if (WorldConfig.Values[WorldCfg.BattlegroundQueueAnnouncerEnable].Bool)
                        Global.WorldMgr.SendWorldText(CypherStrings.BgStartedAnnounceWorld, GetName(), GetMinLevel(), GetMaxLevel());
                }
            }

            if (GetRemainingTime() > TimeSpan.Zero && (m_EndTime -= diff) > TimeSpan.Zero)
                SetRemainingTime(GetRemainingTime() - diff);
        }

        void _ProcessLeave(TimeSpan diff)
        {
            // *********************************************************
            // ***           Battleground ENDING SYSTEM              ***
            // *********************************************************
            // remove all players from Battleground after 2 Time.Minutes
            SetRemainingTime(GetRemainingTime() - diff);
            if (GetRemainingTime() <= TimeSpan.Zero)
            {
                SetRemainingTime(TimeSpan.Zero);
                foreach (var guid in m_Players.Keys)
                {
                    RemovePlayerAtLeave(guid, true, true);// remove player from BG
                    // do not change any Battleground's private variables
                }
            }
        }

        public Player _GetPlayer(ObjectGuid guid, bool offlineRemove, string context)
        {
            Player player = null;
            if (!offlineRemove)
            {
                player = Global.ObjAccessor.FindPlayer(guid);
                if (player == null)
                {
                    Log.outError(LogFilter.Battleground,
                        $"Battleground.{context}: player ({guid}) not found for BG " +
                        $"(map: {GetMapId()}, instance id: {m_InstanceID})!");
                }
            }
            return player;
        }

        public Player _GetPlayer(KeyValuePair<ObjectGuid, BattlegroundPlayer> pair, string context)
        {
            return _GetPlayer(pair.Key, pair.Value.OfflineRemoveTime != ServerTime.Zero, context);
        }

        Player _GetPlayerForTeam(Team team, KeyValuePair<ObjectGuid, BattlegroundPlayer> pair, string context)
        {
            Player player = _GetPlayer(pair, context);
            if (player != null)
            {
                Team playerTeam = pair.Value.Team;
                if (playerTeam == 0)
                    playerTeam = player.GetEffectiveTeam();
                if (playerTeam != team)
                    player = null;
            }
            return player;
        }

        public BattlegroundMap GetBgMap()
        {
            Cypher.Assert(m_Map != null);
            return m_Map;
        }

        public WorldSafeLocsEntry GetTeamStartPosition(int teamId)
        {
            Cypher.Assert(teamId < BattleGroundTeamId.Neutral);
            return _battlegroundTemplate.StartLocation[teamId];
        }

        float GetStartMaxDist()
        {
            return _battlegroundTemplate.MaxStartDistSq;
        }
        
        public void SendPacketToAll(ServerPacket packet)
        {
            foreach (var pair in m_Players)
            {
                Player player = _GetPlayer(pair, "SendPacketToAll");
                if (player != null)
                    player.SendPacket(packet);
            }
        }

        void SendPacketToTeam(Team team, ServerPacket packet, Player except = null)
        {
            foreach (var pair in m_Players)
            {
                Player player = _GetPlayerForTeam(team, pair, "SendPacketToTeam");
                if (player != null)
                {
                    if (player != except)
                        player.SendPacket(packet);
                }
            }
        }

        public void SendChatMessage(Creature source, byte textId, WorldObject target = null)
        {
            Global.CreatureTextMgr.SendChat(source, textId, target);
        }

        public void SendBroadcastText(int id, ChatMsg msgType, WorldObject target = null)
        {
            if (!CliDB.BroadcastTextStorage.ContainsKey(id))
            {
                Log.outError(LogFilter.Battleground, $"Battleground.SendBroadcastText: `broadcast_text` (ID: {id}) was not found");
                return;
            }

            BroadcastTextBuilder builder = new(null, msgType, id, Gender.Male, target);
            LocalizedDo localizer = new(builder);
            BroadcastWorker(localizer);
        }

        public void PlaySoundToAll(int soundID)
        {
            SendPacketToAll(new PlaySound(ObjectGuid.Empty, soundID, 0));
        }

        void PlaySoundToTeam(int soundID, Team team)
        {
            SendPacketToTeam(team, new PlaySound(ObjectGuid.Empty, soundID, 0));
        }

        public void CastSpellOnTeam(int SpellID, Team team)
        {
            foreach (var pair in m_Players)
            {
                Player player = _GetPlayerForTeam(team, pair, "CastSpellOnTeam");
                if (player != null)
                    player.CastSpell(player, SpellID, true);
            }
        }

        void RemoveAuraOnTeam(int SpellID, Team team)
        {
            foreach (var pair in m_Players)
            {
                Player player = _GetPlayerForTeam(team, pair, "RemoveAuraOnTeam");
                if (player != null)
                    player.RemoveAura(SpellID);
            }
        }

        public void RewardHonorToTeam(int Honor, Team team)
        {
            foreach (var pair in m_Players)
            {
                Player player = _GetPlayerForTeam(team, pair, "RewardHonorToTeam");
                if (player != null)
                    UpdatePlayerScore(player, ScoreType.BonusHonor, Honor);
            }
        }

        public void RewardReputationToTeam(int faction_id, int Reputation, Team team)
        {
            FactionRecord factionEntry = CliDB.FactionStorage.LookupByKey(faction_id);
            if (factionEntry == null)
                return;

            foreach (var pair in m_Players)
            {
                Player player = _GetPlayerForTeam(team, pair, "RewardReputationToTeam");
                if (player == null)
                    continue;

                if (player.HasPlayerFlagEx(PlayerFlagsEx.MercenaryMode))
                    continue;

                int repGain = Reputation;
                MathFunctions.AddPct(ref repGain, player.GetTotalAuraModifier(AuraType.ModReputationGain));
                MathFunctions.AddPct(ref repGain, player.GetTotalAuraModifierByMiscValue(AuraType.ModFactionReputationGain, faction_id));
                player.GetReputationMgr().ModifyReputation(factionEntry, repGain);
            }
        }

        public void UpdateWorldState(int worldStateId, DateTime value, bool hidden = false)
        {            
            Global.WorldStateMgr.SetValue(worldStateId, (UnixTime)value, hidden, GetBgMap());
        }

        public void UpdateWorldState(int worldStateId, WorldStateValue value, bool hidden = false)
        {
            Global.WorldStateMgr.SetValue(worldStateId, value, hidden, GetBgMap());
        }

        public virtual void EndBattleground(Team winner)
        {
            RemoveFromBGFreeSlotQueue();

            bool guildAwarded = false;

            if (winner == Team.Alliance)
            {
                if (IsBattleground())
                    SendBroadcastText(BattlegroundBroadcastTexts.AllianceWins, ChatMsg.BgSystemNeutral);

                PlaySoundToAll((int)BattlegroundSounds.AllianceWins);
                SetWinner(PvPTeamId.Alliance);
            }
            else if (winner == Team.Horde)
            {
                if (IsBattleground())
                    SendBroadcastText(BattlegroundBroadcastTexts.HordeWins, ChatMsg.BgSystemNeutral);

                PlaySoundToAll((int)BattlegroundSounds.HordeWins);
                SetWinner(PvPTeamId.Horde);
            }
            else
            {
                SetWinner(PvPTeamId.Neutral);
            }

            PreparedStatement stmt;
            ulong battlegroundId = 1;
            if (IsBattleground() && WorldConfig.Values[WorldCfg.BattlegroundStoreStatisticsEnable].Bool)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_PVPSTATS_MAXID);
                SQLResult result = DB.Characters.Query(stmt);

                if (!result.IsEmpty())
                    battlegroundId = result.Read<ulong>(0) + 1;

                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_PVPSTATS_BATTLEGROUND);
                stmt.SetUInt64(0, battlegroundId);
                stmt.SetUInt8(1, (byte)GetWinner());
                stmt.SetUInt8(2, GetUniqueBracketId());
                stmt.SetUInt32(3, (uint)GetTypeID());
                DB.Characters.Execute(stmt);
            }

            SetStatus(BattlegroundStatus.WaitLeave);
            //we must set it this way, because end time is sent in packet!
            SetRemainingTime(BattlegroundConst.AutocloseBattleground);

            PVPMatchComplete pvpMatchComplete = new();
            pvpMatchComplete.Winner = GetWinner();
            pvpMatchComplete.Duration = Time.Max(TimeSpan.Zero, GetElapsedTime() - (Minutes)2);
            BuildPvPLogDataPacket(out pvpMatchComplete.LogData);
            pvpMatchComplete.Write();

            foreach (var pair in m_Players)
            {
                Team team = pair.Value.Team;

                Player player = _GetPlayer(pair, "EndBattleground");
                if (player == null)
                    continue;

                // should remove spirit of redemption
                if (player.HasAuraType(AuraType.SpiritOfRedemption))
                    player.RemoveAurasByType(AuraType.ModShapeshift);

                if (!player.IsAlive())
                {
                    player.ResurrectPlayer(1.0f);
                    player.SpawnCorpseBones();
                }
                else
                {
                    //needed cause else in av some creatures will kill the players at the end
                    player.CombatStop();
                }

                // remove temporary currency bonus auras before rewarding player
                player.RemoveAura(BattlegroundConst.SpellHonorableDefender25y);
                player.RemoveAura(BattlegroundConst.SpellHonorableDefender60y);

                int winnerKills = player.GetRandomWinner() 
                    ? WorldConfig.Values[WorldCfg.BgRewardWinnerHonorLast].Int32 
                    : WorldConfig.Values[WorldCfg.BgRewardWinnerHonorFirst].Int32;

                int loserKills = player.GetRandomWinner() 
                    ? WorldConfig.Values[WorldCfg.BgRewardLoserHonorLast].Int32 
                    : WorldConfig.Values[WorldCfg.BgRewardLoserHonorFirst].Int32;

                if (IsBattleground() && WorldConfig.Values[WorldCfg.BattlegroundStoreStatisticsEnable].Bool)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_PVPSTATS_PLAYER);
                    var score = PlayerScores.LookupByKey(player.GetGUID());

                    stmt.SetUInt64(0, battlegroundId);
                    stmt.SetInt64(1, player.GetGUID().GetCounter());
                    stmt.SetBool(2, team == winner);
                    stmt.SetInt32(3, score.KillingBlows);
                    stmt.SetInt32(4, score.Deaths);
                    stmt.SetInt32(5, score.HonorableKills);
                    stmt.SetInt32(6, score.BonusHonor);
                    stmt.SetInt32(7, score.DamageDone);
                    stmt.SetInt32(8, score.HealingDone);
                    stmt.SetInt32(9, score.GetAttr1());
                    stmt.SetInt32(10, score.GetAttr2());
                    stmt.SetInt32(11, score.GetAttr3());
                    stmt.SetInt32(12, score.GetAttr4());
                    stmt.SetInt32(13, score.GetAttr5());

                    DB.Characters.Execute(stmt);
                }

                // Reward winner team
                if (team == winner)
                {
                    BattlegroundPlayer bgPlayer = GetBattlegroundPlayerData(player.GetGUID());
                    if (bgPlayer != null)
                    {
                        if (Global.BattlegroundMgr.IsRandomBattleground(bgPlayer.queueTypeId.BattlemasterListId)
                            || Global.BattlegroundMgr.IsBGWeekend(bgPlayer.queueTypeId.BattlemasterListId))
                        {
                            UpdatePlayerScore(player, ScoreType.BonusHonor, GetBonusHonorFromKill(winnerKills));
                            if (!player.GetRandomWinner())
                            {
                                player.SetRandomWinner(true);
                                // TODO: win honor xp
                            }
                        }
                        else
                        {
                            // TODO: loss honor xp
                        }
                    }

                    player.UpdateCriteria(CriteriaType.WinBattleground, player.GetMapId());
                    if (!guildAwarded)
                    {
                        guildAwarded = true;
                        uint guildId = GetBgMap().GetOwnerGuildId(player.GetBGTeam());
                        if (guildId != 0)
                        {
                            Guild guild = Global.GuildMgr.GetGuildById(guildId);
                            if (guild != null)
                                guild.UpdateCriteria(CriteriaType.WinBattleground, player.GetMapId(), 0, 0, null, player);
                        }
                    }
                }
                else
                {
                    BattlegroundPlayer bgPlayer = GetBattlegroundPlayerData(player.GetGUID());
                    if (bgPlayer != null)
                    {
                        if (Global.BattlegroundMgr.IsRandomBattleground(bgPlayer.queueTypeId.BattlemasterListId)
                            || Global.BattlegroundMgr.IsBGWeekend(bgPlayer.queueTypeId.BattlemasterListId))
                            UpdatePlayerScore(player, ScoreType.BonusHonor, GetBonusHonorFromKill(loserKills));
                    }
                }

                player.ResetAllPowers();
                player.CombatStopWithPets(true);

                BlockMovement(player);

                player.SendPacket(pvpMatchComplete);

                player.UpdateCriteria(CriteriaType.ParticipateInBattleground, player.GetMapId());
            }
        }

        int GetScriptId()
        {
            return _battlegroundTemplate.ScriptId;
        }
        
        public int GetBonusHonorFromKill(int kills)
        {
            //variable kills means how many honorable kills you scored (so we need kills * honor_for_one_kill)
            int maxLevel = Math.Min(GetMaxLevel(), 80);
            return Formulas.HKHonorAtLevel(maxLevel, kills);
        }

        void BlockMovement(Player player)
        {
            // movement disabled NOTE: the effect will be automatically removed by client when the player is teleported from the battleground, so no need to send with uint8(1) in RemovePlayerAtLeave()
            player.SetClientControl(player, false);
        }

        public virtual void RemovePlayerAtLeave(ObjectGuid guid, bool Transport, bool SendPacket)
        {
            Team team = GetPlayerTeam(guid);
            bool participant = false;
            // Remove from lists/maps
            var bgPlayer = m_Players.LookupByKey(guid);
            BattlegroundQueueTypeId? bgQueueTypeId = null;
            if (bgPlayer != null)
            {
                bgQueueTypeId = bgPlayer.queueTypeId;
                UpdatePlayersCountByTeam(team, true);               // -1 player
                m_Players.Remove(guid);
                // check if the player was a participant of the match, or only entered through gm command (goname)
                participant = true;
            }

            if (PlayerScores.ContainsKey(guid))
                PlayerScores.Remove(guid);

            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
            { 
                // should remove spirit of redemption
                if (player.HasAuraType(AuraType.SpiritOfRedemption))
                    player.RemoveAurasByType(AuraType.ModShapeshift);

                player.RemoveAurasByType(AuraType.Mounted);
                player.RemoveAura(BattlegroundConst.SpellMercenaryHorde1);
                player.RemoveAura(BattlegroundConst.SpellMercenaryHordeReactions);
                player.RemoveAura(BattlegroundConst.SpellMercenaryAlliance1);
                player.RemoveAura(BattlegroundConst.SpellMercenaryAllianceReactions);
                player.RemoveAura(BattlegroundConst.SpellMercenaryShapeshift);
                player.RemovePlayerFlagEx(PlayerFlagsEx.MercenaryMode);

                player.AtEndOfEncounter(EncounterType.Battleground);

                player.RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.LeaveArenaOrBattleground);

                if (!player.IsAlive())                              // resurrect on exit
                {
                    player.ResurrectPlayer(1.0f);
                    player.SpawnCorpseBones();
                }
            }
            else
                Player.OfflineResurrect(guid, null);

            RemovePlayer(player, guid, team);                           // BG subclass specific code

            if (participant) // if the player was a match participant, remove auras, calc rating, update queue
            {
                if (player != null)
                {
                    player.ClearAfkReports();

                    // if arena, remove the specific arena auras
                    if (IsArena())
                    {
                        // unsummon current and summon old pet if there was one and there isn't a current pet
                        player.RemovePet(null, PetSaveMode.NotInSlot);
                        player.ResummonPetTemporaryUnSummonedIfAny();
                    }
                    if (SendPacket && bgQueueTypeId.HasValue)
                    {
                        BattlefieldStatusNone battlefieldStatus;
                        Global.BattlegroundMgr.BuildBattlegroundStatusNone(out battlefieldStatus, player, player.GetBattlegroundQueueIndex(bgQueueTypeId.Value), player.GetBattlegroundQueueJoinTime(bgQueueTypeId.Value));
                        player.SendPacket(battlefieldStatus);
                    }

                    // this call is important, because player, when joins to Battleground, this method is not called, so it must be called when leaving bg
                    if (bgQueueTypeId.HasValue)
                        player.RemoveBattlegroundQueueId(bgQueueTypeId.Value);
                }

                // remove from raid group if player is member
                Group group = GetBgRaid(team);
                if (group != null)
                {
                    if (!group.RemoveMember(guid))                // group was disbanded
                        SetBgRaid(team, null);
                }
                DecreaseInvitedCount(team);
                //we should update Battleground queue, but only if bg isn't ending
                if (IsBattleground() && GetStatus() < BattlegroundStatus.WaitLeave && bgQueueTypeId.HasValue)
                {
                    // a player has left the Battleground, so there are free slots . add to queue
                    AddToBGFreeSlotQueue();
                    Global.BattlegroundMgr.ScheduleQueueUpdate(0, bgQueueTypeId.Value, GetBracketId());
                }
                // Let others know
                BattlegroundPlayerLeft playerLeft = new();
                playerLeft.Guid = guid;
                SendPacketToTeam(team, playerLeft, player);
            }

            if (player != null)
            {
                // Do next only if found in Battleground
                player.SetBattlegroundId(0, BattlegroundTypeId.None);  // We're not in BG.
                // reset destination bg team
                player.SetBGTeam(Team.Other);

                // remove all criterias on bg leave
                player.FailCriteria(CriteriaFailEvent.LeaveBattleground, 0);

                if (Transport)
                    player.TeleportToBGEntryPoint();

                Log.outDebug(LogFilter.Battleground, 
                    $"Removed player {player.GetName()} from Battleground.");
            }

            //Battleground object will be deleted next Battleground.Update() call
        }

        // this method is called when no players remains in Battleground
        public virtual void Reset()
        {
            SetWinner(PvPTeamId.Neutral);
            SetStatus(BattlegroundStatus.WaitQueue);
            SetElapsedTime(TimeSpan.Zero);
            SetRemainingTime(TimeSpan.Zero);
            m_Events = 0;

            if (m_InvitedAlliance > 0 || m_InvitedHorde > 0)
            {
                Log.outError(LogFilter.Battleground,
                    $"Battleground.Reset: one of the counters is not 0 (Team.Alliance: {m_InvitedAlliance}, " +
                    $"Team.Horde: {m_InvitedHorde}) for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");
            }

            m_InvitedAlliance = 0;
            m_InvitedHorde = 0;
            m_InBGFreeSlotQueue = false;

            m_Players.Clear();

            PlayerScores.Clear();

            _playerPositions.Clear();
        }

        public void StartBattleground()
        {
            SetElapsedTime(TimeSpan.Zero);
            // add BG to free slot queue
            AddToBGFreeSlotQueue();

            // add bg to update list
            // This must be done here, because we need to have already invited some players when first BG.Update() method is executed
            // and it doesn't matter if we call StartBattleground() more times, because m_Battlegrounds is a map and instance id never changes
            Global.BattlegroundMgr.AddBattleground(this);

            if (m_IsRated)
            {
                Log.outDebug(LogFilter.Arena,
                    $"Arena match type: {m_ArenaType} for Team1Id: {m_ArenaTeamIds[BattleGroundTeamId.Alliance]}" +
                    $" - Team2Id: {m_ArenaTeamIds[BattleGroundTeamId.Horde]} started.");
            }
        }

        public void TeleportPlayerToExploitLocation(Player player)
        {
            WorldSafeLocsEntry loc = GetExploitTeleportLocation(player.GetBGTeam());
            if (loc != null)
                player.TeleportTo(loc.Loc);
        }

        public virtual void AddPlayer(Player player, BattlegroundQueueTypeId queueId)
        {
            // remove afk from player
            if (player.IsAFK())
                player.ToggleAFK();

            // score struct must be created in inherited class

            ObjectGuid guid = player.GetGUID();
            Team team = player.GetBGTeam();

            BattlegroundPlayer bp = new();
            bp.OfflineRemoveTime = ServerTime.Zero;
            bp.Team = team;
            bp.Mercenary = player.IsMercenaryForBattlegroundQueueType(queueId);
            bp.queueTypeId = queueId;

            bool isInBattleground = IsPlayerInBattleground(player.GetGUID());
            // Add to list/maps
            m_Players[guid] = bp;

            if (!isInBattleground)
                UpdatePlayersCountByTeam(team, false);                  // +1 player

            BattlegroundPlayerJoined playerJoined = new();
            playerJoined.Guid = player.GetGUID();
            SendPacketToTeam(team, playerJoined, player);

            PVPMatchInitialize pvpMatchInitialize = new();
            pvpMatchInitialize.MapID = GetMapId();
            switch (GetStatus())
            {
                case BattlegroundStatus.None:
                case BattlegroundStatus.WaitQueue:
                    pvpMatchInitialize.State = PVPMatchState.Inactive;
                    break;
                case BattlegroundStatus.WaitJoin:
                    pvpMatchInitialize.State = PVPMatchState.StartUp;
                    break;
                case BattlegroundStatus.InProgress:
                    pvpMatchInitialize.State = PVPMatchState.Engaged;
                    break;
                case BattlegroundStatus.WaitLeave:
                    pvpMatchInitialize.State = PVPMatchState.Complete;
                    break;
                default:
                    break;
            }

            if (GetElapsedTime() >= (Minutes)2)
            {
                pvpMatchInitialize.Duration = GetElapsedTime() - (Minutes)2;
                pvpMatchInitialize.StartTime = LoopTime.ServerTime - pvpMatchInitialize.Duration;
            }

            pvpMatchInitialize.ArenaFaction = (byte)(player.GetBGTeam() == Team.Horde ? PvPTeamId.Horde : PvPTeamId.Alliance);
            pvpMatchInitialize.BattlemasterListID = queueId.BattlemasterListId;
            pvpMatchInitialize.Registered = false;
            pvpMatchInitialize.AffectsRating = IsRated();

            player.SendPacket(pvpMatchInitialize);

            player.RemoveAurasByType(AuraType.Mounted);

            // add arena specific auras
            if (IsArena())
            {
                player.RemoveArenaEnchantments(EnchantmentSlot.EnhancementTemporary);

                player.DestroyConjuredItems(true);
                player.UnsummonPetTemporaryIfAny();

                if (GetStatus() == BattlegroundStatus.WaitJoin)                 // not started yet
                {
                    player.CastSpell(player, BattlegroundConst.SpellArenaPreparation, true);
                    player.ResetAllPowers();
                }
            }
            else
            {
                if (GetStatus() == BattlegroundStatus.WaitJoin)                 // not started yet
                    player.CastSpell(player, BattlegroundConst.SpellPreparation, true);   // reduces all mana cost of spells.

                if (bp.Mercenary)
                {
                    if (bp.Team == Team.Horde)
                    {
                        player.CastSpell(player, BattlegroundConst.SpellMercenaryHorde1, true);
                        player.CastSpell(player, BattlegroundConst.SpellMercenaryHordeReactions, true);
                    }
                    else if (bp.Team == Team.Alliance)
                    {
                        player.CastSpell(player, BattlegroundConst.SpellMercenaryAlliance1, true);
                        player.CastSpell(player, BattlegroundConst.SpellMercenaryAllianceReactions, true);
                    }

                    player.CastSpell(player, BattlegroundConst.SpellMercenaryShapeshift);
                    player.SetPlayerFlagEx(PlayerFlagsEx.MercenaryMode);
                }
            }

            // setup BG group membership
            PlayerAddedToBGCheckIfBGIsRunning(player);
            AddOrSetPlayerToCorrectBgGroup(player, team);
        }

        // this method adds player to his team's bg group, or sets his correct group if player is already in bg group
        public void AddOrSetPlayerToCorrectBgGroup(Player player, Team team)
        {
            ObjectGuid playerGuid = player.GetGUID();
            Group group = GetBgRaid(team);
            if (group == null)                                      // first player joined
            {
                group = new Group();
                SetBgRaid(team, group);
                group.Create(player);
                TimeSpan countdownMaxForBGType = StartDelayTimes[BattlegroundConst.EventIdFirst];
                if (_preparationStartTime != ServerTime.Zero)
                    group.StartCountdown(CountdownTimerType.Pvp, countdownMaxForBGType, _preparationStartTime);
                else
                    group.StartCountdown(CountdownTimerType.Pvp, countdownMaxForBGType);
            }
            else                                            // raid already exist
            {
                if (group.IsMember(playerGuid))
                {
                    byte subgroup = group.GetMemberGroup(playerGuid);
                    player.SetBattlegroundOrBattlefieldRaid(group, subgroup);
                }
                else
                {
                    group.AddMember(player);
                    Group originalGroup = player.GetOriginalGroup();
                    if (originalGroup != null)
                    {
                        if (originalGroup.IsLeader(playerGuid))
                        {
                            group.ChangeLeader(playerGuid);
                            group.SendUpdate();
                        }
                    }
                }
            }
        }

        // This method should be called when player logs into running Battleground
        public void EventPlayerLoggedIn(Player player)
        {
            ObjectGuid guid = player.GetGUID();
            // player is correct pointer
            foreach (var id in m_OfflineQueue)
            {
                if (id == guid)
                {
                    m_OfflineQueue.Remove(id);
                    break;
                }
            }
            m_Players[guid].OfflineRemoveTime = ServerTime.Zero;
            PlayerAddedToBGCheckIfBGIsRunning(player);
            // if Battleground is starting, then add preparation aura
            // we don't have to do that, because preparation aura isn't removed when player logs out
        }

        // This method should be called when player logs out from running Battleground
        public void EventPlayerLoggedOut(Player player)
        {
            ObjectGuid guid = player.GetGUID();
            if (!IsPlayerInBattleground(guid))  // Check if this player really is in Battleground (might be a GM who teleported inside)
                return;

            // player is correct pointer, it is checked in WorldSession.LogoutPlayer()
            m_OfflineQueue.Add(player.GetGUID());
            m_Players[guid].OfflineRemoveTime = LoopTime.ServerTime + BattlegroundConst.MaxOfflineTime;
            if (GetStatus() == BattlegroundStatus.InProgress)
            {
                // drop flag and handle other cleanups
                RemovePlayer(player, guid, GetPlayerTeam(guid));

                // 1 player is logging out, if it is the last alive, then end arena!
                if (IsArena() && player.IsAlive())
                    if (GetAlivePlayersCountByTeam(player.GetBGTeam()) <= 1 && GetPlayersCountByTeam(GetOtherTeam(player.GetBGTeam())) != 0)
                        EndBattleground(GetOtherTeam(player.GetBGTeam()));
            }
        }

        // This method should be called only once ... it adds pointer to queue
        void AddToBGFreeSlotQueue()
        {
            if (!m_InBGFreeSlotQueue && IsBattleground())
            {
                Global.BattlegroundMgr.AddToBGFreeSlotQueue(this);
                m_InBGFreeSlotQueue = true;
            }
        }

        // This method removes this Battleground from free queue - it must be called when deleting Battleground
        public void RemoveFromBGFreeSlotQueue()
        {
            if (m_InBGFreeSlotQueue)
            {
                Global.BattlegroundMgr.RemoveFromBGFreeSlotQueue(GetMapId(), m_InstanceID);
                m_InBGFreeSlotQueue = false;
            }
        }

        // get the number of free slots for team
        // returns the number how many players can join Battleground to MaxPlayersPerTeam
        public int GetFreeSlotsForTeam(Team team)
        {
            // if BG is starting and WorldCfg.BattlegroundInvitationType == BattlegroundQueueInvitationTypeB.NoBalance, invite anyone
            if (GetStatus() == BattlegroundStatus.WaitJoin 
                && WorldConfig.Values[WorldCfg.BattlegroundInvitationType].Int32 == (int)BattlegroundQueueInvitationType.NoBalance)
            {
                return (GetInvitedCount(team) < GetMaxPlayersPerTeam()) ? GetMaxPlayersPerTeam() - GetInvitedCount(team) : 0;
            }

            // if BG is already started
            // or WorldCfg.BattlegroundInvitationType != BattlegroundQueueInvitationType.NoBalance,
            // do not allow to join too much players of one faction
            int otherTeamInvitedCount;
            int thisTeamInvitedCount;
            int otherTeamPlayersCount;
            int thisTeamPlayersCount;

            if (team == Team.Alliance)
            {
                thisTeamInvitedCount = GetInvitedCount(Team.Alliance);
                otherTeamInvitedCount = GetInvitedCount(Team.Horde);
                thisTeamPlayersCount = GetPlayersCountByTeam(Team.Alliance);
                otherTeamPlayersCount = GetPlayersCountByTeam(Team.Horde);
            }
            else
            {
                thisTeamInvitedCount = GetInvitedCount(Team.Horde);
                otherTeamInvitedCount = GetInvitedCount(Team.Alliance);
                thisTeamPlayersCount = GetPlayersCountByTeam(Team.Horde);
                otherTeamPlayersCount = GetPlayersCountByTeam(Team.Alliance);
            }
            if (GetStatus() == BattlegroundStatus.InProgress || GetStatus() == BattlegroundStatus.WaitJoin)
            {
                // difference based on ppl invited (not necessarily entered battle)
                // default: allow 0
                var diff = 0;

                // allow join one person if the sides are equal (to fill up bg to minPlayerPerTeam)
                if (otherTeamInvitedCount == thisTeamInvitedCount)
                    diff = 1;
                // allow join more ppl if the other side has more players
                else if (otherTeamInvitedCount > thisTeamInvitedCount)
                    diff = otherTeamInvitedCount - thisTeamInvitedCount;

                // difference based on max players per team (don't allow inviting more)
                var diff2 = (thisTeamInvitedCount < GetMaxPlayersPerTeam()) ? GetMaxPlayersPerTeam() - thisTeamInvitedCount : 0;
                // difference based on players who already entered
                // default: allow 0
                var diff3 = 0;
                // allow join one person if the sides are equal (to fill up bg minPlayerPerTeam)
                if (otherTeamPlayersCount == thisTeamPlayersCount)
                    diff3 = 1;
                // allow join more ppl if the other side has more players
                else if (otherTeamPlayersCount > thisTeamPlayersCount)
                    diff3 = otherTeamPlayersCount - thisTeamPlayersCount;
                // or other side has less than minPlayersPerTeam
                else if (thisTeamInvitedCount <= GetMinPlayersPerTeam())
                    diff3 = GetMinPlayersPerTeam() - thisTeamInvitedCount + 1;

                // return the minimum of the 3 differences

                // min of diff and diff 2
                diff = Math.Min(diff, diff2);
                // min of diff, diff2 and diff3
                return Math.Min(diff, diff3);
            }
            return 0;
        }

        public bool IsArena()
        {
            return _battlegroundTemplate.IsArena();
        }

        public bool IsBattleground()
        {
            return !IsArena();
        }
        
        public bool HasFreeSlots()
        {
            return GetPlayersSize() < GetMaxPlayers();
        }

        public virtual void BuildPvPLogDataPacket(out PVPMatchStatistics pvpLogData)
        {
            pvpLogData = new PVPMatchStatistics();

            foreach (var score in PlayerScores)
            {
                PVPMatchStatistics.PVPMatchPlayerStatistics playerData;

                score.Value.BuildPvPLogPlayerDataPacket(out playerData);

                Player player = Global.ObjAccessor.GetPlayer(GetBgMap(), playerData.PlayerGUID);
                if (player != null)
                {
                    playerData.IsInWorld = true;
                    playerData.PrimaryTalentTree = player.GetPrimarySpecialization();
                    playerData.Sex = player.GetGender();
                    playerData.PlayerRace = player.GetRace();
                    playerData.PlayerClass = player.GetClass();
                    playerData.HonorLevel = player.GetHonorLevel();
                }

                pvpLogData.Statistics.Add(playerData);
            }

            pvpLogData.PlayerCount[(int)PvPTeamId.Horde] = (sbyte)GetPlayersCountByTeam(Team.Horde);
            pvpLogData.PlayerCount[(int)PvPTeamId.Alliance] = (sbyte)GetPlayersCountByTeam(Team.Alliance);
        }

        public virtual bool UpdatePlayerScore(Player player, ScoreType type, int value, bool doAddHonor = true)
        {
            var bgScore = PlayerScores.LookupByKey(player.GetGUID());
            if (bgScore == null)  // player not found...
                return false;

            if (type == ScoreType.BonusHonor && doAddHonor && IsBattleground())
                player.RewardHonor(null, 1, value);
            else
                bgScore.UpdateScore(type, value);

            return true;
        }

        public bool AddObject(int type, int entry, float x, float y, float z, float o, float rotation0, float rotation1, float rotation2, float rotation3, TimeSpan respawnTime = default, GameObjectState goState = GameObjectState.Ready)
        {
            Map map = FindBgMap();
            if (map == null)
                return false;

            Quaternion rotation = new(rotation0, rotation1, rotation2, rotation3);
            // Temporally add safety check for bad spawns and send log (object rotations need to be rechecked in sniff)
            if (rotation0 == 0 && rotation1 == 0 && rotation2 == 0 && rotation3 == 0)
            {
                Log.outDebug(LogFilter.Battleground, 
                    $"Battleground.AddObject: gameoobject [entry: {entry}, object Type: {type}] " +
                    $"for BG (map: {GetMapId()}) has zeroed rotation fields, " +
                    "orientation used temporally, but please fix the spawn");

                rotation = Quaternion.CreateFromRotationMatrix(Extensions.fromEulerAnglesZYX(o, 0.0f, 0.0f));
            }

            // Must be created this way, adding to godatamap would add it to the base map of the instance
            // and when loading it (in go.LoadFromDB()), a new guid would be assigned to the object, and a new object would be created
            // So we must create it specific for this instance
            GameObject go = GameObject.CreateGameObject(entry, GetBgMap(), new Position(x, y, z, o), rotation, 255, goState);
            if (go == null)
            {
                Log.outError(LogFilter.Battleground, 
                    $"Battleground.AddObject: cannot create gameobject (entry: {entry}) " +
                    $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");
                return false;
            }

            // Add to world, so it can be later looked up from HashMapHolder
            if (!map.AddToMap(go))
                return false;

            BgObjects[type] = go.GetGUID();
            return true;
        }

        public bool AddObject(int type, int entry, Position pos, float rotation0, float rotation1, float rotation2, float rotation3, TimeSpan respawnTime = default, GameObjectState goState = GameObjectState.Ready)
        {
            return AddObject(type, entry, pos.GetPositionX(), pos.GetPositionY(), pos.GetPositionZ(), pos.GetOrientation(), rotation0, rotation1, rotation2, rotation3, respawnTime, goState);
        }

        // Some doors aren't despawned so we cannot handle their closing in gameobject.update()
        // It would be nice to correctly implement GO_ACTIVATED state and open/close doors in gameobject code
        public void DoorClose(int type)
        {
            GameObject obj = GetBgMap().GetGameObject(BgObjects[type]);
            if (obj != null)
            {
                // If doors are open, close it
                if (obj.GetLootState() == LootState.Activated && obj.GetGoState() != GameObjectState.Ready)
                {
                    obj.SetLootState(LootState.Ready);
                    obj.SetGoState(GameObjectState.Ready);
                }
            }
            else
            {
                Log.outError(LogFilter.Battleground,
                    $"Battleground.DoorClose: door gameobject (Type: {type}, {BgObjects[type]}) not found " +
                    $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");
            }
        }

        public void DoorOpen(int type)
        {
            GameObject obj = GetBgMap().GetGameObject(BgObjects[type]);
            if (obj != null)
            {
                obj.SetLootState(LootState.Activated);
                obj.SetGoState(GameObjectState.Active);
            }
            else
            {
                Log.outError(LogFilter.Battleground, 
                    $"Battleground.DoorOpen: door gameobject (Type: {type}, {BgObjects[type]}) not found " +
                    $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");
            }
        }

        public GameObject GetBGObject(int type)
        {
            if (BgObjects[type].IsEmpty())
                return null;

            GameObject obj = GetBgMap().GetGameObject(BgObjects[type]);
            if (obj == null)
            {
                Log.outError(LogFilter.Battleground, 
                    $"Battleground.GetBGObject: gameobject (type: {type}, {BgObjects[type]}) not found " +
                    $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");
            }

            return obj;
        }

        public Creature GetBGCreature(int type)
        {
            if (BgCreatures[type].IsEmpty())
                return null;

            Creature creature = GetBgMap().GetCreature(BgCreatures[type]);
            if (creature == null)
            {
                Log.outError(LogFilter.Battleground, 
                    $"Battleground.GetBGCreature: creature (type: {type}, {BgCreatures[type]}) not found " +
                    $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");
            }

            return creature;
        }

        public int GetMapId()
        {
            return _battlegroundTemplate.BattlemasterEntry.MapId[0];
        }

        public void SpawnBGObject(int type, TimeSpan respawntime)
        {
            Map map = FindBgMap();
            if (map != null)
            {
                GameObject obj = map.GetGameObject(BgObjects[type]);
                if (obj != null)
                {
                    if (respawntime != TimeSpan.Zero)
                    {
                        obj.SetLootState(LootState.JustDeactivated);
                        {
                            GameObjectOverride goOverride = obj.GetGameObjectOverride();
                            if (goOverride != null)
                                if (goOverride.Flags.HasFlag(GameObjectFlags.NoDespawn))
                                {
                                    // This function should be called in GameObject::Update() but in case of
                                    // GO_FLAG_NODESPAWN flag the function is never called, so we call it here
                                    obj.SendGameObjectDespawn();
                                }
                        }
                    }
                    else if (obj.GetLootState() == LootState.JustDeactivated)
                    {
                        // Change state from GO_JUST_DEACTIVATED to GO_READY in case battleground is starting again
                        obj.SetLootState(LootState.Ready);
                    }
                    obj.SetRespawnTime(respawntime);
                    map.AddToMap(obj);
                }
            }
        }

        public virtual Creature AddCreature(int entry, int type, float x, float y, float z, float o, int teamIndex = BattleGroundTeamId.Neutral, TimeSpan respawntime = default, Transport transport = null)
        {
            Map map = FindBgMap();
            if (map == null)
                return null;

            if (Global.ObjectMgr.GetCreatureTemplate(entry) == null)
            {
                Log.outError(LogFilter.Battleground, 
                    $"Battleground.AddCreature: creature template (entry: {entry}) does not exist " +
                    $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");
                return null;
            }


            if (transport != null)
            {
                Creature transCreature = transport.SummonPassenger(entry, new Position(x, y, z, o), TempSummonType.ManualDespawn);
                if (transCreature != null)
                {
                    BgCreatures[type] = transCreature.GetGUID();
                    return transCreature;
                }

                return null;
            }

            Position pos = new(x, y, z, o);

            Creature creature = Creature.CreateCreature(entry, map, pos);
            if (creature == null)
            {
                Log.outError(LogFilter.Battleground, 
                    $"Battleground.AddCreature: cannot create creature (entry: {entry}) " +
                    $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");
                return null;
            }

            creature.SetHomePosition(pos);

            if (!map.AddToMap(creature))
                return null;

            BgCreatures[type] = creature.GetGUID();

            if (respawntime != TimeSpan.Zero)
                creature.SetRespawnDelay(respawntime);

            return creature;
        }

        public Creature AddCreature(int entry, int type, Position pos, int teamIndex = BattleGroundTeamId.Neutral, TimeSpan respawntime = default, Transport transport = null)
        {
            return AddCreature(entry, type, pos.GetPositionX(), pos.GetPositionY(), pos.GetPositionZ(), pos.GetOrientation(), teamIndex, respawntime, transport);
        }

        public bool DelCreature(int type)
        {
            if (BgCreatures[type].IsEmpty())
                return true;

            Creature creature = GetBgMap().GetCreature(BgCreatures[type]);
            if (creature != null)
            {
                creature.AddObjectToRemoveList();
                BgCreatures[type].Clear();
                return true;
            }

            Log.outError(LogFilter.Battleground, 
                $"Battleground.DelCreature: creature (Type: {type}, {BgCreatures[type]}) not found " +
                $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");

            BgCreatures[type].Clear();
            return false;
        }

        public bool DelObject(int type)
        {
            if (BgObjects[type].IsEmpty())
                return true;

            GameObject obj = GetBgMap().GetGameObject(BgObjects[type]);
            if (obj != null)
            {
                obj.SetRespawnTime(TimeSpan.Zero);                                 // not save respawn time
                obj.Delete();
                BgObjects[type].Clear();
                return true;
            }
            Log.outError(LogFilter.Battleground, 
                $"Battleground.DelObject: gameobject (Type: {type}, {BgObjects[type]}) not found " +
                $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");

            BgObjects[type].Clear();
            return false;
        }

        bool RemoveObjectFromWorld(uint type)
        {
            if (BgObjects[type].IsEmpty())
                return true;

            GameObject obj = GetBgMap().GetGameObject(BgObjects[type]);
            if (obj != null)
            {
                obj.RemoveFromWorld();
                BgObjects[type].Clear();
                return true;
            }

            Log.outInfo(LogFilter.Battleground, 
                $"Battleground::RemoveObjectFromWorld: gameobject (Type: {type}, {BgObjects[type]}) not found " +
                $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");

            return false;
        }

        public bool AddSpiritGuide(int type, float x, float y, float z, float o, int teamIndex)
        {
            int entry = (int)(teamIndex == BattleGroundTeamId.Alliance ? BattlegroundCreatures.A_SpiritGuide : BattlegroundCreatures.H_SpiritGuide);

            if (AddCreature(entry, type, x, y, z, o) != null)
                return true;

            Log.outError(LogFilter.Battleground, 
                $"Battleground.AddSpiritGuide: cannot create spirit guide (type: {type}, entry: {entry}) " +
                $"for BG (map: {GetMapId()}, instance id: {m_InstanceID})!");

            EndNow();
            return false;
        }

        public bool AddSpiritGuide(int type, Position pos, int teamIndex = BattleGroundTeamId.Neutral)
        {
            return AddSpiritGuide(type, pos.GetPositionX(), pos.GetPositionY(), pos.GetPositionZ(), pos.GetOrientation(), teamIndex);
        }

        public void SendMessageToAll(CypherStrings entry, ChatMsg msgType, Player source = null)
        {
            if (entry == 0)
                return;

            CypherStringChatBuilder builder = new(null, msgType, entry, source);
            LocalizedDo localizer = new(builder);
            BroadcastWorker(localizer);
        }

        public void SendMessageToAll(CypherStrings entry, ChatMsg msgType, Player source, params object[] args)
        {
            if (entry == 0)
                return;

            CypherStringChatBuilder builder = new(null, msgType, entry, source, args);
            LocalizedDo localizer = new(builder);
            BroadcastWorker(localizer);
        }

        public void AddPlayerPosition(BattlegroundPlayerPosition position)
        {
            _playerPositions.Add(position);
        }

        public void RemovePlayerPosition(ObjectGuid guid)
        {
            _playerPositions.RemoveAll(playerPosition => playerPosition.Guid == guid);
        }
        
        void EndNow()
        {
            RemoveFromBGFreeSlotQueue();
            SetStatus(BattlegroundStatus.WaitLeave);
            SetRemainingTime(TimeSpan.Zero);
        }

        public virtual void HandleKillPlayer(Player victim, Player killer)
        {
            // Keep in mind that for arena this will have to be changed a bit

            // Add +1 deaths
            UpdatePlayerScore(victim, ScoreType.Deaths, 1);
            // Add +1 kills to group and +1 killing_blows to killer
            if (killer != null)
            {
                // Don't reward credit for killing ourselves, like fall damage of hellfire (warlock)
                if (killer == victim)
                    return;

                Team killerTeam = GetPlayerTeam(killer.GetGUID());

                UpdatePlayerScore(killer, ScoreType.HonorableKills, 1);
                UpdatePlayerScore(killer, ScoreType.KillingBlows, 1);

                foreach (var (guid, player) in m_Players)
                {
                    Player creditedPlayer = Global.ObjAccessor.FindPlayer(guid);
                    if (creditedPlayer == null || creditedPlayer == killer)
                        continue;

                    if (player.Team == killerTeam && creditedPlayer.IsAtGroupRewardDistance(victim))
                        UpdatePlayerScore(creditedPlayer, ScoreType.HonorableKills, 1);
                }
            }

            if (!IsArena())
            {
                // To be able to remove insignia -- ONLY IN Battlegrounds
                victim.SetUnitFlag(UnitFlags.Skinnable);
                RewardXPAtKill(killer, victim);
            }
        }

        public virtual void HandleKillUnit(Creature creature, Unit killer) { }

        // Return the player's team based on Battlegroundplayer info
        // Used in same faction arena matches mainly
        public Team GetPlayerTeam(ObjectGuid guid)
        {
            var player = m_Players.LookupByKey(guid);
            if (player != null)
                return player.Team;
            return Team.Other;
        }

        public Team GetOtherTeam(Team team)
        {
            return team != 0 ? ((team == Team.Alliance) ? Team.Horde : Team.Alliance) : Team.Other;
        }

        public bool IsPlayerInBattleground(ObjectGuid guid)
        {
            return m_Players.ContainsKey(guid);
        }

        public bool IsPlayerMercenaryInBattleground(ObjectGuid guid)
        {
            var player = m_Players.LookupByKey(guid);
            if (player != null)
                return player.Mercenary;

            return false;
        }
        
        void PlayerAddedToBGCheckIfBGIsRunning(Player player)
        {
            if (GetStatus() != BattlegroundStatus.WaitLeave)
                return;

            BlockMovement(player);

            PVPMatchStatisticsMessage pvpMatchStatistics = new();
            BuildPvPLogDataPacket(out pvpMatchStatistics.Data);
            player.SendPacket(pvpMatchStatistics);
        }

        public uint GetAlivePlayersCountByTeam(Team team)
        {
            uint count = 0;
            foreach (var pair in m_Players)
            {
                if (pair.Value.Team == team)
                {
                    Player player = Global.ObjAccessor.FindPlayer(pair.Key);
                    if (player != null && player.IsAlive())
                        ++count;
                }
            }
            return count;
        }

        public int GetObjectType(ObjectGuid guid)
        {
            for (int i = 0; i < BgObjects.Length; ++i)
                if (BgObjects[i] == guid)
                    return i;
            Log.outError(LogFilter.Battleground, 
                $"Battleground.GetObjectType: player used gameobject ({guid}) which is not in internal data " +
                $"for BG (map: {GetMapId()}, instance id: {m_InstanceID}), cheating?");

            return -1;
        }

        public void SetBgRaid(Team team, Group bg_raid)
        {
            Group old_raid = m_BgRaids[GetTeamIndexByTeamId(team)];
            if (old_raid != null)
                old_raid.SetBattlegroundGroup(null);
            if (bg_raid != null)
                bg_raid.SetBattlegroundGroup(this);
            m_BgRaids[GetTeamIndexByTeamId(team)] = bg_raid;
        }

        public virtual WorldSafeLocsEntry GetClosestGraveyard(Player player)
        {
            return Global.ObjectMgr.GetClosestGraveyard(player, GetPlayerTeam(player.GetGUID()), player);
        }

        public override void TriggerGameEvent(int gameEventId, WorldObject source = null, WorldObject target = null)
        {
            ProcessEvent(target, gameEventId, source);
            GameEvents.TriggerForMap(gameEventId, GetBgMap(), source, target);
            foreach (var guid in GetPlayers().Keys)
            {
                Player player = Global.ObjAccessor.FindPlayer(guid);
                if (player != null)
                    GameEvents.TriggerForPlayer(gameEventId, player);
            }
        }

        public void SetBracket(PvpDifficultyRecord bracketEntry)
        {
            _pvpDifficultyEntry = bracketEntry;
        }

        void RewardXPAtKill(Player killer, Player victim)
        {
            if (WorldConfig.Values[WorldCfg.BgXpForKill].Bool && killer != null && victim != null)
                new KillRewarder([killer], victim, true).Reward();
        }

        public int GetTeamScore(int teamIndex)
        {
            if (teamIndex == BattleGroundTeamId.Alliance || teamIndex == BattleGroundTeamId.Horde)
                return m_TeamScores[teamIndex];

            Log.outError(LogFilter.Battleground, 
                $"GetTeamScore with wrong Team {teamIndex} for BG {GetTypeID()}");

            return 0;
        }

        public virtual void HandleAreaTrigger(Player player, int trigger, bool entered)
        {
            Log.outDebug(LogFilter.Battleground, 
                $"Unhandled AreaTrigger {trigger} in Battleground {player.GetMapId()}. " +
                $"Player coords (x: {player.GetPositionX()}, y: {player.GetPositionY()}, z: {player.GetPositionZ()})");
        }

        public virtual bool SetupBattleground()
        {
            return true;
        }

        public string GetName()
        {
            return _battlegroundTemplate.BattlemasterEntry.Name[Global.WorldMgr.GetDefaultDbcLocale()];
        }

        public BattlegroundTypeId GetTypeID()
        {
            return _battlegroundTemplate.Id;
        }

        public BattlegroundBracketId GetBracketId()
        {
            return _pvpDifficultyEntry.BracketId;
        }

        byte GetUniqueBracketId()
        {
            return (byte)((GetMinLevel() / 5) - 1); // 10 - 1, 15 - 2, 20 - 3, etc.
        }

        int GetMaxPlayers()
        {
            return GetMaxPlayersPerTeam() * 2;
        }

        int GetMinPlayers()
        {
            return GetMinPlayersPerTeam() * 2;
        }

        public int GetMinLevel()
        {
            if (_pvpDifficultyEntry != null)
                return _pvpDifficultyEntry.MinLevel;

            return _battlegroundTemplate.GetMinLevel();
        }

        public int GetMaxLevel()
        {
            if (_pvpDifficultyEntry != null)
                return _pvpDifficultyEntry.MaxLevel;

            return _battlegroundTemplate.GetMaxLevel();
        }

        public int GetMaxPlayersPerTeam()
        {
            if (IsArena())
            {
                switch (GetArenaType())
                {
                    case ArenaTypes.Team2v2:
                        return 2;
                    case ArenaTypes.Team3v3:
                        return 3;
                    case ArenaTypes.Team5v5: // removed
                        return 5;
                    default:
                        break;
                }
            }

            return _battlegroundTemplate.GetMaxPlayersPerTeam();
        }

        public int GetMinPlayersPerTeam()
        {
            return _battlegroundTemplate.GetMinPlayersPerTeam();
        }

        public BattlegroundPlayer GetBattlegroundPlayerData(ObjectGuid playerGuid)
        {
            return m_Players.LookupByKey(playerGuid);
        }
        
        public virtual void StartingEventCloseDoors() { }
        public virtual void StartingEventOpenDoors() { }

        public virtual void DestroyGate(Player player, GameObject go) { }

        public int GetInstanceID() { return m_InstanceID; }
        public BattlegroundStatus GetStatus() { return m_Status; }
        public int GetClientInstanceID() { return m_ClientInstanceID; }
        public TimeSpan GetElapsedTime() { return m_StartTime; }
        public TimeSpan GetRemainingTime() { return m_EndTime; }

        TimeSpan GetStartDelayTime() { return m_StartDelayTime; }
        public ArenaTypes GetArenaType() { return m_ArenaType; }
        PvPTeamId GetWinner() { return _winnerTeamId; }

        //here we can count minlevel and maxlevel for players
        public void SetInstanceID(int InstanceID) { m_InstanceID = InstanceID; }
        public void SetStatus(BattlegroundStatus Status) { m_Status = Status; }
        public void SetClientInstanceID(int InstanceID) { m_ClientInstanceID = InstanceID; }
        public void SetElapsedTime(TimeSpan Time) { m_StartTime = Time; }
        public void SetRemainingTime(TimeSpan Time) { m_EndTime = Time; }
        public void SetRated(bool state) { m_IsRated = state; }
        public void SetArenaType(ArenaTypes type) { m_ArenaType = type; }
        public void SetWinner(PvPTeamId winnerTeamId) { _winnerTeamId = winnerTeamId; }

        void ModifyStartDelayTime(TimeSpan diff) { m_StartDelayTime -= diff; }
        void SetStartDelayTime(TimeSpan Time) { m_StartDelayTime = Time; }

        public void DecreaseInvitedCount(Team team)
        {
            if (team == Team.Alliance)
                --m_InvitedAlliance;
            else
                --m_InvitedHorde;
        }

        public void IncreaseInvitedCount(Team team)
        {
            if (team == Team.Alliance)
                ++m_InvitedAlliance;
            else
                ++m_InvitedHorde;
        }

        int GetInvitedCount(Team team) { return (team == Team.Alliance) ? m_InvitedAlliance : m_InvitedHorde; }

        public bool IsRated() { return m_IsRated; }

        public Dictionary<ObjectGuid, BattlegroundPlayer> GetPlayers() { return m_Players; }
        int GetPlayersSize() { return m_Players.Count; }
        int GetPlayerScoresSize() { return PlayerScores.Count; }

        public void SetBgMap(BattlegroundMap map) { m_Map = map; }
        public BattlegroundMap FindBgMap() { return m_Map; }

        Group GetBgRaid(Team team) { return m_BgRaids[GetTeamIndexByTeamId(team)]; }

        public static int GetTeamIndexByTeamId(Team team) { return team == Team.Alliance ? BattleGroundTeamId.Alliance : BattleGroundTeamId.Horde; }
        public int GetPlayersCountByTeam(Team team) { return m_PlayersCount[GetTeamIndexByTeamId(team)]; }
        
        void UpdatePlayersCountByTeam(Team team, bool remove)
        {
            if (remove)
                --m_PlayersCount[GetTeamIndexByTeamId(team)];
            else
                ++m_PlayersCount[GetTeamIndexByTeamId(team)];
        }

        public virtual void CheckWinConditions() { }

        public void SetArenaTeamIdForTeam(Team team, int ArenaTeamId) { m_ArenaTeamIds[GetTeamIndexByTeamId(team)] = ArenaTeamId; }
        public int GetArenaTeamIdForTeam(Team team) { return m_ArenaTeamIds[GetTeamIndexByTeamId(team)]; }
        public int GetArenaTeamIdByIndex(int index) { return m_ArenaTeamIds[index]; }
        public void SetArenaMatchmakerRating(Team team, int MMR) { m_ArenaTeamMMR[GetTeamIndexByTeamId(team)] = MMR; }
        public int GetArenaMatchmakerRating(Team team) { return m_ArenaTeamMMR[GetTeamIndexByTeamId(team)]; }

        // Battleground events
        public virtual void EventPlayerDroppedFlag(Player player) { }
        public virtual void EventPlayerClickedOnFlag(Player player, GameObject target_obj) { }

        public override void ProcessEvent(WorldObject obj, int eventId, WorldObject invoker = null) { }

        public virtual void HandlePlayerResurrect(Player player) { }

        public virtual WorldSafeLocsEntry GetExploitTeleportLocation(Team team) { return null; }

        public virtual bool HandlePlayerUnderMap(Player player) { return false; }

        public bool ToBeDeleted() { return m_SetDeleteThis; }
        void SetDeleteThis() { m_SetDeleteThis = true; }

        bool CanAwardArenaPoints() { return GetMinLevel() >= 71; }

        public virtual ObjectGuid GetFlagPickerGUID(int teamIndex = -1) { return ObjectGuid.Empty; }
        public virtual void SetDroppedFlagGUID(ObjectGuid guid, int teamIndex = -1) { }
        public virtual void HandleQuestComplete(int questid, Player player) { }
        public virtual bool CanActivateGO(int entry, Team team) { return true; }
        public virtual bool IsSpellAllowed(int spellId, Player player) { return true; }

        public virtual void RemovePlayer(Player player, ObjectGuid guid, Team team) { }

        public virtual bool PreUpdateImpl(TimeSpan diff) { return true; }

        public virtual void PostUpdateImpl(TimeSpan diff) { }

        void BroadcastWorker(IDoWork<Player> _do)
        {
            foreach (var pair in m_Players)
            {
                Player player = _GetPlayer(pair, "BroadcastWorker");
                if (player != null)
                    _do.Invoke(player);
            }
        }

        #region Fields
        protected Dictionary<ObjectGuid, BattlegroundScore> PlayerScores = new();                // Player scores
        // Player lists, those need to be accessible by inherited classes
        Dictionary<ObjectGuid, BattlegroundPlayer> m_Players = new();

        // these are important variables used for starting messages
        BattlegroundEventFlags m_Events;
        public TimeSpan[] StartDelayTimes = new TimeSpan[4];
        // this must be filled inructors!
        public int[] StartMessageIds = new int[4];

        public int[] m_TeamScores = new int[SharedConst.PvpTeamsCount];

        protected ObjectGuid[] BgObjects;// = new Dictionary<int, ObjectGuid>();
        protected ObjectGuid[] BgCreatures;// = new Dictionary<int, ObjectGuid>();

        public int[] Buff_Entries = [BattlegroundConst.SpeedBuff, BattlegroundConst.RegenBuff, BattlegroundConst.BerserkerBuff];

        // Battleground
        int m_InstanceID;                                // Battleground Instance's GUID!
        BattlegroundStatus m_Status;
        int m_ClientInstanceID;                          // the instance-id which is sent to the client and without any other internal use
        TimeSpan m_StartTime;
        TimeSpan m_ResetStatTimer;
        TimeSpan m_ValidStartPositionTimer;
        TimeSpan m_EndTime;                                    // it is set to 120000 when bg is ending and it decreases itself
        ArenaTypes m_ArenaType;                                 // 2=2v2, 3=3v3, 5=5v5
        bool m_InBGFreeSlotQueue;                         // used to make sure that BG is only once inserted into the BattlegroundMgr.BGFreeSlotQueue[bgTypeId] deque
        bool m_SetDeleteThis;                             // used for safe deletion of the bg after end / all players leave
        PvPTeamId _winnerTeamId;
        TimeSpan m_StartDelayTime;
        bool m_IsRated;                                   // is this battle rated?
        bool m_PrematureCountDown;
        TimeSpan m_PrematureCountDownTimer;
        TimeSpan m_LastPlayerPositionBroadcast;

        // Player lists
        List<ObjectGuid> m_OfflineQueue = new();                  // Player GUID

        // Invited counters are useful for player invitation to BG - do not allow, if BG is started to one faction to have 2 more players than another faction
        // Invited counters will be changed only when removing already invited player from queue, removing player from Battleground and inviting player to BG
        // Invited players counters
        int m_InvitedAlliance;
        int m_InvitedHorde;

        // Raid Group
        Group[] m_BgRaids = new Group[SharedConst.PvpTeamsCount];                   // 0 - Team.Alliance, 1 - Team.Horde

        // Players count by team
        int[] m_PlayersCount = new int[SharedConst.PvpTeamsCount];

        // Arena team ids by team
        int[] m_ArenaTeamIds = new int[SharedConst.PvpTeamsCount];

        int[] m_ArenaTeamMMR = new int[SharedConst.PvpTeamsCount];

        // Start location
        BattlegroundMap m_Map;

        BattlegroundTemplate _battlegroundTemplate;
        PvpDifficultyRecord _pvpDifficultyEntry;

        List<BattlegroundPlayerPosition> _playerPositions = new();

        // Time when the first message "the battle will begin in 2minutes" is send (or 1m for arenas)
        ServerTime _preparationStartTime;
        #endregion
    }

    public class BattlegroundPlayer
    {
        public ServerTime OfflineRemoveTime;  // for tracking and removing offline players from queue after 5 Time.Minutes
        public Team Team;               // Player's team
        public bool Mercenary;
        public BattlegroundQueueTypeId queueTypeId;
    }
}
