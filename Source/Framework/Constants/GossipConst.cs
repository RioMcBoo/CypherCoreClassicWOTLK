﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;

namespace Framework.Constants
{
    public enum GossipOption
    {
        None = 0,                   //Unit_Npc_Flag_None                (0)
        Gossip = 1,                 //Unit_Npc_Flag_Gossip              (1)
        Questgiver = 2,             //Unit_Npc_Flag_Questgiver          (2)
        Vendor = 3,                 //Unit_Npc_Flag_Vendor              (128)
        Taxivendor = 4,             //Unit_Npc_Flag_Taxivendor          (8192)
        Trainer = 5,                //Unit_Npc_Flag_Trainer             (16)
        Spirithealer = 6,           //Unit_Npc_Flag_Spirithealer        (16384)
        Spiritguide = 7,            //Unit_Npc_Flag_Spiritguide         (32768)
        Innkeeper = 8,              //Unit_Npc_Flag_Innkeeper           (65536)
        Banker = 9,                 //Unit_Npc_Flag_Banker              (131072)
        Petitioner = 10,            //Unit_Npc_Flag_Petitioner          (262144)
        Tabarddesigner = 11,        //Unit_Npc_Flag_Tabarddesigner      (524288)
        Battlefield = 12,           //Unit_Npc_Flag_Battlefieldperson   (1048576)
        Auctioneer = 13,            //Unit_Npc_Flag_Auctioneer          (2097152)
        Stablepet = 14,             //Unit_Npc_Flag_Stable              (4194304)
        Armorer = 15,               //Unit_Npc_Flag_Armorer             (4096)
        Unlearntalents = 16,        //Unit_Npc_Flag_Trainer             (16) (Bonus Option For Trainer)
        Unlearnpettalents_Old = 17, // deprecated
        Learndualspec = 18,         //Unit_Npc_Flag_Trainer             (16) (Bonus Option For Trainer)
        Outdoorpvp = 19,            //Added By Code (Option For Outdoor Pvp Creatures)
        Transmogrifier = 20,        //UNIT_NPC_FLAG_TRANSMOGRIFIER
        Mailbox = 21,               //UNIT_NPC_FLAG_MAILBOX
        Max
    }

    public enum GossipOptionNpc
    {
        None = 0,    // White chat bubble. Default
        Vendor = 1,    // Brown bag
        Taxinode = 2,    // White wing
        Trainer = 3,    // Brown book
        SpiritHealer = 4,    // Golden interaction wheel (with red center)
        Binder = 5,    // Golden interaction wheel
        Banker = 6,    // Brown bag (with gold coin in lower corner)
        PetitionVendor = 7,    // White chat bubble (with "..." inside)
        GuildTabardVendor = 8,    // White tabard
        Battlemaster = 9,    // Two crossed swords
        Auctioneer = 10,   // Stack of gold coins
        TalentMaster = 11,   // White chat bubble
        Stablemaster = 12,   // White chat bubble
        PetSpecializationMaster = 13,   /*DEPRECATED*/ // White chat bubble
        GuildBanker = 14,   // White chat bubble
        Spellclick = 15,   // White chat bubble
        DisableXPGain = 16,   // White chat bubble
        EnableXPGain = 17,   // White chat bubble
        Mailbox = 18,   // White chat bubble
        WorldPvPQueue = 19,   /*NYI*/ // White chat bubble
        LFGDungeon = 20,   /*NYI*/ // White chat bubble
        ArtifactRespec = 21,   /*NYI*/ // White chat bubble
        CemeterySelect = 22,   /*DEPRECATED*/ // White chat bubble
        SpecializationMaster = 23,   /*DEPRECATED*/ // White chat bubble
        GlyphMaster = 24,   /*DEPRECATED*/ // White chat bubble
        QueueScenario = 25,   /*NYI*/ // White chat bubble
        GarrisonArchitect = 26,   /*NYI*/ // White chat bubble
        GarrisonMissionNpc = 27,   /*NYI*/ // White chat bubble
        ShipmentCrafter = 28,   /*NYI*/ // Brown document
        GarrisonTradeskillNpc = 29,   /*NYI*/ // White chat bubble
        GarrisonRecruitment = 30,   /*NYI*/ // White chat bubble
        AdventureMap = 31,   /*NYI*/ // White chat bubble
        GarrisonTalent = 32,   // White chat bubble
        ContributionCollector = 33,   /*NYI*/ // White chat bubble
        Transmogrify = 34,   // Purple helm
        AzeriteRespec = 35,   // White chat bubble
        IslandsMissionNpc = 36,   /*NYI*/ // White chat bubble
        UIItemInteraction = 37,   /*NYI*/ // White chat bubble
        WorldMap = 38,   /*NYI*/ // White chat bubble
        Soulbind = 39,   /*NYI*/ // White chat bubble
        ChromieTimeNpc = 40,   /*NYI*/ // White chat bubble
        CovenantPreviewNpc = 41,   /*NYI*/ // White chat bubble
        RuneforgeLegendaryCrafting = 42,   /*NYI*/ // White chat bubble
        NewPlayerGuide = 43,   /*NYI*/ // White chat bubble
        RuneforgeLegendaryUpgrade = 44,   /*NYI*/ // White chat bubble
        CovenantRenownNpc = 45,   /*NYI*/ // White chat bubble
        BlackMarketAuctionHouse = 46,
        PerksProgramVendor = 47,
        ProfessionsCraftingOrder = 48,
        ProfessionsOpen = 49,
        ProfessionsCustomerOrder = 50,
        TraitSystem = 51,
        BarbersChoice = 52,
        MajorFactionRenown = 53,
        PersonalTabardVendor = 54,

        Max
    }

    public struct eTradeskill
    {
        // Skill Defines
        public const int TradeskillAlchemy = 1;
        public const int TradeskillBlacksmithing = 2;
        public const int TradeskillCooking = 3;
        public const int TradeskillEnchanting = 4;
        public const int TradeskillEngineering = 5;
        public const int TradeskillFirstaid = 6;
        public const int TradeskillHerbalism = 7;
        public const int TradeskillLeatherworking = 8;
        public const int TradeskillPoisons = 9;
        public const int TradeskillTailoring = 10;
        public const int TradeskillMining = 11;
        public const int TradeskillFishing = 12;
        public const int TradeskillSkinning = 13;
        public const int TradeskillJewlcrafting = 14;
        public const int TradeskillInscription = 15;

        public const int TradeskillLevelNone = 0;
        public const int TradeskillLevelApprentice = 1;
        public const int TradeskillLevelJourneyman = 2;
        public const int TradeskillLevelExpert = 3;
        public const int TradeskillLevelArtisan = 4;
        public const int TradeskillLevelMaster = 5;
        public const int TradeskillLevelGrandMaster = 6;

        // Gossip Defines
        public const int GossipActionTrade = 1;
        public const int GossipActionTrain = 2;
        public const int GossipActionTaxi = 3;
        public const int GossipActionGuild = 4;
        public const int GossipActionBattle = 5;
        public const int GossipActionBank = 6;
        public const int GossipActionInn = 7;
        public const int GossipActionHeal = 8;
        public const int GossipActionTabard = 9;
        public const int GossipActionAuction = 10;
        public const int GossipActionInnInfo = 11;
        public const int GossipActionUnlearn = 12;
        public const int GossipActionInfoDef = 1000;

        public const int GossipSenderMain = 1;
        public const int GossipSenderInnInfo = 2;
        public const int GossipSenderInfo = 3;
        public const int GossipSenderSecProftrain = 4;
        public const int GossipSenderSecClasstrain = 5;
        public const int GossipSenderSecBattleinfo = 6;
        public const int GossipSenderSecBank = 7;
        public const int GossipSenderSecInn = 8;
        public const int GossipSenderSecMailbox = 9;
        public const int GossipSenderSecStablemaster = 10;
    }

    public enum GossipOptionStatus
    {
        Available = 0,
        Unavailable = 1,
        Locked = 2,
        AlreadyComplete = 3
    }

    public enum GossipOptionRewardType
    {
        Item = 0,
        Currency = 1
    }

    public enum GossipOptionFlags
    {
        None = 0x0,
        QuestLabelPrepend = 0x1
    }

    [Flags]
    public enum ConversationFlags
    {
        None = 0x00,
        MultipleConversationType = 0x01, // NYI purpose unknown
        IsTalkingHeadConversation = 0x02, // implicitly implemented when conversation_actors.ActivePlayerObject == 0 && conversation_actors.NoActorObject == 0 && conversation_actors.ConversationActorGuid == 0
        AllowWithoutSpawnedActor = 0x04,
    }
}
