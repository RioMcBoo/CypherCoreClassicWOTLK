// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Miscellaneous;
using System;
using System.Security.Cryptography.X509Certificates;
using static Game.AI.SmartAction;

namespace Game.DataStorage
{
    public sealed class ImportPriceArmorRecord
    {
        public uint Id;
        public float ClothModifier;
        public float LeatherModifier;
        public float ChainModifier;
        public float PlateModifier;
    }

    public sealed class ImportPriceQualityRecord
    {
        public uint Id;
        public float Data;
    }

    public sealed class ImportPriceShieldRecord
    {
        public uint Id;
        public float Data;
    }

    public sealed class ImportPriceWeaponRecord
    {
        public uint Id;
        public float Data;
    }

    public sealed class ItemRecord
    {
        public int Id;
        private byte _classID;
        private byte _subclassID;
        public byte Material;
        private sbyte _inventoryType;
        public int RequiredLevel;
        public byte SheatheType;
        public ushort RandomSelect;
        public ushort ItemRandomSuffixGroupID;
        public sbyte SoundOverrideSubclassID;
        public ushort ScalingStatDistributionID;
        public int IconFileDataID;
        public byte ItemGroupSoundsID;
        public int ContentTuningID;
        public uint MaxDurability;
        public byte AmmunitionType;
        public int ScalingStatValue;
        public byte[] DamageType = new byte[5];
        public short[] Resistances = new short[7];
        public ushort[] MinDamage = new ushort[5];
        public ushort[] MaxDamage = new ushort[5];

        #region Properties
        public ItemClass ClassID => (ItemClass)_classID;
        public ItemSubClass SubclassID => new(_subclassID);
        public InventoryType InventoryType => (InventoryType)_inventoryType;
        #endregion        
    }

    public sealed class ItemAppearanceRecord
    {
        public uint Id;
        public byte DisplayType;
        public int ItemDisplayInfoID;
        public int DefaultIconFileDataID;
        public int UiOrder;
    }

    public sealed class ItemArmorQualityRecord
    {
        public uint Id;
        public float[] QualityMod = new float[7];
    }

    public sealed class ItemArmorShieldRecord
    {
        public uint Id;
        public float[] Quality = new float[7];
        public ushort ItemLevel;
    }

    public sealed class ItemArmorTotalRecord
    {
        public uint Id;
        public short ItemLevel;
        public float Cloth;
        public float Leather;
        public float Mail;
        public float Plate;
    }

    public sealed class ItemBagFamilyRecord
    {
        public int Id;
        public LocalizedString Name;
    }

    public sealed class ItemBonusRecord
    {
        public int Id;
        public int[] Value = new int[4];
        public ushort ParentItemBonusListID;
        public byte _type;
        public byte OrderIndex;

        #region Properties
        public ItemBonusType Type => (ItemBonusType)_type;
        #endregion
    }

    public sealed class ItemBonusListLevelDeltaRecord
    {
        public short ItemLevelDelta;
        public int Id;
    }

    public sealed class ItemBonusTreeNodeRecord
    {
        public int Id;
        public byte ItemContext;
        public ushort ChildItemBonusTreeID;
        public ushort ChildItemBonusListID;
        public ushort ChildItemLevelSelectorID;
        public int ParentItemBonusTreeID;
    }

    public sealed class ItemChildEquipmentRecord
    {
        public int Id;        
        public int ChildItemID;
        public byte ChildItemEquipSlot;
        public int ParentItemID;
    }

    public sealed class ItemClassRecord
    {
        public int Id;
        public LocalizedString ClassName;
        public sbyte ClassID;
        public float PriceModifier;
        public byte Flags;
    }

    public sealed class ItemContextPickerEntryRecord
    {
        public int Id;
        public byte ItemCreationContext;
        public byte OrderIndex;
        public int PVal;
        public int LabelID;
        public uint Flags;
        public int PlayerConditionID;
        public int ItemContextPickerID;
    }

    public sealed class ItemCurrencyCostRecord
    {
        public int Id;
        public int ItemID;
    }

    /// <summary>
    /// common struct for:<br/>
    /// ItemDamageAmmo.dbc<br/>
    /// ItemDamageOneHand.dbc<br/>
    /// ItemDamageOneHandCaster.dbc<br/>
    /// ItemDamageRanged.dbc<br/>
    /// ItemDamageThrown.dbc<br/>
    /// ItemDamageTwoHand.dbc<br/>
    /// ItemDamageTwoHandCaster.dbc<br/>
    /// ItemDamageWand.dbc
    /// </summary>
    public sealed class ItemDamageRecord
    {
        public int Id;
        public ushort ItemLevel;
        public float[] Quality = new float[7];
    }

    public sealed class ItemDisenchantLootRecord
    {
        public int Id;
        private sbyte _subclass;
        private byte _quality;
        public ushort MinLevel;
        public ushort MaxLevel;
        public ushort SkillRequired;
        private sbyte _expansionID;
        private uint _class;

        #region Properties
        public ItemSubClass Subclass => (ItemSubClass)_subclass;
        public ItemQuality Quality => (ItemQuality)_quality;
        public Expansion ExpansionID => (Expansion)_expansionID;
        public ItemClass Class => (ItemClass)_class;
        #endregion
    }

    public sealed class ItemEffectRecord
    {
        public int Id;
        public byte LegacySlotIndex;
        private sbyte _triggerType;
        public short Charges;
        public int CoolDownMSec;
        public int CategoryCoolDownMSec;
        private ushort _spellCategoryID;
        public int SpellID;
        public ushort ChrSpecializationID;
        public int ParentItemID;

        #region Properties
        public ItemSpelltriggerType TriggerType => (ItemSpelltriggerType)_triggerType;
        public SpellCategories SpellCategoryID => (SpellCategories)_spellCategoryID;
        #endregion
    }

    public sealed class ItemExtendedCostRecord
    {
        public int Id;
        public ushort RequiredArenaRating;
        /// <summary>
        /// arena slot restrictions (min slot value)
        /// </summary>
        public byte ArenaBracket;
        public byte Flags;
        public byte MinFactionID;
        private int _minReputation;
        /// <summary>
        /// required personal arena rating
        /// </summary>
        public byte RequiredAchievement;
        /// <summary>
        /// required item id
        /// </summary>
        public int[] ItemID = new int[ItemConst.MaxItemExtCostItems];
        /// <summary>
        /// required count of 1st item
        /// </summary>
        public ushort[] ItemCount = new ushort[ItemConst.MaxItemExtCostItems];
        /// <summary>
        /// required curency id
        /// </summary>
        public ushort[] CurrencyID = new ushort[ItemConst.MaxItemExtCostCurrencies];
        /// <summary>
        /// required curency count
        /// </summary>
        public int[] CurrencyCount = new int[ItemConst.MaxItemExtCostCurrencies];

        #region Properties
        public ReputationRank MinReputation => (ReputationRank)_minReputation;
        #endregion
    }

    public sealed class ItemLevelSelectorRecord
    {
        public uint Id;
        public ushort MinItemLevel;
        public ushort ItemLevelSelectorQualitySetID;
    }

    public sealed class ItemLevelSelectorQualityRecord : IEquatable<ItemLevelSelectorQualityRecord>, IEquatable<ItemQuality>
    {
        public int Id;
        public int QualityItemBonusListID;
        private sbyte _quality;
        public int ParentILSQualitySetID;

        #region Properties
        public ItemQuality Quality => (ItemQuality)_quality;
        #endregion

        #region Helpers
        public bool Equals(ItemLevelSelectorQualityRecord other) { return Quality < other.Quality; }
        public bool Equals(ItemQuality quality) { return Quality < quality; }
        #endregion       
    }

    public sealed class ItemLevelSelectorQualitySetRecord
    {
        public int Id;
        public short IlvlRare;
        public short IlvlEpic;
    }

    public sealed class ItemLimitCategoryRecord
    {
        public int Id;
        public LocalizedString Name;
        public byte Quantity;
        public byte Flags;
    }

    public sealed class ItemLimitCategoryConditionRecord
    {
        public int Id;
        public sbyte AddQuantity;
        public int PlayerConditionID;
        public int ParentItemLimitCategoryID;
    }

    public sealed class ItemModifiedAppearanceRecord
    {
        public int Id;
        public int ItemID;
        public int ItemAppearanceModifierID;
        public int ItemAppearanceID;
        public int OrderIndex;
        public int TransmogSourceTypeEnum;
    }

    public sealed class ItemModifiedAppearanceExtraRecord
    {
        public int Id;
        public int IconFileDataID;
        public int UnequippedIconFileDataID;
        public byte SheatheType;
        public sbyte DisplayWeaponSubclassID;
        public sbyte DisplayInventoryType;
    }

    public sealed class ItemNameDescriptionRecord
    {
        public int Id;
        public LocalizedString Description;
        public int Color;
    }

    public sealed class ItemPriceBaseRecord
    {
        public int Id;
        public ushort ItemLevel;
        public float Armor;
        public float Weapon;
    }

    public sealed class ItemRandomPropertiesRecord
    {
        public int Id;
        public LocalizedString Name;
        public ushort[] Enchantment = new ushort[ItemConst.MaxItemRandomProperties];
    };

    public sealed class ItemRandomSuffixRecord
    {
        public int Id;
        public LocalizedString Name;
        public ushort[] Enchantment = new ushort[ItemConst.MaxItemRandomProperties];
        public ushort[] AllocationPct = new ushort[ItemConst.MaxItemRandomProperties];
    };

    public sealed class ItemSearchNameRecord
    {
        private long _raceMask;
        public LocalizedString Display;
        public uint Id;
        public byte OverallQualityID;
        public sbyte ExpansionID;
        public ushort MinFactionID;
        public int MinReputation;
        public int AllowableClass;
        public sbyte RequiredLevel;
        public ushort RequiredSkill;
        public ushort RequiredSkillRank;
        public uint RequiredAbility;
        public ushort ItemLevel;
        public int[] Flags = new int[ItemConst.MaxItemProtoFlags];

        #region Properties
        public RaceMask RaceMask => (RaceMask)_raceMask;
        #endregion
    };

    public sealed class ItemSetRecord
    {
        public uint Id;
        public LocalizedString Name;
        private uint _setFlags;
        public uint RequiredSkill;
        public ushort RequiredSkillRank;
        public uint[] ItemID = new uint[ItemConst.MaxItemSetItems];

        #region Properties
        public ItemSetFlags SetFlags => (ItemSetFlags)_setFlags;
        #endregion

        #region Helpers
        public bool HasFlag(ItemSetFlags flag)
        {
            return _setFlags.HasFlag((uint)flag);
        }

        public bool HasAnyFlag(ItemSetFlags flag)
        {
            return _setFlags.HasAnyFlag((uint)flag);
        }
        #endregion
    }

    public sealed class ItemSetSpellRecord
    {
        public int Id;
        public ushort ChrSpecID;
        public int SpellID;
        public byte Threshold;
        public int ItemSetID;
    }

    public sealed class ItemSparseRecord
    {
        public int Id;
        private long _allowableRace;
        public LocalizedString Description;
        public LocalizedString Display3;
        public LocalizedString Display2;
        public LocalizedString Display1;
        public LocalizedString Display;
        public float DmgVariance;
        public uint DurationInInventory;
        public float QualityModifier;
        public uint BagFamily;
        public int StartQuestID;
        public float ItemRange;
        public float[] StatPercentageOfSocket = new float[ItemConst.MaxStats];
        public int[] StatPercentEditor = new int[ItemConst.MaxStats];
        public int Stackable;
        public int MaxCount;
        private int _minReputation;
        public int RequiredAbility;
        public uint SellPrice;
        public uint BuyPrice;
        public int VendorStackCount;
        public float PriceVariance;
        public float PriceRandomValue;
        public int[] Flags = new int[ItemConst.MaxItemProtoFlags];
        public int FactionRelated;
        public int ModifiedCraftingReagentItemID;
        public int ContentTuningID;
        public int PlayerLevelToItemLevelCurveID;
        public uint MaxDurability;
        public ushort ItemNameDescriptionID;
        public ushort RequiredTransmogHoliday;
        public ushort RequiredHoliday;
        public ushort LimitCategory;
        public ushort GemProperties;
        public ushort SocketMatchEnchantmentId;
        public ushort TotemCategoryID;
        public ushort InstanceBound;
        public ushort[] ZoneBound = new ushort[ItemConst.MaxItemProtoZones];
        public ushort ItemSet;
        public ushort LockID;
        public ushort PageID;
        public ushort ItemDelay;
        public ushort MinFactionID;
        public ushort RequiredSkillRank;
        public ushort RequiredSkill;
        public ushort ItemLevel;
        private short _allowableClass;
        public ushort ItemRandomSuffixGroupID;
        public ushort RandomSelect;
        public ushort[] MinDamage = new ushort[5];
        public ushort[] MaxDamage = new ushort[5];
        public short[] Resistances = new short[7];
        public ushort ScalingStatDistributionID;
        public short[] StatModifierBonusAmount = new short[ItemConst.MaxStats];
        private byte _expansionID;
        public byte ArtifactID;
        public byte SpellWeight;
        public byte SpellWeightCategory;
        public byte[] SocketType = new byte[ItemConst.MaxGemSockets];
        public byte SheatheType;
        public byte Material;
        public byte PageMaterialID;
        public byte LanguageID;
        public byte Bonding;
        private byte _damageType;
        private sbyte[] _statModifierBonusStat = new sbyte[ItemConst.MaxStats];
        public byte ContainerSlots;
        public byte RequiredPVPMedal;
        public byte RequiredPVPRank;
        private sbyte _inventoryType;
        private sbyte _overallQualityID;
        public byte AmmunitionType;
        public sbyte RequiredLevel;

        #region Properties
        public RaceMask AllowableRace => (RaceMask)_allowableRace;
        public ClassMask AllowableClass => (ClassMask)_allowableClass;
        public SpellSchools DamageType => (SpellSchools)_damageType;
        public ItemModType StatModifierBonusStat(int itemStatSlot) => (ItemModType)_statModifierBonusStat[itemStatSlot];
        public InventoryType InventoryType => (InventoryType)_inventoryType;
        public ItemQuality OverallQualityID => (ItemQuality)_overallQualityID;
        public Expansion ExpansionID => (Expansion)_expansionID;
        public ReputationRank MinReputation => (ReputationRank)_minReputation;
        #endregion
    }

    public sealed class ItemSpecRecord
    {
        public uint Id;
        public byte MinLevel;
        public byte MaxLevel;
        public byte ItemType;
        private byte _primaryStat;
        private byte _secondaryStat;
        public ushort SpecializationID;

        #region Properties
        public ItemSpecStat PrimaryStat => (ItemSpecStat)_primaryStat;
        public ItemSpecStat SecondaryStat => (ItemSpecStat)_secondaryStat;
        #endregion
    }

    public sealed class ItemSpecOverrideRecord
    {
        public int Id;
        public ushort SpecID;
        public int ItemID;
    }

    public sealed class ItemXBonusTreeRecord
    {
        public int Id;
        public ushort ItemBonusTreeID;
        public int ItemID;
    }
}
