// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using System;

namespace Game.Chat.Commands
{
    [CommandGroup("bnetaccount")]
    class BNetAccountCommands
    {
        [Command("create", RBACPermissions.CommandBnetAccountCreate, true)]
        static bool HandleAccountCreateCommand(CommandHandler handler, string accountName, string password, bool? createGameAccount)
        {
            if (accountName.IsEmpty() || !accountName.Contains('@'))
            {
                handler.SendSysMessage(CypherStrings.AccountInvalidBnetName);
                return false;
            }

            string gameAccountName;
            switch (Global.BNetAccountMgr.CreateBattlenetAccount(accountName, password, createGameAccount.GetValueOrDefault(true), out gameAccountName))
            {
                case AccountOpResult.Ok:
                    if (createGameAccount.HasValue && createGameAccount.Value)
                        handler.SendSysMessage(CypherStrings.AccountCreatedBnetWithGame, accountName, gameAccountName);
                    else
                        handler.SendSysMessage(CypherStrings.AccountCreated, accountName);

                    if (handler.GetSession() != null)
                    {
                        Log.outInfo(LogFilter.Player, 
                            $"Account: {handler.GetSession().GetAccountId()} (IP: {handler.GetSession().GetRemoteAddress()}) " +
                            $"Character:[{handler.GetSession().GetPlayer().GetName()}] ({handler.GetSession().GetPlayer().GetGUID()}) " +
                            $"created Battle.net account " +
                            $"{accountName}{(createGameAccount.Value ? " with game account " : "")}{(createGameAccount.Value ? gameAccountName : "")}");
                    }
                    break;
                case AccountOpResult.NameTooLong:
                    handler.SendSysMessage(CypherStrings.AccountNameTooLong);
                    return false;
                case AccountOpResult.PassTooLong:
                    handler.SendSysMessage(CypherStrings.AccountPassTooLong);
                    return false;
                case AccountOpResult.NameAlreadyExist:
                    handler.SendSysMessage(CypherStrings.AccountAlreadyExist);
                    return false;
                default:
                    break;
            }

            return true;
        }

        [Command("gameaccountcreate", RBACPermissions.CommandBnetAccountCreateGame, true)]
        static bool HandleGameAccountCreateCommand(CommandHandler handler, string bnetAccountName)
        {
            int accountId = Global.BNetAccountMgr.GetId(bnetAccountName);
            if (accountId == 0)
            {
                handler.SendSysMessage(CypherStrings.AccountNotExist, bnetAccountName);
                return false;
            }

            byte index = (byte)(Global.BNetAccountMgr.GetMaxIndex(accountId) + 1);
            string accountName = accountId.ToString() + '#' + index;

            // Generate random hex string for password, these accounts must not be logged on with GRUNT
            byte[] randPassword = Array.Empty<byte>().GenerateRandomKey(8);
            switch (Global.AccountMgr.CreateAccount(accountName, randPassword.ToHexString(), bnetAccountName, accountId, index))
            {
                case AccountOpResult.Ok:
                    handler.SendSysMessage(CypherStrings.AccountCreated, accountName);
                    if (handler.GetSession() != null)
                    {
                        Log.outInfo(LogFilter.Player, 
                            $"Account: {handler.GetSession().GetAccountId()} (IP: {handler.GetSession().GetRemoteAddress()}) " +
                            $"Character:[{handler.GetSession().GetPlayer().GetName()}] ({handler.GetSession().GetPlayer().GetGUID()}) " +
                            $"created Account {accountName} (Email: '{bnetAccountName}')");
                    }
                    break;
                case AccountOpResult.NameTooLong:
                    handler.SendSysMessage(CypherStrings.AccountNameTooLong);
                    return false;
                case AccountOpResult.PassTooLong:
                    handler.SendSysMessage(CypherStrings.AccountPassTooLong);
                    return false;
                case AccountOpResult.NameAlreadyExist:
                    handler.SendSysMessage(CypherStrings.AccountAlreadyExist);
                    return false;
                case AccountOpResult.DBInternalError:
                    handler.SendSysMessage(CypherStrings.AccountNotCreatedSqlError, accountName);
                    return false;
                default:
                    handler.SendSysMessage(CypherStrings.AccountNotCreated, accountName);
                    return false;
            }

            return true;
        }

        [Command("link", RBACPermissions.CommandBnetAccountLink, true)]
        static bool HandleAccountLinkCommand(CommandHandler handler, string bnetAccountName, string gameAccountName)
        {
            switch (Global.BNetAccountMgr.LinkWithGameAccount(bnetAccountName, gameAccountName))
            {
                case AccountOpResult.Ok:
                    handler.SendSysMessage(CypherStrings.AccountBnetLinked, bnetAccountName, gameAccountName);
                    break;
                case AccountOpResult.NameNotExist:
                    handler.SendSysMessage(CypherStrings.AccountOrBnetDoesNotExist, bnetAccountName, gameAccountName);
                    break;
                case AccountOpResult.BadLink:
                    handler.SendSysMessage( CypherStrings.AccountAlreadyLinked, gameAccountName);
                    break;
                default:
                    break;
            }

            return true;
        }

        [Command("listgameaccounts", RBACPermissions.CommandBnetAccountListGameAccounts, true)]
        static bool HandleListGameAccountsCommand(CommandHandler handler, string battlenetAccountName)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_BNET_GAME_ACCOUNT_LIST);
            stmt.SetString(0, battlenetAccountName);

            SQLResult accountList = DB.Login.Query(stmt);
            if (!accountList.IsEmpty())
            {
                var formatDisplayName = new Func<string, string>(name =>
                {
                    int index = name.IndexOf('#');
                    if (index > 0)
                        return "WoW" + name[++index..];
                    else
                        return name;
                });

                handler.SendSysMessage("----------------------------------------------------");
                handler.SendSysMessage(CypherStrings.AccountBnetListHeader);
                handler.SendSysMessage("----------------------------------------------------");
                do
                {
                    handler.SendSysMessage($"| {accountList.Read<uint>(0),10} | {accountList.Read<string>(1),16} | {formatDisplayName(accountList.Read<string>(1)),16} |");
                } while (accountList.NextRow());
                handler.SendSysMessage("----------------------------------------------------");
            }
            else
                handler.SendSysMessage(CypherStrings.AccountBnetListNoAccounts, battlenetAccountName);

            return true;
        }

        [Command("password", RBACPermissions.CommandBnetAccountPassword, true)]
        static bool HandleAccountPasswordCommand(CommandHandler handler, string oldPassword, string newPassword, string passwordConfirmation)
        {
            // We compare the old, saved password to the entered old password - no Chance for the unauthorized.
            if (!Global.BNetAccountMgr.CheckPassword(handler.GetSession().GetBattlenetAccountId(), oldPassword))
            {
                handler.SendSysMessage(CypherStrings.CommandWrongoldpassword);

                Log.outInfo(LogFilter.Player, 
                    $"Battle.net account: {handler.GetSession().GetBattlenetAccountId()} (IP: {handler.GetSession().GetRemoteAddress()}) " +
                    $"Character:[{handler.GetSession().GetPlayer().GetName()}] ({handler.GetSession().GetPlayer().GetGUID()}) " +
                    $"Tried to change password, but the provided old password is wrong.");
                return false;
            }

            // Making sure that newly entered password is correctly entered.
            if (newPassword != passwordConfirmation)
            {
                handler.SendSysMessage(CypherStrings.NewPasswordsNotMatch);
                return false;
            }

            // Changes password and prints result.
            AccountOpResult result = Global.BNetAccountMgr.ChangePassword(handler.GetSession().GetBattlenetAccountId(), newPassword);
            switch (result)
            {
                case AccountOpResult.Ok:
                    handler.SendSysMessage(CypherStrings.CommandPassword);
                    Log.outInfo(LogFilter.Player, 
                        $"Battle.net account: {handler.GetSession().GetBattlenetAccountId()} (IP: {handler.GetSession().GetRemoteAddress()}) " +
                        $"Character:[{handler.GetSession().GetPlayer().GetName()}] ({handler.GetSession().GetPlayer().GetGUID()}) Changed Password.");
                    break;
                case AccountOpResult.PassTooLong:
                    handler.SendSysMessage(CypherStrings.PasswordTooLong);
                    return false;
                default:
                    handler.SendSysMessage(CypherStrings.CommandNotchangepassword);
                    return false;
            }

            return true;
        }

        [Command("unlink", RBACPermissions.CommandBnetAccountUnlink, true)]
        static bool HandleAccountUnlinkCommand(CommandHandler handler, string gameAccountName)
        {
            switch (Global.BNetAccountMgr.UnlinkGameAccount(gameAccountName))
            {
                case AccountOpResult.Ok:
                    handler.SendSysMessage(CypherStrings.AccountBnetUnlinked, gameAccountName);
                    break;
                case AccountOpResult.NameNotExist:
                    handler.SendSysMessage(CypherStrings.AccountNotExist, gameAccountName);
                    break;
                case AccountOpResult.BadLink:
                    handler.SendSysMessage(CypherStrings.AccountBnetNotLinked, gameAccountName);
                    break;
                default:
                    break;
            }

            return true;
        }

        [CommandGroup("lock")]
        class AccountLockCommands
        {
            [Command("country", RBACPermissions.CommandBnetAccountLockCountry, true)]
            static bool HandleAccountLockCountryCommand(CommandHandler handler, bool state)
            {
                /*if (state)
                {
                    if (IpLocationRecord const* location = sIPLocation->GetLocationRecord(handler->GetSession()->GetRemoteAddress()))
            {
                        LoginDatabasePreparedStatement* stmt = LoginDatabase.GetPreparedStatement(LOGIN_UPD_BNET_ACCOUNT_LOCK_CONTRY);
                        stmt->setString(0, location->CountryCode);
                        stmt->setUInt32(1, handler->GetSession()->GetBattlenetAccountId());
                        LoginDatabase.Execute(stmt);
                        handler->PSendSysMessage(LANG_COMMAND_ACCLOCKLOCKED);
                    }
            else
                    {
                        handler->PSendSysMessage("IP2Location] No information");
                        TC_LOG_DEBUG("server.bnetserver", "IP2Location] No information");
                    }
                }
                else
                {
                    LoginDatabasePreparedStatement* stmt = LoginDatabase.GetPreparedStatement(LOGIN_UPD_BNET_ACCOUNT_LOCK_CONTRY);
                    stmt->setString(0, "00");
                    stmt->setUInt32(1, handler->GetSession()->GetBattlenetAccountId());
                    LoginDatabase.Execute(stmt);
                    handler->PSendSysMessage(LANG_COMMAND_ACCLOCKUNLOCKED);
                }
                */
                return true;
            }

            [Command("ip", RBACPermissions.CommandBnetAccountLockIp, true)]
            static bool HandleAccountLockIpCommand(CommandHandler handler, bool state)
            {
                PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_BNET_ACCOUNT_LOCK);

                if (state)
                {
                    stmt.SetBool(0, true);                                     // locked
                    handler.SendSysMessage(CypherStrings.CommandAcclocklocked);
                }
                else
                {
                    stmt.SetBool(0, false);                                    // unlocked
                    handler.SendSysMessage(CypherStrings.CommandAcclockunlocked);
                }

                stmt.SetInt32(1, handler.GetSession().GetBattlenetAccountId());

                DB.Login.Execute(stmt);
                return true;
            }
        }

        [CommandGroup("set")]
        class AccountSetCommands
        {
            [Command("password", RBACPermissions.CommandBnetAccountSetPassword, true)]
            static bool HandleAccountSetPasswordCommand(CommandHandler handler, string accountName, string password, string passwordConfirmation)
            {
                int targetAccountId = Global.BNetAccountMgr.GetId(accountName);
                if (targetAccountId == 0)
                {
                    handler.SendSysMessage(CypherStrings.AccountNotExist, accountName);
                    return false;
                }

                if (password != passwordConfirmation)
                {
                    handler.SendSysMessage(CypherStrings.NewPasswordsNotMatch);
                    return false;
                }

                AccountOpResult result = Global.BNetAccountMgr.ChangePassword(targetAccountId, password);
                switch (result)
                {
                    case AccountOpResult.Ok:
                        handler.SendSysMessage(CypherStrings.CommandPassword);
                        break;
                    case AccountOpResult.NameNotExist:
                        handler.SendSysMessage(CypherStrings.AccountNotExist, accountName);
                        return false;
                    case AccountOpResult.PassTooLong:
                        handler.SendSysMessage(CypherStrings.PasswordTooLong);
                        return false;
                    default:
                        break;
                }
                return true;
            }
        }
    }
}
