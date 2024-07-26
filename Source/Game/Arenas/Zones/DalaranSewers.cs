// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.BattleGrounds;
using Game.Entities;
using System;

namespace Game.Arenas
{
    class DalaranSewersArena : Arena
    {
        TimeSpan _pipeKnockBackTimer;
        int _pipeKnockBackCount;

        public DalaranSewersArena(BattlegroundTemplate battlegroundTemplate) : base(battlegroundTemplate)
        {
            _events = new EventMap();
        }

        public override void StartingEventCloseDoors()
        {
            for (int i = DalaranSewersObjectTypes.Door1; i <= DalaranSewersObjectTypes.Door2; ++i)
                SpawnBGObject(i, BattlegroundConst.RespawnImmediately);
        }

        public override void StartingEventOpenDoors()
        {
            for (int i = DalaranSewersObjectTypes.Door1; i <= DalaranSewersObjectTypes.Door2; ++i)
                DoorOpen(i);

            for (int i = DalaranSewersObjectTypes.Buff1; i <= DalaranSewersObjectTypes.Buff2; ++i)
                SpawnBGObject(i, (Seconds)60);

            _events.ScheduleEvent(DalaranSewersEvents.WaterfallWarning, DalaranSewersData.WaterfallTimerMin, DalaranSewersData.WaterfallTimerMax);
            //_events.ScheduleEvent(DalaranSewersEvents.PipeKnockback, DalaranSewersData.PipeKnockbackFirstDelay);

            _pipeKnockBackCount = 0;
            _pipeKnockBackTimer = DalaranSewersData.PipeKnockbackFirstDelay;

            SpawnBGObject(DalaranSewersObjectTypes.Water2, BattlegroundConst.RespawnImmediately);

            DoorOpen(DalaranSewersObjectTypes.Water1); // Turn off collision
            DoorOpen(DalaranSewersObjectTypes.Water2);

            // Remove effects of Demonic Circle Summon
            foreach (var pair in GetPlayers())
            {
                Player player = _GetPlayer(pair, "BattlegroundDS::StartingEventOpenDoors");
                if (player != null)
                    player.RemoveAurasDueToSpell(DalaranSewersSpells.DemonicCircle);
            }
        }

        public override bool SetupBattleground()
        {
            bool result = true;
            result &= AddObject(DalaranSewersObjectTypes.Door1, DalaranSewersGameObjects.Door1, 1350.95f, 817.2f, 20.8096f, 3.15f, 0, 0, 0.99627f, 0.0862864f, BattlegroundConst.RespawnImmediately);
            result &= AddObject(DalaranSewersObjectTypes.Door2, DalaranSewersGameObjects.Door2, 1232.65f, 764.913f, 20.0729f, 6.3f, 0, 0, 0.0310211f, -0.999519f, BattlegroundConst.RespawnImmediately);
            if (!result)
            {
                Log.outError(LogFilter.Sql, "DalaranSewersArena: Failed to spawn door object!");
                return false;
            }

            // buffs
            result &= AddObject(DalaranSewersObjectTypes.Buff1, DalaranSewersGameObjects.Buff1, 1291.7f, 813.424f, 7.11472f, 4.64562f, 0, 0, 0.730314f, -0.683111f, (Seconds)120);
            result &= AddObject(DalaranSewersObjectTypes.Buff2, DalaranSewersGameObjects.Buff2, 1291.7f, 768.911f, 7.11472f, 1.55194f, 0, 0, 0.700409f, 0.713742f, (Seconds)120);
            if (!result)
            {
                Log.outError(LogFilter.Sql, "DalaranSewersArena: Failed to spawn buff object!");
                return false;
            }

            result &= AddObject(DalaranSewersObjectTypes.Water1, DalaranSewersGameObjects.Water1, 1291.56f, 790.837f, 7.1f, 3.14238f, 0, 0, 0.694215f, -0.719768f, (Seconds)120);
            result &= AddObject(DalaranSewersObjectTypes.Water2, DalaranSewersGameObjects.Water2, 1291.56f, 790.837f, 7.1f, 3.14238f, 0, 0, 0.694215f, -0.719768f, (Seconds)120);
            result &= AddCreature(DalaranSewersData.NpcWaterSpout, DalaranSewersCreatureTypes.WaterfallKnockback, 1292.587f, 790.2205f, 7.19796f, 3.054326f, BattleGroundTeamId.Neutral, BattlegroundConst.RespawnImmediately) != null;
            result &= AddCreature(DalaranSewersData.NpcWaterSpout, DalaranSewersCreatureTypes.PipeKnockback1, 1369.977f, 817.2882f, 16.08718f, 3.106686f, BattleGroundTeamId.Neutral, BattlegroundConst.RespawnImmediately) != null;
            result &= AddCreature(DalaranSewersData.NpcWaterSpout, DalaranSewersCreatureTypes.PipeKnockback2, 1212.833f, 765.3871f, 16.09484f, 0.0f, BattleGroundTeamId.Neutral, BattlegroundConst.RespawnImmediately) != null;
            if (!result)
            {
                Log.outError(LogFilter.Sql, "DalaranSewersArena: Failed to spawn collision object!");
                return false;
            }

            return true;
        }

        public override void PostUpdateImpl(TimeSpan diff)
        {
            if (GetStatus() != BattlegroundStatus.InProgress)
                return;

            _events.ExecuteEvents(eventId =>
            {
                switch (eventId)
                {
                    case DalaranSewersEvents.WaterfallWarning:
                        // Add the water
                        DoorClose(DalaranSewersObjectTypes.Water2);
                        _events.ScheduleEvent(DalaranSewersEvents.WaterfallOn, DalaranSewersData.WaterWarningDuration);
                        break;
                    case DalaranSewersEvents.WaterfallOn:
                        // Active collision and start knockback timer
                        DoorClose(DalaranSewersObjectTypes.Water1);
                        _events.ScheduleEvent(DalaranSewersEvents.WaterfallOff, DalaranSewersData.WaterfallDuration);
                        _events.ScheduleEvent(DalaranSewersEvents.WaterfallKnockback, DalaranSewersData.WaterfallKnockbackTimer);
                        break;
                    case DalaranSewersEvents.WaterfallOff:
                        // Remove collision and water
                        DoorOpen(DalaranSewersObjectTypes.Water1);
                        DoorOpen(DalaranSewersObjectTypes.Water2);
                        _events.CancelEvent(DalaranSewersEvents.WaterfallKnockback);
                        _events.ScheduleEvent(DalaranSewersEvents.WaterfallWarning, DalaranSewersData.WaterfallTimerMin, DalaranSewersData.WaterfallTimerMax);
                        break;
                    case DalaranSewersEvents.WaterfallKnockback:
                    {
                        // Repeat knockback while the waterfall still active
                        Creature waterSpout = GetBGCreature(DalaranSewersCreatureTypes.WaterfallKnockback);
                        if (waterSpout != null)
                            waterSpout.CastSpell(waterSpout, DalaranSewersSpells.WaterSpout, true);
                        _events.ScheduleEvent(eventId, DalaranSewersData.WaterfallKnockbackTimer);
                    }
                    break;
                    case DalaranSewersEvents.PipeKnockback:
                    {
                        for (int i = DalaranSewersCreatureTypes.PipeKnockback1; i <= DalaranSewersCreatureTypes.PipeKnockback2; ++i)
                        {
                            Creature waterSpout = GetBGCreature(i);
                            if (waterSpout != null)
                                waterSpout.CastSpell(waterSpout, DalaranSewersSpells.Flush, true);
                        }
                    }
                    break;
                }
            });

            if (_pipeKnockBackCount < DalaranSewersData.PipeKnockbackTotalCount)
            {
                if (_pipeKnockBackTimer < diff)
                {
                    for (int i = DalaranSewersCreatureTypes.PipeKnockback1; i <= DalaranSewersCreatureTypes.PipeKnockback2; ++i)
                    {
                        Creature waterSpout = GetBGCreature(i);
                        if (waterSpout != null)
                            waterSpout.CastSpell(waterSpout, DalaranSewersSpells.Flush, true);
                    }

                    ++_pipeKnockBackCount;
                    _pipeKnockBackTimer = DalaranSewersData.PipeKnockbackDelay;
                }
                else
                    _pipeKnockBackTimer -= diff;
            }
        }

        public override void SetData(int dataId, int value)
        {
            base.SetData(dataId, value);
            if (dataId == DalaranSewersData.PipeKnockbackCount)
                _pipeKnockBackCount = value;
        }

        public override int GetData(int dataId)
        {
            if (dataId == DalaranSewersData.PipeKnockbackCount)
                return _pipeKnockBackCount;

            return base.GetData(dataId);
        }
    }

    struct DalaranSewersEvents
    {
        public const int WaterfallWarning = 1; // Water starting to fall, but no LoS Blocking nor movement blocking
        public const int WaterfallOn = 2; // LoS and Movement blocking active
        public const int WaterfallOff = 3;
        public const int WaterfallKnockback = 4;

        public const int PipeKnockback = 5;
    }

    struct DalaranSewersObjectTypes
    {
        public const int Door1 = 0;
        public const int Door2 = 1;
        public const int Water1 = 2; // Collision
        public const int Water2 = 3;
        public const int Buff1 = 4;
        public const int Buff2 = 5;
        public const int Max = 6;
    }

    struct DalaranSewersGameObjects
    {
        public const int Door1 = 192642;
        public const int Door2 = 192643;
        public const int Water1 = 194395; // Collision
        public const int Water2 = 191877;
        public const int Buff1 = 184663;
        public const int Buff2 = 184664;
    }

    struct DalaranSewersCreatureTypes
    {
        public const int WaterfallKnockback = 0;
        public const int PipeKnockback1 = 1;
        public const int PipeKnockback2 = 2;
        public const int Max = 3;
    }

    struct DalaranSewersData
    {
        // These values are NOT blizzlike... need the correct data!
        public static TimeSpan WaterfallTimerMin = (Seconds)30;
        public static TimeSpan WaterfallTimerMax = (Seconds)60;
        public static TimeSpan WaterWarningDuration = (Seconds)5;
        public static TimeSpan WaterfallDuration = (Seconds)30;
        public static TimeSpan WaterfallKnockbackTimer = (Milliseconds)1500;

        public static TimeSpan PipeKnockbackFirstDelay = (Seconds)5;
        public static TimeSpan PipeKnockbackDelay = (Seconds)3;
        public const int PipeKnockbackTotalCount = 2;
        public const int PipeKnockbackCount = 1;

        public const int NpcWaterSpout = 28567;
    }

    struct DalaranSewersSpells
    {
        public const int Flush = 57405; // Visual And Target Selector For The Starting Knockback From The Pipe
        public const int FlushKnockback = 61698; // Knockback Effect For Previous Spell (Triggered, Not Needed To Be Cast)
        public const int WaterSpout = 58873; // Knockback Effect Of The Central Waterfall

        public const int DemonicCircle = 48018;  // Demonic Circle Summon
    }
}
