// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.ShadowHunterVoshgajin
{
    struct SpellIds
    {
        public const int Curseofblood = 24673;
        public const int Hex = 16708;
        public const int Cleave = 20691;
    }

    [Script]
    class boss_shadow_hunter_voshgajin : BossAI
    {
        public boss_shadow_hunter_voshgajin(Creature creature) : base(creature, DataTypes.ShadowHunterVoshgajin) { }

        public override void Reset()
        {
            _Reset();
            //DoCast(me, SpellIcearmor, true);
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);

            _scheduler.Schedule((Seconds)2, task =>
            {
                DoCastVictim(SpellIds.Curseofblood);
                task.Repeat((Seconds)45);
            });
            _scheduler.Schedule((Seconds)8, task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                if (target != null)
                    DoCast(target, SpellIds.Hex);
                task.Repeat((Seconds)15);
            });
            _scheduler.Schedule((Seconds)14, task =>
            {
                DoCastVictim(SpellIds.Cleave);
                task.Repeat((Seconds)7);
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

