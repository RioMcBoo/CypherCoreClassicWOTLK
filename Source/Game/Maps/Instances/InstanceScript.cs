﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.DataStorage;
using Game.Entities;
using Game.Groups;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Maps
{
    public class InstanceScript : ZoneScript
    {
        public InstanceScript(InstanceMap map)
        {
            instance = map;
            _instanceSpawnGroups = Global.ObjectMgr.GetInstanceSpawnGroupsForMap(map.GetId());
        }

        public virtual bool IsEncounterInProgress()
        {
            foreach (var boss in bosses.Values)
            {
                if (boss.state == EncounterState.InProgress)
                    return true;
            }

            return false;
        }

        public override void OnCreatureCreate(Creature creature)
        {
            AddObject(creature, true);
            AddMinion(creature, true);
        }

        public override void OnCreatureRemove(Creature creature)
        {
            AddObject(creature, false);
            AddMinion(creature, false);
        }

        public override void OnGameObjectCreate(GameObject go)
        {
            AddObject(go, true);
            AddDoor(go, true);
        }

        public override void OnGameObjectRemove(GameObject go)
        {
            AddObject(go, false);
            AddDoor(go, false);
        }

        public ObjectGuid GetObjectGuid(int type)
        {
            return _objectGuids.LookupByKey(type);
        }

        public override ObjectGuid GetGuidData(int type)
        {
            return GetObjectGuid(type);
        }

        public void SetHeaders(string dataHeaders)
        {
            headers = dataHeaders;
        }

        public void LoadBossBoundaries(BossBoundaryEntry[] data)
        {
            foreach (BossBoundaryEntry entry in data)
            {
                if (entry.BossId < bosses.Count)
                    bosses[entry.BossId].boundary.Add(entry.Boundary);
            }
        }

        public void LoadMinionData(params MinionData[] data)
        {
            foreach (var minion in data)
            {
                if (minion.entry == 0)
                    continue;

                if (minion.bossId < bosses.Count)
                    minions.Add(minion.entry, new MinionInfo(bosses[minion.bossId]));
            }

            Log.outDebug(LogFilter.Scripts, 
                $"InstanceScript.LoadMinionData: {minions.Count} minions loaded.");
        }

        public void LoadDoorData(params DoorData[] data)
        {
            foreach (var door in data)
            {
                if (door.entry == 0)
                    continue;

                if (door.bossId < bosses.Count)
                    doors.Add(door.entry, new DoorInfo(bosses[door.bossId], door.Behavior));
            }

            Log.outDebug(LogFilter.Scripts,
                $"InstanceScript.LoadDoorData: {doors.Count} doors loaded.");
        }

        public void LoadObjectData(ObjectData[] creatureData, ObjectData[] gameObjectData)
        {
            if (creatureData != null)
                LoadObjectData(creatureData, _creatureInfo);

            if (gameObjectData != null)
                LoadObjectData(gameObjectData, _gameObjectInfo);

            Log.outDebug(LogFilter.Scripts, 
                $"InstanceScript.LoadObjectData: " +
                $"{_creatureInfo.Count + _gameObjectInfo.Count} objects loaded.");
        }

        void LoadObjectData(ObjectData[] objectData, Dictionary<int, int> objectInfo)
        {
            foreach (var data in objectData)
            {
                Cypher.Assert(!objectInfo.ContainsKey(data.entry));
                objectInfo[data.entry] = data.type;
            }
        }

        public void LoadDungeonEncounterData(DungeonEncounterData[] encounters)
        {
            foreach (DungeonEncounterData encounter in encounters)
                LoadDungeonEncounterData(encounter.BossId, encounter.DungeonEncounterId);
        }

        void LoadDungeonEncounterData(int bossId, int[] dungeonEncounterIds)
        {
            if (bossId < bosses.Count)
            {
                for (int i = 0; i < dungeonEncounterIds.Length && i < MapConst.MaxDungeonEncountersPerBoss; ++i)
                    bosses[bossId].DungeonEncounters[i] = CliDB.DungeonEncounterStorage.LookupByKey(dungeonEncounterIds[i]);
            }
        }

        public virtual void UpdateDoorState(GameObject door)
        {
            var range = doors[door.GetEntry()];
            if (range.Empty())
                return;

            bool open = true;
            foreach (var info in range)
            {
                if (!open)
                    break;

                switch (info.Behavior)
                {
                    case EncounterDoorBehavior.OpenWhenNotInProgress:
                        open = info.bossInfo.state != EncounterState.InProgress;
                        break;
                    case EncounterDoorBehavior.OpenWhenDone:
                        open = info.bossInfo.state == EncounterState.Done;
                        break;
                    case EncounterDoorBehavior.OpenWhenInProgress:
                        open = info.bossInfo.state == EncounterState.InProgress;
                        break;
                    case EncounterDoorBehavior.OpenWhenNotDone:
                        open = info.bossInfo.state != EncounterState.Done;
                        break;
                    default:
                        break;
                }
            }

            door.SetGoState(open ? GameObjectState.Active : GameObjectState.Ready);
        }

        void UpdateMinionState(Creature minion, EncounterState state)
        {
            switch (state)
            {
                case EncounterState.NotStarted:
                    if (!minion.IsAlive())
                        minion.Respawn();
                    else if (minion.IsInCombat())
                        minion.GetAI().EnterEvadeMode();
                    break;
                case EncounterState.InProgress:
                    if (!minion.IsAlive())
                        minion.Respawn();
                    else if (minion.GetVictim() == null)
                        minion.GetAI().DoZoneInCombat();
                    break;
                default:
                    break;
            }
        }

        enum states { Block, Spawn, ForceBlock };

        void UpdateSpawnGroups()
        {
            if (_instanceSpawnGroups.Empty())
                return;

            Dictionary<int, states> newStates = new();
            foreach (var info in _instanceSpawnGroups)
            {
                if (!newStates.ContainsKey(info.SpawnGroupId))
                    newStates[info.SpawnGroupId] = 0;// makes sure there's a BLOCK value in the map

                if (newStates[info.SpawnGroupId] == states.ForceBlock) // nothing will change this
                    continue;

                if (((1 << (int)GetBossState(info.BossStateId)) & (int)info.BossStates) == 0)
                    continue;

                if (((instance.GetTeamIdInInstance() == BattleGroundTeamId.Alliance) && info.Flags.HasFlag(InstanceSpawnGroupFlags.HordeOnly))
                    || ((instance.GetTeamIdInInstance() == BattleGroundTeamId.Horde) && info.Flags.HasFlag(InstanceSpawnGroupFlags.AllianceOnly)))
                    continue;

                if (info.Flags.HasAnyFlag(InstanceSpawnGroupFlags.BlockSpawn))
                    newStates[info.SpawnGroupId] = states.ForceBlock;

                else if (info.Flags.HasAnyFlag(InstanceSpawnGroupFlags.ActivateSpawn))
                    newStates[info.SpawnGroupId] = states.Spawn;
            }

            foreach (var pair in newStates)
            {
                int groupId = pair.Key;
                bool doSpawn = pair.Value == states.Spawn;
                if (instance.IsSpawnGroupActive(groupId) == doSpawn)
                    continue; // nothing to do here
                              // if we should spawn group, then spawn it...
                if (doSpawn)
                    instance.SpawnGroupSpawn(groupId, instance != null);
                else // otherwise, set it as inactive so it no longer respawns (but don't despawn it)
                    instance.SetSpawnGroupInactive(groupId);
            }
        }

        public BossInfo GetBossInfo(int id)
        {
            Cypher.Assert(id < bosses.Count);
            return bosses[id];
        }

        void AddObject(Creature obj, bool add)
        {
            if (_creatureInfo.ContainsKey(obj.GetEntry()))
                AddObject(obj, _creatureInfo[obj.GetEntry()], add);
        }

        void AddObject(GameObject obj, bool add)
        {
            if (_gameObjectInfo.ContainsKey(obj.GetEntry()))
                AddObject(obj, _gameObjectInfo[obj.GetEntry()], add);
        }

        void AddObject(WorldObject obj, int type, bool add)
        {
            if (add)
                _objectGuids[type] = obj.GetGUID();
            else
            {
                var guid = _objectGuids.LookupByKey(type);
                if (!guid.IsEmpty() && guid == obj.GetGUID())
                    _objectGuids.Remove(type);
            }
        }

        public virtual void AddDoor(GameObject door, bool add)
        {
            var range = doors[door.GetEntry()];
            if (range.Empty())
                return;

            foreach (var data in range)
            {
                if (add)
                    data.bossInfo.door[(int)data.Behavior].Add(door.GetGUID());
                else
                    data.bossInfo.door[(int)data.Behavior].Remove(door.GetGUID());
            }

            if (add)
                UpdateDoorState(door);
        }

        public void AddMinion(Creature minion, bool add)
        {
            var minionInfo = minions.LookupByKey(minion.GetEntry());
            if (minionInfo == null)
                return;

            if (add)
                minionInfo.bossInfo.minion.Add(minion.GetGUID());
            else
                minionInfo.bossInfo.minion.Remove(minion.GetGUID());
        }

        // Triggers a GameEvent
        // * If source is null then event is triggered for each player in the instance as "source"
        public override void TriggerGameEvent(int gameEventId, WorldObject source = null, WorldObject target = null)
        {
            if (source != null)
            {
                base.TriggerGameEvent(gameEventId, source, target);
                return;
            }

            ProcessEvent(target, gameEventId, source);
            instance.DoOnPlayers(player => GameEvents.TriggerForPlayer(gameEventId, player));

            GameEvents.TriggerForMap(gameEventId, instance);
        }

        public Creature GetCreature(int type)
        {
            return instance.GetCreature(GetObjectGuid(type));
        }

        public GameObject GetGameObject(int type)
        {
            return instance.GetGameObject(GetObjectGuid(type));
        }

        public virtual bool SetBossState(int id, EncounterState state)
        {
            if (id < bosses.Count)
            {
                BossInfo bossInfo = bosses[id];
                if (bossInfo.state == EncounterState.ToBeDecided) // loading
                {
                    bossInfo.state = state;
                    Log.outDebug(LogFilter.Scripts, 
                        $"InstanceScript: Initialize boss {id} state as {state} " +
                        $"(map {instance.GetId()}, {instance.GetInstanceId()}).");
                    return false;
                }
                else
                {
                    if (bossInfo.state == state)
                        return false;

                    if (bossInfo.state == EncounterState.Done)
                    {
                        Log.outError(LogFilter.Maps, 
                            $"InstanceScript: Tried to set instance boss {id} " +
                            $"state from {bossInfo.state} back to {state} " +
                            $"for map {instance.GetId()}, instance id {instance.GetInstanceId()}. " +
                            $"Blocked!");
                        return false;
                    }

                    if (state == EncounterState.Done)
                    {
                        foreach (var guid in bossInfo.minion)
                        {
                            Creature minion = instance.GetCreature(guid);
                            if (minion != null)
                            {
                                if (minion.IsWorldBoss() && minion.IsAlive())
                                    return false;
                            }
                        }
                    }

                    DungeonEncounterRecord dungeonEncounter = null;
                    switch (state)
                    {
                        case EncounterState.InProgress:
                        {
                            TimeSpan resInterval = GetCombatResurrectionChargeInterval();
                            InitializeCombatResurrections(1, resInterval);
                            SendEncounterStart(1, 9, resInterval, resInterval);

                            instance.DoOnPlayers(player => player.AtStartOfEncounter(EncounterType.DungeonEncounter));
                            break;
                        }
                        case EncounterState.Fail:
                            ResetCombatResurrections();
                            SendEncounterEnd();

                            instance.DoOnPlayers(player => player.AtEndOfEncounter(EncounterType.DungeonEncounter));
                            break;
                        case EncounterState.Done:
                            ResetCombatResurrections();
                            SendEncounterEnd();
                            dungeonEncounter = bossInfo.GetDungeonEncounterForDifficulty(instance.GetDifficultyID());
                            if (dungeonEncounter != null)
                            {
                                instance.DoOnPlayers(player =>
                                {
                                    if (!player.IsLockedToDungeonEncounter(dungeonEncounter.Id))
                                        player.UpdateCriteria(CriteriaType.DefeatDungeonEncounterWhileElegibleForLoot, dungeonEncounter.Id);
                                });

                                DoUpdateCriteria(CriteriaType.DefeatDungeonEncounter, dungeonEncounter.Id);
                                SendBossKillCredit(dungeonEncounter.Id);

                                UpdateLfgEncounterState(bossInfo);
                            }

                            instance.DoOnPlayers(player => player.AtEndOfEncounter(EncounterType.DungeonEncounter));
                            break;
                        default:
                            break;
                    }

                    bossInfo.state = state;
                    if (dungeonEncounter != null)
                        instance.UpdateInstanceLock(new UpdateBossStateSaveDataEvent(dungeonEncounter, id, state));
                }

                foreach (var doorSet in bossInfo.door)
                {
                    foreach (ObjectGuid doorGUID in doorSet)
                    {
                        GameObject door = instance.GetGameObject(doorGUID);
                        if (door != null)
                            UpdateDoorState(door);
                    }
                }

                foreach (var guid in bossInfo.minion.ToArray())
                {
                    Creature minion = instance.GetCreature(guid);
                    if (minion != null)
                        UpdateMinionState(minion, state);
                }

                UpdateSpawnGroups();
                return true;
            }
            return false;
        }

        public bool _SkipCheckRequiredBosses(Player player = null)
        {
            return player != null && player.GetSession().HasPermission(RBACPermissions.SkipCheckInstanceRequiredBosses);
        }

        public virtual void Create()
        {
            for (int i = 0; i < bosses.Count; ++i)
                SetBossState(i, EncounterState.NotStarted);

            UpdateSpawnGroups();
        }

        public void Load(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                OutLoadInstDataFail();
                return;
            }

            OutLoadInstData(data);

            InstanceScriptDataReader reader = new(this);
            if (reader.Load(data) == InstanceScriptDataReader.Result.Ok)
            {
                // in loot-based lockouts instance can be loaded with later boss marked as killed without preceding bosses
                // but we still need to have them alive
                for (int i = 0; i < bosses.Count; ++i)
                {
                    if (bosses[i].state == EncounterState.Done && !CheckRequiredBosses(i))
                        bosses[i].state = EncounterState.NotStarted;
                }

                UpdateSpawnGroups();
                AfterDataLoad();
            }
            else
                OutLoadInstDataFail();

            OutLoadInstDataComplete();
        }

        public string GetSaveData()
        {
            OutSaveInstData();

            InstanceScriptDataWriter writer = new(this);

            writer.FillData();

            OutSaveInstDataComplete();

            return writer.GetString();
        }

        public string UpdateBossStateSaveData(string oldData, UpdateBossStateSaveDataEvent saveEvent)
        {
            if (!instance.GetMapDifficulty().IsUsingEncounterLocks)
                return GetSaveData();

            InstanceScriptDataWriter writer = new(this);
            writer.FillDataFrom(oldData);
            writer.SetBossState(saveEvent);
            return writer.GetString();
        }

        public string UpdateAdditionalSaveData(string oldData, UpdateAdditionalSaveDataEvent saveEvent)
        {
            if (!instance.GetMapDifficulty().IsUsingEncounterLocks)
                return GetSaveData();

            InstanceScriptDataWriter writer = new(this);
            writer.FillDataFrom(oldData);
            writer.SetAdditionalData(saveEvent);
            return writer.GetString();
        }

        public int? GetEntranceLocationForCompletedEncounters(uint completedEncountersMask)
        {
            if (!instance.GetMapDifficulty().IsUsingEncounterLocks)
                return _entranceId;

            return ComputeEntranceLocationForCompletedEncounters(completedEncountersMask);
        }

        public virtual int? ComputeEntranceLocationForCompletedEncounters(uint completedEncountersMask)
        {
            return null;
        }

        public void HandleGameObject(ObjectGuid guid, bool open, GameObject go = null)
        {
            if (go == null)
                go = instance.GetGameObject(guid);
            if (go != null)
                go.SetGoState(open ? GameObjectState.Active : GameObjectState.Ready);
            else
                Log.outDebug(LogFilter.Scripts, "InstanceScript: HandleGameObject failed");
        }

        public void DoUseDoorOrButton(ObjectGuid uiGuid, Milliseconds withRestoreTime = default, bool useAlternativeState = false)
        {
            if (uiGuid.IsEmpty())
                return;

            GameObject go = instance.GetGameObject(uiGuid);
            if (go != null)
            {
                if (go.GetGoType() == GameObjectTypes.Door || go.GetGoType() == GameObjectTypes.Button)
                {
                    if (go.GetLootState() == LootState.Ready)
                        go.UseDoorOrButton(withRestoreTime, useAlternativeState);
                    else if (go.GetLootState() == LootState.Activated)
                        go.ResetDoorOrButton();
                }
                else
                {
                    Log.outError(LogFilter.Scripts,
                        $"InstanceScript: DoUseDoorOrButton can't use gameobject entry {go.GetEntry()}, " +
                        $"because Type is {go.GetGoType()}.");
                }
            }
            else
                Log.outDebug(LogFilter.Scripts, "InstanceScript: DoUseDoorOrButton failed");
        }

        void DoCloseDoorOrButton(ObjectGuid guid)
        {
            if (guid.IsEmpty())
                return;

            GameObject go = instance.GetGameObject(guid);
            if (go != null)
            {
                if (go.GetGoType() == GameObjectTypes.Door || go.GetGoType() == GameObjectTypes.Button)
                {
                    if (go.GetLootState() == LootState.Activated)
                        go.ResetDoorOrButton();
                }
                else
                {
                    Log.outError(LogFilter.Scripts,
                        $"InstanceScript: DoCloseDoorOrButton can't use gameobject entry {go.GetEntry()}, " +
                        $"because Type is {go.GetGoType()}.");
                }
            }
            else
                Log.outDebug(LogFilter.Scripts, "InstanceScript: DoCloseDoorOrButton failed");
        }

        public void DoRespawnGameObject(ObjectGuid guid, TimeSpan timeToDespawn)
        {
            GameObject go = instance.GetGameObject(guid);
            if (go != null)
            {
                switch (go.GetGoType())
                {
                    case GameObjectTypes.Door:
                    case GameObjectTypes.Button:
                    case GameObjectTypes.Trap:
                    case GameObjectTypes.FishingNode:
                        // not expect any of these should ever be handled
                        Log.outError(LogFilter.Scripts, 
                            $"InstanceScript: DoRespawnGameObject " +
                            $"can't respawn gameobject entry {go.GetEntry()}, " +
                            $"because Type is {go.GetGoType()}.");
                        return;
                    default:
                        break;
                }

                if (go.IsSpawned())
                    return;

                go.SetRespawnTime(timeToDespawn);
            }
            else
                Log.outDebug(LogFilter.Scripts, "InstanceScript: DoRespawnGameObject failed");
        }

        public void DoUpdateWorldState(int worldStateId, WorldStateValue value)
        {
            Global.WorldStateMgr.SetValue(worldStateId, value, false, instance);
        }

        // Send Notify to all players in instance
        void DoSendNotifyToInstance(string format, params object[] args)
        {
            instance.DoOnPlayers(player => player.GetSession()?.SendNotification(format, args));
        }

        // Update Achievement Criteria for all players in instance
        public void DoUpdateCriteria(CriteriaType type, int miscValue1 = 0, int miscValue2 = 0, Unit unit = null)
        {
            instance.DoOnPlayers(player => player.UpdateCriteria(type, miscValue1, miscValue2, 0, unit));
        }

        // Remove Auras due to Spell on all players in instance
        public void DoRemoveAurasDueToSpellOnPlayers(int spell, bool includePets = false, bool includeControlled = false)
        {
            instance.DoOnPlayers(player => DoRemoveAurasDueToSpellOnPlayer(player, spell, includePets, includeControlled));
        }

        public void DoRemoveAurasDueToSpellOnPlayer(Player player, int spell, bool includePets = false, bool includeControlled = false)
        {
            if (player == null)
                return;

            player.RemoveAurasDueToSpell(spell);

            if (!includePets)
                return;

            for (var i = 0; i < SharedConst.MaxSummonSlot; ++i)
            {
                ObjectGuid summonGUID = player.m_SummonSlot[i];
                if (!summonGUID.IsEmpty())
                {
                    Creature summon = instance.GetCreature(summonGUID);
                    if (summon != null)
                        summon.RemoveAurasDueToSpell(spell);
                }
            }

            if (!includeControlled)
                return;

            for (var i = 0; i < player.m_Controlled.Count; ++i)
            {
                Unit controlled = player.m_Controlled[i];
                if (controlled != null)
                {
                    if (controlled.IsInWorld && controlled.IsCreature())
                        controlled.RemoveAurasDueToSpell(spell);
                }
            }
        }

        // Cast spell on all players in instance
        public void DoCastSpellOnPlayers(int spell, bool includePets = false, bool includeControlled = false)
        {
            instance.DoOnPlayers(player => DoCastSpellOnPlayer(player, spell, includePets, includeControlled));
        }

        public void DoCastSpellOnPlayer(Player player, int spell, bool includePets = false, bool includeControlled = false)
        {
            if (player == null)
                return;

            player.CastSpell(player, spell, true);

            if (!includePets)
                return;

            for (var i = 0; i < SharedConst.MaxSummonSlot; ++i)
            {
                ObjectGuid summonGUID = player.m_SummonSlot[i];
                if (!summonGUID.IsEmpty())
                {
                    Creature summon = instance.GetCreature(summonGUID);
                    if (summon != null)
                        summon.CastSpell(player, spell, true);
                }
            }

            if (!includeControlled)
                return;

            for (var i = 0; i < player.m_Controlled.Count; ++i)
            {
                Unit controlled = player.m_Controlled[i];
                if (controlled != null)
                {
                    if (controlled.IsInWorld && controlled.IsCreature())
                        controlled.CastSpell(player, spell, true);
                }
            }
        }

        public DungeonEncounterRecord GetBossDungeonEncounter(int id)
        {
            return id < bosses.Count ? bosses[id].GetDungeonEncounterForDifficulty(instance.GetDifficultyID()) : null;
        }

        public DungeonEncounterRecord GetBossDungeonEncounter(Creature creature)
        {
            BossAI bossAi = (BossAI)creature.GetAI();
            if (bossAi != null)
                return GetBossDungeonEncounter(bossAi.GetBossId());

            return null;
        }
        
        public virtual bool CheckAchievementCriteriaMeet(int criteria_id, Player source, Unit target = null, uint miscvalue1 = 0)
        {
            Log.outError(LogFilter.Server, 
                $"Achievement system call CheckAchievementCriteriaMeet " +
                $"but instance script for map {instance.GetId()} " +
                $"not have implementation for achievement criteria {criteria_id}");
            return false;
        }

        public bool IsEncounterCompleted(int dungeonEncounterId)
        {
            for (int i = 0; i < bosses.Count; ++i)
            {
                for (var j = 0; j < bosses[i].DungeonEncounters.Length; ++j)
                {
                    if (bosses[i].DungeonEncounters[j] != null && bosses[i].DungeonEncounters[j].Id == dungeonEncounterId)
                        return bosses[i].state == EncounterState.Done;
                }
            }

            return false;
        }

        public bool IsEncounterCompletedInMaskByBossId(uint completedEncountersMask, int bossId)
        {
            DungeonEncounterRecord dungeonEncounter = GetBossDungeonEncounter(bossId);
            if (dungeonEncounter != null)
            {
                if ((completedEncountersMask & (1 << dungeonEncounter.Bit)) != 0)
                    return bosses[bossId].state == EncounterState.Done;
            }

            return false;
        }
        
        public void SetEntranceLocation(int worldSafeLocationId)
        {
            _entranceId = worldSafeLocationId;
            _temporaryEntranceId = 0;
        }

        public void SendEncounterUnit(EncounterFrameType type, Unit unit = null, int? param1 = null, int? param2 = null)
        {
            switch (type)
            {
                case EncounterFrameType.Engage:
                    if (unit == null)
                        return;

                    InstanceEncounterEngageUnit encounterEngageMessage = new();
                    encounterEngageMessage.Unit = unit.GetGUID();
                    encounterEngageMessage.TargetFramePriority = (byte)param1.GetValueOrDefault(0);
                    instance.SendToPlayers(encounterEngageMessage);
                    break;
                case EncounterFrameType.Disengage:
                    if (unit == null)
                        return;

                    InstanceEncounterDisengageUnit encounterDisengageMessage = new();
                    encounterDisengageMessage.Unit = unit.GetGUID();
                    instance.SendToPlayers(encounterDisengageMessage);
                    break;
                case EncounterFrameType.UpdatePriority:
                    if (unit == null)
                        return;

                    InstanceEncounterChangePriority encounterChangePriorityMessage = new();
                    encounterChangePriorityMessage.Unit = unit.GetGUID();
                    encounterChangePriorityMessage.TargetFramePriority = (byte)param1.GetValueOrDefault(0);
                    instance.SendToPlayers(encounterChangePriorityMessage);
                    break;
                case EncounterFrameType.AddTimer:
                {
                    InstanceEncounterTimerStart instanceEncounterTimerStart = new();
                    instanceEncounterTimerStart.TimeRemaining = param1.GetValueOrDefault(0);
                    instance.SendToPlayers(instanceEncounterTimerStart);
                    break;
                }
                case EncounterFrameType.EnableObjective:
                {
                    InstanceEncounterObjectiveStart instanceEncounterObjectiveStart = new();
                    instanceEncounterObjectiveStart.ObjectiveID = param1.GetValueOrDefault(0);
                    instance.SendToPlayers(instanceEncounterObjectiveStart);
                    break;
                }
                case EncounterFrameType.UpdateObjective:
                {
                    InstanceEncounterObjectiveUpdate instanceEncounterObjectiveUpdate = new();
                    instanceEncounterObjectiveUpdate.ObjectiveID = param1.GetValueOrDefault(0);
                    instanceEncounterObjectiveUpdate.ProgressAmount = param2.GetValueOrDefault(0);
                    instance.SendToPlayers(instanceEncounterObjectiveUpdate);
                    break;
                }
                case EncounterFrameType.DisableObjective:
                {
                    InstanceEncounterObjectiveComplete instanceEncounterObjectiveComplete = new();
                    instanceEncounterObjectiveComplete.ObjectiveID = param1.GetValueOrDefault(0);
                    instance.SendToPlayers(instanceEncounterObjectiveComplete);
                    break;
                }
                case EncounterFrameType.PhaseShiftChanged:
                {
                    InstanceEncounterPhaseShiftChanged instanceEncounterPhaseShiftChanged = new();
                    instance.SendToPlayers(instanceEncounterPhaseShiftChanged);
                    break;
                }
                default:
                    break;
            }
        }

        void SendEncounterStart(uint inCombatResCount = 0, uint maxInCombatResCount = 0, TimeSpan inCombatResChargeRecovery = default, TimeSpan nextCombatResChargeTime = default)
        {
            InstanceEncounterStart encounterStartMessage = new();
            encounterStartMessage.InCombatResCount = inCombatResCount;
            encounterStartMessage.MaxInCombatResCount = maxInCombatResCount;
            encounterStartMessage.CombatResChargeRecovery = (Milliseconds)inCombatResChargeRecovery;
            encounterStartMessage.NextCombatResChargeTime = (Milliseconds)nextCombatResChargeTime;

            instance.SendToPlayers(encounterStartMessage);
        }

        void SendEncounterEnd()
        {
            instance.SendToPlayers(new InstanceEncounterEnd());
        }

        public void SendBossKillCredit(int encounterId)
        {
            BossKill bossKillCreditMessage = new();
            bossKillCreditMessage.DungeonEncounterID = encounterId;

            instance.SendToPlayers(bossKillCreditMessage);
        }

        void UpdateLfgEncounterState(BossInfo bossInfo)
        {
            foreach (var player in instance.GetPlayers())
            {
                if (player != null)
                {
                    Group grp = player.GetGroup();
                    if (grp != null && grp.IsLFGGroup())
                    {
                        Global.LFGMgr.OnDungeonEncounterDone(grp.GetGUID(), bossInfo.DungeonEncounters.Select(entry => entry.Id).ToArray(), instance);
                        break;
                    }
                }
            }
        }

        void UpdatePhasing()
        {
            instance.DoOnPlayers(player => PhasingHandler.SendToPlayer(player));
        }

        public void UpdateCombatResurrection(TimeSpan diff)
        {
            if (!_combatResurrectionTimerStarted)
                return;

            if (_combatResurrectionTimer <= diff)
                AddCombatResurrectionCharge();
            else
                _combatResurrectionTimer -= diff;
        }

        void InitializeCombatResurrections(byte charges = 1, TimeSpan interval = default)
        {
            _combatResurrectionCharges = charges;
            if (interval == default)
                return;

            _combatResurrectionTimer = interval;
            _combatResurrectionTimerStarted = true;
        }

        public void AddCombatResurrectionCharge()
        {
            ++_combatResurrectionCharges;
            _combatResurrectionTimer = GetCombatResurrectionChargeInterval();
            _combatResurrectionTimerStarted = true;

            var gainCombatResurrectionCharge = new InstanceEncounterGainCombatResurrectionCharge();
            gainCombatResurrectionCharge.InCombatResCount = _combatResurrectionCharges;
            gainCombatResurrectionCharge.CombatResChargeRecovery = (Milliseconds)_combatResurrectionTimer;
            instance.SendToPlayers(gainCombatResurrectionCharge);
        }

        public void UseCombatResurrection()
        {
            --_combatResurrectionCharges;

            instance.SendToPlayers(new InstanceEncounterInCombatResurrection());
        }

        public void ResetCombatResurrections()
        {
            _combatResurrectionCharges = 0;
            _combatResurrectionTimer = TimeSpan.Zero;
            _combatResurrectionTimerStarted = false;
        }

        public TimeSpan GetCombatResurrectionChargeInterval()
        {
            TimeSpan interval = default;
            int playerCount = instance.GetPlayers().Count;
            if (playerCount != 0)
                interval = Time.SpanFromMinutes(90) / playerCount;

            return interval;
        }

        public bool InstanceHasScript(WorldObject obj, string scriptName)
        {
            InstanceMap instance = obj.GetMap().ToInstanceMap();
            if (instance != null)
                return instance.GetScriptName() == scriptName;

            return false;
        }

        public virtual void Update(TimeSpan diff) { }

        // Called when a player successfully enters the instance.
        public virtual void OnPlayerEnter(Player player) { }

        // Called when a player successfully leaves the instance.
        public virtual void OnPlayerLeave(Player player) { }

        // Return wether server allow two side groups or not
        public bool ServerAllowsTwoSideGroups() { return WorldConfig.Values[WorldCfg.AllowTwoSideInteractionGroup].Bool; }

        public EncounterState GetBossState(int id) { return id < bosses.Count ? bosses[id].state : EncounterState.ToBeDecided; }
        public List<AreaBoundary> GetBossBoundary(int id) { return id < bosses.Count ? bosses[id].boundary : null; }

        public virtual bool CheckRequiredBosses(int bossId, Player player = null) { return true; }

        // Sets a temporary entrance that does not get saved to db
        public void SetTemporaryEntranceLocation(int worldSafeLocationId) { _temporaryEntranceId = worldSafeLocationId; }

        // Get's the current entrance id
        public int GetEntranceLocation() { return _temporaryEntranceId != 0 ? _temporaryEntranceId : _entranceId; }

        // Only used by areatriggers that inherit from OnlyOnceAreaTriggerScript
        public void MarkAreaTriggerDone(int id) { _activatedAreaTriggers.Add(id); }
        public void ResetAreaTriggerDone(int id) { _activatedAreaTriggers.Remove(id); }
        public bool IsAreaTriggerDone(int id) { return _activatedAreaTriggers.Contains(id); }

        public int GetEncounterCount() { return bosses.Count; }

        public byte GetCombatResurrectionCharges() { return _combatResurrectionCharges; }

        public void RegisterPersistentScriptValue(PersistentInstanceScriptValueBase value) { _persistentScriptValues.Add(value); }

        public string GetHeader() { return headers; }

        public List<PersistentInstanceScriptValueBase> GetPersistentScriptValues() { return _persistentScriptValues; }

        public void SetBossNumber(int number)
        {
            for (int i = 0; i < number; ++i)
                bosses.Add(i, new BossInfo());
        }

        public void OutSaveInstData() 
        { 
            Log.outDebug(LogFilter.Scripts, 
                $"Saving Instance Data for Instance {instance.GetMapName()} " +
                $"(Map {instance.GetId()}, Instance Id {instance.GetInstanceId()})"); 
        }

        public void OutSaveInstDataComplete() 
        { 
            Log.outDebug(LogFilter.Scripts, 
                $"Saving Instance Data for Instance {instance.GetMapName()} " +
                $"(Map {instance.GetId()}, Instance Id {instance.GetInstanceId()}) completed."); 
        }

        public void OutLoadInstData(string input) 
        { 
            Log.outDebug(LogFilter.Scripts, 
                $"Loading Instance Data for Instance {instance.GetMapName()} " +
                $"(Map {instance.GetId()}, Instance Id {instance.GetInstanceId()}). " +
                $"Input is '{input}'"); 
        }

        public void OutLoadInstDataComplete() 
        { 
            Log.outDebug(LogFilter.Scripts, 
                $"Instance Data Load for Instance {instance.GetMapName()} " +
                $"(Map {instance.GetId()}, Instance Id: {instance.GetInstanceId()}) is complete.");
        }

        public void OutLoadInstDataFail() 
        { 
            Log.outDebug(LogFilter.Scripts, 
                $"Unable to load Instance Data for Instance {instance.GetMapName()} " +
                $"(Map {instance.GetId()}, Instance Id: {instance.GetInstanceId()})."); 
        }

        public IReadOnlyList<InstanceSpawnGroupInfo> GetInstanceSpawnGroups() { return _instanceSpawnGroups; }

        // Override this function to validate all additional data loads
        public virtual void AfterDataLoad() { }

        public InstanceMap instance;
        string headers;
        Dictionary<int, BossInfo> bosses = new();
        List<PersistentInstanceScriptValueBase> _persistentScriptValues = new();
        MultiMap<int, DoorInfo> doors = new();
        Dictionary<int, MinionInfo> minions = new();
        Dictionary<int, int> _creatureInfo = new();
        Dictionary<int, int> _gameObjectInfo = new();
        Dictionary<int, ObjectGuid> _objectGuids = new();
        IReadOnlyList<InstanceSpawnGroupInfo> _instanceSpawnGroups;
        List<int> _activatedAreaTriggers = new();
        int _entranceId;
        int _temporaryEntranceId;
        TimeSpan _combatResurrectionTimer;
        byte _combatResurrectionCharges; // the counter for available battle resurrections
        bool _combatResurrectionTimerStarted;
    }

    public class DungeonEncounterData
    {
        public int BossId;
        public int[] DungeonEncounterId = new int[4];

        public DungeonEncounterData(int bossId, params int[] dungeonEncounterIds)
        {
            BossId = bossId;
            DungeonEncounterId = dungeonEncounterIds;
        }
    }

    public class DoorData
    {
        public DoorData(int _entry, int _bossid, EncounterDoorBehavior behavior)
        {
            entry = _entry;
            bossId = _bossid;
            Behavior = behavior;
        }

        public int entry;
        public int bossId;
        public EncounterDoorBehavior Behavior;
    }

    public class BossBoundaryEntry
    {
        public BossBoundaryEntry(int bossId, AreaBoundary boundary)
        {
            BossId = bossId;
            Boundary = boundary;
        }

        public int BossId;
        public AreaBoundary Boundary;
    }

    public class MinionData
    {
        public MinionData(int _entry, int _bossid)
        {
            entry = _entry;
            bossId = _bossid;
        }

        public int entry;
        public int bossId;
    }

    public struct ObjectData
    {
        public ObjectData(int _entry, int _type)
        {
            entry = _entry;
            type = _type;
        }

        public int entry;
        public int type;
    }

    public class BossInfo
    {
        public EncounterState state;
        public List<ObjectGuid>[] door = new List<ObjectGuid>[(int)EncounterDoorBehavior.Max];
        public List<ObjectGuid> minion = new();
        public List<AreaBoundary> boundary = new();
        public DungeonEncounterRecord[] DungeonEncounters = new DungeonEncounterRecord[MapConst.MaxDungeonEncountersPerBoss];

        public BossInfo()
        {
            state = EncounterState.ToBeDecided;
            for (var i = 0; i < (int)EncounterDoorBehavior.Max; ++i)
                door[i] = new List<ObjectGuid>();
        }

        public DungeonEncounterRecord GetDungeonEncounterForDifficulty(Difficulty difficulty)
        {
            return DungeonEncounters.FirstOrDefault(dungeonEncounter => 
            dungeonEncounter?.DifficultyID == 0 || (Difficulty)dungeonEncounter?.DifficultyID == difficulty
            );
        }
    }

    class DoorInfo
    {
        public DoorInfo(BossInfo _bossInfo, EncounterDoorBehavior behavior)
        {
            bossInfo = _bossInfo;
            Behavior = behavior;
        }

        public BossInfo bossInfo;
        public EncounterDoorBehavior Behavior;
    }

    class MinionInfo
    {
        public MinionInfo(BossInfo _bossInfo)
        {
            bossInfo = _bossInfo;
        }

        public BossInfo bossInfo;
    }

    public struct UpdateBossStateSaveDataEvent
    {
        public DungeonEncounterRecord DungeonEncounter;
        public int BossId;
        public EncounterState NewState;

        public UpdateBossStateSaveDataEvent(DungeonEncounterRecord dungeonEncounter, int bossId, EncounterState state)
        {
            DungeonEncounter = dungeonEncounter;
            BossId = bossId;
            NewState = state;
        }
    }

    public struct UpdateAdditionalSaveDataEvent
    {
        public string Key;
        public object Value;

        public UpdateAdditionalSaveDataEvent(string key, object value)
        {
            Key = key;
            Value = value;
        }
    }

    public class PersistentInstanceScriptValueBase
    {
        protected InstanceScript _instance;
        protected string _name;
        protected object _value;

        protected PersistentInstanceScriptValueBase(InstanceScript instance, string name, object value)
        {
            _instance = instance;
            _name = name;
            _value = value;

            _instance.RegisterPersistentScriptValue(this);
        }

        public string GetName() { return _name; }

        public UpdateAdditionalSaveDataEvent CreateEvent()
        {
            return new UpdateAdditionalSaveDataEvent(_name, _value);
        }

        public void LoadValue(long value)
        {
            _value = value;
        }

        public void LoadValue(double value)
        {
            _value = value;
        }
    }

    class PersistentInstanceScriptValue<T> : PersistentInstanceScriptValueBase
    {
        public PersistentInstanceScriptValue(InstanceScript instance, string name, T value) : base(instance, name, value) { }

        public PersistentInstanceScriptValue<T> SetValue(T value)
        {
            _value = value;
            NotifyValueChanged();
            return this;
        }

        void NotifyValueChanged()
        {
            _instance.instance.UpdateInstanceLock(CreateEvent());
        }

        void LoadValue(T value)
        {
            _value = value;
        }
    }
}
