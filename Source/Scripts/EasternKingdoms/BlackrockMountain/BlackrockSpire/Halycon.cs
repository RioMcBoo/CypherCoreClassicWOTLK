// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.Halycon
{

    struct SpellIds
    {
        public const int Rend = 13738;
        public const int Thrash = 3391;
    }

    struct TextIds
    {
        public const int EmoteDeath = 0;
    }

    [Script]
    class boss_halycon : BossAI
    {
        static Position SummonLocation = new Position(-167.9561f, -411.7844f, 76.23057f, 1.53589f);

        bool Summoned;

        public boss_halycon(Creature creature) : base(creature, DataTypes.Halycon)
        {
            Initialize();
        }

        void Initialize()
        {
            Summoned = false;
        }

        public override void Reset()
        {
            _Reset();
            Initialize();
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);

            _scheduler.Schedule((Seconds)17, (Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Rend);
                task.Repeat((Seconds)8, (Seconds)10);
            });
            _scheduler.Schedule((Seconds)10, (Seconds)12, task =>
            {
                DoCast(me, SpellIds.Thrash);
            });
        }

        public override void JustDied(Unit killer)
        {
            me.SummonCreature(CreaturesIds.GizrulTheSlavener, SummonLocation, TempSummonType.TimedDespawn, (Minutes)5);
            Talk(TextIds.EmoteDeath);

            Summoned = true;
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }
}

