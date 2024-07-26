// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.GizrulTheSlavener
{
    struct SpellIds
    {
        public const int FatalBite = 16495;
        public const int InfectedBite = 16128;
        public const int Frenzy = 8269;
    }

    struct PathIds
    {
        public const int Gizrul = 3219600;
    }

    [Script]
    class boss_gizrul_the_slavener : BossAI
    {
        public boss_gizrul_the_slavener(Creature creature) : base(creature, DataTypes.GizrulTheSlavener) { }

        public override void Reset()
        {
            _Reset();
        }

        public override void IsSummonedBy(WorldObject summoner)
        {
            me.GetMotionMaster().MovePath(PathIds.Gizrul, false);
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);

            _scheduler.Schedule((Seconds)17, (Seconds)20, task =>
            {
                DoCastVictim(SpellIds.FatalBite);
                task.Repeat((Seconds)8, (Seconds)10);
            });
            _scheduler.Schedule((Seconds)10, (Seconds)12, task =>
            {
                DoCast(me, SpellIds.InfectedBite);
                task.Repeat((Seconds)8, (Seconds)10);
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

