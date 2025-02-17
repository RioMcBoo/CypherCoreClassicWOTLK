﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game
{
    public class MMapManager : Singleton<MMapManager>
    {
        MMapManager() { }

        const string MAP_FILE_NAME_FORMAT = "{0}/mmaps/{1:D4}.mmap";
        const string TILE_FILE_NAME_FORMAT = "{0}/mmaps/{1:D4}{2:D2}{3:D2}.mmtile";

        public void Initialize(MultiMap<int, int> mapData)
        {
            foreach (var pair in mapData)
                parentMapData[pair.Value] = pair.Key;
        }

        MMapData GetMMapData(int mapId)
        {
            return loadedMMaps.LookupByKey(mapId);
        }

        bool LoadMapData(string basePath, int mapId)
        {
            // we already have this map loaded?
            if (loadedMMaps.ContainsKey(mapId) && loadedMMaps[mapId] != null)
                return true;

            // load and init dtNavMesh - read parameters from file
            string filename = string.Format(MAP_FILE_NAME_FORMAT, basePath, mapId);
            if (!File.Exists(filename))
            {
                Log.outError(LogFilter.Maps, $"Could not open mmap file {filename}");
                return false;
            }

            using BinaryReader reader = new(new FileStream(filename, FileMode.Open, FileAccess.Read), Encoding.UTF8);
            Detour.dtNavMeshParams Params = new();
            Params.orig[0] = reader.ReadSingle();
            Params.orig[1] = reader.ReadSingle();
            Params.orig[2] = reader.ReadSingle();

            Params.tileWidth = reader.ReadSingle();
            Params.tileHeight = reader.ReadSingle();
            Params.maxTiles = reader.ReadInt32();
            Params.maxPolys = reader.ReadInt32();

            Detour.dtNavMesh mesh = new();
            if (Detour.dtStatusFailed(mesh.init(Params)))
            {
                Log.outError(LogFilter.Maps, 
                    $"MMAP:loadMapData: Failed to initialize dtNavMesh " +
                    $"for mmap {mapId:D4} from file {filename}");
                return false;
            }

            Log.outInfo(LogFilter.Maps, 
                $"MMAP:loadMapData: Loaded {mapId:D4}.mmap");

            // store inside our map list
            loadedMMaps[mapId] = new MMapData(mesh);
            return true;
        }

        uint PackTileID(int x, int y)
        {
            return (uint)(x << 16 | y);
        }

        public bool LoadMap(string basePath, int mapId, int x, int y)
        {
            // make sure the mmap is loaded and ready to load tiles
            if (!LoadMapData(basePath, mapId))
                return false;

            // get this mmap data
            MMapData mmap = loadedMMaps[mapId];
            Cypher.Assert(mmap.navMesh != null);

            // check if we already have this tile loaded
            uint packedGridPos = PackTileID(x, y);
            if (mmap.loadedTileRefs.ContainsKey(packedGridPos))
                return false;

            // load this tile . mmaps/MMMXXYY.mmtile
            string fileName = string.Format(TILE_FILE_NAME_FORMAT, basePath, mapId, x, y);
            if (!File.Exists(fileName))
            {
                if (parentMapData.ContainsKey(mapId))
                    fileName = string.Format(TILE_FILE_NAME_FORMAT, basePath, parentMapData[mapId], x, y);
            }

            if (!File.Exists(fileName))
            { 
                Log.outDebug(LogFilter.Maps, 
                    $"MMAP:loadMap: Could not open mmtile file '{fileName}'");
                return false;
            }

            using BinaryReader reader = new(new FileStream(fileName, FileMode.Open, FileAccess.Read));
            MmapTileHeader fileHeader = reader.Read<MmapTileHeader>();
            if (fileHeader.mmapMagic != MapConst.mmapMagic)
            {
                Log.outError(LogFilter.Maps, 
                    $"MMAP:loadMap: Bad header in mmap {mapId:D4}{x:D2}{y:D2}.mmtile");
                return false;
            }
            if (fileHeader.mmapVersion != MapConst.mmapVersion)
            {
                Log.outError(LogFilter.Maps, 
                    $"MMAP:loadMap: {mapId:D4}{x:D2}{y:D2}.mmtile " +
                    $"was built with generator v{fileHeader.mmapVersion}, " +
                    $"expected v{MapConst.mmapVersion}");
                return false;
            }

            var bytes = reader.ReadBytes((int)fileHeader.size);
            Detour.dtRawTileData data = new();
            data.FromBytes(bytes, 0);

            ulong tileRef = 0;
            // memory allocated for data is now managed by detour, and will be deallocated when the tile is removed
            if (Detour.dtStatusSucceed(mmap.navMesh.addTile(data, 1, 0, ref tileRef)))
            {
                mmap.loadedTileRefs.Add(packedGridPos, tileRef);
                ++loadedTiles;
                Log.outInfo(LogFilter.Maps, 
                    $"MMAP:loadMap: Loaded mmtile {mapId:D4}[{x:D2}, {y:D2}]");
                return true;
            }

            Log.outError(LogFilter.Maps, 
                $"MMAP:loadMap: Could not load {mapId:D4}{x:D2}{y:D2}.mmtile into navmesh");

            return false;
        }

        public bool LoadMapInstance(string basePath, int meshMapId, int instanceMapId, int instanceId)
        {
            if (!LoadMapData(basePath, meshMapId))
                return false;

            MMapData mmap = loadedMMaps[meshMapId];
            if (mmap.navMeshQueries.ContainsKey((instanceMapId, instanceId)))
                return true;

            // allocate mesh query
            Detour.dtNavMeshQuery query = new();
            if (Detour.dtStatusFailed(query.init(mmap.navMesh, 1024)))
            {
                Log.outError(LogFilter.Maps, 
                    $"MMAP.GetNavMeshQuery: Failed to initialize dtNavMeshQuery " +
                    $"for mapId {instanceMapId:D4} instanceId {instanceId}");
                return false;
            }

            Log.outDebug(LogFilter.Maps, 
                $"MMAP.GetNavMeshQuery: created dtNavMeshQuery " +
                $"for mapId {instanceMapId:D4} instanceId {instanceId}");

            mmap.navMeshQueries.Add((instanceMapId, instanceId), query);
            return true;
        }

        public bool UnloadMap(int mapId, int x, int y)
        {
            // check if we have this map loaded
            MMapData mmap = GetMMapData(mapId);
            if (mmap == null)
            {
                // file may not exist, therefore not loaded
                Log.outDebug(LogFilter.Maps, 
                    $"MMAP:unloadMap: Asked to unload not loaded navmesh map. " +
                    $"{mapId:D4}{x:D2}{y:D2}.mmtile");

                return false;
            }

            // check if we have this tile loaded
            uint packedGridPos = PackTileID(x, y);
            if (!mmap.loadedTileRefs.ContainsKey(packedGridPos))
            {
                // file may not exist, therefore not loaded
                Log.outDebug(LogFilter.Maps, 
                    $"MMAP:unloadMap: Asked to unload not loaded navmesh tile. " +
                    $"{mapId:D4}{x:D2}{y:D2}.mmtile");

                return false;
            }

            ulong tileRef = mmap.loadedTileRefs[packedGridPos];

            // unload, and mark as non loaded
            if (Detour.dtStatusFailed(mmap.navMesh.removeTile(tileRef, out _)))
            {
                // this is technically a memory leak
                // if the grid is later reloaded, dtNavMesh.addTile will return error but no extra memory is used
                // we cannot recover from this error - assert out
                Log.outError(LogFilter.Maps, 
                    $"MMAP:unloadMap: Could not unload " +
                    $"{mapId:D4}{x:D2}{y:D2}.mmtile from navmesh");
                Cypher.Assert(false);
            }
            else
            {
                mmap.loadedTileRefs.Remove(packedGridPos);
                --loadedTiles;
                Log.outInfo(LogFilter.Maps, 
                    $"MMAP:unloadMap: Unloaded mmtile {mapId:D4}[{x:D2}, {y:D2}] from {mapId:D4}");
                return true;
            }

            return false;
        }

        public bool UnloadMap(int mapId)
        {
            if (!loadedMMaps.ContainsKey(mapId))
            {
                // file may not exist, therefore not loaded
                Log.outDebug(LogFilter.Maps, 
                    $"MMAP:unloadMap: Asked to unload not loaded navmesh map {mapId:D4}");
                return false;
            }

            // unload all tiles from given map
            MMapData mmap = loadedMMaps.LookupByKey(mapId);
            foreach (var i in mmap.loadedTileRefs)
            {
                uint x = (i.Key >> 16) & 0x0000FFFF;
                uint y = i.Key & 0x0000FFFF;
                if (Detour.dtStatusFailed(mmap.navMesh.removeTile(i.Value, out _)))
                {
                    Log.outError(LogFilter.Maps,
                        $"MMAP:unloadMap: Could not unload {mapId:D4}{x:D2}{y:D2}.mmtile from navmesh");
                }
                else
                {
                    --loadedTiles;
                    Log.outInfo(LogFilter.Maps, 
                        $"MMAP:unloadMap: Unloaded mmtile {mapId:D4} [{x:D2}, {y:D2}] from {mapId:D4}");
                }
            }

            loadedMMaps.Remove(mapId);
            Log.outInfo(LogFilter.Maps, $"MMAP:unloadMap: Unloaded {mapId:D4}.mmap");

            return true;
        }

        public bool UnloadMapInstance(int meshMapId, int instanceMapId, int instanceId)
        {
            // check if we have this map loaded
            MMapData mmap = GetMMapData(meshMapId);
            if (mmap == null)
            {
                // file may not exist, therefore not loaded
                Log.outDebug(LogFilter.Maps, 
                    $"MMAP:unloadMapInstance: Asked to unload not loaded navmesh map {meshMapId}");
                return false;
            }

            if (!mmap.navMeshQueries.ContainsKey((instanceMapId, instanceId)))
            {
                Log.outDebug(LogFilter.Maps, 
                    $"MMAP:unloadMapInstance: Asked to unload not loaded dtNavMeshQuery " +
                    $"mapId {instanceMapId} instanceId {instanceId}");
                return false;
            }

            mmap.navMeshQueries.Remove((instanceMapId, instanceId));
            Log.outInfo(LogFilter.Maps, 
                $"MMAP:unloadMapInstance: Unloaded mapId {instanceMapId} instanceId {instanceId}");

            return true;
        }

        public Detour.dtNavMesh GetNavMesh(int mapId)
        {
            MMapData mmap = GetMMapData(mapId);
            if (mmap == null)
                return null;

            return mmap.navMesh;
        }

        public Detour.dtNavMeshQuery GetNavMeshQuery(int meshMapId, int instanceMapId, int instanceId)
        {
            MMapData mmap = GetMMapData(meshMapId);
            if (mmap == null)
                return null;

            return mmap.navMeshQueries.LookupByKey((instanceMapId, instanceId));
        }

        public uint GetLoadedTilesCount() { return loadedTiles; }
        public int GetLoadedMapsCount() { return loadedMMaps.Count; }

        Dictionary<int, MMapData> loadedMMaps = new();
        uint loadedTiles;

        Dictionary<int, int> parentMapData = new();
    }

    public class MMapData
    {
        public MMapData(Detour.dtNavMesh mesh)
        {
            navMesh = mesh;
        }

        public Dictionary<(int, int), Detour.dtNavMeshQuery> navMeshQueries = new();     // instanceId to query

        public Detour.dtNavMesh navMesh;
        public Dictionary<uint, ulong> loadedTileRefs = new(); // maps [map grid coords] to [dtTile]
    }

    public struct MmapTileHeader
    {
        public uint mmapMagic;
        public uint dtVersion;
        public uint mmapVersion;
        public uint size;
        public byte usesLiquids;
    }
}
