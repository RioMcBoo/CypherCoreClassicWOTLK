// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Cryptography;
using Framework.Database;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Game
{
    public sealed class BNetAccountManager : Singleton<BNetAccountManager>
    {
        static int MAX_BNET_EMAIL_STR = 320;
        static int MAX_BNET_PASS_STR = 128;

        BNetAccountManager() { }

        public AccountOpResult CreateBattlenetAccount(string email, string password, bool withGameAccount, out string gameAccountName)
        {
            gameAccountName = "";

            if (email.IsEmpty() || email.Length > MAX_BNET_EMAIL_STR)
                return AccountOpResult.NameTooLong;

            if (password.IsEmpty() || password.Length > MAX_BNET_PASS_STR)
                return AccountOpResult.PassTooLong;

            if (GetId(email) != 0)
                return AccountOpResult.NameAlreadyExist;

            string srpUsername = GetSrpUsername(email.ToUpper());
            var (salt, verifier) = SRP6.MakeBNetRegistrationData<BnetSRP6v2Hash256>(srpUsername, password);

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.INS_BNET_ACCOUNT);
            stmt.SetString(0, email);
            stmt.SetInt8(1, (sbyte)SrpVersion.v2);
            stmt.SetBytes(2, salt);
            stmt.SetBytes(3, verifier);
            DB.Login.DirectExecute(stmt);

            int newAccountId = GetId(email);
            Cypher.Assert(newAccountId != 0);

            if (withGameAccount)
            {
                gameAccountName = newAccountId + "#1";
                string gameAccountPassword = password.Substring(0, Math.Min(16, password.Length));
                Global.AccountMgr.CreateAccount(gameAccountName, gameAccountPassword.ToUpper(), email, newAccountId, 1);
            }

            return AccountOpResult.Ok;
        }

        public AccountOpResult ChangePassword(int accountId, string newPassword)
        {
            string username;
            if (!GetName(accountId, out username))
                return AccountOpResult.NameNotExist;

            if (newPassword.Length > MAX_BNET_PASS_STR)
                return AccountOpResult.PassTooLong;

            string srpUsername = GetSrpUsername(username.ToUpper());
            var (salt, verifier) = SRP6.MakeBNetRegistrationData<BnetSRP6v2Hash256>(srpUsername, newPassword);

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_LOGON);
            stmt.SetInt8(0, (sbyte)SrpVersion.v2);
            stmt.SetBytes(1, salt);
            stmt.SetBytes(2, verifier);
            stmt.SetInt32(3, accountId);
            DB.Login.Execute(stmt);

            return AccountOpResult.Ok;
        }

        public bool CheckPassword(int accountId, string password)
        {
            string username;
            if (!GetName(accountId, out username))
                return false;

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_BNET_CHECK_PASSWORD);
            stmt.SetInt32(0, accountId);
            SQLResult result = DB.Login.Query(stmt);
            if (!result.IsEmpty())
            {
                var salt = result.Read<byte[]>(1);
                var verifier = result.Read<byte[]>(2);
                switch ((SrpVersion)result.Read<sbyte>(0))
                {
                    case SrpVersion.v1:
                        return new BnetSRP6v1Hash256(username, salt, verifier).CheckCredentials(username, password);
                    case SrpVersion.v2:
                        return new BnetSRP6v2Hash256(username, salt, verifier).CheckCredentials(username, password);
                    default:
                        break;
                }
            }

            return false;
        }

        public AccountOpResult LinkWithGameAccount(string email, string gameAccountName)
        {
            int bnetAccountId = GetId(email);
            if (bnetAccountId == 0)
                return AccountOpResult.NameNotExist;

            int gameAccountId = Global.AccountMgr.GetId(gameAccountName);
            if (gameAccountId == 0)
                return AccountOpResult.NameNotExist;

            if (GetIdByGameAccount(gameAccountId) != 0)
                return AccountOpResult.BadLink;

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_BNET_GAME_ACCOUNT_LINK);
            stmt.SetInt32(0, bnetAccountId);
            stmt.SetInt32(1, GetMaxIndex(bnetAccountId) + 1);
            stmt.SetInt32(2, gameAccountId);
            DB.Login.Execute(stmt);
            return AccountOpResult.Ok;
        }

        public AccountOpResult UnlinkGameAccount(string gameAccountName)
        {
            int gameAccountId = Global.AccountMgr.GetId(gameAccountName);
            if (gameAccountId == 0)
                return AccountOpResult.NameNotExist;

            if (GetIdByGameAccount(gameAccountId) == 0)
                return AccountOpResult.BadLink;

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_BNET_GAME_ACCOUNT_LINK);
            stmt.SetNull(0);
            stmt.SetNull(1);
            stmt.SetInt32(2, gameAccountId);
            DB.Login.Execute(stmt);
            return AccountOpResult.Ok;
        }

        public int GetId(string username)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_BNET_ACCOUNT_ID_BY_EMAIL);
            stmt.SetString(0, username);
            SQLResult result = DB.Login.Query(stmt);
            if (!result.IsEmpty())
                return result.Read<int>(0);

            return 0;
        }

        public bool GetName(int accountId, out string name)
        {
            name = "";
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_BNET_ACCOUNT_EMAIL_BY_ID);
            stmt.SetInt32(0, accountId);
            SQLResult result = DB.Login.Query(stmt);
            if (!result.IsEmpty())
            {
                name = result.Read<string>(0);
                return true;
            }

            return false;
        }

        public int GetIdByGameAccount(int gameAccountId)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_BNET_ACCOUNT_ID_BY_GAME_ACCOUNT);
            stmt.SetInt32(0, gameAccountId);
            SQLResult result = DB.Login.Query(stmt);
            if (!result.IsEmpty())
                return result.Read<int>(0);

            return 0;
        }

        public QueryCallback GetIdByGameAccountAsync(int gameAccountId)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_BNET_ACCOUNT_ID_BY_GAME_ACCOUNT);
            stmt.SetInt32(0, gameAccountId);
            return DB.Login.AsyncQuery(stmt);
        }

        public byte GetMaxIndex(int accountId)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_BNET_MAX_ACCOUNT_INDEX);
            stmt.SetInt32(0, accountId);
            SQLResult result = DB.Login.Query(stmt);
            if (!result.IsEmpty())
                return result.Read<byte>(0);

            return 0;
        }

        public string GetSrpUsername(string name)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(name)).ToHexString();
        }
    }

    enum SrpVersion
    {
        v1 = 1, // password length limit 16 characters, case-insensitive, uses SHA256 to generate verifier
        v2 = 2  // password length limit 128 characters, case-sensitive, uses PBKDF2 with SHA512 to generate verifier
    }
}
