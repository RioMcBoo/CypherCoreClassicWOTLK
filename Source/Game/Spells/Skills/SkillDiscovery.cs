// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Spells
{
    public class SkillDiscovery
    {
        public static void LoadSkillDiscoveryTable()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            SkillDiscoveryStorage.Clear();                            // need for reload

            //                                         0        1         2              3
            SQLResult result = DB.World.Query("SELECT spellId, reqSpell, reqSkillValue, Chance FROM skill_discovery_template");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 skill discovery definitions. " +
                    "DB table `skill_discovery_template` is empty.");
                return;
            }

            uint count = 0;

            StringBuilder ssNonDiscoverableEntries = new();
            List<int> reportedReqSpells = new();

            do
            {
                var spellId = result.Read<int>(0);
                var reqSkillOrSpell = result.Read<int>(1);
                int reqSkillValue = result.Read<int>(2);
                float chance = result.Read<float>(3);

                if (chance <= 0)                                    // Chance
                {
                    ssNonDiscoverableEntries.AppendFormat(
                        $"spellId = {spellId} " +
                        $"reqSkillOrSpell = {reqSkillOrSpell} " +
                        $"reqSkillValue = {reqSkillValue} " +
                        $"Chance = {chance} (Chance problem)\n");
                    continue;
                }

                if (reqSkillOrSpell > 0)                            // spell case
                {
                    var absReqSkillOrSpell = reqSkillOrSpell;
                    SpellInfo reqSpellInfo = Global.SpellMgr.GetSpellInfo(absReqSkillOrSpell, Difficulty.None);
                    if (reqSpellInfo == null)
                    {
                        if (!reportedReqSpells.Contains(absReqSkillOrSpell))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Spell (ID: {spellId}) have not existed spell (ID: {reqSkillOrSpell}) " +
                                $"in `reqSpell` field in `skill_discovery_template` table");
                            reportedReqSpells.Add(absReqSkillOrSpell);
                        }
                        continue;
                    }

                    // mechanic discovery
                    if (reqSpellInfo.Mechanic != Mechanics.Discovery &&
                        // explicit discovery ability
                        !reqSpellInfo.IsExplicitDiscovery())
                    {
                        if (!reportedReqSpells.Contains(absReqSkillOrSpell))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Spell (ID: {absReqSkillOrSpell}) not have MECHANIC_DISCOVERY (28) value in Mechanic field in spell.dbc" +
                                $" and not 100%% Chance random discovery ability but listed for spellId {spellId} (and maybe more) " +
                                $"in `skill_discovery_template` table");

                            reportedReqSpells.Add(absReqSkillOrSpell);
                        }
                        continue;
                    }

                    SkillDiscoveryStorage.Add(reqSkillOrSpell, new SkillDiscoveryEntry(spellId, reqSkillValue, chance));
                }
                else if (reqSkillOrSpell == 0)                      // skill case
                {
                    var bounds = Global.SpellMgr.GetSkillLineAbilityMapBounds(spellId);

                    if (bounds.Empty())
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spell (ID: {spellId}) not listed in `SkillLineAbility.dbc` but listed with `reqSpell`=0 " +
                            $"in `skill_discovery_template` table");
                        continue;
                    }

                    foreach (var _spell_idx in bounds)
                        SkillDiscoveryStorage.Add(-(int)_spell_idx.SkillLine, new SkillDiscoveryEntry(spellId, reqSkillValue, chance));
                }
                else
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell (ID: {spellId}) have negative value in `reqSpell` field " +
                        $"in `skill_discovery_template` table");
                    continue;
                }

                ++count;
            }
            while (result.NextRow());

            if (ssNonDiscoverableEntries.Length != 0)
            {
                Log.outError(LogFilter.Sql, 
                    $"Some items can't be successfully discovered: " +
                    $"have in Chance field value < 0.000001 in `skill_discovery_template` DB table . " +
                    $"List:\n{ssNonDiscoverableEntries}");
            }

            // report about empty data for explicit discovery spells
            foreach (SpellNameRecord spellNameEntry in CliDB.SpellNameStorage.Values)
            {
                SpellInfo spellEntry = Global.SpellMgr.GetSpellInfo(spellNameEntry.Id, Difficulty.None);
                if (spellEntry == null)
                    continue;

                // skip not explicit discovery spells
                if (!spellEntry.IsExplicitDiscovery())
                    continue;

                if (!SkillDiscoveryStorage.ContainsKey(spellEntry.Id))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell (ID: {spellEntry.Id}) is 100% Chance random discovery ability " +
                        $"but not have data in `skill_discovery_template` table");
                }
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} skill discovery definitions in {Time.Diff(oldMSTime)} ms.");
        }

        public static int GetExplicitDiscoverySpell(int spellId, Player player)
        {
            // explicit discovery spell chances (always success if case exist)
            // in this case we have both skill and spell
            var tab = SkillDiscoveryStorage.LookupByKey(spellId);
            if (tab.Empty())
                return 0;

            var bounds = Global.SpellMgr.GetSkillLineAbilityMapBounds(spellId);
            int skillvalue = !bounds.Empty() ? player.GetSkillValue(bounds.FirstOrDefault().SkillLine) : 0;

            float full_chance = 0;
            foreach (var item_iter in tab)
                if (item_iter.reqSkillValue <= skillvalue)
                    if (!player.HasSpell(item_iter.spellId))
                        full_chance += item_iter.chance;

            float rate = full_chance / 100.0f;
            float roll = (float)RandomHelper.randPercent() * rate;                      // roll now in range 0..full_chance

            foreach (var item_iter in tab)
            {
                if (item_iter.reqSkillValue > skillvalue)
                    continue;

                if (player.HasSpell(item_iter.spellId))
                    continue;

                if (item_iter.chance > roll)
                    return item_iter.spellId;

                roll -= item_iter.chance;
            }

            return 0;
        }

        public static bool HasDiscoveredAllSpells(int spellId, Player player)
        {
            var tab = SkillDiscoveryStorage.LookupByKey(spellId);
            if (tab.Empty())
                return true;

            foreach (var item_iter in tab)
            {
                if (!player.HasSpell(item_iter.spellId))
                    return false;
            }

            return true;
        }

        public static bool HasDiscoveredAnySpell(int spellId, Player player)
        {
            var tab = SkillDiscoveryStorage.LookupByKey(spellId);
            if (tab.Empty())
                return false;

            foreach (var item_iter in tab)
            {
                if (player.HasSpell(item_iter.spellId))
                    return true;
            }

            return false;
        }

        public static int GetSkillDiscoverySpell(SkillType skillId, int spellId, Player player)
        {
            return GetSkillDiscoverySpell((int)skillId, spellId, player);
        }

        public static int GetSkillDiscoverySpell(int skillId, int spellId, Player player)
        {
            int skillvalue = skillId != 0 ? player.GetSkillValue(skillId) : 0;

            // check spell case
            var tab = SkillDiscoveryStorage.LookupByKey(spellId);

            if (!tab.Empty())
            {
                foreach (var item_iter in tab)
                {
                    if (RandomHelper.randChance(item_iter.chance * WorldConfig.Values[WorldCfg.RateSkillDiscovery].Float) &&
                        item_iter.reqSkillValue <= skillvalue &&
                        !player.HasSpell(item_iter.spellId))
                        return item_iter.spellId;
                }

                return 0;
            }

            if (skillId == 0)
                return 0;

            // check skill line case
            tab = SkillDiscoveryStorage.LookupByKey(-skillId);
            if (!tab.Empty())
            {
                foreach (var item_iter in tab)
                {
                    if (RandomHelper.randChance(item_iter.chance * WorldConfig.Values[WorldCfg.RateSkillDiscovery].Float) &&
                        item_iter.reqSkillValue <= skillvalue &&
                        !player.HasSpell(item_iter.spellId))
                        return item_iter.spellId;
                }

                return 0;
            }

            return 0;
        }

        static MultiMap<int, SkillDiscoveryEntry> SkillDiscoveryStorage = new();
    }

    public class SkillDiscoveryEntry
    {
        public SkillDiscoveryEntry(int _spellId = 0, int req_skill_val = 0, float _chance = 0)
        {
            spellId = _spellId;
            reqSkillValue = req_skill_val;
            chance = _chance;
        }

        public int spellId;                                        // discavered spell
        public int reqSkillValue;                                  // skill level limitation
        public float chance;                                         // Chance
    }
}
