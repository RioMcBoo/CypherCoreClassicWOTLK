// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;
using static Global;

namespace Scripts.World.NpcGuard
{
    [Script]
    class npc_guard_generic : GuardAI
    {
        const int SayGuardSilAggro = 0;
        const int NpcCenarionHoldInfantry = 15184;
        const int NpcStormwindCityGuard = 68;
        const int NpcStormwindCityPatroller = 1976;
        const int NpcOrgrimmarGrunt = 3296;

        TaskScheduler _combatScheduler = new();

        public npc_guard_generic(Creature creature) : base(creature)
        {
            _scheduler.SetValidator(() => !me.HasUnitState(UnitState.Casting) && !me.IsInEvadeMode() && me.IsAlive());
            _combatScheduler.SetValidator(() => !me.HasUnitState(UnitState.Casting));
        }

        public override void Reset()
        {
            _scheduler.CancelAll();
            _combatScheduler.CancelAll();
            _scheduler.Schedule(Time.SpanFromSeconds(1), context =>
            {
                // Find a spell that targets friendly and applies an aura (these are generally buffs)
                SpellInfo spellInfo = SelectSpell(me, 0, 0, SelectTargetType.AnyFriend, 0, 0, SelectEffect.Aura);
                if (spellInfo != null)
                    DoCast(me, spellInfo.Id);

                context.Repeat((Minutes)10);
            });
        }

        void DoReplyToTextEmote(TextEmotes emote)
        {
            switch (emote)
            {
                case TextEmotes.Kiss:
                    me.HandleEmoteCommand(Emote.OneshotBow);
                    break;
                case TextEmotes.Wave:
                    me.HandleEmoteCommand(Emote.OneshotWave);
                    break;
                case TextEmotes.Salute:
                    me.HandleEmoteCommand(Emote.OneshotSalute);
                    break;
                case TextEmotes.Shy:
                    me.HandleEmoteCommand(Emote.OneshotFlex);
                    break;
                case TextEmotes.Rude:
                case TextEmotes.Chicken:
                    me.HandleEmoteCommand(Emote.OneshotPoint);
                    break;
                default:
                    break;
            }
        }

        public override void ReceiveEmote(Player player, TextEmotes textEmote)
        {
            switch (me.GetEntry())
            {
                case NpcStormwindCityGuard:
                case NpcStormwindCityPatroller:
                case NpcOrgrimmarGrunt:
                    break;
                default:
                    return;
            }

            if (!me.IsFriendlyTo(player))
                return;

            DoReplyToTextEmote(textEmote);
        }

        public override void JustEngagedWith(Unit who)
        {
            if (me.GetEntry() == NpcCenarionHoldInfantry)
                Talk(SayGuardSilAggro, who);

            _combatScheduler.Schedule(Time.SpanFromSeconds(1), meleeContext =>
            {
                Unit victim = me.GetVictim();
                if (!me.IsAttackReady() || !me.IsWithinMeleeRange(victim))
                {
                    meleeContext.Repeat();
                    return;
                }
                if (RandomHelper.randChance(20))
                {
                    SpellInfo spellInfo = SelectSpell(me.GetVictim(), 0, 0, SelectTargetType.AnyEnemy, 0, SharedConst.NominalMeleeRange, SelectEffect.DontCare);
                    if (spellInfo != null)
                    {
                        me.ResetAttackTimer();
                        DoCastVictim(spellInfo.Id);
                        return;
                    }
                }
                meleeContext.Repeat();
            }).Schedule(Time.SpanFromSeconds(5), spellContext =>
            {
                bool healing = false;
                SpellInfo spellInfo = null;

                // Select a healing spell if less than 30% hp and Only 33% of the time
                if (me.HealthBelowPct(30) && RandomHelper.randChance(33))
                    spellInfo = SelectSpell(me, 0, 0, SelectTargetType.AnyFriend, 0, 0, SelectEffect.Healing);

                // No healing spell available, check if we can cast a ranged spell
                if (spellInfo != null)
                    healing = true;
                else
                    spellInfo = SelectSpell(me.GetVictim(), 0, 0, SelectTargetType.AnyEnemy, SharedConst.NominalMeleeRange, 0, SelectEffect.DontCare);

                // Found a spell
                if (spellInfo != null)
                {
                    if (healing)
                        DoCast(me, spellInfo.Id);
                    else
                        DoCastVictim(spellInfo.Id);
                    spellContext.Repeat(Time.SpanFromSeconds(5));
                }
                else
                    spellContext.Repeat(Time.SpanFromSeconds(1));
            });
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);

            if (!UpdateVictim())
                return;

            _combatScheduler.Update(diff);
        }
    }

    [Script]
    class npc_guard_shattrath_faction : GuardAI
    {
        const int NpcAldorVindicator = 18549;
        const int SpellBanishedShattrathA = 36642;
        const int SpellBanishedShattrathS = 36671;
        const int SpellBanishTeleport = 36643;
        const int SpellExile = 39533;

        public npc_guard_shattrath_faction(Creature creature) : base(creature)
        {
            _scheduler.SetValidator(() => !me.HasUnitState(UnitState.Casting));
        }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void JustEngagedWith(Unit who)
        {
            ScheduleVanish();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }

        void ScheduleVanish()
        {
            _scheduler.Schedule(Time.SpanFromSeconds(5), banishContext =>
            {
                Unit temp = me.GetVictim();
                if (temp != null && temp.IsPlayer())
                {
                    DoCast(temp, me.GetEntry() == NpcAldorVindicator ? SpellBanishedShattrathS : SpellBanishedShattrathA);
                    ObjectGuid playerGUID = temp.GetGUID();
                    banishContext.Schedule(Time.SpanFromSeconds(9), exileContext =>
                    {
                        Unit temp = ObjAccessor.GetUnit(me, playerGUID);
                        if (temp != null)
                        {
                            temp.CastSpell(temp, SpellExile, true);
                            temp.CastSpell(temp, SpellBanishTeleport, true);
                        }
                        ScheduleVanish();


                    });
                }
                else
                    banishContext.Repeat();
            });
        }
    }
}