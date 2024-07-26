// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.AlteracValley.Galvangar
{
    struct SpellIds
    {
        public const int Cleave = 15284;
        public const int FrighteningShout = 19134;
        public const int Whirlwind1 = 15589;
        public const int Whirlwind2 = 13736;
        public const int MortalStrike = 16856;
    }

    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SayEvade = 1;
        public const int SayBuff = 2;
    }

    struct ActionIds
    {
        public const int BuffYell = -30001; // shared from Battleground
    }

    [Script]
    class boss_galvangar : ScriptedAI
    {
        public boss_galvangar(Creature creature) : base(creature) { }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayAggro);
            _scheduler.Schedule((Seconds)1, (Seconds)9, task =>
            {
                DoCastVictim(SpellIds.Cleave);
                task.Repeat((Seconds)10, (Seconds)16);
            });
            _scheduler.Schedule((Seconds)2, (Seconds)19, task =>
            {
                DoCastVictim(SpellIds.FrighteningShout);
                task.Repeat((Seconds)10, (Seconds)15);
            });
            _scheduler.Schedule((Seconds)1, (Seconds)13, task =>
            {
                DoCastVictim(SpellIds.Whirlwind1);
                task.Repeat((Seconds)6, (Seconds)10);
            });
            _scheduler.Schedule((Seconds)5, (Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Whirlwind2);
                task.Repeat((Seconds)10, (Seconds)25);
            });
            _scheduler.Schedule((Seconds)5, (Seconds)20, task =>
            {
                DoCastVictim(SpellIds.MortalStrike);
                task.Repeat((Seconds)10, (Seconds)30);
            });
        }

        public override void DoAction(int actionId)
        {
            if (actionId == ActionIds.BuffYell)
                Talk(TextIds.SayBuff);
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