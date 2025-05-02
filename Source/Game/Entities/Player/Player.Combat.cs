// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Groups;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using Game.Maps;

namespace Game.Entities
{
    public partial class Player
    {
        void SetRegularAttackTime()
        {
            for (WeaponAttackType weaponAttackType = 0; weaponAttackType < WeaponAttackType.Max; ++weaponAttackType)
            {
                Item tmpitem = GetWeaponForAttack(weaponAttackType, true);
                if (tmpitem != null && !tmpitem.IsBroken())
                {
                    ItemTemplate proto = tmpitem.GetTemplate();
                    if (proto.GetDelay() != 0)
                        SetBaseAttackTime(weaponAttackType, proto.GetDelay());
                }
                else
                    SetBaseAttackTime(weaponAttackType, SharedConst.BaseAttackTime);  // If there is no weapon reset attack time to base (might have been changed from forms)
            }
        }

        public void RewardPlayerAndGroupAtEvent(int creature_id, WorldObject pRewardSource)
        {
            if (pRewardSource == null)
                return;
            ObjectGuid creature_guid = pRewardSource.IsTypeId(TypeId.Unit) ? pRewardSource.GetGUID() : ObjectGuid.Empty;

            // prepare data for near group iteration
            Group group = GetGroup();
            if (group != null)
            {
                for (GroupReference refe = group.GetFirstMember(); refe != null; refe = refe.Next())
                {
                    Player player = refe.GetSource();
                    if (player == null)
                        continue;

                    if (!player.IsAtGroupRewardDistance(pRewardSource))
                        continue;                               // member (alive or dead) or his corpse at req. distance

                    // quest objectives updated only for alive group member or dead but with not released body
                    if (player.IsAlive() || player.GetCorpse() == null)
                        player.KilledMonsterCredit(creature_id, creature_guid);
                }
            }
            else
                KilledMonsterCredit(creature_id, creature_guid);
        }

        public void AddWeaponProficiency(ItemSubClassWeaponMask newflag) { m_WeaponProficiency |= newflag; }
        public void AddArmorProficiency(ItemSubClassArmorMask newflag) { m_ArmorProficiency |= newflag; }
        public ItemSubClassWeaponMask GetWeaponProficiency() { return m_WeaponProficiency; }
        public ItemSubClassArmorMask GetArmorProficiency() { return m_ArmorProficiency; }
        public void SendProficiency(ItemClass itemClass, ItemSubClassMask itemSubclassMask)
        {
            SetProficiency packet = new();
            packet.ProficiencyMask = itemSubclassMask;
            packet.ProficiencyClass = itemClass;
            SendPacket(packet);
        }

        public bool CanTitanGrip() { return m_canTitanGrip; }

        float GetRatingMultiplier(CombatRating cr)
        {
            GtCombatRatingsRecord Rating = CliDB.CombatRatingsGameTable.GetRow(GetLevel());
            if (Rating == null)
                return 1.0f;

            float value = GetGameTableColumnForCombatRating(Rating, cr);
            if (value == 0)
                return 1.0f;                                        // By default use minimum coefficient (not must be called)

            return 1.0f / value;
        }

        public float GetRatingBonusValue(CombatRating cr)
        {
            return m_activePlayerData.CombatRatings[(int)cr] * GetRatingMultiplier(cr);
        }

        void GetDodgeFromAgility(ref float diminishing, ref float nondiminishing)
        {
            // Table for base dodge values
            float[] dodge_base =
            {
                0.036640f, // Warrior
                0.034943f, // Paladin
                -0.040873f, // Hunter
                0.020957f, // Rogue
                0.034178f, // Priest
                0.036640f, // DK
                0.021080f, // Shaman
                0.036587f, // Mage
                0.024211f, // Warlock
                0.0f,      // Monk
                0.056097f  // Druid
            };

            // Crit/agility to dodge/agility coefficient multipliers; 3.2.0 increased required agility by 15%
            float[] crit_to_dodge =
            {
                0.85f/1.15f,    // Warrior
                1.00f/1.15f,    // Paladin
                1.11f/1.15f,    // Hunter
                2.00f/1.15f,    // Rogue
                1.00f/1.15f,    // Priest
                0.85f/1.15f,    // DK
                1.60f/1.15f,    // Shaman
                1.00f/1.15f,    // Mage
                0.97f/1.15f,    // Warlock (?)
                0.0f,           // Monk
                2.00f/1.15f     // Druid
            };

            int level = GetLevel();
            Class pclass = GetClass();

            // Dodge per agility is proportional to crit per agility, which is available from DBC files
            GtChanceToMeleeCritRecord dodgeRatio = CliDB.ChanceToMeleeCritGameTable.GetRow(level);
            if (dodgeRatio == null || pclass > Class.Max)
                return;

            // @todo research if talents/effects that increase total agility by x% should increase non-diminishing part
            float base_agility = GetCreateStat(Stats.Agility);
            base_agility *= StatMods.GetOrDefault(UnitMods.StatStart + (int)Stats.Agility, UnitModType.BasePermanent).Mult;
            base_agility *= StatMods.GetOrDefault(UnitMods.StatStart + (int)Stats.Agility, UnitModType.BaseTemporary).Mult;

            float bonus_agility = GetStat(Stats.Agility) - base_agility;

            // calculate diminishing (green in char screen) and non-diminishing (white) contribution
            float classRatio = CliDB.GetGameTableColumnForClass(dodgeRatio, pclass);
            diminishing = 100.0f * bonus_agility * classRatio * crit_to_dodge[(int)pclass - 1];
            nondiminishing = 100.0f * (dodge_base[(int)pclass - 1] + base_agility * classRatio * crit_to_dodge[(int)pclass - 1]);            
        }       
        
        public float GetExpertiseDodgeOrParryReduction(WeaponAttackType attType)
        {
            switch (attType)
            {
                case WeaponAttackType.BaseAttack:
                    return m_activePlayerData.MainhandExpertise / 4.0f;
                case WeaponAttackType.OffAttack:
                    return m_activePlayerData.OffhandExpertise / 4.0f;
                default:
                    break;
            }
            return 0.0f;
        }

        public bool IsUseEquipedWeapon(bool mainhand)
        {
            // disarm applied only to mainhand weapon
            return !IsInFeralForm() && (!mainhand || !HasUnitFlag(UnitFlags.Disarmed));
        }

        public int GetTitanGripSpellId()
        {
            return m_titanGripPenaltySpellId;
        }

        public void SetCanTitanGrip(bool value, int penaltySpellId = 0)
        {
            if (value == m_canTitanGrip)
                return;

            m_canTitanGrip = value;
            m_titanGripPenaltySpellId = penaltySpellId;
        }

        void CheckTitanGripPenalty()
        {
            if (!CanTitanGrip())
                return;

            bool apply = IsUsingTwoHandedWeaponInOneHand();
            if (apply)
            {
                if (!HasAura(m_titanGripPenaltySpellId))
                    CastSpell(null, m_titanGripPenaltySpellId, true);
            }
            else
                RemoveAurasDueToSpell(m_titanGripPenaltySpellId);
        }

        bool IsTwoHandUsed()
        {
            Item mainItem = GetItemByPos(EquipmentSlot.MainHand);
            if (mainItem == null)
                return false;

            ItemTemplate itemTemplate = mainItem.GetTemplate();
            return (itemTemplate.GetInventoryType() == InventoryType.Weapon2Hand && !CanTitanGrip()) ||
                itemTemplate.GetInventoryType() == InventoryType.Ranged ||
                (itemTemplate.GetInventoryType() == InventoryType.RangedRight && itemTemplate.GetClass() == ItemClass.Weapon && itemTemplate.GetSubClass().Weapon != ItemSubClassWeapon.Wand);
        }

        bool IsUsingTwoHandedWeaponInOneHand()
        {
            Item offItem = GetItemByPos(EquipmentSlot.OffHand);
            if (offItem != null && offItem.GetTemplate().GetInventoryType() == InventoryType.Weapon2Hand)
                return true;

            Item mainItem = GetItemByPos(EquipmentSlot.MainHand);
            if (mainItem == null || mainItem.GetTemplate().GetInventoryType() == InventoryType.Weapon2Hand)
                return false;

            if (offItem == null)
                return false;

            return true;
        }

        public void _ApplyWeaponDamage(byte slot, Item item, bool apply)
        {
            ItemTemplate proto = item.GetTemplate();
            var attType = GetAttackBySlot(slot);
            if (!IsInFeralForm() && apply && !CanUseAttackType(attType))
                return;

            ScalingStatValuesRecord ssv = null;
            if (proto.GetScalingStatDistribution() is ScalingStatDistributionRecord ssd)
                ssv = proto.GetScalingStatValue(Math.Clamp(GetLevel(), ssd.MinLevel, ssd.MaxLevel));

            float damage = 0.0f;
            var itemLevel = item.GetItemLevel(this);

            //for (int i = 0; i < ItemConst.MaxDamages; ++i)
            {
                float minDamage = proto.GetMinDamage(0);
                float maxDamage = proto.GetMaxDamage(0);

                // If set dpsMod in ScalingStatValue use it for min(70 % from average), max(130 % from average) damage
                if (ssv != null)
                {
                    var extraDPS = ssv.getDPSMod(proto.GetScalingStatValueID());
                    if (extraDPS != 0)
                    {
                        float average = extraDPS * proto.GetDelay() / 1000.0f;
                        float mod = ssv.isTwoHand((uint)proto.GetScalingStatValueID()) ? 0.2f : 0.3f;

                        minDamage = (1.0f - mod) * average;
                        maxDamage = (1.0f + mod) * average;
                    }
                }
                
                if (minDamage > 0)
                {
                    damage = apply ? minDamage : SharedConst.BaseMinDamage;
                    SetBaseWeaponDamage(attType.Value, WeaponDamageRange.MinDamage, damage);
                }

                if (maxDamage > 0)
                {
                    damage = apply ? maxDamage : SharedConst.BaseMaxDamage;
                    SetBaseWeaponDamage(attType.Value, WeaponDamageRange.MaxDamage, damage);
                }
            }

            SpellShapeshiftFormRecord shapeshift = CliDB.SpellShapeshiftFormStorage.LookupByKey((int)GetShapeshiftForm());
            if (proto.GetDelay() != 0 && !(shapeshift != null && shapeshift.CombatRoundTime != 0))
                SetBaseAttackTime(attType.Value, apply ? proto.GetDelay() : SharedConst.BaseAttackTime);

            if (CanModifyStats() && (damage != 0 || proto.GetDelay() != 0))
                UpdateDamagePhysical(attType.Value);
        }

        public override void AtEnterCombat()
        {
            base.AtEnterCombat();
            if (GetCombatManager().HasPvPCombat())
                EnablePvpRules(true);
        }

        public override void AtExitCombat()
        {
            base.AtExitCombat();
            UpdatePotionCooldown();
            m_regenInterruptTimestamp = LoopTime.ServerTime;
        }

        public override float GetBlockPercent(int attackerLevel)
        {
            float blockArmor = (float)m_activePlayerData.ShieldBlock;
            float armorConstant = Global.DB2Mgr.EvaluateExpectedStat(ExpectedStatType.ArmorConstant, attackerLevel, Expansion.Unk, 0, Class.None);

            if ((blockArmor + armorConstant) == 0)
                return 0;

            return Math.Min(blockArmor / (blockArmor + armorConstant), 0.85f);
        }
        
        public void SetCanParry(bool value)
        {
            if (m_canParry == value)
                return;

            m_canParry = value;
            UpdateParryPercentage();
        }

        public void SetCanBlock(bool value)
        {
            if (m_canBlock == value)
                return;

            m_canBlock = value;
            UpdateBlockPercentage();
        }

        // duel health and mana reset methods
        public void SaveHealthBeforeDuel() { healthBeforeDuel = (int)GetHealth(); }
        public void SaveManaBeforeDuel() { manaBeforeDuel = GetPower(PowerType.Mana); }
        public void RestoreHealthAfterDuel() { SetHealth(healthBeforeDuel); }
        public void RestoreManaAfterDuel() { SetPower(PowerType.Mana, manaBeforeDuel); }

        void UpdateDuelFlag(ServerTime currTime)
        {
            if (duel != null && duel.State == DuelState.Countdown && duel.StartTime <= currTime)
            {
                Global.ScriptMgr.OnPlayerDuelStart(this, duel.Opponent);

                SetDuelTeam(1);
                duel.Opponent.SetDuelTeam(2);

                duel.State = DuelState.InProgress;
                duel.Opponent.duel.State = DuelState.InProgress;
            }
        }

        void CheckDuelDistance(ServerTime currTime)
        {
            if (duel == null)
                return;

            ObjectGuid duelFlagGUID = m_playerData.DuelArbiter;
            GameObject obj = GetMap().GetGameObject(duelFlagGUID);
            if (obj == null)
                return;

            if (duel.OutOfBoundsTime == ServerTime.Zero)
            {
                if (!IsWithinDistInMap(obj, 50))
                {
                    duel.OutOfBoundsTime = currTime + (Seconds)10;
                    SendPacket(new DuelOutOfBounds());
                }
            }
            else
            {
                if (IsWithinDistInMap(obj, 40))
                {
                    duel.OutOfBoundsTime = ServerTime.Zero;
                    SendPacket(new DuelInBounds());
                }
                else if (currTime >= duel.OutOfBoundsTime)
                    DuelComplete(DuelCompleteType.Fled);
            }
        }
        public void DuelComplete(DuelCompleteType type)
        {
            // duel not requested
            if (duel == null)
                return;

            // Check if DuelComplete() has been called already up in the stack
            // and in that case don't do anything else here
            if (duel.State == DuelState.Completed)
                return;

            Player opponent = duel.Opponent;
            duel.State = DuelState.Completed;
            opponent.duel.State = DuelState.Completed;

            Log.outDebug(LogFilter.Player, $"Duel Complete {GetName()} {opponent.GetName()}");

            DuelComplete duelCompleted = new();
            duelCompleted.Started = type != DuelCompleteType.Interrupted;
            SendPacket(duelCompleted);

            if (opponent.GetSession() != null)
                opponent.SendPacket(duelCompleted);

            if (type != DuelCompleteType.Interrupted)
            {
                DuelWinner duelWinner = new();
                duelWinner.BeatenName = (type == DuelCompleteType.Won ? opponent.GetName() : GetName());
                duelWinner.WinnerName = (type == DuelCompleteType.Won ? GetName() : opponent.GetName());
                duelWinner.BeatenVirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
                duelWinner.WinnerVirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
                duelWinner.Fled = type != DuelCompleteType.Won;

                SendMessageToSet(duelWinner, true);
            }

            opponent.DisablePvpRules();
            DisablePvpRules();

            Global.ScriptMgr.OnPlayerDuelEnd(opponent, this, type);

            switch (type)
            {
                case DuelCompleteType.Fled:
                    // if initiator and opponent are on the same team
                    // or initiator and opponent are not PvP enabled, forcibly stop attacking
                    if (GetTeam() == opponent.GetTeam())
                    {
                        AttackStop();
                        opponent.AttackStop();
                    }
                    else
                    {
                        if (!IsPvP())
                            AttackStop();
                        if (!opponent.IsPvP())
                            opponent.AttackStop();
                    }
                    break;
                case DuelCompleteType.Won:
                    UpdateCriteria(CriteriaType.LoseDuel, 1);
                    opponent.UpdateCriteria(CriteriaType.WinDuel, 1);

                    // Credit for quest Death's Challenge
                    if (GetClass() == Class.DeathKnight && opponent.GetQuestStatus(12733) == QuestStatus.Incomplete)
                        opponent.CastSpell(duel.Opponent, 52994, true);

                    // Honor points after duel (the winner) - ImpConfig
                    int amount = WorldConfig.Values[WorldCfg.HonorAfterDuel].Int32;
                    if (amount > 0)
                        opponent.RewardHonor(null, 1, amount);

                    break;
                default:
                    break;
            }

            // Victory emote spell
            if (type != DuelCompleteType.Interrupted)
                opponent.CastSpell(duel.Opponent, 52852, true);

            //Remove Duel Flag object
            GameObject obj = GetMap().GetGameObject(m_playerData.DuelArbiter);
            if (obj != null)
                duel.Initiator.RemoveGameObject(obj, true);

            //remove auras
            foreach (var pair in opponent.GetAppliedAurasCopy())
            {
                Aura aura = pair.Value.GetBase();
                if (!pair.Value.IsPositive() && aura.GetCasterGUID() == GetGUID() && aura.GetApplyTime() >= duel.StartTime)
                    opponent.RemoveAura(pair);
            }

            foreach (var pair in GetAppliedAurasCopy())
            {
                Aura aura = pair.Value.GetBase();
                if (!pair.Value.IsPositive() && aura.GetCasterGUID() == opponent.GetGUID() && aura.GetApplyTime() >= duel.StartTime)
                    RemoveAura(pair);
            }

            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.DuelEnd);
            opponent.RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.DuelEnd);

            // cleanup combo points
            SetPower(PowerType.ComboPoints, 0);
            opponent.SetPower(PowerType.ComboPoints, 0);

            //cleanups
            SetDuelArbiter(ObjectGuid.Empty);
            SetDuelTeam(0);
            opponent.SetDuelArbiter(ObjectGuid.Empty);
            opponent.SetDuelTeam(0);

            opponent.duel = null;
            duel = null;
        }

        public void SetDuelArbiter(ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.DuelArbiter), guid); }
        void SetDuelTeam(uint duelTeam) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.DuelTeam), duelTeam); }

        //PVP
        public void SetPvPDeath(bool on)
        {
            if (on)
                m_ExtraFlags |= PlayerExtraFlags.PVPDeath;
            else
                m_ExtraFlags &= ~PlayerExtraFlags.PVPDeath;
        }

        public void SetContestedPvPTimer(Milliseconds newTimer) { m_contestedPvPTimer = newTimer; }

        public void ResetContestedPvP()
        {
            ClearUnitState(UnitState.AttackPlayer);
            RemovePlayerFlag(PlayerFlags.ContestedPVP);
            m_contestedPvPTimer = default;
        }

        void UpdateAfkReport(ServerTime currTime)
        {
            if (m_bgData.bgAfkReportedTimer <= currTime)
            {
                m_bgData.bgAfkReportedCount = 0;
                m_bgData.bgAfkReportedTimer = currTime + (Minutes)5;
            }
        }

        public void SetContestedPvP(Player attackedPlayer = null)
        {
            if (attackedPlayer != null && (attackedPlayer == this || (duel != null && duel.Opponent == attackedPlayer)))
                return;

            SetContestedPvPTimer((Seconds)30);
            if (!HasUnitState(UnitState.AttackPlayer))
            {
                AddUnitState(UnitState.AttackPlayer);
                SetPlayerFlag(PlayerFlags.ContestedPVP);
                // call MoveInLineOfSight for nearby contested guards
                AIRelocationNotifier notifier = new(this);
                Cell.VisitWorldObjects(this, notifier, GetVisibilityRange());
            }
            foreach (Unit unit in m_Controlled)
            {
                if (!unit.HasUnitState(UnitState.AttackPlayer))
                {
                    unit.AddUnitState(UnitState.AttackPlayer);
                    AIRelocationNotifier notifier = new(unit);
                    Cell.VisitWorldObjects(this, notifier, GetVisibilityRange());
                }
            }
        }
        
        public void UpdateContestedPvP(TimeSpan diff)
        {
            if (m_contestedPvPTimer == 0 || IsInCombat())
                return;

            if (m_contestedPvPTimer <= diff)
                ResetContestedPvP();
            else
                m_contestedPvPTimer -= diff;
        }

        public void UpdatePvPFlag(ServerTime currTime)
        {
            if (!IsPvP())
                return;

            if (pvpInfo.EndTimer == ServerTime.Zero || currTime < pvpInfo.EndTimer + (Minutes)5 || pvpInfo.IsHostile)
                return;

            if (pvpInfo.EndTimer <= currTime)
            {
                pvpInfo.EndTimer = ServerTime.Zero;
                RemovePlayerFlag(PlayerFlags.PVPTimer);
            }
            
            UpdatePvP(false);
        }

        public void UpdatePvP(bool state, bool Override = false)
        {
            if (!state || Override)
            {
                SetPvP(state);
                pvpInfo.EndTimer = ServerTime.Zero;
            }
            else
            {
                pvpInfo.EndTimer = LoopTime.ServerTime;
                SetPvP(state);
            }
        }

        void InitPvP()
        {
            // pvp flag should stay after relog
            if (HasPlayerFlag(PlayerFlags.InPVP))
                UpdatePvP(true, true);
        }

        public void UpdatePvPState(bool onlyFFA = false)
        {
            // @todo should we always synchronize UNIT_FIELD_BYTES_2, 1 of controller and controlled?
            // no, we shouldn't, those are checked for affecting player by client
            if (!pvpInfo.IsInNoPvPArea && !IsGameMaster()
                && (pvpInfo.IsInFFAPvPArea || Global.WorldMgr.IsFFAPvPRealm() || HasAuraType(AuraType.SetFFAPvp)))
            {
                if (!IsFFAPvP())
                {
                    SetPvpFlag(UnitPVPStateFlags.FFAPvp);
                    foreach (var unit in m_Controlled)
                        unit.SetPvpFlag(UnitPVPStateFlags.FFAPvp);
                }
            }
            else if (IsFFAPvP())
            {
                RemovePvpFlag(UnitPVPStateFlags.FFAPvp);
                foreach (var unit in m_Controlled)
                    unit.RemovePvpFlag(UnitPVPStateFlags.FFAPvp);
            }

            if (onlyFFA)
                return;

            if (pvpInfo.IsHostile)                               // in hostile area
            {
                if (!IsPvP() || pvpInfo.EndTimer != ServerTime.Zero)
                    UpdatePvP(true, true);
            }
            else                                                    // in friendly area
            {
                if (IsPvP() && !HasPlayerFlag(PlayerFlags.InPVP) && pvpInfo.EndTimer == ServerTime.Zero)
                    pvpInfo.EndTimer = LoopTime.ServerTime;                  // start toggle-off
            }
        }

        public override void SetPvP(bool state)
        {
            base.SetPvP(state);
            foreach (var unit in m_Controlled)
                unit.SetPvP(state);
        }
    }
}
