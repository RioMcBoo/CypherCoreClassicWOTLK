﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;

namespace Game.BattleGrounds.Zones.AlteracValley
{
    class BattlegroundAVScore : BattlegroundScore
    {
        public BattlegroundAVScore(ObjectGuid playerGuid, Team team) : base(playerGuid, team)
        {
            GraveyardsAssaulted = 0;
            GraveyardsDefended = 0;
            TowersAssaulted = 0;
            TowersDefended = 0;
            MinesCaptured = 0;
        }

        public override void UpdateScore(ScoreType type, int value)
        {
            switch (type)
            {
                case ScoreType.GraveyardsAssaulted:
                    GraveyardsAssaulted += value;
                    break;
                case ScoreType.GraveyardsDefended:
                    GraveyardsDefended += value;
                    break;
                case ScoreType.TowersAssaulted:
                    TowersAssaulted += value;
                    break;
                case ScoreType.TowersDefended:
                    TowersDefended += value;
                    break;
                case ScoreType.MinesCaptured:
                    MinesCaptured += value;
                    break;
                default:
                    base.UpdateScore(type, value);
                    break;
            }
        }

        public override void BuildPvPLogPlayerDataPacket(out PVPMatchStatistics.PVPMatchPlayerStatistics playerData)
        {
            base.BuildPvPLogPlayerDataPacket(out playerData);

            playerData.Stats.Add(new PVPMatchStatistics.PVPMatchPlayerPVPStat(AVObjectives.AssaultGraveyard, GraveyardsAssaulted));
            playerData.Stats.Add(new PVPMatchStatistics.PVPMatchPlayerPVPStat(AVObjectives.DefendGraveyard, GraveyardsDefended));
            playerData.Stats.Add(new PVPMatchStatistics.PVPMatchPlayerPVPStat(AVObjectives.AssaultTower, TowersAssaulted));
            playerData.Stats.Add(new PVPMatchStatistics.PVPMatchPlayerPVPStat(AVObjectives.DefendTower, TowersDefended));
            playerData.Stats.Add(new PVPMatchStatistics.PVPMatchPlayerPVPStat(AVObjectives.SecondaryObjective, MinesCaptured));
        }

        public override int GetAttr1() { return GraveyardsAssaulted; }
        public override int GetAttr2() { return GraveyardsDefended; }
        public override int GetAttr3() { return TowersAssaulted; }
        public override int GetAttr4() { return TowersDefended; }
        public override int GetAttr5() { return MinesCaptured; }

        int GraveyardsAssaulted;
        int GraveyardsDefended;
        int TowersAssaulted;
        int TowersDefended;
        int MinesCaptured;
    }

    class BgAlteracValley : Battleground
    {
        int[] _teamResources = new int[SharedConst.PvpTeamsCount];
        int[][] m_Team_QuestStatus = new int[SharedConst.PvpTeamsCount][]; //[x][y] x=team y=questcounter

        AVNodeInfo[] _nodes = new AVNodeInfo[(int)AVNodes.Max];

        TimeTracker _mineResourceTimer; //ticks for both teams

        AlteracValleyMineInfo[] _mineInfo = new AlteracValleyMineInfo[2];

        TimeTracker[] _captainBuffTimer = new TimeTracker[SharedConst.PvpTeamsCount];

        bool[] _isInformedNearVictory = new bool[SharedConst.PvpTeamsCount];
        List<ObjectGuid> _doorGUIDs = new();
        ObjectGuid _balindaGUID;
        ObjectGuid _galvangarGUID;
        List<ObjectGuid> _heraldGUIDs = new();

        public BgAlteracValley(BattlegroundTemplate battlegroundTemplate) : base(battlegroundTemplate)
        {
            _teamResources = [MiscConst.ScoreInitialPoints, MiscConst.ScoreInitialPoints];
            _isInformedNearVictory = [false, false];

            for (byte i = 0; i < 2; i++) //forloop for both teams (it just make 0 == alliance and 1 == horde also for both mines 0=north 1=south
            {
                for (byte j = 0; j < 9; j++)
                    m_Team_QuestStatus[i][j] = 0;

                _captainBuffTimer[i] = new((Minutes)(2 + RandomHelper.IRand(0, 4))); //as far as i could see, the buff is randomly so i make 2minutes (thats the duration of the buff itself) + 0-4minutes @todo get the right times
            }

            _mineInfo[(byte)AlteracValleyMine.North] = new AlteracValleyMineInfo(Team.Other, new StaticMineInfo(WorldStateIds.IrondeepMineOwner, WorldStateIds.IrondeepMineAllianceControlled, WorldStateIds.IrondeepMineHordeControlled, WorldStateIds.IrondeepMineTroggControlled, (byte)TextIds.IrondeepMineAllianceTaken, (byte)TextIds.IrondeepMineHordeTaken));
            _mineInfo[(byte)AlteracValleyMine.South] = new AlteracValleyMineInfo(Team.Other, new StaticMineInfo(WorldStateIds.ColdtoothMineOwner, WorldStateIds.ColdtoothMineAllianceControlled, WorldStateIds.ColdtoothMineHordeControlled, WorldStateIds.ColdtoothMineKoboldControlled, (byte)TextIds.ColdtoothMineAllianceTaken, (byte)TextIds.ColdtoothMineHordeTaken));

            for (AVNodes i = AVNodes.FirstaidStation; i <= AVNodes.StoneheartGrave; ++i) //alliance graves
                InitNode(i, Team.Alliance, false);
            for (AVNodes i = AVNodes.DunbaldarSouth; i <= AVNodes.StoneheartBunker; ++i) //alliance towers
                InitNode(i, Team.Alliance, true);
            for (AVNodes i = AVNodes.IcebloodGrave; i <= AVNodes.FrostwolfHut; ++i) //horde graves
                InitNode(i, Team.Horde, false);
            for (AVNodes i = AVNodes.IcebloodTower; i <= AVNodes.FrostwolfWtower; ++i) //horde towers
                InitNode(i, Team.Horde, true);
            InitNode(AVNodes.SnowfallGrave, Team.Other, false); //give snowfall neutral owner

            _mineResourceTimer = new(MiscConst.MineResourceTimer);

            StartMessageIds[BattlegroundConst.EventIdSecond] = (int)BroadcastTextIds.StartOneMinute;
            StartMessageIds[BattlegroundConst.EventIdThird] = (int)BroadcastTextIds.StartHalfMinute;
            StartMessageIds[BattlegroundConst.EventIdFourth] = (int)BroadcastTextIds.BattleHasBegun;
        }

        public override void HandleKillPlayer(Player player, Player killer)
        {
            if (GetStatus() != BattlegroundStatus.InProgress)
                return;

            base.HandleKillPlayer(player, killer);
            UpdateScore(GetPlayerTeam(player.GetGUID()), -1);
        }

        public override void HandleKillUnit(Creature unit, Unit killer)
        {
            Log.outDebug(LogFilter.Battleground, $"bg_av HandleKillUnit {unit.GetEntry()}");
            if (GetStatus() != BattlegroundStatus.InProgress)
                return;

            switch ((CreatureIds)unit.GetEntry())
            {
                case CreatureIds.Vanndar:
                {
                    UpdateWorldState(WorldStateIds.VandaarAlive, 0);
                    CastSpellOnTeam(MiscConst.SpellCompleteAlteracValleyQuest, Team.Horde); //this is a spell which finishes a quest where a player has to kill the boss
                    RewardReputationToTeam(MiscConst.FactionFrostwolfClan, MiscConst.RepGainBoss, Team.Horde);
                    RewardHonorToTeam(GetBonusHonorFromKill(MiscConst.HonorKillBonusBoss), Team.Horde);
                    EndBattleground(Team.Horde);
                    break;
                }
                case CreatureIds.Drekthar:
                {
                    UpdateWorldState(WorldStateIds.DrektharAlive, 0);
                    CastSpellOnTeam(MiscConst.SpellCompleteAlteracValleyQuest, Team.Alliance); //this is a spell which finishes a quest where a player has to kill the boss
                    RewardReputationToTeam(MiscConst.FactionStormpikeGuard, MiscConst.RepGainBoss, Team.Alliance);
                    RewardHonorToTeam(GetBonusHonorFromKill(MiscConst.HonorKillBonusBoss), Team.Alliance);
                    EndBattleground(Team.Alliance);
                    break;
                }
                case CreatureIds.Balinda:
                {
                    UpdateWorldState(WorldStateIds.BalindaAlive, 0);
                    RewardReputationToTeam(MiscConst.FactionFrostwolfClan, MiscConst.RepGainCaptain, Team.Horde);
                    RewardHonorToTeam(GetBonusHonorFromKill(MiscConst.HonorKillBonusCaptain), Team.Horde);
                    UpdateScore(Team.Alliance, MiscConst.ResourceLossCaptain);
                    Creature herald = FindHerald("bg_av_herald_horde_win");
                    if (herald != null)
                        herald.GetAI().Talk((int)TextIds.StormpikeGeneralDead);
                    break;
                }
                case CreatureIds.Galvangar:
                {
                    UpdateWorldState(WorldStateIds.GalvagarAlive, 0);
                    RewardReputationToTeam(MiscConst.FactionStormpikeGuard, MiscConst.RepGainCaptain, Team.Alliance);
                    RewardHonorToTeam(GetBonusHonorFromKill(MiscConst.HonorKillBonusCaptain), Team.Alliance);
                    UpdateScore(Team.Horde, MiscConst.ResourceLossCaptain);
                    Creature herald = FindHerald("bg_av_herald_alliance_win");
                    if (herald != null)
                        herald.GetAI().Talk((int)TextIds.FrostwolfGeneralDead);
                    break;
                }
                case CreatureIds.Morloch:
                {
                    // if mine is not owned by morloch, then nothing happens
                    if (_mineInfo[(byte)AlteracValleyMine.North].Owner != Team.Other)
                        break;

                    Team killerTeam = GetPlayerTeam((killer.GetCharmerOrOwnerPlayerOrPlayerItself() ?? killer).GetGUID());
                    ChangeMineOwner(AlteracValleyMine.North, killerTeam);
                    break;
                }
                case CreatureIds.TaskmasterSnivvle:
                {
                    if (_mineInfo[(byte)AlteracValleyMine.South].Owner != Team.Other)
                        break;

                    Team killerTeam = GetPlayerTeam((killer.GetCharmerOrOwnerPlayerOrPlayerItself() ?? killer).GetGUID());
                    ChangeMineOwner(AlteracValleyMine.South, killerTeam);
                    break;
                }
                case CreatureIds.UmiThorson:
                case CreatureIds.Keetar:
                {
                    Team killerTeam = GetPlayerTeam((killer.GetCharmerOrOwnerPlayerOrPlayerItself() ?? killer).GetGUID());
                    ChangeMineOwner(AlteracValleyMine.North, killerTeam);
                    break;
                }
                case CreatureIds.AgiRumblestomp:
                case CreatureIds.MashaSwiftcut:
                {
                    Team killerTeam = GetPlayerTeam((killer.GetCharmerOrOwnerPlayerOrPlayerItself() ?? killer).GetGUID());
                    ChangeMineOwner(AlteracValleyMine.South, killerTeam);
                    break;
                }
            }
        }

        public override void HandleQuestComplete(int questid, Player player)
        {
            if (GetStatus() != BattlegroundStatus.InProgress)
                return;//maybe we should log this, cause this must be a cheater or a big bug
            Team team = GetPlayerTeam(player.GetGUID());
            int teamIndex = GetTeamIndexByTeamId(team);
            /// @todo add reputation, events (including quest not available anymore, next quest available, go/npc de/spawning)and maybe honor
            Log.outDebug(LogFilter.Battleground, $"BG_AV Quest {questid} completed");
            switch ((QuestIds)questid)
            {
                case QuestIds.AScraps1:
                case QuestIds.AScraps2:
                case QuestIds.HScraps1:
                case QuestIds.HScraps2:
                    m_Team_QuestStatus[teamIndex][0] += 20;
                    break;
                case QuestIds.ACommander1:
                case QuestIds.HCommander1:
                    m_Team_QuestStatus[teamIndex][1]++;
                    RewardReputationToTeam((int)team, 1, team);
                    if (m_Team_QuestStatus[teamIndex][1] == 30)
                        Log.outDebug(LogFilter.Battleground, 
                            $"BG_AV Quest {questid} completed (need to implement some events here");
                    break;
                case QuestIds.ACommander2:
                case QuestIds.HCommander2:
                    m_Team_QuestStatus[teamIndex][2]++;
                    RewardReputationToTeam((int)team, 1, team);
                    if (m_Team_QuestStatus[teamIndex][2] == 60)
                        Log.outDebug(LogFilter.Battleground, 
                            $"BG_AV Quest {questid} completed (need to implement some events here");
                    break;
                case QuestIds.ACommander3:
                case QuestIds.HCommander3:
                    m_Team_QuestStatus[teamIndex][3]++;
                    RewardReputationToTeam((int)team, 1, team);
                    if (m_Team_QuestStatus[teamIndex][3] == 120)
                        Log.outDebug(LogFilter.Battleground, 
                            $"BG_AV Quest {questid} completed (need to implement some events here");
                    break;
                case QuestIds.ABoss1:
                case QuestIds.HBoss1:
                    m_Team_QuestStatus[teamIndex][4] += 4; //you can turn in 5 or 1 item..
                    goto case QuestIds.ABoss2;
                case QuestIds.ABoss2:
                case QuestIds.HBoss2:
                    m_Team_QuestStatus[teamIndex][4]++;
                    if (m_Team_QuestStatus[teamIndex][4] >= 200)
                        Log.outDebug(LogFilter.Battleground, 
                            $"BG_AV Quest {questid} completed (need to implement some events here");
                    UpdateWorldState((int)(teamIndex == BattleGroundTeamId.Alliance ? WorldStateIds.IvusStormCrystalCount : WorldStateIds.LokholarStormpikeSoldiersBloodCount), (int)m_Team_QuestStatus[teamIndex][4]);
                    break;
                case QuestIds.ANearMine:
                case QuestIds.HNearMine:
                    m_Team_QuestStatus[teamIndex][5]++;
                    if (m_Team_QuestStatus[teamIndex][5] == 28)
                    {
                        Log.outDebug(LogFilter.Battleground, 
                            $"BG_AV Quest {questid} completed (need to implement some events here");
                        if (m_Team_QuestStatus[teamIndex][6] == 7)
                            Log.outDebug(LogFilter.Battleground, 
                                $"BG_AV Quest {questid} completed (need to implement some events here - ground assault ready");
                    }
                    break;
                case QuestIds.AOtherMine:
                case QuestIds.HOtherMine:
                    m_Team_QuestStatus[teamIndex][6]++;
                    if (m_Team_QuestStatus[teamIndex][6] == 7)
                    {
                        Log.outDebug(LogFilter.Battleground, 
                            $"BG_AV Quest {questid} completed (need to implement some events here");
                        if (m_Team_QuestStatus[teamIndex][5] == 20)
                            Log.outDebug(LogFilter.Battleground, 
                                $"BG_AV Quest {questid} completed (need to implement some events here - ground assault ready");
                    }
                    break;
                case QuestIds.ARiderHide:
                case QuestIds.HRiderHide:
                    m_Team_QuestStatus[teamIndex][7]++;
                    if (m_Team_QuestStatus[teamIndex][7] == 25)
                    {
                        Log.outDebug(LogFilter.Battleground, 
                            $"BG_AV Quest {questid} completed (need to implement some events here");
                        if (m_Team_QuestStatus[teamIndex][8] == 25)
                            Log.outDebug(LogFilter.Battleground, 
                                $"BG_AV Quest {questid} completed (need to implement some events here - rider assault ready");
                    }
                    break;
                case QuestIds.ARiderTame:
                case QuestIds.HRiderTame:
                    m_Team_QuestStatus[teamIndex][8]++;
                    if (m_Team_QuestStatus[teamIndex][8] == 25)
                    {
                        Log.outDebug(LogFilter.Battleground, 
                            $"BG_AV Quest {questid} completed (need to implement some events here");
                        if (m_Team_QuestStatus[teamIndex][7] == 25)
                            Log.outDebug(LogFilter.Battleground, 
                                $"BG_AV Quest {questid} completed (need to implement some events here - rider assault ready");
                    }
                    break;
                default:
                    Log.outDebug(LogFilter.Battleground, $"BG_AV Quest {questid} completed but is not interesting at all");
                    break;
            }
        }

        void UpdateScore(Team team, short points)
        {
            Cypher.Assert(team == Team.Alliance || team == Team.Horde);
            int teamindex = GetTeamIndexByTeamId(team);
            _teamResources[teamindex] += points;

            UpdateWorldState((int)(teamindex == BattleGroundTeamId.Horde ? WorldStateIds.HordeReinforcements : WorldStateIds.AllianceReinforcements), _teamResources[teamindex]);
            if (points < 0)
            {
                if (_teamResources[teamindex] < 1)
                {
                    _teamResources[teamindex] = 0;
                    EndBattleground(teamindex == BattleGroundTeamId.Horde ? Team.Alliance : Team.Horde);
                }
                else if (!_isInformedNearVictory[teamindex] && _teamResources[teamindex] < MiscConst.NearLosePoints)
                {
                    if (teamindex == BattleGroundTeamId.Alliance)
                        SendBroadcastText((int)BroadcastTextIds.AllianceNearLose, ChatMsg.BgSystemAlliance);
                    else
                        SendBroadcastText((int)BroadcastTextIds.HordeNearLose, ChatMsg.BgSystemHorde);
                    PlaySoundToAll((int)SoundsId.NearVictory);
                    _isInformedNearVictory[teamindex] = true;
                }
            }
        }

        public override void PostUpdateImpl(TimeSpan diff)
        {
            if (GetStatus() != BattlegroundStatus.InProgress)
                return;

            _mineResourceTimer.Update(diff);
            if (_mineResourceTimer.Passed())
            {
                foreach (AlteracValleyMineInfo info in _mineInfo)
                {
                    if (info.Owner == Team.Other)
                        continue;

                    UpdateScore(info.Owner, 1);
                }

                _mineResourceTimer.Reset(MiscConst.MineResourceTimer);
            }

            for (byte i = BattleGroundTeamId.Alliance; i <= BattleGroundTeamId.Horde; i++)
            {
                if (!IsCaptainAlive(i))
                    continue;

                _captainBuffTimer[i].Update(diff);
                if (_captainBuffTimer[i].Passed())
                {
                    if (i == 0)
                    {
                        CastSpellOnTeam((int)AVBuffs.ACaptain, Team.Alliance);
                        Creature creature = GetBgMap().GetCreature(_balindaGUID);
                        if (creature != null)
                            creature.GetAI().DoAction(MiscConst.ActionBuffYell);
                    }
                    else
                    {
                        CastSpellOnTeam((int)AVBuffs.HCaptain, Team.Horde);
                        Creature creature = GetBgMap().GetCreature(_galvangarGUID);
                        if (creature != null)
                            creature.GetAI().DoAction(MiscConst.ActionBuffYell);
                    }

                    _captainBuffTimer[i].Reset((Minutes)(2 + RandomHelper.IRand(0, 4))); //as far as i could see, the buff is randomly so i make 2minutes (thats the duration of the buff itself) + 0-4minutes @todo get the right times
                }
            }
        }

        bool IsCaptainAlive(uint teamId)
        {
            if (teamId == BattleGroundTeamId.Horde)
                return GetBgMap().GetWorldStateValue((int)WorldStateIds.GalvagarAlive) == 1;
            else if (teamId == BattleGroundTeamId.Alliance)
                return GetBgMap().GetWorldStateValue((int)WorldStateIds.BalindaAlive) == 1;

            return false;
        }

        public override void AddPlayer(Player player, BattlegroundQueueTypeId queueId)
        {
            bool isInBattleground = IsPlayerInBattleground(player.GetGUID());
            base.AddPlayer(player, queueId);
            if (!isInBattleground)
                PlayerScores[player.GetGUID()] = new BattlegroundAVScore(player.GetGUID(), player.GetBGTeam());
        }

        public override void RemovePlayer(Player player, ObjectGuid guid, Team team)
        {
            if (player == null)
            {
                Log.outError(LogFilter.Battleground, "bg.battleground", "bg_AV no player at remove");
                return;
            }
            /// @todo search more buffs
            player.RemoveAurasDueToSpell(AVBuffs.Armor);
            player.RemoveAurasDueToSpell(AVBuffs.ACaptain);
            player.RemoveAurasDueToSpell(AVBuffs.HCaptain);
        }

        public override void StartingEventOpenDoors()
        {
            Log.outDebug(LogFilter.Battleground, "BG_AV: start spawning mine stuff");

            UpdateWorldState(WorldStateIds.ShowHordeReinforcements, 1);
            UpdateWorldState(WorldStateIds.ShowAllianceReinforcements, 1);

            // Achievement: The Alterac Blitz
            TriggerGameEvent(MiscConst.EventStartBattle);

            foreach (ObjectGuid guid in _doorGUIDs)
            {
                GameObject gameObject = GetBgMap().GetGameObject(guid);
                if (gameObject != null)
                {
                    gameObject.UseDoorOrButton();
                    TimeSpan delay = gameObject.GetEntry() == (int)GameObjectIds.GhostGate ? TimeSpan.Zero : (Seconds)3;
                    gameObject.DespawnOrUnsummon(delay);
                }
            }
        }

        public override void EndBattleground(Team winner)
        {
            //calculate bonuskills for both teams:
            //first towers:
            int[] kills = [0, 0];
            int[] rep = [0, 0];

            for (int i = (int)AVNodes.DunbaldarSouth; i <= (int)AVNodes.FrostwolfWtower; ++i)
            {
                if (_nodes[i].State == AVStates.PointControled)
                {
                    if (_nodes[i].Owner == Team.Alliance)
                    {
                        rep[BattleGroundTeamId.Alliance] += MiscConst.RepGainSurvivingTower;
                        kills[BattleGroundTeamId.Alliance] += MiscConst.HonorKillBonusSurvivingTower;
                    }
                    else
                    {
                        rep[BattleGroundTeamId.Horde] += MiscConst.RepGainSurvivingTower;
                        kills[BattleGroundTeamId.Horde] += MiscConst.HonorKillBonusSurvivingTower;
                    }
                }
            }

            for (byte i = BattleGroundTeamId.Alliance; i <= BattleGroundTeamId.Horde; ++i)
            {
                if (IsCaptainAlive(i))
                {
                    kills[i] += MiscConst.HonorKillBonusSurvivingCaptain;
                    rep[i] += MiscConst.RepGainSurvivingCaptain;
                }
                if (rep[i] != 0)
                    RewardReputationToTeam(i == 0 ? MiscConst.FactionStormpikeGuard : MiscConst.FactionFrostwolfClan, rep[i], i == 0 ? Team.Alliance : Team.Horde);
                if (kills[i] != 0)
                    RewardHonorToTeam(GetBonusHonorFromKill(kills[i]), i == 0 ? Team.Alliance : Team.Horde);
            }

            /// @todo add enterevademode for all attacking creatures
            base.EndBattleground(winner);
        }

        void RemovePlayer(Player player, ObjectGuid guid, uint team)
        {
            if (player == null)
            {
                Log.outError(LogFilter.Battleground, "bg_AV no player at remove");
                return;
            }
            /// @todo search more buffs
            player.RemoveAurasDueToSpell((int)AVBuffs.Armor);
        }

        void EventPlayerDestroyedPoint(GameObject gameobject)
        {
            if (gameobject == null)
                return;

            AVNodes node = GetNodeThroughObject(gameobject.GetEntry());
            DestroyNode(node);
            UpdateNodeWorldState(node);

            Team owner = _nodes[(int)node].Owner;
            if (IsTower(node))
            {
                UpdateScore((owner == Team.Alliance) ? Team.Horde : Team.Alliance, MiscConst.ResourceLossTower);
                RewardReputationToTeam(owner == Team.Alliance ? MiscConst.FactionStormpikeGuard : MiscConst.FactionFrostwolfClan, MiscConst.RepGainDestroyTower, owner);
                RewardHonorToTeam(GetBonusHonorFromKill(MiscConst.HonorKillBonusDestroyTower), owner);
            }

            StaticNodeInfo nodeInfo = GetStaticNodeInfo(node);
            if (nodeInfo != null)
            {
                Creature herald = FindHerald(nodeInfo.HordeOrDestroy);
                if (herald != null)
                    herald.GetAI().Talk(owner == Team.Alliance ? nodeInfo.AllianceCapture : nodeInfo.HordeCapture);
            }

            GetBgMap().UpdateSpawnGroupConditions();
        }

        public override void DoAction(int actionId, WorldObject source, WorldObject target)
        {
            switch (actionId)
            {
                case MiscConst.ActionCaptureCaptuableObject:
                    EventPlayerDestroyedPoint(source.ToGameObject());
                    break;
                case MiscConst.ActionInteractCapturableObject:
                    if (target != null && source != null && source.IsPlayer())
                        HandleInteractCapturableObject(source.ToPlayer(), target.ToGameObject());
                    break;
                default:
                    Log.outError(LogFilter.Battleground, $"BattlegroundAV::DoAction: {actionId}. Unhandled action.");
                    break;
            }
        }

        void ChangeMineOwner(AlteracValleyMine mine, Team team, bool initial = false)
        {
            if (team != Team.Alliance && team != Team.Horde)
                team = Team.Other;

            AlteracValleyMineInfo mineInfo = _mineInfo[(int)mine];

            if (mineInfo.Owner == team && !initial)
                return;

            mineInfo.Owner = team;

            SendMineWorldStates(mine);

            byte textId = team == Team.Alliance ? mineInfo.StaticInfo.TextIdAlliance : mineInfo.StaticInfo.TextIdHorde;

            string stringId = team == Team.Alliance ? "bg_av_herald_mine_alliance" : "bg_av_herald_mine_horde";

            Creature herald = FindHerald(stringId);
            if (herald != null)
                herald.GetAI().Talk(textId);
        }

        AVNodes GetNodeThroughObject(int objectId)
        {
            switch ((GameObjectIds)objectId)
            {
                case GameObjectIds.AidStationAllianceControlled:
                case GameObjectIds.AidStationHordeContested:
                case GameObjectIds.AidStationHordeControlled:
                case GameObjectIds.AidStationAllianceContested:
                    return AVNodes.FirstaidStation;
                case GameObjectIds.StormpikeAllianceControlled:
                case GameObjectIds.StormpikeHordeContested:
                case GameObjectIds.StormpikeHordeControlled:
                case GameObjectIds.StormpikeAllianceContested:
                    return AVNodes.StormpikeGrave;
                case GameObjectIds.StonehearthHordeContested:
                case GameObjectIds.StonehearthHordeControlled:
                case GameObjectIds.StonehearthAllianceContested:
                case GameObjectIds.StonehearthAllianceControlled:
                    return AVNodes.StoneheartGrave;
                case GameObjectIds.SnowfallNeutral:
                case GameObjectIds.SnowfallHordeContested:
                case GameObjectIds.SnowfallAllianceContested:
                case GameObjectIds.SnowfallHordeControlled:
                case GameObjectIds.SnowfallAllianceControlled:
                    return AVNodes.SnowfallGrave;
                case GameObjectIds.IcebloodHordeControlled:
                case GameObjectIds.IcebloodAllianceContested:
                case GameObjectIds.IcebloodAllianceControlled:
                case GameObjectIds.IcebloodHordeContested:
                    return AVNodes.IcebloodGrave;
                case GameObjectIds.FrostwolfHordeControlled:
                case GameObjectIds.FrostwolfAllianceContested:
                case GameObjectIds.FrostwolfAllianceControlled:
                case GameObjectIds.FrostwolfHordeContested:
                    return AVNodes.FrostwolfGrave;
                case GameObjectIds.FrostwolfHutHordeControlled:
                case GameObjectIds.FrostwolfHutAllianceContested:
                case GameObjectIds.FrostwolfHutAllianceControlled:
                case GameObjectIds.FrostwolfHutHordeContested:
                    return AVNodes.FrostwolfHut;
                case GameObjectIds.SouthBunkerControlledTowerBanner:
                case GameObjectIds.SouthBunkerControlledBanner:
                case GameObjectIds.SouthBunkerContestedBanner:
                case GameObjectIds.SouthBunkerContestedTowerBanner:
                    return AVNodes.DunbaldarSouth;
                case GameObjectIds.NorthBunkerControlledTowerBanner:
                case GameObjectIds.NorthBunkerControlledBanner:
                case GameObjectIds.NorthBunkerContestedBanner:
                case GameObjectIds.NorthBunkerContestedTowerBanner:
                    return AVNodes.DunbaldarNorth;
                case GameObjectIds.EastTowerControlledTowerBanner:
                case GameObjectIds.EastTowerControlledBanner:
                case GameObjectIds.EastTowerContestedBanner:
                case GameObjectIds.EastTowerContestedTowerBanner:
                    return AVNodes.FrostwolfEtower;
                case GameObjectIds.WestTowerControlledTowerBanner:
                case GameObjectIds.WestTowerControlledBanner:
                case GameObjectIds.WestTowerContestedBanner:
                case GameObjectIds.WestTowerContestedTowerBanner:
                    return AVNodes.FrostwolfWtower;
                case GameObjectIds.TowerPointControlledTowerBanner:
                case GameObjectIds.TowerPointControlledBanner:
                case GameObjectIds.TowerPointContestedBanner:
                case GameObjectIds.TowerPointContestedTowerBanner:
                    return AVNodes.TowerPoint;
                case GameObjectIds.IcebloodTowerControlledTowerBanner:
                case GameObjectIds.IcebloodTowerControlledBanner:
                case GameObjectIds.IcebloodTowerContestedBanner:
                case GameObjectIds.IcebloodTowerContestedTowerBanner:
                    return AVNodes.IcebloodTower;
                case GameObjectIds.StonehearthBunkerControlledTowerBanner:
                case GameObjectIds.StonehearthBunkerControlledBanner:
                case GameObjectIds.StonehearthBunkerContestedBanner:
                case GameObjectIds.StonehearthBunkerContestedTowerBanner:
                    return AVNodes.StoneheartBunker;
                case GameObjectIds.IcewingBunkerControlledTowerBanner:
                case GameObjectIds.IcewingBunkerControlledBanner:
                case GameObjectIds.IcewingBunkerContestedBanner:
                case GameObjectIds.IcewingBunkerContestedTowerBanner:
                    return AVNodes.IcewingBunker;
                default:
                    Log.outError(LogFilter.Battleground, $"BattlegroundAV: ERROR! GetPlace got a wrong objectId {objectId}");
                    //ABORT();
                    return 0;
            }
        }

        void HandleInteractCapturableObject(Player player, GameObject target)
        {
            if (player == null || target == null)
                return;

            switch ((GameObjectIds)target.GetEntry())
            {
                case GameObjectIds.AidStationAllianceControlled:
                case GameObjectIds.AidStationHordeControlled:
                case GameObjectIds.FrostwolfAllianceControlled:
                case GameObjectIds.FrostwolfHordeControlled:
                case GameObjectIds.FrostwolfHutAllianceControlled:
                case GameObjectIds.FrostwolfHutHordeControlled:
                case GameObjectIds.IcebloodAllianceControlled:
                case GameObjectIds.IcebloodHordeControlled:
                case GameObjectIds.StonehearthAllianceControlled:
                case GameObjectIds.StonehearthHordeControlled:
                case GameObjectIds.StormpikeAllianceControlled:
                case GameObjectIds.StormpikeHordeControlled:
                // Snowfall
                case GameObjectIds.SnowfallNeutral:
                case GameObjectIds.SnowfallAllianceControlled:
                case GameObjectIds.SnowfallHordeControlled:
                // Towers
                case GameObjectIds.EastTowerControlledBanner:
                case GameObjectIds.WestTowerControlledBanner:
                case GameObjectIds.TowerPointControlledBanner:
                case GameObjectIds.IcebloodTowerControlledBanner:
                case GameObjectIds.StonehearthBunkerControlledBanner:
                case GameObjectIds.IcewingBunkerControlledBanner:
                case GameObjectIds.SouthBunkerControlledBanner:
                case GameObjectIds.NorthBunkerControlledBanner:
                    EventPlayerAssaultsPoint(player, target.GetEntry());
                    break;
                // Graveyards
                case GameObjectIds.AidStationAllianceContested:
                case GameObjectIds.AidStationHordeContested:
                case GameObjectIds.FrostwolfAllianceContested:
                case GameObjectIds.FrostwolfHordeContested:
                case GameObjectIds.FrostwolfHutAllianceContested:
                case GameObjectIds.FrostwolfHutHordeContested:
                case GameObjectIds.IcebloodAllianceContested:
                case GameObjectIds.IcebloodHordeContested:
                case GameObjectIds.StonehearthAllianceContested:
                case GameObjectIds.StonehearthHordeContested:
                case GameObjectIds.StormpikeAllianceContested:
                case GameObjectIds.StormpikeHordeContested:
                // Towers
                case GameObjectIds.EastTowerContestedBanner:
                case GameObjectIds.WestTowerContestedBanner:
                case GameObjectIds.TowerPointContestedBanner:
                case GameObjectIds.IcebloodTowerContestedBanner:
                case GameObjectIds.StonehearthBunkerContestedBanner:
                case GameObjectIds.IcewingBunkerContestedBanner:
                case GameObjectIds.SouthBunkerContestedBanner:
                case GameObjectIds.NorthBunkerContestedBanner:
                    EventPlayerDefendsPoint(player, target.GetEntry());
                    break;
                // Snowfall Special cases (Either Defend/Assault)
                case GameObjectIds.SnowfallAllianceContested:
                case GameObjectIds.SnowfallHordeContested:
                {
                    AVNodes node = GetNodeThroughObject(target.GetEntry());
                    if (_nodes[(int)node].TotalOwner == Team.Other)
                        EventPlayerAssaultsPoint(player, target.GetEntry());
                    else
                        EventPlayerDefendsPoint(player, target.GetEntry());
                    break;
                }
                default:
                    break;
            }
        }

        void EventPlayerDefendsPoint(Player player, int obj)
        {
            AVNodes node = GetNodeThroughObject(obj);

            Team owner = _nodes[(int)node].Owner;
            Team team = GetPlayerTeam(player.GetGUID());

            if (owner == team || _nodes[(int)node].State != AVStates.PointAssaulted)
                return;

            Log.outDebug(LogFilter.Battleground, 
                $"player defends point object: {obj} node: {node}");

            if (_nodes[(int)node].PrevOwner != team)
            {
                Log.outError(LogFilter.Battleground, 
                    $"BG_AV: player defends point which doesn't belong to his team {node}");
                return;
            }

            DefendNode(node, team);
            UpdateNodeWorldState(node);

            StaticNodeInfo nodeInfo = GetStaticNodeInfo(node);
            if (nodeInfo != null)
            {
                string stringId;

                if (IsTower(node))
                    stringId = nodeInfo.AllianceOrDefend;
                else
                    stringId = team == Team.Alliance ? nodeInfo.AllianceOrDefend : nodeInfo.HordeOrDestroy;

                Creature herald = FindHerald(stringId);
                if (herald != null)
                    herald.GetAI().Talk(team == Team.Alliance ? nodeInfo.AllianceCapture : nodeInfo.HordeCapture);
            }

            // update the statistic for the defending player
            UpdatePlayerScore(player, IsTower(node) ? ScoreType.TowersDefended : ScoreType.GraveyardsDefended, 1);
            GetBgMap().UpdateSpawnGroupConditions();
        }

        void EventPlayerAssaultsPoint(Player player, int obj)
        {
            AVNodes node = GetNodeThroughObject(obj);
            Team owner = _nodes[(int)node].Owner; //maybe name it prevowner
            Team team = GetPlayerTeam(player.GetGUID());

            Log.outDebug(LogFilter.Battleground, 
                $"bg_av: player assaults point object {obj} node {node}");

            if (owner == team || team == _nodes[(int)node].TotalOwner)
                return; //surely a gm used this object

            AssaultNode(node, team);
            UpdateNodeWorldState(node);

            StaticNodeInfo nodeInfo = GetStaticNodeInfo(node);
            if (nodeInfo != null)
            {
                string stringId;
                if (IsTower(node))
                    stringId = nodeInfo.HordeOrDestroy;
                else
                    stringId = team == Team.Alliance ? nodeInfo.AllianceOrDefend : nodeInfo.HordeOrDestroy;

                Creature herald = FindHerald(stringId);
                if (herald != null)
                    herald.GetAI().Talk(team == Team.Alliance ? nodeInfo.AllianceAttack : nodeInfo.HordeAttack);
            }

            // update the statistic for the assaulting player
            UpdatePlayerScore(player, IsTower(node) ? ScoreType.TowersAssaulted : ScoreType.GraveyardsAssaulted, 1);
            GetBgMap().UpdateSpawnGroupConditions();
        }

        void UpdateNodeWorldState(AVNodes node)
        {
            StaticNodeInfo nodeInfo = GetStaticNodeInfo(node);
            if (nodeInfo != null)
            {
                Team owner = _nodes[(int)node].Owner;
                AVStates state = _nodes[(int)node].State;

                UpdateWorldState(nodeInfo.AllianceAssault, (owner == Team.Alliance && state == AVStates.PointAssaulted) ? 1 : 0);
                UpdateWorldState(nodeInfo.AllianceControl, (owner == Team.Alliance && state >= AVStates.PointDestroyed) ? 1 : 0);
                UpdateWorldState(nodeInfo.HordeAssault, (owner == Team.Horde && state == AVStates.PointAssaulted) ? 1 : 0);
                UpdateWorldState(nodeInfo.HordeControl, (owner == Team.Horde && state >= AVStates.PointDestroyed) ? 1 : 0);
                if (nodeInfo.Owner != 0)
                    UpdateWorldState(nodeInfo.Owner, owner == Team.Horde ? 2 : owner == Team.Alliance ? 1 : 0);
            }

            if (node == AVNodes.SnowfallGrave)
                UpdateWorldState(WorldStateIds.SnowfallGraveyardUncontrolled, _nodes[(int)node].Owner == Team.Other ? 1 : 0);
        }

        void SendMineWorldStates(AlteracValleyMine mine)
        {
            AlteracValleyMineInfo mineInfo = _mineInfo[(byte)mine];
            UpdateWorldState(mineInfo.StaticInfo.WorldStateHordeControlled, mineInfo.Owner == Team.Horde ? 1 : 0);
            UpdateWorldState(mineInfo.StaticInfo.WorldStateAllianceControlled, mineInfo.Owner == Team.Alliance ? 1 : 0);
            UpdateWorldState(mineInfo.StaticInfo.WorldStateNeutralControlled, mineInfo.Owner == Team.Other ? 1 : 0);
            UpdateWorldState(mineInfo.StaticInfo.WorldStateOwner, mineInfo.Owner == Team.Horde ? 2 : mineInfo.Owner == Team.Alliance ? 1 : 0);
        }

        public override WorldSafeLocsEntry GetExploitTeleportLocation(Team team)
        {
            return Global.ObjectMgr.GetWorldSafeLoc(team == Team.Alliance ? MiscConst.ExploitTeleportLocationAlliance : MiscConst.ExploitTeleportLocationHorde);
        }

        public override bool SetupBattleground()
        {
            return true;
        }

        void AssaultNode(AVNodes node, Team team)
        {
            _nodes[(int)node].PrevOwner = _nodes[(int)node].Owner;
            _nodes[(int)node].Owner = team;
            _nodes[(int)node].PrevState = _nodes[(int)node].State;
            _nodes[(int)node].State = AVStates.PointAssaulted;
        }

        void DestroyNode(AVNodes node)
        {
            _nodes[(int)node].TotalOwner = _nodes[(int)node].Owner;
            _nodes[(int)node].PrevOwner = _nodes[(int)node].Owner;
            _nodes[(int)node].PrevState = _nodes[(int)node].State;
            _nodes[(int)node].State = (_nodes[(int)node].Tower) ? AVStates.PointDestroyed : AVStates.PointControled;
        }

        void InitNode(AVNodes node, Team team, bool tower)
        {
            _nodes[(int)node].TotalOwner = team;
            _nodes[(int)node].Owner = team;
            _nodes[(int)node].PrevOwner = 0;
            _nodes[(int)node].State = AVStates.PointControled;
            _nodes[(int)node].PrevState = _nodes[(int)node].State;
            _nodes[(int)node].State = AVStates.PointControled;
            _nodes[(int)node].Tower = tower;
        }

        void DefendNode(AVNodes node, Team team)
        {
            _nodes[(int)node].PrevOwner = _nodes[(int)node].Owner;
            _nodes[(int)node].Owner = team;
            _nodes[(int)node].PrevState = _nodes[(int)node].State;
            _nodes[(int)node].State = AVStates.PointControled;
        }

        public override Team GetPrematureWinner()
        {
            int allianceScore = _teamResources[GetTeamIndexByTeamId(Team.Alliance)];
            int hordeScore = _teamResources[GetTeamIndexByTeamId(Team.Horde)];

            if (allianceScore > hordeScore)
                return Team.Alliance;
            else if (hordeScore > allianceScore)
                return Team.Horde;

            return base.GetPrematureWinner();
        }

        public override void OnGameObjectCreate(GameObject gameObject)
        {
            switch ((GameObjectIds)gameObject.GetEntry())
            {
                case GameObjectIds.GhostGate:
                case GameObjectIds.Gate:
                    _doorGUIDs.Add(gameObject.GetGUID());
                    break;
                default:
                    break;
            }
        }

        public override void OnCreatureCreate(Creature creature)
        {
            switch ((CreatureIds)creature.GetEntry())
            {
                case CreatureIds.Galvangar:
                    _galvangarGUID = creature.GetGUID();
                    break;
                case CreatureIds.Balinda:
                    _balindaGUID = creature.GetGUID();
                    break;
                case CreatureIds.Herald:
                    _heraldGUIDs.Add(creature.GetGUID());
                    break;
                default:
                    break;
            }
        }

        public override int GetData(int dataId)
        {
            var getDefenderTierForTeam = DefenderTier (int teamId) =>
            {
                if (m_Team_QuestStatus[teamId][0] < 500)
                    return DefenderTier.Defender;

                if (m_Team_QuestStatus[teamId][0] < 1000)
                    return DefenderTier.Seasoned;

                if (m_Team_QuestStatus[teamId][0] < 1500)
                    return DefenderTier.Veteran;

                return DefenderTier.Champion;
            };

            switch (dataId)
            {
                case MiscConst.DataDefenderTierAlliance:
                    return (int)getDefenderTierForTeam(BattleGroundTeamId.Alliance);
                case MiscConst.DataDefenderTierHorde:
                    return (int)getDefenderTierForTeam(BattleGroundTeamId.Horde);
                default:
                    return base.GetData(dataId);
            }
        }

        Creature FindHerald(string stringId)
        {
            foreach (ObjectGuid guid in _heraldGUIDs)
            {
                Creature creature = GetBgMap().GetCreature(guid);
                if (creature != null && creature.HasStringId(stringId))
                    return creature;
            }

            return null;
        }

        StaticNodeInfo GetStaticNodeInfo(AVNodes node)
        {
            for (byte i = 0; i < (int)AVNodes.Max; ++i)
                if (MiscConst.BGAVNodeInfo[i].NodeId == node)
                    return MiscConst.BGAVNodeInfo[i];

            return null;
        }

        bool IsTower(AVNodes node) { return _nodes[(int)node].Tower; }
    }

    struct AVNodeInfo
    {
        public AVStates State;
        public AVStates PrevState;
        public Team TotalOwner;
        public Team Owner;
        public Team PrevOwner;
        public bool Tower;
    }

    struct StaticMineInfo
    {
        public int WorldStateOwner;
        public int WorldStateAllianceControlled;
        public int WorldStateHordeControlled;
        public int WorldStateNeutralControlled;
        public byte TextIdAlliance;
        public byte TextIdHorde;

        public StaticMineInfo(int worldStateOwner, int worldStateAllianceControlled, int worldStateHordeControlled, int worldStateNeutralControlled, byte textIdAlliance, byte textIdHorde)
        {
            WorldStateOwner = worldStateOwner;
            WorldStateAllianceControlled = worldStateAllianceControlled;
            WorldStateHordeControlled = worldStateHordeControlled;
            WorldStateNeutralControlled = worldStateNeutralControlled;
            TextIdAlliance = textIdAlliance;
            TextIdHorde = textIdHorde;
        }
    }

    struct AlteracValleyMineInfo
    {
        public Team Owner;
        public StaticMineInfo StaticInfo;

        public AlteracValleyMineInfo(Team owner, StaticMineInfo staticMineInfo)
        {
            Owner = owner;
            StaticInfo = staticMineInfo;
        }
    }

    public class StaticNodeInfo
    {
        public AVNodes NodeId;
        public byte AllianceCapture;
        public byte AllianceAttack;
        public byte HordeCapture;
        public byte HordeAttack;
        public int AllianceControl;
        public int AllianceAssault;
        public int HordeControl;
        public int HordeAssault;
        public int Owner;
        public string AllianceOrDefend;
        public string HordeOrDestroy;

        public StaticNodeInfo(AVNodes nodeId, byte allianceCapture, byte allianceAttack, byte hordeCapture, byte hordeAttack, int allianceControl, int allianceAssault, int hordeControl, int hordeAssault, int owner, string allianceOrDefend, string hordeOrDestroy)
        {
            NodeId = nodeId;
            AllianceCapture = allianceCapture;
            AllianceAttack = allianceAttack;
            HordeCapture = hordeCapture;
            HordeAttack = hordeAttack;
            AllianceControl = allianceControl;
            AllianceAssault = allianceAssault;
            HordeControl = hordeControl;
            HordeAssault = hordeAssault;
            Owner = owner;
            AllianceOrDefend = allianceOrDefend;
            HordeOrDestroy = hordeOrDestroy;
        }
    }

    #region Constants
    struct MiscConst
    {
        public const int ActionBuffYell = -30001;
        public const int ActionInteractCapturableObject = 1;
        public const int ActionCaptureCaptuableObject = 2;

        public const int ExploitTeleportLocationAlliance = 3664;
        public const int ExploitTeleportLocationHorde = 3665;

        public const int DataDefenderTierHorde = 1;
        public const int DataDefenderTierAlliance = 2;

        public const int DefenderTierDefender = 0;
        public const int DefenderTierSeasoned = 1;
        public const int DefenderTierVeteran = 2;
        public const int DefenderTierChampion = 3;

        public const int NearLosePoints = 140;

        public const int PvpStatTowersAssaulted = 61;
        public const int PvpStatGraveyardsAssaulted = 63;
        public const int PvpStatTowersDefended = 64;
        public const int PvpStatGraveyardsDefended = 65;
        public const int PvpStatSecondaryObjectives = 82;

        public const int HonorKillBonusBoss = 4;
        public const int HonorKillBonusCaptain = 3;
        public const int HonorKillBonusSurvivingTower = 2;
        public const int HonorKillBonusSurvivingCaptain = 2;
        public const int HonorKillBonusDestroyTower = 3;

        public const int RepGainBoss = 350;
        public const int RepGainCaptain = 125;
        public const int RepGainDestroyTower = 12;
        public const int RepGainSurvivingTower = 12;
        public const int RepGainSurvivingCaptain = 125;

        public const int ResourceLossTower = -75;
        public const int ResourceLossCaptain = -100;

        public const int SpellCompleteAlteracValleyQuest = 23658;

        public const int FactionFrostwolfClan = 729;
        public const int FactionStormpikeGuard = 730;

        public const int ScoreInitialPoints = 700;
        public const int EventStartBattle = 9166; // Achievement: The Alterac Blitz

        public static TimeSpan MineResourceTimer = (Seconds)45;

        public static StaticNodeInfo[] BGAVNodeInfo =
        {
            new(AVNodes.FirstaidStation, 47, 48, 45, 46,  1325, 1326, 1327, 1328, 0, "bg_av_herald_stormpike_aid_station_alliance", "bg_av_herald_stormpike_aid_station_horde"), // Stormpike First Aid Station
            new(AVNodes.StormpikeGrave, 1, 2, 3, 4,  1333, 1335, 1334, 1336, 0, "bg_av_herald_stormpike_alliance", "bg_av_herald_stormpike_horde"), // Stormpike Graveyard
            new(AVNodes.StoneheartGrave, 55, 56, 53, 54,  1302, 1304, 1301, 1303, 0, "bg_av_herald_stonehearth_alliance", "bg_av_herald_stonehearth_horde"), // Stoneheart Graveyard
            new(AVNodes.SnowfallGrave, 5, 6, 7, 8,  1341, 1343, 1342, 1344, 0, "bg_av_herald_snowfall_alliance", "bg_av_herald_snowfall_horde"), // Snowfall Graveyard
            new(AVNodes.IcebloodGrave, 59, 60, 57, 58,  1346, 1348, 1347, 1349, 0, "bg_av_herald_iceblood_alliance", "bg_av_herald_iceblood_horde"), // Iceblood Graveyard
            new(AVNodes.FrostwolfGrave, 9, 10, 11, 12,  1337, 1339, 1338, 1340, 0, "bg_av_herald_frostwolf_alliance", "bg_av_herald_frostwolf_horde"), // Frostwolf Graveyard
            new(AVNodes.FrostwolfHut, 51, 52, 49, 50,  1329, 1331, 1330, 1332, 0, "bg_av_herald_frostwolf_hut_alliance", "bg_av_herald_frostwolf_hut_horde"), // Frostwolf Hut
            new(AVNodes.DunbaldarSouth,  16, 15, 14, 13,  1361, 1375, 1370, 1378, 1181, "bg_av_herald_south_bunker_defend", "bg_av_herald_south_bunker_attack"), // Dunbaldar South Bunker
            new(AVNodes.DunbaldarNorth,  20, 19, 18, 17,  1362, 1374, 1371, 1379, 1182, "bg_av_herald_north_bunker_defend", "bg_av_herald_south_bunker_attack"), // Dunbaldar North Bunker
            new(AVNodes.IcewingBunker, 24, 23, 22, 21,  1363, 1376, 1372, 1380, 1183, "bg_av_herald_icewing_bunker_defend", "bg_av_herald_icewing_bunker_attack"), // Icewing Bunker
            new(AVNodes.StoneheartBunker,  28, 27, 26, 25,  1364, 1377, 1373, 1381, 1184, "bg_av_herald_stonehearth_bunker_defend", "bg_av_herald_stonehearth_bunker_attack"), // Stoneheart Bunker
            new(AVNodes.IcebloodTower, 44, 43, 42, 41,  1368, 1390, 1385, 1395, 1188, "bg_av_herald_iceblood_tower_defend", "bg_av_herald_iceblood_tower_attack"), // Iceblood Tower
            new(AVNodes.TowerPoint, 40, 39, 38, 37,  1367, 1389, 1384, 1394, 1187, "bg_av_herald_tower_point_defend", "bg_av_herald_tower_point_attack"), // Tower Point
            new(AVNodes.FrostwolfEtower, 36, 35, 34, 33,  1366, 1388, 1383, 1393, 1186, "bg_av_herald_east_tower_defend", "bg_av_herald_east_tower_attack"), // Frostwolf East Tower
            new(AVNodes.FrostwolfWtower, 32, 31, 30, 29,  1365, 1387, 1382, 1392, 1185, "bg_av_herald_west_tower_defend", "bg_av_herald_west_tower_attack"), // Frostwolf West Tower
        };
    }

    enum DefenderTier
    {
        Defender,
        Seasoned,
        Veteran,
        Champion
    }

    enum BroadcastTextIds
    {
        StartOneMinute = 10638,
        StartHalfMinute = 10639,
        BattleHasBegun = 10640,

        AllianceNearLose = 23210,
        HordeNearLose = 23211
    }

    enum SoundsId
    {
        NearVictory = 8456, /// @Todo: Not Confirmed Yet

        AllianceAssaults = 8212, //Tower, Grave + Enemy Boss If Someone Tries To Attack Him
        HordeAssaults = 8174,
        AllianceGood = 8173, //If Something Good Happens For The Team:  Wins(Maybe Only Through Killing The Boss), Captures Mine Or Grave, Destroys Tower And Defends Grave
        HordeGood = 8213,
        BothTowerDefend = 8192,

        AllianceCaptain = 8232, //Gets Called When Someone Attacks Them And At The Beginning After 5min+Rand(X)*10sec (Maybe Buff)
        HordeCaptain = 8333
    }

    enum AlteracValleyMine
    {
        North = 0,
        South
    }

    enum CreatureIds
    {
        Vanndar = 11948,
        Drekthar = 11946,
        Balinda = 11949,
        Galvangar = 11947,
        Morloch = 11657,
        UmiThorson = 13078,
        Keetar = 13079,
        TaskmasterSnivvle = 11677,
        AgiRumblestomp = 13086,
        MashaSwiftcut = 13088,
        Herald = 14848,

        StormpikeDefender = 12050,
        FrostwolfGuardian = 12053,
        SeasonedDefender = 13326,
        SeasonedGuardian = 13328,
        VeteranDefender = 13331,
        VeteranGuardian = 13332,
        ChampionDefender = 13422,
        ChampionGuardian = 13421
    }

    enum GameObjectIds
    {
        BannerA = 178925, // Can Only Be Used By Horde
        BannerH = 178943, // Can Only Be Used By Alliance
        BannerContA = 178940, // Can Only Be Used By Horde
        BannerContH = 179435, // Can Only Be Used By Alliance

        BannerAB = 178365,
        BannerHB = 178364,
        BannerContAB = 179286,
        BannerContHB = 179287,
        BannerSnowfallN = 180418,

        //Snowfall Eyecandy Banner:
        SnowfallCandyA = 179044,
        SnowfallCandyPa = 179424,
        SnowfallCandyH = 179064,
        SnowfallCandyPh = 179425,

        //Banners On Top Of Towers:
        TowerBannerA = 178927, //[Ph] Alliance A1 Tower Banner Big
        TowerBannerH = 178955, //[Ph] Horde H1 Tower Banner Big
        TowerBannerPa = 179446, //[Ph] Alliance H1 Tower Pre-Banner Big
        TowerBannerPh = 179436, //[Ph] Horde A1 Tower Pre-Banner Big

        //Auras
        AuraA = 180421,
        AuraH = 180422,
        AuraN = 180423,
        AuraAS = 180100,
        AuraHS = 180101,
        AuraNS = 180102,

        Gate = 180424,
        GhostGate = 180322,

        //Mine Supplies
        MineN = 178785,
        MineS = 178784,

        Fire = 179065,
        Smoke = 179066,

        // Towers
        SouthBunkerControlledTowerBanner = 178927,
        SouthBunkerControlledBanner = 178925,
        SouthBunkerContestedBanner = 179435,
        SouthBunkerContestedTowerBanner = 179436,

        NorthBunkerControlledTowerBanner = 178932,
        NorthBunkerControlledBanner = 178929,
        NorthBunkerContestedBanner = 179439,
        NorthBunkerContestedTowerBanner = 179440,

        EastTowerControlledTowerBanner = 178956,
        EastTowerControlledBanner = 178944,
        EastTowerContestedBanner = 179449,
        EastTowerContestedTowerBanner = 179450,

        WestTowerControlledTowerBanner = 178955,
        WestTowerControlledBanner = 178943,
        WestTowerContestedBanner = 179445,
        WestTowerContestedTowerBanner = 179446,

        TowerPointControlledTowerBanner = 178957,
        TowerPointControlledBanner = 178945,
        TowerPointContestedBanner = 179453,
        TowerPointContestedTowerBanner = 179454,

        IcebloodTowerControlledTowerBanner = 178958,
        IcebloodTowerControlledBanner = 178946,
        IcebloodTowerContestedBanner = 178940,
        IcebloodTowerContestedTowerBanner = 179458,

        StonehearthBunkerControlledTowerBanner = 178948,
        StonehearthBunkerControlledBanner = 178936,
        StonehearthBunkerContestedBanner = 179443,
        StonehearthBunkerContestedTowerBanner = 179444,

        IcewingBunkerControlledTowerBanner = 178947,
        IcewingBunkerControlledBanner = 178935,
        IcewingBunkerContestedBanner = 179441,
        IcewingBunkerContestedTowerBanner = 179442,

        // Graveyards
        AidStationAllianceControlled = 179465,
        AidStationHordeContested = 179468,
        AidStationHordeControlled = 179467,
        AidStationAllianceContested = 179466,

        StormpikeAllianceControlled = 178389,
        StormpikeHordeContested = 179287,
        StormpikeHordeControlled = 178388,
        StormpikeAllianceContested = 179286,

        StonehearthHordeContested = 179310,
        StonehearthHordeControlled = 179285,
        StonehearthAllianceContested = 179308,
        StonehearthAllianceControlled = 179284,

        SnowfallNeutral = 180418,
        SnowfallHordeContested = 180420,
        SnowfallAllianceContested = 180419,
        SnowfallHordeControlled = 178364,
        SnowfallAllianceControlled = 178365,

        IcebloodHordeControlled = 179483,
        IcebloodAllianceContested = 179482,
        IcebloodAllianceControlled = 179481,
        IcebloodHordeContested = 179484,

        FrostwolfHordeControlled = 178393,
        FrostwolfAllianceContested = 179304,
        FrostwolfAllianceControlled = 178394,
        FrostwolfHordeContested = 179305,

        FrostwolfHutHordeControlled = 179472,
        FrostwolfHutAllianceContested = 179471,
        FrostwolfHutAllianceControlled = 179470,
        FrostwolfHutHordeContested = 179473
    }

    public enum AVNodes
    {
        FirstaidStation = 0,
        StormpikeGrave = 1,
        StoneheartGrave = 2,
        SnowfallGrave = 3,
        IcebloodGrave = 4,
        FrostwolfGrave = 5,
        FrostwolfHut = 6,
        DunbaldarSouth = 7,
        DunbaldarNorth = 8,
        IcewingBunker = 9,
        StoneheartBunker = 10,
        IcebloodTower = 11,
        TowerPoint = 12,
        FrostwolfEtower = 13,
        FrostwolfWtower = 14,

        Max = 15
    }

    static class AVObjectives
    {
        public static int AssaultTower = 61;
        public static int AssaultGraveyard = 63;
        public static int DefendTower = 64;
        public static int DefendGraveyard = 65;
        public static int SecondaryObjective = 82;
    }
    static class AVBuffs
    {
        public static int Armor = 21163;
        public static int ACaptain = 23693; //the buff which the alliance captain does
        public static int HCaptain = 22751; //the buff which the horde captain does
    }

    enum AVStates
    {
        PointNeutral = 0,
        PointAssaulted = 1,
        PointDestroyed = 2,
        PointControled = 3
    }

    struct WorldStateIds
    {
        public static int AllianceReinforcements = 3127;
        public static int HordeReinforcements = 3128;
        public static int ShowHordeReinforcements = 3133;
        public static int ShowAllianceReinforcements = 3134;
        public static int MaxReinforcements = 3136;

        // Graves
        // Alliance
        //Stormpike First Aid Station
        public static int StormpikeAidStationAllianceControlled = 1325;
        public static int StormpikeAidStationInConflictAllianceAttacking = 1326;
        public static int StormpikeAidStationHordeControlled = 1327;
        public static int StormpikeAidStationInConflictHordeAttacking = 1328;
        //Stormpike Graveyard
        public static int StormpikeGraveyardAllianceControlled = 1333;
        public static int StormpikeGraveyardInConflictAllianceAttacking = 1335;
        public static int StormpikeGraveyardHordeControlled = 1334;
        public static int StormpikeGraveyardInConflictHordeAttacking = 1336;
        //Stoneheart Grave
        public static int StonehearthGraveyardAllianceControlled = 1302;
        public static int StonehearthGraveyardInConflictAllianceAttacking = 1304;
        public static int StonehearthGraveyardHordeControlled = 1301;
        public static int StonehearthGraveyardInConflictHordeAttacking = 1303;
        //Neutral
        //Snowfall Grave
        public static int SnowfallGraveyardUncontrolled = 1966;
        public static int SnowfallGraveyardAllianceControlled = 1341;
        public static int SnowfallGraveyardInConflictAllianceAttacking = 1343;
        public static int SnowfallGraveyardHordeControlled = 1342;
        public static int SnowfallGraveyardInConflictHordeAttacking = 1344;
        //Horde
        //Iceblood Grave
        public static int IcebloodGraveyardAllianceControlled = 1346;
        public static int IcebloodGraveyardInConflictAllianceAttacking = 1348;
        public static int IcebloodGraveyardHordeControlled = 1347;
        public static int IcebloodGraveyardInConflictHordeAttacking = 1349;
        //Frostwolf Grave
        public static int FrostwolfGraveyardAllianceControlled = 1337;
        public static int FrostwolfGraveyardInConflictAllianceAttacking = 1339;
        public static int FrostwolfGraveyardHordeControlled = 1338;
        public static int FrostwolfGraveyardInConflictHordeAttacking = 1340;
        //Frostwolf Hut
        public static int FrostwolfReliefHutAllianceControlled = 1329;
        public static int FrostwolfReliefHutInConflictAllianceAttacking = 1331;
        public static int FrostwolfReliefHutHordeControlled = 1330;
        public static int FrostwolfReliefHutInConflictHordeAttacking = 1332;

        //Towers
        //Alliance
        //Dunbaldar South Bunker
        public static int DunBaldarSouthBunkerOwner = 1181;
        public static int DunBaldarSouthBunkerAllianceControlled = 1361;
        public static int DunBaldarSouthBunkerDestroyed = 1370;
        public static int DunBaldarSouthBunkerInConflictHordeAttacking = 1378;
        public static int DunBaldarSouthBunkerInConflictAllianceAttacking = 1374; // Unused
                                                                //Dunbaldar North Bunker
        public static int DunBaldarNorthBunkerOwner = 1182;
        public static int DunBaldarNorthBunkerAllianceControlled = 1362;
        public static int DunBaldarNorthBunkerDestroyed = 1371;
        public static int DunBaldarNorthBunkerInConflictHordeAttacking = 1379;
        public static int DunBaldarNorthBunkerInConflictAllianceAttacking = 1375; // Unused
                                                                //Icewing Bunker
        public static int IcewingBunkerOwner = 1183;
        public static int IcewingBunkerAllianceControlled = 1363;
        public static int IcewingBunkerDestroyed = 1372;
        public static int IcewingBunkerInConflictHordeAttacking = 1380;
        public static int IcewingBunkerInConflictAllianceAttacking = 1376; // Unused
                                                         //Stoneheart Bunker
        public static int StonehearthBunkerOwner = 1184;
        public static int StonehearthBunkerAllianceControlled = 1364;
        public static int StonehearthBunkerDestroyed = 1373;
        public static int StonehearthBunkerInConflictHordeAttacking = 1381;
        public static int StonehearthBunkerInConflictAllianceAttacking = 1377; // Unused
                                                             //Horde
                                                             //Iceblood Tower
        public static int IcebloodTowerOwner = 1187;
        public static int IcebloodTowerDestroyed = 1368;
        public static int IcebloodTowerHordeControlled = 1385;
        public static int IcebloodTowerInConflictAllianceAttacking = 1390;
        public static int IcebloodTowerInConflictHordeAttacking = 1395; // Unused
                                                      //Tower Point
        public static int TowerPointOwner = 1188;
        public static int TowerPointDestroyed = 1367;
        public static int TowerPointHordeControlled = 1384;
        public static int TowerPointInConflictAllianceAttacking = 1389;
        public static int TowerPointInConflictHordeAttacking = 1394; // Unused
                                                   //Frostwolf West
        public static int WestFrostwolfTowerOwner = 1185;
        public static int WestFrostwolfTowerDestroyed = 1365;
        public static int WestFrostwolfTowerHordeControlled = 1382;
        public static int WestFrostwolfTowerInConflictAllianceAttacking = 1387;
        public static int WestFrostwolfTowerInConflictHordeAttacking = 1392; // Unused
                                                           //Frostwolf East
        public static int EastFrostwolfTowerOwner = 1186;
        public static int EastFrostwolfTowerDestroyed = 1366;
        public static int EastFrostwolfTowerHordeControlled = 1383;
        public static int EastFrostwolfTowerInConflictAllianceAttacking = 1388;
        public static int EastFrostwolfTowerInConflictHordeAttacking = 1393; // Unused

        //Mines
        public static int IrondeepMineOwner = 801;
        public static int IrondeepMineTroggControlled = 1360;
        public static int IrondeepMineAllianceControlled = 1358;
        public static int IrondeepMineHordeControlled = 1359;

        public static int ColdtoothMineOwner = 804;
        public static int ColdtoothMineKoboldControlled = 1357;
        public static int ColdtoothMineAllianceControlled = 1355;
        public static int ColdtoothMineHordeControlled = 1356;

        //Turnins
        public static int IvusStormCrystalCount = 1043;
        public static int IvusStormCrystalMax = 1044;
        public static int LokholarStormpikeSoldiersBloodCount = 923;
        public static int LokholarStormpikeSoldiersBloodMax = 922;

        //Bosses
        public static int DrektharAlive = 601;
        public static int VandaarAlive = 602;

        //Captains
        public static int GalvagarAlive = 1352;
        public static int BalindaAlive = 1351;
    }

    enum QuestIds
    {
        AScraps1 = 7223,
        AScraps2 = 6781,
        HScraps1 = 7224,
        HScraps2 = 6741,
        ACommander1 = 6942, //Soldier
        HCommander1 = 6825,
        ACommander2 = 6941, //Leutnant
        HCommander2 = 6826,
        ACommander3 = 6943, //Commander
        HCommander3 = 6827,
        ABoss1 = 7386, // 5 Cristal/Blood
        HBoss1 = 7385,
        ABoss2 = 6881, // 1
        HBoss2 = 6801,
        ANearMine = 5892, //The Mine Near Start Location Of Team
        HNearMine = 5893,
        AOtherMine = 6982, //The Other Mine ;)
        HOtherMine = 6985,
        ARiderHide = 7026,
        HRiderHide = 7002,
        ARiderTame = 7027,
        HRiderTame = 7001
    }

    enum TextIds
    {
        // Herold
        // Towers/Graveyards = 1 - 60
        ColdtoothMineAllianceTaken = 61,
        IrondeepMineAllianceTaken = 62,
        ColdtoothMineHordeTaken = 63,
        IrondeepMineHordeTaken = 64,
        FrostwolfGeneralDead = 65, /// @Todo: Sound Is Missing
        StormpikeGeneralDead = 66, /// @Todo: Sound Is Missing
        AllianceWins = 67, // Nyi /// @Todo: Sound Is Missing
        HordeWins = 68, // Nyi /// @Todo: Sound Is Missing

        // Taskmaster Snivvle
        SnivvleRandom = 0
    }
    #endregion
}
