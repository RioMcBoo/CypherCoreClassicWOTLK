﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.AI;
using Game.BattleGrounds;
using Game.DataStorage;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Entities
{
    public abstract partial class Unit
    {
        public virtual bool HasSpell(int spellId) { return false; }

        public void SetInstantCast(bool set) { _instantCast = set; }
        public bool CanInstantCast() { return _instantCast; }

        public int SpellBaseDamageBonusDone(SpellSchoolMask schoolMask)
        {
            float DoneAdvertisedBenefit = GetTotalAuraModifierByMiscMask(AuraType.ModDamageDone, (uint)schoolMask);

            if (IsTypeId(TypeId.Player))
            {
                // Base value
                DoneAdvertisedBenefit += ToPlayer().GetBaseSpellPowerBonus();

                // Damage bonus from stats
                foreach (var eff in GetAuraEffectsByType(AuraType.ModSpellDamageOfStatPercent))
                {
                    SpellSchoolMask spellSchoolMask = (SpellSchoolMask)eff.GetMiscValue();
                    if (spellSchoolMask.HasAnyFlag(schoolMask))
                    {
                        // stat used stored in miscValueB for this aura
                        Stats usedStat = (Stats)eff.GetMiscValueB();
                        DoneAdvertisedBenefit += MathFunctions.CalculatePct(GetStat(usedStat), eff.GetAmount());
                    }
                }
                
                // ... and attack power
                DoneAdvertisedBenefit += MathFunctions.CalculatePct(GetTotalAttackPowerValue(WeaponAttackType.BaseAttack), GetTotalAuraModifierByMiscMask(AuraType.ModSpellDamageOfAttackPower, (uint)schoolMask));
            }
            return (int)DoneAdvertisedBenefit;
        }

        public int SpellDamageBonusDone(Unit victim, SpellInfo spellProto, int pdamage, DamageEffectType damagetype, SpellEffectInfo spellEffectInfo, int stack = 1, Spell spell = null, AuraEffect aurEff = null)
        {
            if (spellProto == null || victim == null)
                return pdamage;

            int DoneTotal = 0;
            float DoneTotalMod = 1.0f;

            void callDamageScript(ref int dmg, ref int flatMod, ref float pctMod)
            {
                if (spell != null)
                    spell.CallScriptCalcDamageHandlers(victim, ref dmg, ref flatMod, ref pctMod);
                else if (aurEff != null)
                    aurEff.GetBase().CallScriptCalcDamageAndHealingHandlers(aurEff, aurEff.GetBase().GetApplicationOfTarget(victim.GetGUID()), victim, ref dmg, ref flatMod, ref pctMod);
            }

            // Some spells don't benefit from done mods
            if (damagetype == DamageEffectType.Direct || spellProto.HasAttribute(SpellAttr3.IgnoreCasterModifiers))
            {
                callDamageScript(ref pdamage, ref DoneTotal, ref DoneTotalMod);
                return (int)Math.Max((pdamage + DoneTotal) * DoneTotalMod, 0.0f);
            }

            // For totems get damage bonus from owner
            if (IsTypeId(TypeId.Unit) && IsTotem())
            {
                Unit owner = GetOwner();
                if (owner != null)
                    return owner.SpellDamageBonusDone(victim, spellProto, pdamage, damagetype, spellEffectInfo, stack, spell, aurEff);
            }

            DoneTotalMod = SpellDamagePctDone(victim, spellProto, damagetype, spellEffectInfo);

            // Done fixed damage bonus auras
            int DoneAdvertisedBenefit = SpellBaseDamageBonusDone(spellProto.GetSchoolMask());
            // modify spell power by victim's SPELL_AURA_MOD_DAMAGE_TAKEN auras (eg Amplify/Dampen Magic)
            DoneAdvertisedBenefit += victim.GetTotalAuraModifierByMiscMask(AuraType.ModDamageTaken, (uint)spellProto.GetSchoolMask());

            // Pets just add their bonus damage to their spell damage
            // note that their spell damage is just gain of their own auras
            if (HasUnitTypeMask(UnitTypeMask.Guardian))
                DoneAdvertisedBenefit += ((Guardian)this).GetBonusDamage();

            // Check for table values
            if (spellEffectInfo.BonusCoefficientFromAP > 0.0f)
            {
                float ApCoeffMod = spellEffectInfo.BonusCoefficientFromAP;
                Player modOwner = GetSpellModOwner();
                if (modOwner != null)
                {
                    ApCoeffMod *= 100.0f;
                    modOwner.ApplySpellMod(spellProto, SpellModOp.BonusCoefficient, ref ApCoeffMod);
                    ApCoeffMod /= 100.0f;
                }

                WeaponAttackType attType = WeaponAttackType.BaseAttack;
                if ((spellProto.IsRangedWeaponSpell() && spellProto.DmgClass != SpellDmgClass.Melee))
                    attType = WeaponAttackType.RangedAttack;

                if (spellProto.HasAttribute(SpellAttr3.RequiresOffHandWeapon)
                    && !spellProto.HasAttribute(SpellAttr3.RequiresMainHandWeapon))
                {
                    attType = WeaponAttackType.OffAttack;
                }

                float APbonus = victim.GetTotalAuraModifier(
                    attType != WeaponAttackType.RangedAttack 
                    ? AuraType.MeleeAttackPowerAttackerBonus 
                    : AuraType.RangedAttackPowerAttackerBonus);

                APbonus += GetTotalAttackPowerValue(attType);
                DoneTotal += (int)(stack * ApCoeffMod * APbonus);
            }

            // Default calculation
            if (DoneAdvertisedBenefit != 0)
            {
                float coeff = spellEffectInfo.BonusCoefficient;
                Player modOwner1 = GetSpellModOwner();
                if (modOwner1 != null)
                {
                    coeff *= 100.0f;
                    modOwner1.ApplySpellMod(spellProto, SpellModOp.BonusCoefficient, ref coeff);
                    coeff /= 100.0f;
                }
                DoneTotal += (int)(DoneAdvertisedBenefit * coeff * stack);
            }

            callDamageScript(ref pdamage, ref DoneTotal, ref DoneTotalMod);

            float tmpDamage = (pdamage + DoneTotal) * DoneTotalMod;

            // apply spellmod to Done damage (flat and pct)
            Player _modOwner = GetSpellModOwner();
            if (_modOwner != null)
            {
                _modOwner.ApplySpellMod(spellProto,
                    damagetype == DamageEffectType.DOT
                    ? SpellModOp.PeriodicHealingAndDamage
                    : SpellModOp.HealingAndDamage,
                ref tmpDamage);
            }

            return (int)Math.Max(tmpDamage, 0.0f);
        }

        public float SpellDamagePctDone(Unit victim, SpellInfo spellProto, DamageEffectType damagetype, SpellEffectInfo spellEffectInfo)
        {
            if (spellProto == null || victim == null || damagetype == DamageEffectType.Direct)
                return 1.0f;

            // Some spells don't benefit from done mods
            if (spellProto.HasAttribute(SpellAttr3.IgnoreCasterModifiers))
                return 1.0f;

            // Some spells don't benefit from pct done mods
            if (spellProto.HasAttribute(SpellAttr6.IgnoreCasterDamageModifiers))
                return 1.0f;

            // For totems get damage bonus from owner
            if (IsCreature() && IsTotem())
            {
                Unit owner = GetOwner();
                if (owner != null)
                    return owner.SpellDamagePctDone(victim, spellProto, damagetype, spellEffectInfo);
            }

            // Done total percent damage auras
            float DoneTotalMod = 1.0f;

            // Pet damage?
            if (IsTypeId(TypeId.Unit) && !IsPet())
                DoneTotalMod *= ToCreature().GetSpellDamageMod(ToCreature().GetCreatureTemplate().Classification);

            float maxModDamagePercentSchool = 1.0f;
            SpellSchoolMask spellSchoolMask = spellProto.GetSchoolMask();

            if (this is Player player)
            {
                for (SpellSchools school = SpellSchools.Normal; school < SpellSchools.Max; school++)
                {
                    if (spellSchoolMask.HasSchool(school))
                        maxModDamagePercentSchool = Math.Max(maxModDamagePercentSchool, player.m_activePlayerData.ModDamageDonePercent[(int)school]);
                }
            }
            else
            {                
                for (SpellSchools school = SpellSchools.Normal; school < SpellSchools.Max; school++)
                {
                    if (spellSchoolMask.HasSchool(school))
                        maxModDamagePercentSchool *= StatMods.GetOrDefault(UnitMods.SpellDamageStart + (int)school).Mult;
                }
            }

            DoneTotalMod *= maxModDamagePercentSchool;

            uint creatureTypeMask = victim.GetCreatureTypeMask();

            DoneTotalMod *= GetTotalAuraMultiplierByMiscMask(AuraType.ModDamageDoneVersus, creatureTypeMask);

            // bonus against aurastate
            DoneTotalMod *= GetTotalAuraMultiplier(AuraType.ModDamageDoneVersusAurastate, aurEff =>
            {
                if (victim.HasAuraState((AuraStateType)aurEff.GetMiscValue()))
                    return true;
                return false;
            });

            // bonus against target aura mechanic
            DoneTotalMod *= GetTotalAuraMultiplier(AuraType.ModDamagePercentDoneByTargetAuraMechanic, aurEff =>
            {
                if (victim.HasAuraWithMechanic(1ul << aurEff.GetMiscValue()))
                    return true;
                return false;
            });

            // Add SPELL_AURA_MOD_DAMAGE_DONE_FOR_MECHANIC percent bonus
            if (spellEffectInfo.Mechanic != 0)
                MathFunctions.AddPct(ref DoneTotalMod, GetTotalAuraModifierByMiscValue(AuraType.ModDamageDoneForMechanic, (int)spellEffectInfo.Mechanic));
            else if (spellProto.Mechanic != 0)
                MathFunctions.AddPct(ref DoneTotalMod, GetTotalAuraModifierByMiscValue(AuraType.ModDamageDoneForMechanic, (int)spellProto.Mechanic));

            // Custom scripted damage
            switch (spellProto.SpellFamilyName)
            {
                case SpellFamilyNames.Mage:
                    // Ice Lance (no unique family flag)
                    if (spellProto.Id == 228598)
                        if (victim.HasAuraState(AuraStateType.Frozen, spellProto, this))
                            DoneTotalMod *= 3.0f;

                    break;
                case SpellFamilyNames.Warlock:
                    // Shadow Bite (30% increase from each dot)
                    if (spellProto.SpellFamilyFlags[1].HasAnyFlag<uint>(0x00400000) && IsPet())
                    {
                        uint count = victim.GetDoTsByCaster(GetOwnerGUID());
                        if (count != 0)
                            MathFunctions.AddPct(ref DoneTotalMod, 30 * count);
                    }

                    // Drain Soul - increased damage for targets under 20% HP
                    if (spellProto.Id == 198590)
                        if (HasAuraState(AuraStateType.Wounded20Percent))
                            DoneTotalMod *= 2;
                    break;
            }

            return DoneTotalMod;
        }

        public int SpellDamageBonusTaken(Unit caster, SpellInfo spellProto, int pdamage, DamageEffectType damagetype)
        {
            if (spellProto == null || damagetype == DamageEffectType.Direct)
                return pdamage;

            float TakenTotalMod = 1.0f;

            // Mod damage from spell mechanic
            ulong mechanicMask = spellProto.GetAllEffectsMechanicMask();
            if (mechanicMask != 0)
            {
                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModMechanicDamageTakenPercent, aurEff =>
                {
                    if ((mechanicMask & (1ul << aurEff.GetMiscValue())) != 0)
                        return true;
                    return false;
                });
            }

            AuraEffect cheatDeath = GetAuraEffect(45182, 0);
            if (cheatDeath != null)
                if (cheatDeath.GetMiscValue().HasAnyFlag((int)SpellSchoolMask.Normal))
                    MathFunctions.AddPct(ref TakenTotalMod, cheatDeath.GetAmount());

            // Spells with SPELL_ATTR4_IGNORE_DAMAGE_TAKEN_MODIFIERS should only benefit from mechanic damage mod auras.
            if (!spellProto.HasAttribute(SpellAttr4.IgnoreDamageTakenModifiers))
            {
                // from positive and negative SPELL_AURA_MOD_DAMAGE_PERCENT_TAKEN
                // multiplicative bonus, for example Dispersion + Shadowform (0.10*0.85=0.085)
                TakenTotalMod *= GetTotalAuraMultiplierByMiscMask(AuraType.ModDamagePercentTaken, (uint)spellProto.GetSchoolMask());

                // From caster spells
                if (caster != null)
                {
                    TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModSchoolMaskDamageFromCaster, aurEff =>
                    {
                        return aurEff.GetCasterGUID() == caster.GetGUID() && (aurEff.GetMiscValue() & (int)spellProto.GetSchoolMask()) != 0;
                    });

                    TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModSpellDamageFromCaster, aurEff =>
                    {
                        return aurEff.GetCasterGUID() == caster.GetGUID() && aurEff.IsAffectingSpell(spellProto);
                    });

                    TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModDamageTakenFromCasterByLabel, aurEff =>
                    {
                        return aurEff.GetCasterGUID() == caster.GetGUID() && spellProto.HasLabel(aurEff.GetMiscValue());
                    });
                }

                if (damagetype == DamageEffectType.DOT)
                {
                    TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModPeriodicDamageTaken,
                        aurEff =>
                        (aurEff.GetMiscValue() & (uint)spellProto.GetSchoolMask()) != 0);
                }
            }

            // Sanctified Wrath (bypass damage reduction)
            if (caster != null && TakenTotalMod < 1.0f)
            {
                float damageReduction = 1.0f - TakenTotalMod;

                foreach (AuraEffect aurEff in caster.GetAuraEffectsByType(AuraType.ModIgnoreTargetResist))
                {
                    if ((aurEff.GetMiscValue() & (int)spellProto.GetSchoolMask()) == 0)
                        continue;

                    MathFunctions.AddPct(ref damageReduction, -aurEff.GetAmount());
                }

                TakenTotalMod = 1.0f - damageReduction;
            }

            float tmpDamage = pdamage * TakenTotalMod;
            return (int)Math.Max(tmpDamage, 0.0f);
        }

        public int SpellBaseHealingBonusDone(SpellSchoolMask schoolMask)
        {
            float advertisedBenefit = GetTotalAuraModifier(AuraType.ModHealingDone, aurEff =>
            {
                if (aurEff.GetMiscValue() == 0 || (aurEff.GetMiscValue() & (int)schoolMask) != 0)
                    return true;
                return false;
            });

            // Healing bonus of spirit, intellect and strength
            if (IsTypeId(TypeId.Player))
            {
                // Base value
                advertisedBenefit += ToPlayer().GetBaseSpellPowerBonus();

                // Healing bonus from stats
                foreach (var i in GetAuraEffectsByType(AuraType.ModSpellHealingOfStatPercent))
                {
                    // stat used dependent from misc value (stat index)
                    Stats usedStat = (Stats)(i.GetSpellEffectInfo().MiscValue);
                    advertisedBenefit += (int)MathFunctions.CalculatePct(GetStat(usedStat), i.GetAmount());
                }

                // ... and attack power SPELL_AURA_MOD_SPELL_HEALING_OF_ATTACK_POWER
                foreach (var effect in GetAuraEffectsByType(AuraType.ModSpellHealingOfAttackPower))
                {
                    SpellSchoolMask spellSchoolMask = (SpellSchoolMask)effect.GetMiscValue();
                    if (spellSchoolMask.HasAnyFlag(schoolMask))
                        advertisedBenefit += MathFunctions.CalculatePct(GetTotalAttackPowerValue(WeaponAttackType.BaseAttack), effect.GetAmount());
                }
            }

            return (int)advertisedBenefit;
        }

        public static int SpellCriticalHealingBonus(Unit caster, SpellInfo spellProto, int damage, Unit victim)
        {
            // Calculate critical bonus
            int crit_bonus = damage;

            // adds additional damage to critBonus (from talents)
            if (caster != null)
            {
                Player modOwner = caster.GetSpellModOwner();
                if (modOwner != null)
                    modOwner.ApplySpellMod(spellProto, SpellModOp.CritDamageAndHealing, ref crit_bonus);
            }

            damage += crit_bonus;

            if (caster != null)
                damage = (int)(damage * caster.GetTotalAuraMultiplier(AuraType.ModCriticalHealingAmount));

            return damage;
        }

        public int SpellHealingBonusDone(Unit victim, SpellInfo spellProto, int healamount, DamageEffectType damagetype, SpellEffectInfo spellEffectInfo, int stack = 1, Spell spell = null, AuraEffect aurEff = null)
        {
            // For totems get healing bonus from owner (statue isn't totem in fact)
            if (IsTypeId(TypeId.Unit) && IsTotem())
            {
                Unit owner = GetOwner();
                if (owner != null)
                    return owner.SpellHealingBonusDone(victim, spellProto, healamount, damagetype, spellEffectInfo, stack, spell, aurEff);
            }

            // No bonus healing for potion spells
            if (spellProto.SpellFamilyName == SpellFamilyNames.Potion)
                return healamount;

            int DoneTotal = 0;
            float DoneTotalMod = SpellHealingPctDone(victim, spellProto);

            // done scripted mod (take it from owner)
            Unit owner1 = GetOwner() ?? this;

            foreach (var effect in owner1.GetAuraEffectsByType(AuraType.OverrideClassScripts))
            {
                if (!effect.IsAffectingSpell(spellProto))
                    continue;

                switch (effect.GetMiscValue())
                {
                    case 3736: // Hateful Totem of the Third Wind / Increased Lesser Healing Wave / LK Arena (4/5/6) Totem of the Third Wind / Savage Totem of the Third Wind
                        DoneTotal += effect.GetAmount();
                        break;
                    default:
                        break;
                }
            }

            // Done fixed damage bonus auras
            var DoneAdvertisedBenefit = SpellBaseHealingBonusDone(spellProto.GetSchoolMask());
            // modify spell power by victim's SPELL_AURA_MOD_HEALING auras (eg Amplify/Dampen Magic)
            DoneAdvertisedBenefit += victim.GetTotalAuraModifierByMiscMask(AuraType.ModHealing, (uint)spellProto.GetSchoolMask());

            // Pets just add their bonus damage to their spell damage
            // note that their spell damage is just gain of their own auras
            if (HasUnitTypeMask(UnitTypeMask.Guardian))
                DoneAdvertisedBenefit += ((Guardian)this).GetBonusDamage();

            // Check for table values
            if (spellEffectInfo.BonusCoefficientFromAP > 0.0f)
            {
                WeaponAttackType attType = (spellProto.IsRangedWeaponSpell() && spellProto.DmgClass != SpellDmgClass.Melee) ? WeaponAttackType.RangedAttack : WeaponAttackType.BaseAttack;
                float APbonus = victim.GetTotalAuraModifier(attType == WeaponAttackType.BaseAttack ? AuraType.MeleeAttackPowerAttackerBonus : AuraType.RangedAttackPowerAttackerBonus);
                APbonus += GetTotalAttackPowerValue(attType);

                DoneTotal += (int)(spellEffectInfo.BonusCoefficientFromAP * stack * APbonus);
            }

            // Default calculation
            if (DoneAdvertisedBenefit != 0)
            {
                float coeff = spellEffectInfo.BonusCoefficient;
                Player modOwner = GetSpellModOwner();
                if (modOwner != null)
                {
                    coeff *= 100.0f;
                    modOwner.ApplySpellMod(spellProto, SpellModOp.BonusCoefficient, ref coeff);
                    coeff /= 100.0f;
                }

                DoneTotal += (int)(DoneAdvertisedBenefit * coeff * stack);
            }

            foreach (var otherSpellEffectInfo in spellProto.GetEffects())
            {
                switch (otherSpellEffectInfo.ApplyAuraName)
                {
                    // Bonus healing does not apply to these spells
                    case AuraType.PeriodicLeech:
                    case AuraType.PeriodicHealthFunnel:
                        DoneTotal = 0;
                        break;
                }

                if (otherSpellEffectInfo.IsEffect(SpellEffectName.HealthLeech))
                    DoneTotal = 0;
            }

            if (spell != null)
                spell.CallScriptCalcHealingHandlers(victim, ref healamount, ref DoneTotal, ref DoneTotalMod);
            else if (aurEff != null)
                aurEff.GetBase().CallScriptCalcDamageAndHealingHandlers(aurEff, aurEff.GetBase().GetApplicationOfTarget(victim.GetGUID()), victim, ref healamount, ref DoneTotal, ref DoneTotalMod);

            float heal = (healamount + DoneTotal) * DoneTotalMod;

            // apply spellmod to Done amount
            Player _modOwner = GetSpellModOwner();
            if (_modOwner != null)
            {
                _modOwner.ApplySpellMod(spellProto,
                    damagetype == DamageEffectType.DOT
                    ? SpellModOp.PeriodicHealingAndDamage
                    : SpellModOp.HealingAndDamage,
                ref heal);
            }

            return (int)Math.Max(heal, 0.0f);
        }

        public float SpellHealingPctDone(Unit victim, SpellInfo spellProto)
        {
            // For totems get healing bonus from owner
            if (IsCreature() && IsTotem())
            {
                Unit owner = GetOwner();
                if (owner != null)
                    return owner.SpellHealingPctDone(victim, spellProto);
            }

            // Some spells don't benefit from done mods
            if (spellProto.HasAttribute(SpellAttr3.IgnoreCasterModifiers))
                return 1.0f;

            // Some spells don't benefit from done mods
            if (spellProto.HasAttribute(SpellAttr6.IgnoreHealingModifiers))
                return 1.0f;

            // No bonus healing for potion spells
            if (spellProto.SpellFamilyName == SpellFamilyNames.Potion)
                return 1.0f;

            float DoneTotalMod = 1.0f;

            // Healing done percent
            if (ToPlayer() is Player thisPlayer)
                DoneTotalMod *= thisPlayer.m_activePlayerData.ModHealingDonePercent;
            else // SPELL_AURA_MOD_HEALING_DONE_PERCENT is included in m_activePlayerData->ModHealingDonePercent for players
                DoneTotalMod *= GetTotalAuraMultiplier(AuraType.ModHealingDonePercent);

            // bonus against aurastate
            DoneTotalMod *= GetTotalAuraMultiplier(AuraType.ModDamageDoneVersusAurastate, aurEff =>
            {
                return victim.HasAuraState((AuraStateType)aurEff.GetMiscValue());
            });

            // bonus from missing health of target
            float healthPctDiff = 100.0f - victim.GetHealthPct();
            foreach (var healingDonePctVsTargetHealth in GetAuraEffectsByType(AuraType.ModHealingDonePctVersusTargetHealth))
            {
                if (healingDonePctVsTargetHealth.IsAffectingSpell(spellProto))
                    MathFunctions.AddPct(ref DoneTotalMod, MathFunctions.CalculatePct((float)healingDonePctVsTargetHealth.GetAmount(), healthPctDiff));
            }

            return DoneTotalMod;
        }

        public int SpellHealingBonusTaken(Unit caster, SpellInfo spellProto, int healamount, DamageEffectType damagetype)
        {
            float TakenTotalMod = 1.0f;

            // Healing taken percent
            float minval = GetMaxNegativeAuraModifier(AuraType.ModHealingPct);
            if (minval != 0)
                MathFunctions.AddPct(ref TakenTotalMod, minval);

            float maxval = GetMaxPositiveAuraModifier(AuraType.ModHealingPct);
            if (maxval != 0)
                MathFunctions.AddPct(ref TakenTotalMod, maxval);

            // Nourish cast
            if (spellProto.SpellFamilyName == SpellFamilyNames.Druid && spellProto.SpellFamilyFlags[1].HasAnyFlag(0x2000000u))
            {
                // Rejuvenation, Regrowth, Lifebloom, or Wild Growth
                if (GetAuraEffect(AuraType.PeriodicHeal, SpellFamilyNames.Druid, new FlagArray128(0x50, 0x4000010, 0)) != null)
                    // increase healing by 20%
                    TakenTotalMod *= 1.2f;
            }

            if (damagetype == DamageEffectType.DOT)
            {
                // Healing over time taken percent
                float minval_hot = GetMaxNegativeAuraModifier(AuraType.ModHotPct);
                if (minval_hot != 0)
                    MathFunctions.AddPct(ref TakenTotalMod, minval_hot);

                float maxval_hot = GetMaxPositiveAuraModifier(AuraType.ModHotPct);
                if (maxval_hot != 0)
                    MathFunctions.AddPct(ref TakenTotalMod, maxval_hot);
            }

            if (caster != null)
            {
                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModHealingReceived, aurEff =>
                {
                    if (caster.GetGUID() == aurEff.GetCasterGUID() && aurEff.IsAffectingSpell(spellProto))
                        return true;
                    return false;
                });

                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModHealingTakenFromCaster, aurEff =>
                {
                    return aurEff.GetCasterGUID() == caster.GetGUID();
                });
            }

            float heal = healamount * TakenTotalMod;
            return (int)Math.Max(heal, 0.0f);
        }

        public float SpellCritChanceDone(Spell spell, AuraEffect aurEff, SpellSchoolMask schoolMask, WeaponAttackType attackType = WeaponAttackType.BaseAttack)
        {
            SpellInfo spellInfo = spell != null ? spell.GetSpellInfo() : aurEff.GetSpellInfo();
            //! Mobs can't crit with spells. (Except player controlled)
            if (IsCreature() && GetSpellModOwner() == null)
                return 0.0f;

            // not critting spell
            if (spell != null && !spellInfo.HasAttribute(SpellCustomAttributes.CanCrit))
                return 0.0f;

            float crit_chance = 0.0f;
            switch (spellInfo.DmgClass)
            {
                case SpellDmgClass.Magic:
                {
                    if (schoolMask.HasAnyFlag(SpellSchoolMask.Normal))
                        crit_chance = 0.0f;
                    // For other schools
                    else if (IsTypeId(TypeId.Player))
                        crit_chance = ToPlayer().m_activePlayerData.SpellCritPercentage[(int)schoolMask.GetFirstSchool()];
                    else
                        crit_chance = BaseSpellCritChance;
                    break;
                }
                case SpellDmgClass.Melee:
                case SpellDmgClass.Ranged:
                    crit_chance += GetUnitCriticalChanceDone(attackType);
                    break;

                case SpellDmgClass.None:
                default:
                    return 0f;
            }
            // percent done
            // only players use intelligence for critical Chance computations
            Player modOwner = GetSpellModOwner();
            if (modOwner != null)
                modOwner.ApplySpellMod(spellInfo, SpellModOp.CritChance, ref crit_chance);

            return Math.Max(crit_chance, 0.0f);
        }

        public float SpellCritChanceTaken(Unit caster, Spell spell, AuraEffect aurEff, SpellSchoolMask schoolMask, float doneChance, WeaponAttackType attackType = WeaponAttackType.BaseAttack)
        {
            SpellInfo spellInfo = spell != null ? spell.GetSpellInfo() : aurEff.GetSpellInfo();
            // not critting spell
            if (spell != null && !spellInfo.HasAttribute(SpellCustomAttributes.CanCrit))
                return 0.0f;

            float crit_chance = doneChance;
            switch (spellInfo.DmgClass)
            {
                case SpellDmgClass.Magic:
                {
                    // taken
                    if (!spellInfo.IsPositive())
                    {
                        // Modify critical Chance by victim SPELL_AURA_MOD_ATTACKER_SPELL_AND_WEAPON_CRIT_CHANCE
                        crit_chance += GetTotalAuraModifier(AuraType.ModAttackerSpellAndWeaponCritChance);
                    }

                    if (caster != null)
                    {
                        // scripted (increase crit Chance ... against ... target by x%
                        foreach (var eff in caster.GetAuraEffectsByType(AuraType.OverrideClassScripts))
                        {
                            if (!eff.IsAffectingSpell(spellInfo))
                                continue;

                            switch (eff.GetMiscValue())
                            {
                                case 911: // Shatter
                                    if (HasAuraState(AuraStateType.Frozen, spellInfo, this))
                                    {
                                        crit_chance *= 1.5f;
                                        AuraEffect _eff = eff.GetBase().GetEffect(1);
                                        if (_eff != null)
                                            crit_chance += _eff.GetAmount();
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        // Custom crit by class
                        switch (spellInfo.SpellFamilyName)
                        {
                            case SpellFamilyNames.Rogue:
                                // Shiv-applied poisons can't crit
                                if (caster.FindCurrentSpellBySpellId(5938) != null)
                                    crit_chance = 0.0f;
                                break;
                        }

                        // Spell crit suppression
                        if (IsCreature())
                        {
                            int levelDiff = GetLevelForTarget(this) - caster.GetLevel();
                            crit_chance -= levelDiff * 1.0f;
                        }
                    }
                    break;
                }
                case SpellDmgClass.Melee:
                case SpellDmgClass.Ranged:
                {
                    if (caster != null)
                        crit_chance += GetUnitCriticalChanceTaken(caster, attackType, crit_chance);
                    break;
                }
                case SpellDmgClass.None:
                default:
                    return 0f;
            }

            // for this types the bonus was already added in GetUnitCriticalChance, do not add twice
            if (caster != null && spellInfo.DmgClass != SpellDmgClass.Melee && spellInfo.DmgClass != SpellDmgClass.Ranged)
            {
                crit_chance += GetTotalAuraModifier(AuraType.ModCritChanceForCasterWithAbilities, aurEff => aurEff.GetCasterGUID() == caster.GetGUID() && aurEff.IsAffectingSpell(spellInfo));

                crit_chance += GetTotalAuraModifier(AuraType.ModCritChanceForCaster, aurEff => aurEff.GetCasterGUID() == caster.GetGUID());

                crit_chance += caster.GetTotalAuraModifier(AuraType.ModCritChanceVersusTargetHealth, aurEff => !HealthBelowPct(aurEff.GetMiscValueB()));

                TempSummon tempSummon = caster.ToTempSummon();
                if (tempSummon != null)
                    crit_chance += GetTotalAuraModifier(AuraType.ModCritChanceForCasterPet, aurEff => aurEff.GetCasterGUID() == tempSummon.GetSummonerGUID());
            }

            // call script handlers
            if (spell != null)
                spell.CallScriptCalcCritChanceHandlers(this, ref crit_chance);
            else
                aurEff.GetBase().CallScriptEffectCalcCritChanceHandlers(aurEff, aurEff.GetBase().GetApplicationOfTarget(GetGUID()), this, ref crit_chance);

            return Math.Max(crit_chance, 0.0f);
        }

        // Melee based spells hit result calculations
        public override SpellMissInfo MeleeSpellHitResult(Unit victim, SpellInfo spellInfo)
        {
            if (spellInfo.HasAttribute(SpellAttr3.NoAvoidance))
                return SpellMissInfo.None;

            WeaponAttackType attType = WeaponAttackType.BaseAttack;

            // Check damage class instead of attack Type to correctly handle judgements
            // - they are meele, but can't be dodged/parried/deflected because of ranged dmg class
            if (spellInfo.DmgClass == SpellDmgClass.Ranged)
                attType = WeaponAttackType.RangedAttack;

            int roll = RandomHelper.IRand(0, 9999);

            int missChance = (int)(MeleeSpellMissChance(victim, attType, spellInfo) * 100.0f);
            // Roll miss
            int tmp = missChance;
            if (roll < tmp)
                return SpellMissInfo.Miss;

            // Chance resist mechanic
            int resist_chance = victim.GetMechanicResistChance(spellInfo) * 100;
            tmp += resist_chance;
            if (roll < tmp)
                return SpellMissInfo.Resist;

            // Same spells cannot be parried/dodged
            if (spellInfo.HasAttribute(SpellAttr0.NoActiveDefense))
                return SpellMissInfo.None;

            bool canDodge = !spellInfo.HasAttribute(SpellAttr7.NoAttackDodge);
            bool canParry = !spellInfo.HasAttribute(SpellAttr7.NoAttackParry);
            bool canBlock = !spellInfo.HasAttribute(SpellAttr8.NoAttackBlock);

            // if victim is casting or cc'd it can't avoid attacks
            if (victim.IsNonMeleeSpellCast(false, false, true) || victim.HasUnitState(UnitState.Controlled))
            {
                canDodge = false;
                canParry = false;
                canBlock = false;
            }

            // Ranged attacks can only miss, resist and deflect and get blocked
            if (attType == WeaponAttackType.RangedAttack)
            {
                canParry = false;
                canDodge = false;

                // only if in front
                if (!victim.HasUnitState(UnitState.Controlled) && (victim.HasInArc(MathFunctions.PI, this) || victim.HasAuraType(AuraType.IgnoreHitDirection)))
                {
                    int deflect_chance = victim.GetTotalAuraModifier(AuraType.DeflectSpells) * 100;
                    tmp += deflect_chance;
                    if (roll < tmp)
                        return SpellMissInfo.Deflect;
                }
            }

            // Check for attack from behind
            if (!victim.HasInArc(MathFunctions.PI, this))
            {
                if (!victim.HasAuraType(AuraType.IgnoreHitDirection))
                {
                    // Can`t dodge from behind in PvP (but its possible in PvE)
                    if (victim.IsTypeId(TypeId.Player))
                        canDodge = false;
                    // Can`t parry or block
                    canParry = false;
                    canBlock = false;
                }
                else // Only deterrence as of 3.3.5
                {
                    if (spellInfo.HasAttribute(SpellCustomAttributes.ReqCasterBehindTarget))
                        canParry = false;
                }
            }

            // Ignore combat result aura
            foreach (var aurEff in GetAuraEffectsByType(AuraType.IgnoreCombatResult))
            {
                if (!aurEff.IsAffectingSpell(spellInfo))
                    continue;

                switch ((MeleeHitOutcome)aurEff.GetMiscValue())
                {
                    case MeleeHitOutcome.Dodge:
                        canDodge = false;
                        break;
                    case MeleeHitOutcome.Block:
                        canBlock = false;
                        break;
                    case MeleeHitOutcome.Parry:
                        canParry = false;
                        break;
                    default:
                        Log.outDebug(LogFilter.Unit,
                            $"Spell {aurEff.GetId()} SPELL_AURA_IGNORE_COMBAT_RESULT has unhandled state {aurEff.GetMiscValue()}");
                        break;
                }
            }

            if (canDodge)
            {
                // Roll dodge
                int dodgeChance = (int)(GetUnitDodgeChance(attType, victim) * 100.0f);
                if (dodgeChance < 0)
                    dodgeChance = 0;

                if (roll < (tmp += dodgeChance))
                    return SpellMissInfo.Dodge;
            }

            if (canParry)
            {
                // Roll parry
                int parryChance = (int)(GetUnitParryChance(attType, victim) * 100.0f);
                if (parryChance < 0)
                    parryChance = 0;

                tmp += parryChance;
                if (roll < tmp)
                    return SpellMissInfo.Parry;
            }

            if (canBlock)
            {
                int blockChance = (int)(GetUnitBlockChance(attType, victim) * 100.0f);
                if (blockChance < 0)
                    blockChance = 0;
                tmp += blockChance;

                if (roll < tmp)
                    return SpellMissInfo.Block;
            }

            return SpellMissInfo.None;
        }

        public void FinishSpell(CurrentSpellTypes spellType, SpellCastResult result = SpellCastResult.SpellCastOk)
        {
            Spell spell = GetCurrentSpell(spellType);
            if (spell == null)
                return;

            if (spellType == CurrentSpellTypes.Channeled)
                spell.SendChannelUpdate(Milliseconds.Zero);

            spell.Finish(result);
        }

        public virtual SpellInfo GetCastSpellInfo(SpellInfo spellInfo, TriggerCastFlags triggerFlag)
        {
            SpellInfo findMatchingAuraEffectIn(AuraType type)
            {
                foreach (AuraEffect auraEffect in GetAuraEffectsByType(type))
                {
                    bool matches = auraEffect.GetMiscValue() != 0 ? auraEffect.GetMiscValue() == spellInfo.Id : auraEffect.IsAffectingSpell(spellInfo);
                    if (matches)
                    {
                        SpellInfo info = Global.SpellMgr.GetSpellInfo(auraEffect.GetAmount(), GetMap().GetDifficultyID());
                        if (info != null)
                        {
                            if (auraEffect.GetSpellInfo().HasAttribute(SpellAttr8.IgnoreSpellcastOverrideCost))
                                triggerFlag |= TriggerCastFlags.IgnorePowerAndReagentCost;

                            return info;

                        }
                    }
                }

                return null;
            }

            SpellInfo newInfo = findMatchingAuraEffectIn(AuraType.OverrideActionbarSpells);
            if (newInfo != null)
                return newInfo;

            newInfo = findMatchingAuraEffectIn(AuraType.OverrideActionbarSpellsTriggered);
            if (newInfo != null)
                return newInfo;

            return spellInfo;
        }

        public override int GetCastSpellXSpellVisualId(SpellInfo spellInfo)
        {
            foreach (AuraEffect effect in GetAuraEffectsByType(AuraType.OverrideSpellVisual))
            {
                if (effect.GetMiscValue() == spellInfo.Id)
                {
                    SpellInfo visualSpell = Global.SpellMgr.GetSpellInfo(effect.GetMiscValueB(), GetMap().GetDifficultyID());
                    if (visualSpell != null)
                    {
                        spellInfo = visualSpell;
                        break;
                    }
                }
            }

            return base.GetCastSpellXSpellVisualId(spellInfo);
        }

        public SpellHistory GetSpellHistory() { return _spellHistory; }

        public static ProcFlagsHit CreateProcHitMask(SpellNonMeleeDamage damageInfo, SpellMissInfo missCondition)
        {
            ProcFlagsHit hitMask = ProcFlagsHit.None;
            // Check victim state
            if (missCondition != SpellMissInfo.None)
            {
                switch (missCondition)
                {
                    case SpellMissInfo.Miss:
                        hitMask |= ProcFlagsHit.Miss;
                        break;
                    case SpellMissInfo.Dodge:
                        hitMask |= ProcFlagsHit.Dodge;
                        break;
                    case SpellMissInfo.Parry:
                        hitMask |= ProcFlagsHit.Parry;
                        break;
                    case SpellMissInfo.Block:
                        // spells can't be partially blocked (it's damage can though)
                        hitMask |= ProcFlagsHit.Block | ProcFlagsHit.FullBlock;
                        break;
                    case SpellMissInfo.Evade:
                        hitMask |= ProcFlagsHit.Evade;
                        break;
                    case SpellMissInfo.Immune:
                    case SpellMissInfo.Immune2:
                        hitMask |= ProcFlagsHit.Immune;
                        break;
                    case SpellMissInfo.Deflect:
                        hitMask |= ProcFlagsHit.Deflect;
                        break;
                    case SpellMissInfo.Absorb:
                        hitMask |= ProcFlagsHit.Absorb;
                        break;
                    case SpellMissInfo.Reflect:
                        hitMask |= ProcFlagsHit.Reflect;
                        break;
                    case SpellMissInfo.Resist:
                        hitMask |= ProcFlagsHit.FullResist;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                // On block
                if (damageInfo.blocked != 0)
                {
                    hitMask |= ProcFlagsHit.Block;
                    if (damageInfo.fullBlock)
                        hitMask |= ProcFlagsHit.FullBlock;
                }
                // On absorb
                if (damageInfo.absorb != 0)
                    hitMask |= ProcFlagsHit.Absorb;

                // Don't set hit/crit hitMask if damage is nullified
                bool damageNullified = damageInfo.HitInfo.HasAnyFlag(HitInfo.FullAbsorb | HitInfo.FullResist) 
                    || hitMask.HasAnyFlag(ProcFlagsHit.FullBlock);

                if (!damageNullified)
                {
                    // On crit
                    if (damageInfo.HitInfo.HasAnyFlag(HitInfo.CriticalHit))
                        hitMask |= ProcFlagsHit.Critical;
                    else
                        hitMask |= ProcFlagsHit.Normal;
                }
                else if (damageInfo.HitInfo.HasAnyFlag(HitInfo.FullResist))
                    hitMask |= ProcFlagsHit.FullResist;
            }

            return hitMask;
        }

        public void SetAuraStack(int spellId, Unit target, int stack)
        {
            Aura aura = target.GetAura(spellId, GetGUID());
            if (aura == null)
                aura = AddAura(spellId, target);
            if (aura != null && stack != 0)
                aura.SetStackAmount((byte)stack);
        }

        public Spell FindCurrentSpellBySpellId(int spell_id)
        {
            foreach (var spell in m_currentSpells.Values)
            {
                if (spell == null)
                    continue;
                if (spell.m_spellInfo.Id == spell_id)
                    return spell;
            }
            return null;
        }

        public TimeSpan GetCurrentSpellCastTime(int spell_id)
        {
            Spell spell = FindCurrentSpellBySpellId(spell_id);
            if (spell != null)
                return spell.GetCastTime();

            return TimeSpan.Zero;
        }

        public virtual bool HasSpellFocus(Spell focusSpell = null) { return false; }

        /// <summary>
        /// Check if our current channel spell has attribute SPELL_ATTR5_CAN_CHANNEL_WHEN_MOVING
        /// </summary>
        public virtual bool IsMovementPreventedByCasting()
        {
            // can always move when not casting
            if (!HasUnitState(UnitState.Casting))
                return false;

            Spell spell = GetCurrentSpell(CurrentSpellTypes.Generic);
            if (spell != null)
            {
                if (CanCastSpellWhileMoving(spell.GetSpellInfo()) || spell.GetState() == SpellState.Finished ||
                    !spell.m_spellInfo.InterruptFlags.HasFlag(SpellInterruptFlags.Movement))
                    return false;
            }

            // channeled spells during channel stage (after the initial cast timer) allow movement with a specific spell attribute
            spell = m_currentSpells.LookupByKey(CurrentSpellTypes.Channeled);
            if (spell != null)
            {
                if (spell.GetState() != SpellState.Finished && spell.IsChannelActive())
                {
                    if (spell.GetSpellInfo().IsMoveAllowedChannel() || CanCastSpellWhileMoving(spell.GetSpellInfo()))
                        return false;
                }
            }

            // prohibit movement for all other spell casts
            return true;
        }

        public bool HasAuraTypeWithFamilyFlags(AuraType auraType, SpellFamilyNames familyName, FlagArray128 familyFlags)
        {
            foreach (AuraEffect aura in GetAuraEffectsByType(auraType))
            {
                if (aura.GetSpellInfo().SpellFamilyName == (SpellFamilyNames)familyName && aura.GetSpellInfo().SpellFamilyFlags & familyFlags)
                    return true;
            }

            return false;
        }

        public bool HasBreakableByDamageAuraType(AuraType type, int excludeAura = 0)
        {
            var auras = GetAuraEffectsByType(type);
            foreach (var eff in auras)
            {
                if ((excludeAura == 0 || excludeAura != eff.GetSpellInfo().Id) && //Avoid self interrupt of channeled Crowd Control spells like Seduction
                eff.GetSpellInfo().HasAuraInterruptFlag(SpellAuraInterruptFlags.Damage))
                    return true;
            }

            return false;
        }

        public bool HasBreakableByDamageCrowdControlAura(Unit excludeCasterChannel = null)
        {
            int excludeAura = 0;
            Spell currentChanneledSpell = excludeCasterChannel?.GetCurrentSpell(CurrentSpellTypes.Channeled);
            if (currentChanneledSpell != null)
                excludeAura = currentChanneledSpell.GetSpellInfo().Id; //Avoid self interrupt of channeled Crowd Control spells like Seduction

            return (HasBreakableByDamageAuraType(AuraType.ModConfuse, excludeAura)
                    || HasBreakableByDamageAuraType(AuraType.ModFear, excludeAura)
                    || HasBreakableByDamageAuraType(AuraType.ModStun, excludeAura)
                    || HasBreakableByDamageAuraType(AuraType.ModRoot, excludeAura)
                    || HasBreakableByDamageAuraType(AuraType.ModRoot2, excludeAura)
                    || HasBreakableByDamageAuraType(AuraType.Transform, excludeAura));
        }

        public uint GetDiseasesByCaster(ObjectGuid casterGUID, bool remove = false)
        {
            AuraType[] diseaseAuraTypes =
            [
                AuraType.PeriodicDamage, // Frost Fever and Blood Plague
                AuraType.Linked          // Crypt Fever and Ebon Plague
            ];

            uint diseases = 0;
            foreach (var aType in diseaseAuraTypes)
            {
                if (aType == AuraType.None)
                    break;

                foreach (var eff in GetAuraEffectsByTypeCopy(aType))
                {
                    // Get auras with disease dispel Type by caster
                    if (eff.GetSpellInfo().Dispel == DispelType.Disease && eff.GetCasterGUID() == casterGUID)
                    {
                        ++diseases;

                        if (remove)
                        {
                            RemoveAura(eff.GetId(), eff.GetCasterGUID());
                            continue;
                        }
                    }
                }
            }
            return diseases;
        }

        uint GetDoTsByCaster(ObjectGuid casterGUID)
        {
            AuraType[] diseaseAuraTypes =
            [
                AuraType.PeriodicDamage,
                AuraType.PeriodicDamagePercent,
                AuraType.None
            ];

            uint dots = 0;
            foreach (var aura in diseaseAuraTypes)
            {
                if (aura == AuraType.None)
                    break;

                foreach (var eff in GetAuraEffectsByType(aura))
                {
                    // Get auras by caster
                    if (eff.GetCasterGUID() == casterGUID)
                        ++dots;
                }
            }

            return dots;
        }

        public void SendEnergizeSpellLog(Unit victim, int spellId, int amount, int overEnergize, PowerType powerType)
        {
            SpellEnergizeLog data = new();
            data.CasterGUID = GetGUID();
            data.TargetGUID = victim.GetGUID();
            data.SpellID = spellId;
            data.Type = powerType;
            data.Amount = amount;
            data.OverEnergize = overEnergize;
            data.LogData.Initialize(victim);

            SendCombatLogMessage(data);
        }

        public void EnergizeBySpell(Unit victim, SpellInfo spellInfo, int damage, PowerType powerType)
        {
            if (victim is Player victimPlayer)
            {
                var powerTypeEntry = Global.DB2Mgr.GetPowerTypeEntry(powerType);
                if (powerTypeEntry != null)
                {
                    if (powerTypeEntry.HasFlag(PowerTypeFlags.UseRegenInterrupt))
                        victimPlayer.InterruptPowerRegen(powerType);
                }
            }

            int gain = 0;
            int overEnergize = 0;
            ServerTime currentTime = LoopTime.ServerTime;

            if (Runes.RunesByPowerList.ContainsKey(powerType))
            {
                if (this is Player player)
                {
                    // Runes behave a bit differently than regular power.
                    // While they do have a legit power type and value,
                    // they never are set or changed so we have to use the runes api instead

                    if (player.GetClass() == Class.DeathKnight)
                    {
                        RuneStateMask activatedRunes = RuneStateMask.None;
                        int runesToEnergize = damage;
                        foreach (var rune in Runes.RunesList.Values)
                        {
                            if (runesToEnergize <= 0)
                                break;

                            if (rune.Power == powerType
                                && player.Runes.GetRuneCooldown(rune, currentTime) > RuneCooldowns.Zero)
                            {
                                player.Runes.SetRuneCooldown(rune, RuneCooldowns.Zero, currentTime);
                                activatedRunes |= rune.Mask;
                                --runesToEnergize;
                            }
                        }

                        foreach (var rune in Runes.RunesByPowerList[powerType])
                        {
                            if (runesToEnergize <= 0)
                                break;

                            if (player.Runes.GetRuneType(rune) == RuneType.Death
                                && player.Runes.GetRuneCooldown(rune, currentTime) > RuneCooldowns.Zero)
                            {
                                player.Runes.SetRuneCooldown(rune, RuneCooldowns.Zero, currentTime);
                                activatedRunes |= rune.Mask;
                                --runesToEnergize;
                            }
                        }

                        player.Runes.SendActivateRunes(activatedRunes);

                        gain = damage - runesToEnergize;
                        overEnergize = runesToEnergize;
                    }
                }
            }
            else
            {
                gain = victim.ModifyPower(powerType, damage, false);
                overEnergize = damage - gain;
            }            

            victim.GetThreatManager().ForwardThreatForAssistingMe(this, damage / 2, spellInfo, true);
            SendEnergizeSpellLog(victim, spellInfo.Id, gain, overEnergize, powerType);
        }

        public void ApplySpellImmune(int spellId, SpellImmunity op, SpellSchoolMask type, bool apply)
        {
            ApplySpellImmune(spellId, op, (int)type, apply);
        }

        public void ApplySpellImmune(int spellId, SpellImmunity op, DispelType type, bool apply)
        {
            ApplySpellImmune(spellId, op, (int)type, apply);
        }

        public void ApplySpellImmune(int spellId, SpellImmunity op, AuraType type, bool apply)
        {
            ApplySpellImmune(spellId, op, (int)type, apply);
        }

        public void ApplySpellImmune(int spellId, SpellImmunity op, SpellEffectName type, bool apply)
        {
            ApplySpellImmune(spellId, op, (int)type, apply);
        }

        public void ApplySpellImmune(int spellId, SpellImmunity op, Mechanics type, bool apply)
        {
            ApplySpellImmune(spellId, op, (int)type, apply);
        }

        public void ApplySpellImmune(int spellId, SpellImmunity op, int type, bool apply)
        {
            if (apply)
            {
                m_spellImmune[(int)op].Add(type, spellId);
            }
            else
            {
                var bounds = m_spellImmune[(int)op][type];
                foreach (var spell in bounds.ToList())
                {
                    if (spell == spellId)
                    {
                        m_spellImmune[(int)op].Remove(type, spell);
                        break;
                    }
                }
            }
        }

        public bool IsImmunedToSpell(SpellInfo spellInfo, WorldObject caster, bool requireImmunityPurgesEffectAttribute = false)
        {
            if (spellInfo == null)
                return false;

            bool hasImmunity(MultiMap<int, int> container, int key)
            {
                var range = container[key];
                if (!requireImmunityPurgesEffectAttribute)
                    return !range.Empty();

                return range.Any(entry =>
                {
                    SpellInfo immunitySourceSpell = Global.SpellMgr.GetSpellInfo(entry, Difficulty.None);
                    if (immunitySourceSpell != null && immunitySourceSpell.HasAttribute(SpellAttr1.ImmunityPurgesEffect))
                        return true;

                    return false;
                });
            }

            // Single spell immunity.
            var idList = m_spellImmune[(int)SpellImmunity.Id];
            if (hasImmunity(idList, spellInfo.Id))
                return true;

            if (spellInfo.HasAttribute(SpellAttr0.NoImmunities))
                return false;

            int dispel = (int)spellInfo.Dispel;
            if (dispel != 0)
            {
                var dispelList = m_spellImmune[(int)SpellImmunity.Dispel];
                if (hasImmunity(dispelList, dispel))
                    return true;
            }

            // Spells that don't have effectMechanics.
            int mechanic = (int)spellInfo.Mechanic;
            if (mechanic != 0)
            {
                var mechanicList = m_spellImmune[(int)SpellImmunity.Mechanic];
                if (hasImmunity(mechanicList, mechanic))
                    return true;
            }

            bool immuneToAllEffects = true;
            foreach (var spellEffectInfo in spellInfo.GetEffects())
            {
                // State/effect immunities applied by aura expect full spell immunity
                // Ignore effects with mechanic, they are supposed to be checked separately
                if (!spellEffectInfo.IsEffect())
                    continue;

                if (!IsImmunedToSpellEffect(spellInfo, spellEffectInfo, caster, requireImmunityPurgesEffectAttribute))
                {
                    immuneToAllEffects = false;
                    break;
                }

                if (spellInfo.HasAttribute(SpellAttr4.NoPartialImmunity))
                    return true;
            }

            if (immuneToAllEffects) //Return immune only if the target is immune to all spell effects.
                return true;

            int schoolMask = (int)spellInfo.GetSchoolMask();
            if (schoolMask != 0)
            {
                int schoolImmunityMask = 0;
                var schoolList = m_spellImmune[(int)SpellImmunity.School];
                foreach (var pair in schoolList)
                {
                    if (!pair.Key.HasAnyFlag(schoolMask))
                        continue;

                    SpellInfo immuneSpellInfo = Global.SpellMgr.GetSpellInfo(pair.Value, GetMap().GetDifficultyID());
                    if (requireImmunityPurgesEffectAttribute)
                    {
                        if (immuneSpellInfo == null || !immuneSpellInfo.HasAttribute(SpellAttr1.ImmunityPurgesEffect))
                            continue;
                    }

                    // Consider the school immune if any of these conditions are not satisfied.
                    // In case of no immuneSpellInfo, ignore that condition and check only the other conditions
                    if ((immuneSpellInfo != null && !immuneSpellInfo.IsPositive()) || !spellInfo.IsPositive()
                        || caster == null || !IsFriendlyTo(caster))
                    {
                        if (!spellInfo.CanPierceImmuneAura(immuneSpellInfo))
                        {
                            schoolImmunityMask |= pair.Key;
                        }
                    }
                }

                if (schoolImmunityMask.HasFlag(schoolMask))
                    return true;
            }

            return false;
        }

        public uint GetSchoolImmunityMask()
        {
            int mask = 0;
            var schoolList = m_spellImmune[(int)SpellImmunity.School];
            foreach (var pair in schoolList)
                mask |= pair.Key;

            return (uint)mask;
        }

        public uint GetDamageImmunityMask()
        {
            int mask = 0;
            var damageList = m_spellImmune[(int)SpellImmunity.Damage];
            foreach (var pair in damageList)
                mask |= pair.Key;

            return (uint)mask;
        }

        public ulong GetMechanicImmunityMask()
        {
            ulong mask = 0;
            var mechanicList = m_spellImmune[(int)SpellImmunity.Mechanic];
            foreach (var pair in mechanicList)
                mask |= (1ul << pair.Value);

            return mask;
        }

        public virtual bool IsImmunedToSpellEffect(SpellInfo spellInfo, SpellEffectInfo spellEffectInfo, WorldObject caster, bool requireImmunityPurgesEffectAttribute = false)
        {
            if (spellInfo == null)
                return false;

            if (spellInfo.HasAttribute(SpellAttr0.NoImmunities))
                return false;

            if (spellEffectInfo.EffectAttributes.HasFlag(SpellEffectAttributes.NoImmunity))
                return false;

            bool hasImmunity(MultiMap<int, int> container, int key)
            {
                var range = container[key];
                if (!requireImmunityPurgesEffectAttribute)
                    return !range.Empty();

                return range.Any(entry =>
                {
                    var immunitySourceSpell = Global.SpellMgr.GetSpellInfo(entry, Difficulty.None);
                    if (immunitySourceSpell != null)
                    {
                        if (immunitySourceSpell.HasAttribute(SpellAttr1.ImmunityPurgesEffect))
                            return true;
                    }

                    return false;
                });
            }

            // If m_immuneToEffect Type contain this effect Type, IMMUNE effect.
            var effectList = m_spellImmune[(int)SpellImmunity.Effect];
            if (hasImmunity(effectList, (int)spellEffectInfo.Effect))
                return true;

            int mechanic = (int)spellEffectInfo.Mechanic;
            if (mechanic != 0)
            {
                var mechanicList = m_spellImmune[(int)SpellImmunity.Mechanic];
                if (hasImmunity(mechanicList, mechanic))
                    return true;
            }

            AuraType aura = spellEffectInfo.ApplyAuraName;
            if (aura != 0)
            {
                if (!spellInfo.HasAttribute(SpellAttr3.AlwaysHit))
                {
                    var list = m_spellImmune[(int)SpellImmunity.State];
                    if (hasImmunity(list, (int)aura))
                        return true;
                }

                if (!spellInfo.HasAttribute(SpellAttr2.NoSchoolImmunities))
                {
                    // Check for immune to application of harmful magical effects
                    foreach (var auraEffect in GetAuraEffectsByType(AuraType.ModImmuneAuraApplySchool))
                    {
                        if (auraEffect.GetMiscValue().HasAnyFlag((int)spellInfo.GetSchoolMask()) &&  // Check school
                            ((caster != null && !IsFriendlyTo(caster)) || !spellInfo.IsPositiveEffect(spellEffectInfo.EffectIndex)))
                        {                     // Harmful
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool IsImmunedToDamage(SpellSchoolMask schoolMask)
        {
            if (schoolMask == SpellSchoolMask.None)
                return false;

            // If m_immuneToSchool Type contain this school Type, IMMUNE damage.
            uint schoolImmunityMask = GetSchoolImmunityMask();
            if (((SpellSchoolMask)schoolImmunityMask & schoolMask) == schoolMask) // We need to be immune to all types
                return true;

            // If m_immuneToDamage Type contain magic, IMMUNE damage.
            uint damageImmunityMask = GetDamageImmunityMask();
            if (((SpellSchoolMask)damageImmunityMask & schoolMask) == schoolMask) // We need to be immune to all types
                return true;

            return false;
        }

        public bool IsImmunedToDamage(SpellInfo spellInfo, SpellEffectInfo spellEffectInfo = null)
        {
            if (spellInfo == null)
                return false;

            // for example 40175
            if (spellInfo.HasAttribute(SpellAttr0.NoImmunities) && spellInfo.HasAttribute(SpellAttr3.AlwaysHit))
                return false;

            if (spellInfo.HasAttribute(SpellAttr1.ImmunityToHostileAndFriendlyEffects) || spellInfo.HasAttribute(SpellAttr2.NoSchoolImmunities))
                return false;

            if (spellEffectInfo != null && spellEffectInfo.EffectAttributes.HasFlag(SpellEffectAttributes.NoImmunity))
                return false;

            int schoolMask = (int)spellInfo.GetSchoolMask();
            if (schoolMask != 0)
            {
                // If m_immuneToSchool Type contain this school Type, IMMUNE damage.
                int schoolImmunityMask = 0;
                var schoolList = m_spellImmune[(int)SpellImmunity.School];
                foreach (var pair in schoolList)
                {
                    if (Convert.ToBoolean(pair.Key & schoolMask) && !spellInfo.CanPierceImmuneAura(Global.SpellMgr.GetSpellInfo(pair.Value, GetMap().GetDifficultyID())))
                    {
                        schoolImmunityMask |= pair.Key;
                    }
                }

                // // We need to be immune to all types
                if ((schoolImmunityMask & schoolMask) == schoolMask)
                    return true;

                // If m_immuneToDamage Type contain magic, IMMUNE damage.
                uint damageImmunityMask = GetDamageImmunityMask();
                if ((damageImmunityMask & schoolMask) == schoolMask) // We need to be immune to all types
                    return true;
            }

            return false;
        }

        public bool CanCastSpellWhileMoving(SpellInfo spellInfo)
        {
            if (HasAuraTypeWithAffectMask(AuraType.CastWhileWalking, spellInfo))
                return true;

            if (HasAuraType(AuraType.CastWhileWalkingAll))
                return true;

            foreach (uint label in spellInfo.Labels)
                if (HasAuraTypeWithMiscvalue(AuraType.CastWhileWalkingBySpellLabel, (int)label))
                    return true;

            return false;
        }

        public static void ProcSkillsAndAuras(Unit actor, Unit actionTarget, ProcFlagsInit typeMaskActor, ProcFlagsInit typeMaskActionTarget, ProcFlagsSpellType spellTypeMask, ProcFlagsSpellPhase spellPhaseMask, ProcFlagsHit hitMask, Spell spell, DamageInfo damageInfo, HealInfo healInfo)
        {
            int ProcChainHardLimit = 10;
            if (spell != null && spell.GetProcChainLength() >= ProcChainHardLimit)
            {
                Log.outError(LogFilter.Spells, 
                    $"Unit::ProcSkillsAndAuras: Possible infinite proc loop detected, " +
                    $"current triggering spell {spell.GetDebugInfo()}");

                return;
            }

            WeaponAttackType attType = damageInfo != null ? damageInfo.GetAttackType() : WeaponAttackType.BaseAttack;
            SpellInfo spellInfo = null;
            if (spell != null)
                spellInfo = spell.GetSpellInfo();
            else if (damageInfo != null)
                spellInfo = damageInfo.GetSpellInfo();
            else if (healInfo != null)
                spellInfo = healInfo.GetSpellInfo();

            if (typeMaskActor != null && actor != null && !(spellInfo != null && spellInfo.HasAttribute(SpellAttr3.SuppressCasterProcs)))
                actor.ProcSkillsAndReactives(false, actionTarget, typeMaskActor, hitMask, attType);

            if (typeMaskActionTarget != ProcFlagsInit.None && actionTarget != null
                && !(spellInfo != null && spellInfo.HasAttribute(SpellAttr3.SuppressTargetProcs)))
            {
                actionTarget.ProcSkillsAndReactives(true, actor, typeMaskActionTarget, hitMask, attType);
            }

            if (actor != null)
                actor.TriggerAurasProcOnEvent(null, null, actionTarget, typeMaskActor, typeMaskActionTarget, spellTypeMask, spellPhaseMask, hitMask, spell, damageInfo, healInfo);
        }

        void ProcSkillsAndReactives(bool isVictim, Unit procTarget, ProcFlagsInit typeMask, ProcFlagsHit hitMask, WeaponAttackType attType)
        {
            // Player is loaded now - do not allow passive spell casts to proc
            if (IsPlayer() && ToPlayer().GetSession().PlayerLoading())
                return;

            // For melee/ranged based attack need update skills and set some Aura states if victim present
            if (typeMask.HasAnyFlag(ProcFlags.MeleeBasedTriggerMask) && procTarget != null)
            {
                // Update skills here for players
                // only when you are not fighting other players or their pets/totems (pvp)
                if (IsPlayer() && !procTarget.IsPlayer() &&
                    !(procTarget.IsTotem() && procTarget.ToTotem().GetOwner().IsPlayer()) && !procTarget.IsPet())
                {
                    // On melee based hit/miss/resist need update skill (for victim and attacker)
                    if ((hitMask & (ProcFlagsHit.Normal | ProcFlagsHit.Miss | ProcFlagsHit.FullResist)) != 0)
                    {
                        if (!procTarget.IsPlayer() && !procTarget.IsCritter())
                            ToPlayer().UpdateCombatSkills(procTarget, attType, isVictim);
                    }
                    // Update defense if player is victim and parry/dodge/block
                    else if (isVictim && ((hitMask & (ProcFlagsHit.Dodge | ProcFlagsHit.Parry | ProcFlagsHit.Block)) != 0))
                        ToPlayer().UpdateCombatSkills(procTarget, attType, true);
                }

                // If exist crit/parry/dodge/block need update aura state (for victim and attacker)
                if ((hitMask & (ProcFlagsHit.Critical | ProcFlagsHit.Parry | ProcFlagsHit.Dodge | ProcFlagsHit.Block)) != 0)
                {
                    // for victim
                    if (isVictim)
                    {
                        // if victim and dodge attack
                        if (hitMask.HasAnyFlag(ProcFlagsHit.Dodge))
                        {
                            // Update AURA_STATE on dodge
                            if (GetClass() != Class.Rogue) // skip Rogue Riposte
                            {
                                ModifyAuraState(AuraStateType.Defensive, true);
                                StartReactiveTimer(ReactiveType.Defense);
                            }
                        }
                        // if victim and parry attack
                        if (hitMask.HasAnyFlag(ProcFlagsHit.Parry))
                        {
                            ModifyAuraState(AuraStateType.Defensive, true);
                            StartReactiveTimer(ReactiveType.Defense);
                        }
                        // if and victim block attack
                        if (hitMask.HasAnyFlag(ProcFlagsHit.Block))
                        {
                            ModifyAuraState(AuraStateType.Defensive, true);
                            StartReactiveTimer(ReactiveType.Defense);
                        }
                    }
                }
            }
        }

        void GetProcAurasTriggeredOnEvent(List<(uint, AuraApplication)> aurasTriggeringProc, List<AuraApplication> procAuras, ProcEventInfo eventInfo)
        {
            ServerTime now = LoopTime.ServerTime;

            void processAuraApplication(AuraApplication aurApp)
            {
                uint procEffectMask = aurApp.GetBase().GetProcEffectMask(aurApp, eventInfo, now);
                if (procEffectMask != 0)
                {
                    aurApp.GetBase().PrepareProcToTrigger(aurApp, eventInfo, now);
                    aurasTriggeringProc.Add((procEffectMask, aurApp));
                }
                else
                {
                    if (aurApp.GetBase().GetSpellInfo().HasAttribute(SpellAttr0.ProcFailureBurnsCharge))
                    {
                        SpellProcEntry procEntry = Global.SpellMgr.GetSpellProcEntry(aurApp.GetBase().GetSpellInfo());
                        if (procEntry != null)
                        {
                            aurApp.GetBase().PrepareProcChargeDrop(procEntry, eventInfo);
                            aurasTriggeringProc.Add((0u, aurApp));
                        }
                    }

                    if (aurApp.GetBase().GetSpellInfo().HasAttribute(SpellAttr2.ProcCooldownOnFailure))
                    {
                        SpellProcEntry procEntry = Global.SpellMgr.GetSpellProcEntry(aurApp.GetBase().GetSpellInfo());
                        if (procEntry != null)
                            aurApp.GetBase().AddProcCooldown(procEntry, now);
                    }
                }
            }

            // use provided list of auras which can proc
            if (procAuras != null)
            {
                foreach (AuraApplication aurApp in procAuras)
                {
                    Cypher.Assert(aurApp.GetTarget() == this);
                    processAuraApplication(aurApp);
                }
            }
            // or generate one on our own
            else
            {
                foreach (var pair in GetAppliedAuras())
                    processAuraApplication(pair.Value);
            }
        }

        void TriggerAurasProcOnEvent(List<AuraApplication> myProcAuras, List<AuraApplication> targetProcAuras, Unit actionTarget, ProcFlagsInit typeMaskActor, ProcFlagsInit typeMaskActionTarget, ProcFlagsSpellType spellTypeMask, ProcFlagsSpellPhase spellPhaseMask, ProcFlagsHit hitMask, Spell spell, DamageInfo damageInfo, HealInfo healInfo)
        {
            // prepare data for self trigger
            ProcEventInfo myProcEventInfo = new(this, actionTarget, actionTarget, typeMaskActor, spellTypeMask, spellPhaseMask, hitMask, spell, damageInfo, healInfo);
            List<(uint, AuraApplication)> myAurasTriggeringProc = new();
            if (typeMaskActor != ProcFlagsInit.None)
            {
                GetProcAurasTriggeredOnEvent(myAurasTriggeringProc, myProcAuras, myProcEventInfo);

                // needed for example for Cobra Strikes, pet does the attack, but aura is on owner
                Player modOwner = GetSpellModOwner();
                if (modOwner != null)
                {
                    if (modOwner != this && spell != null)
                    {
                        List<AuraApplication> modAuras = new();
                        foreach (var itr in modOwner.GetAppliedAuras())
                        {
                            if (spell.m_appliedMods.Contains(itr.Value.GetBase()))
                                modAuras.Add(itr.Value);
                        }
                        modOwner.GetProcAurasTriggeredOnEvent(myAurasTriggeringProc, modAuras, myProcEventInfo);
                    }
                }
            }

            // prepare data for target trigger
            ProcEventInfo targetProcEventInfo = new(this, actionTarget, this, typeMaskActionTarget, spellTypeMask, spellPhaseMask, hitMask, spell, damageInfo, healInfo);
            List<(uint, AuraApplication)> targetAurasTriggeringProc = new();
            if (typeMaskActionTarget != ProcFlagsInit.None && actionTarget != null)
                actionTarget.GetProcAurasTriggeredOnEvent(targetAurasTriggeringProc, targetProcAuras, targetProcEventInfo);

            TriggerAurasProcOnEvent(myProcEventInfo, myAurasTriggeringProc);

            if (typeMaskActionTarget != ProcFlagsInit.None && actionTarget != null)
                actionTarget.TriggerAurasProcOnEvent(targetProcEventInfo, targetAurasTriggeringProc);
        }

        void TriggerAurasProcOnEvent(ProcEventInfo eventInfo, List<(uint, AuraApplication)> aurasTriggeringProc)
        {
            Spell triggeringSpell = eventInfo.GetProcSpell();
            bool disableProcs = triggeringSpell != null && triggeringSpell.IsProcDisabled();


            int oldProcChainLength = m_procChainLength;
            m_procChainLength = Math.Max(m_procChainLength + 1, triggeringSpell != null ? triggeringSpell.GetProcChainLength() : 0);

            if (disableProcs)
                SetCantProc(true);

            foreach (var (procEffectMask, aurApp) in aurasTriggeringProc)
            {
                if (aurApp.GetRemoveMode() != 0)
                    continue;

                aurApp.GetBase().TriggerProcOnEvent(procEffectMask, aurApp, eventInfo);
            }

            if (disableProcs)
                SetCantProc(false);

            m_procChainLength = oldProcChainLength;
        }

        void SetCantProc(bool apply)
        {
            if (apply)
                ++m_procDeep;
            else
            {
                Cypher.Assert(m_procDeep != 0);
                --m_procDeep;
            }
        }

        public void CastStop(uint except_spellid = 0)
        {
            for (var i = CurrentSpellTypes.Generic; i < CurrentSpellTypes.Max; i++)
            {
                if (GetCurrentSpell(i) != null && GetCurrentSpell(i).m_spellInfo.Id != except_spellid)
                    InterruptSpell(i, false);
            }
        }

        public ushort GetMaxSkillValueForLevel(Unit target = null)
        {
            return (ushort)(target != null ? GetLevelForTarget(target) : GetLevel() * 5);
        }

        public Spell GetCurrentSpell(CurrentSpellTypes spellType)
        {
            return m_currentSpells.LookupByKey(spellType);
        }

        public void SetCurrentCastSpell(Spell pSpell)
        {
            Cypher.Assert(pSpell != null);                                         // NULL may be never passed here, use InterruptSpell or InterruptNonMeleeSpells

            CurrentSpellTypes CSpellType = pSpell.GetCurrentContainer();

            if (pSpell == GetCurrentSpell(CSpellType))             // avoid breaking self
                return;

            // special breakage effects:
            switch (CSpellType)
            {
                case CurrentSpellTypes.Generic:
                {
                    InterruptSpell(CurrentSpellTypes.Generic, false);

                    // generic spells always break channeled not delayed spells
                    if (GetCurrentSpell(CurrentSpellTypes.Channeled) != null &&
                        !GetCurrentSpell(CurrentSpellTypes.Channeled).GetSpellInfo().HasAttribute(SpellAttr5.AllowActionsDuringChannel))
                    {
                        InterruptSpell(CurrentSpellTypes.Channeled, false);
                    }

                    // autorepeat breaking
                    if (GetCurrentSpell(CurrentSpellTypes.AutoRepeat) != null)
                    {
                        // break autorepeat if not Auto Shot
                        if (m_currentSpells[CurrentSpellTypes.AutoRepeat].m_spellInfo.Id != 75)
                            InterruptSpell(CurrentSpellTypes.AutoRepeat);
                    }

                    if (pSpell.m_spellInfo.CalcCastTime() > 0)
                        AddUnitState(UnitState.Casting);

                    break;
                }
                case CurrentSpellTypes.Channeled:
                {
                    // channel spells always break generic non-delayed and any channeled spells
                    InterruptSpell(CurrentSpellTypes.Generic, false);
                    InterruptSpell(CurrentSpellTypes.Channeled);

                    // it also does break autorepeat if not Auto Shot
                    if (GetCurrentSpell(CurrentSpellTypes.AutoRepeat) != null &&
                        m_currentSpells[CurrentSpellTypes.AutoRepeat].m_spellInfo.Id != 75)
                        InterruptSpell(CurrentSpellTypes.AutoRepeat);
                    AddUnitState(UnitState.Casting);

                    break;
                }
                case CurrentSpellTypes.AutoRepeat:
                {
                    if (GetCurrentSpell(CSpellType) != null && GetCurrentSpell(CSpellType).GetState() == SpellState.Idle)
                        GetCurrentSpell(CSpellType).SetState(SpellState.Finished);

                    // only Auto Shoot does not break anything
                    if (pSpell.m_spellInfo.Id != 75)
                    {
                        // generic autorepeats break generic non-delayed and channeled non-delayed spells
                        InterruptSpell(CurrentSpellTypes.Generic, false);
                        InterruptSpell(CurrentSpellTypes.Channeled, false);
                    }
                    break;
                }
                default:
                    break; // other spell types don't break anything now
            }

            // current spell (if it is still here) may be safely deleted now
            if (GetCurrentSpell(CSpellType) != null)
                m_currentSpells[CSpellType].SetReferencedFromCurrent(false);

            // set new current spell
            m_currentSpells[CSpellType] = pSpell;
            pSpell.SetReferencedFromCurrent(true);

            pSpell.m_selfContainer = m_currentSpells[pSpell.GetCurrentContainer()];
        }

        public bool IsNonMeleeSpellCast(bool withDelayed, bool skipChanneled = false, bool skipAutorepeat = false, bool isAutoshoot = false, bool skipInstant = true)
        {
            // We don't do loop here to explicitly show that melee spell is excluded.
            // Maybe later some special spells will be excluded too.

            // generic spells are cast when they are not finished and not delayed
            var currentSpell = GetCurrentSpell(CurrentSpellTypes.Generic);
            if (currentSpell != null && (currentSpell.GetState() != SpellState.Finished) 
                && (withDelayed || currentSpell.GetState() != SpellState.Delayed))
            {
                if (!skipInstant || currentSpell.GetCastTime() != 0)
                {
                    if (!isAutoshoot || !currentSpell.m_spellInfo.HasAttribute(SpellAttr2.DoNotResetCombatTimers))
                        return true;
                }
            }

            currentSpell = GetCurrentSpell(CurrentSpellTypes.Channeled);
            // channeled spells may be delayed, but they are still considered cast
            if (!skipChanneled && currentSpell != null && (currentSpell.GetState() != SpellState.Finished))
            {
                if (!isAutoshoot || !currentSpell.m_spellInfo.HasAttribute(SpellAttr2.DoNotResetCombatTimers))
                    return true;
            }

            currentSpell = GetCurrentSpell(CurrentSpellTypes.AutoRepeat);
            // autorepeat spells may be finished or delayed, but they are still considered cast
            if (!skipAutorepeat && currentSpell != null)
                return true;

            return false;
        }

        public static int SpellCriticalDamageBonus(Unit caster, SpellInfo spellProto, int damage, Unit victim = null)
        {
            // Calculate critical bonus
            int crit_bonus = damage * 2;
            float crit_mod = 0.0f;

            if (caster != null)
            {
                crit_mod += (caster.GetTotalAuraMultiplierByMiscMask(AuraType.ModCritDamageBonus, (uint)spellProto.GetSchoolMask()) - 1.0f) * 100;

                if (crit_bonus != 0)
                    MathFunctions.AddPct(ref crit_bonus, (int)crit_mod);

                MathFunctions.AddPct(ref crit_bonus, victim.GetTotalAuraModifier(AuraType.ModCriticalDamageTakenFromCaster, aurEff =>
                {
                    return aurEff.GetCasterGUID() == caster.GetGUID();
                }));

                crit_bonus -= damage;

                // adds additional damage to critBonus (from talents)
                Player modOwner = caster.GetSpellModOwner();
                if (modOwner != null)
                    modOwner.ApplySpellMod(spellProto, SpellModOp.CritDamageAndHealing, ref crit_bonus);

                crit_bonus += damage;
            }

            return crit_bonus;
        }

        public int GetWeaponSkillValue(WeaponAttackType attType, Unit target = null)
        {
            int value = 0;
            if (this is Player player)
            {
                Item item = player.GetWeaponForAttack(attType, true);

                if (IsInFeralForm())
                    return GetMaxSkillValueForLevel();              // always maximized SKILL_FERAL_COMBAT in fact

                // weapon skill or (unarmed for base attack)
                SkillType skill = SkillType.Unarmed;
                if (item != null)
                    skill = item.GetSkill();

                skill = skill == SkillType.None ? SkillType.Unarmed : skill;

                // in PvP use full skill instead current skill value
                value = (target != null && target.IsControlledByPlayer())
                    ? player.GetMaxSkillValue(skill)
                    : player.GetSkillValue(skill);

                // Modify value from ratings
                value += (int)player.GetRatingBonusValue(CombatRating.WeaponSkill);

                switch (attType)
                {
                    case WeaponAttackType.BaseAttack:
                        value += (int)player.GetRatingBonusValue(CombatRating.WeaponSkillMainhand);
                        break;
                    case WeaponAttackType.OffAttack:
                        value += (int)player.GetRatingBonusValue(CombatRating.WeaponSkillOffhand);
                        break;
                    case WeaponAttackType.RangedAttack:
                        value += (int)player.GetRatingBonusValue(CombatRating.WeaponSkillRanged);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                value = GetMaxSkillValueForLevel(target);
            }

            return value;
        }

        public void _DeleteRemovedAuras()
        {
            while (!m_removedAuras.Empty())
            {
                m_removedAuras.First().Dispose();
                m_removedAuras.RemoveAt(0);
            }

            m_removedAurasCount = 0;
        }

        public bool HasStealthAura() { return HasAuraType(AuraType.ModStealth); }
        public bool HasInvisibilityAura() { return HasAuraType(AuraType.ModInvisibility); }
        public bool IsFeared() { return HasAuraType(AuraType.ModFear); }
        public bool IsFrozen() { return HasAuraState(AuraStateType.Frozen); }

        public bool HasRootAura() 
        { 
            return HasAuraType(AuraType.ModRoot) || HasAuraType(AuraType.ModRoot2) || HasAuraType(AuraType.ModRootDisableGravity); 
        }

        public bool IsPolymorphed()
        {
            var transformId = GetTransformSpell();
            if (transformId == 0)
                return false;

            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(transformId, GetMap().GetDifficultyID());
            if (spellInfo == null)
                return false;

            return spellInfo.GetSpellSpecific() == SpellSpecificType.MagePolymorph;
        }

        public static void DealHeal(HealInfo healInfo)
        {
            int gain = 0;
            Unit healer = healInfo.GetHealer();
            Unit victim = healInfo.GetTarget();
            var addhealth = healInfo.GetHeal();

            UnitAI victimAI = victim.GetAI();
            if (victimAI != null)
                victimAI.HealReceived(healer, addhealth);

            UnitAI healerAI = healer != null ? healer.GetAI() : null;
            if (healerAI != null)
                healerAI.HealDone(victim, addhealth);

            if (addhealth != 0)
                gain = (int)victim.ModifyHealth(addhealth);

            // Hook for OnHeal Event
            int tempGain = gain;
            Global.ScriptMgr.OnHeal(healer, victim, ref tempGain);
            gain = tempGain;

            Unit unit = healer;
            if (healer != null && healer.IsCreature() && healer.IsTotem())
                unit = healer.GetOwner();

            if (unit != null)
            {
                Player bgPlayer = unit.ToPlayer();
                if (bgPlayer != null)
                {
                    if (healInfo.GetSpellInfo() == null || !healInfo.GetSpellInfo().HasAttribute(SpellAttr7.DoNotCountForPvpScoreboard))
                    {
                        Battleground bg = bgPlayer.GetBattleground();
                        if (bg != null)
                            bg.UpdatePlayerScore(bgPlayer, ScoreType.HealingDone, gain);
                    }

                    // use the actual gain, as the overheal shall not be counted, skip gain 0 (it ignored anyway in to criteria)
                    if (gain != 0)
                        bgPlayer.UpdateCriteria(CriteriaType.HealingDone, gain, 0, 0, victim);

                    bgPlayer.UpdateCriteria(CriteriaType.HighestHealCast, addhealth);
                }
            }

            Player player = victim.ToPlayer();
            if (player != null)
            {
                player.UpdateCriteria(CriteriaType.TotalHealReceived, gain);
                player.UpdateCriteria(CriteriaType.HighestHealReceived, addhealth);
            }

            if (gain != 0)
                healInfo.SetEffectiveHeal(gain > 0 ? gain : 0);
        }

        void SendHealSpellLog(HealInfo healInfo, bool critical = false)
        {
            SpellHealLog spellHealLog = new();

            spellHealLog.TargetGUID = healInfo.GetTarget().GetGUID();
            spellHealLog.CasterGUID = healInfo.GetHealer().GetGUID();
            spellHealLog.SpellID = healInfo.GetSpellInfo().Id;
            spellHealLog.Health = healInfo.GetHeal();
            spellHealLog.OriginalHeal = healInfo.GetOriginalHeal();
            spellHealLog.OverHeal = healInfo.GetHeal() - healInfo.GetEffectiveHeal();
            spellHealLog.Absorbed = healInfo.GetAbsorb();
            spellHealLog.Crit = critical;

            spellHealLog.LogData.Initialize(healInfo.GetTarget());
            SendCombatLogMessage(spellHealLog);
        }

        public int HealBySpell(HealInfo healInfo, bool critical = false)
        {
            // calculate heal absorb and reduce healing
            CalcHealAbsorb(healInfo);
            DealHeal(healInfo);

            SendHealSpellLog(healInfo, critical);
            return healInfo.GetEffectiveHeal();
        }

        public void ApplyCastTimePercentMod(float val, bool apply)
        {
            if (val > 0.0f)
            {
                ApplyPercentModUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModCastingSpeed), val, !apply);
                ApplyPercentModUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModSpellHaste), val, !apply);
                ApplyPercentModUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModHasteRegen), val, !apply);
            }
            else
            {
                ApplyPercentModUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModCastingSpeed), -val, apply);
                ApplyPercentModUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModSpellHaste), -val, apply);
                ApplyPercentModUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModHasteRegen), -val, apply);
            }
        }

        public void RemoveAllGroupBuffsFromCaster(ObjectGuid casterGUID)
        {
            foreach (var pair in GetOwnedAurasCopy())
            {
                Aura aura = pair.Value;
                if (aura.GetCasterGUID() == casterGUID && aura.GetSpellInfo().IsGroupBuff())
                    RemoveOwnedAura(pair);
            }
        }

        public void DelayOwnedAuras(int spellId, ObjectGuid caster, Milliseconds delaytime)
        {
            foreach (var aura in m_ownedAuras[spellId])
            {
                if (caster.IsEmpty() || aura.GetCasterGUID() == caster)
                {
                    if (aura.GetDuration() < delaytime)
                        aura.SetDuration(Milliseconds.Zero);
                    else
                        aura.SetDuration(aura.GetDuration() - delaytime);

                    // update for out of range group members (on 1 slot use)
                    aura.SetNeedClientUpdateForTargets();
                }
            }
        }

        public void CalculateSpellDamageTaken(SpellNonMeleeDamage damageInfo, int damage, SpellInfo spellInfo, WeaponAttackType attackType = WeaponAttackType.BaseAttack, bool crit = false, bool blocked = false, Spell spell = null)
        {
            if (damage < 0)
                return;

            Unit victim = damageInfo.target;
            if (victim == null || !victim.IsAlive())
                return;

            SpellSchoolMask damageSchoolMask = damageInfo.schoolMask;

            // Spells with SPELL_ATTR4_IGNORE_DAMAGE_TAKEN_MODIFIERS ignore resilience because their damage is based off another spell's damage.
            if (!spellInfo.HasAttribute(SpellAttr4.IgnoreDamageTakenModifiers))
            {
                if (IsDamageReducedByArmor(damageSchoolMask, spellInfo))
                    damage = CalcArmorReducedDamage(damageInfo.attacker, victim, damage, spellInfo, attackType);

                // Per-school calc
                switch (spellInfo.DmgClass)
                {
                    // Melee and Ranged Spells
                    case SpellDmgClass.Ranged:
                    case SpellDmgClass.Melee:
                    {
                        if (crit)
                        {
                            damageInfo.HitInfo |= HitInfo.CriticalHit;

                            // Calculate crit bonus
                            uint crit_bonus = (uint)damage;
                            // Apply crit_damage bonus for melee spells
                            Player modOwner = GetSpellModOwner();
                            if (modOwner != null)
                                modOwner.ApplySpellMod(spellInfo, SpellModOp.CritDamageAndHealing, ref crit_bonus);
                            damage += (int)crit_bonus;

                            // Increase crit damage from SPELL_AURA_MOD_CRIT_DAMAGE_BONUS
                            float critPctDamageMod = (GetTotalAuraMultiplierByMiscMask(AuraType.ModCritDamageBonus, (uint)spellInfo.GetSchoolMask()) - 1.0f) * 100;

                            if (critPctDamageMod != 0)
                                MathFunctions.AddPct(ref damage, critPctDamageMod);
                        }

                        // Spell weapon based damage CAN BE crit & blocked at same time
                        if (blocked)
                        {
                            // double blocked amount if block is critical
                            int value = victim.GetShieldBlockValue();
                            if (victim.IsBlockCritical())
                                value *= 2;
                            damageInfo.blocked = value;
                            if (damage <= damageInfo.blocked)
                            {
                                damageInfo.blocked = damage;
                                damageInfo.fullBlock = true;
                            }
                            damage -= damageInfo.blocked;
                        }

                        if (CanApplyResilience())
                            ApplyResilience(victim, ref damage);

                        break;
                    }
                    // Magical Attacks
                    case SpellDmgClass.None:
                    case SpellDmgClass.Magic:
                    {
                        // If crit add critical bonus
                        if (crit)
                        {
                            damageInfo.HitInfo |= HitInfo.CriticalHit;
                            damage = SpellCriticalDamageBonus(this, spellInfo, damage, victim);
                        }

                        if (CanApplyResilience())
                            ApplyResilience(victim, ref damage);

                        break;
                    }
                    default:
                        break;
                }
            }

            // Script Hook For CalculateSpellDamageTaken -- Allow scripts to change the Damage post class mitigation calculations
            Global.ScriptMgr.ModifySpellDamageTaken(damageInfo.target, damageInfo.attacker, ref damage, spellInfo);

            // Calculate absorb resist
            if (damage < 0)
                damage = 0;

            damageInfo.damage = damage;
            damageInfo.originalDamage = damage;
            DamageInfo dmgInfo = new(damageInfo, DamageEffectType.SpellDirect, WeaponAttackType.BaseAttack, ProcFlagsHit.None);
            CalcAbsorbResist(dmgInfo, spell);
            damageInfo.absorb = dmgInfo.GetAbsorb();
            damageInfo.resist = dmgInfo.GetResist();

            if (damageInfo.absorb != 0)
                damageInfo.HitInfo |= (damageInfo.damage - damageInfo.absorb == 0 ? HitInfo.FullAbsorb : HitInfo.PartialAbsorb);

            if (damageInfo.resist != 0)
                damageInfo.HitInfo |= (damageInfo.damage - damageInfo.resist == 0 ? HitInfo.FullResist : HitInfo.PartialResist);

            damageInfo.damage = dmgInfo.GetDamage();
        }

        public void DealSpellDamage(SpellNonMeleeDamage damageInfo, bool durabilityLoss)
        {
            if (damageInfo == null)
                return;

            Unit victim = damageInfo.target;
            if (victim == null)
                return;

            if (!victim.IsAlive() || victim.HasUnitState(UnitState.InFlight) || (victim.IsTypeId(TypeId.Unit) && victim.ToCreature().IsEvadingAttacks()))
                return;

            if (damageInfo.Spell == null)
            {
                Log.outDebug(LogFilter.Unit, "Unit.DealSpellDamage has no spell");
                return;
            }

            // Call default DealDamage
            CleanDamage cleanDamage = new(damageInfo.cleanDamage, damageInfo.absorb, WeaponAttackType.BaseAttack, MeleeHitOutcome.Normal);
            DealDamage(this, victim, damageInfo.damage, cleanDamage, DamageEffectType.SpellDirect, damageInfo.schoolMask, damageInfo.Spell, durabilityLoss);
        }

        public void SendSpellNonMeleeDamageLog(SpellNonMeleeDamage log)
        {
            SpellNonMeleeDamageLog packet = new();
            packet.Me = log.target.GetGUID();
            packet.CasterGUID = log.attacker != null ? log.attacker.GetGUID() : ObjectGuid.Empty;
            packet.CastID = log.castId;
            packet.SpellID = (log.Spell != null ? log.Spell.Id : 0);
            packet.Visual = log.SpellVisual;
            packet.Damage = log.damage;
            packet.OriginalDamage = log.originalDamage;
            if (log.damage > log.preHitHealth)
                packet.Overkill = (log.damage - log.preHitHealth);
            else
                packet.Overkill = -1;

            packet.SchoolMask = log.schoolMask;
            packet.Absorbed = log.absorb;
            packet.Resisted = log.resist;
            packet.ShieldBlock = log.blocked;
            packet.Periodic = log.periodicLog;
            packet.Flags = log.HitInfo;

            ContentTuningParams contentTuningParams = new();
            if (contentTuningParams.GenerateDataForUnits(log.attacker, log.target))
                packet.ContentTuning = contentTuningParams;

            SendCombatLogMessage(packet);
        }

        public void SendPeriodicAuraLog(SpellPeriodicAuraLogInfo info)
        {
            AuraEffect aura = info.auraEff;

            SpellPeriodicAuraLog data = new();
            data.TargetGUID = GetGUID();
            data.CasterGUID = aura.GetCasterGUID();
            data.SpellID = aura.GetId();
            data.LogData.Initialize(this);

            SpellPeriodicAuraLog.SpellLogEffect spellLogEffect = new();
            spellLogEffect.Effect = aura.GetAuraType();
            spellLogEffect.Amount = info.damage;
            spellLogEffect.OriginalDamage = info.originalDamage;
            spellLogEffect.OverHealOrKill = info.overDamage;
            spellLogEffect.SchoolMaskOrPower = aura.GetSpellInfo().GetSchoolMask();
            spellLogEffect.AbsorbedOrAmplitude = info.absorb;
            spellLogEffect.Resisted = info.resist;
            spellLogEffect.Crit = info.critical;
            // @todo: implement debug info

            ContentTuningParams contentTuningParams = new();
            Unit caster = Global.ObjAccessor.GetUnit(this, aura.GetCasterGUID());
            if (caster != null && contentTuningParams.GenerateDataForUnits(caster, this))
                spellLogEffect.ContentTuning = contentTuningParams;

            data.Effects.Add(spellLogEffect);

            SendCombatLogMessage(data);
        }

        void SendSpellDamageResist(Unit target, int spellId)
        {
            ProcResist procResist = new();
            procResist.Caster = GetGUID();
            procResist.SpellID = spellId;
            procResist.Target = target.GetGUID();
            SendMessageToSet(procResist, true);
        }

        public void SendSpellDamageImmune(Unit target, int spellId, bool isPeriodic)
        {
            SpellOrDamageImmune spellOrDamageImmune = new();
            spellOrDamageImmune.CasterGUID = GetGUID();
            spellOrDamageImmune.VictimGUID = target.GetGUID();
            spellOrDamageImmune.SpellID = spellId;
            spellOrDamageImmune.IsPeriodic = isPeriodic;
            SendMessageToSet(spellOrDamageImmune, true);
        }

        public void SendSpellInstakillLog(int spellId, Unit caster, Unit target = null)
        {
            SpellInstakillLog spellInstakillLog = new();
            spellInstakillLog.Caster = caster.GetGUID();
            spellInstakillLog.Target = target != null ? target.GetGUID() : caster.GetGUID();
            spellInstakillLog.SpellID = spellId;
            SendMessageToSet(spellInstakillLog, false);
        }

        public void RemoveAurasOnEvade()
        {
            if (IsCharmedOwnedByPlayerOrPlayer()) // if it is a player owned creature it should not remove the aura
                return;

            // don't remove vehicle auras, passengers aren't supposed to drop off the vehicle
            // don't remove clone caster on evade (to be verified)
            bool evadeAuraCheck(Aura aura)
            {
                if (aura.HasEffectType(AuraType.ControlVehicle))
                    return false;

                if (aura.HasEffectType(AuraType.CloneCaster))
                    return false;

                if (aura.GetSpellInfo().HasAttribute(SpellAttr1.AuraStaysAfterCombat))
                    return false;

                return true;
            }

            bool evadeAuraApplicationCheck(AuraApplication aurApp)
            {
                return evadeAuraCheck(aurApp.GetBase());
            }

            RemoveAppliedAuras(evadeAuraApplicationCheck);
            RemoveOwnedAuras(evadeAuraCheck);
        }

        public void RemoveAllAurasOnDeath()
        {
            // used just after dieing to remove all visible auras
            // and disable the mods for the passive ones
            foreach (var app in GetAppliedAurasCopy())
            {
                if (app.Value == null)
                    continue;

                Aura aura = app.Value.GetBase();
                if (!aura.IsPassive() && !aura.IsDeathPersistent())
                    _UnapplyAura(app, AuraRemoveMode.Death);
            }

            foreach (var pair in GetOwnedAurasCopy())
            {
                Aura aura = pair.Value;
                if (pair.Value == null)
                    continue;

                if (!aura.IsPassive() && !aura.IsDeathPersistent())
                    RemoveOwnedAura(pair, AuraRemoveMode.Death);
            }
        }

        public void RemoveMovementImpairingAuras(bool withRoot)
        {
            if (withRoot)
                RemoveAurasWithMechanic(1 << (int)Mechanics.Root, AuraRemoveMode.Default, 0, true);

            RemoveAurasWithMechanic(1 << (int)Mechanics.Snare, AuraRemoveMode.Default, 0, false);
        }

        public void RemoveAllAurasRequiringDeadTarget()
        {
            foreach (var app in GetAppliedAurasCopy())
            {
                Aura aura = app.Value.GetBase();
                if (!aura.IsPassive() && aura.GetSpellInfo().IsRequiringDeadTarget())
                    _UnapplyAura(app, AuraRemoveMode.Default);
            }

            foreach (var aura in GetOwnedAurasCopy())
            {
                if (!aura.Value.IsPassive() && aura.Value.GetSpellInfo().IsRequiringDeadTarget())
                    RemoveOwnedAura(aura, AuraRemoveMode.Default);
            }
        }

        public AuraEffect IsScriptOverriden(SpellInfo spell, int script)
        {
            foreach (var eff in GetAuraEffectsByType(AuraType.OverrideClassScripts))
            {
                if (eff.GetMiscValue() == script)
                {
                    if (eff.IsAffectingSpell(spell))
                        return eff;
                }
            }

            return null;
        }

        public virtual bool IsAffectedByDiminishingReturns() { return (GetCharmerOrOwnerPlayerOrPlayerItself() != null); }

        public DiminishingLevels GetDiminishing(DiminishingGroup group)
        {
            DiminishingReturn diminish = m_Diminishing[(int)group];
            if (diminish.HitCount == 0)
                return DiminishingLevels.Level1;

            // If last spell was cast more than 18 seconds ago - reset level.
            if (diminish.Stack == 0 && LoopTime.Diff(diminish.HitTime) > (Seconds)18)
                return DiminishingLevels.Level1;

            return diminish.HitCount;
        }

        public void IncrDiminishing(SpellInfo auraSpellInfo)
        {
            DiminishingGroup group = auraSpellInfo.GetDiminishingReturnsGroupForSpell();
            DiminishingLevels currentLevel = GetDiminishing(group);
            DiminishingLevels maxLevel = auraSpellInfo.GetDiminishingReturnsMaxLevel();

            DiminishingReturn diminish = m_Diminishing[(int)group];
            if (currentLevel < maxLevel)
                diminish.HitCount = currentLevel + 1;
        }

        public bool ApplyDiminishingToDuration(SpellInfo auraSpellInfo, ref Milliseconds duration, WorldObject caster, DiminishingLevels previousLevel)
        {
            DiminishingGroup group = auraSpellInfo.GetDiminishingReturnsGroupForSpell();
            if (duration == -1 || group == DiminishingGroup.None)
                return true;

            Milliseconds limitDuration = auraSpellInfo.GetDiminishingReturnsLimitDuration();

            // test pet/charm masters instead pets/charmeds
            Unit targetOwner = GetCharmerOrOwner();
            Unit casterOwner = caster.GetCharmerOrOwner();

            if (limitDuration > 0 && duration > limitDuration)
            {
                Unit target = targetOwner ?? this;
                WorldObject source = casterOwner ?? caster;

                if (target.IsAffectedByDiminishingReturns() && source.IsPlayer())
                    duration = limitDuration;
            }

            float mod = 1.0f;
            switch (group)
            {
                case DiminishingGroup.Taunt:
                    if (IsTypeId(TypeId.Unit) 
                        && ToCreature().GetCreatureTemplate().FlagsExtra.HasAnyFlag(CreatureFlagsExtra.ObeysTauntDiminishingReturns))
                    {
                        DiminishingLevels diminish = previousLevel;
                        switch (diminish)
                        {
                            case DiminishingLevels.Level1:
                                break;
                            case DiminishingLevels.Level2:
                                mod = 0.65f;
                                break;
                            case DiminishingLevels.Level3:
                                mod = 0.4225f;
                                break;
                            case DiminishingLevels.Level4:
                                mod = 0.274625f;
                                break;
                            case DiminishingLevels.TauntImmune:
                                mod = 0.0f;
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                case DiminishingGroup.AOEKnockback:
                    if (auraSpellInfo.GetDiminishingReturnsGroupType() == DiminishingReturnsType.All ||
                        (auraSpellInfo.GetDiminishingReturnsGroupType() == DiminishingReturnsType.Player &&
                            (targetOwner != null ? targetOwner.IsAffectedByDiminishingReturns() : IsAffectedByDiminishingReturns())))
                    {
                        DiminishingLevels diminish = previousLevel;
                        switch (diminish)
                        {
                            case DiminishingLevels.Level1:
                                break;
                            case DiminishingLevels.Level2:
                                mod = 0.5f;
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                default:
                    if (auraSpellInfo.GetDiminishingReturnsGroupType() == DiminishingReturnsType.All ||
                        (auraSpellInfo.GetDiminishingReturnsGroupType() == DiminishingReturnsType.Player &&
                            (targetOwner != null ? targetOwner.IsAffectedByDiminishingReturns() : IsAffectedByDiminishingReturns())))
                    {
                        DiminishingLevels diminish = previousLevel;
                        switch (diminish)
                        {
                            case DiminishingLevels.Level1:
                                break;
                            case DiminishingLevels.Level2:
                                mod = 0.5f;
                                break;
                            case DiminishingLevels.Level3:
                                mod = 0.25f;
                                break;
                            case DiminishingLevels.Immune:
                                mod = 0.0f;
                                break;
                            default: break;
                        }
                    }
                    break;
            }

            duration = (Milliseconds)(duration * mod);
            return duration != 0;
        }

        public void ApplyDiminishingAura(DiminishingGroup group, bool apply)
        {
            // Checking for existing in the table
            DiminishingReturn diminish = m_Diminishing[(int)group];

            if (apply)
                ++diminish.Stack;
            else if (diminish.Stack != 0)
            {
                --diminish.Stack;

                // Remember time after last aura from group removed
                if (diminish.Stack == 0)
                    diminish.HitTime = LoopTime.RelativeTime;
            }
        }

        void ClearDiminishings()
        {
            for (int i = 0; i < (int)DiminishingGroup.Max; ++i)
                m_Diminishing[i].Clear();
        }

        // Interrupts
        public void InterruptNonMeleeSpells(bool withDelayed, int spell_id = 0, bool withInstant = true)
        {
            // generic spells are interrupted if they are not finished or delayed
            if (GetCurrentSpell(CurrentSpellTypes.Generic) != null && (spell_id == 0
                || m_currentSpells[CurrentSpellTypes.Generic].m_spellInfo.Id == spell_id))
            {
                InterruptSpell(CurrentSpellTypes.Generic, withDelayed, withInstant);
            }

            // autorepeat spells are interrupted if they are not finished or delayed
            if (GetCurrentSpell(CurrentSpellTypes.AutoRepeat) != null && (spell_id == 0
                || m_currentSpells[CurrentSpellTypes.AutoRepeat].m_spellInfo.Id == spell_id))
            {
                InterruptSpell(CurrentSpellTypes.AutoRepeat, withDelayed, withInstant);
            }

            // channeled spells are interrupted if they are not finished, even if they are delayed
            if (GetCurrentSpell(CurrentSpellTypes.Channeled) != null && (spell_id == 0
                || m_currentSpells[CurrentSpellTypes.Channeled].m_spellInfo.Id == spell_id))
            {
                InterruptSpell(CurrentSpellTypes.Channeled, true, true);
            }
        }

        public void InterruptSpell(CurrentSpellTypes spellType, bool withDelayed = true, bool withInstant = true)
        {
            Cypher.Assert(spellType < CurrentSpellTypes.Max);

            Log.outDebug(LogFilter.Unit, $"Interrupt spell for unit {GetEntry()}");
            Spell spell = m_currentSpells.LookupByKey(spellType);
            if (spell != null
                && (withDelayed || spell.GetState() != SpellState.Delayed)
                && (withInstant || spell.GetCastTime() > 0 || spell.GetState() == SpellState.Casting))
            {
                // for example, do not let self-stun aura interrupt itself
                if (!spell.IsInterruptable())
                    return;

                // send autorepeat cancel message for autorepeat spells
                if (spellType == CurrentSpellTypes.AutoRepeat)
                {
                    if (IsTypeId(TypeId.Player))
                        ToPlayer().SendAutoRepeatCancel(this);
                }

                if (spell.GetState() != SpellState.Finished)
                {
                    spell.Cancel();
                }
                else
                {
                    m_currentSpells[spellType] = null;
                    spell.SetReferencedFromCurrent(false);
                }

                if (IsCreature() && IsAIEnabled())
                    ToCreature().GetAI().OnSpellFailed(spell.GetSpellInfo());
            }
        }

        public void UpdateInterruptMask()
        {
            m_interruptMask = SpellAuraInterruptFlags.None;
            m_interruptMask2 = SpellAuraInterruptFlags2.None;
            foreach (AuraApplication aurApp in m_interruptableAuras)
            {
                m_interruptMask |= aurApp.GetBase().GetSpellInfo().AuraInterruptFlags;
                m_interruptMask2 |= aurApp.GetBase().GetSpellInfo().AuraInterruptFlags2;
            }

            Spell spell = GetCurrentSpell(CurrentSpellTypes.Channeled);
            if (spell != null)
            {
                if (spell.GetState() == SpellState.Casting)
                {
                    m_interruptMask |= spell.m_spellInfo.ChannelInterruptFlags;
                    m_interruptMask2 |= spell.m_spellInfo.ChannelInterruptFlags2;
                }
            }
        }

        // Auras
        public List<Aura> GetSingleCastAuras() { return m_scAuras; }

        public List<KeyValuePair<int, Aura>> GetOwnedAurasCopy()
        {
            return m_ownedAuras.ToList();
        }

        public IReadOnlyMultiMap<int, Aura> GetOwnedAuras()
        {
            return m_ownedAuras;
        }

        public List<KeyValuePair<int, AuraApplication>> GetAppliedAurasCopy()
        {
            return m_appliedAuras.ToList();
        }

        public IReadOnlyMultiMap<int, AuraApplication> GetAppliedAuras()
        {
            return m_appliedAuras;
        }

        public Aura AddAura(int spellId, Unit target)
        {
            if (target == null)
                return null;

            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, GetMap().GetDifficultyID());
            if (spellInfo == null)
                return null;

            return AddAura(spellInfo, SpellConst.MaxEffectMask, target);
        }

        public Aura AddAura(SpellInfo spellInfo, uint effMask, Unit target)
        {
            if (spellInfo == null)
                return null;

            if (!target.IsAlive() && !spellInfo.IsPassive() && !spellInfo.HasAttribute(SpellAttr2.AllowDeadTarget))
                return null;

            if (target.IsImmunedToSpell(spellInfo, this))
                return null;

            foreach (var spellEffectInfo in spellInfo.GetEffects())
            {
                if ((effMask & (1 << spellEffectInfo.EffectIndex)) == 0)
                    continue;

                if (target.IsImmunedToSpellEffect(spellInfo, spellEffectInfo, this))
                    effMask &= ~(1u << spellEffectInfo.EffectIndex);
            }

            if (effMask == 0)
                return null;

            ObjectGuid castId = ObjectGuid.Create(HighGuid.Cast, SpellCastSource.Normal, GetMapId(), spellInfo.Id, GetMap().GenerateLowGuid(HighGuid.Cast));

            AuraCreateInfo createInfo = new(castId, spellInfo, GetMap().GetDifficultyID(), effMask, target);
            createInfo.SetCaster(this);

            Aura aura = Aura.TryRefreshStackOrCreate(createInfo);
            if (aura != null)
            {
                aura.ApplyForTargets();
                return aura;
            }

            return null;
        }

        public void HandleSpellClick(Unit clicker, sbyte seatId = -1)
        {
            bool spellClickHandled = false;

            int spellClickEntry = GetVehicleKit() != null ? GetVehicleKit().GetCreatureEntry() : GetEntry();
            TriggerCastFlags flags = GetVehicleKit() != null ? TriggerCastFlags.IgnoreCasterMountedOrOnVehicle : TriggerCastFlags.None;

            var clickBounds = Global.ObjectMgr.GetSpellClickInfoMapBounds(spellClickEntry);
            foreach (var clickInfo in clickBounds)
            {
                //! First check simple relations from clicker to clickee
                if (!clickInfo.IsFitToRequirements(clicker, this))
                    continue;

                //! Check database conditions
                if (!Global.ConditionMgr.IsObjectMeetingSpellClickConditions(spellClickEntry, clickInfo.spellId, clicker, this))
                    continue;

                Unit caster = Convert.ToBoolean(clickInfo.castFlags & (byte)SpellClickCastFlags.CasterClicker) ? clicker : this;
                Unit target = Convert.ToBoolean(clickInfo.castFlags & (byte)SpellClickCastFlags.TargetClicker) ? clicker : this;
                ObjectGuid origCasterGUID = Convert.ToBoolean(clickInfo.castFlags & (byte)SpellClickCastFlags.OrigCasterOwner) ? GetOwnerGUID() : clicker.GetGUID();

                SpellInfo spellEntry = Global.SpellMgr.GetSpellInfo(clickInfo.spellId, caster.GetMap().GetDifficultyID());
                // if (!spellEntry) should be checked at npc_spellclick load

                if (seatId > -1)
                {
                    byte i = 0;
                    bool valid = false;
                    foreach (var spellEffectInfo in spellEntry.GetEffects())
                    {
                        if (spellEffectInfo.ApplyAuraName == AuraType.ControlVehicle)
                        {
                            valid = true;
                            break;
                        }
                        ++i;
                    }

                    if (!valid)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spell {clickInfo.spellId} specified in npc_spellclick_spells " +
                            $"is not a valid vehicle enter aura!");

                        continue;
                    }

                    if (IsInMap(caster))
                    {
                        CastSpellExtraArgs args = new(flags);
                        args.OriginalCaster = origCasterGUID;
                        args.AddSpellMod(SpellValueMod.BasePoint0 + i, seatId + 1);
                        caster.CastSpell(target, clickInfo.spellId, args);
                    }
                    else    // This can happen during Player._LoadAuras
                    {
                        int[] bp = new int[SpellConst.MaxEffects];
                        foreach (var spellEffectInfo in spellEntry.GetEffects())
                            bp[spellEffectInfo.EffectIndex] = (int)spellEffectInfo.BasePoints;

                        bp[i] = seatId;

                        AuraCreateInfo createInfo = 
                            new(ObjectGuid.Create(HighGuid.Cast, SpellCastSource.Normal, GetMapId(), spellEntry.Id, 
                            GetMap().GenerateLowGuid(HighGuid.Cast)), spellEntry, GetMap().GetDifficultyID(), SpellConst.MaxEffectMask, this);

                        createInfo.SetCaster(clicker);
                        createInfo.SetBaseAmount(bp);
                        createInfo.SetCasterGUID(origCasterGUID);

                        Aura.TryRefreshStackOrCreate(createInfo);
                    }
                }
                else
                {
                    if (IsInMap(caster))
                        caster.CastSpell(target, spellEntry.Id, new CastSpellExtraArgs().SetOriginalCaster(origCasterGUID));
                    else
                    {
                        AuraCreateInfo createInfo = 
                            new(ObjectGuid.Create(HighGuid.Cast, SpellCastSource.Normal, GetMapId(), spellEntry.Id, 
                            GetMap().GenerateLowGuid(HighGuid.Cast)), spellEntry, GetMap().GetDifficultyID(), SpellConst.MaxEffectMask, this);
                        
                        createInfo.SetCaster(clicker);
                        createInfo.SetCasterGUID(origCasterGUID);

                        Aura.TryRefreshStackOrCreate(createInfo);
                    }
                }

                spellClickHandled = true;
            }

            Creature creature = ToCreature();
            if (creature != null && creature.IsAIEnabled())
                creature.GetAI().OnSpellClick(clicker, ref spellClickHandled);
        }

        public bool HasAura(int spellId, ObjectGuid casterGUID = default, ObjectGuid itemCasterGUID = default, uint reqEffMask = 0)
        {
            return GetAuraApplication(spellId, casterGUID, itemCasterGUID, reqEffMask) != null;
        }

        public bool HasAura(Func<Aura, bool> predicate)
        {
            return GetAuraApplication(predicate) != null;
        }

        public bool HasAuraEffect(int spellId, int effIndex, ObjectGuid casterGUID = default)
        {
            foreach (var aura in m_appliedAuras[spellId])
            {
                    if (aura.HasEffect(effIndex) && (casterGUID.IsEmpty() || aura.GetBase().GetCasterGUID() == casterGUID))
                        return true;
                }

            return false;
        }

        public bool HasAuraWithMechanic(ulong mechanicMask)
        {
            foreach (var pair in GetAppliedAuras())
            {
                SpellInfo spellInfo = pair.Value.GetBase().GetSpellInfo();
                if (spellInfo.Mechanic != 0 && mechanicMask.HasAnyFlag(1ul << (int)spellInfo.Mechanic))
                    return true;

                foreach (var spellEffectInfo in spellInfo.GetEffects())
                {
                    if (spellEffectInfo != null && pair.Value.HasEffect(spellEffectInfo.EffectIndex) 
                        && spellEffectInfo.IsEffect() && spellEffectInfo.Mechanic != 0)
                    {
                        if (mechanicMask.HasAnyFlag(1ul << (int)spellEffectInfo.Mechanic))
                            return true;
                    }
                }
            }

            return false;
        }

        public bool HasAuraType(AuraType auraType)
        {
            return m_modAuras.ContainsKey(auraType);
        }

        public bool HasAuraTypeWithCaster(AuraType auraType, ObjectGuid caster)
        {
            foreach (var auraEffect in GetAuraEffectsByType(auraType))
            {
                if (caster == auraEffect.GetCasterGUID())
                    return true;
            }

            return false;
        }

        public bool HasAuraTypeWithMiscvalue(AuraType auraType, int miscvalue)
        {
            foreach (var auraEffect in GetAuraEffectsByType(auraType))
            {
                if (miscvalue == auraEffect.GetMiscValue())
                    return true;
            }

            return false;
        }

        public bool HasAuraTypeWithAffectMask(AuraType auraType, SpellInfo affectedSpell)
        {
            foreach (var auraEffect in GetAuraEffectsByType(auraType))
            {
                if (auraEffect.IsAffectingSpell(affectedSpell))
                    return true;
            }

            return false;
        }

        public bool HasAuraTypeWithValue(AuraType auraType, int value)
        {
            foreach (var auraEffect in GetAuraEffectsByType(auraType))
            {
                if (value == auraEffect.GetAmount())
                    return true;
            }

            return false;
        }

        public bool HasAuraTypeWithTriggerSpell(AuraType auratype, int triggerSpell)
        {
            foreach (var aura in GetAuraEffectsByType(auratype))
            {
                if (aura.GetSpellEffectInfo().TriggerSpell == triggerSpell)
                    return true;
            }

            return false;
        }

        public bool HasNegativeAuraWithInterruptFlag(SpellAuraInterruptFlags flag, ObjectGuid guid = default)
        {
            if (!HasInterruptFlag(flag))
                return false;

            foreach (var aura in m_interruptableAuras)
            {
                if (!aura.IsPositive() && aura.GetBase().GetSpellInfo().HasAuraInterruptFlag(flag)
                    && (guid.IsEmpty() || aura.GetBase().GetCasterGUID() == guid))
                    return true;
            }

            return false;
        }

        public bool HasNegativeAuraWithInterruptFlag(SpellAuraInterruptFlags2 flag, ObjectGuid guid = default)
        {
            if (!HasInterruptFlag(flag))
                return false;

            foreach (var aura in m_interruptableAuras)
            {
                if (!aura.IsPositive() && aura.GetBase().GetSpellInfo().HasAuraInterruptFlag(flag)
                    && (guid.IsEmpty() || aura.GetBase().GetCasterGUID() == guid))
                    return true;
            }

            return false;
        }

        public bool HasStrongerAuraWithDR(SpellInfo auraSpellInfo, Unit caster)
        {
            DiminishingGroup diminishGroup = auraSpellInfo.GetDiminishingReturnsGroupForSpell();
            DiminishingLevels level = GetDiminishing(diminishGroup);
            foreach (var itr in GetAppliedAuras())
            {
                SpellInfo spellInfo = itr.Value.GetBase().GetSpellInfo();
                if (spellInfo.GetDiminishingReturnsGroupForSpell() != diminishGroup)
                    continue;

                Milliseconds existingDuration = itr.Value.GetBase().GetDuration();
                Milliseconds newDuration = auraSpellInfo.GetMaxDuration();
                ApplyDiminishingToDuration(auraSpellInfo, ref newDuration, caster, level);
                if (newDuration > 0 && newDuration < existingDuration)
                    return true;
            }

            return false;
        }
        
        public int GetAuraCount(int spellId)
        {
            int count = 0;
            var range = m_appliedAuras[spellId];
            foreach (var aura in range)
            {
                if (aura.GetBase().GetStackAmount() == 0)
                    ++count;
                else
                    count += aura.GetBase().GetStackAmount();
            }

            return count;
        }

        public Aura GetAuraOfRankedSpell(int spellId, ObjectGuid casterGUID = default, ObjectGuid itemCasterGUID = default, uint reqEffMask = 0)
        {
            var aurApp = GetAuraApplicationOfRankedSpell(spellId, casterGUID, itemCasterGUID, reqEffMask);
            return aurApp?.GetBase();
        }

        public AuraApplication GetAuraApplicationOfRankedSpell(int spellId, ObjectGuid casterGUID = default, ObjectGuid itemCasterGUID = default, uint reqEffMask = 0, AuraApplication except = null)
        {
            int rankSpell = Global.SpellMgr.GetFirstSpellInChain(spellId);
            while (rankSpell != 0)
            {
                AuraApplication aurApp = GetAuraApplication(rankSpell, casterGUID, itemCasterGUID, reqEffMask, except);
                if (aurApp != null)
                    return aurApp;

                rankSpell = Global.SpellMgr.GetNextSpellInChain(rankSpell);
            }

            return null;
        }

        public List<DispelableAura> GetDispellableAuraList(WorldObject caster, uint dispelMask, bool isReflect = false)
        {
            List<DispelableAura> dispelList = new();

            var auras = GetOwnedAuras();
            foreach (var pair in auras)
            {
                Aura aura = pair.Value;
                AuraApplication aurApp = aura.GetApplicationOfTarget(GetGUID());
                if (aurApp == null)
                    continue;

                // don't try to remove passive auras
                if (aura.IsPassive())
                    continue;

                if (aura.GetSpellInfo().GetDispelMask().HasAnyFlag(dispelMask))
                {
                    // do not remove positive auras if friendly target
                    //               negative auras if non-friendly
                    // unless we're reflecting (dispeller eliminates one of it's benefitial buffs)
                    if (isReflect != (aurApp.IsPositive() == IsFriendlyTo(caster)))
                        continue;

                    // 2.4.3 Patch Notes: "Dispel effects will no longer attempt to remove effects that have 100% dispel resistance."
                    int chance = aura.CalcDispelChance(this, !IsFriendlyTo(caster));
                    if (chance == 0)
                        continue;

                    // The charges / stack amounts don't count towards the total number of auras that can be dispelled.
                    // Ie: A dispel on a target with 5 stacks of Winters Chill and a Polymorph has 1 / (1 + 1) . 50% Chance to dispell
                    // Polymorph instead of 1 / (5 + 1) . 16%.
                    bool dispelCharges = aura.GetSpellInfo().HasAttribute(SpellAttr7.DispelRemovesCharges);
                    byte charges = dispelCharges ? aura.GetCharges() : aura.GetStackAmount();
                    if (charges > 0)
                        dispelList.Add(new DispelableAura(aura, chance, charges));
                }
            }

            return dispelList;
        }

        bool IsInterruptFlagIgnoredForSpell(SpellAuraInterruptFlags flag, Unit unit, SpellInfo auraSpellInfo, SpellInfo interruptSource)
        {
            switch (flag)
            {
                case SpellAuraInterruptFlags.Moving:
                    return unit.CanCastSpellWhileMoving(auraSpellInfo);
                case SpellAuraInterruptFlags.Action:
                case SpellAuraInterruptFlags.ActionDelayed:
                    if (interruptSource != null)
                    {
                        if (interruptSource.HasAttribute(SpellAttr1.AllowWhileStealthed) && auraSpellInfo.Dispel == DispelType.Stealth)
                            return true;

                        if (interruptSource.HasAttribute(SpellAttr2.AllowWhileInvisible) && auraSpellInfo.Dispel == DispelType.Invisibility)
                            return true;
                    }
                    break;
                default:
                    break;
            }

            return false;
        }

        bool IsInterruptFlagIgnoredForSpell(SpellAuraInterruptFlags2 flag, Unit unit, SpellInfo auraSpellInfo, SpellInfo interruptSource)
        {
            return false;
        }

        public void RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags flag, SpellInfo source = null)
        {
            if (!HasInterruptFlag(flag))
                return;

            // interrupt auras
            for (var i = 0; i < m_interruptableAuras.Count; i++)
            {
                Aura aura = m_interruptableAuras[i].GetBase();

                if (aura.GetSpellInfo().HasAuraInterruptFlag(flag) 
                    && (source == null || aura.GetId() != source.Id) 
                    && !IsInterruptFlagIgnoredForSpell(flag, this, aura.GetSpellInfo(), source))
                {
                    uint removedAuras = m_removedAurasCount;
                    RemoveAura(aura, AuraRemoveMode.Interrupt);
                    if (m_removedAurasCount > removedAuras + 1)
                        i = 0;
                }
            }

            // interrupt channeled spell
            Spell spell = GetCurrentSpell(CurrentSpellTypes.Channeled);
            if (spell != null)
            {
                if (spell.GetState() == SpellState.Casting
                    && spell.GetSpellInfo().HasChannelInterruptFlag(flag)
                    && (source == null || spell.GetSpellInfo().Id != source.Id)
                    && !IsInterruptFlagIgnoredForSpell(flag, this, spell.GetSpellInfo(), source))
                {
                    InterruptNonMeleeSpells(false, spell.GetSpellInfo().Id);
                }
            }

            UpdateInterruptMask();
        }

        public void RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2 flag, SpellInfo source = null)
        {
            if (!HasInterruptFlag(flag))
                return;

            // interrupt auras
            for (var i = 0; i < m_interruptableAuras.Count; i++)
            {
                Aura aura = m_interruptableAuras[i].GetBase();

                if (aura.GetSpellInfo().HasAuraInterruptFlag(flag) 
                    && (source == null || aura.GetId() != source.Id) 
                    && !IsInterruptFlagIgnoredForSpell(flag, this, aura.GetSpellInfo(), source))
                {
                    uint removedAuras = m_removedAurasCount;
                    RemoveAura(aura, AuraRemoveMode.Interrupt);
                    if (m_removedAurasCount > removedAuras + 1)
                        i = 0;
                }
            }

            // interrupt channeled spell
            Spell spell = GetCurrentSpell(CurrentSpellTypes.Channeled);
            if (spell != null)
            {
                if (spell.GetState() == SpellState.Casting
                    && spell.GetSpellInfo().HasChannelInterruptFlag(flag)
                    && (source == null || spell.GetSpellInfo().Id != source.Id)
                    && !IsInterruptFlagIgnoredForSpell(flag, this, spell.GetSpellInfo(), source))
                    InterruptNonMeleeSpells(false);
            }

            UpdateInterruptMask();
        }

        public void RemoveAurasWithMechanic(ulong mechanicMaskToRemove, AuraRemoveMode removeMode = AuraRemoveMode.Default, int exceptSpellId = 0, bool withEffectMechanics = false)
        {
            List<Aura> aurasToUpdateTargets = new();
            RemoveAppliedAuras(aurApp =>
            {
                Aura aura = aurApp.GetBase();
                if (exceptSpellId != 0 && aura.GetId() == exceptSpellId)
                    return false;

                ulong appliedMechanicMask = aura.GetSpellInfo().GetSpellMechanicMaskByEffectMask(aurApp.GetEffectMask());
                if ((appliedMechanicMask & mechanicMaskToRemove) == 0)
                    return false;

                // spell mechanic matches required mask for removal
                if (((1ul << (int)aura.GetSpellInfo().Mechanic) & mechanicMaskToRemove) != 0 || withEffectMechanics)
                    return true;

                // effect mechanic matches required mask for removal - don't remove, only update targets
                aurasToUpdateTargets.Add(aura);
                return false;
            }, removeMode);

            foreach (Aura aura in aurasToUpdateTargets)
            {
                aura.UpdateTargetMap(aura.GetCaster());

                // Fully remove the aura if all effects were removed
                if (!aura.IsPassive() && aura.GetOwner() == this && aura.GetApplicationOfTarget(GetGUID()) == null)
                    aura.Remove(removeMode);
            }
        }

        public void RemoveAurasDueToSpellBySteal(int spellId, ObjectGuid casterGUID, WorldObject stealer, int stolenCharges = 1)
        {
            foreach (var aura in m_ownedAuras[spellId].ToList())
            {
                if (aura.GetCasterGUID() == casterGUID)
                {
                    int[] damage = new int[SpellConst.MaxEffects];
                    int[] baseDamage = new int[SpellConst.MaxEffects];
                    uint effMask = 0;
                    uint recalculateMask = 0;
                    Unit caster = aura.GetCaster();
                    for (byte i = 0; i < SpellConst.MaxEffects; ++i)
                    {
                        if (aura.GetEffect(i) != null)
                        {
                            baseDamage[i] = aura.GetEffect(i).GetBaseAmount();
                            damage[i] = aura.GetEffect(i).GetAmount();
                            effMask |= 1u << i;
                            if (aura.GetEffect(i).CanBeRecalculated())
                                recalculateMask |= 1u << i;
                        }
                        else
                        {
                            baseDamage[i] = 0;
                            damage[i] = 0;
                        }
                    }

                    bool stealCharge = aura.GetSpellInfo().HasAttribute(SpellAttr7.DispelRemovesCharges);
                    // Cast duration to unsigned to prevent permanent aura's such as Righteous Fury being permanently added to caster
                    Milliseconds dur = Time.Min((Minutes)2, aura.GetDuration());

                    Unit unitStealer = stealer.ToUnit();
                    if (unitStealer != null)
                    {
                        Aura oldAura = unitStealer.GetAura(aura.GetId(), aura.GetCasterGUID());
                        if (oldAura != null)
                        {
                            if (stealCharge)
                                oldAura.ModCharges(stolenCharges);
                            else
                                oldAura.ModStackAmount(stolenCharges);
                            oldAura.SetDuration(dur);
                        }
                        else
                        {
                            // single target state must be removed before aura creation to preserve existing single target aura
                            if (aura.IsSingleTarget())
                                aura.UnregisterSingleTarget();

                            AuraCreateInfo createInfo = new(aura.GetCastId(), aura.GetSpellInfo(), aura.GetCastDifficulty(), effMask, stealer);
                            createInfo.SetCasterGUID(aura.GetCasterGUID());
                            createInfo.SetBaseAmount(baseDamage);

                            Aura newAura = Aura.TryRefreshStackOrCreate(createInfo);
                            if (newAura != null)
                            {
                                // created aura must not be single target aura, so stealer won't loose it on recast
                                if (newAura.IsSingleTarget())
                                {
                                    newAura.UnregisterSingleTarget();
                                    // bring back single target aura status to the old aura
                                    aura.SetIsSingleTarget(true);
                                    caster.GetSingleCastAuras().Add(aura);
                                }
                                // FIXME: using aura.GetMaxDuration() maybe not blizzlike but it fixes stealing of spells like Innervate
                                newAura.SetLoadedState(aura.GetMaxDuration(), dur, stealCharge ? stolenCharges : aura.GetCharges(), (byte)stolenCharges, recalculateMask, damage);
                                newAura.ApplyForTargets();
                            }
                        }
                    }

                    if (stealCharge)
                        aura.ModCharges(-stolenCharges, AuraRemoveMode.EnemySpell);
                    else
                        aura.ModStackAmount(-stolenCharges, AuraRemoveMode.EnemySpell);

                    return;
                }
            }
        }

        public void RemoveAurasDueToItemSpell(int spellId, ObjectGuid castItemGuid)
        {
            foreach (var app in m_appliedAuras[spellId].ToList())
            {
                if (app.GetBase().GetCastItemGUID() == castItemGuid)
                {
                    RemoveAura(app);
                }
            }
        }

        public void RemoveAurasByType(AuraType auraType, ObjectGuid casterGUID = default, Aura except = null, bool negative = true, bool positive = true)
        {
            foreach (var eff in GetAuraEffectsByTypeCopy(auraType))
            {
                Aura aura = eff.GetBase();
                AuraApplication aurApp = aura.GetApplicationOfTarget(GetGUID());

                if (aura != except && (casterGUID.IsEmpty() || aura.GetCasterGUID() == casterGUID)
                    && ((negative && !aurApp.IsPositive()) || (positive && aurApp.IsPositive())))
                {
                    RemoveAura(aurApp);
                }
            }
        }

        public void RemoveNotOwnSingleTargetAuras(bool onPhaseChange = false)
        {
            // Iterate m_ownedAuras - aura is marked as single target in Unit::AddAura (and pushed to m_ownedAuras).
            // m_appliedAuras will NOT contain the aura before first Unit::Update after adding it to m_ownedAuras.
            // Quickly removing such an aura will lead to it not being unregistered from caster's single cast auras container
            // leading to assertion failures if the aura was cast on a player that can
            // (and is changing map at the point where this function is called).
            // Such situation occurs when player is logging in inside an instance and fails the entry check for any reason.
            // The aura that was loaded from db (indirectly, via linked casts) gets removed before it has a Chance
            // to register in m_appliedAuras
            foreach (var pair in GetOwnedAurasCopy())
            {
                Aura aura = pair.Value;

                if (aura.GetCasterGUID() != GetGUID() && aura.IsSingleTarget())
                {
                    if (onPhaseChange)
                        RemoveOwnedAura(pair);
                    else
                    {
                        Unit caster = aura.GetCaster();
                        if (caster == null || !caster.InSamePhase(this))
                            RemoveOwnedAura(pair);
                    }
                }
            }

            // single target auras at other targets
            for (var i = 0; i < m_scAuras.Count; i++)
            {
                var aura = m_scAuras[i];
                if (aura.GetUnitOwner() != this && (!onPhaseChange || !aura.GetUnitOwner().InSamePhase(this)))
                    aura.Remove();
            }
        }

        // All aura base removes should go through this function!
        public void RemoveOwnedAura(KeyValuePair<int, Aura> keyValuePair, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            Aura aura = keyValuePair.Value;

            Cypher.Assert(!aura.IsRemoved());

            m_ownedAuras.Remove(keyValuePair);
            m_removedAuras.Add(aura);

            // Unregister single target aura
            if (aura.IsSingleTarget())
                aura.UnregisterSingleTarget();

            aura._Remove(removeMode);
        }

        public void RemoveOwnedAura(int spellId, ObjectGuid casterGUID = default, uint reqEffMask = 0, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            foreach (var pair in GetOwnedAurasCopy())
            {
                if (pair.Key != spellId)
                    continue;

                if (((pair.Value.GetEffectMask() & reqEffMask) == reqEffMask) && (casterGUID.IsEmpty() || pair.Value.GetCasterGUID() == casterGUID))
                    RemoveOwnedAura(pair, removeMode);
            }
        }

        public void RemoveOwnedAura(Aura auraToRemove, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            if (auraToRemove.IsRemoved())
                return;

            Cypher.Assert(auraToRemove.GetOwner() == this);

            if (removeMode == AuraRemoveMode.None)
            {
                Log.outError(LogFilter.Spells, 
                    $"Unit.RemoveOwnedAura() called with unallowed removeMode AURA_REMOVE_NONE, spellId {auraToRemove.GetId()}");
                return;
            }

            int spellId = auraToRemove.GetId();
            foreach (var pair in GetOwnedAurasCopy())
            {
                if (pair.Key != spellId)
                    continue;

                if (pair.Value == auraToRemove)
                {
                    RemoveOwnedAura(pair, removeMode);
                    return;
                }
            }

            Cypher.Assert(false);
        }

        public void RemoveAurasDueToSpell(int spellId, ObjectGuid casterGUID = default, uint reqEffMask = 0, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            foreach (var pair in GetAppliedAurasCopy())
            {
                if (pair.Key != spellId)
                    continue;

                Aura aura = pair.Value.GetBase();
                if (((aura.GetEffectMask() & reqEffMask) == reqEffMask) && (casterGUID.IsEmpty() || aura.GetCasterGUID() == casterGUID))
                {
                    RemoveAura(pair, removeMode);
                }
            }
        }

        public void RemoveAurasDueToSpellByDispel(int spellId, int dispellerSpellId, ObjectGuid casterGUID, WorldObject dispeller, byte chargesRemoved = 1)
        {
            foreach (var pair in GetOwnedAurasCopy())
            {
                if (pair.Key != spellId)
                    continue;

                Aura aura = pair.Value;
                if (aura.GetCasterGUID() == casterGUID)
                {
                    DispelInfo dispelInfo = new(dispeller, dispellerSpellId, chargesRemoved);

                    // Call OnDispel hook on AuraScript
                    aura.CallScriptDispel(dispelInfo);

                    if (aura.GetSpellInfo().HasAttribute(SpellAttr7.DispelRemovesCharges))
                        aura.ModCharges(-dispelInfo.GetRemovedCharges(), AuraRemoveMode.EnemySpell);
                    else
                        aura.ModStackAmount(-dispelInfo.GetRemovedCharges(), AuraRemoveMode.EnemySpell);

                    // Call AfterDispel hook on AuraScript
                    aura.CallScriptAfterDispel(dispelInfo);
                    return;
                }
            }
        }

        public void RemoveAuraFromStack(int spellId, ObjectGuid casterGUID = default, AuraRemoveMode removeMode = AuraRemoveMode.Default, ushort num = 1)
        {
            foreach (var aura in m_ownedAuras[spellId].ToList())
            {
                if ((aura.GetAuraType() == AuraObjectType.Unit) && (casterGUID.IsEmpty() || aura.GetCasterGUID() == casterGUID))
                {
                    aura.ModStackAmount(-num, removeMode);
                    return;
                }
            }
        }

        public void RemoveAura(KeyValuePair<int, AuraApplication> appMap, AuraRemoveMode mode = AuraRemoveMode.Default)
        {
            var aurApp = appMap.Value;
            // Do not remove aura which is already being removed
            if (aurApp.HasRemoveMode())
                return;
            Aura aura = aurApp.GetBase();
            _UnapplyAura(appMap, mode);
            // Remove aura - for Area and Target auras
            if (aura.GetOwner() == this)
                aura.Remove(mode);
        }

        public void RemoveAura(int spellId, ObjectGuid caster = default, uint reqEffMask = 0, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            foreach (var iter in m_appliedAuras[spellId].ToList())
            {
                Aura aura = iter.GetBase();
                if (((aura.GetEffectMask() & reqEffMask) == reqEffMask) && (caster.IsEmpty() || aura.GetCasterGUID() == caster))
                {
                    RemoveAura(iter, removeMode);
                    return;
                }
            }
        }

        public void RemoveAura(AuraApplication aurApp, AuraRemoveMode mode = AuraRemoveMode.Default)
        {
            // we've special situation here, RemoveAura called while during aura removal
            // this kind of call is needed only when aura effect removal handler
            // or event triggered by it expects to remove
            // not yet removed effects of an aura
            if (aurApp.HasRemoveMode())
            {
                // remove remaining effects of an aura
                for (byte effectIndex = 0; effectIndex < SpellConst.MaxEffects; ++effectIndex)
                {
                    if (aurApp.HasEffect(effectIndex))
                        aurApp._HandleEffect(effectIndex, false);
                }
                return;
            }
            // no need to remove
            if (aurApp.GetBase().GetApplicationOfTarget(GetGUID()) != aurApp || aurApp.GetBase().IsRemoved())
                return;

            int spellId = aurApp.GetBase().GetId();

            foreach (var pair in m_appliedAuras.Where(p => p.Key == spellId).ToList())
            {
                if (aurApp == pair.Value)
                {
                    RemoveAura(pair, mode);
                    return;
                }
            }
        }

        public void RemoveAura(Aura aura, AuraRemoveMode mode = AuraRemoveMode.Default)
        {
            if (aura.IsRemoved())
                return;

            AuraApplication aurApp = aura.GetApplicationOfTarget(GetGUID());
            if (aurApp != null)
                RemoveAura(aurApp, mode);
        }

        public void RemoveAurasWithAttribute(SpellAttr0 flags)
        {
            foreach (var app in GetAppliedAurasCopy())
            {
                SpellInfo spell = app.Value.GetBase().GetSpellInfo();
                if (spell.HasAttribute(flags))
                    RemoveAura(app);
            }
        }

        public void RemoveAurasWithFamily(SpellFamilyNames family, FlagArray128 familyFlag, ObjectGuid casterGUID)
        {
            foreach (var pair in GetAppliedAurasCopy())
            {
                Aura aura = pair.Value.GetBase();
                if (casterGUID.IsEmpty() || aura.GetCasterGUID() == casterGUID)
                {
                    SpellInfo spell = aura.GetSpellInfo();
                    if (spell.SpellFamilyName == family && spell.SpellFamilyFlags & familyFlag)
                    {
                        RemoveAura(pair);
                        continue;
                    }
                }
            }
        }

        public void RemoveAppliedAuras(Func<AuraApplication, bool> check, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            foreach (var pair in GetAppliedAurasCopy())
            {
                if (check(pair.Value))
                    RemoveAura(pair, removeMode);
            }
        }

        public void RemoveOwnedAuras(Func<Aura, bool> check, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            foreach (var pair in GetOwnedAurasCopy())
            {
                if (check(pair.Value))
                    RemoveOwnedAura(pair, removeMode);
            }
        }

        void RemoveAppliedAuras(int spellId, Func<AuraApplication, bool> check, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            foreach (var app in m_appliedAuras[spellId].ToList())
            {
                if (check(app))
                    RemoveAura(app, removeMode);
            }
        }

        void RemoveOwnedAuras(int spellId, Func<Aura, bool> check, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            foreach (var aura in m_ownedAuras[spellId].ToList())
            {
                if (check(aura))
                    RemoveOwnedAura(aura, removeMode);
            }
        }

        public void RemoveAurasByType(AuraType auraType, Func<AuraApplication, bool> check, AuraRemoveMode removeMode = AuraRemoveMode.Default)
        {
            foreach (var eff in GetAuraEffectsByTypeCopy(auraType))
            {
                Aura aura = eff.GetBase();
                AuraApplication aurApp = aura.GetApplicationOfTarget(GetGUID());
                Cypher.Assert(aurApp != null);

                if (check(aurApp))
                {
                    RemoveAura(aurApp, removeMode);
                }
            }
        }

        public void RemoveAurasByShapeShift()
        {
            ulong mechanic_mask = (1 << (int)Mechanics.Snare) | (1 << (int)Mechanics.Root);
            foreach (var pair in GetAppliedAurasCopy())
            {
                Aura aura = pair.Value.GetBase();
                if ((aura.GetSpellInfo().GetAllEffectsMechanicMask() & mechanic_mask) != 0 
                    && !aura.GetSpellInfo().HasAttribute(SpellCustomAttributes.AuraCC))
                {
                    RemoveAura(pair);
                    continue;
                }
            }
        }

        void RemoveAreaAurasDueToLeaveWorld()
        {
            // make sure that all area auras not applied on self are removed
            foreach (var pair in GetOwnedAurasCopy())
            {
                var appMap = pair.Value.GetApplicationMap();
                foreach (var aurApp in appMap.Values.ToList())
                {
                    Unit target = aurApp.GetTarget();
                    if (target == this)
                        continue;

                    target.RemoveAura(aurApp);
                    // things linked on aura remove may apply new area aura - so start from the beginning
                }
            }

            // remove area auras owned by others
            foreach (var pair in GetAppliedAurasCopy())
            {
                if (pair.Value.GetBase().GetOwner() != this)
                    RemoveAura(pair);
            }
        }

        public void RemoveAllAuras()
        {
            // this may be a dead loop if some events on aura remove will continiously apply aura on remove
            // we want to have all auras removed, so use your brain when linking events
            for (int counter = 0; !m_appliedAuras.Empty() || !m_ownedAuras.Empty(); counter++)
            {
                foreach (var aurAppIter in GetAppliedAurasCopy())
                    _UnapplyAura(aurAppIter, AuraRemoveMode.Default);

                foreach (var aurIter in GetOwnedAurasCopy())
                    RemoveOwnedAura(aurIter);

                const int maxIteration = 50;
                // give this loop a few tries, if there are still auras then log as much information as possible
                if (counter >= maxIteration)
                {
                    StringBuilder sstr = new();
                    sstr.AppendLine($"Unit::RemoveAllAuras() iterated {maxIteration} times already " +
                        $"but there are still {m_appliedAuras.Count} m_appliedAuras " +
                        $"and {m_ownedAuras.Count} m_ownedAuras. Details:");

                    sstr.AppendLine(GetDebugInfo());

                    if (!m_appliedAuras.Empty())
                    {
                        sstr.AppendLine("m_appliedAuras:");

                        foreach (var auraAppPair in GetAppliedAurasCopy())
                            sstr.AppendLine(auraAppPair.Value.GetDebugInfo());
                    }

                    if (!m_ownedAuras.Empty())
                    {
                        sstr.AppendLine("m_ownedAuras:");

                        foreach (var auraPair in GetOwnedAurasCopy())
                            sstr.AppendLine(auraPair.Value.GetDebugInfo());
                    }

                    Log.outError(LogFilter.Unit, sstr.ToString());
                    break;
                }
            }
        }

        public void RemoveArenaAuras()
        {
            // in join, remove positive buffs, on end, remove negative
            // used to remove positive visible auras in arenas
            RemoveAppliedAuras(aurApp =>
            {
                Aura aura = aurApp.GetBase();
                return (!aura.GetSpellInfo().HasAttribute(SpellAttr4.AllowEnteringArena)                          // don't remove stances, shadowform, pally/hunter auras
                    && !aura.IsPassive()                                                                              // don't remove passive auras
                    && (aurApp.IsPositive() || !aura.GetSpellInfo().HasAttribute(SpellAttr3.AllowAuraWhileDead))) || // not negative death persistent auras
                    aura.GetSpellInfo().HasAttribute(SpellAttr5.RemoveEnteringArena);                             // special marker, always remove
            });
        }

        public void RemoveAllAurasExceptType(AuraType type)
        {
            foreach (var pair in GetAppliedAurasCopy())
            {
                if (pair.Value == null)
                    continue;
                Aura aura = pair.Value.GetBase();
                if (!aura.GetSpellInfo().HasAura(type))
                    _UnapplyAura(pair, AuraRemoveMode.Default);
            }

            foreach (var pair in GetOwnedAurasCopy())
            {
                if (pair.Value == null)
                    continue;

                Aura aura = pair.Value;
                if (!aura.GetSpellInfo().HasAura(type))
                    RemoveOwnedAura(pair, AuraRemoveMode.Default);
            }
        }

        public void RemoveAllAurasExceptType(AuraType type1, AuraType type2)
        {
            foreach (var pair in GetAppliedAurasCopy())
            {
                Aura aura = pair.Value.GetBase();
                if (!aura.GetSpellInfo().HasAura(type1) || !aura.GetSpellInfo().HasAura(type2))
                    _UnapplyAura(pair, AuraRemoveMode.Default);
            }

            foreach (var pair in GetOwnedAurasCopy())
            {
                Aura aura = pair.Value;
                if (!aura.GetSpellInfo().HasAura(type1) || !aura.GetSpellInfo().HasAura(type2))
                    RemoveOwnedAura(pair, AuraRemoveMode.Default);
            }
        }

        public void ModifyAuraState(AuraStateType flag, bool apply)
        {
            uint mask = 1u << ((int)flag - 1);
            if (apply)
            {
                if ((m_unitData.AuraState & mask) == 0)
                {
                    SetUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AuraState), mask);
                    if (IsTypeId(TypeId.Player))
                    {
                        var sp_list = ToPlayer().GetSpellMap();
                        foreach (var spell in sp_list)
                        {
                            if (spell.Value.State == PlayerSpellState.Removed || spell.Value.Disabled)
                                continue;

                            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spell.Key, Difficulty.None);
                            if (spellInfo == null || !spellInfo.IsPassive())
                                continue;

                            if (spellInfo.CasterAuraState == flag)
                                CastSpell(this, spell.Key, true);
                        }
                    }
                    else if (IsPet())
                    {
                        Pet pet = ToPet();
                        foreach (var spell in pet.m_spells)
                        {
                            if (spell.Value.UpdateState == PetSpellState.Removed)
                                continue;
                            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spell.Key, Difficulty.None);
                            if (spellInfo == null || !spellInfo.IsPassive())
                                continue;
                            if (spellInfo.CasterAuraState == flag)
                                CastSpell(this, spell.Key, true);
                        }
                    }
                }
            }
            else
            {
                if ((m_unitData.AuraState & mask) != 0)
                {
                    RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AuraState), mask);

                    foreach (var app in GetAppliedAurasCopy())
                    {
                        if (app.Value == null)
                            continue;

                        SpellInfo spellProto = app.Value.GetBase().GetSpellInfo();
                        if (app.Value.GetBase().GetCasterGUID() == GetGUID()
                            && spellProto.CasterAuraState == flag
                            && (spellProto.IsPassive() || flag != AuraStateType.Enraged))
                        {
                            RemoveAura(app);
                        }
                    }
                }
            }
        }
        public bool HasAuraState(AuraStateType flag, SpellInfo spellProto = null, Unit caster = null)
        {
            if (caster != null)
            {
                if (spellProto != null)
                {
                    if (caster.HasAuraTypeWithAffectMask(AuraType.AbilityIgnoreAurastate, spellProto))
                        return true;
                }

                // Check per caster aura state
                // If aura with aurastate by caster not found return false
                if ((1 << (int)flag).HasAnyFlag((int)AuraStateType.PerCasterAuraStateMask))
                {
                    var range = m_auraStateAuras[flag];
                    foreach (var auraApp in range)
                    {
                        if (auraApp.GetBase().GetCasterGUID() == caster.GetGUID())
                            return true;
                    }

                    return false;
                }
            }

            return (m_unitData.AuraState & (1 << ((int)flag - 1))) != 0;
        }

        public void _ApplyAllAuraStatMods()
        {
            foreach (var i in GetAppliedAuras())
                i.Value.GetBase().HandleAllEffects(i.Value, AuraEffectHandleModes.Stat, true);
        }

        public void _RemoveAllAuraStatMods()
        {
            foreach (var i in GetAppliedAuras())
                i.Value.GetBase().HandleAllEffects(i.Value, AuraEffectHandleModes.Stat, false);
        }

        // removes aura application from lists and unapplies effects
        public void _UnapplyAura(KeyValuePair<int, AuraApplication> pair, AuraRemoveMode removeMode)
        {
            //Check if aura was already removed, if so just return.
            if (!m_appliedAuras.Remove(pair))
                return;

            AuraApplication aurApp = pair.Value;
            Cypher.Assert(aurApp != null);
            Cypher.Assert(!aurApp.HasRemoveMode());
            Cypher.Assert(aurApp.GetTarget() == this);

            aurApp.SetRemoveMode(removeMode);
            Aura aura = aurApp.GetBase();
            Log.outDebug(LogFilter.Spells, $"Aura {aura.GetId()} now is remove mode {removeMode}");

            // dead loop is killing the server probably
            Cypher.Assert(m_removedAurasCount < 0xFFFFFFFF);

            ++m_removedAurasCount;

            Unit caster = aura.GetCaster();

            if (aura.GetSpellInfo().HasAnyAuraInterruptFlag())
            {
                m_interruptableAuras.Remove(aurApp);
                UpdateInterruptMask();
            }

            bool auraStateFound = false;
            AuraStateType auraState = aura.GetSpellInfo().GetAuraState();
            if (auraState != 0)
            {
                // Get mask of all aurastates from remaining auras
                foreach (var i in m_auraStateAuras[auraState].ToList())
                {
                    if (i == aurApp)
                    {
                        m_auraStateAuras.Remove(auraState, i);
                        continue;
                    }

                    auraStateFound = true;
                }
            }

            aurApp._Remove();
            aura._UnapplyForTarget(this, caster, aurApp);

            // remove effects of the spell - needs to be done after removing aura from lists
            for (byte c = 0; c < SpellConst.MaxEffects; ++c)
            {
                if (aurApp.HasEffect(c))
                    aurApp._HandleEffect(c, false);
            }

            // all effect mustn't be applied
            Cypher.Assert(aurApp.GetEffectMask() == 0);

            // Remove totem at next update if totem loses its aura
            if (aurApp.GetRemoveMode() == AuraRemoveMode.Expire && IsTypeId(TypeId.Unit) && IsTotem())
            {
                if (ToTotem().GetSpell() == aura.GetId() && ToTotem().GetTotemType() == TotemType.Passive)
                    ToTotem().SetDeathState(DeathState.JustDied);
            }

            // Remove aurastates only if needed and were not found
            if (auraState != 0)
            {
                if (!auraStateFound)
                    ModifyAuraState(auraState, false);
                else
                {
                    // update for casters, some shouldn't 'see' the aura state
                    uint aStateMask = (1u << ((int)auraState - 1));
                    if ((aStateMask & (uint)AuraStateType.PerCasterAuraStateMask) != 0)
                    {
                        m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AuraState);
                        ForceUpdateFieldChange();
                    }
                }
            }

            aura.HandleAuraSpecificMods(aurApp, caster, false, false);

            if (ToPlayer() is Player player)
            {
                if (Global.ConditionMgr.IsSpellUsedInSpellClickConditions(aurApp.GetBase().GetId()))
                    player.UpdateVisibleObjectInteractions(false, true, false, false);

                player.FailCriteria(CriteriaFailEvent.LoseAura, aurApp.GetBase().GetId());
            }
        }

        public void _UnapplyAura(AuraApplication aurApp, AuraRemoveMode removeMode)
        {
            // aura can be removed from unit only if it's applied on it, shouldn't happen
            Cypher.Assert(aurApp.GetBase().GetApplicationOfTarget(GetGUID()) == aurApp);

            var spellId = aurApp.GetBase().GetId();

            foreach (var app in m_appliedAuras[spellId].ToList())
            {
                if (app == aurApp)
                {
                    _UnapplyAura(new KeyValuePair<int, AuraApplication>(spellId, app), removeMode);
                    return;
                }
            }
            Cypher.Assert(false);
        }

        public AuraEffect GetAuraEffect(int spellId, int effIndex, ObjectGuid casterGUID = default)
        {
            foreach (var aura in m_appliedAuras[spellId])
            {
                if (aura.HasEffect(effIndex)
                        && (casterGUID.IsEmpty() || aura.GetBase().GetCasterGUID() == casterGUID))
                {
                    return aura.GetBase().GetEffect(effIndex);
                }
            }

            return null;
        }

        public AuraEffect GetAuraEffectOfTalent(int talentId, int effIndex, ObjectGuid casterGUID = default)
        {
            return GetAuraEffectOfTalent(talentId, effIndex, out _, casterGUID);
        }

        public AuraEffect GetAuraEffectOfTalent(int talentId, int effIndex, out int rank, ObjectGuid casterGUID = default)
        {
            rank = 0;
            TalentRecord talentInfo = CliDB.TalentStorage.LookupByKey(talentId);
            if (talentInfo == null)
                return null;

            for (rank = PlayerConst.MaxTalentRank; rank > 0; rank--)
            {
                int rankSpell = talentInfo.SpellRank[rank - 1];
                if (rankSpell != 0)
                {
                    AuraEffect aurEff = GetAuraEffect(rankSpell, effIndex, casterGUID);
                    if (aurEff != null)
                        return aurEff;
                }
            }

            return null;
        }

        public AuraEffect GetAuraEffectOfRankedSpell(int spellId, int effIndex, ObjectGuid casterGUID = default)
        {
            var rankSpell = Global.SpellMgr.GetFirstSpellInChain(spellId);
            while (rankSpell != 0)
            {
                AuraEffect aurEff = GetAuraEffect(rankSpell, effIndex, casterGUID);
                if (aurEff != null)
                    return aurEff;

                rankSpell = Global.SpellMgr.GetNextSpellInChain(rankSpell);
            }
            return null;
        }

        // spell mustn't have familyflags
        public AuraEffect GetAuraEffect(AuraType type, SpellFamilyNames family, FlagArray128 familyFlag, ObjectGuid casterGUID = default)
        {
            foreach (var aura in GetAuraEffectsByType(type))
            {
                SpellInfo spell = aura.GetSpellInfo();
                if (spell.SpellFamilyName == family && spell.SpellFamilyFlags & familyFlag)
                {
                    if (!casterGUID.IsEmpty() && aura.GetCasterGUID() != casterGUID)
                        continue;

                    return aura;
                }
            }

            return null;
        }

        public AuraApplication GetAuraApplication(int spellId, ObjectGuid casterGUID = default, ObjectGuid itemCasterGUID = default, uint reqEffMask = 0, AuraApplication except = null)
        {
            return GetAuraApplication(spellId, app =>
            {
                Aura aura = app.GetBase();

                if (((aura.GetEffectMask() & reqEffMask) == reqEffMask) && (casterGUID.IsEmpty() || aura.GetCasterGUID() == casterGUID)
                    && (itemCasterGUID.IsEmpty() || aura.GetCastItemGUID() == itemCasterGUID) && (except == null || except != app))
                {
                    return true;
                }

                return false;
            });
        }

        public AuraApplication GetAuraApplication(int spellId, Func<AuraApplication, bool> predicate)
        {
            foreach (var app in m_appliedAuras[spellId])
            {
                if (predicate(app))
                    return app;
            }

            return null;
        }

        public AuraApplication GetAuraApplication(int spellId, Func<Aura, bool> predicate)
        {
            foreach (var app in m_appliedAuras[spellId])
            {
                if (predicate(app.GetBase()))
                    return app;
            }

            return null;
        }

        public AuraApplication GetAuraApplication(Func<AuraApplication, bool> predicate)
        {
            foreach (var pair in m_appliedAuras)
            {
                if (predicate(pair.Value))
                    return pair.Value;
            }

            return null;
        }

        public AuraApplication GetAuraApplication(Func<Aura, bool> predicate)
        {
            foreach (var pair in m_appliedAuras)
            {
                if (predicate(pair.Value.GetBase()))
                    return pair.Value;
            }

            return null;
        }

        public Aura GetAura(int spellId, ObjectGuid casterGUID = default, ObjectGuid itemCasterGUID = default, uint reqEffMask = 0)
        {
            AuraApplication aurApp = GetAuraApplication(spellId, casterGUID, itemCasterGUID, reqEffMask);
            return aurApp?.GetBase();
        }

        public Aura GetAura(int spellId, Func<Aura, bool> predicate)
        {
            AuraApplication aurApp = GetAuraApplication(spellId, predicate);
            return aurApp?.GetBase();
        }

        public Aura GetAura(Func<Aura, bool> predicate)
        {
            AuraApplication aurApp = GetAuraApplication(predicate);
            return aurApp?.GetBase();
        }

        public uint BuildAuraStateUpdateForTarget(Unit target)
        {
            uint auraStates = m_unitData.AuraState & ~(uint)AuraStateType.PerCasterAuraStateMask;
            foreach (var state in m_auraStateAuras)
            {
                if ((1u << (int)state.Key - 1).HasAnyFlag((uint)AuraStateType.PerCasterAuraStateMask))
                {
                    if (state.Value.GetBase().GetCasterGUID() == target.GetGUID())
                        auraStates |= (uint)(1 << (int)state.Key - 1);
                }
            }

            return auraStates;
        }

        public bool CanProc() { return m_procDeep == 0; }

        public int GetProcChainLength() { return m_procChainLength; }
        
        public void _ApplyAuraEffect(Aura aura, int effIndex)
        {
            Cypher.Assert(aura != null);
            Cypher.Assert(aura.HasEffect(effIndex));
            AuraApplication aurApp = aura.GetApplicationOfTarget(GetGUID());
            Cypher.Assert(aurApp != null);
            if (aurApp.GetEffectMask() == 0)
                _ApplyAura(aurApp, 1u << effIndex);
            else
                aurApp._HandleEffect(effIndex, true);
        }

        // handles effects of aura application
        // should be done after registering aura in lists
        public void _ApplyAura(AuraApplication aurApp, uint effMask)
        {
            Aura aura = aurApp.GetBase();

            _RemoveNoStackAurasDueToAura(aura, false);

            if (aurApp.HasRemoveMode())
                return;

            // Update target aura state flag
            AuraStateType aState = aura.GetSpellInfo().GetAuraState();
            if (aState != 0)
            {
                uint aStateMask = 1u << ((int)aState - 1);
                // force update so the new caster registers it
                if (aStateMask.HasAnyFlag((uint)AuraStateType.PerCasterAuraStateMask) && (m_unitData.AuraState & aStateMask) != 0)
                {
                    m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AuraState);
                    ForceUpdateFieldChange();
                }
                else
                    ModifyAuraState(aState, true);
            }

            if (aurApp.HasRemoveMode())
                return;

            // Sitdown on apply aura req seated
            if (aura.GetSpellInfo().HasAuraInterruptFlag(SpellAuraInterruptFlags.Standing) && !IsSitState())
                SetStandState(UnitStandStateType.Sit);

            Unit caster = aura.GetCaster();

            if (aurApp.HasRemoveMode())
                return;

            aura.HandleAuraSpecificMods(aurApp, caster, true, false);

            // apply effects of the aura
            for (byte i = 0; i < SpellConst.MaxEffects; i++)
            {
                if (Convert.ToBoolean(effMask & 1 << i) && !(aurApp.HasRemoveMode()))
                    aurApp._HandleEffect(i, true);
            }

            if (ToPlayer() is Player player)
            {
                if (Global.ConditionMgr.IsSpellUsedInSpellClickConditions(aurApp.GetBase().GetId()))
                    player.UpdateVisibleObjectInteractions(false, true, false, false);

                player.FailCriteria(CriteriaFailEvent.GainAura, aurApp.GetBase().GetId());
                player.StartCriteria(CriteriaStartEvent.GainAura, aurApp.GetBase().GetId());
                player.UpdateCriteria(CriteriaType.GainAura, aurApp.GetBase().GetId());
            }
        }

        public void _AddAura(UnitAura aura, Unit caster)
        {
            Cypher.Assert(!m_cleanupDone);
            m_ownedAuras.Add(aura.GetId(), aura);

            _RemoveNoStackAurasDueToAura(aura, true);

            if (aura.IsRemoved())
                return;

            aura.SetIsSingleTarget(caster != null && aura.GetSpellInfo().IsSingleTarget());
            if (aura.IsSingleTarget())
            {

                // @HACK: Player is not in world during loading auras.
                //Single target auras are not saved or loaded from database
                //but may be created as a result of aura links (player mounts with passengers)
                Cypher.Assert((IsInWorld && !IsDuringRemoveFromWorld()) || aura.GetCasterGUID() == GetGUID());

                // register single target aura
                caster.m_scAuras.Add(aura);

                Queue<Aura> aurasSharingLimit = new();
                // remove other single target auras
                foreach (Aura scAura in caster.GetSingleCastAuras())
                    if (scAura != aura && scAura.IsSingleTargetWith(aura))
                        aurasSharingLimit.Enqueue(scAura);

                int maxOtherAuras = aura.GetSpellInfo().MaxAffectedTargets - 1;
                while (aurasSharingLimit.Count > maxOtherAuras)
                {
                    aurasSharingLimit.Peek().Remove();
                    aurasSharingLimit.Dequeue();
                }
            }
        }

        public Aura _TryStackingOrRefreshingExistingAura(AuraCreateInfo createInfo)
        {
            Cypher.Assert(!createInfo.CasterGUID.IsEmpty() || createInfo.Caster != null);

            // Check if these can stack anyway
            if (createInfo.CasterGUID.IsEmpty() && !createInfo.GetSpellInfo().IsStackableOnOneSlotWithDifferentCasters())
                createInfo.CasterGUID = createInfo.Caster.GetGUID();

            // passive and Incanter's Absorption and auras with different Type can stack with themselves any number of times
            if (!createInfo.GetSpellInfo().IsMultiSlotAura())
            {
                // check if cast item changed
                ObjectGuid castItemGUID = createInfo.CastItemGUID;

                // find current aura from spell and change it's stackamount, or refresh it's duration
                Aura foundAura = GetOwnedAura(createInfo.GetSpellInfo().Id, createInfo.GetSpellInfo().IsStackableOnOneSlotWithDifferentCasters() ? ObjectGuid.Empty : createInfo.CasterGUID, createInfo.GetSpellInfo().HasAttribute(SpellCustomAttributes.EnchantProc) ? castItemGUID : ObjectGuid.Empty, 0);
                if (foundAura != null)
                {
                    // effect masks do not match
                    // extremely rare case
                    // let's just recreate aura
                    if (createInfo.GetAuraEffectMask() != foundAura.GetEffectMask())
                        return null;

                    // update basepoints with new values - effect amount will be recalculated in ModStackAmount
                    foreach (var spellEffectInfo in createInfo.GetSpellInfo().GetEffects())
                    {
                        AuraEffect auraEff = foundAura.GetEffect(spellEffectInfo.EffectIndex);
                        if (auraEff == null)
                            continue;

                        int bp;
                        if (createInfo.BaseAmount != null)
                            bp = createInfo.BaseAmount[spellEffectInfo.EffectIndex];
                        else
                            bp = (int)spellEffectInfo.BasePoints;

                        int oldBP = auraEff.m_baseAmount;
                        if (spellEffectInfo.EffectAttributes.HasFlag(SpellEffectAttributes.AuraPointsStack))
                            auraEff.m_baseAmount += bp;
                        else
                            auraEff.m_baseAmount = bp;
                    }

                    // correct cast item guid if needed
                    if (castItemGUID != foundAura.GetCastItemGUID())
                    {
                        foundAura.SetCastItemGUID(castItemGUID);
                        foundAura.SetCastItemId(createInfo.CastItemId);
                        foundAura.SetCastItemLevel(createInfo.CastItemLevel);
                    }

                    // try to increase stack amount
                    foundAura.ModStackAmount(1, AuraRemoveMode.Default, createInfo.ResetPeriodicTimer);
                    return foundAura;
                }
            }

            return null;
        }

        void _RemoveNoStackAurasDueToAura(Aura aura, bool owned)
        {
            SpellInfo spellProto = aura.GetSpellInfo();

            // passive spell special case (only non stackable with ranks)
            if (spellProto.IsPassiveStackableWithRanks())
                return;

            if (!IsHighestExclusiveAura(aura))
            {
                aura.Remove();
                return;
            }

            if (owned)
                RemoveOwnedAuras(ownedAura => !aura.CanStackWith(ownedAura), AuraRemoveMode.Default);
            else
                RemoveAppliedAuras(appliedAura => !aura.CanStackWith(appliedAura.GetBase()), AuraRemoveMode.Default);
        }

        public int GetHighestExclusiveSameEffectSpellGroupValue(AuraEffect aurEff, AuraType auraType, bool checkMiscValue = false, int miscValue = 0)
        {
            int val = 0;
            var spellGroupList = Global.SpellMgr.GetSpellSpellGroupMapBounds(aurEff.GetSpellInfo().GetFirstRankSpell().Id);
            foreach (var spellGroup in spellGroupList)
            {
                if (Global.SpellMgr.GetSpellGroupStackRule(spellGroup) == SpellGroupStackRule.ExclusiveSameEffect)
                {
                    foreach (var auraEffect in GetAuraEffectsByType(auraType))
                    {
                        if (aurEff != auraEffect && (!checkMiscValue || auraEffect.GetMiscValue() == miscValue) &&
                            Global.SpellMgr.IsSpellMemberOfSpellGroup(auraEffect.GetSpellInfo().Id, spellGroup))
                        {
                            // absolute value only
                            if (Math.Abs(val) < Math.Abs(auraEffect.GetAmount()))
                                val = auraEffect.GetAmount();
                        }
                    }
                }
            }

            return val;
        }

        public bool IsHighestExclusiveAura(Aura aura, bool removeOtherAuraApplications = false)
        {
            foreach (AuraEffect aurEff in aura.GetAuraEffects())
            {
                if (aurEff == null)
                    continue;

                if (!IsHighestExclusiveAuraEffect(aura.GetSpellInfo(), aurEff.GetAuraType(), aurEff.GetAmount(), aura.GetEffectMask(), removeOtherAuraApplications))
                    return false;
            }

            return true;
        }

        public bool IsHighestExclusiveAuraEffect(SpellInfo spellInfo, AuraType auraType, int effectAmount, uint auraEffectMask, bool removeOtherAuraApplications = false)
        {
            foreach (AuraEffect existingAurEff in GetAuraEffectsByTypeCopy(auraType))
            {
                if (Global.SpellMgr.CheckSpellGroupStackRules(spellInfo, existingAurEff.GetSpellInfo()) == SpellGroupStackRule.ExclusiveHighest)
                {
                    long diff = Math.Abs(effectAmount) - Math.Abs(existingAurEff.GetAmount());
                    if (diff == 0)
                    {
                        for (int i = 0; i < SpellConst.MaxEffects; ++i)
                            diff += ((auraEffectMask & (1 << i)) >> i) - ((existingAurEff.GetBase().GetEffectMask() & (1 << i)) >> i);
                    }

                    if (diff > 0)
                    {
                        Aura auraBase = existingAurEff.GetBase();
                        // no removing of area auras from the original owner, as that completely cancels them
                        if (removeOtherAuraApplications && (!auraBase.IsArea() || auraBase.GetOwner() != this))
                        {
                            AuraApplication aurApp = existingAurEff.GetBase().GetApplicationOfTarget(GetGUID());
                            if (aurApp != null)
                            {
                                // bool hasMoreThanOneEffect = auraBase.HasMoreThanOneEffectForType(auraType);
                                RemoveAura(aurApp);
                                //if (hasMoreThanOneEffect)
                                //  ontinue;
                            }
                        }
                    }
                    else if (diff < 0)
                        return false;
                }
            }

            return true;
        }

        public Aura GetOwnedAura(int spellId, ObjectGuid casterGUID = default, ObjectGuid itemCasterGUID = default, uint reqEffMask = 0, Aura except = null)
        {
            foreach (var aura in m_ownedAuras[spellId])
            {
                if (((aura.GetEffectMask() & reqEffMask) == reqEffMask) && (casterGUID.IsEmpty() || aura.GetCasterGUID() == casterGUID)
                    && (itemCasterGUID.IsEmpty() || aura.GetCastItemGUID() == itemCasterGUID) && (except == null || except != aura))
                {
                    return aura;
                }
            }

            return null;
        }

        public List<AuraEffect> GetAuraEffectsByTypeCopy(AuraType type)
        {
            return m_modAuras[type].ToList();
        }

        public IReadOnlyList<AuraEffect> GetAuraEffectsByType(AuraType type)
        {
            return m_modAuras[type];
        }

        public int GetTotalAuraModifier(AuraType auraType)
        {
            return GetTotalAuraModifier(auraType, aurEff => true);
        }

        public int GetTotalAuraModifier(AuraType auraType, Func<AuraEffect, bool> predicate)
        {
            Dictionary<SpellGroup, int> sameEffectSpellGroup = new();
            int modifier = 0;

            foreach (AuraEffect aurEff in GetAuraEffectsByType(auraType))
            {
                if (predicate(aurEff))
                {
                    // Check if the Aura Effect has a the Same Effect Stack Rule and if so, use the highest amount of that SpellGroup
                    // If the Aura Effect does not have this Stack Rule, it returns false so we can add to the multiplier as usual
                    if (!Global.SpellMgr.AddSameEffectStackRuleSpellGroups(aurEff.GetSpellInfo(), auraType, aurEff.GetAmount(), sameEffectSpellGroup))
                        modifier += aurEff.GetAmount();
                }
            }

            // Add the highest of the Same Effect Stack Rule SpellGroups to the accumulator
            foreach (var pair in sameEffectSpellGroup)
                modifier += pair.Value;

            return modifier;
        }

        public float GetTotalAuraMultiplier(AuraType auraType)
        {
            return GetTotalAuraMultiplier(auraType, aurEff => true);
        }

        public MultModifier GetTotalAuraMultiplier(AuraType auraType, Func<AuraEffect, bool> predicate)
        {
            var mTotalAuraList = GetAuraEffectsByType(auraType);
            if (mTotalAuraList.Empty())
                return new();

            Dictionary<SpellGroup, int> sameEffectSpellGroup = new();
            MultModifier multiplier = new();

            foreach (var aurEff in mTotalAuraList)
            {
                if (predicate(aurEff))
                {
                    // Check if the Aura Effect has a the Same Effect Stack Rule and if so, use the highest amount of that SpellGroup
                    // If the Aura Effect does not have this Stack Rule, it returns false so we can add to the multiplier as usual
                    if (!Global.SpellMgr.AddSameEffectStackRuleSpellGroups(aurEff.GetSpellInfo(), auraType, aurEff.GetAmount(), sameEffectSpellGroup))
                        multiplier.ModifyPercentage(aurEff.GetAmount(), true);
                }
            }

            // Add the highest of the Same Effect Stack Rule SpellGroups to the multiplier
            foreach (var pair in sameEffectSpellGroup)
                multiplier.ModifyPercentage(pair.Value, true);

            return multiplier;
        }

        public bool GetTotalAuraAggregate(AuraType auraType, Func<AuraEffect, bool> predicate, Func<AuraEffect, int> amountSearcher, Action<int> aggregator)
        {
            Dictionary<SpellGroup, int> sameEffectSpellGroup = new();
            bool hasAura = false;

            foreach (AuraEffect aurEff in GetAuraEffectsByType(auraType))
            {
                if (predicate(aurEff))
                {                    
                    int amount = amountSearcher(aurEff);
                    hasAura = true;

                    // Check if the Aura Effect has a the Same Effect Stack Rule and if so, use the highest amount of that SpellGroup
                    // If the Aura Effect does not have this Stack Rule, it returns false so we can aggregate as usual
                    if (!Global.SpellMgr.AddSameEffectStackRuleSpellGroups(aurEff.GetSpellInfo(), auraType, amount, sameEffectSpellGroup))
                        aggregator(amount);
                }
            }

            // Put the highest of the Same Effect Stack Rule SpellGroups to the aggregator
            foreach (var pair in sameEffectSpellGroup)
                aggregator(pair.Value);

            return hasAura;
        }

        public int GetMaxPositiveAuraModifier(AuraType auraType)
        {
            return GetMaxPositiveAuraModifier(auraType, aurEff => true);
        }

        public int GetMaxPositiveAuraModifier(AuraType auraType, Func<AuraEffect, bool> predicate)
        {
            int modifier = 0;
            foreach (var aurEff in GetAuraEffectsByType(auraType))
            {
                if (predicate(aurEff))
                    modifier = Math.Max(modifier, aurEff.GetAmount());
            }

            return modifier;
        }

        public int GetMaxNegativeAuraModifier(AuraType auraType)
        {
            return GetMaxNegativeAuraModifier(auraType, aurEff => true);
        }

        public int GetMaxNegativeAuraModifier(AuraType auraType, Func<AuraEffect, bool> predicate)
        {
            int modifier = 0;
            foreach (var aurEff in GetAuraEffectsByType(auraType))
            {
                if (predicate(aurEff))
                    modifier = Math.Min(modifier, aurEff.GetAmount());
            }

            return modifier;
        }

        public int GetTotalAuraModifierByMiscMask(AuraType auraType, uint miscMask)
        {
            return GetTotalAuraModifier(auraType, aurEff =>
            {
                if ((aurEff.GetMiscValue() & miscMask) != 0)
                    return true;
                return false;
            });
        }

        public float GetTotalAuraMultiplierByMiscMask(AuraType auraType, uint miscMask)
        {
            return GetTotalAuraMultiplier(auraType, aurEff =>
            {
                if ((aurEff.GetMiscValue() & miscMask) != 0)
                    return true;
                return false;
            });
        }

        public int GetMaxPositiveAuraModifierByMiscMask(AuraType auraType, uint miscMask, AuraEffect except = null)
        {
            return GetMaxPositiveAuraModifier(auraType, aurEff =>
            {
                if (except != aurEff && (aurEff.GetMiscValue() & miscMask) != 0)
                    return true;
                return false;
            });
        }

        public int GetMaxNegativeAuraModifierByMiscMask(AuraType auraType, uint miscMask)
        {
            return GetMaxNegativeAuraModifier(auraType, aurEff =>
            {
                if ((aurEff.GetMiscValue() & miscMask) != 0)
                    return true;
                return false;
            });
        }

        public int GetTotalAuraModifierByMiscValue(AuraType auraType, int miscValue)
        {
            return GetTotalAuraModifier(auraType, aurEff =>
            {
                if (aurEff.GetMiscValue() == miscValue)
                    return true;
                return false;
            });
        }

        public float GetTotalAuraMultiplierByMiscValue(AuraType auraType, int miscValue)
        {
            return GetTotalAuraMultiplier(auraType, aurEff =>
            {
                if (aurEff.GetMiscValue() == miscValue)
                    return true;
                return false;
            });
        }

        int GetMaxPositiveAuraModifierByMiscValue(AuraType auraType, int miscValue)
        {
            return GetMaxPositiveAuraModifier(auraType, aurEff =>
            {
                if (aurEff.GetMiscValue() == miscValue)
                    return true;
                return false;
            });
        }

        public int GetMaxNegativeAuraModifierByMiscValue(AuraType auraType, int miscValue)
        {
            return GetMaxNegativeAuraModifier(auraType, aurEff =>
            {
                if (aurEff.GetMiscValue() == miscValue)
                    return true;
                return false;
            });
        }

        public void _RegisterAuraEffect(AuraEffect aurEff, bool apply)
        {
            if (apply)
            {
                m_modAuras.Add(aurEff.GetAuraType(), aurEff);
                
                if (ToPlayer() is Player player)
                {
                    player.StartCriteria(CriteriaStartEvent.GainAuraEffect, (int)aurEff.GetAuraType());
                    player.FailCriteria(CriteriaFailEvent.GainAuraEffect, (int)aurEff.GetAuraType());
                }
            }
            else
                m_modAuras.Remove(aurEff.GetAuraType(), aurEff);
        }

        public void SetVisibleAura(AuraApplication aurApp)
        {
            m_visibleAuras.Add(aurApp);
            m_visibleAurasToUpdate.Add(aurApp);
            UpdateAuraForGroup();
        }

        public void RemoveVisibleAura(AuraApplication aurApp)
        {
            m_visibleAuras.Remove(aurApp);
            m_visibleAurasToUpdate.Remove(aurApp);
            UpdateAuraForGroup();
        }

        void UpdateAuraForGroup()
        {
            Player player = ToPlayer();
            if (player != null)
            {
                if (player.GetGroup() != null)
                    player.SetGroupUpdateFlag(GroupUpdateFlags.Auras);
            }
            else if (IsPet())
            {
                Pet pet = ToPet();
                if (pet.IsControlled())
                    pet.SetGroupUpdateFlag(GroupUpdatePetFlags.Auras);
            }
        }

        public SortedSet<AuraApplication> GetVisibleAuras() { return m_visibleAuras; }
        public bool HasVisibleAura(AuraApplication aurApp) { return m_visibleAuras.Contains(aurApp); }
        public void SetVisibleAuraUpdate(AuraApplication aurApp) { m_visibleAurasToUpdate.Add(aurApp); }
        public void RemoveVisibleAuraUpdate(AuraApplication aurApp) { m_visibleAurasToUpdate.Remove(aurApp); }
    }
}
