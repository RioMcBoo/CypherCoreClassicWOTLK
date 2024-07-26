// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockSpire.RendBlackhand
{
    struct SpellIds
    {
        public const int Whirlwind = 13736; // sniffed
        public const int Cleave = 15284;
        public const int MortalStrike = 16856;
        public const int Frenzy = 8269;
        public const int Knockdown = 13360;  // On spawn during Gyth fight
    }

    struct TextIds
    {
        // Rend Blackhand
        public const int SayBlackhand1 = 0;
        public const int SayBlackhand2 = 1;
        public const int EmoteBlackhandDismount = 2;
        // Victor Nefarius
        public const int SayNefarius0 = 0;
        public const int SayNefarius1 = 1;
        public const int SayNefarius2 = 2;
        public const int SayNefarius3 = 3;
        public const int SayNefarius4 = 4;
        public const int SayNefarius5 = 5;
        public const int SayNefarius6 = 6;
        public const int SayNefarius7 = 7;
        public const int SayNefarius8 = 8;
        public const int SayNefarius9 = 9;
    }

    struct AddIds
    {
        public const int ChromaticWhelp = 10442;
        public const int ChromaticDragonspawn = 10447;
        public const int BlackhandDragonHandler = 10742;
    }

    struct MiscConst
    {
        public const int NefariusPath1 = 11037360;
        public const int NefariusPath2 = 11037368;
        public const int NefariusPath3 = 11037376;
        public const int RendPath1 = 11037440;
        public const int RendPath2 = 11037448;

        public static Wave[] Wave2 = // 22 sec
        [
            new Wave(10447, 209.8637f, -428.2729f, 110.9877f, 0.6632251f),
            new Wave(10442, 209.3122f, -430.8724f, 110.9814f, 2.9147f),
            new Wave(10442, 211.3309f, -425.9111f, 111.0006f, 1.727876f)
        ];

        public static Wave[] Wave3 = // 60 sec
        [
            new Wave(10742, 208.6493f, -424.5787f, 110.9872f, 5.8294f),
            new Wave(10447, 203.9482f, -428.9446f, 110.982f,  4.677482f),
            new Wave(10442, 203.3441f, -426.8668f, 110.9772f, 4.712389f),
            new Wave(10442, 206.3079f, -424.7509f, 110.9943f, 4.08407f)
        ];

        public static Wave[] Wave4 = // 49 sec
        [
            new Wave(10742, 212.3541f, -412.6826f, 111.0352f, 5.88176f),
            new Wave(10447, 212.5754f, -410.2841f, 111.0296f, 2.740167f),
            new Wave(10442, 212.3449f, -414.8659f, 111.0348f, 2.356194f),
            new Wave(10442, 210.6568f, -412.1552f, 111.0124f, 0.9773844f)
        ];

        public static Wave[] Wave5 = // 60 sec
        [
            new Wave(10742, 210.2188f, -410.6686f, 111.0211f, 5.8294f),
            new Wave(10447, 209.4078f, -414.13f,   111.0264f, 4.677482f),
            new Wave(10442, 208.0858f, -409.3145f, 111.0118f, 4.642576f),
            new Wave(10442, 207.9811f, -413.0728f, 111.0098f, 5.288348f),
            new Wave(10442, 208.0854f, -412.1505f, 111.0057f, 4.08407f)
        ];

        public static Wave[] Wave6 = // 27 sec
        [
            new Wave(10742, 213.9138f, -426.512f,  111.0013f, 3.316126f),
            new Wave(10447, 213.7121f, -429.8102f, 110.9888f, 1.413717f),
            new Wave(10447, 213.7157f, -424.4268f, 111.009f,  3.001966f),
            new Wave(10442, 210.8935f, -423.913f,  111.0125f, 5.969026f),
            new Wave(10442, 212.2642f, -430.7648f, 110.9807f, 5.934119f)
        ];

        public static Position GythLoc = new Position(211.762f, -397.5885f, 111.1817f, 4.747295f);
        public static Position Teleport1Loc = new Position(194.2993f, -474.0814f, 121.4505f, -0.01225555f);
        public static Position Teleport2Loc = new Position(216.485f, -434.93f, 110.888f, -0.01225555f);
    }

    class Wave
    {
        public int entry;
        public float x_pos;
        public float y_pos;
        public float z_pos;
        public float o_pos;

        public Wave(int _entry, float x, float y, float z, float o)
        {
            entry = _entry;
            x_pos = x;
            y_pos = y;
            z_pos = z;
            o_pos = o;
        }
    }

    struct EventIds
    {
        public const int Start1 = 1;
        public const int Start2 = 2;
        public const int Start3 = 3;
        public const int Start4 = 4;
        public const int TurnToRend = 5;
        public const int TurnToPlayer = 6;
        public const int TurnToFacing1 = 7;
        public const int TurnToFacing2 = 8;
        public const int TurnToFacing3 = 9;
        public const int Wave1 = 10;
        public const int Wave2 = 11;
        public const int Wave3 = 12;
        public const int Wave4 = 13;
        public const int Wave5 = 14;
        public const int Wave6 = 15;
        public const int WavesText1 = 16;
        public const int WavesText2 = 17;
        public const int WavesText3 = 18;
        public const int WavesText4 = 19;
        public const int WavesText5 = 20;
        public const int WavesCompleteText1 = 21;
        public const int WavesCompleteText2 = 22;
        public const int WavesCompleteText3 = 23;
        public const int WavesEmote1 = 24;
        public const int WavesEmote2 = 25;
        public const int PathRend = 26;
        public const int PathNefarius = 27;
        public const int Teleport1 = 28;
        public const int Teleport2 = 29;
        public const int Whirlwind = 30;
        public const int Cleave = 31;
        public const int MortalStrike = 32;
    }

    [Script]
    class boss_rend_blackhand : BossAI
    {
        bool gythEvent;
        ObjectGuid victorGUID;
        ObjectGuid portcullisGUID;

        public boss_rend_blackhand(Creature creature) : base(creature, DataTypes.WarchiefRendBlackhand) { }

        public override void Reset()
        {
            _Reset();
            gythEvent = false;
            victorGUID.Clear();
            portcullisGUID.Clear();
        }

        public override void JustEngagedWith(Unit who)
        {
            base.JustEngagedWith(who);
            _events.ScheduleEvent(EventIds.Whirlwind, Time.SpanFromSeconds(13), Time.SpanFromSeconds(15));
            _events.ScheduleEvent(EventIds.Cleave, Time.SpanFromSeconds(15), Time.SpanFromSeconds(17));
            _events.ScheduleEvent(EventIds.MortalStrike, Time.SpanFromSeconds(17), Time.SpanFromSeconds(19));
        }

        public override void IsSummonedBy(WorldObject summoner)
        {
            me.SetImmuneToPC(false);
            DoZoneInCombat();
        }

        public override void JustDied(Unit killer)
        {
            _JustDied();
            Creature victor = me.FindNearestCreature(CreaturesIds.LordVictorNefarius, 75.0f, true);
            if (victor != null)
                victor.GetAI().SetData(1, 2);
        }

        public override void SetData(int type, int data)
        {
            if (type == BRSMiscConst.Areatrigger && data == BRSMiscConst.AreatriggerBlackrockStadium)
            {
                if (!gythEvent)
                {
                    gythEvent = true;

                    Creature victor = me.FindNearestCreature(CreaturesIds.LordVictorNefarius, 5.0f, true);
                    if (victor != null)
                        victorGUID = victor.GetGUID();

                    GameObject portcullis = me.FindNearestGameObject(GameObjectsIds.DrPortcullis, 50.0f);
                    if (portcullis != null)
                        portcullisGUID = portcullis.GetGUID();

                    _events.ScheduleEvent(EventIds.TurnToPlayer, (Seconds)0);
                    _events.ScheduleEvent(EventIds.Start1, (Seconds)1);
                }
            }
        }

        public override void MovementInform(MovementGeneratorType type, int id)
        {
            if (type == MovementGeneratorType.Waypoint)
            {
                switch (id)
                {
                    case 5:
                        _events.ScheduleEvent(EventIds.Teleport1, (Seconds)2);
                        break;
                    case 11:
                        Creature gyth = me.FindNearestCreature(CreaturesIds.Gyth, 10.0f, true);
                        if (gyth != null)
                            gyth.GetAI().SetData(1, 1);
                        me.DespawnOrUnsummon((Seconds)1, (Days)7);
                        break;
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (gythEvent)
            {
                _events.Update(diff);

                _events.ExecuteEvents(eventId =>
                {
                    switch (eventId)
                    {
                        case EventIds.Start1:
                        {
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetAI().Talk(TextIds.SayNefarius0);
                            _events.ScheduleEvent(EventIds.Start2, (Seconds)4);
                            break;
                        }
                        case EventIds.Start2:
                        {
                            _events.ScheduleEvent(EventIds.TurnToPlayer, (Seconds)0);
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.HandleEmoteCommand(Emote.OneshotPoint);
                            _events.ScheduleEvent(EventIds.Start3, (Seconds)4);
                            break;
                        }
                        case EventIds.Start3:
                        {
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetAI().Talk(TextIds.SayNefarius1);
                            _events.ScheduleEvent(EventIds.Wave1, (Seconds)2);
                            _events.ScheduleEvent(EventIds.TurnToRend, (Seconds)4);
                            _events.ScheduleEvent(EventIds.WavesText1, (Seconds)20);
                            break;
                        }
                        case EventIds.TurnToRend:
                        {
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                            {
                                victor.SetFacingToObject(me);
                                victor.HandleEmoteCommand(Emote.OneshotTalk);
                            }
                            break;
                        }
                        case EventIds.TurnToPlayer:
                        {
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                            {
                                Unit player = victor.SelectNearestPlayer(60.0f);
                                if (player != null)
                                    victor.SetFacingToObject(player);
                            }
                            break;
                        }
                        case EventIds.TurnToFacing1:
                        {
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.SetFacingTo(1.518436f);
                            break;
                        }
                        case EventIds.TurnToFacing2:
                            me.SetFacingTo(1.658063f);
                            break;
                        case EventIds.TurnToFacing3:
                            me.SetFacingTo(1.500983f);
                            break;
                        case EventIds.WavesEmote1:
                        {
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.HandleEmoteCommand(Emote.OneshotQuestion);
                            break;
                        }
                        case EventIds.WavesEmote2:
                            me.HandleEmoteCommand(Emote.OneshotRoar);
                            break;
                        case EventIds.WavesText1:
                        {
                            _events.ScheduleEvent(EventIds.TurnToPlayer, (Seconds)0);
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetAI().Talk(TextIds.SayNefarius2);
                            me.HandleEmoteCommand(Emote.OneshotTalk);
                            _events.ScheduleEvent(EventIds.TurnToFacing1, (Seconds)4);
                            _events.ScheduleEvent(EventIds.WavesEmote1, (Seconds)5);
                            _events.ScheduleEvent(EventIds.Wave2, (Seconds)2);
                            _events.ScheduleEvent(EventIds.WavesText2, (Seconds)20);
                            break;
                        }
                        case EventIds.WavesText2:
                        {
                            _events.ScheduleEvent(EventIds.TurnToPlayer, (Seconds)0);
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetAI().Talk(TextIds.SayNefarius3);
                            _events.ScheduleEvent(EventIds.TurnToFacing1, (Seconds)4);
                            _events.ScheduleEvent(EventIds.Wave3, (Seconds)2);
                            _events.ScheduleEvent(EventIds.WavesText3, (Seconds)20);
                            break;
                        }
                        case EventIds.WavesText3:
                        {
                            _events.ScheduleEvent(EventIds.TurnToPlayer, (Seconds)0);
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetAI().Talk(TextIds.SayNefarius4);
                            _events.ScheduleEvent(EventIds.TurnToFacing1, (Seconds)4);
                            _events.ScheduleEvent(EventIds.Wave4, (Seconds)2);
                            _events.ScheduleEvent(EventIds.WavesText4, (Seconds)20);
                            break;
                        }
                        case EventIds.WavesText4:
                            Talk(TextIds.SayBlackhand1);
                            _events.ScheduleEvent(EventIds.WavesEmote2, (Seconds)4);
                            _events.ScheduleEvent(EventIds.TurnToFacing3, (Seconds)8);
                            _events.ScheduleEvent(EventIds.Wave5, (Seconds)2);
                            _events.ScheduleEvent(EventIds.WavesText5, (Seconds)20);
                            break;
                        case EventIds.WavesText5:
                        {
                            _events.ScheduleEvent(EventIds.TurnToPlayer, (Seconds)0);
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetAI().Talk(TextIds.SayNefarius5);
                            _events.ScheduleEvent(EventIds.TurnToFacing1, (Seconds)4);
                            _events.ScheduleEvent(EventIds.Wave6, (Seconds)2);
                            _events.ScheduleEvent(EventIds.WavesCompleteText1, (Seconds)20);
                            break;
                        }
                        case EventIds.WavesCompleteText1:
                        {
                            _events.ScheduleEvent(EventIds.TurnToPlayer, (Seconds)0);
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetAI().Talk(TextIds.SayNefarius6);
                            _events.ScheduleEvent(EventIds.TurnToFacing1, (Seconds)4);
                            _events.ScheduleEvent(EventIds.WavesCompleteText2, (Seconds)13);
                            break;
                        }
                        case EventIds.WavesCompleteText2:
                        {
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetAI().Talk(TextIds.SayNefarius7);
                            Talk(TextIds.SayBlackhand2);
                            _events.ScheduleEvent(EventIds.PathRend, (Seconds)1);
                            _events.ScheduleEvent(EventIds.WavesCompleteText3, (Seconds)4);
                            break;
                        }
                        case EventIds.WavesCompleteText3:
                        {
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetAI().Talk(TextIds.SayNefarius8);
                            _events.ScheduleEvent(EventIds.PathNefarius, (Seconds)1);
                            _events.ScheduleEvent(EventIds.PathRend, (Seconds)1);
                            break;
                        }
                        case EventIds.PathNefarius:
                        {
                            Creature victor = ObjectAccessor.GetCreature(me, victorGUID);
                            if (victor != null)
                                victor.GetMotionMaster().MovePath(MiscConst.NefariusPath1, true);
                            break;
                        }
                        case EventIds.PathRend:
                            me.GetMotionMaster().MovePath(MiscConst.RendPath1, false);
                            break;
                        case EventIds.Teleport1:
                            me.NearTeleportTo(194.2993f, -474.0814f, 121.4505f, -0.01225555f);
                            _events.ScheduleEvent(EventIds.Teleport2, (Seconds)50);
                            break;
                        case EventIds.Teleport2:
                            me.NearTeleportTo(216.485f, -434.93f, 110.888f, -0.01225555f);
                            me.SummonCreature(CreaturesIds.Gyth, 211.762f, -397.5885f, 111.1817f, 4.747295f);
                            break;
                        case EventIds.Wave1:
                        {
                            GameObject portcullis = ObjectAccessor.GetGameObject(me, portcullisGUID);
                            if (portcullis != null)
                                portcullis.UseDoorOrButton();

                            // move wave
                            break;
                        }
                        case EventIds.Wave2:
                        {
                            // spawn wave
                            GameObject portcullis = ObjectAccessor.GetGameObject(me, portcullisGUID);
                            if (portcullis != null)
                                portcullis.UseDoorOrButton();
                            // move wave
                            break;
                        }
                        case EventIds.Wave3:
                        {
                            // spawn wave
                            GameObject portcullis = ObjectAccessor.GetGameObject(me, portcullisGUID);
                            if (portcullis != null)
                                portcullis.UseDoorOrButton();
                            // move wave
                            break;
                        }
                        case EventIds.Wave4:
                        {
                            // spawn wave
                            GameObject portcullis = ObjectAccessor.GetGameObject(me, portcullisGUID);
                            if (portcullis != null)
                                portcullis.UseDoorOrButton();
                            // move wave
                            break;
                        }
                        case EventIds.Wave5:
                        {
                            // spawn wave
                            GameObject portcullis = ObjectAccessor.GetGameObject(me, portcullisGUID);
                            if (portcullis != null)
                                portcullis.UseDoorOrButton();
                            // move wave
                            break;
                        }
                        case EventIds.Wave6:
                        {
                            // spawn wave
                            GameObject portcullis = ObjectAccessor.GetGameObject(me, portcullisGUID);
                            if (portcullis != null)
                                portcullis.UseDoorOrButton();
                            // move wave
                            break;
                        }
                        default:
                            break;
                    }
                });
            }

            if (!UpdateVictim())
                return;

            _events.Update(diff);

            if (me.HasUnitState(UnitState.Casting))
                return;

            _events.ExecuteEvents(eventId =>
            {
                switch (eventId)
                {
                    case EventIds.Whirlwind:
                        DoCast(SpellIds.Whirlwind);
                        _events.ScheduleEvent(EventIds.Whirlwind, Time.SpanFromSeconds(13), Time.SpanFromSeconds(18));
                        break;
                    case EventIds.Cleave:
                        DoCastVictim(SpellIds.Cleave);
                        _events.ScheduleEvent(EventIds.Cleave, Time.SpanFromSeconds(10), Time.SpanFromSeconds(14));
                        break;
                    case EventIds.MortalStrike:
                        DoCastVictim(SpellIds.MortalStrike);
                        _events.ScheduleEvent(EventIds.MortalStrike, Time.SpanFromSeconds(14), Time.SpanFromSeconds(16));
                        break;
                }

                if (me.HasUnitState(UnitState.Casting))
                    return;
            });
        }
    }
}

