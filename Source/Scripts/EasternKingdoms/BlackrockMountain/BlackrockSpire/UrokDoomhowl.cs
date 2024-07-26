// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.UrokDoomhowl
{
    struct SpellIds
    {
        public const int Rend = 16509;
        public const int Strike = 15580;
        public const int IntimidatingRoar = 16508;
    }

    struct TextIds
    {
        public const int SaySummon = 0;
        public const int SayAggro = 1;
    }

    [Script]
    class boss_urok_doomhowl : BossAI
    {
        public boss_urok_doomhowl(Creature creature) : base(creature, DataTypes.UrokDoomhowl) { }

        public override void Reset()
        {
            _Reset();
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);

            _scheduler.Schedule((Seconds)17, (Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Rend);
                task.Repeat((Seconds)8, (Seconds)10);
            });
            _scheduler.Schedule((Seconds)10, (Seconds)12, task =>
            {
                DoCastVictim(SpellIds.Strike);
                task.Repeat((Seconds)8, (Seconds)10);
            });

            Talk(TextIds.SayAggro);
        }

        public override void JustDied(Unit killer)
        {
            _JustDied();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }
}
