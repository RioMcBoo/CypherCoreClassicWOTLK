﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using System;
using System.Collections.Generic;

namespace Game
{
    public class DisableManager : Singleton<DisableManager>
    {
        DisableManager() { }

        public class DisableData
        {
            public ushort flags;
            public List<int> param0 = new();
            public List<int> param1 = new();
        }

        Dictionary<DisableType, Dictionary<int, DisableData>> m_DisableMap = new();

        public void LoadDisables()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            // reload case
            m_DisableMap.Clear();

            SQLResult result = DB.World.Query("SELECT sourceType, entry, flags, params_0, params_1 FROM disables");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 disables. DB table `disables` is empty!");
                return;
            }

            uint total_count = 0;
            do
            {
                DisableType type = (DisableType)result.Read<uint>(0);
                if (type >= DisableType.Max)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Invalid Type {type} specified in `disables` table, skipped.");
                    continue;
                }

                int entry = result.Read<int>(1);
                DisableFlags flags = (DisableFlags)result.Read<ushort>(2);
                string params_0 = result.Read<string>(3);
                string params_1 = result.Read<string>(4);

                DisableData data = new();
                data.flags = (ushort)flags;

                switch (type)
                {
                    case DisableType.Spell:
                        if (!(Global.SpellMgr.HasSpellInfo(entry, Difficulty.None) || flags.HasFlag(DisableFlags.SpellDeprecatedSpell)))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Spell entry {entry} from `disables` doesn't exist in dbc, skipped.");
                            continue;
                        }

                        if (flags == 0 || flags > DisableFlags.MaxSpell)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Disable flags for spell {entry} are invalid, skipped.");
                            continue;
                        }

                        if (flags.HasFlag(DisableFlags.SpellMap))
                        {
                            var array = new StringArray(params_0, ',');
                            for (byte i = 0; i < array.Length;)
                            {
                                if (int.TryParse(array[i++], out int id))
                                    data.param0.Add(id);
                            }
                        }

                        if (flags.HasFlag(DisableFlags.SpellArea))
                        {
                            var array = new StringArray(params_1, ',');
                            for (byte i = 0; i < array.Length;)
                            {
                                if (int.TryParse(array[i++], out int id))
                                    data.param1.Add(id);
                            }
                        }

                        break;
                    // checked later
                    case DisableType.Quest:
                        break;
                    case DisableType.Map:
                    case DisableType.LFGMap:
                    {
                        MapRecord mapEntry = CliDB.MapStorage.LookupByKey(entry);
                        if (mapEntry == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Map entry {entry} from `disables` doesn't exist in dbc, skipped.");
                            continue;
                        }
                        bool isFlagInvalid = false;
                        switch (mapEntry.InstanceType)
                        {
                            case MapTypes.Common:
                                if (flags != 0)
                                    isFlagInvalid = true;
                                break;
                            case MapTypes.Instance:
                            case MapTypes.Raid:
                                if (flags.HasFlag(DisableFlags.DungeonStatusHeroic) && Global.DB2Mgr.GetMapDifficultyData(entry, Difficulty.Heroic) == null)
                                    flags &= ~DisableFlags.DungeonStatusHeroic;
                                if (flags.HasFlag(DisableFlags.DungeonStatusHeroic10Man) && Global.DB2Mgr.GetMapDifficultyData(entry, Difficulty.Raid10HC) == null)
                                    flags &= ~DisableFlags.DungeonStatusHeroic10Man;
                                if (flags.HasFlag(DisableFlags.DungeonStatusHeroic25Man) && Global.DB2Mgr.GetMapDifficultyData(entry, Difficulty.Raid25HC) == null)
                                    flags &= ~DisableFlags.DungeonStatusHeroic25Man;
                                if (flags == 0)
                                    isFlagInvalid = true;
                                break;
                            case MapTypes.Battleground:
                            case MapTypes.Arena:
                                Log.outError(LogFilter.Sql, 
                                    $"Battlegroundmap {entry} specified to be disabled in map case, skipped.");
                                continue;
                        }
                        if (isFlagInvalid)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Disable flags for map {entry} are invalid, skipped.");
                            continue;
                        }
                        break;
                    }
                    case DisableType.Battleground:
                        if (!CliDB.BattlemasterListStorage.ContainsKey(entry))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Battlegroundentry {entry} from `disables` doesn't exist in dbc, skipped.");
                            continue;
                        }
                        if (flags != 0)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Disable flags specified for Battleground{entry}, useless data.");
                        }
                        break;
                    case DisableType.OutdoorPVP:
                        if (entry > (int)OutdoorPvPTypes.Max)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"OutdoorPvPTypes value {entry} from `disables` is invalid, skipped.");
                            continue;
                        }
                        if (flags != 0)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Disable flags specified for outdoor PvP {entry}, useless data.");
                        }
                        break;
                    case DisableType.Criteria:
                        if (Global.CriteriaMgr.GetCriteria(entry) == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Criteria entry {entry} from `disables` doesn't exist in dbc, skipped.");
                            continue;
                        }
                        if (flags != 0)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Disable flags specified for Criteria {entry}, useless data.");
                        }
                        break;
                    case DisableType.VMAP:
                    {
                        MapRecord mapEntry = CliDB.MapStorage.LookupByKey(entry);
                        if (mapEntry == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Map entry {entry} from `disables` doesn't exist in dbc, skipped.");
                            continue;
                        }
                        switch (mapEntry.InstanceType)
                        {
                            case MapTypes.Common:
                                if (flags.HasFlag(DisableFlags.VmapAreaFlag))
                                    Log.outInfo(LogFilter.Server, $"Areaflag disabled for world map {entry}.");
                                if (flags.HasFlag(DisableFlags.VmapLiquidStatus))
                                    Log.outInfo(LogFilter.Server, $"Liquid status disabled for world map {entry}.");
                                break;
                            case MapTypes.Instance:
                            case MapTypes.Raid:
                                if (flags.HasFlag(DisableFlags.VmapHeight))
                                    Log.outInfo(LogFilter.Server, $"Height disabled for instance map {entry}.");
                                if (flags.HasFlag(DisableFlags.VmapLOS))
                                    Log.outInfo(LogFilter.Server, $"LoS disabled for instance map {entry}.");
                                break;
                            case MapTypes.Battleground:
                                if (flags.HasFlag(DisableFlags.VmapHeight))
                                    Log.outInfo(LogFilter.Server, $"Height disabled for Battlegroundmap {entry}.");
                                if (flags.HasFlag(DisableFlags.VmapLOS))
                                    Log.outInfo(LogFilter.Server, $"LoS disabled for Battlegroundmap {entry}.");
                                break;
                            case MapTypes.Arena:
                                if (flags.HasFlag(DisableFlags.VmapHeight))
                                    Log.outInfo(LogFilter.Server, $"Height disabled for arena map {entry}.");
                                if (flags.HasFlag(DisableFlags.VmapLOS))
                                    Log.outInfo(LogFilter.Server, $"LoS disabled for arena map {entry}.");
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                    case DisableType.MMAP:
                    {
                        MapRecord mapEntry = CliDB.MapStorage.LookupByKey(entry);
                        if (mapEntry == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Map entry {entry} from `disables` doesn't exist in dbc, skipped.");
                            continue;
                        }
                        switch (mapEntry.InstanceType)
                        {
                            case MapTypes.Common:
                                Log.outInfo(LogFilter.Server, $"Pathfinding disabled for world map {entry}.");
                                break;
                            case MapTypes.Instance:
                            case MapTypes.Raid:
                                Log.outInfo(LogFilter.Server, $"Pathfinding disabled for instance map {entry}.");
                                break;
                            case MapTypes.Battleground:
                                Log.outInfo(LogFilter.Server, $"Pathfinding disabled for Battlegroundmap {entry}.");
                                break;
                            case MapTypes.Arena:
                                Log.outInfo(LogFilter.Server, $"Pathfinding disabled for arena map {entry}.");
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                    default:
                        break;
                }
                if (!m_DisableMap.ContainsKey(type))
                    m_DisableMap[type] = new Dictionary<int, DisableData>();

                m_DisableMap[type].Add(entry, data);
                ++total_count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {total_count} disables in {Time.Diff(oldMSTime)} ms.");
        }

        public void CheckQuestDisables()
        {
            if (!m_DisableMap.ContainsKey(DisableType.Quest) || m_DisableMap[DisableType.Quest].Count == 0)
            {
                Log.outInfo(LogFilter.ServerLoading, "Checked 0 quest disables.");
                return;
            }

            RelativeTime oldMSTime = Time.NowRelative;

            // check only quests, rest already done at startup
            foreach (var pair in m_DisableMap[DisableType.Quest])
            {
                int entry = pair.Key;
                if (Global.ObjectMgr.GetQuestTemplate(entry) == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Quest entry {entry} from `disables` doesn't exist, skipped.");
                    m_DisableMap[DisableType.Quest].Remove(entry);
                    continue;
                }
                if (pair.Value.flags != 0)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Disable flags specified for quest {entry}, useless data.");
                }
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Checked {m_DisableMap[DisableType.Quest].Count} quest disables in {Time.Diff(oldMSTime)} ms.");
        }

        public bool IsDisabledFor(DisableType type, int entry, WorldObject refe, DisableFlags flags = 0)
        {
            Cypher.Assert(type < DisableType.Max);
            if (!m_DisableMap.ContainsKey(type) || m_DisableMap[type].Empty())
                return false;

            var data = m_DisableMap[type].LookupByKey(entry);
            if (data == null)    // not disabled
                return false;

            switch (type)
            {
                case DisableType.Spell:
                {
                    DisableFlags spellFlags = (DisableFlags)data.flags;
                    if (refe != null)
                    {
                        if ((refe.IsPlayer() && spellFlags.HasFlag(DisableFlags.SpellPlayer)) ||
                        (refe.IsCreature() && (spellFlags.HasFlag(DisableFlags.SpellCreature) || (refe.ToUnit().IsPet() && spellFlags.HasFlag(DisableFlags.SpellPet)))) ||
                        (refe.IsGameObject() && spellFlags.HasFlag(DisableFlags.SpellGameobject)))
                        {
                            if (spellFlags.HasAnyFlag(DisableFlags.SpellArenas | DisableFlags.SpellBattleGrounds))
                            {
                                var map = refe.GetMap();
                                if (map != null)
                                {
                                    if (spellFlags.HasFlag(DisableFlags.SpellArenas) && map.IsBattleArena())
                                        return true;                                    // Current map is Arena and this spell is disabled here

                                    if (spellFlags.HasFlag(DisableFlags.SpellBattleGrounds) && map.IsBattleground())
                                        return true;                                    // Current map is a Battleground and this spell is disabled here
                                }
                            }

                            if (spellFlags.HasFlag(DisableFlags.SpellMap))
                            {
                                List<int> mapIds = data.param0;
                                if (mapIds.Contains(refe.GetMapId()))
                                    return true;                                        // Spell is disabled on current map

                                if (!spellFlags.HasFlag(DisableFlags.SpellArea))
                                    return false;                                       // Spell is disabled on another map, but not this one, return false

                                // Spell is disabled in an area, but not explicitly our current mapId. Continue processing.
                            }

                            if (spellFlags.HasFlag(DisableFlags.SpellArea))
                            {
                                var areaIds = data.param1;
                                if (areaIds.Contains(refe.GetAreaId()))
                                    return true;                                        // Spell is disabled in this area
                                return false;                                           // Spell is disabled in another area, but not this one, return false
                            }
                            else
                                return true;                                            // Spell disabled for all maps
                        }

                        return false;
                    }
                    else if (spellFlags.HasFlag(DisableFlags.SpellDeprecatedSpell))    // call not from spellcast
                        return true;
                    else if (flags.HasAnyFlag(DisableFlags.SpellLOS))
                        return spellFlags.HasFlag(DisableFlags.SpellLOS);

                    break;
                }
                case DisableType.Map:
                case DisableType.LFGMap:
                    Player player = refe.ToPlayer();
                    if (player != null)
                    {
                        MapRecord mapEntry = CliDB.MapStorage.LookupByKey(entry);
                        if (mapEntry.IsDungeon)
                        {
                            DisableFlags disabledModes = (DisableFlags)data.flags;
                            Difficulty targetDifficulty = player.GetDifficultyID(mapEntry);
                            Global.DB2Mgr.GetDownscaledMapDifficultyData(entry, ref targetDifficulty);
                            switch (targetDifficulty)
                            {
                                case Difficulty.Normal:
                                    return disabledModes.HasFlag(DisableFlags.DungeonStatusNormal);
                                case Difficulty.Heroic:
                                    return disabledModes.HasFlag(DisableFlags.DungeonStatusHeroic);
                                case Difficulty.Raid10HC:
                                    return disabledModes.HasFlag(DisableFlags.DungeonStatusHeroic10Man);
                                case Difficulty.Raid25HC:
                                    return disabledModes.HasFlag(DisableFlags.DungeonStatusHeroic25Man);
                                default:
                                    return false;
                            }
                        }
                        else if (mapEntry.InstanceType == MapTypes.Common)
                            return true;
                    }
                    return false;
                case DisableType.Quest:
                    return true;
                case DisableType.Battleground:
                case DisableType.OutdoorPVP:
                case DisableType.Criteria:
                case DisableType.MMAP:
                    return true;
                case DisableType.VMAP:
                    return flags.HasAnyFlag((DisableFlags)data.flags);
            }

            return false;
        }

        public bool IsVMAPDisabledFor(int entry, DisableFlags flags)
        {
            return IsDisabledFor(DisableType.VMAP, entry, null, flags);
        }

        public bool IsPathfindingEnabled(int mapId)
        {
            return WorldConfig.Values[WorldCfg.EnableMmaps].Bool && !Global.DisableMgr.IsDisabledFor(DisableType.MMAP, mapId, null);
        }
    }

    public enum DisableType
    {
        Spell = 0,
        Quest = 1,
        Map = 2,
        Battleground = 3,
        Criteria = 4,
        OutdoorPVP = 5,
        VMAP = 6,
        MMAP = 7,
        LFGMap = 8,
        Max = 9
    }

    [Flags]
    public enum DisableFlags
    {
        SpellPlayer = 0x01,
        SpellCreature = 0x02,
        SpellPet = 0x04,
        SpellDeprecatedSpell = 0x08,
        SpellMap = 0x10,
        SpellArea = 0x20,
        SpellLOS = 0x40,
        SpellGameobject = 0x80,
        SpellArenas = 0x100,
        SpellBattleGrounds = 0x200,
        MaxSpell = SpellPlayer | SpellCreature | SpellPet | SpellDeprecatedSpell | SpellMap | SpellArea | SpellLOS | SpellGameobject | SpellArenas | SpellBattleGrounds,

        VmapAreaFlag = 0x01,
        VmapHeight = 0x02,
        VmapLOS = 0x04,
        VmapLiquidStatus = 0x08,

        MMapPathFinding = 0x00,

        DungeonStatusNormal = 0x01,
        DungeonStatusHeroic = 0x02,

        DungeonStatusNormal10Man = 0x01,
        DungeonStatusNormal25Man = 0x02,
        DungeonStatusHeroic10Man = 0x04,
        DungeonStatusHeroic25Man = 0x08
    }
}