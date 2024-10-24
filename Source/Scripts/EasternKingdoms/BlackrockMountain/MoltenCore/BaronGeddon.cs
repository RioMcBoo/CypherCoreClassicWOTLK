// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.MoltenCore.BaronGeddon
{
    struct SpellIds
    {
        public const int Inferno = 19695;
        public const int InfernoDmg = 19698;
        public const int IgniteMana = 19659;
        public const int LivingBomb = 20475;
        public const int Armageddon = 20478;
    }

    struct TextIds
    {
        public const int EmoteService = 0;
    }

    [Script]
    class boss_baron_geddon : BossAI
    {
        public boss_baron_geddon(Creature creature) : base(creature, DataTypes.BaronGeddon) { }

        public override void JustEngagedWith(Unit victim)
        {
            base.JustEngagedWith(victim);

            _scheduler.Schedule(Time.SpanFromSeconds(45), task =>
            {
                DoCast(me, SpellIds.Inferno);
                task.Repeat(Time.SpanFromSeconds(45));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(30), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 0.0f, true, true, -SpellIds.IgniteMana);
                if (target != null)
                    DoCast(target, SpellIds.IgniteMana);
                task.Repeat(Time.SpanFromSeconds(30));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(35), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 0.0f, true);
                if (target != null)
                    DoCast(target, SpellIds.LivingBomb);
                task.Repeat(Time.SpanFromSeconds(35));
            });
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);

            // If we are <2% hp cast Armageddon
            if (!HealthAbovePct(2))
            {
                me.InterruptNonMeleeSpells(true);
                DoCast(me, SpellIds.Armageddon);
                Talk(TextIds.EmoteService);
                return;
            }

            if (me.HasUnitState(UnitState.Casting))
                return;
        }
    }

    [Script] // 19695 - Inferno
    class spell_baron_geddon_inferno : AuraScript
    {
        void OnPeriodic(AuraEffect aurEff)
        {
            PreventDefaultAction();
            int[] damageForTick = [500, 500, 1000, 1000, 2000, 2000, 3000, 5000];
            CastSpellExtraArgs args = new CastSpellExtraArgs(TriggerCastFlags.FullMask);
            args.TriggeringAura = aurEff;
            args.AddSpellMod(SpellValueMod.BasePoint0, damageForTick[aurEff.GetTickNumber() - 1]);
            GetTarget().CastSpell((WorldObject)null, SpellIds.InfernoDmg, args);
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new EffectPeriodicHandler(OnPeriodic, 0, AuraType.PeriodicTriggerSpell));
        }
    }
}

