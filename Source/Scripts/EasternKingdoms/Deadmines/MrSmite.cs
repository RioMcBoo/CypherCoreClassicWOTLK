// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.Deadmines
{
    struct SpellIds
    {
        public const int Trash = 3391;
        public const int SmiteStomp = 6432;
        public const int SmiteSlam = 6435;
    }

    struct EquipIds
    {
        public const int Sword = 5191;
        public const int Axe = 5196;
        public const int Mace = 7230;
    }

    struct TextIds
    {
        public const int SayPhase1 = 2;
        public const int SayPhase2 = 3;
    }

    [Script]
    class boss_mr_smite : ScriptedAI
    {
        InstanceScript instance;
        TimeSpan uiTrashTimer;
        TimeSpan uiSlamTimer;

        byte uiHealth;

        uint uiPhase;
        TimeSpan uiTimer;

        bool uiIsMoving;

        public boss_mr_smite(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            uiTrashTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(5000, 9000));
            uiSlamTimer = Time.SpanFromMilliseconds(9000);

            uiHealth = 0;

            uiPhase = 0;
            uiTimer = TimeSpan.Zero;

            uiIsMoving = false;
        }

        public override void Reset()
        {
            Initialize();

            SetEquipmentSlots(false, EquipIds.Sword, 0);
            me.SetStandState(UnitStandStateType.Stand);
            me.SetReactState(ReactStates.Aggressive);
            me.SetNoCallAssistance(true);
        }

        public override void JustEngagedWith(Unit who) { }

        bool bCheckChances()
        {
            uint uiChances = RandomHelper.URand(0, 99);
            if (uiChances <= 15)
                return false;
            else
                return true;
        }

        public override void UpdateAI(TimeSpan uiDiff)
        {
            if (!UpdateVictim())
                return;

            if (!uiIsMoving) // halt abilities in between phases
            {
                if (uiTrashTimer <= uiDiff)
                {
                    if (bCheckChances())
                        DoCast(me, SpellIds.Trash);
                    uiTrashTimer = Time.SpanFromMilliseconds(RandomHelper.IRand(6000, 15500));
                }
                else uiTrashTimer -= uiDiff;

                if (uiSlamTimer <= uiDiff)
                {
                    if (bCheckChances())
                        DoCastVictim(SpellIds.SmiteSlam);
                    uiSlamTimer = Time.SpanFromMilliseconds(11000);
                }
                else uiSlamTimer -= uiDiff;

            }

            if ((uiHealth == 0 && !HealthAbovePct(66)) || (uiHealth == 1 && !HealthAbovePct(33)))
            {
                ++uiHealth;
                DoCastAOE(SpellIds.SmiteStomp, new CastSpellExtraArgs(false));
                SetCombatMovement(false);
                me.AttackStop();
                me.InterruptNonMeleeSpells(false);
                me.SetReactState(ReactStates.Passive);
                uiTimer = Time.SpanFromMilliseconds(2500);
                uiPhase = 1;

                switch (uiHealth)
                {
                    case 1:
                        Talk(TextIds.SayPhase1);
                        break;
                    case 2:
                        Talk(TextIds.SayPhase2);
                        break;
                }
            }

            if (uiPhase != 0)
            {
                if (uiTimer <= uiDiff)
                {
                    switch (uiPhase)
                    {
                        case 1:
                        {
                            if (uiIsMoving)
                                break;

                            GameObject go = ObjectAccessor.GetGameObject(me, instance.GetGuidData(DataTypes.SmiteChest));
                            if (go != null)
                            {
                                me.GetMotionMaster().Clear();
                                me.GetMotionMaster().MovePoint(1, go.GetPositionX() - 1.5f, go.GetPositionY() + 1.4f, go.GetPositionZ());
                                uiIsMoving = true;
                            }
                            break;
                        }
                        case 2:
                            if (uiHealth == 1)
                                SetEquipmentSlots(false, EquipIds.Axe, EquipIds.Axe);
                            else
                                SetEquipmentSlots(false, EquipIds.Mace, 0);
                            uiTimer = Time.SpanFromMilliseconds(500);
                            uiPhase = 3;
                            break;
                        case 3:
                            me.SetStandState(UnitStandStateType.Stand);
                            uiTimer = Time.SpanFromMilliseconds(750);
                            uiPhase = 4;
                            break;
                        case 4:
                            me.SetReactState(ReactStates.Aggressive);
                            SetCombatMovement(true);
                            me.GetMotionMaster().MoveChase(me.GetVictim(), me.m_CombatDistance);
                            uiIsMoving = false;
                            uiPhase = 0;
                            break;
                    }
                }
                else uiTimer -= uiDiff;
            }
        }

        public override void MovementInform(MovementGeneratorType uiType, int uiId)
        {
            if (uiType != MovementGeneratorType.Point)
                return;

            me.SetFacingTo(5.47f);
            me.SetStandState(UnitStandStateType.Kneel);

            uiTimer = Time.SpanFromMilliseconds(2000);
            uiPhase = 2;
        }
    }
}

