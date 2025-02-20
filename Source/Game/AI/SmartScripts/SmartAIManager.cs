﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using Game.Movement;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Game.AI
{
    public class SmartAIManager : Singleton<SmartAIManager>
    {
        MultiMap<int, SmartScriptHolder>[] _eventMap = new MultiMap<int, SmartScriptHolder>[(int)SmartScriptType.Max];

        SmartAIManager()
        {
            for (byte i = 0; i < (int)SmartScriptType.Max; i++)
                _eventMap[i] = new MultiMap<int, SmartScriptHolder>();
        }

        public void LoadFromDB()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            for (byte i = 0; i < (int)SmartScriptType.Max; i++)
                _eventMap[i].Clear();  //Drop Existing SmartAI List

            PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.SEL_SMART_SCRIPTS);
            SQLResult result = DB.World.Query(stmt);
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 SmartAI scripts. DB table `smartai_scripts` is empty.");
                return;
            }

            int count = 0;
            do
            {
                SmartScriptHolder temp = new();

                temp.EntryOrGuid = result.Read<int>(0);
                if (temp.EntryOrGuid == 0)
                {
                    Log.outError(LogFilter.Sql, 
                        "SmartAIMgr.LoadFromDB: invalid entryorguid (0), skipped loading.");
                    continue;
                }

                SmartScriptType source_type = (SmartScriptType)result.Read<byte>(1);
                if (source_type >= SmartScriptType.Max)
                {
                    Log.outError(LogFilter.Sql, 
                        $"SmartAIMgr.LoadFromDB: invalid source_type " +
                        $"({source_type}), skipped loading.");
                    continue;
                }
                if (temp.EntryOrGuid >= 0)
                {
                    switch (source_type)
                    {
                        case SmartScriptType.Creature:
                            if (Global.ObjectMgr.GetCreatureTemplate(temp.EntryOrGuid) == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: Creature entry ({temp.EntryOrGuid}) " +
                                    $"does not exist, skipped loading.");
                                continue;
                            }
                            break;

                        case SmartScriptType.GameObject:
                        {
                            if (Global.ObjectMgr.GetGameObjectTemplate(temp.EntryOrGuid) == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: GameObject entry ({temp.EntryOrGuid}) " +
                                    $"does not exist, skipped loading.");
                                continue;
                            }
                            break;
                        }
                        case SmartScriptType.AreaTrigger:
                        {
                            if (CliDB.AreaTableStorage.LookupByKey(temp.EntryOrGuid) == null)
                            {
                                Log.outError(LogFilter.Sql,
                                    $"SmartAIMgr.LoadFromDB: AreaTrigger entry ({temp.EntryOrGuid}) " +
                                    $"does not exist, skipped loading.");
                                continue;
                            }
                            break;
                        }
                        case SmartScriptType.Scene:
                        {
                            if (Global.ObjectMgr.GetSceneTemplate(temp.EntryOrGuid) == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: Scene id ({temp.EntryOrGuid}) " +
                                    $"does not exist, skipped loading.");
                                continue;
                            }
                            break;
                        }
                        case SmartScriptType.Event:
                        {
                            if (!Global.ObjectMgr.IsValidEvent(temp.EntryOrGuid))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr::LoadFromDB: Event id ({temp.EntryOrGuid}) " +
                                    $"does not exist, skipped loading.");
                                continue;
                            }
                            break;
                        }
                        case SmartScriptType.Quest:
                        {
                            if (Global.ObjectMgr.GetQuestTemplate(temp.EntryOrGuid) == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: Quest id ({temp.EntryOrGuid}) " +
                                    $"does not exist, skipped loading.");
                                continue;
                            }
                            break;
                        }
                        case SmartScriptType.TimedActionlist:
                            break;//nothing to check, really
                        case SmartScriptType.AreaTriggerEntity:
                        {
                            if (Global.AreaTriggerDataStorage.GetAreaTriggerTemplate(new AreaTriggerId(temp.EntryOrGuid, false)) == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: AreaTrigger entry ({temp.EntryOrGuid} " +
                                    $"IsServerSide false) does not exist, skipped loading.");
                                continue;
                            }
                            break;
                        }
                        case SmartScriptType.AreaTriggerEntityCustom:
                        {
                            if (Global.AreaTriggerDataStorage.GetAreaTriggerTemplate(new AreaTriggerId(temp.EntryOrGuid, true)) == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: AreaTrigger entry " +
                                    $"({temp.EntryOrGuid} IsCustom true) does not exist, " +
                                    $"skipped loading.");
                                continue;
                            }
                            break;
                        }
                        default:
                            Log.outError(LogFilter.Sql, $"SmartAIMgr.LoadFromDB: " +
                                $"not yet implemented source_type {source_type}");
                            continue;
                    }
                }
                else
                {
                    switch (source_type)
                    {
                        case SmartScriptType.Creature:
                        {
                            CreatureData creature = Global.ObjectMgr.GetCreatureData(-temp.EntryOrGuid);
                            if (creature == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: Creature guid ({-temp.EntryOrGuid}) " +
                                    $"does not exist, skipped loading.");
                                continue;
                            }

                            CreatureTemplate creatureInfo = Global.ObjectMgr.GetCreatureTemplate(creature.Id);
                            if (creatureInfo == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: Creature entry ({creature.Id}) " +
                                    $"guid ({-temp.EntryOrGuid}) does not exist, skipped loading.");
                                continue;
                            }

                            if (creatureInfo.AIName != "SmartAI")
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: Creature entry ({creature.Id}) " +
                                    $"guid ({-temp.EntryOrGuid}) is not using SmartAI, skipped loading.");
                                continue;
                            }
                            break;
                        }
                        case SmartScriptType.GameObject:
                        {
                            GameObjectData gameObject = Global.ObjectMgr.GetGameObjectData(-temp.EntryOrGuid);
                            if (gameObject == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: GameObject guid ({-temp.EntryOrGuid}) " +
                                    $"does not exist, skipped loading.");
                                continue;
                            }

                            GameObjectTemplate gameObjectInfo = Global.ObjectMgr.GetGameObjectTemplate(gameObject.Id);
                            if (gameObjectInfo == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: GameObject entry ({gameObject.Id}) " +
                                    $"guid ({-temp.EntryOrGuid}) does not exist, skipped loading.");
                                continue;
                            }

                            if (gameObjectInfo.AIName != "SmartGameObjectAI")
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: GameObject entry ({gameObject.Id}) " +
                                    $"guid ({-temp.EntryOrGuid}) is not using SmartGameObjectAI, skipped loading.");
                                continue;
                            }
                            break;
                        }
                        default:
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr.LoadFromDB: GUID-specific scripting " +
                                $"not yet implemented for source_type {source_type}");
                            continue;
                    }
                }

                temp.SourceType = source_type;
                temp.EventId = result.Read<ushort>(2);
                temp.Link = result.Read<ushort>(3);

                bool invalidDifficulties = false;
                foreach (string token in result.Read<string>(4).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Enum.TryParse<Difficulty>(token, out Difficulty difficultyId))
                    {
                        invalidDifficulties = true;
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr::LoadFromDB: Invalid difficulties for entryorguid " +
                            $"({temp.EntryOrGuid}) source_type ({temp.GetScriptType()}) " +
                            $"id ({temp.EventId}), skipped loading.");
                        break;
                    }

                    if (difficultyId != 0 && !CliDB.DifficultyStorage.ContainsKey(difficultyId))
                    {
                        invalidDifficulties = true;
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr::LoadFromDB: Invalid difficulty id ({difficultyId}) " +
                            $"for entryorguid ({temp.EntryOrGuid}) source_type ({temp.GetScriptType()}) " +
                            $"id ({temp.EventId}), skipped loading.");
                        break;
                    }

                    temp.Difficulties.Add(difficultyId);
                }

                if (invalidDifficulties)
                    continue;

                temp.Event.type = (SmartEvents)result.Read<byte>(5);
                temp.Event.event_phase_mask = result.Read<ushort>(6);
                temp.Event.event_chance = result.Read<byte>(7);
                temp.Event.event_flags = (SmartEventFlags)result.Read<ushort>(8);

                temp.Event.raw.param1 = result.Read<int>(9);
                temp.Event.raw.param2 = result.Read<int>(10);
                temp.Event.raw.param3 = result.Read<int>(11);
                temp.Event.raw.param4 = result.Read<int>(12);
                temp.Event.raw.param5 = result.Read<int>(13);
                temp.Event.param_string = result.Read<string>(14);

                temp.Action.type = (SmartActions)result.Read<byte>(15);
                temp.Action.raw.param1 = result.Read<int>(16);
                temp.Action.raw.param2 = result.Read<int>(17);
                temp.Action.raw.param3 = result.Read<int>(18);
                temp.Action.raw.param4 = result.Read<int>(19);
                temp.Action.raw.param5 = result.Read<int>(20);
                temp.Action.raw.param6 = result.Read<int>(21);
                temp.Action.raw.param7 = result.Read<int>(22);

                temp.Target.type = (SmartTargets)result.Read<byte>(23);
                temp.Target.raw.param1 = result.Read<int>(24);
                temp.Target.raw.param2 = result.Read<int>(25);
                temp.Target.raw.param3 = result.Read<int>(26);
                temp.Target.raw.param4 = result.Read<int>(27);
                temp.Target.x = result.Read<float>(28);
                temp.Target.y = result.Read<float>(29);
                temp.Target.z = result.Read<float>(30);
                temp.Target.o = result.Read<float>(31);

                //check target
                if (!IsTargetValid(temp))
                    continue;

                // check all event and action params
                if (!IsEventValid(temp))
                    continue;

                // specific check for timed events
                switch (temp.Event.type)
                {
                    case SmartEvents.Update:
                    case SmartEvents.UpdateOoc:
                    case SmartEvents.UpdateIc:
                    case SmartEvents.HealthPct:
                    case SmartEvents.ManaPct:
                    case SmartEvents.Range:
                    case SmartEvents.FriendlyHealthPCT:
                    case SmartEvents.FriendlyMissingBuff:
                    case SmartEvents.HasAura:
                    case SmartEvents.TargetBuffed:
                        if (temp.Event.minMaxRepeat.repeatMin == 0 && temp.Event.minMaxRepeat.repeatMax == 0 
                            && !temp.Event.event_flags.HasAnyFlag(SmartEventFlags.NotRepeatable) 
                            && temp.SourceType != SmartScriptType.TimedActionlist)
                        {
                            temp.Event.event_flags |= SmartEventFlags.NotRepeatable;
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr.LoadFromDB: Entry {temp.EntryOrGuid} " +
                                $"SourceType {temp.GetScriptType()}, Event {temp.EventId}, " +
                                $"Missing Repeat flag.");
                        }
                        break;
                    case SmartEvents.VictimCasting:
                    case SmartEvents.IsBehindTarget:
                        if (temp.Event.minMaxRepeat.min == 0 && temp.Event.minMaxRepeat.max == 0 
                            && !temp.Event.event_flags.HasAnyFlag(SmartEventFlags.NotRepeatable) 
                            && temp.SourceType != SmartScriptType.TimedActionlist)
                        {
                            temp.Event.event_flags |= SmartEventFlags.NotRepeatable;
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr.LoadFromDB: Entry {temp.EntryOrGuid} " +
                                $"SourceType {temp.GetScriptType()}, Event {temp.EventId}, " +
                                $"Missing Repeat flag.");
                        }
                        break;
                    case SmartEvents.FriendlyIsCc:
                        if (temp.Event.friendlyCC.repeatMin == 0 && temp.Event.friendlyCC.repeatMax == 0 
                            && !temp.Event.event_flags.HasAnyFlag(SmartEventFlags.NotRepeatable) 
                            && temp.SourceType != SmartScriptType.TimedActionlist)
                        {
                            temp.Event.event_flags |= SmartEventFlags.NotRepeatable;
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr.LoadFromDB: Entry {temp.EntryOrGuid} " +
                                $"SourceType {temp.GetScriptType()}, Event {temp.EventId}, " +
                                $"Missing Repeat flag.");
                        }
                        break;
                    default:
                        break;
                }

                // creature entry / guid not found in storage,
                // create empty event list for it and increase counters
                if (!_eventMap[(int)source_type].ContainsKey(temp.EntryOrGuid))
                    ++count;

                // store the new event
                _eventMap[(int)source_type].Add(temp.EntryOrGuid, temp);
            }
            while (result.NextRow());

            // Post Loading Validation
            for (byte i = 0; i < (int)SmartScriptType.Max; ++i)
            {
                if (_eventMap[i] == null)
                    continue;

                foreach (var key in _eventMap[i].Keys)
                {
                    var list = _eventMap[i][key];
                    foreach (var e in list)
                    {
                        if (e.Link != 0)
                        {
                            if (FindLinkedEvent(list, e.Link) == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: Entry {e.EntryOrGuid} " +
                                    $"SourceType {e.GetScriptType()}, Event {e.EventId}, " +
                                    $"Link Event {e.Link} not found or invalid.");
                            }
                        }

                        if (e.GetEventType() == SmartEvents.Link)
                        {
                            if (FindLinkedSourceEvent(list, e.EventId) == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr.LoadFromDB: Entry {e.EntryOrGuid} " +
                                    $"SourceType {e.GetScriptType()}, Event {e.EventId}, " +
                                    "Link Source Event not found or invalid. Event will never trigger.");
                            }
                        }
                    }
                }
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {count} SmartAI scripts in {Time.Diff(oldMSTime)} ms.");
        }

        static bool EventHasInvoker(SmartEvents smartEvent)
        {
            switch (smartEvent)
            { // white list of events that actually have an invoker passed to them
                case SmartEvents.Aggro:
                case SmartEvents.Death:
                case SmartEvents.Kill:
                case SmartEvents.SummonedUnit:
                case SmartEvents.SummonedUnitDies:
                case SmartEvents.SpellHit:
                case SmartEvents.SpellHitTarget:
                case SmartEvents.Damaged:
                case SmartEvents.ReceiveHeal:
                case SmartEvents.ReceiveEmote:
                case SmartEvents.JustSummoned:
                case SmartEvents.DamagedTarget:
                case SmartEvents.SummonDespawned:
                case SmartEvents.PassengerBoarded:
                case SmartEvents.PassengerRemoved:
                case SmartEvents.GossipHello:
                case SmartEvents.GossipSelect:
                case SmartEvents.AcceptedQuest:
                case SmartEvents.RewardQuest:
                case SmartEvents.FollowCompleted:
                case SmartEvents.OnSpellclick:
                case SmartEvents.GoLootStateChanged:
                case SmartEvents.AreatriggerOntrigger:
                case SmartEvents.IcLos:
                case SmartEvents.OocLos:
                case SmartEvents.DistanceCreature:
                case SmartEvents.FriendlyHealthPCT:
                case SmartEvents.FriendlyIsCc:
                case SmartEvents.FriendlyMissingBuff:
                case SmartEvents.ActionDone:
                case SmartEvents.Range:
                case SmartEvents.VictimCasting:
                case SmartEvents.TargetBuffed:
                case SmartEvents.InstancePlayerEnter:
                case SmartEvents.TransportAddcreature:
                case SmartEvents.DataSet:
                case SmartEvents.QuestAccepted:
                case SmartEvents.QuestObjCompletion:
                case SmartEvents.QuestCompletion:
                case SmartEvents.QuestFail:
                case SmartEvents.QuestRewarded:
                case SmartEvents.SceneStart:
                case SmartEvents.SceneTrigger:
                case SmartEvents.SceneCancel:
                case SmartEvents.SceneComplete:
                case SmartEvents.SendEventTrigger:
                    return true;
                default:
                    return false;
            }
        }

        static bool IsTargetValid(SmartScriptHolder e)
        {
            if (Math.Abs(e.Target.o) > 2 * MathFunctions.PI)
            {
                Log.outError(LogFilter.Sql,
                    $"SmartAIMgr: {e} has abs(`target.o` = {e.Target.o}) > 2*PI " +
                    $"(orientation is expressed in radians)");
            }

            switch (e.GetTargetType())
            {
                case SmartTargets.CreatureDistance:
                case SmartTargets.CreatureRange:
                {
                    if (e.Target.unitDistance.creature != 0 
                        && Global.ObjectMgr.GetCreatureTemplate(e.Target.unitDistance.creature) == null)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} uses non-existent Creature entry " +
                            $"{e.Target.unitDistance.creature} as target_param1, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartTargets.GameobjectDistance:
                case SmartTargets.GameobjectRange:
                {
                    if (e.Target.goDistance.entry != 0 
                        && Global.ObjectMgr.GetGameObjectTemplate(e.Target.goDistance.entry) == null)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} uses non-existent GameObject entry " +
                            $"{e.Target.goDistance.entry} as target_param1, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartTargets.CreatureGuid:
                {
                    if (e.Target.unitGUID.entry != 0 && !IsCreatureValid(e, e.Target.unitGUID.entry))
                        return false;

                    long guid = e.Target.unitGUID.dbGuid;
                    CreatureData data = Global.ObjectMgr.GetCreatureData(guid);
                    if (data == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} using invalid creature guid " +
                            $"{guid} as target_param1, skipped.");
                        return false;
                    }
                    else if (e.Target.unitGUID.entry != 0 && e.Target.unitGUID.entry != data.Id)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} using invalid creature entry " +
                            $"{e.Target.unitGUID.entry} (expected {data.Id}) " +
                            $"for guid {guid} as target_param1, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartTargets.GameobjectGuid:
                {
                    if (e.Target.goGUID.entry != 0 && !IsGameObjectValid(e, e.Target.goGUID.entry))
                        return false;

                    long guid = e.Target.goGUID.dbGuid;
                    GameObjectData data = Global.ObjectMgr.GetGameObjectData(guid);
                    if (data == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} using invalid gameobject guid " +
                            $"{guid} as target_param1, skipped.");
                        return false;
                    }
                    else if (e.Target.goGUID.entry != 0 && e.Target.goGUID.entry != data.Id)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} using invalid gameobject entry " +
                            $"{e.Target.goGUID.entry} (expected {data.Id}) " +
                            $"for guid {guid} as target_param1, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartTargets.PlayerDistance:
                case SmartTargets.ClosestPlayer:
                {
                    if (e.Target.playerDistance.dist == 0)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} has maxDist 0 as target_param1, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartTargets.ActionInvoker:
                case SmartTargets.ActionInvokerVehicle:
                case SmartTargets.InvokerParty:
                    if (e.GetScriptType() != SmartScriptType.TimedActionlist 
                        && e.GetEventType() != SmartEvents.Link 
                        && !EventHasInvoker(e.Event.type))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                            $"Event {e.GetEventType()} Action {e.GetActionType()} " +
                            $"has invoker target, but event does not provide any invoker!");
                        return false;
                    }
                    break;
                case SmartTargets.HostileSecondAggro:
                case SmartTargets.HostileLastAggro:
                case SmartTargets.HostileRandom:
                case SmartTargets.HostileRandomNotTop:
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Target.hostilRandom.playerOnly);
                    break;
                case SmartTargets.Farthest:
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Target.farthest.playerOnly);
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Target.farthest.isInLos);
                    break;
                case SmartTargets.ClosestCreature:
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Target.unitClosest.dead);
                    break;
                case SmartTargets.ClosestEnemy:
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Target.closestAttackable.playerOnly);
                    break;
                case SmartTargets.ClosestFriendly:
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Target.closestFriendly.playerOnly);
                    break;
                case SmartTargets.OwnerOrSummoner:
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Target.owner.useCharmerOrOwner);
                    break;
                case SmartTargets.ClosestGameobject:
                case SmartTargets.PlayerRange:
                case SmartTargets.Self:
                case SmartTargets.Victim:
                case SmartTargets.Position:
                case SmartTargets.None:
                case SmartTargets.ThreatList:
                case SmartTargets.Stored:
                case SmartTargets.LootRecipients:
                case SmartTargets.VehiclePassenger:
                case SmartTargets.ClosestUnspawnedGameobject:
                    break;
                default:
                    Log.outError(LogFilter.ScriptsAi, 
                        $"SmartAIMgr: Not handled target_type({e.GetTargetType()}), " +
                        $"Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                        $"Event {e.EventId} Action {e.GetActionType()}, skipped.");
                    return false;
            }

            if (!CheckUnusedTargetParams(e))
                return false;

            return true;
        }

        static bool IsSpellVisualKitValid(SmartScriptHolder e, int entry)
        {
            if (!CliDB.SpellVisualKitStorage.ContainsKey(entry))
            {
                Log.outError(LogFilter.Sql, 
                    $"SmartAIMgr: Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                    $"Event {e.EventId} Action {e.GetActionType()} " +
                    $"uses non-existent SpellVisualKit entry {entry}, skipped.");
                return false;
            }
            return true;
        }

        static bool CheckUnusedEventParams(SmartScriptHolder e)
        {
            int paramsStructSize = e.Event.type switch
            {
                SmartEvents.UpdateIc => Marshal.SizeOf(typeof(SmartEvent.MinMaxRepeat)),
                SmartEvents.UpdateOoc => Marshal.SizeOf(typeof(SmartEvent.MinMaxRepeat)),
                SmartEvents.HealthPct => Marshal.SizeOf(typeof(SmartEvent.MinMaxRepeat)),
                SmartEvents.ManaPct => Marshal.SizeOf(typeof(SmartEvent.MinMaxRepeat)),
                SmartEvents.Aggro => 0,
                SmartEvents.Kill => Marshal.SizeOf(typeof(SmartEvent.Kill)),
                SmartEvents.Death => 0,
                SmartEvents.Evade => 0,
                SmartEvents.SpellHit => Marshal.SizeOf(typeof(SmartEvent.SpellHit)),
                SmartEvents.Range => Marshal.SizeOf(typeof(SmartEvent.MinMaxRepeat)),
                SmartEvents.OocLos => Marshal.SizeOf(typeof(SmartEvent.Los)),
                SmartEvents.Respawn => Marshal.SizeOf(typeof(SmartEvent.Respawn)),
                SmartEvents.VictimCasting => Marshal.SizeOf(typeof(SmartEvent.TargetCasting)),
                SmartEvents.FriendlyIsCc => Marshal.SizeOf(typeof(SmartEvent.FriendlyCC)),
                SmartEvents.FriendlyMissingBuff => Marshal.SizeOf(typeof(SmartEvent.MissingBuff)),
                SmartEvents.SummonedUnit => Marshal.SizeOf(typeof(SmartEvent.Summoned)),
                SmartEvents.AcceptedQuest => Marshal.SizeOf(typeof(SmartEvent.Quest)),
                SmartEvents.RewardQuest => Marshal.SizeOf(typeof(SmartEvent.Quest)),
                SmartEvents.ReachedHome => 0,
                SmartEvents.ReceiveEmote => Marshal.SizeOf(typeof(SmartEvent.Emote)),
                SmartEvents.HasAura => Marshal.SizeOf(typeof(SmartEvent.Aura)),
                SmartEvents.TargetBuffed => Marshal.SizeOf(typeof(SmartEvent.Aura)),
                SmartEvents.Reset => 0,
                SmartEvents.IcLos => Marshal.SizeOf(typeof(SmartEvent.Los)),
                SmartEvents.PassengerBoarded => Marshal.SizeOf(typeof(SmartEvent.MinMax)),
                SmartEvents.PassengerRemoved => Marshal.SizeOf(typeof(SmartEvent.MinMax)),
                SmartEvents.Charmed => Marshal.SizeOf(typeof(SmartEvent.Charm)),
                SmartEvents.SpellHitTarget => Marshal.SizeOf(typeof(SmartEvent.SpellHit)),
                SmartEvents.Damaged => Marshal.SizeOf(typeof(SmartEvent.MinMaxRepeat)),
                SmartEvents.DamagedTarget => Marshal.SizeOf(typeof(SmartEvent.MinMaxRepeat)),
                SmartEvents.Movementinform => Marshal.SizeOf(typeof(SmartEvent.MovementInform)),
                SmartEvents.SummonDespawned => Marshal.SizeOf(typeof(SmartEvent.Summoned)),
                SmartEvents.CorpseRemoved => 0,
                SmartEvents.AiInit => 0,
                SmartEvents.DataSet => Marshal.SizeOf(typeof(SmartEvent.DataSet)),
                SmartEvents.WaypointReached => Marshal.SizeOf(typeof(SmartEvent.Waypoint)),
                SmartEvents.TransportAddplayer => 0,
                SmartEvents.TransportAddcreature => Marshal.SizeOf(typeof(SmartEvent.TransportAddCreature)),
                SmartEvents.TransportRemovePlayer => 0,
                SmartEvents.TransportRelocate => Marshal.SizeOf(typeof(SmartEvent.TransportRelocate)),
                SmartEvents.InstancePlayerEnter => Marshal.SizeOf(typeof(SmartEvent.InstancePlayerEnter)),
                SmartEvents.AreatriggerOntrigger => Marshal.SizeOf(typeof(SmartEvent.Areatrigger)),
                SmartEvents.QuestAccepted => 0,
                SmartEvents.QuestObjCompletion => 0,
                SmartEvents.QuestCompletion => 0,
                SmartEvents.QuestRewarded => 0,
                SmartEvents.QuestFail => 0,
                SmartEvents.TextOver => Marshal.SizeOf(typeof(SmartEvent.TextOver)),
                SmartEvents.ReceiveHeal => Marshal.SizeOf(typeof(SmartEvent.MinMaxRepeat)),
                SmartEvents.JustSummoned => 0,
                SmartEvents.WaypointPaused => Marshal.SizeOf(typeof(SmartEvent.Waypoint)),
                SmartEvents.WaypointResumed => Marshal.SizeOf(typeof(SmartEvent.Waypoint)),
                SmartEvents.WaypointStopped => Marshal.SizeOf(typeof(SmartEvent.Waypoint)),
                SmartEvents.WaypointEnded => Marshal.SizeOf(typeof(SmartEvent.Waypoint)),
                SmartEvents.TimedEventTriggered => Marshal.SizeOf(typeof(SmartEvent.TimedEvent)),
                SmartEvents.Update => Marshal.SizeOf(typeof(SmartEvent.MinMaxRepeat)),
                SmartEvents.Link => 0,
                SmartEvents.GossipSelect => Marshal.SizeOf(typeof(SmartEvent.Gossip)),
                SmartEvents.JustCreated => 0,
                SmartEvents.GossipHello => Marshal.SizeOf(typeof(SmartEvent.GossipHello)),
                SmartEvents.FollowCompleted => 0,
                SmartEvents.GameEventStart => Marshal.SizeOf(typeof(SmartEvent.GameEvent)),
                SmartEvents.GameEventEnd => Marshal.SizeOf(typeof(SmartEvent.GameEvent)),
                SmartEvents.GoLootStateChanged => Marshal.SizeOf(typeof(SmartEvent.GoLootStateChanged)),
                SmartEvents.GoEventInform => Marshal.SizeOf(typeof(SmartEvent.EventInform)),
                SmartEvents.ActionDone => Marshal.SizeOf(typeof(SmartEvent.DoAction)),
                SmartEvents.OnSpellclick => 0,
                SmartEvents.FriendlyHealthPCT => Marshal.SizeOf(typeof(SmartEvent.FriendlyHealthPct)),
                SmartEvents.DistanceCreature => Marshal.SizeOf(typeof(SmartEvent.Distance)),
                SmartEvents.DistanceGameobject => Marshal.SizeOf(typeof(SmartEvent.Distance)),
                SmartEvents.CounterSet => Marshal.SizeOf(typeof(SmartEvent.Counter)),
                SmartEvents.SceneStart => 0,
                SmartEvents.SceneTrigger => 0,
                SmartEvents.SceneCancel => 0,
                SmartEvents.SceneComplete => 0,
                SmartEvents.SummonedUnitDies => Marshal.SizeOf(typeof(SmartEvent.Summoned)),
                SmartEvents.OnSpellCast => Marshal.SizeOf(typeof(SmartEvent.SpellCast)),
                SmartEvents.OnSpellFailed => Marshal.SizeOf(typeof(SmartEvent.SpellCast)),
                SmartEvents.OnSpellStart => Marshal.SizeOf(typeof(SmartEvent.SpellCast)),
                SmartEvents.OnDespawn => 0,
                SmartEvents.SendEventTrigger => 0,
                _ => Marshal.SizeOf(typeof(SmartEvent.Raw)),
            };

            int rawCount = Marshal.SizeOf(typeof(SmartEvent.Raw)) / sizeof(int);
            int paramsCount = paramsStructSize / sizeof(int);

            for (int index = paramsCount; index < rawCount; index++)
            {
                int value = 0;
                switch (index)
                {
                    case 0:
                        value = e.Event.raw.param1;
                        break;
                    case 1:
                        value = e.Event.raw.param2;
                        break;
                    case 2:
                        value = e.Event.raw.param3;
                        break;
                    case 3:
                        value = e.Event.raw.param4;
                        break;
                    case 4:
                        value = e.Event.raw.param5;
                        break;
                }

                if (value != 0)
                {
                    Log.outWarn(LogFilter.Sql,
                        $"SmartAIMgr: {e} has unused event_param{index + 1} " +
                        $"with value {value}, it should be 0.");
                }
            }

            return true;
        }

        static bool CheckUnusedActionParams(SmartScriptHolder e)
        {
            int paramsStructSize = e.Action.type switch
            {
                SmartActions.None => 0,
                SmartActions.Talk => Marshal.SizeOf(typeof(SmartAction.Talk)),
                SmartActions.SetFaction => Marshal.SizeOf(typeof(SmartAction.Faction)),
                SmartActions.MorphToEntryOrModel => Marshal.SizeOf(typeof(SmartAction.MorphOrMount)),
                SmartActions.Sound => Marshal.SizeOf(typeof(SmartAction.Sound)),
                SmartActions.PlayEmote => Marshal.SizeOf(typeof(SmartAction.Emote)),
                SmartActions.FailQuest => Marshal.SizeOf(typeof(SmartAction.Quest)),
                SmartActions.OfferQuest => Marshal.SizeOf(typeof(SmartAction.QuestOffer)),
                SmartActions.SetReactState => Marshal.SizeOf(typeof(SmartAction.React)),
                SmartActions.ActivateGobject => 0,
                SmartActions.RandomEmote => Marshal.SizeOf(typeof(SmartAction.RandomEmote)),
                SmartActions.Cast => Marshal.SizeOf(typeof(SmartAction.Cast)),
                SmartActions.SummonCreature => Marshal.SizeOf(typeof(SmartAction.SummonCreature)),
                SmartActions.ThreatSinglePct => Marshal.SizeOf(typeof(SmartAction.ThreatPCT)),
                SmartActions.ThreatAllPct => Marshal.SizeOf(typeof(SmartAction.ThreatPCT)),
                SmartActions.CallAreaexploredoreventhappens => Marshal.SizeOf(typeof(SmartAction.Quest)),
                SmartActions.SetIngamePhaseGroup => Marshal.SizeOf(typeof(SmartAction.IngamePhaseGroup)),
                SmartActions.SetEmoteState => Marshal.SizeOf(typeof(SmartAction.Emote)),
                SmartActions.AutoAttack => Marshal.SizeOf(typeof(SmartAction.AutoAttack)),
                SmartActions.AllowCombatMovement => Marshal.SizeOf(typeof(SmartAction.CombatMove)),
                SmartActions.SetEventPhase => Marshal.SizeOf(typeof(SmartAction.SetEventPhase)),
                SmartActions.IncEventPhase => Marshal.SizeOf(typeof(SmartAction.IncEventPhase)),
                SmartActions.Evade => Marshal.SizeOf(typeof(SmartAction.Evade)),
                SmartActions.FleeForAssist => Marshal.SizeOf(typeof(SmartAction.FleeAssist)),
                SmartActions.CallGroupeventhappens => Marshal.SizeOf(typeof(SmartAction.Quest)),
                SmartActions.CombatStop => 0,
                SmartActions.RemoveAurasFromSpell => Marshal.SizeOf(typeof(SmartAction.RemoveAura)),
                SmartActions.Follow => Marshal.SizeOf(typeof(SmartAction.Follow)),
                SmartActions.RandomPhase => Marshal.SizeOf(typeof(SmartAction.RandomPhase)),
                SmartActions.RandomPhaseRange => Marshal.SizeOf(typeof(SmartAction.RandomPhaseRange)),
                SmartActions.ResetGobject => 0,
                SmartActions.CallKilledmonster => Marshal.SizeOf(typeof(SmartAction.KilledMonster)),
                SmartActions.SetInstData => Marshal.SizeOf(typeof(SmartAction.SetInstanceData)),
                SmartActions.SetInstData64 => Marshal.SizeOf(typeof(SmartAction.SetInstanceData64)),
                SmartActions.UpdateTemplate => Marshal.SizeOf(typeof(SmartAction.UpdateTemplate)),
                SmartActions.Die => 0,
                SmartActions.SetInCombatWithZone => 0,
                SmartActions.CallForHelp => Marshal.SizeOf(typeof(SmartAction.CallHelp)),
                SmartActions.SetSheath => Marshal.SizeOf(typeof(SmartAction.SetSheath)),
                SmartActions.ForceDespawn => Marshal.SizeOf(typeof(SmartAction.ForceDespawn)),
                SmartActions.SetInvincibilityHpLevel => Marshal.SizeOf(typeof(SmartAction.InvincHP)),
                SmartActions.MountToEntryOrModel => Marshal.SizeOf(typeof(SmartAction.MorphOrMount)),
                SmartActions.SetIngamePhaseId => Marshal.SizeOf(typeof(SmartAction.IngamePhaseId)),
                SmartActions.SetData => Marshal.SizeOf(typeof(SmartAction.SetData)),
                SmartActions.AttackStop => 0,
                SmartActions.SetVisibility => Marshal.SizeOf(typeof(SmartAction.Visibility)),
                SmartActions.SetActive => Marshal.SizeOf(typeof(SmartAction.Active)),
                SmartActions.AttackStart => 0,
                SmartActions.SummonGo => Marshal.SizeOf(typeof(SmartAction.SummonGO)),
                SmartActions.KillUnit => 0,
                SmartActions.ActivateTaxi => Marshal.SizeOf(typeof(SmartAction.Taxi)),
                SmartActions.WpStart => Marshal.SizeOf(typeof(SmartAction.WpStart)),
                SmartActions.WpPause => Marshal.SizeOf(typeof(SmartAction.WpPause)),
                SmartActions.WpStop => Marshal.SizeOf(typeof(SmartAction.WpStop)),
                SmartActions.AddItem => Marshal.SizeOf(typeof(SmartAction.Item)),
                SmartActions.RemoveItem => Marshal.SizeOf(typeof(SmartAction.Item)),
                SmartActions.SetRun => Marshal.SizeOf(typeof(SmartAction.SetRun)),
                SmartActions.SetDisableGravity => Marshal.SizeOf(typeof(SmartAction.SetDisableGravity)),
                SmartActions.Teleport => Marshal.SizeOf(typeof(SmartAction.Teleport)),
                SmartActions.SetCounter => Marshal.SizeOf(typeof(SmartAction.SetCounter)),
                SmartActions.StoreTargetList => Marshal.SizeOf(typeof(SmartAction.StoreTargets)),
                SmartActions.WpResume => 0,
                SmartActions.SetOrientation => 0,
                SmartActions.CreateTimedEvent => Marshal.SizeOf(typeof(SmartAction.TimeEvent)),
                SmartActions.Playmovie => Marshal.SizeOf(typeof(SmartAction.Movie)),
                SmartActions.MoveToPos => Marshal.SizeOf(typeof(SmartAction.MoveToPos)),
                SmartActions.EnableTempGobj => Marshal.SizeOf(typeof(SmartAction.EnableTempGO)),
                SmartActions.Equip => Marshal.SizeOf(typeof(SmartAction.Equip)),
                SmartActions.CloseGossip => 0,
                SmartActions.TriggerTimedEvent => Marshal.SizeOf(typeof(SmartAction.TimeEvent)),
                SmartActions.RemoveTimedEvent => Marshal.SizeOf(typeof(SmartAction.TimeEvent)),
                SmartActions.CallScriptReset => 0,
                SmartActions.SetRangedMovement => Marshal.SizeOf(typeof(SmartAction.SetRangedMovement)),
                SmartActions.CallTimedActionlist => Marshal.SizeOf(typeof(SmartAction.TimedActionList)),
                SmartActions.SetNpcFlag => Marshal.SizeOf(typeof(SmartAction.Flag)),
                SmartActions.AddNpcFlag => Marshal.SizeOf(typeof(SmartAction.Flag)),
                SmartActions.RemoveNpcFlag => Marshal.SizeOf(typeof(SmartAction.Flag)),
                SmartActions.SimpleTalk => Marshal.SizeOf(typeof(SmartAction.SimpleTalk)),
                SmartActions.SelfCast => Marshal.SizeOf(typeof(SmartAction.Cast)),
                SmartActions.CrossCast => Marshal.SizeOf(typeof(SmartAction.CrossCast)),
                SmartActions.CallRandomTimedActionlist => Marshal.SizeOf(typeof(SmartAction.RandTimedActionList)),
                SmartActions.CallRandomRangeTimedActionlist => Marshal.SizeOf(typeof(SmartAction.RandRangeTimedActionList)),
                SmartActions.RandomMove => Marshal.SizeOf(typeof(SmartAction.MoveRandom)),
                SmartActions.SetUnitFieldBytes1 => Marshal.SizeOf(typeof(SmartAction.SetunitByte)),
                SmartActions.RemoveUnitFieldBytes1 => Marshal.SizeOf(typeof(SmartAction.DelunitByte)),
                SmartActions.InterruptSpell => Marshal.SizeOf(typeof(SmartAction.InterruptSpellCasting)),
                SmartActions.AddDynamicFlag => Marshal.SizeOf(typeof(SmartAction.Flag)),
                SmartActions.RemoveDynamicFlag => Marshal.SizeOf(typeof(SmartAction.Flag)),
                SmartActions.JumpToPos => Marshal.SizeOf(typeof(SmartAction.Jump)),
                SmartActions.SendGossipMenu => Marshal.SizeOf(typeof(SmartAction.SendGossipMenu)),
                SmartActions.GoSetLootState => Marshal.SizeOf(typeof(SmartAction.SetGoLootState)),
                SmartActions.SendTargetToTarget => Marshal.SizeOf(typeof(SmartAction.SendTargetToTarget)),
                SmartActions.SetHomePos => 0,
                SmartActions.SetHealthRegen => Marshal.SizeOf(typeof(SmartAction.SetHealthRegen)),
                SmartActions.SetRoot => Marshal.SizeOf(typeof(SmartAction.SetRoot)),
                SmartActions.SummonCreatureGroup => Marshal.SizeOf(typeof(SmartAction.CreatureGroup)),
                SmartActions.SetPower => Marshal.SizeOf(typeof(SmartAction.Power)),
                SmartActions.AddPower => Marshal.SizeOf(typeof(SmartAction.Power)),
                SmartActions.RemovePower => Marshal.SizeOf(typeof(SmartAction.Power)),
                SmartActions.GameEventStop => Marshal.SizeOf(typeof(SmartAction.GameEventStop)),
                SmartActions.GameEventStart => Marshal.SizeOf(typeof(SmartAction.GameEventStart)),
                SmartActions.StartClosestWaypoint => Marshal.SizeOf(typeof(SmartAction.ClosestWaypointFromList)),
                SmartActions.MoveOffset => Marshal.SizeOf(typeof(SmartAction.MoveOffset)),
                SmartActions.RandomSound => Marshal.SizeOf(typeof(SmartAction.RandomSound)),
                SmartActions.SetCorpseDelay => Marshal.SizeOf(typeof(SmartAction.CorpseDelay)),
                SmartActions.DisableEvade => Marshal.SizeOf(typeof(SmartAction.DisableEvade)),
                SmartActions.GoSetGoState => Marshal.SizeOf(typeof(SmartAction.GoState)),
                SmartActions.AddThreat => Marshal.SizeOf(typeof(SmartAction.Threat)),
                SmartActions.LoadEquipment => Marshal.SizeOf(typeof(SmartAction.LoadEquipment)),
                SmartActions.TriggerRandomTimedEvent => Marshal.SizeOf(typeof(SmartAction.RandomTimedEvent)),
                SmartActions.PauseMovement => Marshal.SizeOf(typeof(SmartAction.PauseMovement)),
                SmartActions.PlayAnimkit => Marshal.SizeOf(typeof(SmartAction.AnimKit)),
                SmartActions.ScenePlay => Marshal.SizeOf(typeof(SmartAction.Scene)),
                SmartActions.SceneCancel => Marshal.SizeOf(typeof(SmartAction.Scene)),
                SmartActions.SpawnSpawngroup => Marshal.SizeOf(typeof(SmartAction.GroupSpawn)),
                SmartActions.DespawnSpawngroup => Marshal.SizeOf(typeof(SmartAction.GroupSpawn)),
                SmartActions.RespawnBySpawnId => Marshal.SizeOf(typeof(SmartAction.RespawnData)),
                SmartActions.InvokerCast => Marshal.SizeOf(typeof(SmartAction.Cast)),
                SmartActions.PlayCinematic => Marshal.SizeOf(typeof(SmartAction.Cinematic)),
                SmartActions.SetMovementSpeed => Marshal.SizeOf(typeof(SmartAction.MovementSpeed)),
                SmartActions.PlaySpellVisualKit => Marshal.SizeOf(typeof(SmartAction.SpellVisualKit)),
                SmartActions.OverrideLight => Marshal.SizeOf(typeof(SmartAction.OverrideLight)),
                SmartActions.OverrideWeather => Marshal.SizeOf(typeof(SmartAction.OverrideWeather)),
                SmartActions.SetAIAnimKit => 0,
                SmartActions.SetHover => Marshal.SizeOf(typeof(SmartAction.SetHover)),
                SmartActions.SetHealthPct => Marshal.SizeOf(typeof(SmartAction.SetHealthPct)),
                SmartActions.CreateConversation => Marshal.SizeOf(typeof(SmartAction.Conversation)),
                SmartActions.SetImmunePC => Marshal.SizeOf(typeof(SmartAction.SetImmunePC)),
                SmartActions.SetImmuneNPC => Marshal.SizeOf(typeof(SmartAction.SetImmuneNPC)),
                SmartActions.SetUninteractible => Marshal.SizeOf(typeof(SmartAction.SetUninteractible)),
                SmartActions.ActivateGameobject => Marshal.SizeOf(typeof(SmartAction.ActivateGameObject)),
                SmartActions.AddToStoredTargetList => Marshal.SizeOf(typeof(SmartAction.AddToStoredTargets)),
                SmartActions.BecomePersonalCloneForPlayer => Marshal.SizeOf(typeof(SmartAction.BecomePersonalClone)),
                SmartActions.TriggerGameEvent => Marshal.SizeOf(typeof(SmartAction.TriggerGameEvent)),
                SmartActions.DoAction => Marshal.SizeOf(typeof(SmartAction.DoAction)),
                _ => Marshal.SizeOf(typeof(SmartAction.Raw)),
            };

            int rawCount = Marshal.SizeOf(typeof(SmartAction.Raw)) / sizeof(int);
            int paramsCount = paramsStructSize / sizeof(int);

            for (int index = paramsCount; index < rawCount; index++)
            {
                int value = 0;
                switch (index)
                {
                    case 0:
                        value = e.Action.raw.param1;
                        break;
                    case 1:
                        value = e.Action.raw.param2;
                        break;
                    case 2:
                        value = e.Action.raw.param3;
                        break;
                    case 3:
                        value = e.Action.raw.param4;
                        break;
                    case 4:
                        value = e.Action.raw.param5;
                        break;
                    case 5:
                        value = e.Action.raw.param6;
                        break;
                }

                if (value != 0)
                {
                    Log.outWarn(LogFilter.Sql,
                        $"SmartAIMgr: {e} has unused action_param{index + 1} " +
                        $"with value {value}, it should be 0.");
                }
            }

            return true;
        }

        static bool CheckUnusedTargetParams(SmartScriptHolder e)
        {
            int paramsStructSize = e.Target.type switch
            {
                SmartTargets.None => 0,
                SmartTargets.Self => 0,
                SmartTargets.Victim => 0,
                SmartTargets.HostileSecondAggro => Marshal.SizeOf(typeof(SmartTarget.HostilRandom)),
                SmartTargets.HostileLastAggro => Marshal.SizeOf(typeof(SmartTarget.HostilRandom)),
                SmartTargets.HostileRandom => Marshal.SizeOf(typeof(SmartTarget.HostilRandom)),
                SmartTargets.HostileRandomNotTop => Marshal.SizeOf(typeof(SmartTarget.HostilRandom)),
                SmartTargets.ActionInvoker => 0,
                SmartTargets.Position => 0, //Uses X,Y,Z,O
                SmartTargets.CreatureRange => Marshal.SizeOf(typeof(SmartTarget.UnitRange)),
                SmartTargets.CreatureGuid => Marshal.SizeOf(typeof(SmartTarget.UnitGUID)),
                SmartTargets.CreatureDistance => Marshal.SizeOf(typeof(SmartTarget.UnitDistance)),
                SmartTargets.Stored => Marshal.SizeOf(typeof(SmartTarget.Stored)),
                SmartTargets.GameobjectRange => Marshal.SizeOf(typeof(SmartTarget.GoRange)),
                SmartTargets.GameobjectGuid => Marshal.SizeOf(typeof(SmartTarget.GoGUID)),
                SmartTargets.GameobjectDistance => Marshal.SizeOf(typeof(SmartTarget.GoDistance)),
                SmartTargets.InvokerParty => 0,
                SmartTargets.PlayerRange => Marshal.SizeOf(typeof(SmartTarget.PlayerRange)),
                SmartTargets.PlayerDistance => Marshal.SizeOf(typeof(SmartTarget.PlayerDistance)),
                SmartTargets.ClosestCreature => Marshal.SizeOf(typeof(SmartTarget.UnitClosest)),
                SmartTargets.ClosestGameobject => Marshal.SizeOf(typeof(SmartTarget.GoClosest)),
                SmartTargets.ClosestPlayer => Marshal.SizeOf(typeof(SmartTarget.PlayerDistance)),
                SmartTargets.ActionInvokerVehicle => 0,
                SmartTargets.OwnerOrSummoner => Marshal.SizeOf(typeof(SmartTarget.Owner)),
                SmartTargets.ThreatList => Marshal.SizeOf(typeof(SmartTarget.ThreatList)),
                SmartTargets.ClosestEnemy => Marshal.SizeOf(typeof(SmartTarget.ClosestAttackable)),
                SmartTargets.ClosestFriendly => Marshal.SizeOf(typeof(SmartTarget.ClosestFriendly)),
                SmartTargets.LootRecipients => 0,
                SmartTargets.Farthest => Marshal.SizeOf(typeof(SmartTarget.Farthest)),
                SmartTargets.VehiclePassenger => Marshal.SizeOf(typeof(SmartTarget.Vehicle)),
                SmartTargets.ClosestUnspawnedGameobject => Marshal.SizeOf(typeof(SmartTarget.GoClosest)),
                _ => Marshal.SizeOf(typeof(SmartTarget.Raw)),
            };

            int rawCount = Marshal.SizeOf(typeof(SmartTarget.Raw)) / sizeof(int);
            int paramsCount = paramsStructSize / sizeof(int);

            for (int index = paramsCount; index < rawCount; index++)
            {
                int value = 0;
                switch (index)
                {
                    case 0:
                        value = e.Target.raw.param1;
                        break;
                    case 1:
                        value = e.Target.raw.param2;
                        break;
                    case 2:
                        value = e.Target.raw.param3;
                        break;
                    case 3:
                        value = e.Target.raw.param4;
                        break;
                }

                if (value != 0)
                {
                    Log.outWarn(LogFilter.Sql,
                        $"SmartAIMgr: {e} has unused target_param{index + 1} " +
                        $"with value {value}, it must be 0, skipped.");
                }
            }

            return true;
        }

        bool IsEventValid(SmartScriptHolder e)
        {
            if (e.Event.type >= SmartEvents.End)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: EntryOrGuid {e.EntryOrGuid} using event({e.EventId}) " +
                    $"has invalid event Type ({e.GetEventType()}), skipped.");
                return false;
            }

            // in SMART_SCRIPT_TYPE_TIMED_ACTIONLIST all event types are overriden by core
            if (e.GetScriptType() != SmartScriptType.TimedActionlist 
                && !Convert.ToBoolean(GetEventMask(e.Event.type) & GetTypeMask(e.GetScriptType())))
            {
                Log.outError(LogFilter.Scripts, 
                    $"SmartAIMgr: EntryOrGuid {e.EntryOrGuid}, event Type {e.GetEventType()} " +
                    $"can not be used for Script Type {e.GetScriptType()}");
                return false;
            }
            if (e.Action.type <= 0 || e.Action.type >= SmartActions.End)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: EntryOrGuid {e.EntryOrGuid} using event({e.EventId}) " +
                    $"has invalid action Type ({e.GetActionType()}), skipped.");
                return false;
            }
            if (e.Event.event_phase_mask > (uint)SmartEventPhaseBits.All)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: EntryOrGuid {e.EntryOrGuid} using event({e.EventId}) " +
                    $"has invalid phase mask ({e.Event.event_phase_mask}), skipped.");
                return false;
            }
            if (e.Event.event_flags > SmartEventFlags.All)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: EntryOrGuid {e.EntryOrGuid} using event({e.EventId}) " +
                    $"has invalid event flags ({e.Event.event_flags}), skipped.");
                return false;
            }
            if (e.Event.event_flags.HasFlag(SmartEventFlags.Deprecated))
            {
                Log.outError(LogFilter.Sql, 
                    $"SmartAIMgr: EntryOrGuid {e.EntryOrGuid} using event ({e.EventId}) " +
                    $"has deprecated event flags ({e.Event.event_flags}), skipped.");
                return false;
            }
            if (e.Link != 0 && e.Link == e.EventId)
            {
                Log.outError(LogFilter.Sql, 
                    $"SmartAIMgr: EntryOrGuid {e.EntryOrGuid} SourceType {e.GetScriptType()}, " +
                    $"Event {e.EventId}, Event is linking self (infinite loop), skipped.");
                return false;
            }
            if (e.GetScriptType() == SmartScriptType.TimedActionlist)
            {
                e.Event.type = SmartEvents.UpdateOoc;//force default OOC, can change when calling the script!
                if (!IsMinMaxValid(e, e.Event.minMaxRepeat.min, e.Event.minMaxRepeat.max))
                    return false;

                if (!IsMinMaxValid(e, e.Event.minMaxRepeat.repeatMin, e.Event.minMaxRepeat.repeatMax))
                    return false;
            }
            else
            {
                switch (e.Event.type)
                {
                    case SmartEvents.Update:
                    case SmartEvents.UpdateIc:
                    case SmartEvents.UpdateOoc:
                    case SmartEvents.HealthPct:
                    case SmartEvents.ManaPct:
                    case SmartEvents.Range:
                    case SmartEvents.Damaged:
                    case SmartEvents.DamagedTarget:
                    case SmartEvents.ReceiveHeal:
                        if (!IsMinMaxValid(e, e.Event.minMaxRepeat.min, e.Event.minMaxRepeat.max))
                            return false;

                        if (!IsMinMaxValid(e, e.Event.minMaxRepeat.repeatMin, e.Event.minMaxRepeat.repeatMax))
                            return false;
                        break;
                    case SmartEvents.SpellHit:
                    case SmartEvents.SpellHitTarget:
                        if (e.Event.spellHit.spell != 0)
                        {
                            SpellInfo spellInfo = 
                                Global.SpellMgr.GetSpellInfo(e.Event.spellHit.spell, Difficulty.None);

                            if (spellInfo == null)
                            {
                                Log.outError(LogFilter.ScriptsAi, 
                                    $"SmartAIMgr: {e} uses non-existent Spell entry " +
                                    $"{e.Event.spellHit.spell}, skipped.");
                                return false;
                            }
                            if (e.Event.spellHit.school != 0 
                                && ((SpellSchoolMask)e.Event.spellHit.school & spellInfo.SchoolMask) != spellInfo.SchoolMask)
                            {
                                Log.outError(LogFilter.ScriptsAi, 
                                    $"SmartAIMgr: {e} uses Spell entry {e.Event.spellHit.spell} " +
                                    $"with invalid school mask, skipped.");
                                return false;
                            }
                        }
                        if (!IsMinMaxValid(e, e.Event.spellHit.cooldownMin, e.Event.spellHit.cooldownMax))
                            return false;
                        break;
                    case SmartEvents.OnSpellCast:
                    case SmartEvents.OnSpellFailed:
                    case SmartEvents.OnSpellStart:
                    {
                        if (!IsSpellValid(e, e.Event.spellCast.spell))
                            return false;

                        if (!IsMinMaxValid(e, e.Event.spellCast.cooldownMin, e.Event.spellCast.cooldownMax))
                            return false;
                        break;
                    }
                    case SmartEvents.OocLos:
                    case SmartEvents.IcLos:
                        if (!IsMinMaxValid(e, e.Event.los.cooldownMin, e.Event.los.cooldownMax))
                            return false;
                        if (e.Event.los.hostilityMode >= (uint)LOSHostilityMode.End)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: {e} uses hostilityMode with invalid value " +
                                $"{e.Event.los.hostilityMode} " +
                                $"(max allowed value {LOSHostilityMode.End - 1}), skipped.");
                            return false;
                        }
                        TC_SAI_IS_BOOLEAN_VALID(e, e.Event.los.playerOnly);
                        break;
                    case SmartEvents.Respawn:
                        if (e.Event.respawn.type == (uint)SmartRespawnCondition.Map 
                            && CliDB.MapStorage.LookupByKey(e.Event.respawn.map) == null)
                        {
                            Log.outError(LogFilter.ScriptsAi, 
                                $"SmartAIMgr: {e} uses non-existent Map entry " +
                                $"{e.Event.respawn.map}, skipped.");
                            return false;
                        }
                        if (e.Event.respawn.type == (uint)SmartRespawnCondition.Area 
                            && !CliDB.AreaTableStorage.ContainsKey(e.Event.respawn.area))
                        {
                            Log.outError(LogFilter.ScriptsAi, 
                                $"SmartAIMgr: {e} uses non-existent Area entry " +
                                $"{e.Event.respawn.area}, skipped.");
                            return false;
                        }
                        break;
                    case SmartEvents.FriendlyIsCc:
                        if (!IsMinMaxValid(e, e.Event.friendlyCC.repeatMin, e.Event.friendlyCC.repeatMax))
                            return false;
                        break;
                    case SmartEvents.FriendlyMissingBuff:
                    {
                        if (!IsSpellValid(e, e.Event.missingBuff.spell))
                            return false;

                        if (!NotNULL(e, e.Event.missingBuff.radius))
                            return false;

                        if (!IsMinMaxValid(e, e.Event.missingBuff.repeatMin, e.Event.missingBuff.repeatMax))
                            return false;
                        break;
                    }
                    case SmartEvents.Kill:
                        if (!IsMinMaxValid(e, e.Event.kill.cooldownMin, e.Event.kill.cooldownMax))
                            return false;

                        if (e.Event.kill.creature != 0 && !IsCreatureValid(e, e.Event.kill.creature))
                            return false;

                        TC_SAI_IS_BOOLEAN_VALID(e, e.Event.kill.playerOnly);
                        break;
                    case SmartEvents.VictimCasting:
                        if (e.Event.targetCasting.spellId > 0 
                            && !Global.SpellMgr.HasSpellInfo(e.Event.targetCasting.spellId, Difficulty.None))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                                $"Event {e.EventId} Action {e.GetActionType()} " +
                                $"uses non-existent Spell entry {e.Event.spellHit.spell}, skipped.");
                            return false;
                        }

                        if (!IsMinMaxValid(e, e.Event.minMax.repeatMin, e.Event.minMax.repeatMax))
                            return false;
                        break;
                    case SmartEvents.PassengerBoarded:
                    case SmartEvents.PassengerRemoved:
                        if (!IsMinMaxValid(e, e.Event.minMax.repeatMin, e.Event.minMax.repeatMax))
                            return false;
                        break;
                    case SmartEvents.SummonDespawned:
                    case SmartEvents.SummonedUnit:
                    case SmartEvents.SummonedUnitDies:
                        if (e.Event.summoned.creature != 0 && !IsCreatureValid(e, e.Event.summoned.creature))
                            return false;

                        if (!IsMinMaxValid(e, e.Event.summoned.cooldownMin, e.Event.summoned.cooldownMax))
                            return false;
                        break;
                    case SmartEvents.AcceptedQuest:
                    case SmartEvents.RewardQuest:
                        if (e.Event.quest.questId != 0 && !IsQuestValid(e, e.Event.quest.questId))
                            return false;

                        if (!IsMinMaxValid(e, e.Event.quest.cooldownMin, e.Event.quest.cooldownMax))
                            return false;
                        break;
                    case SmartEvents.ReceiveEmote:
                    {
                        if (e.Event.emote.emoteId != 0 && !IsTextEmoteValid(e, e.Event.emote.emoteId))
                            return false;

                        if (!IsMinMaxValid(e, e.Event.emote.cooldownMin, e.Event.emote.cooldownMax))
                            return false;
                        break;
                    }
                    case SmartEvents.HasAura:
                    case SmartEvents.TargetBuffed:
                    {
                        if (!IsSpellValid(e, e.Event.aura.spell))
                            return false;

                        if (!IsMinMaxValid(e, e.Event.aura.repeatMin, e.Event.aura.repeatMax))
                            return false;
                        break;
                    }
                    case SmartEvents.TransportAddcreature:
                    {
                        if (e.Event.transportAddCreature.creature != 0
                            && !IsCreatureValid(e, e.Event.transportAddCreature.creature))
                        {
                            return false;
                        }
                        break;
                    }
                    case SmartEvents.Movementinform:
                    {
                        if (MotionMaster.IsInvalidMovementGeneratorType((MovementGeneratorType)e.Event.movementInform.type))
                        {
                            Log.outError(LogFilter.ScriptsAi,
                                $"SmartAIMgr: {e} uses invalid Motion Type " +
                                $"{e.Event.movementInform.type}, skipped.");
                            return false;
                        }
                        break;
                    }
                    case SmartEvents.DataSet:
                    {
                        if (!IsMinMaxValid(e, e.Event.dataSet.cooldownMin, e.Event.dataSet.cooldownMax))
                            return false;
                        break;
                    }
                    case SmartEvents.AreatriggerOntrigger:
                    {
                        if (e.Event.areatrigger.id != 0 
                            && (e.GetScriptType() == SmartScriptType.AreaTriggerEntity || e.GetScriptType() == SmartScriptType.AreaTriggerEntityCustom))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: Entry {e.EntryOrGuid} SourceType " +
                                $"{e.GetScriptType()} Event {e.EventId} Action {e.GetActionType()} " +
                                $"areatrigger param not supported for SMART_SCRIPT_TYPE_AREATRIGGER_ENTITY " +
                                $"and SMART_SCRIPT_TYPE_AREATRIGGER_ENTITY_CUSTOM, skipped.");
                            return false;
                        }

                        if (e.Event.areatrigger.id != 0 && !IsAreaTriggerValid(e, e.Event.areatrigger.id))
                            return false;
                        break;
                    }
                    case SmartEvents.TextOver:
                    {
                        if (!IsTextValid(e, e.Event.textOver.textGroupID))
                            return false;
                        break;
                    }
                    case SmartEvents.GameEventStart:
                    case SmartEvents.GameEventEnd:
                    {
                        var events = Global.GameEventMgr.GetEventMap();
                        if (e.Event.gameEvent.gameEventId >= events.Length
                            || !events[e.Event.gameEvent.gameEventId].IsValid())
                        {
                            return false;
                        }

                        break;
                    }
                    case SmartEvents.FriendlyHealthPCT:
                        if (!IsMinMaxValid(e, e.Event.friendlyHealthPct.repeatMin, e.Event.friendlyHealthPct.repeatMax))
                            return false;

                        if (e.Event.friendlyHealthPct.maxHpPct > 100 || e.Event.friendlyHealthPct.minHpPct > 100)
                        {
                            Log.outError(LogFilter.Sql, $"SmartAIMgr: {e} has pct value above 100, skipped.");
                            return false;
                        }

                        switch (e.GetTargetType())
                        {
                            case SmartTargets.CreatureRange:
                            case SmartTargets.CreatureGuid:
                            case SmartTargets.CreatureDistance:
                            case SmartTargets.ClosestCreature:
                            case SmartTargets.ClosestPlayer:
                            case SmartTargets.PlayerRange:
                            case SmartTargets.PlayerDistance:
                                break;
                            case SmartTargets.ActionInvoker:
                                if (!NotNULL(e, e.Event.friendlyHealthPct.radius))
                                    return false;
                                break;
                            default:
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr: {e} uses invalid target_type {e.GetTargetType()}, skipped.");
                                return false;
                        }
                        break;
                    case SmartEvents.DistanceCreature:
                        if (e.Event.distance.guid == 0 && e.Event.distance.entry == 0)
                        {
                            Log.outError(LogFilter.Sql, 
                                "SmartAIMgr: Event SMART_EVENT_DISTANCE_CREATURE " +
                                "did not provide creature guid or entry, skipped.");
                            return false;
                        }

                        if (e.Event.distance.guid != 0 && e.Event.distance.entry != 0)
                        {
                            Log.outError(LogFilter.Sql, 
                                "SmartAIMgr: Event SMART_EVENT_DISTANCE_CREATURE " +
                                "provided both an entry and guid, skipped.");
                            return false;
                        }

                        if (e.Event.distance.guid != 0 
                            && Global.ObjectMgr.GetCreatureData(e.Event.distance.guid) == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: Event SMART_EVENT_DISTANCE_CREATURE " +
                                $"using invalid creature guid {e.Event.distance.guid}, skipped.");
                            return false;
                        }

                        if (e.Event.distance.entry != 0 
                            && Global.ObjectMgr.GetCreatureTemplate(e.Event.distance.entry) == null)
                        {
                            Log.outError(LogFilter.Sql,
                                $"SmartAIMgr: Event SMART_EVENT_DISTANCE_CREATURE " +
                                $"using invalid creature entry {e.Event.distance.entry}, skipped.");
                            return false;
                        }
                        break;
                    case SmartEvents.DistanceGameobject:
                        if (e.Event.distance.guid == 0 && e.Event.distance.entry == 0)
                        {
                            Log.outError(LogFilter.Sql, 
                                "SmartAIMgr: Event SMART_EVENT_DISTANCE_GAMEOBJECT " +
                                "did not provide gameobject guid or entry, skipped.");
                            return false;
                        }

                        if (e.Event.distance.guid != 0 && e.Event.distance.entry != 0)
                        {
                            Log.outError(LogFilter.Sql, 
                                "SmartAIMgr: Event SMART_EVENT_DISTANCE_GAMEOBJECT " +
                                "provided both an entry and guid, skipped.");
                            return false;
                        }

                        if (e.Event.distance.guid != 0 
                            && Global.ObjectMgr.GetGameObjectData(e.Event.distance.guid) == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: Event SMART_EVENT_DISTANCE_GAMEOBJECT" +
                                $" using invalid gameobject guid {e.Event.distance.guid}, skipped.");
                            return false;
                        }

                        if (e.Event.distance.entry != 0 
                            && Global.ObjectMgr.GetGameObjectTemplate(e.Event.distance.entry) == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: Event SMART_EVENT_DISTANCE_GAMEOBJECT " +
                                $"using invalid gameobject entry {e.Event.distance.entry}, skipped.");
                            return false;
                        }
                        break;
                    case SmartEvents.CounterSet:
                        if (!IsMinMaxValid(e, e.Event.counter.cooldownMin, e.Event.counter.cooldownMax))
                            return false;

                        if (e.Event.counter.id == 0)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: Event SMART_EVENT_COUNTER_SET " +
                                $"using invalid counter id {e.Event.counter.id}, skipped.");
                            return false;
                        }

                        if (e.Event.counter.value == 0)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: Event SMART_EVENT_COUNTER_SET " +
                                $"using invalid value {e.Event.counter.value}, skipped.");
                            return false;
                        }
                        break;
                    case SmartEvents.Reset:
                        if (e.Action.type == SmartActions.CallScriptReset)
                        {
                            // There might be SMART_TARGET_* cases where this should be allowed,
                            // they will be handled if needed
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: {e} uses event SMART_EVENT_RESET and action " +
                                $"SMART_ACTION_CALL_SCRIPT_RESET, skipped.");
                            return false;
                        }
                        break;
                    case SmartEvents.Charmed:
                        TC_SAI_IS_BOOLEAN_VALID(e, e.Event.charm.onRemove);
                        break;
                    case SmartEvents.QuestObjCompletion:
                        if (Global.ObjectMgr.GetQuestObjective(e.Event.questObjective.id) == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: Event SMART_EVENT_QUEST_OBJ_COMPLETION " +
                                $"using invalid objective id {e.Event.questObjective.id}, skipped.");
                            return false;
                        }
                        break;
                    case SmartEvents.QuestAccepted:
                    case SmartEvents.QuestCompletion:
                    case SmartEvents.QuestFail:
                    case SmartEvents.QuestRewarded:
                        break;
                    case SmartEvents.Link:
                    case SmartEvents.GoLootStateChanged:
                    case SmartEvents.GoEventInform:
                    case SmartEvents.TimedEventTriggered:
                    case SmartEvents.InstancePlayerEnter:
                    case SmartEvents.TransportRelocate:
                    case SmartEvents.CorpseRemoved:
                    case SmartEvents.AiInit:
                    case SmartEvents.ActionDone:
                    case SmartEvents.TransportAddplayer:
                    case SmartEvents.TransportRemovePlayer:
                    case SmartEvents.Aggro:
                    case SmartEvents.Death:
                    case SmartEvents.Evade:
                    case SmartEvents.ReachedHome:
                    case SmartEvents.JustSummoned:
                    case SmartEvents.WaypointReached:
                    case SmartEvents.WaypointPaused:
                    case SmartEvents.WaypointResumed:
                    case SmartEvents.WaypointStopped:
                    case SmartEvents.WaypointEnded:
                    case SmartEvents.GossipSelect:
                    case SmartEvents.GossipHello:
                    case SmartEvents.JustCreated:
                    case SmartEvents.FollowCompleted:
                    case SmartEvents.OnSpellclick:
                    case SmartEvents.OnDespawn:
                    case SmartEvents.SceneStart:
                    case SmartEvents.SceneCancel:
                    case SmartEvents.SceneComplete:
                    case SmartEvents.SceneTrigger:
                    case SmartEvents.SendEventTrigger:
                        break;

                    //Unused
                    case SmartEvents.TargetHealthPct:
                    case SmartEvents.FriendlyHealth:
                    case SmartEvents.TargetManaPct:
                    case SmartEvents.CharmedTarget:
                    case SmartEvents.WaypointStart:
                    case SmartEvents.PhaseChange:
                    case SmartEvents.IsBehindTarget:
                        Log.outError(LogFilter.Sql, $"SmartAIMgr: Unused event_type {e} skipped.");
                        return false;
                    default:
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: Not handled event_type({e.GetEventType()}), " +
                            $"Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} Event {e.EventId} " +
                            $"Action {e.GetActionType()}, skipped.");
                        return false;
                }
            }

            if (!CheckUnusedEventParams(e))
                return false;

            switch (e.GetActionType())
            {
                case SmartActions.Talk:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.talk.useTalkTarget);

                    if (!IsTextValid(e, e.Action.talk.textGroupId))
                        return false;
                    break;
                }
                case SmartActions.SimpleTalk:
                {
                    if (!IsTextValid(e, e.Action.simpleTalk.textGroupId))
                        return false;
                    break;
                }
                case SmartActions.SetFaction:
                    if (e.Action.faction.factionId != 0 
                        && CliDB.FactionTemplateStorage.LookupByKey(e.Action.faction.factionId) == null)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} uses non-existent Faction " +
                            $"{e.Action.faction.factionId}, skipped.");
                        return false;
                    }
                    break;
                case SmartActions.MorphToEntryOrModel:
                case SmartActions.MountToEntryOrModel:
                    if (e.Action.morphOrMount.creature != 0 || e.Action.morphOrMount.model != 0)
                    {
                        if (e.Action.morphOrMount.creature > 0 
                            && Global.ObjectMgr.GetCreatureTemplate(e.Action.morphOrMount.creature) == null)
                        {
                            Log.outError(LogFilter.ScriptsAi, 
                                $"SmartAIMgr: {e} uses non-existent Creature entry " +
                                $"{e.Action.morphOrMount.creature}, skipped.");
                            return false;
                        }

                        if (e.Action.morphOrMount.model != 0)
                        {
                            if (e.Action.morphOrMount.creature != 0)
                            {
                                Log.outError(LogFilter.ScriptsAi, 
                                    $"SmartAIMgr: {e} has ModelID set with also set CreatureId, skipped.");
                                return false;
                            }
                            else if (!CliDB.CreatureDisplayInfoStorage.ContainsKey(e.Action.morphOrMount.model))
                            {
                                Log.outError(LogFilter.ScriptsAi, 
                                    $"SmartAIMgr: {e} uses non-existent Model id " +
                                    $"{e.Action.morphOrMount.model}, skipped.");
                                return false;
                            }
                        }
                    }
                    break;
                case SmartActions.Sound:
                    if (!IsSoundValid(e, e.Action.sound.soundId))
                        return false;

                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.sound.onlySelf);
                    break;
                case SmartActions.SetEmoteState:
                case SmartActions.PlayEmote:
                    if (!IsEmoteValid(e, e.Action.emote.emoteId))
                        return false;
                    break;
                case SmartActions.PlayAnimkit:
                    if (e.Action.animKit.animKit != 0 && !IsAnimKitValid(e, e.Action.animKit.animKit))
                        return false;

                    if (e.Action.animKit.type > 3)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses invalid AnimKit Type " +
                            $"{e.Action.animKit.type}, skipped.");
                        return false;
                    }
                    break;
                case SmartActions.PlaySpellVisualKit:
                    if (e.Action.spellVisualKit.spellVisualKitId != 0 
                        && !IsSpellVisualKitValid(e, e.Action.spellVisualKit.spellVisualKitId))
                        return false;
                    break;
                case SmartActions.OfferQuest:
                    if (!IsQuestValid(e, e.Action.questOffer.questId))
                        return false;

                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.questOffer.directAdd);
                    break;
                case SmartActions.FailQuest:
                    if (!IsQuestValid(e, e.Action.quest.questId))
                        return false;
                    break;
                case SmartActions.ActivateTaxi:
                {
                    if (!CliDB.TaxiPathStorage.ContainsKey(e.Action.taxi.id))
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} uses invalid Taxi path ID {e.Action.taxi.id}, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartActions.RandomEmote:
                    if (e.Action.randomEmote.emote1 != 0 && !IsEmoteValid(e, e.Action.randomEmote.emote1))
                        return false;
                    if (e.Action.randomEmote.emote2 != 0 && !IsEmoteValid(e, e.Action.randomEmote.emote2))
                        return false;
                    if (e.Action.randomEmote.emote3 != 0 && !IsEmoteValid(e, e.Action.randomEmote.emote3))
                        return false;
                    if (e.Action.randomEmote.emote4 != 0 && !IsEmoteValid(e, e.Action.randomEmote.emote4))
                        return false;
                    if (e.Action.randomEmote.emote5 != 0 && !IsEmoteValid(e, e.Action.randomEmote.emote5))
                        return false;
                    if (e.Action.randomEmote.emote6 != 0 && !IsEmoteValid(e, e.Action.randomEmote.emote6))
                        return false;
                    break;
                case SmartActions.RandomSound:
                    if (e.Action.randomSound.sound1 != 0 && !IsSoundValid(e, e.Action.randomSound.sound1))
                        return false;
                    if (e.Action.randomSound.sound2 != 0 && !IsSoundValid(e, e.Action.randomSound.sound2))
                        return false;
                    if (e.Action.randomSound.sound3 != 0 && !IsSoundValid(e, e.Action.randomSound.sound3))
                        return false;
                    if (e.Action.randomSound.sound4 != 0 && !IsSoundValid(e, e.Action.randomSound.sound4))
                        return false;

                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.randomSound.onlySelf);
                    break;
                case SmartActions.Cast:
                {
                    if (!IsSpellValid(e, e.Action.cast.spell))
                        return false;

                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(e.Action.cast.spell, Difficulty.None);
                    foreach (var spellEffectInfo in spellInfo.GetEffects())
                    {
                        if (spellEffectInfo.IsEffect(SpellEffectName.KillCredit) 
                            || spellEffectInfo.IsEffect(SpellEffectName.KillCredit2))
                        {
                            if (spellEffectInfo.TargetA.GetTarget() == Targets.UnitCaster)
                            {
                                Log.outError(LogFilter.Sql,
                                    $"SmartAIMgr: {e} Effect: SPELL_EFFECT_KILL_CREDIT: " +
                                    $"(SpellId: {e.Action.cast.spell} " +
                                    $"targetA: {spellEffectInfo.TargetA.GetTarget()} - " +
                                    $"targetB: {spellEffectInfo.TargetB.GetTarget()}) " +
                                    $"has invalid target for this Action");
                            }
                        }
                    }
                    break;
                }
                case SmartActions.CrossCast:
                {
                    if (!IsSpellValid(e, e.Action.crossCast.spell))
                        return false;

                    SmartTargets targetType = (SmartTargets)e.Action.crossCast.targetType;
                    if (targetType == SmartTargets.CreatureGuid || targetType == SmartTargets.GameobjectGuid)
                    {
                        if (e.Action.crossCast.targetParam2 != 0)
                        {
                            if (targetType == SmartTargets.CreatureGuid 
                                && !IsCreatureValid(e, e.Action.crossCast.targetParam2))
                                return false;
                            else if (targetType == SmartTargets.GameobjectGuid 
                                && !IsGameObjectValid(e, e.Action.crossCast.targetParam2))
                                return false;
                        }

                        long guid = e.Action.crossCast.targetParam1;

                        SpawnObjectType spawnType = 
                            targetType == SmartTargets.CreatureGuid 
                            ? SpawnObjectType.Creature 
                            : SpawnObjectType.GameObject;

                        var data = Global.ObjectMgr.GetSpawnData(spawnType, guid);
                        if (data == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: {e} specifies invalid CasterTargetType guid " +
                                $"({spawnType},{guid})");
                            return false;
                        }
                        else if (e.Action.crossCast.targetParam2 != 0 
                            && e.Action.crossCast.targetParam2 != data.Id)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: {e} specifies invalid entry " +
                                $"{e.Action.crossCast.targetParam2} (expected {data.Id}) " +
                                $"for CasterTargetType guid ({spawnType},{guid})");
                            return false;
                        }
                    }
                    break;
                }
                case SmartActions.InvokerCast:
                    if (e.GetScriptType() != SmartScriptType.TimedActionlist 
                        && e.GetEventType() != SmartEvents.Link && !EventHasInvoker(e.Event.type))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} has invoker cast action, " +
                            $"but event does not provide any invoker!");
                        return false;
                    }
                    if (!IsSpellValid(e, e.Action.cast.spell))
                        return false;
                    break;
                case SmartActions.SelfCast:
                    if (!IsSpellValid(e, e.Action.cast.spell))
                        return false;
                    break;
                case SmartActions.CallAreaexploredoreventhappens:
                case SmartActions.CallGroupeventhappens:
                    Quest qid = Global.ObjectMgr.GetQuestTemplate(e.Action.quest.questId);
                    if (qid != null)
                    {
                        if (!qid.HasAnyFlag(QuestFlags.CompletionEvent) 
                            && !qid.HasAnyFlag(QuestFlags.CompletionAreaTrigger))
                        {
                            Log.outError(LogFilter.ScriptsAi, 
                                $"SmartAIMgr: {e} Flags for Quest entry {e.Action.quest.questId} " +
                                $"does not include QUEST_FLAGS_COMPLETION_EVENT " +
                                $"or QUEST_FLAGS_COMPLETION_AREA_TRIGGER, skipped.");
                            return false;
                        }
                    }
                    else
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} uses non-existent Quest entry " +
                            $"{e.Action.quest.questId}, skipped.");
                        return false;
                    }
                    break;
                case SmartActions.SetEventPhase:
                    if (e.Action.setEventPhase.phase >= (uint)SmartPhase.Max)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} attempts to set phase {e.Action.setEventPhase.phase}. " +
                            $"Phase mask cannot be used past phase {SmartPhase.Max - 1}, skipped.");
                        return false;
                    }
                    break;
                case SmartActions.IncEventPhase:
                    if (e.Action.incEventPhase.inc == 0 && e.Action.incEventPhase.dec == 0)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} is incrementing phase by 0, skipped.");
                        return false;
                    }
                    else if (e.Action.incEventPhase.inc > (uint)SmartPhase.Max 
                        || e.Action.incEventPhase.dec > (uint)SmartPhase.Max)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} attempts to increment phase by too large value, skipped.");
                        return false;
                    }
                    break;
                case SmartActions.RemoveAurasFromSpell:
                    if (e.Action.removeAura.spell != 0 && !IsSpellValid(e, e.Action.removeAura.spell))
                        return false;

                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.removeAura.onlyOwnedAuras);
                    break;
                case SmartActions.RandomPhase:
                {
                    if (e.Action.randomPhase.phase1 >= (uint)SmartPhase.Max ||
                        e.Action.randomPhase.phase2 >= (uint)SmartPhase.Max ||
                        e.Action.randomPhase.phase3 >= (uint)SmartPhase.Max ||
                        e.Action.randomPhase.phase4 >= (uint)SmartPhase.Max ||
                        e.Action.randomPhase.phase5 >= (uint)SmartPhase.Max ||
                        e.Action.randomPhase.phase6 >= (uint)SmartPhase.Max)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                            $"Event {e.EventId} Action {e.GetActionType()} " +
                            $"attempts to set invalid phase, skipped.");
                        return false;
                    }

                    break;
                }
                case SmartActions.RandomPhaseRange:       //PhaseMin, PhaseMax
                {
                    if (e.Action.randomPhaseRange.phaseMin >= (uint)SmartPhase.Max ||
                        e.Action.randomPhaseRange.phaseMax >= (uint)SmartPhase.Max)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} attempts to set invalid phase, skipped.");
                        return false;
                    }
                    if (!IsMinMaxValid(e, e.Action.randomPhaseRange.phaseMin, e.Action.randomPhaseRange.phaseMax))
                        return false;
                    break;
                }
                case SmartActions.SummonCreature:
                    if (!IsCreatureValid(e, e.Action.summonCreature.creature))
                        return false;

                    if (e.Action.summonCreature.type < (uint)TempSummonType.TimedOrDeadDespawn 
                        || e.Action.summonCreature.type > (uint)TempSummonType.ManualDespawn)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} uses incorrect TempSummonType " +
                            $"{e.Action.summonCreature.type}, skipped.");
                        return false;
                    }

                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.summonCreature.attackInvoker);
                    break;
                case SmartActions.CallKilledmonster:
                    if (!IsCreatureValid(e, e.Action.killedMonster.creature))
                        return false;

                    if (e.GetTargetType() == SmartTargets.Position)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses incorrect TargetType " +
                            $"{e.GetTargetType()}, skipped.");
                        return false;
                    }
                    break;
                case SmartActions.UpdateTemplate:
                    if (e.Action.updateTemplate.creature != 0 
                        && !IsCreatureValid(e, e.Action.updateTemplate.creature))
                        return false;

                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.updateTemplate.updateLevel);
                    break;
                case SmartActions.SetSheath:
                    if (e.Action.setSheath.sheath != 0 
                        && e.Action.setSheath.sheath >= (uint)SheathState.Max)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} uses incorrect Sheath state " +
                            $"{e.Action.setSheath.sheath}, skipped.");
                        return false;
                    }
                    break;
                case SmartActions.SetReactState:
                {
                    if (e.Action.react.state > (uint)ReactStates.Aggressive)
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: Creature {e.EntryOrGuid} Event {e.EventId} " +
                            $"Action {e.GetActionType()} uses invalid React State " +
                            $"{e.Action.react.state}, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartActions.SummonGo:
                    if (!IsGameObjectValid(e, e.Action.summonGO.entry))
                        return false;
                    break;
                case SmartActions.RemoveItem:
                    if (!IsItemValid(e, e.Action.item.entry))
                        return false;

                    if (!NotNULL(e, e.Action.item.count))
                        return false;
                    break;
                case SmartActions.AddItem:
                    if (!IsItemValid(e, e.Action.item.entry))
                        return false;

                    if (!NotNULL(e, e.Action.item.count))
                        return false;
                    break;
                case SmartActions.Teleport:
                    if (!CliDB.MapStorage.ContainsKey(e.Action.teleport.mapID))
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} uses non-existent Map entry " +
                            $"{e.Action.teleport.mapID}, skipped.");
                        return false;
                    }
                    break;
                case SmartActions.WpStop:
                    if (e.Action.wpStop.quest != 0 && !IsQuestValid(e, e.Action.wpStop.quest))
                        return false;

                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.wpStop.fail);
                    break;
                case SmartActions.WpStart:
                {
                    WaypointPath path = Global.WaypointMgr.GetPath(e.Action.wpStart.pathID);
                    if (path == null || path.Nodes.Empty())
                    {
                        Log.outError(LogFilter.ScriptsAi, 
                            $"SmartAIMgr: {e} uses non-existent WaypointPath id " +
                            $"{e.Action.wpStart.pathID}, skipped.");
                        return false;
                    }

                    if (e.Action.wpStart.quest != 0 && !IsQuestValid(e, e.Action.wpStart.quest))
                        return false;

                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.wpStart.run);
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.wpStart.repeat);
                    break;
                }
                case SmartActions.CreateTimedEvent:
                {
                    if (!IsMinMaxValid(e, e.Action.timeEvent.min, e.Action.timeEvent.max))
                        return false;

                    if (!IsMinMaxValid(e, e.Action.timeEvent.repeatMin, e.Action.timeEvent.repeatMax))
                        return false;
                    break;
                }
                case SmartActions.CallRandomRangeTimedActionlist:
                {
                    if (!IsMinMaxValid(e, e.Action.randRangeTimedActionList.idMin, e.Action.randRangeTimedActionList.idMax))
                        return false;
                    break;
                }
                case SmartActions.SetPower:
                case SmartActions.AddPower:
                case SmartActions.RemovePower:
                    if (e.Action.power.powerType > (int)PowerType.Max)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses non-existent Power " +
                            $"{e.Action.power.powerType}, skipped.");
                        return false;
                    }
                    break;
                case SmartActions.GameEventStop:
                {
                    int eventId = e.Action.gameEventStop.id;

                    var events = Global.GameEventMgr.GetEventMap();
                    if (eventId < 1 || eventId >= events.Length)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses non-existent event, eventId " +
                            $"{e.Action.gameEventStop.id}, skipped.");
                        return false;
                    }

                    GameEventData eventData = events[eventId];
                    if (!eventData.IsValid())
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses non-existent event, eventId " +
                            $"{e.Action.gameEventStop.id}, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartActions.GameEventStart:
                {
                    int eventId = e.Action.gameEventStart.id;

                    var events = Global.GameEventMgr.GetEventMap();
                    if (eventId < 1 || eventId >= events.Length)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses non-existent event, eventId " +
                            $"{e.Action.gameEventStart.id}, skipped.");
                        return false;
                    }

                    GameEventData eventData = events[eventId];
                    if (!eventData.IsValid())
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses non-existent event, eventId " +
                            $"{e.Action.gameEventStart.id}, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartActions.Equip:
                {
                    if (e.GetScriptType() == SmartScriptType.Creature)
                    {
                        sbyte equipId = (sbyte)e.Action.equip.entry;

                        if (equipId != 0 && Global.ObjectMgr.GetEquipmentInfo(e.EntryOrGuid, equipId) == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartScript: SMART_ACTION_EQUIP " +
                                $"uses non-existent equipment info id {equipId} " +
                                $"for creature {e.EntryOrGuid}, skipped.");
                            return false;
                        }
                    }
                    break;
                }
                case SmartActions.SetInstData:
                {
                    if (e.Action.setInstanceData.type > 1)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses invalid data Type " +
                            $"{e.Action.setInstanceData.type} (value range 0-1), skipped.");
                        return false;
                    }
                    else if (e.Action.setInstanceData.type == 1)
                    {
                        if (e.Action.setInstanceData.data > (int)EncounterState.ToBeDecided)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"SmartAIMgr: {e} uses invalid boss state " +
                                $"{e.Action.setInstanceData.data} (value range 0-5), skipped.");
                            return false;
                        }
                    }
                    break;
                }
                case SmartActions.SetIngamePhaseId:
                {
                    int phaseId = e.Action.ingamePhaseId.id;
                    int apply = e.Action.ingamePhaseId.apply;

                    if (apply != 0 && apply != 1)
                    {
                        Log.outError(LogFilter.Sql,
                            $"SmartScript: SMART_ACTION_SET_INGAME_PHASE_ID " +
                            $"uses invalid apply value {apply} (Should be 0 or 1) " +
                            $"for creature {e.EntryOrGuid}, skipped");
                        return false;
                    }

                    if (!CliDB.PhaseStorage.ContainsKey(phaseId))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartScript: SMART_ACTION_SET_INGAME_PHASE_ID " +
                            $"uses invalid phaseid {phaseId} " +
                            $"for creature {e.EntryOrGuid}, skipped");
                        return false;
                    }
                    break;
                }
                case SmartActions.SetIngamePhaseGroup:
                {
                    int phaseGroup = e.Action.ingamePhaseGroup.groupId;
                    int apply = e.Action.ingamePhaseGroup.apply;

                    if (apply != 0 && apply != 1)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartScript: SMART_ACTION_SET_INGAME_PHASE_GROUP " +
                            $"uses invalid apply value {apply} (Should be 0 or 1) " +
                            $"for creature {e.EntryOrGuid}, skipped");
                        return false;
                    }

                    if (Global.DB2Mgr.GetPhasesForGroup(phaseGroup).Empty())
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartScript: SMART_ACTION_SET_INGAME_PHASE_GROUP " +
                            $"uses invalid phase group id {phaseGroup} " +
                            $"for creature {e.EntryOrGuid}, skipped");
                        return false;
                    }
                    break;
                }
                case SmartActions.ScenePlay:
                {
                    if (Global.ObjectMgr.GetSceneTemplate(e.Action.scene.sceneId) == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartScript: SMART_ACTION_SCENE_PLAY " +
                            $"uses sceneId {e.Action.scene.sceneId} " +
                            $"but scene don't exist, skipped");
                        return false;
                    }

                    break;
                }
                case SmartActions.SceneCancel:
                {
                    if (Global.ObjectMgr.GetSceneTemplate(e.Action.scene.sceneId) == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartScript: SMART_ACTION_SCENE_CANCEL " +
                            $"uses sceneId {e.Action.scene.sceneId} " +
                            $"but scene don't exist, skipped");
                        return false;
                    }

                    break;
                }
                case SmartActions.RespawnBySpawnId:
                {
                    if (Global.ObjectMgr.GetSpawnData((SpawnObjectType)e.Action.respawnData.spawnType, e.Action.respawnData.spawnId) == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                            $"Event {e.EventId} Action {e.GetActionType()} " +
                            $"specifies invalid spawn data " +
                            $"({e.Action.respawnData.spawnType},{e.Action.respawnData.spawnId})");
                        return false;
                    }
                    break;
                }
                case SmartActions.EnableTempGobj:
                {
                    if (e.Action.enableTempGO.duration == 0)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                            $"Event {e.EventId} Action {e.GetActionType()} " +
                            $"does not specify duration");
                        return false;
                    }
                    break;
                }
                case SmartActions.PlayCinematic:
                {
                    if (!CliDB.CinematicSequencesStorage.ContainsKey(e.Action.cinematic.entry))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: SMART_ACTION_PLAY_CINEMATIC {e} " +
                            $"uses invalid entry {e.Action.cinematic.entry}, skipped.");
                        return false;
                    }

                    break;
                }
                case SmartActions.PauseMovement:
                {
                    if (e.Action.pauseMovement.pauseTimer == 0)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} does not specify pause duration");
                        return false;
                    }

                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.pauseMovement.force);
                    break;
                }
                case SmartActions.SetMovementSpeed:
                {
                    if (e.Action.movementSpeed.movementType >= (int)MovementGeneratorType.Max)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                            $"Event {e.EventId} Action {e.GetActionType()} " +
                            $"uses invalid movementType {e.Action.movementSpeed.movementType}, skipped.");
                        return false;
                    }

                    if (e.Action.movementSpeed.speedInteger == 0 
                        && e.Action.movementSpeed.speedFraction == 0)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                            $"Event {e.EventId} Action {e.GetActionType()} uses speed 0, skipped.");
                        return false;
                    }

                    break;
                }
                case SmartActions.OverrideLight:
                {
                    var areaEntry = CliDB.AreaTableStorage.LookupByKey(e.Action.overrideLight.zoneId);
                    if (areaEntry == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"SmartAIMgr: {e} uses non-existent zoneId " +
                            $"{e.Action.overrideLight.zoneId}, skipped.");
                        return false;
                    }

                    if (areaEntry.ParentAreaID != 0 
                        && areaEntry.HasFlag(AreaFlags.IsSubzone))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses subzone (ID: {e.Action.overrideLight.zoneId}) " +
                            $"instead of zone, skipped.");
                        return false;
                    }

                    if (!CliDB.LightStorage.ContainsKey(e.Action.overrideLight.areaLightId))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses non-existent areaLightId " +
                            $"{e.Action.overrideLight.areaLightId}, skipped.");
                        return false;
                    }

                    if (e.Action.overrideLight.overrideLightId != 0 
                        && !CliDB.LightStorage.ContainsKey(e.Action.overrideLight.overrideLightId))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses non-existent overrideLightId " +
                            $"{e.Action.overrideLight.overrideLightId}, skipped.");
                        return false;
                    }

                    break;
                }
                case SmartActions.OverrideWeather:
                {
                    var areaEntry = CliDB.AreaTableStorage.LookupByKey(e.Action.overrideWeather.zoneId);
                    if (areaEntry == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses non-existent zoneId " +
                            $"{e.Action.overrideWeather.zoneId}, skipped.");
                        return false;
                    }

                    if (areaEntry.ParentAreaID != 0 && areaEntry.HasFlag(AreaFlags.IsSubzone))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses subzone " +
                            $"(ID: {e.Action.overrideWeather.zoneId}) " +
                            $"instead of zone, skipped.");
                        return false;
                    }

                    break;
                }
                case SmartActions.SetAIAnimKit:
                {
                    Log.outError(LogFilter.Sql, 
                        $"SmartAIMgr: Deprecated Event:({e}) skipped.");
                    break;
                }
                case SmartActions.SetHealthPct:
                {
                    if (e.Action.setHealthPct.percent > 100 || e.Action.setHealthPct.percent == 0)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} is trying to set invalid HP percent " +
                            $"{e.Action.setHealthPct.percent}, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartActions.AutoAttack:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.autoAttack.attack);
                    break;
                }
                case SmartActions.AllowCombatMovement:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.combatMove.move);
                    break;
                }
                case SmartActions.CallForHelp:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.callHelp.withEmote);
                    break;
                }
                case SmartActions.SetVisibility:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.visibility.state);
                    break;
                }
                case SmartActions.SetActive:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.active.state);
                    break;
                }
                case SmartActions.SetRun:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.setRun.run);
                    break;
                }
                case SmartActions.SetDisableGravity:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.setDisableGravity.disable);
                    break;
                }
                case SmartActions.SetCounter:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.setCounter.reset);
                    break;
                }
                case SmartActions.CallTimedActionlist:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.timedActionList.allowOverride);
                    break;
                }
                case SmartActions.InterruptSpell:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.interruptSpellCasting.withDelayed);
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.interruptSpellCasting.withInstant);
                    break;
                }
                case SmartActions.FleeForAssist:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.fleeAssist.withEmote);
                    break;
                }
                case SmartActions.MoveToPos:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.moveToPos.transport);
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.moveToPos.disablePathfinding);
                    break;
                }
                case SmartActions.SetRoot:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.setRoot.root);
                    break;
                }
                case SmartActions.DisableEvade:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.disableEvade.disable);
                    break;
                }
                case SmartActions.LoadEquipment:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.loadEquipment.force);
                    break;
                }
                case SmartActions.SetHover:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.setHover.enable);
                    break;
                }
                case SmartActions.Evade:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.evade.toRespawnPosition);
                    break;
                }
                case SmartActions.SetHealthRegen:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.setHealthRegen.regenHealth);
                    break;
                }
                case SmartActions.CreateConversation:
                {
                    if (Global.ConversationDataStorage.GetConversationTemplate(e.Action.conversation.id) == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: SMART_ACTION_CREATE_CONVERSATION " +
                            $"Entry {e.EntryOrGuid} SourceType {e.GetScriptType()} " +
                            $"Event {e.EventId} Action {e.GetActionType()} " +
                            $"uses invalid entry {e.Action.conversation.id}, skipped.");
                        return false;
                    }

                    break;
                }
                case SmartActions.SetImmunePC:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.setImmunePC.immunePC);
                    break;
                }
                case SmartActions.SetImmuneNPC:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.setImmuneNPC.immuneNPC);
                    break;
                }
                case SmartActions.SetUninteractible:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.setUninteractible.uninteractible);
                    break;
                }
                case SmartActions.ActivateGameobject:
                {
                    if (!NotNULL(e, e.Action.activateGameObject.gameObjectAction))
                        return false;

                    if (e.Action.activateGameObject.gameObjectAction >= (int)GameObjectActions.Max)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} has gameObjectAction parameter out of range " +
                            $"(max allowed {(uint)GameObjectActions.Max - 1}, " +
                            $"current value {e.Action.activateGameObject.gameObjectAction}), skipped.");
                        return false;
                    }

                    break;
                }
                case SmartActions.StartClosestWaypoint:
                case SmartActions.Follow:
                case SmartActions.SetOrientation:
                case SmartActions.StoreTargetList:
                case SmartActions.CombatStop:
                case SmartActions.Die:
                case SmartActions.SetInCombatWithZone:
                case SmartActions.WpResume:
                case SmartActions.KillUnit:
                case SmartActions.SetInvincibilityHpLevel:
                case SmartActions.ResetGobject:
                case SmartActions.AttackStart:
                case SmartActions.ThreatAllPct:
                case SmartActions.ThreatSinglePct:
                case SmartActions.SetInstData64:
                case SmartActions.SetData:
                case SmartActions.AttackStop:
                case SmartActions.WpPause:
                case SmartActions.ForceDespawn:
                case SmartActions.Playmovie:
                case SmartActions.CloseGossip:
                case SmartActions.TriggerTimedEvent:
                case SmartActions.RemoveTimedEvent:
                case SmartActions.ActivateGobject:
                case SmartActions.CallScriptReset:
                case SmartActions.SetRangedMovement:
                case SmartActions.SetNpcFlag:
                case SmartActions.AddNpcFlag:
                case SmartActions.RemoveNpcFlag:
                case SmartActions.CallRandomTimedActionlist:
                case SmartActions.RandomMove:
                case SmartActions.SetUnitFieldBytes1:
                case SmartActions.RemoveUnitFieldBytes1:
                case SmartActions.JumpToPos:
                case SmartActions.SendGossipMenu:
                case SmartActions.GoSetLootState:
                case SmartActions.GoSetGoState:
                case SmartActions.SendTargetToTarget:
                case SmartActions.SetHomePos:
                case SmartActions.SummonCreatureGroup:
                case SmartActions.MoveOffset:
                case SmartActions.SetCorpseDelay:
                case SmartActions.AddThreat:
                case SmartActions.TriggerRandomTimedEvent:
                case SmartActions.SpawnSpawngroup:
                case SmartActions.AddToStoredTargetList:
                case SmartActions.DoAction:
                    break;
                case SmartActions.BecomePersonalCloneForPlayer:
                {
                    if (e.Action.becomePersonalClone.type < (int)TempSummonType.TimedOrDeadDespawn 
                        || e.Action.becomePersonalClone.type > (int)TempSummonType.ManualDespawn)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SmartAIMgr: {e} uses incorrect TempSummonType " +
                            $"{e.Action.becomePersonalClone.type}, skipped.");
                        return false;
                    }
                    break;
                }
                case SmartActions.TriggerGameEvent:
                {
                    TC_SAI_IS_BOOLEAN_VALID(e, e.Action.triggerGameEvent.useSaiTargetAsGameEventSource);
                    break;
                }
                // No longer supported
                case SmartActions.SetUnitFlag:
                case SmartActions.RemoveUnitFlag:
                case SmartActions.InstallAITemplate:
                case SmartActions.SetSwim:
                case SmartActions.AddAura:
                case SmartActions.OverrideScriptBaseObject:
                case SmartActions.ResetScriptBaseObject:
                case SmartActions.SendGoCustomAnim:
                case SmartActions.SetDynamicFlag:
                case SmartActions.AddDynamicFlag:
                case SmartActions.RemoveDynamicFlag:
                case SmartActions.SetGoFlag:
                case SmartActions.AddGoFlag:
                case SmartActions.RemoveGoFlag:
                case SmartActions.SetCanFly:
                case SmartActions.RemoveAurasByType:
                case SmartActions.SetSightDist:
                case SmartActions.Flee:
                case SmartActions.RemoveAllGameobjects:
                    Log.outError(LogFilter.Sql, $"SmartAIMgr: Unused action_type: {e} Skipped.");
                    return false;
                default:
                    Log.outError(LogFilter.ScriptsAi,
                        $"SmartAIMgr: Not handled action_type({e.GetActionType()}), " +
                        $"event_type({e.GetEventType()}), Entry {e.EntryOrGuid} " +
                        $"SourceType {e.GetScriptType()} Event {e.EventId}, skipped.");
                    return false;
            }

            if (!CheckUnusedActionParams(e))
                return false;

            return true;
        }

        static bool IsAnimKitValid(SmartScriptHolder e, int entry)
        {
            if (!CliDB.AnimKitStorage.ContainsKey(entry))
            {
                Log.outError(LogFilter.Sql, 
                    $"SmartAIMgr: {e} uses non-existent AnimKit entry {entry}, skipped.");
                return false;
            }
            return true;
        }
        static bool IsTextValid(SmartScriptHolder e, int id)
        {
            if (e.GetScriptType() != SmartScriptType.Creature)
                return true;

            int entry;
            if (e.GetEventType() == SmartEvents.TextOver)
            {
                entry = e.Event.textOver.creatureEntry;
            }
            else
            {
                switch (e.GetTargetType())
                {
                    case SmartTargets.CreatureDistance:
                    case SmartTargets.CreatureRange:
                    case SmartTargets.ClosestCreature:
                        return true; // ignore
                    default:
                        if (e.EntryOrGuid < 0)
                        {
                            long guid = -e.EntryOrGuid;
                            CreatureData data = Global.ObjectMgr.GetCreatureData(guid);
                            if (data == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"SmartAIMgr: {e} using non-existent Creature guid {guid}, skipped.");
                                return false;
                            }
                            else
                                entry = data.Id;
                        }
                        else
                            entry = e.EntryOrGuid;
                        break;
                }
            }

            if (entry == 0 || !Global.CreatureTextMgr.TextExist(entry, (byte)id))
            {
                Log.outError(LogFilter.Sql, 
                    $"SmartAIMgr: {e} using non-existent Text id {id}, skipped.");
                return false;
            }

            return true;
        }
        static bool IsCreatureValid(SmartScriptHolder e, int entry)
        {
            if (Global.ObjectMgr.GetCreatureTemplate(entry) == null)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses non-existent Creature entry {entry}, skipped.");
                return false;
            }
            return true;
        }
        static bool IsGameObjectValid(SmartScriptHolder e, int entry)
        {
            if (Global.ObjectMgr.GetGameObjectTemplate(entry) == null)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses non-existent GameObject entry {entry}, skipped.");
                return false;
            }
            return true;
        }
        static bool IsQuestValid(SmartScriptHolder e, int entry)
        {
            if (Global.ObjectMgr.GetQuestTemplate(entry) == null)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses non-existent Quest entry {entry}, skipped.");
                return false;
            }
            return true;
        }
        static bool IsSpellValid(SmartScriptHolder e, int entry)
        {
            if (!Global.SpellMgr.HasSpellInfo(entry, Difficulty.None))
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses non-existent Spell entry {entry}, skipped.");
                return false;
            }
            return true;
        }
        static bool IsMinMaxValid(SmartScriptHolder e, int min, int max)
        {
            if (max < min)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses min/max params wrong ({min}/{max}), skipped.");
                return false;
            }
            return true;
        }
        static bool IsMinMaxValid(SmartScriptHolder e, uint min, uint max)
        {
            if (max < min)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses min/max params wrong ({min}/{max}), skipped.");
                return false;
            }
            return true;
        }
        static bool NotNULL(SmartScriptHolder e, int data)
        {
            if (data == 0)
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} Parameter can not be NULL, skipped.");
                return false;
            }
            return true;
        }
        static bool IsEmoteValid(SmartScriptHolder e, int entry)
        {
            if (!CliDB.EmotesStorage.ContainsKey(entry))
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses non-existent Emote entry {entry}, skipped.");
                return false;
            }
            return true;
        }
        static bool IsItemValid(SmartScriptHolder e, int entry)
        {
            if (!CliDB.ItemSparseStorage.ContainsKey(entry))
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses non-existent Item entry {entry}, skipped.");
                return false;
            }
            return true;
        }
        static bool IsTextEmoteValid(SmartScriptHolder e, int entry)
        {
            if (!CliDB.EmotesTextStorage.ContainsKey(entry))
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses non-existent Text Emote entry {entry}, skipped.");
                return false;
            }
            return true;
        }
        static bool IsAreaTriggerValid(SmartScriptHolder e, int entry)
        {
            if (!CliDB.AreaTriggerStorage.ContainsKey(entry))
            {
                Log.outError(LogFilter.ScriptsAi, 
                    $"SmartAIMgr: {e} uses non-existent AreaTrigger entry {entry}, skipped.");
                return false;
            }
            return true;
        }
        static bool IsSoundValid(SmartScriptHolder e, int entry)
        {
            if (!CliDB.SoundKitStorage.ContainsKey(entry))
            {
                Log.outError(LogFilter.ScriptsAi,
                    $"SmartAIMgr: {e} uses non-existent Sound entry {entry}, skipped.");
                return false;
            }
            return true;
        }

        public List<SmartScriptHolder> GetScript(int entry, SmartScriptType type)
        {
            List<SmartScriptHolder> temp = new();
            if (_eventMap[(int)type].ContainsKey(entry))
            {
                foreach (var holder in _eventMap[(int)type][entry])
                    temp.Add(new SmartScriptHolder(holder));
            }
            else
            {
                if (entry > 0)//first search is for guid (negative), do not drop error if not found
                {
                    Log.outDebug(LogFilter.ScriptsAi,
                        $"SmartAIMgr.GetScript: Could not load Script " +
                        $"for Entry {entry} ScriptType {type}.");
                }
            }
            return temp;
        }

        public static SmartScriptHolder FindLinkedSourceEvent(IReadOnlyList<SmartScriptHolder> list, int eventId)
        {
            var sch = list.Find(p => p.Link == eventId);
            if (sch != null)
                return sch;

            return null;
        }

        public SmartScriptHolder FindLinkedEvent(IReadOnlyList<SmartScriptHolder> list, int link)
        {
            var sch = list.Find(p => p.EventId == link && p.GetEventType() == SmartEvents.Link);
            if (sch != null)
                return sch;

            return null;
        }

        public static uint GetTypeMask(SmartScriptType smartScriptType) =>
            smartScriptType switch
            {
                SmartScriptType.Creature => SmartScriptTypeMaskId.Creature,
                SmartScriptType.GameObject => SmartScriptTypeMaskId.Gameobject,
                SmartScriptType.AreaTrigger => SmartScriptTypeMaskId.Areatrigger,
                SmartScriptType.Event => SmartScriptTypeMaskId.Event,
                SmartScriptType.Gossip => SmartScriptTypeMaskId.Gossip,
                SmartScriptType.Quest => SmartScriptTypeMaskId.Quest,
                SmartScriptType.Spell => SmartScriptTypeMaskId.Spell,
                SmartScriptType.Transport => SmartScriptTypeMaskId.Transport,
                SmartScriptType.Instance => SmartScriptTypeMaskId.Instance,
                SmartScriptType.TimedActionlist => SmartScriptTypeMaskId.TimedActionlist,
                SmartScriptType.Scene => SmartScriptTypeMaskId.Scene,
                SmartScriptType.AreaTriggerEntity => SmartScriptTypeMaskId.AreatrigggerEntity,
                SmartScriptType.AreaTriggerEntityCustom => SmartScriptTypeMaskId.AreatrigggerEntity,
                _ => 0,
            };

        public static uint GetEventMask(SmartEvents smartEvent) =>
            smartEvent switch
            {
                SmartEvents.UpdateIc => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.TimedActionlist,
                SmartEvents.UpdateOoc => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject + SmartScriptTypeMaskId.Instance + SmartScriptTypeMaskId.AreatrigggerEntity,
                SmartEvents.HealthPct => SmartScriptTypeMaskId.Creature,
                SmartEvents.ManaPct => SmartScriptTypeMaskId.Creature,
                SmartEvents.Aggro => SmartScriptTypeMaskId.Creature,
                SmartEvents.Kill => SmartScriptTypeMaskId.Creature,
                SmartEvents.Death => SmartScriptTypeMaskId.Creature,
                SmartEvents.Evade => SmartScriptTypeMaskId.Creature,
                SmartEvents.SpellHit => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.Range => SmartScriptTypeMaskId.Creature,
                SmartEvents.OocLos => SmartScriptTypeMaskId.Creature,
                SmartEvents.Respawn => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.TargetHealthPct => SmartScriptTypeMaskId.Creature,
                SmartEvents.VictimCasting => SmartScriptTypeMaskId.Creature,
                SmartEvents.FriendlyHealth => SmartScriptTypeMaskId.Creature,
                SmartEvents.FriendlyIsCc => SmartScriptTypeMaskId.Creature,
                SmartEvents.FriendlyMissingBuff => SmartScriptTypeMaskId.Creature,
                SmartEvents.SummonedUnit => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.TargetManaPct => SmartScriptTypeMaskId.Creature,
                SmartEvents.AcceptedQuest => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.RewardQuest => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.ReachedHome => SmartScriptTypeMaskId.Creature,
                SmartEvents.ReceiveEmote => SmartScriptTypeMaskId.Creature,
                SmartEvents.HasAura => SmartScriptTypeMaskId.Creature,
                SmartEvents.TargetBuffed => SmartScriptTypeMaskId.Creature,
                SmartEvents.Reset => SmartScriptTypeMaskId.Creature,
                SmartEvents.IcLos => SmartScriptTypeMaskId.Creature,
                SmartEvents.PassengerBoarded => SmartScriptTypeMaskId.Creature,
                SmartEvents.PassengerRemoved => SmartScriptTypeMaskId.Creature,
                SmartEvents.Charmed => SmartScriptTypeMaskId.Creature,
                SmartEvents.CharmedTarget => SmartScriptTypeMaskId.Creature,
                SmartEvents.SpellHitTarget => SmartScriptTypeMaskId.Creature,
                SmartEvents.Damaged => SmartScriptTypeMaskId.Creature,
                SmartEvents.DamagedTarget => SmartScriptTypeMaskId.Creature,
                SmartEvents.Movementinform => SmartScriptTypeMaskId.Creature,
                SmartEvents.SummonDespawned => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.CorpseRemoved => SmartScriptTypeMaskId.Creature,
                SmartEvents.AiInit => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.DataSet => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.WaypointStart => SmartScriptTypeMaskId.Creature,
                SmartEvents.WaypointReached => SmartScriptTypeMaskId.Creature,
                SmartEvents.TransportAddplayer => SmartScriptTypeMaskId.Transport,
                SmartEvents.TransportAddcreature => SmartScriptTypeMaskId.Transport,
                SmartEvents.TransportRemovePlayer => SmartScriptTypeMaskId.Transport,
                SmartEvents.TransportRelocate => SmartScriptTypeMaskId.Transport,
                SmartEvents.InstancePlayerEnter => SmartScriptTypeMaskId.Instance,
                SmartEvents.AreatriggerOntrigger => SmartScriptTypeMaskId.Areatrigger + SmartScriptTypeMaskId.AreatrigggerEntity,
                SmartEvents.QuestAccepted => SmartScriptTypeMaskId.Quest,
                SmartEvents.QuestObjCompletion => SmartScriptTypeMaskId.Quest,
                SmartEvents.QuestRewarded => SmartScriptTypeMaskId.Quest,
                SmartEvents.QuestCompletion => SmartScriptTypeMaskId.Quest,
                SmartEvents.QuestFail => SmartScriptTypeMaskId.Quest,
                SmartEvents.TextOver => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.ReceiveHeal => SmartScriptTypeMaskId.Creature,
                SmartEvents.JustSummoned => SmartScriptTypeMaskId.Creature,
                SmartEvents.WaypointPaused => SmartScriptTypeMaskId.Creature,
                SmartEvents.WaypointResumed => SmartScriptTypeMaskId.Creature,
                SmartEvents.WaypointStopped => SmartScriptTypeMaskId.Creature,
                SmartEvents.WaypointEnded => SmartScriptTypeMaskId.Creature,
                SmartEvents.TimedEventTriggered => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.Update => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject + SmartScriptTypeMaskId.AreatrigggerEntity,
                SmartEvents.Link => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject + SmartScriptTypeMaskId.Areatrigger + SmartScriptTypeMaskId.Event + SmartScriptTypeMaskId.Gossip + SmartScriptTypeMaskId.Quest + SmartScriptTypeMaskId.Spell + SmartScriptTypeMaskId.Transport + SmartScriptTypeMaskId.Instance + SmartScriptTypeMaskId.AreatrigggerEntity,
                SmartEvents.GossipSelect => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.JustCreated => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.GossipHello => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.FollowCompleted => SmartScriptTypeMaskId.Creature,
                SmartEvents.PhaseChange => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.IsBehindTarget => SmartScriptTypeMaskId.Creature,
                SmartEvents.GameEventStart => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.GameEventEnd => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.GoLootStateChanged => SmartScriptTypeMaskId.Gameobject,
                SmartEvents.GoEventInform => SmartScriptTypeMaskId.Gameobject,
                SmartEvents.ActionDone => SmartScriptTypeMaskId.Creature,
                SmartEvents.OnSpellclick => SmartScriptTypeMaskId.Creature,
                SmartEvents.FriendlyHealthPCT => SmartScriptTypeMaskId.Creature,
                SmartEvents.DistanceCreature => SmartScriptTypeMaskId.Creature,
                SmartEvents.DistanceGameobject => SmartScriptTypeMaskId.Creature,
                SmartEvents.CounterSet => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.SceneStart => SmartScriptTypeMaskId.Scene,
                SmartEvents.SceneTrigger => SmartScriptTypeMaskId.Scene,
                SmartEvents.SceneCancel => SmartScriptTypeMaskId.Scene,
                SmartEvents.SceneComplete => SmartScriptTypeMaskId.Scene,
                SmartEvents.SummonedUnitDies => SmartScriptTypeMaskId.Creature + SmartScriptTypeMaskId.Gameobject,
                SmartEvents.OnSpellCast => SmartScriptTypeMaskId.Creature,
                SmartEvents.OnSpellFailed => SmartScriptTypeMaskId.Creature,
                SmartEvents.OnSpellStart => SmartScriptTypeMaskId.Creature,
                SmartEvents.OnDespawn => SmartScriptTypeMaskId.Creature,
                SmartEvents.SendEventTrigger => SmartScriptTypeMaskId.Event,
                _ => 0,
            };

        public static void TC_SAI_IS_BOOLEAN_VALID(SmartScriptHolder e, int value, [CallerArgumentExpression("value")] string valueName = null)
        {
            if (value > 1)
            {
                Log.outError(LogFilter.Sql, 
                    $"SmartAIMgr: {e} uses param {valueName} of Type Boolean with value {value}, " +
                    $"valid values are 0 or 1, skipped.");
            }
        }
    }

    public class SmartScriptHolder : IComparable<SmartScriptHolder>
    {
        public const int DefaultPriority = -1;

        public int EntryOrGuid;
        public SmartScriptType SourceType;
        public int EventId;
        public int Link;
        public List<Difficulty> Difficulties = new();

        public SmartEvent Event;
        public SmartAction Action;
        public SmartTarget Target;
        public TimeSpan Timer;
        public int Priority;
        public bool Active;
        public bool RunOnce;
        public bool EnableTimed;

        public SmartScriptHolder() { }
        public SmartScriptHolder(SmartScriptHolder other)
        {
            EntryOrGuid = other.EntryOrGuid;
            SourceType = other.SourceType;
            EventId = other.EventId;
            Link = other.Link;
            Event = other.Event;
            Action = other.Action;
            Target = other.Target;
            Timer = other.Timer;
            Active = other.Active;
            RunOnce = other.RunOnce;
            EnableTimed = other.EnableTimed;
        }

        public SmartScriptType GetScriptType() { return SourceType; }
        public SmartEvents GetEventType() { return Event.type; }
        public SmartActions GetActionType() { return Action.type; }
        public SmartTargets GetTargetType() { return Target.type; }

        public override string ToString()
        {
            return 
                $"Entry {EntryOrGuid} SourceType {GetScriptType()} " +
                $"Event {EventId} Action {GetActionType()}";
        }

        public int CompareTo(SmartScriptHolder other)
        {
            int result = Priority.CompareTo(other.Priority);
            if (result == 0)
                result = EntryOrGuid.CompareTo(other.EntryOrGuid);
            if (result == 0)
                result = SourceType.CompareTo(other.SourceType);
            if (result == 0)
                result = EventId.CompareTo(other.EventId);
            if (result == 0)
                result = Link.CompareTo(other.Link);

            return result;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct SmartEvent
    {
        [FieldOffset(0)]
        public SmartEvents type;

        [FieldOffset(4)]
        public uint event_phase_mask;

        [FieldOffset(8)]
        public int event_chance;

        [FieldOffset(12)]
        public SmartEventFlags event_flags;

        [FieldOffset(16)]
        public MinMaxRepeat minMaxRepeat;

        [FieldOffset(16)]
        public Kill kill;

        [FieldOffset(16)]
        public SpellHit spellHit;

        [FieldOffset(16)]
        public Los los;

        [FieldOffset(16)]
        public Respawn respawn;

        [FieldOffset(16)]
        public MinMax minMax;

        [FieldOffset(16)]
        public TargetCasting targetCasting;

        [FieldOffset(16)]
        public FriendlyCC friendlyCC;

        [FieldOffset(16)]
        public MissingBuff missingBuff;

        [FieldOffset(16)]
        public Summoned summoned;

        [FieldOffset(16)]
        public Quest quest;

        [FieldOffset(16)]
        public QuestObjective questObjective;

        [FieldOffset(16)]
        public Emote emote;

        [FieldOffset(16)]
        public Aura aura;

        [FieldOffset(16)]
        public Charm charm;

        [FieldOffset(16)]
        public MovementInform movementInform;

        [FieldOffset(16)]
        public DataSet dataSet;

        [FieldOffset(16)]
        public Waypoint waypoint;

        [FieldOffset(16)]
        public TransportAddCreature transportAddCreature;

        [FieldOffset(16)]
        public TransportRelocate transportRelocate;

        [FieldOffset(16)]
        public InstancePlayerEnter instancePlayerEnter;

        [FieldOffset(16)]
        public Areatrigger areatrigger;

        [FieldOffset(16)]
        public TextOver textOver;

        [FieldOffset(16)]
        public TimedEvent timedEvent;

        [FieldOffset(16)]
        public GossipHello gossipHello;

        [FieldOffset(16)]
        public Gossip gossip;

        [FieldOffset(16)]
        public GameEvent gameEvent;

        [FieldOffset(16)]
        public GoLootStateChanged goLootStateChanged;

        [FieldOffset(16)]
        public EventInform eventInform;

        [FieldOffset(16)]
        public DoAction doAction;

        [FieldOffset(16)]
        public FriendlyHealthPct friendlyHealthPct;

        [FieldOffset(16)]
        public Distance distance;

        [FieldOffset(16)]
        public Counter counter;

        [FieldOffset(16)]
        public SpellCast spellCast;

        [FieldOffset(16)]
        public Spell spell;

        [FieldOffset(16)]
        public Raw raw;

        [FieldOffset(40)]
        public string param_string;

        #region Structs
        public struct MinMaxRepeat
        {
            public int min;
            public int max;
            public Milliseconds repeatMin;
            public Milliseconds repeatMax;
        }

        public struct Kill
        {
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
            public int playerOnly;
            public int creature;
        }

        public struct SpellHit
        {
            public int spell;
            public int school;
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
        }

        public struct Los
        {
            public int hostilityMode;
            public int maxDist;
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
            public int playerOnly;
        }

        public struct Respawn
        {
            public int type;
            public int map;
            public int area;
        }

        public struct MinMax
        {
            public Milliseconds repeatMin;
            public Milliseconds repeatMax;
        }

        public struct TargetCasting
        {
            public Milliseconds repeatMin;
            public Milliseconds repeatMax;
            public int spellId;
        }

        public struct FriendlyCC
        {
            public int radius;
            public Milliseconds repeatMin;
            public Milliseconds repeatMax;
        }

        public struct MissingBuff
        {
            public int spell;
            public int radius;
            public Milliseconds repeatMin;
            public Milliseconds repeatMax;
        }

        public struct Summoned
        {
            public int creature;
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
        }

        public struct Quest
        {
            public int questId;
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
        }

        public struct QuestObjective
        {
            public int id;
        }

        public struct Emote
        {
            public int emoteId;
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
        }

        public struct Aura
        {
            public int spell;
            public int count;
            public Milliseconds repeatMin;
            public Milliseconds repeatMax;
        }

        public struct Charm
        {
            public int onRemove;
        }

        public struct MovementInform
        {
            public int type;
            public int id;
        }

        public struct DataSet
        {
            public int id;
            public int value;
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
        }

        public struct Waypoint
        {
            public int pointID;
            public int pathID;
        }

        public struct TransportAddCreature
        {
            public int creature;
        }

        public struct TransportRelocate
        {
            public int pointID;
        }

        public struct InstancePlayerEnter
        {
            public int team;
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
        }

        public struct Areatrigger
        {
            public int id;
        }

        public struct TextOver
        {
            public int textGroupID;
            public int creatureEntry;
        }

        public struct TimedEvent
        {
            public int id;
        }

        public struct GossipHello
        {
            public int filter;
        }

        public struct Gossip
        {
            public int sender;
            public int action;
        }

        public struct GameEvent
        {
            public int gameEventId;
        }

        public struct GoLootStateChanged
        {
            public int lootState;
        }

        public struct EventInform
        {
            public int eventId;
        }

        public struct DoAction
        {
            public int eventId;
        }

        public struct FriendlyHealthPct
        {
            public int minHpPct;
            public int maxHpPct;
            public Milliseconds repeatMin;
            public Milliseconds repeatMax;
            public int radius;
        }

        public struct Distance
        {
            public int guid;
            public int entry;
            public int dist;
            public Milliseconds repeat;
        }

        public struct Counter
        {
            public int id;
            public int value;
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
        }

        public struct SpellCast
        {
            public int spell;
            public Milliseconds cooldownMin;
            public Milliseconds cooldownMax;
        }

        public struct Spell
        {
            public int effIndex;
        }

        public struct Raw
        {
            public int param1;
            public int param2;
            public int param3;
            public int param4;
            public int param5;
        }
        #endregion
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct SmartAction
    {
        [FieldOffset(0)]
        public SmartActions type;

        [FieldOffset(4)]
        public Talk talk;

        [FieldOffset(4)]
        public SimpleTalk simpleTalk;

        [FieldOffset(4)]
        public Faction faction;

        [FieldOffset(4)]
        public MorphOrMount morphOrMount;

        [FieldOffset(4)]
        public Sound sound;

        [FieldOffset(4)]
        public Emote emote;

        [FieldOffset(4)]
        public Quest quest;

        [FieldOffset(4)]
        public QuestOffer questOffer;

        [FieldOffset(4)]
        public React react;

        [FieldOffset(4)]
        public RandomEmote randomEmote;

        [FieldOffset(4)]
        public Cast cast;

        [FieldOffset(4)]
        public CrossCast crossCast;

        [FieldOffset(4)]
        public SummonCreature summonCreature;

        [FieldOffset(4)]
        public ThreatPCT threatPCT;

        [FieldOffset(4)]
        public Threat threat;

        [FieldOffset(4)]
        public CastCreatureOrGO castCreatureOrGO;

        [FieldOffset(4)]
        public AutoAttack autoAttack;

        [FieldOffset(4)]
        public CombatMove combatMove;

        [FieldOffset(4)]
        public SetEventPhase setEventPhase;

        [FieldOffset(4)]
        public IncEventPhase incEventPhase;

        [FieldOffset(4)]
        public CastedCreatureOrGO castedCreatureOrGO;

        [FieldOffset(4)]
        public RemoveAura removeAura;

        [FieldOffset(4)]
        public Follow follow;

        [FieldOffset(4)]
        public RandomPhase randomPhase;

        [FieldOffset(4)]
        public RandomPhaseRange randomPhaseRange;

        [FieldOffset(4)]
        public KilledMonster killedMonster;

        [FieldOffset(4)]
        public SetInstanceData setInstanceData;

        [FieldOffset(4)]
        public SetInstanceData64 setInstanceData64;

        [FieldOffset(4)]
        public UpdateTemplate updateTemplate;

        [FieldOffset(4)]
        public CallHelp callHelp;

        [FieldOffset(4)]
        public SetSheath setSheath;

        [FieldOffset(4)]
        public ForceDespawn forceDespawn;

        [FieldOffset(4)]
        public InvincHP invincHP;

        [FieldOffset(4)]
        public IngamePhaseId ingamePhaseId;

        [FieldOffset(4)]
        public IngamePhaseGroup ingamePhaseGroup;

        [FieldOffset(4)]
        public SetData setData;

        [FieldOffset(4)]
        public MoveRandom moveRandom;

        [FieldOffset(4)]
        public Visibility visibility;

        [FieldOffset(4)]
        public SummonGO summonGO;

        [FieldOffset(4)]
        public Active active;

        [FieldOffset(4)]
        public Taxi taxi;

        [FieldOffset(4)]
        public WpStart wpStart;

        [FieldOffset(4)]
        public WpPause wpPause;

        [FieldOffset(4)]
        public WpStop wpStop;

        [FieldOffset(4)]
        public Item item;

        [FieldOffset(4)]
        public SetRun setRun;

        [FieldOffset(4)]
        public SetDisableGravity setDisableGravity;

        [FieldOffset(4)]
        public Teleport teleport;

        [FieldOffset(4)]
        public SetCounter setCounter;

        [FieldOffset(4)]
        public StoreTargets storeTargets;

        [FieldOffset(4)]
        public TimeEvent timeEvent;

        [FieldOffset(4)]
        public Movie movie;

        [FieldOffset(4)]
        public Equip equip;

        [FieldOffset(4)]
        public Flag flag;

        [FieldOffset(4)]
        public SetunitByte setunitByte;

        [FieldOffset(4)]
        public DelunitByte delunitByte;

        [FieldOffset(4)]
        public TimedActionList timedActionList;

        [FieldOffset(4)]
        public RandTimedActionList randTimedActionList;

        [FieldOffset(4)]
        public RandRangeTimedActionList randRangeTimedActionList;

        [FieldOffset(4)]
        public InterruptSpellCasting interruptSpellCasting;

        [FieldOffset(4)]
        public Jump jump;

        [FieldOffset(4)]
        public FleeAssist fleeAssist;

        [FieldOffset(4)]
        public EnableTempGO enableTempGO;

        [FieldOffset(4)]
        public MoveToPos moveToPos;

        [FieldOffset(4)]
        public SendGossipMenu sendGossipMenu;

        [FieldOffset(4)]
        public SetGoLootState setGoLootState;

        [FieldOffset(4)]
        public SendTargetToTarget sendTargetToTarget;

        [FieldOffset(4)]
        public SetRangedMovement setRangedMovement;

        [FieldOffset(4)]
        public SetHealthRegen setHealthRegen;

        [FieldOffset(4)]
        public SetRoot setRoot;

        [FieldOffset(4)]
        public GoState goState;

        [FieldOffset(4)]
        public CreatureGroup creatureGroup;

        [FieldOffset(4)]
        public Power power;

        [FieldOffset(4)]
        public GameEventStop gameEventStop;

        [FieldOffset(4)]
        public GameEventStart gameEventStart;

        [FieldOffset(4)]
        public ClosestWaypointFromList closestWaypointFromList;

        [FieldOffset(4)]
        public MoveOffset moveOffset;

        [FieldOffset(4)]
        public RandomSound randomSound;

        [FieldOffset(4)]
        public CorpseDelay corpseDelay;

        [FieldOffset(4)]
        public DisableEvade disableEvade;

        [FieldOffset(4)]
        public GroupSpawn groupSpawn;

        [FieldOffset(4)]
        public AuraType auraType;

        [FieldOffset(4)]
        public LoadEquipment loadEquipment;

        [FieldOffset(4)]
        public RandomTimedEvent randomTimedEvent;

        [FieldOffset(4)]
        public PauseMovement pauseMovement;

        [FieldOffset(4)]
        public RespawnData respawnData;

        [FieldOffset(4)]
        public AnimKit animKit;

        [FieldOffset(4)]
        public Scene scene;

        [FieldOffset(4)]
        public Cinematic cinematic;

        [FieldOffset(4)]
        public MovementSpeed movementSpeed;

        [FieldOffset(4)]
        public SpellVisualKit spellVisualKit;

        [FieldOffset(4)]
        public OverrideLight overrideLight;

        [FieldOffset(4)]
        public OverrideWeather overrideWeather;

        [FieldOffset(4)]
        public SetHover setHover;

        [FieldOffset(4)]
        public Evade evade;

        [FieldOffset(4)]
        public SetHealthPct setHealthPct;

        [FieldOffset(4)]
        public Conversation conversation;

        [FieldOffset(4)]
        public SetImmunePC setImmunePC;

        [FieldOffset(4)]
        public SetImmuneNPC setImmuneNPC;

        [FieldOffset(4)]
        public SetUninteractible setUninteractible;

        [FieldOffset(4)]
        public ActivateGameObject activateGameObject;

        [FieldOffset(4)]
        public AddToStoredTargets addToStoredTargets;

        [FieldOffset(4)]
        public BecomePersonalClone becomePersonalClone;

        [FieldOffset(4)]
        public TriggerGameEvent triggerGameEvent;

        [FieldOffset(4)]
        public DoAction doAction;

        [FieldOffset(4)]
        public Raw raw;

        #region Stucts
        public struct Talk
        {
            public int textGroupId;
            public Milliseconds duration;
            public int useTalkTarget;
        }

        public struct SimpleTalk
        {
            public int textGroupId;
            public Milliseconds duration;
        }

        public struct Faction
        {
            public int factionId;
        }

        public struct MorphOrMount
        {
            public int creature;
            public int model;
        }

        public struct Sound
        {
            public int soundId;
            public int onlySelf;
            public int distance;
            public int keyBroadcastTextId;
        }

        public struct Emote
        {
            public int emoteId;
        }

        public struct Quest
        {
            public int questId;
        }

        public struct QuestOffer
        {
            public int questId;
            public int directAdd;
        }

        public struct React
        {
            public int state;
        }

        public struct RandomEmote
        {
            public int emote1;
            public int emote2;
            public int emote3;
            public int emote4;
            public int emote5;
            public int emote6;
        }

        public struct Cast
        {
            public int spell;
            public int castFlags;
            public int triggerFlags;
            public int targetsLimit;
        }

        public struct CrossCast
        {
            public int spell;
            public int castFlags;
            public int targetType;
            public int targetParam1;
            public int targetParam2;
            public int targetParam3;
        }

        public struct SummonCreature
        {
            public int creature;
            public int type;
            public Milliseconds duration;
            public int storageID;
            public int attackInvoker;
            public int flags; // SmartActionSummonCreatureFlags
            public int count;
        }

        public struct ThreatPCT
        {
            public int threatINC;
            public int threatDEC;
        }

        public struct CastCreatureOrGO
        {
            public int quest;
            public int spell;
        }

        public struct Threat
        {
            public int threatINC;
            public int threatDEC;
        }

        public struct AutoAttack
        {
            public int attack;
        }

        public struct CombatMove
        {
            public int move;
        }

        public struct SetEventPhase
        {
            public int phase;
        }

        public struct IncEventPhase
        {
            public int inc;
            public int dec;
        }

        public struct CastedCreatureOrGO
        {
            public int creature;
            public int spell;
        }

        public struct RemoveAura
        {
            public int spell;
            public int charges;
            public int onlyOwnedAuras;
        }

        public struct Follow
        {
            public int dist;
            public int angle;
            public int entry;
            public int credit;
            public int creditType;
        }

        public struct RandomPhase
        {
            public int phase1;
            public int phase2;
            public int phase3;
            public int phase4;
            public int phase5;
            public int phase6;
        }

        public struct RandomPhaseRange
        {
            public int phaseMin;
            public int phaseMax;
        }

        public struct KilledMonster
        {
            public int creature;
        }

        public struct SetInstanceData
        {
            public int field;
            public int data;
            public int type;
        }

        public struct SetInstanceData64
        {
            public int field;
        }

        public struct UpdateTemplate
        {
            public int creature;
            public int updateLevel;
        }

        public struct CallHelp
        {
            public int range;
            public int withEmote;
        }

        public struct SetSheath
        {
            public int sheath;
        }

        public struct ForceDespawn
        {
            public Milliseconds delay;
            public Seconds forceRespawnTimer;
        }

        public struct InvincHP
        {
            public int minHP;
            public int percent;
        }

        public struct IngamePhaseId
        {
            public int id;
            public int apply;
        }

        public struct IngamePhaseGroup
        {
            public int groupId;
            public int apply;
        }

        public struct SetData
        {
            public int field;
            public int data;
        }

        public struct MoveRandom
        {
            public int distance;
        }

        public struct Visibility
        {
            public int state;
        }

        public struct SummonGO
        {
            public int entry;
            public Seconds despawnTime;
            public int summonType;
        }

        public struct Active
        {
            public int state;
        }

        public struct Taxi
        {
            public int id;
        }

        public struct WpStart
        {
            public int run;
            public int pathID;
            public int repeat;
            public int quest;
            public Milliseconds despawnTime;
            //public int reactState; DO NOT REUSE
        }

        public struct WpPause
        {
            public Milliseconds delay;
        }

        public struct WpStop
        {
            public Milliseconds despawnTime;
            public int quest;
            public int fail;
        }

        public struct Item
        {
            public int entry;
            public int count;
        }

        public struct SetRun
        {
            public int run;
        }

        public struct SetDisableGravity
        {
            public int disable;
        }

        public struct Teleport
        {
            public int mapID;
        }

        public struct SetCounter
        {
            public int counterId;
            public int value;
            public int reset;
        }

        public struct StoreTargets
        {
            public int id;
        }

        public struct TimeEvent
        {
            public int id;
            public int min;
            public int max;
            public Milliseconds repeatMin;
            public Milliseconds repeatMax;
            public int chance;
        }

        public struct Movie
        {
            public int entry;
        }

        public struct Equip
        {
            public int entry;
            public uint mask;
            public int slot1;
            public int slot2;
            public int slot3;
        }

        public struct Flag
        {
            public uint flag;
        }

        public struct SetunitByte
        {
            public int byte1;
            public int type;
        }

        public struct DelunitByte
        {
            public int byte1;
            public int type;
        }

        public struct TimedActionList
        {
            public int id;
            public int timerType;
            public int allowOverride;
        }

        public struct RandTimedActionList
        {
            public int actionList1;
            public int actionList2;
            public int actionList3;
            public int actionList4;
            public int actionList5;
            public int actionList6;
        }

        public struct RandRangeTimedActionList
        {
            public int idMin;
            public int idMax;
        }

        public struct InterruptSpellCasting
        {
            public int withDelayed;
            public int spell_id;
            public int withInstant;
        }

        public struct Jump
        {
            public Speed SpeedXY;
            public Speed SpeedZ;
            public int Gravity;
            public int UseDefaultGravity;
            public int PointId;
            public int ContactDistance;
        }

        public struct FleeAssist
        {
            public int withEmote;
        }

        public struct EnableTempGO
        {
            public Milliseconds duration;
        }

        public struct MoveToPos
        {
            public int pointId;
            public int transport;
            public int disablePathfinding;
            public int contactDistance;
        }

        public struct SendGossipMenu
        {
            public int gossipMenuId;
            public int gossipNpcTextId;
        }

        public struct SetGoLootState
        {
            public int state;
        }

        public struct SendTargetToTarget
        {
            public int id;
        }

        public struct SetRangedMovement
        {
            public int distance;
            public int angle;
        }

        public struct SetHealthRegen
        {
            public int regenHealth;
        }

        public struct SetRoot
        {
            public int root;
        }

        public struct GoState
        {
            public int state;
        }

        public struct CreatureGroup
        {
            public int group;
            public int attackInvoker;
        }

        public struct Power
        {
            public int powerType;
            public int newPower;
        }

        public struct GameEventStop
        {
            public int id;
        }

        public struct GameEventStart
        {
            public int id;
        }

        public struct ClosestWaypointFromList
        {
            public int wp1;
            public int wp2;
            public int wp3;
            public int wp4;
            public int wp5;
            public int wp6;
        }

        public struct MoveOffset
        {
            public int PointId;
        }

        public struct RandomSound
        {
            public int sound1;
            public int sound2;
            public int sound3;
            public int sound4;
            public int onlySelf;
            public int distance;
        }

        public struct CorpseDelay
        {
            public Seconds timer;
            public int includeDecayRatio;
        }

        public struct DisableEvade
        {
            public int disable;
        }

        public struct GroupSpawn
        {
            public int groupId;
            public Milliseconds minDelay;
            public Milliseconds maxDelay;
            public uint spawnflags;
        }

        public struct LoadEquipment
        {
            public int id;
            public int force;
        }

        public struct RandomTimedEvent
        {
            public int minId;
            public int maxId;
        }

        public struct PauseMovement
        {
            public int movementSlot;
            public Milliseconds pauseTimer;
            public int force;
        }

        public struct RespawnData
        {
            public int spawnType;
            public int spawnId;
        }

        public struct AnimKit
        {
            public int animKit;
            public int type;
        }

        public struct Scene
        {
            public int sceneId;
        }

        public struct Cinematic
        {
            public int entry;
        }

        public struct MovementSpeed
        {
            public int movementType;
            public int speedInteger;
            public int speedFraction;
        }

        public struct SpellVisualKit
        {
            public int spellVisualKitId;
            public int kitType;
            public uint duration;
        }

        public struct OverrideLight
        {
            public int zoneId;
            public int areaLightId;
            public int overrideLightId;
            public Milliseconds transition;
        }

        public struct OverrideWeather
        {
            public int zoneId;
            public int weatherId;
            public int intensity;
        }

        public struct SetHover
        {
            public int enable;
        }

        public struct Evade
        {
            public int toRespawnPosition;
        }

        public struct SetHealthPct
        {
            public int percent;
        }

        public struct Conversation
        {
            public int id;
        }

        public struct SetImmunePC
        {
            public int immunePC;
        }

        public struct SetImmuneNPC
        {
            public int immuneNPC;
        }

        public struct SetUninteractible
        {
            public int uninteractible;
        }

        public struct ActivateGameObject
        {
            public int gameObjectAction;
            public int param;
        }

        public struct AddToStoredTargets
        {
            public int id;
        }

        public struct BecomePersonalClone
        {
            public int type;
            public Milliseconds duration;
        }

        public struct TriggerGameEvent
        {
            public int eventId;
            public int useSaiTargetAsGameEventSource;
        }

        public struct DoAction
        {
            public int actionId;
        }

        public struct Raw
        {
            public int param1;
            public int param2;
            public int param3;
            public int param4;
            public int param5;
            public int param6;
            public int param7;
        }
        #endregion
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct SmartTarget
    {
        [FieldOffset(0)]
        public SmartTargets type;

        [FieldOffset(4)]
        public float x;

        [FieldOffset(8)]
        public float y;

        [FieldOffset(12)]
        public float z;

        [FieldOffset(16)]
        public float o;

        [FieldOffset(20)]
        public HostilRandom hostilRandom;

        [FieldOffset(20)]
        public Farthest farthest;

        [FieldOffset(20)]
        public UnitRange unitRange;

        [FieldOffset(20)]
        public UnitGUID unitGUID;

        [FieldOffset(20)]
        public UnitDistance unitDistance;

        [FieldOffset(20)]
        public PlayerDistance playerDistance;

        [FieldOffset(20)]
        public PlayerRange playerRange;

        [FieldOffset(20)]
        public Stored stored;

        [FieldOffset(20)]
        public GoRange goRange;

        [FieldOffset(20)]
        public GoGUID goGUID;

        [FieldOffset(20)]
        public GoDistance goDistance;

        [FieldOffset(20)]
        public UnitClosest unitClosest;

        [FieldOffset(20)]
        public GoClosest goClosest;

        [FieldOffset(20)]
        public ClosestAttackable closestAttackable;

        [FieldOffset(20)]
        public ClosestFriendly closestFriendly;

        [FieldOffset(20)]
        public Owner owner;

        [FieldOffset(20)]
        public Vehicle vehicle;

        [FieldOffset(20)]
        public ThreatList threatList;

        [FieldOffset(20)]
        public Raw raw;

        #region Structs
        public struct HostilRandom
        {
            public int maxDist;
            public int playerOnly;
            public int powerType;
        }

        public struct Farthest
        {
            public int maxDist;
            public int playerOnly;
            public int isInLos;
        }

        public struct UnitRange
        {
            public int creature;
            public int minDist;
            public int maxDist;
            public int maxSize;
        }

        public struct UnitGUID
        {
            public int dbGuid;
            public int entry;
        }

        public struct UnitDistance
        {
            public int creature;
            public int dist;
            public int maxSize;
        }

        public struct PlayerDistance
        {
            public int dist;
        }

        public struct PlayerRange
        {
            public int minDist;
            public int maxDist;
        }

        public struct Stored
        {
            public int id;
        }

        public struct GoRange
        {
            public int entry;
            public int minDist;
            public int maxDist;
            public int maxSize;
        }

        public struct GoGUID
        {
            public int dbGuid;
            public int entry;
        }

        public struct GoDistance
        {
            public int entry;
            public int dist;
            public int maxSize;
        }

        public struct UnitClosest
        {
            public int entry;
            public int dist;
            public int dead;
        }

        public struct GoClosest
        {
            public int entry;
            public int dist;
        }

        public struct ClosestAttackable
        {
            public int maxDist;
            public int playerOnly;
        }

        public struct ClosestFriendly
        {
            public int maxDist;
            public int playerOnly;
        }

        public struct Owner
        {
            public int useCharmerOrOwner;
        }

        public struct Vehicle
        {
            public uint seatMask;
        }

        public struct ThreatList
        {
            public int maxDist;
        }

        public struct Raw
        {
            public int param1;
            public int param2;
            public int param3;
            public int param4;
        }
        #endregion
    }
}
