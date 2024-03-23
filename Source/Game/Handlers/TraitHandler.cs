// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.TraitsCommitConfig)]
        void HandleTraitsCommitConfig(TraitsCommitConfig traitsCommitConfig)
        {            
        }

        [WorldPacketHandler(ClientOpcodes.ClassTalentsRequestNewConfig)]
        void HandleClassTalentsRequestNewConfig(ClassTalentsRequestNewConfig classTalentsRequestNewConfig)
        {
            if (classTalentsRequestNewConfig.Config.Type != TraitConfigType.Combat)
                return;

            if ((classTalentsRequestNewConfig.Config.CombatConfigFlags & TraitCombatConfigFlags.ActiveForSpec) != (int)TraitCombatConfigFlags.None)
                return;

            long configCount = _player.m_activePlayerData.TraitConfigs._values.Count(traitConfig =>
            {
                return (TraitConfigType)(int)traitConfig.Type == TraitConfigType.Combat
                    && ((TraitCombatConfigFlags)(int)traitConfig.CombatConfigFlags & TraitCombatConfigFlags.ActiveForSpec) == TraitCombatConfigFlags.None;
            });
            if (configCount >= TraitMgr.MAX_COMBAT_TRAIT_CONFIGS)
                return;

            int findFreeLocalIdentifier()
            {
                int index = 1;
                while (_player.m_activePlayerData.TraitConfigs.FindIndexIf(traitConfig =>
                {
                    return (TraitConfigType)(int)traitConfig.Type == TraitConfigType.Combat
                        && traitConfig.ChrSpecializationID == (int)_player.GetPrimarySpecialization()
                        && traitConfig.LocalIdentifier == index;
                }) >= 0)
                    ++index;

                return index;
            }

            classTalentsRequestNewConfig.Config.ChrSpecializationID = _player.GetPrimarySpecialization();
            classTalentsRequestNewConfig.Config.LocalIdentifier = findFreeLocalIdentifier();

            foreach (TraitEntry grantedEntry in TraitMgr.GetGrantedTraitEntriesForConfig(classTalentsRequestNewConfig.Config, _player))
            {
                var newEntry = classTalentsRequestNewConfig.Config.Entries.Find(entry => { return entry.TraitNodeID == grantedEntry.TraitNodeID && entry.TraitNodeEntryID == grantedEntry.TraitNodeEntryID; });
                if (newEntry == null)
                {
                    newEntry = new();
                    classTalentsRequestNewConfig.Config.Entries.Add(newEntry);
                }

                newEntry.TraitNodeID = grantedEntry.TraitNodeID;
                newEntry.TraitNodeEntryID = grantedEntry.TraitNodeEntryID;
                newEntry.Rank = grantedEntry.Rank;
                newEntry.GrantedRanks = grantedEntry.GrantedRanks;

                TraitNodeEntryRecord traitNodeEntry = CliDB.TraitNodeEntryStorage.LookupByKey(grantedEntry.TraitNodeEntryID);
                if (traitNodeEntry != null)
                    if (newEntry.Rank + newEntry.GrantedRanks > traitNodeEntry.MaxRanks)
                        newEntry.Rank = Math.Max(0, traitNodeEntry.MaxRanks - newEntry.GrantedRanks);
            }

            TalentLearnResult validationResult = TraitMgr.ValidateConfig(classTalentsRequestNewConfig.Config, _player);
            if (validationResult != TalentLearnResult.LearnOk)
                return;

            _player.CreateTraitConfig(classTalentsRequestNewConfig.Config);
        }

        [WorldPacketHandler(ClientOpcodes.ClassTalentsRenameConfig)]
        void HandleClassTalentsRenameConfig(ClassTalentsRenameConfig classTalentsRenameConfig)
        {
            _player.RenameTraitConfig(classTalentsRenameConfig.ConfigID, classTalentsRenameConfig.Name);
        }

        [WorldPacketHandler(ClientOpcodes.ClassTalentsDeleteConfig)]
        void HandleClassTalentsDeleteConfig(ClassTalentsDeleteConfig classTalentsDeleteConfig)
        {
            _player.DeleteTraitConfig(classTalentsDeleteConfig.ConfigID);
        }

        [WorldPacketHandler(ClientOpcodes.ClassTalentsSetStarterBuildActive)]
        void HandleClassTalentsSetStarterBuildActive(ClassTalentsSetStarterBuildActive classTalentsSetStarterBuildActive)
        {
            TraitConfig traitConfig = _player.GetTraitConfig(classTalentsSetStarterBuildActive.ConfigID);
            if (traitConfig == null)
                return;

            if ((TraitConfigType)(int)traitConfig.Type != TraitConfigType.Combat)
                return;

            if (!((TraitCombatConfigFlags)(int)traitConfig.CombatConfigFlags).HasFlag(TraitCombatConfigFlags.ActiveForSpec))
                return;

            if (classTalentsSetStarterBuildActive.Active)
            {
                TraitConfigPacket newConfigState = new(traitConfig);

                int freeLocalIdentifier = 1;
                while (_player.m_activePlayerData.TraitConfigs.FindIndexIf(traitConfig =>
                {
                    return (TraitConfigType)(int)traitConfig.Type == TraitConfigType.Combat
                        && traitConfig.ChrSpecializationID == (int)_player.GetPrimarySpecialization()
                        && traitConfig.LocalIdentifier == freeLocalIdentifier;
                }) >= 0)
                    ++freeLocalIdentifier;

                TraitMgr.InitializeStarterBuildTraitConfig(newConfigState, _player);
                newConfigState.CombatConfigFlags |= TraitCombatConfigFlags.StarterBuild;
                newConfigState.LocalIdentifier = freeLocalIdentifier;

                _player.UpdateTraitConfig(newConfigState, 0, true);
            }
            else
                _player.SetTraitConfigUseStarterBuild(classTalentsSetStarterBuildActive.ConfigID, false);
        }

        [WorldPacketHandler(ClientOpcodes.ClassTalentsSetUsesSharedActionBars)]
        void HandleClassTalentsSetUsesSharedActionBars(ClassTalentsSetUsesSharedActionBars classTalentsSetUsesSharedActionBars)
        {
            _player.SetTraitConfigUseSharedActionBars(classTalentsSetUsesSharedActionBars.ConfigID, classTalentsSetUsesSharedActionBars.UsesShared,
                classTalentsSetUsesSharedActionBars.IsLastSelectedSavedConfig);
        }
    }
}
