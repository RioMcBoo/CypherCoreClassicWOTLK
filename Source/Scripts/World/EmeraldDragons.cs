// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Scripts.World.Achievements
{
    struct CreatureIds
    {
        public const int DragonYsondre = 14887;
        public const int DragonLethon = 14888;
        public const int DragonEmeriss = 14889;
        public const int DragonTaerar = 14890;
        public const int DreamFog = 15224;

        //Ysondre
        public const int DementedDruid = 15260;

        //Lethon
        public const int SpiritShade = 15261;
    }

    struct SpellIds
    {
        public const int TailSweep = 15847;    // Tail Sweep - Slap Everything Behind Dragon (2 Seconds Interval)
        public const int SummonPlayer = 24776;    // Teleport Highest Threat Player In Front Of Dragon If Wandering Off
        public const int DreamFog = 24777;    // Auraspell For Dream Fog Npc (15224)
        public const int Sleep = 24778;    // Sleep Triggerspell (Used For Dream Fog)
        public const int SeepingFogLeft = 24813;    // Dream Fog - Summon Left
        public const int SeepingFogRight = 24814;    // Dream Fog - Summon Right
        public const int NoxiousBreath = 24818;
        public const int MarkOfNature = 25040;    // Mark Of Nature Trigger (Applied On Target Death - 15 Minutes Of Being Suspectible To Aura Of Nature)
        public const int MarkOfNatureAura = 25041;    // Mark Of Nature (Passive Marker-Test; Ticks Every 10 Seconds From Boss; Triggers Spellid 25042 (Scripted)
        public const int AuraOfNature = 25043;    // Stun For 2 Minutes (Used When public const int MarkOfNature Exists On The Target)

        //Ysondre
        public const int LightningWave = 24819;
        public const int SummonDruidSpirits = 24795;

        //Lethon
        public const int DrawSpirit = 24811;
        public const int ShadowBoltWhirl = 24834;
        public const int DarkOffering = 24804;

        //Emeriss
        public const int PutridMushroom = 24904;
        public const int CorruptionOfEarth = 24910;
        public const int VolatileInfection = 24928;

        //Taerar
        public const int BellowingRoar = 22686;
        public const int Shade = 24313;
        public const int ArcaneBlast = 24857;

        public static int[] TaerarShadeSpells = [24841, 24842, 24843];
    }

    struct TextIds
    {
        //Ysondre
        public const int SayYsondreAggro = 0;
        public const int SayYsondreSummonDruids = 1;

        //Lethon
        public const int SayLethonAggro = 0;
        public const int SayLethonDrawSpirit = 1;

        //Emeriss
        public const int SayEmerissAggro = 0;
        public const int SayEmerissCastCorruption = 1;

        //Taerar
        public const int SayTaerarAggro = 0;
        public const int SayTaerarSummonShades = 1;
    }

    class emerald_dragon : WorldBossAI
    {
        public emerald_dragon(Creature creature) : base(creature) { }

        public override void Reset()
        {
            base.Reset();
            me.RemoveUnitFlag(UnitFlags.NonAttackable);
            me.SetUninteractible(false);
            me.SetReactState(ReactStates.Aggressive);
            DoCast(me, SpellIds.MarkOfNatureAura, true);

            _scheduler.Schedule(Time.SpanFromSeconds(4), task =>
            {
                // Tail Sweep is cast every two seconds, no matter what goes on in front of the dragon
                DoCast(me, SpellIds.TailSweep);
                task.Repeat(Time.SpanFromSeconds(2));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(7.5), Time.SpanFromSeconds(15), task =>
            {
                // Noxious Breath is cast on random intervals, no less than 7.5 seconds between
                DoCast(me, SpellIds.NoxiousBreath);
                task.Repeat(Time.SpanFromSeconds(7.5), Time.SpanFromSeconds(15));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(12.5), Time.SpanFromSeconds(20), task =>
            {
                // Seeping Fog appears only as "pairs", and only One pair at any given time!
                // Despawntime is 2 minutes, so reschedule it for new cast after 2 minutes + a minor "random time" (30 seconds at max)
                DoCast(me, SpellIds.SeepingFogLeft, true);
                DoCast(me, SpellIds.SeepingFogRight, true);
                task.Repeat(Time.SpanFromMinutes(2), Time.SpanFromMinutes(2.5));
            });
        }

        // Target killed during encounter, mark them as suspectible for Aura Of Nature
        public override void KilledUnit(Unit who)
        {
            if (who.IsPlayer())
                who.CastSpell(who, SpellIds.MarkOfNature, true);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);

            if (me.HasUnitState(UnitState.Casting))
                return;

            Unit target = SelectTarget(SelectTargetMethod.MaxThreat, 0, -50.0f, true);
            if (target != null)
                DoCast(target, SpellIds.SummonPlayer);
        }
    }

    [Script]
    class npc_dream_fog : ScriptedAI
    {
        public npc_dream_fog(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            _scheduler.Schedule(Time.SpanFromSeconds(0), task =>
            {
                // Chase target, but don't attack - otherwise just roam around
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 0.0f, true);
                if (target != null)
                {
                    task.Repeat(Time.SpanFromSeconds(15), Time.SpanFromSeconds(30));
                    me.GetMotionMaster().Clear();
                    me.GetMotionMaster().MoveChase(target, 0.2f);
                }
                else
                {
                    task.Repeat(Time.SpanFromSeconds(2.5));
                    me.GetMotionMaster().Clear();
                    me.GetMotionMaster().MoveRandom(25.0f);
                }
                // Seeping fog movement is slow enough for a player to be able to walk backwards and still outpace it
                me.SetWalk(true);
                me.SetSpeedRate(UnitMoveType.Walk, 0.75f);
            });
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }

    [Script]
    class boss_ysondre : emerald_dragon
    {
        byte _stage;

        public boss_ysondre(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            _stage = 1;
        }

        public override void Reset()
        {
            Initialize();
            base.Reset();

            _scheduler.Schedule(Time.SpanFromSeconds(12), task =>
            {
                DoCastVictim(SpellIds.LightningWave);
                task.Repeat(Time.SpanFromSeconds(10), Time.SpanFromSeconds(20));
            });
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayYsondreAggro);
            base.JustEngagedWith(who);
        }

        // Summon druid spirits on 75%, 50% and 25% health
        public override void DamageTaken(Unit attacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (!HealthAbovePct(100 - 25 * _stage))
            {
                Talk(TextIds.SayYsondreSummonDruids);

                for (byte i = 0; i < 10; ++i)
                    DoCast(me, SpellIds.SummonDruidSpirits, true);
                ++_stage;
            }
        }
    }

    [Script]
    class boss_lethon : emerald_dragon
    {
        byte _stage;

        public boss_lethon(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            _stage = 1;
        }

        public override void Reset()
        {
            Initialize();
            base.Reset();
            _scheduler.Schedule(Time.SpanFromSeconds(10), task =>
            {
                me.CastSpell(null, SpellIds.ShadowBoltWhirl, false);
                task.Repeat(Time.SpanFromSeconds(15), Time.SpanFromSeconds(30));
            });
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayLethonAggro);
            base.JustEngagedWith(who);
        }

        public override void DamageTaken(Unit attacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (!HealthAbovePct(100 - 25 * _stage))
            {
                Talk(TextIds.SayLethonDrawSpirit);
                DoCast(me, SpellIds.DrawSpirit);
                ++_stage;
            }
        }

        public override void SpellHitTarget(WorldObject target, SpellInfo spellInfo)
        {
            if (spellInfo.Id == SpellIds.DrawSpirit && target.IsPlayer())
            {
                Position targetPos = target.GetPosition();
                me.SummonCreature(CreatureIds.SpiritShade, targetPos, TempSummonType.TimedDespawnOutOfCombat, Time.SpanFromSeconds(50));
            }
        }
    }

    [Script]
    class npc_spirit_shade : PassiveAI
    {
        ObjectGuid _summonerGuid;

        public npc_spirit_shade(Creature creature) : base(creature) { }

        public override void IsSummonedBy(WorldObject summonerWO)
        {
            Unit summoner = summonerWO.ToUnit();
            if (summoner == null)
                return;

            _summonerGuid = summoner.GetGUID();
            me.GetMotionMaster().MoveFollow(summoner, 0.0f, 0.0f);
        }

        public override void MovementInform(MovementGeneratorType moveType, int data)
        {
            if (moveType == MovementGeneratorType.Follow && data == _summonerGuid.GetCounter())
            {
                me.CastSpell(null, SpellIds.DarkOffering, false);
                me.DespawnOrUnsummon(Time.SpanFromSeconds(1));
            }
        }
    }

    [Script]
    class boss_emeriss : emerald_dragon
    {
        byte _stage;

        public boss_emeriss(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            _stage = 1;
        }

        public override void Reset()
        {
            Initialize();
            base.Reset();

            _scheduler.Schedule(Time.SpanFromSeconds(12), task =>
            {
                DoCastVictim(SpellIds.VolatileInfection);
                task.Repeat(Time.SpanFromSeconds(120));
            });
        }

        public override void KilledUnit(Unit who)
        {
            if (who.IsPlayer())
                DoCast(who, SpellIds.PutridMushroom, true);

            base.KilledUnit(who);
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayEmerissAggro);
            base.JustEngagedWith(who);
        }

        public override void DamageTaken(Unit attacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (!HealthAbovePct(100 - 25 * _stage))
            {
                Talk(TextIds.SayEmerissCastCorruption);
                DoCast(me, SpellIds.CorruptionOfEarth, true);
                ++_stage;
            }
        }
    }

    [Script]
    class boss_taerar : emerald_dragon
    {
        bool _banished;                              // used for shades activation testing
        TimeSpan _banishedTimer;                         // counter for banishment timeout
        byte _shades;                                // keep track of how many shades are dead
        byte _stage;                                 // check which "shade phase" we're at (75-50-25 percentage counters)

        public boss_taerar(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            _stage = 1;
            _shades = 0;
            _banished = false;
            _banishedTimer = TimeSpan.Zero;
        }

        public override void Reset()
        {
            me.RemoveAurasDueToSpell(SpellIds.Shade);

            Initialize();

            base.Reset();

            _scheduler.Schedule(Time.SpanFromSeconds(12), task =>
            {
                DoCast(SpellIds.ArcaneBlast);
                task.Repeat(Time.SpanFromSeconds(7), Time.SpanFromSeconds(12));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(30), task =>
            {
                DoCast(SpellIds.BellowingRoar);
                task.Repeat(Time.SpanFromSeconds(20), Time.SpanFromSeconds(30));
            });
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayTaerarAggro);
            base.JustEngagedWith(who);
        }

        public override void SummonedCreatureDies(Creature summon, Unit killer)
        {
            --_shades;
        }

        public override void DamageTaken(Unit attacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            // At 75, 50 or 25 percent health, we need to activate the shades and go "banished"
            // Note: _stage holds the amount of times they have been summoned
            if (!_banished && !HealthAbovePct(100 - 25 * _stage))
            {
                _banished = true;
                _banishedTimer = (Minutes)1;

                me.InterruptNonMeleeSpells(false);
                DoStopAttack();

                Talk(TextIds.SayTaerarSummonShades);

                foreach (var spell in SpellIds.TaerarShadeSpells)
                    DoCastVictim(spell, true);

                _shades += (byte)SpellIds.TaerarShadeSpells.Length;

                DoCast(SpellIds.Shade);
                me.SetUnitFlag(UnitFlags.NonAttackable);
                me.SetUninteractible(true);
                me.SetReactState(ReactStates.Passive);

                ++_stage;
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!me.IsInCombat())
                return;

            if (_banished)
            {
                // If all three shades are dead, Or it has taken too long, end the current event and get Taerar back into buMath.Siness
                if (_banishedTimer <= diff || _shades == 0)
                {
                    _banished = false;

                    me.RemoveUnitFlag(UnitFlags.NonAttackable);
                    me.SetUninteractible(false);
                    me.RemoveAurasDueToSpell(SpellIds.Shade);
                    me.SetReactState(ReactStates.Aggressive);
                }
                // _banishtimer has not expired, and we still have active shades:
                else
                    _banishedTimer -= diff;

                // Update the events before we return (handled under emerald_dragonAI.UpdateAI(diff); if we're not inside this check)
                _scheduler.Update(diff);

                return;
            }

            base.UpdateAI(diff);
        }
    }

    [Script] // 24778 - Sleep
    class spell_dream_fog_sleep_SpellScript : SpellScript
    {
        void FilterTargets(List<WorldObject> targets)
        {
            targets.RemoveAll(obj =>
            {
                Unit unit = obj.ToUnit();
                if (unit != null)
                    return unit.HasAura(SpellIds.Sleep);
                return true;
            });
        }

        public override void Register()
        {
            OnObjectAreaTargetSelect.Add(new(FilterTargets, 0, Targets.UnitDestAreaEnemy));
        }
    }

    [Script] // 25042 - Triggerspell - Mark of Nature
    class spell_mark_of_nature_SpellScript : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.MarkOfNature, SpellIds.AuraOfNature);
        }

        void FilterTargets(List<WorldObject> targets)
        {
            targets.RemoveAll(obj =>
            {
                // return those not tagged or already under the influence of Aura of Nature
                Unit unit = obj.ToUnit();
                if (unit != null)
                    return !(unit.HasAura(SpellIds.MarkOfNature) && !unit.HasAura(SpellIds.AuraOfNature));
                return true;
            });
        }

        void HandleEffect(int effIndex)
        {
            PreventHitDefaultEffect(effIndex);
            GetHitUnit().CastSpell(GetHitUnit(), SpellIds.AuraOfNature, true);
        }

        public override void Register()
        {
            OnObjectAreaTargetSelect.Add(new(FilterTargets, 0, Targets.UnitSrcAreaEnemy));
            OnEffectHitTarget.Add(new(HandleEffect, 0, SpellEffectName.ApplyAura));
        }
    }
}