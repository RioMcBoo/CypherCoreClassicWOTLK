// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.HighlordOmokk
{
    struct SpellIds
    {
        public const int Frenzy = 8269;
        public const int KnockAway = 10101;
    }

    [Script]
    class boss_highlord_omokk : BossAI
    {
        public boss_highlord_omokk(Creature creature) : base(creature, DataTypes.HighlordOmokk) { }

        public override void Reset()
        {
            _Reset();
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);

            _scheduler.Schedule((Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Frenzy);
                task.Repeat((Minutes)1);
            });
            _scheduler.Schedule((Seconds)18, task =>
            {
                DoCastVictim(SpellIds.KnockAway);
                task.Repeat((Seconds)12);
            });
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

