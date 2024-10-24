// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockDepths.Magmus
{
    struct SpellIds
    {
        //Magmus
        public const int Fieryburst = 13900;
        public const int Warstomp = 24375;

        //IronhandGuardian
        public const int Goutofflame = 15529;
    }

    enum Phases
    {
        One = 1,
        Two = 2
    }

    [Script]
    class boss_magmus : ScriptedAI
    {
        Phases phase;

        public boss_magmus(Creature creature) : base(creature) { }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void JustEngagedWith(Unit who)
        {
            InstanceScript instance = me.GetInstanceScript();
            if (instance != null)
                instance.SetData(DataTypes.TypeIronHall, (int)EncounterState.InProgress);

            phase = Phases.One;
            _scheduler.Schedule((Seconds)5, task =>
            {
                DoCastVictim(SpellIds.Fieryburst);
                task.Repeat((Seconds)6);
            });
        }

        public override void DamageTaken(Unit attacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (me.HealthBelowPctDamaged(50, damage) && phase == Phases.One)
            {
                phase = Phases.Two;
                _scheduler.Schedule((Seconds)0, task =>
                {
                    DoCastVictim(SpellIds.Warstomp);
                    task.Repeat((Seconds)8);
                });
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }

        public override void JustDied(Unit killer)
        {
            InstanceScript instance = me.GetInstanceScript();
            if (instance != null)
            {
                instance.HandleGameObject(instance.GetGuidData(DataTypes.DataThroneDoor), true);
                instance.SetData(DataTypes.TypeIronHall, (int)EncounterState.Done);
            }
        }
    }

    [Script]
    class npc_ironhand_guardian : ScriptedAI
    {
        InstanceScript _instance;
        bool _active;

        public npc_ironhand_guardian(Creature creature) : base(creature)
        {
            _instance = me.GetInstanceScript();
            _active = false;
        }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!_active)
            {
                if (_instance.GetData(DataTypes.TypeIronHall) == (int)EncounterState.NotStarted)
                    return;
                // Once the boss is engaged, the guardians will stay activated until the next instance reset
                _scheduler.Schedule((Seconds)0, (Seconds)10, task =>
                {
                    DoCastAOE(SpellIds.Goutofflame);
                    task.Repeat((Seconds)16, (Seconds)21);
                });
                _active = true;
            }

            _scheduler.Update(diff);
        }
    }
}