// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Realm;
using Game.DataStorage;
using Game.Entities;
using Game.Maps;
using Game.Misc;
using Game.Networking;
using Game.Networking.Packets;
using System.Collections.Generic;
using System.Numerics;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.QueryPlayerNames, Processing = PacketProcessing.Inplace)]
        void HandleQueryPlayerNames(QueryPlayerNames queryPlayerName)
        {
            QueryPlayerNamesResponse response = new();
            foreach (ObjectGuid guid in queryPlayerName.Players)
            {
                BuildNameQueryData(guid, out NameCacheLookupResult nameCacheLookupResult);
                response.Players.Add(nameCacheLookupResult);
            }

            SendPacket(response);
        }

        public void BuildNameQueryData(ObjectGuid guid, out NameCacheLookupResult lookupData)
        {
            lookupData = new();

            Player player = Global.ObjAccessor.FindPlayer(guid);

            lookupData.Player = guid;

            lookupData.Data = new();
            if (lookupData.Data.Initialize(guid, player))
                lookupData.Result = (byte)ResponseCodes.Success;
            else
                lookupData.Result = (byte)ResponseCodes.Failure; // name unknown
        }

        [WorldPacketHandler(ClientOpcodes.QueryTime, Processing = PacketProcessing.Inplace)]
        void HandleQueryTime(QueryTime packet)
        {
            SendQueryTimeResponse();
        }

        void SendQueryTimeResponse()
        {
            QueryTimeResponse queryTimeResponse = new();
            queryTimeResponse.CurrentTime = LoopTime.RealmTime;
            SendPacket(queryTimeResponse);
        }

        [WorldPacketHandler(ClientOpcodes.QueryGameObject, Processing = PacketProcessing.Inplace)]
        void HandleGameObjectQuery(QueryGameObject packet)
        {
            GameObjectTemplate info = Global.ObjectMgr.GetGameObjectTemplate(packet.GameObjectID);
            if (info != null)
            {
                if (!WorldConfig.Values[WorldCfg.CacheDataQueries].Bool)
                    info.InitializeQueryData();

                QueryGameObjectResponse queryGameObjectResponse = info.QueryData;

                Locale loc = GetSessionDbLocaleIndex();
                if (loc != Locale.enUS)
                {
                    GameObjectLocale gameObjectLocale = Global.ObjectMgr.GetGameObjectLocale(queryGameObjectResponse.GameObjectID);
                    if (gameObjectLocale != null)
                    {
                        ObjectManager.GetLocaleString(gameObjectLocale.Name, loc, ref queryGameObjectResponse.Stats.Name[0]);
                        ObjectManager.GetLocaleString(gameObjectLocale.CastBarCaption, loc, ref queryGameObjectResponse.Stats.CastBarCaption);
                        ObjectManager.GetLocaleString(gameObjectLocale.Unk1, loc, ref queryGameObjectResponse.Stats.UnkString);
                    }
                }

                SendPacket(queryGameObjectResponse);
            }
            else
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: CMSG_GAMEOBJECT_QUERY - " +
                    $"Missing gameobject info for (ENTRY: {packet.GameObjectID})");

                QueryGameObjectResponse response = new();
                response.GameObjectID = packet.GameObjectID;
                response.Guid = packet.Guid;
                SendPacket(response);
            }
        }

        [WorldPacketHandler(ClientOpcodes.QueryCreature, Processing = PacketProcessing.Inplace)]
        void HandleCreatureQuery(QueryCreature packet)
        {
            CreatureTemplate ci = Global.ObjectMgr.GetCreatureTemplate(packet.CreatureID);
            if (ci != null)
            {
                Difficulty difficulty = _player.GetMap().GetDifficultyID();

                // Cache only exists for difficulty base
                if (!WorldConfig.Values[WorldCfg.CacheDataQueries].Bool && difficulty == Difficulty.None)
                    SendPacket(ci.QueryData[(int)GetSessionDbLocaleIndex()]);
                else
                {
                    var response = ci.BuildQueryData(GetSessionDbLocaleIndex(), difficulty);
                    SendPacket(response);
                }
            }
            else
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: CMSG_QUERY_CREATURE - " +
                    $"NO CREATURE INFO! (ENTRY: {packet.CreatureID})");

                QueryCreatureResponse response = new();
                response.CreatureID = packet.CreatureID;
                SendPacket(response);
            }
        }

        [WorldPacketHandler(ClientOpcodes.QueryNpcText, Processing = PacketProcessing.Inplace)]
        void HandleNpcTextQuery(QueryNPCText packet)
        {
            NpcText npcText = Global.ObjectMgr.GetNpcText(packet.TextID);

            QueryNPCTextResponse response = new();
            response.TextID = packet.TextID;

            if (npcText != null)
            {
                for (byte i = 0; i < SharedConst.MaxNpcTextOptions; ++i)
                {
                    response.Probabilities[i] = npcText.Data[i].Probability;
                    response.BroadcastTextID[i] = npcText.Data[i].BroadcastTextID;
                    if (!response.Allow && npcText.Data[i].BroadcastTextID != 0)
                        response.Allow = true;
                }
            }

            if (!response.Allow)
            {
                Log.outError(LogFilter.Sql,
                    $"HandleNpcTextQuery: no BroadcastTextID found " +
                    $"for text {packet.TextID} in `npc_text table`");
            }

            SendPacket(response);
        }

        [WorldPacketHandler(ClientOpcodes.QueryPageText, Processing = PacketProcessing.Inplace)]
        void HandleQueryPageText(QueryPageText packet)
        {
            QueryPageTextResponse response = new();
            response.PageTextID = packet.PageTextID;

            int pageID = packet.PageTextID;
            while (pageID != 0)
            {
                PageText pageText = Global.ObjectMgr.GetPageText(pageID);

                if (pageText == null)
                    break;

                QueryPageTextResponse.PageTextInfo page;
                page.Id = pageID;
                page.NextPageID = pageText.NextPageID;
                page.Text = pageText.Text;
                page.PlayerConditionID = pageText.PlayerConditionID;
                page.Flags = pageText.Flags;

                Locale locale = GetSessionDbLocaleIndex();
                if (locale != Locale.enUS)
                {
                    PageTextLocale pageLocale = Global.ObjectMgr.GetPageTextLocale(pageID);
                    if (pageLocale != null)
                        ObjectManager.GetLocaleString(pageLocale.Text, locale, ref page.Text);
                }

                response.Pages.Add(page);
                pageID = pageText.NextPageID;
            }

            response.Allow = !response.Pages.Empty();
            SendPacket(response);
        }

        [WorldPacketHandler(ClientOpcodes.QueryCorpseLocationFromClient)]
        void HandleQueryCorpseLocation(QueryCorpseLocationFromClient queryCorpseLocation)
        {
            CorpseLocation packet = new();
            Player player = Global.ObjAccessor.FindConnectedPlayer(queryCorpseLocation.Player);
            if (player == null || !player.HasCorpse() || !_player.IsInSameRaidWith(player))
            {
                packet.Valid = false;                               // corpse not found
                packet.Player = queryCorpseLocation.Player;
                SendPacket(packet);
                return;
            }

            WorldLocation corpseLocation = player.GetCorpseLocation();
            int corpseMapID = corpseLocation.GetMapId();
            int mapID = corpseLocation.GetMapId();
            var pos = corpseLocation.GetPosition3D();

            // if corpse at different map
            if (mapID != player.GetMapId())
            {
                // search entrance map for proper show entrance
                MapRecord corpseMapEntry = CliDB.MapStorage.LookupByKey(mapID);
                if (corpseMapEntry != null)
                {
                    if (corpseMapEntry.IsDungeon && corpseMapEntry.CorpseMapID >= 0)
                    {
                        // if corpse map have entrance
                        TerrainInfo entranceTerrain = Global.TerrainMgr.LoadTerrain(corpseMapEntry.CorpseMapID);
                        if (entranceTerrain != null)
                        {
                            mapID = corpseMapEntry.CorpseMapID;

                            pos = new Vector3(Global.ObjectMgr.GetMapCorpsePosition(corpseMapEntry.Id),
                                entranceTerrain.GetStaticHeight(player.GetPhaseShift(), mapID, pos.X, pos.Y, MapConst.MaxHeight));
                        }
                    }
                }
            }

            packet.Valid = true;
            packet.Player = queryCorpseLocation.Player;
            packet.MapID = corpseMapID;
            packet.ActualMapID = mapID;
            packet.Position = pos;
            packet.Transport = ObjectGuid.Empty;
            SendPacket(packet);
        }

        [WorldPacketHandler(ClientOpcodes.QueryCorpseTransport)]
        void HandleQueryCorpseTransport(QueryCorpseTransport queryCorpseTransport)
        {
            CorpseTransportQuery response = new();
            response.Player = queryCorpseTransport.Player;

            Player player = Global.ObjAccessor.FindConnectedPlayer(queryCorpseTransport.Player);
            if (player != null)
            {
                Corpse corpse = player.GetCorpse();
                if (_player.IsInSameRaidWith(player) && corpse != null && !corpse.GetTransGUID().IsEmpty() && corpse.GetTransGUID() == queryCorpseTransport.Transport)
                {
                    response.Position = new Vector3(corpse.GetTransOffsetX(), corpse.GetTransOffsetY(), corpse.GetTransOffsetZ());
                    response.Facing = corpse.GetTransOffsetO();
                }
            }

            SendPacket(response);
        }

        [WorldPacketHandler(ClientOpcodes.QueryQuestCompletionNpcs, Processing = PacketProcessing.Inplace)]
        void HandleQueryQuestCompletionNPCs(QueryQuestCompletionNPCs queryQuestCompletionNPCs)
        {
            QuestCompletionNPCResponse response = new();

            foreach (var questID in queryQuestCompletionNPCs.QuestCompletionNPCs)
            {
                QuestCompletionNPC questCompletionNPC = new();

                if (Global.ObjectMgr.GetQuestTemplate(questID) == null)
                {
                    Log.outDebug(LogFilter.Network, 
                        $"WORLD: Unknown quest {questID} " +
                        $"in CMSG_QUEST_NPC_QUERY by {GetPlayer().GetGUID()}");
                    continue;
                }

                questCompletionNPC.QuestID = questID;

                foreach (var id in Global.ObjectMgr.GetCreatureQuestInvolvedRelationReverseBounds(questID))
                    questCompletionNPC.NPCs.Add(id);

                foreach (var id in Global.ObjectMgr.GetGOQuestInvolvedRelationReverseBounds(questID))
                    questCompletionNPC.NPCs.Add(id | unchecked((int)0x80000000)); // GO mask

                response.QuestCompletionNPCs.Add(questCompletionNPC);
            }

            SendPacket(response);
        }

        [WorldPacketHandler(ClientOpcodes.ItemTextQuery, Processing = PacketProcessing.Inplace)]
        void HandleItemTextQuery(ItemTextQuery packet)
        {
            QueryItemTextResponse queryItemTextResponse = new();
            queryItemTextResponse.Id = packet.Id;

            Item item = GetPlayer().GetItemByGuid(packet.Id);
            if (item != null)
            {
                queryItemTextResponse.Valid = true;
                queryItemTextResponse.Text = item.GetText();
            }

            SendPacket(queryItemTextResponse);
        }

        [WorldPacketHandler(ClientOpcodes.QueryRealmName, Processing = PacketProcessing.Inplace)]
        void HandleQueryRealmName(QueryRealmName queryRealmName)
        {
            RealmQueryResponse realmQueryResponse = new();
            realmQueryResponse.VirtualRealmAddress = queryRealmName.VirtualRealmAddress;

            RealmId realmHandle = new(queryRealmName.VirtualRealmAddress);
            if (Global.RealmMgr.GetRealmNames(realmHandle, out realmQueryResponse.NameInfo.RealmNameActual, out realmQueryResponse.NameInfo.RealmNameNormalized))
            {
                realmQueryResponse.LookupState = (byte)ResponseCodes.Success;
                realmQueryResponse.NameInfo.IsInternalRealm = false;

                realmQueryResponse.NameInfo.IsLocal = 
                    queryRealmName.VirtualRealmAddress == Global.WorldMgr.GetVirtualRealmAddress();
            }
            else
                realmQueryResponse.LookupState = (byte)ResponseCodes.Failure;
        }
    }
}
