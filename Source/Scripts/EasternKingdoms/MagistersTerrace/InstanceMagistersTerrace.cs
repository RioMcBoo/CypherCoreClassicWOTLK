// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using System;
using System.Collections.Generic;

namespace Scripts.EasternKingdoms.MagistersTerrace
{
    struct DataTypes
    {
        // Encounter states
        public const int SelinFireheart = 0;
        public const int Vexallus = 1;
        public const int PriestessDelrissa = 2;
        public const int KaelthasSunstrider = 3;

        // Encounter related
        public const int KaelthasIntro = 4;
        public const int DelrissaDeathCount = 5;

        // Additional data
        public const int Kalecgos = 6;
        public const int EscapeOrb = 7;
    }

    struct CreatureIds
    {
        // Bosses
        public const int KaelthasSunstrider = 24664;
        public const int SelinFireheart = 24723;
        public const int Vexallus = 24744;
        public const int PriestessDelrissa = 24560;

        // Encounter related
        // Kael'thas Sunstrider
        public const int ArcaneSphere = 24708;
        public const int FlameStrike = 24666;
        public const int Phoenix = 24674;
        public const int PhoenixEgg = 24675;

        // Selin Fireheart
        public const int FelCrystal = 24722;

        // Event related
        public const int Kalecgos = 24844;
        public const int HumanKalecgos = 24848;
        public const int CoilskarWitch = 24696;
        public const int SunbladeWarlock = 24686;
        public const int SunbladeMageGuard = 24683;
        public const int SisterOfTorment = 24697;
        public const int EthereumSmuggler = 24698;
        public const int SunbladeBloodKnight = 24684;
    }

    struct GameObjectIds
    {
        public const int AssemblyChamberDoor = 188065;
        public const int SunwellRaidGate2 = 187979;
        public const int SunwellRaidGate4 = 187770;
        public const int SunwellRaidGate5 = 187896;
        public const int AsylumDoor = 188064;
        public const int EscapeOrb = 188173;
    }

    struct MiscConst
    {
        public const int EventSpawnKalecgos = 16547;

        public const int SayKalecgosSpawn = 0;

        public const int PathKalecgosFlight = 1987520;

        public static ObjectData[] creatureData =
        [
            new ObjectData(CreatureIds.SelinFireheart, DataTypes.SelinFireheart),
            new ObjectData(CreatureIds.Vexallus, DataTypes.Vexallus),
            new ObjectData(CreatureIds.PriestessDelrissa, DataTypes.PriestessDelrissa),
            new ObjectData(CreatureIds.KaelthasSunstrider, DataTypes.KaelthasSunstrider),
            new ObjectData(CreatureIds.Kalecgos, DataTypes.Kalecgos),
            new ObjectData(CreatureIds.HumanKalecgos, DataTypes.Kalecgos),
        ];

        public static ObjectData[] gameObjectData =
        [
            new ObjectData(GameObjectIds.EscapeOrb, DataTypes.EscapeOrb),
        ];

        public static DoorData[] doorData =
        [
            new DoorData(GameObjectIds.SunwellRaidGate2, DataTypes.SelinFireheart, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(GameObjectIds.AssemblyChamberDoor, DataTypes.SelinFireheart, EncounterDoorBehavior.OpenWhenNotInProgress),
            new DoorData(GameObjectIds.SunwellRaidGate5, DataTypes.Vexallus, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(GameObjectIds.SunwellRaidGate4, DataTypes.PriestessDelrissa, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(GameObjectIds.AsylumDoor, DataTypes.KaelthasSunstrider, EncounterDoorBehavior.OpenWhenNotInProgress),
        ];

        public static Position KalecgosSpawnPos = new Position(164.3747f, -397.1197f, 2.151798f, 1.66219f);
        public static Position KaelthasTrashGroupDistanceComparisonPos = new Position(150.0f, 141.0f, -14.4f);
    }

    [Script]
    class instance_magisters_terrace : InstanceMapScript
    {
        static DungeonEncounterData[] encounters =
        [
            new DungeonEncounterData(DataTypes.SelinFireheart, 1897),
            new DungeonEncounterData(DataTypes.Vexallus, 1898),
            new DungeonEncounterData(DataTypes.PriestessDelrissa, 1895),
            new DungeonEncounterData(DataTypes.KaelthasSunstrider, 1894)
        ];
        
        public instance_magisters_terrace() : base(nameof(instance_magisters_terrace), 585) { }

        class instance_magisters_terrace_InstanceMapScript : InstanceScript
        {
            List<ObjectGuid> _kaelthasPreTrashGUIDs = new();
            byte _delrissaDeathCount;

            public instance_magisters_terrace_InstanceMapScript(InstanceMap map) : base(map)
            {
                SetHeaders("MT");
                SetBossNumber(4);
                LoadObjectData(MiscConst.creatureData, MiscConst.gameObjectData);
                LoadDoorData(MiscConst.doorData);
                LoadDungeonEncounterData(encounters);
            }

            public override int GetData(int type)
            {
                switch (type)
                {
                    case DataTypes.DelrissaDeathCount:
                        return _delrissaDeathCount;
                    default:
                        break;
                }
                return 0;
            }

            public override void SetData(int type, int data)
            {
                switch (type)
                {
                    case DataTypes.DelrissaDeathCount:
                        if (data == (uint)EncounterState.Special)
                            _delrissaDeathCount++;
                        else
                            _delrissaDeathCount = 0;
                        break;
                    default:
                        break;
                }
            }

            public override void OnCreatureCreate(Creature creature)
            {
                base.OnCreatureCreate(creature);

                switch (creature.GetEntry())
                {
                    case CreatureIds.CoilskarWitch:
                    case CreatureIds.SunbladeWarlock:
                    case CreatureIds.SunbladeMageGuard:
                    case CreatureIds.SisterOfTorment:
                    case CreatureIds.EthereumSmuggler:
                    case CreatureIds.SunbladeBloodKnight:
                        if (creature.GetDistance(MiscConst.KaelthasTrashGroupDistanceComparisonPos) < 10.0f)
                            _kaelthasPreTrashGUIDs.Add(creature.GetGUID());
                        break;
                    default:
                        break;
                }
            }

            public override void OnUnitDeath(Unit unit)
            {
                if (!unit.IsCreature())
                    return;

                switch (unit.GetEntry())
                {
                    case CreatureIds.CoilskarWitch:
                    case CreatureIds.SunbladeWarlock:
                    case CreatureIds.SunbladeMageGuard:
                    case CreatureIds.SisterOfTorment:
                    case CreatureIds.EthereumSmuggler:
                    case CreatureIds.SunbladeBloodKnight:
                        if (_kaelthasPreTrashGUIDs.Contains(unit.GetGUID()))
                        {
                            _kaelthasPreTrashGUIDs.Remove(unit.GetGUID());
                            if (_kaelthasPreTrashGUIDs.Count == 0)
                            {
                                Creature kaelthas = GetCreature(DataTypes.KaelthasSunstrider);
                                if (kaelthas != null)
                                    kaelthas.GetAI().SetData(DataTypes.KaelthasIntro, (int)EncounterState.InProgress);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }

            public override void OnGameObjectCreate(GameObject go)
            {
                base.OnGameObjectCreate(go);

                switch (go.GetEntry())
                {
                    case GameObjectIds.EscapeOrb:
                        if (GetBossState(DataTypes.KaelthasSunstrider) == EncounterState.Done)
                            go.RemoveFlag(GameObjectFlags.NotSelectable);
                        break;
                    default:
                        break;
                }
            }

            public override void ProcessEvent(WorldObject obj, int eventId, WorldObject invoker)
            {
                if (eventId == MiscConst.EventSpawnKalecgos)
                {
                    if (GetCreature(DataTypes.Kalecgos) == null && _events.Empty())
                        _events.ScheduleEvent(MiscConst.EventSpawnKalecgos, (Minutes)1);
                }
            }

            public override void Update(TimeSpan diff)
            {
                _events.Update(diff);

                if (_events.ExecuteEvent() == MiscConst.EventSpawnKalecgos)
                {
                    Creature kalecgos = instance.SummonCreature(CreatureIds.Kalecgos, MiscConst.KalecgosSpawnPos);
                    if (kalecgos != null)
                    {
                        kalecgos.GetMotionMaster().MovePath(MiscConst.PathKalecgosFlight, false);
                        kalecgos.GetAI().Talk(MiscConst.SayKalecgosSpawn);
                    }
                }
            }

            public override bool SetBossState(int type, EncounterState state)
            {
                if (!base.SetBossState(type, state))
                    return false;

                switch (type)
                {
                    case DataTypes.PriestessDelrissa:
                        if (state == EncounterState.InProgress)
                            _delrissaDeathCount = 0;
                        break;
                    case DataTypes.KaelthasSunstrider:
                        if (state == EncounterState.Done)
                        {
                            GameObject orb = GetGameObject(DataTypes.EscapeOrb);
                            if (orb != null)
                                orb.RemoveFlag(GameObjectFlags.NotSelectable);
                        }
                        break;
                    default:
                        break;
                }
                return true;
            }
        }

        public override InstanceScript GetInstanceScript(InstanceMap map)
        {
            return new instance_magisters_terrace_InstanceMapScript(map);
        }
    }
}

