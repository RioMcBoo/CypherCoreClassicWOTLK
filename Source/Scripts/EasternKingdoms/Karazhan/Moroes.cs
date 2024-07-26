// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scripts.EasternKingdoms.Karazhan.Moroes
{
    struct SpellIds
    {
        public const int Vanish = 29448;
        public const int Garrote = 37066;
        public const int Blind = 34694;
        public const int Gouge = 29425;
        public const int Frenzy = 37023;

        // Adds
        public const int Manaburn = 29405;
        public const int Mindfly = 29570;
        public const int Swpain = 34441;
        public const int Shadowform = 29406;

        public const int Hammerofjustice = 13005;
        public const int Judgementofcommand = 29386;
        public const int Sealofcommand = 29385;

        public const int Dispelmagic = 15090;
        public const int Greaterheal = 29564;
        public const int Holyfire = 29563;
        public const int Pwshield = 29408;

        public const int Cleanse = 29380;
        public const int Greaterblessofmight = 29381;
        public const int Holylight = 29562;
        public const int Divineshield = 41367;

        public const int Hamstring = 9080;
        public const int Mortalstrike = 29572;
        public const int Whirlwind = 29573;

        public const int Disarm = 8379;
        public const int Heroicstrike = 29567;
        public const int Shieldbash = 11972;
        public const int Shieldwall = 29390;
    }

    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SaySpecial = 1;
        public const int SayKill = 2;
        public const int SayDeath = 3;
    }

    struct MiscConst
    {
        public const uint GroupNonEnrage = 1;

        public static Position[] Locations =
        [
            new Position(-10991.0f, -1884.33f, 81.73f, 0.614315f),
            new Position(-10989.4f, -1885.88f, 81.73f, 0.904913f),
            new Position(-10978.1f, -1887.07f, 81.73f, 2.035550f),
            new Position(-10975.9f, -1885.81f, 81.73f, 2.253890f)
        ];

        public static int[] Adds =
        [
            17007,
            19872,
            19873,
            19874,
            19875,
            19876,
        ];
    }

    [Script]
    class boss_moroes : BossAI
    {
        public ObjectGuid[] AddGUID = new ObjectGuid[4];
        int[] AddId = new int[4];

        bool InVanish;
        bool Enrage;

        public boss_moroes(Creature creature) : base(creature, DataTypes.Moroes)
        {
            Initialize();
        }

        void Initialize()
        {
            Enrage = false;
            InVanish = false;
        }

        public override void Reset()
        {
            Initialize();
            if (me.IsAlive())
                SpawnAdds();

            instance.SetBossState(DataTypes.Moroes, EncounterState.NotStarted);
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);

            _scheduler.Schedule(Time.SpanFromSeconds(5), MiscConst.GroupNonEnrage, task =>
            {
                for (byte i = 0; i < 4; ++i)
                {
                    if (!AddGUID[i].IsEmpty())
                    {
                        Creature temp = ObjectAccessor.GetCreature(me, AddGUID[i]);
                        if (temp != null && temp.IsAlive())
                            if (temp.GetVictim() == null)
                                temp.GetAI().AttackStart(me.GetVictim());
                    }
                }
                task.Repeat();
            });
            _scheduler.Schedule(Time.SpanFromSeconds(23), MiscConst.GroupNonEnrage, task =>
            {
                DoCastVictim(SpellIds.Gouge);
                task.Repeat(Time.SpanFromSeconds(40));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(30), MiscConst.GroupNonEnrage, task =>
            {
                DoCast(me, SpellIds.Vanish);
                me.SetCanMelee(false);
                InVanish = true;

                task.Schedule(Time.SpanFromSeconds(5), garroteTask =>
                {
                    Talk(TextIds.SaySpecial);

                    Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                    if (target != null)
                        target.CastSpell(target, SpellIds.Garrote, true);

                    InVanish = false;
                    me.SetCanMelee(true);
                });

                task.Repeat();
            });
            _scheduler.Schedule(Time.SpanFromSeconds(35), MiscConst.GroupNonEnrage, task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.MinDistance, 0, 0.0f, true, false);
                if (target != null)
                    DoCast(target, SpellIds.Blind);
                task.Repeat(Time.SpanFromSeconds(40));
            });

            Talk(TextIds.SayAggro);
            AddsAttack();
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SayKill);
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayDeath);

            base.JustDied(killer);

            DeSpawnAdds();

            //remove aura from spell Garrote when Moroes dies
            instance.DoRemoveAurasDueToSpellOnPlayers(SpellIds.Garrote);
        }

        void SpawnAdds()
        {
            DeSpawnAdds();

            if (isAddlistEmpty())
            {
                List<int> AddList = MiscConst.Adds.ToList();
                AddList.RandomResize(4);
                
                for (var i = 0; i < 4; ++i)
                {
                    Creature creature = 
                        me.SummonCreature(AddList[i], MiscConst.Locations[i], 
                        TempSummonType.CorpseTimedDespawn, Time.SpanFromSeconds(10));

                    if (creature != null)
                    {
                        AddGUID[i] = creature.GetGUID();
                        AddId[i] = AddList[i];
                    }
                }
            }
            else
            {
                for (byte i = 0; i < 4; ++i)
                {
                    Creature creature = 
                        me.SummonCreature(AddId[i], MiscConst.Locations[i], 
                        TempSummonType.CorpseTimedDespawn, Time.SpanFromSeconds(10));

                    if (creature != null)
                        AddGUID[i] = creature.GetGUID();
                }
            }
        }

        bool isAddlistEmpty()
        {
            for (byte i = 0; i < 4; ++i)
                if (AddId[i] == 0)
                    return true;

            return false;
        }

        void DeSpawnAdds()
        {
            for (byte i = 0; i < 4; ++i)
            {
                if (!AddGUID[i].IsEmpty())
                {
                    Creature temp = ObjectAccessor.GetCreature(me, AddGUID[i]);
                    if (temp != null)
                        temp.DespawnOrUnsummon();
                }
            }
        }

        void AddsAttack()
        {
            for (byte i = 0; i < 4; ++i)
            {
                if (!AddGUID[i].IsEmpty())
                {
                    Creature temp = ObjectAccessor.GetCreature((me), AddGUID[i]);
                    if (temp != null && temp.IsAlive())
                    {
                        temp.GetAI().AttackStart(me.GetVictim());
                        DoZoneInCombat(temp);
                    }
                    else
                        EnterEvadeMode();
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            if (!Enrage && HealthBelowPct(30))
            {
                DoCast(me, SpellIds.Frenzy);
                Enrage = true;
                _scheduler.CancelGroup(MiscConst.GroupNonEnrage);
            }

            _scheduler.Update(diff);
        }
    }

    class boss_moroes_guest : ScriptedAI
    {
        InstanceScript instance;

        ObjectGuid[] GuestGUID = new ObjectGuid[4];

        public boss_moroes_guest(Creature creature) : base(creature)
        {
            instance = creature.GetInstanceScript();
        }

        public override void Reset()
        {
            instance.SetBossState(DataTypes.Moroes, EncounterState.NotStarted);
        }

        public void AcquireGUID()
        {
            Creature Moroes = ObjectAccessor.GetCreature(me, instance.GetGuidData(DataTypes.Moroes));
            if (Moroes != null)
            {
                for (byte i = 0; i < 4; ++i)
                {
                    ObjectGuid Guid = Moroes.GetAI<boss_moroes>().AddGUID[i];
                    if (!Guid.IsEmpty())
                        GuestGUID[i] = Guid;
                }
            }
        }

        public Unit SelectGuestTarget()
        {
            ObjectGuid TempGUID = GuestGUID[RandomHelper.Rand32() % 4];
            if (!TempGUID.IsEmpty())
            {
                Unit unit = Global.ObjAccessor.GetUnit(me, TempGUID);
                if (unit != null && unit.IsAlive())
                    return unit;
            }

            return me;
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (instance.GetBossState(DataTypes.Moroes) != EncounterState.InProgress)
                EnterEvadeMode();
        }
    }

    [Script]
    class boss_baroness_dorothea_millstipe : boss_moroes_guest
    {
        //Shadow Priest
        public boss_baroness_dorothea_millstipe(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            ManaBurn_Timer = (Seconds)7;
            MindFlay_Timer = (Seconds)1;
            ShadowWordPain_Timer = (Seconds)6;
        }

        TimeSpan ManaBurn_Timer;
        TimeSpan MindFlay_Timer;
        TimeSpan ShadowWordPain_Timer;

        public override void Reset()
        {
            Initialize();

            DoCast(me, SpellIds.Shadowform, new CastSpellExtraArgs(true));

            base.Reset();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            base.UpdateAI(diff);

            if (MindFlay_Timer <= diff)
            {
                DoCastVictim(SpellIds.Mindfly);
                MindFlay_Timer = (Seconds)12;                         // 3 sec channeled
            }
            else MindFlay_Timer -= diff;

            if (ManaBurn_Timer <= diff)
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                if (target != null)
                {
                    if (target.GetPowerType() == PowerType.Mana)
                        DoCast(target, SpellIds.Manaburn);
                }

                ManaBurn_Timer = (Seconds)5;                          // 3 sec cast
            }
            else ManaBurn_Timer -= diff;

            if (ShadowWordPain_Timer <= diff)
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                if (target != null)
                {
                    DoCast(target, SpellIds.Swpain);
                    ShadowWordPain_Timer = (Seconds)7;
                }
            }
            else ShadowWordPain_Timer -= diff;
        }
    }

    [Script]
    class boss_baron_rafe_dreuger : boss_moroes_guest
    {
        //Retr Pally
        public boss_baron_rafe_dreuger(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            HammerOfJustice_Timer = (Seconds)1;
            SealOfCommand_Timer = (Seconds)7;
            JudgementOfCommand_Timer = SealOfCommand_Timer + (Seconds)29;
        }

        TimeSpan HammerOfJustice_Timer;
        TimeSpan SealOfCommand_Timer;
        TimeSpan JudgementOfCommand_Timer;

        public override void Reset()
        {
            Initialize();

            base.Reset();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            base.UpdateAI(diff);

            if (SealOfCommand_Timer <= diff)
            {
                DoCast(me, SpellIds.Sealofcommand);
                SealOfCommand_Timer = (Seconds)32;
                JudgementOfCommand_Timer = (Seconds)29;
            }
            else SealOfCommand_Timer -= diff;

            if (JudgementOfCommand_Timer <= diff)
            {
                DoCastVictim(SpellIds.Judgementofcommand);
                JudgementOfCommand_Timer = SealOfCommand_Timer + (Seconds)29;
            }
            else JudgementOfCommand_Timer -= diff;

            if (HammerOfJustice_Timer <= diff)
            {
                DoCastVictim(SpellIds.Hammerofjustice);
                HammerOfJustice_Timer = (Seconds)12;
            }
            else HammerOfJustice_Timer -= diff;
        }
    }

    [Script]
    class boss_lady_catriona_von_indi : boss_moroes_guest
    {
        //Holy Priest
        public boss_lady_catriona_von_indi(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            DispelMagic_Timer = (Seconds)11;
            GreaterHeal_Timer = (Milliseconds)1500;
            HolyFire_Timer = (Seconds)5;
            PowerWordShield_Timer = (Seconds)1;
        }

        TimeSpan DispelMagic_Timer;
        TimeSpan GreaterHeal_Timer;
        TimeSpan HolyFire_Timer;
        TimeSpan PowerWordShield_Timer;

        public override void Reset()
        {
            Initialize();

            AcquireGUID();

            base.Reset();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            base.UpdateAI(diff);

            if (PowerWordShield_Timer <= diff)
            {
                DoCast(me, SpellIds.Pwshield);
                PowerWordShield_Timer = (Seconds)15;
            }
            else PowerWordShield_Timer -= diff;

            if (GreaterHeal_Timer <= diff)
            {
                Unit target = SelectGuestTarget();

                DoCast(target, SpellIds.Greaterheal);
                GreaterHeal_Timer = (Seconds)17;
            }
            else GreaterHeal_Timer -= diff;

            if (HolyFire_Timer <= diff)
            {
                DoCastVictim(SpellIds.Holyfire);
                HolyFire_Timer = (Seconds)22;
            }
            else HolyFire_Timer -= diff;

            if (DispelMagic_Timer <= diff)
            {
                Unit target = 
                    RandomHelper.RAND(SelectGuestTarget(), SelectTarget(SelectTargetMethod.Random, 0, 100, true));

                if (target != null)
                    DoCast(target, SpellIds.Dispelmagic);

                DispelMagic_Timer = (Seconds)25;
            }
            else DispelMagic_Timer -= diff;
        }
    }

    [Script]
    class boss_lady_keira_berrybuck : boss_moroes_guest
    {
        //Holy Pally
        public boss_lady_keira_berrybuck(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            Cleanse_Timer = (Seconds)13;
            GreaterBless_Timer = (Seconds)1;
            HolyLight_Timer = (Seconds)7;
            DivineShield_Timer = (Seconds)31;
        }

        TimeSpan Cleanse_Timer;
        TimeSpan GreaterBless_Timer;
        TimeSpan HolyLight_Timer;
        TimeSpan DivineShield_Timer;

        public override void Reset()
        {
            Initialize();

            AcquireGUID();

            base.Reset();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            base.UpdateAI(diff);

            if (DivineShield_Timer <= diff)
            {
                DoCast(me, SpellIds.Divineshield);
                DivineShield_Timer = Time.SpanFromMilliseconds(31000);
            }
            else DivineShield_Timer -= diff;

            if (HolyLight_Timer <= diff)
            {
                Unit target = SelectGuestTarget();

                DoCast(target, SpellIds.Holylight);
                HolyLight_Timer = Time.SpanFromMilliseconds(10000);
            }
            else HolyLight_Timer -= diff;

            if (GreaterBless_Timer <= diff)
            {
                Unit target = SelectGuestTarget();

                DoCast(target, SpellIds.Greaterblessofmight);

                GreaterBless_Timer = Time.SpanFromMilliseconds(50000);
            }
            else GreaterBless_Timer -= diff;

            if (Cleanse_Timer <= diff)
            {
                Unit target = SelectGuestTarget();

                DoCast(target, SpellIds.Cleanse);

                Cleanse_Timer = Time.SpanFromMilliseconds(10000);
            }
            else Cleanse_Timer -= diff;
        }
    }

    [Script]
    class boss_lord_robin_daris : boss_moroes_guest
    {
        //Arms Warr
        public boss_lord_robin_daris(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            Hamstring_Timer = Time.SpanFromMilliseconds(7000);
            MortalStrike_Timer = Time.SpanFromMilliseconds(10000);
            WhirlWind_Timer = Time.SpanFromMilliseconds(21000);
        }

        TimeSpan Hamstring_Timer;
        TimeSpan MortalStrike_Timer;
        TimeSpan WhirlWind_Timer;

        public override void Reset()
        {
            Initialize();

            base.Reset();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            base.UpdateAI(diff);

            if (Hamstring_Timer <= diff)
            {
                DoCastVictim(SpellIds.Hamstring);
                Hamstring_Timer = Time.SpanFromMilliseconds(12000);
            }
            else Hamstring_Timer -= diff;

            if (MortalStrike_Timer <= diff)
            {
                DoCastVictim(SpellIds.Mortalstrike);
                MortalStrike_Timer = Time.SpanFromMilliseconds(18000);
            }
            else MortalStrike_Timer -= diff;

            if (WhirlWind_Timer <= diff)
            {
                DoCast(me, SpellIds.Whirlwind);
                WhirlWind_Timer = Time.SpanFromMilliseconds(21000);
            }
            else WhirlWind_Timer -= diff;
        }
    }

    [Script]
    class boss_lord_crispin_ference : boss_moroes_guest
    {
        //Arms Warr
        public boss_lord_crispin_ference(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            Disarm_Timer = Time.SpanFromMilliseconds(6000);
            HeroicStrike_Timer = Time.SpanFromMilliseconds(10000);
            ShieldBash_Timer = Time.SpanFromMilliseconds(8000);
            ShieldWall_Timer = Time.SpanFromMilliseconds(4000);
        }

        TimeSpan Disarm_Timer;
        TimeSpan HeroicStrike_Timer;
        TimeSpan ShieldBash_Timer;
        TimeSpan ShieldWall_Timer;

        public override void Reset()
        {
            Initialize();

            base.Reset();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            base.UpdateAI(diff);

            if (Disarm_Timer <= diff)
            {
                DoCastVictim(SpellIds.Disarm);
                Disarm_Timer = Time.SpanFromMilliseconds(12000);
            }
            else Disarm_Timer -= diff;

            if (HeroicStrike_Timer <= diff)
            {
                DoCastVictim(SpellIds.Heroicstrike);
                HeroicStrike_Timer = Time.SpanFromMilliseconds(10000);
            }
            else HeroicStrike_Timer -= diff;

            if (ShieldBash_Timer <= diff)
            {
                DoCastVictim(SpellIds.Shieldbash);
                ShieldBash_Timer = Time.SpanFromMilliseconds(13000);
            }
            else ShieldBash_Timer -= diff;

            if (ShieldWall_Timer <= diff)
            {
                DoCast(me, SpellIds.Shieldwall);
                ShieldWall_Timer = Time.SpanFromMilliseconds(21000);
            }
            else ShieldWall_Timer -= diff;
        }
    }
}