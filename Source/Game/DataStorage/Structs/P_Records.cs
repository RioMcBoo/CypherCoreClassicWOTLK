// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;

namespace Game.DataStorage
{
    public sealed class ParagonReputationRecord
    {
        public int Id;
        public int FactionID;
        public int LevelThreshold;
        public int QuestID;
    }

    public sealed class PhaseRecord
    {
        public int Id;
        private ushort _flags;

        #region Properties
        public PhaseEntryFlags Flags => (PhaseEntryFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(PhaseEntryFlags flag)
        {
            return _flags.HasFlag((ushort)flag);
        }

        public bool HasAnyFlag(PhaseEntryFlags flag)
        {
            return _flags.HasAnyFlag((ushort)flag);
        }
        #endregion
    }

    public sealed class PhaseXPhaseGroupRecord
    {
        public int Id;
        public ushort PhaseId;
        public int PhaseGroupID;
    }

    public sealed class PlayerConditionRecord
    {        
        private long _raceMask;
        public LocalizedString FailureDescription;
        public int Id;
        public ushort MinLevel;
        public ushort MaxLevel;
        private int _classMask;
        public uint SkillLogic;
        public byte LanguageID;
        public byte MinLanguage;
        public int MaxLanguage;
        public ushort MaxFactionID;
        public byte MaxReputation;
        public uint ReputationLogic;
        public sbyte CurrentPvpFaction;
        public byte PvpMedal;
        public uint PrevQuestLogic;
        public uint CurrQuestLogic;
        public uint CurrentCompletedQuestLogic;
        public uint SpellLogic;
        public uint ItemLogic;
        public byte ItemFlags;
        public uint AuraSpellLogic;
        public ushort WorldStateExpressionID;
        public byte WeatherID;
        public byte PartyStatus;
        public byte LifetimeMaxPVPRank;
        public uint AchievementLogic;
        private sbyte _gender;
        private sbyte _nativeGender;
        public uint AreaLogic;
        public uint LfgLogic;
        public uint CurrencyLogic;
        public int QuestKillID;
        public uint QuestKillLogic;
        public sbyte MinExpansionLevel;
        public sbyte MaxExpansionLevel;
        public int MinAvgItemLevel;
        public int MaxAvgItemLevel;
        public ushort MinAvgEquippedItemLevel;
        public ushort MaxAvgEquippedItemLevel;
        public byte PhaseUseFlags;
        public ushort PhaseID;
        public int PhaseGroupID;
        public byte Flags;
        public sbyte ChrSpecializationIndex;
        private sbyte _chrSpecializationRole;
        public int ModifierTreeID;
        private sbyte _powerType;
        public byte PowerTypeComp;
        public byte PowerTypeValue;
        private int _weaponSubclassMask;
        public byte MaxGuildLevel;
        public byte MinGuildLevel;
        public sbyte MaxExpansionTier;
        public sbyte MinExpansionTier;
        public byte MinPVPRank;
        public byte MaxPVPRank;
        public ushort[] SkillID = new ushort[4];
        public ushort[] MinSkill = new ushort[4];
        public ushort[] MaxSkill = new ushort[4];
        public int[] MinFactionID = new int[3];
        public byte[] MinReputation = new byte[3];
        public int[] PrevQuestID = new int[4];
        public int[] CurrQuestID = new int[4];
        public int[] CurrentCompletedQuestID = new int[4];
        public int[] SpellID = new int[4];
        public int[] ItemID = new int[4];
        public uint[] ItemCount = new uint[4];
        public ushort[] Explored = new ushort[2];
        private int[] _time = new int[2];
        public int[] AuraSpellID = new int[4];
        public byte[] AuraStacks = new byte[4];
        public ushort[] Achievement = new ushort[4];
        public ushort[] AreaID = new ushort[4];
        public byte[] LfgStatus = new byte[4];
        public byte[] LfgCompare = new byte[4];
        public uint[] LfgValue = new uint[4];
        public int[] CurrencyID = new int[4];
        public uint[] CurrencyCount = new uint[4];
        public uint[] QuestKillMonster = new uint[6];
        public int[] MovementFlags = new int[2];

        #region Properties
        public RaceMask RaceMask => (RaceMask)_raceMask;
        public ClassMask ClassMask => (ClassMask)_classMask;
        public Gender Gender => (Gender)_gender;
        public Gender NativeGender => (Gender)_nativeGender;
        public PowerType PowerType => (PowerType)_powerType;
        public ChrSpecializationRole ChrSpecializationRole => (ChrSpecializationRole)_chrSpecializationRole;
        public ItemSubClassWeaponMask WeaponSubclassMask => (ItemSubClassWeaponMask)_weaponSubclassMask;
        public WowTime Time(int index) => (WowTime)_time[index];
        #endregion
    }

    public sealed class PowerDisplayRecord
    {
        public int Id;
        public string GlobalStringBaseTag;
        public byte ActualType;
        public byte Red;
        public byte Green;
        public byte Blue;
    }

    public sealed class PowerTypeRecord
    {
        public int Id;
        public string NameGlobalStringTag;
        public string CostGlobalStringTag;        
        private sbyte _powerTypeEnum;
        public int MinPower;
        public int MaxBasePower;
        public int CenterPower;
        public int DefaultPower;
        public int DisplayModifier;
        public int _regenInterruptTimeMS;
        public float RegenPeace;
        public float RegenCombat;
        private short _flags;

        #region Properties
        public PowerTypeFlags Flags => (PowerTypeFlags)_flags;
        public PowerType PowerTypeEnum => (PowerType)_powerTypeEnum;
        public Milliseconds RegenInterruptTime => (Milliseconds)_regenInterruptTimeMS;
        #endregion

        #region Helpers
        public bool HasFlag(PowerTypeFlags flag)
        {
            return _flags.HasFlag((short)flag);
        }

        public bool HasAnyFlag(PowerTypeFlags flag)
        {
            return _flags.HasAnyFlag((short)flag);
        }
        #endregion
    }

    public sealed class PrestigeLevelInfoRecord
    {
        public int Id;
        public LocalizedString Name;
        public int PrestigeLevel;
        public int BadgeTextureFileDataID;
        private byte _flags;
        public int AwardedAchievementID;

        #region Properties
        public PrestigeLevelInfoFlags Flags => (PrestigeLevelInfoFlags)_flags;
        #endregion

        #region Helpers
        public bool HasFlag(PrestigeLevelInfoFlags flag)
        {
            return _flags.HasFlag((byte)flag);
        }

        public bool HasAnyFlag(PrestigeLevelInfoFlags flag)
        {
            return _flags.HasAnyFlag((byte)flag);
        }

        public bool IsDisabled => HasFlag(PrestigeLevelInfoFlags.Disabled);
        #endregion
    }

    public sealed class PvpDifficultyRecord
    {
        public int Id;
        private byte RangeIndex;
        public byte MinLevel;
        public byte MaxLevel;
        public int MapID;

        #region Helpers
        public BattlegroundBracketId BracketId => (BattlegroundBracketId)RangeIndex;
        #endregion
    }

    public sealed class PvpItemRecord
    {
        public int Id;
        public int ItemID;
        public byte ItemLevelDelta;
    }

    public sealed class PvpSeasonRecord
    {
        public int Id;
        public int MilestoneSeason;
        public int AllianceAchievementID;
        public int HordeAchievementID;
    }

    public sealed class PvpTalentRecord
    {
        public LocalizedString Description;
        public int Id;
        public int SpecID;
        public int SpellID;
        public int OverridesSpellID;
        public int Flags;
        public int ActionBarSpellID;
        public int PvpTalentCategoryID;
        public int LevelRequired;
    }

    public sealed class PvpTalentCategoryRecord
    {
        public int Id;
        public byte TalentSlotMask;
    }

    public sealed class PvpTalentSlotUnlockRecord
    {
        public int Id;
        public sbyte Slot;
        public int LevelRequired;
        public int DeathKnightLevelRequired;
        public int DemonHunterLevelRequired;
    }

    public sealed class PvpTierRecord
    {
        public int Id;
        public LocalizedString Name;        
        public short MinRating;
        public short MaxRating;
        public int PrevTier;
        public int NextTier;
        public byte BracketID;
        public sbyte Rank;
        public int RankIconFileDataID;
    }
}
