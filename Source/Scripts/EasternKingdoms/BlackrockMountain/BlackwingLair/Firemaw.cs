// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackwingLair.Firemaw
{
    struct SpellIds
    {
        public const int Shadowflame = 22539;
        public const int Wingbuffet = 23339;
        public const int Flamebuffet = 23341;
    }

    [Script]
    class boss_firemaw : BossAI
    {
        public boss_firemaw(Creature creature) : base(creature, DataTypes.Firemaw) { }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);

            _scheduler.Schedule((Seconds)10, (Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Shadowflame);
                task.Repeat((Seconds)10, (Seconds)20);
            });
            _scheduler.Schedule((Seconds)30, task =>
            {
                DoCastVictim(SpellIds.Wingbuffet);
                if (GetThreat(me.GetVictim()) != 0)
                    ModifyThreatByPercent(me.GetVictim(), -75);
                task.Repeat((Seconds)30);
            });
            _scheduler.Schedule((Seconds)5, task =>
            {
                DoCastVictim(SpellIds.Flamebuffet);
                task.Repeat((Seconds)5);
            });
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }
}

