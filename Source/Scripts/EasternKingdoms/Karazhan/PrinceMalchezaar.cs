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
using System.Numerics;

namespace Scripts.EasternKingdoms.Karazhan.PrinceMalchezaar
{
    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SayAxeToss1 = 1;
        public const int SayAxeToss2 = 2;
        //public const int SaySpecial1                = 3; Not used, needs to be implemented, but I don't know where it should be used.
        //public const int SaySpecial2                = 4; Not used, needs to be implemented, but I don't know where it should be used.
        //public const int SaySpecial3                = 5; Not used, needs to be implemented, but I don't know where it should be used.
        public const int SaySlay = 6;
        public const int SaySummon = 7;
        public const int SayDeath = 8;
    }

    struct SpellIds
    {
        public const int Enfeeble = 30843;                       //Enfeeble during phase 1 and 2
        public const int EnfeebleEffect = 41624;

        public const int Shadownova = 30852;                       //Shadownova used during all phases
        public const int SwPain = 30854;                       //Shadow word pain during phase 1 and 3 (different targeting rules though)
        public const int ThrashPassive = 12787;                       //Extra attack Chance during phase 2
        public const int SunderArmor = 30901;                       //Sunder armor during phase 2
        public const int ThrashAura = 12787;                       //Passive proc Chance for thrash
        public const int EquipAxes = 30857;                       //Visual for axe equiping
        public const int AmplifyDamage = 39095;                       //Amplifiy during phase 3
        public const int Cleave = 30131;                     //Same as Nightbane.
        public const int Hellfire = 30859;                       //Infenals' hellfire aura

        public const int InfernalRelay = 30834;
    }

    struct MiscConst
    {
        public const int TotalInfernalPoints = 18;
        public const int NetherspiteInfernal = 17646;                       //The netherspite infernal creature
        public const int MalchezarsAxe = 17650;                       //Malchezar's axes (creatures), summoned during phase 3

        public const int InfernalModelInvisible = 11686;                       //Infernal Effects
        public const int EquipIdAxe = 33542;                      //Axes info
    }

    [Script]
    class netherspite_infernal : ScriptedAI
    {
        public ObjectGuid Malchezaar;
        public Vector2 Point;

        public netherspite_infernal(Creature creature) : base(creature) { }

        public override void Reset() { }

        public override void JustEngagedWith(Unit who) { }

        public override void MoveInLineOfSight(Unit who) { }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }

        public override void KilledUnit(Unit who)
        {
            Unit unit = Global.ObjAccessor.GetUnit(me, Malchezaar);
            if (unit != null)
            {
                Creature creature = unit.ToCreature();
                if (creature != null)
                    creature.GetAI().KilledUnit(who);
            }
        }

        public override void SpellHit(WorldObject caster, SpellInfo spellInfo)
        {
            if (spellInfo.Id == SpellIds.InfernalRelay)
            {
                me.SetDisplayId(me.GetNativeDisplayId());
                me.SetUninteractible(true);

                _scheduler.Schedule(Time.SpanFromSeconds(4), task => DoCast(me, SpellIds.Hellfire));
                _scheduler.Schedule(Time.SpanFromSeconds(170), task =>
                {
                    Creature pMalchezaar = ObjectAccessor.GetCreature(me, Malchezaar);

                    if (pMalchezaar != null && pMalchezaar.IsAlive())
                        pMalchezaar.GetAI<boss_malchezaar>().Cleanup(me, Point);
                });
            }
        }

        public override void DamageTaken(Unit done_by, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (done_by == null || done_by.GetGUID() != Malchezaar)
                damage = 0;
        }
    }

    [Script]
    class boss_malchezaar : ScriptedAI
    {
        static Vector2[] InfernalPoints =
        [
            new Vector2(-10922.8f, -1985.2f),
            new Vector2(-10916.2f, -1996.2f),
            new Vector2(-10932.2f, -2008.1f),
            new Vector2(-10948.8f, -2022.1f),
            new Vector2(-10958.7f, -1997.7f),
            new Vector2(-10971.5f, -1997.5f),
            new Vector2(-10990.8f, -1995.1f),
            new Vector2(-10989.8f, -1976.5f),
            new Vector2(-10971.6f, -1973.0f),
            new Vector2(-10955.5f, -1974.0f),
            new Vector2(-10939.6f, -1969.8f),
            new Vector2(-10958.0f, -1952.2f),
            new Vector2(-10941.7f, -1954.8f),
            new Vector2(-10943.1f, -1988.5f),
            new Vector2(-10948.8f, -2005.1f),
            new Vector2(-10984.0f, -2019.3f),
            new Vector2(-10932.8f, -1979.6f),
            new Vector2(-10935.7f, -1996.0f)
        ];

        InstanceScript instance;
        TimeSpan EnfeebleTimer;
        TimeSpan EnfeebleResetTimer;
        TimeSpan ShadowNovaTimer;
        TimeSpan SWPainTimer;
        TimeSpan SunderArmorTimer;
        TimeSpan AmplifyDamageTimer;
        TimeSpan Cleave_Timer;
        TimeSpan InfernalTimer;
        TimeSpan AxesTargetSwitchTimer;
        TimeSpan InfernalCleanupTimer;

        List<ObjectGuid> infernals = new();
        List<Vector2> positions = new();

        ObjectGuid[] axes = new ObjectGuid[2];
        ObjectGuid[] enfeeble_targets = new ObjectGuid[5];
        long[] enfeeble_health = new long[5];

        int phase;

        public boss_malchezaar(Creature creature) : base(creature)
        {
            Initialize();

            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            EnfeebleTimer = Time.SpanFromMilliseconds(30000);
            EnfeebleResetTimer = Time.SpanFromMilliseconds(38000);
            ShadowNovaTimer = Time.SpanFromMilliseconds(35500);
            SWPainTimer = Time.SpanFromMilliseconds(20000);
            AmplifyDamageTimer = Time.SpanFromMilliseconds(5000);
            Cleave_Timer = Time.SpanFromMilliseconds(8000);
            InfernalTimer = Time.SpanFromMilliseconds(40000);
            InfernalCleanupTimer = Time.SpanFromMilliseconds(47000);
            AxesTargetSwitchTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(7500, 20000));
            SunderArmorTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(5000, 10000));
            phase = 1;

            for (byte i = 0; i < 5; ++i)
            {
                enfeeble_targets[i].Clear();
                enfeeble_health[i] = 0;
            }
        }

        public override void Reset()
        {
            AxesCleanup();
            ClearWeapons();
            InfernalCleanup();
            positions.Clear();

            Initialize();

            for (byte i = 0; i < MiscConst.TotalInfernalPoints; ++i)
                positions.Add(InfernalPoints[i]);

            instance.HandleGameObject(instance.GetGuidData(DataTypes.GoNetherDoor), true);
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SaySlay);
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayDeath);

            AxesCleanup();
            ClearWeapons();
            InfernalCleanup();
            positions.Clear();

            for (byte i = 0; i < MiscConst.TotalInfernalPoints; ++i)
                positions.Add(InfernalPoints[i]);

            instance.HandleGameObject(instance.GetGuidData(DataTypes.GoNetherDoor), true);
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayAggro);

            instance.HandleGameObject(instance.GetGuidData(DataTypes.GoNetherDoor), false); // Open the door leading further in
        }

        void InfernalCleanup()
        {
            //Infernal Cleanup
            foreach (var guid in infernals)
            {
                Unit pInfernal = Global.ObjAccessor.GetUnit(me, guid);
                if (pInfernal != null && pInfernal.IsAlive())
                {
                    pInfernal.SetVisible(false);
                    pInfernal.SetDeathState(DeathState.JustDied);
                }
            }

            infernals.Clear();
        }

        void AxesCleanup()
        {
            for (byte i = 0; i < 2; ++i)
            {
                Unit axe = Global.ObjAccessor.GetUnit(me, axes[i]);
                if (axe != null && axe.IsAlive())
                    axe.KillSelf();
                axes[i].Clear();
            }
        }

        void ClearWeapons()
        {
            SetEquipmentSlots(false, 0, 0);
            me.SetCanDualWield(false);
        }

        void EnfeebleHealthEffect()
        {
            SpellInfo info = Global.SpellMgr.GetSpellInfo(SpellIds.EnfeebleEffect, GetDifficulty());
            if (info == null)
                return;

            Unit tank = me.GetThreatManager().GetCurrentVictim();
            List<Unit> targets = new();

            foreach (var refe in me.GetThreatManager().GetSortedThreatList())
            {
                Unit target = refe.GetVictim();
                if (target != tank && target.IsAlive() && target.IsPlayer())
                    targets.Add(target);
            }

            if (targets.Empty())
                return;

            //cut down to size if we have more than 5 targets
            targets.RandomResize(5);

            uint i = 0;
            foreach (var target in targets)
            {
                if (target != null)
                {
                    enfeeble_targets[i] = target.GetGUID();
                    enfeeble_health[i] = target.GetHealth();

                    CastSpellExtraArgs args = new(TriggerCastFlags.FullMask);
                    args.OriginalCaster = me.GetGUID();
                    target.CastSpell(target, SpellIds.Enfeeble, args);
                    target.SetHealth(1);
                }

                i++;
            }
        }

        void EnfeebleResetHealth()
        {
            for (byte i = 0; i < 5; ++i)
            {
                Unit target = Global.ObjAccessor.GetUnit(me, enfeeble_targets[i]);
                if (target != null && target.IsAlive())
                    target.SetHealth(enfeeble_health[i]);
                enfeeble_targets[i].Clear();
                enfeeble_health[i] = 0;
            }
        }

        void SummonInfernal(TimeSpan diff)
        {
            Vector2 point = Vector2.Zero;
            Position pos = null;
            if ((me.GetMapId() != 532) || positions.Empty())
                pos = me.GetRandomNearPosition(60);
            else
            {
                point = positions.SelectRandom();
                pos.Relocate(point.X, point.Y, 275.5f, RandomHelper.FRand(0.0f, (MathF.PI * 2)));
            }

            Creature infernal = me.SummonCreature(MiscConst.NetherspiteInfernal, pos, TempSummonType.TimedDespawn, (Minutes)3);

            if (infernal != null)
            {
                infernal.SetDisplayId(MiscConst.InfernalModelInvisible);
                infernal.SetFaction(me.GetFaction());
                if (point != Vector2.Zero)
                    infernal.GetAI<netherspite_infernal>().Point = point;
                infernal.GetAI<netherspite_infernal>().Malchezaar = me.GetGUID();

                infernals.Add(infernal.GetGUID());
                DoCast(infernal, SpellIds.InfernalRelay);
            }

            Talk(TextIds.SaySummon);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            if (EnfeebleResetTimer != default && EnfeebleResetTimer <= diff) // Let's not forget to reset that
            {
                EnfeebleResetHealth();
                EnfeebleResetTimer = default;
            }
            else EnfeebleResetTimer -= diff;

            if (me.HasUnitState(UnitState.Stunned))      // While shifting to phase 2 malchezaar stuns himself
                return;

            if (me.GetVictim() != null && me.GetTarget() != me.GetVictim().GetGUID())
                me.SetTarget(me.GetVictim().GetGUID());

            if (phase == 1)
            {
                if (HealthBelowPct(60))
                {
                    me.InterruptNonMeleeSpells(false);

                    phase = 2;

                    //animation
                    DoCast(me, SpellIds.EquipAxes);

                    //text
                    Talk(TextIds.SayAxeToss1);

                    //passive thrash aura
                    DoCast(me, SpellIds.ThrashAura, new CastSpellExtraArgs(true));

                    //models
                    SetEquipmentSlots(false, MiscConst.EquipIdAxe, MiscConst.EquipIdAxe);

                    me.SetBaseAttackTime(WeaponAttackType.OffAttack, (Milliseconds)(me.GetBaseAttackTime(WeaponAttackType.BaseAttack) * 150 / 100));
                    me.SetCanDualWield(true);
                }
            }
            else if (phase == 2)
            {
                if (HealthBelowPct(30))
                {
                    InfernalTimer = Time.SpanFromMilliseconds(15000);

                    phase = 3;

                    ClearWeapons();

                    //remove thrash
                    me.RemoveAurasDueToSpell(SpellIds.ThrashAura);

                    Talk(TextIds.SayAxeToss2);

                    Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                    for (byte i = 0; i < 2; ++i)
                    {
                        Creature axe = me.SummonCreature(MiscConst.MalchezarsAxe, me.GetPositionX(), me.GetPositionY(), me.GetPositionZ(), 0, TempSummonType.TimedDespawnOutOfCombat, (Seconds)1);
                        if (axe != null)
                        {
                            axe.SetUninteractible(true);
                            axe.SetFaction(me.GetFaction());
                            axes[i] = axe.GetGUID();
                            if (target != null)
                            {
                                axe.GetAI().AttackStart(target);
                                AddThreat(target, 10000000.0f, axe);
                            }
                        }
                    }

                    if (ShadowNovaTimer > Time.SpanFromMilliseconds(35000))
                        ShadowNovaTimer = EnfeebleTimer + Time.SpanFromMilliseconds(5000);

                    return;
                }

                if (SunderArmorTimer <= diff)
                {
                    DoCastVictim(SpellIds.SunderArmor);
                    SunderArmorTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(10000, 18000));
                }
                else SunderArmorTimer -= diff;

                if (Cleave_Timer <= diff)
                {
                    DoCastVictim(SpellIds.Cleave);
                    Cleave_Timer = Time.SpanFromMilliseconds(RandomHelper.IRand(6000, 12000));
                }
                else Cleave_Timer -= diff;
            }
            else
            {
                if (AxesTargetSwitchTimer <= diff)
                {
                    AxesTargetSwitchTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(7500, 20000));

                    Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                    if (target != null)
                    {
                        for (byte i = 0; i < 2; ++i)
                        {
                            Unit axe = Global.ObjAccessor.GetUnit(me, axes[i]);
                            if (axe != null)
                            {
                                if (axe.GetVictim() != null)
                                    ResetThreat(axe.GetVictim(), axe);
                                AddThreat(target, 1000000.0f, axe);
                            }
                        }
                    }
                }
                else AxesTargetSwitchTimer -= diff;

                if (AmplifyDamageTimer <= diff)
                {
                    Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                    if (target != null)
                        DoCast(target, SpellIds.AmplifyDamage);
                    AmplifyDamageTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(20000, 30000));
                }
                else AmplifyDamageTimer -= diff;
            }

            //Time for global and double timers
            if (InfernalTimer <= diff)
            {
                SummonInfernal(diff);
                InfernalTimer = phase == 3 ? (Milliseconds)14500 : (Milliseconds)44500;    // 15 secs in phase 3, 45 otherwise
            }
            else InfernalTimer -= diff;

            if (ShadowNovaTimer <= diff)
            {
                DoCastVictim(SpellIds.Shadownova);
                ShadowNovaTimer = phase == 3 ? (Milliseconds)31000 : Milliseconds.MaxValue;
            }
            else ShadowNovaTimer -= diff;

            if (phase != 2)
            {
                if (SWPainTimer <= diff)
                {
                    Unit target;
                    if (phase == 1)
                        target = me.GetVictim();        // the tank
                    else                                          // anyone but the tank
                        target = SelectTarget(SelectTargetMethod.Random, 1, 100, true);

                    if (target != null)
                        DoCast(target, SpellIds.SwPain);

                    SWPainTimer = (Milliseconds)20000;
                }
                else SWPainTimer -= diff;
            }

            if (phase != 3)
            {
                if (EnfeebleTimer <= diff)
                {
                    EnfeebleHealthEffect();
                    EnfeebleTimer = (Milliseconds)30000;
                    ShadowNovaTimer = (Milliseconds)5000;
                    EnfeebleResetTimer = (Milliseconds)9000;
                }
                else EnfeebleTimer -= diff;
            }

            if (phase == 2)
                DoMeleeAttacksIfReady();
        }

        void DoMeleeAttacksIfReady()
        {
            if (me.IsWithinMeleeRange(me.GetVictim()) && !me.IsNonMeleeSpellCast(false))
            {
                //Check for base attack
                if (me.IsAttackReady() && me.GetVictim() != null)
                {
                    me.AttackerStateUpdate(me.GetVictim());
                    me.ResetAttackTimer();
                }
                //Check for offhand attack
                if (me.IsAttackReady(WeaponAttackType.OffAttack) && me.GetVictim() != null)
                {
                    me.AttackerStateUpdate(me.GetVictim(), WeaponAttackType.OffAttack);
                    me.ResetAttackTimer(WeaponAttackType.OffAttack);
                }
            }
        }

        public void Cleanup(Creature infernal, Vector2 point)
        {
            foreach (var guid in infernals)
            {
                if (guid == infernal.GetGUID())
                {
                    infernals.Remove(guid);
                    break;
                }
            }

            positions.Add(point);
        }
    }
}