// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.MoltenCore.Magmadar
{
    struct SpellIds
    {
        public const int Frenzy = 19451;
        public const int MagmaSpit = 19449;
        public const int Panic = 19408;
        public const int LavaBomb = 19428;
    }

    struct TextIds
    {
        public const int EmoteFrenzy = 0;
    }

    [Script]
    class boss_magmadar : BossAI
    {
        public boss_magmadar(Creature creature) : base(creature, DataTypes.Magmadar) { }

        public override void Reset()
        {
            base.Reset();
            DoCast(me, SpellIds.MagmaSpit, new CastSpellExtraArgs(true));
        }

        public override void JustEngagedWith(Unit victim)
        {
            base.JustEngagedWith(victim);

            _scheduler.Schedule(Time.SpanFromSeconds(30), task =>
            {
                Talk(TextIds.EmoteFrenzy);
                DoCast(me, SpellIds.Frenzy);
                task.Repeat(Time.SpanFromSeconds(15));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(20), task =>
            {
                DoCastVictim(SpellIds.Panic);
                task.Repeat(Time.SpanFromSeconds(35));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(12), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 0.0f, true, true, -SpellIds.LavaBomb);
                if (target != null)
                    DoCast(target, SpellIds.LavaBomb);
                task.Repeat(Time.SpanFromSeconds(12));
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

