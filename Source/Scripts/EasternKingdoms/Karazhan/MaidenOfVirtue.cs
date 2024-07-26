// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.Karazhan.MaidenOfVirtue
{
    struct SpellIds
    {
        public const int Repentance = 29511;
        public const int Holyfire = 29522;
        public const int Holywrath = 32445;
        public const int Holyground = 29523;
        public const int Berserk = 26662;
    }

    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SaySlay = 1;
        public const int SayRepentance = 2;
        public const int SayDeath = 3;
    }

    [Script]
    class boss_maiden_of_virtue : BossAI
    {
        public boss_maiden_of_virtue(Creature creature) : base(creature, DataTypes.MaidenOfVirtue) { }

        public override void KilledUnit(Unit Victim)
        {
            if (RandomHelper.randChance(50))
                Talk(TextIds.SaySlay);
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayDeath);
            _JustDied();
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);
            Talk(TextIds.SayAggro);

            DoCastSelf(SpellIds.Holyground, new CastSpellExtraArgs(true));
            _scheduler.Schedule(Time.SpanFromSeconds(33), Time.SpanFromSeconds(45), task =>
            {
                DoCastVictim(SpellIds.Repentance);
                Talk(TextIds.SayRepentance);
                task.Repeat(Time.SpanFromSeconds(35));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(8), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 50, true);
                if (target != null)
                    DoCast(target, SpellIds.Holyfire);
                task.Repeat(Time.SpanFromSeconds(8), Time.SpanFromSeconds(19));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(15), Time.SpanFromSeconds(25), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 80, true);
                if (target != null)
                    DoCast(target, SpellIds.Holywrath);
                task.Repeat(Time.SpanFromSeconds(15), Time.SpanFromSeconds(25));
            });
            _scheduler.Schedule((Minutes)10, task =>
            {
                DoCastSelf(SpellIds.Berserk, new CastSpellExtraArgs(true));
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