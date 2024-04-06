// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Numerics;

namespace Game.DataStorage
{
    public sealed class Cfg_CategoriesRecord
    {
        public int Id;
        public LocalizedString Name;
        public ushort LocaleMask;
        private byte _createCharsetMask;
        private byte _existingCharsetMask;
        private byte _flags;
        public sbyte Order;

        #region Properties
        public CfgCategoriesCharsets CreateCharsetMask => (CfgCategoriesCharsets)_createCharsetMask;
        public CfgCategoriesCharsets ExistingCharsetMask => (CfgCategoriesCharsets)_existingCharsetMask;
        public CfgCategoriesFlags Flags => (CfgCategoriesFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(CfgCategoriesFlags flag)
        {
            return _flags.HasFlag((byte)flag);
        }

        public bool HasAnyFlag(CfgCategoriesFlags flag)
        {
            return _flags.HasAnyFlag((byte)flag);
        }
        #endregion
    }

    public sealed class Cfg_RegionsRecord
    {
        public int Id;
        public string Tag;
        public ushort RegionID;
        /// <summary>
        /// Date of first raid reset, all other resets are calculated as this date plus interval
        /// </summary>
        public uint Raidorigin;
        public byte RegionGroupMask;
        public uint ChallengeOrigin;
    }

    public sealed class CharTitlesRecord
    {
        public int Id;
        public LocalizedString Name;
        public LocalizedString Name1;
        public ushort MaskID;
        public sbyte Flags;
    }

    public sealed class CharacterLoadoutRecord
    {
        private long _raceMask;
        public int Id;
        private sbyte _chrClassID;
        public int Purpose;
        private sbyte _itemContext;

        #region Properties
        public RaceMask RaceMask => (RaceMask)_raceMask;
        public Class ChrClassID => (Class)_chrClassID;
        public ItemContext ItemContext => (ItemContext)_itemContext;
        #endregion

        #region Helpers
        public bool IsForNewCharacter => Purpose == 9;
        #endregion
    }

    public sealed class CharacterLoadoutItemRecord
    {
        public int Id;
        public ushort CharacterLoadoutID;
        public int ItemID;
    }

    public sealed class ChatChannelsRecord
    {        
        public LocalizedString Name;
        public LocalizedString Shortcut;
        public int Id;
        private int _flags;
        public sbyte FactionGroup;
        private int _ruleset;

        #region Properties
        public ChatChannelFlags Flags => (ChatChannelFlags)_flags;
        public ChatChannelRuleset Ruleset => (ChatChannelRuleset)_ruleset;
        #endregion

        #region Helpers
        public bool HasFlag(ChatChannelFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(ChatChannelFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class ChrClassUIDisplayRecord
    {
        public int Id;
        private byte _chrClassesID;
        public int AdvGuidePlayerConditionID;
        public int SplashPlayerConditionID;

        #region Properties
        public Class ChrClassesID => (Class)_chrClassesID;
        #endregion
    }

    public sealed class ChrClassesRecord
    {
        public LocalizedString Name;
        public string Filename;
        public LocalizedString NameMale;
        public LocalizedString NameFemale;
        public string PetNameToken;
        public int Id;
        public int CreateScreenFileDataID;
        public int SelectScreenFileDataID;
        public int IconFileDataID;
        public int LowResScreenFileDataID;
        public int Flags;
        public int StartingLevel;
        public uint ArmorTypeMask;
        public ushort CinematicSequenceID;
        public ushort DefaultSpec;
        public byte HasStrengthAttackBonus;
        public byte PrimaryStatPriority;
        private byte _displayPower;
        public byte RangedAttackPowerPerAgility;
        public byte AttackPowerPerAgility;
        public byte AttackPowerPerStrength;
        public byte SpellClassSet;
        public byte RolesMask;
        public byte DamageBonusStat;
        public byte HasRelicSlot;

        #region Properties
        public PowerType DisplayPower => (PowerType)_displayPower;
        #endregion

        #region Helpers
        public bool HasFlag(int flag)
        {
            return Flags.HasFlag(flag);
        }

        public bool HasAnyFlag(int flag)
        {
            return Flags.HasAnyFlag(flag);
        }
        #endregion
    }

    public sealed class ChrClassesXPowerTypesRecord
    {
        public int Id;
        public sbyte _powerType;
        public uint ClassID;

        #region Properties
        public PowerType PowerType => (PowerType)_powerType;
        #endregion
    }

    public sealed class ChrCustomizationChoiceRecord
    {
        public LocalizedString Name;
        public int Id;
        public int ChrCustomizationOptionID;
        public int ChrCustomizationReqID;
        public int ChrCustomizationVisReqID;
        public ushort SortOrder;
        public ushort UiOrderIndex;
        public int Flags;
        public int AddedInPatch;
        public int SoundKitID;
        public int[] SwatchColor = new int[2];
    }

    public sealed class ChrCustomizationDisplayInfoRecord
    {
        public int Id;
        private int _shapeshiftFormID;
        public int DisplayID;
        public float BarberShopMinCameraDistance;
        public float BarberShopHeightOffset;

        #region Properties
        public ShapeShiftForm ShapeshiftFormID => (ShapeShiftForm)_shapeshiftFormID;
        #endregion
    }

    public sealed class ChrCustomizationElementRecord
    {
        public int Id;
        public int ChrCustomizationChoiceID;
        public int RelatedChrCustomizationChoiceID;
        public int ChrCustomizationGeosetID;
        public int ChrCustomizationSkinnedModelID;
        public int ChrCustomizationMaterialID;
        public int ChrCustomizationBoneSetID;
        public int ChrCustomizationCondModelID;
        public int ChrCustomizationDisplayInfoID;
        public int ChrCustItemGeoModifyID;
        public int ChrCustomizationVoiceID;
        public int AnimKitID;
        public int ParticleColorID;
    }

    public sealed class ChrCustomizationOptionRecord
    {
        public LocalizedString Name;
        public int Id;
        public ushort SecondaryID;
        public int Flags;
        public int ChrModelID;
        public int SortIndex;
        public int ChrCustomizationCategoryID;
        public int OptionType;
        public float BarberShopCostModifier;
        public int ChrCustomizationID;
        public int ChrCustomizationReqID;
        public int UiOrderIndex;
    }

    public sealed class ChrCustomizationReqRecord
    {
        private long _raceMask;
        public LocalizedString ReqSource;
        public int Id;
        private int _flags;
        private int _classMask;
        public int AchievementID;
        public int QuestID;
        /// <summary>
        /// -1: allow any, otherwise must match OverrideArchive cvar
        /// </summary>
        public int OverrideArchive;
        public int ItemModifiedAppearanceID;

        #region Properties
        public RaceMask RaceMask => (RaceMask)_raceMask;
        public ClassMask ClassMask => (ClassMask)_classMask;
        public ChrCustomizationReqFlag Flags => (ChrCustomizationReqFlag)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(ChrCustomizationReqFlag flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(ChrCustomizationReqFlag flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class ChrCustomizationReqChoiceRecord
    {
        public int Id;
        public int ChrCustomizationChoiceID;
        public int ChrCustomizationReqID;
    }

    public sealed class ChrModelRecord
    {
        public float[] FaceCustomizationOffset = new float[3];
        public float[] CustomizeOffset = new float[3];
        public int Id;
        public sbyte Sex;
        public int DisplayID;
        public int CharComponentTextureLayoutID;
        public int Flags;
        public int SkeletonFileDataID;
        public int ModelFallbackChrModelID;
        public int TextureFallbackChrModelID;
        public int HelmVisFallbackChrModelID;
        public float CustomizeScale;
        public float CustomizeFacing;
        public float CameraDistanceOffset;
        public float BarberShopCameraOffsetScale;
        /// <summary>
        /// applied after BarberShopCameraOffsetScale
        /// </summary>
        public float BarberShopCameraHeightOffsetScale;
        public float BarberShopCameraRotationOffset;
    }

    public sealed class ChrRaceXChrModelRecord
    {
        public int Id;
        private int _chrRacesID;
        public int ChrModelID;
        private int _sex;
        public int AllowedTransmogSlots;

        #region Properties
        public Race ChrRacesID => (Race)_chrRacesID;
        public Gender Sex => (Gender)_sex;
        #endregion
    }

    public sealed class ChrRacesRecord
    {
        private int _id;
        public string ClientPrefix;
        public string ClientFileString;
        public LocalizedString Name;
        public LocalizedString NameFemale;
        public LocalizedString NameLowercase;
        public LocalizedString NameFemaleLowercase;
        public LocalizedString LoreName;
        public LocalizedString LoreNameFemale;
        public LocalizedString LoreNameLower;
        public LocalizedString LoreNameLowerFemale;
        public LocalizedString LoreDescription;
        public LocalizedString ShortName;
        public LocalizedString ShortNameFemale;
        public LocalizedString ShortNameLower;
        public LocalizedString ShortNameLowerFemale;
        private int _flags;
        public uint MaleDisplayID;
        public uint FemaleDisplayID;
        public uint HighResMaleDisplayID;
        public uint HighResFemaleDisplayID;
        public int ResSicknessSpellID;
        public int SplashSoundID;
        public int CreateScreenFileDataID;
        public int SelectScreenFileDataID;
        public int LowResScreenFileDataID;
        public uint[] AlteredFormStartVisualKitID = new uint[3];
        public uint[] AlteredFormFinishVisualKitID = new uint[3];
        public int HeritageArmorAchievementID;
        public int StartingLevel;
        public int UiDisplayOrder;
        public int PlayableRaceBit;
        public int FemaleSkeletonFileDataID;
        public int MaleSkeletonFileDataID;
        public int HelmetAnimScalingRaceID;
        public int TransmogrifyDisabledSlotMask;
        public float[] AlteredFormCustomizeOffsetFallback = new float[3];
        public float AlteredFormCustomizeRotationFallback;
        public float[] Unknown910_1 = new float[3];
        public float[] Unknown910_2 = new float[3];
        public short FactionID;
        public short CinematicSequenceID;
        public sbyte BaseLanguage;
        public sbyte CreatureType;
        public sbyte Alliance;
        public sbyte Race_related;
        private sbyte _unalteredVisualRaceID;
        public sbyte DefaultClassID;
        public sbyte NeutralRaceID;
        public sbyte MaleModelFallbackRaceID;
        public sbyte MaleModelFallbackSex;
        public sbyte FemaleModelFallbackRaceID;
        public sbyte FemaleModelFallbackSex;
        public sbyte MaleTextureFallbackRaceID;
        public sbyte MaleTextureFallbackSex;
        public sbyte FemaleTextureFallbackRaceID;
        public sbyte FemaleTextureFallbackSex;
        public sbyte UnalteredVisualCustomizationRaceID;

        #region Properties
        public Race Id => (Race)_id;
        public ChrRacesFlag Flags => (ChrRacesFlag)_flags;
        public Race UnalteredVisualRaceID => (Race)_unalteredVisualRaceID;
        #endregion

        #region Helpers
        public bool HasFlag(ChrRacesFlag flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(ChrRacesFlag flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class ChrSpecializationRecord
    {
        public LocalizedString Name;
        public LocalizedString FemaleName;
        public LocalizedString Description;
        private int _id;
        private byte _classID;
        public sbyte OrderIndex;
        public sbyte PetTalentType;
        private sbyte _role;
        private uint _flags;
        public int SpellIconFileID;
        public sbyte PrimaryStatPriority;
        public int AnimReplacements;
        public int[] MasterySpellID = new int[PlayerConst.MaxMasterySpells];

        #region Properties
        public ChrSpecialization Id => (ChrSpecialization)_id;
        public Class ClassID => (Class)_classID;
        public ChrSpecializationFlag Flags => (ChrSpecializationFlag)_flags;
        public ChrSpecializationRole Role => (ChrSpecializationRole)_role;        
        #endregion

        #region Helpers
        public bool HasFlag(ChrSpecializationFlag flag)
        {
            return _flags.HasFlag((uint)flag);
        }

        public bool HasAnyFlag(ChrSpecializationFlag flag)
        {
            return _flags.HasAnyFlag((uint)flag);
        }

        public bool IsPetSpecialization => ClassID == 0;
        #endregion
    }

    public sealed class CinematicCameraRecord
    {
        public int Id;
        /// <summary>
        /// Position in map used for basis for M2 co-ordinates
        /// </summary>
        public Vector3 Origin;
        /// <summary>
        /// Sound ID (voiceover for cinematic)
        /// </summary>
        public uint SoundID;
        /// <summary>
        /// Orientation in map used for basis for M2 co
        /// </summary>
        public float OriginFacing;
        /// <summary>
        /// Model
        /// </summary>
        public uint FileDataID;
    }

    public sealed class CinematicSequencesRecord
    {
        public int Id;
        public int SoundID;
        public ushort[] Camera = new ushort[8];
    }

    public sealed class ConditionalChrModelRecord
    {
        public int Id;
        public int ChrModelID;                                      // This is the PK
        public int ChrCustomizationReqID;
        public int PlayerConditionID;
        public int Flags;
        public int ChrCustomizationCategoryID;
    }

    public sealed class ConditionalContentTuningRecord
    {
        public int Id;
        public int OrderIndex;
        public int RedirectContentTuningID;
        public int RedirectFlag;
        public int ParentContentTuningID;
    }

    public sealed class ContentTuningRecord
    {
        public int Id;
        public int MinLevel;
        public int MaxLevel;
        private int _flags;
        public int ExpectedStatModID;
        public int DifficultyESMID;

        #region Properties
        public ContentTuningFlag Flags => (ContentTuningFlag)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(ContentTuningFlag flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(ContentTuningFlag flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }

        public int GetScalingFactionGroup()
        {
            if (HasFlag(ContentTuningFlag.Horde))
                return 5;

            if (HasFlag(ContentTuningFlag.Alliance))
                return 3;

            return 0;
        }
        #endregion


    }
    
    public sealed class ConversationLineRecord
    {
        public int Id;
        public int BroadcastTextID;
        public int SpellVisualKitID;
        public int AdditionalDuration;
        public ushort NextConversationLineID;
        public ushort AnimKitID;
        public byte SpeechType;
        public byte StartAnimation;
        public byte EndAnimation;
    }

    public sealed class CreatureDisplayInfoRecord
    {
        public int Id;
        public ushort ModelID;
        public ushort SoundID;
        public sbyte SizeClass;
        public float CreatureModelScale;
        public byte CreatureModelAlpha;
        public byte BloodID;
        public int ExtendedDisplayInfoID;
        public ushort NPCSoundID;
        public ushort ParticleColorID;
        public int PortraitCreatureDisplayInfoID;
        public int PortraitTextureFileDataID;
        public ushort ObjectEffectPackageID;
        public ushort AnimReplacementSetID;
        public byte Flags;
        public int StateSpellVisualKitID;
        public float PlayerOverrideScale;
        /// <summary>
        /// scale of not own player pets inside dungeons/raids/scenarios
        /// </summary>
        public float PetInstanceScale;
        public sbyte UnarmedWeaponType;
        public int MountPoofSpellVisualKitID;
        public int DissolveEffectID;
        public sbyte Gender;
        public int DissolveOutEffectID;
        public sbyte CreatureModelMinLod;
        public int[] TextureVariationFileDataID = new int[4];
    }

    public sealed class CreatureDisplayInfoExtraRecord
    {
        public int Id;
        public sbyte DisplayRaceID;
        public sbyte DisplaySexID;
        public sbyte DisplayClassID;
        public sbyte SkinID;
        public sbyte FaceID;
        public sbyte HairStyleID;
        public sbyte HairColorID;
        public sbyte FacialHairID;
        public sbyte Flags;
        public int BakeMaterialResourcesID;
        public int HDBakeMaterialResourcesID;
        public byte[] CustomDisplayOption = new byte[3];
    }

    public sealed class CreatureFamilyRecord
    {
        public int Id;
        public LocalizedString Name;
        public float MinScale;
        public sbyte MinScaleLevel;
        public float MaxScale;
        public sbyte MaxScaleLevel;
        public ushort PetFoodMask;
        public sbyte PetTalentType;
        public int CategoryEnumID;
        public int IconFileID;
        private short[] _skillLine = new short[2];

        #region Properties
        public SkillType SkillLine(int index) => (SkillType)_skillLine[index];
        #endregion
    }

    public sealed class CreatureModelDataRecord
    {
        public int Id;
        public float[] GeoBox = new float[6];
        private uint _flags;
        public uint FileDataID;
        public uint BloodID;
        public uint FootprintTextureID;
        public float FootprintTextureLength;
        public float FootprintTextureWidth;
        public float FootprintParticleScale;
        public uint FoleyMaterialID;
        public uint FootstepCameraEffectID;
        public uint DeathThudCameraEffectID;
        public uint SoundID;
        public uint SizeClass;
        public float CollisionWidth;
        public float CollisionHeight;
        public float WorldEffectScale;
        public uint CreatureGeosetDataID;
        public float HoverHeight;
        public float AttachedEffectScale;
        public float ModelScale;
        public float MissileCollisionRadius;
        public float MissileCollisionPush;
        public float MissileCollisionRaise;
        public float MountHeight;
        public float OverrideLootEffectScale;
        public float OverrideNameScale;
        public float OverrideSelectionRadius;
        public float TamedPetBaseScale;

        #region Properties
        public CreatureModelDataFlags Flags => (CreatureModelDataFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(CreatureModelDataFlags flag)
        {
            return _flags.HasFlag((uint)flag);
        }

        public bool HasAnyFlag(CreatureModelDataFlags flag)
        {
            return _flags.HasAnyFlag((uint)flag);
        }
        #endregion
    }

    public sealed class CreatureTypeRecord
    {
        public int Id;
        public LocalizedString Name;
        public byte Flags;
    }

    public sealed class CriteriaRecord
    {
        public int Id;
        private short _type;
        public int Asset;
        public int ModifierTreeId;
        public int StartEvent;
        public int StartAsset;
        public ushort StartTimer;
        public int FailEvent;
        public int FailAsset;
        private int _flags;
        public short EligibilityWorldStateID;
        public sbyte EligibilityWorldStateValue;

        #region Properties
        public CriteriaType Type => (CriteriaType)_type;
        public CriteriaFlags Flags => (CriteriaFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(CriteriaFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(CriteriaFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class CriteriaTreeRecord
    {
        public int Id;
        public LocalizedString Description;
        public int Parent;
        public int Amount;
        public int Operator;
        public int CriteriaID;
        public int OrderIndex;
        private int _flags;

        #region Properties
        public CriteriaTreeFlags Flags => (CriteriaTreeFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(CriteriaTreeFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(CriteriaTreeFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class CurrencyContainerRecord
    {
        public int Id;
        public LocalizedString ContainerName;
        public LocalizedString ContainerDescription;
        public int MinAmount;
        public int MaxAmount;
        public int ContainerIconID;
        public int ContainerQuality;
        public int OnLootSpellVisualKitID;
        public int CurrencyTypesID;
    }

    public sealed class CurrencyTypesRecord
    {
        public int Id;
        public LocalizedString Name;
        public LocalizedString Description;
        public byte CategoryID;
        public int InventoryIconFileID;
        public int SpellWeight;
        public byte SpellCategory;
        public int MaxQty;
        public int MaxEarnablePerWeek;
        public sbyte Quality;
        public int FactionID;
        public int AwardConditionID;
        private int[] _flags = new int[2];

        #region Properties
        public CurrencyTypesFlags Flags => (CurrencyTypesFlags)_flags[0];
        public CurrencyTypesFlagsB FlagsB => (CurrencyTypesFlagsB)_flags[1];        
        #endregion

        #region Helpers
        public bool HasFlag(CurrencyTypesFlags flag)
        {
            return _flags[0].HasFlag((int)flag);
        }

        public bool HasAnyFlag(CurrencyTypesFlags flag)
        {
            return _flags[0].HasAnyFlag((int)flag);
        }

        public bool HasFlag(CurrencyTypesFlagsB flag)
        {
            return _flags[1].HasFlag((int)flag);
        }

        public bool HasAnyFlag(CurrencyTypesFlagsB flag)
        {
            return _flags[1].HasAnyFlag((int)flag);
        }

        public int Scaler => HasFlag(CurrencyTypesFlags._100_Scaler) ? 100 : 1;
        public bool HasMaxEarnablePerWeek => MaxEarnablePerWeek != 0 || HasFlag(CurrencyTypesFlags.ComputedWeeklyMaximum);

        public bool HasMaxQuantity(bool onLoad = false, bool onUpdateVersion = false)
        {
            if (onLoad && HasFlag(CurrencyTypesFlags.IgnoreMaxQtyOnLoad))
                return false;

            if (onUpdateVersion && HasFlag(CurrencyTypesFlags.UpdateVersionIgnoreMax))
                return false;

            return MaxQty != 0 || HasFlag(CurrencyTypesFlags.DynamicMaximum);
        }

        public bool HasTotalEarned => HasFlag(CurrencyTypesFlagsB.UseTotalEarnedForEarned);

        public bool IsAlliance => HasFlag(CurrencyTypesFlags.IsAllianceOnly);
        public bool IsHorde => HasFlag(CurrencyTypesFlags.IsHordeOnly);

        public bool IsSuppressingChatLog(bool onUpdateVersion = false)
        {
            if ((onUpdateVersion && HasFlag(CurrencyTypesFlags.SuppressChatMessageOnVersionChange)) ||
                HasFlag(CurrencyTypesFlags.SuppressChatMessages))
                return true;

            return false;
        }

        public bool IsTrackingQuantity => HasFlag(CurrencyTypesFlags.TrackQuantity);
        #endregion
    }

    public sealed class CurveRecord
    {
        public int Id;
        public byte Type;
        public byte Flags;

        #region Properties
        //public ??? Type => (???)_type;
        #endregion
    }

    public sealed class CurvePointRecord
    {
        public Vector2 Pos;
        public Vector2 PreSLSquishPos;
        public int Id;
        public int CurveID;
        public byte OrderIndex;
    }
}
