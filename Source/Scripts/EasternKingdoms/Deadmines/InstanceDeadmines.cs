// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.Deadmines
{
    enum DMCannonState
    {
        CannonNotUsed,
        CannonGunpowderUsed,
        CannonBlastInitiated,
        PiratesAttack,
        EventDone
    }

    enum DMData
    {
        EventState,
        EventRhahkzor
    }

    struct DataTypes
    {
        public const int SmiteChest = 0;
    }

    struct GameObjectIds
    {
        public const int FactoryDoor = 13965;
        public const int IroncladDoor = 16397;
        public const int DefiasCannon = 16398;
        public const int DoorLever = 101833;
        public const int MrSmiteChest = 144111;
    }

    struct SoundIds
    {
        public const int Cannonfire = 1400;
        public const int Destroydoor = 3079;
        public const int MrSmiteAlarm1 = 5775;
        public const int MrSmiteAlarm2 = 5777;
    }


    struct MiscConst
    {
        public static readonly TimeSpan DataCannonBlastTimer = (Seconds)3;
        public static readonly TimeSpan DataPiratesDelayTimer = (Seconds)1;
    }

    class instance_deadmines : InstanceMapScript
    {
        public instance_deadmines() : base(nameof(instance_deadmines), 36) { }

        class instance_deadmines_InstanceMapScript : InstanceScript
        {
            ObjectGuid FactoryDoorGUID;
            ObjectGuid IronCladDoorGUID;
            ObjectGuid DefiasCannonGUID;
            ObjectGuid DoorLeverGUID;
            ObjectGuid DefiasPirate1GUID;
            ObjectGuid DefiasPirate2GUID;

            DMCannonState State;
            TimeSpan CannonBlast_Timer;
            TimeSpan PiratesDelay_Timer;
            ObjectGuid uiSmiteChestGUID;

            public instance_deadmines_InstanceMapScript(InstanceMap map) : base(map)
            {
                SetHeaders("DM");

                State = DMCannonState.CannonNotUsed;
                CannonBlast_Timer = TimeSpan.Zero;
                PiratesDelay_Timer = TimeSpan.Zero;
            }

            public override void Update(TimeSpan diff)
            {
                if (IronCladDoorGUID.IsEmpty() || DefiasCannonGUID.IsEmpty() || DoorLeverGUID.IsEmpty())
                    return;

                GameObject pIronCladDoor = instance.GetGameObject(IronCladDoorGUID);
                if (pIronCladDoor == null)
                    return;

                switch (State)
                {
                    case DMCannonState.CannonGunpowderUsed:
                        CannonBlast_Timer = MiscConst.DataCannonBlastTimer;
                        // it's a hack - Mr. Smite should do that but his too far away
                        //pIronCladDoor.SetName("Mr. Smite");
                        //pIronCladDoor.MonsterYell(SayMrSmiteAlarm1, LangUniversal, null);
                        pIronCladDoor.PlayDirectSound(SoundIds.MrSmiteAlarm1);
                        State = DMCannonState.CannonBlastInitiated;
                        break;
                    case DMCannonState.CannonBlastInitiated:
                        PiratesDelay_Timer = MiscConst.DataPiratesDelayTimer;
                        if (CannonBlast_Timer <= diff)
                        {
                            SummonCreatures();
                            ShootCannon();
                            BlastOutDoor();
                            LeverStucked();
                            //pIronCladDoor.MonsterYell(SayMrSmiteAlarm2, LangUniversal, null);
                            pIronCladDoor.PlayDirectSound(SoundIds.MrSmiteAlarm2);
                            State = DMCannonState.PiratesAttack;
                        }
                        else CannonBlast_Timer -= diff;
                        break;
                    case DMCannonState.PiratesAttack:
                        if (PiratesDelay_Timer <= diff)
                        {
                            MoveCreaturesInside();
                            State = DMCannonState.EventDone;
                        }
                        else PiratesDelay_Timer -= diff;
                        break;
                }
            }

            void SummonCreatures()
            {
                GameObject pIronCladDoor = instance.GetGameObject(IronCladDoorGUID);
                if (pIronCladDoor != null)
                {
                    Creature DefiasPirate1 = pIronCladDoor.SummonCreature(657, pIronCladDoor.GetPositionX() - 2, pIronCladDoor.GetPositionY() - 7, pIronCladDoor.GetPositionZ(), 0, TempSummonType.CorpseTimedDespawn, (Seconds)3);
                    Creature DefiasPirate2 = pIronCladDoor.SummonCreature(657, pIronCladDoor.GetPositionX() + 3, pIronCladDoor.GetPositionY() - 6, pIronCladDoor.GetPositionZ(), 0, TempSummonType.CorpseTimedDespawn, (Seconds)3);

                    DefiasPirate1GUID = DefiasPirate1.GetGUID();
                    DefiasPirate2GUID = DefiasPirate2.GetGUID();
                }
            }

            void MoveCreaturesInside()
            {
                if (DefiasPirate1GUID.IsEmpty() || DefiasPirate2GUID.IsEmpty())
                    return;

                Creature pDefiasPirate1 = instance.GetCreature(DefiasPirate1GUID);
                Creature pDefiasPirate2 = instance.GetCreature(DefiasPirate2GUID);
                if (pDefiasPirate1 == null || pDefiasPirate2 == null)
                    return;

                MoveCreatureInside(pDefiasPirate1);
                MoveCreatureInside(pDefiasPirate2);
            }

            void MoveCreatureInside(Creature creature)
            {
                creature.SetWalk(false);
                creature.GetMotionMaster().MovePoint(0, -102.7f, -655.9f, creature.GetPositionZ());
            }

            void ShootCannon()
            {
                GameObject pDefiasCannon = instance.GetGameObject(DefiasCannonGUID);
                if (pDefiasCannon != null)
                {
                    pDefiasCannon.SetGoState(GameObjectState.Active);
                    pDefiasCannon.PlayDirectSound(SoundIds.Cannonfire);
                }
            }

            void BlastOutDoor()
            {
                GameObject pIronCladDoor = instance.GetGameObject(IronCladDoorGUID);
                if (pIronCladDoor != null)
                {
                    pIronCladDoor.SetGoState(GameObjectState.Destroyed);
                    pIronCladDoor.PlayDirectSound(SoundIds.Destroydoor);
                }
            }

            void LeverStucked()
            {
                GameObject pDoorLever = instance.GetGameObject(DoorLeverGUID);
                if (pDoorLever != null)
                    pDoorLever.SetFlag(GameObjectFlags.InteractCond);
            }

            public override void OnGameObjectCreate(GameObject go)
            {
                switch (go.GetEntry())
                {
                    case GameObjectIds.FactoryDoor:
                        FactoryDoorGUID = go.GetGUID();
                        break;
                    case GameObjectIds.IroncladDoor:
                        IronCladDoorGUID = go.GetGUID();
                        break;
                    case GameObjectIds.DefiasCannon:
                        DefiasCannonGUID = go.GetGUID();
                        break;
                    case GameObjectIds.DoorLever:
                        DoorLeverGUID = go.GetGUID();
                        break;
                    case GameObjectIds.MrSmiteChest:
                        uiSmiteChestGUID = go.GetGUID();
                        break;
                }
            }

            public override void SetData(int type, int data)
            {
                switch ((DMData)type)
                {
                    case DMData.EventState:
                        if (!DefiasCannonGUID.IsEmpty() && !IronCladDoorGUID.IsEmpty())
                            State = (DMCannonState)data;
                        break;
                    case DMData.EventRhahkzor:
                        if (data == (uint)EncounterState.Done)
                        {
                            GameObject go = instance.GetGameObject(FactoryDoorGUID);
                            if (go != null)
                                go.SetGoState(GameObjectState.Active);
                        }
                        break;
                }
            }

            public override int GetData(int type)
            {
                switch ((DMData)type)
                {
                    case DMData.EventState:
                        return (int)State;
                }

                return 0;
            }

            public override ObjectGuid GetGuidData(int data)
            {
                switch (data)
                {
                    case DataTypes.SmiteChest:
                        return uiSmiteChestGUID;
                }

                return ObjectGuid.Empty;
            }
        }

        public override InstanceScript GetInstanceScript(InstanceMap map)
        {
            return new instance_deadmines_InstanceMapScript(map);
        }
    }
}

