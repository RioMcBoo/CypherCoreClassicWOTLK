﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Framework.IO;
using Game.DataStorage;
using Game.Entities;
using Game.Maps;
using Game.Movement;
using System;
using System.Collections.Generic;
using System.Linq;
using Framework.Collections;
using Game.Loots;

namespace Game.Chat
{
    [CommandGroup("npc")]
    class NPCCommands
    {
        [Command("despawngroup", RBACPermissions.CommandNpcDespawngroup)]
        static bool HandleNpcDespawnGroup(CommandHandler handler, string[] opts)
        {
            if (opts.Empty())
                return false;

            bool deleteRespawnTimes = false;
            int groupId = 0;

            // Decode arguments
            foreach (var variant in opts)
            {
                if (!int.TryParse(variant, out groupId))
                    deleteRespawnTimes = true;
            }

            Player player = handler.GetSession().GetPlayer();
            if (!player.GetMap().SpawnGroupDespawn(groupId, deleteRespawnTimes, out int despawnedCount))
            {
                handler.SendSysMessage(CypherStrings.SpawngroupBadgroup, groupId);
                return false;
            }
            handler.SendSysMessage($"Despawned a total of {despawnedCount} objects.");

            return true;
        }

        [Command("evade", RBACPermissions.CommandNpcEvade)]
        static bool HandleNpcEvadeCommand(CommandHandler handler, EvadeReason? why, string force)
        {
            Creature creatureTarget = handler.GetSelectedCreature();
            if (creatureTarget == null || creatureTarget.IsPet())
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            if (creatureTarget.IsAIEnabled())
            {
                handler.SendSysMessage(CypherStrings.CreatureNotAiEnabled);
                return false;
            }

            if (force.Equals("force", StringComparison.OrdinalIgnoreCase))
                creatureTarget.ClearUnitState(UnitState.Evade);
            creatureTarget.GetAI().EnterEvadeMode(why.GetValueOrDefault(EvadeReason.Other));

            return true;
        }

        [Command("info", RBACPermissions.CommandNpcInfo)]
        static bool HandleNpcInfoCommand(CommandHandler handler)
        {
            Creature target = handler.GetSelectedCreature();
            if (target == null)
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            CreatureTemplate cInfo = target.GetCreatureTemplate();

            int faction = target.GetFaction();

            NPCFlags1 npcflags1 = (NPCFlags1)target.m_unitData.NpcFlags[0];
            NPCFlags2 npcflags2 = (NPCFlags2)target.m_unitData.NpcFlags[1];
            ulong npcflags = (uint)npcflags1 | ((uint)npcflags2 << 32);

            ulong mechanicImmuneMask = cInfo.MechanicImmuneMask;
            int displayid = target.GetDisplayId();
            int nativeid = target.GetNativeDisplayId();
            int entry = target.GetEntry();

            TimeSpan curRespawnDelay = target.GetRespawnCompatibilityMode() 
                ? target.GetRespawnTimeEx() - LoopTime.ServerTime 
                : target.GetMap().GetCreatureRespawnTime(target.GetSpawnId()) - LoopTime.ServerTime;

            if (curRespawnDelay < TimeSpan.Zero)
                curRespawnDelay = TimeSpan.Zero;

            string curRespawnDelayStr = Time.SpanToTimeString(curRespawnDelay, TimeFormat.ShortText);
            string defRespawnDelayStr = Time.SpanToTimeString(target.GetRespawnDelay(), TimeFormat.ShortText);

            handler.SendSysMessage(CypherStrings.NpcinfoChar, target.GetName(), target.GetSpawnId(), target.GetGUID().ToString(), entry, faction, npcflags, displayid, nativeid);
            if (target.GetCreatureData() != null && target.GetCreatureData().spawnGroupData.groupId != 0)
            {
                SpawnGroupTemplateData groupData = target.GetCreatureData().spawnGroupData;
                handler.SendSysMessage(CypherStrings.SpawninfoGroupId, groupData.name, groupData.groupId, groupData.flags, target.GetMap().IsSpawnGroupActive(groupData.groupId));
            }
            handler.SendSysMessage(CypherStrings.SpawninfoCompatibilityMode, target.GetRespawnCompatibilityMode());
            handler.SendSysMessage(CypherStrings.NpcinfoLevel, target.GetLevel());
            handler.SendSysMessage(CypherStrings.NpcinfoEquipment, target.GetCurrentEquipmentId(), target.GetOriginalEquipmentId());
            handler.SendSysMessage(CypherStrings.NpcinfoHealth, target.GetCreateHealth(), target.GetMaxHealth(), target.GetHealth());
            handler.SendSysMessage(CypherStrings.NpcinfoMovementData, target.GetMovementTemplate().ToString());

            handler.SendSysMessage(CypherStrings.NpcinfoUnitFieldFlags, (uint)target.m_unitData.Flags);
            foreach (UnitFlags value in Enum.GetValues(typeof(UnitFlags)))
                if (target.HasUnitFlag(value))
                    handler.SendSysMessage($"{value} (0x{value:X})");

            handler.SendSysMessage(CypherStrings.NpcinfoUnitFieldFlags2, (uint)target.m_unitData.Flags2);
            foreach (UnitFlags2 value in Enum.GetValues(typeof(UnitFlags2)))
                if (target.HasUnitFlag2(value))
                    handler.SendSysMessage($"{value} (0x{value:X})");

            handler.SendSysMessage(CypherStrings.NpcinfoUnitFieldFlags3, (uint)target.m_unitData.Flags3);
            foreach (UnitFlags3 value in Enum.GetValues(typeof(UnitFlags3)))
                if (target.HasUnitFlag3(value))
                    handler.SendSysMessage($"{value} (0x{value:X})");

            handler.SendSysMessage(CypherStrings.NpcinfoDynamicFlags, target.GetDynamicFlags());
            handler.SendSysMessage(CypherStrings.CommandRawpawntimes, defRespawnDelayStr, curRespawnDelayStr);

            CreatureDifficulty creatureDifficulty = target.GetCreatureDifficulty();
            handler.SendSysMessage(CypherStrings.NpcinfoLoot, creatureDifficulty.LootID, creatureDifficulty.PickPocketLootID, creatureDifficulty.SkinLootID);
            handler.SendSysMessage(CypherStrings.NpcinfoDungeonId, target.GetInstanceId());

            CreatureData data = Global.ObjectMgr.GetCreatureData(target.GetSpawnId());
            if (data != null)
                handler.SendSysMessage(CypherStrings.NpcinfoPhases, data.PhaseId, data.PhaseGroup);

            PhasingHandler.PrintToChat(handler, target);

            handler.SendSysMessage(CypherStrings.NpcinfoArmor, target.GetArmor());
            handler.SendSysMessage(CypherStrings.NpcinfoPosition, target.GetPositionX(), target.GetPositionY(), target.GetPositionZ());
            handler.SendSysMessage(CypherStrings.ObjectinfoAiInfo, target.GetAIName(), target.GetScriptName());
            handler.SendSysMessage(CypherStrings.ObjectinfoStringIds, target.GetStringIds()[0], target.GetStringIds()[1], target.GetStringIds()[2]);
            handler.SendSysMessage(CypherStrings.NpcinfoReactstate, target.GetReactState());
            var ai = target.GetAI();
            if (ai != null)
                handler.SendSysMessage(CypherStrings.ObjectinfoAiType, nameof(ai));
            handler.SendSysMessage(CypherStrings.NpcinfoFlagsExtra, cInfo.FlagsExtra);
            foreach (var value in Enum.GetValues(typeof(CreatureFlagsExtra)))
                if (cInfo.FlagsExtra.HasAnyFlag((CreatureFlagsExtra)value))
                    handler.SendSysMessage($"{(CreatureFlagsExtra)value} (0x{value:X})");

            handler.SendSysMessage(CypherStrings.NpcinfoNpcFlags, target.m_unitData.NpcFlags[0]);
            foreach (NPCFlags1 value in Enum.GetValues(typeof(NPCFlags1)))
                if (npcflags1.HasAnyFlag(value))
                    handler.SendSysMessage($"{value} (0x{value:X})");

            handler.SendSysMessage(CypherStrings.NpcinfoMechanicImmune, mechanicImmuneMask);
            foreach (int value in Enum.GetValues(typeof(Mechanics)))
                if (mechanicImmuneMask.HasAnyFlag(1ul << (value - 1)))
                    handler.SendSysMessage($"{value} (0x{value:X})");

            return true;
        }

        [Command("move", RBACPermissions.CommandNpcMove)]
        static bool HandleNpcMoveCommand(CommandHandler handler, long? spawnId)
        {
            Creature creature = handler.GetSelectedCreature();
            Player player = handler.GetSession().GetPlayer();
            if (player == null)
                return false;

            if (!spawnId.HasValue && creature == null)
                return false;

            long lowguid = spawnId.HasValue ? spawnId.Value : creature.GetSpawnId();

            // Attempting creature load from DB data
            CreatureData data = Global.ObjectMgr.GetCreatureData(lowguid);
            if (data == null)
            {
                handler.SendSysMessage(CypherStrings.CommandCreatguidnotfound, lowguid);
                return false;
            }

            if (player.GetMapId() != data.MapId)
            {
                handler.SendSysMessage(CypherStrings.CommandCreatureatsamemap, lowguid);
                return false;
            }

            Global.ObjectMgr.RemoveCreatureFromGrid(data);
            data.SpawnPoint.Relocate(player);
            Global.ObjectMgr.AddCreatureToGrid(data);

            // update position in DB
            PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_POSITION);
            stmt.SetFloat(0, player.GetPositionX());
            stmt.SetFloat(1, player.GetPositionY());
            stmt.SetFloat(2, player.GetPositionZ());
            stmt.SetFloat(3, player.GetOrientation());
            stmt.SetInt64(4, lowguid);

            DB.World.Execute(stmt);

            // respawn selected creature at the new location
            if (creature != null)
                creature.DespawnOrUnsummon((Seconds)0, (Seconds)1);

            handler.SendSysMessage(CypherStrings.CommandCreaturemoved);
            return true;
        }

        [Command("near", RBACPermissions.CommandNpcNear)]
        static bool HandleNpcNearCommand(CommandHandler handler, float? dist)
        {
            float distance = dist.GetValueOrDefault(10.0f);
            uint count = 0;

            Player player = handler.GetPlayer();

            PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.SEL_CREATURE_NEAREST);
            stmt.SetFloat(0, player.GetPositionX());
            stmt.SetFloat(1, player.GetPositionY());
            stmt.SetFloat(2, player.GetPositionZ());
            stmt.SetInt32(3, player.GetMapId());
            stmt.SetFloat(4, player.GetPositionX());
            stmt.SetFloat(5, player.GetPositionY());
            stmt.SetFloat(6, player.GetPositionZ());
            stmt.SetFloat(7, distance * distance);
            SQLResult result = DB.World.Query(stmt);

            if (!result.IsEmpty())
            {
                do
                {
                    long guid = result.Read<long>(0);
                    int entry = result.Read<int>(1);
                    float x = result.Read<float>(2);
                    float y = result.Read<float>(3);
                    float z = result.Read<float>(4);
                    ushort mapId = result.Read<ushort>(5);

                    CreatureTemplate creatureTemplate = Global.ObjectMgr.GetCreatureTemplate(entry);
                    if (creatureTemplate == null)
                        continue;

                    handler.SendSysMessage(
                        CypherStrings.CreatureListChat, guid, guid, 
                        creatureTemplate.Name, x, y, z, mapId, "", "");

                    ++count;
                }
                while (result.NextRow());
            }

            handler.SendSysMessage(CypherStrings.CommandNearNpcMessage, distance, count);

            return true;
        }

        [Command("playemote", RBACPermissions.CommandNpcPlayemote)]
        static bool HandleNpcPlayEmoteCommand(CommandHandler handler, uint emote)
        {
            Creature target = handler.GetSelectedCreature();
            if (target == null)
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            target.SetEmoteState((Emote)emote);

            return true;
        }

        [Command("say", RBACPermissions.CommandNpcSay)]
        static bool HandleNpcSayCommand(CommandHandler handler, Tail text)
        {
            if (text.IsEmpty())
                return false;

            Creature creature = handler.GetSelectedCreature();
            if (creature == null)
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            creature.Say(text, Language.Universal);

            // make some emotes
            switch (((string)text).LastOrDefault())
            {
                case '?':
                    creature.HandleEmoteCommand(Emote.OneshotQuestion);
                    break;
                case '!':
                    creature.HandleEmoteCommand(Emote.OneshotExclamation);
                    break;
                default:
                    creature.HandleEmoteCommand(Emote.OneshotTalk);
                    break;
            }

            return true;
        }

        [Command("showloot", RBACPermissions.CommandNpcShowloot)]
        static bool HandleNpcShowLootCommand(CommandHandler handler, string all)
        {
            Creature creatureTarget = handler.GetSelectedCreature();
            if (creatureTarget == null || creatureTarget.IsPet())
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            Loot loot = creatureTarget._loot;
            if (creatureTarget.IsDead() || loot == null || loot.IsLooted())
            {
                handler.SendSysMessage(CypherStrings.CommandNotDeadOrNoLoot, creatureTarget.GetName());
                return false;
            }

            handler.SendSysMessage(
                CypherStrings.CommandNpcShowlootHeader, 
                creatureTarget.GetName(), creatureTarget.GetEntry());

            handler.SendSysMessage(
                CypherStrings.CommandNpcShowlootMoney, 
                loot.gold / MoneyConstants.Gold, 
                (loot.gold % MoneyConstants.Gold) / MoneyConstants.Silver, 
                loot.gold % MoneyConstants.Silver);

            if (all.Equals("all", StringComparison.OrdinalIgnoreCase)) // nonzero from strcmp <. not equal
            {
                handler.SendSysMessage(CypherStrings.CommandNpcShowlootLabel, "Standard items", loot.items.Count);
                foreach (LootItem item in loot.items)
                    if (!item.is_looted)
                        _ShowLootEntry(handler, item.itemid, item.count);
            }
            else
            {
                handler.SendSysMessage(CypherStrings.CommandNpcShowlootLabel, "Standard items", loot.items.Count);
                foreach (LootItem item in loot.items)
                    if (!item.is_looted && !item.freeforall && item.conditions.IsEmpty())
                        _ShowLootEntry(handler, item.itemid, item.count);

                if (!loot.GetPlayerFFAItems().Empty())
                {
                    handler.SendSysMessage(CypherStrings.CommandNpcShowlootLabel2, "FFA items per allowed player");
                    _IterateNotNormalLootMap(handler, loot.GetPlayerFFAItems(), loot.items);
                }
            }

            return true;
        }

        [Command("spawngroup", RBACPermissions.CommandNpcSpawngroup)]
        static bool HandleNpcSpawnGroup(CommandHandler handler, string[] opts)
        {
            if (opts.Empty())
                return false;

            bool ignoreRespawn = false;
            bool force = false;
            int groupId = 0;

            // Decode arguments
            foreach (var variant in opts)
            {
                switch (variant)
                {
                    case "force":
                        force = true;
                        break;
                    case "ignorerespawn":
                        ignoreRespawn = true;
                        break;
                    default:
                        int.TryParse(variant, out groupId);
                        break;
                }
            }

            Player player = handler.GetSession().GetPlayer();

            List<WorldObject> creatureList = new();
            if (!player.GetMap().SpawnGroupSpawn(groupId, ignoreRespawn, force, creatureList))
            {
                handler.SendSysMessage(CypherStrings.SpawngroupBadgroup, groupId);
                return false;
            }

            handler.SendSysMessage(CypherStrings.SpawngroupSpawncount, creatureList.Count);

            return true;
        }

        [Command("tame", RBACPermissions.CommandNpcTame)]
        static bool HandleNpcTameCommand(CommandHandler handler)
        {
            Creature creatureTarget = handler.GetSelectedCreature();
            if (creatureTarget == null || creatureTarget.IsPet())
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            Player player = handler.GetSession().GetPlayer();

            if (!player.GetPetGUID().IsEmpty())
            {
                handler.SendSysMessage(CypherStrings.YouAlreadyHavePet);
                return false;
            }

            CreatureTemplate cInfo = creatureTarget.GetCreatureTemplate();

            if (!cInfo.IsTameable(player.CanTameExoticPets(), creatureTarget.GetCreatureDifficulty()))
            {
                handler.SendSysMessage(CypherStrings.CreatureNonTameable, cInfo.Entry);
                return false;
            }

            // Everything looks OK, create new pet
            Pet pet = player.CreateTamedPetFrom(creatureTarget);
            if (pet == null)
            {
                handler.SendSysMessage(CypherStrings.CreatureNonTameable, cInfo.Entry);
                return false;
            }

            // place pet before player
            float x, y, z;

            player.GetClosePoint(
                out x, out y, out z, 
                creatureTarget.GetCombatReach(), SharedConst.ContactDistance);

            pet.Relocate(x, y, z, MathFunctions.PI - player.GetOrientation());

            // set pet to defensive mode by default (some classes can't control controlled pets in fact).
            pet.SetReactState(ReactStates.Defensive);

            // calculate proper level
            int level = Math.Max(player.GetLevel() - 5, creatureTarget.GetLevel());

            // prepare visual effect for levelup
            pet.SetLevel(level - 1);

            // add to world
            pet.GetMap().AddToMap(pet.ToCreature());

            // visual effect for levelup
            pet.SetLevel(level);

            // caster have pet now
            player.SetMinion(pet, true);

            pet.SavePetToDB(PetSaveMode.AsCurrent);
            player.PetSpellInitialize();

            return true;
        }

        [Command("textemote", RBACPermissions.CommandNpcTextemote)]
        static bool HandleNpcTextEmoteCommand(CommandHandler handler, Tail text)
        {
            Creature creature = handler.GetSelectedCreature();
            if (creature == null)
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            creature.TextEmote(text);

            return true;
        }

        [Command("whisper", RBACPermissions.CommandNpcWhisper)]
        static bool HandleNpcWhisperCommand(CommandHandler handler, string recv, Tail text)
        {
            if (text.IsEmpty())
            {
                handler.SendSysMessage(CypherStrings.CmdSyntax);
                return false;
            }

            Creature creature = handler.GetSelectedCreature();
            if (creature == null)
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            // check online security
            Player receiver = Global.ObjAccessor.FindPlayerByName(recv);
            if (handler.HasLowerSecurity(receiver, ObjectGuid.Empty))
                return false;

            creature.Whisper(text, Language.Universal, receiver);
            return true;
        }

        [Command("yell", RBACPermissions.CommandNpcYell)]
        static bool HandleNpcYellCommand(CommandHandler handler, Tail text)
        {
            if (text.IsEmpty())
                return false;

            Creature creature = handler.GetSelectedCreature();
            if (creature == null)
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                return false;
            }

            creature.Yell(text, Language.Universal);

            // make an emote
            creature.HandleEmoteCommand(Emote.OneshotShout);

            return true;
        }

        static void _ShowLootEntry(CommandHandler handler, int itemId, byte itemCount, bool alternateString = false)
        {
            string name = "Unknown item";

            ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(itemId);
            if (itemTemplate != null)
                name = itemTemplate.GetName(handler.GetSessionDbcLocale());

            handler.SendSysMessage(alternateString 
                ? CypherStrings.CommandNpcShowlootEntry2 
                : CypherStrings.CommandNpcShowlootEntry,
                itemCount, 
                ItemConst.ItemQualityColors[(int)(itemTemplate != null ? itemTemplate.GetQuality() : ItemQuality.Poor)], 
                itemId, name, itemId);
        }
        static void _IterateNotNormalLootMap(CommandHandler handler, MultiMap<ObjectGuid, NotNormalLootItem> map, List<LootItem> items)
        {
            foreach (var key in map.Keys)
            {
                if (map[key].Empty())
                    continue;

                var list = map[key];

                Player player = Global.ObjAccessor.FindConnectedPlayer(key);
                handler.SendSysMessage(
                    CypherStrings.CommandNpcShowlootSublabel, 
                    player != null ? player.GetName() : $"Offline player (GUID {key})", 
                    list.Count);

                foreach (var it in list)
                {
                    LootItem item = items[it.LootListId];
                    if (!it.is_looted && !item.is_looted)
                        _ShowLootEntry(handler, item.itemid, item.count, true);
                }
            }
        }

        [CommandGroup("add")]
        class AddCommands
        {
            [Command("", RBACPermissions.CommandNpcAdd)]
            static bool HandleNpcAddCommand(CommandHandler handler, int id)
            {
                if (Global.ObjectMgr.GetCreatureTemplate(id) == null)
                    return false;

                Player chr = handler.GetSession().GetPlayer();
                Map map = chr.GetMap();

                Transport trans = chr.GetTransport<Transport>();
                if (trans != null)
                {
                    long guid = Global.ObjectMgr.GenerateCreatureSpawnId();
                    CreatureData data = Global.ObjectMgr.NewOrExistCreatureData(guid);
                    data.SpawnId = guid;
                    data.spawnGroupData = Global.ObjectMgr.GetDefaultSpawnGroup();
                    data.Id = id;
                    data.SpawnPoint.Relocate(
                        chr.GetTransOffsetX(),
                        chr.GetTransOffsetY(), 
                        chr.GetTransOffsetZ(), 
                        chr.GetTransOffsetO());

                    data.spawnGroupData = new();

                    Creature creaturePassenger = trans.CreateNPCPassenger(guid, data);
                    if (creaturePassenger != null)
                    {
                        creaturePassenger.SaveToDB(trans.GetGoInfo().MoTransport.SpawnMap, 
                            new List<Difficulty>() { map.GetDifficultyID() });
                        Global.ObjectMgr.AddCreatureToGrid(data);
                    }
                    return true;
                }

                Creature creature = Creature.CreateCreature(id, map, chr.GetPosition());
                if (creature == null)
                    return false;

                PhasingHandler.InheritPhaseShift(creature, chr);
                creature.SaveToDB(map.GetId(), new List<Difficulty>() { map.GetDifficultyID() });

                long db_guid = creature.GetSpawnId();

                // To call _LoadGoods(); _LoadQuests(); CreateTrainerSpells()
                // current "creature" variable is deleted and created fresh new,
                // otherwise old values might trigger asserts or cause undefined behavior
                creature.CleanupsBeforeDelete();
                creature = Creature.CreateCreatureFromDB(db_guid, map, true, true);
                if (creature == null)
                    return false;

                Global.ObjectMgr.AddCreatureToGrid(Global.ObjectMgr.GetCreatureData(db_guid));
                return true;
            }

            [Command("item", RBACPermissions.CommandNpcAddItem)]
            static bool HandleNpcAddVendorItemCommand(CommandHandler handler, int itemId, int? mc, int? it, int? ec, [OptionalArg] string bonusListIds)
            {
                if (itemId == 0)
                {
                    handler.SendSysMessage(CypherStrings.CommandNeeditemsend);
                    return false;
                }

                Creature vendor = handler.GetSelectedCreature();
                if (vendor == null)
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                int maxcount = mc.GetValueOrDefault(0);
                Seconds incrtime = (Seconds)it.GetValueOrDefault(0);
                int extendedcost = ec.GetValueOrDefault(0);
                int vendor_entry = vendor.GetEntry();

                VendorItem vItem = new();
                vItem.item = itemId;
                vItem.maxcount = maxcount;
                vItem.incrtime = incrtime;
                vItem.ExtendedCost = extendedcost;
                vItem.Type = ItemVendorType.Item;

                if (!bonusListIds.IsEmpty())
                {
                    var bonusListIDsTok = new StringArray(bonusListIds, ';');
                    if (!bonusListIDsTok.IsEmpty())
                    {
                        foreach (string token in bonusListIDsTok)
                        {
                            if (int.TryParse(token, out int id))
                                vItem.BonusListIDs.Add(id);
                        }
                    }
                }

                if (!Global.ObjectMgr.IsVendorItemValid(vendor_entry, vItem, handler.GetSession().GetPlayer()))
                    return false;

                Global.ObjectMgr.AddVendorItem(vendor_entry, vItem);

                ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(itemId);

                handler.SendSysMessage(
                    CypherStrings.ItemAddedToList, itemId, itemTemplate.GetName(), 
                    maxcount, incrtime, extendedcost);
                return true;
            }

            [Command("move", RBACPermissions.CommandNpcAddMove)]
            static bool HandleNpcAddMoveCommand(CommandHandler handler, long lowGuid)
            {
                // attempt check creature existence by DB data
                CreatureData data = Global.ObjectMgr.GetCreatureData(lowGuid);
                if (data == null)
                {
                    handler.SendSysMessage(CypherStrings.CommandCreatguidnotfound, lowGuid);
                    return false;
                }

                // Update movement type
                PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_MOVEMENT_TYPE);
                stmt.SetUInt8(0, (byte)MovementGeneratorType.Waypoint);
                stmt.SetInt64(1, lowGuid);
                DB.World.Execute(stmt);

                handler.SendSysMessage(CypherStrings.WaypointAdded);

                return true;
            }

            [Command("formation", RBACPermissions.CommandNpcAddFormation)]
            static bool HandleNpcAddFormationCommand(CommandHandler handler, long leaderGUID)
            {
                Creature creature = handler.GetSelectedCreature();
                if (creature == null || creature.GetSpawnId() == 0)
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                long lowguid = creature.GetSpawnId();
                if (creature.GetFormation() != null)
                {
                    handler.SendSysMessage(
                        $"Selected creature is already member of group " +
                        $"{creature.GetFormation().GetLeaderSpawnId()}");
                    return false;
                }

                if (lowguid == 0)
                    return false;

                Player chr = handler.GetSession().GetPlayer();
                float followAngle = (creature.GetAbsoluteAngle(chr) - chr.GetOrientation()) * 180.0f / MathF.PI;
                float followDist = MathF.Sqrt(MathF.Pow(chr.GetPositionX() - creature.GetPositionX(), 2f) + MathF.Pow(chr.GetPositionY() - creature.GetPositionY(), 2f));
                int groupAI = 0;
                FormationMgr.AddFormationMember(lowguid, followAngle, followDist, leaderGUID, groupAI);
                creature.SearchFormation();

                PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.INS_CREATURE_FORMATION);
                stmt.SetInt64(0, leaderGUID);
                stmt.SetInt64(1, lowguid);
                stmt.SetFloat(2, followAngle);
                stmt.SetFloat(3, followDist);
                stmt.SetInt32(4, groupAI);

                DB.World.Execute(stmt);

                handler.SendSysMessage(
                    $"Creature {lowguid} added to formation with leader {leaderGUID}");

                return true;
            }

            [Command("temp", RBACPermissions.CommandNpcAddTemp)]
            static bool HandleNpcAddTempSpawnCommand(CommandHandler handler, [OptionalArg] string lootStr, int id)
            {
                bool loot = false;
                if (!lootStr.IsEmpty())
                {
                    if (lootStr.Equals("loot", StringComparison.OrdinalIgnoreCase))
                        loot = true;
                    else if (lootStr.Equals("noloot", StringComparison.OrdinalIgnoreCase))
                        loot = false;
                    else
                        return false;
                }

                if (Global.ObjectMgr.GetCreatureTemplate(id) == null)
                    return false;

                Player chr = handler.GetSession().GetPlayer();
                chr.SummonCreature(id, chr.GetPosition(), loot 
                    ? TempSummonType.CorpseTimedDespawn 
                    : TempSummonType.CorpseDespawn,
                    (Seconds)30);

                return true;
            }
        }

        [CommandGroup("delete")]
        class DeleteCommands
        {
            [Command("", RBACPermissions.CommandNpcDelete)]
            static bool HandleNpcDeleteCommand(CommandHandler handler, long? spawnIdArg)
            {
                long spawnId;
                if (spawnIdArg.HasValue)
                    spawnId = spawnIdArg.Value;
                else
                {
                    Creature creature = handler.GetSelectedCreature();
                    if (creature == null || creature.IsPet() || creature.IsTotem())
                    {
                        handler.SendSysMessage(CypherStrings.SelectCreature);
                        return false;
                    }

                    TempSummon summon = creature.ToTempSummon();
                    if (summon != null)
                    {
                        summon.UnSummon();
                        handler.SendSysMessage(CypherStrings.CommandDelcreatmessage);
                        return true;
                    }
                    spawnId = creature.GetSpawnId();
                }

                if (Creature.DeleteFromDB(spawnId))
                {
                    handler.SendSysMessage(CypherStrings.CommandDelcreatmessage);
                    return true;
                }

                handler.SendSysMessage(CypherStrings.CommandCreatguidnotfound, spawnId);
                return false;
            }

            [Command("item", RBACPermissions.CommandNpcDeleteItem)]
            static bool HandleNpcDeleteVendorItemCommand(CommandHandler handler, int itemId)
            {
                Creature vendor = handler.GetSelectedCreature();
                if (vendor == null || !vendor.IsVendor())
                {
                    handler.SendSysMessage(CypherStrings.CommandVendorselection);
                    return false;
                }

                if (itemId == 0)
                    return false;

                if (!Global.ObjectMgr.RemoveVendorItem(vendor.GetEntry(), itemId, ItemVendorType.Item))
                {
                    handler.SendSysMessage(CypherStrings.ItemNotInList, itemId);
                    return false;
                }

                ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(itemId);
                handler.SendSysMessage(CypherStrings.ItemDeletedFromList, itemId, itemTemplate.GetName());
                return true;
            }
        }

        [CommandGroup("follow")]
        class FollowCommands
        {
            [Command("", RBACPermissions.CommandNpcFollow)]
            static bool HandleNpcFollowCommand(CommandHandler handler)
            {
                Player player = handler.GetSession().GetPlayer();
                Creature creature = handler.GetSelectedCreature();

                if (creature == null)
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                // Follow player - Using pet's default dist and angle
                creature.GetMotionMaster().MoveFollow(player, SharedConst.PetFollowDist, creature.GetFollowAngle());

                handler.SendSysMessage(CypherStrings.CreatureFollowYouNow, creature.GetName());
                return true;
            }

            [Command("stop", RBACPermissions.CommandNpcFollowStop)]
            static bool HandleNpcUnFollowCommand(CommandHandler handler)
            {
                Player player = handler.GetPlayer();
                Creature creature = handler.GetSelectedCreature();

                if (creature == null)
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                MovementGenerator movement = creature.GetMotionMaster().GetMovementGenerator(a =>
                {
                    if (a.GetMovementGeneratorType() == MovementGeneratorType.Follow)
                    {
                        FollowMovementGenerator followMovement = a as FollowMovementGenerator;
                        return followMovement != null && followMovement.GetTarget() == player;
                    }
                    return false;
                });

                if (movement != null)
                {
                    handler.SendSysMessage(CypherStrings.CreatureNotFollowYou, creature.GetName());
                    return false;
                }

                creature.GetMotionMaster().Remove(movement);
                handler.SendSysMessage(CypherStrings.CreatureNotFollowYouNow, creature.GetName());
                return true;
            }
        }

        [CommandGroup("set")]
        class SetCommands
        {
            [Command("allowmove", RBACPermissions.CommandNpcSetAllowmove)]
            static bool HandleNpcSetAllowMovementCommand(CommandHandler handler)
            {
                /*
                if (Global.WorldMgr.getAllowMovement())
                {
                    Global.WorldMgr.SetAllowMovement(false);
                    handler.SendSysMessage(LANG_CREATURE_MOVE_DISABLED);
                }
                else
                {
                    Global.WorldMgr.SetAllowMovement(true);
                    handler.SendSysMessage(LANG_CREATURE_MOVE_ENABLED);
                }
                */
                return true;
            }

            [Command("data", RBACPermissions.CommandNpcSetData)]
            static bool HandleNpcSetDataCommand(CommandHandler handler, int data_1, int data_2)
            {
                Creature creature = handler.GetSelectedCreature();
                if (creature == null)
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                creature.GetAI().SetData(data_1, data_2);

                string AIorScript = 
                    creature.GetAIName() != "" 
                    ? "AI Type: " + creature.GetAIName() 
                    : 
                    (creature.GetScriptName() != "" 
                    ? "Script Name: " + creature.GetScriptName() 
                    : "No AI or Script Name Set");

                handler.SendSysMessage(
                    CypherStrings.NpcSetdata, creature.GetGUID(), creature.GetEntry(), 
                    creature.GetName(), data_1, data_2, AIorScript);

                return true;
            }

            [Command("entry", RBACPermissions.CommandNpcSetEntry)]
            static bool HandleNpcSetEntryCommand(CommandHandler handler, int newEntryNum)
            {
                if (newEntryNum == 0)
                    return false;

                Unit unit = handler.GetSelectedUnit();
                if (unit == null || !unit.IsTypeId(TypeId.Unit))
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }
                Creature creature = unit.ToCreature();
                if (creature.UpdateEntry(newEntryNum))
                    handler.SendSysMessage(CypherStrings.Done);
                else
                    handler.SendSysMessage(CypherStrings.Error);
                return true;

            }

            [Command("factionid", RBACPermissions.CommandNpcSetFactionid)]
            static bool HandleNpcSetFactionIdCommand(CommandHandler handler, int factionId)
            {
                if (!CliDB.FactionTemplateStorage.ContainsKey(factionId))
                {
                    handler.SendSysMessage(CypherStrings.WrongFaction, factionId);
                    return false;
                }

                Creature creature = handler.GetSelectedCreature();
                if (creature == null)
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                creature.SetFaction(factionId);

                // Faction is set in creature_template - not inside creature

                // Update in memory..
                CreatureTemplate cinfo = creature.GetCreatureTemplate();
                if (cinfo != null)
                    cinfo.Faction = factionId;

                // ..and DB
                PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_FACTION);

                stmt.SetInt32(0, factionId);
                stmt.SetInt32(1, factionId);
                stmt.SetInt32(2, creature.GetEntry());

                DB.World.Execute(stmt);

                return true;
            }

            [Command("flag", RBACPermissions.CommandNpcSetFlag)]
            static bool HandleNpcSetFlagCommand(CommandHandler handler, NPCFlags1 npcFlags, NPCFlags2 npcFlags2)
            {
                Creature creature = handler.GetSelectedCreature();
                if (creature == null)
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                creature.ReplaceAllNpcFlags(npcFlags);
                creature.ReplaceAllNpcFlags2(npcFlags2);

                PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_NPCFLAG);
                stmt.SetUInt64(0, (ulong)npcFlags | ((ulong)npcFlags2 << 32));
                stmt.SetInt32(1, creature.GetEntry());
                DB.World.Execute(stmt);

                handler.SendSysMessage(CypherStrings.ValueSavedRejoin);

                return true;
            }

            [Command("level", RBACPermissions.CommandNpcSetLevel)]
            static bool HandleNpcSetLevelCommand(CommandHandler handler, byte lvl)
            {
                if (lvl < 1 || lvl > WorldConfig.Values[WorldCfg.MaxPlayerLevel].Int32 + 3)
                {
                    handler.SendSysMessage(CypherStrings.BadValue);
                    return false;
                }

                Creature creature = handler.GetSelectedCreature();
                if (creature == null || creature.IsPet())
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                creature.SetMaxHealth((uint)(100 + 30 * lvl));
                creature.SetHealth((uint)(100 + 30 * lvl));
                creature.SetLevel(lvl);
                creature.SaveToDB();

                return true;
            }

            [Command("link", RBACPermissions.CommandNpcSetLink)]
            static bool HandleNpcSetLinkCommand(CommandHandler handler, long linkguid)
            {
                Creature creature = handler.GetSelectedCreature();
                if (creature == null)
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                if (creature.GetSpawnId() == 0)
                {
                    handler.SendSysMessage(
                        $"Selected creature {creature.GetGUID()} isn't in creature table");
                    return false;
                }

                if (!Global.ObjectMgr.SetCreatureLinkedRespawn(creature.GetSpawnId(), linkguid))
                {
                    handler.SendSysMessage(
                        $"Selected creature can't link with guid '{linkguid}'");
                    return false;
                }

                handler.SendSysMessage(
                    $"LinkGUID '{linkguid}' added to creature " +
                    $"with DBTableGUID: '{creature.GetSpawnId()}'");
                return true;
            }

            [Command("model", RBACPermissions.CommandNpcSetModel)]
            static bool HandleNpcSetModelCommand(CommandHandler handler, int displayId)
            {
                Creature creature = handler.GetSelectedCreature();
                if (creature == null || creature.IsPet())
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                if (!CliDB.CreatureDisplayInfoStorage.ContainsKey(displayId))
                {
                    handler.SendSysMessage(CypherStrings.CommandInvalidParam, displayId);
                    return false;
                }

                creature.SetDisplayId(displayId, true);

                creature.SaveToDB();

                return true;
            }

            [Command("movetype", RBACPermissions.CommandNpcSetMovetype)]
            static bool HandleNpcSetMoveTypeCommand(CommandHandler handler, long? lowGuid, string type, string nodel)
            {
                // 3 arguments:
                // GUID (optional - you can also select the creature)
                // stay|random|way (determines the kind of movement)
                // NODEL (optional - tells the system NOT to delete any waypoints)
                //        this is very handy if you want to do waypoints, that are
                //        later switched on/off according to special events (like escort
                //        quests, etc)
                bool doNotDelete = !nodel.IsEmpty();

                long lowguid = 0;
                Creature creature = null;

                if (!lowGuid.HasValue)                                           // case .setmovetype $move_type (with selected creature)
                {
                    creature = handler.GetSelectedCreature();
                    if (creature == null || creature.IsPet())
                        return false;

                    lowguid = creature.GetSpawnId();
                }
                else
                {
                    lowguid = lowGuid.Value;

                    if (lowguid != 0)
                        creature = handler.GetCreatureFromPlayerMapByDbGuid(lowguid);

                    // attempt check creature existence by DB data
                    if (creature == null)
                    {
                        CreatureData data = Global.ObjectMgr.GetCreatureData(lowguid);
                        if (data == null)
                        {
                            handler.SendSysMessage(CypherStrings.CommandCreatguidnotfound, lowguid);
                            return false;
                        }
                    }
                    else
                    {
                        lowguid = creature.GetSpawnId();
                    }
                }

                // now lowguid is low guid really existed creature
                // and creature point (maybe) to this creature or NULL

                MovementGeneratorType move_type;

                switch (type)
                {
                    case "stay":
                        move_type = MovementGeneratorType.Idle;
                        break;
                    case "random":
                        move_type = MovementGeneratorType.Random;
                        break;
                    case "way":
                        move_type = MovementGeneratorType.Waypoint;
                        break;
                    default:
                        return false;
                }

                if (creature != null)
                {
                    // update movement Type
                    if (!doNotDelete)
                        creature.LoadPath(0);

                    creature.SetDefaultMovementType(move_type);
                    creature.GetMotionMaster().Initialize();
                    if (creature.IsAlive())                            // dead creature will reset movement generator at respawn
                    {
                        creature.SetDeathState(DeathState.JustDied);
                        creature.Respawn();
                    }
                    creature.SaveToDB();
                }

                if (!doNotDelete)
                {
                    handler.SendSysMessage(CypherStrings.MoveTypeSet, type);
                }
                else
                {
                    handler.SendSysMessage(CypherStrings.MoveTypeSetNodel, type);
                }

                return true;
            }

            [Command("phase", RBACPermissions.CommandNpcSetPhase)]
            static bool HandleNpcSetPhaseCommand(CommandHandler handler, int phaseId)
            {
                if (phaseId == 0)
                {
                    handler.SendSysMessage(CypherStrings.PhaseNotfound);
                    return false;
                }

                Creature creature = handler.GetSelectedCreature();
                if (creature == null || creature.IsPet())
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                PhasingHandler.ResetPhaseShift(creature);
                PhasingHandler.AddPhase(creature, phaseId, true);
                creature.SetDBPhase(phaseId);

                creature.SaveToDB();
                return true;
            }

            [Command("phasegroup", RBACPermissions.CommandNpcSetPhase)]
            static bool HandleNpcSetPhaseGroup(CommandHandler handler, StringArguments args)
            {
                if (args.Empty())
                    return false;

                int phaseGroupId = args.NextInt32();

                Creature creature = handler.GetSelectedCreature();
                if (creature == null || creature.IsPet())
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    return false;
                }

                PhasingHandler.ResetPhaseShift(creature);
                PhasingHandler.AddPhaseGroup(creature, phaseGroupId, true);
                creature.SetDBPhase(-phaseGroupId);

                creature.SaveToDB();

                return true;
            }

            [Command("wanderdistance", RBACPermissions.CommandNpcSetSpawndist)]
            static bool HandleNpcSetWanderDistanceCommand(CommandHandler handler, float option)
            {
                if (option < 0.0f)
                {
                    handler.SendSysMessage(CypherStrings.BadValue);
                    return false;
                }

                MovementGeneratorType mtype = MovementGeneratorType.Idle;
                if (option > 0.0f)
                    mtype = MovementGeneratorType.Random;

                Creature creature = handler.GetSelectedCreature();
                long guidLow;

                if (creature != null)
                    guidLow = creature.GetSpawnId();
                else
                    return false;

                creature.SetWanderDistance(option);
                creature.SetDefaultMovementType(mtype);
                creature.GetMotionMaster().Initialize();
                if (creature.IsAlive())                                // dead creature will reset movement generator at respawn
                {
                    creature.SetDeathState(DeathState.JustDied);
                    creature.Respawn();
                }

                PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_WANDER_DISTANCE);
                stmt.SetFloat(0, option);
                stmt.SetUInt8(1, (byte)mtype);
                stmt.SetInt64(2, guidLow);

                DB.World.Execute(stmt);

                handler.SendSysMessage(CypherStrings.CommandWanderDistance, option);
                return true;
            }

            [Command("spawntime", RBACPermissions.CommandNpcSetSpawntime)]
            static bool HandleNpcSetSpawnTimeCommand(CommandHandler handler, int spawnTime)
            {
                Creature creature = handler.GetSelectedCreature();
                if (creature == null)
                    return false;

                PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_SPAWN_TIME_SECS);
                stmt.SetInt32(0, spawnTime);
                stmt.SetInt64(1, creature.GetSpawnId());
                DB.World.Execute(stmt);

                creature.SetRespawnDelay((Seconds)spawnTime);
                handler.SendSysMessage(CypherStrings.CommandSpawntime, spawnTime);

                return true;
            }
        }
    }
}
