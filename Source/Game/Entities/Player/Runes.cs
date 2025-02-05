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

        private static readonly ImmutableDictionary<PowerType, ImmutableList<RuneTemplate>> RunesByPowerList = ImmutableDictionary.CreateRange(
        new KeyValuePair<PowerType, ImmutableList<RuneTemplate>>[]{
            KeyValuePair.Create(PowerType.RuneBlood, (ImmutableList<RuneTemplate>)[RunesList[RuneIndex.Blood_0], RunesList[RuneIndex.Blood_1]]),
            KeyValuePair.Create(PowerType.RuneUnholy, (ImmutableList<RuneTemplate>)[RunesList[RuneIndex.Unholy_0], RunesList[RuneIndex.Unholy_1]]),
            KeyValuePair.Create(PowerType.RuneFrost, (ImmutableList<RuneTemplate>)[RunesList[RuneIndex.Frost_0], RunesList[RuneIndex.Frost_1]]),
        });

        public Runes(Player player)
        {
            Owner = player;
            AvailableRunes = RuneStateMask.All;
            RuneRegenMultiplier = MultModifier.IdleModifier;
            CooldownMultiplier = MultModifier.IdleModifier;

            UpdatePowerRegen(RuneRegenMultiplier);
        }

        public void ApplyRegenSpeedPercentage(int percentage, bool apply, ServerTime currentTime = default)
        {
            currentTime = currentTime == default ? LoopTime.ServerTime : currentTime;

            MultModifier.ModifyPercentage(ref RuneRegenMultiplier, percentage, apply);
            MultModifier.ModifyPercentage(ref CooldownMultiplier, percentage, !apply);

            UpdatePowerRegen(RuneRegenMultiplier);

            foreach (var rune in RunesList.Values)
            {
                TimeSpan cooldown = GetRuneCooldown(rune, currentTime);
                _setRuneCooldown(rune.Mask, cooldown * CooldownMultiplier, currentTime);
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

        private bool IsStateChanged(RuneTemplate rune)
        {
            return rune.Mask.HasAnyFlag(PreviousState ^ AvailableRunes);
        }

        private void SetRuneState(RuneTemplate rune, bool set = true)
        {
            SetRuneState(rune.Mask, set);
        }

        private void SetRuneState(RuneStateMask mask, bool set = true)
        {
            if (set)
            {
                AvailableRunes |= mask;                      // usable
            }
            else
            {
                AvailableRunes &= ~mask;                     // on cooldown
            }
        }

        private bool IsRuneReady(RuneTemplate rune)
        {
            return AvailableRunes.HasFlag(rune.Mask);
        }

        public void SetDeathRune(RuneTemplate rune, bool set = true)
        {
            if (set)
            {
                DeathRunes |= rune.Mask;
            }
            else
            {
                DeathRunes &= ~rune.Mask;
            }
        }

        public RuneType GetRuneType(RuneTemplate rune)
        {
            if (DeathRunes.HasFlag(rune.Mask))
                return RuneType.Death;

            return rune.Type;
        }

        public void SetRuneCooldown(RuneTemplate rune, RuneCooldowns cooldown, ServerTime currentTime = default)
        {
            _setRuneCooldown(rune.Mask, cooldown.Amount * CooldownMultiplier, currentTime);
        }

        public void SetRuneCooldown(RuneStateMask runesToSet, RuneCooldowns cooldown, ServerTime currentTime = default)
        {
            _setRuneCooldown(runesToSet, cooldown.Amount * CooldownMultiplier, currentTime);
        }

        private void _setRuneCooldown(RuneStateMask runesToSet, TimeSpan cooldown, ServerTime currentTime = default)
        {
            currentTime = currentTime == default ? LoopTime.ServerTime : currentTime;

            foreach (var rune in RunesList.Values)
            {
                if (rune.Mask.HasAnyFlag(runesToSet))
                {
                    NextResetTime[(int)rune.Index] = currentTime + cooldown;
                }
            }

            if (!NeedsToBeSynchronized)
            {
                PreviousState = AvailableRunes;
                NeedsToBeSynchronized = true;
            }

            SetRuneState(runesToSet, cooldown == TimeSpan.Zero);
        }

        public TimeSpan GetRuneCooldown(RuneTemplate rune, ServerTime currentTime = default)
        {
            currentTime = currentTime == default ? LoopTime.ServerTime : currentTime;
            var cooldown = NextResetTime[(int)rune.Index] - currentTime;

            if (cooldown < TimeSpan.Zero)
                cooldown = TimeSpan.Zero;

            return cooldown;
        }

        public SpellCastResult GetRunesForSpellCast(List<SpellPowerCost> costs, out RuneStateMask runesToUse)
        {
            runesToUse = RuneStateMask.None;
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

                        if (BaseRunes.HasAnyFlag(rune.Mask))
                        {
                            BaseRunes &= ~(rune.Mask & BaseRunes);
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

                            if (DeathRunes.HasAnyFlag(rune.Mask))
                            {
                                DeathRunes &= ~(rune.Mask & DeathRunes);
                                runeCost -= 1;
                            }
                        }

                        // Try to consume any death runes
                        foreach (var rune in RunesList.Values)
                        {
                            if (DeathRunes == RuneStateMask.None || runeCost <= 0)
                                break;

                            if (DeathRunes.HasAnyFlag(rune.Mask))
                            {
                                DeathRunes &= ~(rune.Mask & DeathRunes);
                                runeCost -= 1;
                            }
                        }
                    }

                    if (runeCost > 0)
                        return SpellCastResult.NoPower;
                }
            }

            runesToUse = AvailableRunes & ~(BaseRunes | DeathRunes);
            return SpellCastResult.SpellCastOk;
        }

        /// <summary> Always changes runes state. Use only as directed. </summary>
        public RuneData Resync(ServerTime currentTime = default)
        {
            currentTime = currentTime == default ? LoopTime.ServerTime : currentTime;

            RuneData data = new RuneData();

            foreach (var rune in RunesList.Values)
            {
                TimeSpan cooldown = GetRuneCooldown(rune, currentTime);
                data.Cooldowns.Add(new(cooldown, CooldownMultiplier));
            }

            data.RuneStateBefore = PreviousState;
            data.RuneStateAfter = AvailableRunes;

            PreviousState = AvailableRunes;
            NeedsToBeSynchronized = false;

            return data;
        }

        public void Regenerate(ServerTime currentTime = default)
        {
            currentTime = currentTime == default ? LoopTime.ServerTime : currentTime;

            ResyncRunes data = new ResyncRunes();
            bool HasRecoveredRune = false;

            foreach (var rune in RunesList.Values)
            {
                TimeSpan cooldown = TimeSpan.Zero;

                if (!IsRuneReady(rune))
                {
                    cooldown = GetRuneCooldown(rune, currentTime);

                    if (cooldown == TimeSpan.Zero)
                    {
                        SetRuneState(rune, true);
                        HasRecoveredRune = true;
                        data.Runes.Cooldowns.Add(new(cooldown, CooldownMultiplier));
                    }
                }
            }

            data.Runes.RuneStateBefore = PreviousState;
            data.Runes.RuneStateAfter = AvailableRunes;

            if (!HasRecoveredRune)
                return;

            PreviousState = AvailableRunes;
            NeedsToBeSynchronized = false;

            Owner.SendPacket(data);
        }

        public RuneStateMask AvailableRunes { get; private set; }
        public RuneStateMask DeathRunes { get; private set; }
        public RuneStateMask BaseRunes => AvailableRunes & ~DeathRunes;
        public readonly Player Owner;

        private RuneStateMask PreviousState { get; set; }
        private ServerTime[] NextResetTime = new ServerTime[PlayerConst.MaxRunes];
        private bool NeedsToBeSynchronized;
        private float RuneRegenMultiplier;
        private float CooldownMultiplier;
    }
}
