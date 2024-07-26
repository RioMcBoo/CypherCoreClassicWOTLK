// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackwingLair.Flamegor
{
    struct SpellIds
    {
        public const int Shadowflame = 22539;
        public const int Wingbuffet = 23339;
        public const int Frenzy = 23342;  //This spell periodically triggers fire nova
    }

    struct TextIds
    {
        public const int EmoteFrenzy = 0;
    }

    [Script]
    class boss_flamegor : BossAI
    {
        public boss_flamegor(Creature creature) : base(creature, DataTypes.Flamegor) { }

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
            _scheduler.Schedule((Seconds)10, task =>
            {
                Talk(TextIds.EmoteFrenzy);
                DoCast(me, SpellIds.Frenzy);
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

