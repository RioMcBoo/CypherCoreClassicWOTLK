// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Cryptography;
using Framework.Database;
using Game.Accounts;
using Game.Entities;
using System;
using System.Collections.Generic;

namespace Game
{
    public sealed class AccountManager : Singleton<AccountManager>
    {
        const int MaxAccountLength = 16;
        const int MaxEmailLength = 64;

        readonly Dictionary<RBACPermissions, RBACPermission> _permissions = new();
        readonly MultiMap<byte, RBACPermissions> _defaultPermissions = new();

        AccountManager() { }

        public AccountOpResult CreateAccount(string username, string password, string email = "", int bnetAccountId = 0, byte bnetIndex = 0)
        {
            if (username.Length > MaxAccountLength)
                return AccountOpResult.NameTooLong;

            if (password.Length > MaxAccountLength)
                return AccountOpResult.PassTooLong;

            if (GetId(username) != 0)
                return AccountOpResult.NameAlreadyExist;

            (byte[] salt, byte[] verifier) = SRP6.MakeAccountRegistrationData<GruntSRP6>(username, password);

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.INS_ACCOUNT);
            stmt.SetString(0, username);
            stmt.SetBytes(1, salt);
            stmt.SetBytes(2, verifier);
            stmt.SetString(3, email);
            stmt.SetString(4, email);
            if (bnetAccountId != 0 && bnetIndex != 0)
            {
                stmt.SetInt32(5, bnetAccountId);
                stmt.SetUInt8(6, bnetIndex);
            }
            else
            {
                stmt.SetNull(5);
                stmt.SetNull(6);
            }
            DB.Login.DirectExecute(stmt); // Enforce saving, otherwise AddGroup can fail

            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.INS_REALM_CHARACTERS_INIT);
            DB.Login.Execute(stmt);

            return AccountOpResult.Ok;
        }

        public AccountOpResult DeleteAccount(int accountId)
        {
            // Check if accounts exists
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_BY_ID);
            stmt.SetInt32(0, accountId);
            {
                SQLResult result = DB.Login.Query(stmt);
            
                if (result.IsEmpty())
                    return AccountOpResult.NameNotExist;
            }

            // Obtain accounts characters
            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHARS_BY_ACCOUNT_ID);
            stmt.SetInt32(0, accountId);

            {
                SQLResult result = DB.Characters.Query(stmt);
            
                if (!result.IsEmpty())
                {
                    do
                    {
                        ObjectGuid guid = ObjectGuid.Create(HighGuid.Player, result.Read<long>(0));

                    // Kick if player is online
                    Player player = Global.ObjAccessor.FindPlayer(guid);
                    if (player != null)
                    {
                        WorldSession s = player.GetSession();
                        s.KickPlayer("AccountMgr::DeleteAccount Deleting the account");                            // mark session to remove at next session list update
                        s.LogoutPlayer(false);                     // logout player without waiting next session list update
                    }

                        Player.DeleteFromDB(guid, accountId, false);       // no need to update realm characters
                    } while (result.NextRow());
                }
            }

            // table realm specific but common for all characters of account for realm
            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_TUTORIALS);
            stmt.SetInt32(0, accountId);
            DB.Characters.Execute(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ACCOUNT_DATA);
            stmt.SetInt32(0, accountId);
            DB.Characters.Execute(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_BAN);
            stmt.SetInt32(0, accountId);
            DB.Characters.Execute(stmt);

            SQLTransaction trans = new();

            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_ACCOUNT);
            stmt.SetInt32(0, accountId);
            trans.Append(stmt);

            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_ACCOUNT_ACCESS);
            stmt.SetInt32(0, accountId);
            trans.Append(stmt);

            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_REALM_CHARACTERS);
            stmt.SetInt32(0, accountId);
            trans.Append(stmt);

            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_ACCOUNT_BANNED);
            stmt.SetInt32(0, accountId);
            trans.Append(stmt);

            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_ACCOUNT_MUTED);
            stmt.SetInt32(0, accountId);
            trans.Append(stmt);

            DB.Login.CommitTransaction(trans);

            return AccountOpResult.Ok;
        }

        public AccountOpResult ChangeUsername(int accountId, string newUsername, string newPassword)
        {
            // Check if accounts exists
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_BY_ID);
            stmt.SetInt32(0, accountId);
            SQLResult result = DB.Login.Query(stmt);
            if (result.IsEmpty())
                return AccountOpResult.NameNotExist;

            if (newUsername.Length > MaxAccountLength)
                return AccountOpResult.NameTooLong;

            if (newPassword.Length > MaxAccountLength)
                return AccountOpResult.PassTooLong;

            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_USERNAME);
            stmt.SetString(0, newUsername);
            stmt.SetInt32(1, accountId);
            DB.Login.Execute(stmt);

            (byte[] salt, byte[] verifier) = SRP6.MakeAccountRegistrationData<GruntSRP6>(newUsername, newPassword);
            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_LOGON);
            stmt.SetBytes(0, salt);
            stmt.SetBytes(1, verifier);
            stmt.SetInt32(2, accountId);
            DB.Login.Execute(stmt);

            return AccountOpResult.Ok;
        }

        public AccountOpResult ChangePassword(int accountId, string newPassword)
        {
            string username;

            if (!GetName(accountId, out username))
            {
                Global.ScriptMgr.OnFailedPasswordChange(accountId);
                return AccountOpResult.NameNotExist;                          // account doesn't exist
            }

            if (newPassword.Length > MaxAccountLength)
            {
                Global.ScriptMgr.OnFailedPasswordChange(accountId);
                return AccountOpResult.PassTooLong;
            }

            (byte[] salt, byte[] verifier) = SRP6.MakeAccountRegistrationData<GruntSRP6>(username, newPassword);

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_LOGON);
            stmt.SetBytes(0, salt);
            stmt.SetBytes(1, verifier);
            stmt.SetInt32(2, accountId);
            DB.Login.Execute(stmt);

            Global.ScriptMgr.OnPasswordChange(accountId);
            return AccountOpResult.Ok;
        }

        public AccountOpResult ChangeEmail(int accountId, string newEmail)
        {
            if (!GetName(accountId, out _))
            {
                Global.ScriptMgr.OnFailedEmailChange(accountId);
                return AccountOpResult.NameNotExist;                          // account doesn't exist
            }

            if (newEmail.Length > MaxEmailLength)
            {
                Global.ScriptMgr.OnFailedEmailChange(accountId);
                return AccountOpResult.EmailTooLong;
            }

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_EMAIL);
            stmt.SetString(0, newEmail);
            stmt.SetInt32(1, accountId);
            DB.Login.Execute(stmt);

            Global.ScriptMgr.OnEmailChange(accountId);
            return AccountOpResult.Ok;
        }

        public AccountOpResult ChangeRegEmail(int accountId, string newEmail)
        {
            if (!GetName(accountId, out _))
            {
                Global.ScriptMgr.OnFailedEmailChange(accountId);
                return AccountOpResult.NameNotExist;                          // account doesn't exist
            }

            if (newEmail.Length > MaxEmailLength)
            {
                Global.ScriptMgr.OnFailedEmailChange(accountId);
                return AccountOpResult.EmailTooLong;
            }

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_REG_EMAIL);
            stmt.SetString(0, newEmail);
            stmt.SetInt32(1, accountId);
            DB.Login.Execute(stmt);

            Global.ScriptMgr.OnEmailChange(accountId);
            return AccountOpResult.Ok;
        }

        public int GetId(string username)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.GET_ACCOUNT_ID_BY_USERNAME);
            stmt.SetString(0, username);
            SQLResult result = DB.Login.Query(stmt);
            return !result.IsEmpty() ? result.Read<int>(0) : 0;
        }

        public AccountTypes GetSecurity(int accountId, int realmId)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.GET_GMLEVEL_BY_REALMID);
            stmt.SetInt32(0, accountId);
            stmt.SetInt32(1, realmId);
            SQLResult result = DB.Login.Query(stmt);
            return !result.IsEmpty() ? (AccountTypes)result.Read<int>(0) : AccountTypes.Player;
        }

        public QueryCallback GetSecurityAsync(int accountId, int realmId, Action<int> callback)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.GET_GMLEVEL_BY_REALMID);
            stmt.SetInt32(0, accountId);
            stmt.SetInt32(1, realmId);
            return DB.Login.AsyncQuery(stmt).WithCallback(result =>
            {
                callback(!result.IsEmpty() ? result.Read<byte>(0) : (int)AccountTypes.Player);
            });
        }

        public bool GetName(int accountId, out string name)
        {
            name = "";
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.GET_USERNAME_BY_ID);
            stmt.SetInt32(0, accountId);
            SQLResult result = DB.Login.Query(stmt);
            if (!result.IsEmpty())
            {
                name = result.Read<string>(0);
                return true;
            }

            return false;
        }

        public bool GetEmail(int accountId, out string email)
        {
            email = "";
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.GET_EMAIL_BY_ID);
            stmt.SetInt32(0, accountId);
            SQLResult result = DB.Login.Query(stmt);
            if (!result.IsEmpty())
            {
                email = result.Read<string>(0);
                return true;
            }

            return false;
        }

        public bool CheckPassword(string username, string password)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_CHECK_PASSWORD_BY_NAME);
            stmt.SetString(0, username);

            SQLResult result = DB.Login.Query(stmt);
            if (!result.IsEmpty())
            {
                byte[] salt = result.Read<byte[]>(0);
                byte[] verifier = result.Read<byte[]>(1);
                if (new GruntSRP6(username, salt, verifier).CheckCredentials(username, password))
                    return true;
            }

            return false;
        }

        public bool CheckPassword(int accountId, string password)
        {
            if (!GetName(accountId, out string username))
                return false;

            return CheckPassword(username, password);
        }

        public bool CheckEmail(int accountId, string newEmail)
        {
            string oldEmail;

            // We simply return false for a non-existing email
            if (!GetEmail(accountId, out oldEmail))
                return false;

            if (oldEmail == newEmail)
                return true;

            return false;
        }

        public int GetCharactersCount(int accountId)
        {
            // check character count
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_SUM_CHARS);
            stmt.SetInt32(0, accountId);
            SQLResult result = DB.Characters.Query(stmt);
            return result.IsEmpty() ? 0 : (int)result.Read<long>(0);
        }

        public bool IsBannedAccount(string name)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_BANNED_BY_USERNAME);
            stmt.SetString(0, name);
            SQLResult result = DB.Login.Query(stmt);
            return !result.IsEmpty();
        }

        public bool IsPlayerAccount(AccountTypes gmlevel)
        {
            return gmlevel == AccountTypes.Player;
        }

        public bool IsAdminAccount(AccountTypes gmlevel)
        {
            return gmlevel >= AccountTypes.Administrator && gmlevel <= AccountTypes.Console;
        }

        public bool IsConsoleAccount(AccountTypes gmlevel)
        {
            return gmlevel == AccountTypes.Console;
        }

        public void LoadRBAC()
        {
            _permissions.Clear();
            _defaultPermissions.Clear();

            Log.outDebug(LogFilter.Rbac, "AccountMgr:LoadRBAC");
            RelativeTime oldMSTime = Time.NowRelative;
            uint count1 = 0;
            uint count2 = 0;
            uint count3 = 0;

            Log.outDebug(LogFilter.Rbac, "AccountMgr:LoadRBAC: Loading permissions");
            {
                SQLResult result = DB.Login.Query("SELECT id, name FROM rbac_permissions");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 account permission definitions. DB table `rbac_permissions` is empty.");
                    return;
                }

                do
                {
                    RBACPermissions id = (RBACPermissions)result.Read<int>(0);
                    _permissions[id] = new RBACPermission(id, result.Read<string>(1));
                    ++count1;
                }
                while (result.NextRow());                
            }

            Log.outDebug(LogFilter.Rbac, "AccountMgr:LoadRBAC: Loading linked permissions");
            {
                SQLResult result = DB.Login.Query("SELECT id, linkedId FROM rbac_linked_permissions ORDER BY id ASC");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 linked permissions. DB table `rbac_linked_permissions` is empty.");
                    return;
                }

                RBACPermissions permissionId = 0;
                RBACPermission permission = null;

                do
                {
                    RBACPermissions newId = (RBACPermissions)result.Read<int>(0);
                    if (permissionId != newId)
                    {
                        permissionId = newId;
                        permission = _permissions[newId];
                    }

                    RBACPermissions linkedPermissionId = (RBACPermissions)result.Read<int>(1);
                    if (linkedPermissionId == permissionId)
                    {
                        Log.outError(LogFilter.Sql, $"RBAC Permission {permissionId} has itself as linked permission. Ignored");
                        continue;
                    }
                    permission.AddLinkedPermission(linkedPermissionId);
                    ++count2;
                }
                while (result.NextRow());                
            }

            Log.outDebug(LogFilter.Rbac, "AccountMgr:LoadRBAC: Loading default permissions");
            {
                SQLResult result = DB.Login.Query("SELECT secId, permissionId FROM rbac_default_permissions ORDER BY secId ASC");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 default permission definitions. DB table `rbac_default_permissions` is empty.");
                    return;
                }

                uint secId = 255;
                do
                {
                    uint newId = result.Read<uint>(0);
                    if (secId != newId)
                        secId = newId;

                    _defaultPermissions.Add((byte)secId, (RBACPermissions)result.Read<int>(1));
                    ++count3;
                }
                while (result.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {count1} permission definitions, {count2} linked permissions and {count3} default permissions in {Time.Diff(oldMSTime)} ms.");
        }

        public void UpdateAccountAccess(RBACData rbac, int accountId, byte securityLevel, int realmId)
        {
            if (rbac != null && securityLevel != rbac.GetSecurityLevel())
                rbac.SetSecurityLevel(securityLevel);

            PreparedStatement stmt;
            SQLTransaction trans = new();
            // Delete old security level from DB
            if (realmId == -1)
            {
                stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_ACCOUNT_ACCESS);
                stmt.SetInt32(0, accountId);
                trans.Append(stmt);
            }
            else
            {
                stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_ACCOUNT_ACCESS_BY_REALM);
                stmt.SetInt32(0, accountId);
                stmt.SetInt32(1, realmId);
                trans.Append(stmt);
            }

            // Add new security level
            if (securityLevel != 0)
            {
                stmt = LoginDatabase.GetPreparedStatement(LoginStatements.INS_ACCOUNT_ACCESS);
                stmt.SetInt32(0, accountId);
                stmt.SetUInt8(1, securityLevel);
                stmt.SetInt32(2, realmId);
                trans.Append(stmt);
            }

            DB.Login.CommitTransaction(trans);
        }

        public RBACPermission GetRBACPermission(RBACPermissions permissionId)
        {
            Log.outDebug(LogFilter.Rbac, $"AccountMgr:GetRBACPermission: {permissionId}");
            return _permissions.LookupByKey(permissionId);
        }

        public bool HasPermission(int accountId, RBACPermissions permissionId, int realmId)
        {
            if (accountId == 0)
            {
                Log.outError(LogFilter.Rbac, "AccountMgr:HasPermission: Wrong accountId 0");
                return false;
            }

            RBACData rbac = new(accountId, "", realmId, (byte)GetSecurity(accountId, realmId));
            rbac.LoadFromDB();
            bool hasPermission = rbac.HasPermission(permissionId);

            Log.outDebug(LogFilter.Rbac, 
                $"AccountMgr:HasPermission [AccountId: {accountId}, PermissionId: {permissionId}, realmId: {realmId}]: {hasPermission}");
            return hasPermission;
        }

        public List<RBACPermissions> GetRBACDefaultPermissions(byte secLevel)
        {
            return _defaultPermissions[secLevel];
        }

        public Dictionary<RBACPermissions, RBACPermission> GetRBACPermissionList() { return _permissions; }
    }

    public enum AccountOpResult
    {
        Ok,
        NameTooLong,
        PassTooLong,
        EmailTooLong,
        NameAlreadyExist,
        NameNotExist,
        DBInternalError,
        BadLink
    }
}
