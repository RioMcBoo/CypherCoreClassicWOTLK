// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.AI;
using Game.DataStorage;
using Game.Entities;
using Game.Miscellaneous;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using static Global;

namespace Scripts.Spells.Paladin
{
    struct SpellIds
    {
        public const int ArdentDefenderHeal = 66235;
        public const int AvengersShield = 31935;
        public const int AvengingWrath = 31884;
        public const int BeaconOfLight = 53563;
        public const int BeaconOfLightHeal = 53652;
        public const int ConcentractionAura = 19746;
        public const int ConsecratedGroundPassive = 204054;
        public const int ConsecratedGroundSlow = 204242;
        public const int Consecration = 26573;
        public const int ConsecrationDamage = 81297;
        public const int ConsecrationProtectionAura = 188370;
        public const int EnduringLight = 40471;
        public const int EnduringJudgement = 40472;
        public const int FinalStand = 204077;
        public const int FinalStandEffect = 204079;
        public const int Forbearance = 25771;
        public const int GuardianOfAncientKings = 86659;
        public const int HammerOfJustice = 853;
        public const int HammerOfTheRighteousAoe = 88263;
        public const int HandOfSacrifice = 6940;
        public const int HolyMending = 64891;
        public const int HolyPowerArmor = 28790;
        public const int HolyPowerAttackPower = 28791;
        public const int HolyPowerSpellPower = 28793;
        public const int HolyPowerMp5 = 28795;
        public const int HolyShock = 20473;
        public const int HolyShockDamage = 25912;
        public const int HolyShockHealing = 25914;
        public const int HolyLight = 82326;
        public const int InfusionOfLightEnergize = 356717;
        public const int ImmuneShieldMarker = 61988;
        public const int ItemHealingTrance = 37706;
        public const int JudgmentGainHolyPower = 220637;
        public const int JudgmentHolyR3 = 231644;
        public const int JudgmentHolyR3Debuff = 214222;
        public const int JudgmentProtRetR3 = 315867;

        public const int AshenHallow = 316958;
        public const int AshenHallowDamage = 317221;
        public const int AshenHallowHeal = 317223;
        public const int AshenHallowAllowHammer = 330382;
    }

    [Script] // 31850 - Ardent Defender
    class spell_pal_ardent_defender : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ArdentDefenderHeal)
            && ValidateSpellEffect((spellInfo.Id, 1));
        }

        void HandleAbsorb(AuraEffect aurEff, DamageInfo dmgInfo, ref int absorbAmount)
        {
            PreventDefaultAction();

            int targetHealthPercent = GetEffectInfo(1).CalcValue(GetTarget());
            long targetHealth = GetTarget().CountPctFromMaxHealth(targetHealthPercent);
            if (GetTarget().HealthBelowPct(targetHealthPercent))
            {
                // we are currently below desired health
                // absorb everything and heal up
                GetTarget().CastSpell(GetTarget(), SpellIds.ArdentDefenderHeal,
                    new CastSpellExtraArgs(aurEff)
                    .AddSpellMod(SpellValueMod.BasePoint0, (int)(targetHealth - GetTarget().GetHealth())));
            }
            else
            {
                // we are currently above desired health
                // just absorb enough to reach that percentage
                absorbAmount = dmgInfo.GetDamage() - (int)(GetTarget().GetHealth() - targetHealth);
            }

            Remove();
        }

        public override void Register()
        {
            OnEffectAbsorb.Add(new(HandleAbsorb, 2));
        }
    }

    [Script] // 19042 - Ashen Hallow
    class areatrigger_pal_ashen_hallow : AreaTriggerAI
    {
        TimeSpan _refreshTimer;
        TimeSpan _period;

        public areatrigger_pal_ashen_hallow(AreaTrigger areatrigger) : base(areatrigger) { }

        void RefreshPeriod()
        {
            Unit caster = at.GetCaster();
            if (caster != null)
            {
                AuraEffect ashen = caster.GetAuraEffect(SpellIds.AshenHallow, 1);
                if (ashen != null)
                    _period = Time.SpanFromMilliseconds(ashen.GetPeriod());
            }
        }

        public override void OnCreate(Spell creatingSpell)
        {
            RefreshPeriod();
            _refreshTimer = _period;
        }

        public override void OnUpdate(TimeSpan diff)
        {
            _refreshTimer -= diff;

            while (_refreshTimer <= Time.SpanFromSeconds(0))
            {
                Unit caster = at.GetCaster();
                if (caster != null)
                {
                    caster.CastSpell(at.GetPosition(), SpellIds.AshenHallowHeal);
                    caster.CastSpell(at.GetPosition(), SpellIds.AshenHallowDamage);
                }

                RefreshPeriod();

                _refreshTimer += _period;
            }
        }

        public override void OnUnitEnter(Unit unit)
        {
            if (unit.GetGUID() == at.GetCasterGuid())
                unit.CastSpell(unit, SpellIds.AshenHallowAllowHammer, true);
        }

        public override void OnUnitExit(Unit unit)
        {
            if (unit.GetGUID() == at.GetCasterGuid())
                unit.RemoveAura(SpellIds.AshenHallowAllowHammer);
        }
    }

    // 1022 - Blessing of Protection
    [Script]
    class spell_pal_blessing_of_protection : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.Forbearance, SpellIds.ImmuneShieldMarker) && spellInfo.ExcludeTargetAuraSpell == SpellIds.ImmuneShieldMarker;
        }

        SpellCastResult CheckForbearance()
        {
            Unit target = GetExplTargetUnit();
            if (target == null || target.HasAura(SpellIds.Forbearance))
                return SpellCastResult.TargetAurastate;

            return SpellCastResult.SpellCastOk;
        }

        void TriggerForbearance()
        {
            Unit target = GetHitUnit();
            if (target != null)
            {
                GetCaster().CastSpell(target, SpellIds.Forbearance, true);
                GetCaster().CastSpell(target, SpellIds.ImmuneShieldMarker, true);
            }
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckForbearance));
            AfterHit.Add(new(TriggerForbearance));
        }
    }

    [Script] // 26573 - Consecration
    class spell_pal_consecration : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ConsecrationDamage, SpellIds.ConsecrationProtectionAura, 
                SpellIds.ConsecratedGroundPassive, SpellIds.ConsecratedGroundSlow);
        }

        void HandleEffectPeriodic(AuraEffect aurEff)
        {
            AreaTrigger at = GetTarget().GetAreaTrigger(SpellIds.Consecration);
            if (at != null)
                GetTarget().CastSpell(at.GetPosition(), SpellIds.ConsecrationDamage);
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(HandleEffectPeriodic, 0, AuraType.PeriodicDummy));
        }
    }

    // 26573 - Consecration
    [Script] //  9228 - AreaTriggerId
    class areatrigger_pal_consecration : AreaTriggerAI
    {
        public areatrigger_pal_consecration(AreaTrigger areatrigger) : base(areatrigger) { }

        public override void OnUnitEnter(Unit unit)
        {
            Unit caster = at.GetCaster();
            if (caster != null)
            {
                // 243597 is also being cast as protection, but CreateObject is not sent, either serverside areatrigger for this aura or unused - also no visual is seen
                if (unit == caster && caster.IsPlayer()
                    && caster.ToPlayer().GetPrimarySpecialization() == ChrSpecialization.PaladinProtection)
                {
                    caster.CastSpell(caster, SpellIds.ConsecrationProtectionAura);
                }

                if (caster.IsValidAttackTarget(unit))
                    if (caster.HasAura(SpellIds.ConsecratedGroundPassive))
                        caster.CastSpell(unit, SpellIds.ConsecratedGroundSlow);
            }
        }

        public override void OnUnitExit(Unit unit)
        {
            if (at.GetCasterGuid() == unit.GetGUID())
                unit.RemoveAurasDueToSpell(SpellIds.ConsecrationProtectionAura, at.GetCasterGuid());

            unit.RemoveAurasDueToSpell(SpellIds.ConsecratedGroundSlow, at.GetCasterGuid());
        }
    }

    [Script] // 642 - Divine Shield
    class spell_pal_divine_shield : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.FinalStand, SpellIds.FinalStandEffect, 
                SpellIds.Forbearance, SpellIds.ImmuneShieldMarker) 
                && spellInfo.ExcludeCasterAuraSpell == SpellIds.ImmuneShieldMarker;
        }

        SpellCastResult CheckForbearance()
        {
            if (GetCaster().HasAura(SpellIds.Forbearance))
                return SpellCastResult.TargetAurastate;

            return SpellCastResult.SpellCastOk;
        }

        void HandleFinalStand()
        {
            if (GetCaster().HasAura(SpellIds.FinalStand))
                GetCaster().CastSpell(null, SpellIds.FinalStandEffect, true);
        }

        void TriggerForbearance()
        {
            Unit caster = GetCaster();
            caster.CastSpell(caster, SpellIds.Forbearance, true);
            caster.CastSpell(caster, SpellIds.ImmuneShieldMarker, true);
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckForbearance));
            AfterCast.Add(new(HandleFinalStand));
            AfterCast.Add(new(TriggerForbearance));
        }
    }

    [Script] // 53385 - Divine Storm
    class spell_pal_divine_storm : SpellScript
    {
        const int PaladinVisualKitDivineStorm = 73892;

        public override bool Validate(SpellInfo spellInfo)
        {
            return CliDB.SpellVisualKitStorage.HasRecord(PaladinVisualKitDivineStorm);
        }

        void HandleOnCast()
        {
            GetCaster().SendPlaySpellVisualKit(PaladinVisualKitDivineStorm, 0, 0);
        }

        public override void Register()
        {
            OnCast.Add(new(HandleOnCast));
        }
    }

    [Script] // -85043 - Grand Crusader
    class spell_pal_grand_crusader : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.AvengersShield);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            return GetTarget().IsPlayer();
        }

        void HandleEffectProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            GetTarget().GetSpellHistory().ResetCooldown(SpellIds.AvengersShield, true);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleEffectProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 54968 - Glyph of Holy Light
    class spell_pal_glyph_of_holy_light : SpellScript
    {
        void FilterTargets(List<WorldObject> targets)
        {
            int maxTargets = GetSpellInfo().MaxAffectedTargets;

            if (targets.Count > maxTargets)
            {
                targets.Sort(new HealthPctOrderPred());
                targets.Resize(maxTargets);
            }
        }

        public override void Register()
        {
            OnObjectAreaTargetSelect.Add(new(FilterTargets, 0, Targets.UnitDestAreaAlly));
        }
    }

    [Script] // 53595 - Hammer of the Righteous
    class spell_pal_hammer_of_the_righteous : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ConsecrationProtectionAura, 
                SpellIds.HammerOfTheRighteousAoe);
        }

        void HandleAoEHit(int effIndex)
        {
            if (GetCaster().HasAura(SpellIds.ConsecrationProtectionAura))
                GetCaster().CastSpell(GetHitUnit(), SpellIds.HammerOfTheRighteousAoe);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleAoEHit, 0, SpellEffectName.SchoolDamage));
        }
    }

    [Script] // 6940 - Hand of Sacrifice
    class spell_pal_hand_of_sacrifice : AuraScript
    {
        int remainingAmount;

        public override bool Load()
        {
            Unit caster = GetCaster();
            if (caster != null)
            {
                remainingAmount = (int)caster.GetMaxHealth();
                return true;
            }
            return false;
        }

        void Split(AuraEffect aurEff, DamageInfo dmgInfo, int splitAmount)
        {
            remainingAmount -= splitAmount;

            if (remainingAmount <= 0)
            {
                GetTarget().RemoveAura(SpellIds.HandOfSacrifice);
            }
        }

        public override void Register()
        {
            OnEffectSplit.Add(new(Split, 0));
        }
    }

    [Script] // 54149 - Infusion of Light
    class spell_pal_infusion_of_light : AuraScript
    {
        FlagArray128 HolyLightSpellClassMask = new(0, 0, 0x400);

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.InfusionOfLightEnergize);
        }

        bool CheckFlashOfLightProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            return eventInfo.GetProcSpell() != null 
                && eventInfo.GetProcSpell().m_appliedMods.Contains(GetAura());
        }

        bool CheckHolyLightProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            return eventInfo.GetSpellInfo() != null 
                && eventInfo.GetSpellInfo().IsAffected(SpellFamilyNames.Paladin, HolyLightSpellClassMask);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            eventInfo.GetActor().CastSpell(eventInfo.GetActor(), SpellIds.InfusionOfLightEnergize,
                new CastSpellExtraArgs(
                    TriggerCastFlags.FullMask).SetTriggeringSpell(eventInfo.GetProcSpell()));
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(CheckFlashOfLightProc, 0, AuraType.AddPctModifier));
            DoCheckEffectProc.Add(new(CheckFlashOfLightProc, 2, AuraType.AddFlatModifier));

            DoCheckEffectProc.Add(new(CheckHolyLightProc, 1, AuraType.Dummy));
            OnEffectProc.Add(new(HandleProc, 1, AuraType.Dummy));
        }
    }

    // 20271 - Judgement (Retribution/Protection/Holy)
    [Script] 
    class spell_pal_judgment : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.JudgmentProtRetR3, SpellIds.JudgmentGainHolyPower, 
                SpellIds.JudgmentHolyR3, SpellIds.JudgmentHolyR3Debuff);
        }

        void HandleOnHit()
        {
            Unit caster = GetCaster();

            if (caster.HasSpell(SpellIds.JudgmentProtRetR3))
                caster.CastSpell(caster, SpellIds.JudgmentGainHolyPower, GetSpell());

            if (caster.HasSpell(SpellIds.JudgmentHolyR3))
                caster.CastSpell(GetHitUnit(), SpellIds.JudgmentHolyR3Debuff, GetSpell());
        }

        public override void Register()
        {
            OnHit.Add(new(HandleOnHit));
        }
    }

    [Script] // 20473 - Holy Shock
    class spell_pal_holy_shock : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.HolyShock, SpellIds.HolyShockHealing, 
                SpellIds.HolyShockDamage);
        }

        SpellCastResult CheckCast()
        {
            Unit caster = GetCaster();

            Unit target = GetExplTargetUnit();
            if (target != null)
            {
                if (!caster.IsFriendlyTo(target))
                {
                    if (!caster.IsValidAttackTarget(target))
                        return SpellCastResult.BadTargets;

                    if (!caster.IsInFront(target))
                        return SpellCastResult.UnitNotInfront;
                }
            }
            else
                return SpellCastResult.BadTargets;

            return SpellCastResult.SpellCastOk;
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();

            Unit unitTarget = GetHitUnit();
            if (unitTarget != null)
            {
                if (caster.IsFriendlyTo(unitTarget))
                    caster.CastSpell(unitTarget, SpellIds.HolyShockHealing, GetSpell());
                else
                    caster.CastSpell(unitTarget, SpellIds.HolyShockDamage, GetSpell());
            }
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckCast));
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 25912 - Holy Shock
    class spell_pal_holy_shock_damage_visual : SpellScript
    {
        const int PaladinVisualSpellHolyShockDamage = 83731;
        const int PaladinVisualSpellHolyShockDamageCrit = 83881;

        public override bool Validate(SpellInfo spellInfo)
        {
            return CliDB.SpellVisualStorage.HasRecord(PaladinVisualSpellHolyShockDamage)
                && CliDB.SpellVisualStorage.HasRecord(PaladinVisualSpellHolyShockDamageCrit);
        }

        void PlayVisual()
        {
            GetCaster().SendPlaySpellVisual(GetHitUnit(), 
                IsHitCrit() 
                ? PaladinVisualSpellHolyShockDamageCrit 
                : PaladinVisualSpellHolyShockDamage, 
                0, 0, 0.0f, false);
        }

        public override void Register()
        {
            AfterHit.Add(new(PlayVisual));
        }
    }

    [Script] // 25914 - Holy Shock
    class spell_pal_holy_shock_heal_visual : SpellScript
    {
        const int PaladinVisualSpellHolyShockHeal = 83732;
        const int PaladinVisualSpellHolyShockHealCrit = 83880;

        public override bool Validate(SpellInfo spellInfo)
        {
            return CliDB.SpellVisualStorage.HasRecord(PaladinVisualSpellHolyShockHeal)
                && CliDB.SpellVisualStorage.HasRecord(PaladinVisualSpellHolyShockHealCrit);
        }

        void PlayVisual()
        {
            GetCaster().SendPlaySpellVisual(GetHitUnit(), 
                IsHitCrit() 
                ? PaladinVisualSpellHolyShockHealCrit 
                : PaladinVisualSpellHolyShockHeal, 
                0, 0, 0.0f, false);
        }

        public override void Register()
        {
            AfterHit.Add(new(PlayVisual));
        }
    }

    [Script] // 37705 - Healing Discount
    class spell_pal_item_healing_discount : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ItemHealingTrance);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(GetTarget(), SpellIds.ItemHealingTrance, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 40470 - Paladin Tier 6 Trinket
    class spell_pal_item_t6_trinket : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.EnduringLight, SpellIds.EnduringJudgement);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            SpellInfo spellInfo = eventInfo.GetSpellInfo();
            if (spellInfo == null)
                return;

            int spellId;
            int chance;

            // Holy Light & Flash of Light
            if ((spellInfo.SpellFamilyFlags[0] & 0xC0000000) != 0)
            {
                spellId = SpellIds.EnduringLight;
                chance = 15;
            }
            // Judgements
            else if ((spellInfo.SpellFamilyFlags[0] & 0x00800000) != 0)
            {
                spellId = SpellIds.EnduringJudgement;
                chance = 50;
            }
            else
                return;

            if (RandomHelper.randChance(chance))
                eventInfo.GetActor().CastSpell(eventInfo.GetProcTarget(), spellId, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 633 - Lay on Hands
    class spell_pal_lay_on_hands : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.Forbearance, SpellIds.ImmuneShieldMarker) 
                && spellInfo.ExcludeTargetAuraSpell == SpellIds.ImmuneShieldMarker;
        }

        SpellCastResult CheckForbearance()
        {
            Unit target = GetExplTargetUnit();
            if (target == null || target.HasAura(SpellIds.Forbearance))
                return SpellCastResult.TargetAurastate;

            return SpellCastResult.SpellCastOk;
        }

        void TriggerForbearance()
        {
            Unit target = GetHitUnit();
            if (target != null)
            {
                GetCaster().CastSpell(target, SpellIds.Forbearance, true);
                GetCaster().CastSpell(target, SpellIds.ImmuneShieldMarker, true);
            }
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckForbearance));
            AfterHit.Add(new(TriggerForbearance));
        }
    }

    [Script] // 53651 - Light's Beacon - Beacon of Light
    class spell_pal_light_s_beacon : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.BeaconOfLight, SpellIds.BeaconOfLightHeal);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            if (eventInfo.GetActionTarget() == null)
                return false;

            if (eventInfo.GetActionTarget().HasAura(SpellIds.BeaconOfLight,
                eventInfo.GetActor().GetGUID()))
            {
                return false;
            }

            return true;
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            HealInfo healInfo = eventInfo.GetHealInfo();
            if (healInfo == null || healInfo.GetHeal() == 0)
                return;

            int heal = MathFunctions.CalculatePct(healInfo.GetHeal(), aurEff.GetAmount());

            var auras = GetCaster().GetSingleCastAuras();
            foreach (var aura in auras)
            {
                if (aura.GetId() == SpellIds.BeaconOfLight)
                {
                    List<AuraApplication> applications = aura.GetApplicationList();
                    if (!applications.Empty())
                    {
                        CastSpellExtraArgs args = new(aurEff);
                        args.AddSpellMod(SpellValueMod.BasePoint0, heal);
                        eventInfo.GetActor().CastSpell(
                            applications.FirstOrDefault().GetTarget(), SpellIds.BeaconOfLightHeal, args);
                    }
                    return;
                }
            }
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 28789 - Holy Power
    class spell_pal_t3_6p_bonus : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.HolyPowerArmor, 
                SpellIds.HolyPowerAttackPower, SpellIds.HolyPowerSpellPower, 
                SpellIds.HolyPowerMp5);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            int spellId;
            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetProcTarget();

            switch (target.GetClass())
            {
                case Class.Paladin:
                case Class.Priest:
                case Class.Shaman:
                case Class.Druid:
                    spellId = SpellIds.HolyPowerMp5;
                    break;
                case Class.Mage:
                case Class.Warlock:
                    spellId = SpellIds.HolyPowerSpellPower;
                    break;
                case Class.Hunter:
                case Class.Rogue:
                    spellId = SpellIds.HolyPowerAttackPower;
                    break;
                case Class.Warrior:
                    spellId = SpellIds.HolyPowerArmor;
                    break;
                default:
                    return;
            }

            caster.CastSpell(target, spellId, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 64890 - Item - Paladin T8 Holy 2P Bonus
    class spell_pal_t8_2p_bonus : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.HolyMending);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            HealInfo healInfo = eventInfo.GetHealInfo();
            if (healInfo == null || healInfo.GetHeal() == 0)
                return;

            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetProcTarget();

            SpellInfo spellInfo = SpellMgr.GetSpellInfo(SpellIds.HolyMending, GetCastDifficulty());
            int amount = MathFunctions.CalculatePct(healInfo.GetHeal(), aurEff.GetAmount());

            Cypher.Assert(spellInfo.GetMaxTicks() > 0);
            amount /= spellInfo.GetMaxTicks();

            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, amount);
            caster.CastSpell(target, SpellIds.HolyMending, args);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }
}