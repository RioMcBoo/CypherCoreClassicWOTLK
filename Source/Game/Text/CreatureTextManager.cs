﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Framework.Database;
using Game.Chat;
using Game.DataStorage;
using Game.Entities;
using Game.Groups;
using Game.Maps;
using Game.Networking;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public sealed class CreatureTextManager : Singleton<CreatureTextManager>
    {
        CreatureTextManager() { }

        public void LoadCreatureTexts()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mTextMap.Clear(); // for reload case
            //all currently used temp texts are NOT reset

            PreparedStatement stmt = WorldDatabase.GetPreparedStatement(WorldStatements.SEL_CREATURE_TEXT);
            SQLResult result = DB.World.Query(stmt);

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 ceature texts. DB table `creature_texts` is empty.");
                return;
            }

            uint textCount = 0;
            uint creatureCount = 0;

            do
            {
                CreatureTextEntry temp = new();

                temp.creatureId = result.Read<int>(0);
                temp.groupId = result.Read<byte>(1);
                temp.id = result.Read<byte>(2);
                temp.text = result.Read<string>(3);
                temp.type = (ChatMsg)result.Read<byte>(4);
                temp.lang = (Language)result.Read<byte>(5);
                temp.probability = result.Read<float>(6);
                temp.emote = (Emote)result.Read<uint>(7);
                temp.duration = result.Read<uint>(8);
                temp.sound = result.Read<int>(9);
                temp.SoundPlayType = (SoundKitPlayType)result.Read<byte>(10);
                temp.BroadcastTextId = result.Read<int>(11);
                temp.TextRange = (CreatureTextRange)result.Read<byte>(12);

                if (temp.sound != 0)
                {
                    if (!CliDB.SoundKitStorage.ContainsKey(temp.sound))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"GossipManager: Entry {temp.creatureId}, Group {temp.groupId} " +
                            $"in table `creature_texts` has Sound {temp.sound} but sound does not exist.");
                        temp.sound = 0;
                    }
                }

                if (temp.SoundPlayType >= SoundKitPlayType.Max)
                {
                    Log.outError(LogFilter.Sql, 
                        $"CreatureTextMgr: Entry {temp.creatureId}, Group {temp.groupId} " +
                        $"in table `creature_text` has PlayType {temp.SoundPlayType} but does not exist.");
                    temp.SoundPlayType = SoundKitPlayType.Normal;
                }

                if (temp.lang != Language.Universal && !Global.LanguageMgr.IsLanguageExist(temp.lang))
                {
                    Log.outError(LogFilter.Sql, 
                        $"CreatureTextMgr: Entry {temp.creatureId}, Group {temp.groupId} " +
                        $"in table `creature_texts` using Language {temp.lang} but Language does not exist.");
                    temp.lang = Language.Universal;
                }

                if (temp.type >= ChatMsg.Max)
                {
                    Log.outError(LogFilter.Sql, 
                        $"CreatureTextMgr: Entry {temp.creatureId}, Group {temp.groupId} " +
                        $"in table `creature_texts` has Type {temp.type} but this Chat Type does not exist.");
                    temp.type = ChatMsg.Say;
                }

                if (temp.emote != 0)
                {
                    if (!CliDB.EmotesStorage.ContainsKey((int)temp.emote))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"CreatureTextMgr: Entry {temp.creatureId}, Group {temp.groupId} " +
                            $"in table `creature_texts` has Emote {temp.emote} but emote does not exist.");
                        temp.emote = Emote.OneshotNone;
                    }
                }

                if (temp.BroadcastTextId != 0)
                {
                    if (!CliDB.BroadcastTextStorage.ContainsKey(temp.BroadcastTextId))
                    {
                        Log.outError(LogFilter.Sql, 
                            $"CreatureTextMgr: Entry {temp.creatureId}, Group {temp.groupId}, Id {temp.id} " +
                            $"in table `creature_texts` has non-existing " +
                            $"or incompatible BroadcastTextId {temp.BroadcastTextId}.");
                        temp.BroadcastTextId = 0;
                    }
                }

                if (temp.TextRange > CreatureTextRange.Personal)
                {
                    Log.outError(LogFilter.Sql, 
                        $"CreatureTextMgr: Entry {temp.creatureId}, Group {temp.groupId}, Id {temp.id} " +
                        $"in table `creature_text` has incorrect TextRange {temp.TextRange}.");
                    temp.TextRange = CreatureTextRange.Normal;
                }

                if (!mTextMap.ContainsKey(temp.creatureId))
                {
                    mTextMap[temp.creatureId] = new MultiMap<byte,CreatureTextEntry>();
                    ++creatureCount;
                }

                mTextMap[temp.creatureId].Add(temp.groupId, temp);
                ++textCount;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {textCount} creature texts for {creatureCount} creatures in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadCreatureTextLocales()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            mLocaleTextMap.Clear(); // for reload case

            SQLResult result = DB.World.Query("SELECT CreatureId, GroupId, ID, Locale, Text FROM creature_text_locale");

            if (result.IsEmpty())
                return;

            do
            {
                int creatureId = result.Read<int>(0);
                int groupId = result.Read<byte>(1);
                int id = result.Read<byte>(2);
                string localeName = result.Read<string>(3);
                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale) || locale == Locale.enUS)
                    continue;

                var key = new CreatureTextId(creatureId, groupId, id);
                if (!mLocaleTextMap.ContainsKey(key))
                    mLocaleTextMap[key] = new CreatureTextLocale();

                CreatureTextLocale data = mLocaleTextMap[key];
                ObjectManager.AddLocaleString(result.Read<string>(4), locale, data.Text);

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {mLocaleTextMap.Count} creature localized texts in {Time.Diff(oldMSTime)} ms.");
        }

        public uint SendChat(Creature source, byte textGroup, WorldObject whisperTarget = null, ChatMsg msgType = ChatMsg.Addon, Language language = Language.Addon,
            CreatureTextRange range = CreatureTextRange.Normal, int sound = 0, SoundKitPlayType playType = SoundKitPlayType.Normal, Team team = Team.Other, bool gmOnly = false, Player srcPlr = null)
        {
            if (source == null)
                return 0;

            var sList = mTextMap.LookupByKey(source.GetEntry());
            if (sList == null)
            {
                Log.outError(LogFilter.Sql, 
                    $"GossipManager: Could not find Text " +
                    $"for Creature({source.GetName()}) Entry {source.GetEntry()} " +
                    $"in 'creature_text' table. Ignoring.");
                return 0;
            }

            var textGroupContainer = sList[textGroup];
            if (textGroupContainer.Empty())
            {
                Log.outError(LogFilter.ChatSystem, 
                    $"GossipManager: Could not find TextGroup {textGroup} " +
                    $"for Creature({source.GetName()}) GuidLow {source.GetGUID()} Entry {source.GetEntry()}. " +
                    $"Ignoring.");
                return 0;
            }

            List<CreatureTextEntry> tempGroup = new();
            var repeatGroup = source.GetTextRepeatGroup(textGroup);

            foreach (var entry in textGroupContainer)
            {
                if (!repeatGroup.Contains(entry.id))
                    tempGroup.Add(entry);
            }

            if (tempGroup.Empty())
            {
                source.ClearTextRepeatGroup(textGroup);
                tempGroup = textGroupContainer.ToList();
            }

            var textEntry = tempGroup.SelectRandomElementByWeight(t => t.probability);

            ChatMsg finalType = (msgType == ChatMsg.Addon) ? textEntry.type : msgType;
            Language finalLang = (language == Language.Addon) ? textEntry.lang : language;
            int finalSound = textEntry.sound;
            SoundKitPlayType finalPlayType = textEntry.SoundPlayType;
            if (sound != 0)
            {
                finalSound = sound;
                finalPlayType = playType;
            }
            else
            {
                BroadcastTextRecord bct = CliDB.BroadcastTextStorage.LookupByKey(textEntry.BroadcastTextId);
                if (bct != null)
                {
                    int broadcastTextSoundId = bct.SoundKitID[source.GetGender() == Gender.Female ? 1 : 0];
                    if (broadcastTextSoundId != 0)
                        finalSound = broadcastTextSoundId;
                }
            }

            if (range == CreatureTextRange.Normal)
                range = textEntry.TextRange;

            if (finalSound != 0)
                SendSound(source, finalSound, finalType, whisperTarget, range, team, gmOnly, textEntry.BroadcastTextId, finalPlayType);

            Unit finalSource = source;
            if (srcPlr != null)
                finalSource = srcPlr;

            if (textEntry.emote != 0)
                SendEmote(finalSource, textEntry.emote);

            if (srcPlr != null)
            {
                PlayerTextBuilder builder = new(source, finalSource, finalSource.GetGender(), finalType, textEntry.groupId, textEntry.id, finalLang, whisperTarget);
                SendChatPacket(finalSource, builder, finalType, whisperTarget, range, team, gmOnly);
            }
            else
            {
                CreatureTextBuilder builder = new(finalSource, finalSource.GetGender(), finalType, textEntry.groupId, textEntry.id, finalLang, whisperTarget);
                SendChatPacket(finalSource, builder, finalType, whisperTarget, range, team, gmOnly);
            }

            source.SetTextRepeatId(textGroup, textEntry.id);
            return textEntry.duration;
        }

        public float GetRangeForChatType(ChatMsg msgType)
        {
            float dist = WorldConfig.Values[WorldCfg.ListenRangeSay].Float;
            switch (msgType)
            {
                case ChatMsg.MonsterYell:
                    dist = WorldConfig.Values[WorldCfg.ListenRangeYell].Float;
                    break;
                case ChatMsg.MonsterEmote:
                case ChatMsg.RaidBossEmote:
                    dist = WorldConfig.Values[WorldCfg.ListenRangeTextemote].Float;
                    break;
                default:
                    break;
            }

            return dist;
        }

        public void SendSound(Creature source, int sound, ChatMsg msgType, WorldObject whisperTarget = null, CreatureTextRange range = CreatureTextRange.Normal, Team team = Team.Other, bool gmOnly = false, int keyBroadcastTextId = 0, SoundKitPlayType playType = SoundKitPlayType.Normal)
        {
            if (sound == 0 || source == null)
                return;

            if (playType == SoundKitPlayType.ObjectSound)
            {
                PlayObjectSound pkt = new();
                pkt.TargetObjectGUID = whisperTarget.GetGUID();
                pkt.SourceObjectGUID = source.GetGUID();
                pkt.SoundKitID = sound;
                pkt.Position = whisperTarget.GetWorldLocation();
                pkt.BroadcastTextID = keyBroadcastTextId;
                SendNonChatPacket(source, pkt, msgType, whisperTarget, range, team, gmOnly);
            }
            else if (playType == SoundKitPlayType.Normal)
                SendNonChatPacket(source, new PlaySound(source.GetGUID(), sound, keyBroadcastTextId), msgType, whisperTarget, range, team, gmOnly);
        }

        void SendNonChatPacket(WorldObject source, ServerPacket data, ChatMsg msgType, WorldObject whisperTarget, CreatureTextRange range, Team team, bool gmOnly)
        {
            float dist = GetRangeForChatType(msgType);

            switch (msgType)
            {
                case ChatMsg.MonsterParty:
                    if (whisperTarget == null)
                        return;

                    Player whisperPlayer = whisperTarget.ToPlayer();
                    if (whisperPlayer != null)
                    {
                        Group group = whisperPlayer.GetGroup();
                        if (group != null)
                            group.BroadcastWorker(player => player.SendPacket(data));
                    }
                    return;
                case ChatMsg.MonsterWhisper:
                case ChatMsg.RaidBossWhisper:
                    {
                        if (range == CreatureTextRange.Normal)//ignores team and gmOnly
                        {
                            if (whisperTarget == null || !whisperTarget.IsTypeId(TypeId.Player))
                                return;

                            whisperTarget.ToPlayer().SendPacket(data);
                            return;
                        }
                        break;
                    }
                default:
                    break;
            }

            switch (range)
            {
                case CreatureTextRange.Area:
                    {
                        int areaId = source.GetAreaId();
                        var players = source.GetMap().GetPlayers();
                        foreach (var pl in players)
                            if (pl.GetAreaId() == areaId && (team == 0 || pl.GetEffectiveTeam() == team) && (!gmOnly || pl.IsGameMaster()))
                                pl.SendPacket(data);
                        return;
                    }
                case CreatureTextRange.Zone:
                    {
                        int zoneId = source.GetZoneId();
                        var players = source.GetMap().GetPlayers();
                        foreach (var pl in players)
                            if (pl.GetZoneId() == zoneId && (team == 0 || pl.GetEffectiveTeam() == team) && (!gmOnly || pl.IsGameMaster()))
                                pl.SendPacket(data);
                        return;
                    }
                case CreatureTextRange.Map:
                    {
                        var players = source.GetMap().GetPlayers();
                        foreach (var pl in players)
                            if ((team == 0 || pl.GetEffectiveTeam() == team) && (!gmOnly || pl.IsGameMaster()))
                                pl.SendPacket(data);
                        return;
                    }
                case CreatureTextRange.World:
                    {
                        var smap = Global.WorldMgr.GetAllSessions();
                        foreach (var session in smap)
                        {
                            Player player = session.GetPlayer();
                            if (player != null)
                                if ((team == 0 || player.GetTeam() == team) && (!gmOnly || player.IsGameMaster()))
                                    player.SendPacket(data);
                        }
                        return;
                    }
                case CreatureTextRange.Personal:
                    if (whisperTarget == null || !whisperTarget.IsPlayer())
                        return;

                    whisperTarget.ToPlayer().SendPacket(data);
                    return;
                case CreatureTextRange.Normal:
                default:
                    break;
            }

            source.SendMessageToSetInRange(data, dist, true);
        }

        void SendEmote(Unit source, Emote emote)
        {
            if (source == null)
                return;

            source.HandleEmoteCommand(emote);
        }

        public bool TextExist(int sourceEntry, byte textGroup)
        {
            if (sourceEntry == 0)
                return false;

            var textHolder = mTextMap.LookupByKey(sourceEntry);
            if (textHolder == null)
            {
                Log.outDebug(LogFilter.Unit, 
                    $"CreatureTextMgr.TextExist: Could not find Text " +
                    $"for Creature (entry {sourceEntry}) in 'creature_text' table.");
                return false;
            }

            var textEntryList = textHolder[textGroup];
            if (textEntryList.Empty())
            {
                Log.outDebug(LogFilter.Unit, 
                    $"CreatureTextMgr.TextExist: Could not find TextGroup {textGroup} " +
                    $"for Creature (entry {sourceEntry}).");
                return false;
            }

            return true;
        }

        public string GetLocalizedChatString(int entry, Gender gender, byte textGroup, int id, Locale locale = Locale.enUS)
        {
            var multiMap = mTextMap.LookupByKey(entry);
            if (multiMap == null)
                return "";

            var creatureTextEntryList = multiMap[textGroup];
            if (creatureTextEntryList.Empty())
                return "";

            CreatureTextEntry creatureTextEntry = null;
            for (var i = 0; i != creatureTextEntryList.Count; ++i)
            {
                creatureTextEntry = creatureTextEntryList[i];
                if (creatureTextEntry.id == id)
                    break;
            }

            if (creatureTextEntry == null)
                return "";

            if (locale >= Locale.Total)
                locale = Locale.enUS;

            string baseText;
            BroadcastTextRecord bct = CliDB.BroadcastTextStorage.LookupByKey(creatureTextEntry.BroadcastTextId);

            if (bct != null)
                baseText = Global.DB2Mgr.GetBroadcastTextValue(bct, locale, gender);
            else
                baseText = creatureTextEntry.text;

            if (locale != Locale.enUS && bct == null)
            {
                var creatureTextLocale = mLocaleTextMap.LookupByKey(new CreatureTextId(entry, textGroup, id));
                if (creatureTextLocale != null)
                    ObjectManager.GetLocaleString(creatureTextLocale.Text, locale, ref baseText);
            }

            return baseText;
        }

        public void SendChatPacket(WorldObject source, MessageBuilder builder, ChatMsg msgType, WorldObject whisperTarget = null, CreatureTextRange range = CreatureTextRange.Normal, Team team = Team.Other, bool gmOnly = false)
        {
            if (source == null)
                return;

            var localizer = new CreatureTextLocalizer(builder, msgType);

            switch (msgType)
            {
                case ChatMsg.MonsterWhisper:
                case ChatMsg.RaidBossWhisper:
                    {
                        if (range == CreatureTextRange.Normal) //ignores team and gmOnly
                        {
                            if (whisperTarget == null || !whisperTarget.IsTypeId(TypeId.Player))
                                return;

                            localizer.Invoke(whisperTarget.ToPlayer());
                            return;
                        }
                        break;
                    }
                default:
                    break;
            }

            switch (range)
            {
                case CreatureTextRange.Area:
                    {
                        int areaId = source.GetAreaId();
                        var players = source.GetMap().GetPlayers();
                        foreach (var pl in players)
                            if (pl.GetAreaId() == areaId && (team == 0 || pl.GetEffectiveTeam() == team) && (!gmOnly || pl.IsGameMaster()))
                                localizer.Invoke(pl);
                        return;
                    }
                case CreatureTextRange.Zone:
                    {
                        int zoneId = source.GetZoneId();
                        var players = source.GetMap().GetPlayers();
                        foreach (var pl in players)
                            if (pl.GetZoneId() == zoneId && (team == 0 || pl.GetEffectiveTeam() == team) && (!gmOnly || pl.IsGameMaster()))
                                localizer.Invoke(pl);
                        return;
                    }
                case CreatureTextRange.Map:
                    {
                        var players = source.GetMap().GetPlayers();
                        foreach (var pl in players)
                            if ((team == 0 || pl.GetEffectiveTeam() == team) && (!gmOnly || pl.IsGameMaster()))
                                localizer.Invoke(pl);
                        return;
                    }
                case CreatureTextRange.World:
                    {
                        var smap = Global.WorldMgr.GetAllSessions();
                        foreach (var session in smap)
                        {
                            Player player = session.GetPlayer();
                            if (player != null)
                                if ((team == 0 || player.GetTeam() == team) && (!gmOnly || player.IsGameMaster()))
                                    localizer.Invoke(player);
                        }
                        return;
                    }
                case CreatureTextRange.Personal:
                    if (whisperTarget == null || !whisperTarget.IsPlayer())
                        return;

                    localizer.Invoke(whisperTarget.ToPlayer());
                    return;
                case CreatureTextRange.Normal:
                default:
                    break;
            }

            float dist = GetRangeForChatType(msgType);
            var worker = new PlayerDistWorker(source, dist, localizer);
            Cell.VisitWorldObjects(source, worker, dist);
        }

        Dictionary<int, MultiMap<byte, CreatureTextEntry>> mTextMap = new();
        Dictionary<CreatureTextId, CreatureTextLocale> mLocaleTextMap = new();
    }

    public class CreatureTextEntry
    {
        public int creatureId;
        public byte groupId;
        public byte id;
        public string text;
        public ChatMsg type;
        public Language lang;
        public float probability;
        public Emote emote;
        public uint duration;
        public int sound;
        public SoundKitPlayType SoundPlayType;
        public int BroadcastTextId;
        public CreatureTextRange TextRange;
    }

    public class CreatureTextLocale
    {
        public StringArray Text = new((int)Locale.Total);
    }

    public class CreatureTextId
    {
        public CreatureTextId(int e, int g, int i)
        {
            entry = e;
            textGroup = g;
            textId = i;
        }

        public int entry;
        public int textGroup;
        public int textId;
    }

    public enum CreatureTextRange
    {
        Normal = 0,
        Area = 1,
        Zone = 2,
        Map = 3,
        World = 4,
        Personal = 5
    }

    public enum SoundKitPlayType
    {
        Normal = 0,
        ObjectSound = 1,
        Max = 2,
    }

    public class CreatureTextLocalizer : IDoWork<Player>
    {
        public CreatureTextLocalizer(MessageBuilder builder, ChatMsg msgType)
        {
            _builder = builder;
            _msgType = msgType;
        }

        public void Invoke(Player player)
        {
            Locale loc_idx = player.GetSession().GetSessionDbLocaleIndex();
            ChatPacketSender sender;

            // create if not cached yet
            if (!_packetCache.ContainsKey(loc_idx))
            {
                sender = _builder.Invoke(loc_idx);
                _packetCache[loc_idx] = sender;
            }
            else
                sender = _packetCache[loc_idx];

            switch (_msgType)
            {
                case ChatMsg.MonsterWhisper:
                case ChatMsg.RaidBossWhisper:
                    ChatPkt message = sender.UntranslatedPacket;
                    message.SetReceiver(player, loc_idx);
                    player.SendPacket(message);
                    break;
                default:
                    break;
            }

            sender.Invoke(player);
        }

        Dictionary<Locale, ChatPacketSender> _packetCache = new();
        MessageBuilder _builder;
        ChatMsg _msgType;
    }

    public class CreatureTextBuilder : MessageBuilder
    {
        public CreatureTextBuilder(WorldObject obj, Gender gender, ChatMsg msgtype, byte textGroup, int id, Language language, WorldObject target)
        {
            _source = obj;
            _gender = gender;
            _msgType = msgtype;
            _textGroup = textGroup;
            _textId = id;
            _language = language;
            _target = target;
        }

        public override ChatPacketSender Invoke(Locale locale = Locale.enUS)
        {
            string text = Global.CreatureTextMgr.GetLocalizedChatString(_source.GetEntry(), _gender, _textGroup, _textId, locale);
            return new ChatPacketSender(_msgType, _language, _source, _target, text, 0, locale);
        }

        WorldObject _source;
        Gender _gender;
        ChatMsg _msgType;
        byte _textGroup;
        int _textId;
        Language _language;
        WorldObject _target;
    }

    public class PlayerTextBuilder : MessageBuilder
    {
        public PlayerTextBuilder(WorldObject obj, WorldObject speaker, Gender gender, ChatMsg msgtype, byte textGroup, int id, Language language, WorldObject target)
        {
            _source = obj;
            _gender = gender;
            _talker = speaker;
            _msgType = msgtype;
            _textGroup = textGroup;
            _textId = id;
            _language = language;
            _target = target;
        }

        public override PacketSenderOwning<ChatPkt> Invoke(Locale loc_idx = Locale.enUS)
        {
            string text = Global.CreatureTextMgr.GetLocalizedChatString(_source.GetEntry(), _gender, _textGroup, _textId, loc_idx);
            PacketSenderOwning<ChatPkt> chat = new();
            chat.Data.Initialize(_msgType, _language, _talker, _target, text, 0, "", loc_idx);
            return chat;
        }

        WorldObject _source;
        WorldObject _talker;
        Gender _gender;
        ChatMsg _msgType;
        byte _textGroup;
        int _textId;
        Language _language;
        WorldObject _target;
    }
}