// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.OverlordWyrmthalak
{
    struct SpellIds
    {
        public const int Blastwave = 11130;
        public const int Shout = 23511;
        public const int Cleave = 20691;
        public const int Knockaway = 20686;
    }

    struct MiscConst
    {
        public const int NpcSpirestoneWarlord = 9216;
        public const int NpcSmolderthornBerserker = 9268;

        public static Position SummonLocation1 = new Position(-39.355f, -513.456f, 88.472f, 4.679f);
        public static Position SummonLocation2 = new Position(-49.875f, -511.896f, 88.195f, 4.613f);
    }

    [Script]
    class boss_overlord_wyrmthalak : BossAI
    {
        bool Summoned;

        public boss_overlord_wyrmthalak(Creature creature) : base(creature, DataTypes.OverlordWyrmthalak)
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

            _scheduler.Schedule((Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Blastwave);
                task.Repeat((Seconds)20);
            });
            _scheduler.Schedule((Seconds)2, task =>
            {
                DoCastVictim(SpellIds.Shout);
                task.Repeat((Seconds)10);
            });
            _scheduler.Schedule((Seconds)6, task =>
            {
                DoCastVictim(SpellIds.Cleave);
                task.Repeat((Seconds)7);
            });
            _scheduler.Schedule((Seconds)12, task =>
            {
                DoCastVictim(SpellIds.Knockaway);
                task.Repeat((Seconds)14);
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

            if (!Summoned && HealthBelowPct(51))
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                if (target != null)
                {
                    Creature warlord = me.SummonCreature(MiscConst.NpcSpirestoneWarlord, MiscConst.SummonLocation1, TempSummonType.TimedDespawn, (Minutes)5);
                    if (warlord != null)
                        warlord.GetAI().AttackStart(target);
                    Creature berserker = me.SummonCreature(MiscConst.NpcSmolderthornBerserker, MiscConst.SummonLocation2, TempSummonType.TimedDespawn, (Minutes)5);
                    if (berserker != null)
                        berserker.GetAI().AttackStart(target);
                    Summoned = true;
                }
            }

            _scheduler.Update(diff);
        }
    }
}