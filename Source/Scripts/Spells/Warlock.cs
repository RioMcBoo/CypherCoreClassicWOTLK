// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System.Collections.Generic;
using static Global;

namespace Scripts.Spells.Warlock
{
    struct SpellIds
    {
        public const int CreateHealthstone = 23517;
        public const int DemonicCircleAllowCast = 62388;
        public const int DemonicCircleSummon = 48018;
        public const int DemonicCircleTeleport = 48020;
        public const int DevourMagicHeal = 19658;
        public const int DrainSoulEnergize = 205292;
        public const int GlyphOfDemonTraining = 56249;
        public const int GlyphOfSoulSwap = 56226;
        public const int GlyphOfSuccubus = 56250;
        public const int ImmolatePeriodic = 157736;
        public const int ImprovedHealthFunnelBuffR1 = 60955;
        public const int ImprovedHealthFunnelBuffR2 = 60956;
        public const int ImprovedHealthFunnelR1 = 18703;
        public const int ImprovedHealthFunnelR2 = 18704;
        public const int RainOfFire = 5740;
        public const int RainOfFireDamage = 42223;
        public const int SeedOfCorruptionDamage = 27285;
        public const int SeedOfCorruptionGeneric = 32865;
        public const int ShadowBoltEnergize = 194192;
        public const int SoulshatterEffect = 32835;
        public const int SoulSwapCdMarker = 94229;
        public const int SoulSwapOverride = 86211;
        public const int SoulSwapModCost = 92794;
        public const int SoulSwapDotMarker = 92795;
        public const int UnstableAffliction = 30108;
        public const int UnstableAfflictionDispel = 31117;
        public const int Shadowflame = 37378;
        public const int Flameshadow = 37379;
        public const int SummonSuccubus = 712;
        public const int SummonIncubus = 365349;
        public const int StrengthenPactSuccubus = 366323;
        public const int StrengthenPactIncubus = 366325;
        public const int SuccubusPact = 365360;
        public const int IncubusPact = 365355;

        public const int GenReplenishment = 57669;
        public const int PriestShadowWordDeath = 32409;
    }

    [Script] // 710 - Banish
    class spell_warl_banish : SpellScript
    {
        public spell_warl_banish() { }

        void HandleBanish(SpellMissInfo missInfo)
        {
            if (missInfo != SpellMissInfo.Immune)
                return;

            Unit target = GetHitUnit();
            if (target != null)
            {
                // Casting Banish on a banished target will Remove applied aura
                Aura banishAura = target.GetAura(GetSpellInfo().Id, GetCaster().GetGUID());
                if (banishAura != null)
                    banishAura.Remove();
            }
        }

        public override void Register()
        {
            BeforeHit.Add(new(HandleBanish));
        }
    }

    [Script] // 111400 - Burning Rush
    class spell_warl_burning_rush : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect((spellInfo.Id, 1));
        }

        SpellCastResult CheckApplyAura()
        {
            Unit caster = GetCaster();

            if (caster.GetHealthPct() <= GetEffectInfo(1).CalcValue(caster))
            {
                SetCustomCastResultMessage(SpellCustomErrors.YouDontHaveEnoughHealth);
                return SpellCastResult.CustomError;
            }

            return SpellCastResult.SpellCastOk;
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckApplyAura));
        }
    }

    [Script] // 111400 - Burning Rush
    class spell_warl_burning_rush_AuraScript : AuraScript
    {
        void PeriodicTick(AuraEffect aurEff)
        {
            if (GetTarget().GetHealthPct() <= aurEff.GetAmount())
            {
                PreventDefaultAction();
                Remove();
            }
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(PeriodicTick, 1, AuraType.PeriodicDamagePercent));
        }
    }

    [Script] // 116858 - Chaos Bolt
    class spell_warl_chaos_bolt : SpellScript
    {
        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleDummy(int effIndex)
        {
            SetHitDamage(GetHitDamage() + MathFunctions.CalculatePct(GetHitDamage(), GetCaster().ToPlayer().m_activePlayerData.SpellCritPercentage[(int)SpellSchools.Normal]));
        }

        void CalcCritChance(Unit victim, ref float critChance)
        {
            critChance = 100.0f;
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.SchoolDamage));
            OnCalcCritChance.Add(new(CalcCritChance));
        }
    }

    [Script] // 77220 - Mastery: Chaotic Energies
    class spell_warl_chaotic_energies : AuraScript
    {
        void HandleAbsorb(AuraEffect aurEff, DamageInfo dmgInfo, ref int absorbAmount)
        {
            AuraEffect effect1 = GetEffect(1);
            if (effect1 == null || !GetTargetApplication().HasEffect(1))
            {
                PreventDefaultAction();
                return;
            }

            // You take ${$s2/3}% reduced damage
            float damageReductionPct = (float)(effect1.GetAmount()) / 3;
            // plus a random amount of up to ${$s2/3}% additional reduced damage
            damageReductionPct += RandomHelper.FRand(0.0f, damageReductionPct);

            absorbAmount = MathFunctions.CalculatePct(dmgInfo.GetDamage(), damageReductionPct);
        }

        public override void Register()
        {
            OnEffectAbsorb.Add(new(HandleAbsorb, 2));
        }
    }

    [Script] // 6201 - Create Healthstone
    class spell_warl_create_healthstone : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.CreateHealthstone);
        }

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleScriptEffect(int effIndex)
        {
            GetCaster().CastSpell(GetCaster(), SpellIds.CreateHealthstone, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScriptEffect, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 108416 - Dark Pact
    class spell_warl_dark_pact : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect((spellInfo.Id, 1), (spellInfo.Id, 2));
        }

        void CalculateAmount(AuraEffect aurEff, ref int amount, ref bool canBeRecalculated)
        {
            canBeRecalculated = false;
            Unit caster = GetCaster();
            if (caster != null)
            {
                float extraAmount = caster.SpellBaseDamageBonusDone(GetSpellInfo().GetSchoolMask()) * 2.5f;
                long absorb = caster.CountPctFromCurHealth(GetEffectInfo(1).CalcValue(caster));
                caster.SetHealth(caster.GetHealth() - absorb);
                amount = (int)(MathFunctions.CalculatePct(absorb, GetEffectInfo(2).CalcValue(caster)) + extraAmount);
            }
        }

        public override void Register()
        {
            DoEffectCalcAmount.Add(new(CalculateAmount, 0, AuraType.SchoolAbsorb));
        }
    }

    [Script] // 48018 - Demonic Circle: Summon
    class spell_warl_demonic_circle_summon : AuraScript
    {
        void HandleRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            // If effect is Removed by expire Remove the summoned demonic circle too.
            if (!mode.HasFlag(AuraEffectHandleModes.Reapply))
                GetTarget().RemoveGameObject(GetId(), true);

            GetTarget().RemoveAura(SpellIds.DemonicCircleAllowCast);
        }

        void HandleDummyTick(AuraEffect aurEff)
        {
            GameObject circle = GetTarget().GetGameObject(GetId());
            if (circle != null)
            {
                // Here we check if player is in demonic circle teleport range, if so add
                // WarlockDemonicCircleAllowCast; allowing him to cast the WarlockDemonicCircleTeleport.
                // If not in range Remove the WarlockDemonicCircleAllowCast.

                SpellInfo spellInfo = SpellMgr.GetSpellInfo(SpellIds.DemonicCircleTeleport, GetCastDifficulty());

                if (GetTarget().IsWithinDist(circle, spellInfo.GetMaxRange(true)))
                {
                    if (!GetTarget().HasAura(SpellIds.DemonicCircleAllowCast))
                        GetTarget().CastSpell(GetTarget(), SpellIds.DemonicCircleAllowCast, true);
                }
                else
                    GetTarget().RemoveAura(SpellIds.DemonicCircleAllowCast);
            }
        }

        public override void Register()
        {
            OnEffectRemove.Add(new(HandleRemove, 0, AuraType.PeriodicDummy, AuraEffectHandleModes.RealOrReapplyMask));
            OnEffectPeriodic.Add(new(HandleDummyTick, 0, AuraType.PeriodicDummy));
        }
    }

    [Script] // 48020 - Demonic Circle: Teleport
    class spell_warl_demonic_circle_teleport : AuraScript
    {
        void HandleTeleport(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Player player = GetTarget().ToPlayer();
            if (player != null)
            {
                GameObject circle = player.GetGameObject(SpellIds.DemonicCircleSummon);
                if (circle != null)
                {
                    player.NearTeleportTo(circle.GetPositionX(), circle.GetPositionY(), circle.GetPositionZ(), circle.GetOrientation());
                    player.RemoveMovementImpairingAuras(false);
                }
            }
        }

        public override void Register()
        {
            OnEffectApply.Add(new(HandleTeleport, 0, AuraType.MechanicImmunity, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 67518, 19505 - Devour Magic
    class spell_warl_devour_magic : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.GlyphOfDemonTraining, SpellIds.DevourMagicHeal)
            && ValidateSpellEffect((spellInfo.Id, 1));
        }

        void OnSuccessfulDispel(int effIndex)
        {
            Unit caster = GetCaster();
            CastSpellExtraArgs args = new(TriggerCastFlags.FullMask);
            args.AddSpellMod(SpellValueMod.BasePoint0, GetEffectInfo(1).CalcValue(caster));

            caster.CastSpell(caster, SpellIds.DevourMagicHeal, args);

            // Glyph of Felhunter
            Unit owner = caster.GetOwner();
            if (owner?.GetAura(SpellIds.GlyphOfDemonTraining) != null)
                owner.CastSpell(owner, SpellIds.DevourMagicHeal, args);
        }

        public override void Register()
        {
            OnEffectSuccessfulDispel.Add(new(OnSuccessfulDispel, 0, SpellEffectName.Dispel));
        }
    }

    [Script] // 198590 - Drain Soul
    class spell_warl_drain_soul : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.DrainSoulEnergize);
        }

        void HandleRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            if (GetTargetApplication().GetRemoveMode() != AuraRemoveMode.Death)
                return;

            Unit caster = GetCaster();
            if (caster != null)
                caster.CastSpell(caster, SpellIds.DrainSoulEnergize, true);
        }

        public override void Register()
        {
            AfterEffectRemove.Add(new(HandleRemove, 0, AuraType.PeriodicDamage, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 48181 - Haunt
    class spell_warl_haunt : SpellScript
    {
        void HandleAfterHit()
        {
            Aura aura = GetHitAura();
            if (aura != null)
            {
                AuraEffect aurEff = aura.GetEffect(1);
                if (aurEff != null)
                    aurEff.SetAmount(MathFunctions.CalculatePct(GetHitDamage(), aurEff.GetAmount()));
            }
        }

        public override void Register()
        {
            AfterHit.Add(new(HandleAfterHit));
        }
    }

    [Script] // 755 - Health Funnel
    class spell_warl_health_funnel : AuraScript
    {
        void ApplyEffect(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Unit caster = GetCaster();
            if (caster == null)
                return;

            Unit target = GetTarget();
            if (caster.HasAura(SpellIds.ImprovedHealthFunnelR2))
                target.CastSpell(target, SpellIds.ImprovedHealthFunnelBuffR2, true);
            else if (caster.HasAura(SpellIds.ImprovedHealthFunnelR1))
                target.CastSpell(target, SpellIds.ImprovedHealthFunnelBuffR1, true);
        }

        void RemoveEffect(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Unit target = GetTarget();
            target.RemoveAurasDueToSpell(SpellIds.ImprovedHealthFunnelBuffR1);
            target.RemoveAurasDueToSpell(SpellIds.ImprovedHealthFunnelBuffR2);
        }

        void OnPeriodic(AuraEffect aurEff)
        {
            Unit caster = GetCaster();
            if (caster == null)
                return;
            //! Hack for self damage, is not blizz :/
            int damage = (int)caster.CountPctFromMaxHealth(aurEff.GetBaseAmount());

            Player modOwner = caster.GetSpellModOwner();
            if (modOwner != null)
                modOwner.ApplySpellMod(GetSpellInfo(), SpellModOp.PowerCost0, ref damage);

            SpellNonMeleeDamage damageInfo = new(caster, caster, GetSpellInfo(), GetAura().GetSpellVisual(), GetSpellInfo().SchoolMask, GetAura().GetCastId());
            damageInfo.periodicLog = true;
            damageInfo.damage = damage;
            caster.DealSpellDamage(damageInfo, false);
            caster.SendSpellNonMeleeDamageLog(damageInfo);
        }

        public override void Register()
        {
            OnEffectApply.Add(new(ApplyEffect, 0, AuraType.ModTotalHealthRegen, AuraEffectHandleModes.Real));
            OnEffectRemove.Add(new(RemoveEffect, 0, AuraType.ModTotalHealthRegen, AuraEffectHandleModes.Real));
            OnEffectPeriodic.Add(new(OnPeriodic, 0, AuraType.ModTotalHealthRegen));
        }
    }

    [Script] // 6262 - Healthstone
    class spell_warl_healthstone_heal : SpellScript
    {
        void HandleOnHit()
        {
            int heal = MathFunctions.CalculatePct(GetCaster().GetCreateHealth(), GetHitHeal());
            SetHitHeal(heal);
        }

        public override void Register()
        {
            OnHit.Add(new(HandleOnHit));
        }
    }

    [Script] // 348 - Immolate
    class spell_warl_immolate : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ImmolatePeriodic);
        }

        void HandleOnEffectHit(int effIndex)
        {
            GetCaster().CastSpell(GetHitUnit(), SpellIds.ImmolatePeriodic, GetSpell());
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleOnEffectHit, 0, SpellEffectName.SchoolDamage));
        }
    }

    [Script] // 366330 - Random Sayaad
    class spell_warl_random_sayaad : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.SuccubusPact, SpellIds.IncubusPact);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();

            caster.RemoveAurasDueToSpell(SpellIds.SuccubusPact);
            caster.RemoveAurasDueToSpell(SpellIds.IncubusPact);

            Player player = GetCaster().ToPlayer();
            if (player == null)
                return;

            Pet pet = player.GetPet();
            if (pet != null)
            {
                if (pet.IsPetSayaad())
                    pet.DespawnOrUnsummon();
            }
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // 366323 - Strengthen Pact - Succubus
    // 366325 - Strengthen Pact - Incubus
    [Script] // 366222 - Summon Sayaad
    class spell_warl_sayaad_precast_disorientation : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SharedConst.SpellPetSummoningDisorientation);
        }

        // Note: this is a special case in which the warlock's minion pet must also cast Summon Disorientation at the beginning Math.Since this is only handled by SpellEffectSummonPet in Spell.CheckCast.
        public override void OnPrecast()
        {
            Player player = GetCaster().ToPlayer();
            if (player == null)
                return;

            Pet pet = player.GetPet();
            if (pet != null)
                pet.CastSpell(pet, SharedConst.SpellPetSummoningDisorientation, new CastSpellExtraArgs(TriggerCastFlags.FullMask)
                    .SetOriginalCaster(pet.GetGUID())
                    .SetTriggeringSpell(GetSpell()));
        }

        public override void Register() { }
    }

    [Script] // 6358 - Seduction (Special Ability)
    class spell_warl_seduction : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.GlyphOfSuccubus, SpellIds.PriestShadowWordDeath);
        }

        void HandleScriptEffect(int effIndex)
        {
            Unit caster = GetCaster();
            Unit target = GetHitUnit();
            if (target != null)
            {
                if (caster.GetOwner() != null && caster.GetOwner().HasAura(SpellIds.GlyphOfSuccubus))
                {
                    target.RemoveAurasByType(AuraType.PeriodicDamage, ObjectGuid.Empty, target.GetAura(SpellIds.PriestShadowWordDeath)); // Sw:D shall not be Removed.
                    target.RemoveAurasByType(AuraType.PeriodicDamagePercent);
                    target.RemoveAurasByType(AuraType.PeriodicLeech);
                }
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScriptEffect, 0, SpellEffectName.ApplyAura));
        }
    }

    [Script] // 27285 - Seed of Corruption
    class spell_warl_seed_of_corruption : SpellScript
    {
        void FilterTargets(List<WorldObject> targets)
        {
            if (GetExplTargetUnit() != null)
                targets.Remove(GetExplTargetUnit());
        }

        public override void Register()
        {
            OnObjectAreaTargetSelect.Add(new(FilterTargets, 0, Targets.UnitDestAreaEnemy));
        }
    }

    [Script] // 27243 - Seed of Corruption
    class spell_warl_seed_of_corruption_dummy : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.SeedOfCorruptionDamage);
        }

        void CalculateBuffer(AuraEffect aurEff, ref int amount, ref bool canBeRecalculated)
        {
            Unit caster = GetCaster();
            if (caster == null)
                return;

            amount = caster.SpellBaseDamageBonusDone(GetSpellInfo().GetSchoolMask()) * GetEffectInfo(0).CalcValue(caster) / 100;
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            DamageInfo damageInfo = eventInfo.GetDamageInfo();
            if (damageInfo == null || damageInfo.GetDamage() == 0)
                return;

            int amount = aurEff.GetAmount() - damageInfo.GetDamage();
            if (amount > 0)
            {
                aurEff.SetAmount(amount);
                if (!GetTarget().HealthBelowPctDamaged(1, damageInfo.GetDamage()))
                    return;
            }

            Remove();

            Unit caster = GetCaster();
            if (caster == null)
                return;

            caster.CastSpell(eventInfo.GetActionTarget(), SpellIds.SeedOfCorruptionDamage, aurEff);
        }

        public override void Register()
        {
            DoEffectCalcAmount.Add(new(CalculateBuffer, 2, AuraType.Dummy));
            OnEffectProc.Add(new(HandleProc, 2, AuraType.Dummy));
        }
    }

    // 32863 - Seed of Corruption
    // 36123 - Seed of Corruption
    // 38252 - Seed of Corruption
    // 39367 - Seed of Corruption
    // 44141 - Seed of Corruption
    // 70388 - Seed of Corruption
    [Script] // Monster spells, triggered only on amount drop (not on death)
    class spell_warl_seed_of_corruption_generic : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.SeedOfCorruptionGeneric);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            DamageInfo damageInfo = eventInfo.GetDamageInfo();
            if (damageInfo == null || damageInfo.GetDamage() == 0)
                return;

            int amount = aurEff.GetAmount() - damageInfo.GetDamage();
            if (amount > 0)
            {
                aurEff.SetAmount(amount);
                return;
            }

            Remove();

            Unit caster = GetCaster();
            if (caster == null)
                return;

            caster.CastSpell(eventInfo.GetActionTarget(), SpellIds.SeedOfCorruptionGeneric, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 1, AuraType.Dummy));
        }
    }

    [Script] // 686 - Shadow Bolt
    class spell_warl_shadow_bolt : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ShadowBoltEnergize);
        }

        void HandleAfterCast()
        {
            GetCaster().CastSpell(GetCaster(), SpellIds.ShadowBoltEnergize, true);
        }

        public override void Register()
        {
            AfterCast.Add(new(HandleAfterCast));
        }
    }

    [Script] // 86121 - Soul Swap
    class spell_warl_soul_swap : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.GlyphOfSoulSwap, SpellIds.SoulSwapCdMarker, SpellIds.SoulSwapOverride);
        }

        void HandleHit(int effIndex)
        {
            GetCaster().CastSpell(GetCaster(), SpellIds.SoulSwapOverride, true);
            GetHitUnit().CastSpell(GetCaster(), SpellIds.SoulSwapDotMarker, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleHit, 0, SpellEffectName.SchoolDamage));
        }
    }

    [Script] // 86211 - Soul Swap Override - Also acts as a dot container
    class spell_warl_soul_swap_override : AuraScript
    {
        List<int> _dotList = new();
        Unit _swapCaster;

        //! Forced to, pure virtual functions must have a body when linking
        public override void Register() { }

        public void AddDot(int id) { _dotList.Add(id); }

        public List<int> GetDotList() { return _dotList; }

        public Unit GetOriginalSwapSource() { return _swapCaster; }

        public void SetOriginalSwapSource(Unit victim) { _swapCaster = victim; }
    }

    [Script] //! Soul Swap Copy Spells - 92795 - Simply copies spell IDs.
    class spell_warl_soul_swap_dot_marker : SpellScript
    {
        void HandleHit(int effIndex)
        {
            Unit swapVictim = GetCaster();
            Unit warlock = GetHitUnit();
            if (warlock == null || swapVictim == null)
                return;

            var appliedAuras = swapVictim.GetAppliedAuras();
            spell_warl_soul_swap_override swapSpellScript = null;
            Aura swapOverrideAura = warlock.GetAura(SpellIds.SoulSwapOverride);
            if (swapOverrideAura != null)
                swapSpellScript = swapOverrideAura.GetScript<spell_warl_soul_swap_override>();

            if (swapSpellScript == null)
                return;

            FlagArray128 classMask = GetEffectInfo().SpellClassMask;

            foreach (var (id, aurApp) in appliedAuras)
            {
                SpellInfo spellProto = aurApp.GetBase().GetSpellInfo();
                if (aurApp.GetBase().GetCaster() == warlock)
                    if (spellProto.SpellFamilyName == SpellFamilyNames.Warlock && (spellProto.SpellFamilyFlags & classMask))
                        swapSpellScript.AddDot(id);
            }

            swapSpellScript.SetOriginalSwapSource(swapVictim);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleHit, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 86213 - Soul Swap Exhale
    class spell_warl_soul_swap_exhale : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.SoulSwapModCost, SpellIds.SoulSwapOverride);
        }

        SpellCastResult CheckCast()
        {
            Unit currentTarget = GetExplTargetUnit();
            Unit swapTarget = null;
            Aura swapOverride = GetCaster().GetAura(SpellIds.SoulSwapOverride);
            if (swapOverride != null)
            {
                spell_warl_soul_swap_override swapScript = swapOverride.GetScript<spell_warl_soul_swap_override>();
                if (swapScript != null)
                    swapTarget = swapScript.GetOriginalSwapSource();
            }

            // Soul Swap Exhale can't be cast on the same target than Soul Swap
            if (swapTarget != null && currentTarget != null && swapTarget == currentTarget)
                return SpellCastResult.BadTargets;

            return SpellCastResult.SpellCastOk;
        }

        void OnEffectHitTargetTemp(int effIndex)
        {
            GetCaster().CastSpell(GetCaster(), SpellIds.SoulSwapModCost, true);
            bool hasGlyph = GetCaster().HasAura(SpellIds.GlyphOfSoulSwap);

            List<int> dotList = new();
            Unit swapSource = null;
            Aura swapOverride = GetCaster().GetAura(SpellIds.SoulSwapOverride);
            if (swapOverride != null)
            {
                spell_warl_soul_swap_override swapScript = swapOverride.GetScript<spell_warl_soul_swap_override>();
                if (swapScript == null)
                    return;
                dotList = swapScript.GetDotList();
                swapSource = swapScript.GetOriginalSwapSource();
            }

            if (dotList.Empty())
                return;

            foreach (var spellId in dotList)
            {
                GetCaster().AddAura(spellId, GetHitUnit());
                if (!hasGlyph && swapSource != null)
                    swapSource.RemoveAurasDueToSpell(spellId);
            }

            // Remove Soul Swap Exhale buff
            GetCaster().RemoveAurasDueToSpell(SpellIds.SoulSwapOverride);

            if (hasGlyph) // Add a cooldown on Soul Swap if caster has the glyph
                GetCaster().CastSpell(GetCaster(), SpellIds.SoulSwapCdMarker, false);
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckCast));
            OnEffectHitTarget.Add(new(OnEffectHitTargetTemp, 0, SpellEffectName.SchoolDamage));
        }
    }

    [Script] // 29858 - Soulshatter
    class spell_warl_soulshatter : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.SoulshatterEffect);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            Unit target = GetHitUnit();
            if (target != null)
                if (target.GetThreatManager().IsThreatenedBy(caster, true))
                    caster.CastSpell(target, SpellIds.SoulshatterEffect, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 366323 - Strengthen Pact - Succubus
    class spell_warl_strengthen_pact_succubus : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.SuccubusPact, SpellIds.SummonSuccubus);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();

            caster.CastSpell(null, SpellIds.SuccubusPact, TriggerCastFlags.FullMask);
            caster.CastSpell(null, SpellIds.SummonSuccubus, TriggerCastFlags.FullMask);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 366325 - Strengthen Pact - Incubus
    class spell_warl_strengthen_pact_incubus : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.IncubusPact, SpellIds.SummonIncubus);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();

            caster.CastSpell(null, SpellIds.IncubusPact, TriggerCastFlags.FullMask);
            caster.CastSpell(null, SpellIds.SummonIncubus, TriggerCastFlags.FullMask);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 366222 - Summon Sayaad
    class spell_warl_summon_sayaad : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.SummonSuccubus, SpellIds.SummonIncubus);
        }

        void HandleDummy(int effIndex)
        {
            GetCaster().CastSpell(null, RandomHelper.randChance(50) ? SpellIds.SummonSuccubus : SpellIds.SummonIncubus, TriggerCastFlags.FullMask);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // 37377 - Shadowflame
    // 39437 - Shadowflame Hellfire and RoF
    [Script("spell_warl_t4_2p_bonus_shadow", SpellIds.Flameshadow)]
    [Script("spell_warl_t4_2p_bonus_fire", SpellIds.Shadowflame)]
    class spell_warl_t4_2p_bonus : AuraScript
    {
        int _triggerId;

        public spell_warl_t4_2p_bonus(int triggerId)
        {
            _triggerId = triggerId;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(_triggerId);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            Unit caster = eventInfo.GetActor();
            caster.CastSpell(caster, _triggerId, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 30108, 34438, 34439, 35183 - Unstable Affliction
    class spell_warl_unstable_affliction : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.UnstableAfflictionDispel);
        }

        void HandleDispel(DispelInfo dispelInfo)
        {
            Unit caster = GetCaster();
            if (caster != null)
            {
                AuraEffect aurEff = GetEffect(1);
                if (aurEff != null)
                {
                    Unit target = dispelInfo.GetDispeller().ToUnit();
                    if (target != null)
                    {
                        int bp = aurEff.GetAmount();
                        bp = target.SpellDamageBonusTaken(caster, aurEff.GetSpellInfo(), bp, DamageEffectType.DOT);
                        bp *= 9;

                        // backfire damage and silence
                        CastSpellExtraArgs args = new(aurEff);
                        args.AddSpellMod(SpellValueMod.BasePoint0, bp);
                        caster.CastSpell(target, SpellIds.UnstableAfflictionDispel, args);
                    }
                }
            }
        }

        public override void Register()
        {
            AfterDispel.Add(new(HandleDispel));
        }
    }

    // 5740 - Rain of Fire
    [Script] /// Updated 7.1.5
    class spell_warl_rain_of_fire : AuraScript
    {
        void HandleDummyTick(AuraEffect aurEff)
        {
            List<AreaTrigger> rainOfFireAreaTriggers = GetTarget().GetAreaTriggers(SpellIds.RainOfFire);
            List<ObjectGuid> targetsInRainOfFire = new();

            foreach (AreaTrigger rainOfFireAreaTrigger in rainOfFireAreaTriggers)
            {
                List<ObjectGuid> insideTargets = rainOfFireAreaTrigger.GetInsideUnits();
                targetsInRainOfFire.AddRange(insideTargets);
            }

            foreach (ObjectGuid insideTargetGuid in targetsInRainOfFire)
            {
                Unit insideTarget = ObjAccessor.GetUnit(GetTarget(), insideTargetGuid);
                if (insideTarget != null)
                    if (!GetTarget().IsFriendlyTo(insideTarget))
                        GetTarget().CastSpell(insideTarget, SpellIds.RainOfFireDamage, true);
            }
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(HandleDummyTick, 3, AuraType.PeriodicDummy));
        }
    }
}