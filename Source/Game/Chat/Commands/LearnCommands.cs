﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Entities;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Game.Chat.Commands
{
    [CommandGroup("learn")]
    class LearnCommands
    {
        [Command("", CypherStrings.CommandLearnHelp, RBACPermissions.CommandLearn)]
        static bool HandleLearnCommand(CommandHandler handler, int spellId, [OptionalArg] string allRanksStr)
        {
            Player targetPlayer = handler.GetSelectedPlayerOrSelf();
            if (targetPlayer == null)
            {
                handler.SendSysMessage(CypherStrings.PlayerNotFound);
                return false;
            }

            if (!Global.SpellMgr.IsSpellValid(spellId, handler.GetSession().GetPlayer()))
            {
                handler.SendSysMessage(CypherStrings.CommandSpellBroken, spellId);
                return false;
            }

            bool allRanks = !allRanksStr.IsEmpty() && allRanksStr.Equals("all", StringComparison.OrdinalIgnoreCase);

            if (!allRanks && targetPlayer.HasSpell(spellId))
            {
                if (targetPlayer == handler.GetPlayer())
                    handler.SendSysMessage(CypherStrings.YouKnownSpell);
                else
                    handler.SendSysMessage(CypherStrings.TargetKnownSpell, handler.GetNameLink(targetPlayer));

                return false;
            }

            targetPlayer.SpellBook.Learn(spellId, false);
            if (allRanks)
            {
                while ((spellId = Global.SpellMgr.GetNextSpellInChain(spellId)) != 0)
                    targetPlayer.SpellBook.Learn(spellId, false);
            }

            return true;
        }

        [CommandGroup("all")]
        class LearnAllCommands
        {
            [Command("blizzard", CypherStrings.CommandLearnAllBlizzardHelp, RBACPermissions.CommandLearnAllGm)]
            static bool HandleLearnAllGMCommand(CommandHandler handler)
            {
                foreach (var skillSpell in Global.SpellMgr.GetSkillLineAbilityMapBounds((int)SkillType.Internal))
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(skillSpell.Spell, Difficulty.None);
                    if (spellInfo == null || !Global.SpellMgr.IsSpellValid(spellInfo, handler.GetSession().GetPlayer(), false))
                        continue;

                    handler.GetSession().GetPlayer().SpellBook.Learn(skillSpell.Spell, false);
                }

                handler.SendSysMessage(CypherStrings.LearningGmSkills);
                return true;
            }

            [Command("debug", CypherStrings.CommandLearnAllDebugHelp, RBACPermissions.CommandLearn)]
            static bool HandleLearnDebugSpellsCommand(CommandHandler handler)
            {
                Player  player = handler.GetPlayer();
                player.SpellBook.Learn(63364, false); /* 63364 - Saronite Barrier (reduces damage taken by 99%) */
                player.SpellBook.Learn(1908, false);  /*  1908 - Uber Heal Over Time (heals target to full constantly) */
                player.SpellBook.Learn(27680, false); /* 27680 - Berserk (+500% damage, +150% speed, 10m duration) */
                player.SpellBook.Learn(62555, false); /* 62555 - Berserk (+500% damage, +150% melee haste, 10m duration) */
                player.SpellBook.Learn(64238, false); /* 64238 - Berserk (+900% damage, +150% melee haste, 30m duration) */
                player.SpellBook.Learn(72525, false); /* 72525 - Berserk (+240% damage, +160% haste, infinite duration) */
                player.SpellBook.Learn(66776, false); /* 66776 - Rage (+300% damage, -95% damage taken, +100% speed, infinite duration) */
                return true;
            }

            [Command("crafts", CypherStrings.CommandLearnAllCraftsHelp, RBACPermissions.CommandLearnAllCrafts)]
            static bool HandleLearnAllCraftsCommand(CommandHandler handler, PlayerIdentifier player)
            {
                if (player == null)
                    player = PlayerIdentifier.FromTargetOrSelf(handler);
                if (player == null || !player.IsConnected())
                    return false;

                Player target = player.GetConnectedPlayer();
                foreach (var (_, skillInfo) in CliDB.SkillLineStorage)
                {
                    if ((skillInfo.CategoryID == SkillCategory.Profession || skillInfo.CategoryID == SkillCategory.Secondary) && skillInfo.CanLink) // only prof. with recipes have
                        HandleLearnSkillRecipesHelper(target, skillInfo.Id);
                }

                handler.SendSysMessage(CypherStrings.CommandLearnAllCraft);
                return true;
            }

            [Command("default", CypherStrings.CommandLearnAllDefaultHelp, RBACPermissions.CommandLearnAllDefault)]
            static bool HandleLearnAllDefaultCommand(CommandHandler handler, PlayerIdentifier player)
            {
                if (player == null)
                    player = PlayerIdentifier.FromTargetOrSelf(handler);
                if (player == null || !player.IsConnected())
                    return false;

                Player target = player.GetConnectedPlayer();
                target.LearnDefaultSkills();
                target.LearnCustomSpells();
                target.LearnQuestRewardedSpells();

                handler.SendSysMessage(CypherStrings.CommandLearnAllDefaultAndQuest, handler.GetNameLink(target));
                return true;
            }

            [Command("languages", CypherStrings.CommandLearnAllLanguagesHelp, RBACPermissions.CommandLearnAllLang)]
            static bool HandleLearnAllLangCommand(CommandHandler handler)
            {
                Global.LanguageMgr.ForEachLanguage((_, languageDesc) =>
                {
                    if (languageDesc.SpellId != 0)
                        handler.GetSession().GetPlayer().SpellBook.Learn(languageDesc.SpellId, false);

                    return true;
                });

                handler.SendSysMessage(CypherStrings.CommandLearnAllLang);
                return true;
            }

            [Command("recipes", CypherStrings.CommandLearnAllRecipesHelp, RBACPermissions.CommandLearnAllRecipes)]
            static bool HandleLearnAllRecipesCommand(CommandHandler handler, Tail namePart)
            {
                //  Learns all recipes of specified profession and sets skill to max
                //  Example: .learn all_recipes enchanting

                Player target = handler.GetSelectedPlayer();
                if (target == null)
                {
                    handler.SendSysMessage(CypherStrings.PlayerNotFound);
                    return false;
                }

                if (namePart.IsEmpty())
                    return false;

                string name = "";
                SkillType skillId = 0;
                foreach (var (_, skillInfo) in CliDB.SkillLineStorage)
                {
                    if ((skillInfo.CategoryID != SkillCategory.Profession &&
                        skillInfo.CategoryID != SkillCategory.Secondary) ||
                        skillInfo.CanLink)                            // only prof with recipes have set
                    {
                        continue;
                    }

                    Locale locale = handler.GetSessionDbcLocale();
                    name = skillInfo.DisplayName[locale];
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (!name.Like(namePart))
                    {
                        locale = 0;
                        for (; locale < Locale.Total; ++locale)
                        {
                            name = skillInfo.DisplayName[locale];
                            if (name.IsEmpty())
                                continue;

                            if (name.Like(namePart))
                                break;
                        }
                    }

                    if (locale < Locale.Total)
                    {
                        skillId = skillInfo.Id;
                        break;
                    }
                }

                if (!(name.IsEmpty() && skillId != 0))
                    return false;

                HandleLearnSkillRecipesHelper(target, skillId);

                ushort maxLevel = target.GetPureMaxSkillValue(skillId);
                target.SetSkill(skillId, target.GetSkillStep(skillId), maxLevel, maxLevel);
                handler.SendSysMessage(CypherStrings.CommandLearnAllRecipes, name);
                return true;
            }

            [Command("talents", CypherStrings.CommandLearnAllTalentsHelp, RBACPermissions.CommandLearnAllTalents)]
            static bool HandleLearnAllTalentsCommand(CommandHandler handler)
            {
                Player player = handler.GetSession().GetPlayer();

                foreach (var (_, talentInfo) in CliDB.TalentStorage)
                {
                    TalentTabRecord talentTabInfo = CliDB.TalentTabStorage.LookupByKey(talentInfo.TabID);
                    if (talentTabInfo == null)
                        continue;

                    if (!talentTabInfo.ClassMask.HasClass(player.GetClass()))
                        continue;

                    // search highest talent rank
                    int highestrank = -1;
                    for (byte rank = PlayerConst.MaxTalentRank - 1; rank >= 0; --rank)
                    {
                        if (talentInfo.SpellRank[rank] != 0)
                        {
                            highestrank = rank;
                            break;
                        }
                    }

                    if (highestrank < 0)    // ??? none spells in talent
                        continue;

                    // learn highest rank of talent
                    player.AddTalent(talentInfo, highestrank, player.GetActiveTalentGroup(), true);
                }

                player.SetFreeTalentPoints(0);
                player.SendTalentsInfoData();

                handler.SendSysMessage(CypherStrings.CommandLearnClassTalents);
                return true;
            }

            [Command("pettalents", CypherStrings.CommandLearnAllPettalentHelp, RBACPermissions.CommandLearnMyPetTalents)]
            static bool HandleLearnAllPetTalentsCommand(CommandHandler handler) { return true; }

            static void HandleLearnSkillRecipesHelper(Player player, SkillType skillId)
            {
                var classId = player.GetClass();

                var skillLineAbilities = Global.DB2Mgr.GetSkillLineAbilitiesBySkill(skillId);
                if (skillLineAbilities == null)
                    return;

                foreach (var skillLine in skillLineAbilities)
                {
                    // not high rank
                    if (skillLine.SupercedesSpell != 0)
                        continue;

                    // skip racial skills
                    if (skillLine.RaceMask != 0)
                        continue;

                    // skip wrong class skills
                    if (skillLine.ClassMask != 0 && !skillLine.ClassMask.HasClass(classId))
                        continue;

                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(skillLine.Spell, Difficulty.None);
                    if (spellInfo == null || !Global.SpellMgr.IsSpellValid(spellInfo, player, false))
                        continue;

                    player.SpellBook.Learn(skillLine.Spell, false);
                }
            }
        }

        [CommandGroup("my")]
        class LearnAllMyCommands
        {
            [Command("quests", CypherStrings.CommandLearnMyQuestsHelp, RBACPermissions.CommandLearnAllMySpells)]
            static bool HandleLearnMyQuestsCommand(CommandHandler handler)
            {
                Player player = handler.GetPlayer();
                foreach (var (_, quest) in Global.ObjectMgr.GetQuestTemplates())
                {
                    if (quest.AllowableClasses != 0 && player.SatisfyQuestClass(quest, false))
                        player.LearnQuestRewardedSpells(quest);
                }
                return true;
            }

            [Command("trainer", CypherStrings.CommandLearnMyTrainerHelp, RBACPermissions.CommandLearnAllMySpells)]
            static bool HandleLearnMySpellsCommand(CommandHandler handler)
            {
                ChrClassesRecord classEntry = CliDB.ChrClassesStorage.LookupByKey(handler.GetPlayer().GetClass());
                if (classEntry == null)
                    return true;

                var family = classEntry.SpellClassSet;

                foreach (var (_, entry) in CliDB.SkillLineAbilityStorage)
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(entry.Spell, Difficulty.None);
                    if (spellInfo == null)
                        continue;

                    // skip server-side/triggered spells
                    if (spellInfo.SpellLevel == 0)
                        continue;

                    // skip wrong class/race skills
                    if (!handler.GetSession().GetPlayer().IsSpellFitByClassAndRace(spellInfo.Id))
                        continue;

                    // skip other spell families
                    if (spellInfo.SpellFamilyName != family)
                        continue;

                    // skip broken spells
                    if (!Global.SpellMgr.IsSpellValid(spellInfo, handler.GetSession().GetPlayer(), false))
                        continue;

                    handler.GetSession().GetPlayer().SpellBook.Learn(spellInfo.Id, false);
                }

                handler.SendSysMessage(CypherStrings.CommandLearnClassSpells);
                return true;
            }
        }

        [CommandNonGroup("unlearn", CypherStrings.CommandUnlearnHelp, RBACPermissions.CommandUnlearn)]
        static bool HandleUnLearnCommand(CommandHandler handler, int spellId, [OptionalArg] string allRanksStr)
        {
            Player target = handler.GetSelectedPlayer();
            if (target == null)
            {
                handler.SendSysMessage(CypherStrings.NoCharSelected);
                return false;
            }

            bool allRanks = !allRanksStr.IsEmpty() && allRanksStr.Equals("all", StringComparison.OrdinalIgnoreCase);

            if (allRanks)
                spellId = Global.SpellMgr.GetFirstSpellInChain(spellId);

            if (target.HasSpell(spellId))
                target.SpellBook.Remove(spellId, false, !allRanks);
            else
                handler.SendSysMessage(CypherStrings.ForgetSpell);

            return true;
        }
    }
}
