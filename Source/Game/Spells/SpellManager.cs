﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Framework.Dynamic;
using Game.BattleFields;
using Game.BattleGrounds;
using Game.BattlePets;
using Game.DataStorage;
using Game.Movement;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Game.Entities
{
    public sealed class SpellManager : Singleton<SpellManager>
    {
        SpellManager()
        {
            Assembly currentAsm = Assembly.GetExecutingAssembly();
            foreach (var type in currentAsm.GetTypes())
            {
                foreach (var methodInfo in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    foreach (var auraEffect in methodInfo.GetCustomAttributes<AuraEffectHandlerAttribute>())
                    {
                        if (auraEffect == null)
                            continue;

                        var parameters = methodInfo.GetParameters();
                        if (parameters.Length < 3)
                        {
                            Log.outError(LogFilter.ServerLoading, 
                                $"Method: {methodInfo.Name} " +
                                $"has wrong parameter count: {parameters.Length}. " +
                                $"Should be {3}. " +
                                $"Can't load AuraEffect.");
                            continue;
                        }

                        if (parameters[0].ParameterType != typeof(AuraApplication) 
                            || parameters[1].ParameterType != typeof(AuraEffectHandleModes) 
                            || parameters[2].ParameterType != typeof(bool))
                        {
                            Log.outError(LogFilter.ServerLoading, 
                                $"Method: {methodInfo.Name} " +
                                $"has wrong parameter Types: ({parameters[0].ParameterType}, {parameters[1].ParameterType}, {parameters[2].ParameterType}). " +
                                $"Should be (AuraApplication, AuraEffectHandleModes, Bool). " +
                                $"Can't load AuraEffect.");
                            continue;
                        }

                        if (AuraEffectHandlers.ContainsKey(auraEffect.AuraType))
                        {
                            Log.outError(LogFilter.ServerLoading, 
                                $"Tried to override AuraEffectHandler of {AuraEffectHandlers[auraEffect.AuraType].GetMethodInfo().Name} " +
                                $"with {methodInfo.Name} (AuraType {auraEffect.AuraType}).");
                            continue;
                        }

                        AuraEffectHandlers.Add(auraEffect.AuraType, (AuraEffectHandler)methodInfo.CreateDelegate(typeof(AuraEffectHandler)));

                    }

                    foreach (var spellEffect in methodInfo.GetCustomAttributes<SpellEffectHandlerAttribute>())
                    {
                        if (spellEffect == null)
                            continue;

                        if (SpellEffectsHandlers.ContainsKey(spellEffect.EffectName))
                        {
                            Log.outError(LogFilter.ServerLoading, 
                                $"Tried to override SpellEffectsHandler of {SpellEffectsHandlers[spellEffect.EffectName]} " +
                                $"with {methodInfo.Name} (EffectName {spellEffect.EffectName}).");
                            continue;
                        }

                        SpellEffectsHandlers.Add(spellEffect.EffectName, (SpellEffectHandler)methodInfo.CreateDelegate(typeof(SpellEffectHandler)));
                    }
                }
            }

            if (SpellEffectsHandlers.Count == 0)
            {
                Log.outFatal(LogFilter.ServerLoading, 
                    "Could'nt find any SpellEffectHandlers. Dev needs to check this out.");
                Environment.Exit(1);
            }
        }

        public bool IsSpellValid(int spellId, Player player = null, bool msg = true)
        {
            SpellInfo spellInfo = GetSpellInfo(spellId, Difficulty.None);
            return IsSpellValid(spellInfo, player, msg);
        }

        public bool IsSpellValid(SpellInfo spellInfo, Player player = null, bool msg = true)
        {
            // not exist
            if (spellInfo == null)
                return false;

            bool needCheckReagents = false;

            // check effects
            foreach (var spellEffectInfo in spellInfo.GetEffects())
            {
                switch (spellEffectInfo.Effect)
                {
                    case 0:
                        continue;

                    // craft spell for crafting non-existed item (break client recipes list show)
                    case SpellEffectName.CreateItem:
                    case SpellEffectName.CreateLoot:
                    {
                        if (spellEffectInfo.ItemType == 0)
                        {
                            // skip auto-loot crafting spells, its not need explicit item info (but have special fake items sometime)
                            if (!spellInfo.IsLootCrafting())
                            {
                                if (msg)
                                {
                                    string text = $"Craft spell {spellInfo.Id} not have create item entry.";
                                    if (player != null)
                                        player.SendSysMessage(text);
                                    else
                                        Log.outError(LogFilter.Spells, text);
                                }
                                return false;
                            }

                        }
                        // also possible IsLootCrafting case but fake item must exist anyway
                        else if (Global.ObjectMgr.GetItemTemplate(spellEffectInfo.ItemType) == null)
                        {
                            if (msg)
                            {
                                string text = $"Craft spell {spellInfo.Id} create not-exist in DB " +
                                    $"item (Entry: {spellEffectInfo.ItemType}) and then...";

                                if (player != null)
                                    player.SendSysMessage(text);                                
                                else                                
                                    Log.outError(LogFilter.Spells, text);                                
                            }
                            return false;
                        }

                        needCheckReagents = true;
                        break;
                    }
                    case SpellEffectName.LearnSpell:
                    {
                        SpellInfo spellInfo2 = GetSpellInfo(spellEffectInfo.TriggerSpell, Difficulty.None);
                        if (!IsSpellValid(spellInfo2, player, msg))
                        {
                            if (msg)
                            {
                                string text = $"Spell {spellInfo.Id} learn to invalid spell " +
                                    $"{spellEffectInfo.TriggerSpell}, and then...";

                                if (player != null)                                
                                    player.SendSysMessage(text);                                
                                else                                
                                    Log.outError(LogFilter.Spells, text);                                
                            }
                            return false;
                        }
                        break;
                    }
                }
            }

            if (needCheckReagents)
            {
                for (int j = 0; j < SpellConst.MaxReagents; ++j)
                {
                    if (spellInfo.Reagent[j] > 0 && Global.ObjectMgr.GetItemTemplate(spellInfo.Reagent[j]) == null)
                    {
                        if (msg)
                        {
                            string text = $"Craft spell {spellInfo.Id} have not-exist reagent in DB " +
                                $"item (Entry: {spellInfo.Reagent[j]}) and then..."; 

                            if (player != null)
                                player.SendSysMessage(text);
                            else
                                Log.outError(LogFilter.Spells, text);
                        }
                        return false;
                    }
                }
            }

            return true;
        }

        public SpellChainNode GetSpellChainNode(int spell_id)
        {
            return mSpellChains.LookupByKey(spell_id);
        }

        public int GetFirstSpellInChain(int spell_id)
        {
            var node = GetSpellChainNode(spell_id);
            if (node != null)
                return node.first.Id;

            return spell_id;
        }

        public int GetLastSpellInChain(int spell_id)
        {
            var node = GetSpellChainNode(spell_id);
            if (node != null)
                return node.last.Id;

            return spell_id;
        }

        public int GetNextSpellInChain(int spell_id)
        {
            var node = GetSpellChainNode(spell_id);
            if (node != null)
            {
                if (node.next != null)
                    return node.next.Id;
            }

            return 0;
        }

        public int GetPrevSpellInChain(int spell_id)
        {
            var node = GetSpellChainNode(spell_id);
            if (node != null)
            {
                if (node.prev != null)
                    return node.prev.Id;
            }

            return 0;
        }

        public byte GetSpellRank(int spell_id)
        {
            var node = GetSpellChainNode(spell_id);
            if (node != null)
                return node.rank;

            return 0;
        }

        public int GetSpellWithRank(int spell_id, int rank, bool strict = false)
        {
            var node = GetSpellChainNode(spell_id);
            if (node != null)
            {
                if (rank != node.rank)
                    return GetSpellWithRank(node.rank < rank ? node.next.Id : node.prev.Id, rank, strict);
            }
            else if (strict && rank > 1)
            {
                return 0;
            }

            return spell_id;
        }

        public IReadOnlyList<int> GetSpellsRequiredForSpellBounds(int spell_id)
        {
            return mSpellReq[spell_id];
        }

        public IReadOnlyList<int> GetSpellsRequiringSpellBounds(int spell_id)
        {
            return mSpellsReqSpell[spell_id];
        }

        public bool IsSpellRequiringSpell(int spellid, int req_spellid)
        {
            var spellsRequiringSpell = GetSpellsRequiringSpellBounds(req_spellid);

            foreach (var spell in spellsRequiringSpell)
            {
                if (spell == spellid)
                    return true;
            }
            return false;
        }

        public SpellLearnSkillNode GetSpellLearnSkill(int spell_id)
        {
            return mSpellLearnSkills.LookupByKey(spell_id);
        }

        public IReadOnlyList<SpellLearnSpellNode> GetSpellLearnSpellMapBounds(int spell_id)
        {
            return mSpellLearnSpells[spell_id];
        }

        bool IsSpellLearnSpell(int spell_id)
        {
            return mSpellLearnSpells.ContainsKey(spell_id);
        }

        public bool IsSpellLearnToSpell(int spell_id1, int spell_id2)
        {
            var bounds = GetSpellLearnSpellMapBounds(spell_id1);
            foreach (var bound in bounds)
            {
                if (bound.Spell == spell_id2)
                    return true;
            }

            return false;
        }

        public SpellTargetPosition GetSpellTargetPosition(int spell_id, int effIndex)
        {
            return mSpellTargetPositions.LookupByKey(new KeyValuePair<int, int>(spell_id, effIndex));
        }

        public IReadOnlyList<SpellGroup> GetSpellSpellGroupMapBounds(int spell_id)
        {
            return mSpellSpellGroup[GetFirstSpellInChain(spell_id)];
        }

        public bool IsSpellMemberOfSpellGroup(int spellid, SpellGroup groupid)
        {
            var spellGroup = GetSpellSpellGroupMapBounds(spellid);
            foreach (var group in spellGroup)
            {
                if (group == groupid)
                    return true;
            }
            return false;
        }

        IReadOnlyList<int> GetSpellGroupSpellMapBounds(SpellGroup group_id)
        {
            return mSpellGroupSpell[group_id];
        }

        public void GetSetOfSpellsInSpellGroup(SpellGroup group_id, out List<int> foundSpells)
        {
            List<SpellGroup> usedGroups = new();
            GetSetOfSpellsInSpellGroup(group_id, out foundSpells, ref usedGroups);
        }

        void GetSetOfSpellsInSpellGroup(SpellGroup group_id, out List<int> foundSpells, ref List<SpellGroup> usedGroups)
        {
            foundSpells = new List<int>();
            if (usedGroups.Find(p => p == group_id) == 0)
                return;

            usedGroups.Add(group_id);

            var groupSpell = GetSpellGroupSpellMapBounds(group_id);
            foreach (var group in groupSpell)
            {
                if (group < 0)
                {
                    SpellGroup currGroup = (SpellGroup)Math.Abs(group);
                    GetSetOfSpellsInSpellGroup(currGroup, out foundSpells, ref usedGroups);
                }
                else
                {
                    foundSpells.Add(group);
                }
            }
        }

        public bool AddSameEffectStackRuleSpellGroups(SpellInfo spellInfo, AuraType auraType, int amount, Dictionary<SpellGroup, int> groups)
        {
            int spellId = spellInfo.GetFirstRankSpell().Id;
            var spellGroupList = GetSpellSpellGroupMapBounds(spellId);
            // Find group with SPELL_GROUP_STACK_RULE_EXCLUSIVE_SAME_EFFECT if it belongs to one
            foreach (var group in spellGroupList)
            {
                var found = mSpellSameEffectStack[group];
                if (!found.Empty())
                {
                    // check auraTypes
                    if (!found.Any(p => p == auraType))
                        continue;

                    // Put the highest amount in the map
                    if (!groups.ContainsKey(group))
                        groups.Add(group, amount);
                    else
                    {
                        int curr_amount = groups[group];
                        // Take absolute value because this also counts for the highest negative aura
                        if (Math.Abs(curr_amount) < Math.Abs(amount))
                            groups[group] = amount;
                    }
                    // return because a spell should be in only one SPELL_GROUP_STACK_RULE_EXCLUSIVE_SAME_EFFECT group per auraType
                    return true;
                }
            }
            // Not in a SPELL_GROUP_STACK_RULE_EXCLUSIVE_SAME_EFFECT group, so return false
            return false;
        }

        public SpellGroupStackRule CheckSpellGroupStackRules(SpellInfo spellInfo1, SpellInfo spellInfo2)
        {
            int spellid_1 = spellInfo1.GetFirstRankSpell().Id;
            int spellid_2 = spellInfo2.GetFirstRankSpell().Id;

            // find SpellGroups which are common for both spells
            var spellGroup1 = GetSpellSpellGroupMapBounds(spellid_1);
            List<SpellGroup> groups = new();
            foreach (var group in spellGroup1)
            {
                if (IsSpellMemberOfSpellGroup(spellid_2, group))
                {
                    bool add = true;
                    var groupSpell = GetSpellGroupSpellMapBounds(group);
                    foreach (var group2 in groupSpell)
                    {
                        if (group2 < 0)
                        {
                            SpellGroup currGroup = (SpellGroup)Math.Abs(group2);
                            if (IsSpellMemberOfSpellGroup(spellid_1, currGroup) && IsSpellMemberOfSpellGroup(spellid_2, currGroup))
                            {
                                add = false;
                                break;
                            }
                        }
                    }
                    if (add)
                        groups.Add(group);
                }
            }

            SpellGroupStackRule rule = SpellGroupStackRule.Default;

            foreach (var group in groups)
            {
                var found = mSpellGroupStack.LookupByKey(group);
                if (found != 0)
                    rule = found;
                if (rule != 0)
                    break;
            }
            return rule;
        }

        public SpellGroupStackRule GetSpellGroupStackRule(SpellGroup group)
        {
            if (mSpellGroupStack.ContainsKey(group))
                return mSpellGroupStack.LookupByKey(group);

            return SpellGroupStackRule.Default;
        }

        public SpellProcEntry GetSpellProcEntry(SpellInfo spellInfo)
        {
            SpellProcEntry procEntry = mSpellProcMap.LookupByKey((spellInfo.Id, spellInfo.Difficulty));
            if (procEntry != null)
                return procEntry;

            DifficultyRecord difficulty = CliDB.DifficultyStorage.LookupByKey(spellInfo.Difficulty);
            if (difficulty != null)
            {
                do
                {
                    procEntry = mSpellProcMap.LookupByKey((spellInfo.Id, difficulty.FallbackDifficultyID));
                    if (procEntry != null)
                        return procEntry;

                    difficulty = CliDB.DifficultyStorage.LookupByKey(difficulty.FallbackDifficultyID);
                } while (difficulty != null);
            }

            return null;
        }

        public static bool CanSpellTriggerProcOnEvent(SpellProcEntry procEntry, ProcEventInfo eventInfo)
        {
            // proc Type doesn't match
            if (!eventInfo.GetTypeMask().HasAnyFlag(procEntry.ProcFlags))
                return false;

            // check XP or honor target requirement
            if (procEntry.AttributesMask.HasAnyFlag(ProcAttributes.ReqExpOrHonor))
            {
                Player actor = eventInfo.GetActor().ToPlayer();
                if (actor != null)
                {
                    if (eventInfo.GetActionTarget() != null && !actor.IsHonorOrXPTarget(eventInfo.GetActionTarget()))
                        return false;
                }
            }

            // check power requirement
            if (procEntry.AttributesMask.HasAnyFlag(ProcAttributes.ReqPowerCost))
            {
                if (eventInfo.GetProcSpell() == null)
                    return false;

                var costs = eventInfo.GetProcSpell().GetPowerCost();
                var m = costs.Find(cost => cost.Amount > 0);
                if (m == null)
                    return false;
            }

            // always trigger for these types
            if (eventInfo.GetTypeMask().HasAnyFlag(ProcFlags.Heartbeat | ProcFlags.Kill | ProcFlags.Death))
                return true;

            // check school mask (if set) for other trigger types
            if (procEntry.SchoolMask != SpellSchoolMask.None && !procEntry.SchoolMask.HasAnyFlag(eventInfo.GetSchoolMask()))
                return false;

            // check spell family name/flags (if set) for spells
            if (eventInfo.GetTypeMask().HasAnyFlag(ProcFlags.SpellMask))
            {
                SpellInfo eventSpellInfo = eventInfo.GetSpellInfo();
                if (eventSpellInfo != null)
                {
                    if (!eventSpellInfo.IsAffected(procEntry.SpellFamilyName, procEntry.SpellFamilyMask))
                        return false;
                }

                // check spell Type mask (if set)
                if (procEntry.SpellTypeMask != ProcFlagsSpellType.None && !procEntry.SpellTypeMask.HasAnyFlag(eventInfo.GetSpellTypeMask()))
                    return false;
            }

            // check spell phase mask
            if (eventInfo.GetTypeMask().HasAnyFlag(ProcFlags.ReqSpellPhaseMask))
            {
                if (!procEntry.SpellPhaseMask.HasAnyFlag(eventInfo.GetSpellPhaseMask()))
                    return false;
            }

            // check hit mask (on taken hit or on done hit, but not on spell cast phase)
            if (eventInfo.GetTypeMask().HasAnyFlag(ProcFlags.TakenHitMask | ProcFlags.DoneHitMask)
                && !eventInfo.GetSpellPhaseMask().HasAnyFlag(ProcFlagsSpellPhase.Cast))
            {
                ProcFlagsHit hitMask = procEntry.HitMask;
                // get default values if hit mask not set
                if (hitMask == 0)
                {
                    // for taken procs allow normal + critical hits by default
                    if (eventInfo.GetTypeMask().HasAnyFlag(ProcFlags.TakenHitMask))
                        hitMask |= ProcFlagsHit.Normal | ProcFlagsHit.Critical;
                    // for done procs allow normal + critical + absorbs by default
                    else
                        hitMask |= ProcFlagsHit.Normal | ProcFlagsHit.Critical | ProcFlagsHit.Absorb;
                }
                if (!Convert.ToBoolean(eventInfo.GetHitMask() & hitMask))
                    return false;
            }

            return true;
        }

        public SpellThreatEntry GetSpellThreatEntry(int spellID)
        {
            var spellthreat = mSpellThreatMap.LookupByKey(spellID);
            if (spellthreat != null)
                return spellthreat;
            else
            {
                int firstSpell = GetFirstSpellInChain(spellID);
                return mSpellThreatMap.LookupByKey(firstSpell);
            }
        }

        public IReadOnlyList<SkillLineAbilityRecord> GetSkillLineAbilityMapBounds(int spell_id)
        {
            return mSkillLineAbilityMap[spell_id];
        }

        public PetAura GetPetAura(int spell_id, byte eff)
        {
            return mSpellPetAuraMap.LookupByKey((spell_id << 8) + eff);
        }

        public SpellEnchantProcEntry GetSpellEnchantProcEvent(int enchId)
        {
            return mSpellEnchantProcEventMap.LookupByKey(enchId);
        }

        public bool IsArenaAllowedEnchancment(int ench_id)
        {
            var enchantment = CliDB.SpellItemEnchantmentStorage.LookupByKey(ench_id);
            if (enchantment != null)
                return enchantment.HasFlag(SpellItemEnchantmentFlags.AllowEnteringArena);

            return false;
        }

        public IReadOnlyList<int> GetSpellLinked(SpellLinkedType type, int spellId)
        {
            return mSpellLinkedMap[(type, spellId)];
        }

        public MultiMap<int, int> GetPetLevelupSpellList(CreatureFamily petFamily)
        {
            return mPetLevelupSpellMap.LookupByKey(petFamily);
        }

        public PetDefaultSpellsEntry GetPetDefaultSpellsEntry(int id)
        {
            return mPetDefaultSpellsMap.LookupByKey(id);
        }

        public IReadOnlyList<SpellArea> GetSpellAreaMapBounds(int spell_id)
        {
            return mSpellAreaMap[spell_id];
        }

        public IReadOnlyList<SpellArea> GetSpellAreaForQuestMapBounds(int quest_id)
        {
            return mSpellAreaForQuestMap[quest_id];
        }

        public IReadOnlyList<SpellArea> GetSpellAreaForQuestEndMapBounds(int quest_id)
        {
            return mSpellAreaForQuestEndMap[quest_id];
        }

        public IReadOnlyList<SpellArea> GetSpellAreaForAuraMapBounds(int spell_id)
        {
            return mSpellAreaForAuraMap[spell_id];
        }

        public IReadOnlyList<SpellArea> GetSpellAreaForAreaMapBounds(int area_id)
        {
            return mSpellAreaForAreaMap[area_id];
        }

        public SpellInfo GetSpellInfo(int spellId, Difficulty difficulty)
        {
            var list = mSpellInfoMap[spellId];

            if (list.TryFind(out SpellInfo result, out _, spellInfo => spellInfo.Difficulty == difficulty))
                return result;

            DifficultyRecord difficultyEntry = CliDB.DifficultyStorage.LookupByKey(difficulty);
            if (difficultyEntry != null)
            {
                do
                {
                    if (list.TryFind(out result, out _, spellInfo => spellInfo.Difficulty == difficultyEntry.FallbackDifficultyID))
                        return result;

                    difficultyEntry = CliDB.DifficultyStorage.LookupByKey(difficultyEntry.FallbackDifficultyID);
                } while (difficultyEntry != null);
            }

            return null;
        }

        IReadOnlyList<SpellInfo> _GetSpellInfo(int spellId)
        {
            return mSpellInfoMap[spellId];
        }

        public void ForEachSpellInfo(Action<SpellInfo> callback)
        {
            foreach (SpellInfo spellInfo in mSpellInfoMap.Values)
                callback(spellInfo);
        }

        public void ForEachSpellInfoDifficulty(int spellId, Action<SpellInfo> callback)
        {
            foreach (SpellInfo spellInfo in _GetSpellInfo(spellId))
                callback(spellInfo);
        }

        void UnloadSpellInfoChains()
        {
            foreach (var pair in mSpellChains)
            {
                foreach (SpellInfo spellInfo in _GetSpellInfo(pair.Key))
                    spellInfo.ChainEntry = null;
            }

            mSpellChains.Clear();
        }

        #region Loads
        public void LoadSpellRanks()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            Dictionary<int /*spell*/, int /*next*/> chains = new();
            List<int> hasPrev = new();
            foreach (SkillLineAbilityRecord skillAbility in CliDB.SkillLineAbilityStorage.Values)
            {
                if (skillAbility.SupercedesSpell == 0)
                    continue;

                if (!HasSpellInfo(skillAbility.SupercedesSpell, Difficulty.None) || !HasSpellInfo(skillAbility.Spell, Difficulty.None))
                    continue;

                chains[skillAbility.SupercedesSpell] = skillAbility.Spell;
                hasPrev.Add(skillAbility.Spell);
            }

            // each key in chains that isn't present in hasPrev is a first rank
            foreach (var pair in chains)
            {
                if (hasPrev.Contains(pair.Key))
                    continue;

                SpellInfo first = GetSpellInfo(pair.Key, Difficulty.None);
                SpellInfo next = GetSpellInfo(pair.Value, Difficulty.None);

                if (!mSpellChains.ContainsKey(pair.Key))
                    mSpellChains[pair.Key] = new SpellChainNode();

                mSpellChains[pair.Key].first = first;
                mSpellChains[pair.Key].prev = null;
                mSpellChains[pair.Key].next = next;
                mSpellChains[pair.Key].last = next;
                mSpellChains[pair.Key].rank = 1;
                foreach (SpellInfo difficultyInfo in _GetSpellInfo(pair.Key))
                    difficultyInfo.ChainEntry = mSpellChains[pair.Key];

                if (!mSpellChains.ContainsKey(pair.Value))
                    mSpellChains[pair.Value] = new SpellChainNode();

                mSpellChains[pair.Value].first = first;
                mSpellChains[pair.Value].prev = first;
                mSpellChains[pair.Value].next = null;
                mSpellChains[pair.Value].last = next;
                mSpellChains[pair.Value].rank = 2;
                foreach (SpellInfo difficultyInfo in _GetSpellInfo(pair.Value))
                    difficultyInfo.ChainEntry = mSpellChains[pair.Value];

                byte rank = 3;
                var nextPair = chains.Find(pair.Value);
                while (nextPair.Key != 0)
                {
                    SpellInfo prev = GetSpellInfo(nextPair.Key, Difficulty.None); // already checked in previous iteration (or above, in case this is the first one)
                    SpellInfo last = GetSpellInfo(nextPair.Value, Difficulty.None);
                    if (last == null)
                        break;

                    if (!mSpellChains.ContainsKey(nextPair.Key))
                        mSpellChains[nextPair.Key] = new SpellChainNode();

                    mSpellChains[nextPair.Key].next = last;

                    if (!mSpellChains.ContainsKey(nextPair.Value))
                        mSpellChains[nextPair.Value] = new SpellChainNode();

                    mSpellChains[nextPair.Value].first = first;
                    mSpellChains[nextPair.Value].prev = prev;
                    mSpellChains[nextPair.Value].next = null;
                    mSpellChains[nextPair.Value].last = last;
                    mSpellChains[nextPair.Value].rank = rank++;
                    foreach (SpellInfo difficultyInfo in _GetSpellInfo(nextPair.Value))
                        difficultyInfo.ChainEntry = mSpellChains[nextPair.Value];

                    // fill 'last'
                    do
                    {
                        mSpellChains[prev.Id].last = last;
                        prev = mSpellChains[prev.Id].prev;
                    } while (prev != null);

                    nextPair = chains.Find(nextPair.Value);
                }
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {mSpellChains.Count} spell rank records in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellRequired()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellsReqSpell.Clear();                                   // need for reload case
            mSpellReq.Clear();                                         // need for reload case

            //                                                   0        1
            SQLResult result = DB.World.Query("SELECT spell_id, req_spell from spell_required");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spell required records. DB table `spell_required` is empty.");

                return;
            }

            uint count = 0;
            do
            {
                int spell_id = result.Read<int>(0);
                int spell_req = result.Read<int>(1);

                // check if chain is made with valid first spell
                SpellInfo spell = GetSpellInfo(spell_id, Difficulty.None);
                if (spell == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"spell_id {spell_id} in `spell_required` table is not found in dbcs, skipped");
                    continue;
                }

                SpellInfo req_spell = GetSpellInfo(spell_req, Difficulty.None);
                if (req_spell == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"req_spell {spell_req} in `spell_required` table is not found in dbcs, skipped");
                    continue;
                }

                if (spell.IsRankOf(req_spell))
                {
                    Log.outError(LogFilter.Sql, 
                        $"req_spell {spell_req} and spell_id {spell_id} in `spell_required` table are ranks of the same spell, " +
                        $"entry not needed, skipped");
                    continue;
                }

                if (IsSpellRequiringSpell(spell_id, spell_req))
                {
                    Log.outError(LogFilter.Sql, 
                        $"duplicated entry of req_spell {spell_req} and spell_id {spell_id} in `spell_required`, skipped");
                    continue;
                }

                mSpellReq.Add(spell_id, spell_req);
                mSpellsReqSpell.Add(spell_req, spell_id);
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} spell required records in {Time.Diff(oldMSTime)} ms.");

        }

        public void LoadSpellLearnSkills()
        {
            mSpellLearnSkills.Clear();

            // search auto-learned skills and add its to map also for use in unlearn spells/talents
            uint dbc_count = 0;
            foreach (var entry in mSpellInfoMap.Values)
            {
                if (entry.Difficulty != Difficulty.None)
                    continue;

                foreach (var spellEffectInfo in entry.GetEffects())
                {
                    SpellLearnSkillNode dbc_node = new();
                    switch (spellEffectInfo.Effect)
                    {
                        case SpellEffectName.Skill:
                            dbc_node.skill = (SkillType)spellEffectInfo.MiscValue;
                            dbc_node.step = (ushort)spellEffectInfo.CalcValue();
                            dbc_node.value = 0;
                            dbc_node.maxvalue = 0;
                            break;
                        case SpellEffectName.DualWield:
                            dbc_node.skill = SkillType.DualWield;
                            dbc_node.step = 1;
                            dbc_node.value = 1;
                            dbc_node.maxvalue = 1;
                            break;
                        default:
                            continue;
                    }

                    mSpellLearnSkills.Add(entry.Id, dbc_node);
                    ++dbc_count;
                    break;
                }
            }
            Log.outInfo(LogFilter.ServerLoading, $"Loaded {dbc_count} Spell Learn Skills from DBC.");
        }

        public void LoadSpellLearnSpells()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellLearnSpells.Clear();

            //                                         0      1        2
            SQLResult result = DB.World.Query("SELECT entry, SpellID, Active FROM spell_learn_spell");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spell learn spells. DB table `spell_learn_spell` is empty.");
                return;
            }
            uint count = 0;
            do
            {
                int spell_id = result.Read<int>(0);

                var node = new SpellLearnSpellNode();
                node.Spell = result.Read<int>(1);
                node.OverridesSpell = 0;
                node.Active = result.Read<bool>(2);
                node.AutoLearned = false;

                SpellInfo spellInfo = GetSpellInfo(spell_id, Difficulty.None);
                if (spellInfo == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell {spell_id} listed in `spell_learn_spell` does not exist");
                    continue;
                }

                if (!HasSpellInfo(node.Spell, Difficulty.None))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell {spell_id} listed in `spell_learn_spell` learning not existed spell {node.Spell}");
                    continue;
                }

                if (spellInfo.HasAttribute(SpellCustomAttributes.IsTalent))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell {spell_id} listed in `spell_learn_spell` attempt learning talent spell {node.Spell}, skipped");
                    continue;
                }

                mSpellLearnSpells.Add(spell_id, node);
                ++count;
            } while (result.NextRow());

            // search auto-learned spells and add its to map also for use in unlearn spells/talents
            uint dbc_count = 0;
            foreach (var entry in mSpellInfoMap.Values)
            {
                if (entry.Difficulty != Difficulty.None)
                    continue;

                foreach (var spellEffectInfo in entry.GetEffects())
                {
                    if (spellEffectInfo.Effect == SpellEffectName.LearnSpell)
                    {
                        var dbc_node = new SpellLearnSpellNode();
                        dbc_node.Spell = spellEffectInfo.TriggerSpell;
                        dbc_node.Active = true;                     // all dbc based learned spells is active (show in spell book or hide by client itself)
                        dbc_node.OverridesSpell = 0;

                        // ignore learning not existed spells (broken/outdated/or generic learnig spell 483
                        if (GetSpellInfo(dbc_node.Spell, Difficulty.None) == null)
                            continue;

                        // talent or passive spells or skill-step spells auto-cast and not need dependent learning,
                        // pet teaching spells must not be dependent learning (cast)
                        // other required explicit dependent learning
                        dbc_node.AutoLearned = spellEffectInfo.TargetA.GetTarget() == Targets.UnitPet || entry.HasAttribute(SpellCustomAttributes.IsTalent) || entry.IsPassive() || entry.HasEffect(SpellEffectName.SkillStep);

                        var db_node_bounds = GetSpellLearnSpellMapBounds(entry.Id);

                        bool found = false;
                        foreach (var bound in db_node_bounds)
                        {
                            if (bound.Spell == dbc_node.Spell)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Spell {entry.Id} auto-learn spell {dbc_node.Spell} in spell.dbc, " +
                                    $"then the record in `spell_learn_spell` is redundant, please fix DB.");
                                found = true;
                                break;
                            }
                        }

                        if (!found)                                  // add new spell-spell pair if not found
                        {
                            mSpellLearnSpells.Add(entry.Id, dbc_node);
                            ++dbc_count;
                        }
                    }
                }
            }

            foreach (var spellLearnSpell in CliDB.SpellLearnSpellStorage.Values)
            {
                if (!HasSpellInfo(spellLearnSpell.SpellID, Difficulty.None)
                    || !HasSpellInfo(spellLearnSpell.LearnSpellID, Difficulty.None))
                {
                    continue;
                }

                var db_node_bounds = mSpellLearnSpells[spellLearnSpell.SpellID];
                bool found = false;
                foreach (var spellNode in db_node_bounds)
                {
                    if (spellNode.Spell == spellLearnSpell.LearnSpellID)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Found redundant record (entry: {spellLearnSpell.SpellID}, " +
                            $"SpellID: {spellLearnSpell.LearnSpellID}) in `spell_learn_spell`, " +
                            $"spell added automatically from SpellLearnSpell.db2");

                        found = true;
                        break;
                    }
                }

                if (found)
                    continue;

                // Check if it is already found in Spell.dbc, ignore silently if yes
                var dbc_node_bounds = GetSpellLearnSpellMapBounds(spellLearnSpell.SpellID);
                found = false;
                foreach (var spellNode in dbc_node_bounds)
                {
                    if (spellNode.Spell == spellLearnSpell.LearnSpellID)
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                    continue;

                SpellLearnSpellNode dbcLearnNode = new();
                dbcLearnNode.Spell = spellLearnSpell.LearnSpellID;
                dbcLearnNode.OverridesSpell = spellLearnSpell.OverridesSpellID;
                dbcLearnNode.Active = true;
                dbcLearnNode.AutoLearned = false;

                mSpellLearnSpells.Add(spellLearnSpell.SpellID, dbcLearnNode);
                ++dbc_count;
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {count} spell learn spells, {dbc_count} found in Spell.dbc in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellTargetPositions()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellTargetPositions.Clear();                                // need for reload case

            //                                        0   1            2      3          4          5          6
            SQLResult result = DB.World.Query("SELECT ID, EffectIndex, MapID, PositionX, PositionY, PositionZ, Orientation FROM spell_target_position");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 spell target coordinates. DB table `spell_target_position` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int spellId = result.Read<int>(0);
                int effIndex = result.Read<byte>(1);

                SpellTargetPosition st = new();
                st.target_mapId = result.Read<int>(2);
                st.target_X = result.Read<float>(3);
                st.target_Y = result.Read<float>(4);
                st.target_Z = result.Read<float>(5);

                var mapEntry = CliDB.MapStorage.LookupByKey(st.target_mapId);
                if (mapEntry == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell (ID: {spellId}, EffectIndex: {effIndex}) is using a non-existant MapID (ID: {st.target_mapId})");
                    continue;
                }

                if (st.target_X == 0 && st.target_Y == 0 && st.target_Z == 0)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell (ID: {spellId}, EffectIndex: {effIndex}) target coordinates not provided.");
                    continue;
                }

                SpellInfo spellInfo = GetSpellInfo(spellId, Difficulty.None);
                if (spellInfo == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell (ID: {spellId}) listed in `spell_target_position` does not exist.");
                    continue;
                }

                if (effIndex >= spellInfo.GetEffects().Count)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell (Id: {spellId}, effIndex: {effIndex}) listed in `spell_target_position` " +
                        $"does not have an effect at index {effIndex}.");
                    continue;
                }

                if (!result.IsNull(6))
                    st.target_Orientation = result.Read<float>(6);
                else
                {
                    // target facing is in degrees for 6484 & 9268... (blizz sucks)
                    if (spellInfo.GetEffect(effIndex).PositionFacing > 2 * MathF.PI)
                        st.target_Orientation = spellInfo.GetEffect(effIndex).PositionFacing * MathF.PI / 180;
                    else
                        st.target_Orientation = spellInfo.GetEffect(effIndex).PositionFacing;
                }

                bool hasTarget(Targets target)
                {
                    SpellEffectInfo spellEffectInfo = spellInfo.GetEffect(effIndex);
                    return spellEffectInfo.TargetA.GetTarget() == target || spellEffectInfo.TargetB.GetTarget() == target;
                }

                if (hasTarget(Targets.DestDb) || hasTarget(Targets.DestNearbyEntryOrDB))
                {
                    var key = new KeyValuePair<int, int>(spellId, effIndex);
                    mSpellTargetPositions[key] = st;
                    ++count;
                }
                else
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell (Id: {spellId}, effIndex: {effIndex}) listed in `spell_target_position` " +
                        $"does not have target TARGET_DEST_DB ({(int)Targets.DestDb}).");
                    continue;
                }

            }
            while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} spell teleport coordinates in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellGroups()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellSpellGroup.Clear();                                  // need for reload case
            mSpellGroupSpell.Clear();

            //                                         0     1
            SQLResult result = DB.World.Query("SELECT id, spell_id FROM spell_group");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 spell group definitions. DB table `spell_group` is empty.");
                return;
            }

            List<int> groups = new();
            uint count = 0;
            do
            {
                int group_id = result.Read<int>(0);
                if (group_id <= 1000 && group_id >= (int)SpellGroup.CoreRangeMax)
                {
                    Log.outError(LogFilter.Sql, 
                        $"SpellGroup id {group_id} listed in `spell_group` is in core range, but is not defined in core!");
                    continue;
                }
                int spell_id = result.Read<int>(1);

                groups.Add(group_id);
                mSpellGroupSpell.Add((SpellGroup)group_id, spell_id);

            } while (result.NextRow());

            foreach (var group in mSpellGroupSpell.ToList())
            {
                if (group.Value < 0)
                {
                    if (!groups.Contains(Math.Abs(group.Value)))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"SpellGroup id {Math.Abs(group.Value)} listed in `spell_group` does not exist");

                        mSpellGroupSpell.Remove(group.Key);
                    }
                }
                else
                {
                    SpellInfo spellInfo = GetSpellInfo(group.Value, Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spell {group.Value} listed in `spell_group` does not exist");

                        mSpellGroupSpell.Remove(group.Key);
                    }
                    else if (spellInfo.GetRank() > 1)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spell {group.Value} listed in `spell_group` is not first rank of spell");

                        mSpellGroupSpell.Remove(group.Key);
                    }
                }
            }

            foreach (var group in groups)
            {
                List<int> spells;
                GetSetOfSpellsInSpellGroup((SpellGroup)group, out spells);

                foreach (var spell in spells)
                {
                    ++count;
                    mSpellSpellGroup.Add(spell, (SpellGroup)group);
                }
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} spell group definitions in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellGroupStackRules()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellGroupStack.Clear();                                  // need for reload case
            mSpellSameEffectStack.Clear();

            List<SpellGroup> sameEffectGroups = new();

            //                                         0         1
            SQLResult result = DB.World.Query("SELECT group_id, stack_rule FROM spell_group_stack_rules");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 spell group stack rules. DB table `spell_group_stack_rules` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                SpellGroup group_id = (SpellGroup)result.Read<uint>(0);
                SpellGroupStackRule stack_rule = (SpellGroupStackRule)result.Read<byte>(1);
                if (stack_rule >= SpellGroupStackRule.Max)
                {
                    Log.outError(LogFilter.Sql, 
                        $"SpellGroupStackRule {stack_rule} listed in `spell_group_stack_rules` does not exist");
                    continue;
                }

                var spellGroup = GetSpellGroupSpellMapBounds(group_id);
                if (spellGroup == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"SpellGroup id {group_id} listed in `spell_group_stack_rules` does not exist");
                    continue;
                }

                mSpellGroupStack.Add(group_id, stack_rule);

                // different container for same effect stack rules, need to check effect types
                if (stack_rule == SpellGroupStackRule.ExclusiveSameEffect)
                    sameEffectGroups.Add(group_id);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} spell group stack rules in {Time.Diff(oldMSTime)} ms.");

            count = 0;
            oldMSTime = Time.NowRelative;
            Log.outInfo(LogFilter.ServerLoading, "Parsing SPELL_GROUP_STACK_RULE_EXCLUSIVE_SAME_EFFECT stack rules...");

            foreach (SpellGroup group_id in sameEffectGroups)
            {
                GetSetOfSpellsInSpellGroup(group_id, out var spellIds);

                List<AuraType> auraTypes = new();

                // we have to 'guess' what effect this group corresponds to
                {
                    List<AuraType> frequencyContainer = new();

                    // only waylay for the moment (shared group)
                    AuraType[] SubGroups =
                    [
                        AuraType.ModMeleeHaste,
                        AuraType.ModMeleeRangedHaste,
                        AuraType.ModRangedHaste
                    ];

                    foreach (var spellId in spellIds)
                    {
                        SpellInfo spellInfo = GetSpellInfo(spellId, Difficulty.None);
                        foreach (var spellEffectInfo in spellInfo.GetEffects())
                        {
                            if (!spellEffectInfo.IsAura())
                                continue;

                            AuraType auraName = spellEffectInfo.ApplyAuraName;
                            if (SubGroups.Contains(auraName))
                            {
                                // count as first aura
                                auraName = SubGroups[0];
                            }

                            frequencyContainer.Add(auraName);
                        }
                    }

                    AuraType auraType = 0;
                    int auraTypeCount = 0;
                    foreach (AuraType auraName in frequencyContainer)
                    {
                        int currentCount = frequencyContainer.Count(p => p == auraName);
                        if (currentCount > auraTypeCount)
                        {
                            auraType = auraName;
                            auraTypeCount = currentCount;
                        }
                    }

                    if (auraType == SubGroups[0])
                    {
                        auraTypes.AddRange(SubGroups);
                        break;
                    }

                    if (auraTypes.Empty())
                        auraTypes.Add(auraType);
                }

                // re-check spells against guessed group
                foreach (var spellId in spellIds)
                {
                    SpellInfo spellInfo = GetSpellInfo(spellId, Difficulty.None);

                    bool found = false;
                    while (spellInfo != null)
                    {
                        foreach (AuraType auraType in auraTypes)
                        {
                            if (spellInfo.HasAura(auraType))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                            break;

                        spellInfo = spellInfo.GetNextRankSpell();
                    }

                    // not found either, log error
                    if (!found)
                    {
                        Log.outError(LogFilter.Sql,
                            $"SpellId {spellId} listed in `spell_group` with stack rule '3' " +
                            $"does not share aura assigned for group {group_id}");
                    }
                }

                mSpellSameEffectStack.SetValues(group_id, auraTypes);
                ++count;
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Parsed {count} SPELL_GROUP_STACK_RULE_EXCLUSIVE_SAME_EFFECT stack rules in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellProcs()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellProcMap.Clear();                             // need for reload case

            //                                         0        1           2                3                 4                 5                 6
            SQLResult result = DB.World.Query("SELECT SpellId, SchoolMask, SpellFamilyName, SpellFamilyMask0, SpellFamilyMask1, SpellFamilyMask2, SpellFamilyMask3, " +
                //7          8           9              10              11       12              13                  14              15      16        17
                "ProcFlags, ProcFlags2, SpellTypeMask, SpellPhaseMask, HitMask, AttributesMask, DisableEffectsMask, ProcsPerMinute, Chance, Cooldown, Charges FROM spell_proc");

            uint count = 0;
            if (!result.IsEmpty())
            {
                do
                {
                    int spellId = result.Read<int>(0);

                    bool allRanks = false;
                    if (spellId < 0)
                    {
                        allRanks = true;
                        spellId = -spellId;
                    }

                    SpellInfo spellInfo = GetSpellInfo(spellId, Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"Spell {spellId} listed in `spell_proc` does not exist");
                        continue;
                    }

                    if (allRanks)
                    {
                        if (spellInfo.GetFirstRankSpell().Id != spellId)
                        {
                            Log.outError(LogFilter.Sql,
                                $"Spell {spellId} listed in `spell_proc` is not first rank of spell.");
                            continue;
                        }
                    }

                    SpellProcEntry baseProcEntry = new();

                    baseProcEntry.SchoolMask = (SpellSchoolMask)result.Read<uint>(1);
                    baseProcEntry.SpellFamilyName = (SpellFamilyNames)result.Read<uint>(2);
                    baseProcEntry.SpellFamilyMask = new FlagArray128(result.Read<uint>(3), result.Read<uint>(4), result.Read<uint>(5), result.Read<uint>(6));
                    baseProcEntry.ProcFlags = new ProcFlagsInit((ProcFlags)result.Read<int>(7), (ProcFlags2)result.Read<int>(8));
                    baseProcEntry.SpellTypeMask = (ProcFlagsSpellType)result.Read<uint>(9);
                    baseProcEntry.SpellPhaseMask = (ProcFlagsSpellPhase)result.Read<uint>(10);
                    baseProcEntry.HitMask = (ProcFlagsHit)result.Read<uint>(11);
                    baseProcEntry.AttributesMask = (ProcAttributes)result.Read<uint>(12);
                    baseProcEntry.DisableEffectsMask = result.Read<uint>(13);
                    baseProcEntry.ProcsPerMinute = result.Read<float>(14);
                    baseProcEntry.Chance = result.Read<float>(15);
                    baseProcEntry.Cooldown = (Milliseconds)result.Read<int>(16);
                    baseProcEntry.Charges = result.Read<int>(17);

                    while (spellInfo != null)
                    {
                        if (mSpellProcMap.ContainsKey((spellInfo.Id, spellInfo.Difficulty)))
                        {
                            Log.outError(LogFilter.Sql,
                                $"Spell {spellInfo.Id} listed in `spell_proc` " +
                                $"has duplicate entry in the table");
                            break;
                        }
                        SpellProcEntry procEntry = baseProcEntry;

                        // take defaults from dbcs
                        if (procEntry.ProcFlags == ProcFlagsInit.None)
                            procEntry.ProcFlags = spellInfo.ProcFlags;
                        if (procEntry.Charges == 0)
                            procEntry.Charges = spellInfo.ProcCharges;
                        if (procEntry.Chance == 0 && procEntry.ProcsPerMinute == 0)
                            procEntry.Chance = spellInfo.ProcChance;
                        if (procEntry.Cooldown == 0)
                            procEntry.Cooldown = spellInfo.ProcCooldown;

                        // validate data
                        if (procEntry.SchoolMask.HasAnyFlag(~SpellSchoolMask.All))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has wrong `SchoolMask` set: {procEntry.SchoolMask}");
                        }

                        if (procEntry.SpellFamilyName != 0 && !Global.DB2Mgr.IsValidSpellFamiliyName(procEntry.SpellFamilyName))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has wrong `SpellFamilyName` set: {procEntry.SpellFamilyName}");
                        }

                        if (procEntry.Chance < 0)
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has negative value in `Chance` field");
                            procEntry.Chance = 0;
                        }

                        if (procEntry.ProcsPerMinute < 0)
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has negative value in `ProcsPerMinute` field");
                            procEntry.ProcsPerMinute = 0;
                        }

                        if (procEntry.ProcFlags == ProcFlagsInit.None)
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"doesn't have `ProcFlags` value defined, proc will not be triggered");
                        }

                        if (procEntry.SpellTypeMask.HasAnyFlag(~ProcFlagsSpellType.MaskAll))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has wrong `SpellTypeMask` set: {procEntry.SpellTypeMask}");
                        }

                        if (procEntry.SpellTypeMask != 0 && !procEntry.ProcFlags.HasAnyFlag(ProcFlags.SpellMask))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has `SpellTypeMask` value defined, but it won't be used for defined `ProcFlags` value");
                        }

                        if (procEntry.SpellPhaseMask == 0 && procEntry.ProcFlags.HasAnyFlag(ProcFlags.ReqSpellPhaseMask))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"doesn't have `SpellPhaseMask` value defined, but it's required for defined `ProcFlags` value, " +
                                $"proc will not be triggered");
                        }

                        if (procEntry.SpellPhaseMask.HasAnyFlag(~ProcFlagsSpellPhase.MaskAll))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has wrong `SpellPhaseMask` set: {procEntry.SpellPhaseMask}");
                        }

                        if (procEntry.SpellPhaseMask != 0 && !procEntry.ProcFlags.HasAnyFlag(ProcFlags.ReqSpellPhaseMask))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has `SpellPhaseMask` value defined, but it won't be used for defined `ProcFlags` value");
                        }

                        if (procEntry.SpellPhaseMask == 0 && !procEntry.ProcFlags.HasAnyFlag(ProcFlags.ReqSpellPhaseMask) 
                            && procEntry.ProcFlags.HasAnyFlag(ProcFlags2.CastSuccessful))
                        {
                            procEntry.SpellPhaseMask = ProcFlagsSpellPhase.Cast; // set default phase for PROC_FLAG_2_CAST_SUCCESSFUL
                        }

                        if (procEntry.HitMask.HasAnyFlag(~ProcFlagsHit.MaskAll))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has wrong `HitMask` set: {procEntry.HitMask}");
                        }

                        if (procEntry.HitMask != 0
                            && !(procEntry.ProcFlags.HasAnyFlag(ProcFlags.TakenHitMask) || (procEntry.ProcFlags.HasAnyFlag(ProcFlags.DoneHitMask) && (procEntry.SpellPhaseMask == 0 || procEntry.SpellPhaseMask.HasAnyFlag(ProcFlagsSpellPhase.Hit | ProcFlagsSpellPhase.Finish)))))
                        {
                            Log.outError(LogFilter.Sql,
                                $"`spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has `HitMask` value defined, but it won't be used " +
                                $"for defined `ProcFlags` and `SpellPhaseMask` values");
                        }

                        foreach (var spellEffectInfo in spellInfo.GetEffects())
                        {
                            if ((procEntry.DisableEffectsMask & (1u << spellEffectInfo.EffectIndex)) != 0 && !spellEffectInfo.IsAura())
                            {
                                Log.outError(LogFilter.Sql,
                                    $"The `spell_proc` table entry for spellId {spellInfo.Id} " +
                                    $"has DisableEffectsMask with effect {spellEffectInfo.EffectIndex}, " +
                                    $"but effect {spellEffectInfo.EffectIndex} is not an aura effect");
                            }
                        }

                        if (procEntry.AttributesMask.HasFlag(ProcAttributes.ReqSpellmod))
                        {
                            bool found = false;
                            foreach (var spellEffectInfo in spellInfo.GetEffects())
                            {
                                if (!spellEffectInfo.IsAura())
                                    continue;

                                if (spellEffectInfo.ApplyAuraName == AuraType.AddPctModifier || spellEffectInfo.ApplyAuraName == AuraType.AddFlatModifier
                                    || spellEffectInfo.ApplyAuraName == AuraType.IgnoreSpellCooldown)
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                Log.outError(LogFilter.Sql,
                                    $"The `spell_proc` table entry for spellId {spellInfo.Id} " +
                                    $"has Attribute PROC_ATTR_REQ_SPELLMOD, " +
                                    $"but spell has no spell mods. Proc will not be triggered");
                            }
                        }

                        if ((procEntry.AttributesMask & ~ProcAttributes.AllAllowed) != 0)
                        {
                            Log.outError(LogFilter.Sql,
                                $"The `spell_proc` table entry for spellId {spellInfo.Id} " +
                                $"has `AttributesMask` value specifying invalid attributes " +
                                $"0x{(procEntry.AttributesMask & ~ProcAttributes.AllAllowed):X}.");

                            procEntry.AttributesMask &= ProcAttributes.AllAllowed;
                        }

                        mSpellProcMap.Add((spellInfo.Id, spellInfo.Difficulty), procEntry);
                        ++count;

                        if (allRanks)
                            spellInfo = spellInfo.GetNextRankSpell();
                        else
                            break;
                    }
                } while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading,
                    $"Loaded {count} spell proc conditions and data in {Time.Diff(oldMSTime)} ms.");
            }
            else
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    $"Loaded 0 spell proc conditions and data. DB table `spell_proc` is empty.");
            }

            // This generates default procs to retain compatibility with previous proc system
            Log.outInfo(LogFilter.ServerLoading, "Generating spell proc data from SpellMap...");
            count = 0;
            oldMSTime = Time.NowRelative;

            foreach (SpellInfo spellInfo in mSpellInfoMap.Values)
            {
                // Data already present in DB, overwrites default proc
                if (mSpellProcMap.ContainsKey((spellInfo.Id, spellInfo.Difficulty)))
                    continue;

                // Nothing to do if no flags set
                if (spellInfo.ProcFlags == null)
                    continue;

                bool addTriggerFlag = false;
                ProcFlagsSpellType procSpellTypeMask = ProcFlagsSpellType.None;
                uint nonProcMask = 0;
                foreach (var spellEffectInfo in spellInfo.GetEffects())
                {
                    if (!spellEffectInfo.IsEffect())
                        continue;

                    AuraType auraName = spellEffectInfo.ApplyAuraName;
                    if (auraName == 0)
                        continue;

                    if (!IsTriggerAura(auraName))
                    {
                        // explicitly disable non proccing auras to avoid losing charges on self proc
                        nonProcMask |= 1u << spellEffectInfo.EffectIndex;
                        continue;
                    }

                    procSpellTypeMask |= GetSpellTypeMask(auraName);
                    if (IsAlwaysTriggeredAura(auraName))
                        addTriggerFlag = true;

                    // many proc auras with taken procFlag mask don't have attribute "can proc with triggered"
                    // they should proc nevertheless (example mage armor spells with judgement)
                    if (!addTriggerFlag && spellInfo.ProcFlags.HasAnyFlag(ProcFlags.TakenHitMask))
                    {
                        switch (auraName)
                        {
                            case AuraType.ProcTriggerSpell:
                            case AuraType.ProcTriggerDamage:
                                addTriggerFlag = true;
                                break;
                            default:
                                break;
                        }
                    }
                }

                if (procSpellTypeMask == 0)
                {
                    foreach (var spellEffectInfo in spellInfo.GetEffects())
                    {
                        if (spellEffectInfo.IsAura())
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Spell Id {spellInfo.Id} has DBC ProcFlags " +
                                $"0x{spellInfo.ProcFlags.ProcFlags:X} " +
                                $"0x{spellInfo.ProcFlags.ProcFlags2:X}, " +
                                $"but it's of non-proc aura Type, " +
                                $"it probably needs an entry in `spell_proc` table to be handled correctly.");
                            break;
                        }
                    }

                    continue;
                }

                SpellProcEntry procEntry = new();
                procEntry.SchoolMask = 0;
                procEntry.ProcFlags = spellInfo.ProcFlags;
                procEntry.SpellFamilyName = 0;
                foreach (var spellEffectInfo in spellInfo.GetEffects())
                    if (spellEffectInfo.IsEffect() && IsTriggerAura(spellEffectInfo.ApplyAuraName))
                        procEntry.SpellFamilyMask |= spellEffectInfo.SpellClassMask;

                if (procEntry.SpellFamilyMask)
                    procEntry.SpellFamilyName = spellInfo.SpellFamilyName;

                procEntry.SpellTypeMask = procSpellTypeMask;
                procEntry.SpellPhaseMask = ProcFlagsSpellPhase.Hit;
                procEntry.HitMask = ProcFlagsHit.None; // uses default proc @see SpellMgr::CanSpellTriggerProcOnEvent

                if (!procEntry.ProcFlags.HasAnyFlag(ProcFlags.ReqSpellPhaseMask)
                    && procEntry.ProcFlags.HasAnyFlag(ProcFlags2.CastSuccessful))
                {
                    procEntry.SpellPhaseMask = ProcFlagsSpellPhase.Cast; // set default phase for PROC_FLAG_2_CAST_SUCCESSFUL
                }

                bool triggersSpell = false;
                foreach (var spellEffectInfo in spellInfo.GetEffects())
                {
                    if (!spellEffectInfo.IsAura())
                        continue;

                    switch (spellEffectInfo.ApplyAuraName)
                    {
                        // Reflect auras should only proc off reflects
                        case AuraType.ReflectSpells:
                        case AuraType.ReflectSpellsSchool:
                            procEntry.HitMask = ProcFlagsHit.Reflect;
                            break;
                        // Only drop charge on crit
                        case AuraType.ModWeaponCritPct:
                            procEntry.HitMask = ProcFlagsHit.Critical;
                            break;
                        // Only drop charge on block
                        case AuraType.ModBlockPercent:
                            procEntry.HitMask = ProcFlagsHit.Block;
                            break;
                        // proc auras with another aura reducing hit Chance (eg 63767) only proc on missed attack
                        case AuraType.ModHitChance:
                            if (spellEffectInfo.CalcValue() <= -100)
                                procEntry.HitMask = ProcFlagsHit.Miss;
                            break;
                        case AuraType.ProcTriggerSpell:
                        case AuraType.ProcTriggerSpellWithValue:
                            triggersSpell = spellEffectInfo.TriggerSpell != 0;
                            break;
                        default:
                            continue;
                    }
                    break;
                }

                procEntry.AttributesMask = 0;
                procEntry.DisableEffectsMask = nonProcMask;
                if (spellInfo.ProcFlags.HasAnyFlag(ProcFlags.Kill))
                    procEntry.AttributesMask |= ProcAttributes.ReqExpOrHonor;
                if (addTriggerFlag)
                    procEntry.AttributesMask |= ProcAttributes.TriggeredCanProc;

                procEntry.ProcsPerMinute = 0;
                procEntry.Chance = spellInfo.ProcChance;
                procEntry.Cooldown = spellInfo.ProcCooldown;
                procEntry.Charges = spellInfo.ProcCharges;

                if (spellInfo.HasAttribute(SpellAttr3.CanProcFromProcs) && !procEntry.SpellFamilyMask
                    && procEntry.Chance >= 100
                    && spellInfo.ProcBasePPM <= 0.0f
                    && procEntry.Cooldown <= 0
                    && procEntry.Charges <= 0
                    && procEntry.ProcFlags.HasAnyFlag(ProcFlags.DealMeleeAbility | ProcFlags.DealRangedAttack | ProcFlags.DealRangedAbility | ProcFlags.DealHelpfulAbility
                    | ProcFlags.DealHarmfulAbility | ProcFlags.DealHelpfulSpell | ProcFlags.DealHarmfulSpell | ProcFlags.DealHarmfulPeriodic | ProcFlags.DealHelpfulPeriodic)
                    && triggersSpell)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell Id {spellInfo.Id} has SPELL_ATTR3_CAN_PROC_FROM_PROCS attribute " +
                        $"and no restriction on what spells can cause it to proc and no cooldown. " +
                        "This spell can cause infinite proc loops. Proc data for this spell was not generated, " +
                        "data in `spell_proc` table is required for it to function!");
                    continue;
                }

                mSpellProcMap[(spellInfo.Id, spellInfo.Difficulty)] = procEntry;
                ++count;
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Generated spell proc data for {count} spells in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellThreats()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellThreatMap.Clear();                                // need for reload case

            //                                           0      1        2       3
            SQLResult result = DB.World.Query("SELECT entry, flatMod, pctMod, apPctMod FROM spell_threat");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 aggro generating spells. DB table `spell_threat` is empty.");
                return;
            }

            uint count = 0;

            do
            {
                int entry = result.Read<int>(0);

                if (!HasSpellInfo(entry, Difficulty.None))
                {
                    Log.outError(LogFilter.Sql, $"Spell {entry} listed in `spell_threat` does not exist");
                    continue;
                }

                SpellThreatEntry ste = new();
                ste.flatMod = result.Read<int>(1);
                ste.pctMod = result.Read<float>(2);
                ste.apPctMod = result.Read<float>(3);

                mSpellThreatMap[entry] = ste;
                count++;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} SpellThreatEntries in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSkillLineAbilityMap()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSkillLineAbilityMap.Clear();

            foreach (var skill in CliDB.SkillLineAbilityStorage.Values)
                mSkillLineAbilityMap.Add(skill.Spell, skill);

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {mSkillLineAbilityMap.Count} SkillLineAbility MultiMap Data in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellPetAuras()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellPetAuraMap.Clear();                                  // need for reload case

            //                                          0       1       2    3
            SQLResult result = DB.World.Query("SELECT spell, effectId, pet, aura FROM spell_pet_auras");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spell pet auras. DB table `spell_pet_auras` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int spell = result.Read<int>(0);
                byte eff = result.Read<byte>(1);
                int pet = result.Read<int>(2);
                int aura = result.Read<int>(3);

                var petAura = mSpellPetAuraMap.LookupByKey((spell << 8) + eff);
                if (petAura != null)
                    petAura.AddAura(pet, aura);
                else
                {
                    SpellInfo spellInfo = GetSpellInfo(spell, Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spell {spell} listed in `spell_pet_auras` does not exist");
                        continue;
                    }
                    if (eff >= spellInfo.GetEffects().Count)
                    {
                        Log.outError(LogFilter.Spells, 
                            $"Spell {spell} listed in `spell_pet_auras` does not have effect at index {eff}");
                        continue;
                    }

                    if (spellInfo.GetEffect(eff).Effect != SpellEffectName.Dummy 
                        && (spellInfo.GetEffect(eff).Effect != SpellEffectName.ApplyAura || spellInfo.GetEffect(eff).ApplyAuraName != AuraType.Dummy))
                    {
                        Log.outError(LogFilter.Spells, 
                            $"Spell {spell} listed in `spell_pet_auras` does not have dummy aura or dummy effect");
                        continue;
                    }

                    SpellInfo spellInfo2 = GetSpellInfo(aura, Difficulty.None);
                    if (spellInfo2 == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Aura {aura} listed in `spell_pet_auras` does not exist");
                        continue;
                    }

                    PetAura pa = new(pet, aura, spellInfo.GetEffect(eff).TargetA.GetTarget() == Targets.UnitPet, spellInfo.GetEffect(eff).CalcValue());
                    mSpellPetAuraMap[(spell << 8) + eff] = pa;
                }
                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} spell pet auras in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellEnchantProcData()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellEnchantProcEventMap.Clear();                             // need for reload case

            //                                         0          1       2               3        4
            SQLResult result = DB.World.Query("SELECT EnchantID, Chance, ProcsPerMinute, HitMask, AttributesMask FROM spell_enchant_proc_data");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 spell enchant proc event conditions. DB table `spell_enchant_proc_data` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int enchantId = result.Read<int>(0);

                var ench = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchantId);
                if (ench == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Enchancment {enchantId} listed in `spell_enchant_proc_data` does not exist");
                    continue;
                }

                SpellEnchantProcEntry spe = new();
                spe.Chance = result.Read<uint>(1);
                spe.ProcsPerMinute = result.Read<float>(2);
                spe.HitMask = result.Read<uint>(3);
                spe.AttributesMask = (EnchantProcAttributes)result.Read<uint>(4);

                mSpellEnchantProcEventMap[enchantId] = spe;

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} enchant proc data definitions in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellLinked()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellLinkedMap.Clear();    // need for reload case

            //                                                0              1             2
            SQLResult result = DB.World.Query("SELECT spell_trigger, spell_effect, Type FROM spell_linked_spell");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 linked spells. DB table `spell_linked_spell` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int trigger = result.Read<int>(0);
                int effect = result.Read<int>(1);
                SpellLinkedType type = (SpellLinkedType)result.Read<byte>(2);

                SpellInfo spellInfo = GetSpellInfo(Math.Abs(trigger), Difficulty.None);
                if (spellInfo == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell {Math.Abs(trigger)} listed in `spell_linked_spell` does not exist");
                    continue;
                }

                if (effect >= 0)
                {
                    foreach (var spellEffectInfo in spellInfo.GetEffects())
                    {
                        if (spellEffectInfo.CalcValue() == Math.Abs(effect))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"The spell {Math.Abs(trigger)} Effect: " +
                                $"{Math.Abs(effect)} listed in `spell_linked_spell` " +
                                $"has same bp{spellEffectInfo.EffectIndex} like effect (possible hack)");
                        }
                    }
                }

                if (!HasSpellInfo(Math.Abs(effect), Difficulty.None))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell {Math.Abs(effect)} listed in `spell_linked_spell` does not exist");
                    continue;
                }

                if (type < SpellLinkedType.Cast || type > SpellLinkedType.Remove)
                {
                    Log.outError(LogFilter.Sql, 
                        $"The spell trigger {trigger}, effect {effect} listed in `spell_linked_spell` " +
                        $"has invalid link type {type}, skipped.");
                    continue;
                }

                if (trigger < 0)
                {
                    if (type != SpellLinkedType.Cast)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"The spell trigger {trigger} listed in `spell_linked_spell` " +
                            $"has invalid link type {type}, changed to 0.");
                    }

                    trigger = -trigger;
                    type = SpellLinkedType.Remove;
                }


                if (type != SpellLinkedType.Aura)
                {
                    if (trigger == effect)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"The spell trigger {trigger}, effect {effect} listed in `spell_linked_spell` " +
                            $"triggers itself (infinite loop), skipped.");
                        continue;
                    }
                }

                mSpellLinkedMap.Add((type, trigger), effect);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} linked spells in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadPetLevelupSpellMap()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mPetLevelupSpellMap.Clear();                                   // need for reload case

            int count = 0;
            int family_count = 0;

            foreach (var creatureFamily in CliDB.CreatureFamilyStorage.Values)
            {
                for (byte j = 0; j < 2; ++j)
                {
                    if (creatureFamily.SkillLine(j) == 0)
                        continue;

                    var skillLineAbilities = Global.DB2Mgr.GetSkillLineAbilitiesBySkill(creatureFamily.SkillLine(j));
                    if (skillLineAbilities == null)
                        continue;

                    foreach (var skillLine in skillLineAbilities)
                    {
                        if (skillLine.AcquireMethod != AbilityLearnType.OnSkillLearn)
                            continue;

                        SpellInfo spell = GetSpellInfo(skillLine.Spell, Difficulty.None);
                        if (spell == null) // not exist or triggered or talent
                            continue;

                        if (spell.SpellLevel == 0)
                            continue;

                        if (!mPetLevelupSpellMap.ContainsKey(creatureFamily.Id))
                            mPetLevelupSpellMap.Add(creatureFamily.Id, new());

                        var spellSet = mPetLevelupSpellMap.LookupByKey(creatureFamily.Id);
                        if (spellSet.Count == 0)
                            ++family_count;

                        spellSet.Add(spell.SpellLevel, spell.Id);
                        ++count;
                    }
                }
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {count} pet levelup and default spells for {family_count} families in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadPetDefaultSpells()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mPetDefaultSpellsMap.Clear();

            uint countCreature = 0;

            Log.outInfo(LogFilter.ServerLoading, "Loading summonable creature templates...");

            // different summon spells
            foreach (var spellEntry in mSpellInfoMap.Values)
            {
                if (spellEntry.Difficulty != Difficulty.None)
                    continue;

                foreach (var spellEffectInfo in spellEntry.GetEffects())
                {
                    if (spellEffectInfo.Effect == SpellEffectName.Summon || spellEffectInfo.Effect == SpellEffectName.SummonPet)
                    {
                        int creature_id = spellEffectInfo.MiscValue;
                        CreatureTemplate cInfo = Global.ObjectMgr.GetCreatureTemplate(creature_id);
                        if (cInfo == null)
                            continue;

                        // get default pet spells from creature_template
                        var petSpellsId = cInfo.Entry;
                        if (mPetDefaultSpellsMap.LookupByKey(cInfo.Entry) != null)
                            continue;

                        PetDefaultSpellsEntry petDefSpells = new();
                        for (byte j = 0; j < SharedConst.MaxCreatureSpellDataSlots; ++j)
                            petDefSpells.spellid[j] = cInfo.Spells[j];

                        if (LoadPetDefaultSpells_helper(cInfo, petDefSpells))
                        {
                            mPetDefaultSpellsMap[petSpellsId] = petDefSpells;
                            ++countCreature;
                        }
                    }
                }
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {countCreature} summonable creature templates in {Time.Diff(oldMSTime)} ms.");
        }

        bool LoadPetDefaultSpells_helper(CreatureTemplate cInfo, PetDefaultSpellsEntry petDefSpells)
        {
            // skip empty list;
            bool have_spell = false;
            for (byte j = 0; j < SharedConst.MaxCreatureSpellDataSlots; ++j)
            {
                if (petDefSpells.spellid[j] != 0)
                {
                    have_spell = true;
                    break;
                }
            }

            if (!have_spell)
                return false;

            // remove duplicates with levelupSpells if any
            var levelupSpells = cInfo.Family != 0 ? GetPetLevelupSpellList(cInfo.Family) : null;
            if (levelupSpells != null)
            {
                for (byte j = 0; j < SharedConst.MaxCreatureSpellDataSlots; ++j)
                {
                    if (petDefSpells.spellid[j] == 0)
                        continue;

                    foreach (var pair in levelupSpells)
                    {
                        if (pair.Value == petDefSpells.spellid[j])
                        {
                            petDefSpells.spellid[j] = 0;
                            break;
                        }
                    }
                }
            }

            // skip empty list;
            have_spell = false;
            for (byte j = 0; j < SharedConst.MaxCreatureSpellDataSlots; ++j)
            {
                if (petDefSpells.spellid[j] != 0)
                {
                    have_spell = true;
                    break;
                }
            }

            return have_spell;
        }

        public void LoadSpellAreas()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellAreaMap.Clear();                                  // need for reload case
            mSpellAreaForAreaMap.Clear();
            mSpellAreaForQuestMap.Clear();
            mSpellAreaForQuestEndMap.Clear();
            mSpellAreaForAuraMap.Clear();

            //                                            0     1         2              3               4                 5          6          7       8      9
            SQLResult result = DB.World.Query("SELECT spell, area, quest_start, quest_start_status, quest_end_status, quest_end, aura_spell, racemask, gender, flags FROM spell_area");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 spell area requirements. DB table `spell_area` is empty.");

                return;
            }

            uint count = 0;
            do
            {
                int spell = result.Read<int>(0);

                SpellArea spellArea = new();
                spellArea.spellId = spell;
                spellArea.areaId = result.Read<int>(1);
                spellArea.questStart = result.Read<int>(2);
                spellArea.questStartStatus = result.Read<uint>(3);
                spellArea.questEndStatus = result.Read<uint>(4);
                spellArea.questEnd = result.Read<int>(5);
                spellArea.auraSpell = result.Read<int>(6);
                spellArea.raceMask = (RaceMask)result.Read<ulong>(7);
                spellArea.gender = (Gender)result.Read<uint>(8);
                spellArea.flags = (SpellAreaFlag)result.Read<byte>(9);

                SpellInfo spellInfo = GetSpellInfo(spell, Difficulty.None);
                if (spellInfo != null)
                {
                    if (spellArea.flags.HasAnyFlag(SpellAreaFlag.AutoCast))
                        spellInfo.Attributes |= SpellAttr0.NoAuraCancel;
                }
                else
                {
                    Log.outError(LogFilter.Sql, $"Spell {spell} listed in `spell_area` does not exist");
                    continue;
                }

                {
                    bool ok = true;
                    var sa_bounds = GetSpellAreaMapBounds(spellArea.spellId);
                    foreach (var bound in sa_bounds)
                    {
                        if (spellArea.spellId != bound.spellId)
                            continue;
                        if (spellArea.areaId != bound.areaId)
                            continue;
                        if (spellArea.questStart != bound.questStart)
                            continue;
                        if (spellArea.auraSpell != bound.auraSpell)
                            continue;
                        if (spellArea.raceMask.HasAnyFlag(bound.raceMask))
                            continue;
                        if (spellArea.gender != bound.gender)
                            continue;

                        // duplicate by requirements
                        ok = false;
                        break;
                    }

                    if (!ok)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spell {spell} listed in `spell_area` already listed with similar requirements.");
                        continue;
                    }
                }

                if (spellArea.areaId != 0 && !CliDB.AreaTableStorage.ContainsKey(spellArea.areaId))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell {spell} listed in `spell_area` have wrong area ({spellArea.areaId}) requirement");
                    continue;
                }

                if (spellArea.questStart != 0 && Global.ObjectMgr.GetQuestTemplate(spellArea.questStart) == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell {spell} listed in `spell_area` have wrong start quest ({spellArea.questStart}) requirement");
                    continue;
                }

                if (spellArea.questEnd != 0)
                {
                    if (Global.ObjectMgr.GetQuestTemplate(spellArea.questEnd) == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spell {spell} listed in `spell_area` have wrong end quest ({spellArea.questEnd}) requirement");
                        continue;
                    }
                }

                if (spellArea.auraSpell != 0)
                {
                    SpellInfo info = GetSpellInfo(Math.Abs(spellArea.auraSpell), Difficulty.None);
                    if (info == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spell {spell} listed in `spell_area` have wrong aura spell " +
                            $"({Math.Abs(spellArea.auraSpell)}) requirement");

                        continue;
                    }

                    if (Math.Abs(spellArea.auraSpell) == spellArea.spellId)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Spell {spell} listed in `spell_area` have aura spell " +
                            $"({Math.Abs(spellArea.auraSpell)}) requirement for itself");

                        continue;
                    }

                    // not allow autocast chains by auraSpell field (but allow use as alternative if not present)
                    if (spellArea.flags.HasAnyFlag(SpellAreaFlag.AutoCast) && spellArea.auraSpell > 0)
                    {
                        bool chain = false;
                        var saBound = GetSpellAreaForAuraMapBounds(spellArea.spellId);
                        foreach (var bound in saBound)
                        {
                            if (bound.flags.HasAnyFlag(SpellAreaFlag.AutoCast) && bound.auraSpell > 0)
                            {
                                chain = true;
                                break;
                            }
                        }

                        if (chain)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Spell {spell} listed in `spell_area` " +
                                $"have aura spell ({spellArea.auraSpell}) " +
                                $"requirement that itself autocast from aura");

                            continue;
                        }

                        var saBound2 = GetSpellAreaMapBounds(spellArea.auraSpell);
                        foreach (var bound in saBound2)
                        {
                            if (bound.flags.HasAnyFlag(SpellAreaFlag.AutoCast) && bound.auraSpell > 0)
                            {
                                chain = true;
                                break;
                            }
                        }

                        if (chain)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Spell {spell} listed in `spell_area` " +
                                $"have aura spell ({spellArea.auraSpell}) " +
                                $"requirement that itself autocast from aura");

                            continue;
                        }
                    }
                }

                if (spellArea.raceMask != RaceMask.None && !spellArea.raceMask.HasAnyFlag(RaceMask.Playable))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell {spell} listed in `spell_area` " +
                        $"have wrong race mask ({spellArea.raceMask}) requirement");
                    continue;
                }

                if (spellArea.gender != Gender.None && spellArea.gender != Gender.Female && spellArea.gender != Gender.Male)
                {
                    Log.outError(LogFilter.Sql, 
                        $"Spell {spell} listed in `spell_area` have wrong gender ({spellArea.gender}) requirement");
                    continue;
                }
                mSpellAreaMap.Add(spell, spellArea);
                var sa = mSpellAreaMap[spell];

                // for search by current zone/subzone at zone/subzone change
                if (spellArea.areaId != 0)
                    mSpellAreaForAreaMap.AddRange(spellArea.areaId, sa);

                // for search at quest update checks
                if (spellArea.questStart != 0 || spellArea.questEnd != 0)
                {
                    if (spellArea.questStart == spellArea.questEnd)
                        mSpellAreaForQuestMap.AddRange(spellArea.questStart, sa);
                    else
                    {
                        if (spellArea.questStart != 0)
                            mSpellAreaForQuestMap.AddRange(spellArea.questStart, sa);
                        if (spellArea.questEnd != 0)
                            mSpellAreaForQuestMap.AddRange(spellArea.questEnd, sa);
                    }
                }

                // for search at quest start/reward
                if (spellArea.questEnd != 0)
                    mSpellAreaForQuestEndMap.AddRange(spellArea.questEnd, sa);

                // for search at aura apply
                if (spellArea.auraSpell != 0)
                    mSpellAreaForAuraMap.AddRange(Math.Abs(spellArea.auraSpell), sa);

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {count} spell area requirements in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellInfoStore()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mSpellInfoMap.Clear();
            var loadData = new Dictionary<(int Id, Difficulty difficulty), SpellInfoLoadHelper>();

            Dictionary<int, BattlePetSpeciesRecord> battlePetSpeciesByCreature = new();
            foreach (var battlePetSpecies in CliDB.BattlePetSpeciesStorage.Values)
            {
                if (battlePetSpecies.CreatureID != 0)
                    battlePetSpeciesByCreature[battlePetSpecies.CreatureID] = battlePetSpecies;
            }

            SpellInfoLoadHelper GetLoadHelper(int spellId, Difficulty difficulty)
            {
                var key = (spellId, difficulty);
                if (!loadData.ContainsKey(key))
                    loadData[key] = new SpellInfoLoadHelper();

                return loadData[key];
            }

            foreach (var effect in CliDB.SpellEffectStorage.Values)
            {
                Cypher.Assert(effect.EffectIndex < SpellConst.MaxEffects, $"MAX_SPELL_EFFECTS must be at least {effect.EffectIndex}");
                Cypher.Assert(effect.Effect < SpellEffectName.TotalSpellEffects, $"TOTAL_SPELL_EFFECTS must be at least {effect.Effect}");
                Cypher.Assert(effect.EffectAura < AuraType.Total, $"TOTAL_AURAS must be at least {effect.EffectAura}");
                Cypher.Assert(effect.ImplicitTarget[0] < (int)Targets.TotalSpellTargets, $"TOTAL_SPELL_TARGETS must be at least {effect.ImplicitTarget[0]}");
                Cypher.Assert(effect.ImplicitTarget[1] < (int)Targets.TotalSpellTargets, $"TOTAL_SPELL_TARGETS must be at least {effect.ImplicitTarget[1]}");

                GetLoadHelper(effect.SpellID, effect.DifficultyID).Effects[effect.EffectIndex] = effect;

                if (effect.Effect == SpellEffectName.Summon)
                {
                    var summonProperties = CliDB.SummonPropertiesStorage.LookupByKey(effect.EffectMiscValue[1]);
                    if (summonProperties != null)
                    {
                        if (summonProperties.Slot == (int)SummonSlot.MiniPet && summonProperties.HasFlag(SummonPropertiesFlags.SummonFromBattlePetJournal))
                        {
                            var battlePetSpecies = battlePetSpeciesByCreature.LookupByKey(effect.EffectMiscValue[0]);
                            if (battlePetSpecies != null)
                                BattlePetMgr.AddBattlePetSpeciesBySpell(effect.SpellID, battlePetSpecies);
                        }
                    }
                }

                if (effect.Effect == SpellEffectName.Language)
                    Global.LanguageMgr.LoadSpellEffectLanguage(effect);

                switch (effect.EffectAura)
                {
                    case AuraType.AddFlatModifier:
                    case AuraType.AddPctModifier:
                        Cypher.Assert(effect.EffectMiscValue[0] < (int)SpellModOp.Max, 
                            $"MAX_SPELLMOD must be at least {effect.EffectMiscValue[0] + 1}");
                        break;
                    default:
                        break;
                }
            }

            foreach (SpellAuraOptionsRecord auraOptions in CliDB.SpellAuraOptionsStorage.Values)
                GetLoadHelper(auraOptions.SpellID, auraOptions.DifficultyID).AuraOptions = auraOptions;

            foreach (SpellAuraRestrictionsRecord auraRestrictions in CliDB.SpellAuraRestrictionsStorage.Values)
                GetLoadHelper(auraRestrictions.SpellID, auraRestrictions.DifficultyID).AuraRestrictions = auraRestrictions;

            foreach (SpellCastingRequirementsRecord castingRequirements in CliDB.SpellCastingRequirementsStorage.Values)
                GetLoadHelper(castingRequirements.SpellID, 0).CastingRequirements = castingRequirements;

            foreach (SpellCategoriesRecord categories in CliDB.SpellCategoriesStorage.Values)
                GetLoadHelper(categories.SpellID, categories.DifficultyID).Categories = categories;

            foreach (SpellClassOptionsRecord classOptions in CliDB.SpellClassOptionsStorage.Values)
                GetLoadHelper(classOptions.SpellID, 0).ClassOptions = classOptions;

            foreach (SpellCooldownsRecord cooldowns in CliDB.SpellCooldownsStorage.Values)
                GetLoadHelper(cooldowns.SpellID, cooldowns.DifficultyID).Cooldowns = cooldowns;

            foreach (SpellEquippedItemsRecord equippedItems in CliDB.SpellEquippedItemsStorage.Values)
                GetLoadHelper(equippedItems.SpellID, 0).EquippedItems = equippedItems;

            foreach (SpellInterruptsRecord interrupts in CliDB.SpellInterruptsStorage.Values)
                GetLoadHelper(interrupts.SpellID, interrupts.DifficultyID).Interrupts = interrupts;

            foreach (SpellLabelRecord label in CliDB.SpellLabelStorage.Values)
                GetLoadHelper(label.SpellID, 0).Labels.Add(label);

            foreach (SpellLevelsRecord levels in CliDB.SpellLevelsStorage.Values)
                GetLoadHelper(levels.SpellID, levels.DifficultyID).Levels = levels;

            foreach (SpellMiscRecord misc in CliDB.SpellMiscStorage.Values)
            {
                if (misc.DifficultyID == 0)
                    GetLoadHelper(misc.SpellID, misc.DifficultyID).Misc = misc;
            }

            foreach (SpellPowerRecord power in CliDB.SpellPowerStorage.Values)
            {
                var difficulty = Difficulty.None;
                byte index = power.OrderIndex;

                SpellPowerDifficultyRecord powerDifficulty = CliDB.SpellPowerDifficultyStorage.LookupByKey(power.Id);
                if (powerDifficulty != null)
                {
                    difficulty = powerDifficulty.DifficultyID;
                    index = powerDifficulty.OrderIndex;
                }

                GetLoadHelper(power.SpellID, difficulty).Powers[index] = power;
            }

            foreach (SpellReagentsRecord reagents in CliDB.SpellReagentsStorage.Values)
                GetLoadHelper(reagents.SpellID, 0).Reagents = reagents;

            foreach (SpellReagentsCurrencyRecord reagentsCurrency in CliDB.SpellReagentsCurrencyStorage.Values)
                GetLoadHelper(reagentsCurrency.SpellID, 0).ReagentsCurrency.Add(reagentsCurrency);

            foreach (SpellScalingRecord scaling in CliDB.SpellScalingStorage.Values)
                GetLoadHelper(scaling.SpellID, 0).Scaling = scaling;

            foreach (SpellShapeshiftRecord shapeshift in CliDB.SpellShapeshiftStorage.Values)
                GetLoadHelper(shapeshift.SpellID, 0).Shapeshift = shapeshift;

            foreach (SpellTargetRestrictionsRecord targetRestrictions in CliDB.SpellTargetRestrictionsStorage.Values)
                GetLoadHelper(targetRestrictions.SpellID, targetRestrictions.DifficultyID).TargetRestrictions = targetRestrictions;

            foreach (SpellTotemsRecord totems in CliDB.SpellTotemsStorage.Values)
                GetLoadHelper(totems.SpellID, 0).Totems = totems;

            foreach (var visual in CliDB.SpellXSpellVisualStorage.Values)
            {
                var visuals = GetLoadHelper(visual.SpellID, visual.DifficultyID).Visuals;
                visuals.Add(visual);
            }

            // sorted with unconditional visuals being last
            foreach (var data in loadData)
            {
                data.Value.Visuals.Sort((left, right) =>
                {
                    return right.CasterPlayerConditionID.CompareTo(left.CasterPlayerConditionID);
                });
            }

            foreach (var data in loadData)
            {
                SpellNameRecord spellNameEntry = CliDB.SpellNameStorage.LookupByKey(data.Key.Id);
                if (spellNameEntry == null)
                    continue;

                // fill blanks
                DifficultyRecord difficultyEntry = CliDB.DifficultyStorage.LookupByKey(data.Key.difficulty);
                if (difficultyEntry != null)
                {
                    do
                    {
                        SpellInfoLoadHelper fallbackData = 
                            loadData.LookupByKey((data.Key.Id, difficultyEntry.FallbackDifficultyID));

                        if (fallbackData != null)
                        {
                            if (data.Value.AuraOptions == null)
                                data.Value.AuraOptions = fallbackData.AuraOptions;

                            if (data.Value.AuraRestrictions == null)
                                data.Value.AuraRestrictions = fallbackData.AuraRestrictions;

                            if (data.Value.CastingRequirements == null)
                                data.Value.CastingRequirements = fallbackData.CastingRequirements;

                            if (data.Value.Categories == null)
                                data.Value.Categories = fallbackData.Categories;

                            if (data.Value.ClassOptions == null)
                                data.Value.ClassOptions = fallbackData.ClassOptions;

                            if (data.Value.Cooldowns == null)
                                data.Value.Cooldowns = fallbackData.Cooldowns;

                            for (var i = 0; i < data.Value.Effects.Length; ++i)
                                if (data.Value.Effects[i] == null)
                                    data.Value.Effects[i] = fallbackData.Effects[i];

                            if (data.Value.EquippedItems == null)
                                data.Value.EquippedItems = fallbackData.EquippedItems;

                            if (data.Value.Interrupts == null)
                                data.Value.Interrupts = fallbackData.Interrupts;

                            if (data.Value.Labels.Empty())
                                data.Value.Labels = fallbackData.Labels;

                            if (data.Value.Levels == null)
                                data.Value.Levels = fallbackData.Levels;

                            if (data.Value.Misc == null)
                                data.Value.Misc = fallbackData.Misc;

                            for (var i = 0; i < fallbackData.Powers.Length; ++i)
                                if (data.Value.Powers[i] == null)
                                    data.Value.Powers[i] = fallbackData.Powers[i];

                            if (data.Value.Reagents == null)
                                data.Value.Reagents = fallbackData.Reagents;

                            if (data.Value.ReagentsCurrency.Empty())
                                data.Value.ReagentsCurrency = fallbackData.ReagentsCurrency;

                            if (data.Value.Scaling == null)
                                data.Value.Scaling = fallbackData.Scaling;

                            if (data.Value.Shapeshift == null)
                                data.Value.Shapeshift = fallbackData.Shapeshift;

                            if (data.Value.TargetRestrictions == null)
                                data.Value.TargetRestrictions = fallbackData.TargetRestrictions;

                            if (data.Value.Totems == null)
                                data.Value.Totems = fallbackData.Totems;

                            // visuals fall back only to first difficulty that defines any visual
                            // they do not stack all difficulties in fallback chain
                            if (data.Value.Visuals.Empty())
                                data.Value.Visuals = fallbackData.Visuals;
                        }

                        difficultyEntry = CliDB.DifficultyStorage.LookupByKey(difficultyEntry.FallbackDifficultyID);
                    } while (difficultyEntry != null);
                }

                //first key = id, difficulty
                //second key = id


                mSpellInfoMap.Add(spellNameEntry.Id, new SpellInfo(spellNameEntry, data.Key.difficulty, data.Value));
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded SpellInfo store in {Time.Diff(oldMSTime)} ms.");
        }

        public void UnloadSpellInfoImplicitTargetConditionLists()
        {
            foreach (var spell in mSpellInfoMap.Values)
                spell._UnloadImplicitTargetConditionLists();

        }

        public void LoadSpellInfoServerside()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            MultiMap<(int spellId, Difficulty difficulty), SpellEffectRecord> spellEffects = new();

            {
                //                                                  0          1            2           3            4               5              6
                SQLResult effectsResult = DB.World.Query("SELECT SpellID, DifficultyID, EffectIndex, Effect, EffectAmplitude, EffectAttributes, EffectAura, " +
                //         7                 8                 9                       10                    11                  12              13              14
                "EffectAuraPeriod, EffectBasePoints, EffectBonusCoefficient, EffectChainAmplitude, EffectChainTargets, EffectDieSides, EffectItemType, EffectMechanic, " +
                //         15                       16               17                        18                  19                    20           21         22
                "EffectPointsPerResource, EffectPosFacing, EffectRealPointsPerLevel, EffectTriggerSpell, BonusCoefficientFromAP, PvpMultiplier, Coefficient, Variance, " +
                //         23                          24                      25                26                27                  28
                "ResourceCoefficient, GroupSizeBasePointsCoefficient, EffectMiscValue1, EffectMiscValue2, EffectRadiusIndex1, EffectRadiusIndex2, " +
                //         29                     30                     31                     32                     33
                "EffectSpellClassMask1, EffectSpellClassMask2, EffectSpellClassMask3, EffectSpellClassMask4, ImplicitTarget1, " +
                //         34
                "ImplicitTarget2 FROM serverside_spell_effect");

                if (!effectsResult.IsEmpty())
                {
                    do
                    {
                        int spellId = effectsResult.Read<int>(0);
                        Difficulty difficulty = (Difficulty)effectsResult.Read<uint>(1);
                        SpellEffectRecord effect = new();
                        effect.EffectIndex = effectsResult.Read<int>(2);
                        effect.Effect = (SpellEffectName)effectsResult.Read<int>(3);
                        effect.EffectAmplitude = effectsResult.Read<float>(4);
                        effect.EffectAttributes = (SpellEffectAttributes)effectsResult.Read<int>(5);
                        effect.EffectAura = (AuraType)effectsResult.Read<short>(6);
                        effect.EffectAuraPeriod = (Milliseconds)effectsResult.Read<int>(7);
                        effect.EffectBasePoints = effectsResult.Read<int>(8);
                        effect.EffectBonusCoefficient = effectsResult.Read<float>(9);
                        effect.EffectChainAmplitude = effectsResult.Read<float>(10);
                        effect.EffectChainTargets = effectsResult.Read<int>(11);
                        effect.EffectDieSides = effectsResult.Read<int>(12);
                        effect.EffectItemType = effectsResult.Read<int>(13);
                        effect.EffectMechanic = effectsResult.Read<int>(14);
                        effect.EffectPointsPerResource = effectsResult.Read<float>(15);
                        effect.EffectPosFacing = effectsResult.Read<float>(16);
                        effect.EffectRealPointsPerLevel = effectsResult.Read<float>(17);
                        effect.EffectTriggerSpell = effectsResult.Read<int>(18);
                        effect.BonusCoefficientFromAP = effectsResult.Read<float>(19);
                        effect.PvpMultiplier = effectsResult.Read<float>(20);
                        effect.Coefficient = effectsResult.Read<float>(21);
                        effect.Variance = effectsResult.Read<float>(22);
                        effect.ResourceCoefficient = effectsResult.Read<float>(23);
                        effect.GroupSizeBasePointsCoefficient = effectsResult.Read<float>(24);
                        effect.EffectMiscValue[0] = effectsResult.Read<int>(25);
                        effect.EffectMiscValue[1] = effectsResult.Read<int>(26);
                        effect.EffectRadiusIndex[0] = effectsResult.Read<int>(27);
                        effect.EffectRadiusIndex[1] = effectsResult.Read<int>(28);
                        effect.EffectSpellClassMask = new FlagArray128(effectsResult.Read<uint>(29), effectsResult.Read<uint>(30), effectsResult.Read<uint>(31), effectsResult.Read<uint>(32));
                        effect.ImplicitTarget[0] = effectsResult.Read<short>(33);
                        effect.ImplicitTarget[1] = effectsResult.Read<short>(34);

                        var existingSpellBounds = _GetSpellInfo(spellId);
                        if (existingSpellBounds == null)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} difficulty {difficulty} effext index {effect.EffectIndex} " +
                                $"references a regular spell loaded from file. " +
                                $"Adding serverside effects to existing spells is not allowed.");
                            continue;
                        }

                        if (difficulty != Difficulty.None && !CliDB.DifficultyStorage.HasRecord((int)difficulty))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} effect index {effect.EffectIndex} " +
                                $"references non-existing difficulty {difficulty}, skipped");
                            continue;
                        }

                        if (effect.EffectIndex >= SpellConst.MaxEffects)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} difficulty {difficulty} " +
                                $"has more than {SpellConst.MaxEffects} effects, effect at index {effect.EffectIndex} skipped");
                            continue;
                        }

                        if (effect.Effect >= SpellEffectName.TotalSpellEffects)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} difficulty {difficulty} " +
                                $"has invalid effect Type {effect.Effect} at index {effect.EffectIndex}, skipped");
                            continue;
                        }

                        if (effect.EffectAura >= AuraType.Total)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} difficulty {difficulty} " +
                                $"has invalid aura Type {effect.EffectAura} at index {effect.EffectIndex}, skipped");
                            continue;
                        }

                        if (effect.ImplicitTarget[0] >= (uint)Targets.TotalSpellTargets)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} difficulty {difficulty} " +
                                $"has invalid targetA Type {effect.ImplicitTarget[0]} at index {effect.EffectIndex}, skipped");
                            continue;
                        }

                        if (effect.ImplicitTarget[1] >= (uint)Targets.TotalSpellTargets)
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} difficulty {difficulty} " +
                                $"has invalid targetB Type {effect.ImplicitTarget[1]} at index {effect.EffectIndex}, skipped");
                            continue;
                        }

                        if (effect.EffectRadiusIndex[0] != 0 && !CliDB.SpellRadiusStorage.HasRecord(effect.EffectRadiusIndex[0]))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} difficulty {difficulty} " +
                                $"has invalid radius id {effect.EffectRadiusIndex[0]} at index {effect.EffectIndex}, set to 0");
                        }

                        if (effect.EffectRadiusIndex[1] != 0 && !CliDB.SpellRadiusStorage.HasRecord(effect.EffectRadiusIndex[1]))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} difficulty {difficulty} " +
                                $"has invalid max radius id {effect.EffectRadiusIndex[1]} at index {effect.EffectIndex}, set to 0");
                        }

                        spellEffects.Add((spellId, difficulty), effect);

                    } while (effectsResult.NextRow());
                }
            }

            {
                //                                               0       1            2         3       4           5           6             7              8
                SQLResult spellsResult = DB.World.Query("SELECT Id, DifficultyID, CategoryId, Dispel, Mechanic, Attributes, AttributesEx, AttributesEx2, AttributesEx3, " +
                    //   9              10             11             12             13             14             15              16              17              18
                    "AttributesEx4, AttributesEx5, AttributesEx6, AttributesEx7, AttributesEx8, AttributesEx9, AttributesEx10, AttributesEx11, AttributesEx12, AttributesEx13, " +
                    //   19       20          21       22                  23                  24                 25               26
                    "Stances, StancesNot, Targets, TargetCreatureType, RequiresSpellFocus, FacingCasterFlags, CasterAuraState, TargetAuraState, " +
                    //   27                      28                      29               30               31                      32                      33
                    "ExcludeCasterAuraState, ExcludeTargetAuraState, CasterAuraSpell, TargetAuraSpell, ExcludeCasterAuraSpell, ExcludeTargetAuraSpell, CastingTimeIndex, " +
                    //   34            35                    36                     37                 38              39                   40
                    "RecoveryTime, CategoryRecoveryTime, StartRecoveryCategory, StartRecoveryTime, InterruptFlags, AuraInterruptFlags1, AuraInterruptFlags2, " +
                    //   41                      42                      43         44          45          46           47            48           49        50         51
                    "ChannelInterruptFlags1, ChannelInterruptFlags2, ProcFlags, ProcFlags2, ProcChance, ProcCharges, ProcCooldown, ProcBasePPM, MaxLevel, BaseLevel, SpellLevel, " +
                    //   52             53          54     55           56           57                 58                        59                             60
                    "DurationIndex, RangeIndex, Speed, LaunchDelay, StackAmount, EquippedItemClass, EquippedItemSubClassMask, EquippedItemInventoryTypeMask, ContentTuningId, " +
                    //   61         62         63         64              65                  66               67                 68                 69                 70
                    "SpellName, ConeAngle, ConeWidth, MaxTargetLevel, MaxAffectedTargets, SpellFamilyName, SpellFamilyFlags1, SpellFamilyFlags2, SpellFamilyFlags3, SpellFamilyFlags4, " +
                    //   71        72              73           74          75
                    "DmgClass, PreventionType, AreaGroupId, SchoolMask, ChargeCategoryId FROM serverside_spell");

                if (!spellsResult.IsEmpty())
                {
                    do
                    {
                        int spellId = spellsResult.Read<int>(0);
                        Difficulty difficulty = (Difficulty)spellsResult.Read<int>(1);
                        if (CliDB.SpellNameStorage.HasRecord(spellId))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Serverside spell {spellId} difficulty {difficulty} is already loaded from file. " +
                                $"Overriding existing spells is not allowed.");
                            continue;
                        }

                        mServersideSpellNames.Add(new(spellId, spellsResult.Read<string>(62)));

                        SpellInfo spellInfo = new(mServersideSpellNames.Last().Name, difficulty, spellEffects[(spellId, difficulty)]);
                        spellInfo.CategoryId = (SpellCategories)spellsResult.Read<int>(2);
                        spellInfo.Dispel = (DispelType)spellsResult.Read<uint>(3);
                        spellInfo.Mechanic = (Mechanics)spellsResult.Read<uint>(4);
                        spellInfo.Attributes = (SpellAttr0)spellsResult.Read<uint>(5);
                        spellInfo.AttributesEx = (SpellAttr1)spellsResult.Read<uint>(6);
                        spellInfo.AttributesEx2 = (SpellAttr2)spellsResult.Read<uint>(7);
                        spellInfo.AttributesEx3 = (SpellAttr3)spellsResult.Read<uint>(8);
                        spellInfo.AttributesEx4 = (SpellAttr4)spellsResult.Read<uint>(9);
                        spellInfo.AttributesEx5 = (SpellAttr5)spellsResult.Read<uint>(10);
                        spellInfo.AttributesEx6 = (SpellAttr6)spellsResult.Read<uint>(11);
                        spellInfo.AttributesEx7 = (SpellAttr7)spellsResult.Read<uint>(12);
                        spellInfo.AttributesEx8 = (SpellAttr8)spellsResult.Read<uint>(13);
                        spellInfo.AttributesEx9 = (SpellAttr9)spellsResult.Read<uint>(14);
                        spellInfo.AttributesEx10 = (SpellAttr10)spellsResult.Read<uint>(15);
                        spellInfo.AttributesEx11 = (SpellAttr11)spellsResult.Read<uint>(16);
                        spellInfo.AttributesEx12 = (SpellAttr12)spellsResult.Read<uint>(17);
                        spellInfo.AttributesEx13 = (SpellAttr13)spellsResult.Read<uint>(18);
                        spellInfo.Stances = spellsResult.Read<long>(19);
                        spellInfo.StancesNot = spellsResult.Read<long>(20);
                        spellInfo.Targets = (SpellCastTargetFlags)spellsResult.Read<uint>(21);
                        spellInfo.TargetCreatureType = spellsResult.Read<int>(22);
                        spellInfo.RequiresSpellFocus = spellsResult.Read<int>(23);
                        spellInfo.FacingCasterFlags = spellsResult.Read<uint>(24);
                        spellInfo.CasterAuraState = (AuraStateType)spellsResult.Read<uint>(25);
                        spellInfo.TargetAuraState = (AuraStateType)spellsResult.Read<uint>(26);
                        spellInfo.ExcludeCasterAuraState = (AuraStateType)spellsResult.Read<uint>(27);
                        spellInfo.ExcludeTargetAuraState = (AuraStateType)spellsResult.Read<uint>(28);
                        spellInfo.CasterAuraSpell = spellsResult.Read<int>(29);
                        spellInfo.TargetAuraSpell = spellsResult.Read<int>(30);
                        spellInfo.ExcludeCasterAuraSpell = spellsResult.Read<int>(31);
                        spellInfo.ExcludeTargetAuraSpell = spellsResult.Read<int>(32);
                        spellInfo.CastTimeEntry = CliDB.SpellCastTimesStorage.LookupByKey(spellsResult.Read<int>(33));
                        spellInfo.RecoveryTime = (Milliseconds)spellsResult.Read<int>(34);
                        spellInfo.CategoryRecoveryTime = (Milliseconds)spellsResult.Read<int>(35);
                        spellInfo.StartRecoveryCategory = (Milliseconds)spellsResult.Read<int>(36);
                        spellInfo.StartRecoveryTime = (Milliseconds)spellsResult.Read<int>(37);
                        spellInfo.InterruptFlags = (SpellInterruptFlags)spellsResult.Read<uint>(38);
                        spellInfo.AuraInterruptFlags = (SpellAuraInterruptFlags)spellsResult.Read<uint>(39);
                        spellInfo.AuraInterruptFlags2 = (SpellAuraInterruptFlags2)spellsResult.Read<uint>(40);
                        spellInfo.ChannelInterruptFlags = (SpellAuraInterruptFlags)spellsResult.Read<uint>(41);
                        spellInfo.ChannelInterruptFlags2 = (SpellAuraInterruptFlags2)spellsResult.Read<uint>(42);
                        spellInfo.ProcFlags = new ProcFlagsInit((ProcFlags)spellsResult.Read<int>(43), (ProcFlags2)spellsResult.Read<int>(44));
                        spellInfo.ProcChance = spellsResult.Read<int>(45);
                        spellInfo.ProcCharges = spellsResult.Read<int>(46);
                        spellInfo.ProcCooldown = (Milliseconds)spellsResult.Read<int>(47);
                        spellInfo.ProcBasePPM = spellsResult.Read<float>(48);
                        spellInfo.MaxLevel = spellsResult.Read<int>(49);
                        spellInfo.BaseLevel = spellsResult.Read<int>(50);
                        spellInfo.SpellLevel = spellsResult.Read<int>(51);
                        spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(spellsResult.Read<int>(52));
                        spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(spellsResult.Read<int>(53));
                        spellInfo.Speed = new Speed(spellsResult.Read<float>(54));
                        spellInfo.LaunchDelay = new Speed(spellsResult.Read<float>(55)).AsDelayMS; //TODO: Convert to int (ms)
                        spellInfo.StackAmount = spellsResult.Read<int>(56);
                        spellInfo.EquippedItemClass = (ItemClass)spellsResult.Read<int>(57);
                        spellInfo.EquippedItemSubClassMask = spellsResult.Read<int>(58);
                        spellInfo.EquippedItemInventoryTypeMask = spellsResult.Read<int>(59);
                        spellInfo.ContentTuningId = spellsResult.Read<int>(60);
                        //spellInfo.SpellName (61)
                        spellInfo.ConeAngle = spellsResult.Read<float>(62);
                        spellInfo.Width = spellsResult.Read<float>(63);
                        spellInfo.MaxTargetLevel = spellsResult.Read<int>(64);
                        spellInfo.MaxAffectedTargets = spellsResult.Read<int>(65);
                        spellInfo.SpellFamilyName = (SpellFamilyNames)spellsResult.Read<uint>(66);
                        spellInfo.SpellFamilyFlags = new FlagArray128(spellsResult.Read<uint>(67), spellsResult.Read<uint>(68), spellsResult.Read<uint>(69), spellsResult.Read<uint>(70));
                        spellInfo.DmgClass = (SpellDmgClass)spellsResult.Read<uint>(71);
                        spellInfo.PreventionType = (SpellPreventionType)spellsResult.Read<uint>(72);
                        spellInfo.RequiredAreasID = spellsResult.Read<int>(73);
                        spellInfo.SchoolMask = (SpellSchoolMask)spellsResult.Read<uint>(74);
                        spellInfo.ChargeCategoryId = (SpellCategories)spellsResult.Read<int>(75);

                        mSpellInfoMap.Add(spellId, spellInfo);

                    } while (spellsResult.NextRow());
                }
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {mServersideSpellNames.Count} serverside spells {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellInfoCustomAttributes()
        {
            RelativeTime oldMSTime = Time.NowRelative;
            RelativeTime oldMSTime2 = oldMSTime;

            SQLResult result = DB.World.Query("SELECT entry, attributes FROM spell_custom_attr");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading,
                    "Loaded 0 spell custom attributes from DB. DB table `spell_custom_attr` is empty.");
            }
            else
            {
                uint count = 0;
                do
                {
                    int spellId = result.Read<int>(0);
                    uint attributes = result.Read<uint>(1);

                    var spells = _GetSpellInfo(spellId);
                    if (spells.Empty())
                    {
                        Log.outError(LogFilter.Sql,
                            $"Table `spell_custom_attr` has wrong spell (entry: {spellId}), ignored.");
                        continue;
                    }

                    foreach (SpellInfo spellInfo in spells)
                    {
                        if (attributes.HasAnyFlag((uint)SpellCustomAttributes.ShareDamage))
                        {
                            if (!spellInfo.HasEffect(SpellEffectName.SchoolDamage))
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Spell {spellId} listed in table `spell_custom_attr` " +
                                    $"with SPELL_ATTR0_CU_SHARE_DAMAGE has no SPELL_EFFECT_SCHOOL_DAMAGE, ignored.");
                                continue;
                            }
                        }

                        spellInfo.AttributesCu |= (SpellCustomAttributes)attributes;
                    }
                    ++count;
                } while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, 
                    $"Loaded {count} spell custom attributes from DB in {Time.Diff(oldMSTime2)} ms.");
            }

            List<int> talentSpells = new();
            foreach (var talentInfo in CliDB.TalentStorage.Values)
            {
                if (talentInfo != null)
                {
                    foreach (int spellRank in talentInfo.SpellRank)
                        talentSpells.Add(spellRank);
                }
            }

            foreach (var spellInfo in mSpellInfoMap.Values)
            {
                foreach (var spellEffectInfo in spellInfo.GetEffects())
                {
                    // all bleed effects and spells ignore armor
                    if ((spellInfo.GetEffectMechanicMask(spellEffectInfo.EffectIndex) & (1ul << (int)Mechanics.Bleed)) != 0)
                        spellInfo.AttributesCu |= SpellCustomAttributes.IgnoreArmor;

                    switch (spellEffectInfo.ApplyAuraName)
                    {
                        case AuraType.ModPossess:
                        case AuraType.ModConfuse:
                        case AuraType.ModCharm:
                        case AuraType.AoeCharm:
                        case AuraType.ModFear:
                        case AuraType.ModStun:
                            spellInfo.AttributesCu |= SpellCustomAttributes.AuraCC;
                            break;
                    }

                    switch (spellEffectInfo.ApplyAuraName)
                    {
                        case AuraType.OpenStable:    // No point in saving this, since the stable dialog can't be open on aura load anyway.
                        // Auras that require both caster & target to be in world cannot be saved
                        case AuraType.ControlVehicle:
                        case AuraType.BindSight:
                        case AuraType.ModPossess:
                        case AuraType.ModCharm:
                        case AuraType.AoeCharm:
                        // Controlled by Battleground
                        case AuraType.BattleGroundPlayerPosition:
                        case AuraType.BattleGroundPlayerPositionFactional:
                            spellInfo.AttributesCu |= SpellCustomAttributes.AuraCannotBeSaved;
                            break;
                    }

                    switch (spellEffectInfo.Effect)
                    {
                        case SpellEffectName.SchoolDamage:
                        case SpellEffectName.HealthLeech:
                        case SpellEffectName.Heal:
                        case SpellEffectName.WeaponDamageNoSchool:
                        case SpellEffectName.WeaponPercentDamage:
                        case SpellEffectName.WeaponDamage:
                        case SpellEffectName.PowerBurn:
                        case SpellEffectName.HealMechanical:
                        case SpellEffectName.NormalizedWeaponDmg:
                        case SpellEffectName.HealPct:
                        case SpellEffectName.DamageFromMaxHealthPCT:
                            spellInfo.AttributesCu |= SpellCustomAttributes.CanCrit;
                            break;
                    }

                    switch (spellEffectInfo.Effect)
                    {
                        case SpellEffectName.SchoolDamage:
                        case SpellEffectName.WeaponDamage:
                        case SpellEffectName.WeaponDamageNoSchool:
                        case SpellEffectName.NormalizedWeaponDmg:
                        case SpellEffectName.WeaponPercentDamage:
                        case SpellEffectName.Heal:
                            spellInfo.AttributesCu |= SpellCustomAttributes.DirectDamage;
                            break;
                        case SpellEffectName.PowerDrain:
                        case SpellEffectName.PowerBurn:
                        case SpellEffectName.HealMaxHealth:
                        case SpellEffectName.HealthLeech:
                        case SpellEffectName.HealPct:
                        case SpellEffectName.EnergizePct:
                        case SpellEffectName.Energize:
                        case SpellEffectName.HealMechanical:
                            spellInfo.AttributesCu |= SpellCustomAttributes.NoInitialThreat;
                            break;
                        case SpellEffectName.Charge:
                        case SpellEffectName.ChargeDest:
                        case SpellEffectName.Jump:
                        case SpellEffectName.JumpDest:
                        case SpellEffectName.LeapBack:
                            spellInfo.AttributesCu |= SpellCustomAttributes.Charge;
                            break;
                        case SpellEffectName.Pickpocket:
                            spellInfo.AttributesCu |= SpellCustomAttributes.PickPocket;
                            break;
                        case SpellEffectName.EnchantItem:
                        case SpellEffectName.EnchantItemTemporary:
                        case SpellEffectName.EnchantItemPrismatic:
                        case SpellEffectName.EnchantHeldItem:
                        {
                            // only enchanting profession enchantments procs can stack
                            if (IsPartOfSkillLine(SkillType.Enchanting, spellInfo.Id))
                            {
                                int enchantId = spellEffectInfo.MiscValue;
                                var enchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchantId);
                                if (enchant == null)
                                    break;

                                for (var s = 0; s < ItemConst.MaxItemEnchantmentEffects; ++s)
                                {
                                    if (enchant.Effect(s) != ItemEnchantmentType.CombatSpell)
                                        continue;

                                    foreach (SpellInfo procInfo in _GetSpellInfo(enchant.EffectArg[s]))
                                    {

                                        // if proced directly from enchantment, not via proc aura
                                        // NOTE: Enchant Weapon - Blade Ward also has proc aura spell and is proced directly
                                        // however its not expected to stack so this check is good
                                        if (procInfo.HasAura(AuraType.ProcTriggerSpell))
                                            continue;

                                        procInfo.AttributesCu |= SpellCustomAttributes.EnchantProc;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }

                // spells ignoring hit result should not be binary
                if (!spellInfo.HasAttribute(SpellAttr3.AlwaysHit))
                {
                    bool setFlag = false;
                    foreach (var spellEffectInfo in spellInfo.GetEffects())
                    {
                        if (spellEffectInfo.IsEffect())
                        {
                            switch (spellEffectInfo.Effect)
                            {
                                case SpellEffectName.SchoolDamage:
                                case SpellEffectName.WeaponDamage:
                                case SpellEffectName.WeaponDamageNoSchool:
                                case SpellEffectName.NormalizedWeaponDmg:
                                case SpellEffectName.WeaponPercentDamage:
                                case SpellEffectName.TriggerSpell:
                                case SpellEffectName.TriggerSpellWithValue:
                                    break;
                                case SpellEffectName.PersistentAreaAura:
                                case SpellEffectName.ApplyAura:
                                case SpellEffectName.ApplyAreaAuraParty:
                                case SpellEffectName.ApplyAreaAuraRaid:
                                case SpellEffectName.ApplyAreaAuraFriend:
                                case SpellEffectName.ApplyAreaAuraEnemy:
                                case SpellEffectName.ApplyAreaAuraPet:
                                case SpellEffectName.ApplyAreaAuraOwner:
                                case SpellEffectName.ApplyAuraOnPet:
                                case SpellEffectName.ApplyAreaAuraSummons:
                                case SpellEffectName.ApplyAreaAuraPartyNonrandom:
                                {
                                    if (spellEffectInfo.ApplyAuraName == AuraType.PeriodicDamage ||
                                        spellEffectInfo.ApplyAuraName == AuraType.PeriodicDamagePercent ||
                                        spellEffectInfo.ApplyAuraName == AuraType.PeriodicDummy ||
                                        spellEffectInfo.ApplyAuraName == AuraType.PeriodicLeech ||
                                        spellEffectInfo.ApplyAuraName == AuraType.PeriodicHealthFunnel ||
                                        spellEffectInfo.ApplyAuraName == AuraType.PeriodicDummy)
                                        break;

                                    goto default;
                                }
                                default:
                                {
                                    // No value and not interrupt cast or crowd control without SPELL_ATTR0_UNAFFECTED_BY_INVULNERABILITY flag
                                    if (spellEffectInfo.CalcValue() == 0 && !((spellEffectInfo.Effect == SpellEffectName.InterruptCast || spellInfo.HasAttribute(SpellCustomAttributes.AuraCC)) && !spellInfo.HasAttribute(SpellAttr0.NoImmunities)))
                                        break;

                                    // Sindragosa Frost Breath
                                    if (spellInfo.Id == 69649 || spellInfo.Id == 71056 || spellInfo.Id == 71057 || spellInfo.Id == 71058 || spellInfo.Id == 73061 || spellInfo.Id == 73062 || spellInfo.Id == 73063 || spellInfo.Id == 73064)
                                        break;

                                    // Frostbolt
                                    if (spellInfo.SpellFamilyName == SpellFamilyNames.Mage && spellInfo.SpellFamilyFlags[0].HasAnyFlag(0x20u))
                                        break;

                                    // Frost Fever
                                    if (spellInfo.Id == 55095)
                                        break;

                                    // Haunt
                                    if (spellInfo.SpellFamilyName == SpellFamilyNames.Warlock && spellInfo.SpellFamilyFlags[1].HasAnyFlag(0x40000u))
                                        break;

                                    setFlag = true;
                                    break;
                                }
                            }

                            if (setFlag)
                            {
                                spellInfo.AttributesCu |= SpellCustomAttributes.BinarySpell;
                                break;
                            }
                        }
                    }
                }

                // Remove normal school mask to properly calculate damage
                if (spellInfo.SchoolMask.HasAnyFlag(SpellSchoolMask.Normal) 
                    && spellInfo.SchoolMask.HasAnyFlag(SpellSchoolMask.Magic))
                {
                    spellInfo.SchoolMask &= ~SpellSchoolMask.Normal;
                    spellInfo.AttributesCu |= SpellCustomAttributes.SchoolmaskNormalWithMagic;
                }

                spellInfo.InitializeSpellPositivity();

                if (talentSpells.Contains(spellInfo.Id))
                    spellInfo.AttributesCu |= SpellCustomAttributes.IsTalent;

                if (MathFunctions.fuzzyNe(spellInfo.Width, 0.0f))
                    spellInfo.AttributesCu |= SpellCustomAttributes.ConeLine;

                switch (spellInfo.SpellFamilyName)
                {
                    case SpellFamilyNames.Warrior:
                        // Shout / Piercing Howl
                        if (spellInfo.SpellFamilyFlags[0].HasAnyFlag(0x20000u)/* || spellInfo.SpellFamilyFlags[1] & 0x20*/)
                            spellInfo.AttributesCu |= SpellCustomAttributes.AuraCC;
                        break;
                    case SpellFamilyNames.Druid:
                        // Roar
                        if (spellInfo.SpellFamilyFlags[0].HasAnyFlag(0x8u))
                            spellInfo.AttributesCu |= SpellCustomAttributes.AuraCC;
                        break;
                    case SpellFamilyNames.Generic:
                        // Stoneclaw Totem effect
                        if (spellInfo.Id == 5729)
                            spellInfo.AttributesCu |= SpellCustomAttributes.AuraCC;
                        break;
                    default:
                        break;
                }

                spellInfo._InitializeExplicitTargetMask();

                if (spellInfo.Speed > 0.0f)
                {
                    bool visualNeedsAmmo(SpellXSpellVisualRecord spellXspellVisual)
                    {
                        SpellVisualRecord spellVisual = CliDB.SpellVisualStorage.LookupByKey(spellXspellVisual.SpellVisualID);
                        if (spellVisual == null)
                            return false;

                        var spellVisualMissiles = Global.DB2Mgr.GetSpellVisualMissiles(spellVisual.SpellVisualMissileSetID);
                        if (spellVisualMissiles.Empty())
                            return false;

                        foreach (SpellVisualMissileRecord spellVisualMissile in spellVisualMissiles)
                        {
                            var spellVisualEffectName = 
                                CliDB.SpellVisualEffectNameStorage.LookupByKey(spellVisualMissile.SpellVisualEffectNameID);

                            if (spellVisualEffectName == null)
                                continue;

                            SpellVisualEffectNameType type = (SpellVisualEffectNameType)spellVisualEffectName.Type;
                            if (type == SpellVisualEffectNameType.UnitAmmoBasic || type == SpellVisualEffectNameType.UnitAmmoPreferred)
                                return true;
                        }

                        return false;
                    }

                    foreach (SpellXSpellVisualRecord spellXspellVisual in spellInfo.GetSpellVisuals())
                    {
                        if (visualNeedsAmmo(spellXspellVisual))
                        {
                            spellInfo.AttributesCu |= SpellCustomAttributes.NeedsAmmoData;
                            break;
                        }
                    }
                }

                // Saving to DB happens before removing from world - skip saving these auras
                if (spellInfo.HasAuraInterruptFlag(SpellAuraInterruptFlags.LeaveWorld))
                    spellInfo.AttributesCu |= SpellCustomAttributes.AuraCannotBeSaved;
            }

            // addition for binary spells, omit spells triggering other spells
            foreach (var spellInfo in mSpellInfoMap.Values)
            {
                if (!spellInfo.HasAttribute(SpellCustomAttributes.BinarySpell))
                {
                    bool allNonBinary = true;
                    bool overrideAttr = false;
                    foreach (var spellEffectInfo in spellInfo.GetEffects())
                    {
                        if (spellEffectInfo.IsAura() && spellEffectInfo.TriggerSpell != 0)
                        {
                            switch (spellEffectInfo.ApplyAuraName)
                            {
                                case AuraType.PeriodicTriggerSpell:
                                case AuraType.PeriodicTriggerSpellFromClient:
                                case AuraType.PeriodicTriggerSpellWithValue:
                                    SpellInfo triggerSpell = 
                                        Global.SpellMgr.GetSpellInfo(spellEffectInfo.TriggerSpell, Difficulty.None);
                                    
                                    if (triggerSpell != null)
                                    {
                                        overrideAttr = true;
                                        if (triggerSpell.HasAttribute(SpellCustomAttributes.BinarySpell))
                                            allNonBinary = false;
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    if (overrideAttr && allNonBinary)
                        spellInfo.AttributesCu &= ~SpellCustomAttributes.BinarySpell;
                }

                // remove attribute from spells that can't crit
                if (spellInfo.HasAttribute(SpellCustomAttributes.CanCrit))
                    if (spellInfo.HasAttribute(SpellAttr2.CantCrit))
                        spellInfo.AttributesCu &= ~SpellCustomAttributes.CanCrit;
            }

            // add custom attribute to liquid auras
            foreach (var liquid in CliDB.LiquidTypeStorage.Values)
            {
                if (liquid.SpellID != 0)
                {
                    foreach (SpellInfo spellInfo in _GetSpellInfo(liquid.SpellID))
                        spellInfo.AttributesCu |= SpellCustomAttributes.AuraCannotBeSaved;
                }
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded SpellInfo custom attributes in {Time.Diff(oldMSTime)} ms.");
        }

        void ApplySpellFix(int[] spellIds, Action<SpellInfo> fix)
        {
            foreach (int spellId in spellIds)
            {
                var range = _GetSpellInfo(spellId);
                if (range == null)
                {
                    Log.outError(LogFilter.ServerLoading, 
                        $"Spell info correction specified for non-existing spell {spellId}");
                    continue;
                }

                foreach (SpellInfo spellInfo in range)
                    fix(spellInfo);
            }
        }

        void ApplySpellEffectFix(SpellInfo spellInfo, int effectIndex, Action<SpellEffectInfo> fix)
        {
            if (spellInfo.GetEffects().Count <= effectIndex)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Spell effect info correction specified " +
                    $"for non-existing effect {effectIndex} of spell {spellInfo.Id}");
                return;
            }

            fix(spellInfo.GetEffect(effectIndex));
        }

        public void LoadSpellInfoCorrections()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            // Some spells have no amplitude set
            {
                ApplySpellFix([
                    6727,  // Poison Mushroom
                    7331,  // Healing Aura (TEST) (Rank 1)
                    /*
                    30400, // Nether Beam - Perseverance
                        Blizzlike to have it disabled? DBC says:
                        "This is currently turned off to increase performance. Enable this to make it fire more frequently."
                    */
                    34589, // Dangerous Water
                    52562, // Arthas Zombie Catcher
                    57550, // Tirion Aggro
                    65755
                ], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                    {
                        spellEffectInfo.ApplyAuraPeriod = (Seconds)1;
                    });
                });

                ApplySpellFix([
                    24707, // Food
                    26263, // Dim Sum
                    29055  // Refreshing Red Apple
                ], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 1, spellEffectInfo =>
                    {
                        spellEffectInfo.ApplyAuraPeriod = (Seconds)1;
                    });
                });

                // Karazhan - Chess NPC AI, action timer
                ApplySpellFix([37504], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 1, spellEffectInfo =>
                    {
                        spellEffectInfo.ApplyAuraPeriod = (Seconds)5;
                    });
                });

                // Vomit
                ApplySpellFix([43327], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 1, spellEffectInfo =>
                    {
                        spellEffectInfo.ApplyAuraPeriod = (Seconds)1;
                    });
                });
            }

            // specific code for cases with no trigger spell provided in field
            {
                // Brood Affliction: Bronze
                ApplySpellFix([23170], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                    {
                        spellEffectInfo.TriggerSpell = 23171;
                    });
                });

                // Feed Captured Animal
                ApplySpellFix([29917], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                    {
                        spellEffectInfo.TriggerSpell = 29916;
                    });
                });

                // Remote Toy
                ApplySpellFix([37027], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                    {
                        spellEffectInfo.TriggerSpell = 37029;
                    });
                });

                // Eye of Grillok
                ApplySpellFix([38495], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                    {
                        spellEffectInfo.TriggerSpell = 38530;
                    });
                });

                // Tear of Azzinoth Summon Channel - it's not really supposed to do anything,
                // and this only prevents the console spam
                ApplySpellFix([39857], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                    {
                        spellEffectInfo.TriggerSpell = 39856;
                    });
                });

                // Personalized Weather
                ApplySpellFix([46736], spellInfo =>
                {
                    ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                    {
                        spellEffectInfo.TriggerSpell = 46737;
                        spellEffectInfo.ApplyAuraName = AuraType.PeriodicTriggerSpell;
                    });
                });
            }

            // Allows those to crit
            ApplySpellFix([
                379,   // Earth Shield
                71607, // Item - Bauble of True Blood 10m
                71646, // Item - Bauble of True Blood 25m
                71610, // Item - Althor's Abacus trigger 10m
                71641  // Item - Althor's Abacus trigger 25m
            ], spellInfo =>
            {
                // We need more spells to find a general way (if there is any)
                spellInfo.DmgClass = SpellDmgClass.Magic;
            });

            ApplySpellFix([
                63026, // Summon Aspirant Test NPC (HACK: Target shouldn't be changed)
                63137  // Summon Valiant Test (HACK: Target shouldn't be changed; summon position should be untied from spell destination)
            ], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.DestDb);
                });
            });

            // Summon Skeletons
            ApplySpellFix([52611, 52612], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.MiscValueB = 64;
                });
            });

            ApplySpellFix([
                40244, // Simon Game Visual
                40245, // Simon Game Visual
                40246, // Simon Game Visual
                40247, // Simon Game Visual
                42835  // Spout, remove damage effect, only anim is needed
            ], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.Effect = SpellEffectName.None;
                });
            });

            ApplySpellFix([
                63665, // Charge (Argent Tournament emote on riders)
                31298, // Sleep (needs target selection script)
                51904, // Summon Ghouls On Scarlet Crusade (this should use conditions table, script for this spell needs to be fixed)
                68933, // Wrath of Air Totem rank 2 (Aura)
                29200  // Purify Helboar Meat
            ], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.UnitCaster);
                    spellEffectInfo.TargetB = new SpellImplicitTargetInfo();
                });
            });

            ApplySpellFix([
                56690, // Thrust Spear
                60586, // Mighty Spear Thrust
                60776, // Claw Swipe
                60881, // Fatal Strike
                60864  // Jaws of Death
           ], spellInfo =>
           {
               spellInfo.AttributesEx4 |= SpellAttr4.IgnoreDamageTakenModifiers;
           });

            // Howl of Azgalor
            ApplySpellFix([31344], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards100); // 100yards instead of 50000?!
                });
            });

            ApplySpellFix([
                42818, // Headless Horseman - Wisp Flight Port
                42821  // Headless Horseman - Wisp Flight Missile
            ], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(6); // 100 yards
            });

            // They Must Burn Bomb Aura (self)
            ApplySpellFix([36350], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TriggerSpell = 36325; // They Must Burn Bomb Drop (DND)
                });
            });

            ApplySpellFix([
                31347, // Doom
                36327, // Shoot Arcane Explosion Arrow
                39365, // Thundering Storm
                41071, // Raise Dead (HACK)
                42442, // Vengeance Landing Cannonfire
                42611, // Shoot
                44978, // Wild Magic
                45001, // Wild Magic
                45002, // Wild Magic
                45004, // Wild Magic
                45006, // Wild Magic
                45010, // Wild Magic
                45761, // Shoot Gun
                45863, // Cosmetic - Incinerate to Random Target
                48246, // Ball of Flame
                41635, // Prayer of Mending
                44869, // Spectral Blast
                45027, // Revitalize
                45976, // Muru Portal Channel
                52124, // Sky Darkener Assault
                52479, // Gift of the Harvester
                61588, // Blazing Harpoon
                55479, // Force Obedience
                28560, // Summon Blizzard (Sapphiron)
                53096, // Quetz'lun's Judgment
                70743, // AoD Special
                70614, // AoD Special - Vegard
                4020,  // Safirdrang's Chill
                52438, // Summon Skittering Swarmer (Force Cast)
                52449, // Summon Skittering Infector (Force Cast)
                53609, // Summon Anub'ar Assassin (Force Cast)
                53457, // Summon Impale Trigger (AoE)
                45907, // Torch Target Picker
                52953, // Torch
                58121, // Torch
                43109, // Throw Torch
                58552, // Return to Orgrimmar
                58533, // Return to Stormwind
                21855, // Challenge Flag
                38762, // Force of Neltharaku
                51122, // Fierce Lightning Stike
                71848, // Toxic Wasteling Find Target
                36146, // Chains of Naberius
                33711, // Murmur's Touch
                38794  // Murmur's Touch
            ], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 1;
            });

            ApplySpellFix([
                36384, // Skartax Purple Beam
                47731  // Critter
            ], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 2;
            });

            ApplySpellFix([
                28542, // Life Drain - Sapphiron
                29213, // Curse of the Plaguebringer - Noth
                29576, // Multi-Shot
                37790, // Spread Shot
                39992, // Needle Spine
                40816, // Saber Lash
                41303, // Soul Drain
                41376, // Spite
                45248, // Shadow Blades
                46771, // Flame Sear
                66588  // Flaming Spear
            ], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 3;
            });

            ApplySpellFix([
                38310, // Multi-Shot
                53385  // Divine Storm (Damage)
            ], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 4;
            });

            ApplySpellFix([
                42005, // Bloodboil
                38296, // Spitfire Totem
                37676, // Insidious Whisper
                46008, // Negative Energy
                45641, // Fire Bloom
                55665, // Life Drain - Sapphiron (H)
                28796, // Poison Bolt Volly - Faerlina
                37135  // Domination
            ], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 5;
            });

            ApplySpellFix([
                40827, // Sinful Beam
                40859, // Sinister Beam
                40860, // Vile Beam
                40861, // Wicked Beam
                54098, // Poison Bolt Volly - Faerlina (H)
                54835  // Curse of the Plaguebringer - Noth (H)
            ], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 10;
            });

            // Unholy Frenzy
            ApplySpellFix([50312], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 15;
            });

            // Fingers of Frost
            ApplySpellFix([44544], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.SpellClassMask[0] |= 0x20000;
                });
            });

            ApplySpellFix([
                52212, // Death and Decay
                41485, // Deadly Poison - Black Temple
                41487  // Envenom - Black Temple
            ], spellInfo =>
            {
                spellInfo.AttributesEx6 |= SpellAttr6.IgnorePhaseShift;
            });

            // Oscillation Field
            ApplySpellFix([37408], spellInfo =>
            {
                spellInfo.AttributesEx3 |= SpellAttr3.DotStackingRule;
            });

            // Crafty's Ultra-Advanced Proto-Typical Shortening Blaster
            ApplySpellFix([51912], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.ApplyAuraPeriod = (Seconds)3;
                });
            });

            // Nether Portal - Perseverence
            ApplySpellFix([30421], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 2, spellEffectInfo =>
                {
                    spellEffectInfo.BasePoints += 30000;
                });
            });

            // Parasitic Shadowfiend Passive
            ApplySpellFix([41913], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.ApplyAuraName = AuraType.Dummy; // proc debuff, and summon infinite fiends
                });
            });

            ApplySpellFix([
                27892, // To Anchor 1
                27928, // To Anchor 1
                27935, // To Anchor 1
            ], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards10);
                });
            });

            // Wrath of the Plaguebringer
            ApplySpellFix([29214, 54836], spellInfo =>
            {
                // target allys instead of enemies, target A is src_caster, spells with effect like that have ally target
                // this is the only known exception, probably just wrong data
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetB = new SpellImplicitTargetInfo(Targets.UnitSrcAreaAlly);
                });
                ApplySpellEffectFix(spellInfo, 1, spellEffectInfo =>
                {
                    spellEffectInfo.TargetB = new SpellImplicitTargetInfo(Targets.UnitSrcAreaAlly);
                });
            });

            // Earthbind Totem (instant pulse)
            ApplySpellFix([6474], spellInfo =>
            {
                spellInfo.AttributesEx5 |= SpellAttr5.ExtraInitialPeriod;
            });

            ApplySpellFix([
                70728, // Exploit Weakness (needs target selection script)
                70840  // Devious Minds (needs target selection script)
            ], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.UnitCaster);
                    spellEffectInfo.TargetB = new SpellImplicitTargetInfo(Targets.UnitPet);
                });
            });

            // Ride Carpet
            ApplySpellFix([45602], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.BasePoints = 0; // force seat 0, vehicle doesn't have the required seat flags for "no seat specified (-1)"
                });
            });

            // Easter Lay Noblegarden Egg Aura - Interrupt flags copied from aura which this aura is linked with
            ApplySpellFix([61719], spellInfo =>
            {
                spellInfo.AuraInterruptFlags = SpellAuraInterruptFlags.HostileActionReceived | SpellAuraInterruptFlags.Damage;
            });

            ApplySpellFix([
                71838, // Drain Life - Bryntroll Normal
                71839  // Drain Life - Bryntroll Heroic
            ], spellInfo =>
            {
                spellInfo.AttributesEx2 |= SpellAttr2.CantCrit;
            });

            ApplySpellFix([
                51597, // Summon Scourged Captive
                56606, // Ride Jokkum
                61791  // Ride Vehicle (Yogg-Saron)
            ], spellInfo =>
            {
                /// @todo: remove this when basepoints of all Ride Vehicle auras are calculated correctly
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.BasePoints = 1;
                });
            });

            // Summon Scourged Captive
            ApplySpellFix([51597], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.Scaling.Variance = 0.0f;
                });
            });

            // Black Magic
            ApplySpellFix([59630], spellInfo =>
            {
                spellInfo.Attributes |= SpellAttr0.Passive;
            });

            // Paralyze
            ApplySpellFix([48278], spellInfo =>
            {
                spellInfo.AttributesEx3 |= SpellAttr3.DotStackingRule;
            });

            ApplySpellFix([
                51798, // Brewfest - Relay Race - Intro - Quest Complete
                47134  // Quest Complete
            ], spellInfo =>
            {
                //! HACK: This spell break quest complete for alliance and on retail not used
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.Effect = SpellEffectName.None;
                });
            });

            // Siege Cannon (Tol Barad)
            ApplySpellFix([85123], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards200);
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.UnitSrcAreaEntry);
                });
            });

            // Gathering Storms
            ApplySpellFix([198300], spellInfo =>
            {
                spellInfo.ProcCharges = 1; // override proc charges, has 0 (unlimited) in db2
            });

            ApplySpellFix([
                15538, // Gout of Flame
                42490, // Energized!
                42492, // Cast Energized
                43115  // Plague Vial
            ], spellInfo =>
            {
                spellInfo.AttributesEx |= SpellAttr1.NoThreat;
            });

            // Test Ribbon Pole Channel
            ApplySpellFix([29726], spellInfo =>
            {
                spellInfo.ChannelInterruptFlags &= ~SpellAuraInterruptFlags.Action;
            });

            // Sic'em
            ApplySpellFix([42767], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.UnitNearbyEntry);
                });
            });

            // Burn Body
            ApplySpellFix([42793], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 2, spellEffectInfo =>
                {
                    spellEffectInfo.MiscValue = 24008; // Fallen Combatant
                });
            });

            // Gift of the Naaru (priest and monk variants)
            ApplySpellFix([59544, 121093], spellInfo =>
            {
                spellInfo.SpellFamilyFlags[2] = 0x80000000;
            });

            ApplySpellFix([
                50661, // Weakened Resolve
                68979, // Unleashed Souls
                48714, // Compelled
                7853,  // The Art of Being a Water Terror: Force Cast on Player
            ], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(13); // 50000yd
            });

            ApplySpellFix([
                44327, // Trained Rock Falcon/Hawk Hunting
                44408  // Trained Rock Falcon/Hawk Hunting
             ], spellInfo =>
             {
                 spellInfo.Speed = Speed.Zero;
             });

            // Summon Corpse Scarabs
            ApplySpellFix([28864, 29105], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards10);
                });
            });

            // Tag Greater Felfire Diemetradon
            ApplySpellFix([
                37851, // Tag Greater Felfire Diemetradon
                37918  // Arcano-pince
            ], spellInfo =>
            {
                spellInfo.RecoveryTime = (Seconds)3;
            });

            // Jormungar Strike
            ApplySpellFix([56513], spellInfo =>
            {
                spellInfo.RecoveryTime = (Seconds)2;
            });

            ApplySpellFix([
                54997, // Cast Net (tooltip says 10s but sniffs say 6s)
                56524  // Acid Breath
            ], spellInfo =>
            {
                spellInfo.RecoveryTime = (Seconds)6;
            });

            ApplySpellFix([
                47911, // EMP
                48620, // Wing Buffet
                51752  // Stampy's Stompy-Stomp
            ], spellInfo =>
            {
                spellInfo.RecoveryTime = (Seconds)10;
            });

            ApplySpellFix([
                37727, // Touch of Darkness
                54996  // Ice Slick (tooltip says 20s but sniffs say 12s)
            ], spellInfo =>
            {
                spellInfo.RecoveryTime = (Seconds)12;
            });

            // Signal Helmet to Attack
            ApplySpellFix([51748], spellInfo =>
            {
                spellInfo.RecoveryTime = (Seconds)15;
            });

            // Charge
            ApplySpellFix([
                51756, // Charge
                37919, //Arcano-dismantle
                37917  //Arcano-Cloak
            ], spellInfo =>
            {
                spellInfo.RecoveryTime = (Seconds)20;
            });

            // Summon Frigid Bones
            ApplySpellFix([53525], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(4); // 2 minutes
            });

            // Dark Conclave Ritualist Channel
            ApplySpellFix([38469], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(6);  // 100yd
            });

            // Chrono Shift (enemy slow part)
            ApplySpellFix([236299], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(6);  // 100yd
            });

            //
            // VIOLET HOLD SPELLS
            //
            // Water Globule (Ichoron)
            ApplySpellFix([54258, 54264, 54265, 54266, 54267], spellInfo =>
            {
                // in 3.3.5 there is only one radius in dbc which is 0 yards in this case
                // use max radius from 4.3.4
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards25);
                });
            });
            // ENDOF VIOLET HOLD

            //
            // ULDUAR SPELLS
            //
            // Pursued (Flame Leviathan)
            ApplySpellFix([62374], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards50000);   // 50000yd
                });
            });

            // Focused Eyebeam Summon Trigger (Kologarn)
            ApplySpellFix([63342], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 1;
            });

            ApplySpellFix([
                65584, // Growth of Nature (Freya)
                64381  // Strength of the Pack (Auriaya)
            ], spellInfo =>
            {
                spellInfo.AttributesEx3 |= SpellAttr3.DotStackingRule;
            });

            ApplySpellFix([
                63018, // Searing Light (XT-002)
                65121, // Searing Light (25m) (XT-002)
                63024, // Gravity Bomb (XT-002)
                64234  // Gravity Bomb (25m) (XT-002)
            ], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 1;
            });

            ApplySpellFix([
                64386, // Terrifying Screech (Auriaya)
                64389, // Sentinel Blast (Auriaya)
                64678  // Sentinel Blast (Auriaya)
            ], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(28); // 5 seconds, wrong DBC data?
            });

            // Potent Pheromones (Freya)
            ApplySpellFix([64321], spellInfo =>
            {
                // spell should dispel area aura, but doesn't have the attribute
                // may be db data bug, or blizz may keep reapplying area auras every update with checking immunity
                // that will be clear if we get more spells with problem like this
                spellInfo.AttributesEx |= SpellAttr1.ImmunityPurgesEffect;
            });

            // Blizzard (Thorim)
            ApplySpellFix([62576, 62602], spellInfo =>
            {
                // DBC data is wrong for 0, it's a different dynobject target than 1
                // Both effects should be shared by the same DynObject
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.DestCasterLeft);
                });
            });

            // Spinning Up (Mimiron)
            ApplySpellFix([63414], spellInfo =>
            {
                spellInfo.ChannelInterruptFlags = SpellAuraInterruptFlags.None;
                spellInfo.ChannelInterruptFlags2 = SpellAuraInterruptFlags2.None;
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetB = new SpellImplicitTargetInfo(Targets.UnitCaster);
                });
            });

            // Rocket Strike (Mimiron)
            ApplySpellFix([63036], spellInfo =>
            {
                spellInfo.Speed = Speed.Zero;
            });

            // Magnetic Field (Mimiron)
            ApplySpellFix([64668], spellInfo =>
            {
                spellInfo.Mechanic = Mechanics.None;
            });

            // Empowering Shadows (Yogg-Saron)
            ApplySpellFix([64468, 64486], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 3;  // same for both modes?
            });

            // Cosmic Smash (Algalon the Observer)
            ApplySpellFix([62301], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 1;
            });

            // Cosmic Smash (Algalon the Observer)
            ApplySpellFix([64598], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 3;
            });

            // Cosmic Smash (Algalon the Observer)
            ApplySpellFix([62293], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetB = new SpellImplicitTargetInfo(Targets.DestCaster);
                });
            });

            // Cosmic Smash (Algalon the Observer)
            ApplySpellFix([62311, 64596], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(6);  // 100yd
            });

            ApplySpellFix([
                64014, // Expedition Base Camp Teleport
                64024, // Conservatory Teleport
                64025, // Halls of Invention Teleport
                64028, // Colossal Forge Teleport
                64029, // Shattered Walkway Teleport
                64030, // Antechamber Teleport
                64031, // Scrapyard Teleport
                64032, // Formation Grounds Teleport
                65042  // Prison of Yogg-Saron Teleport
            ], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.DestDb);
                });
            });
            // ENDOF ULDUAR SPELLS

            //
            // TRIAL OF THE CRUSADER SPELLS
            //
            // Infernal Eruption
            ApplySpellFix([66258], spellInfo =>
            {
                // increase duration from 15 to 18 seconds because caster is already
                // unsummoned when spell missile hits the ground so nothing happen in result
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(85);
            });
            // ENDOF TRIAL OF THE CRUSADER SPELLS

            //
            // ICECROWN CITADEL SPELLS
            //
            ApplySpellFix([
                70781, // Light's Hammer Teleport
                70856, // Oratory of the Damned Teleport
                70857, // Rampart of Skulls Teleport
                70858, // Deathbringer's Rise Teleport
                70859, // Upper Spire Teleport
                70860, // Frozen Throne Teleport
                70861  // Sindragosa's Lair Teleport
            ], spellInfo =>
            {
                // THESE SPELLS ARE WORKING CORRECTLY EVEN WITHOUT THIS HACK
                // THE ONLY REASON ITS HERE IS THAT CURRENT GRID SYSTEM
                // DOES NOT ALLOW FAR OBJECT SELECTION (dist > 333)
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.DestDb);
                });
            });

            // Shadow's Fate
            ApplySpellFix([71169], spellInfo =>
            {
                spellInfo.AttributesEx3 |= SpellAttr3.DotStackingRule;
            });

            // Resistant Skin (Deathbringer Saurfang adds)
            ApplySpellFix([72723], spellInfo =>
            {
                // this spell initially granted Shadow damage immunity, however it was removed but the data was left in client
                ApplySpellEffectFix(spellInfo, 2, spellEffectInfo =>
                {
                    spellEffectInfo.Effect = SpellEffectName.None;
                });
            });

            // Coldflame Jets (Traps after Saurfang)
            ApplySpellFix([70460], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(1); // 10 seconds
            });

            ApplySpellFix([
                71412, // Green Ooze Summon (Professor Putricide)
                71415  // Orange Ooze Summon (Professor Putricide)
            ], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.UnitTargetAny);
                });
            });

            // Awaken Plagued Zombies
            ApplySpellFix([71159], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(21);
            });

            // Volatile Ooze Beam Protection (Professor Putricide)
            ApplySpellFix([70530], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.Effect = SpellEffectName.ApplyAura; // for an unknown reason this was SPELL_EFFECT_APPLY_AREA_AURA_RAID
                });
            });

            // Mutated Strength (Professor Putricide)
            ApplySpellFix([71604], spellInfo =>
            {
                // THIS IS HERE BECAUSE COOLDOWN ON CREATURE PROCS WERE NOT IMPLEMENTED WHEN THE SCRIPT WAS WRITTEN
                ApplySpellEffectFix(spellInfo, 1, spellEffectInfo =>
                {
                    spellEffectInfo.Effect = SpellEffectName.None;
                });
            });

            // Unbound Plague (Professor Putricide) (needs target selection script)
            ApplySpellFix([70911], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetB = new SpellImplicitTargetInfo(Targets.UnitTargetEnemy);
                });
            });

            // Empowered Flare (Blood Prince Council)
            ApplySpellFix([71708], spellInfo =>
            {
                spellInfo.AttributesEx3 |= SpellAttr3.IgnoreCasterModifiers;
            });

            // Swarming Shadows
            ApplySpellFix([71266], spellInfo =>
            {
                spellInfo.RequiredAreasID = 0; // originally, these require area 4522, which is... outside of Icecrown Citadel
            });

            // Corruption
            ApplySpellFix([70602], spellInfo =>
            {
                spellInfo.AttributesEx3 |= SpellAttr3.DotStackingRule;
            });

            // Column of Frost (visual marker)
            ApplySpellFix([70715], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(32); // 6 seconds (missing)
            });

            // Mana Void (periodic aura)
            ApplySpellFix([71085], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(9); // 30 seconds (missing)
            });

            // Summon Suppressor (needs target selection script)
            ApplySpellFix([70936], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(157); // 90yd
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.UnitTargetAny);
                    spellEffectInfo.TargetB = new SpellImplicitTargetInfo();
                });
            });

            // Sindragosa's Fury
            ApplySpellFix([70598], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.DestDest);
                });
            });

            // Frost Bomb
            ApplySpellFix([69846], spellInfo =>
            {
                spellInfo.Speed = Speed.Zero;    // This spell's summon happens instantly
            });

            // Chilled to the Bone
            ApplySpellFix([70106], spellInfo =>
            {
                spellInfo.AttributesEx3 |= SpellAttr3.IgnoreCasterModifiers;
                spellInfo.AttributesEx6 |= SpellAttr6.IgnoreCasterDamageModifiers;
            });

            // Ice Lock
            ApplySpellFix([71614], spellInfo =>
            {
                spellInfo.Mechanic = Mechanics.Stun;
            });

            // Defile
            ApplySpellFix([72762], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(559); // 53 seconds
            });

            // Defile
            ApplySpellFix([72743], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(22); // 45 seconds
            });

            // Defile
            ApplySpellFix([72754], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards200); // 200yd
                });
                ApplySpellEffectFix(spellInfo, 1, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards200); // 200yd
                });
            });

            // Val'kyr Target Search
            ApplySpellFix([69030], spellInfo =>
            {
                spellInfo.Attributes |= SpellAttr0.NoImmunities;
            });

            // Raging Spirit Visual
            ApplySpellFix([69198], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(13); // 50000yd
            });

            // Harvest Soul
            ApplySpellFix([73655], spellInfo =>
            {
                spellInfo.AttributesEx3 |= SpellAttr3.IgnoreCasterModifiers;
            });

            // Summon Shadow Trap
            ApplySpellFix([73540], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(3); // 60 seconds
            });

            // Shadow Trap (visual)
            ApplySpellFix([73530], spellInfo =>
            {
                spellInfo.DurationEntry = CliDB.SpellDurationStorage.LookupByKey(27); // 3 seconds
            });

            // Summon Spirit Bomb
            ApplySpellFix([74302], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 2;
            });

            // Summon Spirit Bomb
            ApplySpellFix([73579], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards25); // 25yd
                });
            });

            // Raise Dead
            ApplySpellFix([72376], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 3;
            });

            // Jump
            ApplySpellFix([71809], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(5); // 40yd
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards10); // 10yd
                    spellEffectInfo.MiscValue = 190;
                });
            });
            // ENDOF ICECROWN CITADEL SPELLS

            //
            // RUBY SANCTUM SPELLS
            //
            // Soul Consumption
            ApplySpellFix([74799], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 1, spellEffectInfo =>
                {
                    spellEffectInfo.TargetARadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards12);
                });
            });

            // Twilight Mending
            ApplySpellFix([75509], spellInfo =>
            {
                spellInfo.AttributesEx6 |= SpellAttr6.IgnorePhaseShift;
                spellInfo.AttributesEx2 |= SpellAttr2.IgnoreLineOfSight;
            });

            // Awaken Flames
            ApplySpellFix([75888], spellInfo =>
            {
                spellInfo.AttributesEx |= SpellAttr1.ExcludeCaster;
            });
            // ENDOF RUBY SANCTUM SPELLS

            //
            // EYE OF ETERNITY SPELLS
            //
            ApplySpellFix([
                57473, // Arcane Storm bonus explicit visual spell
                57431, // Summon Static Field
                56091, // Flame Spike (Wyrmrest Skytalon)
                56092, // Engulf in Flames (Wyrmrest Skytalon)
                57090, // Revivify (Wyrmrest Skytalon)
                57143  // Life Burst (Wyrmrest Skytalon)
            ], spellInfo =>
            {
                // All spells work even without these changes. The LOS attribute is due to problem
                // from collision between maps & gos with active destroyed state.
                spellInfo.AttributesEx2 |= SpellAttr2.IgnoreLineOfSight;
            });

            // Arcane Barrage (cast by players and NONMELEEDAMAGELOG with caster Scion of Eternity (original caster)).
            ApplySpellFix([63934], spellInfo =>
            {
                // This would never crit on retail and it has attribute for SPELL_ATTR3_NO_DONE_BONUS because is handled from player,
                // until someone figures how to make scions not critting without hack and without making them main casters this should stay here.
                spellInfo.AttributesEx2 |= SpellAttr2.CantCrit;
            });
            // ENDOF EYE OF ETERNITY SPELLS

            ApplySpellFix([
                40055, // Introspection
                40165, // Introspection
                40166, // Introspection
                40167, // Introspection
            ], spellInfo =>
            {
                spellInfo.Attributes |= SpellAttr0.AuraIsDebuff;
            });

            //
            // STONECORE SPELLS
            //
            ApplySpellFix([
                95284, // Teleport (from entrance to Slabhide)
                95285  // Teleport (from Slabhide to entrance)
            ], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetB = new SpellImplicitTargetInfo(Targets.DestDb);
                });
            });
            // ENDOF STONECORE SPELLS

            //
            // HALLS OF ORIGINATION SPELLS
            //
            ApplySpellFix([
                76606, // Disable Beacon Beams L
                76608  // Disable Beacon Beams R
            ], spellInfo =>
            {
                // Little hack, Increase the radius so it can hit the Cave In Stalkers in the platform.
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetBRadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards45);
                });
            });

            // ENDOF HALLS OF ORIGINATION SPELLS

            // Threatening Gaze
            ApplySpellFix([24314], spellInfo =>
            {
                spellInfo.AuraInterruptFlags |= SpellAuraInterruptFlags.Action | SpellAuraInterruptFlags.Moving | SpellAuraInterruptFlags.Anim;
            });

            // Travel Form (dummy) - cannot be cast indoors.
            ApplySpellFix([783], spellInfo =>
            {
                spellInfo.Attributes |= SpellAttr0.OnlyOutdoors;
            });

            // Tree of Life (Passive)
            ApplySpellFix([5420], spellInfo =>
            {
                spellInfo.Stances = 1L << ((int)ShapeShiftForm.TreeForm - 1);
            });

            // Gaze of Occu'thar
            ApplySpellFix([96942], spellInfo =>
            {
                spellInfo.AttributesEx &= ~SpellAttr1.IsChannelled;
            });

            // Evolution
            ApplySpellFix([75610], spellInfo =>
            {
                spellInfo.MaxAffectedTargets = 1;
            });

            // Evolution
            ApplySpellFix([75697], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.UnitSrcAreaEntry);
                });
            });

            //
            // ISLE OF CONQUEST SPELLS
            //
            // Teleport
            ApplySpellFix([66551], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(13); // 50000yd
            });
            // ENDOF ISLE OF CONQUEST SPELLS

            // Aura of Fear
            ApplySpellFix([40453], spellInfo =>
            {
                // Bad DBC data? Copying 25820 here due to spell description
                // either is a periodic with Chance on tick, or a proc

                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.ApplyAuraName = AuraType.ProcTriggerSpell;
                    spellEffectInfo.ApplyAuraPeriod = Milliseconds.Zero;
                });
                spellInfo.ProcChance = 10;
            });

            // Survey Sinkholes
            ApplySpellFix([45853], spellInfo =>
            {
                spellInfo.RangeEntry = CliDB.SpellRangeStorage.LookupByKey(5); // 40 yards
            });

            // Baron Rivendare (Stratholme) - Unholy Aura
            ApplySpellFix([17466, 17467], spellInfo =>
            {
                spellInfo.AttributesEx2 |= SpellAttr2.NoInitialThreat;
            });

            // Spore - Spore Visual
            ApplySpellFix([42525], spellInfo =>
            {
                spellInfo.AttributesEx3 |= SpellAttr3.AllowAuraWhileDead;
                spellInfo.AttributesEx2 |= SpellAttr2.AllowDeadTarget;
            });

            // Soul Sickness (Forge of Souls)
            ApplySpellFix([69131], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 1, spellEffectInfo =>
                {
                    spellEffectInfo.ApplyAuraName = AuraType.ModDecreaseSpeed;
                });
            });

            //
            // FIRELANDS SPELLS
            //
            // Torment Searcher
            ApplySpellFix([99253], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetBRadiusEntry = CliDB.SpellRadiusStorage.LookupByKey((int)EffectRadiusIndex.Yards15);
                });
            });

            // Torment Damage
            ApplySpellFix([99256], spellInfo =>
            {
                spellInfo.Attributes |= SpellAttr0.AuraIsDebuff;
            });

            // Blaze of Glory
            ApplySpellFix([99252], spellInfo =>
            {
                spellInfo.AuraInterruptFlags |= SpellAuraInterruptFlags.LeaveWorld;
            });
            // ENDOF FIRELANDS SPELLS

            //
            // ANTORUS THE BURNING THRONE SPELLS
            //

            // Decimation
            ApplySpellFix([244449], spellInfo =>
            {
                // For some reason there is a instakill effect that serves absolutely no purpose.
                // Until we figure out what it's actually used for we disable it.
                ApplySpellEffectFix(spellInfo, 2, spellEffectInfo =>
                {
                    spellEffectInfo.Effect = SpellEffectName.None;
                });
            });

            // ENDOF ANTORUS THE BURNING THRONE SPELLS

            // Summon Master Li Fei
            ApplySpellFix([102445], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.TargetA = new SpellImplicitTargetInfo(Targets.DestDb);
                });
            });

            // Earthquake
            ApplySpellFix([61882], spellInfo =>
            {
                spellInfo.NegativeEffects[2] = true;
            });

            // Headless Horseman Climax - Return Head (Hallow End)
            // Headless Horseman Climax - Body Regen (confuse only - removed on death)
            // Headless Horseman Climax - Head Is Dead
            ApplySpellFix([42401, 43105, 42428], spellInfo =>
            {
                spellInfo.Attributes |= SpellAttr0.NoImmunities;
            });

            // Horde / Alliance switch (BG mercenary system)
            ApplySpellFix([195838, 195843], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    spellEffectInfo.Effect = SpellEffectName.ApplyAura;
                });
                ApplySpellEffectFix(spellInfo, 1, spellEffectInfo =>
                {
                    spellEffectInfo.Effect = SpellEffectName.ApplyAura;
                });
                ApplySpellEffectFix(spellInfo, 2, spellEffectInfo =>
                {
                    spellEffectInfo.Effect = SpellEffectName.ApplyAura;
                });
            });

            // Fire Cannon
            ApplySpellFix([181593], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    // This spell never triggers, theory is that it was supposed to be only triggered until target reaches some health percentage
                    // but was broken and always caused visuals to break, then target was changed to immediately spawn with desired health
                    // leaving old data in db2
                    spellEffectInfo.TriggerSpell = 0;
                });
            });

            ApplySpellFix([265057], spellInfo =>
            {
                ApplySpellEffectFix(spellInfo, 0, spellEffectInfo =>
                {
                    // Fix incorrect spell id (it has self in TriggerSpell)
                    spellEffectInfo.TriggerSpell = 16403;
                });
            });

            // Ray of Frost (Fingers of Frost charges)
            ApplySpellFix([269748], spellInfo =>
            {
                spellInfo.AttributesEx &= ~SpellAttr1.IsChannelled;
            });

            // Burning Rush
            ApplySpellFix([111400], spellInfo =>
            {
                spellInfo.AttributesEx4 |= SpellAttr4.AuraIsBuff;
            });

            foreach (var spellInfo in mSpellInfoMap.Values)
            {
                // Fix range for trajectory triggered spell
                foreach (var spellEffectInfo in spellInfo.GetEffects())
                {
                    if (spellEffectInfo.IsEffect() && 
                        (spellEffectInfo.TargetA.GetTarget() == Targets.DestTraj || spellEffectInfo.TargetB.GetTarget() == Targets.DestTraj))
                    {
                        // Get triggered spell if any
                        foreach (SpellInfo spellInfoTrigger in _GetSpellInfo(spellEffectInfo.TriggerSpell))
                        {
                            float maxRangeMain = spellInfo.GetMaxRange();
                            float maxRangeTrigger = spellInfoTrigger.GetMaxRange();

                            // check if triggered spell has enough max range to cover trajectory
                            if (maxRangeTrigger < maxRangeMain)
                                spellInfoTrigger.RangeEntry = spellInfo.RangeEntry;
                        }
                    }

                    switch (spellEffectInfo.Effect)
                    {
                        case SpellEffectName.Charge:
                        case SpellEffectName.ChargeDest:
                        case SpellEffectName.Jump:
                        case SpellEffectName.JumpDest:
                        case SpellEffectName.LeapBack:
                            if (spellInfo.Speed == 0 && spellInfo.SpellFamilyName == 0 && !spellInfo.HasAttribute(SpellAttr9.MissileSpeedIsDelayInSeconds))
                                spellInfo.Speed = MotionMaster.SPEED_CHARGE;
                            break;
                    }

                    if (spellEffectInfo.TargetA.GetSelectionCategory() == SpellTargetSelectionCategories.Cone
                        || spellEffectInfo.TargetB.GetSelectionCategory() == SpellTargetSelectionCategories.Cone)
                    {
                        if (MathFunctions.fuzzyEq(spellInfo.ConeAngle, 0.0f))
                            spellInfo.ConeAngle = 90.0f;
                    }

                    // Area auras may not target area (they're self cast)
                    if (spellEffectInfo.IsAreaAuraEffect() && spellEffectInfo.IsTargetingArea())
                    {
                        spellEffectInfo.TargetA = new(Targets.UnitCaster);
                        spellEffectInfo.TargetB = new();
                    }
                }

                // disable proc for magnet auras, they're handled differently
                if (spellInfo.HasAura(AuraType.SpellMagnet))
                    spellInfo.ProcFlags = new ProcFlagsInit();

                // due to the way spell system works, unit would change orientation in Spell::_cast
                if (spellInfo.HasAura(AuraType.ControlVehicle))
                    spellInfo.AttributesEx5 |= SpellAttr5.AiDoesntFaceTarget;

                if (spellInfo.ActiveIconFileDataId == 135754)  // flight
                    spellInfo.Attributes |= SpellAttr0.Passive;

                if (spellInfo.IsSingleTarget() && spellInfo.MaxAffectedTargets == 0)
                    spellInfo.MaxAffectedTargets = 1;
            }

            SummonPropertiesRecord properties = CliDB.SummonPropertiesStorage.LookupByKey(121);
            if (properties != null)
                properties.Title = SummonTitle.Totem;
            properties = CliDB.SummonPropertiesStorage.LookupByKey(647); // 52893
            if (properties != null)
                properties.Title = SummonTitle.Totem;
            properties = CliDB.SummonPropertiesStorage.LookupByKey(628);
            if (properties != null) // Hungry Plaguehound
                properties.Control = SummonCategory.Pet;

            Log.outInfo(LogFilter.ServerLoading, $"Loaded SpellInfo corrections in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellInfoSpellSpecificAndAuraState()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            foreach (SpellInfo spellInfo in mSpellInfoMap.Values)
            {
                // AuraState depends on SpellSpecific
                spellInfo._LoadSpellSpecific();
                spellInfo._LoadAuraState();
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded SpellInfo SpellSpecific and AuraState in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellInfoDiminishing()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            foreach (SpellInfo spellInfo in mSpellInfoMap.Values)
            {
                if (spellInfo == null)
                    continue;

                spellInfo._LoadSpellDiminishInfo();
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded SpellInfo diminishing infos in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadSpellInfoImmunities()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            foreach (SpellInfo spellInfo in mSpellInfoMap.Values)
            {
                if (spellInfo == null)
                    continue;

                spellInfo._LoadImmunityInfo();
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded SpellInfo immunity infos in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadPetFamilySpellsStore()
        {
            Dictionary<int, SpellLevelsRecord> levelsBySpell = new();
            foreach (SpellLevelsRecord levels in CliDB.SpellLevelsStorage.Values)
            {
                if (levels.DifficultyID == 0)
                    levelsBySpell[levels.SpellID] = levels;
            }

            foreach (var skillLine in CliDB.SkillLineAbilityStorage.Values)
            {
                SpellInfo spellInfo = GetSpellInfo(skillLine.Spell, Difficulty.None);
                if (spellInfo == null)
                    continue;

                var levels = levelsBySpell.LookupByKey(skillLine.Spell);
                if (levels != null && levels.SpellLevel != 0)
                    continue;

                if (spellInfo.IsPassive())
                {
                    foreach (CreatureFamilyRecord cFamily in CliDB.CreatureFamilyStorage.Values)
                    {
                        if (skillLine.SkillLine != cFamily.SkillLine(0) && skillLine.SkillLine != cFamily.SkillLine(1))
                            continue;

                        if (skillLine.AcquireMethod != AbilityLearnType.OnSkillLearn)
                            continue;

                        Global.SpellMgr.PetFamilySpellsStorage.Add(cFamily.Id, spellInfo.Id);
                    }
                }
            }
        }

        public void LoadSpellTotemModel()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            SQLResult result = DB.World.Query("SELECT SpellID, RaceID, DisplayID from spell_totem_model");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 spell totem model records. DB table `spell_totem_model` is empty.");
                return;
            }

            uint count = 0;
            do
            {
                int spellId = result.Read<int>(0);
                Race race = (Race)result.Read<byte>(1);
                int displayId = result.Read<int>(2);

                SpellInfo spellEntry = GetSpellInfo(spellId, Difficulty.None);
                if (spellEntry == null)
                {
                    Log.outError(LogFilter.Sql, 
                        $"SpellID: {spellId} in `spell_totem_model` table " +
                        $"could not be found in dbc, skipped.");
                    continue;
                }

                if (!CliDB.ChrRacesStorage.ContainsKey((int)race))
                {
                    Log.outError(LogFilter.Sql, 
                        $"Race {race} defined in `spell_totem_model` does not exists, skipped.");
                    continue;
                }

                if (!CliDB.CreatureDisplayInfoStorage.ContainsKey(displayId))
                {
                    Log.outError(LogFilter.Sql, 
                        $"SpellID: {spellId} defined in `spell_totem_model` " +
                        $"has non-existing model ({displayId}).");
                    continue;
                }

                mSpellTotemModel[(spellId, race)] = displayId;
                ++count;

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {count} spell totem model records in {Time.Diff(oldMSTime)} ms.");

        }
        #endregion

        bool IsTriggerAura(AuraType type)
        {
            switch (type)
            {
                case AuraType.Dummy:
                case AuraType.PeriodicDummy:
                case AuraType.ModConfuse:
                case AuraType.ModThreat:
                case AuraType.ModStun:
                case AuraType.ModDamageDone:
                case AuraType.ModDamageTaken:
                case AuraType.ModResistance:
                case AuraType.ModStealth:
                case AuraType.ModFear:
                case AuraType.ModRoot:
                case AuraType.Transform:
                case AuraType.ReflectSpells:
                case AuraType.DamageImmunity:
                case AuraType.ProcTriggerSpell:
                case AuraType.ProcTriggerDamage:
                case AuraType.ModCastingSpeedNotStack:
                case AuraType.SchoolAbsorb:
                case AuraType.ModPowerCostSchoolPct:
                case AuraType.ModPowerCostSchool:
                case AuraType.ReflectSpellsSchool:
                case AuraType.MechanicImmunity:
                case AuraType.ModDamagePercentTaken:
                case AuraType.SpellMagnet:
                case AuraType.ModAttackPower:
                case AuraType.ModPowerRegenPercent:
                case AuraType.InterceptMeleeRangedAttacks:
                case AuraType.OverrideClassScripts:
                case AuraType.ModMechanicResistance:
                case AuraType.MeleeAttackPowerAttackerBonus:
                case AuraType.ModMeleeHaste:
                case AuraType.ModMeleeHaste3:
                case AuraType.ModAttackerMeleeHitChance:
                case AuraType.ProcTriggerSpellWithValue:
                case AuraType.ModSchoolMaskDamageFromCaster:
                case AuraType.ModSpellDamageFromCaster:
                case AuraType.AbilityIgnoreAurastate:
                case AuraType.ModInvisibility:
                case AuraType.ForceReaction:
                case AuraType.ModTaunt:
                case AuraType.ModDetaunt:
                case AuraType.ModDamagePercentDone:
                case AuraType.ModAttackPowerPct:
                case AuraType.ModHitChance:
                case AuraType.ModWeaponCritPct:
                case AuraType.ModBlockPercent:
                case AuraType.ModRoot2:
                case AuraType.IgnoreSpellCooldown:
                    return true;
            }
            return false;
        }

        bool IsAlwaysTriggeredAura(AuraType type)
        {
            switch (type)
            {
                case AuraType.OverrideClassScripts:
                case AuraType.ModStealth:
                case AuraType.ModConfuse:
                case AuraType.ModFear:
                case AuraType.ModRoot:
                case AuraType.ModStun:
                case AuraType.Transform:
                case AuraType.ModInvisibility:
                case AuraType.SpellMagnet:
                case AuraType.SchoolAbsorb:
                case AuraType.ModRoot2:
                    return true;
            }
            return false;
        }

        ProcFlagsSpellType GetSpellTypeMask(AuraType type)
        {
            switch (type)
            {
                case AuraType.ModStealth:
                    return ProcFlagsSpellType.Damage | ProcFlagsSpellType.NoDmgHeal;
                case AuraType.ModConfuse:
                case AuraType.ModFear:
                case AuraType.ModRoot:
                case AuraType.ModRoot2:
                case AuraType.ModStun:
                case AuraType.Transform:
                case AuraType.ModInvisibility:
                    return ProcFlagsSpellType.Damage;
                default:
                    return ProcFlagsSpellType.MaskAll;
            }
        }

        // SpellInfo object management
        public bool HasSpellInfo(int spellId, Difficulty difficulty)
        {
            var list = mSpellInfoMap[spellId];
            return list.Any(spellInfo => spellInfo.Difficulty == difficulty);
        }

        public MultiMap<int, SpellInfo> GetSpellInfoStorage()
        {
            return mSpellInfoMap;
        }

        //Extra Shit
        public SpellEffectHandler GetSpellEffectHandler(SpellEffectName eff)
        {
            if (!SpellEffectsHandlers.ContainsKey(eff))
            {
                Log.outError(LogFilter.Spells, $"No defined handler for SpellEffect {eff}");
                return SpellEffectsHandlers[SpellEffectName.None];
            }

            return SpellEffectsHandlers[eff];
        }

        public AuraEffectHandler GetAuraEffectHandler(AuraType type)
        {
            if (!AuraEffectHandlers.ContainsKey(type))
            {
                Log.outError(LogFilter.Spells, $"No defined handler for AuraEffect {type}");
                return AuraEffectHandlers[AuraType.None];
            }

            return AuraEffectHandlers[type];
        }

        public SkillRangeType GetSkillRangeType(SkillRaceClassInfoRecord rcEntry)
        {
            SkillLineRecord skill = CliDB.SkillLineStorage.LookupByKey(rcEntry.SkillID);
            if (skill == null)
                return SkillRangeType.None;

            if (Global.ObjectMgr.GetSkillTier(rcEntry.SkillTierID) != null)
                return SkillRangeType.Rank;

            if (rcEntry.SkillID == SkillType.Runeforging)
                return SkillRangeType.Mono;

            switch (skill.CategoryID)
            {
                case SkillCategory.Armor:
                    return SkillRangeType.Mono;
                case SkillCategory.Languages:
                    return SkillRangeType.Language;
            }
            return SkillRangeType.Level;
        }

        public bool IsPrimaryProfessionSkill(SkillType skill)
        {
            SkillLineRecord pSkill = CliDB.SkillLineStorage.LookupByKey(skill);
            return pSkill != null && pSkill.CategoryID == SkillCategory.Profession && pSkill.ParentSkillLineID == 0;
        }

        public bool IsWeaponSkill(SkillType skill)
        {
            var pSkill = CliDB.SkillLineStorage.LookupByKey(skill);
            return pSkill != null && pSkill.CategoryID == SkillCategory.Weapon;
        }

        public bool IsProfessionOrRidingSkill(SkillType skill)
        {
            return IsProfessionSkill(skill) || skill == SkillType.Riding;
        }

        public bool IsProfessionSkill(SkillType skill)
        {
            return IsPrimaryProfessionSkill(skill) || skill == SkillType.Fishing || skill == SkillType.Cooking;
        }

        public bool IsPartOfSkillLine(SkillType skillId, int spellId)
        {
            var skillBounds = GetSkillLineAbilityMapBounds(spellId);
            if (skillBounds != null)
            {
                foreach (var skill in skillBounds)
                    if (skill.SkillLine == skillId)
                        return true;
            }

            return false;
        }

        public int GetModelForTotem(int spellId, Race race)
        {
            return mSpellTotemModel.LookupByKey((spellId, race));
        }

        #region Fields
        Dictionary<int, SpellChainNode> mSpellChains = new();
        MultiMap<int, int> mSpellsReqSpell = new();
        MultiMap<int, int> mSpellReq = new();
        Dictionary<int, SpellLearnSkillNode> mSpellLearnSkills = new();
        MultiMap<int, SpellLearnSpellNode> mSpellLearnSpells = new();
        Dictionary<KeyValuePair<int, int>, SpellTargetPosition> mSpellTargetPositions = new();
        MultiMap<int, SpellGroup> mSpellSpellGroup = new();
        MultiMap<SpellGroup, int> mSpellGroupSpell = new();
        Dictionary<SpellGroup, SpellGroupStackRule> mSpellGroupStack = new();
        MultiMap<SpellGroup, AuraType> mSpellSameEffectStack = new();
        List<ServersideSpellName> mServersideSpellNames = new();
        Dictionary<(int id, Difficulty difficulty), SpellProcEntry> mSpellProcMap = new();
        Dictionary<int, SpellThreatEntry> mSpellThreatMap = new();
        Dictionary<int, PetAura> mSpellPetAuraMap = new();
        MultiMap<(SpellLinkedType, int), int> mSpellLinkedMap = new();
        Dictionary<int, SpellEnchantProcEntry> mSpellEnchantProcEventMap = new();
        MultiMap<int, SpellArea> mSpellAreaMap = new();
        MultiMap<int, SpellArea> mSpellAreaForQuestMap = new();
        MultiMap<int, SpellArea> mSpellAreaForQuestEndMap = new();
        MultiMap<int, SpellArea> mSpellAreaForAuraMap = new();
        MultiMap<int, SpellArea> mSpellAreaForAreaMap = new();
        MultiMap<int, SkillLineAbilityRecord> mSkillLineAbilityMap = new();
        Dictionary<CreatureFamily, MultiMap<int /*spell_level*/, int/*spell_id*/>> mPetLevelupSpellMap = new();
        Dictionary<int, PetDefaultSpellsEntry> mPetDefaultSpellsMap = new();           // only spells not listed in related mPetLevelupSpellMap entry
        MultiMap<int, SpellInfo> mSpellInfoMap = new();
        Dictionary<(int, Race), int> mSpellTotemModel = new();

        public delegate void AuraEffectHandler(AuraEffect effect, AuraApplication aurApp, AuraEffectHandleModes mode, bool apply);
        Dictionary<AuraType, AuraEffectHandler> AuraEffectHandlers = new();
        public delegate void SpellEffectHandler(Spell spell);
        Dictionary<SpellEffectName, SpellEffectHandler> SpellEffectsHandlers = new();

        public MultiMap<CreatureFamily, int> PetFamilySpellsStorage = new();
        #endregion
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class AuraEffectHandlerAttribute : Attribute
    {
        public AuraEffectHandlerAttribute(AuraType type)
        {
            AuraType = type;
        }

        public AuraType AuraType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class SpellEffectHandlerAttribute : Attribute
    {
        public SpellEffectHandlerAttribute(SpellEffectName effectName)
        {
            EffectName = effectName;
        }

        public SpellEffectName EffectName { get; set; }
    }

    public class SpellInfoLoadHelper
    {
        public SpellAuraOptionsRecord AuraOptions;
        public SpellAuraRestrictionsRecord AuraRestrictions;
        public SpellCastingRequirementsRecord CastingRequirements;
        public SpellCategoriesRecord Categories;
        public SpellClassOptionsRecord ClassOptions;
        public SpellCooldownsRecord Cooldowns;
        public SpellEffectRecord[] Effects = new SpellEffectRecord[SpellConst.MaxEffects];
        public SpellEquippedItemsRecord EquippedItems;
        public SpellInterruptsRecord Interrupts;
        public List<SpellLabelRecord> Labels = new();
        public SpellLevelsRecord Levels;
        public SpellMiscRecord Misc;
        public SpellPowerRecord[] Powers = new SpellPowerRecord[SpellConst.MaxPowersPerSpell];
        public SpellReagentsRecord Reagents;
        public List<SpellReagentsCurrencyRecord> ReagentsCurrency = new();
        public SpellScalingRecord Scaling;
        public SpellShapeshiftRecord Shapeshift;
        public SpellTargetRestrictionsRecord TargetRestrictions;
        public SpellTotemsRecord Totems;
        public List<SpellXSpellVisualRecord> Visuals = new(); // only to group visuals when parsing sSpellXSpellVisualStore, not for loading
    }

    public class SpellThreatEntry
    {
        public int flatMod;                                    // flat threat-value for this Spell  - default: 0
        public float pctMod;                                     // threat-multiplier for this Spell  - default: 1.0f
        public float apPctMod;                                   // Pct of AP that is added as Threat - default: 0.0f
    }

    public class SpellProcEntry
    {
        public SpellSchoolMask SchoolMask { get; set; }                                 // if nonzero - bitmask for matching proc condition based on spell's school
        public SpellFamilyNames SpellFamilyName { get; set; }                            // if nonzero - for matching proc condition based on candidate spell's SpellFamilyName
        public FlagArray128 SpellFamilyMask { get; set; } = new(4);    // if nonzero - bitmask for matching proc condition based on candidate spell's SpellFamilyFlags
        public ProcFlagsInit ProcFlags { get; set; }                                   // if nonzero - owerwrite procFlags field for given Spell.dbc entry, bitmask for matching proc condition, see enum ProcFlags
        public ProcFlagsSpellType SpellTypeMask { get; set; }                              // if nonzero - bitmask for matching proc condition based on candidate spell's damage/heal effects, see enum ProcFlagsSpellType
        public ProcFlagsSpellPhase SpellPhaseMask { get; set; }                             // if nonzero - bitmask for matching phase of a spellcast on which proc occurs, see enum ProcFlagsSpellPhase
        public ProcFlagsHit HitMask { get; set; }                                    // if nonzero - bitmask for matching proc condition based on hit result, see enum ProcFlagsHit
        public ProcAttributes AttributesMask { get; set; }                             // bitmask, see ProcAttributes
        public uint DisableEffectsMask { get; set; }                            // bitmask
        public float ProcsPerMinute { get; set; }                              // if nonzero - Chance to proc is equal to value * aura caster's weapon speed / 60
        public float Chance { get; set; }                                     // if nonzero - owerwrite procChance field for given Spell.dbc entry, defines Chance of proc to occur, not used if ProcsPerMinute set
        public Milliseconds Cooldown { get; set; }                           // if nonzero - cooldown in secs for aura proc, applied to aura
        public int Charges { get; set; }                                    // if nonzero - owerwrite procCharges field for given Spell.dbc entry, defines how many times proc can occur before aura remove, 0 - infinite
    }

    struct ServersideSpellName
    {
        public SpellNameRecord Name;

        public ServersideSpellName(int id, string name)
        {
            Name = new();
            Name.Name = new LocalizedString();

            Name.Id = id;
            for (Locale i = 0; i < Locale.Total; ++i)
                Name.Name[i] = name;
        }
    }


    public class PetDefaultSpellsEntry
    {
        public int[] spellid = new int[4];
    }

    public class SpellArea
    {
        public int spellId;
        public int areaId;                                         // zone/subzone/or 0 is not limited to zone
        public int questStart;                                     // quest start (quest must be active or rewarded for spell apply)
        public int questEnd;                                       // quest end (quest must not be rewarded for spell apply)
        public int auraSpell;                                       // spell aura must be applied for spell apply)if possitive) and it must not be applied in other case
        public RaceMask raceMask;                                   // can be applied only to races
        public Gender gender;                                       // can be applied only to gender
        public uint questStartStatus;                               // QuestStatus that quest_start must have in order to keep the spell
        public uint questEndStatus;                                 // QuestStatus that the quest_end must have in order to keep the spell (if the quest_end's status is different than this, the spell will be dropped)
        public SpellAreaFlag flags;                                 // if SPELL_AREA_FLAG_AUTOCAST then auto applied at area enter, in other case just allowed to cast || if SPELL_AREA_FLAG_AUTOREMOVE then auto removed inside area (will allways be removed on leaved even without flag)

        // helpers
        public bool IsFitToRequirements(Player player, int newZone, int newArea)
        {
            if (gender != Gender.None)                   // not in expected gender
            {
                if (player == null || gender != player.GetNativeGender())
                    return false;
            }

            if (raceMask != RaceMask.None)                    // not in expected race
            {
                if (player == null || !raceMask.HasRace(player.GetRace()))
                    return false;
            }

            if (areaId != 0)                                  // not in expected zone
            {
                if (newZone != areaId && newArea != areaId)
                    return false;
            }

            if (questStart != 0)                              // not in expected required quest state
            {
                if (player == null || (((1 << (int)player.GetQuestStatus(questStart)) & questStartStatus) == 0))
                    return false;
            }

            if (questEnd != 0)                                // not in expected forbidden quest state
            {
                if (player == null || (((1 << (int)player.GetQuestStatus(questEnd)) & questEndStatus) == 0))
                    return false;
            }

            if (auraSpell != 0)                               // not have expected aura
            {
                if (player == null || (auraSpell > 0 && !player.HasAura(auraSpell))
                    || (auraSpell < 0 && player.HasAura(-auraSpell)))
                {
                    return false;
                }
            }

            if (player != null)
            {
                Battleground bg = player.GetBattleground();
                if (bg != null)
                    return bg.IsSpellAllowed(spellId, player);
            }

            // Extra conditions -- leaving the possibility add extra conditions...
            switch (spellId)
            {
                case 91604: // No fly Zone - Wintergrasp
                {
                    if (player == null)
                        return false;

                    BattleField Bf = Global.BattleFieldMgr.GetBattlefieldToZoneId(player.GetMap(), player.GetZoneId());

                    if (Bf == null || Bf.CanFlyIn()
                        || (!player.HasAuraType(AuraType.ModIncreaseMountedFlightSpeed) && !player.HasAuraType(AuraType.Fly)))
                    {
                        return false;
                    }
                    break;
                }
                case 56618: // Horde Controls Factory Phase Shift
                case 56617: // Alliance Controls Factory Phase Shift
                {
                    if (player == null)
                        return false;

                    BattleField bf = Global.BattleFieldMgr.GetBattlefieldToZoneId(player.GetMap(), player.GetZoneId());

                    if (bf == null || bf.GetTypeId() != (int)BattleFieldTypes.WinterGrasp)
                        return false;

                    // team that controls the workshop in the specified area
                    int team = bf.GetData(newArea);

                    if (team == BattleGroundTeamId.Horde)
                        return spellId == 56618;
                    else if (team == BattleGroundTeamId.Alliance)
                        return spellId == 56617;
                    break;
                }
                case 57940: // Essence of Wintergrasp - Northrend
                case 58045: // Essence of Wintergrasp - Wintergrasp
                {
                    if (player == null)
                        return false;

                    BattleField battlefieldWG = Global.BattleFieldMgr.GetBattlefieldByBattleId(player.GetMap(), 1);
                    if (battlefieldWG != null)
                    {
                        return
                            battlefieldWG.IsEnabled() &&
                            (player.GetBatttleGroundTeamId() == battlefieldWG.GetDefenderTeam())
                            && !battlefieldWG.IsWarTime();
                    }

                    break;
                }
                case 74411: // Battleground- Dampening
                {
                    if (player == null)
                        return false;

                    BattleField bf = Global.BattleFieldMgr.GetBattlefieldToZoneId(player.GetMap(), player.GetZoneId());
                    if (bf != null)
                        return bf.IsWarTime();
                    break;
                }
            }
            return true;
        }
    }

    public class PetAura
    {
        public PetAura()
        {
            removeOnChangePet = false;
            damage = 0;
        }

        public PetAura(int petEntry, int aura, bool _removeOnChangePet, int _damage)
        {
            removeOnChangePet = _removeOnChangePet;
            damage = _damage;

            auras[petEntry] = aura;
        }

        public int GetAura(int petEntry)
        {
            var auraId = auras.LookupByKey(petEntry);
            if (auraId != 0)
                return auraId;

            auraId = auras.LookupByKey(0);
            if (auraId != 0)
                return auraId;

            return 0;
        }

        public void AddAura(int petEntry, int aura)
        {
            auras[petEntry] = aura;
        }

        public bool IsRemovedOnChangePet()
        {
            return removeOnChangePet;
        }

        public int GetDamage()
        {
            return damage;
        }

        Dictionary<int, int> auras = new();
        bool removeOnChangePet;
        int damage;
    }

    public class SpellEnchantProcEntry
    {
        public float Chance;         // if nonzero - overwrite SpellItemEnchantment value
        public float ProcsPerMinute; // if nonzero - Chance to proc is equal to value * aura caster's weapon speed / 60
        public uint HitMask;        // if nonzero - bitmask for matching proc condition based on hit result, see enum ProcFlagsHit
        public EnchantProcAttributes AttributesMask; // bitmask, see EnchantProcAttributes
    }

    public class SpellTargetPosition
    {
        public int target_mapId;
        public float target_X;
        public float target_Y;
        public float target_Z;
        public float target_Orientation;
    }
}
