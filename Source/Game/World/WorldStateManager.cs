// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Database;
using Game.DataStorage;
using Game.Maps;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace Game
{
    public class WorldStateManager : Singleton<WorldStateManager>
    {
        static int AnyMap = -1;

        private Dictionary<int/*worldStateId*/, WorldStateTemplate> _worldStateTemplates = new();
        private Dictionary<int/*worldStateId*/, WorldStateValue> _realmWorldStateValues = new();
        private Dictionary<int/*map*/, Dictionary<int/*worldStateId*/, WorldStateValue>> _worldStatesByMap = new();

        WorldStateManager() { }

        public void LoadFromDB()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            {
                //                                         0   1             2       3        4
                SQLResult result = DB.World.Query("SELECT ID, DefaultValue, MapIDs, AreaIDs, ScriptName FROM world_state");
                if (result.IsEmpty())
                    return;

                do
                {
                    int id = result.Read<int>(0);
                    int defaultValue = result.Read<int>(1);

                    string mapIds = result.Read<string>(2);
                    List<int> mapIdsList = new List<int>();
                    if (!mapIds.IsEmpty())
                    {
                        foreach (string mapIdToken in new StringArray(mapIds, ','))
                        {
                            if (!int.TryParse(mapIdToken, out int mapId))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `world_state` contains a world state {id} " +
                                    $"with non-integer MapID ({mapIdToken}), map ignored");
                                continue;
                            }

                            if (mapId != AnyMap && !CliDB.MapStorage.ContainsKey(mapId))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `world_state` contains a world state {id} " +
                                    $"with invalid MapID ({mapId}), map ignored");
                                continue;
                            }

                            mapIdsList.Add(mapId);
                        }
                    }

                    if (!mapIds.IsEmpty() && mapIdsList.Empty())
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Table `world_state` contains a world state {id} " +
                            $"with nonempty MapIDs ({mapIds}) but no valid map id was found, ignored");
                        continue;
                    }

                    string areaIds = result.Read<string>(3);
                    List<int> areaIdsList = new List<int>();
                    if (!areaIds.IsEmpty() && !mapIdsList.Empty())
                    {
                        foreach (string areaIdToken in new StringArray(areaIds, ','))
                        {
                            if (!int.TryParse(areaIdToken, out int areaId))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `world_state` contains a world state {id} " +
                                    $"with non-integer AreaID ({areaIdToken}), area ignored");
                                continue;
                            }

                            var areaTableEntry = CliDB.AreaTableStorage.LookupByKey(areaId);
                            if (areaTableEntry == null)
                            {
                                Log.outError(LogFilter.Sql,
                                    $"Table `world_state` contains a world state {id} " +
                                    $"with invalid AreaID ({areaId}), area ignored");
                                continue;
                            }

                            if (!mapIdsList.Contains(areaTableEntry.ContinentID))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `world_state` contains a world state {id} " +
                                    $"with AreaID ({areaId}) not on any of required maps, area ignored");
                                continue;
                            }

                            areaIdsList.Add(areaId);
                        }

                        if (!areaIds.IsEmpty() && areaIdsList.Empty())
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Table `world_state` contains a world state {id} " +
                                $"with nonempty AreaIDs ({areaIds}) but no valid area id was found, ignored");
                            continue;
                        }
                    }
                    else if (!areaIds.IsEmpty())
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Table `world_state` contains a world state {id} with nonempty AreaIDs ({areaIds}) " +
                            $"but is a realm wide world state, area requirement ignored");
                    }

                    int scriptId = Global.ObjectMgr.GetScriptId(result.Read<string>(4));

                    if (!mapIdsList.Empty())
                    {
                        foreach (int mapId in mapIdsList)
                        {
                            if (!_worldStatesByMap.ContainsKey(mapId))
                                _worldStatesByMap[mapId] = new();

                            _worldStatesByMap[mapId][id] = defaultValue;
                        }
                    }
                    else
                        _realmWorldStateValues[id] = defaultValue;

                    _worldStateTemplates[id] = new WorldStateTemplate(id, defaultValue, scriptId, mapIdsList, areaIdsList);

                } while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {_worldStateTemplates.Count} world state templates {Time.Diff(oldMSTime)} ms.");
            }

            oldMSTime = Time.NowRelative;

            {
                SQLResult result = DB.Characters.Query("SELECT Id, Value FROM world_state_value");
                uint savedValueCount = 0;
                if (!result.IsEmpty())
                {
                    do
                    {
                        int worldStateId = result.Read<int>(0);
                        WorldStateTemplate worldState = _worldStateTemplates.LookupByKey(worldStateId);
                        if (worldState == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Table `world_state_value` contains a value " +
                                $"for unknown world state {worldStateId}, ignored");
                            continue;
                        }

                        int value = result.Read<int>(1);

                        if (!worldState.MapIds.Empty())
                        {
                            foreach (int mapId in worldState.MapIds)
                            {
                                if (!_worldStatesByMap.ContainsKey(mapId))
                                    _worldStatesByMap[mapId] = new();

                                _worldStatesByMap[mapId][worldStateId] = value;
                            }
                        }
                        else
                            _realmWorldStateValues[worldStateId] = value;

                        ++savedValueCount;
                    }
                    while (result.NextRow());
                }

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {savedValueCount} saved world state values {Time.Diff(oldMSTime)} ms.");
            }
        }

        public WorldStateTemplate GetWorldStateTemplate(int worldStateId)
        {
            return _worldStateTemplates.LookupByKey(worldStateId);
        }

        public WorldStateValue GetValue(int worldStateId, Map map)
        {
            WorldStateTemplate worldStateTemplate = GetWorldStateTemplate(worldStateId);
            if (worldStateTemplate == null || worldStateTemplate.MapIds.Empty())
                return _realmWorldStateValues.LookupByKey(worldStateId);

            if (map == null || (!worldStateTemplate.MapIds.Contains(map.GetId()) && !worldStateTemplate.MapIds.Contains(AnyMap)))
                return 0;

            return map.GetWorldStateValue(worldStateId);
        }

        /// <summary cref="Framework.Constants.WorldStates">WorldStates worldStateId</summary>
        public void SetValue(int worldStateId, WorldStateValue value, bool hidden, Map map)
        {
            WorldStateTemplate worldStateTemplate = GetWorldStateTemplate(worldStateId);
            if (worldStateTemplate == null || worldStateTemplate.MapIds.Empty())
            {
                WorldStateValue oldValue = default;
                if (!_realmWorldStateValues.TryAdd(worldStateId, value))
                {
                    oldValue = _realmWorldStateValues[worldStateId];
                    if (oldValue == value)
                        return;
                }

                if (worldStateTemplate != null)
                    Global.ScriptMgr.OnWorldStateValueChange(worldStateTemplate, oldValue, value, null);

                // Broadcast update to all players on the server
                UpdateWorldState updateWorldState = new();
                updateWorldState.VariableID = worldStateId;
                updateWorldState.Value = value;
                updateWorldState.Hidden = hidden;
                Global.WorldMgr.SendGlobalMessage(updateWorldState);
                return;
            }

            if (map == null || (!worldStateTemplate.MapIds.Contains(map.GetId()) && !worldStateTemplate.MapIds.Contains(AnyMap)))
                return;

            map.SetWorldStateValue(worldStateId, value, hidden);
        }

        public void SaveValueInDb(int worldStateId, WorldStateValue value)
        {
            if (GetWorldStateTemplate(worldStateId) == null)
                return;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_WORLD_STATE);
            stmt.SetInt32(0, worldStateId);
            stmt.SetInt32(1, value);
            DB.Characters.Execute(stmt);
        }

        public void SetValueAndSaveInDb(int worldStateId, WorldStateValue value, bool hidden, Map map)
        {
            SetValue(worldStateId, value, hidden, map);
            SaveValueInDb(worldStateId, value);
        }

        public Dictionary<int, WorldStateValue> GetInitialWorldStatesForMap(Map map)
        {
            Dictionary<int, WorldStateValue> initialValues = new();

            if (_worldStatesByMap.TryGetValue(map.GetId(), out var valuesTemplate))
            {
                foreach (var (key, value) in valuesTemplate)
                    initialValues.Add(key, value);
            }

            if (_worldStatesByMap.TryGetValue(AnyMap, out valuesTemplate))
            {
                foreach (var (key, value) in valuesTemplate)
                    initialValues.Add(key, value);
            }

            return initialValues;
        }

        public void FillInitialWorldStates(InitWorldStates initWorldStates, Map map, int playerAreaId)
        {
            foreach (var (worldStateId, value) in _realmWorldStateValues)
                initWorldStates.AddState(worldStateId, value);

            foreach (var (worldStateId, value) in map.GetWorldStateValues())
            {
                WorldStateTemplate worldStateTemplate = GetWorldStateTemplate(worldStateId);
                if (worldStateTemplate != null && !worldStateTemplate.AreaIds.Empty())
                {
                    bool isInAllowedArea = worldStateTemplate.AreaIds.Any(requiredAreaId => Global.DB2Mgr.IsInArea(playerAreaId, requiredAreaId));
                    if (!isInAllowedArea)
                        continue;
                }

                initWorldStates.AddState(worldStateId, value);
            }
        }
    }

    public class WorldStateTemplate
    {
        public readonly int Id;
        public readonly int DefaultValue;
        public readonly int ScriptId;

        public List<int> MapIds;
        public List<int> AreaIds;

        public WorldStateTemplate(int stateId, int defaultValue, int scriptId, List<int> mapIds, List<int> areaIds)
        {
            Id = stateId;
            DefaultValue = defaultValue;
            ScriptId = scriptId;
            MapIds = mapIds;
            AreaIds = areaIds;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public readonly struct WorldStateValue
    {
        [FieldOffset(0)]
        private readonly int _Raw;
        [FieldOffset(0)]
        public readonly int Int32;
        [FieldOffset(0)]
        public readonly bool Bool;
        [FieldOffset(0)]
        public readonly Milliseconds Milliseconds;
        [FieldOffset(0)]
        public readonly UnixTime UnixTime;

        public WorldStateValue(int value) { Int32 = value; }
        public WorldStateValue(bool value) { Bool = value; }
        public WorldStateValue(Milliseconds value) { Milliseconds = value; }
        public WorldStateValue(UnixTime value) { UnixTime = value; }

        public static implicit operator WorldStateValue(int value) => new(value);
        public static implicit operator WorldStateValue(bool value) => new(value);
        public static implicit operator WorldStateValue(Milliseconds value) => new(value);
        public static implicit operator WorldStateValue(UnixTime value) => new(value);

        public static implicit operator int(WorldStateValue value) => value.Int32;
        public static implicit operator bool(WorldStateValue value) => value.Bool;
        public static implicit operator Milliseconds(WorldStateValue value) => value.Milliseconds;
        public static implicit operator UnixTime(WorldStateValue value) => value.UnixTime;

        public override string ToString() => Int32.ToString();
        public override int GetHashCode() => Int32;

        public static bool operator ==(WorldStateValue left, WorldStateValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldStateValue left, WorldStateValue right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj);
        }

        public bool Equals(WorldStateValue another)
        {
            return _Raw == another._Raw;
        }
    }
}
