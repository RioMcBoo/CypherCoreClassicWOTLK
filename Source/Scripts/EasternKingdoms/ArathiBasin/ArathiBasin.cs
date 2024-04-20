// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.ArathiBasin
{
    // 150513 - Arathor Gryphon Rider
    [Script]
    class npc_bg_ab_arathor_gryphon_rider_leader : ScriptedAI
    {
        private const int scriptedPath = 800000059;

        public npc_bg_ab_arathor_gryphon_rider_leader(Creature creature) : base(creature) {}

        public override void WaypointPathEnded(int nodeId, int pathId)
        {
            if (pathId != scriptedPath)
                return;

            // despawn formation group
            var followers = me.GetCreatureListWithEntryInGrid(me.GetEntry());
            foreach (var follower in followers)
                follower.DespawnOrUnsummon(new TimeSpan(500));

            me.DespawnOrUnsummon(new TimeSpan(500));
        }
    }

    // 150459 - Defiler Bat Rider
    [Script]
    class npc_bg_ab_defiler_bat_rider_leader : ScriptedAI
    {
        private const int scriptedPath = 800000058;

        public npc_bg_ab_defiler_bat_rider_leader(Creature creature) : base(creature) { }    

        public override void WaypointPathEnded(int nodeId, int pathId)
        {
            if (pathId != scriptedPath)
                return;

            // despawn formation group
            var followers = me.GetCreatureListWithEntryInGrid(me.GetEntry());
            foreach (var follower in followers)
                follower.DespawnOrUnsummon(new TimeSpan(500));

            me.DespawnOrUnsummon(new TimeSpan(500));
        }
    }

    // 261985 - Blacksmith Working
    [Script]
    class spell_bg_ab_blacksmith_working : AuraScript
    {
        const int ITEM_BLACKSMITH_HAMMER = 5956;

        void OnApply(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            GetTarget().SetVirtualItem(0, ITEM_BLACKSMITH_HAMMER);
        }

        void OnRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            if (GetTarget().ToCreature() is Creature creature)
                creature.LoadEquipment(creature.GetOriginalEquipmentId());
        }

        public override void Register()
        {
            OnEffectApply.Add(new(OnApply, 0, AuraType.Dummy, AuraEffectHandleModes.Real));
            OnEffectRemove.Add(new(OnRemove, 0, AuraType.Dummy, AuraEffectHandleModes.Real));
        }
    }
}