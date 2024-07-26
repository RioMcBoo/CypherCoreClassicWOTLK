// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockDepths.HighInterrogatorGerstahn
{
    struct SpellIds
    {
        public const int Shadowwordpain = 10894;
        public const int Manaburn = 10876;
        public const int Psychicscream = 8122;
        public const int Shadowshield = 22417;
    }

    [Script]
    class boss_high_interrogator_gerstahn : ScriptedAI
    {
        public boss_high_interrogator_gerstahn(Creature creature) : base(creature) { }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void JustEngagedWith(Unit who)
        {
            _scheduler.Schedule((Seconds)4, task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100.0f, true);
                if (target != null)
                    DoCast(target, SpellIds.Shadowwordpain);
                task.Repeat((Seconds)7);
            });
            _scheduler.Schedule((Seconds)14, task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100.0f, true);
                if (target != null)
                    DoCast(target, SpellIds.Manaburn);
                task.Repeat((Seconds)10);
            });
            _scheduler.Schedule((Seconds)32, task =>
            {
                DoCastVictim(SpellIds.Psychicscream);
                task.Repeat((Seconds)30);
            });
            _scheduler.Schedule((Seconds)8, task =>
            {
                DoCast(me, SpellIds.Shadowshield);
                task.Repeat((Seconds)25);
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

