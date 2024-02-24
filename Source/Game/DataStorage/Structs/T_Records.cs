// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Numerics;
using System.Xml.Linq;
using static Game.AI.SmartAction;

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
        public uint Id;
        public LocalizedString Description;
        public byte TierID;
        public byte Flags;
        public byte ColumnIndex;
        public ushort TabID;
        public byte ClassID;
        public ushort SpecID;
        public int SpellID;
        public int OverridesSpellID;
        public int RequiredSpellID;
        public int[] CategoryMask = new int[2];
        public int[] SpellRank = new int[9];
        public int[] PrereqTalent = new int[3];
        public int[] PrereqRank = new int[3];
    }

    public sealed class TalentTabRecord
    {
        public uint Id;
        public LocalizedString Name;
        public string BackgroundFile;
        public int OrderIndex;
        public int RaceMask;
        public int ClassMask;
        public int PetTalentMask;
        public int SpellIconID;
    };

    public sealed class TaxiNodesRecord
    {
        public LocalizedString Name;
        public Vector3 Pos;
        public Vector2 MapOffset;
        public Vector2 FlightMapOffset;
        public uint Id;
        public uint ContinentID;
        public uint ConditionID;
        public ushort CharacterBitNumber;
        private int _flags;
        public int UiTextureKitID;
        public float Facing;
        public uint SpecialIconConditionID;
        public uint VisibilityConditionID;
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
        public uint Id;
        public ushort FromTaxiNode;
        public ushort ToTaxiNode;
        public uint Cost;
    }

    public sealed class TaxiPathNodeRecord
    {
        public Vector3 Loc;
        public uint Id;
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
        public uint Id;
        public LocalizedString Name;
        public byte TotemCategoryType;
        public int TotemCategoryMask;
    }

    public sealed class ToyRecord
    {
        public LocalizedString SourceText;
        public uint Id;
        private int _itemID;
        public byte Flags;
        public sbyte SourceTypeEnum;

        #region Properties
        public uint ItemID => (uint)_itemID;
        #endregion
    }   

    public sealed class TransmogHolidayRecord
    {
        public uint Id;
        public int RequiredTransmogHoliday;
    }

    public sealed class TraitCondRecord
    {
        public uint Id;
        private int _condType;
        public uint TraitTreeID;
        public int GrantedRanks;
        public int QuestID;
        public int AchievementID;
        public int SpecSetID;
        public int TraitNodeGroupID;
        public int TraitNodeID;
        public int TraitCurrencyID;
        public int SpentAmountRequired;
        public int Flags;
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
        public uint Id;
        public int Amount;
        public int TraitCurrencyID;
    }

    public sealed class TraitCurrencyRecord
    {
        public uint Id;
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
        public uint Id;
        public uint TraitCurrencyID;
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
        public uint Id;
        public int SpellID;
        public int OverrideIcon;
        public int OverridesSpellID;
        public int VisibleSpellID;
    }

    public sealed class TraitDefinitionEffectPointsRecord
    {
        public uint Id;
        public uint TraitDefinitionID;
        public int EffectIndex;
        private int _operationType;
        public int CurveID;

        #region Properties
        public TraitPointsOperationType OperationType => (TraitPointsOperationType)_operationType;
        #endregion
    }

    public sealed class TraitEdgeRecord
    {
        public uint Id;
        public int VisualStyle;
        public uint LeftTraitNodeID;
        public int RightTraitNodeID;
        public int Type;
    }

    public sealed class TraitNodeRecord
    {
        public uint Id;
        public uint TraitTreeID;
        public int PosX;
        public int PosY;
        private byte _type;
        public int Flags;

        #region Properties
        public TraitNodeType NodeType => (TraitNodeType)_type;
        #endregion
    }

    public sealed class TraitNodeEntryRecord
    {
        public uint Id;
        public int TraitDefinitionID;
        public int MaxRanks;
        private byte _nodeEntryType;

        #region Properties
        public TraitNodeEntryType NodeEntryType => (TraitNodeEntryType)_nodeEntryType;
        #endregion
    }

    public sealed class TraitNodeEntryXTraitCondRecord
    {
        public uint Id;
        public int TraitCondID;
        public uint TraitNodeEntryID;
    }

    public sealed class TraitNodeEntryXTraitCostRecord
    {
        public uint Id;
        public uint TraitNodeEntryID;
        public int TraitCostID;
    }

    public sealed class TraitNodeGroupRecord
    {
        public uint Id;
        public uint TraitTreeID;
        public int Flags;
    }

    public sealed class TraitNodeGroupXTraitCondRecord
    {
        public uint Id;
        public int TraitCondID;
        public uint TraitNodeGroupID;
    }

    public sealed class TraitNodeGroupXTraitCostRecord
    {
        public uint Id;
        public uint TraitNodeGroupID;
        public int TraitCostID;
    }

    public sealed class TraitNodeGroupXTraitNodeRecord
    {
        public uint Id;
        public uint TraitNodeGroupID;
        public int TraitNodeID;
        public int Index;
    }

    public sealed class TraitNodeXTraitCondRecord
    {
        public uint Id;
        public int TraitCondID;
        public uint TraitNodeID;
    }

    public sealed class TraitNodeXTraitCostRecord
    {
        public uint Id;
        public uint TraitNodeID;
        public int TraitCostID;
    }

    public sealed class TraitNodeXTraitNodeEntryRecord
    {
        public uint Id;
        public uint TraitNodeID;
        public int TraitNodeEntryID;
        public int Index;
    }

    public sealed class TraitTreeRecord
    {
        public uint Id;
        public uint TraitSystemID;
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
        public uint Id;
        public uint TraitTreeID;
        public int ChrSpecializationID;
    }

    public sealed class TraitTreeLoadoutEntryRecord
    {
        public uint Id;
        public uint TraitTreeLoadoutID;
        public int SelectedTraitNodeID;
        public int SelectedTraitNodeEntryID;
        public int NumPoints;
        public int OrderIndex;
    }

    public sealed class TraitTreeXTraitCostRecord
    {
        public uint Id;
        public uint TraitTreeID;
        public int TraitCostID;
    }

    public sealed class TraitTreeXTraitCurrencyRecord
    {
        public uint Id;
        public int Index;
        public uint TraitTreeID;
        public int TraitCurrencyID;
    }

    public sealed class TransmogSetRecord
    {
        public LocalizedString Name;
        public uint Id;
        public int ClassMask;
        public uint TrackingQuestID;
        public int Flags;
        public uint TransmogSetGroupID;
        public int ItemNameDescriptionID;
        public ushort ParentTransmogSetID;
        public byte ExpansionID;
        public short UiOrder;
    }

    public sealed class TransmogSetGroupRecord
    {
        public LocalizedString Name;
        public uint Id;        
    }

    public sealed class TransmogSetItemRecord
    {
        public uint Id;
        public uint TransmogSetID;
        public uint ItemModifiedAppearanceID;
        public int Flags;
    }

    public sealed class TransportAnimationRecord
    {
        public uint Id;
        public Vector3 Pos;
        public byte SequenceID;
        public uint TimeIndex;
        public uint TransportID;
    }

    public sealed class TransportRotationRecord
    {
        public uint Id;
        public float[] Rot = new float[4];
        public uint TimeIndex;
        public uint GameObjectsID;
    }
}