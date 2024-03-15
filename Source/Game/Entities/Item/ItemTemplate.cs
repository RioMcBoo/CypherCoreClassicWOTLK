// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Miscellaneous;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Game.Entities
{
    public class ItemTemplate
    {
        public override int GetHashCode()
        {
            return BasicData.Id;
        }

        public override bool Equals(object obj)
        {
            if (obj is ItemTemplate other)
                return BasicData.Id == other.BasicData.Id;

            return ReferenceEquals(this, obj);
        }

        public bool Equals(ItemTemplate other)
        {
            if (other is not null)
                return BasicData.Id == other.BasicData.Id;

            return ReferenceEquals(this, other);
        }

        public ItemTemplate(ItemRecord item, ItemSparseRecord sparse)
        {
            BasicData = item;
            ExtendedData = sparse;

            Specializations[0] = new BitSet((int)Class.Max * PlayerConst.MaxSpecializations);
            Specializations[1] = new BitSet((int)Class.Max * PlayerConst.MaxSpecializations);
            Specializations[2] = new BitSet((int)Class.Max * PlayerConst.MaxSpecializations);
        }

        public string GetName(Locale locale = SharedConst.DefaultLocale)
        {
            return ExtendedData.Display[locale];
        }

        public bool HasSignature()
        {
            return GetMaxStackSize() == 1 &&
                GetClass() != ItemClass.Consumable &&
                GetClass() != ItemClass.Quest &&
                !HasFlag(ItemFlags.NoCreator) &&
                GetId() != 6948; /*Hearthstone*/
        }

        public bool HasFlag(ItemFlags flag) { return (ExtendedData.Flags[0] & (int)flag) != 0; }
        public bool HasFlag(ItemFlags2 flag) { return (ExtendedData.Flags[1] & (int)flag) != 0; }
        public bool HasFlag(ItemFlags3 flag) { return (ExtendedData.Flags[2] & (int)flag) != 0; }
        public bool HasFlag(ItemFlags4 flag) { return (ExtendedData.Flags[3] & (int)flag) != 0; }
        public bool HasFlag(ItemFlagsCustom customFlag) { return (FlagsCu & customFlag) != 0; }
        
        public bool CanChangeEquipStateInCombat()
        {
            switch (GetInventoryType())
            {
                case InventoryType.Relic:
                case InventoryType.Shield:
                case InventoryType.Holdable:
                    return true;
                default:
                    break;
            }

            switch (GetClass())
            {
                case ItemClass.Weapon:
                case ItemClass.Projectile:
                    return true;
            }

            return false;
        }

        static SkillType[] item_weapon_skills =
        {
            SkillType.Axes,             SkillType.TwoHandedAxes,    SkillType.Bows,     SkillType.Guns,             SkillType.Maces,
            SkillType.TwoHandedMaces,   SkillType.Polearms,         SkillType.Swords,   SkillType.TwoHandedSwords,  SkillType.Warglaives,
            SkillType.Staves,           0,                          0,                  SkillType.FistWeapons,      0,
            SkillType.Daggers,          0,                          0,                  SkillType.Crossbows,        SkillType.Wands,
            SkillType.Fishing
        };

        static SkillType[] item_armor_skills =
        {
            0, SkillType.Cloth, SkillType.Leather, SkillType.Mail, SkillType.PlateMail, 0, SkillType.Shield, 0, 0, 0, 0, 0
        };

        static SkillType[] itemProfessionSkills =
        {
            SkillType.Blacksmithing, SkillType.Leatherworking, SkillType.Alchemy,     SkillType.Herbalism,  SkillType.Cooking,
            SkillType.Mining,        SkillType.Tailoring,      SkillType.Engineering, SkillType.Enchanting, SkillType.Fishing,
            SkillType.Skinning,      SkillType.Jewelcrafting,  SkillType.Inscription, SkillType.Archaeology
        };

        public SkillType GetSkill()
        {
            switch (GetClass())
            {
                case ItemClass.Weapon:
                    if (GetSubClass().Weapon >= ItemSubClassWeapon.Max)
                        return 0;
                    else
                        return item_weapon_skills[GetSubClass().data];
                case ItemClass.Armor:
                    if (GetSubClass().Armor >= ItemSubClassArmor.Max)
                        return 0;
                    else
                        return item_armor_skills[GetSubClass().data];
                case ItemClass.Profession:
                    if (GetSubClass().Profession >= ItemSubclassProfession.Max)
                        return 0;
                    else
                        return itemProfessionSkills[GetSubClass().data];
                default:
                    return 0;
            }
        }

        public float GetDPS(int itemLevel)
        {
            ItemQuality quality = GetQuality() != ItemQuality.Heirloom ? GetQuality() : ItemQuality.Rare;
            if (GetClass() != ItemClass.Weapon || quality > ItemQuality.Artifact)
                return 0.0f;

            float dps = 0.0f;
            switch (GetInventoryType())
            {
                case InventoryType.Ammo:
                    dps = CliDB.ItemDamageAmmoStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    break;
                case InventoryType.Weapon2Hand:
                    if (HasFlag(ItemFlags2.CasterWeapon))
                        dps = CliDB.ItemDamageTwoHandCasterStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    else
                        dps = CliDB.ItemDamageTwoHandStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    break;
                case InventoryType.Ranged:
                case InventoryType.Thrown:
                case InventoryType.RangedRight:
                    switch (GetSubClass().Weapon)
                    {
                        case ItemSubClassWeapon.Wand:
                            dps = CliDB.ItemDamageOneHandCasterStorage.LookupByKey(itemLevel).Quality[(int)quality];
                            break;
                        case ItemSubClassWeapon.Bow:
                        case ItemSubClassWeapon.Gun:
                        case ItemSubClassWeapon.Crossbow:
                            if (HasFlag(ItemFlags2.CasterWeapon))
                                dps = CliDB.ItemDamageTwoHandCasterStorage.LookupByKey(itemLevel).Quality[(int)quality];
                            else
                                dps = CliDB.ItemDamageTwoHandStorage.LookupByKey(itemLevel).Quality[(int)quality];
                            break;
                        default:
                            break;
                    }
                    break;
                case InventoryType.Weapon:
                case InventoryType.WeaponMainhand:
                case InventoryType.WeaponOffhand:
                    if (HasFlag(ItemFlags2.CasterWeapon))
                        dps = CliDB.ItemDamageOneHandCasterStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    else
                        dps = CliDB.ItemDamageOneHandStorage.LookupByKey(itemLevel).Quality[(int)quality];
                    break;
                default:
                    break;
            }

            return dps;
        }

        public void GetDamage(int itemLevel, out float minDamage, out float maxDamage)
        {
            minDamage = maxDamage = 0.0f;
            float dps = GetDPS(itemLevel);
            if (dps > 0.0f)
            {
                float avgDamage = dps * GetDelay() * 0.001f;
                minDamage = (GetDmgVariance() * -0.5f + 1.0f) * avgDamage;
                maxDamage = (float)Math.Floor(avgDamage * (GetDmgVariance() * 0.5f + 1.0f) + 0.5f);
            }
        }

        public bool IsUsableByLootSpecialization(Player player, bool alwaysAllowBoundToAccount)
        {
            if (HasFlag(ItemFlags.IsBoundToAccount) && alwaysAllowBoundToAccount)
                return true;

            var spec = player.GetLootSpecId();
            if (spec == 0)
                spec = player.GetPrimarySpecialization();
            if (spec == 0)
                spec = player.GetDefaultSpecId();

            ChrSpecializationRecord chrSpecialization = CliDB.ChrSpecializationStorage.LookupByKey((int)spec);
            if (chrSpecialization == null)
                return false;

            int levelIndex = 0;
            if (player.GetLevel() >= 110)
                levelIndex = 2;
            else if (player.GetLevel() > 40)
                levelIndex = 1;

            return Specializations[levelIndex].Get(CalculateItemSpecBit(chrSpecialization));
        }

        public static int CalculateItemSpecBit(ChrSpecializationRecord spec)
        {
            return ((int)spec.ClassID - 1) * PlayerConst.MaxSpecializations + spec.OrderIndex;
        }

        public int GetRandomSuffix()  { return ExtendedData.ItemRandomSuffixGroupID; }

        public int GetRandomSuffixGroupID() { return BasicData.ItemRandomSuffixGroupID; }
        public int GetRandomSelect()  { return BasicData.RandomSelect; }

        public int GetId() { return BasicData.Id; }
        public ItemClass GetClass() { return BasicData.ClassID; }
        public ItemSubClass GetSubClass() { return BasicData.SubclassID; }
        public ItemQuality GetQuality() { return ExtendedData.OverallQualityID; }
        public int GetOtherFactionItemId() { return ExtendedData.FactionRelated; }
        public float GetPriceRandomValue() { return ExtendedData.PriceRandomValue; }
        public float GetPriceVariance() { return ExtendedData.PriceVariance; }
        public int GetBuyCount() { return Math.Max(ExtendedData.VendorStackCount, 1); }
        public uint GetBuyPrice() { return ExtendedData.BuyPrice; }
        public uint GetSellPrice() { return ExtendedData.SellPrice; }
        public InventoryType GetInventoryType() { return ExtendedData.InventoryType; }
        public ClassMask GetAllowableClass() { return ExtendedData.AllowableClass; }
        public RaceMask GetAllowableRace() { return ExtendedData.AllowableRace; }
        public int GetItemLevel() { return ExtendedData.ItemLevel; }
        public int GetBaseRequiredLevel() { return ExtendedData.RequiredLevel; }
        public SkillType GetRequiredSkill() { return (SkillType)ExtendedData.RequiredSkill; }
        public int GetRequiredSkillRank() { return ExtendedData.RequiredSkillRank; }
        public int GetRequiredSpell() { return ExtendedData.RequiredAbility; }
        public int GetRequiredReputationFaction() { return ExtendedData.MinFactionID; }
        public ReputationRank GetRequiredReputationRank() { return ExtendedData.MinReputation; }
        public int GetResistance(SpellSchools school) { return ExtendedData.Resistances[(int)school]; }
        public int GetMaxCount() { return ExtendedData.MaxCount; }
        public byte GetContainerSlots() { return ExtendedData.ContainerSlots; }
        public ItemModType GetStatModifierBonusStat(int index) { Cypher.Assert(index < ItemConst.MaxStats); return ExtendedData.StatModifierBonusStat(index); }
        public short GetStatModifierBonusAmount(int index) { Cypher.Assert(index < ItemConst.MaxStats); return ExtendedData.StatModifierBonusAmount[index]; }
        public int GetStatPercentEditor(int index) { Cypher.Assert(index < ItemConst.MaxStats); return ExtendedData.StatPercentEditor[index]; }
        public float GetStatPercentageOfSocket(int index) { Cypher.Assert(index < ItemConst.MaxStats); return ExtendedData.StatPercentageOfSocket[index]; }
        public int GetScalingStatContentTuning() { return ExtendedData.ContentTuningID; }
        public ushort GetScalingStatDistributionID() { return ExtendedData.ScalingStatDistributionID; }
        public int GetScalingStatValue() { return BasicData.ScalingStatValue; }
        public int GetPlayerLevelToItemLevelCurveId() { return ExtendedData.PlayerLevelToItemLevelCurveID; }
        public SpellSchools GetDamageType() { return ExtendedData.DamageType; }
        public uint GetDelay() { return ExtendedData.ItemDelay; }
        public float GetRangedModRange() { return ExtendedData.ItemRange; }
        public ItemBondingType GetBonding() { return (ItemBondingType)ExtendedData.Bonding; }
        public int GetPageText() { return ExtendedData.PageID; }
        public int GetStartQuest() { return ExtendedData.StartQuestID; }
        public int GetLockID() { return ExtendedData.LockID; }
        public int GetItemSet() { return ExtendedData.ItemSet; }
        public int GetArea(int index) { return ExtendedData.ZoneBound[index]; }
        public int GetMap() { return ExtendedData.InstanceBound; }
        public BagFamilyMask GetBagFamily() { return (BagFamilyMask)ExtendedData.BagFamily; }
        public int GetTotemCategory() { return ExtendedData.TotemCategoryID; }

        public SocketColor GetSocketColor(int index)
        {
            Cypher.Assert(index < ItemConst.MaxGemSockets);
            return (SocketColor)ExtendedData.SocketType[index];
        }

        public int GetShieldBlockValue(int itemLevel)
        {
            var blockEntry = CliDB.ShieldBlockRegularGameTable.GetRow(itemLevel);
            return CliDB.GetShieldBlockRegularColumnForQuality(blockEntry, GetQuality());
        }

        public int GetSocketBonus() { return ExtendedData.SocketMatchEnchantmentId; }
        public int GetGemProperties() { return ExtendedData.GemProperties; }
        public float GetQualityModifier() { return ExtendedData.QualityModifier; }
        public uint GetDuration() { return ExtendedData.DurationInInventory; }
        public int GetItemLimitCategory() { return ExtendedData.LimitCategory; }
        public HolidayIds GetHolidayID() { return (HolidayIds)ExtendedData.RequiredHoliday; }
        public float GetDmgVariance() { return ExtendedData.DmgVariance; }
        public byte GetArtifactID() { return ExtendedData.ArtifactID; }
        public Expansion GetRequiredExpansion() { return ExtendedData.ExpansionID; }

        public bool IsCurrencyToken() { return (GetBagFamily() & BagFamilyMask.CurrencyTokens) != 0; }

        public int GetMaxStackSize()
        {
            return (ExtendedData.Stackable == int.MaxValue || ExtendedData.Stackable <= 0) ? (0x7FFFFFFF - 1) : ExtendedData.Stackable;
        }

        public bool IsPotion() { return GetClass() == ItemClass.Consumable && GetSubClass().Consumable == ItemSubClassConsumable.Potion; }
        public bool IsVellum() { return HasFlag(ItemFlags3.CanStoreEnchants); }
        public bool IsConjuredConsumable() { return GetClass() == ItemClass.Consumable && HasFlag(ItemFlags.Conjured); }
        public bool IsCraftingReagent() { return HasFlag(ItemFlags2.UsedInATradeskill); }

        public bool IsWeapon() { return GetClass() == ItemClass.Weapon; }

        public bool IsArmor() { return GetClass() == ItemClass.Armor; }
        
        public bool IsRangedWeapon()
        {
            return IsWeapon() && (GetSubClass().Weapon == ItemSubClassWeapon.Bow ||
                   GetSubClass().Weapon == ItemSubClassWeapon.Gun || GetSubClass().Weapon == ItemSubClassWeapon.Crossbow);
        }

        public int MaxDurability;
        public List<ItemEffectRecord> Effects = new();

        // extra fields, not part of db2 files
        public int ScriptId;
        public uint FoodType;
        public uint MinMoneyLoot;
        public uint MaxMoneyLoot;
        public ItemFlagsCustom FlagsCu;
        public float SpellPPMRate;
        public BitSet[] Specializations = new BitSet[3];  // one set for 1-40 level range and another for 41-109 and one for 110
        public ClassMask ItemSpecClassMask;

        protected ItemRecord BasicData;
        protected ItemSparseRecord ExtendedData;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ItemSubClass
    {
        [FieldOffset(0)]
        public int data;
        [FieldOffset(0)]
        public ItemSubClassConsumable Consumable;
        [FieldOffset(0)]
        public ItemSubClassContainer Container;
        [FieldOffset(0)]
        public ItemSubClassWeapon Weapon;
        [FieldOffset(0)]
        public ItemSubClassGem Gem;
        [FieldOffset(0)]
        public ItemSubClassArmor Armor;
        [FieldOffset(0)]
        public ItemSubClassReagent Reagent;
        [FieldOffset(0)]
        public ItemSubClassProjectile Projectile;
        [FieldOffset(0)]
        public ItemSubClassTradeGoods TradeGoods;
        [FieldOffset(0)]
        public ItemSubclassItemEnhancement Enhancement;
        [FieldOffset(0)]
        public ItemSubClassRecipe Recipe;
        [FieldOffset(0)]
        public ItemSubClassMoney Money;
        [FieldOffset(0)]
        public ItemSubClassQuiver Quiver;
        [FieldOffset(0)]
        public ItemSubClassQuest Quest;
        [FieldOffset(0)]
        public ItemSubClassKey Key;
        [FieldOffset(0)]
        public ItemSubClassPermanent Permanent;
        [FieldOffset(0)]
        public ItemSubClassJunk Junk;
        [FieldOffset(0)]
        public ItemSubClassGlyph Glyph;
        [FieldOffset(0)]
        public ItemSubclassBattlePet BattlePet;
        [FieldOffset(0)]
        public ItemSubclassWowToken WowToken;
        [FieldOffset(0)]
        public ItemSubclassProfession Profession;

        public ItemSubClass(int data = default)
        {
            this.data = data;
        }

        public static implicit operator ItemSubClass(int data) { return new ItemSubClass(data); }
        public static implicit operator int(ItemSubClass itemSubClass) { return itemSubClass.data; }
    }
}
