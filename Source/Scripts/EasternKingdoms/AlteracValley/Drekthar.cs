// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.AlteracValley.Drekthar
{
    struct SpellIds
    {
        public const int Whirlwind = 15589;
        public const int Whirlwind2 = 13736;
        public const int Knockdown = 19128;
        public const int Frenzy = 8269;
        public const int SweepingStrikes = 18765; // not sure
        public const int Cleave = 20677; // not sure
        public const int Windfury = 35886; // not sure
        public const int Stormpike = 51876;  // not sure
    }

    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SayEvade = 1;
        public const int SayRespawn = 2;
        public const int SayRandom = 3;
    }

    [Script]
    class boss_drekthar : ScriptedAI
    {
        public boss_drekthar(Creature creature) : base(creature) { }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayAggro);
            _scheduler.Schedule((Seconds)1, (Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Whirlwind);
                task.Repeat((Seconds)8, (Seconds)18);
            });
            _scheduler.Schedule((Seconds)1, (Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Whirlwind2);
                task.Repeat((Seconds)7, (Seconds)25);
            });
            _scheduler.Schedule((Seconds)12, task =>
            {
                DoCastVictim(SpellIds.Knockdown);
                task.Repeat((Seconds)10, (Seconds)15);
            });
            _scheduler.Schedule((Seconds)6, task =>
            {
                DoCastVictim(SpellIds.Frenzy);
                task.Repeat((Seconds)20, (Seconds)30);
            });
            _scheduler.Schedule((Seconds)20, (Seconds)30, task =>
            {
                Talk(TextIds.SayRandom);
                task.Repeat();
            });
        }

        public override void JustAppeared()
        {
            Reset();
            Talk(TextIds.SayRespawn);
        }

        public override bool CheckInRoom()
        {
            if (me.GetDistance2d(me.GetHomePosition().GetPositionX(), me.GetHomePosition().GetPositionY()) > 50)
            {
                EnterEvadeMode();
                Talk(TextIds.SayEvade);
                return false;
            }

            return true;
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim() || !CheckInRoom())
                return;

            _scheduler.Update(diff);
        }
    }
}