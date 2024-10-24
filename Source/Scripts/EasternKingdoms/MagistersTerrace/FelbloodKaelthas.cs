// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.MagistersTerrace.FelbloodKaelthas
{
    struct TextIds
    {
        // Kael'thas Sunstrider
        public const int SayIntro1 = 0;
        public const int SayIntro2 = 1;
        public const int SayGravityLapse1 = 2;
        public const int SayGravityLapse2 = 3;
        public const int SayPowerFeedback = 4;
        public const int SaySummonPhoenix = 5;
        public const int SayAnnouncePyroblast = 6;
        public const int SayFlameStrike = 7;
        public const int SayDeath = 8;
    }

    struct SpellIds
    {
        // Kael'thas Sunstrider
        public const int Fireball = 44189;
        public const int GravityLapse = 49887;
        public const int HGravityLapse = 44226;
        public const int GravityLapseCenterTeleport = 44218;
        public const int GravityLapseLeftTeleport = 44219;
        public const int GravityLapseFrontLeftTeleport = 44220;
        public const int GravityLapseFrontTeleport = 44221;
        public const int GravityLapseFrontRightTeleport = 44222;
        public const int GravityLapseRightTeleport = 44223;
        public const int GravityLapseInitial = 44224;
        public const int GravityLapseFly = 44227;
        public const int GravityLapseBeamVisualPeriodic = 44251;
        public const int SummonArcaneSphere = 44265;
        public const int FlameStrike = 46162;
        public const int ShockBarrier = 46165;
        public const int PowerFeedback = 44233;
        public const int HPowerFeedback = 47109;
        public const int Pyroblast = 36819;
        public const int Phoenix = 44194;
        public const int EmoteTalkExclamation = 48348;
        public const int EmotePoint = 48349;
        public const int EmoteRoar = 48350;
        public const int ClearFlight = 44232;
        public const int QuiteSuicide = 3617; // Serverside public const int 

        // Flame Strike
        public const int FlameStrikeDummy = 44191;
        public const int FlameStrikeDamage = 44190;

        // Phoenix
        public const int Rebirth = 44196;
        public const int Burn = 44197;
        public const int EmberBlast = 44199;
        public const int SummonPhoenixEgg = 44195; // Serverside public const int 
        public const int FullHeal = 17683;
    }

    enum Phase
    {
        Intro = 0,
        One = 1,
        Two = 2,
        Outro = 3
    }

    struct MiscConst
    {
        public static int[] GravityLapseTeleportSpells =
        [
            SpellIds.GravityLapseLeftTeleport,
            SpellIds.GravityLapseFrontLeftTeleport,
            SpellIds.GravityLapseFrontTeleport,
            SpellIds.GravityLapseFrontRightTeleport,
            SpellIds.GravityLapseRightTeleport
        ];
    }

    [Script]
    class boss_felblood_kaelthas : BossAI
    {
        byte _gravityLapseTargetCount;
        bool _firstGravityLapse;

        Phase _phase;

        static uint groupFireBall = 1;

        public boss_felblood_kaelthas(Creature creature) : base(creature, DataTypes.KaelthasSunstrider)
        {
            Initialize();
        }

        void Initialize()
        {
            _gravityLapseTargetCount = 0;
            _firstGravityLapse = true;
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);
            _phase = Phase.One;

            _scheduler.Schedule(Time.SpanFromMilliseconds(1), groupFireBall, task =>
            {
                DoCastVictim(SpellIds.Fireball);
                task.Repeat(Time.SpanFromSeconds(2.5));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(44), task =>
            {
                Talk(TextIds.SayFlameStrike);
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 40.0f, true);
                if (target != null)
                    DoCast(target, SpellIds.FlameStrike);
                task.Repeat();
            });
            _scheduler.Schedule(Time.SpanFromSeconds(12), task =>
            {
                Talk(TextIds.SaySummonPhoenix);
                DoCastSelf(SpellIds.Phoenix);
                task.Repeat(Time.SpanFromSeconds(45));
            });

            if (IsHeroic())
            {
                _scheduler.Schedule((Seconds)1 + (Minutes)1, task =>
                {
                    Talk(TextIds.SayAnnouncePyroblast);
                    DoCastSelf(SpellIds.ShockBarrier);
                    task.RescheduleGroup(groupFireBall, Time.SpanFromSeconds(2.5));
                    task.Schedule((Seconds)2, pyroBlastTask =>
                    {
                        Unit target = SelectTarget(SelectTargetMethod.Random, 0, 40.0f, true);
                        if (target != null)
                            DoCast(target, SpellIds.Pyroblast);
                    });
                    task.Repeat((Minutes)1);
                });
            }
        }

        public override void Reset()
        {
            _Reset();
            Initialize();
            _phase = Phase.Intro;
        }

        public override void JustDied(Unit killer)
        {
            // No _JustDied() here because otherwise we would reset the events which will trigger the death sequence twice.
            instance.SetBossState(DataTypes.KaelthasSunstrider, EncounterState.Done);
        }

        public override void EnterEvadeMode(EvadeReason why)
        {
            DoCastAOE(SpellIds.ClearFlight, new CastSpellExtraArgs(true));
            _EnterEvadeMode();
            summons.DespawnAll();
            _DespawnAtEvade();
        }

        public override void DamageTaken(Unit attacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            // Checking for lethal damage first so we trigger the outro phase without triggering phase two in case of oneshot attacks
            if (damage >= me.GetHealth() && _phase != Phase.Outro)
            {
                me.AttackStop();
                me.SetReactState(ReactStates.Passive);
                me.InterruptNonMeleeSpells(true);
                me.RemoveAurasDueToSpell(DungeonMode(SpellIds.PowerFeedback, SpellIds.HPowerFeedback));
                summons.DespawnAll();
                DoCastAOE(SpellIds.ClearFlight);
                Talk(TextIds.SayDeath);

                _phase = Phase.Outro;
                _scheduler.CancelAll();

                _scheduler.Schedule(Time.SpanFromSeconds(1), task =>
                {
                    DoCastSelf(SpellIds.EmoteTalkExclamation);
                });
                _scheduler.Schedule(Time.SpanFromSeconds(3.8), task =>
                {
                    DoCastSelf(SpellIds.EmotePoint);
                });
                _scheduler.Schedule(Time.SpanFromSeconds(7.4), task =>
                {
                    DoCastSelf(SpellIds.EmoteRoar);
                });
                _scheduler.Schedule(Time.SpanFromSeconds(10), task =>
                {
                    DoCastSelf(SpellIds.EmoteRoar);
                });
                _scheduler.Schedule(Time.SpanFromSeconds(11), task =>
                {
                    DoCastSelf(SpellIds.QuiteSuicide);
                });
            }

            // Phase two checks. Skip phase two if we are in the outro already
            if (me.HealthBelowPctDamaged(50, damage) && _phase != Phase.Two && _phase != Phase.Outro)
            {
                _phase = Phase.Two;
                _scheduler.CancelAll();
                _scheduler.Schedule(Time.SpanFromMilliseconds(1), task =>
                {
                    Talk(_firstGravityLapse ? TextIds.SayGravityLapse1 : TextIds.SayGravityLapse2);
                    _firstGravityLapse = false;
                    me.SetReactState(ReactStates.Passive);
                    me.AttackStop();
                    me.GetMotionMaster().Clear();
                    task.Schedule(Time.SpanFromSeconds(1), _ =>
                    {
                        DoCastSelf(SpellIds.GravityLapseCenterTeleport);
                        task.Schedule(Time.SpanFromSeconds(1), _ =>
                        {
                            _gravityLapseTargetCount = 0;
                            DoCastAOE(SpellIds.GravityLapseInitial);
                            _scheduler.Schedule(Time.SpanFromSeconds(4), _ =>
                            {
                                for (byte i = 0; i < 3; i++)
                                    DoCastSelf(SpellIds.SummonArcaneSphere, new CastSpellExtraArgs(true));
                            });
                            _scheduler.Schedule(Time.SpanFromSeconds(5), _ =>
                            {
                                DoCastAOE(SpellIds.GravityLapseBeamVisualPeriodic);
                            });
                            _scheduler.Schedule(Time.SpanFromSeconds(35), _ =>
                            {
                                Talk(TextIds.SayPowerFeedback);
                                DoCastAOE(SpellIds.ClearFlight);
                                DoCastSelf(DungeonMode(SpellIds.PowerFeedback, SpellIds.HPowerFeedback));
                                summons.DespawnEntry(CreatureIds.ArcaneSphere);
                                task.Repeat(Time.SpanFromSeconds(11));
                            });
                        });
                    });
                });
            }

            // Kael'thas may only kill himself via Quite Suicide
            if (damage >= me.GetHealth() && attacker != me)
                damage = (int)(me.GetHealth() - 1);
        }

        public override void SetData(int type, int data)
        {
            if (type == DataTypes.KaelthasIntro)
            {
                // skip the intro if Kael'thas is engaged already
                if (_phase != Phase.Intro)
                    return;

                me.SetImmuneToPC(true);
                _scheduler.Schedule(Time.SpanFromSeconds(6), task =>
                {
                    Talk(TextIds.SayIntro1);
                    me.SetEmoteState(Emote.StateTalk);
                    _scheduler.Schedule(Time.SpanFromSeconds(20.6), _ =>
                    {
                        Talk(TextIds.SayIntro2);
                        _scheduler.Schedule(Time.SpanFromMilliseconds(15500), _ =>
                        {
                            me.SetEmoteState(Emote.OneshotNone);
                            me.SetImmuneToPC(false);
                        });
                    });
                    _scheduler.Schedule(Time.SpanFromSeconds(15.6), _ => me.HandleEmoteCommand(Emote.OneshotLaughNoSheathe));
                });
            }
        }

        public override void SpellHitTarget(WorldObject target, SpellInfo spellInfo)
        {
            Unit unitTarget = target.ToUnit();
            if (unitTarget == null)
                return;

            switch (spellInfo.Id)
            {
                case SpellIds.GravityLapseInitial:
                {
                    DoCast(unitTarget, MiscConst.GravityLapseTeleportSpells[_gravityLapseTargetCount], new CastSpellExtraArgs(true));
                    target.m_Events.AddEventAtOffset(() =>
                    {
                        target.CastSpell(target, DungeonMode(SpellIds.GravityLapse, SpellIds.HGravityLapse));
                        target.CastSpell(target, SpellIds.GravityLapseFly);

                    }, Time.SpanFromMilliseconds(400));
                    _gravityLapseTargetCount++;
                    break;
                }
                case SpellIds.ClearFlight:
                    unitTarget.RemoveAurasDueToSpell(SpellIds.GravityLapseFly);
                    unitTarget.RemoveAurasDueToSpell(DungeonMode(SpellIds.GravityLapse, SpellIds.HGravityLapse));
                    break;
                default:
                    break;
            }
        }

        public override void JustSummoned(Creature summon)
        {
            summons.Summon(summon);

            switch (summon.GetEntry())
            {
                case CreatureIds.ArcaneSphere:
                    Unit target = SelectTarget(SelectTargetMethod.Random, 0, 70.0f, true);
                    if (target != null)
                        summon.GetMotionMaster().MoveFollow(target, 0.0f, 0.0f);
                    break;
                case CreatureIds.FlameStrike:
                    summon.CastSpell(summon, SpellIds.FlameStrikeDummy);
                    summon.DespawnOrUnsummon(Time.SpanFromSeconds(15));
                    break;
                default:
                    break;
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim() && _phase != Phase.Intro)
                return;

            _scheduler.Update(diff);
        }
    }

    [Script]
    class npc_felblood_kaelthas_phoenix : ScriptedAI
    {
        InstanceScript _instance;

        bool _isInEgg;
        ObjectGuid _eggGUID;

        public npc_felblood_kaelthas_phoenix(Creature creature) : base(creature)
        {
            _instance = creature.GetInstanceScript();
            Initialize();
        }

        void Initialize()
        {
            me.SetReactState(ReactStates.Passive);
            _isInEgg = false;
        }

        public override void IsSummonedBy(WorldObject summoner)
        {
            DoZoneInCombat();
            DoCastSelf(SpellIds.Burn);
            DoCastSelf(SpellIds.Rebirth);
            _scheduler.Schedule(Time.SpanFromSeconds(2), task => me.SetReactState(ReactStates.Aggressive));
        }

        public override void JustEngagedWith(Unit who) { }

        public override void DamageTaken(Unit attacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (damage >= me.GetHealth())
            {
                if (!_isInEgg)
                {
                    me.AttackStop();
                    me.SetReactState(ReactStates.Passive);
                    me.RemoveAllAuras();
                    me.SetUninteractible(true);
                    DoCastSelf(SpellIds.EmberBlast);
                    // DoCastSelf(SpellSummonPhoenixEgg); -- We do a manual summon for now. Feel free to move it to spelleffect_dbc
                    Creature egg = DoSummon(CreatureIds.PhoenixEgg, me.GetPosition(), Time.SpanFromSeconds(0));
                    if (egg != null)
                    {
                        Creature kaelthas = _instance.GetCreature(DataTypes.KaelthasSunstrider);
                        if (kaelthas != null)
                        {
                            kaelthas.GetAI().JustSummoned(egg);
                            _eggGUID = egg.GetGUID();
                        }
                    }

                    _scheduler.Schedule(Time.SpanFromSeconds(15), task =>
                    {
                        Creature egg = ObjectAccessor.GetCreature(me, _eggGUID);
                        if (egg != null)
                            egg.DespawnOrUnsummon();

                        me.RemoveAllAuras();
                        task.Schedule(Time.SpanFromSeconds(2), rebirthTask =>
                        {
                            DoCastSelf(SpellIds.Rebirth);
                            rebirthTask.Schedule(Time.SpanFromSeconds(2), engageTask =>
                            {
                                _isInEgg = false;
                                DoCastSelf(SpellIds.FullHeal);
                                DoCastSelf(SpellIds.Burn);
                                me.SetUninteractible(false);
                                engageTask.Schedule(Time.SpanFromSeconds(2), task => me.SetReactState(ReactStates.Aggressive));
                            });
                        });
                    });
                    _isInEgg = true;
                }
                damage = (int)(me.GetHealth() - 1);
            }

        }

        public override void SummonedCreatureDies(Creature summon, Unit killer)
        {
            // Egg has been destroyed within 15 seconds so we lose the phoenix.
            me.DespawnOrUnsummon();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }

    [Script] // 44191 - Flame Strike
    class spell_felblood_kaelthas_flame_strike : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.FlameStrikeDamage);
        }

        void AfterRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Unit target = GetTarget();
            if (target != null)
                target.CastSpell(target, SpellIds.FlameStrikeDamage);
        }

        public override void Register()
        {
            AfterEffectRemove.Add(new EffectApplyHandler(AfterRemove, 0, AuraType.Dummy, AuraEffectHandleModes.Real));
        }
    }
}

