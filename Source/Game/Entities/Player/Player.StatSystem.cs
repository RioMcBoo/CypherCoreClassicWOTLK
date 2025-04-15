// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public partial class Player
    {
        public override bool UpdateAllStats()
        {
            for (var i = Stats.Strength; i < Stats.Max; ++i)
            {
                UpdateStats(i, true);
            }

            UpdateAllResistances();
            UpdateArmor(true);
            UpdateMeleeAttackPowerAndDamage(true);
            UpdateRangedAttackPowerAndDamage(true);
            UpdateMaxHealth();

            for (var i = PowerType.Mana; i < PowerType.Max; ++i)
                UpdateMaxPower(i);

            UpdateAllRatings();
            UpdateAllCritPercentages();
            UpdateAllSpellCritChances();
            UpdateBlockPercentage();
            UpdateParryPercentage();
            UpdateDodgePercentage();
            UpdateShieldBlockValue();
            UpdatePowerRegen(PowerType.Mana);
            UpdatePowerRegen(PowerType.Rage);
            UpdatePowerRegen(PowerType.Energy);
            UpdatePowerRegen(PowerType.RunicPower);
            UpdateExpertise(WeaponAttackType.BaseAttack);
            UpdateExpertise(WeaponAttackType.OffAttack);
            UpdateDamagePhysical(WeaponAttackType.BaseAttack);
            UpdateDamagePhysical(WeaponAttackType.OffAttack);
            UpdateDamagePhysical(WeaponAttackType.RangedAttack);
            UpdateSpellDamageAndHealingBonus();
            RecalculateRating(CombatRating.ArmorPenetration);

            if (GetPet() is Pet pet)
                pet.UpdateAllStats();

            if (GetGuardianPet() is Guardian guardian)
                guardian.UpdateAllStats();

            return true;
        }

        public override bool UpdateStats(Stats stat, bool skipDependents)
        {
            UnitMods unitMod = UnitMods.StatStart + (int)stat;
            UnitModResult modifiedStat = new(GetCreateStat(stat));
            StatMods.ApplyModsTo(modifiedStat, unitMod);

            SetStat(stat, (int)modifiedStat.TotalValue);

            // Update stat buff mods for the client
            UpdateStatBuffModForClient(stat, modifiedStat.ModPos, modifiedStat.ModNeg);

            if (skipDependents)
                return true;

            Pet pet = GetPet();

            if (stat == Stats.Stamina || stat == Stats.Intellect || stat == Stats.Strength)
            {
                if (pet != null)
                    pet.UpdateStats(stat);
            }

            switch (stat)
            {
                case Stats.Strength:
                    UpdateMeleeAttackPowerAndDamage();
                    UpdateShieldBlockValue();
                    UpdateParryPercentage();
                    break;
                case Stats.Agility:
                    UpdateArmor();
                    UpdateRangedAttackPowerAndDamage();
                    UpdateAllCritPercentages();
                    UpdateDodgePercentage();
                    break;
                case Stats.Stamina:
                    UpdateMaxHealth();
                    break;
                case Stats.Intellect:
                    UpdateMaxPower(PowerType.Mana);
                    UpdateAllSpellCritChances();
                    UpdateArmor();
                    break;
                case Stats.Spirit:
                    break;
                default:
                    break;
            }

            UpdateSpellDamageAndHealingBonus();
            UpdatePowerRegen(PowerType.Mana);

            /*
            // Update ratings in exist SPELL_AURA_MOD_RATING_FROM_STAT and only depends from stat
            uint32 mask = 0;
            AuraEffectList const& modRatingFromStat = GetAuraEffectsByType(SPELL_AURA_MOD_RATING_FROM_STAT);
            for (AuraEffectList::const_iterator i = modRatingFromStat.begin(); i != modRatingFromStat.end(); ++i)
                if (Stats((*i)->GetMiscValueB()) == stat)
                    mask |= (*i)->GetMiscValue();
            if (mask)
            {
                for (uint32 rating = 0; rating < MAX_COMBAT_RATING; ++rating)
                    if (mask & (1 << rating))
                        ApplyRatingMod(CombatRating(rating), 0, true);
            }
            */
            return true;
        }

        public void UpdateAllSpellCritChances()
        {
            for (var i = SpellSchools.Normal; i < SpellSchools.Max; ++i)
            {
                UpdateSpellCritChance(i);
            }
        }

        //skill+step, checking for max value
        bool UpdateSkill(SkillType skill_id, int step)
        {
            if (skill_id == SkillType.None)
                return false;

            var itr = mSkillStatus.LookupByKey(skill_id);
            if (itr == null || itr.State == SkillState.Deleted)
                return false;

            int value = m_activePlayerData.Skill.GetValue().SkillRank[itr.Pos];
            int max = m_activePlayerData.Skill.GetValue().SkillMaxRank[itr.Pos];

            if ((max == 0) || (value == 0) || (value >= max))
                return false;

            if (value < max)
            {
                int new_value = value + step;
                if (new_value > max)
                    new_value = max;

                SetSkillRank(itr.Pos, new_value);
                if (itr.State != SkillState.New)
                    itr.State = SkillState.Changed;

                UpdateSkillEnchantments(skill_id, value, new_value);
                UpdateCriteria(CriteriaType.SkillRaised, (long)skill_id);
                return true;
            }

            return false;
        }

        public override void UpdateResistances(SpellSchools school, bool skipDependents = false)
        {
            if (school != SpellSchools.Normal)
            {
                UnitMods unitMod = UnitMods.ResistanceStart + (int)school;
                UnitModResult modifiedRes = new();

                // Talent spell "Magic Absorption" [1650:1]^1 = 29441
                UnitMod? magicAbsorption = null;
                if (GetAuraEffectOfTalent(1650, 1, out int rank) is AuraEffect auraEffect)
                {
                    magicAbsorption = new(UnitModType.TotalPermanent)
                    {
                        Flat = new((int)(rank * 0.5f * GetLevel()))
                    };
                }

                StatMods.ApplyModsTo(modifiedRes, unitMod, myTotalPerm: magicAbsorption);

                SetResistance(school, (int)modifiedRes.TotalValue);
                UpdateResistanceBuffModForClient(school, modifiedRes.ModPos, modifiedRes.ModNeg);

                if (!skipDependents && GetPet() is Pet pet)
                    pet.UpdateResistances(school);
            }
        }

        public void UpdateDefense()
        {
            if (UpdateSkill(SkillType.Defense, WorldConfig.Values[WorldCfg.SkillGainDefense].Int32))
                UpdateDefenseBonusesMod(); // update dependent from defense skill part
        }

        void UpdateWeaponSkill(Unit victim, WeaponAttackType attType)
        {
            if (IsInFeralForm())
                return;                                             // always maximized SKILL_FERAL_COMBAT in fact

            if (GetShapeshiftForm() == ShapeShiftForm.TreeForm)
                return;                                             // use weapon but not skill up

            if (victim.GetTypeId() == TypeId.Unit && victim.ToCreature().GetCreatureTemplate().FlagsExtra.HasAnyFlag(CreatureFlagsExtra.NoSkillGains))
                return;

            int weapon_skill_gain = WorldConfig.Values[WorldCfg.SkillGainWeapon].Int32;

            Item tmpitem = GetWeaponForAttack(attType, true);
            if (tmpitem == null && attType == WeaponAttackType.BaseAttack)
            {
                // Keep unarmed & fist weapon skills in sync
                UpdateSkill(SkillType.Unarmed, weapon_skill_gain);
            }
            else if (tmpitem != null)
            {
                switch (tmpitem.GetTemplate().GetSubClass().Weapon)
                {
                    case ItemSubClassWeapon.FishingPole:
                    case ItemSubClassWeapon.Miscellaneous:
                    case ItemSubClassWeapon.Exotic:
                    case ItemSubClassWeapon.Exotic2:
                        UpdateSkill(SkillType.Unarmed, weapon_skill_gain);
                        break;
                    default:
                        UpdateSkill(tmpitem.GetSkill(), weapon_skill_gain);
                        break;
                }
            }

            UpdateCritPercentage(attType);
        }

        public void UpdateCombatSkills(Unit victim, WeaponAttackType attType, bool defense)
        {
            int plevel = GetLevel();                              // if defense than victim == attacker
            int greylevel = Formulas.GetGrayLevel(plevel);
            int moblevel = victim.GetLevelForTarget(this);

            if (moblevel > plevel + 5)
                moblevel = plevel + 5;

            int lvldif = moblevel - greylevel;
            if (lvldif < 3)
                lvldif = 3;

            int skilldif = 5 * plevel - (defense ? GetBaseDefenseSkillValue() : GetBaseWeaponSkillValue(attType));
            if (skilldif <= 0)
                return;

            float chance = (float)(3 * lvldif * skilldif) / plevel;
            if (!defense)
            {
                if (GetClass() == Class.Warrior || GetClass() == Class.Rogue)
                    chance += chance * 0.02f * GetStat(Stats.Intellect);
            }

            chance = chance < 1.0f ? 1.0f : chance;                 //minimum chance to increase skill is 1%

            if (RandomHelper.randChance(chance))
            {
                if (defense)
                    UpdateDefense();
                else
                    UpdateWeaponSkill(victim, attType);
            }
            else
                return;
        }

        ushort GetBaseWeaponSkillValue(WeaponAttackType attType)
        {
            Item item = GetWeaponForAttack(attType, true);

            // unarmed only with base attack
            if (attType != WeaponAttackType.BaseAttack && item == null)
                return 0;

            // weapon skill or (unarmed for base attack)
            SkillType skill = item != null ? item.GetSkill() : SkillType.Unarmed;
            return GetBaseSkillValue(skill);
        }

        public ushort GetBaseDefenseSkillValue()
        {
            return GetBaseSkillValue(SkillType.Defense);
        }

        void UpdateDefenseBonusesMod()
        {
            UpdateBlockPercentage();
            UpdateParryPercentage();
            UpdateDodgePercentage();
        }


        public void ApplyModTargetResistance(int mod, bool apply) { ApplyModUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModTargetResistance), mod, apply); }

        public void ApplyModTargetPhysicalResistance(int mod, bool apply) { ApplyModUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModTargetPhysicalResistance), mod, apply); }

        public void RecalculateRating(CombatRating cr) { ApplyRatingMod(cr, 0, true); }

        public void ApplyModDamageDonePos(SpellSchools school, int mod, bool apply) { ApplyModUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDonePos, (int)school), mod, apply); }

        public void ApplyModDamageDoneNeg(SpellSchools school, int mod, bool apply) { ApplyModUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDoneNeg, (int)school), mod, apply); }

        public void ApplyModDamageDonePercent(SpellSchools school, float pct, bool apply) { ApplyPercentModUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDonePercent, (int)school), pct, apply); }

        public void SetModDamageDonePercent(SpellSchools school, float pct) { SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDonePercent, (int)school), pct); }

        float OCTRegenHPPerSpirit()
        {
            var level = GetLevel();
            var pclass = GetClass();

            float baseRatio = Global.ObjectMgr.GetOCTRegenHP(pclass, level);
            float moreRatio = Global.ObjectMgr.GetRegenHPPerSpt(pclass, level);
            if (baseRatio == 0 || moreRatio == 0)
                return 0.0f;

            // Formula from PaperDollFrame script 3.3.5
            var spirit = GetStat(Stats.Spirit);
            var baseSpirit = spirit;
            if (baseSpirit > 50)
                baseSpirit = 50;
            var moreSpirit = spirit - baseSpirit;
            var regen = baseSpirit * baseRatio + moreSpirit * moreRatio;
            return regen;
        }

        float OCTRegenMPPerSpirit()
        {
            var level = GetLevel();
            var pclass = GetClass();

            //    GtOCTRegenMPEntry     const* baseRatio = sGtOCTRegenMPStore.LookupEntry((pclass-1)*GT_MAX_LEVEL + level-1);
            float moreRatio = Global.ObjectMgr.GetRegenMPPerSpt(pclass, level);
            if (moreRatio == 0)
                return 0.0f;

            // Formula get from PaperDollFrame script 3.3.5
            float spirit = GetStat(Stats.Spirit);
            float regen = spirit * moreRatio;
            return regen;
        }

        public void ApplyRatingMod(CombatRating combatRating, int value, bool apply)
        {
            baseRatingValue[(int)combatRating] += (apply ? value : -value);

            UpdateRating(combatRating);
        }

        public override void CalculateMinMaxDamage(WeaponAttackType attType, bool normalized, bool addTotalPct, out float min_damage, out float max_damage)
        {
            UnitMods unitMod;

            switch (attType)
            {
                case WeaponAttackType.BaseAttack:
                default:
                    unitMod = UnitMods.DamageMainHand;
                    break;
                case WeaponAttackType.OffAttack:
                    unitMod = UnitMods.DamageOffHand;
                    break;
                case WeaponAttackType.RangedAttack:
                    unitMod = UnitMods.DamageRanged;
                    break;
            }

            float weaponMinDamage = GetWeaponDamageRange(attType, WeaponDamageRange.MinDamage);
            float weaponMaxDamage = GetWeaponDamageRange(attType, WeaponDamageRange.MaxDamage);
            float attackPowerMod = Math.Max(GetAPMultiplier(attType, normalized), 0.25f);

            // check if player is druid and in cat or bear forms
            if (IsInFeralForm())
            {
                var lvl = GetLevel();
                if (lvl > 60)
                    lvl = 60;

                weaponMinDamage = lvl * 0.85f * attackPowerMod;
                weaponMaxDamage = lvl * 1.25f * attackPowerMod;
            }
            else if (!CanUseAttackType(attType))      //check if player not in form but still can't use (disarm case)
            {
                //cannot use ranged/off attack, set values to 0
                if (attType != WeaponAttackType.BaseAttack)
                {
                    min_damage = 0;
                    max_damage = 0;
                    return;
                }
                weaponMinDamage = SharedConst.BaseMinDamage;
                weaponMaxDamage = SharedConst.BaseMaxDamage;
            }
            else if (attType == WeaponAttackType.RangedAttack)
            {
                weaponMinDamage += AmmoDPS * attackPowerMod;
                weaponMaxDamage += AmmoDPS * attackPowerMod;
            }

            UnitModResult minDamageValue = new(weaponMinDamage);
            UnitModResult maxDamageValue = new(weaponMaxDamage);

            UnitMod baseValue = new(UnitModType.BasePermanent)
            {
                Flat = new((int)(GetTotalAttackPowerValue(attType) / 14.0f * attackPowerMod))
            };

            UnitMod? totalValue = StatMods.Get(UnitMods.DamagePhysical);

            var cache = StatMods.ApplyModsTo(minDamageValue, unitMod, myBasePerm: baseValue, myTotalTemp: totalValue).ReApplyTo(maxDamageValue);

            min_damage = minDamageValue.TotalValue;
            max_damage = maxDamageValue.TotalValue;

            // wotlk_classic client doesn't show this multiplier for particular attack type (Are UpdateFields wrong?)
            SetWeaponDmgMultiplier(cache.TotalPermanent.HasValue ? cache.TotalPermanent.Value.Mult: 1.0f, attType);
            SetModDamageDonePercent(SpellSchools.Normal, cache.TotalTemporary.HasValue ? cache.TotalTemporary.Value.Mult : 1.0f);
        }

        public float GetMeleeCritFromAgility()
        {
            int level = GetLevel();
            Class pclass = GetClass();

            if (level > SharedConst.GTMaxLevel)
                level = SharedConst.GTMaxLevel;

            GtChanceToMeleeCritBaseRecord critBase = CliDB.ChanceToMeleeCritBaseGameTable.GetRow(1);
            GtChanceToMeleeCritRecord critRatio = CliDB.ChanceToMeleeCritGameTable.GetRow(level);
            if (critBase == null || critRatio == null)
                return 0.0f;

            float crit = CliDB.GetGameTableColumnForClass(critBase, pclass) + GetStat(Stats.Agility) * CliDB.GetGameTableColumnForClass(critRatio, pclass);
            return crit * 100.0f;
        }

        public void UpdateAllCritPercentages()
        {
            float value = GetMeleeCritFromAgility();

            SetBaseModPctValue(BaseModGroup.CritPercentage, value);
            SetBaseModPctValue(BaseModGroup.OffhandCritPercentage, value);
            SetBaseModPctValue(BaseModGroup.RangedCritPercentage, value);
        }

        public void UpdatePowerRegen(PowerType power)
        {
            var powerIndex = GetPowerIndex(power);
            if (powerIndex == (int)PowerType.Max)
                return;

            PowerTypeRecord powerInfo = Global.DB2Mgr.GetPowerTypeEntry(power);
            if (powerInfo == null)
                return;

            float mod_regen = 0.0f; // Out-of-combat / without last mana use effect
            float mod_regen_interrupted = 0.0f; // In combat / with last mana use effect

            if (power == PowerType.Mana)
            {
                // Aura of Despair [62692] - only
                if (HasAuraTypeWithValue(AuraType.PreventRegeneratePower, (int)power))
                {
                    SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PowerRegenFlatModifier, (int)power), 0);
                    SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PowerRegenInterruptedFlatModifier, (int)power), 0);
                    return;
                }

                float Intellect = GetStat(Stats.Intellect);
                // Mana regen from spirit and intellect
                float power_regen = (float)Math.Sqrt(Intellect) * OCTRegenMPPerSpirit();
                // Apply PCT bonus from SPELL_AURA_MOD_POWER_REGEN_PERCENT aura on spirit base regen
                power_regen *= GetTotalAuraMultiplierByMiscValue(AuraType.ModPowerRegenPercent, (int)PowerType.Mana);

                // Mana regen from SPELL_AURA_MOD_POWER_REGEN aura
                float power_regen_mp5 = (GetTotalAuraModifierByMiscValue(AuraType.ModPowerRegen, (int)PowerType.Mana) + m_baseManaRegen) / 5.0f;

                // Get bonus from SPELL_AURA_MOD_MANA_REGEN_FROM_STAT aura
                foreach (var aura in GetAuraEffectsByType(AuraType.ModManaRegenFromStat))
                    power_regen_mp5 += GetStat((Stats)aura.GetMiscValue()) * aura.GetAmount() / 500.0f;

                // Set regen rate in cast state apply only on spirit based regen
                int modManaRegenInterrupt = GetTotalAuraModifier(AuraType.ModManaRegenInterrupt);
                if (modManaRegenInterrupt > 100)
                    modManaRegenInterrupt = 100;

                mod_regen = power_regen_mp5 + power_regen;
                mod_regen_interrupted = power_regen_mp5 + MathFunctions.CalculatePct(power_regen, modManaRegenInterrupt);
            }
            else
            {
                float multModifier = GetTotalAuraMultiplierByMiscValue(AuraType.ModPowerRegenPercent, (int)power);
                float flatModifier = GetTotalAuraModifierByMiscValue(AuraType.ModPowerRegen, (int)power) / 5.0f;
                if (flatModifier > 0)
                    flatModifier *= multModifier;

                // Peace regen mod
                {
                    mod_regen = powerInfo.RegenPeace;
                    if (powerInfo.RegenPeace > 0)
                        mod_regen *= multModifier;

                    if (power != PowerType.RunicPower) // Butchery [48979]^1 requires combat
                        mod_regen += flatModifier;

                    // Unit fields contain an offset relative to the base power regeneration.
                    mod_regen -= powerInfo.RegenPeace;
                }

                // Combat regen mod
                {
                    mod_regen_interrupted = powerInfo.RegenCombat;
                    if (powerInfo.RegenCombat > 0)
                        mod_regen_interrupted *= multModifier;

                    mod_regen_interrupted += flatModifier;

                    // Unit fields contain an offset relative to the base power regeneration.
                    mod_regen_interrupted -= powerInfo.RegenCombat;
                }
            }

            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PowerRegenFlatModifier, powerIndex), mod_regen);
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PowerRegenInterruptedFlatModifier, powerIndex), mod_regen_interrupted);
        }

        public void UpdateSpellDamageAndHealingBonus()
        {
            // Magic damage modifiers implemented in Unit.SpellDamageBonusDone
            // This information for client side use only
            // Get healing bonus for all schools
            SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModHealingDonePos), SpellBaseHealingBonusDone(SpellSchoolMask.All));
            // Get damage bonus for all schools
            var modDamageAuras = GetAuraEffectsByType(AuraType.ModDamageDone);
            for (SpellSchools i = SpellSchools.Holy; i < SpellSchools.Max; ++i)
            {
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDoneNeg, (int)i), (int)modDamageAuras.Aggregate(0.0f, (negativeMod, aurEff) =>
                {
                    SpellSchoolMask spellSchoolMask = (SpellSchoolMask)aurEff.GetMiscValue();
                    if (aurEff.GetAmount() < 0 && spellSchoolMask.HasSchool(i))
                        negativeMod += aurEff.GetAmount();
                    return negativeMod;
                }));
                SetUpdateFieldStatValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDonePos, (int)i),
                    SpellBaseDamageBonusDone(i.GetSpellSchoolMask()) - m_activePlayerData.ModDamageDoneNeg[(int)i]);
            }
        }

        public int GetBaseSpellPowerBonus() { return m_baseSpellPower; }

        public void ApplyFeralAPBonus(int amount, bool apply)
        {
            _Modify(apply, ref m_baseFeralAP, ref amount);
            UpdateMeleeAttackPowerAndDamage();
        }

        public override void UpdateDamagePhysical(WeaponAttackType attType)
        {
            if (attType == WeaponAttackType.OffAttack)
            {
                if (GetWeaponForAttack(WeaponAttackType.OffAttack, true) is Item offhand)
                {
                    if (CanDualWield() || offhand.GetTemplate().HasFlag(ItemFlags3.AlwaysAllowDualWield))
                        base.UpdateDamagePhysical(WeaponAttackType.OffAttack);
                }
            }
            else
            {
                base.UpdateDamagePhysical(attType);
            }
        }

        public override void UpdateDamageSpell(SpellSchools school)
        {
            SetModDamageDonePercent(school, StatMods.GetOrDefault(UnitMods.SpellDamageStart + (int)school).Mult);
        }

        public override void UpdateMeleeAttackPowerAndDamage(bool skipDependents = false)
        {
            float baseValue;
            float level = GetLevel();

            var entry = CliDB.ChrClassesStorage.LookupByKey(GetClass());

            float strengthValue = Math.Max(GetStat(Stats.Strength) * entry.AttackPowerPerStrength, 0.0f);
            float agilityValue = Math.Max(GetStat(Stats.Agility) * entry.AttackPowerPerAgility, 0.0f);

            float classSpecificBonus = GetClass() switch
            {
                Class.Warrior or Class.Paladin or Class.DeathKnight
                    => level * 3.0f - 20.0f,

                Class.Rogue or Class.Hunter or Class.Shaman
                    => level * 2.0f - 20.0f,

                _ => -10f
            };

            if (GetClass() == Class.Druid)
            {
                float levelMultBonus = 1.0f;
                float weaponFlatBonus = 0.0f;

                ShapeShiftForm form = GetShapeshiftForm();
                if (IsFeralForm(form))
                {
                    // Talent: Predatory Strikes [803:0]^1 = 16972
                    if (GetAuraEffectOfTalent(803, 0) is AuraEffect levelMod)
                        levelMultBonus = MathFunctions.CalculatePct(1.0f, levelMod.GetAmount());

                    // = 0 if removing the weapon, do not calculate bonus (uses template)
                    if (m_baseFeralAP > 0)
                    {
                        if (m_items[EquipmentSlot.MainHand] is Item weapon)
                        {
                            // Talent: Predatory Strikes [803:1]^1 = 16972
                            if (GetAuraEffectOfTalent(803, 1) is AuraEffect weaponMod)
                            {
                                float APBonus = m_baseFeralAP /*+ weapon.GetTemplate().GetTotalAPBonus() - what a strange mechanic?*/;
                                weaponFlatBonus = MathFunctions.CalculatePct(APBonus, weaponMod.GetAmount());
                            }
                        }
                    }
                }

                classSpecificBonus = form switch
                {
                    ShapeShiftForm.CatForm or ShapeShiftForm.BearForm or ShapeShiftForm.DireBearForm
                        => level * levelMultBonus - 20.0f + m_baseFeralAP + weaponFlatBonus,

                    ShapeShiftForm.MoonkinForm
                        => -20.0f + m_baseFeralAP,

                    _ => -20f
                };
            }

            baseValue = strengthValue + agilityValue + classSpecificBonus;

            UnitMods unitMod = UnitMods.AttackPowerMelee;
            UnitModResult attPowerValue = new(baseValue);
            UnitMod bonusPowerPerm = new(UnitModType.TotalPermanent);
            UnitMod bonusPowerTemp = new(UnitModType.TotalTemporary);

            // Bonus from stats (talents)
            {
                var mAPbyStat = GetAuraEffectsByType(AuraType.ModMeleeAttackPowerOfStatPercent);
                foreach (var aurEff in mAPbyStat)
                {
                    float bonus = MathFunctions.CalculatePct(GetStat((Stats)aurEff.GetMiscValue()), aurEff.GetAmount());
                    if (aurEff.GetSpellInfo().IsPassive())
                        bonusPowerPerm.Flat.Modify((int)bonus, true);
                    else
                        bonusPowerTemp.Flat.Modify((int)bonus, true);
                }
            }

            // Bonus from armor (talents)
            //{
            //    // amount updated in PeriodicTick each 30 seconds
            //    float bonus = GetTotalAuraModifier(AuraType.MOD_ATTACK_POWER_OF_ARMOR));

            //    if (bonus != FlatModifier.IdleModifier)
            //    {
            //        bonusPower.Flat.Modify((int)bonus, true);
            //    }
            //}

            StatMods.ApplyModsTo(attPowerValue, unitMod, myTotalPerm: bonusPowerPerm, myTotalTemp: bonusPowerTemp);

            SetAttackPower((int)attPowerValue.NakedValue);
            SetAttackPowerModPos((int)attPowerValue.ModPos);
            SetAttackPowerModNeg((int)attPowerValue.ModNeg);
            SetAttackPowerMultiplier(1.0f);

            if (skipDependents)
                return;

            //automatically update weapon damage after attack power modification
            UpdateDamagePhysical(WeaponAttackType.BaseAttack);
            UpdateDamagePhysical(WeaponAttackType.OffAttack);

            if (HasAuraType(AuraType.OverrideSpellPowerByApPct))
                UpdateSpellDamageAndHealingBonus();

            Pet pet = GetPet();
            Guardian guardian = GetGuardianPet();

            if (pet != null && pet.IsPetGhoul()) // At melee attack power change for DK pet
                pet.UpdateMeleeAttackPowerAndDamage();

            if (guardian != null && guardian.IsSpiritWolf()) // At melee attack power change for Shaman feral spirit
                guardian.UpdateMeleeAttackPowerAndDamage();
        }

        public override void UpdateRangedAttackPowerAndDamage(bool skipDependents = false)
        {
            float baseValue;
            float level = GetLevel();

            var entry = CliDB.ChrClassesStorage.LookupByKey(GetClass());

            float agilityValue = Math.Max(GetStat(Stats.Agility) * entry.RangedAttackPowerPerAgility, 0.0f);
            float classSpecificBonus = GetClass() switch
            {
                Class.Warrior or Class.Rogue => level - 20.0f,
                Class.Hunter => level * 2.0f - 20.0f,
                _ => -10f
            };

            if (IsInFeralForm())
                baseValue = 0.0f;
            else
                baseValue = agilityValue + classSpecificBonus;

            UnitMods unitMod = UnitMods.AttackPowerRanged;
            UnitModResult attPowerValue = new(baseValue);
            UnitMod bonusPowerPerm = new(UnitModType.TotalPermanent);
            UnitMod bonusPowerTemp = new(UnitModType.TotalTemporary);

            // Bonus from stats (talents)
            {
                foreach (var aurEff in GetAuraEffectsByType(AuraType.ModRangedAttackPowerOfStatPercent))
                {
                    float bonus = MathFunctions.CalculatePct(GetStat((Stats)aurEff.GetMiscValue()), aurEff.GetAmount());
                    if (aurEff.GetSpellInfo().IsPassive())
                        bonusPowerPerm.Flat.Modify((int)bonus, true);
                    else
                        bonusPowerTemp.Flat.Modify((int)bonus, true);
                }
            }

            // Bonus from armor (talents)
            //{
            //    // amount updated in PeriodicTick each 30 seconds
            //    float bonus = GetTotalAuraModifier(AuraType.MOD_ATTACK_POWER_OF_ARMOR));

            //    if (bonus != FlatModifier.IdleModifier)
            //    {
            //        bonusPower.Flat.Modify((int)bonus, true);
            //    }
            //}

            StatMods.ApplyModsTo(attPowerValue, unitMod, myTotalPerm: bonusPowerPerm, myTotalTemp: bonusPowerTemp);

            SetRangedAttackPower((int)attPowerValue.NakedValue);
            SetRangedAttackPowerModPos((int)attPowerValue.ModPos);
            SetRangedAttackPowerModNeg((int)attPowerValue.ModNeg);
            SetRangedAttackPowerMultiplier(1.0f);

            if (skipDependents)
                return;

            //automatically update weapon damage after attack power modification
            UpdateDamagePhysical(WeaponAttackType.RangedAttack);

            Pet pet = GetPet();

            if (pet != null && pet.IsHunterPet()) // At ranged attack change for hunter pet
                pet.UpdateMeleeAttackPowerAndDamage();
        }

        public void UpdateShieldBlockValue()
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ShieldBlock), GetShieldBlockValue());
        }

        public override void UpdateArmor(bool skipDependents = false)
        {
            UnitMods unitMod = UnitMods.Armor;
            UnitModResult armorValue = new();
            UnitMod bonusArmor = new(UnitModType.TotalPermanent)
            {
                // Add armor from agility
                Flat = new((int)(GetStat(Stats.Agility) * 2.0f))
            };

            // Add dynamic flat mods (talents)
            foreach (var aurEff in GetAuraEffectsByType(AuraType.ModResistanceOfStatPercent))
            {
                SpellSchoolMask schoolsMask = (SpellSchoolMask)aurEff.GetMiscValue();
                Stats stat = (Stats)aurEff.GetMiscValueB();

                if (schoolsMask.HasSchool(SpellSchools.Normal))
                {
                    bonusArmor.Flat.Modify((int)MathFunctions.CalculatePct(GetStat(stat), aurEff.GetAmount()), true);
                }
            }

            StatMods.ApplyModsTo(armorValue, unitMod, myTotalPerm: bonusArmor);

            SetArmor((int)armorValue.TotalValue);
            UpdateResistanceBuffModForClient(SpellSchools.Normal, armorValue.ModPos, armorValue.ModNeg);

            if (skipDependents)
                return;

            Pet pet = GetPet();
            if (pet != null)
                pet.UpdateArmor();

            // armor dependent auras update for SPELL_AURA_MOD_ATTACK_POWER_OF_ARMOR
            UpdateMeleeAttackPowerAndDamage();
        }

        void _ApplyAllStatBonuses()
        {
            SetCanModifyStats(false);

            _ApplyAllAuraStatMods();
            _ApplyAllItemMods();

            SetCanModifyStats(true);

            UpdateAllStats();
        }

        void _RemoveAllStatBonuses()
        {
            SetCanModifyStats(false);

            _RemoveAllItemMods();
            _RemoveAllAuraStatMods();

            SetCanModifyStats(true);

            UpdateAllStats();
        }

        void UpdateAllRatings()
        {
            for (CombatRating cr = 0; cr < CombatRating.Max; ++cr)
                UpdateRating(cr);
        }

        public void UpdateRating(CombatRating cr)
        {
            int amount = baseRatingValue[(int)cr];

            foreach (AuraEffect aurEff in GetAuraEffectsByType(AuraType.ModCombatRatingFromCombatRating))
            {
                if ((aurEff.GetMiscValueB() & (1 << (int)cr)) != 0)
                {
                    short? highestRating = null;
                    for (byte dependentRating = 0; dependentRating < (int)CombatRating.Max; ++dependentRating)
                    {
                        if ((aurEff.GetMiscValue() & (1 << dependentRating)) != 0)
                            highestRating = (short)Math.Max(highestRating.HasValue ? highestRating.Value : baseRatingValue[dependentRating], baseRatingValue[dependentRating]);
                    }

                    if (highestRating != 0)
                        amount += MathFunctions.CalculatePct(highestRating.Value, aurEff.GetAmount());
                }
            }

            foreach (var aurEff in GetAuraEffectsByType(AuraType.ModRatingPct))
            {
                if (aurEff.GetMiscValue().HasAnyFlag(1 << (int)cr))
                    amount += MathFunctions.CalculatePct(amount, aurEff.GetAmount());
            }

            if (amount < 0)
                amount = 0;

            int oldRating = m_activePlayerData.CombatRatings[(int)cr];
            SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CombatRatings, (int)cr), amount);

            bool affectStats = CanModifyStats();

            switch (cr)
            {
                case CombatRating.WeaponSkill:
                case CombatRating.DefenseSkill:
                    break;
                case CombatRating.Dodge:
                    UpdateDodgePercentage();
                    break;
                case CombatRating.Parry:
                    UpdateParryPercentage();
                    break;
                case CombatRating.Block:
                    UpdateBlockPercentage();
                    break;
                case CombatRating.HitMelee:
                    UpdateMeleeHitChances();
                    break;
                case CombatRating.HitRanged:
                    UpdateRangedHitChances();
                    break;
                case CombatRating.HitSpell:
                    UpdateSpellHitChances();
                    break;
                case CombatRating.CritMelee:
                    if (affectStats)
                    {
                        UpdateCritPercentage(WeaponAttackType.BaseAttack);
                        UpdateCritPercentage(WeaponAttackType.OffAttack);
                    }
                    break;
                case CombatRating.CritRanged:
                    if (affectStats)
                        UpdateCritPercentage(WeaponAttackType.RangedAttack);
                    break;
                case CombatRating.CritSpell:
                    if (affectStats)
                        UpdateAllSpellCritChances();
                    break;
                case CombatRating.HitTakenMelee:    // Implemented in Unit::MeleeMissChanceCalc
                case CombatRating.HitTakenRanged:
                    break;
                case CombatRating.CritTakenMelee:   // Implemented in Unit::RollMeleeOutcomeAgainst (only for chance to crit)
                case CombatRating.CritTakenRanged:
                    break;
                case CombatRating.CritTakenSpell:   // Implemented in Unit::SpellCriticalBonus (only for chance to crit)
                    break;
                case CombatRating.HasteMelee:
                case CombatRating.HasteRanged:
                case CombatRating.HasteSpell:
                {
                    // explicit affected values
                    float multiplier = GetRatingMultiplier(cr);
                    float oldVal = oldRating * multiplier;
                    float newVal = amount * multiplier;
                    switch (cr)
                    {
                        case CombatRating.HasteMelee:
                            ApplyAttackTimePercentMod(WeaponAttackType.BaseAttack, oldVal, false);
                            ApplyAttackTimePercentMod(WeaponAttackType.OffAttack, oldVal, false);
                            ApplyAttackTimePercentMod(WeaponAttackType.BaseAttack, newVal, true);
                            ApplyAttackTimePercentMod(WeaponAttackType.OffAttack, newVal, true);
                            break;
                        case CombatRating.HasteRanged:
                            ApplyAttackTimePercentMod(WeaponAttackType.RangedAttack, oldVal, false);
                            ApplyAttackTimePercentMod(WeaponAttackType.RangedAttack, newVal, true);
                            break;
                        case CombatRating.HasteSpell:
                            ApplyCastTimePercentMod(oldVal, false);
                            ApplyCastTimePercentMod(newVal, true);
                            break;
                        default:
                            break;
                    }
                    break;
                }
                case CombatRating.WeaponSkillMainhand:  // Implemented in Unit::RollMeleeOutcomeAgainst
                case CombatRating.WeaponSkillOffhand:
                case CombatRating.WeaponSkillRanged:
                    break;
                case CombatRating.Expertise:
                    if (affectStats)
                    {
                        UpdateExpertise(WeaponAttackType.BaseAttack);
                        UpdateExpertise(WeaponAttackType.OffAttack);
                    }
                    break;
                case CombatRating.ArmorPenetration:
                    if (affectStats)
                        UpdateArmorPenetration(amount);
                    break;
            }
        }

        public void UpdateMastery()
        {
            if (!CanUseMastery())
            {
                SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Mastery), 0.0f);
                return;
            }

            float value = GetTotalAuraModifier(AuraType.Mastery);
            //value += GetRatingBonusValue(CombatRating.Mastery);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Mastery), value);

            ChrSpecializationRecord chrSpec = GetPrimarySpecializationEntry();
            if (chrSpec == null)
                return;

            foreach (int masterySpellId in chrSpec.MasterySpellID)
            {
                Aura aura = GetAura(masterySpellId);
                if (aura != null)
                {
                    foreach (var spellEffectInfo in aura.GetSpellInfo().GetEffects())
                    {
                        float mult = spellEffectInfo.BonusCoefficient;
                        if (MathFunctions.fuzzyEq(mult, 0.0f))
                            continue;

                        aura.GetEffect(spellEffectInfo.EffectIndex).ChangeAmount((int)(value * mult));
                    }
                }
            }
        }

        public void UpdateVersatilityDamageDone()
        {
            // No proof that CR_VERSATILITY_DAMAGE_DONE is allways = ActivePlayerData::Versatility
            //SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Versatility), (int)m_activePlayerData.CombatRatings[(int)CombatRating.VersatilityDamageDone]);

            if (GetClass() == Class.Hunter)
                UpdateDamagePhysical(WeaponAttackType.RangedAttack);
            else
                UpdateDamagePhysical(WeaponAttackType.BaseAttack);
        }

        public void UpdateHealingDonePercentMod()
        {
            float value = 1.0f;

            //MathFunctions.AddPct(ref value, GetRatingBonusValue(CombatRating.VersatilityHealingDone) + GetTotalAuraModifier(AuraType.ModVersatility));

            foreach (AuraEffect auraEffect in GetAuraEffectsByType(AuraType.ModHealingDonePercent))
                MathFunctions.AddPct(ref value, auraEffect.GetAmount());

            SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModHealingDonePercent), value);
        }

        void UpdateArmorPenetration(int amount)
        {
            // Store Rating Value
            SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CombatRatings, (int)CombatRating.ArmorPenetration), amount);
        }

        float CalculateDiminishingReturns(float[] capArray, Class playerClass, float nonDiminishValue, float diminishValue)
        {
            float[] m_diminishing_k =
            [
                0.9560f,  // Warrior
                0.9560f,  // Paladin
                0.9880f,  // Hunter
                0.9880f,  // Rogue
                0.9830f,  // Priest
                0.9560f,  // DK
                0.9880f,  // Shaman
                0.9830f,  // Mage
                0.9830f,  // Warlock
                0.0f,     // Monk
                0.9720f   // Druid
            ];

            //  1     1     k              cx
            // --- = --- + --- <=> x' = --------
            //  x'    c     x            x + ck

            // where:
            // k  is m_diminishing_k for that class
            // c  is capArray for that class
            // x  is Chance before DR (diminishValue)
            // x' is Chance after DR (our result)

            int classIdx = (int)playerClass - 1;

            float k = m_diminishing_k[classIdx];
            float c = capArray[classIdx];

            float result = c * diminishValue / (diminishValue + c * k);
            result += nonDiminishValue;
            return result;
        }

        float[] parry_cap =
        [

            47.003525f,     // Warrior
            47.003525f,     // Paladin
            145.560408f,    // Hunter
            145.560408f,    // Rogue
            0.0f,           // Priest
            47.003525f,     // DK
            145.560408f,    // Shaman
            0.0f,           // Mage
            0.0f,           // Warlock
            0.0f,           // Monk
            0.0f            // Druid
        ];

        public void UpdateParryPercentage()
        {
            // No parry
            float value = 0.0f;
            int pclass = (int)GetClass() - 1;
            if (CanParry() && parry_cap[pclass] > 0.0f)
            {
                float nondiminishing = 5.0f;
                // Parry from rating
                float diminishing = GetRatingBonusValue(CombatRating.Parry);
                // Modify value from defense skill (only bonus from defense rating diminishes)
                nondiminishing += (GetSkillValue(SkillType.Defense) - GetMaxSkillValueForLevel()) * 0.04f;
                diminishing += GetRatingBonusValue(CombatRating.DefenseSkill) * 0.04f;
                // Parry from SPELL_AURA_MOD_PARRY_PERCENT aura
                nondiminishing += GetTotalAuraModifier(AuraType.ModParryPercent);

                // apply diminishing formula to diminishing parry Chance
                value = CalculateDiminishingReturns(parry_cap, GetClass(), nondiminishing, diminishing);

                if (WorldConfig.Values[WorldCfg.StatsLimitsEnable].Bool)
                    value = value > WorldConfig.Values[WorldCfg.StatsLimitsParry].Float ? WorldConfig.Values[WorldCfg.StatsLimitsParry].Float : value;

                value = Math.Max(0.0f, value);
            }

            SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ParryPercentage), value);
        }

        float[] dodge_cap =
        [
            88.129021f,     // Warrior
            88.129021f,     // Paladin
            145.560408f,    // Hunter
            145.560408f,    // Rogue
            150.375940f,    // Priest
            88.129021f,     // DK
            145.560408f,    // Shaman
            150.375940f,    // Mage
            150.375940f,    // Warlock
            0.0f,           // Monk
            116.890707f     // Druid
        ];

        public void UpdateDodgePercentage()
        {
            float diminishing = 0.0f, nondiminishing = 0.0f;
            GetDodgeFromAgility(ref diminishing, ref nondiminishing);
            // Modify value from defense skill (only bonus from defense rating diminishes)
            nondiminishing += (GetSkillValue(SkillType.Defense) - GetMaxSkillValueForLevel()) * 0.04f;
            diminishing += GetRatingBonusValue(CombatRating.DefenseSkill) * 0.04f;
            // Dodge from SPELL_AURA_MOD_DODGE_PERCENT aura
            nondiminishing += GetTotalAuraModifier(AuraType.ModDodgePercent);
            // Dodge from rating
            diminishing += GetRatingBonusValue(CombatRating.Dodge);

            // apply diminishing formula to diminishing dodge Chance
            float value = CalculateDiminishingReturns(dodge_cap, GetClass(), nondiminishing, diminishing);

            if (WorldConfig.Values[WorldCfg.StatsLimitsEnable].Bool)
                value = value > WorldConfig.Values[WorldCfg.StatsLimitsDodge].Float ? WorldConfig.Values[WorldCfg.StatsLimitsDodge].Float : value;

            value = Math.Max(0.0f, value);
            SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.DodgePercentage), value);
        }

        public void UpdateBlockPercentage()
        {
            // No block
            float value = 0.0f;
            if (CanBlock())
            {
                // Base value
                value = 5.0f;
                // Modify value from defense skill
                value += (GetDefenseSkillValue() - GetMaxSkillValueForLevel()) * 0.04f;
                // Increase from SPELL_AURA_MOD_BLOCK_PERCENT aura
                value += GetTotalAuraModifier(AuraType.ModBlockPercent);
                // Increase from rating
                value += GetRatingBonusValue(CombatRating.Block);

                if (WorldConfig.Values[WorldCfg.StatsLimitsEnable].Bool)
                    value = value > WorldConfig.Values[WorldCfg.StatsLimitsBlock].Float ? WorldConfig.Values[WorldCfg.StatsLimitsBlock].Float : value;

                value = Math.Max(0.0f, value);
            }

            SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.BlockPercentage), value);
        }

        public void UpdateCritPercentage(WeaponAttackType attType)
        {
            BaseModGroup modGroup;
            UpdateField<float> updateField;
            CombatRating combatRating;

            switch (attType)
            {
                case WeaponAttackType.OffAttack:
                    modGroup = BaseModGroup.OffhandCritPercentage;
                    updateField = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.OffhandCritPercentage);
                    combatRating = CombatRating.CritMelee;
                    break;
                case WeaponAttackType.RangedAttack:
                    modGroup = BaseModGroup.RangedCritPercentage;
                    updateField = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.RangedCritPercentage);
                    combatRating = CombatRating.CritRanged;
                    break;
                case WeaponAttackType.BaseAttack:
                default:
                    modGroup = BaseModGroup.CritPercentage;
                    updateField = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CritPercentage);
                    combatRating = CombatRating.CritMelee;
                    break;
            }

            // flat = bonus from crit auras, pct = bonus from agility, combat rating = mods from items
            float value = GetBaseModValue(modGroup, BaseModType.FlatMod) + GetBaseModValue(modGroup, BaseModType.PctMod) + GetRatingBonusValue(combatRating);

            // Modify crit from weapon skill and maximized defense skill of same level victim difference
            value += (GetWeaponSkillValue(attType) - GetMaxSkillValueForLevel()) * 0.04f;

            if (WorldConfig.Values[WorldCfg.StatsLimitsEnable].Bool)
                value = value > WorldConfig.Values[WorldCfg.StatsLimitsCrit].Float ? WorldConfig.Values[WorldCfg.StatsLimitsCrit].Float : value;

            value = Math.Max(0.0f, value);
            SetUpdateFieldStatValue(updateField, value);
        }

        public void UpdateExpertise(WeaponAttackType attack)
        {
            if (attack == WeaponAttackType.RangedAttack)
                return;

            int expertise = (int)GetRatingBonusValue(CombatRating.Expertise);

            Item weapon = GetWeaponForAttack(attack, true);

            expertise += GetTotalAuraModifier(AuraType.ModExpertise, aurEff => aurEff.GetSpellInfo().IsItemFitToSpellRequirements(weapon));

            if (expertise < 0)
                expertise = 0;

            switch (attack)
            {
                case WeaponAttackType.BaseAttack:
                    SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.MainhandExpertise), expertise);
                    break;
                case WeaponAttackType.OffAttack:
                    SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.OffhandExpertise), expertise);
                    break;
                default: break;
            }
        }

        float GetGameTableColumnForCombatRating(GtCombatRatingsRecord row, CombatRating rating)
        {
            switch (rating)
            {
                case CombatRating.WeaponSkill:
                    return row.WeaponSkill;
                case CombatRating.DefenseSkill:
                    return row.DefenseSkill;
                case CombatRating.Dodge:
                    return row.Dodge;
                case CombatRating.Parry:
                    return row.Parry;
                case CombatRating.Block:
                    return row.Block;
                case CombatRating.HitMelee:
                    return row.HitMelee;
                case CombatRating.HitRanged:
                    return row.HitRanged;
                case CombatRating.HitSpell:
                    return row.HitSpell;
                case CombatRating.CritMelee:
                    return row.CritMelee;
                case CombatRating.CritRanged:
                    return row.CritRanged;
                case CombatRating.CritSpell:
                    return row.CritSpell;
                case CombatRating.HitTakenMelee:
                    return row.HitTakenMelee;
                case CombatRating.HitTakenRanged:
                    return row.HitTakenRanged;
                case CombatRating.HitTakenSpell:
                    return row.HitTakenSpell;
                case CombatRating.CritTakenMelee:
                    return row.CritTakenMelee;
                case CombatRating.CritTakenRanged:
                    return row.CritTakenRanged;
                case CombatRating.CritTakenSpell:
                    return row.CritTakenSpell;
                case CombatRating.HasteMelee:
                    return row.HasteMelee;
                case CombatRating.HasteRanged:
                    return row.HasteRanged;
                case CombatRating.HasteSpell:
                    return row.HasteSpell;
                default:
                    break;
            }
            return 1.0f;
        }

        public float GetSpellCritFromIntellect()
        {
            int level = GetLevel();
            Class pclass = GetClass();

            if (level > SharedConst.GTMaxLevel)
                level = SharedConst.GTMaxLevel;

            GtChanceToSpellCritBaseRecord critBase = CliDB.ChanceToSpellCritBaseGameTable.GetRow(1);
            GtChanceToSpellCritRecord critRatio = CliDB.ChanceToSpellCritGameTable.GetRow(level);

            if (critBase == null || critRatio == null)
                return 0;

            float crit = CliDB.GetGameTableColumnForClass(critBase, pclass) + GetStat(Stats.Intellect) * CliDB.GetGameTableColumnForClass(critRatio, pclass);

            return crit * 100.0f;
        }

        public void UpdateSpellCritChance(SpellSchools school)
        {
            float crit = 0.0f;

            // For normal school set zero crit chance
            if (school != SpellSchools.Normal)
            {
                // For others recalculate it from:
                // Crit from Intellect
                crit += GetSpellCritFromIntellect();
                // Increase crit from AuraType.ModSpellCritSchoolPct
                crit += GetTotalAuraModifierByMiscMask(AuraType.ModSpellCritSchoolChance, (uint)school.GetSpellSchoolMask());
                // Increase crit from AuraType.ModSpellCritPct
                crit += GetTotalAuraModifier(AuraType.ModSpellCritChance);
                // Increase crit from spell crit ratings
                crit += GetRatingBonusValue(CombatRating.CritSpell);
            }

            // Store crit value
            SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SpellCritPercentage, (int)school), crit);
        }

        public void UpdateMeleeHitChances()
        {
            ModMeleeHitChance = GetRatingBonusValue(CombatRating.HitMelee);
        }

        public void UpdateRangedHitChances()
        {
            ModRangedHitChance = GetRatingBonusValue(CombatRating.HitRanged);
        }

        public void UpdateSpellHitChances()
        {
            ModSpellHitChance = GetTotalAuraModifier(AuraType.ModSpellHitChance);
            ModSpellHitChance += GetRatingBonusValue(CombatRating.HitSpell);
        }

        public override void UpdateMaxHealth()
        {
            UnitMods unitMod = UnitMods.Health;
            UnitModResult value = new(GetCreateHealth());
            UnitMod healthFromStamina = new(UnitModType.TotalPermanent)
            {
                Flat = new((int)GetHealthBonusFromStamina())
            };

            StatMods.ApplyModsTo(value, unitMod, myTotalPerm: healthFromStamina);

            SetMaxHealth((uint)value.TotalValue);
        }

        float GetHealthBonusFromStamina()
        {
            float stamina = GetStat(Stats.Stamina);
            float baseStam = Math.Min(20f, stamina);
            float moreStam = stamina - baseStam;

            return baseStam + (moreStam * 10.0f);
        }

        float GetManaBonusFromIntellect()
        {
            float intellect = GetStat(Stats.Intellect);

            float baseInt = Math.Min(20.0f, intellect);
            float moreInt = intellect - baseInt;

            return baseInt + (moreInt * 15.0f);
        }

        public override int GetPowerIndex(PowerType powerType)
        {
            return Global.DB2Mgr.GetPowerIndexByClass(powerType, GetClass());
        }

        public override void UpdateMaxPower(PowerType power)
        {
            var powerIndex = GetPowerIndex(power);
            if (powerIndex == (int)PowerType.Max || powerIndex >= (int)PowerType.MaxPerClass)
                return;

            UnitMods unitMod = UnitMods.PowerStart + (int)power;
            UnitModResult value = new(GetCreatePowerValue(power));
            UnitMod? powerFromIntellect = default;

            float bonusPower = (power == PowerType.Mana && GetCreatePowerValue(power) > 0) ? GetManaBonusFromIntellect() : 0;
            if (bonusPower != FlatModifier.IdleModifier)
            {
                powerFromIntellect = new(UnitModType.TotalPermanent)
                {
                    Flat = new((int)bonusPower)
                };
            }

            StatMods.ApplyModsTo(value, unitMod, myTotalPerm: powerFromIntellect);

            SetMaxPower(power, (int)Math.Round(value.TotalValue));
        }

        public void ApplySpellPenetrationBonus(int amount, bool apply)
        {
            ApplyModTargetResistance(-amount, apply);
            m_spellPenetrationItemMod += apply ? amount : -amount;
        }

        void ApplyManaRegenBonus(int amount, bool apply)
        {
            _Modify(apply, ref m_baseManaRegen, ref amount);
            UpdatePowerRegen(PowerType.Mana);
        }

        void ApplyHealthRegenBonus(int amount, bool apply)
        {
            _Modify(apply, ref m_baseHealthRegen, ref amount);
        }

        void ApplySpellPowerBonus(int amount, bool apply)
        {
            if (HasAuraType(AuraType.OverrideSpellPowerByApPct))
                return;

            apply = _Modify(apply, ref m_baseSpellPower, ref amount);

            // For speed just update for client
            ApplyModUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModHealingDonePos), amount, apply);
            for (SpellSchools spellSchool = SpellSchools.Holy; spellSchool < SpellSchools.Max; ++spellSchool)
                ApplyModDamageDonePos(spellSchool, amount, apply);

            if (HasAuraType(AuraType.OverrideAttackPowerBySpPct))
            {
                UpdateMeleeAttackPowerAndDamage();
                UpdateRangedAttackPowerAndDamage();
            }
        }

        public bool _Modify(bool apply, ref int baseValue, ref int amount)
        {
            // If amount is negative, change sign and value of apply.
            if (amount < 0)
            {
                apply = !apply;
                amount = -amount;
            }

            if (apply)
                baseValue += amount;
            else
            {
                // Make sure we do not get public uint overflow.
                if (amount > baseValue)
                    amount = baseValue;
                baseValue -= amount;
            }

            return apply;
        }
    }
}
