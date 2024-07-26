// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.Karazhan.Curator
{
    struct SpellIds
    {
        public const int HatefulBolt = 30383;
        public const int Evocation = 30254;
        public const int ArcaneInfusion = 30403;
        public const int Berserk = 26662;
        public const int SummonAstralFlareNe = 30236;
        public const int SummonAstralFlareNw = 30239;
        public const int SummonAstralFlareSe = 30240;
        public const int SummonAstralFlareSw = 30241;
    }

    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SaySummon = 1;
        public const int SayEvocate = 2;
        public const int SayEnrage = 3;
        public const int SayKill = 4;
        public const int SayDeath = 5;
    }

    struct MiscConst
    {
        public const uint GroupAstralFlare = 1;
    }

    [Script]
    class boss_curator : BossAI
    {
        public boss_curator(Creature creature) : base(creature, DataTypes.Curator) { }

        public override void Reset()
        {
            _Reset();
            _infused = false;
        }

        public override void KilledUnit(Unit victim)
        {
            if (victim.IsPlayer())
                Talk(TextIds.SayKill);
        }

        public override void JustDied(Unit killer)
        {
            _JustDied();
            Talk(TextIds.SayDeath);
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);
            Talk(TextIds.SayAggro);

            _scheduler.Schedule(Time.SpanFromSeconds(12), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.MaxThreat, 1);
                if (target != null)
                    DoCast(target, SpellIds.HatefulBolt);
                task.Repeat(Time.SpanFromSeconds(7), Time.SpanFromSeconds(15));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(10), MiscConst.GroupAstralFlare, task =>
            {
                if (RandomHelper.randChance(50))
                    Talk(TextIds.SaySummon);

                DoCastSelf(RandomHelper.RAND(SpellIds.SummonAstralFlareNe, SpellIds.SummonAstralFlareNw, SpellIds.SummonAstralFlareSe, SpellIds.SummonAstralFlareSw), new CastSpellExtraArgs(true));

                int mana = (me.GetMaxPower(PowerType.Mana) / 10);
                if (mana != 0)
                {
                    me.ModifyPower(PowerType.Mana, -mana);

                    if (me.GetPower(PowerType.Mana) * 100 / me.GetMaxPower(PowerType.Mana) < 10)
                    {
                        Talk(TextIds.SayEvocate);
                        me.InterruptNonMeleeSpells(false);
                        DoCastSelf(SpellIds.Evocation);
                    }
                }
                task.Repeat(Time.SpanFromSeconds(10));
            });
            _scheduler.Schedule((Minutes)12, ScheduleTasks =>
            {
                Talk(TextIds.SayEnrage);
                DoCastSelf(SpellIds.Berserk, new CastSpellExtraArgs(true));
            });
        }

        public override void DamageTaken(Unit attacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (!HealthAbovePct(15) && !_infused)
            {
                _infused = true;
                _scheduler.Schedule(Time.SpanFromMilliseconds(1), task => DoCastSelf(SpellIds.ArcaneInfusion, new CastSpellExtraArgs(true)));
                _scheduler.CancelGroup(MiscConst.GroupAstralFlare);
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }

        bool _infused;
    }

    [Script]
    class npc_curator_astral_flare : ScriptedAI
    {
        public npc_curator_astral_flare(Creature creature) : base(creature)
        {
            me.SetReactState(ReactStates.Passive);
        }

        public override void Reset()
        {
            _scheduler.Schedule(Time.SpanFromSeconds(2), task =>
            {
                me.SetReactState(ReactStates.Aggressive);
                me.SetUninteractible(false);
                DoZoneInCombat();
            });
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }
    }
}