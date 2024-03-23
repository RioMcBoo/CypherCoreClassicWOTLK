// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Numerics;

namespace Game.DataStorage
{
    public sealed class TactKeyRecord
    {
        public const int TACTKEY_SIZE = 16;

        public uint Id;
        public byte[] Key = new byte[TACTKEY_SIZE];
    }

    public sealed class TalentRecord
    {
        public int Id;
        public LocalizedString Description;
        public byte TierID;
        public byte Flags;
        public byte ColumnIndex;
        public ushort TabID;
        private byte _classID;
        public ushort SpecID;
        public int SpellID;
        public int OverridesSpellID;
        public int RequiredSpellID;
        public int[] CategoryMask = new int[2];
        public int[] SpellRank = new int[9];
        public int[] PrereqTalent = new int[3];
        public int[] PrereqRank = new int[3];

        #region Properties
        public Class ClassID => (Class)_classID;
        #endregion
    }

    public sealed class TalentTabRecord
    {
        public int Id;
        public LocalizedString Name;
        public string BackgroundFile;
        public int OrderIndex;
        private int _raceMask;
        private int _classMask;
        public uint PetTalentMask;
        public int SpellIconID;

        #region Properties
        public RaceMask RaceMask => (RaceMask)_raceMask;
        public ClassMask ClassMask => (ClassMask)_classMask;
        #endregion
    };

    public sealed class TaxiNodesRecord
    {
        public LocalizedString Name;
        public Vector3 Pos;
        public Vector2 MapOffset;
        public Vector2 FlightMapOffset;
        public int Id;
        public int ContinentID;
        public int ConditionID;
        public ushort CharacterBitNumber;
        private int _flags;
        public int UiTextureKitID;
        public float Facing;
        public int SpecialIconConditionID;
        public int VisibilityConditionID;
        private int[] _mountCreatureID = new int[2];

        #region Properties
        public TaxiNodeFlags Flags => (TaxiNodeFlags)_flags;
        public int MountCreatureID(Team team)
        {
            switch (team)
            {
                case Team.Horde:
                    return _mountCreatureID[0];
                case Team.Alliance:
                    return _mountCreatureID[1];
                default:
                    Cypher.Assert(false, $"Wrong variable of enum `Team`:{team}", "public sealed class TaxiNodesRecord, public int MountCreatureID(Team team), wrong variable value `team`.");
                    return 0;
            }
        }
        #endregion

        #region Helpers
        public bool HasFlag(TaxiNodeFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(TaxiNodeFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }

        public bool IsPartOfTaxiNetwork
        {
            get
            {
                return HasAnyFlag(TaxiNodeFlags.ShowOnAllianceMap | TaxiNodeFlags.ShowOnHordeMap)
                    // manually whitelisted nodes
                    || Id == 1985   // [Hidden] Argus Ground Points Hub (Ground TP out to here, TP to Vindicaar from here)
                    || Id == 1986   // [Hidden] Argus Vindicaar Ground Hub (Vindicaar TP out to here, TP to ground from here)
                    || Id == 1987   // [Hidden] Argus Vindicaar No Load Hub (Vindicaar No Load transition goes through here)
                    || Id == 2627   // [Hidden] 9.0 Bastion Ground Points Hub (Ground TP out to here, TP to Sanctum from here)
                    || Id == 2628   // [Hidden] 9.0 Bastion Ground Hub (Sanctum TP out to here, TP to ground from here)
                    || Id == 2732   // [HIDDEN] 9.2 Resonant Peaks - Teleport Network - Hidden Hub (Connects all Nodes to each other without unique paths)
                    || Id == 2835   // [Hidden] 10.0 Travel Network - Destination Input
                    || Id == 2843   // [Hidden] 10.0 Travel Network - Destination Output
                ;
            }
        }
        #endregion
    }

    public sealed class TaxiPathRecord
    {
        public int Id;
        public ushort FromTaxiNode;
        public ushort ToTaxiNode;
        public uint Cost;
    }

    public sealed class TaxiPathNodeRecord
    {
        public Vector3 Loc;
        public int Id;
        public ushort PathID;
        public int NodeIndex;
        public ushort ContinentID;
        private int _flags;
        public uint Delay;
        public int ArrivalEventID;
        public int DepartureEventID;

        #region Properties
        public TaxiNodeFlags Flags => (TaxiNodeFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(TaxiNodeFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(TaxiNodeFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class TotemCategoryRecord
    {
        public int Id;
        public LocalizedString Name;
        public byte TotemCategoryType;
        public int TotemCategoryMask;
    }

    public sealed class ToyRecord
    {
        public LocalizedString SourceText;
        public int Id;
        public int ItemID;
        public byte Flags;
        public sbyte SourceTypeEnum;

        #region Properties
        //public ??? Flags => (???)Flags;
        #endregion
    }   

    public sealed class TransmogHolidayRecord
    {
        public int Id;
        public int RequiredTransmogHoliday;
    }

    public sealed class TraitCondRecord
    {
        public int Id;
        private int _condType;
        public int TraitTreeID;
        public int GrantedRanks;
        public int QuestID;
        public int AchievementID;
        public int SpecSetID;
        public int TraitNodeGroupID;
        public int TraitNodeID;
        public int TraitCurrencyID;
        public int SpentAmountRequired;
        public uint Flags;
        public int RequiredLevel;
        public int FreeSharedStringID;
        public int SpendMoreSharedStringID;

        #region Properties
        public TraitConditionType CondType => (TraitConditionType)_condType;
        #endregion
    }

    public sealed class TraitCostRecord
    {
        public string InternalName;
        public int Id;
        public int Amount;
        public int TraitCurrencyID;
    }

    public sealed class TraitCurrencyRecord
    {
        public int Id;
        private int _type;
        public int CurrencyTypesID;
        public int Flags;
        public int Icon;

        #region Properties
        public TraitCurrencyType CurrencyType => (TraitCurrencyType)_type;
        #endregion
    }

    public sealed class TraitCurrencySourceRecord
    {
        public LocalizedString Requirement;
        public int Id;
        public int TraitCurrencyID;
        public int Amount;
        public int QuestID;
        public int AchievementID;
        public int PlayerLevel;
        public int TraitNodeEntryID;
        public int OrderIndex;
    }

    public sealed class TraitDefinitionRecord
    {
        public LocalizedString OverrideName;
        public LocalizedString OverrideSubtext;
        public LocalizedString OverrideDescription;
        public int Id;
        public int SpellID;
        public int OverrideIcon;
        public int OverridesSpellID;
        public int VisibleSpellID;
    }

    public sealed class TraitDefinitionEffectPointsRecord
    {
        public int Id;
        public int TraitDefinitionID;
        public int EffectIndex;
        private int _operationType;
        public int CurveID;

        #region Properties
        public TraitPointsOperationType OperationType => (TraitPointsOperationType)_operationType;
        #endregion
    }

    public sealed class TraitEdgeRecord
    {
        public int Id;
        public int VisualStyle;
        public int LeftTraitNodeID;
        public int RightTraitNodeID;
        public int Type;
    }

    public sealed class TraitNodeRecord
    {
        public int Id;
        public int TraitTreeID;
        public int PosX;
        public int PosY;
        private byte _type;
        public uint Flags;

        #region Properties
        public TraitNodeType NodeType => (TraitNodeType)_type;
        #endregion
    }

    public sealed class TraitNodeEntryRecord
    {
        public int Id;
        public int TraitDefinitionID;
        public int MaxRanks;
        private byte _nodeEntryType;

        #region Properties
        public TraitNodeEntryType NodeEntryType => (TraitNodeEntryType)_nodeEntryType;
        #endregion
    }

    public sealed class TraitNodeEntryXTraitCondRecord
    {
        public int Id;
        public int TraitCondID;
        public int TraitNodeEntryID;
    }

    public sealed class TraitNodeEntryXTraitCostRecord
    {
        public int Id;
        public int TraitNodeEntryID;
        public int TraitCostID;
    }

    public sealed class TraitNodeGroupRecord
    {
        public int Id;
        public int TraitTreeID;
        public uint Flags;
    }

    public sealed class TraitNodeGroupXTraitCondRecord
    {
        public int Id;
        public int TraitCondID;
        public int TraitNodeGroupID;
    }

    public sealed class TraitNodeGroupXTraitCostRecord
    {
        public int Id;
        public int TraitNodeGroupID;
        public int TraitCostID;
    }

    public sealed class TraitNodeGroupXTraitNodeRecord
    {
        public int Id;
        public int TraitNodeGroupID;
        public int TraitNodeID;
        public int Index;
    }

    public sealed class TraitNodeXTraitCondRecord
    {
        public int Id;
        public int TraitCondID;
        public int TraitNodeID;
    }

    public sealed class TraitNodeXTraitCostRecord
    {
        public int Id;
        public int TraitNodeID;
        public int TraitCostID;
    }

    public sealed class TraitNodeXTraitNodeEntryRecord
    {
        public int Id;
        public int TraitNodeID;
        public int TraitNodeEntryID;
        public int Index;
    }

    public sealed class TraitTreeRecord
    {
        public int Id;
        public int TraitSystemID;
        public int Unused1000_1;
        public int FirstTraitNodeID;
        public int PlayerConditionID;
        private int _flags;
        public float Unused1000_2;
        public float Unused1000_3;

        #region Properties
        public TraitTreeFlag Flags => (TraitTreeFlag)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(TraitTreeFlag flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(TraitTreeFlag flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class TraitTreeLoadoutRecord
    {
        public int Id;
        public int TraitTreeID;
        public int ChrSpecializationID;
    }

    public sealed class TraitTreeLoadoutEntryRecord
    {
        public int Id;
        public int TraitTreeLoadoutID;
        public int SelectedTraitNodeID;
        public int SelectedTraitNodeEntryID;
        public int NumPoints;
        public int OrderIndex;
    }

    public sealed class TraitTreeXTraitCostRecord
    {
        public int Id;
        public int TraitTreeID;
        public int TraitCostID;
    }

    public sealed class TraitTreeXTraitCurrencyRecord
    {
        public int Id;
        public int Index;
        public int TraitTreeID;
        public int TraitCurrencyID;
    }

    public sealed class TransmogSetRecord
    {
        public LocalizedString Name;
        public int Id;
        public int ClassMask;
        public int TrackingQuestID;
        public int Flags;
        public int TransmogSetGroupID;
        public int ItemNameDescriptionID;
        public ushort ParentTransmogSetID;
        public byte ExpansionID;
        public short UiOrder;
    }

    public sealed class TransmogSetGroupRecord
    {
        public LocalizedString Name;
        public int Id;        
    }

    public sealed class TransmogSetItemRecord
    {
        public int Id;
        public int TransmogSetID;
        public int ItemModifiedAppearanceID;
        public int Flags;
    }

    public sealed class TransportAnimationRecord
    {
        public int Id;
        public Vector3 Pos;
        public byte SequenceID;
        public uint TimeIndex;
        public int TransportID;
    }

    public sealed class TransportRotationRecord
    {
        public int Id;
        public float[] Rot = new float[4];
        public uint TimeIndex;
        public int GameObjectsID;
    }
}