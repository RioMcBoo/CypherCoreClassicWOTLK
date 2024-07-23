// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Text;

namespace Game
{
    class CharacterDatabaseCleaner
    {
        public static void CleanDatabase()
        {
            // config to disable
            if (!WorldConfig.Values[WorldCfg.CleanCharacterDb].Bool)
                return;

            Log.outInfo(LogFilter.Server, "Cleaning character database...");

            uint oldMSTime = Time.GetMSTime();

            CleaningFlags flags = (CleaningFlags)Global.WorldMgr.GetPersistentWorldVariable(WorldManager.CharacterDatabaseCleaningFlagsVarId);

            // clean up
            if (flags.HasAnyFlag(CleaningFlags.AchievementProgress))
                CleanCharacterAchievementProgress();

            if (flags.HasAnyFlag(CleaningFlags.Skills))
                CleanCharacterSkills();

            if (flags.HasAnyFlag(CleaningFlags.Spells))
                CleanCharacterSpell();

            if (flags.HasAnyFlag(CleaningFlags.Talents))
                CleanCharacterTalent();

            if (flags.HasAnyFlag(CleaningFlags.Queststatus))
                CleanCharacterQuestStatus();

            // NOTE: In order to have persistentFlags be set in worldstates for the next cleanup,
            // you need to define them at least once in worldstates.
            flags &= (CleaningFlags)WorldConfig.Values[WorldCfg.PersistentCharacterCleanFlags].Int32;
            Global.WorldMgr.SetPersistentWorldVariable(WorldManager.CharacterDatabaseCleaningFlagsVarId, (int)flags);

            Global.WorldMgr.SetCleaningFlags(flags);

            Log.outInfo(LogFilter.ServerLoading, "Cleaned character database in {0} ms", Time.GetMSTimeDiffToNow(oldMSTime));
        }

        delegate bool CheckFor(int id);

        static void CheckUnique(string column, string table, CheckFor check)
        {
            SQLResult result = DB.Characters.Query($"SELECT DISTINCT {column} FROM {table}");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.Sql, $"Table {table} is empty.");
                return;
            }

            bool found = false;
            StringBuilder ss = new();
            do
            {
                int id = result.Read<int>(0);
                if (!check(id))
                {
                    if (!found)
                    {
                        ss.AppendFormat($"DELETE FROM {table} WHERE {column} IN(");
                        found = true;
                    }
                    else
                        ss.Append(',');

                    ss.Append(id);
                }
            }
            while (result.NextRow());

            if (found)
            {
                ss.Append(')');
                DB.Characters.Execute(ss.ToString());
            }
        }

        static bool AchievementProgressCheck(int criteria)
        {
            return Global.CriteriaMgr.GetCriteria(criteria) != null;
        }

        static void CleanCharacterAchievementProgress()
        {
            CheckUnique("criteria", "character_achievement_progress", AchievementProgressCheck);
        }

        static bool SkillCheck(int skill)
        {
            return CliDB.SkillLineStorage.ContainsKey(skill);
        }

        static void CleanCharacterSkills()
        {
            CheckUnique("skill", "character_skills", SkillCheck);
        }

        static bool SpellCheck(int spell_id)
        {
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spell_id, Difficulty.None);
            return spellInfo != null && !spellInfo.HasAttribute(SpellCustomAttributes.IsTalent);
        }

        static void CleanCharacterSpell()
        {
            CheckUnique("spell", "character_spell", SpellCheck);
        }

        static bool TalentCheck(int talent_id)
        {
            TalentRecord talentInfo = CliDB.TalentStorage.LookupByKey(talent_id);
            if (talentInfo == null)
                return false;

            return CliDB.ChrSpecializationStorage.ContainsKey(talentInfo.SpecID);
        }

        static void CleanCharacterTalent()
        {
            DB.Characters.DirectExecute($"DELETE FROM character_talent WHERE talentGroup > {PlayerConst.MaxSpecializations}");
            CheckUnique("talentId", "character_talent", TalentCheck);
        }

        static void CleanCharacterQuestStatus()
        {
            DB.Characters.DirectExecute("DELETE FROM character_queststatus WHERE status = 0");
        }
    }

    [Flags]
    public enum CleaningFlags
    {
        AchievementProgress = 0x1,
        Skills = 0x2,
        Spells = 0x4,
        Talents = 0x8,
        Queststatus = 0x10
    }
}
