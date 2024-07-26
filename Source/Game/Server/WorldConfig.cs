// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Configuration;
using Framework.Constants;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Game
{
    public class WorldConfig : ConfigMgr
    {
        private WorldConfig() { }        

        public static WorldConfig Values = new();

        public static void Load(bool reload = false)
        {
            if (reload)
                Load("WorldServer.conf");

            // We cleanse all data to catch access errors on data that is not yet initialized.
            //Values._values.Clear(); // TODO: WorldCfg.ClientCacheVersion Already Pre-Loaded in WorldServer.Server.StartDB();

            // Read support system setting from the config file
            Values[WorldCfg.SupportEnabled] = GetDefaultValue("Support.Enabled", true);
            Values[WorldCfg.SupportTicketsEnabled] = GetDefaultValue("Support.TicketsEnabled", false);
            Values[WorldCfg.SupportBugsEnabled] = GetDefaultValue("Support.BugsEnabled", false);
            Values[WorldCfg.SupportComplaintsEnabled] = GetDefaultValue("Support.ComplaintsEnabled", false);
            Values[WorldCfg.SupportSuggestionsEnabled] = GetDefaultValue("Support.SuggestionsEnabled", false);

            // Send server info on login?
            Values[WorldCfg.EnableSinfoLogin] = GetDefaultValue("Server.LoginInfo", false);

            // Read all rates from the config file
            static void SetRegenRate(WorldCfg rate, string configKey)
            {
                float default_rate = GetDefaultValue(configKey, 1.0f);
                if (default_rate < 0.0f)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"{configKey} ({default_rate}) must be > 0. Using 1 instead.");

                    default_rate = 1.0f;
                }

                Values[rate] = default_rate;
            }

            SetRegenRate(WorldCfg.RateHealth, "Rate.Health");
            SetRegenRate(WorldCfg.RatePowerMana, "Rate.Mana");
            SetRegenRate(WorldCfg.RatePowerRageIncome, "Rate.Rage.Gain");
            SetRegenRate(WorldCfg.RatePowerRageLoss, "Rate.Rage.Loss");
            SetRegenRate(WorldCfg.RatePowerFocus, "Rate.Focus");
            SetRegenRate(WorldCfg.RatePowerEnergy, "Rate.Energy");
            SetRegenRate(WorldCfg.RatePowerComboPointsLoss, "Rate.ComboPoints.Loss");
            SetRegenRate(WorldCfg.RatePowerRunicPowerIncome, "Rate.RunicPower.Gain");
            SetRegenRate(WorldCfg.RatePowerRunicPowerLoss, "Rate.RunicPower.Loss");
            SetRegenRate(WorldCfg.RatePowerSoulShards, "Rate.SoulShards.Loss");
            SetRegenRate(WorldCfg.RatePowerLunarPower, "Rate.LunarPower.Loss");
            SetRegenRate(WorldCfg.RatePowerHolyPower, "Rate.HolyPower.Loss");
            SetRegenRate(WorldCfg.RatePowerMaelstrom, "Rate.Maelstrom.Loss");
            SetRegenRate(WorldCfg.RatePowerChi, "Rate.Chi.Loss");
            SetRegenRate(WorldCfg.RatePowerInsanity, "Rate.Insanity.Loss");
            SetRegenRate(WorldCfg.RatePowerArcaneCharges, "Rate.ArcaneCharges.Loss");
            SetRegenRate(WorldCfg.RatePowerFury, "Rate.Fury.Loss");
            SetRegenRate(WorldCfg.RatePowerPain, "Rate.Pain.Loss");
            SetRegenRate(WorldCfg.RatePowerEssence, "Rate.Essence.Loss");

            Values[WorldCfg.RateSkillDiscovery] = GetDefaultValue("Rate.Skill.Discovery", 1.0f);
            Values[WorldCfg.RateDropItemPoor] = GetDefaultValue("Rate.Drop.Item.Poor", 1.0f);
            Values[WorldCfg.RateDropItemNormal] = GetDefaultValue("Rate.Drop.Item.Normal", 1.0f);
            Values[WorldCfg.RateDropItemUncommon] = GetDefaultValue("Rate.Drop.Item.Uncommon", 1.0f);
            Values[WorldCfg.RateDropItemRare] = GetDefaultValue("Rate.Drop.Item.Rare", 1.0f);
            Values[WorldCfg.RateDropItemEpic] = GetDefaultValue("Rate.Drop.Item.Epic", 1.0f);
            Values[WorldCfg.RateDropItemLegendary] = GetDefaultValue("Rate.Drop.Item.Legendary", 1.0f);
            Values[WorldCfg.RateDropItemArtifact] = GetDefaultValue("Rate.Drop.Item.Artifact", 1.0f);
            Values[WorldCfg.RateDropItemReferenced] = GetDefaultValue("Rate.Drop.Item.Referenced", 1.0f);
            Values[WorldCfg.RateDropItemReferencedAmount] = GetDefaultValue("Rate.Drop.Item.ReferencedAmount", 1.0f);
            Values[WorldCfg.RateDropMoney] = GetDefaultValue("Rate.Drop.Money", 1.0f);
            Values[WorldCfg.RateXpKill] = GetDefaultValue("Rate.XP.Kill", 1.0f);
            Values[WorldCfg.RateXpBgKill] = GetDefaultValue("Rate.XP.BattlegroundKill", 1.0f);
            Values[WorldCfg.RateXpQuest] = GetDefaultValue("Rate.XP.Quest", 1.0f);
            Values[WorldCfg.RateXpExplore] = GetDefaultValue("Rate.XP.Explore", 1.0f);
            Values[WorldCfg.XpBoostDaymask] = GetDefaultValue("XP.Boost.Daymask", 0u);
            Values[WorldCfg.RateXpBoost] = GetDefaultValue("XP.Boost.Rate", 2.0f);

            Values[WorldCfg.RateRepaircost] = GetDefaultValue("Rate.RepairCost", 1.0f);
            if (Values[WorldCfg.RateRepaircost].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Rate.RepairCost ({Values[WorldCfg.RateRepaircost]}) must be >=0. Using 0.0 instead.");

                Values[WorldCfg.RateRepaircost] = 0.0f;
            }

            Values[WorldCfg.RateReputationGain] = GetDefaultValue("Rate.Reputation.Gain", 1.0f);
            Values[WorldCfg.RateReputationLowLevelKill] = GetDefaultValue("Rate.Reputation.LowLevel.Kill", 1.0f);
            Values[WorldCfg.RateReputationLowLevelQuest] = GetDefaultValue("Rate.Reputation.LowLevel.Quest", 1.0f);
            Values[WorldCfg.RateReputationRecruitAFriendBonus] = GetDefaultValue("Rate.Reputation.RecruitAFriendBonus", 0.1f);
            Values[WorldCfg.RateCreatureHpNormal] = GetDefaultValue("Rate.Creature.HP.Normal", 1.0f);
            Values[WorldCfg.RateCreatureHpElite] = GetDefaultValue("Rate.Creature.HP.Elite", 1.0f);
            Values[WorldCfg.RateCreatureHpRareelite] = GetDefaultValue("Rate.Creature.HP.RareElite", 1.0f);
            Values[WorldCfg.RateCreatureHpObsolete] = GetDefaultValue("Rate.Creature.HP.Obsolete", 1.0f);
            Values[WorldCfg.RateCreatureHpRare] = GetDefaultValue("Rate.Creature.HP.Rare", 1.0f);
            Values[WorldCfg.RateCreatureHpTrivial] = GetDefaultValue("Rate.Creature.HP.Trivial", 1.0f);
            Values[WorldCfg.RateCreatureHpMinusmob] = GetDefaultValue("Rate.Creature.HP.MinusMob", 1.0f);
            Values[WorldCfg.RateCreatureDamageNormal] = GetDefaultValue("Rate.Creature.Damage.Normal", 1.0f);
            Values[WorldCfg.RateCreatureDamageElite] = GetDefaultValue("Rate.Creature.Damage.Elite", 1.0f);
            Values[WorldCfg.RateCreatureDamageRareelite] = GetDefaultValue("Rate.Creature.Damage.RareElite", 1.0f);
            Values[WorldCfg.RateCreatureDamageObsolete] = GetDefaultValue("Rate.Creature.Damage.Obsolete", 1.0f);
            Values[WorldCfg.RateCreatureDamageRare] = GetDefaultValue("Rate.Creature.Damage.Rare", 1.0f);
            Values[WorldCfg.RateCreatureDamageTrivial] = GetDefaultValue("Rate.Creature.Damage.Trivial", 1.0f);
            Values[WorldCfg.RateCreatureDamageMinusmob] = GetDefaultValue("Rate.Creature.Damage.MinusMob", 1.0f);
            Values[WorldCfg.RateCreatureSpelldamageNormal] = GetDefaultValue("Rate.Creature.SpellDamage.Normal", 1.0f);
            Values[WorldCfg.RateCreatureSpelldamageElite] = GetDefaultValue("Rate.Creature.SpellDamage.Elite", 1.0f);
            Values[WorldCfg.RateCreatureSpelldamageRareelite] = GetDefaultValue("Rate.Creature.SpellDamage.RareElite", 1.0f);
            Values[WorldCfg.RateCreatureSpelldamageObsolete] = GetDefaultValue("Rate.Creature.SpellDamage.Obsolete", 1.0f);
            Values[WorldCfg.RateCreatureSpelldamageRare] = GetDefaultValue("Rate.Creature.SpellDamage.Rare", 1.0f);
            Values[WorldCfg.RateCreatureSpelldamageTrivial] = GetDefaultValue("Rate.Creature.SpellDamage.Trivial", 1.0f);
            Values[WorldCfg.RateCreatureSpelldamageMinusmob] = GetDefaultValue("Rate.Creature.SpellDamage.MinusMob", 1.0f);
            Values[WorldCfg.RateCreatureAggro] = GetDefaultValue("Rate.Creature.Aggro", 1.0f);
            Values[WorldCfg.RateRestIngame] = GetDefaultValue("Rate.Rest.InGame", 1.0f);
            Values[WorldCfg.RateRestOfflineInTavernOrCity] = GetDefaultValue("Rate.Rest.Offline.InTavernOrCity", 1.0f);
            Values[WorldCfg.RateRestOfflineInWilderness] = GetDefaultValue("Rate.Rest.Offline.InWilderness", 1.0f);
            Values[WorldCfg.RateDamageFall] = GetDefaultValue("Rate.Damage.Fall", 1.0f);
            Values[WorldCfg.RateAuctionTime] = GetDefaultValue("Rate.Auction.Time", 1.0f);
            Values[WorldCfg.RateAuctionDeposit] = GetDefaultValue("Rate.Auction.Deposit", 1.0f);
            Values[WorldCfg.RateAuctionCut] = GetDefaultValue("Rate.Auction.Cut", 1.0f);
            Values[WorldCfg.RateHonor] = GetDefaultValue("Rate.Honor", 1.0f);
            Values[WorldCfg.RateInstanceResetTime] = GetDefaultValue("Rate.InstanceResetTime", 1.0f);

            Values[WorldCfg.RateTalent] = GetDefaultValue("Rate.Talent", 1.0f);
            if (Values[WorldCfg.RateTalent].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Rate.Talent ({Values[WorldCfg.RateTalent]}) must be > 0. Using 1 instead.");

                Values[WorldCfg.RateTalent] = 1.0f;
            }

            Values[WorldCfg.RateMovespeed] = GetDefaultValue("Rate.MoveSpeed", 1.0f);
            if (Values[WorldCfg.RateMovespeed].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Rate.MoveSpeed ({Values[WorldCfg.RateMovespeed]}) must be > 0. Using 1 instead.");

                Values[WorldCfg.RateMovespeed] = 1.0f;
            }

            Values[WorldCfg.RateCorpseDecayLooted] = GetDefaultValue("Rate.Corpse.Decay.Looted", 0.5f);

            Values[WorldCfg.RateDurabilityLossOnDeath] = GetDefaultValue("DurabilityLoss.OnDeath", 10.0f);
            if (Values[WorldCfg.RateDurabilityLossOnDeath].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"DurabilityLoss.OnDeath ({Values[WorldCfg.RateDurabilityLossOnDeath]}) must be >=0. Using 0.0 instead.");

                Values[WorldCfg.RateDurabilityLossOnDeath] = 0.0f;
            }
            if (Values[WorldCfg.RateDurabilityLossOnDeath].Float > 100.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"DurabilityLoss.OnDeath ({Values[WorldCfg.RateDurabilityLossOnDeath]}) must be <= 100. Using 100.0 instead.");

                Values[WorldCfg.RateDurabilityLossOnDeath] = 0.0f;
            }
            Values[WorldCfg.RateDurabilityLossOnDeath] = Values[WorldCfg.RateDurabilityLossOnDeath].Float / 100.0f;

            Values[WorldCfg.RateDurabilityLossDamage] = GetDefaultValue("DurabilityLossChance.Damage", 0.5f);
            if (Values[WorldCfg.RateDurabilityLossDamage].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"DurabilityLossChance.Damage ({Values[WorldCfg.RateDurabilityLossDamage]}) must be >=0. Using 0.0 instead.");

                Values[WorldCfg.RateDurabilityLossDamage] = 0.0f;
            }

            Values[WorldCfg.RateDurabilityLossAbsorb] = GetDefaultValue("DurabilityLossChance.Absorb", 0.5f);
            if (Values[WorldCfg.RateDurabilityLossAbsorb].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"DurabilityLossChance.Absorb ({Values[WorldCfg.RateDurabilityLossAbsorb]}) must be >=0. Using 0.0 instead.");

                Values[WorldCfg.RateDurabilityLossAbsorb] = 0.0f;
            }

            Values[WorldCfg.RateDurabilityLossParry] = GetDefaultValue("DurabilityLossChance.Parry", 0.05f);
            if (Values[WorldCfg.RateDurabilityLossParry].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"DurabilityLossChance.Parry ({Values[WorldCfg.RateDurabilityLossParry]}) must be >=0. Using 0.0 instead.");

                Values[WorldCfg.RateDurabilityLossParry] = 0.0f;
            }

            Values[WorldCfg.RateDurabilityLossBlock] = GetDefaultValue("DurabilityLossChance.Block", 0.05f);
            if (Values[WorldCfg.RateDurabilityLossBlock].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"DurabilityLossChance.Block ({Values[WorldCfg.RateDurabilityLossBlock]}) must be >=0. Using 0.0 instead.");

                Values[WorldCfg.RateDurabilityLossBlock] = 0.0f;
            }

            Values[WorldCfg.RateMoneyQuest] = GetDefaultValue("Rate.Quest.Money.Reward", 1.0f);
            if (Values[WorldCfg.RateMoneyQuest].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Rate.Quest.Money.Reward ({Values[WorldCfg.RateMoneyQuest]}) must be >=0. Using 0 instead.");

                Values[WorldCfg.RateMoneyQuest] = 0.0f;
            }

            Values[WorldCfg.RateMoneyMaxLevelQuest] = GetDefaultValue("Rate.Quest.Money.Max.Level.Reward", 1.0f);
            if (Values[WorldCfg.RateMoneyMaxLevelQuest].Float < 0.0f)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Rate.Quest.Money.Max.Level.Reward ({Values[WorldCfg.RateMoneyMaxLevelQuest]}) must be >=0. Using 0 instead.");

                Values[WorldCfg.RateMoneyMaxLevelQuest] = 0.0f;
            }

            // Read other configuration items from the config file
            Values[WorldCfg.DurabilityLossInPvp] = GetDefaultValue("DurabilityLoss.InPvP", false);

            Values[WorldCfg.Compression] = GetDefaultValue("Compression", 1);
            if (Values[WorldCfg.Compression].Int32 < 1 || Values[WorldCfg.Compression].Int32 > 9)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Compression Level ({Values[WorldCfg.Compression]}) must be in range 1..9. Using default compression Level (1).");

                Values[WorldCfg.Compression] = 1;
            }

            Values[WorldCfg.AddonChannel] = GetDefaultValue("AddonChannel", true);
            Values[WorldCfg.CleanCharacterDb] = GetDefaultValue("CleanCharacterDB", false);
            Values[WorldCfg.PersistentCharacterCleanFlags] = GetDefaultValue("PersistentCharacterCleanFlags", 0);
            Values[WorldCfg.AuctionReplicateDelay] = (TimeSpan)GetDefaultValue("Auction.ReplicateItemsCooldown", (Seconds)900);

            Values[WorldCfg.AuctionSearchDelay] = (TimeSpan)GetDefaultValue("Auction.SearchDelay", (Milliseconds)300);
            if (Values[WorldCfg.AuctionSearchDelay].TimeSpan < (Milliseconds)100 || Values[WorldCfg.AuctionSearchDelay].TimeSpan > (Milliseconds)10000)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Auction.SearchDelay ({Values[WorldCfg.AuctionSearchDelay]}) " +
                    $"must be between {100} and {10000}. Using default of {300}ms.");

                Values[WorldCfg.AuctionSearchDelay] = (TimeSpan)(Milliseconds)300;
            }

            Values[WorldCfg.AuctionTaintedSearchDelay] = (TimeSpan)GetDefaultValue("Auction.TaintedSearchDelay", (Milliseconds)3000);
            if (Values[WorldCfg.AuctionTaintedSearchDelay].TimeSpan < (Milliseconds)100 || Values[WorldCfg.AuctionTaintedSearchDelay].TimeSpan > (Milliseconds)10000)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Auction.TaintedSearchDelay ({Values[WorldCfg.AuctionTaintedSearchDelay]}) " +
                    $"must be between 100 and 10000. Using default of 3s");

                Values[WorldCfg.AuctionTaintedSearchDelay] = (TimeSpan)(Milliseconds)3000;
            }

            Values[WorldCfg.ChatChannelLevelReq] = GetDefaultValue("ChatLevelReq.Channel", 1);
            Values[WorldCfg.ChatWhisperLevelReq] = GetDefaultValue("ChatLevelReq.Whisper", 1);
            Values[WorldCfg.ChatEmoteLevelReq] = GetDefaultValue("ChatLevelReq.Emote", 1);
            Values[WorldCfg.ChatSayLevelReq] = GetDefaultValue("ChatLevelReq.Say", 1);
            Values[WorldCfg.ChatYellLevelReq] = GetDefaultValue("ChatLevelReq.Yell", 1);
            Values[WorldCfg.PartyLevelReq] = GetDefaultValue("PartyLevelReq", 1);
            Values[WorldCfg.TradeLevelReq] = GetDefaultValue("LevelReq.Trade", 1);
            Values[WorldCfg.AuctionLevelReq] = GetDefaultValue("LevelReq.Auction", 1);
            Values[WorldCfg.MailLevelReq] = GetDefaultValue("LevelReq.Mail", 1);
            Values[WorldCfg.PreserveCustomChannels] = GetDefaultValue("PreserveCustomChannels", false);
            Values[WorldCfg.PreserveCustomChannelDuration] = (TimeSpan)GetDefaultValue("PreserveCustomChannelDuration", (Days)14);
            Values[WorldCfg.PreserveCustomChannelInterval] = (TimeSpan)GetDefaultValue("PreserveCustomChannelInterval", (Minutes)5);

            {
                Values[WorldCfg.GridUnload] = GetDefaultValue("GridUnload", true);

                Values[WorldCfg.BasemapLoadGrids] = GetDefaultValue("BaseMapLoadAllGrids", false);
                if (Values[WorldCfg.BasemapLoadGrids].Bool && Values[WorldCfg.GridUnload].Bool)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"BaseMapLoadAllGrids enabled, but GridUnload also enabled. " +
                        $"GridUnload must be disabled to enable base map pre-loading. " +
                        $"Base map pre-loading disabled");

                    Values[WorldCfg.BasemapLoadGrids] = false;
                }
                Values[WorldCfg.InstancemapLoadGrids] = GetDefaultValue("InstanceMapLoadAllGrids", false);
                if (Values[WorldCfg.InstancemapLoadGrids].Bool && Values[WorldCfg.GridUnload].Bool)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"InstanceMapLoadAllGrids enabled, but GridUnload also enabled. " +
                        $"GridUnload must be disabled to enable instance map pre-loading. " +
                        $"Instance map pre-loading disabled");

                    Values[WorldCfg.InstancemapLoadGrids] = false;
                }

                Values[WorldCfg.BattlegroundMapLoadGrids] = GetDefaultValue("BattlegroundMapLoadAllGrids", true);
                Values[WorldCfg.IntervalSave] = GetDefaultValue<Milliseconds>("PlayerSaveInterval", (Minutes)15);
                Values[WorldCfg.IntervalDisconnectTolerance] = GetDefaultValue("DisconnectToleranceInterval", (Seconds)0);
                Values[WorldCfg.StatsSaveOnlyOnLogout] = GetDefaultValue("PlayerSave.Stats.SaveOnlyOnLogout", true);

                Values[WorldCfg.MinLevelStatSave] = GetDefaultValue("PlayerSave.Stats.MinLevel", 0);
                if (Values[WorldCfg.MinLevelStatSave].Int32 > SharedConst.MaxLevel)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"PlayerSave.Stats.MinLevel ({Values[WorldCfg.MinLevelStatSave]}) must be in range 0..{SharedConst.MaxLevel}. " +
                        $"Using default, do not save character stats (0).");

                    Values[WorldCfg.MinLevelStatSave] = 0;
                }

                Values[WorldCfg.IntervalGridClean] = (TimeSpan)GetDefaultValue<Milliseconds>("GridCleanUpDelay", (Minutes)5);
                if (Values[WorldCfg.IntervalGridClean].TimeSpan < MapConst.MinGridDelay)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"GridCleanUpDelay ({Values[WorldCfg.IntervalGridClean]}) " +
                        $"must be greater {(Milliseconds)MapConst.MinGridDelay} Use this minimal value.");

                    Values[WorldCfg.IntervalGridClean] = MapConst.MinGridDelay;
                }
            }

            Values[WorldCfg.IntervalMapUpdate] = (TimeSpan)GetDefaultValue("MapUpdateInterval", (Milliseconds)10);
            if (Values[WorldCfg.IntervalMapUpdate].TimeSpan < MapConst.MinMapUpdateDelay)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MapUpdateInterval ({Values[WorldCfg.IntervalMapUpdate]}) " +
                    $"must be greater {(Milliseconds)MapConst.MinMapUpdateDelay}. Use this minimal value.");

                Values[WorldCfg.IntervalMapUpdate] = MapConst.MinMapUpdateDelay;
            }

            Values[WorldCfg.IntervalChangeweather] = (TimeSpan)GetDefaultValue<Milliseconds>("ChangeWeatherInterval", (Minutes)10);

            if (reload)
            {
                int val = GetDefaultValue("WorldServerPort", 8085);
                if (val != Values[WorldCfg.PortWorld].Int32)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"WorldServerPort option can't be changed at worldserver.conf reload, " +
                        $"using current value ({Values[WorldCfg.PortWorld]}).");
                }

                val = GetDefaultValue("InstanceServerPort", 8086);
                if (val != Values[WorldCfg.PortInstance].Int32)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"InstanceServerPort option can't be changed at worldserver.conf reload, " +
                        $"using current value ({Values[WorldCfg.PortInstance]}).");
                }
            }
            else
            {
                Values[WorldCfg.PortWorld] = GetDefaultValue("WorldServerPort", 8085);
                Values[WorldCfg.PortInstance] = GetDefaultValue("InstanceServerPort", 8086);
            }

            Values[WorldCfg.SocketTimeoutTime] = (TimeSpan)GetDefaultValue<Milliseconds>("SocketTimeOutTime", (Minutes)15);
            Values[WorldCfg.SocketTimeoutTimeActive] = (TimeSpan)GetDefaultValue<Milliseconds>("SocketTimeOutTimeActive", (Minutes)1);
            Values[WorldCfg.SessionAddDelay] = TimeSpan.FromMicroseconds(GetDefaultValue("SessionAddDelay", 10000));

            Values[WorldCfg.GroupXpDistance] = GetDefaultValue("MaxGroupXPDistance", 74.0f);
            Values[WorldCfg.MaxRecruitAFriendDistance] = GetDefaultValue("MaxRecruitAFriendBonusDistance", 100.0f);

            Values[WorldCfg.MinQuestScaledXpRatio] = GetDefaultValue("MinQuestScaledXPRatio", 0);
            if ((uint)Values[WorldCfg.MinQuestScaledXpRatio].Int32 > 100)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MinQuestScaledXPRatio ({Values[WorldCfg.MinQuestScaledXpRatio]}) must be in range 0..100. Set to 0.");

                Values[WorldCfg.MinQuestScaledXpRatio] = 0;
            }

            Values[WorldCfg.MinCreatureScaledXpRatio] = GetDefaultValue("MinCreatureScaledXPRatio", 0);
            if ((uint)Values[WorldCfg.MinCreatureScaledXpRatio].Int32 > 100)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MinCreatureScaledXPRatio ({Values[WorldCfg.MinCreatureScaledXpRatio]}) must be in range 0..100. Set to 0.");

                Values[WorldCfg.MinCreatureScaledXpRatio] = 0;
            }

            Values[WorldCfg.MinDiscoveredScaledXpRatio] = GetDefaultValue("MinDiscoveredScaledXPRatio", 0);
            if ((uint)Values[WorldCfg.MinDiscoveredScaledXpRatio].Int32 > 100)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MinDiscoveredScaledXPRatio ({Values[WorldCfg.MinDiscoveredScaledXpRatio]}) must be in range 0..100. Set to 0.");

                Values[WorldCfg.MinDiscoveredScaledXpRatio] = 0;
            }

            /// @todo Add MonsterSight (with meaning) in worldserver.conf or put them as define
            Values[WorldCfg.SightMonster] = GetDefaultValue("MonsterSight", 50.0f);

            Values[WorldCfg.RegenHpCannotReachTargetInRaid] = GetDefaultValue("Creature.RegenHPCannotReachTargetInRaid", true);

            if (reload)
            {
                int val = GetDefaultValue("GameType", 0);
                if (val != Values[WorldCfg.GameType].Int32)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"GameType option can't be changed at worldserver.conf reload, " +
                        $"using current value ({Values[WorldCfg.GameType]}).");
                }
            }
            else
                Values[WorldCfg.GameType] = GetDefaultValue("GameType", 0);

            if (reload)
            {
                int val = GetDefaultValue("RealmZone", RealmManager.HardcodedDevelopmentRealmCategoryId);
                if (val != Values[WorldCfg.RealmZone].Int32)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"RealmZone option can't be changed at worldserver.conf reload, " +
                        $"using current value ({Values[WorldCfg.RealmZone]}).");
                }
            }
            else
                Values[WorldCfg.RealmZone] = GetDefaultValue("RealmZone", RealmManager.HardcodedDevelopmentRealmCategoryId);

            { //TimeZoneOffset
                TimeSpan val = GetDefaultValue("TimeZoneOffset", (Minutes)(-1500));

                if (reload)
                {
                    if (val != Values[WorldCfg.TimeZoneOffset].TimeSpan)
                    {
                        string sign = Values[WorldCfg.TimeZoneOffset].Int32 > 0 ? "+" : "";

                        Log.outError(LogFilter.ServerLoading,
                            $"TimeZoneOffset option can't be changed at worldserver.conf reload, " +
                            $"using current value ({sign}{(Minutes)Values[WorldCfg.TimeZoneOffset].TimeSpan}).");
                    }
                }
                else
                    Values[WorldCfg.TimeZoneOffset] = val;
            }

            Values[WorldCfg.AllowTwoSideInteractionCalendar] = GetDefaultValue("AllowTwoSide.Interaction.Calendar", false);
            Values[WorldCfg.AllowTwoSideInteractionChannel] = GetDefaultValue("AllowTwoSide.Interaction.Channel", false);
            Values[WorldCfg.AllowTwoSideInteractionGroup] = GetDefaultValue("AllowTwoSide.Interaction.Group", false);
            Values[WorldCfg.AllowTwoSideInteractionGuild] = GetDefaultValue("AllowTwoSide.Interaction.Guild", false);
            Values[WorldCfg.AllowTwoSideInteractionAuction] = GetDefaultValue("AllowTwoSide.Interaction.Auction", true);
            Values[WorldCfg.AllowTwoSideTrade] = GetDefaultValue("AllowTwoSide.Trade", false);
            Values[WorldCfg.StrictPlayerNames] = GetDefaultValue("StrictPlayerNames", 0);
            Values[WorldCfg.StrictCharterNames] = GetDefaultValue("StrictCharterNames", 0);
            Values[WorldCfg.StrictPetNames] = GetDefaultValue("StrictPetNames", 0);

            Values[WorldCfg.MinPlayerName] = GetDefaultValue("MinPlayerName", 2);
            if (Values[WorldCfg.MinPlayerName].Int32 < 1 || Values[WorldCfg.MinPlayerName].Int32 > 12)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MinPlayerName ({Values[WorldCfg.MinPlayerName]}) must be in range {1}..{12}. Set to {2}.");

                Values[WorldCfg.MinPlayerName] = 2;
            }

            Values[WorldCfg.MinCharterName] = GetDefaultValue("MinCharterName", 2);
            if (Values[WorldCfg.MinCharterName].Int32 < 1 || Values[WorldCfg.MinCharterName].Int32 > 24)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MinCharterName ({Values[WorldCfg.MinCharterName]}) must be in range {1}..{24}. Set to {2}.");

                Values[WorldCfg.MinCharterName] = 2;
            }

            Values[WorldCfg.MinPetName] = GetDefaultValue("MinPetName", 2);
            if (Values[WorldCfg.MinPetName].Int32 < 1 || Values[WorldCfg.MinPetName].Int32 > 12)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MinPetName ({Values[WorldCfg.MinPetName]}) must be in range {1}..{12}. Set to {2}.");

                Values[WorldCfg.MinPetName] = 2;
            }

            Values[WorldCfg.CharterCostGuild] = GetDefaultValue("Guild.CharterCost", 1000u);
            Values[WorldCfg.CharterCostArena2v2] = GetDefaultValue("ArenaTeam.CharterCost.2v2", 800000u);
            Values[WorldCfg.CharterCostArena3v3] = GetDefaultValue("ArenaTeam.CharterCost.3v3", 1200000u);
            Values[WorldCfg.CharterCostArena5v5] = GetDefaultValue("ArenaTeam.CharterCost.5v5", 2000000u);

            Values[WorldCfg.CharacterCreatingDisabled] = GetDefaultValue("CharacterCreating.Disabled", 0);
            Values[WorldCfg.CharacterCreatingDisabledRacemask] = GetDefaultValue("CharacterCreating.Disabled.RaceMask", 0);
            Values[WorldCfg.CharacterCreatingDisabledClassmask] = GetDefaultValue("CharacterCreating.Disabled.ClassMask", 0);

            Values[WorldCfg.CharactersPerRealm] = GetDefaultValue("CharactersPerRealm", 60);
            if (Values[WorldCfg.CharactersPerRealm].Int32 < 1 || Values[WorldCfg.CharactersPerRealm].Int32 > 200)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"CharactersPerRealm ({Values[WorldCfg.CharactersPerRealm]}) must be in range {1}..{200}. Set to {200}.");

                Values[WorldCfg.CharactersPerRealm] = 200;
            }

            // must be after CharactersPerRealm
            Values[WorldCfg.CharactersPerAccount] = GetDefaultValue("CharactersPerAccount", 60);
            if (Values[WorldCfg.CharactersPerAccount].Int32 < Values[WorldCfg.CharactersPerRealm].Int32)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"CharactersPerAccount ({Values[WorldCfg.CharactersPerAccount]}) " +
                    $"can't be less than CharactersPerRealm ({Values[WorldCfg.CharactersPerRealm]}).");

                Values[WorldCfg.CharactersPerAccount] = Values[WorldCfg.CharactersPerRealm];
            }

            Values[WorldCfg.CharacterCreatingEvokersPerRealm] = GetDefaultValue("CharacterCreating.EvokersPerRealm", 1);
            if ((uint)Values[WorldCfg.CharacterCreatingEvokersPerRealm].Int32 > 10)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"CharacterCreating.EvokersPerRealm ({Values[WorldCfg.CharacterCreatingEvokersPerRealm]}) " +
                    $"must be in range 0..{10}. Set to {1}.");

                Values[WorldCfg.CharacterCreatingEvokersPerRealm] = 1;
            }

            Values[WorldCfg.CharacterCreatingMinLevelForDemonHunter] = GetDefaultValue("CharacterCreating.MinLevelForDemonHunter", 0);
            Values[WorldCfg.CharacterCreatingMinLevelForEvoker] = GetDefaultValue("CharacterCreating.MinLevelForEvoker", 50);

            Values[WorldCfg.CharacterCreatingDisableAlliedRaceAchievementRequirement] =
                GetDefaultValue("CharacterCreating.DisableAlliedRaceAchievementRequirement", false);

            Values[WorldCfg.SkipCinematics] = GetDefaultValue("SkipCinematics", 0);
            if (Values[WorldCfg.SkipCinematics].Int32 < 0 || Values[WorldCfg.SkipCinematics].Int32 > 2)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"SkipCinematics ({Values[WorldCfg.SkipCinematics]}) must be in range {0}..{2}. Set to {0}.");

                Values[WorldCfg.SkipCinematics] = 0;
            }

            if (reload)
            {
                int val = GetDefaultValue("MaxPlayerLevel", SharedConst.DefaultMaxPlayerLevel);
                if (val != Values[WorldCfg.MaxPlayerLevel].Int32)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"MaxPlayerLevel option can't be changed at config reload, " +
                        $"using current value ({Values[WorldCfg.MaxPlayerLevel]}).");
                }
            }
            else
                Values[WorldCfg.MaxPlayerLevel] = GetDefaultValue("MaxPlayerLevel", SharedConst.DefaultMaxPlayerLevel);

            if (Values[WorldCfg.MaxPlayerLevel].Int32 > SharedConst.MaxLevel && Values[WorldCfg.MaxPlayerLevel].Int32 >= 1)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MaxPlayerLevel ({Values[WorldCfg.MaxPlayerLevel]}) must be in range {1}..{SharedConst.MaxLevel}. " +
                    $"Set to {SharedConst.MaxLevel}.");

                Values[WorldCfg.MaxPlayerLevel] = SharedConst.MaxLevel;
            }

            Values[WorldCfg.MinDualspecLevel] = GetDefaultValue("MinDualSpecLevel", 40);

            Values[WorldCfg.StartPlayerLevel] = GetDefaultValue("StartPlayerLevel", 1);
            if (Values[WorldCfg.StartPlayerLevel].Int32 < 1)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartPlayerLevel ({Values[WorldCfg.StartPlayerLevel]}) " +
                    $"must be in range {1}..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {1}.");

                Values[WorldCfg.StartPlayerLevel] = 1;
            }
            else if (Values[WorldCfg.StartPlayerLevel].Int32 > Values[WorldCfg.MaxPlayerLevel].Int32)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartPlayerLevel ({Values[WorldCfg.StartPlayerLevel]}) " +
                    $"must be in range {1}..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {Values[WorldCfg.MaxPlayerLevel]}.");

                Values[WorldCfg.StartPlayerLevel] = Values[WorldCfg.MaxPlayerLevel];
            }

            Values[WorldCfg.StartDeathKnightPlayerLevel] = GetDefaultValue("StartDeathKnightPlayerLevel", 55);
            if (Values[WorldCfg.StartDeathKnightPlayerLevel].Int32 < 1)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartDeathKnightPlayerLevel ({Values[WorldCfg.StartDeathKnightPlayerLevel]}) " +
                    $"must be in range {1}..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {1}.");

                Values[WorldCfg.StartDeathKnightPlayerLevel] = 1;
            }
            else if (Values[WorldCfg.StartDeathKnightPlayerLevel].Int32 > Values[WorldCfg.MaxPlayerLevel].Int32)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartDeathKnightPlayerLevel ({Values[WorldCfg.StartDeathKnightPlayerLevel]}) " +
                    $"must be in range 1..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {Values[WorldCfg.MaxPlayerLevel]}.");

                Values[WorldCfg.StartDeathKnightPlayerLevel] = Values[WorldCfg.MaxPlayerLevel];
            }

            Values[WorldCfg.StartDemonHunterPlayerLevel] = GetDefaultValue("StartDemonHunterPlayerLevel", 8);
            if (Values[WorldCfg.StartDemonHunterPlayerLevel].Int32 < 1)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartDemonHunterPlayerLevel ({Values[WorldCfg.StartDemonHunterPlayerLevel]}) " +
                    $"must be in range {1}..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {1}.");

                Values[WorldCfg.StartDemonHunterPlayerLevel] = 1;
            }
            else if (Values[WorldCfg.StartDemonHunterPlayerLevel].Int32 > Values[WorldCfg.MaxPlayerLevel].Int32)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartDemonHunterPlayerLevel ({Values[WorldCfg.StartDemonHunterPlayerLevel]}) " +
                    $"must be in range {1}..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {Values[WorldCfg.MaxPlayerLevel]}.");

                Values[WorldCfg.StartDemonHunterPlayerLevel] = Values[WorldCfg.MaxPlayerLevel];
            }

            Values[WorldCfg.StartEvokerPlayerLevel] = GetDefaultValue("StartEvokerPlayerLevel", 58);
            if (Values[WorldCfg.StartEvokerPlayerLevel].Int32 < 1)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartEvokerPlayerLevel ({Values[WorldCfg.StartEvokerPlayerLevel]}) " +
                    $"must be in range {1}..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {1}.");

                Values[WorldCfg.StartEvokerPlayerLevel] = 1;
            }
            else if (Values[WorldCfg.StartEvokerPlayerLevel].Int32 > Values[WorldCfg.MaxPlayerLevel].Int32)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartEvokerPlayerLevel ({Values[WorldCfg.StartEvokerPlayerLevel]}) " +
                    $"must be in range {1}..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {Values[WorldCfg.MaxPlayerLevel]}.");

                Values[WorldCfg.StartEvokerPlayerLevel] = Values[WorldCfg.MaxPlayerLevel];
            }

            Values[WorldCfg.StartAlliedRaceLevel] = GetDefaultValue("StartAlliedRacePlayerLevel", 10);
            if (Values[WorldCfg.StartAlliedRaceLevel].Int32 < 1)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartAlliedRaceLevel ({Values[WorldCfg.StartAlliedRaceLevel]}) " +
                    $"must be in range {1}..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {1}.");

                Values[WorldCfg.StartAlliedRaceLevel] = 1;
            }
            else if (Values[WorldCfg.StartAlliedRaceLevel].Int32 > Values[WorldCfg.MaxPlayerLevel].Int32)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartAlliedRaceLevel ({Values[WorldCfg.StartAlliedRaceLevel]}) " +
                    $"must be in range {1}..MaxPlayerLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {Values[WorldCfg.MaxPlayerLevel]}.");

                Values[WorldCfg.StartAlliedRaceLevel] = Values[WorldCfg.MaxPlayerLevel];
            }

            Values[WorldCfg.StartPlayerMoney] = GetDefaultValue("StartPlayerMoney", 0);
            if (Values[WorldCfg.StartPlayerMoney].Int32 < 0)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartPlayerMoney ({Values[WorldCfg.StartPlayerMoney]}) " +
                    $"must be in range {0}..{PlayerConst.MaxMoneyAmount}. " +
                    $"Set to {0}.");

                Values[WorldCfg.StartPlayerMoney] = 0;
            }
            else if (Values[WorldCfg.StartPlayerMoney].Int32 > PlayerConst.MaxMoneyAmount)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartPlayerMoney ({Values[WorldCfg.StartPlayerMoney]}) " +
                    $"must be in range {0}..{PlayerConst.MaxMoneyAmount}. " +
                    $"Set to {PlayerConst.MaxMoneyAmount}.");

                Values[WorldCfg.StartPlayerMoney] = PlayerConst.MaxMoneyAmount;
            }

            Values[WorldCfg.CurrencyResetHour] = GetDefaultValue("Currency.ResetHour", 3);
            if ((uint)Values[WorldCfg.CurrencyResetHour].Int32 > 23)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"StartPlayerMoney ({Values[WorldCfg.CurrencyResetHour]}) " +
                    $"must be in range {0}..{23}. " +
                    $"Set to {3}.");

                Values[WorldCfg.CurrencyResetHour] = 3;
            }

            Values[WorldCfg.CurrencyResetDay] = GetDefaultValue("Currency.ResetDay", 3);
            if ((uint)Values[WorldCfg.CurrencyResetDay].Int32 > 6)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Currency.ResetDay ({Values[WorldCfg.CurrencyResetDay]}) " +
                    $"must be in range {0}..{6}. " +
                    $"Set to {3}.");

                Values[WorldCfg.CurrencyResetDay] = 3;
            }

            Values[WorldCfg.CurrencyResetInterval] = (TimeSpan)GetDefaultValue("Currency.ResetInterval", (Days)7);
            if (Values[WorldCfg.CurrencyResetInterval].TimeSpan <= (Days)0)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Currency.ResetInterval ({(Days)Values[WorldCfg.CurrencyResetInterval].TimeSpan}) " +
                    $"must be > 0, " +
                    $"set to default {7}.");

                Values[WorldCfg.CurrencyResetInterval] = (TimeSpan)(Days)7;
            }

            Values[WorldCfg.MaxRecruitAFriendBonusPlayerLevel] = GetDefaultValue("RecruitAFriend.MaxLevel", 60);
            if ((uint)Values[WorldCfg.MaxRecruitAFriendBonusPlayerLevel].Int32 > Values[WorldCfg.MaxPlayerLevel].Int32)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"RecruitAFriend.MaxLevel ({Values[WorldCfg.MaxRecruitAFriendBonusPlayerLevel]}) " +
                    $"must be in the range {0}..MaxLevel({Values[WorldCfg.MaxPlayerLevel]}). " +
                    $"Set to {60}.");

                Values[WorldCfg.MaxRecruitAFriendBonusPlayerLevel] = 60;
            }

            Values[WorldCfg.MaxRecruitAFriendBonusPlayerLevelDifference] = GetDefaultValue("RecruitAFriend.MaxDifference", 4);
            Values[WorldCfg.AllTaxiPaths] = GetDefaultValue("AllFlightPaths", false);
            Values[WorldCfg.InstantTaxi] = GetDefaultValue("InstantFlightPaths", false);

            Values[WorldCfg.InstanceIgnoreLevel] = GetDefaultValue("Instance.IgnoreLevel", false);
            Values[WorldCfg.InstanceIgnoreRaid] = GetDefaultValue("Instance.IgnoreRaid", false);

            Values[WorldCfg.CastUnstuck] = GetDefaultValue("CastUnstuck", true);
            Values[WorldCfg.ResetScheduleWeekDay] = GetDefaultValue("ResetSchedule.WeekDay", 2);
            Values[WorldCfg.ResetScheduleHour] = GetDefaultValue("ResetSchedule.Hour", 8);
            Values[WorldCfg.InstanceUnloadDelay] = (TimeSpan)GetDefaultValue<Milliseconds>("Instance.UnloadDelay", (Minutes)30);

            Values[WorldCfg.DailyQuestResetTimeHour] = GetDefaultValue("Quests.DailyResetTime", 3);
            if ((uint)Values[WorldCfg.DailyQuestResetTimeHour].Int32 > 23)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Quests.DailyResetTime ({Values[WorldCfg.DailyQuestResetTimeHour]}) " +
                    $"must be in range {0}..{23}. " +
                    $"Set to {3}.");

                Values[WorldCfg.DailyQuestResetTimeHour] = 3;
            }

            Values[WorldCfg.WeeklyQuestResetTimeWDay] = GetDefaultValue("Quests.WeeklyResetWDay", 3);
            if ((uint)Values[WorldCfg.WeeklyQuestResetTimeWDay].Int32 > 6)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Quests.WeeklyResetDay ({Values[WorldCfg.WeeklyQuestResetTimeWDay]}) " +
                    $"must be in range {0}..{6}. " +
                    $"Set to {3} ({(DayOfWeek)3}).");

                Values[WorldCfg.WeeklyQuestResetTimeWDay] = 3;
            }

            Values[WorldCfg.MaxPrimaryTradeSkill] = GetDefaultValue("MaxPrimaryTradeSkill", 2);

            Values[WorldCfg.MinPetitionSigns] = GetDefaultValue("MinPetitionSigns", 4);
            if ((uint)Values[WorldCfg.MinPetitionSigns].Int32 > 4)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MinPetitionSigns ({Values[WorldCfg.MinPetitionSigns]}) " +
                    $"must be in range {0}..{4}. " +
                    $"Set to {4}.");

                Values[WorldCfg.MinPetitionSigns] = 4;
            }

            Values[WorldCfg.GmLoginState] = GetDefaultValue("GM.LoginState", 2);
            Values[WorldCfg.GmVisibleState] = GetDefaultValue("GM.Visible", 2);
            Values[WorldCfg.GmChat] = GetDefaultValue("GM.Chat", 2);
            Values[WorldCfg.GmWhisperingTo] = GetDefaultValue("GM.WhisperingTo", 2);
            Values[WorldCfg.GmFreezeDuration] = GetDefaultValue("GM.FreezeAuraDuration", (Seconds)0);

            Values[WorldCfg.GmLevelInGmList] = GetDefaultValue("GM.InGMList.Level", (int)AccountTypes.Administrator);
            Values[WorldCfg.GmLevelInWhoList] = GetDefaultValue("GM.InWhoList.Level", (int)AccountTypes.Administrator);

            Values[WorldCfg.StartGmLevel] = GetDefaultValue("GM.StartLevel", 1);
            if (Values[WorldCfg.StartGmLevel].Int32 < Values[WorldCfg.StartPlayerLevel].Int32)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"GM.StartLevel ({Values[WorldCfg.StartGmLevel]}) " +
                    $"must be in range StartPlayerLevel({Values[WorldCfg.StartPlayerLevel]})..{SharedConst.MaxLevel}. " +
                    $"Set to {Values[WorldCfg.StartPlayerLevel]}.");

                Values[WorldCfg.StartGmLevel] = Values[WorldCfg.StartPlayerLevel];
            }
            else if (Values[WorldCfg.StartGmLevel].Int32 > SharedConst.MaxLevel)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"GM.StartLevel ({Values[WorldCfg.StartGmLevel]}) " +
                    $"must be in range {1}..{SharedConst.MaxLevel}. " +
                    $"Set to {SharedConst.MaxLevel}.");

                Values[WorldCfg.StartGmLevel] = SharedConst.MaxLevel;
            }
            Values[WorldCfg.AllowGmGroup] = GetDefaultValue("GM.AllowInvite", false);
            Values[WorldCfg.GmLowerSecurity] = GetDefaultValue("GM.LowerSecurity", false);
            Values[WorldCfg.ForceShutdownThreshold] = GetDefaultValue("GM.ForceShutdownThreshold", (Seconds)30);

            Values[WorldCfg.GroupVisibility] = GetDefaultValue("Visibility.GroupMode", 1);

            Values[WorldCfg.MailDeliveryDelay] = (TimeSpan)GetDefaultValue<Seconds>("MailDeliveryDelay", (Hours)1);

            {
                int defaultValue = GetDefaultValue("CleanOldMailTime", 4);
                if ((uint)defaultValue > 23)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"CleanOldMailTime ({defaultValue}) " +
                        $"must be an hour, between {0} and {23}. " +
                        $"Set to {4}.");

                    defaultValue = 4;
                }

                Values[WorldCfg.CleanOldMailTime] = Time.SpanFromHours(defaultValue);
            }

            {
                int defaultValue = GetDefaultValue("UpdateUptimeInterval", 10);
                if (defaultValue <= 0)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"UpdateUptimeInterval ({defaultValue}) " +
                        $"must be > {0}, " +
                        $"set to default {10}.");

                    defaultValue = 10;
                }

                Values[WorldCfg.UptimeUpdate] = Time.SpanFromMinutes(defaultValue);
            }

            // log db cleanup interval
            {
                int defaultValue = GetDefaultValue("LogDB.Opt.ClearInterval", 10);
                if (defaultValue <= 0)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"LogDB.Opt.ClearInterval ({defaultValue}) " +
                        $"must be > {0}, " +
                        $"set to default {10}.");

                    defaultValue = 10;
                }

                Values[WorldCfg.LogdbClearinterval] = Time.SpanFromMinutes(defaultValue);

                Values[WorldCfg.LogdbCleartime] = GetDefaultValue<Seconds>("LogDB.Opt.ClearTime", (Days)14);

                Log.outInfo(LogFilter.ServerLoading,
                    $"Will clear `logs` table of entries older than {Values[WorldCfg.LogdbCleartime]} seconds " +
                    $"every {(Minutes)Values[WorldCfg.LogdbClearinterval].TimeSpan} minutes.");
            }

            Values[WorldCfg.SkillChanceOrange] = GetDefaultValue("SkillChance.Orange", 100);
            Values[WorldCfg.SkillChanceYellow] = GetDefaultValue("SkillChance.Yellow", 75);
            Values[WorldCfg.SkillChanceGreen] = GetDefaultValue("SkillChance.Green", 25);
            Values[WorldCfg.SkillChanceGrey] = GetDefaultValue("SkillChance.Grey", 0);

            Values[WorldCfg.SkillChanceMiningSteps] = GetDefaultValue("SkillChance.MiningSteps", 75);
            Values[WorldCfg.SkillChanceSkinningSteps] = GetDefaultValue("SkillChance.SkinningSteps", 75);

            Values[WorldCfg.SkillProspecting] = GetDefaultValue("SkillChance.Prospecting", false);
            Values[WorldCfg.SkillMilling] = GetDefaultValue("SkillChance.Milling", false);

            Values[WorldCfg.SkillGainCrafting] = GetDefaultValue("SkillGain.Crafting", 1);
            Values[WorldCfg.SkillGainDefense] = GetDefaultValue("SkillGain.Defense", 1);
            Values[WorldCfg.SkillGainGathering] = GetDefaultValue("SkillGain.Gathering", 1);
            Values[WorldCfg.SkillGainWeapon] = GetDefaultValue("SkillGain.Weapon", 1);

            Values[WorldCfg.MaxOverspeedPings] = GetDefaultValue("MaxOverspeedPings", 2);
            if (Values[WorldCfg.MaxOverspeedPings].Int32 != 0 && Values[WorldCfg.MaxOverspeedPings].Int32 < 2)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"MaxOverspeedPings ({Values[WorldCfg.MaxOverspeedPings]}) " +
                    $"must be in range {2}..infinity (or {0} to disable check). " +
                    $"Set to {2}.");

                Values[WorldCfg.MaxOverspeedPings] = 2;
            }

            Values[WorldCfg.Weather] = GetDefaultValue("ActivateWeather", true);

            Values[WorldCfg.DisableBreathing] = GetDefaultValue("DisableWaterBreath", (int)AccountTypes.Console);

            if (reload)
            {
                int val = GetDefaultValue("Expansion", (int)Expansion.WrathOfTheLichKing);
                if (val != Values[WorldCfg.Expansion].Int32)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"Expansion option can't be changed at worldserver.conf reload, " +
                        $"using current value ({Values[WorldCfg.Expansion]}[{(Expansion)Values[WorldCfg.Expansion].Int32}]).");
                }
            }
            else
                Values[WorldCfg.Expansion] = GetDefaultValue("Expansion", (int)Expansion.WrathOfTheLichKing);

            Values[WorldCfg.ChatFloodMessageCount] = GetDefaultValue("ChatFlood.MessageCount", 10);
            Values[WorldCfg.ChatFloodMessageDelay] = (TimeSpan)GetDefaultValue("ChatFlood.MessageDelay", (Seconds)1);
            Values[WorldCfg.ChatFloodMuteTime] = (TimeSpan)GetDefaultValue("ChatFlood.MuteTime", (Seconds)10);

            Values[WorldCfg.EventAnnounce] = GetDefaultValue("Event.Announce", false);

            Values[WorldCfg.CreatureFamilyFleeAssistanceRadius] = GetDefaultValue("CreatureFamilyFleeAssistanceRadius", 30.0f);
            Values[WorldCfg.CreatureFamilyAssistanceRadius] = GetDefaultValue("CreatureFamilyAssistanceRadius", 10.0f);
            Values[WorldCfg.CreatureFamilyAssistanceDelay] = (TimeSpan)GetDefaultValue("CreatureFamilyAssistanceDelay", (Milliseconds)1500);
            Values[WorldCfg.CreatureFamilyFleeDelay] = (TimeSpan)GetDefaultValue("CreatureFamilyFleeDelay", (Milliseconds)7000);

            Values[WorldCfg.WorldBossLevelDiff] = GetDefaultValue("WorldBossLevelDiff", 3);

            Values[WorldCfg.QuestEnableQuestTracker] = GetDefaultValue("Quests.EnableQuestTracker", false);

            {
                // note: disable value (-1) will assigned as SharedConst.MaxLevel,
                // to prevent wrong calculations in this case
                int defaultValue;

                defaultValue = GetDefaultValue("Quests.LowLevelHideDiff", 5);
                if (defaultValue > SharedConst.MaxLevel || defaultValue < 0)
                    Values[WorldCfg.QuestLowLevelHideDiff] = SharedConst.MaxLevel;
                else
                    Values[WorldCfg.QuestLowLevelHideDiff] = defaultValue;

                defaultValue = GetDefaultValue("Quests.HighLevelHideDiff", 2);
                if (defaultValue > SharedConst.MaxLevel || defaultValue < 0)
                    Values[WorldCfg.QuestHighLevelHideDiff] = SharedConst.MaxLevel;
                else
                    Values[WorldCfg.QuestHighLevelHideDiff] = defaultValue;
            }

            Values[WorldCfg.QuestIgnoreRaid] = GetDefaultValue("Quests.IgnoreRaid", false);
            Values[WorldCfg.QuestIgnoreAutoAccept] = GetDefaultValue("Quests.IgnoreAutoAccept", false);
            Values[WorldCfg.QuestIgnoreAutoComplete] = GetDefaultValue("Quests.IgnoreAutoComplete", false);

            Values[WorldCfg.RandomBgResetHour] = GetDefaultValue("Battleground.Random.ResetHour", 6);
            if ((uint)Values[WorldCfg.RandomBgResetHour].Int32 > 23)
            {
                Log.outError(LogFilter.ServerLoading,
                    $"Battleground.Random.ResetHour ({Values[WorldCfg.RandomBgResetHour]}) " +
                    $"must be an hour, between {0} and {23}. " +
                    $"Set to {6}.");

                Values[WorldCfg.RandomBgResetHour] = 6;
            }

            Values[WorldCfg.CalendarDeleteOldEventsHour] = GetDefaultValue("Calendar.DeleteOldEventsHour", 6);
            if ((uint)Values[WorldCfg.CalendarDeleteOldEventsHour].Int32 > 23)
            {
                Log.outError(LogFilter.Misc,
                    $"Calendar.DeleteOldEventsHour ({Values[WorldCfg.CalendarDeleteOldEventsHour]}) " +
                    $"must be an hour, between {0} and {23}. " +
                    $"Set to {6}.");

                Values[WorldCfg.CalendarDeleteOldEventsHour] = 6;
            }

            Values[WorldCfg.GuildResetHour] = GetDefaultValue("Guild.ResetHour", 6);
            if ((uint)Values[WorldCfg.GuildResetHour].Int32 > 23)
            {
                Log.outError(LogFilter.Server,
                    $"Guild.ResetHour ({Values[WorldCfg.GuildResetHour]}) " +
                    $"must be an hour, between {0} and {23}. " +
                    $"Set to {6}.");

                Values[WorldCfg.GuildResetHour] = 6;
            }

            Values[WorldCfg.DetectPosCollision] = GetDefaultValue("DetectPosCollision", true);

            Values[WorldCfg.RestrictedLfgChannel] = GetDefaultValue("Channel.RestrictedLfg", true);
            Values[WorldCfg.TalentsInspecting] = GetDefaultValue("TalentsInspecting", 1);
            Values[WorldCfg.ChatFakeMessagePreventing] = GetDefaultValue("ChatFakeMessagePreventing", false); //TODO: NIY?
            Values[WorldCfg.ChatStrictLinkCheckingSeverity] = GetDefaultValue("ChatStrictLinkChecking.Severity", 0);
            Values[WorldCfg.ChatStrictLinkCheckingKick] = GetDefaultValue("ChatStrictLinkChecking.Kick", 0);

            Values[WorldCfg.CorpseDecayNormal] = GetDefaultValue("Corpse.Decay.Normal", (Seconds)300);
            Values[WorldCfg.CorpseDecayElite] = GetDefaultValue("Corpse.Decay.Elite", (Seconds)300);
            Values[WorldCfg.CorpseDecayRareelite] = GetDefaultValue("Corpse.Decay.RareElite", (Seconds)300);
            Values[WorldCfg.CorpseDecayObsolete] = GetDefaultValue("Corpse.Decay.Obsolete", (Seconds)3600);
            Values[WorldCfg.CorpseDecayRare] = GetDefaultValue("Corpse.Decay.Rare", (Seconds)300);
            Values[WorldCfg.CorpseDecayTrivial] = GetDefaultValue("Corpse.Decay.Trivial", (Seconds)300);
            Values[WorldCfg.CorpseDecayMinusMob] = GetDefaultValue("Corpse.Decay.MinusMob", (Seconds)150);

            Values[WorldCfg.DeathSicknessLevel] = GetDefaultValue("Death.SicknessLevel", 11);
            Values[WorldCfg.DeathCorpseReclaimDelayPvp] = GetDefaultValue("Death.CorpseReclaimDelay.PvP", true);
            Values[WorldCfg.DeathCorpseReclaimDelayPve] = GetDefaultValue("Death.CorpseReclaimDelay.PvE", true);
            Values[WorldCfg.DeathBonesWorld] = GetDefaultValue("Death.Bones.World", true);
            Values[WorldCfg.DeathBonesBgOrArena] = GetDefaultValue("Death.Bones.BattlegroundOrArena", true);

            Values[WorldCfg.DieCommandMode] = GetDefaultValue("Die.Command.Mode", true);

            Values[WorldCfg.ThreatRadius] = GetDefaultValue("ThreatRadius", 60.0f);

            Values[WorldCfg.DeclinedNamesUsed] = GetDefaultValue("DeclinedNames", false);

            Values[WorldCfg.ListenRangeSay] = GetDefaultValue("ListenRange.Say", 25.0f);
            Values[WorldCfg.ListenRangeTextemote] = GetDefaultValue("ListenRange.TextEmote", 25.0f);
            Values[WorldCfg.ListenRangeYell] = GetDefaultValue("ListenRange.Yell", 300.0f);

            Values[WorldCfg.BattlegroundCastDeserter] = GetDefaultValue("Battleground.CastDeserter", true);
            Values[WorldCfg.BattlegroundQueueAnnouncerEnable] = GetDefaultValue("Battleground.QueueAnnouncer.Enable", false);
            Values[WorldCfg.BattlegroundQueueAnnouncerPlayerOnly] = GetDefaultValue("Battleground.QueueAnnouncer.PlayerOnly", false);
            Values[WorldCfg.BattlegroundStoreStatisticsEnable] = GetDefaultValue("Battleground.StoreStatistics.Enable", false);

            {
                Values[WorldCfg.BattlegroundReportAfk] = GetDefaultValue("Battleground.ReportAFK", 3);
                if (Values[WorldCfg.BattlegroundReportAfk].Int32 < 1)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"Battleground.ReportAFK ({Values[WorldCfg.BattlegroundReportAfk]}) " +
                        $"must be > {0}. " +
                        $"Using {3} instead.");

                    Values[WorldCfg.BattlegroundReportAfk] = 3;
                }
                if (Values[WorldCfg.BattlegroundReportAfk].Int32 > 9)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"Battleground.ReportAFK ({Values[WorldCfg.BattlegroundReportAfk]}) " +
                        $"must be < {10}. " +
                        $"Using {3} instead.");

                    Values[WorldCfg.BattlegroundReportAfk] = 3;
                }
            }

            Values[WorldCfg.BattlegroundInvitationType] = GetDefaultValue("Battleground.InvitationType", 0);
            Values[WorldCfg.BattlegroundPrematureFinishTimer] = (TimeSpan)GetDefaultValue<Milliseconds>("Battleground.PrematureFinishTimer", (Minutes)5);
            Values[WorldCfg.BattlegroundPremadeGroupWaitForMatch] = (TimeSpan)GetDefaultValue<Milliseconds>("Battleground.PremadeGroupWaitForMatch", (Minutes)30);
            Values[WorldCfg.BgXpForKill] = GetDefaultValue("Battleground.GiveXPForKills", false);
            Values[WorldCfg.ArenaMaxRatingDifference] = GetDefaultValue("Arena.MaxRatingDifference", 150);
            Values[WorldCfg.ArenaRatingDiscardTimer] = (TimeSpan)GetDefaultValue<Milliseconds>("Arena.RatingDiscardTimer", (Minutes)10);
            Values[WorldCfg.ArenaRatedUpdateTimer] = (TimeSpan)GetDefaultValue<Milliseconds>("Arena.RatedUpdateTimer", (Seconds)5);
            Values[WorldCfg.ArenaQueueAnnouncerEnable] = GetDefaultValue("Arena.QueueAnnouncer.Enable", false);
            Values[WorldCfg.ArenaSeasonId] = GetDefaultValue("Arena.ArenaSeason.ID", 8);
            Values[WorldCfg.ArenaStartRating] = GetDefaultValue("Arena.ArenaStartRating", 0);
            Values[WorldCfg.ArenaStartPersonalRating] = GetDefaultValue("Arena.ArenaStartPersonalRating", 0);
            Values[WorldCfg.ArenaStartMatchmakerRating] = GetDefaultValue("Arena.ArenaStartMatchmakerRating", 1500);
            Values[WorldCfg.ArenaSeasonInProgress] = GetDefaultValue("Arena.ArenaSeason.InProgress", true);
            Values[WorldCfg.ArenaLogExtendedInfo] = GetDefaultValue("ArenaLog.ExtendedInfo", false);
            Values[WorldCfg.ArenaWinRatingModifier1] = GetDefaultValue("Arena.ArenaWinRatingModifier1", 48.0f);
            Values[WorldCfg.ArenaWinRatingModifier2] = GetDefaultValue("Arena.ArenaWinRatingModifier2", 24.0f);
            Values[WorldCfg.ArenaLoseRatingModifier] = GetDefaultValue("Arena.ArenaLoseRatingModifier", 24.0f);
            Values[WorldCfg.ArenaMatchmakerRatingModifier] = GetDefaultValue("Arena.ArenaMatchmakerRatingModifier", 24.0f);

            if (reload)
            {
                Global.WorldStateMgr.SetValue(
                    WorldStates.CurrentPvpSeasonId, Values[WorldCfg.ArenaSeasonInProgress].Bool ? Values[WorldCfg.ArenaSeasonId].Int32 : 0, false, null);

                Global.WorldStateMgr.SetValue(
                    WorldStates.PreviousPvpSeasonId, Values[WorldCfg.ArenaSeasonId].Int32 - (Values[WorldCfg.ArenaSeasonInProgress].Bool ? 1 : 0), false, null);
            }

            Values[WorldCfg.OffhandCheckAtSpellUnlearn] = GetDefaultValue("OffhandCheckAtSpellUnlearn", true);

            Values[WorldCfg.CreaturePickpocketRefill] = (TimeSpan)GetDefaultValue<Seconds>("Creature.PickPocketRefillDelay", (Minutes)10);
            Values[WorldCfg.CreatureStopForPlayer] = GetDefaultValue<Milliseconds>("Creature.MovingStopTimeForPlayer", (Minutes)3);

            {
                uint defaultChacheId = 0u;
                uint oldCacheId = Values[WorldCfg.ClientCacheVersion].UInt32; // Already Pre-Loaded in WorldServer.Server.StartDB();
                uint clientCacheId = GetDefaultValue("ClientCacheVersion", defaultChacheId);

                // overwrite DB/old value
                if (clientCacheId != oldCacheId && clientCacheId != defaultChacheId)
                {
                    Values[WorldCfg.ClientCacheVersion] = clientCacheId;
                    Log.outInfo(LogFilter.ServerLoading, $"Client cache version set to: {clientCacheId}");
                }
            }

            Values[WorldCfg.GuildNewsLogCount] = GetDefaultValue("Guild.NewsLogRecordsCount", GuildConst.NewsLogMaxRecords);
            if (Values[WorldCfg.GuildNewsLogCount].Int32 > GuildConst.NewsLogMaxRecords)
                Values[WorldCfg.GuildNewsLogCount] = GuildConst.NewsLogMaxRecords;

            Values[WorldCfg.GuildEventLogCount] = GetDefaultValue("Guild.EventLogRecordsCount", GuildConst.EventLogMaxRecords);
            if (Values[WorldCfg.GuildEventLogCount].Int32 > GuildConst.EventLogMaxRecords)
                Values[WorldCfg.GuildEventLogCount] = GuildConst.EventLogMaxRecords;

            Values[WorldCfg.GuildBankEventLogCount] = GetDefaultValue("Guild.BankEventLogRecordsCount", GuildConst.BankLogMaxRecords);
            if (Values[WorldCfg.GuildBankEventLogCount].Int32 > GuildConst.BankLogMaxRecords)
                Values[WorldCfg.GuildBankEventLogCount] = GuildConst.BankLogMaxRecords;

            // Load the CharDelete related config options
            Values[WorldCfg.ChardeleteMethod] = GetDefaultValue("CharDelete.Method", 0);
            Values[WorldCfg.ChardeleteMinLevel] = GetDefaultValue("CharDelete.MinLevel", 0);
            Values[WorldCfg.ChardeleteDeathKnightMinLevel] = GetDefaultValue("CharDelete.DeathKnight.MinLevel", 0);
            Values[WorldCfg.ChardeleteDemonHunterMinLevel] = GetDefaultValue("CharDelete.DemonHunter.MinLevel", 0);
            Values[WorldCfg.ChardeleteKeepDays] = (TimeSpan)GetDefaultValue("CharDelete.KeepDays", (Days)30);

            // No aggro from gray mobs
            {
                Values[WorldCfg.NoGrayAggroAbove] = GetDefaultValue("NoGrayAggro.Above", 0);
                Values[WorldCfg.NoGrayAggroBelow] = GetDefaultValue("NoGrayAggro.Below", 0);
                if (Values[WorldCfg.NoGrayAggroAbove].Int32 > Values[WorldCfg.MaxPlayerLevel].Int32)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"NoGrayAggro.Above ({Values[WorldCfg.NoGrayAggroAbove]}) " +
                        $"must be in range {0}..{Values[WorldCfg.MaxPlayerLevel]}. " +
                        $"Set to {Values[WorldCfg.MaxPlayerLevel]}.");

                    Values[WorldCfg.NoGrayAggroAbove] = Values[WorldCfg.MaxPlayerLevel];
                }

                if (Values[WorldCfg.NoGrayAggroBelow].Int32 > Values[WorldCfg.MaxPlayerLevel].Int32)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"NoGrayAggro.Below ({Values[WorldCfg.NoGrayAggroBelow]}) " +
                        $"must be in range {0}..{Values[WorldCfg.MaxPlayerLevel]}. " +
                        $"Set to {Values[WorldCfg.MaxPlayerLevel]}.");

                    Values[WorldCfg.NoGrayAggroBelow] = Values[WorldCfg.MaxPlayerLevel];
                }

                if (Values[WorldCfg.NoGrayAggroAbove].Int32 > 0 && Values[WorldCfg.NoGrayAggroAbove].Int32 < Values[WorldCfg.NoGrayAggroBelow].Int32)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"NoGrayAggro.Below ({Values[WorldCfg.NoGrayAggroBelow]}) " +
                        $"cannot be greater than NoGrayAggro. " +
                        $"Above ({Values[WorldCfg.NoGrayAggroAbove]}). " +
                        $"Set to {Values[WorldCfg.NoGrayAggroAbove]}.");

                    Values[WorldCfg.NoGrayAggroBelow] = Values[WorldCfg.NoGrayAggroAbove];
                }
            }

            // Respawn Settings
            {
                Values[WorldCfg.RespawnMinCheckIntervalMs] = (TimeSpan)GetDefaultValue("Respawn.MinCheckIntervalMS", (Milliseconds)5000);

                Values[WorldCfg.RespawnDynamicMode] = GetDefaultValue("Respawn.DynamicMode", 0);
                if ((uint)Values[WorldCfg.RespawnDynamicMode].Int32 > 1)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"Invalid value for Respawn. " +
                        $"DynamicMode ({Values[WorldCfg.RespawnDynamicMode]}). " +
                        $"Set to {0}.");

                    Values[WorldCfg.RespawnDynamicMode] = 0;
                }

                Values[WorldCfg.RespawnDynamicEscortNpc] = GetDefaultValue("Respawn.DynamicEscortNPC", false);

                Values[WorldCfg.RespawnGuidWarnLevel] = GetDefaultValue("Respawn.GuidWarnLevel", 12000000L);
                if ((uint)Values[WorldCfg.RespawnGuidWarnLevel].Int64 > 16777215)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"Respawn.GuidWarnLevel ({Values[WorldCfg.RespawnGuidWarnLevel]}) " +
                        $"cannot be greater than maximum GUID ({16777215}). " +
                        $"Set to {12000000}.");

                    Values[WorldCfg.RespawnGuidWarnLevel] = 12000000L;
                }

                Values[WorldCfg.RespawnGuidAlertLevel] = GetDefaultValue("Respawn.GuidAlertLevel", 16000000L);
                if ((ulong)Values[WorldCfg.RespawnGuidAlertLevel].Int64 > 16777215)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"Respawn.GuidWarnLevel ({Values[WorldCfg.RespawnGuidAlertLevel]}) " +
                        $"cannot be greater than maximum GUID ({16777215}). " +
                        $"Set to {16000000}.");

                    Values[WorldCfg.RespawnGuidAlertLevel] = 16000000L;
                }

                Values[WorldCfg.RespawnRestartQuietTime] = GetDefaultValue("Respawn.RestartQuietTime", 3);
                if ((uint)Values[WorldCfg.RespawnRestartQuietTime].Int32 > 23)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"Respawn.RestartQuietTime ({Values[WorldCfg.RespawnRestartQuietTime]}) " +
                        $"must be an hour, between {0} and {23}. " +
                        $"Set to {3}.");

                    Values[WorldCfg.RespawnRestartQuietTime] = 3;
                }

                Values[WorldCfg.RespawnDynamicRateCreature] = GetDefaultValue("Respawn.DynamicRateCreature", 10.0f);
                if (Values[WorldCfg.RespawnDynamicRateCreature].Float < 0.0f)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"Respawn.DynamicRateCreature ({Values[WorldCfg.RespawnDynamicRateCreature]}) " +
                        $"must be positive. " +
                        $"Set to {10}.");

                    Values[WorldCfg.RespawnDynamicRateCreature] = 10.0f;
                }

                Values[WorldCfg.RespawnDynamicMinimumCreature] = GetDefaultValue("Respawn.DynamicMinimumCreature", (Seconds)10);

                Values[WorldCfg.RespawnDynamicRateGameobject] = GetDefaultValue("Respawn.DynamicRateGameObject", 10.0f);
                if (Values[WorldCfg.RespawnDynamicRateGameobject].Float < 0.0f)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"Respawn.DynamicRateGameObject ({Values[WorldCfg.RespawnDynamicRateGameobject]}) " +
                        $"must be positive. " +
                        $"Set to {10}.");

                    Values[WorldCfg.RespawnDynamicRateGameobject] = 10.0f;
                }

                Values[WorldCfg.RespawnDynamicMinimumGameObject] = GetDefaultValue("Respawn.DynamicMinimumGameObject", (Seconds)10);
                Values[WorldCfg.RespawnGuidWarningFrequency] = (TimeSpan)GetDefaultValue<Seconds>("Respawn.WarningFrequency", (Minutes)30);
            }

            Values[WorldCfg.EnableMmaps] = GetDefaultValue("mmap.EnablePathFinding", true);
            Values[WorldCfg.VmapIndoorCheck] = GetDefaultValue("vmap.EnableIndoorCheck", true);

            Values[WorldCfg.MaxWho] = GetDefaultValue("MaxWhoListReturns", 50);

            Values[WorldCfg.StartAllSpells] = GetDefaultValue("PlayerStart.AllSpells", false);
            if (Values[WorldCfg.StartAllSpells].Bool)
                Log.outWarn(LogFilter.ServerLoading, "PlayerStart.AllSpells Enabled - may not function as intended!");

            Values[WorldCfg.HonorAfterDuel] = GetDefaultValue("HonorPointsAfterDuel", 0);
            Values[WorldCfg.ResetDuelCooldowns] = GetDefaultValue("ResetDuelCooldowns", false);
            Values[WorldCfg.ResetDuelHealthMana] = GetDefaultValue("ResetDuelHealthMana", false);
            Values[WorldCfg.StartAllExplored] = GetDefaultValue("PlayerStart.MapsExplored", false);
            Values[WorldCfg.StartAllRep] = GetDefaultValue("PlayerStart.AllReputation", false);

            {
                Values[WorldCfg.PvpTokenEnable] = GetDefaultValue("PvPToken.Enable", false);
                Values[WorldCfg.PvpTokenMapType] = GetDefaultValue("PvPToken.MapAllowType", 4);
                Values[WorldCfg.PvpTokenId] = GetDefaultValue("PvPToken.ItemID", 29434);
                Values[WorldCfg.PvpTokenCount] = GetDefaultValue("PvPToken.ItemCount", 1);
                if (Values[WorldCfg.PvpTokenCount].Int32 < 1)
                    Values[WorldCfg.PvpTokenCount] = 1;
            }

            Values[WorldCfg.NoResetTalentCost] = GetDefaultValue("NoResetTalentsCost", false);
            Values[WorldCfg.ShowKickInWorld] = GetDefaultValue("ShowKickInWorld", false);
            Values[WorldCfg.ShowMuteInWorld] = GetDefaultValue("ShowMuteInWorld", false);
            Values[WorldCfg.ShowBanInWorld] = GetDefaultValue("ShowBanInWorld", false);
            Values[WorldCfg.Numthreads] = GetDefaultValue("MapUpdate.Threads", 1);
            Values[WorldCfg.MaxResultsLookupCommands] = GetDefaultValue("Command.LookupMaxResults", 0);

            // Warden
            Values[WorldCfg.WardenEnabled] = GetDefaultValue("Warden.Enabled", false);
            Values[WorldCfg.WardenNumInjectChecks] = GetDefaultValue("Warden.NumInjectionChecks", 9);
            Values[WorldCfg.WardenNumLuaChecks] = GetDefaultValue("Warden.NumLuaSandboxChecks", 1);
            Values[WorldCfg.WardenNumClientModChecks] = GetDefaultValue("Warden.NumClientModChecks", 1);
            Values[WorldCfg.WardenClientBanDuration] = GetDefaultValue<Seconds>("Warden.BanDuration", (Hours)24);
            Values[WorldCfg.WardenClientCheckHoldoff] = (TimeSpan)GetDefaultValue("Warden.ClientCheckHoldOff", (Seconds)30);
            Values[WorldCfg.WardenClientFailAction] = GetDefaultValue("Warden.ClientCheckFailAction", 0);
            Values[WorldCfg.WardenClientResponseDelay] = (TimeSpan)GetDefaultValue<Seconds>("Warden.ClientResponseDelay", (Minutes)10);

            // Feature System
            Values[WorldCfg.FeatureSystemBpayStoreEnabled] = GetDefaultValue("FeatureSystem.BpayStore.Enabled", false);
            Values[WorldCfg.FeatureSystemCharacterUndeleteEnabled] = GetDefaultValue("FeatureSystem.CharacterUndelete.Enabled", false);
            Values[WorldCfg.FeatureSystemCharacterUndeleteCooldown] = GetDefaultValue<Seconds>("FeatureSystem.CharacterUndelete.Cooldown", (Days)30);
            Values[WorldCfg.FeatureSystemWarModeEnabled] = GetDefaultValue("FeatureSystem.WarMode.Enabled", false);

            // Dungeon finder
            Values[WorldCfg.LfgOptionsmask] = GetDefaultValue("DungeonFinder.OptionsMask", 1);

            // DBC_ItemAttributes
            Values[WorldCfg.DbcEnforceItemAttributes] = GetDefaultValue("DBC.EnforceItemAttributes", true);

            // Accountpassword Secruity
            Values[WorldCfg.AccPasschangesec] = GetDefaultValue("Account.PasswordChangeSecurity", 0);

            // Random Battleground Rewards
            Values[WorldCfg.BgRewardWinnerHonorFirst] = GetDefaultValue("Battleground.RewardWinnerHonorFirst", 27000);
            Values[WorldCfg.BgRewardWinnerConquestFirst] = GetDefaultValue("Battleground.RewardWinnerConquestFirst", 10000);
            Values[WorldCfg.BgRewardWinnerHonorLast] = GetDefaultValue("Battleground.RewardWinnerHonorLast", 13500);
            Values[WorldCfg.BgRewardWinnerConquestLast] = GetDefaultValue("Battleground.RewardWinnerConquestLast", 5000);
            Values[WorldCfg.BgRewardLoserHonorFirst] = GetDefaultValue("Battleground.RewardLoserHonorFirst", 4500);
            Values[WorldCfg.BgRewardLoserHonorLast] = GetDefaultValue("Battleground.RewardLoserHonorLast", 3500);

            // Max instances per hour
            Values[WorldCfg.MaxInstancesPerHour] = GetDefaultValue("AccountInstancesPerHour", 10);

            // Anounce reset of instance to whole party
            Values[WorldCfg.InstancesResetAnnounce] = GetDefaultValue("InstancesResetAnnounce", false);

            // Autobroadcast
            //AutoBroadcast.On
            Values[WorldCfg.AutoBroadcast] = GetDefaultValue("AutoBroadcast.On", false);
            Values[WorldCfg.AutoBroadcastCenter] = GetDefaultValue("AutoBroadcast.Center", 0);
            Values[WorldCfg.AutoBroadcastInterval] = (TimeSpan)GetDefaultValue<Milliseconds>("AutoBroadcast.Timer", (Minutes)10);

            // Guild save interval
            Values[WorldCfg.GuildSaveInterval] = (TimeSpan)GetDefaultValue("Guild.SaveInterval", (Minutes)15);

            // misc
            Values[WorldCfg.PdumpNoPaths] = GetDefaultValue("PlayerDump.DisallowPaths", true);          //Todo: NIY
            Values[WorldCfg.PdumpNoOverwrite] = GetDefaultValue("PlayerDump.DisallowOverwrite", true);  //Todo: NIY

            // Wintergrasp battlefield
            Values[WorldCfg.WintergraspEnable] = GetDefaultValue("Wintergrasp.Enable", false);
            Values[WorldCfg.WintergraspPlrMax] = GetDefaultValue("Wintergrasp.PlayerMax", 100);
            Values[WorldCfg.WintergraspPlrMin] = GetDefaultValue("Wintergrasp.PlayerMin", 0);
            Values[WorldCfg.WintergraspPlrMinLvl] = GetDefaultValue("Wintergrasp.PlayerMinLvl", 77);
            Values[WorldCfg.WintergraspBattletime] = (TimeSpan)GetDefaultValue("Wintergrasp.BattleTimer", (Minutes)30);
            Values[WorldCfg.WintergraspNobattletime] = (TimeSpan)GetDefaultValue("Wintergrasp.NoBattleTimer", (Minutes)150);
            Values[WorldCfg.WintergraspRestartAfterCrash] = (TimeSpan)GetDefaultValue("Wintergrasp.CrashRestartTimer", (Minutes)10);

            // Stats limits
            Values[WorldCfg.StatsLimitsEnable] = GetDefaultValue("Stats.Limits.Enable", false);
            Values[WorldCfg.StatsLimitsDodge] = GetDefaultValue("Stats.Limits.Dodge", 95.0f);
            Values[WorldCfg.StatsLimitsParry] = GetDefaultValue("Stats.Limits.Parry", 95.0f);
            Values[WorldCfg.StatsLimitsBlock] = GetDefaultValue("Stats.Limits.Block", 95.0f);
            Values[WorldCfg.StatsLimitsCrit] = GetDefaultValue("Stats.Limits.Crit", 95.0f);

            //packet spoof punishment
            Values[WorldCfg.PacketSpoofPolicy] = GetDefaultValue("PacketSpoof.Policy", 1);  // log + Kick
            Values[WorldCfg.PacketSpoofBanmode] = GetDefaultValue("PacketSpoof.BanMode", (int)BanMode.Account);
            if (Values[WorldCfg.PacketSpoofBanmode].Int32 == (int)BanMode.Character || Values[WorldCfg.PacketSpoofBanmode].Int32 > (int)BanMode.IP)
                Values[WorldCfg.PacketSpoofBanmode] = (int)BanMode.Account;

            Values[WorldCfg.PacketSpoofBanduration] = GetDefaultValue<Seconds>("PacketSpoof.BanDuration", (Days)1);

            Values[WorldCfg.IpBasedActionLogging] = GetDefaultValue("Allow.IP.Based.Action.Logging", false); //Todo: NIY

            // AHBot
            Values[WorldCfg.AhbotUpdateInterval] = (TimeSpan)GetDefaultValue("AuctionHouseBot.Update.Interval", (Seconds)20);

            Values[WorldCfg.CalculateCreatureZoneAreaData] = GetDefaultValue("Calculate.Creature.Zone.Area.Data", false);
            Values[WorldCfg.CalculateGameobjectZoneAreaData] = GetDefaultValue("Calculate.Gameoject.Zone.Area.Data", false);

            // Black Market
            Values[WorldCfg.BlackmarketEnabled] = GetDefaultValue("BlackMarket.Enabled", true);
            Values[WorldCfg.BlackmarketMaxAuctions] = GetDefaultValue("BlackMarket.MaxAuctions", 12);
            Values[WorldCfg.BlackmarketUpdatePeriod] = (TimeSpan)GetDefaultValue("BlackMarket.UpdatePeriod", (Hours)24);

            // prevent character rename on character customization
            Values[WorldCfg.PreventRenameCustomization] = GetDefaultValue("PreventRenameCharacterOnCustomization", false);

            // Allow 5-man parties to use raid warnings
            Values[WorldCfg.ChatPartyRaidWarnings] = GetDefaultValue("PartyRaidWarnings", false);

            // Allow to cache data queries
            Values[WorldCfg.CacheDataQueries] = GetDefaultValue("CacheDataQueries", true);

            // Check Invalid Position
            Values[WorldCfg.CreatureCheckInvalidPostion] = GetDefaultValue("Creature.CheckInvalidPosition", false); //TODO: NIY
            Values[WorldCfg.GameobjectCheckInvalidPostion] = GetDefaultValue("GameObject.CheckInvalidPosition", false); //TODO: NIY

            // Whether to use LoS from game objects
            Values[WorldCfg.CheckGobjectLos] = GetDefaultValue("CheckGameObjectLoS", true);

            // FactionBalance
            Values[WorldCfg.FactionBalanceLevelCheckDiff] = GetDefaultValue("Pvp.FactionBalance.LevelCheckDiff", 0); //TODO: NIY
            Values[WorldCfg.CallToArms5Pct] = GetDefaultValue("Pvp.FactionBalance.Pct5", 0.6f);
            Values[WorldCfg.CallToArms10Pct] = GetDefaultValue("Pvp.FactionBalance.Pct10", 0.7f);
            Values[WorldCfg.CallToArms20Pct] = GetDefaultValue("Pvp.FactionBalance.Pct20", 0.8f);

            // Specifies if IP addresses can be logged to the database
            Values[WorldCfg.AllowLogginIpAddressesInDatabase] = GetDefaultValue("AllowLoggingIPAddressesInDatabase", true);

            //Loot Settings
            Values[WorldCfg.EnableAELoot] = GetDefaultValue("EnableAELoot", false);

            // call ScriptMgr if we're reloading the configuration
            if (reload)
                Global.ScriptMgr.OnConfigLoad(reload);
        }

        public WorldConfigValue this[WorldCfg index]
        {
            get
            {
                // All Configs must be initialized before use,
                // so skip the check to get an exception
                return _values[index];
            }

            protected set
            {
                if (_values.TryGetValue(index, out var worldConfigValue))
                {
                    if (worldConfigValue.Type != value.Type)
                    {
                        throw new InvalidOperationException(
                            "You cannot change a type of value that has already been initialized.");
                    }
                }

                _values[index] = value;
            }
        }

        public static void SetValue(WorldCfg config, WorldConfigValue value)
        {
            Values[config] = value;
        }

        public IEnumerator GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        private Dictionary<WorldCfg, WorldConfigValue> _values = new();

        private class WorldConfigSet
        {
            public delegate void OverridedCheckLogic(WorldConfigSet self);

            public WorldCfgType ConfigValueType;
            public dynamic MinValue = null;
            public bool ExcludingMinValue;
            public dynamic MaxValue = null;
            public bool ExcludingMaxValue;
            public dynamic[] Variety = null;
            public bool ExcludingVariety;
            public dynamic DefaultValue = null;
            public dynamic Value = null;
            public string ConfigName;
            public OverridedCheckLogic OverridedLogic;
        }
    }

    public enum WorldCfgType : byte
    {
        None = 0,
        Int32,
        Int64,
        UInt32,
        UInt64,
        Float,
        Double,
        Bool,
        Milliseconds,
        Seconds,
        Minutes,
        Hours,
        Days,
        Weeks,
        TimeSpan
    }

    [StructLayout(LayoutKind.Explicit, Size = 9)]
    public readonly struct WorldConfigValue
    {
        [FieldOffset(0)]
        public readonly WorldCfgType Type;
        [FieldOffset(1)]
        private readonly long _Raw;
        [FieldOffset(1)]
        private readonly int _Int32;
        [FieldOffset(1)]
        private readonly uint _UInt32;
        [FieldOffset(1)]
        private readonly long _Int64;
        [FieldOffset(1)]
        private readonly ulong _UInt64;
        [FieldOffset(1)]
        private readonly float _Float;
        [FieldOffset(1)]
        private readonly double _Double;
        [FieldOffset(1)]
        private readonly bool _Bool;
        [FieldOffset(1)]
        private readonly Milliseconds _Milliseconds;
        [FieldOffset(1)]
        private readonly Seconds _Seconds;
        [FieldOffset(1)]
        private readonly Minutes _Minutes;
        [FieldOffset(1)]
        private readonly Hours _Hours;
        [FieldOffset(1)]
        private readonly Days _Days;
        [FieldOffset(1)]
        private readonly Weeks _Weeks;
        [FieldOffset(1)]
        private readonly TimeSpan _TimeSpan;

        public WorldConfigValue(int value) { _Int32 = value; Type = WorldCfgType.Int32; }
        public WorldConfigValue(uint value) { _UInt32 = value; Type = WorldCfgType.UInt32; }
        public WorldConfigValue(long value) { _Int64 = value; Type = WorldCfgType.Int64; }
        //public WorldConfigValue(ulong value) { _UInt64 = value; Type = WorldCfgType.UInt64; }
        public WorldConfigValue(float value) { _Float = value; Type = WorldCfgType.Float; }
        //public WorldConfigValue(double value) { _Double = value; Type = WorldCfgType.Double; }
        public WorldConfigValue(bool value) { _Bool = value; Type = WorldCfgType.Bool; }
        public WorldConfigValue(Milliseconds value) { _Milliseconds = value; Type = WorldCfgType.Milliseconds; }
        public WorldConfigValue(Seconds value) { _Seconds = value; Type = WorldCfgType.Seconds; }
        public WorldConfigValue(Minutes value) { _Minutes = value; Type = WorldCfgType.Minutes; }
        public WorldConfigValue(Hours value) { _Hours = value; Type = WorldCfgType.Hours; }
        public WorldConfigValue(Days value) { _Days = value; Type = WorldCfgType.Days; }
        public WorldConfigValue(Weeks value) { _Weeks = value; Type = WorldCfgType.Weeks; }
        public WorldConfigValue(TimeSpan value) { _TimeSpan = value; Type = WorldCfgType.TimeSpan; }

        public static implicit operator WorldConfigValue(int value) => new(value);
        public static implicit operator WorldConfigValue(uint value) => new(value);
        public static implicit operator WorldConfigValue(long value) => new(value);
        //public static implicit operator WorldConfigValue(ulong value) => new(value);
        public static implicit operator WorldConfigValue(float value) => new(value);
        //public static implicit operator WorldConfigValue(double value) => new(value);
        public static implicit operator WorldConfigValue(bool value) => new(value);
        public static implicit operator WorldConfigValue(Milliseconds value) => new(value);
        public static implicit operator WorldConfigValue(Seconds value) => new(value);
        public static implicit operator WorldConfigValue(Minutes value) => new(value);
        public static implicit operator WorldConfigValue(Hours value) => new(value);
        public static implicit operator WorldConfigValue(Days value) => new(value);
        public static implicit operator WorldConfigValue(Weeks value) => new(value);
        public static implicit operator WorldConfigValue(TimeSpan value) => new(value);
        /*
                public static implicit operator int(WorldConfigValue value) => value.Int32;
                public static implicit operator uint(WorldConfigValue value) => value.UInt32;
                public static implicit operator long(WorldConfigValue value) => value.Int64;
                //public static implicit operator ulong(WorldConfigValue value) => value.UInt64;
                public static implicit operator float(WorldConfigValue value) => value.Float;
                //public static implicit operator double(WorldConfigValue value) => value.Double;
                public static implicit operator bool(WorldConfigValue value) => value.Bool;
                public static implicit operator Milliseconds(WorldConfigValue value) => value.Milliseconds;
                public static implicit operator Seconds(WorldConfigValue value) => value.Seconds;
                public static implicit operator Minutes(WorldConfigValue value) => value.Minutes;
                public static implicit operator Hours(WorldConfigValue value) => value.Hours;
                public static implicit operator Days(WorldConfigValue value) => value.Days;
                public static implicit operator Weeks(WorldConfigValue value) => value.Weeks;
                public static implicit operator TimeSpan(WorldConfigValue value) => value.TimeSpan;
        */

        public override string ToString()
        {
            switch (Type)
            {
                case WorldCfgType.Int32: return _Int32.ToString();
                case WorldCfgType.UInt32: return _UInt32.ToString();
                case WorldCfgType.Int64: return _Int64.ToString();
                //case WorldCfgType.UInt64: return _UInt64.ToString();
                case WorldCfgType.Float: return _Float.ToString();
                //case WorldCfgType.Double: return _Double.ToString();
                case WorldCfgType.Bool: return _Bool.ToString();
                case WorldCfgType.Milliseconds: return _Milliseconds.ToString();
                case WorldCfgType.Seconds: return _Seconds.ToString();
                case WorldCfgType.Minutes: return _Minutes.ToString();
                case WorldCfgType.Hours: return _Hours.ToString();
                case WorldCfgType.Days: return _Days.ToString();
                case WorldCfgType.Weeks: return _Weeks.ToString();
                case WorldCfgType.TimeSpan: return _TimeSpan.ToString();
            }

            return base.ToString();
        }

        public override int GetHashCode()
        {
            return (_UInt64 & (uint)Type << 56).GetHashCode();
        }

        public static bool operator ==(WorldConfigValue left, WorldConfigValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldConfigValue left, WorldConfigValue right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (obj is WorldConfigValue other)
                return Equals(other);

            throw new ArgumentException();
        }

        public bool Equals(WorldConfigValue another)
        {
            return _Raw == another._Raw && Type == another.Type;
        }

        public int Int32
        {
            get
            {
                if (Type == WorldCfgType.Int32)
                    return _Int32;
                else
                    throw new InvalidOperationException();
            }
        }

        public uint UInt32
        {
            get
            {
                if (Type == WorldCfgType.UInt32)
                    return _UInt32;
                else
                    throw new InvalidOperationException();
            }
        }

        public float Float
        {
            get
            {
                if (Type == WorldCfgType.Float)
                    return _Float;
                else
                    throw new InvalidOperationException();
            }
        }

        public long Int64
        {
            get
            {
                if (Type == WorldCfgType.Int64)
                    return _Int64;
                else
                    throw new InvalidOperationException();
            }
        }

        public bool Bool
        {
            get
            {
                if (Type == WorldCfgType.Bool)
                    return _Bool;
                else
                    throw new InvalidOperationException();
            }
        }

        public Milliseconds Milliseconds
        {
            get
            {
                if (Type == WorldCfgType.Milliseconds)
                    return _Milliseconds;
                else
                    throw new InvalidOperationException();
            }
        }

        public Seconds Seconds
        {
            get
            {
                if (Type == WorldCfgType.Seconds)
                    return _Seconds;
                else
                    throw new InvalidOperationException();
            }
        }

        public Minutes Minutes
        {
            get
            {
                if (Type == WorldCfgType.Minutes)
                    return _Minutes;
                else
                    throw new InvalidOperationException();
            }
        }

        public Hours Hours
        {
            get
            {
                if (Type == WorldCfgType.Hours)
                    return _Hours;
                else
                    throw new InvalidOperationException();
            }
        }

        public Days Days
        {
            get
            {
                if (Type == WorldCfgType.Days)
                    return _Days;
                else
                    throw new InvalidOperationException();
            }
        }

        public Weeks Weeks
        {
            get
            {
                if (Type == WorldCfgType.Weeks)
                    return _Weeks;
                else
                    throw new InvalidOperationException();
            }
        }

        public TimeSpan TimeSpan
        {
            get
            {
                if (Type == WorldCfgType.TimeSpan)
                    return _TimeSpan;
                else
                    throw new InvalidOperationException();
            }
        }
    }
}
