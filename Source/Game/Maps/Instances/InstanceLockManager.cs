// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using System;
using System.Collections.Generic;

namespace Game.Maps
{
    using InstanceLockKey = (int, int);

    public class InstanceLockManager : Singleton<InstanceLockManager>
    {
        object _lockObject = new();
        Dictionary<ObjectGuid, Dictionary<InstanceLockKey, InstanceLock>> _temporaryInstanceLocksByPlayer = new(); // locks stored here before any boss gets killed
        Dictionary<ObjectGuid, Dictionary<InstanceLockKey, InstanceLock>> _instanceLocksByPlayer = new();
        Dictionary<int, SharedInstanceLockData> _instanceLockDataById = new();
        bool _unloading;

        InstanceLockManager() { }

        public void Load()
        {
            Dictionary<int, SharedInstanceLockData> instanceLockDataById = new();

            //                                              0           1     2
            SQLResult result = DB.Characters.Query("SELECT instanceId, data, completedEncountersMask FROM instance");
            if (!result.IsEmpty())
            {
                do
                {
                    int instanceId = result.Read<int>(0);

                    SharedInstanceLockData data = new();
                    data.Data = result.Read<string>(1);
                    data.CompletedEncountersMask = result.Read<uint>(2);
                    data.InstanceId = instanceId;

                    instanceLockDataById[instanceId] = data;

                } while (result.NextRow());
            }

            //           ORDER BY required by MapManager::RegisterInstanceId
            //                                                  0     1      2       3           4           5     6                        7           8
            SQLResult lockResult = DB.Characters.Query("SELECT guid, mapId, lockId, instanceId, difficulty, data, completedEncountersMask, expiryTime, extended FROM character_instance_lock ORDER BY instanceId");
            if (!result.IsEmpty())
            {
                do
                {
                    ObjectGuid playerGuid = ObjectGuid.Create(HighGuid.Player, lockResult.Read<long>(0));
                    int mapId = lockResult.Read<int>(1);
                    int lockId = lockResult.Read<int>(2);
                    int instanceId = lockResult.Read<int>(3);
                    Difficulty difficulty = (Difficulty)lockResult.Read<byte>(4);
                    ServerTime expiryTime = (ServerTime)(UnixTime64)lockResult.Read<long>(7);

                    // Mark instance id as being used
                    Global.MapMgr.RegisterInstanceId(instanceId);

                    InstanceLock instanceLock;
                    if (new MapDb2Entries(mapId, difficulty).IsInstanceIdBound())
                    {
                        var sharedData = instanceLockDataById.LookupByKey(instanceId);
                        if (sharedData == null)
                        {
                            Log.outError(LogFilter.Instance, $"Missing instance data for instance id based lock (id {instanceId})");
                            DB.Characters.Execute($"DELETE FROM character_instance_lock WHERE instanceId = {instanceId}");
                            continue;
                        }

                        instanceLock = new SharedInstanceLock(mapId, difficulty, expiryTime, instanceId, sharedData);
                        _instanceLockDataById[instanceId] = sharedData;
                    }
                    else
                        instanceLock = new InstanceLock(mapId, difficulty, expiryTime, instanceId);

                    instanceLock.GetData().Data = lockResult.Read<string>(5);
                    instanceLock.GetData().CompletedEncountersMask = lockResult.Read<uint>(6);
                    instanceLock.SetExtended(lockResult.Read<bool>(8));

                    _instanceLocksByPlayer[playerGuid][(mapId, lockId)] = instanceLock;

                } while (result.NextRow());
            }
        }

        public void Unload()
        {
            _unloading = true;
            _instanceLocksByPlayer.Clear();
            _instanceLockDataById.Clear();
        }

        public TransferAbortReason CanJoinInstanceLock(ObjectGuid playerGuid, MapDb2Entries entries, InstanceLock instanceLock)
        {
            InstanceLock playerInstanceLock = FindActiveInstanceLock(playerGuid, entries);
            if (playerInstanceLock == null)
                return TransferAbortReason.None;

            if (entries.Map.IsFlexLocking)
            {
                // compare completed encounters - if instance has any encounters unkilled in players lock then cannot enter
                if ((playerInstanceLock.GetData().CompletedEncountersMask & ~instanceLock.GetData().CompletedEncountersMask) != 0)
                    return TransferAbortReason.AlreadyCompletedEncounter;

                return TransferAbortReason.None;
            }

            if (!entries.MapDifficulty.IsUsingEncounterLocks && playerInstanceLock.IsNew() && playerInstanceLock.GetInstanceId() != instanceLock.GetInstanceId())
                return TransferAbortReason.LockedToDifferentInstance;

            return TransferAbortReason.None;
        }

        public InstanceLock FindInstanceLock(Dictionary<ObjectGuid, Dictionary<InstanceLockKey, InstanceLock>> locks, ObjectGuid playerGuid, MapDb2Entries entries)
        {
            var playerLocks = locks.LookupByKey(playerGuid);
            if (playerLocks == null)
                return null;

            return playerLocks.LookupByKey(entries.GetKey());
        }

        public InstanceLock FindActiveInstanceLock(ObjectGuid playerGuid, MapDb2Entries entries)
        {
            lock(_lockObject)
                return FindActiveInstanceLock(playerGuid, entries, false, true);
        }

        public InstanceLock FindActiveInstanceLock(ObjectGuid playerGuid, MapDb2Entries entries, bool ignoreTemporary, bool ignoreExpired)
        {
            if (!entries.MapDifficulty.HasResetSchedule)
                return null;

            InstanceLock instanceLock = FindInstanceLock(_instanceLocksByPlayer, playerGuid, entries);

            // Ignore expired and not extended locks
            if (instanceLock != null && (!instanceLock.IsExpired() || instanceLock.IsExtended() || !ignoreExpired))
                return instanceLock;

            if (ignoreTemporary)
                return null;

            return FindInstanceLock(_temporaryInstanceLocksByPlayer, playerGuid, entries);
        }

        public ICollection<InstanceLock> GetInstanceLocksForPlayer(ObjectGuid playerGuid)
        {
            if (_instanceLocksByPlayer.TryGetValue(playerGuid, out Dictionary<InstanceLockKey, InstanceLock> dictionary))
                return dictionary.Values;

            return new List<InstanceLock>();
        }

        public InstanceLock CreateInstanceLockForNewInstance(ObjectGuid playerGuid, MapDb2Entries entries, int instanceId)
        {
            if (!entries.MapDifficulty.HasResetSchedule)
                return null;

            InstanceLock instanceLock;
            if (entries.IsInstanceIdBound())
            {
                SharedInstanceLockData sharedData = new();
                _instanceLockDataById[instanceId] = sharedData;
                instanceLock = new SharedInstanceLock(entries.MapDifficulty.MapID, entries.MapDifficulty.DifficultyID,
                    GetNextResetTime(entries), instanceId, sharedData);
            }
            else
            {
                instanceLock = new InstanceLock(entries.MapDifficulty.MapID, entries.MapDifficulty.DifficultyID,
                    GetNextResetTime(entries), instanceId);
            }

            instanceLock.SetIsNew(true);

            if (!_temporaryInstanceLocksByPlayer.ContainsKey(playerGuid))
                _temporaryInstanceLocksByPlayer[playerGuid] = new Dictionary<InstanceLockKey, InstanceLock>();

            _temporaryInstanceLocksByPlayer[playerGuid][entries.GetKey()] = instanceLock;

            Log.outDebug(LogFilter.Instance, 
                $"[{entries.Map.Id}-{entries.Map.MapName[Global.WorldMgr.GetDefaultDbcLocale()]} | " +
                $"{entries.MapDifficulty.DifficultyID}-{CliDB.DifficultyStorage.LookupByKey(entries.MapDifficulty.DifficultyID).Name}] " +
                $"Created new temporary instance lock for {playerGuid} in instance {instanceId}");

            return instanceLock;
        }

        public InstanceLock UpdateInstanceLockForPlayer(SQLTransaction trans, ObjectGuid playerGuid, MapDb2Entries entries, InstanceLockUpdateEvent updateEvent)
        {
            InstanceLock instanceLock = FindActiveInstanceLock(playerGuid, entries, true, true);
            if (instanceLock == null)
            {
                lock (_lockObject)
                {
                    // Move lock from temporary storage if it exists there
                    // This is to avoid destroying expired locks before any boss is killed in a fresh lock
                    // player can still change his mind, exit instance and reactivate old lock
                    var playerLocks = _temporaryInstanceLocksByPlayer.LookupByKey(playerGuid);
                    if (playerLocks != null)
                    {
                        var playerInstanceLock = playerLocks.LookupByKey(entries.GetKey());
                        if (playerInstanceLock != null)
                        {
                            instanceLock = playerInstanceLock;
                            _instanceLocksByPlayer[playerGuid][entries.GetKey()] = instanceLock;

                            playerLocks.Remove(entries.GetKey());
                            if (playerLocks.Empty())
                                _temporaryInstanceLocksByPlayer.Remove(playerGuid);

                            Log.outDebug(LogFilter.Instance, 
                                $"[{entries.Map.Id}-{entries.Map.MapName[Global.WorldMgr.GetDefaultDbcLocale()]} | " +
                                $"{entries.MapDifficulty.DifficultyID}-{CliDB.DifficultyStorage.LookupByKey(entries.MapDifficulty.DifficultyID).Name}] " +
                                $"Promoting temporary lock to permanent for {playerGuid} in instance {updateEvent.InstanceId}");
                        }
                    }
                }
            }

            if (instanceLock == null)
            {
                if (entries.IsInstanceIdBound())
                {
                    var sharedDataItr = _instanceLockDataById.LookupByKey(updateEvent.InstanceId);
                    Cypher.Assert(sharedDataItr != null);

                    instanceLock = new SharedInstanceLock(entries.MapDifficulty.MapID, entries.MapDifficulty.DifficultyID,
                        GetNextResetTime(entries), updateEvent.InstanceId, sharedDataItr);
                    Cypher.Assert((instanceLock as SharedInstanceLock).GetSharedData().InstanceId == updateEvent.InstanceId);
                }
                else
                {
                    instanceLock = new InstanceLock(entries.MapDifficulty.MapID, entries.MapDifficulty.DifficultyID,
                        GetNextResetTime(entries), updateEvent.InstanceId);
                }

                lock(_lockObject)
                    _instanceLocksByPlayer[playerGuid][entries.GetKey()] = instanceLock;

                Log.outDebug(LogFilter.Instance, 
                    $"[{entries.Map.Id}-{entries.Map.MapName[Global.WorldMgr.GetDefaultDbcLocale()]} | " +
                    $"{entries.MapDifficulty.DifficultyID}-{CliDB.DifficultyStorage.LookupByKey(entries.MapDifficulty.DifficultyID).Name}] " +
                    $"Created new instance lock for {playerGuid} in instance {updateEvent.InstanceId}");
            }
            else
            {
                if (entries.IsInstanceIdBound())
                {
                    Cypher.Assert(instanceLock.GetInstanceId() == updateEvent.InstanceId);
                    var sharedDataItr = _instanceLockDataById.LookupByKey(updateEvent.InstanceId);
                    Cypher.Assert(sharedDataItr != null);
                    Cypher.Assert(sharedDataItr == (instanceLock as SharedInstanceLock).GetSharedData());
                }

                instanceLock.SetInstanceId(updateEvent.InstanceId);
            }

            instanceLock.SetIsNew(false);
            instanceLock.GetData().Data = updateEvent.NewData;
            if (updateEvent.CompletedEncounter != null)
            {
                instanceLock.GetData().CompletedEncountersMask |= 1u << updateEvent.CompletedEncounter.Bit;
                Log.outDebug(LogFilter.Instance, 
                    $"[{entries.Map.Id}-{entries.Map.MapName[Global.WorldMgr.GetDefaultDbcLocale()]} | " +
                    $"{entries.MapDifficulty.DifficultyID}-{CliDB.DifficultyStorage.LookupByKey(entries.MapDifficulty.DifficultyID).Name}] " +
                    $"Instance lock for {playerGuid} in instance {updateEvent.InstanceId} " +
                    $"gains completed encounter [{updateEvent.CompletedEncounter.Id}-{updateEvent.CompletedEncounter.Name[Global.WorldMgr.GetDefaultDbcLocale()]}]");
            }

            // Synchronize map completed encounters into players completed encounters for UI
            if (!entries.MapDifficulty.IsUsingEncounterLocks)
                instanceLock.GetData().CompletedEncountersMask |= updateEvent.InstanceCompletedEncountersMask;

            if (updateEvent.EntranceWorldSafeLocId.HasValue)
                instanceLock.GetData().EntranceWorldSafeLocId = updateEvent.EntranceWorldSafeLocId.Value;

            if (instanceLock.IsExpired())
            {
                instanceLock.SetExpiryTime(GetNextResetTime(entries));
                instanceLock.SetExtended(false);

                Log.outDebug(LogFilter.Instance, 
                    $"[{entries.Map.Id}-{entries.Map.MapName[Global.WorldMgr.GetDefaultDbcLocale()]} | " +
                    $"{entries.MapDifficulty.DifficultyID}-{CliDB.DifficultyStorage.LookupByKey(entries.MapDifficulty.DifficultyID).Name}] " +
                    $"Expired instance lock for {playerGuid} in instance {updateEvent.InstanceId} is now active");
            }

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_INSTANCE_LOCK);
            stmt.SetInt64(0, playerGuid.GetCounter());
            stmt.SetInt32(1, entries.MapDifficulty.MapID);
            stmt.SetUInt8(2, entries.MapDifficulty.LockID);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_INSTANCE_LOCK);
            stmt.SetInt64(0, playerGuid.GetCounter());
            stmt.SetInt32(1, entries.MapDifficulty.MapID);
            stmt.SetUInt8(2, entries.MapDifficulty.LockID);
            stmt.SetInt32(3, instanceLock.GetInstanceId());
            stmt.SetUInt8(4, (byte)entries.MapDifficulty.DifficultyID);
            stmt.SetString(5, instanceLock.GetData().Data);
            stmt.SetUInt32(6, instanceLock.GetData().CompletedEncountersMask);
            stmt.SetInt32(7, instanceLock.GetData().EntranceWorldSafeLocId);
            stmt.SetInt64(8, (UnixTime64)instanceLock.GetExpiryTime());
            stmt.SetInt32(9, instanceLock.IsExtended() ? 1 : 0);
            trans.Append(stmt);

            return instanceLock;
        }

        public void UpdateSharedInstanceLock(SQLTransaction trans, InstanceLockUpdateEvent updateEvent)
        {
            var sharedData = _instanceLockDataById.LookupByKey(updateEvent.InstanceId);
            Cypher.Assert(sharedData != null);
            Cypher.Assert(sharedData.InstanceId == 0 || sharedData.InstanceId == updateEvent.InstanceId);
            sharedData.Data = updateEvent.NewData;
            sharedData.InstanceId = updateEvent.InstanceId;
            if (updateEvent.CompletedEncounter != null)
            {
                sharedData.CompletedEncountersMask |= 1u << updateEvent.CompletedEncounter.Bit;
                Log.outDebug(LogFilter.Instance, 
                    $"Instance {updateEvent.InstanceId} gains completed encounter " +
                    $"[{updateEvent.CompletedEncounter.Id}-{updateEvent.CompletedEncounter.Name[Global.WorldMgr.GetDefaultDbcLocale()]}]");
            }

            if (updateEvent.EntranceWorldSafeLocId.HasValue)
                sharedData.EntranceWorldSafeLocId = updateEvent.EntranceWorldSafeLocId.Value;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_INSTANCE);
            stmt.SetInt32(0, sharedData.InstanceId);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_INSTANCE);
            stmt.SetInt32(0, sharedData.InstanceId);
            stmt.SetString(1, sharedData.Data);
            stmt.SetUInt32(2, sharedData.CompletedEncountersMask);
            stmt.SetInt32(3, sharedData.EntranceWorldSafeLocId);
            trans.Append(stmt);
        }

        public void OnSharedInstanceLockDataDelete(int instanceId)
        {
            if (_unloading)
                return;

            if (!_instanceLockDataById.ContainsKey(instanceId))
                return;

            _instanceLockDataById.Remove(instanceId);
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_INSTANCE);
            stmt.SetInt32(0, instanceId);
            DB.Characters.Execute(stmt);

            Log.outDebug(LogFilter.Instance, 
                $"Deleting instance {instanceId} as it is no longer referenced by any player");
        }

        public (ServerTime, ServerTime) UpdateInstanceLockExtensionForPlayer(ObjectGuid playerGuid, MapDb2Entries entries, bool extended)
        {
            InstanceLock instanceLock = FindActiveInstanceLock(playerGuid, entries, true, false);
            if (instanceLock != null)
            {
                ServerTime oldExpiryTime = instanceLock.GetEffectiveExpiryTime();
                instanceLock.SetExtended(extended);
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHARACTER_INSTANCE_LOCK_EXTENSION);
                stmt.SetInt32(0, extended ? 1 : 0);
                stmt.SetInt64(1, playerGuid.GetCounter());
                stmt.SetInt32(2, entries.MapDifficulty.MapID);
                stmt.SetUInt8(3, entries.MapDifficulty.LockID);
                DB.Characters.Execute(stmt);

                Log.outDebug(LogFilter.Instance, 
                    $"[{entries.Map.Id}-{entries.Map.MapName[Global.WorldMgr.GetDefaultDbcLocale()]} | " +
                    $"{entries.MapDifficulty.DifficultyID}-{CliDB.DifficultyStorage.LookupByKey(entries.MapDifficulty.DifficultyID).Name}] " +
                    $"Instance lock for {playerGuid} is {(extended ? "now" : "no longer")} extended");

                return (oldExpiryTime, instanceLock.GetEffectiveExpiryTime());
            }

            return (ServerTime.Zero, ServerTime.Zero);
        }

        /// <summary>
        /// Resets instances that match given filter - for use in GM commands
        /// </summary>
        /// <param name="playerGuid">Guid of player whose locks will be removed</param>
        /// <param name="mapId">(Optional) Map id of instance locks to reset</param>
        /// <param name="difficulty">(Optional) Difficulty of instance locks to reset</param>
        /// <param name="locksReset">All locks that were reset</param>
        /// <param name="locksFailedToReset">Locks that could not be reset because they are used by existing instance map</param>
        public void ResetInstanceLocksForPlayer(ObjectGuid playerGuid, int? mapId, Difficulty? difficulty, List<InstanceLock> locksReset, List<InstanceLock> locksFailedToReset)
        {
            var playerLocks = _instanceLocksByPlayer.LookupByKey(playerGuid);
            if (playerLocks == null)
                return;

            foreach (var playerLockPair in playerLocks)
            {
                if (playerLockPair.Value.IsInUse())
                {
                    locksFailedToReset.Add(playerLockPair.Value);
                    continue;
                }

                if (mapId.HasValue && mapId.Value != playerLockPair.Value.GetMapId())
                    continue;

                if (difficulty.HasValue && difficulty.Value != playerLockPair.Value.GetDifficultyId())
                    continue;

                if (playerLockPair.Value.IsExpired())
                    continue;

                locksReset.Add(playerLockPair.Value);
            }

            if (!locksReset.Empty())
            {
                SQLTransaction trans = new();
                foreach (InstanceLock instanceLock in locksReset)
                {
                    MapDb2Entries entries = new(instanceLock.GetMapId(), instanceLock.GetDifficultyId());
                    ServerTime newExpiryTime = GetNextResetTime(entries) - entries.MapDifficulty.RaidDuration;
                    // set reset time to last reset time
                    instanceLock.SetExpiryTime(newExpiryTime);
                    instanceLock.SetExtended(false);

                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHARACTER_INSTANCE_LOCK_FORCE_EXPIRE);
                    stmt.SetInt64(0, (UnixTime64)newExpiryTime);
                    stmt.SetInt64(1, playerGuid.GetCounter());
                    stmt.SetInt32(2, entries.MapDifficulty.MapID);
                    stmt.SetUInt8(3, entries.MapDifficulty.LockID);
                    trans.Append(stmt);
                }
                DB.Characters.CommitTransaction(trans);
            }
        }

        /// <summary>
        /// Retrieves instance lock statistics - for use in GM commands
        /// </summary>
        /// <returns>Statistics info</returns>
        public InstanceLocksStatistics GetStatistics()
        {
            InstanceLocksStatistics statistics;
            statistics.InstanceCount = _instanceLockDataById.Count;
            statistics.PlayerCount = _instanceLocksByPlayer.Count;
            return statistics;
        }
        
        public ServerTime GetNextResetTime(MapDb2Entries entries)
        {
            ServerTime dateTime = LoopTime.ServerTime;
            int resetHour = WorldConfig.Values[WorldCfg.ResetScheduleHour].Int32;

            int hour = 0;
            int day = 0;
            switch (entries.MapDifficulty.ResetInterval)
            {
                case MapDifficultyResetInterval.Daily:
                {
                    if (dateTime.Hour >= resetHour)
                        day++;

                    hour = resetHour;
                    break;
                }
                case MapDifficultyResetInterval.Weekly:
                {
                    int resetDay = WorldConfig.Values[WorldCfg.ResetScheduleWeekDay].Int32;
                    int daysAdjust = resetDay - (int)dateTime.DayOfWeek;
                    if (dateTime.Day > resetDay || (dateTime.Day == resetDay && dateTime.Hour >= resetHour))
                        daysAdjust += 7; // passed it for current week, grab time from next week

                    hour = resetHour;
                    day += daysAdjust;
                    break;
                }
                default:
                    break;
            }

            return dateTime.Date + (Days)day + (Hours)hour;
        }
    }

    public class InstanceLockData
    {
        public string Data;
        public uint CompletedEncountersMask;
        public int EntranceWorldSafeLocId;
    }

    public class InstanceLock
    {
        int _mapId;
        Difficulty _difficultyId;
        int _instanceId;
        ServerTime _expiryTime;
        bool _extended;
        InstanceLockData _data = new();
        bool _isInUse;
        bool _isNew;

        public InstanceLock(int mapId, Difficulty difficultyId, ServerTime expiryTime, int instanceId)
        {
            _mapId = mapId;
            _difficultyId = difficultyId;
            _instanceId = instanceId;
            _expiryTime = expiryTime;
            _extended = false;
        }

        public bool IsExpired()
        {
            return _expiryTime < LoopTime.ServerTime;
        }

        public ServerTime GetEffectiveExpiryTime()
        {
            if (!IsExtended())
                return GetExpiryTime();

            MapDb2Entries entries = new(_mapId, _difficultyId);

            // return next reset time
            if (IsExpired())
                return Global.InstanceLockMgr.GetNextResetTime(entries);

            // if not expired, return expiration time + 1 reset period
            return GetExpiryTime() + entries.MapDifficulty.RaidDuration;
        }

        public int GetMapId() { return _mapId; }

        public Difficulty GetDifficultyId() { return _difficultyId; }

        public int GetInstanceId() { return _instanceId; }

        public void SetInstanceId(int instanceId) { _instanceId = instanceId; }

        public ServerTime GetExpiryTime() { return _expiryTime; }

        public void SetExpiryTime(ServerTime expiryTime) { _expiryTime = expiryTime; }

        public bool IsExtended() { return _extended; }

        public void SetExtended(bool extended) { _extended = extended; }

        public InstanceLockData GetData() { return _data; }

        public virtual InstanceLockData GetInstanceInitializationData() { return _data; }

        public bool IsInUse() { return _isInUse; }

        public void SetInUse(bool inUse) { _isInUse = inUse; }

        public bool IsNew() { return _isNew; }

        public void SetIsNew(bool isNew) { _isNew = isNew; }
    }

    class SharedInstanceLockData : InstanceLockData
    {
        public int InstanceId;

        ~SharedInstanceLockData()
        {
            // Cleanup database
            if (InstanceId != 0)
                Global.InstanceLockMgr.OnSharedInstanceLockDataDelete(InstanceId);
        }
    }

    class SharedInstanceLock : InstanceLock
    {
        /// <summary>
        /// Instance id based locks have two states
        /// One shared by everyone, which is the real state used by instance
        /// and one for each player that shows in UI that might have less encounters completed
        /// </summary>
        SharedInstanceLockData _sharedData;

        public SharedInstanceLock(int mapId, Difficulty difficultyId, ServerTime expiryTime, int instanceId, SharedInstanceLockData sharedData) : base(mapId, difficultyId, expiryTime, instanceId)
        {
            _sharedData = sharedData;            
        }

        public override InstanceLockData GetInstanceInitializationData() { return _sharedData; }

        public SharedInstanceLockData GetSharedData() { return _sharedData; }
    }

    public struct MapDb2Entries
    {
        public MapRecord Map;
        public MapDifficultyRecord MapDifficulty;

        public MapDb2Entries(int mapId, Difficulty difficulty)
        {
            Map = CliDB.MapStorage.LookupByKey(mapId);
            MapDifficulty = Global.DB2Mgr.GetMapDifficultyData(mapId, difficulty);
        }

        public MapDb2Entries(MapRecord map, MapDifficultyRecord mapDifficulty)
        {
            Map = map;
            MapDifficulty = mapDifficulty;
        }

        public InstanceLockKey GetKey()
        {
            return (MapDifficulty.MapID, MapDifficulty.LockID);
        }

        public bool IsInstanceIdBound()
        {
            return !Map.IsFlexLocking && !MapDifficulty.IsUsingEncounterLocks;
        }
    }

    public struct InstanceLockUpdateEvent
    {
        public int InstanceId;
        public string NewData;
        public uint InstanceCompletedEncountersMask;
        public DungeonEncounterRecord CompletedEncounter;
        public int? EntranceWorldSafeLocId;

        public InstanceLockUpdateEvent(int instanceId, string newData, uint instanceCompletedEncountersMask, DungeonEncounterRecord completedEncounter, int? entranceWorldSafeLocId)
        {
            InstanceId = instanceId;
            NewData = newData;
            InstanceCompletedEncountersMask = instanceCompletedEncountersMask;
            CompletedEncounter = completedEncounter;
            EntranceWorldSafeLocId = entranceWorldSafeLocId;
        }
    }

    public struct InstanceLocksStatistics
    {
        public int InstanceCount;   // Number of existing ID-based locks
        public int PlayerCount;     // Number of players that have any lock
    }
}
