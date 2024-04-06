// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Numerics;

namespace Game.DataStorage
{
    public sealed class UiMapRecord
    {
        public LocalizedString Name;
        public int Id;
        public int ParentUiMapID;
        private int _flags;
        private sbyte _system;
        private byte _type;
        public int BountySetID;
        public int BountyDisplayLocation;
        public int VisibilityPlayerConditionID2; // if not met then map is skipped when evaluating UiMapAssignment
        public int VisibilityPlayerConditionID;  // if not met then client checks other maps with the same AlternateUiMapGroup, not re-evaluating UiMapAssignment for them
        public sbyte HelpTextPosition;
        public int BkgAtlasID;
        public int AlternateUiMapGroup;
        public int ContentTuningID;

        #region Properties
        public UiMapFlag Flags => (UiMapFlag)_flags;
        public UiMapSystem System => (UiMapSystem)_system;
        public UiMapType Type => (UiMapType)_type;
        
        #endregion

        #region Helpers
        public bool HasFlag(UiMapFlag flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(UiMapFlag flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class UiMapAssignmentRecord
    {
        public Vector2 UiMin;
        public Vector2 UiMax;
        public Vector3[] Region = new Vector3[2];
        public int Id;
        public int UiMapID;
        public int OrderIndex;
        public int MapID;
        public int AreaID;
        public int WmoDoodadPlacementID;
        public int WmoGroupID;
    }

    public sealed class UiMapLinkRecord
    {
        public Vector2 UiMin;
        public Vector2 UiMax;
        public int Id;
        public int ParentUiMapID;
        public int OrderIndex;
        public int ChildUiMapID;
        public int PlayerConditionID;
        public int OverrideHighlightFileDataID;
        public int OverrideHighlightAtlasID;
        public int Flags;
    }

    public sealed class UiMapXMapArtRecord
    {
        public int Id;
        public int PhaseID;
        public int UiMapArtID;
        public uint UiMapID;
    }

    public sealed class UnitConditionRecord
    {
        public const int MAX_UNIT_CONDITION_VALUES = 8;

        public int Id;
        private byte _flags;
        public byte[] Variable = new byte[MAX_UNIT_CONDITION_VALUES];
        public sbyte[] Op = new sbyte[MAX_UNIT_CONDITION_VALUES];
        public int[] Value = new int[MAX_UNIT_CONDITION_VALUES];

        #region Properties
        public UnitConditionFlags Flags => (UnitConditionFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(UnitConditionFlags flag)
        {
            return _flags.HasFlag((byte)flag);
        }

        public bool HasAnyFlag(UnitConditionFlags flag)
        {
            return _flags.HasAnyFlag((byte)flag);
        }
        #endregion
    }

    public sealed class UnitPowerBarRecord
    {
        public int Id;
        public LocalizedString Name;
        public LocalizedString Cost;
        public LocalizedString OutOfError;
        public LocalizedString ToolTip;
        public uint MinPower;
        public uint MaxPower;
        public ushort StartPower;
        public byte CenterPower;
        public float RegenerationPeace;
        public float RegenerationCombat;
        public byte BarType;
        public ushort Flags;
        public float StartInset;
        public float EndInset;
        public int[] FileDataID = new int[6];
        public int[] Color = new int[6];
    }
}
