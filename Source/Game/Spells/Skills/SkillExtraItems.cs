// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Database;
using Game.Entities;
using System.Collections.Generic;

namespace Game.Spells
{
    public class SkillExtraItems
    {
        // loads the extra item creation info from DB
        public static void LoadSkillExtraItemTable()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            SkillExtraItemStorage.Clear();                            // need for reload

            //                                             0               1                       2                    3
            SQLResult result = DB.World.Query("SELECT spellId, requiredSpecialization, additionalCreateChance, additionalMaxNum FROM skill_extra_item_template");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 spell specialization definitions. " +
                    "DB table `skill_extra_item_template` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int spellId = result.Read<int>(0);

                if (!Global.SpellMgr.HasSpellInfo(spellId, Framework.Constants.Difficulty.None))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Skill specialization {spellId} " +
                        $"has non-existent spell id " +
                        $"in `skill_extra_item_template`!");
                    continue;
                }

                int requiredSpecialization = result.Read<int>(1);
                if (!Global.SpellMgr.HasSpellInfo(requiredSpecialization, Framework.Constants.Difficulty.None))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Skill specialization {spellId} " +
                        $"have not existed required specialization spell id {requiredSpecialization} " +
                        $"in `skill_extra_item_template`!");
                    continue;
                }

                float additionalCreateChance = result.Read<float>(2);
                if (additionalCreateChance <= 0.0f)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Skill specialization {spellId} " +
                        $"has too low additional create Chance " +
                        $"in `skill_extra_item_template`!");
                    continue;
                }

                byte additionalMaxNum = result.Read<byte>(3);
                if (additionalMaxNum == 0)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Skill specialization {spellId} " +
                        $"has 0 max number of extra items " +
                        $"in `skill_extra_item_template`!");
                    continue;
                }

                SkillExtraItemEntry skillExtraItemEntry = new();
                skillExtraItemEntry.requiredSpecialization = requiredSpecialization;
                skillExtraItemEntry.additionalCreateChance = additionalCreateChance;
                skillExtraItemEntry.additionalMaxNum = additionalMaxNum;

                SkillExtraItemStorage[spellId] = skillExtraItemEntry;
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} spell specialization definitions in {Time.Diff(oldMSTime)} ms.");
        }

        public static bool CanCreateExtraItems(Player player, int spellId, ref float additionalChance, ref byte additionalMax)
        {
            // get the info for the specified spell
            var specEntry = SkillExtraItemStorage.LookupByKey(spellId);
            if (specEntry == null)
                return false;

            // the player doesn't have the required specialization, return false
            if (!player.HasSpell(specEntry.requiredSpecialization))
                return false;

            // set the arguments to the appropriate values
            additionalChance = specEntry.additionalCreateChance;
            additionalMax = specEntry.additionalMaxNum;

            // enable extra item creation
            return true;
        }

        static Dictionary<int, SkillExtraItemEntry> SkillExtraItemStorage = new();
    }

    class SkillExtraItemEntry
    {
        public SkillExtraItemEntry(int rS = 0, float aCC = 0f, byte aMN = 0)
        {
            requiredSpecialization = rS;
            additionalCreateChance = aCC;
            additionalMaxNum = aMN;
        }

        // the spell id of the specialization required to create extra items
        public int requiredSpecialization;
        // the Chance to create one additional item
        public float additionalCreateChance;
        // maximum number of extra items created per crafting
        public byte additionalMaxNum;
    }

    public class SkillPerfectItems
    {
        // loads the perfection proc info from DB
        public static void LoadSkillPerfectItemTable()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            SkillPerfectItemStorage.Clear(); // reload capability

            //                                           0               1                      2                  3
            SQLResult result = DB.World.Query("SELECT spellId, requiredSpecialization, perfectCreateChance, perfectItemType FROM skill_perfect_item_template");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 spell perfection definitions. " +
                    "DB table `skill_perfect_item_template` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int spellId = result.Read<int>(0);
                if (!Global.SpellMgr.HasSpellInfo(spellId, Framework.Constants.Difficulty.None))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Skill perfection data for spell {spellId} " +
                        $"has non-existent spell id " +
                        $"in `skill_perfect_item_template`!");
                    continue;
                }

                int requiredSpecialization = result.Read<int>(1);
                if (!Global.SpellMgr.HasSpellInfo(requiredSpecialization, Framework.Constants.Difficulty.None))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Skill perfection data for spell {spellId} " +
                        $"has non-existent required specialization spell id {requiredSpecialization} " +
                        $"in `skill_perfect_item_template`!");
                    continue;
                }

                float perfectCreateChance = result.Read<float>(2);
                if (perfectCreateChance <= 0.0f)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Skill perfection data for spell {spellId} " +
                        $"has impossibly low proc Chance " +
                        $"in `skill_perfect_item_template`!");
                    continue;
                }

                int perfectItemType = result.Read<int>(3);
                if (Global.ObjectMgr.GetItemTemplate(perfectItemType) == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Skill perfection data for spell {spellId} " +
                        $"references non-existent perfect item id {perfectItemType} " +
                        $"in `skill_perfect_item_template`!");
                    continue;
                }

                SkillPerfectItemStorage[spellId] = new SkillPerfectItemEntry(requiredSpecialization, perfectCreateChance, perfectItemType);

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {count} spell perfection definitions in {Time.Diff(oldMSTime)} ms.");
        }

        public static bool CanCreatePerfectItem(Player player, int spellId, ref float perfectCreateChance, ref int perfectItemType)
        {
            var entry = SkillPerfectItemStorage.LookupByKey(spellId);
            // no entry in DB means no perfection proc possible
            if (entry == null)
                return false;

            // if you don't have the spell needed, then no procs for you
            if (!player.HasSpell(entry.requiredSpecialization))
                return false;

            // set values as appropriate
            perfectCreateChance = entry.perfectCreateChance;
            perfectItemType = entry.perfectItemType;

            // and tell the caller to start rolling the dice
            return true;
        }

        static Dictionary<int, SkillPerfectItemEntry> SkillPerfectItemStorage = new();
    }

    // struct to store information about perfection procs
    // one entry per spell
    class SkillPerfectItemEntry
    {
        public SkillPerfectItemEntry(int rS = 0, float pCC = 0f, int pIT = 0)
        {
            requiredSpecialization = rS;
            perfectCreateChance = pCC;
            perfectItemType = pIT;
        }

        // the spell id of the spell required - it's named "specialization" to conform with SkillExtraItemEntry
        public int requiredSpecialization;
        // perfection proc Chance
        public float perfectCreateChance;
        // itemid of the resulting perfect item
        public int perfectItemType;
    }
}
