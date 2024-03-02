// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Numerics;

namespace Game.DataStorage
{
    public sealed class AchievementRecord
    {
        public LocalizedString Description;
        public LocalizedString Title;
        public LocalizedString Reward;
        public int Id;
        /// <summary> -1 = none </summary>
        public short InstanceID;
        private sbyte _faction;
        /// <summary> it's Achievement parent (can`t start while parent uncomplete,
        /// use its Criteria if don`t have own, use its progress on begin) </summary>
        public short Supercedes;
        public short Category;
        /// <summary>
        /// need this count of completed criterias (own or referenced achievement criterias)
        /// </summary>
        public byte MinimumCriteria;
        public byte Points;
        private int _flags;
        public short UiOrder;
        public int IconFileID;
        public int CriteriaTree;
        /// <summary>
        /// referenced achievement (counting of all completed criterias)
        /// </summary>
        public short SharesCriteria;

        #region Properties
        public AchievementFaction Faction => (AchievementFaction)_faction;
        public AchievementFlags Flags => (AchievementFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(AchievementFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(AchievementFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class AchievementCategoryRecord
    {
        public LocalizedString Name;
        public uint Id;
        public short Parent;
        public sbyte UiOrder;
    }

    public sealed class AdventureJournalRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Description;
        public LocalizedString ButtonText;
        public LocalizedString RewardDescription;
        public LocalizedString ContinueDescription;
        public byte Type;
        public uint PlayerConditionID;
        public int Flags;
        public byte ButtonActionType;
        public int TextureFileDataID;
        public ushort LfgDungeonID;
        public uint QuestID;
        public ushort BattleMasterListID;
        public byte PriorityMin;
        public byte PriorityMax;
        public int ItemID;
        public uint ItemQuantity;
        public ushort CurrencyType;
        public byte CurrencyQuantity;
        public ushort UiMapID;
        public uint[] BonusPlayerConditionID = new uint[2];
        public byte[] BonusValue = new byte[2];
    }

    public sealed class AdventureMapPOIRecord
    {
        public uint Id;
        public LocalizedString Title;
        public LocalizedString Description;
        public Vector2 WorldPosition;
        public sbyte Type;
        public uint PlayerConditionID;
        public uint QuestID;
        public uint LfgDungeonID;
        public int RewardItemID;
        public uint UiTextureAtlasMemberID;
        public uint UiTextureKitID;
        public int MapID;
        public uint AreaTableID;
    }

    public sealed class AnimationDataRecord
    {
        public int Id;
        public ushort Fallback;
        public byte BehaviorTier;
        public int BehaviorID;
        public int[] Flags = new int[2];
    }

    public sealed class AnimKitRecord
    {
        public int Id;
        public uint OneShotDuration;
        public ushort OneShotStopAnimKitID;
        public ushort LowDefAnimKitID;
    }

    public sealed class AreaGroupMemberRecord
    {
        public int Id;
        public ushort AreaID;
        public int AreaGroupID;
    }

    public sealed class AreaTableRecord
    {
        public int Id;
        public string ZoneName;
        public LocalizedString AreaName;
        public ushort ContinentID;
        public ushort ParentAreaID;
        public short AreaBit;
        public byte SoundProviderPref;
        public byte SoundProviderPrefUnderwater;
        public ushort AmbienceID;
        public ushort UwAmbience;
        public ushort ZoneMusic;
        public ushort UwZoneMusic;
        public sbyte ExplorationLevel;
        public ushort IntroSound;
        public uint UwIntroSound;
        public byte FactionGroupMask;
        public float AmbientMultiplier;
        private int _mountFlags;
        public short PvpCombatWorldStateID;
        public byte WildBattlePetLevelMin;
        public byte WildBattlePetLevelMax;
        public byte WindSettingsID;
        private int[] _flags = new int[2];
        public ushort[] LiquidTypeID = new ushort[4];

        #region Properties
        public AreaFlags Flags => (AreaFlags)_flags[0];
        public AreaFlags2 Flags2 => (AreaFlags2)_flags[1];
        public AreaMountFlags MountFlags => (AreaMountFlags)_mountFlags;
        #endregion

        #region Helpers
        public bool HasFlag(AreaFlags flag)
        {
            return _flags[0].HasFlag((int)flag);
        }

        public bool HasAnyFlag(AreaFlags flag)
        {
            return _flags[0].HasAnyFlag((int)flag);
        }

        public bool HasFlag(AreaFlags2 flag)
        {
            return _flags[1].HasFlag((int)flag);
        }

        public bool HasAnyFlag(AreaFlags2 flag)
        {
            return _flags[1].HasAnyFlag((int)flag);
        }

        public bool HasFlag(AreaMountFlags flag)
        {
            return _mountFlags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(AreaMountFlags flag)
        {
            return _mountFlags.HasAnyFlag((int)flag);
        }

        public bool IsSanctuary => HasFlag(AreaFlags.NoPvP);
        #endregion
    }

    public sealed class AreaTriggerRecord
    {
        public LocalizedString Message;
        public Vector3 Pos;
        public int Id;
        public short ContinentID;
        public sbyte PhaseUseFlags;
        public short PhaseID;
        public short PhaseGroupID;
        public float Radius;
        public float BoxLength;
        public float BoxWidth;
        public float BoxHeight;
        public float BoxYaw;
        public sbyte ShapeType;
        public short ShapeID;
        public short AreaTriggerActionSetID;
        public sbyte Flags;
    }

    public sealed class ArmorLocationRecord
    {
        public uint Id;
        public float Clothmodifier;
        public float Leathermodifier;
        public float Chainmodifier;
        public float Platemodifier;
        public float Modifier;
    }

    public sealed class ArtifactRecord
    {
        public LocalizedString Name;
        public uint Id;
        public ushort UiTextureKitID;
        public int UiNameColor;
        public int UiBarOverlayColor;
        public int UiBarBackgroundColor;
        public ushort ChrSpecializationID;
        public byte Flags;
        public byte ArtifactCategoryID;
        public uint UiModelSceneID;
        public uint SpellVisualKitID;
    }

    public sealed class ArtifactAppearanceRecord
    {
        public LocalizedString Name;
        public int Id;
        public ushort ArtifactAppearanceSetID;
        public byte DisplayIndex;
        public uint UnlockPlayerConditionID;
        public byte ItemAppearanceModifierID;
        public int UiSwatchColor;
        public float UiModelSaturation;
        public float UiModelOpacity;
        public byte OverrideShapeshiftFormID;
        public uint OverrideShapeshiftDisplayID;
        public uint UiItemAppearanceID;
        public uint UiAltItemAppearanceID;
        public byte Flags;
        public ushort UiCameraID;
    }

    public sealed class ArtifactAppearanceSetRecord
    {
        public LocalizedString Name;
        public LocalizedString Description;
        public uint Id;
        public byte DisplayIndex;
        public ushort UiCameraID;
        public ushort AltHandUICameraID;
        public sbyte ForgeAttachmentOverride;
        public byte Flags;
        public uint ArtifactID;
    }

    public sealed class ArtifactCategoryRecord
    {
        public uint Id;
        public short XpMultCurrencyID;
        public short XpMultCurveID;
    }

    public sealed class ArtifactPowerRecord
    {
        public Vector2 DisplayPos;
        public uint Id;
        public byte ArtifactID;
        public byte MaxPurchasableRank;
        public int Label;
        private byte _flags;
        public byte Tier;

        #region Properties
        public ArtifactPowerFlag Flags => (ArtifactPowerFlag)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(ArtifactPowerFlag flag)
        {
            return _flags.HasFlag((byte)flag);
        }

        public bool HasAnyFlag(ArtifactPowerFlag flag)
        {
            return _flags.HasAnyFlag((byte)flag);
        }
        #endregion
    }

    public sealed class ArtifactPowerLinkRecord
    {
        public uint Id;
        public ushort PowerA;
        public ushort PowerB;
    }

    public sealed class ArtifactPowerPickerRecord
    {
        public uint Id;
        public uint PlayerConditionID;
    }

    public sealed class ArtifactPowerRankRecord
    {
        public uint Id;
        public byte RankIndex;
        public int SpellID;
        public ushort ItemBonusListID;
        public float AuraPointsOverride;
        public uint ArtifactPowerID;
    }

    public sealed class ArtifactQuestXPRecord
    {
        public uint Id;
        public uint[] Difficulty = new uint[10];
    }

    public sealed class ArtifactTierRecord
    {
        public uint Id;
        public uint ArtifactTier;
        public uint MaxNumTraits;
        public uint MaxArtifactKnowledge;
        public uint KnowledgePlayerCondition;
        public uint MinimumEmpowerKnowledge;
    }

    public sealed class ArtifactUnlockRecord
    {
        public uint Id;
        public uint PowerID;
        public byte PowerRank;
        public ushort ItemBonusListID;
        public uint PlayerConditionID;
        public uint ArtifactID;
    }

    public sealed class AuctionHouseRecord
    {
        public uint Id;
        public LocalizedString Name;
        /// <summary>
        /// id of faction.dbc for player factions associated with city
        /// </summary>
        public ushort FactionID;
        public byte DepositRate;
        public byte ConsignmentRate;
    }

    public sealed class AzeriteEmpoweredItemRecord
    {
        public uint Id;
        public int ItemID;
        public uint AzeriteTierUnlockSetID;
        public uint AzeritePowerSetID;
    }

    public sealed class AzeriteEssenceRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Description;
        public int SpecSetID;
    }

    public sealed class AzeriteEssencePowerRecord
    {
        public uint Id;
        public LocalizedString SourceAlliance;
        public LocalizedString SourceHorde;
        public int AzeriteEssenceID;
        public byte Tier;
        public int MajorPowerDescription;
        public int MinorPowerDescription;
        public int MajorPowerActual;
        public int MinorPowerActual;
    }

    public sealed class AzeriteItemRecord
    {
        public uint Id;
        public int ItemID;
    }

    public sealed class AzeriteItemMilestonePowerRecord
    {
        public uint Id;
        public int RequiredLevel;
        public int AzeritePowerID;
        public int Type;
        public int AutoUnlock;
    }

    public sealed class AzeriteKnowledgeMultiplierRecord
    {
        public uint Id;
        public float Multiplier;
    }

    public sealed class AzeriteLevelInfoRecord
    {
        public uint Id;
        public ulong BaseExperienceToNextLevel;
        public ulong MinimumExperienceToNextLevel;
        public int ItemLevel;
    }

    public sealed class AzeritePowerRecord
    {
        public uint Id;
        public int SpellID;
        public int ItemBonusListID;
        public int SpecSetID;
        public int Flags;
    }

    public sealed class AzeritePowerSetMemberRecord
    {
        public uint Id;
        public uint AzeritePowerSetID;
        public int AzeritePowerID;
        public int Class;
        public byte Tier;
        public int OrderIndex;
    }

    public sealed class AzeriteTierUnlockRecord
    {
        public uint Id;
        public byte ItemCreationContext;
        public byte Tier;
        public byte AzeriteLevel;
        public uint AzeriteTierUnlockSetID;
    }

    public sealed class AzeriteTierUnlockSetRecord
    {
        public uint Id;
        private int _flags;


        #region Properties
        public AzeriteTierUnlockSetFlags Flags => (AzeriteTierUnlockSetFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(AzeriteTierUnlockSetFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(AzeriteTierUnlockSetFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }
}
