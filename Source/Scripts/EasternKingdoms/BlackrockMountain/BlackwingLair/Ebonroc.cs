// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackwingLair.Ebonroc
{
    struct SpellIds
    {
        public const int Shadowflame = 22539;
        public const int Wingbuffet = 23339;
        public const int Shadowofebonroc = 23340;
    }

    [Script]
    class boss_ebonroc : BossAI
    {
        public boss_ebonroc(Creature creature) : base(creature, DataTypes.Ebonroc) { }

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
                task.Repeat((Seconds)30);
            });
            _scheduler.Schedule((Seconds)8, (Seconds)10, task =>
            {
                DoCastVictim(SpellIds.Shadowofebonroc);
                task.Repeat((Seconds)8, (Seconds)10);
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

