// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Networking.Packets;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using static Global;

namespace Scripts.Spells.DeathKnight
{
    struct SpellIds
    {
        public const int ArmyFleshBeastTransform = 127533;
        public const int ArmyGeistTransform = 127534;
        public const int ArmyNorthrendSkeletonTransform = 127528;
        public const int ArmySkeletonTransform = 127527;
        public const int ArmySpikedGhoulTransform = 127525;
        public const int ArmySuperZombieTransform = 127526;
        public const int Blood = 137008;
        public const int BloodPlague = 55078;
        public const int BloodShieldAbsorb = 77535;
        public const int BloodShieldMastery = 77513;
        public const int BreathOfSindragosa = 152279;
        public const int CorpseExplosionTriggered = 43999;
        public const int DeathAndDecayDamage = 52212;
        public const int DeathCoilDamage = 47632;
        public const int DeathGripDummy = 243912;
        public const int DeathGripJump = 49575;
        public const int DeathGripTaunt = 51399;
        public const int DeathStrikeHeal = 45470;
        public const int DeathStrikeOffhand = 66188;
        public const int FesteringWound = 194310;
        public const int Frost = 137006;
        public const int FrostFever = 55095;
        public const int FrostScythe = 207230;
        public const int GlyphOfFoulMenagerie = 58642;
        public const int GlyphOfTheGeist = 58640;
        public const int GlyphOfTheSkeleton = 146652;
        public const int KillingMachineProc = 51124;
        public const int MarkOfBloodHeal = 206945;
        public const int NecrosisEffect = 216974;
        public const int Obliteration = 281238;
        public const int ObliterationRuneEnergize = 281327;
        public const int PillarOfFrost = 51271;
        public const int RaiseDeadSummon = 52150;
        public const int RecentlyUsedDeathStrike = 180612;
        public const int RunicPowerEnergize = 49088;
        public const int RunicReturn = 61258;
        public const int SludgeBelcher = 207313;
        public const int SludgeBelcherSummon = 212027;
        public const int DeathStrikeEnabler = 89832; // Server Side
        public const int TighteningGrasp = 206970;
        public const int TighteningGraspSlow = 143375;
        public const int Unholy = 137007;
        public const int UnholyVigor = 196263;
        public const int VolatileShielding = 207188;
        public const int VolatileShieldingDamage = 207194;
        public const int BloodOfTheNorth = 54639;
        public const int Reaping = 49208;
        public const int DeathRuneMastery = 49467;
    }

    [Script] // 70656 - Advantage (T10 4P Melee Bonus)
    class spell_dk_advantage_t10_4p : AuraScript
    {
        bool CheckProc(ProcEventInfo eventInfo)
        {
            if (eventInfo.GetActor() is Player player && player.GetClass() == Class.DeathKnight)
            {
                return player.Runes.AvailableRunes == RuneStateMask.None;
            }

            return false;
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
        }
    }

    [Script] // 48707 - Anti-Magic Shell
    class spell_dk_anti_magic_shell : AuraScript
    {
        int absorbPct;
        long maxHealth;
        int absorbedAmount;

        public spell_dk_anti_magic_shell()
        {
            absorbPct = 0;
            maxHealth = 0;
            absorbedAmount = 0;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.RunicPowerEnergize, SpellIds.VolatileShielding)
                && ValidateSpellEffect((spellInfo.Id, 1));
        }

        public override bool Load()
        {
            absorbPct = GetEffectInfo(1).CalcValue(GetCaster());
            maxHealth = GetCaster().GetMaxHealth();
            absorbedAmount = 0;
            return true;
        }

        void CalculateAmount(AuraEffect aurEff, ref int amount, ref bool canBeRecalculated)
        {
            amount = (int)MathFunctions.CalculatePct(maxHealth, absorbPct);
        }

        void Trigger(AuraEffect aurEff, DamageInfo dmgInfo, ref int absorbAmount)
        {
            absorbedAmount += absorbAmount;

            if (!GetTarget().HasAura(SpellIds.VolatileShielding))
            {
                CastSpellExtraArgs args = new(aurEff);
                args.AddSpellMod(SpellValueMod.BasePoint0, MathFunctions.CalculatePct(absorbAmount, 2 * absorbAmount * 100 / maxHealth));
                GetTarget().CastSpell(GetTarget(), SpellIds.RunicPowerEnergize, args);
            }
        }

        void HandleEffectRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            AuraEffect volatileShielding = GetTarget().GetAuraEffect(SpellIds.VolatileShielding, 1);
            if (volatileShielding != null)
            {
                CastSpellExtraArgs args = new(volatileShielding);
                args.AddSpellMod(SpellValueMod.BasePoint0, MathFunctions.CalculatePct(absorbedAmount, volatileShielding.GetAmount()));
                GetTarget().CastSpell(null, SpellIds.VolatileShieldingDamage, args);
            }
        }

        public override void Register()
        {
            DoEffectCalcAmount.Add(new(CalculateAmount, 0, AuraType.SchoolAbsorb));
            AfterEffectAbsorb.Add(new(Trigger, 0));
            AfterEffectRemove.Add(new(HandleEffectRemove, 0, AuraType.SchoolAbsorb, AuraEffectHandleModes.Real));
        }
    }

    // 127517 - Army Transform
    [Script] /// 6.x, does this belong here or in spell_generic? where do we cast this? sniffs say this is only cast when caster has glyph of foul menagerie.
    class spell_dk_army_transform : SpellScript
    {
        int[] ArmyTransforms =
        [
            SpellIds.ArmyFleshBeastTransform,
            SpellIds.ArmyGeistTransform,
            SpellIds. ArmyNorthrendSkeletonTransform,
            SpellIds.ArmySkeletonTransform,
            SpellIds.ArmySpikedGhoulTransform,
            SpellIds. ArmySuperZombieTransform,
        ];

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.GlyphOfFoulMenagerie);
        }

        public override bool Load()
        {
            return GetCaster().IsGuardian();
        }

        SpellCastResult CheckCast()
        {
            Unit owner = GetCaster().GetOwner();
            if (owner != null)
                if (owner.HasAura(SpellIds.GlyphOfFoulMenagerie))
                    return SpellCastResult.SpellCastOk;

            return SpellCastResult.SpellUnavailable;
        }

        void HandleDummy(int effIndex)
        {
            GetCaster().CastSpell(GetCaster(), ArmyTransforms.SelectRandom(), true);
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckCast));
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 50842 - Blood Boil
    class spell_dk_blood_boil : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.BloodPlague);
        }

        void HandleEffect()
        {
            GetCaster().CastSpell(GetHitUnit(), SpellIds.BloodPlague, true);
        }

        public override void Register()
        {
            OnHit.Add(new(HandleEffect));
        }
    }

    // 49028 - Dancing Rune Weapon
    [Script] /// 7.1.5
    class spell_dk_dancing_rune_weapon : AuraScript
    {
        const int NpcDkDancingRuneWeapon = 27893;

        public override bool Validate(SpellInfo spellInfo)
        {
            if (ObjectMgr.GetCreatureTemplate(NpcDkDancingRuneWeapon) == null)
                return false;
            return true;
        }

        // This is a port of the old switch hack in Unit.cpp, it's not correct
        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            Unit caster = GetCaster();
            if (caster == null)
                return;

            Unit drw = null;
            foreach (Unit controlled in caster.m_Controlled)
            {
                if (controlled.GetEntry() == NpcDkDancingRuneWeapon)
                {
                    drw = controlled;
                    break;
                }
            }

            if (drw == null || drw.GetVictim() == null)
                return;

            SpellInfo spellInfo = eventInfo.GetSpellInfo();
            if (spellInfo == null)
                return;

            DamageInfo damageInfo = eventInfo.GetDamageInfo();
            if (damageInfo == null || damageInfo.GetDamage() == 0)
                return;

            int amount = damageInfo.GetDamage() / 2;
            SpellNonMeleeDamage log = new(drw, drw.GetVictim(), spellInfo, new SpellCastVisual(spellInfo.GetSpellXSpellVisualId(drw)), spellInfo.GetSchoolMask());
            log.damage = amount;
            Unit.DealDamage(drw, drw.GetVictim(), amount, null, DamageEffectType.SpellDirect, spellInfo.GetSchoolMask(), spellInfo, true);
            drw.SendSpellNonMeleeDamageLog(log);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 1, AuraType.Dummy));
        }
    }

    [Script] // 43265 - Death and Decay
    class spell_dk_death_and_decay : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.TighteningGrasp, SpellIds.TighteningGraspSlow);
        }

        void HandleDummy()
        {
            if (GetCaster().HasAura(SpellIds.TighteningGrasp))
            {
                WorldLocation pos = GetExplTargetDest();
                if (pos != null)
                    GetCaster().CastSpell(pos, SpellIds.TighteningGraspSlow, true);
            }
        }

        public override void Register()
        {
            OnCast.Add(new(HandleDummy));
        }
    }

    [Script] // 43265 - Death and Decay (Aura)
    class spell_dk_death_and_decay_AuraScript : AuraScript
    {
        void HandleDummyTick(AuraEffect aurEff)
        {
            Unit caster = GetCaster();
            if (caster != null)
                caster.CastSpell(GetTarget(), SpellIds.DeathAndDecayDamage, aurEff);
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(HandleDummyTick, 2, AuraType.PeriodicDummy));
        }
    }

    [Script] // 47541 - Death Coil
    class spell_dk_death_coil : SpellScript
    {
        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellIds.DeathCoilDamage, SpellIds.Unholy, SpellIds.UnholyVigor);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            caster.CastSpell(GetHitUnit(), SpellIds.DeathCoilDamage, true);
            AuraEffect unholyAura = caster.GetAuraEffect(SpellIds.Unholy, 6);
            if (unholyAura != null) // can be any effect, just here to send SpellCastResult.DontReport on failure
                caster.CastSpell(caster, SpellIds.UnholyVigor, unholyAura);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 52751 - Death Gate
    class spell_dk_death_gate : SpellScript
    {
        SpellCastResult CheckClass()
        {
            if (GetCaster().GetClass() != Class.DeathKnight)
            {
                SetCustomCastResultMessage(SpellCustomErrors.MustBeDeathKnight);
                return SpellCastResult.CustomError;
            }

            return SpellCastResult.SpellCastOk;
        }

        void HandleScript(int effIndex)
        {
            PreventHitDefaultEffect(effIndex);
            Unit target = GetHitUnit();
            if (target != null)
                target.CastSpell(target, GetEffectValue(), false);
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckClass));
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 49576 - Death Grip Initial
    class spell_dk_death_grip_initial : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.DeathGripDummy, SpellIds.DeathGripJump, SpellIds.DeathGripTaunt, SpellIds.Blood);
        }

        SpellCastResult CheckCast()
        {
            Unit caster = GetCaster();
            // Death Grip should not be castable while jumping/falling
            if (caster.HasUnitState(UnitState.Jumping) || caster.HasUnitMovementFlag(MovementFlag.Falling))
                return SpellCastResult.Moving;

            return SpellCastResult.SpellCastOk;
        }

        void HandleDummy(int effIndex)
        {
            GetCaster().CastSpell(GetHitUnit(), SpellIds.DeathGripDummy, true);
            GetHitUnit().CastSpell(GetCaster(), SpellIds.DeathGripJump, true);
            if (GetCaster().HasAura(SpellIds.Blood))
                GetCaster().CastSpell(GetHitUnit(), SpellIds.DeathGripTaunt, true);
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckCast));
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 48743 - Death Pact
    class spell_dk_death_pact : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect((spellInfo.Id, 2));
        }

        void HandleCalcAmount(AuraEffect aurEff, ref int amount, ref bool canBeRecalculated)
        {
            Unit caster = GetCaster();
            if (caster != null)
                amount = (int)caster.CountPctFromMaxHealth(GetEffectInfo(2).CalcValue(caster));
        }

        public override void Register()
        {
            DoEffectCalcAmount.Add(new(HandleCalcAmount, 1, AuraType.SchoolHealAbsorb));
        }
    }

    [Script] // 49998 - Death Strike
    class spell_dk_death_strike : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.DeathStrikeEnabler, SpellIds.DeathStrikeHeal, SpellIds.BloodShieldMastery, SpellIds.BloodShieldAbsorb, SpellIds.Frost, SpellIds.DeathStrikeOffhand)
                && ValidateSpellEffect((spellInfo.Id, 2));
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();

            AuraEffect enabler = caster.GetAuraEffect(SpellIds.DeathStrikeEnabler, 0, GetCaster().GetGUID());
            if (enabler != null)
            {
                // Heals you for 25% of all damage taken in the last 5 sec,
                int heal = MathFunctions.CalculatePct(enabler.CalculateAmount(GetCaster()), GetEffectInfo(1).CalcValue(GetCaster()));
                // minimum 7.0% of maximum health.
                int pctOfMaxHealth = MathFunctions.CalculatePct(GetEffectInfo(2).CalcValue(GetCaster()), caster.GetMaxHealth());
                heal = Math.Max(heal, pctOfMaxHealth);

                caster.CastSpell(caster, SpellIds.DeathStrikeHeal, new CastSpellExtraArgs(TriggerCastFlags.FullMask).AddSpellMod(SpellValueMod.BasePoint0, heal));

                AuraEffect aurEff = caster.GetAuraEffect(SpellIds.BloodShieldMastery, 0);
                if (aurEff != null)
                    caster.CastSpell(caster, SpellIds.BloodShieldAbsorb, new CastSpellExtraArgs(TriggerCastFlags.FullMask).AddSpellMod(SpellValueMod.BasePoint0, MathFunctions.CalculatePct(heal, aurEff.GetAmount())));

                if (caster.HasAura(SpellIds.Frost))
                    caster.CastSpell(GetHitUnit(), SpellIds.DeathStrikeOffhand, true);
            }
        }

        void TriggerRecentlyUsedDeathStrike()
        {
            GetCaster().CastSpell(GetCaster(), SpellIds.RecentlyUsedDeathStrike, true);
        }

        public override void Register()
        {
            OnEffectLaunch.Add(new(HandleDummy, 1, SpellEffectName.Dummy));
            AfterCast.Add(new(TriggerRecentlyUsedDeathStrike));
        }
    }

    [Script] // 89832 - Death Strike Enabler - SpellDeathStrikeEnabler
    class spell_dk_death_strike_enabler : AuraScript
    {
        // Amount of seconds we calculate damage over
        const byte LastSeconds = 5;

        int[] _damagePerSecond = new int[LastSeconds];

        bool CheckProc(ProcEventInfo eventInfo)
        {
            return eventInfo.GetDamageInfo() != null;
        }

        void Update(AuraEffect aurEff)
        {
            // Move backwards all datas by one from [23][0][0][0][0] -> [0][23][0][0][0]
            _damagePerSecond = Enumerable.Range(1, _damagePerSecond.Length).Select(i => _damagePerSecond[i % _damagePerSecond.Length]).ToArray();
            _damagePerSecond[0] = 0;
        }

        void HandleCalcAmount(AuraEffect aurEff, ref int amount, ref bool canBeRecalculated)
        {
            canBeRecalculated = true;
            amount = Enumerable.Range(1, _damagePerSecond.Length).Sum();
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            _damagePerSecond[0] += eventInfo.GetDamageInfo().GetDamage();
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.PeriodicDummy));
            DoEffectCalcAmount.Add(new(HandleCalcAmount, 0, AuraType.PeriodicDummy));
            OnEffectUpdatePeriodic.Add(new(Update, 0, AuraType.PeriodicDummy));
        }
    }

    [Script] // 85948 - Festering Strike
    class spell_dk_festering_strike : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.FesteringWound);
        }

        void HandleScriptEffect(int effIndex)
        {
            GetCaster().CastSpell(GetHitUnit(), SpellIds.FesteringWound, new CastSpellExtraArgs(TriggerCastFlags.FullMask).AddSpellMod(SpellValueMod.AuraStack, GetEffectValue()));
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScriptEffect, 1, SpellEffectName.Dummy));
        }
    }

    [Script] // 47496 - Explode, Ghoul spell for Corpse Explosion
    class spell_dk_ghoul_explode : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.CorpseExplosionTriggered) && ValidateSpellEffect((spellInfo.Id, 2));
        }

        void HandleDamage(int effIndex)
        {
            SetHitDamage((int)GetCaster().CountPctFromMaxHealth(GetEffectInfo(2).CalcValue(GetCaster())));
        }

        void Suicide(int effIndex)
        {
            Unit unitTarget = GetHitUnit();
            if (unitTarget != null)
            {
                // Corpse Explosion (Suicide)
                unitTarget.CastSpell(unitTarget, SpellIds.CorpseExplosionTriggered, true);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDamage, 0, SpellEffectName.SchoolDamage));
            OnEffectHitTarget.Add(new(Suicide, 1, SpellEffectName.SchoolDamage));
        }
    }

    [Script] // 69961 - Glyph of Scourge Strike
    class spell_dk_glyph_of_scourge_strike_script : SpellScript
    {
        void HandleScriptEffect(int effIndex)
        {
            Unit caster = GetCaster();
            Unit target = GetHitUnit();

            foreach (var aurEff in target.GetAuraEffectsByType(AuraType.PeriodicDamage))
            {
                SpellInfo spellInfo = aurEff.GetSpellInfo();
                // search our Blood Plague and Frost Fever on target
                if (spellInfo.SpellFamilyName == SpellFamilyNames.Deathknight && (spellInfo.SpellFamilyFlags[2] & 0x2) != 0 &&
                    aurEff.GetCasterGUID() == caster.GetGUID())
                {
                    Milliseconds countMin = aurEff.GetBase().GetMaxDuration();
                    Milliseconds countMax = spellInfo.GetMaxDuration();

                    // this Glyph
                    countMax += (Seconds)9;

                    if (countMin < countMax)
                    {
                        aurEff.GetBase().SetDuration(aurEff.GetBase().GetDuration() + (Seconds)3);
                        aurEff.GetBase().SetMaxDuration(countMin + (Seconds)3);
                    }
                }
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScriptEffect, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 49184 - Howling Blast
    class spell_dk_howling_blast : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.FrostFever);
        }

        void HandleFrostFever(int effIndex)
        {
            GetCaster().CastSpell(GetHitUnit(), SpellIds.FrostFever);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleFrostFever, 0, SpellEffectName.SchoolDamage));
        }
    }

    [Script] // 206940 - Mark of Blood
    class spell_dk_mark_of_blood : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.MarkOfBloodHeal);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            Unit caster = GetCaster();
            if (caster != null)
                caster.CastSpell(eventInfo.GetProcTarget(), SpellIds.MarkOfBloodHeal, true);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 207346 - Necrosis
    class spell_dk_necrosis : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.NecrosisEffect);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(eventInfo.GetProcTarget(), SpellIds.NecrosisEffect, true);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 207256 - Obliteration
    class spell_dk_obliteration : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.Obliteration, SpellIds.ObliterationRuneEnergize, SpellIds.KillingMachineProc)
                && ValidateSpellEffect((SpellIds.Obliteration, 1));
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            Unit target = GetTarget();
            target.CastSpell(target, SpellIds.KillingMachineProc, aurEff);

            AuraEffect oblitaration = target.GetAuraEffect(SpellIds.Obliteration, 1);
            if (oblitaration != null)
                if (RandomHelper.randChance(oblitaration.GetAmount()))
                    target.CastSpell(target, SpellIds.ObliterationRuneEnergize, aurEff);
        }

        public override void Register()
        {
            AfterEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    // 121916 - Glyph of the Geist (Unholy)
    [Script] /// 6.x, does this belong here or in spell_generic? apply this in creature_template_addon? sniffs say this is always cast on raise dead.
    class spell_dk_pet_geist_transform : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.GlyphOfTheGeist);
        }

        public override bool Load()
        {
            return GetCaster().IsPet();
        }

        SpellCastResult CheckCast()
        {
            Unit owner = GetCaster().GetOwner();
            if (owner != null)
                if (owner.HasAura(SpellIds.GlyphOfTheGeist))
                    return SpellCastResult.SpellCastOk;

            return SpellCastResult.SpellUnavailable;
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckCast));
        }
    }

    // 147157 Glyph of the Skeleton (Unholy)
    [Script] /// 6.x, does this belong here or in spell_generic? apply this in creature_template_addon? sniffs say this is always cast on raise dead.
    class spell_dk_pet_skeleton_transform : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.GlyphOfTheSkeleton);
        }

        SpellCastResult CheckCast()
        {
            Unit owner = GetCaster().GetOwner();
            if (owner != null)
            {
                if (owner.HasAura(SpellIds.GlyphOfTheSkeleton))
                    return SpellCastResult.SpellCastOk;
            }

            return SpellCastResult.SpellUnavailable;
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckCast));
        }
    }

    // 61257 - Runic Power Back on Snare/Root
    [Script] /// 7.1.5
    class spell_dk_pvp_4p_bonus : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.RunicReturn);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            SpellInfo spellInfo = eventInfo.GetSpellInfo();
            if (spellInfo == null)
                return false;

            return (spellInfo.GetAllEffectsMechanicMask() & ((1 << (int)Mechanics.Root) | (1 << (int)Mechanics.Snare))) != 0;
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            eventInfo.GetActionTarget().CastSpell(null, SpellIds.RunicReturn, true);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 46584 - Raise Dead
    class spell_dk_raise_dead : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.RaiseDeadSummon, SpellIds.SludgeBelcher, SpellIds.SludgeBelcherSummon);
        }

        void HandleDummy(int effIndex)
        {
            int spellId = SpellIds.RaiseDeadSummon;
            if (GetCaster().HasAura(SpellIds.SludgeBelcher))
                spellId = SpellIds.SludgeBelcherSummon;

            GetCaster().CastSpell(null, spellId, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 59057 - Rime
    class spell_dk_rime : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect((spellInfo.Id, 1)) && ValidateSpellInfo(SpellIds.FrostScythe);
        }

        bool CheckProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            float chance = GetSpellInfo().GetEffect(1).CalcValue(GetTarget());
            if (eventInfo.GetSpellInfo().Id == SpellIds.FrostScythe)
                chance /= 2.0f;

            return RandomHelper.randChance(chance);
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(CheckProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    // -54639 - Blood of the North
    // -49208 - Reaping
    // -49467 - Death Rune Mastery
    [Script]
    class spell_dk_death_rune : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.BloodOfTheNorth, SpellIds.Reaping, SpellIds.DeathRuneMastery);
        }

        public override bool Load()
        {
            return GetUnitOwner() is Player player && player.GetClass() == Class.DeathKnight;
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            Unit caster = eventInfo.GetActor();

            if (caster is not Player player)
                return false;

            if (player.GetClass() != Class.DeathKnight)
                return false;

            return true;
        }

        void HandleProc(ProcEventInfo eventInfo)
        {
            Player player = eventInfo.GetActor().ToPlayer();

            AuraEffect aurEff = GetEffect(0);
            if (aurEff == null)
                return;

            // Reset amplitude - set death rune remove timer to 30s
            aurEff.ResetPeriodic(true);

            int runesLeft = 1;

            // Death Rune Mastery [2086]^1
            if (GetSpellInfo().IconFileDataId == 135372)
                runesLeft = 2;

            foreach (var rune in Runes.RunesList.Values)
            {
                if (GetSpellInfo().IconFileDataId == 135372)
                {
                    if (rune.Type == RuneType.Blood)
                        continue;
                }
                else
                {
                    if (rune.Type != RuneType.Blood)
                        continue;
                }

                ServerTime currentTime = LoopTime.ServerTime;

                if (player.Runes.GetRuneCooldown(rune, currentTime) == TimeSpan.Zero)
                    continue;

                --runesLeft;
                // Mark aura as used
                player.Runes.ApplyConvertRuneAura(rune, aurEff);
            }
        }

        void PeriodicTick(AuraEffect aurEff)
        {
            // timer expired - remove death runes
            GetTarget().ToPlayer().Runes.RemoveConvertRuneAura(aurEff);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnProc.Add(new(HandleProc));
            OnEffectPeriodic.Add(new(PeriodicTick, 0, AuraType.PeriodicDummy));
        }
    }

    [Script] // 55233 - Vampiric Blood
    class spell_dk_vampiric_blood : AuraScript
    {
        void CalculateAmount(AuraEffect aurEff, ref int amount, ref bool canBeRecalculated)
        {
            amount = (int)GetUnitOwner().CountPctFromMaxHealth(amount);
        }

        public override void Register()
        {
            DoEffectCalcAmount.Add(new(CalculateAmount, 1, AuraType.ModIncreaseHealth2));
        }
    }
}