﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Conditions;
using Game.DataStorage;
using Game.Entities;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Game.Loots
{
    using LootStoreItemList = List<LootStoreItem>;
    using LootTemplateMap = Dictionary<int, LootTemplate>;

    public class LootManager : LootStorage
    {
        static void Initialize()
        {
            Creature = new LootStore("creature_loot_template", "creature entry");
            Disenchant = new LootStore("disenchant_loot_template", "item disenchant id");
            Fishing = new LootStore("fishing_loot_template", "area id");
            Gameobject = new LootStore("gameobject_loot_template", "gameobject entry");
            Items = new LootStore("item_loot_template", "item entry");
            Mail = new LootStore("mail_loot_template", "mail template id", false);
            Milling = new LootStore("milling_loot_template", "item entry (herb)");
            Pickpocketing = new LootStore("pickpocketing_loot_template", "creature pickpocket lootid");
            Prospecting = new LootStore("prospecting_loot_template", "item entry (ore)");
            Reference = new LootStore("reference_loot_template", "reference id", false);
            Skinning = new LootStore("skinning_loot_template", "creature skinning id");
            Spell = new LootStore("spell_loot_template", "spell id (random item creating)", false);
        }

        public static void LoadLootTables()
        {
            Initialize();
            LoadLootTemplates_Creature();
            LoadLootTemplates_Fishing();
            LoadLootTemplates_Gameobject();
            LoadLootTemplates_Item();
            LoadLootTemplates_Mail();
            LoadLootTemplates_Milling();
            LoadLootTemplates_Pickpocketing();
            LoadLootTemplates_Skinning();
            LoadLootTemplates_Disenchant();
            LoadLootTemplates_Prospecting();
            LoadLootTemplates_Spell();

            LoadLootTemplates_Reference();
        }

        public static Dictionary<ObjectGuid, Loot> GenerateDungeonEncounterPersonalLoot(int dungeonEncounterId, int lootId, LootStore store,
            LootType type, WorldObject lootOwner, long minMoney, long maxMoney, LootModes lootMode, MapDifficultyRecord mapDifficulty, List<Player> tappers)
        {
            Dictionary<Player, Loot> tempLoot = new();

            foreach (Player tapper in tappers)
            {
                if (tapper.IsLockedToDungeonEncounter(dungeonEncounterId))
                    continue;

                Loot loot = new(lootOwner.GetMap(), lootOwner.GetGUID(), type, null);
                loot.SetDungeonEncounterId(dungeonEncounterId);
                loot.GenerateMoneyLoot(minMoney, maxMoney);

                tempLoot[tapper] = loot;
            }

            LootTemplate tab = store.GetLootFor(lootId);
            if (tab != null)
                tab.ProcessPersonalLoot(tempLoot, store.IsRatesAllowed(), lootMode);

            Dictionary<ObjectGuid, Loot> personalLoot = new();
            foreach (var (looter, loot) in tempLoot)
            {
                loot.FillNotNormalLootFor(looter);

                if (loot.IsLooted())
                    continue;

                personalLoot[looter.GetGUID()] = loot;
            }

            return personalLoot;
        }

        public static void LoadLootTemplates_Creature()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading creature loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet, lootIdSetUsed = new();
            int count = Creature.LoadAndCollectLootIds(out lootIdSet);

            // Remove real entries and check loot existence
            var templates = Global.ObjectMgr.GetCreatureTemplates();
            foreach (var creatureTemplate in templates.Values)
            {
                foreach (var (_, creatureDifficulty) in creatureTemplate.difficultyStorage)
                {
                    if (creatureDifficulty.LootID != 0)
                    {
                        if (!lootIdSet.Contains(creatureDifficulty.LootID))
                            Creature.ReportNonExistingId(creatureDifficulty.LootID, creatureTemplate.Entry);
                        else
                            lootIdSetUsed.Add(creatureDifficulty.LootID);
                    }
                }
            }

            foreach (var id in lootIdSetUsed)
                lootIdSet.Remove(id);

            // 1 means loot for player corpse
            lootIdSet.Remove(SharedConst.PlayerCorpseLootEntry);

            // output error for any still listed (not referenced from appropriate table) ids
            Creature.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 creature loot templates. DB table `creature_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Disenchant()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading disenchanting loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet, lootIdSetUsed = new();
            int count = Disenchant.LoadAndCollectLootIds(out lootIdSet);

            foreach (var disenchant in CliDB.ItemDisenchantLootStorage.Values)
            {
                int lootid = disenchant.Id;
                if (!lootIdSet.Contains(lootid))
                    Disenchant.ReportNonExistingId(lootid, disenchant.Id);
                else
                    lootIdSetUsed.Add(lootid);
            }

            foreach (var id in lootIdSetUsed)
                lootIdSet.Remove(id);

            // output error for any still listed (not referenced from appropriate table) ids
            Disenchant.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} disenchanting loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 disenchanting loot templates. DB table `disenchant_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Fishing()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading fishing loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet;
            int count = Fishing.LoadAndCollectLootIds(out lootIdSet);

            // remove real entries and check existence loot
            foreach (var areaEntry in CliDB.AreaTableStorage.Values)
                if (lootIdSet.Contains(areaEntry.Id))
                    lootIdSet.Remove(areaEntry.Id);

            // output error for any still listed (not referenced from appropriate table) ids
            Fishing.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} fishing loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 fishing loot templates. DB table `fishing_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Gameobject()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading gameobject loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet, lootIdSetUsed = new();
            int count = Gameobject.LoadAndCollectLootIds(out lootIdSet);

            void checkLootId(int lootId, int gameObjectId)
            {
                if (!lootIdSet.Contains(lootId))
                    Gameobject.ReportNonExistingId(lootId, gameObjectId);
                else
                    lootIdSetUsed.Add(lootId);
            }

            // remove real entries and check existence loot
            var gotc = Global.ObjectMgr.GetGameObjectTemplates();
            foreach (var (gameObjectId, gameObjectTemplate) in gotc)
            {
                int lootid = gameObjectTemplate.GetLootId();
                if (lootid != 0)
                    checkLootId(lootid, gameObjectId);

                if (gameObjectTemplate.type == GameObjectTypes.Chest)
                {
                    if (gameObjectTemplate.Chest.chestPersonalLoot != 0)
                        checkLootId(gameObjectTemplate.Chest.chestPersonalLoot, gameObjectId);

                    if (gameObjectTemplate.Chest.chestPushLoot != 0)
                        checkLootId(gameObjectTemplate.Chest.chestPushLoot, gameObjectId);
                }
            }

            foreach (var id in lootIdSetUsed)
                lootIdSet.Remove(id);

            // output error for any still listed (not referenced from appropriate table) ids
            Gameobject.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} gameobject loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 gameobject loot templates. DB table `gameobject_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Item()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading item loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet;
            int count = Items.LoadAndCollectLootIds(out lootIdSet);

            // remove real entries and check existence loot
            var its = Global.ObjectMgr.GetItemTemplates();
            foreach (var pair in its)
                if (lootIdSet.Contains(pair.Value.GetId()) && pair.Value.HasFlag(ItemFlags.HasLoot))
                    lootIdSet.Remove(pair.Value.GetId());

            // output error for any still listed (not referenced from appropriate table) ids
            Items.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} item loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 item loot templates. DB table `item_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Milling()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading milling loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet;
            int count = Milling.LoadAndCollectLootIds(out lootIdSet);

            // remove real entries and check existence loot
            var its = Global.ObjectMgr.GetItemTemplates();
            foreach (var pair in its)
            {
                if (!pair.Value.HasFlag(ItemFlags.IsMillable))
                    continue;

                if (lootIdSet.Contains(pair.Value.GetId()))
                    lootIdSet.Remove(pair.Value.GetId());
            }

            // output error for any still listed (not referenced from appropriate table) ids
            Milling.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} milling loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 milling loot templates. DB table `milling_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Pickpocketing()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading pickpocketing loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet;
            List<int> lootIdSetUsed = new();
            int count = Pickpocketing.LoadAndCollectLootIds(out lootIdSet);

            // Remove real entries and check loot existence
            var templates = Global.ObjectMgr.GetCreatureTemplates();
            foreach (var creatureTemplate in templates.Values)
            {
                foreach (var (_, creatureDifficulty) in creatureTemplate.difficultyStorage)
                {
                    if (creatureDifficulty.PickPocketLootID != 0)
                    {
                        if (!lootIdSet.Contains(creatureDifficulty.PickPocketLootID))
                            Pickpocketing.ReportNonExistingId(creatureDifficulty.PickPocketLootID, creatureTemplate.Entry);
                        else
                            lootIdSetUsed.Add(creatureDifficulty.PickPocketLootID);
                    }
                }
            }

            foreach (var id in lootIdSetUsed)
                lootIdSet.Remove(id);

            // output error for any still listed (not referenced from appropriate table) ids
            Pickpocketing.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} pickpocketing loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 pickpocketing loot templates. DB table `pickpocketing_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Prospecting()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading prospecting loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet;
            int count = Prospecting.LoadAndCollectLootIds(out lootIdSet);

            // remove real entries and check existence loot
            var its = Global.ObjectMgr.GetItemTemplates();
            foreach (var pair in its)
            {
                if (!pair.Value.HasFlag(ItemFlags.IsProspectable))
                    continue;

                if (lootIdSet.Contains(pair.Value.GetId()))
                    lootIdSet.Remove(pair.Value.GetId());
            }

            // output error for any still listed (not referenced from appropriate table) ids
            Prospecting.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} prospecting loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 prospecting loot templates. DB table `prospecting_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Mail()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading mail loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet;
            int count = Mail.LoadAndCollectLootIds(out lootIdSet);

            // remove real entries and check existence loot
            foreach (var mail in CliDB.MailTemplateStorage.Values)
                if (lootIdSet.Contains(mail.Id))
                    lootIdSet.Remove(mail.Id);

            // output error for any still listed (not referenced from appropriate table) ids
            Mail.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} mail loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 mail loot templates. DB table `mail_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Skinning()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading skinning loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet;
            List<int> lootIdSetUsed = new();
            int count = Skinning.LoadAndCollectLootIds(out lootIdSet);

            // remove real entries and check existence loot
            var templates = Global.ObjectMgr.GetCreatureTemplates();
            foreach (var creatureTemplate in templates.Values)
            {
                foreach (var (_, creatureDifficulty) in creatureTemplate.difficultyStorage)
                {
                    if (creatureDifficulty.SkinLootID != 0)
                    {
                        if (!lootIdSet.Contains(creatureDifficulty.SkinLootID))
                            Skinning.ReportNonExistingId(creatureDifficulty.SkinLootID, creatureTemplate.Entry);
                        else
                            lootIdSetUsed.Add(creatureDifficulty.SkinLootID);
                    }
                }
            }

            foreach (var id in lootIdSetUsed)
                lootIdSet.Remove(id);

            // output error for any still listed (not referenced from appropriate table) ids
            Skinning.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} skinning loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 skinning loot templates. DB table `skinning_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Spell()
        {
            // TODO: change this to use MiscValue from spell effect as id instead of spell id
            Log.outInfo(LogFilter.ServerLoading, "Loading spell loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet;
            int count = Spell.LoadAndCollectLootIds(out lootIdSet);

            // remove real entries and check existence loot
            foreach (SpellNameRecord spellNameEntry in CliDB.SpellNameStorage.Values)
            {
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellNameEntry.Id, Difficulty.None);
                if (spellInfo == null)
                    continue;

                // possible cases
                if (!spellInfo.IsLootCrafting())
                    continue;

                if (!lootIdSet.Contains(spellInfo.Id))
                {
                    // not report about not trainable spells (optionally supported by DB)
                    // ignore 61756 (Northrend Inscription Research (FAST QA VERSION) for example
                    if (!spellInfo.HasAttribute(SpellAttr0.NotShapeshifted) || spellInfo.HasAttribute(SpellAttr0.IsTradeskill))
                    {
                        Spell.ReportNonExistingId(spellInfo.Id, spellInfo.Id);
                    }
                }
                else
                    lootIdSet.Remove(spellInfo.Id);
            }

            // output error for any still listed (not referenced from appropriate table) ids
            Spell.ReportUnusedIds(lootIdSet);

            if (count != 0)
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} spell loot templates in {Time.Diff(oldMSTime)} ms.");
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 spell loot templates. DB table `spell_loot_template` is empty");
            }
        }

        public static void LoadLootTemplates_Reference()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading reference loot templates...");

            RelativeTime oldMSTime = Time.NowRelative;

            List<int> lootIdSet;
            Reference.LoadAndCollectLootIds(out lootIdSet);

            // check references and remove used
            Creature.CheckLootRefs(lootIdSet);
            Fishing.CheckLootRefs(lootIdSet);
            Gameobject.CheckLootRefs(lootIdSet);
            Items.CheckLootRefs(lootIdSet);
            Milling.CheckLootRefs(lootIdSet);
            Pickpocketing.CheckLootRefs(lootIdSet);
            Skinning.CheckLootRefs(lootIdSet);
            Disenchant.CheckLootRefs(lootIdSet);
            Prospecting.CheckLootRefs(lootIdSet);
            Mail.CheckLootRefs(lootIdSet);
            Reference.CheckLootRefs(lootIdSet);

            // output error for any still listed ids (not referenced from any loot table)
            Reference.ReportUnusedIds(lootIdSet);

            Log.outInfo(LogFilter.ServerLoading, $"Loaded reference loot templates in {Time.Diff(oldMSTime)} ms.");
        }
    }

    public class LootStoreItem
    {
        public int itemid;                 // id of the item
        public int reference;              // referenced TemplateleId
        public float chance;                // Chance to drop for both quest and non-quest items, Chance to be used for refs
        public LootModes lootmode;
        public bool needs_quest;            // quest drop (negative ChanceOrQuestChance in DB)
        public byte groupid;
        public byte mincount;               // mincount for drop items
        public byte maxcount;               // max drop count for the item mincount or Ref multiplicator
        public ConditionsReference conditions;  // additional loot condition

        public LootStoreItem(int _itemid, int _reference, float _chance, bool _needs_quest, LootModes _lootmode, byte _groupid, byte _mincount, byte _maxcount)
        {
            itemid = _itemid;
            reference = _reference;
            chance = _chance;
            lootmode = _lootmode;
            needs_quest = _needs_quest;
            groupid = _groupid;
            mincount = _mincount;
            maxcount = _maxcount;
        }

        public bool Roll(bool rate)
        {
            if (chance >= 100.0f)
                return true;

            if (reference > 0)                                   // reference case
                return RandomHelper.randChance(chance * (rate ? WorldConfig.Values[WorldCfg.RateDropItemReferenced].Float : 1.0f));

            ItemTemplate pProto = Global.ObjectMgr.GetItemTemplate(itemid);

            float qualityModifier = pProto != null && rate ? WorldConfig.Values[qualityToRate[(int)pProto.GetQuality()]].Float : 1.0f;

            return RandomHelper.randChance(chance * qualityModifier);
        }

        public bool IsValid(LootStore store, int entry)
        {
            if (mincount == 0)
            {
                Log.outError(LogFilter.Sql, 
                    $"Table '{store.GetName()}' entry {entry} item {itemid}: " +
                    $"wrong mincount ({reference}) - skipped");
                return false;
            }

            if (reference == 0)                                  // item (quest or non-quest) entry, maybe grouped
            {
                ItemTemplate proto = Global.ObjectMgr.GetItemTemplate(itemid);
                if (proto == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table '{store.GetName()}' entry {entry} item {itemid}: " +
                        $"item does not exist - skipped");
                    return false;
                }

                if (chance == 0 && groupid == 0)                      // Zero Chance is allowed for grouped entries only
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table '{store.GetName()}' entry {entry} item {itemid}: " +
                        $"equal-chanced grouped entry, but group not defined - skipped");
                    return false;
                }

                if (chance != 0 && chance < 0.000001f)             // loot with low Chance
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table '{store.GetName()}' entry {entry} item {itemid}: " +
                        $"low Chance ({chance}) - skipped");
                    return false;
                }

                if (maxcount < mincount)                       // wrong max count
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table '{store.GetName()}' entry {entry} item {itemid}: " +
                        $"max count ({maxcount}) less that min count ({reference}) - skipped");
                    return false;
                }
            }
            else                                                    // mincountOrRef < 0
            {
                if (needs_quest)
                {
                    Log.outError(LogFilter.Sql,
                        $"Table '{store.GetName()}' entry {entry} item {itemid}: " +
                        $"quest Chance will be treated as non-quest Chance");
                }
                else if (chance == 0)                              // no Chance for the reference
                {
                    Log.outError(LogFilter.Sql,
                        $"Table '{store.GetName()}' entry {entry} item {itemid}: " +
                        $"zero Chance is specified for a reference, skipped");
                    return false;
                }
            }
            return true;                                            // Referenced template existence is checked at whole store level
        }

        public static WorldCfg[] qualityToRate =
        [
            WorldCfg.RateDropItemPoor,                                    // ITEM_QUALITY_POOR
            WorldCfg.RateDropItemNormal,                                  // ITEM_QUALITY_NORMAL
            WorldCfg.RateDropItemUncommon,                                // ITEM_QUALITY_UNCOMMON
            WorldCfg.RateDropItemRare,                                    // ITEM_QUALITY_RARE
            WorldCfg.RateDropItemEpic,                                    // ITEM_QUALITY_EPIC
            WorldCfg.RateDropItemLegendary,                               // ITEM_QUALITY_LEGENDARY
            WorldCfg.RateDropItemArtifact,                                // ITEM_QUALITY_ARTIFACT
        ];
    }

    public class LootStore
    {
        public LootStore(string name, string entryName, bool ratesAllowed = true)
        {
            m_name = name;
            m_entryName = entryName;
            m_ratesAllowed = ratesAllowed;
        }

        void Verify()
        {
            foreach (var i in m_LootTemplates)
                i.Value.Verify(this, i.Key);
        }

        public int LoadAndCollectLootIds(out List<int> lootIdSet)
        {
            int count = LoadLootTable();
            lootIdSet = new List<int>();

            foreach (var tab in m_LootTemplates)
                lootIdSet.Add(tab.Key);

            return count;
        }

        public void CheckLootRefs(List<int> ref_set = null)
        {
            foreach (var pair in m_LootTemplates)
                pair.Value.CheckLootRefs(m_LootTemplates, ref_set);
        }

        public void ReportUnusedIds(List<int> lootIdSet)
        {
            // all still listed ids isn't referenced
            foreach (var id in lootIdSet)
            {
                Log.outError(LogFilter.Sql,
                    $"Table '{GetName()}' entry {id} isn't {GetEntryName()} " +
                    $"and not referenced from loot, and then useless.");
            }
        }

        public void ReportNonExistingId(int lootId, int ownerId)
        {
            Log.outError(LogFilter.Sql, 
                $"Table '{GetName()}' Entry {lootId} does not exist " +
                $"but it is used by {GetEntryName()} {ownerId}");
        }

        public bool HaveLootFor(int loot_id) { return m_LootTemplates.LookupByKey(loot_id) != null; }

        public bool HaveQuestLootFor(int loot_id)
        {
            var lootTemplate = m_LootTemplates.LookupByKey(loot_id);
            if (lootTemplate == null)
                return false;

            // scan loot for quest items
            return lootTemplate.HasQuestDrop(m_LootTemplates);
        }

        public bool HaveQuestLootForPlayer(int loot_id, Player player)
        {
            var tab = m_LootTemplates.LookupByKey(loot_id);
            if (tab != null)
            {
                if (tab.HasQuestDropForPlayer(m_LootTemplates, player))
                    return true;
            }

            return false;
        }

        public LootTemplate GetLootFor(int loot_id)
        {
            var tab = m_LootTemplates.LookupByKey(loot_id);

            if (tab == null)
                return null;

            return tab;
        }

        public LootTemplate GetLootForConditionFill(int loot_id)
        {
            var tab = m_LootTemplates.LookupByKey(loot_id);

            if (tab == null)
                return null;

            return tab;
        }

        public string GetName() { return m_name; }
        string GetEntryName() { return m_entryName; }
        public bool IsRatesAllowed() { return m_ratesAllowed; }

        int LoadLootTable()
        {
            // Clearing store (for reloading case)
            Clear();

            //                                            0     1      2        3         4             5          6        7         8
            SQLResult result = DB.World.Query($"SELECT Entry, Item, Reference, Chance, QuestRequired, LootMode, GroupId, MinCount, MaxCount FROM {GetName()}");
            if (result.IsEmpty())
                return 0;

            int count = 0;
            do
            {
                int entry = result.Read<int>(0);
                int item = result.Read<int>(1);
                int reference = result.Read<int>(2);
                float chance = result.Read<float>(3);
                bool needsquest = result.Read<bool>(4);
                var lootmode = (LootModes)result.Read<ushort>(5);
                byte groupid = result.Read<byte>(6);
                byte mincount = result.Read<byte>(7);
                byte maxcount = result.Read<byte>(8);

                if (groupid >= 1 << 7)                                     // it stored in 7 bit field
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table '{GetName()}' entry {entry} item {item}: " +
                        $"group ({groupid}) must be less {1 << 7} - skipped");
                    return 0;
                }

                LootStoreItem storeitem = new(item, reference, chance, needsquest, lootmode, groupid, mincount, maxcount);

                if (!storeitem.IsValid(this, entry))            // Validity checks
                    continue;

                // Looking for the template of the entry
                // often entries are put together
                if (m_LootTemplates.Empty() || !m_LootTemplates.ContainsKey(entry))
                    m_LootTemplates.Add(entry, new LootTemplate());

                // Adds current row to the template
                m_LootTemplates[entry].AddEntry(storeitem);
                ++count;
            }
            while (result.NextRow());

            Verify();                                           // Checks validity of the loot store

            return count;
        }

        void Clear()
        {
            m_LootTemplates.Clear();
        }

        LootTemplateMap m_LootTemplates = new();
        string m_name;
        string m_entryName;
        bool m_ratesAllowed;
    }

    public class LootTemplate
    {
        public void AddEntry(LootStoreItem item)
        {
            if (item.groupid > 0 && item.reference == 0)         // Group
            {
                if (!Groups.ContainsKey(item.groupid - 1))
                    Groups[item.groupid - 1] = new LootGroup();

                Groups[item.groupid - 1].AddEntry(item);              // Adds new entry to the group
            }
            else                                                    // Non-grouped entries and references are stored together
                Entries.Add(item);
        }

        public void Process(Loot loot, bool rate, LootModes lootMode, byte groupId, Player personalLooter = null)
        {
            if (groupId != 0)                                            // Group reference uses own processing of the group
            {
                if (groupId > Groups.Count)
                    return;                                         // Error message already printed at loading stage

                if (Groups[groupId - 1] == null)
                    return;

                Groups[groupId - 1].Process(loot, lootMode, personalLooter);
                return;
            }

            // Rolling non-grouped items
            foreach (var item in Entries)
            {
                if (!Convert.ToBoolean(item.lootmode & lootMode))                       // Do not add if mode mismatch
                    continue;

                if (!item.Roll(rate))
                    continue;                                           // Bad luck for the entry

                if (item.reference > 0)                            // References processing
                {
                    LootTemplate Referenced = LootStorage.Reference.GetLootFor(item.reference);
                    if (Referenced == null)
                        continue;                                       // Error message already printed at loading stage

                    uint maxcount = (uint)(item.maxcount * WorldConfig.Values[WorldCfg.RateDropItemReferencedAmount].Float);
                    for (uint loop = 0; loop < maxcount; ++loop)      // Ref multiplicator
                        Referenced.Process(loot, rate, lootMode, item.groupid, personalLooter);
                }
                else
                {
                    // Plain entries (not a reference, not grouped)
                    // Chance is already checked, just add
                    if (personalLooter == null
                        || LootItem.AllowedForPlayer(personalLooter, null, item.itemid, item.needs_quest,
                            !item.needs_quest || Global.ObjectMgr.GetItemTemplate(item.itemid).HasFlag(ItemFlagsCustom.FollowLootRules),
                            true, item.conditions))
                    {
                        loot.AddItem(item);
                    }
                }
            }

            // Now processing groups
            foreach (var group in Groups.Values)
            {
                if (group != null)
                    group.Process(loot, lootMode, personalLooter);
            }
        }

        public void ProcessPersonalLoot(Dictionary<Player, Loot> personalLoot, bool rate, LootModes lootMode)
        {
            List<Player> getLootersForItem(Func<Player, bool> predicate)
            {
                List<Player> lootersForItem = new();
                foreach (var (looter, loot) in personalLoot)
                {
                    if (predicate(looter))
                        lootersForItem.Add(looter);
                }
                return lootersForItem;
            }

            // Rolling non-grouped items
            foreach (LootStoreItem item in Entries)
            {
                if ((item.lootmode & lootMode) == 0)                       // Do not add if mode mismatch
                    continue;

                if (!item.Roll(rate))
                    continue;                                           // Bad luck for the entry

                if (item.reference > 0)                                // References processing
                {
                    LootTemplate referenced = LootStorage.Reference.GetLootFor(item.reference);
                    if (referenced == null)
                        continue;                                       // Error message already printed at loading stage

                    uint maxcount = (uint)(item.maxcount * WorldConfig.Values[WorldCfg.RateDropItemReferencedAmount].Float);
                    List<Player> gotLoot = new();
                    for (uint loop = 0; loop < maxcount; ++loop)      // Ref multiplicator
                    {
                        var lootersForItem = getLootersForItem(looter => referenced.HasDropForPlayer(looter, item.groupid, true));

                        // nobody can loot this, skip it
                        if (lootersForItem.Empty())
                            break;

                        var newEnd = lootersForItem.RemoveAll(looter => gotLoot.Contains(looter));

                        if (lootersForItem.Count == newEnd)
                        {
                            // if we run out of looters this means that there are more items dropped than players
                            // start a new cycle adding one item to everyone
                            gotLoot.Clear();
                        }
                        else
                            lootersForItem.RemoveRange(newEnd, lootersForItem.Count - newEnd);

                        Player chosenLooter = lootersForItem.SelectRandom();
                        referenced.Process(personalLoot[chosenLooter], rate, lootMode, item.groupid, chosenLooter);
                        gotLoot.Add(chosenLooter);
                    }
                }
                else
                {
                    // Plain entries (not a reference, not grouped)
                    // Chance is already checked, just add
                    var lootersForItem = getLootersForItem(looter =>
                    {
                        return LootItem.AllowedForPlayer(looter, null, item.itemid, item.needs_quest,
                            !item.needs_quest || Global.ObjectMgr.GetItemTemplate(item.itemid).HasFlag(ItemFlagsCustom.FollowLootRules),
                            true, item.conditions);
                    });

                    if (!lootersForItem.Empty())
                    {
                        Player chosenLooter = lootersForItem.SelectRandom();
                        personalLoot[chosenLooter].AddItem(item);
                    }
                }
            }

            // Now processing groups
            foreach (LootGroup group in Groups.Values)
            {
                if (group != null)
                {
                    var lootersForGroup = getLootersForItem(looter => group.HasDropForPlayer(looter, true));

                    if (!lootersForGroup.Empty())
                    {
                        Player chosenLooter = lootersForGroup.SelectRandom();
                        group.Process(personalLoot[chosenLooter], lootMode);
                    }
                }
            }
        }

        // True if template includes at least 1 drop for the player
        bool HasDropForPlayer(Player player, byte groupId, bool strictUsabilityCheck)
        {
            if (groupId != 0)                                            // Group reference
            {
                if (groupId > Groups.Count)
                    return false;                                   // Error message already printed at loading stage

                if (Groups[groupId - 1] == null)
                    return false;

                return Groups[groupId - 1].HasDropForPlayer(player, strictUsabilityCheck);
            }

            // Checking non-grouped entries
            foreach (LootStoreItem lootStoreItem in Entries)
            {
                if (lootStoreItem.reference > 0)                   // References processing
                {
                    LootTemplate referenced = LootStorage.Reference.GetLootFor(lootStoreItem.reference);
                    if (referenced == null)
                        continue;                                   // Error message already printed at loading stage
                    if (referenced.HasDropForPlayer(player, lootStoreItem.groupid, strictUsabilityCheck))
                        return true;
                }
                else if (LootItem.AllowedForPlayer(player, null, lootStoreItem.itemid, lootStoreItem.needs_quest,
                    !lootStoreItem.needs_quest || Global.ObjectMgr.GetItemTemplate(lootStoreItem.itemid).HasFlag(ItemFlagsCustom.FollowLootRules),
                    strictUsabilityCheck, lootStoreItem.conditions))
                {
                    return true;                                    // active quest drop found
                }
            }

            // Now checking groups
            foreach (LootGroup group in Groups.Values)
            {
                if (group != null && group.HasDropForPlayer(player, strictUsabilityCheck))
                    return true;
            }

            return false;
        }

        public void CopyConditions(LootItem li)
        {
            // Copies the conditions list from a template item to a LootItem
            foreach (var item in Entries)
            {
                if (item.itemid != li.itemid)
                    continue;

                li.conditions = item.conditions;
                break;
            }
        }

        public bool HasQuestDrop(LootTemplateMap store, byte groupId = 0)
        {
            if (groupId != 0)                                            // Group reference
            {
                if (groupId > Groups.Count)
                    return false;                                   // Error message [should be] already printed at loading stage

                if (Groups[groupId - 1] == null)
                    return false;

                return Groups[groupId - 1].HasQuestDrop();
            }

            foreach (var item in Entries)
            {
                if (item.reference > 0)                        // References
                {
                    var Referenced = store.LookupByKey(item.reference);
                    if (Referenced == null)
                        continue;                                   // Error message [should be] already printed at loading stage
                    if (Referenced.HasQuestDrop(store, item.groupid))
                        return true;
                }
                else if (item.needs_quest)
                    return true;                                    // quest drop found
            }

            // Now processing groups
            foreach (var group in Groups.Values)
            {
                if (group.HasQuestDrop())
                    return true;
            }

            return false;
        }

        public bool HasQuestDropForPlayer(LootTemplateMap store, Player player, byte groupId = 0)
        {
            if (groupId != 0)                                            // Group reference
            {
                if (groupId > Groups.Count)
                    return false;                                   // Error message already printed at loading stage

                if (Groups[groupId - 1] == null)
                    return false;

                return Groups[groupId - 1].HasQuestDropForPlayer(player);
            }

            // Checking non-grouped entries
            foreach (var item in Entries)
            {
                if (item.reference > 0)                        // References processing
                {
                    var Referenced = store.LookupByKey(item.reference);
                    if (Referenced == null)
                        continue;                                   // Error message already printed at loading stage
                    if (Referenced.HasQuestDropForPlayer(store, player, item.groupid))
                        return true;
                }
                else if (player.HasQuestForItem(item.itemid))
                    return true;                                    // active quest drop found
            }

            // Now checking groups
            foreach (var group in Groups.Values)
            {
                if (group.HasQuestDropForPlayer(player))
                    return true;
            }

            return false;
        }

        public void Verify(LootStore lootstore, int id)
        {
            // Checking group chances
            foreach (var group in Groups)
                group.Value.Verify(lootstore, id, (byte)(group.Key + 1));

            // @todo References validity checks
        }

        public void CheckLootRefs(LootTemplateMap store, List<int> ref_set)
        {
            foreach (var item in Entries)
            {
                if (item.reference > 0)
                {
                    if (LootStorage.Reference.GetLootFor(item.reference) == null)
                        LootStorage.Reference.ReportNonExistingId(item.reference, item.itemid);
                    else if (ref_set != null)
                        ref_set.Remove(item.reference);
                }
            }

            foreach (var group in Groups.Values)
                group.CheckLootRefs(store, ref_set);
        }

        public bool LinkConditions(ConditionId id, ConditionsReference reference)
        {
            if (!Entries.Empty())
            {
                foreach (var item in Entries)
                {
                    if (item.itemid == id.SourceEntry)
                    {
                        item.conditions = reference;
                        return true;
                    }
                }
            }

            if (!Groups.Empty())
            {
                foreach (var (_, group) in Groups)
                {
                    if (group == null)
                        continue;

                    LootStoreItemList itemList = group.GetExplicitlyChancedItemList();
                    if (!itemList.Empty())
                    {
                        foreach (var item in itemList)
                        {
                            if (item.itemid == id.SourceEntry)
                            {
                                item.conditions = reference;
                                return true;
                            }
                        }
                    }

                    itemList = group.GetEqualChancedItemList();
                    if (!itemList.Empty())
                    {
                        foreach (var item in itemList)
                        {
                            if (item.itemid == id.SourceEntry)
                            {
                                item.conditions = reference;
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public bool IsReference(int id)
        {
            foreach (var storeItem in Entries)
            {
                if (storeItem.itemid == id && storeItem.reference > 0)
                    return true;
            }

            return false;//not found or not reference
        }

        LootStoreItemList Entries = new();                          // not grouped only
        Dictionary<int, LootGroup> Groups = new();                           // groups have own (optimised) processing, grouped entries go there

        public class LootGroup                               // A set of loot definitions for items (refs are not allowed)
        {
            public void AddEntry(LootStoreItem item)
            {
                if (item.chance != 0)
                    ExplicitlyChanced.Add(item);
                else
                    EqualChanced.Add(item);
            }

            public bool HasQuestDrop()
            {
                foreach (var i in ExplicitlyChanced)
                {
                    if (i.needs_quest)
                        return true;
                }

                foreach (var i in EqualChanced)
                {
                    if (i.needs_quest)
                        return true;
                }

                return false;
            }

            public bool HasQuestDropForPlayer(Player player)
            {
                foreach (var i in ExplicitlyChanced)
                {
                    if (player.HasQuestForItem(i.itemid))
                        return true;
                }

                foreach (var i in EqualChanced)
                {
                    if (player.HasQuestForItem(i.itemid))
                        return true;
                }

                return false;
            }

            public void Process(Loot loot, LootModes lootMode, Player personalLooter = null)
            {
                LootStoreItem item = Roll(lootMode, personalLooter);
                if (item != null)
                    loot.AddItem(item);
            }

            float RawTotalChance()
            {
                float result = 0;

                foreach (var i in ExplicitlyChanced)
                {
                    if (!i.needs_quest)
                        result += i.chance;
                }
               
                return result;
            }

            float TotalChance()
            {
                float result = RawTotalChance();

                if (!EqualChanced.Empty() && result < 100.0f)
                    return 100.0f;

                return result;
            }

            public void Verify(LootStore lootstore, int id, byte group_id = 0)
            {
                float chance = RawTotalChance();
                if (chance > 101.0f)                                    // @todo replace with 100% when DBs will be ready
                {
                    Log.outError(LogFilter.Sql,
                        $"Table '{lootstore.GetName()}' entry {id} group {group_id} " +
                        $"has total Chance > 100% ({chance})");
                }

                if (chance >= 100.0f && !EqualChanced.Empty())
                {
                    Log.outError(LogFilter.Sql,
                        $"Table '{lootstore.GetName()}' entry {id} group {group_id} " +
                        $"has items with Chance=0% but group total Chance >= 100% ({chance})");
                }

            }

            public void CheckLootRefs(LootTemplateMap store, List<int> ref_set)
            {
                foreach (var item in ExplicitlyChanced)
                {
                    if (item.reference > 0)
                    {
                        if (LootStorage.Reference.GetLootFor(item.reference) == null)
                            LootStorage.Reference.ReportNonExistingId(item.reference, item.itemid);
                        else if (ref_set != null)
                            ref_set.Remove(item.reference);
                    }
                }

                foreach (var item in EqualChanced)
                {
                    if (item.reference > 0)
                    {
                        if (LootStorage.Reference.GetLootFor(item.reference) == null)
                            LootStorage.Reference.ReportNonExistingId(item.reference, item.itemid);
                        else if (ref_set != null)
                            ref_set.Remove(item.reference);
                    }
                }
            }

            public LootStoreItemList GetExplicitlyChancedItemList() { return ExplicitlyChanced; }
            public LootStoreItemList GetEqualChancedItemList() { return EqualChanced; }

            LootStoreItemList ExplicitlyChanced = new();                // Entries with chances defined in DB
            LootStoreItemList EqualChanced = new();                     // Zero chances - every entry takes the same Chance

            LootStoreItem Roll(LootModes lootMode, Player personalLooter = null)
            {
                LootStoreItemList possibleLoot = ExplicitlyChanced;
                possibleLoot.RemoveAll(new LootGroupInvalidSelector(lootMode, personalLooter).Check);

                if (!possibleLoot.Empty())                             // First explicitly chanced entries are checked
                {
                    float roll = (float)RandomHelper.randPercent();

                    foreach (var item in possibleLoot)   // check each explicitly chanced entry in the template and modify its Chance based on quality.
                    {
                        if (item.chance >= 100.0f)
                            return item;

                        roll -= item.chance;
                        if (roll < 0)
                            return item;
                    }
                }

                possibleLoot = EqualChanced;
                possibleLoot.RemoveAll(new LootGroupInvalidSelector(lootMode, personalLooter).Check);
                if (!possibleLoot.Empty())                              // If nothing selected yet - an item is taken from equal-chanced part
                    return possibleLoot.SelectRandom();

                return null;                                            // Empty drop from the group
            }

            public bool HasDropForPlayer(Player player, bool strictUsabilityCheck)
            {
                foreach (LootStoreItem lootStoreItem in ExplicitlyChanced)
                {
                    if (LootItem.AllowedForPlayer(player, null, lootStoreItem.itemid, lootStoreItem.needs_quest,
                        !lootStoreItem.needs_quest || Global.ObjectMgr.GetItemTemplate(lootStoreItem.itemid).HasFlag(ItemFlagsCustom.FollowLootRules),
                        strictUsabilityCheck, lootStoreItem.conditions))
                    {
                        return true;
                    }
                }

                foreach (LootStoreItem lootStoreItem in EqualChanced)
                {
                    if (LootItem.AllowedForPlayer(player, null, lootStoreItem.itemid, lootStoreItem.needs_quest,
                        !lootStoreItem.needs_quest || Global.ObjectMgr.GetItemTemplate(lootStoreItem.itemid).HasFlag(ItemFlagsCustom.FollowLootRules),
                        strictUsabilityCheck, lootStoreItem.conditions))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    public struct LootGroupInvalidSelector
    {
        public LootGroupInvalidSelector(LootModes lootMode, Player personalLooter)
        {
            _lootMode = lootMode;
            _personalLooter = personalLooter;
        }

        public bool Check(LootStoreItem item)
        {
            if ((item.lootmode & _lootMode) == 0)
                return true;

            if (_personalLooter != null && !LootItem.AllowedForPlayer(_personalLooter, null, item.itemid, item.needs_quest,
                !item.needs_quest || Global.ObjectMgr.GetItemTemplate(item.itemid).HasFlag(ItemFlagsCustom.FollowLootRules),
                true, item.conditions))
            {
                return true;
            }

            return false;
        }

        LootModes _lootMode;
        Player _personalLooter;
    }
}
