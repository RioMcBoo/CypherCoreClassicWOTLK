// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;
using static Global;

namespace Scripts.World.DuelReset
{
    [Script]
    class DuelResetScript : PlayerScript
    {
        public DuelResetScript() : base("DuelResetScript") { }

        // Called when a duel starts (after Time.SpanFromSeconds(3) countdown)
        public override void OnDuelStart(Player player1, Player player2)
        {
            // Cooldowns reset
            if (WorldConfig.Values[WorldCfg.ResetDuelCooldowns].Bool)
            {
                player1.GetSpellHistory().SaveCooldownStateBeforeDuel();
                player2.GetSpellHistory().SaveCooldownStateBeforeDuel();

                ResetSpellCooldowns(player1, true);
                ResetSpellCooldowns(player2, true);
            }

            // Health and mana reset
            if (WorldConfig.Values[WorldCfg.ResetDuelHealthMana].Bool)
            {
                player1.SaveHealthBeforeDuel();
                player1.SaveManaBeforeDuel();
                player1.ResetAllPowers();

                player2.SaveHealthBeforeDuel();
                player2.SaveManaBeforeDuel();
                player2.ResetAllPowers();
            }
        }

        // Called when a duel ends
        public override void OnDuelEnd(Player winner, Player loser, DuelCompleteType type)
        {
            // do not reset anything if DuelInterrupted or DuelFled
            if (type == DuelCompleteType.Won)
            {
                // Cooldown restore
                if (WorldConfig.Values[WorldCfg.ResetDuelCooldowns].Bool)
                {
                    ResetSpellCooldowns(winner, false);
                    ResetSpellCooldowns(loser, false);

                    winner.GetSpellHistory().RestoreCooldownStateAfterDuel();
                    loser.GetSpellHistory().RestoreCooldownStateAfterDuel();
                }

                // Health and mana restore
                if (WorldConfig.Values[WorldCfg.ResetDuelHealthMana].Bool)
                {
                    winner.RestoreHealthAfterDuel();
                    loser.RestoreHealthAfterDuel();

                    // check if player1 class uses mana
                    if (winner.GetPowerType() == PowerType.Mana || winner.GetClass() == Class.Druid)
                        winner.RestoreManaAfterDuel();

                    // check if player2 class uses mana
                    if (loser.GetPowerType() == PowerType.Mana || loser.GetClass() == Class.Druid)
                        loser.RestoreManaAfterDuel();
                }
            }
        }

        static void ResetSpellCooldowns(Player player, bool onStartDuel)
        {
            // Remove cooldowns on spells that have < 10 min Cd > 30 sec and has no onHold
            player.GetSpellHistory().ResetCooldowns(pair =>
            {
                SpellInfo spellInfo = SpellMgr.GetSpellInfo(pair.Key, Difficulty.None);
                TimeSpan remainingCooldown = player.GetSpellHistory().GetRemainingCooldown(spellInfo);
                TimeSpan totalCooldown = spellInfo.RecoveryTime;
                TimeSpan categoryCooldown = spellInfo.CategoryRecoveryTime;

                var applySpellMod = (TimeSpan value) =>
                {
                    Milliseconds intValue = (Milliseconds)value;
                    player.ApplySpellMod(spellInfo, SpellModOp.Cooldown, ref intValue, null);
                    value = intValue;
                };

                applySpellMod(totalCooldown);

                Milliseconds cooldownMod = (Milliseconds)player.GetTotalAuraModifier(AuraType.ModCooldown);
                if (cooldownMod != 0)
                    totalCooldown += cooldownMod;

                if (spellInfo.HasAttribute(SpellAttr6.NoCategoryCooldownMods))
                    applySpellMod(categoryCooldown);

                return remainingCooldown > TimeSpan.Zero
                    && !pair.Value.OnHold
                    && totalCooldown < (Minutes)10
                    && categoryCooldown < (Minutes)10
                    && remainingCooldown < (Minutes)10
                    && (onStartDuel ? totalCooldown - remainingCooldown > (Seconds)30 : true)
                    && (onStartDuel ? categoryCooldown - remainingCooldown > (Seconds)30 : true);
            }, true);

            // pet cooldowns
            Pet pet = player.GetPet();
            if (pet != null)
                pet.GetSpellHistory().ResetAllCooldowns();
        }
    }
}