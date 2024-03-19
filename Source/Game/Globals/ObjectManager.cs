// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Framework.Database;
using Framework.IO;
using Game.Achievements;
using Game.Conditions;
using Game.DataStorage;
using Game.Entities;
using Game.Loots;
using Game.Mails;
using Game.Maps;
using Game.Misc;
using Game.Movement;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Game
{
    public sealed class ObjectManager : Singleton<ObjectManager>
    {
        ObjectManager() { }

        //Static Methods
        public static bool NormalizePlayerName(ref string name)
        {
            if (name.IsEmpty())
                return false;

            //CultureInfo cultureInfo = Thread.CurrentThread.CurrentCulture;
            //TextInfo textInfo = cultureInfo.TextInfo;

            //str = textInfo.ToTitleCase(str);

            name = name.ToLower();

            var charArray = name.ToCharArray();
            charArray[0] = char.ToUpper(charArray[0]);

            name = new string(charArray);
            return true;
        }

        public static ExtendedPlayerName ExtractExtendedPlayerName(string name)
        {
            int pos = name.IndexOf('-');
            if (pos != -1)
                return new ExtendedPlayerName(name.Substring(0, pos), name[(pos + 1)..]);
            else
                return new ExtendedPlayerName(name, "");
        }

        static CfgCategoriesCharsets GetRealmLanguageType(bool create)
        {
            Cfg_CategoriesRecord category = CliDB.CfgCategoriesStorage.LookupByKey(Global.WorldMgr.GetRealm().Timezone);
            if (category != null)
                return create ? category.CreateCharsetMask : category.ExistingCharsetMask;

            return create ? CfgCategoriesCharsets.English : CfgCategoriesCharsets.Any;        // basic-Latin at create, any at login
        }

        public static CreatureModel ChooseDisplayId(CreatureTemplate cinfo, CreatureData data = null)
        {
            // Load creature model (display id)
            if (data != null && data.display != null)
                return data.display;

            if (!cinfo.FlagsExtra.HasAnyFlag(CreatureFlagsExtra.Trigger))
            {
                if (cinfo.GetRandomValidModel() is CreatureModel model)
                    return model;
            }

            // Triggers by default receive the invisible model
            return cinfo.GetFirstInvisibleModel();
        }

        public static void ChooseCreatureFlags(CreatureTemplate cInfo, out NPCFlags1 npcFlag, out NPCFlags2 npcFlag2, out UnitFlags unitFlags, out UnitFlags2 unitFlags2, out UnitFlags3 unitFlags3, CreatureStaticFlagsHolder staticFlags, CreatureData data = null)
        {
            npcFlag = data != null && data.npcflag.HasValue ? data.npcflag.Value : cInfo.Npcflag;
            npcFlag2 = data != null && data.npcflag2.HasValue ? data.npcflag2.Value : cInfo.Npcflag2;

            unitFlags = data != null && data.unit_flags.HasValue ? data.unit_flags.Value : cInfo.UnitFlags;
            if (staticFlags.HasFlag(CreatureStaticFlags.CanSwim))
                unitFlags |= UnitFlags.CanSwim;

            unitFlags2 = data != null && data.unit_flags2.HasValue ? data.unit_flags2.Value : cInfo.UnitFlags2;
            if (staticFlags.HasFlag(CreatureStaticFlags3.CannotTurn))
                unitFlags2 |= UnitFlags2.CannotTurn;

            if (staticFlags.HasFlag(CreatureStaticFlags5.InteractWhileHostile))
                unitFlags2 |= UnitFlags2.InteractWhileHostile;

            unitFlags3 = data != null && data.unit_flags3.HasValue ? data.unit_flags3.Value : cInfo.UnitFlags3;
            if (staticFlags.HasFlag(CreatureStaticFlags3.AllowInteractionWhileInCombat))
                unitFlags3 |= UnitFlags3.AllowInteractionWhileInCombat;
        }

        public static ResponseCodes CheckPlayerName(string name, Locale locale, bool create = false)
        {
            if (name.Length > SharedConst.MaxPlayerNameLenght)
                return ResponseCodes.CharNameTooLong;

            uint minName = WorldConfig.GetUIntValue(WorldCfg.MinPlayerName);
            if (name.Length < minName)
                return ResponseCodes.CharNameTooShort;

            uint strictMask = WorldConfig.GetUIntValue(WorldCfg.StrictPlayerNames);
            if (!IsValidString(name, strictMask, false, create))
                return ResponseCodes.CharNameMixedLanguages;

            name = name.ToLower();
            for (int i = 2; i < name.Length; ++i)
                if (name[i] == name[i - 1] && name[i] == name[i - 2])
                    return ResponseCodes.CharNameThreeConsecutive;

            return Global.DB2Mgr.ValidateName(name, locale);
        }

        public static bool IsValidCharterName(string name)
        {
            if (name.Length > SharedConst.MaxCharterNameLenght)
                return false;

            uint minName = WorldConfig.GetUIntValue(WorldCfg.MinCharterName);
            if (name.Length < minName)
                return false;

            uint strictMask = WorldConfig.GetUIntValue(WorldCfg.StrictCharterNames);

            return IsValidString(name, strictMask, true);
        }

        public static PetNameInvalidReason CheckPetName(string name)
        {
            if (name.Length > SharedConst.MaxPetNameLenght)
                return PetNameInvalidReason.TooLong;

            uint minName = WorldConfig.GetUIntValue(WorldCfg.MinPetName);
            if (name.Length < minName)
                return PetNameInvalidReason.TooShort;

            uint strictMask = WorldConfig.GetUIntValue(WorldCfg.StrictPetNames);
            if (!IsValidString(name, strictMask, false))
                return PetNameInvalidReason.MixedLanguages;

            return PetNameInvalidReason.Success;
        }        

        public static void AddLocaleString(string value, Locale locale, StringArray data)
        {
            if (value == null)
                value = string.Empty;

            data[(int)locale] = value;
        }

        public static void GetLocaleString(StringArray data, Locale locale, ref string value)
        {
            if (data.Length > (int)locale && !string.IsNullOrEmpty(data[(int)locale]))
                value = data[(int)locale];
        }

        static bool IsValidString(string str, uint strictMask, bool numericOrSpace, bool create = false)
        {
            if (strictMask == 0)                                       // any language, ignore realm
            {
                if (IsCultureString(CfgCategoriesCharsets.Latin1, str, numericOrSpace))
                    return true;
                if (IsCultureString(CfgCategoriesCharsets.Russian, str, numericOrSpace))
                    return true;
                if (IsCultureString(CfgCategoriesCharsets.Korean, str, numericOrSpace))
                    return true;
                if (IsCultureString(CfgCategoriesCharsets.Chinese, str, numericOrSpace))
                    return true;
                return false;
            }

            if ((strictMask & 0x2) != 0)                                    // realm zone specific
            {
                CfgCategoriesCharsets lt = GetRealmLanguageType(create);
                if (lt == CfgCategoriesCharsets.Any)
                    return true;
                if (lt.HasFlag(CfgCategoriesCharsets.Latin1) && IsCultureString(CfgCategoriesCharsets.Latin1, str, numericOrSpace))
                    return true;
                if (lt.HasFlag(CfgCategoriesCharsets.English) && IsCultureString(CfgCategoriesCharsets.English, str, numericOrSpace))
                    return true;
                if (lt.HasFlag(CfgCategoriesCharsets.Russian) && IsCultureString(CfgCategoriesCharsets.Russian, str, numericOrSpace))
                    return true;
                if (lt.HasFlag(CfgCategoriesCharsets.Korean) && IsCultureString(CfgCategoriesCharsets.Korean, str, numericOrSpace))
                    return true;
                if (lt.HasFlag(CfgCategoriesCharsets.Chinese) && IsCultureString(CfgCategoriesCharsets.Chinese, str, numericOrSpace))
                    return true;
            }

            if ((strictMask & 0x1) != 0)                                    // basic Latin
            {
                if (IsCultureString(CfgCategoriesCharsets.English, str, numericOrSpace))
                    return true;
            }

            return false;
        }

        static bool IsCultureString(CfgCategoriesCharsets culture, string str, bool numericOrSpace)
        {
            foreach (var wchar in str)
            {
                if (numericOrSpace && (char.IsNumber(wchar) || char.IsWhiteSpace(wchar)))
                    return true;

                switch (culture)
                {
                    case CfgCategoriesCharsets.English:
                        if (wchar >= 'a' && wchar <= 'z')                      // LATIN SMALL LETTER A - LATIN SMALL LETTER Z
                            return true;
                        if (wchar >= 'A' && wchar <= 'Z')                      // LATIN CAPITAL LETTER A - LATIN CAPITAL LETTER Z
                            return true;
                        break;
                    case CfgCategoriesCharsets.Latin1:
                        if (wchar >= 'a' && wchar <= 'z')                      // LATIN SMALL LETTER A - LATIN SMALL LETTER Z
                            return true;
                        if (wchar >= 'A' && wchar <= 'Z')                      // LATIN CAPITAL LETTER A - LATIN CAPITAL LETTER Z
                            return true;
                        if (wchar >= 0x00C0 && wchar <= 0x00D6)                  // LATIN CAPITAL LETTER A WITH GRAVE - LATIN CAPITAL LETTER O WITH DIAERESIS
                            return true;
                        if (wchar >= 0x00D8 && wchar <= 0x00DE)                  // LATIN CAPITAL LETTER O WITH STROKE - LATIN CAPITAL LETTER THORN
                            return true;
                        if (wchar == 0x00DF)                                     // LATIN SMALL LETTER SHARP S
                            return true;
                        if (wchar >= 0x00E0 && wchar <= 0x00F6)                  // LATIN SMALL LETTER A WITH GRAVE - LATIN SMALL LETTER O WITH DIAERESIS
                            return true;
                        if (wchar >= 0x00F8 && wchar <= 0x00FE)                  // LATIN SMALL LETTER O WITH STROKE - LATIN SMALL LETTER THORN
                            return true;
                        if (wchar >= 0x0100 && wchar <= 0x012F)                  // LATIN CAPITAL LETTER A WITH MACRON - LATIN SMALL LETTER I WITH OGONEK
                            return true;
                        if (wchar == 0x1E9E)                                     // LATIN CAPITAL LETTER SHARP S
                            return true;
                        break;
                    case CfgCategoriesCharsets.Russian:
                        if (wchar >= 0x0410 && wchar <= 0x044F)                  // CYRILLIC CAPITAL LETTER A - CYRILLIC SMALL LETTER YA
                            return true;
                        if (wchar == 0x0401 || wchar == 0x0451)                  // CYRILLIC CAPITAL LETTER IO, CYRILLIC SMALL LETTER IO
                            return true;
                        break;
                    case CfgCategoriesCharsets.Korean:
                        if (wchar >= 0x1100 && wchar <= 0x11F9)                  // Hangul Jamo
                            return true;
                        if (wchar >= 0x3131 && wchar <= 0x318E)                  // Hangul Compatibility Jamo
                            return true;
                        if (wchar >= 0xAC00 && wchar <= 0xD7A3)                  // Hangul Syllables
                            return true;
                        if (wchar >= 0xFF01 && wchar <= 0xFFEE)                  // Halfwidth forms
                            return true;
                        break;
                    case CfgCategoriesCharsets.Chinese:
                        if (wchar >= 0x4E00 && wchar <= 0x9FFF)                  // Unified CJK Ideographs
                            return true;
                        if (wchar >= 0x3400 && wchar <= 0x4DBF)                  // CJK Ideographs Ext. A
                            return true;
                        if (wchar >= 0x3100 && wchar <= 0x312C)                  // Bopomofo
                            return true;
                        if (wchar >= 0xF900 && wchar <= 0xFAFF)                  // CJK Compatibility Ideographs
                            return true;
                        break;
                }
            }

            return false;
        }

        //General
        public bool LoadCypherStrings()
        {
            var time = Time.GetMSTime();
            CypherStringStorage.Clear();

            SQLResult result = DB.World.Query("SELECT entry, content_default, content_loc1, content_loc2, content_loc3, content_loc4, content_loc5, content_loc6, content_loc7, content_loc8 FROM trinity_string");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 CypherStrings. DB table `trinity_string` is empty.");
                return false;
            }
            uint count = 0;
            do
            {
                uint entry = result.Read<uint>(0);

                CypherStringStorage[entry] = new StringArray((int)SharedConst.DefaultLocale + 1);
                count++;

                for (var i = SharedConst.DefaultLocale; i >= 0; --i)
                    AddLocaleString(result.Read<string>((int)i + 1).ConvertFormatSyntax(), i, CypherStringStorage[entry]);
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} CypherStrings in {Time.GetMSTimeDiffToNow(time)} ms.");
            return true;
        }

        public void LoadRaceAndClassExpansionRequirements()
        {
            uint oldMSTime = Time.GetMSTime();
            _raceUnlockRequirementStorage.Clear();

            {
                //                                             0       1          2
                SQLResult result = DB.World.Query("SELECT raceID, expansion, achievementId FROM `race_unlock_requirement`");
            
                if (!result.IsEmpty())
                {
                    uint count = 0;
                    do
                    {
                        Race raceID = (Race)result.Read<byte>(0);
                        Expansion expansion = (Expansion)result.Read<sbyte>(1);
                        int achievementId = result.Read<int>(2);

                        ChrRacesRecord raceEntry = CliDB.ChrRacesStorage.LookupByKey((int)raceID);
                        if (raceEntry == null)
                        {
                            Log.outError(LogFilter.Sql, $"Race {raceID} defined in `race_unlock_requirement` does not exists, skipped.");
                            continue;
                        }

                        if (expansion >= Expansion.MaxAccountExpansions)
                        {
                            Log.outError(LogFilter.Sql, $"Race {raceID} defined in `race_unlock_requirement` has incorrect expansion {expansion}, skipped.");
                            continue;
                        }

                        if (achievementId != 0 && !CliDB.AchievementStorage.ContainsKey(achievementId))
                        {
                            Log.outError(LogFilter.Sql, $"Race {raceID} defined in `race_unlock_requirement` has incorrect achievement {achievementId}, skipped.");
                            continue;
                        }

                        RaceUnlockRequirement raceUnlockRequirement = new();
                        raceUnlockRequirement.Expansion = expansion;
                        raceUnlockRequirement.AchievementId = achievementId;

                        _raceUnlockRequirementStorage[raceID] = raceUnlockRequirement;

                        ++count;
                    }
                    while (result.NextRow());
                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} race expansion requirements in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
                }
                else
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 race expansion requirements. DB table `race_expansion_requirement` is empty.");
            }

            oldMSTime = Time.GetMSTime();
            _classExpansionRequirementStorage.Clear();

            {
                //                                             0        1       2                     3
                SQLResult result = DB.World.Query("SELECT ClassID, RaceID, ActiveExpansionLevel, AccountExpansionLevel FROM `class_expansion_requirement`");
            
                if (!result.IsEmpty())
                {
                    Dictionary<Race, Dictionary<Class, (Expansion activeExpansionLevel, Expansion accountExpansionLevel)>> temp = new();
                    Array<Expansion> minRequirementForClass = new ((int)Class.Max, Expansion.MaxAccountExpansions);

                    uint count = 0;
                    do
                    {
                        Class classID = (Class)result.Read<byte>(0);
                        Race raceID = (Race)result.Read<byte>(1);
                        Expansion activeExpansionLevel = (Expansion)result.Read<byte>(2);
                        Expansion accountExpansionLevel = (Expansion)result.Read<byte>(3);

                        ChrClassesRecord classEntry = CliDB.ChrClassesStorage.LookupByKey((int)classID);
                        if (classEntry == null)
                        {
                            Log.outError(LogFilter.Sql, $"Class {classID} (race {raceID}) defined in `class_expansion_requirement` does not exists, skipped.");
                            continue;
                        }

                        ChrRacesRecord raceEntry = CliDB.ChrRacesStorage.LookupByKey((int)raceID);
                        if (raceEntry == null)
                        {
                            Log.outError(LogFilter.Sql, $"Race {raceID} (class {classID}) defined in `class_expansion_requirement` does not exists, skipped.");
                            continue;
                        }

                        if (activeExpansionLevel >= Expansion.Max)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Class {classID} Race {raceID} defined in `class_expansion_requirement` has incorrect ActiveExpansionLevel {activeExpansionLevel}, skipped.");
                            continue;
                        }

                        if (accountExpansionLevel >= Expansion.MaxAccountExpansions)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Class {classID} Race {raceID} defined in `class_expansion_requirement` has incorrect AccountExpansionLevel {accountExpansionLevel}, skipped.");
                            continue;
                        }

                        if (!temp.ContainsKey(raceID))
                            temp[raceID] = new();

                        temp[raceID][classID] = (activeExpansionLevel, accountExpansionLevel);
                        minRequirementForClass[(int)classID] = (Expansion)Math.Min((int)minRequirementForClass[(int)classID], (int)activeExpansionLevel);

                        ++count;
                    }
                    while (result.NextRow());

                    foreach (var race in temp)
                    {
                        RaceClassAvailability raceClassAvailability = new();
                        raceClassAvailability.RaceID = race.Key;

                        foreach (var class_ in race.Value)
                        {
                            ClassAvailability classAvailability = new();
                            classAvailability.ClassID = class_.Key;
                            classAvailability.ActiveExpansionLevel = class_.Value.activeExpansionLevel;
                            classAvailability.AccountExpansionLevel = class_.Value.accountExpansionLevel;
                            classAvailability.MinActiveExpansionLevel = minRequirementForClass[(int)class_.Key];

                            raceClassAvailability.Classes.Add(classAvailability);
                        }

                        _classExpansionRequirementStorage.Add(raceClassAvailability);
                    }

                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} class expansion requirements in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
                }
                else
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 class expansion requirements. DB table `class_expansion_requirement` is empty.");
            }
        }       

        public string GetCypherString(uint entry, Locale locale = Locale.enUS)
        {
            if (!CypherStringStorage.ContainsKey(entry))
            {
                Log.outError(LogFilter.Sql, $"Cypher string entry {entry} not found in DB.");
                return "<Error>";
            }

            var cs = CypherStringStorage[entry];
            if (cs.Length > (int)locale && !string.IsNullOrEmpty(cs[(int)locale]))
                return cs[(int)locale];

            return cs[(int)SharedConst.DefaultLocale];
        }

        public string GetCypherString(CypherStrings cmd, Locale locale = Locale.enUS)
        {
            return GetCypherString((uint)cmd, locale);
        }

        public Dictionary<Race, RaceUnlockRequirement> GetRaceUnlockRequirements() { return _raceUnlockRequirementStorage; }
        public RaceUnlockRequirement GetRaceUnlockRequirement(Race race) { return _raceUnlockRequirementStorage.LookupByKey(race); }
        public List<RaceClassAvailability> GetClassExpansionRequirements() { return _classExpansionRequirementStorage; }

        public ClassAvailability GetClassExpansionRequirement(Race raceId, Class classId)
        {
            var raceClassAvailability = _classExpansionRequirementStorage.Find(raceClass =>
            {
                return raceClass.RaceID == raceId;
            });

            if (raceClassAvailability == null)
                return null;

            var classAvailability = raceClassAvailability.Classes.Find(availability =>
            {
                return availability.ClassID == classId;
            });

            if (classAvailability == null)
                return null;

            return classAvailability;
        }

        public ClassAvailability GetClassExpansionRequirementFallback(Class classId)
        {
            foreach (RaceClassAvailability raceClassAvailability in _classExpansionRequirementStorage)
                foreach (ClassAvailability classAvailability in raceClassAvailability.Classes)
                    if (classAvailability.ClassID == classId)
                        return classAvailability;

            return null;
        }

        public PlayerChoice GetPlayerChoice(int choiceId)
        {
            return _playerChoices.LookupByKey(choiceId);
        }

        public PlayerChoiceLocale GetPlayerChoiceLocale(int ChoiceID)
        {
            return _playerChoiceLocales.LookupByKey(ChoiceID);
        }

        //Gossip
        public void LoadGossipMenu()
        {
            uint oldMSTime = Time.GetMSTime();

            gossipMenusStorage.Clear();
            //                                          0       1
            SQLResult result = DB.World.Query("SELECT MenuId, TextId FROM gossip_menu");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gossip_menu entries. DB table `gossip_menu` is empty!");
                return;
            }

            do
            {
                GossipMenus gMenu = new();

                gMenu.MenuId = result.Read<int>(0);
                gMenu.TextId = result.Read<int>(1);

                if (GetNpcText(gMenu.TextId) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table gossip_menu: Id {gMenu.MenuId} is using non-existing TextId {gMenu.TextId}.");
                    continue;
                }

                gossipMenusStorage.Add(gMenu.MenuId, gMenu);
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {gossipMenusStorage.Count} gossip_menu Ids in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadGossipMenuItems()
        {
            uint oldMSTime = Time.GetMSTime();

            gossipMenuItemsStorage.Clear();

            SQLResult result = DB.World.Query(
                //      0       1               2         3          4           5                      6         7      8            9             10
                "SELECT MenuID, GossipOptionID, OptionID, OptionNpc, OptionText, OptionBroadcastTextID, Language, Flags, ActionMenuID, ActionPoiID, GossipNpcOptionID, " +
                //11       12        13       14                  15       16
                "BoxCoded, BoxMoney, BoxText, BoxBroadcastTextID, SpellID, OverrideIconID " +
                "FROM gossip_menu_option ORDER BY MenuID, OptionID");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gossip_menu_option Ids. DB table `gossip_menu_option` is empty!");
                return;
            }

            Dictionary<int, int> optionToNpcOption = new();
            foreach (var npcOption in CliDB.GossipNPCOptionStorage.Values)
                optionToNpcOption[npcOption.GossipOptionID] = npcOption.Id;

            do
            {
                GossipMenuItems gMenuItem = new();

                gMenuItem.MenuID = result.Read<int>(0);
                gMenuItem.GossipOptionID = result.Read<int>(1);
                gMenuItem.OrderIndex = result.Read<int>(2);
                gMenuItem.OptionNpc = (GossipOptionNpc)result.Read<byte>(3);
                gMenuItem.OptionText = result.Read<string>(4);
                gMenuItem.OptionBroadcastTextId = result.Read<int>(5);
                gMenuItem.Language = result.Read<int>(6);
                gMenuItem.Flags = (GossipOptionFlags)result.Read<int>(7);
                gMenuItem.ActionMenuID = result.Read<int>(8);
                gMenuItem.ActionPoiID = result.Read<int>(9);
                if (!result.IsNull(10))
                    gMenuItem.GossipNpcOptionID = result.Read<int>(10);

                gMenuItem.BoxCoded = result.Read<bool>(11);
                gMenuItem.BoxMoney = result.Read<uint>(12);
                gMenuItem.BoxText = result.Read<string>(13);
                gMenuItem.BoxBroadcastTextId = result.Read<int>(14);
                if (!result.IsNull(15))
                    gMenuItem.SpellID = result.Read<int>(15);

                if (!result.IsNull(16))
                    gMenuItem.OverrideIconID = result.Read<int>(16);

                if (gMenuItem.OptionNpc >= GossipOptionNpc.Max)
                {
                    Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for menu {gMenuItem.MenuID}, id {gMenuItem.OrderIndex} has unknown NPC option " +
                        $"id {gMenuItem.OptionNpc}. Replacing with GossipOptionNpc.None");
                    gMenuItem.OptionNpc = GossipOptionNpc.None;
                }

                if (gMenuItem.OptionBroadcastTextId != 0)
                {
                    if (!CliDB.BroadcastTextStorage.ContainsKey(gMenuItem.OptionBroadcastTextId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for MenuId {gMenuItem.MenuID}, OptionIndex {gMenuItem.OrderIndex} has non-existing " +
                            $"or incompatible OptionBroadcastTextId {gMenuItem.OptionBroadcastTextId}, ignoring.");
                        gMenuItem.OptionBroadcastTextId = 0;
                    }
                }

                if (gMenuItem.Language != 0 && !CliDB.LanguagesStorage.ContainsKey(gMenuItem.Language))
                {
                    Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for menu {gMenuItem.MenuID}, id {gMenuItem.OrderIndex} use non-existing " +
                        $"Language {gMenuItem.Language}, ignoring.");
                    gMenuItem.Language = 0;
                }

                if (gMenuItem.ActionMenuID != 0 && gMenuItem.OptionNpc != GossipOptionNpc.None)
                {
                    Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for menu {gMenuItem.MenuID}, id {gMenuItem.OrderIndex} can not use ActionMenuID " +
                        $"for GossipOptionNpc different from GossipOptionNpc.None, ignoring.");
                    gMenuItem.ActionMenuID = 0;
                }

                if (gMenuItem.ActionPoiID != 0)
                {
                    if (gMenuItem.OptionNpc != GossipOptionNpc.None)
                    {
                        Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for menu {gMenuItem.MenuID}, id {gMenuItem.OrderIndex} can not use ActionPoiID " +
                            $"for GossipOptionNpc different from GossipOptionNpc.None, ignoring.");
                        gMenuItem.ActionPoiID = 0;
                    }
                    else if (GetPointOfInterest(gMenuItem.ActionPoiID) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for menu {gMenuItem.MenuID}, id {gMenuItem.OrderIndex} use non-existing " +
                            $"ActionPoiID {gMenuItem.ActionPoiID}, ignoring.");
                        gMenuItem.ActionPoiID = 0;
                    }
                }

                if (gMenuItem.GossipNpcOptionID.HasValue)
                {
                    if (!CliDB.GossipNPCOptionStorage.ContainsKey(gMenuItem.GossipNpcOptionID.Value))
                    {
                        Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for menu {gMenuItem.MenuID}, id {gMenuItem.OrderIndex} use non-existing " +
                            $"GossipNPCOption {gMenuItem.GossipNpcOptionID}, ignoring.");
                        gMenuItem.GossipNpcOptionID = null;
                    }
                }
                else
                {
                    int npcOptionId = optionToNpcOption.LookupByKey(gMenuItem.GossipOptionID);
                    if (npcOptionId != 0)
                        gMenuItem.GossipNpcOptionID = npcOptionId;
                }

                if (gMenuItem.BoxBroadcastTextId != 0)
                {
                    if (!CliDB.BroadcastTextStorage.ContainsKey(gMenuItem.BoxBroadcastTextId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for MenuId {gMenuItem.MenuID}, OptionIndex {gMenuItem.OrderIndex} has " +
                            $"non-existing or incompatible BoxBroadcastTextId {gMenuItem.BoxBroadcastTextId}, ignoring.");
                        gMenuItem.BoxBroadcastTextId = 0;
                    }
                }

                if (gMenuItem.SpellID.HasValue)
                {
                    if (!Global.SpellMgr.HasSpellInfo(gMenuItem.SpellID.Value, Difficulty.None))
                    {
                        Log.outError(LogFilter.Sql, $"Table `gossip_menu_option` for menu {gMenuItem.MenuID}, id {gMenuItem.OrderIndex} use non-existing " +
                            $"Spell {gMenuItem.SpellID}, ignoring.");
                        gMenuItem.SpellID = null;
                    }
                }

                gossipMenuItemsStorage.Add(gMenuItem.MenuID, gMenuItem);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {gossipMenuItemsStorage.Count} gossip_menu_option entries in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadGossipMenuAddon()
        {
            uint oldMSTime = Time.GetMSTime();

            _gossipMenuAddonStorage.Clear();

            //                                           0       1
            SQLResult result = DB.World.Query("SELECT MenuID, FriendshipFactionID FROM gossip_menu_addon");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gossip_menu_addon IDs. DB table `gossip_menu_addon` is empty!");
                return;
            }

            do
            {
                int menuID = result.Read<int>(0);
                GossipMenuAddon addon = new();
                addon.FriendshipFactionID = result.Read<int>(1);

                var faction = CliDB.FactionStorage.LookupByKey(addon.FriendshipFactionID);
                if (faction != null)
                {
                    if (!CliDB.FriendshipReputationStorage.ContainsKey(faction.FriendshipRepID))
                    {
                        Log.outError(LogFilter.Sql, $"Table gossip_menu_addon: ID {menuID} is using FriendshipFactionID {addon.FriendshipFactionID} " +
                            $"referencing non-existing FriendshipRepID {faction.FriendshipRepID}.");
                        addon.FriendshipFactionID = 0;
                    }
                }
                else
                {
                    Log.outError(LogFilter.Sql, $"Table gossip_menu_addon: ID {menuID} is using non-existing FriendshipFactionID {addon.FriendshipFactionID}.");
                    addon.FriendshipFactionID = 0;
                }

                _gossipMenuAddonStorage[menuID] = addon;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_gossipMenuAddonStorage.Count} gossip_menu_addon IDs in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }        

        public void LoadPointsOfInterest()
        {
            uint oldMSTime = Time.GetMSTime();

            pointsOfInterestStorage.Clear(); // need for reload case

            //                                        0   1          2          3          4     5      6           7     8
            SQLResult result = DB.World.Query("SELECT ID, PositionX, PositionY, PositionZ, Icon, Flags, Importance, Name, WMOGroupID FROM points_of_interest");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 Points of Interest definitions. DB table `points_of_interest` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int id = result.Read<int>(0);

                PointOfInterest POI = new();
                POI.Id = id;
                POI.Pos = new Vector3(result.Read<float>(1), result.Read<float>(2), result.Read<float>(3));
                POI.Icon = result.Read<int>(4);
                POI.Flags = result.Read<uint>(5);
                POI.Importance = result.Read<int>(6);
                POI.Name = result.Read<string>(7);
                POI.WMOGroupID = result.Read<int>(8);

                if (!GridDefines.IsValidMapCoord(POI.Pos.X, POI.Pos.Y, POI.Pos.Z))
                {
                    Log.outError(LogFilter.Sql, $"Table `points_of_interest` (ID: {id}) have invalid coordinates (PositionX: {POI.Pos.X} PositionY: {POI.Pos.Y} PositionZ: {POI.Pos.Z}), ignored.");
                    continue;
                }

                pointsOfInterestStorage[id] = POI;

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Points of Interest definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public List<GossipMenus> GetGossipMenusMapBounds(int uiMenuId)
        {
            return gossipMenusStorage.LookupByKey(uiMenuId);
        }

        public List<GossipMenuItems> GetGossipMenuItemsMapBounds(int uiMenuId)
        {
            return gossipMenuItemsStorage.LookupByKey(uiMenuId);
        }

        public GossipMenuAddon GetGossipMenuAddon(int menuId)
        {
            return _gossipMenuAddonStorage.LookupByKey(menuId);
        }

        public PointOfInterest GetPointOfInterest(int id)
        {
            return pointsOfInterestStorage.LookupByKey(id);
        }

        public void LoadGraveyardZones()
        {
            uint oldMSTime = Time.GetMSTime();

            GraveyardStorage.Clear();                                  // need for reload case

            //                                         0       1         2
            SQLResult result = DB.World.Query("SELECT ID, GhostZone, faction FROM graveyard_zone");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 graveyard-zone links. DB table `graveyard_zone` is empty.");
                return;
            }

            uint count = 0;

            do
            {
                int safeLocId = result.Read<int>(0);
                int zoneId = result.Read<int>(1);

                WorldSafeLocsEntry entry = GetWorldSafeLoc(safeLocId);
                if (entry == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `graveyard_zone` has a record for not existing graveyard (WorldSafeLocs.dbc id) {safeLocId}, skipped.");
                    continue;
                }

                AreaTableRecord areaEntry = CliDB.AreaTableStorage.LookupByKey(zoneId);
                if (areaEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `graveyard_zone` has a record for not existing zone id ({zoneId}), skipped.");
                    continue;
                }

                if (!AddGraveyardLink(safeLocId, zoneId, 0, false))
                    Log.outError(LogFilter.Sql, $"Table `graveyard_zone` has a duplicate record for Graveyard (ID: {safeLocId}) and Zone (ID: {zoneId}), skipped.");

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} graveyard-zone links in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadWorldSafeLocs()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0   1      2     3     4     5
            SQLResult result = DB.World.Query("SELECT ID, MapID, LocX, LocY, LocZ, Facing FROM world_safe_locs");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 world locations. DB table `world_safe_locs` is empty.");
                return;
            }

            do
            {
                int id = result.Read<int>(0);
                WorldLocation loc = new(result.Read<int>(1), result.Read<float>(2), result.Read<float>(3), result.Read<float>(4), MathFunctions.DegToRad(result.Read<float>(5)));
                if (!GridDefines.IsValidMapCoord(loc))
                {
                    Log.outError(LogFilter.Sql, $"World location (ID: {id}) has a invalid position MapID: {loc.GetMapId()} {loc}, skipped.");
                    continue;
                }

                WorldSafeLocsEntry worldSafeLocs = new();
                worldSafeLocs.Id = id;
                worldSafeLocs.Loc = loc;
                _worldSafeLocs[id] = worldSafeLocs;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_worldSafeLocs.Count} world locations {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public WorldSafeLocsEntry GetDefaultGraveyard(Team team)
        {            
            if (team == Team.Horde)
                return GetWorldSafeLoc((int)DefaultGraveyard.HORDE_GRAVEYARD);
            else if (team == Team.Alliance)
                return GetWorldSafeLoc((int)DefaultGraveyard.ALLIANCE_GRAVEYARD);
            else return null;
        }

        public WorldSafeLocsEntry GetClosestGraveyard(WorldLocation location, Team team, WorldObject conditionObject)
        {
            var pos = location.GetPosition3D();
            int MapId = location.GetMapId();

            // search for zone associated closest graveyard
            int zoneId = Global.TerrainMgr.GetZoneId(conditionObject != null ? conditionObject.GetPhaseShift() : PhasingHandler.EmptyPhaseShift, MapId, pos.X, pos.Y, pos.Z);
            if (zoneId == 0)
            {
                if (pos.Z > -500)
                {
                    Log.outError(LogFilter.Server, $"ZoneId not found for map {MapId} coords ({pos.X}, {pos.Y}, {pos.Z}).");
                    return GetDefaultGraveyard(team);
                }
            }

            var graveyard = GetClosestGraveyardInZone(location, team, conditionObject, zoneId);
            var zoneEntry = CliDB.AreaTableStorage.LookupByKey(zoneId);
            var parentEntry = CliDB.AreaTableStorage.LookupByKey(zoneEntry.ParentAreaID);

            while (graveyard == null && parentEntry != null)
            {
                graveyard = GetClosestGraveyardInZone(location, team, conditionObject, parentEntry.Id);
                if (graveyard == null && parentEntry.ParentAreaID != 0)
                    parentEntry = CliDB.AreaTableStorage.LookupByKey(parentEntry.ParentAreaID);
                else // nothing found, cant look further, give up.
                    parentEntry = null;
            }

            return graveyard;
        }

        WorldSafeLocsEntry GetClosestGraveyardInZone(WorldLocation location, Team team, WorldObject conditionObject, int zoneId)
        {
            var pos = location.GetPosition3D();
            int MapId = location.GetMapId();
            // Simulate std. algorithm:
            //   found some graveyard associated to (ghost_zone, ghost_map)
            //
            //   if mapId == graveyard.mapId (ghost in plain zone or city or Battleground) and search graveyard at same map
            //     then check faction
            //   if mapId != graveyard.mapId (ghost in instance) and search any graveyard associated
            //     then check faction
            var range = GraveyardStorage.LookupByKey(zoneId);
            MapRecord mapEntry = CliDB.MapStorage.LookupByKey(MapId);

            ConditionSourceInfo conditionSource = new(conditionObject);

            // not need to check validity of map object; MapId _MUST_ be valid here
            if (range.Empty() && !mapEntry.IsBattlegroundOrArena)
            {
                if (zoneId != 0) // zone == 0 can't be fixed, used by bliz for bugged zones
                    Log.outError(LogFilter.Sql, $"Table `game_graveyard_zone` incomplete: Zone {zoneId} Team {team} does not have a linked graveyard.");
                return GetDefaultGraveyard(team);
            }

            // at corpse map
            bool foundNear = false;
            float distNear = 10000;
            WorldSafeLocsEntry entryNear = null;

            // at entrance map for corpse map
            bool foundEntr = false;
            //float distEntr = 10000;
            WorldSafeLocsEntry entryEntr = null;

            // some where other
            WorldSafeLocsEntry entryFar = null;

            foreach (var data in range)
            {
                WorldSafeLocsEntry entry = GetWorldSafeLoc(data.SafeLocId);
                if (entry == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `game_graveyard_zone` has record for not existing graveyard (WorldSafeLocs.dbc id) {data.SafeLocId}, skipped.");
                    continue;
                }

                if (conditionObject != null)
                {
                    if (!data.Conditions.Meets(conditionSource))
                        continue;

                    if (entry.Loc.GetMapId() == mapEntry.ParentMapID && !conditionObject.GetPhaseShift().HasVisibleMapId(entry.Loc.GetMapId()))
                        continue;
                }
                else if (team != 0)
                {
                    bool teamConditionMet = true;
                    foreach (Condition cond in data.Conditions.Conditions)
                    {
                        if (cond.ConditionType != ConditionTypes.Team)
                            continue;

                        if (cond.ConditionValue1 == (uint)team)
                            continue;

                        teamConditionMet = false;
                    }

                    if (!teamConditionMet)
                        continue;
                }

                // find now nearest graveyard at other map
                if (MapId != entry.Loc.GetMapId() && entry.Loc.GetMapId() != mapEntry.ParentMapID)
                {
                    Vector2 mapCorpsePosition = GetMapCorpsePosition(mapEntry.Id);

                    // if find graveyard at different map from where entrance placed (or no entrance data), use any first
                    if (mapEntry == null
                        || mapEntry.CorpseMapID < 0
                        || mapEntry.CorpseMapID != entry.Loc.GetMapId())

                    {
                        // not have any corrdinates for check distance anyway
                        entryFar = entry;
                        continue;
                    }

                    Vector2 entryLocPosition = entry.Loc.GetPosition2D();

                    //DistanceSquared is faster than Distance. And we don't care about the value itself, we compare the results
                    float dist2 = Vector2.DistanceSquared(entryLocPosition, mapCorpsePosition);

                    if (foundEntr)
                    {
                        //distEntr = 0.0f;
                        entryEntr = entry;
                    }
                    else
                    {
                        foundEntr = true;
                        //distEntr = 0.0f;
                        entryEntr = entry;
                    }
                }

                // find now nearest graveyard at same map
                else
                {
                    //DistanceSquared is faster than Distance. And we don't care about the value itself, we compare the results
                    float dist3 = Vector3.DistanceSquared(entry.Loc.GetPosition3D(), pos);
                    if (foundNear)
                    {
                        if (dist3 < distNear)
                        {
                            distNear = dist3;
                            entryNear = entry;
                        }
                    }
                    else
                    {
                        foundNear = true;
                        distNear = dist3;
                        entryNear = entry;
                    }
                }
            }

            if (entryNear != null)
                return entryNear;

            if (entryEntr != null)
                return entryEntr;

            return entryFar;
        }

        public GraveyardData FindGraveyardData(int id, int zoneId)
        {
            var range = GraveyardStorage.LookupByKey(zoneId);
            foreach (var data in range)
            {
                if (data.SafeLocId == id)
                    return data;
            }
            return null;
        }

        public WorldSafeLocsEntry GetWorldSafeLoc(int id)
        {
            return _worldSafeLocs.LookupByKey(id);
        }

        public Dictionary<int, WorldSafeLocsEntry> GetWorldSafeLocs()
        {
            return _worldSafeLocs;
        }

        public bool AddGraveyardLink(int id, int zoneId, Team team, bool persist = true)
        {
            if (FindGraveyardData(id, zoneId) != null)
                return false;

            // add link to loaded data
            GraveyardData data = new();
            data.SafeLocId = id;

            GraveyardStorage.Add(zoneId, data);

            // add link to DB
            if (persist)
            {
                PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.INS_GRAVEYARD_ZONE);

                stmt.AddValue(0, id);
                stmt.AddValue(1, zoneId);

                DB.World.Execute(stmt);

                // Store graveyard condition if team is set
                if (team != 0)
                {
                    PreparedStatement conditionStmt = WorldDatabase.GetPreparedStatement(WorldStatements.INS_CONDITION);
                    conditionStmt.AddValue(0, (uint)ConditionSourceType.Graveyard); // SourceTypeOrReferenceId
                    conditionStmt.AddValue(1, zoneId); // SourceGroup
                    conditionStmt.AddValue(2, id); // SourceEntry
                    conditionStmt.AddValue(3, 0); // SourceId
                    conditionStmt.AddValue(4, 0); // ElseGroup
                    conditionStmt.AddValue(5, (uint)ConditionTypes.Team); // ConditionTypeOrReference
                    conditionStmt.AddValue(6, 0); // ConditionTarget
                    conditionStmt.AddValue(7, (uint)team); // ConditionValue1
                    conditionStmt.AddValue(8, 0); // ConditionValue2
                    conditionStmt.AddValue(9, 0); // ConditionValue3
                    conditionStmt.AddValue(10, 0); // NegativeCondition
                    conditionStmt.AddValue(11, 0); // ErrorType
                    conditionStmt.AddValue(12, 0); // ErrorTextId
                    conditionStmt.AddValue(13, ""); // ScriptName
                    conditionStmt.AddValue(14, ""); // Comment

                    DB.World.Execute(conditionStmt);

                    // reload conditions to make sure everything is loaded as it should be
                    Global.ConditionMgr.LoadConditions(true);
                    //Global.ScriptMgr.NotifyScriptIDUpdate();
                }
            }

            return true;
        }

        //Scripts
        public void LoadAreaTriggerScripts()
        {
            uint oldMSTime = Time.GetMSTime();

            areaTriggerScriptStorage.Clear();                            // need for reload case
            SQLResult result = DB.World.Query("SELECT entry, ScriptName FROM areatrigger_scripts");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 areatrigger scripts. DB table `areatrigger_scripts` is empty.");
                return;
            }
            uint count = 0;
            do
            {
                int triggerId = result.Read<int>(0);
                string scriptName = result.Read<string>(1);

                AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(triggerId);
                if (atEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Area trigger (Id:{triggerId}) does not exist in `AreaTrigger.dbc`.");
                    continue;
                }
                ++count;
                areaTriggerScriptStorage[triggerId] = GetScriptId(scriptName);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} areatrigger scripts in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        void LoadScripts(ScriptsType type)
        {
            uint oldMSTime = Time.GetMSTime();

            var scripts = GetScriptsMapByType(type);
            if (scripts == null)
                return;

            string tableName = GetScriptsTableNameByType(type);
            if (string.IsNullOrEmpty(tableName))
                return;

            if (Global.MapMgr.IsScriptScheduled())                    // function cannot be called when scripts are in use.
                return;

            Log.outInfo(LogFilter.ServerLoading, $"Loading {tableName}...");

            scripts.Clear();                                       // need for reload support

            bool isSpellScriptTable = (type == ScriptsType.Spell);
            //                                          0    1       2         3         4          5    6  7  8  9
            SQLResult result = DB.World.Query($"SELECT id, delay, command, datalong, datalong2, dataint, x, y, z, o{0} FROM {1}", isSpellScriptTable ? ", effIndex" : "", tableName);
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, $"Loaded 0 script definitions. DB table `{tableName}` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                ScriptInfo tmp = new();
                tmp.type = type;
                tmp.id = result.Read<int>(0);
                if (isSpellScriptTable)
                    tmp.id |= result.Read<int>(10) << 24;
                tmp.delay = result.Read<uint>(1);
                tmp.command = (ScriptCommands)result.Read<uint>(2);
                unsafe
                {
                    tmp.Raw.nData[0] = result.Read<uint>(3);
                    tmp.Raw.nData[1] = result.Read<uint>(4);
                    tmp.Raw.nData[2] = result.Read<uint>(5);
                    tmp.Raw.fData[0] = result.Read<float>(6);
                    tmp.Raw.fData[1] = result.Read<float>(7);
                    tmp.Raw.fData[2] = result.Read<float>(8);
                    tmp.Raw.fData[3] = result.Read<float>(9);
                }

                // generic command args check
                switch (tmp.command)
                {
                    case ScriptCommands.Talk:
                        {
                            if (tmp.Talk.ChatType > ChatMsg.RaidBossWhisper)
                            {
                                Log.outError(LogFilter.Sql,
                                    $"Table `{tableName}` has invalid talk Type (datalong = {tmp.Talk.ChatType}) in SCRIPT_COMMAND_TALK for script id {tmp.id}.");
                                continue;
                            }
                            if (!CliDB.BroadcastTextStorage.ContainsKey(tmp.Talk.TextID))
                            {
                                Log.outError(LogFilter.Sql,
                                    $"Table `{tableName}` has invalid talk text id (dataint = {tmp.Talk.TextID}) in SCRIPT_COMMAND_TALK for script id {tmp.id}.");
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.Emote:
                        {
                            if (!CliDB.EmotesStorage.ContainsKey(tmp.Emote.EmoteID))
                            {
                                Log.outError(LogFilter.Sql,
                                    $"Table `{tableName}` has invalid emote id (datalong = {tmp.Emote.EmoteID}) in SCRIPT_COMMAND_EMOTE for script id {tmp.id}.");
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.TeleportTo:
                        {
                            if (!CliDB.MapStorage.ContainsKey(tmp.TeleportTo.MapID))
                            {
                                Log.outError(LogFilter.Sql,
                                    $"Table `{tableName}` has invalid map (Id: {tmp.TeleportTo.MapID}) in SCRIPT_COMMAND_TELEPORT_TO for script id {tmp.id}.");
                                continue;
                            }

                            if (!GridDefines.IsValidMapCoord(tmp.TeleportTo.DestX, tmp.TeleportTo.DestY, tmp.TeleportTo.DestZ, tmp.TeleportTo.Orientation))
                            {
                                Log.outError(LogFilter.Sql, $"Table `{tableName}` has invalid coordinates (X: {tmp.TeleportTo.DestX} Y: {tmp.TeleportTo.DestY} " +
                                    $"Z: {tmp.TeleportTo.DestZ} O: {tmp.TeleportTo.Orientation}) in SCRIPT_COMMAND_TELEPORT_TO for script id {tmp.id}.");
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.QuestExplored:
                    {
                        Quest quest = GetQuestTemplate(tmp.QuestExplored.QuestID);
                        if (quest == null)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Table `{tableName}` has invalid quest (ID: {tmp.QuestExplored.QuestID}) in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {tmp.id}.");
                            continue;
                        }

                        if (!quest.HasAnyFlag(QuestFlags.CompletionEvent) && !quest.HasAnyFlag(QuestFlags.CompletionAreaTrigger))
                        {
                            Log.outError(LogFilter.Sql, $"Table `{tableName}` has quest (ID: {tmp.QuestExplored.QuestID}) in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {tmp.id}, " +
                                $"but quest not have QUEST_FLAGS_COMPLETION_EVENT or QUEST_FLAGS_COMPLETION_AREA_TRIGGER in quest flags. Script command will do nothing.");

                            continue;
                        }

                        if (tmp.QuestExplored.Distance > SharedConst.DefaultVisibilityDistance)
                        {
                            Log.outError(LogFilter.Sql, $"Table `{tableName}` has too large distance ({tmp.QuestExplored.Distance}) for exploring objective complete in " +
                                $"`datalong2` in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {tmp.id}.");
                            continue;
                        }

                        if (tmp.QuestExplored.Distance != 0 && tmp.QuestExplored.Distance > SharedConst.DefaultVisibilityDistance)
                        {
                            Log.outError(LogFilter.Sql, $"Table `{tableName}` has too large distance ({tmp.QuestExplored.Distance}) for exploring objective complete in " +
                                $"`datalong2` in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {tmp.id}, max distance is {SharedConst.DefaultVisibilityDistance} " +
                                "or 0 for disable distance check.");
                            continue;
                        }

                        if (tmp.QuestExplored.Distance != 0 && tmp.QuestExplored.Distance < SharedConst.InteractionDistance)
                        {
                            Log.outError(LogFilter.Sql, $"Table `{tableName}` has too small distance ({tmp.QuestExplored.Distance}) for exploring objective complete in " +
                                $"`datalong2` in SCRIPT_COMMAND_QUEST_EXPLORED in `datalong` for script id {tmp.id}, min distance is {SharedConst.InteractionDistance} " +
                                "or 0 for disable distance check.");
                            continue;
                        }

                        break;
                    }

                    case ScriptCommands.KillCredit:
                        {
                            if (GetCreatureTemplate(tmp.KillCredit.CreatureEntry) == null)
                            {
                                Log.outError(LogFilter.Sql, $"Table `{tableName}` has invalid creature (Entry: {tmp.KillCredit.CreatureEntry}) in " +
                                    $"SCRIPT_COMMAND_KILL_CREDIT for script id {tmp.id}.");
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.RespawnGameobject:
                    {
                        GameObjectData data = GetGameObjectData(tmp.RespawnGameObject.GOGuid);
                        if (data == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `{tableName}` has invalid gameobject (GUID: {tmp.RespawnGameObject.GOGuid}) in " +
                                $"SCRIPT_COMMAND_RESPAWN_GAMEOBJECT for script id {tmp.id}.");
                            continue;
                        }

                        GameObjectTemplate info = GetGameObjectTemplate(data.Id);
                        if (info == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `{tableName}` has gameobject with invalid entry (GUID: {tmp.RespawnGameObject.GOGuid} Entry: {data.Id}) in " +
                                $"SCRIPT_COMMAND_RESPAWN_GAMEOBJECT for script id {tmp.id}.");
                            continue;
                        }

                        if (info.type == GameObjectTypes.FishingNode ||
                            info.type == GameObjectTypes.FishingHole ||
                            info.type == GameObjectTypes.Door ||
                            info.type == GameObjectTypes.Button ||
                            info.type == GameObjectTypes.Trap)
                        {
                            Log.outError(LogFilter.Sql, $"Table `{tableName}` have gameobject Type ({info.entry}) unsupported by command SCRIPT_COMMAND_RESPAWN_GAMEOBJECT " +
                                $"for script id {tmp.id}.");
                            continue;
                        }
                        break;
                    }

                    case ScriptCommands.TempSummonCreature:
                        {
                            if (!GridDefines.IsValidMapCoord(tmp.TempSummonCreature.PosX, tmp.TempSummonCreature.PosY, tmp.TempSummonCreature.PosZ, tmp.TempSummonCreature.Orientation))
                            {
                                Log.outError(LogFilter.Sql, $"Table `{tableName}` has invalid coordinates (X: {tmp.TempSummonCreature.PosX} Y: {tmp.TempSummonCreature.PosY} " +
                                    $"Z: {tmp.TempSummonCreature.PosZ} O: {tmp.TempSummonCreature.Orientation}) in SCRIPT_COMMAND_TEMP_SUMMON_CREATURE for script id {tmp.id}.");
                                continue;
                            }

                            if (GetCreatureTemplate(tmp.TempSummonCreature.CreatureEntry) == null)
                            {
                                Log.outError(LogFilter.Sql, $"Table `{tableName}` has invalid creature (Entry: {tmp.TempSummonCreature.CreatureEntry}) in " +
                                    $"SCRIPT_COMMAND_TEMP_SUMMON_CREATURE for script id {tmp.id}.");
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.OpenDoor:
                    case ScriptCommands.CloseDoor:
                        {
                            GameObjectData data = GetGameObjectData(tmp.ToggleDoor.GOGuid);
                            if (data == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `{tableName}` has invalid gameobject (GUID: {tmp.ToggleDoor.GOGuid}) in {tmp.command} for script id {tmp.id}.");
                                continue;
                            }

                            GameObjectTemplate info = GetGameObjectTemplate(data.Id);
                            if (info == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `{tableName}` has gameobject with invalid entry (GUID: {tmp.ToggleDoor.GOGuid} Entry: {data.Id}) in {tmp.command} " +
                                    $"for script id {tmp.id}.");
                                continue;
                            }

                            if (info.type != GameObjectTypes.Door)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `{tableName}` has gameobject Type ({info.entry}) non supported by command {tmp.command} for script id {tmp.id}.");
                                continue;
                            }

                            break;
                        }

                    case ScriptCommands.RemoveAura:
                        {
                            if (!Global.SpellMgr.HasSpellInfo(tmp.RemoveAura.SpellID, Difficulty.None))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `{tableName}` using non-existent spell (id: {tmp.RemoveAura.SpellID}) in SCRIPT_COMMAND_REMOVE_AURA for script id {tmp.id}.");
                                continue;
                            }
                            if (tmp.RemoveAura.Flags.HasAnyFlag(~eScriptFlags.AllRemoveAura))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `{tableName}` using unknown flags in datalong2 ({tmp.RemoveAura.Flags}) in SCRIPT_COMMAND_REMOVE_AURA for script id {tmp.id}.");
                                continue;
                            }
                            break;
                        }

                    case ScriptCommands.CastSpell:
                    {
                        if (!Global.SpellMgr.HasSpellInfo(tmp.CastSpell.SpellID, Difficulty.None))
                        {
                            Log.outError(LogFilter.Sql,
                                $"Table `{tableName}` using non-existent spell (id: {tmp.CastSpell.SpellID}) in SCRIPT_COMMAND_CAST_SPELL for script id {tmp.id}.");
                            continue;
                        }
                        if (tmp.CastSpell.Flags > eScriptFlags.CastSpellTargetLast)                      // targeting Type
                        {
                            Log.outError(LogFilter.Sql,
                                $"Table `{tableName}` using unknown target in datalong2 ({tmp.CastSpell.Flags}) in SCRIPT_COMMAND_CAST_SPELL for script id {tmp.id}.");
                            continue;
                        }
                        if (tmp.CastSpell.Flags != eScriptFlags.CastspellSearchCreature && tmp.CastSpell.CreatureEntry.HasAnyFlag(~(int)eScriptFlags.AllCastspell))                      // 1 bit (0, 1)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Table `{tableName}` using unknown flags in dataint ({tmp.CastSpell.CreatureEntry}) in SCRIPT_COMMAND_CAST_SPELL for script id {tmp.id}.");
                            continue;
                        }
                        else if (tmp.CastSpell.Flags == eScriptFlags.CastSpellTargetLast && GetCreatureTemplate(tmp.CastSpell.CreatureEntry) == null)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Table `{tableName}` using invalid creature entry in dataint ({tmp.CastSpell.CreatureEntry}) in SCRIPT_COMMAND_CAST_SPELL for script id {tmp.id}.");
                            continue;
                        }
                        break;
                    }

                    case ScriptCommands.CreateItem:
                        {
                            if (GetItemTemplate(tmp.CreateItem.ItemEntry) == null)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `{tableName}` has nonexistent item (entry: {tmp.CreateItem.ItemEntry}) in SCRIPT_COMMAND_CREATE_ITEM for script id {tmp.id}.");
                                continue;
                            }
                            if (tmp.CreateItem.Amount == 0)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `{tableName}` SCRIPT_COMMAND_CREATE_ITEM but amount is {tmp.CreateItem.Amount} for script id {tmp.id}.");
                                continue;
                            }
                            break;
                        }
                    case ScriptCommands.PlayAnimkit:
                        {
                            if (!CliDB.AnimKitStorage.ContainsKey(tmp.PlayAnimKit.AnimKitID))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `{tableName}` has invalid AnimKid id (datalong = {tmp.PlayAnimKit.AnimKitID}) in SCRIPT_COMMAND_PLAY_ANIMKIT for script id {tmp.id}.");
                                continue;
                            }
                            break;
                        }
                    case ScriptCommands.FieldSetDeprecated:
                    case ScriptCommands.FlagSetDeprecated:
                    case ScriptCommands.FlagRemoveDeprecated:
                        {
                            Log.outError(LogFilter.Sql, $"Table `{tableName}` uses deprecated direct updatefield modify command {tmp.command} for script id {tmp.id}.");
                            continue;
                        }
                    default:
                        break;
                }

                if (!scripts.ContainsKey(tmp.id))
                    scripts[tmp.id] = new MultiMap<int, ScriptInfo>();

                scripts[tmp.id].Add((int)tmp.delay, tmp);

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} script definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }

        public void LoadSpellScripts()
        {
            LoadScripts(ScriptsType.Spell);

            // check ids
            foreach (var script in sSpellScripts)
            {
                int spellId = script.Key & 0x00FFFFFF;
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
                if (spellInfo == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `spell_scripts` has not existing spell (Id: {spellId}) as script id");
                    continue;
                }

                byte spellEffIndex = (byte)((script.Key >> 24) & 0x000000FF);
                if (spellEffIndex >= spellInfo.GetEffects().Count)
                {
                    Log.outError(LogFilter.Sql, $"Table `spell_scripts` has too high effect index {spellEffIndex} for spell (Id: {spellId}) as script id");
                    continue;
                }

                //check for correct spellEffect
                if (spellInfo.GetEffect(spellEffIndex).Effect == SpellEffectName.None || 
                    (spellInfo.GetEffect(spellEffIndex).Effect != SpellEffectName.ScriptEffect && spellInfo.GetEffect(spellEffIndex).Effect != SpellEffectName.Dummy))
                    Log.outError(LogFilter.Sql, $"Table `spell_scripts` - spell {spellId} effect {spellEffIndex} is not SPELL_EFFECT_SCRIPT_EFFECT or SPELL_EFFECT_DUMMY");
            }
        }

        void LoadEventSet()
        {
            _eventStorage.Clear();

            // Load all possible event ids from gameobjects
            foreach (var go in _gameObjectTemplateStorage)
                _eventStorage.AddRange(go.Value.GetEventScriptSet());

            // Load all possible event ids from spells
            foreach (var spellEffect in CliDB.SpellEffectStorage.Values)            
                if (spellEffect.Effect == SpellEffectName.SendEvent && spellEffect.EffectMiscValue[0] != 0)
                    _eventStorage.Add(spellEffect.EffectMiscValue[0]);

            // Load all possible event ids from taxi path nodes
            foreach (var node in CliDB.TaxiPathNodeStorage.Values)
            {
                if (node.ArrivalEventID != 0)
                    _eventStorage.Add(node.ArrivalEventID);

                if (node.DepartureEventID != 0)
                    _eventStorage.Add(node.DepartureEventID);
            }

            // Load all possible event ids from criterias
            void addCriteriaEventsToStore(List<Criteria> criteriaList)
            {
                foreach (Criteria criteria in criteriaList)
                    if (criteria.Entry.Asset != 0)
                        _eventStorage.Add(criteria.Entry.Asset);
            };

            CriteriaType[] eventCriteriaTypes = [CriteriaType.PlayerTriggerGameEvent, CriteriaType.AnyoneTriggerGameEventScenario];
            foreach (CriteriaType criteriaType in eventCriteriaTypes)
            {
                addCriteriaEventsToStore(Global.CriteriaMgr.GetPlayerCriteriaByType(criteriaType, 0));
                addCriteriaEventsToStore(Global.CriteriaMgr.GetGuildCriteriaByType(criteriaType));
                addCriteriaEventsToStore(Global.CriteriaMgr.GetQuestObjectiveCriteriaByType(criteriaType));
            }

            foreach (ScenarioRecord scenario in CliDB.ScenarioStorage.Values)
                foreach (CriteriaType criteriaType in eventCriteriaTypes)
                    addCriteriaEventsToStore(Global.CriteriaMgr.GetScenarioCriteriaByTypeAndScenario(criteriaType, scenario.Id));

            foreach (var (gameEventId, _) in Global.CriteriaMgr.GetCriteriaByStartEvent(CriteriaStartEvent.SendEvent))
                if (gameEventId != 0)
                    _eventStorage.Add(gameEventId);

            foreach (var (gameEventId, _) in Global.CriteriaMgr.GetCriteriaByFailEvent(CriteriaFailEvent.SendEvent))
                if (gameEventId != 0)
                    _eventStorage.Add(gameEventId);
        }

        public void LoadEventScripts()
        {
            // Set of valid events referenced in several sources
            LoadEventSet();

            // Deprecated
            LoadScripts(ScriptsType.Event);

            // Then check if all scripts are in above list of possible script entries
            foreach (var script in sEventScripts)
            {
                if (!IsValidEvent(script.Key))
                    Log.outError(LogFilter.Sql, $"Table `event_scripts` has script (Id: {script.Key}) not referring to any gameobject_template " +
                        $"(data field referencing GameEvent), any taxi path node, any criteria asset or any spell effect {SpellEffectName.SendEvent}.");
            }

            uint oldMSTime = Time.GetMSTime();

            _eventScriptStorage.Clear(); // Reload case

            SQLResult result = DB.World.Query("SELECT Id, ScriptName FROM event_script_names");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 event scripts. DB table `event_script_names` is empty.");
                return;
            }

            do
            {
                int eventId = result.Read<int>(0);
                string scriptName = result.Read<string>(1);

                if (!IsValidEvent(eventId))
                {
                    Log.outError(LogFilter.Sql, $"Event (ID: {eventId}) not referring to any gameobject_template (data field referencing GameEvent), " +
                        $"any taxi path node, any criteria asset or any spell effect {SpellEffectName.SendEvent}.");
                    continue;
                }
                _eventScriptStorage[eventId] = GetScriptId(scriptName);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_eventScriptStorage.Count} event scripts in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        //Load WP Scripts
        public void LoadSpellScriptNames()
        {
            uint oldMSTime = Time.GetMSTime();

            spellScriptsStorage.Clear();                            // need for reload case

            SQLResult result = DB.World.Query("SELECT spell_id, ScriptName FROM spell_script_names");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spell script names. DB table `spell_script_names` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                int spellId = result.Read<int>(0);
                string scriptName = result.Read<string>(1);

                bool allRanks = false;
                if (spellId < 0)
                {
                    allRanks = true;
                    spellId = -spellId;
                }

                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
                if (spellInfo == null)
                {
                    Log.outError(LogFilter.Sql, $"Scriptname: `{scriptName}` spell (Id: {spellId}) does not exist.");
                    continue;
                }

                if (allRanks)
                {
                    if (!spellInfo.IsRanked())
                        Log.outError(LogFilter.Sql, $"Scriptname: `{scriptName}` spell (Id: {spellId}) has no ranks of spell.");

                    if (spellInfo.GetFirstRankSpell().Id != spellId)
                    {
                        Log.outError(LogFilter.Sql, $"Scriptname: `{scriptName}` spell (Id: {spellId}) is not first rank of spell.");
                        continue;
                    }
                    while (spellInfo != null)
                    {
                        spellScriptsStorage.Add(spellInfo.Id, GetScriptId(scriptName));
                        spellInfo = spellInfo.GetNextRankSpell();
                    }
                }
                else
                {
                    if (spellInfo.IsRanked())
                        Log.outError(LogFilter.Sql, $"Scriptname: `{scriptName}` spell (Id: {spellId}) is ranked spell. Perhaps not all ranks are assigned to this script.");

                    spellScriptsStorage.Add(spellInfo.Id, GetScriptId(scriptName));
                }

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} spell script names in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }

        public void ValidateSpellScripts()
        {
            uint oldMSTime = Time.GetMSTime();

            if (spellScriptsStorage.Empty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Validated 0 scripts.");
                return;
            }

            uint count = 0;

            foreach (var script in spellScriptsStorage.KeyValueList)
            {
                SpellInfo spellEntry = Global.SpellMgr.GetSpellInfo(script.Key, Difficulty.None);

                var SpellScriptLoaders = Global.ScriptMgr.CreateSpellScriptLoaders(script.Key);
                foreach (var pair in SpellScriptLoaders)
                {
                    SpellScript spellScript = pair.Key.GetSpellScript();
                    bool valid = true;

                    if (spellScript == null)
                    {
                        Log.outError(LogFilter.Scripts, $"Functions GetSpellScript() of script `{GetScriptName(pair.Value)}` do not return object - script skipped.");
                        valid = false;
                    }

                    if (spellScript != null)
                    {
                        spellScript._Init(pair.Key.GetName(), spellEntry.Id);
                        spellScript._Register();
                        if (!spellScript._Validate(spellEntry))
                            valid = false;
                    }

                    if (!valid)
                        spellScriptsStorage.Remove(script);
                }

                var AuraScriptLoaders = Global.ScriptMgr.CreateAuraScriptLoaders(script.Key);
                foreach (var pair in AuraScriptLoaders)
                {
                    AuraScript auraScript = pair.Key.GetAuraScript();
                    bool valid = true;

                    if (auraScript == null)
                    {
                        Log.outError(LogFilter.Scripts, $"Functions GetAuraScript() of script `{GetScriptName(pair.Value)}` do not return object - script skipped.");
                        valid = false;
                    }

                    if (auraScript != null)
                    {
                        auraScript._Init(pair.Key.GetName(), spellEntry.Id);
                        auraScript._Register();
                        if (!auraScript._Validate(spellEntry))
                            valid = false;
                    }

                    if (!valid)
                        spellScriptsStorage.Remove(script);
                }
                ++count;
            }

            Log.outInfo(LogFilter.ServerLoading, $"Validated {count} scripts in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public bool IsValidEvent(int eventId)
        {
            return _eventStorage.Contains(eventId);
        }

        public List<int> GetSpellScriptsBounds(int spellId)
        {
            return spellScriptsStorage.LookupByKey(spellId);
        }

        public int GetEventScriptId(int eventId)
        {
            return _eventScriptStorage.LookupByKey(eventId);
        }

        public List<string> GetAllDBScriptNames()
        {
            return _scriptNamesStorage.GetAllDBScriptNames();
        }

        public string GetScriptName(int id)
        {
            var entry = _scriptNamesStorage.Find(id);
            if (entry != null)
                return entry.Name;

            return string.Empty;
        }

        bool IsScriptDatabaseBound(int id)
        {
            var entry = _scriptNamesStorage.Find(id);
            if (entry != null)
                return entry.IsScriptDatabaseBound;

            return false;
        }

        public int GetScriptId(string name, bool isDatabaseBound = true)
        {
            return _scriptNamesStorage.Insert(name, isDatabaseBound);
        }

        public int GetAreaTriggerScriptId(int triggerid)
        {
            return areaTriggerScriptStorage.LookupByKey(triggerid);
        }

        public Dictionary<int, MultiMap<int, ScriptInfo>> GetScriptsMapByType(ScriptsType type)
        {
            switch (type)
            {
                case ScriptsType.Spell:
                    return sSpellScripts;
                case ScriptsType.Event:
                    return sEventScripts;
                default:
                    return null;
            }
        }

        public string GetScriptsTableNameByType(ScriptsType type)
        {
            switch (type)
            {
                case ScriptsType.Spell:
                    return "spell_scripts";
                case ScriptsType.Event:
                    return "event_scripts";
                default:
                    return "";
            }
        }

        //Creatures
        public void LoadCreatureTemplates()
        {
            var time = Time.GetMSTime();

            PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.SEL_CREATURE_TEMPLATE);
            stmt.AddValue(0, 0);
            stmt.AddValue(1, 1);

            {
                SQLResult result = DB.World.Query(stmt);
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creatures. DB table `creature_template` is empty.");
                    return;
                }

                do
                {
                    LoadCreatureTemplate(result.GetFields());
                } while (result.NextRow());
            }

            LoadCreatureTemplateResistances();
            LoadCreatureTemplateSpells();

            // We load the creature models after loading but before checking
            LoadCreatureTemplateModels();

            // Checking needs to be done after loading because of the difficulty self referencing
            foreach (var template in creatureTemplateStorage.Values)
                CheckCreatureTemplate(template);

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {creatureTemplateStorage.Count} creature definitions in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public void LoadCreatureTemplate(SQLFields fields)
        {
            int entry = fields.Read<int>(0);

            CreatureTemplate creature = new();
            creature.Entry = entry;

            for (var i = 0; i < 2; ++i)
                creature.KillCredit[i] = fields.Read<int>(1 + i);

            creature.Name = fields.Read<string>(3);
            creature.FemaleName = fields.Read<string>(4);
            creature.SubName = fields.Read<string>(5);
            creature.TitleAlt = fields.Read<string>(6);
            creature.IconName = fields.Read<string>(7);
            creature.RequiredExpansion = (Expansion)fields.Read<int>(8);
            creature.VignetteID = fields.Read<int>(9);
            creature.Faction = fields.Read<int>(10);
            creature.Npcflag = (NPCFlags1)(fields.Read<ulong>(11) & 0xFFFFFFFF);
            creature.Npcflag2 = (NPCFlags2)((fields.Read<ulong>(11) >> 32) & 0xFFFFFFFF);
            creature.SpeedWalk = fields.Read<float>(12);
            creature.SpeedRun = fields.Read<float>(13);
            creature.Scale = fields.Read<float>(14);
            creature.Classification = (CreatureClassifications)fields.Read<int>(15);
            creature.DmgSchool = (SpellSchools)fields.Read<int>(16);
            creature.BaseAttackTime = fields.Read<uint>(17);
            creature.RangeAttackTime = fields.Read<uint>(18);
            creature.BaseVariance = fields.Read<float>(19);
            creature.RangeVariance = fields.Read<float>(20);
            creature.UnitClass = (Class)fields.Read<int>(21);
            creature.UnitFlags = (UnitFlags)fields.Read<uint>(22);
            creature.UnitFlags2 = (UnitFlags2)fields.Read<uint>(23);
            creature.UnitFlags3 = (UnitFlags3)fields.Read<uint>(24);
            creature.Family = (CreatureFamily)fields.Read<uint>(25);
            creature.TrainerClass = (Class)fields.Read<byte>(26);
            creature.CreatureType = (CreatureType)fields.Read<byte>(27);
            creature.PetSpellDataId = fields.Read<int>(28);

            for (var i = (int)SpellSchools.Holy; i < (int)SpellSchools.Max; ++i)
                creature.Resistance[i] = 0;

            for (var i = 0; i < SharedConst.MaxCreatureSpells; ++i)
                creature.Spells[i] = 0;

            creature.VehicleId = fields.Read<int>(29);
            creature.AIName = fields.Read<string>(30);
            creature.MovementType = (MovementGeneratorType)fields.Read<int>(31);

            if (!fields.IsNull(32))
                creature.Movement.Ground = (CreatureGroundMovementType)fields.Read<byte>(32);

            if (!fields.IsNull(33))
                creature.Movement.Swim = fields.Read<bool>(33);

            if (!fields.IsNull(34))
                creature.Movement.Flight = (CreatureFlightMovementType)fields.Read<byte>(34);

            if (!fields.IsNull(35))
                creature.Movement.Rooted = fields.Read<bool>(35);

            if (!fields.IsNull(36))
                creature.Movement.Chase = (CreatureChaseMovementType)fields.Read<byte>(36);

            if (!fields.IsNull(37))
                creature.Movement.Random = (CreatureRandomMovementType)fields.Read<byte>(37);

            if (!fields.IsNull(38))
                creature.Movement.InteractionPauseTimer = fields.Read<uint>(38);

            creature.ModExperience = fields.Read<float>(39);
            creature.Civilian = fields.Read<bool>(40);
            creature.RacialLeader = fields.Read<bool>(41);
            creature.MovementId = fields.Read<int>(42);
            creature.WidgetSetID = fields.Read<int>(43);
            creature.WidgetSetUnitConditionID = fields.Read<int>(44);
            creature.RegenHealth = fields.Read<bool>(45);
            creature.MechanicImmuneMask = fields.Read<ulong>(46);
            creature.SpellSchoolImmuneMask = (SpellSchoolMask)fields.Read<int>(47);
            creature.FlagsExtra = (CreatureFlagsExtra)fields.Read<uint>(48);
            creature.ScriptID = GetScriptId(fields.Read<string>(49));
            creature.StringId = fields.Read<string>(50);

            creatureTemplateStorage[entry] = creature;
        }

        public void LoadCreatureTemplateGossip()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0           1
            SQLResult result = DB.World.Query("SELECT CreatureID, MenuID FROM creature_template_gossip");
            if (result == null)
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template gossip definitions. DB table `creature_template_gossip` is empty.");
                return;
            }

            uint count = 0;

            do
            {
                int creatureID = result.Read<int>(0);
                int menuID = result.Read<int>(1);

                var creatureTemplate = creatureTemplateStorage.LookupByKey(creatureID);
                if (creatureTemplate == null)
                {
                    Log.outError(LogFilter.Sql, $"creature_template_gossip has gossip definitions for creature {creatureID} but this creature doesn't exist.");
                    continue;
                }

                var menuBounds = GetGossipMenusMapBounds(menuID);
                if (menuBounds.Empty())
                {
                    Log.outError(LogFilter.Sql, $"creature_template_gossip has gossip definitions for menu id {menuID} but this menu doesn't exist.");
                    continue;
                }

                creatureTemplate.GossipMenuIds.Add(menuID);

                ++count;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature template gossip menus in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        void LoadCreatureTemplateResistances()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0           1       2
            SQLResult result = DB.World.Query("SELECT CreatureID, School, Resistance FROM creature_template_resistance");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template resistance definitions. DB table `creature_template_resistance` is empty.");
                return;
            }

            uint count = 0;

            do
            {
                int creatureID = result.Read<int>(0);
                SpellSchools school = (SpellSchools)result.Read<byte>(1);
                if (school == SpellSchools.Normal || school >= SpellSchools.Max)
                {
                    Log.outError(LogFilter.Sql, $"creature_template_resistance has resistance definitions for creature {creatureID} but this school {school} doesn't exist.");
                    continue;
                }

                if (creatureTemplateStorage.TryGetValue(creatureID, out CreatureTemplate creatureTemplate))
                {
                    Log.outError(LogFilter.Sql, $"creature_template_resistance has resistance definitions for creature {creatureID} but this creature doesn't exist.");
                    continue;
                }

                creatureTemplate.Resistance[(int)school] = result.Read<short>(2);

                ++count;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature template resistances in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        void LoadCreatureTemplateSpells()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0           1       2
            SQLResult result = DB.World.Query("SELECT CreatureID, `Index`, Spell FROM creature_template_spell");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template spell definitions. DB table `creature_template_spell` is empty.");
                return;
            }

            uint count = 0;

            do
            {
                int creatureID = result.Read<int>(0);
                byte index = result.Read<byte>(1);

                if (index >= SharedConst.MaxCreatureSpells)
                {
                    Log.outError(LogFilter.Sql, $"creature_template_spell has spell definitions for creature {creatureID} with a incorrect index {index}.");
                    continue;
                }

                if (creatureTemplateStorage.TryGetValue(creatureID, out CreatureTemplate creatureTemplate))
                {
                    Log.outError(LogFilter.Sql, $"creature_template_spell has spell definitions for creature {creatureID} but this creature doesn't exist.");
                    continue;
                }

                creatureTemplate.Spells[index] = result.Read<int>(2);

                ++count;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature template spells in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        void LoadCreatureTemplateModels()
        {
            uint oldMSTime = Time.GetMSTime();
            //                                         0           1                  2             3
            SQLResult result = DB.World.Query("SELECT CreatureID, CreatureDisplayID, DisplayScale, Probability FROM creature_template_model ORDER BY Idx ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template model definitions. DB table `creature_template_model` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int creatureId = result.Read<int>(0);
                int creatureDisplayId = result.Read<int>(1);
                float displayScale = result.Read<float>(2);
                float probability = result.Read<float>(3);

                CreatureTemplate cInfo = GetCreatureTemplate(creatureId);
                if (cInfo == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature template (Entry: {creatureId}) does not exist but has a record in `creature_template_model`.");
                    continue;
                }

                CreatureDisplayInfoRecord displayEntry = CliDB.CreatureDisplayInfoStorage.LookupByKey(creatureDisplayId);
                if (displayEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature (Entry: {creatureId}) lists non-existing CreatureDisplayID id ({creatureDisplayId}), this can crash the client.");
                    continue;
                }

                CreatureModelInfo modelInfo = GetCreatureModelInfo(creatureDisplayId);
                if (modelInfo == null)
                    Log.outError(LogFilter.Sql, $"No model data exist for `CreatureDisplayID` = {creatureDisplayId} listed by creature (Entry: {creatureId}).");

                if (displayScale <= 0.0f)
                    displayScale = 1.0f;

                cInfo.Models.Add(new CreatureModel(creatureDisplayId, displayScale, probability));
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature template models in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }
        
        public void LoadCreatureSummonedData()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                              0           1                            2                     3                     4
            SQLResult result = DB.World.Query("SELECT CreatureID, CreatureIDVisibleToSummoner, GroundMountDisplayID, FlyingMountDisplayID, DespawnOnQuestsRemoved FROM creature_summoned_data");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature summoned data definitions. DB table `creature_summoned_data` is empty.");
                return;
            }

            do
            {
                int creatureId = result.Read<int>(0);
                if (GetCreatureTemplate(creatureId) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature_summoned_data` references non-existing creature {creatureId}, skipped.");
                    continue;
                }

                if (!creatureSummonedDataStorage.ContainsKey(creatureId))
                    creatureSummonedDataStorage[creatureId] = new();

                CreatureSummonedData summonedData = creatureSummonedDataStorage[creatureId];

                if (!result.IsNull(1))
                {
                    summonedData.CreatureIDVisibleToSummoner = result.Read<int>(1);
                    if (GetCreatureTemplate(summonedData.CreatureIDVisibleToSummoner.Value) == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `creature_summoned_data` references non-existing creature {summonedData.CreatureIDVisibleToSummoner.Value} in CreatureIDVisibleToSummoner for creature {creatureId}, set to 0.");
                        summonedData.CreatureIDVisibleToSummoner = null;
                    }
                }

                if (!result.IsNull(2))
                {
                    summonedData.GroundMountDisplayID = result.Read<int>(2);
                    if (!CliDB.CreatureDisplayInfoStorage.ContainsKey(summonedData.GroundMountDisplayID.Value))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Table `creature_summoned_data` references non-existing display id {summonedData.GroundMountDisplayID.Value} in GroundMountDisplayID for creature {creatureId}, set to 0.");
                        summonedData.CreatureIDVisibleToSummoner = null;
                    }
                }

                if (!result.IsNull(3))
                {
                    summonedData.FlyingMountDisplayID = result.Read<int>(3);
                    if (!CliDB.CreatureDisplayInfoStorage.ContainsKey(summonedData.FlyingMountDisplayID.Value))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Table `creature_summoned_data` references non-existing display id {summonedData.FlyingMountDisplayID.Value} in FlyingMountDisplayID for creature {creatureId}, set to 0.");
                        summonedData.GroundMountDisplayID = null;
                    }
                }

                if (!result.IsNull(4))
                {
                    List<int> questList = new();
                    foreach (string questStr in result.Read<string>(4).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!int.TryParse(questStr, out int questId))
                            continue;

                        Quest quest = GetQuestTemplate(questId);
                        if (quest == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `creature_summoned_data` references non-existing quest {questId} in DespawnOnQuestsRemoved for creature {creatureId}, skipping.");
                            continue;
                        }

                        questList.Add(questId);
                    }

                    if (!questList.Empty())
                        summonedData.DespawnOnQuestsRemoved = questList;
                }

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {creatureSummonedDataStorage.Count} creature summoned data definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadCreatureTemplateAddons()
        {
            var time = Time.GetMSTime();
            //                                           0      1       2      3           4         5         6            7         8      9          10               11            12                      13
            SQLResult result = DB.World.Query("SELECT entry, PathId, mount, StandState, AnimTier, VisFlags, SheathState, PvPFlags, emote, aiAnimKit, movementAnimKit, meleeAnimKit, visibilityDistanceType, auras FROM creature_template_addon");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template addon definitions. DB table `creature_template_addon` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int entry = result.Read<int>(0);
                if (GetCreatureTemplate(entry) == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature template (Entry: {entry}) does not exist but has a record in `creature_template_addon`.");
                    continue;
                }

                CreatureAddon creatureAddon = new();
                creatureAddon.PathId = result.Read<int>(1);
                creatureAddon.mount = result.Read<int>(2);
                creatureAddon.standState = (UnitStandStateType)result.Read<byte>(3);
                creatureAddon.animTier = (AnimTier)result.Read<byte>(4);
                creatureAddon.visFlags = result.Read<byte>(5);
                creatureAddon.sheathState = (SheathState)result.Read<byte>(6);
                creatureAddon.pvpFlags = result.Read<byte>(7);
                creatureAddon.emote = result.Read<int>(8);
                creatureAddon.aiAnimKit = result.Read<ushort>(9);
                creatureAddon.movementAnimKit = result.Read<ushort>(10);
                creatureAddon.meleeAnimKit = result.Read<ushort>(11);
                creatureAddon.visibilityDistanceType = (VisibilityDistanceType)result.Read<byte>(12);

                var tokens = new StringArray(result.Read<string>(13), ' ');
                for (var c = 0; c < tokens.Length; ++c)
                {
                    string id = tokens[c].Trim().Replace(",", "");
                    if (!int.TryParse(id, out int spellId))
                        continue;

                    SpellInfo AdditionalSpellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
                    if (AdditionalSpellInfo == null)
                    {
                        Log.outError(LogFilter.Sql, $"Creature (Entry: {entry}) has wrong spell {spellId} defined in `auras` field in `creature_template_addon`.");
                        continue;
                    }

                    if (AdditionalSpellInfo.HasAura(AuraType.ControlVehicle))
                        Log.outError(LogFilter.Sql, 
                            $"Creature (Entry: {entry}) has SPELL_AURA_CONTROL_VEHICLE aura {spellId} defined in `auras` field in `creature_template_addon`.");

                    if (creatureAddon.auras.Contains(spellId))
                    {
                        Log.outError(LogFilter.Sql, $"Creature (Entry: {entry}) has duplicate aura (spell {spellId}) in `auras` field in `creature_template_addon`.");
                        continue;
                    }

                    if (AdditionalSpellInfo.GetDuration() > 0)
                    {
                        Log.outError(LogFilter.Sql, $"Creature (Entry: {entry}) has temporary aura (spell {spellId}) in `auras` field in `creature_template_addon`.");
                        continue;
                    }

                    creatureAddon.auras.Add(spellId);
                }

                if (creatureAddon.mount != 0)
                {
                    if (CliDB.CreatureDisplayInfoStorage.LookupByKey(creatureAddon.mount) == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Creature (Entry: {entry}) has invalid displayInfoId ({creatureAddon.mount}) for mount defined in `creature_template_addon`");
                        creatureAddon.mount = 0;
                    }
                }

                if (creatureAddon.standState >= UnitStandStateType.Max)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Creature (Entry: {entry}) has invalid unit stand state ({creatureAddon.standState}) defined in `creature_template_addon`. Truncated to 0.");
                    creatureAddon.standState = 0;
                }

                if (creatureAddon.animTier >= AnimTier.Max)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Creature (Entry: {entry}) has invalid animation tier ({creatureAddon.animTier}) defined in `creature_template_addon`. Truncated to 0.");
                    creatureAddon.animTier = 0;
                }

                if (creatureAddon.sheathState >= SheathState.Max)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Creature (Entry: {entry}) has invalid sheath state ({creatureAddon.sheathState}) defined in `creature_template_addon`. Truncated to 0.");
                    creatureAddon.sheathState = 0;
                }

                // PvPFlags don't need any checking for the time being since they cover the entire range of a byte

                if (!CliDB.EmotesStorage.ContainsKey(creatureAddon.emote))
                {
                    Log.outError(LogFilter.Sql, $"Creature (Entry: {entry}) has invalid emote ({creatureAddon.emote}) defined in `creatureaddon`.");
                    creatureAddon.emote = 0;
                }

                if (creatureAddon.aiAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.aiAnimKit))
                {
                    Log.outError(LogFilter.Sql, $"Creature (Entry: {entry}) has invalid aiAnimKit ({creatureAddon.aiAnimKit}) defined in `creature_template_addon`.");
                    creatureAddon.aiAnimKit = 0;
                }

                if (creatureAddon.movementAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.movementAnimKit))
                {
                    Log.outError(LogFilter.Sql, $"Creature (Entry: {entry}) has invalid movementAnimKit ({creatureAddon.movementAnimKit}) defined in `creature_template_addon`.");
                    creatureAddon.movementAnimKit = 0;
                }

                if (creatureAddon.meleeAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.meleeAnimKit))
                {
                    Log.outError(LogFilter.Sql, $"Creature (Entry: {entry}) has invalid meleeAnimKit ({creatureAddon.meleeAnimKit}) defined in `creature_template_addon`.");
                    creatureAddon.meleeAnimKit = 0;
                }

                if (creatureAddon.visibilityDistanceType >= VisibilityDistanceType.Max)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Creature (Entry: {entry}) has invalid visibilityDistanceType ({creatureAddon.visibilityDistanceType}) defined in `creature_template_addon`.");
                    creatureAddon.visibilityDistanceType = VisibilityDistanceType.Normal;
                }

                creatureTemplateAddonStorage.Add(entry, creatureAddon);
                count++;
            }
            while (result.NextRow());
            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature template addons in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public void LoadCreatureAddons()
        {
            var time = Time.GetMSTime();
            //                                         0     1       2      3           4         5         6            7         8      9          10               11            12                      13
            SQLResult result = DB.World.Query("SELECT guid, PathId, mount, StandState, AnimTier, VisFlags, SheathState, PvPFlags, emote, aiAnimKit, movementAnimKit, meleeAnimKit, visibilityDistanceType, auras FROM creature_addon");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature addon definitions. DB table `creature_addon` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                long guid = result.Read<long>(0);
                CreatureData creData = GetCreatureData(guid);
                if (creData == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) does not exist but has a record in `creatureaddon`.");
                    continue;
                }

                CreatureAddon creatureAddon = new();

                creatureAddon.PathId = result.Read<int>(1);
                if (creData.movementType == MovementGeneratorType.Waypoint && creatureAddon.PathId == 0)
                {
                    creData.movementType = MovementGeneratorType.Idle;
                    Log.outError(LogFilter.Sql, $"Creature (GUID {guid}) has movement type set to WAYPOINTMOTIONTYPE but no path assigned.");
                }

                creatureAddon.mount = result.Read<int>(2);
                creatureAddon.standState = (UnitStandStateType)result.Read<byte>(3);
                creatureAddon.animTier = (AnimTier)result.Read<byte>(4);
                creatureAddon.visFlags = result.Read<byte>(5);
                creatureAddon.sheathState = (SheathState)result.Read<byte>(6);
                creatureAddon.pvpFlags = result.Read<byte>(7);
                creatureAddon.emote = result.Read<int>(8);
                creatureAddon.aiAnimKit = result.Read<ushort>(9);
                creatureAddon.movementAnimKit = result.Read<ushort>(10);
                creatureAddon.meleeAnimKit = result.Read<ushort>(11);
                creatureAddon.visibilityDistanceType = (VisibilityDistanceType)result.Read<byte>(12);

                var tokens = new StringArray(result.Read<string>(13), ' ');
                for (var c = 0; c < tokens.Length; ++c)
                {
                    string id = tokens[c].Trim().Replace(",", "");
                    if (!int.TryParse(id, out int spellId))
                        continue;

                    SpellInfo AdditionalSpellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
                    if (AdditionalSpellInfo == null)
                    {
                        Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has wrong spell {spellId} defined in `auras` field in `creatureaddon`.");
                        continue;
                    }

                    if (AdditionalSpellInfo.HasAura(AuraType.ControlVehicle))
                        Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has SPELL_AURA_CONTROL_VEHICLE aura {spellId} defined in `auras` field in `creature_addon`.");

                    if (creatureAddon.auras.Contains(spellId))
                    {
                        Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has duplicate aura (spell {spellId}) in `auras` field in `creature_addon`.");
                        continue;
                    }

                    if (AdditionalSpellInfo.GetDuration() > 0)
                    {
                        Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has temporary aura (spell {spellId}) in `auras` field in `creature_addon`.");
                        continue;
                    }

                    creatureAddon.auras.Add(spellId);
                }

                if (creatureAddon.mount != 0)
                {
                    if (!CliDB.CreatureDisplayInfoStorage.ContainsKey(creatureAddon.mount))
                    {
                        Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has invalid displayInfoId ({creatureAddon.mount}) for mount defined in `creatureaddon`.");
                        creatureAddon.mount = 0;
                    }
                }

                if (creatureAddon.standState >= UnitStandStateType.Max)
                {
                    Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has invalid unit stand state ({creatureAddon.standState}) defined in `creature_addon`. Truncated to 0.");
                    creatureAddon.standState = 0;
                }

                if (creatureAddon.animTier >= AnimTier.Max)
                {
                    Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has invalid animation tier ({creatureAddon.animTier}) defined in `creature_addon`. Truncated to 0.");
                    creatureAddon.animTier = 0;
                }

                if (creatureAddon.sheathState >= SheathState.Max)
                {
                    Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has invalid sheath state ({creatureAddon.sheathState}) defined in `creature_addon`. Truncated to 0.");
                    creatureAddon.sheathState = 0;
                }

                // PvPFlags don't need any checking for the time being since they cover the entire range of a byte

                if (!CliDB.EmotesStorage.ContainsKey(creatureAddon.emote))
                {
                    Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has invalid emote ({creatureAddon.emote}) defined in `creatureaddon`.");
                    creatureAddon.emote = 0;
                }

                if (creatureAddon.aiAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.aiAnimKit))
                {
                    Log.outError(LogFilter.Sql, $"Creature (Guid: {guid}) has invalid aiAnimKit ({creatureAddon.aiAnimKit}) defined in `creature_addon`.");
                    creatureAddon.aiAnimKit = 0;
                }

                if (creatureAddon.movementAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.movementAnimKit))
                {
                    Log.outError(LogFilter.Sql, $"Creature (Guid: {guid}) has invalid movementAnimKit ({creatureAddon.movementAnimKit}) defined in `creature_addon`.");
                    creatureAddon.movementAnimKit = 0;
                }

                if (creatureAddon.meleeAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(creatureAddon.meleeAnimKit))
                {
                    Log.outError(LogFilter.Sql, $"Creature (Guid: {guid}) has invalid meleeAnimKit ({creatureAddon.meleeAnimKit}) defined in `creature_addon`.");
                    creatureAddon.meleeAnimKit = 0;
                }

                if (creatureAddon.visibilityDistanceType >= VisibilityDistanceType.Max)
                {
                    Log.outError(LogFilter.Sql, $"Creature (GUID: {guid}) has invalid visibilityDistanceType ({creatureAddon.visibilityDistanceType}) defined in `creature_addon`.");
                    creatureAddon.visibilityDistanceType = VisibilityDistanceType.Normal;
                }

                creatureAddonStorage.Add(guid, creatureAddon);
                count++;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature addons in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public void LoadCreatureQuestItems()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                          0              1             2      3
            SQLResult result = DB.World.Query("SELECT CreatureEntry, DifficultyID, ItemId, Idx FROM creature_questitem ORDER BY Idx ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature quest items. DB table `creature_questitem` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int entry = result.Read<int>(0);
                Difficulty difficulty = (Difficulty)result.Read<byte>(1);
                int item = result.Read<int>(2);
                int idx = result.Read<int>(3);

                if (creatureTemplateStorage.ContainsKey(entry))
                {
                    Log.outError(LogFilter.Sql,
                        $"Table `creature_questitem` has data for nonexistent creature (entry: {entry}, difficulty: {difficulty} idx: {idx}), skipped.");
                    continue;
                }

                if (!CliDB.ItemStorage.ContainsKey(item))
                {
                    Log.outError(LogFilter.Sql,
                        $"Table `creature_questitem` has nonexistent item (ID: {item}) in creature (entry: {entry}, difficulty: {difficulty} idx: {idx}), skipped.");
                    continue;
                }

                creatureQuestItemStorage.Add((entry, difficulty), item);

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature quest items in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadEquipmentTemplates()
        {
            var time = Time.GetMSTime();
            //                                                0   1        2                 3            4
            SQLResult result = DB.World.Query("SELECT CreatureID, ID, ItemID1, AppearanceModID1, ItemVisual1, " +
                //5                 6            7       8                 9           10
                "ItemID2, AppearanceModID2, ItemVisual2, ItemID3, AppearanceModID3, ItemVisual3 " +
                "FROM creature_equip_template");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature equipment templates. DB table `creature_equip_template` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                int entry = result.Read<int>(0);

                if (GetCreatureTemplate(entry) == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature template (CreatureID: {entry}) does not exist but has a record in `creature_equip_template`");
                    continue;
                }

                int id = result.Read<int>(1);

                EquipmentInfo equipmentInfo = new();

                for (var i = 0; i < SharedConst.MaxEquipmentItems; ++i)
                {
                    equipmentInfo.Items[i].ItemId = result.Read<int>(2 + i * 3);
                    equipmentInfo.Items[i].AppearanceModId = result.Read<ushort>(3 + i * 3);
                    equipmentInfo.Items[i].ItemVisual = result.Read<ushort>(4 + i * 3);

                    if (equipmentInfo.Items[i].ItemId == 0)
                        continue;

                    var dbcItem = CliDB.ItemStorage.LookupByKey(equipmentInfo.Items[i].ItemId);
                    if (dbcItem == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Unknown item (ID: {equipmentInfo.Items[i].ItemId}) in creature_equip_template.ItemID{i + 1} for CreatureID  = {entry}, forced to 0.");
                        equipmentInfo.Items[i].ItemId = 0;
                        continue;
                    }

                    if (Global.DB2Mgr.GetItemModifiedAppearance(equipmentInfo.Items[i].ItemId, equipmentInfo.Items[i].AppearanceModId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Unknown item appearance for (ID: {equipmentInfo.Items[i].ItemId}, " +
                            $"AppearanceModID: {equipmentInfo.Items[i].AppearanceModId}) " +
                            $"pair in creature_equip_template.ItemID{i + 1} creature_equip_template.AppearanceModID{i + 1} " +
                            $"for CreatureID: {entry} and ID: {id}, forced to default.");

                        ItemModifiedAppearanceRecord defaultAppearance = Global.DB2Mgr.GetDefaultItemModifiedAppearance(equipmentInfo.Items[i].ItemId);
                        if (defaultAppearance != null)
                            equipmentInfo.Items[i].AppearanceModId = (ushort)defaultAppearance.ItemAppearanceModifierID;
                        else
                            equipmentInfo.Items[i].AppearanceModId = 0;
                        continue;
                    }

                    if (dbcItem.InventoryType != InventoryType.Weapon &&
                        dbcItem.InventoryType != InventoryType.Shield &&
                        dbcItem.InventoryType != InventoryType.Ranged &&
                        dbcItem.InventoryType != InventoryType.Weapon2Hand &&
                        dbcItem.InventoryType != InventoryType.WeaponMainhand &&
                        dbcItem.InventoryType != InventoryType.WeaponOffhand &&
                        dbcItem.InventoryType != InventoryType.Holdable &&
                        dbcItem.InventoryType != InventoryType.Thrown &&
                        dbcItem.InventoryType != InventoryType.RangedRight)
                    {
                        Log.outError(LogFilter.Sql, $"Item (ID {equipmentInfo.Items[i].ItemId}) in creature_equip_template. " +
                            $"ItemID{i + 1} for CreatureID  = {entry} is not equipable in a hand, forced to 0.");
                        equipmentInfo.Items[i].ItemId = 0;
                    }
                }

                equipmentInfoStorage.Add(entry, (id, equipmentInfo));
                ++count;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} equipment templates in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public void LoadCreatureMovementOverrides()
        {
            uint oldMSTime = Time.GetMSTime();

            creatureMovementOverrides.Clear();

            // Load the data from creature_movement_override and if NULL fallback to creature_template_movement
            SQLResult result = DB.World.Query(
                "SELECT cmo.SpawnId," +
                "COALESCE(cmo.Ground, ctm.Ground)," +
                "COALESCE(cmo.Swim, ctm.Swim)," +
                "COALESCE(cmo.Flight, ctm.Flight)," +
                "COALESCE(cmo.Rooted, ctm.Rooted)," +
                "COALESCE(cmo.Chase, ctm.Chase)," +
                "COALESCE(cmo.Random, ctm.Random)," +
                "COALESCE(cmo.InteractionPauseTimer, ctm.InteractionPauseTimer) " +
                "FROM creature_movement_override AS cmo " +
                "LEFT JOIN creature AS c ON c.guid = cmo.SpawnId " +
                "LEFT JOIN creature_template_movement AS ctm ON ctm.CreatureId = c.id");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature movement overrides. DB table `creature_movement_override` is empty!");
                return;
            }

            do
            {
                long spawnId = result.Read<long>(0);
                if (GetCreatureData(spawnId) == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature (GUID: {spawnId}) does not exist but has a record in `creature_movement_override`.");
                    continue;
                }

                CreatureMovementData movement = new();
                if (!result.IsNull(1))
                    movement.Ground = (CreatureGroundMovementType)result.Read<byte>(1);
                if (!result.IsNull(2))
                    movement.Swim = result.Read<bool>(2);
                if (!result.IsNull(3))
                    movement.Flight = (CreatureFlightMovementType)result.Read<byte>(3);
                if (!result.IsNull(4))
                    movement.Rooted = result.Read<bool>(4);
                if (!result.IsNull(5))
                    movement.Chase = (CreatureChaseMovementType)result.Read<byte>(5);
                if (!result.IsNull(6))
                    movement.Random = (CreatureRandomMovementType)result.Read<byte>(6);
                if (!result.IsNull(7))
                    movement.InteractionPauseTimer = result.Read<uint>(7);

                CheckCreatureMovement("creature_movement_override", spawnId, movement);

                creatureMovementOverrides[spawnId] = movement;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {creatureMovementOverrides.Count} movement overrides in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadCreatureClassLevelStats()
        {
            var time = Time.GetMSTime();

            creatureBaseStatsStorage.Clear();

            //                                        0      1      2        3        4        5         6          7            8                  9            10           11
            SQLResult result = DB.World.Query("SELECT level, class, basehp0, basehp1, basehp2, basemana, basearmor, attackpower, rangedattackpower, damage_base, damage_exp1, damage_exp2 FROM creature_classlevelstats");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature base stats. DB table `creature_classlevelstats` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                byte Level = result.Read<byte>(0);
                Class class_ = (Class)result.Read<byte>(1);

                if (class_ == Class.None || !ClassMask.Creatures.HasClass(class_))
                    Log.outError(LogFilter.Sql, $"Creature base stats for level {Level} has invalid class {class_}.");

                CreatureBaseStats stats = new();

                for (byte i = 0; i <= (byte)Expansion.Current; ++i)
                {
                    stats.BaseHealth[i] = result.Read<ushort>(2 + i);

                    if (stats.BaseHealth[i] == 0)
                    {
                        Log.outError(LogFilter.Sql, $"Creature base stats for class {class_}, level {Level} has invalid zero base HP[{i}] - set to 1.");
                        stats.BaseHealth[i] = 1;
                    }

                    stats.BaseDamage[i] = result.Read<float>(9 + i);
                    if (stats.BaseDamage[i] < 0.0f)
                    {
                        Log.outError(LogFilter.Sql, $"Creature base stats for class {class_}, level {Level} has invalid negative base damage[{i}] - set to 0.0.");
                        stats.BaseDamage[i] = 0.0f;
                    }
                }

                stats.BaseMana = result.Read<ushort>(5);
                stats.BaseArmor = result.Read<ushort>(6);
                stats.AttackPower = result.Read<ushort>(7);
                stats.RangedAttackPower = result.Read<ushort>(8);

                creatureBaseStatsStorage.Add((Level, class_), stats);

                ++count;
            } while (result.NextRow());

            for (byte unitLevel = 1; unitLevel <= SharedConst.DefaultMaxUnitLevel; ++unitLevel)
            {
                for (Class unitClass = Class.None; unitClass < Class.Max; ++unitClass)
                {
                    if (ClassMask.Creatures.HasClass(unitClass))
                    {
                        if (creatureBaseStatsStorage.ContainsKey((unitLevel, unitClass)))
                            Log.outError(LogFilter.Sql, $"Missing base stats for creature class {unitClass} level {unitLevel}.");
                    }
                }
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature base stats in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public void LoadCreatureModelInfo()
        {
            var time = Time.GetMSTime();

            //                                        0          1               2            3
            SQLResult result = DB.World.Query("SELECT DisplayID, BoundingRadius, CombatReach, DisplayID_Other_Gender FROM creature_model_info");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature model definitions. DB table `creaturemodelinfo` is empty.");
                return;
            }

            // List of model FileDataIDs that the client treats as invisible stalker
            List<int> trigggerCreatureModelFileID = [124640, 124641, 124642, 343863, 439302];

            uint count = 0;
            do
            {
                int displayId = result.Read<int>(0);

                var creatureDisplay = CliDB.CreatureDisplayInfoStorage.LookupByKey(displayId);
                if (creatureDisplay == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature_model_info` has a non-existent DisplayID (ID: {displayId}). Skipped.");
                    continue;
                }

                CreatureModelInfo modelInfo = new();
                modelInfo.BoundingRadius = result.Read<float>(1);
                modelInfo.CombatReach = result.Read<float>(2);
                modelInfo.DisplayIdOtherGender = result.Read<int>(3);
                modelInfo.gender = (Gender)creatureDisplay.Gender;

                // Checks
                if (modelInfo.gender == Gender.Unknown)
                    modelInfo.gender = Gender.Male;

                if (modelInfo.DisplayIdOtherGender != 0 && !CliDB.CreatureDisplayInfoStorage.ContainsKey(modelInfo.DisplayIdOtherGender))
                {
                    Log.outError(LogFilter.Sql, $"Table `creature_model_info` has a non-existent DisplayID_Other_Gender " +
                        $"(ID: {modelInfo.DisplayIdOtherGender}) being used by DisplayID (ID: {displayId}).");

                    modelInfo.DisplayIdOtherGender = 0;
                }

                if (modelInfo.CombatReach < 0.1f)
                    modelInfo.CombatReach = SharedConst.DefaultPlayerCombatReach;

                CreatureModelDataRecord modelData = CliDB.CreatureModelDataStorage.LookupByKey(creatureDisplay.ModelID);
                if (modelData != null)
                {
                    foreach (var id in trigggerCreatureModelFileID)
                    {
                        if (modelData.FileDataID == id)
                        {
                            modelInfo.IsTrigger = true;
                            break;
                        }
                    }                
                }

                creatureModelStorage.Add(displayId, modelInfo);
                count++;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature model based info in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public void LoadCreatureTemplateSparring()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                        0      1 
            SQLResult result = DB.World.Query("SELECT Entry, NoNPCDamageBelowHealthPct FROM creature_template_sparring");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template sparring definitions. DB table `creature_template_sparring` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int entry = result.Read<int>(0);
                float noNPCDamageBelowHealthPct = result.Read<float>(1);

                if (GetCreatureTemplate(entry) == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature template (Entry: {entry}) does not exist but has a record in `creature_template_sparring`.");
                    continue;
                }

                if (noNPCDamageBelowHealthPct <= 0 || noNPCDamageBelowHealthPct > 100)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Creature (Entry: {entry}) has invalid NoNPCDamageBelowHealthPct ({noNPCDamageBelowHealthPct}) defined in `creature_template_sparring`. Skipping.");
                    continue;
                }
                _creatureTemplateSparringStorage.Add(entry, noNPCDamageBelowHealthPct);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature template sparring rows in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadCreatureTemplateDifficulty()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0      1             2        3          4
            SQLResult result = DB.World.Query("SELECT Entry, DifficultyID, MinLevel, MaxLevel, HealthScalingExpansion, " +
                    //   5               6             7              8               9                     10         11
                    "HealthModifier, ManaModifier, ArmorModifier, DamageModifier, CreatureDifficultyID, TypeFlags, TypeFlags2, " +
                    //   12      13                14          15       16
                    "LootID, PickPocketLootID, SkinLootID, GoldMin, GoldMax," +
                    //   17            18            19            20            21            22            23            24
                    "StaticFlags1, StaticFlags2, StaticFlags3, StaticFlags4, StaticFlags5, StaticFlags6, StaticFlags7, StaticFlags8 " +
                    "FROM creature_template_difficulty ORDER BY Entry");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creature template difficulty definitions. DB table `creature_template_difficulty` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int entry = result.Read<int>(0);
                Difficulty difficulty = (Difficulty)result.Read<byte>(1);

                var template = creatureTemplateStorage.LookupByKey(entry);
                if (template == null)
                {
                    Log.outError(LogFilter.Sql, $"Creature template (Entry: {entry}) does not exist but has a record in `creature_template_difficulty`.");
                    continue;
                }

                CreatureDifficulty creatureDifficulty = new();
                creatureDifficulty.MinLevel = result.Read<byte>(2);
                creatureDifficulty.MaxLevel = result.Read<byte>(3);
                creatureDifficulty.HealthScalingExpansion = (Expansion)result.Read<int>(4);
                creatureDifficulty.HealthModifier = result.Read<float>(5);
                creatureDifficulty.ManaModifier = result.Read<float>(6);
                creatureDifficulty.ArmorModifier = result.Read<float>(7);
                creatureDifficulty.DamageModifier = result.Read<float>(8);
                creatureDifficulty.CreatureDifficultyID = result.Read<int>(9);
                creatureDifficulty.TypeFlags = (CreatureTypeFlags)result.Read<uint>(10);
                creatureDifficulty.TypeFlags2 = result.Read<uint>(11);
                creatureDifficulty.LootID = result.Read<int>(12);
                creatureDifficulty.PickPocketLootID = result.Read<int>(13);
                creatureDifficulty.SkinLootID = result.Read<int>(14);
                creatureDifficulty.GoldMin = result.Read<int>(15);
                creatureDifficulty.GoldMax = result.Read<int>(16);
                creatureDifficulty.StaticFlags = new(
                    result.Read<uint>(17),
                    result.Read<uint>(18),
                    result.Read<uint>(19),
                    result.Read<uint>(20),
                    result.Read<uint>(21),
                    result.Read<uint>(22),
                    result.Read<uint>(23),
                    result.Read<uint>(24)
                );

                // TODO: Check if this still applies
                creatureDifficulty.DamageModifier *= Creature.GetDamageMod(template.Classification);

                if (creatureDifficulty.MinLevel == 0 || creatureDifficulty.MaxLevel == 0)
                {
                    if (creatureDifficulty.MinLevel == 0)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `creature_template_difficulty` lists creature (ID: {entry}) has MinLevel set to 0 but the allowed minimum is 1. Ignored and set to 1.");
                        creatureDifficulty.MinLevel = 1;
                    }

                    if (creatureDifficulty.MaxLevel == 0)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `creature_template_difficulty` lists creature (ID: {entry}) has MaxLevel set to 0 but the allowed minimum is 1. Ignored and set to 1.");
                        creatureDifficulty.MaxLevel = 1;
                    }
                }

                if (creatureDifficulty.MinLevel > creatureDifficulty.MaxLevel)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature_template_difficulty` lists creature(ID: {entry}) with a higher " +
                        $"MinLevel({creatureDifficulty.MinLevel}) than MaxLevel({creatureDifficulty.MaxLevel}). MaxLevel will be set to MinLevel value.");
                    creatureDifficulty.MinLevel = creatureDifficulty.MaxLevel;
                }

                if (creatureDifficulty.HealthScalingExpansion < Expansion.LevelCurrent || creatureDifficulty.HealthScalingExpansion > Expansion.Current)
                {
                    Log.outError(LogFilter.Sql,$"Table `creature_template_difficulty` lists creature (ID: {entry}) with invalid `HealthScalingExpansion` " +
                        $"{creatureDifficulty.HealthScalingExpansion}. Ignored and set to 0.");
                    creatureDifficulty.HealthScalingExpansion = 0;
                }

                if (creatureDifficulty.GoldMin > creatureDifficulty.GoldMax)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature_template_difficulty` lists creature (ID: {entry}) with `GoldMin` {creatureDifficulty.GoldMin} " +
                        $"greater than `GoldMax` {creatureDifficulty.GoldMax}, setting `GoldMax` to {creatureDifficulty.GoldMin}.");
                    creatureDifficulty.GoldMax = creatureDifficulty.GoldMin;
                }

                template.difficultyStorage[difficulty] = creatureDifficulty;

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature template difficulty data in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void CheckCreatureTemplate(CreatureTemplate cInfo)
        {
            if (cInfo == null)
                return;

            if (!CliDB.FactionTemplateStorage.ContainsKey(cInfo.Faction))
            {
                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has non-existing faction template ({cInfo.Faction}). This can lead to crashes, set to faction 35.");
                cInfo.Faction = 35;
            }

            for (int k = 0; k < SharedConst.MaxCreatureKillCredit; ++k)
            {
                if (cInfo.KillCredit[k] != 0)
                {
                    if (GetCreatureTemplate(cInfo.KillCredit[k]) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) lists non-existing creature entry {cInfo.KillCredit[k]} in `KillCredit{k + 1}`.");
                        cInfo.KillCredit[k] = 0;
                    }
                }
            }

            if (cInfo.Models.Empty())
                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) does not have any existing display id in creature_template_model.");

            if (cInfo.UnitClass == Class.None || !ClassMask.Creatures.HasClass(cInfo.UnitClass))
            {
                Class default_ = Class.Warrior;

                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has invalid unit_class ({cInfo.UnitClass})[{(int)cInfo.UnitClass}] "+
                    $"in creature_template. Set to ({default_})[{(int)default_}].");
                cInfo.UnitClass = default_;
            }

            if (!SpellSchoolMask.All.HasSchool(cInfo.DmgSchool))
            {
                SpellSchools default_ = SpellSchools.Normal;

                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has invalid spell school value ({cInfo.DmgSchool}) in `dmgschool`. " +
                    $"Set to ({default_})[{(int)default_}].");
                cInfo.DmgSchool = default_;
            }

            if (cInfo.BaseAttackTime == 0)
                cInfo.BaseAttackTime = SharedConst.BaseAttackTime;

            if (cInfo.RangeAttackTime == 0)
                cInfo.RangeAttackTime = SharedConst.BaseAttackTime;

            if (cInfo.SpeedWalk == 0.0f)
            {
                float default_ = 1.0f;

                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has wrong value ({cInfo.SpeedWalk}) in speed_walk, set to {default_}.");
                cInfo.SpeedWalk = default_;
            }

            if (cInfo.SpeedRun == 0.0f)
            {
                float default_ = 1.14286f;

                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has wrong value ({cInfo.SpeedRun}) in speed_run, set to {default_}.");
                cInfo.SpeedRun = default_;
            }

            if (cInfo.CreatureType != CreatureType.None && !CliDB.CreatureTypeStorage.ContainsKey((int)cInfo.CreatureType))
            {
                CreatureType default_ = CreatureType.Humanoid;

                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has invalid creature Type ({cInfo.CreatureType})[{(int)cInfo.CreatureType}] in `Type`. " +
                     $"Set to ({default_})[{(int)default_}].");
                cInfo.CreatureType = default_;
            }

            if (cInfo.Family != CreatureFamily.None && !CliDB.CreatureFamilyStorage.ContainsKey((int)cInfo.Family))
            {
                CreatureFamily default_ = CreatureFamily.None;

                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has invalid creature family ({cInfo.Family})[{(int)cInfo.Family}] in `family`. " +
                    $"Set to ({default_})[{(int)default_}].");
                cInfo.Family = default_;
            }

            CheckCreatureMovement("creature_template_movement", cInfo.Entry, cInfo.Movement);

            if (cInfo.VehicleId != 0)
            {
                if (!CliDB.VehicleStorage.ContainsKey(cInfo.VehicleId))
                {
                    int default_ = 0;

                    Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has a non-existing VehicleId ({cInfo.VehicleId}). This *WILL* cause the client to freeze! " +
                        $"Set to {default_}.");
                    cInfo.VehicleId = default_;
                }
            }

            for (byte j = 0; j < SharedConst.MaxCreatureSpells; ++j)
            {
                if (cInfo.Spells[j] != 0 && !Global.SpellMgr.HasSpellInfo(cInfo.Spells[j], Difficulty.None))
                {
                    int default_ = 0;

                    Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has non-existing Spell{j + 1} ({cInfo.Spells[j]}), set to {default_}.");
                    cInfo.Spells[j] = default_;
                }
            }

            if (cInfo.MovementType >= MovementGeneratorType.MaxDB)
            {
                Log.outError(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has wrong movement generator Type ({cInfo.MovementType}), ignored and set to IDLE.");
                cInfo.MovementType = (uint)MovementGeneratorType.Idle;
            }

            if (cInfo.RequiredExpansion > Expansion.Max)
            {
                Expansion default_ = Expansion.Classic;

                Log.outError(LogFilter.Sql, $"Table `creature_template` lists creature (Entry: {cInfo.Entry}) with " +
                    $"`RequiredExpansion` {cInfo.RequiredExpansion} [{(int)cInfo.RequiredExpansion}]. Ignored and set to {default_} [{(int)default_}].");
                cInfo.RequiredExpansion = default_;
            }

            CreatureFlagsExtra badFlags = (cInfo.FlagsExtra & ~CreatureFlagsExtra.DBAllowed);
            if (badFlags != 0)
            {
                Log.outError(LogFilter.Sql,
                    $"Table `creature_template` lists creature (Entry: {cInfo.Entry}) with disallowed `flags_extra` {badFlags} [{(uint)badFlags}], removing incorrect flag.");
                cInfo.FlagsExtra &= CreatureFlagsExtra.DBAllowed;
            }

            UnitFlags disallowedUnitFlags = (cInfo.UnitFlags & ~UnitFlags.Allowed);
            if (disallowedUnitFlags != 0)
            {
                Log.outError(LogFilter.Sql, $"Table `creature_template` lists creature (Entry: {cInfo.Entry}) " +
                    $"with disallowed `unit_flags` {disallowedUnitFlags} [{(uint)disallowedUnitFlags}], removing incorrect flag.");
                cInfo.UnitFlags &= UnitFlags.Allowed;
            }

            UnitFlags2 disallowedUnitFlags2 = (cInfo.UnitFlags2 & ~UnitFlags2.Allowed);
            if (disallowedUnitFlags2 != 0)
            {
                Log.outError(LogFilter.Sql, $"Table `creature_template` lists creature (Entry: {cInfo.Entry}) " +
                    $"with disallowed `unit_flags2` {disallowedUnitFlags2} [{(uint)disallowedUnitFlags2}], removing incorrect flag.");
                cInfo.UnitFlags2 &= UnitFlags2.Allowed;
            }

            UnitFlags3 disallowedUnitFlags3 = (cInfo.UnitFlags3 & ~UnitFlags3.Allowed);
            if (disallowedUnitFlags3 != 0)
            {
                Log.outError(LogFilter.Sql, $"Table `creature_template` lists creature (Entry: {cInfo.Entry}) " +
                    $"with disallowed `unit_flags3` {disallowedUnitFlags3} [{(uint)disallowedUnitFlags3}], removing incorrect flag.");
                cInfo.UnitFlags3 &= UnitFlags3.Allowed;
            }

            if (!cInfo.GossipMenuIds.Empty() && !cInfo.Npcflag.HasFlag(NPCFlags1.Gossip))
                Log.outInfo(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has assigned gossip menu, but npcflag does not include UNIT_NPC_FLAG_GOSSIP.");
            else if (cInfo.GossipMenuIds.Empty() && cInfo.Npcflag.HasFlag(NPCFlags1.Gossip))
                Log.outInfo(LogFilter.Sql, $"Creature (Entry: {cInfo.Entry}) has npcflag UNIT_NPC_FLAG_GOSSIP, but gossip menu is unassigned.");
        }

        void CheckCreatureMovement(string table, long id, CreatureMovementData creatureMovement)
        {
            if (creatureMovement.Ground >= CreatureGroundMovementType.Max)
            {
                Log.outError(LogFilter.Sql, $"`{table}`.`Ground` wrong value ({creatureMovement.Ground}) for Id {id}, setting to Run.");
                creatureMovement.Ground = CreatureGroundMovementType.Run;
            }

            if (creatureMovement.Flight >= CreatureFlightMovementType.Max)
            {
                Log.outError(LogFilter.Sql, $"`{table}`.`Flight` wrong value ({creatureMovement.Flight}) for Id {id}, setting to None.");
                creatureMovement.Flight = CreatureFlightMovementType.None;
            }

            if (creatureMovement.Chase >= CreatureChaseMovementType.Max)
            {
                Log.outError(LogFilter.Sql, $"`{table}`.`Chase` wrong value ({creatureMovement.Chase}) for Id {id}, setting to Run.");
                creatureMovement.Chase = CreatureChaseMovementType.Run;
            }

            if (creatureMovement.Random >= CreatureRandomMovementType.Max)
            {
                Log.outError(LogFilter.Sql, $"`{table}`.`Random` wrong value ({creatureMovement.Random}) for Id {id}, setting to Walk.");
                creatureMovement.Random = CreatureRandomMovementType.Walk;
            }
        }

        public void LoadLinkedRespawn()
        {
            uint oldMSTime = Time.GetMSTime();

            linkedRespawnStorage.Clear();
            //                                          0        1          2
            SQLResult result = DB.World.Query("SELECT guid, linkedGuid, linkType FROM linked_respawn ORDER BY guid ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 linked respawns. DB table `linked_respawn` is empty.");
                return;
            }

            do
            {
                long guidLow = result.Read<long>(0);
                long linkedGuidLow = result.Read<long>(1);
                CreatureLinkedRespawnType linkType = (CreatureLinkedRespawnType)result.Read<byte>(2);

                ObjectGuid guid = ObjectGuid.Empty;
                ObjectGuid linkedGuid = ObjectGuid.Empty;
                bool error = false;
                switch (linkType)
                {
                    case CreatureLinkedRespawnType.CreatureToCreature:
                        {
                            CreatureData slave = GetCreatureData(guidLow);
                            if (slave == null)
                            {
                                Log.outError(LogFilter.Sql, $"Couldn't get creature data for GUIDLow {guidLow}.");
                                error = true;
                                break;
                            }

                            CreatureData master = GetCreatureData(linkedGuidLow);
                            if (master == null)
                            {
                                Log.outError(LogFilter.Sql, $"Couldn't get creature data for GUIDLow {linkedGuidLow}.");
                                error = true;
                                break;
                            }

                            MapRecord map = CliDB.MapStorage.LookupByKey(master.MapId);
                            if (map == null || !map.Instanceable || (master.MapId != slave.MapId))
                            {
                                Log.outError(LogFilter.Sql, $"Creature '{guidLow}' linking to '{linkedGuidLow}' on an unpermitted map.");
                                error = true;
                                break;
                            }

                            // they must have a possibility to meet (normal/heroic difficulty)
                            if (!master.SpawnDifficulties.Intersect(slave.SpawnDifficulties).Any())
                            {
                                Log.outError(LogFilter.Sql, $"LinkedRespawn: Creature '{guidLow}' linking to '{linkedGuidLow}' with not corresponding spawnMask.");
                                error = true;
                                break;
                            }

                            guid = ObjectGuid.Create(HighGuid.Creature, slave.MapId, slave.Id, guidLow);
                            linkedGuid = ObjectGuid.Create(HighGuid.Creature, master.MapId, master.Id, linkedGuidLow);
                            break;
                        }
                    case CreatureLinkedRespawnType.CreatureToGO:
                        {
                            CreatureData slave = GetCreatureData(guidLow);
                            if (slave == null)
                            {
                                Log.outError(LogFilter.Sql, $"Couldn't get creature data for GUIDLow {guidLow}.");
                                error = true;
                                break;
                            }

                            GameObjectData master = GetGameObjectData(linkedGuidLow);
                            if (master == null)
                            {
                                Log.outError(LogFilter.Sql, $"Couldn't get gameobject data for GUIDLow {linkedGuidLow}.");
                                error = true;
                                break;
                            }

                            MapRecord map = CliDB.MapStorage.LookupByKey(master.MapId);
                            if (map == null || !map.Instanceable || (master.MapId != slave.MapId))
                            {
                                Log.outError(LogFilter.Sql, $"Creature '{guidLow}' linking to '{linkedGuidLow}' on an unpermitted map.");
                                error = true;
                                break;
                            }

                            // they must have a possibility to meet (normal/heroic difficulty)
                            if (!master.SpawnDifficulties.Intersect(slave.SpawnDifficulties).Any())
                            {
                                Log.outError(LogFilter.Sql, $"LinkedRespawn: Creature '{guidLow}' linking to '{linkedGuidLow}' with not corresponding spawnMask.");
                                error = true;
                                break;
                            }

                            guid = ObjectGuid.Create(HighGuid.Creature, slave.MapId, slave.Id, guidLow);
                            linkedGuid = ObjectGuid.Create(HighGuid.GameObject, master.MapId, master.Id, linkedGuidLow);
                            break;
                        }
                    case CreatureLinkedRespawnType.GOToGO:
                        {
                            GameObjectData slave = GetGameObjectData(guidLow);
                            if (slave == null)
                            {
                                Log.outError(LogFilter.Sql, $"Couldn't get gameobject data for GUIDLow {guidLow}.");
                                error = true;
                                break;
                            }

                            GameObjectData master = GetGameObjectData(linkedGuidLow);
                            if (master == null)
                            {
                                Log.outError(LogFilter.Sql, $"Couldn't get gameobject data for GUIDLow {linkedGuidLow}.");
                                error = true;
                                break;
                            }

                            MapRecord map = CliDB.MapStorage.LookupByKey(master.MapId);
                            if (map == null || !map.Instanceable || (master.MapId != slave.MapId))
                            {
                                Log.outError(LogFilter.Sql, $"Creature '{guidLow}' linking to '{linkedGuidLow}' on an unpermitted map.");
                                error = true;
                                break;
                            }

                            // they must have a possibility to meet (normal/heroic difficulty)
                            if (!master.SpawnDifficulties.Intersect(slave.SpawnDifficulties).Any())
                            {
                                Log.outError(LogFilter.Sql, $"LinkedRespawn: Creature '{guidLow}' linking to '{linkedGuidLow}' with not corresponding spawnMask.");
                                error = true;
                                break;
                            }

                            guid = ObjectGuid.Create(HighGuid.GameObject, slave.MapId, slave.Id, guidLow);
                            linkedGuid = ObjectGuid.Create(HighGuid.GameObject, master.MapId, master.Id, linkedGuidLow);
                            break;
                        }
                    case CreatureLinkedRespawnType.GOToCreature:
                        {
                            GameObjectData slave = GetGameObjectData(guidLow);
                            if (slave == null)
                            {
                                Log.outError(LogFilter.Sql, $"Couldn't get gameobject data for GUIDLow {guidLow}.");
                                error = true;
                                break;
                            }

                            CreatureData master = GetCreatureData(linkedGuidLow);
                            if (master == null)
                            {
                                Log.outError(LogFilter.Sql, $"Couldn't get creature data for GUIDLow {linkedGuidLow}.");
                                error = true;
                                break;
                            }

                            MapRecord map = CliDB.MapStorage.LookupByKey(master.MapId);
                            if (map == null || !map.Instanceable || (master.MapId != slave.MapId))
                            {
                                Log.outError(LogFilter.Sql, $"Creature '{guidLow}' linking to '{linkedGuidLow}' on an unpermitted map.");
                                error = true;
                                break;
                            }

                            // they must have a possibility to meet (normal/heroic difficulty)
                            if (!master.SpawnDifficulties.Intersect(slave.SpawnDifficulties).Any())
                            {
                                Log.outError(LogFilter.Sql, $"LinkedRespawn: Creature '{guidLow}' linking to '{linkedGuidLow}' with not corresponding spawnMask.");
                                error = true;
                                break;
                            }

                            guid = ObjectGuid.Create(HighGuid.GameObject, slave.MapId, slave.Id, guidLow);
                            linkedGuid = ObjectGuid.Create(HighGuid.Creature, master.MapId, master.Id, linkedGuidLow);
                            break;
                        }

                }

                if (!error)
                    linkedRespawnStorage[guid] = linkedGuid;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {linkedRespawnStorage.Count} linked respawns in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadNPCText()
        {
            uint oldMSTime = Time.GetMSTime();

            npcTextStorage.Clear();

            SQLResult result = DB.World.Query("SELECT ID, Probability0, Probability1, Probability2, Probability3, Probability4, Probability5, Probability6, Probability7, " +
                "BroadcastTextID0, BroadcastTextID1, BroadcastTextID2, BroadcastTextID3, BroadcastTextID4, BroadcastTextID5, BroadcastTextID6, BroadcastTextID7 FROM npc_text");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 npc texts, table is empty!");
                return;
            }

            do
            {
                int textID = result.Read<int>(0);
                if (textID == 0)
                {
                    Log.outError(LogFilter.Sql, "Table `npc_text` has record wit reserved id 0, ignore.");
                    continue;
                }

                NpcText npcText = new();
                for (int i = 0; i < SharedConst.MaxNpcTextOptions; i++)
                {
                    npcText.Data[i].Probability = result.Read<float>(1 + i);
                    npcText.Data[i].BroadcastTextID = result.Read<int>(9 + i);
                }

                for (int i = 0; i < SharedConst.MaxNpcTextOptions; i++)
                {
                    if (npcText.Data[i].BroadcastTextID != 0)
                    {
                        if (!CliDB.BroadcastTextStorage.ContainsKey(npcText.Data[i].BroadcastTextID))
                        {
                            Log.outError(LogFilter.Sql, $"NPCText (Id: {textID}) has a non-existing BroadcastText (ID: {npcText.Data[i].BroadcastTextID}, Index: {i}).");
                            npcText.Data[i].Probability = 0.0f;
                            npcText.Data[i].BroadcastTextID = 0;
                        }
                    }
                }

                for (byte i = 0; i < SharedConst.MaxNpcTextOptions; i++)
                {
                    if (npcText.Data[i].Probability > 0 && npcText.Data[i].BroadcastTextID == 0)
                    {
                        Log.outError(LogFilter.Sql, $"NPCText (ID: {textID}) has a probability (Index: {i}) set, but no BroadcastTextID to go with it.");
                        npcText.Data[i].Probability = 0;
                    }
                }

                float probabilitySum = npcText.Data.Aggregate(0f, (float sum, NpcTextData data) => { return sum + data.Probability; });
                if (probabilitySum <= 0.0f)
                {
                    Log.outError(LogFilter.Sql, $"NPCText (ID: {textID}) has a probability sum 0, no text can be selected from it, skipped.");
                    continue;
                }

                npcTextStorage[textID] = npcText;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {npcTextStorage.Count} npc texts in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadTrainers()
        {
            uint oldMSTime = Time.GetMSTime();

            // For reload case
            trainers.Clear();

            MultiMap<int, TrainerSpell> spellsByTrainer = new();

            {
                //                                                      0           1       2           3               4           5               6           7           8
                SQLResult trainerSpellsResult = DB.World.Query("SELECT TrainerId, SpellId, MoneyCost, ReqSkillLine, ReqSkillRank, ReqAbility1, ReqAbility2, ReqAbility3, ReqLevel FROM trainer_spell");
            
                if (!trainerSpellsResult.IsEmpty())
                {
                    do
                    {
                        TrainerSpell spell = new();
                        int trainerId = trainerSpellsResult.Read<int>(0);
                        spell.SpellId = trainerSpellsResult.Read<int>(1);
                        spell.MoneyCost = trainerSpellsResult.Read<uint>(2);
                        spell.ReqSkillLine = trainerSpellsResult.Read<int>(3);
                        spell.ReqSkillRank = trainerSpellsResult.Read<int>(4);
                        spell.ReqAbility[0] = trainerSpellsResult.Read<int>(5);
                        spell.ReqAbility[1] = trainerSpellsResult.Read<int>(6);
                        spell.ReqAbility[2] = trainerSpellsResult.Read<int>(7);
                        spell.ReqLevel = trainerSpellsResult.Read<byte>(8);

                        SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spell.SpellId, Difficulty.None);
                        if (spellInfo == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Table `trainer_spell` references non-existing spell (SpellId: {spell.SpellId}) for TrainerId {trainerId}, ignoring.");
                            continue;
                        }

                        if (spell.ReqSkillLine != 0 && !CliDB.SkillLineStorage.ContainsKey(spell.ReqSkillLine))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Table `trainer_spell` references non-existing skill (ReqSkillLine: {spell.ReqSkillLine}) for TrainerId {trainerId} and SpellId {spell.SpellId}, ignoring.");
                            continue;
                        }

                        bool allReqValid = true;
                        for (var i = 0; i < spell.ReqAbility.Count; ++i)
                        {
                            int requiredSpell = spell.ReqAbility[i];
                            if (requiredSpell != 0 && !Global.SpellMgr.HasSpellInfo(requiredSpell, Difficulty.None))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Table `trainer_spell` references non-existing spell (ReqAbility {i + 1}: {requiredSpell}) for TrainerId {trainerId} and SpellId {spell.SpellId}, ignoring.");
                                allReqValid = false;
                            }
                        }

                        if (!allReqValid)
                            continue;

                        spellsByTrainer.Add(trainerId, spell);

                    } while (trainerSpellsResult.NextRow());
                }
            }

            {
                SQLResult trainersResult = DB.World.Query("SELECT Id, Type, Greeting FROM trainer");
            
                if (!trainersResult.IsEmpty())
                {
                    do
                    {
                        int trainerId = trainersResult.Read<int>(0);
                        TrainerType trainerType = (TrainerType)trainersResult.Read<byte>(1);
                        string greeting = trainersResult.Read<string>(2);
                        List<TrainerSpell> spells = new();
                        var spellList = spellsByTrainer.LookupByKey(trainerId);
                        if (spellList != null)
                        {
                            spells = spellList;
                            spellsByTrainer.Remove(trainerId);
                        }

                        trainers.Add(trainerId, new Trainer(trainerId, trainerType, greeting, spells));

                    } while (trainersResult.NextRow());
                }
            }

            foreach (var unusedSpells in spellsByTrainer)
            {
                Log.outError(LogFilter.Sql, 
                    $"Table `trainer_spell` references non-existing trainer (TrainerId: {unusedSpells.Key}) for SpellId {unusedSpells.Value.SpellId}, ignoring.");
            }

            {
                SQLResult trainerLocalesResult = DB.World.Query("SELECT Id, locale, Greeting_lang FROM trainer_locale");
            
                if (!trainerLocalesResult.IsEmpty())
                {
                    do
                    {
                        int trainerId = trainerLocalesResult.Read<int>(0);
                        string localeName = trainerLocalesResult.Read<string>(1);

                        Locale locale = localeName.ToEnum<Locale>();
                        if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                            continue;

                        Trainer trainer = trainers.LookupByKey(trainerId);
                        if (trainer != null)
                            trainer.AddGreetingLocale(locale, trainerLocalesResult.Read<string>(2));
                        else
                            Log.outError(LogFilter.Sql,
                                $"Table `trainer_locale` references non-existing trainer (TrainerId: {trainerId}) for locale {localeName}, ignoring.");

                    } while (trainerLocalesResult.NextRow());
                }
            }
            Log.outInfo(LogFilter.ServerLoading, $"Loaded {trainers.Count} Trainers in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadCreatureTrainers()
        {
            uint oldMSTime = Time.GetMSTime();

            _creatureDefaultTrainers.Clear();

            SQLResult result = DB.World.Query("SELECT CreatureID, TrainerID, MenuID, OptionID FROM creature_trainer");
            if (!result.IsEmpty())
            {
                do
                {
                    int creatureId = result.Read<int>(0);
                    int trainerId = result.Read<int>(1);
                    int gossipMenuId = result.Read<int>(2);
                    int gossipOptionIndex = result.Read<int>(3);

                    if (GetCreatureTemplate(creatureId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature_trainer` references non-existing creature template (CreatureId: {creatureId}), ignoring.");
                        continue;
                    }

                    if (GetTrainer(trainerId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature_trainer` references non-existing trainer (TrainerId: {trainerId}) for CreatureId {creatureId} " +
                            $"MenuId {gossipMenuId} OptionIndex {gossipOptionIndex}, ignoring.");
                        continue;
                    }

                    if (gossipMenuId != 0 || gossipOptionIndex != 0)
                    {
                        var gossipMenuItems = GetGossipMenuItemsMapBounds(gossipMenuId);
                        var gossipOptionItr = gossipMenuItems.Find(entry =>
                        {
                            return entry.OrderIndex == gossipOptionIndex;
                        });

                        if (gossipOptionItr == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `creature_trainer` references non-existing gossip menu option (MenuId {gossipMenuId} " +
                                $"OptionIndex {gossipOptionIndex}) for CreatureId {creatureId} and TrainerId {trainerId}, ignoring.");
                            continue;
                        }
                    }

                    _creatureDefaultTrainers[(creatureId, gossipMenuId, gossipOptionIndex)] = trainerId;
                } while (result.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_creatureDefaultTrainers.Count} default trainers in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadVendors()
        {
            var time = Time.GetMSTime();
            // For reload case
            cacheVendorItemStorage.Clear();

            List<int> skipvendors = new();
            //                                          0      1    2           3           4          5        6           7                   8   
            SQLResult result = DB.World.Query("SELECT entry, item, maxcount, incrtime, ExtendedCost, Type, BonusListIDs, PlayerConditionID, IgnoreFiltering FROM npc_vendor ORDER BY entry, slot ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 Vendors. DB table `npc_vendor` is empty!");
                return;
            }

            uint count = 0;

            do
            {
                int entry = result.Read<int>(0);
                int itemid = result.Read<int>(1);

                // if item is a negative, its a reference
                if (itemid < 0)
                    count += LoadReferenceVendor(entry, -itemid, skipvendors);
                else
                {
                    VendorItem vItem = new();
                    vItem.item = itemid;
                    vItem.maxcount = result.Read<int>(2);
                    vItem.incrtime = result.Read<uint>(3);
                    vItem.ExtendedCost = result.Read<int>(4);
                    vItem.Type = (ItemVendorType)result.Read<byte>(5);
                    vItem.PlayerConditionId = result.Read<int>(7);
                    vItem.IgnoreFiltering = result.Read<bool>(8);

                    var bonusListIDsTok = new StringArray(result.Read<string>(6), ' ');
                    if (!bonusListIDsTok.IsEmpty())
                    {
                        foreach (int token in bonusListIDsTok)
                        {
                            vItem.BonusListIDs.Add(token);
                        }
                    }

                    if (!IsVendorItemValid(entry, vItem, null, skipvendors))
                        continue;

                    if (cacheVendorItemStorage.LookupByKey(entry) == null)
                        cacheVendorItemStorage.Add(entry, new VendorItemData());

                    cacheVendorItemStorage[entry].AddItem(vItem);
                    ++count;
                }
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Vendors in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        uint LoadReferenceVendor(int vendor, int item, List<int> skip_vendors)
        {
            // find all items from the reference vendor
            PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.SEL_NPC_VENDOR_REF);
            stmt.AddValue(0, item);
            SQLResult result = DB.World.Query(stmt);

            if (result.IsEmpty())
                return 0;

            uint count = 0;
            do
            {
                int item_id = result.Read<int>(0);

                // if item is a negative, its a reference
                if (item_id < 0)
                    count += LoadReferenceVendor(vendor, -item_id, skip_vendors);
                else
                {
                    VendorItem vItem = new();
                    vItem.item = item_id;
                    vItem.maxcount = result.Read<int>(1);
                    vItem.incrtime = result.Read<uint>(2);
                    vItem.ExtendedCost = result.Read<int>(3);
                    vItem.Type = (ItemVendorType)result.Read<byte>(4);
                    vItem.PlayerConditionId = result.Read<int>(6);
                    vItem.IgnoreFiltering = result.Read<bool>(7);

                    var bonusListIDsTok = new StringArray(result.Read<string>(5), ' ');
                    if (!bonusListIDsTok.IsEmpty())
                    {
                        foreach (int token in bonusListIDsTok)
                        {
                            //if (uint.TryParse(token, out uint id))
                            //    vItem.BonusListIDs.Add(id);
                            vItem.BonusListIDs.Add(token);
                        }
                    }

                    if (!IsVendorItemValid(vendor, vItem, null, skip_vendors))
                        continue;

                    VendorItemData vList = cacheVendorItemStorage.LookupByKey(vendor);
                    if (vList == null)
                        continue;

                    vList.AddItem(vItem);
                    ++count;
                }
            } while (result.NextRow());

            return count;
        }

        public void LoadCreatures()
        {
            var time = Time.GetMSTime();

            //                                         0              1   2    3           4           5           6            7        8             9              10
            SQLResult result = DB.World.Query("SELECT creature.guid, id, map, position_x, position_y, position_z, orientation, modelid, equipment_id, spawntimesecs, wander_distance, " +
                //11               12         13       14            15                 16          17           18                19                   20                    21
                "currentwaypoint, curhealth, curmana, MovementType, spawnDifficulties, eventEntry, poolSpawnId, creature.npcflag, creature.unit_flags, creature.unit_flags2, creature.unit_flags3, " +
                //22                      23                24                   25                       26                   27
                "creature.phaseUseFlags, creature.phaseid, creature.phasegroup, creature.terrainSwapMap, creature.ScriptName, creature.StringId " +
                "FROM creature LEFT OUTER JOIN game_event_creature ON creature.guid = game_event_creature.guid LEFT OUTER JOIN pool_members ON pool_members.type = 0 AND creature.guid = pool_members.spawnId");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creatures. DB table `creature` is empty.");
                return;
            }

            // Build single time for check spawnmask
            Dictionary<int, List<Difficulty>> spawnMasks = new();
            foreach (var mapDifficultyPair in Global.DB2Mgr.GetMapDifficulties())
            {
                foreach (var difficultyPair in mapDifficultyPair.Value)
                {
                    if (!spawnMasks.ContainsKey(mapDifficultyPair.Key))
                        spawnMasks[mapDifficultyPair.Key] = new List<Difficulty>();

                    spawnMasks[mapDifficultyPair.Key].Add(difficultyPair.Key);
                }
            }

            PhaseShift phaseShift = new();

            uint count = 0;
            do
            {
                long guid = result.Read<long>(0);
                int entry = result.Read<int>(1);

                CreatureTemplate cInfo = GetCreatureTemplate(entry);
                if (cInfo == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` has creature (GUID: {guid}) with non existing creature entry {entry}, skipped.");
                    continue;
                }

                CreatureData data = new();
                data.SpawnId = guid;
                data.Id = entry;
                data.MapId = result.Read<ushort>(2);
                data.SpawnPoint = new Position(result.Read<float>(3), result.Read<float>(4), result.Read<float>(5), result.Read<float>(6));
                int displayId = result.Read<int>(7);
                if (displayId != 0)
                    data.display = new(displayId, SharedConst.DefaultPlayerDisplayScale, 1.0f);

                data.equipmentId = result.Read<sbyte>(8);
                data.spawntimesecs = result.Read<int>(9);
                data.WanderDistance = result.Read<float>(10);
                data.currentwaypoint = result.Read<uint>(11);
                data.curhealth = result.Read<int>(12);
                data.curmana = result.Read<int>(13);
                data.movementType = (MovementGeneratorType)result.Read<byte>(14);
                data.SpawnDifficulties = ParseSpawnDifficulties(result.Read<string>(15), "creature", guid, data.MapId, spawnMasks.LookupByKey(data.MapId));
                short gameEvent = result.Read<short>(16);
                data.poolId = result.Read<int>(17);

                if (!result.IsNull(18))
                {
                    data.npcflag = (NPCFlags1)(result.Read<ulong>(18) & 0xFFFFFFFF);
                    data.npcflag2 = (NPCFlags2)((result.Read<ulong>(18) >> 32) & 0xFFFFFFFF);
                }
                if (!result.IsNull(19))
                    data.unit_flags = (UnitFlags)result.Read<uint>(19);
                if (!result.IsNull(20))
                    data.unit_flags2 = (UnitFlags2)result.Read<uint>(20);
                if (!result.IsNull(21))
                    data.unit_flags3 = (UnitFlags3)result.Read<uint>(21);

                data.PhaseUseFlags = (PhaseUseFlagsValues)result.Read<byte>(22);
                data.PhaseId = result.Read<int>(23);
                data.PhaseGroup = result.Read<int>(24);
                data.terrainSwapMap = result.Read<int>(25);
                data.ScriptId = GetScriptId(result.Read<string>(26));
                data.StringId = result.Read<string>(27);
                data.spawnGroupData = IsTransportMap(data.MapId) ? GetLegacySpawnGroup() : GetDefaultSpawnGroup(); // transport spawns default to compatibility group
                
                var mapEntry = CliDB.MapStorage.LookupByKey(data.MapId);
                if (mapEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid}) that spawned at not existed map (Id: {data.MapId}), skipped.");
                    continue;
                }

                //if (sWorld->getBoolConfig(CONFIG_CREATURE_CHECK_INVALID_POSITION))
                //{
                //    if (VMAP::IVMapManager * vmgr = VMAP::VMapFactory::createOrGetVMapManager())
                //    {
                //        if (vmgr->isMapLoadingEnabled() && !IsTransportMap(data.mapId))
                //        {
                //            GridCoord gridCoord = Trinity::ComputeGridCoord(data.spawnPoint.GetPositionX(), data.spawnPoint.GetPositionY());
                //            int gx = (MAX_NUMBER_OF_GRIDS - 1) - gridCoord.x_coord;
                //            int gy = (MAX_NUMBER_OF_GRIDS - 1) - gridCoord.y_coord;

                //            VMAP::LoadResult result = vmgr->existsMap((sWorld->GetDataPath() + "vmaps").c_str(), data.mapId, gx, gy);
                //            if (result != VMAP::LoadResult::Success)
                //                TC_LOG_ERROR("sql.sql", "Table `creature` has creature (GUID: {} Entry: {} MapID: {}) spawned on a possible invalid position ({})",
                //                    guid, data.id, data.mapId, data.spawnPoint.ToString());
                //        }
                //    }
                //}

                if (data.SpawnDifficulties.Empty())
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` has creature (GUID: {guid}) that is not spawned in any difficulty, skipped.");
                    continue;
                }

                // -1 random, 0 no equipment,
                if (data.equipmentId != 0)
                {
                    if (GetEquipmentInfo(data.Id, data.equipmentId) == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `creature` have creature (Entry: {data.Id}) with equipmentid {data.equipmentId} not found in table `creatureequiptemplate`, set to no equipment.");
                        data.equipmentId = 0;
                    }
                }

                if (cInfo.FlagsExtra.HasFlag(CreatureFlagsExtra.InstanceBind))
                {
                    if (!mapEntry.IsDungeon)
                        Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) with " +
                            "`creature_template`.`flagsextra` including CREATURE_FLAG_EXTRA_INSTANCE_BIND but creature are not in instance.");
                }

                if (data.WanderDistance < 0.0f)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) with `wander_distance`< 0, set to 0.");
                    data.WanderDistance = 0.0f;
                }
                else if (data.WanderDistance > 0.0f && data.WanderDistance < 0.1f)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` has creature (GUID: {guid} Entry: {data.Id}) with `wander_distance` below the allowed minimum distance of 0.1, set to 0.");
                    data.WanderDistance = 0.0f;
                }
                else if (data.movementType == MovementGeneratorType.Random)
                {
                    if (MathFunctions.fuzzyEq(data.WanderDistance, 0.0f))
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) with `MovementType`=1 (random movement) " +
                            $"but with `wander_distance`=0, replace by idle movement Type (0).");
                        data.movementType = MovementGeneratorType.Idle;
                    }
                }
                else if (data.movementType == MovementGeneratorType.Idle)
                {
                    if (data.WanderDistance != 0.0f)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) with `MovementType`=0 (idle) have " +
                            $"`wander_distance`<>0, set to 0.");
                        data.WanderDistance = 0.0f;
                    }
                }

                if ((data.PhaseUseFlags & ~PhaseUseFlagsValues.All) != 0)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) has unknown `phaseUseFlags` set, " +
                        $"removed unknown value.");
                    data.PhaseUseFlags &= PhaseUseFlagsValues.All;
                }

                if (data.PhaseUseFlags.HasFlag(PhaseUseFlagsValues.AlwaysVisible | PhaseUseFlagsValues.Inverse))
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) has both `phaseUseFlags` " +
                        "PHASE_USE_FLAGS_ALWAYS_VISIBLE and PHASE_USE_FLAGS_INVERSE, removing PHASE_USE_FLAGS_INVERSE.");
                    data.PhaseUseFlags &= ~PhaseUseFlagsValues.Inverse;
                }

                if (data.PhaseGroup != 0 && data.PhaseId != 0)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) with both `phaseid` and " +
                        $"`phasegroup` set, `phasegroup` set to 0.");
                    data.PhaseGroup = 0;
                }

                if (data.PhaseId != 0)
                {
                    if (!CliDB.PhaseStorage.ContainsKey(data.PhaseId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) with `phaseid` " +
                            $"{data.PhaseId} does not exist, set to 0.");
                        data.PhaseId = 0;
                    }
                }

                if (data.PhaseGroup != 0)
                {
                    if (Global.DB2Mgr.GetPhasesForGroup(data.PhaseGroup).Empty())
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) with `phasegroup` " +
                            $"{data.PhaseGroup} does not exist, set to 0.");
                        data.PhaseGroup = 0;
                    }
                }

                if (data.terrainSwapMap != -1)
                {
                    MapRecord terrainSwapEntry = CliDB.MapStorage.LookupByKey(data.terrainSwapMap);
                    if (terrainSwapEntry == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) with `terrainSwapMap` " +
                            $"{data.terrainSwapMap} does not exist, set to -1.");
                        data.terrainSwapMap = -1;
                    }
                    else if (terrainSwapEntry.ParentMapID != data.MapId)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature` have creature (GUID: {guid} Entry: {data.Id}) with `terrainSwapMap` " +
                            $"{data.terrainSwapMap} which cannot be used on spawn map, set to -1.");
                        data.terrainSwapMap = -1;
                    }
                }

                if (data.unit_flags.HasValue)
                {
                    UnitFlags disallowedUnitFlags = (data.unit_flags.Value & ~UnitFlags.Allowed);
                    if (disallowedUnitFlags != 0)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature_template` lists creature (Entry: {cInfo.Entry}) with disallowed `unit_flags` " +
                            $"{disallowedUnitFlags} [{(uint)disallowedUnitFlags}], removing incorrect flag.");
                        data.unit_flags = data.unit_flags & UnitFlags.Allowed;
                    }
                }

                if (data.unit_flags2.HasValue)
                {
                    UnitFlags2 disallowedUnitFlags2 = (data.unit_flags2.Value & ~UnitFlags2.Allowed);
                    if (disallowedUnitFlags2 != 0)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature_template` lists creature (Entry: {cInfo.Entry}) with disallowed `unit_flags2` " +
                            $"{disallowedUnitFlags2} [{(uint)disallowedUnitFlags2}], removing incorrect flag.");
                        data.unit_flags2 = data.unit_flags2 & UnitFlags2.Allowed;
                    }
                }

                if (data.unit_flags3.HasValue)
                {
                    UnitFlags3 disallowedUnitFlags3 = (data.unit_flags3.Value & ~UnitFlags3.Allowed);
                    if (disallowedUnitFlags3 != 0)
                    {
                        Log.outError(LogFilter.Sql, $"Table `creature_template` lists creature (Entry: {cInfo.Entry}) with disallowed `unit_flags3` " +
                            $"{disallowedUnitFlags3} [{(uint)disallowedUnitFlags3}], removing incorrect flag.");
                        data.unit_flags3 = data.unit_flags3 & UnitFlags3.Allowed;
                    }
                }

                if (WorldConfig.GetBoolValue(WorldCfg.CalculateCreatureZoneAreaData))
                {
                    PhasingHandler.InitDbVisibleMapId(phaseShift, data.terrainSwapMap);
                    Global.TerrainMgr.GetZoneAndAreaId(phaseShift, out int zoneId, out int areaId, data.MapId, data.SpawnPoint);

                    PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_ZONE_AREA_DATA);
                    stmt.AddValue(0, zoneId);
                    stmt.AddValue(1, areaId);
                    stmt.AddValue(2, guid);

                    DB.World.Execute(stmt);
                }

                // Add to grid if not managed by the game event
                if (gameEvent == 0)
                    AddCreatureToGrid(data);

                creatureDataStorage[guid] = data;
                count++;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creatures in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public bool HasPersonalSpawns(int mapid, Difficulty spawnMode, int phaseId)
        {
            return mapPersonalObjectGuidsStore.ContainsKey((mapid, spawnMode, phaseId));
        }

        public CellObjectGuids GetCellPersonalObjectGuids(int mapid, Difficulty spawnMode, int phaseId, int cell_id)
        {
            var guids = mapPersonalObjectGuidsStore.LookupByKey((mapid, spawnMode, phaseId));
            if (guids != null)
                return guids.LookupByKey(cell_id);

            return null;
        }

        void AddSpawnDataToGrid(SpawnData data)
        {
            var cellId = GridDefines.ComputeCellCoord(data.SpawnPoint.GetPositionX(), data.SpawnPoint.GetPositionY()).GetId();
            bool isPersonalPhase = PhasingHandler.IsPersonalPhase(data.PhaseId);
            if (!isPersonalPhase)
            {
                foreach (Difficulty difficulty in data.SpawnDifficulties)
                {
                    var key = (data.MapId, difficulty);
                    if (!mapObjectGuidsStore.ContainsKey(key))
                        mapObjectGuidsStore[key] = new();

                    if (!mapObjectGuidsStore[key].ContainsKey(cellId))
                        mapObjectGuidsStore[key][cellId] = new();

                    mapObjectGuidsStore[key][cellId].AddSpawn(data);
                }
            }
            else
            {
                foreach (Difficulty difficulty in data.SpawnDifficulties)
                {
                    var key = (data.MapId, difficulty, data.PhaseId);
                    if (!mapPersonalObjectGuidsStore.ContainsKey(key))
                        mapPersonalObjectGuidsStore[key] = new();

                    if (!mapPersonalObjectGuidsStore[key].ContainsKey(cellId))
                        mapPersonalObjectGuidsStore[key][cellId] = new();

                    mapPersonalObjectGuidsStore[key][cellId].AddSpawn(data);
                }
            }
        }

        void RemoveSpawnDataFromGrid(SpawnData data)
        {
            var cellId = GridDefines.ComputeCellCoord(data.SpawnPoint.GetPositionX(), data.SpawnPoint.GetPositionY()).GetId();
            bool isPersonalPhase = PhasingHandler.IsPersonalPhase(data.PhaseId);
            if (!isPersonalPhase)
            {
                foreach (Difficulty difficulty in data.SpawnDifficulties)
                {
                    var key = (data.MapId, difficulty);
                    if (!mapObjectGuidsStore.ContainsKey(key) || !mapObjectGuidsStore[key].ContainsKey(cellId))
                        continue;

                    mapObjectGuidsStore[(data.MapId, difficulty)][cellId].RemoveSpawn(data);
                }
            }
            else
            {
                foreach (Difficulty difficulty in data.SpawnDifficulties)
                {
                    var key = (data.MapId, difficulty, data.PhaseId);
                    if (!mapPersonalObjectGuidsStore.ContainsKey(key) || !mapPersonalObjectGuidsStore[key].ContainsKey(cellId))
                        continue;

                    mapPersonalObjectGuidsStore[key][cellId].RemoveSpawn(data);
                }
            }
        }

        public void AddCreatureToGrid(CreatureData data)
        {
            AddSpawnDataToGrid(data);
        }

        public void RemoveCreatureFromGrid(CreatureData data)
        {
            RemoveSpawnDataFromGrid(data);
        }

        public CreatureAddon GetCreatureAddon(long lowguid)
        {
            return creatureAddonStorage.LookupByKey(lowguid);
        }

        public CreatureTemplate GetCreatureTemplate(int entry)
        {
            return creatureTemplateStorage.LookupByKey(entry);
        }

        public CreatureAddon GetCreatureTemplateAddon(int entry)
        {
            return creatureTemplateAddonStorage.LookupByKey(entry);
        }

        public int GetCreatureDefaultTrainer(int creatureId)
        {
            return GetCreatureTrainerForGossipOption(creatureId, 0, 0);
        }

        public int GetCreatureTrainerForGossipOption(int creatureId, int gossipMenuId, int gossipOptionIndex)
        {
            return _creatureDefaultTrainers.LookupByKey((creatureId, gossipMenuId, gossipOptionIndex));
        }

        public Dictionary<int, CreatureTemplate> GetCreatureTemplates()
        {
            return creatureTemplateStorage;
        }

        public Dictionary<long, CreatureData> GetAllCreatureData() { return creatureDataStorage; }

        public CreatureData GetCreatureData(long spawnId)
        {
            return creatureDataStorage.LookupByKey(spawnId);
        }

        public ObjectGuid GetLinkedRespawnGuid(ObjectGuid spawnId)
        {
            var retGuid = linkedRespawnStorage.LookupByKey(spawnId);
            if (retGuid.IsEmpty())
                return ObjectGuid.Empty;
            return retGuid;
        }

        public bool SetCreatureLinkedRespawn(long guidLow, long linkedGuidLow)
        {
            if (guidLow == 0)
                return false;

            CreatureData master = GetCreatureData(guidLow);
            Cypher.Assert(master != null);
            ObjectGuid guid = ObjectGuid.Create(HighGuid.Creature, master.MapId, master.Id, guidLow);
            PreparedStatement stmt;

            if (linkedGuidLow == 0) // we're removing the linking
            {
                linkedRespawnStorage.Remove(guid);
                stmt = WorldDatabase.GetPreparedStatement(WorldStatements.DEL_LINKED_RESPAWN);
                stmt.AddValue(0, guidLow);
                stmt.AddValue(1, (uint)CreatureLinkedRespawnType.CreatureToCreature);
                DB.World.Execute(stmt);
                return true;
            }

            CreatureData slave = GetCreatureData(linkedGuidLow);
            if (slave == null)
            {
                Log.outError(LogFilter.Sql, $"Creature '{guidLow}' linking to non-existent creature '{linkedGuidLow}'.");
                return false;
            }

            MapRecord map = CliDB.MapStorage.LookupByKey(master.MapId);
            if (map == null || !map.Instanceable || (master.MapId != slave.MapId))
            {
                Log.outError(LogFilter.Sql, $"Creature '{guidLow}' linking to '{linkedGuidLow}' on an unpermitted map.");
                return false;
            }

            // they must have a possibility to meet (normal/heroic difficulty)
            if (!master.SpawnDifficulties.Intersect(slave.SpawnDifficulties).Any())
            {
                Log.outError(LogFilter.Sql, $"LinkedRespawn: Creature '{guidLow}' linking to '{linkedGuidLow}' with not corresponding spawnMask");
                return false;
            }

            ObjectGuid linkedGuid = ObjectGuid.Create(HighGuid.Creature, slave.MapId, slave.Id, linkedGuidLow);

            linkedRespawnStorage[guid] = linkedGuid;
            stmt = WorldDatabase.GetPreparedStatement(WorldStatements.REP_LINKED_RESPAWN);
            stmt.AddValue(0, guidLow);
            stmt.AddValue(1, linkedGuidLow);
            stmt.AddValue(2, (uint)CreatureLinkedRespawnType.CreatureToCreature);
            DB.World.Execute(stmt);
            return true;
        }

        public CreatureData NewOrExistCreatureData(long spawnId)
        {
            if (!creatureDataStorage.ContainsKey(spawnId))
                creatureDataStorage[spawnId] = new CreatureData();
            return creatureDataStorage[spawnId];
        }

        public void DeleteCreatureData(long spawnId)
        {
            CreatureData data = GetCreatureData(spawnId);
            if (data != null)
            {
                RemoveCreatureFromGrid(data);
                OnDeleteSpawnData(data);
            }

            creatureDataStorage.Remove(spawnId);
        }

        public CreatureBaseStats GetCreatureBaseStats(int level, Class unitClass)
        {
            var stats = creatureBaseStatsStorage.LookupByKey((level, unitClass));
            if (stats != null)
                return stats;

            return new DefaultCreatureBaseStats();
        }

        public CreatureModelInfo GetCreatureModelRandomGender(ref CreatureModel model, CreatureTemplate creatureTemplate)
        {
            CreatureModelInfo modelInfo = GetCreatureModelInfo(model.CreatureDisplayID);
            if (modelInfo == null)
                return null;

            // If a model for another gender exists, 50% Chance to use it
            if (modelInfo.DisplayIdOtherGender != 0 && RandomHelper.URand(0, 1) == 0)
            {
                CreatureModelInfo minfotmp = GetCreatureModelInfo(modelInfo.DisplayIdOtherGender);
                if (minfotmp == null)
                    Log.outError(LogFilter.Sql,
                        $"Model (Entry: {model.CreatureDisplayID}) has modelidothergender {modelInfo.DisplayIdOtherGender} not found in table `creaturemodelinfo`.");
                else
                {
                    // DisplayID changed
                    model.CreatureDisplayID = modelInfo.DisplayIdOtherGender;
                    if (creatureTemplate != null)
                    {
                        var creatureModel = creatureTemplate.Models.Find(templateModel =>
                        {
                            return templateModel.CreatureDisplayID == modelInfo.DisplayIdOtherGender;
                        });

                        if (creatureModel != null)
                            model = creatureModel;
                    }
                    return minfotmp;
                }
            }

            return modelInfo;
        }

        public CreatureModelInfo GetCreatureModelInfo(int modelId)
        {
            return creatureModelStorage.LookupByKey(modelId);
        }

        public CreatureSummonedData GetCreatureSummonedData(int entryId)
        {
            return creatureSummonedDataStorage.LookupByKey(entryId);
        }

        public NpcText GetNpcText(int textId)
        {
            return npcTextStorage.LookupByKey(textId);
        }

        //GameObjects
        public void LoadGameObjectTemplate()
        {
            var time = Time.GetMSTime();

            foreach (GameObjectsRecord db2go in CliDB.GameObjectsStorage.Values)
            {
                GameObjectTemplate go = new();
                go.entry = db2go.Id;
                go.type = db2go.TypeID;
                go.displayId = db2go.DisplayID;
                go.name = db2go.Name[Global.WorldMgr.GetDefaultDbcLocale()];
                go.size = db2go.Scale;

                unsafe
                {
                    for (byte x = 0; x < db2go.PropValue.Length; ++x)
                        go.Raw.data[x] = db2go.PropValue[x];
                }

                go.ContentTuningId = 0;
                go.ScriptId = 0;

                _gameObjectTemplateStorage[db2go.Id] = go;
            }

            //                                          0      1     2          3     4         5               6     7
            SQLResult result = DB.World.Query("SELECT entry, Type, displayId, name, IconName, castBarCaption, unk1, size, " +
                //8      9      10     11     12     13     14     15     16     17     18      19      20
                "Data0, Data1, Data2, Data3, Data4, Data5, Data6, Data7, Data8, Data9, Data10, Data11, Data12, " +
                //21      22      23      24      25      26      27      28      29      30      31      32      33      34      35      36
                "Data13, Data14, Data15, Data16, Data17, Data18, Data19, Data20, Data21, Data22, Data23, Data24, Data25, Data26, Data27, Data28, " +
                //37      38       39     40      41      42      43               44      45          46
                "Data29, Data30, Data31, Data32, Data33, Data34, ContentTuningId, AIName, ScriptName, StringId FROM gameobject_template");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobject definitions. DB table `gameobject_template` is empty.");
            }
            else
            {
                do
                {
                    int entry = result.Read<int>(0);

                    GameObjectTemplate got = new();

                    got.entry = entry;
                    got.type = (GameObjectTypes)result.Read<byte>(1);
                    got.displayId = result.Read<int>(2);
                    got.name = result.Read<string>(3);
                    got.IconName = result.Read<string>(4);
                    got.castBarCaption = result.Read<string>(5);
                    got.unk1 = result.Read<string>(6);
                    got.size = result.Read<float>(7);

                    unsafe
                    {
                        for (byte x = 0; x < SharedConst.MaxGOData; ++x)
                            got.Raw.data[x] = result.Read<int>(8 + x);
                    }

                    got.ContentTuningId = result.Read<int>(43);
                    got.AIName = result.Read<string>(44);
                    got.ScriptId = GetScriptId(result.Read<string>(45));
                    got.StringId = result.Read<string>(46);

                    //// Checks
                    //if (!got.AIName.empty() && !sGameObjectAIRegistry->HasItem(got.AIName))
                    //{
                    //    TC_LOG_ERROR("sql.sql", "GameObject (Entry: {}) has non-registered `AIName` '{}' set, removing", got.entry, got.AIName);
                    //    got.AIName.clear();
                    //}

                    switch (got.type)
                    {
                        case GameObjectTypes.Door:                      //0
                            if (got.Door.open != 0)
                                CheckGOLockId(got, got.Door.open, 1);
                            CheckGONoDamageImmuneId(got, got.Door.noDamageImmune, 3);
                            break;
                        case GameObjectTypes.Button:                    //1
                            if (got.Button.open != 0)
                                CheckGOLockId(got, got.Button.open, 1);
                            CheckGONoDamageImmuneId(got, got.Button.noDamageImmune, 4);
                            break;
                        case GameObjectTypes.QuestGiver:                //2
                            if (got.QuestGiver.open != 0)
                                CheckGOLockId(got, got.QuestGiver.open, 0);
                            CheckGONoDamageImmuneId(got, got.QuestGiver.noDamageImmune, 5);
                            break;
                        case GameObjectTypes.Chest:                     //3
                            if (got.Chest.open != 0)
                                CheckGOLockId(got, got.Chest.open, 0);

                            CheckGOConsumable(got, got.Chest.consumable, 3);

                            if (got.Chest.linkedTrap != 0)              // linked trap
                                CheckGOLinkedTrapId(got, got.Chest.linkedTrap, 7);
                            break;
                        case GameObjectTypes.Trap:                      //6
                            if (got.Trap.open != 0)
                                CheckGOLockId(got, got.Trap.open, 0);
                            break;
                        case GameObjectTypes.Chair:                     //7
                            CheckAndFixGOChairHeightId(got, ref got.Chair.chairheight, 1);
                            break;
                        case GameObjectTypes.SpellFocus:               //8
                            if (got.SpellFocus.spellFocusType != 0)
                            {
                                if (!CliDB.SpellFocusObjectStorage.ContainsKey(got.SpellFocus.spellFocusType))
                                    Log.outError(LogFilter.Sql, $"GameObject (Entry: {entry} GoType: {got.type}) have data0={got.SpellFocus.spellFocusType} but " +
                                        $"SpellFocus (Id: {got.SpellFocus.spellFocusType}) not exist.");
                            }

                            if (got.SpellFocus.linkedTrap != 0)        // linked trap
                                CheckGOLinkedTrapId(got, got.SpellFocus.linkedTrap, 2);
                            break;
                        case GameObjectTypes.Goober:                    //10
                            if (got.Goober.open != 0)
                                CheckGOLockId(got, got.Goober.open, 0);

                            CheckGOConsumable(got, got.Goober.consumable, 3);

                            if (got.Goober.pageID != 0)                  // pageId
                            {
                                if (GetPageText(got.Goober.pageID) == null)
                                    Log.outError(LogFilter.Sql,
                                        $"GameObject (Entry: {entry} GoType: {got.type}) have data7={got.Goober.pageID} but PageText (Entry {got.Goober.pageID}) not exist.");
                            }
                            CheckGONoDamageImmuneId(got, got.Goober.noDamageImmune, 11);
                            if (got.Goober.linkedTrap != 0)            // linked trap
                                CheckGOLinkedTrapId(got, got.Goober.linkedTrap, 12);
                            break;
                        case GameObjectTypes.AreaDamage:                //12
                            if (got.AreaDamage.open != 0)
                                CheckGOLockId(got, got.AreaDamage.open, 0);
                            break;
                        case GameObjectTypes.Camera:                    //13
                            if (got.Camera.open != 0)
                                CheckGOLockId(got, got.Camera.open, 0);
                            break;
                        case GameObjectTypes.MapObjTransport:              //15
                        {
                            if (got.MoTransport.taxiPathID != 0)
                            {
                                if (got.MoTransport.taxiPathID >= CliDB.TaxiPathNodesByPath.Count || CliDB.TaxiPathNodesByPath[got.MoTransport.taxiPathID].Empty())
                                    Log.outError(LogFilter.Sql, $"GameObject (Entry: {entry} GoType: {got.type}) have data0={got.MoTransport.taxiPathID} but " +
                                        $"TaxiPath (Id: {got.MoTransport.taxiPathID}) not exist.");
                            }
                            int transportMap = got.MoTransport.SpawnMap;
                            if (transportMap != 0)
                                _transportMaps.Add((ushort)transportMap);
                            break;
                        }
                        case GameObjectTypes.SpellCaster:               //22
                                                                        // always must have spell
                            CheckGOSpellId(got, got.SpellCaster.spell, 0);
                            break;
                        case GameObjectTypes.FlagStand:                 //24
                            if (got.FlagStand.open != 0)
                                CheckGOLockId(got, got.FlagStand.open, 0);
                            CheckGONoDamageImmuneId(got, got.FlagStand.noDamageImmune, 5);
                            break;
                        case GameObjectTypes.FishingHole:               //25
                            if (got.FishingHole.open != 0)
                                CheckGOLockId(got, got.FishingHole.open, 4);
                            break;
                        case GameObjectTypes.FlagDrop:                  //26
                            if (got.FlagDrop.open != 0)
                                CheckGOLockId(got, got.FlagDrop.open, 0);
                            CheckGONoDamageImmuneId(got, got.FlagDrop.noDamageImmune, 3);
                            break;
                        case GameObjectTypes.BarberChair:              //32
                            CheckAndFixGOChairHeightId(got, ref got.BarberChair.chairheight, 0);
                            if (got.BarberChair.SitAnimKit != 0 && !CliDB.AnimKitStorage.ContainsKey(got.BarberChair.SitAnimKit))
                            {
                                Log.outError(LogFilter.Sql, $"GameObject (Entry: {entry} GoType: {got.type}) have data2 = {got.BarberChair.SitAnimKit} but " +
                                    $"AnimKit.dbc (Id: {got.BarberChair.SitAnimKit}) not exist, set to 0.");
                                got.BarberChair.SitAnimKit = 0;
                            }
                            break;
                        case GameObjectTypes.GarrisonBuilding:
                            {
                                int transportMap = got.GarrisonBuilding.SpawnMap;
                                if (transportMap != 0)
                                    _transportMaps.Add((ushort)transportMap);
                            }
                            break;
                        case GameObjectTypes.GatheringNode:
                            if (got.GatheringNode.open != 0)
                                CheckGOLockId(got, got.GatheringNode.open, 0);
                            if (got.GatheringNode.linkedTrap != 0)
                                CheckGOLinkedTrapId(got, got.GatheringNode.linkedTrap, 20);
                            break;
                    }

                    _gameObjectTemplateStorage[entry] = got;
                }
                while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {_gameObjectTemplateStorage.Count} game object templates in {Time.GetMSTimeDiffToNow(time)} ms.");
            }
        }

        public void LoadGameObjectTemplateAddons()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0       1       2      3        4        5        6        7        8        9        10             11
            SQLResult result = DB.World.Query("SELECT entry, faction, flags, mingold, maxgold, artkit0, artkit1, artkit2, artkit3, artkit4, WorldEffectID, AIAnimKitID FROM gameobject_template_addon");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobject template addon definitions. DB table `gameobject_template_addon` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int entry = result.Read<int>(0);

                GameObjectTemplate got = GetGameObjectTemplate(entry);
                if (got == null)
                {
                    Log.outError(LogFilter.Sql, $"GameObject template (Entry: {entry}) does not exist but has a record in `gameobject_template_addon`.");
                    continue;
                }

                GameObjectTemplateAddon gameObjectAddon = new();
                gameObjectAddon.Faction = result.Read<ushort>(1);
                gameObjectAddon.Flags = (GameObjectFlags)result.Read<int>(2);
                gameObjectAddon.Mingold = result.Read<uint>(3);
                gameObjectAddon.Maxgold = result.Read<uint>(4);
                gameObjectAddon.WorldEffectID = result.Read<int>(10);
                gameObjectAddon.AIAnimKitID = result.Read<int>(11);

                for (int i = 0; i < gameObjectAddon.ArtKits.Length; ++i)
                {
                    int artKitID = result.Read<int>(5 + i);
                    if (artKitID == 0)
                        continue;

                    if (!CliDB.GameObjectArtKitStorage.ContainsKey(artKitID))
                    {
                        Log.outError(LogFilter.Sql, $"GameObject (Entry: {entry}) has invalid `artkit{i}` ({artKitID}) defined, set to zero instead.");
                        continue;
                    }

                    gameObjectAddon.ArtKits[i] = artKitID;
                }

                // checks
                if (gameObjectAddon.Faction != 0 && !CliDB.FactionTemplateStorage.ContainsKey(gameObjectAddon.Faction))
                    Log.outError(LogFilter.Sql, $"GameObject (Entry: {entry}) has invalid faction ({gameObjectAddon.Faction}) defined in `gameobject_template_addon`.");

                if (gameObjectAddon.Maxgold > 0)
                {
                    switch (got.type)
                    {
                        case GameObjectTypes.Chest:
                        case GameObjectTypes.FishingHole:
                            break;
                        default:
                            Log.outError(LogFilter.Sql, $"GameObject (Entry {entry} GoType: {got.type}) cannot be looted but has maxgold set in `gameobject_template_addon`.");
                            break;
                    }
                }

                if (gameObjectAddon.WorldEffectID != 0 && !CliDB.WorldEffectStorage.ContainsKey(gameObjectAddon.WorldEffectID))
                {
                    Log.outError(LogFilter.Sql, 
                        $"GameObject (Entry: {entry}) has invalid WorldEffectID ({gameObjectAddon.WorldEffectID}) defined in `gameobject_template_addon`, set to 0.");
                    gameObjectAddon.WorldEffectID = 0;
                }

                if (gameObjectAddon.AIAnimKitID != 0 && !CliDB.AnimKitStorage.ContainsKey(gameObjectAddon.AIAnimKitID))
                {
                    Log.outError(LogFilter.Sql, 
                        $"GameObject (Entry: {entry}) has invalid AIAnimKitID ({gameObjectAddon.AIAnimKitID}) defined in `gameobject_template_addon`, set to 0.");
                    gameObjectAddon.AIAnimKitID = 0;
                }

                _gameObjectTemplateAddonStorage[entry] = gameObjectAddon;
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} game object template addons in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadGameObjectOverrides()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                           0        1        2
            SQLResult result = DB.World.Query("SELECT spawnId, faction, flags FROM gameobject_overrides");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobject faction and flags overrides. DB table `gameobject_overrides` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                long spawnId = result.Read<long>(0);
                GameObjectData goData = GetGameObjectData(spawnId);
                if (goData == null)
                {
                    Log.outError(LogFilter.Sql, $"GameObject (SpawnId: {spawnId}) does not exist but has a record in `gameobject_overrides`.");
                    continue;
                }

                GameObjectOverride gameObjectOverride = new();
                gameObjectOverride.Faction = result.Read<ushort>(1);
                gameObjectOverride.Flags = (GameObjectFlags)result.Read<int>(2);

                _gameObjectOverrideStorage[spawnId] = gameObjectOverride;

                if (gameObjectOverride.Faction != 0 && !CliDB.FactionTemplateStorage.ContainsKey(gameObjectOverride.Faction))
                    Log.outError(LogFilter.Sql, $"GameObject (SpawnId: {spawnId}) has invalid faction ({gameObjectOverride.Faction}) defined in `gameobject_overrides`.");

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} gameobject faction and flags overrides in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadGameObjects()
        {
            var time = Time.GetMSTime();
            //                                         0                1   2    3           4           5           6
            SQLResult result = DB.World.Query("SELECT gameobject.guid, id, map, position_x, position_y, position_z, orientation, " +
                //7          8          9          10         11             12            13     14                 15          16
                "rotation0, rotation1, rotation2, rotation3, spawntimesecs, animprogress, state, spawnDifficulties, eventEntry, poolSpawnId, " +
                //17             18       19          20              21          22
                "phaseUseFlags, phaseid, phasegroup, terrainSwapMap, ScriptName, StringId " +
                "FROM gameobject LEFT OUTER JOIN game_event_gameobject ON gameobject.guid = game_event_gameobject.guid " +
                "LEFT OUTER JOIN pool_members ON pool_members.Type = 1 AND gameobject.guid = pool_members.spawnId");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobjects. DB table `gameobject` is empty.");

                return;
            }
            uint count = 0;

            // build single time for check spawnmask
            Dictionary<int, List<Difficulty>> spawnMasks = new();
            foreach (var mapDifficultyPair in Global.DB2Mgr.GetMapDifficulties())
            {
                foreach (var difficultyPair in mapDifficultyPair.Value)
                {
                    if (!spawnMasks.ContainsKey(mapDifficultyPair.Key))
                        spawnMasks[mapDifficultyPair.Key] = new List<Difficulty>();

                    spawnMasks[mapDifficultyPair.Key].Add(difficultyPair.Key);
                }
            }

            PhaseShift phaseShift = new();

            do
            {
                long guid = result.Read<long>(0);
                int entry = result.Read<int>(1);

                GameObjectTemplate gInfo = GetGameObjectTemplate(entry);
                if (gInfo == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` has gameobject (GUID: {guid}) with non existing gameobject entry {entry}, skipped.");
                    continue;
                }

                if (gInfo.displayId == 0)
                {
                    switch (gInfo.type)
                    {
                        case GameObjectTypes.Trap:
                        case GameObjectTypes.SpellFocus:
                            break;
                        default:
                            Log.outError(LogFilter.Sql, $"Gameobject (GUID: {guid} Entry {entry} GoType: {gInfo.type} [{(uint)gInfo.type}]) " +
                                $"doesn't have a displayId ({gInfo.displayId}), not loaded.");
                            break;
                    }
                }

                if (gInfo.displayId != 0 && !CliDB.GameObjectDisplayInfoStorage.ContainsKey(gInfo.displayId))
                {
                    Log.outError(LogFilter.Sql, $"Gameobject (GUID: {guid} Entry {entry} GoType: {gInfo.type} [{(uint)gInfo.type}]) " +
                        $"has an invalid displayId ({gInfo.displayId}), not loaded.");
                    continue;
                }

                GameObjectData data = new();
                data.SpawnId = guid;
                data.Id = entry;
                data.MapId = result.Read<ushort>(2);
                data.SpawnPoint = new Position(result.Read<float>(3), result.Read<float>(4), result.Read<float>(5), result.Read<float>(6));
                data.rotation.X = result.Read<float>(7);
                data.rotation.Y = result.Read<float>(8);
                data.rotation.Z = result.Read<float>(9);
                data.rotation.W = result.Read<float>(10);
                data.spawntimesecs = result.Read<int>(11);
                data.spawnGroupData = IsTransportMap(data.MapId) ? GetLegacySpawnGroup() : GetDefaultSpawnGroup(); // transport spawns default to compatibility group

                var mapEntry = CliDB.MapStorage.LookupByKey(data.MapId);
                if (mapEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` has gameobject (GUID: {guid} Entry: {data.Id}) spawned on " +
                        $"a non-existed map (Id: {data.MapId}), skip.");
                    continue;
                }

                //if (sWorld->getBoolConfig(CONFIG_GAME_OBJECT_CHECK_INVALID_POSITION))
                //{
                //    if (VMAP::IVMapManager * vmgr = VMAP::VMapFactory::createOrGetVMapManager())
                //    {
                //        if (vmgr->isMapLoadingEnabled() && !IsTransportMap(data.mapId))
                //        {
                //            GridCoord gridCoord = Trinity::ComputeGridCoord(data.spawnPoint.GetPositionX(), data.spawnPoint.GetPositionY());
                //            int gx = (MAX_NUMBER_OF_GRIDS - 1) - gridCoord.x_coord;
                //            int gy = (MAX_NUMBER_OF_GRIDS - 1) - gridCoord.y_coord;

                //            VMAP::LoadResult result = vmgr->existsMap((sWorld->GetDataPath() + "vmaps").c_str(), data.mapId, gx, gy);
                //            if (result != VMAP::LoadResult::Success)
                //                TC_LOG_ERROR("sql.sql", "Table `gameobject` has gameobject (GUID: {} Entry: {} MapID: {}) spawned on a possible invalid position ({})",
                //                    guid, data.id, data.mapId, data.spawnPoint.ToString());
                //        }
                //    }
                //}

                if (data.spawntimesecs == 0 && gInfo.IsDespawnAtAction())
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` has gameobject (GUID: {guid} Entry: {data.Id}) with `spawntimesecs` (0) value, " +
                        "but the gameobejct is marked as despawnable at action.");
                }

                data.animprogress = result.Read<int>(12);
                data.artKit = 0;

                GameObjectState gostate = (GameObjectState)result.Read<uint>(13);
                if (gostate >= GameObjectState.Max)
                {
                    if (gInfo.type != GameObjectTypes.Transport || gostate > GameObjectState.TransportFrameLast)
                    {
                        Log.outError(LogFilter.Sql, $"Table `gameobject` has gameobject (GUID: {guid} Entry: {data.Id}) with invalid `state` " +
                            $"({gostate}) value, skip.");
                        continue;
                    }
                }
                data.goState = gostate;

                data.SpawnDifficulties = ParseSpawnDifficulties(result.Read<string>(14), "gameobject", guid, data.MapId, spawnMasks.LookupByKey(data.MapId));
                if (data.SpawnDifficulties.Empty())
                {
                    Log.outError(LogFilter.Sql, $"Table `creature` has creature (GUID: {guid}) that is not spawned in any difficulty, skipped.");
                    continue;
                }

                short gameEvent = result.Read<sbyte>(15);
                data.poolId = result.Read<int>(16);
                data.PhaseUseFlags = (PhaseUseFlagsValues)result.Read<byte>(17);
                data.PhaseId = result.Read<int>(18);
                data.PhaseGroup = result.Read<int>(19);

                if ((data.PhaseUseFlags & ~PhaseUseFlagsValues.All) != 0)
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` have gameobject (GUID: {guid} Entry: {data.Id}) has unknown `phaseUseFlags` set, removed unknown value.");
                    data.PhaseUseFlags &= PhaseUseFlagsValues.All;
                }

                if (data.PhaseUseFlags.HasFlag(PhaseUseFlagsValues.AlwaysVisible | PhaseUseFlagsValues.Inverse))
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` have gameobject (GUID: {guid} Entry: {data.Id}) has both `phaseUseFlags` " +
                        $"PHASE_USE_FLAGS_ALWAYS_VISIBLE and PHASE_USE_FLAGS_INVERSE, removing PHASE_USE_FLAGS_INVERSE.");
                    data.PhaseUseFlags &= ~PhaseUseFlagsValues.Inverse;
                }

                if (data.PhaseGroup != 0 && data.PhaseId != 0)
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` have gameobject (GUID: {guid} Entry: {data.Id}) with both `phaseid` and " +
                        $"`phasegroup` set, `phasegroup` set to 0.");
                    data.PhaseGroup = 0;
                }

                if (data.PhaseId != 0)
                {
                    if (!CliDB.PhaseStorage.ContainsKey(data.PhaseId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `gameobject` have gameobject (GUID: {guid} Entry: {data.Id}) with `phaseid` {data.PhaseId} " +
                            $"does not exist, set to 0.");
                        data.PhaseId = 0;
                    }
                }

                if (data.PhaseGroup != 0)
                {
                    if (Global.DB2Mgr.GetPhasesForGroup(data.PhaseGroup).Empty())
                    {
                        Log.outError(LogFilter.Sql, $"Table `gameobject` have gameobject (GUID: {guid} Entry: {data.Id}) with `phaseGroup` {data.PhaseGroup} " +
                            $"does not exist, set to 0.");
                        data.PhaseGroup = 0;
                    }
                }

                data.terrainSwapMap = result.Read<int>(20);
                if (data.terrainSwapMap != -1)
                {
                    MapRecord terrainSwapEntry = CliDB.MapStorage.LookupByKey(data.terrainSwapMap);
                    if (terrainSwapEntry == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `gameobject` have gameobject (GUID: {guid} Entry: {data.Id}) with `terrainSwapMap` " +
                            $"{data.terrainSwapMap} does not exist, set to -1.");
                        data.terrainSwapMap = -1;
                    }
                    else if (terrainSwapEntry.ParentMapID != data.MapId)
                    {
                        Log.outError(LogFilter.Sql, $"Table `gameobject` have gameobject (GUID: {guid} Entry: {data.Id}) with `terrainSwapMap` " +
                            $"{data.terrainSwapMap} which cannot be used on spawn map, set to -1.");
                        data.terrainSwapMap = -1;
                    }
                }

                data.ScriptId = GetScriptId(result.Read<string>(21));
                data.StringId = result.Read<string>(22);

                if (data.rotation.X < -1.0f || data.rotation.X > 1.0f)
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` has gameobject (GUID: {guid} Entry: {data.Id}) with invalid rotationX ({data.rotation.X}) value, skip.");
                    continue;
                }

                if (data.rotation.Y < -1.0f || data.rotation.Y > 1.0f)
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` has gameobject (GUID: {guid} Entry: {data.Id}) with invalid rotationY ({data.rotation.Y}) value, skip.");
                    continue;
                }

                if (data.rotation.Z < -1.0f || data.rotation.Z > 1.0f)
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` has gameobject (GUID: {guid} Entry: {data.Id}) with invalid rotationZ ({data.rotation.Z}) value, skip.");
                    continue;
                }

                if (data.rotation.W < -1.0f || data.rotation.W > 1.0f)
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` has gameobject (GUID: {guid} Entry: {data.Id}) with invalid rotationW ({data.rotation.W}) value, skip.");
                    continue;
                }

                if (!GridDefines.IsValidMapCoord(data.MapId, data.SpawnPoint))
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject` has gameobject (GUID: {guid} Entry: {data.Id}) with invalid coordinates, skip.");
                    continue;
                }

                if (!(Math.Abs(Quaternion.Dot(data.rotation, data.rotation) - 1) < 1e-5))
                {
                    Log.outError(LogFilter.Sql,
                        $"Table `gameobject` has gameobject (GUID: {guid} Entry: {data.Id}) with invalid rotation quaternion (non-unit), defaulting to orientation on Z axis only.");
                    data.rotation = Quaternion.CreateFromRotationMatrix(Extensions.fromEulerAnglesZYX(data.SpawnPoint.GetOrientation(), 0f, 0f));
                }

                if (WorldConfig.GetBoolValue(WorldCfg.CalculateGameobjectZoneAreaData))
                {
                    PhasingHandler.InitDbVisibleMapId(phaseShift, data.terrainSwapMap);
                    Global.TerrainMgr.GetZoneAndAreaId(phaseShift, out int zoneId, out int areaId, data.MapId, data.SpawnPoint);

                    PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_GAMEOBJECT_ZONE_AREA_DATA);
                    stmt.AddValue(0, zoneId);
                    stmt.AddValue(1, areaId);
                    stmt.AddValue(2, guid);
                    DB.World.Execute(stmt);
                }

                // if not this is to be managed by GameEvent System
                if (gameEvent == 0)
                    AddGameObjectToGrid(data);

                gameObjectDataStorage[guid] = data;
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} gameobjects in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public void LoadGameObjectAddons()
        {
            uint oldMSTime = Time.GetMSTime();

            _gameObjectAddonStorage.Clear();

            //                                         0     1                 2                 3                 4                 5                 6                  7              8
            SQLResult result = DB.World.Query("SELECT guid, parent_rotation0, parent_rotation1, parent_rotation2, parent_rotation3, invisibilityType, invisibilityValue, WorldEffectID, AIAnimKitID FROM gameobject_addon");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobject addon definitions. DB table `gameobject_addon` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                long guid = result.Read<long>(0);

                GameObjectData goData = GetGameObjectData(guid);
                if (goData == null)
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) does not exist but has a record in `gameobject_addon`.");
                    continue;
                }

                GameObjectAddon gameObjectAddon = new();
                gameObjectAddon.ParentRotation = new Quaternion(result.Read<float>(1), result.Read<float>(2), result.Read<float>(3), result.Read<float>(4));
                gameObjectAddon.invisibilityType = (InvisibilityType)result.Read<byte>(5);
                gameObjectAddon.invisibilityValue = result.Read<uint>(6);
                gameObjectAddon.WorldEffectID = result.Read<int>(7);
                gameObjectAddon.AIAnimKitID = result.Read<int>(8);

                if (gameObjectAddon.invisibilityType >= InvisibilityType.Max)
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has invalid InvisibilityType in `gameobject_addon`, disabled invisibility.");
                    gameObjectAddon.invisibilityType = InvisibilityType.General;
                    gameObjectAddon.invisibilityValue = 0;
                }

                if (gameObjectAddon.invisibilityType != 0 && gameObjectAddon.invisibilityValue == 0)
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has InvisibilityType set but has no InvisibilityValue in `gameobject_addon`, set to 1.");
                    gameObjectAddon.invisibilityValue = 1;
                }

                if (!(Math.Abs(Quaternion.Dot(gameObjectAddon.ParentRotation, gameObjectAddon.ParentRotation) - 1) < 1e-5))
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has invalid parent rotation in `gameobject_addon`, set to default.");
                    gameObjectAddon.ParentRotation = Quaternion.Identity;
                }

                if (gameObjectAddon.WorldEffectID != 0 && !CliDB.WorldEffectStorage.ContainsKey(gameObjectAddon.WorldEffectID))
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has invalid WorldEffectID ({gameObjectAddon.WorldEffectID}) in `gameobject_addon`, set to 0.");
                    gameObjectAddon.WorldEffectID = 0;
                }

                if (gameObjectAddon.AIAnimKitID != 0 && !CliDB.AnimKitStorage.ContainsKey(gameObjectAddon.AIAnimKitID))
                {
                    Log.outError(LogFilter.Sql, $"GameObject (GUID: {guid}) has invalid AIAnimKitID ({gameObjectAddon.AIAnimKitID}) in `gameobject_addon`, set to 0.");
                    gameObjectAddon.AIAnimKitID = 0;
                }

                _gameObjectAddonStorage[guid] = gameObjectAddon;
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} gameobject addons in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadGameObjectQuestItems()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                           0                1     2
            SQLResult result = DB.World.Query("SELECT GameObjectEntry, ItemId, Idx FROM gameobject_questitem ORDER BY Idx ASC");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 gameobject quest items. DB table `gameobject_questitem` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int entry = result.Read<int>(0);
                int item = result.Read<int>(1);
                int idx = result.Read<int>(2);

                if (!_gameObjectTemplateStorage.ContainsKey(entry))
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject_questitem` has data for nonexistent gameobject (entry: {entry}, idx: {idx}), skipped.");
                    continue;
                }

                if (!CliDB.ItemStorage.ContainsKey(item))
                {
                    Log.outError(LogFilter.Sql, $"Table `gameobject_questitem` has nonexistent item (ID: {item}) in gameobject (entry: {entry}, idx: {idx}), skipped.");
                    continue;
                }

                _gameObjectQuestItemStorage.Add(entry, item);

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} gameobject quest items in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadGameObjectForQuests()
        {
            uint oldMSTime = Time.GetMSTime();

            _gameObjectForQuestStorage.Clear();                         // need for reload case

            if (GetGameObjectTemplates().Empty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 GameObjects for quests.");
                return;
            }

            uint count = 0;

            // collect GO entries for GO that must activated
            foreach (var pair in GetGameObjectTemplates())
            {
                switch (pair.Value.type)
                {
                    case GameObjectTypes.QuestGiver:
                        break;
                    case GameObjectTypes.Chest:
                        {
                            // scan GO chest with loot including quest items
                            // find quest loot for GO
                            if (pair.Value.Chest.questID != 0
                                || LootStorage.Gameobject.HaveQuestLootFor(pair.Value.Chest.chestLoot)
                                || LootStorage.Gameobject.HaveQuestLootFor(pair.Value.Chest.chestPersonalLoot)
                                || LootStorage.Gameobject.HaveQuestLootFor(pair.Value.Chest.chestPushLoot))
                                break;

                            continue;
                        }
                    case GameObjectTypes.Generic:
                        {
                            if (pair.Value.Generic.questID > 0)            //quests objects
                                break;

                            continue;
                        }
                    case GameObjectTypes.Goober:
                        {
                            if (pair.Value.Goober.questID > 0)              //quests objects
                                break;

                            continue;
                        }
                    case GameObjectTypes.GatheringNode:
                        {
                            // scan GO chest with loot including quest items
                            // find quest loot for GO
                            if (LootStorage.Gameobject.HaveQuestLootFor(pair.Value.GatheringNode.chestLoot))
                                break;
                            continue;
                        }
                    default:
                        continue;
                }

                _gameObjectForQuestStorage.Add(pair.Value.entry);
                ++count;
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} GameObjects for quests in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void AddGameObjectToGrid(GameObjectData data)
        {
            AddSpawnDataToGrid(data);
        }

        public void RemoveGameObjectFromGrid(GameObjectData data)
        {
            RemoveSpawnDataFromGrid(data);
        }

        public GameObjectAddon GetGameObjectAddon(long lowguid)
        {
            return _gameObjectAddonStorage.LookupByKey(lowguid);
        }

        public List<int> GetGameObjectQuestItemList(int id)
        {
            return _gameObjectQuestItemStorage.LookupByKey(id);
        }

        MultiMap<int, int> GetGameObjectQuestItemMap() { return _gameObjectQuestItemStorage; }

        public Dictionary<long, GameObjectData> GetAllGameObjectData() { return gameObjectDataStorage; }

        public GameObjectData GetGameObjectData(long spawnId)
        {
            return gameObjectDataStorage.LookupByKey(spawnId);
        }

        public void DeleteGameObjectData(long spawnId)
        {
            GameObjectData data = GetGameObjectData(spawnId);
            if (data != null)
            {
                RemoveGameObjectFromGrid(data);
                OnDeleteSpawnData(data);
            }

            gameObjectDataStorage.Remove(spawnId);
        }

        public GameObjectData NewOrExistGameObjectData(long spawnId)
        {
            if (!gameObjectDataStorage.ContainsKey(spawnId))
                gameObjectDataStorage[spawnId] = new GameObjectData();

            return gameObjectDataStorage[spawnId];
        }

        public GameObjectTemplate GetGameObjectTemplate(int entry)
        {
            return _gameObjectTemplateStorage.LookupByKey(entry);
        }

        public GameObjectTemplateAddon GetGameObjectTemplateAddon(int entry)
        {
            return _gameObjectTemplateAddonStorage.LookupByKey(entry);
        }

        public GameObjectOverride GetGameObjectOverride(long spawnId)
        {
            return _gameObjectOverrideStorage.LookupByKey(spawnId);
        }

        public Dictionary<int, GameObjectTemplate> GetGameObjectTemplates()
        {
            return _gameObjectTemplateStorage;
        }

        public bool IsGameObjectForQuests(int entry)
        {
            return _gameObjectForQuestStorage.Contains(entry);
        }

        void CheckGOLockId(GameObjectTemplate goInfo, int dataN, uint N)
        {
            if (CliDB.LockStorage.ContainsKey(dataN))
                return;

            Log.outError(LogFilter.Sql,
                $"Gameobject (Entry: {goInfo.entry} GoType: {goInfo.type}) have data{N}={dataN} but lock (Id: {dataN}) not found.");
        }

        void CheckGOLinkedTrapId(GameObjectTemplate goInfo, int dataN, uint N)
        {
            GameObjectTemplate trapInfo = GetGameObjectTemplate(dataN);
            if (trapInfo != null)
            {
                if (trapInfo.type != GameObjectTypes.Trap)
                    Log.outError(LogFilter.Sql,
                        $"Gameobject (Entry: {goInfo.entry} GoType: {goInfo.type}) have data{N}={dataN} but GO (Entry {dataN}) have not GAMEOBJECT_TYPE_TRAP Type.");
            }
        }

        void CheckGOSpellId(GameObjectTemplate goInfo, int dataN, uint N)
        {
            if (Global.SpellMgr.HasSpellInfo(dataN, Difficulty.None))
                return;

            Log.outError(LogFilter.Sql,
                $"Gameobject (Entry: {goInfo.entry} GoType: {goInfo.type}) have data{N}={dataN} but Spell (Entry {dataN}) not exist.");
        }

        void CheckAndFixGOChairHeightId(GameObjectTemplate goInfo, ref int dataN, uint N)
        {
            if (dataN <= (UnitStandStateType.SitHighChair - UnitStandStateType.SitLowChair))
                return;

            Log.outError(LogFilter.Sql, $"Gameobject (Entry: {goInfo.entry} GoType: {goInfo.type}) have data{N}={dataN} but " +
                $"correct chair height in range 0..{UnitStandStateType.SitHighChair - UnitStandStateType.SitLowChair}.");

            // prevent client and server unexpected work
            dataN = 0;
        }

        void CheckGONoDamageImmuneId(GameObjectTemplate goTemplate, int dataN, uint N)
        {
            // 0/1 correct values
            if (dataN == 0 || dataN == 1)
                return;

            Log.outError(LogFilter.Sql,
                $"Gameobject (Entry: {goTemplate.entry} GoType: {goTemplate.type}) have data{N}={dataN} but expected boolean (0/1) noDamageImmune field value.");
        }

        void CheckGOConsumable(GameObjectTemplate goInfo, int dataN, uint N)
        {
            // 0/1 correct values
            if (dataN == 0 || dataN == 1)
                return;

            Log.outError(LogFilter.Sql,
                $"Gameobject (Entry: {goInfo.entry} GoType: {goInfo.type}) have data{N}={dataN} but expected boolean (0/1) consumable field value.");
        }

        public List<Difficulty> ParseSpawnDifficulties(string difficultyString, string table, long spawnId, int mapId, List<Difficulty> mapDifficulties)
        {
            List<Difficulty> difficulties = new();
            StringArray tokens = new(difficultyString, ',');
            if (tokens.Length == 0)
                return difficulties;

            bool isTransportMap = IsTransportMap(mapId);
            foreach (string token in tokens)
            {
                Difficulty difficultyId = (Difficulty)Enum.Parse(typeof(Difficulty), token);
                if (difficultyId != 0 && !CliDB.DifficultyStorage.ContainsKey(difficultyId))
                {
                    Log.outError(LogFilter.Sql, $"Table `{table}` has {table} (GUID: {spawnId}) with non invalid difficulty id {difficultyId}, skipped.");
                    continue;
                }

                if (!isTransportMap && (mapDifficulties == null || !mapDifficulties.Contains(difficultyId)))
                {
                    Log.outError(LogFilter.Sql, $"Table `{table}` has {table} (GUID: {spawnId}) has unsupported difficulty {difficultyId} for map (Id: {mapId}).");
                    continue;
                }

                difficulties.Add(difficultyId);
            }

            difficulties.Sort();
            return difficulties;
        }

        //Items
        public void LoadItemTemplates()
        {
            var oldMSTime = Time.GetMSTime();
            uint sparseCount = 0;

            foreach (var sparse in CliDB.ItemSparseStorage.Values)
            {
                ItemRecord db2Data = CliDB.ItemStorage.LookupByKey(sparse.Id);
                if (db2Data == null)
                    continue;

                var itemTemplate = new ItemTemplate(db2Data, sparse);
                itemTemplate.MaxDurability = FillMaxDurability(db2Data.ClassID, db2Data.SubclassID, sparse.InventoryType, sparse.OverallQualityID, sparse.ItemLevel);

                var itemSpecOverrides = Global.DB2Mgr.GetItemSpecOverrides(sparse.Id);
                if (itemSpecOverrides != null)
                {
                    foreach (ItemSpecOverrideRecord itemSpecOverride in itemSpecOverrides)
                    {
                        ChrSpecializationRecord specialization = CliDB.ChrSpecializationStorage.LookupByKey(itemSpecOverride.SpecID);
                        if (specialization != null)
                        {
                            itemTemplate.ItemSpecClassMask |= specialization.ClassID.GetClassMask();
                            itemTemplate.Specializations[0].Set(ItemTemplate.CalculateItemSpecBit(specialization), true);
                            itemTemplate.Specializations[1] = itemTemplate.Specializations[1].Or(itemTemplate.Specializations[0]);
                            itemTemplate.Specializations[2] = itemTemplate.Specializations[2].Or(itemTemplate.Specializations[0]);
                        }
                    }
                }
                else
                {
                    ItemSpecStats itemSpecStats = new(db2Data, sparse);

                    foreach (ItemSpecRecord itemSpec in CliDB.ItemSpecStorage.Values)
                    {
                        if (itemSpecStats.ItemType != itemSpec.ItemType)
                            continue;

                        bool hasPrimary = itemSpec.PrimaryStat == ItemSpecStat.None;
                        bool hasSecondary = itemSpec.SecondaryStat == ItemSpecStat.None;
                        for (int i = 0; i < itemSpecStats.ItemSpecStatCount; ++i)
                        {
                            if (itemSpecStats.ItemSpecStatTypes[i] == itemSpec.PrimaryStat)
                                hasPrimary = true;
                            if (itemSpecStats.ItemSpecStatTypes[i] == itemSpec.SecondaryStat)
                                hasSecondary = true;
                        }

                        if (!hasPrimary || !hasSecondary)
                            continue;

                        ChrSpecializationRecord specialization = CliDB.ChrSpecializationStorage.LookupByKey(itemSpec.SpecializationID);
                        if (specialization != null)
                        {
                            if (sparse.AllowableClass.HasClass(specialization.ClassID))
                            {
                                itemTemplate.ItemSpecClassMask |= specialization.ClassID.GetClassMask();
                                int specBit = ItemTemplate.CalculateItemSpecBit(specialization);
                                itemTemplate.Specializations[0].Set(specBit, true);
                                if (itemSpec.MaxLevel > 40)
                                    itemTemplate.Specializations[1].Set(specBit, true);
                                if (itemSpec.MaxLevel >= 110)
                                    itemTemplate.Specializations[2].Set(specBit, true);
                            }
                        }
                    }
                }

                // Items that have no specializations set can be used by everyone
                foreach (var specs in itemTemplate.Specializations)
                    if (specs.Count == 0)
                        specs.SetAll(true);

                if (Global.DB2Mgr.GetItemEffectsForItemId(sparse.Id) is List<ItemEffectRecord> itemEffects)
                    foreach (var itemEffect in itemEffects)
                        itemTemplate.Effects.Add(itemEffect);

                ++sparseCount;
                ItemTemplateStorage.Add(sparse.Id, itemTemplate);
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {sparseCount} item templates in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        static float[] qualityMultipliers =
        [
            0.92f, 0.92f, 0.92f, 1.11f, 1.32f, 1.61f, 0.0f, 0.0f
        ];

        static float[] armorMultipliers =
        [
            0.00f, // INVTYPE_NON_EQUIP
            0.60f, // INVTYPE_HEAD
            0.00f, // INVTYPE_NECK
            0.60f, // INVTYPE_SHOULDERS
            0.00f, // INVTYPE_BODY
            1.00f, // INVTYPE_CHEST
            0.33f, // INVTYPE_WAIST
            0.72f, // INVTYPE_LEGS
            0.48f, // INVTYPE_FEET
            0.33f, // INVTYPE_WRISTS
            0.33f, // INVTYPE_HANDS
            0.00f, // INVTYPE_FINGER
            0.00f, // INVTYPE_TRINKET
            0.00f, // INVTYPE_WEAPON
            0.72f, // INVTYPE_SHIELD
            0.00f, // INVTYPE_RANGED
            0.00f, // INVTYPE_CLOAK
            0.00f, // INVTYPE_2HWEAPON
            0.00f, // INVTYPE_BAG
            0.00f, // INVTYPE_TABARD
            1.00f, // INVTYPE_ROBE
            0.00f, // INVTYPE_WEAPONMAINHAND
            0.00f, // INVTYPE_WEAPONOFFHAND
            0.00f, // INVTYPE_HOLDABLE
            0.00f, // INVTYPE_AMMO
            0.00f, // INVTYPE_THROWN
            0.00f, // INVTYPE_RANGEDRIGHT
            0.00f, // INVTYPE_QUIVER
            0.00f, // INVTYPE_RELIC
            0.00f, // INVTYPE_PROFESSION_TOOL
            0.00f, // INVTYPE_PROFESSION_GEAR
            0.00f, // INVTYPE_EQUIPABLE_SPELL_OFFENSIVE
            0.00f, // INVTYPE_EQUIPABLE_SPELL_UTILITY
            0.00f, // INVTYPE_EQUIPABLE_SPELL_DEFENSIVE
            0.00f, // INVTYPE_EQUIPABLE_SPELL_MOBILITY
        ];

        static float[] weaponMultipliers =
        [
            0.91f, // ITEM_SUBCLASS_WEAPON_AXE
            1.00f, // ITEM_SUBCLASS_WEAPON_AXE2
            1.00f, // ITEM_SUBCLASS_WEAPON_BOW
            1.00f, // ITEM_SUBCLASS_WEAPON_GUN
            0.91f, // ITEM_SUBCLASS_WEAPON_MACE
            1.00f, // ITEM_SUBCLASS_WEAPON_MACE2
            1.00f, // ITEM_SUBCLASS_WEAPON_POLEARM
            0.91f, // ITEM_SUBCLASS_WEAPON_SWORD
            1.00f, // ITEM_SUBCLASS_WEAPON_SWORD2
            1.00f, // ITEM_SUBCLASS_WEAPON_WARGLAIVES
            1.00f, // ITEM_SUBCLASS_WEAPON_STAFF
            0.00f, // ITEM_SUBCLASS_WEAPON_EXOTIC
            0.00f, // ITEM_SUBCLASS_WEAPON_EXOTIC2
            0.66f, // ITEM_SUBCLASS_WEAPON_FIST_WEAPON
            0.00f, // ITEM_SUBCLASS_WEAPON_MISCELLANEOUS
            0.66f, // ITEM_SUBCLASS_WEAPON_DAGGER
            0.00f, // ITEM_SUBCLASS_WEAPON_THROWN
            0.00f, // ITEM_SUBCLASS_WEAPON_SPEAR
            1.00f, // ITEM_SUBCLASS_WEAPON_CROSSBOW
            0.66f, // ITEM_SUBCLASS_WEAPON_WAND
            0.66f, // ITEM_SUBCLASS_WEAPON_FISHING_POLE
        ];

        int FillMaxDurability(ItemClass itemClass, ItemSubClass itemSubClass, InventoryType inventoryType, ItemQuality quality, int itemLevel)
        {
            if (itemClass != ItemClass.Armor && itemClass != ItemClass.Weapon)
                return 0;

            float levelPenalty = 1.0f;
            if (itemLevel <= 28)
                levelPenalty = 0.966f - (28u - itemLevel) / 54.0f;

            if (itemClass == ItemClass.Armor)
            {
                if (inventoryType > InventoryType.Robe)
                    return 0;

                return 5 * (int)(Math.Round(25.0f * qualityMultipliers[(int)quality] * armorMultipliers[(int)inventoryType] * levelPenalty));
            }

            return 5 * (int)(Math.Round(18.0f * qualityMultipliers[(int)quality] * weaponMultipliers[itemSubClass.data] * levelPenalty));
        }

        public void LoadItemTemplateAddon()
        {
            var time = Time.GetMSTime();

            uint count = 0;

            //                                        0   1        2         3             4             5                6
            SQLResult result = DB.World.Query("SELECT Id, FlagsCu, FoodType, MinMoneyLoot, MaxMoneyLoot, SpellPPMChance, QuestLogItemId FROM item_template_addon");
            if (!result.IsEmpty())
            {
                do
                {
                    int itemId = result.Read<int>(0);
                    ItemTemplate itemTemplate = GetItemTemplate(itemId);
                    if (itemTemplate == null)
                    {
                        Log.outError(LogFilter.Sql, $"Item {itemId} specified in `itemtemplateaddon` does not exist, skipped.");
                        continue;
                    }

                    uint minMoneyLoot = result.Read<uint>(3);
                    uint maxMoneyLoot = result.Read<uint>(4);
                    if (minMoneyLoot > maxMoneyLoot)
                    {
                        Log.outError(LogFilter.Sql, $"Minimum money loot specified in `itemtemplateaddon` for item {itemId} was greater than maximum amount, swapping.");
                        (minMoneyLoot, maxMoneyLoot) = (maxMoneyLoot, minMoneyLoot);
                    }
                    itemTemplate.FlagsCu = (ItemFlagsCustom)result.Read<uint>(1);
                    itemTemplate.FoodType = result.Read<byte>(2);
                    itemTemplate.MinMoneyLoot = minMoneyLoot;
                    itemTemplate.MaxMoneyLoot = maxMoneyLoot;
                    itemTemplate.SpellPPMRate = result.Read<float>(5);
                    itemTemplate.QuestLogItemId = result.Read<int>(6);
                    ++count;
                } while (result.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} item addon templates in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public void LoadItemScriptNames()
        {
            uint oldMSTime = Time.GetMSTime();
            uint count = 0;

            SQLResult result = DB.World.Query("SELECT Id, ScriptName FROM item_script_names");
            if (!result.IsEmpty())
            {
                do
                {
                    int itemId = result.Read<int>(0);
                    if (GetItemTemplate(itemId) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Item {itemId} specified in `item_script_names` does not exist, skipped.");
                        continue;
                    }

                    ItemTemplateStorage[itemId].ScriptId = GetScriptId(result.Read<string>(1));
                    ++count;
                } while (result.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} item script names in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public ItemTemplate GetItemTemplate(int ItemId)
        {
            return ItemTemplateStorage.LookupByKey(ItemId);
        }

        public Dictionary<int, ItemTemplate> GetItemTemplates()
        {
            return ItemTemplateStorage;
        }

        public Trainer GetTrainer(int trainerId)
        {
            return trainers.LookupByKey(trainerId);
        }

        public void AddVendorItem(int entry, VendorItem vItem, bool persist = true)
        {
            VendorItemData vList = cacheVendorItemStorage[entry];
            vList.AddItem(vItem);

            if (persist)
            {
                PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.INS_NPC_VENDOR);

                stmt.AddValue(0, entry);
                stmt.AddValue(1, vItem.item);
                stmt.AddValue(2, vItem.maxcount);
                stmt.AddValue(3, vItem.incrtime);
                stmt.AddValue(4, vItem.ExtendedCost);
                stmt.AddValue(5, (byte)vItem.Type);

                DB.World.Execute(stmt);
            }
        }

        public bool RemoveVendorItem(int entry, int item, ItemVendorType type, bool persist = true)
        {
            var iter = cacheVendorItemStorage.LookupByKey(entry);
            if (iter == null)
                return false;

            if (!iter.RemoveItem(item, type))
                return false;

            if (persist)
            {
                PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.DEL_NPC_VENDOR);

                stmt.AddValue(0, entry);
                stmt.AddValue(1, item);
                stmt.AddValue(2, (byte)type);

                DB.World.Execute(stmt);
            }

            return true;
        }

        public bool IsVendorItemValid(int vendorentry, VendorItem vItem, Player player = null, List<int> skipvendors = null, NPCFlags1 ORnpcflag = 0)
        {
            CreatureTemplate cInfo = GetCreatureTemplate(vendorentry);
            if (cInfo == null)
            {
                if (player != null)
                    player.SendSysMessage(CypherStrings.CommandVendorselection);
                else
                    Log.outError(LogFilter.Sql, $"Table `(gameevent)npcvendor` have data for not existed creature template (Entry: {vendorentry}), ignore.");
                return false;
            }

            if (!(cInfo.Npcflag | ORnpcflag).HasAnyFlag(NPCFlags1.Vendor))
            {
                if (skipvendors == null || skipvendors.Count == 0)
                {
                    if (player != null)
                        player.SendSysMessage(CypherStrings.CommandVendorselection);
                    else
                        Log.outError(LogFilter.Sql, $"Table `(gameevent)npcvendor` have data for not creature template (Entry: {vendorentry}) without vendor flag, ignore");

                    if (skipvendors != null)
                        skipvendors.Add(vendorentry);
                }
                return false;
            }

            if ((vItem.Type == ItemVendorType.Item && GetItemTemplate(vItem.item) == null) ||
                (vItem.Type == ItemVendorType.Currency && CliDB.CurrencyTypesStorage.LookupByKey(vItem.item) == null))
            {
                if (player != null)
                    player.SendSysMessage(CypherStrings.ItemNotFound, vItem.item, vItem.Type);
                else
                    Log.outError(LogFilter.Sql,
                        $"Table `(gameevent)npcvendor` for Vendor (Entry: {vendorentry}) have in item list non-existed item ({vItem.item}, Type {vItem.Type}), ignore.");
                return false;
            }

            if (vItem.PlayerConditionId != 0 && !CliDB.PlayerConditionStorage.ContainsKey(vItem.PlayerConditionId))
            {
                Log.outError(LogFilter.Sql,
                    $"Table `(game_event_)npc_vendor` has Item (Entry: {vItem.item}) with invalid PlayerConditionId ({vItem.PlayerConditionId}) for vendor ({vendorentry}), ignore.");
                return false;
            }

            if (vItem.ExtendedCost != 0 && !CliDB.ItemExtendedCostStorage.ContainsKey(vItem.ExtendedCost))
            {
                if (player != null)
                    player.SendSysMessage(CypherStrings.ExtendedCostNotExist, vItem.ExtendedCost);
                else
                    Log.outError(LogFilter.Sql,
                        $"Table `(gameevent)npcvendor` have Item (Entry: {vItem.item}) with wrong ExtendedCost ({vItem.ExtendedCost}) for vendor ({vendorentry}), ignore.");
                return false;
            }

            if (vItem.Type == ItemVendorType.Item) // not applicable to currencies
            {
                if (vItem.maxcount > 0 && vItem.incrtime == 0)
                {
                    if (player != null)
                        player.SendSysMessage($"MaxCount != 0 ({vItem.maxcount}) but IncrTime == 0.");
                    else
                        Log.outError(LogFilter.Sql, 
                            $"Table `(gameevent)npcvendor` has `maxcount` ({vItem.maxcount}) for item {vItem.item} of vendor (Entry: {vendorentry}) but `incrtime`=0, ignore.");
                    return false;
                }
                else if (vItem.maxcount == 0 && vItem.incrtime > 0)
                {
                    if (player != null)
                        player.SendSysMessage("MaxCount == 0 but IncrTime<>= 0");
                    else
                        Log.outError(LogFilter.Sql, 
                            $"Table `(gameevent)npcvendor` has `maxcount`=0 for item {vItem.item} of vendor (Entry: {vendorentry}) but `incrtime`<>0, ignore.");
                    return false;
                }
            }

            VendorItemData vItems = GetNpcVendorItemList(vendorentry);
            if (vItems == null)
                return true;                                        // later checks for non-empty lists

            if (vItems.FindItemCostPair(vItem.item, vItem.ExtendedCost, vItem.Type) != null)
            {
                if (player != null)
                    player.SendSysMessage(CypherStrings.ItemAlreadyInList, vItem.item, vItem.ExtendedCost, vItem.Type);
                else
                    Log.outError(LogFilter.Sql,
                        $"Table `npcvendor` has duplicate items {vItem.item} (with extended cost {vItem.ExtendedCost}, Type {vItem.Type}) for vendor (Entry: {vendorentry}), ignoring.");
                return false;
            }

            if (vItem.Type == ItemVendorType.Currency && vItem.maxcount == 0)
            {
                Log.outError(LogFilter.Sql,
                    $"Table `(game_event_)npc_vendor` have Item (Entry: {vItem.item}, Type: {vItem.Type}) with missing maxcount for vendor ({vendorentry}), ignore.");
                return false;
            }

            return true;
        }

        public VendorItemData GetNpcVendorItemList(int entry)
        {
            return cacheVendorItemStorage.LookupByKey(entry);
        }

        public List<float> GetCreatureTemplateSparringValues(int entry)
        {
            return _creatureTemplateSparringStorage.LookupByKey(entry);
        }

        public CreatureMovementData GetCreatureMovementOverride(long spawnId)
        {
            return creatureMovementOverrides.LookupByKey(spawnId);
        }

        public EquipmentInfo GetEquipmentInfo(int entry, int id)
        {
            var equip = equipmentInfoStorage.LookupByKey(entry);
            if (equip.Empty())
                return null;

            if (id == -1)
                return equip[RandomHelper.IRand(0, equip.Count - 1)].Item2;
            else
                return equip.Find(p => p.Item1 == id).Item2;
        }

        //Maps
        public void LoadInstanceTemplate()
        {
            var time = Time.GetMSTime();

            //                                          0     1       2
            SQLResult result = DB.World.Query("SELECT map, parent, script FROM instance_template");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 instance templates. DB table `instance_template` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                int mapID = result.Read<int>(0);

                if (!Global.MapMgr.IsValidMAP(mapID))
                {
                    Log.outError(LogFilter.Sql, $"ObjectMgr.LoadInstanceTemplate: bad mapid {mapID} for template!");
                    continue;
                }

                var instanceTemplate = new InstanceTemplate();
                instanceTemplate.Parent = result.Read<ushort>(1);
                instanceTemplate.ScriptId = GetScriptId(result.Read<string>(2));

                instanceTemplateStorage.Add(mapID, instanceTemplate);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} instance templates in {Time.GetMSTimeDiffToNow(time)} ms.");
        }

        public void LoadGameTele()
        {
            uint oldMSTime = Time.GetMSTime();

            gameTeleStorage.Clear();

            //                                        0       1           2           3           4        5     6
            SQLResult result = DB.World.Query("SELECT id, position_x, position_y, position_z, orientation, map, name FROM game_tele");

            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "Loaded 0 GameTeleports. DB table `game_tele` is empty!");
                return;
            }

            uint count = 0;

            do
            {
                int id = result.Read<int>(0);

                GameTele gt = new();

                gt.posX = result.Read<float>(1);
                gt.posY = result.Read<float>(2);
                gt.posZ = result.Read<float>(3);
                gt.orientation = result.Read<float>(4);
                gt.mapId = result.Read<int>(5);
                gt.name = result.Read<string>(6);

                gt.nameLow = gt.name.ToLowerInvariant();

                if (!GridDefines.IsValidMapCoord(gt.mapId, gt.posX, gt.posY, gt.posZ, gt.orientation))
                {
                    Log.outError(LogFilter.Sql, $"Wrong position for id {id} (name: {gt.name}) in `game_tele` table, ignoring.");
                    continue;
                }
                gameTeleStorage.Add(id, gt);
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} GameTeleports in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadAreaTriggerTeleports()
        {
            uint oldMSTime = Time.GetMSTime();

            _areaTriggerStorage.Clear();                                  // need for reload case

            //                                         0   1
            SQLResult result = DB.World.Query("SELECT ID, PortLocID FROM areatrigger_teleport");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 area trigger teleport definitions. DB table `areatrigger_teleport` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                ++count;

                int Trigger_ID = result.Read<int>(0);
                int PortLocID = result.Read<int>(1);

                WorldSafeLocsEntry portLoc = GetWorldSafeLoc(PortLocID);
                if (portLoc == null)
                {
                    Log.outError(LogFilter.Sql, $"Area Trigger (ID: {Trigger_ID}) has a non-existing Port Loc (ID: {PortLocID}) in WorldSafeLocs.dbc, skipped.");
                    continue;
                }

                AreaTriggerStruct at = new();
                at.target_mapId = portLoc.Loc.GetMapId();
                at.target_X = portLoc.Loc.GetPositionX();
                at.target_Y = portLoc.Loc.GetPositionY();
                at.target_Z = portLoc.Loc.GetPositionZ();
                at.target_Orientation = portLoc.Loc.GetOrientation();

                AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(Trigger_ID);
                if (atEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Area trigger (ID: {Trigger_ID}) does not exist in `AreaTrigger.dbc`.");
                    continue;
                }

                _areaTriggerStorage[Trigger_ID] = at;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} area trigger teleport definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadAccessRequirements()
        {
            uint oldMSTime = Time.GetMSTime();

            _accessRequirementStorage.Clear();

            //                                          0      1           2          3          4     5      6             7             8                      9     
            SQLResult result = DB.World.Query("SELECT mapid, difficulty, level_min, level_max, item, item2, quest_done_A, quest_done_H, completed_achievement, quest_failed_text FROM access_requirement");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 access requirement definitions. DB table `access_requirement` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int mapid = result.Read<int>(0);
                if (!CliDB.MapStorage.ContainsKey(mapid))
                {
                    Log.outError(LogFilter.Sql, $"Map {mapid} referenced in `access_requirement` does not exist, skipped.");
                    continue;
                }

                Difficulty difficulty = (Difficulty)result.Read<byte>(1);
                if (Global.DB2Mgr.GetMapDifficultyData(mapid, difficulty) == null)
                {
                    Log.outError(LogFilter.Sql, $"Map {mapid} referenced in `access_requirement` does not have difficulty {difficulty}, skipped.");
                    continue;
                }

                (int, Difficulty) requirementId = (mapid, difficulty);

                AccessRequirement ar = new();
                ar.levelMin = result.Read<byte>(2);
                ar.levelMax = result.Read<byte>(3);
                ar.item = result.Read<int>(4);
                ar.item2 = result.Read<int>(5);
                ar.quest_A = result.Read<int>(6);
                ar.quest_H = result.Read<int>(7);
                ar.achievement = result.Read<int>(8);
                ar.questFailedText = result.Read<string>(9);

                if (ar.item != 0)
                {
                    ItemTemplate pProto = GetItemTemplate(ar.item);
                    if (pProto == null)
                    {
                        Log.outError(LogFilter.Sql, $"Key item {ar.item} does not exist for map {mapid} difficulty {difficulty}, removing key requirement.");
                        ar.item = 0;
                    }
                }

                if (ar.item2 != 0)
                {
                    ItemTemplate pProto = GetItemTemplate(ar.item2);
                    if (pProto == null)
                    {
                        Log.outError(LogFilter.Sql, $"Second item {ar.item2} does not exist for map {mapid} difficulty {difficulty}, removing key requirement.");
                        ar.item2 = 0;
                    }
                }

                if (ar.quest_A != 0)
                {
                    if (GetQuestTemplate(ar.quest_A) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Required Alliance Quest {ar.quest_A} not exist for map {mapid} difficulty {difficulty}, remove quest done requirement.");
                        ar.quest_A = 0;
                    }
                }

                if (ar.quest_H != 0)
                {
                    if (GetQuestTemplate(ar.quest_H) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Required Horde Quest {ar.quest_H} not exist for map {mapid} difficulty {difficulty}, remove quest done requirement.");
                        ar.quest_H = 0;
                    }
                }

                if (ar.achievement != 0)
                {
                    if (!CliDB.AchievementStorage.ContainsKey(ar.achievement))
                    {
                        Log.outError(LogFilter.Sql, $"Required Achievement {ar.achievement} not exist for map {mapid} difficulty {difficulty}, remove quest done requirement.");
                        ar.achievement = 0;
                    }
                }

                _accessRequirementStorage[requirementId] = ar;
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} access requirement definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }
        public void LoadSpawnGroupTemplates()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0        1          2
            SQLResult result = DB.World.Query("SELECT groupId, groupName, groupFlags FROM spawn_group_template");
            if (!result.IsEmpty())
            {
                do
                {
                    int groupId = result.Read<int>(0);
                    SpawnGroupTemplateData group = new();
                    group.groupId = groupId;
                    group.name = result.Read<string>(1);
                    group.mapId = -1;
                    SpawnGroupFlags flags = (SpawnGroupFlags)result.Read<int>(2);
                    if (flags.HasAnyFlag(~SpawnGroupFlags.All))
                    {
                        flags &= SpawnGroupFlags.All;
                        Log.outError(LogFilter.Sql, $"Invalid spawn group flag {flags} on group ID {groupId} ({group.name}), reduced to valid flag {group.flags}.");
                    }
                    if (flags.HasFlag(SpawnGroupFlags.System | SpawnGroupFlags.ManualSpawn))
                    {
                        flags &= ~SpawnGroupFlags.ManualSpawn;
                        Log.outError(LogFilter.Sql, $"System spawn group {groupId} ({group.name}) has invalid manual spawn flag. Ignored.");
                    }
                    group.flags = flags;

                    _spawnGroupDataStorage[groupId] = group;
                } while (result.NextRow());
            }

            if (!_spawnGroupDataStorage.ContainsKey(0))
            {
                Log.outError(LogFilter.Sql, "Default spawn group (index 0) is missing from DB! Manually inserted.");
                SpawnGroupTemplateData data = new();
                data.groupId = 0;
                data.name = "Default Group";
                data.mapId = 0;
                data.flags = SpawnGroupFlags.System;
                _spawnGroupDataStorage[0] = data;
            }

            if (!_spawnGroupDataStorage.ContainsKey(1))
            {
                Log.outError(LogFilter.Sql, "Default legacy spawn group (index 1) is missing from DB! Manually inserted.");
                SpawnGroupTemplateData data = new();
                data.groupId = 1;
                data.name = "Legacy Group";
                data.mapId = 0;
                data.flags = SpawnGroupFlags.System | SpawnGroupFlags.CompatibilityMode;
                _spawnGroupDataStorage[1] = data;
            }

            if (!result.IsEmpty())
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {_spawnGroupDataStorage.Count} spawn group templates in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
            else
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spawn group templates. DB table `spawn_group_template` is empty.");
        }

        public void LoadSpawnGroups()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0        1          2
            SQLResult result = DB.World.Query("SELECT groupId, spawnType, spawnId FROM spawn_group");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spawn group members. DB table `spawn_group` is empty.");
                return;
            }

            uint numMembers = 0;
            do
            {
                int groupId = result.Read<int>(0);
                SpawnObjectType spawnType = (SpawnObjectType)result.Read<byte>(1);
                if (!SpawnData.TypeIsValid(spawnType))
                {
                    Log.outError(LogFilter.Sql, $"Spawn data with invalid Type {spawnType} listed for spawn group {groupId}. Skipped.");
                    continue;
                }
                long spawnId = result.Read<long>(2);

                SpawnMetadata data = GetSpawnMetadata(spawnType, spawnId);
                if (data == null)
                {
                    Log.outError(LogFilter.Sql, $"Spawn data with ID ({spawnType},{spawnId}) not found, but is listed as a member of spawn group {groupId}!");
                    continue;
                }
                else if (data.spawnGroupData.groupId != 0)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spawn with ID ({spawnType},{spawnId}) is listed as a member of spawn group {groupId}, but is already a member of spawn group {data.spawnGroupData.groupId}. Skipping.");
                    continue;
                }
                var groupTemplate = _spawnGroupDataStorage.LookupByKey(groupId);
                if (groupTemplate == null)
                {
                    Log.outError(LogFilter.Sql, $"Spawn group {groupId} assigned to spawn ID ({spawnType},{spawnId}), but group is found!");
                    continue;
                }
                else
                {
                    if (groupTemplate.mapId == -1)
                    {
                        groupTemplate.mapId = data.MapId;
                        _spawnGroupsByMap.Add(data.MapId, groupId);
                    }
                    else if (groupTemplate.mapId != data.MapId && !groupTemplate.flags.HasAnyFlag(SpawnGroupFlags.System))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spawn group {groupId} has map ID {groupTemplate.mapId}, but spawn ({spawnType},{spawnId}) has map id {data.MapId} - spawn NOT added to group!");
                        continue;
                    }
                    data.spawnGroupData = groupTemplate;
                    if (!groupTemplate.flags.HasAnyFlag(SpawnGroupFlags.System))
                        _spawnGroupMapStorage.Add(groupId, data);
                    ++numMembers;
                }
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {numMembers} spawn group members in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadInstanceSpawnGroups()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0              1            2           3             4
            SQLResult result = DB.World.Query("SELECT instanceMapId, bossStateId, bossStates, spawnGroupId, flags FROM instance_spawn_groups");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 instance spawn groups. DB table `instance_spawn_groups` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int instanceMapId = result.Read<ushort>(0);
                int spawnGroupId = result.Read<int>(3);
                var spawnGroupTemplate = _spawnGroupDataStorage.LookupByKey(spawnGroupId);
                if (spawnGroupTemplate == null || spawnGroupTemplate.flags.HasAnyFlag(SpawnGroupFlags.System))
                {
                    Log.outError(LogFilter.Sql, $"Invalid spawn group {spawnGroupId} specified for instance {instanceMapId}. Skipped.");
                    continue;
                }

                if (spawnGroupTemplate.mapId != instanceMapId)
                {
                    Log.outError(LogFilter.Sql, $"Instance spawn group {spawnGroupId} specified for instance {instanceMapId} has spawns on a different map {spawnGroupTemplate.mapId}. Skipped.");
                    continue;
                }

                InstanceSpawnGroupInfo info = new();
                info.SpawnGroupId = spawnGroupId;
                info.BossStateId = result.Read<byte>(1);

                EncounterStateMask states = (EncounterStateMask)result.Read<byte>(2);
                if (states.HasAnyFlag(~EncounterStateMask.All))
                {
                    info.BossStates = states & EncounterStateMask.All;
                    Log.outError(LogFilter.Sql, $"Instance spawn group ({instanceMapId},{spawnGroupId}) had invalid boss state mask " +
                        $"{states} [{(byte)states}] - truncated to {info.BossStates} [{(byte)info.BossStates}].");
                }
                else
                    info.BossStates = states;

                InstanceSpawnGroupFlags flags = (InstanceSpawnGroupFlags)result.Read<byte>(4);
                if (flags.HasAnyFlag(~InstanceSpawnGroupFlags.All))
                {
                    info.Flags = flags & InstanceSpawnGroupFlags.All;
                    Log.outError(LogFilter.Sql, $"Instance spawn group ({instanceMapId},{spawnGroupId}) had invalid flags {flags} - truncated to {info.Flags}.");
                }
                else
                    info.Flags = flags;

                if (flags.HasFlag(InstanceSpawnGroupFlags.AllianceOnly | InstanceSpawnGroupFlags.HordeOnly))
                {
                    info.Flags = flags & ~(InstanceSpawnGroupFlags.AllianceOnly | InstanceSpawnGroupFlags.HordeOnly);
                    Log.outError(LogFilter.Sql,
                        $"Instance spawn group ({instanceMapId},{spawnGroupId}) FLAG_ALLIANCE_ONLY and FLAG_HORDE_ONLY may not be used together in a single entry - truncated to {info.Flags}.");
                }

                _instanceSpawnGroupStorage.Add(instanceMapId, info);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} instance spawn groups in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        void OnDeleteSpawnData(SpawnData data)
        {
            var templateIt = _spawnGroupDataStorage.LookupByKey(data.spawnGroupData.groupId);
            Cypher.Assert(templateIt != null, $"Creature data for ({data.type},{data.SpawnId}) is being deleted and has invalid spawn group index {data.spawnGroupData.groupId}!");

            if (templateIt.flags.HasAnyFlag(SpawnGroupFlags.System)) // system groups don't store their members in the map
                return;

            var spawnDatas = _spawnGroupMapStorage.LookupByKey(data.spawnGroupData.groupId);
            foreach (var it in spawnDatas)
            {
                if (it != data)
                    continue;
                _spawnGroupMapStorage.Remove(data.spawnGroupData.groupId, it);
                return;
            }

            Cypher.Assert(false, $"Spawn data ({data.type},{data.SpawnId}) being removed is member of spawn group {data.spawnGroupData.groupId}, but not actually listed in the lookup table for that group!");
        }

        public Dictionary<int, InstanceTemplate> GetInstanceTemplates() { return instanceTemplateStorage; }

        public InstanceTemplate GetInstanceTemplate(int mapID)
        {
            return instanceTemplateStorage.LookupByKey(mapID);
        }

        public GameTele GetGameTele(int id)
        {
            return gameTeleStorage.LookupByKey(id);
        }

        public GameTele GetGameTele(string name)
        {
            name = name.ToLower();

            // Alternative first GameTele what contains wnameLow as substring in case no GameTele location found
            GameTele alt = null;
            foreach (var tele in gameTeleStorage.Values)
            {
                if (tele.nameLow == name)
                    return tele;
                else if (alt == null && tele.nameLow.Contains(name))
                    alt = tele;
            }

            return alt;
        }

        public GameTele GetGameTeleExactName(string name)
        {
            name = name.ToLower();
            foreach (var tele in gameTeleStorage.Values)
            {
                if (tele.nameLow == name)
                    return tele;
            }

            return null;
        }

        public bool AddGameTele(GameTele tele)
        {
            // find max id
            int newId = 0;
            foreach (var itr in gameTeleStorage.Keys)
                if (itr > newId)
                    newId = itr;

            // use next
            ++newId;

            gameTeleStorage[newId] = tele;

            PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.INS_GAME_TELE);

            stmt.AddValue(0, newId);
            stmt.AddValue(1, tele.posX);
            stmt.AddValue(2, tele.posY);
            stmt.AddValue(3, tele.posZ);
            stmt.AddValue(4, tele.orientation);
            stmt.AddValue(5, tele.mapId);
            stmt.AddValue(6, tele.name);

            DB.World.Execute(stmt);

            return true;
        }

        public bool DeleteGameTele(string name)
        {
            name = name.ToLowerInvariant();

            int? keyToDelete = null;

            foreach (var pair in gameTeleStorage)
            {
                if (pair.Value.nameLow == name)
                {
                    PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.DEL_GAME_TELE);
                    stmt.AddValue(0, pair.Value.name);
                    DB.World.Execute(stmt);

                    keyToDelete = pair.Key;
                    break;
                }
            }

            if (keyToDelete.HasValue)
            {
                gameTeleStorage.Remove(keyToDelete.Value);
                return true;
            }
            
            return false;
        }

        public bool IsTransportMap(int mapId) { return _transportMaps.Contains((ushort)mapId); }

        public SpawnGroupTemplateData GetSpawnGroupData(int groupId) { return _spawnGroupDataStorage.LookupByKey(groupId); }

        public SpawnGroupTemplateData GetSpawnGroupData(SpawnObjectType type, long spawnId)
        {
            SpawnMetadata data = GetSpawnMetadata(type, spawnId);
            return data != null ? data.spawnGroupData : null;
        }

        public SpawnGroupTemplateData GetDefaultSpawnGroup() { return _spawnGroupDataStorage.ElementAt(0).Value; }
        public SpawnGroupTemplateData GetLegacySpawnGroup() { return _spawnGroupDataStorage.ElementAt(1).Value; }
        public List<SpawnMetadata> GetSpawnMetadataForGroup(int groupId) { return _spawnGroupMapStorage.LookupByKey(groupId); }
        public List<int> GetSpawnGroupsForMap(int mapId) { return _spawnGroupsByMap.LookupByKey(mapId); }

        public SpawnMetadata GetSpawnMetadata(SpawnObjectType type, long spawnId)
        {
            if (SpawnData.TypeHasData(type))
                return GetSpawnData(type, spawnId);
            else
                return null;
        }

        public SpawnData GetSpawnData(SpawnObjectType type, long spawnId)
        {
            if (!SpawnData.TypeHasData(type))
                return null;

            switch (type)
            {
                case SpawnObjectType.Creature:
                    return GetCreatureData(spawnId);
                case SpawnObjectType.GameObject:
                    return GetGameObjectData(spawnId);
                case SpawnObjectType.AreaTrigger:
                    return Global.AreaTriggerDataStorage.GetAreaTriggerSpawn(spawnId);
                default:
                    Cypher.Assert(false, $"Invalid spawn object Type {type} [{(byte)type}]");
                    return null;
            }
        }

        public List<InstanceSpawnGroupInfo> GetInstanceSpawnGroupsForMap(int mapId) { return _instanceSpawnGroupStorage.LookupByKey(mapId); }

        //Player
        public void LoadPlayerInfo()
        {
            var time = Time.GetMSTime();
            // Load playercreate
            {
                //                                         0     1      2    3           4           5           6            7        8               9               10              11               12                  13              14              15
                SQLResult result = DB.World.Query("SELECT race, class, map, position_x, position_y, position_z, orientation, npe_map, npe_position_x, npe_position_y, npe_position_z, npe_orientation, npe_transport_guid, intro_movie_id, intro_scene_id, npe_intro_scene_id FROM playercreateinfo");
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player create definitions. DB table `playercreateinfo` is empty.");
                    return;
                }

                uint count = 0;
                do
                {
                    Race currentrace = (Race)result.Read<byte>(0);
                    Class currentclass = (Class)result.Read<byte>(1);
                    int mapId = result.Read<int>(2);
                    float positionX = result.Read<float>(3);
                    float positionY = result.Read<float>(4);
                    float positionZ = result.Read<float>(5);
                    float orientation = result.Read<float>(6);

                    if (!CliDB.ChrRacesStorage.ContainsKey((int)currentrace))
                    {
                        Log.outError(LogFilter.Sql, $"Wrong race {currentrace} in `playercreateinfo` table, ignoring.");
                        continue;
                    }

                    if (!CliDB.ChrClassesStorage.ContainsKey((int)currentclass))
                    {
                        Log.outError(LogFilter.Sql, $"Wrong class {currentclass} in `playercreateinfo` table, ignoring.");
                        continue;
                    }

                    // accept DB data only for valid position (and non instanceable)
                    if (!GridDefines.IsValidMapCoord(mapId, positionX, positionY, positionZ, orientation))
                    {
                        Log.outError(LogFilter.Sql, $"Wrong home position for class {currentclass} race {currentrace} pair in `playercreateinfo` table, ignoring.");
                        continue;
                    }

                    if (CliDB.MapStorage.LookupByKey(mapId).Instanceable)
                    {
                        Log.outError(LogFilter.Sql, $"Home position in instanceable map for class {currentclass} race {currentrace} pair in `playercreateinfo` table, ignoring.");
                        continue;
                    }

                    if (Global.DB2Mgr.GetChrModel(currentrace, Gender.Male) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Missing male model for race {currentrace}, ignoring.");
                        continue;
                    }

                    if (Global.DB2Mgr.GetChrModel(currentrace, Gender.Female) == null)
                    {
                        Log.outError(LogFilter.Sql, $"Missing female model for race {currentrace}, ignoring.");
                        continue;
                    }

                    PlayerInfo info = new();
                    info.createPosition.Loc = new WorldLocation(mapId, positionX, positionY, positionZ, orientation);

                    if (!result.IsNull(7))
                    {
                        PlayerInfo.CreatePosition createPosition = new();

                        createPosition.Loc = new WorldLocation(result.Read<int>(7), result.Read<float>(8), result.Read<float>(9), result.Read<float>(10), result.Read<float>(11));
                        if (!result.IsNull(12))
                            createPosition.TransportGuid = result.Read<long>(12);

                        info.createPositionNPE = createPosition;

                        if (!CliDB.MapStorage.ContainsKey(info.createPositionNPE.Value.Loc.GetMapId()))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Invalid NPE map id {info.createPositionNPE.Value.Loc.GetMapId()} for class {currentclass} race {currentrace} pair in `playercreateinfo` table, ignoring.");
                            info.createPositionNPE = null;
                        }

                        if (info.createPositionNPE.HasValue && info.createPositionNPE.Value.TransportGuid.HasValue && Global.TransportMgr.GetTransportSpawn(info.createPositionNPE.Value.TransportGuid.Value) == null)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Invalid NPE transport spawn id {info.createPositionNPE.Value.TransportGuid.Value} for class {currentclass} race {currentrace} pair in `playercreateinfo` table, ignoring.");
                            info.createPositionNPE = null; // remove entire NPE data - assume user put transport offsets into npe_position fields
                        }
                    }

                    if (!result.IsNull(13))
                    {
                        int introMovieId = result.Read<int>(13);
                        if (CliDB.MovieStorage.ContainsKey(introMovieId))
                            info.introMovieId = introMovieId;
                        else
                            Log.outError(LogFilter.Sql, 
                                $"Invalid intro movie id {introMovieId} for class {currentclass} race {currentrace} pair in `playercreateinfo` table, ignoring.");
                    }

                    if (!result.IsNull(14))
                    {
                        int introSceneId = result.Read<int>(14);
                        if (GetSceneTemplate(introSceneId) != null)
                            info.introSceneId = introSceneId;
                        else
                            Log.outError(LogFilter.Sql, 
                                $"Invalid intro scene id {introSceneId} for class {currentclass} race {currentrace} pair in `playercreateinfo` table, ignoring.");
                    }

                    if (!result.IsNull(15))
                    {
                        int introSceneId = result.Read<int>(15);
                        if (GetSceneTemplate(introSceneId) != null)
                            info.introSceneIdNPE = introSceneId;
                        else
                            Log.outError(LogFilter.Sql, 
                                $"Invalid NPE intro scene id {introSceneId} for class {currentclass} race {currentrace} pair in `playercreateinfo` table, ignoring.");
                    }

                    _playerInfo[(currentrace, currentclass)] = info;

                    ++count;
                } while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} player create definitions in {Time.GetMSTimeDiffToNow(time)} ms.");
            }

            time = Time.GetMSTime();
            // Load playercreate items
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Items Data...");
            {
                MultiMap<int, ItemTemplate> itemsByCharacterLoadout = new();
                foreach (CharacterLoadoutItemRecord characterLoadoutItem in CliDB.CharacterLoadoutItemStorage.Values)
                {
                    ItemTemplate itemTemplate = GetItemTemplate(characterLoadoutItem.ItemID);
                    if (itemTemplate != null)
                        itemsByCharacterLoadout.Add(characterLoadoutItem.CharacterLoadoutID, itemTemplate);
                }

                foreach (CharacterLoadoutRecord characterLoadout in CliDB.CharacterLoadoutStorage.Values)
                {
                    if (!characterLoadout.IsForNewCharacter)
                        continue;

                    var items = itemsByCharacterLoadout.LookupByKey(characterLoadout.Id);
                    if (items.Empty())
                        continue;

                    var raceMask = characterLoadout.RaceMask;

                    for (var raceIndex = Race.Human; raceIndex < Race.Max; ++raceIndex)
                    {

                        if (!raceMask.HasRace(raceIndex))
                            continue;

                        var playerInfo = _playerInfo.LookupByKey((raceIndex, characterLoadout.ChrClassID));
                        if (playerInfo != null)
                        {
                            playerInfo.itemContext = characterLoadout.ItemContext;

                            foreach (ItemTemplate itemTemplate in items)
                            {
                                // BuyCount by default
                                int count = itemTemplate.GetBuyCount();

                                // special amount for food/drink
                                if (itemTemplate.GetClass() == ItemClass.Consumable && itemTemplate.GetSubClass().Consumable == ItemSubClassConsumable.FoodDrink)
                                {
                                    if (!itemTemplate.Effects.Empty())
                                    {
                                        switch (itemTemplate.Effects[0].SpellCategoryID)
                                        {
                                            case SpellCategories.Food:                                // food
                                                count = characterLoadout.ChrClassID == Class.Deathknight ? 10 : 4;
                                                break;
                                            case SpellCategories.Drink:                                // drink
                                                count = 2;
                                                break;
                                        }
                                    }
                                    if (itemTemplate.GetMaxStackSize() < count)
                                        count = itemTemplate.GetMaxStackSize();
                                }

                                playerInfo.item.Add(new PlayerCreateInfoItem(itemTemplate.GetId(), count));
                            }
                        }
                    }
                }
            }

            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Items Override Data...");
            {
                //                                         0     1      2       3
                SQLResult result = DB.World.Query("SELECT race, class, itemid, amount FROM playercreateinfo_item");

                if (result.IsEmpty())
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 custom player create items. DB table `playercreateinfo_item` is empty.");
                else
                {
                    uint count = 0;
                    do
                    {
                        Race currentrace = (Race)result.Read<byte>(0);
                        if (currentrace >= Race.Max)
                        {
                            Log.outError(LogFilter.Sql, $"Wrong race {currentrace} in `playercreateinfo_item` table, ignoring.");
                            continue;
                        }

                        Class currentclass = (Class)result.Read<byte>(1);
                        if (currentclass >= Class.Max)
                        {
                            Log.outError(LogFilter.Sql, $"Wrong class {currentclass} in `playercreateinfo_item` table, ignoring.");
                            continue;
                        }

                        int itemid = result.Read<int>(2);
                        if (GetItemTemplate(itemid).GetId() == 0)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Item id {itemid} (race {currentrace} class {currentclass}) in `playercreateinfo_item` table but not listed in `itemtemplate`, ignoring.");
                            continue;
                        }

                        int amount = result.Read<int>(3);

                        if (amount == 0)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Item id {itemid} (class {currentrace} race {currentclass}) have amount == 0 in `playercreateinfo_item` table, ignoring.");
                            continue;
                        }

                        if (currentrace == Race.None || currentclass == Class.None)
                        {
                            Race minrace = currentrace != Race.None ? currentrace : Race.None + 1;
                            Race maxrace = currentrace != Race.None ? currentrace + 1 : Race.Max;
                            Class minclass = currentclass != Class.None ? currentclass : Class.None + 1;
                            Class maxclass = currentclass != Class.None ? currentclass + 1 : Class.Max;
                            for (var r = minrace; r < maxrace; ++r)
                                for (var c = minclass; c < maxclass; ++c)
                                    PlayerCreateInfoAddItemHelper(r, c, itemid, amount);

                        }
                        else
                            PlayerCreateInfoAddItemHelper(currentrace, currentclass, itemid, amount);

                        ++count;
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} custom player create items in {Time.GetMSTimeDiffToNow(time)} ms.");
                }
            }

            // Load playercreate skills
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Skill Data...");
            {
                uint oldMSTime = Time.GetMSTime();

                foreach (SkillRaceClassInfoRecord rcInfo in CliDB.SkillRaceClassInfoStorage.Values)
                {
                    if (rcInfo.Availability == 1)
                    {
                        var raceMask = rcInfo.RaceMask;
                        for (Race raceIndex = Race.Human; raceIndex < Race.Max; ++raceIndex)
                        {
                            if (raceMask == RaceMask.None || raceMask.HasRace(raceIndex))
                            {
                                for (Class classIndex = Class.Warrior; classIndex < Class.Max; ++classIndex)
                                {
                                    if (rcInfo.ClassMask.HasClass(classIndex))
                                    {
                                        if (_playerInfo.LookupByKey((raceIndex, classIndex)) is PlayerInfo info)
                                            info.skills.Add(rcInfo);
                                    }
                                }
                            }
                        }
                    }
                }
                Log.outInfo(LogFilter.ServerLoading, $"Loaded player create skills in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
            }

            // Load playercreate custom spells
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Custom Spell Data...");
            {
                uint oldMSTime = Time.GetMSTime();

                //                                        0          1          2
                SQLResult result = DB.World.Query("SELECT racemask, classmask, Spell FROM playercreateinfo_spell_custom");
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player create custom spells. DB table `playercreateinfo_spell_custom` is empty.");
                }
                else
                {
                    uint count = 0;
                    do
                    {
                        RaceMask raceMask = (RaceMask)result.Read<ulong>(0);
                        ClassMask classMask = (ClassMask)result.Read<uint>(1);
                        int spellId = result.Read<int>(2);

                        if (raceMask != RaceMask.None && !raceMask.HasAnyFlag(RaceMask.Playable))
                        {
                            Log.outError(LogFilter.Sql, $"Wrong race mask {raceMask} in `playercreateinfo_spell_custom` table, ignoring.");
                            continue;
                        }

                        if (classMask != ClassMask.None && !classMask.HasAnyFlag(ClassMask.Playable))
                        {
                            Log.outError(LogFilter.Sql, $"Wrong class mask {classMask} in `playercreateinfo_spell_custom` table, ignoring.");
                            continue;
                        }

                        for (Race raceIndex = Race.Human; raceIndex < Race.Max; ++raceIndex)
                        {
                            if (raceMask == RaceMask.None || raceMask.HasRace(raceIndex))
                            {
                                for (Class classIndex = Class.Warrior; classIndex < Class.Max; ++classIndex)
                                {
                                    if (classMask == ClassMask.None || classMask.HasClass(classIndex))
                                    {
                                        PlayerInfo playerInfo = _playerInfo.LookupByKey((raceIndex, classIndex));
                                        if (playerInfo != null)
                                        {
                                            playerInfo.customSpells.Add(spellId);
                                            ++count;
                                        }
                                        // We need something better here, the check is not accounting for spells used by multiple races/classes but not all of them.
                                        // Either split the masks per class, or per race, which kind of kills the point yet.
                                        // else if (raceMask != 0 && classMask != 0)
                                        //     TC_LOG_ERROR("sql.sql", "Racemask/classmask ({}/{}) combination was found containing an invalid race/class combination ({}/{}) in `{}` (Spell {}), ignoring.", raceMask, classMask, raceIndex, classIndex, tableName, spellId);
                                    }
                                }
                            }
                        }
                    }
                    while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} custom player create spells in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
                }
            }

            // Load playercreate cast spell
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Cast Spell Data...");
            {
                uint oldMSTime = Time.GetMSTime();

                //                                          0           1       2       3
                SQLResult result = DB.World.Query("SELECT raceMask, classMask, spell, createMode FROM playercreateinfo_cast_spell");

                if (result.IsEmpty())
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player create cast spells. DB table `playercreateinfo_cast_spell` is empty.");
                else
                {
                    uint count = 0;

                    do
                    {
                        RaceMask raceMask = (RaceMask)result.Read<ulong>(0);
                        ClassMask classMask = (ClassMask)result.Read<uint>(1);
                        int spellId = result.Read<int>(2);
                        PlayerCreateMode playerCreateMode = (PlayerCreateMode)result.Read<sbyte>(3);

                        if (raceMask != RaceMask.None && !raceMask.HasAnyFlag(RaceMask.Playable))
                        {
                            Log.outError(LogFilter.Sql, $"Wrong race mask {raceMask} in `playercreateinfo_cast_spell` table, ignoring.");
                            continue;
                        }

                        if (classMask != ClassMask.None && !classMask.HasAnyFlag(ClassMask.Playable))
                        {
                            Log.outError(LogFilter.Sql, $"Wrong class mask {classMask} in `playercreateinfo_cast_spell` table, ignoring.");
                            continue;
                        }

                        if (playerCreateMode <= PlayerCreateMode.None || playerCreateMode >= PlayerCreateMode.Max)
                        {
                            Log.outError(LogFilter.Sql, $"Uses invalid createMode {playerCreateMode} in `playercreateinfo_cast_spell` table, ignoring.");
                            continue;
                        }

                        for (Race raceIndex = Race.Human; raceIndex < Race.Max; ++raceIndex)
                        {
                            if (raceMask == RaceMask.None || raceMask.HasRace(raceIndex))
                            {
                                for (Class classIndex = Class.Warrior; classIndex < Class.Max; ++classIndex)
                                {
                                    if (classMask == ClassMask.None || classMask.HasClass(classIndex))
                                    {
                                        if (_playerInfo.LookupByKey((raceIndex, classIndex)) is PlayerInfo info)
                                        {
                                            info.castSpells[(int)playerCreateMode].Add(spellId);
                                            ++count;
                                        }
                                    }
                                }
                            }
                        }
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} player create cast spells in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
                }
            }

            // Load playercreate actions
            time = Time.GetMSTime();
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Action Data...");
            {
                //                                         0     1      2       3       4
                SQLResult result = DB.World.Query("SELECT race, class, button, action, Type FROM playercreateinfo_action");

                if (result.IsEmpty())
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player create actions. DB table `playercreateinfo_action` is empty.");
                else
                {
                    uint count = 0;
                    do
                    {
                        Race currentrace = (Race)result.Read<byte>(0);
                        if (currentrace >= Race.Max)
                        {
                            Log.outError(LogFilter.Sql, $"Wrong race {currentrace} in `playercreateinfo_action` table, ignoring.");
                            continue;
                        }

                        Class currentclass = (Class)result.Read<byte>(1);
                        if (currentclass >= Class.Max)
                        {
                            Log.outError(LogFilter.Sql, $"Wrong class {currentclass} in `playercreateinfo_action` table, ignoring.");
                            continue;
                        }

                        if (_playerInfo.LookupByKey((currentrace, currentclass)) is PlayerInfo info)
                            info.action.Add(new PlayerCreateInfoAction(result.Read<byte>(2), result.Read<int>(3), result.Read<byte>(4)));

                        ++count;
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} player create actions in {Time.GetMSTimeDiffToNow(time)} ms.");
                }
            }

            time = Time.GetMSTime();
            // Loading levels data (class/race dependent)
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Level Stats Data...");
            {
                short[][] raceStatModifiers = new short[(int)Race.Max][];
                for (var i = 0; i < (int)Race.Max; ++i)
                    raceStatModifiers[i] = new short[(int)Stats.Max];

                {
                    //                                          0     1    2    3    4     5
                    SQLResult result = DB.World.Query("SELECT race, str, agi, sta, inte, spi FROM player_racestats");
                
                    if (result.IsEmpty())
                    {
                        Log.outInfo(LogFilter.ServerLoading, "Loaded 0 level stats definitions. DB table `player_racestats` is empty.");
                        Global.WorldMgr.StopNow();
                        return;
                    }

                    do
                    {
                        Race currentrace = (Race)result.Read<byte>(0);
                        if (currentrace >= Race.Max)
                        {
                            Log.outError(LogFilter.Sql, $"Wrong race {currentrace} in `player_racestats` table, ignoring.");
                            continue;
                        }

                        for (int i = 0; i < (int)Stats.Max; ++i)
                            raceStatModifiers[(int)currentrace][i] = result.Read<short>(i + 1);

                    } while (result.NextRow());
                }

                {
                    //                                           0      1      2    3    4    5     6
                    SQLResult result = DB.World.Query("SELECT class, level, str, agi, sta, inte, spi FROM player_classlevelstats");
                
                    if (result.IsEmpty())
                    {
                        Log.outInfo(LogFilter.ServerLoading, "Loaded 0 level stats definitions. DB table `player_classlevelstats` is empty.");
                        Global.WorldMgr.StopNow();
                        return;
                    }

                    uint count = 0;

                    do
                    {
                        Class currentclass = (Class)result.Read<byte>(0);
                        if (currentclass >= Class.Max)
                        {
                            Log.outError(LogFilter.Sql, $"Wrong class {currentclass} in `player_classlevelstats` table, ignoring.");
                            continue;
                        }

                        uint currentlevel = result.Read<byte>(1);
                        if (currentlevel > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                        {
                            if (currentlevel > SharedConst.StrongMaxLevel)        // hardcoded level maximum
                                Log.outError(LogFilter.Sql, $"Wrong (> {SharedConst.StrongMaxLevel}) level {currentlevel} in `player_classlevelstats` table, ignoring.");
                            else
                                Log.outError(LogFilter.Sql, $"Unused (> MaxPlayerLevel in worldserver.conf) level {currentlevel} in `player_levelstats` table, ignoring.");

                            continue;
                        }

                        for (var race = 0; race < raceStatModifiers.Length; ++race)
                        {
                            if (_playerInfo.LookupByKey(((Race)race, currentclass)) is PlayerInfo playerInfo)
                            {
                                for (var i = 0; i < (int)Stats.Max; i++)
                                    playerInfo.levelInfo[currentlevel - 1].stats[i] = (ushort)(result.Read<ushort>(i + 2) + raceStatModifiers[race][i]);
                            }
                        }

                        ++count;
                    } while (result.NextRow());


                    // Fill gaps and check integrity
                    for (Race race = 0; race < Race.Max; ++race)
                    {
                        // skip non existed races
                        if (!CliDB.ChrRacesStorage.ContainsKey((int)race))
                            continue;

                        for (Class class_ = 0; class_ < Class.Max; ++class_)
                        {
                            // skip non existed classes
                            if (CliDB.ChrClassesStorage.LookupByKey((int)class_) == null)
                                continue;

                            var playerInfo = _playerInfo.LookupByKey((race, class_));
                            if (playerInfo == null)
                                continue;

                            // skip expansion races if not playing with expansion
                            if ((Expansion)WorldConfig.GetIntValue(WorldCfg.Expansion) < Expansion.BurningCrusade && (race == Race.BloodElf || race == Race.Draenei))
                                continue;

                            // skip expansion classes if not playing with expansion
                            if ((Expansion)WorldConfig.GetIntValue(WorldCfg.Expansion) < Expansion.WrathOfTheLichKing && class_ == Class.Deathknight)
                                continue;

                            if ((Expansion)WorldConfig.GetIntValue(WorldCfg.Expansion) < Expansion.MistsOfPandaria && (race == Race.PandarenNeutral || race == Race.PandarenHorde || race == Race.PandarenAlliance))
                                continue;

                            if ((Expansion)WorldConfig.GetIntValue(WorldCfg.Expansion) < Expansion.Legion && class_ == Class.DemonHunter)
                                continue;

                            if ((Expansion)WorldConfig.GetIntValue(WorldCfg.Expansion) < Expansion.Dragonflight && class_ == Class.Evoker)
                                continue;

                            // fatal error if no level 1 data
                            if (playerInfo.levelInfo[0].stats[0] == 0)
                            {
                                Log.outError(LogFilter.Sql, $"Race {race} Class {class_} Level 1 does not have stats data!");
                                Environment.Exit(1);
                                return;
                            }

                            // fill level gaps
                            for (var level = 1; level < WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel); ++level)
                            {
                                if (playerInfo.levelInfo[level].stats[0] == 0)
                                {
                                    Log.outError(LogFilter.Sql, $"Race {race} Class {class_} Level {level + 1} does not have stats data. Using stats data of level {level}.");
                                    playerInfo.levelInfo[level] = playerInfo.levelInfo[level - 1];
                                }
                            }
                        }
                    }
                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} level stats definitions in {Time.GetMSTimeDiffToNow(time)} ms.");
                }
            }

            time = Time.GetMSTime();
            // Loading xp per level data
            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create XP Data...");
            {
                _playerXPperLevel = new int[WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel)];

                //                                          0      1
                SQLResult result = DB.World.Query("SELECT Level, Experience FROM player_xp_for_level");

                if (result.IsEmpty())
                {
                    Log.outError(LogFilter.ServerLoading, $"Loaded 0 xp for level definitions. DB table `player_xp_for_level` is empty.");
                    Global.WorldMgr.StopNow();
                    return;
                }

                uint count = 0;
                // ...overwrite if needed (custom values)

                do
                {
                    int currentlevel = result.Read<byte>(0);
                    int currentxp = result.Read<int>(1);

                    if (currentlevel >= WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                    {
                        if (currentlevel > SharedConst.StrongMaxLevel)        // hardcoded level maximum
                            Log.outError(LogFilter.Sql, $"Wrong (> {SharedConst.StrongMaxLevel}) level {currentlevel} in `player_xp_for_levels` table, ignoring.");
                        else
                        {
                            Log.outError(LogFilter.Sql, $"Unused (> MaxPlayerLevel in worldserver.conf) level {currentlevel} in `player_xp_for_levels` table, ignoring.");
                            ++count;                                // make result loading percent "expected" correct in case disabled detail mode for example.
                        }
                        continue;
                    }
                    //PlayerXPperLevel
                    _playerXPperLevel[currentlevel] = currentxp;
                    ++count;
                } while (result.NextRow());

                // fill level gaps
                for (var level = 1; level < WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel); ++level)
                {
                    if (_playerXPperLevel[level] == 0)
                    {
                        Log.outError(LogFilter.Sql, $"Level {level + 1} does not have XP for level data. Using data of level [{level}] + 100XP.");
                        _playerXPperLevel[level] = _playerXPperLevel[level - 1] + 100;
                    }
                }

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} xp for level definition(s) from database in {Time.GetMSTimeDiffToNow(time)} ms.");
            }
        }

        public float GetOCTRegenHP(Class class_, int level)
        {
            if (level < 1 || class_ >= Class.Max)
                return 0.0f;

            if (level > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                level = WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel);

            GtOCTRegenHPRecord octRegenHP = CliDB.OCTRegenHPGameTable.GetRow(level);
            if (octRegenHP is null)
            {
                Log.outError(LogFilter.Misc, $"Tried to get non-existant Class-Level combination data from OCTRegenHP. Class {class_} Level {level}.");
                return 0.0f;
            }

            return CliDB.GetGameTableColumnForClass(octRegenHP, class_);
        }

        public float GetRegenHPPerSpt(Class class_, int level)
        {
            if (level < 1 || class_ >= Class.Max)
                return 0.0f;

            if (level > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                level = WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel);

            GtRegenHPPerSptRecord regenHPPerSpt = CliDB.RegenHPPerSptGameTable.GetRow(level);
            if (regenHPPerSpt is null)
            {
                Log.outError(LogFilter.Misc, $"Tried to get non-existant Class-Level combination data from RegenHPPerSpt. Class {class_} Level {level}.");
                return 0.0f;
            }
            return CliDB.GetGameTableColumnForClass(regenHPPerSpt, class_);
        }

        public float GetRegenMPPerSpt(Class class_, int level)
        {
            if (level < 1 || class_ >= Class.Max)
                return 0.0f;

            if (level > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                level = WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel);

            GtRegenMPPerSptRecord regenMPPerSpt = CliDB.RegenMPPerSptGameTable.GetRow(level);
            if (regenMPPerSpt is null)
            {
                Log.outError(LogFilter.Misc, $"Tried to get non-existant Class-Level combination data from RegenMPPerSpt. Class {class_} Level {level}.");
                return 0.0f;
            }

            return CliDB.GetGameTableColumnForClass(regenMPPerSpt, class_);
        }

        void PlayerCreateInfoAddItemHelper(Race race, Class class_, int itemId, int count)
        {
            var playerInfo = _playerInfo.LookupByKey((race, class_));
            if (playerInfo == null)
                return;

            if (count > 0)
                playerInfo.item.Add(new PlayerCreateInfoItem(itemId, count));
            else
            {
                if (count < -1)
                    Log.outError(LogFilter.Sql, $"Invalid count {count} specified on item {itemId} be removed from original player create info (use -1)!");

                playerInfo.item.RemoveAll(item => item.item_id == itemId);
            }
        }

        public PlayerInfo GetPlayerInfo(Race raceId, Class classId)
        {
            if (raceId >= Race.Max)
                return null;

            if (classId >= Class.Max)
                return null;

            var info = _playerInfo.LookupByKey((raceId, classId));
            if (info == null)
                return null;

            return info;
        }

        public void GetPlayerClassLevelInfo(Class _class, int level, out int baseMana)
        {
            baseMana = 0;
            if (level < 1 || _class >= Class.Max)
                return;

            if (level > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                level = (byte)WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel);

            GtBaseMPRecord mp = CliDB.BaseMPGameTable.GetRow(level);
            if (mp == null)
            {
                Log.outError(LogFilter.Sql, $"Tried to get non-existant Class-Level combination data for base mp. Class {_class} Level {level}.");
                return;
            }

            baseMana = (int)CliDB.GetGameTableColumnForClass(mp, _class);
        }

        public PlayerLevelInfo GetPlayerLevelInfo(Race race, Class _class, int level)
        {
            if (level < 1 || race >= Race.Max || _class >= Class.Max)
                return null;

            PlayerInfo pInfo = _playerInfo.LookupByKey((race, _class));
            if (pInfo == null)
                return null;

            if (level <= WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                return pInfo.levelInfo[level - 1];
            else
                return BuildPlayerLevelInfo(race, _class, level);
        }

        PlayerLevelInfo BuildPlayerLevelInfo(Race race, Class _class, int level)
        {
            // base data (last known level)
            var info = _playerInfo.LookupByKey((race, _class)).levelInfo[WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel) - 1];

            for (int lvl = WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel) - 1; lvl < level; ++lvl)
            {
                switch (_class)
                {
                    case Class.Warrior:
                        info.stats[0] += (lvl > 23 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[1] += (lvl > 23 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[2] += (lvl > 36 ? 1 : (lvl > 6 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[3] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        break;
                    case Class.Paladin:
                        info.stats[0] += (lvl > 3 ? 1 : 0);
                        info.stats[1] += (lvl > 33 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[2] += (lvl > 38 ? 1 : (lvl > 7 && (lvl % 2) == 0 ? 1 : 0));
                        info.stats[3] += (lvl > 6 && (lvl % 2) != 0 ? 1 : 0);
                        break;
                    case Class.Hunter:
                        info.stats[0] += (lvl > 4 ? 1 : 0);
                        info.stats[1] += (lvl > 4 ? 1 : 0);
                        info.stats[2] += (lvl > 33 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[3] += (lvl > 8 && (lvl % 2) != 0 ? 1 : 0);
                        break;
                    case Class.Rogue:
                        info.stats[0] += (lvl > 5 ? 1 : 0);
                        info.stats[1] += (lvl > 4 ? 1 : 0);
                        info.stats[2] += (lvl > 16 ? 2 : (lvl > 1 ? 1 : 0));
                        info.stats[3] += (lvl > 8 && (lvl % 2) == 0 ? 1 : 0);
                        break;
                    case Class.Priest:
                        info.stats[0] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[1] += (lvl > 5 ? 1 : 0);
                        info.stats[2] += (lvl > 38 ? 1 : (lvl > 8 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[3] += (lvl > 22 ? 2 : (lvl > 1 ? 1 : 0));
                        break;
                    case Class.Shaman:
                        info.stats[0] += (lvl > 34 ? 1 : (lvl > 6 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[1] += (lvl > 4 ? 1 : 0);
                        info.stats[2] += (lvl > 7 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[3] += (lvl > 5 ? 1 : 0);
                        break;
                    case Class.Mage:
                        info.stats[0] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[1] += (lvl > 5 ? 1 : 0);
                        info.stats[2] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[3] += (lvl > 24 ? 2 : (lvl > 1 ? 1 : 0));
                        break;
                    case Class.Warlock:
                        info.stats[0] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[1] += (lvl > 38 ? 2 : (lvl > 3 ? 1 : 0));
                        info.stats[2] += (lvl > 9 && (lvl % 2) == 0 ? 1 : 0);
                        info.stats[3] += (lvl > 33 ? 2 : (lvl > 2 ? 1 : 0));
                        break;
                    case Class.Druid:
                        info.stats[0] += (lvl > 38 ? 2 : (lvl > 6 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[1] += (lvl > 32 ? 2 : (lvl > 4 ? 1 : 0));
                        info.stats[2] += (lvl > 38 ? 2 : (lvl > 8 && (lvl % 2) != 0 ? 1 : 0));
                        info.stats[3] += (lvl > 38 ? 3 : (lvl > 4 ? 1 : 0));
                        break;
                }
            }

            return info;
        }

        //Pets
        public void LoadPetLevelInfo()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0               1      2   3     4    5    6    7     8    9
            SQLResult result = DB.World.Query("SELECT creature_entry, level, hp, mana, str, agi, sta, inte, spi, armor FROM pet_levelstats");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 level pet stats definitions. DB table `pet_levelstats` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int creatureid = result.Read<int>(0);
                if (GetCreatureTemplate(creatureid) == null)
                {
                    Log.outError(LogFilter.Sql, $"Wrong creature id {creatureid} in `pet_levelstats` table, ignoring.");
                    continue;
                }

                int currentlevel = result.Read<int>(1);
                if (currentlevel > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                {
                    if (currentlevel > SharedConst.StrongMaxLevel)        // hardcoded level maximum
                        Log.outError(LogFilter.Sql, $"Wrong (> {SharedConst.StrongMaxLevel}) level {currentlevel} in `pet_levelstats` table, ignoring.");
                    else
                    {
                        Log.outInfo(LogFilter.Server, $"Unused (> MaxPlayerLevel in worldserver.conf) level {currentlevel} in `pet_levelstats` table, ignoring.");
                        ++count;                                // make result loading percent "expected" correct in case disabled detail mode for example.
                    }
                    continue;
                }
                else if (currentlevel < 1)
                {
                    Log.outError(LogFilter.Sql, $"Wrong (<1) level {currentlevel} in `pet_levelstats` table, ignoring.");
                    continue;
                }

                var pInfoMapEntry = petInfoStore.LookupByKey(creatureid);

                if (pInfoMapEntry == null)
                    pInfoMapEntry = new PetLevelInfo[WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel)];

                PetLevelInfo pLevelInfo = new();
                pLevelInfo.health = result.Read<int>(2);
                pLevelInfo.mana = result.Read<int>(3);
                pLevelInfo.armor = result.Read<int>(9);

                for (int i = 0; i < (int)Stats.Max; i++)
                {
                    pLevelInfo.stats[i] = result.Read<int>(i + 4);
                }

                pInfoMapEntry[currentlevel - 1] = pLevelInfo;

                ++count;
            }
            while (result.NextRow());

            // Fill gaps and check integrity
            foreach (var map in petInfoStore)
            {
                var pInfo = map.Value;

                // fatal error if no level 1 data
                if (pInfo == null || pInfo[0].health == 0)
                {
                    Log.outError(LogFilter.Sql, $"Creature {map.Key} does not have pet stats data for Level 1!");
                    Global.WorldMgr.StopNow();
                }

                // fill level gaps
                for (byte level = 1; level < WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel); ++level)
                {
                    if (pInfo[level].health == 0)
                    {
                        Log.outError(LogFilter.Sql, $"Creature {map.Key} has no data for Level {level + 1} pet stats data, using data of Level {level}.");
                        pInfo[level] = pInfo[level - 1];
                    }
                }
            }
            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} level pet stats definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadPetNames()
        {
            uint oldMSTime = Time.GetMSTime();
            //                                          0     1      2
            SQLResult result = DB.World.Query("SELECT word, entry, half FROM pet_name_generation");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 pet name parts. DB table `pet_name_generation` is empty!");
                return;
            }

            uint count = 0;

            do
            {
                string word = result.Read<string>(0);
                int entry = result.Read<int>(1);
                bool half = result.Read<bool>(2);
                if (half)
                    _petHalfName1.Add(entry, word);
                else
                    _petHalfName0.Add(entry, word);
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} pet name parts in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadPetNumber()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.Characters.Query("SELECT MAX(id) FROM character_pet");
            if (!result.IsEmpty())
                _hiPetNumber = result.Read<int>(0) + 1;

            Log.outInfo(LogFilter.ServerLoading, $"Loaded the max pet number: {_hiPetNumber - 1} in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public PetLevelInfo GetPetLevelInfo(int creatureid, int level)
        {
            if (level > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                level = WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel);

            var petinfo = petInfoStore.LookupByKey(creatureid);

            if (petinfo == null)
                return null;

            return petinfo[level - 1];                           // data for level 1 stored in [0] array element, ...
        }

        public string GeneratePetName(int entry)
        {
            var list0 = _petHalfName0[entry];
            var list1 = _petHalfName1[entry];

            if (list0.Empty() || list1.Empty())
            {
                CreatureTemplate cinfo = GetCreatureTemplate(entry);
                if (cinfo == null)
                    return string.Empty;

                string petname = Global.DB2Mgr.GetCreatureFamilyPetName(cinfo.Family, Global.WorldMgr.GetDefaultDbcLocale());
                if (!string.IsNullOrEmpty(petname))
                    return petname;
                else
                    return cinfo.Name;
            }

            return list0[RandomHelper.IRand(0, list0.Count - 1)] + list1[RandomHelper.IRand(0, list1.Count - 1)];
        }

        //Faction Change
        public void LoadFactionChangeAchievements()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_achievement");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change achievement pairs. DB table `player_factionchange_achievement` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int alliance = result.Read<int>(0);
                int horde = result.Read<int>(1);

                if (!CliDB.AchievementStorage.ContainsKey(alliance))
                    Log.outError(LogFilter.Sql, $"Achievement {alliance} (alliance_id) referenced in `player_factionchange_achievement` does not exist, pair skipped!");
                else if (!CliDB.AchievementStorage.ContainsKey(horde))
                    Log.outError(LogFilter.Sql, $"Achievement {horde} (horde_id) referenced in `player_factionchange_achievement` does not exist, pair skipped!");
                else
                    FactionChangeAchievements[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} faction change achievement pairs in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadFactionChangeItems()
        {
            uint oldMSTime = Time.GetMSTime();

            uint count = 0;
            foreach (var itemPair in ItemTemplateStorage)
            {
                if (itemPair.Value.GetOtherFactionItemId() == 0)
                    continue;

                if (itemPair.Value.HasFlag(ItemFlags2.FactionHorde))
                    FactionChangeItemsHordeToAlliance[itemPair.Key] = itemPair.Value.GetOtherFactionItemId();

                if (itemPair.Value.HasFlag(ItemFlags2.FactionAlliance))
                    FactionChangeItemsAllianceToHorde[itemPair.Key] = itemPair.Value.GetOtherFactionItemId();

                ++count;
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} faction change item pairs in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadFactionChangeQuests()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_quests");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change quest pairs. DB table `player_factionchange_quests` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int alliance = result.Read<int>(0);
                int horde = result.Read<int>(1);

                if (GetQuestTemplate(alliance) == null)
                    Log.outError(LogFilter.Sql, $"Quest {alliance} (alliance_id) referenced in `player_factionchange_quests` does not exist, pair skipped!");
                else if (GetQuestTemplate(horde) == null)
                    Log.outError(LogFilter.Sql, $"Quest {horde} (horde_id) referenced in `player_factionchange_quests` does not exist, pair skipped!");
                else
                    FactionChangeQuests[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} faction change quest pairs in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadFactionChangeReputations()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_reputations");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change reputation pairs. DB table `player_factionchange_reputations` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int alliance = result.Read<int>(0);
                int horde = result.Read<int>(1);

                if (!CliDB.FactionStorage.ContainsKey(alliance))
                    Log.outError(LogFilter.Sql, $"Reputation {alliance} (alliance_id) referenced in `player_factionchange_reputations` does not exist, pair skipped!");
                else if (!CliDB.FactionStorage.ContainsKey(horde))
                    Log.outError(LogFilter.Sql, $"Reputation {horde} (horde_id) referenced in `player_factionchange_reputations` does not exist, pair skipped!");
                else
                    FactionChangeReputation[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} faction change reputation pairs in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadFactionChangeSpells()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_spells");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change spell pairs. DB table `player_factionchange_spells` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int alliance = result.Read<int>(0);
                int horde = result.Read<int>(1);

                if (!Global.SpellMgr.HasSpellInfo(alliance, Difficulty.None))
                    Log.outError(LogFilter.Sql, $"Spell {alliance} (alliance_id) referenced in `player_factionchange_spells` does not exist, pair skipped!");
                else if (!Global.SpellMgr.HasSpellInfo(horde, Difficulty.None))
                    Log.outError(LogFilter.Sql, $"Spell {horde} (horde_id) referenced in `player_factionchange_spells` does not exist, pair skipped!");
                else
                    FactionChangeSpells[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} faction change spell pairs in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }

        public void LoadFactionChangeTitles()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT alliance_id, horde_id FROM player_factionchange_titles");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 faction change title pairs. DB table `player_factionchange_title` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int alliance = result.Read<int>(0);
                int horde = result.Read<int>(1);

                if (!CliDB.CharTitlesStorage.ContainsKey(alliance))
                    Log.outError(LogFilter.Sql, $"Title {alliance} (alliance_id) referenced in `player_factionchange_title` does not exist, pair skipped!");
                else if (!CliDB.CharTitlesStorage.ContainsKey(horde))
                    Log.outError(LogFilter.Sql, $"Title {horde} (horde_id) referenced in `player_factionchange_title` does not exist, pair skipped!");
                else
                    FactionChangeTitles[alliance] = horde;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} faction change title pairs in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        //Quests
        public void LoadQuests()
        {
            uint oldMSTime = Time.GetMSTime();

            // For reload case
            _questTemplates.Clear();
            _questTemplatesAutoPush.Clear();
            _questObjectives.Clear();
            _exclusiveQuestGroups.Clear();

            // Load `quest_template`
            {
                SQLResult result = DB.World.Query("SELECT " +
                    //0  1          2           3                         4                     5               6              7            8            9                  10               11                  12
                    "ID, QuestType, QuestLevel, QuestScalingFactionGroup, QuestMaxScalingLevel, QuestPackageID, QuestMinLevel, QuestSortID, QuestInfoID, SuggestedGroupNum, RewardNextQuest, RewardXPDifficulty, RewardXPMultiplier, " +
                    //13                    14                     15                16           17           18               19
                    "RewardMoneyDifficulty, RewardMoneyMultiplier, RewardBonusMoney, RewardSpell, RewardHonor, RewardKillHonor, StartItem, " +
                    //20                         21                          22                        23     24       25
                    "RewardArtifactXPDifficulty, RewardArtifactXPMultiplier, RewardArtifactCategoryID, Flags, FlagsEx, FlagsEx2, " +
                    //26          27             28         29                 30           31             32         33
                    "RewardItem1, RewardAmount1, ItemDrop1, ItemDropQuantity1, RewardItem2, RewardAmount2, ItemDrop2, ItemDropQuantity2, " +
                    //34          35             36         37                 38           39             40         41
                    "RewardItem3, RewardAmount3, ItemDrop3, ItemDropQuantity3, RewardItem4, RewardAmount4, ItemDrop4, ItemDropQuantity4, " +
                    //42                  43                         44                          45                   46                         47
                    "RewardChoiceItemID1, RewardChoiceItemQuantity1, RewardChoiceItemDisplayID1, RewardChoiceItemID2, RewardChoiceItemQuantity2, RewardChoiceItemDisplayID2, " +
                    //48                  49                         50                          51                   52                         53
                    "RewardChoiceItemID3, RewardChoiceItemQuantity3, RewardChoiceItemDisplayID3, RewardChoiceItemID4, RewardChoiceItemQuantity4, RewardChoiceItemDisplayID4, " +
                    //54                  55                         56                          57                   58                         59
                    "RewardChoiceItemID5, RewardChoiceItemQuantity5, RewardChoiceItemDisplayID5, RewardChoiceItemID6, RewardChoiceItemQuantity6, RewardChoiceItemDisplayID6, " +
                    //60           61    62    63           64           65                 66                 67
                    "POIContinent, POIx, POIy, POIPriority, RewardTitle, RewardArenaPoints, RewardSkillLineID, RewardNumSkillUps, " +
                    //68            69                  70                         71
                    "PortraitGiver, PortraitGiverMount, PortraitGiverModelSceneID, PortraitTurnIn, " +
                    //72               73                   74                      75                   76                77                   78                      79
                    "RewardFactionID1, RewardFactionValue1, RewardFactionOverride1, RewardFactionCapIn1, RewardFactionID2, RewardFactionValue2, RewardFactionOverride2, RewardFactionCapIn2, " +
                    //80               81                   82                      83                   84                85                   86                      87
                    "RewardFactionID3, RewardFactionValue3, RewardFactionOverride3, RewardFactionCapIn3, RewardFactionID4, RewardFactionValue4, RewardFactionOverride4, RewardFactionCapIn4, " +
                    //88               89                   90                      91                   92
                    "RewardFactionID5, RewardFactionValue5, RewardFactionOverride5, RewardFactionCapIn5, RewardFactionFlags, " +
                    //93                94                  95                 96                  97                 98                  99                 100
                    "RewardCurrencyID1, RewardCurrencyQty1, RewardCurrencyID2, RewardCurrencyQty2, RewardCurrencyID3, RewardCurrencyQty3, RewardCurrencyID4, RewardCurrencyQty4, " +
                    //101                102                 103          104          105             106               107        108                  109
                    "AcceptedSoundKitID, CompleteSoundKitID, AreaGroupID, TimeAllowed, AllowableRaces, TreasurePickerID, Expansion, ManagedWorldStateID, QuestSessionBonus, " +
                    //110      111             112               113              114                115                116                 117                 118
                    "LogTitle, LogDescription, QuestDescription, AreaDescription, PortraitGiverText, PortraitGiverName, PortraitTurnInText, PortraitTurnInName, QuestCompletionLog " +
                    "FROM quest_template");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quests definitions. DB table `quest_template` is empty.");
                    return;
                }

                // create multimap previous quest for each existed quest
                // some quests can have many previous maps set by NextQuestId in previous quest
                // for example set of race quests can lead to single not race specific quest
                do
                {
                    Quest newQuest = new(result.GetFields());
                    _questTemplates[newQuest.Id] = newQuest;
                    if (newQuest.IsAutoPush())
                        _questTemplatesAutoPush.Add(newQuest);
                }
                while (result.NextRow());
            }

            // Load `quest_reward_choice_items`
            {
                //                                          0        1      2      3      4      5      6
                SQLResult result = DB.World.Query("SELECT QuestID, Type1, Type2, Type3, Type4, Type5, Type6 FROM quest_reward_choice_items");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest reward choice items. DB table `quest_reward_choice_items` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadRewardChoiceItems(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_reward_choice_items` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_reward_display_spell`            
            {
                //                                         0        1        2
                SQLResult result = DB.World.Query("SELECT QuestID, SpellID, Idx FROM quest_reward_display_spell ORDER BY QuestID ASC, Idx ASC");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest reward display spells. DB table `quest_reward_display_spell` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadRewardDisplaySpell(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_reward_display_spell` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_details`
            {
                //                                        0   1       2       3       4       5            6            7            8
                SQLResult result = DB.World.Query("SELECT ID, Emote1, Emote2, Emote3, Emote4, EmoteDelay1, EmoteDelay2, EmoteDelay3, EmoteDelay4 FROM quest_details");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest details. DB table `quest_details` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadQuestDetails(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_details` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_request_items`
            {
                //                                        0   1                2                  3                     4                       5
                SQLResult result = DB.World.Query("SELECT ID, EmoteOnComplete, EmoteOnIncomplete, EmoteOnCompleteDelay, EmoteOnIncompleteDelay, CompletionText FROM quest_request_items");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest request items. DB table `quest_request_items` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadQuestRequestItems(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_request_items` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_offer_reward`
            {
                //                                        0   1       2       3       4       5            6            7            8            9
                SQLResult result = DB.World.Query("SELECT ID, Emote1, Emote2, Emote3, Emote4, EmoteDelay1, EmoteDelay2, EmoteDelay3, EmoteDelay4, RewardText FROM quest_offer_reward");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest reward emotes. DB table `quest_offer_reward` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadQuestOfferReward(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_offer_reward` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_template_addon`
            {
                //                                        0   1         2                 3              4            5            6               7                     8                     9
                SQLResult result = DB.World.Query("SELECT ID, MaxLevel, AllowableClasses, SourceSpellID, PrevQuestID, NextQuestID, ExclusiveGroup, BreadcrumbForQuestId, RewardMailTemplateID, RewardMailDelay, " +
                    //10               11                   12                     13                     14                   15                   16
                    "RequiredSkillID, RequiredSkillPoints, RequiredMinRepFaction, RequiredMaxRepFaction, RequiredMinRepValue, RequiredMaxRepValue, ProvidedItemCount, " +
                    //17           18
                    "SpecialFlags, ScriptName FROM quest_template_addon");
            

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest template addons. DB table `quest_template_addon` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadQuestTemplateAddon(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_template_addon` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_mail_sender`
            {
                //                                              0        1
                SQLResult result = DB.World.Query("SELECT QuestId, RewardMailSenderEntry FROM quest_mail_sender");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest mail senders. DB table `quest_mail_sender` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadQuestMailSender(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_mail_sender` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_objectives`
            {
                //                                           0           1      2        3                4            5          6         7          8                     9
                SQLResult result = DB.World.Query("SELECT qo.QuestID, qo.ID, qo.Type, qo.StorageIndex, qo.ObjectID, qo.Amount, qo.Flags, qo.Flags2, qo.ProgressBarWeight, qo.Description, " +
                    //   10                11            12                   13                     14
                    "qoce.GameEventID, qoce.SpellID, qoce.ConversationID, qoce.UpdatePhaseShift, qoce.UpdateZoneAuras FROM quest_objectives AS qo " +
                    "LEFT JOIN quest_objectives_completion_effect AS qoce ON qo.ID = qoce.ObjectiveID ORDER BY `Order` ASC, StorageIndex ASC");


                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest objectives. DB table `quest_objectives` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);
                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadQuestObjective(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_objectives` has objective for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_description_conditional`
            {
                //                                         0        1                  2                     3       4
                SQLResult result = DB.World.Query("SELECT QuestId, PlayerConditionId, QuestgiverCreatureId, Text, locale " +
                    "FROM quest_description_conditional ORDER BY OrderIndex");

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest mail senders. DB table `quest_description_conditional` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadConditionalConditionalQuestDescription(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_description_conditional` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_request_items_conditional`
            {
                //                                         0        1                  2                     3       4
                SQLResult result = DB.World.Query("SELECT QuestId, PlayerConditionId, QuestgiverCreatureId, Text, locale " +
                    "FROM quest_request_items_conditional ORDER BY OrderIndex");

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest mail senders. DB table `quest_request_items_conditional` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadConditionalConditionalRequestItemsText(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_request_items_conditional` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_offer_reward_conditional`
            {
                //                                         0        1                  2                     3       4
                SQLResult result = DB.World.Query("SELECT QuestId, PlayerConditionId, QuestgiverCreatureId, Text, locale " +
                    "FROM quest_offer_reward_conditional ORDER BY OrderIndex");

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest mail senders. DB table `quest_offer_reward_conditional` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadConditionalConditionalOfferRewardText(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_offer_reward_conditional` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_completion_log_conditional`
            {
                //                                         0        1                  2                     3       4
                SQLResult result = DB.World.Query("SELECT QuestId, PlayerConditionId, QuestgiverCreatureId, Text, locale " +
                    "FROM quest_completion_log_conditional ORDER BY OrderIndex");

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest mail senders. DB table `quest_completion_log_conditional` is empty.");
                }
                else
                {
                    do
                    {
                        int questId = result.Read<int>(0);

                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadConditionalConditionalQuestCompletionLog(result.GetFields());
                        else
                            Log.outError(LogFilter.Sql, $"Table `quest_completion_log_conditional` has data for quest {questId} but such quest does not exist.");
                    } while (result.NextRow());
                }
            }

            // Load `quest_visual_effect` join table with quest_objectives because visual effects are based on objective ID (core stores objectives by their index in quest)
            {
                //                                          0     1     2          3        4
                SQLResult result = DB.World.Query("SELECT v.ID, o.ID, o.QuestID, v.Index, v.VisualEffect FROM quest_visual_effect AS v LEFT JOIN quest_objectives AS o ON v.ID = o.ID ORDER BY v.Index DESC");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest visual effects. DB table `quest_visual_effect` is empty.");
                }
                else
                {
                    do
                    {
                        uint vID = result.Read<uint>(0);
                        uint oID = result.Read<uint>(1);

                        if (vID == 0)
                        {
                            Log.outError(LogFilter.Sql, "Table `quest_visual_effect` has visual effect for null objective id");
                            continue;
                        }

                        // objID will be null if match for table join is not found
                        if (vID != oID)
                        {
                            Log.outError(LogFilter.Sql, $"Table `quest_visual_effect` has visual effect for objective {vID} but such objective does not exist.");
                            continue;
                        }

                        int questId = result.Read<int>(2);

                        // Do not throw error here because error for non existing quest is thrown while loading quest objectives. we do not need duplication
                        var quest = _questTemplates.LookupByKey(questId);
                        if (quest != null)
                            quest.LoadQuestObjectiveVisualEffect(result.GetFields());
                    } while (result.NextRow());
                }
            }

            Dictionary<int, int> usedMailTemplates = new();

            // Post processing
            foreach (var qinfo in _questTemplates.Values)
            {
                // skip post-loading checks for disabled quests
                if (Global.DisableMgr.IsDisabledFor(DisableType.Quest, qinfo.Id, null))
                    continue;

                // additional quest integrity checks (GO, creaturetemplate and itemtemplate must be loaded already)

                if (qinfo.Type >= QuestType.MaxDBAllowedQuestTypes)
                    Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `Method` = {qinfo.Type}, expected values are 0, 1 or 2.");

                if (qinfo.SpecialFlags.HasAnyFlag(~QuestSpecialFlags.DbAllowed))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Quest {qinfo.Id} has `SpecialFlags` = {qinfo.SpecialFlags} > max allowed value. Correct `SpecialFlags` to value <= {QuestSpecialFlags.DbAllowed}.");
                    qinfo.SpecialFlags &= QuestSpecialFlags.DbAllowed;
                }

                if (qinfo.Flags.HasFlag(QuestFlags.Daily | QuestFlags.Weekly))
                {
                    Log.outError(LogFilter.Sql, $"Weekly Quest {qinfo.Id} is marked as daily quest in `Flags`, removed daily flag.");
                    qinfo.Flags &= ~QuestFlags.Daily;
                }

                if (qinfo.Flags.HasFlag(QuestFlags.Daily))
                {
                    if (!qinfo.SpecialFlags.HasAnyFlag(QuestSpecialFlags.Repeatable))
                    {
                        Log.outError(LogFilter.Sql, $"Daily Quest {qinfo.Id} not marked as repeatable in `SpecialFlags`, added.");
                        qinfo.SpecialFlags |= QuestSpecialFlags.Repeatable;
                    }
                }

                if (qinfo.Flags.HasFlag(QuestFlags.Weekly))
                {
                    if (!qinfo.SpecialFlags.HasAnyFlag(QuestSpecialFlags.Repeatable))
                    {
                        Log.outError(LogFilter.Sql, $"Weekly Quest {qinfo.Id} not marked as repeatable in `SpecialFlags`, added.");
                        qinfo.SpecialFlags |= QuestSpecialFlags.Repeatable;
                    }
                }

                if (qinfo.SpecialFlags.HasFlag(QuestSpecialFlags.Monthly))
                {
                    if (!qinfo.SpecialFlags.HasAnyFlag(QuestSpecialFlags.Repeatable))
                    {
                        Log.outError(LogFilter.Sql, $"Monthly quest {qinfo.Id} not marked as repeatable in `SpecialFlags`, added.");
                        qinfo.SpecialFlags |= QuestSpecialFlags.Repeatable;
                    }
                }

                if (qinfo.Flags.HasFlag(QuestFlags.TrackingEvent))
                {
                    // at auto-reward can be rewarded only RewardChoiceItemId[0]
                    for (int j = 1; j < qinfo.RewardChoiceItemId.Length; ++j)
                    {
                        var id = qinfo.RewardChoiceItemId[j];
                        if (id != 0)
                        {
                            Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardChoiceItemId{j + 1}` = {id} but item from " +
                                $"`RewardChoiceItemId{j + 1}` can't be rewarded with quest flag QUEST_FLAGS_TRACKING.");
                            // no changes, quest ignore this data
                        }
                    }
                }

                // client quest log visual (area case)
                if (qinfo.QuestSortID > 0)
                {
                    if (!CliDB.AreaTableStorage.ContainsKey(qinfo.QuestSortID))
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `ZoneOrSort` = {qinfo.QuestSortID} (zone case) but zone with this id does not exist.");
                        // no changes, quest not dependent from this value but can have problems at client
                    }
                }
                // client quest log visual (sort case)
                if (qinfo.QuestSortID < 0)
                {
                    var qSort = CliDB.QuestSortStorage.LookupByKey(-qinfo.QuestSortID);
                    if (qSort == null)
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `ZoneOrSort` = {qinfo.QuestSortID} (sort case) but quest sort with this id does not exist.");
                        // no changes, quest not dependent from this value but can have problems at client (note some may be 0, we must allow this so no check)
                    }

                    //check for proper RequiredSkillId value (skill case)
                    var skillid = SharedConst.SkillByQuestSort(-qinfo.QuestSortID);
                    if (skillid != SkillType.None)
                    {
                        if (qinfo.RequiredSkillId != skillid)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Quest {qinfo.Id} has `ZoneOrSort` = {qinfo.QuestSortID} but `RequiredSkillId` does not have a corresponding value ({skillid}).");
                            //override, and force proper value here?
                        }
                    }
                }

                // AllowableClasses, can be 0/CLASSMASK_ALL_PLAYABLE to allow any class
                if (qinfo.AllowableClasses != ClassMask.None)
                {
                    if (!qinfo.AllowableClasses.HasAnyFlag(ClassMask.Playable))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Quest {qinfo.Id} does not contain any playable classes in `RequiredClasses` ({qinfo.AllowableClasses}), value set to 0 (all classes).");
                        qinfo.AllowableClasses = ClassMask.None;
                    }
                }

                // AllowableRaces, can be -1/RACEMASK_ALL_PLAYABLE to allow any race
                if (qinfo.AllowableRaces != (RaceMask)(-1))
                {
                    if (qinfo.AllowableRaces != RaceMask.None && !qinfo.AllowableRaces.HasAnyFlag(RaceMask.Playable))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Quest {qinfo.Id} does not contain any playable races in `RequiredRaces` ({qinfo.AllowableRaces}), value set to -1 (all races).");
                        qinfo.AllowableRaces = (RaceMask)(-1);
                    }
                }

                // RequiredSkillId, can be 0
                if (qinfo.RequiredSkillId != SkillType.None)
                {
                    if (!CliDB.SkillLineStorage.ContainsKey((int)qinfo.RequiredSkillId))
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RequiredSkillId` = {qinfo.RequiredSkillId} but this skill does not exist.");
                    }
                }

                if (qinfo.RequiredSkillPoints != 0)
                {
                    if (qinfo.RequiredSkillPoints > Global.WorldMgr.GetConfigMaxSkillValue())
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RequiredSkillPoints` = {qinfo.RequiredSkillPoints} " +
                            $"but max possible skill is {Global.WorldMgr.GetConfigMaxSkillValue()}, quest can't be done.");
                        // no changes, quest can't be done for this requirement
                    }
                }
                // else Skill quests can have 0 skill level, this is ok

                if (qinfo.RequiredMinRepFaction != 0 && !CliDB.FactionStorage.ContainsKey(qinfo.RequiredMinRepFaction))
                {
                    Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RequiredMinRepFaction` = {qinfo.RequiredMinRepFaction} but " +
                        $"faction template {qinfo.RequiredMinRepFaction} does not exist, quest can't be done.");
                    // no changes, quest can't be done for this requirement
                }

                if (qinfo.RequiredMaxRepFaction != 0 && !CliDB.FactionStorage.ContainsKey(qinfo.RequiredMaxRepFaction))
                {
                    Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RequiredMaxRepFaction` = {qinfo.RequiredMaxRepFaction} but " +
                        $"faction template {qinfo.RequiredMaxRepFaction} does not exist, quest can't be done.");
                    // no changes, quest can't be done for this requirement
                }

                if (qinfo.RequiredMinRepValue != 0 && qinfo.RequiredMinRepValue > SharedConst.ReputationCap)
                {
                    Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RequiredMinRepValue` = {qinfo.RequiredMinRepValue} but " +
                        $"max reputation is {SharedConst.ReputationCap}, quest can't be done.");
                    // no changes, quest can't be done for this requirement
                }

                if (qinfo.RequiredMinRepValue != 0 && qinfo.RequiredMaxRepValue != 0 && qinfo.RequiredMaxRepValue <= qinfo.RequiredMinRepValue)
                {
                    Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RequiredMaxRepValue` = {qinfo.RequiredMaxRepValue} and " +
                        $"`RequiredMinRepValue` = {qinfo.RequiredMinRepValue}, quest can't be done.");
                    // no changes, quest can't be done for this requirement
                }

                if (qinfo.RequiredMinRepFaction == 0 && qinfo.RequiredMinRepValue != 0)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Quest {qinfo.Id} has `RequiredMinRepValue` = {qinfo.RequiredMinRepValue} but `RequiredMinRepFaction` is 0, value has no effect.");
                    // warning
                }

                if (qinfo.RequiredMaxRepFaction == 0 && qinfo.RequiredMaxRepValue != 0)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Quest {qinfo.Id} has `RequiredMaxRepValue` = {qinfo.RequiredMaxRepValue} but `RequiredMaxRepFaction` is 0, value has no effect.");
                    // warning
                }

                if (qinfo.RewardTitleId != 0 && !CliDB.CharTitlesStorage.ContainsKey(qinfo.RewardTitleId))
                {
                    Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardTitleId` = {qinfo.RewardTitleId} but " +
                        $"CharTitle Id {qinfo.RewardTitleId} does not exist, quest can't be rewarded with title.");
                    qinfo.RewardTitleId = 0;
                    // quest can't reward this title
                }

                if (qinfo.SourceItemId != 0)
                {
                    if (GetItemTemplate(qinfo.SourceItemId) == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `SourceItemId` = {qinfo.SourceItemId} but item with entry {qinfo.SourceItemId} does not exist, quest can't be done.");
                        qinfo.SourceItemId = 0;                       // quest can't be done for this requirement
                    }
                    else if (qinfo.SourceItemIdCount <= 0)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `StartItem` = {qinfo.SourceItemId} but `ProvidedItemCount` = 0, set to 1 but need fix in DB.");
                        qinfo.SourceItemIdCount = 1;                    // update to 1 for allow quest work for backward compatibility with DB
                    }
                }
                else if (qinfo.SourceItemIdCount > 0)
                {
                    Log.outError(LogFilter.Sql,
                        $"Quest {qinfo.Id} has `SourceItemId` = 0 but `SourceItemIdCount` = {qinfo.SourceItemIdCount}, useless value.");
                    qinfo.SourceItemIdCount = 0;                          // no quest work changes in fact
                }

                if (qinfo.SourceSpellID != 0)
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(qinfo.SourceSpellID, Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `SourceSpellid` = {qinfo.SourceSpellID} but spell {qinfo.SourceSpellID} doesn't exist, quest can't be done.");
                        qinfo.SourceSpellID = 0;                        // quest can't be done for this requirement
                    }
                    else if (!Global.SpellMgr.IsSpellValid(spellInfo))
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `SourceSpellid` = {qinfo.SourceSpellID} but spell {qinfo.SourceSpellID} is broken, quest can't be done.");
                        qinfo.SourceSpellID = 0;                        // quest can't be done for this requirement
                    }
                }

                foreach (QuestObjective obj in qinfo.Objectives)
                {
                    // Store objective for lookup by id
                    _questObjectives[obj.Id] = obj;

                    // Check storage index for objectives which store data
                    if (obj.StorageIndex < 0)
                    {
                        switch (obj.Type)
                        {
                            case QuestObjectiveType.Monster:
                            case QuestObjectiveType.Item:
                            case QuestObjectiveType.GameObject:
                            case QuestObjectiveType.TalkTo:
                            case QuestObjectiveType.PlayerKills:
                            case QuestObjectiveType.AreaTrigger:
                            case QuestObjectiveType.WinPetBattleAgainstNpc:
                            case QuestObjectiveType.ObtainCurrency:
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has invalid StorageIndex = {obj.StorageIndex} for objective Type {obj.Type}.");
                                break;
                            default:
                                break;
                        }
                    }

                    switch (obj.Type)
                    {
                        case QuestObjectiveType.Item:
                            if (GetItemTemplate(obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing item entry {obj.ObjectID}, quest can't be done.");
                            break;
                        case QuestObjectiveType.Monster:
                            if (GetCreatureTemplate(obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing creature entry {obj.ObjectID}, quest can't be done.");
                            break;
                        case QuestObjectiveType.GameObject:
                            if (GetGameObjectTemplate(obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing gameobject entry {obj.ObjectID}, quest can't be done.");
                            break;
                        case QuestObjectiveType.TalkTo:
                            if (GetCreatureTemplate(obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing creature entry {obj.ObjectID}, quest can't be done.");
                            break;
                        case QuestObjectiveType.MinReputation:
                        case QuestObjectiveType.MaxReputation:
                        case QuestObjectiveType.IncreaseReputation:
                            if (!CliDB.FactionStorage.ContainsKey(obj.ObjectID))
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing faction id {obj.ObjectID}.");
                            break;
                        case QuestObjectiveType.PlayerKills:
                            if (obj.Amount <= 0)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has invalid player kills count {obj.Amount}.");
                            break;
                        case QuestObjectiveType.Currency:
                        case QuestObjectiveType.HaveCurrency:
                        case QuestObjectiveType.ObtainCurrency:
                            if (!CliDB.CurrencyTypesStorage.ContainsKey(obj.ObjectID))
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing currency {obj.ObjectID}.");
                            if (obj.Amount <= 0)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has invalid currency amount {obj.Amount}.");
                            break;
                        case QuestObjectiveType.LearnSpell:
                            if (!Global.SpellMgr.HasSpellInfo(obj.ObjectID, Difficulty.None))
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing spell id {obj.ObjectID}.");
                            break;
                        case QuestObjectiveType.WinPetBattleAgainstNpc:
                            if (obj.ObjectID != 0 && GetCreatureTemplate(obj.ObjectID) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing creature entry {obj.ObjectID}, quest can't be done.");
                            break;
                        case QuestObjectiveType.DefeatBattlePet:
                            if (!CliDB.BattlePetSpeciesStorage.ContainsKey(obj.ObjectID))
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing battlepet species id {obj.ObjectID}.");
                            break;
                        case QuestObjectiveType.CriteriaTree:
                            if (!CliDB.CriteriaTreeStorage.ContainsKey(obj.ObjectID))
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing criteria tree id {obj.ObjectID}.");
                            break;
                        case QuestObjectiveType.AreaTrigger:
                            if (!CliDB.AreaTriggerStorage.ContainsKey(obj.ObjectID) && obj.ObjectID != -1)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing AreaTrigger.db2 id {obj.ObjectID}.");
                            break;
                        case QuestObjectiveType.AreaTriggerEnter:
                        case QuestObjectiveType.AreaTriggerExit:
                            if (Global.AreaTriggerDataStorage.GetAreaTriggerTemplate(new AreaTriggerId(obj.ObjectID, false)) == null && Global.AreaTriggerDataStorage.GetAreaTriggerTemplate(new AreaTriggerId(obj.ObjectID, true)) == null)
                                Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has non existing areatrigger id {obj.ObjectID}.");
                            break;
                        case QuestObjectiveType.Money:
                        case QuestObjectiveType.WinPvpPetBattles:
                        case QuestObjectiveType.ProgressBar:
                            break;
                        default:
                            Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} objective {obj.Id} has unhandled Type {obj.Type}.");
                            break;
                    }

                    if (obj.Flags.HasAnyFlag(QuestObjectiveFlags.Sequenced))
                        qinfo.SetSpecialFlag(QuestSpecialFlags.SequencedObjectives);
                }

                for (var j = 0; j < SharedConst.QuestItemDropCount; j++)
                {
                    var id = qinfo.ItemDrop[j];
                    if (id != 0)
                    {
                        if (GetItemTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Quest {qinfo.Id} has `RequiredSourceItemId{j + 1}` = {id} but item with entry {id} does not exist, quest can't be done.");
                            // no changes, quest can't be done for this requirement
                        }
                    }
                    else
                    {
                        if (qinfo.ItemDropQuantity[j] > 0)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Quest {qinfo.Id} has `RequiredSourceItemId{j + 1}` = 0 but `RequiredSourceItemCount{j + 1}` = {qinfo.ItemDropQuantity[j]}.");
                            // no changes, quest ignore this data
                        }
                    }
                }

                for (var j = 0; j < SharedConst.QuestRewardChoicesCount; ++j)
                {
                    var id = qinfo.RewardChoiceItemId[j];
                    if (id != 0)
                    {
                        switch (qinfo.RewardChoiceItemType[j])
                        {
                            case LootItemType.Item:
                                if (GetItemTemplate(id) == null)
                                {
                                    Log.outError(LogFilter.Sql,
                                        $"Quest {qinfo.Id} has `RewardChoiceItemId{j + 1}` = {id} but item with entry {id} does not exist, quest will not reward this item.");
                                    qinfo.RewardChoiceItemId[j] = 0;          // no changes, quest will not reward this
                                }
                                break;
                            case LootItemType.Currency:
                                if (!CliDB.CurrencyTypesStorage.HasRecord(id))
                                {
                                    Log.outError(LogFilter.Sql,
                                        $"Quest {qinfo.Id} has `RewardChoiceItemId{j + 1}` = {id} but currency with id {id} does not exist, quest will not reward this currency.");
                                    qinfo.RewardChoiceItemId[j] = 0;          // no changes, quest will not reward this
                                }
                                break;
                            default:
                                Log.outError(LogFilter.Sql,
                                    $"Quest {qinfo.Id} has `RewardChoiceItemType{j + 1}` = {qinfo.RewardChoiceItemType[j]} but it is not a valid item Type, reward removed.");
                                qinfo.RewardChoiceItemId[j] = 0;
                                break;
                        }

                        if (qinfo.RewardChoiceItemCount[j] == 0)
                            Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardChoiceItemId{j + 1}` = {id} but `RewardChoiceItemCount{j + 1}` = 0, quest can't be done.");
                    }
                    else if (qinfo.RewardChoiceItemCount[j] > 0)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `RewardChoiceItemId{j + 1}` = 0 but `RewardChoiceItemCount{j + 1}` = {qinfo.RewardChoiceItemCount[j]}.");
                        // no changes, quest ignore this data
                    }
                }

                for (var j = 0; j < SharedConst.QuestRewardItemCount; ++j)
                {
                    var id = qinfo.RewardItemId[j];
                    if (id != 0)
                    {
                        if (GetItemTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Quest {qinfo.Id} has `RewardItemId{j + 1}` = {id} but item with entry {id} does not exist, quest will not reward this item.");
                            qinfo.RewardItemId[j] = 0;                // no changes, quest will not reward this item
                        }

                        if (qinfo.RewardItemCount[j] == 0)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Quest {qinfo.Id} has `RewardItemId{j + 1}` = {id} but `RewardItemIdCount{j + 1}` = 0, quest will not reward this item.");
                            // no changes
                        }
                    }
                    else if (qinfo.RewardItemCount[j] > 0)
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardItemId{j + 1}` = 0 but `RewardItemIdCount{j + 1}` = {qinfo.RewardItemCount[j]}.");
                        // no changes, quest ignore this data
                    }
                }

                for (var j = 0; j < SharedConst.QuestRewardReputationsCount; ++j)
                {
                    if (qinfo.RewardFactionId[j] != 0)
                    {
                        if (Math.Abs(qinfo.RewardFactionValue[j]) > 9)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Quest {qinfo.Id} has RewardFactionValueId{j + 1} = {qinfo.RewardFactionValue[j]}. That is outside the range of valid values (-9 to 9).");
                        }
                        if (!CliDB.FactionStorage.ContainsKey(qinfo.RewardFactionId[j]))
                        {
                            Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardFactionId{j + 1}` = {qinfo.RewardFactionId[j]} but " +
                                $"raw faction (faction.dbc) {qinfo.RewardFactionId[j]} does not exist, quest will not reward reputation for this faction.");
                            qinfo.RewardFactionId[j] = 0;            // quest will not reward this
                        }
                    }

                    else if (qinfo.RewardFactionOverride[j] != 0)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `RewardFactionId{j + 1}` = 0 but `RewardFactionValueIdOverride{j + 1}` = {qinfo.RewardFactionOverride[j]}.");
                        // no changes, quest ignore this data
                    }
                }

                if (qinfo.RewardSpell > 0)
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(qinfo.RewardSpell, Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `RewardSpellCast` = {qinfo.RewardSpell} but spell {qinfo.RewardSpell} does not exist, quest will not have a spell reward.");
                        qinfo.RewardSpell = 0;                    // no spell will be casted on player
                    }

                    else if (!Global.SpellMgr.IsSpellValid(spellInfo))
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `RewardSpellCast` = {qinfo.RewardSpell} but spell {qinfo.RewardSpell} is broken, quest will not have a spell reward.");
                        qinfo.RewardSpell = 0;                    // no spell will be casted on player
                    }
                }

                if (qinfo.RewardMailTemplateId != 0)
                {
                    if (!CliDB.MailTemplateStorage.ContainsKey(qinfo.RewardMailTemplateId))
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardMailTemplateId` = {qinfo.RewardMailTemplateId} but " +
                            $"mail template {qinfo.RewardMailTemplateId} does not exist, quest will not have a mail reward.");
                        qinfo.RewardMailTemplateId = 0;               // no mail will send to player
                        qinfo.RewardMailDelay = 0;                // no mail will send to player
                        qinfo.RewardMailSenderEntry = 0;
                    }
                    else if (usedMailTemplates.ContainsKey(qinfo.RewardMailTemplateId))
                    {
                        var usedId = usedMailTemplates.LookupByKey(qinfo.RewardMailTemplateId);
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardMailTemplateId` = {qinfo.RewardMailTemplateId} but " +
                            $"mail template  {qinfo.RewardMailTemplateId} already used for quest {usedId}, quest will not have a mail reward.");
                        qinfo.RewardMailTemplateId = 0;               // no mail will send to player
                        qinfo.RewardMailDelay = 0;                // no mail will send to player
                        qinfo.RewardMailSenderEntry = 0;
                    }
                    else
                        usedMailTemplates[qinfo.RewardMailTemplateId] = qinfo.Id;
                }

                if (qinfo.NextQuestInChain != 0)
                {
                    if (!_questTemplates.ContainsKey(qinfo.NextQuestInChain))
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `NextQuestIdChain` = {qinfo.NextQuestInChain} but quest {qinfo.NextQuestInChain} does not exist, quest chain will not work.");
                        qinfo.NextQuestInChain = 0;
                    }
                }

                for (var j = 0; j < SharedConst.QuestRewardCurrencyCount; ++j)
                {
                    if (qinfo.RewardCurrencyId[j] != 0)
                    {
                        if (qinfo.RewardCurrencyCount[j] == 0)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Quest {qinfo.Id} has `RewardCurrencyId{j + 1}` = {qinfo.RewardCurrencyId[j]} but `RewardCurrencyCount{j + 1}` = 0, quest can't be done.");
                            // no changes, quest can't be done for this requirement
                        }

                        if (!CliDB.CurrencyTypesStorage.ContainsKey(qinfo.RewardCurrencyId[j]))
                        {
                            Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardCurrencyId{j + 1}` = {qinfo.RewardCurrencyId[j]} but " +
                                $"currency with entry {qinfo.RewardCurrencyId[j]} does not exist, quest can't be done.");
                            qinfo.RewardCurrencyCount[j] = 0;             // prevent incorrect work of quest
                        }
                    }
                    else if (qinfo.RewardCurrencyCount[j] > 0)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Quest {qinfo.Id} has `RewardCurrencyId{j + 1}` = 0 but `RewardCurrencyCount{j + 1}` = {qinfo.RewardCurrencyCount[j]}, quest can't be done.");
                        qinfo.RewardCurrencyCount[j] = 0;                 // prevent incorrect work of quest
                    }
                }

                if (qinfo.SoundAccept != 0)
                {
                    if (!CliDB.SoundKitStorage.ContainsKey(qinfo.SoundAccept))
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `SoundAccept` = {qinfo.SoundAccept} but sound {qinfo.SoundAccept} does not exist, set to 0.");
                        qinfo.SoundAccept = 0;                        // no sound will be played
                    }
                }

                if (qinfo.SoundTurnIn != 0)
                {
                    if (!CliDB.SoundKitStorage.ContainsKey(qinfo.SoundTurnIn))
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `SoundTurnIn` = {qinfo.SoundTurnIn} but sound {qinfo.SoundTurnIn} does not exist, set to 0.");
                        qinfo.SoundTurnIn = 0;                        // no sound will be played
                    }
                }

                if (qinfo.RewardSkillId > 0)
                {
                    if (!CliDB.SkillLineStorage.ContainsKey(qinfo.RewardSkillId))
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardSkillId` = {qinfo.RewardSkillId} but this skill does not exist.");
                    }
                    if (qinfo.RewardSkillPoints == 0)
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardSkillId` = {qinfo.RewardSkillId} but `RewardSkillPoints` is 0.");
                    }
                }

                if (qinfo.RewardSkillPoints != 0)
                {
                    if (qinfo.RewardSkillPoints > Global.WorldMgr.GetConfigMaxSkillValue())
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardSkillPoints` = {qinfo.RewardSkillPoints} but " +
                            $"max possible skill is {Global.WorldMgr.GetConfigMaxSkillValue()}, quest can't be done.");
                        // no changes, quest can't be done for this requirement
                    }
                    if (qinfo.RewardSkillId == 0)
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has `RewardSkillPoints` = {qinfo.RewardSkillPoints} but `RewardSkillId` is 0.");
                    }
                }

                // fill additional data stores
                int prevQuestId = Math.Abs(qinfo.PrevQuestId);
                if (prevQuestId != 0)
                {
                    var prevQuestItr = _questTemplates.LookupByKey(prevQuestId);
                    if (prevQuestItr == null)
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has PrevQuestId {prevQuestId}, but no such quest.");
                    else if (prevQuestItr.BreadcrumbForQuestId != 0)
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} should not be unlocked by breadcrumb quest {prevQuestId}.");
                    else if (qinfo.PrevQuestId > 0)
                        qinfo.DependentPreviousQuests.Add(prevQuestId);
                }

                if (qinfo.NextQuestId != 0)
                {
                    var nextquest = _questTemplates.LookupByKey(qinfo.NextQuestId);
                    if (nextquest == null)
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} has NextQuestId {qinfo.NextQuestId}, but no such quest.");
                    else
                        nextquest.DependentPreviousQuests.Add(qinfo.Id);
                }

                int breadcrumbForQuestId = Math.Abs(qinfo.BreadcrumbForQuestId);
                if (breadcrumbForQuestId != 0)
                {
                    if (!_questTemplates.ContainsKey(breadcrumbForQuestId))
                    {
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} is a breadcrumb for quest {breadcrumbForQuestId}, but no such quest exists.");
                        qinfo.BreadcrumbForQuestId = 0;
                    }
                    if (qinfo.NextQuestId != 0)
                        Log.outError(LogFilter.Sql, $"Quest {qinfo.Id} is a breadcrumb, should not unlock quest {qinfo.NextQuestId}.");
                }

                if (qinfo.ExclusiveGroup != 0)
                    _exclusiveQuestGroups.Add(qinfo.ExclusiveGroup, qinfo.Id);
            }

            foreach (var questPair in _questTemplates)
            {
                // skip post-loading checks for disabled quests
                if (Global.DisableMgr.IsDisabledFor(DisableType.Quest, questPair.Key, null))
                    continue;

                Quest qinfo = questPair.Value;
                int qid = qinfo.Id;
                int breadcrumbForQuestId = Math.Abs(qinfo.BreadcrumbForQuestId);
                List<int> questSet = new();

                while (breadcrumbForQuestId != 0)
                {
                    //a previously visited quest was found as a breadcrumb quest
                    //breadcrumb loop found!
                    if (questSet.Contains(qinfo.Id))
                    {
                        Log.outError(LogFilter.Sql, $"Breadcrumb quests {qid} and {breadcrumbForQuestId} are in a loop.");
                        qinfo.BreadcrumbForQuestId = 0;
                        break;
                    }

                    questSet.Add(qinfo.Id);

                    qinfo = GetQuestTemplate(breadcrumbForQuestId);

                    //every quest has a list of every breadcrumb towards it
                    qinfo.DependentBreadcrumbQuests.Add(qid);

                    breadcrumbForQuestId = Math.Abs(qinfo.BreadcrumbForQuestId);
                }
            }

            // don't check spells with SPELL_EFFECT_QUEST_COMPLETE, a lot of invalid db2 data

            // Make all paragon reward quests repeatable
            foreach (ParagonReputationRecord paragonReputation in CliDB.ParagonReputationStorage.Values)
            {
                Quest quest = GetQuestTemplate(paragonReputation.QuestID);
                if (quest != null)
                    quest.SetSpecialFlag(QuestSpecialFlags.Repeatable);
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_questTemplates.Count} quests definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadQuestStartersAndEnders()
        {
            Log.outInfo(LogFilter.ServerLoading, "Loading GO Start Quest Data...");
            LoadGameobjectQuestStarters();
            Log.outInfo(LogFilter.ServerLoading, "Loading GO End Quest Data...");
            LoadGameobjectQuestEnders();
            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Start Quest Data...");
            LoadCreatureQuestStarters();
            Log.outInfo(LogFilter.ServerLoading, "Loading Creature End Quest Data...");
            LoadCreatureQuestEnders();
        }

        public void LoadGameobjectQuestStarters()
        {
            LoadQuestRelationsHelper(_goQuestRelations, null, "gameobject_queststarter");

            foreach (var pair in _goQuestRelations)
            {
                GameObjectTemplate goInfo = GetGameObjectTemplate(pair.Key);
                if (goInfo == null)
                    Log.outError(LogFilter.Sql, $"Table `gameobject_queststarter` have data for not existed gameobject entry ({pair.Key}) and existed quest {pair.Value}.");
                else if (goInfo.type != GameObjectTypes.QuestGiver)
                    Log.outError(LogFilter.Sql, 
                        $"Table `gameobject_queststarter` have data gameobject entry ({pair.Key}) for quest {pair.Value}, but GO is not GAMEOBJECT_TYPE_QUESTGIVER.");
            }
        }

        public void LoadGameobjectQuestEnders()
        {
            LoadQuestRelationsHelper(_goQuestInvolvedRelations, _goQuestInvolvedRelationsReverse, "gameobject_questender");

            foreach (var pair in _goQuestInvolvedRelations)
            {
                GameObjectTemplate goInfo = GetGameObjectTemplate(pair.Key);
                if (goInfo == null)
                    Log.outError(LogFilter.Sql, $"Table `gameobject_questender` have data for not existed gameobject entry ({pair.Key}) and existed quest {pair.Value}.");
                else if (goInfo.type != GameObjectTypes.QuestGiver)
                    Log.outError(LogFilter.Sql, 
                        $"Table `gameobject_questender` have data gameobject entry ({pair.Key}) for quest {pair.Value}, but GO is not GAMEOBJECT_TYPE_QUESTGIVER.");
            }
        }

        public void LoadCreatureQuestStarters()
        {
            LoadQuestRelationsHelper(_creatureQuestRelations, null, "creature_queststarter");

            foreach (var pair in _creatureQuestRelations)
            {
                CreatureTemplate cInfo = GetCreatureTemplate(pair.Key);
                if (cInfo == null)
                    Log.outError(LogFilter.Sql, $"Table `creature_queststarter` have data for not existed creature entry ({pair.Key}) and existed quest {pair.Value}.");
                else if (!cInfo.Npcflag.HasAnyFlag(NPCFlags1.QuestGiver))
                    Log.outError(LogFilter.Sql,
                        $"Table `creature_queststarter` has creature entry ({pair.Key}) for quest {pair.Value}, but npcflag does not include UNIT_NPC_FLAG_QUESTGIVER.");
            }
        }

        public void LoadCreatureQuestEnders()
        {
            LoadQuestRelationsHelper(_creatureQuestInvolvedRelations, _creatureQuestInvolvedRelationsReverse, "creature_questender");

            foreach (var pair in _creatureQuestInvolvedRelations)
            {
                CreatureTemplate cInfo = GetCreatureTemplate(pair.Key);
                if (cInfo == null)
                    Log.outError(LogFilter.Sql, $"Table `creature_questender` have data for not existed creature entry ({pair.Key}) and existed quest {pair.Value}.");
                else if (!cInfo.Npcflag.HasAnyFlag(NPCFlags1.QuestGiver))
                    Log.outError(LogFilter.Sql, 
                        $"Table `creature_questender` has creature entry ({pair.Key}) for quest {pair.Value}, but npcflag does not include UNIT_NPC_FLAG_QUESTGIVER.");
            }
        }

        void LoadQuestRelationsHelper(MultiMap<int, int> map, MultiMap<int, int> reverseMap, string table)
        {
            uint oldMSTime = Time.GetMSTime();

            map.Clear();                                            // need for reload case

            uint count = 0;

            SQLResult result = DB.World.Query($"SELECT id, quest FROM {table}");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, $"Loaded 0 quest relations from `{table}`, table is empty.");
                return;
            }

            do
            {
                int id = result.Read<int>(0);
                int quest = result.Read<int>(1);

                if (!_questTemplates.ContainsKey(quest))
                {
                    Log.outError(LogFilter.Sql, $"Table `{table}`: Quest {quest} listed for entry {id} does not exist.");
                    continue;
                }

                map.Add(id, quest);
                if (reverseMap != null)
                    reverseMap.Add(quest, id);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} quest relations from {table} in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadQuestPOI()
        {
            uint oldMSTime = Time.GetMSTime();

            _questPOIStorage.Clear();                              // need for reload case

            //                                         0        1          2     3               4                 5              6      7        8         9      10             11                 12                           13               14
            SQLResult result = DB.World.Query("SELECT QuestID, BlobIndex, Idx1, ObjectiveIndex, QuestObjectiveID, QuestObjectID, MapID, UiMapID, Priority, Flags, WorldEffectID, PlayerConditionID, NavigationPlayerConditionID, SpawnTrackingID, AlwaysAllowMergingBlobs FROM quest_poi order by QuestID, Idx1");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest POI definitions. DB table `quest_poi` is empty.");
                return;
            }

            //                                               0        1    2  3  4
            SQLResult pointsResult = DB.World.Query("SELECT QuestID, Idx1, X, Y, Z FROM quest_poi_points ORDER BY QuestID DESC, Idx1, Idx2");
            Dictionary<int, MultiMap<int, QuestPOIBlobPoint>> allPoints = new();
            if (!pointsResult.IsEmpty())
            {
                do
                {
                    int questId = pointsResult.Read<int>(0);
                    int Idx1 = pointsResult.Read<int>(1);
                    int x = pointsResult.Read<int>(2);
                    int y = pointsResult.Read<int>(3);
                    int z = pointsResult.Read<int>(4);

                    if (!allPoints.ContainsKey(questId))
                        allPoints[questId] = new MultiMap<int, QuestPOIBlobPoint>();

                    allPoints[questId].Add(Idx1, new QuestPOIBlobPoint(x, y, z));
                } while (pointsResult.NextRow());
            }

            do
            {
                int questID = result.Read<int>(0);
                int blobIndex = result.Read<int>(1);
                int idx1 = result.Read<int>(2);
                int objectiveIndex = result.Read<int>(3);
                int questObjectiveID = result.Read<int>(4);
                int questObjectID = result.Read<int>(5);
                int mapID = result.Read<int>(6);
                int uiMapId = result.Read<int>(7);
                int priority = result.Read<int>(8);
                int flags = result.Read<int>(9);
                int worldEffectID = result.Read<int>(10);
                int playerConditionID = result.Read<int>(11);
                int navigationPlayerConditionID = result.Read<int>(12);
                int spawnTrackingID = result.Read<int>(13);
                bool alwaysAllowMergingBlobs = result.Read<bool>(14);

                if (GetQuestTemplate(questID) == null)
                    Log.outError(LogFilter.Sql, $"`quest_poi` quest id ({questID}) Idx1 ({idx1}) does not exist in `quest_template`.");

                var blobs = allPoints.LookupByKey(questID);
                if (blobs != null)
                {
                    var points = blobs.LookupByKey(idx1);
                    if (!points.Empty())
                    {
                        if (!_questPOIStorage.ContainsKey(questID))
                            _questPOIStorage[questID] = new QuestPOIData(questID);

                        QuestPOIData poiData = _questPOIStorage[questID];
                        poiData.QuestID = questID;
                        poiData.Blobs.Add(new QuestPOIBlobData(blobIndex, objectiveIndex, questObjectiveID, questObjectID, mapID, uiMapId, priority, flags,
                            worldEffectID, playerConditionID, navigationPlayerConditionID, spawnTrackingID, points, alwaysAllowMergingBlobs));
                        continue;
                    }
                }
                Log.outError(LogFilter.Sql, $"Table quest_poi references unknown quest points for quest {questID} POI id {blobIndex}.");

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_questPOIStorage.Count} quest POI definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadQuestAreaTriggers()
        {
            uint oldMSTime = Time.GetMSTime();

            _questAreaTriggerStorage.Clear();                           // need for reload case

            SQLResult result = DB.World.Query("SELECT id, quest FROM areatrigger_involvedrelation");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 quest trigger points. DB table `areatrigger_involvedrelation` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                ++count;

                int triggerId = result.Read<int>(0);
                int questId = result.Read<int>(1);

                AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(triggerId);
                if (atEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Area trigger (ID:{triggerId}) does not exist in `AreaTrigger.dbc`.");
                    continue;
                }

                Quest quest = GetQuestTemplate(questId);
                if (quest == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `areatrigger_involvedrelation` has record (id: {triggerId}) for not existing quest {questId}");
                    continue;
                }

                if (!quest.HasAnyFlag(QuestFlags.CompletionAreaTrigger) && !quest.HasQuestObjectiveType(QuestObjectiveType.AreaTrigger))
                {
                    Log.outError(LogFilter.Sql, $"Table `areatrigger_involvedrelation` has record (id: {triggerId}) for not quest {questId}, " +
                        "but quest not have flag QUEST_FLAGS_COMPLETION_AREA_TRIGGER and no objective with type QUEST_OBJECTIVE_AREATRIGGER. Trigger is obsolete, skipped.");
                    continue;
                }

                _questAreaTriggerStorage.Add(triggerId, questId);

            } while (result.NextRow());

            foreach (var pair in _questObjectives)
            {
                QuestObjective objective = pair.Value;
                if (objective.Type == QuestObjectiveType.AreaTrigger)
                    _questAreaTriggerStorage.Add(objective.ObjectID, objective.QuestID);
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} quest trigger points in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadQuestGreetings()
        {
            uint oldMSTime = Time.GetMSTime();

            for (var i = 0; i < 2; ++i)
                _questGreetingStorage[i] = new Dictionary<int, QuestGreeting>();

            //                                         0   1          2                3            4
            SQLResult result = DB.World.Query("SELECT ID, Type, GreetEmoteType, GreetEmoteDelay, Greeting FROM quest_greeting");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 npc texts, table is empty!");
                return;
            }

            uint count = 0;
            do
            {
                int id = result.Read<int>(0);
                byte type = result.Read<byte>(1);

                switch (type)
                {
                    case 0: // Creature
                        if (GetCreatureTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `quest_greeting`: creature template entry {id} does not exist.");
                            continue;
                        }
                        break;
                    case 1: // GameObject
                        if (GetGameObjectTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `quest_greeting`: gameobject template entry {id} does not exist.");
                            continue;
                        }
                        break;
                    default:
                        continue;
                }

                ushort greetEmoteType = result.Read<ushort>(2);
                uint greetEmoteDelay = result.Read<uint>(3);
                string greeting = result.Read<string>(4);

                _questGreetingStorage[type][id] = new QuestGreeting(greetEmoteType, greetEmoteDelay, greeting);
                count++;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} quest_greeting in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public Quest GetQuestTemplate(int questId)
        {
            return _questTemplates.LookupByKey(questId);
        }

        public Dictionary<int, Quest> GetQuestTemplates()
        {
            return _questTemplates;
        }

        public List<Quest> GetQuestTemplatesAutoPush()
        {
            return _questTemplatesAutoPush;
        }

        public MultiMap<int, int> GetGOQuestRelationMapHACK() { return _goQuestRelations; }

        public QuestRelationResult GetGOQuestRelations(int entry) { return GetQuestRelationsFrom(_goQuestRelations, entry, true); }

        public QuestRelationResult GetGOQuestInvolvedRelations(int entry) { return GetQuestRelationsFrom(_goQuestInvolvedRelations, entry, false); }

        public List<int> GetGOQuestInvolvedRelationReverseBounds(int questId) { return _goQuestInvolvedRelationsReverse.LookupByKey(questId); }

        public MultiMap<int, int> GetCreatureQuestRelationMapHACK() { return _creatureQuestRelations; }

        public QuestRelationResult GetCreatureQuestRelations(int entry) { return GetQuestRelationsFrom(_creatureQuestRelations, entry, true); }

        public QuestRelationResult GetCreatureQuestInvolvedRelations(int entry) { return GetQuestRelationsFrom(_creatureQuestInvolvedRelations, entry, false); }

        public List<int> GetCreatureQuestInvolvedRelationReverseBounds(int questId) { return _creatureQuestInvolvedRelationsReverse.LookupByKey(questId); }

        public QuestPOIData GetQuestPOIData(int questId)
        {
            return _questPOIStorage.LookupByKey(questId);
        }

        public QuestObjective GetQuestObjective(int questObjectiveId)
        {
            return _questObjectives.LookupByKey(questObjectiveId);
        }

        public List<int> GetQuestsForAreaTrigger(int triggerId)
        {
            return _questAreaTriggerStorage.LookupByKey(triggerId);
        }

        public QuestGreeting GetQuestGreeting(TypeId type, int id)
        {
            byte typeIndex;
            if (type == TypeId.Unit)
                typeIndex = 0;
            else if (type == TypeId.GameObject)
                typeIndex = 1;
            else
                return null;

            return _questGreetingStorage[typeIndex].LookupByKey(id);
        }

        public QuestGreetingLocale GetQuestGreetingLocale(TypeId type, int id)
        {
            byte typeIndex;
            if (type == TypeId.Unit)
                typeIndex = 0;
            else if (type == TypeId.GameObject)
                typeIndex = 1;
            else
                return null;

            return _questGreetingLocaleStorage[typeIndex].LookupByKey(id);
        }

        public List<int> GetExclusiveQuestGroupBounds(int exclusiveGroupId)
        {
            return _exclusiveQuestGroups.LookupByKey(exclusiveGroupId);
        }

        QuestRelationResult GetQuestRelationsFrom(MultiMap<int, int> map, int key, bool onlyActive) { return new QuestRelationResult(map.LookupByKey(key), onlyActive); }

        public List<int> GetCreatureQuestItemList(int creatureEntry, Difficulty difficulty)
        {
            var itr = creatureQuestItemStorage.LookupByKey((creatureEntry, difficulty));
            if (itr != null)
                return itr;

            // If there is no data for the difficulty, try to get data for the fallback difficulty
            var difficultyEntry = CliDB.DifficultyStorage.LookupByKey(difficulty);
            if (difficultyEntry != null)
                return GetCreatureQuestItemList(creatureEntry, difficultyEntry.FallbackDifficultyID);

            return null;
        }

        //Spells /Skills / Phases
        public void LoadPhases()
        {
            foreach (PhaseRecord phase in CliDB.PhaseStorage.Values)
                _phaseInfoById.Add(phase.Id, new PhaseInfoStruct(phase.Id));

            foreach (MapRecord map in CliDB.MapStorage.Values)
                if (map.ParentMapID != -1)
                    _terrainSwapInfoById.Add(map.Id, new TerrainSwapInfo(map.Id));

            Log.outInfo(LogFilter.ServerLoading, "Loading Terrain World Map definitions...");
            LoadTerrainWorldMaps();

            Log.outInfo(LogFilter.ServerLoading, "Loading Terrain Swap Default definitions...");
            LoadTerrainSwapDefaults();

            Log.outInfo(LogFilter.ServerLoading, "Loading Phase Area definitions...");
            LoadAreaPhases();
        }

        public void UnloadPhaseConditions()
        {
            foreach (var pair in _phaseInfoByArea)
                pair.Value.Conditions.Clear();
        }

        void LoadTerrainWorldMaps()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0               1
            SQLResult result = DB.World.Query("SELECT TerrainSwapMap, UiMapPhaseId  FROM `terrain_worldmap`");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 terrain world maps. DB table `terrain_worldmap` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int mapId = result.Read<int>(0);
                int uiMapPhaseId = result.Read<int>(1);

                if (!CliDB.MapStorage.ContainsKey(mapId))
                {
                    Log.outError(LogFilter.Sql, $"TerrainSwapMap {mapId} defined in `terrain_worldmap` does not exist, skipped.");
                    continue;
                }

                if (!Global.DB2Mgr.IsUiMapPhase(uiMapPhaseId))
                {
                    Log.outError(LogFilter.Sql, $"Phase {uiMapPhaseId} defined in `terrain_worldmap` is not a valid terrain swap phase, skipped.");
                    continue;
                }

                if (!_terrainSwapInfoById.ContainsKey(mapId))
                    _terrainSwapInfoById.Add(mapId, new TerrainSwapInfo());

                TerrainSwapInfo terrainSwapInfo = _terrainSwapInfoById[mapId];
                terrainSwapInfo.Id = mapId;
                terrainSwapInfo.UiMapPhaseIDs.Add(uiMapPhaseId);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} terrain world maps in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        void LoadTerrainSwapDefaults()
        {
            uint oldMSTime = Time.GetMSTime();
            //                                        0       1
            SQLResult result = DB.World.Query("SELECT MapId, TerrainSwapMap FROM `terrain_swap_defaults`");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 terrain swap defaults. DB table `terrain_swap_defaults` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int mapId = result.Read<int>(0);
                if (!CliDB.MapStorage.ContainsKey(mapId))
                {
                    Log.outError(LogFilter.Sql, $"Map {mapId} defined in `terrain_swap_defaults` does not exist, skipped.");
                    continue;
                }

                int terrainSwap = result.Read<int>(1);
                if (!CliDB.MapStorage.ContainsKey(terrainSwap))
                {
                    Log.outError(LogFilter.Sql, $"TerrainSwapMap {terrainSwap} defined in `terrain_swap_defaults` does not exist, skipped.");
                    continue;
                }

                TerrainSwapInfo terrainSwapInfo = _terrainSwapInfoById[terrainSwap];
                terrainSwapInfo.Id = terrainSwap;
                _terrainSwapInfoByMap.Add(mapId, terrainSwapInfo);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} terrain swap defaults in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        void LoadAreaPhases()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                         0       1
            SQLResult result = DB.World.Query("SELECT AreaId, PhaseId FROM `phase_area`");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 phase areas. DB table `phase_area` is empty.");
                return;
            }

            PhaseInfoStruct getOrCreatePhaseIfMissing(int phaseId)
            {
                PhaseInfoStruct phaseInfo = _phaseInfoById[phaseId];
                phaseInfo.Id = phaseId;
                return phaseInfo;
            }

            uint count = 0;
            do
            {
                int area = result.Read<int>(0);
                int phaseId = result.Read<int>(1);

                if (!CliDB.AreaTableStorage.ContainsKey(area))
                {
                    Log.outError(LogFilter.Sql, $"Area {area} defined in `phase_area` does not exist, skipped.");
                    continue;
                }

                if (!CliDB.PhaseStorage.ContainsKey(phaseId))
                {
                    Log.outError(LogFilter.Sql, $"Phase {phaseId} defined in `phase_area` does not exist, skipped.");
                    continue;
                }

                PhaseInfoStruct phase = getOrCreatePhaseIfMissing(phaseId);
                phase.Areas.Add(area);
                _phaseInfoByArea.Add(area, new PhaseAreaInfo(phase));

                ++count;
            } while (result.NextRow());

            foreach (var pair in _phaseInfoByArea)
            {
                int parentAreaId = pair.Key;
                do
                {
                    AreaTableRecord area = CliDB.AreaTableStorage.LookupByKey(parentAreaId);
                    if (area == null)
                        break;

                    parentAreaId = area.ParentAreaID;
                    if (parentAreaId == 0)
                        break;

                    var parentAreaPhases = _phaseInfoByArea.LookupByKey(parentAreaId);
                    foreach (PhaseAreaInfo parentAreaPhase in parentAreaPhases)
                        if (parentAreaPhase.PhaseInfo.Id == pair.Value.PhaseInfo.Id)
                            parentAreaPhase.SubAreaExclusions.Add(pair.Key);

                } while (true);
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} phase areas in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadNPCSpellClickSpells()
        {
            uint oldMSTime = Time.GetMSTime();

            _spellClickInfoStorage.Clear();
            //                                           0          1         2            3
            SQLResult result = DB.World.Query("SELECT npc_entry, spell_id, cast_flags, user_type FROM npc_spellclick_spells");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spellclick spells. DB table `npc_spellclick_spells` is empty.");
                return;
            }

            uint count = 0;

            do
            {
                int npc_entry = result.Read<int>(0);
                CreatureTemplate cInfo = GetCreatureTemplate(npc_entry);
                if (cInfo == null)
                {
                    Log.outError(LogFilter.Sql, $"Table npc_spellclick_spells references unknown creature_template {npc_entry}. Skipping entry.");
                    continue;
                }

                int spellid = result.Read<int>(1);
                SpellInfo spellinfo = Global.SpellMgr.GetSpellInfo(spellid, Difficulty.None);
                if (spellinfo == null)
                {
                    Log.outError(LogFilter.Sql, $"Table npc_spellclick_spells creature: {npc_entry} references unknown spellid {spellid}. Skipping entry.");
                    continue;
                }

                SpellClickUserTypes userType = (SpellClickUserTypes)result.Read<byte>(3);
                if (userType >= SpellClickUserTypes.Max)
                    Log.outError(LogFilter.Sql, $"Table npc_spellclick_spells creature: {npc_entry} references unknown user Type {userType}. Skipping entry.");

                byte castFlags = result.Read<byte>(2);
                SpellClickInfo info = new();
                info.spellId = spellid;
                info.castFlags = castFlags;
                info.userType = userType;
                _spellClickInfoStorage.Add(npc_entry, info);

                ++count;
            }
            while (result.NextRow());

            // all spellclick data loaded, now we check if there are creatures with NPC_FLAG_SPELLCLICK but with no data
            // NOTE: It *CAN* be the other way around: no spellclick flag but with spellclick data, in case of creature-only vehicle accessories
            var ctc = GetCreatureTemplates();
            foreach (var creature in ctc.Values)
            {
                if (creature.Npcflag.HasAnyFlag(NPCFlags1.SpellClick) && !_spellClickInfoStorage.ContainsKey(creature.Entry))
                {
                    Log.outError(LogFilter.Sql,
                        $"npc_spellclick_spells: Creature template {creature.Entry} has UNIT_NPC_FLAG_SPELLCLICK but no data in spellclick table! Removing flag.");
                    creature.Npcflag &= ~NPCFlags1.SpellClick;
                }
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} spellclick definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadFishingBaseSkillLevel()
        {
            uint oldMSTime = Time.GetMSTime();

            _fishingBaseForAreaStorage.Clear();                            // for reload case

            SQLResult result = DB.World.Query("SELECT entry, skill FROM skill_fishing_base_level");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 areas for fishing base skill level. DB table `skill_fishing_base_level` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int entry = result.Read<int>(0);
                int skill = result.Read<int>(1);

                AreaTableRecord fArea = CliDB.AreaTableStorage.LookupByKey(entry);
                if (fArea == null)
                {
                    Log.outError(LogFilter.Sql, $"AreaId {entry} defined in `skill_fishing_base_level` does not exist.");
                    continue;
                }

                _fishingBaseForAreaStorage[entry] = skill;
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} areas for fishing base skill level in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadSkillTiers()
        {
            uint oldMSTime = Time.GetMSTime();

            _skillTiers.Clear();

            SQLResult result = DB.World.Query("SELECT ID, Value1, Value2, Value3, Value4, Value5, Value6, Value7, Value8, Value9, Value10, " +
                " Value11, Value12, Value13, Value14, Value15, Value16 FROM skill_tiers");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 skill max values. DB table `skill_tiers` is empty.");
                return;
            }

            do
            {
                int id = result.Read<int>(0);
                SkillTiersEntry tier = new();
                for (int i = 0; i < SkillConst.MaxSkillStep; ++i)
                    tier.Value[i] = result.Read<int>(1 + i);

                _skillTiers[id] = tier;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_skillTiers.Count} skill max values in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public PhaseInfoStruct GetPhaseInfo(int phaseId)
        {
            return _phaseInfoById.LookupByKey(phaseId);
        }

        public List<PhaseAreaInfo> GetPhasesForArea(int areaId)
        {
            return _phaseInfoByArea.LookupByKey(areaId);
        }

        public TerrainSwapInfo GetTerrainSwapInfo(int terrainSwapId)
        {
            return _terrainSwapInfoById.LookupByKey(terrainSwapId);
        }

        public List<SpellClickInfo> GetSpellClickInfoMapBounds(int creature_id)
        {
            return _spellClickInfoStorage.LookupByKey(creature_id);
        }

        public SkillTiersEntry GetSkillTier(int skillTierId)
        {
            return _skillTiers.LookupByKey(skillTierId);
        }

        public MultiMap<int, TerrainSwapInfo> GetTerrainSwaps() { return _terrainSwapInfoByMap; }

        //Locales
        public void LoadCreatureLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _creatureLocaleStorage.Clear(); // need for reload case

            //                                         0      1       2     3        4      5
            SQLResult result = DB.World.Query("SELECT entry, locale, Name, NameAlt, Title, TitleAlt FROM creature_template_locale");

            if (result.IsEmpty())
                return;

            do
            {
                int id = result.Read<int>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                if (!_creatureLocaleStorage.ContainsKey(id))
                    _creatureLocaleStorage[id] = new CreatureLocale();

                CreatureLocale data = _creatureLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Name);
                AddLocaleString(result.Read<string>(3), locale, data.NameAlt);
                AddLocaleString(result.Read<string>(4), locale, data.Title);
                AddLocaleString(result.Read<string>(5), locale, data.TitleAlt);

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_creatureLocaleStorage.Count} creature locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadGameObjectLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _gameObjectLocaleStorage.Clear(); // need for reload case

            //                                         0      1       2     3               4
            SQLResult result = DB.World.Query("SELECT entry, locale, name, castBarCaption, unk1 FROM gameobject_template_locale");
            if (result.IsEmpty())
                return;

            do
            {
                int id = result.Read<int>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                if (!_gameObjectLocaleStorage.ContainsKey(id))
                    _gameObjectLocaleStorage[id] = new GameObjectLocale();

                GameObjectLocale data = _gameObjectLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Name);
                AddLocaleString(result.Read<string>(3), locale, data.CastBarCaption);
                AddLocaleString(result.Read<string>(4), locale, data.Unk1);

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_gameObjectLocaleStorage.Count} gameobject_template_locale locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadQuestTemplateLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            _questObjectivesLocaleStorage.Clear(); // need for reload case
            //                                         0     1     2           3                 4                5                 6                  7                   8                   9                  10
            SQLResult result = DB.World.Query("SELECT Id, locale, LogTitle, LogDescription, QuestDescription, AreaDescription, PortraitGiverText, PortraitGiverName, PortraitTurnInText, PortraitTurnInName, QuestCompletionLog" +
                " FROM quest_template_locale");

            if (result.IsEmpty())
                return;

            do
            {
                int id = result.Read<int>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                if (!_questTemplateLocaleStorage.ContainsKey(id))
                    _questTemplateLocaleStorage[id] = new QuestTemplateLocale();

                QuestTemplateLocale data = _questTemplateLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.LogTitle);
                AddLocaleString(result.Read<string>(3), locale, data.LogDescription);
                AddLocaleString(result.Read<string>(4), locale, data.QuestDescription);
                AddLocaleString(result.Read<string>(5), locale, data.AreaDescription);
                AddLocaleString(result.Read<string>(6), locale, data.PortraitGiverText);
                AddLocaleString(result.Read<string>(7), locale, data.PortraitGiverName);
                AddLocaleString(result.Read<string>(8), locale, data.PortraitTurnInText);
                AddLocaleString(result.Read<string>(9), locale, data.PortraitTurnInName);
                AddLocaleString(result.Read<string>(10), locale, data.QuestCompletionLog);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_questTemplateLocaleStorage.Count} Quest Tempalate locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadQuestObjectivesLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            _questObjectivesLocaleStorage.Clear(); // need for reload case
            //                                        0     1          2
            SQLResult result = DB.World.Query("SELECT Id, locale, Description FROM quest_objectives_locale");
            if (result.IsEmpty())
                return;

            do
            {
                int id = result.Read<int>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                if (!_questObjectivesLocaleStorage.ContainsKey(id))
                    _questObjectivesLocaleStorage[id] = new QuestObjectivesLocale();

                QuestObjectivesLocale data = _questObjectivesLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Description);
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_questObjectivesLocaleStorage.Count} Quest Objectives locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadQuestGreetingLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            for (var i = 0; i < 2; ++i)
                _questGreetingLocaleStorage[i] = new Dictionary<int, QuestGreetingLocale>();

            //                                         0   1     2       3
            SQLResult result = DB.World.Query("SELECT Id, Type, locale, Greeting FROM quest_greeting_locale");
            if (result.IsEmpty())
                return;

            uint count = 0;
            do
            {
                int id = result.Read<int>(0);
                byte type = result.Read<byte>(1);
                switch (type)
                {
                    case 0: // Creature
                        if (GetCreatureTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `quest_greeting_locale`: creature template entry {id} does not exist.");
                            continue;
                        }
                        break;
                    case 1: // GameObject
                        if (GetGameObjectTemplate(id) == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `quest_greeting_locale`: gameobject template entry {id} does not exist.");
                            continue;
                        }
                        break;
                    default:
                        continue;
                }

                string localeName = result.Read<string>(2);

                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                if (!_questGreetingLocaleStorage[type].ContainsKey(id))
                    _questGreetingLocaleStorage[type][id] = new QuestGreetingLocale();

                QuestGreetingLocale data = _questGreetingLocaleStorage[type][id];
                AddLocaleString(result.Read<string>(3), locale, data.Greeting);
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Quest Greeting locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadQuestOfferRewardLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            _questOfferRewardLocaleStorage.Clear(); // need for reload case
            //                                         0     1          2
            SQLResult result = DB.World.Query("SELECT Id, locale, RewardText FROM quest_offer_reward_locale");
            if (result.IsEmpty())
                return;

            do
            {
                int id = result.Read<int>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                if (!_questOfferRewardLocaleStorage.ContainsKey(id))
                    _questOfferRewardLocaleStorage[id] = new QuestOfferRewardLocale();

                QuestOfferRewardLocale data = _questOfferRewardLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.RewardText);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_questOfferRewardLocaleStorage.Count} Quest Offer Reward locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadQuestRequestItemsLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            _questRequestItemsLocaleStorage.Clear();             // need for reload case
            //                                         0     1          2
            SQLResult result = DB.World.Query("SELECT Id, locale, CompletionText FROM quest_request_items_locale");
            if (result.IsEmpty())
                return;

            do
            {
                int id = result.Read<int>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                if (!_questRequestItemsLocaleStorage.ContainsKey(id))
                    _questRequestItemsLocaleStorage[id] = new QuestRequestItemsLocale();

                QuestRequestItemsLocale data = _questRequestItemsLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.CompletionText);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_questRequestItemsLocaleStorage.Count} Quest Request Items locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadGossipMenuItemsLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _gossipMenuItemsLocaleStorage.Clear();                              // need for reload case

            //                                         0       1            2       3           4
            SQLResult result = DB.World.Query("SELECT MenuId, OptionID, Locale, OptionText, BoxText FROM gossip_menu_option_locale");
            if (result.IsEmpty())
                return;

            do
            {
                int menuId = result.Read<int>(0);
                int optionIndex = result.Read<int>(1);
                string localeName = result.Read<string>(2);

                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                GossipMenuItemsLocale data = new();
                AddLocaleString(result.Read<string>(3), locale, data.OptionText);
                AddLocaleString(result.Read<string>(4), locale, data.BoxText);

                _gossipMenuItemsLocaleStorage[(menuId, optionIndex)] = data;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_gossipMenuItemsLocaleStorage.Count} gossip_menu_option locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadPageTextLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _pageTextLocaleStorage.Clear(); // needed for reload case

            //                                         0      1     2
            SQLResult result = DB.World.Query("SELECT ID, locale, `Text` FROM page_text_locale");
            if (result.IsEmpty())
                return;

            do
            {
                int id = result.Read<int>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                if (!_pageTextLocaleStorage.ContainsKey(id))
                    _pageTextLocaleStorage[id] = new PageTextLocale();

                PageTextLocale data = _pageTextLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Text);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_pageTextLocaleStorage.Count} PageText locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadPointOfInterestLocales()
        {
            uint oldMSTime = Time.GetMSTime();

            _pointOfInterestLocaleStorage.Clear(); // need for reload case

            //                                        0      1      2
            SQLResult result = DB.World.Query("SELECT ID, locale, Name FROM points_of_interest_locale");
            if (result.IsEmpty())
                return;

            do
            {
                int id = result.Read<int>(0);
                string localeName = result.Read<string>(1);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                    continue;

                if (!_pointOfInterestLocaleStorage.ContainsKey(id))
                    _pointOfInterestLocaleStorage[id] = new PointOfInterestLocale();

                PointOfInterestLocale data = _pointOfInterestLocaleStorage[id];
                AddLocaleString(result.Read<string>(2), locale, data.Name);
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_pointOfInterestLocaleStorage.Count} points_of_interest locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public CreatureLocale GetCreatureLocale(int entry)
        {
            return _creatureLocaleStorage.LookupByKey(entry);
        }

        public GameObjectLocale GetGameObjectLocale(int entry)
        {
            return _gameObjectLocaleStorage.LookupByKey(entry);
        }

        public QuestTemplateLocale GetQuestLocale(int entry)
        {
            return _questTemplateLocaleStorage.LookupByKey(entry);
        }

        public QuestOfferRewardLocale GetQuestOfferRewardLocale(int entry)
        {
            return _questOfferRewardLocaleStorage.LookupByKey(entry);
        }

        public QuestRequestItemsLocale GetQuestRequestItemsLocale(int entry)
        {
            return _questRequestItemsLocaleStorage.LookupByKey(entry);
        }

        public QuestObjectivesLocale GetQuestObjectivesLocale(int entry)
        {
            return _questObjectivesLocaleStorage.LookupByKey(entry);
        }

        public GossipMenuItemsLocale GetGossipMenuItemsLocale(int menuId, int optionIndex)
        {
            return _gossipMenuItemsLocaleStorage.LookupByKey((menuId, optionIndex));
        }

        public PageTextLocale GetPageTextLocale(int entry)
        {
            return _pageTextLocaleStorage.LookupByKey(entry);
        }

        public PointOfInterestLocale GetPointOfInterestLocale(int id)
        {
            return _pointOfInterestLocaleStorage.LookupByKey(id);
        }

        //General
        public void LoadReputationRewardRate()
        {
            uint oldMSTime = Time.GetMSTime();

            _repRewardRateStorage.Clear();                             // for reload case

            //                                          0          1             2                  3                  4                 5                      6             7
            SQLResult result = DB.World.Query("SELECT faction, quest_rate, quest_daily_rate, quest_weekly_rate, quest_monthly_rate, quest_repeatable_rate, creature_rate, spell_rate FROM reputation_reward_rate");
            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "Loaded `reputation_reward_rate`, table is empty!");
                return;
            }
            uint count = 0;
            do
            {
                int factionId = result.Read<int>(0);

                RepRewardRate repRate = new();

                repRate.questRate = result.Read<float>(1);
                repRate.questDailyRate = result.Read<float>(2);
                repRate.questWeeklyRate = result.Read<float>(3);
                repRate.questMonthlyRate = result.Read<float>(4);
                repRate.questRepeatableRate = result.Read<float>(5);
                repRate.creatureRate = result.Read<float>(6);
                repRate.spellRate = result.Read<float>(7);

                FactionRecord factionEntry = CliDB.FactionStorage.LookupByKey(factionId);
                if (factionEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Faction (faction.dbc) {factionId} does not exist but is used in `reputation_reward_rate`.");
                    continue;
                }

                if (repRate.questRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table reputation_reward_rate has quest_rate with invalid rate {repRate.questRate}, skipping data for faction {factionId}.");
                    continue;
                }

                if (repRate.questDailyRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table reputation_reward_rate has quest_daily_rate with invalid rate {repRate.questDailyRate}, skipping data for faction {factionId}.");
                    continue;
                }

                if (repRate.questWeeklyRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table reputation_reward_rate has quest_weekly_rate with invalid rate {repRate.questWeeklyRate}, skipping data for faction {factionId}.");
                    continue;
                }

                if (repRate.questMonthlyRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table reputation_reward_rate has quest_monthly_rate with invalid rate {repRate.questMonthlyRate}, skipping data for faction {factionId}.");
                    continue;
                }

                if (repRate.questRepeatableRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table reputation_reward_rate has quest_repeatable_rate with invalid rate {repRate.questRepeatableRate}, skipping data for faction {factionId}.");
                    continue;
                }

                if (repRate.creatureRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table reputation_reward_rate has creature_rate with invalid rate {repRate.creatureRate}, skipping data for faction {factionId}.");
                    continue;
                }

                if (repRate.spellRate < 0.0f)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table reputation_reward_rate has spell_rate with invalid rate {repRate.spellRate}, skipping data for faction {factionId}.");
                    continue;
                }

                _repRewardRateStorage[factionId] = repRate;

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} reputation_reward_rate in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadReputationOnKill()
        {
            uint oldMSTime = Time.GetMSTime();

            // For reload case
            _repOnKillStorage.Clear();

            //                                                0            1                     2
            SQLResult result = DB.World.Query("SELECT creature_id, RewOnKillRepFaction1, RewOnKillRepFaction2, " +
                //   3             4             5                   6             7             8                   9
                "IsTeamAward1, MaxStanding1, RewOnKillRepValue1, IsTeamAward2, MaxStanding2, RewOnKillRepValue2, TeamDependent " +
                "FROM creature_onkill_reputation");

            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "oaded 0 creature award reputation definitions. DB table `creature_onkill_reputation` is empty.");
                return;
            }
            uint count = 0;
            do
            {
                int creature_id = result.Read<int>(0);

                ReputationOnKillEntry repOnKill = new();
                repOnKill.RepFaction1 = result.Read<ushort>(1);
                repOnKill.RepFaction2 = result.Read<ushort>(2);
                repOnKill.IsTeamAward1 = result.Read<bool>(3);
                repOnKill.ReputationMaxCap1 = result.Read<byte>(4);
                repOnKill.RepValue1 = result.Read<int>(5);
                repOnKill.IsTeamAward2 = result.Read<bool>(6);
                repOnKill.ReputationMaxCap2 = result.Read<byte>(7);
                repOnKill.RepValue2 = result.Read<int>(8);
                repOnKill.TeamDependent = result.Read<bool>(9);

                if (GetCreatureTemplate(creature_id) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature_onkill_reputation` have data for not existed creature entry ({creature_id}), skipped.");
                    continue;
                }

                if (repOnKill.RepFaction1 != 0)
                {
                    FactionRecord factionEntry1 = CliDB.FactionStorage.LookupByKey(repOnKill.RepFaction1);
                    if (factionEntry1 == null)
                    {
                        Log.outError(LogFilter.Sql, $"Faction (faction.dbc) {repOnKill.RepFaction1} does not exist but is used in `creature_onkill_reputation`.");
                        continue;
                    }
                }

                if (repOnKill.RepFaction2 != 0)
                {
                    FactionRecord factionEntry2 = CliDB.FactionStorage.LookupByKey(repOnKill.RepFaction2);
                    if (factionEntry2 == null)
                    {
                        Log.outError(LogFilter.Sql, $"Faction (faction.dbc) {repOnKill.RepFaction2} does not exist but is used in `creature_onkill_reputation`.");
                        continue;
                    }
                }

                _repOnKillStorage[creature_id] = repOnKill;

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} creature award reputation definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadReputationSpilloverTemplate()
        {
            var oldMSTime = Time.GetMSTime();

            _repSpilloverTemplateStorage.Clear();                      // for reload case

            //                                        0        1         2       3       4         5       6       7         8       9       10        11      12      13        14      15
            SQLResult result = DB.World.Query("SELECT faction, faction1, rate_1, rank_1, faction2, rate_2, rank_2, faction3, rate_3, rank_3, faction4, rate_4, rank_4, faction5, rate_5, rank_5 FROM " +
                "reputation_spillover_template");
            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "Loaded `reputation_spillover_template`, table is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int factionId = result.Read<int>(0);

                RepSpilloverTemplate repTemplate = new();
                repTemplate.faction[0] = result.Read<ushort>(1);
                repTemplate.faction_rate[0] = result.Read<float>(2);
                repTemplate.faction_rank[0] = result.Read<byte>(3);
                repTemplate.faction[1] = result.Read<ushort>(4);
                repTemplate.faction_rate[1] = result.Read<float>(5);
                repTemplate.faction_rank[1] = result.Read<byte>(6);
                repTemplate.faction[2] = result.Read<ushort>(7);
                repTemplate.faction_rate[2] = result.Read<float>(8);
                repTemplate.faction_rank[2] = result.Read<byte>(9);
                repTemplate.faction[3] = result.Read<ushort>(10);
                repTemplate.faction_rate[3] = result.Read<float>(11);
                repTemplate.faction_rank[3] = result.Read<byte>(12);
                repTemplate.faction[4] = result.Read<ushort>(13);
                repTemplate.faction_rate[4] = result.Read<float>(14);
                repTemplate.faction_rank[4] = result.Read<byte>(15);

                var factionEntry = CliDB.FactionStorage.LookupByKey(factionId);
                if (factionEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Faction (faction.dbc) {factionId} does not exist but is used in `reputation_spillover_template`");
                    continue;
                }

                if (factionEntry.ParentFactionID == 0)
                {
                    Log.outError(LogFilter.Sql, $"Faction (faction.dbc) {factionId} in `reputation_spillover_template` does not belong to any team, skipping.");
                    continue;
                }

                bool invalidSpilloverFaction = false;
                for (var i = 0; i < SharedConst.SpilloverFactionsMax; ++i)
                {
                    if (repTemplate.faction[i] != 0)
                    {
                        var factionSpillover = CliDB.FactionStorage.LookupByKey(repTemplate.faction[i]);
                        if (factionSpillover == null || factionSpillover.Id == 0)
                        {
                            Log.outError(LogFilter.Sql, $"Spillover faction (faction.dbc) {repTemplate.faction[i]} does not exist but is used in `reputation_spillover_template` " +
                                $"for faction {factionId}, skipping.");
                            invalidSpilloverFaction = true;
                            break;
                        }

                        if (!factionSpillover.CanHaveReputation)
                        {
                            Log.outError(LogFilter.Sql, $"Spillover faction (faction.dbc) {repTemplate.faction[i]} for faction {factionId} in `reputation_spillover_template` " +
                                $"can not be listed for client, and then useless, skipping.");
                            invalidSpilloverFaction = true;
                            break;
                        }

                        if (repTemplate.faction_rank[i] >= (uint)ReputationRank.Max)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Rank {repTemplate.faction_rank[i]} used in `reputation_spillover_template` for spillover faction {repTemplate.faction[i]} is not valid, skipping.");
                            invalidSpilloverFaction = true;
                            break;
                        }
                    }
                }

                if (invalidSpilloverFaction)
                    continue;

                _repSpilloverTemplateStorage[factionId] = repTemplate;
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} reputation_spillover_template in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadTavernAreaTriggers()
        {
            uint oldMSTime = Time.GetMSTime();

            _tavernAreaTriggerStorage.Clear();                          // need for reload case

            SQLResult result = DB.World.Query("SELECT id FROM areatrigger_tavern");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 tavern triggers. DB table `areatrigger_tavern` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                ++count;

                int Trigger_ID = result.Read<int>(0);

                AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(Trigger_ID);
                if (atEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Area trigger (ID:{Trigger_ID}) does not exist in `AreaTrigger.dbc`.");
                    continue;
                }

                _tavernAreaTriggerStorage.Add(Trigger_ID);
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} tavern triggers in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadMailLevelRewards()
        {
            uint oldMSTime = Time.GetMSTime();

            _mailLevelRewardStorage.Clear();                           // for reload case

            //                                           0        1             2            3
            SQLResult result = DB.World.Query("SELECT level, raceMask, mailTemplateId, senderEntry FROM mail_level_reward");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 level dependent mail rewards. DB table `mail_level_reward` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                byte level = result.Read<byte>(0);
                RaceMask raceMask = (RaceMask)result.Read<ulong>(1);
                int mailTemplateId = result.Read<int>(2);
                int senderEntry = result.Read<int>(3);

                if (level > WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel))
                {
                    Log.outError(LogFilter.Sql,
                        $"Table `mail_level_reward` have data for level {level} that more supported by client ({WorldConfig.GetIntValue(WorldCfg.MaxPlayerLevel)}), ignoring.");
                    continue;
                }

                if (!raceMask.HasAnyFlag(RaceMask.Playable))
                {
                    Log.outError(LogFilter.Sql, $"Table `mail_level_reward` have raceMask ({raceMask}) for level {level} that not include any player races, ignoring.");
                    continue;
                }

                if (!CliDB.MailTemplateStorage.ContainsKey(mailTemplateId))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table `mail_level_reward` have invalid mailTemplateId ({mailTemplateId}) for level {level} that invalid not include any player races, ignoring.");
                    continue;
                }

                if (GetCreatureTemplate(senderEntry) == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Table `mail_level_reward` have not existed sender creature entry ({senderEntry}) for level {level} that invalid not include any player races, ignoring.");
                    continue;
                }

                _mailLevelRewardStorage.Add(level, new MailLevelReward(raceMask, mailTemplateId, senderEntry));

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} level dependent mail rewards in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadExplorationBaseXP()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.World.Query("SELECT level, basexp FROM exploration_basexp");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 BaseXP definitions. DB table `exploration_basexp` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                byte level = result.Read<byte>(0);
                int basexp = result.Read<int>(1);
                _baseXPTable[level] = basexp;
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} BaseXP definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadTempSummons()
        {
            uint oldMSTime = Time.GetMSTime();

            _tempSummonDataStorage.Clear();   // needed for reload case

            //                                             0           1             2        3      4           5           6           7            8           9
            SQLResult result = DB.World.Query("SELECT summonerId, summonerType, groupId, entry, position_x, position_y, position_z, orientation, summonType, summonTime FROM creature_summon_groups");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 temp summons. DB table `creature_summon_groups` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int summonerId = result.Read<int>(0);
                SummonerType summonerType = (SummonerType)result.Read<byte>(1);
                byte group = result.Read<byte>(2);

                switch (summonerType)
                {
                    case SummonerType.Creature:
                        if (GetCreatureTemplate(summonerId) == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `creature_summon_groups` has summoner with non existing entry {summonerId} for creature summoner Type, skipped.");
                            continue;
                        }
                        break;
                    case SummonerType.GameObject:
                        if (GetGameObjectTemplate(summonerId) == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `creature_summon_groups` has summoner with non existing entry {summonerId} for gameobject summoner Type, skipped.");
                            continue;
                        }
                        break;
                    case SummonerType.Map:
                        if (!CliDB.MapStorage.ContainsKey(summonerId))
                        {
                            Log.outError(LogFilter.Sql, $"Table `creature_summon_groups` has summoner with non existing entry {summonerId} for map summoner Type, skipped.");
                            continue;
                        }
                        break;
                    default:
                        Log.outError(LogFilter.Sql, $"Table `creature_summon_groups` has unhandled summoner Type {summonerType} for summoner {summonerId}, skipped.");
                        continue;
                }

                TempSummonData data = new();
                data.entry = result.Read<int>(3);

                if (GetCreatureTemplate(data.entry) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature_summon_groups` has creature in group [Summoner ID: {summonerId}, " +
                        $"Summoner Type: {summonerType}, Group ID: {group}] with non existing creature entry {data.entry}, skipped.");
                    continue;
                }

                float posX = result.Read<float>(4);
                float posY = result.Read<float>(5);
                float posZ = result.Read<float>(6);
                float orientation = result.Read<float>(7);

                data.pos = new Position(posX, posY, posZ, orientation);

                data.type = (TempSummonType)result.Read<byte>(8);

                if (data.type > TempSummonType.ManualDespawn)
                {
                    Log.outError(LogFilter.Sql, $"Table `creature_summon_groups` has unhandled temp summon Type {data.type} in group " +
                        $"[Summoner ID: {summonerId}, Summoner Type: {summonerType} [{(int)summonerType}], Group ID: {group}] for creature entry {data.entry}, skipped.");
                    continue;
                }

                data.time = TimeSpan.FromMilliseconds(result.Read<uint>(9));

                Tuple<int, SummonerType, byte> key = Tuple.Create(summonerId, summonerType, group);
                _tempSummonDataStorage.Add(key, data);

                ++count;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} temp summons in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadPageTexts()
        {
            uint oldMSTime = Time.GetMSTime();

            //                                        0    1      2           3                  4
            SQLResult result = DB.World.Query("SELECT ID, `text`, NextPageID, PlayerConditionID, Flags FROM page_text");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 page texts. DB table `page_text` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                int id = result.Read<int>(0);

                PageText pageText = new();
                pageText.Text = result.Read<string>(1);
                pageText.NextPageID = result.Read<int>(2);
                pageText.PlayerConditionID = result.Read<int>(3);
                pageText.Flags = result.Read<byte>(4);

                _pageTextStorage[id] = pageText;
                ++count;
            }
            while (result.NextRow());

            foreach (var pair in _pageTextStorage)
            {
                if (pair.Value.NextPageID != 0)
                {
                    if (!_pageTextStorage.ContainsKey(pair.Value.NextPageID))
                        Log.outError(LogFilter.Sql, $"Page text (ID: {pair.Key}) has non-existing `NextPageID` ({pair.Value.NextPageID}).");

                }
            }
            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} page texts in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadReservedPlayersNames()
        {
            uint oldMSTime = Time.GetMSTime();

            _reservedNamesStorage.Clear();                                // need for reload case

            SQLResult result = DB.Characters.Query("SELECT name FROM reserved_name");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 reserved player names. DB table `reserved_name` is empty!");
                return;
            }

            uint count = 0;
            do
            {
                string name = result.Read<string>(0);

                _reservedNamesStorage.Add(name.ToLower());
                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} reserved player names in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        //not very fast function but it is called only once a day, or on starting-up
        public void ReturnOrDeleteOldMails(bool serverUp)
        {
            uint oldMSTime = Time.GetMSTime();

            long curTime = GameTime.GetGameTime();
            DateTime lt = Time.UnixTimeToDateTime(curTime).ToLocalTime();
            Log.outInfo(LogFilter.Server, $"Returning mails current time: hour: {lt.Hour}, minute: {lt.Minute}, second: {lt.Second}.");

            PreparedStatement stmt;
            // Delete all old mails without item and without body immediately, if starting server
            if (!serverUp)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_EMPTY_EXPIRED_MAIL);
                stmt.AddValue(0, curTime);
                DB.Characters.Execute(stmt);
            }
            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_EXPIRED_MAIL);
            stmt.AddValue(0, curTime);

            {
                SQLResult result = DB.Characters.Query(stmt);            

                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "No expired mails found.");
                    return;                                             // any mails need to be returned or deleted
                }

                MultiMap<long, MailItemInfo> itemsCache = new();
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_EXPIRED_MAIL_ITEMS);
                stmt.AddValue(0, curTime);
                {
                    SQLResult items = DB.Characters.Query(stmt);
                
                    if (!items.IsEmpty())
                    {
                        MailItemInfo item = new();
                        do
                        {
                            item.item_guid = result.Read<long>(0);
                            item.item_template = result.Read<int>(1);
                            long mailId = result.Read<long>(2);
                            itemsCache.Add(mailId, item);
                        } while (items.NextRow());
                    }
                }

                uint deletedCount = 0;
                uint returnedCount = 0;
                do
                {
                    long receiver = result.Read<long>(3);
                    if (serverUp && Global.ObjAccessor.FindConnectedPlayer(ObjectGuid.Create(HighGuid.Player, receiver)) != null)
                        continue;

                    Mail m = new();
                    m.messageID = result.Read<long>(0);
                    m.messageType = (MailMessageType)result.Read<byte>(1);
                    m.sender = result.Read<long>(2);
                    m.receiver = receiver;
                    bool has_items = result.Read<bool>(4);
                    m.expire_time = result.Read<long>(5);
                    m.deliver_time = 0;
                    m.COD = result.Read<long>(6);
                    m.checkMask = (MailCheckFlags)result.Read<byte>(7);
                    m.mailTemplateId = result.Read<ushort>(8);

                    // Delete or return mail
                    if (has_items)
                    {
                        // read items from cache
                        List<MailItemInfo> temp = itemsCache[m.messageID];
                        Extensions.Swap(ref m.items, ref temp);

                        // if it is mail from non-player, or if it's already return mail, it shouldn't be returned, but deleted
                        if (m.messageType != MailMessageType.Normal || (m.checkMask.HasAnyFlag(MailCheckFlags.CodPayment | MailCheckFlags.Returned)))
                        {
                            // mail open and then not returned
                            foreach (var itemInfo in m.items)
                                Item.DeleteFromDB(null, itemInfo.item_guid);

                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_ITEM_BY_ID);
                            stmt.AddValue(0, m.messageID);
                            DB.Characters.Execute(stmt);
                        }
                        else
                        {
                            // Mail will be returned
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_MAIL_RETURNED);
                            stmt.AddValue(0, m.receiver);
                            stmt.AddValue(1, m.sender);
                            stmt.AddValue(2, curTime + 30 * Time.Day);
                            stmt.AddValue(3, curTime);
                            stmt.AddValue(4, (byte)MailCheckFlags.Returned);
                            stmt.AddValue(5, m.messageID);
                            DB.Characters.Execute(stmt);
                            foreach (var itemInfo in m.items)
                            {
                                // Update receiver in mail items for its proper delivery, and in instance_item for avoid lost item at sender delete
                                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_MAIL_ITEM_RECEIVER);
                                stmt.AddValue(0, m.sender);
                                stmt.AddValue(1, itemInfo.item_guid);
                                DB.Characters.Execute(stmt);

                                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ITEM_OWNER);
                                stmt.AddValue(0, m.sender);
                                stmt.AddValue(1, itemInfo.item_guid);
                                DB.Characters.Execute(stmt);
                            }
                            ++returnedCount;
                            continue;
                        }
                    }

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_BY_ID);
                    stmt.AddValue(0, m.messageID);
                    DB.Characters.Execute(stmt);
                    ++deletedCount;
                }
                while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, $"Processed {deletedCount + returnedCount} expired mails: {deletedCount} deleted and {returnedCount} " +
                    $"returned in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
            }
        }

        public void LoadSceneTemplates()
        {
            uint oldMSTime = Time.GetMSTime();
            _sceneTemplateStorage.Clear();
            //                                        0         1       2               3           4
            SQLResult result = DB.World.Query("SELECT SceneId, Flags, ScriptPackageID, Encrypted, ScriptName FROM scene_template");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 scene templates. DB table `scene_template` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int sceneId = result.Read<int>(0);
                SceneTemplate sceneTemplate = new();
                sceneTemplate.SceneId = sceneId;
                sceneTemplate.PlaybackFlags = (SceneFlags)result.Read<int>(1);
                sceneTemplate.ScenePackageId = result.Read<int>(2);
                sceneTemplate.Encrypted = result.Read<byte>(3) != 0;
                sceneTemplate.ScriptId = GetScriptId(result.Read<string>(4));

                _sceneTemplateStorage[sceneId] = sceneTemplate;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} scene templates in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadPlayerChoices()
        {
            uint oldMSTime = Time.GetMSTime();
            _playerChoices.Clear();
            //                                                     0               1           2                3         4         5                  6                   7                    8
            SQLResult choiceResult = DB.World.Query("SELECT ChoiceId, UiTextureKitId, SoundKitId, CloseSoundKitId, Duration, Question, PendingChoiceText, HideWarboardHeader, KeepOpenAfterChoice FROM playerchoice");
            if (choiceResult.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 player choices. DB table `playerchoice` is empty.");
                return;
            }

            uint responseCount = 0;
            uint rewardCount = 0;
            uint itemRewardCount = 0;
            uint currencyRewardCount = 0;
            uint factionRewardCount = 0;
            uint itemChoiceRewardCount = 0;
            uint mawPowersCount = 0;

            do
            {
                PlayerChoice choice = new();
                choice.ChoiceId = choiceResult.Read<int>(0);
                choice.UiTextureKitId = choiceResult.Read<int>(1);
                choice.SoundKitId = choiceResult.Read<int>(2);
                choice.CloseSoundKitId = choiceResult.Read<int>(3);
                choice.Duration = choiceResult.Read<long>(4);
                choice.Question = choiceResult.Read<string>(5);
                choice.PendingChoiceText = choiceResult.Read<string>(6);
                choice.HideWarboardHeader = choiceResult.Read<bool>(7);
                choice.KeepOpenAfterChoice = choiceResult.Read<bool>(8);

                _playerChoices[choice.ChoiceId] = choice;

            } while (choiceResult.NextRow());

            //                                                  0           1                   2               3       4            5
            SQLResult responses = DB.World.Query("SELECT ChoiceId, ResponseId, ResponseIdentifier, ChoiceArtFileId, Flags, WidgetSetID, " +
                //                     6           7        8               9      10      11        12              13          14            15              16
                "UiTextureAtlasElementID, SoundKitID, GroupID, UiTextureKitID, Answer, Header, SubHeader, ButtonTooltip, Description, Confirmation, RewardQuestID " +
                "FROM playerchoice_response ORDER BY `Index` ASC");
            if (!responses.IsEmpty())
            {
                do
                {
                    int choiceId = responses.Read<int>(0);
                    int responseId = responses.Read<int>(1);

                    if (!_playerChoices.ContainsKey(choiceId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped.");
                        continue;
                    }

                    PlayerChoice choice = _playerChoices[choiceId];
                    PlayerChoiceResponse response = new();

                    response.ResponseId = responseId;
                    response.ResponseIdentifier = responses.Read<short>(2);
                    response.ChoiceArtFileId = responses.Read<int>(3);
                    response.Flags = responses.Read<int>(4);
                    response.WidgetSetID = responses.Read<int>(5);
                    response.UiTextureAtlasElementID = responses.Read<int>(6);
                    response.SoundKitID = responses.Read<int>(7);
                    response.GroupID = responses.Read<byte>(8);
                    response.UiTextureKitID = responses.Read<int>(9);
                    response.Answer = responses.Read<string>(10);
                    response.Header = responses.Read<string>(11);
                    response.SubHeader = responses.Read<string>(12);
                    response.ButtonTooltip = responses.Read<string>(13);
                    response.Description = responses.Read<string>(14);
                    response.Confirmation = responses.Read<string>(15);
                    if (!responses.IsNull(16))
                        response.RewardQuestID = responses.Read<int>(16);

                    choice.Responses.Add(response);

                    ++responseCount;

                } while (responses.NextRow());
            }

            //                                                0           1        2          3            4               5                 6               7      8    9
            SQLResult rewards = DB.World.Query("SELECT ChoiceId, ResponseId, TitleId, PackageId, SkillLineId, SkillPointCount, ArenaPointCount, HonorPointCount, Money, Xp FROM playerchoice_response_reward");
            if (!rewards.IsEmpty())
            {
                do
                {
                    int choiceId = rewards.Read<int>(0);
                    int responseId = rewards.Read<int>(1);

                    if (!_playerChoices.ContainsKey(choiceId))
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped.");
                        continue;
                    }

                    PlayerChoice choice = _playerChoices[choiceId];
                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_reward` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped.");
                        continue;
                    }

                    PlayerChoiceResponseReward reward = new();
                    reward.TitleId = rewards.Read<int>(2);
                    reward.PackageId = rewards.Read<int>(3);
                    reward.SkillLineId = (SkillType)rewards.Read<int>(4);
                    reward.SkillPointCount = rewards.Read<int>(5);
                    reward.ArenaPointCount = rewards.Read<int>(6);
                    reward.HonorPointCount = rewards.Read<int>(7);
                    reward.Money = rewards.Read<long>(8);
                    reward.Xp = rewards.Read<int>(9);

                    if (reward.TitleId != 0 && !CliDB.CharTitlesStorage.ContainsKey(reward.TitleId))
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward` references non-existing Title {reward.TitleId} for ChoiceId {choiceId}, ResponseId: {responseId}, set to 0.");
                        reward.TitleId = 0;
                    }

                    if (reward.PackageId != 0 && Global.DB2Mgr.GetQuestPackageItems(reward.PackageId) == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward` references non-existing QuestPackage {reward.TitleId} for ChoiceId {choiceId}, ResponseId: {responseId}, set to 0.");
                        reward.PackageId = 0;
                    }

                    if (reward.SkillLineId != 0 && !CliDB.SkillLineStorage.ContainsKey((int)reward.SkillLineId))
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward` references non-existing SkillLine {reward.TitleId} for ChoiceId {choiceId}, ResponseId: {responseId}, set to 0.");
                        reward.SkillLineId = 0;
                        reward.SkillPointCount = 0;
                    }

                    response.Reward = reward;
                    ++rewardCount;

                } while (rewards.NextRow());
            }

            //                                                   0           1       2             4        5
            SQLResult rewardItem = DB.World.Query("SELECT ChoiceId, ResponseId, ItemId, BonusListIDs, Quantity FROM playerchoice_response_reward_item ORDER BY `Index` ASC");
            if (!rewardItem.IsEmpty())
            {
                do
                {
                    int choiceId = rewardItem.Read<int>(0);
                    int responseId = rewardItem.Read<int>(1);
                    int itemId = rewardItem.Read<int>(2);
                    StringArray bonusListIDsTok = new(rewardItem.Read<string>(3), ' ');
                    List<int> bonusListIds = new();
                    if (!bonusListIDsTok.IsEmpty())
                    {
                        foreach (int token in bonusListIDsTok)
                            bonusListIds.Add(token);
                    }

                    int quantity = rewardItem.Read<int>(4);

                    PlayerChoice choice = _playerChoices[choiceId];
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_item` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped.");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_item` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped.");
                        continue;
                    }

                    if (response.Reward == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_item` references non-existing player choice reward for ChoiceId {choiceId}, ResponseId: {responseId}, skipped.");
                        continue;
                    }

                    if (GetItemTemplate(itemId) == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_item` references non-existing item {itemId} for ChoiceId {choiceId}, ResponseId: {responseId}, skipped.");
                        continue;
                    }

                    response.Reward.Items.Add(new PlayerChoiceResponseRewardItem(itemId, bonusListIds, quantity));
                    itemRewardCount++;

                } while (rewardItem.NextRow());
            }

            //                                                       0           1           2        3
            SQLResult rewardCurrency = DB.World.Query("SELECT ChoiceId, ResponseId, CurrencyId, Quantity FROM playerchoice_response_reward_currency ORDER BY `Index` ASC");
            if (!rewardCurrency.IsEmpty())
            {
                do
                {
                    int choiceId = rewardCurrency.Read<int>(0);
                    int responseId = rewardCurrency.Read<int>(1);
                    int currencyId = rewardCurrency.Read<int>(2);
                    int quantity = rewardCurrency.Read<int>(3);

                    PlayerChoice choice = _playerChoices.LookupByKey(choiceId);
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_currency` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped.");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_currency` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped.");
                        continue;
                    }

                    if (response.Reward == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_currency` references non-existing player choice reward for ChoiceId {choiceId}, ResponseId: {responseId}, skipped.");
                        continue;
                    }

                    if (!CliDB.CurrencyTypesStorage.ContainsKey(currencyId))
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_currency` references non-existing currency {currencyId} for ChoiceId {choiceId}, ResponseId: {responseId}, skipped.");
                        continue;
                    }

                    response.Reward.Currency.Add(new PlayerChoiceResponseRewardEntry(currencyId, quantity));
                    currencyRewardCount++;

                } while (rewardCurrency.NextRow());
            }

            //                                                      0          1          2         3
            SQLResult rewardFaction = DB.World.Query("SELECT ChoiceId, ResponseId, FactionId, Quantity FROM playerchoice_response_reward_faction ORDER BY `Index` ASC");
            if (!rewardFaction.IsEmpty())
            {
                do
                {
                    int choiceId = rewardFaction.Read<int>(0);
                    int responseId = rewardFaction.Read<int>(1);
                    int factionId = rewardFaction.Read<int>(2);
                    int quantity = rewardFaction.Read<int>(3);

                    PlayerChoice choice = _playerChoices.LookupByKey(choiceId);
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_faction` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped.");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_faction` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped.");
                        continue;
                    }

                    if (response.Reward == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_faction` references non-existing player choice reward for ChoiceId {choiceId}, ResponseId: {responseId}, skipped.");
                        continue;
                    }

                    if (!CliDB.FactionStorage.ContainsKey(factionId))
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_faction` references non-existing faction {factionId} for ChoiceId {choiceId}, ResponseId: {responseId}, skipped.");
                        continue;
                    }

                    response.Reward.Faction.Add(new PlayerChoiceResponseRewardEntry(factionId, quantity));
                    factionRewardCount++;

                } while (rewardFaction.NextRow());
            }

            //                                                    0           1       2            3         4
            SQLResult rewardItems = DB.World.Query("SELECT ChoiceId, ResponseId, ItemId, BonusListIDs, Quantity FROM playerchoice_response_reward_item_choice ORDER BY `Index` ASC");
            if (!rewardItems.IsEmpty())
            {
                do
                {
                    int choiceId = rewardItems.Read<int>(0);
                    int responseId = rewardItems.Read<int>(1);
                    int itemId = rewardItems.Read<int>(2);
                    StringArray bonusListIDsTok = new(rewardItems.Read<string>(3), ' ');
                    List<int> bonusListIds = new();
                    foreach (int token in bonusListIDsTok)
                        bonusListIds.Add(token);

                    int quantity = rewardItems.Read<int>(4);

                    PlayerChoice choice = _playerChoices.LookupByKey(choiceId);
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_item_choice` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped.");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_item_choice` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped.");
                        continue;
                    }

                    if (response.Reward == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_item_choice` references non-existing player choice reward for ChoiceId {choiceId}, ResponseId: {responseId}, skipped.");
                        continue;
                    }

                    if (GetItemTemplate(itemId) == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `playerchoice_response_reward_item_choice` references non-existing item {itemId} for ChoiceId {choiceId}, ResponseId: {responseId}, skipped.");
                        continue;
                    }

                    response.Reward.ItemChoices.Add(new PlayerChoiceResponseRewardItem(itemId, bonusListIds, quantity));
                    itemChoiceRewardCount++;

                } while (rewards.NextRow());
            }

            //                                                        0          1               2      3             4       5          6
            SQLResult mawPowersResult = DB.World.Query("SELECT ChoiceId, ResponseId, TypeArtFileID, Rarity, RarityColor, SpellID, MaxStacks FROM playerchoice_response_maw_power");
            if (!mawPowersResult.IsEmpty())
            {
                do
                {
                    int choiceId = mawPowersResult.Read<int>(0);
                    int responseId = mawPowersResult.Read<int>(1);

                    PlayerChoice choice = _playerChoices.LookupByKey(choiceId);
                    if (choice == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_maw_power` references non-existing ChoiceId: {choiceId} (ResponseId: {responseId}), skipped.");
                        continue;
                    }

                    var response = choice.Responses.Find(playerChoiceResponse => { return playerChoiceResponse.ResponseId == responseId; });
                    if (response == null)
                    {
                        Log.outError(LogFilter.Sql, $"Table `playerchoice_response_maw_power` references non-existing ResponseId: {responseId} for ChoiceId {choiceId}, skipped.");
                        continue;
                    }

                    PlayerChoiceResponseMawPower mawPower = new();
                    mawPower.TypeArtFileID = mawPowersResult.Read<int>(2);
                    if (!mawPowersResult.IsNull(3))
                        mawPower.Rarity = mawPowersResult.Read<int>(3);
                    if (!mawPowersResult.IsNull(4))
                        mawPower.RarityColor = mawPowersResult.Read<int>(4);
                    mawPower.SpellID = mawPowersResult.Read<int>(5);
                    mawPower.MaxStacks = mawPowersResult.Read<int>(6);
                    response.MawPower = mawPower;

                    ++mawPowersCount;

                } while (mawPowersResult.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_playerChoices.Count} player choices, {responseCount} responses, {rewardCount} rewards, {itemRewardCount} item rewards, " +
                $"{currencyRewardCount} currency rewards, {factionRewardCount} faction rewards, {itemChoiceRewardCount} item choice rewards and {mawPowersCount} maw powers in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadPlayerChoicesLocale()
        {
            uint oldMSTime = Time.GetMSTime();

            // need for reload case
            _playerChoiceLocales.Clear();

            {
                //                                               0         1       2
                SQLResult result = DB.World.Query("SELECT ChoiceId, locale, Question FROM playerchoice_locale");
            
                if (!result.IsEmpty())
                {
                    do
                    {
                        int choiceId = result.Read<int>(0);
                        string localeName = result.Read<string>(1);
                        Locale locale = localeName.ToEnum<Locale>();
                        if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                            continue;

                        if (GetPlayerChoice(choiceId) == null)
                        {
                            Log.outError(LogFilter.Sql, $"Table `playerchoice_locale` references non-existing ChoiceId: {choiceId} for locale {localeName}, skipped.");
                            continue;
                        }

                        if (!_playerChoiceLocales.ContainsKey(choiceId))
                            _playerChoiceLocales[choiceId] = new PlayerChoiceLocale();

                        PlayerChoiceLocale data = _playerChoiceLocales[choiceId];
                        AddLocaleString(result.Read<string>(2), locale, data.Question);
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {_playerChoiceLocales.Count} Player Choice locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
                }

            }

            oldMSTime = Time.GetMSTime();

            {
                //                                            0         1           2       3       4       5          6               7            8
                SQLResult result = DB.World.Query("SELECT ChoiceID, ResponseID, locale, Answer, Header, SubHeader, ButtonTooltip, Description, Confirmation FROM playerchoice_response_locale");
            
                if (!result.IsEmpty())
                {
                    uint count = 0;
                    do
                    {
                        int choiceId = result.Read<int>(0);
                        int responseId = result.Read<int>(1);
                        string localeName = result.Read<string>(2);
                        Locale locale = localeName.ToEnum<Locale>();
                        if (!SharedConst.IsValidLocale(locale) || locale == SharedConst.DefaultLocale)
                            continue;

                        var playerChoiceLocale = _playerChoiceLocales.LookupByKey(choiceId);
                        if (playerChoiceLocale == null)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Table `playerchoice_locale` references non-existing ChoiceId: {choiceId} for ResponseId {responseId} locale {localeName}, skipped.");
                            continue;
                        }

                        PlayerChoice playerChoice = GetPlayerChoice(choiceId);
                        if (playerChoice.GetResponse(responseId) == null)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Table `playerchoice_locale` references non-existing ResponseId: {responseId} for ChoiceId {choiceId} locale {localeName}, skipped.");
                            continue;
                        }

                        PlayerChoiceResponseLocale data = playerChoiceLocale.Responses[responseId];
                        AddLocaleString(result.Read<string>(3), locale, data.Answer);
                        AddLocaleString(result.Read<string>(4), locale, data.Header);
                        AddLocaleString(result.Read<string>(5), locale, data.SubHeader);
                        AddLocaleString(result.Read<string>(6), locale, data.ButtonTooltip);
                        AddLocaleString(result.Read<string>(7), locale, data.Description);
                        AddLocaleString(result.Read<string>(8), locale, data.Confirmation);
                        count++;
                    } while (result.NextRow());

                    Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Player Choice Response locale strings in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
                }
            }
        }

        public void InitializeQueriesData(QueryDataGroup mask)
        {
            uint oldMSTime = Time.GetMSTime();

            // cache disabled
            if (!WorldConfig.GetBoolValue(WorldCfg.CacheDataQueries))
            {
                Log.outInfo(LogFilter.ServerLoading, "Query data caching is disabled. Skipped initialization.");
                return;
            }

            // Initialize Query data for creatures
            if (mask.HasAnyFlag(QueryDataGroup.Creatures))
                foreach (var creaturePair in creatureTemplateStorage)
                    creaturePair.Value.InitializeQueryData();

            // Initialize Query Data for gameobjects
            if (mask.HasAnyFlag(QueryDataGroup.Gameobjects))
                foreach (var gameobjectPair in _gameObjectTemplateStorage)
                    gameobjectPair.Value.InitializeQueryData();

            // Initialize Query Data for quests
            if (mask.HasAnyFlag(QueryDataGroup.Quests))
                foreach (var questPair in _questTemplates)
                    questPair.Value.InitializeQueryData();

            // Initialize Quest POI data
            if (mask.HasAnyFlag(QueryDataGroup.POIs))
                foreach (var poiPair in _questPOIStorage)
                    poiPair.Value.InitializeQueryData();

            Log.outInfo(LogFilter.ServerLoading, $"Initialized query cache data in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadJumpChargeParams()
        {
            uint oldMSTime = Time.GetMSTime();

            // need for reload case
            _jumpChargeParams.Clear();

            //                                         0   1      2                            3            4              5                6
            SQLResult result = DB.World.Query("SELECT id, speed, treatSpeedAsMoveTimeSeconds, jumpGravity, spellVisualId, progressCurveId, parabolicCurveId FROM jump_charge_params");
            if (result.IsEmpty())
                return;

            do
            {
                int id = result.Read<int>(0);
                float speed = result.Read<float>(1);
                bool treatSpeedAsMoveTimeSeconds = result.Read<bool>(2);
                float jumpGravity = result.Read<float>(3);
                int? spellVisualId = null;
                int? progressCurveId = null;
                int? parabolicCurveId = null;

                if (speed <= 0.0f)
                {
                    Log.outError(LogFilter.Sql, $"Table `jump_charge_params` uses invalid speed {speed} for id {id}, set to default charge speed {MotionMaster.SPEED_CHARGE}.");
                    speed = MotionMaster.SPEED_CHARGE;
                }

                if (jumpGravity <= 0.0f)
                {
                    Log.outError(LogFilter.Sql, $"Table `jump_charge_params` uses invalid jump gravity {jumpGravity} for id {id}, set to default {MotionMaster.gravity}.");
                    jumpGravity = (float)MotionMaster.gravity;
                }

                if (!result.IsNull(4))
                {
                    if (CliDB.SpellVisualStorage.ContainsKey(result.Read<int>(4)))
                        spellVisualId = result.Read<int>(4);
                    else
                        Log.outError(LogFilter.Sql, $"Table `jump_charge_params` references non-existing SpellVisual: {result.Read<int>(4)} for id {id}, ignored.");
                }

                if (!result.IsNull(5))
                {
                    if (CliDB.CurveStorage.ContainsKey(result.Read<int>(5)))
                        progressCurveId = result.Read<int>(5);
                    else
                        Log.outError(LogFilter.Sql, $"Table `jump_charge_params` references non-existing progress Curve: {result.Read<int>(5)} for id {id}, ignored.");
                }

                if (!result.IsNull(6))
                {
                    if (CliDB.CurveStorage.ContainsKey(result.Read<int>(6)))
                        parabolicCurveId = result.Read<int>(6);
                    else
                        Log.outError(LogFilter.Sql, $"Table `jump_charge_params` references non-existing parabolic Curve: {result.Read<int>(6)} for id {id}, ignored.");
                }

                JumpChargeParams jumpParams = new();
                jumpParams.Speed = speed;
                jumpParams.TreatSpeedAsMoveTimeSeconds = treatSpeedAsMoveTimeSeconds;
                jumpParams.JumpGravity = jumpGravity;
                jumpParams.SpellVisualId = spellVisualId;
                jumpParams.ProgressCurveId = progressCurveId;
                jumpParams.ParabolicCurveId = parabolicCurveId;
                _jumpChargeParams[id] = jumpParams;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_jumpChargeParams.Count} Jump Charge Params in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadPhaseNames()
        {
            uint oldMSTime = Time.GetMSTime();
            _phaseNameStorage.Clear();

            //                                          0     1
            SQLResult result = DB.World.Query("SELECT `ID`, `Name` FROM `phase_name`");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 phase names. DB table `phase_name` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int phaseId = result.Read<int>(0);
                string name = result.Read<string>(1);

                _phaseNameStorage[phaseId] = name;

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} phase names in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public MailLevelReward GetMailLevelReward(int level, Race race)
        {
            var mailList = _mailLevelRewardStorage.LookupByKey((byte)level);
            if (mailList.Empty())
                return null;

            foreach (var mailReward in mailList)
                if (mailReward.raceMask.HasRace(race))
                    return mailReward;

            return null;
        }

        public RepRewardRate GetRepRewardRate(int factionId)
        {
            return _repRewardRateStorage.LookupByKey(factionId);
        }

        public RepSpilloverTemplate GetRepSpillover(int factionId)
        {
            return _repSpilloverTemplateStorage.LookupByKey(factionId);
        }

        public ReputationOnKillEntry GetReputationOnKilEntry(int id)
        {
            return _repOnKillStorage.LookupByKey(id);
        }

        public void SetHighestGuids()
        {
            {
                SQLResult result = DB.Characters.Query("SELECT MAX(guid) FROM characters");
            
                if (!result.IsEmpty())
                    GetGuidSequenceGenerator(HighGuid.Player).Set(result.Read<long>(0) + 1);
            }

            {
                SQLResult result = DB.Characters.Query("SELECT MAX(guid) FROM item_instance");
            
                if (!result.IsEmpty())
                    GetGuidSequenceGenerator(HighGuid.Item).Set(result.Read<long>(0) + 1);
            }

            // Cleanup other tables from not existed guids ( >= hiItemGuid)
            DB.Characters.Execute($"DELETE FROM character_inventory WHERE item >= {GetGuidSequenceGenerator(HighGuid.Item).GetNextAfterMaxUsed()}");      // One-time query
            DB.Characters.Execute($"DELETE FROM mail_items WHERE item_guid >= {GetGuidSequenceGenerator(HighGuid.Item).GetNextAfterMaxUsed()}");          // One-time query
            DB.Characters.Execute("DELETE a, ab, ai FROM auctionhouse a LEFT JOIN auction_bidders ab ON ab.auctionId = a.id LEFT " +
                $"JOIN auction_items ai ON ai.auctionId = a.id WHERE ai.itemGuid >= '{GetGuidSequenceGenerator(HighGuid.Item).GetNextAfterMaxUsed()}'");       // One-time query
            DB.Characters.Execute($"DELETE FROM guild_bank_item WHERE item_guid >= {GetGuidSequenceGenerator(HighGuid.Item).GetNextAfterMaxUsed()}");     // One-time query

            {
                SQLResult result = DB.World.Query("SELECT MAX(guid) FROM transports");
            
                if (!result.IsEmpty())
                    GetGuidSequenceGenerator(HighGuid.Transport).Set(result.Read<long>(0) + 1);
            }

            {
                SQLResult result = DB.Characters.Query("SELECT MAX(id) FROM auctionhouse");
            
                if (!result.IsEmpty())
                    _auctionId = result.Read<int>(0) + 1;
            }

            {
                SQLResult result = DB.Characters.Query("SELECT MAX(id) FROM mail");
            
                if (!result.IsEmpty())
                    _mailId = result.Read<long>(0) + 1;
            }

            {
                SQLResult result = DB.Characters.Query("SELECT MAX(arenateamid) FROM arena_team");
            
                if (!result.IsEmpty())
                    Global.ArenaTeamMgr.SetNextArenaTeamId(result.Read<int>(0) + 1);
            }

            {
                SQLResult result = DB.Characters.Query("SELECT MAX(maxguid) FROM ((SELECT MAX(setguid) AS maxguid FROM character_equipmentsets) UNION " +
                    "(SELECT MAX(setguid) AS maxguid FROM character_transmog_outfits)) allsets");
            
                if (!result.IsEmpty())
                    _equipmentSetGuid = result.Read<long>(0) + 1;
            }

            {
                SQLResult result = DB.Characters.Query("SELECT MAX(guildId) FROM guild");
            
                if (!result.IsEmpty())
                    Global.GuildMgr.SetNextGuildId(result.Read<long>(0) + 1);
            }

            {
                SQLResult result = DB.Characters.Query("SELECT MAX(guid) FROM `groups`");
            
                if (!result.IsEmpty())
                    Global.GroupMgr.SetGroupDbStoreSize(result.Read<int>(0) + 1);
            }

            {
                SQLResult result = DB.Characters.Query("SELECT MAX(itemId) from character_void_storage");
            
                if (!result.IsEmpty())
                    _voidItemId = result.Read<long>(0) + 1;
            }

            {
                SQLResult result = DB.World.Query("SELECT MAX(guid) FROM creature");
            
                if (!result.IsEmpty())
                    _creatureSpawnId = result.Read<long>(0) + 1;
            }

            {
                SQLResult result = DB.World.Query("SELECT MAX(guid) FROM gameobject");
            
                if (!result.IsEmpty())
                    _gameObjectSpawnId = result.Read<long>(0) + 1;
            }
        }

        public int GenerateAuctionID()
        {
            if (_auctionId >= -2)
            {
                Log.outError(LogFilter.Server, "Auctions ids overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _auctionId++;
        }

        public long GenerateEquipmentSetGuid()
        {
            if (_equipmentSetGuid >= -2)
            {
                Log.outError(LogFilter.Server, "EquipmentSet guid overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _equipmentSetGuid++;
        }

        public long GenerateMailID()
        {
            if (_mailId >= -2)
            {
                Log.outError(LogFilter.Server, "Mail ids overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _mailId++;
        }

        public int GeneratePetNumber()
        {
            if (_hiPetNumber >= -2)
            {
                Log.outError(LogFilter.Misc, "_hiPetNumber Id overflow!! Can't continue, shutting down server.");
                Global.WorldMgr.StopNow(ShutdownExitCode.Error);
            }
            return _hiPetNumber++;
        }

        public long GenerateVoidStorageItemId()
        {
            if (_voidItemId >= -2)
            {
                Log.outError(LogFilter.Misc, "_voidItemId overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow(ShutdownExitCode.Error);
            }
            return _voidItemId++;
        }

        public long GenerateCreatureSpawnId()
        {
            if (_creatureSpawnId >= -2)
            {
                Log.outFatal(LogFilter.Server, "Creature spawn id overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _creatureSpawnId++;
        }

        public long GenerateGameObjectSpawnId()
        {
            if (_gameObjectSpawnId >= -2)
            {
                Log.outFatal(LogFilter.Server, "GameObject spawn id overflow!! Can't continue, shutting down server. ");
                Global.WorldMgr.StopNow();
            }
            return _gameObjectSpawnId++;
        }

        public ObjectGuidGenerator GetGenerator(HighGuid high)
        {
            Cypher.Assert(ObjectGuid.IsGlobal(high) || ObjectGuid.IsRealmSpecific(high), "Only global guid can be generated in ObjectMgr context");

            return GetGuidSequenceGenerator(high);
        }

        ObjectGuidGenerator GetGuidSequenceGenerator(HighGuid high)
        {
            if (!_guidGenerators.ContainsKey(high))
                _guidGenerators[high] = new ObjectGuidGenerator(high);

            return _guidGenerators[high];
        }

        public int GetBaseXP(int level)
        {
            return _baseXPTable.ContainsKey(level) ? _baseXPTable[level] : 0;
        }

        public int GetXPForLevel(int level)
        {
            if (level < _playerXPperLevel.Length)
                return _playerXPperLevel[level];
            return 0;
        }

        public int GetFishingBaseSkillLevel(AreaTableRecord areaEntry)
        {
            if (areaEntry == null)
                return 0;

            // Get level for the area
            var level = _fishingBaseForAreaStorage.LookupByKey(areaEntry.Id);
            if (level != 0)
                return level;

            // If there is no data for the current area and it has a parent area, get data from the last (recursive)
            var parentAreaEntry = CliDB.AreaTableStorage.LookupByKey(areaEntry.ParentAreaID);
            if (parentAreaEntry != null)
                return GetFishingBaseSkillLevel(parentAreaEntry);

            Log.outError(LogFilter.Sql, $"Fishable areaId {areaEntry.Id} is not properly defined in `skill_fishing_base_level`.");

            return 0;
        }

        public uint GetMaxLevelForExpansion(Expansion expansion)
        {
            switch (expansion)
            {
                case Expansion.Classic:
                    return 60;
                case Expansion.BurningCrusade:
                    return 70;
                case Expansion.WrathOfTheLichKing:
                case Expansion.Cataclysm:           // since we do support retail accounts, we will make all newer expansions follow the support max level
                case Expansion.MistsOfPandaria:
                case Expansion.WarlordsOfDraenor:
                case Expansion.Legion:
                case Expansion.BattleForAzeroth:
                case Expansion.ShadowLands:
                case Expansion.Dragonflight:
                    return 80;
                default:
                    break;
            }
            return 0;
        }

        CellObjectGuids CreateCellObjectGuids(int mapid, Difficulty difficulty, int cellid)
        {
            var key = (mapid, difficulty);

            if (!mapObjectGuidsStore.ContainsKey(key))
                mapObjectGuidsStore.Add(key, new Dictionary<int, CellObjectGuids>());

            if (!mapObjectGuidsStore[key].ContainsKey(cellid))
                mapObjectGuidsStore[key].Add(cellid, new CellObjectGuids());

            return mapObjectGuidsStore[key][cellid];
        }

        public CellObjectGuids GetCellObjectGuids(int mapid, Difficulty difficulty, int cellid)
        {
            var key = (mapid, difficulty);

            if (mapObjectGuidsStore.ContainsKey(key) && mapObjectGuidsStore[key].TryGetValue(cellid, out CellObjectGuids guids))
                return guids;

            return null;
        }

        public Dictionary<int, CellObjectGuids> GetMapObjectGuids(int mapid, Difficulty difficulty)
        {
            return mapObjectGuidsStore.LookupByKey((mapid, difficulty));
        }

        public PageText GetPageText(int pageEntry)
        {
            return _pageTextStorage.LookupByKey(pageEntry);
        }

        public int GetNearestTaxiNode(Vector3 point3d, int mapid, Team team)
        {
            bool found = false;
            float dist = 10000;
            int id = 0;

            TaxiNodeFlags requireFlag = (team == Team.Alliance) ? TaxiNodeFlags.ShowOnAllianceMap : TaxiNodeFlags.ShowOnHordeMap;
            foreach (var node in CliDB.TaxiNodesStorage.Values)
            {
                if (node.ContinentID != mapid || !node.HasFlag(requireFlag) || node.HasFlag(TaxiNodeFlags.IgnoreForFindNearest))
                    continue;

                var field = (node.Id - 1) / 8;
                byte submask = (byte)(1 << ((node.Id - 1) % 8));

                // skip not taxi network nodes
                if ((CliDB.TaxiNodesMask[field] & submask) == 0)
                    continue;

                float dist2 = Vector3.DistanceSquared(node.Pos, point3d);
                if (found)
                {
                    if (dist2 < dist)
                    {
                        dist = dist2;
                        id = node.Id;
                    }
                }
                else
                {
                    found = true;
                    dist = dist2;
                    id = node.Id;
                }
            }

            return id;
        }

        public void GetTaxiPath(int source, int destination, out int path, out uint cost)
        {
            var pathSet = CliDB.TaxiPathSetBySource.LookupByKey(source);
            if (pathSet == null)
            {
                path = 0;
                cost = 0;
                return;
            }

            var dest_i = pathSet.LookupByKey(destination);
            if (dest_i == null)
            {
                path = 0;
                cost = 0;
                return;
            }

            cost = dest_i.price;
            path = dest_i.Id;
        }

        public int GetTaxiMountDisplayId(int id, Team team, bool allowed_alt_team = false)
        {
            CreatureModel mountModel = new();
            CreatureTemplate mount_info = null;

            // select mount creature id
            TaxiNodesRecord node = CliDB.TaxiNodesStorage.LookupByKey(id);
            if (node != null)
            {
                int mount_entry = node.MountCreatureID(team);

                // Fix for Alliance not being able to use Acherus taxi
                // only one mount Type for both sides
                if (mount_entry == 0 && allowed_alt_team)
                {
                    // Simply reverse the selection. At least one team in theory should have a valid mount ID to choose.
                    if (team == Team.Alliance)
                        mount_entry = node.MountCreatureID(Team.Horde);
                    else
                        mount_entry = node.MountCreatureID(Team.Alliance);
                }

                mount_info = GetCreatureTemplate(mount_entry);
                if (mount_info != null)
                {
                    CreatureModel model = mount_info.GetRandomValidModel();
                    if (model == null)
                    {
                        Log.outError(LogFilter.Sql, $"No displayid found for the taxi mount with the entry {mount_entry}! Can't load it!");
                        return 0;
                    }
                    mountModel = model;
                }
            }

            // minfo is not actually used but the mount_id was updated
            GetCreatureModelRandomGender(ref mountModel, mount_info);

            return mountModel.CreatureDisplayID;
        }

        public AreaTriggerStruct GetAreaTrigger(int trigger)
        {
            return _areaTriggerStorage.LookupByKey(trigger);
        }

        public AccessRequirement GetAccessRequirement(int mapid, Difficulty difficulty)
        {
            return _accessRequirementStorage.LookupByKey((mapid, difficulty));
        }

        public bool IsTavernAreaTrigger(int Trigger_ID)
        {
            return _tavernAreaTriggerStorage.Contains(Trigger_ID);
        }

        public AreaTriggerStruct GetGoBackTrigger(int Map)
        {
            int? parentId = null;
            MapRecord mapEntry = CliDB.MapStorage.LookupByKey(Map);
            if (mapEntry == null || mapEntry.CorpseMapID < 0)
                return null;

            if (mapEntry.IsDungeon)
            {
                InstanceTemplate iTemplate = GetInstanceTemplate(Map);
                if (iTemplate != null)
                    parentId = iTemplate.Parent;
            }

            int entrance_map = parentId.GetValueOrDefault(mapEntry.CorpseMapID);
            foreach (var pair in _areaTriggerStorage)
            {
                if (pair.Value.target_mapId == entrance_map)
                {
                    AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(pair.Key);
                    if (atEntry != null && atEntry.ContinentID == Map)
                        return pair.Value;
                }
            }
            return null;
        }

        public AreaTriggerStruct GetMapEntranceTrigger(int Map)
        {
            foreach (var pair in _areaTriggerStorage)
            {
                if (pair.Value.target_mapId == Map)
                {
                    AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(pair.Key);
                    if (atEntry != null)
                        return pair.Value;
                }
            }
            return null;
        }

        public SceneTemplate GetSceneTemplate(int sceneId)
        {
            return _sceneTemplateStorage.LookupByKey(sceneId);
        }

        public List<TempSummonData> GetSummonGroup(int summonerId, SummonerType summonerType, byte group)
        {
            Tuple<int, SummonerType, byte> key = Tuple.Create(summonerId, summonerType, group);
            return _tempSummonDataStorage.LookupByKey(key);
        }

        public bool IsReservedName(string name)
        {
            return _reservedNamesStorage.Contains(name.ToLower());
        }

        public JumpChargeParams GetJumpChargeParams(int id)
        {
            return _jumpChargeParams.LookupByKey(id);
        }

        public string GetPhaseName(int phaseId)
        {
            return _phaseNameStorage.TryGetValue(phaseId, out string value) ? value : "Unknown Name";
        }

        public void LoadMapCorpsePositions()
        {
            uint oldMSTime = Time.GetMSTime();
            _mapCorpsePositionStorage.Clear();

            //                                            0     1    2
            SQLResult result = DB.World.Query("SELECT `MapId`, `X`, `Y` FROM `map_corpse_position`");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 map corpse positions. DB table `map_corpse_position` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                Vector2 corpse;
                int mapId = result.Read<int>(0);
                corpse.X = result.Read<float>(1);
                corpse.Y = result.Read<float>(2);

                _mapCorpsePositionStorage[mapId] = corpse;

                ++count;
            } while (result.NextRow());
            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} map corpse positions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        //Vehicles
        public void LoadVehicleTemplate()
        {
            uint oldMSTime = Time.GetMSTime();

            _vehicleTemplateStore.Clear();

            //                                         0           1
            SQLResult result = DB.World.Query("SELECT creatureId, despawnDelayMs FROM vehicle_template");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 vehicle template. DB table `vehicle_template` is empty.");
                return;
            }

            do
            {
                int creatureId = result.Read<int>(0);

                if (GetCreatureTemplate(creatureId) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `vehicle_template`: Vehicle {creatureId} does not exist.");
                    continue;
                }

                VehicleTemplate vehicleTemplate = new();
                vehicleTemplate.DespawnDelay = TimeSpan.FromMilliseconds(result.Read<int>(1));
                _vehicleTemplateStore[creatureId] = vehicleTemplate;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_vehicleTemplateStore.Count} Vehicle Template entries in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadVehicleTemplateAccessories()
        {
            uint oldMSTime = Time.GetMSTime();

            _vehicleTemplateAccessoryStore.Clear();                           // needed for reload case

            uint count = 0;

            //                                          0             1              2          3           4             5
            SQLResult result = DB.World.Query("SELECT `entry`, `accessory_entry`, `seat_id`, `minion`, `summontype`, `summontimer` FROM `vehicle_template_accessory`");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 vehicle template accessories. DB table `vehicle_template_accessory` is empty.");
                return;
            }

            do
            {
                int entry = result.Read<int>(0);
                int accessory = result.Read<int>(1);
                sbyte seatId = result.Read<sbyte>(2);
                bool isMinion = result.Read<bool>(3);
                byte summonType = result.Read<byte>(4);
                uint summonTimer = result.Read<uint>(5);

                if (GetCreatureTemplate(entry) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `vehicle_template_accessory`: creature template entry {entry} does not exist.");
                    continue;
                }

                if (GetCreatureTemplate(accessory) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `vehicle_template_accessory`: Accessory {accessory} does not exist.");
                    continue;
                }

                if (!_spellClickInfoStorage.ContainsKey(entry))
                {
                    Log.outError(LogFilter.Sql, $"Table `vehicle_template_accessory`: creature template entry {entry} has no data in npc_spellclick_spells.");
                    continue;
                }

                _vehicleTemplateAccessoryStore.Add(entry, new VehicleAccessory(accessory, seatId, isMinion, summonType, summonTimer));

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Vehicle Template Accessories in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadVehicleAccessories()
        {
            uint oldMSTime = Time.GetMSTime();

            _vehicleAccessoryStore.Clear();                           // needed for reload case

            uint count = 0;

            //                                          0             1             2          3           4             5
            SQLResult result = DB.World.Query("SELECT `guid`, `accessory_entry`, `seat_id`, `minion`, `summontype`, `summontimer` FROM `vehicle_accessory`");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, $"Loaded 0 Vehicle Accessories in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
                return;
            }

            do
            {
                uint uiGUID = result.Read<uint>(0);
                int uiAccessory = result.Read<int>(1);
                sbyte uiSeat = result.Read<sbyte>(2);
                bool bMinion = result.Read<bool>(3);
                byte uiSummonType = result.Read<byte>(4);
                uint uiSummonTimer = result.Read<uint>(5);

                if (GetCreatureTemplate(uiAccessory) == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `vehicle_accessory`: Accessory {uiAccessory} does not exist.");
                    continue;
                }

                _vehicleAccessoryStore.Add(uiGUID, new VehicleAccessory(uiAccessory, uiSeat, bMinion, uiSummonType, uiSummonTimer));

                ++count;
            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Vehicle Accessories in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadVehicleSeatAddon()
        {
            uint oldMSTime = Time.GetMSTime();

            _vehicleSeatAddonStore.Clear();                           // needed for reload case

            //                                          0            1                  2             3             4             5             6
            SQLResult result = DB.World.Query("SELECT `SeatEntry`, `SeatOrientation`, `ExitParamX`, `ExitParamY`, `ExitParamZ`, `ExitParamO`, `ExitParamValue` FROM `vehicle_seat_addon`");
            if (result.IsEmpty())
            {
                Log.outError(LogFilter.ServerLoading, "Loaded 0 vehicle seat addons. DB table `vehicle_seat_addon` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int seatID = result.Read<int>(0);
                float orientation = result.Read<float>(1);
                float exitX = result.Read<float>(2);
                float exitY = result.Read<float>(3);
                float exitZ = result.Read<float>(4);
                float exitO = result.Read<float>(5);
                VehicleExitParameters exitParam = (VehicleExitParameters)result.Read<byte>(6);

                if (!CliDB.VehicleSeatStorage.ContainsKey(seatID))
                {
                    Log.outError(LogFilter.Sql, $"Table `vehicle_seat_addon`: SeatID: {seatID} does not exist in VehicleSeat.dbc. Skipping entry.");
                    continue;
                }

                // Sanitizing values
                if (orientation > MathF.PI * 2)
                {
                    Log.outError(LogFilter.Sql, $"Table `vehicle_seat_addon`: SeatID: {seatID} is using invalid angle offset value ({orientation}). Set Value to 0.");
                    orientation = 0.0f;
                }

                if (exitParam >= VehicleExitParameters.VehicleExitParamMax)
                {
                    Log.outError(LogFilter.Sql, $"Table `vehicle_seat_addon`: SeatID: {seatID} is using invalid exit parameter value ({exitParam}). Setting to 0 (none).");
                    continue;
                }

                _vehicleSeatAddonStore[seatID] = new VehicleSeatAddon(orientation, exitX, exitY, exitZ, exitO, exitParam);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Vehicle Seat Addon entries in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public Vector2 GetMapCorpsePosition(int mapId)
        {
            return _mapCorpsePositionStorage.LookupByKey(mapId);
        }

        public VehicleTemplate GetVehicleTemplate(Vehicle veh)
        {
            return _vehicleTemplateStore.LookupByKey(veh.GetCreatureEntry());
        }

        public List<VehicleAccessory> GetVehicleAccessoryList(Vehicle veh)
        {
            Creature cre = veh.GetBase().ToCreature();
            if (cre != null)
            {
                // Give preference to GUID-based accessories
                var list = _vehicleAccessoryStore.LookupByKey(cre.GetSpawnId());
                if (!list.Empty())
                    return list;
            }

            // Otherwise return entry-based
            return _vehicleTemplateAccessoryStore.LookupByKey(veh.GetCreatureEntry());
        }

        public VehicleSeatAddon GetVehicleSeatAddon(int seatId)
        {
            return _vehicleSeatAddonStore.LookupByKey(seatId);
        }

        #region Fields
        //General
        Dictionary<uint, StringArray> CypherStringStorage = new();
        Dictionary<int, RepRewardRate> _repRewardRateStorage = new();
        Dictionary<int, ReputationOnKillEntry> _repOnKillStorage = new();
        Dictionary<int, RepSpilloverTemplate> _repSpilloverTemplateStorage = new();
        MultiMap<byte, MailLevelReward> _mailLevelRewardStorage = new();
        MultiMap<Tuple<int, SummonerType, byte>, TempSummonData> _tempSummonDataStorage = new();
        Dictionary<int /*choiceId*/, PlayerChoice> _playerChoices = new();
        Dictionary<int, PageText> _pageTextStorage = new();
        List<string> _reservedNamesStorage = new();
        Dictionary<int, SceneTemplate> _sceneTemplateStorage = new();
        Dictionary<int, JumpChargeParams> _jumpChargeParams = new();
        Dictionary<int, string> _phaseNameStorage = new();
        Dictionary<int, Vector2> _mapCorpsePositionStorage = new();

        Dictionary<Race, RaceUnlockRequirement> _raceUnlockRequirementStorage = new();
        List<RaceClassAvailability> _classExpansionRequirementStorage = new();

        //Quest
        Dictionary<int, Quest> _questTemplates = new();
        List<Quest> _questTemplatesAutoPush = new();
        MultiMap<int, int> _goQuestRelations = new();
        MultiMap<int, int> _goQuestInvolvedRelations = new();
        MultiMap<int, int> _goQuestInvolvedRelationsReverse = new();
        MultiMap<int, int> _creatureQuestRelations = new();
        MultiMap<int, int> _creatureQuestInvolvedRelations = new();
        MultiMap<int, int> _creatureQuestInvolvedRelationsReverse = new();
        MultiMap<int, int> _exclusiveQuestGroups = new();
        Dictionary<int, QuestPOIData> _questPOIStorage = new();
        MultiMap<int, int> _questAreaTriggerStorage = new();
        Dictionary<int, QuestObjective> _questObjectives = new();
        Dictionary<int, QuestGreeting>[] _questGreetingStorage = new Dictionary<int, QuestGreeting>[2];
        Dictionary<int, QuestGreetingLocale>[] _questGreetingLocaleStorage = new Dictionary<int, QuestGreetingLocale>[2];

        //Scripts
        ScriptNameContainer _scriptNamesStorage = new();
        MultiMap<int, int> spellScriptsStorage = new();
        public Dictionary<int, MultiMap<int, ScriptInfo>> sSpellScripts = new();
        public Dictionary<int, MultiMap<int, ScriptInfo>> sEventScripts = new();
        Dictionary<int, int> areaTriggerScriptStorage = new();
        List<int> _eventStorage = new();
        Dictionary<int, int> _eventScriptStorage = new();

        //Maps
        public Dictionary<int, GameTele> gameTeleStorage = new();
        Dictionary<(int mapId, Difficulty difficulty), Dictionary<int, CellObjectGuids>> mapObjectGuidsStore = new();
        Dictionary<(int mapId, Difficulty diffuculty, int phaseId), Dictionary<int, CellObjectGuids>> mapPersonalObjectGuidsStore = new();
        Dictionary<int, InstanceTemplate> instanceTemplateStorage = new();
        public MultiMap<int, GraveyardData> GraveyardStorage = new();
        List<ushort> _transportMaps = new();
        Dictionary<int, SpawnGroupTemplateData> _spawnGroupDataStorage = new();
        MultiMap<int, SpawnMetadata> _spawnGroupMapStorage = new();
        MultiMap<int, int> _spawnGroupsByMap = new();
        MultiMap<int, InstanceSpawnGroupInfo> _instanceSpawnGroupStorage = new();

        //Spells /Skills / Phases
        Dictionary<int, PhaseInfoStruct> _phaseInfoById = new();
        Dictionary<int, TerrainSwapInfo> _terrainSwapInfoById = new();
        MultiMap<int, PhaseAreaInfo> _phaseInfoByArea = new();
        MultiMap<int, TerrainSwapInfo> _terrainSwapInfoByMap = new();
        MultiMap<int, SpellClickInfo> _spellClickInfoStorage = new();
        Dictionary<int, int> _fishingBaseForAreaStorage = new();
        Dictionary<int, SkillTiersEntry> _skillTiers = new();

        //Gossip
        MultiMap<int, GossipMenus> gossipMenusStorage = new();
        MultiMap<int, GossipMenuItems> gossipMenuItemsStorage = new();
        Dictionary<int, GossipMenuAddon> _gossipMenuAddonStorage = new();
        Dictionary<int, PointOfInterest> pointsOfInterestStorage = new();

        //Creature
        Dictionary<int, CreatureTemplate> creatureTemplateStorage = new();
        Dictionary<int, CreatureModelInfo> creatureModelStorage = new();
        Dictionary<int, CreatureSummonedData> creatureSummonedDataStorage = new();
        Dictionary<long, CreatureData> creatureDataStorage = new();
        Dictionary<long, CreatureAddon> creatureAddonStorage = new();
        MultiMap<(int, Difficulty), int> creatureQuestItemStorage = new();
        Dictionary<int, CreatureAddon> creatureTemplateAddonStorage = new();
        MultiMap<int, float> _creatureTemplateSparringStorage = new();
        Dictionary<long, CreatureMovementData> creatureMovementOverrides = new();
        MultiMap<int, (int, EquipmentInfo)> equipmentInfoStorage = new();
        Dictionary<ObjectGuid, ObjectGuid> linkedRespawnStorage = new();
        Dictionary<(int, Class), CreatureBaseStats> creatureBaseStatsStorage = new();
        Dictionary<int, VendorItemData> cacheVendorItemStorage = new();
        Dictionary<int, Trainer> trainers = new();
        Dictionary<(int creatureId, int gossipMenuId, int gossipOptionIndex), int> _creatureDefaultTrainers = new();
        Dictionary<int, NpcText> npcTextStorage = new();

        //GameObject
        Dictionary<int, GameObjectTemplate> _gameObjectTemplateStorage = new();
        Dictionary<long, GameObjectData> gameObjectDataStorage = new();
        Dictionary<int, GameObjectTemplateAddon> _gameObjectTemplateAddonStorage = new();
        Dictionary<long, GameObjectOverride> _gameObjectOverrideStorage = new();
        Dictionary<long, GameObjectAddon> _gameObjectAddonStorage = new();
        MultiMap<int, int> _gameObjectQuestItemStorage = new();
        List<int> _gameObjectForQuestStorage = new();

        //Item
        Dictionary<int, ItemTemplate> ItemTemplateStorage = new();

        //Player
        Dictionary<(Race, Class), PlayerInfo> _playerInfo = new();

        //Faction Change
        public Dictionary<int /*alliance*/, int /*horde*/> FactionChangeAchievements = new();
        public Dictionary<int, int> FactionChangeItemsAllianceToHorde = new();
        public Dictionary<int, int> FactionChangeItemsHordeToAlliance = new();
        public Dictionary<int /*alliance*/, int /*horde*/> FactionChangeQuests = new();
        public Dictionary<int /*alliance*/, int /*horde*/> FactionChangeReputation = new();
        public Dictionary<int /*alliance*/, int /*horde*/> FactionChangeSpells = new();
        public Dictionary<int /*alliance*/, int /*horde*/> FactionChangeTitles = new();

        //Pets
        Dictionary<int, PetLevelInfo[]> petInfoStore = new();
        MultiMap<int, string> _petHalfName0 = new();
        MultiMap<int, string> _petHalfName1 = new();

        //Vehicles
        Dictionary<int, VehicleTemplate> _vehicleTemplateStore = new();
        MultiMap<int, VehicleAccessory> _vehicleTemplateAccessoryStore = new();
        MultiMap<long, VehicleAccessory> _vehicleAccessoryStore = new();
        Dictionary<int, VehicleSeatAddon> _vehicleSeatAddonStore = new();

        //Locales
        Dictionary<int, CreatureLocale> _creatureLocaleStorage = new();
        Dictionary<int, GameObjectLocale> _gameObjectLocaleStorage = new();
        Dictionary<int, QuestTemplateLocale> _questTemplateLocaleStorage = new();
        Dictionary<int, QuestObjectivesLocale> _questObjectivesLocaleStorage = new();
        Dictionary<int, QuestOfferRewardLocale> _questOfferRewardLocaleStorage = new();
        Dictionary<int, QuestRequestItemsLocale> _questRequestItemsLocaleStorage = new();
        Dictionary<(int, int), GossipMenuItemsLocale> _gossipMenuItemsLocaleStorage = new();
        Dictionary<int, PageTextLocale> _pageTextLocaleStorage = new();
        Dictionary<int, PointOfInterestLocale> _pointOfInterestLocaleStorage = new();
        Dictionary<int, PlayerChoiceLocale> _playerChoiceLocales = new();

        List<int> _tavernAreaTriggerStorage = new();
        Dictionary<int, AreaTriggerStruct> _areaTriggerStorage = new();
        Dictionary<(int, Difficulty), AccessRequirement> _accessRequirementStorage = new();
        Dictionary<int, WorldSafeLocsEntry> _worldSafeLocs = new();

        Dictionary<HighGuid, ObjectGuidGenerator> _guidGenerators = new();
        // first free id for selected id Type
        int _auctionId;
        long _equipmentSetGuid;
        long _mailId;
        int _hiPetNumber;
        long _creatureSpawnId;
        long _gameObjectSpawnId;
        long _voidItemId;
        int[] _playerXPperLevel;
        Dictionary<int, int> _baseXPTable = new();
        #endregion
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ScriptInfo
    {
        [FieldOffset(0)]
        public ScriptsType type;

        [FieldOffset(4)]
        public int id;

        [FieldOffset(8)]
        public uint delay;

        [FieldOffset(12)]
        public ScriptCommands command;

        [FieldOffset(16)]
        public raw Raw;

        [FieldOffset(16)]
        public talk Talk;

        [FieldOffset(16)]
        public emote Emote;

        [FieldOffset(16)]
        public fieldset FieldSet;

        [FieldOffset(16)]
        public moveto MoveTo;

        [FieldOffset(16)]
        public flagtoggle FlagToggle;

        [FieldOffset(16)]
        public teleportto TeleportTo;

        [FieldOffset(16)]
        public questexplored QuestExplored;

        [FieldOffset(16)]
        public killcredit KillCredit;

        [FieldOffset(16)]
        public respawngameobject RespawnGameObject;

        [FieldOffset(16)]
        public tempsummoncreature TempSummonCreature;

        [FieldOffset(16)]
        public toggledoor ToggleDoor;

        [FieldOffset(16)]
        public removeaura RemoveAura;

        [FieldOffset(16)]
        public castspell CastSpell;

        [FieldOffset(16)]
        public playsound PlaySound;

        [FieldOffset(16)]
        public createitem CreateItem;

        [FieldOffset(16)]
        public despawnself DespawnSelf;

        [FieldOffset(16)]
        public loadpath LoadPath;

        [FieldOffset(16)]
        public callscript CallScript;

        [FieldOffset(16)]
        public kill Kill;

        [FieldOffset(16)]
        public orientation Orientation;

        [FieldOffset(16)]
        public equip Equip;

        [FieldOffset(16)]
        public model Model;

        [FieldOffset(16)]
        public playmovie PlayMovie;

        [FieldOffset(16)]
        public movement Movement;

        [FieldOffset(16)]
        public playanimkit PlayAnimKit;

        public string GetDebugInfo()
        {
            return $"{command} ('{Global.ObjectMgr.GetScriptsTableNameByType(type)}' script id: {id})";
        }

        #region Structs
        public unsafe struct raw
        {
            public fixed uint nData[3];
            public fixed float fData[4];
        }

        public struct talk                   // TALK (0)
        {
            public ChatMsg ChatType;        // datalong
            public eScriptFlags Flags;           // datalong2
            public int TextID;          // dataint
        }

        public struct emote                   // EMOTE (1)
        {
            public int EmoteID;         // datalong
            public eScriptFlags Flags;           // datalong2
        }

        public struct fieldset                    // FIELDSET (2)
        {
            public uint FieldID;         // datalong
            public uint FieldValue;      // datalong2
        }

        public struct moveto                   // MOVETO (3)
        {
            public uint Unused1;         // datalong
            public uint TravelTime;      // datalong2
            public int Unused2;         // dataint

            public float DestX;
            public float DestY;
            public float DestZ;
        }

        public struct flagtoggle                   // FLAGSET (4)
        // FLAGREMOVE (5)
        {
            public uint FieldID;         // datalong
            public uint FieldValue;      // datalong2
        }

        public struct teleportto                 // TELEPORTTO (6)
        {
            public int MapID;           // datalong
            public eScriptFlags Flags;           // datalong2
            public int Unused1;         // dataint

            public float DestX;
            public float DestY;
            public float DestZ;
            public float Orientation;
        }

        public struct questexplored                  // QUESTEXPLORED (7)
        {
            public int QuestID;         // datalong
            public uint Distance;        // datalong2
        }

        public struct killcredit                    // KILLCREDIT (8)
        {
            public int CreatureEntry;   // datalong
            public eScriptFlags Flags;           // datalong2
        }

        public struct respawngameobject                 // RESPAWNGAMEOBJECT (9)
        {
            public uint GOGuid;          // datalong
            public uint DespawnDelay;    // datalong2
        }

        public struct tempsummoncreature                // TEMPSUMMONCREATURE (10)
        {
            public int CreatureEntry;   // datalong
            public uint DespawnDelay;    // datalong2
            public int Unused1;         // dataint

            public float PosX;
            public float PosY;
            public float PosZ;
            public float Orientation;
        }

        public struct toggledoor                  // CLOSEDOOR (12)
        // OPENDOOR (11)
        {
            public uint GOGuid;          // datalong
            public uint ResetDelay;      // datalong2
        }

        // ACTIVATEOBJECT (13)

        public struct removeaura                   // REMOVEAURA (14)
        {
            public int SpellID;         // datalong
            public eScriptFlags Flags;           // datalong2
        }

        public struct castspell                  // CASTSPELL (15)
        {
            public int SpellID;         // datalong
            public eScriptFlags Flags;           // datalong2
            public int CreatureEntry;   // dataint

            public float SearchRadius;
        }

        public struct playsound                     // PLAYSOUND (16)
        {
            public int SoundID;         // datalong
            public eScriptFlags Flags;           // datalong2
        }

        public struct createitem                   // CREATEITEM (17)
        {
            public int ItemEntry;       // datalong
            public int Amount;          // datalong2
        }

        public struct despawnself                 // DESPAWNSELF (18)
        {
            public uint DespawnDelay;    // datalong
        }

        public struct loadpath                    // LOADPATH (20)
        {
            public int PathID;          // datalong
            public int IsRepeatable;    // datalong2
        }

        public struct callscript                   // CALLSCRIPTTOUNIT (21)
        {
            public int CreatureEntry;   // datalong
            public int ScriptID;        // datalong2
            public int ScriptType;      // dataint
        }

        public struct kill                    // KILL (22)
        {
            public uint Unused1;         // datalong
            public uint Unused2;         // datalong2
            public int RemoveCorpse;    // dataint
        }

        public struct orientation                    // ORIENTATION (30)
        {
            public eScriptFlags Flags;           // datalong
            public uint Unused1;         // datalong2
            public int Unused2;         // dataint

            public float Unused3;
            public float Unused4;
            public float Unused5;
            public float _Orientation;
        }

        public struct equip                  // EQUIP (31)
        {
            public int EquipmentID;     // datalong
        }

        public struct model                    // MODEL (32)
        {
            public int ModelID;         // datalong
        }

        // CLOSEGOSSIP (33)

        public struct playmovie                    // PLAYMOVIE (34)
        {
            public int MovieID;         // datalong
        }

        public struct movement                      // SCRIPT_COMMAND_MOVEMENT (35)
        {
            public int MovementType;     // datalong
            public int MovementDistance; // datalong2
            public int Path;             // dataint
        }

        public struct playanimkit                  // SCRIPT_COMMAND_PLAY_ANIMKIT (36)
        {
            public int AnimKitID;       // datalong
        }
        #endregion
    }

    public class CellObjectGuids
    {
        public SortedSet<long> creatures = new();
        public SortedSet<long> gameobjects = new();

        public void AddSpawn(SpawnData data)
        {
            switch (data.type)
            {
                case SpawnObjectType.Creature:
                    creatures.Add(data.SpawnId);
                    break;
                case SpawnObjectType.GameObject:
                    gameobjects.Add(data.SpawnId);
                    break;
            }
        }

        public void RemoveSpawn(SpawnData data)
        {
            switch (data.type)
            {
                case SpawnObjectType.Creature:
                    creatures.Remove(data.SpawnId);
                    break;
                case SpawnObjectType.GameObject:
                    gameobjects.Remove(data.SpawnId);
                    break;
            }
        }
    }

    public class GameTele
    {
        public float posX;
        public float posY;
        public float posZ;
        public float orientation;
        public int mapId;
        public string name;
        public string nameLow;
    }

    public class PetLevelInfo
    {
        public PetLevelInfo()
        {
            health = 0;
            mana = 0;
        }

        public int[] stats = new int[(int)Stats.Max];
        public int health;
        public int mana;
        public int armor;
    }

    public struct InstanceSpawnGroupInfo
    {
        public byte BossStateId;
        public EncounterStateMask BossStates;
        public int SpawnGroupId;
        public InstanceSpawnGroupFlags Flags;
    }

    public class SpellClickInfo
    {
        public int spellId;
        public byte castFlags;
        public SpellClickUserTypes userType;

        // helpers
        public bool IsFitToRequirements(Unit clicker, Unit clickee)
        {
            Player playerClicker = clicker.ToPlayer();
            if (playerClicker == null)
                return true;

            Unit summoner = null;
            // Check summoners for party
            if (clickee.IsSummon())
                summoner = clickee.ToTempSummon().GetSummonerUnit();
            if (summoner == null)
                summoner = clickee;

            // This only applies to players
            switch (userType)
            {
                case SpellClickUserTypes.Friend:
                    if (!playerClicker.IsFriendlyTo(summoner))
                        return false;
                    break;
                case SpellClickUserTypes.Raid:
                    if (!playerClicker.IsInRaidWith(summoner))
                        return false;
                    break;
                case SpellClickUserTypes.Party:
                    if (!playerClicker.IsInPartyWith(summoner))
                        return false;
                    break;
                default:
                    break;
            }

            return true;
        }
    }

    public class WorldSafeLocsEntry
    {
        public int Id;
        public WorldLocation Loc;
    }

    public class GraveyardData
    {
        public int SafeLocId;
        public ConditionsReference Conditions;
    }

    public class QuestPOIBlobData
    {
        public int BlobIndex;
        public int ObjectiveIndex;
        public int QuestObjectiveID;
        public int QuestObjectID;
        public int MapID;
        public int UiMapID;
        public int Priority;
        public int Flags;
        public int WorldEffectID;
        public int PlayerConditionID;
        public int NavigationPlayerConditionID;
        public int SpawnTrackingID;
        public List<QuestPOIBlobPoint> Points;
        public bool AlwaysAllowMergingBlobs;

        public QuestPOIBlobData(int blobIndex, int objectiveIndex, int questObjectiveID, int questObjectID, int mapID, int uiMapID, int priority, int flags,
            int worldEffectID, int playerConditionID, int navigationPlayerConditionID, int spawnTrackingID, List<QuestPOIBlobPoint> points, bool alwaysAllowMergingBlobs)
        {
            BlobIndex = blobIndex;
            ObjectiveIndex = objectiveIndex;
            QuestObjectiveID = questObjectiveID;
            QuestObjectID = questObjectID;
            MapID = mapID;
            UiMapID = uiMapID;
            Priority = priority;
            Flags = flags;
            WorldEffectID = worldEffectID;
            PlayerConditionID = playerConditionID;
            NavigationPlayerConditionID = navigationPlayerConditionID;
            SpawnTrackingID = spawnTrackingID;
            Points = points;
            AlwaysAllowMergingBlobs = alwaysAllowMergingBlobs;
        }
    }

    public class QuestPOIBlobPoint
    {
        public int X;
        public int Y;
        public int Z;

        public QuestPOIBlobPoint(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class QuestPOIData
    {
        public int QuestID;
        public List<QuestPOIBlobData> Blobs;
        public ByteBuffer QueryDataBuffer;

        public QuestPOIData(int questId)
        {
            QuestID = questId;
            Blobs = new List<QuestPOIBlobData>();
            QueryDataBuffer = new ByteBuffer();
        }

        public void InitializeQueryData()
        {
            Write(QueryDataBuffer);
        }

        public void Write(ByteBuffer data)
        {
            data.WriteInt32(QuestID);
            data.WriteInt32(Blobs.Count);

            foreach (QuestPOIBlobData questPOIBlobData in Blobs)
            {
                data.WriteInt32(questPOIBlobData.BlobIndex);
                data.WriteInt32(questPOIBlobData.ObjectiveIndex);
                data.WriteInt32(questPOIBlobData.QuestObjectiveID);
                data.WriteInt32(questPOIBlobData.QuestObjectID);
                data.WriteInt32(questPOIBlobData.MapID);
                data.WriteInt32(questPOIBlobData.UiMapID);
                data.WriteInt32(questPOIBlobData.Priority);
                data.WriteInt32(questPOIBlobData.Flags);
                data.WriteInt32(questPOIBlobData.WorldEffectID);
                data.WriteInt32(questPOIBlobData.PlayerConditionID);
                data.WriteInt32(questPOIBlobData.NavigationPlayerConditionID);
                data.WriteInt32(questPOIBlobData.SpawnTrackingID);
                data.WriteInt32(questPOIBlobData.Points.Count);

                foreach (QuestPOIBlobPoint questPOIBlobPoint in questPOIBlobData.Points)
                {
                    data.WriteInt16((short)questPOIBlobPoint.X);
                    data.WriteInt16((short)questPOIBlobPoint.Y);
                    data.WriteInt16((short)questPOIBlobPoint.Z);
                }

                data.WriteBit(questPOIBlobData.AlwaysAllowMergingBlobs);
                data.FlushBits();
            }
        }
    }

    public class AreaTriggerStruct
    {
        public int target_mapId;
        public float target_X;
        public float target_Y;
        public float target_Z;
        public float target_Orientation;
    }

    public class MailLevelReward
    {
        public MailLevelReward(RaceMask _raceMask, int _mailTemplateId = 0, int _senderEntry = 0)
        {
            raceMask = _raceMask;
            mailTemplateId = _mailTemplateId;
            senderEntry = _senderEntry;
        }

        public RaceMask raceMask;
        public int mailTemplateId;
        public int senderEntry;
    }

    public class PageText
    {
        public string Text;
        public int NextPageID;
        public int PlayerConditionID;
        public byte Flags;
    }

    public struct ExtendedPlayerName
    {
        public ExtendedPlayerName(string name, string realmName)
        {
            Name = name;
            Realm = realmName;
        }

        public string Name;
        public string Realm;
    }

    public class LanguageDesc
    {
        public int SpellId;
        public int SkillId;

        public LanguageDesc() { }
        public LanguageDesc(int spellId, int skillId)
        {
            SpellId = spellId;
            SkillId = skillId;
        }

        public override int GetHashCode()
        {
            return SpellId.GetHashCode() ^ SkillId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is LanguageDesc)
                return (LanguageDesc)obj == this;

            return false;
        }

        public static bool operator ==(LanguageDesc left, LanguageDesc right)
        {
            return left.SpellId == right.SpellId && left.SkillId == right.SkillId;
        }

        public static bool operator !=(LanguageDesc left, LanguageDesc right)
        {
            return !(left == right);
        }
    }

    class ItemSpecStats
    {
        public ItemSpecStats(ItemRecord item, ItemSparseRecord sparse)
        {
            if (item.ClassID == ItemClass.Weapon)
            {
                ItemType = 5;
                switch (item.SubclassID.Weapon)
                {
                    case ItemSubClassWeapon.Axe:
                        AddStat(ItemSpecStat.OneHandedAxe);
                        break;
                    case ItemSubClassWeapon.Axe2:
                        AddStat(ItemSpecStat.TwoHandedAxe);
                        break;
                    case ItemSubClassWeapon.Bow:
                        AddStat(ItemSpecStat.Bow);
                        break;
                    case ItemSubClassWeapon.Gun:
                        AddStat(ItemSpecStat.Gun);
                        break;
                    case ItemSubClassWeapon.Mace:
                        AddStat(ItemSpecStat.OneHandedMace);
                        break;
                    case ItemSubClassWeapon.Mace2:
                        AddStat(ItemSpecStat.TwoHandedMace);
                        break;
                    case ItemSubClassWeapon.Polearm:
                        AddStat(ItemSpecStat.Polearm);
                        break;
                    case ItemSubClassWeapon.Sword:
                        AddStat(ItemSpecStat.OneHandedSword);
                        break;
                    case ItemSubClassWeapon.Sword2:
                        AddStat(ItemSpecStat.TwoHandedSword);
                        break;
                    case ItemSubClassWeapon.Warglaives:
                        AddStat(ItemSpecStat.Warglaives);
                        break;
                    case ItemSubClassWeapon.Staff:
                        AddStat(ItemSpecStat.Staff);
                        break;
                    case ItemSubClassWeapon.Fist:
                        AddStat(ItemSpecStat.FistWeapon);
                        break;
                    case ItemSubClassWeapon.Dagger:
                        AddStat(ItemSpecStat.Dagger);
                        break;
                    case ItemSubClassWeapon.Thrown:
                        AddStat(ItemSpecStat.Thrown);
                        break;
                    case ItemSubClassWeapon.Crossbow:
                        AddStat(ItemSpecStat.Crossbow);
                        break;
                    case ItemSubClassWeapon.Wand:
                        AddStat(ItemSpecStat.Wand);
                        break;
                    default:
                        break;
                }
            }
            else if (item.ClassID == ItemClass.Armor)
            {
                switch (item.SubclassID.Armor)
                {
                    case ItemSubClassArmor.Cloth:
                        if (sparse.InventoryType != InventoryType.Cloak)
                        {
                            ItemType = 1;
                            break;
                        }

                        ItemType = 0;
                        AddStat(ItemSpecStat.Cloak);
                        break;
                    case ItemSubClassArmor.Leather:
                        ItemType = 2;
                        break;
                    case ItemSubClassArmor.Mail:
                        ItemType = 3;
                        break;
                    case ItemSubClassArmor.Plate:
                        ItemType = 4;
                        break;
                    default:
                        if (item.SubclassID.Armor == ItemSubClassArmor.Shield)
                        {
                            ItemType = 6;
                            AddStat(ItemSpecStat.Shield);
                        }
                        else if (item.SubclassID.Armor > ItemSubClassArmor.Shield && item.SubclassID.Armor <= ItemSubClassArmor.Relic)
                        {
                            ItemType = 6;
                            AddStat(ItemSpecStat.Relic);
                        }
                        else
                            ItemType = 0;
                        break;
                }
            }
            else if (item.ClassID == ItemClass.Gem)
            {
                ItemType = 7;
            }
            else
            {
                ItemType = 0;
            }

            for (int i = 0; i < ItemConst.MaxStats; ++i)
                if (sparse.StatModifierBonusStat(i) != ItemModType.None)
                    AddModStat(sparse.StatModifierBonusStat(i));
        }

        void AddStat(ItemSpecStat statType)
        {
            if (ItemSpecStatCount >= ItemConst.MaxStats)
                return;

            for (int i = 0; i < ItemConst.MaxStats; ++i)
                if (ItemSpecStatTypes[i] == statType)
                    return;

            ItemSpecStatTypes[ItemSpecStatCount++] = statType;
        }

        void AddModStat(ItemModType itemStatType)
        {
            switch (itemStatType)
            {
                case ItemModType.Agility:
                    AddStat(ItemSpecStat.Agility);
                    break;
                case ItemModType.Strength:
                    AddStat(ItemSpecStat.Strength);
                    break;
                case ItemModType.Intellect:
                    AddStat(ItemSpecStat.Intellect);
                    break;
                case ItemModType.DodgeRating:
                    AddStat(ItemSpecStat.Dodge);
                    break;
                case ItemModType.ParryRating:
                    AddStat(ItemSpecStat.Parry);
                    break;
                case ItemModType.CritMeleeRating:
                case ItemModType.CritRangedRating:
                case ItemModType.CritSpellRating:
                case ItemModType.CritRating:
                    AddStat(ItemSpecStat.Crit);
                    break;
                case ItemModType.HasteRating:
                    AddStat(ItemSpecStat.Haste);
                    break;
                case ItemModType.HitRating:
                    AddStat(ItemSpecStat.Hit);
                    break;
                case ItemModType.ExtraArmor:
                    AddStat(ItemSpecStat.BonusArmor);
                    break;
                case ItemModType.AgiStrInt:
                    AddStat(ItemSpecStat.Agility);
                    AddStat(ItemSpecStat.Strength);
                    AddStat(ItemSpecStat.Intellect);
                    break;
                case ItemModType.AgiStr:
                    AddStat(ItemSpecStat.Agility);
                    AddStat(ItemSpecStat.Strength);
                    break;
                case ItemModType.AgiInt:
                    AddStat(ItemSpecStat.Agility);
                    AddStat(ItemSpecStat.Intellect);
                    break;
                case ItemModType.StrInt:
                    AddStat(ItemSpecStat.Strength);
                    AddStat(ItemSpecStat.Intellect);
                    break;
            }
        }

        public int ItemType;
        public ItemSpecStat[] ItemSpecStatTypes = new ItemSpecStat[ItemConst.MaxStats];
        public int ItemSpecStatCount;
    }

    public class SkillTiersEntry
    {
        public int Id;
        public int[] Value = new int[SkillConst.MaxSkillStep];

        public int GetValueForTierIndex(int tierIndex)
        {
            if (tierIndex >= SkillConst.MaxSkillStep)
                tierIndex = SkillConst.MaxSkillStep - 1;

            while (Value[tierIndex] == 0 && tierIndex > 0)
                --tierIndex;

            return Value[tierIndex];
        }
    }

    public class TerrainSwapInfo
    {
        public TerrainSwapInfo() { }
        public TerrainSwapInfo(int id)
        {
            Id = id;
        }

        public int Id;
        public List<int> UiMapPhaseIDs = new();
    }

    public class PhaseInfoStruct
    {
        public PhaseInfoStruct(int id)
        {
            Id = id;
        }

        public bool IsAllowedInArea(int areaId)
        {
            return Areas.Any(areaToCheck => Global.DB2Mgr.IsInArea(areaId, areaToCheck));
        }

        public int Id;
        public List<int> Areas = new();
    }

    public class PhaseAreaInfo
    {
        public PhaseAreaInfo(PhaseInfoStruct phaseInfo)
        {
            PhaseInfo = phaseInfo;
        }

        public PhaseInfoStruct PhaseInfo;
        public List<int> SubAreaExclusions = new();
        public List<Condition> Conditions = new();
    }

    public class SceneTemplate
    {
        public int SceneId;
        public SceneFlags PlaybackFlags;
        public int ScenePackageId;
        public bool Encrypted;
        public int ScriptId;
    }

    public class DefaultCreatureBaseStats : CreatureBaseStats
    {
        public DefaultCreatureBaseStats()
        {            
            BaseMana = 0;
            AttackPower = 0;
            RangedAttackPower = 0;
        }
    }

    public class GossipMenuItemsLocale
    {
        public StringArray OptionText = new((int)Locale.Total);
        public StringArray BoxText = new((int)Locale.Total);
    }

    public class PlayerChoiceLocale
    {
        public StringArray Question = new((int)Locale.Total);
        public Dictionary<int /*ResponseId*/, PlayerChoiceResponseLocale> Responses = new();
    }

    public class PlayerChoiceResponseLocale
    {
        public StringArray Answer = new((int)Locale.Total);
        public StringArray Header = new((int)Locale.Total);
        public StringArray SubHeader = new((int)Locale.Total);
        public StringArray ButtonTooltip = new((int)Locale.Total);
        public StringArray Description = new((int)Locale.Total);
        public StringArray Confirmation = new((int)Locale.Total);
    }

    public class PlayerChoiceResponseRewardItem
    {
        public PlayerChoiceResponseRewardItem() { }
        public PlayerChoiceResponseRewardItem(int id, List<int> bonusListIDs, int quantity)
        {
            Id = id;
            BonusListIDs = bonusListIDs;
            Quantity = quantity;
        }

        public int Id;
        public List<int> BonusListIDs = new();
        public int Quantity;
    }

    public class PlayerChoiceResponseRewardEntry
    {
        public PlayerChoiceResponseRewardEntry(int id, int quantity)
        {
            Id = id;
            Quantity = quantity;
        }

        public int Id;
        public int Quantity;
    }

    public class PlayerChoiceResponseReward
    {
        public int TitleId;
        public int PackageId;
        public SkillType SkillLineId;
        public int SkillPointCount;
        public int ArenaPointCount;
        public int HonorPointCount;
        public long Money;
        public int Xp;

        public List<PlayerChoiceResponseRewardItem> Items = new();
        public List<PlayerChoiceResponseRewardEntry> Currency = new();
        public List<PlayerChoiceResponseRewardEntry> Faction = new();
        public List<PlayerChoiceResponseRewardItem> ItemChoices = new();
    }

    public struct PlayerChoiceResponseMawPower
    {
        public int TypeArtFileID;
        public int? Rarity;
        public int? RarityColor;
        public int SpellID;
        public int MaxStacks;
    }

    public class PlayerChoiceResponse
    {
        public int ResponseId;
        public short ResponseIdentifier;
        public int ChoiceArtFileId;
        public int Flags;
        public int WidgetSetID;
        public int UiTextureAtlasElementID;
        public int SoundKitID;
        public byte GroupID;
        public int UiTextureKitID;
        public string Answer;
        public string Header;
        public string SubHeader;
        public string ButtonTooltip;
        public string Description;
        public string Confirmation;
        public PlayerChoiceResponseReward Reward;
        public int? RewardQuestID;
        public PlayerChoiceResponseMawPower? MawPower;
    }

    public class PlayerChoice
    {
        public int ChoiceId;
        public int UiTextureKitId;
        public int SoundKitId;
        public int CloseSoundKitId;
        public long Duration;
        public string Question;
        public string PendingChoiceText;
        public List<PlayerChoiceResponse> Responses = new();
        public bool HideWarboardHeader;
        public bool KeepOpenAfterChoice;

        public PlayerChoiceResponse GetResponse(int responseId)
        {
            return Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseId == responseId);
        }

        public PlayerChoiceResponse GetResponseByIdentifier(int responseIdentifier)
        {
            return Responses.Find(playerChoiceResponse => playerChoiceResponse.ResponseIdentifier == responseIdentifier);
        }
    }

    public class ClassAvailability
    {
        public Class ClassID;
        public Expansion ActiveExpansionLevel;
        public Expansion AccountExpansionLevel;
        public Expansion MinActiveExpansionLevel;
    }

    public class RaceClassAvailability
    {
        public Race RaceID;
        public List<ClassAvailability> Classes = new();
    }

    public class RaceUnlockRequirement
    {
        public Expansion Expansion;
        public int AchievementId;
    }

    public class QuestRelationResult : List<int>
    {
        bool _onlyActive;

        public QuestRelationResult() { }

        public QuestRelationResult(List<int> range, bool onlyActive) : base(range)
        {
            _onlyActive = onlyActive;
        }

        public bool HasQuest(int questId)
        {
            return Contains(questId) && (!_onlyActive || Quest.IsTakingQuestEnabled(questId));
        }
    }

    class ScriptNameContainer
    {
        Dictionary<string, Entry> NameToIndex = new();
        List<Entry> IndexToName = new();

        public ScriptNameContainer()
        {
            // We insert an empty placeholder here so we can use the
            // script id 0 as dummy for "no script found".
            int id = Insert("", false);

            Cypher.Assert(id <= 0);
        }

        public int Insert(string scriptName, bool isScriptNameBound)
        {
            Entry entry = new(NameToIndex.Count, isScriptNameBound, scriptName);
            var result = NameToIndex.TryAdd(scriptName, entry);
            if (result)
            {
                Cypher.Assert(NameToIndex.Count <= int.MaxValue);
                IndexToName.Add(entry);
            }

            return NameToIndex[scriptName].Id;
        }

        public int GetSize()
        {
            return IndexToName.Count;
        }

        public Entry Find(int index)
        {
            return index < IndexToName.Count ? IndexToName[index] : null;
        }

        public Entry Find(string name)
        {
            // assume "" is the first element
            if (name.IsEmpty())
                return null;

            return NameToIndex.LookupByKey(name);
        }

        public List<string> GetAllDBScriptNames()
        {
            List<string> scriptNames = new();

            foreach (var (name, entry) in NameToIndex)
                if (entry.IsScriptDatabaseBound)
                    scriptNames.Add(name);

            return scriptNames;
        }

        public class Entry
        {
            public int Id;
            public bool IsScriptDatabaseBound;
            public string Name;

            public Entry(int id, bool isScriptDatabaseBound, string name)
            {
                Id = id;
                IsScriptDatabaseBound = isScriptDatabaseBound;
                Name = name;
            }
        }
    }

    public class InstanceTemplate
    {
        public int Parent;
        public int ScriptId;
    }

    enum DefaultGraveyard
    {
        /// <summary>Crossroads</summary>
        HORDE_GRAVEYARD = 10,
        /// <summary>Westfall</summary>
        ALLIANCE_GRAVEYARD = 4
    }
}
