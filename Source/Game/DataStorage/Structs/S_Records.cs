// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.Miscellaneous;
using System;
using System.Threading.Tasks;
using static Game.AI.SmartAction;

namespace Game.DataStorage
{
    public sealed class ScenarioRecord
    {
        public int Id;
        public LocalizedString Name;
        public ushort AreaTableID;
        public byte Type;
        public byte Flags;
        public int UiTextureKitID;
    }

    public sealed class ScenarioStepRecord
    {
        public int Id;
        public LocalizedString Description;
        public LocalizedString Title;
        public ushort ScenarioID;
        public int CriteriaTreeId;
        public int RewardQuestID;
        /// <summary>
        /// Bonus step can only be completed if scenario is in the step specified in this field
        /// </summary>
        public int RelatedStep;
        /// <summary>
        /// Used in conjunction with Proving Grounds scenarios, when sequencing steps (Not using step order?)
        /// </summary>
        public ushort Supersedes;
        public byte OrderIndex;
        private byte _flags;
        public uint VisibilityPlayerConditionID;
        public ushort WidgetSetID;

        #region Properties
        public ScenarioStepFlags Flags => (ScenarioStepFlags) _flags;
        #endregion

        #region Helpers
        public bool HasFlag(ScenarioStepFlags flag)
        {
            return _flags.HasFlag((byte)flag);
        }

        public bool HasAnyFlag(ScenarioStepFlags flag)
        {
            return _flags.HasAnyFlag((byte)flag);
        }

        public bool IsBonusObjective => HasFlag(ScenarioStepFlags.BonusObjective);
        #endregion
    }

    public sealed class ScalingStatDistributionRecord
    {
        public int Id;
        public ushort PlayerLevelToItemLevelCurveID;
        public int MinLevel;
        public int MaxLevel;
        public int[] Bonus = new int[10];
        private int[] _statID = new int[10];

        #region Properties
        public ItemModType StatID(int index) => (ItemModType)_statID[index];
        #endregion
    };

    public sealed class ScalingStatValuesRecord
    {
        public int Id;
        public int Charlevel;
        public int WeaponDPS1H;
        public int WeaponDPS2H;
        public int SpellcasterDPS1H;
        public int SpellcasterDPS2H;
        public int RangedDPS;
        public int WandDPS;
        public int SpellPower;
        public int ShoulderBudget;
        public int TrinketBudget;
        public int WeaponBudget1H;
        public int PrimaryBudget;
        public int RangedBudget;
        public int TertiaryBudget;
        public int ClothShoulderArmor;
        public int LeatherShoulderArmor;
        public int MailShoulderArmor;
        public int PlateShoulderArmor;
        public int ClothCloakArmor;
        public int ClothChestArmor;
        public int LeatherChestArmor;
        public int MailChestArmor;
        public int PlateChestArmor;

        #region Helpers
        public int getSSDMultiplier(int mask)
        {
            if (mask.HasAnyFlag(0x4001F))
            {
                if (mask.HasAnyFlag(0x00000001)) return ShoulderBudget;
                if (mask.HasAnyFlag(0x00000002)) return TrinketBudget;
                if (mask.HasAnyFlag(0x00000004)) return WeaponBudget1H;
                if (mask.HasAnyFlag(0x00000008)) return PrimaryBudget;
                if (mask.HasAnyFlag(0x00000010)) return RangedBudget;
                if (mask.HasAnyFlag(0x00040000)) return TertiaryBudget;
            }
            return 0;
        }

        public int getArmorMod(int mask)
        {
            if (mask.HasAnyFlag(0x00F001E0))
            {
                if (mask.HasAnyFlag(0x00000020)) return ClothShoulderArmor;
                if (mask.HasAnyFlag(0x00000040)) return LeatherShoulderArmor;
                if (mask.HasAnyFlag(0x00000080)) return MailShoulderArmor;
                if (mask.HasAnyFlag(0x00000100)) return PlateShoulderArmor;

                if (mask.HasAnyFlag(0x00080000)) return ClothCloakArmor;
                if (mask.HasAnyFlag(0x00100000)) return ClothChestArmor;
                if (mask.HasAnyFlag(0x00200000)) return LeatherChestArmor;
                if (mask.HasAnyFlag(0x00400000)) return MailChestArmor;
                if (mask.HasAnyFlag(0x00800000)) return PlateChestArmor;
            }
            return 0;
        }

        public int getDPSMod(int mask)
        {
            if (mask.HasAnyFlag(0x7E00))
            {
                if (mask.HasAnyFlag(0x00000200)) return WeaponDPS1H;
                if (mask.HasAnyFlag(0x00000400)) return WeaponDPS2H;
                if (mask.HasAnyFlag(0x00000800)) return SpellcasterDPS1H;
                if (mask.HasAnyFlag(0x00001000)) return SpellcasterDPS2H;
                if (mask.HasAnyFlag(0x00002000)) return RangedDPS;
                if (mask.HasAnyFlag(0x00004000)) return WandDPS;
            }
            return 0;
        }

        public bool isTwoHand(uint mask)
        {
            int Mask = (int)mask;
            if (Mask.HasAnyFlag(0x7E00))
            {
                if (Mask.HasAnyFlag(0x00000400)) return true;
                if (Mask.HasAnyFlag(0x00001000)) return true;
            }
            return false;
        }

        public int getSpellBonus(int mask)
        {
            if (mask.HasAnyFlag(0x00008000)) return SpellPower;
            return 0;
        }
        #endregion
    }

    public sealed class SceneScriptRecord
    {
        public int Id;
        public ushort FirstSceneScriptID;
        public ushort NextSceneScriptID;
        public int Unknown915;
    }

    public sealed class SceneScriptGlobalTextRecord
    {
        public int Id;
        public string Name;
        public string Script;
    }

    public sealed class SceneScriptPackageRecord
    {
        public int Id;
        public string Name;
    }

    public sealed class SceneScriptTextRecord
    {
        public int Id;
        public string Name;
        public string Script;
    }

    public sealed class ServerMessagesRecord
    {
        public int Id;
        public LocalizedString Text;
    };

    public sealed class SkillLineRecord
    {
        public LocalizedString DisplayName;
        public LocalizedString AlternateVerb;
        public LocalizedString Description;
        public LocalizedString HordeDisplayName;
        public string OverrideSourceInfoDisplayName;
        private int _id;
        private sbyte _categoryID;
        public int SpellIconFileID;
        public sbyte CanLink;
        private int _parentSkillLineID;
        public int ParentTierIndex;
        private ushort _flags;
        public int SpellBookSpellID;

        #region Properties
        public SkillType Id => (SkillType)_id;
        public SkillCategory CategoryID => (SkillCategory)_categoryID;
        public SkillType ParentSkillLineID => (SkillType)_parentSkillLineID;
        public SkillLineFlags Flags => (SkillLineFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(SkillLineFlags flag)
        {
            return _flags.HasFlag((ushort)flag);
        }

        public bool HasAnyFlag(SkillLineFlags flag)
        {
            return _flags.HasAnyFlag((ushort)flag);
        }
        #endregion
    }

    public sealed class SkillLineAbilityRecord
    {
        private long _raceMask;
        public int Id;
        private ushort _skillLine;
        public int Spell;
        public short MinSkillLineRank;
        private int _classMask;
        public int SupercedesSpell;
        private sbyte _acquireMethod;
        public short TrivialSkillLineRankHigh;
        public short TrivialSkillLineRankLow;
        private sbyte _flags;
        public sbyte NumSkillUps;
        public short UniqueBit;
        public short TradeSkillCategoryID;
        private short _skillupSkillLineID;
        public int[] CharacterPoints = new int[2];

        #region Properties
        public RaceMask RaceMask => (RaceMask)_raceMask;
        public SkillType SkillLine => (SkillType)_skillLine;
        public ClassMask ClassMask => (ClassMask)_classMask;
        public AbilityLearnType AcquireMethod => (AbilityLearnType)_acquireMethod;
        public SkillLineAbilityFlags Flags => (SkillLineAbilityFlags)_flags;
        public SkillType SkillupSkillLineID => (SkillType)_skillupSkillLineID;
        #endregion

        #region Helpers
        public bool HasFlag(SkillRaceClassInfoFlags flag)
        {
            return _flags.HasFlag((sbyte)flag);
        }

        public bool HasAnyFlag(SkillRaceClassInfoFlags flag)
        {
            return _flags.HasAnyFlag((sbyte)flag);
        }
        #endregion
    }

    public sealed class SkillLineXTraitTreeRecord
    {
        public int Id;
        public int SkillLineID;
        public int TraitTreeID;
        public int OrderIndex;
    }

    public sealed class SkillRaceClassInfoRecord
    {
        public int Id;
        private long _raceMask;
        private short _skillID;
        private int _classMask;
        private ushort _flags;
        public sbyte Availability;
        public sbyte MinLevel;
        public short SkillTierID;

        #region Properties
        public SkillRaceClassInfoFlags Flags => (SkillRaceClassInfoFlags)_flags;
        public RaceMask RaceMask => (RaceMask)_raceMask;
        public ClassMask ClassMask => (ClassMask)_classMask;
        public SkillType SkillID => (SkillType)_skillID;
        #endregion

        #region Helpers
        public bool HasFlag(SkillRaceClassInfoFlags flag)
        {
            return _flags.HasFlag((ushort)flag);
        }

        public bool HasAnyFlag(SkillRaceClassInfoFlags flag)
        {
            return _flags.HasAnyFlag((ushort)flag);
        }
        #endregion
    }

    public sealed class SoundKitRecord
    {
        public int Id;
        public byte SoundType;
        public float VolumeFloat;
        public ushort Flags;
        public float MinDistance;
        public float DistanceCutoff;
        public byte EAXDef;
        public uint SoundKitAdvancedID;
        public float VolumeVariationPlus;
        public float VolumeVariationMinus;
        public float PitchVariationPlus;
        public float PitchVariationMinus;
        public sbyte DialogType;
        public float PitchAdjust;
        public ushort BusOverwriteID;
        public byte MaxInstances;
        public uint SoundMixGroupID;
    }

    public sealed class SpecializationSpellsRecord
    {
        public LocalizedString Description;
        public int Id;
        private ushort _specID;
        public int SpellID;
        public int OverridesSpellID;
        public byte DisplayOrder;

        #region Properties
        public ChrSpecialization SpecID => (ChrSpecialization)_specID;
        #endregion
    }

    public sealed class SpecSetMemberRecord
    {
        public int Id;
        private int _chrSpecializationID;
        public int SpecSetID;

        #region Properties
        public ChrSpecialization ChrSpecializationID => (ChrSpecialization)_chrSpecializationID;
        #endregion
    }

    public sealed class SpellAuraOptionsRecord
    {
        public int Id;
        private byte _difficultyID;
        public int CumulativeAura;
        public uint ProcCategoryRecovery;
        public byte ProcChance;
        public int ProcCharges;
        public ushort SpellProcsPerMinuteID;
        public int[] ProcTypeMask = new int[2];
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class SpellAuraRestrictionsRecord
    {
        public int Id;
        private byte _difficultyID;
        public byte CasterAuraState;
        public byte TargetAuraState;
        public byte ExcludeCasterAuraState;
        public byte ExcludeTargetAuraState;
        public int CasterAuraSpell;
        public int TargetAuraSpell;
        public int ExcludeCasterAuraSpell;
        public int ExcludeTargetAuraSpell;
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class SpellCastTimesRecord
    {
        public int Id;
        public int Base;
        public short PerLevel;
        public int Minimum;
    }

    public sealed class SpellCastingRequirementsRecord
    {
        public int Id;
        public int SpellID;
        public byte FacingCasterFlags;
        public ushort MinFactionID;
        public int MinReputation;
        public ushort RequiredAreasID;
        public byte RequiredAuraVision;
        public ushort RequiresSpellFocus;
    }

    public sealed class SpellCategoriesRecord
    {
        public int Id;
        private byte _difficultyID;
        private short _category;
        public sbyte DefenseType;
        public sbyte DispelType;
        public sbyte Mechanic;
        public sbyte PreventionType;
        public short StartRecoveryCategory;
        private short _chargeCategory;
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        public SpellCategories Category => (SpellCategories)_category;
        public SpellCategories ChargeCategory => (SpellCategories)_chargeCategory;
        #endregion
    }

    public sealed class SpellCategoryRecord
    {
        public int Id;
        public LocalizedString Name;
        private int _flags;
        public byte UsesPerWeek;
        public sbyte MaxCharges;
        public int ChargeRecoveryTime;
        public uint TypeMask;

        #region Properties
        public SpellCategoryFlags Flags => (SpellCategoryFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(SpellCategoryFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(SpellCategoryFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class SpellClassOptionsRecord
    {
        public int Id;
        public int SpellID;
        public int ModalNextSpell;
        private byte _spellClassSet;
        public FlagArray128 SpellClassMask;
        
        #region Properties
        public SpellFamilyNames SpellClassSet => (SpellFamilyNames)_spellClassSet;
        #endregion
    }

    public sealed class SpellCooldownsRecord
    {
        public int Id;
        private byte _difficultyID;
        public uint CategoryRecoveryTime;
        public uint RecoveryTime;
        public uint StartRecoveryTime;
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class SpellDurationRecord
    {
        public int Id;
        public int Duration;
        public uint DurationPerLevel;
        public int MaxDuration;
    }

    public sealed class SpellEffectRecord
    {
        public int Id;
        private int _difficultyID;
        public int EffectIndex;
        private int _effect;
        public float EffectAmplitude;
        private int _effectAttributes;
        private short _effectAura;
        public uint EffectAuraPeriod;
        public int EffectBasePoints;
        public float EffectBonusCoefficient;
        public float EffectChainAmplitude;
        public int EffectChainTargets;
        public int EffectDieSides;
        public int EffectItemType;
        public int EffectMechanic;
        public float EffectPointsPerResource;
        public float EffectPosFacing;
        public float EffectRealPointsPerLevel;
        public int EffectTriggerSpell;
        public float BonusCoefficientFromAP;
        public float PvpMultiplier;
        public float Coefficient;
        public float Variance;
        public float ResourceCoefficient;
        public float GroupSizeBasePointsCoefficient;
        public int[] EffectMiscValue = new int[2];
        public int[] EffectRadiusIndex = new int[2];
        public FlagArray128 EffectSpellClassMask;
        public short[] ImplicitTarget = new short[2];
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID { get => (Difficulty)_difficultyID; /*set => _difficultyID = (int)value; */}
        public SpellEffectName Effect { get => (SpellEffectName)_effect; set => _effect = (int)value; }
        public AuraType EffectAura { get => (AuraType)_effectAura; set => _effectAura = (short)value; }
        public SpellEffectAttributes EffectAttributes { get => (SpellEffectAttributes)_effectAttributes; set => _effectAttributes = (int)value; }
        #endregion
    }

    public sealed class SpellEquippedItemsRecord
    {
        public int Id;
        public int SpellID;
        public sbyte EquippedItemClass;
        public int EquippedItemInvTypes;
        public int EquippedItemSubclass;
    }

    public sealed class SpellFocusObjectRecord
    {
        public int Id;
        public LocalizedString Name;
    }

    public sealed class SpellInterruptsRecord
    {
        public static int MAX_SPELL_AURA_INTERRUPT_FLAGS = 2;

        public int Id;
        private byte _difficultyID;
        public short InterruptFlags;
        public int[] AuraInterruptFlags = new int[MAX_SPELL_AURA_INTERRUPT_FLAGS];
        public int[] ChannelInterruptFlags = new int[MAX_SPELL_AURA_INTERRUPT_FLAGS];
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class SpellItemEnchantmentRecord
    {
        public int Id;
        public LocalizedString Name;
        public LocalizedString HordeName;
        public int[] EffectArg = new int[ItemConst.MaxItemEnchantmentEffects];
        public float[] EffectScalingPoints = new float[ItemConst.MaxItemEnchantmentEffects];
        public int GemItemID;
        public int TransmogUseConditionID;
        public uint TransmogCost;
        public uint IconFileDataID;
        public short[] EffectPointsMin = new short[ItemConst.MaxItemEnchantmentEffects];
        public ushort ItemVisual;
        private ushort _flags;
        private ushort _requiredSkillID;
        public ushort RequiredSkillRank;
        public ushort ItemLevel;
        public byte Charges;
        private byte[] _effect = new byte[ItemConst.MaxItemEnchantmentEffects];
        private sbyte _scalingClass;
        private sbyte _scalingClassRestricted;
        public byte ConditionID;
        public byte MinLevel;
        public byte MaxLevel;


        #region Properties
        public SpellItemEnchantmentFlags Flags => (SpellItemEnchantmentFlags)_flags;
        public SkillType RequiredSkillID => (SkillType)_requiredSkillID;
        public ItemEnchantmentType Effect(int index) => (ItemEnchantmentType)_effect[index];
        public ScalingClass ScalingClass => (ScalingClass)_scalingClass;
        public ScalingClass ScalingClassRestricted => (ScalingClass)_scalingClassRestricted;        
        #endregion

        #region Helpers
        public bool HasFlag(SpellItemEnchantmentFlags flag)
        {
            return _flags.HasFlag((ushort)flag);
        }

        public bool HasAnyFlag(SpellItemEnchantmentFlags flag)
        {
            return _flags.HasAnyFlag((ushort)flag);
        }
        #endregion
    }

    public sealed class SpellItemEnchantmentConditionRecord
    {
        public int Id;
        public byte[] LtOperandType = new byte[5];
        public uint[] LtOperand = new uint[5];
        public byte[] Operator = new byte[5];
        public byte[] RtOperandType = new byte[5];
        public byte[] RtOperand = new byte[5];
        public byte[] Logic = new byte[5];
    }

    public sealed class SpellKeyboundOverrideRecord
    {
        public int Id;
        public string Function;
        public sbyte Type;
        public int Data;
        public int Flags;
    }

    public sealed class SpellLabelRecord
    {
        public int Id;
        public int LabelID;
        public int SpellID;
    }

    public sealed class SpellLearnSpellRecord
    {
        public int Id;
        public int SpellID;
        public int LearnSpellID;
        public int OverridesSpellID;
    }

    public sealed class SpellLevelsRecord
    {
        public int Id;
        private byte _difficultyID;
        public short BaseLevel;
        public short MaxLevel;
        public short SpellLevel;
        public byte MaxPassiveAuraLevel;        
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class SpellMiscRecord
    {
        public int Id;
        public int[] Attributes = new int[15];
        private byte _difficultyID;
        public ushort CastingTimeIndex;
        public ushort DurationIndex;
        public ushort RangeIndex;
        public byte SchoolMask;
        public float Speed;
        public float LaunchDelay;
        public float MinDuration;
        public int SpellIconFileDataID;
        public int ActiveIconFileDataID;
        public int ContentTuningID;
        public int ShowFutureSpellPlayerConditionID;        
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class SpellNameRecord
    {
        /// <summary>
        /// SpellID
        /// </summary>
        public int Id;
        public LocalizedString Name;
    }

    public sealed class SpellPowerRecord
    {
        public int Id;
        public byte OrderIndex;
        public int ManaCost;
        public int ManaCostPerLevel;
        public int ManaPerSecond;
        public int PowerDisplayID;
        public int AltPowerBarID;
        public float PowerCostPct;
        public float PowerCostMaxPct;
        public float PowerPctPerSecond;
        private sbyte _powerType;
        public int RequiredAuraSpellID;
        /// <summary>
        /// Spell uses [ManaCost, ManaCost+ManaCostAdditional] power<br/>
        /// - affects tooltip parsing as multiplier on SpellEffectEntry::EffectPointsPerResource
        /// </summary>
        public uint OptionalCost;
        public int SpellID;

        #region Properties
        public PowerType PowerType => (PowerType)_powerType;
        #endregion
    }

    public sealed class SpellPowerDifficultyRecord
    {
        public int Id;
        private byte _difficultyID;
        public byte OrderIndex;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class SpellProcsPerMinuteRecord
    {
        public int Id;
        public float BaseProcRate;
        public byte Flags;
    }

    public sealed class SpellProcsPerMinuteModRecord
    {
        public int Id;
        private byte _type;
        public short Param;
        public float Coeff;
        public int SpellProcsPerMinuteID;

        #region Properties
        public SpellProcsPerMinuteModType Type => (SpellProcsPerMinuteModType)_type;
        #endregion
    }

    public sealed class SpellRadiusRecord
    {
        public int Id;
        public float Radius;
        public float RadiusPerLevel;
        public float RadiusMin;
        public float RadiusMax;
    }

    public sealed class SpellRangeRecord
    {
        public int Id;
        public LocalizedString DisplayName;
        public LocalizedString DisplayNameShort;
        private byte _flags;
        public float[] RangeMin = new float[2];
        public float[] RangeMax = new float[2];

        #region Properties
        public SpellRangeFlag Flags => (SpellRangeFlag)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(SpellRangeFlag flag)
        {
            return _flags.HasFlag((byte)flag);
        }

        public bool HasAnyFlag(SpellRangeFlag flag)
        {
            return _flags.HasAnyFlag((byte)flag);
        }
        #endregion      
    }

    public sealed class SpellReagentsRecord
    {
        public int Id;
        public int SpellID;
        public int[] Reagent = new int[SpellConst.MaxReagents];
        public ushort[] ReagentCount = new ushort[SpellConst.MaxReagents];
    }

    public sealed class SpellReagentsCurrencyRecord
    {
        public int Id;
        public int SpellID;
        public ushort CurrencyTypesID;
        public ushort CurrencyCount;
    }

    public sealed class SpellScalingRecord
    {
        public int Id;
        public int SpellID;
        public int Class;
        public int MinScalingLevel;
        public int MaxScalingLevel;
        public short ScalesFromItemLevel;
    }

    public sealed class SpellShapeshiftRecord
    {
        public int Id;
        public int SpellID;
        public sbyte StanceBarOrder;
        public int[] ShapeshiftExclude = new int[2];
        public int[] ShapeshiftMask = new int[2];
    }

    public sealed class SpellShapeshiftFormRecord
    {
        public int Id;
        public LocalizedString Name;
        public sbyte CreatureType;
        private int _flags;
        public int AttackIconFileID;
        public sbyte BonusActionBar;
        public ushort CombatRoundTime;
        public float DamageVariance;
        public ushort MountTypeID;
        public int[] CreatureDisplayID = new int[4];
        public int[] PresetSpellID = new int[SpellConst.MaxShapeshift];

        #region Properties
        public SpellShapeshiftFormFlags Flags => (SpellShapeshiftFormFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(SpellShapeshiftFormFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(SpellShapeshiftFormFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion      
    }

    public sealed class SpellTargetRestrictionsRecord
    {
        public int Id;
        private byte _difficultyID;
        public float ConeDegrees;
        public byte MaxTargets;
        public int MaxTargetLevel;
        public short TargetCreatureType;
        public int Targets;
        public float Width;
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class SpellTotemsRecord
    {
        public int Id;
        public int SpellID;
        public ushort[] RequiredTotemCategoryID = new ushort[SpellConst.MaxTotems];
        public int[] Totem = new int[SpellConst.MaxTotems];
    }

    public sealed class SpellVisualRecord
    {
        public int Id;
        public float[] MissileCastOffset = new float[3];
        public float[] MissileImpactOffset = new float[3];
        public uint AnimEventSoundID;
        public int Flags;
        public sbyte MissileAttachment;
        public sbyte MissileDestinationAttachment;
        public uint MissileCastPositionerID;
        public uint MissileImpactPositionerID;
        public int MissileTargetingKit;
        public uint HostileSpellVisualID;
        public uint CasterSpellVisualID;
        public ushort SpellVisualMissileSetID;
        public ushort DamageNumberDelay;
        public uint LowViolenceSpellVisualID;
        public uint RaidSpellVisualMissileSetID;
        public int ReducedUnexpectedCameraMovementSpellVisualID;
        public ushort AreaModel;
        public sbyte HasMissile;
    }

    public sealed class SpellVisualEffectNameRecord
    {
        public int Id;
        public int ModelFileDataID;
        public float BaseMissileSpeed;
        public float Scale;
        public float MinAllowedScale;
        public float MaxAllowedScale;
        public float Alpha;
        public uint Flags;
        public int TextureFileDataID;
        public float EffectRadius;
        public uint Type;
        public int GenericID;
        public uint RibbonQualityID;
        public int DissolveEffectID;
        public int ModelPosition;
    }

    public sealed class SpellVisualMissileRecord
    {
        public float[] CastOffset = new float[3];
        public float[] ImpactOffset = new float[3];
        public int Id;
        public ushort SpellVisualEffectNameID;
        public uint SoundEntriesID;
        public sbyte Attachment;
        public sbyte DestinationAttachment;
        public ushort CastPositionerID;
        public ushort ImpactPositionerID;
        public int FollowGroundHeight;
        public uint FollowGroundDropSpeed;
        public ushort FollowGroundApproach;
        public uint Flags;
        public ushort SpellMissileMotionID;
        public int AnimKitID;
        public int SpellVisualMissileSetID;
    }

    public sealed class SpellVisualKitRecord
    {
        public int Id;
        public int FallbackSpellVisualKitId;
        public ushort DelayMin;
        public ushort DelayMax;
        public float FallbackPriority;
        public int[] Flags = new int[2];
    }   

    public sealed class SpellXSpellVisualRecord
    {
        public int Id;
        private byte _difficultyID;
        public int SpellVisualID;
        public float Probability;
        public byte Flags;
        public int Priority;
        public int SpellIconFileID;
        public int ActiveIconFileID;
        public ushort ViewerUnitConditionID;
        public uint ViewerPlayerConditionID;
        public ushort CasterUnitConditionID;
        public int CasterPlayerConditionID;
        public int SpellID;

        #region Properties
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class SummonPropertiesRecord
    {
        public int Id;
        private int _control;
        public int Faction;
        private int _title;
        public int Slot;
        private int[] _flags = new int[2];

        #region Properties
        public SummonCategory Control { get => (SummonCategory)_control; set => _control = (int)value; }
        public SummonTitle Title { get => (SummonTitle)_title; set => _title = (int)value; }
        public SummonPropertiesFlags Flags => (SummonPropertiesFlags)_flags[0];
        #endregion

        #region Helpers
        public bool HasFlag(SummonPropertiesFlags flag)
        {
            return _flags[0].HasFlag((int)flag);
        }

        public bool HasAnyFlag(SummonPropertiesFlags flag)
        {
            return _flags[0].HasAnyFlag((int)flag);
        }
        #endregion        
    }
}
