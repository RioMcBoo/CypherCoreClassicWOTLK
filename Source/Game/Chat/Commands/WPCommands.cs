﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Chat.Commands
{
    [CommandGroup("wp")]
    class WPCommands
    {
        [Command("add", RBACPermissions.CommandWpAdd)]
        static bool HandleWpAddCommand(CommandHandler handler, int? optionalPathId)
        {
            Creature target = handler.GetSelectedCreature();

            PreparedStatement stmt;
            int pathId;

            if (!optionalPathId.HasValue)
            {
                if (target != null)
                    pathId = target.GetWaypointPathId();
                else
                {
                    stmt = WorldDatabase.GetPreparedStatement(WorldStatements.SEL_WAYPOINT_PATH_NODE_MAX_PATHID);
                    SQLResult result1 = DB.World.Query(stmt);

                    int maxpathId = result1.Read<int>(0);
                    pathId = maxpathId + 1;
                    handler.SendSysMessage("|cff00ff00New path started.|r");
                }
            }
            else
                pathId = optionalPathId.Value;

            // pathId . ID of the Path
            // point   . number of the waypoint (if not 0)

            if (pathId == 0)
            {
                handler.SendSysMessage("|cffff33ffCurrent creature haven't loaded path.|r");
                return true;
            }

            stmt = WorldDatabase.GetPreparedStatement(WorldStatements.SEL_WAYPOINT_PATH_NODE_MAX_NODEID);
            stmt.SetInt32(0, pathId);
            SQLResult result = DB.World.Query(stmt);

            int nodeId = 0;
            if (result.IsEmpty())
                nodeId = result.Read<int>(0);

            Player player = handler.GetPlayer();

            stmt = WorldDatabase.GetPreparedStatement(WorldStatements.INS_WAYPOINT_PATH_NODE);
            stmt.SetInt32(0, pathId);
            stmt.SetInt32(1, nodeId);
            stmt.SetFloat(2, player.GetPositionX());
            stmt.SetFloat(3, player.GetPositionY());
            stmt.SetFloat(4, player.GetPositionZ());
            stmt.SetFloat(5, player.GetOrientation());
            DB.World.Execute(stmt);

            if (target != null)
            {
                int displayId = target.GetDisplayId();

                WaypointPath path = Global.WaypointMgr.GetPath(pathId);
                if (path == null)
                    return true;

                Global.WaypointMgr.DevisualizePath(player, path);
                Global.WaypointMgr.ReloadPath(pathId);
                Global.WaypointMgr.VisualizePath(player, path, displayId);
            }

            handler.SendSysMessage("|cff00ff00pathId: |r|cff00ffff{pathId}|r|cff00ff00: Waypoint |r|cff00ffff{nodeId}|r|cff00ff00 created. ");
            return true;
        }

        [Command("load", RBACPermissions.CommandWpLoad)]
        static bool HandleWpLoadCommand(CommandHandler handler, int? optionalPathId)
        {
            Creature target = handler.GetSelectedCreature();

            // Did player provide a pathId?
            if (!optionalPathId.HasValue)
                return false;

            int pathId = optionalPathId.Value;

            if (target == null)
            {
                handler.SendSysMessage(CypherStrings.SelectCreature);
                //handler->SetSentErrorMessage(true);
                return false;
            }

            if (target.GetEntry() == 1)
            {
                handler.SendSysMessage("|cffff33ffYou want to load path to a waypoint? Aren't you?|r");
                //handler->SetSentErrorMessage(true);
                return false;
            }

            if (pathId == 0)
            {
                handler.SendSysMessage("|cffff33ffNo valid path number provided.|r");
                return true;
            }

            long guidLow = target.GetSpawnId();

            PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.SEL_CREATURE_ADDON_BY_GUID);
            stmt.SetInt64(0, guidLow);
            SQLResult result = DB.World.Query(stmt);

            if (!result.IsEmpty())
            {
                stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_ADDON_PATH);
                stmt.SetInt32(0, pathId);
                stmt.SetInt64(1, guidLow);
            }
            else
            {
                stmt = WorldDatabase.GetPreparedStatement(WorldStatements.INS_CREATURE_ADDON);
                stmt.SetInt64(0, guidLow);
                stmt.SetInt32(1, pathId);
            }

            DB.World.Execute(stmt);

            stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_MOVEMENT_TYPE);
            stmt.SetUInt8(0, (byte)MovementGeneratorType.Waypoint);
            stmt.SetInt64(1, guidLow);

            DB.World.Execute(stmt);

            target.LoadPath(pathId);
            target.SetDefaultMovementType(MovementGeneratorType.Waypoint);
            target.GetMotionMaster().Initialize();
            target.Say("Path loaded.", Language.Universal);

            return true;
        }

        [Command("modify", RBACPermissions.CommandWpModify)]
        static bool HandleWpModifyCommand(CommandHandler handler, string subCommand, [OptionalArg] string arg)
        {
            // first arg: add del text emote spell waittime move
            if (subCommand.IsEmpty())
                return false;

            // Did user provide a GUID
            // or did the user select a creature?
            // . variable lowguid is filled with the GUID of the NPC
            Creature target = handler.GetSelectedCreature();

            // User did select a visual waypoint?
            if (target == null || target.GetEntry() != 1)
            {
                handler.SendSysMessage("|cffff33ffERROR: You must select a waypoint.|r");
                return false;
            }

            WaypointPath path = Global.WaypointMgr.GetPathByVisualGUID(target.GetGUID());
            if (path == null)
            {
                handler.SendSysMessage("|cff00ff00Path does not exist or target has no path|r");
                //handler->SetSentErrorMessage(true);
                return true;
            }

            WaypointNode node = Global.WaypointMgr.GetNodeByVisualGUID(target.GetGUID());
            if (node == null)
            {
                handler.SendSysMessage("|cff00ff00Path does not exist or target has no path|r");
                //handler->SetSentErrorMessage(true);
                return true;
            }

            if (subCommand == "del")
            {
                handler.SendSysMessage($"|cff00ff00DEBUG: .wp modify del, pathId: |r|cff00ffff{path.Id}|r, NodeId: |r|cff00ffff{node.Id}|r");

                int displayId = target.GetDisplayId();

                Global.WaypointMgr.DevisualizePath(handler.GetPlayer(), path);
                Global.WaypointMgr.DeleteNode(path, node);
                Global.WaypointMgr.ReloadPath(path.Id);
                Global.WaypointMgr.VisualizePath(handler.GetPlayer(), path, displayId);

                handler.SendSysMessage(CypherStrings.WaypointRemoved);
                return true;
            }
            else if (subCommand == "move")
            {
                handler.SendSysMessage("|cff00ff00DEBUG: .wp move, pathId: |r|cff00ffff%u|r, NodeId: |r|cff00ffff%u|r", path.Id, node.Id);

                int displayId = target.GetDisplayId();

                Global.WaypointMgr.DevisualizePath(handler.GetPlayer(), path);
                Global.WaypointMgr.MoveNode(path, node, handler.GetPlayer().GetPosition());
                Global.WaypointMgr.ReloadPath(path.Id);
                Global.WaypointMgr.VisualizePath(handler.GetPlayer(), path, displayId);

                handler.SendSysMessage(CypherStrings.WaypointChanged);
                return true;
            }

            return false;
        }

        [Command("reload", RBACPermissions.CommandWpReload)]
        static bool HandleWpReloadCommand(CommandHandler handler, int pathId)
        {
            if (pathId == 0)
                return false;

            handler.SendSysMessage("|cff00ff00Loading Path: |r|cff00ffff{0}|r", pathId);
            Global.WaypointMgr.ReloadPath(pathId);
            return true;
        }

        [Command("show", RBACPermissions.CommandWpShow)]
        static bool HandleWpShowCommand(CommandHandler handler, string subCommand, int? optionalPathId)
        {
            // first arg: on, off, first, last
            if (subCommand.IsEmpty())
                return false;

            int pathId;
            Creature target = handler.GetSelectedCreature();

            // Did player provide a pathId?
            if (!optionalPathId.HasValue)
            {
                // No pathId provided
                // . Player must have selected a creature

                if (target == null)
                {
                    handler.SendSysMessage(CypherStrings.SelectCreature);
                    //handler->SetSentErrorMessage(true);
                    return false;
                }

                pathId = target.GetWaypointPathId();
            }
            else
            {
                // pathId provided
                // Warn if player also selected a creature
                // . Creature selection is ignored <-
                if (target != null)
                    handler.SendSysMessage(CypherStrings.WaypointCreatselected);

                pathId = optionalPathId.Value;
            }

            // Show info for the selected waypoint
            if (subCommand == "info")
            {
                if (target == null || target.GetEntry() != 1)
                {
                    handler.SendSysMessage(CypherStrings.WaypointVpSelect);
                    return false;
                }

                WaypointPath path = Global.WaypointMgr.GetPathByVisualGUID(target.GetGUID());
                if (path == null)
                {
                    handler.SendSysMessage("|cff00ff00Path does not exist or target has no path|r");
                    handler.SetSentErrorMessage(true);
                    return false;
                }

                WaypointNode node = Global.WaypointMgr.GetNodeByVisualGUID(target.GetGUID());
                if (node == null)
                {
                    handler.SendSysMessage("|cff00ff00Path does not exist or target has no path|r");
                    handler.SetSentErrorMessage(true);
                    return false;

                }

                handler.SendSysMessage("|cff00ffffDEBUG: .wp show info:|r");
                handler.SendSysMessage($"|cff00ff00Show info: Path Id: |r|cff00ffff{path.Id}|r");
                handler.SendSysMessage($"|cff00ff00Show info: Path MoveType: |r|cff00ffff{(uint)path.MoveType}|r");
                handler.SendSysMessage($"|cff00ff00Show info: Path Flags: |r|cff00ffff{(uint)path.Flags}|r");
                handler.SendSysMessage($"|cff00ff00Show info: Node Id: |r|cff00ffff{node.Id}|r");
                handler.SendSysMessage($"|cff00ff00Show info: Node Delay: |r|cff00ffff{node.Id}|r");

                return true;
            }
            else if (subCommand == "on")
            {
                WaypointPath path = Global.WaypointMgr.GetPath(pathId);
                if (path == null)
                {
                    handler.SendSysMessage($"|cff00ff00Path does not exist: id {pathId}|r");
                    return true;
                }

                if (path.Nodes.Empty())
                {
                    handler.SendSysMessage($"|cff00ff00Path does not have any nodes: id {pathId}|r");
                    return true;
                }

                int? displayId = null;
                if (target != null)
                    displayId = target.GetDisplayId();

                Global.WaypointMgr.VisualizePath(handler.GetPlayer(), path, displayId);

                ObjectGuid guid = Global.WaypointMgr.GetVisualGUIDByNode(path.Id, path.Nodes.First().Id);
                if (!guid.IsEmpty())
                {
                    handler.SendSysMessage($"|cff00ff00Path with id {pathId} is already showing.|r");
                    return true;
                }

                handler.SendSysMessage($"|cff00ff00Showing path with id {pathId}.|r");
                return true;
            }
            else            if (subCommand == "off")
            {
                WaypointPath path = Global.WaypointMgr.GetPath(pathId);
                if (path == null)
                {
                    handler.SendSysMessage($"|cff00ff00Path does not exist: id {pathId}|r");
                    return true;
                }

                Global.WaypointMgr.DevisualizePath(handler.GetPlayer(), path);

                handler.SendSysMessage(CypherStrings.WaypointVpAllremoved);
                return true;
            }

            handler.SendSysMessage("|cffff33ffDEBUG: .wp show - no valid command found|r");
            return true;
        }

        [Command("unload", RBACPermissions.CommandWpUnload)]
        static bool HandleWpUnLoadCommand(CommandHandler handler)
        {
            Creature target = handler.GetSelectedCreature();
            if (target == null)
            {
                handler.SendSysMessage("|cff33ffffYou must select a target.|r");
                return true;
            }

            long guidLow = target.GetSpawnId();
            if (guidLow == 0)
            {
                handler.SendSysMessage("|cffff33ffTarget is not saved to DB.|r");
                return true;
            }

            CreatureAddon addon = Global.ObjectMgr.GetCreatureAddon(guidLow);
            if (addon == null || addon.PathId == 0)
            {
                handler.SendSysMessage("|cffff33ffTarget does not have a loaded path.|r");
                return true;
            }

            PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.DEL_CREATURE_ADDON);
            stmt.SetInt64(0, guidLow);
            DB.World.Execute(stmt);

            target.UpdateCurrentWaypointInfo(0, 0);

            stmt = WorldDatabase.GetPreparedStatement(WorldStatements.UPD_CREATURE_MOVEMENT_TYPE);
            stmt.SetUInt8(0, (byte)MovementGeneratorType.Idle);
            stmt.SetInt64(1, guidLow);
            DB.World.Execute(stmt);

            target.LoadPath(0);
            target.SetDefaultMovementType(MovementGeneratorType.Idle);
            target.GetMotionMaster().MoveTargetedHome();
            target.GetMotionMaster().Initialize();
            target.Say("Path unloaded.", Language.Universal);
            return true;
        }
    }
}
