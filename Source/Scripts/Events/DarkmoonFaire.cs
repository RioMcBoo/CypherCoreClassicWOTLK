// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.AI;
using Game.DataStorage;
using Game.Entities;
using Game.Scripting;
using System;

namespace Scripts.Events.DarkmoonFaire
{
    struct GossipIds
    {
        public const int MenuSelinaPois = 13076;
        public const int MenuSelinaItem = 13113;

        public const int MenuOptionTonkArenaPoi = 0;
        public const int MenuOptionCannonPoi = 1;
        public const int MenuOptionWhackAGnollPoi = 2;
        public const int MenuOptionRingTossPoi = 3;
        public const int MenuOptionShootingGalleryPoi = 4;
        public const int MenuOptionFortuneTellerPoi = 5;
    }

    struct PoiIds
    {
        public const int WhackAGnoll = 2716;
        public const int Cannon = 2717;
        public const int ShootingGallery = 2718;
        public const int TonkArena = 2719;
        public const int FortuneTeller = 2720;
        public const int RingToss = 2721;
    }

    [Script] // 10445 - Selina Dourman
    class npc_selina_dourman : ScriptedAI
    {
        const int SpellReplaceDarkmoonAdventuresGuide = 103413;
        const int SayWelcome = 0;

        bool _talkCooldown;

        public npc_selina_dourman(Creature creature) : base(creature) { }

        public override bool OnGossipSelect(Player player, int menuId, int gossipListId)
        {
            switch (menuId)
            {
                case GossipIds.MenuSelinaPois:
                {
                    int poiId = 0;
                    switch (gossipListId)
                    {
                        case GossipIds.MenuOptionTonkArenaPoi:
                            poiId = PoiIds.TonkArena;
                            break;
                        case GossipIds.MenuOptionCannonPoi:
                            poiId = PoiIds.Cannon;
                            break;
                        case GossipIds.MenuOptionWhackAGnollPoi:
                            poiId = PoiIds.WhackAGnoll;
                            break;
                        case GossipIds.MenuOptionRingTossPoi:
                            poiId = PoiIds.RingToss;
                            break;
                        case GossipIds.MenuOptionShootingGalleryPoi:
                            poiId = PoiIds.ShootingGallery;
                            break;
                        case GossipIds.MenuOptionFortuneTellerPoi:
                            poiId = PoiIds.FortuneTeller;
                            break;
                        default:
                            break;
                    }
                    if (poiId != 0)
                        player.PlayerTalkClass.SendPointOfInterest(poiId);
                    break;
                }
                case GossipIds.MenuSelinaItem:
                    me.CastSpell(player, SpellReplaceDarkmoonAdventuresGuide);
                    player.CloseGossipMenu();
                    break;
                default:
                    break;
            }

            return false;
        }

        public void DoWelcomeTalk(Unit talkTarget)
        {
            if (talkTarget == null || _talkCooldown)
                return;

            _talkCooldown = true;
            _scheduler.Schedule(TimeSpan.FromSeconds(30), _ => _talkCooldown = false);
            Talk(SayWelcome, talkTarget);
        }

        public override void UpdateAI(uint diff)
        {
            _scheduler.Update(diff);
        }
    }

    [Script] // 7016 - Darkmoon Faire Entrance
    class at_darkmoon_faire_entrance : AreaTriggerScript
    {
        const int NpcSelinaDourman = 10445;

        public at_darkmoon_faire_entrance() : base("at_darkmoon_faire_entrance") { }

        public override bool OnTrigger(Player player, AreaTriggerRecord areaTrigger)
        {
            Creature selinaDourman = player.FindNearestCreature(NpcSelinaDourman, 50.0f);
            if (selinaDourman != null)
            {
                npc_selina_dourman selinaDourmanAI = selinaDourman.GetAI<npc_selina_dourman>();
                if (selinaDourmanAI != null)
                    selinaDourmanAI.DoWelcomeTalk(player);
            }

            return true;
        }
    }
}