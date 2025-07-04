﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.AI;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;
using Game.Spells;
using System.Collections.Generic;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.DismissCritter)]
        void HandleDismissCritter(DismissCritter packet)
        {
            Unit pet = ObjectAccessor.GetCreatureOrPetOrVehicle(GetPlayer(), packet.CritterGUID);
            if (pet == null)
            {
                Log.outDebug(LogFilter.Network, 
                    $"Critter {packet.CritterGUID} does not exist - " +
                    $"player '{GetPlayer().GetName()}' ({GetPlayer().GetGUID()} / " +
                    $"account: {GetAccountId()}) attempted to dismiss it (possibly lagged out)");
                return;
            }

            if (GetPlayer().GetCritterGUID() == pet.GetGUID())
            {
                if (pet.IsCreature() && pet.IsSummon())
                    pet.ToTempSummon().UnSummon();
            }
        }

        [WorldPacketHandler(ClientOpcodes.PetAction)]
        void HandlePetAction(PetAction packet)
        {
            if (_player.IsMounted())
                return;

            ObjectGuid guid1 = packet.PetGUID;         //pet guid
            ObjectGuid guid2 = packet.TargetGUID;      //tag guid

            int spellid = packet.Button.Action;
            ActiveStates flag = packet.Button.State;             //delete = 0x07 CastSpell = C1

            // used also for charmed creature
            Unit pet = Global.ObjAccessor.GetUnit(GetPlayer(), guid1);
            if (pet == null)
            {
                Log.outError(LogFilter.Network, 
                    $"HandlePetAction: {guid1} doesn't exist for {GetPlayer().GetGUID()}");
                return;
            }

            if (pet != GetPlayer().GetFirstControlled())
            {
                Log.outError(LogFilter.Network, 
                    $"HandlePetAction: {guid1} does not belong to {GetPlayer().GetGUID()}");
                return;
            }

            if (!pet.IsAlive())
            {
                SpellInfo spell = (flag == ActiveStates.Enabled || flag == ActiveStates.Passive) ? Global.SpellMgr.GetSpellInfo(spellid, pet.GetMap().GetDifficultyID()) : null;
                if (spell == null)
                    return;
                if (!spell.HasAttribute(SpellAttr0.AllowCastWhileDead))
                    return;
            }

            // @todo allow control charmed player?
            if (pet.IsTypeId(TypeId.Player) && !(flag == ActiveStates.Command && spellid == (int)CommandStates.Attack))
                return;

            if (GetPlayer().m_Controlled.Count == 1)
            {
                HandlePetActionHelper(pet, guid1, spellid, flag, guid2,
                    packet.ActionPosition.X, packet.ActionPosition.Y, packet.ActionPosition.Z);
            }
            else
            {
                //If a pet is dismissed, m_Controlled will change
                List<Unit> controlled = new();
                foreach (var unit in GetPlayer().m_Controlled)
                {
                    if (unit.GetEntry() == pet.GetEntry() && unit.IsAlive())
                        controlled.Add(unit);
                }

                foreach (var unit in controlled)
                    HandlePetActionHelper(unit, guid1, spellid, flag, guid2, packet.ActionPosition.X, packet.ActionPosition.Y, packet.ActionPosition.Z);
            }
        }

        [WorldPacketHandler(ClientOpcodes.PetStopAttack, Processing = PacketProcessing.Inplace)]
        void HandlePetStopAttack(PetStopAttack packet)
        {
            Unit pet = ObjectAccessor.GetCreatureOrPetOrVehicle(GetPlayer(), packet.PetGUID);
            if (pet == null)
            {
                Log.outError(LogFilter.Network, 
                    $"HandlePetStopAttack: {packet.PetGUID} does not exist");
                return;
            }

            if (pet != GetPlayer().GetPet() && pet != GetPlayer().GetCharmed())
            {
                Log.outError(LogFilter.Network,
                    $"HandlePetStopAttack: {packet.PetGUID} isn't a pet or charmed creature " +
                    $"of player {GetPlayer().GetName()}");
                return;
            }

            if (!pet.IsAlive())
                return;

            pet.AttackStop();
        }

        void HandlePetActionHelper(Unit pet, ObjectGuid guid1, int spellid, ActiveStates flag, ObjectGuid guid2, float x, float y, float z)
        {
            CharmInfo charmInfo = pet.GetCharmInfo();
            if (charmInfo == null)
            {
                Log.outError(LogFilter.Network, 
                    $"WorldSession.HandlePetAction(petGuid: {guid1}, tagGuid: {guid2}, spellId: {spellid}, flag: {flag}): " +
                    $"object (GUID: {pet.GetGUID()} Entry: {pet.GetEntry()} TypeId: {pet.GetTypeId()}) " +
                    $"is considered pet-like but doesn't have a charminfo!");
                return;
            }

            switch (flag)
            {
                case ActiveStates.Command: //0x07
                    switch ((CommandStates)spellid)
                    {
                        case CommandStates.Stay: // flat = 1792  //STAY
                            pet.GetMotionMaster().Clear(MovementGeneratorPriority.Normal);
                            pet.GetMotionMaster().MoveIdle();
                            charmInfo.SetCommandState(CommandStates.Stay);

                            charmInfo.SetIsCommandAttack(false);
                            charmInfo.SetIsAtStay(true);
                            charmInfo.SetIsCommandFollow(false);
                            charmInfo.SetIsFollowing(false);
                            charmInfo.SetIsReturning(false);
                            charmInfo.SaveStayPosition();
                            break;
                        case CommandStates.Follow: // spellid = 1792  //FOLLOW
                            pet.AttackStop();
                            pet.InterruptNonMeleeSpells(false);
                            pet.GetMotionMaster().MoveFollow(GetPlayer(), SharedConst.PetFollowDist, pet.GetFollowAngle());
                            charmInfo.SetCommandState(CommandStates.Follow);

                            charmInfo.SetIsCommandAttack(false);
                            charmInfo.SetIsAtStay(false);
                            charmInfo.SetIsReturning(true);
                            charmInfo.SetIsCommandFollow(true);
                            charmInfo.SetIsFollowing(false);
                            break;
                        case CommandStates.Attack: // spellid = 1792  //ATTACK
                        {
                            // Can't attack if owner is pacified
                            if (GetPlayer().HasAuraType(AuraType.ModPacify))
                            {
                                // @todo Send proper error message to client
                                return;
                            }

                            // only place where pet can be player
                            Unit TargetUnit = Global.ObjAccessor.GetUnit(GetPlayer(), guid2);
                            if (TargetUnit == null)
                                return;

                            Unit owner = pet.GetOwner();
                            if (owner != null)
                                if (!owner.IsValidAttackTarget(TargetUnit))
                                    return;

                            // This is true if pet has no target or has target but targets differs.
                            if (pet.GetVictim() != TargetUnit || !pet.GetCharmInfo().IsCommandAttack())
                            {
                                if (pet.GetVictim() != null)
                                    pet.AttackStop();

                                if (!pet.IsTypeId(TypeId.Player) && pet.ToCreature().IsAIEnabled())
                                {
                                    charmInfo.SetIsCommandAttack(true);
                                    charmInfo.SetIsAtStay(false);
                                    charmInfo.SetIsFollowing(false);
                                    charmInfo.SetIsCommandFollow(false);
                                    charmInfo.SetIsReturning(false);

                                    CreatureAI AI = pet.ToCreature().GetAI();
                                    if (AI is PetAI)
                                        ((PetAI)AI)._AttackStart(TargetUnit); // force target switch
                                    else
                                        AI.AttackStart(TargetUnit);

                                    //10% Chance to play special pet attack talk, else growl
                                    if (pet.IsPet() && pet.ToPet().GetPetType() == PetType.Summon
                                        && pet != TargetUnit && RandomHelper.IRand(0, 100) < 10)
                                    {
                                        pet.SendPetTalk(PetTalk.Attack);
                                    }
                                    else
                                    {
                                        // 90% Chance for pet and 100% Chance for charmed creature
                                        pet.SendPetAIReaction(guid1);
                                    }
                                }
                                else // charmed player
                                {
                                    charmInfo.SetIsCommandAttack(true);
                                    charmInfo.SetIsAtStay(false);
                                    charmInfo.SetIsFollowing(false);
                                    charmInfo.SetIsCommandFollow(false);
                                    charmInfo.SetIsReturning(false);

                                    pet.Attack(TargetUnit, true);
                                    pet.SendPetAIReaction(guid1);
                                }
                            }
                            break;
                        }
                        case CommandStates.Abandon: // abandon (hunter pet) or dismiss (summoned pet)
                            if (pet.GetCharmerGUID() == GetPlayer().GetGUID())
                                GetPlayer().StopCastingCharm();
                            else if (pet.GetOwnerGUID() == GetPlayer().GetGUID())
                            {
                                Cypher.Assert(pet.IsTypeId(TypeId.Unit));
                                if (pet.IsPet())
                                {
                                    if (pet.ToPet().GetPetType() == PetType.Hunter)
                                        GetPlayer().RemovePet(pet.ToPet(), PetSaveMode.AsDeleted);
                                    else
                                        GetPlayer().RemovePet(pet.ToPet(), PetSaveMode.NotInSlot);
                                }
                                else if (pet.HasUnitTypeMask(UnitTypeMask.Minion))
                                {
                                    ((Minion)pet).UnSummon();
                                }
                            }
                            break;
                        case CommandStates.MoveTo:
                            pet.StopMoving();
                            pet.GetMotionMaster().Clear();
                            pet.GetMotionMaster().MovePoint(0, x, y, z);
                            charmInfo.SetCommandState(CommandStates.MoveTo);

                            charmInfo.SetIsCommandAttack(false);
                            charmInfo.SetIsAtStay(true);
                            charmInfo.SetIsFollowing(false);
                            charmInfo.SetIsReturning(false);
                            charmInfo.SaveStayPosition();
                            break;
                        default:
                            Log.outError(LogFilter.Network, 
                                $"WORLD: unknown PET flag Action {flag} and spellid {spellid}.");
                            break;
                    }
                    break;
                case ActiveStates.Reaction: // 0x6
                    switch ((ReactStates)spellid)
                    {
                        case ReactStates.Passive: //passive
                            pet.AttackStop();
                            goto case ReactStates.Defensive;
                        case ReactStates.Defensive: //recovery
                        case ReactStates.Aggressive: //activete
                            if (pet.IsTypeId(TypeId.Unit))
                                pet.ToCreature().SetReactState((ReactStates)spellid);
                            break;
                    }
                    break;
                case ActiveStates.Disabled: // 0x81    spell (disabled), ignore
                case ActiveStates.Passive: // 0x01
                case ActiveStates.Enabled: // 0xC1    spell
                {
                    Unit unit_target = null;

                    if (!guid2.IsEmpty())
                        unit_target = Global.ObjAccessor.GetUnit(GetPlayer(), guid2);

                    // do not cast unknown spells
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellid, pet.GetMap().GetDifficultyID());
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Network,
                            $"WORLD: unknown PET spell id {spellid}");
                        return;
                    }

                    foreach (var spellEffectInfo in spellInfo.GetEffects())
                    {
                        if (spellEffectInfo.TargetA.GetTarget() == Targets.UnitSrcAreaEnemy
                            || spellEffectInfo.TargetA.GetTarget() == Targets.UnitDestAreaEnemy
                            || spellEffectInfo.TargetA.GetTarget() == Targets.DestDynobjEnemy)
                        {
                            return;
                        }
                    }

                    // do not cast not learned spells
                    if (!pet.HasSpell(spellid) || spellInfo.IsPassive())
                        return;

                    //  Clear the flags as if owner clicked 'attack'. AI will reset them
                    //  after AttackStart, even if spell failed
                    if (pet.GetCharmInfo() != null)
                    {
                        pet.GetCharmInfo().SetIsAtStay(false);
                        pet.GetCharmInfo().SetIsCommandAttack(true);
                        pet.GetCharmInfo().SetIsReturning(false);
                        pet.GetCharmInfo().SetIsFollowing(false);
                    }

                    Spell spell = new(pet, spellInfo, TriggerCastFlags.None);

                    SpellCastResult result = spell.CheckPetCast(unit_target);

                    //auto turn to target unless possessed
                    if (result == SpellCastResult.UnitNotInfront && !pet.IsPossessed() && !pet.IsVehicle())
                    {
                        Unit unit_target2 = spell.m_targets.GetUnitTarget();
                        if (unit_target != null)
                        {
                            if (!pet.HasSpellFocus())
                                pet.SetInFront(unit_target);
                            Player player = unit_target.ToPlayer();
                            if (player != null)
                                pet.SendUpdateToPlayer(player);
                        }
                        else if (unit_target2 != null)
                        {
                            if (!pet.HasSpellFocus())
                                pet.SetInFront(unit_target2);
                            Player player = unit_target2.ToPlayer();
                            if (player != null)
                                pet.SendUpdateToPlayer(player);
                        }
                        Unit powner = pet.GetCharmerOrOwner();
                        if (powner != null)
                        {
                            Player player = powner.ToPlayer();
                            if (player != null)
                                pet.SendUpdateToPlayer(player);
                        }

                        result = SpellCastResult.SpellCastOk;
                    }

                    if (result == SpellCastResult.SpellCastOk)
                    {
                        unit_target = spell.m_targets.GetUnitTarget();

                        //10% Chance to play special pet attack talk, else growl
                        //actually this only seems to happen on special spells, fire shield for imp,
                        //torment for voidwalker, but it's stupid to check every spell
                        if (pet.IsPet() && (pet.ToPet().GetPetType() == PetType.Summon)
                            && (pet != unit_target) && (RandomHelper.IRand(0, 100) < 10))
                        {
                            pet.SendPetTalk(PetTalk.SpecialSpell);
                        }
                        else
                        {
                            pet.SendPetAIReaction(guid1);
                        }

                        if (unit_target != null && !GetPlayer().IsFriendlyTo(unit_target) 
                            && !pet.IsPossessed() && !pet.IsVehicle())
                        {
                            // This is true if pet has no target or has target but targets differs.
                            if (pet.GetVictim() != unit_target)
                            {
                                CreatureAI ai = pet.ToCreature().GetAI();
                                if (ai != null)
                                {
                                    PetAI petAI = (PetAI)ai;
                                    if (petAI != null)
                                        petAI._AttackStart(unit_target); // force victim switch
                                    else
                                        ai.AttackStart(unit_target);
                                }
                            }
                        }

                        spell.Prepare(spell.m_targets);
                    }
                    else
                    {
                        if (pet.IsPossessed() || pet.IsVehicle()) // @todo: confirm this check
                            Spell.SendCastResult(GetPlayer(), spellInfo, spell.m_SpellVisual, spell.m_castId, result);
                        else
                            spell.SendPetCastResult(result);

                        if (!pet.GetSpellHistory().HasCooldown(spellid))
                            pet.GetSpellHistory().ResetCooldown(spellid, true);

                        spell.Finish(result);
                        spell.Dispose();

                        // reset specific flags in case of spell fail. AI will reset other flags
                        if (pet.GetCharmInfo() != null)
                            pet.GetCharmInfo().SetIsCommandAttack(false);
                    }
                    break;
                }
                default:
                    Log.outError(LogFilter.Network, 
                        $"WORLD: unknown PET flag Action {flag} and spellid {spellid}.");
                    break;
            }
        }

        [WorldPacketHandler(ClientOpcodes.QueryPetName, Processing = PacketProcessing.Inplace)]
        void HandleQueryPetName(QueryPetName packet)
        {
            SendQueryPetNameResponse(packet.UnitGUID);
        }

        void SendQueryPetNameResponse(ObjectGuid guid)
        {
            QueryPetNameResponse response = new();
            response.UnitGUID = guid;

            Creature unit = ObjectAccessor.GetCreatureOrPetOrVehicle(GetPlayer(), guid);
            if (unit != null)
            {
                response.Allow = true;
                response.Timestamp = unit.m_unitData.PetNameTimestamp.GetValue();
                response.Name = unit.GetName();

                Pet pet = unit.ToPet();
                if (pet != null)
                {
                    DeclinedName names = pet.GetDeclinedNames();
                    if (names != null)
                    {
                        response.HasDeclined = true;
                        response.DeclinedNames = names;
                    }
                }
            }

            GetPlayer().SendPacket(response);
        }

        bool CheckStableMaster(ObjectGuid guid)
        {
            // spell case or GM
            if (guid == GetPlayer().GetGUID())
            {
                if (!GetPlayer().IsGameMaster() && !GetPlayer().HasAuraType(AuraType.OpenStable))
                {
                    Log.outDebug(LogFilter.Network, 
                        $"{guid} attempt open stable in cheating way.");
                    return false;
                }
            }
            // stable master case
            else
            {
                if (GetPlayer().GetNPCIfCanInteractWith(guid, NPCFlags1.StableMaster, NPCFlags2.None) == null)
                {
                    Log.outDebug(LogFilter.Network, 
                        $"Stablemaster {guid} not found or you can't interact with him.");
                    return false;
                }
            }
            return true;
        }

        [WorldPacketHandler(ClientOpcodes.PetSetAction)]
        void HandlePetSetAction(PetSetAction packet)
        {
            ObjectGuid petguid = packet.PetGUID;
            Unit pet = Global.ObjAccessor.GetUnit(GetPlayer(), petguid);
            if (pet == null || pet != GetPlayer().GetFirstControlled())
            {
                Log.outError(LogFilter.Network, 
                    $"HandlePetSetAction: Unknown {petguid} or pet owner {GetPlayer().GetGUID()}");
                return;
            }

            CharmInfo charmInfo = pet.GetCharmInfo();
            if (charmInfo == null)
            {
                Log.outError(LogFilter.Network, 
                    $"WorldSession.HandlePetSetAction: {pet.GetGUID()} " +
                    $"is considered pet-like but doesn't have a charminfo!");
                return;
            }

            List<Unit> pets = new();
            foreach (Unit controlled in _player.m_Controlled)
            {
                if (controlled.GetEntry() == pet.GetEntry() && controlled.IsAlive())
                    pets.Add(controlled);
            }

            int position = packet.Index;

            int spell_id = packet.ActionButton.Action;
            ActiveStates act_state = packet.ActionButton.State;

            Log.outDebug(LogFilter.Network, 
                $"Player {GetPlayer().GetName()} has changed pet spell action. " +
                $"Position: {position}, Spell: {spell_id}, State: {act_state}");

            foreach (Unit petControlled in pets)
            {
                //if it's act for spell (en/disable/cast) and there is a spell given (0 = remove spell) which pet doesn't know, don't add
                if (!(
                    (act_state == ActiveStates.Enabled || act_state == ActiveStates.Disabled || act_state == ActiveStates.Passive)
                    && spell_id != 0 && !petControlled.HasSpell(spell_id)))
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spell_id, petControlled.GetMap().GetDifficultyID());
                    if (spellInfo != null)
                    {
                        //sign for autocast
                        if (act_state == ActiveStates.Enabled)
                        {
                            if (petControlled.GetTypeId() == TypeId.Unit && petControlled.IsPet())
                            {
                                ((Pet)petControlled).ToggleAutocast(spellInfo, true);
                            }
                            else
                            {
                                foreach (var unit in GetPlayer().m_Controlled)
                                {
                                    if (unit.GetEntry() == petControlled.GetEntry())
                                        unit.GetCharmInfo().ToggleCreatureAutocast(spellInfo, true);
                            }
                        }
                        }
                        //sign for no/turn off autocast
                        else if (act_state == ActiveStates.Disabled)
                        {
                            if (petControlled.GetTypeId() == TypeId.Unit && petControlled.IsPet())
                            {
                                petControlled.ToPet().ToggleAutocast(spellInfo, false);
                            }
                            else
                            {
                                foreach (var unit in GetPlayer().m_Controlled)
                                {
                                    if (unit.GetEntry() == petControlled.GetEntry())
                                        unit.GetCharmInfo().ToggleCreatureAutocast(spellInfo, false);
                            }
                        }
                    }
                    }

                    charmInfo.SetActionBar((byte)position, spell_id, act_state);
                }
            }
        }

        [WorldPacketHandler(ClientOpcodes.PetRename)]
        void HandlePetRename(PetRename packet)
        {
            ObjectGuid petguid = packet.RenameData.PetGUID;
            bool isdeclined = packet.RenameData.HasDeclinedNames;
            string name = packet.RenameData.NewName;

            PetStable petStable = _player.GetPetStable();
            Pet pet = ObjectAccessor.GetPet(GetPlayer(), petguid);
            // check it!
            if (pet == null || !pet.IsPet() || pet.ToPet().GetPetType() != PetType.Hunter
                || !pet.HasPetFlag(UnitPetFlags.CanBeRenamed)
                || pet.GetOwnerGUID() != _player.GetGUID() || pet.GetCharmInfo() == null
                || petStable == null || petStable.GetCurrentPet() == null
                || petStable.GetCurrentPet().PetNumber != pet.GetCharmInfo().GetPetNumber())
            {
                return;
            }

            PetNameInvalidReason res = ObjectManager.CheckPetName(name);
            if (res != PetNameInvalidReason.Success)
            {
                SendPetNameInvalid(res, name, null);
                return;
            }

            if (Global.ObjectMgr.IsReservedName(name))
            {
                SendPetNameInvalid(PetNameInvalidReason.Reserved, name, null);
                return;
            }

            pet.SetName(name);
            pet.SetGroupUpdateFlag(GroupUpdatePetFlags.Name);
            pet.RemovePetFlag(UnitPetFlags.CanBeRenamed);

            petStable.GetCurrentPet().Name = name;
            petStable.GetCurrentPet().WasRenamed = true;

            PreparedStatement stmt;
            SQLTransaction trans = new();
            if (isdeclined)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_PET_DECLINEDNAME);
                stmt.SetInt32(0, pet.GetCharmInfo().GetPetNumber());
                trans.Append(stmt);

                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_PET_DECLINEDNAME);
                stmt.SetInt32(0, pet.GetCharmInfo().GetPetNumber());
                stmt.SetString(1, GetPlayer().GetGUID().ToString());

                for (byte i = 0; i < SharedConst.MaxDeclinedNameCases; i++)
                    stmt.SetString(i + 1, packet.RenameData.DeclinedNames.Name[i]);

                trans.Append(stmt);
            }

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHAR_PET_NAME);
            stmt.SetString(0, name);
            stmt.SetString(1, GetPlayer().GetGUID().ToString());
            stmt.SetInt32(2, pet.GetCharmInfo().GetPetNumber());
            trans.Append(stmt);

            DB.Characters.CommitTransaction(trans);

            pet.SetPetNameTimestamp(LoopTime.ServerTime); // cast can't be helped
        }

        [WorldPacketHandler(ClientOpcodes.PetAbandon)]
        void HandlePetAbandon(PetAbandon packet)
        {
            // pet/charmed
            Creature pet = ObjectAccessor.GetCreatureOrPetOrVehicle(GetPlayer(), packet.Pet);
            if (pet != null && pet.ToPet() != null && pet.ToPet().GetPetType() == PetType.Hunter)
            {
                _player.RemovePet((Pet)pet, PetSaveMode.AsDeleted);
            }
        }

        [WorldPacketHandler(ClientOpcodes.PetSpellAutocast, Processing = PacketProcessing.Inplace)]
        void HandlePetSpellAutocast(PetSpellAutocast packet)
        {
            Creature pet = ObjectAccessor.GetCreatureOrPetOrVehicle(GetPlayer(), packet.PetGUID);
            if (pet == null)
            {
                Log.outError(LogFilter.Network, 
                    $"WorldSession.HandlePetSpellAutocast: {packet.PetGUID} not found.");
                return;
            }

            if (pet != GetPlayer().GetGuardianPet() && pet != GetPlayer().GetCharmed())
            {
                Log.outError(LogFilter.Network, 
                    $"WorldSession.HandlePetSpellAutocast: {packet.PetGUID} " +
                    $"isn't pet of player {GetPlayer().GetName()} ({GetPlayer().GetGUID()}).");
                return;
            }

            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(packet.SpellID, pet.GetMap().GetDifficultyID());
            if (spellInfo == null)
            {
                Log.outError(LogFilter.Network, 
                    $"WorldSession.HandlePetSpellAutocast: " +
                    $"Unknown spell id {packet.SpellID} used by {packet.PetGUID}.");
                return;
            }

            List<Unit> pets = new();
            foreach (Unit controlled in _player.m_Controlled)
                if (controlled.GetEntry() == pet.GetEntry() && controlled.IsAlive())
                    pets.Add(controlled);

            foreach (Unit petControlled in pets)
            {
                // do not add not learned spells/ passive spells
                if (!petControlled.HasSpell(packet.SpellID) || !spellInfo.IsAutocastable())
                    return;

                CharmInfo charmInfo = petControlled.GetCharmInfo();
                if (charmInfo == null)
                {
                    Log.outError(LogFilter.Network, 
                        $"WorldSession.HandlePetSpellAutocastOpcod: " +
                        $"object {petControlled.GetGUID()} is considered pet-like " +
                        $"but doesn't have a charminfo!");
                    return;
                }

                if (petControlled.IsPet())
                    petControlled.ToPet().ToggleAutocast(spellInfo, packet.AutocastEnabled);
                else
                    charmInfo.ToggleCreatureAutocast(spellInfo, packet.AutocastEnabled);

                charmInfo.SetSpellAutocast(spellInfo, packet.AutocastEnabled);
            }
        }

        [WorldPacketHandler(ClientOpcodes.PetCastSpell, Processing = PacketProcessing.Inplace)]
        void HandlePetCastSpell(PetCastSpell petCastSpell)
        {
            Unit caster = Global.ObjAccessor.GetUnit(GetPlayer(), petCastSpell.PetGUID);
            if (caster == null)
            {
                Log.outError(LogFilter.Network, 
                    $"WorldSession.HandlePetCastSpell: Caster {petCastSpell.PetGUID} not found.");
                return;
            }

            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(petCastSpell.Cast.SpellID, caster.GetMap().GetDifficultyID());
            if (spellInfo == null)
            {
                Log.outError(LogFilter.Network, 
                    $"WorldSession.HandlePetCastSpell: unknown spell id {petCastSpell.Cast.SpellID} " +
                    $"tried to cast by {petCastSpell.PetGUID}");
                return;
            }

            // This opcode is also sent from charmed and possessed units (players and creatures)
            if (caster != GetPlayer().GetGuardianPet() && caster != GetPlayer().GetCharmed())
            {
                Log.outError(LogFilter.Network, 
                    $"WorldSession.HandlePetCastSpell: {petCastSpell.PetGUID} " +
                    $"isn't pet of player {GetPlayer().GetName()} ({GetPlayer().GetGUID()}).");
                return;
            }

            SpellCastTargets targets = new(caster, petCastSpell.Cast);

            TriggerCastFlags triggerCastFlags = TriggerCastFlags.None;

            if (spellInfo.IsPassive())
                return;

            // cast only learned spells
            if (!caster.HasSpell(spellInfo.Id))
            {
                bool allow = false;

                // allow casting of spells triggered by clientside periodic trigger auras
                if (caster.HasAuraTypeWithTriggerSpell(AuraType.PeriodicTriggerSpellFromClient, spellInfo.Id))
                {
                    allow = true;
                    triggerCastFlags = TriggerCastFlags.FullMask;
                }

                if (!allow)
                    return;
            }

            Spell spell = new(caster, spellInfo, triggerCastFlags);
            spell.m_fromClient = true;
            spell.m_misc.Data0 = petCastSpell.Cast.Misc[0];
            spell.m_misc.Data1 = petCastSpell.Cast.Misc[1];
            spell.m_targets = targets;

            SpellCastResult result = spell.CheckPetCast(null);

            if (result == SpellCastResult.SpellCastOk)
            {
                Creature creature = caster.ToCreature();
                if (creature != null)
                {
                    Pet pet = creature.ToPet();
                    if (pet != null)
                    {
                        // 10% Chance to play special pet attack talk, else growl
                        // actually this only seems to happen on special spells, fire shield for imp,
                        // torment for voidwalker, but it's stupid to check every spell
                        if (pet.GetPetType() == PetType.Summon && (RandomHelper.IRand(0, 100) < 10))
                            pet.SendPetTalk(PetTalk.SpecialSpell);
                        else
                            pet.SendPetAIReaction(petCastSpell.PetGUID);
                    }
                }

                SpellPrepare spellPrepare = new();
                spellPrepare.ClientCastID = petCastSpell.Cast.CastID;
                spellPrepare.ServerCastID = spell.m_castId;
                SendPacket(spellPrepare);

                spell.Prepare(targets);
            }
            else
            {
                spell.SendPetCastResult(result);

                if (!caster.GetSpellHistory().HasCooldown(spellInfo.Id))
                    caster.GetSpellHistory().ResetCooldown(spellInfo.Id, true);

                spell.Finish(result);
                spell.Dispose();
            }
        }

        void SendPetNameInvalid(PetNameInvalidReason error, string name, DeclinedName declinedName)
        {
            PetNameInvalid petNameInvalid = new();
            petNameInvalid.Result = error;
            petNameInvalid.RenameData.NewName = name;
            for (int i = 0; i < SharedConst.MaxDeclinedNameCases; i++)
                petNameInvalid.RenameData.DeclinedNames.Name[i] = declinedName.Name[i];

            SendPacket(petNameInvalid);
        }

        [WorldPacketHandler(ClientOpcodes.RequestPetInfo)]
        void HandleRequestPetInfo(RequestPetInfo requestPetInfo)
        {
            // Handle the packet CMSG_REQUEST_PET_INFO - sent when player does ingame /reload command

            // Packet sent when player has a pet
            if (_player.GetPet() != null)
                _player.PetSpellInitialize();
            else
            {
                Unit charm = _player.GetCharmed();
                if (charm != null)
                {
                    // Packet sent when player has a possessed unit
                    if (charm.HasUnitState(UnitState.Possessed))
                        _player.PossessSpellInitialize();
                    // Packet sent when player controlling a vehicle
                    else if (charm.HasUnitFlag(UnitFlags.PlayerControlled) && charm.HasUnitFlag(UnitFlags.Possessed))
                        _player.VehicleSpellInitialize();
                    // Packet sent when player has a charmed unit
                    else
                        _player.CharmSpellInitialize();
                }
            }
        }
    }
}
