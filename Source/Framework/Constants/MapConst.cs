﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;

namespace Framework.Constants
{
    public class MapConst
    {
        public const int InvalidZone = -1;

        //Grids
        public const int MaxGrids = 64;
        public const float SizeofGrids = 533.33333f;
        public const int CenterGridCellId = (MaxCells * MaxGrids / 2);
        public const int CenterGridId = (MaxGrids / 2);
        public const float CenterGridOffset = (SizeofGrids / 2);
        public const float CenterGridCellOffset = (SizeofCells / 2);

        //Cells
        public const int MaxCells = 8;
        public const float SizeofCells = (SizeofGrids / MaxCells);
        public const int TotalCellsPerMap = (MaxGrids * MaxCells);
        public const float MapSize = (SizeofGrids * MaxGrids);
        public const float MapHalfSize = (MapSize / 2);

        public const uint MaxGroupSize = 5;
        public const uint MaxRaidSize = 40;
        public const uint MaxRaidSubGroups = MaxRaidSize / MaxGroupSize;
        public const uint TargetIconsCount = 8;
        public const uint RaidMarkersCount = 8;
        public static readonly TimeSpan ReadycheckDuration = (Seconds)35;

        //Liquid
        public const float LiquidTileSize = (533.333f / 128.0f);

        public static readonly TimeSpan MinMapUpdateDelay = (Milliseconds)1;
        public static readonly TimeSpan MinGridDelay = (Minutes)1;

        public const int MapResolution = 128;
        public const float DefaultHeightSearch = 50.0f;
        public const float InvalidHeight = -100000.0f;
        public const float MaxHeight = 100000.0f;
        public const float MaxFallDistance = 250000.0f;
        public const float GroundHeightTolerance = 0.05f;
        public const float ZOffsetFindHeight = 0.5f;
        public const float DefaultCollesionHeight = 2.03128f; // Most common value in dbc

        public const uint MapMagic = 0x5350414D; //"MAPS";
        public const uint MapVersionMagic = 10;
        public const uint MapVersionMagic2 = 0x302E3276; //"v2.0"; // Hack for some different extractors using v2.0 header
        public const uint MapAreaMagic = 0x41455241; //"AREA";
        public const uint MapHeightMagic = 0x5447484D; //"MHGT";
        public const uint MapLiquidMagic = 0x51494C4D; //"MLIQ";

        public const uint mmapMagic = 0x4D4D4150; // 'MMAP'
        public const int mmapVersion = 15;

        public const string VMapMagic = "VMAP_4.B";
        public const float VMAPInvalidHeightValue = -200000.0f;

        public const uint MaxDungeonEncountersPerBoss = 4;
    }

    public enum NewWorldReason
    {
        Normal = 16,   // Normal map change
        Seamless = 21,   // Teleport to another map without a loading screen, used for outdoor scenarios
    }

    public enum InstanceResetWarningType : byte
    {
        WarningHours = 1,                    // WARNING! %s is scheduled to reset in %d hour(s).
        WarningMin = 2,                    // WARNING! %s is scheduled to reset in %d minute(s)!
        WarningMinSoon = 3,                    // WARNING! %s is scheduled to reset in %d minute(s). Please exit the zone or you will be returned to your bind location!
        Welcome = 4,                    // Welcome to %s. This raid instance is scheduled to reset in %s.
        Expired = 5
    }

    public enum InstanceResetMethod
    {
        Manual,
        OnChangeDifficulty,
        Expire,
    }

    public enum InstanceResetResult
    {
        Success,
        NotEmpty,
        CannotReset
    }

    [Flags]
    public enum GridMapTypeMask
    {
        None = 0x00,
        Corpse = 0x01,
        Creature = 0x02,
        DynamicObject = 0x04,
        GameObject = 0x08,
        Player = 0x10,
        AreaTrigger = 0x20,
        SceneObject = 0x40,
        Conversation = 0x80,

        All = 0xFF,

        //GameObjects, Creatures(except pets), DynamicObject, Corpse(Bones), AreaTrigger, SceneObject
        AllGrid = GameObject | Creature | DynamicObject | Corpse | AreaTrigger | SceneObject | Conversation,

        //Player, Pets, Corpse(resurrectable), DynamicObject(farsight)
        AllWorld = Player | Creature | Corpse | DynamicObject
    }

    [Flags]
    public enum ZLiquidStatus
    {
        NoWater = 0x00,
        AboveWater = 0x01,
        WaterWalk = 0x02,
        InWater = 0x04,
        UnderWater = 0x08,
        OceanFloor = 0x10,

        Swimming = InWater | UnderWater,
        InContact = Swimming | WaterWalk
    }

    public enum EncounterFrameType
    {
        Engage = 0,
        Disengage = 1,
        UpdatePriority = 2,
        AddTimer = 3,
        EnableObjective = 4,
        UpdateObjective = 5,
        DisableObjective = 6,
        PhaseShiftChanged = 7
    }

    public enum EncounterState : byte
    {
        NotStarted = 0,
        InProgress = 1,
        Fail = 2,
        Done = 3,
        Special = 4,
        ToBeDecided = 5
    }

    [Flags]
    public enum EncounterStateMask : byte
    {
        NotStarted = 1 << EncounterState.NotStarted,
        InProgress = 1 << EncounterState.InProgress,
        Fail = 1 << EncounterState.Fail,
        Done = 1 << EncounterState.Done,
        Special = 1 << EncounterState.Special,
        ToBeDecided = 1 << EncounterState.ToBeDecided,

        All = NotStarted | InProgress | Fail | Done | Special | ToBeDecided,
    }

    public enum EncounterCreditType
    {
        KillCreature = 0,
        CastSpell = 1
    }

    public enum EncounterDoorBehavior
    {
        OpenWhenNotInProgress = 0, // open if encounter is not in progress
        OpenWhenDone = 1, // open if encounter is done
        OpenWhenInProgress = 2, // open if encounter is in progress, typically used for spawning places
        OpenWhenNotDone = 3, // open if encounter is not done
        Max
    }

    public enum ModelIgnoreFlags
    {
        Nothing = 0x00,
        M2 = 0x01
    }

    public enum LineOfSightChecks
    {
        Vmap = 0x1, // check static floor layout data
        Gobject = 0x2, // check dynamic game object data

        All = Vmap | Gobject
    }

    public enum SpawnObjectType : byte
    {
        Creature = 0,
        GameObject = 1,
        AreaTrigger = 2,

        NumSpawnTypesWithData,
        NumSpawnTypes
    }

    public enum SpawnObjectTypeMask
    {
        Creature = (1 << SpawnObjectType.Creature),
        GameObject = (1 << SpawnObjectType.GameObject),
        AreaTrigger = (1 << SpawnObjectType.AreaTrigger),

        WithData = (1 << SpawnObjectType.NumSpawnTypesWithData) - 1,
        All = (1 << SpawnObjectType.NumSpawnTypes) - 1
    }

    [Flags]
    public enum SpawnGroupFlags
    {
        None = 0x00,
        System = 0x01,
        CompatibilityMode = 0x02,
        ManualSpawn = 0x04,
        DynamicSpawnRate = 0x08,
        EscortQuestNpc = 0x10,
        DespawnOnConditionFailure = 0x20,

        All = (System | CompatibilityMode | ManualSpawn | DynamicSpawnRate | EscortQuestNpc | DespawnOnConditionFailure)
    }

    [Flags]
    public enum InstanceSpawnGroupFlags : byte
    {
        ActivateSpawn = 0x01,
        BlockSpawn = 0x02,
        AllianceOnly = 0x04,
        HordeOnly = 0x08,

        All = ActivateSpawn | BlockSpawn | AllianceOnly | HordeOnly
    }

    public enum AreaHeaderFlags : ushort
    {
        None = 0x0000,
        NoArea = 0x0001
    }

    public enum HeightHeaderFlags : uint
    {
        None = 0x0000,
        NoHeight = 0x0001,
        HeightAsInt16 = 0x0002,
        HeightAsInt8 = 0x0004,
        HasFlightBounds = 0x0008
    }

    public enum LiquidHeaderFlags : byte
    {
        None = 0x0000,
        NoType = 0x0001,
        NoHeight = 0x0002
    }

    public enum LiquidHeaderTypeFlags : byte
    {
        NoWater = 0x00,
        Water = 0x01,
        Ocean = 0x02,
        Magma = 0x04,
        Slime = 0x08,

        DarkWater = 0x10,

        AllLiquids = Water | Ocean | Magma | Slime
    }
}
