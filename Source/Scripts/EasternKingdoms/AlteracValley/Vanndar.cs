// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.AlteracValley.Vanndar
{
    struct SpellIds
    {
        public const int Avatar = 19135;
        public const int Thunderclap = 15588;
        public const int Stormbolt = 20685; // not sure
    }

    struct TextIds
    {
        public const int YellAggro = 0;
        public const int YellEvade = 1;
        //public const int YellRespawn1                                 = -1810010; // Missing in database
        //public const int YellRespawn2                                 = -1810011; // Missing in database
        public const int YellRandom = 2;
        public const int YellSpell = 3;
    }

    [Script]
    class boss_vanndar : ScriptedAI
    {
        public boss_vanndar(Creature creature) : base(creature) { }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void JustEngagedWith(Unit who)
        {
            _scheduler.Schedule((Seconds)3, task =>
            {
                DoCastVictim(SpellIds.Avatar);
                task.Repeat((Seconds)15, (Seconds)20);
            });
            _scheduler.Schedule((Seconds)4, task =>
            {
                DoCastVictim(SpellIds.Thunderclap);
                task.Repeat((Seconds)5, (Seconds)15);
            });
            _scheduler.Schedule((Seconds)6, task =>
            {
                DoCastVictim(SpellIds.Stormbolt);
                task.Repeat((Seconds)10, (Seconds)25);
            });
            _scheduler.Schedule((Seconds)20, (Seconds)30, task =>
            {
                Talk(TextIds.YellRandom);
                task.Repeat((Seconds)20, (Seconds)30);
            });
            _scheduler.Schedule((Seconds)5, task =>
            {
                if (me.GetDistance2d(me.GetHomePosition().GetPositionX(), me.GetHomePosition().GetPositionY()) > 50)
                {
                    EnterEvadeMode();
                    Talk(TextIds.YellEvade);
                }
                task.Repeat();
            });

            Talk(TextIds.YellAggro);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }
}