// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Scripting;
using Game;
using System;

namespace Scripts.World.Achievements
{
    [Script]
    class xp_boost_PlayerScript : PlayerScript
    {
        public xp_boost_PlayerScript() : base("xp_boost_PlayerScript") { }

        public override int OnGiveXP(Player player, int amount, Unit unit)
        {
            if (IsXPBoostActive())
                amount = (int)(amount * WorldConfig.Values[WorldCfg.RateXpBoost].Float);

            return amount;
        }

        bool IsXPBoostActive()
        {
            RealmTime localTm = LoopTime.RealmTime;
            uint weekdayMaskBoosted = WorldConfig.Values[WorldCfg.XpBoostDaymask].UInt32;
            uint weekdayMask = (1u << (int)localTm.DayOfWeek);
            bool currentDayBoosted = (weekdayMask & weekdayMaskBoosted) != 0;
            return currentDayBoosted;
        }
    }
}

