﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Entities;
using Game.Maps;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public class PoolManager : Singleton<PoolManager>
    {
        PoolManager() { }

        public void Initialize()
        {
            mGameobjectSearchMap.Clear();
            mCreatureSearchMap.Clear();
        }

        public void LoadFromDB()
        {
            // Pool templates
            {
                RelativeTime oldMSTime = Time.NowRelative;

                SQLResult result = DB.World.Query("SELECT entry, max_limit FROM pool_template");
                if (result.IsEmpty())
                {
                    mPoolTemplate.Clear();
                    Log.outInfo(LogFilter.ServerLoading, 
                        "Loaded 0 object pools. DB table `pool_template` is empty.");
                    return;
                }

                uint count = 0;
                do
                {
                    int pool_id = result.Read<int>(0);

                    PoolTemplateData pPoolTemplate = new();
                    pPoolTemplate.MaxLimit = result.Read<int>(1);
                    pPoolTemplate.MapId = -1;
                    mPoolTemplate[pool_id] = pPoolTemplate;
                    ++count;
                }
                while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} objects pools in {Time.Diff(oldMSTime)} ms.");
            }

            // Creatures

            Log.outInfo(LogFilter.ServerLoading, "Loading Creatures Pooling Data...");
            {
                RelativeTime oldMSTime = Time.NowRelative;

                //                                         1        2            3
                SQLResult result = DB.World.Query("SELECT spawnId, poolSpawnId, Chance FROM pool_members WHERE Type = 0");

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, 
                        "Loaded 0 creatures in  pools. DB table `pool_creature` is empty.");
                }
                else
                {
                    uint count = 0;
                    do
                    {
                        long guid = result.Read<long>(0);
                        int pool_id = result.Read<int>(1);
                        float chance = result.Read<float>(2);

                        CreatureData data = Global.ObjectMgr.GetCreatureData(guid);
                        if (data == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_creature` has a non existing creature spawn" +
                                $" (GUID: {guid}) defined for pool id ({pool_id}), skipped.");
                            continue;
                        }
                        if (!mPoolTemplate.ContainsKey(pool_id))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_creature` pool id ({pool_id}) " +
                                $"is not in `pool_template`, skipped.");
                            continue;
                        }
                        if (chance < 0 || chance > 100)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_creature` has an invalid Chance ({chance}) " +
                                $"for creature guid ({guid}) in pool id ({pool_id}), skipped.");
                            continue;
                        }

                        PoolTemplateData pPoolTemplate = mPoolTemplate[pool_id];
                        if (pPoolTemplate.MapId == -1)
                            pPoolTemplate.MapId = data.MapId;

                        if (pPoolTemplate.MapId != data.MapId)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_creature` has creature spawns " +
                                $"on multiple different maps for creature guid " +
                                $"({guid}) in pool id ({pool_id}), skipped.");
                            continue;
                        }

                        PoolObject plObject = new(guid, chance);

                        if (!mPoolCreatureGroups.ContainsKey(pool_id))
                            mPoolCreatureGroups[pool_id] = new PoolGroup<Creature>();

                        PoolGroup<Creature> cregroup = mPoolCreatureGroups[pool_id];
                        cregroup.SetPoolId(pool_id);
                        cregroup.AddEntry(plObject, pPoolTemplate.MaxLimit);

                        mCreatureSearchMap.Add(guid, pool_id);
                        ++count;
                    }
                    while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, 
                        $"Loaded {count} creatures in pools in {Time.Diff(oldMSTime)} ms.");
                }
            }

            // Gameobjects

            Log.outInfo(LogFilter.ServerLoading, "Loading Gameobject Pooling Data...");
            {
                RelativeTime oldMSTime = Time.NowRelative;

                //                                         1        2            3
                SQLResult result = DB.World.Query("SELECT spawnId, poolSpawnId, Chance FROM pool_members WHERE Type = 1");

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, 
                        "Loaded 0 gameobjects in  pools. " +
                        "DB table `pool_gameobject` is empty.");
                }
                else
                {
                    uint count = 0;
                    do
                    {
                        long guid = result.Read<long>(0);
                        int pool_id = result.Read<int>(1);
                        float chance = result.Read<float>(2);

                        GameObjectData data = Global.ObjectMgr.GetGameObjectData(guid);
                        if (data == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_gameobject` has a non existing gameobject spawn " +
                                $"(GUID: {guid}) defined for pool id ({pool_id}), skipped.");
                            continue;
                        }

                        if (!mPoolTemplate.ContainsKey(pool_id))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`pool_gameobject` pool id ({pool_id}) " +
                                $"is not in `pool_template`, skipped.");
                            continue;
                        }

                        if (chance < 0 || chance > 100)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_gameobject` has an invalid Chance ({chance}) " +
                                $"for gameobject guid ({guid}) in pool id ({pool_id}), skipped.");
                            continue;
                        }

                        PoolTemplateData pPoolTemplate = mPoolTemplate[pool_id];
                        if (pPoolTemplate.MapId == -1)
                            pPoolTemplate.MapId = data.MapId;

                        if (pPoolTemplate.MapId != data.MapId)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_gameobject` has gameobject spawns " +
                                $"on multiple different maps for gameobject guid ({guid}) " +
                                $"in pool id ({pool_id}), skipped.");
                            continue;
                        }

                        PoolObject plObject = new(guid, chance);

                        if (!mPoolGameobjectGroups.ContainsKey(pool_id))
                            mPoolGameobjectGroups[pool_id] = new PoolGroup<GameObject>();

                        PoolGroup<GameObject> gogroup = mPoolGameobjectGroups[pool_id];
                        gogroup.SetPoolId(pool_id);
                        gogroup.AddEntry(plObject, pPoolTemplate.MaxLimit);

                        mGameobjectSearchMap.Add(guid, pool_id);
                        ++count;
                    }
                    while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, 
                        $"Loaded {count} gameobject in pools in {Time.Diff(oldMSTime)} ms.");
                }
            }

            // Pool of pools

            Log.outInfo(LogFilter.ServerLoading, "Loading Mother Pooling Data...");
            {
                RelativeTime oldMSTime = Time.NowRelative;

                //                                         1        2            3
                SQLResult result = DB.World.Query("SELECT spawnId, poolSpawnId, Chance FROM pool_members WHERE Type = 2");

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 pools in pools");
                }
                else
                {
                    uint count = 0;
                    do
                    {
                        int child_pool_id = result.Read<int>(0);
                        int mother_pool_id = result.Read<int>(1);
                        float chance = result.Read<float>(2);

                        if (!mPoolTemplate.ContainsKey(mother_pool_id))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_pool` mother_pool id ({mother_pool_id}) " +
                                $"is not in `pool_template`, skipped.");
                            continue;
                        }
                        if (!mPoolTemplate.ContainsKey(child_pool_id))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_pool` included pool_id ({child_pool_id}) " +
                                $"is not in `pool_template`, skipped.");
                            continue;
                        }
                        if (mother_pool_id == child_pool_id)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_pool` pool_id ({child_pool_id}) includes itself, " +
                                $"dead-lock detected, skipped.");
                            continue;
                        }
                        if (chance < 0 || chance > 100)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"`pool_pool` has an invalid Chance ({chance}) " +
                                $"for pool id ({child_pool_id}) " +
                                $"in mother pool id ({mother_pool_id}), skipped.");
                            continue;
                        }
                        PoolTemplateData pPoolTemplateMother = mPoolTemplate[mother_pool_id];
                        PoolObject plObject = new(child_pool_id, chance);

                        if (!mPoolPoolGroups.ContainsKey(mother_pool_id))
                            mPoolPoolGroups[mother_pool_id] = new PoolGroup<Pool>();

                        PoolGroup<Pool> plgroup = mPoolPoolGroups[mother_pool_id];
                        plgroup.SetPoolId(mother_pool_id);
                        plgroup.AddEntry(plObject, pPoolTemplateMother.MaxLimit);

                        mPoolSearchMap.Add(child_pool_id, mother_pool_id);
                        ++count;
                    }
                    while (result.NextRow());

                    // Now check for circular reference
                    // All pool_ids are in pool_template
                    foreach (var (id, poolData) in mPoolTemplate)
                    {
                        List<int> checkedPools = new();
                        var poolItr = mPoolSearchMap.LookupByKey(id);
                        while (poolItr != 0)
                        {
                            if (poolData.MapId != -1)
                            {
                                if (mPoolTemplate[poolItr].MapId == -1)
                                    mPoolTemplate[poolItr].MapId = poolData.MapId;

                                if (mPoolTemplate[poolItr].MapId != poolData.MapId)
                                {
                                    Log.outError(LogFilter.Sql, 
                                        $"`pool_pool` has child pools on multiple maps " +
                                        $"in pool id ({poolItr}), skipped.");

                                    mPoolPoolGroups[poolItr].RemoveOneRelation(id);
                                    mPoolSearchMap.Remove(poolItr);
                                    --count;
                                    break;
                                }
                            }

                            checkedPools.Add(id);
                            if (checkedPools.Contains(poolItr))
                            {
                                string ss = "The pool(s) ";
                                foreach (var itr in checkedPools)
                                {
                                    ss += $"{itr} ";
                                }

                                ss += $"create(s) a circular reference, which can cause the server to freeze.\n" +
                                    $"Removing the last link between mother pool {id} and child pool {poolItr}";

                                Log.outError(LogFilter.Sql, ss);
                                mPoolPoolGroups[poolItr].RemoveOneRelation(id);
                                mPoolSearchMap.Remove(poolItr);
                                --count;
                                break;
                            }

                            poolItr = mPoolSearchMap.LookupByKey(poolItr);
                        }
                    }

                    Log.outInfo(LogFilter.ServerLoading, 
                        $"Loaded {count} pools in mother pools in {Time.Diff(oldMSTime)} ms.");
                }
            }

            foreach (var (poolId, templateData) in mPoolTemplate)
            {
                if (IsEmpty(poolId))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Pool Id {poolId} is empty (has no creatures and no gameobects " +
                        $"and either no child pools or child pools are all empty. " +
                        $"The pool will not be spawned");
                    continue;
                }
                Cypher.Assert(templateData.MapId != -1);
            }

            // The initialize method will spawn all pools not in an event and not in another pool, this is why there is 2 left joins with 2 null checks
            Log.outInfo(LogFilter.ServerLoading, "Starting objects pooling system...");
            {
                RelativeTime oldMSTime = Time.NowRelative;

                SQLResult result = DB.World.Query("SELECT DISTINCT pool_template.entry, pool_members.spawnId, pool_members.poolSpawnId FROM pool_template" +
                    " LEFT JOIN game_event_pool ON pool_template.entry=game_event_pool.pool_entry" +
                    " LEFT JOIN pool_members ON pool_members.Type = 2 AND pool_template.entry = pool_members.spawnId WHERE game_event_pool.pool_entry IS NULL");

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Pool handling system initialized, 0 pools spawned.");
                }
                else
                {
                    uint count = 0;
                    do
                    {
                        int pool_entry = result.Read<int>(0);
                        int pool_pool_id = result.Read<int>(1);

                        if (IsEmpty(pool_entry))
                            continue;

                        if (!CheckPool(pool_entry))
                        {
                            if (pool_pool_id != 0)
                            {
                                // The pool is a child pool in pool_pool table. Ideally we should remove it from the pool handler to ensure it never gets spawned,
                                // however that could recursively invalidate entire chain of mother pools. It can be done in the future but for now we'll do nothing.
                                Log.outError(LogFilter.Sql,
                                    $"Pool Id {pool_entry} has no equal Chance pooled entites defined " +
                                    $"and explicit Chance sum is not 100. " +
                                    $"This broken pool is a child pool of Id {result.Read<uint>(2)} " +
                                    $"and cannot be safely removed.");
                            }
                            else
                            {
                                Log.outError(LogFilter.Sql,
                                    $"Pool Id {pool_entry} has no equal Chance pooled entites defined " +
                                    $"and explicit Chance sum is not 100. " +
                                    $"The pool will not be spawned.");
                            }
                            continue;
                        }

                        // Don't spawn child pools, they are spawned recursively by their parent pools
                        if (pool_pool_id == 0)
                        {
                            mAutoSpawnPoolsPerMap.Add(mPoolTemplate[pool_entry].MapId, pool_entry);
                            count++;
                        }
                    }
                    while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading,
                        $"Pool handling system initialized, {count} pools " +
                        $"will be spawned by default in {Time.Diff(oldMSTime)} ms.");

                }
            }
        }

        void SpawnPool<T>(SpawnedPoolData spawnedPoolData, int pool_id, long db_guid)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                    if (mPoolCreatureGroups.ContainsKey(pool_id) && !mPoolCreatureGroups[pool_id].IsEmpty())
                        mPoolCreatureGroups[pool_id].SpawnObject(spawnedPoolData, mPoolTemplate[pool_id].MaxLimit, db_guid);
                    break;
                case "GameObject":
                    if (mPoolGameobjectGroups.ContainsKey(pool_id) && !mPoolGameobjectGroups[pool_id].IsEmpty())
                        mPoolGameobjectGroups[pool_id].SpawnObject(spawnedPoolData, mPoolTemplate[pool_id].MaxLimit, db_guid);
                    break;
                case "Pool":
                    if (mPoolPoolGroups.ContainsKey(pool_id) && !mPoolPoolGroups[pool_id].IsEmpty())
                        mPoolPoolGroups[pool_id].SpawnObject(spawnedPoolData, mPoolTemplate[pool_id].MaxLimit, db_guid);
                    break;
            }
        }

        public void SpawnPool(SpawnedPoolData spawnedPoolData, int pool_id)
        {
            SpawnPool<Pool>(spawnedPoolData, pool_id, 0);
            SpawnPool<GameObject>(spawnedPoolData, pool_id, 0);
            SpawnPool<Creature>(spawnedPoolData, pool_id, 0);
        }

        public void DespawnPool(SpawnedPoolData spawnedPoolData, int pool_id, bool alwaysDeleteRespawnTime = false)
        {
            if (mPoolCreatureGroups.ContainsKey(pool_id) && !mPoolCreatureGroups[pool_id].IsEmpty())
                mPoolCreatureGroups[pool_id].DespawnObject(spawnedPoolData, 0, alwaysDeleteRespawnTime);

            if (mPoolGameobjectGroups.ContainsKey(pool_id) && !mPoolGameobjectGroups[pool_id].IsEmpty())
                mPoolGameobjectGroups[pool_id].DespawnObject(spawnedPoolData, 0, alwaysDeleteRespawnTime);

            if (mPoolPoolGroups.ContainsKey(pool_id) && !mPoolPoolGroups[pool_id].IsEmpty())
                mPoolPoolGroups[pool_id].DespawnObject(spawnedPoolData, 0, alwaysDeleteRespawnTime);
        }

        public bool IsEmpty(int pool_id)
        {
            if (mPoolGameobjectGroups.TryGetValue(pool_id, out PoolGroup<GameObject> gameobjectPool) && !gameobjectPool.IsEmptyDeepCheck())
                return false;

            if (mPoolCreatureGroups.TryGetValue(pool_id, out PoolGroup<Creature> creaturePool) && !creaturePool.IsEmptyDeepCheck())
                return false;

            if (mPoolPoolGroups.TryGetValue(pool_id, out PoolGroup<Pool> pool) && !pool.IsEmptyDeepCheck())
                return false;

            return true;
        }

        public bool CheckPool(int pool_id)
        {
            if (mPoolGameobjectGroups.ContainsKey(pool_id) && !mPoolGameobjectGroups[pool_id].CheckPool())
                return false;

            if (mPoolCreatureGroups.ContainsKey(pool_id) && !mPoolCreatureGroups[pool_id].CheckPool())
                return false;

            if (mPoolPoolGroups.ContainsKey(pool_id) && !mPoolPoolGroups[pool_id].CheckPool())
                return false;

            return true;
        }

        public void UpdatePool<T>(SpawnedPoolData spawnedPoolData, int pool_id, long db_guid_or_pool_id)
        {
            int motherpoolid = IsPartOfAPool<Pool>(pool_id);
            if (motherpoolid != 0)
                SpawnPool<Pool>(spawnedPoolData, motherpoolid, pool_id);
            else
                SpawnPool<T>(spawnedPoolData, pool_id, db_guid_or_pool_id);
        }

        public void UpdatePool(SpawnedPoolData spawnedPoolData, int pool_id, SpawnObjectType type, long spawnId)
        {
            switch (type)
            {
                case SpawnObjectType.Creature:
                    UpdatePool<Creature>(spawnedPoolData, pool_id, spawnId);
                    break;
                case SpawnObjectType.GameObject:
                    UpdatePool<GameObject>(spawnedPoolData, pool_id, spawnId);
                    break;
            }
        }

        public SpawnedPoolData InitPoolsForMap(Map map)
        {
            SpawnedPoolData spawnedPoolData = new(map);
            var poolIds = mAutoSpawnPoolsPerMap[spawnedPoolData.GetMap().GetId()];
            foreach (var poolId in poolIds)
                SpawnPool(spawnedPoolData, poolId);

            return spawnedPoolData;
        }

        public PoolTemplateData GetPoolTemplate(int pool_id)
        {
            return mPoolTemplate.LookupByKey(pool_id);
        }
        
        public int IsPartOfAPool<T>(long db_guid)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                    return mCreatureSearchMap.LookupByKey(db_guid);
                case "GameObject":
                    return mGameobjectSearchMap.LookupByKey(db_guid);
                case "Pool":
                    return mPoolSearchMap.LookupByKey(db_guid);
            }
            return 0;
        }

        // Selects proper template overload to call based on passed Type
        public int IsPartOfAPool(SpawnObjectType type, long spawnId)
        {
            switch (type)
            {
                case SpawnObjectType.Creature:
                    return IsPartOfAPool<Creature>(spawnId);
                case SpawnObjectType.GameObject:
                    return IsPartOfAPool<GameObject>(spawnId);
                case SpawnObjectType.AreaTrigger:
                    return 0;
                default:
                    Cypher.Assert(false, 
                        $"Invalid spawn Type {type} passed to PoolMgr.IsPartOfPool " +
                        $"(with spawnId {spawnId})");
                    return 0;
            }
        }

        public enum QuestTypes
        {
            None = 0,
            Daily = 1,
            Weekly = 2
        }

        public bool IsSpawnedObject<T>(long db_guid_or_pool_id)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                    return mCreatureSearchMap.ContainsKey(db_guid_or_pool_id);
                case "GameObject":
                    return mGameobjectSearchMap.ContainsKey(db_guid_or_pool_id);
                case "Pool":
                    return mPoolSearchMap.ContainsKey(db_guid_or_pool_id);
            }
            return false;
        }

        public MultiMap<int, int> mQuestCreatureRelation = new();
        public MultiMap<int, int> mQuestGORelation = new();

        Dictionary<int, PoolTemplateData> mPoolTemplate = new();
        Dictionary<int, PoolGroup<Creature>> mPoolCreatureGroups = new();
        Dictionary<int, PoolGroup<GameObject>> mPoolGameobjectGroups = new();
        Dictionary<int, PoolGroup<Pool>> mPoolPoolGroups = new();
        Dictionary<long, int> mCreatureSearchMap = new();
        Dictionary<long, int> mGameobjectSearchMap = new();
        Dictionary<long, int> mPoolSearchMap = new();

        MultiMap<int, int> mAutoSpawnPoolsPerMap = new();
    }

    public class PoolGroup<T>
    {
        public PoolGroup()
        {
            poolId = 0;
        }

        public bool IsEmptyDeepCheck()
        {
            if (typeof(T).Name != "Pool")
                return IsEmpty();

            foreach (PoolObject explicitlyChanced in ExplicitlyChanced)
                if (!Global.PoolMgr.IsEmpty((int)explicitlyChanced.guid))
                    return false;

            foreach (PoolObject equalChanced in EqualChanced)
                if (!Global.PoolMgr.IsEmpty((int)equalChanced.guid))
                    return false;

            return true;
        }

        public void AddEntry(PoolObject poolitem, int maxentries)
        {
            if (poolitem.chance != 0 && maxentries == 1)
                ExplicitlyChanced.Add(poolitem);
            else
                EqualChanced.Add(poolitem);
        }

        public bool CheckPool()
        {
            if (EqualChanced.Empty())
            {
                float chance = 0;
                for (int i = 0; i < ExplicitlyChanced.Count; ++i)
                    chance += ExplicitlyChanced[i].chance;
                if (chance != 100 && chance != 0)
                    return false;
            }
            return true;
        }

        public void DespawnObject(SpawnedPoolData spawns, long guid = 0, bool alwaysDeleteRespawnTime = false)
        {
            for (int i = 0; i < EqualChanced.Count; ++i)
            {
                // if spawned
                if (spawns.IsSpawnedObject<T>(EqualChanced[i].guid))
                {
                    if (guid == 0 || EqualChanced[i].guid == guid)
                    {
                        Despawn1Object(spawns, EqualChanced[i].guid, alwaysDeleteRespawnTime);
                        spawns.RemoveSpawn<T>(EqualChanced[i].guid, poolId);
                    }
                }
                else if (alwaysDeleteRespawnTime)
                    RemoveRespawnTimeFromDB(spawns, EqualChanced[i].guid);
            }

            for (int i = 0; i < ExplicitlyChanced.Count; ++i)
            {
                // spawned
                if (spawns.IsSpawnedObject<T>(ExplicitlyChanced[i].guid))
                {
                    if (guid == 0 || ExplicitlyChanced[i].guid == guid)
                    {
                        Despawn1Object(spawns, ExplicitlyChanced[i].guid, alwaysDeleteRespawnTime);
                        spawns.RemoveSpawn<T>(ExplicitlyChanced[i].guid, poolId);
                    }
                }
                else if (alwaysDeleteRespawnTime)
                    RemoveRespawnTimeFromDB(spawns, ExplicitlyChanced[i].guid);
            }
        }

        void Despawn1Object(SpawnedPoolData spawns, long guid, bool alwaysDeleteRespawnTime = false, bool saveRespawnTime = true)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                {
                    var creatureBounds = spawns.GetMap().GetCreatureBySpawnIdStore()[guid];
                    foreach (var creature in creatureBounds)
                    {
                        // For dynamic spawns, save respawn time here
                        if (saveRespawnTime && !creature.GetRespawnCompatibilityMode())
                            creature.SaveRespawnTime();

                        creature.AddObjectToRemoveList();
                    }

                    if (alwaysDeleteRespawnTime)
                        spawns.GetMap().RemoveRespawnTime(SpawnObjectType.Creature, guid, null, true);

                    break;
                }
                case "GameObject":
                {
                    var gameobjectBounds = spawns.GetMap().GetGameObjectBySpawnIdStore()[guid];
                    foreach (var go in gameobjectBounds)
                    {
                        // For dynamic spawns, save respawn time here
                        if (saveRespawnTime && !go.GetRespawnCompatibilityMode())
                            go.SaveRespawnTime();

                        go.AddObjectToRemoveList();
                    }

                    if (alwaysDeleteRespawnTime)
                        spawns.GetMap().RemoveRespawnTime(SpawnObjectType.GameObject, guid, null, true);

                    break;
                }
                case "Pool":
                    Global.PoolMgr.DespawnPool(spawns, (int)guid, alwaysDeleteRespawnTime);
                    break;
            }
        }

        public void RemoveOneRelation(int child_pool_id)
        {
            if (typeof(T).Name != "Pool")
                return;

            foreach (var poolObject in ExplicitlyChanced)
            {
                if (poolObject.guid == child_pool_id)
                {
                    ExplicitlyChanced.Remove(poolObject);
                    break;
                }
            }

            foreach (var poolObject in EqualChanced)
            {
                if (poolObject.guid == child_pool_id)
                {
                    EqualChanced.Remove(poolObject);
                    break;
                }
            }
        }

        public void SpawnObject(SpawnedPoolData spawns, int limit, long triggerFrom)
        {
            int count = limit - spawns.GetSpawnedObjects(poolId);

            // If triggered from some object respawn this object is still marked as spawned
            // and also counted into m_SpawnedPoolAmount so we need increase count to be
            // spawned by 1
            if (triggerFrom != 0)
                ++count;

            // This will try to spawn the rest of pool, not guaranteed
            if (count > 0)
            {
                List<PoolObject> rolledObjects = new();

                // roll objects to be spawned
                if (!ExplicitlyChanced.Empty())
                {
                    float roll = (float)RandomHelper.randPercent();

                    foreach (PoolObject obj in ExplicitlyChanced)
                    {
                        roll -= obj.chance;
                        // Triggering object is marked as spawned at this time and can be also rolled (respawn case)
                        // so this need explicit check for this case
                        if (roll < 0 && (obj.guid == triggerFrom || !spawns.IsSpawnedObject<T>(obj.guid)))
                        {
                            rolledObjects.Add(obj);
                            break;
                        }
                    }
                }

                if (!EqualChanced.Empty() && rolledObjects.Empty())
                {
                    rolledObjects.AddRange(EqualChanced.Where(obj => 
                    obj.guid == triggerFrom || !spawns.IsSpawnedObject<T>(obj.guid))
                        );

                    rolledObjects.RandomResize(count);
                }

                // try to spawn rolled objects
                foreach (PoolObject obj in rolledObjects)
                {
                    if (obj.guid == triggerFrom)
                    {
                        ReSpawn1Object(spawns, obj);
                        triggerFrom = 0;
                    }
                    else
                    {
                        spawns.AddSpawn<T>(obj.guid, poolId);
                        Spawn1Object(spawns, obj);
                    }
                }
            }

            // One spawn one despawn no count increase
            if (triggerFrom != 0)
                DespawnObject(spawns, triggerFrom);
        }

        void Spawn1Object(SpawnedPoolData spawns, PoolObject obj)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                {
                    CreatureData data = Global.ObjectMgr.GetCreatureData(obj.guid);
                    if (data != null)
                    {
                        // Spawn if necessary (loaded grids only)
                        // We use spawn coords to spawn
                        if (spawns.GetMap().IsGridLoaded(data.SpawnPoint))
                            Creature.CreateCreatureFromDB(obj.guid, spawns.GetMap());
                    }
                }
                break;
                case "GameObject":
                {
                    GameObjectData data = Global.ObjectMgr.GetGameObjectData(obj.guid);
                    if (data != null)
                    {
                        // Spawn if necessary (loaded grids only)
                        // We use current coords to unspawn, not spawn coords since creature can have changed grid
                        if (spawns.GetMap().IsGridLoaded(data.SpawnPoint))
                        {
                            GameObject go = GameObject.CreateGameObjectFromDB(obj.guid, spawns.GetMap(), false);
                            if (go != null && go.IsSpawnedByDefault())
                                if (!spawns.GetMap().AddToMap(go))
                                    go.Dispose();
                        }
                    }
                }
                break;
                case "Pool":
                    Global.PoolMgr.SpawnPool(spawns, (int)obj.guid);
                    break;
            }
        }

        void ReSpawn1Object(SpawnedPoolData spawns, PoolObject obj)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                case "GameObject":
                    Despawn1Object(spawns, obj.guid, false, false);
                    Spawn1Object(spawns, obj);
                    break;
            }
        }

        void RemoveRespawnTimeFromDB(SpawnedPoolData spawns, long guid)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                    spawns.GetMap().RemoveRespawnTime(SpawnObjectType.Creature, guid, null, true);
                    break;
                case "GameObject":
                    spawns.GetMap().RemoveRespawnTime(SpawnObjectType.GameObject, guid, null, true);
                    break;
            }
        }

        public void SetPoolId(int pool_id) { poolId = pool_id; }

        public bool IsEmpty() { return ExplicitlyChanced.Empty() && EqualChanced.Empty(); }

        public int GetPoolId() { return poolId; }

        int poolId;
        List<PoolObject> ExplicitlyChanced = new();
        List<PoolObject> EqualChanced = new();
    }

    public class SpawnedPoolData
    {
        Map mOwner;
        List<long> mSpawnedCreatures = new();
        List<long> mSpawnedGameobjects = new();
        Dictionary<long, int> mSpawnedPools = new();

        public SpawnedPoolData(Map owner)
        {
            mOwner = owner;
        }

        public int GetSpawnedObjects(int pool_id)
        {
            return mSpawnedPools.LookupByKey(pool_id);
        }

        public bool IsSpawnedObject<T>(long db_guid)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                    return mSpawnedCreatures.Contains(db_guid);
                case "GameObject":
                    return mSpawnedGameobjects.Contains(db_guid);
                case "Pool":
                    return mSpawnedPools.ContainsKey(db_guid);
                default:
                    return false;
            }
        }

        public bool IsSpawnedObject(SpawnObjectType type, long db_guid_or_pool_id)
        {
            switch (type)
            {
                case SpawnObjectType.Creature:
                    return mSpawnedCreatures.Contains(db_guid_or_pool_id);
                case SpawnObjectType.GameObject:
                    return mSpawnedGameobjects.Contains(db_guid_or_pool_id);
                default:
                    Log.outFatal(LogFilter.Misc, 
                        $"Invalid spawn Type {type} passed to SpawnedPoolData::IsSpawnedObject " +
                        $"(with spawnId {db_guid_or_pool_id})");
                    return false;
            }
        }

        public void AddSpawn<T>(long db_guid, int pool_id)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                    mSpawnedCreatures.Add(db_guid);
                    break;
                case "GameObject":
                    mSpawnedGameobjects.Add(db_guid);
                    break;
                case "Pool":
                    mSpawnedPools[db_guid] = 0;
                    break;
                default:
                    return;
            }
            if (!mSpawnedPools.ContainsKey(pool_id))
                mSpawnedPools[pool_id] = 0;

            ++mSpawnedPools[pool_id];
        }

        public void RemoveSpawn<T>(long db_guid, int pool_id)
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                    mSpawnedCreatures.Remove(db_guid);
                    break;
                case "GameObject":
                    mSpawnedGameobjects.Remove(db_guid);
                    break;
                case "Pool":
                    mSpawnedPools.Remove(db_guid);
                    break;
                default:
                    return;
            }

            if (mSpawnedPools[pool_id] > 0)
                --mSpawnedPools[pool_id];
        }

        public Map GetMap() { return mOwner; }
    }

    public class PoolObject
    {
        public PoolObject(long _guid, float _chance)
        {
            guid = _guid;
            chance = Math.Abs(_chance);
        }

        public long guid;
        public float chance;
    }

    public class PoolTemplateData
    {
        public int MaxLimit;
        public int MapId;
    }

    public class Pool { }                 // for Pool of Pool case
}
