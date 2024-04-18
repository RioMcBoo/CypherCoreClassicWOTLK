// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Numerics;

namespace Game.DataStorage
{
    public sealed class GameObjectArtKitRecord
    {
        public int Id;
        public int AttachModelFileID;
        public int[] TextureVariationFileID = new int[3];
    }

    public sealed class GameObjectDisplayInfoRecord
    {
        public int Id;
        public string ModelName;
        public Vector3[] GeoBoxes = new Vector3[2];
        public int FileDataID;
        public short ObjectEffectPackageID;
        public float OverrideLootEffectScale;
        public float OverrideNameScale;

        #region Properties
        public Vector3 GeoBoxMin
        {
            get { return GeoBoxes[0]; } 
            set { GeoBoxes[0] = value; }
        }
        public Vector3 GeoBoxMax
        {
            get { return GeoBoxes[1]; }
            set { GeoBoxes[1] = value; }
        }
        #endregion
    }

    public sealed class GameObjectsRecord
    {
        public LocalizedString Name;
        public Vector3 Pos;
        public float[] Rot = new float[4];
        public int Id;
        public ushort OwnerID;
        public int DisplayID;
        public float Scale;
        private byte _typeID;
        public byte PhaseUseFlags;
        public ushort PhaseID;
        public ushort PhaseGroupID;
        public int[] PropValue = new int[8];

        #region Properties
        public GameObjectTypes TypeID => (GameObjectTypes)_typeID;
        #endregion
    }

    public sealed class GarrAbilityRecord
    {        
        public LocalizedString Name;
        public LocalizedString Description;
        public int Id;
        public byte GarrAbilityCategoryID;
        public byte GarrFollowerTypeID;
        public int IconFileDataID;
        public ushort FactionChangeGarrAbilityID;
        private ushort _flags;

        #region Properties
        public GarrisonAbilityFlags Flags => (GarrisonAbilityFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(GarrisonAbilityFlags flag)
        {
            return _flags.HasFlag((ushort)flag);
        }

        public bool HasAnyFlag(GarrisonAbilityFlags flag)
        {
            return _flags.HasAnyFlag((ushort)flag);
        }
        #endregion
    }

    public sealed class GarrBuildingRecord
    {
        public int Id;
        public LocalizedString HordeName;
        public LocalizedString AllianceName;
        public LocalizedString Description;
        public LocalizedString Tooltip;
        public byte GarrTypeID;
        public byte BuildingType;
        public int HordeGameObjectID;
        public int AllianceGameObjectID;
        public byte GarrSiteID;
        public byte UpgradeLevel;
        public int BuildSeconds;
        public ushort CurrencyTypeID;
        public int CurrencyQty;
        public ushort HordeUiTextureKitID;
        public ushort AllianceUiTextureKitID;
        public int IconFileDataID;
        public ushort AllianceSceneScriptPackageID;
        public ushort HordeSceneScriptPackageID;
        public int MaxAssignments;
        public byte ShipmentCapacity;
        public ushort GarrAbilityID;
        public ushort BonusGarrAbilityID;
        public ushort GoldCost;
        private byte _flags;

        #region Properties
        public GarrisonBuildingFlags Flags => (GarrisonBuildingFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(GarrisonBuildingFlags flag)
        {
            return _flags.HasFlag((byte)flag);
        }

        public bool HasAnyFlag(GarrisonBuildingFlags flag)
        {
            return _flags.HasAnyFlag((byte)flag);
        }
        #endregion
    }

    public sealed class GarrBuildingPlotInstRecord
    {
        public Vector2 MapOffset;
        public int Id;
        public byte GarrBuildingID;
        public ushort GarrSiteLevelPlotInstID;
        public ushort UiTextureAtlasMemberID;
    }

    public sealed class GarrClassSpecRecord
    {       
        public LocalizedString ClassSpec;
        public LocalizedString ClassSpecMale;
        public LocalizedString ClassSpecFemale;
        public int Id;
        public ushort UiTextureAtlasMemberID;
        public ushort GarrFollItemSetID;
        public byte FollowerClassLimit;
        public byte Flags;
    }

    public sealed class GarrFollowerRecord
    {        
        public LocalizedString HordeSourceText;
        public LocalizedString AllianceSourceText;
        public LocalizedString TitleName;
        public int Id;
        public byte GarrTypeID;
        public byte GarrFollowerTypeID;
        public int HordeCreatureID;
        public int AllianceCreatureID;
        public byte HordeGarrFollRaceID;
        public byte AllianceGarrFollRaceID;
        public byte HordeGarrClassSpecID;
        public byte AllianceGarrClassSpecID;
        public byte Quality;
        public byte FollowerLevel;
        public ushort ItemLevelWeapon;
        public ushort ItemLevelArmor;
        public sbyte HordeSourceTypeEnum;
        public sbyte AllianceSourceTypeEnum;
        public int HordeIconFileDataID;
        public int AllianceIconFileDataID;
        public ushort HordeGarrFollItemSetID;
        public ushort AllianceGarrFollItemSetID;
        public ushort HordeUITextureKitID;
        public ushort AllianceUITextureKitID;
        public byte Vitality;
        public byte HordeFlavorGarrStringID;
        public byte AllianceFlavorGarrStringID;
        public uint HordeSlottingBroadcastTextID;
        public uint AllySlottingBroadcastTextID;
        public byte ChrClassID;
        public byte Flags;
        public byte Gender;
    }

    public sealed class GarrFollowerXAbilityRecord
    {
        public int Id;
        public byte OrderIndex;
        public byte FactionIndex;
        public ushort GarrAbilityID;
        public uint GarrFollowerID;
    }

    public sealed class GarrMissionRecord
    {        
        public LocalizedString Name;
        public LocalizedString Location;
        public LocalizedString Description;
        public Vector2 MapPos;
        public Vector2 WorldPos;
        public int Id;
        public sbyte GarrTypeID;
        public byte GarrMissionTypeID;
        public byte GarrFollowerTypeID;
        public byte MaxFollowers;
        public uint MissionCost;
        public ushort MissionCostCurrencyTypesID;
        public byte OfferedGarrMissionTextureID;
        public ushort UiTextureKitID;
        public uint EnvGarrMechanicID;
        public byte EnvGarrMechanicTypeID;
        public uint PlayerConditionID;
        public sbyte TargetLevel;
        public ushort TargetItemLevel;
        public int MissionDuration;
        public int TravelDuration;
        public uint OfferDuration;
        public byte BaseCompletionChance;
        public uint BaseFollowerXP;
        public uint OvermaxRewardPackID;
        public byte FollowerDeathChance;
        public uint AreaID;
        public uint Flags;
        public uint GarrMissionSetID;
    }

    public sealed class GarrPlotRecord
    {
        public int Id;
        public string Name;
        public byte PlotType;
        public int HordeConstructObjID;
        public int AllianceConstructObjID;
        public byte Flags;
        public byte UiCategoryID;
        public uint[] UpgradeRequirement = new uint[2];
    }

    public sealed class GarrPlotBuildingRecord
    {
        public int Id;
        public byte GarrPlotID;
        public byte GarrBuildingID;
    }

    public sealed class GarrPlotInstanceRecord
    {
        public int Id;
        public string Name;
        public byte GarrPlotID;
    }

    public sealed class GarrSiteLevelRecord
    {
        public int Id;
        public Vector2 TownHallUiPos;
        public uint GarrSiteID;
        public byte GarrLevel;
        public ushort MapID;
        public ushort UpgradeMovieID;
        public ushort UiTextureKitID;
        public byte MaxBuildingLevel;
        public ushort UpgradeCost;
        public ushort UpgradeGoldCost;
    }

    public sealed class GarrSiteLevelPlotInstRecord
    {
        public int Id;
        public Vector2 UiMarkerPos;
        public ushort GarrSiteLevelID;
        public byte GarrPlotInstanceID;
        public byte UiMarkerSize;
    }

    public sealed class GarrTalentTreeRecord
    {
        public int Id;
        public LocalizedString Name;
        public int GarrTypeID;
        public int ClassID;
        public sbyte MaxTiers;
        public sbyte UiOrder;
        public sbyte Flags;
        public ushort UiTextureKitID;
    }

    public sealed class GemPropertiesRecord
    {
        public int Id;
        public ushort EnchantId;
        private int _typeMask;
        public ushort MinItemLevel;

        #region Properties
        public SocketColor Color => (SocketColor)_typeMask;
        #endregion
    }

    public sealed class GlyphBindableSpellRecord
    {
        public int Id;
        public int SpellID;
        public int GlyphPropertiesID;
    }

    public sealed class GlyphSlotRecord
    {
        public int Id;
        public int ToolTip;
        public int Type;
    }

    public sealed class GlyphPropertiesRecord
    {
        public int Id;
        public int SpellID;
        public byte GlyphType;
        public byte GlyphExclusiveCategoryID;
        public int SpellIconFileDataID;
        public uint GlyphSlotFlags;
    }

    public sealed class GlyphRequiredSpecRecord
    {
        public int Id;
        public ushort ChrSpecializationID;
        public int GlyphPropertiesID;
    }

    public sealed class GossipNPCOptionRecord
    {
        public int Id;
        public int GossipNpcOption;
        public int LFGDungeonsID;
        public int Unk341_1;
        public int Unk341_2;
        public int Unk341_3;
        public int Unk341_4;
        public int Unk341_5;
        public int Unk341_6;
        public int Unk341_7;
        public int Unk341_8;
        public int Unk341_9;
        public int GossipOptionID;
    }

    public sealed class GuildColorBackgroundRecord
    {
        public int Id;
        public byte Red;
        public byte Blue;
        public byte Green;
    }

    public sealed class GuildColorBorderRecord
    {
        public int Id;
        public byte Red;
        public byte Blue;
        public byte Green;
    }

    public sealed class GuildColorEmblemRecord
    {
        public int Id;
        public byte Red;
        public byte Blue;
        public byte Green;
    }

    public sealed class GuildPerkSpellsRecord
    {
        public int Id;
        public int SpellID;
    }
}
