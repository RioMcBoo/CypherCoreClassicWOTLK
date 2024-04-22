// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public class PetitionManager : Singleton<PetitionManager>
    {
        Dictionary<ObjectGuid, Petition> _petitionStorage = new();

        PetitionManager() { }

        public void LoadPetitions()
        {
            uint oldMSTime = Time.GetMSTime();
            _petitionStorage.Clear();

            SQLResult result = DB.Characters.Query("SELECT petitionguid, ownerguid, name FROM petition");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 petitions.");
                return;
            }

            uint count = 0;
            do
            {
                AddPetition(ObjectGuid.Create(HighGuid.Item, result.Read<long>(0)), ObjectGuid.Create(HighGuid.Player, result.Read<long>(1)), result.Read<string>(2), true);
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} petitions in: {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void LoadSignatures()
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.Characters.Query("SELECT petitionguid, player_account, playerguid FROM petition_sign");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 Petition signs!");
                return;
            }

            uint count = 0;
            do
            {
                Petition petition = GetPetition(ObjectGuid.Create(HighGuid.Item, result.Read<long>(0)));
                if (petition == null)
                    continue;

                petition.AddSignature(result.Read<int>(1), ObjectGuid.Create(HighGuid.Player, result.Read<long>(2)), true);
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} Petition signs in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public void AddPetition(ObjectGuid petitionGuid, ObjectGuid ownerGuid, string name, bool isLoading)
        {
            Petition p = new();
            p.PetitionGuid = petitionGuid;
            p.ownerGuid = ownerGuid;
            p.PetitionName = name;
            p.Signatures.Clear();

            _petitionStorage[petitionGuid] = p;

            if (isLoading)
                return;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_PETITION);
            stmt.SetInt64(0, ownerGuid.GetCounter());
            stmt.SetInt64(1, petitionGuid.GetCounter());
            stmt.SetString(2, name);
            DB.Characters.Execute(stmt);
        }

        public void RemovePetition(ObjectGuid petitionGuid)
        {
            _petitionStorage.Remove(petitionGuid);

            // Delete From DB
            SQLTransaction trans = new();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PETITION_BY_GUID);
            stmt.SetInt64(0, petitionGuid.GetCounter());
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PETITION_SIGNATURE_BY_GUID);
            stmt.SetInt64(0, petitionGuid.GetCounter());
            trans.Append(stmt);

            DB.Characters.CommitTransaction(trans);
        }

        public Petition GetPetition(ObjectGuid petitionGuid)
        {
            return _petitionStorage.LookupByKey(petitionGuid);
        }

        public Petition GetPetitionByOwner(ObjectGuid ownerGuid)
        {
            return _petitionStorage.FirstOrDefault(p => p.Value.ownerGuid == ownerGuid).Value;
        }

        public void RemovePetitionsByOwner(ObjectGuid ownerGuid)
        {
            foreach (var key in _petitionStorage.Keys.ToList())
            {
                if (_petitionStorage[key].ownerGuid == ownerGuid)
                {
                    _petitionStorage.Remove(key);
                    break;
                }
            }

            SQLTransaction trans = new();
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PETITION_BY_OWNER);
            stmt.SetInt64(0, ownerGuid.GetCounter());
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PETITION_SIGNATURE_BY_OWNER);
            stmt.SetInt64(0, ownerGuid.GetCounter());
            trans.Append(stmt);
            DB.Characters.CommitTransaction(trans);
        }

        public void RemoveSignaturesBySigner(ObjectGuid signerGuid)
        {
            foreach (var petitionPair in _petitionStorage)
                petitionPair.Value.RemoveSignatureBySigner(signerGuid);

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ALL_PETITION_SIGNATURES);
            stmt.SetInt64(0, signerGuid.GetCounter());
            DB.Characters.Execute(stmt);
        }
    }

    public class Petition
    {
        public ObjectGuid PetitionGuid;
        public ObjectGuid ownerGuid;
        public string PetitionName;
        public List<(int AccountId, ObjectGuid PlayerGuid)> Signatures = new();

        public bool IsPetitionSignedByAccount(int accountId)
        {
            foreach (var signature in Signatures)
                if (signature.AccountId == accountId)
                    return true;

            return false;
        }

        public void AddSignature(int accountId, ObjectGuid playerGuid, bool isLoading)
        {
            Signatures.Add((accountId, playerGuid));

            if (isLoading)
                return;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_PETITION_SIGNATURE);
            stmt.SetInt64(0, ownerGuid.GetCounter());
            stmt.SetInt64(1, PetitionGuid.GetCounter());
            stmt.SetInt64(2, playerGuid.GetCounter());
            stmt.SetInt32(3, accountId);

            DB.Characters.Execute(stmt);
        }

        public void UpdateName(string newName)
        {
            PetitionName = newName;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_PETITION_NAME);
            stmt.SetString(0, newName);
            stmt.SetInt64(1, PetitionGuid.GetCounter());
            DB.Characters.Execute(stmt);
        }

        public void RemoveSignatureBySigner(ObjectGuid playerGuid)
        {
            foreach (var itr in Signatures)
            {
                if (itr.PlayerGuid == playerGuid)
                {
                    Signatures.Remove(itr);

                    // notify owner
                    Player owner = Global.ObjAccessor.FindConnectedPlayer(ownerGuid);
                    if (owner != null)
                        owner.GetSession().SendPetitionQuery(PetitionGuid);

                    break;
                }
            }
        }
    }
}
