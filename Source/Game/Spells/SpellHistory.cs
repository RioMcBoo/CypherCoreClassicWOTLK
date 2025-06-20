﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Spells
{
    public class SpellHistory
    {
        public SpellHistory(Unit owner)
        {
            _owner = owner;
        }

        public void LoadFromDB<T>(SQLResult cooldownsResult, SQLResult chargesResult) where T : WorldObject
        {
            if (!cooldownsResult.IsEmpty())
            {
                do
                {
                    CooldownEntry cooldownEntry = new();
                    cooldownEntry.SpellId = cooldownsResult.Read<int>(0);
                    if (!Global.SpellMgr.HasSpellInfo(cooldownEntry.SpellId, Difficulty.None))
                        continue;

                    if (typeof(T) == typeof(Pet))
                    {
                        cooldownEntry.CooldownEnd = (ServerTime)(UnixTime64)cooldownsResult.Read<long>(1);
                        cooldownEntry.ItemId = 0;
                        cooldownEntry.CategoryId = (SpellCategories)cooldownsResult.Read<int>(2);
                        cooldownEntry.CategoryEnd = (ServerTime)(UnixTime64)cooldownsResult.Read<long>(3);
                    }
                    else
                    {
                        cooldownEntry.CooldownEnd = (ServerTime)(UnixTime64)cooldownsResult.Read<long>(2);
                        cooldownEntry.ItemId = cooldownsResult.Read<int>(1);
                        cooldownEntry.CategoryId = (SpellCategories)cooldownsResult.Read<int>(3);
                        cooldownEntry.CategoryEnd = (ServerTime)(UnixTime64)cooldownsResult.Read<long>(4);
                    }

                    _spellCooldowns[cooldownEntry.SpellId] = cooldownEntry;
                    if (cooldownEntry.CategoryId != 0)
                        _categoryCooldowns[cooldownEntry.CategoryId] = _spellCooldowns[cooldownEntry.SpellId];

                } while (cooldownsResult.NextRow());
            }

            if (!chargesResult.IsEmpty())
            {
                do
                {
                    SpellCategories categoryId = (SpellCategories)chargesResult.Read<int>(0);

                    if (!CliDB.SpellCategoryStorage.ContainsKey((int)categoryId))
                        continue;

                    ChargeEntry charges;
                    charges.RechargeStart = (ServerTime)(UnixTime64)chargesResult.Read<long>(1);
                    charges.RechargeEnd = (ServerTime)(UnixTime64)chargesResult.Read<long>(2);
                    _categoryCharges.Add(categoryId, charges);

                } while (chargesResult.NextRow());
            }
        }

        public void SaveToDB<T>(SQLTransaction trans) where T : WorldObject
        {
            PreparedStatement stmt;
            if (typeof(T) == typeof(Pet))
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PET_SPELL_COOLDOWNS);
                stmt.SetInt32(0, _owner.GetCharmInfo().GetPetNumber());
                trans.Append(stmt);

                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PET_SPELL_CHARGES);
                stmt.SetInt32(0, _owner.GetCharmInfo().GetPetNumber());
                trans.Append(stmt);

                byte index;
                foreach (var pair in _spellCooldowns)
                {
                    if (!pair.Value.OnHold)
                    {
                        index = 0;
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_PET_SPELL_COOLDOWN);
                        stmt.SetInt32(index++, _owner.GetCharmInfo().GetPetNumber());
                        stmt.SetInt32(index++, pair.Key);
                        stmt.SetInt64(index++, (UnixTime64)pair.Value.CooldownEnd);
                        stmt.SetInt32(index++, (int)pair.Value.CategoryId);
                        stmt.SetInt64(index++, (UnixTime64)pair.Value.CategoryEnd);
                        trans.Append(stmt);
                    }
                }

                foreach (var pair in _categoryCharges)
                {
                    index = 0;
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_PET_SPELL_CHARGES);
                    stmt.SetInt32(index++, _owner.GetCharmInfo().GetPetNumber());
                    stmt.SetInt32(index++, (int)pair.Key);
                    stmt.SetInt64(index++, (UnixTime64)pair.Value.RechargeStart);
                    stmt.SetInt64(index++, (UnixTime64)pair.Value.RechargeEnd);
                    trans.Append(stmt);
                }
            }
            else
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SPELL_COOLDOWNS);
                stmt.SetInt64(0, _owner.GetGUID().GetCounter());
                trans.Append(stmt);

                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SPELL_CHARGES);
                stmt.SetInt64(0, _owner.GetGUID().GetCounter());
                trans.Append(stmt);

                byte index;
                foreach (var pair in _spellCooldowns)
                {
                    if (!pair.Value.OnHold)
                    {
                        index = 0;
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_SPELL_COOLDOWN);
                        stmt.SetInt64(index++, _owner.GetGUID().GetCounter());
                        stmt.SetInt32(index++, pair.Key);
                        stmt.SetInt32(index++, pair.Value.ItemId);
                        stmt.SetInt64(index++, (UnixTime64)pair.Value.CooldownEnd);
                        stmt.SetInt32(index++, (int)pair.Value.CategoryId);
                        stmt.SetInt64(index++, (UnixTime64)pair.Value.CategoryEnd);
                        trans.Append(stmt);
                    }
                }

                foreach (var pair in _categoryCharges)
                {
                    index = 0;
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_SPELL_CHARGES);
                    stmt.SetInt64(index++, _owner.GetGUID().GetCounter());
                    stmt.SetInt32(index++, (int)pair.Key);
                    stmt.SetInt64(index++, (UnixTime64)pair.Value.RechargeStart);
                    stmt.SetInt64(index++, (UnixTime64)pair.Value.RechargeEnd);
                    trans.Append(stmt);
                }
            }
        }

        public void Update()
        {
            ServerTime now = LoopTime.ServerTime;
            foreach (var pair in _categoryCooldowns.ToList())
            {
                if (pair.Value.CategoryEnd < now)
                    _categoryCooldowns.Remove(pair.Key);
            }

            foreach (var pair in _spellCooldowns.ToList())
            {
                if (pair.Value.CooldownEnd < now)
                {
                    _categoryCooldowns.Remove(pair.Value.CategoryId);
                    _spellCooldowns.Remove(pair.Key);
                }
            }

            foreach (var pair in _categoryCharges.ToList())
            {
                if (pair.Value.RechargeEnd <= now)
                    _categoryCharges.Remove(pair);
            }
        }

        public void HandleCooldowns(SpellInfo spellInfo, Item item, Spell spell = null)
        {
            HandleCooldowns(spellInfo, item != null ? item.GetEntry() : 0, spell);
        }

        public void HandleCooldowns(SpellInfo spellInfo, int itemId, Spell spell = null)
        {
            if (spell != null && spell.IsIgnoringCooldowns())
                return;

            if (ConsumeCharge(spellInfo.ChargeCategoryId))
                return;

            Player player = _owner.ToPlayer();
            if (player != null)
            {
                // potions start cooldown until exiting combat
                ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(itemId);
                if (itemTemplate != null)
                {
                    if (itemTemplate.IsPotion() || spellInfo.IsCooldownStartedOnEvent())
                    {
                        player.SetLastPotionId(itemId);
                        return;
                    }
                }
            }

            if (spellInfo.IsCooldownStartedOnEvent() || spellInfo.IsPassive())
                return;

            StartCooldown(spellInfo, itemId, spell);
        }

        public bool IsReady(SpellInfo spellInfo, int itemId = 0)
        {
            if (spellInfo.PreventionType.HasAnyFlag(SpellPreventionType.Silence))
            {
                if (IsSchoolLocked(spellInfo.GetSchoolMask()))
                    return false;
            }

            if (HasCooldown(spellInfo, itemId))
                return false;

            if (!HasCharge(spellInfo.ChargeCategoryId))
                return false;

            return true;
        }

        public void WritePacket(SendSpellHistory sendSpellHistory)
        {
            ServerTime now = LoopTime.ServerTime;
            foreach (var p in _spellCooldowns)
            {
                SpellHistoryEntry historyEntry = new();
                historyEntry.SpellID = p.Key;
                historyEntry.ItemID = p.Value.ItemId;

                if (p.Value.OnHold)
                    historyEntry.OnHold = true;
                else
                {
                    TimeSpan cooldownDuration = p.Value.CooldownEnd - now;
                    if (cooldownDuration.TotalMilliseconds <= 0)
                        continue;

                    TimeSpan categoryDuration = p.Value.CategoryEnd - now;
                    if (categoryDuration.TotalMilliseconds > 0)
                    {
                        historyEntry.Category = p.Value.CategoryId;
                        historyEntry.CategoryRecoveryTime = (int)categoryDuration.TotalMilliseconds;
                    }

                    if (cooldownDuration > categoryDuration)
                        historyEntry.RecoveryTime = (int)cooldownDuration.TotalMilliseconds;
                }

                sendSpellHistory.Entries.Add(historyEntry);
            }
        }

        public void WritePacket(SendSpellCharges sendSpellCharges)
        {
            ServerTime now = LoopTime.ServerTime;
            foreach (var key in _categoryCharges.Keys)
            {
                var list = _categoryCharges[key];
                if (!list.Empty())
                {
                    TimeSpan cooldownDuration = list.FirstOrDefault().RechargeEnd - now;
                    if (cooldownDuration.TotalMilliseconds <= 0)
                        continue;

                    SpellChargeEntry chargeEntry = new();
                    chargeEntry.Category = key;
                    chargeEntry.NextRecoveryTime = (uint)cooldownDuration.TotalMilliseconds;
                    chargeEntry.ConsumedCharges = (byte)list.Count;
                    sendSpellCharges.Entries.Add(chargeEntry);
                }
            }
        }

        public void WritePacket(PetSpells petSpells)
        {
            ServerTime now = LoopTime.ServerTime;

            foreach (var pair in _spellCooldowns)
            {
                PetSpellCooldown petSpellCooldown = new();
                petSpellCooldown.SpellID = pair.Key;
                petSpellCooldown.Category = (ushort)pair.Value.CategoryId;

                if (!pair.Value.OnHold)
                {
                    var cooldownDuration = pair.Value.CooldownEnd - now;
                    if (cooldownDuration.TotalMilliseconds <= 0)
                        continue;

                    petSpellCooldown.Duration = (uint)cooldownDuration.TotalMilliseconds;
                    var categoryDuration = pair.Value.CategoryEnd - now;
                    if (categoryDuration.TotalMilliseconds > 0)
                        petSpellCooldown.CategoryDuration = (uint)categoryDuration.TotalMilliseconds;
                }
                else
                    petSpellCooldown.CategoryDuration = 0x80000000;

                petSpells.Cooldowns.Add(petSpellCooldown);
            }

            foreach (var key in _categoryCharges.Keys)
            {
                var list = _categoryCharges[key];
                if (!list.Empty())
                {
                    var cooldownDuration = list.FirstOrDefault().RechargeEnd - now;
                    if (cooldownDuration.TotalMilliseconds <= 0)
                        continue;

                    PetSpellHistory petChargeEntry = new();
                    petChargeEntry.CategoryID = key;
                    petChargeEntry.RecoveryTime = (uint)cooldownDuration.TotalMilliseconds;
                    petChargeEntry.ConsumedCharges = (sbyte)list.Count;

                    petSpells.SpellHistory.Add(petChargeEntry);
                }
            }
        }

        public void StartCooldown(SpellInfo spellInfo, int itemId, Spell spell = null, bool onHold = false, Milliseconds? forcedCooldown = null)
        {
            // init cooldown values
            SpellCategories categoryId = 0;
            Milliseconds cooldown = Milliseconds.Zero;
            Milliseconds categoryCooldown = Milliseconds.Zero;

            ServerTime curTime = LoopTime.ServerTime;
            ServerTime catrecTime;
            ServerTime recTime;
            bool needsCooldownPacket = false;

            if (!forcedCooldown.HasValue)
                GetCooldownDurations(spellInfo, itemId, ref cooldown, ref categoryId, ref categoryCooldown);
            else
                cooldown = forcedCooldown.Value;

            // overwrite time for selected category
            if (onHold)
            {
                // use +MONTH as infinite cooldown marker
                catrecTime = categoryCooldown > 0 ? (curTime + PlayerConst.InfinityCooldownDelay) : curTime;
                recTime = cooldown > 0 ? (curTime + PlayerConst.InfinityCooldownDelay) : catrecTime;
            }
            else
            {
                if (!forcedCooldown.HasValue)
                {
                    // Now we have cooldown data (if found any), time to apply mods
                    Player modOwner = _owner.GetSpellModOwner();
                    if (modOwner != null)
                    {

                        if (cooldown >= 0)
                            modOwner.ApplySpellMod(spellInfo, SpellModOp.Cooldown, ref cooldown, spell);

                        if (categoryCooldown >= 0 && !spellInfo.HasAttribute(SpellAttr6.NoCategoryCooldownMods))
                            modOwner.ApplySpellMod(spellInfo, SpellModOp.Cooldown, ref categoryCooldown, spell);
                    }

                    if (_owner.HasAuraTypeWithAffectMask(AuraType.ModSpellCooldownByHaste, spellInfo))
                    {
                        cooldown = (Milliseconds)(cooldown * _owner.m_unitData.ModSpellHaste);
                        categoryCooldown = (Milliseconds)(categoryCooldown * _owner.m_unitData.ModSpellHaste);
                    }

                    if (_owner.HasAuraTypeWithAffectMask(AuraType.ModCooldownByHasteRegen, spellInfo))
                    {
                        cooldown = (Milliseconds)(cooldown * _owner.m_unitData.ModHasteRegen);
                        categoryCooldown = (Milliseconds)(categoryCooldown * _owner.m_unitData.ModHasteRegen);
                    }

                    Milliseconds cooldownMod = (Milliseconds)_owner.GetTotalAuraModifier(AuraType.ModCooldown);
                    if (cooldownMod != 0)
                    {
                        // Apply SPELL_AURA_MOD_COOLDOWN only to own spells
                        Player playerOwner = GetPlayerOwner();
                        if (playerOwner == null || playerOwner.HasSpell(spellInfo.Id))
                        {
                            needsCooldownPacket = true;
                            cooldown += cooldownMod;   // SPELL_AURA_MOD_COOLDOWN does not affect category cooldows, verified with shaman shocks
                        }
                    }

                    // Apply SPELL_AURA_MOD_SPELL_CATEGORY_COOLDOWN modifiers
                    // Note: This aura applies its modifiers to all cooldowns of spells with set category, not to category cooldown only
                    if (categoryId != 0)
                    {
                        Milliseconds categoryModifier = (Milliseconds)_owner.GetTotalAuraModifierByMiscValue(AuraType.ModSpellCategoryCooldown, (int)categoryId);
                        if (categoryModifier != 0)
                        {
                            if (cooldown > 0)
                                cooldown += categoryModifier;

                            if (categoryCooldown > 0)
                                categoryCooldown += categoryModifier;
                        }

                        SpellCategoryRecord categoryEntry = CliDB.SpellCategoryStorage.LookupByKey((int)categoryId);
                        if (categoryEntry.HasFlag(SpellCategoryFlags.CooldownExpiresAtDailyReset))
                            categoryCooldown = (Milliseconds)(Global.WorldMgr.GetNextDailyQuestsResetTime() - LoopTime.RealmTime);
                    }
                }
                else
                    needsCooldownPacket = true;

                // replace negative cooldowns by 0
                if (cooldown < 0)
                    cooldown = Milliseconds.Zero;

                if (categoryCooldown < 0)
                    categoryCooldown = Milliseconds.Zero;

                // no cooldown after applying spell mods
                if (cooldown == 0 && categoryCooldown == 0)
                    return;

                catrecTime = categoryCooldown != 0 ? curTime + categoryCooldown : curTime;
                recTime = cooldown != 0 ? curTime + cooldown : catrecTime;
            }

            // self spell cooldown
            if (recTime != curTime)
            {
                AddCooldown(spellInfo.Id, itemId, recTime, categoryId, catrecTime, onHold);

                if (needsCooldownPacket)
                {
                    Player playerOwner = GetPlayerOwner();
                    if (playerOwner != null)
                    {
                        SpellCooldownPkt spellCooldown = new();
                        spellCooldown.Caster = _owner.GetGUID();
                        spellCooldown.Flags = SpellCooldownFlags.None;
                        spellCooldown.SpellCooldowns.Add(new SpellCooldownStruct(spellInfo.Id, cooldown));
                        playerOwner.SendPacket(spellCooldown);
                    }
                }
            }
        }

        public void SendCooldownEvent(SpellInfo spellInfo, int itemId = 0, Spell spell = null, bool startCooldown = true)
        {
            Player player = GetPlayerOwner();
            if (player != null)
            {
                var category = spellInfo.GetCategory();
                GetCooldownDurations(spellInfo, itemId, ref category);

                var categoryEntry = _categoryCooldowns.LookupByKey(category);
                if (categoryEntry != null && categoryEntry.SpellId != spellInfo.Id)
                {
                    player.SendPacket(new CooldownEvent(player != _owner, categoryEntry.SpellId));

                    if (startCooldown)
                        StartCooldown(Global.SpellMgr.GetSpellInfo(categoryEntry.SpellId, _owner.GetMap().GetDifficultyID()), itemId, spell);
                }

                player.SendPacket(new CooldownEvent(player != _owner, spellInfo.Id));
            }

            // start cooldowns at server side, if any
            if (startCooldown)
                StartCooldown(spellInfo, itemId, spell);
        }

        public void AddCooldown(int spellId, int itemId, TimeSpan cooldownDuration)
        {
            ServerTime now = LoopTime.ServerTime;
            AddCooldown(spellId, itemId, now + cooldownDuration, 0, now);
        }

        public void AddCooldown(int spellId, int itemId, ServerTime cooldownEnd, SpellCategories categoryId, ServerTime categoryEnd, bool onHold = false)
        {
            CooldownEntry cooldownEntry = new();
            // scripts can start multiple cooldowns for a given spell, only store the longest one
            if (cooldownEnd > cooldownEntry.CooldownEnd || categoryEnd > cooldownEntry.CategoryEnd || onHold)
            {
                cooldownEntry.SpellId = spellId;
                cooldownEntry.CooldownEnd = cooldownEnd;
                cooldownEntry.ItemId = itemId;
                cooldownEntry.CategoryId = categoryId;
                cooldownEntry.CategoryEnd = categoryEnd;
                cooldownEntry.OnHold = onHold;
                _spellCooldowns[spellId] = cooldownEntry;

                if (categoryId != 0)
                    _categoryCooldowns[categoryId] = cooldownEntry;
            }
        }

        public void ModifySpellCooldown(int spellId, TimeSpan cooldownMod, bool withoutCategoryCooldown)
        {
            var cooldownEntry = _spellCooldowns.LookupByKey(spellId);
            if (cooldownMod.TotalMilliseconds == 0 || cooldownEntry == null)
                return;

            ModifySpellCooldown(cooldownEntry, cooldownMod, withoutCategoryCooldown);
        }

        void ModifySpellCooldown(CooldownEntry cooldownEntry, TimeSpan cooldownMod, bool withoutCategoryCooldown)
        {
            ServerTime now = LoopTime.ServerTime;

            cooldownEntry.CooldownEnd += cooldownMod;

            if (cooldownEntry.CategoryId != 0)
            {
                if (!withoutCategoryCooldown)
                    cooldownEntry.CategoryEnd += cooldownMod;

                // Because category cooldown existence is tied to regular cooldown,
                // we cannot allow a situation where regular cooldown is shorter than category
                if (cooldownEntry.CooldownEnd < cooldownEntry.CategoryEnd)
                    cooldownEntry.CooldownEnd = cooldownEntry.CategoryEnd;
            }

            Player playerOwner = GetPlayerOwner();
            if (playerOwner != null)
            {
                ModifyCooldown modifyCooldown = new();
                modifyCooldown.IsPet = _owner != playerOwner;
                modifyCooldown.SpellID = cooldownEntry.SpellId;
                modifyCooldown.DeltaTime = (int)cooldownMod.TotalMilliseconds;
                modifyCooldown.WithoutCategoryCooldown = withoutCategoryCooldown;
                playerOwner.SendPacket(modifyCooldown);
            }

            if (cooldownEntry.CooldownEnd <= now)
            {
                _categoryCooldowns.Remove(cooldownEntry.CategoryId);
                _spellCooldowns.Remove(cooldownEntry.SpellId);
            }
        }

        public void ModifyCooldown(int spellId, TimeSpan cooldownMod, bool withoutCategoryCooldown = false)
        {
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, _owner.GetMap().GetDifficultyID());
            if (spellInfo != null)
                ModifyCooldown(spellInfo, cooldownMod, withoutCategoryCooldown);
        }

        public void ModifyCooldown(SpellInfo spellInfo, TimeSpan cooldownMod, bool withoutCategoryCooldown = false)
        {
            if (cooldownMod == TimeSpan.Zero)
                return;

            if (GetChargeRecoveryTime(spellInfo.ChargeCategoryId) > 0 && GetMaxCharges(spellInfo.ChargeCategoryId) > 0)
                ModifyChargeRecoveryTime(spellInfo.ChargeCategoryId, cooldownMod);
            else
                ModifySpellCooldown(spellInfo.Id, cooldownMod, withoutCategoryCooldown);
        }

        public void ModifyCoooldowns(Func<CooldownEntry, bool> predicate, TimeSpan cooldownMod, bool withoutCategoryCooldown = false)
        {
            foreach (var cooldownEntry in _spellCooldowns.Values.ToList())
            {
                if (predicate(cooldownEntry))
                    ModifySpellCooldown(cooldownEntry, cooldownMod, withoutCategoryCooldown);
            }
        }

        public void ResetCooldown(int spellId, bool update = false)
        {
            var entry = _spellCooldowns.LookupByKey(spellId);
            if (entry == null)
                return;

            if (update)
            {
                Player playerOwner = GetPlayerOwner();
                if (playerOwner != null)
                {
                    ClearCooldown clearCooldown = new();
                    clearCooldown.IsPet = _owner != playerOwner;
                    clearCooldown.SpellID = spellId;
                    clearCooldown.ClearOnHold = false;
                    playerOwner.SendPacket(clearCooldown);
                }
            }

            _categoryCooldowns.Remove(entry.CategoryId);
            _spellCooldowns.Remove(spellId);
        }

        public void ResetCooldowns(Func<KeyValuePair<int, CooldownEntry>, bool> predicate, bool update = false)
        {
            List<int> resetCooldowns = new();
            foreach (var pair in _spellCooldowns)
            {
                if (predicate(pair))
                {
                    resetCooldowns.Add(pair.Key);
                    ResetCooldown(pair.Key, false);
                }
            }

            if (update && !resetCooldowns.Empty())
                SendClearCooldowns(resetCooldowns);
        }

        public void ResetAllCooldowns()
        {
            Player playerOwner = GetPlayerOwner();
            if (playerOwner != null)
            {
                List<int> cooldowns = new();
                foreach (var id in _spellCooldowns.Keys)
                    cooldowns.Add(id);

                SendClearCooldowns(cooldowns);
            }

            _categoryCooldowns.Clear();
            _spellCooldowns.Clear();
        }

        public bool HasCooldown(int spellId, int itemId = 0)
        {
            return HasCooldown(Global.SpellMgr.GetSpellInfo(spellId, _owner.GetMap().GetDifficultyID()), itemId);
        }

        public bool HasCooldown(SpellInfo spellInfo, int itemId = 0)
        {
            if (_owner.HasAuraTypeWithAffectMask(AuraType.IgnoreSpellCooldown, spellInfo))
                return false;

            if (_spellCooldowns.ContainsKey(spellInfo.Id))
                return true;

            if (spellInfo.CooldownAuraSpellId != 0 && _owner.HasAura(spellInfo.CooldownAuraSpellId))
                return true;

            SpellCategories category = 0;
            GetCooldownDurations(spellInfo, itemId, ref category);

            if (category == 0)
                category = spellInfo.GetCategory();

            if (category == 0)
                return false;

            return _categoryCooldowns.ContainsKey(category);
        }

        public TimeSpan GetRemainingCooldown(SpellInfo spellInfo)
        {
            ServerTime end;
            var entry = _spellCooldowns.LookupByKey(spellInfo.Id);
            if (entry != null)
                end = entry.CooldownEnd;
            else
            {
                var cooldownEntry = _categoryCooldowns.LookupByKey(spellInfo.GetCategory());
                if (cooldownEntry == null)
                    return TimeSpan.Zero;

                end = cooldownEntry.CategoryEnd;
            }

            ServerTime now = LoopTime.ServerTime;
            if (end < now)
                return TimeSpan.Zero;

            var remaining = end - now;
            return remaining;
        }

        public TimeSpan GetRemainingCategoryCooldown(SpellCategories categoryId)
        {
            ServerTime end;
            var cooldownEntry = _categoryCooldowns.LookupByKey(categoryId);
            if (cooldownEntry == null)
                return TimeSpan.Zero;

            end = cooldownEntry.CategoryEnd;

            ServerTime now = LoopTime.ServerTime;
            if (end < now)
                return TimeSpan.Zero;

            TimeSpan remaining = end - now;
            return remaining;
        }

        public TimeSpan GetRemainingCategoryCooldown(SpellInfo spellInfo)
        {
            return GetRemainingCategoryCooldown(spellInfo.GetCategory());
        }

        public void LockSpellSchool(SpellSchoolMask schoolMask, TimeSpan lockoutTime)
        {
            ServerTime now = LoopTime.ServerTime;
            ServerTime lockoutEnd = now + lockoutTime;
            for (SpellSchools i = 0; i < SpellSchools.Max; ++i)
            {
                if (schoolMask.HasSchool(i))
                    _schoolLockouts[(int)i] = lockoutEnd;
            }

            List<int> knownSpells = new();
            Player plrOwner = _owner.ToPlayer();
            if (plrOwner != null)
            {
                foreach (var p in plrOwner.GetSpellMap())
                {
                    if (p.Value.State != PlayerSpellState.Removed)
                        knownSpells.Add(p.Key);
                }
            }
            else if (_owner.IsPet())
            {
                Pet petOwner = _owner.ToPet();
                foreach (var p in petOwner.m_spells)
                {
                    if (p.Value.UpdateState != PetSpellState.Removed)
                        knownSpells.Add(p.Key);
                }
            }
            else
            {
                Creature creatureOwner = _owner.ToCreature();
                for (byte i = 0; i < SharedConst.MaxCreatureSpells; ++i)
                {
                    if (creatureOwner.m_spells[i] != 0)
                        knownSpells.Add(creatureOwner.m_spells[i]);
                }
            }

            SpellCooldownPkt spellCooldown = new();
            spellCooldown.Caster = _owner.GetGUID();
            spellCooldown.Flags = SpellCooldownFlags.LossOfControlUi;
            foreach (var spellId in knownSpells)
            {
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, _owner.GetMap().GetDifficultyID());
                if (spellInfo.IsCooldownStartedOnEvent())
                    continue;

                if (!spellInfo.PreventionType.HasAnyFlag(SpellPreventionType.Silence))
                    continue;

                if ((schoolMask & spellInfo.GetSchoolMask()) == 0)
                    continue;

                if (GetRemainingCooldown(spellInfo) < lockoutTime)
                    AddCooldown(spellId, 0, lockoutEnd, 0, now);

                // always send cooldown, even if it will be shorter than already existing cooldown for LossOfControl UI
                spellCooldown.SpellCooldowns.Add(new SpellCooldownStruct(spellId, (Milliseconds)lockoutTime));
            }

            Player player = GetPlayerOwner();
            if (player != null)
            {
                if (!spellCooldown.SpellCooldowns.Empty())
                    player.SendPacket(spellCooldown);
            }
        }

        public bool IsSchoolLocked(SpellSchoolMask schoolMask)
        {
            ServerTime now = LoopTime.ServerTime;
            for (var i = SpellSchools.Normal; i < SpellSchools.Max; ++i)
            {
                if (schoolMask.HasSchool(i))
                {
                    if (_schoolLockouts[(int)i] > now)
                        return true;
                }
            }

            return false;
        }

        public bool ConsumeCharge(SpellCategories chargeCategoryId)
        {
            if (!CliDB.SpellCategoryStorage.ContainsKey((int)chargeCategoryId))
                return false;

            Milliseconds chargeRecovery = GetChargeRecoveryTime(chargeCategoryId);
            if (chargeRecovery > 0 && GetMaxCharges(chargeCategoryId) > 0)
            {
                ServerTime recoveryStart;
                var charges = _categoryCharges[chargeCategoryId];
                if (charges.Empty())
                    recoveryStart = LoopTime.ServerTime;
                else
                    recoveryStart = charges.Last().RechargeEnd;

                _categoryCharges.Add(chargeCategoryId, new ChargeEntry(recoveryStart, chargeRecovery));
                return true;
            }

            return false;
        }

        void ModifyChargeRecoveryTime(SpellCategories chargeCategoryId, TimeSpan cooldownMod)
        {
            var chargeCategoryEntry = CliDB.SpellCategoryStorage.LookupByKey((int)chargeCategoryId);
            if (chargeCategoryEntry == null)
                return;

            var chargeList = _categoryCharges[chargeCategoryId];
            if (chargeList.Empty())
                return;

            ServerTime now = LoopTime.ServerTime;

            List<ChargeEntry> newList = new();
            for (var i = 0; i < chargeList.Count; ++i)
            {
                var entry = chargeList[i];
                entry.RechargeStart += cooldownMod;
                entry.RechargeEnd += cooldownMod;
                newList.Add(entry);
            }

            _categoryCharges.SetValues(chargeCategoryId, newList);

            List<ChargeEntry> cooldownRemainsList = new();
            foreach (var charge in chargeList)
            {
                if (charge.RechargeEnd >= now)
                    cooldownRemainsList.Add(charge);
            }
            _categoryCharges.SetValues(chargeCategoryId, cooldownRemainsList);

            SendSetSpellCharges(chargeCategoryId, chargeList);
        }

        public void RestoreCharge(SpellCategories chargeCategoryId)
        {
            var chargeList = _categoryCharges[chargeCategoryId].ToList();
            if (!chargeList.Empty())
            {
                chargeList.RemoveAt(chargeList.Count - 1);

                SendSetSpellCharges(chargeCategoryId, chargeList);

                _categoryCharges.SetValues(chargeCategoryId, chargeList);
            }
        }

        public void ResetCharges(SpellCategories chargeCategoryId)
        {
            var chargeList = _categoryCharges[chargeCategoryId];
            if (!chargeList.Empty())
            {
                _categoryCharges.Remove(chargeCategoryId);

                Player player = GetPlayerOwner();
                if (player != null)
                {
                    ClearSpellCharges clearSpellCharges = new();
                    clearSpellCharges.IsPet = _owner != player;
                    clearSpellCharges.Category = chargeCategoryId;
                    player.SendPacket(clearSpellCharges);
                }
            }
        }

        public void ResetAllCharges()
        {
            _categoryCharges.Clear();

            Player player = GetPlayerOwner();
            if (player != null)
            {
                ClearAllSpellCharges clearAllSpellCharges = new();
                clearAllSpellCharges.IsPet = _owner != player;
                player.SendPacket(clearAllSpellCharges);
            }
        }

        public bool HasCharge(SpellCategories chargeCategoryId)
        {
            if (!CliDB.SpellCategoryStorage.ContainsKey((int)chargeCategoryId))
                return true;

            // Check if the spell is currently using charges (untalented warlock Dark Soul)
            int maxCharges = GetMaxCharges(chargeCategoryId);
            if (maxCharges <= 0)
                return true;

            var chargeList = _categoryCharges[chargeCategoryId];
            return chargeList.Empty() || chargeList.Count < maxCharges;
        }

        public int GetMaxCharges(SpellCategories chargeCategoryId)
        {
            SpellCategoryRecord chargeCategoryEntry = CliDB.SpellCategoryStorage.LookupByKey((int)chargeCategoryId);
            if (chargeCategoryEntry == null)
                return 0;

            int charges = chargeCategoryEntry.MaxCharges;
            charges += _owner.GetTotalAuraModifierByMiscValue(AuraType.ModMaxCharges, (int)chargeCategoryId);
            return charges;
        }

        public Milliseconds GetChargeRecoveryTime(SpellCategories chargeCategoryId)
        {
            SpellCategoryRecord chargeCategoryEntry = CliDB.SpellCategoryStorage.LookupByKey((int)chargeCategoryId);
            if (chargeCategoryEntry == null)
                return Milliseconds.Zero;

            Milliseconds recoveryTime = chargeCategoryEntry.ChargeRecoveryTime;
            recoveryTime += (Milliseconds)_owner.GetTotalAuraModifierByMiscValue(AuraType.ChargeRecoveryMod, (int)chargeCategoryId);

            float recoveryTimeF = recoveryTime;
            recoveryTimeF *= _owner.GetTotalAuraMultiplierByMiscValue(AuraType.ChargeRecoveryMultiplier, (int)chargeCategoryId);

            if (_owner.HasAuraType(AuraType.ChargeRecoveryAffectedByHaste))
                recoveryTimeF *= _owner.m_unitData.ModSpellHaste;

            if (_owner.HasAuraTypeWithMiscvalue(AuraType.ChargeRecoveryAffectedByHasteRegen, (int)chargeCategoryId))
                recoveryTimeF *= _owner.m_unitData.ModHasteRegen;

            return (Milliseconds)Math.Floor(recoveryTimeF);
        }

        public bool HasGlobalCooldown(SpellInfo spellInfo)
        {
            return _globalCooldowns.ContainsKey(spellInfo.StartRecoveryCategory) && _globalCooldowns[spellInfo.StartRecoveryCategory] > LoopTime.ServerTime;
        }

        public void AddGlobalCooldown(SpellInfo spellInfo, Milliseconds durationMs)
        {
            _globalCooldowns[spellInfo.StartRecoveryCategory] = LoopTime.ServerTime + durationMs;
        }

        public void CancelGlobalCooldown(SpellInfo spellInfo)
        {
            _globalCooldowns[spellInfo.StartRecoveryCategory] = ServerTime.Zero;
        }

        public TimeSpan GetRemainingGlobalCooldown(SpellInfo spellInfo)
        {
            if (!_globalCooldowns.TryGetValue(spellInfo.StartRecoveryCategory, out ServerTime end))
                return TimeSpan.Zero;

            ServerTime now = LoopTime.ServerTime;
            if (end < now)
                return TimeSpan.Zero;

            return end - now;
        }

        public bool IsPaused() { return _pauseTime.HasValue; }

        public void PauseCooldowns()
        {
            _pauseTime = LoopTime.ServerTime.TimeOfDay;
        }

        public void ResumeCooldowns()
        {
            if (!_pauseTime.HasValue)
                return;

            TimeSpan pausedDuration = LoopTime.ServerTime.TimeOfDay - _pauseTime.Value;

            foreach (var itr in _spellCooldowns)
                itr.Value.CooldownEnd += pausedDuration;

            foreach (var itr in _categoryCharges.Keys)
            {
                for (var i = 0; i < _categoryCharges[itr].Count; ++i)
                {
                    var entry = _categoryCharges[itr][i];
                    entry.RechargeEnd += pausedDuration;
                }
            }

            _pauseTime = null;

            Update();
        }

        public Player GetPlayerOwner()
        {
            return _owner.GetCharmerOrOwnerPlayerOrPlayerItself();
        }

        public void SendClearCooldowns(List<int> cooldowns)
        {
            Player playerOwner = GetPlayerOwner();
            if (playerOwner != null)
            {
                ClearCooldowns clearCooldowns = new();
                clearCooldowns.IsPet = _owner != playerOwner;
                clearCooldowns.SpellID = cooldowns;
                playerOwner.SendPacket(clearCooldowns);
            }
        }

        void SendSetSpellCharges(SpellCategories chargeCategoryId, IReadOnlyList<ChargeEntry> chargeCollection)
        {
            if (GetPlayerOwner() is Player player)
            {
                SetSpellCharges setSpellCharges = new();
                setSpellCharges.Category = chargeCategoryId;
                if (!chargeCollection.Empty())
                    setSpellCharges.NextRecoveryTime = (uint)(chargeCollection[0].RechargeEnd - LoopTime.ServerTime).TotalMilliseconds;
                setSpellCharges.ConsumedCharges = (byte)chargeCollection.Count;
                setSpellCharges.IsPet = player != _owner;
                player.SendPacket(setSpellCharges);
            }
        }
        
        void GetCooldownDurations(SpellInfo spellInfo, int itemId, ref SpellCategories categoryId)
        {
            Milliseconds notUsed = Milliseconds.Zero;
            GetCooldownDurations(spellInfo, itemId, ref notUsed, ref categoryId, ref notUsed);
        }

        void GetCooldownDurations(SpellInfo spellInfo, int itemId, ref Milliseconds cooldown, ref SpellCategories categoryId, ref Milliseconds categoryCooldown)
        {
            Milliseconds tmpCooldown = Milliseconds.Zero;
            SpellCategories tmpCategoryId = SpellCategories.None;
            Milliseconds tmpCategoryCooldown = Milliseconds.Zero;

            // cooldown information stored in ItemEffect.db2, overriding normal cooldown and category
            if (itemId != 0)
            {
                ItemTemplate proto = Global.ObjectMgr.GetItemTemplate(itemId);
                if (proto != null)
                {
                    foreach (ItemEffectRecord itemEffect in proto.Effects)
                    {
                        if (itemEffect.SpellID == spellInfo.Id)
                        {
                            tmpCooldown = itemEffect.CoolDown;
                            tmpCategoryId = itemEffect.SpellCategoryID;
                            tmpCategoryCooldown = itemEffect.CategoryCoolDown;
                            break;
                        }
                    }
                }
            }

            // if no cooldown found above then base at DBC data
            if (tmpCooldown < 0 && tmpCategoryCooldown < 0)
            {
                tmpCooldown = spellInfo.RecoveryTime;
                tmpCategoryId = spellInfo.GetCategory();
                tmpCategoryCooldown = spellInfo.CategoryRecoveryTime;
            }

            cooldown = tmpCooldown;
            categoryId = tmpCategoryId;
            categoryCooldown = tmpCategoryCooldown;
        }

        public void SaveCooldownStateBeforeDuel()
        {
            _spellCooldownsBeforeDuel = _spellCooldowns;
        }

        public void RestoreCooldownStateAfterDuel()
        {
            Player player = _owner.ToPlayer();
            if (player != null)
            {
                // add all profession CDs created while in duel (if any)
                foreach (var c in _spellCooldowns)
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(c.Key, Difficulty.None);

                    if (spellInfo.RecoveryTime > (Minutes)10 || spellInfo.CategoryRecoveryTime > (Minutes)10)
                        _spellCooldownsBeforeDuel[c.Key] = _spellCooldowns[c.Key];
                }

                // check for spell with onHold active before and during the duel
                foreach (var pair in _spellCooldownsBeforeDuel)
                {
                    if (!pair.Value.OnHold && _spellCooldowns.ContainsKey(pair.Key) && !_spellCooldowns[pair.Key].OnHold)
                        _spellCooldowns[pair.Key] = _spellCooldownsBeforeDuel[pair.Key];
                }

                // update the client: restore old cooldowns
                SpellCooldownPkt spellCooldown = new();
                spellCooldown.Caster = _owner.GetGUID();
                spellCooldown.Flags = SpellCooldownFlags.IncludeEventCooldowns;

                foreach (var c in _spellCooldowns)
                {
                    ServerTime now = LoopTime.ServerTime;
                    Milliseconds cooldownDuration = c.Value.CooldownEnd > now ? (Milliseconds)(c.Value.CooldownEnd - now) : Milliseconds.Zero;

                    // cooldownDuration must be between 0 and 10 minutes in order to avoid any visual bugs
                    if (cooldownDuration <= 0 || cooldownDuration > (Minutes)10 || c.Value.OnHold)
                        continue;

                    spellCooldown.SpellCooldowns.Add(new SpellCooldownStruct(c.Key, cooldownDuration));
                }

                player.SendPacket(spellCooldown);
            }
        }

        Unit _owner;
        Dictionary<int, CooldownEntry> _spellCooldowns = new();
        Dictionary<int, CooldownEntry> _spellCooldownsBeforeDuel = new();
        Dictionary<SpellCategories, CooldownEntry> _categoryCooldowns = new();
        ServerTime[] _schoolLockouts = new ServerTime[(int)SpellSchools.Max];
        MultiMap<SpellCategories, ChargeEntry> _categoryCharges = new();
        Dictionary<int, ServerTime> _globalCooldowns = new();
        TimeSpan? _pauseTime;

        public class CooldownEntry
        {
            public int SpellId;
            public ServerTime CooldownEnd;
            public int ItemId;
            public SpellCategories CategoryId;
            public ServerTime CategoryEnd;
            public bool OnHold;
        }

        public struct ChargeEntry
        {
            public ChargeEntry(ServerTime startTime, TimeSpan rechargeTime)
            {
                RechargeStart = startTime;
                RechargeEnd = startTime + rechargeTime;
            }

            public ServerTime RechargeStart;
            public ServerTime RechargeEnd;
        }
    }
}
