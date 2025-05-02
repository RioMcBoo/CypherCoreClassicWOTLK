﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Achievements;
using Game.BattleGrounds;
using Game.Chat;
using Game.DataStorage;
using Game.Groups;
using Game.Loots;
using Game.Mails;
using Game.Misc;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.Entities
{
    public partial class Player
    {
        public WorldSession GetSession() { return _session; }
        public PlayerSocial GetSocial() { return m_social; }

        //Gossip
        public PlayerMenu PlayerTalkClass;
        PlayerSocial m_social;
        List<Channel> m_channels = new();
        List<ObjectGuid> WhisperList = new();
        public string autoReplyMsg;

        //Inventory
        Dictionary<long, EquipmentSetInfo> _equipmentSets = new();
        public List<ItemSetEffect> ItemSetEff = new();
        List<EnchantDuration> m_enchantDuration = new();
        List<Item> m_itemDuration = new();
        List<ObjectGuid> m_itemSoulboundTradeable = new();
        List<ObjectGuid> m_refundableItems = new();
        public List<Item> ItemUpdateQueue = new();
        VoidStorageItem[] _voidStorageItems = new VoidStorageItem[SharedConst.VoidStorageMaxSlot];
        Item[] m_items = new Item[(int)PlayerSlots.Count];
        ItemSubClassWeaponMask m_WeaponProficiency;
        ItemSubClassArmorMask m_ArmorProficiency;
        byte m_currentBuybackSlot;
        TradeData m_trade;

        //PVP
        BgBattlegroundQueueID_Rec[] m_bgBattlegroundQueueID = new BgBattlegroundQueueID_Rec[SharedConst.MaxPlayerBGQueues];
        public BGData m_bgData;
        bool m_IsBGRandomWinner;
        public PvPInfo pvpInfo;
        int m_ArenaTeamIdInvited;
        RealmTime m_lastHonorUpdateTime;
        Milliseconds m_contestedPvPTimer;
        bool _usePvpItemLevels;
        ObjectGuid _areaSpiritHealerGUID;

        //Groups/Raids
        GroupReference m_group = new();
        GroupReference m_originalGroup = new();
        Group m_groupInvite;
        GroupUpdateFlags m_groupUpdateMask;
        bool m_bPassOnGroupLoot;
        GroupUpdateCounter[] m_groupUpdateSequences = new GroupUpdateCounter[2];

        Dictionary<int, int> m_recentInstances = new();
        Dictionary<int, ServerTime> _instanceResetTimes = new();
        int _pendingBindId;
        Milliseconds _pendingBindTimer;
        public bool m_InstanceValid;

        Difficulty m_dungeonDifficulty;
        Difficulty m_raidDifficulty;
        Difficulty m_legacyRaidDifficulty;

        //Movement
        public PlayerTaxi m_taxi = new(CliDB.GetTaxiMaskSize());
        public byte[] m_forced_speed_changes = new byte[(int)UnitMoveType.Max];
        public byte m_movementForceModMagnitudeChanges;
        uint m_lastFallTime;
        float m_lastFallZ;
        WorldLocation teleportDest;
        int? m_teleport_instanceId;
        TeleportToOptions m_teleport_options;
        bool mSemaphoreTeleport_Near;
        bool mSemaphoreTeleport_Far;
        PlayerDelayedOperations m_DelayedOperations;
        bool m_bCanDelayTeleport;
        bool m_bHasDelayedTeleport;

        PlayerUnderwaterState m_MirrorTimerFlags;
        PlayerUnderwaterState m_MirrorTimerFlagsLast;

        //Stats
        int m_baseSpellPower;
        int m_baseFeralAP;
        int m_baseManaRegen;
        int m_baseHealthRegen;
        int m_spellPenetrationItemMod;
        int m_lastPotionId;
        int m_questRewardedTalentPoints;

        //Spell
        public SpellBook SpellBook;
        Dictionary<SkillType, SkillStatusData> mSkillStatus = new();
        Dictionary<int, PlayerCurrency> _currencyStorage = new();
        List<SpellModifier>[][] m_spellMods = new List<SpellModifier>[(int)SpellModOp.Max][];
        MultiMap<int, int> m_overrideSpells = new();
        public Spell m_spellModTakingSpell;
        int m_oldpetspell;
        Dictionary<int, StoredAuraTeleportLocation> m_storedAuraTeleportLocations = new();
        SpellCastRequest _pendingSpellCastRequest;

        //Mail
        List<Mail> m_mail = new();
        Dictionary<long, Item> mMitems = new();
        public byte unReadMails;
        ServerTime m_nextMailDelivereTime;
        public bool m_mailsUpdated;

        //Pets
        PetStable m_petStable;
        public List<PetAura> m_petAuras = new();
        int m_temporaryUnsummonedPetNumber;
        ReactStates? m_temporaryPetReactState;
        int m_lastpetnumber;

        // Player summoning
        ServerTime m_summon_expire;
        WorldLocation m_summon_location;
        int m_summon_instanceId;

        RestMgr _restMgr;

        //Combat
        int[] baseRatingValue = new int[(int)CombatRating.Max];
        float[] m_auraBaseFlatMod = new float[(int)BaseModGroup.End];
        float[] m_auraBasePctMod = new float[(int)BaseModGroup.End];
        public float AmmoDPS { get; set; }
        public DuelInfo duel;
        bool m_canParry;
        bool m_canBlock;
        bool m_canTitanGrip;
        int m_titanGripPenaltySpellId;
        Milliseconds m_deathTimer;
        ServerTime m_deathExpireTime;
        AttackSwingErr? m_swingErrorMsg;
        ServerTime m_regenInterruptTimestamp;
        Milliseconds m_regenTimerCount;
        Milliseconds m_foodEmoteTimerCount;
        Milliseconds m_weaponChangeTimer;

        //Quest
        List<int> m_timedquests = new();
        List<int> m_weeklyquests = new();
        List<int> m_monthlyquests = new();
        Dictionary<int, Dictionary<int, RealmTime>> m_seasonalquests = new();
        Dictionary<int, QuestStatusData> m_QuestStatus = new();
        MultiMap<(QuestObjectiveType Type, int ObjectID), QuestObjectiveStatusData> m_questObjectiveStatus = new();
        Dictionary<int, QuestSaveType> m_QuestStatusSave = new();
        List<int> m_DFQuests = new();
        List<int> m_RewardedQuests = new();
        Dictionary<int, QuestSaveType> m_RewardedQuestsSave = new();

        bool m_DailyQuestChanged;
        bool m_WeeklyQuestChanged;
        bool m_MonthlyQuestChanged;
        bool m_SeasonalQuestChanged;
        ServerTime m_lastDailyQuestTime;

        CinematicManager _cinematicMgr;

        // variables to save health and mana before duel and restore them after duel
        long healthBeforeDuel;
        int manaBeforeDuel;

        bool _advancedCombatLoggingEnabled;

        WorldLocation _corpseLocation;

        //Core
        WorldSession _session;

        public PlayerData m_playerData;
        public ActivePlayerData m_activePlayerData;

        ServerTime m_createTime;
        PlayerCreateMode m_createMode;

        Milliseconds m_nextSave;
        byte m_cinematic;

        int m_movie;
        bool m_customizationsChanged;

        SpecializationInfo _specializationInfo;
        public List<ObjectGuid> m_clientGUIDs = new();
        public List<ObjectGuid> m_visibleTransports = new();
        public WorldObject seerView;
        Team m_team;
        ReputationMgr reputationMgr;
        QuestObjectiveCriteriaManager m_questObjectiveCriteriaMgr;
        public AtLoginFlags atLoginFlags;
        public bool m_itemUpdateQueueBlocked;

        PlayerExtraFlags m_ExtraFlags;

        public bool IsDebugAreaTriggers { get; set; }
        int m_zoneUpdateId;
        int m_areaUpdateId;
        Milliseconds m_zoneUpdateTimer;

        int m_ChampioningFaction;
        byte m_fishingSteps;

        // Recall position
        WorldLocation m_recall_location;
        int m_recall_instanceId;
        WorldLocation homebind = new();
        int homebindAreaId;
        Milliseconds m_HomebindTimer;

        ResurrectionData _resurrectionData;

        PlayerAchievementMgr m_achievementSys;

        SceneMgr m_sceneMgr;

        Dictionary<ObjectGuid, Loot> m_AELootView = new();
        List<LootRoll> m_lootRolls = new();                                     // loot rolls waiting for answer

        CUFProfile[] _CUFProfiles = new CUFProfile[PlayerConst.MaxCUFProfiles];
        float[] m_powerFraction = new float[(int)PowerType.MaxPerClass];
        Milliseconds[] m_MirrorTimer = new Milliseconds[3];

        TimeTracker m_groupUpdateTimer;

        long m_GuildIdInvited;
        DeclinedName _declinedname;
        public Runes Runes { get; protected set; }
        Milliseconds m_hostileReferenceCheckTimer;
        Milliseconds m_drunkTimer;
        ServerTime m_logintime;
        ServerTime m_Last_tick;
        Seconds m_PlayedTimeTotal;
        Seconds m_PlayedTimeLevel;

        Dictionary<int, PlayerSpellState> m_traitConfigStates = new();

        Dictionary<byte, ActionButton> m_actionButtons = new();
        ObjectGuid m_playerSharingQuest;
        int m_sharedQuestId;
        RelativeTime m_ingametime;

        PlayerCommandStates _activeCheats;

        class ValuesUpdateForPlayerWithMaskSender : IDoWork<Player>
        {
            Player Owner;
            ObjectFieldData ObjectMask = new();
            UnitData UnitMask = new();
            PlayerData PlayerMask = new();
            ActivePlayerData ActivePlayerMask = new();

            public ValuesUpdateForPlayerWithMaskSender(Player owner)
            {
                Owner = owner;
            }

            public void Invoke(Player player)
            {
                UpdateData udata = new(Owner.GetMapId());

                Owner.BuildValuesUpdateForPlayerWithMask(udata, ObjectMask.GetUpdateMask(), UnitMask.GetUpdateMask(), PlayerMask.GetUpdateMask(), ActivePlayerMask.GetUpdateMask(), player);

                udata.BuildPacket(out UpdateObject packet);
                player.SendPacket(packet);
            }
        }
    }

    public class PlayerInfo
    {
        public CreatePosition createPosition;
        public CreatePosition? createPositionNPE;

        public ItemContext itemContext;
        public List<PlayerCreateInfoItem> item = new();
        public List<int> customSpells = new();
        public List<int>[] castSpells = new List<int>[(int)PlayerCreateMode.Max];
        public List<PlayerCreateInfoAction> action = new();
        public List<SkillRaceClassInfoRecord> skills = new();

        public int? introMovieId;
        public int? introSceneId;
        public int? introSceneIdNPE;

        public PlayerLevelInfo[] levelInfo = new PlayerLevelInfo[WorldConfig.Values[WorldCfg.MaxPlayerLevel].Int32];

        public PlayerInfo()
        {
            for (var i = 0; i < castSpells.Length; ++i)
                castSpells[i] = new();

            for (var i = 0; i < levelInfo.Length; ++i)
                levelInfo[i] = new();
        }

        public struct CreatePosition
        {
            public WorldLocation Loc;
            public long? TransportGuid;
        }
    }

    public class PlayerCreateInfoItem
    {
        public PlayerCreateInfoItem(int id, int amount)
        {
            item_id = id;
            item_amount = amount;
        }

        public int item_id;
        public int item_amount;
    }

    public class PlayerCreateInfoAction
    {
        public PlayerCreateInfoAction() : this(0, 0, 0) { }
        public PlayerCreateInfoAction(byte _button, int _action, byte _type)
        {
            button = _button;
            type = _type;
            action = _action;
        }

        public byte button;
        public byte type;
        public int action;
    }

    public class PlayerLevelInfo
    {
        public int[] stats = new int[(int)Stats.Max];
        public int baseHealth;
        public int baseMana;
    }

    public class PlayerCurrency
    {
        public PlayerCurrencyState state;
        public int Quantity;
        public int WeeklyQuantity;
        public int TrackedQuantity;
        public int IncreasedCapQuantity;
        public int EarnedQuantity;
        public CurrencyDbFlags Flags;
    }

    public class SpecializationInfo
    {
        public SpecializationInfo()
        {
            for (byte i = 0; i < PlayerConst.MaxSpecializations; ++i)
            {
                Talents[i] = new Dictionary<int, PlayerTalent>();
                Glyphs[i] = new int[PlayerConst.MaxGlyphSlotIndex];
            }
        }

        public Dictionary<int, PlayerTalent>[] Talents = new Dictionary<int, PlayerTalent>[PlayerConst.MaxSpecializations];
        public int[][] Glyphs = new int[PlayerConst.MaxSpecializations][];
        public uint ResetTalentsCost;
        public ServerTime ResetTalentsTime;
        public byte ActiveGroup;
        public byte BonusGroups;
    }

    public class ActionButton
    {
        public ActionButton()
        {
            packedData = 0;
            uState = ActionButtonUpdateState.New;
        }

        public ActionButtonType GetButtonType() { return (ActionButtonType)UnitActionBarEntry.UNIT_ACTION_BUTTON_TYPE(packedData); }

        public int GetAction() { return UnitActionBarEntry.UNIT_ACTION_BUTTON_ACTION(packedData); }

        public void SetActionAndType(int action, ActionButtonType type)
        {
            int newData = UnitActionBarEntry.MAKE_UNIT_ACTION_BUTTON(action, (byte)type);
            
            if (newData != packedData || uState == ActionButtonUpdateState.Deleted)
            {
                packedData = newData;
                if (uState != ActionButtonUpdateState.New)
                    uState = ActionButtonUpdateState.Changed;
            }
        }

        public int packedData;
        public ActionButtonUpdateState uState;
    }

    public class ResurrectionData
    {
        public ObjectGuid GUID;
        public WorldLocation Location = new();
        public int Health;
        public int Mana;
        public int Aura;
    }

    public struct PvPInfo
    {
        public bool IsHostile;
        public bool IsInHostileArea;               //> Marks if player is in an area which forces PvP flag
        public bool IsInNoPvPArea;                 //> Marks if player is in a sanctuary or friendly capital city
        public bool IsInFFAPvPArea;                //> Marks if player is in an FFAPvP area (such as Gurubashi Arena)
        public ServerTime EndTimer;                //> Time when player unflags himself for PvP (flag removed after 5 minutes)
    }

    public class DuelInfo
    {
        public Player Opponent;
        public Player Initiator;
        public bool IsMounted;
        public DuelState State;
        public ServerTime StartTime;
        public ServerTime OutOfBoundsTime;

        public DuelInfo(Player opponent, Player initiator, bool isMounted)
        {
            Opponent = opponent;
            Initiator = initiator;
            IsMounted = isMounted;
        }
    }

    public class AccessRequirement
    {
        public byte levelMin;
        public byte levelMax;
        public int item;
        public int item2;
        public int quest_A;
        public int quest_H;
        public int achievement;
        public string questFailedText;
    }

    public class EnchantDuration
    {
        public EnchantDuration(Item _item = null, EnchantmentSlot _slot = EnchantmentSlot.Max, Milliseconds _leftduration = default)
        {
            item = _item;
            slot = _slot;
            leftduration = _leftduration;
        }

        public Item item;
        public EnchantmentSlot slot;
        public Milliseconds leftduration;
    }

    public class VoidStorageItem
    {
        public VoidStorageItem(long id, int entry, ObjectGuid creator, int fixedScalingLevel, ItemRandomProperties randomProperties, ItemContext context)
        {
            ItemId = id;
            ItemEntry = entry;
            CreatorGuid = creator;
            FixedScalingLevel = fixedScalingLevel;
            RandomProperties = randomProperties;
            Context = context;
        }

        public long ItemId;
        public int ItemEntry;
        public ObjectGuid CreatorGuid;
        public int FixedScalingLevel;
        public ItemRandomProperties RandomProperties;
        public ItemContext Context;
    }

    public class EquipmentSetInfo
    {
        public EquipmentSetInfo()
        {
            state = EquipmentSetUpdateState.New;
            Data = new EquipmentSetData();
        }

        public EquipmentSetUpdateState state;
        public EquipmentSetData Data;

        // Data sent in EquipmentSet related packets
        public class EquipmentSetData
        {
            public EquipmentSetType Type;
            public long Guid; // Set Identifier
            public int SetID; // Index
            public uint IgnoreMask ; // Mask of EquipmentSlot
            public int AssignedSpecIndex = -1; // Index of character specialization that this set is automatically equipped for
            public string SetName = "";
            public string SetIcon = "";
            public ObjectGuid[] Pieces = new ObjectGuid[EquipmentSlot.End];
            public int[] Appearances = new int[EquipmentSlot.End];  // ItemModifiedAppearanceID
            public int[] Enchants = new int[2];  // SpellItemEnchantmentID
            public int SecondaryShoulderApparanceID; // Secondary shoulder appearance
            public int SecondaryShoulderSlot; // Always 2 if secondary shoulder apperance is used
            public int SecondaryWeaponAppearanceID; // For legion artifacts: linked child item appearance
            public int SecondaryWeaponSlot; // For legion artifacts: which slot is used by child item
        }

        public enum EquipmentSetType
        {
            Equipment = 0,
            Transmog = 1
        }
    }

    public class BgBattlegroundQueueID_Rec
    {
        public BattlegroundQueueTypeId bgQueueTypeId;
        public int invitedToInstance;
        public ServerTime joinTime;
        public bool mercenary;
    }

    // Holder for Battlegrounddata
    public class BGData
    {
        public BGData()
        {
            bgTypeID = BattlegroundTypeId.None;
            ClearTaxiPath();
            joinPos = new WorldLocation();
        }

        public int bgInstanceID;                    //< This variable is set to bg.m_InstanceID,
        //  when player is teleported to BG - (it is Battleground's GUID)
        public BattlegroundTypeId bgTypeID;

        public List<ObjectGuid> bgAfkReporter = new();
        public byte bgAfkReportedCount;
        public ServerTime bgAfkReportedTimer;

        public Team bgTeam;                          //< What side the player will be added to

        public int mountSpell;
        public int[] taxiPath = new int[2];

        public WorldLocation joinPos;                  //< From where player entered BG
        public BattlegroundQueueTypeId queueId;

        public void ClearTaxiPath() { taxiPath[0] = taxiPath[1] = 0; }
        public bool HasTaxiPath() { return taxiPath[0] != 0 && taxiPath[1] != 0; }
    }

    public class CUFProfile
    {
        public CUFProfile()
        {
            BoolOptions = new BitSet((int)CUFBoolOptions.BoolOptionsCount);
        }

        public CUFProfile(string name, ushort frameHeight, ushort frameWidth, byte sortBy, byte healthText, uint boolOptions,
            byte topPoint, byte bottomPoint, byte leftPoint, ushort topOffset, ushort bottomOffset, ushort leftOffset)
        {
            ProfileName = name;
            BoolOptions = new BitSet(new uint[] { boolOptions });

            FrameHeight = frameHeight;
            FrameWidth = frameWidth;
            SortBy = sortBy;
            HealthText = healthText;
            TopPoint = topPoint;
            BottomPoint = bottomPoint;
            LeftPoint = leftPoint;
            TopOffset = topOffset;
            BottomOffset = bottomOffset;
            LeftOffset = leftOffset;
        }

        public void SetOption(CUFBoolOptions opt, byte arg)
        {
            BoolOptions.Set((int)opt, arg != 0);
        }
        public bool GetOption(CUFBoolOptions opt)
        {
            return BoolOptions.Get((int)opt);
        }
        public ulong GetUlongOptionValue()
        {
            uint[] array = new uint[1];
            BoolOptions.CopyTo(array, 0);
            return array[0];
        }

        public string ProfileName;
        public ushort FrameHeight;
        public ushort FrameWidth;
        public byte SortBy;
        public byte HealthText;

        // LeftAlign, TopAlight, BottomAlign
        public byte TopPoint;
        public byte BottomPoint;
        public byte LeftPoint;

        // LeftOffset, TopOffset and BottomOffset
        public ushort TopOffset;
        public ushort BottomOffset;
        public ushort LeftOffset;

        public BitSet BoolOptions;

        // More fields can be added to BoolOptions without changing DB schema (up to 32, currently 27)
    }

    struct GroupUpdateCounter
    {
        public ObjectGuid GroupGuid;
        public int UpdateSequenceNumber;
    }

    class StoredAuraTeleportLocation
    {
        public WorldLocation Loc;
        public State CurrentState;

        public enum State
        {
            Unchanged,
            Changed,
            Deleted
        }
    }

    struct QuestObjectiveStatusData
    {
        public (int QuestID, QuestStatusData Status) QuestStatusPair;
        public int ObjectiveId;
    }

    public readonly record struct PlayerTalent
    {
        public PlayerTalent(int rank, PlayerSpellState state)
        {
            State = state;
            Rank = (byte)rank;
        }
        public readonly PlayerSpellState State;
        public readonly byte Rank;
    };
}
