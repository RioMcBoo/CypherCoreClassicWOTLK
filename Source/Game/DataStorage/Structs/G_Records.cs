// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System;
using System.Numerics;

namespace Game.DataStorage
{
    public sealed class GameObjectArtKitRecord
    {
        public uint Id;
        public int AttachModelFileID;
        public int[] TextureVariationFileID = new int[3];
    }

    public sealed class GameObjectDisplayInfoRecord
    {
        public uint Id;
        public string ModelName;
        public float[] GeoBox = new float[6];
        public int FileDataID;
        public short ObjectEffectPackageID;
        public float OverrideLootEffectScale;
        public float OverrideNameScale;
        public int AlternateDisplayType;
        public int ClientCreatureDisplayInfoID;
        public int ClientItemID;

        public Vector3 GeoBoxMin
        {
            get { return new Vector3(GeoBox[0], GeoBox[1], GeoBox[2]); }
            set { GeoBox[0] = value.X; GeoBox[1] = value.Y; GeoBox[2] = value.Z; }
        }
        public Vector3 GeoBoxMax
        {
            get { return new Vector3(GeoBox[3], GeoBox[4], GeoBox[5]); }
            set { GeoBox[3] = value.X; GeoBox[4] = value.Y; GeoBox[5] = value.Z; }
        }
    }

    public sealed class GameObjectsRecord
    {
        public LocalizedString Name;
        public Vector3 Pos;
        public float[] Rot = new float[4];
        public uint Id;
        public ushort OwnerID;
        public uint DisplayID;
        public float Scale;
        public GameObjectTypes TypeID;
        public byte PhaseUseFlags;
        public ushort PhaseID;
        public ushort PhaseGroupID;
        public int[] PropValue = new int[8];
    }

    public sealed class GarrAbilityRecord
    {        
        public string Name;
        public string Description;
        public uint Id;
        public byte GarrAbilityCategoryID;
        public byte GarrFollowerTypeID;
        public int IconFileDataID;
        public ushort FactionChangeGarrAbilityID;
        public GarrisonAbilityFlags Flags;
    }

    public sealed class GarrBuildingRecord
    {
        public uint Id;
        public string HordeName;
        public string AllianceName;
        public string Description;
        public string Tooltip;
        public sbyte GarrTypeID;
        public sbyte BuildingType;
        public uint HordeGameObjectID;
        public uint AllianceGameObjectID;
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
        public GarrisonBuildingFlags Flags;
    }

    public sealed class GarrBuildingPlotInstRecord
    {
        public Vector2 MapOffset;
        public uint Id;
        public byte GarrBuildingID;
        public ushort GarrSiteLevelPlotInstID;
        public ushort UiTextureAtlasMemberID;
    }

    public sealed class GarrClassSpecRecord
    {       
        public string ClassSpec;
        public string ClassSpecMale;
        public string ClassSpecFemale;
        public uint Id;
        public ushort UiTextureAtlasMemberID;
        public ushort GarrFollItemSetID;
        public byte FollowerClassLimit;
        public byte Flags;
    }

    public sealed class GarrFollowerRecord
    {        
        public string HordeSourceText;
        public string AllianceSourceText;
        public string TitleName;
        public sbyte GarrTypeID;
        public sbyte GarrFollowerTypeID;
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
        public uint Id;
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
        public int GarrMissionSetID;
    }

    public sealed class GarrPlotRecord
    {
        public uint Id;
        public string Name;
        public byte PlotType;
        public uint HordeConstructObjID;
        public uint AllianceConstructObjID;
        public byte Flags;
        public byte UiCategoryID;
        public uint[] UpgradeRequirement = new uint[2];
    }

    public sealed class GarrPlotBuildingRecord
    {
        public uint Id;
        public byte GarrPlotID;
        public byte GarrBuildingID;
    }

    public sealed class GarrPlotInstanceRecord
    {
        public uint Id;
        public string Name;
        public byte GarrPlotID;
    }

    public sealed class GarrSiteLevelRecord
    {
        public uint Id;
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
        public uint Id;
        public Vector2 UiMarkerPos;
        public ushort GarrSiteLevelID;
        public byte GarrPlotInstanceID;
        public byte UiMarkerSize;
    }

    public sealed class GarrTalentTreeRecord
    {
        public uint Id;
        public string Name;
        public sbyte GarrTypeID;
        public int ClassID;
        public sbyte MaxTiers;
        public sbyte UiOrder;
        public int Flags;
        public ushort UiTextureKitID;
        public int GarrTalentTreeType;
        public int PlayerConditionID;
        public byte FeatureTypeIndex;
        public sbyte FeatureSubtypeIndex;
        public int CurrencyID;
    }

    public sealed class GemPropertiesRecord
    {
        public uint Id;
        public ushort EnchantId;
        public SocketColor Type;
        public ushort MinItemLevel;
    }

    public sealed class GlyphBindableSpellRecord
    {
        public uint Id;
        public int SpellID;
        public uint GlyphPropertiesID;
    }

    public sealed class GlyphPropertiesRecord
    {
        public uint Id;
        public uint SpellID;
        public byte GlyphType;
        public byte GlyphExclusiveCategoryID;
        public uint SpellIconFileDataID;
        public uint GlyphSlotFlags;
    }

    public sealed class GlyphRequiredSpecRecord
    {
        public uint Id;
        public ushort ChrSpecializationID;
        public uint GlyphPropertiesID;
    }

    public sealed class GlyphSlotRecord
    {
        public uint Id;
        public int ToolTip;
        public uint Type;
    };

    public sealed class GossipNPCOptionRecord
    {
        public uint Id;
        public int GossipNpcOption;
        public int LFGDungeonsID;
        public int TrainerID;
        public int GarrFollowerTypeID;
        public int CharShipmentID;
        public int GarrTalentTreeID;
        public int UiMapID;
        public int UiItemInteractionID;
        public int Unknown_1000_8;
        public int Unknown_1000_9;
        public int CovenantID;
        public int GossipOptionID;
        public int TraitTreeID;
        public int ProfessionID;
        public int Unknown_1002_14;
    }

    public sealed class GuildColorBackgroundRecord
    {
        public uint Id;
        public byte Red;
        public byte Blue;
        public byte Green;
    }

    public sealed class GuildColorBorderRecord
    {
        public uint Id;
        public byte Red;
        public byte Blue;
        public byte Green;
    }

    public sealed class GuildColorEmblemRecord
    {
        public uint Id;
        public byte Red;
        public byte Blue;
        public byte Green;
    }

    public sealed class GuildPerkSpellsRecord
    {
        public uint Id;
        public uint SpellID;
    }
}
