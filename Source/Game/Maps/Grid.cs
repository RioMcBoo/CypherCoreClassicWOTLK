﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System;
using System.Collections.Generic;

namespace Game.Maps
{
    public class GridInfo
    {
        public GridInfo()
        {
            i_timer = new TimeTracker();
            vis_Update = new PeriodicTimer(Milliseconds.Zero, (Milliseconds)RandomHelper.IRand(0, 1000));
            i_unloadActiveLockCount = 0;
            i_unloadExplicitLock = false;
        }

        public GridInfo(TimeSpan expiry, bool unload = true)
        {
            i_timer = new TimeTracker(expiry);
            vis_Update = new PeriodicTimer(Milliseconds.Zero, (Milliseconds)RandomHelper.IRand(0, 1000));
            i_unloadActiveLockCount = 0;
            i_unloadExplicitLock = !unload;
        }

        public TimeTracker GetTimeTracker()
        {
            return i_timer;
        }

        public bool GetUnloadLock()
        {
            return i_unloadActiveLockCount != 0 || i_unloadExplicitLock;
        }

        public void SetUnloadExplicitLock(bool on)
        {
            i_unloadExplicitLock = on;
        }

        public void IncUnloadActiveLock()
        {
            ++i_unloadActiveLockCount;
        }

        public void DecUnloadActiveLock()
        {
            if (i_unloadActiveLockCount != 0) --i_unloadActiveLockCount;
        }

        private void SetTimer(TimeTracker pTimer)
        {
            i_timer = pTimer;
        }

        public void ResetTimeTracker(TimeSpan interval)
        {
            i_timer.Reset(interval);
        }

        public void UpdateTimeTracker(TimeSpan diff)
        {
            i_timer.Update(diff);
        }

        public PeriodicTimer GetRelocationTimer()
        {
            return vis_Update;
        }

        TimeTracker i_timer;
        PeriodicTimer vis_Update;

        ushort i_unloadActiveLockCount; // lock from active object spawn points (prevent clone loading)
        bool i_unloadExplicitLock; // explicit manual lock or config setting
    }

    public class Grid
    {
        public Grid(int id, int x, int y, TimeSpan expiry, bool unload = true)
        {
            gridId = id;
            gridX = x;
            gridY = y;
            gridInfo = new GridInfo(expiry, unload);
            gridState = GridState.Invalid;
            gridObjectDataLoaded = false;

            for (uint xx = 0; xx < MapConst.MaxCells; ++xx)
            {
                i_cells[xx] = new GridCell[MapConst.MaxCells];
                for (int yy = 0; yy < MapConst.MaxCells; ++yy)
                    i_cells[xx][yy] = new GridCell();
            }
        }

        public Grid(Cell cell, TimeSpan expiry, bool unload = true) : this(cell.GetId(), cell.GetGridX(), cell.GetGridY(), expiry, unload) { }

        public GridCell GetGridCell(int x, int y)
        {
            return i_cells[x][y];
        }

        public int GetGridId()
        {
            return gridId;
        }

        private void SetGridId(int id)
        {
            gridId = id;
        }

        public GridState GetGridState()
        {
            return gridState;
        }

        public void SetGridState(GridState s)
        {
            gridState = s;
        }

        public int GetX()
        {
            return gridX;
        }

        public int GetY()
        {
            return gridY;
        }

        public bool IsGridObjectDataLoaded()
        {
            return gridObjectDataLoaded;
        }

        public void SetGridObjectDataLoaded(bool pLoaded)
        {
            gridObjectDataLoaded = pLoaded;
        }

        public GridInfo GetGridInfoRef()
        {
            return gridInfo;
        }

        private TimeTracker GetTimeTracker()
        {
            return gridInfo.GetTimeTracker();
        }

        public bool GetUnloadLock()
        {
            return gridInfo.GetUnloadLock();
        }

        public void SetUnloadExplicitLock(bool on)
        {
            gridInfo.SetUnloadExplicitLock(on);
        }

        public void IncUnloadActiveLock()
        {
            gridInfo.IncUnloadActiveLock();
        }

        public void DecUnloadActiveLock()
        {
            gridInfo.DecUnloadActiveLock();
        }

        public void ResetTimeTracker(TimeSpan interval)
        {
            gridInfo.ResetTimeTracker(interval);
        }

        public void UpdateTimeTracker(TimeSpan diff)
        {
            gridInfo.UpdateTimeTracker(diff);
        }

        public void Update(Map map, TimeSpan diff)
        {
            switch (GetGridState())
            {
                case GridState.Active:
                    // Only check grid activity every (grid_expiry/10) ms, because it's really useless to do it every cycle
                    GetGridInfoRef().UpdateTimeTracker(diff);
                    if (GetGridInfoRef().GetTimeTracker().Passed())
                    {
                        if (GetWorldObjectCountInNGrid<Player>() == 0 && !map.ActiveObjectsNearGrid(this))
                        {
                            ObjectGridStoper worker = new();
                            var visitor = new Visitor(worker, GridMapTypeMask.AllGrid);
                            VisitAllGrids(visitor);
                            SetGridState(GridState.Idle);
                            Log.outDebug(LogFilter.Maps, $"Grid[{GetX()}, {GetY()}] on map {map.GetId()} moved to IDLE state");
                        }
                        else
                            map.ResetGridExpiry(this, 0.1f);
                    }
                    break;
                case GridState.Idle:
                    map.ResetGridExpiry(this);
                    SetGridState(GridState.Removal);
                    Log.outDebug(LogFilter.Maps, $"Grid[{GetX()}, {GetY()}] on map {map.GetId()} moved to REMOVAL state");
                    break;
                case GridState.Removal:
                    if (!GetGridInfoRef().GetUnloadLock())
                    {
                        GetGridInfoRef().UpdateTimeTracker(diff);
                        if (GetGridInfoRef().GetTimeTracker().Passed())
                        {
                            if (!map.UnloadGrid(this, false))
                            {
                                Log.outDebug(LogFilter.Maps,
                                    $"Grid[{GetX()}, {GetY()}] for map {map.GetId()} " +
                                    $"differed unloading due to players or active objects nearby");
                                map.ResetGridExpiry(this);
                            }
                        }
                    }
                    break;
            }
        }

        public void VisitAllGrids(Visitor visitor)
        {
            for (int x = 0; x < MapConst.MaxCells; ++x)
            {
                for (int y = 0; y < MapConst.MaxCells; ++y)
                    GetGridCell(x, y).Visit(visitor);
            }
        }

        public void VisitGrid(int x, int y, Visitor visitor)
        {
            GetGridCell(x, y).Visit(visitor);
        }

        public int GetWorldObjectCountInNGrid<T>() where T : WorldObject
        {
            int count = 0;
            for (int x = 0; x < MapConst.MaxCells; ++x)
            {
                for (int y = 0; y < MapConst.MaxCells; ++y)
                    count += i_cells[x][y].GetWorldObjectCountInGrid<T>();
            }
            return count;
        }

        int gridId;
        int gridX;
        int gridY;
        GridInfo gridInfo;
        GridState gridState;
        bool gridObjectDataLoaded;
        GridCell[][] i_cells = new GridCell[MapConst.MaxCells][];
    }

    public class GridCell
    {
        public GridCell()
        {
            _objects = new MultiTypeContainer();
            _container = new MultiTypeContainer();
        }

        public void Visit(Visitor visitor)
        {
            switch (visitor._mask)
            {
                case GridMapTypeMask.AllGrid:
                    visitor.Visit(_container.gameObjects);
                    visitor.Visit(_container.creatures);
                    visitor.Visit(_container.dynamicObjects);
                    visitor.Visit(_container.corpses);
                    visitor.Visit(_container.areaTriggers);
                    visitor.Visit(_container.sceneObjects);
                    visitor.Visit(_container.conversations);
                    visitor.Visit(_container.worldObjects);
                    break;
                case GridMapTypeMask.AllWorld:
                    visitor.Visit(_objects.players);
                    visitor.Visit(_objects.creatures);
                    visitor.Visit(_objects.corpses);
                    visitor.Visit(_objects.dynamicObjects);
                    visitor.Visit(_objects.worldObjects);
                    break;
                default:
                    Log.outError(LogFilter.Server, $"{visitor} called Visit with Unknown Mask {visitor._mask}.");
                    break;
            }
        }

        public int GetWorldObjectCountInGrid<T>() where T : WorldObject
        {
            return _objects.GetCount<T>();
        }

        public void AddWorldObject(WorldObject obj)
        {
            _objects.Insert(obj);
        }

        public void AddGridObject(WorldObject obj)
        {
            _container.Insert(obj);
        }

        public void RemoveWorldObject(WorldObject obj)
        {
            _objects.Remove(obj);
        }

        public void RemoveGridObject(WorldObject obj)
        {
            _container.Remove(obj);
        }

        public bool HasWorldObject(WorldObject obj)
        {
            return _objects.Contains(obj);
        }

        public bool HasGridObject(WorldObject obj)
        {
            return _container.Contains(obj);
        }

        /// <summary>
        /// Holds all World objects - Player, Pets, Corpse(resurrectable), DynamicObject(farsight)
        /// </summary>
        MultiTypeContainer _objects;

        /// <summary>
        /// Holds all Grid objects - GameObjects, Creatures(except pets), DynamicObject, Corpse(Bones), AreaTrigger, Conversation, SceneObject
        /// </summary>
        MultiTypeContainer _container;
    }

    public class MultiTypeContainer
    {
        public void Insert(WorldObject obj)
        {
            worldObjects.Add(obj);
            switch (obj.GetTypeId())
            {
                case TypeId.Unit:
                    creatures.Add((Creature)obj);
                    break;
                case TypeId.Player:
                    players.Add((Player)obj);
                    break;
                case TypeId.GameObject:
                    gameObjects.Add((GameObject)obj);
                    break;
                case TypeId.DynamicObject:
                    dynamicObjects.Add((DynamicObject)obj);
                    break;
                case TypeId.Corpse:
                    corpses.Add((Corpse)obj);
                    break;
                case TypeId.AreaTrigger:
                    areaTriggers.Add((AreaTrigger)obj);
                    break;
                case TypeId.SceneObject:
                    sceneObjects.Add((SceneObject)obj);
                    break;
                case TypeId.Conversation:
                    conversations.Add((Conversation)obj);
                    break;
            }
        }

        public void Remove(WorldObject obj)
        {
            worldObjects.Remove(obj);
            switch (obj.GetTypeId())
            {
                case TypeId.Unit:
                    creatures.Remove((Creature)obj);
                    break;
                case TypeId.Player:
                    players.Remove((Player)obj);
                    break;
                case TypeId.GameObject:
                    gameObjects.Remove((GameObject)obj);
                    break;
                case TypeId.DynamicObject:
                    dynamicObjects.Remove((DynamicObject)obj);
                    break;
                case TypeId.Corpse:
                    corpses.Remove((Corpse)obj);
                    break;
                case TypeId.AreaTrigger:
                    areaTriggers.Remove((AreaTrigger)obj);
                    break;
                case TypeId.SceneObject:
                    sceneObjects.Remove((SceneObject)obj);
                    break;
                case TypeId.Conversation:
                    conversations.Remove((Conversation)obj);
                    break;
            }
        }

        public bool Contains(WorldObject obj)
        {
            return worldObjects.Contains(obj);
        }

        public int GetCount<T>()
        {
            switch (typeof(T).Name)
            {
                case "Creature":
                    return creatures.Count;
                case "Player":
                    return players.Count;
                case "GameObject":
                    return gameObjects.Count;
                case "DynamicObject":
                    return dynamicObjects.Count;
                case "Corpse":
                    return corpses.Count;
                case "AreaTrigger":
                    return areaTriggers.Count;
                case "Conversation":
                    return conversations.Count;
            }

            return 0;
        }

        public List<Player> players = new();
        public List<Creature> creatures = new();
        public List<Corpse> corpses = new();
        public List<DynamicObject> dynamicObjects = new();
        public List<AreaTrigger> areaTriggers = new();
        public List<SceneObject> sceneObjects = new();
        public List<Conversation> conversations = new();
        public List<GameObject> gameObjects = new();
        public List<WorldObject> worldObjects = new();
    }

    public enum GridState
    {
        Invalid = 0,
        Active = 1,
        Idle = 2,
        Removal = 3,
        Max = 4
    }
}