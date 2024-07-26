// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.MoltenCore.Lucifron
{
    struct SpellIds
    {
        public const int ImpendingDoom = 19702;
        public const int LucifronCurse = 19703;
        public const int ShadowShock = 20603;
    }

    [Script]
    class boss_lucifron : BossAI
    {
        public boss_lucifron(Creature creature) : base(creature, DataTypes.Lucifron) { }

        public override void JustEngagedWith(Unit victim)
        {
            base.JustEngagedWith(victim);

            _scheduler.Schedule(Time.SpanFromSeconds(10), task =>
            {
                DoCastVictim(SpellIds.ImpendingDoom);
                task.Repeat(Time.SpanFromSeconds(20));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(20), task =>
            {
                DoCastVictim(SpellIds.LucifronCurse);
                task.Repeat(Time.SpanFromSeconds(15));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(6), task =>
            {
                DoCastVictim(SpellIds.ShadowShock);
                task.Repeat(Time.SpanFromSeconds(6));
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

