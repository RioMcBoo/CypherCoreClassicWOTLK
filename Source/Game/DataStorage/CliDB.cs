// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;

namespace Game.DataStorage
{
    public class CliDB
    {
        public static BitSet LoadStores(string dataPath, Locale defaultLocale)
        {
            uint oldMSTime = Time.GetMSTime();

            string db2Path = $"{dataPath}/dbc";

            var data = new DB2LoadData();
            data.path = $"{db2Path}/{defaultLocale}";
            data.availableLocales = new((int)Locale.Total);
            data.counter = 0;

            foreach (var dir in Directory.GetDirectories(db2Path))
            {
                Locale locale = Path.GetFileName(dir).ToEnum<Locale>();
                if (SharedConst.IsValidLocale(locale))
                    data.availableLocales[(int)locale] = true;
            }

            if (!data.availableLocales[(int)defaultLocale])
                return null;

            AchievementStorage.ReadDB2(data, "Achievement.db2", HotfixStatements.SEL_ACHIEVEMENT, HotfixStatements.SEL_ACHIEVEMENT_LOCALE);
            AchievementCategoryStorage.ReadDB2(data, "Achievement_Category.db2", HotfixStatements.SEL_ACHIEVEMENT_CATEGORY, HotfixStatements.SEL_ACHIEVEMENT_CATEGORY_LOCALE);
            AdventureJournalStorage.ReadDB2(data, "AdventureJournal.db2", HotfixStatements.SEL_ADVENTURE_JOURNAL, HotfixStatements.SEL_ADVENTURE_JOURNAL_LOCALE);
            AdventureMapPOIStorage.ReadDB2(data, "AdventureMapPOI.db2", HotfixStatements.SEL_ADVENTURE_MAP_POI, HotfixStatements.SEL_ADVENTURE_MAP_POI_LOCALE);
            AnimationDataStorage.ReadDB2(data, "AnimationData.db2", HotfixStatements.SEL_ANIMATION_DATA);
            AnimKitStorage.ReadDB2(data, "AnimKit.db2", HotfixStatements.SEL_ANIM_KIT);
            AreaGroupMemberStorage.ReadDB2(data, "AreaGroupMember.db2", HotfixStatements.SEL_AREA_GROUP_MEMBER);
            AreaTableStorage.ReadDB2(data, "AreaTable.db2", HotfixStatements.SEL_AREA_TABLE, HotfixStatements.SEL_AREA_TABLE_LOCALE);
            AreaTriggerStorage.ReadDB2(data, "AreaTrigger.db2", HotfixStatements.SEL_AREA_TRIGGER);
            ArmorLocationStorage.ReadDB2(data, "ArmorLocation.db2", HotfixStatements.SEL_ARMOR_LOCATION);
            ArtifactStorage.ReadDB2(data, "Artifact.db2", HotfixStatements.SEL_ARTIFACT, HotfixStatements.SEL_ARTIFACT_LOCALE);
            ArtifactAppearanceStorage.ReadDB2(data, "ArtifactAppearance.db2", HotfixStatements.SEL_ARTIFACT_APPEARANCE, HotfixStatements.SEL_ARTIFACT_APPEARANCE_LOCALE);
            ArtifactAppearanceSetStorage.ReadDB2(data, "ArtifactAppearanceSet.db2", HotfixStatements.SEL_ARTIFACT_APPEARANCE_SET, HotfixStatements.SEL_ARTIFACT_APPEARANCE_SET_LOCALE);
            ArtifactCategoryStorage.ReadDB2(data, "ArtifactCategory.db2", HotfixStatements.SEL_ARTIFACT_CATEGORY);
            ArtifactPowerStorage.ReadDB2(data, "ArtifactPower.db2", HotfixStatements.SEL_ARTIFACT_POWER);
            ArtifactPowerLinkStorage.ReadDB2(data, "ArtifactPowerLink.db2", HotfixStatements.SEL_ARTIFACT_POWER_LINK);
            ArtifactPowerPickerStorage.ReadDB2(data, "ArtifactPowerPicker.db2", HotfixStatements.SEL_ARTIFACT_POWER_PICKER);
            ArtifactPowerRankStorage.ReadDB2(data, "ArtifactPowerRank.db2", HotfixStatements.SEL_ARTIFACT_POWER_RANK);
            ArtifactQuestXPStorage.ReadDB2(data, "ArtifactQuestXP.db2", HotfixStatements.SEL_ARTIFACT_QUEST_XP);
            ArtifactTierStorage.ReadDB2(data, "ArtifactTier.db2", HotfixStatements.SEL_ARTIFACT_TIER);
            ArtifactUnlockStorage.ReadDB2(data, "ArtifactUnlock.db2", HotfixStatements.SEL_ARTIFACT_UNLOCK);
            AuctionHouseStorage.ReadDB2(data, "AuctionHouse.db2", HotfixStatements.SEL_AUCTION_HOUSE, HotfixStatements.SEL_AUCTION_HOUSE_LOCALE);
            AzeriteEmpoweredItemStorage.ReadDB2(data, "AzeriteEmpoweredItem.db2", HotfixStatements.SEL_AZERITE_EMPOWERED_ITEM);
            AzeriteEssenceStorage.ReadDB2(data, "AzeriteEssence.db2", HotfixStatements.SEL_AZERITE_ESSENCE, HotfixStatements.SEL_AZERITE_ESSENCE_LOCALE);
            AzeriteEssencePowerStorage.ReadDB2(data, "AzeriteEssencePower.db2", HotfixStatements.SEL_AZERITE_ESSENCE_POWER, HotfixStatements.SEL_AZERITE_ESSENCE_POWER_LOCALE);
            AzeriteItemStorage.ReadDB2(data, "AzeriteItem.db2", HotfixStatements.SEL_AZERITE_ITEM);
            AzeriteItemMilestonePowerStorage.ReadDB2(data, "AzeriteItemMilestonePower.db2", HotfixStatements.SEL_AZERITE_ITEM_MILESTONE_POWER);
            AzeriteKnowledgeMultiplierStorage.ReadDB2(data, "AzeriteKnowledgeMultiplier.db2", HotfixStatements.SEL_AZERITE_KNOWLEDGE_MULTIPLIER);
            AzeriteLevelInfoStorage.ReadDB2(data, "AzeriteLevelInfo.db2", HotfixStatements.SEL_AZERITE_LEVEL_INFO);
            AzeritePowerStorage.ReadDB2(data, "AzeritePower.db2", HotfixStatements.SEL_AZERITE_POWER);
            AzeritePowerSetMemberStorage.ReadDB2(data, "AzeritePowerSetMember.db2", HotfixStatements.SEL_AZERITE_POWER_SET_MEMBER);
            AzeriteTierUnlockStorage.ReadDB2(data, "AzeriteTierUnlock.db2", HotfixStatements.SEL_AZERITE_TIER_UNLOCK);
            AzeriteTierUnlockSetStorage.ReadDB2(data, "AzeriteTierUnlockSet.db2", HotfixStatements.SEL_AZERITE_TIER_UNLOCK_SET);
            BankBagSlotPricesStorage.ReadDB2(data, "BankBagSlotPrices.db2", HotfixStatements.SEL_BANK_BAG_SLOT_PRICES);
            BannedAddOnsStorage.ReadDB2(data, "BannedAddons.db2", HotfixStatements.SEL_BANNED_ADDONS);
            BarberShopStyleStorage.ReadDB2(data, "BarberShopStyle.db2", HotfixStatements.SEL_BARBER_SHOP_STYLE, HotfixStatements.SEL_BARBER_SHOP_STYLE_LOCALE);
            BattlePetBreedQualityStorage.ReadDB2(data, "BattlePetBreedQuality.db2", HotfixStatements.SEL_BATTLE_PET_BREED_QUALITY);
            BattlePetBreedStateStorage.ReadDB2(data, "BattlePetBreedState.db2", HotfixStatements.SEL_BATTLE_PET_BREED_STATE);
            BattlePetSpeciesStorage.ReadDB2(data, "BattlePetSpecies.db2", HotfixStatements.SEL_BATTLE_PET_SPECIES, HotfixStatements.SEL_BATTLE_PET_SPECIES_LOCALE);
            BattlePetSpeciesStateStorage.ReadDB2(data, "BattlePetSpeciesState.db2", HotfixStatements.SEL_BATTLE_PET_SPECIES_STATE);
            BattlemasterListStorage.ReadDB2(data, "BattlemasterList.db2", HotfixStatements.SEL_BATTLEMASTER_LIST, HotfixStatements.SEL_BATTLEMASTER_LIST_LOCALE);
            BroadcastTextStorage.ReadDB2(data, "BroadcastText.db2", HotfixStatements.SEL_BROADCAST_TEXT, HotfixStatements.SEL_BROADCAST_TEXT_LOCALE);
            CfgCategoriesStorage.ReadDB2(data, "Cfg_Categories.db2", HotfixStatements.SEL_CFG_CATEGORIES, HotfixStatements.SEL_CFG_CATEGORIES_LOCALE);
            CfgRegionsStorage.ReadDB2(data, "Cfg_Regions.db2", HotfixStatements.SEL_CFG_REGIONS);
            CharTitlesStorage.ReadDB2(data, "CharTitles.db2", HotfixStatements.SEL_CHAR_TITLES, HotfixStatements.SEL_CHAR_TITLES_LOCALE);
            CharacterLoadoutStorage.ReadDB2(data, "CharacterLoadout.db2", HotfixStatements.SEL_CHARACTER_LOADOUT);
            CharacterLoadoutItemStorage.ReadDB2(data, "CharacterLoadoutItem.db2", HotfixStatements.SEL_CHARACTER_LOADOUT_ITEM);
            ChatChannelsStorage.ReadDB2(data, "ChatChannels.db2", HotfixStatements.SEL_CHAT_CHANNELS, HotfixStatements.SEL_CHAT_CHANNELS_LOCALE);
            ChrClassUIDisplayStorage.ReadDB2(data, "ChrClassUIDisplay.db2", HotfixStatements.SEL_CHR_CLASS_UI_DISPLAY);
            ChrClassesStorage.ReadDB2(data, "ChrClasses.db2", HotfixStatements.SEL_CHR_CLASSES, HotfixStatements.SEL_CHR_CLASSES_LOCALE);
            ChrClassesXPowerTypesStorage.ReadDB2(data, "ChrClassesXPowerTypes.db2", HotfixStatements.SEL_CHR_CLASSES_X_POWER_TYPES);
            ChrCustomizationChoiceStorage.ReadDB2(data, "ChrCustomizationChoice.db2", HotfixStatements.SEL_CHR_CUSTOMIZATION_CHOICE, HotfixStatements.SEL_CHR_CUSTOMIZATION_CHOICE_LOCALE);
            ChrCustomizationDisplayInfoStorage.ReadDB2(data, "ChrCustomizationDisplayInfo.db2", HotfixStatements.SEL_CHR_CUSTOMIZATION_DISPLAY_INFO);
            ChrCustomizationElementStorage.ReadDB2(data, "ChrCustomizationElement.db2", HotfixStatements.SEL_CHR_CUSTOMIZATION_ELEMENT);
            ChrCustomizationOptionStorage.ReadDB2(data, "ChrCustomizationOption.db2", HotfixStatements.SEL_CHR_CUSTOMIZATION_OPTION, HotfixStatements.SEL_CHR_CUSTOMIZATION_OPTION_LOCALE);
            ChrCustomizationReqStorage.ReadDB2(data, "ChrCustomizationReq.db2", HotfixStatements.SEL_CHR_CUSTOMIZATION_REQ);
            ChrCustomizationReqChoiceStorage.ReadDB2(data, "ChrCustomizationReqChoice.db2", HotfixStatements.SEL_CHR_CUSTOMIZATION_REQ_CHOICE);
            ChrModelStorage.ReadDB2(data, "ChrModel.db2", HotfixStatements.SEL_CHR_MODEL);
            ChrRaceXChrModelStorage.ReadDB2(data, "ChrRaceXChrModel.db2", HotfixStatements.SEL_CHR_RACE_X_CHR_MODEL);
            ChrRacesStorage.ReadDB2(data, "ChrRaces.db2", HotfixStatements.SEL_CHR_RACES, HotfixStatements.SEL_CHR_RACES_LOCALE);
            ChrSpecializationStorage.ReadDB2(data, "ChrSpecialization.db2", HotfixStatements.SEL_CHR_SPECIALIZATION, HotfixStatements.SEL_CHR_SPECIALIZATION_LOCALE);
            CinematicCameraStorage.ReadDB2(data, "CinematicCamera.db2", HotfixStatements.SEL_CINEMATIC_CAMERA);
            CinematicSequencesStorage.ReadDB2(data, "CinematicSequences.db2", HotfixStatements.SEL_CINEMATIC_SEQUENCES);
            ConditionalChrModelStorage.ReadDB2(data, "ConditionalChrModel.db2", HotfixStatements.SEL_CONDITIONAL_CHR_MODEL);
            ConditionalContentTuningStorage.ReadDB2(data, "ConditionalContentTuning.db2", HotfixStatements.SEL_CONDITIONAL_CONTENT_TUNING);
            ContentTuningStorage.ReadDB2(data, "ContentTuning.db2", HotfixStatements.SEL_CONTENT_TUNING);
            ConversationLineStorage.ReadDB2(data, "ConversationLine.db2", HotfixStatements.SEL_CONVERSATION_LINE);
            CreatureDisplayInfoStorage.ReadDB2(data, "CreatureDisplayInfo.db2", HotfixStatements.SEL_CREATURE_DISPLAY_INFO);
            CreatureDisplayInfoExtraStorage.ReadDB2(data, "CreatureDisplayInfoExtra.db2", HotfixStatements.SEL_CREATURE_DISPLAY_INFO_EXTRA);
            CreatureFamilyStorage.ReadDB2(data, "CreatureFamily.db2", HotfixStatements.SEL_CREATURE_FAMILY, HotfixStatements.SEL_CREATURE_FAMILY_LOCALE);
            CreatureModelDataStorage.ReadDB2(data, "CreatureModelData.db2", HotfixStatements.SEL_CREATURE_MODEL_DATA);
            CreatureTypeStorage.ReadDB2(data, "CreatureType.db2", HotfixStatements.SEL_CREATURE_TYPE, HotfixStatements.SEL_CREATURE_TYPE_LOCALE);
            CriteriaStorage.ReadDB2(data, "Criteria.db2", HotfixStatements.SEL_CRITERIA);
            CriteriaTreeStorage.ReadDB2(data, "CriteriaTree.db2", HotfixStatements.SEL_CRITERIA_TREE, HotfixStatements.SEL_CRITERIA_TREE_LOCALE);
            CurrencyContainerStorage.ReadDB2(data, "CurrencyContainer.db2", HotfixStatements.SEL_CURRENCY_CONTAINER, HotfixStatements.SEL_CURRENCY_CONTAINER_LOCALE);
            CurrencyTypesStorage.ReadDB2(data, "CurrencyTypes.db2", HotfixStatements.SEL_CURRENCY_TYPES, HotfixStatements.SEL_CURRENCY_TYPES_LOCALE);
            CurveStorage.ReadDB2(data, "Curve.db2", HotfixStatements.SEL_CURVE);
            CurvePointStorage.ReadDB2(data, "CurvePoint.db2", HotfixStatements.SEL_CURVE_POINT);
            DestructibleModelDataStorage.ReadDB2(data, "DestructibleModelData.db2", HotfixStatements.SEL_DESTRUCTIBLE_MODEL_DATA);
            DifficultyStorage.ReadDB2(data, "Difficulty.db2", HotfixStatements.SEL_DIFFICULTY, HotfixStatements.SEL_DIFFICULTY_LOCALE);
            DungeonEncounterStorage.ReadDB2(data, "DungeonEncounter.db2", HotfixStatements.SEL_DUNGEON_ENCOUNTER, HotfixStatements.SEL_DUNGEON_ENCOUNTER_LOCALE);
            DurabilityCostsStorage.ReadDB2(data, "DurabilityCosts.db2", HotfixStatements.SEL_DURABILITY_COSTS);
            DurabilityQualityStorage.ReadDB2(data, "DurabilityQuality.db2", HotfixStatements.SEL_DURABILITY_QUALITY);
            EmotesStorage.ReadDB2(data, "Emotes.db2", HotfixStatements.SEL_EMOTES);
            EmotesTextStorage.ReadDB2(data, "EmotesText.db2", HotfixStatements.SEL_EMOTES_TEXT);
            EmotesTextSoundStorage.ReadDB2(data, "EmotesTextSound.db2", HotfixStatements.SEL_EMOTES_TEXT_SOUND);
            ExpectedStatStorage.ReadDB2(data, "ExpectedStat.db2", HotfixStatements.SEL_EXPECTED_STAT);
            ExpectedStatModStorage.ReadDB2(data, "ExpectedStatMod.db2", HotfixStatements.SEL_EXPECTED_STAT_MOD);
            FactionStorage.ReadDB2(data, "Faction.db2", HotfixStatements.SEL_FACTION, HotfixStatements.SEL_FACTION_LOCALE);
            FactionTemplateStorage.ReadDB2(data, "FactionTemplate.db2", HotfixStatements.SEL_FACTION_TEMPLATE);
            FriendshipRepReactionStorage.ReadDB2(data, "FriendshipRepReaction.db2", HotfixStatements.SEL_FRIENDSHIP_REP_REACTION, HotfixStatements.SEL_FRIENDSHIP_REP_REACTION_LOCALE);
            FriendshipReputationStorage.ReadDB2(data, "FriendshipReputation.db2", HotfixStatements.SEL_FRIENDSHIP_REPUTATION, HotfixStatements.SEL_FRIENDSHIP_REPUTATION_LOCALE);
            GameObjectArtKitStorage.ReadDB2(data, "GameObjectArtKit.db2", HotfixStatements.SEL_GAMEOBJECT_ART_KIT);
            GameObjectDisplayInfoStorage.ReadDB2(data, "GameObjectDisplayInfo.db2", HotfixStatements.SEL_GAMEOBJECT_DISPLAY_INFO);
            GameObjectsStorage.ReadDB2(data, "GameObjects.db2", HotfixStatements.SEL_GAMEOBJECTS, HotfixStatements.SEL_GAMEOBJECTS_LOCALE);
            GarrAbilityStorage.ReadDB2(data, "GarrAbility.db2", HotfixStatements.SEL_GARR_ABILITY, HotfixStatements.SEL_GARR_ABILITY_LOCALE);
            GarrBuildingStorage.ReadDB2(data, "GarrBuilding.db2", HotfixStatements.SEL_GARR_BUILDING, HotfixStatements.SEL_GARR_BUILDING_LOCALE);
            GarrBuildingPlotInstStorage.ReadDB2(data, "GarrBuildingPlotInst.db2", HotfixStatements.SEL_GARR_BUILDING_PLOT_INST);
            GarrClassSpecStorage.ReadDB2(data, "GarrClassSpec.db2", HotfixStatements.SEL_GARR_CLASS_SPEC, HotfixStatements.SEL_GARR_CLASS_SPEC_LOCALE);
            GarrFollowerStorage.ReadDB2(data, "GarrFollower.db2", HotfixStatements.SEL_GARR_FOLLOWER, HotfixStatements.SEL_GARR_FOLLOWER_LOCALE);
            GarrFollowerXAbilityStorage.ReadDB2(data, "GarrFollowerXAbility.db2", HotfixStatements.SEL_GARR_FOLLOWER_X_ABILITY);
            GarrMissionStorage.ReadDB2(data, "GarrMission.db2", HotfixStatements.SEL_GARR_MISSION, HotfixStatements.SEL_GARR_MISSION_LOCALE);
            GarrPlotStorage.ReadDB2(data, "GarrPlot.db2", HotfixStatements.SEL_GARR_PLOT);
            GarrPlotBuildingStorage.ReadDB2(data, "GarrPlotBuilding.db2", HotfixStatements.SEL_GARR_PLOT_BUILDING);
            GarrPlotInstanceStorage.ReadDB2(data, "GarrPlotInstance.db2", HotfixStatements.SEL_GARR_PLOT_INSTANCE);
            GarrSiteLevelStorage.ReadDB2(data, "GarrSiteLevel.db2", HotfixStatements.SEL_GARR_SITE_LEVEL);
            GarrSiteLevelPlotInstStorage.ReadDB2(data, "GarrSiteLevelPlotInst.db2", HotfixStatements.SEL_GARR_SITE_LEVEL_PLOT_INST);
            GarrTalentTreeStorage.ReadDB2(data, "GarrTalentTree.db2", HotfixStatements.SEL_GARR_TALENT_TREE, HotfixStatements.SEL_GARR_TALENT_TREE_LOCALE);
            GemPropertiesStorage.ReadDB2(data, "GemProperties.db2", HotfixStatements.SEL_GEM_PROPERTIES);
            GlyphBindableSpellStorage.ReadDB2(data, "GlyphBindableSpell.db2", HotfixStatements.SEL_GLYPH_BINDABLE_SPELL);
            GlyphPropertiesStorage.ReadDB2(data, "GlyphProperties.db2", HotfixStatements.SEL_GLYPH_PROPERTIES);
            GlyphRequiredSpecStorage.ReadDB2(data, "GlyphRequiredSpec.db2", HotfixStatements.SEL_GLYPH_REQUIRED_SPEC);
            GlyphSlotStorage.ReadDB2(data, "GlyphSlot.db2", HotfixStatements.SEL_GLYPH_SLOT);          
            GossipNPCOptionStorage.ReadDB2(data, "GossipNPCOption.db2", HotfixStatements.SEL_GOSSIP_NPC_OPTION);
            GuildColorBackgroundStorage.ReadDB2(data, "GuildColorBackground.db2", HotfixStatements.SEL_GUILD_COLOR_BACKGROUND);
            GuildColorBorderStorage.ReadDB2(data, "GuildColorBorder.db2", HotfixStatements.SEL_GUILD_COLOR_BORDER);
            GuildColorEmblemStorage.ReadDB2(data, "GuildColorEmblem.db2", HotfixStatements.SEL_GUILD_COLOR_EMBLEM);
            GuildPerkSpellsStorage.ReadDB2(data, "GuildPerkSpells.db2", HotfixStatements.SEL_GUILD_PERK_SPELLS);
            HeirloomStorage.ReadDB2(data, "Heirloom.db2", HotfixStatements.SEL_HEIRLOOM, HotfixStatements.SEL_HEIRLOOM_LOCALE);
            HolidaysStorage.ReadDB2(data, "Holidays.db2", HotfixStatements.SEL_HOLIDAYS);
            ImportPriceArmorStorage.ReadDB2(data, "ImportPriceArmor.db2", HotfixStatements.SEL_IMPORT_PRICE_ARMOR);
            ImportPriceQualityStorage.ReadDB2(data, "ImportPriceQuality.db2", HotfixStatements.SEL_IMPORT_PRICE_QUALITY);
            ImportPriceShieldStorage.ReadDB2(data, "ImportPriceShield.db2", HotfixStatements.SEL_IMPORT_PRICE_SHIELD);
            ImportPriceWeaponStorage.ReadDB2(data, "ImportPriceWeapon.db2", HotfixStatements.SEL_IMPORT_PRICE_WEAPON);
            ItemAppearanceStorage.ReadDB2(data, "ItemAppearance.db2", HotfixStatements.SEL_ITEM_APPEARANCE);
            ItemArmorQualityStorage.ReadDB2(data, "ItemArmorQuality.db2", HotfixStatements.SEL_ITEM_ARMOR_QUALITY);
            ItemArmorShieldStorage.ReadDB2(data, "ItemArmorShield.db2", HotfixStatements.SEL_ITEM_ARMOR_SHIELD);
            ItemArmorTotalStorage.ReadDB2(data, "ItemArmorTotal.db2", HotfixStatements.SEL_ITEM_ARMOR_TOTAL);
            ItemBagFamilyStorage.ReadDB2(data, "ItemBagFamily.db2", HotfixStatements.SEL_ITEM_BAG_FAMILY, HotfixStatements.SEL_ITEM_BAG_FAMILY_LOCALE);
            ItemBonusStorage.ReadDB2(data, "ItemBonus.db2", HotfixStatements.SEL_ITEM_BONUS);
            ItemBonusListLevelDeltaStorage.ReadDB2(data, "ItemBonusListLevelDelta.db2", HotfixStatements.SEL_ITEM_BONUS_LIST_LEVEL_DELTA);
            ItemBonusTreeNodeStorage.ReadDB2(data, "ItemBonusTreeNode.db2", HotfixStatements.SEL_ITEM_BONUS_TREE_NODE);
            ItemChildEquipmentStorage.ReadDB2(data, "ItemChildEquipment.db2", HotfixStatements.SEL_ITEM_CHILD_EQUIPMENT);
            ItemClassStorage.ReadDB2(data, "ItemClass.db2", HotfixStatements.SEL_ITEM_CLASS, HotfixStatements.SEL_ITEM_CLASS_LOCALE);
            ItemContextPickerEntryStorage.ReadDB2(data, "ItemContextPickerEntry.db2", HotfixStatements.SEL_ITEM_CONTEXT_PICKER_ENTRY);
            ItemCurrencyCostStorage.ReadDB2(data, "ItemCurrencyCost.db2", HotfixStatements.SEL_ITEM_CURRENCY_COST);
            ItemDamageAmmoStorage.ReadDB2(data, "ItemDamageAmmo.db2", HotfixStatements.SEL_ITEM_DAMAGE_AMMO);
            ItemDamageOneHandStorage.ReadDB2(data, "ItemDamageOneHand.db2", HotfixStatements.SEL_ITEM_DAMAGE_ONE_HAND);
            ItemDamageOneHandCasterStorage.ReadDB2(data, "ItemDamageOneHandCaster.db2", HotfixStatements.SEL_ITEM_DAMAGE_ONE_HAND_CASTER);
            ItemDamageTwoHandStorage.ReadDB2(data, "ItemDamageTwoHand.db2", HotfixStatements.SEL_ITEM_DAMAGE_TWO_HAND);
            ItemDamageTwoHandCasterStorage.ReadDB2(data, "ItemDamageTwoHandCaster.db2", HotfixStatements.SEL_ITEM_DAMAGE_TWO_HAND_CASTER);
            ItemDisenchantLootStorage.ReadDB2(data, "ItemDisenchantLoot.db2", HotfixStatements.SEL_ITEM_DISENCHANT_LOOT);
            ItemEffectStorage.ReadDB2(data, "ItemEffect.db2", HotfixStatements.SEL_ITEM_EFFECT);
            ItemStorage.ReadDB2(data, "Item.db2", HotfixStatements.SEL_ITEM);
            ItemExtendedCostStorage.ReadDB2(data, "ItemExtendedCost.db2", HotfixStatements.SEL_ITEM_EXTENDED_COST);
            ItemLevelSelectorStorage.ReadDB2(data, "ItemLevelSelector.db2", HotfixStatements.SEL_ITEM_LEVEL_SELECTOR);
            ItemLevelSelectorQualityStorage.ReadDB2(data, "ItemLevelSelectorQuality.db2", HotfixStatements.SEL_ITEM_LEVEL_SELECTOR_QUALITY);
            ItemLevelSelectorQualitySetStorage.ReadDB2(data, "ItemLevelSelectorQualitySet.db2", HotfixStatements.SEL_ITEM_LEVEL_SELECTOR_QUALITY_SET);
            ItemLimitCategoryStorage.ReadDB2(data, "ItemLimitCategory.db2", HotfixStatements.SEL_ITEM_LIMIT_CATEGORY, HotfixStatements.SEL_ITEM_LIMIT_CATEGORY_LOCALE);
            ItemLimitCategoryConditionStorage.ReadDB2(data, "ItemLimitCategoryCondition.db2", HotfixStatements.SEL_ITEM_LIMIT_CATEGORY_CONDITION);
            ItemModifiedAppearanceStorage.ReadDB2(data, "ItemModifiedAppearance.db2", HotfixStatements.SEL_ITEM_MODIFIED_APPEARANCE);
            ItemModifiedAppearanceExtraStorage.ReadDB2(data, "ItemModifiedAppearanceExtra.db2", HotfixStatements.SEL_ITEM_MODIFIED_APPEARANCE_EXTRA);
            ItemNameDescriptionStorage.ReadDB2(data, "ItemNameDescription.db2", HotfixStatements.SEL_ITEM_NAME_DESCRIPTION, HotfixStatements.SEL_ITEM_NAME_DESCRIPTION_LOCALE);
            ItemPriceBaseStorage.ReadDB2(data, "ItemPriceBase.db2", HotfixStatements.SEL_ITEM_PRICE_BASE);
            ItemRandomPropertiesStorage.ReadDB2(data, "ItemRandomProperties.db2", HotfixStatements.SEL_ITEM_RANDOM_PROPERTIES);
            ItemRandomSuffixStorage.ReadDB2(data, "ItemRandomSuffix.db2", HotfixStatements.SEL_ITEM_RANDOM_SUFFIX);
            ItemSearchNameStorage.ReadDB2(data, "ItemSearchName.db2", HotfixStatements.SEL_ITEM_SEARCH_NAME, HotfixStatements.SEL_ITEM_SEARCH_NAME_LOCALE);
            ItemSetStorage.ReadDB2(data, "ItemSet.db2", HotfixStatements.SEL_ITEM_SET, HotfixStatements.SEL_ITEM_SET_LOCALE);
            ItemSetSpellStorage.ReadDB2(data, "ItemSetSpell.db2", HotfixStatements.SEL_ITEM_SET_SPELL);
            ItemSparseStorage.ReadDB2(data, "ItemSparse.db2", HotfixStatements.SEL_ITEM_SPARSE, HotfixStatements.SEL_ITEM_SPARSE_LOCALE);
            ItemSpecStorage.ReadDB2(data, "ItemSpec.db2", HotfixStatements.SEL_ITEM_SPEC);
            ItemSpecOverrideStorage.ReadDB2(data, "ItemSpecOverride.db2", HotfixStatements.SEL_ITEM_SPEC_OVERRIDE);
            ItemXBonusTreeStorage.ReadDB2(data, "ItemXBonusTree.db2", HotfixStatements.SEL_ITEM_X_BONUS_TREE);
            JournalEncounterStorage.ReadDB2(data, "JournalEncounter.db2", HotfixStatements.SEL_JOURNAL_ENCOUNTER, HotfixStatements.SEL_JOURNAL_ENCOUNTER_LOCALE);
            JournalEncounterSectionStorage.ReadDB2(data, "JournalEncounterSection.db2", HotfixStatements.SEL_JOURNAL_ENCOUNTER_SECTION, HotfixStatements.SEL_JOURNAL_ENCOUNTER_SECTION_LOCALE);
            JournalInstanceStorage.ReadDB2(data, "JournalInstance.db2", HotfixStatements.SEL_JOURNAL_INSTANCE, HotfixStatements.SEL_JOURNAL_INSTANCE_LOCALE);
            JournalTierStorage.ReadDB2(data, "JournalTier.db2", HotfixStatements.SEL_JOURNAL_TIER, HotfixStatements.SEL_JOURNAL_TIER_LOCALE);
            KeyChainStorage.ReadDB2(data, "KeyChain.db2", HotfixStatements.SEL_KEYCHAIN);
            KeystoneAffixStorage.ReadDB2(data, "KeystoneAffix.db2", HotfixStatements.SEL_KEYSTONE_AFFIX, HotfixStatements.SEL_KEYSTONE_AFFIX_LOCALE);
            LanguageWordsStorage.ReadDB2(data, "LanguageWords.db2", HotfixStatements.SEL_LANGUAGE_WORDS);
            LanguagesStorage.ReadDB2(data, "Languages.db2", HotfixStatements.SEL_LANGUAGES, HotfixStatements.SEL_LANGUAGES_LOCALE);
            LFGDungeonsStorage.ReadDB2(data, "LFGDungeons.db2", HotfixStatements.SEL_LFG_DUNGEONS, HotfixStatements.SEL_LFG_DUNGEONS_LOCALE);
            LightStorage.ReadDB2(data, "Light.db2", HotfixStatements.SEL_LIGHT);
            LiquidTypeStorage.ReadDB2(data, "LiquidType.db2", HotfixStatements.SEL_LIQUID_TYPE);
            LockStorage.ReadDB2(data, "Lock.db2", HotfixStatements.SEL_LOCK);
            MailTemplateStorage.ReadDB2(data, "MailTemplate.db2", HotfixStatements.SEL_MAIL_TEMPLATE, HotfixStatements.SEL_MAIL_TEMPLATE_LOCALE);
            MapStorage.ReadDB2(data, "Map.db2", HotfixStatements.SEL_MAP, HotfixStatements.SEL_MAP_LOCALE);
            MapChallengeModeStorage.ReadDB2(data, "MapChallengeMode.db2", HotfixStatements.SEL_MAP_CHALLENGE_MODE, HotfixStatements.SEL_MAP_CHALLENGE_MODE_LOCALE);
            MapDifficultyStorage.ReadDB2(data, "MapDifficulty.db2", HotfixStatements.SEL_MAP_DIFFICULTY, HotfixStatements.SEL_MAP_DIFFICULTY_LOCALE);
            MapDifficultyXConditionStorage.ReadDB2(data, "MapDifficultyXCondition.db2", HotfixStatements.SEL_MAP_DIFFICULTY_X_CONDITION, HotfixStatements.SEL_MAP_DIFFICULTY_X_CONDITION_LOCALE);
            ModifierTreeStorage.ReadDB2(data, "ModifierTree.db2", HotfixStatements.SEL_MODIFIER_TREE);
            MountCapabilityStorage.ReadDB2(data, "MountCapability.db2", HotfixStatements.SEL_MOUNT_CAPABILITY);
            MountStorage.ReadDB2(data, "Mount.db2", HotfixStatements.SEL_MOUNT, HotfixStatements.SEL_MOUNT_LOCALE);
            MountTypeXCapabilityStorage.ReadDB2(data, "MountTypeXCapability.db2", HotfixStatements.SEL_MOUNT_TYPE_X_CAPABILITY);
            MountXDisplayStorage.ReadDB2(data, "MountXDisplay.db2", HotfixStatements.SEL_MOUNT_X_DISPLAY);
            MovieStorage.ReadDB2(data, "Movie.db2", HotfixStatements.SEL_MOVIE);
            MythicPlusSeasonStorage.ReadDB2(data, "MythicPlusSeason.db2", HotfixStatements.SEL_MYTHIC_PLUS_SEASON);
            NameGenStorage.ReadDB2(data, "NameGen.db2", HotfixStatements.SEL_NAME_GEN);
            NamesProfanityStorage.ReadDB2(data, "NamesProfanity.db2", HotfixStatements.SEL_NAMES_PROFANITY);
            NamesReservedStorage.ReadDB2(data, "NamesReserved.db2", HotfixStatements.SEL_NAMES_RESERVED, HotfixStatements.SEL_NAMES_RESERVED_LOCALE);
            NamesReservedLocaleStorage.ReadDB2(data, "NamesReservedLocale.db2", HotfixStatements.SEL_NAMES_RESERVED_LOCALE);
            NumTalentsAtLevelStorage.ReadDB2(data, "NumTalentsAtLevel.db2", HotfixStatements.SEL_NUM_TALENTS_AT_LEVEL);
            OverrideSpellDataStorage.ReadDB2(data, "OverrideSpellData.db2", HotfixStatements.SEL_OVERRIDE_SPELL_DATA);
            ParagonReputationStorage.ReadDB2(data, "ParagonReputation.db2", HotfixStatements.SEL_PARAGON_REPUTATION);
            PhaseStorage.ReadDB2(data, "Phase.db2", HotfixStatements.SEL_PHASE);
            PhaseXPhaseGroupStorage.ReadDB2(data, "PhaseXPhaseGroup.db2", HotfixStatements.SEL_PHASE_X_PHASE_GROUP);
            PlayerConditionStorage.ReadDB2(data, "PlayerCondition.db2", HotfixStatements.SEL_PLAYER_CONDITION, HotfixStatements.SEL_PLAYER_CONDITION_LOCALE);
            PowerDisplayStorage.ReadDB2(data, "PowerDisplay.db2", HotfixStatements.SEL_POWER_DISPLAY);
            PowerTypeStorage.ReadDB2(data, "PowerType.db2", HotfixStatements.SEL_POWER_TYPE);
            PrestigeLevelInfoStorage.ReadDB2(data, "PrestigeLevelInfo.db2", HotfixStatements.SEL_PRESTIGE_LEVEL_INFO, HotfixStatements.SEL_PRESTIGE_LEVEL_INFO_LOCALE);
            PvpDifficultyStorage.ReadDB2(data, "PVPDifficulty.db2", HotfixStatements.SEL_PVP_DIFFICULTY);
            PvpItemStorage.ReadDB2(data, "PVPItem.db2", HotfixStatements.SEL_PVP_ITEM);
            PvpSeasonStorage.ReadDB2(data, "PvpSeason.db2", HotfixStatements.SEL_PVP_SEASON);
            PvpTalentStorage.ReadDB2(data, "PvpTalent.db2", HotfixStatements.SEL_PVP_TALENT, HotfixStatements.SEL_PVP_TALENT_LOCALE);
            PvpTalentCategoryStorage.ReadDB2(data, "PvpTalentCategory.db2", HotfixStatements.SEL_PVP_TALENT_CATEGORY);
            PvpTalentSlotUnlockStorage.ReadDB2(data, "PvpTalentSlotUnlock.db2", HotfixStatements.SEL_PVP_TALENT_SLOT_UNLOCK);
            PvpTierStorage.ReadDB2(data, "PvpTier.db2", HotfixStatements.SEL_PVP_TIER, HotfixStatements.SEL_PVP_TIER_LOCALE);
            QuestFactionRewardStorage.ReadDB2(data, "QuestFactionReward.db2", HotfixStatements.SEL_QUEST_FACTION_REWARD);
            QuestInfoStorage.ReadDB2(data, "QuestInfo.db2", HotfixStatements.SEL_QUEST_INFO, HotfixStatements.SEL_QUEST_INFO_LOCALE);
            QuestLineXQuestStorage.ReadDB2(data, "QuestLineXQuest.db2", HotfixStatements.SEL_QUEST_LINE_X_QUEST);
            QuestMoneyRewardStorage.ReadDB2(data, "QuestMoneyReward.db2", HotfixStatements.SEL_QUEST_MONEY_REWARD);
            QuestPackageItemStorage.ReadDB2(data, "QuestPackageItem.db2", HotfixStatements.SEL_QUEST_PACKAGE_ITEM);
            QuestSortStorage.ReadDB2(data, "QuestSort.db2", HotfixStatements.SEL_QUEST_SORT, HotfixStatements.SEL_QUEST_SORT_LOCALE);
            QuestV2Storage.ReadDB2(data, "QuestV2.db2", HotfixStatements.SEL_QUEST_V2);
            QuestXPStorage.ReadDB2(data, "QuestXP.db2", HotfixStatements.SEL_QUEST_XP);
            RandPropPointsStorage.ReadDB2(data, "RandPropPoints.db2", HotfixStatements.SEL_RAND_PROP_POINTS);
            RewardPackStorage.ReadDB2(data, "RewardPack.db2", HotfixStatements.SEL_REWARD_PACK);
            RewardPackXCurrencyTypeStorage.ReadDB2(data, "RewardPackXCurrencyType.db2", HotfixStatements.SEL_REWARD_PACK_X_CURRENCY_TYPE);
            RewardPackXItemStorage.ReadDB2(data, "RewardPackXItem.db2", HotfixStatements.SEL_REWARD_PACK_X_ITEM);
            ScalingStatDistributionStorage.ReadDB2(data, "ScalingStatDistribution.db2", HotfixStatements.SEL_SCALING_STAT_DISTRIBUTION);
            ScalingStatValuesStorage.ReadDB2(data, "ScalingStatValues.db2", HotfixStatements.SEL_SCALING_STAT_VALUES);
            ScenarioStorage.ReadDB2(data, "Scenario.db2", HotfixStatements.SEL_SCENARIO, HotfixStatements.SEL_SCENARIO_LOCALE);
            ScenarioStepStorage.ReadDB2(data, "ScenarioStep.db2", HotfixStatements.SEL_SCENARIO_STEP, HotfixStatements.SEL_SCENARIO_STEP_LOCALE);
            SceneScriptStorage.ReadDB2(data, "SceneScript.db2", HotfixStatements.SEL_SCENE_SCRIPT);
            SceneScriptGlobalTextStorage.ReadDB2(data, "SceneScriptGlobalText.db2", HotfixStatements.SEL_SCENE_SCRIPT_GLOBAL_TEXT);
            SceneScriptPackageStorage.ReadDB2(data, "SceneScriptPackage.db2", HotfixStatements.SEL_SCENE_SCRIPT_PACKAGE);
            SceneScriptTextStorage.ReadDB2(data, "SceneScriptText.db2", HotfixStatements.SEL_SCENE_SCRIPT_TEXT);
            ServerMessagesStorage.ReadDB2(data, "ServerMessages.db2", HotfixStatements.SEL_SERVER_MESSAGES, HotfixStatements.SEL_SERVER_MESSAGES_LOCALE);
            SkillLineStorage.ReadDB2(data, "SkillLine.db2", HotfixStatements.SEL_SKILL_LINE, HotfixStatements.SEL_SKILL_LINE_LOCALE);
            SkillLineAbilityStorage.ReadDB2(data, "SkillLineAbility.db2", HotfixStatements.SEL_SKILL_LINE_ABILITY);
            SkillLineXTraitTreeStorage.ReadDB2(data, "SkillLineXTraitTree.db2", HotfixStatements.SEL_SKILL_LINE_X_TRAIT_TREE);
            SkillRaceClassInfoStorage.ReadDB2(data, "SkillRaceClassInfo.db2", HotfixStatements.SEL_SKILL_RACE_CLASS_INFO);
            SoundKitStorage.ReadDB2(data, "SoundKit.db2", HotfixStatements.SEL_SOUND_KIT);
            SpecializationSpellsStorage.ReadDB2(data, "SpecializationSpells.db2", HotfixStatements.SEL_SPECIALIZATION_SPELLS, HotfixStatements.SEL_SPECIALIZATION_SPELLS_LOCALE);
            SpecSetMemberStorage.ReadDB2(data, "SpecSetMember.db2", HotfixStatements.SEL_SPEC_SET_MEMBER);
            SpellAuraOptionsStorage.ReadDB2(data, "SpellAuraOptions.db2", HotfixStatements.SEL_SPELL_AURA_OPTIONS);
            SpellAuraRestrictionsStorage.ReadDB2(data, "SpellAuraRestrictions.db2", HotfixStatements.SEL_SPELL_AURA_RESTRICTIONS);
            SpellCastTimesStorage.ReadDB2(data, "SpellCastTimes.db2", HotfixStatements.SEL_SPELL_CAST_TIMES);
            SpellCastingRequirementsStorage.ReadDB2(data, "SpellCastingRequirements.db2", HotfixStatements.SEL_SPELL_CASTING_REQUIREMENTS);
            SpellCategoriesStorage.ReadDB2(data, "SpellCategories.db2", HotfixStatements.SEL_SPELL_CATEGORIES);
            SpellCategoryStorage.ReadDB2(data, "SpellCategory.db2", HotfixStatements.SEL_SPELL_CATEGORY, HotfixStatements.SEL_SPELL_CATEGORY_LOCALE);
            SpellClassOptionsStorage.ReadDB2(data, "SpellClassOptions.db2", HotfixStatements.SEL_SPELL_CLASS_OPTIONS);
            SpellCooldownsStorage.ReadDB2(data, "SpellCooldowns.db2", HotfixStatements.SEL_SPELL_COOLDOWNS);
            SpellDurationStorage.ReadDB2(data, "SpellDuration.db2", HotfixStatements.SEL_SPELL_DURATION);
            SpellEffectStorage.ReadDB2(data, "SpellEffect.db2", HotfixStatements.SEL_SPELL_EFFECT);
            SpellEquippedItemsStorage.ReadDB2(data, "SpellEquippedItems.db2", HotfixStatements.SEL_SPELL_EQUIPPED_ITEMS);
            SpellFocusObjectStorage.ReadDB2(data, "SpellFocusObject.db2", HotfixStatements.SEL_SPELL_FOCUS_OBJECT, HotfixStatements.SEL_SPELL_FOCUS_OBJECT_LOCALE);
            SpellInterruptsStorage.ReadDB2(data, "SpellInterrupts.db2", HotfixStatements.SEL_SPELL_INTERRUPTS);
            SpellItemEnchantmentStorage.ReadDB2(data, "SpellItemEnchantment.db2", HotfixStatements.SEL_SPELL_ITEM_ENCHANTMENT, HotfixStatements.SEL_SPELL_ITEM_ENCHANTMENT_LOCALE);
            SpellItemEnchantmentConditionStorage.ReadDB2(data, "SpellItemEnchantmentCondition.db2", HotfixStatements.SEL_SPELL_ITEM_ENCHANTMENT_CONDITION);
            SpellKeyboundOverrideStorage.ReadDB2(data, "SpellKeyboundOverride.db2", HotfixStatements.SEL_SPELL_KEYBOUND_OVERRIDE);
            SpellLabelStorage.ReadDB2(data, "SpellLabel.db2", HotfixStatements.SEL_SPELL_LABEL);
            SpellLearnSpellStorage.ReadDB2(data, "SpellLearnSpell.db2", HotfixStatements.SEL_SPELL_LEARN_SPELL);
            SpellLevelsStorage.ReadDB2(data, "SpellLevels.db2", HotfixStatements.SEL_SPELL_LEVELS);
            SpellMiscStorage.ReadDB2(data, "SpellMisc.db2", HotfixStatements.SEL_SPELL_MISC);
            SpellNameStorage.ReadDB2(data, "SpellName.db2", HotfixStatements.SEL_SPELL_NAME, HotfixStatements.SEL_SPELL_NAME_LOCALE);
            SpellPowerStorage.ReadDB2(data, "SpellPower.db2", HotfixStatements.SEL_SPELL_POWER);
            SpellPowerDifficultyStorage.ReadDB2(data, "SpellPowerDifficulty.db2", HotfixStatements.SEL_SPELL_POWER_DIFFICULTY);
            SpellProcsPerMinuteStorage.ReadDB2(data, "SpellProcsPerMinute.db2", HotfixStatements.SEL_SPELL_PROCS_PER_MINUTE);
            SpellProcsPerMinuteModStorage.ReadDB2(data, "SpellProcsPerMinuteMod.db2", HotfixStatements.SEL_SPELL_PROCS_PER_MINUTE_MOD);
            SpellRadiusStorage.ReadDB2(data, "SpellRadius.db2", HotfixStatements.SEL_SPELL_RADIUS);
            SpellRangeStorage.ReadDB2(data, "SpellRange.db2", HotfixStatements.SEL_SPELL_RANGE, HotfixStatements.SEL_SPELL_RANGE_LOCALE);
            SpellReagentsStorage.ReadDB2(data, "SpellReagents.db2", HotfixStatements.SEL_SPELL_REAGENTS);
            SpellReagentsCurrencyStorage.ReadDB2(data, "SpellReagentsCurrency.db2", HotfixStatements.SEL_SPELL_REAGENTS_CURRENCY);
            SpellScalingStorage.ReadDB2(data, "SpellScaling.db2", HotfixStatements.SEL_SPELL_SCALING);
            SpellShapeshiftStorage.ReadDB2(data, "SpellShapeshift.db2", HotfixStatements.SEL_SPELL_SHAPESHIFT);
            SpellShapeshiftFormStorage.ReadDB2(data, "SpellShapeshiftForm.db2", HotfixStatements.SEL_SPELL_SHAPESHIFT_FORM, HotfixStatements.SEL_SPELL_SHAPESHIFT_FORM_LOCALE);
            SpellTargetRestrictionsStorage.ReadDB2(data, "SpellTargetRestrictions.db2", HotfixStatements.SEL_SPELL_TARGET_RESTRICTIONS);
            SpellTotemsStorage.ReadDB2(data, "SpellTotems.db2", HotfixStatements.SEL_SPELL_TOTEMS);
            SpellVisualStorage.ReadDB2(data, "SpellVisual.db2", HotfixStatements.SEL_SPELL_VISUAL);
            SpellVisualEffectNameStorage.ReadDB2(data, "SpellVisualEffectName.db2", HotfixStatements.SEL_SPELL_VISUAL_EFFECT_NAME);
            SpellVisualMissileStorage.ReadDB2(data, "SpellVisualMissile.db2", HotfixStatements.SEL_SPELL_VISUAL_MISSILE);
            SpellVisualKitStorage.ReadDB2(data, "SpellVisualKit.db2", HotfixStatements.SEL_SPELL_VISUAL_KIT);
            SpellXSpellVisualStorage.ReadDB2(data, "SpellXSpellVisual.db2", HotfixStatements.SEL_SPELL_X_SPELL_VISUAL);
            SummonPropertiesStorage.ReadDB2(data, "SummonProperties.db2", HotfixStatements.SEL_SUMMON_PROPERTIES);
            TactKeyStorage.ReadDB2(data, "TactKey.db2", HotfixStatements.SEL_TACT_KEY);
            TalentStorage.ReadDB2(data, "Talent.db2", HotfixStatements.SEL_TALENT, HotfixStatements.SEL_TALENT_LOCALE);
            TalentTabStorage.ReadDB2(data, "TalentTab.db2", HotfixStatements.SEL_TALENT_TAB, HotfixStatements.SEL_TALENT_TAB_LOCALE);
            TaxiNodesStorage.ReadDB2(data, "TaxiNodes.db2", HotfixStatements.SEL_TAXI_NODES, HotfixStatements.SEL_TAXI_NODES_LOCALE);
            TaxiPathStorage.ReadDB2(data, "TaxiPath.db2", HotfixStatements.SEL_TAXI_PATH);
            TaxiPathNodeStorage.ReadDB2(data, "TaxiPathNode.db2", HotfixStatements.SEL_TAXI_PATH_NODE);
            TotemCategoryStorage.ReadDB2(data, "TotemCategory.db2", HotfixStatements.SEL_TOTEM_CATEGORY, HotfixStatements.SEL_TOTEM_CATEGORY_LOCALE);
            ToyStorage.ReadDB2(data, "Toy.db2", HotfixStatements.SEL_TOY, HotfixStatements.SEL_TOY_LOCALE);
            TraitCondStorage.ReadDB2(data, "TraitCond.db2", HotfixStatements.SEL_TRAIT_COND);
            TraitCostStorage.ReadDB2(data, "TraitCost.db2", HotfixStatements.SEL_TRAIT_COST);
            TraitCurrencyStorage.ReadDB2(data, "TraitCurrency.db2", HotfixStatements.SEL_TRAIT_CURRENCY);
            TraitCurrencySourceStorage.ReadDB2(data, "TraitCurrencySource.db2", HotfixStatements.SEL_TRAIT_CURRENCY_SOURCE, HotfixStatements.SEL_TRAIT_CURRENCY_SOURCE_LOCALE);
            TraitDefinitionStorage.ReadDB2(data, "TraitDefinition.db2", HotfixStatements.SEL_TRAIT_DEFINITION, HotfixStatements.SEL_TRAIT_DEFINITION_LOCALE);
            TraitDefinitionEffectPointsStorage.ReadDB2(data, "TraitDefinitionEffectPoints.db2", HotfixStatements.SEL_TRAIT_DEFINITION_EFFECT_POINTS);
            TraitEdgeStorage.ReadDB2(data, "TraitEdge.db2", HotfixStatements.SEL_TRAIT_EDGE);
            TraitNodeStorage.ReadDB2(data, "TraitNode.db2", HotfixStatements.SEL_TRAIT_NODE);
            TraitNodeEntryStorage.ReadDB2(data, "TraitNodeEntry.db2", HotfixStatements.SEL_TRAIT_NODE_ENTRY);
            TraitNodeEntryXTraitCondStorage.ReadDB2(data, "TraitNodeEntryXTraitCond.db2", HotfixStatements.SEL_TRAIT_NODE_ENTRY_X_TRAIT_COND);
            TraitNodeEntryXTraitCostStorage.ReadDB2(data, "TraitNodeEntryXTraitCost.db2", HotfixStatements.SEL_TRAIT_NODE_ENTRY_X_TRAIT_COST);
            TraitNodeGroupStorage.ReadDB2(data, "TraitNodeGroup.db2", HotfixStatements.SEL_TRAIT_NODE_GROUP);
            TraitNodeGroupXTraitCondStorage.ReadDB2(data, "TraitNodeGroupXTraitCond.db2", HotfixStatements.SEL_TRAIT_NODE_GROUP_X_TRAIT_COND);
            TraitNodeGroupXTraitCostStorage.ReadDB2(data, "TraitNodeGroupXTraitCost.db2", HotfixStatements.SEL_TRAIT_NODE_GROUP_X_TRAIT_COST);
            TraitNodeGroupXTraitNodeStorage.ReadDB2(data, "TraitNodeGroupXTraitNode.db2", HotfixStatements.SEL_TRAIT_NODE_GROUP_X_TRAIT_NODE);
            TraitNodeXTraitCondStorage.ReadDB2(data, "TraitNodeXTraitCond.db2", HotfixStatements.SEL_TRAIT_NODE_X_TRAIT_COND);
            TraitNodeXTraitCostStorage.ReadDB2(data, "TraitNodeXTraitCost.db2", HotfixStatements.SEL_TRAIT_NODE_X_TRAIT_COST);
            TraitNodeXTraitNodeEntryStorage.ReadDB2(data, "TraitNodeXTraitNodeEntry.db2", HotfixStatements.SEL_TRAIT_NODE_X_TRAIT_NODE_ENTRY);
            TraitTreeStorage.ReadDB2(data, "TraitTree.db2", HotfixStatements.SEL_TRAIT_TREE);
            TraitTreeLoadoutStorage.ReadDB2(data, "TraitTreeLoadout.db2", HotfixStatements.SEL_TRAIT_TREE_LOADOUT);
            TraitTreeLoadoutEntryStorage.ReadDB2(data, "TraitTreeLoadoutEntry.db2", HotfixStatements.SEL_TRAIT_TREE_LOADOUT_ENTRY);
            TraitTreeXTraitCostStorage.ReadDB2(data, "TraitTreeXTraitCost.db2", HotfixStatements.SEL_TRAIT_TREE_X_TRAIT_COST);
            TraitTreeXTraitCurrencyStorage.ReadDB2(data, "TraitTreeXTraitCurrency.db2", HotfixStatements.SEL_TRAIT_TREE_X_TRAIT_CURRENCY);
            TransmogHolidayStorage.ReadDB2(data, "TransmogHoliday.db2", HotfixStatements.SEL_TRANSMOG_HOLIDAY);
            TransmogSetStorage.ReadDB2(data, "TransmogSet.db2", HotfixStatements.SEL_TRANSMOG_SET, HotfixStatements.SEL_TRANSMOG_SET_LOCALE);
            TransmogSetGroupStorage.ReadDB2(data, "TransmogSetGroup.db2", HotfixStatements.SEL_TRANSMOG_SET_GROUP, HotfixStatements.SEL_TRANSMOG_SET_GROUP_LOCALE);
            TransmogSetItemStorage.ReadDB2(data, "TransmogSetItem.db2", HotfixStatements.SEL_TRANSMOG_SET_ITEM);
            TransportAnimationStorage.ReadDB2(data, "TransportAnimation.db2", HotfixStatements.SEL_TRANSPORT_ANIMATION);
            TransportRotationStorage.ReadDB2(data, "TransportRotation.db2", HotfixStatements.SEL_TRANSPORT_ROTATION);
            UiMapStorage.ReadDB2(data, "UiMap.db2", HotfixStatements.SEL_UI_MAP, HotfixStatements.SEL_UI_MAP_LOCALE);
            UiMapAssignmentStorage.ReadDB2(data, "UiMapAssignment.db2", HotfixStatements.SEL_UI_MAP_ASSIGNMENT);
            UiMapLinkStorage.ReadDB2(data, "UiMapLink.db2", HotfixStatements.SEL_UI_MAP_LINK);
            UiMapXMapArtStorage.ReadDB2(data, "UiMapXMapArt.db2", HotfixStatements.SEL_UI_MAP_X_MAP_ART);
            UnitConditionStorage.ReadDB2(data, "UnitCondition.db2", HotfixStatements.SEL_UNIT_CONDITION);
            UnitPowerBarStorage.ReadDB2(data, "UnitPowerBar.db2", HotfixStatements.SEL_UNIT_POWER_BAR, HotfixStatements.SEL_UNIT_POWER_BAR_LOCALE);
            VehicleStorage.ReadDB2(data, "Vehicle.db2", HotfixStatements.SEL_VEHICLE);
            VehicleSeatStorage.ReadDB2(data, "VehicleSeat.db2", HotfixStatements.SEL_VEHICLE_SEAT);
            WMOAreaTableStorage.ReadDB2(data, "WMOAreaTable.db2", HotfixStatements.SEL_WMO_AREA_TABLE, HotfixStatements.SEL_WMO_AREA_TABLE_LOCALE);
            WorldEffectStorage.ReadDB2(data, "WorldEffect.db2", HotfixStatements.SEL_WORLD_EFFECT);
            WorldMapOverlayStorage.ReadDB2(data, "WorldMapOverlay.db2", HotfixStatements.SEL_WORLD_MAP_OVERLAY);
            WorldStateExpressionStorage.ReadDB2(data, "WorldStateExpression.db2", HotfixStatements.SEL_WORLD_STATE_EXPRESSION);

            Global.DB2Mgr.LoadStores();

            foreach (var entry in TaxiPathStorage.Values)
            {
                if (!TaxiPathSetBySource.ContainsKey(entry.FromTaxiNode))
                    TaxiPathSetBySource.Add(entry.FromTaxiNode, new Dictionary<int, TaxiPathBySourceAndDestination>());
                TaxiPathSetBySource[entry.FromTaxiNode][entry.ToTaxiNode] = new TaxiPathBySourceAndDestination(entry.Id, entry.Cost);
            }

            int pathCount = TaxiPathStorage.GetNumRows();

            // Calculate path nodes count
            uint[] pathLength = new uint[pathCount];                           // 0 and some other indexes not used
            foreach (TaxiPathNodeRecord entry in TaxiPathNodeStorage.Values)
                if (pathLength[entry.PathID] < entry.NodeIndex + 1)
                    pathLength[entry.PathID] = (uint)entry.NodeIndex + 1u;

            // Set path length
            for (int i = 0; i < pathCount; ++i)
                TaxiPathNodesByPath[i] = new TaxiPathNodeRecord[pathLength[i]];

            // fill data
            foreach (var entry in TaxiPathNodeStorage.Values)
                TaxiPathNodesByPath[entry.PathID][entry.NodeIndex] = entry;

            var taxiMaskSize = ((TaxiNodesStorage.GetNumRows() - 1) / (1 * 64) + 1) * 8;
            TaxiNodesMask = new byte[taxiMaskSize];
            OldContinentsNodesMask = new byte[taxiMaskSize];
            HordeTaxiNodesMask = new byte[taxiMaskSize];
            AllianceTaxiNodesMask = new byte[taxiMaskSize];

            foreach (var node in TaxiNodesStorage.Values)
            {
                if (!node.IsPartOfTaxiNetwork)
                    continue;

                // valid taxi network node
                int field = (node.Id - 1) / 8;
                byte submask = (byte)(1 << (node.Id - 1) % 8);

                TaxiNodesMask[field] |= submask;
                if (node.HasAnyFlag(TaxiNodeFlags.ShowOnHordeMap))
                    HordeTaxiNodesMask[field] |= submask;
                if (node.HasAnyFlag(TaxiNodeFlags.ShowOnAllianceMap))
                    AllianceTaxiNodesMask[field] |= submask;

                int uiMapId;
                if (!Global.DB2Mgr.GetUiMapPosition(node.Pos.X, node.Pos.Y, node.Pos.Z, node.ContinentID, 0, 0, 0, UiMapSystem.Adventure, false, out uiMapId))
                    Global.DB2Mgr.GetUiMapPosition(node.Pos.X, node.Pos.Y, node.Pos.Z, node.ContinentID, 0, 0, 0, UiMapSystem.Taxi, false, out uiMapId);

                if (uiMapId == 985 || uiMapId == 986)
                    OldContinentsNodesMask[field] |= submask;
            }

            //// Check loaded DB2 files proper version
            if (!AreaTableStorage.ContainsKey(14483) ||               // last area added in 3.4.3 (51943)
                !CharTitlesStorage.ContainsKey(757) ||                // last char title added in 3.4.3 (51943)
                !GemPropertiesStorage.ContainsKey(1629) ||            // last gem property added in 3.4.3 (51943)
                !ItemStorage.ContainsKey(211851) ||                   // last item added in 3.4.3 (51943)
                !ItemExtendedCostStorage.ContainsKey(8328) ||         // last item extended cost added in 3.4.3 (51943)
                !MapStorage.ContainsKey(2567) ||                      // last map added in 3.4.3 (51943)
                !SpellNameStorage.ContainsKey(429548))                // last spell added in 3.4.3 (51943)
            {
                Log.outError(LogFilter.Misc, "You have _outdated_ DB2 files. Please extract correct versions from current using client.");
                Environment.Exit(1);
            }

            Log.outInfo(LogFilter.ServerLoading, $"Initialized {data.counter} DB2 data storages in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");

            return data.availableLocales;
        }

        public static void LoadGameTables(string dataPath)
        {
            uint oldMSTime = Time.GetMSTime();

            string gtPath = dataPath + "/gt/";

            uint loadedFileCount = 0;
            GameTable<T> ReadGameTable<T>(string fileName) where T : new()
            {
                return GameTableReader.Read<T>(gtPath, fileName, ref loadedFileCount);
            }

            ArtifactKnowledgeMultiplierGameTable = ReadGameTable<GtArtifactKnowledgeMultiplierRecord>("ArtifactKnowledgeMultiplier.txt");
            ArtifactLevelXPGameTable = ReadGameTable<GtArtifactLevelXPRecord>("artifactLevelXP.txt");
            BarberShopCostBaseGameTable = ReadGameTable<GtBarberShopCostBaseRecord>("BarberShopCostBase.txt");
            BaseMPGameTable = ReadGameTable<GtBaseMPRecord>("BaseMp.txt");
            BattlePetXPGameTable = ReadGameTable<GtBattlePetXPRecord>("BattlePetXP.txt");
            CombatRatingsGameTable = ReadGameTable<GtCombatRatingsRecord>("CombatRatings.txt");
            CombatRatingsMultByILvlGameTable = ReadGameTable<GtCombatRatingsMultByILvlRecord>("CombatRatingsMultByILvl.txt");
            ItemSocketCostPerLevelGameTable = ReadGameTable<GtItemSocketCostPerLevelRecord>("ItemSocketCostPerLevel.txt");
            HpPerStaGameTable = ReadGameTable<GtHpPerStaRecord>("HpPerSta.txt");
            NpcManaCostScalerGameTable = ReadGameTable<GtNpcManaCostScalerRecord>("NPCManaCostScaler.txt");
            OCTRegenHPGameTable = ReadGameTable<GtOCTRegenHPRecord>("OCTRegenHP.txt");
            OCTRegenMPGameTable = ReadGameTable<GtOCTRegenMPRecord>("OCTRegenMP.txt");
            RegenHPPerSptGameTable = ReadGameTable<GtRegenHPPerSptRecord>("RegenHPPerSpt.txt");
            RegenMPPerSptGameTable = ReadGameTable<GtRegenMPPerSptRecord>("RegenMPPerSpt.txt");
            ShieldBlockRegularGameTable = ReadGameTable<GtShieldBlockRegularRecord>("ShieldBlockRegular.txt");
            SpellScalingGameTable = ReadGameTable<GtSpellScalingRecord>("SpellScaling.txt");

            Log.outInfo(LogFilter.ServerLoading, $"Initialized {loadedFileCount} DBC GameTables data stores in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        #region Main Collections
        public static DB6Storage<int, AchievementRecord> AchievementStorage = new();
        public static DB6Storage<int, AchievementCategoryRecord> AchievementCategoryStorage = new();
        public static DB6Storage<int, AdventureJournalRecord> AdventureJournalStorage = new();
        public static DB6Storage<int, AdventureMapPOIRecord> AdventureMapPOIStorage = new();
        public static DB6Storage<int, AnimationDataRecord> AnimationDataStorage = new();
        public static DB6Storage<int, AnimKitRecord> AnimKitStorage = new();
        public static DB6Storage<int, AreaGroupMemberRecord> AreaGroupMemberStorage = new();
        public static DB6Storage<int, AreaTableRecord> AreaTableStorage = new();
        public static DB6Storage<int, AreaTriggerRecord> AreaTriggerStorage = new();
        public static DB6Storage<int, ArmorLocationRecord> ArmorLocationStorage = new();
        public static DB6Storage<int, ArtifactRecord> ArtifactStorage = new();
        public static DB6Storage<int, ArtifactAppearanceRecord> ArtifactAppearanceStorage = new();
        public static DB6Storage<int, ArtifactAppearanceSetRecord> ArtifactAppearanceSetStorage = new();
        public static DB6Storage<int, ArtifactCategoryRecord> ArtifactCategoryStorage = new();
        public static DB6Storage<int, ArtifactPowerRecord> ArtifactPowerStorage = new();
        public static DB6Storage<int, ArtifactPowerLinkRecord> ArtifactPowerLinkStorage = new();
        public static DB6Storage<int, ArtifactPowerPickerRecord> ArtifactPowerPickerStorage = new();
        public static DB6Storage<int, ArtifactPowerRankRecord> ArtifactPowerRankStorage = new();
        public static DB6Storage<int, ArtifactQuestXPRecord> ArtifactQuestXPStorage = new();
        public static DB6Storage<int, ArtifactTierRecord> ArtifactTierStorage = new();
        public static DB6Storage<int, ArtifactUnlockRecord> ArtifactUnlockStorage = new();
        public static DB6Storage<int, AuctionHouseRecord> AuctionHouseStorage = new();
        public static DB6Storage<int, AzeriteEmpoweredItemRecord> AzeriteEmpoweredItemStorage = new();
        public static DB6Storage<int, AzeriteEssenceRecord> AzeriteEssenceStorage = new();
        public static DB6Storage<int, AzeriteEssencePowerRecord> AzeriteEssencePowerStorage = new();
        public static DB6Storage<int, AzeriteItemRecord> AzeriteItemStorage = new();
        public static DB6Storage<int, AzeriteItemMilestonePowerRecord> AzeriteItemMilestonePowerStorage = new();
        public static DB6Storage<int, AzeriteKnowledgeMultiplierRecord> AzeriteKnowledgeMultiplierStorage = new();
        public static DB6Storage<int, AzeriteLevelInfoRecord> AzeriteLevelInfoStorage = new();
        public static DB6Storage<int, AzeritePowerRecord> AzeritePowerStorage = new();
        public static DB6Storage<int, AzeritePowerSetMemberRecord> AzeritePowerSetMemberStorage = new();
        public static DB6Storage<int, AzeriteTierUnlockRecord> AzeriteTierUnlockStorage = new();
        public static DB6Storage<int, AzeriteTierUnlockSetRecord> AzeriteTierUnlockSetStorage = new();
        public static DB6Storage<int, BankBagSlotPricesRecord> BankBagSlotPricesStorage = new();
        public static DB6Storage<int, BannedAddonsRecord> BannedAddOnsStorage = new();
        public static DB6Storage<int, BarberShopStyleRecord> BarberShopStyleStorage = new();
        public static DB6Storage<int, BattlePetAbilityRecord> BarberBattlePetAbilityStorage = new();
        public static DB6Storage<int, BattlePetBreedQualityRecord> BattlePetBreedQualityStorage = new();
        public static DB6Storage<int, BattlePetBreedStateRecord> BattlePetBreedStateStorage = new();
        public static DB6Storage<int, BattlePetSpeciesRecord> BattlePetSpeciesStorage = new();
        public static DB6Storage<int, BattlePetSpeciesStateRecord> BattlePetSpeciesStateStorage = new();
        public static DB6Storage<int, BattlemasterListRecord> BattlemasterListStorage = new();
        public static DB6Storage<int, BroadcastTextRecord> BroadcastTextStorage = new();
        public static DB6Storage<int, Cfg_CategoriesRecord> CfgCategoriesStorage = new();
        public static DB6Storage<int, Cfg_RegionsRecord> CfgRegionsStorage = new();
        public static DB6Storage<int, CharTitlesRecord> CharTitlesStorage = new();
        public static DB6Storage<int, CharacterLoadoutRecord> CharacterLoadoutStorage = new();
        public static DB6Storage<int, CharacterLoadoutItemRecord> CharacterLoadoutItemStorage = new();
        public static DB6Storage<int, ChatChannelsRecord> ChatChannelsStorage = new();
        public static DB6Storage<int, ChrClassUIDisplayRecord> ChrClassUIDisplayStorage = new();
        public static DB6Storage<int, ChrClassesRecord> ChrClassesStorage = new();
        public static DB6Storage<int, ChrClassesXPowerTypesRecord> ChrClassesXPowerTypesStorage = new();
        public static DB6Storage<int, ChrCustomizationChoiceRecord> ChrCustomizationChoiceStorage = new();
        public static DB6Storage<int, ChrCustomizationDisplayInfoRecord> ChrCustomizationDisplayInfoStorage = new();
        public static DB6Storage<int, ChrCustomizationElementRecord> ChrCustomizationElementStorage = new();
        public static DB6Storage<int, ChrCustomizationOptionRecord> ChrCustomizationOptionStorage = new();
        public static DB6Storage<int, ChrCustomizationReqRecord> ChrCustomizationReqStorage = new();
        public static DB6Storage<int, ChrCustomizationReqChoiceRecord> ChrCustomizationReqChoiceStorage = new();
        public static DB6Storage<int, ChrModelRecord> ChrModelStorage = new();
        public static DB6Storage<int, ChrRaceXChrModelRecord> ChrRaceXChrModelStorage = new();
        public static DB6Storage<int, ChrRacesRecord> ChrRacesStorage = new();
        public static DB6Storage<int, ChrSpecializationRecord> ChrSpecializationStorage = new();
        public static DB6Storage<int, CinematicCameraRecord> CinematicCameraStorage = new();
        public static DB6Storage<int, CinematicSequencesRecord> CinematicSequencesStorage = new();
        public static DB6Storage<int, ConditionalChrModelRecord> ConditionalChrModelStorage = new();
        public static DB6Storage<int, ConditionalContentTuningRecord> ConditionalContentTuningStorage = new();
        public static DB6Storage<int, ContentTuningRecord> ContentTuningStorage = new();
        public static DB6Storage<int, ConversationLineRecord> ConversationLineStorage = new();
        public static DB6Storage<int, CreatureDisplayInfoRecord> CreatureDisplayInfoStorage = new();
        public static DB6Storage<int, CreatureDisplayInfoExtraRecord> CreatureDisplayInfoExtraStorage = new();
        public static DB6Storage<int, CreatureFamilyRecord> CreatureFamilyStorage = new();
        public static DB6Storage<int, CreatureModelDataRecord> CreatureModelDataStorage = new();
        public static DB6Storage<int, CreatureTypeRecord> CreatureTypeStorage = new();
        public static DB6Storage<int, CriteriaRecord> CriteriaStorage = new();
        public static DB6Storage<int, CriteriaTreeRecord> CriteriaTreeStorage = new();
        public static DB6Storage<int, CurrencyContainerRecord> CurrencyContainerStorage = new();
        public static DB6Storage<int, CurrencyTypesRecord> CurrencyTypesStorage = new();
        public static DB6Storage<int, CurveRecord> CurveStorage = new();
        public static DB6Storage<int, CurvePointRecord> CurvePointStorage = new();
        public static DB6Storage<int, DestructibleModelDataRecord> DestructibleModelDataStorage = new();
        public static DB6Storage<Difficulty, DifficultyRecord> DifficultyStorage = new();
        public static DB6Storage<int, DungeonEncounterRecord> DungeonEncounterStorage = new();
        public static DB6Storage<int, DurabilityCostsRecord> DurabilityCostsStorage = new();
        public static DB6Storage<int, DurabilityQualityRecord> DurabilityQualityStorage = new();
        public static DB6Storage<int, EmotesRecord> EmotesStorage = new();
        public static DB6Storage<int, EmotesTextRecord> EmotesTextStorage = new();
        public static DB6Storage<int, EmotesTextSoundRecord> EmotesTextSoundStorage = new();
        public static DB6Storage<int, ExpectedStatRecord> ExpectedStatStorage = new();
        public static DB6Storage<int, ExpectedStatModRecord> ExpectedStatModStorage = new();
        public static DB6Storage<int, FactionRecord> FactionStorage = new();
        public static DB6Storage<int, FactionTemplateRecord> FactionTemplateStorage = new();
        public static DB6Storage<int, FriendshipRepReactionRecord> FriendshipRepReactionStorage = new();
        public static DB6Storage<int, FriendshipReputationRecord> FriendshipReputationStorage = new();
        public static DB6Storage<int, GameObjectArtKitRecord> GameObjectArtKitStorage = new();
        public static DB6Storage<int, GameObjectDisplayInfoRecord> GameObjectDisplayInfoStorage = new();
        public static DB6Storage<int, GameObjectsRecord> GameObjectsStorage = new();
        public static DB6Storage<int, GarrAbilityRecord> GarrAbilityStorage = new();
        public static DB6Storage<int, GarrBuildingRecord> GarrBuildingStorage = new();
        public static DB6Storage<int, GarrBuildingPlotInstRecord> GarrBuildingPlotInstStorage = new();
        public static DB6Storage<int, GarrClassSpecRecord> GarrClassSpecStorage = new();
        public static DB6Storage<int, GarrFollowerRecord> GarrFollowerStorage = new();
        public static DB6Storage<int, GarrFollowerXAbilityRecord> GarrFollowerXAbilityStorage = new();
        public static DB6Storage<int, GarrMissionRecord> GarrMissionStorage = new();
        public static DB6Storage<int, GarrPlotRecord> GarrPlotStorage = new();
        public static DB6Storage<int, GarrPlotBuildingRecord> GarrPlotBuildingStorage = new();
        public static DB6Storage<int, GarrPlotInstanceRecord> GarrPlotInstanceStorage = new();
        public static DB6Storage<int, GarrSiteLevelRecord> GarrSiteLevelStorage = new();
        public static DB6Storage<int, GarrSiteLevelPlotInstRecord> GarrSiteLevelPlotInstStorage = new();
        public static DB6Storage<int, GarrTalentTreeRecord> GarrTalentTreeStorage = new();
        public static DB6Storage<int, GemPropertiesRecord> GemPropertiesStorage = new();
        public static DB6Storage<int, GlyphBindableSpellRecord> GlyphBindableSpellStorage = new();
        public static DB6Storage<int, GlyphSlotRecord> GlyphSlotStorage = new();
        public static DB6Storage<int, GlyphPropertiesRecord> GlyphPropertiesStorage = new();
        public static DB6Storage<int, GlyphRequiredSpecRecord> GlyphRequiredSpecStorage = new();
        public static DB6Storage<int, GossipNPCOptionRecord> GossipNPCOptionStorage = new();
        public static DB6Storage<int, GuildColorBackgroundRecord> GuildColorBackgroundStorage = new();
        public static DB6Storage<int, GuildColorBorderRecord> GuildColorBorderStorage = new();
        public static DB6Storage<int, GuildColorEmblemRecord> GuildColorEmblemStorage = new();
        public static DB6Storage<int, GuildPerkSpellsRecord> GuildPerkSpellsStorage = new();
        public static DB6Storage<int, HeirloomRecord> HeirloomStorage = new();
        public static DB6Storage<int, HolidaysRecord> HolidaysStorage = new();
        public static DB6Storage<int, ImportPriceArmorRecord> ImportPriceArmorStorage = new();
        public static DB6Storage<int, ImportPriceQualityRecord> ImportPriceQualityStorage = new();
        public static DB6Storage<int, ImportPriceShieldRecord> ImportPriceShieldStorage = new();
        public static DB6Storage<int, ImportPriceWeaponRecord> ImportPriceWeaponStorage = new();
        public static DB6Storage<int, ItemAppearanceRecord> ItemAppearanceStorage = new();
        public static DB6Storage<int, ItemArmorQualityRecord> ItemArmorQualityStorage = new();
        public static DB6Storage<int, ItemArmorShieldRecord> ItemArmorShieldStorage = new();
        public static DB6Storage<int, ItemArmorTotalRecord> ItemArmorTotalStorage = new();
        public static DB6Storage<int, ItemBagFamilyRecord> ItemBagFamilyStorage = new();
        public static DB6Storage<int, ItemBonusRecord> ItemBonusStorage = new();
        public static DB6Storage<int, ItemBonusListLevelDeltaRecord> ItemBonusListLevelDeltaStorage = new();
        public static DB6Storage<int, ItemBonusTreeNodeRecord> ItemBonusTreeNodeStorage = new();
        public static DB6Storage<int, ItemChildEquipmentRecord> ItemChildEquipmentStorage = new();
        public static DB6Storage<int, ItemClassRecord> ItemClassStorage = new();
        public static DB6Storage<int, ItemContextPickerEntryRecord> ItemContextPickerEntryStorage = new();
        public static DB6Storage<int, ItemCurrencyCostRecord> ItemCurrencyCostStorage = new();
        public static DB6Storage<int, ItemDamageRecord> ItemDamageAmmoStorage = new();
        public static DB6Storage<int, ItemDamageRecord> ItemDamageOneHandStorage = new();
        public static DB6Storage<int, ItemDamageRecord> ItemDamageOneHandCasterStorage = new();
        public static DB6Storage<int, ItemDamageRecord> ItemDamageTwoHandStorage = new();
        public static DB6Storage<int, ItemDamageRecord> ItemDamageTwoHandCasterStorage = new();
        public static DB6Storage<int, ItemDisenchantLootRecord> ItemDisenchantLootStorage = new();
        public static DB6Storage<int, ItemEffectRecord> ItemEffectStorage = new();
        public static DB6Storage<int, ItemRecord> ItemStorage = new();
        public static DB6Storage<int, ItemExtendedCostRecord> ItemExtendedCostStorage = new();
        public static DB6Storage<int, ItemLevelSelectorRecord> ItemLevelSelectorStorage = new();
        public static DB6Storage<int, ItemLevelSelectorQualityRecord> ItemLevelSelectorQualityStorage = new();
        public static DB6Storage<int, ItemLevelSelectorQualitySetRecord> ItemLevelSelectorQualitySetStorage = new();
        public static DB6Storage<int, ItemLimitCategoryRecord> ItemLimitCategoryStorage = new();
        public static DB6Storage<int, ItemLimitCategoryConditionRecord> ItemLimitCategoryConditionStorage = new();
        public static DB6Storage<int, ItemModifiedAppearanceRecord> ItemModifiedAppearanceStorage = new();
        public static DB6Storage<int, ItemModifiedAppearanceExtraRecord> ItemModifiedAppearanceExtraStorage = new();
        public static DB6Storage<int, ItemNameDescriptionRecord> ItemNameDescriptionStorage = new();
        public static DB6Storage<int, ItemPriceBaseRecord> ItemPriceBaseStorage = new();
        public static DB6Storage<int, ItemRandomPropertiesRecord> ItemRandomPropertiesStorage = new();
        public static DB6Storage<int, ItemRandomSuffixRecord> ItemRandomSuffixStorage = new();
        public static DB6Storage<int, ItemSearchNameRecord> ItemSearchNameStorage = new();
        public static DB6Storage<int, ItemSetRecord> ItemSetStorage = new();
        public static DB6Storage<int, ItemSetSpellRecord> ItemSetSpellStorage = new();
        public static DB6Storage<int, ItemSparseRecord> ItemSparseStorage = new();
        public static DB6Storage<int, ItemSpecRecord> ItemSpecStorage = new();
        public static DB6Storage<int, ItemSpecOverrideRecord> ItemSpecOverrideStorage = new();
        public static DB6Storage<int, ItemXBonusTreeRecord> ItemXBonusTreeStorage = new();
        public static DB6Storage<int, JournalEncounterRecord> JournalEncounterStorage = new();
        public static DB6Storage<int, JournalEncounterSectionRecord> JournalEncounterSectionStorage = new();
        public static DB6Storage<int, JournalInstanceRecord> JournalInstanceStorage = new();
        public static DB6Storage<int, JournalTierRecord> JournalTierStorage = new();
        public static DB6Storage<int, KeyChainRecord> KeyChainStorage = new();
        public static DB6Storage<int, KeystoneAffixRecord> KeystoneAffixStorage = new();
        public static DB6Storage<int, LanguageWordsRecord> LanguageWordsStorage = new();
        public static DB6Storage<int, LanguagesRecord> LanguagesStorage = new();
        public static DB6Storage<int, LFGDungeonsRecord> LFGDungeonsStorage = new();
        public static DB6Storage<int, LightRecord> LightStorage = new();
        public static DB6Storage<int, LiquidTypeRecord> LiquidTypeStorage = new();
        public static DB6Storage<int, LockRecord> LockStorage = new();
        public static DB6Storage<int, MailTemplateRecord> MailTemplateStorage = new();
        public static DB6Storage<int, MapRecord> MapStorage = new();
        public static DB6Storage<int, MapChallengeModeRecord> MapChallengeModeStorage = new();
        public static DB6Storage<int, MapDifficultyRecord> MapDifficultyStorage = new();
        public static DB6Storage<int, MapDifficultyXConditionRecord> MapDifficultyXConditionStorage = new();
        public static DB6Storage<int, ModifierTreeRecord> ModifierTreeStorage = new();
        public static DB6Storage<int, MountCapabilityRecord> MountCapabilityStorage = new();
        public static DB6Storage<int, MountRecord> MountStorage = new();
        public static DB6Storage<int, MountTypeXCapabilityRecord> MountTypeXCapabilityStorage = new();
        public static DB6Storage<int, MountXDisplayRecord> MountXDisplayStorage = new();
        public static DB6Storage<int, MovieRecord> MovieStorage = new();
        public static DB6Storage<int, MythicPlusSeasonRecord> MythicPlusSeasonStorage = new();
        public static DB6Storage<int, NameGenRecord> NameGenStorage = new();
        public static DB6Storage<int, NamesProfanityRecord> NamesProfanityStorage = new();
        public static DB6Storage<int, NamesReservedRecord> NamesReservedStorage = new();
        public static DB6Storage<int, NamesReservedLocaleRecord> NamesReservedLocaleStorage = new();
        public static DB6Storage<int, NumTalentsAtLevelRecord> NumTalentsAtLevelStorage = new();
        public static DB6Storage<int, OverrideSpellDataRecord> OverrideSpellDataStorage = new();
        public static DB6Storage<int, ParagonReputationRecord> ParagonReputationStorage = new();
        public static DB6Storage<int, PhaseRecord> PhaseStorage = new();
        public static DB6Storage<int, PhaseXPhaseGroupRecord> PhaseXPhaseGroupStorage = new();
        public static DB6Storage<int, PlayerConditionRecord> PlayerConditionStorage = new();
        public static DB6Storage<int, PowerDisplayRecord> PowerDisplayStorage = new();
        public static DB6Storage<int, PowerTypeRecord> PowerTypeStorage = new();
        public static DB6Storage<int, PrestigeLevelInfoRecord> PrestigeLevelInfoStorage = new();
        public static DB6Storage<int, PvpDifficultyRecord> PvpDifficultyStorage = new();
        public static DB6Storage<int, PvpItemRecord> PvpItemStorage = new();
        public static DB6Storage<int, PvpSeasonRecord> PvpSeasonStorage = new();
        public static DB6Storage<int, PvpTalentRecord> PvpTalentStorage = new();
        public static DB6Storage<int, PvpTalentCategoryRecord> PvpTalentCategoryStorage = new();
        public static DB6Storage<int, PvpTalentSlotUnlockRecord> PvpTalentSlotUnlockStorage = new();
        public static DB6Storage<int, PvpTierRecord> PvpTierStorage = new();
        public static DB6Storage<int, QuestFactionRewardRecord> QuestFactionRewardStorage = new();
        public static DB6Storage<int, QuestInfoRecord> QuestInfoStorage = new();
        public static DB6Storage<int, QuestLineXQuestRecord> QuestLineXQuestStorage = new();
        public static DB6Storage<int, QuestMoneyRewardRecord> QuestMoneyRewardStorage = new();
        public static DB6Storage<int, QuestPackageItemRecord> QuestPackageItemStorage = new();
        public static DB6Storage<int, QuestSortRecord> QuestSortStorage = new();
        public static DB6Storage<int, QuestV2Record> QuestV2Storage = new();
        public static DB6Storage<int, QuestXPRecord> QuestXPStorage = new();
        public static DB6Storage<int, RandPropPointsRecord> RandPropPointsStorage = new();
        public static DB6Storage<int, RewardPackRecord> RewardPackStorage = new();
        public static DB6Storage<int, RewardPackXCurrencyTypeRecord> RewardPackXCurrencyTypeStorage = new();
        public static DB6Storage<int, RewardPackXItemRecord> RewardPackXItemStorage = new();
        public static DB6Storage<int, ScalingStatDistributionRecord> ScalingStatDistributionStorage = new();
        public static DB6Storage<int, ScalingStatValuesRecord> ScalingStatValuesStorage = new();
        public static DB6Storage<int, ScenarioRecord> ScenarioStorage = new();
        public static DB6Storage<int, ScenarioStepRecord> ScenarioStepStorage = new();
        public static DB6Storage<int, SceneScriptRecord> SceneScriptStorage = new();
        public static DB6Storage<int, SceneScriptGlobalTextRecord> SceneScriptGlobalTextStorage = new();
        public static DB6Storage<int, SceneScriptPackageRecord> SceneScriptPackageStorage = new();
        public static DB6Storage<int, SceneScriptTextRecord> SceneScriptTextStorage = new();
        public static DB6Storage<int, ServerMessagesRecord> ServerMessagesStorage = new();
        public static DB6Storage<int, SkillLineRecord> SkillLineStorage = new();
        public static DB6Storage<int, SkillLineAbilityRecord> SkillLineAbilityStorage = new();
        public static DB6Storage<int, SkillLineXTraitTreeRecord> SkillLineXTraitTreeStorage = new();
        public static DB6Storage<int, SkillRaceClassInfoRecord> SkillRaceClassInfoStorage = new();
        public static DB6Storage<int, SoundKitRecord> SoundKitStorage = new();
        public static DB6Storage<int, SpecializationSpellsRecord> SpecializationSpellsStorage = new();
        public static DB6Storage<int, SpecSetMemberRecord> SpecSetMemberStorage = new();
        public static DB6Storage<int, SpellAuraOptionsRecord> SpellAuraOptionsStorage = new();
        public static DB6Storage<int, SpellAuraRestrictionsRecord> SpellAuraRestrictionsStorage = new();
        public static DB6Storage<int, SpellCastTimesRecord> SpellCastTimesStorage = new();
        public static DB6Storage<int, SpellCastingRequirementsRecord> SpellCastingRequirementsStorage = new();
        public static DB6Storage<int, SpellCategoriesRecord> SpellCategoriesStorage = new();
        public static DB6Storage<int, SpellCategoryRecord> SpellCategoryStorage = new();
        public static DB6Storage<int, SpellClassOptionsRecord> SpellClassOptionsStorage = new();
        public static DB6Storage<int, SpellCooldownsRecord> SpellCooldownsStorage = new();
        public static DB6Storage<int, SpellDurationRecord> SpellDurationStorage = new();
        public static DB6Storage<int, SpellEffectRecord> SpellEffectStorage = new();
        public static DB6Storage<int, SpellEquippedItemsRecord> SpellEquippedItemsStorage = new();
        public static DB6Storage<int, SpellFocusObjectRecord> SpellFocusObjectStorage = new();
        public static DB6Storage<int, SpellInterruptsRecord> SpellInterruptsStorage = new();
        public static DB6Storage<int, SpellItemEnchantmentRecord> SpellItemEnchantmentStorage = new();
        public static DB6Storage<int, SpellItemEnchantmentConditionRecord> SpellItemEnchantmentConditionStorage = new();
        public static DB6Storage<int, SpellKeyboundOverrideRecord> SpellKeyboundOverrideStorage = new();
        public static DB6Storage<int, SpellLabelRecord> SpellLabelStorage = new();
        public static DB6Storage<int, SpellLearnSpellRecord> SpellLearnSpellStorage = new();
        public static DB6Storage<int, SpellLevelsRecord> SpellLevelsStorage = new();
        public static DB6Storage<int, SpellMiscRecord> SpellMiscStorage = new();
        public static DB6Storage<int, SpellNameRecord> SpellNameStorage = new();
        public static DB6Storage<int, SpellPowerRecord> SpellPowerStorage = new();
        public static DB6Storage<int, SpellPowerDifficultyRecord> SpellPowerDifficultyStorage = new();
        public static DB6Storage<int, SpellProcsPerMinuteRecord> SpellProcsPerMinuteStorage = new();
        public static DB6Storage<int, SpellProcsPerMinuteModRecord> SpellProcsPerMinuteModStorage = new();
        public static DB6Storage<int, SpellRadiusRecord> SpellRadiusStorage = new();
        public static DB6Storage<int, SpellRangeRecord> SpellRangeStorage = new();
        public static DB6Storage<int, SpellReagentsRecord> SpellReagentsStorage = new();
        public static DB6Storage<int, SpellReagentsCurrencyRecord> SpellReagentsCurrencyStorage = new();
        public static DB6Storage<int, SpellScalingRecord> SpellScalingStorage = new();
        public static DB6Storage<int, SpellShapeshiftRecord> SpellShapeshiftStorage = new();
        public static DB6Storage<int, SpellShapeshiftFormRecord> SpellShapeshiftFormStorage = new();
        public static DB6Storage<int, SpellTargetRestrictionsRecord> SpellTargetRestrictionsStorage = new();
        public static DB6Storage<int, SpellTotemsRecord> SpellTotemsStorage = new();
        public static DB6Storage<int, SpellVisualRecord> SpellVisualStorage = new();
        public static DB6Storage<int, SpellVisualEffectNameRecord> SpellVisualEffectNameStorage = new();
        public static DB6Storage<int, SpellVisualMissileRecord> SpellVisualMissileStorage = new();
        public static DB6Storage<int, SpellVisualKitRecord> SpellVisualKitStorage = new();
        public static DB6Storage<int, SpellXSpellVisualRecord> SpellXSpellVisualStorage = new();
        public static DB6Storage<int, SummonPropertiesRecord> SummonPropertiesStorage = new();
        public static DB6Storage<int, TactKeyRecord> TactKeyStorage = new();
        public static DB6Storage<int, TalentRecord> TalentStorage = new();
        public static DB6Storage<int, TalentTabRecord> TalentTabStorage = new();
        public static DB6Storage<int, TaxiNodesRecord> TaxiNodesStorage = new();
        public static DB6Storage<int, TaxiPathRecord> TaxiPathStorage = new();
        public static DB6Storage<int, TaxiPathNodeRecord> TaxiPathNodeStorage = new();
        public static DB6Storage<int, TotemCategoryRecord> TotemCategoryStorage = new();
        public static DB6Storage<int, ToyRecord> ToyStorage = new();
        public static DB6Storage<int, TraitCondRecord> TraitCondStorage = new();
        public static DB6Storage<int, TraitCostRecord> TraitCostStorage = new();
        public static DB6Storage<int, TraitCurrencyRecord> TraitCurrencyStorage = new();
        public static DB6Storage<int, TraitCurrencySourceRecord> TraitCurrencySourceStorage = new();
        public static DB6Storage<int, TraitDefinitionRecord> TraitDefinitionStorage = new();
        public static DB6Storage<int, TraitDefinitionEffectPointsRecord> TraitDefinitionEffectPointsStorage = new();
        public static DB6Storage<int, TraitEdgeRecord> TraitEdgeStorage = new();
        public static DB6Storage<int, TraitNodeRecord> TraitNodeStorage = new();
        public static DB6Storage<int, TraitNodeEntryRecord> TraitNodeEntryStorage = new();
        public static DB6Storage<int, TraitNodeEntryXTraitCondRecord> TraitNodeEntryXTraitCondStorage = new();
        public static DB6Storage<int, TraitNodeEntryXTraitCostRecord> TraitNodeEntryXTraitCostStorage = new();
        public static DB6Storage<int, TraitNodeGroupRecord> TraitNodeGroupStorage = new();
        public static DB6Storage<int, TraitNodeGroupXTraitCondRecord> TraitNodeGroupXTraitCondStorage = new();
        public static DB6Storage<int, TraitNodeGroupXTraitCostRecord> TraitNodeGroupXTraitCostStorage = new();
        public static DB6Storage<int, TraitNodeGroupXTraitNodeRecord> TraitNodeGroupXTraitNodeStorage = new();
        public static DB6Storage<int, TraitNodeXTraitCondRecord> TraitNodeXTraitCondStorage = new();
        public static DB6Storage<int, TraitNodeXTraitCostRecord> TraitNodeXTraitCostStorage = new();
        public static DB6Storage<int, TraitNodeXTraitNodeEntryRecord> TraitNodeXTraitNodeEntryStorage = new();
        public static DB6Storage<int, TraitTreeRecord> TraitTreeStorage = new();
        public static DB6Storage<int, TraitTreeLoadoutRecord> TraitTreeLoadoutStorage = new();
        public static DB6Storage<int, TraitTreeLoadoutEntryRecord> TraitTreeLoadoutEntryStorage = new();
        public static DB6Storage<int, TraitTreeXTraitCostRecord> TraitTreeXTraitCostStorage = new();
        public static DB6Storage<int, TraitTreeXTraitCurrencyRecord> TraitTreeXTraitCurrencyStorage = new();
        public static DB6Storage<int, TransmogHolidayRecord> TransmogHolidayStorage = new();
        public static DB6Storage<int, TransmogSetRecord> TransmogSetStorage = new();
        public static DB6Storage<int, TransmogSetGroupRecord> TransmogSetGroupStorage = new();
        public static DB6Storage<int, TransmogSetItemRecord> TransmogSetItemStorage = new();
        public static DB6Storage<int, TransportAnimationRecord> TransportAnimationStorage = new();
        public static DB6Storage<int, TransportRotationRecord> TransportRotationStorage = new();
        public static DB6Storage<int, UiMapRecord> UiMapStorage = new();
        public static DB6Storage<int, UiMapAssignmentRecord> UiMapAssignmentStorage = new();
        public static DB6Storage<int, UiMapLinkRecord> UiMapLinkStorage = new();
        public static DB6Storage<int, UiMapXMapArtRecord> UiMapXMapArtStorage = new();
        public static DB6Storage<int, UnitConditionRecord> UnitConditionStorage = new();
        public static DB6Storage<int, UnitPowerBarRecord> UnitPowerBarStorage = new();
        public static DB6Storage<int, VehicleRecord> VehicleStorage = new();
        public static DB6Storage<int, VehicleSeatRecord> VehicleSeatStorage = new();
        public static DB6Storage<int, WMOAreaTableRecord> WMOAreaTableStorage = new();
        public static DB6Storage<int, WorldEffectRecord> WorldEffectStorage = new();
        public static DB6Storage<int, WorldMapOverlayRecord> WorldMapOverlayStorage = new();
        public static DB6Storage<int, WorldStateExpressionRecord> WorldStateExpressionStorage = new();
        #endregion

        #region GameTables
        public static GameTable<GtArtifactKnowledgeMultiplierRecord> ArtifactKnowledgeMultiplierGameTable;
        public static GameTable<GtArtifactLevelXPRecord> ArtifactLevelXPGameTable;
        public static GameTable<GtBarberShopCostBaseRecord> BarberShopCostBaseGameTable;
        public static GameTable<GtBaseMPRecord> BaseMPGameTable;
        public static GameTable<GtBattlePetXPRecord> BattlePetXPGameTable;
        public static GameTable<GtCombatRatingsRecord> CombatRatingsGameTable;
        public static GameTable<GtCombatRatingsMultByILvlRecord> CombatRatingsMultByILvlGameTable;
        public static GameTable<GtHpPerStaRecord> HpPerStaGameTable;
        public static GameTable<GtItemSocketCostPerLevelRecord> ItemSocketCostPerLevelGameTable;
        public static GameTable<GtNpcManaCostScalerRecord> NpcManaCostScalerGameTable;
        public static GameTable<GtOCTRegenHPRecord> OCTRegenHPGameTable;
        public static GameTable<GtOCTRegenMPRecord> OCTRegenMPGameTable;
        public static GameTable<GtRegenHPPerSptRecord> RegenHPPerSptGameTable;
        public static GameTable<GtRegenMPPerSptRecord> RegenMPPerSptGameTable;
        public static GameTable<GtShieldBlockRegularRecord> ShieldBlockRegularGameTable;
        public static GameTable<GtSpellScalingRecord> SpellScalingGameTable;
        #endregion

        #region Taxi Collections
        public static byte[] TaxiNodesMask;
        public static byte[] OldContinentsNodesMask;
        public static byte[] HordeTaxiNodesMask;
        public static byte[] AllianceTaxiNodesMask;
        public static Dictionary<int, Dictionary<int, TaxiPathBySourceAndDestination>> TaxiPathSetBySource = new();
        public static Dictionary<int, TaxiPathNodeRecord[]> TaxiPathNodesByPath = new();
        #endregion

        #region Talent Collections
        public static Dictionary<int, TalentSpellPos> TalentSpellPosMap = new();
        public static HashSet<int> PetTalentSpells = new();
        /// <summary>store absolute bit position for first rank for talent inspect</summary>        
        public static uint[][] TalentTabPages = new uint[(uint)Class.Max][];
        #endregion

        #region Helper Methods
        public static float GetGameTableColumnForClass(dynamic row, Class class_)
        {
            switch (class_)
            {
                case Class.Warrior:
                    return row.Warrior;
                case Class.Paladin:
                    return row.Paladin;
                case Class.Hunter:
                    return row.Hunter;
                case Class.Rogue:
                    return row.Rogue;
                case Class.Priest:
                    return row.Priest;
                case Class.Deathknight:
                    return row.DeathKnight;
                case Class.Shaman:
                    return row.Shaman;
                case Class.Mage:
                    return row.Mage;
                case Class.Warlock:
                    return row.Warlock;
                case Class.Druid:
                    return row.Druid;
                default:
                    break;
            }
            return 0.0f;
        }

        public static float GetSpellScalingColumnForClass(GtSpellScalingRecord row, ScalingClass class_)
        {
            switch (class_)
            {
                case ScalingClass.Warrior:
                    return row.Warrior;
                case ScalingClass.Paladin:
                    return row.Paladin;
                case ScalingClass.Hunter:
                    return row.Hunter;
                case ScalingClass.Rogue:
                    return row.Rogue;
                case ScalingClass.Priest:
                    return row.Priest;
                case ScalingClass.Deathknight:
                    return row.DeathKnight;
                case ScalingClass.Shaman:
                    return row.Shaman;
                case ScalingClass.Mage:
                    return row.Mage;
                case ScalingClass.Warlock:
                    return row.Warlock;
                case ScalingClass.Druid:
                    return row.Druid;
                case ScalingClass.Item1:
                case ScalingClass.Item2:
                    return row.Item;
                case ScalingClass.Consumable:
                    return row.Consumable;
                case ScalingClass.Gem1:
                    return row.Gem1;
                case ScalingClass.Gem2:
                    return row.Gem2;
                case ScalingClass.Gem3:
                    return row.Gem3;
                case ScalingClass.Health:
                    return row.Health;
                default:
                    break;
            }
            return 0.0f;
        }

        public static float GetBattlePetXPPerLevel(GtBattlePetXPRecord row)
        {
            return row.Wins * row.Xp;
        }

        public static int GetShieldBlockRegularColumnForQuality(GtShieldBlockRegularRecord row, ItemQuality quality)
        {
            int value;

            switch (quality)
            {
                case ItemQuality.Poor:
                    value = (int)row.Poor;
                    break;
                case ItemQuality.Normal:
                    value = (int)row.Standard;
                    break;
                case ItemQuality.Uncommon:
                    value = (int)row.Good;
                    break;
                case ItemQuality.Rare:
                    value = (int)row.Superior;
                    break;
                case ItemQuality.Epic:
                    value = (int)row.Epic;
                    break;
                case ItemQuality.Legendary:
                    value = (int)row.Legendary;
                    break;
                case ItemQuality.Artifact:
                    value = (int)row.Artifact;
                    break;
                case ItemQuality.Heirloom:
                    value = (int)row.ScalingStat;
                    break;
                default:
                    value = 0;
                    break;
            }
            return value;
        }
        #endregion
    }
}
