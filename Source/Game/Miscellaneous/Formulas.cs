﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System;

namespace Game
{
    public class Formulas
    {
        public static float HKHonorAtLevelF(int level, float multiplier = 1.0f)
        {
            float honor = multiplier * level * 1.55f;
            Global.ScriptMgr.OnHonorCalculation(honor, level, multiplier);
            return honor;
        }

        public static int HKHonorAtLevel(int level, float multiplier = 1.0f)
        {
            return (int)Math.Ceiling(HKHonorAtLevelF(level, multiplier));
        }

        public static int GetGrayLevel(int pl_level)
        {
            int level;

            if (pl_level <= 5)
                level = 0;
            else if (pl_level <= 39)
                level = pl_level - 5 - pl_level / 10;
            else if (pl_level <= 59)
                level = pl_level - 1 - pl_level / 5;
            else
                level = pl_level - 9;

            Global.ScriptMgr.OnGrayLevelCalculation(level, pl_level);
            return level;
        }

        public static XPColorChar GetColorCode(int pl_level, int mob_level)
        {
            XPColorChar color;

            if (mob_level >= pl_level + 5)
                color = XPColorChar.Red;
            else if (mob_level >= pl_level + 3)
                color = XPColorChar.Orange;
            else if (mob_level >= pl_level - 2)
                color = XPColorChar.Yellow;
            else if (mob_level > GetGrayLevel(pl_level))
                color = XPColorChar.Green;
            else
                color = XPColorChar.Gray;

            Global.ScriptMgr.OnColorCodeCalculation(color, pl_level, mob_level);
            return color;
        }

        public static int GetZeroDifference(int pl_level)
        {
            int diff;

            if (pl_level < 4)
                diff = 5;
            else if (pl_level < 10)
                diff = 6;
            else if (pl_level < 12)
                diff = 7;
            else if (pl_level < 16)
                diff = 8;
            else if (pl_level < 20)
                diff = 9;
            else if (pl_level < 30)
                diff = 11;
            else if (pl_level < 40)
                diff = 12;
            else if (pl_level < 45)
                diff = 13;
            else if (pl_level < 50)
                diff = 14;
            else if (pl_level < 55)
                diff = 15;
            else if (pl_level < 60)
                diff = 16;
            else
                diff = 17;

            Global.ScriptMgr.OnZeroDifferenceCalculation(diff, pl_level);
            return diff;
        }

        public static int BaseGain(int pl_level, int mob_level, ContentLevels content)
        {
            int baseGain;
            int nBaseExp;

            switch (content)
            {
                case ContentLevels.Content_1_60:
                    nBaseExp = 45;
                    break;
                case ContentLevels.Content_61_70:
                    nBaseExp = 235;
                    break;
                case ContentLevels.Content_71_80:
                    nBaseExp = 580;
                    break;
                default:
                    Log.outError(LogFilter.Misc, $"BaseGain: Unsupported content level {content}");
                    nBaseExp = 45;
                    break;
            }

            if (mob_level >= pl_level)
            {
                var nLevelDiff = mob_level - pl_level;
                if (nLevelDiff > 4)
                    nLevelDiff = 4;

                baseGain = ((pl_level * 5 + nBaseExp) * (20 + nLevelDiff) / 10 + 1) / 2;
            }
            else
            {
                var gray_level = GetGrayLevel(pl_level);
                if (mob_level > gray_level)
                {
                    int ZD = GetZeroDifference(pl_level);
                    baseGain = (pl_level * 5 + nBaseExp) * (ZD + mob_level - pl_level) / ZD;
                }
                else
                    baseGain = 0;
            }

            if (WorldConfig.Values[WorldCfg.MinCreatureScaledXpRatio].Int32 != 0)
            {
                // Use mob level instead of player level to avoid overscaling on gain in a min is enforced
                var baseGainMin = (mob_level * 5 + nBaseExp) * WorldConfig.Values[WorldCfg.MinCreatureScaledXpRatio].Int32 / 100;
                baseGain = Math.Max(baseGainMin, baseGain);
            }

            Global.ScriptMgr.OnBaseGainCalculation(baseGain, pl_level, mob_level, content);
            return baseGain;
        }

        public static int XPGain(Player player, Unit u, bool isBattleGround = false)
        {
            Creature creature = u.ToCreature();
            int gain = 0;

            if (creature == null || creature.CanGiveExperience())
            {
                float xpMod = 1.0f;

                gain = BaseGain(player.GetLevel(), u.GetLevel(), Global.DB2Mgr.GetContentLevelsForMapAndZone(u.GetMapId(), u.GetZoneId()));

                if (gain != 0 && creature != null)
                {
                    // Players get only 10% xp for killing creatures of lower expansion levels than himself
                    if ((creature.GetCreatureDifficulty().GetHealthScalingExpansion() < GetExpansionForLevel(player.GetLevel())))
                        gain = (int)Math.Round(gain / 10.0);

                    if (creature.IsElite())
                    {
                        // Elites in instances have a 2.75x XP bonus instead of the regular 2x world bonus.
                        if (u.GetMap().IsDungeon())
                            xpMod *= 2.75f;
                        else
                            xpMod *= 2.0f;
                    }

                    xpMod *= creature.GetCreatureTemplate().ModExperience;
                }

                xpMod *= isBattleGround ? WorldConfig.Values[WorldCfg.RateXpBgKill].Float : WorldConfig.Values[WorldCfg.RateXpKill].Float;

                if (creature != null && creature.m_PlayerDamageReq != 0) // if players dealt less than 50% of the damage and were credited anyway (due to CREATURE_FLAG_EXTRA_NO_PLAYER_DAMAGE_REQ), scale XP gained appropriately (linear scaling)
                    xpMod *= 1.0f - 2.0f * creature.m_PlayerDamageReq / creature.GetMaxHealth();

                gain = (int)(gain * xpMod);
            }

            Global.ScriptMgr.OnGainCalculation(gain, player, u);
            return gain;
        }

        public static float XPInGroupRate(int count, bool isRaid)
        {
            float rate;

            if (isRaid)
            {
                // FIXME: Must apply decrease modifiers depending on raid size.
                // set to < 1 to, so client will display raid related strings
                rate = 0.99f;
            }
            else
            {
                switch (count)
                {
                    case 0:
                    case 1:
                    case 2:
                        rate = 1.0f;
                        break;
                    case 3:
                        rate = 1.166f;
                        break;
                    case 4:
                        rate = 1.3f;
                        break;
                    case 5:
                    default:
                        rate = 1.4f;
                        break;
                }
            }

            Global.ScriptMgr.OnGroupRateCalculation(rate, count, isRaid);
            return rate;
        }

        static Expansion GetExpansionForLevel(int level)
        {
            if (level < 60)
                return Expansion.Classic;
            else if (level < 70)
                return Expansion.BurningCrusade;
            else if (level < 80)
                return Expansion.WrathOfTheLichKing;            
            else
                return Expansion.Current;
        }

        public static uint ConquestRatingCalculator(uint rate)
        {
            if (rate <= 1500)
                return 1350; // Default conquest points
            else if (rate > 3000)
                rate = 3000;

            // http://www.arenajunkies.com/topic/179536-conquest-point-cap-vs-personal-rating-chart/page__st__60#entry3085246
            return (uint)(1.4326 * ((1511.26 / (1 + 1639.28 / Math.Exp(0.00412 * rate))) + 850.15));
        }

        public static uint BgConquestRatingCalculator(uint rate)
        {
            // WowWiki: Battlegroundratings receive a bonus of 22.2% to the cap they generate
            return (uint)((ConquestRatingCalculator(rate) * 1.222f) + 0.5f);
        }
    }
}
