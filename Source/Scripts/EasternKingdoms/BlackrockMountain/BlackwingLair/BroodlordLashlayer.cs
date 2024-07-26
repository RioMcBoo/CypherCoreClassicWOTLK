// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using System;
using System.Collections.Generic;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackwingLair.Broodlord
{
    struct SpellIds
    {
        public const int Cleave = 26350;
        public const int Blastwave = 23331;
        public const int Mortalstrike = 24573;
        public const int Knockback = 25778;
        public const int SuppressionAura = 22247; // Suppression Device Spell
    }

    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SayLeash = 1;
    }

    struct EventIds
    {
        // Suppression Device Events
        public const int SuppressionCast = 1;
        public const int SuppressionReset = 2;
    }

    struct ActionIds
    {
        public const int Deactivate = 0;
    }

    [Script]
    class boss_broodlord : BossAI
    {
        public boss_broodlord(Creature creature) : base(creature, DataTypes.BroodlordLashlayer) { }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);
            Talk(TextIds.SayAggro);

            _scheduler.Schedule((Seconds)8, task =>
            {
                DoCastVictim(SpellIds.Cleave);
                task.Repeat((Seconds)7);
            });
            _scheduler.Schedule((Seconds)12, task =>
            {
                DoCastVictim(SpellIds.Blastwave);
                task.Repeat((Seconds)8, (Seconds)16);
            });
            _scheduler.Schedule((Seconds)20, task =>
            {
                DoCastVictim(SpellIds.Mortalstrike);
                task.Repeat((Seconds)25, (Seconds)35);
            });
            _scheduler.Schedule((Seconds)30, task =>
            {
                DoCastVictim(SpellIds.Knockback);
                if (GetThreat(me.GetVictim()) != 0)
                    ModifyThreatByPercent(me.GetVictim(), -50);
                task.Repeat((Seconds)15, (Seconds)30);
            });
            _scheduler.Schedule((Seconds)1, task =>
            {
                if (me.GetDistance(me.GetHomePosition()) > 150.0f)
                {
                    Talk(TextIds.SayLeash);
                    EnterEvadeMode(EvadeReason.Boundary);
                }
                task.Repeat((Seconds)1);
            });
        }

        public override void JustDied(Unit killer)
        {
            _JustDied();

            List<GameObject> _goList = me.GetGameObjectListWithEntryInGrid(BWLGameObjectIds.SuppressionDevice, 200.0f);
            foreach (var go in _goList)
                go.GetAI().DoAction(ActionIds.Deactivate);
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }

    [Script]
    class go_suppression_device : GameObjectAI
    {
        InstanceScript _instance;
        bool _active;

        public go_suppression_device(GameObject go) : base(go)
        {
            _instance = go.GetInstanceScript();
            _active = true;
        }

        public override void InitializeAI()
        {
            if (_instance.GetBossState(DataTypes.BroodlordLashlayer) == EncounterState.Done)
            {
                Deactivate();
                return;
            }

            _events.ScheduleEvent(EventIds.SuppressionCast, (Seconds)0, Time.SpanFromSeconds(5));
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _events.Update(diff);

            _events.ExecuteEvents(eventId =>
            {
                switch (eventId)
                {
                    case EventIds.SuppressionCast:
                        if (me.GetGoState() == GameObjectState.Ready)
                        {
                            me.CastSpell(null, SpellIds.SuppressionAura, true);
                            me.SendCustomAnim(0);
                        }
                        _events.ScheduleEvent(EventIds.SuppressionCast, (Seconds)5);
                        break;
                    case EventIds.SuppressionReset:
                        Activate();
                        break;
                }
            });
        }

        public override void OnLootStateChanged(LootState state, Unit unit)
        {
            switch (state)
            {
                case LootState.Activated:
                    Deactivate();
                    _events.CancelEvent(EventIds.SuppressionCast);
                    _events.ScheduleEvent(EventIds.SuppressionReset, (Seconds)30, Time.SpanFromSeconds(120));
                    break;
                case LootState.JustDeactivated: // This case prevents the Gameobject despawn by Disarm Trap
                    me.SetLootState(LootState.Ready);
                    break;
            }
        }

        public override void DoAction(int action)
        {
            if (action == ActionIds.Deactivate)
            {
                Deactivate();
                _events.CancelEvent(EventIds.SuppressionReset);
            }
        }

        void Activate()
        {
            if (_active)
                return;
            _active = true;
            if (me.GetGoState() == GameObjectState.Active)
                me.SetGoState(GameObjectState.Ready);
            me.SetLootState(LootState.Ready);
            me.RemoveFlag(GameObjectFlags.NotSelectable);
            _events.ScheduleEvent(EventIds.SuppressionCast, (Seconds)0);
        }

        void Deactivate()
        {
            if (!_active)
                return;
            _active = false;
            me.SetGoState(GameObjectState.Active);
            me.SetFlag(GameObjectFlags.NotSelectable);
            _events.CancelEvent(EventIds.SuppressionCast);
        }
    }
}

