// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Cache;
using Game.DataStorage;
using Game.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Chat
{
    [CommandGroup("character")]
    class CharacterCommands
    {
        [Command("titles", RBACPermissions.CommandCharacterTitles, true)]
        static bool HandleCharacterTitlesCommand(CommandHandler handler, PlayerIdentifier player)
        {
            if (player == null)
                player = PlayerIdentifier.FromTargetOrSelf(handler);
            if (player == null || !player.IsConnected())
            {
                handler.SendSysMessage(CypherStrings.PlayerNotFound);
                return false;
            }

            Player target = player.GetConnectedPlayer();

            Locale loc = handler.GetSessionDbcLocale();
            string targetName = player.GetName();
            string knownStr = handler.GetCypherString(CypherStrings.Known);

            // Search in CharTitles.dbc
            foreach (var titleInfo in CliDB.CharTitlesStorage.Values)
            {
                if (target.HasTitle(titleInfo))
                {
                    string name = (target.GetNativeGender() == Gender.Male ? titleInfo.Name : titleInfo.Name1)[loc];
                    if (name.IsEmpty())
                        name = (target.GetNativeGender() == Gender.Male ? titleInfo.Name : titleInfo.Name1)[Global.WorldMgr.GetDefaultDbcLocale()];
                    if (name.IsEmpty())
                        continue;

                    string activeStr = "";
                    if (target.m_playerData.PlayerTitle == titleInfo.MaskID)
                        activeStr = handler.GetCypherString(CypherStrings.Active);

                    string titleName = string.Format(name.ConvertFormatSyntax(), targetName);

                    // send title in "id (idx:idx) - [namedlink locale]" format
                    if (handler.GetSession() != null)
                        handler.SendSysMessage(CypherStrings.TitleListChat, titleInfo.Id, titleInfo.MaskID, titleInfo.Id, titleName, loc, knownStr, activeStr);
                    else
                        handler.SendSysMessage(CypherStrings.TitleListConsole, titleInfo.Id, titleInfo.MaskID, name, loc, knownStr, activeStr);
                }
            }

            return true;
        }

        //rename characters
        [Command("rename", RBACPermissions.CommandCharacterRename, true)]
        static bool HandleCharacterRenameCommand(CommandHandler handler, PlayerIdentifier player, [OptionalArg] string newName)
        {
            if (player == null && !newName.IsEmpty())
                return false;

            if (player == null)
                player = PlayerIdentifier.FromTarget(handler);
            if (player == null)
                return false;
            
            // check online security
            if (handler.HasLowerSecurity(null, player.GetGUID()))
                return false;

            if (!newName.IsEmpty())
            {
                if (!ObjectManager.NormalizePlayerName(ref newName))
                {
                    handler.SendSysMessage(CypherStrings.BadValue);
                    return false;
                }

                if (ObjectManager.CheckPlayerName(newName, player.IsConnected() ? player.GetConnectedPlayer().GetSession().GetSessionDbcLocale() : Global.WorldMgr.GetDefaultDbcLocale(), true) != ResponseCodes.CharNameSuccess)
                {
                    handler.SendSysMessage(CypherStrings.BadValue);
                    return false;
                }

                WorldSession session = handler.GetSession();
                if (session != null)
                {
                    if (!session.HasPermission(RBACPermissions.SkipCheckCharacterCreationReservedname) && Global.ObjectMgr.IsReservedName(newName))
                    {
                        handler.SendSysMessage(CypherStrings.ReservedName);
                        return false;
                    }
                }

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHECK_NAME);
                stmt.SetString(0, newName);
                {
                    SQLResult result = DB.Characters.Query(stmt);
                
                    if (!result.IsEmpty())
                    {
                        handler.SendSysMessage(CypherStrings.RenamePlayerAlreadyExists, newName);
                        return false;
                    }
                }

                // Remove declined name from db
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_DECLINED_NAME);
                stmt.SetInt64(0, player.GetGUID().GetCounter());
                DB.Characters.Execute(stmt);

                Player target = player.GetConnectedPlayer();
                if (target != null)
                {
                    target.SetName(newName);
                    session = target.GetSession();
                    if (session != null)
                        session.KickPlayer("HandleCharacterRenameCommand GM Command renaming character");
                }
                else
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_NAME_BY_GUID);
                    stmt.SetString(0, newName);
                    stmt.SetInt64(1, player.GetGUID().GetCounter());
                    DB.Characters.Execute(stmt);
                }

                Global.CharacterCacheStorage.UpdateCharacterData(player.GetGUID(), newName);

                handler.SendSysMessage(CypherStrings.RenamePlayerWithNewName, player.GetName(), newName);

                if (session != null)
                {
                    Player sessionPlayer = session.GetPlayer();
                    if (sessionPlayer != null)
                    {
                        Log.outCommand(session.GetAccountId(),
                            $"GM {sessionPlayer.GetName()} (Account: {session.GetAccountId()}) forced rename {newName} " +
                            $"to player {sessionPlayer.GetName()} " +
                            $"(Account: {Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(sessionPlayer.GetGUID())})");
                    }
                }
                else
                {
                    Log.outCommand(0,
                        $"CONSOLE forced rename '{player.GetName()}' to '{newName}' ({player.GetGUID()})");
                }
            }
            else
            {
                Player target = player.GetConnectedPlayer();
                if (target != null)
                {
                    handler.SendSysMessage(CypherStrings.RenamePlayer, handler.GetNameLink(target));
                    target.SetAtLoginFlag(AtLoginFlags.Rename);
                }
                else
                {
                    // check offline security
                    if (handler.HasLowerSecurity(null, player.GetGUID()))
                        return false;

                    handler.SendSysMessage(CypherStrings.RenamePlayerGuid, handler.PlayerLink(player.GetName()), player.GetGUID().ToString());

                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ADD_AT_LOGIN_FLAG);
                    stmt.SetUInt16(0, (ushort)AtLoginFlags.Rename);
                    stmt.SetInt64(1, player.GetGUID().GetCounter());
                    DB.Characters.Execute(stmt);
                }
            }

            return true;
        }

        [Command("level", RBACPermissions.CommandCharacterLevel, true)]
        static bool HandleCharacterLevelCommand(CommandHandler handler, PlayerIdentifier player, short newlevel)
        {
            if (player == null)
                player = PlayerIdentifier.FromTargetOrSelf(handler);
            if (player == null)
                return false;

            int oldlevel = player.IsConnected() ? player.GetConnectedPlayer().GetLevel() : Global.CharacterCacheStorage.GetCharacterLevelByGuid(player.GetGUID());

            if (newlevel < 1)
                newlevel = 1;

            if (newlevel > SharedConst.StrongMaxLevel)
                newlevel = SharedConst.StrongMaxLevel;

            Player target = player.GetConnectedPlayer();
            if (target != null)
            {
                target.GiveLevel(newlevel);
                target.InitTalentForLevel();
                target.SetXP(0);

                if (handler.NeedReportToTarget(target))
                {
                    if (oldlevel == newlevel)
                        target.SendSysMessage(CypherStrings.YoursLevelProgressReset, handler.GetNameLink());
                    else if (oldlevel < newlevel)
                        target.SendSysMessage(CypherStrings.YoursLevelUp, handler.GetNameLink(), newlevel);
                    else                                                // if (oldlevel > newlevel)
                        target.SendSysMessage(CypherStrings.YoursLevelDown, handler.GetNameLink(), newlevel);
                }
            }
            else
            {
                // Update level and reset XP, everything else will be updated at login
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_LEVEL);
                stmt.SetUInt8(0, (byte)newlevel);
                stmt.SetInt64(1, player.GetGUID().GetCounter());
                DB.Characters.Execute(stmt);
            }

            if (handler.GetSession() == null || (handler.GetSession().GetPlayer() != player.GetConnectedPlayer()))      // including chr == NULL
                handler.SendSysMessage(CypherStrings.YouChangeLvl, handler.PlayerLink(player.GetName()), newlevel);

            return true;
        }

        [Command("customize", RBACPermissions.CommandCharacterCustomize, true)]
        static bool HandleCharacterCustomizeCommand(CommandHandler handler, PlayerIdentifier player)
        {
            if (player == null)
                player = PlayerIdentifier.FromTarget(handler);
            if (player == null)
                return false;

            Player target = player.GetConnectedPlayer();
            if (target != null)
            {
                handler.SendSysMessage(CypherStrings.CustomizePlayer, handler.GetNameLink(target));
                target.SetAtLoginFlag(AtLoginFlags.Customize);
            }
            else
            {
                handler.SendSysMessage(CypherStrings.CustomizePlayerGuid, handler.PlayerLink(player.GetName()), player.GetGUID().GetCounter());
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ADD_AT_LOGIN_FLAG);
                stmt.SetUInt16(0, (ushort)AtLoginFlags.Customize);
                stmt.SetInt64(1, player.GetGUID().GetCounter());
                DB.Characters.Execute(stmt);
            }

            return true;
        }

        [Command("changeaccount", RBACPermissions.CommandCharacterChangeaccount, true)]
        static bool HandleCharacterChangeAccountCommand(CommandHandler handler, PlayerIdentifier player, AccountIdentifier newAccount)
        {
            if (player == null)
                player = PlayerIdentifier.FromTarget(handler);
            if (player == null)
                return false;

            CharacterCacheEntry characterInfo = Global.CharacterCacheStorage.GetCharacterCacheByGuid(player.GetGUID());
            if (characterInfo == null)
            {
                handler.SendSysMessage(CypherStrings.PlayerNotFound);
                return false;
            }

            int oldAccountId = characterInfo.AccountId;

            // nothing to do :)
            if (newAccount.GetID() == oldAccountId)
                return true;

            int charCount = Global.AccountMgr.GetCharactersCount(newAccount.GetID());
            if (charCount != 0)
            {
                if (charCount >= WorldConfig.Values[WorldCfg.CharactersPerRealm].Int32)
                {
                    handler.SendSysMessage(CypherStrings.AccountCharacterListFull, newAccount.GetName(), newAccount.GetID());
                    return false;
                }
            }

            Player onlinePlayer = player.GetConnectedPlayer();
            if (onlinePlayer != null)
                onlinePlayer.GetSession().KickPlayer("HandleCharacterChangeAccountCommand GM Command transferring character to another account");

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ACCOUNT_BY_GUID);
            stmt.SetInt32(0, newAccount.GetID());
            stmt.SetInt64(1, player.GetGUID().GetCounter());
            DB.Characters.DirectExecute(stmt);

            Global.WorldMgr.UpdateRealmCharCount(oldAccountId);
            Global.WorldMgr.UpdateRealmCharCount(newAccount.GetID());

            Global.CharacterCacheStorage.UpdateCharacterAccountId(player.GetGUID(), newAccount.GetID());

            handler.SendSysMessage(CypherStrings.ChangeAccountSuccess, player.GetName(), newAccount.GetName());

            string logString = 
                $"changed ownership of player {player.GetName()} ({player.GetGUID()}) " +
                $"from account {oldAccountId} to account {newAccount.GetID()}";
            
            WorldSession session = handler.GetSession();
            if (session != null)
            {
                Player sessionPlayer = session.GetPlayer();
                if (sessionPlayer != null)
                    Log.outCommand(session.GetAccountId(), $"GM {sessionPlayer.GetName()} (Account: {session.GetAccountId()}) {logString}");
            }
            else
                Log.outCommand(0, $"{handler.GetCypherString(CypherStrings.Console)} {logString}");
            return true;
        }

        [Command("changefaction", RBACPermissions.CommandCharacterChangefaction, true)]
        static bool HandleCharacterChangeFactionCommand(CommandHandler handler, PlayerIdentifier player)
        {
            if (player == null)
                player = PlayerIdentifier.FromTarget(handler);
            if (player == null)
                return false;

            Player target = player.GetConnectedPlayer();
            if (target != null)
            {
                handler.SendSysMessage(CypherStrings.CustomizePlayer, handler.GetNameLink(target));
                target.SetAtLoginFlag(AtLoginFlags.ChangeFaction);
            }
            else
            {
                handler.SendSysMessage(CypherStrings.CustomizePlayerGuid, handler.PlayerLink(player.GetName()), player.GetGUID().GetCounter());
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ADD_AT_LOGIN_FLAG);
                stmt.SetUInt16(0, (ushort)AtLoginFlags.ChangeFaction);
                stmt.SetInt64(1, player.GetGUID().GetCounter());
                DB.Characters.Execute(stmt);
            }

            return true;
        }

        [Command("changerace", RBACPermissions.CommandCharacterChangerace, true)]
        static bool HandleCharacterChangeRaceCommand(CommandHandler handler, PlayerIdentifier player)
        {
            if (player == null)
                player = PlayerIdentifier.FromTarget(handler);
            if (player == null)
                return false;

            Player target = player.GetConnectedPlayer();
            if (target != null)
            {
                handler.SendSysMessage(CypherStrings.CustomizePlayer, handler.GetNameLink(target));
                target.SetAtLoginFlag(AtLoginFlags.ChangeRace);
            }
            else
            {
                handler.SendSysMessage(CypherStrings.CustomizePlayerGuid, handler.PlayerLink(player.GetName()), player.GetGUID().GetCounter());
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ADD_AT_LOGIN_FLAG);
                stmt.SetUInt16(0, (ushort)AtLoginFlags.ChangeRace);
                stmt.SetInt64(1, player.GetGUID().GetCounter());
                DB.Characters.Execute(stmt);
            }

            return true;
        }

        [Command("reputation", RBACPermissions.CommandCharacterReputation, true)]
        static bool HandleCharacterReputationCommand(CommandHandler handler, PlayerIdentifier player)
        {
            if (player == null)
                player = PlayerIdentifier.FromTargetOrSelf(handler);
            if (player == null || !player.IsConnected())
            {
                handler.SendSysMessage(CypherStrings.PlayerNotFound);
                return false;
            }

            Player target = player.GetConnectedPlayer();
            Locale loc = handler.GetSessionDbcLocale();

            var targetFSL = target.GetReputationMgr().GetStateList();
            foreach (var pair in targetFSL)
            {
                FactionState faction = pair.Value;
                FactionRecord factionEntry = CliDB.FactionStorage.LookupByKey(faction.Id);
                string factionName = factionEntry != null ? factionEntry.Name[loc] : "#Not found#";
                ReputationRank rank = target.GetReputationMgr().GetRank(factionEntry);
                string rankName = handler.GetCypherString(ReputationMgr.ReputationRankStrIndex[(int)rank]);
                StringBuilder ss = new();
                if (handler.GetSession() != null)
                    ss.AppendFormat($"{faction.Id} - |cffffffff|Hfaction:{faction.Id}|h[{factionName} {loc}]|h|r");
                else
                    ss.AppendFormat($"{faction.Id} - {factionName} {loc}");

                ss.AppendFormat($" {rankName} ({target.GetReputationMgr().GetReputation(factionEntry)})");

                if (faction.Flags.HasFlag(ReputationFlags.Visible))
                    ss.Append(handler.GetCypherString(CypherStrings.FactionVisible));
                if (faction.Flags.HasFlag(ReputationFlags.AtWar))
                    ss.Append(handler.GetCypherString(CypherStrings.FactionAtwar));
                if (faction.Flags.HasFlag(ReputationFlags.Peaceful))
                    ss.Append(handler.GetCypherString(CypherStrings.FactionPeaceForced));
                if (faction.Flags.HasFlag(ReputationFlags.Hidden))
                    ss.Append(handler.GetCypherString(CypherStrings.FactionHidden));
                if (faction.Flags.HasFlag(ReputationFlags.Header))
                    ss.Append(handler.GetCypherString(CypherStrings.FactionInvisibleForced));
                if (faction.Flags.HasFlag(ReputationFlags.Inactive))
                    ss.Append(handler.GetCypherString(CypherStrings.FactionInactive));

                handler.SendSysMessage(ss.ToString());
            }

            return true;
        }

        [Command("erase", RBACPermissions.CommandCharacterErase, true)]
        static bool HandleCharacterEraseCommand(CommandHandler handler, PlayerIdentifier player)
        {
            int accountId;

            Player target = player?.GetConnectedPlayer();
            if (target != null)
            {
                accountId = target.GetSession().GetAccountId();
                target.GetSession().KickPlayer("HandleCharacterEraseCommand GM Command deleting character");
            }
            else
                accountId = Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(player.GetGUID());

            string accountName;
            Global.AccountMgr.GetName(accountId, out accountName);

            Player.DeleteFromDB(player.GetGUID(), accountId, true, true);
            handler.SendSysMessage(CypherStrings.CharacterDeleted, player.GetName(), player.GetGUID().ToString(), accountName, accountId);

            return true;
        }

        [CommandGroup("deleted")]
        class DeletedCommands
        {
            [Command("delete", RBACPermissions.CommandCharacterDeletedDelete, true)]
            static bool HandleCharacterDeletedDeleteCommand(CommandHandler handler, string needle)
            {
                List<DeletedInfo> foundList = new();
                if (!GetDeletedCharacterInfoList(foundList, needle))
                    return false;

                if (foundList.Empty())
                {
                    handler.SendSysMessage(CypherStrings.CharacterDeletedListEmpty);
                    return false;
                }

                handler.SendSysMessage(CypherStrings.CharacterDeletedDelete);
                HandleCharacterDeletedListHelper(foundList, handler);

                // Call the appropriate function to delete them (current account for deleted characters is 0)
                foreach (var info in foundList)
                    Player.DeleteFromDB(info.guid, 0, false, true);

                return true;
            }

            [Command("list", RBACPermissions.CommandCharacterDeletedList, true)]
            static bool HandleCharacterDeletedListCommand(CommandHandler handler, [OptionalArg] string needle)
            {
                List<DeletedInfo> foundList = new();
                if (!GetDeletedCharacterInfoList(foundList, needle))
                    return false;

                // if no characters have been found, output a warning
                if (foundList.Empty())
                {
                    handler.SendSysMessage(CypherStrings.CharacterDeletedListEmpty);
                    return false;
                }

                HandleCharacterDeletedListHelper(foundList, handler);

                return true;
            }

            [Command("restore", RBACPermissions.CommandCharacterDeletedRestore, true)]
            static bool HandleCharacterDeletedRestoreCommand(CommandHandler handler, string needle, [OptionalArg] string newCharName, AccountIdentifier newAccount)
            {
                List<DeletedInfo> foundList = new();
                if (!GetDeletedCharacterInfoList(foundList, needle))
                    return false;

                if (foundList.Empty())
                {
                    handler.SendSysMessage(CypherStrings.CharacterDeletedListEmpty);
                    return false;
                }

                handler.SendSysMessage(CypherStrings.CharacterDeletedRestore);
                HandleCharacterDeletedListHelper(foundList, handler);

                if (newCharName.IsEmpty())
                {
                    // Drop not existed account cases
                    foreach (var info in foundList)
                        HandleCharacterDeletedRestoreHelper(info, handler);

                    return true;
                }

                if (foundList.Count == 1)
                {
                    DeletedInfo delInfo = foundList[0];

                    // update name
                    delInfo.name = newCharName;

                    // if new account provided update deleted info
                    if (newAccount != null)
                    {
                        delInfo.accountId = newAccount.GetID();
                        delInfo.accountName = newAccount.GetName();
                    }

                    HandleCharacterDeletedRestoreHelper(delInfo, handler);
                    return true;
                }
                
                handler.SendSysMessage(CypherStrings.CharacterDeletedErrRename);
                return false;
            }

            [Command("old", RBACPermissions.CommandCharacterDeletedOld, true)]
            static bool HandleCharacterDeletedOldCommand(CommandHandler handler, ushort? days)
            {
                if (!days.HasValue)
                    return Player.DeleteOldCharacters();
                else
                    return Player.DeleteOldCharacters(Time.SpanFromDays(days.Value)); 
            }

            static bool GetDeletedCharacterInfoList(List<DeletedInfo> foundList, string searchString)
            {
                PreparedStatement stmt;
                if (!searchString.IsEmpty())
                {
                    // search by GUID
                    if (searchString.IsNumber())
                    {
                        if (!ulong.TryParse(searchString, out ulong guid))
                            return false;

                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_DEL_INFO_BY_GUID);
                        stmt.SetUInt64(0, guid);                        
                    }
                    // search by name
                    else
                    {
                        if (!ObjectManager.NormalizePlayerName(ref searchString))
                            return false;

                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_DEL_INFO_BY_NAME);
                        stmt.SetString(0, searchString);
                    }
                }
                else
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_DEL_INFO);
                }

                SQLResult result = DB.Characters.Query(stmt);

                if (!result.IsEmpty())
                {
                    do
                    {
                        DeletedInfo info;

                        info.guid = ObjectGuid.Create(HighGuid.Player, result.Read<long>(0));
                        info.name = result.Read<string>(1);
                        info.accountId = result.Read<int>(2);

                        // account name will be empty for not existed account
                        Global.AccountMgr.GetName(info.accountId, out info.accountName);
                        info.deleteDate = (ServerTime)(UnixTime64)result.Read<long>(3);
                        foundList.Add(info);
                    }
                    while (result.NextRow());
                }

                return true;
            }

            static void HandleCharacterDeletedListHelper(List<DeletedInfo> foundList, CommandHandler handler)
            {
                if (handler.GetSession() == null)
                {
                    handler.SendSysMessage(CypherStrings.CharacterDeletedListBar);
                    handler.SendSysMessage(CypherStrings.CharacterDeletedListHeader);
                    handler.SendSysMessage(CypherStrings.CharacterDeletedListBar);
                }

                foreach (var info in foundList)
                {
                    string dateStr = info.deleteDate.ToShortDateString();

                    if (handler.GetSession() == null)
                    {
                        handler.SendSysMessage(CypherStrings.CharacterDeletedListLineConsole,
                            info.guid.ToString(), info.name, info.accountName.IsEmpty() ? "<Not existed>" : info.accountName,
                            info.accountId, dateStr);
                    }
                    else
                    {
                        handler.SendSysMessage(CypherStrings.CharacterDeletedListLineChat,
                            info.guid.ToString(), info.name, info.accountName.IsEmpty() ? "<Not existed>" : info.accountName,
                            info.accountId, dateStr);
                    }
                }

                if (handler.GetSession() == null)
                    handler.SendSysMessage(CypherStrings.CharacterDeletedListBar);
            }

            static void HandleCharacterDeletedRestoreHelper(DeletedInfo delInfo, CommandHandler handler)
            {
                if (delInfo.accountName.IsEmpty())                    // account not exist
                {
                    handler.SendSysMessage(CypherStrings.CharacterDeletedSkipAccount, delInfo.name, delInfo.guid.ToString(), delInfo.accountId);
                    return;
                }

                // check character count
                int charcount = Global.AccountMgr.GetCharactersCount(delInfo.accountId);
                if (charcount >= WorldConfig.Values[WorldCfg.CharactersPerRealm].Int32)
                {
                    handler.SendSysMessage(CypherStrings.CharacterDeletedSkipFull, delInfo.name, delInfo.guid.ToString(), delInfo.accountId);
                    return;
                }

                if (!Global.CharacterCacheStorage.GetCharacterGuidByName(delInfo.name).IsEmpty())
                {
                    handler.SendSysMessage(CypherStrings.CharacterDeletedSkipName, delInfo.name, delInfo.guid.ToString(), delInfo.accountId);
                    return;
                }

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_RESTORE_DELETE_INFO);
                stmt.SetString(0, delInfo.name);
                stmt.SetInt32(1, delInfo.accountId);
                stmt.SetInt64(2, delInfo.guid.GetCounter());
                DB.Characters.Execute(stmt);

                Global.CharacterCacheStorage.UpdateCharacterInfoDeleted(delInfo.guid, false, delInfo.name);
            }

            struct DeletedInfo
            {
                public ObjectGuid guid; // the GUID from the character
                public string name; // the character name
                public int accountId; // the account id
                public string accountName; // the account name
                public ServerTime deleteDate; // the date at which the character has been deleted
            }
        }

        [CommandNonGroup("levelup", RBACPermissions.CommandLevelup)]
        static bool HandleLevelUpCommand(CommandHandler handler, [OptionalArg]PlayerIdentifier player, short level)
        {
            if (player == null)
                player = PlayerIdentifier.FromTargetOrSelf(handler);
            if (player == null)
                return false;

            int oldlevel = player.IsConnected() ? player.GetConnectedPlayer().GetLevel() : Global.CharacterCacheStorage.GetCharacterLevelByGuid(player.GetGUID());
            int newlevel = oldlevel + level;

            if (newlevel < 1)
                newlevel = 1;

            if (newlevel > SharedConst.StrongMaxLevel)                         // hardcoded maximum level
                newlevel = SharedConst.StrongMaxLevel;

            Player target = player.GetConnectedPlayer();
            if (target != null)
            {
                target.GiveLevel(newlevel);
                target.InitTalentForLevel();
                target.SetXP(0);

                if (handler.NeedReportToTarget(player.GetConnectedPlayer()))
                {
                    if (oldlevel == newlevel)
                        player.GetConnectedPlayer().SendSysMessage(CypherStrings.YoursLevelProgressReset, handler.GetNameLink());
                    else if (oldlevel < newlevel)
                        player.GetConnectedPlayer().SendSysMessage(CypherStrings.YoursLevelUp, handler.GetNameLink(), newlevel);
                    else                                                // if (oldlevel > newlevel)
                        player.GetConnectedPlayer().SendSysMessage(CypherStrings.YoursLevelDown, handler.GetNameLink(), newlevel);
                }
            }
            else
            {
                // Update level and reset XP, everything else will be updated at login
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_LEVEL);
                stmt.SetInt32(0, newlevel);
                stmt.SetInt64(1, player.GetGUID().GetCounter());
                DB.Characters.Execute(stmt);
            }
            
            if (handler.GetSession() == null || handler.GetSession().GetPlayer() != target)      // including chr == NULL
                handler.SendSysMessage(CypherStrings.YouChangeLvl, handler.PlayerLink(player.GetName()), newlevel);

            return true;
        }
    }

    [CommandGroup("pdump")]
    class PdumpCommand
    {
        [Command("copy", RBACPermissions.CommandPdumpCopy, true)]
        static bool HandlePDumpCopyCommand(CommandHandler handler, PlayerIdentifier player, AccountIdentifier account, [OptionalArg] string characterName, ulong? characterGUID)
        {
            /*
            std::string name;
            if (!ValidatePDumpTarget(handler, name, characterName, characterGUID))
                return false;

            std::string dump;
            switch (PlayerDumpWriter().WriteDumpToString(dump, player.GetGUID().GetCounter()))
            {
                case DUMP_SUCCESS:
                    break;
                case DUMP_CHARACTER_DELETED:
                    handler->PSendSysMessage(LANG_COMMAND_EXPORT_DELETED_CHAR);
                    handler->SetSentErrorMessage(true);
                    return false;
                case DUMP_FILE_OPEN_ERROR: // this error code should not happen
                default:
                    handler->PSendSysMessage(LANG_COMMAND_EXPORT_FAILED);
                    handler->SetSentErrorMessage(true);
                    return false;
            }

            switch (PlayerDumpReader().LoadDumpFromString(dump, account, name, characterGUID.value_or(0)))
            {
                case DUMP_SUCCESS:
                    break;
                case DUMP_TOO_MANY_CHARS:
                    handler->PSendSysMessage(LANG_ACCOUNT_CHARACTER_LIST_FULL, account.GetName().c_str(), account.GetID());
                    handler->SetSentErrorMessage(true);
                    return false;
                case DUMP_FILE_OPEN_ERROR: // this error code should not happen
                case DUMP_FILE_BROKEN: // this error code should not happen
                default:
                    handler->PSendSysMessage(LANG_COMMAND_IMPORT_FAILED);
                    handler->SetSentErrorMessage(true);
                    return false;
            }

            // ToDo: use a new trinity_string for this commands
            handler->PSendSysMessage(LANG_COMMAND_IMPORT_SUCCESS);
            */
            return true;
        }

        [Command("load", RBACPermissions.CommandPdumpLoad, true)]
        static bool HandlePDumpLoadCommand(CommandHandler handler, string fileName, AccountIdentifier account, [OptionalArg] string characterName, ulong? characterGuid)
        {
            /*
            if (!AccountMgr.normalizeString(accountName))
            {
                handler.SendSysMessage(LANG_ACCOUNT_NOT_EXIST, accountName);
                handler.SetSentErrorMessage(true);
                return false;
            }

            public uint accountId = AccountMgr.GetId(accountName);
            if (!accountId)
            {
                accountId = atoi(accountStr);                             // use original string
                if (!accountId)
                {
                    handler.SendSysMessage(LANG_ACCOUNT_NOT_EXIST, accountName);

                    return false;
                }
            }

            if (!AccountMgr.GetName(accountId, accountName))
            {
                handler.SendSysMessage(LANG_ACCOUNT_NOT_EXIST, accountName);
                handler.SetSentErrorMessage(true);
                return false;
            }

            string name;
            if (nameStr)
            {
                name = nameStr;
                // normalize the name if specified and check if it exists
                if (!ObjectManager.NormalizePlayerName(name))
                {
                    handler.SendSysMessage(LANG_INVALID_CHARACTER_NAME);

                    return false;
                }

                if (ObjectMgr.CheckPlayerName(name, true) != CHAR_NAME_SUCCESS)
                {
                    handler.SendSysMessage(LANG_INVALID_CHARACTER_NAME);

                    return false;
                }

                guidStr = strtok(NULL, " ");
            }

            public uint guid = 0;

            if (guidStr)
            {
                guid = uint32(atoi(guidStr));
                if (!guid)
                {
                    handler.SendSysMessage(LANG_INVALID_CHARACTER_GUID);

                    return false;
                }

                if (Global.ObjectMgr.GetPlayerAccountIdByGUID(guid))
                {
                    handler.SendSysMessage(LANG_CHARACTER_GUID_IN_USE, guid);

                    return false;
                }
            }

            switch (PlayerDumpReader().LoadDump(fileStr, accountId, name, guid))
            {
                case DUMP_SUCCESS:
                    handler.SendSysMessage(LANG_COMMAND_IMPORT_SUCCESS);
                    break;
                case DUMP_FILE_OPEN_ERROR:
                    handler.SendSysMessage(LANG_FILE_OPEN_FAIL, fileStr);

                    return false;
                case DUMP_FILE_BROKEN:
                    handler.SendSysMessage(LANG_DUMP_BROKEN, fileStr);

                    return false;
                case DUMP_TOO_MANY_CHARS:
                    handler.SendSysMessage(LANG_ACCOUNT_CHARACTER_LIST_FULL, accountName, accountId);

                    return false;
                default:
                    handler.SendSysMessage(LANG_COMMAND_IMPORT_FAILED);

                    return false;
            }
            */
            return true;
        }

        [Command("write", RBACPermissions.CommandPdumpWrite, true)]
        static bool HandlePDumpWriteCommand(CommandHandler handler, string fileName, string playerName)
        {
            /*
            switch (PlayerDumpWriter().WriteDump(fileName, player.GetGUID().GetCounter()))
            {
                case DUMP_SUCCESS:
                    handler.SendSysMessage(LANG_COMMAND_EXPORT_SUCCESS);
                    break;
                case DUMP_FILE_OPEN_ERROR:
                    handler.SendSysMessage(LANG_FILE_OPEN_FAIL, fileName);

                    return false;
                case DUMP_CHARACTER_DELETED:
                    handler.SendSysMessage(LANG_COMMAND_EXPORT_DELETED_CHAR);

                    return false;
                default:
                    handler.SendSysMessage(LANG_COMMAND_EXPORT_FAILED);

                    return false;
            }
            */
            return true;
        }
    }
}
