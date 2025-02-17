﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.DataStorage;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Game.BattleFields
{
    class BattlefieldWG : BattleField
    {
        public BattlefieldWG(Map map) : base(map) { }

        public override bool SetupBattlefield()
        {
            m_TypeId = (int)BattleFieldTypes.WinterGrasp;                              // See enum BattlefieldTypes
            m_BattleId = BattlefieldIds.WG;
            m_ZoneId = (int)AreaId.Wintergrasp;

            InitStalker(WGNpcs.Stalker, WGConst.WintergraspStalkerPos);

            m_MaxPlayer = WorldConfig.Values[WorldCfg.WintergraspPlrMax].Int32;
            m_IsEnabled = WorldConfig.Values[WorldCfg.WintergraspEnable].Bool;
            m_MinPlayer = WorldConfig.Values[WorldCfg.WintergraspPlrMin].Int32;
            m_MinLevel = WorldConfig.Values[WorldCfg.WintergraspPlrMinLvl].Int32;
            m_BattleTime = WorldConfig.Values[WorldCfg.WintergraspBattletime].TimeSpan;
            m_NoWarBattleTime = WorldConfig.Values[WorldCfg.WintergraspNobattletime].TimeSpan;
            m_RestartAfterCrash = WorldConfig.Values[WorldCfg.WintergraspRestartAfterCrash].TimeSpan;

            m_TimeForAcceptInvite = (Seconds)20;
            m_StartGroupingTimer = (Minutes)15;
            m_tenacityTeam = BattleGroundTeamId.Neutral;

            KickPosition = new WorldLocation(m_MapId, 5728.117f, 2714.346f, 697.733f, 0);

            RegisterZone(m_ZoneId);

            for (var team = 0; team < SharedConst.PvpTeamsCount; ++team)
            {
                DefenderPortalList[team] = new List<ObjectGuid>();
                m_vehicles[team] = new List<ObjectGuid>();
            }

            // Load from db
            if (Global.WorldStateMgr.GetValue(WorldStates.BattlefieldWgShowTimeNextBattle, m_Map) == 0 
                && Global.WorldStateMgr.GetValue(WGConst.ClockWorldState[0], m_Map) == 0)
            {
                Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.BattlefieldWgShowTimeNextBattle, 0, false, m_Map);
                Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.BattlefieldWgDefender, RandomHelper.IRand(0, 1), false, m_Map);
                Global.WorldStateMgr.SetValueAndSaveInDb(WGConst.ClockWorldState[0], LoopTime.UnixServerTime + (Seconds)m_NoWarBattleTime, false, m_Map);
            }

            m_isActive = Global.WorldStateMgr.GetValue(WorldStates.BattlefieldWgShowTimeNextBattle, m_Map) == 0;
            m_DefenderTeam = Global.WorldStateMgr.GetValue(WorldStates.BattlefieldWgDefender, m_Map);

            m_Timer = (UnixTime)Global.WorldStateMgr.GetValue(WGConst.ClockWorldState[0], m_Map) - LoopTime.UnixServerTime;
            if (m_isActive)
            {
                m_isActive = false;
                m_Timer = m_RestartAfterCrash;
            }

            Global.WorldStateMgr.SetValue(WorldStates.BattlefieldWgAttacker, GetAttackerTeam(), false, m_Map);
            Global.WorldStateMgr.SetValue(WGConst.ClockWorldState[1], LoopTime.UnixServerTime + (Seconds)m_Timer, false, m_Map);

            foreach (var gy in WGConst.WGGraveYard)
            {
                BfGraveyardWG graveyard = new(this);

                // When between games, the graveyard is controlled by the defending team
                if (gy.StartControl == BattleGroundTeamId.Neutral)
                    graveyard.Initialize(m_DefenderTeam, gy.GraveyardID);
                else
                    graveyard.Initialize(gy.StartControl, gy.GraveyardID);

                graveyard.SetTextId(gy.TextId);
                m_GraveyardList.Add(graveyard);
            }


            // Spawn workshop creatures and gameobjects
            for (byte i = 0; i < WGConst.MaxWorkshops; i++)
            {
                WGWorkshop workshop = new(this, i);
                if (i < WGWorkshopIds.Ne)
                    workshop.GiveControlTo(GetAttackerTeam(), true);
                else
                    workshop.GiveControlTo(GetDefenderTeam(), true);

                // Note: Capture point is added once the gameobject is created.
                Workshops.Add(workshop);
            }

            // Spawn turrets and hide them per default
            foreach (var turret in WGConst.WGTurret)
            {
                Position towerCannonPos = turret.GetPosition();
                Creature creature = SpawnCreature(WGNpcs.TowerCannon, towerCannonPos);
                if (creature != null)
                {
                    CanonList.Add(creature.GetGUID());
                    HideNpc(creature);
                }
            }

            // Spawn all gameobjects
            foreach (var build in WGConst.WGGameObjectBuilding)
            {
                GameObject go = SpawnGameObject(build.Entry, build.Pos, build.Rot);
                if (go != null)
                {
                    BfWGGameObjectBuilding b = new(this, build.BuildingType, build.WorldState);
                    b.Init(go);
                    if (!IsEnabled() && go.GetEntry() == WGGameObjects.VaultGate)
                        go.SetDestructibleState(GameObjectDestructibleState.Destroyed);
                    BuildingsInZone.Add(b);
                }
            }

            // Spawning portal defender
            foreach (var teleporter in WGConst.WGPortalDefenderData)
            {
                GameObject go = SpawnGameObject(teleporter.AllianceEntry, teleporter.Pos, teleporter.Rot);
                if (go != null)
                {
                    DefenderPortalList[BattleGroundTeamId.Alliance].Add(go.GetGUID());

                    go.SetRespawnTime(
                        GetDefenderTeam() == BattleGroundTeamId.Alliance 
                        ? BattlegroundConst.RespawnImmediately 
                        : BattlegroundConst.RespawnOneDay);
                }
                go = SpawnGameObject(teleporter.HordeEntry, teleporter.Pos, teleporter.Rot);
                if (go != null)
                {
                    DefenderPortalList[BattleGroundTeamId.Horde].Add(go.GetGUID());

                    go.SetRespawnTime(
                        GetDefenderTeam() == BattleGroundTeamId.Horde 
                        ? BattlegroundConst.RespawnImmediately 
                        : BattlegroundConst.RespawnOneDay);
                }
            }

            UpdateCounterVehicle(true);
            return true;
        }

        public override void OnBattleStart()
        {
            // Spawn titan relic
            GameObject relic = SpawnGameObject(WGGameObjects.TitanSRelic, WGConst.RelicPos, WGConst.RelicRot);
            if (relic != null)
            {
                // Update faction of relic, only attacker can click on
                relic.SetFaction(WGConst.WintergraspFaction[GetAttackerTeam()]);
                // Set in use (not allow to click on before last door is broken)
                relic.SetFlag(GameObjectFlags.InUse | GameObjectFlags.NotSelectable);
                m_titansRelicGUID = relic.GetGUID();
            }
            else
                Log.outError(LogFilter.Battlefield, "WG: Failed to spawn titan relic.");

            Global.WorldStateMgr.SetValue(WorldStates.BattlefieldWgAttacker, GetAttackerTeam(), false, m_Map);
            Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.BattlefieldWgDefender, GetDefenderTeam(), false, m_Map);
            Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.BattlefieldWgShowTimeNextBattle, 0, false, m_Map);
            Global.WorldStateMgr.SetValue(WorldStates.BattlefieldWgShowTimeBattleEnd, 1, false, m_Map);
            Global.WorldStateMgr.SetValueAndSaveInDb(WGConst.ClockWorldState[0], LoopTime.UnixServerTime + (Seconds)m_Timer, false, m_Map);

            // Update tower visibility and update faction
            foreach (var guid in CanonList)
            {
                Creature creature = GetCreature(guid);
                if (creature != null)
                {
                    ShowNpc(creature, true);
                    creature.SetFaction(WGConst.WintergraspFaction[GetDefenderTeam()]);
                }
            }

            // Rebuild all wall
            foreach (var wall in BuildingsInZone)
            {
                if (wall != null)
                {
                    wall.Rebuild();
                    wall.UpdateTurretAttack(false);
                }
            }

            SetData(WGData.BrokenTowerAtt, 0);
            SetData(WGData.BrokenTowerDef, 0);
            SetData(WGData.DamagedTowerAtt, 0);
            SetData(WGData.DamagedTowerDef, 0);

            // Update graveyard (in no war time all graveyard is to deffender, in war time, depend of base)
            foreach (var workShop in Workshops)
            {
                if (workShop != null)
                    workShop.UpdateGraveyardAndWorkshop();
            }

            for (byte team = 0; team < SharedConst.PvpTeamsCount; ++team)
            {
                foreach (var guid in m_players[team])
                {
                    // Kick player in orb room, TODO: offline player ?
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                    {
                        float x, y, z;
                        player.GetPosition(out x, out y, out z);
                        if (5500 > x && x > 5392 && y < 2880 && y > 2800 && z < 480)
                            player.TeleportTo(571, 5349.8686f, 2838.481f, 409.240f, 0.046328f);
                    }
                }
            }
            // Initialize vehicle counter
            UpdateCounterVehicle(true);
            // Send start warning to all players
            SendWarning(WintergraspText.StartBattle);
        }

        public void UpdateCounterVehicle(bool init)
        {
            if (init)
            {
                SetData(WGData.VehicleH, 0);
                SetData(WGData.VehicleA, 0);
            }
            SetData(WGData.MaxVehicleH, 0);
            SetData(WGData.MaxVehicleA, 0);

            foreach (var workshop in Workshops)
            {
                if (workshop.GetTeamControl() == BattleGroundTeamId.Alliance)
                    UpdateData(WGData.MaxVehicleA, 4);
                else if (workshop.GetTeamControl() == BattleGroundTeamId.Horde)
                    UpdateData(WGData.MaxVehicleH, 4);
            }

            UpdateVehicleCountWG();
        }

        public override void OnBattleEnd(bool endByTimer)
        {
            // Remove relic
            if (!m_titansRelicGUID.IsEmpty())
            {
                GameObject relic = GetGameObject(m_titansRelicGUID);
                if (relic != null)
                    relic.RemoveFromWorld();
            }
            m_titansRelicGUID.Clear();

            // change collision wall state closed
            foreach (BfWGGameObjectBuilding building in BuildingsInZone)
                building.RebuildGate();

            // update win statistics
            {
                int worldStateId;
                // successful defense
                if (endByTimer)
                    worldStateId = GetDefenderTeam() == BattleGroundTeamId.Horde ? WorldStates.BattlefieldWgDefendedH : WorldStates.BattlefieldWgDefendedA;
                // successful attack (note that teams have already been swapped, so defender team is the one who won)
                else
                    worldStateId = GetDefenderTeam() == BattleGroundTeamId.Horde ? WorldStates.BattlefieldWgAttackedH : WorldStates.BattlefieldWgAttackedA;

                Global.WorldStateMgr.SetValueAndSaveInDb(worldStateId, Global.WorldStateMgr.GetValue(worldStateId, m_Map) + 1, false, m_Map);
            }

            Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.BattlefieldWgDefender, GetDefenderTeam(), false, m_Map);
            Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.BattlefieldWgShowTimeNextBattle, 1, false, m_Map);
            Global.WorldStateMgr.SetValue(WorldStates.BattlefieldWgShowTimeBattleEnd, 0, false, m_Map);
            Global.WorldStateMgr.SetValue(WGConst.ClockWorldState[1], LoopTime.UnixServerTime + (Seconds)m_Timer, false, m_Map);

            // Remove turret
            foreach (var guid in CanonList)
            {
                Creature creature = GetCreature(guid);
                if (creature != null)
                {
                    if (!endByTimer)
                        creature.SetFaction(WGConst.WintergraspFaction[GetDefenderTeam()]);
                    HideNpc(creature);
                }
            }

            // Update all graveyard, control is to defender when no wartime
            for (byte i = 0; i < WGGraveyardId.Horde; i++)
            {
                BfGraveyard graveyard = GetGraveyardById(i);
                if (graveyard != null)
                    graveyard.GiveControlTo(GetDefenderTeam());
            }

            // Update portals
            foreach (var guid in DefenderPortalList[GetDefenderTeam()])
            {
                GameObject portal = GetGameObject(guid);
                if (portal != null)
                    portal.SetRespawnTime(BattlegroundConst.RespawnImmediately);
            }

            foreach (var guid in DefenderPortalList[GetAttackerTeam()])
            {
                GameObject portal = GetGameObject(guid);
                if (portal != null)
                    portal.SetRespawnTime(BattlegroundConst.RespawnOneDay);
            }

            foreach (var guid in m_PlayersInWar[GetDefenderTeam()])
            {
                Player player = Global.ObjAccessor.FindPlayer(guid);
                if (player != null)
                {
                    player.CastSpell(player, WGSpells.EssenceOfWintergrasp, true);
                    player.CastSpell(player, WGSpells.VictoryReward, true);
                    // Complete victory quests
                    player.AreaExploredOrEventHappens(WintergraspQuests.VictoryAlliance);
                    player.AreaExploredOrEventHappens(WintergraspQuests.VictoryHorde);
                    // Send Wintergrasp victory achievement
                    DoCompleteOrIncrementAchievement(WGAchievements.WinWg, player);
                    // Award achievement for succeeding in Wintergrasp in 10 minutes or less
                    if (!endByTimer && GetTimer() <= (Minutes)10)
                        DoCompleteOrIncrementAchievement(WGAchievements.WinWgTimer10, player);
                }
            }

            foreach (var guid in m_PlayersInWar[GetAttackerTeam()])
            {
                Player player = Global.ObjAccessor.FindPlayer(guid);
                if (player != null)
                    player.CastSpell(player, WGSpells.DefeatReward, true);
            }

            for (byte team = 0; team < SharedConst.PvpTeamsCount; ++team)
            {
                foreach (var guid in m_PlayersInWar[team])
                {
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                        RemoveAurasFromPlayer(player);
                }

                m_PlayersInWar[team].Clear();

                foreach (var guid in m_vehicles[team])
                {
                    Creature creature = GetCreature(guid);
                    if (creature != null)
                        if (creature.IsVehicle())
                            creature.DespawnOrUnsummon();
                }

                m_vehicles[team].Clear();
            }

            if (!endByTimer)
            {
                for (byte team = 0; team < SharedConst.PvpTeamsCount; ++team)
                {
                    foreach (var guid in m_players[team])
                    {
                        Player player = Global.ObjAccessor.FindPlayer(guid);
                        if (player != null)
                        {
                            player.RemoveAurasDueToSpell(
                                m_DefenderTeam == BattleGroundTeamId.Alliance 
                                ? WGSpells.HordeControlPhaseShift 
                                : WGSpells.AllianceControlPhaseShift, 
                            player.GetGUID());

                            player.AddAura(
                                m_DefenderTeam == BattleGroundTeamId.Horde 
                                ? WGSpells.HordeControlPhaseShift 
                                : WGSpells.AllianceControlPhaseShift, 
                            player);
                        }
                    }
                }
            }

            if (!endByTimer) // win alli/horde
                SendWarning((GetDefenderTeam() == BattleGroundTeamId.Alliance) ? WintergraspText.FortressCaptureAlliance : WintergraspText.FortressCaptureHorde);
            else // defend alli/horde
                SendWarning((GetDefenderTeam() == BattleGroundTeamId.Alliance) ? WintergraspText.FortressDefendAlliance : WintergraspText.FortressDefendHorde);
        }

        public override void DoCompleteOrIncrementAchievement(int achievement, Player player, byte incrementNumber = 1)
        {
            AchievementRecord achievementEntry = CliDB.AchievementStorage.LookupByKey(achievement);
            if (achievementEntry == null)
                return;

            switch (achievement)
            {
                //removed by TC
                //case ACHIEVEMENTS_WIN_WG_100:
                //{
                // player.UpdateAchievementCriteria();
                //}
                default:
                {
                    if (player != null)
                        player.CompletedAchievement(achievementEntry);
                    break;
                }
            }
        }

        public override void OnStartGrouping()
        {
            SendWarning(WintergraspText.StartGrouping);
        }

        int GetSpiritGraveyardId(int areaId)
        {
            switch ((AreaId)areaId)
            {
                case AreaId.WintergraspFortress:
                    return WGGraveyardId.Keep;
                case AreaId.TheSunkenRing:
                    return WGGraveyardId.WorkshopNE;
                case AreaId.TheBrokenTemplate:
                    return WGGraveyardId.WorkshopNW;
                case AreaId.WestparkWorkshop:
                    return WGGraveyardId.WorkshopSW;
                case AreaId.EastparkWorkshop:
                    return WGGraveyardId.WorkshopSE;
                case AreaId.Wintergrasp:
                    return WGGraveyardId.Alliance;
                case AreaId.TheChilledQuagmire:
                    return WGGraveyardId.Horde;
                default:
                    Log.outError(LogFilter.Battlefield, 
                        $"BattlefieldWG.GetSpiritGraveyardId: Unexpected Area Id {areaId}");
                    break;
            }

            return 0;
        }

        public override void OnCreatureCreate(Creature creature)
        {
            // Accessing to db spawned creatures
            switch (creature.GetEntry())
            {
                case WGNpcs.DwarvenSpiritGuide:
                case WGNpcs.TaunkaSpiritGuide:
                {
                    int teamIndex = (creature.GetEntry() == WGNpcs.DwarvenSpiritGuide ? BattleGroundTeamId.Alliance : BattleGroundTeamId.Horde);
                    byte graveyardId = (byte)GetSpiritGraveyardId(creature.GetAreaId());
                    if (m_GraveyardList[graveyardId] != null)
                        m_GraveyardList[graveyardId].SetSpirit(creature, teamIndex);
                    break;
                }
            }

            // untested code - not sure if it is valid.
            if (IsWarTime())
            {
                switch (creature.GetEntry())
                {
                    case WGNpcs.SiegeEngineAlliance:
                    case WGNpcs.SiegeEngineHorde:
                    case WGNpcs.Catapult:
                    case WGNpcs.Demolisher:
                    {
                        if (creature.ToTempSummon() == null || creature.ToTempSummon().GetSummonerGUID().IsEmpty() ||
                            Global.ObjAccessor.FindPlayer(creature.ToTempSummon().GetSummonerGUID()) == null)
                        {
                            creature.DespawnOrUnsummon();
                            return;
                        }

                        Player creator = Global.ObjAccessor.FindPlayer(creature.ToTempSummon().GetSummonerGUID());
                        int teamIndex = creator.GetBatttleGroundTeamId();
                        if (teamIndex == BattleGroundTeamId.Horde)
                        {
                            if (GetData(WGData.VehicleH) < GetData(WGData.MaxVehicleH))
                            {
                                UpdateData(WGData.VehicleH, 1);
                                creature.AddAura(WGSpells.HordeFlag, creature);
                                m_vehicles[teamIndex].Add(creature.GetGUID());
                                UpdateVehicleCountWG();
                            }
                            else
                            {
                                creature.DespawnOrUnsummon();
                                return;
                            }
                        }
                        else
                        {
                            if (GetData(WGData.VehicleA) < GetData(WGData.MaxVehicleA))
                            {
                                UpdateData(WGData.VehicleA, 1);
                                creature.AddAura(WGSpells.AllianceFlag, creature);
                                m_vehicles[teamIndex].Add(creature.GetGUID());
                                UpdateVehicleCountWG();
                            }
                            else
                            {
                                creature.DespawnOrUnsummon();
                                return;
                            }
                        }

                        creature.CastSpell(creator, WGSpells.GrabPassenger, true);
                        break;
                    }
                }
            }
        }

        public override void OnCreatureRemove(Creature c) { }

        public override void OnGameObjectCreate(GameObject go)
        {
            int workshopId;

            switch (go.GetEntry())
            {
                case WGGameObjects.FactoryBannerNe:
                    workshopId = WGWorkshopIds.Ne;
                    break;
                case WGGameObjects.FactoryBannerNw:
                    workshopId = WGWorkshopIds.Nw;
                    break;
                case WGGameObjects.FactoryBannerSe:
                    workshopId = WGWorkshopIds.Se;
                    break;
                case WGGameObjects.FactoryBannerSw:
                    workshopId = WGWorkshopIds.Sw;
                    break;
                default:
                    return;
            }

            foreach (var workshop in Workshops)
            {
                if (workshop.GetId() == workshopId)
                {
                    ControlZoneHandlers[go.GetEntry()] =new WintergraspCapturePoint(this, workshop);
                    if (GetAttackerTeam() == BattleGroundTeamId.Alliance)
                    {
                        //go->SetGoArtKit(); // todo set art kit
                        go.HandleCustomTypeCommand(new SetControlZoneValue(100));
                    }
                    else if (GetAttackerTeam() == BattleGroundTeamId.Horde)
                    {
                        //go->SetGoArtKit(); // todo set art kit
                        go.HandleCustomTypeCommand(new SetControlZoneValue(0));
                    }
                    break;
                }
            }
        }

        public override void HandleKill(Player killer, Unit victim)
        {
            if (killer == victim)
                return;

            if (victim.IsTypeId(TypeId.Player))
            {
                HandlePromotion(killer, victim);
                // Allow to Skin non-released corpse
                victim.SetUnitFlag(UnitFlags.Skinnable);
            }

            // @todo Recent PvP activity worldstate
        }

        bool FindAndRemoveVehicleFromList(Unit vehicle)
        {
            for (byte i = 0; i < SharedConst.PvpTeamsCount; ++i)
            {
                if (m_vehicles[i].Contains(vehicle.GetGUID()))
                {
                    m_vehicles[i].Remove(vehicle.GetGUID());
                    if (i == BattleGroundTeamId.Horde)
                        UpdateData(WGData.VehicleH, -1);
                    else
                        UpdateData(WGData.VehicleA, -1);
                    return true;
                }
            }
            return false;
        }

        public override void OnUnitDeath(Unit unit)
        {
            if (IsWarTime())
                if (unit.IsVehicle())
                    if (FindAndRemoveVehicleFromList(unit))
                        UpdateVehicleCountWG();
        }

        public void HandlePromotion(Player playerKiller, Unit unitKilled)
        {
            int teamId = playerKiller.GetBatttleGroundTeamId();

            foreach (var guid in m_PlayersInWar[teamId])
            {
                Player player = Global.ObjAccessor.FindPlayer(guid);
                if (player != null)
                    if (player.GetDistance2d(unitKilled) < 40.0f)
                        PromotePlayer(player);
            }
        }

        // Update rank for player
        void PromotePlayer(Player killer)
        {
            if (!m_isActive)
                return;
            // Updating rank of player
            Aura aur = killer.GetAura(WGSpells.Recruit);
            if (aur != null)
            {
                if (aur.GetStackAmount() >= 5)
                {
                    killer.RemoveAura(WGSpells.Recruit);
                    killer.CastSpell(killer, WGSpells.Corporal, true);
                    Creature stalker = GetCreature(StalkerGuid);
                    if (stalker != null)
                    {
                        Global.CreatureTextMgr.SendChat(
                            stalker, WintergraspText.RankCorporal, killer, ChatMsg.Addon, Language.Addon,
                            CreatureTextRange.Normal, 0, SoundKitPlayType.Normal, Team.Other, false, killer);
                    }
                }
                else
                    killer.CastSpell(killer, WGSpells.Recruit, true);
            }
            else if ((aur = killer.GetAura(WGSpells.Corporal)) != null)
            {
                if (aur.GetStackAmount() >= 5)
                {
                    killer.RemoveAura(WGSpells.Corporal);
                    killer.CastSpell(killer, WGSpells.Lieutenant, true);
                    Creature stalker = GetCreature(StalkerGuid);
                    if (stalker != null)
                    {
                        Global.CreatureTextMgr.SendChat(
                            stalker, WintergraspText.RankFirstLieutenant, killer, ChatMsg.Addon, Language.Addon,
                            CreatureTextRange.Normal, 0, SoundKitPlayType.Normal, Team.Other, false, killer);
                    }
                }
                else
                    killer.CastSpell(killer, WGSpells.Corporal, true);
            }
        }

        void RemoveAurasFromPlayer(Player player)
        {
            player.RemoveAurasDueToSpell(WGSpells.Recruit);
            player.RemoveAurasDueToSpell(WGSpells.Corporal);
            player.RemoveAurasDueToSpell(WGSpells.Lieutenant);
            player.RemoveAurasDueToSpell(WGSpells.TowerControl);
            player.RemoveAurasDueToSpell(WGSpells.SpiritualImmunity);
            player.RemoveAurasDueToSpell(WGSpells.Tenacity);
            player.RemoveAurasDueToSpell(WGSpells.EssenceOfWintergrasp);
            player.RemoveAurasDueToSpell(WGSpells.WintergraspRestrictedFlightArea);
        }

        public override void OnPlayerJoinWar(Player player)
        {
            RemoveAurasFromPlayer(player);

            player.CastSpell(player, WGSpells.Recruit, true);

            if (player.GetZoneId() != m_ZoneId)
            {
                if (player.GetBatttleGroundTeamId() == GetDefenderTeam())
                    player.TeleportTo(571, 5345, 2842, 410, 3.14f);
                else
                {
                    if (player.GetBatttleGroundTeamId() == BattleGroundTeamId.Horde)
                        player.TeleportTo(571, 5025.857422f, 3674.628906f, 362.737122f, 4.135169f);
                    else
                        player.TeleportTo(571, 5101.284f, 2186.564f, 373.549f, 3.812f);
                }
            }

            UpdateTenacity();

            if (player.GetBatttleGroundTeamId() == GetAttackerTeam())
            {
                if (GetData(WGData.BrokenTowerAtt) < 3)
                    player.SetAuraStack(WGSpells.TowerControl, player, 3 - GetData(WGData.BrokenTowerAtt));
            }
            else
            {
                if (GetData(WGData.BrokenTowerAtt) > 0)
                    player.SetAuraStack(WGSpells.TowerControl, player, GetData(WGData.BrokenTowerAtt));
            }
        }

        public override void OnPlayerLeaveWar(Player player)
        {
            // Remove all aura from WG // @todo false we can go out of this zone on retail and keep Rank buff, remove on end of WG
            if (!player.GetSession().PlayerLogout())
            {
                Creature vehicle = player.GetVehicleCreatureBase();
                if (vehicle != null)   // Remove vehicle of player if he go out.
                    vehicle.DespawnOrUnsummon();

                RemoveAurasFromPlayer(player);
            }

            player.RemoveAurasDueToSpell(WGSpells.HordeControlsFactoryPhaseShift);
            player.RemoveAurasDueToSpell(WGSpells.AllianceControlsFactoryPhaseShift);
            player.RemoveAurasDueToSpell(WGSpells.HordeControlPhaseShift);
            player.RemoveAurasDueToSpell(WGSpells.AllianceControlPhaseShift);
            UpdateTenacity();
        }

        public override void OnPlayerLeaveZone(Player player)
        {
            if (!m_isActive)
                RemoveAurasFromPlayer(player);

            player.RemoveAurasDueToSpell(WGSpells.HordeControlsFactoryPhaseShift);
            player.RemoveAurasDueToSpell(WGSpells.AllianceControlsFactoryPhaseShift);
            player.RemoveAurasDueToSpell(WGSpells.HordeControlPhaseShift);
            player.RemoveAurasDueToSpell(WGSpells.AllianceControlPhaseShift);
        }

        public override void OnPlayerEnterZone(Player player)
        {
            if (!m_isActive)
                RemoveAurasFromPlayer(player);

            player.AddAura(m_DefenderTeam == BattleGroundTeamId.Horde ? WGSpells.HordeControlPhaseShift : WGSpells.AllianceControlPhaseShift, player);
        }

        public override int GetData(int data)
        {
            switch ((AreaId)data)
            {
                // Used to determine when the phasing spells must be cast
                // See: SpellArea.IsFitToRequirements
                case AreaId.TheSunkenRing:
                case AreaId.TheBrokenTemplate:
                case AreaId.WestparkWorkshop:
                case AreaId.EastparkWorkshop:
                    // Graveyards and Workshops are controlled by the same team.
                    BfGraveyard graveyard = GetGraveyardById(GetSpiritGraveyardId(data));
                    if (graveyard != null)
                        return graveyard.GetControlTeamId();
                    break;
                default:
                    break;
            }

            return base.GetData(data);
        }

        public void BrokenWallOrTower(int team, BfWGGameObjectBuilding building)
        {
            if (team == GetDefenderTeam())
            {
                foreach (var guid in m_PlayersInWar[GetAttackerTeam()])
                {
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                        if (player.GetDistance2d(GetGameObject(building.GetGUID())) < 50.0f)
                            player.KilledMonsterCredit(WintergraspQuests.CreditDefendSiege);
                }
            }
        }

        // Called when a tower is broke
        public void UpdatedDestroyedTowerCount(int team)
        {
            // Southern tower
            if (team == GetAttackerTeam())
            {
                // Update counter
                UpdateData(WGData.DamagedTowerAtt, -1);
                UpdateData(WGData.BrokenTowerAtt, 1);

                // Remove buff stack on attackers
                foreach (var guid in m_PlayersInWar[GetAttackerTeam()])
                {
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                        player.RemoveAuraFromStack(WGSpells.TowerControl);
                }

                // Add buff stack to defenders and give achievement/quest credit
                foreach (var guid in m_PlayersInWar[GetDefenderTeam()])
                {
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                    {
                        player.CastSpell(player, WGSpells.TowerControl, true);
                        player.KilledMonsterCredit(WintergraspQuests.CreditTowersDestroyed);
                        DoCompleteOrIncrementAchievement(WGAchievements.WgTowerDestroy, player);
                    }
                }

                // If all three south towers are destroyed (ie. all attack towers), remove ten minutes from battle time
                if (GetData(WGData.BrokenTowerAtt) == 3)
                {
                    if (m_Timer - (Minutes)10 < TimeSpan.Zero)
                        m_Timer = TimeSpan.Zero;
                    else
                        m_Timer -= (Minutes)10;

                    Global.WorldStateMgr.SetValue(WGConst.ClockWorldState[0], LoopTime.UnixServerTime + (Seconds)m_Timer, false, m_Map);
                }
            }
            else // Keep tower
            {
                UpdateData(WGData.DamagedTowerDef, -1);
                UpdateData(WGData.BrokenTowerDef, 1);
            }
        }

        public override void ProcessEvent(WorldObject obj, int eventId, WorldObject invoker)
        {
            base.ProcessEvent(obj, eventId, invoker);

            if (obj == null || !IsWarTime())
                return;

            // We handle only gameobjects here
            GameObject go = obj.ToGameObject();
            if (go == null)
                return;

            // On click on titan relic
            if (go.GetEntry() == WGGameObjects.TitanSRelic)
            {
                GameObject relic = GetRelic();
                if (CanInteractWithRelic())
                    EndBattle(false);
                else if (relic != null)
                    relic.SetRespawnTime(TimeSpan.Zero);
            }

            // if destroy or damage event, search the wall/tower and update worldstate/send warning message
            foreach (var building in BuildingsInZone)
            {
                if (go.GetGUID() == building.GetGUID())
                {
                    GameObject buildingGo = GetGameObject(building.GetGUID());
                    if (buildingGo != null)
                    {
                        if (buildingGo.GetGoInfo().DestructibleBuilding.DamagedEvent == eventId)
                            building.Damaged();

                        if (buildingGo.GetGoInfo().DestructibleBuilding.DestroyedEvent == eventId)
                            building.Destroyed();

                        break;
                    }
                }
            }
        }

        // Called when a tower is damaged, used for honor reward calcul
        public void UpdateDamagedTowerCount(int team)
        {
            if (team == GetAttackerTeam())
                UpdateData(WGData.DamagedTowerAtt, 1);
            else
                UpdateData(WGData.DamagedTowerDef, 1);
        }

        // Update vehicle count WorldState to player
        void UpdateVehicleCountWG()
        {
            Global.WorldStateMgr.SetValue(WorldStates.BattlefieldWgVehicleH, GetData(WGData.VehicleH), false, m_Map);
            Global.WorldStateMgr.SetValue(WorldStates.BattlefieldWgMaxVehicleH, GetData(WGData.MaxVehicleH), false, m_Map);
            Global.WorldStateMgr.SetValue(WorldStates.BattlefieldWgVehicleA, GetData(WGData.VehicleA), false, m_Map);
            Global.WorldStateMgr.SetValue(WorldStates.BattlefieldWgMaxVehicleA, GetData(WGData.MaxVehicleA), false, m_Map);
        }

        void UpdateTenacity()
        {
            int alliancePlayers = m_PlayersInWar[BattleGroundTeamId.Alliance].Count;
            int hordePlayers = m_PlayersInWar[BattleGroundTeamId.Horde].Count;
            int newStack = 0;

            if (alliancePlayers != 0 && hordePlayers != 0)
            {
                if (alliancePlayers < hordePlayers)
                    newStack = (int)((((float)hordePlayers / alliancePlayers) - 1) * 4);  // positive, should cast on alliance
                else if (alliancePlayers > hordePlayers)
                    newStack = (int)((1 - ((float)alliancePlayers / hordePlayers)) * 4);  // negative, should cast on horde
            }

            if (newStack == m_tenacityStack)
                return;

            m_tenacityStack = newStack;
            // Remove old buff
            if (m_tenacityTeam != BattleGroundTeamId.Neutral)
            {
                foreach (var guid in m_players[m_tenacityTeam])
                {
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                        if (player.GetLevel() >= m_MinLevel)
                            player.RemoveAurasDueToSpell(WGSpells.Tenacity);
                }

                foreach (var guid in m_vehicles[m_tenacityTeam])
                {
                    Creature creature = GetCreature(guid);
                    if (creature != null)
                        creature.RemoveAurasDueToSpell(WGSpells.TenacityVehicle);
                }
            }

            // Apply new buff
            if (newStack != 0)
            {
                m_tenacityTeam = newStack > 0 ? BattleGroundTeamId.Alliance : BattleGroundTeamId.Horde;

                if (newStack < 0)
                    newStack = -newStack;
                if (newStack > 20)
                    newStack = 20;

                int buff_honor = WGSpells.GreatestHonor;
                if (newStack < 15)
                    buff_honor = WGSpells.GreaterHonor;
                if (newStack < 10)
                    buff_honor = WGSpells.GreatHonor;
                if (newStack < 5)
                    buff_honor = 0;

                foreach (var guid in m_PlayersInWar[m_tenacityTeam])
                {
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                        player.SetAuraStack(WGSpells.Tenacity, player, newStack);
                }

                foreach (var guid in m_vehicles[m_tenacityTeam])
                {
                    Creature creature = GetCreature(guid);
                    if (creature != null)
                        creature.SetAuraStack(WGSpells.TenacityVehicle, creature, newStack);
                }

                if (buff_honor != 0)
                {
                    foreach (var guid in m_PlayersInWar[m_tenacityTeam])
                    {
                        Player player = Global.ObjAccessor.FindPlayer(guid);
                        if (player != null)
                            player.CastSpell(player, buff_honor, true);
                    }

                    foreach (var guid in m_vehicles[m_tenacityTeam])
                    {
                        Creature creature = GetCreature(guid);
                        if (creature != null)
                            creature.CastSpell(creature, buff_honor, true);
                    }
                }
            }
            else
                m_tenacityTeam = BattleGroundTeamId.Neutral;
        }

        public GameObject GetRelic() { return GetGameObject(m_titansRelicGUID); }

        // Define relic object
        void SetRelic(ObjectGuid relicGUID) { m_titansRelicGUID = relicGUID; }

        // Check if players can interact with the relic (Only if the last door has been broken)
        bool CanInteractWithRelic() { return m_isRelicInteractible; }

        // Define if player can interact with the relic
        public void SetRelicInteractible(bool allow) { m_isRelicInteractible = allow; }


        bool m_isRelicInteractible;

        List<WGWorkshop> Workshops = new();

        List<ObjectGuid>[] DefenderPortalList = new List<ObjectGuid>[SharedConst.PvpTeamsCount];
        List<BfWGGameObjectBuilding> BuildingsInZone = new();

        List<ObjectGuid>[] m_vehicles = new List<ObjectGuid>[SharedConst.PvpTeamsCount];
        List<ObjectGuid> CanonList = new();

        int m_tenacityTeam;
        int m_tenacityStack;

        ObjectGuid m_titansRelicGUID;
    }

    class BfWGGameObjectBuilding
    {
        public BfWGGameObjectBuilding(BattlefieldWG WG, WGGameObjectBuildingType type, int worldState)
        {
            _wg = WG;
            _teamControl = BattleGroundTeamId.Neutral;
            _type = type;
            _worldState = worldState;
            _state = WGGameObjectState.None;

            for (var i = 0; i < 2; ++i)
            {
                m_GameObjectList[i] = new List<ObjectGuid>();
                m_CreatureBottomList[i] = new List<ObjectGuid>();
                m_CreatureTopList[i] = new List<ObjectGuid>();
            }
        }

        public void Rebuild()
        {
            switch (_type)
            {
                case WGGameObjectBuildingType.KeepTower:
                case WGGameObjectBuildingType.DoorLast:
                case WGGameObjectBuildingType.Door:
                case WGGameObjectBuildingType.Wall:
                    _teamControl = _wg.GetDefenderTeam();           // Objects that are part of the keep should be the defender's
                    break;
                case WGGameObjectBuildingType.Tower:
                    _teamControl = _wg.GetAttackerTeam();           // The towers in the south should be the attacker's
                    break;
                default:
                    _teamControl = BattleGroundTeamId.Neutral;
                    break;
            }

            GameObject build = _wg.GetGameObject(_buildGUID);
            if (build != null)
            {
                // Rebuild gameobject
                if (build.IsDestructibleBuilding())
                {
                    build.SetDestructibleState(GameObjectDestructibleState.Rebuilding, null, true);
                    if (build.GetEntry() == WGGameObjects.VaultGate)
                    {
                        GameObject go = build.FindNearestGameObject(WGGameObjects.KeepCollisionWall, 50.0f);
                        if (go != null)
                            go.SetGoState(GameObjectState.Active);
                    }

                    // Update worldstate
                    _state = WGGameObjectState.AllianceIntact - (_teamControl * 3);
                    Global.WorldStateMgr.SetValueAndSaveInDb(_worldState, (int)_state, false, _wg.GetMap());
                }
                UpdateCreatureAndGo();
                build.SetFaction(WGConst.WintergraspFaction[_teamControl]);
            }
        }

        public void RebuildGate()
        {
            GameObject build = _wg.GetGameObject(_buildGUID);
            if (build != null)
            {
                if (build.IsDestructibleBuilding() && build.GetEntry() == WGGameObjects.VaultGate)
                {
                    GameObject go = build.FindNearestGameObject(WGGameObjects.KeepCollisionWall, 50.0f);
                    if (go != null)
                        go.SetGoState(GameObjectState.Ready); //not GO_STATE_ACTIVE
                }
            }
        }

        // Called when associated gameobject is damaged
        public void Damaged()
        {
            // Update worldstate
            _state = WGGameObjectState.AllianceDamage - (_teamControl * 3);
            Global.WorldStateMgr.SetValueAndSaveInDb(_worldState, (int)_state, false, _wg.GetMap());

            // Send warning message
            if (_staticTowerInfo != null)                                       // tower damage + name
                _wg.SendWarning(_staticTowerInfo.DamagedTextId);

            foreach (var guid in m_CreatureTopList[_wg.GetAttackerTeam()])
            {
                Creature creature = _wg.GetCreature(guid);
                if (creature != null)
                    _wg.HideNpc(creature);
            }

            foreach (var guid in m_TurretTopList)
            {
                Creature creature = _wg.GetCreature(guid);
                if (creature != null)
                    _wg.HideNpc(creature);
            }

            if (_type == WGGameObjectBuildingType.KeepTower)
                _wg.UpdateDamagedTowerCount(_wg.GetDefenderTeam());
            else if (_type == WGGameObjectBuildingType.Tower)
                _wg.UpdateDamagedTowerCount(_wg.GetAttackerTeam());
        }

        // Called when associated gameobject is destroyed
        public void Destroyed()
        {
            // Update worldstate
            _state = WGGameObjectState.AllianceDestroy - (_teamControl * 3);
            Global.WorldStateMgr.SetValueAndSaveInDb(_worldState, (int)_state, false, _wg.GetMap());

            // Warn players
            if (_staticTowerInfo != null)
                _wg.SendWarning(_staticTowerInfo.DestroyedTextId);

            switch (_type)
            {
                // Inform the global wintergrasp script of the destruction of this object
                case WGGameObjectBuildingType.Tower:
                case WGGameObjectBuildingType.KeepTower:
                    _wg.UpdatedDestroyedTowerCount(_teamControl);
                    break;
                case WGGameObjectBuildingType.DoorLast:
                    if (_wg.GetGameObject(_buildGUID) is GameObject build)
                    {
                        GameObject go = build.FindNearestGameObject(WGGameObjects.KeepCollisionWall, 50.0f);
                        if (go != null)
                            go.SetGoState(GameObjectState.Active);
                    }
                    _wg.SetRelicInteractible(true);
                    if (_wg.GetRelic() is GameObject relic)
                        relic.RemoveFlag(GameObjectFlags.InUse | GameObjectFlags.NotSelectable);
                    else
                        Log.outError(LogFilter.Server, "BattlefieldWG: Titan Relic not found.");
                    break;
            }

            _wg.BrokenWallOrTower(_teamControl, this);
        }

        public void Init(GameObject go)
        {
            if (go == null)
                return;

            // GameObject associated to object
            _buildGUID = go.GetGUID();

            switch (_type)
            {
                case WGGameObjectBuildingType.KeepTower:
                case WGGameObjectBuildingType.DoorLast:
                case WGGameObjectBuildingType.Door:
                case WGGameObjectBuildingType.Wall:
                    _teamControl = _wg.GetDefenderTeam();           // Objects that are part of the keep should be the defender's
                    break;
                case WGGameObjectBuildingType.Tower:
                    _teamControl = _wg.GetAttackerTeam();           // The towers in the south should be the attacker's
                    break;
                default:
                    _teamControl = BattleGroundTeamId.Neutral;
                    break;
            }

            _state = (WGGameObjectState)Global.WorldStateMgr.GetValue(_worldState, _wg.GetMap()).Int32;
            if (_state == WGGameObjectState.None)
            {
                // set to default state based on Type
                switch (_teamControl)
                {
                    case BattleGroundTeamId.Alliance:
                        _state = WGGameObjectState.AllianceIntact;
                        break;
                    case BattleGroundTeamId.Horde:
                        _state = WGGameObjectState.HordeIntact;
                        break;
                    case BattleGroundTeamId.Neutral:
                        _state = WGGameObjectState.NeutralIntact;
                        break;
                    default:
                        break;
                }
                Global.WorldStateMgr.SetValueAndSaveInDb(_worldState, (int)_state, false, _wg.GetMap());
            }

            switch (_state)
            {
                case WGGameObjectState.NeutralIntact:
                case WGGameObjectState.AllianceIntact:
                case WGGameObjectState.HordeIntact:
                    go.SetDestructibleState(GameObjectDestructibleState.Rebuilding, null, true);
                    break;
                case WGGameObjectState.NeutralDestroy:
                case WGGameObjectState.AllianceDestroy:
                case WGGameObjectState.HordeDestroy:
                    go.SetDestructibleState(GameObjectDestructibleState.Destroyed);
                    break;
                case WGGameObjectState.NeutralDamage:
                case WGGameObjectState.AllianceDamage:
                case WGGameObjectState.HordeDamage:
                    go.SetDestructibleState(GameObjectDestructibleState.Damaged);
                    break;
            }

            int towerId = -1;
            switch (go.GetEntry())
            {
                case WGGameObjects.FortressTower1:
                    towerId = 0;
                    break;
                case WGGameObjects.FortressTower2:
                    towerId = 1;
                    break;
                case WGGameObjects.FortressTower3:
                    towerId = 2;
                    break;
                case WGGameObjects.FortressTower4:
                    towerId = 3;
                    break;
                case WGGameObjects.ShadowsightTower:
                    towerId = 4;
                    break;
                case WGGameObjects.WinterSEdgeTower:
                    towerId = 5;
                    break;
                case WGGameObjects.FlamewatchTower:
                    towerId = 6;
                    break;
            }

            if (towerId > 3) // Attacker towers
            {
                // Spawn associate gameobjects
                foreach (var gobData in WGConst.AttackTowers[towerId - 4].GameObject)
                {
                    GameObject goHorde = _wg.SpawnGameObject(gobData.HordeEntry, gobData.Pos, gobData.Rot);
                    if (goHorde != null)
                        m_GameObjectList[BattleGroundTeamId.Horde].Add(goHorde.GetGUID());

                    GameObject goAlliance = _wg.SpawnGameObject(gobData.AllianceEntry, gobData.Pos, gobData.Rot);
                    if (goAlliance != null)
                        m_GameObjectList[BattleGroundTeamId.Alliance].Add(goAlliance.GetGUID());
                }

                // Spawn associate npc bottom
                foreach (var creatureData in WGConst.AttackTowers[towerId - 4].CreatureBottom)
                {
                    Creature creature = _wg.SpawnCreature(creatureData.HordeEntry, creatureData.Pos);
                    if (creature != null)
                        m_CreatureBottomList[BattleGroundTeamId.Horde].Add(creature.GetGUID());

                    creature = _wg.SpawnCreature(creatureData.AllianceEntry, creatureData.Pos);
                    if (creature != null)
                        m_CreatureBottomList[BattleGroundTeamId.Alliance].Add(creature.GetGUID());
                }
            }

            if (towerId >= 0)
            {
                _staticTowerInfo = WGConst.TowerData[towerId];

                // Spawn Turret bottom
                foreach (var turretPos in WGConst.TowerCannon[towerId].TowerCannonBottom)
                {
                    Creature turret = _wg.SpawnCreature(WGNpcs.TowerCannon, turretPos);
                    if (turret != null)
                    {
                        m_TowerCannonBottomList.Add(turret.GetGUID());
                        switch (go.GetEntry())
                        {
                            case WGGameObjects.FortressTower1:
                            case WGGameObjects.FortressTower2:
                            case WGGameObjects.FortressTower3:
                            case WGGameObjects.FortressTower4:
                                turret.SetFaction(WGConst.WintergraspFaction[_wg.GetDefenderTeam()]);
                                break;
                            case WGGameObjects.ShadowsightTower:
                            case WGGameObjects.WinterSEdgeTower:
                            case WGGameObjects.FlamewatchTower:
                                turret.SetFaction(WGConst.WintergraspFaction[_wg.GetAttackerTeam()]);
                                break;
                        }
                        _wg.HideNpc(turret);
                    }
                }

                // Spawn Turret top
                foreach (var towerCannonPos in WGConst.TowerCannon[towerId].TurretTop)
                {
                    Creature turret = _wg.SpawnCreature(WGNpcs.TowerCannon, towerCannonPos);
                    if (turret != null)
                    {
                        m_TurretTopList.Add(turret.GetGUID());
                        switch (go.GetEntry())
                        {
                            case WGGameObjects.FortressTower1:
                            case WGGameObjects.FortressTower2:
                            case WGGameObjects.FortressTower3:
                            case WGGameObjects.FortressTower4:
                                turret.SetFaction(WGConst.WintergraspFaction[_wg.GetDefenderTeam()]);
                                break;
                            case WGGameObjects.ShadowsightTower:
                            case WGGameObjects.WinterSEdgeTower:
                            case WGGameObjects.FlamewatchTower:
                                turret.SetFaction(WGConst.WintergraspFaction[_wg.GetAttackerTeam()]);
                                break;
                        }
                        _wg.HideNpc(turret);
                    }
                }
                UpdateCreatureAndGo();
            }
        }

        void UpdateCreatureAndGo()
        {
            foreach (var guid in m_CreatureTopList[_wg.GetDefenderTeam()])
            {
                Creature creature = _wg.GetCreature(guid);
                if (creature != null)
                    _wg.HideNpc(creature);
            }

            foreach (var guid in m_CreatureTopList[_wg.GetAttackerTeam()])
            {
                Creature creature = _wg.GetCreature(guid);
                if (creature != null)
                    _wg.ShowNpc(creature, true);
            }

            foreach (var guid in m_CreatureBottomList[_wg.GetDefenderTeam()])
            {
                Creature creature = _wg.GetCreature(guid);
                if (creature != null)
                    _wg.HideNpc(creature);
            }

            foreach (var guid in m_CreatureBottomList[_wg.GetAttackerTeam()])
            {
                Creature creature = _wg.GetCreature(guid);
                if (creature != null)
                    _wg.ShowNpc(creature, true);
            }

            foreach (var guid in m_GameObjectList[_wg.GetDefenderTeam()])
            {
                GameObject obj = _wg.GetGameObject(guid);
                if (obj != null)
                    obj.SetRespawnTime(Time.Day);
            }

            foreach (var guid in m_GameObjectList[_wg.GetAttackerTeam()])
            {
                GameObject obj = _wg.GetGameObject(guid);
                if (obj != null)
                    obj.SetRespawnTime(TimeSpan.Zero);
            }
        }

        public void UpdateTurretAttack(bool disable)
        {
            foreach (var guid in m_TowerCannonBottomList)
            {
                Creature creature = _wg.GetCreature(guid);
                if (creature != null)
                {
                    GameObject build = _wg.GetGameObject(_buildGUID);
                    if (build != null)
                    {
                        if (disable)
                            _wg.HideNpc(creature);
                        else
                            _wg.ShowNpc(creature, true);

                        switch (build.GetEntry())
                        {
                            case WGGameObjects.FortressTower1:
                            case WGGameObjects.FortressTower2:
                            case WGGameObjects.FortressTower3:
                            case WGGameObjects.FortressTower4:
                            {
                                creature.SetFaction(WGConst.WintergraspFaction[_wg.GetDefenderTeam()]);
                                break;
                            }
                            case WGGameObjects.ShadowsightTower:
                            case WGGameObjects.WinterSEdgeTower:
                            case WGGameObjects.FlamewatchTower:
                            {
                                creature.SetFaction(WGConst.WintergraspFaction[_wg.GetAttackerTeam()]);
                                break;
                            }
                        }
                    }
                }
            }

            foreach (var guid in m_TurretTopList)
            {
                Creature creature = _wg.GetCreature(guid);
                if (creature != null)
                {
                    GameObject build = _wg.GetGameObject(_buildGUID);
                    if (build != null)
                    {
                        if (disable)
                            _wg.HideNpc(creature);
                        else
                            _wg.ShowNpc(creature, true);

                        switch (build.GetEntry())
                        {
                            case WGGameObjects.FortressTower1:
                            case WGGameObjects.FortressTower2:
                            case WGGameObjects.FortressTower3:
                            case WGGameObjects.FortressTower4:
                            {
                                creature.SetFaction(WGConst.WintergraspFaction[_wg.GetDefenderTeam()]);
                                break;
                            }
                            case WGGameObjects.ShadowsightTower:
                            case WGGameObjects.WinterSEdgeTower:
                            case WGGameObjects.FlamewatchTower:
                            {
                                creature.SetFaction(WGConst.WintergraspFaction[_wg.GetAttackerTeam()]);
                                break;
                            }
                        }
                    }
                }
            }
        }

        public ObjectGuid GetGUID() { return _buildGUID; }

        // WG object
        BattlefieldWG _wg;

        // Linked gameobject
        ObjectGuid _buildGUID;

        // the team that controls this point
        int _teamControl;

        WGGameObjectBuildingType _type;
        int _worldState;

        WGGameObjectState _state;

        StaticWintergraspTowerInfo _staticTowerInfo;

        // GameObject associations
        List<ObjectGuid>[] m_GameObjectList = new List<ObjectGuid>[SharedConst.PvpTeamsCount];

        // Creature associations
        List<ObjectGuid>[] m_CreatureBottomList = new List<ObjectGuid>[SharedConst.PvpTeamsCount];
        List<ObjectGuid>[] m_CreatureTopList = new List<ObjectGuid>[SharedConst.PvpTeamsCount];
        List<ObjectGuid> m_TowerCannonBottomList = new();
        List<ObjectGuid> m_TurretTopList = new();
    }

    class WGWorkshop
    {
        public WGWorkshop(BattlefieldWG wg, byte type)
        {
            _wg = wg;
            _state = WGGameObjectState.None;
            _teamControl = BattleGroundTeamId.Neutral;
            _staticInfo = WGConst.WorkshopData[type];
        }

        public byte GetId()
        {
            return _staticInfo.WorkshopId;
        }

        public void GiveControlTo(int teamId, bool init = false)
        {
            switch (teamId)
            {
                case BattleGroundTeamId.Neutral:
                {
                    // Send warning message to all player to inform a faction attack to a workshop
                    // alliance / horde attacking a workshop
                    _wg.SendWarning(_teamControl != 0 ? _staticInfo.HordeAttackTextId : _staticInfo.AllianceAttackTextId);
                    break;
                }
                case BattleGroundTeamId.Alliance:
                {
                    // Updating worldstate
                    _state = WGGameObjectState.AllianceIntact;
                    Global.WorldStateMgr.SetValueAndSaveInDb(_staticInfo.WorldStateId, (int)_state, false, _wg.GetMap());

                    // Warning message
                    if (!init)
                        _wg.SendWarning(_staticInfo.AllianceCaptureTextId); // workshop taken - alliance

                    // Found associate graveyard and update it
                    if (_staticInfo.WorkshopId < WGWorkshopIds.KeepWest)
                    {
                        BfGraveyard gy = _wg.GetGraveyardById(_staticInfo.WorkshopId);
                        if (gy != null)
                            gy.GiveControlTo(BattleGroundTeamId.Alliance);
                    }
                    _teamControl = teamId;
                    break;
                }
                case BattleGroundTeamId.Horde:
                {
                    // Update worldstate
                    _state = WGGameObjectState.HordeIntact;
                    Global.WorldStateMgr.SetValueAndSaveInDb(_staticInfo.WorldStateId, (int)_state, false, _wg.GetMap());

                    // Warning message
                    if (!init)
                        _wg.SendWarning(_staticInfo.HordeCaptureTextId); // workshop taken - horde

                    // Update graveyard control
                    if (_staticInfo.WorkshopId < WGWorkshopIds.KeepWest)
                    {
                        BfGraveyard gy = _wg.GetGraveyardById(_staticInfo.WorkshopId);
                        if (gy != null)
                            gy.GiveControlTo(BattleGroundTeamId.Horde);
                    }

                    _teamControl = teamId;
                    break;
                }
            }

            if (!init)
                _wg.UpdateCounterVehicle(false);
        }

        public void UpdateGraveyardAndWorkshop()
        {
            if (_staticInfo.WorkshopId < WGWorkshopIds.Ne)
                GiveControlTo(_wg.GetAttackerTeam(), true);
            else
                GiveControlTo(_wg.GetDefenderTeam(), true);
        }

        public int GetTeamControl() { return _teamControl; }

        BattlefieldWG _wg;                             // Pointer to wintergrasp
        //ObjectGuid _buildGUID;
        WGGameObjectState _state;              // For worldstate
        int _teamControl;                            // Team witch control the workshop

        StaticWintergraspWorkshopInfo _staticInfo;
    }

    class WintergraspCapturePoint : BattleFieldControlZoneHandler
    {
        public WintergraspCapturePoint(BattlefieldWG battlefield, WGWorkshop workshop) : base(battlefield)
        {
            m_Workshop = workshop;
        }

        public override void HandleContestedEventHorde(GameObject controlZone)
        {
            Cypher.Assert(m_Workshop != null);
            base.HandleContestedEventHorde(controlZone);
            m_Workshop.GiveControlTo(BattleGroundTeamId.Neutral);
        }

        public override void HandleContestedEventAlliance(GameObject controlZone)
        {
            Cypher.Assert(m_Workshop != null);
            base.HandleContestedEventAlliance(controlZone);
            m_Workshop.GiveControlTo(BattleGroundTeamId.Neutral);
        }

        public override void HandleProgressEventHorde(GameObject controlZone)
        {
            Cypher.Assert(m_Workshop != null);
            m_Workshop.GiveControlTo(BattleGroundTeamId.Horde);
        }

        public override void HandleProgressEventAlliance(GameObject controlZone)
        {
            Cypher.Assert(m_Workshop != null);
            m_Workshop.GiveControlTo(BattleGroundTeamId.Alliance);
        }

        protected WGWorkshop m_Workshop;
    }

    class BfGraveyardWG : BfGraveyard
    {
        public BfGraveyardWG(BattlefieldWG battlefield)
            : base(battlefield)
        {
            m_Bf = battlefield;
            m_GossipTextId = 0;
        }

        public void SetTextId(int textid) { m_GossipTextId = textid; }
        int GetTextId() { return m_GossipTextId; }

        protected int m_GossipTextId;
    }

    [Script]
    class Battlefield_wintergrasp : BattlefieldScript
    {
        public Battlefield_wintergrasp() : base("battlefield_wg") { }

        public override BattleField GetBattlefield(Map map)
        {
            return new BattlefieldWG(map);
        }
    }

    [Script]
    class npc_wg_give_promotion_credit : ScriptedAI
    {
        public npc_wg_give_promotion_credit(Creature creature) : base(creature) { }

        public override void JustDied(Unit killer)
        {
            if (killer == null || !killer.IsPlayer())
                return;

            BattlefieldWG wintergrasp = (BattlefieldWG)Global.BattleFieldMgr.GetBattlefieldByBattleId(killer.GetMap(), BattlefieldIds.WG);
            if (wintergrasp == null)
                return;

            wintergrasp.HandlePromotion(killer.ToPlayer(), me);
        }
    }
}
