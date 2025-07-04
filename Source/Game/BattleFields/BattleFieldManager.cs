﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Database;
using Game.Entities;
using Game.Maps;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.BattleFields
{
    public class BattleFieldManager : Singleton<BattleFieldManager>
    {
        static int[] BattlefieldIdToMapId = [0, 571, 732];
        static int[] BattlefieldIdToZoneId = [0, 4197, 5095]; // imitate World_PVP_Area.db2
        static int[] BattlefieldIdToScriptId = [0, 0, 0];

        BattleFieldManager() { }

        public void InitBattlefield()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            uint count = 0;
            SQLResult result = DB.World.Query("SELECT TypeId, ScriptName FROM battlefield_template");
            if (!result.IsEmpty())
            {
                do
                {
                    BattleFieldTypes typeId = (BattleFieldTypes)result.Read<byte>(0);
                    if (typeId >= BattleFieldTypes.Max)
                    {
                        Log.outError(LogFilter.Sql, $"BattlefieldMgr::InitBattlefield: Invalid TypeId value {typeId} in battlefield_template, skipped.");
                        continue;
                    }

                    BattlefieldIdToScriptId[(int)typeId] = Global.ObjectMgr.GetScriptId(result.Read<string>(1));
                    ++count;

                } while (result.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} battlefields in {Time.Diff(oldMSTime)} ms.");
        }

        public void CreateBattlefieldsForMap(Map map)
        {
            for (uint i = 0; i < (int)BattleFieldTypes.Max; ++i)
            {
                if (BattlefieldIdToScriptId[i] == 0)
                    continue;

                if (BattlefieldIdToMapId[i] != map.GetId())
                    continue;

                BattleField bf = Global.ScriptMgr.CreateBattlefield(BattlefieldIdToScriptId[i], map);
                if (bf == null)
                    continue;

                if (!bf.SetupBattlefield())
                {
                    Log.outInfo(LogFilter.Battlefield, 
                        $"Setting up battlefield with TypeId {(BattleFieldTypes)i} " +
                        $"on map {map.GetId()} instance id {map.GetInstanceId()} failed.");
                    continue;
                }

                _battlefieldsByMap.Add(map, bf);
                Log.outInfo(LogFilter.Battlefield, 
                    $"Setting up battlefield with TypeId {(BattleFieldTypes)i} " +
                    $"on map {map.GetId()} instance id {map.GetInstanceId()} succeeded.");
            }
        }

        public void DestroyBattlefieldsForMap(Map map)
        {
            _battlefieldsByMap.Remove(map);
        }

        public void AddZone(int zoneId, BattleField bf)
        {
            _battlefieldsByZone[(bf.GetMap(), zoneId)] = bf;
        }

        public void HandlePlayerEnterZone(Player player, int zoneId)
        {
            var bf = _battlefieldsByZone.LookupByKey((player.GetMap(), zoneId));
            if (bf == null)
                return;

            if (!bf.IsEnabled() || bf.HasPlayer(player))
                return;

            bf.HandlePlayerEnterZone(player, zoneId);
            Log.outDebug(LogFilter.Battlefield, 
                $"Player {player.GetGUID()} entered battlefield id {bf.GetTypeId()}");
        }

        public void HandlePlayerLeaveZone(Player player, int zoneId)
        {
            var bf = _battlefieldsByZone.LookupByKey((player.GetMap(), zoneId));
            if (bf == null)
                return;

            // teleport: remove once in removefromworld, once in updatezone
            if (!bf.HasPlayer(player))
                return;

            bf.HandlePlayerLeaveZone(player, zoneId);
            Log.outDebug(LogFilter.Battlefield, 
                $"Player {player.GetGUID()} left battlefield id {bf.GetTypeId()}");
        }

        public bool IsWorldPvpArea(int zoneId)
        {
            return BattlefieldIdToZoneId.Contains(zoneId);
        }

        public BattleField GetBattlefieldToZoneId(Map map, int zoneId)
        {
            var bf = _battlefieldsByZone.LookupByKey((map, zoneId));
            if (bf == null)
            {
                // no handle for this zone, return
                return null;
            }

            if (!bf.IsEnabled())
                return null;

            return bf;
        }

        public BattleField GetBattlefieldByBattleId(Map map, uint battleId)
        {
            var battlefields = _battlefieldsByMap[map];
            foreach (var battlefield in battlefields)
            {
                if (battlefield.GetBattleId() == battleId)
                    return battlefield;
            }

            return null;
        }

        public void Update(TimeSpan diff)
        {
            _updateTimer += diff;
            if (_updateTimer > (Seconds)1)
            {
                foreach (var (map, battlefield) in _battlefieldsByMap)
                    if (battlefield.IsEnabled())
                        battlefield.Update(_updateTimer);

                _updateTimer = TimeSpan.Zero;
            }
        }

        // contains all initiated battlefield events
        // used when initing / cleaning up
        MultiMap<Map, BattleField>  _battlefieldsByMap = new();
        // maps the zone ids to an battlefield event
        // used in player event handling
        Dictionary<(Map map, int zoneId), BattleField>  _battlefieldsByZone = new();
        // update interval
        TimeSpan _updateTimer;
    }
}
