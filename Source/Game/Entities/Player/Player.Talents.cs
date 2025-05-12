// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Game.Entities
{
    public partial class Player
    {
        public void InitTalentForLevel()
        {
            var level = GetLevel();
            var talentPoints = CalculateTalentsPoints();
            var spentTalentPoints = GetSpentTalentPointsCount();

            if (spentTalentPoints > 0 && level < PlayerConst.MinSpecializationLevel)
            {
                // Remove all talent points                
                ResetTalents(true);
            }
            else if (!GetSession().HasPermission(RBACPermissions.SkipCheckMoreTalentsThanAllowed))
            {
                if (spentTalentPoints > talentPoints)
                    ResetTalents(true);
            }
           
            int freeTalentPoints = talentPoints - spentTalentPoints;
            //Global.ScriptMgr.OnPlayerFreeTalentPointsChanged(this, freeTalentPoints);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.MaxTalentTiers), talentPoints);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CharacterPoints), freeTalentPoints);

            if (!GetSession().PlayerLoading())
                SendTalentsInfoData();   // update at client
        }

        public bool AddTalent(TalentRecord talent, int rank, int talentGroupId, bool learning)
        {
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(talent.SpellRank[rank], Difficulty.None);
            if (spellInfo == null)
            {
                Log.outError(LogFilter.Spells, 
                    $"Player.AddTalent: Spell (ID: {talent.SpellRank[rank]}) does not exist.");
                return false;
            }

            if (!Global.SpellMgr.IsSpellValid(spellInfo, this, false))
            {
                Log.outError(LogFilter.Spells, 
                    $"Player.AddTalent: Spell (ID: {spellInfo.Id}) is invalid");
                return false;
            }

            var talentMap = GetPlayerTalents(talentGroupId);

            // Remove the previously learned talent
            if (talentMap.TryGetValue(talent.Id, out PlayerTalent itr))
            {
                if (Global.SpellMgr.GetSpellInfo(talent.SpellRank[itr.Rank], Difficulty.None) is SpellInfo spellToRemove)
                {
                    SpellBook.Remove(spellToRemove.Id, true);

                    // search for spells that the talent teaches and unlearn them
                    foreach (var spellEffectInfo in spellToRemove.GetEffects())
                    {
                        if (spellEffectInfo.IsEffect(SpellEffectName.LearnSpell) && spellEffectInfo.TriggerSpell > 0)
                            SpellBook.Remove(spellEffectInfo.TriggerSpell, true);
                    }
                }
            }

            talentMap[talent.Id] = new(rank, PlayerSpellState.Unchanged);

            // Inactive talent groups will only be initialized
            if (GetActiveTalentGroup() == talentGroupId)
            {
                SpellBook.Learn(spellInfo.Id, true);
                if (talent.OverridesSpellID != default)
                    AddOverrideSpell(talent.OverridesSpellID, talent.SpellID);
            }

            if (learning)
                RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.ChangeTalent);

            return true;
        }

        public void RemoveTalent(TalentRecord talent)
        {
            var talentMap = GetPlayerTalents(GetActiveTalentGroup());
            if (!talentMap.TryGetValue(talent.Id, out PlayerTalent itr))
                return;

            var spellId = talent.SpellRank[itr.Rank];

            if (Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None) is SpellInfo spellToRemove)
            {
                SpellBook.Remove(spellToRemove.Id, true);

                // search for spells that the talent teaches and unlearn them
                foreach (var spellEffectInfo in spellToRemove.GetEffects())
                {
                    if (spellEffectInfo.IsEffect(SpellEffectName.LearnSpell) && spellEffectInfo.TriggerSpell > 0)
                        SpellBook.Remove(spellEffectInfo.TriggerSpell, true);
                }
            }

            if (talent.OverridesSpellID != default)
                RemoveOverrideSpell(talent.OverridesSpellID, talent.SpellID);

            // Mark the talent as deleted to it will be deleted upon the next save cycle
            talentMap[talent.Id] = new(0, PlayerSpellState.Removed);
        }

        public bool LearnTalent(int talentId, int requestedRank)
        {
            //  No talent points left to spend, skip learn request
            var curTalentPoints = GetFreeTalentPoints();

            if (curTalentPoints == 0)
                return false;

            if (requestedRank >= PlayerConst.MaxTalentRank)
                return false;
                        
            TalentRecord talentInfo = CliDB.TalentStorage.LookupByKey(talentId);
            if (talentInfo == null)
                return false;

            TalentTabRecord talentTabInfo = CliDB.TalentTabStorage.LookupByKey(talentInfo.TabID);
            if (talentTabInfo == null)
                return false;

            // prevent learn talent for different class (cheating)
            if(!talentTabInfo.ClassMask.HasClass(GetClass()))
                return false;

            // Check for existing talents
            var neededTalentPoints = 0;
            PlayerTalent itr;

            var talentMap = GetPlayerTalents(GetActiveTalentGroup());
            if (talentMap.TryGetValue(talentId, out itr))
            {
                // We already know this or a higher rank of this talent
                if (itr.Rank >= requestedRank)
                    return false;

                neededTalentPoints = itr.Rank - requestedRank + 1;
            }
            else
                neededTalentPoints = requestedRank + 1;

            // Not enough talent points to learn the talent at this rank
            if (neededTalentPoints > curTalentPoints)
                return false;

            // Check talent dependencies
            if (talentInfo.PrereqTalent > 0)
            {
                if (!talentMap.TryGetValue(talentInfo.PrereqTalent, out itr)
                    || itr.Rank < talentInfo.PrereqRank)
                {
                    return false;
                }
            }

            // Find out how many points we have in this field
            var spentPoints = 0;
            if (talentInfo.TierID > 0)
            {
                foreach (var tmpTalent in CliDB.TalentStorage) // the way talents are tracked
                {
                    if (tmpTalent.Value.TabID != talentInfo.TabID)
                        continue;

                    for (int i = 0; i < tmpTalent.Value.SpellRank.Length; ++i)
                    {
                        if (tmpTalent.Value.SpellRank[i] != 0 && HasSpell(tmpTalent.Value.SpellRank[i]))
                            spentPoints += (i + 1);
                    }
                }

                // not have required min points spent in talent tree
                if (spentPoints < talentInfo.TierID * PlayerConst.TalentPointsPerTalentTier)
                    return false;
            }      

            // spell not set in talent.dbc
            var spellid = talentInfo.SpellRank[requestedRank];
            if (spellid == 0)
            {
                Log.outError(LogFilter.Player, 
                    $"Player::LearnTalent: Talent.dbc has no spellInfo " +
                    $"for talent: {talentId} (spell id = 0).");
                return false;
            }

            if (!AddTalent(talentInfo, requestedRank, GetActiveTalentGroup(), true))
                return false;

            Log.outDebug(LogFilter.Misc, 
                $"Player::LearnTalent: TalentID: {talentId} Spell: {spellid} " +
                $"Group: {GetActiveTalentGroup()}\n");

            // update free talent points
            int freeTalentPoints = CalculateTalentsPoints() - GetSpentTalentPointsCount();
            //Global.ScriptMgr.OnPlayerFreeTalentPointsChanged(this, freeTalentPoints);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CharacterPoints), freeTalentPoints);

            return true;
        }        

        uint GetTalentResetCost() { return _specializationInfo.ResetTalentsCost; }

        void SetTalentResetCost(long cost) 
        {
            if (cost > uint.MaxValue)
                throw new ArgumentOutOfRangeException();

            _specializationInfo.ResetTalentsCost = (uint)cost; 
        }

        ServerTime GetTalentResetTime() { return _specializationInfo.ResetTalentsTime; }

        void SetTalentResetTime(ServerTime time_) { _specializationInfo.ResetTalentsTime = time_; }

        public ChrSpecialization GetPrimarySpecialization() { return (ChrSpecialization)m_playerData.CurrentSpecID.GetValue(); }

        void SetPrimarySpecialization(ChrSpecialization spec) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.CurrentSpecID), (int)spec); }

        public ChrSpecializationRecord GetPrimarySpecializationEntry()
        {
            return CliDB.ChrSpecializationStorage.LookupByKey((int)GetPrimarySpecialization());
        }
        
        public byte GetActiveTalentGroup() { return _specializationInfo.ActiveGroup; }
        void SetActiveTalentGroup(int group) { _specializationInfo.ActiveGroup = (byte)group; }

        byte GetBonusTalentGroupCount() { return _specializationInfo.BonusGroups; }
        
        public void SetBonusTalentGroupCount(int amount)
        {
            if (_specializationInfo.BonusGroups == amount)
                return;

            _specializationInfo.BonusGroups = (byte)Math.Min(amount, PlayerConst.MaxSpecializations - 1);
            if (GetActiveTalentGroup() > amount)
            {
                ResetTalents(true);
                ActivateTalentGroup(0);
            }
        }

        // Loot Spec
        public void SetLootSpecId(ChrSpecialization id) 
        { 
            SetLootSpecId((int)id); 
        }
        
        public void SetLootSpecId(int id) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.LootSpecID), (ushort)id); 
        }

        public ChrSpecialization GetLootSpecId() 
        { 
            return (ChrSpecialization)m_activePlayerData.LootSpecID.GetValue(); 
        }

        public ChrSpecialization GetDefaultSpecId()
        {
            return Global.DB2Mgr.GetDefaultChrSpecializationForClass(GetClass()).Id;
        }

        public void ActivateTalentGroup(int talentGroup)
        {
            if (GetActiveTalentGroup() == talentGroup)
                return;

            if (IsNonMeleeSpellCast(false))
                InterruptNonMeleeSpells(false);

            SQLTransaction trans = new();
            _SaveActions(trans);
            DB.Characters.CommitTransaction(trans);

            // TO-DO: We need more research to know what happens with warlock's reagent
            Pet pet = GetPet();
            if (pet != null)
                RemovePet(pet, PetSaveMode.NotInSlot);

            ClearAllReactives();
            UnsummonAllTotems();
            ExitVehicle();
            RemoveAllControlled();

            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.ChangeSpec);

            // remove single target auras at other targets
            var scAuras = GetSingleCastAuras();
            foreach (var aura in scAuras)
            {
                if (aura.GetUnitOwner() != this)
                    aura.Remove();
            }

            // Let client clear his current Actions
            SendActionButtons(ActionsButtonsUpdateReason.SpecSwap);

            foreach (var talent in GetPlayerTalents(GetActiveTalentGroup()))
            {

                if (!CliDB.TalentStorage.TryGetValue(talent.Key, out TalentRecord talentEntry))
                    continue;

                foreach (var spellId in talentEntry.SpellRank)
                {
                    if (spellId == 0)
                        continue;

                    var spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
                    if (spellInfo == null)
                        continue;

                    SpellBook.Remove(spellInfo.Id, true);

                    // search for spells that the talent teaches and unlearn them
                    foreach (var spellEffectInfo in spellInfo.GetEffects())
                    {
                        if (spellEffectInfo.IsEffect(SpellEffectName.LearnSpell) && spellEffectInfo.TriggerSpell > 0)
                            SpellBook.Remove(spellEffectInfo.TriggerSpell, true);
                    }
                }

                if (talentEntry.OverridesSpellID != 0)
                    RemoveOverrideSpell(talentEntry.OverridesSpellID, talentEntry.SpellID);
            }

            // m_actionButtons.clear() is called in the next _LoadActionButtons

            ApplyTraitConfig(m_activePlayerData.ActiveCombatTraitConfigID, false);

            foreach (var glyphId in GetGlyphs(GetActiveTalentGroup()))
            {
                if (glyphId == 0)
                    continue;

                RemoveAurasDueToSpell(CliDB.GlyphPropertiesStorage.LookupByKey(glyphId).SpellID);
            }

            SetActiveTalentGroup(talentGroup);
            SetPrimarySpecialization(ChrSpecialization.None);

            // if the talent can be found in the newly activated PlayerTalentMap
            foreach (var talent in GetPlayerTalents(GetActiveTalentGroup()))
            {

                if (!CliDB.TalentStorage.TryGetValue(talent.Key, out TalentRecord talentEntry))
                    continue;

                var spellInfo = Global.SpellMgr.GetSpellInfo(talentEntry.SpellRank[talent.Value.Rank], Difficulty.None);
                {
                    if (spellInfo == null)
                        continue;
                }

                SpellBook.Learn(spellInfo.Id, true);     // add the talent to the PlayerSpellMap

                if (talentEntry.OverridesSpellID != 0)
                    AddOverrideSpell(talentEntry.OverridesSpellID, talentEntry.SpellID);
            }

            InitTalentForLevel();

            StartLoadingActionButtons();

            UpdateDisplayPower();
            PowerType pw = GetPowerType();
            if (pw != PowerType.Mana)
                SetPower(PowerType.Mana, 0); // Mana must be 0 even if it isn't the active power type.

            SetPower(pw, 0);
            UpdateItemSetAuras(false);

            // update visible transmog
            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
            {
                Item equippedItem = GetItemByPos(i);
                if (equippedItem != null)
                    SetVisibleItemSlot(i, equippedItem);
            }

            foreach (var glyphId in GetGlyphs(talentGroup))
            {
                if (glyphId == 0)
                    continue;

                CastSpell(this, CliDB.GlyphPropertiesStorage.LookupByKey(glyphId).SpellID, true);
            }

            var shapeshiftAuras = GetAuraEffectsByType(AuraType.ModShapeshift);
            
            foreach (AuraEffect aurEff in shapeshiftAuras)
            {
                aurEff.HandleShapeshiftBoosts(this, false);
                aurEff.HandleShapeshiftBoosts(this, true);
            }
        }

        void StartLoadingActionButtons(Action callback = null)
        {
            uint traitConfigId = 0;

            TraitConfig traitConfig = GetTraitConfig(m_activePlayerData.ActiveCombatTraitConfigID);
            if (traitConfig != null)
            {
                int usedSavedTraitConfigIndex = m_activePlayerData.TraitConfigs.FindIndexIf(savedConfig =>
                {
                    return (TraitConfigType)(int)savedConfig.Type == TraitConfigType.Combat
                    && ((TraitCombatConfigFlags)(int)savedConfig.CombatConfigFlags & TraitCombatConfigFlags.ActiveForSpec) == TraitCombatConfigFlags.None
                    && ((TraitCombatConfigFlags)(int)savedConfig.CombatConfigFlags & TraitCombatConfigFlags.SharedActionBars) == TraitCombatConfigFlags.None
                    && savedConfig.LocalIdentifier == traitConfig.LocalIdentifier;
                });

                if (usedSavedTraitConfigIndex >= 0)
                    traitConfigId = (uint)(int)m_activePlayerData.TraitConfigs[usedSavedTraitConfigIndex].ID;
            }

            // load them asynchronously
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHARACTER_ACTIONS_SPEC);
            stmt.SetInt64(0, GetGUID().GetCounter());
            stmt.SetUInt8(1, GetActiveTalentGroup());
            stmt.SetUInt32(2, traitConfigId);

            var myGuid = GetGUID();

            WorldSession mySess = GetSession();
            mySess.GetQueryProcessor().AddCallback(DB.Characters.AsyncQuery(stmt)
                .WithCallback(result =>
                {
                    // safe callback, we can't pass this pointer directly
                    // in case player logs out before db response (player would be deleted in that case)
                    Player thisPlayer = mySess.GetPlayer();
                    if (thisPlayer != null && thisPlayer.GetGUID() == myGuid)
                        thisPlayer.LoadActions(result);

                    if (callback != null)
                        callback();
                }));
        }



        public Dictionary<int, PlayerTalent> GetPlayerTalents(int talentGroupId) => _specializationInfo.Talents[talentGroupId];
        public int[] GetGlyphs(int spec) { return _specializationInfo.Glyphs[spec]; }

        public long GetNextResetTalentsCost()
        {
            // The first time reset costs 1 gold
            if (GetTalentResetCost() < 1 * MoneyConstants.Gold)
                return 1 * MoneyConstants.Gold;
            // then 5 gold
            else if (GetTalentResetCost() < 5 * MoneyConstants.Gold)
                return 5 * MoneyConstants.Gold;
            // After that it increases in increments of 5 gold
            else if (GetTalentResetCost() < 10 * MoneyConstants.Gold)
                return 10 * MoneyConstants.Gold;
            else
            {
                long months = (long)((LoopTime.ServerTime - GetTalentResetTime()) / Time.SpanFromDays(30));
                if (months > 0)
                {
                    // This cost will be reduced by a rate of 5 gold per month
                    var new_cost = GetTalentResetCost() - 5 * MoneyConstants.Gold * months;
                    // to a minimum of 10 gold.
                    return new_cost < 10 * MoneyConstants.Gold ? 10 * MoneyConstants.Gold : new_cost;
                }
                else
                {
                    // After that it increases in increments of 5 gold
                    long new_cost = GetTalentResetCost() + 5 * MoneyConstants.Gold;
                    // until it hits a cap of 50 gold.
                    if (new_cost > 50 * MoneyConstants.Gold)
                        new_cost = 50 * MoneyConstants.Gold;
                    return new_cost;
                }
            }
        }

        public bool ResetTalents(bool noCost = false)
        {
            Global.ScriptMgr.OnPlayerTalentsReset(this, noCost);

            // not need after this call
            if (HasAtLoginFlag(AtLoginFlags.ResetTalents))
                RemoveAtLoginFlag(AtLoginFlags.ResetTalents, true);

            long cost = 0;

            if (!noCost && !WorldConfig.Values[WorldCfg.NoResetTalentCost].Bool)
            {
                cost = GetNextResetTalentsCost();

                if (!HasEnoughMoney(cost))
                {
                    SendBuyError(BuyResult.NotEnoughtMoney, null, 0);
                    return false;
                }
            }
            
            RemovePet(null, PetSaveMode.NotInSlot, true);

            var talentMap = GetPlayerTalents(GetActiveTalentGroup());

            foreach (var talentInfo in talentMap)
            {
                var talentEntry = CliDB.TalentStorage.LookupByKey(talentInfo.Key);
                if (talentEntry == null)
                    continue;

                /*
                CliDB.TalentTabStorage.TryGetValue(talentInfo.TabID, out TalentTabRecord talentTabInfo);
                if (talentTabInfo == null)
                    continue;
                unlearn only talents for character class
                some spell learned by one class as normal spells or know at creation but another class learn it as talent,
                 to prevent unexpected lost normal learned spell skip another class talents
                if ((GetClassMask() & (uint) talentTabInfo.ClassMask) == 0)
                    continue;
                */

                RemoveTalent(talentEntry);
            }

            SQLTransaction trans = new();
            _SaveTalents(trans);
            _SaveSpells(trans);
            DB.Characters.CommitTransaction(trans);

            if (!noCost)
            {
                ModifyMoney(-cost);
                UpdateCriteria(CriteriaType.MoneySpentOnRespecs, cost);
                UpdateCriteria(CriteriaType.TotalRespecs, 1);

                SetTalentResetCost(cost);
                SetTalentResetTime(LoopTime.ServerTime);
            }

            /* when prev line will dropped use next line
            if (Pet* pet = GetPet())
            {
                if (pet->getPetType() == HUNTER_PET && !pet->GetCreatureTemplate()->IsTameable(CanTameExoticPets()))
                    RemovePet(nullptr, PET_SAVE_NOT_IN_SLOT, true);
            }
            */

            InitTalentForLevel();
            return true;
        }

        public void ResetPetTalents()
        {
            // This needs another gossip option + NPC text as a confirmation.
            // The confirmation gossip listid has the text: "Yes, please do."
            Pet pet = GetPet();

            if (pet == null || pet.GetPetType() != PetType.Hunter || pet.UsedTalentCount == 0)
                return;

            CharmInfo charmInfo = pet.GetCharmInfo();
            if (charmInfo == null)
            {
                Log.outError(LogFilter.Player,
                    $"Object {pet.GetGUID()} is considered pet-like, but doesn't have charm info!");

                return;
            }

            pet.ResetTalents();
            SendTalentsInfoData(true);
        }

        public void ResetTalentsForAllPets(Pet onlinePet = null, bool involuntarily = false)
        {
            // not need after this call
            if (HasAtLoginFlag(AtLoginFlags.ResetPetTalents))
                RemoveAtLoginFlag(AtLoginFlags.ResetPetTalents, true);

            // reset for online
            if (onlinePet != null)
                onlinePet.ResetTalents(involuntarily);

            PetStable petStable = GetPetStable();
            if (petStable == null)
                return;

            HashSet<int> petIds = new();
            if (petStable.CurrentPetIndex.HasValue)
                petIds.Add(petStable.CurrentPetIndex.Value);

            foreach (var stabledPet in petStable.StabledPets)
            {
                if (stabledPet != null)
                    petIds.Add(stabledPet.PetNumber);
            }

            foreach (var unslottedPet in petStable.UnslottedPets)
            {
                if (unslottedPet != null)
                    petIds.Add(unslottedPet.PetNumber);
            }

            // now need only reset for offline pets (all pets except online case)
            if (onlinePet != null)
                petIds.Remove(onlinePet.GetCharmInfo().GetPetNumber());

            // no offline pets
            if (petIds.Empty())
                return;

            if (onlinePet == null)
                SendPacket(new TalentsInvoluntarilyReset(true));

            //bool need_comma = false;
            //std::ostringstream ss;
            //ss << "DELETE FROM pet_spell WHERE guid IN (";

            //for (uint32 id : petIds)
            //{
            //    if (need_comma)
            //        ss << ',';

            //    ss << id;

            //    need_comma = true;
            //}

            //ss << ") AND spell IN (";

            //need_comma = false;
            //for (uint32 spell : sPetTalentSpells)
            //{
            //    if (need_comma)
            //        ss << ',';

            //    ss << spell;

            //    need_comma = true;
            //}

            //ss << ')';

            //CharacterDatabase.Execute(ss.str().c_str());

            //*********************
            // Delete all of the player's pet spells
            PreparedStatement stmtSpells = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ALL_PET_SPELLS_BY_OWNER);
            stmtSpells.SetInt64(0, GetGUID().GetCounter());
            DB.Characters.Execute(stmtSpells);

            // Then reset all of the player's pet specualizations
            PreparedStatement stmtSpec = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_PET_SPECS_BY_OWNER);
            stmtSpec.SetInt64(0, GetGUID().GetCounter());
            DB.Characters.Execute(stmtSpec);
        }

        void BuildPlayerTalentsInfoData(UpdateTalentData data)
        {
            data.UnspentTalentPoints = GetFreeTalentPoints();

            for (byte specIdx = 0; specIdx < (1 + GetBonusTalentGroupCount()); ++specIdx)
            {
                TalentGroupInfo groupInfoPkt = new();
                groupInfoPkt.SpecID = PlayerConst.MaxSpecializations;
                var talents = GetPlayerTalents(specIdx);

                foreach (var pair in talents)
                {
                    if (pair.Value.State == PlayerSpellState.Removed)
                        continue;

                    TalentRecord talentInfo = CliDB.TalentStorage.LookupByKey(pair.Key);
                    if (talentInfo == null)
                    {
                        Log.outError(LogFilter.Player,
                            $"Player::SendTalentsInfoData: " +
                            $"Player '{GetName()}' ({GetGuild()}) " +
                            $"has unknown talent id: {pair.Key}.");
                        continue;
                    }

                    SpellInfo spellEntry = Global.SpellMgr.GetSpellInfo(talentInfo.SpellRank[pair.Value.Rank], Difficulty.None);
                    if (spellEntry == null)
                    {
                        Log.outError(LogFilter.Player,
                            $"Player::SendTalentsInfoData: " +
                            $"Player '{GetName()}' ({GetGuild()}) " +
                            $"has unknown talent spell: {talentInfo.SpellID}.");
                        continue;
                    }

                    groupInfoPkt.Talents.Add(new(pair.Key, pair.Value.Rank));
                }

                groupInfoPkt.GlyphIDs = GetGlyphs(specIdx);
                data.TalentGroupInfos.Add(groupInfoPkt);
            }
        }

        void BuildPetTalentsInfoData(UpdateTalentData data)
        {
            Pet pet = GetPet();
            if (pet == null)
                return;

            CreatureTemplate ci = pet.GetCreatureTemplate();
            if (ci == null)
                return;

            CreatureFamilyRecord pet_family = CliDB.CreatureFamilyStorage.LookupByKey(ci.Family);
            if (pet_family == null || pet_family.PetTalentType < 0)
                return;

            data.UnspentTalentPoints = pet.GetFreeTalentPoints();            

            TalentGroupInfo groupInfoPkt = new();
            groupInfoPkt.SpecID = PlayerConst.MaxSpecializations;

            for (int talentTabId = 1; talentTabId < CliDB.TalentTabStorage.Count; ++talentTabId)
            {
                TalentTabRecord talentTabInfo = CliDB.TalentTabStorage.LookupByKey(talentTabId);
                if (talentTabInfo == null)
                    continue;

                if (!talentTabInfo.PetTalentMask.HasAnyFlag(1u << pet_family.PetTalentType))
                    continue;

                for (int talentId = 0; talentId < CliDB.TalentStorage.Count; ++talentId)
                {
                    TalentRecord talentInfo = CliDB.TalentStorage.LookupByKey(talentId);
                    if (talentInfo == null)
                        continue;

                    // skip another tab talents
                    if (talentInfo.TabID != talentTabId)
                        continue;

                    // find max talent rank (0~4)
                    sbyte curtalent_maxrank = -1;
                    for (sbyte rank = PlayerConst.MaxTalentRank - 1; rank >= 0; --rank)
                    {
                        if (talentInfo.SpellRank[rank] > 0 && pet.HasSpell(talentInfo.SpellRank[rank]))
                        {
                            curtalent_maxrank = rank;
                            break;
                        }
                    }

                    // not learned talent
                    if (curtalent_maxrank < 0)
                        continue;

                    groupInfoPkt.Talents.Add(new(talentInfo.Id, (byte)curtalent_maxrank));
                }

                data.TalentGroupInfos.Add(groupInfoPkt);
            }
        }

        public void LearnPetTalent(ObjectGuid petGuid, int talentId, int talentRank)
        {
            Pet pet = GetPet();

            if (pet == null)
                return;

            if (petGuid != pet.GetGUID())
                return;

            byte CurTalentPoints = pet.GetFreeTalentPoints();

            if (CurTalentPoints == 0)
                return;

            if (talentRank >= PlayerConst.MaxPetTalentRank)
                return;

            TalentRecord talentInfo = CliDB.TalentStorage.LookupByKey(talentId);

            if (talentInfo == null)
                return;

            TalentTabRecord talentTabInfo = CliDB.TalentTabStorage.LookupByKey(talentInfo.TabID);

            if (talentTabInfo == null)
                return;

            CreatureTemplate ci = pet.GetCreatureTemplate();

            if (ci == null)
                return;

            CreatureFamilyRecord pet_family = CliDB.CreatureFamilyStorage.LookupByKey(ci.Family);

            if (pet_family == null)
                return;

            if (pet_family.PetTalentType < 0)                       // not hunter pet
                return;

            // prevent learn talent for different family (cheating)
            if (!talentTabInfo.PetTalentMask.HasAnyFlag(1u << pet_family.PetTalentType))
                return;

            // find current max talent rank (0~5)
            int curtalent_maxrank = 0; // 0 = not learned any rank
            for (int rank = PlayerConst.MaxTalentRank - 1; rank >= 0; --rank)
            {
                if (talentInfo.SpellRank[rank] > 0 && pet.HasSpell(talentInfo.SpellRank[rank]))
                {
                    curtalent_maxrank = rank + 1;
                    break;
                }
            }

            // we already have same or higher talent rank learned
            if (curtalent_maxrank >= (talentRank + 1))
                return;

            // check if we have enough talent points
            if (CurTalentPoints < (talentRank - curtalent_maxrank + 1))
                return;

            // Check if it requires another talent
            if (talentInfo.PrereqTalent > 0)
            {
                if (CliDB.TalentStorage.LookupByKey(talentInfo.PrereqTalent) is TalentRecord depTalentInfo)
                {
                    bool hasEnoughRank = false;
                    for (int rank = talentInfo.PrereqRank; rank < PlayerConst.MaxTalentRank; rank++)
                    {
                        if (depTalentInfo.SpellRank[rank] != 0)
                        {
                            if (pet.HasSpell(depTalentInfo.SpellRank[rank]))
                                hasEnoughRank = true;
                        }
                    }

                    if (!hasEnoughRank)
                        return;
                }
            }

            // Find out how many points we have in this field
            int spentPoints = 0;

            int tTab = talentInfo.TabID;
            if (talentInfo.TierID > 0)
            {
                int numRows = CliDB.TalentStorage.Count;
                for (int i = 0; i < numRows; ++i)          // Loop through all talents.
                {
                    // Someday, someone needs to revamp
                    if (CliDB.TalentStorage.LookupByKey(i) is TalentRecord tmpTalent)                                  // the way talents are tracked
                    {
                        if (tmpTalent.TabID == tTab)
                        {
                            for (int rank = 0; rank < PlayerConst.MaxTalentRank; rank++)
                            {
                                if (tmpTalent.SpellRank[rank] != 0)
                                {
                                    if (pet.HasSpell(tmpTalent.SpellRank[rank]))
                                    {
                                        spentPoints += (rank + 1);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // not have required min points spent in talent tree
            if (spentPoints < (talentInfo.TierID * PlayerConst.MaxPetTalentRank))
                return;

            // spell not set in Talent.db2
            int spellid = talentInfo.SpellRank[talentRank];
            if (spellid == 0)
            {
                Log.outError(LogFilter.Player,
                            $"Player::LearnPetTalent: " +
                            $"Talent.db2 contains talent: {talentId}, Rank: {talentRank}, spell id = 0");
                return;
            }

            // already known
            if (pet.HasSpell(spellid))
                return;

            // learn! (other talent ranks will unlearned at learning)
            pet.LearnSpell(spellid);

            Log.outLog(LogFilter.Player, LogLevel.Debug,
                            $"Player::LearnPetTalent: " +
                            $"PetTalentID: {talentId}, Rank: {talentRank}, Spell: {spellid}{Environment.NewLine}");

            // update free talent points
            pet.SetFreeTalentPoints((byte)(CurTalentPoints - (talentRank - curtalent_maxrank + 1)));
        }

        public void SendTalentsInfoData(bool IsPetTalents = false)
        {
            UpdateTalentData packet = new();
            var ActiveGroup = GetActiveTalentGroup();
            packet.ActiveGroup = ActiveGroup;
            packet.IsPetTalents = IsPetTalents;

            if (IsPetTalents)
            {
                BuildPetTalentsInfoData(packet);
            }
            else
            {
                BuildPlayerTalentsInfoData(packet);
            }

            SendPacket(packet);
        }

        public void SendRespecWipeConfirm(ObjectGuid guid, long cost, SpecResetType respecType)
        {
            if (cost > uint.MaxValue)
                throw new ArgumentOutOfRangeException();

            RespecWipeConfirm respecWipeConfirm = new();
            respecWipeConfirm.RespecMaster = guid;
            respecWipeConfirm.Cost = (uint)cost;
            respecWipeConfirm.RespecType = respecType;
            SendPacket(respecWipeConfirm);
        }

        int GetFreeTalentPoints() { return m_activePlayerData.CharacterPoints; }

        int GetSpentTalentPointsCount()
        {
            var talentMap = GetPlayerTalents(GetActiveTalentGroup());

            var spentCount = 0;
            
            foreach (var talentState in talentMap.Values)
            {
                if (talentState.State != PlayerSpellState.Removed)
                    spentCount += talentState.Rank + 1;
            }

            return spentCount;
        }

        public void SetWeaponDmgMultiplier(float multiplier, WeaponAttackType attackType)
        {
            SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.WeaponDmgMultipliers, (int)attackType), multiplier);
        }

        public void SetWeaponSpeedMultiplier(float multiplier, WeaponAttackType attackType)
        {
            SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.WeaponAtkSpeedMultipliers, (int)attackType), multiplier);
        }

        public void SetFreeTalentPoints(int freeTalentPoints)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CharacterPoints), freeTalentPoints);
        }

        public int CalculateTalentsPoints()
        {
            return GetNumTalentsAtLevel(GetLevel()) + m_questRewardedTalentPoints;
        }

        public int GetNumTalentsAtLevel(int level)
        {
            var talentsAtLevel = CliDB.NumTalentsAtLevelStorage;
            talentsAtLevel.TryGetValue(level, out NumTalentsAtLevelRecord numTalentsAtLevel);

            if (numTalentsAtLevel == null)
                talentsAtLevel.TryGetValue(talentsAtLevel.GetNumRows() - 1, out numTalentsAtLevel);

            if (numTalentsAtLevel != null)
            {
                switch (GetClass())
                {
                    case Class.DeathKnight:
                        return numTalentsAtLevel.NumTalentsDeathKnight;
                    case Class.DemonHunter:
                        return numTalentsAtLevel.NumTalentsDemonHunter;
                    default:
                        return numTalentsAtLevel.NumTalents;
                }
            }

            return 0;
        }

        public void SetGlyphSlot(int slotIndex, int slotType)
        {
            var v1 = m_values.ModifyValue(m_activePlayerData);
            var v2 = v1.ModifyValue(m_activePlayerData.GlyphSlots, slotIndex);
            
            SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.GlyphSlots, slotIndex), slotType);
        }

        public int GetGlyphSlot(int slotIndex) { return m_activePlayerData.GlyphSlots[slotIndex]; }

        public void SetGlyph(int slotIndex, int glyph)
        {
            _specializationInfo.Glyphs[GetActiveTalentGroup()][slotIndex] = glyph;
            SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Glyphs, slotIndex), glyph);
        }
    
        public int GetGlyph(int slotIndex) { return _specializationInfo.Glyphs[GetActiveTalentGroup()][slotIndex]; }

        // Only sent on CreateObject
        void InitGlyphsForLevel()
        {
            foreach (var gs in CliDB.GlyphSlotStorage.Values)
            {
                if (gs.ToolTip != 0)
                    SetGlyphSlot(gs.ToolTip - 1, gs.Id);
            }

            var level = GetLevel();
            byte value = 0;

            // 0x3F = 0x01 | 0x02 | 0x04 | 0x08 | 0x10 | 0x20 for 80 level
            if (level >= 15)
                value |= (0x01 | 0x02);
            if (level >= 30)
                value |= 0x08;
            if (level >= 50)
                value |= 0x04;
            if (level >= 70)
                value |= 0x10;
            if (level >= 80)
                value |= 0x20;

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.GlyphsEnabled), value);
        }   

        //Traits
        public void CreateTraitConfig(TraitConfigPacket traitConfig)
        {
            int configId = TraitMgr.GenerateNewTraitConfigId();
            bool hasConfigId(int id)
            {
                return m_activePlayerData.TraitConfigs.FindIndexIf(config => config.ID == id) >= 0;
            }

            while (hasConfigId(configId))
                configId = TraitMgr.GenerateNewTraitConfigId();

            traitConfig.ID = configId;

            int traitConfigIndex = m_activePlayerData.TraitConfigs.Size();
            AddTraitConfig(traitConfig);

            foreach (TraitEntry grantedEntry in TraitMgr.GetGrantedTraitEntriesForConfig(traitConfig, this))
            {
                var entryIndex = traitConfig.Entries.Find(entry => entry.TraitNodeID == grantedEntry.TraitNodeID && entry.TraitNodeEntryID == grantedEntry.TraitNodeEntryID);
                if (entryIndex == null)
                {
                    TraitConfig value = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs, traitConfigIndex);
                    AddDynamicUpdateFieldValue(value.ModifyValue(value.Entries), grantedEntry);
                }
            }

            m_traitConfigStates[configId] = PlayerSpellState.Changed;
        }

        void AddTraitConfig(TraitConfigPacket traitConfig)
        {
            var setter = new TraitConfig();
            setter.ModifyValue(setter.ID).SetValue(traitConfig.ID);
            setter.ModifyValue(setter.Name).SetValue(traitConfig.Name);
            setter.ModifyValue(setter.Type).SetValue((int)traitConfig.Type);
            setter.ModifyValue(setter.SkillLineID).SetValue((int)traitConfig.SkillLineID);
            setter.ModifyValue(setter.ChrSpecializationID).SetValue((int)traitConfig.ChrSpecializationID);
            setter.ModifyValue(setter.CombatConfigFlags).SetValue((int)traitConfig.CombatConfigFlags);
            setter.ModifyValue(setter.LocalIdentifier).SetValue(traitConfig.LocalIdentifier);
            setter.ModifyValue(setter.TraitSystemID).SetValue(traitConfig.TraitSystemID);

            AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs), setter);

            foreach (TraitEntryPacket traitEntry in traitConfig.Entries)
            {
                TraitEntry newEntry = new();
                newEntry.TraitNodeID = traitEntry.TraitNodeID;
                newEntry.TraitNodeEntryID = traitEntry.TraitNodeEntryID;
                newEntry.Rank = traitEntry.Rank;
                newEntry.GrantedRanks = traitEntry.GrantedRanks;
                AddDynamicUpdateFieldValue(setter.ModifyValue(setter.Entries), newEntry);
            }
        }

        public TraitConfig GetTraitConfig()
        {
            return GetTraitConfig(m_activePlayerData.ActiveCombatTraitConfigID);
        }

        public TraitConfig GetTraitConfig(int configId)
        {
            int index = m_activePlayerData.TraitConfigs.FindIndexIf(config => config.ID == configId);
            if (index < 0)
                return null;

            return m_activePlayerData.TraitConfigs[index];
        }

        public void UpdateTraitConfig(TraitConfigPacket newConfig, int savedConfigId, bool withCastTime)
        {
            int index = m_activePlayerData.TraitConfigs.FindIndexIf(config => config.ID == newConfig.ID);
            if (index < 0)
                return;

            if (withCastTime)
            {
                CastSpell(this, TraitMgr.COMMIT_COMBAT_TRAIT_CONFIG_CHANGES_SPELL_ID, new CastSpellExtraArgs(SpellValueMod.BasePoint0, savedConfigId).SetCustomArg(newConfig));
                return;
            }

            bool isActiveConfig = true;
            bool loadActionButtons = false;
            switch ((TraitConfigType)(int)m_activePlayerData.TraitConfigs[index].Type)
            {
                case TraitConfigType.Combat:
                    isActiveConfig = newConfig.ID == m_activePlayerData.ActiveCombatTraitConfigID;
                    loadActionButtons = m_activePlayerData.TraitConfigs[index].LocalIdentifier != newConfig.LocalIdentifier;
                    break;
                case TraitConfigType.Profession:
                    isActiveConfig = HasSkill(m_activePlayerData.TraitConfigs[index].SkillLineID);
                    break;
                default:
                    break;
            }

            Action finalizeTraitConfigUpdate = () =>
            {
                TraitConfig newTraitConfig = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs, index);
                SetUpdateFieldValue(newTraitConfig.ModifyValue(newTraitConfig.LocalIdentifier), newConfig.LocalIdentifier);

                ApplyTraitEntryChanges(newConfig.ID, newConfig, isActiveConfig, true);

                if (savedConfigId != 0)
                    ApplyTraitEntryChanges(savedConfigId, newConfig, false, false);

                if (((TraitCombatConfigFlags)(int)newConfig.CombatConfigFlags).HasFlag(TraitCombatConfigFlags.StarterBuild))
                    SetTraitConfigUseStarterBuild(newConfig.ID, true);
            };

            if (loadActionButtons)
            {
                SQLTransaction trans = new SQLTransaction();
                _SaveActions(trans);
                DB.Characters.CommitTransaction(trans);

                StartLoadingActionButtons(finalizeTraitConfigUpdate);
            }
            else
                finalizeTraitConfigUpdate();
        }

        void ApplyTraitEntryChanges(int editedConfigId, TraitConfigPacket newConfig, bool applyTraits, bool consumeCurrencies)
        {
            int editedIndex = m_activePlayerData.TraitConfigs.FindIndexIf(config => config.ID == editedConfigId);
            if (editedIndex < 0)
                return;

            TraitConfig editedConfig = m_activePlayerData.TraitConfigs[editedIndex];

            // remove traits not found in new config
            SortedSet<int> entryIndicesToRemove = new(Comparer<int>.Create((a, b) => -a.CompareTo(b)));
            for (int i = 0; i < editedConfig.Entries.Size(); ++i)
            {
                TraitEntry oldEntry = editedConfig.Entries[i];
                var entryItr = newConfig.Entries.Find(ufEntry => ufEntry.TraitNodeID == oldEntry.TraitNodeID && ufEntry.TraitNodeEntryID == oldEntry.TraitNodeEntryID);
                if (entryItr != null)
                    continue;

                if (applyTraits)
                    ApplyTraitEntry(oldEntry.TraitNodeEntryID, 0, 0, false);

                entryIndicesToRemove.Add(i);
            }

            foreach (int indexToRemove in entryIndicesToRemove)
            {
                TraitConfig traitConfig = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs, editedIndex);
                RemoveDynamicUpdateFieldValue(traitConfig.ModifyValue(traitConfig.Entries), indexToRemove);
            }

            List<TraitEntryPacket> costEntries = new();

            // apply new traits
            for (var i = 0; i < newConfig.Entries.Count; ++i)
            {
                TraitEntryPacket newEntry = newConfig.Entries[i];
                int oldEntryIndex = editedConfig.Entries.FindIndexIf(ufEntry => ufEntry.TraitNodeID == newEntry.TraitNodeID && ufEntry.TraitNodeEntryID == newEntry.TraitNodeEntryID);
                if (oldEntryIndex < 0)
                {
                    if (consumeCurrencies)
                        costEntries.Add(newEntry);

                    TraitConfig newTraitConfig = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs, editedIndex);
                    TraitEntry newUfEntry = new();
                    newUfEntry.TraitNodeID = newEntry.TraitNodeID;
                    newUfEntry.TraitNodeEntryID = newEntry.TraitNodeEntryID;
                    newUfEntry.Rank = newEntry.Rank;
                    newUfEntry.GrantedRanks = newEntry.GrantedRanks;

                    AddDynamicUpdateFieldValue(newTraitConfig.ModifyValue(newTraitConfig.Entries), newUfEntry);

                    if (applyTraits)
                        ApplyTraitEntry(newUfEntry.TraitNodeEntryID, newUfEntry.Rank, 0, true);
                }
                else if (newEntry.Rank != editedConfig.Entries[oldEntryIndex].Rank || newEntry.GrantedRanks != editedConfig.Entries[oldEntryIndex].GrantedRanks)
                {
                    if (consumeCurrencies && newEntry.Rank > editedConfig.Entries[oldEntryIndex].Rank)
                    {
                        TraitEntryPacket costEntry = new();
                        costEntry.Rank -= editedConfig.Entries[oldEntryIndex].Rank;
                        costEntries.Add(newEntry);
                    }

                    TraitConfig traitConfig = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs, editedIndex);
                    TraitEntry traitEntry = traitConfig.ModifyValue(traitConfig.Entries, oldEntryIndex);
                    traitEntry.Rank = newEntry.Rank;
                    traitEntry.GrantedRanks = newEntry.GrantedRanks;
                    SetUpdateFieldValue(traitConfig.Entries, oldEntryIndex, traitEntry);

                    if (applyTraits)
                        ApplyTraitEntry(newEntry.TraitNodeEntryID, newEntry.Rank, newEntry.GrantedRanks, true);
                }
            }

            if (consumeCurrencies)
            {
                Dictionary<int, int> currencies = new();
                foreach (TraitEntryPacket costEntry in costEntries)
                    TraitMgr.FillSpentCurrenciesMap(costEntry, currencies);

                foreach (var (traitCurrencyId, amount) in currencies)
                {
                    TraitCurrencyRecord traitCurrency = CliDB.TraitCurrencyStorage.LookupByKey(traitCurrencyId);
                    if (traitCurrency == null)
                        continue;

                    switch (traitCurrency.CurrencyType)
                    {
                        case TraitCurrencyType.Gold:
                            ModifyMoney(-amount);
                            break;
                        case TraitCurrencyType.CurrencyTypesBased:
                            RemoveCurrency(traitCurrency.CurrencyTypesID, amount /* TODO: CurrencyDestroyReason */);
                            break;
                        default:
                            break;
                    }
                }
            }

            m_traitConfigStates[editedConfigId] = PlayerSpellState.Changed;
        }

        public void RenameTraitConfig(int editedConfigId, string newName)
        {
            int editedIndex = m_activePlayerData.TraitConfigs.FindIndexIf(traitConfig =>
            {
                return traitConfig.ID == editedConfigId
                    && (TraitConfigType)(int)traitConfig.Type == TraitConfigType.Combat
                    && ((TraitCombatConfigFlags)(int)traitConfig.CombatConfigFlags & TraitCombatConfigFlags.ActiveForSpec) == TraitCombatConfigFlags.None;
            });
            if (editedIndex < 0)
                return;

            TraitConfig traitConfig = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs, editedIndex);
            SetUpdateFieldValue(traitConfig.ModifyValue(traitConfig.Name), newName);

            m_traitConfigStates[editedConfigId] = PlayerSpellState.Changed;
        }

        public void DeleteTraitConfig(int deletedConfigId)
        {
            int deletedIndex = m_activePlayerData.TraitConfigs.FindIndexIf(traitConfig =>
            {
                return traitConfig.ID == deletedConfigId
                    && (TraitConfigType)(int)traitConfig.Type == TraitConfigType.Combat
                    && ((TraitCombatConfigFlags)(int)traitConfig.CombatConfigFlags & TraitCombatConfigFlags.ActiveForSpec) == TraitCombatConfigFlags.None;
            });
            if (deletedIndex < 0)
                return;

            RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData)
                .ModifyValue(m_activePlayerData.TraitConfigs), deletedIndex);

            m_traitConfigStates[deletedConfigId] = PlayerSpellState.Removed;
        }

        void ApplyTraitConfig(int configId, bool apply)
        {
            TraitConfig traitConfig = GetTraitConfig(configId);
            if (traitConfig == null)
                return;

            foreach (TraitEntry traitEntry in traitConfig.Entries)
                ApplyTraitEntry(traitEntry.TraitNodeEntryID, traitEntry.Rank, traitEntry.GrantedRanks, apply);
        }

        void ApplyTraitEntry(int traitNodeEntryId, int rank, int grantedRanks, bool apply)
        {
            TraitNodeEntryRecord traitNodeEntry = CliDB.TraitNodeEntryStorage.LookupByKey(traitNodeEntryId);
            if (traitNodeEntry == null)
                return;

            TraitDefinitionRecord traitDefinition = CliDB.TraitDefinitionStorage.LookupByKey(traitNodeEntry.TraitDefinitionID);
            if (traitDefinition == null)
                return;

            if (traitDefinition.SpellID != 0)
            {
                if (apply)
                    SpellBook.Learn(traitDefinition.SpellID, true, 0, false, traitNodeEntry.TraitDefinitionID);
                else
                    SpellBook.Remove(traitDefinition.SpellID);
            }
        }

        public void SetTraitConfigUseStarterBuild(int traitConfigId, bool useStarterBuild)
        {
            int configIndex = m_activePlayerData.TraitConfigs.FindIndexIf(traitConfig =>
            {
                return traitConfig.ID == traitConfigId
                    && (TraitConfigType)(int)traitConfig.Type == TraitConfigType.Combat
                    && ((TraitCombatConfigFlags)(int)traitConfig.CombatConfigFlags & TraitCombatConfigFlags.ActiveForSpec) != TraitCombatConfigFlags.None;
            });
            if (configIndex < 0)
                return;

            bool currentlyUsesStarterBuild = ((TraitCombatConfigFlags)(int)m_activePlayerData.TraitConfigs[configIndex].CombatConfigFlags).HasFlag(TraitCombatConfigFlags.StarterBuild);
            if (currentlyUsesStarterBuild == useStarterBuild)
                return;

            if (useStarterBuild)
            {
                TraitConfig traitConfig = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs, configIndex);
                SetUpdateFieldFlagValue(traitConfig.ModifyValue(traitConfig.CombatConfigFlags), (int)TraitCombatConfigFlags.StarterBuild);
            }
            else
            {
                TraitConfig traitConfig = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs, configIndex);
                RemoveUpdateFieldFlagValue(traitConfig.ModifyValue(traitConfig.CombatConfigFlags), (int)TraitCombatConfigFlags.StarterBuild);
            }

            m_traitConfigStates[traitConfigId] = PlayerSpellState.Changed;
        }

        public void SetTraitConfigUseSharedActionBars(int traitConfigId, bool usesSharedActionBars, bool isLastSelectedSavedConfig)
        {
            int configIndex = m_activePlayerData.TraitConfigs.FindIndexIf(traitConfig =>
            {
                return traitConfig.ID == traitConfigId
                    && (TraitConfigType)(int)traitConfig.Type == TraitConfigType.Combat
                    && ((TraitCombatConfigFlags)(int)traitConfig.CombatConfigFlags & TraitCombatConfigFlags.ActiveForSpec) == TraitCombatConfigFlags.None;
            });
            if (configIndex < 0)
                return;

            bool currentlyUsesSharedActionBars = ((TraitCombatConfigFlags)(int)m_activePlayerData.TraitConfigs[configIndex].CombatConfigFlags).HasFlag(TraitCombatConfigFlags.SharedActionBars);
            if (currentlyUsesSharedActionBars == usesSharedActionBars)
                return;

            TraitConfig traitConfig = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TraitConfigs, configIndex);
            if (usesSharedActionBars)
            {
                SetUpdateFieldFlagValue(traitConfig.ModifyValue(traitConfig.CombatConfigFlags), (int)TraitCombatConfigFlags.SharedActionBars);

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_ACTION_BY_TRAIT_CONFIG);
                stmt.SetInt64(0, GetGUID().GetCounter());
                stmt.SetInt32(1, traitConfigId);
                DB.Characters.Execute(stmt);

                if (isLastSelectedSavedConfig)
                    StartLoadingActionButtons(); // load action buttons that were saved in shared mode
            }
            else
            {
                RemoveUpdateFieldFlagValue(traitConfig.ModifyValue(traitConfig.CombatConfigFlags), (int)TraitCombatConfigFlags.SharedActionBars);

                // trigger a save with traitConfigId
                foreach (var (_, button) in m_actionButtons)
                {
                    if (button.State != ActionButtonUpdateState.Deleted)
                        button.State = ActionButtonUpdateState.New;
                }
            }

            m_traitConfigStates[traitConfigId] = PlayerSpellState.Changed;
        }
    }
}
