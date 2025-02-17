﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    public class ChatMessage : ClientPacket
    {
        public ChatMessage(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Language = (Language)_worldPacket.ReadInt32();
            int len = _worldPacket.ReadBits<int>(11);
            switch (GetOpcode())
            {
                case ClientOpcodes.ChatMessageSay:
                case ClientOpcodes.ChatMessageParty:
                case ClientOpcodes.ChatMessageRaid:
                case ClientOpcodes.ChatMessageRaidWarning:
                case ClientOpcodes.ChatMessageInstanceChat:
                    IsSecure = _worldPacket.HasBit();
                    break;
                default:
                    break;
            }
            Text = _worldPacket.ReadString(len);
        }

        public string Text;
        public Language Language = Language.Universal;
        public bool IsSecure = true;
    }

    public class ChatMessageWhisper : ClientPacket
    {
        public ChatMessageWhisper(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Language = (Language)_worldPacket.ReadInt32();
            int targetLen = _worldPacket.ReadBits<int>(9);
            int textLen = _worldPacket.ReadBits<int>(11);
            Target = _worldPacket.ReadString(targetLen);
            Text = _worldPacket.ReadString(textLen);
        }

        public Language Language = Language.Universal;
        public string Text;
        public string Target;
    }

    public class ChatMessageChannel : ClientPacket
    {
        public ChatMessageChannel(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Language = (Language)_worldPacket.ReadInt32();
            ChannelGUID = _worldPacket.ReadPackedGuid();
            int targetLen = _worldPacket.ReadBits<int>(9);
            int textLen = _worldPacket.ReadBits<int>(11);
            if (_worldPacket.HasBit())
                IsSecure = _worldPacket.HasBit();

            Target = _worldPacket.ReadString(targetLen);
            Text = _worldPacket.ReadString(textLen);
        }

        public Language Language = Language.Universal;
        public ObjectGuid ChannelGUID;
        public string Text;
        public string Target;
        public bool? IsSecure;
    }

    public class ChatAddonMessage : ClientPacket
    {
        public ChatAddonMessage(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Params.Read(_worldPacket);
        }

        public ChatAddonMessageParams Params = new();
    }

    class ChatAddonMessageTargeted : ClientPacket
    {
        public ChatAddonMessageTargeted(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int targetLen = _worldPacket.ReadBits<int>(9);
            _worldPacket.ResetBitPos();

            Params.Read(_worldPacket);
            ChannelGUID = _worldPacket.ReadPackedGuid();
            Target = _worldPacket.ReadString(targetLen);
        }

        public string Target;
        public ChatAddonMessageParams Params = new();
        public ObjectGuid? ChannelGUID; // not optional in the packet. Optional for api reasons
    }

    public class ChatMessageDND : ClientPacket
    {
        public ChatMessageDND(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int len = _worldPacket.ReadBits<int>(11);
            Text = _worldPacket.ReadString(len);
        }

        public string Text;
    }

    public class ChatMessageAFK : ClientPacket
    {
        public ChatMessageAFK(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int len = _worldPacket.ReadBits<int>(11);
            Text = _worldPacket.ReadString(len);
        }

        public string Text;
    }

    public class ChatMessageEmote : ClientPacket
    {
        public ChatMessageEmote(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int len = _worldPacket.ReadBits<int>(11);
            Text = _worldPacket.ReadString(len);
        }

        public string Text;
    }

    public class ChatPkt : ServerPacket
    {
        public ChatPkt() : base(ServerOpcodes.Chat) { }

        public void Initialize(ChatMsg chatType, Language language, WorldObject sender, WorldObject receiver, string message, int achievementId = 0, string channelName = "", Locale locale = Locale.enUS, string addonPrefix = "")
        {
            // Clear everything because same packet can be used multiple times
            Clear();

            SenderGUID.Clear();
            SenderAccountGUID.Clear();
            SenderGuildGUID.Clear();
            TargetGUID.Clear();
            SenderName = string.Empty;
            TargetName = string.Empty;
            _ChatFlags = ChatFlags.None;

            SlashCmd = chatType;
            _Language = language;

            if (sender != null)
                SetSender(sender, locale);

            if (receiver != null)
                SetReceiver(receiver, locale);

            SenderVirtualAddress = Global.WorldMgr.GetVirtualRealmAddress();
            TargetVirtualAddress = Global.WorldMgr.GetVirtualRealmAddress();
            AchievementID = achievementId;
            Channel = channelName;
            Prefix = addonPrefix;
            ChatText = message;
        }

        void SetSender(WorldObject sender, Locale locale)
        {
            SenderGUID = sender.GetGUID();

            if (sender.ToCreature() is Creature creatureSender)
                SenderName = creatureSender.GetName(locale);

            if (sender.ToPlayer() is Player playerSender)
            {
                SenderAccountGUID = playerSender.GetSession().GetAccountGUID();
                _ChatFlags = playerSender.GetChatFlags();

                SenderGuildGUID = ObjectGuid.Create(HighGuid.Guild, playerSender.GetGuildId());
            }
        }

        public void SetReceiver(WorldObject receiver, Locale locale)
        {
            TargetGUID = receiver.GetGUID();

            if (receiver.ToCreature() is Creature creatureReceiver)
                TargetName = creatureReceiver.GetName(locale);
        }

        public override void Write()
        {
            _worldPacket.WriteUInt8((byte)SlashCmd);
            _worldPacket.WriteUInt32((uint)_Language);
            _worldPacket.WritePackedGuid(SenderGUID);
            _worldPacket.WritePackedGuid(SenderGuildGUID);
            _worldPacket.WritePackedGuid(SenderAccountGUID);
            _worldPacket.WritePackedGuid(TargetGUID);
            _worldPacket.WriteUInt32(TargetVirtualAddress);
            _worldPacket.WriteUInt32(SenderVirtualAddress);
            _worldPacket.WriteInt32(AchievementID);
            _worldPacket.WriteFloat(DisplayTime);
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteBits(SenderName.GetByteCount(), 11);
            _worldPacket.WriteBits(TargetName.GetByteCount(), 11);
            _worldPacket.WriteBits(Prefix.GetByteCount(), 5);
            _worldPacket.WriteBits(Channel.GetByteCount(), 7);
            _worldPacket.WriteBits(ChatText.GetByteCount(), 12);
            _worldPacket.WriteBits((ushort)_ChatFlags, 15);
            _worldPacket.WriteBit(HideChatLog);
            _worldPacket.WriteBit(FakeSenderName);
            _worldPacket.WriteBit(Unused_801.HasValue);
            _worldPacket.WriteBit(ChannelGUID.HasValue);
            _worldPacket.FlushBits();

            _worldPacket.WriteString(SenderName);
            _worldPacket.WriteString(TargetName);
            _worldPacket.WriteString(Prefix);
            _worldPacket.WriteString(Channel);
            _worldPacket.WriteString(ChatText);

            if (Unused_801.HasValue)
                _worldPacket.WriteUInt32(Unused_801.Value);

            if (ChannelGUID.HasValue)
                _worldPacket.WritePackedGuid(ChannelGUID.Value);
        }

        public ChatMsg SlashCmd;
        public Language _Language = Language.Universal;
        public ObjectGuid SenderGUID;
        public ObjectGuid SenderGuildGUID;
        public ObjectGuid SenderAccountGUID;
        public ObjectGuid TargetGUID;
        public uint SenderVirtualAddress;
        public uint TargetVirtualAddress;
        public string SenderName = string.Empty;
        public string TargetName = string.Empty;
        public string Prefix = string.Empty;
        public string Channel = string.Empty;
        public string ChatText = string.Empty;
        public int AchievementID;
        public ChatFlags _ChatFlags;
        public float DisplayTime;
        public int SpellID;
        public uint? Unused_801;
        public bool HideChatLog;
        public bool FakeSenderName;
        public ObjectGuid? ChannelGUID;
    }

    public class EmoteMessage : ServerPacket
    {
        public EmoteMessage() : base(ServerOpcodes.Emote, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteInt32((int)EmoteID);
            _worldPacket.WriteInt32(SpellVisualKitIDs.Count);
            _worldPacket.WriteInt32(SequenceVariation);

            foreach (var id in SpellVisualKitIDs)
                _worldPacket.WriteInt32(id);
        }

        public ObjectGuid Guid;
        public Emote EmoteID;
        public List<int> SpellVisualKitIDs = new();
        public int SequenceVariation;
    }

    public class CTextEmote : ClientPacket
    {
        public CTextEmote(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Target = _worldPacket.ReadPackedGuid();
            EmoteID = _worldPacket.ReadInt32();
            SoundIndex = _worldPacket.ReadInt32();

            SpellVisualKitIDs = new int[_worldPacket.ReadInt32()];
            SequenceVariation = _worldPacket.ReadInt32();
            for (var i = 0; i < SpellVisualKitIDs.Length; ++i)
                SpellVisualKitIDs[i] = _worldPacket.ReadInt32();
        }

        public ObjectGuid Target;
        public int EmoteID;
        public int SoundIndex;
        public int[] SpellVisualKitIDs;
        public int SequenceVariation;
    }

    public class STextEmote : ServerPacket
    {
        public STextEmote() : base(ServerOpcodes.TextEmote, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(SourceGUID);
            _worldPacket.WritePackedGuid(SourceAccountGUID);
            _worldPacket.WriteInt32(EmoteID);
            _worldPacket.WriteInt32(SoundIndex);
            _worldPacket.WritePackedGuid(TargetGUID);
        }

        public ObjectGuid SourceGUID;
        public ObjectGuid SourceAccountGUID;
        public ObjectGuid TargetGUID;
        public int SoundIndex = -1;
        public int EmoteID;
    }

    class ClearBossEmotes : ServerPacket
    {
        public ClearBossEmotes() : base(ServerOpcodes.ClearBossEmotes) { }

        public override void Write() { }
    }

    public class PrintNotification : ServerPacket
    {
        public PrintNotification(string notifyText) : base(ServerOpcodes.PrintNotification)
        {
            NotifyText = notifyText;
        }

        public override void Write()
        {
            _worldPacket.WriteBits(NotifyText.GetByteCount(), 12);
            _worldPacket.FlushBits();
            _worldPacket.WriteString(NotifyText);
        }

        public string NotifyText;
    }

    public class EmoteClient : ClientPacket
    {
        public EmoteClient(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class ChatPlayerNotfound : ServerPacket
    {
        public ChatPlayerNotfound(string name) : base(ServerOpcodes.ChatPlayerNotfound)
        {
            Name = name;
        }

        public override void Write()
        {
            _worldPacket.WriteBits(Name.GetByteCount(), 9);
            _worldPacket.FlushBits();
            _worldPacket.WriteString(Name);
        }

        string Name;
    }

    class ChatServerMessage : ServerPacket
    {
        public ChatServerMessage() : base(ServerOpcodes.ChatServerMessage) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(MessageID);

            _worldPacket.WriteBits(StringParam.GetByteCount(), 11);
            _worldPacket.FlushBits();
            _worldPacket.WriteString(StringParam);
        }

        public int MessageID;
        public string StringParam = string.Empty;
    }

    class ChatRegisterAddonPrefixes : ClientPacket
    {
        public const int MAX_PREFIXES = 64;
        public ChatRegisterAddonPrefixes(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int count = _worldPacket.ReadInt32();

            for (int i = 0; i < count; ++i)
                Prefixes[i] = _worldPacket.ReadString(_worldPacket.ReadBits<int>(5));
        }

        public Array<string> Prefixes = new(MAX_PREFIXES);
    }

    class ChatUnregisterAllAddonPrefixes : ClientPacket
    {
        public ChatUnregisterAllAddonPrefixes(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class DefenseMessage : ServerPacket
    {
        public DefenseMessage() : base(ServerOpcodes.DefenseMessage) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(ZoneID);
            _worldPacket.WriteBits(MessageText.GetByteCount(), 12);
            _worldPacket.FlushBits();
            _worldPacket.WriteString(MessageText);
        }

        public int ZoneID;
        public string MessageText = string.Empty;
    }

    class ChatReportIgnored : ClientPacket
    {
        public ChatReportIgnored(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            IgnoredGUID = _worldPacket.ReadPackedGuid();
            Reason = _worldPacket.ReadUInt8();
        }

        public ObjectGuid IgnoredGUID;
        public byte Reason;
    }

    class ChatPlayerAmbiguous : ServerPacket
    {
        public ChatPlayerAmbiguous(string name) : base(ServerOpcodes.ChatPlayerAmbiguous)
        {
            Name = name;
        }

        public override void Write()
        {
            _worldPacket.WriteBits(Name.GetByteCount(), 9);
            _worldPacket.WriteString(Name);
        }

        string Name;
    }

    class ChatRestricted : ServerPacket
    {
        public ChatRestricted(ChatRestrictionType reason) : base(ServerOpcodes.ChatRestricted)
        {
            Reason = reason;
        }

        public override void Write()
        {
            _worldPacket.WriteUInt8((byte)Reason);
        }

        ChatRestrictionType Reason;
    }

    class CanLocalWhisperTargetRequest : ClientPacket
    {
        public CanLocalWhisperTargetRequest(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            WhisperTarget = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid WhisperTarget;
    }

    class CanLocalWhisperTargetResponse : ServerPacket
    {
        public CanLocalWhisperTargetResponse() : base(ServerOpcodes.ChatCanLocalWhisperTargetResponse) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(WhisperTarget);
            _worldPacket.WriteInt32((int)Status);
        }

        public ObjectGuid WhisperTarget;
        public ChatWhisperTargetStatus Status;
    }

    class UpdateAADCStatus : ClientPacket
    {
        public UpdateAADCStatus(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ChatDisabled = _worldPacket.HasBit();
        }

        public bool ChatDisabled;
    }

    class UpdateAADCStatusResponse : ServerPacket
    {
        public UpdateAADCStatusResponse() : base(ServerOpcodes.UpdateAadcStatusResponse) { }

        public override void Write()
        {
            _worldPacket.WriteBit(Success);
            _worldPacket.WriteBit(ChatDisabled);
            _worldPacket.FlushBits();
        }

        public bool Success = false;
        public bool ChatDisabled = false;
    }

    //structs
    public class ChatAddonMessageParams
    {
        public void Read(WorldPacket data)
        {
            int prefixLen = data.ReadBits<int>(5);
            int textLen = data.ReadBits<int>(8);
            IsLogged = data.HasBit();
            Type = (ChatMsg)data.ReadInt32();
            Prefix = data.ReadString(prefixLen);
            Text = data.ReadString(textLen);
        }

        public string Prefix;
        public string Text;
        public ChatMsg Type = ChatMsg.Party;
        public bool IsLogged;
    }
}
