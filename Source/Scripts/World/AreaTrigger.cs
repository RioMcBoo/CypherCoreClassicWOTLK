// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.DataStorage;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using System;
using System.Collections.Generic;

namespace Scripts.World.Areatriggers
{
    [Script]
    class AreaTrigger_at_coilfang_waterfall : AreaTriggerScript
    {
        const int GoCoilfangWaterfall = 184212;

        public AreaTrigger_at_coilfang_waterfall() : base("at_coilfang_waterfall") { }

        public override bool OnTrigger(Player player, AreaTriggerRecord areaTrigger)
        {
            GameObject go = player.FindNearestGameObject(GoCoilfangWaterfall, 35.0f);
            if (go != null)
                if (go.GetLootState() == LootState.Ready)
                    go.UseDoorOrButton();

            return false;
        }
    }

    [Script]
    class AreaTrigger_at_legion_teleporter : AreaTriggerScript
    {
        const int SpellTeleATo = 37387;
        const int QuestGainingAccessA = 10589;

        const int SpellTeleHTo = 37389;
        const int QuestGainingAccessH = 10604;

        public AreaTrigger_at_legion_teleporter() : base("at_legion_teleporter") { }

        public override bool OnTrigger(Player player, AreaTriggerRecord areaTrigger)
        {
            if (player.IsAlive() && !player.IsInCombat())
            {
                if (player.GetTeam() == Team.Alliance && player.GetQuestRewardStatus(QuestGainingAccessA))
                {
                    player.CastSpell(player, SpellTeleATo, false);
                    return true;
                }

                if (player.GetTeam() == Team.Horde && player.GetQuestRewardStatus(QuestGainingAccessH))
                {
                    player.CastSpell(player, SpellTeleHTo, false);
                    return true;
                }

                return false;
            }
            return false;
        }
    }

    [Script]
    class AreaTrigger_at_scent_larkorwi : AreaTriggerScript
    {
        const int QuestScentOfLarkorwi = 4291;
        const int NpcLarkorwiMate = 9683;

        public AreaTrigger_at_scent_larkorwi() : base("at_scent_larkorwi") { }

        public override bool OnTrigger(Player player, AreaTriggerRecord areaTrigger)
        {
            if (!player.IsDead() && player.GetQuestStatus(QuestScentOfLarkorwi) == QuestStatus.Incomplete)
            {
                if (player.FindNearestCreature(NpcLarkorwiMate, 15) == null)
                    player.SummonCreature(NpcLarkorwiMate, player.GetPositionX() + 5, player.GetPositionY(), player.GetPositionZ(), 3.3f, TempSummonType.TimedDespawnOutOfCombat, (Seconds)100);
            }

            return false;
        }
    }

    [Script]
    class AreaTrigger_at_sholazar_waygate : AreaTriggerScript
    {
        const int SpellSholazarToUngoroTeleport = 52056;
        const int SpellUngoroToSholazarTeleport = 52057;

        const int AtSholazar = 5046;
        const int AtUngoro = 5047;

        const int QuestTheMakersOverlook = 12613;
        const int QuestTheMakersPerch = 12559;
        const int QuestMeetingAGreatOne = 13956;

        public AreaTrigger_at_sholazar_waygate() : base("at_sholazar_waygate") { }

        public override bool OnTrigger(Player player, AreaTriggerRecord trigger)
        {
            if (!player.IsDead() && (player.GetQuestStatus(QuestMeetingAGreatOne) != QuestStatus.None ||
                (player.GetQuestStatus(QuestTheMakersOverlook) == QuestStatus.Rewarded && player.GetQuestStatus(QuestTheMakersPerch) == QuestStatus.Rewarded)))
            {
                switch (trigger.Id)
                {
                    case AtSholazar:
                        player.CastSpell(player, SpellSholazarToUngoroTeleport, true);
                        break;

                    case AtUngoro:
                        player.CastSpell(player, SpellUngoroToSholazarTeleport, true);
                        break;
                }
            }

            return false;
        }
    }

    [Script]
    class AreaTrigger_at_nats_landing : AreaTriggerScript
    {
        const int QuestNatsBargain = 11209;
        const int SpellFishPaste = 42644;
        const int NpcLurkingShark = 23928;

        public AreaTrigger_at_nats_landing() : base("at_nats_landing") { }

        public override bool OnTrigger(Player player, AreaTriggerRecord areaTrigger)
        {
            if (!player.IsAlive() || !player.HasAura(SpellFishPaste))
                return false;

            if (player.GetQuestStatus(QuestNatsBargain) == QuestStatus.Incomplete)
            {
                if (player.FindNearestCreature(NpcLurkingShark, 20.0f) == null)
                {
                    Creature shark = player.SummonCreature(NpcLurkingShark, -4246.243f, -3922.356f, -7.488f, 5.0f, TempSummonType.TimedDespawnOutOfCombat, (Seconds)100);
                    if (shark != null)
                        shark.GetAI().AttackStart(player);

                    return false;
                }
            }
            return true;
        }
    }

    [Script]
    class AreaTrigger_at_brewfest : AreaTriggerScript
    {
        const int NpcTapperSwindlekeg = 24711;
        const int NpcIpfelkoferIronkeg = 24710;

        const int AtBrewfestDurotar = 4829;
        const int AtBrewfestDunMorogh = 4820;

        const int SayWelcome = 4;

        static readonly Seconds AreatriggerTalkCooldown = (Seconds)5;

        Dictionary<int, DateTime> _triggerTimes = new();

        public AreaTrigger_at_brewfest() : base("at_brewfest")
        {
            // Initialize for cooldown
            _triggerTimes[AtBrewfestDurotar] = _triggerTimes[AtBrewfestDunMorogh] = Time.Zero;
        }

        public override bool OnTrigger(Player player, AreaTriggerRecord trigger)
        {
            int triggerId = trigger.Id;
            // Second trigger happened too early after first, skip for now
            if (LoopTime.ServerTime - _triggerTimes[triggerId] < AreatriggerTalkCooldown)
                return false;

            switch (triggerId)
            {
                case AtBrewfestDurotar:
                    Creature tapper = player.FindNearestCreature(NpcTapperSwindlekeg, 20.0f);
                    if (tapper != null)
                        tapper.GetAI().Talk(SayWelcome, player);
                    break;
                case AtBrewfestDunMorogh:
                    Creature ipfelkofer = player.FindNearestCreature(NpcIpfelkoferIronkeg, 20.0f);
                    if (ipfelkofer != null)
                        ipfelkofer.GetAI().Talk(SayWelcome, player);
                    break;
                default:
                    break;
            }

            _triggerTimes[triggerId] = LoopTime.ServerTime;
            return false;
        }
    }

    [Script]
    class AreaTrigger_at_area_52_entrance : AreaTriggerScript
    {
        const int SpellA52Neuralyzer = 34400;
        const int NpcSpotlight = 19913;
        static readonly Seconds SummonCooldown = (Seconds)5;

        const int AtArea52South = 4472;
        const int AtArea52North = 4466;
        const int AtArea52West = 4471;
        const int AtArea52East = 4422;

        Dictionary<int, ServerTime> _triggerTimes = new();

        public AreaTrigger_at_area_52_entrance() : base("at_area_52_entrance")
        {
            _triggerTimes[AtArea52South] = _triggerTimes[AtArea52North] = _triggerTimes[AtArea52West] = _triggerTimes[AtArea52East] = ServerTime.Zero;
        }

        public override bool OnTrigger(Player player, AreaTriggerRecord trigger)
        {
            float x = 0.0f, y = 0.0f, z = 0.0f;

            if (!player.IsAlive())
                return false;

            int triggerId = trigger.Id;
            if (LoopTime.ServerTime - _triggerTimes[triggerId] < SummonCooldown)
                return false;

            switch (triggerId)
            {
                case AtArea52East:
                    x = 3044.176f;
                    y = 3610.692f;
                    z = 143.61f;
                    break;
                case AtArea52North:
                    x = 3114.87f;
                    y = 3687.619f;
                    z = 143.62f;
                    break;
                case AtArea52West:
                    x = 3017.79f;
                    y = 3746.806f;
                    z = 144.27f;
                    break;
                case AtArea52South:
                    x = 2950.63f;
                    y = 3719.905f;
                    z = 143.33f;
                    break;
            }

            player.SummonCreature(NpcSpotlight, x, y, z, 0.0f, TempSummonType.TimedDespawn, (Seconds)5);
            player.AddAura(SpellA52Neuralyzer, player);
            _triggerTimes[trigger.Id] = LoopTime.ServerTime;
            return false;
        }
    }

    class AreaTrigger_at_frostgrips_hollow : AreaTriggerScript
    {
        const int QuestTheLonesomeWatcher = 12877;

        const int NpcStormforgedMonitor = 29862;
        const int NpcStormforgedEradictor = 29861;

        Position stormforgedMonitorPosition = new(6963.95f, 45.65f, 818.71f, 4.948f);
        Position stormforgedEradictorPosition = new(6983.18f, 7.15f, 806.33f, 2.228f);

        ObjectGuid stormforgedMonitorGUID;
        ObjectGuid stormforgedEradictorGUID;

        public AreaTrigger_at_frostgrips_hollow() : base("at_frostgrips_hollow")
        {
            stormforgedMonitorGUID.Clear();
            stormforgedEradictorGUID.Clear();
        }

        public override bool OnTrigger(Player player, AreaTriggerRecord areaTrigger)
        {
            if (player.GetQuestStatus(QuestTheLonesomeWatcher) != QuestStatus.Incomplete)
                return false;

            Creature stormforgedMonitor = ObjectAccessor.GetCreature(player, stormforgedMonitorGUID);
            if (stormforgedMonitor != null)
                return false;

            Creature stormforgedEradictor = ObjectAccessor.GetCreature(player, stormforgedEradictorGUID);
            if (stormforgedEradictor != null)
                return false;

            stormforgedMonitor = player.SummonCreature(NpcStormforgedMonitor, stormforgedMonitorPosition, TempSummonType.TimedDespawnOutOfCombat, (Minutes)1);
            if (stormforgedMonitor != null)
            {
                stormforgedMonitorGUID = stormforgedMonitor.GetGUID();
                stormforgedMonitor.SetWalk(false);
                /// The npc would search an alternative way to get to the last waypoint without this unit state.
                stormforgedMonitor.AddUnitState(UnitState.IgnorePathfinding);
                stormforgedMonitor.GetMotionMaster().MovePath((NpcStormforgedMonitor * 100) << 3, false);
            }

            stormforgedEradictor = player.SummonCreature(NpcStormforgedEradictor, stormforgedEradictorPosition, TempSummonType.TimedDespawnOutOfCombat, (Minutes)1);
            if (stormforgedEradictor != null)
            {
                stormforgedEradictorGUID = stormforgedEradictor.GetGUID();
                stormforgedEradictor.GetMotionMaster().MovePath((NpcStormforgedEradictor * 100) << 3, false);
            }

            return true;
        }
    }

    class areatrigger_stormwind_teleport_unit : AreaTriggerAI
    {
        const int SpellDustInTheStormwind = 312593;
        const int NpcKillCreditTeleportStormwind = 160561;

        public areatrigger_stormwind_teleport_unit(AreaTrigger areatrigger) : base(areatrigger) { }

        public override void OnUnitEnter(Unit unit)
        {
            Player player = unit.ToPlayer();
            if (player == null)
                return;

            player.CastSpell(unit, SpellDustInTheStormwind);
            player.KilledMonsterCredit(NpcKillCreditTeleportStormwind);
        }
    }

    class areatrigger_battleground_buffs : AreaTriggerAI
    {
        public areatrigger_battleground_buffs(AreaTrigger areatrigger) : base(areatrigger) { }

        public override void OnUnitEnter(Unit unit)
        {
            if (!unit.IsPlayer())
                return;

            HandleBuffAreaTrigger(unit.ToPlayer());
        }

        void HandleBuffAreaTrigger(Player player)
        {
            GameObject buffObject = player.FindNearestGameObjectWithOptions(4.0f, new FindGameObjectOptions() { StringId = "bg_buff_obj" });
            if (buffObject != null)
            {
                buffObject.ActivateObject(GameObjectActions.Disturb, 0, player);
                buffObject.DespawnOrUnsummon();
            }
        }
    }

    class AreaTrigger_at_battleground_buffs : AreaTriggerScript
    {
        public AreaTrigger_at_battleground_buffs() : base("at_battleground_buffs") { }

        public override bool OnTrigger(Player player, AreaTriggerRecord areaTrigger)
        {
            HandleBuffAreaTrigger(player);
            return true;
        }

        void HandleBuffAreaTrigger(Player player)
        {
            GameObject buffObject = player.FindNearestGameObjectWithOptions(4.0f, new FindGameObjectOptions() { StringId = "bg_buff_obj" });
            if (buffObject != null)
            {
                buffObject.ActivateObject(GameObjectActions.Disturb, 0, player);
                buffObject.DespawnOrUnsummon();
            }
        }
    }

    class areatrigger_action_capture_flag : AreaTriggerAI
    {
        public areatrigger_action_capture_flag(AreaTrigger areatrigger) : base(areatrigger) { }

        public override void OnUnitEnter(Unit unit)
        {
            if (!unit.IsPlayer())
                return;

            Player player = unit.ToPlayer();
            ZoneScript zoneScript = at.GetZoneScript();
            if (zoneScript != null)
                if (zoneScript.CanCaptureFlag(at, player))
                    zoneScript.OnCaptureFlag(at, player);
        }
    }
}