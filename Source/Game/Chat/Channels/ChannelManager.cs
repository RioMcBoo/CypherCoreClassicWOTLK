﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;

namespace Game.Chat
{
    public class ChannelManager
    {
        public static AreaTableRecord SpecialLinkedArea { get; private set; }

        public ChannelManager(Team team)
        {
            _team = team;
            _guidGenerator = new ObjectGuidGenerator(HighGuid.ChatChannel);
        }

        public static void LoadFromDB()
        {
            SpecialLinkedArea = CliDB.AreaTableStorage.LookupByKey(3459);
            Cypher.Assert(SpecialLinkedArea.HasFlag(AreaFlags.LinkedChatSpecialArea));

            if (!WorldConfig.Values[WorldCfg.PreserveCustomChannels].Bool)
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 custom chat channels. Custom channel saving is disabled.");
                return;
            }

            uint count = 0;
            RelativeTime oldMSTime = Time.NowRelative;
            TimeSpan cleanBefore = WorldConfig.Values[WorldCfg.PreserveCustomChannelDuration].TimeSpan;
            if (cleanBefore > TimeSpan.Zero)
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_OLD_CHANNELS);
                stmt.SetInt32(0, (Seconds)cleanBefore);
                DB.Characters.Execute(stmt);
            }

            {
                SQLResult result = DB.Characters.Query("SELECT name, team, announce, ownership, password, bannedList FROM channels");
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 custom chat channels. DB table `channels` is empty.");
                    return;
                }

                List<(string name, Team team)> toDelete = new();
                do
                {
                    string dbName = result.Read<string>(0); // may be different - channel names are case insensitive
                    Team team = (Team)result.Read<int>(1);
                    bool dbAnnounce = result.Read<bool>(2);
                    bool dbOwnership = result.Read<bool>(3);
                    string dbPass = result.Read<string>(4);
                    string dbBanned = result.Read<string>(5);

                    ChannelManager mgr = ForTeam(team);
                    if (mgr == null)
                    {
                        Log.outError(LogFilter.ServerLoading, 
                            $"Failed to load custom chat channel '{dbName}' from database - invalid team {team}. Deleted.");
                        toDelete.Add((dbName, team));
                        continue;
                    }

                    Channel channel = new Channel(mgr.CreateCustomChannelGuid(), dbName, team, dbBanned);
                    channel.SetAnnounce(dbAnnounce);
                    channel.SetOwnership(dbOwnership);
                    channel.SetPassword(dbPass);
                    mgr._customChannels.Add(dbName, channel);

                    ++count;
                } while (result.NextRow());


                foreach (var (name, team) in toDelete)
                {
                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHANNEL);
                    stmt.SetString(0, name);
                    stmt.SetUInt32(1, (uint)team);
                    DB.Characters.Execute(stmt);
                }
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} custom chat channels in {Time.Diff(oldMSTime)} ms.");
        }

        public static ChannelManager ForTeam(Team team)
        {
            if (WorldConfig.Values[WorldCfg.AllowTwoSideInteractionChannel].Bool)
                return allianceChannelMgr;        // cross-faction

            if (team == Team.Alliance)
                return allianceChannelMgr;

            if (team == Team.Horde)
                return hordeChannelMgr;

            return null;
        }

        public static Channel GetChannelForPlayerByNamePart(string namePart, Player playerSearcher)
        {
            foreach (Channel channel in playerSearcher.GetJoinedChannels())
            {
                string chanName = channel.GetName(playerSearcher.GetSession().GetSessionDbcLocale());
                if (chanName.ToLower().Equals(namePart.ToLower()))
                    return channel;
            }

            return null;
        }

        public void SaveToDB()
        {
            foreach (var pair in _customChannels)
                pair.Value.UpdateChannelInDB();
        }

        public static Channel GetChannelForPlayerByGuid(ObjectGuid channelGuid, Player playerSearcher)
        {
            foreach (Channel channel in playerSearcher.GetJoinedChannels())
                if (channel.GetGUID() == channelGuid)
                    return channel;

            return null;
        }

        public Channel GetSystemChannel(int channelId, AreaTableRecord zoneEntry = null)
        {
            ObjectGuid channelGuid = CreateBuiltinChannelGuid(channelId, zoneEntry);
            var currentChannel = _channels.LookupByKey(channelGuid);
            if (currentChannel != null)
                return currentChannel;

            Channel newChannel = new Channel(channelGuid, channelId, _team, zoneEntry);
            _channels[channelGuid] = newChannel;
            return newChannel;
        }

        public Channel CreateCustomChannel(string name)
        {
            if (_customChannels.ContainsKey(name.ToLower()))
                return null;

            Channel newChannel = new(CreateCustomChannelGuid(), name, _team);
            newChannel.SetDirty();

            _customChannels[name.ToLower()] = newChannel;
            return newChannel;
        }

        public Channel GetCustomChannel(string name)
        {
            return _customChannels.LookupByKey(name.ToLower());
        }

        public Channel GetChannel(int channelId, string name, Player player, bool notify = true, AreaTableRecord zoneEntry = null)
        {
            Channel result = null;
            if (channelId != 0) // builtin
            {
                var channel = _channels.LookupByKey(CreateBuiltinChannelGuid(channelId, zoneEntry));
                if (channel != null)
                    result = channel;
            }
            else // custom
            {
                var channel = _customChannels.LookupByKey(name.ToLower());
                if (channel != null)
                    result = channel;
            }

            if (result == null && notify)
            {
                string channelName = name;
                Channel.GetChannelName(ref channelName, channelId, player.GetSession().GetSessionDbcLocale(), zoneEntry);

                SendNotOnChannelNotify(player, channelName);
            }

            return result;
        }

        public void LeftChannel(int channelId, AreaTableRecord zoneEntry)
        {
            var guid = CreateBuiltinChannelGuid(channelId, zoneEntry);
            var channel = _channels.LookupByKey(guid);
            if (channel == null)
                return;

            if (channel.GetNumPlayers() == 0)
                _channels.Remove(guid);
        }

        public static void SendNotOnChannelNotify(Player player, string name)
        {
            ChannelNotify notify = new();
            notify.Type = ChatNotify.NotMemberNotice;
            notify.Channel = name;
            player.SendPacket(notify);
        }

        ObjectGuid CreateCustomChannelGuid()
        {
            long high = 0;
            high |= (long)HighGuid.ChatChannel << 58;
            high |= (long)Global.WorldMgr.GetRealmId().Index << 42;
            high |= (long)(_team == Team.Alliance ? 3 : 5) << 4;

            ObjectGuid channelGuid = new();
            channelGuid.SetRawValue(high, _guidGenerator.Generate());
            return channelGuid;
        }

        ObjectGuid CreateBuiltinChannelGuid(int channelId, AreaTableRecord zoneEntry = null)
        {
            ChatChannelsRecord channelEntry = CliDB.ChatChannelsStorage.LookupByKey(channelId);

            int zoneId = 0;

            if (zoneEntry != null && channelEntry.HasFlag(ChatChannelFlags.ZoneBased) && !channelEntry.HasFlag(ChatChannelFlags.LinkedChannel))
                zoneId = zoneEntry.Id;

            if (channelEntry.HasFlag(ChatChannelFlags.GlobalForTournament))
            {
                var category = CliDB.CfgCategoriesStorage.LookupByKey(Global.WorldMgr.GetRealm().Timezone);
                if (category != null && category.HasFlag(CfgCategoriesFlags.Tournament))
                    zoneId = 0;
            }

            return ObjectGuid.Create(HighGuid.ChatChannel, true, channelEntry.HasFlag(ChatChannelFlags.LinkedChannel), (ushort)zoneId, (byte)(_team == Team.Alliance ? 3 : 5), channelId);
        }

        Dictionary<string, Channel> _customChannels = new();
        Dictionary<ObjectGuid, Channel> _channels = new();
        Team _team;
        ObjectGuidGenerator _guidGenerator;

        static ChannelManager allianceChannelMgr = new(Team.Alliance);
        static ChannelManager hordeChannelMgr = new(Team.Horde);
    }
}
