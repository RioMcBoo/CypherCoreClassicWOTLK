// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;
using Game.Maps;
using System.Collections.Generic;
using Framework.Constants;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackwingLair
{
    struct DataTypes
    {
        // Encounter States/Boss GUIDs
        public const int RazorgoreTheUntamed = 0;
        public const int VaelastrazTheCorrupt = 1;
        public const int BroodlordLashlayer = 2;
        public const int Firemaw = 3;
        public const int Ebonroc = 4;
        public const int Flamegor = 5;
        public const int Chromaggus = 6;
        public const int Nefarian = 7;

        // Additional Data
        public const int LordVictorNefarius = 8;

        // Doors
        public const int GoChromaggusDoor = 9;
    }

    struct BWLCreatureIds
    {
        public const int Razorgore = 12435;
        public const int BlackwingDragon = 12422;
        public const int BlackwingTaskmaster = 12458;
        public const int BlackwingLegionaire = 12416;
        public const int BlackwingWarlock = 12459;
        public const int Vaelastraz = 13020;
        public const int Broodlord = 12017;
        public const int Firemaw = 11983;
        public const int Ebonroc = 14601;
        public const int Flamegor = 11981;
        public const int Chromaggus = 14020;
        public const int VictorNefarius = 10162;
        public const int Nefarian = 11583;
    }

    struct BWLGameObjectIds
    {
        public const int BlackDragonEgg = 177807;
        public const int PortcullisRazorgore = 176965;
        public const int PortcullisVaelastrasz = 179364;
        public const int PortcullisBroodlord = 179365;
        public const int PortcullisThreedragons = 179115;
        public const int PortcullisChromaggus = 179117; //Door after you kill him, not the one for his room
        public const int ChromaggusLever = 179148;
        public const int ChromaggusDoor = 179116;
        public const int PortcullisNefarian = 176966;
        public const int SuppressionDevice = 179784;
    }

    struct EventIds
    {
        public const int RazorSpawn = 1;
        public const int RazorPhaseTwo = 2;
        public const int RespawnNefarius = 3;
    }

    struct BWLMisc
    {
        public const int EncounterCount = 8;

        // Razorgore Egg Event
        public const int ActionPhaseTwo = 1;
        public const int DataEggEvent = 2;

        public static DoorData[] doorData =
        [
            new DoorData(BWLGameObjectIds.PortcullisRazorgore, DataTypes.RazorgoreTheUntamed, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(BWLGameObjectIds.PortcullisVaelastrasz, DataTypes.VaelastrazTheCorrupt, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(BWLGameObjectIds.PortcullisBroodlord, DataTypes.BroodlordLashlayer, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(BWLGameObjectIds.PortcullisThreedragons, DataTypes.Firemaw, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(BWLGameObjectIds.PortcullisThreedragons, DataTypes.Ebonroc, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(BWLGameObjectIds.PortcullisThreedragons, DataTypes.Flamegor, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(BWLGameObjectIds.PortcullisChromaggus, DataTypes.Chromaggus, EncounterDoorBehavior.OpenWhenDone),
            new DoorData(BWLGameObjectIds.PortcullisNefarian, DataTypes.Nefarian, EncounterDoorBehavior.OpenWhenNotInProgress),
        ];

        public static ObjectData[] creatureData =
        [
            new ObjectData(BWLCreatureIds.Razorgore, DataTypes.RazorgoreTheUntamed),
            new ObjectData(BWLCreatureIds.Vaelastraz, DataTypes.VaelastrazTheCorrupt),
            new ObjectData(BWLCreatureIds.Broodlord, DataTypes.BroodlordLashlayer),
            new ObjectData(BWLCreatureIds.Firemaw, DataTypes.Firemaw),
            new ObjectData(BWLCreatureIds.Ebonroc, DataTypes.Ebonroc),
            new ObjectData(BWLCreatureIds.Flamegor, DataTypes.Flamegor),
            new ObjectData(BWLCreatureIds.Chromaggus, DataTypes.Chromaggus),
            new ObjectData(BWLCreatureIds.Nefarian, DataTypes.Nefarian),
            new ObjectData(BWLCreatureIds.VictorNefarius, DataTypes.LordVictorNefarius),
        ];

        public static ObjectData[] gameObjectData =
        [
            new ObjectData(BWLGameObjectIds.ChromaggusDoor, DataTypes.GoChromaggusDoor),
        ];

        public static Position[] SummonPosition =
        [
            new Position(-7661.207520f, -1043.268188f, 407.199554f, 6.280452f),
            new Position(-7644.145020f, -1065.628052f, 407.204956f, 0.501492f),
            new Position(-7624.260742f, -1095.196899f, 407.205017f, 0.544694f),
            new Position(-7608.501953f, -1116.077271f, 407.199921f, 0.816443f),
            new Position(-7531.841797f, -1063.765381f, 407.199615f, 2.874187f),
            new Position(-7547.319336f, -1040.971924f, 407.205078f, 3.789175f),
            new Position(-7568.547852f, -1013.112488f, 407.204926f, 3.773467f),
            new Position(-7584.175781f, -989.6691289f, 407.199585f, 4.527447f),
        ];

        public static int[] Entry = [12422, 12458, 12416, 12420, 12459];
    }

    [Script]
    class instance_blackwing_lair : InstanceMapScript
    {
        static DungeonEncounterData[] encounters =
        [
            new DungeonEncounterData(DataTypes.RazorgoreTheUntamed, 610),
            new DungeonEncounterData(DataTypes.VaelastrazTheCorrupt, 611),
            new DungeonEncounterData(DataTypes.BroodlordLashlayer, 612),
            new DungeonEncounterData(DataTypes.Firemaw, 613),
            new DungeonEncounterData(DataTypes.Ebonroc, 614),
            new DungeonEncounterData(DataTypes.Flamegor, 615),
            new DungeonEncounterData(DataTypes.Chromaggus, 616),
            new DungeonEncounterData(DataTypes.Nefarian, 617)
        ];

        public instance_blackwing_lair() : base(nameof(instance_blackwing_lair), 469) { }

        class instance_blackwing_lair_InstanceMapScript : InstanceScript
        {
            // Razorgore
            byte EggCount;
            int EggEvent;
            List<ObjectGuid> EggList = new();

            public instance_blackwing_lair_InstanceMapScript(InstanceMap map) : base(map)
            {
                SetHeaders("BWL");
                SetBossNumber(BWLMisc.EncounterCount);
                LoadDoorData(BWLMisc.doorData);
                LoadObjectData(BWLMisc.creatureData, BWLMisc.gameObjectData);
                LoadDungeonEncounterData(encounters);

                // Razorgore
                EggCount = 0;
                EggEvent = 0;
            }

            public override void OnCreatureCreate(Creature creature)
            {
                base.OnCreatureCreate(creature);

                switch (creature.GetEntry())
                {
                    case BWLCreatureIds.BlackwingDragon:
                    case BWLCreatureIds.BlackwingTaskmaster:
                    case BWLCreatureIds.BlackwingLegionaire:
                    case BWLCreatureIds.BlackwingWarlock:
                        Creature razor = GetCreature(DataTypes.RazorgoreTheUntamed);
                        if (razor != null)
                        {
                            CreatureAI razorAI = razor.GetAI();
                            if (razorAI != null)
                                razorAI.JustSummoned(creature);
                        }
                        break;
                    default:
                        break;
                }
            }

            public override int GetGameObjectEntry(long spawnId, int entry)
            {
                if (entry == BWLGameObjectIds.BlackDragonEgg && GetBossState(DataTypes.Firemaw) == EncounterState.Done)
                    return 0;
                return entry;
            }

            public override void OnGameObjectCreate(GameObject go)
            {
                base.OnGameObjectCreate(go);

                switch (go.GetEntry())
                {
                    case BWLGameObjectIds.BlackDragonEgg:
                        EggList.Add(go.GetGUID());
                        break;
                    default:
                        break;
                }
            }

            public override void OnGameObjectRemove(GameObject go)
            {
                base.OnGameObjectRemove(go);

                if (go.GetEntry() == BWLGameObjectIds.BlackDragonEgg)
                    EggList.Remove(go.GetGUID());
            }

            public override bool CheckRequiredBosses(int bossId, Player player = null)
            {
                if (_SkipCheckRequiredBosses(player))
                    return true;

                switch (bossId)
                {
                    case DataTypes.BroodlordLashlayer:
                        if (GetBossState(DataTypes.VaelastrazTheCorrupt) != EncounterState.Done)
                            return false;
                        break;
                    case DataTypes.Firemaw:
                    case DataTypes.Ebonroc:
                    case DataTypes.Flamegor:
                        if (GetBossState(DataTypes.BroodlordLashlayer) != EncounterState.Done)
                            return false;
                        break;
                    case DataTypes.Chromaggus:
                        if (GetBossState(DataTypes.Firemaw) != EncounterState.Done
                            || GetBossState(DataTypes.Ebonroc) != EncounterState.Done
                            || GetBossState(DataTypes.Flamegor) != EncounterState.Done)
                            return false;
                        break;
                    default:
                        break;
                }

                return true;
            }

            public override bool SetBossState(int type, EncounterState state)
            {
                if (!base.SetBossState(type, state))
                    return false;

                switch (type)
                {
                    case DataTypes.RazorgoreTheUntamed:
                        if (state == EncounterState.Done)
                        {
                            foreach (var guid in EggList)
                            {
                                GameObject egg = instance.GetGameObject(guid);
                                if (egg != null)
                                    egg.SetLootState(LootState.JustDeactivated);
                            }
                        }
                        SetData(BWLMisc.DataEggEvent, (int)EncounterState.NotStarted);
                        break;
                    case DataTypes.Nefarian:
                        switch (state)
                        {
                            case EncounterState.NotStarted:
                                Creature nefarian = GetCreature(DataTypes.Nefarian);
                                if (nefarian != null)
                                    nefarian.DespawnOrUnsummon();
                                break;
                            case EncounterState.Fail:
                                _events.ScheduleEvent(EventIds.RespawnNefarius, (Minutes)15);
                                SetBossState(DataTypes.Nefarian, EncounterState.NotStarted);
                                break;
                            default:
                                break;
                        }
                        break;
                }
                return true;
            }

            public override void SetData(int type, int data)
            {
                if (type == BWLMisc.DataEggEvent)
                {
                    switch ((EncounterState)data)
                    {
                        case EncounterState.InProgress:
                            _events.ScheduleEvent(EventIds.RazorSpawn, (Seconds)45);
                            EggEvent = data;
                            EggCount = 0;
                            break;
                        case EncounterState.NotStarted:
                            _events.CancelEvent(EventIds.RazorSpawn);
                            EggEvent = data;
                            EggCount = 0;
                            break;
                        case EncounterState.Special:
                            if (++EggCount == 15)
                            {
                                Creature razor = GetCreature(DataTypes.RazorgoreTheUntamed);
                                if (razor != null)
                                {
                                    SetData(BWLMisc.DataEggEvent, (int)EncounterState.Done);
                                    razor.RemoveAurasDueToSpell(42013); // MindControl
                                    DoRemoveAurasDueToSpellOnPlayers(42013, true, true);
                                }
                                _events.ScheduleEvent(EventIds.RazorPhaseTwo, (Seconds)1);
                                _events.CancelEvent(EventIds.RazorSpawn);
                            }
                            if (EggEvent == (uint)EncounterState.NotStarted)
                                SetData(BWLMisc.DataEggEvent, (int)EncounterState.InProgress);
                            break;
                    }
                }
            }

            public override void OnUnitDeath(Unit unit)
            {
                //! Hack, needed because of buggy CreatureAI after charm
                if (unit.GetEntry() == BWLCreatureIds.Razorgore && GetBossState(DataTypes.RazorgoreTheUntamed) != EncounterState.Done)
                    SetBossState(DataTypes.RazorgoreTheUntamed, EncounterState.Done);
            }

            public override void Update(TimeSpan diff)
            {
                if (_events.Empty())
                    return;

                _events.Update(diff);

                _events.ExecuteEvents(eventId =>
                {
                    switch (eventId)
                    {
                        case EventIds.RazorSpawn:
                            for (uint i = RandomHelper.URand(2, 5); i > 0; --i)
                            {
                                Creature summon = instance.SummonCreature(BWLMisc.Entry[RandomHelper.URand(0, 4)], BWLMisc.SummonPosition[RandomHelper.URand(0, 7)]);
                                if (summon != null)
                                    summon.GetAI().DoZoneInCombat();
                            }
                            _events.ScheduleEvent(EventIds.RazorSpawn, Time.SpanFromSeconds(12), Time.SpanFromSeconds(17));
                            break;
                        case EventIds.RazorPhaseTwo:
                            _events.CancelEvent(EventIds.RazorSpawn);
                            Creature razor = GetCreature(DataTypes.RazorgoreTheUntamed);
                            if (razor != null)
                                razor.GetAI().DoAction(BWLMisc.ActionPhaseTwo);
                            break;
                        case EventIds.RespawnNefarius:
                            Creature nefarius = GetCreature(DataTypes.LordVictorNefarius);
                            if (nefarius != null)
                            {
                                nefarius.SetActive(true);
                                nefarius.SetFarVisible(true);
                                nefarius.Respawn();
                                nefarius.GetMotionMaster().MoveTargetedHome();
                            }
                            break;
                    }
                });
            }
        }

        public override InstanceScript GetInstanceScript(InstanceMap map)
        {
            return new instance_blackwing_lair_InstanceMapScript(map);
        }
    }
}

