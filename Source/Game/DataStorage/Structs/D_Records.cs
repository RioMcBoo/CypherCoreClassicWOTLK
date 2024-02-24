// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using static Game.AI.SmartAction;

namespace Game.DataStorage
{
    public sealed class DestructibleModelDataRecord
    {
        public uint Id;
        public sbyte State0ImpactEffectDoodadSet;
        public byte State0AmbientDoodadSet;
        public uint State1Wmo;
        public sbyte State1DestructionDoodadSet;
        public sbyte State1ImpactEffectDoodadSet;
        public byte State1AmbientDoodadSet;
        public uint State2Wmo;
        public sbyte State2DestructionDoodadSet;
        public sbyte State2ImpactEffectDoodadSet;
        public byte State2AmbientDoodadSet;
        public uint State3Wmo;
        public byte State3InitDoodadSet;
        public byte State3AmbientDoodadSet;
        public byte EjectDirection;
        public byte DoNotHighlight;
        public uint State0Wmo;
        public byte HealEffect;
        public ushort HealEffectSpeed;
        public sbyte State0NameSet;
        public sbyte State1NameSet;
        public sbyte State2NameSet;
        public sbyte State3NameSet;
    }

    public sealed class DifficultyRecord
    {
        private int _id;
        public LocalizedString Name;
        private byte _instanceType;
        public byte OrderIndex;
        public sbyte OldEnumValue;
        private byte _fallbackDifficultyID;
        public byte MinPlayers;
        public byte MaxPlayers;
        private byte _flags;
        public byte ItemContext;
        public byte ToggleDifficultyID;
        public ushort GroupSizeHealthCurveID;
        public ushort GroupSizeDmgCurveID;
        public ushort GroupSizeSpellPointsCurveID;

        #region Properties
        public Difficulty Id => (Difficulty)_id;
        public MapTypes InstanceType => (MapTypes)_instanceType;
        public Difficulty FallbackDifficultyID => (Difficulty)_fallbackDifficultyID;
        public DifficultyFlags Flags => (DifficultyFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(DifficultyFlags flag)
        {
            return _flags.HasFlag((byte)flag);
        }

        public bool HasAnyFlag(DifficultyFlags flag)
        {
            return _flags.HasAnyFlag((byte)flag);
        }
        #endregion
    }

    public sealed class DungeonEncounterRecord
    {
        public LocalizedString Name;
        public uint Id;
        private short _mapID;
        private int _difficultyID;
        public int OrderIndex;
        public sbyte Bit;
        public int Flags;
        public int Faction;

        #region Properties
        public int MapID => _mapID;
        public Difficulty DifficultyID => (Difficulty)_difficultyID;
        #endregion
    }

    public sealed class DurabilityCostsRecord
    {
        public uint Id;
        public ushort[] WeaponSubClassCost = new ushort[21];
        public ushort[] ArmorSubClassCost = new ushort[8];
    }

    public sealed class DurabilityQualityRecord
    {
        public uint Id;
        public float Data;
    }
}
