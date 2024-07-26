// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public partial class Unit
    {
        public void HandleStatFlatModifier(UnitMods unitMod, UnitModifierFlatType modifierType, float amount, bool apply)
        {
            if (unitMod >= UnitMods.End || modifierType >= UnitModifierFlatType.End)
            {
                Log.outError(LogFilter.Unit, "ERROR in HandleStatFlatModifier(): non-existing UnitMods or wrong UnitModifierFlatType!");
                return;
            }

            if (amount == 0)
                return;

            switch (modifierType)
            {
                case UnitModifierFlatType.Base:
                case UnitModifierFlatType.BasePCTExcludeCreate:
                case UnitModifierFlatType.Total:
                    m_auraFlatModifiersGroup[(int)unitMod][(int)modifierType] += apply ? amount : -amount;
                    break;
                default:
                    break;
            }

            UpdateUnitMod(unitMod);
        }

        public void ApplyStatPctModifier(UnitMods unitMod, UnitModifierPctType modifierType, float pct)
        {
            if (unitMod >= UnitMods.End || modifierType >= UnitModifierPctType.End)
            {
                Log.outError(LogFilter.Unit, "ERROR in ApplyStatPctModifier(): non-existing UnitMods or wrong UnitModifierPctType!");
                return;
            }

            if (pct == 0)
                return;

            switch (modifierType)
            {
                case UnitModifierPctType.Base:
                case UnitModifierPctType.Total:
                    MathFunctions.AddPct(ref m_auraPctModifiersGroup[(int)unitMod][(int)modifierType], pct);
                    break;
                default:
                    break;
            }

            UpdateUnitMod(unitMod);
        }

        public void SetStatFlatModifier(UnitMods unitMod, UnitModifierFlatType modifierType, float val)
        {
            if (m_auraFlatModifiersGroup[(int)unitMod][(int)modifierType] == val)
                return;

            m_auraFlatModifiersGroup[(int)unitMod][(int)modifierType] = val;
            UpdateUnitMod(unitMod);
        }

        public void SetStatPctModifier(UnitMods unitMod, UnitModifierPctType modifierType, float val)
        {
            if (m_auraPctModifiersGroup[(int)unitMod][(int)modifierType] == val)
                return;

            m_auraPctModifiersGroup[(int)unitMod][(int)modifierType] = val;
            UpdateUnitMod(unitMod);
        }

        public float GetFlatModifierValue(UnitMods unitMod, UnitModifierFlatType modifierType)
        {
            if (unitMod >= UnitMods.End || modifierType >= UnitModifierFlatType.End)
            {
                Log.outError(LogFilter.Unit, "attempt to access non-existing modifier value from UnitMods!");
                return 0.0f;
            }

            return m_auraFlatModifiersGroup[(int)unitMod][(int)modifierType];
        }

        public float GetPctModifierValue(UnitMods unitMod, UnitModifierPctType modifierType)
        {
            if (unitMod >= UnitMods.End || modifierType >= UnitModifierPctType.End)
            {
                Log.outError(LogFilter.Unit, "attempt to access non-existing modifier value from UnitMods!");
                return 0.0f;
            }

            return m_auraPctModifiersGroup[(int)unitMod][(int)modifierType];
        }

        void UpdateUnitMod(UnitMods unitMod)
        {
            if (!CanModifyStats())
                return;

            switch (unitMod)
            {
                case UnitMods.StatStrength:
                case UnitMods.StatAgility:
                case UnitMods.StatStamina:
                case UnitMods.StatIntellect:
                case UnitMods.StatSpirit:
                    UpdateStats(GetStatByAuraGroup(unitMod));
                    break;
                case UnitMods.Armor:
                    UpdateArmor();
                    break;
                case UnitMods.Health:
                    UpdateMaxHealth();
                    break;
                case UnitMods.Mana:
                case UnitMods.Rage:
                case UnitMods.Focus:
                case UnitMods.Energy:
                case UnitMods.ComboPoints:
                case UnitMods.RunicPower:
                case UnitMods.RuneBlood:
                case UnitMods.RuneFrost:
                case UnitMods.RuneUnholy:
                    UpdateMaxPower((PowerType)(unitMod - UnitMods.PowerStart));
                    break;
                case UnitMods.ResistanceHoly:
                case UnitMods.ResistanceFire:
                case UnitMods.ResistanceNature:
                case UnitMods.ResistanceFrost:
                case UnitMods.ResistanceShadow:
                case UnitMods.ResistanceArcane:
                    UpdateResistances(GetSpellSchoolByAuraGroup(unitMod));
                    break;
                case UnitMods.AttackPower:
                    UpdateAttackPowerAndDamage();
                    break;
                case UnitMods.AttackPowerRanged:
                    UpdateAttackPowerAndDamage(true);
                    break;
                case UnitMods.DamageMainHand:
                    UpdateDamagePhysical(WeaponAttackType.BaseAttack);
                    break;
                case UnitMods.DamageOffHand:
                    UpdateDamagePhysical(WeaponAttackType.OffAttack);
                    break;
                case UnitMods.DamageRanged:
                    UpdateDamagePhysical(WeaponAttackType.RangedAttack);
                    break;
                default:
                    break;
            }
        }

        int GetMinPower(PowerType power) { return 0; }

        // returns negative amount on power reduction
        public int ModifyPower(PowerType power, int dVal, bool withPowerUpdate = true)
        {
            int gain = 0;

            if (dVal == 0)
                return 0;

            if (dVal > 0)
                dVal *= (int)GetTotalAuraMultiplierByMiscValue(AuraType.ModPowerGainPct, (int)power);

            int curPower = GetPower(power);

            int val = (dVal + curPower);
            if (val <= GetMinPower(power))
            {
                SetPower(power, GetMinPower(power), withPowerUpdate);
                return -curPower;
            }

            int maxPower = GetMaxPower(power);
            if (val < maxPower)
            {
                SetPower(power, val, withPowerUpdate);
                gain = val - curPower;
            }
            else if (curPower != maxPower)
            {
                SetPower(power, maxPower, withPowerUpdate);
                gain = maxPower - curPower;
            }

            return gain;
        }

        Stats GetStatByAuraGroup(UnitMods unitMod)
        {
            Stats stat = Stats.Strength;

            switch (unitMod)
            {
                case UnitMods.StatStrength:
                    stat = Stats.Strength;
                    break;
                case UnitMods.StatAgility:
                    stat = Stats.Agility;
                    break;
                case UnitMods.StatStamina:
                    stat = Stats.Stamina;
                    break;
                case UnitMods.StatIntellect:
                    stat = Stats.Intellect;
                    break;
                case UnitMods.StatSpirit:
                    stat = Stats.Spirit;
                    break;
                default:
                    break;
            }

            return stat;
        }

        public void UpdateStatBuffMod(Stats stat)
        {
            float modPos = 0.0f;
            float modNeg = 0.0f;
            float factor = 0.0f;

            UnitMods unitMod = UnitMods.StatStart + (int)stat;

            // includes value from items and enchantments
            float modValue = GetFlatModifierValue(unitMod, UnitModifierFlatType.Base);
            if (modValue > 0.0f)
                modPos += modValue;
            else
                modNeg += modValue;

            if (IsGuardian())
            {
                modValue = ((Guardian)this).GetBonusStatFromOwner(stat);
                if (modValue > 0.0f)
                    modPos += modValue;
                else
                    modNeg += modValue;
            }

            // SPELL_AURA_MOD_STAT_BONUS_PCT only affects BASE_VALUE
            modPos = MathFunctions.CalculatePct(modPos, Math.Max(GetFlatModifierValue(unitMod, UnitModifierFlatType.BasePCTExcludeCreate), -100.0f));
            modNeg = MathFunctions.CalculatePct(modNeg, Math.Max(GetFlatModifierValue(unitMod, UnitModifierFlatType.BasePCTExcludeCreate), -100.0f));

            modPos += GetTotalAuraModifier(AuraType.ModStat, aurEff =>
            {
                if ((aurEff.GetMiscValue() < 0 || aurEff.GetMiscValue() == (int)stat) && aurEff.GetAmount() > 0)
                    return true;
                return false;
            });

            modNeg += GetTotalAuraModifier(AuraType.ModStat, aurEff =>
            {
                if ((aurEff.GetMiscValue() < 0 || aurEff.GetMiscValue() == (int)stat) && aurEff.GetAmount() < 0)
                    return true;
                return false;
            });

            factor = GetTotalAuraMultiplier(AuraType.ModPercentStat, aurEff =>
            {
                if (aurEff.GetMiscValue() == -1 || aurEff.GetMiscValue() == (int)stat)
                    return true;
                return false;
            });

            factor *= GetTotalAuraMultiplier(AuraType.ModTotalStatPercentage, aurEff =>
            {
                if (aurEff.GetMiscValue() == -1 || aurEff.GetMiscValue() == (int)stat)
                    return true;
                return false;
            });

            modPos *= factor;
            modNeg *= factor;

            m_floatStatPosBuff[(int)stat] = modPos;
            m_floatStatNegBuff[(int)stat] = modNeg;

            UpdateStatBuffModForClient(stat);
        }

        void UpdateStatBuffModForClient(Stats stat)
        {
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.StatPosBuff, (int)stat), (int)m_floatStatPosBuff[(int)stat]);
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.StatNegBuff, (int)stat), (int)m_floatStatNegBuff[(int)stat]);
        }

        public virtual bool UpdateStats(Stats stat) { return false; }

        public virtual bool UpdateAllStats() { return false; }

        public virtual void UpdateResistances(SpellSchools school)
        {
            if (school > SpellSchools.Normal)
            {
                UnitMods unitMod = UnitMods.ResistanceStart + (int)school;
                float value = MathFunctions.CalculatePct(GetFlatModifierValue(unitMod, UnitModifierFlatType.Base), Math.Max(GetFlatModifierValue(unitMod, UnitModifierFlatType.BasePCTExcludeCreate), -100.0f));
                value *= GetPctModifierValue(unitMod, UnitModifierPctType.Base);

                value += GetFlatModifierValue(unitMod, UnitModifierFlatType.Total);
                value *= GetPctModifierValue(unitMod, UnitModifierPctType.Total);

                SetResistance(school, (int)value);
            }
            else
                UpdateArmor();
        }

        public virtual void UpdateArmor() { }

        public virtual void UpdateMaxHealth() { }

        public virtual void UpdateMaxPower(PowerType power) { }

        public virtual void UpdateAttackPowerAndDamage(bool ranged = false) { }

        public virtual void UpdateDamagePhysical(WeaponAttackType attType)
        {
            CalculateMinMaxDamage(attType, false, true, out float minDamage, out float maxDamage);

            switch (attType)
            {
                case WeaponAttackType.BaseAttack:
                default:
                    SetUpdateFieldStatValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MinDamage), minDamage);
                    SetUpdateFieldStatValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MaxDamage), maxDamage);
                    break;
                case WeaponAttackType.OffAttack:
                    SetUpdateFieldStatValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MinOffHandDamage), minDamage);
                    SetUpdateFieldStatValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MaxOffHandDamage), maxDamage);
                    break;
                case WeaponAttackType.RangedAttack:
                    SetUpdateFieldStatValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MinRangedDamage), minDamage);
                    SetUpdateFieldStatValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MaxRangedDamage), maxDamage);
                    break;
            }
        }
        
        public virtual void CalculateMinMaxDamage(WeaponAttackType attType, bool normalized, bool addTotalPct, out float minDamage, out float maxDamage)
        {
            minDamage = 0f;
            maxDamage = 0f;
        }

        public void UpdateAllResistances()
        {
            for (var i = SpellSchools.Normal; i < SpellSchools.Max; ++i)
                UpdateResistances(i);
        }

        //Stats
        public float GetStat(Stats stat) { return m_unitData.Stats[(int)stat]; }

        public void SetStat(Stats stat, int val) { SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Stats, (int)stat), val); }

        public int GetCreateMana() { return m_unitData.BaseMana; }

        public void SetCreateMana(int val) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.BaseMana), val); }

        public int GetArmor()
        {
            return GetResistance(SpellSchools.Normal);
        }

        public void SetArmor(int val, int bonusVal)
        {
            SetResistance(SpellSchools.Normal, val);
        }

        public float GetCreateStat(Stats stat)
        {
            return CreateStats[(int)stat];
        }

        public void SetCreateStat(Stats stat, float val)
        {
            CreateStats[(int)stat] = val;
        }

        public float GetPosStat(Stats stat) { return m_unitData.StatPosBuff[(int)stat]; }

        public float GetNegStat(Stats stat) { return m_unitData.StatNegBuff[(int)stat]; }

        public int GetResistance(SpellSchools school)
        {
            return m_unitData.Resistances[(int)school];
        }

        public int GetResistance(SpellSchoolMask mask)
        {
            int? resist = null;
            for (int i = (int)SpellSchools.Normal; i < (int)SpellSchools.Max; ++i)
            {
                int schoolResistance = GetResistance((SpellSchools)i);
                if (Convert.ToBoolean((int)mask & (1 << i)) && (!resist.HasValue || resist.Value > schoolResistance))
                    resist = schoolResistance;
            }

            // resist value will never be negative here
            return resist.HasValue ? resist.Value : 0;
        }

        public void SetResistance(SpellSchools school, int val) { SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Resistances, (int)school), val); }

        public void SetModCastingSpeed(float castingSpeed) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModCastingSpeed), castingSpeed); }

        public void SetModSpellHaste(float spellHaste) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModSpellHaste), spellHaste); }

        public void SetModHaste(float haste) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModHaste), haste); }

        public void SetModRangedHaste(float rangedHaste) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModRangedHaste), rangedHaste); }

        public void SetModHasteRegen(float hasteRegen) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModHasteRegen), hasteRegen); }

        public void SetModTimeRate(float timeRate) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ModTimeRate), timeRate); }

        public void InitStatBuffMods()
        {
            for (var stat = Stats.Strength; stat < Stats.Max; ++stat)
            {
                m_floatStatPosBuff[(int)stat] = 0.0f;
                m_floatStatNegBuff[(int)stat] = 0.0f;
                UpdateStatBuffModForClient(stat);
            }
        }

        public bool CanModifyStats()
        {
            return canModifyStats;
        }

        public void SetCanModifyStats(bool modifyStats)
        {
            canModifyStats = modifyStats;
        }

        public float GetTotalStatValue(Stats stat)
        {
            UnitMods unitMod = UnitMods.StatStart + (int)stat;

            float value = MathFunctions.CalculatePct(GetFlatModifierValue(unitMod, UnitModifierFlatType.Base), Math.Max(GetFlatModifierValue(unitMod, UnitModifierFlatType.BasePCTExcludeCreate), -100.0f));
            value += GetCreateStat(stat);
            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Base);
            value += GetFlatModifierValue(unitMod, UnitModifierFlatType.Total);
            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Total);

            return value;
        }

        //Health  
        public int GetCreateHealth() { return m_unitData.BaseHealth; }

        public void SetCreateHealth(int val) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.BaseHealth), val); }

        public long GetHealth() { return m_unitData.Health; }

        public void SetHealth(long val)
        {
            if (GetDeathState() == DeathState.JustDied || GetDeathState() == DeathState.Corpse)
                val = 0;
            else if (IsTypeId(TypeId.Player) && GetDeathState() == DeathState.Dead)
                val = 1;
            else
            {
                var maxHealth = GetMaxHealth();
                if (maxHealth < val)
                    val = maxHealth;
            }

            var oldVal = GetHealth();
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Health), (int)val);

            TriggerOnHealthChangeAuras(oldVal, val);

            // group update
            Player player = ToPlayer();
            if (player != null)
            {
                if (player.GetGroup() != null)
                    player.SetGroupUpdateFlag(GroupUpdateFlags.CurHp);
            }
            else if (IsPet())
            {
                Pet pet = ToCreature().ToPet();
                if (pet.IsControlled())
                    pet.SetGroupUpdateFlag(GroupUpdatePetFlags.CurHp);
            }
        }

        public long GetMaxHealth() { return m_unitData.MaxHealth; }

        public void SetMaxHealth(long val)
        {
            if (val == 0)
                val = 1;

            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MaxHealth), val);
            var health = GetHealth();

            // group update
            if (IsTypeId(TypeId.Player))
            {
                if (ToPlayer().GetGroup() != null)
                    ToPlayer().SetGroupUpdateFlag(GroupUpdateFlags.MaxHp);
            }
            else if (IsPet())
            {
                Pet pet = ToCreature().ToPet();
                if (pet.IsControlled())
                    pet.SetGroupUpdateFlag(GroupUpdatePetFlags.MaxHp);
            }

            if (val < health)
                SetHealth(val);
        }

        public float GetHealthPct() { return GetMaxHealth() != 0 ? 100.0f * GetHealth() / GetMaxHealth() : 0.0f; }

        public void SetFullHealth() { SetHealth(GetMaxHealth()); }

        public bool IsFullHealth() { return GetHealth() == GetMaxHealth(); }

        public bool HealthBelowPct(int pct) { return GetHealth() < CountPctFromMaxHealth(pct); }

        public bool HealthBelowPctDamaged(int pct, int damage) { return GetHealth() - damage < CountPctFromMaxHealth(pct); }

        public bool HealthAbovePct(int pct) { return GetHealth() > CountPctFromMaxHealth(pct); }

        public bool HealthAbovePctHealed(int pct, int heal) { return GetHealth() + heal > CountPctFromMaxHealth(pct); }

        public long CountPctFromMaxHealth(int pct) { return MathFunctions.CalculatePct(GetMaxHealth(), pct); }

        public long CountPctFromCurHealth(int pct) { return MathFunctions.CalculatePct(GetHealth(), pct); }

        public virtual float GetHealthMultiplierForTarget(WorldObject target) { return 1.0f; }

        public virtual float GetDamageMultiplierForTarget(WorldObject target) { return 1.0f; }

        public virtual float GetArmorMultiplierForTarget(WorldObject target) { return 1.0f; }

        //Powers
        public PowerType GetPowerType() { return (PowerType)(byte)m_unitData.DisplayPower; }

        public void SetPowerType(PowerType powerType, bool sendUpdate = true)
        {
            if (GetPowerType() == powerType)
                return;

            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.DisplayPower), (byte)powerType);

            if (!sendUpdate)
                return;

            Player thisPlayer = ToPlayer();
            if (thisPlayer != null)
            {
                if (thisPlayer.GetGroup() != null)
                    thisPlayer.SetGroupUpdateFlag(GroupUpdateFlags.PowerType);
            }
            /*else if (IsPet()) TODO 6.x
            {
                Pet pet = ToCreature().ToPet();
                if (pet.isControlled())
                    pet.SetGroupUpdateFlag(GROUP_UPDATE_FLAG_PET_POWER_TYPE);
            }*/

            // Update max power
            UpdateMaxPower(powerType);

            // Update current power
            switch (powerType)
            {
                case PowerType.Mana: // Keep the same (druid form switching...)
                case PowerType.Energy:
                    break;
                case PowerType.Rage: // Reset to zero
                    SetPower(PowerType.Rage, 0);
                    break;
                case PowerType.Focus: // Make it full
                    SetFullPower(powerType);
                    break;
                default:
                    break;
            }
        }

        public void SetOverrideDisplayPowerId(int powerDisplayId) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.OverrideDisplayPowerID), powerDisplayId); }

        public void SetMaxPower(PowerType powerType, int val)
        {
            int powerIndex = GetPowerIndex(powerType);
            if (powerIndex == (int)PowerType.Max || powerIndex >= (int)PowerType.MaxPerClass)
                return;

            int cur_power = GetPower(powerType);
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MaxPower, powerIndex), val);

            // group update
            if (IsTypeId(TypeId.Player))
            {
                if (ToPlayer().GetGroup() != null)
                    ToPlayer().SetGroupUpdateFlag(GroupUpdateFlags.MaxPower);
            }
            /*else if (IsPet()) TODO 6.x
            {
                Pet pet = ToCreature().ToPet();
                if (pet.isControlled())
                    pet.SetGroupUpdateFlag(GROUP_UPDATE_FLAG_PET_MAX_POWER);
            }*/

            if (val < cur_power)
                SetPower(powerType, val);
        }

        public void SetPower(PowerType powerType, int val, bool withPowerUpdate = true)
        {
            int powerIndex = GetPowerIndex(powerType);
            if (powerIndex == (int)PowerType.Max || powerIndex >= (int)PowerType.MaxPerClass)
                return;

            int maxPower = GetMaxPower(powerType);
            if (maxPower < val)
                val = maxPower;

            int oldPower = m_unitData.Power[powerIndex];
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Power, powerIndex), val);

            if (IsInWorld && withPowerUpdate)
            {
                PowerUpdate packet = new();
                packet.Guid = GetGUID();
                packet.Powers.Add(new PowerUpdatePower(val, (byte)powerType));
                SendMessageToSet(packet, IsTypeId(TypeId.Player));
            }

            TriggerOnPowerChangeAuras(powerType, oldPower, val);

            // group update
            if (IsTypeId(TypeId.Player))
            {
                Player player = ToPlayer();
                if (player.GetGroup() != null)
                    player.SetGroupUpdateFlag(GroupUpdateFlags.CurPower);
            }
            /*else if (IsPet()) TODO 6.x
            {
                Pet pet = ToCreature().ToPet();
                if (pet.isControlled())
                    pet.SetGroupUpdateFlag(GROUP_UPDATE_FLAG_PET_CUR_POWER);
            }*/
        }

        public void SetFullPower(PowerType powerType) { SetPower(powerType, GetMaxPower(powerType)); }

        public int GetPower(PowerType powerType)
        {
            int powerIndex = GetPowerIndex(powerType);
            if (powerIndex == (int)PowerType.Max || powerIndex >= (int)PowerType.MaxPerClass)
                return 0;

            return m_unitData.Power[powerIndex];
        }

        public int GetMaxPower(PowerType powerType)
        {
            int powerIndex = GetPowerIndex(powerType);
            if (powerIndex == (int)PowerType.Max || powerIndex >= (int)PowerType.MaxPerClass)
                return 0;

            return (int)(uint)m_unitData.MaxPower[powerIndex];
        }

        public virtual int GetCreatePowerValue(PowerType powerType)
        {
            if (powerType == PowerType.Mana)
                return GetCreateMana();

            PowerTypeRecord powerTypeEntry = Global.DB2Mgr.GetPowerTypeEntry(powerType);
            if (powerTypeEntry != null)
                return powerTypeEntry.MaxBasePower;

            return 0;
        }

        public virtual int GetPowerIndex(PowerType powerType) { return 0; }

        public float GetPowerPct(PowerType powerType) { return GetMaxPower(powerType) != 0 ? 100.0f * GetPower(powerType) / GetMaxPower(powerType) : 0.0f; }

        void TriggerOnPowerChangeAuras(PowerType power, int oldVal, int newVal)
        {
            void processAuras(List<AuraEffect> effects)
            {
                foreach (AuraEffect effect in effects)
                {
                    if (effect.GetMiscValue() == (int)power)
                    {
                        int effectAmount = effect.GetAmount();
                        int triggerSpell = effect.GetSpellEffectInfo().TriggerSpell;

                        float oldValueCheck = oldVal;
                        float newValueCheck = newVal;

                        if (effect.GetAuraType() == AuraType.TriggerSpellOnPowerPct)
                        {
                            int maxPower = GetMaxPower(power);
                            oldValueCheck = MathFunctions.GetPctOf(oldVal, maxPower);
                            newValueCheck = MathFunctions.GetPctOf(newVal, maxPower);
                        }

                        switch ((AuraTriggerOnPowerChangeDirection)effect.GetMiscValueB())
                        {
                            case AuraTriggerOnPowerChangeDirection.Gain:
                                if (oldValueCheck >= effect.GetAmount() || newValueCheck < effectAmount)
                                    continue;
                                break;
                            case AuraTriggerOnPowerChangeDirection.Loss:
                                if (oldValueCheck <= effect.GetAmount() || newValueCheck > effectAmount)
                                    continue;
                                break;
                            default:
                                break;
                        }

                        CastSpell(this, triggerSpell, new CastSpellExtraArgs(effect));
                    }
                }
            }

            processAuras(GetAuraEffectsByType(AuraType.TriggerSpellOnPowerPct));
            processAuras(GetAuraEffectsByType(AuraType.TriggerSpellOnPowerAmount));
        }

        public bool CanApplyResilience()
        {
            return !IsVehicle() && GetOwnerGUID().IsPlayer();
        }

        public static void ApplyResilience(Unit victim, ref int damage)
        {
            // player mounted on multi-passenger mount is also classified as vehicle
            if (victim.IsVehicle() && !victim.IsPlayer())
                return;

            Unit target = null;
            if (victim.IsPlayer())
                target = victim;
            else // victim->GetTypeId() == TYPEID_UNIT
            {
                Unit owner = victim.GetOwner();
                if (owner != null)
                    if (owner.IsPlayer())
                        target = owner;
            }

            if (target == null)
                return;

            damage -= target.GetDamageReduction(damage);
        }

        public int CalculateAOEAvoidance(int damage, SpellSchoolMask schoolMask, bool npcCaster)
        {
            damage = (int)(damage * GetTotalAuraMultiplierByMiscMask(AuraType.ModAoeDamageAvoidance, (uint)schoolMask));
            if (npcCaster != null)
                damage = (int)(damage * GetTotalAuraMultiplierByMiscMask(AuraType.ModCreatureAoeDamageAvoidance, (uint)schoolMask));

            return damage;
        }
        
        // player or player's pet resilience (-1%)
        int GetDamageReduction(int damage) { return 0; }

        float GetCombatRatingReduction(CombatRating cr)
        {
            Player player = ToPlayer();
            if (player != null)
                return player.GetRatingBonusValue(cr);
            // Player's pet get resilience from owner
            else if (IsPet() && GetOwner() != null)
            {
                Player owner = GetOwner().ToPlayer();
                if (owner != null)
                    return owner.GetRatingBonusValue(cr);
            }

            return 0.0f;
        }

        uint GetCombatRatingDamageReduction(CombatRating cr, float rate, float cap, uint damage)
        {
            float percent = Math.Min(GetCombatRatingReduction(cr) * rate, cap);
            return MathFunctions.CalculatePct(damage, percent);
        }

        public void SetAttackPower(int attackPower) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AttackPower), attackPower); }

        public void SetAttackPowerModPos(int attackPowerMod) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AttackPowerModPos), attackPowerMod); }

        public void SetAttackPowerModNeg(int attackPowerMod) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AttackPowerModNeg), attackPowerMod); }

        public void SetAttackPowerMultiplier(float attackPowerMult) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AttackPowerMultiplier), attackPowerMult); }

        public void SetRangedAttackPower(int attackPower) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.RangedAttackPower), attackPower); }

        public void SetRangedAttackPowerModPos(int attackPowerMod) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.RangedAttackPowerModPos), attackPowerMod); }

        public void SetRangedAttackPowerModNeg(int attackPowerMod) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.RangedAttackPowerModNeg), attackPowerMod); }

        public void SetRangedAttackPowerMultiplier(float attackPowerMult) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.RangedAttackPowerMultiplier), attackPowerMult); }

        //Chances
        public override float MeleeSpellMissChance(Unit victim, WeaponAttackType attType, SpellInfo spellInfo)
        {
            if (spellInfo != null && spellInfo.HasAttribute(SpellAttr7.NoAttackMiss))
                return 0.0f;

            //calculate miss Chance
            float missChance = victim.GetUnitMissChance();

            // melee attacks while dual wielding have +19% Chance to miss
            if (spellInfo == null && HaveOffhandWeapon() && !IsInFeralForm() && !HasAuraType(AuraType.IgnoreDualWieldHitPenalty))
                missChance += 19.0f;

            // Spellmod from SpellModOp.HitChance
            float resistMissChance = 100.0f;
            if (spellInfo != null)
            {
                Player modOwner = GetSpellModOwner();
                if (modOwner != null)
                    modOwner.ApplySpellMod(spellInfo, SpellModOp.HitChance, ref resistMissChance);
            }

            missChance += resistMissChance - 100.0f;

            if (attType == WeaponAttackType.RangedAttack)
                missChance -= ModRangedHitChance;
            else
                missChance -= ModMeleeHitChance;

            // miss Chance from auras after calculating skill based miss
            missChance -= GetTotalAuraModifier(AuraType.ModHitChance);
            if (attType == WeaponAttackType.RangedAttack)
                missChance -= victim.GetTotalAuraModifier(AuraType.ModAttackerRangedHitChance);
            else
                missChance -= victim.GetTotalAuraModifier(AuraType.ModAttackerMeleeHitChance);

            return Math.Max(missChance, 0f);
        }

        float GetUnitCriticalChanceDone(WeaponAttackType attackType)
        {
            float chance = 0.0f;
            Player thisPlayer = ToPlayer();
            if (thisPlayer != null)
            {
                switch (attackType)
                {
                    case WeaponAttackType.BaseAttack:
                        chance = thisPlayer.m_activePlayerData.CritPercentage;
                        break;
                    case WeaponAttackType.OffAttack:
                        chance = thisPlayer.m_activePlayerData.OffhandCritPercentage;
                        break;
                    case WeaponAttackType.RangedAttack:
                        chance = thisPlayer.m_activePlayerData.RangedCritPercentage;
                        break;
                }
            }
            else
            {
                if (!ToCreature().GetCreatureTemplate().FlagsExtra.HasAnyFlag(CreatureFlagsExtra.NoCrit))
                {
                    chance = 5.0f;
                    chance += GetTotalAuraModifier(AuraType.ModWeaponCritPercent);
                    chance += GetTotalAuraModifier(AuraType.ModCritPct);
                }
            }
            return chance;
        }

        float GetUnitCriticalChanceTaken(Unit attacker, WeaponAttackType attackType, float critDone)
        {
            float chance = critDone;

            // flat aura mods
            if (attackType != WeaponAttackType.RangedAttack)
                chance += GetTotalAuraModifier(AuraType.ModAttackerMeleeCritChance);

            chance += GetTotalAuraModifier(AuraType.ModCritChanceVersusTargetHealth, aurEff => !HealthBelowPct(aurEff.GetMiscValueB()));

            chance += GetTotalAuraModifier(AuraType.ModCritChanceForCaster, aurEff => aurEff.GetCasterGUID() == attacker.GetGUID());

            TempSummon tempSummon = attacker.ToTempSummon();
            if (tempSummon != null)
                chance += GetTotalAuraModifier(AuraType.ModCritChanceForCasterPet, aurEff => aurEff.GetCasterGUID() == tempSummon.GetSummonerGUID());

            chance += GetTotalAuraModifier(AuraType.ModAttackerSpellAndWeaponCritChance);

            return Math.Max(chance, 0.0f);
        }

        float GetUnitCriticalChanceAgainst(WeaponAttackType attackType, Unit victim)
        {
            float chance = GetUnitCriticalChanceDone(attackType);
            return victim.GetUnitCriticalChanceTaken(this, attackType, chance);
        }
        
        float GetUnitDodgeChance(WeaponAttackType attType, Unit victim)
        {
            int levelDiff = victim.GetLevelForTarget(this) - GetLevelForTarget(victim);

            float chance = 0.0f;
            float levelBonus = 0.0f;
            Player playerVictim = victim.ToPlayer();
            if (playerVictim != null)
                chance = playerVictim.m_activePlayerData.DodgePercentage;
            else
            {
                if (!victim.IsTotem())
                {
                    chance = 3.0f;
                    chance += victim.GetTotalAuraModifier(AuraType.ModDodgePercent);

                    if (levelDiff > 0)
                        levelBonus = 1.5f * levelDiff;
                }
            }

            chance += levelBonus;

            // Reduce enemy dodge Chance by SPELL_AURA_MOD_COMBAT_RESULT_CHANCE
            chance += GetTotalAuraModifierByMiscValue(AuraType.ModCombatResultChance, (int)VictimState.Dodge);

            // reduce dodge by SPELL_AURA_MOD_ENEMY_DODGE
            chance += GetTotalAuraModifier(AuraType.ModEnemyDodge);

            // Reduce dodge Chance by attacker expertise rating
            if (IsTypeId(TypeId.Player))
                chance -= ToPlayer().GetExpertiseDodgeOrParryReduction(attType);
            else
                chance -= GetTotalAuraModifier(AuraType.ModExpertise) / 4.0f;
            return Math.Max(chance, 0.0f);
        }

        float GetUnitParryChance(WeaponAttackType attType, Unit victim)
        {
            int levelDiff = victim.GetLevelForTarget(this) - GetLevelForTarget(victim);

            float chance = 0.0f;
            float levelBonus = 0.0f;
            Player playerVictim = victim.ToPlayer();
            if (playerVictim != null)
            {
                if (playerVictim.CanParry())
                {
                    Item tmpitem = playerVictim.GetWeaponForAttack(WeaponAttackType.BaseAttack, true);
                    if (tmpitem == null)
                        tmpitem = playerVictim.GetWeaponForAttack(WeaponAttackType.OffAttack, true);

                    if (tmpitem != null)
                        chance = playerVictim.m_activePlayerData.ParryPercentage;
                }
            }
            else
            {
                if (!victim.IsTotem() && !victim.ToCreature().GetCreatureTemplate().FlagsExtra.HasAnyFlag(CreatureFlagsExtra.NoParry))
                {
                    chance = 6.0f;
                    chance += victim.GetTotalAuraModifier(AuraType.ModParryPercent);

                    if (levelDiff > 0)
                        levelBonus = 1.5f * levelDiff;
                }
            }

            chance += levelBonus;

            // Reduce parry Chance by attacker expertise rating
            if (IsTypeId(TypeId.Player))
                chance -= ToPlayer().GetExpertiseDodgeOrParryReduction(attType);
            else
                chance -= GetTotalAuraModifier(AuraType.ModExpertise) / 4.0f;
            return Math.Max(chance, 0.0f);
        }

        float GetUnitMissChance()
        {
            float miss_chance = 5.0f;

            return miss_chance;
        }

        float GetUnitBlockChance(WeaponAttackType attType, Unit victim)
        {
            int levelDiff = victim.GetLevelForTarget(this) - GetLevelForTarget(victim);

            float chance = 0.0f;
            float levelBonus = 0.0f;
            Player playerVictim = victim.ToPlayer();
            if (playerVictim != null)
            {
                if (playerVictim.CanBlock())
                {
                    Item tmpitem = playerVictim.GetUseableItemByPos(EquipmentSlot.OffHand);
                    if (tmpitem != null && !tmpitem.IsBroken() && tmpitem.GetTemplate().GetInventoryType() == InventoryType.Shield)
                        chance = playerVictim.m_activePlayerData.BlockPercentage;
                }
            }
            else
            {
                if (!victim.IsTotem() && !(victim.ToCreature().GetCreatureTemplate().FlagsExtra.HasAnyFlag(CreatureFlagsExtra.NoBlock)))
                {
                    chance = 3.0f;
                    chance += victim.GetTotalAuraModifier(AuraType.ModBlockPercent);

                    if (levelDiff > 0)
                        levelBonus = 1.5f * levelDiff;
                }
            }

            chance += levelBonus;
            return Math.Max(chance, 0.0f);
        }

        public abstract int GetShieldBlockValue();        

        public int GetShieldBlockValue(int soft_cap, int hard_cap)
        {
            var value = GetShieldBlockValue();
            if (value >= hard_cap)
            {
                value = (soft_cap + hard_cap) / 2;
            }
            else if (value > soft_cap)
            {
                value = soft_cap + ((value - soft_cap) / 2);
            }

            return value;
        }

        public ushort GetDefenseSkillValue(Unit target = null)
        {
            if (GetTypeId() == TypeId.Player)
            {
                // in PvP use full skill instead current skill value
                ushort value = (target != null && target.GetTypeId() == TypeId.Player)
                        ? ToPlayer().GetMaxSkillValue(SkillType.Defense)
                        : ToPlayer().GetSkillValue(SkillType.Defense);
                value += (ushort)ToPlayer().GetRatingBonusValue(CombatRating.DefenseSkill);
                return value;
            }
            else
                return GetMaxSkillValueForLevel(target);
        }

        public int GetMechanicResistChance(SpellInfo spellInfo)
        {
            if (spellInfo == null)
                return 0;

            int resistMech = 0;
            foreach (var spellEffectInfo in spellInfo.GetEffects())
            {
                if (!spellEffectInfo.IsEffect())
                    break;

                int effect_mech = (int)spellInfo.GetEffectMechanic(spellEffectInfo.EffectIndex);
                if (effect_mech != 0)
                {
                    int temp = GetTotalAuraModifierByMiscValue(AuraType.ModMechanicResistance, effect_mech);
                    if (resistMech < temp)
                        resistMech = temp;
                }
            }
            return Math.Max(resistMech, 0);
        }
    }

    public partial class Player
    {
        public override bool UpdateAllStats()
        {
            for (var i = Stats.Strength; i < Stats.Max; ++i)
            {
                float value = GetTotalStatValue(i);
                SetStat(i, (int)value);
            }

            UpdateArmor();
            // calls UpdateAttackPowerAndDamage() in UpdateArmor for SPELL_AURA_MOD_ATTACK_POWER_OF_ARMOR
            UpdateAttackPowerAndDamage(true);
            UpdateMaxHealth();

            for (var i = PowerType.Mana; i < PowerType.Max; ++i)
                UpdateMaxPower(i);

            UpdateAllRatings();
            UpdateAllCritPercentages();
            UpdateSpellCritChance();
            UpdateBlockPercentage();
            UpdateParryPercentage();
            UpdateDodgePercentage();
            UpdateShieldBlockValue();
            UpdateSpellDamageAndHealingBonus();
            UpdateManaRegen();
            UpdateExpertise(WeaponAttackType.BaseAttack);
            UpdateExpertise(WeaponAttackType.OffAttack);
            RecalculateRating(CombatRating.ArmorPenetration);
            UpdateAllResistances();

            return true;
        }

        public override bool UpdateStats(Stats stat)
        {
            // value = ((base_value * base_pct) + total_value) * total_pct
            float value = GetTotalStatValue(stat);

            SetStat(stat, (int)value);

            if (stat == Stats.Stamina || stat == Stats.Intellect || stat == Stats.Strength)
            {
                Pet pet = GetPet();
                if (pet != null)
                    pet.UpdateStats(stat);
            }

            switch (stat)
            {
                case Stats.Strength:
                    UpdateShieldBlockValue();
                    break;
                case Stats.Agility:
                    UpdateArmor();
                    UpdateAllCritPercentages();
                    UpdateDodgePercentage();
                    break;
                case Stats.Stamina:
                    UpdateMaxHealth();
                    break;
                case Stats.Intellect:
                    UpdateSpellCritChance();
                    break;
                case Stats.Spirit:
                    break;
                default:
                    break;
            }

            if (stat == Stats.Strength)
                UpdateAttackPowerAndDamage(false);
            else if (stat == Stats.Agility)
            {
                //UpdateAttackPowerAndDamage(false); duplicated in UpdateArmor();
                UpdateAttackPowerAndDamage(true);
            }
            
            UpdateSpellDamageAndHealingBonus();
            UpdateManaRegen();
            return true;
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

        public override void UpdateResistances(SpellSchools school)
        {
            if (school > SpellSchools.Normal)
            {
                base.UpdateResistances(school);

                Pet pet = GetPet();
                if (pet != null)
                    pet.UpdateResistances(school);
            }
            else
                UpdateArmor();
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

            if (GetShapeshiftForm() == ShapeShiftForm.TreeOfLife)
                return;                                             // use weapon but not skill up

            if (victim.GetTypeId() == TypeId.Unit && (victim.ToCreature().GetCreatureTemplate().FlagsExtra & CreatureFlagsExtra.NoSkillGains) != 0)
                return;
                        
            int weapon_skill_gain = WorldConfig.Values[WorldCfg.SkillGainWeapon].Int32;

            Item tmpitem = GetWeaponForAttack(attType, true);
            if (tmpitem == null && attType == WeaponAttackType.BaseAttack)
            {
                // Keep unarmed & fist weapon skills in sync
                UpdateSkill(SkillType.Unarmed, weapon_skill_gain);
                UpdateSkill(SkillType.FistWeapons, weapon_skill_gain);
            }
            else if (tmpitem != null)
            {
                switch (tmpitem.GetTemplate().GetSubClass().Weapon)
                {
                    case ItemSubClassWeapon.Fist:
                        UpdateSkill(tmpitem.GetSkill(), weapon_skill_gain);
                        goto case ItemSubClassWeapon.FishingPole;
                    case ItemSubClassWeapon.FishingPole:
                    case ItemSubClassWeapon.Miscellaneous:
                        UpdateSkill(SkillType.Unarmed, weapon_skill_gain);
                        break;                                             
                    default:
                        UpdateSkill(tmpitem.GetSkill(), weapon_skill_gain);
                        break;
                }
            }

            UpdateAllCritPercentages();
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

            float attackPowerMod = Math.Max(GetAPMultiplier(attType, normalized), 0.25f);

            float baseValue = GetFlatModifierValue(unitMod, UnitModifierFlatType.Base) + GetTotalAttackPowerValue(attType) / 14.0f * attackPowerMod;
            float basePct = GetPctModifierValue(unitMod, UnitModifierPctType.Base);
            float totalValue = GetFlatModifierValue(unitMod, UnitModifierFlatType.Total);
            float totalPct = addTotalPct ? GetPctModifierValue(unitMod, UnitModifierPctType.Total) : 1.0f;

            float weaponMinDamage = GetWeaponDamageRange(attType, WeaponDamageRange.MinDamage);
            float weaponMaxDamage = GetWeaponDamageRange(attType, WeaponDamageRange.MaxDamage);

            float versaDmgMod = 1.0f;

            //MathFunctions.AddPct(ref versaDmgMod, GetRatingBonusValue(CombatRating.VersatilityDamageDone) + (float)GetTotalAuraModifier(AuraType.ModVersatility));

            SpellShapeshiftFormRecord shapeshift = CliDB.SpellShapeshiftFormStorage.LookupByKey((int)GetShapeshiftForm());
            if (shapeshift != null && shapeshift.CombatRoundTime != 0)
            {
                weaponMinDamage = weaponMinDamage * shapeshift.CombatRoundTime / 1000.0f / attackPowerMod;
                weaponMaxDamage = weaponMaxDamage * shapeshift.CombatRoundTime / 1000.0f / attackPowerMod;
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

            min_damage = ((baseValue + weaponMinDamage) * basePct + totalValue) * totalPct * versaDmgMod;
            max_damage = ((baseValue + weaponMaxDamage) * basePct + totalValue) * totalPct * versaDmgMod;
        }

        public void UpdateAllCritPercentages()
        {
            float value = 5.0f;

            SetBaseModPctValue(BaseModGroup.CritPercentage, value);
            SetBaseModPctValue(BaseModGroup.OffhandCritPercentage, value);
            SetBaseModPctValue(BaseModGroup.RangedCritPercentage, value);

            UpdateCritPercentage(WeaponAttackType.BaseAttack);
            UpdateCritPercentage(WeaponAttackType.OffAttack);
            UpdateCritPercentage(WeaponAttackType.RangedAttack);
        }

        public void UpdateManaRegen()
        {
            var manaIndex = GetPowerIndex(PowerType.Mana);
            if (manaIndex == (int)PowerType.Max)
                return;

            float Intellect = GetStat(Stats.Intellect);
            // Mana regen from spirit and intellect
            float power_regen = (float)Math.Sqrt(Intellect) * OCTRegenMPPerSpirit();
            // Apply PCT bonus from SPELL_AURA_MOD_POWER_REGEN_PERCENT aura on spirit base regen
            power_regen *= GetTotalAuraMultiplierByMiscValue(AuraType.ModPowerRegenPercent, (int)PowerType.Mana);

            // Mana regen from SPELL_AURA_MOD_POWER_REGEN aura
            float power_regen_mp5 = (GetTotalAuraModifierByMiscValue(AuraType.ModPowerRegen, (int)PowerType.Mana) + m_baseManaRegen) / 5.0f;

            // Get bonus from SPELL_AURA_MOD_MANA_REGEN_FROM_STAT aura
            var regenAura = GetAuraEffectsByType(AuraType.ModManaRegenFromStat);
            foreach (var aura in regenAura)
                power_regen_mp5 += GetStat((Stats)aura.GetMiscValue()) * aura.GetAmount() / 500.0f;

            // Set regen rate in cast state apply only on spirit based regen
            int modManaRegenInterrupt = GetTotalAuraModifier(AuraType.ModManaRegenInterrupt);
            if (modManaRegenInterrupt > 100)
                modManaRegenInterrupt = 100;

            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PowerRegenFlatModifier, manaIndex), power_regen_mp5 + MathFunctions.CalculatePct(power_regen, modManaRegenInterrupt));
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PowerRegenInterruptedFlatModifier, manaIndex), power_regen_mp5 + power_regen);
        }

        public void UpdateSpellDamageAndHealingBonus()
        {
            // Magic damage modifiers implemented in Unit.SpellDamageBonusDone
            // This information for client side use only
            // Get healing bonus for all schools
            SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModHealingDonePos), SpellBaseHealingBonusDone(SpellSchoolMask.All));
            // Get damage bonus for all schools
            var modDamageAuras = GetAuraEffectsByType(AuraType.ModDamageDone);
            for (int i = (int)SpellSchools.Holy; i < (int)SpellSchools.Max; ++i)
            {
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDoneNeg, i), modDamageAuras.Aggregate(0, (negativeMod, aurEff) =>
                {
                    if (aurEff.GetAmount() < 0 && Convert.ToBoolean(aurEff.GetMiscValue() & (1 << i)))
                        negativeMod += aurEff.GetAmount();
                    return negativeMod;
                }));
                SetUpdateFieldStatValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDonePos, i),
                    (SpellBaseDamageBonusDone((SpellSchoolMask)(1 << i)) - m_activePlayerData.ModDamageDoneNeg[i]));
            }

            if (HasAuraType(AuraType.OverrideAttackPowerBySpPct))
            {
                UpdateAttackPowerAndDamage();
                UpdateAttackPowerAndDamage(true);
            }
        }

        public int GetBaseSpellPowerBonus() { return m_baseSpellPower; }

        public override void UpdateAttackPowerAndDamage(bool ranged = false)
        {
            float val2;
            float level = GetLevel();

            var entry = CliDB.ChrClassesStorage.LookupByKey((int)GetClass());
            UnitMods unitMod = ranged ? UnitMods.AttackPowerRanged : UnitMods.AttackPower;

            if (!HasAuraType(AuraType.OverrideAttackPowerBySpPct))
            {
                if (!ranged)
                {
                    float strengthValue = Math.Max((GetStat(Stats.Strength)) * entry.AttackPowerPerStrength, 0.0f);
                    float agilityValue = Math.Max((GetStat(Stats.Agility)) * entry.AttackPowerPerAgility, 0.0f);
                    float classSpecificBonus = GetClass() switch
                    {
                        Class.Warrior or Class.Paladin or Class.Deathknight => GetLevel() * 3.0f - 20.0f,
                        Class.Rogue or Class.Hunter or Class.Shaman or Class.Druid => GetLevel() * 2.0f - 20.0f,
                        _ => -20f
                    };

                    val2 = strengthValue + agilityValue + classSpecificBonus;
                }
                else
                    val2 = (level + Math.Max(GetStat(Stats.Agility), 0.0f)) * entry.RangedAttackPowerPerAgility - 10.0f;
            }
            else
            {
                int minSpellPower = m_activePlayerData.ModHealingDonePos;
                for (var i = SpellSchools.Holy; i < SpellSchools.Max; ++i)
                    minSpellPower = Math.Min(minSpellPower, m_activePlayerData.ModDamageDonePos[(int)i]);

                val2 = MathFunctions.CalculatePct(minSpellPower, m_activePlayerData.OverrideAPBySpellPowerPercent);
            }

            SetStatFlatModifier(unitMod, UnitModifierFlatType.Base, val2);

            float base_attPower = GetFlatModifierValue(unitMod, UnitModifierFlatType.Base) * GetPctModifierValue(unitMod, UnitModifierPctType.Base);
            float attPowerMod = GetFlatModifierValue(unitMod, UnitModifierFlatType.Total);
            float attPowerMultiplier = GetPctModifierValue(unitMod, UnitModifierPctType.Total) - 1.0f;

            if (ranged)
            {
                SetRangedAttackPower((int)base_attPower);
                SetRangedAttackPowerModPos((int)attPowerMod);
                SetRangedAttackPowerMultiplier(attPowerMultiplier);
            }
            else
            {
                SetAttackPower((int)base_attPower);
                SetAttackPowerModPos((int)attPowerMod);
                SetAttackPowerMultiplier(attPowerMultiplier);
            }

            Pet pet = GetPet();                                //update pet's AP
            Guardian guardian = GetGuardianPet();
            //automatically update weapon damage after attack power modification
            if (ranged)
            {
                UpdateDamagePhysical(WeaponAttackType.RangedAttack);
                if (pet != null && pet.IsHunterPet()) // At ranged attack change for hunter pet
                    pet.UpdateAttackPowerAndDamage();
            }
            else
            {
                UpdateDamagePhysical(WeaponAttackType.BaseAttack);
                Item offhand = GetWeaponForAttack(WeaponAttackType.OffAttack, true);
                if (offhand != null)
                {
                    if (CanDualWield() || offhand.GetTemplate().HasFlag(ItemFlags3.AlwaysAllowDualWield))
                        UpdateDamagePhysical(WeaponAttackType.OffAttack);
                }

                if (HasAuraType(AuraType.OverrideSpellPowerByApPct))
                    UpdateSpellDamageAndHealingBonus();

                if (pet != null && pet.IsPetGhoul()) // At melee attack power change for DK pet
                    pet.UpdateAttackPowerAndDamage();

                if (guardian != null && guardian.IsSpiritWolf()) // At melee attack power change for Shaman feral spirit
                    guardian.UpdateAttackPowerAndDamage();
            }
        }

        public void UpdateShieldBlockValue()
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ShieldBlock), GetShieldBlockValue());
        }

        public override void UpdateArmor()
        {
            UnitMods unitMod = UnitMods.Armor;

            float value = GetFlatModifierValue(unitMod, UnitModifierFlatType.Base);     // base armor
            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Base);            // armor percent
            value += GetStat(Stats.Agility) * 2.0f;                                      // armor bonus from stats
            float baseValue = value;

            value += GetFlatModifierValue(unitMod, UnitModifierFlatType.Total);        // bonus armor from auras and items

            // add dynamic flat mods
            var mResbyIntellect = GetAuraEffectsByType(AuraType.HandleAuraModResistenceOfStatPercent);
            foreach (var aurEff in mResbyIntellect)
            {
                if (aurEff.GetMiscValue().HasAnyFlag((int)SpellSchoolMask.Normal))
                    value += MathFunctions.CalculatePct(GetStat((Stats)aurEff.GetMiscValueB()), aurEff.GetAmount());
            }

            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Total);
            value *= GetTotalAuraMultiplier(AuraType.ModBonusArmorPct);

            SetArmor((int)value, (int)(value - baseValue));

            Pet pet = GetPet();
            if (pet != null)
                pet.UpdateArmor();

            UpdateAttackPowerAndDamage();                           // armor dependent auras update for SPELL_AURA_MOD_ATTACK_POWER_OF_ARMOR
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
                if (Convert.ToBoolean(aurEff.GetMiscValue() & (1 << (int)cr)))
                    amount += MathFunctions.CalculatePct(amount, aurEff.GetAmount());

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
                        UpdateSpellCritChance();
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
                                if (GetClass() == Class.Deathknight)
                                    UpdateAllRunesRegen();
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
                0.9830f,  // Monk
                0.9720f,  // Druid
                0.9830f,  // Demon Hunter
                0.9880f,  // Evoker
                1.0f,     // Adventurer
            ];

            //  1     1     k              cx
            // --- = --- + --- <=> x' = --------
            //  x'    c     x            x + ck

            // where:
            // k  is m_diminishing_k for that class
            // c  is capArray for that class
            // x  is Chance before DR (diminishValue)
            // x' is Chance after DR (our result)

            uint classIdx = (byte)playerClass - 1u;

            float k = m_diminishing_k[classIdx];
            float c = capArray[classIdx];

            float result = c * diminishValue / (diminishValue + c * k);
            result += nonDiminishValue;
            return result;
        }

        float[] parry_cap =
        [
            65.631440f,     // Warrior
            65.631440f,     // Paladin
            145.560408f,    // Hunter
            145.560408f,    // Rogue
            0.0f,           // Priest
            65.631440f,     // DK
            145.560408f,    // Shaman
            0.0f,           // Mage
            0.0f,           // Warlock
            90.6425f,       // Monk
            0.0f,           // Druid
            65.631440f,     // Demon Hunter
            0.0f,           // Evoker
            0.0f,           // Adventurer
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
            }
            SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ParryPercentage), value);
        }
        
        float[] dodge_cap =
        [
            65.631440f,     // Warrior            
            65.631440f,     // Paladin
            145.560408f,    // Hunter
            145.560408f,    // Rogue
            150.375940f,    // Priest
            65.631440f,     // DK
            145.560408f,    // Shaman
            150.375940f,    // Mage
            150.375940f,    // Warlock
            145.560408f,    // Monk
            116.890707f,    // Druid
            145.560408f,    // Demon Hunter
            145.560408f,    // Evoker
            0.0f,           // Adventurer
        ];

        public void UpdateDodgePercentage()
        {
            float diminishing = 0.0f, nondiminishing = 0.0f;
            GetDodgeFromAgility(diminishing, nondiminishing);
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
            }
            SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.BlockPercentage), value);
        }

        public void UpdateCritPercentage(WeaponAttackType attType)
        {
            static float applyCritLimit(float value)
            {
                if (WorldConfig.Values[WorldCfg.StatsLimitsEnable].Bool)
                    value = value > WorldConfig.Values[WorldCfg.StatsLimitsCrit].Float ? WorldConfig.Values[WorldCfg.StatsLimitsCrit].Float : value;
                return value;
            }

            switch (attType)
            {
                case WeaponAttackType.OffAttack:
                    SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.OffhandCritPercentage),
                        applyCritLimit(GetBaseModValue(BaseModGroup.OffhandCritPercentage, BaseModType.FlatMod) + GetBaseModValue(BaseModGroup.OffhandCritPercentage, BaseModType.PctMod) + GetRatingBonusValue(CombatRating.CritMelee)));
                    break;
                case WeaponAttackType.RangedAttack:
                    SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.RangedCritPercentage),
                        applyCritLimit(GetBaseModValue(BaseModGroup.RangedCritPercentage, BaseModType.FlatMod) + GetBaseModValue(BaseModGroup.RangedCritPercentage, BaseModType.PctMod) + GetRatingBonusValue(CombatRating.CritRanged)));
                    break;
                case WeaponAttackType.BaseAttack:
                default:
                    SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CritPercentage),
                        applyCritLimit(GetBaseModValue(BaseModGroup.CritPercentage, BaseModType.FlatMod) + GetBaseModValue(BaseModGroup.CritPercentage, BaseModType.PctMod) + GetRatingBonusValue(CombatRating.CritMelee)));
                    break;
            }
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

        public void UpdateSpellCritChance()
        {
            // For others recalculate it from:
            float crit = 5.0f;
            // Increase crit from SPELL_AURA_MOD_SPELL_CRIT_CHANCE
            crit += GetTotalAuraModifier(AuraType.ModSpellCritChance);
            // Increase crit from SPELL_AURA_MOD_CRIT_PCT
            crit += GetTotalAuraModifier(AuraType.ModCritPct);
            // Increase crit from spell crit ratings
            crit += GetRatingBonusValue(CombatRating.CritSpell);

            // Store crit value
            for (SpellSchools school = SpellSchools.Holy; school < SpellSchools.Max; school++)
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SpellCritPercentage, (int)school), crit);
        }

        public void UpdateMeleeHitChances()
        {
            ModMeleeHitChance = 7.5f + GetRatingBonusValue(CombatRating.HitMelee);
        }

        public void UpdateRangedHitChances()
        {
            ModRangedHitChance = 7.5f + GetRatingBonusValue(CombatRating.HitRanged);
        }

        public void UpdateSpellHitChances()
        {
            ModSpellHitChance = 15.0f + GetTotalAuraModifier(AuraType.ModSpellHitChance);
            ModSpellHitChance += GetRatingBonusValue(CombatRating.HitSpell);
        }
        
        public override void UpdateMaxHealth()
        {
            UnitMods unitMod = UnitMods.Health;

            float value = GetFlatModifierValue(unitMod, UnitModifierFlatType.Base) + GetCreateHealth();
            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Base);
            value += GetFlatModifierValue(unitMod, UnitModifierFlatType.Total) + GetHealthBonusFromStamina();
            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Total);

            SetMaxHealth((uint)value);
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

            float bonusPower = (power == PowerType.Mana && GetCreatePowerValue(power) > 0) ? GetManaBonusFromIntellect() : 0;

            float value = GetFlatModifierValue(unitMod, UnitModifierFlatType.Base) + GetCreatePowerValue(power);
            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Base);
            value += GetFlatModifierValue(unitMod, UnitModifierFlatType.Total) + bonusPower;
            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Total);

            SetMaxPower(power, (int)Math.Round(value));
        }

        public void ApplySpellPenetrationBonus(int amount, bool apply)
        {
            ApplyModTargetResistance(-amount, apply);
            m_spellPenetrationItemMod += apply ? amount : -amount;
        }

        void ApplyManaRegenBonus(int amount, bool apply)
        {
            _Modify(apply, ref m_baseManaRegen, ref amount);
            UpdateManaRegen();
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
                UpdateAttackPowerAndDamage();
                UpdateAttackPowerAndDamage(true);
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

    public partial class Creature
    {
        public override int GetCreatePowerValue(PowerType power)
        {
            var powerType = Global.DB2Mgr.GetPowerTypeEntry(power);
            if (powerType != null)
                if (!powerType.HasFlag(PowerTypeFlags.IsUsedByNPCs))
                    return 0;

            return base.GetCreatePowerValue(power);
        }
        
        public override bool UpdateStats(Stats stat)
        {
            return true;
        }

        public override bool UpdateAllStats()
        {
            UpdateMaxHealth();
            UpdateAttackPowerAndDamage();
            UpdateAttackPowerAndDamage(true);

            for (var i = PowerType.Mana; i < PowerType.Max; ++i)
                UpdateMaxPower(i);

            UpdateAllResistances();

            return true;
        }

        public override void UpdateArmor()
        {
            float baseValue = GetFlatModifierValue(UnitMods.Armor, UnitModifierFlatType.Base);
            float value = GetTotalAuraModValue(UnitMods.Armor);
            SetArmor((int)baseValue, (int)(value - baseValue));
        }

        public override void UpdateMaxHealth()
        {
            float value = GetTotalAuraModValue(UnitMods.Health);
            SetMaxHealth((uint)value);
        }

        public override int GetPowerIndex(PowerType powerType)
        {
            if (powerType == GetPowerType())
                return 0;

            switch (powerType)
            {
                case PowerType.ComboPoints:
                    return 2;
                default:
                    break;
            }

            return (int)PowerType.Max;
        }

        public override void UpdateMaxPower(PowerType power)
        {
            if (GetPowerIndex(power) == (uint)PowerType.Max)
                return;

            UnitMods unitMod = UnitMods.PowerStart + (int)power;

            float value = GetFlatModifierValue(unitMod, UnitModifierFlatType.Base) + GetCreatePowerValue(power);
            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Base);
            value += GetFlatModifierValue(unitMod, UnitModifierFlatType.Total);
            value *= GetPctModifierValue(unitMod, UnitModifierPctType.Total);

            SetMaxPower(power, (int)Math.Round(value));
        }

        public override void UpdateAttackPowerAndDamage(bool ranged = false)
        {
            UnitMods unitMod = ranged ? UnitMods.AttackPowerRanged : UnitMods.AttackPower;

            float baseAttackPower = GetFlatModifierValue(unitMod, UnitModifierFlatType.Base) * GetPctModifierValue(unitMod, UnitModifierPctType.Base);
            float attackPowerMultiplier = GetPctModifierValue(unitMod, UnitModifierPctType.Total) - 1.0f;

            if (ranged)
            {
                SetRangedAttackPower((int)baseAttackPower);
                SetRangedAttackPowerMultiplier(attackPowerMultiplier);
            }
            else
            {
                SetAttackPower((int)baseAttackPower);
                SetAttackPowerMultiplier(attackPowerMultiplier);
            }

            //automatically update weapon damage after attack power modification
            if (ranged)
                UpdateDamagePhysical(WeaponAttackType.RangedAttack);
            else
            {
                UpdateDamagePhysical(WeaponAttackType.BaseAttack);
                UpdateDamagePhysical(WeaponAttackType.OffAttack);
            }
        }

        public override void CalculateMinMaxDamage(WeaponAttackType attType, bool normalized, bool addTotalPct, out float minDamage, out float maxDamage)
        {
            float variance;
            UnitMods unitMod;
            switch (attType)
            {
                case WeaponAttackType.BaseAttack:
                default:
                    variance = GetCreatureTemplate().BaseVariance;
                    unitMod = UnitMods.DamageMainHand;
                    break;
                case WeaponAttackType.OffAttack:
                    variance = GetCreatureTemplate().BaseVariance;
                    unitMod = UnitMods.DamageOffHand;
                    break;
                case WeaponAttackType.RangedAttack:
                    variance = GetCreatureTemplate().RangeVariance;
                    unitMod = UnitMods.DamageRanged;
                    break;
            }

            if (attType == WeaponAttackType.OffAttack && !HaveOffhandWeapon())
            {
                minDamage = 0.0f;
                maxDamage = 0.0f;
                return;
            }

            float weaponMinDamage = GetWeaponDamageRange(attType, WeaponDamageRange.MinDamage);
            float weaponMaxDamage = GetWeaponDamageRange(attType, WeaponDamageRange.MaxDamage);

            if (!CanUseAttackType(attType)) // disarm case
            {
                weaponMinDamage = 0.0f;
                weaponMaxDamage = 0.0f;
            }

            float attackPower = GetTotalAttackPowerValue(attType);
            float attackSpeedMulti = Math.Max(GetAPMultiplier(attType, normalized), 0.25f);
            float baseValue = GetFlatModifierValue(unitMod, UnitModifierFlatType.Base) + (attackPower / 14.0f) * variance;
            float basePct = GetPctModifierValue(unitMod, UnitModifierPctType.Base) * attackSpeedMulti;
            float totalValue = GetFlatModifierValue(unitMod, UnitModifierFlatType.Total);
            float totalPct = addTotalPct ? GetPctModifierValue(unitMod, UnitModifierPctType.Total) : 1.0f;
            float dmgMultiplier = GetCreatureDifficulty().DamageModifier; // = DamageModifier * _GetDamageMod(rank);

            minDamage = ((weaponMinDamage + baseValue) * dmgMultiplier * basePct + totalValue) * totalPct;
            maxDamage = ((weaponMaxDamage + baseValue) * dmgMultiplier * basePct + totalValue) * totalPct;
        }
    }
}
