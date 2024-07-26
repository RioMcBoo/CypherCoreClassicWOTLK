// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game;
using Game.AI;
using Game.DataStorage;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.Events.ChildrensWeek
{
    struct TextIds
    {
        public const int OracleOrphan1 = 1;
        public const int OracleOrphan2 = 2;
        public const int OracleOrphan3 = 3;
        public const int OracleOrphan4 = 4;
        public const int OracleOrphan5 = 5;
        public const int OracleOrphan6 = 6;
        public const int OracleOrphan7 = 7;
        public const int OracleOrphan8 = 8;
        public const int OracleOrphan9 = 9;
        public const int OracleOrphan10 = 10;
        public const int OracleOrphan11 = 11;
        public const int OracleOrphan12 = 12;
        public const int OracleOrphan13 = 13;
        public const int OracleOrphan14 = 14;

        public const int WolvarOrphan1 = 1;
        public const int WolvarOrphan2 = 2;
        public const int WolvarOrphan3 = 3;
        public const int WolvarOrphan4 = 4;
        public const int WolvarOrphan5 = 5;
        // 6 - 9 used in Nesingwary script
        public const int WolvarOrphan10 = 10;
        public const int WolvarOrphan11 = 11;
        public const int WolvarOrphan12 = 12;
        public const int WolvarOrphan13 = 13;

        public const int WinterfinPlaymate1 = 1;
        public const int WinterfinPlaymate2 = 2;

        public const int SnowfallGladePlaymate1 = 1;
        public const int SnowfallGladePlaymate2 = 2;

        public const int SooRoo1 = 1;
        public const int ElderKekek1 = 1;

        public const int Alexstrasza2 = 2;
        public const int Krasus8 = 8;
    }

    struct QuestIds
    {
        public const int PlaymateWolvar = 13951;
        public const int PlaymateOracle = 13950;
        public const int TheBiggestTreeEver = 13929;
        public const int TheBronzeDragonshrineOracle = 13933;
        public const int TheBronzeDragonshrineWolvar = 13934;
        public const int MeetingAGreatOne = 13956;
        public const int TheMightyHemetNesingwary = 13957;
        public const int DownAtTheDocks = 910;
        public const int GatewayToTheFrontier = 911;
        public const int BoughtOfEternals = 1479;
        public const int SpookyLighthouse = 1687;
        public const int StonewroughtDam = 1558;
        public const int DarkPortalH = 10951;
        public const int DarkPortalA = 10952;
        public const int LordaeronThroneRoom = 1800;
        public const int AuchindounAndTheRing = 10950;
        public const int TimeToVisitTheCavernsH = 10963;
        public const int TimeToVisitTheCavernsA = 10962;
        public const int TheSeatOfTheNaruu = 10956;
        public const int CallOnTheFarseer = 10968;
        public const int JheelIsAtAerisLanding = 10954;
        public const int HchuuAndTheMushroomPeople = 10945;
        public const int VisitTheThroneOfElements = 10953;
        public const int NowWhenIGrowUp = 11975;
        public const int HomeOfTheBearMen = 13930;
        public const int TheDragonQueenOracle = 13954;
        public const int TheDragonQueenWolvar = 13955;
    }

    struct AreatriggerIds
    {
        public const int DownAtTheDocks = 3551;
        public const int GatewayToTheFrontier = 3549;
        public const int LordaeronThroneRoom = 3547;
        public const int BoughtOfEternals = 3546;
        public const int SpookyLighthouse = 3552;
        public const int StonewroughtDam = 3548;
        public const int DarkPortal = 4356;
    }

    struct CreatureIds
    {
        public const int OrphanOracle = 33533;
        public const int OrphanWolvar = 33532;
        public const int OrphanBloodElf = 22817;
        public const int OrphanDraenei = 22818;
        public const int OrphanHuman = 14305;
        public const int OrphanOrcish = 14444;

        public const int CavernsOfTimeCwTrigger = 22872;
        public const int Exodar01CwTrigger = 22851;
        public const int Exodar02CwTrigger = 22905;
        public const int AerisLandingCwTrigger = 22838;
        public const int AuchindounCwTrigger = 22831;
        public const int SporeggarCwTrigger = 22829;
        public const int ThroneOfElementsCwTrigger = 22839;
        public const int Silvermoon01CwTrigger = 22866;
        public const int Krasus = 27990;
    }

    struct Misc
    {
        public const int SpellSnowball = 21343;
        public const int SpellOrphanOut = 58818;

        public const int DisplayInvisible = 11686;

        public static ObjectGuid GetOrphanGUID(Player player, int orphan)
        {
            Aura orphanOut = player.GetAura(SpellOrphanOut);
            if (orphanOut != null)
                if (orphanOut.GetCaster() != null && orphanOut.GetCaster().GetEntry() == orphan)
                    return orphanOut.GetCaster().GetGUID();

            return ObjectGuid.Empty;
        }
    }

    [Script]
    class npc_winterfin_playmate : ScriptedAI
    {
        bool working;
        ObjectGuid orphanGUID;

        public npc_winterfin_playmate(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            working = false;
            orphanGUID.Clear();
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (!working && who != null && who.GetDistance2d(me) < 10.0f)
            {
                Player player = who.ToPlayer();
                if (player == null)
                {
                    Reset();
                    return;
                }

                if (player.GetQuestStatus(QuestIds.PlaymateOracle) == QuestStatus.Incomplete)
                {
                    orphanGUID = Misc.GetOrphanGUID(player, CreatureIds.OrphanOracle);
                    if (!orphanGUID.IsEmpty())
                    {
                        Creature orphan = ObjectAccessor.GetCreature(me, orphanGUID);
                        if (orphan == null)
                        {
                            Reset();
                            return;
                        }

                        working = true;

                        _scheduler.Schedule(Time.SpanFromSeconds(0), _ =>
                        {
                            orphan.GetMotionMaster().MovePoint(0, me.GetPositionX() + MathF.Cos(me.GetOrientation()) * 5, me.GetPositionY() + MathF.Sin(me.GetOrientation()) * 5, me.GetPositionZ());
                            orphan.GetAI().Talk(TextIds.OracleOrphan1);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(3), _ =>
                        {
                            orphan.SetFacingToObject(me);
                            Talk(TextIds.WinterfinPlaymate1);
                            me.HandleEmoteCommand(Emote.StateDance);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(6), _ => orphan.GetAI().Talk(TextIds.OracleOrphan2));

                        _scheduler.Schedule(Time.SpanFromSeconds(9), _ => Talk(TextIds.WinterfinPlaymate2));

                        _scheduler.Schedule(Time.SpanFromSeconds(14), _ =>
                        {
                            orphan.GetAI().Talk(TextIds.OracleOrphan3);
                            me.HandleEmoteCommand(Emote.StateNone);
                            player.GroupEventHappens(QuestIds.PlaymateOracle, me);
                            orphan.GetMotionMaster().MoveFollow(player, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
                            Reset();
                        });
                    }
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }
    }

    class npc_snowfall_glade_playmate : ScriptedAI
    {
        bool working;
        ObjectGuid orphanGUID;

        public npc_snowfall_glade_playmate(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            working = false;
            orphanGUID.Clear();
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (!working && who != null && who.GetDistance2d(me) < 10.0f)
            {
                Player player = who.ToPlayer();
                if (player == null)
                {
                    Reset();
                    return;
                }

                if (player.GetQuestStatus(QuestIds.PlaymateWolvar) == QuestStatus.Incomplete)
                {
                    orphanGUID = Misc.GetOrphanGUID(player, CreatureIds.OrphanWolvar);
                    if (!orphanGUID.IsEmpty())
                    {
                        Creature orphan = ObjectAccessor.GetCreature(me, orphanGUID);
                        if (orphan == null)
                        {
                            Reset();
                            return;
                        }

                        working = true;

                        _scheduler.Schedule(Time.SpanFromSeconds(0), _ =>
                        {
                            orphan.GetMotionMaster().MovePoint(0, me.GetPositionX() + MathF.Cos(me.GetOrientation()) * 5, me.GetPositionY() + MathF.Sin(me.GetOrientation()) * 5, me.GetPositionZ());
                            orphan.GetAI().Talk(TextIds.WolvarOrphan1);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(5), _ =>
                        {
                            orphan.SetFacingToObject(me);
                            Talk(TextIds.SnowfallGladePlaymate1);
                            DoCast(orphan, Misc.SpellSnowball);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(10), _ => Talk(TextIds.SnowfallGladePlaymate2));

                        _scheduler.Schedule(Time.SpanFromSeconds(15), _ =>
                        {
                            orphan.GetAI().Talk(TextIds.WolvarOrphan2);
                            orphan.CastSpell(me, Misc.SpellSnowball);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(20), _ =>
                        {
                            orphan.GetAI().Talk(TextIds.WolvarOrphan3);
                            player.GroupEventHappens(QuestIds.PlaymateWolvar, me);
                            orphan.GetMotionMaster().MoveFollow(player, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
                            Reset();
                        });
                    }
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }
    }

    class npc_the_biggest_tree : ScriptedAI
    {
        bool working;
        ObjectGuid orphanGUID;

        public npc_the_biggest_tree(Creature creature) : base(creature)
        {
            Initialize();
            me.SetDisplayId(Misc.DisplayInvisible);
        }

        void Initialize()
        {
            working = false;
            orphanGUID.Clear();
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (!working && who != null && who.GetDistance2d(me) < 10.0f)
            {
                Player player = who.ToPlayer();
                if (player == null)
                {
                    Reset();
                    return;
                }

                if (player.GetQuestStatus(QuestIds.TheBiggestTreeEver) == QuestStatus.Incomplete)
                {
                    orphanGUID = Misc.GetOrphanGUID(player, CreatureIds.OrphanOracle);
                    if (!orphanGUID.IsEmpty())
                    {
                        Creature orphan = ObjectAccessor.GetCreature(me, orphanGUID);
                        if (orphan == null)
                        {
                            Reset();
                            return;
                        }

                        working = true;

                        _scheduler.Schedule(Time.SpanFromSeconds(0), _ => orphan.GetMotionMaster().MovePoint(0, me.GetPositionX() + MathF.Cos(me.GetOrientation()) * 5, me.GetPositionY() + MathF.Sin(me.GetOrientation()) * 5, me.GetPositionZ()));

                        _scheduler.Schedule(Time.SpanFromSeconds(2), _ =>
                        {
                            orphan.SetFacingToObject(me);
                            orphan.GetAI().Talk(TextIds.OracleOrphan4);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(7), _ =>
                        {
                            player.GroupEventHappens(QuestIds.TheBiggestTreeEver, me);
                            orphan.GetMotionMaster().MoveFollow(player, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
                            Reset();
                        });
                    }
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }
    }

    class npc_high_oracle_soo_roo : ScriptedAI
    {
        bool working;
        ObjectGuid orphanGUID;

        public npc_high_oracle_soo_roo(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            working = false;
            orphanGUID.Clear();
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (!working && who != null && who.GetDistance2d(me) < 10.0f)
            {
                Player player = who.ToPlayer();
                if (player == null)
                {
                    Reset();
                    return;
                }

                if (player.GetQuestStatus(QuestIds.TheBronzeDragonshrineOracle) == QuestStatus.Incomplete)
                {
                    orphanGUID = Misc.GetOrphanGUID(player, CreatureIds.OrphanOracle);
                    if (!orphanGUID.IsEmpty())
                    {
                        Creature orphan = ObjectAccessor.GetCreature(me, orphanGUID);
                        if (orphan == null)
                        {
                            Reset();
                            return;
                        }

                        working = true;

                        _scheduler.Schedule(Time.SpanFromSeconds(0), _ =>
                        {
                            orphan.GetMotionMaster().MovePoint(0, me.GetPositionX() + MathF.Cos(me.GetOrientation()) * 5, me.GetPositionY() + MathF.Sin(me.GetOrientation()) * 5, me.GetPositionZ());
                            orphan.GetAI().Talk(TextIds.OracleOrphan5);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(3), _ =>
                        {
                            orphan.SetFacingToObject(me);
                            Talk(TextIds.SooRoo1);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(9), _ =>
                        {
                            orphan.GetAI().Talk(TextIds.OracleOrphan6);
                            player.GroupEventHappens(QuestIds.TheBronzeDragonshrineOracle, me);
                            orphan.GetMotionMaster().MoveFollow(player, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
                            Reset();
                        });
                    }
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }
    }

    class npc_elder_kekek : ScriptedAI
    {
        bool working;
        ObjectGuid orphanGUID;

        public npc_elder_kekek(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            working = false;
            orphanGUID.Clear();
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (!working && who != null && who.GetDistance2d(me) < 10.0f)
            {
                Player player = who.ToPlayer();
                if (player == null)
                {
                    Reset();
                    return;
                }

                if (player.GetQuestStatus(QuestIds.TheBronzeDragonshrineWolvar) == QuestStatus.Incomplete)
                {
                    orphanGUID = Misc.GetOrphanGUID(player, CreatureIds.OrphanWolvar);
                    if (!orphanGUID.IsEmpty())
                    {
                        Creature orphan = ObjectAccessor.GetCreature(me, orphanGUID);
                        if (orphan == null)
                        {
                            Reset();
                            return;
                        }

                        working = true;

                        _scheduler.Schedule(Time.SpanFromSeconds(0), _ =>
                        {
                            orphan.GetMotionMaster().MovePoint(0, me.GetPositionX() + MathF.Cos(me.GetOrientation()) * 5, me.GetPositionY() + MathF.Sin(me.GetOrientation()) * 5, me.GetPositionZ());
                            orphan.GetAI().Talk(TextIds.WolvarOrphan4);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(3), _ => Talk(TextIds.ElderKekek1));

                        _scheduler.Schedule(Time.SpanFromSeconds(9), _ =>
                        {
                            orphan.GetAI().Talk(TextIds.WolvarOrphan5);
                            player.GroupEventHappens(QuestIds.TheBronzeDragonshrineWolvar, me);
                            orphan.GetMotionMaster().MoveFollow(player, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
                            Reset();
                        });
                    }
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }
    }

    class npc_the_etymidian : ScriptedAI
    {
        const int SayActivation = 0;
        const int QuestTheActivationRune = 12547;

        bool working;
        ObjectGuid orphanGUID;

        public npc_the_etymidian(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            working = false;
            orphanGUID.Clear();
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void OnQuestReward(Player player, Quest quest, LootItemType type, int opt)
        {
            if (quest.Id != QuestTheActivationRune)
                return;

            Talk(SayActivation);
        }

        // doesn't trigger if creature is stunned. Restore aura 25900 when it will be possible or
        // find another way to start event(from orphan script)
        public override void MoveInLineOfSight(Unit who)
        {
            if (!working && who != null && who.GetDistance2d(me) < 10.0f)
            {
                Player player = who.ToPlayer();
                if (player == null)
                {
                    Reset();
                    return;
                }

                if (player.GetQuestStatus(QuestIds.MeetingAGreatOne) == QuestStatus.Incomplete)
                {
                    orphanGUID = Misc.GetOrphanGUID(player, CreatureIds.OrphanOracle);
                    if (!orphanGUID.IsEmpty())
                    {
                        Creature orphan = ObjectAccessor.GetCreature(me, orphanGUID);
                        if (orphan == null)
                        {
                            Reset();
                            return;
                        }

                        working = true;

                        _scheduler.Schedule(Time.SpanFromSeconds(0), _ =>
                        {
                            orphan.GetMotionMaster().MovePoint(0, me.GetPositionX() + MathF.Cos(me.GetOrientation()) * 5, me.GetPositionY() + MathF.Sin(me.GetOrientation()) * 5, me.GetPositionZ());
                            orphan.GetAI().Talk(TextIds.OracleOrphan7);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(5), _ =>
                        {
                            orphan.SetFacingToObject(me);
                            orphan.GetAI().Talk(TextIds.OracleOrphan8);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(10), _ => orphan.GetAI().Talk(TextIds.OracleOrphan9));

                        _scheduler.Schedule(Time.SpanFromSeconds(15), _ => orphan.GetAI().Talk(TextIds.OracleOrphan10));

                        _scheduler.Schedule(Time.SpanFromSeconds(20), _ =>
                        {
                            orphan.GetMotionMaster().MoveFollow(player, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
                            player.GroupEventHappens(QuestIds.MeetingAGreatOne, me);
                            Reset();
                        });
                    }
                }

            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }
    }

    class npc_alexstraza_the_lifebinder : ScriptedAI
    {
        bool working;
        ObjectGuid orphanGUID;

        public npc_alexstraza_the_lifebinder(Creature creature) : base(creature)
        {
            Initialize();
        }

        void Initialize()
        {
            working = false;
            orphanGUID.Clear();
        }

        public override void Reset()
        {
            Initialize();
        }

        public override void SetData(int type, int data)
        {
            // Existing SmartAI
            if (type == 0)
            {
                switch (data)
                {
                    case 1:
                        me.SetOrientation(1.6049f);
                        break;
                    case 2:
                        me.SetOrientation(me.GetHomePosition().GetOrientation());
                        break;
                }
            }
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (!working && who != null && who.GetDistance2d(me) < 10.0f)
            {
                Player player = who.ToPlayer();
                if (player == null)
                {
                    Reset();
                    return;
                }

                if (player.GetQuestStatus(QuestIds.TheDragonQueenOracle) == QuestStatus.Incomplete)
                {
                    orphanGUID = Misc.GetOrphanGUID(player, CreatureIds.OrphanOracle);
                    if (!orphanGUID.IsEmpty())
                    {
                        Creature orphan = ObjectAccessor.GetCreature(me, orphanGUID);
                        if (orphan == null)
                        {
                            Reset();
                            return;
                        }

                        working = true;

                        _scheduler.Schedule(Time.SpanFromSeconds(0), _ =>
                        {
                            orphan.GetMotionMaster().MovePoint(0, me.GetPositionX() + MathF.Cos(me.GetOrientation()) * 5, me.GetPositionY() + MathF.Sin(me.GetOrientation()) * 5, me.GetPositionZ());
                            orphan.GetAI().Talk(TextIds.OracleOrphan11);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(5), _ =>
                        {
                            orphan.SetFacingToObject(me);
                            orphan.GetAI().Talk(TextIds.OracleOrphan12);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(10), _ => orphan.GetAI().Talk(TextIds.OracleOrphan13));

                        _scheduler.Schedule(Time.SpanFromSeconds(15), _ =>
                        {
                            Talk(TextIds.Alexstrasza2);
                            me.SetStandState(UnitStandStateType.Kneel);
                            me.SetFacingToObject(orphan);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(20), _ => orphan.GetAI().Talk(TextIds.OracleOrphan14));

                        _scheduler.Schedule(Time.SpanFromSeconds(25), _ =>
                        {
                            me.SetStandState(UnitStandStateType.Stand);
                            me.SetOrientation(me.GetHomePosition().GetOrientation());
                            player.GroupEventHappens(QuestIds.TheDragonQueenOracle, me);
                            orphan.GetMotionMaster().MoveFollow(player, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
                            Reset();
                            return;
                        });
                    }
                }
                else if (player.GetQuestStatus(QuestIds.TheDragonQueenWolvar) == QuestStatus.Incomplete)
                {
                    orphanGUID = Misc.GetOrphanGUID(player, CreatureIds.OrphanWolvar);
                    if (!orphanGUID.IsEmpty())
                    {
                        Creature orphan = ObjectAccessor.GetCreature(me, orphanGUID);
                        if (orphan == null)
                        {
                            Reset();
                            return;
                        }

                        working = true;

                        _scheduler.Schedule(Time.SpanFromSeconds(0), _ =>
                        {
                            orphan.GetMotionMaster().MovePoint(0, me.GetPositionX() + MathF.Cos(me.GetOrientation()) * 5, me.GetPositionY() + MathF.Sin(me.GetOrientation()) * 5, me.GetPositionZ());
                            orphan.GetAI().Talk(TextIds.WolvarOrphan11);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(5), _ =>
                        {
                            Creature krasus = me.FindNearestCreature(CreatureIds.Krasus, 10.0f);
                            if (krasus != null)
                            {
                                orphan.SetFacingToObject(krasus);
                                krasus.GetAI().Talk(TextIds.Krasus8);
                            }
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(10), _ => orphan.GetAI().Talk(TextIds.WolvarOrphan12));

                        _scheduler.Schedule(Time.SpanFromSeconds(15), _ =>
                        {
                            orphan.SetFacingToObject(me);
                            Talk(TextIds.Alexstrasza2);
                        });

                        _scheduler.Schedule(Time.SpanFromSeconds(20), _ => orphan.GetAI().Talk(TextIds.WolvarOrphan13));

                        _scheduler.Schedule(Time.SpanFromSeconds(25), _ =>
                        {
                            player.GroupEventHappens(QuestIds.TheDragonQueenWolvar, me);
                            orphan.GetMotionMaster().MoveFollow(player, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
                            Reset();
                            return;
                        });
                    }
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            _scheduler.Update(diff);
        }
    }

    class at_bring_your_orphan_to : AreaTriggerScript
    {
        public at_bring_your_orphan_to() : base("at_bring_your_orphan_to") { }

        public override bool OnTrigger(Player player, AreaTriggerRecord trigger)
        {
            if (player.IsDead() || !player.HasAura(Misc.SpellOrphanOut))
                return false;

            int questId = 0;
            int orphanId = 0;

            switch (trigger.Id)
            {
                case AreatriggerIds.DownAtTheDocks:
                    questId = QuestIds.DownAtTheDocks;
                    orphanId = CreatureIds.OrphanOrcish;
                    break;
                case AreatriggerIds.GatewayToTheFrontier:
                    questId = QuestIds.GatewayToTheFrontier;
                    orphanId = CreatureIds.OrphanOrcish;
                    break;
                case AreatriggerIds.LordaeronThroneRoom:
                    questId = QuestIds.LordaeronThroneRoom;
                    orphanId = CreatureIds.OrphanOrcish;
                    break;
                case AreatriggerIds.BoughtOfEternals:
                    questId = QuestIds.BoughtOfEternals;
                    orphanId = CreatureIds.OrphanHuman;
                    break;
                case AreatriggerIds.SpookyLighthouse:
                    questId = QuestIds.SpookyLighthouse;
                    orphanId = CreatureIds.OrphanHuman;
                    break;
                case AreatriggerIds.StonewroughtDam:
                    questId = QuestIds.StonewroughtDam;
                    orphanId = CreatureIds.OrphanHuman;
                    break;
                case AreatriggerIds.DarkPortal:
                    questId = player.GetTeam() == Team.Alliance ? QuestIds.DarkPortalA : QuestIds.DarkPortalH;
                    orphanId = player.GetTeam() == Team.Alliance ? CreatureIds.OrphanDraenei : CreatureIds.OrphanBloodElf;
                    break;
            }

            if (questId != 0 && orphanId != 0 && !Misc.GetOrphanGUID(player, orphanId).IsEmpty() && player.GetQuestStatus(questId) == QuestStatus.Incomplete)
                player.AreaExploredOrEventHappens(questId);

            return true;
        }
    }

    class npc_cw_area_trigger : ScriptedAI
    {
        public npc_cw_area_trigger(Creature creature) : base(creature)
        {
            me.SetDisplayId(Misc.DisplayInvisible);
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (who != null && me.GetDistance2d(who) < 20.0f)
            {
                Player player = who.ToPlayer();
                if (player != null && player.HasAura(Misc.SpellOrphanOut))
                {
                    int questId = 0;
                    int orphanId = 0;
                    switch (me.GetEntry())
                    {
                        case CreatureIds.CavernsOfTimeCwTrigger:
                            questId = player.GetTeam() == Team.Alliance ? QuestIds.TimeToVisitTheCavernsA : QuestIds.TimeToVisitTheCavernsH;
                            orphanId = player.GetTeam() == Team.Alliance ? CreatureIds.OrphanDraenei : CreatureIds.OrphanBloodElf;
                            break;
                        case CreatureIds.Exodar01CwTrigger:
                            questId = QuestIds.TheSeatOfTheNaruu;
                            orphanId = CreatureIds.OrphanDraenei;
                            break;
                        case CreatureIds.Exodar02CwTrigger:
                            questId = QuestIds.CallOnTheFarseer;
                            orphanId = CreatureIds.OrphanDraenei;
                            break;
                        case CreatureIds.AerisLandingCwTrigger:
                            questId = QuestIds.JheelIsAtAerisLanding;
                            orphanId = CreatureIds.OrphanDraenei;
                            break;
                        case CreatureIds.AuchindounCwTrigger:
                            questId = QuestIds.AuchindounAndTheRing;
                            orphanId = CreatureIds.OrphanDraenei;
                            break;
                        case CreatureIds.SporeggarCwTrigger:
                            questId = QuestIds.HchuuAndTheMushroomPeople;
                            orphanId = CreatureIds.OrphanBloodElf;
                            break;
                        case CreatureIds.ThroneOfElementsCwTrigger:
                            questId = QuestIds.VisitTheThroneOfElements;
                            orphanId = CreatureIds.OrphanBloodElf;
                            break;
                        case CreatureIds.Silvermoon01CwTrigger:
                            if (player.GetQuestStatus(QuestIds.NowWhenIGrowUp) == QuestStatus.Incomplete && !Misc.GetOrphanGUID(player, CreatureIds.OrphanBloodElf).IsEmpty())
                            {
                                player.AreaExploredOrEventHappens(QuestIds.NowWhenIGrowUp);
                                if (player.GetQuestStatus(QuestIds.NowWhenIGrowUp) == QuestStatus.Complete)
                                {
                                    Creature samuro = me.FindNearestCreature(25151, 20.0f);
                                    if (samuro != null)
                                        samuro.HandleEmoteCommand(RandomHelper.RAND(Emote.OneshotWave, Emote.OneshotRoar, Emote.OneshotFlex, Emote.OneshotSalute, Emote.OneshotDance));
                                }
                            }
                            break;
                    }
                    if (questId != 0 && orphanId != 0 && !Misc.GetOrphanGUID(player, orphanId).IsEmpty() && player.GetQuestStatus(questId) == QuestStatus.Incomplete)
                        player.AreaExploredOrEventHappens(questId);
                }

            }
        }
    }

    class npc_grizzlemaw_cw_trigger : ScriptedAI
    {
        public npc_grizzlemaw_cw_trigger(Creature creature) : base(creature)
        {
            me.SetDisplayId(Misc.DisplayInvisible);
        }

        public override void MoveInLineOfSight(Unit who)
        {
            if (who != null && who.GetDistance2d(me) < 10.0f)
            {
                Player player = who.ToPlayer();
                if (player != null)
                {
                    if (player.GetQuestStatus(QuestIds.HomeOfTheBearMen) == QuestStatus.Incomplete)
                    {
                        Creature orphan = ObjectAccessor.GetCreature(me, Misc.GetOrphanGUID(player, CreatureIds.OrphanWolvar));
                        if (orphan != null)
                        {
                            player.AreaExploredOrEventHappens(QuestIds.HomeOfTheBearMen);
                            orphan.GetAI().Talk(TextIds.WolvarOrphan10);
                        }
                    }
                }
            }
        }
    }
}