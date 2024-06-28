// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;
using System.Collections.Generic;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackwingLair.VictorNefarius
{
    struct SpellIds
    {
        // Victor Nefarius
        // Ubrs Spells
        public const int ChromaticChaos = 16337; // Self Cast hits 10339
        public const int VaelastraszzSpawn = 16354; // Self Cast Depawn one sec after
                                                     // Bwl Spells
        public const int Shadowbolt = 22677;
        public const int ShadowboltVolley = 22665;
        public const int ShadowCommand = 22667;
        public const int Fear = 22678;

        public const int NefariansBarrier = 22663;

        // Nefarian
        public const int ShadowflameInitial = 22992;
        public const int Shadowflame = 22539;
        public const int Bellowingroar = 22686;
        public const int Veilofshadow = 7068;
        public const int Cleave = 20691;
        public const int Taillash = 23364;

        public const int Mage = 23410;     // wild magic
        public const int Warrior = 23397;     // beserk
        public const int Druid = 23398;     // cat form
        public const int Priest = 23401;     // corrupted healing
        public const int Paladin = 23418;     // syphon blessing
        public const int Shaman = 23425;     // totems
        public const int Warlock = 23427;     // infernals
        public const int Hunter = 23436;     // bow broke
        public const int Rogue = 23414;     // Paralise
        public const int DeathKnight = 49576;      // Death Grip

        // 19484
        // 22664
        // 22674
        // 22666
    }

    struct TextIds
    {
        // Nefarius
        // Ubrs
        public const int SayChaosSpell = 9;
        public const int SaySuccess = 10;
        public const int SayFailure = 11;
        // Bwl
        public const int SayGamesbegin1 = 12;
        public const int SayGamesbegin2 = 13;
        // public const int SayVaelIntro             = 14; Not used - when he corrupts Vaelastrasz

        // Nefarian
        public const int SayRandom = 0;
        public const int SayRaiseSkeletons = 1;
        public const int SaySlay = 2;
        public const int SayDeath = 3;

        public const int SayMage = 4;
        public const int SayWarrior = 5;
        public const int SayDruid = 6;
        public const int SayPriest = 7;
        public const int SayPaladin = 8;
        public const int SayShaman = 9;
        public const int SayWarlock = 10;
        public const int SayHunter = 11;
        public const int SayRogue = 12;
        public const int SayDeathKnight = 13;

        public const int GossipId = 6045;
        public const int GossipOptionId = 0;
    }

    struct CreatureIds
    {
        public const int BronzeDrakanoid = 14263;
        public const int BlueDrakanoid = 14261;
        public const int RedDrakanoid = 14264;
        public const int GreenDrakanoid = 14262;
        public const int BlackDrakanoid = 14265;
        public const int ChromaticDrakanoid = 14302;
        public const int BoneConstruct = 14605;
        // Ubrs
        public const int Gyth = 10339;
    }

    struct GameObjectIds
    {
        public const int PortcullisActive = 164726;
        public const int PortcullisTobossrooms = 175186;
    }

    struct MiscConst
    {
        public const int NefariusPath2 = 11037368;
        public const int NefariusPath3 = 11037376;

        public static Position[] DrakeSpawnLoc = // drakonid
        [
            new Position(-7591.151855f, -1204.051880f, 476.800476f, 3.0f),
            new Position(-7514.598633f, -1150.448853f, 476.796570f, 3.0f)
        ];

        public static Position[] NefarianLoc =
        [
            new Position(-7449.763672f, -1387.816040f, 526.783691f, 3.0f), // nefarian spawn
            new Position(-7535.456543f, -1279.562500f, 476.798706f, 3.0f)  // nefarian move
        ];

        public static int[] Entry = [CreatureIds.BronzeDrakanoid, CreatureIds.BlueDrakanoid, CreatureIds.RedDrakanoid, CreatureIds.GreenDrakanoid, CreatureIds.BlackDrakanoid];
    }

    struct EventIds
    {
        // Victor Nefarius
        public const int SpawnAdd = 1;
        public const int ShadowBolt = 2;
        public const int Fear = 3;
        public const int MindControl = 4;
        // Nefarian
        public const int Shadowflame = 5;
        public const int Veilofshadow = 6;
        public const int Cleave = 7;
        public const int Taillash = 8;
        public const int Classcall = 9;
        // Ubrs
        public const int Chaos1 = 10;
        public const int Chaos2 = 11;
        public const int Path2 = 12;
        public const int Path3 = 13;
        public const int Success1 = 14;
        public const int Success2 = 15;
        public const int Success3 = 16;
    }

    [Script]
    class boss_victor_nefarius : BossAI
    {
        int SpawnedAdds;

        public boss_victor_nefarius(Creature creature) : base(creature, DataTypes.Nefarian)
        {
            Initialize();
        }

        void Initialize()
        {
            SpawnedAdds = 0;
        }

        public override void Reset()
        {
            Initialize();

            if (me.GetMapId() == 469)
            {
                if (me.FindNearestCreature(BWLCreatureIds.Nefarian, 1000.0f, true) == null)
                    _Reset();

                me.SetVisible(true);
                me.SetNpcFlag(NPCFlags1.Gossip);
                me.SetFaction(FactionTemplates.Friendly);
                me.SetStandState(UnitStandStateType.SitHighChair);
                me.RemoveAura(SpellIds.NefariansBarrier);
            }
        }

        public override void JustReachedHome()
        {
            Reset();
        }

        void BeginEvent(Player target)
        {
            _JustEngagedWith(target);

            Talk(TextIds.SayGamesbegin2);

            me.SetFaction(FactionTemplates.DragonflightBlack);
            me.RemoveNpcFlag(NPCFlags1.Gossip);
            DoCast(me, SpellIds.NefariansBarrier);
            me.SetStandState(UnitStandStateType.Stand);
            me.SetImmuneToPC(false);
            AttackStart(target);
            _events.ScheduleEvent(EventIds.ShadowBolt, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));
            _events.ScheduleEvent(EventIds.Fear, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
            //_events.ScheduleEvent(EventIds.MindControl, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(35));
            _events.ScheduleEvent(EventIds.SpawnAdd, TimeSpan.FromSeconds(10));
        }

        public override void SummonedCreatureDies(Creature summon, Unit killer)
        {
            if (summon.GetEntry() != BWLCreatureIds.Nefarian)
            {
                summon.UpdateEntry(CreatureIds.BoneConstruct);
                summon.SetUninteractible(true);
                summon.SetReactState(ReactStates.Passive);
                summon.SetStandState(UnitStandStateType.Dead);
            }
        }

        public override void JustSummoned(Creature summon) { }

        public override void SetData(int type, int data)
        {
            if (type == 1 && data == 1)
            {
                me.StopMoving();
                _events.ScheduleEvent(EventIds.Path2, TimeSpan.FromSeconds(9));
            }

            if (type == 1 && data == 2)
                _events.ScheduleEvent(EventIds.Success1, TimeSpan.FromSeconds(5));
        }

        public override void UpdateAI(uint diff)
        {
            if (!UpdateVictim())
            {
                _events.Update(diff);

                _events.ExecuteEvents(eventId =>
                {
                    switch (eventId)
                    {
                        case EventIds.Path2:
                            me.GetMotionMaster().MovePath(MiscConst.NefariusPath2, false);
                            _events.ScheduleEvent(EventIds.Chaos1, TimeSpan.FromSeconds(7));
                            break;
                        case EventIds.Chaos1:
                            Creature gyth = me.FindNearestCreature(CreatureIds.Gyth, 75.0f, true);
                            if (gyth != null)
                            {
                                me.SetFacingToObject(gyth);
                                Talk(TextIds.SayChaosSpell);
                            }
                            _events.ScheduleEvent(EventIds.Chaos2, TimeSpan.FromSeconds(2));
                            break;
                        case EventIds.Chaos2:
                            DoCast(SpellIds.ChromaticChaos);
                            me.SetFacingTo(1.570796f);
                            break;
                        case EventIds.Success1:
                            Unit player = me.SelectNearestPlayer(60.0f);
                            if (player != null)
                            {
                                me.SetFacingToObject(player);
                                Talk(TextIds.SaySuccess);
                                GameObject portcullis1 = me.FindNearestGameObject(GameObjectIds.PortcullisActive, 65.0f);
                                if (portcullis1 != null)
                                    portcullis1.SetGoState(GameObjectState.Active);
                                GameObject portcullis2 = me.FindNearestGameObject(GameObjectIds.PortcullisTobossrooms, 80.0f);
                                if (portcullis2 != null)
                                    portcullis2.SetGoState(GameObjectState.Active);
                            }
                            _events.ScheduleEvent(EventIds.Success2, TimeSpan.FromSeconds(4));
                            break;
                        case EventIds.Success2:
                            DoCast(me, SpellIds.VaelastraszzSpawn);
                            me.DespawnOrUnsummon(TimeSpan.FromSeconds(1));
                            break;
                        case EventIds.Path3:
                            me.GetMotionMaster().MovePath(MiscConst.NefariusPath3, false);
                            break;
                        default:
                            break;
                    }
                });
                return;
            }

            // Only do this if we haven't spawned nefarian yet
            if (UpdateVictim() && SpawnedAdds <= 42)
            {
                _events.Update(diff);

                if (me.HasUnitState(UnitState.Casting))
                    return;

                _events.ExecuteEvents(eventId =>
                {
                    switch (eventId)
                    {
                        case EventIds.ShadowBolt:
                            switch (RandomHelper.URand(0, 1))
                            {
                                case 0:
                                    DoCastVictim(SpellIds.ShadowboltVolley);
                                    break;
                                case 1:
                                    Unit target = SelectTarget(SelectTargetMethod.Random, 0, 40, true);
                                    if (target != null)
                                        DoCast(target, SpellIds.Shadowbolt);
                                    break;
                            }
                            ResetThreatList();
                            _events.ScheduleEvent(EventIds.ShadowBolt, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));
                            break;
                        case EventIds.Fear:
                        {
                            Unit target = SelectTarget(SelectTargetMethod.Random, 0, 40, true);
                            if (target != null)
                                DoCast(target, SpellIds.Fear);
                            _events.ScheduleEvent(EventIds.Fear, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
                            break;
                        }
                        case EventIds.MindControl:
                        {
                            Unit target = SelectTarget(SelectTargetMethod.Random, 0, 40, true);
                            if (target != null)
                                DoCast(target, SpellIds.ShadowCommand);
                            _events.ScheduleEvent(EventIds.MindControl, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(35));
                            break;
                        }
                        case EventIds.SpawnAdd:
                            for (byte i = 0; i < 2; ++i)
                            {
                                int CreatureID;
                                if (RandomHelper.URand(0, 2) == 0)
                                    CreatureID = CreatureIds.ChromaticDrakanoid;
                                else
                                    CreatureID = MiscConst.Entry[RandomHelper.IRand(0, 4)];
                                Creature dragon = me.SummonCreature(CreatureID, MiscConst.DrakeSpawnLoc[i]);
                                if (dragon != null)
                                {
                                    dragon.SetFaction(FactionTemplates.DragonflightBlack);
                                    dragon.GetAI().AttackStart(me.GetVictim());
                                }

                                if (++SpawnedAdds >= 42)
                                {
                                    Creature nefarian = me.SummonCreature(BWLCreatureIds.Nefarian, MiscConst.NefarianLoc[0]);
                                    if (nefarian != null)
                                    {
                                        nefarian.SetActive(true);
                                        nefarian.SetFarVisible(true);
                                        nefarian.SetCanFly(true);
                                        nefarian.SetDisableGravity(true);
                                        nefarian.CastSpell(null, SpellIds.ShadowflameInitial);
                                        nefarian.GetMotionMaster().MovePoint(1, MiscConst.NefarianLoc[1]);
                                    }
                                    _events.CancelEvent(EventIds.MindControl);
                                    _events.CancelEvent(EventIds.Fear);
                                    _events.CancelEvent(EventIds.ShadowBolt);
                                    me.SetVisible(false);
                                    return;
                                }
                            }
                            _events.ScheduleEvent(EventIds.SpawnAdd, TimeSpan.FromSeconds(4));
                            break;
                    }

                    if (me.HasUnitState(UnitState.Casting))
                        return;
                });
            }
        }

        public override bool OnGossipSelect(Player player, int menuId, int gossipListId)
        {
            if (menuId == TextIds.GossipId && gossipListId == TextIds.GossipOptionId)
            {
                player.CloseGossipMenu();
                Talk(TextIds.SayGamesbegin1);
                BeginEvent(player);
            }
            return false;
        }
    }

    [Script]
    class boss_nefarian : BossAI
    {
        bool canDespawn;
        uint DespawnTimer;
        bool Phase3;

        public boss_nefarian(Creature creature) : base(creature, DataTypes.Nefarian)
        {
            Initialize();
        }

        void Initialize()
        {
            Phase3 = false;
            canDespawn = false;
            DespawnTimer = 30000;
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void JustReachedHome()
        {
            canDespawn = true;
        }

        public override void JustEngagedWith(Unit who)
        {
            _events.ScheduleEvent(EventIds.Shadowflame, TimeSpan.FromSeconds(12));
            _events.ScheduleEvent(EventIds.Fear, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(35));
            _events.ScheduleEvent(EventIds.Veilofshadow, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(35));
            _events.ScheduleEvent(EventIds.Cleave, TimeSpan.FromSeconds(7));
            //_events.ScheduleEvent(EventIds.Taillash, TimeSpan.FromSeconds(10));
            _events.ScheduleEvent(EventIds.Classcall, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(35));
            Talk(TextIds.SayRandom);
        }

        public override void JustDied(Unit killer)
        {
            _JustDied();
            Talk(TextIds.SayDeath);
        }

        public override void KilledUnit(Unit victim)
        {
            if ((RandomHelper.Rand32() % 5) != 0)
                return;

            Talk(TextIds.SaySlay, victim);
        }

        public override void MovementInform(MovementGeneratorType type, int id)
        {
            if (type != MovementGeneratorType.Point)
                return;

            if (id == 1)
            {
                DoZoneInCombat();
                if (me.GetVictim() != null)
                    AttackStart(me.GetVictim());
            }
        }

        public override void UpdateAI(uint diff)
        {
            if (canDespawn && DespawnTimer <= diff)
            {
                instance.SetBossState(DataTypes.Nefarian, EncounterState.Fail);

                List<Creature> constructList = me.GetCreatureListWithEntryInGrid(CreatureIds.BoneConstruct, 500.0f);
                foreach (var creature in constructList)
                    creature.DespawnOrUnsummon();

            }
            else DespawnTimer -= diff;

            if (!UpdateVictim())
                return;

            if (canDespawn)
                canDespawn = false;

            _events.Update(diff);

            if (me.HasUnitState(UnitState.Casting))
                return;

            _events.ExecuteEvents(eventId =>
            {
                switch (eventId)
                {
                    case EventIds.Shadowflame:
                        DoCastVictim(SpellIds.Shadowflame);
                        _events.ScheduleEvent(
                            EventIds.Shadowflame, TimeSpan.FromSeconds(12));
                        break;
                    case EventIds.Fear:
                        DoCastVictim(SpellIds.Bellowingroar);
                        _events.ScheduleEvent(
                            EventIds.Fear, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(35));
                        break;
                    case EventIds.Veilofshadow:
                        DoCastVictim(SpellIds.Veilofshadow);
                        _events.ScheduleEvent(
                            EventIds.Veilofshadow, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(35));
                        break;
                    case EventIds.Cleave:
                        DoCastVictim(SpellIds.Cleave);
                        _events.ScheduleEvent(
                            EventIds.Cleave, TimeSpan.FromSeconds(7));
                        break;
                    case EventIds.Taillash:
                        // Cast Nyi since we need a better check for behind target
                        DoCastVictim(SpellIds.Taillash);
                        _events.ScheduleEvent(
                            EventIds.Taillash, TimeSpan.FromSeconds(10));
                        break;
                    case EventIds.Classcall:
                        Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100.0f, true);
                        if (target != null)
                            switch (target.GetClass())
                            {
                                case Class.Mage:
                                    Talk(TextIds.SayMage);
                                    DoCast(me, SpellIds.Mage);
                                    break;
                                case Class.Warrior:
                                    Talk(TextIds.SayWarrior);
                                    DoCast(me, SpellIds.Warrior);
                                    break;
                                case Class.Druid:
                                    Talk(TextIds.SayDruid);
                                    DoCast(target, SpellIds.Druid);
                                    break;
                                case Class.Priest:
                                    Talk(TextIds.SayPriest);
                                    DoCast(me, SpellIds.Priest);
                                    break;
                                case Class.Paladin:
                                    Talk(TextIds.SayPaladin);
                                    DoCast(me, SpellIds.Paladin);
                                    break;
                                case Class.Shaman:
                                    Talk(TextIds.SayShaman);
                                    DoCast(me, SpellIds.Shaman);
                                    break;
                                case Class.Warlock:
                                    Talk(TextIds.SayWarlock);
                                    DoCast(me, SpellIds.Warlock);
                                    break;
                                case Class.Hunter:
                                    Talk(TextIds.SayHunter);
                                    DoCast(me, SpellIds.Hunter);
                                    break;
                                case Class.Rogue:
                                    Talk(TextIds.SayRogue);
                                    DoCast(me, SpellIds.Rogue);
                                    break;
                                case Class.Deathknight:
                                    Talk(TextIds.SayDeathKnight);
                                    DoCast(me, SpellIds.DeathKnight);
                                    break;
                                default:
                                    break;
                            }

                        _events.ScheduleEvent(
                            EventIds.Classcall, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(35));

                        break;
                }

                if (me.HasUnitState(UnitState.Casting))
                    return;
            });

            // Phase3 begins when health below 20 pct
            if (!Phase3 && HealthBelowPct(20))
            {
                List<Creature> constructList = 
                    me.GetCreatureListWithEntryInGrid(CreatureIds.BoneConstruct, 500.0f);

                foreach (var creature in constructList)
                {
                    if (creature != null && !creature.IsAlive())
                    {
                        creature.Respawn();
                        DoZoneInCombat(creature);
                        creature.SetUninteractible(false);
                        creature.SetReactState(ReactStates.Aggressive);
                        creature.SetStandState(UnitStandStateType.Stand);
                    }
                }

                Phase3 = true;
                Talk(TextIds.SayRaiseSkeletons);
            }
        }
    }
}