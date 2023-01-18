// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Numerics;

namespace Game.DataStorage
{
    public sealed class TactKeyRecord
    {
        public uint Id;
        public byte[] Key = new byte[16];
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
        public TaxiNodeFlags Flags;
        public int UiTextureKitID;
        public float Facing;
        public uint SpecialIconConditionID;
        public uint VisibilityConditionID;
        public uint[] MountCreatureID = new uint[2];
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
        public TaxiPathNodeFlags Flags;
        public uint Delay;
        public uint ArrivalEventID;
        public uint DepartureEventID;
    }

    public sealed class TotemCategoryRecord
    {
        public uint Id;
        public string Name;
        public byte TotemCategoryType;
        public int TotemCategoryMask;
    }

    public sealed class ToyRecord
    {
        public string SourceText;
        public uint Id;
        public uint ItemID;
        public byte Flags;
        public sbyte SourceTypeEnum;
    }

    public sealed class TransmogHolidayRecord
    {
        public uint Id;
        public int RequiredTransmogHoliday;
    }

    public sealed class TransmogSetRecord
    {
        public string Name;
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
