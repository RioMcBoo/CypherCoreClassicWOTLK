// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System;
using static Game.AI.SmartAction;

namespace Game.DataStorage
{
    public sealed class BankBagSlotPricesRecord
    {
        public uint Id;
        public uint Cost;
    }

    public sealed class BannedAddonsRecord
    {
        public uint Id;
        public string Name;
        public string Version;
        public byte Flags;
    }

    public sealed class BarberShopStyleRecord
    {        
        public LocalizedString DisplayName;
        public LocalizedString Description;
        public uint Id;
        /// <summary>
        /// value 0 . hair, value 2 . facialhair
        /// </summary>
        public byte Type;
        public float CostModifier;
        public byte Race;
        public byte Sex;
        /// <summary>
        /// real ID to hair/facial hair
        /// </summary>
        public byte Data;
    }

    public sealed class BattlePetAbilityRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString Description;
        public int IconFileDataId;
        public sbyte PetTypeEnum;
        public uint Cooldown;
        public ushort BattlePetVisualID;
        public byte Flags;
    }

    public sealed class BattlePetBreedQualityRecord
    {
        public uint Id;
        public float StateMultiplier;
        public byte QualityEnum;
    }

    public sealed class BattlePetBreedStateRecord
    {
        public uint Id;
        public byte BattlePetStateID;
        public ushort Value;
        public uint BattlePetBreedID;
    }

    public sealed class BattlePetSpeciesRecord
    {
        public LocalizedString Description;
        public LocalizedString SourceText;
        public uint Id;
        public int CreatureID;
        public int SummonSpellID;
        public int IconFileDataID;
        public byte PetTypeEnum;
        private int _flags;
        public sbyte SourceTypeEnum;
        public int CardUIModelSceneID;
        public int LoadoutUIModelSceneID;

        #region Properties
        public BattlePetSpeciesFlags Flags => (BattlePetSpeciesFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(BattlePetSpeciesFlags flag)
        {
            return _flags.HasFlag((int)flag);
        }

        public bool HasAnyFlag(BattlePetSpeciesFlags flag)
        {
            return _flags.HasAnyFlag((int)flag);
        }
        #endregion
    }

    public sealed class BattlePetSpeciesStateRecord
    {
        public uint Id;
        public byte BattlePetStateID;
        public int Value;
        public uint BattlePetSpeciesID;
    }

    public sealed class BattlemasterListRecord
    {
        public int Id;
        public LocalizedString Name;
        public LocalizedString GameType;
        public LocalizedString ShortDescription;
        public LocalizedString LongDescription;
        public sbyte InstanceType;
        public byte MinLevel;
        public byte MaxLevel;
        public sbyte RatedPlayers;
        private byte _minPlayers;
        private int _maxPlayers;
        public sbyte GroupsAllowed;
        public sbyte MaxGroupSize;
        public short HolidayWorldState;
        private sbyte _flags;
        public int IconFileDataID;
        public int RequiredPlayerConditionID;
        public short[] MapId = new short[16];

        #region Properties
        public BattlemasterListFlags Flags => (BattlemasterListFlags)_flags;
        public byte MinPlayers { get => _minPlayers; internal set => _minPlayers = value; }
        public byte MaxPlayers { get => (byte)_maxPlayers; internal set => _maxPlayers = value; }
        #endregion

        #region Helpers
        public bool HasFlag(BattlemasterListFlags flag)
        {
            return _flags.HasFlag((sbyte)flag);
        }

        public bool HasAnyFlag(BattlemasterListFlags flag)
        {
            return _flags.HasAnyFlag((sbyte)flag);
        }
        #endregion
    }

    public sealed class BroadcastTextRecord
    {
        static int MAX_BROADCAST_TEXT_EMOTES = 3;

        public LocalizedString Text;
        public LocalizedString Text1;
        public uint Id;
        public int LanguageID;
        public int ConditionID;
        public ushort EmotesID;
        public byte Flags;
        public uint ChatBubbleDurationMs;
        public int VoiceOverPriorityID;
        public int[] SoundKitID = new int[2];
        public ushort[] EmoteID = new ushort[MAX_BROADCAST_TEXT_EMOTES];
        public ushort[] EmoteDelay = new ushort[MAX_BROADCAST_TEXT_EMOTES];
    }
}
