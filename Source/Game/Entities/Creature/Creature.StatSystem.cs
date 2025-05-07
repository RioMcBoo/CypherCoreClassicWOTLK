// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;

namespace Game.Entities
{
    public partial class Creature
    {
        public override int GetCreatePowerValue(PowerType power)
        {
            var powerType = Global.DB2Mgr.GetPowerTypeEntry(power);
            if (powerType != null)
            {
                if (!powerType.HasFlag(PowerTypeFlags.IsUsedByNPCs))
                    return 0;
            }

            return base.GetCreatePowerValue(power);
        }
        
        public override bool UpdateStats(Stats stat, bool skipDependents)
        {
            return true;
        }

        public override bool UpdateAllStats()
        {
            UpdateMaxHealth();
            UpdateMeleeAttackPowerAndDamage(true);
            UpdateRangedAttackPowerAndDamage(true);
            UpdateDamagePhysical(WeaponAttackType.BaseAttack);
            UpdateDamagePhysical(WeaponAttackType.OffAttack);
            UpdateDamagePhysical(WeaponAttackType.RangedAttack);

            for (var i = PowerType.Mana; i < PowerType.Max; ++i)
                UpdateMaxPower(i);

            UpdateAllResistances();

            return true;
        }

        public override void UpdateArmor(bool skipDependents)
        {
            var value = StatMods.GetTotal(UnitMods.Armor);
            SetArmor((int)value);
        }

        public override void UpdateMaxHealth()
        {
            var value = StatMods.GetTotal(UnitMods.Health);
            SetMaxHealth((long)value);
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
            UnitModResult powerValue = new(0);
            UnitMod createPowerValue = new(UnitModType.BasePermanent)
            {
                Flat = new(GetCreatePowerValue(power))
            };

            StatMods.ApplyModsTo(powerValue, unitMod, myBasePerm: createPowerValue);

            SetMaxPower(power, (int)Math.Round(powerValue.TotalValue));
        }

        public override void UpdateMeleeAttackPowerAndDamage(bool skipDependents = false)
        {
            UnitMods unitMod = UnitMods.AttackPowerMelee;
            UnitModResult attackPowerValue = new(0);

            StatMods.ApplyModsTo(attackPowerValue, unitMod);

            SetAttackPower((int)attackPowerValue.NakedValue);
            SetAttackPowerModPos((int)attackPowerValue.ModPos);
            SetAttackPowerModNeg((int)attackPowerValue.ModNeg);
            SetAttackPowerMultiplier(1.0f);

            if (skipDependents)
                return;

            // automatically update weapon damage after attack power modification
            UpdateDamagePhysical(WeaponAttackType.BaseAttack);
            UpdateDamagePhysical(WeaponAttackType.OffAttack);
        }

        public override void UpdateRangedAttackPowerAndDamage(bool skipDependents = false)
        {
            UnitMods unitMod = UnitMods.AttackPowerRanged;
            UnitModResult attackPowerValueRanged = new(0);

            StatMods.ApplyModsTo(attackPowerValueRanged, unitMod);

            SetRangedAttackPower((int)attackPowerValueRanged.NakedValue);
            SetRangedAttackPowerModPos((int)attackPowerValueRanged.ModPos);
            SetRangedAttackPowerModNeg((int)attackPowerValueRanged.ModNeg);
            SetAttackPowerMultiplier(1.0f);

            if (skipDependents)
                return;

            // automatically update weapon damage after attack power modification
            UpdateDamagePhysical(WeaponAttackType.RangedAttack);            
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

            UnitModResult minDamageModified = new(weaponMinDamage);
            UnitModResult maxDamageModified = new(weaponMaxDamage);

            UnitMod baseMods = new()
            {
                Flat = new((int)(attackPower / 14.0f * variance)),
                Mult = new(attackSpeedMulti * GetCreatureDifficulty().DamageModifier), // = DamageModifier * _GetDamageMod(rank)
            };

            StatMods.ApplyModsTo(minDamageModified, unitMod, addTotalPct, myBasePerm: baseMods).ReApplyTo(maxDamageModified);

            minDamage = minDamageModified.TotalValue;
            maxDamage = maxDamageModified.TotalValue;
        }
    }
}
