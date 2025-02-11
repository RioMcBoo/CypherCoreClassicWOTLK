// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Game.Entities
{
    public readonly record struct RuneCooldown
    {
        public RuneCooldown(TimeSpan cooldown, float cooldownMultiplier)
        {
            var remainsPercentOfBase = cooldown / (RuneCooldowns.Base.Amount * cooldownMultiplier);
            var remainsCompressed = byte.MaxValue * remainsPercentOfBase;
            PassedCompressed = (byte)(byte.MaxValue - remainsCompressed); // cooldown time (0-255)
        }

        public readonly byte PassedCompressed;
    }

    public readonly record struct RuneCooldowns
    {
        public readonly TimeSpan Amount;
        private RuneCooldowns(TimeSpan cooldown) { Amount = cooldown; }

        public readonly static RuneCooldowns Zero = new(Time.SpanFromMilliseconds(0));
        public readonly static RuneCooldowns Base = new(Time.SpanFromMilliseconds(10000));
        public readonly static RuneCooldowns Miss = new(Time.SpanFromMilliseconds(1500));      // cooldown applied on runes when the spell misses
        public static implicit operator TimeSpan(RuneCooldowns runeCooldown) => runeCooldown.Amount;
    }

    public readonly struct RuneTemplate
    {
        public RuneTemplate (RuneIndex runeIndex, RuneStateMask runeMask, RuneType runeType, PowerType runePower)
        {
            Index = runeIndex;
            Mask = runeMask;
            Type = runeType;
            Power = runePower;
        }

        public readonly RuneIndex Index;
        public readonly RuneStateMask Mask;
        public readonly RuneType Type;
        public readonly PowerType Power;
    }

    public class Runes
    {
        public static readonly ImmutableDictionary<RuneIndex, RuneTemplate> RunesList = ImmutableDictionary.CreateRange(
        new KeyValuePair<RuneIndex, RuneTemplate>[]{
            KeyValuePair.Create(RuneIndex.Blood_0, new RuneTemplate(RuneIndex.Blood_0, RuneStateMask.Blood_0, RuneType.Blood, PowerType.RuneBlood)),
            KeyValuePair.Create(RuneIndex.Blood_1, new RuneTemplate(RuneIndex.Blood_1, RuneStateMask.Blood_1, RuneType.Blood, PowerType.RuneBlood)),
            KeyValuePair.Create(RuneIndex.Unholy_0, new RuneTemplate (RuneIndex.Unholy_0, RuneStateMask.Unholy_0, RuneType.Unholy, PowerType.RuneUnholy)),
            KeyValuePair.Create(RuneIndex.Unholy_1, new RuneTemplate (RuneIndex.Unholy_1, RuneStateMask.Unholy_1, RuneType.Unholy, PowerType.RuneUnholy)),
            KeyValuePair.Create(RuneIndex.Frost_0, new RuneTemplate(RuneIndex.Frost_0, RuneStateMask.Frost_0, RuneType.Frost, PowerType.RuneFrost)),
            KeyValuePair.Create(RuneIndex.Frost_1, new RuneTemplate(RuneIndex.Frost_1, RuneStateMask.Frost_1, RuneType.Frost, PowerType.RuneFrost)),
        });

        public static readonly ImmutableDictionary<PowerType, ImmutableList<RuneTemplate>> RunesByPowerList = ImmutableDictionary.CreateRange(
        new KeyValuePair<PowerType, ImmutableList<RuneTemplate>>[]{
            KeyValuePair.Create(PowerType.RuneBlood, (ImmutableList<RuneTemplate>)[RunesList[RuneIndex.Blood_0], RunesList[RuneIndex.Blood_1]]),
            KeyValuePair.Create(PowerType.RuneUnholy, (ImmutableList<RuneTemplate>)[RunesList[RuneIndex.Unholy_0], RunesList[RuneIndex.Unholy_1]]),
            KeyValuePair.Create(PowerType.RuneFrost, (ImmutableList<RuneTemplate>)[RunesList[RuneIndex.Frost_0], RunesList[RuneIndex.Frost_1]]),
        });

        public Runes(Player player)
        {
            Owner = player;
            m_availableRunes = RuneStateMask.All;
            m_runeRegenMultiplier = MultModifier.IdleModifier;
            m_cooldownMultiplier = MultModifier.IdleModifier;

            //           1      2      3      4      5      6
            m_runes = [new(), new(), new(), new(), new(), new()];

            UpdatePowerRegen(m_runeRegenMultiplier);
        }

        public void ApplyRegenSpeedPercentage(int percentage, bool apply, ServerTime currentTime)
        {
            MultModifier.ModifyPercentage(ref m_runeRegenMultiplier, percentage, apply);
            MultModifier.ModifyPercentage(ref m_cooldownMultiplier, percentage, !apply);

            UpdatePowerRegen(m_runeRegenMultiplier);

            foreach (var rune in RunesList.Values)
            {
                TimeSpan cooldown = GetRuneCooldown(rune, currentTime);
                SetRuneCooldown(rune.Mask, cooldown * m_cooldownMultiplier, currentTime);
            }
        }

        private void UpdatePowerRegen(MultModifier multiplier)
        {
            float deltaMultiplier = multiplier - MultModifier.IdleModifier;
            foreach (var power in RunesByPowerList.Keys)
            {
                PowerTypeRecord powerInfo = Global.DB2Mgr.GetPowerTypeEntry(power);

                float powerRegen = powerInfo.RegenPeace * deltaMultiplier;
                float powerRegenInterrupted = powerInfo.RegenCombat * deltaMultiplier;

                SetPowerRegen(powerRegen, powerRegenInterrupted);
            }
        }

        private void SetPowerRegen(float powerRegen, float powerRegenInterrupted)
        {
            foreach (var power in RunesByPowerList.Keys)
            {
                Owner.SetUpdateFieldValue(ref Owner.m_values.ModifyValue(Owner.m_unitData).ModifyValue(Owner.m_unitData.PowerRegenFlatModifier, Owner.GetPowerIndex(power)), powerRegen);
                Owner.SetUpdateFieldValue(ref Owner.m_values.ModifyValue(Owner.m_unitData).ModifyValue(Owner.m_unitData.PowerRegenInterruptedFlatModifier, Owner.GetPowerIndex(power)), powerRegenInterrupted);
            }
        }

        private static void SetRuneState(ref RuneStateMask stateMask, RuneTemplate rune, bool active)
        {
            SetRuneState(ref stateMask, rune.Mask, active);
        }

        private static void SetRuneState(ref RuneStateMask stateMask, RuneStateMask runeMask, bool active)
        {
            if (active)
            {
                stateMask |= runeMask;
            }
            else
            {
                stateMask &= ~runeMask;
            }
        }

        public static bool HasRune(RuneStateMask stateMask, RuneTemplate rune)
        {
            return HasRune(stateMask, rune.Mask);
        }

        public static bool HasRune(RuneStateMask stateMask, RuneStateMask runeMask)
        {
            return stateMask.HasFlag(runeMask);
        }

        private void SendConvertRune(RuneIndex runeIndex, RuneType runeType)
        {
            ConvertRune packet = new();
            packet.Index = runeIndex;
            packet.RuneType = runeType;

            Owner.SendPacket(packet);
        }

        public void ApplyConvertRuneAura(RuneTemplate rune, AuraEffect convertAura)
        {
            SetRuneState(ref m_deathRunes, rune.Mask, true);
            if (m_runes[(int)rune.Index].ConvertAuraList.Find(convertAura) == null)
            {
                m_runes[(int)rune.Index].ConvertAuraList.AddFirst(convertAura);
            }

            SendConvertRune(rune.Index, RuneType.Death);
        }

        public void RemoveConvertRuneAura(AuraEffect convertAura)
        {
            foreach (var rune in RunesList.Values)
            {
                m_runes[(int)rune.Index].ConvertAuraList.Remove(convertAura);
                if (m_runes[(int)rune.Index].ConvertAuraList.Count == 0)
                {
                    SetRuneState(ref m_deathRunes, rune.Mask, false);
                    SendConvertRune(rune.Index, rune.Type);
                }
            }
        }

        public void SendActivateRunes(RuneStateMask activatedRunes)
        {
            if (activatedRunes != RuneStateMask.None)
            {
                AddRunePower packet = new();
                packet.AddedRunesMask = activatedRunes;
                Owner.SendPacket(packet);
            }
        }

        public RuneType GetRuneType(RuneTemplate rune)
        {
            if (DeathRunes.HasFlag(rune.Mask))
                return RuneType.Death;

            return rune.Type;
        }

        public void SetRuneCooldown(RuneTemplate rune, RuneCooldowns cooldown, ServerTime currentTime)
        {
            SetRuneCooldown(rune.Mask, cooldown.Amount * m_cooldownMultiplier, currentTime);
        }

        public void SetRuneCooldown(RuneStateMask runesToSet, RuneCooldowns cooldown, ServerTime currentTime)
        {
            SetRuneCooldown(runesToSet, cooldown.Amount * m_cooldownMultiplier, currentTime);
        }

        private void SetRuneCooldown(RuneStateMask runesToSet, TimeSpan cooldown, ServerTime currentTime)
        {
            foreach (var rune in RunesList.Values)
            {
                if (runesToSet.HasFlag(rune.Mask))
                {
                    m_runes[(int)rune.Index].NextResetTime = currentTime + cooldown;
                }
            }

            if (cooldown == TimeSpan.Zero)
                SetRuneState(ref m_availableRunes, runesToSet, true);
            else
                SetRuneState(ref m_availableRunes, runesToSet, false);
        }

        public TimeSpan GetRuneCooldown(RuneTemplate rune, ServerTime currentTime)
        {
            var cooldown = m_runes[(int)rune.Index].NextResetTime - currentTime;

            if (cooldown < TimeSpan.Zero)
                cooldown = TimeSpan.Zero;

            return cooldown;
        }

        public SpellCastResult GetRunesForSpellCast(List<SpellPowerCost> costs, out RuneStateMask runesToUse)
        {
            runesToUse = RuneStateMask.None;
            RuneStateMask AvailableRunes = this.AvailableRunes;
            RuneStateMask BaseRunes = this.BaseRunes;
            RuneStateMask DeathRunes = this.DeathRunes;

            foreach (var cost in costs)
            {
                if (RunesByPowerList.TryGetValue(cost.Power, out var runesPair))
                {
                    int runeCost = cost.Amount;

                    if (runeCost < 0 || runeCost > 2)
                        return SpellCastResult.NoPower; // Always 2 rune per RuneType in WOTLK_CLASSIC

                    // Try to consume base runes
                    foreach (var rune in runesPair)
                    {
                        if (runeCost <= 0)
                            break;

                        if (HasRune(BaseRunes, rune))
                        {
                            SetRuneState(ref BaseRunes, rune.Mask, false);
                            runeCost -= 1;
                        }
                    }

                    // Try to consume death runes
                    if (runeCost > 0)
                    {
                        if (DeathRunes == RuneStateMask.None)
                            return SpellCastResult.NoPower;

                        // Try to consume sibling death runes                    
                        foreach (var rune in runesPair)
                        {
                            if (runeCost <= 0)
                                break;

                            if (HasRune(DeathRunes, rune))
                            {
                                SetRuneState(ref DeathRunes, rune, false);
                                runeCost -= 1;
                            }
                        }

                        // Try to consume any death runes
                        foreach (var rune in RunesList.Values)
                        {
                            if (DeathRunes == RuneStateMask.None || runeCost <= 0)
                                break;

                            if (HasRune(DeathRunes, rune))
                            {
                                SetRuneState(ref DeathRunes, rune, false);
                                runeCost -= 1;
                            }
                        }
                    }

                    if (runeCost > 0)
                        return SpellCastResult.NoPower;
                }
            }

            runesToUse = AvailableRunes;
            SetRuneState(ref runesToUse, BaseRunes | DeathRunes, false);
            return SpellCastResult.SpellCastOk;
        }

        public RuneData GetRuneData(ServerTime currentTime, RuneStateMask runesStateBefore = RuneStateMask.All)
        {
            RuneData data = new RuneData();
            data.RuneStateBefore = runesStateBefore;

            UpdateRunesState(currentTime, data);

            return data;
        }

        private RuneStateMask GetDeathRunesState()
        {
            return m_deathRunes;
        }

        private RuneStateMask GetRunesState()
        {
            UpdateRunesState(LoopTime.ServerTime);
            return m_availableRunes;
        }

        private void UpdateRunesState(ServerTime currentTime, RuneData runeData = null)
        {
            if (m_lastStateUpdateTime == currentTime && runeData == null)
                return;

            RuneStateMask runesStateMask = RuneStateMask.None;
            foreach (var rune in RunesList.Values)
            {
                TimeSpan cooldown = GetRuneCooldown(rune, currentTime);

                if (cooldown > TimeSpan.Zero)
                {
                    SetRuneState(ref runesStateMask, rune, false);
                }
                else
                {
                    SetRuneState(ref runesStateMask, rune, true);
                }

                if (runeData != null)
                {
                    runeData.Cooldowns.Add(new(cooldown, m_cooldownMultiplier));
                }
            }

            if (runeData != null)
            {
                runeData.RuneStateAfter = runesStateMask;
            }

            m_availableRunes = runesStateMask;
            m_lastStateUpdateTime = currentTime;
        }

        public RuneStateMask AvailableRunes => GetRunesState();
        public RuneStateMask DeathRunes => GetDeathRunesState();
        public RuneStateMask BaseRunes => AvailableRunes & ~DeathRunes;
        public readonly Player Owner;

        public struct RuneInfo
        {
            public ServerTime NextResetTime;
            public LinkedList<AuraEffect> ConvertAuraList = new();

            public RuneInfo() { }
        }

        private RuneStateMask m_availableRunes;
        private RuneStateMask m_deathRunes;
        private RuneInfo[] m_runes;
        private float m_runeRegenMultiplier;
        private float m_cooldownMultiplier;
        private ServerTime m_lastStateUpdateTime;
    }
}
