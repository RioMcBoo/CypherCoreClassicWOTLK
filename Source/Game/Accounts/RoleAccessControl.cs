﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using System.Collections.Generic;
using System.Linq;

namespace Game.Accounts
{
    public class RBACData
    {
        int _id;                                        // Account id
        string _name;                                 // Account name
        int _realmId;                                    // RealmId Affected
        byte _secLevel;                                   // Account SecurityLevel
        List<RBACPermissions> _grantedPerms = new();             // Granted permissions
        List<RBACPermissions> _deniedPerms = new();              // Denied permissions
        List<RBACPermissions> _globalPerms = new();              // Calculated permissions

        public RBACData(int id, string name, int realmId, byte secLevel = 255)
        {
            _id = id;
            _name = name;
            _realmId = realmId;
            _secLevel = secLevel;
        }

        public RBACCommandResult GrantPermission(RBACPermissions permissionId, int realmId = 0)
        {
            // Check if permission Id exists
            RBACPermission perm = Global.AccountMgr.GetRBACPermission(permissionId);
            if (perm == null)
            {
                Log.outDebug(LogFilter.Rbac, 
                    $"RBACData.GrantPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). " +
                    $"Permission does not exists");

                return RBACCommandResult.IdDoesNotExists;
            }

            // Check if already added in denied list
            if (HasDeniedPermission(permissionId))
            {
                Log.outDebug(LogFilter.Rbac, 
                    $"RBACData.GrantPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). " +
                    $"Permission in deny list");

                return RBACCommandResult.InDeniedList;
            }

            // Already added?
            if (HasGrantedPermission(permissionId))
            {
                Log.outDebug(LogFilter.Rbac, 
                    $"RBACData.GrantPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). " +
                    $"Permission already granted");

                return RBACCommandResult.CantAddAlreadyAdded;
            }

            AddGrantedPermission(permissionId);

            // Do not save to db when loading data from DB (realmId = 0)
            if (realmId != 0)
            {
                Log.outDebug(LogFilter.Rbac,
                    $"RBACData.GrantPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). Ok and DB updated");

                SavePermission(permissionId, true, realmId);
                CalculateNewPermissions();
            }
            else
            {
                Log.outDebug(LogFilter.Rbac,
                    $"RBACData.GrantPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). Ok");
            }

            return RBACCommandResult.OK;
        }

        public RBACCommandResult DenyPermission(RBACPermissions permissionId, int realmId = 0)
        {
            // Check if permission Id exists
            RBACPermission perm = Global.AccountMgr.GetRBACPermission(permissionId);
            if (perm == null)
            {
                Log.outDebug(LogFilter.Rbac, 
                    $"RBACData.DenyPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). " +
                    $"Permission does not exists");

                return RBACCommandResult.IdDoesNotExists;
            }

            // Check if already added in granted list
            if (HasGrantedPermission(permissionId))
            {
                Log.outDebug(LogFilter.Rbac, 
                    $"RBACData.DenyPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). " +
                    $"Permission in grant list");

                return RBACCommandResult.InGrantedList;
            }

            // Already added?
            if (HasDeniedPermission(permissionId))
            {
                Log.outDebug(LogFilter.Rbac, 
                    $"RBACData.DenyPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). " +
                    $"Permission already denied");

                return RBACCommandResult.CantAddAlreadyAdded;
            }

            AddDeniedPermission(permissionId);

            // Do not save to db when loading data from DB (realmId = 0)
            if (realmId != 0)
            {
                Log.outDebug(LogFilter.Rbac,
                    $"RBACData.DenyPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). Ok and DB updated");

                SavePermission(permissionId, false, realmId);
                CalculateNewPermissions();
            }
            else
            {
                Log.outDebug(LogFilter.Rbac,
                    $"RBACData.DenyPermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). Ok");
            }
            return RBACCommandResult.OK;
        }

        void SavePermission(RBACPermissions permission, bool granted, int realmId)
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.INS_RBAC_ACCOUNT_PERMISSION);
            stmt.SetInt32(0, GetId());
            stmt.SetInt32(1, (int)permission);
            stmt.SetBool(2, granted);
            stmt.SetInt32(3, realmId);
            DB.Login.Execute(stmt);
        }

        public RBACCommandResult RevokePermission(RBACPermissions permissionId, int realmId = 0)
        {
            // Check if it's present in any list
            if (!HasGrantedPermission(permissionId) && !HasDeniedPermission(permissionId))
            {
                Log.outDebug(LogFilter.Rbac, 
                    $"RBACData.RevokePermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). " +
                    $"Not granted or revoked");

                return RBACCommandResult.CantRevokeNotInList;
            }

            RemoveGrantedPermission(permissionId);
            RemoveDeniedPermission(permissionId);

            // Do not save to db when loading data from DB (realmId = 0)
            if (realmId != 0)
            {
                Log.outDebug(LogFilter.Rbac,
                    $"RBACData.RevokePermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). " +
                    $"Ok and DB updated");

                PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_RBAC_ACCOUNT_PERMISSION);
                stmt.SetInt32(0, GetId());
                stmt.SetInt32(1, (int)permissionId);
                stmt.SetInt32(2, realmId);
                DB.Login.Execute(stmt);

                CalculateNewPermissions();
            }
            else
            {
                Log.outDebug(LogFilter.Rbac,
                    $"RBACData.RevokePermission [Id: {GetId()} Name: {GetName()}] " +
                    $"(Permission {permissionId}, RealmId {realmId}). Ok");
            }

            return RBACCommandResult.OK;
        }

        public void LoadFromDB()
        {
            ClearData();

            Log.outDebug(LogFilter.Rbac, 
                $"RBACData.LoadFromDB [Id: {GetId()} Name: {GetName()}]: Loading permissions");

            // Load account permissions (granted and denied) that affect current realm
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_RBAC_ACCOUNT_PERMISSIONS);
            stmt.SetInt32(0, GetId());
            stmt.SetInt32(1, GetRealmId());
            SQLResult result = DB.Login.Query(stmt);
            LoadFromDBCallback(result);
        }

        public QueryCallback LoadFromDBAsync()
        {
            ClearData();

            Log.outDebug(LogFilter.Rbac, 
                $"RBACData.LoadFromDB [Id: {GetId()} Name: {GetName()}]: Loading permissions");

            // Load account permissions (granted and denied) that affect current realm
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_RBAC_ACCOUNT_PERMISSIONS);
            stmt.SetInt32(0, GetId());
            stmt.SetInt32(1, GetRealmId());

            return DB.Login.AsyncQuery(stmt);
        }

        public void LoadFromDBCallback(SQLResult result)
        {
            if (!result.IsEmpty())
            {
                do
                {
                    if (result.Read<bool>(1))
                        GrantPermission((RBACPermissions)result.Read<int>(0));
                    else
                        DenyPermission((RBACPermissions)result.Read<int>(0));

                } while (result.NextRow());
            }

            // Add default permissions
            var permissions = Global.AccountMgr.GetRBACDefaultPermissions(_secLevel);
            foreach (var id in permissions)
                GrantPermission(id);

            // Force calculation of permissions
            CalculateNewPermissions();
        }

        void CalculateNewPermissions()
        {
            Log.outDebug(LogFilter.Rbac, 
                $"RBACData.CalculateNewPermissions [Id: {GetId()} Name: {GetName()}]");

            // Get the list of granted permissions
            _globalPerms = GetGrantedPermissions();
            ExpandPermissions(_globalPerms);
            List<RBACPermissions> revoked = GetDeniedPermissions();
            ExpandPermissions(revoked);
            RemovePermissions(_globalPerms, revoked);
        }

        public void AddPermissions(List<RBACPermissions> permsFrom, List<RBACPermissions> permsTo)
        {
            foreach (var id in permsFrom)
                permsTo.Add(id);
        }

        /// <summary>
        /// Removes a list of permissions from another list
        /// </summary>
        /// <param name="permsFrom"></param>
        /// <param name="permsToRemove"></param>
        void RemovePermissions(List<RBACPermissions> permsFrom, List<RBACPermissions> permsToRemove)
        {
            foreach (var id in permsToRemove)
                permsFrom.Remove(id);
        }

        void ExpandPermissions(List<RBACPermissions> permissions)
        {
            List<RBACPermissions> toCheck = new(permissions);
            permissions.Clear();

            while (!toCheck.Empty())
            {
                // remove the permission from original list
                RBACPermissions permissionId = toCheck.FirstOrDefault();
                toCheck.RemoveAt(0);

                RBACPermission permission = Global.AccountMgr.GetRBACPermission(permissionId);
                if (permission == null)
                    continue;

                // insert into the final list (expanded list)
                permissions.Add(permissionId);

                // add all linked permissions (that are not already expanded) to the list of permissions to be checked
                List<RBACPermissions> linkedPerms = permission.GetLinkedPermissions();
                foreach (var id in linkedPerms)
                    if (!permissions.Contains(id))
                        toCheck.Add(id);
            }

            //Log.outDebug(LogFilter.General, "RBACData:ExpandPermissions: Expanded: {0}", GetDebugPermissionString(permissions));
        }

        void ClearData()
        {
            _grantedPerms.Clear();
            _deniedPerms.Clear();
            _globalPerms.Clear();
        }

        // Gets the Name of the Object
        public string GetName() { return _name; }
        // Gets the Id of the Object
        public int GetId() { return _id; }

        public bool HasPermission(RBACPermissions permission)
        {
            return _globalPerms.Contains(permission);
        }

        // Returns all the granted permissions (after computation)
        public List<RBACPermissions> GetPermissions() { return _globalPerms; }
        // Returns all the granted permissions
        public List<RBACPermissions> GetGrantedPermissions() { return _grantedPerms; }
        // Returns all the denied permissions
        public List<RBACPermissions> GetDeniedPermissions() { return _deniedPerms; }

        public void SetSecurityLevel(byte id)
        {
            _secLevel = id;
            LoadFromDB();
        }

        public byte GetSecurityLevel() { return _secLevel; }
        int GetRealmId() { return _realmId; }

        // Checks if a permission is granted
        bool HasGrantedPermission(RBACPermissions permissionId)
        {
            return _grantedPerms.Contains(permissionId);
        }

        // Checks if a permission is denied
        bool HasDeniedPermission(RBACPermissions permissionId)
        {
            return _deniedPerms.Contains(permissionId);
        }

        // Adds a new granted permission
        void AddGrantedPermission(RBACPermissions permissionId)
        {
            _grantedPerms.Add(permissionId);
        }

        // Removes a granted permission
        void RemoveGrantedPermission(RBACPermissions permissionId)
        {
            _grantedPerms.Remove(permissionId);
        }

        // Adds a new denied permission
        void AddDeniedPermission(RBACPermissions permissionId)
        {
            _deniedPerms.Add(permissionId);
        }

        // Removes a denied permission
        void RemoveDeniedPermission(RBACPermissions permissionId)
        {
            _deniedPerms.Remove(permissionId);
        }
    }

    public class RBACPermission
    {
        RBACPermissions _id;                                 // id of the object
        string _name;                             // name of the object
        List<RBACPermissions> _perms = new();     // Set of permissions

        public RBACPermission(RBACPermissions id = 0, string name = "")
        {
            _id = id;
            _name = name;
        }

        // Gets the Name of the Object
        public string GetName() { return _name; }
        // Gets the Id of the Object
        public RBACPermissions GetId() { return _id; }

        // Gets the Permissions linked to this permission
        public List<RBACPermissions> GetLinkedPermissions() { return _perms; }
        // Adds a new linked Permission
        public void AddLinkedPermission(RBACPermissions id) { _perms.Add(id); }
        // Removes a linked Permission
        public void RemoveLinkedPermission(RBACPermissions id) { _perms.Remove(id); }
    }

    public enum RBACCommandResult
    {
        OK,
        CantAddAlreadyAdded,
        CantRevokeNotInList,
        InGrantedList,
        InDeniedList,
        IdDoesNotExists
    }
}
