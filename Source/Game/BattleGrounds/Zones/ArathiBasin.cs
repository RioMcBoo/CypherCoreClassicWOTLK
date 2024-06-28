// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Maps;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;

namespace Game.BattleGrounds.Zones.ArathisBasin
{
    class BgArathiBasin : Battleground
    {
        TimeTracker _pointsTimer;
        int[] m_HonorScoreTics = new int[SharedConst.PvpTeamsCount];
        int[] m_ReputationScoreTics = new int[SharedConst.PvpTeamsCount];
        bool m_IsInformedNearVictory;
        int m_HonorTics;
        int m_ReputationTics;

        List<ObjectGuid> _gameobjectsToRemoveOnMatchStart = new();
        List<ObjectGuid> _creaturesToRemoveOnMatchStart = new();
        List<ObjectGuid> _doors = new();
        List<ObjectGuid> _capturePoints = new();

        public BgArathiBasin(BattlegroundTemplate battlegroundTemplate) : base(battlegroundTemplate)
        {
            m_IsInformedNearVictory = false;
            _pointsTimer = new TimeTracker(Misc.TickInterval);

            for (byte i = 0; i < SharedConst.PvpTeamsCount; ++i)
            {
                m_HonorScoreTics[i] = 0;
                m_ReputationScoreTics[i] = 0;
            }

            m_HonorTics = 0;
            m_ReputationTics = 0;
        }

        public override void PostUpdateImpl(uint diff)
        {
            if (GetStatus() == BattlegroundStatus.InProgress)
            {
                // Accumulate points
                _pointsTimer.Update(diff);
                if (_pointsTimer.Passed())
                {
                    _pointsTimer.Reset(Misc.TickInterval);

                    _CalculateTeamNodes(out var ally, out var horde);
                    int[] points = [ally, horde];

                    for (int team = 0; team < SharedConst.PvpTeamsCount; ++team)
                    {
                        if (points[team] == 0)
                            continue;

                        m_TeamScores[team] += Misc.TickPoints[points[team]];
                        m_HonorScoreTics[team] += Misc.TickPoints[points[team]];
                        m_ReputationScoreTics[team] += Misc.TickPoints[points[team]];

                        if (m_ReputationScoreTics[team] >= m_ReputationTics)
                        {
                            if (team == BattleGroundTeamId.Alliance)
                                RewardReputationToTeam(509, 10, Team.Alliance);
                            else
                                RewardReputationToTeam(510, 10, Team.Horde);

                            m_ReputationScoreTics[team] -= m_ReputationTics;
                        }

                        if (m_HonorScoreTics[team] >= m_HonorTics)
                        {
                            RewardHonorToTeam(GetBonusHonorFromKill(1), (team == BattleGroundTeamId.Alliance) ? Team.Alliance : Team.Horde);
                            m_HonorScoreTics[team] -= m_HonorTics;
                        }

                        if (!m_IsInformedNearVictory && m_TeamScores[team] > Misc.WarningNearVictoryScore)
                        {
                            if (team == BattleGroundTeamId.Alliance)
                            {
                                SendBroadcastText(ABBattlegroundBroadcastTexts.AllianceNearVictory, ChatMsg.BgSystemNeutral);
                                PlaySoundToAll(SoundIds.NearVictoryAlliance);
                            }
                            else
                            {
                                SendBroadcastText(ABBattlegroundBroadcastTexts.HordeNearVictory, ChatMsg.BgSystemNeutral);
                                PlaySoundToAll(SoundIds.NearVictoryHorde);
                            }
                            m_IsInformedNearVictory = true;
                        }

                        if (m_TeamScores[team] > Misc.MaxTeamScore)
                            m_TeamScores[team] = Misc.MaxTeamScore;

                        if (team == BattleGroundTeamId.Alliance)
                            UpdateWorldState(WorldStateIds.ResourcesAlly, m_TeamScores[team]);
                        else
                            UpdateWorldState(WorldStateIds.ResourcesHorde, m_TeamScores[team]);
                        // update achievement flags
                        // we increased m_TeamScores[team] so we just need to check if it is 500 more than other teams resources
                        int otherTeam = (team + 1) % SharedConst.PvpTeamsCount;
                        if (m_TeamScores[team] > m_TeamScores[otherTeam] + 500)
                        {
                            if (team == BattleGroundTeamId.Alliance)
                                UpdateWorldState(WorldStateIds.Had500DisadvantageHorde, 1);
                            else
                                UpdateWorldState(WorldStateIds.Had500DisadvantageAlliance, 1);
                        }
                    }

                    UpdateWorldState(WorldStateIds.OccupiedBasesAlly, ally);
                    UpdateWorldState(WorldStateIds.OccupiedBasesHorde, horde);
                }

                // Test win condition
                if (m_TeamScores[BattleGroundTeamId.Alliance] >= Misc.MaxTeamScore)
                    EndBattleground(Team.Alliance);
                else if (m_TeamScores[BattleGroundTeamId.Horde] >= Misc.MaxTeamScore)
                    EndBattleground(Team.Horde);
            }
        }

        public override void StartingEventOpenDoors()
        {
            // Achievement: Let's Get This Done
            TriggerGameEvent(ABEventIds.StartBattle);
        }

        public override void AddPlayer(Player player, BattlegroundQueueTypeId queueId)
        {
            bool isInBattleground = IsPlayerInBattleground(player.GetGUID());
            base.AddPlayer(player, queueId);
            if (!isInBattleground)
                PlayerScores[player.GetGUID()] = new BattlegroundABScore(player.GetGUID(), player.GetBGTeam());
        }

        public override void HandleAreaTrigger(Player player, int trigger, bool entered)
        {
            switch (trigger)
            {
                case 6635: // Horde Start
                case 6634: // Alliance Start
                    if (GetStatus() == BattlegroundStatus.WaitJoin && !entered)
                        TeleportPlayerToExploitLocation(player);
                    break;
                case 3948:                                          // Arathi Basin Alliance Exit.
                case 3949:                                          // Arathi Basin Horde Exit.
                case 3866:                                          // Stables
                case 3869:                                          // Gold Mine
                case 3867:                                          // Farm
                case 3868:                                          // Lumber Mill
                case 3870:                                          // Black Smith
                case 4020:                                          // Unk1
                case 4021:                                          // Unk2
                case 4674:                                          // Unk3
                                                                    //break;
                default:
                    base.HandleAreaTrigger(player, trigger, entered);
                    break;
            }
        }

        void _CalculateTeamNodes(out int alliance, out int horde)
        {
            alliance = 0;
            horde = 0;

            BattlegroundMap map = FindBgMap();
            if (map != null)
            {
                foreach (ObjectGuid guid in _capturePoints)
                {
                    GameObject capturePoint = map.GetGameObject(guid);
                    if (capturePoint != null)
                    {
                        int wsValue = map.GetWorldStateValue(capturePoint.GetGoInfo().CapturePoint.worldState1);
                        switch ((BattlegroundCapturePointState)wsValue)
                        {
                            case BattlegroundCapturePointState.AllianceCaptured:
                                ++alliance;
                                break;
                            case BattlegroundCapturePointState.HordeCaptured:
                                ++horde;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        public override Team GetPrematureWinner()
        {
            // How many bases each team owns
            _CalculateTeamNodes(out var ally, out var horde);

            if (ally > horde)
                return Team.Alliance;
            else if (horde > ally)
                return Team.Horde;

            // If the values are equal, fall back to the original result (based on number of players on each team)
            return base.GetPrematureWinner();
        }

        public override void ProcessEvent(WorldObject source, int eventId, WorldObject invoker)
        {
            Player player = invoker.ToPlayer();

            switch (eventId)
            {
                case ABEventIds.StartBattle:
                {
                    foreach (ObjectGuid guid in _creaturesToRemoveOnMatchStart)
                    {
                        Creature creature = GetBgMap().GetCreature(guid);
                        if (creature != null)
                            creature.DespawnOrUnsummon();
                    }

                    foreach (ObjectGuid guid in _gameobjectsToRemoveOnMatchStart)
                    {
                        GameObject gameObject = GetBgMap().GetGameObject(guid);
                        if (gameObject != null)
                            gameObject.DespawnOrUnsummon();
                    }

                    foreach (ObjectGuid guid in _doors)
                    {
                        GameObject gameObject = GetBgMap().GetGameObject(guid);
                        if (gameObject != null)
                        {
                            gameObject.UseDoorOrButton();
                            gameObject.DespawnOrUnsummon(TimeSpan.FromSeconds(3));
                        }
                    }
                    break;
                }
                case ABEventIds.ContestedBlacksmithAlliance:
                    UpdateWorldState(WorldStateIds.BlacksmithAllianceControlState, 1);
                    UpdateWorldState(WorldStateIds.BlacksmithHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeAssaultedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedBlacksmithAlliance:
                    UpdateWorldState(WorldStateIds.BlacksmithAllianceControlState, 2);
                    UpdateWorldState(WorldStateIds.BlacksmithHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureBlacksmithAlliance:
                    UpdateWorldState(WorldStateIds.BlacksmithAllianceControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    break;
                case ABEventIds.ContestedBlacksmithHorde:
                    UpdateWorldState(WorldStateIds.BlacksmithAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.BlacksmithHordeControlState, 1);
                    PlaySoundToAll(SoundIds.NodeAssaultedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedBlacksmithHorde:
                    UpdateWorldState(WorldStateIds.BlacksmithAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.BlacksmithHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureBlacksmithHorde:
                    UpdateWorldState(WorldStateIds.BlacksmithHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    break;
                case ABEventIds.ContestedFarmAlliance:
                    UpdateWorldState(WorldStateIds.FarmAllianceControlState, 1);
                    UpdateWorldState(WorldStateIds.FarmHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeAssaultedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedFarmAlliance:
                    UpdateWorldState(WorldStateIds.FarmAllianceControlState, 2);
                    UpdateWorldState(WorldStateIds.FarmHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureFarmAlliance:
                    UpdateWorldState(WorldStateIds.FarmAllianceControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    break;
                case ABEventIds.ContestedFarmHorde:
                    UpdateWorldState(WorldStateIds.FarmAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.FarmHordeControlState, 1);
                    PlaySoundToAll(SoundIds.NodeAssaultedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedFarmHorde:
                    UpdateWorldState(WorldStateIds.FarmAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.FarmHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureFarmHorde:
                    UpdateWorldState(WorldStateIds.FarmHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    break;
                case ABEventIds.ContestedGoldMineAlliance:
                    UpdateWorldState(WorldStateIds.GoldMineAllianceControlState, 1);
                    UpdateWorldState(WorldStateIds.GoldMineHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeAssaultedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedGoldMineAlliance:
                    UpdateWorldState(WorldStateIds.GoldMineAllianceControlState, 2);
                    UpdateWorldState(WorldStateIds.GoldMineHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureGoldMineAlliance:
                    UpdateWorldState(WorldStateIds.GoldMineAllianceControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    break;
                case ABEventIds.ContestedGoldMineHorde:
                    UpdateWorldState(WorldStateIds.GoldMineAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.GoldMineHordeControlState, 1);
                    PlaySoundToAll(SoundIds.NodeAssaultedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedGoldMineHorde:
                    UpdateWorldState(WorldStateIds.GoldMineAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.GoldMineHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureGoldMineHorde:
                    UpdateWorldState(WorldStateIds.GoldMineHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    break;
                case ABEventIds.ContestedLumberMillAlliance:
                    UpdateWorldState(WorldStateIds.LumberMillAllianceControlState, 1);
                    UpdateWorldState(WorldStateIds.LumberMillHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeAssaultedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedLumberMillAlliance:
                    UpdateWorldState(WorldStateIds.LumberMillAllianceControlState, 2);
                    UpdateWorldState(WorldStateIds.LumberMillHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureLumberMillAlliance:
                    UpdateWorldState(WorldStateIds.LumberMillAllianceControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    break;
                case ABEventIds.ContestedLumberMillHorde:
                    UpdateWorldState(WorldStateIds.LumberMillAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.LumberMillHordeControlState, 1);
                    PlaySoundToAll(SoundIds.NodeAssaultedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedLumberMillHorde:
                    UpdateWorldState(WorldStateIds.LumberMillAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.LumberMillHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureLumberMillHorde:
                    UpdateWorldState(WorldStateIds.LumberMillHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    break;
                case ABEventIds.ContestedStablesAlliance:
                    UpdateWorldState(WorldStateIds.StablesAllianceControlState, 1);
                    UpdateWorldState(WorldStateIds.StablesHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeAssaultedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedStablesAlliance:
                    UpdateWorldState(WorldStateIds.StablesAllianceControlState, 2);
                    UpdateWorldState(WorldStateIds.StablesHordeControlState, 0);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureStablesAlliance:
                    UpdateWorldState(WorldStateIds.StablesAllianceControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedAlliance);
                    break;
                case ABEventIds.ContestedStablesHorde:
                    UpdateWorldState(WorldStateIds.StablesAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.StablesHordeControlState, 1);
                    PlaySoundToAll(SoundIds.NodeAssaultedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesAssaulted, 1);
                    break;
                case ABEventIds.DefendedStablesHorde:
                    UpdateWorldState(WorldStateIds.StablesAllianceControlState, 0);
                    UpdateWorldState(WorldStateIds.StablesHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    if (player != null)
                        UpdatePlayerScore(player, ScoreType.BasesDefended, 1);
                    break;
                case ABEventIds.CaptureStablesHorde:
                    UpdateWorldState(WorldStateIds.StablesHordeControlState, 2);
                    PlaySoundToAll(SoundIds.NodeCapturedHorde);
                    break;
                default:
                    Log.outWarn(LogFilter.Battleground, $"BattlegroundAB::ProcessEvent: Unhandled event {eventId}.");
                    break;
            }
        }

        public override void OnCreatureCreate(Creature creature)
        {
            switch (creature.GetEntry())
            {
                case CreatureIds.TheBlackBride:
                case CreatureIds.RadulfLeder:
                    _creaturesToRemoveOnMatchStart.Add(creature.GetGUID());
                    break;
                default:
                    break;
            }
        }

        public override void OnGameObjectCreate(GameObject gameObject)
        {
            if (gameObject.GetGoInfo().type == GameObjectTypes.CapturePoint)
                _capturePoints.Add(gameObject.GetGUID());

            switch (gameObject.GetEntry())
            {
                case GameobjectIds.GhostGate:
                    _gameobjectsToRemoveOnMatchStart.Add(gameObject.GetGUID());
                    break;
                case GameobjectIds.AllianceDoor:
                case GameobjectIds.HordeDoor:
                    _doors.Add(gameObject.GetGUID());
                    break;
                default:
                    break;
            }
        }

        public override bool SetupBattleground()
        {
            UpdateWorldState(WorldStateIds.ResourcesMax, Misc.MaxTeamScore);
            UpdateWorldState(WorldStateIds.ResourcesWarning, Misc.WarningNearVictoryScore);

            return true;
        }

        public override void Reset()
        {
            //call parent's class reset
            base.Reset();

            for (var i = 0; i < SharedConst.PvpTeamsCount; ++i)
            {
                m_TeamScores[i] = 0;
                m_HonorScoreTics[i] = 0;
                m_ReputationScoreTics[i] = 0;
            }

            _pointsTimer.Reset(Misc.TickInterval);

            m_IsInformedNearVictory = false;
            bool isBGWeekend = Global.BattlegroundMgr.IsBGWeekend(GetTypeID());
            m_HonorTics = isBGWeekend ? Misc.ABBGWeekendHonorTicks : Misc.NotABBGWeekendHonorTicks;
            m_ReputationTics = isBGWeekend ? Misc.ABBGWeekendReputationTicks : Misc.NotABBGWeekendReputationTicks;

            _creaturesToRemoveOnMatchStart.Clear();
            _gameobjectsToRemoveOnMatchStart.Clear();
            _doors.Clear();
            _capturePoints.Clear();
        }

        public override void EndBattleground(Team winner)
        {
            // Win reward
            if (winner == Team.Alliance)
                RewardHonorToTeam(GetBonusHonorFromKill(1), Team.Alliance);
            if (winner == Team.Horde)
                RewardHonorToTeam(GetBonusHonorFromKill(1), Team.Horde);
            // Complete map_end rewards (even if no team wins)
            RewardHonorToTeam(GetBonusHonorFromKill(1), Team.Horde);
            RewardHonorToTeam(GetBonusHonorFromKill(1), Team.Alliance);

            base.EndBattleground(winner);
        }

        public override WorldSafeLocsEntry GetClosestGraveyard(Player player)
        {
            return Global.ObjectMgr.GetClosestGraveyard(player.GetWorldLocation(), player.GetTeam(), player);
        }

        public override WorldSafeLocsEntry GetExploitTeleportLocation(Team team)
        {
            return Global.ObjectMgr.GetWorldSafeLoc(team == Team.Alliance ? Misc.ExploitTeleportLocationAlliance : Misc.ExploitTeleportLocationHorde);
        }

        public override bool UpdatePlayerScore(Player player, ScoreType type, int value, bool doAddHonor = true)
        {
            if (!base.UpdatePlayerScore(player, type, value, doAddHonor))
                return false;

            switch (type)
            {
                case ScoreType.BasesAssaulted:
                    player.UpdateCriteria(CriteriaType.TrackedWorldStateUIModified, ABObjectives.AssaultBase);
                    break;
                case ScoreType.BasesDefended:
                    player.UpdateCriteria(CriteriaType.TrackedWorldStateUIModified, ABObjectives.DefendBase);
                    break;
                default:
                    break;
            }
            return true;
        }

        class BattlegroundABScore : BattlegroundScore
        {
            public BattlegroundABScore(ObjectGuid playerGuid, Team team) : base(playerGuid, team) { }

            public override void UpdateScore(ScoreType type, int value)
            {
                switch (type)
                {
                    case ScoreType.BasesAssaulted:   // Flags captured
                        BasesAssaulted += value;
                        break;
                    case ScoreType.BasesDefended:    // Flags returned
                        BasesDefended += value;
                        break;
                    default:
                        base.UpdateScore(type, value);
                        break;
                }
            }

            public override void BuildPvPLogPlayerDataPacket(out PVPMatchStatistics.PVPMatchPlayerStatistics playerData)
            {
                base.BuildPvPLogPlayerDataPacket(out playerData);

                playerData.Stats.Add(new PVPMatchStatistics.PVPMatchPlayerPVPStat(ABObjectives.AssaultBase, BasesAssaulted));
                playerData.Stats.Add(new PVPMatchStatistics.PVPMatchPlayerPVPStat(ABObjectives.DefendBase, BasesDefended));
            }

            public override int GetAttr1() { return BasesAssaulted; }
            public override int GetAttr2() { return BasesDefended; }

            private int BasesAssaulted;
            private int BasesDefended;
        }

        #region Constants
        struct Misc
        {
            public const int NotABBGWeekendHonorTicks = 260;
            public const int ABBGWeekendHonorTicks = 160;
            public const int NotABBGWeekendReputationTicks = 160;
            public const int ABBGWeekendReputationTicks = 120;

            public const int WarningNearVictoryScore = 1400;
            public const int MaxTeamScore = 1500;

            public const int ExploitTeleportLocationAlliance = 3705;
            public const int ExploitTeleportLocationHorde = 3706;

            // Tick intervals and given points: case 0, 1, 2, 3, 4, 5 captured nodes
            public static TimeSpan TickInterval = TimeSpan.FromSeconds(2);
            public static int[] TickPoints = { 0, 10, 10, 10, 10, 30 };
        }

        struct ABEventIds
        {
            public const int StartBattle = 9158; // Achievement: Let'S Get This Done

            public const int ContestedStablesHorde = 28523;
            public const int CaptureStablesHorde = 28527;
            public const int DefendedStablesHorde = 28525;
            public const int ContestedStablesAlliance = 28522;
            public const int CaptureStablesAlliance = 28526;
            public const int DefendedStablesAlliance = 28524;

            public const int ContestedBlacksmithHorde = 8876;
            public const int CaptureBlacksmithHorde = 8773;
            public const int DefendedBlacksmithHorde = 8770;
            public const int ContestedBlacksmithAlliance = 8874;
            public const int CaptureBlacksmithAlliance = 8769;
            public const int DefendedBlacksmithAlliance = 8774;

            public const int ContestedFarmHorde = 39398;
            public const int CaptureFarmHorde = 39399;
            public const int DefendedFarmHorde = 39400;
            public const int ContestedFarmAlliance = 39401;
            public const int CaptureFarmAlliance = 39402;
            public const int DefendedFarmAlliance = 39403;

            public const int ContestedGoldMineHorde = 39404;
            public const int CaptureGoldMineHorde = 39405;
            public const int DefendedGoldMineHorde = 39406;
            public const int ContestedGoldMineAlliance = 39407;
            public const int CaptureGoldMineAlliance = 39408;
            public const int DefendedGoldMineAlliance = 39409;

            public const int ContestedLumberMillHorde = 39387;
            public const int CaptureLumberMillHorde = 39388;
            public const int DefendedLumberMillHorde = 39389;
            public const int ContestedLumberMillAlliance = 39390;
            public const int CaptureLumberMillAlliance = 39391;
            public const int DefendedLumberMillAlliance = 39392;
        }

        struct WorldStateIds
        {
            public const int OccupiedBasesHorde = 1778;
            public const int OccupiedBasesAlly = 1779;
            public const int ResourcesAlly = 1776;
            public const int ResourcesHorde = 1777;
            public const int ResourcesMax = 1780;
            public const int ResourcesWarning = 1955;

            public const int StableIcon = 1842;             // Stable Map Icon (None)
            public const int StableStateAlience = 1767;             // Stable Map State (Alience)
            public const int StableStateHorde = 1768;             // Stable Map State (Horde)
            public const int StableStateConAli = 1769;             // Stable Map State (Con Alience)
            public const int StableStateConHor = 1770;             // Stable Map State (Con Horde)
            public const int FarmIcon = 1845;             // Farm Map Icon (None)
            public const int FarmStateAlience = 1772;             // Farm State (Alience)
            public const int FarmStateHorde = 1773;             // Farm State (Horde)
            public const int FarmStateConAli = 1774;             // Farm State (Con Alience)
            public const int FarmStateConHor = 1775;             // Farm State (Con Horde)
            public const int BlacksmithIcon = 1846;             // Blacksmith Map Icon (None)
            public const int BlacksmithStateAlience = 1782;             // Blacksmith Map State (Alience)
            public const int BlacksmithStateHorde = 1783;             // Blacksmith Map State (Horde)
            public const int BlacksmithStateConAli = 1784;             // Blacksmith Map State (Con Alience)
            public const int BlacksmithStateConHor = 1785;             // Blacksmith Map State (Con Horde)
            public const int LumbermillIcon = 1844;             // Lumber Mill Map Icon (None)
            public const int LumbermillStateAlience = 1792;             // Lumber Mill Map State (Alience)
            public const int LumbermillStateHorde = 1793;             // Lumber Mill Map State (Horde)
            public const int LumbermillStateConAli = 1794;             // Lumber Mill Map State (Con Alience)
            public const int LumbermillStateConHor = 1795;             // Lumber Mill Map State (Con Horde)
            public const int GoldmineIcon = 1843;             // Gold Mine Map Icon (None)
            public const int GoldmineStateAlience = 1787;             // Gold Mine Map State (Alience)
            public const int GoldmineStateHorde = 1788;             // Gold Mine Map State (Horde)
            public const int GoldmineStateConAli = 1789;             // Gold Mine Map State (Con Alience
            public const int GoldmineStateConHor = 1790;             // Gold Mine Map State (Con Horde)

            public const int Had500DisadvantageAlliance = 3644;
            public const int Had500DisadvantageHorde = 3645;

            public const int FarmIconNew = 8808;             // Farm Map Icon
            public const int LumberMillIconNew = 8805;             // Lumber Mill Map Icon
            public const int BlacksmithIconNew = 8799;             // Blacksmith Map Icon
            public const int GoldMineIconNew = 8809;             // Gold Mine Map Icon
            public const int StablesIconNew = 5834;             // Stable Map Icon

            public const int FarmHordeControlState = 17328;
            public const int FarmAllianceControlState = 17325;
            public const int LumberMillHordeControlState = 17330;
            public const int LumberMillAllianceControlState = 17326;
            public const int BlacksmithHordeControlState = 17327;
            public const int BlacksmithAllianceControlState = 17324;
            public const int GoldMineHordeControlState = 17329;
            public const int GoldMineAllianceControlState = 17323;
            public const int StablesHordeControlState = 17331;
            public const int StablesAllianceControlState = 17322;
        }

        // Object id templates from DB
        struct GameobjectIds
        {
            public const int CapturePointStables = 227420;
            public const int CapturePointBlacksmith = 227522;
            public const int CapturePointFarm = 227536;
            public const int CapturePointGoldMine = 227538;
            public const int CapturePointLumberMill = 227544;

            public const int GhostGate = 180322;
            public const int AllianceDoor = 322273;
            public const int HordeDoor = 322274;
        }

        struct CreatureIds
        {
            public const int TheBlackBride = 150501;
            public const int RadulfLeder = 150505;
        }

        struct ABBattlegroundNodes
        {
            public const int NodeStables = 0;
            public const int NodeBlacksmith = 1;
            public const int NodeFarm = 2;
            public const int NodeLumberMill = 3;
            public const int NodeGoldMine = 4;

            public const int DynamicNodesCount = 5;                        // Dynamic Nodes That Can Be Captured

            public const int SpiritAliance = 5;
            public const int SpiritHorde = 6;

            public const int AllCount = 7;                         // All Nodes (Dynamic And Static)
        }

        struct ABBattlegroundBroadcastTexts
        {
            public const int AllianceNearVictory = 10598;
            public const int HordeNearVictory = 10599;
        }

        struct SoundIds
        {
            public const int NodeClaimed = 8192;
            public const int NodeCapturedAlliance = 8173;
            public const int NodeCapturedHorde = 8213;
            public const int NodeAssaultedAlliance = 8212;
            public const int NodeAssaultedHorde = 8174;
            public const int NearVictoryAlliance = 8456;
            public const int NearVictoryHorde = 8457;
        }

        struct ABObjectives
        {
            public const int AssaultBase = 926;
            public const int DefendBase = 927;
        }
        #endregion
    }
}
