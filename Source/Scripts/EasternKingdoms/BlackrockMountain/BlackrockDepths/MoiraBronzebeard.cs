// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockDepths.MoiraBronzebeard
{
    struct SpellIds
    {
        public const int Heal = 10917;
        public const int Renew = 10929;
        public const int Shield = 10901;
        public const int Mindblast = 10947;
        public const int Shadowwordpain = 10894;
        public const int Smite = 10934;
    }

    [Script]
    class boss_moira_bronzebeard : ScriptedAI
    {
        public boss_moira_bronzebeard(Creature creature) : base(creature) { }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void JustEngagedWith(Unit who)
        {
            //_scheduler.Schedule(EventHeal, Time.SpanFromSeconds(12s)); // not used atm // These times are probably wrong
            _scheduler.Schedule((Seconds)16, task =>
            {
                DoCastVictim(SpellIds.Mindblast);
                task.Repeat((Seconds)14);
            });
            _scheduler.Schedule((Seconds)2, task =>
            {
                DoCastVictim(SpellIds.Shadowwordpain);
                task.Repeat((Seconds)18);
            });
            _scheduler.Schedule((Seconds)8, task =>
            {
                DoCastVictim(SpellIds.Smite);
                task.Repeat((Seconds)10);
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

