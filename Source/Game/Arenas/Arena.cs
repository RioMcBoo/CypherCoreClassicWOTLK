﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.BattleGrounds;
using Game.Entities;
using Game.Guilds;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;

namespace Game.Arenas
{
    public class Arena : Battleground
    {
        protected TaskScheduler taskScheduler = new();

        public Arena(BattlegroundTemplate battlegroundTemplate) : base(battlegroundTemplate)
        {
            StartDelayTimes[BattlegroundConst.EventIdFirst] = (Minutes)1;
            StartDelayTimes[BattlegroundConst.EventIdSecond] = (Seconds)30;
            StartDelayTimes[BattlegroundConst.EventIdThird] = (Seconds)15;
            StartDelayTimes[BattlegroundConst.EventIdFourth] = TimeSpan.Zero;

            StartMessageIds[BattlegroundConst.EventIdFirst] = ArenaBroadcastTexts.OneMinute;
            StartMessageIds[BattlegroundConst.EventIdSecond] = ArenaBroadcastTexts.ThirtySeconds;
            StartMessageIds[BattlegroundConst.EventIdThird] = ArenaBroadcastTexts.FifteenSeconds;
            StartMessageIds[BattlegroundConst.EventIdFourth] = ArenaBroadcastTexts.HasBegun;
        }

        public override void AddPlayer(Player player, BattlegroundQueueTypeId queueId)
        {
            bool isInBattleground = IsPlayerInBattleground(player.GetGUID());
            base.AddPlayer(player, queueId);
            if (!isInBattleground)
                PlayerScores[player.GetGUID()] = new ArenaScore(player.GetGUID(), player.GetBGTeam());

            if (player.GetBGTeam() == Team.Alliance)        // gold
            {
                if (player.GetEffectiveTeam() == Team.Horde)
                    player.CastSpell(player, ArenaSpellIds.HordeGoldFlag, true);
                else
                    player.CastSpell(player, ArenaSpellIds.AllianceGoldFlag, true);
            }
            else                                        // green
            {
                if (player.GetEffectiveTeam() == Team.Horde)
                    player.CastSpell(player, ArenaSpellIds.HordeGreenFlag, true);
                else
                    player.CastSpell(player, ArenaSpellIds.AllianceGreenFlag, true);
            }

            UpdateArenaWorldState();
        }

        public override void RemovePlayer(Player player, ObjectGuid guid, Team team)
        {
            if (GetStatus() == BattlegroundStatus.WaitLeave)
                return;

            UpdateArenaWorldState();
            CheckWinConditions();
        }

        void UpdateArenaWorldState()
        {
            UpdateWorldState(ArenaWorldStates.AlivePlayersGreen, (int)GetAlivePlayersCountByTeam(Team.Horde));
            UpdateWorldState(ArenaWorldStates.AlivePlayersGold, (int)GetAlivePlayersCountByTeam(Team.Alliance));
        }

        public override void HandleKillPlayer(Player victim, Player killer)
        {
            if (GetStatus() != BattlegroundStatus.InProgress)
                return;

            base.HandleKillPlayer(victim, killer);

            UpdateArenaWorldState();
            CheckWinConditions();
        }

        public override void BuildPvPLogDataPacket(out PVPMatchStatistics pvpLogData)
        {
            base.BuildPvPLogDataPacket(out pvpLogData);

            if (IsRated())
            {
                pvpLogData.Ratings = new();
                for (byte i = 0; i < SharedConst.PvpTeamsCount; ++i)
                {
                    pvpLogData.Ratings.Postmatch[i] = _arenaTeamScores[i].PostMatchRating;
                    pvpLogData.Ratings.Prematch[i] = _arenaTeamScores[i].PreMatchRating;
                    pvpLogData.Ratings.PrematchMMR[i] = _arenaTeamScores[i].PreMatchMMR;
                }
            }
        }

        public override void RemovePlayerAtLeave(ObjectGuid guid, bool Transport, bool SendPacket)
        {
            if (IsRated() && GetStatus() == BattlegroundStatus.InProgress)
            {
                var bgPlayer = GetPlayers().LookupByKey(guid);
                if (bgPlayer != null) // check if the player was a participant of the match, or only entered through gm command (appear)
                {
                    // if the player was a match participant, calculate rating

                    ArenaTeam winnerArenaTeam = Global.ArenaTeamMgr.GetArenaTeamById(GetArenaTeamIdForTeam(GetOtherTeam(bgPlayer.Team)));
                    ArenaTeam loserArenaTeam = Global.ArenaTeamMgr.GetArenaTeamById(GetArenaTeamIdForTeam(bgPlayer.Team));

                    // left a rated match while the encounter was in progress, consider as loser
                    if (winnerArenaTeam != null && loserArenaTeam != null && winnerArenaTeam != loserArenaTeam)
                    {
                        Player player = _GetPlayer(guid, bgPlayer.OfflineRemoveTime != ServerTime.Zero, "Arena.RemovePlayerAtLeave");
                        if (player != null)
                            loserArenaTeam.MemberLost(player, GetArenaMatchmakerRating(GetOtherTeam(bgPlayer.Team)));
                        else
                            loserArenaTeam.OfflineMemberLost(guid, GetArenaMatchmakerRating(GetOtherTeam(bgPlayer.Team)));
                    }
                }
            }

            // remove player
            base.RemovePlayerAtLeave(guid, Transport, SendPacket);
        }

        public override void CheckWinConditions()
        {
            if (GetAlivePlayersCountByTeam(Team.Alliance) == 0 && GetPlayersCountByTeam(Team.Horde) != 0)
                EndBattleground(Team.Horde);
            else if (GetPlayersCountByTeam(Team.Alliance) != 0 && GetAlivePlayersCountByTeam(Team.Horde) == 0)
                EndBattleground(Team.Alliance);
        }

        public override void EndBattleground(Team winner)
        {
            // arena rating calculation
            if (IsRated())
            {
                int loserTeamRating;
                int loserMatchmakerRating;
                int loserChange = 0;
                int loserMatchmakerChange = 0;
                int winnerTeamRating;
                int winnerMatchmakerRating;
                int winnerChange = 0;
                int winnerMatchmakerChange = 0;
                bool guildAwarded = false;

                // In case of arena draw, follow this logic:
                // winnerArenaTeam => ALLIANCE, loserArenaTeam => HORDE
                ArenaTeam winnerArenaTeam = Global.ArenaTeamMgr.GetArenaTeamById(GetArenaTeamIdForTeam(winner == Team.Other ? Team.Alliance : winner));
                ArenaTeam loserArenaTeam = Global.ArenaTeamMgr.GetArenaTeamById(GetArenaTeamIdForTeam(winner == Team.Other ? Team.Horde : GetOtherTeam(winner)));

                if (winnerArenaTeam != null && loserArenaTeam != null && winnerArenaTeam != loserArenaTeam)
                {
                    // In case of arena draw, follow this logic:
                    // winnerMatchmakerRating => ALLIANCE, loserMatchmakerRating => HORDE
                    loserTeamRating = loserArenaTeam.GetRating();
                    loserMatchmakerRating = GetArenaMatchmakerRating(winner == Team.Other ? Team.Horde : GetOtherTeam(winner));
                    winnerTeamRating = winnerArenaTeam.GetRating();
                    winnerMatchmakerRating = GetArenaMatchmakerRating(winner == Team.Other ? Team.Alliance : winner);

                    if (winner != 0)
                    {
                        winnerMatchmakerChange = winnerArenaTeam.WonAgainst(winnerMatchmakerRating, loserMatchmakerRating, ref winnerChange);
                        loserMatchmakerChange = loserArenaTeam.LostAgainst(loserMatchmakerRating, winnerMatchmakerRating, ref loserChange);

                        Log.outDebug(LogFilter.Arena, 
                            $"match Type: {GetArenaType()}" +
                            " --- " +
                            $"Winner: old rating: {winnerTeamRating}, " +
                            $"rating gain: {winnerChange}, old MMR: {winnerMatchmakerRating}, " +
                            $"MMR gain: {winnerMatchmakerChange} --- Loser: old rating: {loserTeamRating}, " +
                            $"rating loss: {loserChange}, old MMR: {loserMatchmakerRating}, MMR loss: {loserMatchmakerChange}" +
                            " --- ");

                        SetArenaMatchmakerRating(winner, winnerMatchmakerRating + winnerMatchmakerChange);
                        SetArenaMatchmakerRating(GetOtherTeam(winner), loserMatchmakerRating + loserMatchmakerChange);

                        // bg team that the client expects is different to TeamId
                        // alliance 1, horde 0
                        byte winnerTeam = (byte)(winner == Team.Alliance ? PvPTeamId.Alliance : PvPTeamId.Horde);
                        byte loserTeam = (byte)(winner == Team.Alliance ? PvPTeamId.Horde : PvPTeamId.Alliance);

                        _arenaTeamScores[winnerTeam].Assign(winnerTeamRating, winnerTeamRating + winnerChange, winnerMatchmakerRating, GetArenaMatchmakerRating(winner));
                        _arenaTeamScores[loserTeam].Assign(loserTeamRating, loserTeamRating + loserChange, loserMatchmakerRating, GetArenaMatchmakerRating(GetOtherTeam(winner)));

                        Log.outDebug(LogFilter.Arena, 
                            $"Arena match Type: {GetArenaType()} for " +
                            $"Team1Id: {GetArenaTeamIdByIndex(BattleGroundTeamId.Alliance)} - Team2Id: {GetArenaTeamIdByIndex(BattleGroundTeamId.Horde)} ended. " +
                            $"WinnerTeamId: {winnerArenaTeam.GetId()}. Winner rating: +{winnerChange}, Loser rating: {loserChange}");

                        if (WorldConfig.Values[WorldCfg.ArenaLogExtendedInfo].Bool)
                        {
                            foreach (var score in PlayerScores)
                            {
                                Player player = Global.ObjAccessor.FindPlayer(score.Key);
                                if (player != null)
                                {
                                    int team = player.GetArenaTeamId((byte)(GetArenaType() == ArenaTypes.Team5v5 ? 2 : (GetArenaType() == ArenaTypes.Team3v3 ? 1 : 0)));
                                    Log.outDebug(LogFilter.Arena, 
                                        $"Statistics match Type: {GetArenaType()} for {player.GetName()} (GUID: {score.Key}, Team: {team}, " +
                                        $"IP: {player.GetSession().GetRemoteAddress()}): {score.Value}");
                                }
                            }
                        }
                    }
                    // Deduct 16 points from each teams arena-rating if there are no winners after 45+2 minutes
                    else
                    {
                        _arenaTeamScores[(int)PvPTeamId.Alliance].Assign(winnerTeamRating, winnerTeamRating + SharedConst.ArenaTimeLimitPointsLoss, winnerMatchmakerRating, GetArenaMatchmakerRating(Team.Alliance));
                        _arenaTeamScores[(int)PvPTeamId.Horde].Assign(loserTeamRating, loserTeamRating + SharedConst.ArenaTimeLimitPointsLoss, loserMatchmakerRating, GetArenaMatchmakerRating(Team.Horde));

                        winnerArenaTeam.FinishGame(SharedConst.ArenaTimeLimitPointsLoss);
                        loserArenaTeam.FinishGame(SharedConst.ArenaTimeLimitPointsLoss);
                    }

                    uint aliveWinners = GetAlivePlayersCountByTeam(winner);
                    foreach (var pair in GetPlayers())
                    {
                        Team team = pair.Value.Team;

                        if (pair.Value.OfflineRemoveTime != ServerTime.Zero)
                        {
                            // if rated arena match - make member lost!
                            if (team == winner)
                                winnerArenaTeam.OfflineMemberLost(pair.Key, loserMatchmakerRating, winnerMatchmakerChange);
                            else
                            {
                                if (winner == 0)
                                    winnerArenaTeam.OfflineMemberLost(pair.Key, loserMatchmakerRating, winnerMatchmakerChange);

                                loserArenaTeam.OfflineMemberLost(pair.Key, winnerMatchmakerRating, loserMatchmakerChange);
                            }
                            continue;
                        }

                        Player player = _GetPlayer(pair.Key, pair.Value.OfflineRemoveTime != ServerTime.Zero, "Arena.EndBattleground");
                        if (player == null)
                            continue;

                        // per player calculation
                        if (team == winner)
                        {
                            // update achievement BEFORE personal rating update
                            int rating = player.GetArenaPersonalRating(winnerArenaTeam.GetSlot());
                            player.StartCriteria(CriteriaStartEvent.WinRankedArenaMatchWithTeamSize, 0);
                            player.UpdateCriteria(CriteriaType.WinAnyRankedArena, rating != 0 ? rating : 1);
                            player.UpdateCriteria(CriteriaType.WinArena, GetMapId());

                            // Last standing - Rated 5v5 arena & be solely alive player
                            if (GetArenaType() == ArenaTypes.Team5v5 && aliveWinners == 1 && player.IsAlive())
                                player.CastSpell(player, ArenaSpellIds.LastManStanding, true);

                            if (!guildAwarded)
                            {
                                guildAwarded = true;
                                long guildId = GetBgMap().GetOwnerGuildId(player.GetBGTeam());
                                if (guildId != 0)
                                {
                                    Guild guild = Global.GuildMgr.GetGuildById(guildId);
                                    if (guild != null)
                                        guild.UpdateCriteria(CriteriaType.WinAnyRankedArena, Math.Max(winnerArenaTeam.GetRating(), 1), 0, 0, null, player);
                                }
                            }

                            winnerArenaTeam.MemberWon(player, loserMatchmakerRating, winnerMatchmakerChange);
                        }
                        else
                        {
                            if (winner == 0)
                                winnerArenaTeam.MemberLost(player, loserMatchmakerRating, winnerMatchmakerChange);

                            loserArenaTeam.MemberLost(player, winnerMatchmakerRating, loserMatchmakerChange);

                            // Arena lost => reset the win_rated_arena having the "no_lose" condition
                            player.FailCriteria(CriteriaFailEvent.LoseRankedArenaMatchWithTeamSize, 0);
                        }
                    }

                    // save the stat changes
                    winnerArenaTeam.SaveToDB();
                    loserArenaTeam.SaveToDB();
                    // send updated arena team stats to players
                    // this way all arena team members will get notified, not only the ones who participated in this match
                    winnerArenaTeam.NotifyStatsChanged();
                    loserArenaTeam.NotifyStatsChanged();
                }
            }

            // end Battleground
            base.EndBattleground(winner);
        }

        public ArenaTeamScore[] _arenaTeamScores = new ArenaTeamScore[SharedConst.PvpTeamsCount];
    }

    struct ArenaWorldStates
    {
        public const int AlivePlayersGreen = 3600;
        public const int AlivePlayersGold = 3601;
        public const int ShowAlivePlayers = 3610;
        public const int TimeRemaining = 8529;
        public const int ShowTimeRemaining = 8524;
        public const int GreenTeamExtraLives = 15480;
        public const int GoldTeamExtraLives = 15481;
        public const int ShowExtraLives = 13401;
        public const int SoloShuffleRound = 21427;
        public const int ShowSoloShuffleRound = 21322;
    }
}
