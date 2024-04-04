// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Scripts.Events
{
    [Script] // 45724 - Braziers Hit!
    class spell_midsummer_braziers_hit : AuraScript
    {
        const int SpellTorchTossingTraining = 45716;
        const int SpellTorchTossingPractice = 46630;
        const int SpellTorchTossingTrainingSuccessAlliance = 45719;
        const int SpellTorchTossingTrainingSuccessHorde = 46651;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellTorchTossingTraining, SpellTorchTossingPractice, SpellTorchTossingTrainingSuccessAlliance, SpellTorchTossingTrainingSuccessHorde);
        }

        void HandleEffectApply(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Player player = GetTarget().ToPlayer();
            if (player == null)
                return;

            if ((player.HasAura(SpellTorchTossingTraining) && GetStackAmount() == 8) || (player.HasAura(SpellTorchTossingPractice) && GetStackAmount() == 20))
            {
                if (player.GetTeam() == Team.Alliance)
                    player.CastSpell(player, SpellTorchTossingTrainingSuccessAlliance, true);
                else if (player.GetTeam() == Team.Horde)
                    player.CastSpell(player, SpellTorchTossingTrainingSuccessHorde, true);
                Remove();
            }
        }

        public override void Register()
        {
            AfterEffectApply.Add(new(HandleEffectApply, 0, AuraType.Dummy, AuraEffectHandleModes.Reapply));
        }
    }

    [Script] // 45907 - Torch Target Picker
    class spell_midsummer_torch_target_picker : SpellScript
    {
        const int SpellTargetIndicatorCosmetic = 46901;
        const int SpellTargetIndicator = 45723;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellTargetIndicatorCosmetic, SpellTargetIndicator);
        }

        void HandleScript(int effIndex)
        {
            Unit target = GetHitUnit();
            target.CastSpell(target, SpellTargetIndicatorCosmetic, true);
            target.CastSpell(target, SpellTargetIndicator, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 46054 - Torch Toss (land)
    class spell_midsummer_torch_toss_land : SpellScript
    {
        const int SpellBraziersHit = 45724;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellBraziersHit);
        }

        void HandleScript(int effIndex)
        {
            GetHitUnit().CastSpell(GetCaster(), SpellBraziersHit, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 29705, 29726, 29727 - Test Ribbon Pole Channel
    class spell_midsummer_test_ribbon_pole_channel : AuraScript
    {
        const int SpellHasFullMidsummerSet = 58933;
        const int SpellBurningHotPoleDance = 58934;
        const int SpellRibbonPolePeriodicVisual = 45406;
        const int SpellRibbonDance = 29175;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellRibbonPolePeriodicVisual, SpellBurningHotPoleDance, SpellHasFullMidsummerSet, SpellRibbonDance);
        }

        void HandleRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(SpellRibbonPolePeriodicVisual);
        }

        void PeriodicTick(AuraEffect aurEff)
        {
            Unit target = GetTarget();
            target.CastSpell(target, SpellRibbonPolePeriodicVisual, true);

            Aura aur = target.GetAura(SpellRibbonDance);
            if (aur != null)
            {
                aur.SetMaxDuration(Math.Min(3600000, aur.GetMaxDuration() + 180000));
                aur.RefreshDuration();

                if (aur.GetMaxDuration() == 3600000 && target.HasAura(SpellHasFullMidsummerSet))
                    target.CastSpell(target, SpellBurningHotPoleDance, true);
            }
            else
                target.CastSpell(target, SpellRibbonDance, true);
        }

        public override void Register()
        {
            AfterEffectRemove.Add(new(HandleRemove, 1, AuraType.PeriodicTriggerSpell, AuraEffectHandleModes.Real));
            OnEffectPeriodic.Add(new(PeriodicTick, 1, AuraType.PeriodicTriggerSpell));
        }
    }

    [Script] // 45406 - Holiday - Midsummer, Ribbon Pole Periodic Visual
    class spell_midsummer_ribbon_pole_periodic_visual : AuraScript
    {
        const int SpellTestRibbonPole1 = 29705;
        const int SpellTestRibbonPole2 = 29726;
        const int SpellTestRibbonPole3 = 29727;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellTestRibbonPole1, SpellTestRibbonPole2, SpellTestRibbonPole3);
        }

        void PeriodicTick(AuraEffect aurEff)
        {
            Unit target = GetTarget();
            if (!target.HasAura(SpellTestRibbonPole1) && !target.HasAura(SpellTestRibbonPole2) && !target.HasAura(SpellTestRibbonPole3))
                Remove();
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(PeriodicTick, 0, AuraType.PeriodicDummy));
        }
    }

    struct JugglingTorch
    {
        public const int SpellJuggleTorchSlow = 45792;
        public const int SpellJuggleTorchMedium = 45806;
        public const int SpellJuggleTorchFast = 45816;
        public const int SpellJuggleTorchSelf = 45638;

        public const int SpellJuggleTorchShadowSlow = 46120;
        public const int SpellJuggleTorchShadowMedium = 46118;
        public const int SpellJuggleTorchShadowFast = 46117;
        public const int SpellJuggleTorchShadowSelf = 46121;
        public const int SpellGiveTorch = 45280;

        public const int QuestTorchCatchingA = 11657;
        public const int QuestTorchCatchingH = 11923;
        public const int QuestMoreTorchCatchingA = 11924;
        public const int QuestMoreTorchCatchingH = 11925;
    }

    [Script] // 45819 - Throw Torch
    class spell_midsummer_juggle_torch : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(JugglingTorch.SpellJuggleTorchSlow, JugglingTorch.SpellJuggleTorchMedium, JugglingTorch.SpellJuggleTorchFast, JugglingTorch.SpellJuggleTorchSelf,
                JugglingTorch.SpellJuggleTorchShadowSlow, JugglingTorch.SpellJuggleTorchShadowMedium, JugglingTorch.SpellJuggleTorchShadowFast, JugglingTorch.SpellJuggleTorchShadowSelf);
        }

        void HandleDummy(int effIndex)
        {
            if (GetExplTargetDest() == null)
                return;

            Position spellDest = GetExplTargetDest();
            float distance = GetCaster().GetExactDist2d(spellDest.GetPositionX(), spellDest.GetPositionY());

            int torchSpellID;
            int torchShadowSpellID;

            if (distance <= 1.5f)
            {
                torchSpellID = JugglingTorch.SpellJuggleTorchSelf;
                torchShadowSpellID = JugglingTorch.SpellJuggleTorchShadowSelf;
                spellDest = GetCaster().GetPosition();
            }
            else if (distance <= 10.0f)
            {
                torchSpellID = JugglingTorch.SpellJuggleTorchSlow;
                torchShadowSpellID = JugglingTorch.SpellJuggleTorchShadowSlow;
            }
            else if (distance <= 20.0f)
            {
                torchSpellID = JugglingTorch.SpellJuggleTorchMedium;
                torchShadowSpellID = JugglingTorch.SpellJuggleTorchShadowMedium;
            }
            else
            {
                torchSpellID = JugglingTorch.SpellJuggleTorchFast;
                torchShadowSpellID = JugglingTorch.SpellJuggleTorchShadowFast;
            }

            GetCaster().CastSpell(spellDest, torchSpellID);
            GetCaster().CastSpell(spellDest, torchShadowSpellID);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 45644 - Juggle Torch (Catch)
    class spell_midsummer_torch_catch : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(JugglingTorch.SpellGiveTorch);
        }

        void HandleDummy(int effIndex)
        {
            Player player = GetHitPlayer();
            if (player == null)
                return;

            if (player.GetQuestStatus(JugglingTorch.QuestTorchCatchingA) == QuestStatus.Rewarded || player.GetQuestStatus(JugglingTorch.QuestTorchCatchingH) == QuestStatus.Rewarded)
                player.CastSpell(player, JugglingTorch.SpellGiveTorch);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    struct FlingTorch
    {
        public const int SpellFlingTorchTriggered = 45669;
        public const int SpellFlingTorchShadow = 46105;
        public const int SpellJuggleTorchMissed = 45676;
        public const int SpellTorchesCaught = 45693;
        public const int SpellTorchCatchingSuccessAlliance = 46081;
        public const int SpellTorchCatchingSuccessHorde = 46654;
        public const int SpellTorchCatchingRemoveTorches = 46084;
    }

    [Script] // 46747 - Fling torch
    class spell_midsummer_fling_torch : SpellScript
    {


        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(FlingTorch.SpellFlingTorchTriggered, FlingTorch.SpellFlingTorchShadow);
        }

        void HandleDummy(int effIndex)
        {
            Position dest = GetCaster().GetFirstCollisionPosition(30.0f, RandomHelper.NextSingle() * (float)(2 * MathF.PI));
            GetCaster().CastSpell(dest, FlingTorch.SpellFlingTorchTriggered, true);
            GetCaster().CastSpell(dest, FlingTorch.SpellFlingTorchShadow);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 45669 - Fling Torch
    class spell_midsummer_fling_torch_triggered : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(FlingTorch.SpellJuggleTorchMissed);
        }

        void HandleTriggerMissile(int effIndex)
        {
            Position pos = GetHitDest();
            if (pos != null)
            {
                if (GetCaster().GetExactDist2d(pos) > 3.0f)
                {
                    PreventHitEffect(effIndex);
                    GetCaster().CastSpell(GetExplTargetDest(), FlingTorch.SpellJuggleTorchMissed);
                    GetCaster().RemoveAura(FlingTorch.SpellTorchesCaught);
                }
            }
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleTriggerMissile, 0, SpellEffectName.TriggerMissile));
        }
    }

    [Script] // 45671 - Juggle Torch (Catch, Quest)
    class spell_midsummer_fling_torch_catch : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(FlingTorch.SpellFlingTorchTriggered, FlingTorch.SpellTorchCatchingSuccessAlliance, FlingTorch.SpellTorchCatchingSuccessHorde, FlingTorch.SpellTorchCatchingRemoveTorches, FlingTorch.SpellFlingTorchShadow);
        }

        void HandleScriptEffect(int effIndex)
        {
            Player player = GetHitPlayer();
            if (player == null)
                return;

            if (GetExplTargetDest() == null)
                return;

            // Only the caster can catch the torch
            if (player.GetGUID() != GetCaster().GetGUID())
                return;

            byte requiredCatches = 0;
            // Number of required catches depends on quest - 4 for the normal quest, 10 for the daily version
            if (player.GetQuestStatus(JugglingTorch.QuestTorchCatchingA) == QuestStatus.Incomplete || player.GetQuestStatus(JugglingTorch.QuestTorchCatchingH) == QuestStatus.Incomplete)
                requiredCatches = 3;
            else if (player.GetQuestStatus(JugglingTorch.QuestMoreTorchCatchingA) == QuestStatus.Incomplete || player.GetQuestStatus(JugglingTorch.QuestMoreTorchCatchingH) == QuestStatus.Incomplete)
                requiredCatches = 9;

            // Used quest item without being on quest - do nothing
            if (requiredCatches == 0)
                return;

            if (player.GetAuraCount(FlingTorch.SpellTorchesCaught) >= requiredCatches)
            {
                player.CastSpell(player, (player.GetTeam() == Team.Alliance) ? FlingTorch.SpellTorchCatchingSuccessAlliance : FlingTorch.SpellTorchCatchingSuccessHorde);
                player.CastSpell(player, FlingTorch.SpellTorchCatchingRemoveTorches);
                player.RemoveAura(FlingTorch.SpellTorchesCaught);
            }
            else
            {
                Position dest = player.GetFirstCollisionPosition(15.0f, RandomHelper.NextSingle() * (2 * MathF.PI));
                player.CastSpell(player, FlingTorch.SpellTorchesCaught);
                player.CastSpell(dest, FlingTorch.SpellFlingTorchTriggered, true);
                player.CastSpell(dest, FlingTorch.SpellFlingTorchShadow);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScriptEffect, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 45676 - Juggle Torch (Quest, Missed)
    class spell_midsummer_fling_torch_missed : SpellScript
    {
        void FilterTargets(List<WorldObject> targets)
        {
            // This spell only hits the caster
            targets.RemoveAll(obj => obj.GetGUID() != GetCaster().GetGUID());
        }

        public override void Register()
        {
            OnObjectAreaTargetSelect.Add(new(FilterTargets, 0, Targets.UnitDestAreaEntry));
            OnObjectAreaTargetSelect.Add(new(FilterTargets, 2, Targets.UnitDestAreaEntry));
        }
    }
}