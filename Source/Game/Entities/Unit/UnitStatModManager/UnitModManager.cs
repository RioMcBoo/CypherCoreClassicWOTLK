// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;

namespace Game.Entities
{
    public enum UnitMods
    {
        StatStrength, // STAT_STRENGTH..UNIT_MOD_STAT_SPIRIT must be in existed order, it's accessed by index values of Stats enum.
        StatAgility,
        StatStamina,
        StatIntellect,
        StatSpirit,
        Health,
        Mana, // UNIT_MOD_MANA..UNIT_MOD_PAIN must be listed in existing order, it is accessed by index values of Powers enum.
        Rage,
        Focus,
        Energy,
        Happiness,
        Runes,
        RunicPower,
        Armor, // ARMOR..RESISTANCE_ARCANE must be in existed order, it's accessed by index values of SpellSchools enum.
        ResistanceHoly,
        ResistanceFire,
        ResistanceNature,
        ResistanceFrost,
        ResistanceShadow,
        ResistanceArcane,
        AttackPowerMelee,
        AttackPowerRanged,
        DamagePhysical,
        DamageHoly,
        DamageFire,
        DamageNature,
        DamageFrost,
        DamageShadow,
        DamageArcane,
        DamageMainHand,
        DamageOffHand,
        DamageRanged,
        End,

        // synonyms
        StatStart = StatStrength,
        StatEnd = StatSpirit + 1,
        ResistanceStart = Armor,
        ResistanceEnd = ResistanceArcane + 1,
        PowerStart = Mana,
        PowerEnd = RunicPower + 1,
        SpellDamageStart = DamagePhysical,
        SpellDamageEnd = DamageArcane + 1,
    }

    public enum UnitModType : byte
    {
        None,
        BasePermanent,
        BaseTemporary,
        TotalPermanent,
        TotalTemporary,        
        Max,
    }

    public enum UnitModFamily
    {
        None,
        Stat,
        Power,
        Resistance,
        SpellPower,
        AttackPower,
        SpellDamage,
        WeaponDamage,
        Max
    }

    public record struct UnitMod
    {
        public readonly UnitModType Type;
        public MultModifier Mult;
        public FlatModifier Flat;

        public UnitMod(UnitModType type)
        {
            Type = type;
            Mult = new();
            Flat = new();
        }

        public UnitMod(UnitMod other)
        {
            Type = other.Type;
            Mult = other.Mult;
            Flat = other.Flat;
        }

        public void ApplyMultTo(ref float statValue)
        {
            statValue *= Mult.TotalValue;
        }

        public void ApplyMultTo(UnitModResult unitModResult)
        {
            ApplyMultTo(ref unitModResult.TotalValue, ref unitModResult.ModPos, ref unitModResult.ModNeg);
        }

        public void ApplyMultTo(ref float statValue, ref float modPos, ref float modNeg)
        {            
            if (Mult.IsIdle)
                return;

            if (Type == UnitModType.BaseTemporary || Type == UnitModType.TotalTemporary)
            {
                modPos += MathFunctions.CalculateFraction(statValue, Mult.Positive - MultModifier.IdleModifier);
            }
            else if (Type == UnitModType.TotalPermanent)
            {
                modPos *= Mult.Positive;
            }
            
            modNeg -= MathFunctions.CalculateFraction(statValue, MultModifier.IdleModifier - Mult.Negative);

            statValue *= Mult.TotalValue;
        }

        public void ApplyFlatTo(ref float statValue)
        {
            statValue += Flat.TotalValue;
        }

        public void ApplyFlatTo(UnitModResult unitModResult)
        {
            ApplyFlatTo(ref unitModResult.TotalValue, ref unitModResult.ModPos, ref unitModResult.ModNeg);
        }

        public void ApplyFlatTo(ref float statValue, ref float modPos, ref float modNeg)
        {
            if (Flat.IsIdle)
                return;

            if (Type == UnitModType.BaseTemporary || Type == UnitModType.TotalTemporary)
                modPos += Flat.Positive;

            modNeg += Flat.Negative;

            statValue += Flat.TotalValue;
        }

        public UnitMod Modify(UnitMod? other, bool apply)
        {
            if (other.HasValue)
            {
                Flat.Modify(other.Value.Flat, apply);
                Mult.Modify(other.Value.Mult, apply);
            }

            return this;
        }

        public bool IsIdle => Mult.IsIdle && Flat.IsIdle;
    }

    public class UnitModResult
    {
        public float TotalValue;
        public float ModPos;
        public float ModNeg;
        public float NakedValue => TotalValue - ModPos - ModNeg;

        public UnitModResult(float initialValue)
        {
            TotalValue = initialValue;
        }

        public UnitModResult() { }
    }

    public class UnitModCache
    {
        public UnitMod? BasePermanent;
        public UnitMod? BaseTemporary;
        public UnitMod? TotalPermanent;
        public UnitMod? TotalTemporary;
        public bool IgnoreTemporary;

        public UnitModCache ReApplyTo(UnitModResult statValue)
        {
            UnitModManager.ApplyModsTo(statValue, this);
            return this;
        }
    }

    public class UnitModManager
    {
        public UnitModManager(Unit owner)
        {
            _owner = owner;
        }

        public static UnitModCache ApplyModsTo(UnitModResult statValue, UnitModCache cache)
        {
            // value = ((base_value * base_pct) + total_value) * total_pct

            // base_value
            cache.BasePermanent?.ApplyFlatTo(ref statValue.TotalValue, ref statValue.ModPos, ref statValue.ModNeg);
            if (!cache.IgnoreTemporary)
                cache.BaseTemporary?.ApplyFlatTo(ref statValue.TotalValue, ref statValue.ModPos, ref statValue.ModNeg);

            // base_pct
            cache.BasePermanent?.ApplyMultTo(ref statValue.TotalValue, ref statValue.ModPos, ref statValue.ModNeg);
            if (!cache.IgnoreTemporary)
                cache.BaseTemporary?.ApplyMultTo(ref statValue.TotalValue, ref statValue.ModPos, ref statValue.ModNeg);

            // total_value
            cache.TotalPermanent?.ApplyFlatTo(ref statValue.TotalValue, ref statValue.ModPos, ref statValue.ModNeg);
            if (!cache.IgnoreTemporary)
                cache.TotalTemporary?.ApplyFlatTo(ref statValue.TotalValue, ref statValue.ModPos, ref statValue.ModNeg);

            // total_pct
            cache.TotalPermanent?.ApplyMultTo(ref statValue.TotalValue, ref statValue.ModPos, ref statValue.ModNeg);
            if (!cache.IgnoreTemporary)
                cache.TotalTemporary?.ApplyMultTo(ref statValue.TotalValue, ref statValue.ModPos, ref statValue.ModNeg);

            return cache;
        }

        public UnitModCache ApplyModsTo(UnitModResult statValue, UnitMods unitMod, bool ignoreTemporary = false, UnitMod? myBasePerm = default, UnitMod? myBaseTemp = default, UnitMod? myTotalPerm = default, UnitMod? myTotalTemp = default)
        {
            UnitModCache cache = new() { IgnoreTemporary = ignoreTemporary };

            cache.BasePermanent = Get(unitMod, UnitModType.BasePermanent);
            if (cache.BasePermanent.HasValue)
                cache.BasePermanent = new(cache.BasePermanent.Value.Modify(myBasePerm, true));
            else
                cache.BasePermanent = myBasePerm;

            cache.BaseTemporary = Get(unitMod, UnitModType.BaseTemporary);
            if (cache.BaseTemporary.HasValue)
                cache.BaseTemporary = new(cache.BaseTemporary.Value.Modify(myBaseTemp, true));
            else
                cache.BaseTemporary = myBaseTemp;

            cache.TotalPermanent = Get(unitMod, UnitModType.TotalPermanent);
            if (cache.TotalPermanent.HasValue)
                cache.TotalPermanent = new(cache.TotalPermanent.Value.Modify(myTotalPerm, true));
            else
                cache.TotalPermanent = myTotalPerm;

            cache.TotalTemporary = Get(unitMod, UnitModType.TotalTemporary);
            if (cache.TotalTemporary.HasValue)
                cache.TotalTemporary = new(cache.TotalTemporary.Value.Modify(myTotalTemp, true));
            else
                cache.TotalTemporary = myTotalTemp;

            return ApplyModsTo(statValue, cache);
        }

        public float GetBase(UnitMods unitMod)
        {
            float baseValue = 0;

            Get(unitMod, UnitModType.BasePermanent)?.ApplyFlatTo(ref baseValue);
            Get(unitMod, UnitModType.BaseTemporary)?.ApplyFlatTo(ref baseValue);

            Get(unitMod, UnitModType.BasePermanent)?.ApplyMultTo(ref baseValue);
            Get(unitMod, UnitModType.BaseTemporary)?.ApplyMultTo(ref baseValue);

            return baseValue;
        }

        public float GetTotal(UnitMods unitMod)
        {
            float totalValue = GetBase(unitMod);

            Get(unitMod, UnitModType.TotalPermanent)?.ApplyFlatTo(ref totalValue);
            Get(unitMod, UnitModType.TotalTemporary)?.ApplyFlatTo(ref totalValue);

            Get(unitMod, UnitModType.TotalPermanent)?.ApplyMultTo(ref totalValue);
            Get(unitMod, UnitModType.TotalTemporary)?.ApplyMultTo(ref totalValue);

            return totalValue;
        }

        public UnitMod? Get(UnitMods unitModName, UnitModType type = UnitModType.TotalTemporary)
        {
            return SearchModToRead(type, GetFamilyFromUnitModName(unitModName), unitModName)?.Mod;
        }

        public UnitMod GetOrDefault(UnitMods unitModName, UnitModType type = UnitModType.TotalTemporary)
        {
            var found = Get(unitModName, type);

            if (found.HasValue)
                return found.Value;

            return new(type);
        }

        public void SetMult(UnitMods unitModName, MultModifier multiplier, UnitModType type = UnitModType.TotalTemporary)
        {
            var newMod = new UnitMod(type);
            newMod.Mult = multiplier;

            var existingMod = SearchModToWrite(type, GetFamilyFromUnitModName(unitModName), unitModName);
            if (existingMod.Mod != newMod)
            {
                existingMod.Mod = newMod;
                UpdateUnitMod(unitModName);
            }
        }

        public void SetFlat(UnitMods unitModName, FlatModifier modifier, UnitModType type = UnitModType.TotalTemporary)
        {
            var newMod = new UnitMod(type);
            newMod.Flat = modifier;

            var existingMod = SearchModToWrite(type, GetFamilyFromUnitModName(unitModName), unitModName);
            if (existingMod.Mod != newMod)
            {
                existingMod.Mod = newMod;
                UpdateUnitMod(unitModName);
            }
        }

        public void Update(UnitMods unitModName)
        {
            UpdateUnitMod(unitModName);
        }

        public void ModifyMaxMultWithExchange(UnitMods unitModName, MultModifier modForApply, MultModifier modForReplacement, bool apply, UnitModType type = UnitModType.TotalTemporary)
        {
            if (modForApply == modForReplacement)
                return;

            bool isPositive = modForApply.Positive != MultModifier.IdleModifier || modForReplacement.Positive != MultModifier.IdleModifier; // Max positive
            bool isNegative = modForApply.Negative != MultModifier.IdleModifier || modForReplacement.Negative != MultModifier.IdleModifier; // Max negative

            if (isPositive == isNegative)
                return;

            // Modify only if valueForApply is larger than valueForReplacement
            if ((isPositive && modForApply > modForReplacement) || (isNegative && modForApply < modForReplacement))
            {
                float modifier = modForApply / modForReplacement;

                var family = GetFamilyFromUnitModName(unitModName);
                var existingMod = SearchModToWrite(type, family, unitModName);
                existingMod.Modify(new MultModifier(modifier), apply);
                CheckForRemove(existingMod, type, family, unitModName);
                UpdateUnitMod(unitModName);
            }
        }

        public void ModifyMaxFlatWithExchange(UnitMods unitModName, FlatModifier modForApply, FlatModifier modForReplacement, bool apply, UnitModType type = UnitModType.TotalTemporary)
        {
            if (modForApply == modForReplacement)
                return;

            bool isPositive = modForApply.Positive != FlatModifier.IdleModifier || modForReplacement.Positive != FlatModifier.IdleModifier; // Max positive
            bool isNegative = modForApply.Negative != FlatModifier.IdleModifier || modForReplacement.Negative != FlatModifier.IdleModifier; // Max negative

            if (isPositive == isNegative)
                return;

            // Modify only if valueForApply is larger than valueForReplacement
            if ((isPositive && modForApply > modForReplacement) || (isNegative && modForApply < modForReplacement))
            {
                int modifier = modForApply - modForReplacement;

                var family = GetFamilyFromUnitModName(unitModName);
                var existingMod = SearchModToWrite(type, family, unitModName);
                existingMod.Modify(new FlatModifier(modifier), apply);
                CheckForRemove(existingMod, type, family, unitModName);
                UpdateUnitMod(unitModName);
            }
        }

        public void ModifyMult(UnitMods unitModName, MultModifier multiplier, bool apply, UnitModType type = UnitModType.TotalTemporary)
        {
            if (multiplier.IsIdle)
                return;

            var family = GetFamilyFromUnitModName(unitModName);
            var existingMod = SearchModToWrite(type, family, unitModName);
            existingMod.Modify(multiplier, apply);
            CheckForRemove(existingMod, type, family, unitModName);
            UpdateUnitMod(unitModName);
        }

        public void ModifyFlat(UnitMods unitModName, FlatModifier modifier, bool apply, UnitModType type = UnitModType.TotalTemporary)
        {
            if (modifier.IsIdle)
                return;

            var family = GetFamilyFromUnitModName(unitModName);
            var existingMod = SearchModToWrite(type, family, unitModName);
            existingMod.Modify(modifier, apply);
            CheckForRemove(existingMod, type, family, unitModName);
            UpdateUnitMod(unitModName);
        }

        private SpellSchools GetSpellSchoolByAuraGroup(UnitMods unitModName)
        {
            SpellSchools school = SpellSchools.Normal;

            switch (unitModName)
            {
                case UnitMods.ResistanceHoly:
                case UnitMods.DamageHoly:
                    school = SpellSchools.Holy;
                    break;
                case UnitMods.ResistanceFire:
                case UnitMods.DamageFire:
                    school = SpellSchools.Fire;
                    break;
                case UnitMods.ResistanceNature:
                case UnitMods.DamageNature:
                    school = SpellSchools.Nature;
                    break;
                case UnitMods.ResistanceFrost:
                case UnitMods.DamageFrost:
                    school = SpellSchools.Frost;
                    break;
                case UnitMods.ResistanceShadow:
                case UnitMods.DamageShadow:
                    school = SpellSchools.Shadow;
                    break;
                case UnitMods.ResistanceArcane:
                case UnitMods.DamageArcane:
                    school = SpellSchools.Arcane;
                    break;
            }

            return school;
        }

        private Stats GetStatByAuraGroup(UnitMods unitModName)
        {
            Stats stat = Stats.Strength;

            switch (unitModName)
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

        private void UpdateUnitMod(UnitMods unitModName)
        {
            if (!_owner.CanModifyStats())
                return;

            switch (unitModName)
            {
                case UnitMods.StatStrength:
                case UnitMods.StatAgility:
                case UnitMods.StatStamina:
                case UnitMods.StatIntellect:
                case UnitMods.StatSpirit:
                    _owner.UpdateStats(GetStatByAuraGroup(unitModName));
                    break;
                case UnitMods.Armor:
                    _owner.UpdateArmor();
                    break;
                case UnitMods.Health:
                    _owner.UpdateMaxHealth();
                    break;
                case UnitMods.Mana:
                case UnitMods.Rage:
                case UnitMods.Focus:
                case UnitMods.Energy:
                case UnitMods.RunicPower:
                    _owner.UpdateMaxPower((PowerType)(unitModName - UnitMods.PowerStart));
                    break;
                case UnitMods.ResistanceHoly:
                case UnitMods.ResistanceFire:
                case UnitMods.ResistanceNature:
                case UnitMods.ResistanceFrost:
                case UnitMods.ResistanceShadow:
                case UnitMods.ResistanceArcane:
                    _owner.UpdateResistances(GetSpellSchoolByAuraGroup(unitModName));
                    break;
                case UnitMods.AttackPowerMelee:
                    _owner.UpdateMeleeAttackPowerAndDamage();
                    break;
                case UnitMods.AttackPowerRanged:
                    _owner.UpdateRangedAttackPowerAndDamage();
                    break;
                case UnitMods.DamagePhysical:
                    _owner.UpdateDamagePhysical(WeaponAttackType.BaseAttack);
                    _owner.UpdateDamagePhysical(WeaponAttackType.OffAttack);
                    _owner.UpdateDamagePhysical(WeaponAttackType.RangedAttack);
                    break;
                case UnitMods.DamageHoly:
                case UnitMods.DamageFire:
                case UnitMods.DamageNature:
                case UnitMods.DamageFrost:
                case UnitMods.DamageShadow:
                case UnitMods.DamageArcane:
                    _owner.UpdateDamageSpell(GetSpellSchoolByAuraGroup(unitModName));
                    break;
                case UnitMods.DamageMainHand:
                    _owner.UpdateDamagePhysical(WeaponAttackType.BaseAttack);
                    break;
                case UnitMods.DamageOffHand:
                    _owner.UpdateDamagePhysical(WeaponAttackType.OffAttack);
                    break;
                case UnitMods.DamageRanged:
                    _owner.UpdateDamagePhysical(WeaponAttackType.RangedAttack);
                    break;
                default:
                    break;
            }
        }

        private void CheckForRemove(UnitStatModNode existingMod, UnitModType type, UnitModFamily family, UnitMods unitModName)
        {
            if (existingMod.Mod.IsIdle)
                Remove(type, family, unitModName);
        }

        private UnitStatModNode SearchModToRead(UnitModType type, UnitModFamily family, UnitMods unitModName)
        {
            if (_unitStatMods != null)
                return _unitStatMods.SearchModToRead(type, family, unitModName);

            return null;
        }

        private UnitStatModNode SearchModToWrite(UnitModType type, UnitModFamily family, UnitMods unitModName)
        {
            if (_unitStatMods == null)
            {
                _unitStatMods = new()
                {
                    Type = type,
                };
            }
            else if (type < _unitStatMods.Type)
            {
                var oldNext = _unitStatMods;
                _unitStatMods = new()
                {
                    Type = type,
                    Next = oldNext,
                };
            }

            var result = _unitStatMods.SearchModToWrite(type, family, unitModName);

            // If just created
            if (result.Mod.Type == UnitModType.None)
                result.Mod = new(type);

            return result;
        }

        private void Remove(UnitModType type, UnitModFamily family, UnitMods unitModName)
        {
            _unitStatMods = _unitStatMods.Remove(type, family, unitModName);
        }

        private UnitModFamily GetFamilyFromUnitModName(UnitMods unitModName)
        {
            switch (unitModName)
            {
                case UnitMods.StatStrength:
                case UnitMods.StatAgility:
                case UnitMods.StatStamina:
                case UnitMods.StatIntellect:
                case UnitMods.StatSpirit:
                    return UnitModFamily.Stat;
                case UnitMods.Health:
                case UnitMods.Mana:
                case UnitMods.Rage:
                case UnitMods.Focus:
                case UnitMods.Energy:
                case UnitMods.Happiness:
                case UnitMods.Runes:
                case UnitMods.RunicPower:
                    return UnitModFamily.Power;
                case UnitMods.Armor:
                case UnitMods.ResistanceHoly:
                case UnitMods.ResistanceFire:
                case UnitMods.ResistanceNature:
                case UnitMods.ResistanceFrost:
                case UnitMods.ResistanceShadow:
                case UnitMods.ResistanceArcane:
                    return UnitModFamily.Resistance;
                case UnitMods.AttackPowerMelee:
                case UnitMods.AttackPowerRanged:
                    return UnitModFamily.AttackPower;
                case UnitMods.DamagePhysical:
                case UnitMods.DamageHoly:
                case UnitMods.DamageFire:
                case UnitMods.DamageNature:
                case UnitMods.DamageFrost:
                case UnitMods.DamageShadow:
                case UnitMods.DamageArcane:
                    return UnitModFamily.SpellDamage;
                case UnitMods.DamageMainHand:
                case UnitMods.DamageOffHand:
                case UnitMods.DamageRanged:
                    return UnitModFamily.WeaponDamage;
                default:
                    throw new ArgumentException();
            }
        }

        private class UnitStatModNode
        {
            public UnitMod Mod;

            public void Modify(MultModifier mod, bool apply)
            {
                Mod.Mult.Modify(mod, apply);
            }

            public void Modify(FlatModifier mod, bool apply)
            {
                Mod.Flat.Modify(mod, apply);
            }
        }

        private class UnitModNameNode
        {
            public UnitMods UnitModName;
            public UnitStatModNode ModNode;
            public UnitModNameNode Next;

            public UnitStatModNode SearchModToRead(UnitMods unitModName)
            {
                if (UnitModName == unitModName)
                    return ModNode;
                else if (Next != null)
                    return Next.SearchModToRead(unitModName);
                else
                    return null;
            }

            public UnitStatModNode SearchModToWrite(UnitMods unitModName)
            {
                if (UnitModName == unitModName)
                {
                    if (ModNode == null)
                    {
                        ModNode = new();
                    }

                    return ModNode;
                }

                if (Next == null)
                {
                    Next = new()
                    {
                        UnitModName = unitModName,
                    };
                }

                var nextModName = Next.UnitModName;

                if (unitModName < nextModName)
                {
                    var oldNext = Next;
                    Next = new()
                    {
                        UnitModName = unitModName,
                        Next = oldNext,
                    };
                }

                return Next.SearchModToWrite(unitModName);
            }

            public UnitModNameNode Remove(UnitMods unitModName)
            {
                if (UnitModName == unitModName)
                {
                    return Next;
                }
                else if (Next != null)
                {
                    Next = Next.Remove(unitModName);
                }

                return this;
            }
        }

        private class UnitModFamilyNode
        {
            public UnitModFamily Family;
            public UnitModNameNode NameNode;
            public UnitModFamilyNode Next;

            public UnitStatModNode SearchModToRead(UnitModFamily family, UnitMods unitModName)
            {
                if (Family == family)
                    return NameNode.SearchModToRead(unitModName);
                else if (Next != null)
                    return Next.SearchModToRead(family, unitModName);
                else
                    return null;
            }

            public UnitStatModNode SearchModToWrite(UnitModFamily family, UnitMods unitModName)
            {
                if (Family == family)
                {
                    if (NameNode == null)
                    {
                        NameNode = new()
                        {
                            UnitModName = unitModName,
                        };
                    }
                    else if (unitModName < NameNode.UnitModName)
                    {
                        var oldNext = NameNode;
                        NameNode = new()
                        {
                            UnitModName = unitModName,
                            Next = oldNext,
                        };
                    }

                    return NameNode.SearchModToWrite(unitModName);
                }

                if (Next == null)
                {
                    Next = new()
                    {
                        Family = family,
                    };
                }

                var nextFamily = Next.Family;

                if (family < nextFamily)
                {
                    var oldNext = Next;
                    Next = new()
                    {
                        Family = family,
                        Next = oldNext,
                    };
                }

                return Next.SearchModToWrite(family, unitModName);
            }

            public UnitModFamilyNode Remove(UnitModFamily family, UnitMods unitModName)
            {
                if (Family == family)
                {
                    NameNode = NameNode.Remove(unitModName);

                    if (NameNode == null)
                        return Next;
                }
                else if (Next != null)
                {
                    Next = Next.Remove(family, unitModName);
                }

                return this;
            }
        }

        private class UnitModTypeNode
        {
            public UnitModType Type;
            public UnitModFamilyNode FamilyNode;
            public UnitModTypeNode Next;

            public UnitStatModNode SearchModToRead(UnitModType type, UnitModFamily family, UnitMods unitModName)
            {
                if (Type == type)
                    return FamilyNode.SearchModToRead(family, unitModName);
                else if (Next != null)
                    return Next.SearchModToRead(type, family, unitModName);
                else
                    return null;
            }

            public UnitStatModNode SearchModToWrite(UnitModType type, UnitModFamily family, UnitMods unitModName)
            {
                if (Type == type)
                {
                    if (FamilyNode == null)
                    {
                        FamilyNode = new()
                        {
                            Family = family,
                        };
                    }
                    else if (family < FamilyNode.Family)
                    {
                        var oldNext = FamilyNode;
                        FamilyNode = new()
                        {
                            Family = family,
                            Next = oldNext,
                        };
                    }

                    return FamilyNode.SearchModToWrite(family, unitModName);
                }

                if (Next == null)
                {
                    Next = new()
                    {
                        Type = type,
                    };
                }

                var nextType = Next.Type;

                if (type < nextType)
                {
                    var oldNext = Next;
                    Next = new()
                    {
                        Type = type,
                        Next = oldNext,
                    };
                }

                return Next.SearchModToWrite(type, family, unitModName);
            }

            public UnitModTypeNode Remove(UnitModType type, UnitModFamily family, UnitMods unitModName)
            {
                if (Type == type)
                {
                    FamilyNode = FamilyNode.Remove(family, unitModName);

                    if (FamilyNode == null)
                        return Next;
                }
                else if (Next != null)
                {
                    Next = Next.Remove(type, family, unitModName);
                }

                return this;
            }
        }

        private Unit _owner;
        private UnitModTypeNode _unitStatMods = null;
    }
}
