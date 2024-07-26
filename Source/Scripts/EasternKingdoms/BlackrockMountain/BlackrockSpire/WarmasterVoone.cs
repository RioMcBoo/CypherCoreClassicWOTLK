// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.WarmasterVoone
{
    struct SpellIds
    {
        public const int Snapkick = 15618;
        public const int Cleave = 15284;
        public const int Uppercut = 10966;
        public const int Mortalstrike = 16856;
        public const int Pummel = 15615;
        public const int Throwaxe = 16075;
    }

    [Script]
    class boss_warmaster_voone : BossAI
    {
        public boss_warmaster_voone(Creature creature) : base(creature, DataTypes.WarmasterVoone) { }

        public override void Reset()
        {
            _Reset();
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);

            _scheduler.Schedule((Seconds)8, task =>
            {
                DoCastVictim(SpellIds.Snapkick);
                task.Repeat((Seconds)6);
            });
            _scheduler.Schedule((Seconds)14, task =>
            {
                DoCastVictim(SpellIds.Cleave);
                task.Repeat((Seconds)12);
            });
            _scheduler.Schedule((Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Uppercut);
                task.Repeat((Seconds)14);
            });
            _scheduler.Schedule((Seconds)12, task =>
            {
                DoCastVictim(SpellIds.Mortalstrike);
                task.Repeat((Seconds)10);
            });
            _scheduler.Schedule((Seconds)32, task =>
            {
                DoCastVictim(SpellIds.Pummel);
                task.Repeat((Seconds)16);
            });
            _scheduler.Schedule((Seconds)1, task =>
            {
                DoCastVictim(SpellIds.Throwaxe);
                task.Repeat((Seconds)8);
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

