// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;

using static Global;

namespace Scripts.Spells.Shaman
{

    struct SpellIds
    {
        public const int ChainLightningOverloadEnergize = 218558;
        public const int ChainedHeal = 70809;
        public const int DoomWindsLegendaryCooldown = 335904;
        public const int Earthquake = 61882;
        public const int EarthquakeKnockingDown = 77505;
        public const int EarthquakeTick = 77478;        
        public const int EchoesOfGreatSunderingLegendary = 336217;
        public const int EchoesOfGreatSunderingTalent = 384088;
        public const int Electrified = 64930;
        public const int ElementalMastery = 16166;
        public const int EnergySurge = 40465;
        public const int FlametongueAttack = 10444;
        public const int GhostWolf = 2645;
        public const int HealingRainVisual = 147490;
        public const int HealingRainHeal = 73921;
        public const int IgneousPotential = 279830;
        public const int ItemLightningShield = 23552;
        public const int ItemLightningShieldDamage = 27635;
        public const int ItemManaSurge = 23571;
        public const int LavaBurst = 51505;
        public const int LavaBurstBonusDamage = 71824;
        public const int LavaBurstOverload = 77451;
        public const int LavaSurge = 77762;
        public const int LightningBoltOverloadEnergize = 214816;
        public const int MaelstromController = 343725;
        public const int MasteryElementalOverload = 168534;
        public const int PathOfFlamesSpread = 210621;
        public const int PathOfFlamesTalent = 201909;
        public const int PowerSurge = 40466;
        public const int Riptide = 61295;
        public const int SpiritWolfTalent = 260878;
        public const int SpiritWolfPeriodic = 260882;
        public const int SpiritWolfAura = 260881;
        public const int Stormstrike = 17364;
        public const int T292PElementalDamageBuff = 394651;
        public const int TidalWaves = 53390;
        public const int TotemicPowerArmor = 28827;
        public const int TotemicPowerAttackPower = 28826;
        public const int TotemicPowerMp5 = 28824;
        public const int TotemicPowerSpellPower = 28825;
        public const int WindfuryAttack = 25504;
        public const int WindfuryEnchantment = 334302;
        public const int WindRush = 192082;

        public const int LabelShamanWindfuryTotem = 1038;
    }

    [Script] // 45297 - Chain Lightning Overload
    class spell_sha_chain_lightning_overload : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ChainLightningOverloadEnergize, SpellIds.MaelstromController)
                && ValidateSpellEffect((SpellIds.MaelstromController, 5));
        }

        void HandleScript(int effIndex)
        {
            AuraEffect energizeAmount = GetCaster().GetAuraEffect(SpellIds.MaelstromController, 5);
            if (energizeAmount != null)
            {
                GetCaster().CastSpell(GetCaster(), SpellIds.ChainLightningOverloadEnergize, 
                    new CastSpellExtraArgs(energizeAmount).AddSpellMod(
                        SpellValueMod.BasePoint0, (int)(energizeAmount.GetAmount() * GetUnitTargetCountForEffect(0))));
        }
        }

        public override void Register()
        {
            OnEffectLaunch.Add(new(HandleScript, 0, SpellEffectName.SchoolDamage));
        }
    }

    [Script] // 335902 - Doom Winds
    class spell_sha_doom_winds_legendary : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.DoomWindsLegendaryCooldown);
        }

        bool CheckProc(AuraEffect aurEff, ProcEventInfo procInfo)
        {
            if (GetTarget().HasAura(SpellIds.DoomWindsLegendaryCooldown))
                return false;

            SpellInfo spellInfo = procInfo.GetSpellInfo();
            if (spellInfo == null)
                return false;

            return spellInfo.HasLabel(SpellIds.LabelShamanWindfuryTotem);
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(CheckProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 8042 - Earth Shock
    class spell_sha_earth_shock : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect((SpellIds.T292PElementalDamageBuff, 0));
        }

        void AddScriptedDamageMods()
        {
            AuraEffect t29 = GetCaster().GetAuraEffect(SpellIds.T292PElementalDamageBuff, 0);
            if (t29 != null)
            {
                SetHitDamage(MathFunctions.CalculatePct(GetHitDamage(), 100 + t29.GetAmount()));
                t29.GetBase().Remove();
            }
        }

        public override void Register()
        {
            OnHit.Add(new(AddScriptedDamageMods));
        }
    }

    // 61882 - Earthquake
    [Script] //  8382 - AreaTriggerId
    class areatrigger_sha_earthquake : AreaTriggerAI
    {
        TimeSpan _refreshTimer;
        TimeSpan _period;
        HashSet<ObjectGuid> _stunnedUnits = new();
        float _damageMultiplier;

        public areatrigger_sha_earthquake(AreaTrigger areatrigger) : base(areatrigger)
        {
            _refreshTimer = TimeSpan.FromSeconds(0);
            _period = TimeSpan.FromSeconds(1);
            _damageMultiplier = 1.0f;
        }

        public override void OnCreate(Spell creatingSpell)
        {
            Unit caster = at.GetCaster();
            if (caster != null)
            {
                AuraEffect earthquake = caster.GetAuraEffect(SpellIds.Earthquake, 1);
                if (earthquake != null)
                    _period = TimeSpan.FromMilliseconds(earthquake.GetPeriod());
            }

            if (creatingSpell != null)
            {
                float damageMultiplier = (float)creatingSpell.m_customArg;
                if (damageMultiplier != 0)
                    _damageMultiplier = damageMultiplier;
            }
        }

        public override void OnUpdate(uint diff)
        {
            _refreshTimer -= TimeSpan.FromMilliseconds(diff);
            while (_refreshTimer <= TimeSpan.FromSeconds(0))
            {
                Unit caster = at.GetCaster();
                if (caster != null)
                {
                    caster.CastSpell(at.GetPosition(), SpellIds.EarthquakeTick,
                        new CastSpellExtraArgs(TriggerCastFlags.FullMask)
                        .SetOriginalCaster(at.GetGUID())
                        .AddSpellMod(SpellValueMod.BasePoint0, 
                        (int)(caster.SpellBaseDamageBonusDone(SpellSchoolMask.Nature) * 0.213f * _damageMultiplier)));
                }

                _refreshTimer += _period;
            }
        }

        // Each target can only be stunned once by each earthquake - keep track of who we already stunned
        public bool AddStunnedTarget(ObjectGuid guid)
        {
            return _stunnedUnits.Add(guid);
        }
    }

    [Script] // 61882 - Earthquake
    class spell_sha_earthquake : SpellScript
    {
        (int, int)[] DamageBuffs =
        [
            (SpellIds.EchoesOfGreatSunderingLegendary, 1),
            (SpellIds.EchoesOfGreatSunderingTalent, 0),
            (SpellIds.T292PElementalDamageBuff, 0)
        ];

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect(DamageBuffs);
        }

        void SnapshotDamageMultiplier(int effIndex)
        {
            float damageMultiplier = 1.0f;
            foreach (var (spellId, effect) in DamageBuffs)
            {
                AuraEffect buff = GetCaster().GetAuraEffect(spellId, effect);
                if (buff != null)
                {
                    MathFunctions.AddPct(ref damageMultiplier, buff.GetAmount());
                    buff.GetBase().Remove();
                }
            }

            if (damageMultiplier != 1.0f)
                GetSpell().m_customArg = damageMultiplier;
        }

        public override void Register()
        {
            OnEffectLaunch.Add(new(SnapshotDamageMultiplier, 2, SpellEffectName.CreateAreaTrigger));
        }
    }

    [Script] // 77478 - Earthquake tick
    class spell_sha_earthquake_tick : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.EarthquakeKnockingDown)
            && ValidateSpellEffect((spellInfo.Id, 1));
        }

        void HandleOnHit()
        {
            Unit target = GetHitUnit();
            if (target != null)
            {
                if (RandomHelper.randChance(GetEffectInfo(1).CalcValue()))
                {
                    List<AreaTrigger> areaTriggers = GetCaster().GetAreaTriggers(SpellIds.Earthquake);
                    var areaTrigger = areaTriggers.Find(at => at.GetGUID() == GetSpell().GetOriginalCasterGUID());
                    if (areaTrigger != null)
                    {
                        areatrigger_sha_earthquake eq = areaTrigger.GetAI<areatrigger_sha_earthquake>();
                        if (eq != null)
                            if (eq.AddStunnedTarget(target.GetGUID()))
                                GetCaster().CastSpell(target, SpellIds.EarthquakeKnockingDown, true);
                    }
                }
            }
        }

        public override void Register()
        {
            OnHit.Add(new(HandleOnHit));
        }
    }

    [Script] // 73920 - Healing Rain (Aura)
    class spell_sha_healing_rain_AuraScript : AuraScript
    {
        ObjectGuid _visualDummy;
        Position _dest;

        public void SetVisualDummy(TempSummon summon)
        {
            _visualDummy = summon.GetGUID();
            _dest = summon.GetPosition();
        }

        void HandleEffectPeriodic(AuraEffect aurEff)
        {
            GetTarget().CastSpell(_dest, SpellIds.HealingRainHeal, aurEff);
        }

        void HandleEffecRemoved(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Creature summon = ObjectAccessor.GetCreature(GetTarget(), _visualDummy);
            if (summon != null)
                summon.DespawnOrUnsummon();
        }

        public override void Register()
        {
            OnEffectRemove.Add(new(HandleEffecRemoved, 1, AuraType.PeriodicDummy, AuraEffectHandleModes.Real));
            OnEffectPeriodic.Add(new(HandleEffectPeriodic, 1, AuraType.PeriodicDummy));
        }
    }

    [Script] // 73920 - Healing Rain
    class spell_sha_healing_rain : SpellScript
    {
        const int NpcHealingRainInvisibleStalker = 73400;

        void InitializeVisualStalker()
        {
            Aura aura = GetHitAura();
            if (aura != null)
            {
                WorldLocation dest = GetExplTargetDest();
                if (dest != null)
                {
                    var duration = TimeSpan.FromMilliseconds(GetSpellInfo().CalcDuration(GetOriginalCaster()));
                    TempSummon summon = 
                        GetCaster().GetMap().SummonCreature(
                            NpcHealingRainInvisibleStalker, dest, null, duration, GetOriginalCaster());

                    if (summon == null)
                        return;

                    summon.CastSpell(summon, SpellIds.HealingRainVisual, true);

                    spell_sha_healing_rain_AuraScript script = aura.GetScript<spell_sha_healing_rain_AuraScript>();
                    if (script != null)
                        script.SetVisualDummy(summon);
                }
            }
        }

        public override void Register()
        {
            AfterHit.Add(new(InitializeVisualStalker));
        }
    }

    [Script] // 73921 - Healing Rain
    class spell_sha_healing_rain_target_limit : SpellScript
    {
        void SelectTargets(List<WorldObject> targets)
        {
            SelectRandomInjuredTargets(targets, 6, true);
        }

        public override void Register()
        {
            OnObjectAreaTargetSelect.Add(new(SelectTargets, 0, Targets.UnitDestAreaAlly));
        }
    }

    [Script] // 52042 - Healing Stream Totem
    class spell_sha_healing_stream_totem_heal : SpellScript
    {
        void SelectTargets(List<WorldObject> targets)
        {
            SelectRandomInjuredTargets(targets, 1, true);
        }

        public override void Register()
        {
            OnObjectAreaTargetSelect.Add(new(SelectTargets, 0, Targets.UnitDestAreaAlly));
        }
    }

    [Script] // 23551 - Lightning Shield T2 Bonus
    class spell_sha_item_lightning_shield : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ItemLightningShield);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(eventInfo.GetProcTarget(), SpellIds.ItemLightningShield, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 23552 - Lightning Shield T2 Bonus
    class spell_sha_item_lightning_shield_trigger : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ItemLightningShieldDamage);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(GetTarget(), SpellIds.ItemLightningShieldDamage, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 23572 - Mana Surge
    class spell_sha_item_mana_surge : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ItemManaSurge);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            return eventInfo.GetProcSpell() != null;
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            List<SpellPowerCost> costs = eventInfo.GetProcSpell().GetPowerCost();
            var m = costs.Find(cost => cost.Power == PowerType.Mana);
            if (m != null)
            {
                int mana = MathFunctions.CalculatePct(m.Amount, 35);
                if (mana > 0)
                {
                    CastSpellExtraArgs args = new(aurEff);
                    args.AddSpellMod(SpellValueMod.BasePoint0, mana);
                    GetTarget().CastSpell(GetTarget(), SpellIds.ItemManaSurge, args);
                }
            }
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 40463 - Shaman Tier 6 Trinket
    class spell_sha_item_t6_trinket : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.EnergySurge, SpellIds.PowerSurge);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            SpellInfo spellInfo = eventInfo.GetSpellInfo();
            if (spellInfo == null)
                return;

            int spellId;
            int chance;

            // Lesser Healing Wave
            if (spellInfo.SpellFamilyFlags[0].HasAnyFlag(0x00000080u))
            {
                spellId = SpellIds.EnergySurge;
                chance = 10;
            }
            // Lightning Bolt
            else if (spellInfo.SpellFamilyFlags[0].HasAnyFlag(0x00000001u))
            {
                spellId = SpellIds.EnergySurge;
                chance = 15;
            }
            // Stormstrike
            else if (spellInfo.SpellFamilyFlags[1].HasAnyFlag(0x00000010u))
            {
                spellId = SpellIds.PowerSurge;
                chance = 50;
            }
            else
                return;

            if (RandomHelper.randChance(chance))
                eventInfo.GetActor().CastSpell(null, spellId, true);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 70811 - Item - Shaman T10 Elemental 2P Bonus
    class spell_sha_item_t10_elemental_2p_bonus : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ElementalMastery);
        }

        void HandleEffectProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            Player target = GetTarget().ToPlayer();
            if (target != null)
            {
                target.GetSpellHistory().ModifyCooldown(
                    SpellIds.ElementalMastery, TimeSpan.FromMilliseconds(-aurEff.GetAmount()));
            }
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleEffectProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 51505 - Lava burst
    class spell_sha_lava_burst : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.PathOfFlamesTalent, SpellIds.PathOfFlamesSpread, SpellIds.LavaSurge);
        }

        void HandleScript(int effIndex)
        {
            Unit caster = GetCaster();
            if (caster != null)
                if (caster.HasAura(SpellIds.PathOfFlamesTalent))
                    caster.CastSpell(GetHitUnit(), SpellIds.PathOfFlamesSpread, GetSpell());
        }

        void EnsureLavaSurgeCanBeImmediatelyConsumed()
        {
            Unit caster = GetCaster();

            Aura lavaSurge = caster.GetAura(SpellIds.LavaSurge);
            if (lavaSurge != null)
            {
                if (!GetSpell().m_appliedMods.Contains(lavaSurge))
                {
                    var chargeCategoryId = GetSpellInfo().ChargeCategoryId;

                    // Ensure we have at least 1 usable charge after cast to allow next cast immediately
                    if (!caster.GetSpellHistory().HasCharge(chargeCategoryId))
                        caster.GetSpellHistory().RestoreCharge(chargeCategoryId);
                }
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.TriggerMissile));
            AfterCast.Add(new(EnsureLavaSurgeCanBeImmediatelyConsumed));
        }
    }

    [Script] // 77756 - Lava Surge
    class spell_sha_lava_surge : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.LavaSurge, SpellIds.IgneousPotential);
        }

        bool CheckProcChance(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            int procChance = aurEff.GetAmount();
            AuraEffect igneousPotential = GetTarget().GetAuraEffect(SpellIds.IgneousPotential, 0);
            if (igneousPotential != null)
                procChance += igneousPotential.GetAmount();

            return RandomHelper.randChance(procChance);
        }

        void HandleEffectProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(GetTarget(), SpellIds.LavaSurge, true);
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(CheckProcChance, 0, AuraType.Dummy));
            OnEffectProc.Add(new(HandleEffectProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 77762 - Lava Surge
    class spell_sha_lava_surge_proc : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.LavaBurst);
        }

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void ResetCooldown()
        {
            GetCaster().GetSpellHistory().RestoreCharge(
                SpellMgr.GetSpellInfo(SpellIds.LavaBurst, GetCastDifficulty()).ChargeCategoryId);
        }

        public override void Register()
        {
            AfterHit.Add(new(ResetCooldown));
        }
    }

    [Script] // 45284 - Lightning Bolt Overload
    class spell_sha_lightning_bolt_overload : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.LightningBoltOverloadEnergize, SpellIds.MaelstromController)
            && ValidateSpellEffect((SpellIds.MaelstromController, 1));
        }

        void HandleScript(int effIndex)
        {
            AuraEffect energizeAmount = GetCaster().GetAuraEffect(SpellIds.MaelstromController, 1);
            if (energizeAmount != null)
            {
                GetCaster().CastSpell(GetCaster(), SpellIds.LightningBoltOverloadEnergize, 
                    new CastSpellExtraArgs(energizeAmount)
                    .AddSpellMod(SpellValueMod.BasePoint0, energizeAmount.GetAmount()));
        }
        }

        public override void Register()
        {
            OnEffectLaunch.Add(new(HandleScript, 0, SpellEffectName.SchoolDamage));
        }
    }

    // 45284 - Lightning Bolt Overload
    // 45297 - Chain Lightning Overload
    [Script]
    class spell_sha_mastery_elemental_overload_proc : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.MasteryElementalOverload);
        }

        void ApplyDamageModifier(int effIndex)
        {
            AuraEffect elementalOverload = GetCaster().GetAuraEffect(SpellIds.MasteryElementalOverload, 1);
            if (elementalOverload != null)
                SetHitDamage(MathFunctions.CalculatePct(GetHitDamage(), elementalOverload.GetAmount()));
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(ApplyDamageModifier, 0, SpellEffectName.SchoolDamage));
        }
    }

    [Script] // 30884 - Nature's Guardian
    class spell_sha_natures_guardian : AuraScript
    {
        bool CheckProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            return eventInfo.GetActionTarget().HealthBelowPct(aurEff.GetAmount());
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(CheckProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    // 2645 - Ghost Wolf
    [Script]
    class spell_sha_spirit_wolf : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.GhostWolf, SpellIds.SpiritWolfTalent, 
                SpellIds.SpiritWolfPeriodic, SpellIds.SpiritWolfAura);
        }

        void OnApply(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Unit target = GetTarget();
            if (target.HasAura(SpellIds.SpiritWolfTalent) && target.HasAura(SpellIds.GhostWolf))
                target.CastSpell(target, SpellIds.SpiritWolfPeriodic, aurEff);
        }

        void OnRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(SpellIds.SpiritWolfPeriodic);
            GetTarget().RemoveAurasDueToSpell(SpellIds.SpiritWolfAura);
        }

        public override void Register()
        {
            AfterEffectApply.Add(new(OnApply, 0, AuraType.Any, AuraEffectHandleModes.Real));
            AfterEffectRemove.Add(new(OnRemove, 0, AuraType.Any, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 51564 - Tidal Waves
    class spell_sha_tidal_waves : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.TidalWaves);
        }

        void HandleEffectProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, -aurEff.GetAmount());
            args.AddSpellMod(SpellValueMod.BasePoint1, aurEff.GetAmount());

            GetTarget().CastSpell(GetTarget(), SpellIds.TidalWaves, args);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleEffectProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 28823 - Totemic Power
    class spell_sha_t3_6p_bonus : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.TotemicPowerArmor, SpellIds.TotemicPowerAttackPower, 
                SpellIds.TotemicPowerSpellPower, SpellIds.TotemicPowerMp5);
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
                    spellId = SpellIds.TotemicPowerMp5;
                    break;
                case Class.Mage:
                case Class.Warlock:
                    spellId = SpellIds.TotemicPowerSpellPower;
                    break;
                case Class.Hunter:
                case Class.Rogue:
                    spellId = SpellIds.TotemicPowerAttackPower;
                    break;
                case Class.Warrior:
                    spellId = SpellIds.TotemicPowerArmor;
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

    [Script] // 28820 - Lightning Shield
    class spell_sha_t3_8p_bonus : AuraScript
    {
        void PeriodicTick(AuraEffect aurEff)
        {
            PreventDefaultAction();

            // Need Remove self if Lightning Shield not active
            if (GetTarget().GetAuraEffect(AuraType.ProcTriggerSpell, SpellFamilyNames.Shaman,
                new FlagArray128(0x400), GetCaster().GetGUID()) == null)
            {
                Remove();
        }
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(PeriodicTick, 1, AuraType.PeriodicTriggerSpell));
        }
    }

    [Script] // 64928 - Item - Shaman T8 Elemental 4P Bonus
    class spell_sha_t8_elemental_4p_bonus : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.Electrified);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            DamageInfo damageInfo = eventInfo.GetDamageInfo();
            if (damageInfo == null || damageInfo.GetDamage() == 0)
                return;

            SpellInfo spellInfo = SpellMgr.GetSpellInfo(SpellIds.Electrified, GetCastDifficulty());
            int amount = MathFunctions.CalculatePct(damageInfo.GetDamage(), aurEff.GetAmount());

            Cypher.Assert(spellInfo.GetMaxTicks() > 0);
            amount /= spellInfo.GetMaxTicks();

            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetProcTarget();

            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, amount);
            caster.CastSpell(target, SpellIds.Electrified, args);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 67228 - Item - Shaman T9 Elemental 4P Bonus (Lava Burst)
    class spell_sha_t9_elemental_4p_bonus : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.LavaBurstBonusDamage);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            DamageInfo damageInfo = eventInfo.GetDamageInfo();
            if (damageInfo == null || damageInfo.GetDamage() == 0)
                return;

            SpellInfo spellInfo = SpellMgr.GetSpellInfo(SpellIds.LavaBurstBonusDamage, GetCastDifficulty());
            int amount = MathFunctions.CalculatePct(damageInfo.GetDamage(), aurEff.GetAmount());

            Cypher.Assert(spellInfo.GetMaxTicks() > 0);
            amount /= spellInfo.GetMaxTicks();

            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetProcTarget();

            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, amount);
            caster.CastSpell(target, SpellIds.LavaBurstBonusDamage, args);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 70817 - Item - Shaman T10 Elemental 4P Bonus
    class spell_sha_t10_elemental_4p_bonus : AuraScript
    {
        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetProcTarget();

            // try to find spell Flame Shock on the target
            AuraEffect flameShock = target.GetAuraEffect(
                AuraType.PeriodicDamage, SpellFamilyNames.Shaman, new FlagArray128(0x10000000), caster.GetGUID());
            
            if (flameShock == null)
                return;

            Aura flameShockAura = flameShock.GetBase();

            int maxDuration = flameShockAura.GetMaxDuration();
            int newDuration = flameShockAura.GetDuration() + aurEff.GetAmount() * Time.InMilliseconds;

            flameShockAura.SetDuration(newDuration);
            // is it blizzlike to change max duration for Fs?
            if (newDuration > maxDuration)
                flameShockAura.SetMaxDuration(newDuration);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 70808 - Item - Shaman T10 Restoration 4P Bonus
    class spell_sha_t10_restoration_4p_bonus : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.ChainedHeal);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            HealInfo healInfo = eventInfo.GetHealInfo();
            if (healInfo == null || healInfo.GetHeal() == 0)
                return;

            SpellInfo spellInfo = SpellMgr.GetSpellInfo(SpellIds.ChainedHeal, GetCastDifficulty());
            int amount = MathFunctions.CalculatePct(healInfo.GetHeal(), aurEff.GetAmount());

            Cypher.Assert(spellInfo.GetMaxTicks() > 0);
            amount /= spellInfo.GetMaxTicks();

            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetProcTarget();

            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, amount);
            caster.CastSpell(target, SpellIds.ChainedHeal, args);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 33757 - Windfury Weapon
    class spell_sha_windfury_weapon : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.WindfuryEnchantment);
        }

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleEffect(int effIndex)
        {
            PreventHitDefaultEffect(effIndex);

            Item mainHand = GetCaster().ToPlayer().GetWeaponForAttack(WeaponAttackType.BaseAttack, false);
            if (mainHand != null)
                GetCaster().CastSpell(mainHand, SpellIds.WindfuryEnchantment, GetSpell());
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleEffect, 0, SpellEffectName.Dummy));
        }
    }

    //  12676 - AreaTriggerId
    [Script]
    class areatrigger_sha_wind_rush_totem : AreaTriggerAI
    {
        uint RefreshTime = 4500;

        uint _refreshTimer;

        public areatrigger_sha_wind_rush_totem(AreaTrigger areatrigger) : base(areatrigger)
        {
            _refreshTimer = RefreshTime;
        }

        public override void OnUpdate(uint diff)
        {
            _refreshTimer -= diff;
            if (_refreshTimer <= 0)
            {
                Unit caster = at.GetCaster();
                if (caster != null)
                {
                    foreach (ObjectGuid guid in at.GetInsideUnits())
                    {
                        Unit unit = ObjAccessor.GetUnit(caster, guid);
                        if (unit != null)
                        {
                            if (!caster.IsFriendlyTo(unit))
                                continue;

                            caster.CastSpell(unit, SpellIds.WindRush, true);
                        }
                    }
                }
                _refreshTimer += RefreshTime;
            }
        }

        public override void OnUnitEnter(Unit unit)
        {
            Unit caster = at.GetCaster();
            if (caster != null)
            {
                if (!caster.IsFriendlyTo(unit))
                    return;

                caster.CastSpell(unit, SpellIds.WindRush, true);
            }
        }
    }
}