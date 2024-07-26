// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.Drakkisath
{
    struct SpellIds
    {
        public const int Firenova = 23462;
        public const int Cleave = 20691;
        public const int Confliguration = 16805;
        public const int Thunderclap = 15548; //Not sure if right Id. 23931 would be a harder possibility.
    }

    [Script]
    class boss_drakkisath : BossAI
    {
        public boss_drakkisath(Creature creature) : base(creature, DataTypes.GeneralDrakkisath) { }

        public override void Reset()
        {
            _Reset();
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);
            _scheduler.Schedule((Seconds)6, task =>
            {
                DoCastVictim(SpellIds.Firenova);
                task.Repeat((Seconds)10);
            });
            _scheduler.Schedule((Seconds)8, task =>
            {
                DoCastVictim(SpellIds.Cleave);
                task.Repeat((Seconds)8);
            });
            _scheduler.Schedule((Seconds)15, task =>
            {
                DoCastVictim(SpellIds.Confliguration);
                task.Repeat((Seconds)18);
            });
            _scheduler.Schedule((Seconds)17, task =>
            {
                DoCastVictim(SpellIds.Thunderclap);
                task.Repeat((Seconds)20);
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
