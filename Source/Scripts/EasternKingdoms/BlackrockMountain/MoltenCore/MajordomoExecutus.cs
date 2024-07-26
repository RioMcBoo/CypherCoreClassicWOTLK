// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.MoltenCore.Majordomo
{
    struct SpellIds
    {
        public const int SummonRagnaros = 19774;
        public const int BlastWave = 20229;
        public const int Teleport = 20618;
        public const int MagicReflection = 20619;
        public const int AegisOfRagnaros = 20620;
        public const int DamageReflection = 21075;
    }

    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SaySpawn = 1;
        public const int SaySlay = 2;
        public const int SaySpecial = 3;
        public const int SayDefeat = 4;

        public const int SaySummonMaj = 5;
        public const int SayArrival2Maj = 6;

        public const int OptionIdYouChallengedUs = 0;
        public const int MenuOptionYouChallengedUs = 4108;
    }

    [Script]
    class boss_majordomo : BossAI
    {
        public boss_majordomo(Creature creature) : base(creature, DataTypes.MajordomoExecutus) { }

        public override void KilledUnit(Unit victim)
        {
            if (RandomHelper.URand(0, 99) < 25)
                Talk(TextIds.SaySlay);
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);
            Talk(TextIds.SayAggro);

            _scheduler.Schedule(Time.SpanFromSeconds(30), task =>
            {
                DoCast(me, SpellIds.MagicReflection);
                task.Repeat(Time.SpanFromSeconds(30));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(15), task =>
            {
                DoCast(me, SpellIds.DamageReflection);
                task.Repeat(Time.SpanFromSeconds(30));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(10), task =>
            {
                DoCastVictim(SpellIds.BlastWave);
                task.Repeat(Time.SpanFromSeconds(10));
            });
            _scheduler.Schedule(Time.SpanFromSeconds(20), task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 1);
                if (target != null)
                    DoCast(target, SpellIds.Teleport);
                task.Repeat(Time.SpanFromSeconds(20));
            });
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);

            if (instance.GetBossState(DataTypes.MajordomoExecutus) != EncounterState.Done)
            {
                if (!UpdateVictim())
                    return;

                if (me.FindNearestCreature(MCCreatureIds.FlamewakerHealer, 100.0f) == null && me.FindNearestCreature(MCCreatureIds.FlamewakerElite, 100.0f) == null)
                {
                    me.SetFaction(FactionTemplates.Friendly);
                    EnterEvadeMode();
                    Talk(TextIds.SayDefeat);
                    _JustDied();
                    _scheduler.Schedule(Time.SpanFromSeconds(32), task =>
                    {
                        me.NearTeleportTo(MCMiscConst.RagnarosTelePos.GetPositionX(), MCMiscConst.RagnarosTelePos.GetPositionY(), MCMiscConst.RagnarosTelePos.GetPositionZ(), MCMiscConst.RagnarosTelePos.GetOrientation());
                        me.SetNpcFlag(NPCFlags1.Gossip);
                    });
                    return;
                }

                if (me.HasUnitState(UnitState.Casting))
                    return;

                if (HealthBelowPct(50))
                    DoCast(me, SpellIds.AegisOfRagnaros, new CastSpellExtraArgs(true));
            }
        }

        public override void DoAction(int action)
        {
            if (action == ActionIds.StartRagnaros)
            {
                me.RemoveNpcFlag(NPCFlags1.Gossip);
                Talk(TextIds.SaySummonMaj);

                _scheduler.Schedule(Time.SpanFromSeconds(8), task =>
                {
                    instance.instance.SummonCreature(MCCreatureIds.Ragnaros, MCMiscConst.RagnarosSummonPos);
                });
                _scheduler.Schedule(Time.SpanFromSeconds(24), task =>
                {
                    Talk(TextIds.SayArrival2Maj);
                });
            }
            else if (action == ActionIds.StartRagnarosAlt)
            {
                me.SetFaction(FactionTemplates.Friendly);
                me.SetNpcFlag(NPCFlags1.Gossip);
            }
        }

        public override bool OnGossipSelect(Player player, int menuId, int gossipListId)
        {
            if (menuId == TextIds.MenuOptionYouChallengedUs && gossipListId == TextIds.OptionIdYouChallengedUs)
            {
                player.CloseGossipMenu();
                DoAction(ActionIds.StartRagnaros);
            }
            return false;
        }
    }
}

