// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.Karazhan
{
    struct DataTypes
    {
        public const int Attumen = 0;
        public const int Moroes = 1;
        public const int MaidenOfVirtue = 2;
        public const int OptionalBoss = 3;
        public const int OperaPerformance = 4;
        public const int Curator = 5;
        public const int Aran = 6;
        public const int Terestian = 7;
        public const int Netherspite = 8;
        public const int Chess = 9;
        public const int Malchezzar = 10;
        public const int Nightbane = 11;

        public const int OperaOzDeathcount = 14;

        public const int Kilrek = 15;
        public const int GoCurtains = 18;
        public const int GoStagedoorleft = 19;
        public const int GoStagedoorright = 20;
        public const int GoLibraryDoor = 21;
        public const int GoMassiveDoor = 22;
        public const int GoNetherDoor = 23;
        public const int GoGameDoor = 24;
        public const int GoGameExitDoor = 25;

        public const int ImageOfMedivh = 26;
        public const int MastersTerraceDoor1 = 27;
        public const int MastersTerraceDoor2 = 28;
        public const int GoSideEntranceDoor = 29;
        public const int GoBlackenedUrn = 30;
    }

    struct CreatureIds
    {
        public const int HyakissTheLurker = 16179;
        public const int RokadTheRavager = 16181;
        public const int ShadikithTheGlider = 16180;
        public const int TerestianIllhoof = 15688;
        public const int Moroes = 15687;
        public const int Nightbane = 17225;
        public const int AttumenUnmounted = 15550;
        public const int AttumenMounted = 16152;
        public const int Midnight = 16151;

        // Trash
        public const int ColdmistWidow = 16171;
        public const int ColdmistStalker = 16170;
        public const int Shadowbat = 16173;
        public const int VampiricShadowbat = 16175;
        public const int GreaterShadowbat = 16174;
        public const int PhaseHound = 16178;
        public const int Dreadbeast = 16177;
        public const int Shadowbeast = 16176;
        public const int Kilrek = 17229;
    }

    struct GameObjectIds
    {
        public const int StageCurtain = 183932;
        public const int StageDoorLeft = 184278;
        public const int StageDoorRight = 184279;
        public const int PrivateLibraryDoor = 184517;
        public const int MassiveDoor = 185521;
        public const int GamesmanHallDoor = 184276;
        public const int GamesmanHallExitDoor = 184277;
        public const int NetherspaceDoor = 185134;
        public const int MastersTerraceDoor = 184274;
        public const int MastersTerraceDoor2 = 184280;
        public const int SideEntranceDoor = 184275;
        public const int DustCoveredChest = 185119;
        public const int BlackenedUrn = 194092;
    }

    enum KZMisc
    {
        OptionalBossRequiredDeathCount = 50
    }

    [Script]
    class instance_karazhan : InstanceMapScript
    {
        public static Position[] OptionalSpawn =
        [
            new Position(-10960.981445f, -1940.138428f, 46.178097f, 4.12f), // Hyakiss the Lurker
            new Position(-10945.769531f, -2040.153320f, 49.474438f, 0.077f), // Shadikith the Glider
            new Position(-10899.903320f, -2085.573730f, 49.474449f, 1.38f)  // Rokad the Ravager
        ];

        static DungeonEncounterData[] encounters =
        [
            new DungeonEncounterData(DataTypes.Attumen, 652),
            new DungeonEncounterData(DataTypes.Moroes, 653),
            new DungeonEncounterData(DataTypes.MaidenOfVirtue, 654),
            new DungeonEncounterData(DataTypes.OperaPerformance, 655),
            new DungeonEncounterData(DataTypes.Curator, 656),
            new DungeonEncounterData(DataTypes.Aran, 658),
            new DungeonEncounterData(DataTypes.Terestian, 657),
            new DungeonEncounterData(DataTypes.Netherspite, 659),
            new DungeonEncounterData(DataTypes.Chess, 660),
            new DungeonEncounterData(DataTypes.Malchezzar, 661),
            new DungeonEncounterData(DataTypes.Nightbane, 662)
        ];

        public instance_karazhan() : base(nameof(instance_karazhan), 532) { }

        class instance_karazhan_InstanceMapScript : InstanceScript
        {
            int OperaEvent;
            int OzDeathCount;
            int OptionalBossCount;
            ObjectGuid CurtainGUID;
            ObjectGuid StageDoorLeftGUID;
            ObjectGuid StageDoorRightGUID;
            ObjectGuid KilrekGUID;
            ObjectGuid TerestianGUID;
            ObjectGuid MoroesGUID;
            ObjectGuid NightbaneGUID;
            ObjectGuid LibraryDoor;                 // Door at Shade of Aran
            ObjectGuid MassiveDoor;                 // Door at Netherspite
            ObjectGuid SideEntranceDoor;            // Side Entrance
            ObjectGuid GamesmansDoor;               // Door before Chess
            ObjectGuid GamesmansExitDoor;           // Door after Chess
            ObjectGuid NetherspaceDoor;             // Door at Malchezaar
            ObjectGuid[] MastersTerraceDoor = new ObjectGuid[2];
            ObjectGuid ImageGUID;
            ObjectGuid DustCoveredChest;
            ObjectGuid BlackenedUrnGUID;

            public instance_karazhan_InstanceMapScript(InstanceMap map) : base(map)
            {
                SetHeaders("KZ");
                SetBossNumber(12);
                LoadDungeonEncounterData(encounters);

                // 1 - Oz, 2 - Hood, 3 - Raj, this never gets altered.
                OperaEvent = RandomHelper.IRand(1, 3);
                OzDeathCount = 0;
                OptionalBossCount = 0;
            }

            public override void OnCreatureCreate(Creature creature)
            {
                switch (creature.GetEntry())
                {
                    case CreatureIds.Kilrek:
                        KilrekGUID = creature.GetGUID();
                        break;
                    case CreatureIds.TerestianIllhoof:
                        TerestianGUID = creature.GetGUID();
                        break;
                    case CreatureIds.Moroes:
                        MoroesGUID = creature.GetGUID();
                        break;
                    case CreatureIds.Nightbane:
                        NightbaneGUID = creature.GetGUID();
                        break;
                    default:
                        break;
                }
            }

            public override void OnUnitDeath(Unit unit)
            {
                Creature creature = unit.ToCreature();
                if (creature == null)
                    return;

                switch (creature.GetEntry())
                {
                    case CreatureIds.ColdmistWidow:
                    case CreatureIds.ColdmistStalker:
                    case CreatureIds.Shadowbat:
                    case CreatureIds.VampiricShadowbat:
                    case CreatureIds.GreaterShadowbat:
                    case CreatureIds.PhaseHound:
                    case CreatureIds.Dreadbeast:
                    case CreatureIds.Shadowbeast:
                        if (GetBossState(DataTypes.OptionalBoss) == EncounterState.ToBeDecided)
                        {
                            ++OptionalBossCount;
                            if (OptionalBossCount == (uint)KZMisc.OptionalBossRequiredDeathCount)
                            {
                                switch (RandomHelper.URand(CreatureIds.HyakissTheLurker, CreatureIds.RokadTheRavager))
                                {
                                    case CreatureIds.HyakissTheLurker:
                                        instance.SummonCreature(CreatureIds.HyakissTheLurker, OptionalSpawn[0]);
                                        break;
                                    case CreatureIds.ShadikithTheGlider:
                                        instance.SummonCreature(CreatureIds.ShadikithTheGlider, OptionalSpawn[1]);
                                        break;
                                    case CreatureIds.RokadTheRavager:
                                        instance.SummonCreature(CreatureIds.RokadTheRavager, OptionalSpawn[2]);
                                        break;
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
            }

            public override void SetData(int type, int data)
            {
                switch (type)
                {
                    case DataTypes.OperaOzDeathcount:
                        if (data == (int)EncounterState.Special)
                            ++OzDeathCount;
                        else if (data == (int)EncounterState.InProgress)
                            OzDeathCount = 0;
                        break;
                }
            }

            public override bool SetBossState(int type, EncounterState state)
            {
                if (!base.SetBossState(type, state))
                    return false;

                switch (type)
                {
                    case DataTypes.OperaPerformance:
                        if (state == EncounterState.Done)
                        {
                            HandleGameObject(StageDoorLeftGUID, true);
                            HandleGameObject(StageDoorRightGUID, true);
                            GameObject sideEntrance = instance.GetGameObject(SideEntranceDoor);
                            if (sideEntrance != null)
                                sideEntrance.RemoveFlag(GameObjectFlags.Locked);
                        }
                        break;
                    case DataTypes.Chess:
                        if (state == EncounterState.Done)
                            DoRespawnGameObject(DustCoveredChest, (Hours)24);
                        break;
                    default:
                        break;
                }

                return true;
            }

            public override void SetGuidData(int type, ObjectGuid data)
            {
                if (type == DataTypes.ImageOfMedivh)
                    ImageGUID = data;
            }

            public override void OnGameObjectCreate(GameObject go)
            {
                switch (go.GetEntry())
                {
                    case GameObjectIds.StageCurtain:
                        CurtainGUID = go.GetGUID();
                        break;
                    case GameObjectIds.StageDoorLeft:
                        StageDoorLeftGUID = go.GetGUID();
                        if (GetBossState(DataTypes.OperaPerformance) == EncounterState.Done)
                            go.SetGoState(GameObjectState.Active);
                        break;
                    case GameObjectIds.StageDoorRight:
                        StageDoorRightGUID = go.GetGUID();
                        if (GetBossState(DataTypes.OperaPerformance) == EncounterState.Done)
                            go.SetGoState(GameObjectState.Active);
                        break;
                    case GameObjectIds.PrivateLibraryDoor:
                        LibraryDoor = go.GetGUID();
                        break;
                    case GameObjectIds.MassiveDoor:
                        MassiveDoor = go.GetGUID();
                        break;
                    case GameObjectIds.GamesmanHallDoor:
                        GamesmansDoor = go.GetGUID();
                        break;
                    case GameObjectIds.GamesmanHallExitDoor:
                        GamesmansExitDoor = go.GetGUID();
                        break;
                    case GameObjectIds.NetherspaceDoor:
                        NetherspaceDoor = go.GetGUID();
                        break;
                    case GameObjectIds.MastersTerraceDoor:
                        MastersTerraceDoor[0] = go.GetGUID();
                        break;
                    case GameObjectIds.MastersTerraceDoor2:
                        MastersTerraceDoor[1] = go.GetGUID();
                        break;
                    case GameObjectIds.SideEntranceDoor:
                        SideEntranceDoor = go.GetGUID();
                        if (GetBossState(DataTypes.OperaPerformance) == EncounterState.Done)
                            go.SetFlag(GameObjectFlags.Locked);
                        else
                            go.RemoveFlag(GameObjectFlags.Locked);
                        break;
                    case GameObjectIds.DustCoveredChest:
                        DustCoveredChest = go.GetGUID();
                        break;
                    case GameObjectIds.BlackenedUrn:
                        BlackenedUrnGUID = go.GetGUID();
                        break;
                }

                switch (OperaEvent)
                {
                    /// @todo Set Object visibilities for Opera based on performance
                    case 1:
                        break;

                    case 2:
                        break;

                    case 3:
                        break;
                }
            }

            public override int GetData(int type)
            {
                switch (type)
                {
                    case DataTypes.OperaPerformance:
                        return OperaEvent;
                    case DataTypes.OperaOzDeathcount:
                        return OzDeathCount;
                }

                return 0;
            }

            public override ObjectGuid GetGuidData(int type)
            {
                switch (type)
                {
                    case DataTypes.Kilrek:
                        return KilrekGUID;
                    case DataTypes.Terestian:
                        return TerestianGUID;
                    case DataTypes.Moroes:
                        return MoroesGUID;
                    case DataTypes.Nightbane:
                        return NightbaneGUID;
                    case DataTypes.GoStagedoorleft:
                        return StageDoorLeftGUID;
                    case DataTypes.GoStagedoorright:
                        return StageDoorRightGUID;
                    case DataTypes.GoCurtains:
                        return CurtainGUID;
                    case DataTypes.GoLibraryDoor:
                        return LibraryDoor;
                    case DataTypes.GoMassiveDoor:
                        return MassiveDoor;
                    case DataTypes.GoSideEntranceDoor:
                        return SideEntranceDoor;
                    case DataTypes.GoGameDoor:
                        return GamesmansDoor;
                    case DataTypes.GoGameExitDoor:
                        return GamesmansExitDoor;
                    case DataTypes.GoNetherDoor:
                        return NetherspaceDoor;
                    case DataTypes.MastersTerraceDoor1:
                        return MastersTerraceDoor[0];
                    case DataTypes.MastersTerraceDoor2:
                        return MastersTerraceDoor[1];
                    case DataTypes.ImageOfMedivh:
                        return ImageGUID;
                    case DataTypes.GoBlackenedUrn:
                        return BlackenedUrnGUID;
                }

                return ObjectGuid.Empty;
            }
        }

        public override InstanceScript GetInstanceScript(InstanceMap map)
        {
            return new instance_karazhan_InstanceMapScript(map);
        }
    }
}