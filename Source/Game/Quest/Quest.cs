// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using Game.Networking.Packets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public class Quest
    {
        public Quest(SQLFields fields)
        {
            Id = fields.Read<int>(0);
            Type = (QuestType)fields.Read<byte>(1);
            Level = fields.Read<int>(2);
            ScalingFactionGroup = fields.Read<int>(3);
            MaxScalingLevel = fields.Read<int>(4);
            PackageID = fields.Read<int>(5);
            MinLevel = fields.Read<int>(6);
            QuestSortID = fields.Read<short>(7);
            QuestInfoID = fields.Read<ushort>(8);
            SuggestedPlayers = fields.Read<byte>(9);
            NextQuestInChain = fields.Read<int>(10);
            RewardXPDifficulty = fields.Read<int>(11);
            RewardXPMultiplier = fields.Read<float>(12);
            RewardMoneyDifficulty = fields.Read<int>(13);
            RewardMoneyMultiplier = fields.Read<float>(14);
            RewardBonusMoney = fields.Read<int>(15);
            RewardSpell = (int)fields.Read<uint>(16); //TODO: change to signed in DB
            RewardHonor = fields.Read<int>(17);
            RewardKillHonor = fields.Read<int>(18);
            SourceItemId = fields.Read<int>(19);
            RewardArtifactXPDifficulty = fields.Read<int>(20);
            RewardArtifactXPMultiplier = fields.Read<float>(21);
            RewardArtifactCategoryID = fields.Read<int>(22);
            Flags = (QuestFlags)fields.Read<uint>(23);
            FlagsEx = (QuestFlagsEx)fields.Read<uint>(24);
            FlagsEx2 = (QuestFlagsEx2)fields.Read<uint>(25);

            for (int i = 0; i < SharedConst.QuestItemDropCount; ++i)
            {
                RewardItemId[i] = fields.Read<int>(26 + i * 4);
                RewardItemCount[i] = fields.Read<int>(27 + i * 4);
                ItemDrop[i] = fields.Read<int>(28 + i * 4);
                ItemDropQuantity[i] = fields.Read<int>(29 + i * 4);

                if (RewardItemId[i] != 0)
                    ++_rewItemsCount;
            }

            for (int i = 0; i < SharedConst.QuestRewardChoicesCount; ++i)
            {
                RewardChoiceItemId[i] = fields.Read<int>(42 + i * 3);
                RewardChoiceItemCount[i] = fields.Read<int>(43 + i * 3);
                RewardChoiceItemDisplayId[i] = fields.Read<int>(44 + i * 3);

                if (RewardChoiceItemId[i] != 0)
                    ++_rewChoiceItemsCount;
            }

            POIContinent = fields.Read<int>(60);
            POIx = fields.Read<float>(61);
            POIy = fields.Read<float>(62);
            POIPriority = fields.Read<int>(63);

            RewardTitleId = fields.Read<int>(64);
            RewardArenaPoints = fields.Read<int>(65);
            RewardSkillId = fields.Read<int>(66);
            RewardSkillPoints = fields.Read<int>(67);

            QuestGiverPortrait = fields.Read<int>(68);
            QuestGiverPortraitMount = fields.Read<int>(69);
            QuestGiverPortraitModelSceneId = fields.Read<int>(70);
            QuestTurnInPortrait = fields.Read<int>(71);

            for (int i = 0; i < SharedConst.QuestRewardReputationsCount; ++i)
            {
                RewardFactionId[i] = fields.Read<int>(72 + i * 4);
                RewardFactionValue[i] = fields.Read<int>(73 + i * 4);
                RewardFactionOverride[i] = fields.Read<int>(74 + i * 4);
                RewardFactionCapIn[i] = fields.Read<int>(75 + i * 4);
            }

            RewardReputationMask = fields.Read<uint>(92);

            for (int i = 0; i < SharedConst.QuestRewardCurrencyCount; ++i)
            {
                RewardCurrencyId[i] = fields.Read<int>(93 + i * 2);
                RewardCurrencyCount[i] = fields.Read<int>(94 + i * 2);

                if (RewardCurrencyId[i] != 0)
                    ++_rewCurrencyCount;
            }

            SoundAccept = fields.Read<int>(101);
            SoundTurnIn = fields.Read<int>(102);
            AreaGroupID = fields.Read<int>(103);
            LimitTime = fields.Read<uint>(104);
            AllowableRaces = (RaceMask)fields.Read<ulong>(105);
            TreasurePickerID = fields.Read<int>(106);
            Expansion = fields.Read<int>(107);
            ManagedWorldStateID = fields.Read<int>(108);
            QuestSessionBonus = fields.Read<int>(109);

            LogTitle = fields.Read<string>(110);
            LogDescription = fields.Read<string>(111);
            QuestDescription = fields.Read<string>(112);
            AreaDescription = fields.Read<string>(113);
            PortraitGiverText = fields.Read<string>(114);
            PortraitGiverName = fields.Read<string>(115);
            PortraitTurnInText = fields.Read<string>(116);
            PortraitTurnInName = fields.Read<string>(117);
            QuestCompletionLog = fields.Read<string>(118);
        }

        public void LoadRewardDisplaySpell(SQLFields fields)
        {
            int questId = fields.Read<int>(0);
            int spellId = fields.Read<int>(1);
            int idx = fields.Read<int>(2);

            if (idx >= SharedConst.QuestRewardDisplaySpellCount)
            {                
                Log.outError(LogFilter.Sql, 
                    $"Table `quest_reward_display_spell` " +
                    $"has a Spell ({spellId}) set for quest {questId} " +
                    $"at Index {idx} which is out of bounds. Skipped.");                
                return;
            }

            if (Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None) == null)
            {
                Log.outError(LogFilter.Sql, 
                    $"Table `quest_reward_display_spell` " +
                    $"has non-existing Spell ({spellId}) " +
                    $"set for quest {questId}. Skipped.");
                return;
            }

            RewardDisplaySpell[idx] = spellId;
        }

        public void LoadRewardChoiceItems(SQLFields fields)
        {
            for (int i = 0; i < SharedConst.QuestRewardChoicesCount; ++i)
                RewardChoiceItemType[i] = (LootItemType)fields.Read<byte>(1 + i);
        }

        public void LoadQuestDetails(SQLFields fields)
        {
            int questId = fields.Read<int>(0);

            for (int i = 0; i < SharedConst.QuestEmoteCount; ++i)
            {
                ushort emoteId = fields.Read<ushort>(1 + i);
                if (!CliDB.EmotesStorage.ContainsKey(emoteId))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table `quest_details` has non-existing Emote{1 + i} ({emoteId}) " +
                        $"set for quest {questId}. Skipped.");
                    continue;
                }
                DetailsEmote[i] = emoteId;
            }

            for (int i = 0; i < SharedConst.QuestEmoteCount; ++i)
                DetailsEmoteDelay[i] = fields.Read<uint>(5 + i);
        }

        public void LoadQuestRequestItems(SQLFields fields)
        {
            int questId = fields.Read<int>(0);
            EmoteOnComplete = fields.Read<short>(1);
            EmoteOnIncomplete = fields.Read<short>(2);

            if (!CliDB.EmotesStorage.ContainsKey(EmoteOnComplete))
            {
                Log.outError(LogFilter.Sql,
                    $"Table `quest_request_items` " +
                    $"has non-existing EmoteOnComplete ({EmoteOnComplete}) " +
                    $"set for quest {questId}.");
            }

            if (!CliDB.EmotesStorage.ContainsKey(EmoteOnIncomplete))
            {
                Log.outError(LogFilter.Sql,
                    $"Table `quest_request_items` has non-existing EmoteOnIncomplete " +
                    $"({EmoteOnIncomplete}) set for quest {questId}.");
            }

            EmoteOnCompleteDelay = fields.Read<uint>(3);
            EmoteOnIncompleteDelay = fields.Read<uint>(4);
            RequestItemsText = fields.Read<string>(5);
        }

        public void LoadQuestOfferReward(SQLFields fields)
        {
            int questId = fields.Read<int>(0);

            for (int i = 0; i < SharedConst.QuestEmoteCount; ++i)
            {
                short emoteId = fields.Read<short>(1 + i);
                if (emoteId < 0 || !CliDB.EmotesStorage.ContainsKey(emoteId))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table `quest_offer_reward` " +
                        $"has non-existing Emote{1 + i} ({emoteId}) " +
                        $"set for quest {questId}. Skipped.");
                    continue;
                }
                OfferRewardEmote[i] = emoteId;
            }

            for (int i = 0; i < SharedConst.QuestEmoteCount; ++i)
                OfferRewardEmoteDelay[i] = fields.Read<uint>(5 + i);

            OfferRewardText = fields.Read<string>(9);
        }

        public void LoadQuestTemplateAddon(SQLFields fields)
        {
            MaxLevel = fields.Read<byte>(1);
            AllowableClasses = (ClassMask)fields.Read<uint>(2);
            SourceSpellID = fields.Read<int>(3);
            PrevQuestId = fields.Read<int>(4);
            NextQuestId = fields.Read<int>(5);
            ExclusiveGroup = fields.Read<int>(6);
            BreadcrumbForQuestId = fields.Read<int>(7);
            RewardMailTemplateId = fields.Read<int>(8);
            RewardMailDelay = fields.Read<uint>(9);
            RequiredSkillId = (SkillType)fields.Read<ushort>(10);
            RequiredSkillPoints = fields.Read<ushort>(11);
            RequiredMinRepFaction = fields.Read<ushort>(12);
            RequiredMaxRepFaction = fields.Read<ushort>(13);
            RequiredMinRepValue = fields.Read<int>(14);
            RequiredMaxRepValue = fields.Read<int>(15);
            SourceItemIdCount = fields.Read<byte>(16);
            SpecialFlags = (QuestSpecialFlags)fields.Read<byte>(17);
            ScriptId = Global.ObjectMgr.GetScriptId(fields.Read<string>(18));

            if (SpecialFlags.HasAnyFlag(QuestSpecialFlags.AutoAccept))
                Flags |= QuestFlags.AutoAccept;
        }

        public void LoadQuestMailSender(SQLFields fields)
        {
            RewardMailSenderEntry = fields.Read<int>(1);
        }

        public void LoadQuestObjective(SQLFields fields)
        {
            QuestObjective obj = new();
            obj.QuestID = fields.Read<int>(0);
            obj.Id = fields.Read<int>(1);
            obj.Type = (QuestObjectiveType)fields.Read<byte>(2);
            obj.StorageIndex = fields.Read<sbyte>(3);
            obj.ObjectID = fields.Read<int>(4);
            obj.Amount = fields.Read<int>(5);
            obj.Flags = (QuestObjectiveFlags)fields.Read<uint>(6);
            obj.Flags2 = (QuestObjectiveFlags2)fields.Read<uint>(7);
            obj.ProgressBarWeight = fields.Read<float>(8);
            obj.Description = fields.Read<string>(9);

            bool hasCompletionEffect = false;
            for (var i = 10; i < 15; ++i)
            {
                if (!fields.IsNull(i))
                {
                    hasCompletionEffect = true;
                    break;
                }
            }

            if (hasCompletionEffect)
            {
                obj.CompletionEffect = new QuestObjectiveAction();
                if (!fields.IsNull(10))
                    obj.CompletionEffect.GameEventId = fields.Read<int>(10);
                if (!fields.IsNull(11))
                    obj.CompletionEffect.SpellId = fields.Read<int>(11);
                if (!fields.IsNull(12))
                    obj.CompletionEffect.ConversationId = fields.Read<int>(12);
                if (!fields.IsNull(13))
                    obj.CompletionEffect.UpdatePhaseShift = fields.Read<bool>(13);
                if (!fields.IsNull(14))
                    obj.CompletionEffect.UpdateZoneAuras = fields.Read<bool>(14);
            }

            Objectives.Add(obj);
            _usedQuestObjectiveTypes[(int)obj.Type] = true;
        }

        public void LoadQuestObjectiveVisualEffect(SQLFields fields)
        {
            uint objID = fields.Read<uint>(1);

            foreach (QuestObjective obj in Objectives)
            {
                if (obj.Id == objID)
                {
                    byte effectIndex = fields.Read<byte>(3);
                    if (obj.VisualEffects == null)
                        obj.VisualEffects = new int[effectIndex + 1];

                    if (effectIndex >= obj.VisualEffects.Length)
                        Array.Resize(ref obj.VisualEffects, effectIndex + 1);

                    obj.VisualEffects[effectIndex] = fields.Read<int>(4);
                    break;
                }
            }
        }

        public void LoadConditionalConditionalQuestDescription(SQLFields fields)
        {
            int questId = fields.Read<int>(0);
            int playerConditionId = fields.Read<int>(1);
            int questgiverCreatureId = fields.Read<int>(2);
            string text = fields.Read<string>(3);
            string rawLocale = fields.Read<string>(4);

            Locale locale = rawLocale.ToEnum<Locale>();
            if (locale >= Locale.Total)
            {
                Log.outError(LogFilter.Sql, 
                    $"Table `quest_description_conditional` " +
                    $"has invalid locale {rawLocale} set for quest {questId}. Skipped.");
                return;
            }

            QuestConditionalText qct = ConditionalQuestDescription.Find(text => 
                text.PlayerConditionId == playerConditionId 
                && text.QuestgiverCreatureId == questgiverCreatureId
                );

            if (qct == null)
            {
                qct = new();
                ConditionalQuestDescription.Add(qct);
            }

            qct.PlayerConditionId = playerConditionId;
            qct.QuestgiverCreatureId = questgiverCreatureId;
            ObjectManager.AddLocaleString(text, locale, qct.Text);
        }

        public void LoadConditionalConditionalRequestItemsText(SQLFields fields)
        {
            int questId = fields.Read<int>(0);
            int playerConditionId = fields.Read<int>(1);
            int questgiverCreatureId = fields.Read<int>(2);
            string text = fields.Read<string>(3);
            string rawLocale = fields.Read<string>(4);

            Locale locale = rawLocale.ToEnum<Locale>();
            if (locale >= Locale.Total)
            {
                Log.outError(LogFilter.Sql, 
                    $"Table `quest_request_items_conditional` " +
                    $"has invalid locale {rawLocale} " +
                    $"set for quest {questId}. Skipped.");
                return;
            }

            QuestConditionalText qct = ConditionalRequestItemsText.Find(text => 
                text.PlayerConditionId == playerConditionId 
                && text.QuestgiverCreatureId == questgiverCreatureId
                );

            if (qct == null)
            {
                qct = new();
                ConditionalRequestItemsText.Add(qct);
            }

            qct.PlayerConditionId = playerConditionId;
            qct.QuestgiverCreatureId = questgiverCreatureId;
            ObjectManager.AddLocaleString(text, locale, qct.Text);
        }

        public void LoadConditionalConditionalOfferRewardText(SQLFields fields)
        {
            int questId = fields.Read<int>(0);
            int playerConditionId = fields.Read<int>(1);
            int questgiverCreatureId = fields.Read<int>(2);
            string text = fields.Read<string>(3);
            string rawLocale = fields.Read<string>(4);

            Locale locale = rawLocale.ToEnum<Locale>();
            if (locale >= Locale.Total)
            {
                Log.outError(LogFilter.Sql, 
                    $"Table `quest_offer_reward_conditional` " +
                    $"has invalid locale {rawLocale} set for quest {questId}. Skipped.");
                return;
            }

            QuestConditionalText qct = ConditionalOfferRewardText.Find(text => 
                text.PlayerConditionId == playerConditionId 
                && text.QuestgiverCreatureId == questgiverCreatureId
                );

            if (qct == null)
            {
                qct = new();
                ConditionalOfferRewardText.Add(qct);
            }

            qct.PlayerConditionId = playerConditionId;
            qct.QuestgiverCreatureId = questgiverCreatureId;
            ObjectManager.AddLocaleString(text, locale, qct.Text);
        }

        public void LoadConditionalConditionalQuestCompletionLog(SQLFields fields)
        {
            int questId = fields.Read<int>(0);
            int playerConditionId = fields.Read<int>(1);
            int questgiverCreatureId = fields.Read<int>(2);
            string text = fields.Read<string>(3);
            string rawLocale = fields.Read<string>(4);

            Locale locale = rawLocale.ToEnum<Locale>();
            if (locale >= Locale.Total)
            {
                Log.outError(LogFilter.Sql,
                    $"Table `quest_completion_log_conditional` " +
                    $"has invalid locale {rawLocale} " +
                    $"set for quest {questId}. Skipped.");
                return;
            }

            QuestConditionalText qct = ConditionalQuestCompletionLog.Find(text => 
                text.PlayerConditionId == playerConditionId 
                && text.QuestgiverCreatureId == questgiverCreatureId
                );

            if (qct == null)
            {
                qct = new();
                ConditionalQuestCompletionLog.Add(qct);
            }

            qct.PlayerConditionId = playerConditionId;
            qct.QuestgiverCreatureId = questgiverCreatureId;
            ObjectManager.AddLocaleString(text, locale, qct.Text);
        }

        public int XPValue(Player player)
        {
            return XPValue(player, player.GetQuestLevel(this), RewardXPDifficulty, RewardXPMultiplier);
        }

        public static int XPValue(Player player, int questLevel, int xpDifficulty, float xpMultiplier = 1.0f)
        {
            if (player == null)
                return 0;

            int quest_level = (questLevel == -1 ? player.GetLevel() : questLevel);

            QuestXPRecord questXp = CliDB.QuestXPStorage.LookupByKey(questLevel);
            if (questXp == null || xpDifficulty >= 10)
                return 0;

            int diffFactor = 2 * (questLevel - player.GetLevel()) + 20;
            if (diffFactor < 1)
                diffFactor = 1;
            else if (diffFactor > 10)
                diffFactor = 10;

            int xp = RoundXPValue(diffFactor * questXp.Difficulty[xpDifficulty] / 10);

            if (WorldConfig.GetUIntValue(WorldCfg.MinQuestScaledXpRatio) != 0)
            {
                int minScaledXP = RoundXPValue((int)(questXp.Difficulty[xpDifficulty] * xpMultiplier)) * WorldConfig.GetIntValue(WorldCfg.MinQuestScaledXpRatio) / 100;
                xp = Math.Max(minScaledXP, xp);
            }

            return xp;
        }

        public static bool IsTakingQuestEnabled(int questId)
        {
            if (!Global.QuestPoolMgr.IsQuestActive(questId))
                return false;

            return true;
        }

        public int MoneyValue(Player player)
        {
            QuestMoneyRewardRecord money = 
                CliDB.QuestMoneyRewardStorage.LookupByKey(player.GetQuestLevel(this));

            if (money != null)
                return (int)(money.Difficulty[RewardMoneyDifficulty] * RewardMoneyMultiplier);
            else
                return 0;
        }        

        public QuestTagType? GetQuestTag()
        {
            QuestInfoRecord questInfo = CliDB.QuestInfoStorage.LookupByKey(QuestInfoID);
            if (questInfo != null)
                return (QuestTagType)questInfo.Type;

            return null;
        }

        public bool IsImportant()
        {
            var questInfo = CliDB.QuestInfoStorage.LookupByKey(QuestInfoID);
            if (questInfo != null)
                return (questInfo.Modifiers & 0x400) != 0;

            return false;
        }
        
        public void BuildQuestRewards(QuestRewards rewards, Player player)
        {
            rewards.ChoiceItemCount = GetRewChoiceItemsCount();
            rewards.ItemCount = GetRewItemsCount();
            rewards.Money = player.GetQuestMoneyReward(this);
            rewards.XP = player.GetQuestXPReward(this);
            rewards.ArtifactCategoryID = RewardArtifactCategoryID;
            rewards.Title = RewardTitleId;
            rewards.FactionFlags = RewardReputationMask;
            rewards.SpellCompletionDisplayID = RewardDisplaySpell;
            rewards.SpellCompletionID = RewardSpell;
            rewards.SkillLineID = RewardSkillId;
            rewards.NumSkillUps = RewardSkillPoints;
            rewards.TreasurePickerID = TreasurePickerID;

            for (int i = 0; i < SharedConst.QuestRewardChoicesCount; ++i)
            {
                rewards.ChoiceItems[i].LootItemType = RewardChoiceItemType[i];
                rewards.ChoiceItems[i].Item = new ItemInstance();
                rewards.ChoiceItems[i].Item.ItemID = RewardChoiceItemId[i];
                rewards.ChoiceItems[i].Quantity = RewardChoiceItemCount[i];
            }

            for (int i = 0; i < SharedConst.QuestRewardItemCount; ++i)
            {
                rewards.ItemID[i] = RewardItemId[i];
                rewards.ItemQty[i] = RewardItemCount[i];
            }

            for (int i = 0; i < SharedConst.QuestRewardReputationsCount; ++i)
            {
                rewards.FactionID[i] = RewardFactionId[i];
                rewards.FactionOverride[i] = RewardFactionOverride[i];
                rewards.FactionValue[i] = RewardFactionValue[i];
                rewards.FactionCapIn[i] = RewardFactionCapIn[i];
            }

            for (int i = 0; i < SharedConst.QuestRewardCurrencyCount; ++i)
            {
                rewards.CurrencyID[i] = RewardCurrencyId[i];
                rewards.CurrencyQty[i] = RewardCurrencyCount[i];
            }
        }

        public int GetRewMoneyMaxLevel()
        {
            // If Quest has flag to not give money on max level, it's 0
            if (HasAnyFlag(QuestFlags.NoMoneyForXp))
                return 0;

            // Else, return the rewarded copper sum modified by the rate
            return (int)(RewardBonusMoney * WorldConfig.GetFloatValue(WorldCfg.RateMoneyMaxLevelQuest));
        }

        public bool IsAutoAccept()
        {
            return !WorldConfig.GetBoolValue(WorldCfg.QuestIgnoreAutoAccept) 
                && HasAnyFlag(QuestFlags.AutoAccept);
        }

        public bool IsTurnIn()
        {
            return !WorldConfig.GetBoolValue(WorldCfg.QuestIgnoreAutoComplete) 
                && Type == QuestType.TurnIn;
        }

        public bool IsRaidQuest(Difficulty difficulty)
        {
            switch ((QuestInfos)QuestInfoID)
            {
                case QuestInfos.Raid:
                    return true;
                case QuestInfos.Raid10:
                    return difficulty == Difficulty.Raid10N || difficulty == Difficulty.Raid10HC;
                case QuestInfos.Raid25:
                    return difficulty == Difficulty.Raid25N || difficulty == Difficulty.Raid25HC;
                default:
                    break;
            }

            if (Flags.HasAnyFlag(QuestFlags.RaidGroupOk))
                return true;

            return false;
        }

        public bool IsAllowedInRaid(Difficulty difficulty)
        {
            if (IsRaidQuest(difficulty))
                return true;

            return WorldConfig.GetBoolValue(WorldCfg.QuestIgnoreRaid);
        }

        public int CalculateHonorGain(int level)
        {
            int honor = 0;
            return honor;
        }

        public bool CanIncreaseRewardedQuestCounters()
        {
            // Dungeon Finder/Daily/Repeatable (if not weekly, monthly or seasonal) quests are never considered rewarded serverside.
            // This affects counters and client requests for completed quests.
            return (!IsDFQuest() && !IsDaily() 
                && (!IsRepeatable() || IsWeekly() || IsMonthly() || IsSeasonal()));
        }

        public void InitializeQueryData()
        {
            for (var loc = Locale.enUS; loc < Locale.Total; ++loc)
                response[(int)loc] = BuildQueryData(loc, null);
        }

        public QueryQuestInfoResponse BuildQueryData(Locale loc, Player player)
        {
            QueryQuestInfoResponse response = new();

            response.Allow = true;
            response.QuestID = Id;

            response.Info.LogTitle = LogTitle;
            response.Info.LogDescription = LogDescription;
            response.Info.QuestDescription = QuestDescription;
            response.Info.AreaDescription = AreaDescription;
            response.Info.QuestCompletionLog = QuestCompletionLog;
            response.Info.PortraitGiverText = PortraitGiverText;
            response.Info.PortraitGiverName = PortraitGiverName;
            response.Info.PortraitTurnInText = PortraitTurnInText;
            response.Info.PortraitTurnInName = PortraitTurnInName;

            if (loc != Locale.enUS)
            {
                var questTemplateLocale = Global.ObjectMgr.GetQuestLocale(Id);
                if (questTemplateLocale != null)
                {
                    ObjectManager.GetLocaleString(questTemplateLocale.LogTitle, loc, ref response.Info.LogTitle);
                    ObjectManager.GetLocaleString(questTemplateLocale.LogDescription, loc, ref response.Info.LogDescription);
                    ObjectManager.GetLocaleString(questTemplateLocale.QuestDescription, loc, ref response.Info.QuestDescription);
                    ObjectManager.GetLocaleString(questTemplateLocale.AreaDescription, loc, ref response.Info.AreaDescription);
                    ObjectManager.GetLocaleString(questTemplateLocale.QuestCompletionLog, loc, ref response.Info.QuestCompletionLog);
                    ObjectManager.GetLocaleString(questTemplateLocale.PortraitGiverText, loc, ref response.Info.PortraitGiverText);
                    ObjectManager.GetLocaleString(questTemplateLocale.PortraitGiverName, loc, ref response.Info.PortraitGiverName);
                    ObjectManager.GetLocaleString(questTemplateLocale.PortraitTurnInText, loc, ref response.Info.PortraitTurnInText);
                    ObjectManager.GetLocaleString(questTemplateLocale.PortraitTurnInName, loc, ref response.Info.PortraitTurnInName);
                }
            }

            response.Info.QuestID = Id;
            response.Info.QuestType = Type;
            response.Info.QuestLevel = Level;
            response.Info.QuestScalingFactionGroup = ScalingFactionGroup;
            response.Info.QuestMaxScalingLevel = MaxScalingLevel;
            response.Info.QuestPackageID = PackageID;
            response.Info.QuestMinLevel = MinLevel;
            response.Info.QuestSortID = QuestSortID;
            response.Info.QuestInfoID = QuestInfoID;
            response.Info.SuggestedGroupNum = SuggestedPlayers;
            response.Info.RewardNextQuest = NextQuestInChain;
            response.Info.RewardXPDifficulty = RewardXPDifficulty;
            response.Info.RewardXPMultiplier = RewardXPMultiplier;

            if (!HasAnyFlag(QuestFlags.HideReward))
                response.Info.RewardMoney = player != null ? player.GetQuestMoneyReward(this) : 0;

            response.Info.RewardMoneyDifficulty = RewardMoneyDifficulty;
            response.Info.RewardMoneyMultiplier = RewardMoneyMultiplier;
            response.Info.RewardBonusMoney = GetRewMoneyMaxLevel();
            response.Info.RewardDisplaySpell = RewardDisplaySpell;

            response.Info.RewardSpell = RewardSpell;

            response.Info.RewardHonor = RewardHonor;
            response.Info.RewardKillHonor = RewardKillHonor;

            response.Info.RewardArtifactXPDifficulty = RewardArtifactXPDifficulty;
            response.Info.RewardArtifactXPMultiplier = RewardArtifactXPMultiplier;
            response.Info.RewardArtifactCategoryID = RewardArtifactCategoryID;

            response.Info.StartItem = SourceItemId;
            response.Info.Flags = Flags;
            response.Info.FlagsEx = FlagsEx;
            response.Info.FlagsEx2 = FlagsEx2;
            response.Info.RewardTitle = RewardTitleId;
            response.Info.RewardArenaPoints = RewardArenaPoints;
            response.Info.RewardSkillLineID = RewardSkillId;
            response.Info.RewardNumSkillUps = RewardSkillPoints;
            response.Info.RewardFactionFlags = RewardReputationMask;
            response.Info.PortraitGiver = QuestGiverPortrait;
            response.Info.PortraitGiverMount = QuestGiverPortraitMount;
            response.Info.PortraitGiverModelSceneID = QuestGiverPortraitModelSceneId;
            response.Info.PortraitTurnIn = QuestTurnInPortrait;

            for (byte i = 0; i < SharedConst.QuestItemDropCount; ++i)
            {
                response.Info.ItemDrop[i] = ItemDrop[i];
                response.Info.ItemDropQuantity[i] = ItemDropQuantity[i];
            }

            if (!HasAnyFlag(QuestFlags.HideReward))
            {
                for (byte i = 0; i < SharedConst.QuestRewardItemCount; ++i)
                {
                    response.Info.RewardItems[i] = RewardItemId[i];
                    response.Info.RewardAmount[i] = RewardItemCount[i];
                }
                for (byte i = 0; i < SharedConst.QuestRewardChoicesCount; ++i)
                {
                    response.Info.UnfilteredChoiceItems[i].ItemID = RewardChoiceItemId[i];
                    response.Info.UnfilteredChoiceItems[i].Quantity = RewardChoiceItemCount[i];
                }
            }

            for (byte i = 0; i < SharedConst.QuestRewardReputationsCount; ++i)
            {
                response.Info.RewardFactionID[i] = RewardFactionId[i];
                response.Info.RewardFactionValue[i] = RewardFactionValue[i];
                response.Info.RewardFactionOverride[i] = RewardFactionOverride[i];
                response.Info.RewardFactionCapIn[i] = RewardFactionCapIn[i];
            }

            response.Info.POIContinent = POIContinent;
            response.Info.POIx = POIx;
            response.Info.POIy = POIy;
            response.Info.POIPriority = POIPriority;

            response.Info.AllowableRaces = AllowableRaces;
            response.Info.TreasurePickerID = TreasurePickerID;
            response.Info.Expansion = Expansion;
            response.Info.ManagedWorldStateID = ManagedWorldStateID;
            response.Info.QuestSessionBonus = 0; //GetQuestSessionBonus(); // this is only sent while quest session is active
            response.Info.QuestGiverCreatureID = 0; // only sent during npc interaction

            foreach (QuestObjective questObjective in Objectives)
            {
                response.Info.Objectives.Add(questObjective);

                if (loc != Locale.enUS)
                {
                    var questObjectivesLocale = Global.ObjectMgr.GetQuestObjectivesLocale(questObjective.Id);
                    if (questObjectivesLocale != null)
                        ObjectManager.GetLocaleString(questObjectivesLocale.Description, loc, ref response.Info.Objectives.Last().Description);
                }
            }

            for (int i = 0; i < SharedConst.QuestRewardCurrencyCount; ++i)
            {
                response.Info.RewardCurrencyID[i] = RewardCurrencyId[i];
                response.Info.RewardCurrencyQty[i] = RewardCurrencyCount[i];
            }

            response.Info.AcceptedSoundKitID = SoundAccept;
            response.Info.CompleteSoundKitID = SoundTurnIn;
            response.Info.AreaGroupID = AreaGroupID;
            response.Info.TimeAllowed = LimitTime;

            response.Write();
            return response;
        }

        public static int RoundXPValue(int xp)
        {
            if (xp <= 100)
                return 5 * ((xp + 2) / 5);
            else if (xp <= 500)
                return 10 * ((xp + 5) / 10);
            else if (xp <= 1000)
                return 25 * ((xp + 12) / 25);
            else
                return 50 * ((xp + 25) / 50);
        }

        public bool HasAnyFlag(QuestFlags flag) { return (Flags & flag) != 0; }
        public bool HasAnyFlag(QuestFlagsEx flag) { return (FlagsEx & flag) != 0; }
        public bool HasAnyFlag(QuestFlagsEx2 flag) { return (FlagsEx2 & flag) != 0; }
        public bool HasAnyFlag(QuestSpecialFlags flag) { return (SpecialFlags & flag) != 0; }

        public bool HasFlag(QuestFlags flag) { return (Flags & flag) == flag; }
        public bool HasFlag(QuestFlagsEx flag) { return (FlagsEx & flag) == flag; }
        public bool HasFlag(QuestFlagsEx2 flag) { return (FlagsEx2 & flag) == flag; }
        public bool HasFlag(QuestSpecialFlags flag) { return (SpecialFlags & flag) == flag; }

        public void SetSpecialFlag(QuestSpecialFlags flag) { SpecialFlags |= flag; }

        public bool HasQuestObjectiveType(QuestObjectiveType type) 
        { 
            return _usedQuestObjectiveTypes[(int)type]; 
        }

        public bool IsAutoPush() { return HasAnyFlag(QuestFlagsEx.AutoPush); }
        public bool IsWorldQuest() { return HasAnyFlag(QuestFlagsEx.IsWorldQuest); }

        // Possibly deprecated flag
        public bool IsUnavailable() { return HasAnyFlag(QuestFlags.Deprecated); }

        // table data accessors:
        public bool IsRepeatable() { return HasAnyFlag(QuestSpecialFlags.Repeatable); }
        public bool IsDaily() { return HasAnyFlag(QuestFlags.Daily); }
        public bool IsWeekly() { return HasAnyFlag(QuestFlags.Weekly); }
        public bool IsMonthly() { return HasAnyFlag(QuestSpecialFlags.Monthly); }
        
        public bool IsSeasonal()
        {
            return (QuestSortID == -(int)QuestSort.Seasonal || QuestSortID == -(int)QuestSort.Special || QuestSortID == -(int)QuestSort.LunarFestival
                || QuestSortID == -(int)QuestSort.Midsummer || QuestSortID == -(int)QuestSort.Brewfest || QuestSortID == -(int)QuestSort.LoveIsInTheAir
                || QuestSortID == -(int)QuestSort.Noblegarden) && !IsRepeatable();
        }

        public bool IsDailyOrWeekly() { return HasAnyFlag(QuestFlags.Daily | QuestFlags.Weekly); }
        public bool IsDFQuest() { return HasAnyFlag(QuestSpecialFlags.DfQuest); }
        public bool IsPushedToPartyOnAccept() { return HasAnyFlag(QuestSpecialFlags.AutoPushToParty); }
        
        public int GetRewChoiceItemsCount() { return _rewChoiceItemsCount; }
        public int GetRewItemsCount() { return _rewItemsCount; }
        public int GetRewCurrencyCount() { return _rewCurrencyCount; }

        public void SetEventIdForQuest(ushort eventId) { _eventIdForQuest = eventId; }
        public ushort GetEventIdForQuest() { return _eventIdForQuest; }

    #region Fields
        public int Id;
        public QuestType Type;
        public int PackageID;
        public int QuestSortID;
        public int QuestInfoID;
        public int SuggestedPlayers;
        public int NextQuestInChain { get; set; }
        public int RewardXPDifficulty;
        public float RewardXPMultiplier;
        public int RewardMoneyDifficulty;
        public float RewardMoneyMultiplier;
        public int RewardBonusMoney;
        public int[] RewardDisplaySpell = new int[SharedConst.QuestRewardDisplaySpellCount];
        public int RewardSpell { get; set; }
        public int RewardHonor;
        public int RewardKillHonor;
        public int RewardArtifactXPDifficulty;
        public float RewardArtifactXPMultiplier;
        public int RewardArtifactCategoryID;
        public int SourceItemId { get; set; }
        public QuestFlags Flags { get; set; }
        public QuestFlagsEx FlagsEx;
        public QuestFlagsEx2 FlagsEx2;
        public int[] RewardItemId = new int[SharedConst.QuestRewardItemCount];
        public int[] RewardItemCount = new int[SharedConst.QuestRewardItemCount];
        public int[] ItemDrop = new int[SharedConst.QuestItemDropCount];
        public int[] ItemDropQuantity = new int[SharedConst.QuestItemDropCount];
        public LootItemType[] RewardChoiceItemType = new LootItemType[SharedConst.QuestRewardChoicesCount];
        public int[] RewardChoiceItemId = new int[SharedConst.QuestRewardChoicesCount];
        public int[] RewardChoiceItemCount = new int[SharedConst.QuestRewardChoicesCount];
        public int[] RewardChoiceItemDisplayId = new int[SharedConst.QuestRewardChoicesCount];
        public int POIContinent;
        public float POIx;
        public float POIy;
        public int POIPriority;
        public int RewardTitleId { get; set; }
        public int RewardArenaPoints;
        public int RewardSkillId;
        public int RewardSkillPoints;
        public int QuestGiverPortrait;
        public int QuestGiverPortraitMount;
        public int QuestGiverPortraitModelSceneId;
        public int QuestTurnInPortrait;
        public int[] RewardFactionId = new int[SharedConst.QuestRewardReputationsCount];
        public int[] RewardFactionValue = new int[SharedConst.QuestRewardReputationsCount];
        public int[] RewardFactionOverride = new int[SharedConst.QuestRewardReputationsCount];
        public int[] RewardFactionCapIn = new int[SharedConst.QuestRewardReputationsCount];
        public uint RewardReputationMask;
        public int[] RewardCurrencyId = new int[SharedConst.QuestRewardCurrencyCount];
        public int[] RewardCurrencyCount = new int[SharedConst.QuestRewardCurrencyCount];
        public int SoundAccept { get; set; }
        public int SoundTurnIn { get; set; }
        public int AreaGroupID;
        public long LimitTime;
        public RaceMask AllowableRaces { get; set; }
        public int TreasurePickerID;
        public int Expansion;
        public int ManagedWorldStateID;
        public int QuestSessionBonus;
        public List<QuestObjective> Objectives = new();
        public string LogTitle = string.Empty;
        public string LogDescription = string.Empty;
        public string QuestDescription = string.Empty;
        public string AreaDescription = string.Empty;
        public string PortraitGiverText = string.Empty;
        public string PortraitGiverName = string.Empty;
        public string PortraitTurnInText = string.Empty;
        public string PortraitTurnInName = string.Empty;
        public string QuestCompletionLog = string.Empty;

        // quest_description_conditional
        public List<QuestConditionalText> ConditionalQuestDescription = new();

        // quest_completion_log_conditional
        public List<QuestConditionalText> ConditionalQuestCompletionLog = new();

        // quest_detais table
        public int[] DetailsEmote = new int[SharedConst.QuestEmoteCount];
        public uint[] DetailsEmoteDelay = new uint[SharedConst.QuestEmoteCount];

        // quest_request_items table
        public int EmoteOnComplete;
        public int EmoteOnIncomplete;
        public uint EmoteOnCompleteDelay;
        public uint EmoteOnIncompleteDelay;
        public string RequestItemsText = string.Empty;

        // quest_request_items_conditional
        public List<QuestConditionalText> ConditionalRequestItemsText = new();

        // quest_offer_reward table
        public int[] OfferRewardEmote = new int[SharedConst.QuestEmoteCount];
        public uint[] OfferRewardEmoteDelay = new uint[SharedConst.QuestEmoteCount];
        public string OfferRewardText = string.Empty;

        // quest_offer_reward_conditional
        public List<QuestConditionalText> ConditionalOfferRewardText = new();

        // quest_template_addon table (custom data)
        public int MaxLevel { get; set; }
        public int MinLevel { get; set; }
        public int Level { get; set; }
        public int ScalingFactionGroup { get; set; }
        public int MaxScalingLevel { get; set; }
        public ClassMask AllowableClasses { get; set; }
        public int SourceSpellID { get; set; }
        public int PrevQuestId { get; set; }
        public int NextQuestId { get; set; }
        public int ExclusiveGroup { get; set; }
        public int BreadcrumbForQuestId { get; set; }
        public int RewardMailTemplateId { get; set; }
        public uint RewardMailDelay { get; set; }
        public SkillType RequiredSkillId { get; set; }
        public int RequiredSkillPoints { get; set; }
        public int RequiredMinRepFaction { get; set; }
        public int RequiredMinRepValue { get; set; }
        public int RequiredMaxRepFaction { get; set; }
        public int RequiredMaxRepValue { get; set; }
        public int SourceItemIdCount { get; set; }
        public int RewardMailSenderEntry { get; set; }
        public QuestSpecialFlags SpecialFlags { get; set; } // custom flags, not sniffed/WDB
        public BitArray _usedQuestObjectiveTypes = new((int)QuestObjectiveType.Max);
        public int ScriptId { get; set; }

        public List<int> DependentPreviousQuests = new();
        public List<int> DependentBreadcrumbQuests = new();
        public QueryQuestInfoResponse[] response = new QueryQuestInfoResponse[(int)Locale.Total];

        int _rewChoiceItemsCount;
        int _rewItemsCount;
        int _rewCurrencyCount;
        ushort _eventIdForQuest;
        #endregion
    }

    public class QuestStatusData
    {
        public ushort Slot = SharedConst.MaxQuestLogSize;
        public QuestStatus Status;
        public long AcceptTime;
        public uint Timer;
        public bool Explored;
    }

    public class QuestGreeting
    {
        public QuestGreeting()
        {
            Text = "";
        }

        public QuestGreeting(ushort emoteType, uint emoteDelay, string text)
        {
            EmoteType = emoteType;
            EmoteDelay = emoteDelay;
            Text = text;
        }

        public ushort EmoteType;
        public uint EmoteDelay;
        public string Text;
    }

    public class QuestGreetingLocale
    {
        public StringArray Greeting = new((int)Locale.Total);
    }

    public class QuestTemplateLocale
    {
        public StringArray LogTitle = new((int)Locale.Total);
        public StringArray LogDescription = new((int)Locale.Total);
        public StringArray QuestDescription = new((int)Locale.Total);
        public StringArray AreaDescription = new((int)Locale.Total);
        public StringArray PortraitGiverText = new((int)Locale.Total);
        public StringArray PortraitGiverName = new((int)Locale.Total);
        public StringArray PortraitTurnInText = new((int)Locale.Total);
        public StringArray PortraitTurnInName = new((int)Locale.Total);
        public StringArray QuestCompletionLog = new((int)Locale.Total);
    }

    public class QuestRequestItemsLocale
    {
        public StringArray CompletionText = new((int)Locale.Total);
    }

    public class QuestObjectivesLocale
    {
        public StringArray Description = new((int)Locale.Total);
    }

    public class QuestOfferRewardLocale
    {
        public StringArray RewardText = new((int)Locale.Total);
    }

    public class QuestConditionalText
    {
        public int PlayerConditionId;
        public int QuestgiverCreatureId;
        public StringArray Text = new((int)Locale.Total);
    }

    public class QuestObjectiveAction
    {
        public int? GameEventId;
        public int? SpellId;
        public int? ConversationId;
        public bool UpdatePhaseShift;
        public bool UpdateZoneAuras;
    }

    public class QuestObjective
    {
        public int Id;
        public int QuestID;
        public QuestObjectiveType Type;
        public int StorageIndex;
        public int ObjectID;
        public int Amount;
        public QuestObjectiveFlags Flags;
        public QuestObjectiveFlags2 Flags2;
        public float ProgressBarWeight;
        public string Description;
        public int[] VisualEffects = Array.Empty<int>();
        public QuestObjectiveAction CompletionEffect;

        public bool IsStoringValue()
        {
            switch (Type)
            {
                case QuestObjectiveType.Monster:
                case QuestObjectiveType.Item:
                case QuestObjectiveType.GameObject:
                case QuestObjectiveType.TalkTo:
                case QuestObjectiveType.PlayerKills:
                case QuestObjectiveType.WinPvpPetBattles:
                case QuestObjectiveType.HaveCurrency:
                case QuestObjectiveType.ObtainCurrency:
                case QuestObjectiveType.IncreaseReputation:
                    return true;
                default:
                    break;
            }
            return false;
        }
        
        public bool IsStoringFlag()
        {
            switch (Type)
            {
                case QuestObjectiveType.AreaTrigger:
                case QuestObjectiveType.WinPetBattleAgainstNpc:
                case QuestObjectiveType.DefeatBattlePet:
                case QuestObjectiveType.CriteriaTree:
                case QuestObjectiveType.AreaTriggerEnter:
                case QuestObjectiveType.AreaTriggerExit:
                    return true;
                default:
                    break;
            }
            return false;
        }

        public static bool CanAlwaysBeProgressedInRaid(QuestObjectiveType type)
        {
            switch (type)
            {
                case QuestObjectiveType.Item:
                case QuestObjectiveType.Currency:
                case QuestObjectiveType.LearnSpell:
                case QuestObjectiveType.MinReputation:
                case QuestObjectiveType.MaxReputation:
                case QuestObjectiveType.Money:
                case QuestObjectiveType.HaveCurrency:
                case QuestObjectiveType.IncreaseReputation:
                    return true;
                default:
                    break;
            }
            return false;
        }
    }
}
