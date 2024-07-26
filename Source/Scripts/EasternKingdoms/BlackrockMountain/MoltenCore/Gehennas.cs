// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.MoltenCore.Gehennas
{
    struct SpellIds
    {
        public const int GehennasCurse = 19716;
        public const int RainOfFire = 19717;
        public const int ShadowBolt = 19728;
    }

    [Script]
    class boss_gehennas : BossAI
    {
        public boss_gehennas(Creature creature) : base(creature, DataTypes.Gehennas) { }

        public override void JustEngagedWith(Unit victim)
        {
            base.JustEngagedWith(victim);

            _scheduler.Schedule(Time.SpanFromSeconds(12), task =>
            {
                DoCastVictim(SpellIds.GehennasCurse);
                task.Repeat(Time.SpanFromSeconds(22), Time.SpanFromSeconds(30));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(10), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0);
                if (target != null)
                    DoCast(target, SpellIds.RainOfFire);
                task.Repeat(Time.SpanFromSeconds(4), Time.SpanFromSeconds(12));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(6), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 1);
                if (target != null)
                    DoCast(target, SpellIds.ShadowBolt);
                task.Repeat(Time.SpanFromSeconds(7));
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

