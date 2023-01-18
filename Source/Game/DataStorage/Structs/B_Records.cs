// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.
using Framework.Constants;
using System;

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
        public string DisplayName;
        public string Description;
        public uint Id;
        public byte Type;                                                     // value 0 . hair, value 2 . facialhair
        public float CostModifier;
        public byte Race;
        public byte Sex;
        public byte Data;                                                     // real ID to hair/facial hair
    }

    public sealed class BattlePetBreedQualityRecord
    {
        public uint Id;
        public float StateMultiplier;
        public sbyte QualityEnum;
    }

    public sealed class BattlePetBreedStateRecord
    {
        public uint Id;
        public byte BattlePetStateID;
        public ushort Value;
        public int BattlePetBreedID;
    }

    public sealed class BattlePetSpeciesRecord
    {
        public string Description;
        public string SourceText;
        public uint Id;
        public uint CreatureID;
        public uint SummonSpellID;
        public int IconFileDataID;
        public byte PetTypeEnum;
        public ushort Flags;
        public sbyte SourceTypeEnum;
        public int CardUIModelSceneID;
        public int LoadoutUIModelSceneID;

        public BattlePetSpeciesFlags GetFlags() { return (BattlePetSpeciesFlags)Flags; }
}

    public sealed class BattlePetSpeciesStateRecord
    {
        public uint Id;
        public byte BattlePetStateID;
        public int Value;
        public int BattlePetSpeciesID;
    }

    public sealed class BattlemasterListRecord
    {
        public uint Id;
        public LocalizedString Name;
        public LocalizedString GameType;
        public LocalizedString ShortDescription;
        public LocalizedString LongDescription;
        public sbyte InstanceType;
        public byte MinLevel;
        public byte MaxLevel;
        public sbyte RatedPlayers;
        public byte MinPlayers;
        public int MaxPlayers;
        public sbyte GroupsAllowed;
        public sbyte MaxGroupSize;
        public ushort HolidayWorldState;
        public BattlemasterListFlags Flags;
        public int IconFileDataID;
        public int RequiredPlayerConditionID;
        public short[] MapID = new short[16];
    }

    public sealed class BroadcastTextRecord
    {
        public LocalizedString Text;
        public LocalizedString Text1;
        public uint Id;
        public int LanguageID;
        public int ConditionID;
        public ushort EmotesID;
        public byte Flags;
        public uint ChatBubbleDurationMs;
        public int VoiceOverPriorityID;
        public uint[] SoundKitID = new uint[2];
        public ushort[] EmoteID = new ushort[3];
        public ushort[] EmoteDelay = new ushort[3];
    }
}
