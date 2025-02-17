// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.MotherSmolderweb
{
    struct SpellIds
    {
        public const int Crystalize = 16104;
        public const int Mothersmilk = 16468;
        public const int SummonSpireSpiderling = 16103;
    }

    [Script]
    class boss_mother_smolderweb : BossAI
    {
        public boss_mother_smolderweb(Creature creature) : base(creature, DataTypes.MotherSmolderweb) { }

        public override void Reset()
        {
            _Reset();
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);
            _scheduler.Schedule((Seconds)20, task =>
            {
                DoCast(me, SpellIds.Crystalize);
                task.Repeat((Seconds)15);
            });
            _scheduler.Schedule((Seconds)10, task =>
            {
                DoCast(me, SpellIds.Mothersmilk);
                task.Repeat((Seconds)5, Time.SpanFromMilliseconds(12500));
            });
        }

        public override void JustDied(Unit killer)
        {
            _JustDied();
        }

        public override void DamageTaken(Unit done_by, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (me.GetHealth() <= damage)
                DoCast(me, SpellIds.SummonSpireSpiderling, new CastSpellExtraArgs(true));
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }
}
