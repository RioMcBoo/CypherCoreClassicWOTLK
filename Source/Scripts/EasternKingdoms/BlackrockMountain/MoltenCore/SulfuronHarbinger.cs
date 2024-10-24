// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;
using System.Collections.Generic;

namespace Scripts.EasternKingdoms.BlackrockMountain.MoltenCore.Sulfuron
{
    struct SpellIds
    {
        // Sulfuron Harbringer
        public const int DarkStrike = 19777;
        public const int DemoralizingShout = 19778;
        public const int Inspire = 19779;
        public const int Knockdown = 19780;
        public const int Flamespear = 19781;

        // Adds
        public const int Heal = 19775;
        public const int Shadowwordpain = 19776;
        public const int Immolate = 20294;
    }

    [Script]
    class boss_sulfuron : BossAI
    {
        public boss_sulfuron(Creature creature) : base(creature, DataTypes.SulfuronHarbinger) { }

        public override void JustEngagedWith(Unit victim)
        {
            base.JustEngagedWith(victim);

            _scheduler.Schedule(Time.SpanFromSeconds(10), task =>
            {
                DoCast(me, SpellIds.DarkStrike);
                task.Repeat(Time.SpanFromSeconds(15), Time.SpanFromSeconds(18));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(15), task =>
            {
                DoCastVictim(SpellIds.DemoralizingShout);
                task.Repeat(Time.SpanFromSeconds(15), Time.SpanFromSeconds(20));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(13), task =>
            {
                List<Creature> healers = DoFindFriendlyMissingBuff(45.0f, SpellIds.Inspire);
                if (!healers.Empty())
                    DoCast(healers.SelectRandom(), SpellIds.Inspire);

                DoCast(me, SpellIds.Inspire);
                task.Repeat(Time.SpanFromSeconds(20), Time.SpanFromSeconds(26));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(6), task =>
            {
                DoCastVictim(SpellIds.Knockdown);
                task.Repeat(Time.SpanFromSeconds(12), Time.SpanFromSeconds(15));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(2), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 0.0f, true);
                if (target != null)
                    DoCast(target, SpellIds.Flamespear);
                task.Repeat(Time.SpanFromSeconds(12), Time.SpanFromSeconds(16));
            });
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }

    [Script]
    class npc_flamewaker_priest : ScriptedAI
    {
        public npc_flamewaker_priest(Creature creature) : base(creature)
        {
        }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void JustDied(Unit killer)
        {
            _scheduler.CancelAll();
        }

        public override void JustEngagedWith(Unit victim)
        {
            base.JustEngagedWith(victim);

            _scheduler.Schedule(Time.SpanFromSeconds(15), Time.SpanFromSeconds(30), task =>
            {
                Unit target = DoSelectLowestHpFriendly(60.0f, 1);
                if (target != null)
                    DoCast(target, SpellIds.Heal);
                task.Repeat(Time.SpanFromSeconds(15), Time.SpanFromSeconds(20));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(2), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 0.0f, true, true, -SpellIds.Shadowwordpain);
                if (target != null)
                    DoCast(target, SpellIds.Shadowwordpain);
                task.Repeat(Time.SpanFromSeconds(18), Time.SpanFromSeconds(26));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(8), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 0.0f, true, true, -SpellIds.Immolate);
                if (target != null)
                    DoCast(target, SpellIds.Immolate);
                task.Repeat(Time.SpanFromSeconds(15), Time.SpanFromSeconds(25));
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

