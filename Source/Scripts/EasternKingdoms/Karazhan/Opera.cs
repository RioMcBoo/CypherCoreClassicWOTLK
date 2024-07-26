// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.Karazhan.EsOpera
{
    struct TextIds
    {
        public const int SayDorotheeDeath = 0;
        public const int SayDorotheeSummon = 1;
        public const int SayDorotheeTitoDeath = 2;
        public const int SayDorotheeAggro = 3;

        public const int SayRoarAggro = 0;
        public const int SayRoarDeath = 1;
        public const int SayRoarSlay = 2;

        public const int SayStrawmanAggro = 0;
        public const int SayStrawmanDeath = 1;
        public const int SayStrawmanSlay = 2;

        public const int SayTinheadAggro = 0;
        public const int SayTinheadDeath = 1;
        public const int SayTinheadSlay = 2;
        public const int EmoteRust = 3;

        public const int SayCroneAggro = 0;
        public const int SayCroneDeath = 1;
        public const int SayCroneSlay = 2;

        //RedRidingHood
        public const int SayWolfAggro = 0;
        public const int SayWolfSlay = 1;
        public const int SayWolfHood = 2;
        public const int OptionWhatPhatLewtsYouHave = 7443;

        //Romulo & Julianne
        public const int SayJulianneAggro = 0;
        public const int SayJulianneEnter = 1;
        public const int SayJulianneDeath01 = 2;
        public const int SayJulianneDeath02 = 3;
        public const int SayJulianneResurrect = 4;
        public const int SayJulianneSlay = 5;

        public const int SayRomuloAggro = 0;
        public const int SayRomuloDeath = 1;
        public const int SayRomuloEnter = 2;
        public const int SayRomuloResurrect = 3;
        public const int SayRomuloSlay = 4;
    }

    struct SpellIds
    {
        // Dorothee
        public const int Waterbolt = 31012;
        public const int Scream = 31013;
        public const int Summontito = 31014;

        // Tito
        public const int Yipping = 31015;

        // Strawman
        public const int BrainBash = 31046;
        public const int BrainWipe = 31069;
        public const int BurningStraw = 31075;

        // Tinhead
        public const int Cleave = 31043;
        public const int Rust = 31086;

        // Roar
        public const int Mangle = 31041;
        public const int Shred = 31042;
        public const int FrightenedScream = 31013;

        // Crone
        public const int ChainLightning = 32337;

        // Cyclone
        public const int Knockback = 32334;
        public const int CycloneVisual = 32332;

        //Red Riding Hood
        public const int LittleRedRidingHood = 30768;
        public const int TerrifyingHowl = 30752;
        public const int WideSwipe = 30761;

        //Romulo & Julianne
        public const int BlindingPassion = 30890;
        public const int Devotion = 30887;
        public const int EternalAffection = 30878;
        public const int PowerfulAttraction = 30889;
        public const int DrinkPoison = 30907;

        public const int BackwardLunge = 30815;
        public const int Daring = 30841;
        public const int DeadlySwathe = 30817;
        public const int PoisonThrust = 30822;

        public const int UndyingLove = 30951;
        public const int ResVisual = 24171;
    }

    struct CreatureIds
    {
        public const int Tito = 17548;
        public const int Cyclone = 18412;
        public const int Crone = 18168;

        //Red Riding Hood
        public const int BigBadWolf = 17521;

        //Romulo & Julianne
        public const int Romulo = 17533;
    }

    struct MiscConst
    {
        //Red Riding Hood
        public const int SoundWolfDeath = 9275;

        //Romulo & Julianne
        public const int RomuloX = -10900;
        public const int RomuloY = -1758;

        public static void SummonCroneIfReady(InstanceScript instance, Creature creature)
        {
            instance.SetData(DataTypes.OperaOzDeathcount, (int)EncounterState.Special);  // Increment DeathCount

            if (instance.GetData(DataTypes.OperaOzDeathcount) == 4)
            {
                Creature pCrone = creature.SummonCreature(CreatureIds.Crone, -10891.96f, -1755.95f, creature.GetPositionZ(), 4.64f, TempSummonType.TimedOrDeadDespawn, (Hours)2);
                if (pCrone != null)
                {
                    if (creature.GetVictim() != null)
                        pCrone.GetAI().AttackStart(creature.GetVictim());
                }
            }
        }

        public static void PretendToDie(Creature creature)
        {
            creature.InterruptNonMeleeSpells(true);
            creature.RemoveAllAuras();
            creature.SetHealth(0);
            creature.SetUninteractible(true);
            creature.GetMotionMaster().Clear();
            creature.GetMotionMaster().MoveIdle();
            creature.SetStandState(UnitStandStateType.Dead);
        }

        public static void Resurrect(Creature target)
        {
            target.SetUninteractible(false);
            target.SetFullHealth();
            target.SetStandState(UnitStandStateType.Stand);
            target.CastSpell(target, SpellIds.ResVisual, true);
            if (target.GetVictim() != null)
            {
                target.GetMotionMaster().MoveChase(target.GetVictim());
                target.GetAI().AttackStart(target.GetVictim());
            }
            else
                target.GetMotionMaster().Initialize();
        }
    }

    enum RAJPhase
    {
        Julianne = 0,
        Romulo = 1,
        Both = 2,
    }

    [Script]
    class boss_dorothee : ScriptedAI
    {
        InstanceScript instance;

        TimeSpan AggroTimer;

        TimeSpan WaterBoltTimer;
        TimeSpan FearTimer;
        TimeSpan SummonTitoTimer;

        public bool SummonedTito;
        public bool TitoDied;

        public boss_dorothee(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            AggroTimer = Time.SpanFromMilliseconds(500);

            WaterBoltTimer = Time.SpanFromMilliseconds(5000);
            FearTimer = Time.SpanFromMilliseconds(15000);
            SummonTitoTimer = Time.SpanFromMilliseconds(47500);

            SummonedTito = false;
            TitoDied = false;
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayDorotheeAggro);
        }

        public override void JustReachedHome()
        {
            me.DespawnOrUnsummon();
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayDorotheeDeath);

            MiscConst.SummonCroneIfReady(instance, me);
        }

        public override void AttackStart(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.AttackStart(who);
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.MoveInLineOfSight(who);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (AggroTimer != TimeSpan.Zero)
            {
                if (AggroTimer <= diff)
                {
                    me.RemoveUnitFlag(UnitFlags.NonAttackable);
                    AggroTimer = TimeSpan.Zero;
                }
                else AggroTimer -= diff;
            }

            if (!UpdateVictim())
                return;

            if (WaterBoltTimer <= diff)
            {
                DoCast(SelectTarget(SelectTargetMethod.Random, 0), SpellIds.Waterbolt);
                WaterBoltTimer = TitoDied ? Time.SpanFromMilliseconds(1500) : Time.SpanFromMilliseconds(5000);
            }
            else WaterBoltTimer -= diff;

            if (FearTimer <= diff)
            {
                DoCastVictim(SpellIds.Scream);
                FearTimer = Time.SpanFromMilliseconds(30000);
            }
            else FearTimer -= diff;

            if (!SummonedTito)
            {
                if (SummonTitoTimer <= diff)
                    SummonTito();
                else SummonTitoTimer -= diff;
            }
        }

        void SummonTito()
        {
            Creature pTito = me.SummonCreature(CreatureIds.Tito, 0.0f, 0.0f, 0.0f, 0.0f, TempSummonType.TimedDespawnOutOfCombat, (Seconds)30);
            if (pTito != null)
            {
                Talk(TextIds.SayDorotheeSummon);
                pTito.GetAI<npc_tito>().DorotheeGUID = me.GetGUID();
                pTito.GetAI().AttackStart(me.GetVictim());
                SummonedTito = true;
                TitoDied = false;
            }
        }
    }

    [Script]
    class npc_tito : ScriptedAI
    {
        public ObjectGuid DorotheeGUID;
        TimeSpan YipTimer;

        public npc_tito(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            DorotheeGUID.Clear();
            YipTimer = Time.SpanFromMilliseconds(10000);
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void JustEngagedWith(Unit who) { }

        public override void JustDied(Unit killer)
        {
            if (!DorotheeGUID.IsEmpty())
            {
                Creature Dorothee = ObjectAccessor.GetCreature(me, DorotheeGUID);
                if (Dorothee != null && Dorothee.IsAlive())
                {
                    Dorothee.GetAI<boss_dorothee>().TitoDied = true;
                    Talk(TextIds.SayDorotheeTitoDeath, Dorothee);
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            if (YipTimer <= diff)
            {
                DoCastVictim(SpellIds.Yipping);
                YipTimer = Time.SpanFromMilliseconds(10000);
            }
            else YipTimer -= diff;
        }
    }

    [Script]
    class boss_strawman : ScriptedAI
    {
        InstanceScript instance;

        TimeSpan AggroTimer;
        TimeSpan BrainBashTimer;
        TimeSpan BrainWipeTimer;

        public boss_strawman(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            AggroTimer = Time.SpanFromMilliseconds(13000);
            BrainBashTimer = Time.SpanFromMilliseconds(5000);
            BrainWipeTimer = Time.SpanFromMilliseconds(7000);
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void AttackStart(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.AttackStart(who);
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.MoveInLineOfSight(who);
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayStrawmanAggro);
        }

        public override void JustReachedHome()
        {
            me.DespawnOrUnsummon();
        }

        public override void SpellHit(WorldObject caster, SpellInfo spellInfo)
        {
            if ((spellInfo.SchoolMask == SpellSchoolMask.Fire) && ((RandomHelper.Rand32() % 10) == 0))
                DoCast(me, SpellIds.BurningStraw, new CastSpellExtraArgs(true));
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayStrawmanDeath);

            MiscConst.SummonCroneIfReady(instance, me);
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SayStrawmanSlay);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (AggroTimer != TimeSpan.Zero)
            {
                if (AggroTimer <= diff)
                {
                    me.RemoveUnitFlag(UnitFlags.NonAttackable);
                    AggroTimer = TimeSpan.Zero;
                }
                else AggroTimer -= diff;
            }

            if (!UpdateVictim())
                return;

            if (BrainBashTimer <= diff)
            {
                DoCastVictim(SpellIds.BrainBash);
                BrainBashTimer = Time.SpanFromMilliseconds(15000);
            }
            else BrainBashTimer -= diff;

            if (BrainWipeTimer <= diff)
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                if (target != null)
                    DoCast(target, SpellIds.BrainWipe);
                BrainWipeTimer = Time.SpanFromMilliseconds(20000);
            }
            else BrainWipeTimer -= diff;
        }
    }

    [Script]
    class boss_tinhead : ScriptedAI
    {
        InstanceScript instance;

        TimeSpan AggroTimer;
        TimeSpan CleaveTimer;
        TimeSpan RustTimer;

        byte RustCount;

        public boss_tinhead(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            AggroTimer = Time.SpanFromMilliseconds(15000);
            CleaveTimer = Time.SpanFromMilliseconds(5000);
            RustTimer = Time.SpanFromMilliseconds(30000);

            RustCount = 0;
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayTinheadAggro);
        }

        public override void JustReachedHome()
        {
            me.DespawnOrUnsummon();
        }

        public override void AttackStart(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.AttackStart(who);
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.MoveInLineOfSight(who);
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayTinheadDeath);

            MiscConst.SummonCroneIfReady(instance, me);
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SayTinheadSlay);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (AggroTimer != TimeSpan.Zero)
            {
                if (AggroTimer <= diff)
                {
                    me.RemoveUnitFlag(UnitFlags.NonAttackable);
                    AggroTimer = TimeSpan.Zero;
                }
                else AggroTimer -= diff;
            }

            if (!UpdateVictim())
                return;

            if (CleaveTimer <= diff)
            {
                DoCastVictim(SpellIds.Cleave);
                CleaveTimer = Time.SpanFromMilliseconds(5000);
            }
            else CleaveTimer -= diff;

            if (RustCount < 8)
            {
                if (RustTimer <= diff)
                {
                    ++RustCount;
                    Talk(TextIds.EmoteRust);
                    DoCast(me, SpellIds.Rust);
                    RustTimer = Time.SpanFromMilliseconds(6000);
                }
                else RustTimer -= diff;
            }
        }
    }

    [Script]
    class boss_roar : ScriptedAI
    {
        InstanceScript instance;

        TimeSpan AggroTimer;
        TimeSpan MangleTimer;
        TimeSpan ShredTimer;
        TimeSpan ScreamTimer;

        public boss_roar(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            AggroTimer = Time.SpanFromMilliseconds(20000);
            MangleTimer = Time.SpanFromMilliseconds(5000);
            ShredTimer = Time.SpanFromMilliseconds(10000);
            ScreamTimer = Time.SpanFromMilliseconds(15000);
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void MoveInLineOfSight(Unit who)

        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.MoveInLineOfSight(who);
        }

        public override void AttackStart(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.AttackStart(who);
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayRoarAggro);
        }

        public override void JustReachedHome()
        {
            me.DespawnOrUnsummon();
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayRoarDeath);

            MiscConst.SummonCroneIfReady(instance, me);
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SayRoarSlay);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (AggroTimer != TimeSpan.Zero)
            {
                if (AggroTimer <= diff)
                {
                    me.RemoveUnitFlag(UnitFlags.NonAttackable);
                    AggroTimer = TimeSpan.Zero;
                }
                else AggroTimer -= diff;
            }

            if (!UpdateVictim())
                return;

            if (MangleTimer <= diff)
            {
                DoCastVictim(SpellIds.Mangle);
                MangleTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(5000, 8000));
            }
            else MangleTimer -= diff;

            if (ShredTimer <= diff)
            {
                DoCastVictim(SpellIds.Shred);
                ShredTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(10000, 15000));
            }
            else ShredTimer -= diff;

            if (ScreamTimer <= diff)
            {
                DoCastVictim(SpellIds.FrightenedScream);
                ScreamTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(20000, 30000));
            }
            else ScreamTimer -= diff;
        }
    }

    [Script]
    class boss_crone : ScriptedAI
    {
        InstanceScript instance;

        TimeSpan CycloneTimer;
        TimeSpan ChainLightningTimer;

        public boss_crone(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            // Hello, developer from the future! It's me again!
            // This time, you're fixing Karazhan scripts. Awesome. These are a mess of hacks. An amalgamation of hacks, so to speak. Maybe even a Patchwerk thereof.
            // Anyway, I digress.
            // @todo This line below is obviously a hack. Duh. I'm just coming in here to hackfix the encounter to actually be completable.
            // It needs a rewrite. Badly. Please, take good care of it.
            me.RemoveUnitFlag(UnitFlags.NonAttackable);
            me.SetImmuneToPC(false);
            CycloneTimer = Time.SpanFromMilliseconds(30000);
            ChainLightningTimer = Time.SpanFromMilliseconds(10000);
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void JustReachedHome()
        {
            me.DespawnOrUnsummon();
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SayCroneSlay);
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayCroneAggro);
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayCroneDeath);
            instance.SetBossState(DataTypes.OperaPerformance, EncounterState.Done);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            if (CycloneTimer <= diff)
            {
                Creature Cyclone = DoSpawnCreature(CreatureIds.Cyclone, RandomHelper.URand(0, 9), RandomHelper.URand(0, 9), 0, 0, TempSummonType.TimedDespawn, Time.SpanFromSeconds(15));
                if (Cyclone != null)
                    Cyclone.CastSpell(Cyclone, SpellIds.CycloneVisual, true);
                CycloneTimer = Time.SpanFromMilliseconds(30000);
            }
            else CycloneTimer -= diff;

            if (ChainLightningTimer <= diff)
            {
                DoCastVictim(SpellIds.ChainLightning);
                ChainLightningTimer = Time.SpanFromMilliseconds(15000);
            }
            else ChainLightningTimer -= diff;
        }
    }

    [Script]
    class npc_cyclone : ScriptedAI
    {
        TimeSpan MoveTimer;

        public npc_cyclone(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            MoveTimer = Time.SpanFromMilliseconds(1000);
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void JustEngagedWith(Unit who) { }

        public override void MoveInLineOfSight(Unit who)

        {
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!me.HasAura(SpellIds.Knockback))
                DoCast(me, SpellIds.Knockback, new CastSpellExtraArgs(true));

            if (MoveTimer <= diff)
            {
                Position pos = me.GetRandomNearPosition(10);
                me.GetMotionMaster().MovePoint(0, pos);
                MoveTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(5000, 8000));
            }
            else MoveTimer -= diff;
        }
    }

    [Script]
    class npc_grandmother : ScriptedAI
    {
        public npc_grandmother(Creature creature) : base(creature) { }

        public override bool OnGossipSelect(Player player, int menuId, int gossipListId)
        {
            if (menuId == TextIds.OptionWhatPhatLewtsYouHave && gossipListId == 0)
            {
                player.CloseGossipMenu();

                Creature pBigBadWolf = me.SummonCreature(
                    CreatureIds.BigBadWolf, me.GetPositionX(), me.GetPositionY(), me.GetPositionZ(), me.GetOrientation(), 
                    TempSummonType.TimedOrDeadDespawn, (Hours)2);

                if (pBigBadWolf != null)
                    pBigBadWolf.GetAI().AttackStart(player);

                me.DespawnOrUnsummon();
            }
            return false;
        }
    }

    [Script]
    class boss_bigbadwolf : ScriptedAI
    {
        InstanceScript instance;

        TimeSpan ChaseTimer;
        TimeSpan FearTimer;
        TimeSpan SwipeTimer;

        ObjectGuid HoodGUID;
        float TempThreat;

        bool IsChasing;

        public boss_bigbadwolf(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            ChaseTimer = Time.SpanFromMilliseconds(30000);
            FearTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(25000, 35000));
            SwipeTimer = Time.SpanFromMilliseconds(5000);

            HoodGUID.Clear();
            TempThreat = 0;

            IsChasing = false;
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayWolfAggro);
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SayWolfSlay);
        }

        public override void JustReachedHome()
        {
            me.DespawnOrUnsummon();
        }

        public override void JustDied(Unit killer)
        {
            DoPlaySoundToSet(me, MiscConst.SoundWolfDeath);
            instance.SetBossState(DataTypes.OperaPerformance, EncounterState.Done);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            if (ChaseTimer <= diff)
            {
                if (!IsChasing)
                {
                    Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                    if (target != null)
                    {
                        Talk(TextIds.SayWolfHood);
                        DoCast(target, SpellIds.LittleRedRidingHood, new CastSpellExtraArgs(true));
                        TempThreat = GetThreat(target);
                        if (TempThreat != 0f)
                            ModifyThreatByPercent(target, -100);
                        HoodGUID = target.GetGUID();
                        AddThreat(target, 1000000.0f);
                        ChaseTimer = Time.SpanFromMilliseconds(20000);
                        IsChasing = true;
                    }
                }
                else
                {
                    IsChasing = false;

                    Unit target = Global.ObjAccessor.GetUnit(me, HoodGUID);
                    if (target != null)
                    {
                        HoodGUID.Clear();
                        if (GetThreat(target) != 0f)
                            ModifyThreatByPercent(target, -100);
                        AddThreat(target, TempThreat);
                        TempThreat = 0;
                    }

                    ChaseTimer = Time.SpanFromMilliseconds(40000);
                }
            }
            else ChaseTimer -= diff;

            if (IsChasing)
                return;

            if (FearTimer <= diff)
            {
                DoCastVictim(SpellIds.TerrifyingHowl);
                FearTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(25000, 35000));
            }
            else FearTimer -= diff;

            if (SwipeTimer <= diff)
            {
                DoCastVictim(SpellIds.WideSwipe);
                SwipeTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(25000, 30000));
            }
            else SwipeTimer -= diff;
        }
    }

    [Script]
    class boss_julianne : ScriptedAI
    {
        InstanceScript instance;

        TimeSpan EntryYellTimer;
        TimeSpan AggroYellTimer;

        ObjectGuid RomuloGUID;

        RAJPhase Phase;

        TimeSpan BlindingPassionTimer;
        TimeSpan DevotionTimer;
        TimeSpan EternalAffectionTimer;
        TimeSpan PowerfulAttractionTimer;
        TimeSpan SummonRomuloTimer;
        public TimeSpan ResurrectTimer;
        TimeSpan DrinkPoisonTimer;
        public TimeSpan ResurrectSelfTimer;

        public bool IsFakingDeath;
        bool SummonedRomulo;
        public bool RomuloDead;

        public boss_julianne(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
            EntryYellTimer = Time.SpanFromMilliseconds(1000);
            AggroYellTimer = Time.SpanFromMilliseconds(10000);
            IsFakingDeath = false;
            ResurrectTimer = TimeSpan.Zero;
        }

        void Initialize()
        {
            RomuloGUID.Clear();
            Phase = RAJPhase.Julianne;

            BlindingPassionTimer = Time.SpanFromMilliseconds(30000);
            DevotionTimer = Time.SpanFromMilliseconds(15000);
            EternalAffectionTimer = Time.SpanFromMilliseconds(25000);
            PowerfulAttractionTimer = Time.SpanFromMilliseconds(5000);
            SummonRomuloTimer = Time.SpanFromMilliseconds(10000);
            DrinkPoisonTimer = TimeSpan.Zero;
            ResurrectSelfTimer = TimeSpan.Zero;

            SummonedRomulo = false;
            RomuloDead = false;
        }

        public override void Reset()
        {
            Initialize();
            if (IsFakingDeath)
            {
                MiscConst.Resurrect(me);
                IsFakingDeath = false;
            }
        }

        public override void JustEngagedWith(Unit who) { }

        public override void AttackStart(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.AttackStart(who);
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.MoveInLineOfSight(who);
        }

        public override void JustReachedHome()
        {
            me.DespawnOrUnsummon();
        }

        public override void SpellHit(WorldObject caster, SpellInfo spellInfo)
        {
            if (spellInfo.Id == SpellIds.DrinkPoison)
            {
                Talk(TextIds.SayJulianneDeath01);
                DrinkPoisonTimer = Time.SpanFromMilliseconds(2500);
            }
        }

        public override void DamageTaken(Unit done_by, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (damage < me.GetHealth())
                return;

            //anything below only used if incoming damage will kill

            if (Phase == RAJPhase.Julianne)
            {
                damage = 0;

                //this means already drinking, so return
                if (IsFakingDeath)
                    return;

                me.InterruptNonMeleeSpells(true);
                DoCast(me, SpellIds.DrinkPoison);

                IsFakingDeath = true;
                //Is This Usefull? Creature Julianne = (ObjectAccessor.GetCreature((me), JulianneGUID));
                return;
            }

            if (Phase == RAJPhase.Romulo)
            {
                Log.outError(LogFilter.Scripts, 
                    "boss_julianneAI: cannot take damage in PhaseRomulo, why was i here?");

                damage = 0;
                return;
            }

            if (Phase == RAJPhase.Both)
            {
                //if this is true then we have to kill romulo too
                if (RomuloDead)
                {
                    Creature Romulo = ObjectAccessor.GetCreature(me, RomuloGUID);
                    if (Romulo != null)
                    {
                        Romulo.SetUninteractible(false);
                        Romulo.GetMotionMaster().Clear();
                        Romulo.SetDeathState(DeathState.JustDied);
                        Romulo.CombatStop(true);
                        Romulo.ReplaceAllDynamicFlags(UnitDynFlags.Lootable);
                    }

                    return;
                }

                //if not already returned, then romulo is alive and we can pretend die
                Creature Romulo1 = (ObjectAccessor.GetCreature((me), RomuloGUID));
                if (Romulo1 != null)
                {
                    MiscConst.PretendToDie(me);
                    IsFakingDeath = true;
                    Romulo1.GetAI<boss_romulo>().ResurrectTimer = Time.SpanFromMilliseconds(10000);
                    Romulo1.GetAI<boss_romulo>().JulianneDead = true;
                    damage = 0;
                    return;
                }
            }

            Log.outError(LogFilter.Scripts, 
                "boss_julianneAI: DamageTaken reach end of code, that should not happen.");
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayJulianneDeath02);
            instance.SetBossState(DataTypes.OperaPerformance, EncounterState.Done);
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SayJulianneSlay);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (EntryYellTimer != TimeSpan.Zero)
            {
                if (EntryYellTimer <= diff)
                {
                    Talk(TextIds.SayJulianneEnter);
                    EntryYellTimer = TimeSpan.Zero;
                }
                else EntryYellTimer -= diff;
            }

            if (AggroYellTimer != TimeSpan.Zero)
            {
                if (AggroYellTimer <= diff)
                {
                    Talk(TextIds.SayJulianneAggro);
                    me.RemoveUnitFlag(UnitFlags.NonAttackable);
                    me.SetFaction(FactionTemplates.Monster2);
                    AggroYellTimer = TimeSpan.Zero;
                }
                else AggroYellTimer -= diff;
            }

            if (DrinkPoisonTimer != TimeSpan.Zero)
            {
                //will do this Time.SpanFromSeconds(2s)ecs after spell hit. this is time to display visual as expected
                if (DrinkPoisonTimer <= diff)
                {
                    MiscConst.PretendToDie(me);
                    Phase = RAJPhase.Romulo;
                    SummonRomuloTimer = Time.SpanFromMilliseconds(10000);
                    DrinkPoisonTimer = TimeSpan.Zero;
                }
                else DrinkPoisonTimer -= diff;
            }

            if (Phase == RAJPhase.Romulo && !SummonedRomulo)
            {
                if (SummonRomuloTimer <= diff)
                {
                    Creature pRomulo = me.SummonCreature(
                        CreatureIds.Romulo, MiscConst.RomuloX, MiscConst.RomuloY, me.GetPositionZ(), 0, 
                        TempSummonType.TimedOrDeadDespawn, (Hours)2);

                    if (pRomulo != null)
                    {
                        RomuloGUID = pRomulo.GetGUID();
                        pRomulo.GetAI<boss_romulo>().JulianneGUID = me.GetGUID();
                        pRomulo.GetAI<boss_romulo>().Phase = RAJPhase.Romulo;
                        DoZoneInCombat(pRomulo);

                        pRomulo.SetFaction(FactionTemplates.Monster2);
                    }
                    SummonedRomulo = true;
                }
                else SummonRomuloTimer -= diff;
            }

            if (ResurrectSelfTimer != TimeSpan.Zero)
            {
                if (ResurrectSelfTimer <= diff)
                {
                    MiscConst.Resurrect(me);
                    Phase = RAJPhase.Both;
                    IsFakingDeath = false;

                    if (me.GetVictim() != null)
                        AttackStart(me.GetVictim());

                    ResurrectSelfTimer = TimeSpan.Zero;
                    ResurrectTimer = Time.SpanFromMilliseconds(1000);
                }
                else ResurrectSelfTimer -= diff;
            }

            if (!UpdateVictim() || IsFakingDeath)
                return;

            if (RomuloDead)
            {
                if (ResurrectTimer <= diff)
                {
                    Creature Romulo = ObjectAccessor.GetCreature(me, RomuloGUID);
                    if (Romulo != null && Romulo.GetAI<boss_romulo>().IsFakingDeath)
                    {
                        Talk(TextIds.SayJulianneResurrect);
                        MiscConst.Resurrect(Romulo);
                        Romulo.GetAI<boss_romulo>().IsFakingDeath = false;
                        RomuloDead = false;
                        ResurrectTimer = Time.SpanFromMilliseconds(10000);
                    }
                }
                else ResurrectTimer -= diff;
            }

            if (BlindingPassionTimer <= diff)
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                if (target != null)
                    DoCast(target, SpellIds.BlindingPassion);
                BlindingPassionTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(30000, 45000));
            }
            else BlindingPassionTimer -= diff;

            if (DevotionTimer <= diff)
            {
                DoCast(me, SpellIds.Devotion);
                DevotionTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(15000, 45000));
            }
            else DevotionTimer -= diff;

            if (PowerfulAttractionTimer <= diff)
            {
                DoCast(SelectTarget(SelectTargetMethod.Random, 0), SpellIds.PowerfulAttraction);
                PowerfulAttractionTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(5000, 30000));
            }
            else PowerfulAttractionTimer -= diff;

            if (EternalAffectionTimer <= diff)
            {
                if (RandomHelper.URand(0, 1) != 0 && SummonedRomulo)
                {
                    Creature Romulo = (ObjectAccessor.GetCreature((me), RomuloGUID));
                    if (Romulo != null && Romulo.IsAlive() && !RomuloDead)
                        DoCast(Romulo, SpellIds.EternalAffection);
                }
                else DoCast(me, SpellIds.EternalAffection);

                EternalAffectionTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(45000, 60000));
            }
            else EternalAffectionTimer -= diff;
        }
    }

    [Script]
    class boss_romulo : ScriptedAI
    {
        InstanceScript instance;

        public ObjectGuid JulianneGUID;
        public RAJPhase Phase;

        TimeSpan BackwardLungeTimer;
        TimeSpan DaringTimer;
        TimeSpan DeadlySwatheTimer;
        TimeSpan PoisonThrustTimer;
        public TimeSpan ResurrectTimer;

        public bool IsFakingDeath;
        public bool JulianneDead;

        public boss_romulo(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            JulianneGUID.Clear();
            Phase = RAJPhase.Romulo;

            BackwardLungeTimer = Time.SpanFromMilliseconds(15000);
            DaringTimer = Time.SpanFromMilliseconds(20000);
            DeadlySwatheTimer = Time.SpanFromMilliseconds(25000);
            PoisonThrustTimer = Time.SpanFromMilliseconds(10000);
            ResurrectTimer = Time.SpanFromMilliseconds(10000);

            IsFakingDeath = false;
            JulianneDead = false;
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void JustReachedHome()
        {
            me.DespawnOrUnsummon();
        }

        public override void DamageTaken(Unit done_by, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (damage < me.GetHealth())
                return;

            //anything below only used if incoming damage will kill

            if (Phase == RAJPhase.Romulo)
            {
                Talk(TextIds.SayRomuloDeath);
                MiscConst.PretendToDie(me);
                IsFakingDeath = true;
                Phase = RAJPhase.Both;

                Creature Julianne = ObjectAccessor.GetCreature(me, JulianneGUID);
                if (Julianne != null)
                {
                    Julianne.GetAI<boss_julianne>().RomuloDead = true;
                    Julianne.GetAI<boss_julianne>().ResurrectSelfTimer = Time.SpanFromMilliseconds(10000);
                }

                damage = 0;
                return;
            }

            if (Phase == RAJPhase.Both)
            {
                if (JulianneDead)
                {
                    Creature Julianne = ObjectAccessor.GetCreature(me, JulianneGUID);
                    if (Julianne != null)
                    {
                        Julianne.SetUninteractible(false);
                        Julianne.GetMotionMaster().Clear();
                        Julianne.SetDeathState(DeathState.JustDied);
                        Julianne.CombatStop(true);
                        Julianne.ReplaceAllDynamicFlags(UnitDynFlags.Lootable);
                    }
                    return;
                }

                Creature Julianne1 = ObjectAccessor.GetCreature(me, JulianneGUID);
                if (Julianne1 != null)
                {
                    MiscConst.PretendToDie(me);
                    IsFakingDeath = true;
                    Julianne1.GetAI<boss_julianne>().ResurrectTimer = Time.SpanFromMilliseconds(10000);
                    Julianne1.GetAI<boss_julianne>().RomuloDead = true;
                    damage = 0;
                    return;
                }
            }

            Log.outError(LogFilter.Scenario, 
                "boss_romulo: DamageTaken reach end of code, that should not happen.");
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayRomuloAggro);
            if (!JulianneGUID.IsEmpty())
            {
                Creature Julianne = ObjectAccessor.GetCreature(me, JulianneGUID);
                if (Julianne != null && Julianne.GetVictim() != null)
                {
                    AddThreat(Julianne.GetVictim(), 1.0f);
                    AttackStart(Julianne.GetVictim());
                }
            }
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (me.HasUnitFlag(UnitFlags.NonAttackable))
                return;

            base.MoveInLineOfSight(who);
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayRomuloDeath);
            instance.SetBossState(DataTypes.OperaPerformance, EncounterState.Done);
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SayRomuloSlay);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim() || IsFakingDeath)
                return;

            if (JulianneDead)
            {
                if (ResurrectTimer <= diff)
                {
                    Creature Julianne = (ObjectAccessor.GetCreature((me), JulianneGUID));
                    if (Julianne != null && Julianne.GetAI<boss_julianne>().IsFakingDeath)
                    {
                        Talk(TextIds.SayRomuloResurrect);
                        MiscConst.Resurrect(Julianne);
                        Julianne.GetAI<boss_julianne>().IsFakingDeath = false;
                        JulianneDead = false;
                        ResurrectTimer = Time.SpanFromMilliseconds(10000);
                    }
                }
                else ResurrectTimer -= diff;
            }

            if (BackwardLungeTimer <= diff)
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 1, 100, true);
                if (target != null && !me.HasInArc(MathF.PI, target))
                {
                    DoCast(target, SpellIds.BackwardLunge);
                    BackwardLungeTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(15000, 30000));
                }
            }
            else BackwardLungeTimer -= diff;

            if (DaringTimer <= diff)
            {
                DoCast(me, SpellIds.Daring);
                DaringTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(20000, 40000));
            }
            else DaringTimer -= diff;

            if (DeadlySwatheTimer <= diff)
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                if (target != null)
                    DoCast(target, SpellIds.DeadlySwathe);
                DeadlySwatheTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(15000, 25000));
            }
            else DeadlySwatheTimer -= diff;

            if (PoisonThrustTimer <= diff)
            {
                DoCastVictim(SpellIds.PoisonThrust);
                PoisonThrustTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(10000, 20000));
            }
            else PoisonThrustTimer -= diff;
        }
    }
}