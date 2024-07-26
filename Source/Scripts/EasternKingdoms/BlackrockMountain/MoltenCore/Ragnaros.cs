// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.MoltenCore
{
    struct SpellIds
    {
        public const int HandOfRagnaros = 19780;
        public const int WrathOfRagnaros = 20566;
        public const int LavaBurst = 21158;
        public const int MagmaBlast = 20565;                   // Ranged attack
        public const int SonsOfFlameDummy = 21108;                   // Server side effect
        public const int Ragsubmerge = 21107;                   // Stealth aura
        public const int Ragemerge = 20568;
        public const int MeltWeapon = 21388;
        public const int ElementalFire = 20564;
        public const int Erruption = 17731;
    }

    struct TextIds
    {
        public const int SaySummonMaj = 0;
        public const int SayArrival1Rag = 1;
        public const int SayArrival2Maj = 2;
        public const int SayArrival3Rag = 3;
        public const int SayArrival5Rag = 4;
        public const int SayReinforcements1 = 5;
        public const int SayReinforcements2 = 6;
        public const int SayHand = 7;
        public const int SayWrath = 8;
        public const int SayKill = 9;
        public const int SayMagmaburst = 10;
    }

    struct EventIds
    {
        public const int Eruption = 1;
        public const int WrathOfRagnaros = 2;
        public const int HandOfRagnaros = 3;
        public const int LavaBurst = 4;
        public const int ElementalFire = 5;
        public const int MagmaBlast = 6;
        public const int Submerge = 7;

        public const int Intro1 = 8;
        public const int Intro2 = 9;
        public const int Intro3 = 10;
        public const int Intro4 = 11;
        public const int Intro5 = 12;
    }

    [Script]
    class boss_ragnaros : BossAI
    {
        TimeSpan _emergeTimer;
        byte _introState;
        bool _hasYelledMagmaBurst;
        bool _hasSubmergedOnce;
        bool _isBanished;

        public boss_ragnaros(Creature creature) : base(creature, DataTypes.Ragnaros)
        {
            Initialize();
            _introState = 0;
            me.SetReactState(ReactStates.Passive);
            me.SetUnitFlag(UnitFlags.NonAttackable);
            SetCombatMovement(false);
        }

        void Initialize()
        {
            _emergeTimer = (Milliseconds)90000;
            _hasYelledMagmaBurst = false;
            _hasSubmergedOnce = false;
            _isBanished = false;
        }

        public override void Reset()
        {
            base.Reset();
            Initialize();
            me.SetEmoteState(Emote.OneshotNone);
        }

        public override void JustEngagedWith(Unit victim)
        {
            base.JustEngagedWith(victim);
            _events.ScheduleEvent(EventIds.Eruption, Time.SpanFromSeconds(15));
            _events.ScheduleEvent(EventIds.WrathOfRagnaros, Time.SpanFromSeconds(30));
            _events.ScheduleEvent(EventIds.HandOfRagnaros, Time.SpanFromSeconds(25));
            _events.ScheduleEvent(EventIds.LavaBurst, Time.SpanFromSeconds(10));
            _events.ScheduleEvent(EventIds.ElementalFire, Time.SpanFromSeconds(3));
            _events.ScheduleEvent(EventIds.MagmaBlast, Time.SpanFromSeconds(2));
            _events.ScheduleEvent(EventIds.Submerge, (Minutes)3);
        }

        public override void KilledUnit(Unit victim)
        {
            if (RandomHelper.URand(0, 99) < 25)
                Talk(TextIds.SayKill);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (_introState != 2)
            {
                if (_introState == 0)
                {
                    me.HandleEmoteCommand(Emote.OneshotEmerge);
                    _events.ScheduleEvent(EventIds.Intro1, Time.SpanFromSeconds(4));
                    _events.ScheduleEvent(EventIds.Intro2, Time.SpanFromSeconds(23));
                    _events.ScheduleEvent(EventIds.Intro3, Time.SpanFromSeconds(42));
                    _events.ScheduleEvent(EventIds.Intro4, Time.SpanFromSeconds(43));
                    _events.ScheduleEvent(EventIds.Intro5, Time.SpanFromSeconds(53));
                    _introState = 1;
                }

                _events.Update(diff);

                _events.ExecuteEvents(eventId =>
                {
                    switch (eventId)
                    {
                        case EventIds.Intro1:
                            Talk(TextIds.SayArrival1Rag);
                            break;
                        case EventIds.Intro2:
                            Talk(TextIds.SayArrival3Rag);
                            break;
                        case EventIds.Intro3:
                            me.HandleEmoteCommand(Emote.OneshotAttack1h);
                            break;
                        case EventIds.Intro4:
                            Talk(TextIds.SayArrival5Rag);
                            Creature executus = ObjectAccessor.GetCreature(me, instance.GetGuidData(DataTypes.MajordomoExecutus));
                            if (executus != null)
                                Unit.Kill(me, executus);
                            break;
                        case EventIds.Intro5:
                            me.SetReactState(ReactStates.Aggressive);
                            me.RemoveUnitFlag(UnitFlags.NonAttackable);
                            me.SetImmuneToPC(false);
                            _introState = 2;
                            break;
                        default:
                            break;
                    }
                });
            }
            else
            {
                if (_isBanished && ((_emergeTimer <= diff) || (instance.GetData(MCMiscConst.DataRagnarosAdds)) > 8))
                {
                    //Become unbanished again
                    me.SetReactState(ReactStates.Aggressive);
                    me.SetFaction(FactionTemplates.Monster);
                    me.SetUninteractible(false);
                    me.SetEmoteState(Emote.OneshotNone);
                    me.HandleEmoteCommand(Emote.OneshotEmerge);
                    Unit target = SelectTarget(SelectTargetMethod.Random, 0);
                    if (target != null)
                        AttackStart(target);
                    instance.SetData(MCMiscConst.DataRagnarosAdds, 0);

                    //DoCast(me, SpellRagemerge); //"phase spells" didnt worked correctly so Ive commented them and wrote solution witch doesnt need core support
                    _isBanished = false;
                }
                else if (_isBanished)
                {
                    _emergeTimer -= diff;
                    //Do nothing while banished
                    return;
                }

                //Return since we have no target
                if (!UpdateVictim())
                    return;

                _events.Update(diff);

                _events.ExecuteEvents(eventId =>
                {
                    switch (eventId)
                    {
                        case EventIds.Eruption:
                            DoCastVictim(SpellIds.Erruption);
                            _events.ScheduleEvent(EventIds.Eruption, Time.SpanFromSeconds(20), Time.SpanFromSeconds(45));
                            break;
                        case EventIds.WrathOfRagnaros:
                            DoCastVictim(SpellIds.WrathOfRagnaros);
                            if (RandomHelper.URand(0, 1) != 0)
                                Talk(TextIds.SayWrath);
                            _events.ScheduleEvent(EventIds.WrathOfRagnaros, Time.SpanFromSeconds(25));
                            break;
                        case EventIds.HandOfRagnaros:
                            DoCast(me, SpellIds.HandOfRagnaros);
                            if (RandomHelper.URand(0, 1) != 0)
                                Talk(TextIds.SayHand);
                            _events.ScheduleEvent(EventIds.HandOfRagnaros, Time.SpanFromSeconds(20));
                            break;
                        case EventIds.LavaBurst:
                            DoCastVictim(SpellIds.LavaBurst);
                            _events.ScheduleEvent(EventIds.LavaBurst, Time.SpanFromSeconds(10));
                            break;
                        case EventIds.ElementalFire:
                            DoCastVictim(SpellIds.ElementalFire);
                            _events.ScheduleEvent(EventIds.ElementalFire, Time.SpanFromSeconds(10), Time.SpanFromSeconds(14));
                            break;
                        case EventIds.MagmaBlast:
                            if (!me.IsWithinMeleeRange(me.GetVictim()))
                            {
                                DoCastVictim(SpellIds.MagmaBlast);
                                if (!_hasYelledMagmaBurst)
                                {
                                    //Say our dialog
                                    Talk(TextIds.SayMagmaburst);
                                    _hasYelledMagmaBurst = true;
                                }
                            }
                            _events.ScheduleEvent(EventIds.MagmaBlast, Time.SpanFromMilliseconds(2500));
                            break;
                        case EventIds.Submerge:
                        {
                            if (!_isBanished)
                            {
                                //Creature spawning and ragnaros becomming unattackable
                                //is not very well supported in the core //no it really isnt
                                //so added normaly spawning and banish workaround and attack again after 90 secs.
                                me.AttackStop();
                                ResetThreatList();
                                me.SetReactState(ReactStates.Passive);
                                me.InterruptNonMeleeSpells(false);
                                //Root self
                                //DoCast(me, 23973);
                                me.SetFaction(FactionTemplates.Friendly);
                                me.SetUninteractible(true);
                                me.SetEmoteState(Emote.StateSubmerged);
                                me.HandleEmoteCommand(Emote.OneshotSubmerge);
                                instance.SetData(MCMiscConst.DataRagnarosAdds, 0);

                                if (!_hasSubmergedOnce)
                                {
                                    Talk(TextIds.SayReinforcements1);

                                    // summon 8 elementals
                                    for (byte i = 0; i < 8; ++i)
                                    {
                                        Unit target = SelectTarget(SelectTargetMethod.Random, 0);
                                        if (target != null)
                                        {
                                            Creature summoned = me.SummonCreature(12143, target.GetPositionX(), target.GetPositionY(), target.GetPositionZ(), 0.0f, TempSummonType.TimedOrCorpseDespawn, (Minutes)15);
                                            if (summoned != null)
                                                summoned.GetAI().AttackStart(target);
                                        }
                                    }

                                    _hasSubmergedOnce = true;
                                    _isBanished = true;
                                    //DoCast(me, SpellRagsubmerge);
                                    _emergeTimer = (Milliseconds)90000;

                                }
                                else
                                {
                                    Talk(TextIds.SayReinforcements2);

                                    for (byte i = 0; i < 8; ++i)
                                    {
                                        Unit target = SelectTarget(SelectTargetMethod.Random, 0);
                                        if (target != null)
                                        {
                                            Creature summoned = me.SummonCreature(12143, target.GetPositionX(), target.GetPositionY(), target.GetPositionZ(), 0.0f, TempSummonType.TimedOrCorpseDespawn, (Minutes)15);
                                            if (summoned != null)
                                                summoned.GetAI().AttackStart(target);
                                        }
                                    }

                                    _isBanished = true;
                                    //DoCast(me, SpellRagsubmerge);
                                    _emergeTimer = (Milliseconds)90000;
                                }
                            }
                            _events.ScheduleEvent(EventIds.Submerge, (Minutes)3);
                            break;
                        }
                        default:
                            break;
                    }
                });
            }
        }
    }

    [Script]
    class npc_son_of_flame : ScriptedAI //didnt work correctly in Eai for me...
    {
        InstanceScript instance;

        public npc_son_of_flame(Creature creature) : base(creature)
        {
            instance = me.GetInstanceScript();
        }

        public override void JustDied(Unit killer)
        {
            instance.SetData(MCMiscConst.DataRagnarosAdds, 1);
        }
    }
}

