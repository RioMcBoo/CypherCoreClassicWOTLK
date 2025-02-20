﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.DataStorage;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public partial class Player
    {
        void UpdateSkillsForLevel()
        {
            Race race = GetRace();
            ushort maxSkill = GetMaxSkillValueForLevel();
            SkillInfo skillInfoField = m_activePlayerData.Skill;

            foreach (var pair in mSkillStatus)
            {
                if (pair.Value.State == SkillState.Deleted || skillInfoField.SkillRank[pair.Value.Pos] == 0)
                    continue;

                SkillType pskill = pair.Key;
                SkillRaceClassInfoRecord rcEntry = Global.DB2Mgr.GetSkillRaceClassInfo(pskill, GetRace(), GetClass());
                if (rcEntry == null)
                    continue;

                if (Global.SpellMgr.GetSkillRangeType(rcEntry) == SkillRangeType.Level)
                {
                    if (rcEntry.HasFlag(SkillRaceClassInfoFlags.AlwaysMaxValue))
                        SetSkillRank(pair.Value.Pos, maxSkill);

                    SetSkillMaxRank(pair.Value.Pos, maxSkill);
                    if (pair.Value.State != SkillState.New)
                        pair.Value.State = SkillState.Changed;
                }

                // Update level dependent skillline spells
                LearnSkillRewardedSpells(rcEntry.SkillID, skillInfoField.SkillRank[pair.Value.Pos], race);
            }
        }

        public ushort GetSkillValue(int skill)
        {
            return GetSkillValue((SkillType)skill);
        }

        public ushort GetSkillValue(SkillType skill)
        {
            if (skill == 0)
                return 0;

            SkillInfo skillInfo = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skill);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted || skillInfo.SkillRank[skillStatusData.Pos] == 0)
                return 0;

            int result = skillInfo.SkillRank[skillStatusData.Pos];
            result += skillInfo.SkillTempBonus[skillStatusData.Pos];
            result += skillInfo.SkillPermBonus[skillStatusData.Pos];
            return (ushort)(result < 0 ? 0 : result);
        }

        public ushort GetMaxSkillValue(int skill)
        {
            return GetMaxSkillValue((SkillType)skill);
        }

        public ushort GetMaxSkillValue(SkillType skill)
        {
            if (skill == 0)
                return 0;

            SkillInfo skillInfo = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skill);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted || skillInfo.SkillRank[skillStatusData.Pos] == 0)
                return 0;

            int result = skillInfo.SkillMaxRank[skillStatusData.Pos];
            result += skillInfo.SkillTempBonus[skillStatusData.Pos];
            result += skillInfo.SkillPermBonus[skillStatusData.Pos];
            return (ushort)(result < 0 ? 0 : result);
        }

        public ushort GetPureSkillValue(int skill)
        {
            return GetPureSkillValue((SkillType)skill);
        }

        public ushort GetPureSkillValue(SkillType skill)
        {
            if (skill == 0)
                return 0;

            SkillInfo skillInfo = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skill);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted || skillInfo.SkillRank[skillStatusData.Pos] == 0)
                return 0;

            return skillInfo.SkillRank[skillStatusData.Pos];
        }

        public ushort GetSkillStep(int skill)
        {
            return GetSkillStep((SkillType)skill);
        }

        public ushort GetSkillStep(SkillType skill)
        {
            if (skill == 0)
                return 0;

            SkillInfo skillInfo = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skill);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted || skillInfo.SkillRank[skillStatusData.Pos] == 0)
                return 0;

            return skillInfo.SkillStep[skillStatusData.Pos];
        }

        public ushort GetPureMaxSkillValue(SkillType skill)
        {
            if (skill == 0)
                return 0;

            SkillInfo skillInfo = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skill);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted || skillInfo.SkillRank[skillStatusData.Pos] == 0)
                return 0;

            return skillInfo.SkillMaxRank[skillStatusData.Pos];
        }

        public ushort GetBaseSkillValue(SkillType skill)
        {
            if (skill == 0)
                return 0;

            SkillInfo skillInfo = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skill);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted || skillInfo.SkillRank[skillStatusData.Pos] == 0)
                return 0;

            int result = skillInfo.SkillRank[skillStatusData.Pos];
            result += skillInfo.SkillPermBonus[skillStatusData.Pos];
            return (ushort)(result < 0 ? 0 : result);
        }

        public ushort GetSkillPermBonusValue(SkillType skill)
        {
            if (skill == 0)
                return 0;

            SkillInfo skillInfo = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skill);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted || skillInfo.SkillRank[skillStatusData.Pos] == 0)
                return 0;

            return skillInfo.SkillPermBonus[skillStatusData.Pos];
        }

        public ushort GetSkillTempBonusValue(SkillType skill)
        {
            if (skill == 0)
                return 0;

            SkillInfo skillInfo = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skill);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted || skillInfo.SkillRank[skillStatusData.Pos] == 0)
                return 0;

            return (ushort)skillInfo.SkillTempBonus[skillStatusData.Pos];
        }

        void InitializeSelfResurrectionSpells()
        {
            ClearSelfResSpell();

            int[] spells = new int[3];

            var dummyAuras = GetAuraEffectsByType(AuraType.Dummy);
            foreach (var auraEffect in dummyAuras)
            {
                // Soulstone Resurrection                           // prio: 3 (max, non death persistent)
                if (auraEffect.GetSpellInfo().SpellFamilyName == SpellFamilyNames.Warlock && auraEffect.GetSpellInfo().SpellFamilyFlags[1].HasAnyFlag(0x1000000u))
                    spells[0] = 3026;
                // Twisting Nether                                  // prio: 2 (max)
                else if (auraEffect.GetId() == 23701 && RandomHelper.randChance(10))
                    spells[1] = 23700;
            }

            // Reincarnation (passive spell)  // prio: 1
            if (HasSpell(20608) && !GetSpellHistory().HasCooldown(21169))
                spells[2] = 21169;

            foreach (var selfResSpell in spells)
            {
                if (selfResSpell != 0)
                    AddSelfResSpell(selfResSpell);
            }
        }

        public void PetSpellInitialize()
        {
            Pet pet = GetPet();

            if (pet == null)
                return;

            Log.outDebug(LogFilter.Pet, "Pet Spells Groups");

            CharmInfo charmInfo = pet.GetCharmInfo();

            PetSpells petSpellsPacket = new();
            petSpellsPacket.PetGUID = pet.GetGUID();
            petSpellsPacket.CreatureFamily = (ushort)pet.GetCreatureTemplate().Family;         // creature family (required for pet talents)
            petSpellsPacket.Specialization = pet.GetSpecialization();
            petSpellsPacket.TimeLimit = (Milliseconds)pet.GetDuration();
            petSpellsPacket.ReactState = pet.GetReactState();
            petSpellsPacket.CommandState = charmInfo.GetCommandState();

            // action bar loop
            for (byte i = 0; i < SharedConst.ActionBarIndexMax; ++i)
                petSpellsPacket.ActionButtons[i] = charmInfo.GetActionBarEntry(i).packedData;

            if (pet.IsPermanentPetFor(this))
            {
                // spells loop
                foreach (var pair in pet.m_spells)
                {
                    if (pair.Value.state == PetSpellState.Removed)
                        continue;

                    petSpellsPacket.Actions.Add(UnitActionBarEntry.MAKE_UNIT_ACTION_BUTTON(pair.Key, (byte)pair.Value.active));
                }
            }

            // Cooldowns
            pet.GetSpellHistory().WritePacket(petSpellsPacket);

            SendPacket(petSpellsPacket);
        }

        public ObjectGuid GetSummonedBattlePetGUID() => m_activePlayerData.SummonedBattlePetGUID;
        public void SetSummonedBattlePetGUID(ObjectGuid guid)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SummonedBattlePetGUID), guid);
        }

        public bool CanSeeSpellClickOn(Creature creature)
        {
            if (!creature.HasNpcFlag(NPCFlags1.SpellClick))
                return false;

            var clickBounds = Global.ObjectMgr.GetSpellClickInfoMapBounds(creature.GetEntry());
            if (clickBounds.Empty())
                return true;

            foreach (var spellClickInfo in clickBounds)
            {
                if (!spellClickInfo.IsFitToRequirements(this, creature))
                    return false;

                if (Global.ConditionMgr.IsObjectMeetingSpellClickConditions(creature.GetEntry(), spellClickInfo.spellId, this, creature))
                    return true;
            }

            return false;
        }

        public override SpellInfo GetCastSpellInfo(SpellInfo spellInfo, TriggerCastFlags triggerFlag)
        {
            var overrides = m_overrideSpells[spellInfo.Id];
            foreach (var spellId in overrides)
            {
                SpellInfo newInfo = Global.SpellMgr.GetSpellInfo(spellId, GetMap().GetDifficultyID());
                if (newInfo != null)
                    return GetCastSpellInfo(newInfo, triggerFlag);
            }

            return base.GetCastSpellInfo(spellInfo, triggerFlag);
        }

        public void SetOverrideSpellsId(int overrideSpellsId) { SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.OverrideSpellsID), overrideSpellsId); }

        public void AddOverrideSpell(int overridenSpellId, int newSpellId)
        {
            m_overrideSpells.Add(overridenSpellId, newSpellId);
        }

        public void RemoveOverrideSpell(int overridenSpellId, int newSpellId)
        {
            m_overrideSpells.Remove(overridenSpellId, newSpellId);
        }

        public void SendSpellCategoryCooldowns()
        {
            SpellCategoryCooldown cooldowns = new();

            var categoryCooldownAuras = GetAuraEffectsByType(AuraType.ModSpellCategoryCooldown);
            foreach (AuraEffect aurEff in categoryCooldownAuras)
            {
                uint categoryId = (uint)aurEff.GetMiscValue();
                var cooldownInfo = cooldowns.CategoryCooldowns.Find(p => p.Category == categoryId);

                if (cooldownInfo == null)
                    cooldowns.CategoryCooldowns.Add(new SpellCategoryCooldown.CategoryCooldownInfo(categoryId, -aurEff.GetAmount()));
                else
                    cooldownInfo.ModCooldown -= aurEff.GetAmount();
            }

            SendPacket(cooldowns);
        }

        void InitializeSkillFields()
        {
            int i = 0;
            foreach (SkillLineRecord skillLine in CliDB.SkillLineStorage.Values)
            {
                SkillRaceClassInfoRecord rcEntry = Global.DB2Mgr.GetSkillRaceClassInfo(skillLine.Id, GetRace(), GetClass());
                if (rcEntry != null)
                {
                    SetSkillLineId(i, skillLine.Id);
                    SetSkillStartingRank(i, 1);
                    mSkillStatus.Add(skillLine.Id, new SkillStatusData(i, SkillState.Unchanged));
                    if (++i >= SkillConst.MaxPlayerSkills)
                        break;
                }
            }
        }

        public bool UpdateSkillPro(int skillId, int chance, int step)
        {
            return UpdateSkillPro((SkillType)skillId, chance, step);
        }

        public bool UpdateSkillPro(SkillType skillId, int chance, int step)
        {
            // levels sync. with spell requirement for skill levels to learn
            // bonus abilities in sSkillLineAbilityStore
            // Used only to avoid scan DBC at each skill grow
            uint[] bonusSkillLevels = [75, 150, 225, 300, 375, 450, 525, 600, 700, 850];

            Log.outDebug(LogFilter.Player, $"UpdateSkillPro(SkillId {skillId}, Chance {chance / 10.0f:F3}%)");
            if (skillId == 0)
                return false;

            if (chance <= 0)                                         // speedup in 0 Chance case
            {
                Log.outDebug(LogFilter.Player, $"Player:UpdateSkillPro Chance={chance / 10.0f:F3}% missed");
                return false;
            }

            var skillStatusData = mSkillStatus.LookupByKey(skillId);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted)
                return false;

            SkillInfo skillInfoField = m_activePlayerData.Skill;

            ushort value = skillInfoField.SkillRank[skillStatusData.Pos];
            ushort max = skillInfoField.SkillMaxRank[skillStatusData.Pos];

            if (max == 0 || value == 0 || value >= max)
                return false;

            if (RandomHelper.IRand(1, 1000) > chance)
            {
                Log.outDebug(LogFilter.Player, $"Player:UpdateSkillPro Chance={chance / 10.0f:F3}% missed");
                return false;
            }

            ushort new_value = (ushort)(value + step);
            if (new_value > max)
                new_value = max;

            SetSkillRank(skillStatusData.Pos, new_value);
            if (skillStatusData.State != SkillState.New)
                skillStatusData.State = SkillState.Changed;

            foreach (uint bsl in bonusSkillLevels)
            {
                if (value < bsl && new_value >= bsl)
                {
                    LearnSkillRewardedSpells(skillId, new_value, GetRace());
                    break;
                }
            }

            UpdateSkillEnchantments(skillId, value, new_value);
            UpdateCriteria(CriteriaType.SkillRaised, (long)skillId);
            Log.outDebug(LogFilter.Player, $"Player:UpdateSkillPro Chance={chance / 10.0f:F3}% taken");
            return true;
        }

        void UpdateSkillEnchantments(SkillType skill_id, int curr_value, int new_value)
        {
            for (byte i = 0; i < InventorySlots.BagEnd; ++i)
            {
                if (m_items[i] != null)
                {
                    for (EnchantmentSlot slot = 0; slot < EnchantmentSlot.Max; ++slot)
                    {
                        int ench_id = m_items[i].GetEnchantmentId(slot);
                        if (ench_id == 0)
                            continue;

                        SpellItemEnchantmentRecord Enchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(ench_id);
                        if (Enchant == null)
                            return;

                        if (Enchant.RequiredSkillID == skill_id)
                        {
                            // Checks if the enchantment needs to be applied or removed
                            if (curr_value < Enchant.RequiredSkillRank && new_value >= Enchant.RequiredSkillRank)
                                ApplyEnchantment(m_items[i], slot, true);
                            else if (new_value < Enchant.RequiredSkillRank && curr_value >= Enchant.RequiredSkillRank)
                                ApplyEnchantment(m_items[i], slot, false);
                        }

                        // If we're dealing with a gem inside a prismatic socket we need to check the prismatic socket requirements
                        // rather than the gem requirements itself. If the socket has no color it is a prismatic socket.
                        if ((slot == EnchantmentSlot.EnhancementSocket || slot == EnchantmentSlot.EnhancementSocket2 || slot == EnchantmentSlot.EnhancementSocket3)
                            && m_items[i].GetSocketType(slot - EnchantmentSlot.EnhancementSocket) == 0)
                        {
                            SpellItemEnchantmentRecord pPrismaticEnchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(m_items[i].GetEnchantmentId(EnchantmentSlot.EnhancementSocketPrismatic));

                            if (pPrismaticEnchant != null && pPrismaticEnchant.RequiredSkillID == skill_id)
                            {
                                if (curr_value < pPrismaticEnchant.RequiredSkillRank && new_value >= pPrismaticEnchant.RequiredSkillRank)
                                    ApplyEnchantment(m_items[i], slot, true);
                                else if (new_value < pPrismaticEnchant.RequiredSkillRank && curr_value >= pPrismaticEnchant.RequiredSkillRank)
                                    ApplyEnchantment(m_items[i], slot, false);
                            }
                        }
                    }
                }
            }
        }

        void UpdateEnchantTime(Milliseconds time)
        {
            for (var i = 0; i < m_enchantDuration.Count; ++i)
            {
                var enchantDuration = m_enchantDuration[i];
                if (enchantDuration.item.GetEnchantmentId(enchantDuration.slot) == 0)
                {
                    m_enchantDuration.Remove(enchantDuration);
                }
                else if (enchantDuration.leftduration <= time)
                {
                    ApplyEnchantment(enchantDuration.item, enchantDuration.slot, false, false);
                    enchantDuration.item.ClearEnchantment(enchantDuration.slot);
                    m_enchantDuration.Remove(enchantDuration);
                }
                else if (enchantDuration.leftduration > time)
                {
                    enchantDuration.leftduration -= time;
                }
            }
        }

        void ApplyEnchantment(Item item, bool apply)
        {
            for (EnchantmentSlot slot = 0; slot < EnchantmentSlot.Max; ++slot)
                ApplyEnchantment(item, slot, apply);
        }

        public void ApplyEnchantment(Item item, EnchantmentSlot enchantmentSlot, bool apply, bool apply_dur = true, bool ignore_condition = false)
        {
            if (item == null || !item.IsEquipped())
                return;

            if (enchantmentSlot >= EnchantmentSlot.Max)
                return;

            var enchant_id = item.GetEnchantmentId(enchantmentSlot);
            if (enchant_id == 0)
                return;

            var pEnchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
            if (pEnchant == null)
                return;

            if (!ignore_condition && pEnchant.ConditionID != 0 && !EnchantmentFitsRequirements(pEnchant.ConditionID, -1))
                return;

            if (pEnchant.MinLevel > GetLevel())
                return;

            if (pEnchant.RequiredSkillID > 0 && pEnchant.RequiredSkillRank > GetSkillValue(pEnchant.RequiredSkillID))
                return;

            // If we're dealing with a gem inside a prismatic socket we need to check the prismatic socket requirements
            // rather than the gem requirements itself. If the socket has no color it is a prismatic socket.
            if (enchantmentSlot == EnchantmentSlot.EnhancementSocket || enchantmentSlot == EnchantmentSlot.EnhancementSocket2 || enchantmentSlot == EnchantmentSlot.EnhancementSocket3)
            {
                if (item.GetSocketType(enchantmentSlot - EnchantmentSlot.EnhancementSocket) == SocketType.None)
                {
                    // Check if the requirements for the prismatic socket are met before applying the gem stats
                    var pPrismaticEnchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(item.GetEnchantmentId(EnchantmentSlot.EnhancementSocketPrismatic));
                    if (pPrismaticEnchant == null || (pPrismaticEnchant.RequiredSkillID > 0 && pPrismaticEnchant.RequiredSkillRank > GetSkillValue(pPrismaticEnchant.RequiredSkillID)))
                        return;
                }
            }

            if (!item.IsBroken())
            {
                for (int s = 0; s < ItemConst.MaxItemEnchantmentEffects; ++s)
                {
                    var enchant_display_type = pEnchant.Effect(s);
                    int enchant_amount = pEnchant.EffectPointsMin[s];
                    var enchant_spell_id = pEnchant.EffectArg[s];

                    switch (enchant_display_type)
                    {
                        case ItemEnchantmentType.None:
                            break;
                        case ItemEnchantmentType.CombatSpell:
                            // processed in Player.CastItemCombatSpell
                            break;
                        case ItemEnchantmentType.Damage:
                        {
                            var attackType = GetAttackBySlot(item.InventorySlot);
                            if (attackType.HasValue)
                                UpdateDamageDoneMods(attackType.Value, apply ? null : enchantmentSlot);
                        }
                        break;
                        case ItemEnchantmentType.EquipSpell:
                            if (enchant_spell_id != 0)
                            {
                                if (apply)
                                {
                                    int basepoints = 0;
                                    // Random Property Exist - try found basepoints for spell (basepoints depends from item suffix factor)
                                    if (item.GetItemRandomPropertyId() < 0)
                                    {
                                        var randomSuffixEntry = CliDB.ItemRandomSuffixStorage.LookupByKey(Math.Abs(item.GetItemRandomPropertyId()));
                                        if (randomSuffixEntry != null)
                                        {
                                            // Search enchant_amount
                                            for (var k = 0; k < ItemConst.MaxItemRandomProperties; ++k)
                                            {
                                                if (randomSuffixEntry.Enchantment[k] == enchant_id)
                                                {
                                                    basepoints = randomSuffixEntry.AllocationPct[k] * item.GetItemSuffixFactor() / 10000;
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    CastSpellExtraArgs args = new(item);
                                    // Cast custom spell vs all equal basepoints got from enchant_amount
                                    if (basepoints != 0)
                                    {
                                        for (var i = 0; i < ItemConst.MaxSpellEffects; ++i)
                                            args.AddSpellMod(SpellValueMod.BasePoint0 + i, basepoints);
                                    }

                                    CastSpell(this, enchant_spell_id, item);
                                }
                                else
                                    RemoveAurasDueToItemSpell(enchant_spell_id, item.GetGUID());
                            }
                            break;
                        case ItemEnchantmentType.Resistance:
                            if (enchant_amount == 0)
                            {
                                var randomSuffixEntry = CliDB.ItemRandomSuffixStorage.LookupByKey(Math.Abs(item.GetItemRandomPropertyId()));
                                if (randomSuffixEntry != null)
                                {
                                    for (int k = 0; k < ItemConst.MaxItemRandomProperties; ++k)
                                    {
                                        if (randomSuffixEntry.Enchantment[k] == enchant_id)
                                        {
                                            enchant_amount = randomSuffixEntry.AllocationPct[k] * item.GetItemSuffixFactor() / 10000;
                                            break;
                                        }
                                    }
                                }
                            }

                            enchant_amount = Math.Max(enchant_amount, 1);
                            StatMods.ModifyFlat(UnitMods.ResistanceStart + enchant_spell_id, enchant_amount, apply, UnitModType.TotalTemporary);
                            break;
                        case ItemEnchantmentType.Stat:
                        {
                            if (enchant_amount == 0)
                            {
                                var randomSuffixEntry = CliDB.ItemRandomSuffixStorage.LookupByKey(Math.Abs(item.GetItemRandomPropertyId()));
                                if (randomSuffixEntry != null)
                                {
                                    for (int k = 0; k < ItemConst.MaxItemRandomProperties; ++k)
                                    {
                                        if (randomSuffixEntry.Enchantment[k] == enchant_id)
                                        {
                                            enchant_amount = randomSuffixEntry.AllocationPct[k] * item.GetItemSuffixFactor() / 10000;
                                            break;
                                        }
                                    }
                                }
                            }

                            enchant_amount = Math.Max(enchant_amount, 1);

                            Log.outDebug(LogFilter.Player, $"Adding {enchant_amount} to stat nb {enchant_spell_id} ");
                            switch ((ItemModType)enchant_spell_id)
                            {
                                case ItemModType.Mana:
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} MANA");
                                    StatMods.ModifyFlat(UnitMods.Mana, enchant_amount, apply, UnitModType.BasePermanent);
                                    break;
                                case ItemModType.Health:
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} HEALTH");
                                    StatMods.ModifyFlat(UnitMods.Health, enchant_amount, apply, UnitModType.BasePermanent);
                                    break;
                                case ItemModType.Agility:
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} AGILITY");
                                    StatMods.ModifyFlat(UnitMods.StatAgility, enchant_amount, apply, enchantmentSlot);
                                    break;
                                case ItemModType.Strength:
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} STRENGTH");
                                    StatMods.ModifyFlat(UnitMods.StatStrength, enchant_amount, apply, enchantmentSlot);
                                    break;
                                case ItemModType.Intellect:
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} INTELLECT");
                                    StatMods.ModifyFlat(UnitMods.StatIntellect, enchant_amount, apply, enchantmentSlot);
                                    break;
                                case ItemModType.Spirit:
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} SPIRIT");
                                    StatMods.ModifyFlat(UnitMods.StatSpirit, enchant_amount, apply, enchantmentSlot);
                                    break;
                                case ItemModType.Stamina:
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} STAMINA");
                                    StatMods.ModifyFlat(UnitMods.StatStamina, enchant_amount, apply, enchantmentSlot);
                                    break;
                                case ItemModType.DefenseSkillRating:
                                    ApplyRatingMod(CombatRating.DefenseSkill, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} DEFENSE");
                                    break;
                                case ItemModType.DodgeRating:
                                    ApplyRatingMod(CombatRating.Dodge, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} DODGE");
                                    break;
                                case ItemModType.ParryRating:
                                    ApplyRatingMod(CombatRating.Parry, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} PARRY");
                                    break;
                                case ItemModType.BlockRating:
                                    ApplyRatingMod(CombatRating.Block, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} SHIELD_BLOCK");
                                    break;
                                case ItemModType.HitMeleeRating:
                                    ApplyRatingMod(CombatRating.HitMelee, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} MELEE_HIT");
                                    break;
                                case ItemModType.HitRangedRating:
                                    ApplyRatingMod(CombatRating.HitRanged, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} RANGED_HIT");
                                    break;
                                case ItemModType.HitSpellRating:
                                    ApplyRatingMod(CombatRating.HitSpell, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} SPELL_HIT");
                                    break;
                                case ItemModType.CritMeleeRating:
                                    ApplyRatingMod(CombatRating.CritMelee, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} MELEE_CRIT");
                                    break;
                                case ItemModType.CritRangedRating:
                                    ApplyRatingMod(CombatRating.CritRanged, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} RANGED_CRIT");
                                    break;
                                case ItemModType.CritSpellRating:
                                    ApplyRatingMod(CombatRating.CritSpell, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} SPELL_CRIT");
                                    break;
                                //case ItemModType.HitTakenMeleeRating:
                                //    ApplyRatingMod(CombatRating.HitTakenMelee, enchant_amount, apply);
                                //    break;
                                //case ItemModType.HitTakenRangedRating:
                                //    ApplyRatingMod(CombatRating.HitTakenRanged, enchant_amount, apply);
                                //    break;
                                //case ItemModType.HitTakenSpellRating:
                                //    ApplyRatingMod(CombatRating.HitTakenSpell, enchant_amount, apply);
                                //    break;
                                //case ItemModType.CritTakenMeleeRating:
                                //    ApplyRatingMod(CombatRating.CritTakenMelee, enchant_amount, apply);
                                //    break;
                                //case ItemModType.CritTakenRangedRating:
                                //    ApplyRatingMod(CombatRating.CritTakenRanged, enchant_amount, apply);
                                //    break;
                                //case ItemModType.CritTakenSpellRating:
                                //    ApplyRatingMod(CombatRating.CritTakenSpell, enchant_amount, apply);
                                //    break;
                                //case ItemModType.HasteMeleeRating:
                                //    ApplyRatingMod(CombatRating.HasteMelee, enchant_amount, apply);
                                //    break;
                                //case ItemModType.HasteRangedRating:
                                //    ApplyRatingMod(CombatRating.HasteRanged, enchant_amount, apply);
                                //    break;
                                case ItemModType.HasteSpellRating:
                                    ApplyRatingMod(CombatRating.HasteSpell, enchant_amount, apply);
                                    break;
                                case ItemModType.HitRating:
                                    ApplyRatingMod(CombatRating.HitMelee, enchant_amount, apply);
                                    ApplyRatingMod(CombatRating.HitRanged, enchant_amount, apply);
                                    ApplyRatingMod(CombatRating.HitSpell, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} HIT");
                                    break;
                                case ItemModType.CritRating:
                                    ApplyRatingMod(CombatRating.CritMelee, enchant_amount, apply);
                                    ApplyRatingMod(CombatRating.CritRanged, enchant_amount, apply);
                                    ApplyRatingMod(CombatRating.CritSpell, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} CRITICAL");
                                    break;
                                //case ItemModType.HitTakenRating: // Unused since 3.3.5
                                //    ApplyRatingMod(CombatRating.HitTakenMelee, enchant_amount, apply);
                                //    ApplyRatingMod(CombatRating.HitTakenRanged, enchant_amount, apply);
                                //    ApplyRatingMod(CombatRating.HitTakenSpell, enchant_amount, apply);
                                //    break;
                                //case ItemModType.CritTakenRating: // Unused since 3.3.5
                                //    ApplyRatingMod(CombatRating.CritTakenMelee, enchant_amount, apply);
                                //    ApplyRatingMod(CombatRating.CritTakenRanged, enchant_amount, apply);
                                //    ApplyRatingMod(CombatRating.CritTakenSpell, enchant_amount, apply);
                                //    break;
                                //case ItemModType.ResilienceRating:
                                //    ApplyRatingMod(CombatRating.ResiliencePlayerDamage, enchant_amount, apply);
                                //    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} RESILIENCE");
                                //    break;
                                case ItemModType.HasteRating:
                                    ApplyRatingMod(CombatRating.HasteMelee, enchant_amount, apply);
                                    ApplyRatingMod(CombatRating.HasteRanged, enchant_amount, apply);
                                    ApplyRatingMod(CombatRating.HasteSpell, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} HASTE");
                                    break;
                                case ItemModType.ExpertiseRating:
                                    ApplyRatingMod(CombatRating.Expertise, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} EXPERTISE");
                                    break;
                                case ItemModType.AttackPower:
                                    StatMods.ModifyFlat(UnitMods.AttackPowerMelee, enchant_amount, apply, enchantmentSlot);
                                    StatMods.ModifyFlat(UnitMods.AttackPowerRanged, enchant_amount, apply, enchantmentSlot);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} ATTACK_POWER");
                                    break;
                                case ItemModType.RangedAttackPower:
                                    StatMods.ModifyFlat(UnitMods.AttackPowerRanged, enchant_amount, apply, enchantmentSlot);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} RANGED_ATTACK_POWER");
                                    break;
                                case ItemModType.ManaRegeneration:
                                    ApplyManaRegenBonus(enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} MANA_REGENERATION");
                                    break;
                                case ItemModType.ArmorPenetrationRating:
                                    ApplyRatingMod(CombatRating.ArmorPenetration, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} ARMOR PENETRATION");
                                    break;
                                case ItemModType.SpellPower:
                                    ApplySpellPowerBonus(enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} SPELL_POWER");
                                    break;
                                case ItemModType.HealthRegen:
                                    ApplyHealthRegenBonus(enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} HEALTH_REGENERATION");
                                    break;
                                case ItemModType.SpellPenetration:
                                    ApplySpellPenetrationBonus(enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} SPELL_PENETRATION");
                                    break;
                                case ItemModType.BlockValue:
                                    HandleBaseModFlatValue(BaseModGroup.ShieldBlockValue, enchant_amount, apply);
                                    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} BLOCK_VALUE");
                                    break;
                                //case ItemModType.MasteryRating:
                                //    ApplyRatingMod(CombatRating.Mastery, (int)enchant_amount, apply);
                                //    Log.outDebug(LogFilter.Player, $"+ {enchant_amount} MASTERY");
                                //    break;                                
                                default:
                                    break;
                            }
                            break;
                        }
                        case ItemEnchantmentType.Totem:           // Shaman Rockbiter Weapon
                        {
                            var attackType = GetAttackBySlot(item.InventorySlot);
                            if (attackType.HasValue)
                                UpdateDamageDoneMods(attackType.Value, apply ? null : enchantmentSlot);
                            break;
                        }
                        case ItemEnchantmentType.UseSpell:
                            // processed in Player.CastItemUseSpell
                            break;
                        case ItemEnchantmentType.PrismaticSocket:
                        case ItemEnchantmentType.ArtifactPowerBonusRankByType:
                        case ItemEnchantmentType.ArtifactPowerBonusRankByID:
                        case ItemEnchantmentType.BonusListID:
                        case ItemEnchantmentType.BonusListCurve:
                        case ItemEnchantmentType.ArtifactPowerBonusRankPicker:
                            // nothing do..
                            break;
                        default:
                            Log.outError(LogFilter.Player,
                                $"Player.ApplyEnchantment: " +
                                $"Unknown item enchantment (ID: {enchant_id}, DisplayType: {enchant_display_type}) " +
                                $"for player '{GetName()}' ({GetGUID()}.)");
                            break;
                    }
                }
            }

            // visualize enchantment at player and equipped items
            if (enchantmentSlot == EnchantmentSlot.EnhancementPermanent && item.InventorySlot < m_playerData.VisibleItems.GetSize())
            {
                VisibleItem visibleItem = m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.VisibleItems, item.InventorySlot);
                SetUpdateFieldValue(visibleItem.ModifyValue(visibleItem.ItemVisual), item.GetVisibleItemVisual(this));
            }

            if (apply_dur)
            {
                if (apply)
                {
                    // set duration
                    Milliseconds duration = item.GetEnchantmentDuration(enchantmentSlot);
                    if (duration > 0)
                        AddEnchantmentDuration(item, enchantmentSlot, duration);
                }
                else
                {
                    // duration == 0 will remove EnchantDuration
                    AddEnchantmentDuration(item, enchantmentSlot, Milliseconds.Zero);
                }
            }
        }

        public void ModifySkillBonus(int skillid, int val, bool talent)
        {
            ModifySkillBonus((SkillType)skillid, val, talent);
        }

        public void ModifySkillBonus(SkillType skillid, int val, bool talent)
        {
            SkillInfo skillInfoField = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skillid);
            if (skillStatusData == null || skillStatusData.State == SkillState.Deleted || skillInfoField.SkillRank[skillStatusData.Pos] == 0)
                return;

            if (talent)
                SetSkillPermBonus(skillStatusData.Pos, (ushort)(skillInfoField.SkillPermBonus[skillStatusData.Pos] + val));
            else
                SetSkillTempBonus(skillStatusData.Pos, (ushort)(skillInfoField.SkillTempBonus[skillStatusData.Pos] + val));

            // Apply/Remove bonus to child skill lines
            var childSkillLines = Global.DB2Mgr.GetSkillLinesForParentSkill(skillid);
            if (childSkillLines != null)
                foreach (var childSkillLine in childSkillLines)
                    ModifySkillBonus(childSkillLine.Id, val, talent);
        }

        public void StopCastingBindSight()
        {
            Unit target = GetViewpoint()?.ToUnit();
            if (target != null)
            {
                target.RemoveAurasByType(AuraType.BindSight, GetGUID());
                target.RemoveAurasByType(AuraType.ModPossess, GetGUID());
                target.RemoveAurasByType(AuraType.ModPossessPet, GetGUID());
            }
        }

        void AddEnchantmentDurations(Item item)
        {
            for (EnchantmentSlot x = 0; x < EnchantmentSlot.Max; ++x)
            {
                if (item.GetEnchantmentId(x) == 0)
                    continue;

                Milliseconds duration = item.GetEnchantmentDuration(x);
                if (duration > 0)
                    AddEnchantmentDuration(item, x, duration);
            }
        }

        void AddEnchantmentDuration(Item item, EnchantmentSlot slot, Milliseconds duration)
        {
            if (item == null)
                return;

            if (slot >= EnchantmentSlot.Max)
                return;

            for (var i = 0; i < m_enchantDuration.Count; ++i)
            {
                var enchantDuration = m_enchantDuration[i];
                if (enchantDuration.item == item && enchantDuration.slot == slot)
                {
                    enchantDuration.item.SetEnchantmentDuration(enchantDuration.slot, enchantDuration.leftduration, this);
                    m_enchantDuration.Remove(enchantDuration);
                    break;
                }
            }
            if (duration > 0)
            {
                GetSession().SendItemEnchantTimeUpdate(GetGUID(), item.GetGUID(), slot, duration);
                m_enchantDuration.Add(new EnchantDuration(item, slot, duration));
            }
        }

        void RemoveEnchantmentDurations(Item item)
        {
            for (var i = 0; i < m_enchantDuration.Count; ++i)
            {
                var enchantDuration = m_enchantDuration[i];
                if (enchantDuration.item == item)
                {
                    // save duration in item
                    item.SetEnchantmentDuration(enchantDuration.slot, enchantDuration.leftduration, this);
                    m_enchantDuration.Remove(enchantDuration);
                }
            }
        }

        void RemoveEnchantmentDurationsReferences(Item item)
        {
            for (var i = 0; i < m_enchantDuration.Count; ++i)
            {
                var enchantDuration = m_enchantDuration[i];
                if (enchantDuration.item == item)
                    m_enchantDuration.Remove(enchantDuration);
            }
        }

        public void RemoveArenaEnchantments(EnchantmentSlot slot)
        {
            // remove enchantments from equipped items first to clean up the m_enchantDuration list
            for (var i = 0; i < m_enchantDuration.Count; ++i)
            {
                var enchantDuration = m_enchantDuration[i];
                if (enchantDuration.slot == slot)
                {
                    if (enchantDuration.item != null && enchantDuration.item.GetEnchantmentId(slot) != 0)
                    {
                        // Poisons and DK runes are enchants which are allowed on arenas
                        if (Global.SpellMgr.IsArenaAllowedEnchancment(enchantDuration.item.GetEnchantmentId(slot)))
                            continue;

                        // remove from stats
                        ApplyEnchantment(enchantDuration.item, slot, false, false);
                        // remove visual
                        enchantDuration.item.ClearEnchantment(slot);
                    }
                    // remove from update list
                    m_enchantDuration.Remove(enchantDuration);
                }
            }

            // remove enchants from inventory items
            // NOTE: no need to remove these from stats, since these aren't equipped
            // in inventory
            int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
            for (byte i = InventorySlots.ItemStart; i < inventoryEnd; ++i)
            {
                Item pItem = GetItemByPos(i);
                if (pItem != null && !Global.SpellMgr.IsArenaAllowedEnchancment(pItem.GetEnchantmentId(slot)))
                    pItem.ClearEnchantment(slot);
            }

            // in inventory bags
            for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; ++i)
            {
                Bag pBag = GetBagByPos(i);
                if (pBag != null)
                {
                    for (byte j = 0; j < pBag.GetBagSize(); j++)
                    {
                        Item pItem = pBag.GetItemByPos(j);
                        if (pItem != null && !Global.SpellMgr.IsArenaAllowedEnchancment(pItem.GetEnchantmentId(slot)))
                            pItem.ClearEnchantment(slot);
                    }
                }
            }
        }

        public void UpdatePotionCooldown(Spell spell = null)
        {
            // no potion used i combat or still in combat
            if (m_lastPotionId == 0 || IsInCombat())
                return;

            // Call not from spell cast, send cooldown event for item spells if no in combat
            if (spell == null)
            {
                // spell/item pair let set proper cooldown (except not existed charged spell cooldown spellmods for potions)
                ItemTemplate proto = Global.ObjectMgr.GetItemTemplate(m_lastPotionId);
                if (proto != null)
                {
                    for (byte idx = 0; idx < proto.Effects.Count; ++idx)
                    {
                        if (proto.Effects[idx].SpellID != 0 && proto.Effects[idx].TriggerType == ItemSpelltriggerType.OnUse)
                        {
                            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(proto.Effects[idx].SpellID, Difficulty.None);
                            if (spellInfo != null)
                                GetSpellHistory().SendCooldownEvent(spellInfo, m_lastPotionId);
                        }
                    }
                }
            }

            // from spell cases (m_lastPotionId set in Spell.SendSpellCooldown)
            else
            {
                if (spell.IsIgnoringCooldowns())
                    return;
                else
                    GetSpellHistory().SendCooldownEvent(spell.m_spellInfo, m_lastPotionId, spell);
            }

            m_lastPotionId = 0;
        }

        public bool CanUseMastery()
        {
            ChrSpecializationRecord chrSpec = GetPrimarySpecializationEntry();
            if (chrSpec != null)
                return HasSpell(chrSpec.MasterySpellID[0]) || HasSpell(chrSpec.MasterySpellID[1]);

            return false;
        }

        public bool HasSkill(int skill)
        {
            return HasSkill((SkillType)skill);
        }

        public bool HasSkill(SkillType skill)
        {
            if (skill == 0)
                return false;

            SkillInfo skillInfoField = m_activePlayerData.Skill;

            var skillStatusData = mSkillStatus.LookupByKey(skill);
            return skillStatusData != null && skillStatusData.State != SkillState.Deleted && skillInfoField.SkillRank[skillStatusData.Pos] != 0;
        }

        public void SetSkill(int skill, int step, int newVal, int maxVal)
        {
            SetSkill((SkillType)skill, step, newVal, maxVal);
        }

        public void SetSkill(SkillType skill, int step, int newVal, int maxVal)
        {
            SkillLineRecord skillEntry = CliDB.SkillLineStorage.LookupByKey((int)skill);
            if (skillEntry == null)
            {
                Log.outError(LogFilter.Misc, 
                    $"Player::SetSkill: Skill (SkillID: {skill}) " +
                    $"not found in SkillLineStore for player '{GetName()}' ({GetGUID()})");
                return;
            }

            int currVal;
            var skillStatusData = mSkillStatus.LookupByKey(skill);
            SkillInfo skillInfoField = m_activePlayerData.Skill;

            void refreshSkillBonusAuras()
            {
                // Temporary bonuses
                foreach (AuraEffect effect in GetAuraEffectsByType(AuraType.ModSkill))
                {
                    if ((SkillType)effect.GetMiscValue() == skill)
                        effect.HandleEffect(this, AuraEffectHandleModes.Skill, true);
                }

                foreach (AuraEffect effect in GetAuraEffectsByType(AuraType.ModSkill2))
                {
                    if ((SkillType)effect.GetMiscValue() == skill)
                        effect.HandleEffect(this, AuraEffectHandleModes.Skill, true);
                }

                // Permanent bonuses
                foreach (AuraEffect effect in GetAuraEffectsByType(AuraType.ModSkillTalent))
                {
                    if ((SkillType)effect.GetMiscValue() == skill)
                        effect.HandleEffect(this, AuraEffectHandleModes.Skill, true);
                }
            }

            // Handle already stored skills
            if (skillStatusData != null)
            {
                currVal = skillInfoField.SkillRank[skillStatusData.Pos];

                // Activate and update skill line
                if (newVal != 0)
                {
                    // enable parent skill line if missing
                    if (skillEntry.ParentSkillLineID != 0 && skillEntry.ParentTierIndex > 0 && GetSkillStep(skillEntry.ParentSkillLineID) < skillEntry.ParentTierIndex)
                    {
                        var rcEntry = Global.DB2Mgr.GetSkillRaceClassInfo(skillEntry.ParentSkillLineID, GetRace(), GetClass());
                        if (rcEntry != null)
                        {
                            var tier = Global.ObjectMgr.GetSkillTier(rcEntry.SkillTierID);
                            if (tier != null)
                                SetSkill(skillEntry.ParentSkillLineID, skillEntry.ParentTierIndex, Math.Max((int)GetPureSkillValue(skillEntry.ParentSkillLineID), 1), tier.GetValueForTierIndex(skillEntry.ParentTierIndex - 1));
                        }
                    }

                    // if skill value is going down, update enchantments before setting the new value
                    if (newVal < currVal)
                        UpdateSkillEnchantments(skill, currVal, newVal);

                    // update step
                    SetSkillStep(skillStatusData.Pos, step);
                    // update value
                    SetSkillRank(skillStatusData.Pos, newVal);
                    SetSkillMaxRank(skillStatusData.Pos, maxVal);

                    LearnSkillRewardedSpells(skill, newVal, GetRace());
                    // if skill value is going up, update enchantments after setting the new value
                    if (newVal > currVal)
                    {
                        UpdateSkillEnchantments(skill, currVal, newVal);
                        if (skill == SkillType.Riding)
                            UpdateMountCapability();
                    }

                    UpdateCriteria(CriteriaType.SkillRaised, (long)skill);
                    UpdateCriteria(CriteriaType.AchieveSkillStep, (long)skill);

                    // update skill state
                    if (skillStatusData.State == SkillState.Unchanged || skillStatusData.State == SkillState.Deleted)
                    {
                        if (currVal == 0)   // activated skill, mark as new to save into database
                        {
                            skillStatusData.State = skillStatusData.State != SkillState.Deleted ? SkillState.New : SkillState.Changed; // skills marked as SKILL_DELETED already exist in database, mark as changed instead of new

                            // Set profession line
                            int freeProfessionSlot = FindEmptyProfessionSlotFor(skill);
                            if (freeProfessionSlot != -1)
                                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ProfessionSkillLine, freeProfessionSlot), (int)skill);

                            refreshSkillBonusAuras();
                        }
                        else                // updated skill, mark as changed to save into database
                            skillStatusData.State = SkillState.Changed;
                    }
                }
                else if (currVal != 0 && newVal == 0) // Deactivate skill line
                {
                    // Try to store profession tools and accessories into the bag
                    // If we can't, we can't unlearn the profession
                    int professionSlot = GetProfessionSlotFor(skill);
                    if (professionSlot != -1)
                    {
                        byte professionSlotStart = (byte)(ProfessionSlots.Profession1Tool + professionSlot * ProfessionSlots.MaxCount);

                        // Get all profession items equipped
                        for (byte slotOffset = 0; slotOffset < ProfessionSlots.MaxCount; ++slotOffset)
                        {
                            Item professionItem = GetItemByPos((byte)(professionSlotStart + slotOffset));
                            if (professionItem != null)
                            {
                                // Store item in bag
                                if (CanStoreItem(ItemPos.Undefined, out var professionItemDest, professionItem) != InventoryResult.Ok)
                                {
                                    SendPacket(new DisplayGameError(GameError.InvFull));
                                    return;
                                }

                                RemoveItem(professionItem.InventoryPosition, true);
                                StoreItem(professionItemDest, professionItem, true);
                            }
                        }

                        // Clear profession lines
                        SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ProfessionSkillLine, professionSlot), 0);
                    }

                    //remove enchantments needing this skill
                    UpdateSkillEnchantments(skill, currVal, 0);
                    // clear skill fields
                    SetSkillStep(skillStatusData.Pos, 0);
                    SetSkillRank(skillStatusData.Pos, 0);
                    SetSkillStartingRank(skillStatusData.Pos, 1);
                    SetSkillMaxRank(skillStatusData.Pos, 0);
                    SetSkillTempBonus(skillStatusData.Pos, 0);
                    SetSkillPermBonus(skillStatusData.Pos, 0);

                    // mark as deleted so the next save will delete the data from the database
                    skillStatusData.State = skillStatusData.State != SkillState.New ? SkillState.Deleted : SkillState.Unchanged; // skills marked as SKILL_NEW don't exist in database (this distinction is not neccessary for deletion but for re-learning the same skill before save to db happens)

                    // remove all spells that related to this skill
                    var skillLineAbilities = Global.DB2Mgr.GetSkillLineAbilitiesBySkill(skill);
                    foreach (SkillLineAbilityRecord skillLineAbility in skillLineAbilities)
                        RemoveSpell(Global.SpellMgr.GetFirstSpellInChain(skillLineAbility.Spell));

                    var childSkillLines = Global.DB2Mgr.GetSkillLinesForParentSkill(skill);
                    foreach (SkillLineRecord childSkillLine in childSkillLines)
                    {
                        if (childSkillLine.ParentSkillLineID == skill)
                            SetSkill(childSkillLine.Id, 0, 0, 0);
                    }

                    // Clear profession lines
                    if (m_activePlayerData.ProfessionSkillLine[0] == (int)skill)
                        SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ProfessionSkillLine, 0), 0);
                    else if (m_activePlayerData.ProfessionSkillLine[1] == (int)skill)
                        SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ProfessionSkillLine, 1), 0);
                }
            }
            else
            {
                // We are about to learn a skill that has been added outside of normal circumstances (Game Master command, scripts etc.)
                byte skillSlot = 0;

                // Find a free skill slot
                for (int i = 0; i < SkillConst.MaxPlayerSkills; ++i)
                {
                    if (((SkillInfo)m_activePlayerData.Skill).SkillLineID[i] == 0)
                    {
                        skillSlot = (byte)i;
                        break;
                    }
                }

                if (skillSlot == 0)
                {
                    Log.outError(LogFilter.Misc,
                        $"Tried to add skill {skill} but player {GetName()} ({GetGUID()}) " +
                        $"cannot have additional skills");
                    return;
                }

                if (skillEntry.ParentSkillLineID != 0)
                {
                    if (skillEntry.ParentTierIndex > 0)
                    {
                        SkillRaceClassInfoRecord rcEntry = Global.DB2Mgr.GetSkillRaceClassInfo(skillEntry.ParentSkillLineID, GetRace(), GetClass());
                        if (rcEntry != null)
                        {
                            SkillTiersEntry tier = Global.ObjectMgr.GetSkillTier(rcEntry.SkillTierID);
                            if (tier != null)
                            {
                                int skillval = GetPureSkillValue(skillEntry.ParentSkillLineID);
                                SetSkill(skillEntry.ParentSkillLineID, skillEntry.ParentTierIndex, Math.Max(skillval, 1), tier.GetValueForTierIndex(skillEntry.ParentTierIndex - 1));
                            }
                        }
                    }
                }
                else
                {
                    // also learn missing child skills at 0 value
                    var childSkillLines = Global.DB2Mgr.GetSkillLinesForParentSkill(skill);
                    foreach (SkillLineRecord childSkillLine in childSkillLines)
                    {
                        if (!HasSkill(childSkillLine.Id))
                            SetSkill(childSkillLine.Id, 0, 0, 0);
                    }

                    int freeProfessionSlot = FindEmptyProfessionSlotFor(skill);
                    if (freeProfessionSlot != -1)
                        SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ProfessionSkillLine, freeProfessionSlot), (int)skill);
                }

                if (skillStatusData == null)
                    SetSkillLineId(skillSlot, skill);

                SetSkillStep(skillSlot, step);
                SetSkillRank(skillSlot, newVal);
                SetSkillStartingRank(skillSlot, 1);
                SetSkillMaxRank(skillSlot, maxVal);

                // apply skill bonuses
                SetSkillTempBonus(skillSlot, 0);
                SetSkillPermBonus(skillSlot, 0);

                UpdateSkillEnchantments(skill, 0, (ushort)newVal);

                mSkillStatus.Add(skill, new SkillStatusData(skillSlot, SkillState.New));

                if (newVal != 0)
                {
                    refreshSkillBonusAuras();

                    // Learn all spells for skill
                    LearnSkillRewardedSpells(skill, newVal, GetRace());
                    UpdateCriteria(CriteriaType.SkillRaised, (long)skill);
                    UpdateCriteria(CriteriaType.AchieveSkillStep, (long)skill);
                }
            }
        }

        public bool UpdateCraftSkill(SpellInfo spellInfo)
        {
            if (spellInfo.HasAttribute(SpellAttr1.NoSkillIncrease))
                return false;

            Log.outDebug(LogFilter.Player, $"UpdateCraftSkill spellid {spellInfo.Id}");

            var bounds = Global.SpellMgr.GetSkillLineAbilityMapBounds(spellInfo.Id);

            foreach (var _spell_idx in bounds)
            {
                if (_spell_idx.SkillupSkillLineID != 0)
                {
                    int SkillValue = GetPureSkillValue(_spell_idx.SkillupSkillLineID);

                    // Alchemy Discoveries here
                    if (spellInfo.Mechanic == Mechanics.Discovery)
                    {
                        int discoveredSpell = SkillDiscovery.GetSkillDiscoverySpell(_spell_idx.SkillupSkillLineID, spellInfo.Id, this);
                        if (discoveredSpell != 0)
                            LearnSpell(discoveredSpell, false);
                    }

                    int craft_skill_gain = _spell_idx.NumSkillUps * WorldConfig.Values[WorldCfg.SkillGainCrafting].Int32;

                    return UpdateSkillPro(_spell_idx.SkillupSkillLineID, SkillGainChance(SkillValue, _spell_idx.TrivialSkillLineRankHigh,
                        (_spell_idx.TrivialSkillLineRankHigh + _spell_idx.TrivialSkillLineRankLow) / 2, _spell_idx.TrivialSkillLineRankLow), craft_skill_gain);
                }
            }
            return false;
        }

        public bool UpdateGatherSkill(SkillType skillId, int skillValue, int redLevel, int multiplicator = 1, WorldObject obj = null)
        {
            return UpdateGatherSkill((int)skillId, skillValue, redLevel, multiplicator, obj);
        }

        public bool UpdateGatherSkill(int skillId, int skillValue, int redLevel, int multiplicator = 1, WorldObject obj = null)
        {
            Log.outDebug(LogFilter.Player, $"UpdateGatherSkill(SkillId {skillId} SkillLevel {skillValue} RedLevel {redLevel})");

            SkillLineRecord skillEntry = CliDB.SkillLineStorage.LookupByKey(skillId);
            if (skillEntry == null)
                return false;

            int gatheringSkillGain = WorldConfig.Values[WorldCfg.SkillGainGathering].Int32;

            int baseSkillLevelStep = 30;
            int yellowLevel = redLevel + baseSkillLevelStep;
            int greenLevel = yellowLevel + baseSkillLevelStep;
            int grayLevel = greenLevel + baseSkillLevelStep;

            GameObject go = obj?.ToGameObject();
            if (go != null)
            {
                if (go.GetGoInfo().GetTrivialSkillLow() != 0)
                    yellowLevel = go.GetGoInfo().GetTrivialSkillLow();

                if (go.GetGoInfo().GetTrivialSkillHigh() != 0)
                    grayLevel = go.GetGoInfo().GetTrivialSkillHigh();

                greenLevel = (yellowLevel + grayLevel) / 2;
            }

            // For skinning and Mining chance decrease with level. 1-74 - no decrease, 75-149 - 2 times, 225-299 - 8 times
            switch (skillEntry.ParentSkillLineID)
            {
                case SkillType.Herbalism:
                    return UpdateSkillPro(skillId, SkillGainChance(skillValue, grayLevel, greenLevel, yellowLevel) * multiplicator, gatheringSkillGain);
                case SkillType.Skinning:
                    if (WorldConfig.Values[WorldCfg.SkillChanceSkinningSteps].Int32 == 0)
                        return UpdateSkillPro(skillId, SkillGainChance(skillValue, grayLevel, greenLevel, yellowLevel) * multiplicator, gatheringSkillGain);
                    else
                        return UpdateSkillPro(skillId, (SkillGainChance(skillValue, grayLevel, greenLevel, yellowLevel) * multiplicator) >> (skillValue / WorldConfig.Values[WorldCfg.SkillChanceSkinningSteps].Int32), gatheringSkillGain);
                case SkillType.Mining:
                    if (WorldConfig.Values[WorldCfg.SkillChanceMiningSteps].Int32 == 0)
                        return UpdateSkillPro(skillId, SkillGainChance(skillValue, grayLevel, greenLevel, yellowLevel) * multiplicator, gatheringSkillGain);
                    else
                        return UpdateSkillPro(skillId, (SkillGainChance(skillValue, grayLevel, greenLevel, yellowLevel) * multiplicator) >> (skillValue / WorldConfig.Values[WorldCfg.SkillChanceMiningSteps].Int32), gatheringSkillGain);
            }
            return false;
        }

        byte GetFishingStepsNeededToLevelUp(int SkillValue)
        {
            // These formulas are guessed to be as close as possible to how the skill difficulty curve for fishing was on Retail.
            if (SkillValue < 75)
                return 1;

            if (SkillValue <= 300)
                return (byte)(SkillValue / 44);

            return (byte)(SkillValue / 31);
        }

        public bool UpdateFishingSkill(int expansion)
        {
            Log.outDebug(LogFilter.Player, 
                $"Player::UpdateFishingSkill: Player '{GetName()}' ({GetGUID()}) Expansion: {expansion}");

            var fishingSkill = GetProfessionSkillForExp(SkillType.Fishing, expansion);
            if (fishingSkill == 0 || !HasSkill(fishingSkill))
                return false;

            int skillValue = GetPureSkillValue(fishingSkill);

            if (skillValue >= GetMaxSkillValue(fishingSkill))
                return false;

            byte stepsNeededToLevelUp = GetFishingStepsNeededToLevelUp(skillValue);
            ++m_fishingSteps;

            if (m_fishingSteps >= stepsNeededToLevelUp)
            {
                m_fishingSteps = 0;

                int gatheringSkillGain = WorldConfig.Values[WorldCfg.SkillGainGathering].Int32;
                return UpdateSkillPro(fishingSkill, 100 * 10, gatheringSkillGain);
            }

            return false;
        }

        public SkillType GetProfessionSkillForExp(SkillType skill, int expansion)
        {
            return GetProfessionSkillForExp((int)skill, expansion);
        }

        public SkillType GetProfessionSkillForExp(int skill, int expansion)
        {
            SkillLineRecord skillEntry = CliDB.SkillLineStorage.LookupByKey(skill);
            if (skillEntry == null)
                return 0;

            if (skillEntry.ParentSkillLineID != 0 ||
                (skillEntry.CategoryID != SkillCategory.Profession && skillEntry.CategoryID != SkillCategory.Secondary))
            {
                return 0;
            }

            // The value -3 from ContentTuning refers to the current expansion
            if (expansion < 0)
                expansion = (int)PlayerConst.CurrentExpansion;

            var childSkillLines = Global.DB2Mgr.GetSkillLinesForParentSkill(skillEntry.Id);
            if (childSkillLines != null)
            {
                foreach (var childSkillLine in childSkillLines)
                {
                    // Values of ParentTierIndex in SkillLine.db2 start at 4 (Classic) and increase by one for each expansion skillLine
                    // Subtract 4 (BASE_PARENT_TIER_INDEX) from this value to obtain the expansion of the skillLine
                    int skillLineExpansion = childSkillLine.ParentTierIndex - 4;
                    if (expansion == skillLineExpansion)
                        return childSkillLine.Id;
                }
            }

            return 0;
        }
        
        int SkillGainChance(int SkillValue, int GrayLevel, int GreenLevel, int YellowLevel)
        {
            if (SkillValue >= GrayLevel)
                return WorldConfig.Values[WorldCfg.SkillChanceGrey].Int32 * 10;
            if (SkillValue >= GreenLevel)
                return WorldConfig.Values[WorldCfg.SkillChanceGreen].Int32 * 10;
            if (SkillValue >= YellowLevel)
                return WorldConfig.Values[WorldCfg.SkillChanceYellow].Int32 * 10;
            return WorldConfig.Values[WorldCfg.SkillChanceOrange].Int32 * 10;
        }

        bool EnchantmentFitsRequirements(int enchantmentcondition, int slot)
        {
            if (enchantmentcondition == 0)
                return true;

            SpellItemEnchantmentConditionRecord Condition = 
                CliDB.SpellItemEnchantmentConditionStorage.LookupByKey(enchantmentcondition);

            if (Condition == null)
                return true;

            byte[] curcount = [0, 0, 0, 0];

            //counting current equipped gem colors
            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
            {
                if (i == slot)
                    continue;

                Item pItem2 = GetItemByPos(i);
                if (pItem2 != null && !pItem2.IsBroken())
                {
                    foreach (SocketedGem gemData in pItem2.m_itemData.Gems)
                    {
                        if (gemData == null)
                            continue;

                        ItemTemplate gemProto = Global.ObjectMgr.GetItemTemplate(gemData.ItemId.GetValue());
                        if (gemProto == null)
                            continue;

                        GemPropertiesRecord gemProperty = CliDB.GemPropertiesStorage.LookupByKey(gemProto.GetGemProperties());
                        if (gemProperty == null)
                            continue;

                        uint GemColor = (uint)gemProperty.Color;

                        for (byte b = 0, tmpcolormask = 1; b < 4; b++, tmpcolormask <<= 1)
                        {
                            if (Convert.ToBoolean(tmpcolormask & GemColor))
                                ++curcount[b];
                        }
                    }
                }
            }

            bool activate = true;

            for (byte i = 0; i < 5; i++)
            {
                if (Condition.LtOperandType[i] == 0)
                    continue;

                uint _cur_gem = curcount[Condition.LtOperandType[i] - 1];

                // if have <CompareColor> use them as count, else use <value> from Condition
                uint _cmp_gem = Condition.RtOperandType[i] != 0 ? curcount[Condition.RtOperandType[i] - 1] : Condition.RtOperand[i];

                switch (Condition.Operator[i])
                {
                    case 2:                                         // requires less <color> than (<value> || <comparecolor>) gems
                        activate &= (_cur_gem < _cmp_gem);
                        break;
                    case 3:                                         // requires more <color> than (<value> || <comparecolor>) gems
                        activate &= (_cur_gem > _cmp_gem);
                        break;
                    case 5:                                         // requires at least <color> than (<value> || <comparecolor>) gems
                        activate &= (_cur_gem >= _cmp_gem);
                        break;
                }
            }

            Log.outDebug(LogFilter.Player,
                $"Checking Condition {enchantmentcondition}, " +
                $"there are {curcount[0]} Meta Gems, {curcount[1]} Red Gems, {curcount[2]} " +
                $"Yellow Gems and {curcount[3]} Blue Gems, Activate:{activate}");

            return activate;
        }

        void CorrectMetaGemEnchants(byte exceptslot, bool apply)
        {
            //cycle all equipped items
            for (byte slot = EquipmentSlot.Start; slot < EquipmentSlot.End; ++slot)
            {
                //enchants for the slot being socketed are handled by Player.ApplyItemMods
                if (slot == exceptslot)
                    continue;

                Item pItem = GetItemByPos(slot);

                if (pItem == null || pItem.GetSocketType(0) == 0)
                    continue;

                for (EnchantmentSlot enchant_slot = EnchantmentSlot.EnhancementSocket; enchant_slot < EnchantmentSlot.EnhancementSocket3; ++enchant_slot)
                {
                    int enchant_id = pItem.GetEnchantmentId(enchant_slot);
                    if (enchant_id == 0)
                        continue;

                    SpellItemEnchantmentRecord enchantEntry = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                    if (enchantEntry == null)
                        continue;

                    int condition = enchantEntry.ConditionID;
                    if (condition != 0)
                    {
                        //was enchant active with/without item?
                        bool wasactive = EnchantmentFitsRequirements(condition, (apply ? exceptslot : -1));
                        //should it now be?
                        if (wasactive ^ EnchantmentFitsRequirements(condition, (apply ? -1 : exceptslot)))
                        {
                            // ignore item gem conditions
                            //if state changed, (dis)apply enchant
                            ApplyEnchantment(pItem, enchant_slot, !wasactive, true, true);
                        }
                    }
                }
            }
        }

        public void CastItemUseSpell(Item item, SpellCastTargets targets, ObjectGuid castCount, int[] misc)
        {
            if (!item.GetTemplate().HasFlag(ItemFlags.Legacy))
            {
                // item spells casted at use
                foreach (ItemEffectRecord effectData in item.GetEffects())
                {
                    // wrong triggering Type
                    if (effectData.TriggerType != ItemSpelltriggerType.OnUse)
                        continue;

                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(effectData.SpellID, Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player.CastItemUseSpell: Item (Entry: {item.GetEntry()}) " +
                            $"have wrong spell id {effectData.SpellID}, ignoring");
                        continue;
                    }

                    Spell spell = new(this, spellInfo, TriggerCastFlags.None);

                    SpellPrepare spellPrepare = new();
                    spellPrepare.ClientCastID = castCount;
                    spellPrepare.ServerCastID = spell.m_castId;
                    SendPacket(spellPrepare);

                    spell.m_fromClient = true;
                    spell.m_CastItem = item;
                    spell.m_misc.Data0 = misc[0];
                    spell.m_misc.Data1 = misc[1];
                    spell.Prepare(targets);
                    return;
                }
            }

            // Item enchantments spells casted at use
            for (EnchantmentSlot e_slot = 0; e_slot < EnchantmentSlot.Max; ++e_slot)
            {
                int enchant_id = item.GetEnchantmentId(e_slot);
                var pEnchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                if (pEnchant == null)
                    continue;
                for (byte s = 0; s < ItemConst.MaxItemEnchantmentEffects; ++s)
                {
                    if (pEnchant.Effect(s) != ItemEnchantmentType.UseSpell)
                        continue;

                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(pEnchant.EffectArg[s], Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player.CastItemUseSpell Enchant {enchant_id}, " +
                            $"cast unknown spell {pEnchant.EffectArg[s]}");
                        continue;
                    }

                    Spell spell = new(this, spellInfo, TriggerCastFlags.None);

                    SpellPrepare spellPrepare = new();
                    spellPrepare.ClientCastID = castCount;
                    spellPrepare.ServerCastID = spell.m_castId;
                    SendPacket(spellPrepare);

                    spell.m_fromClient = true;
                    spell.m_CastItem = item;
                    spell.m_misc.Data0 = misc[0];
                    spell.m_misc.Data1 = misc[1];
                    spell.Prepare(targets);
                    return;
                }
            }
        }

        public int GetLastPotionId() { return m_lastPotionId; }
        public void SetLastPotionId(int item_id) { m_lastPotionId = item_id; }

        public void LearnSkillRewardedSpells(SkillType skillId, int skillValue, Race race)
        {
            Class class_ = GetClass();

            var skillLineAbilities = Global.DB2Mgr.GetSkillLineAbilitiesBySkill(skillId);
            foreach (var ability in skillLineAbilities)
            {
                if (ability.SkillLine != skillId)
                    continue;

                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(ability.Spell, Difficulty.None);
                if (spellInfo == null)
                    continue;

                switch (ability.AcquireMethod)
                {
                    case AbilityLearnType.OnSkillValue:
                    case AbilityLearnType.OnSkillLearn:
                        break;
                    case AbilityLearnType.RewardedFromQuest:
                        if (!ability.HasFlag(SkillLineAbilityFlags.CanFallbackToLearnedOnSkillLearn) ||
                            !spellInfo.MeetsFutureSpellPlayerCondition(this))
                            continue;
                        break;
                    default:
                        continue;
                }

                // AcquireMethod == 2 && NumSkillUps == 1 --> automatically learn riding skill spell,
                // else we skip it (client shows riding in spellbook as trainable).
                if (skillId == SkillType.Riding
                    && (ability.AcquireMethod != AbilityLearnType.OnSkillLearn 
                    || ability.NumSkillUps != 1))
                    continue;

                // Check race if set
                var raceMask = ability.RaceMask;
                if (raceMask != RaceMask.None && !raceMask.HasRace(race))
                    continue;

                // Check class if set
                if (ability.ClassMask != 0 && !ability.ClassMask.HasClass(class_))
                    continue;

                // Check level, skip class spells if not high enough
                int requiredLevel = Math.Max(spellInfo.SpellLevel, spellInfo.BaseLevel);

                if (requiredLevel > GetLevel())
                    continue;

                // need unlearn spell
                if (skillValue < ability.MinSkillLineRank && ability.AcquireMethod == AbilityLearnType.OnSkillValue)
                    RemoveSpell(ability.Spell);
                // need learn
                else if (!IsInWorld)
                    AddSpell(ability.Spell, true, true, true, false, false, ability.SkillLine);
                else
                    LearnSpell(ability.Spell, true, ability.SkillLine);

            }
        }

        int GetProfessionSlotFor(SkillType skillId)
        {
            for (var i = 0; i < m_activePlayerData.ProfessionSkillLine.GetSize(); ++i)
            {
                if (m_activePlayerData.ProfessionSkillLine[i] == (int)skillId)
                    return i;
            }

            return -1;
        }
        
        int FindEmptyProfessionSlotFor(SkillType skillId)
        {
            SkillLineRecord skillEntry = CliDB.SkillLineStorage.LookupByKey((int)skillId);
            if (skillEntry == null)
                return -1;

            if (skillEntry.ParentSkillLineID != 0 || skillEntry.CategoryID != SkillCategory.Profession)
                return -1;

            int index = 0;
            // if there is no same profession, find any free slot
            foreach (var b in m_activePlayerData.ProfessionSkillLine)
            {
                if (b == 0)
                    return index;

                index++;
            }

            return -1;
        }

        void RemoveItemDependentAurasAndCasts(Item pItem)
        {
            foreach (var pair in GetOwnedAurasCopy())
            {
                Aura aura = pair.Value;

                // skip not self applied auras
                SpellInfo spellInfo = aura.GetSpellInfo();
                if (aura.GetCasterGUID() != GetGUID())
                    continue;

                // skip if not item dependent or have alternative item
                if (HasItemFitToSpellRequirements(spellInfo, pItem))
                    continue;

                // no alt item, remove aura, restart check
                RemoveOwnedAura(pair);
            }

            // currently casted spells can be dependent from item
            for (CurrentSpellTypes i = 0; i < CurrentSpellTypes.Max; ++i)
            {
                Spell spell = GetCurrentSpell(i);
                if (spell != null)
                {
                    if (spell.GetState() != SpellState.Delayed && !HasItemFitToSpellRequirements(spell.m_spellInfo, pItem))
                        InterruptSpell(i);
                }
            }
        }

        public bool HasItemFitToSpellRequirements(SpellInfo spellInfo, Item ignoreItem = null)
        {
            if (spellInfo.EquippedItemClass < 0)
                return true;

            // scan other equipped items for same requirements (mostly 2 daggers/etc)
            // for optimize check 2 used cases only
            switch (spellInfo.EquippedItemClass)
            {
                case ItemClass.Weapon:
                {
                    for (byte slot = EquipmentSlot.MainHand; slot <= EquipmentSlot.Ranged; slot++)
                    {
                        Item item = GetUseableItemByPos(new(slot));
                        if (item != null)
                        {
                            if (item != ignoreItem && item.IsFitToSpellRequirements(spellInfo))
                                return true;
                        }
                    }

                    break;
                }
                case ItemClass.Armor:
                {
                    if (!spellInfo.HasAttribute(SpellAttr8.RequiresEquippedInvTypes))
                    {
                        // most used check: shield only
                        if ((spellInfo.EquippedItemSubClassMask & (1 << (int)ItemSubClassArmor.Shield)) != 0)
                        {
                            Item item = GetUseableItemByPos(new(EquipmentSlot.OffHand));
                            if (item != null)
                            {
                                if (item != ignoreItem && item.IsFitToSpellRequirements(spellInfo))
                                    return true;
                            }

                            // special check to filter things like Shield Wall, the aura is not permanent and must stay even without required item
                            if (!spellInfo.IsPassive())
                            {
                                foreach (var spellEffectInfo in spellInfo.GetEffects())
                                {
                                    if (spellEffectInfo.IsAura())
                                        return true;
                                }
                            }
                        }

                        // tabard not have dependent spells
                        for (byte i = EquipmentSlot.Start; i < EquipmentSlot.MainHand; ++i)
                        {
                            Item item = GetUseableItemByPos(new(i));
                            if (item != null)
                            {
                                if (item != ignoreItem && item.IsFitToSpellRequirements(spellInfo))
                                    return true;
                            }
                        }
                    }
                    else
                    {
                        // requires item equipped in all armor slots
                        foreach (byte i in new[] { EquipmentSlot.Head, EquipmentSlot.Shoulders, EquipmentSlot.Chest, EquipmentSlot.Waist, EquipmentSlot.Legs, EquipmentSlot.Feet, EquipmentSlot.Wrist, EquipmentSlot.Hands })
                        {
                            Item item = GetUseableItemByPos(new(i));
                            if (item == null || item == ignoreItem || !item.IsFitToSpellRequirements(spellInfo))
                                return false;
                        }

                        return true;
                    }
                    break;
                }
                default:
                    Log.outError(LogFilter.Player, 
                        $"HasItemFitToSpellRequirements: Not handled spell requirement " +
                        $"for item class {spellInfo.EquippedItemClass}");
                    break;
            }

            return false;
        }

        public Dictionary<int, PlayerSpell> GetSpellMap() { return m_spells; }

        public override SpellSchools GetMeleeDamageSchool(WeaponAttackType attackType = WeaponAttackType.BaseAttack)
        {
            Item weapon = GetWeaponForAttack(attackType, true);
            if (weapon != null)
                return weapon.GetTemplate().GetDamageType();

            return SpellSchools.Normal;
        }

        void CastAllObtainSpells()
        {
            int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
            for (byte slot = InventorySlots.ItemStart; slot < inventoryEnd; ++slot)
            {
                Item item = GetItemByPos(slot);
                if (item != null)
                    ApplyItemObtainSpells(item, true);
            }

            for (byte i = InventorySlots.BagStart; i < InventorySlots.BagEnd; ++i)
            {
                Bag bag = GetBagByPos(i);
                if (bag == null)
                    continue;

                for (byte slot = 0; slot < bag.GetBagSize(); ++slot)
                {
                    Item item = bag.GetItemByPos(slot);
                    if (item != null)
                        ApplyItemObtainSpells(item, true);
                }
            }
        }

        void ApplyItemObtainSpells(Item item, bool apply)
        {
            if (item.GetTemplate().HasFlag(ItemFlags.Legacy))
                return;

            foreach (ItemEffectRecord effect in item.GetEffects())
            {
                if (effect.TriggerType != ItemSpelltriggerType.OnPickup) // On obtain trigger
                    continue;

                int spellId = effect.SpellID;
                if (spellId <= 0)
                    continue;

                if (apply)
                {
                    if (!HasAura(spellId))
                        CastSpell(this, spellId, new CastSpellExtraArgs().SetCastItem(item));
                }
                else
                    RemoveAurasDueToSpell(spellId);
            }
        }

        // this one rechecks weapon auras and stores them in BaseModGroup container
        // needed for things like axe specialization applying only to axe weapons in case of dual-wield
        void UpdateWeaponDependentCritAuras(WeaponAttackType attackType)
        {
            BaseModGroup modGroup;
            switch (attackType)
            {
                case WeaponAttackType.BaseAttack:
                    modGroup = BaseModGroup.CritPercentage;
                    break;
                case WeaponAttackType.OffAttack:
                    modGroup = BaseModGroup.OffhandCritPercentage;
                    break;
                case WeaponAttackType.RangedAttack:
                    modGroup = BaseModGroup.RangedCritPercentage;
                    break;
                default:
                    return;
            }

            float amount = GetTotalAuraModifier(AuraType.ModWeaponCritPct, auraEffect => CheckAttackFitToAuraRequirement(attackType, auraEffect));

            SetBaseModFlatValue(modGroup, amount);
        }

        public void UpdateAllWeaponDependentCritAuras()
        {
            for (var attackType = WeaponAttackType.BaseAttack; attackType < WeaponAttackType.Max; ++attackType)
                UpdateWeaponDependentCritAuras(attackType);
        }

        public void UpdateWeaponDependentAuras(WeaponAttackType attackType)
        {
            UpdateWeaponDependentCritAuras(attackType);
            UpdateDamageDoneMods(attackType);
            UpdateDamagePctDoneMods(attackType);
        }

        public void ApplyItemDependentAuras(Item item, bool apply)
        {
            if (apply)
            {
                var spells = GetSpellMap();
                foreach (var pair in spells)
                {
                    if (pair.Value.State == PlayerSpellState.Removed || pair.Value.Disabled)
                        continue;

                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(pair.Key, Difficulty.None);
                    if (spellInfo == null || !spellInfo.IsPassive() || spellInfo.EquippedItemClass < 0)
                        continue;

                    if (!HasAura(pair.Key) && HasItemFitToSpellRequirements(spellInfo))
                        AddAura(pair.Key, this);  // no SMSG_SPELL_GO in sniff found
                }
            }
            else
                RemoveItemDependentAurasAndCasts(item);
        }

        public override bool CheckAttackFitToAuraRequirement(WeaponAttackType attackType, AuraEffect aurEff)
        {
            SpellInfo spellInfo = aurEff.GetSpellInfo();
            if (spellInfo.EquippedItemClass == ItemClass.None)
                return true;

            Item item = GetWeaponForAttack(attackType, true);
            if (item == null || !item.IsFitToSpellRequirements(spellInfo))
                return false;

            return true;
        }

        public void AddTemporarySpell(int spellId)
        {
            var spell = m_spells.LookupByKey(spellId);
            // spell already added - do not do anything
            if (spell != null)
                return;

            PlayerSpell newspell = new();
            newspell.State = PlayerSpellState.Temporary;
            newspell.Active = true;
            newspell.Dependent = false;
            newspell.Disabled = false;

            m_spells[spellId] = newspell;
        }

        public void RemoveTemporarySpell(int spellId)
        {
            var spell = m_spells.LookupByKey(spellId);
            // spell already not in list - do not do anything
            if (spell == null)
                return;

            // spell has other state than temporary - do not change it
            if (spell.State != PlayerSpellState.Temporary)
                return;

            m_spells.Remove(spellId);
        }

        public void UpdateZoneDependentAuras(int newZone)
        {
            // Some spells applied at enter into zone (with subzones), aura removed in UpdateAreaDependentAuras that called always at zone.area update
            var saBounds = Global.SpellMgr.GetSpellAreaForAreaMapBounds(newZone);
            foreach (var spell in saBounds)
                if (spell.flags.HasAnyFlag(SpellAreaFlag.AutoCast) && spell.IsFitToRequirements(this, newZone, 0))
                    if (!HasAura(spell.spellId))
                        CastSpell(this, spell.spellId, true);
        }

        public void UpdateAreaDependentAuras(int newArea)
        {
            // remove auras from spells with area limitations
            foreach (var pair in GetOwnedAurasCopy())
            {
                // use m_zoneUpdateId for speed: UpdateArea called from UpdateZone or instead UpdateZone in both cases m_zoneUpdateId up-to-date
                if (pair.Value.GetSpellInfo().CheckLocation(GetMapId(), m_zoneUpdateId, newArea, this) != SpellCastResult.SpellCastOk)
                    RemoveOwnedAura(pair);
            }

            // some auras applied at subzone enter
            var saBounds = Global.SpellMgr.GetSpellAreaForAreaMapBounds(newArea);
            foreach (var spell in saBounds)
            {
                if (spell.flags.HasAnyFlag(SpellAreaFlag.AutoCast) && spell.IsFitToRequirements(this, m_zoneUpdateId, newArea))
                {
                    if (!HasAura(spell.spellId))
                        CastSpell(this, spell.spellId, true);
                }
            }
        }

        public void ApplyModToSpell(SpellModifier mod, Spell spell)
        {
            if (spell == null)
                return;

            // don't do anything with no charges
            if (mod.ownerAura.IsUsingCharges() && mod.ownerAura.GetCharges() == 0)
                return;

            // register inside spell, proc system uses this to drop charges
            spell.m_appliedMods.Add(mod.ownerAura);
        }

        public void LearnCustomSpells()
        {
            if (!WorldConfig.Values[WorldCfg.StartAllSpells].Bool)
                return;

            // learn default race/class spells
            PlayerInfo info = Global.ObjectMgr.GetPlayerInfo(GetRace(), GetClass());
            foreach (var tspell in info.customSpells)
            {
                Log.outDebug(LogFilter.Player, 
                    $"PLAYER (Class: {GetClass()} Race: {GetRace()}): " +
                    $"Adding initial spell, id = {tspell}");

                if (!IsInWorld)                                    // will send in INITIAL_SPELLS in list anyway at map add
                    AddSpell(tspell, true, true, true, false);
                else                                                // but send in normal spell in game learn case
                    LearnSpell(tspell, true);
            }
        }

        public void LearnDefaultSkills()
        {
            // learn default race/class skills
            PlayerInfo info = Global.ObjectMgr.GetPlayerInfo(GetRace(), GetClass());
            foreach (var rcInfo in info.skills)
            {
                if (HasSkill(rcInfo.SkillID))
                    continue;

                if (rcInfo.MinLevel > GetLevel())
                    continue;

                LearnDefaultSkill(rcInfo);
            }
        }

        public void LearnDefaultSkill(SkillRaceClassInfoRecord rcInfo)
        {
            SkillType skillId = rcInfo.SkillID;
            switch (Global.SpellMgr.GetSkillRangeType(rcInfo))
            {
                case SkillRangeType.Language:
                    SetSkill(skillId, 0, 300, 300);
                    break;
                case SkillRangeType.Level:
                {
                    ushort skillValue = 1;
                    ushort maxValue = GetMaxSkillValueForLevel();
                    if (rcInfo.HasFlag(SkillRaceClassInfoFlags.AlwaysMaxValue))
                        skillValue = maxValue;
                    else if (GetClass() == Class.DeathKnight)
                        skillValue = (ushort)Math.Min(Math.Max(1, (GetLevel() - 1) * 5), maxValue);

                    SetSkill(skillId, 0, skillValue, maxValue);
                    break;
                }
                case SkillRangeType.Mono:
                    SetSkill(skillId, 0, 1, 1);
                    break;
                case SkillRangeType.Rank:
                {
                    SkillTiersEntry tier = Global.ObjectMgr.GetSkillTier(rcInfo.SkillTierID);
                    ushort maxValue = (ushort)tier.GetValueForTierIndex(0);
                    ushort skillValue = 1;
                    if (rcInfo.HasFlag(SkillRaceClassInfoFlags.AlwaysMaxValue))
                        skillValue = maxValue;
                    else if (GetClass() == Class.DeathKnight)
                        skillValue = (ushort)Math.Min(Math.Max(1, (GetLevel() - 1) * 5), maxValue);

                    SetSkill(skillId, 1, skillValue, maxValue);
                    break;
                }
                default:
                    break;
            }
        }

        void SendKnownSpells()
        {
            SendKnownSpells knownSpells = new();
            knownSpells.InitialLogin = IsLoading();

            foreach (var spell in m_spells.ToList())
            {
                if (spell.Value.State == PlayerSpellState.Removed)
                    continue;

                if (!spell.Value.Active || spell.Value.Disabled)
                    continue;

                knownSpells.KnownSpells.Add(spell.Key);
                if (spell.Value.Favorite)
                    knownSpells.FavoriteSpells.Add(spell.Key);
            }

            SendPacket(knownSpells);
        }

        void SendUnlearnSpells()
        {
            SendPacket(new SendUnlearnSpells());
        }

        public void LearnSpell(int spellId, bool dependent, SkillType fromSkill = 0, bool suppressMessaging = false, int? traitDefinitionId = null)
        {
            PlayerSpell spell = m_spells.LookupByKey(spellId);

            bool disabled = (spell != null) && spell.Disabled;
            bool active = !disabled || spell.Active;
            bool favorite = spell != null ? spell.Favorite : false;

            bool learning = AddSpell(spellId, active, true, dependent, false, false, fromSkill, favorite, traitDefinitionId);

            // prevent duplicated entires in spell book, also not send if not in world (loading)
            if (learning && IsInWorld)
            {
                LearnedSpells learnedSpells = new();
                LearnedSpellInfo learnedSpellInfo = new();
                learnedSpellInfo.SpellID = spellId;
                learnedSpellInfo.IsFavorite = favorite;
                learnedSpellInfo.TraitDefinitionID = traitDefinitionId;
                learnedSpells.SuppressMessaging = suppressMessaging;
                learnedSpells.ClientLearnedSpellData.Add(learnedSpellInfo);
                SendPacket(learnedSpells);
            }

            // learn all disabled higher ranks and required spells (recursive)
            if (disabled)
            {
                var nextSpell = Global.SpellMgr.GetNextSpellInChain(spellId);
                if (nextSpell != 0)
                {
                    var _spell = m_spells.LookupByKey(nextSpell);
                    if (spellId != 0 && _spell.Disabled)
                        LearnSpell(nextSpell, false, fromSkill);
                }

                var spellsRequiringSpell = Global.SpellMgr.GetSpellsRequiringSpellBounds(spellId);
                foreach (var id in spellsRequiringSpell)
                {
                    var spell1 = m_spells.LookupByKey(id);
                    if (spell1 != null && spell1.Disabled)
                        LearnSpell(id, false, fromSkill);
                }
            }
            else
                UpdateQuestObjectiveProgress(QuestObjectiveType.LearnSpell, spellId, 1);
        }

        public void RemoveSpell(int spellId, bool disabled = false, bool learnLowRank = true, bool suppressMessaging = false)
        {
            var pSpell = m_spells.LookupByKey(spellId);
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
                if (HasSpell(nextSpell) && !spellInfo1.HasAttribute(SpellCustomAttributes.IsTalent))
                    RemoveSpell(nextSpell, disabled, false);
            }
            //unlearn spells dependent from recently removed spells
            var spellsRequiringSpell = Global.SpellMgr.GetSpellsRequiringSpellBounds(spellId);
            foreach (var id in spellsRequiringSpell)
                RemoveSpell(id, disabled);

            // re-search, it can be corrupted in prev loop
            pSpell = m_spells.LookupByKey(spellId);
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
                    m_spells.Remove(spellId);
                else
                    pSpell.State = PlayerSpellState.Removed;
            }

            RemoveOwnedAura(spellId, GetGUID());

            // remove pet auras
            for (byte i = 0; i < SpellConst.MaxEffects; ++i)
            {
                PetAura petSpell = Global.SpellMgr.GetPetAura(spellId, i);
                if (petSpell != null)
                    RemovePetAura(petSpell);
            }

            // update free primary prof.points (if not overflow setting, can be in case GM use before .learn prof. learning)
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
            if (spellInfo != null && spellInfo.IsPrimaryProfessionFirstRank())
            {
                int freeProfs = GetFreePrimaryProfessionPoints() + 1;
                if (freeProfs <= WorldConfig.Values[WorldCfg.MaxPrimaryTradeSkill].Int32)
                    SetFreePrimaryProfessions(freeProfs);
            }

            // remove dependent skill
            var spellLearnSkill = Global.SpellMgr.GetSpellLearnSkill(spellId);
            if (spellLearnSkill != null)
            {
                int prev_spell = Global.SpellMgr.GetPrevSpellInChain(spellId);
                if (prev_spell == 0)                                    // first rank, remove skill
                    SetSkill(spellLearnSkill.skill, 0, 0, 0);
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
                        SetSkill(spellLearnSkill.skill, 0, 0, 0);
                    else                                            // set to prev. skill setting values
                    {
                        ushort skill_value = GetPureSkillValue(prevSkill.skill);
                        ushort skill_max_value = GetPureMaxSkillValue(prevSkill.skill);

                        ushort new_skill_max_value = prevSkill.maxvalue;

                        if (new_skill_max_value == 0)
                        {
                            var rcInfo = Global.DB2Mgr.GetSkillRaceClassInfo(prevSkill.skill, GetRace(), GetClass());
                            if (rcInfo != null)
                            {
                                switch (Global.SpellMgr.GetSkillRangeType(rcInfo))
                                {
                                    case SkillRangeType.Language:
                                        skill_value = 300;
                                        new_skill_max_value = 300;
                                        break;
                                    case SkillRangeType.Level:
                                        new_skill_max_value = GetMaxSkillValueForLevel();
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

                        SetSkill(prevSkill.skill, prevSkill.step, skill_value, skill_max_value);
                    }
                }
            }

            // remove dependent spells
            var spell_bounds = Global.SpellMgr.GetSpellLearnSpellMapBounds(spellId);

            foreach (var spellNode in spell_bounds)
            {
                RemoveSpell(spellNode.Spell, disabled);
                if (spellNode.OverridesSpell != 0)
                    RemoveOverrideSpell(spellNode.OverridesSpell, spellNode.Spell);
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
                    var prevSpell = m_spells.LookupByKey(prev_id);
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
                            if (AddSpell(prev_id, true, false, prevSpell.Dependent, prevSpell.Disabled))
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
                    RemoveOverrideSpell(traitDefinition.OverridesSpellID, spellId);
            }

            m_overrideSpells.Remove(spellId);

            if (m_canTitanGrip)
            {
                if (spellInfo != null && spellInfo.IsPassive() && spellInfo.HasEffect(SpellEffectName.TitanGrip))
                {
                    RemoveAurasDueToSpell(m_titanGripPenaltySpellId);
                    SetCanTitanGrip(false);
                }
            }

            if (CanDualWield())
            {
                if (spellInfo != null && spellInfo.IsPassive() && spellInfo.HasEffect(SpellEffectName.DualWield))
                    SetCanDualWield(false);
            }

            if (WorldConfig.Values[WorldCfg.OffhandCheckAtSpellUnlearn].Bool)
                AutoUnequipOffhandIfNeed();

            // remove from spell book if not replaced by lesser rank
            if (!prev_activate)
            {
                UnlearnedSpells unlearnedSpells = new();
                unlearnedSpells.SpellID.Add(spellId);
                unlearnedSpells.SuppressMessaging = suppressMessaging;
                SendPacket(unlearnedSpells);
            }
        }

        public void SetSpellFavorite(int spellId, bool favorite)
        {
            var spell = m_spells.LookupByKey(spellId);
            if (spell == null)
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
            ShapeShiftForm form = GetShapeshiftForm();
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
                        if (!HasAura(spellInfo.Id) && HasItemFitToSpellRequirements(spellInfo))
                            AddAura(spellInfo.Id, this);
                        return false;
                    }
                }
            }

            //Check CasterAuraStates
            return need_cast && (spellInfo.CasterAuraState == 0 || HasAuraState(spellInfo.CasterAuraState));
        }

        public void AddStoredAuraTeleportLocation(int spellId)
        {
            StoredAuraTeleportLocation storedLocation = new();
            storedLocation.Loc = new WorldLocation(this);
            storedLocation.CurrentState = StoredAuraTeleportLocation.State.Changed;

            m_storedAuraTeleportLocations[spellId] = storedLocation;
        }

        public void RemoveStoredAuraTeleportLocation(int spellId)
        {
            StoredAuraTeleportLocation storedLocation = m_storedAuraTeleportLocations.LookupByKey(spellId);
            if (storedLocation != null)
                storedLocation.CurrentState = StoredAuraTeleportLocation.State.Deleted;
        }

        public WorldLocation GetStoredAuraTeleportLocation(int spellId)
        {
            StoredAuraTeleportLocation auraLocation = m_storedAuraTeleportLocations.LookupByKey(spellId);
            if (auraLocation != null)
                return auraLocation.Loc;

            return null;
        }

        bool AddSpell(int spellId, bool active, bool learning, bool dependent, bool disabled, bool loading = false, SkillType fromSkill = 0, bool favorite = false, int? traitDefinitionId = null)
        {
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
            if (spellInfo == null)
            {
                // do character spell book cleanup (all characters)
                if (!IsInWorld && !learning)
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

            if (!Global.SpellMgr.IsSpellValid(spellInfo, this, false))
            {
                // do character spell book cleanup (all characters)
                if (!IsInWorld && !learning)
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

            PlayerSpell spell = m_spells.LookupByKey(spellId);
            if (spell != null && spell.State == PlayerSpellState.Temporary)
                RemoveTemporarySpell(spellId);

            if (spell != null)
            {
                var next_active_spell_id = 0;
                // fix activate state for non-stackable low rank (and find next spell for !active case)
                if (spellInfo.IsRanked())
                {
                    var next = Global.SpellMgr.GetNextSpellInChain(spellId);
                    if (next != 0)
                    {
                        if (HasSpell(next))
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
                    if (!IsInWorld && !learning)
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
                            RemoveOverrideSpell(traitDefinition.OverridesSpellID, spellId);
                    }

                    spell.TraitDefinitionId = traitDefinitionId;
                }

                spell.Favorite = favorite;

                // update active state for known spell
                if (spell.Active != active && spell.State != PlayerSpellState.Removed && !spell.Disabled)
                {
                    spell.Active = active;

                    if (!IsInWorld && !learning && !dependent_set) // explicitly load from DB and then exist in it already and set correctly
                        spell.State = PlayerSpellState.Unchanged;
                    else if (spell.State != PlayerSpellState.New)
                        spell.State = PlayerSpellState.Changed;

                    if (active)
                    {
                        if (spellInfo.IsPassive() && HandlePassiveSpellLearn(spellInfo))
                            CastSpell(this, spellId, true);
                    }
                    else if (IsInWorld)
                    {
                        if (next_active_spell_id != 0)
                            SendSupercededSpell(spellId, next_active_spell_id);
                        else
                        {
                            UnlearnedSpells removedSpells = new();
                            removedSpells.SpellID.Add(spellId);
                            SendPacket(removedSpells);
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
                            m_spells.Remove(spellId);
                            state = PlayerSpellState.Changed;
                            break;
                        }
                        default:
                        {
                            // can be in case spell loading but learned at some previous spell loading
                            if (!IsInWorld && !learning && !dependent_set)
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
                    if (!IsInWorld || disabled)                    // at spells loading, no output, but allow save
                        AddSpell(prev_spell, active, true, true, disabled, false, fromSkill);
                    else                                            // at normal learning
                        LearnSpell(prev_spell, true, fromSkill);
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
                    foreach (var _spell in m_spells)
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
                                    if (IsInWorld)                 // not send spell (re-/over-)learn packets at loading
                                        SendSupercededSpell(_spell.Key, spellId);

                                    // mark old spell as disable (SMSG_SUPERCEDED_SPELL replace it in client by new)
                                    _spell.Value.Active = false;
                                    if (_spell.Value.State != PlayerSpellState.New)
                                        _spell.Value.State = PlayerSpellState.Changed;
                                    superceded_old = true;          // new spell replace old in action bars and spell book.
                                }
                                else
                                {
                                    if (IsInWorld)                 // not send spell (re-/over-)learn packets at loading
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
                m_spells[spellId] = newspell;

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
                    TraitConfig traitConfig = GetTraitConfig(m_activePlayerData.ActiveCombatTraitConfigID);
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
                                        basePoints *= spellInfo.GetEffect(traitDefinitionEffectPoint.EffectIndex).CalcBaseValue(this, null, 0, -1);

                                    args.AddSpellMod(SpellValueMod.BasePoint0 + traitDefinitionEffectPoint.EffectIndex, (int)basePoints);
                                }
                            }
                        }
                    }
                }

                CastSpell(this, spellId, args);
                if (spellInfo.HasEffect(SpellEffectName.SkillStep))
                    return false;
            }

            if (traitDefinitionId.HasValue)
            {
                TraitDefinitionRecord traitDefinition = CliDB.TraitDefinitionStorage.LookupByKey(traitDefinitionId.Value);
                if (traitDefinition != null)
                    AddOverrideSpell(traitDefinition.OverridesSpellID, spellId);
            }

            // update free primary prof.points (if any, can be none in case GM .learn prof. learning)
            var freeProfs = GetFreePrimaryProfessionPoints();
            if (freeProfs != 0)
            {
                if (spellInfo.IsPrimaryProfessionFirstRank())
                    SetFreePrimaryProfessions(freeProfs - 1);
            }

            var skill_bounds = Global.SpellMgr.GetSkillLineAbilityMapBounds(spellId);

            SpellLearnSkillNode spellLearnSkill = Global.SpellMgr.GetSpellLearnSkill(spellId);
            if (spellLearnSkill != null)
            {
                // add dependent skills if this spell is not learned from adding skill already
                if (spellLearnSkill.skill != fromSkill)
                {
                    ushort skill_value = GetPureSkillValue(spellLearnSkill.skill);
                    ushort skill_max_value = GetPureMaxSkillValue(spellLearnSkill.skill);

                    if (skill_value < spellLearnSkill.value)
                        skill_value = spellLearnSkill.value;

                    ushort new_skill_max_value = spellLearnSkill.maxvalue;

                    if (new_skill_max_value == 0)
                    {
                        var rcInfo = Global.DB2Mgr.GetSkillRaceClassInfo(spellLearnSkill.skill, GetRace(), GetClass());
                        if (rcInfo != null)
                        {
                            switch (Global.SpellMgr.GetSkillRangeType(rcInfo))
                            {
                                case SkillRangeType.Language:
                                    skill_value = 300;
                                    new_skill_max_value = 300;
                                    break;
                                case SkillRangeType.Level:
                                    new_skill_max_value = GetMaxSkillValueForLevel();
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

                    SetSkill(spellLearnSkill.skill, spellLearnSkill.step, skill_value, skill_max_value);
                }
            }
            else
            {
                // not ranked skills
                foreach (var _spell_idx in skill_bounds)
                {
                    SkillLineRecord pSkill = CliDB.SkillLineStorage.LookupByKey((int)_spell_idx.SkillLine);
                    if (pSkill == null)
                        continue;

                    if (_spell_idx.SkillLine == fromSkill)
                        continue;

                    // Runeforging special case
                    if ((_spell_idx.AcquireMethod == AbilityLearnType.OnSkillLearn && !HasSkill(_spell_idx.SkillLine))
                        || ((_spell_idx.SkillLine == SkillType.Runeforging) && _spell_idx.TrivialSkillLineRankHigh == 0))
                    {
                        SkillRaceClassInfoRecord rcInfo = 
                            Global.DB2Mgr.GetSkillRaceClassInfo(_spell_idx.SkillLine, GetRace(), GetClass());

                        if (rcInfo != null)
                            LearnDefaultSkill(rcInfo);
                    }
                }
            }


            // learn dependent spells
            var spell_bounds = Global.SpellMgr.GetSpellLearnSpellMapBounds(spellId);
            foreach (var spellNode in spell_bounds)
            {
                if (!spellNode.AutoLearned)
                {
                    if (!IsInWorld || !spellNode.Active)       // at spells loading, no output, but allow save
                        AddSpell(spellNode.Spell, spellNode.Active, true, true, false);
                    else                                            // at normal learning
                        LearnSpell(spellNode.Spell, true);
                }

                if (spellNode.OverridesSpell != 0 && spellNode.Active)
                    AddOverrideSpell(spellNode.OverridesSpell, spellNode.Spell);
            }

            if (!GetSession().PlayerLoading())
            {
                // not ranked skills
                foreach (var _spell_idx in skill_bounds)
                {
                    UpdateCriteria(CriteriaType.LearnTradeskillSkillLine, (long)_spell_idx.SkillLine);
                    UpdateCriteria(CriteriaType.LearnSpellFromSkillLine, (long)_spell_idx.SkillLine);
                }

                UpdateCriteria(CriteriaType.LearnOrKnowSpell, spellId);
            }

            // needs to be when spell is already learned, to prevent infinite recursion crashes
            if (Global.DB2Mgr.GetMount(spellId) != null)
                GetSession().GetCollectionMgr().AddMount(spellId, MountStatusFlags.None, false, !IsInWorld);

            // return true (for send learn packet) only if spell active (in case ranked spells) and not replace old spell
            return active && !disabled && !superceded_old;
        }

        public override bool HasSpell(int spellId)
        {
            var spell = m_spells.LookupByKey(spellId);
            if (spell != null)
                return spell.State != PlayerSpellState.Removed && !spell.Disabled;

            return false;
        }

        public bool HasActiveSpell(int spellId)
        {
            var spell = m_spells.LookupByKey(spellId);
            if (spell != null)
            {
                return spell.State != PlayerSpellState.Removed && spell.Active
                    && !spell.Disabled;
            }

            return false;
        }

        public void AddSpellMod(SpellModifier mod, bool apply)
        {
            Log.outDebug(LogFilter.Spells, 
                $"Player.AddSpellMod: Player '{GetName()}' ({GetGUID()}), SpellID: {mod.spellId}.");

            // First, manipulate our spellmodifier container
            if (apply)
                m_spellMods[(int)mod.op][(int)mod.type].Add(mod);
            else
                m_spellMods[(int)mod.op][(int)mod.type].Remove(mod);

            // Now, send spellmodifier packet
            switch (mod.type)
            {
                case SpellModType.Flat:
                case SpellModType.Pct:
                    if (!IsLoading())
                    {
                        ServerOpcodes opcode = (mod.type == SpellModType.Flat ? ServerOpcodes.SetFlatSpellModifier : ServerOpcodes.SetPctSpellModifier);
                        SetSpellModifier packet = new(opcode);

                        // @todo Implement sending of bulk modifiers instead of single
                        SpellModifierInfo spellMod = new();

                        spellMod.ModIndex = (byte)mod.op;
                        for (int eff = 0; eff < 128; ++eff)
                        {
                            FlagArray128 mask = new();
                            mask[eff / 32] = 1u << (eff % 32);
                            if ((mod as SpellModifierByClassMask).mask & mask)
                            {
                                SpellModifierData modData = new();
                                modData.ModifierValue = 0.0f;

                                foreach (SpellModifierByClassMask spell in m_spellMods[(int)mod.op][(int)mod.type])
                                {
                                    if (spell.mask & mask)
                                        modData.ModifierValue += spell.value;
                                }

                                modData.ClassIndex = (byte)eff;

                                spellMod.ModifierData.Add(modData);
                            }
                        }
                        packet.Modifiers.Add(spellMod);

                        SendPacket(packet);
                    }
                    break;
                case SpellModType.LabelFlat:
                    if (apply)
                    {
                        AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SpellFlatModByLabel), (mod as SpellFlatModifierByLabel).value);
                    }
                    else
                    {
                        int firstIndex = m_activePlayerData.SpellFlatModByLabel.FindIndex((mod as SpellFlatModifierByLabel).value);
                        if (firstIndex >= 0)
                            RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SpellFlatModByLabel), firstIndex);
                    }
                    break;
                case SpellModType.LabelPct:
                    if (apply)
                    {
                        AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SpellPctModByLabel), (mod as SpellPctModifierByLabel).value);
                    }
                    else
                    {
                        int firstIndex = m_activePlayerData.SpellPctModByLabel.FindIndex((mod as SpellPctModifierByLabel).value);
                        if (firstIndex >= 0)
                            RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SpellPctModByLabel), firstIndex);
                    }
                    break;
            }
        }

        public void ApplySpellMod(SpellInfo spellInfo, SpellModOp op, ref int basevalue, Spell spell = null)
        {
            float totalmul = 1.0f;
            int totalflat = 0;

            GetSpellModValues(spellInfo, op, spell, basevalue, ref totalflat, ref totalmul);

            basevalue = (int)(((float)basevalue + totalflat) * totalmul);
        }

        public void ApplySpellMod(SpellInfo spellInfo, SpellModOp op, ref uint basevalue, Spell spell = null)
        {
            float totalmul = 1.0f;
            int totalflat = 0;

            GetSpellModValues(spellInfo, op, spell, basevalue, ref totalflat, ref totalmul);

            basevalue = (uint)(((float)basevalue + totalflat) * totalmul);
        }

        public void ApplySpellMod(SpellInfo spellInfo, SpellModOp op, ref float basevalue, Spell spell = null)
        {
            float totalmul = 1.0f;
            int totalflat = 0;

            GetSpellModValues(spellInfo, op, spell, basevalue, ref totalflat, ref totalmul);

            basevalue = (float)(basevalue + totalflat) * totalmul;
        }

        public void ApplySpellMod(SpellInfo spellInfo, SpellModOp op, ref double basevalue, Spell spell = null)
        {
            float totalmul = 1.0f;
            int totalflat = 0;

            GetSpellModValues(spellInfo, op, spell, basevalue, ref totalflat, ref totalmul);

            basevalue = (double)(basevalue + totalflat) * totalmul;
        }

        public void ApplySpellMod(SpellInfo spellInfo, SpellModOp op, ref Milliseconds basevalue, Spell spell = null)
        {
            float totalmul = 1.0f;
            int totalflat = 0;

            GetSpellModValues(spellInfo, op, spell, basevalue.Ticks, ref totalflat, ref totalmul);

            basevalue = (Milliseconds)((basevalue + totalflat) * (double)totalmul);
        }

        public void GetSpellModValues<T>(SpellInfo spellInfo, SpellModOp op, Spell spell, T baseValue, ref int flat, ref float pct) where T: unmanaged, IComparable
        {
            flat = 0;
            pct = 1.0f;

            // Drop charges for triggering spells instead of triggered ones
            if (m_spellModTakingSpell != null)
                spell = m_spellModTakingSpell;

            switch (op)
            {
                // special case, if a mod makes spell instant, only consume that mod
                case SpellModOp.ChangeCastTime:
                {
                    SpellModifier modInstantSpell = null;
                    foreach (SpellModifierByClassMask mod in m_spellMods[(int)op][(int)SpellModType.Pct])
                    {
                        if (!IsAffectedBySpellmod(spellInfo, mod, spell))
                            continue;

                        if (baseValue.CompareTo(10000) < 0 && mod.value <= -100)
                        {
                            modInstantSpell = mod;
                            break;
                        }
                    }

                    if (modInstantSpell == null)
                    {
                        foreach (SpellPctModifierByLabel mod in m_spellMods[(int)op][(int)SpellModType.LabelPct])
                        {
                            if (!IsAffectedBySpellmod(spellInfo, mod, spell))
                                continue;

                            if (baseValue.CompareTo(10000) < 0 && mod.value.ModifierValue <= -1.0f)
                            {
                                modInstantSpell = mod;
                                break;
                            }
                        }
                    }

                    if (modInstantSpell != null)
                    {
                        ApplyModToSpell(modInstantSpell, spell);
                        pct = 0.0f;
                        return;
                    }
                    break;
                }
                // special case if two mods apply 100% critical chance, only consume one
                case SpellModOp.CritChance:
                {
                    SpellModifier modCritical = null;
                    foreach (SpellModifierByClassMask mod in m_spellMods[(int)op][(int)SpellModType.Flat])
                    {
                        if (!IsAffectedBySpellmod(spellInfo, mod, spell))
                            continue;

                        if (mod.value >= 100)
                        {
                            modCritical = mod;
                            break;
                        }
                    }

                    if (modCritical == null)
                    {
                        foreach (SpellFlatModifierByLabel mod in m_spellMods[(int)op][(int)SpellModType.LabelFlat])
                        {
                            if (!IsAffectedBySpellmod(spellInfo, mod, spell))
                                continue;

                            if (mod.value.ModifierValue >= 100)
                            {
                                modCritical = mod;
                                break;
                            }
                        }
                    }

                    if (modCritical != null)
                    {
                        ApplyModToSpell(modCritical, spell);
                        flat = 100;
                        return;
                    }
                    break;
                }
                default:
                    break;
            }

            foreach (SpellModifierByClassMask mod in m_spellMods[(int)op][(int)SpellModType.Flat])
            {
                if (!IsAffectedBySpellmod(spellInfo, mod, spell))
                    continue;

                int value = mod.value;
                if (value == 0)
                    continue;

                flat += value;
                ApplyModToSpell(mod, spell);
            }

            foreach (SpellFlatModifierByLabel mod in m_spellMods[(int)op][(int)SpellModType.LabelFlat])
            {
                if (!IsAffectedBySpellmod(spellInfo, mod, spell))
                    continue;

                int value = mod.value.ModifierValue;
                if (value == 0)
                    continue;

                flat += value;
                ApplyModToSpell(mod, spell);
            }

            foreach (SpellModifierByClassMask mod in m_spellMods[(int)op][(int)SpellModType.Pct])
            {
                if (!IsAffectedBySpellmod(spellInfo, mod, spell))
                    continue;

                // skip percent mods for null basevalue (most important for spell mods with charges)
                if (baseValue + (dynamic)flat == 0)
                    continue;

                int value = mod.value;
                if (value == 0)
                    continue;

                // special case (skip > 10sec spell casts for instant cast setting)
                if (op == SpellModOp.ChangeCastTime)
                {
                    if (baseValue.CompareTo(10000) > 0 && value <= -100)
                        continue;
                }

                pct *= 1.0f + MathFunctions.CalculatePct(1.0f, value);
                ApplyModToSpell(mod, spell);
            }

            foreach (SpellPctModifierByLabel mod in m_spellMods[(int)op][(int)SpellModType.LabelPct])
            {
                if (!IsAffectedBySpellmod(spellInfo, mod, spell))
                    continue;

                // skip percent mods for null basevalue (most important for spell mods with charges)
                if (baseValue + (dynamic)flat == 0)
                    continue;

                float value = mod.value.ModifierValue;
                if (value == 1.0f)
                    continue;

                // special case (skip > 10sec spell casts for instant cast setting)
                if (op == SpellModOp.ChangeCastTime)
                {
                    if (baseValue.CompareTo(10000) > 0 && value <= -1.0f)
                        continue;
                }

                pct *= value;
                ApplyModToSpell(mod, spell);
            }
        }

        bool IsAffectedBySpellmod(SpellInfo spellInfo, SpellModifier mod, Spell spell)
        {
            if (mod == null || spellInfo == null)
                return false;

            // First time this aura applies a mod to us and is out of charges
            if (spell != null && mod.ownerAura.IsUsingCharges() 
                && mod.ownerAura.GetCharges() == 0 
                && !spell.m_appliedMods.Contains(mod.ownerAura))
                return false;

            switch (mod.op)
            {
                case SpellModOp.Duration: // +duration to infinite duration spells making them limited
                    if (spellInfo.GetDuration() == -1)
                        return false;
                    break;
                case SpellModOp.CritChance: // mod crit to spells that can't crit
                    if (!spellInfo.HasAttribute(SpellCustomAttributes.CanCrit))
                        return false;
                    break;
                case SpellModOp.PointsIndex0: // check if spell has any effect at that index
                case SpellModOp.Points:
                    if (spellInfo.GetEffects().Count <= 0)
                        return false;
                    break;
                case SpellModOp.PointsIndex1: // check if spell has any effect at that index
                    if (spellInfo.GetEffects().Count <= 1)
                        return false;
                    break;
                case SpellModOp.PointsIndex2: // check if spell has any effect at that index
                    if (spellInfo.GetEffects().Count <= 2)
                        return false;
                    break;
                case SpellModOp.PointsIndex3: // check if spell has any effect at that index
                    if (spellInfo.GetEffects().Count <= 3)
                        return false;
                    break;
                case SpellModOp.PointsIndex4: // check if spell has any effect at that index
                    if (spellInfo.GetEffects().Count <= 4)
                        return false;
                    break;
                default:
                    break;
            }

            return spellInfo.IsAffectedBySpellMod(mod);
        }

        public void SetSpellModTakingSpell(Spell spell, bool apply)
        {
            if (apply && m_spellModTakingSpell != null)
                return;

            if (!apply && (m_spellModTakingSpell == null || m_spellModTakingSpell != spell))
                return;

            m_spellModTakingSpell = apply ? spell : null;
        }

        void SendSpellModifiers()
        {
            SetSpellModifier flatMods = new(ServerOpcodes.SetFlatSpellModifier);
            SetSpellModifier pctMods = new(ServerOpcodes.SetPctSpellModifier);
            for (var i = 0; i < (int)SpellModOp.Max; ++i)
            {
                SpellModifierInfo flatMod = new();
                SpellModifierInfo pctMod = new();
                flatMod.ModIndex = pctMod.ModIndex = (byte)i;
                for (byte j = 0; j < 128; ++j)
                {
                    FlagArray128 mask = new();
                    mask[j / 32] = 1u << (j % 32);

                    SpellModifierData flatData;
                    SpellModifierData pctData;

                    flatData.ClassIndex = j;
                    flatData.ModifierValue = 0.0f;
                    pctData.ClassIndex = j;
                    pctData.ModifierValue = 1.0f;

                    foreach (SpellModifierByClassMask mod in m_spellMods[i][(int)SpellModType.Flat])
                    {
                        if (mod.mask & mask)
                            flatData.ModifierValue += mod.value;
                    }

                    foreach (SpellModifierByClassMask mod in m_spellMods[i][(int)SpellModType.Pct])
                    {
                        if (mod.mask & mask)
                            pctData.ModifierValue *= 1.0f + MathFunctions.CalculatePct(1.0f, mod.value);

                    }

                    flatMod.ModifierData.Add(flatData);
                    pctMod.ModifierData.Add(pctData);
                }

                flatMod.ModifierData.RemoveAll(mod => MathFunctions.fuzzyEq(mod.ModifierValue, 0.0f));

                pctMod.ModifierData.RemoveAll(mod => MathFunctions.fuzzyEq(mod.ModifierValue, 1.0f));

                flatMods.Modifiers.Add(flatMod);
                pctMods.Modifiers.Add(pctMod);
            }

            if (!flatMods.Modifiers.Empty())
                SendPacket(flatMods);

            if (!pctMods.Modifiers.Empty())
                SendPacket(pctMods);
        }

        void SendSupercededSpell(int oldSpell, int newSpell)
        {
            SupercededSpells supercededSpells = new();
            LearnedSpellInfo learnedSpellInfo = new();
            learnedSpellInfo.SpellID = newSpell;
            learnedSpellInfo.Superceded = oldSpell;
            supercededSpells.ClientLearnedSpellData.Add(learnedSpellInfo);
            SendPacket(supercededSpells);
        }

        public void UpdateEquipSpellsAtFormChange()
        {
            for (byte i = 0; i < InventorySlots.BagEnd; ++i)
            {
                if (m_items[i] != null && !m_items[i].IsBroken() 
                    && CanUseAttackType(GetAttackBySlot(i)))
                {
                    ApplyItemEquipSpell(m_items[i], false, true);     // remove spells that not fit to form
                    ApplyItemEquipSpell(m_items[i], true, true);      // add spells that fit form but not active
                }
            }

            UpdateItemSetAuras(true);
        }

        void UpdateItemSetAuras(bool formChange = false)
        {
            // item set bonuses not dependent from item broken state
            for (int setindex = 0; setindex < ItemSetEff.Count; ++setindex)
            {
                ItemSetEffect eff = ItemSetEff[setindex];
                if (eff == null)
                    continue;

                foreach (ItemSetSpellRecord itemSetSpell in eff.SetBonuses)
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(itemSetSpell.SpellID, Difficulty.None);

                    if (itemSetSpell.ChrSpecID != 0 && (ChrSpecialization)itemSetSpell.ChrSpecID != GetPrimarySpecialization())
                        ApplyEquipSpell(spellInfo, null, false, false);  // item set aura is not for current spec
                    else
                    {
                        ApplyEquipSpell(spellInfo, null, false, formChange); // remove spells that not fit to form - removal is skipped if shapeshift condition is satisfied
                        ApplyEquipSpell(spellInfo, null, true, formChange);  // add spells that fit form but not active
                    }
                }
            }
        }

        public int GetSpellPenetrationItemMod() { return m_spellPenetrationItemMod; }

        public void RemoveArenaSpellCooldowns(bool removeActivePetCooldowns)
        {
            // remove cooldowns on spells that have < 10 min CD
            Milliseconds cooldownLimit = (Minutes)10;

            GetSpellHistory().ResetCooldowns(p =>
            {
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(p.Key, Difficulty.None);
                return spellInfo.RecoveryTime < cooldownLimit &&
                spellInfo.CategoryRecoveryTime < cooldownLimit &&
                !spellInfo.HasAttribute(SpellAttr6.DoNotResetCooldownInArena);
            }, true);

            // pet cooldowns
            if (removeActivePetCooldowns)
            {
                Pet pet = GetPet();
                if (pet != null)
                    pet.GetSpellHistory().ResetAllCooldowns();
            }
        }

        /**********************************/
        /*************Runes****************/
        /**********************************/

        public void InitRunes()
        {
            if (GetClass() != Class.DeathKnight)
                return;

            Runes = new Runes(this);
        }

        public void UpdateAllRunesRegen()
        {
            if (GetClass() != Class.DeathKnight)
                return;

            /*        
            uint runeIndex = GetPowerIndex(PowerType.Runes);
            if (runeIndex == (int)PowerType.Max)
                return;

            PowerTypeRecord runeEntry = Global.DB2Mgr.GetPowerTypeEntry(PowerType.Runes);

            uint cooldown = GetRuneBaseCooldown();
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PowerRegenFlatModifier, (int)runeIndex), (float)(1 * Time.InMilliseconds) / cooldown - runeEntry.RegenPeace);
            */
        }

        public bool CanNoReagentCast(SpellInfo spellInfo)
        {
            // don't take reagents for spells with SPELL_ATTR5_NO_REAGENT_WHILE_PREP
            if (spellInfo.HasAttribute(SpellAttr5.NoReagentCostWithAura) &&
                HasUnitFlag(UnitFlags.Preparation))
                return true;

            // Check no reagent use mask
            FlagArray128 noReagentMask = new();
            noReagentMask[0] = m_activePlayerData.NoReagentCostMask[0];
            noReagentMask[1] = m_activePlayerData.NoReagentCostMask[1];
            noReagentMask[2] = m_activePlayerData.NoReagentCostMask[2];
            noReagentMask[3] = m_activePlayerData.NoReagentCostMask[3];
            if (spellInfo.SpellFamilyFlags & noReagentMask)
                return true;

            return false;
        }

        public void SetNoRegentCostMask(FlagArray128 mask)
        {
            for (byte i = 0; i < 4; ++i)
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.NoReagentCostMask, i), mask[i]);
        }

        public void CastItemCombatSpell(DamageInfo damageInfo)
        {
            Unit target = damageInfo.GetVictim();
            if (target == null || !target.IsAlive() || target == this)
                return;

            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
            {
                // If usable, try to cast item spell
                Item item = GetItemByPos(i);
                if (item != null)
                {
                    if (!item.IsBroken() && CanUseAttackType(damageInfo.GetAttackType()))
                    {
                        ItemTemplate proto = item.GetTemplate();
                        if (proto != null)
                        {
                            // Additional check for weapons
                            if (proto.GetClass() == ItemClass.Weapon)
                            {
                                // offhand item cannot proc from main hand hit etc
                                byte slot;
                                switch (damageInfo.GetAttackType())
                                {
                                    case WeaponAttackType.BaseAttack:
                                    case WeaponAttackType.RangedAttack:
                                        slot = EquipmentSlot.MainHand;
                                        break;
                                    case WeaponAttackType.OffAttack:
                                        slot = EquipmentSlot.OffHand;
                                        break;
                                    default:
                                        slot = EquipmentSlot.End;
                                        break;
                                }
                                if (slot != i)
                                    continue;
                                // Check if item is useable (forms or disarm)
                                if (damageInfo.GetAttackType() == WeaponAttackType.BaseAttack)
                                {
                                    if (!IsUseEquipedWeapon(true) && !IsInFeralForm())
                                        continue;
                                }
                            }

                            CastItemCombatSpell(damageInfo, item, proto);
                        }
                    }
                }
            }
        }

        public void CastItemCombatSpell(DamageInfo damageInfo, Item item, ItemTemplate proto)
        {
            // Can do effect if any damage done to target
            // for done procs allow normal + critical + absorbs by default
            bool canTrigger = damageInfo.GetHitMask().HasAnyFlag(ProcFlagsHit.Normal | ProcFlagsHit.Critical | ProcFlagsHit.Absorb);
            if (canTrigger)
            {
                if (!item.GetTemplate().HasFlag(ItemFlags.Legacy))
                {
                    foreach (ItemEffectRecord effectData in item.GetEffects())
                    {
                        // wrong triggering type
                        if (effectData.TriggerType != ItemSpelltriggerType.OnProc)
                            continue;

                        SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(effectData.SpellID, Difficulty.None);
                        if (spellInfo == null)
                        {
                            Log.outError(LogFilter.Player, $"WORLD: unknown Item spellid {effectData.SpellID}");
                            continue;
                        }

                        float chance = spellInfo.ProcChance;

                        if (proto.SpellPPMRate != 0)
                        {
                            Milliseconds WeaponSpeed = GetBaseAttackTime(damageInfo.GetAttackType());
                            chance = GetPPMProcChance(WeaponSpeed, proto.SpellPPMRate, spellInfo);
                        }
                        else if (chance > 100.0f)
                            chance = GetWeaponProcChance();

                        if (RandomHelper.randChance(chance) && Global.ScriptMgr.OnCastItemCombatSpell(this, damageInfo.GetVictim(), spellInfo, item))
                            CastSpell(damageInfo.GetVictim(), spellInfo.Id, item);
                    }
                }
            }

            // item combat enchantments
            for (byte e_slot = 0; e_slot < (byte)EnchantmentSlot.Max; ++e_slot)
            {
                int enchant_id = item.GetEnchantmentId((EnchantmentSlot)e_slot);
                SpellItemEnchantmentRecord pEnchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(enchant_id);
                if (pEnchant == null)
                    continue;

                for (byte s = 0; s < ItemConst.MaxItemEnchantmentEffects; ++s)
                {
                    if (pEnchant.Effect(s) != ItemEnchantmentType.CombatSpell)
                        continue;

                    SpellEnchantProcEntry entry = Global.SpellMgr.GetSpellEnchantProcEvent(enchant_id);

                    if (entry != null && entry.HitMask != 0)
                    {
                        // Check hit/crit/dodge/parry requirement
                        if ((entry.HitMask & (uint)damageInfo.GetHitMask()) == 0)
                            continue;
                    }
                    else
                    {
                        // for done procs allow normal + critical + absorbs by default
                        if (!canTrigger)
                            continue;
                    }

                    // check if enchant procs only on white hits
                    if (entry != null && entry.AttributesMask.HasAnyFlag(EnchantProcAttributes.WhiteHit) && damageInfo.GetSpellInfo() != null)
                        continue;

                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(pEnchant.EffectArg[s], Difficulty.None);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player.CastItemCombatSpell(GUID: {GetGUID()}, name: {GetName()}, enchant: {enchant_id}): " +
                            $"unknown spell {pEnchant.EffectArg[s]} is casted, ignoring...");
                        continue;
                    }

                    float chance = pEnchant.EffectPointsMin[s] != 0 ? pEnchant.EffectPointsMin[s] : GetWeaponProcChance();

                    if (entry != null)
                    {
                        if (entry.ProcsPerMinute != 0)
                            chance = GetPPMProcChance(proto.GetDelay(), entry.ProcsPerMinute, spellInfo);
                        else if (entry.Chance != 0)
                            chance = entry.Chance;
                    }

                    // Apply spell mods
                    ApplySpellMod(spellInfo, SpellModOp.ProcChance, ref chance);

                    // Shiv has 100% Chance to apply the poison
                    if (FindCurrentSpellBySpellId(5938) != null && e_slot == (byte)EnchantmentSlot.EnhancementTemporary)
                        chance = 100.0f;

                    if (RandomHelper.randChance(chance))
                    {
                        if (spellInfo.IsPositive())
                            CastSpell(this, spellInfo.Id, item);
                        else
                            CastSpell(damageInfo.GetVictim(), spellInfo.Id, item);
                    }

                    if (RandomHelper.randChance(chance))
                    {
                        Unit target = spellInfo.IsPositive() ? this : damageInfo.GetVictim();

                        CastSpellExtraArgs args = new(item);
                        // reduce effect values if enchant is limited
                        if (entry != null && entry.AttributesMask.HasAnyFlag(EnchantProcAttributes.Limit60) && target.GetLevelForTarget(this) > 60)
                        {
                            int lvlDifference = target.GetLevelForTarget(this) - 60;
                            int lvlPenaltyFactor = 4; // 4% lost effectiveness per level

                            int effectPct = Math.Max(0, 100 - (lvlDifference * lvlPenaltyFactor));

                            foreach (var spellEffectInfo in spellInfo.GetEffects())
                            {
                                if (spellEffectInfo.IsEffect())
                                    args.AddSpellMod(SpellValueMod.BasePoint0 + spellEffectInfo.EffectIndex, MathFunctions.CalculatePct(spellEffectInfo.CalcValue(this), effectPct));
                            }
                        }

                        CastSpell(target, spellInfo.Id, args);
                    }
                }
            }
        }

        float GetWeaponProcChance()
        {
            // normalized proc chance for weapon attack speed
            // (odd formula...)
            if (IsAttackReady(WeaponAttackType.BaseAttack))
                return (GetBaseAttackTime(WeaponAttackType.BaseAttack) * 1.8f / 1000.0f);
            else if (HaveOffhandWeapon() && IsAttackReady(WeaponAttackType.OffAttack))
                return (GetBaseAttackTime(WeaponAttackType.OffAttack) * 1.6f / 1000.0f);
            return 0;
        }

        public void ResetSpells(bool myClassOnly = false)
        {
            // not need after this call
            if (HasAtLoginFlag(AtLoginFlags.ResetSpells))
                RemoveAtLoginFlag(AtLoginFlags.ResetSpells, true);

            // make full copy of map (spells removed and marked as deleted at another spell remove
            // and we can't use original map for safe iterative with visit each spell at loop end
            var smap = GetSpellMap();

            uint family;

            if (myClassOnly)
            {
                ChrClassesRecord clsEntry = CliDB.ChrClassesStorage.LookupByKey((int)GetClass());
                if (clsEntry == null)
                    return;
                family = clsEntry.SpellClassSet;

                foreach (var spellId in smap.Keys)
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, Difficulty.None);
                    if (spellInfo == null)
                        continue;

                    // skip server-side/triggered spells
                    if (spellInfo.SpellLevel == 0)
                        continue;

                    // skip wrong class/race skills
                    if (!IsSpellFitByClassAndRace(spellInfo.Id))
                        continue;

                    // skip other spell families
                    if ((uint)spellInfo.SpellFamilyName != family)
                        continue;

                    // skip broken spells
                    if (!Global.SpellMgr.IsSpellValid(spellInfo, this, false))
                        continue;
                }
            }
            else
            {
                foreach (var spellId in smap.Keys)
                    RemoveSpell(spellId, false, false);           // only iter.first can be accessed, object by iter.second can be deleted already
            }

            LearnDefaultSkills();
            LearnCustomSpells();
            LearnQuestRewardedSpells();
        }

        public void SetPetSpellPower(int spellPower) { SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.PetSpellPower), spellPower); }

        public void SetSkillLineId(int pos, SkillType skillLineId)
        {
            SkillInfo skillInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Skill);
            SetUpdateFieldValue(ref skillInfo.ModifyValue(skillInfo.SkillLineID, (ushort)pos), (ushort)skillLineId);
        }

        public void SetSkillStep(int pos, int step)
        {
            SkillInfo skillInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Skill);
            SetUpdateFieldValue(ref skillInfo.ModifyValue(skillInfo.SkillStep, (ushort)pos), (ushort)step);
        }

        public void SetSkillRank(int pos, int rank)
        {
            SkillInfo skillInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Skill);
            SetUpdateFieldValue(ref skillInfo.ModifyValue(skillInfo.SkillRank, (ushort)pos), (ushort)rank);
        }

        public void SetSkillStartingRank(int pos, int starting)
        {
            SkillInfo skillInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Skill);
            SetUpdateFieldValue(ref skillInfo.ModifyValue(skillInfo.SkillStartingRank, (ushort)pos), (ushort)starting);
        }

        public void SetSkillMaxRank(int pos, int max)
        {
            SkillInfo skillInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Skill);
            SetUpdateFieldValue(ref skillInfo.ModifyValue(skillInfo.SkillMaxRank, (ushort)pos), (ushort)max);
        }

        public void SetSkillTempBonus(int pos, int bonus)
        {
            SkillInfo skillInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Skill);
            SetUpdateFieldValue(ref skillInfo.ModifyValue(skillInfo.SkillTempBonus, (short)pos), (short)bonus);
        }

        public void SetSkillPermBonus(int pos, int bonus)
        {
            SkillInfo skillInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Skill);
            SetUpdateFieldValue(ref skillInfo.ModifyValue(skillInfo.SkillPermBonus, (ushort)pos), (ushort)bonus);
        }

        public void RequestSpellCast(SpellCastRequest castRequest)
        {
            // We are overriding an already existing spell cast request so inform the client that the old cast is being replaced
            if (_pendingSpellCastRequest != null)
                CancelPendingCastRequest();

            _pendingSpellCastRequest = castRequest;

            // If we can process the cast request right now, do it.
            if (CanExecutePendingSpellCastRequest())
                ExecutePendingSpellCastRequest();
        }

        public void CancelPendingCastRequest()
        {
            if (_pendingSpellCastRequest == null)
                return;

            // We have to inform the client that the cast has been canceled. Otherwise the cast button will remain highlightened
            CastFailed castFailed = new();
            castFailed.CastID = _pendingSpellCastRequest.CastRequest.CastID;
            castFailed.SpellID = _pendingSpellCastRequest.CastRequest.SpellID;
            castFailed.Reason = SpellCastResult.DontReport;
            SendPacket(castFailed);

            _pendingSpellCastRequest = null;
        }

        // A spell can be queued up within 400 milliseconds before global cooldown expires or the cast finishes
        static readonly TimeSpan SPELL_QUEUE_TIME_WINDOW = (Milliseconds)400;

        public bool CanRequestSpellCast(SpellInfo spellInfo, Unit castingUnit)
        {
            if (castingUnit.GetSpellHistory().GetRemainingGlobalCooldown(spellInfo) > SPELL_QUEUE_TIME_WINDOW)
                return false;

            foreach (CurrentSpellTypes spellSlot in new[] { CurrentSpellTypes.Melee, CurrentSpellTypes.Generic })
            {
                Spell spell = GetCurrentSpell(spellSlot);
                if (spell != null && spell.GetRemainingCastTime() > SPELL_QUEUE_TIME_WINDOW)
                    return false;
            }

            return true;
        }

        void ExecutePendingSpellCastRequest()
        {
            if (_pendingSpellCastRequest == null)
                return;

            TriggerCastFlags triggerFlag = TriggerCastFlags.None;

            Unit castingUnit = _pendingSpellCastRequest.CastingUnitGUID == GetGUID() ? this : Global.ObjAccessor.GetUnit(this, _pendingSpellCastRequest.CastingUnitGUID);

            // client provided targets
            SpellCastTargets targets = new(castingUnit, _pendingSpellCastRequest.CastRequest);

            // The spell cast has been requested by using an item. Handle the cast accordingly.
            if (_pendingSpellCastRequest.ItemData != null)
            {
                if (ProcessItemCast(_pendingSpellCastRequest, targets))
                    _pendingSpellCastRequest = null;
                else
                    CancelPendingCastRequest();
                return;
            }

            // check known spell or raid marker spell (which not requires player to know it)
            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(_pendingSpellCastRequest.CastRequest.SpellID, GetMap().GetDifficultyID());
            Player plrCaster = castingUnit.ToPlayer();
            if (plrCaster != null && !plrCaster.HasActiveSpell(spellInfo.Id) && !spellInfo.HasAttribute(SpellAttr8.SkipIsKnownCheck))
            {
                bool allow = false;

                // allow casting of unknown spells for special lock cases
                GameObject go = targets.GetGOTarget();
                if (go != null && go.GetSpellForLock(plrCaster) == spellInfo)
                    allow = true;

                // allow casting of spells triggered by clientside periodic trigger auras
                if (castingUnit.HasAuraTypeWithTriggerSpell(AuraType.PeriodicTriggerSpellFromClient, spellInfo.Id))
                {
                    allow = true;
                    triggerFlag = TriggerCastFlags.FullMask;
                }

                if (!allow)
                {
                    CancelPendingCastRequest();
                    return;
                }
            }

            // Check possible spell cast overrides
            spellInfo = castingUnit.GetCastSpellInfo(spellInfo, triggerFlag);
            if (spellInfo.IsPassive())
            {
                CancelPendingCastRequest();
                return;
            }

            // can't use our own spells when we're in possession of another unit
            if (IsPossessing())
            {
                CancelPendingCastRequest();
                return;
            }

            // Client is resending autoshot cast opcode when other spell is cast during shoot rotation
            // Skip it to prevent "interrupt" message
            // Also check targets! target may have changed and we need to interrupt current spell
            if (spellInfo.IsAutoRepeatRangedSpell())
            {
                Spell currentSpell = castingUnit.GetCurrentSpell(CurrentSpellTypes.AutoRepeat);
                if (currentSpell != null)
                {
                    if (currentSpell.m_spellInfo == spellInfo && currentSpell.m_targets.GetUnitTargetGUID() == targets.GetUnitTargetGUID())
                    {
                        CancelPendingCastRequest();
                        return;
                    }
                }
            }

            // auto-selection buff level base at target level (in spellInfo)
            if (targets.GetUnitTarget() != null)
            {
                SpellInfo actualSpellInfo = spellInfo.GetAuraRankForLevel(targets.GetUnitTarget().GetLevelForTarget(this));

                // if rank not found then function return NULL but in explicit cast case original spell can be cast and later failed with appropriate error message
                if (actualSpellInfo != null)
                    spellInfo = actualSpellInfo;
            }

            Spell spell = new Spell(castingUnit, spellInfo, triggerFlag);

            SpellPrepare spellPrepare = new();
            spellPrepare.ClientCastID = _pendingSpellCastRequest.CastRequest.CastID;
            spellPrepare.ServerCastID = spell.m_castId;
            SendPacket(spellPrepare);

            spell.m_fromClient = true;
            spell.m_misc.Data0 = _pendingSpellCastRequest.CastRequest.Misc[0];
            spell.m_misc.Data1 = _pendingSpellCastRequest.CastRequest.Misc[1];
            spell.Prepare(targets);

            _pendingSpellCastRequest = null;
        }

        bool ProcessItemCast(SpellCastRequest castRequest, SpellCastTargets targets)
        {
            Item item = GetUseableItemByPos(new(castRequest.ItemData.Slot, castRequest.ItemData.PackSlot));
            if (item == null)
            {
                SendEquipError(InventoryResult.ItemNotFound);
                return false;
            }

            if (item.GetGUID() != castRequest.ItemData.CastItem)
            {
                SendEquipError(InventoryResult.ItemNotFound);
                return false;
            }

            ItemTemplate proto = item.GetTemplate();
            if (proto == null)
            {
                SendEquipError(InventoryResult.ItemNotFound, item);
                return false;
            }

            // some item classes can be used only in equipped state
            if (proto.GetInventoryType() != InventoryType.NonEquip && !item.IsEquipped())
            {
                SendEquipError(InventoryResult.ItemNotFound, item);
                return false;
            }

            InventoryResult msg = CanUseItem(item);
            if (msg != InventoryResult.Ok)
            {
                SendEquipError(msg, item, null);
                return false;
            }

            // only allow conjured consumable, bandage, poisons (all should have the 2^21 item flag set in DB)
            if (proto.GetClass() == ItemClass.Consumable && !proto.HasFlag(ItemFlags.IgnoreDefaultArenaRestrictions) && InArena())
            {
                SendEquipError(InventoryResult.NotDuringArenaMatch, item);
                return false;
            }

            // don't allow items banned in arena
            if (proto.HasFlag(ItemFlags.NotUseableInArena) && InArena())
            {
                SendEquipError(InventoryResult.NotDuringArenaMatch, item);
                return false;
            }

            if (IsInCombat())
            {
                foreach (var effect in item.GetEffects())
                {
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(effect.SpellID, GetMap().GetDifficultyID());
                    if (spellInfo != null)
                    {
                        if (!spellInfo.CanBeUsedInCombat(this))
                        {
                            SendEquipError(InventoryResult.NotInCombat, item);
                            return false;
                        }
                    }
                }
            }

            // check also  BIND_ON_ACQUIRE and BIND_QUEST for .additem or .additemset case by GM (not binded at adding to inventory)
            if (item.GetBonding() == ItemBondingType.OnUse || item.GetBonding() == ItemBondingType.OnAcquire || item.GetBonding() == ItemBondingType.Quest)
            {
                if (!item.IsSoulBound())
                {
                    item.SetState(ItemUpdateState.Changed, this);
                    item.SetBinding(true);
                    GetSession().GetCollectionMgr().AddItemAppearance(item);
                }
            }

            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.ItemUse);

            // Note: If script stop casting it must send appropriate data to client to prevent stuck item in gray state.
            if (!Global.ScriptMgr.OnItemUse(this, item, targets, castRequest.CastRequest.CastID))
            {
                // no script or script not process request by self
                CastItemUseSpell(item, targets, castRequest.CastRequest.CastID, castRequest.CastRequest.Misc);
            }

            return true;
        }

        bool CanExecutePendingSpellCastRequest()
        {
            if (_pendingSpellCastRequest == null)
                return false;

            Unit castingUnit = _pendingSpellCastRequest.CastingUnitGUID == GetGUID() ? this : Global.ObjAccessor.GetUnit(this, _pendingSpellCastRequest.CastingUnitGUID);
            if (castingUnit == null || !castingUnit.IsInWorld || (castingUnit != this && GetUnitBeingMoved() != castingUnit))
            {
                // If the casting unit is no longer available, just cancel the entire spell cast request and be done with it
                CancelPendingCastRequest();
                return false;
            }

            // Generic and melee spells have to wait, channeled spells can be processed immediately.
            if (castingUnit.GetCurrentSpell(CurrentSpellTypes.Channeled) == null && castingUnit.HasUnitState(UnitState.Casting))
                return false;

            // Waiting for the global cooldown to expire before attempting to execute the cast request
            if (castingUnit.GetSpellHistory().GetRemainingGlobalCooldown(Global.SpellMgr.GetSpellInfo(_pendingSpellCastRequest.CastRequest.SpellID, GetMap().GetDifficultyID())) > TimeSpan.Zero)
                return false;

            return true;
        }
    }

    public class PlayerSpell
    {
        public PlayerSpellState State;
        public bool Active;
        public bool Dependent;
        public bool Disabled;
        public bool Favorite;
        public int? TraitDefinitionId;
    }
}
