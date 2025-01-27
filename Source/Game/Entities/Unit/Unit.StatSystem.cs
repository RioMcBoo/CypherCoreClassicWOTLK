// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Game.Entities
{
    public partial class Unit
    {
        public UnitModManager StatMods => m_unitStatModManager;

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

        public void UpdateResistanceBuffModForClient(SpellSchools spellSchool, float pos, float neg)
        {
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ResistanceBuffModsPositive, (int)spellSchool), (int)pos);
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ResistanceBuffModsNegative, (int)spellSchool), (int)neg);
        }

        public virtual bool UpdateStats(Stats stat, bool skipDependents = false) { return false; }

        public virtual bool UpdateAllStats() { return false; }

        public virtual void UpdateResistances(SpellSchools school, bool skipDependents = false)
        {
            if (school != SpellSchools.Normal)
            {
                UnitMods unitMod = UnitMods.ResistanceStart + (int)school;
                SetResistance(school, (int)StatMods.GetTotal(unitMod));
            }
        }
        
        public virtual void UpdateArmor(bool skipDependents = false) { }        

        public virtual void UpdateMaxHealth() { }

        public virtual void UpdateMaxPower(PowerType power) { }

        public virtual void UpdateMeleeAttackPowerAndDamage(bool skipDependents = false) { }
        public virtual void UpdateRangedAttackPowerAndDamage(bool skipDependents = false) { }
        public virtual void UpdateDamageSpell(SpellSchools school) { }

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
                UpdateResistances(i, true);
        }

        //Stats
        public float GetStat(Stats stat) { return m_unitData.Stats[(int)stat]; }

        public void SetStat(Stats stat, int val)
        { 
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Stats, (int)stat), val);
        }

        public void UpdateStatBuffModForClient(Stats stat, float pos, float neg)
        {
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.StatPosBuff, (int)stat), (int)pos);
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.StatNegBuff, (int)stat), (int)neg);
        }

        public int GetCreateMana() { return m_unitData.BaseMana; }

        public void SetCreateMana(int val)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.BaseMana), val);
        }

        public int GetArmor()
        {
            return GetResistance(SpellSchools.Normal);
        }

        public void SetArmor(int val)
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
            return StatMods.GetTotal(unitMod);
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

        public void SetAttackPowerMultiplier(float attackPowerMult) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AttackPowerMultiplier), attackPowerMult - 1.0f); }

        public void SetRangedAttackPower(int attackPower) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.RangedAttackPower), attackPower); }

        public void SetRangedAttackPowerModPos(int attackPowerMod) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.RangedAttackPowerModPos), attackPowerMod); }

        public void SetRangedAttackPowerModNeg(int attackPowerMod) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.RangedAttackPowerModNeg), attackPowerMod); }

        public void SetRangedAttackPowerMultiplier(float attackPowerMult) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.RangedAttackPowerMultiplier), attackPowerMult - 1.0f); }

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
                    chance += GetTotalAuraModifier(AuraType.ModWeaponCritPct);
                    chance += GetTotalAuraModifier(AuraType.ModSpellCritPct);
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
}
