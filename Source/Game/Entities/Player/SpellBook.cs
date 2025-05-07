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
    public class SpellBook
    {
        static void DeleteSpellFromAllPlayers(int spellId)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_INVALID_SPELL_SPELLS);
            stmt.SetInt32(0, spellId);
            DB.Characters.Execute(stmt);
        }

        public SpellBook(Player player) { this.player = player; }

        public void Clear()
        {
            SpellCollection.Clear();
            TradeSkillSpellCollection.Clear();
        }

        public void AddTemporary(int spellId)
        {
            if (SpellCollection.TryGetValue(spellId, out var spell))
                return; // spell already added - do not do anything

            PlayerSpell newspell = new()
            {
                State = PlayerSpellState.Temporary,
                Active = true,
                Dependent = false,
                Disabled = false
            };

            SpellCollection[spellId] = newspell;
        }

        public void RemoveTemporary(int spellId)
        {
            if (!SpellCollection.TryGetValue(spellId, out var spell))
                return; // spell already not in list - do not do anything

            // spell has other state than temporary - do not change it
            if (spell.State != PlayerSpellState.Temporary)
                return;

            SpellCollection.Remove(spellId);
        }

        public void SetFavorite(int spellId, bool favorite)
        {
            if (!SpellCollection.TryGetValue(spellId, out var spell))
                return;

            spell.Favorite = favorite;
            if (spell.State == PlayerSpellState.Unchanged)
                spell.State = PlayerSpellState.Changed;
        }

        bool HandlePassiveSpellLearn(SpellInfo spellInfo)
        {
            // note: form passives activated with shapeshift spells be implemented by HandleShapeshiftBoosts
            // instead of spell_learn_spell
            // talent dependent passives activated at form apply have proper stance data
            ShapeShiftForm form = player.GetShapeshiftForm();
            bool need_cast = spellInfo.Stances == 0 || (form != 0 && Convert.ToBoolean(spellInfo.Stances & (1 << ((int)form - 1)))) ||
            (form == 0 && spellInfo.HasAttribute(SpellAttr2.AllowWhileNotShapeshiftedCasterForm));

            // Check EquippedItemClass
            // passive spells which apply aura and have an item requirement are to be added manually, instead of casted
            if (spellInfo.EquippedItemClass >= 0)
            {
                foreach (var spellEffectInfo in spellInfo.GetEffects())
                {
                    if (spellEffectInfo.IsAura())
                    {
                        if (!player.HasAura(spellInfo.Id) && player.HasItemFitToSpellRequirements(spellInfo))
                            player.AddAura(spellInfo.Id, player);
                        return false;
                    }
                }
            }

            //Check CasterAuraStates
            return need_cast && (spellInfo.CasterAuraState == 0 || player.HasAuraState(spellInfo.CasterAuraState));
        }

        void SendSupercededSpell(int oldSpell, int newSpell)
        {
            SupercededSpells supercededSpells = new();
            LearnedSpellInfo learnedSpellInfo = new();
            learnedSpellInfo.SpellID = newSpell;
            learnedSpellInfo.Superceded = oldSpell;
            supercededSpells.ClientLearnedSpellData.Add(learnedSpellInfo);
            player.SendPacket(supercededSpells);
        }

        public void Learn(int spellId, bool dependent, SkillType fromSkill = 0, bool suppressMessaging = false, int? traitDefinitionId = null)
        {
            PlayerSpell spell = SpellCollection.LookupByKey(spellId);

            bool disabled = (spell != null) && spell.Disabled;
            bool active = !disabled || spell.Active;
            bool favorite = spell != null ? spell.Favorite : false;

            bool learning = Add(spellId, active, true, dependent, false, false, fromSkill, favorite, traitDefinitionId);

            // prevent duplicated entires in spell book, also not send if not in world (loading)
            if (learning && player.IsInWorld)
            {
                LearnedSpells learnedSpells = new();
                LearnedSpellInfo learnedSpellInfo = new();
                learnedSpellInfo.SpellID = spellId;
                learnedSpellInfo.IsFavorite = favorite;
                learnedSpellInfo.TraitDefinitionID = traitDefinitionId;
                learnedSpells.SuppressMessaging = suppressMessaging;
                learnedSpells.ClientLearnedSpellData.Add(learnedSpellInfo);
                player.SendPacket(learnedSpells);
            }

            // learn all disabled higher ranks and required spells (recursive)
            if (disabled)
            {
                var nextSpell = Global.SpellMgr.GetNextSpellInChain(spellId);
                if (nextSpell != 0)
                {
                    var _spell = SpellCollection.LookupByKey(nextSpell);
                    if (spellId != 0 && _spell.Disabled)
                        Learn(nextSpell, false, fromSkill);
                }

                var spellsRequiringSpell = Global.SpellMgr.GetSpellsRequiringSpellBounds(spellId);
                foreach (var id in spellsRequiringSpell)
                {
                    var spell1 = SpellCollection.LookupByKey(id);
                    if (spell1 != null && spell1.Disabled)
                        Learn(id, false, fromSkill);
                }
            }
            else
                player.UpdateQuestObjectiveProgress(QuestObjectiveType.LearnSpell, spellId, 1);
        }

        public void Remove(int spellId, bool disabled = false, bool learnLowRank = true, bool suppressMessaging = false)
        {
            var pSpell = SpellCollection.LookupByKey(spellId);
            if (pSpell == null)
                return;

            if (pSpell.State == PlayerSpellState.Removed || (disabled && pSpell.Disabled)
                || pSpell.State == PlayerSpellState.Temporary)
                return;

            // unlearn non talent higher ranks (recursive)
            int nextSpell = Global.SpellMgr.GetNextSpellInChain(spellId);
            if (nextSpell != 0)
            {
                SpellInfo spellInfo1 = Global.SpellMgr.GetSpellInfo(nextSpell, Difficulty.None);
                if (Has(nextSpell) && !spellInfo1.HasAttribute(SpellCustomAttributes.IsTalent))
                    Remove(nextSpell, disabled, false);
            }

            //unlearn spells dependent from recently removed spells
            var spellsRequiringSpell = Global.SpellMgr.GetSpellsRequiringSpellBounds(spellId);
            foreach (var id in spellsRequiringSpell)
                Remove(id, disabled);

            // re-search, it can be corrupted in prev loop
            pSpell = SpellCollection.LookupByKey(spellId);
            if (pSpell == null)
                return;                                             // already unleared

            bool cur_active = pSpell.Active;
            bool cur_dependent = pSpell.Dependent;
            int? traitDefinitionId = pSpell.TraitDefinitionId;

            if (disabled)
            {
                pSpell.Disabled = disabled;
                if (pSpell.State != PlayerSpellState.New)
                    pSpell.State = PlayerSpellState.Changed;
            }
            else
            {
                if (pSpell.State == PlayerSpellState.New)
                    SpellCollection.Remove(spellId);
                else
                    pSpell.State = PlayerSpellState.Removed;
            }

            player.RemoveOwnedAura(spellId, player.GetGUID());

            // remove pet auras
            for (byte i = 0; i < SpellConst.MaxEffects; ++i)
            {
                PetAura petSpell = Global.SpellMgr.GetPetAura(spellId, i);
                if (petSpell != null)
                    player.RemovePetAura(petSpell);
            }

            // update free primary prof.points (if not overflow setting, can be in case GM use before .learn prof. learning)
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
            if (spellInfo != null && spellInfo.IsPrimaryProfessionFirstRank())
            {
                int freeProfs = player.GetFreePrimaryProfessionPoints() + 1;
                if (freeProfs <= WorldConfig.Values[WorldCfg.MaxPrimaryTradeSkill].Int32)
                    player.SetFreePrimaryProfessions(freeProfs);
            }

            var skill_bounds = Global.SpellMgr.GetSkillLineAbilityMapBounds(spellId);

            // remove dependent skill
            var spellLearnSkill = Global.SpellMgr.GetSpellLearnSkill(spellId);
            if (spellLearnSkill != null)
            {
                int prev_spell = Global.SpellMgr.GetPrevSpellInChain(spellId);
                if (prev_spell == 0)                                    // first rank, remove skill
                    player.SetSkill(spellLearnSkill.skill, 0, 0, 0);
                else
                {
                    // search prev. skill setting by spell ranks chain
                    var prevSkill = Global.SpellMgr.GetSpellLearnSkill(prev_spell);
                    while (prevSkill == null && prev_spell != 0)
                    {
                        prev_spell = Global.SpellMgr.GetPrevSpellInChain(prev_spell);
                        prevSkill = Global.SpellMgr.GetSpellLearnSkill(Global.SpellMgr.GetFirstSpellInChain(prev_spell));
                    }

                    if (prevSkill == null)                                 // not found prev skill setting, remove skill
                        player.SetSkill(spellLearnSkill.skill, 0, 0, 0);
                    else                                            // set to prev. skill setting values
                    {
                        ushort skill_value = player.GetPureSkillValue(prevSkill.skill);
                        ushort skill_max_value = player.GetPureMaxSkillValue(prevSkill.skill);

                        ushort new_skill_max_value = prevSkill.maxvalue;

                        if (new_skill_max_value == 0)
                        {
                            var rcInfo = Global.DB2Mgr.GetSkillRaceClassInfo(prevSkill.skill, player.GetRace(), player.GetClass());
                            if (rcInfo != null)
                            {
                                switch (Global.SpellMgr.GetSkillRangeType(rcInfo))
                                {
                                    case SkillRangeType.Language:
                                        skill_value = 300;
                                        new_skill_max_value = 300;
                                        break;
                                    case SkillRangeType.Level:
                                        new_skill_max_value = player.GetMaxSkillValueForLevel();
                                        break;
                                    case SkillRangeType.Mono:
                                        new_skill_max_value = 1;
                                        break;
                                    case SkillRangeType.Rank:
                                    {
                                        var tier = Global.ObjectMgr.GetSkillTier(rcInfo.SkillTierID);
                                        new_skill_max_value = (ushort)tier.GetValueForTierIndex(prevSkill.step - 1);
                                        break;
                                    }
                                    default:
                                        break;
                                }

                                if (rcInfo.HasFlag(SkillRaceClassInfoFlags.AlwaysMaxValue))
                                    skill_value = new_skill_max_value;
                            }
                        }
                        else if (skill_value > prevSkill.value)
                            skill_value = prevSkill.value;

                        if (skill_max_value > new_skill_max_value)
                            skill_max_value = new_skill_max_value;

                        if (skill_value > new_skill_max_value)
                            skill_value = new_skill_max_value;

                        player.SetSkill(prevSkill.skill, prevSkill.step, skill_value, skill_max_value);
                    }
                }
            }
            else
            {
                // not ranked skills
                foreach (var _spell_idx in skill_bounds)
                {
                    if (_spell_idx.Spell == spellId && _spell_idx.IsSkillTradeSpell)
                    {
                        TradeSkillSpellCollection.Remove(_spell_idx.SkillLine, spellId);
                    }
                }
            }

            // remove dependent spells
            var spell_bounds = Global.SpellMgr.GetSpellLearnSpellMapBounds(spellId);

            foreach (var spellNode in spell_bounds)
            {
                Remove(spellNode.Spell, disabled);
                if (spellNode.OverridesSpell != 0)
                    player.RemoveOverrideSpell(spellNode.OverridesSpell, spellNode.Spell);
            }

            // activate lesser rank in spellbook/action bar, and cast it if need
            bool prev_activate = false;

            int prev_id = Global.SpellMgr.GetPrevSpellInChain(spellId);
            if (prev_id != 0)
            {
                // if ranked non-stackable spell: need activate lesser rank and update dendence state
                // No need to check for spellInfo != NULL here because if cur_active is true,
                // then that means that the spell was already in m_spells, and only valid spells can be pushed there.
                if (cur_active && spellInfo.IsRanked())
                {
                    // need manually update dependence state (learn spell ignore like attempts)
                    var prevSpell = SpellCollection.LookupByKey(prev_id);
                    if (prevSpell != null)
                    {
                        if (prevSpell.Dependent != cur_dependent)
                        {
                            prevSpell.Dependent = cur_dependent;
                            if (prevSpell.State != PlayerSpellState.New)
                                prevSpell.State = PlayerSpellState.Changed;
                        }

                        // now re-learn if need re-activate
                        if (!prevSpell.Active && learnLowRank)
                        {
                            if (Add(prev_id, true, false, prevSpell.Dependent, prevSpell.Disabled))
                            {
                                // downgrade spell ranks in spellbook and action bar
                                SendSupercededSpell(spellId, prev_id);
                                prev_activate = true;
                            }
                        }
                    }
                }
            }

            if (traitDefinitionId.HasValue)
            {
                var traitDefinition = CliDB.TraitDefinitionStorage.LookupByKey(traitDefinitionId.Value);
                if (traitDefinition != null)
                    player.RemoveOverrideSpell(traitDefinition.OverridesSpellID, spellId);
            }

            player.RemoveOverrideSpell(spellId);

            if (player.CanTitanGrip())
            {
                if (spellInfo != null && spellInfo.IsPassive() && spellInfo.HasEffect(SpellEffectName.TitanGrip))
                {
                    player.RemoveAurasDueToSpell(player.GetTitanGripSpellId());
                    player.SetCanTitanGrip(false);
                }
            }

            if (player.CanDualWield())
            {
                if (spellInfo != null && spellInfo.IsPassive() && spellInfo.HasEffect(SpellEffectName.DualWield))
                    player.SetCanDualWield(false);
            }

            if (WorldConfig.Values[WorldCfg.OffhandCheckAtSpellUnlearn].Bool)
                player.AutoUnequipOffhandIfNeed();

            // remove from spell book if not replaced by lesser rank
            if (!prev_activate)
            {
                UnlearnedSpells unlearnedSpells = new();
                unlearnedSpells.SpellID.Add(spellId);
                unlearnedSpells.SuppressMessaging = suppressMessaging;
                player.SendPacket(unlearnedSpells);
            }
        }

        public bool Add(int spellId, bool active, bool learning, bool dependent, bool disabled, bool loading = false, SkillType fromSkill = 0, bool favorite = false, int? traitDefinitionId = null)
        {
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
            if (spellInfo == null)
            {
                // do character spell book cleanup (all characters)
                if (!player.IsInWorld && !learning)
                {
                    Log.outError(LogFilter.Spells,
                        $"Player.AddSpell: Spell (ID: {spellId}) does not exist. " +
                        $"Deleting for all characters in `character_spell`.");

                    DeleteSpellFromAllPlayers(spellId);
                }
                else
                {
                    Log.outError(LogFilter.Spells,
                        $"Player.AddSpell: Spell (ID: {spellId}) does not exist");
                }

                return false;
            }

            if (!Global.SpellMgr.IsSpellValid(spellInfo, player, false))
            {
                // do character spell book cleanup (all characters)
                if (!player.IsInWorld && !learning)
                {
                    Log.outError(LogFilter.Spells,
                        $"Player.AddSpell: Spell (ID: {spellId}) is invalid. " +
                        $"Deleting for all characters in `character_spell`.");

                    DeleteSpellFromAllPlayers(spellId);
                }
                else
                {
                    Log.outError(LogFilter.Spells,
                        $"Player.AddSpell: Spell (ID: {spellId}) is invalid");
                }

                return false;
            }

            PlayerSpellState state = learning ? PlayerSpellState.New : PlayerSpellState.Unchanged;

            bool dependent_set = false;
            bool disabled_case = false;
            bool superceded_old = false;

            if (SpellCollection.TryGetValue(spellId, out var spell))
            {

                if (spell.State == PlayerSpellState.Temporary)
                    RemoveTemporary(spellId);

                var next_active_spell_id = 0;
                // fix activate state for non-stackable low rank (and find next spell for !active case)
                if (spellInfo.IsRanked())
                {
                    var next = Global.SpellMgr.GetNextSpellInChain(spellId);
                    if (next != 0)
                    {
                        if (Has(next))
                        {
                            // high rank already known so this must !active
                            active = false;
                            next_active_spell_id = next;
                        }
                    }
                }

                // not do anything if already known in expected state
                if (spell.State != PlayerSpellState.Removed && spell.Active == active &&
                    spell.Dependent == dependent && spell.Disabled == disabled)
                {
                    if (!player.IsInWorld && !learning)
                        spell.State = PlayerSpellState.Unchanged;

                    return false;
                }

                // dependent spell known as not dependent, overwrite state
                if (spell.State != PlayerSpellState.Removed && !spell.Dependent && dependent)
                {
                    spell.Dependent = dependent;
                    if (spell.State != PlayerSpellState.New)
                        spell.State = PlayerSpellState.Changed;
                    dependent_set = true;
                }

                if (spell.TraitDefinitionId != traitDefinitionId)
                {
                    if (spell.TraitDefinitionId.HasValue)
                    {
                        TraitDefinitionRecord traitDefinition =
                            CliDB.TraitDefinitionStorage.LookupByKey(spell.TraitDefinitionId.Value);

                        if (traitDefinition != null)
                            player.RemoveOverrideSpell(traitDefinition.OverridesSpellID, spellId);
                    }

                    spell.TraitDefinitionId = traitDefinitionId;
                }

                spell.Favorite = favorite;

                // update active state for known spell
                if (spell.Active != active && spell.State != PlayerSpellState.Removed && !spell.Disabled)
                {
                    spell.Active = active;

                    if (!player.IsInWorld && !learning && !dependent_set) // explicitly load from DB and then exist in it already and set correctly
                        spell.State = PlayerSpellState.Unchanged;
                    else if (spell.State != PlayerSpellState.New)
                        spell.State = PlayerSpellState.Changed;

                    if (active)
                    {
                        if (spellInfo.IsPassive() && HandlePassiveSpellLearn(spellInfo))
                            player.CastSpell(player, spellId, true);
                    }
                    else if (player.IsInWorld)
                    {
                        if (next_active_spell_id != 0)
                            SendSupercededSpell(spellId, next_active_spell_id);
                        else
                        {
                            UnlearnedSpells removedSpells = new();
                            removedSpells.SpellID.Add(spellId);
                            player.SendPacket(removedSpells);
                        }
                    }

                    return active;
                }

                if (spell.Disabled != disabled && spell.State != PlayerSpellState.Removed)
                {
                    if (spell.State != PlayerSpellState.New)
                        spell.State = PlayerSpellState.Changed;
                    spell.Disabled = disabled;

                    if (disabled)
                        return false;

                    disabled_case = true;
                }
                else
                {
                    switch (spell.State)
                    {
                        case PlayerSpellState.Unchanged:
                            return false;
                        case PlayerSpellState.Removed:
                        {
                            SpellCollection.Remove(spellId);
                            state = PlayerSpellState.Changed;
                            break;
                        }
                        default:
                        {
                            // can be in case spell loading but learned at some previous spell loading
                            if (!player.IsInWorld && !learning && !dependent_set)
                                spell.State = PlayerSpellState.Unchanged;
                            return false;
                        }
                    }
                }
            }

            if (!disabled_case) // skip new spell adding if spell already known (disabled spells case)
            {
                // non talent spell: learn low ranks (recursive call)
                var prev_spell = Global.SpellMgr.GetPrevSpellInChain(spellId);
                if (prev_spell != 0)
                {
                    if (!player.IsInWorld || disabled)                    // at spells loading, no output, but allow save
                        Add(prev_spell, active, true, true, disabled, false, fromSkill);
                    else                                            // at normal learning
                        Learn(prev_spell, true, fromSkill);
                }

                PlayerSpell newspell = new();
                newspell.State = state;
                newspell.Active = active;
                newspell.Dependent = dependent;
                newspell.Disabled = disabled;
                newspell.Favorite = favorite;
                if (traitDefinitionId.HasValue)
                    newspell.TraitDefinitionId = traitDefinitionId.Value;

                // replace spells in action bars and spellbook to bigger rank if only one spell rank must be accessible
                if (newspell.Active && !newspell.Disabled && spellInfo.IsRanked())
                {
                    foreach (var _spell in SpellCollection)
                    {
                        if (_spell.Value.State == PlayerSpellState.Removed)
                            continue;

                        SpellInfo i_spellInfo = Global.SpellMgr.GetSpellInfo(_spell.Key, Difficulty.None);
                        if (i_spellInfo == null)
                            continue;

                        if (spellInfo.IsDifferentRankOf(i_spellInfo))
                        {
                            if (_spell.Value.Active)
                            {
                                if (spellInfo.IsHighRankOf(i_spellInfo))
                                {
                                    if (player.IsInWorld)                 // not send spell (re-/over-)learn packets at loading
                                        SendSupercededSpell(_spell.Key, spellId);

                                    // mark old spell as disable (SMSG_SUPERCEDED_SPELL replace it in client by new)
                                    _spell.Value.Active = false;
                                    if (_spell.Value.State != PlayerSpellState.New)
                                        _spell.Value.State = PlayerSpellState.Changed;
                                    superceded_old = true;          // new spell replace old in action bars and spell book.
                                }
                                else
                                {
                                    if (player.IsInWorld)                 // not send spell (re-/over-)learn packets at loading
                                        SendSupercededSpell(spellId, _spell.Key);

                                    // mark new spell as disable (not learned yet for client and will not learned)
                                    newspell.Active = false;
                                    if (newspell.State != PlayerSpellState.New)
                                        newspell.State = PlayerSpellState.Changed;
                                }
                            }
                        }
                    }
                }
                
                SpellCollection[spellId] = newspell;

                // return false if spell disabled
                if (newspell.Disabled)
                    return false;
            }

            bool castSpell = false;

            // cast talents with SPELL_EFFECT_LEARN_SPELL (other dependent spells will learned later as not auto-learned)
            // note: all spells with SPELL_EFFECT_LEARN_SPELL isn't passive
            if (!loading && spellInfo.HasAttribute(SpellCustomAttributes.IsTalent) && spellInfo.HasEffect(SpellEffectName.LearnSpell))
            {
                // ignore stance requirement for talent learn spell (stance set for spell only for client spell description show)
                castSpell = true;
            }
            // also cast passive spells (including all talents without SPELL_EFFECT_LEARN_SPELL) with additional checks
            else if (spellInfo.IsPassive())
                castSpell = HandlePassiveSpellLearn(spellInfo);
            else if (spellInfo.HasEffect(SpellEffectName.SkillStep))
                castSpell = true;
            else if (spellInfo.HasAttribute(SpellAttr1.CastWhenLearned))
                castSpell = true;

            if (castSpell)
            {
                CastSpellExtraArgs args = new(TriggerCastFlags.FullMask);

                if (traitDefinitionId.HasValue)
                {
                    TraitConfig traitConfig = player.GetTraitConfig();
                    if (traitConfig != null)
                    {
                        int traitEntryIndex = traitConfig.Entries.FindIndexIf(traitEntry =>
                        {
                            return CliDB.TraitNodeEntryStorage.LookupByKey(traitEntry.TraitNodeEntryID)?.TraitDefinitionID == traitDefinitionId;
                        });

                        int rank = 0;
                        if (traitEntryIndex >= 0)
                            rank = traitConfig.Entries[traitEntryIndex].Rank + traitConfig.Entries[traitEntryIndex].GrantedRanks;

                        if (rank > 0)
                        {
                            var traitDefinitionEffectPoints = TraitMgr.GetTraitDefinitionEffectPointModifiers(traitDefinitionId.Value);
                            if (traitDefinitionEffectPoints != null)
                            {
                                foreach (TraitDefinitionEffectPointsRecord traitDefinitionEffectPoint in traitDefinitionEffectPoints)
                                {
                                    if (traitDefinitionEffectPoint.EffectIndex >= spellInfo.GetEffects().Count)
                                        continue;

                                    float basePoints = Global.DB2Mgr.GetCurveValueAt(traitDefinitionEffectPoint.CurveID, rank);
                                    if (traitDefinitionEffectPoint.OperationType == TraitPointsOperationType.Multiply)
                                        basePoints *= spellInfo.GetEffect(traitDefinitionEffectPoint.EffectIndex).CalcBaseValue(player, null, 0, -1);

                                    args.AddSpellMod(SpellValueMod.BasePoint0 + traitDefinitionEffectPoint.EffectIndex, (int)basePoints);
                                }
                            }
                        }
                    }
                }

                player.CastSpell(player, spellId, args);
                if (spellInfo.HasEffect(SpellEffectName.SkillStep))
                    return false;
            }

            if (traitDefinitionId.HasValue)
            {
                TraitDefinitionRecord traitDefinition = CliDB.TraitDefinitionStorage.LookupByKey(traitDefinitionId.Value);
                if (traitDefinition != null)
                    player.AddOverrideSpell(traitDefinition.OverridesSpellID, spellId);
            }

            // update free primary prof.points (if any, can be none in case GM .learn prof. learning)
            var freeProfs = player.GetFreePrimaryProfessionPoints();
            if (freeProfs != 0)
            {
                if (spellInfo.IsPrimaryProfessionFirstRank())
                    player.SetFreePrimaryProfessions(freeProfs - 1);
            }

            bool IsPlayerLoading = player.GetSession().PlayerLoading();
            var skill_bounds = Global.SpellMgr.GetSkillLineAbilityMapBounds(spellId);
            
            SpellLearnSkillNode spellLearnSkill = Global.SpellMgr.GetSpellLearnSkill(spellId);
            if (spellLearnSkill != null)
            {
                // add dependent skills if this spell is not learned from adding skill already
                if (spellLearnSkill.skill != fromSkill)
                {
                    ushort skill_value = player.GetPureSkillValue(spellLearnSkill.skill);
                    ushort skill_max_value = player.GetPureMaxSkillValue(spellLearnSkill.skill);

                    if (skill_value < spellLearnSkill.value)
                        skill_value = spellLearnSkill.value;

                    ushort new_skill_max_value = spellLearnSkill.maxvalue;

                    if (new_skill_max_value == 0)
                    {
                        var rcInfo = Global.DB2Mgr.GetSkillRaceClassInfo(spellLearnSkill.skill, player.GetRace(), player.GetClass());
                        if (rcInfo != null)
                        {
                            switch (Global.SpellMgr.GetSkillRangeType(rcInfo))
                            {
                                case SkillRangeType.Language:
                                    skill_value = 300;
                                    new_skill_max_value = 300;
                                    break;
                                case SkillRangeType.Level:
                                    new_skill_max_value = player.GetMaxSkillValueForLevel();
                                    break;
                                case SkillRangeType.Mono:
                                    new_skill_max_value = 1;
                                    break;
                                case SkillRangeType.Rank:
                                {
                                    var tier = Global.ObjectMgr.GetSkillTier(rcInfo.SkillTierID);
                                    new_skill_max_value = (ushort)tier.GetValueForTierIndex(spellLearnSkill.step - 1);
                                    break;
                                }
                                default:
                                    break;
                            }

                            if (rcInfo.HasFlag(SkillRaceClassInfoFlags.AlwaysMaxValue))
                                skill_value = new_skill_max_value;
                        }
                    }

                    if (skill_max_value < new_skill_max_value)
                        skill_max_value = new_skill_max_value;

                    player.SetSkill(spellLearnSkill.skill, spellLearnSkill.step, skill_value, skill_max_value);
                }
            }
            else
            {
                // not ranked skills
                foreach (var _spell_idx in skill_bounds)
                {
                    if (_spell_idx.Spell == spellId)
                    {
                        var skillEntry = CliDB.SkillLineStorage.LookupByKey(_spell_idx.SkillLine);
                        if (skillEntry != null && skillEntry.CanLink && _spell_idx.IsSkillTradeSpell)
                        {
                            if (_spell_idx.ClassMask.HasClass(player.GetClass()) && _spell_idx.RaceMask.HasRace(player.GetRace()))
                                TradeSkillSpellCollection.Add(_spell_idx.SkillLine, spellId);
                        }
                    }

                    if (!IsPlayerLoading)
                    {
                        player.UpdateCriteria(CriteriaType.LearnTradeskillSkillLine, (long)_spell_idx.SkillLine);
                        player.UpdateCriteria(CriteriaType.LearnSpellFromSkillLine, (long)_spell_idx.SkillLine);
                    }

                    SkillLineRecord pSkill = CliDB.SkillLineStorage.LookupByKey(_spell_idx.SkillLine);
                    if (pSkill == null)
                        continue;

                    if (_spell_idx.SkillLine == fromSkill)
                        continue;

                    // Runeforging special case
                    if ((_spell_idx.AcquireMethod == AbilityLearnType.OnSkillLearn && !player.HasSkill(_spell_idx.SkillLine))
                        || ((_spell_idx.SkillLine == SkillType.Runeforging) && _spell_idx.TrivialSkillLineRankHigh == 0))
                    {
                        SkillRaceClassInfoRecord rcInfo =
                            Global.DB2Mgr.GetSkillRaceClassInfo(_spell_idx.SkillLine, player.GetRace(), player.GetClass());

                        if (rcInfo != null)
                            player.LearnDefaultSkill(rcInfo);
                    }
                }
            }

            // learn dependent spells
            var spell_bounds = Global.SpellMgr.GetSpellLearnSpellMapBounds(spellId);
            foreach (var spellNode in spell_bounds)
            {
                if (!spellNode.AutoLearned)
                {
                    if (!player.IsInWorld || !spellNode.Active)       // at spells loading, no output, but allow save
                        Add(spellNode.Spell, spellNode.Active, true, true, false);
                    else                                            // at normal learning
                        Learn(spellNode.Spell, true);
                }

                if (spellNode.OverridesSpell != 0 && spellNode.Active)
                    player.AddOverrideSpell(spellNode.OverridesSpell, spellNode.Spell);
            }

            if (!IsPlayerLoading)
            {
                player.UpdateCriteria(CriteriaType.LearnOrKnowSpell, spellId);
            }

            // needs to be when spell is already learned, to prevent infinite recursion crashes
            if (Global.DB2Mgr.GetMount(spellId) != null)
                player.GetSession().GetCollectionMgr().AddMount(spellId, MountStatusFlags.None, false, !player.IsInWorld);

            // return true (for send learn packet) only if spell active (in case ranked spells) and not replace old spell
            return active && !disabled && !superceded_old;
        }

        public bool Has(int spellId)
        {
            var spell = SpellCollection.LookupByKey(spellId);
            if (spell != null)
                return spell.State != PlayerSpellState.Removed && !spell.Disabled;

            return false;
        }

        public bool HasActive(int spellId)
        {
            var spell = SpellCollection.LookupByKey(spellId);
            if (spell != null)
            {
                return spell.State != PlayerSpellState.Removed && spell.Active
                    && !spell.Disabled;
            }

            return false;
        }

        public PlayerSpell this[int spellId] => SpellCollection.LookupByKey(spellId);

        public IReadOnlyDictionary<int, PlayerSpell> Spells => SpellCollection;
        public IReadOnlyMultiMap<SkillType, int> TradeSkillSpells => TradeSkillSpellCollection;
        public IReadOnlyList<int> GetTradeSkillSpells(SkillType skill) => TradeSkillSpellCollection[skill];

        private Dictionary<int, PlayerSpell> SpellCollection = new();
        private MultiMap<SkillType, int> TradeSkillSpellCollection = new();
        private readonly Player player;
    }
}
