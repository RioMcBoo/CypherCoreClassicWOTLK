// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    public class QueryGuildInfo : ClientPacket
    {
        public QueryGuildInfo(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GuildGuid = _worldPacket.ReadPackedGuid();
            PlayerGuid = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid GuildGuid;
        public ObjectGuid PlayerGuid;
    }

    public class QueryGuildInfoResponse : ServerPacket
    {
        public QueryGuildInfoResponse() : base(ServerOpcodes.QueryGuildInfoResponse) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(GuildGUID);
            _worldPacket.WriteBit(Info != null);
            _worldPacket.FlushBits();

            if (Info != null)
            {
                _worldPacket.WritePackedGuid(Info.GuildGuid);
                _worldPacket.WriteUInt32(Info.VirtualRealmAddress);
                _worldPacket.WriteInt32(Info.Ranks.Count);
                _worldPacket.WriteInt32(Info.EmblemStyle);
                _worldPacket.WriteInt32(Info.EmblemColor);
                _worldPacket.WriteInt32(Info.BorderStyle);
                _worldPacket.WriteInt32(Info.BorderColor);
                _worldPacket.WriteInt32(Info.BackgroundColor);
                _worldPacket.WriteBits(Info.GuildName.GetByteCount(), 7);
                _worldPacket.FlushBits();

                foreach (var rank in Info.Ranks)
                {
                    _worldPacket.WriteInt32(rank.RankID);
                    _worldPacket.WriteInt32(rank.RankOrder);
                    _worldPacket.WriteBits(rank.RankName.GetByteCount(), 7);
                    _worldPacket.FlushBits();

                    _worldPacket.WriteString(rank.RankName);
                }

                _worldPacket.WriteString(Info.GuildName);
            }

        }

        public ObjectGuid GuildGUID;
        public GuildInfo Info;

        public class GuildInfo
        {
            public ObjectGuid GuildGuid;

            public uint VirtualRealmAddress; // a special identifier made from the Index, BattleGroup and Region.

            public int EmblemStyle;
            public int EmblemColor;
            public int BorderStyle;
            public int BorderColor;
            public int BackgroundColor;
            public List<RankInfo> Ranks = new();
            public string GuildName = string.Empty;

            public struct RankInfo
            {
                public RankInfo(int id, int order, string name)
                {
                    RankID = id;
                    RankOrder = order;
                    RankName = name;
                }

                public int RankID;
                public int RankOrder;
                public string RankName;
            }
        }
    }

    public class GuildGetRoster : ClientPacket
    {
        public GuildGetRoster(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class GuildRoster : ServerPacket
    {
        public GuildRoster() : base(ServerOpcodes.GuildRoster)
        {
            MemberData = new List<GuildRosterMemberData>();
        }

        public override void Write()
        {
            _worldPacket.WriteInt32(NumAccounts);
            CreateDate.Write(_worldPacket);
            _worldPacket.WriteInt32(GuildFlags);
            _worldPacket.WriteInt32(MemberData.Count);
            _worldPacket.WriteBits(WelcomeText.GetByteCount(), 11);
            _worldPacket.WriteBits(InfoText.GetByteCount(), 11);
            _worldPacket.FlushBits();

            MemberData.ForEach(p => p.Write(_worldPacket));

            _worldPacket.WriteString(WelcomeText);
            _worldPacket.WriteString(InfoText);
        }

        public List<GuildRosterMemberData> MemberData;
        public string WelcomeText;
        public string InfoText;
        public WowTime CreateDate;
        public int NumAccounts;
        public int GuildFlags;
    }

    public class GuildRosterUpdate : ServerPacket
    {
        public GuildRosterUpdate() : base(ServerOpcodes.GuildRosterUpdate) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(MemberData.Count);

            MemberData.ForEach(p => p.Write(_worldPacket));
        }

        public List<GuildRosterMemberData> MemberData = new ();
    }

    public class GuildUpdateMotdText : ClientPacket
    {
        public GuildUpdateMotdText(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int textLen = _worldPacket.ReadBits<int>(11);
            MotdText = _worldPacket.ReadString(textLen);
        }

        public string MotdText;
    }

    public class GuildCommandResult : ServerPacket
    {
        public GuildCommandResult() : base(ServerOpcodes.GuildCommandResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32((int)Result);
            _worldPacket.WriteInt32((int)Command);

            _worldPacket.WriteBits(Name.GetByteCount(), 8);
            _worldPacket.WriteString(Name);
        }

        public string Name;
        public GuildCommandError Result;
        public GuildCommandType Command;
    }

    public class AcceptGuildInvite : ClientPacket
    {
        public AcceptGuildInvite(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class GuildDeclineInvitation : ClientPacket
    {
        public GuildDeclineInvitation(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class DeclineGuildInvites : ClientPacket
    {
        public DeclineGuildInvites(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Allow = _worldPacket.HasBit();
        }

        public bool Allow;
    }

    public class GuildInviteByName : ClientPacket
    {
        public GuildInviteByName(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int nameLen = _worldPacket.ReadBits<int>(9);
            bool hasUnused910 = _worldPacket.HasBit();

            Name = _worldPacket.ReadString(nameLen);

            if (hasUnused910)
                Unused910 = _worldPacket.ReadInt32();
        }

        public string Name;
        public int? Unused910;
    }

    public class GuildInvite : ServerPacket
    {
        public GuildInvite() : base(ServerOpcodes.GuildInvite) { }

        public override void Write()
        {
            _worldPacket.WriteBits(InviterName.GetByteCount(), 6);
            _worldPacket.WriteBits(GuildName.GetByteCount(), 7);
            _worldPacket.WriteBits(OldGuildName.GetByteCount(), 7);

            _worldPacket.WriteUInt32(InviterVirtualRealmAddress);
            _worldPacket.WriteUInt32(GuildVirtualRealmAddress);
            _worldPacket.WritePackedGuid(GuildGUID);
            _worldPacket.WriteUInt32(OldGuildVirtualRealmAddress);
            _worldPacket.WritePackedGuid(OldGuildGUID);
            _worldPacket.WriteInt32(EmblemStyle);
            _worldPacket.WriteInt32(EmblemColor);
            _worldPacket.WriteInt32(BorderStyle);
            _worldPacket.WriteInt32(BorderColor);
            _worldPacket.WriteInt32(Background);
            _worldPacket.WriteInt32(AchievementPoints);

            _worldPacket.WriteString(InviterName);
            _worldPacket.WriteString(GuildName);
            _worldPacket.WriteString(OldGuildName);
        }

        public ObjectGuid GuildGUID;
        public ObjectGuid OldGuildGUID;
        public int AchievementPoints;
        public int EmblemStyle;
        public int EmblemColor;        
        public int BorderStyle;
        public int BorderColor;
        public int Background;
        public uint GuildVirtualRealmAddress;
        public uint OldGuildVirtualRealmAddress;
        public uint InviterVirtualRealmAddress;
        public string InviterName;
        public string GuildName;
        public string OldGuildName;
    }

    public class GuildEventStatusChange : ServerPacket
    {
        public GuildEventStatusChange() : base(ServerOpcodes.GuildEventStatusChange) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteBit(AFK);
            _worldPacket.WriteBit(DND);
            _worldPacket.FlushBits();
        }

        public ObjectGuid Guid;
        public bool AFK;
        public bool DND;
    }

    public class GuildEventPresenceChange : ServerPacket
    {
        public GuildEventPresenceChange() : base(ServerOpcodes.GuildEventPresenceChange) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteUInt32(VirtualRealmAddress);

            _worldPacket.WriteBits(Name.GetByteCount(), 6);
            _worldPacket.WriteBit(LoggedOn);
            _worldPacket.WriteBit(Mobile);

            _worldPacket.WriteString(Name);
        }

        public ObjectGuid Guid;
        public uint VirtualRealmAddress;
        public string Name;
        public bool Mobile;
        public bool LoggedOn;
    }

    public class GuildEventMotd : ServerPacket
    {
        public GuildEventMotd() : base(ServerOpcodes.GuildEventMotd) { }

        public override void Write()
        {
            _worldPacket.WriteBits(MotdText.GetByteCount(), 11);
            _worldPacket.WriteString(MotdText);            
        }

        public string MotdText;
    }

    public class GuildEventPlayerJoined : ServerPacket
    {
        public GuildEventPlayerJoined() : base(ServerOpcodes.GuildEventPlayerJoined) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteUInt32(VirtualRealmAddress);

            _worldPacket.WriteBits(Name.GetByteCount(), 6);
            _worldPacket.WriteString(Name);
        }

        public ObjectGuid Guid;
        public string Name;
        public uint VirtualRealmAddress;
    }

    public class GuildEventRankChanged : ServerPacket
    {
        public GuildEventRankChanged() : base(ServerOpcodes.GuildEventRankChanged) { }

        public override void Write()
        {
            _worldPacket.WriteUInt32(RankID);
        }

        public uint RankID;
    }

    public class GuildEventRanksUpdated : ServerPacket
    {
        public GuildEventRanksUpdated() : base(ServerOpcodes.GuildEventRanksUpdated) { }

        public override void Write() { }
    }

    public class GuildEventBankMoneyChanged : ServerPacket
    {
        public GuildEventBankMoneyChanged() : base(ServerOpcodes.GuildEventBankMoneyChanged) { }

        public override void Write()
        {
            _worldPacket.WriteInt64(Money);
        }

        public long Money;
    }

    public class GuildEventDisbanded : ServerPacket
    {
        public GuildEventDisbanded() : base(ServerOpcodes.GuildEventDisbanded) { }

        public override void Write() { }
    }

    public class GuildEventLogQuery : ClientPacket
    {
        public GuildEventLogQuery(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class GuildEventLogQueryResults : ServerPacket
    {
        public GuildEventLogQueryResults() : base(ServerOpcodes.GuildEventLogQueryResults)
        {
            Entry = new List<GuildEventEntry>();
        }

        public override void Write()
        {
            _worldPacket.WriteInt32(Entry.Count);

            foreach (GuildEventEntry entry in Entry)
            {
                _worldPacket.WritePackedGuid(entry.PlayerGUID);
                _worldPacket.WritePackedGuid(entry.OtherGUID);
                _worldPacket.WriteUInt8(entry.TransactionType);
                _worldPacket.WriteUInt8(entry.RankID);
                _worldPacket.WriteUInt32(entry.TransactionDate);
            }
        }

        public List<GuildEventEntry> Entry;
    }

    public class GuildEventPlayerLeft : ServerPacket
    {
        public GuildEventPlayerLeft() : base(ServerOpcodes.GuildEventPlayerLeft) { }

        public override void Write()
        {
            _worldPacket.WriteBit(Removed);
            _worldPacket.WriteBits(LeaverName.GetByteCount(), 6);

            if (Removed)
            {
                _worldPacket.WriteBits(RemoverName.GetByteCount(), 6);
                _worldPacket.WritePackedGuid(RemoverGUID);
                _worldPacket.WriteUInt32(RemoverVirtualRealmAddress);
                _worldPacket.WriteString(RemoverName);
            }

            _worldPacket.WritePackedGuid(LeaverGUID);
            _worldPacket.WriteUInt32(LeaverVirtualRealmAddress);
            _worldPacket.WriteString(LeaverName);
        }

        public ObjectGuid LeaverGUID;
        public string LeaverName;
        public uint LeaverVirtualRealmAddress;
        public ObjectGuid RemoverGUID;
        public string RemoverName;
        public uint RemoverVirtualRealmAddress;
        public bool Removed;
    }

    public class GuildEventNewLeader : ServerPacket
    {
        public GuildEventNewLeader() : base(ServerOpcodes.GuildEventNewLeader) { }

        public override void Write()
        {
            _worldPacket.WriteBit(SelfPromoted);
            _worldPacket.WriteBits(OldLeaderName.GetByteCount(), 6);
            _worldPacket.WriteBits(NewLeaderName.GetByteCount(), 6);

            _worldPacket.WritePackedGuid(OldLeaderGUID);
            _worldPacket.WriteUInt32(OldLeaderVirtualRealmAddress);
            _worldPacket.WritePackedGuid(NewLeaderGUID);
            _worldPacket.WriteUInt32(NewLeaderVirtualRealmAddress);

            _worldPacket.WriteString(OldLeaderName);
            _worldPacket.WriteString(NewLeaderName);
        }

        public ObjectGuid NewLeaderGUID;
        public string NewLeaderName;
        public uint NewLeaderVirtualRealmAddress;
        public ObjectGuid OldLeaderGUID;
        public string OldLeaderName;
        public uint OldLeaderVirtualRealmAddress;
        public bool SelfPromoted;
    }

    public class GuildEventTabAdded : ServerPacket
    {
        public GuildEventTabAdded() : base(ServerOpcodes.GuildEventTabAdded) { }

        public override void Write() { }
    }

    public class GuildEventTabModified : ServerPacket
    {
        public GuildEventTabModified() : base(ServerOpcodes.GuildEventTabModified) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Tab);

            _worldPacket.WriteBits(Name.GetByteCount(), 7);
            _worldPacket.WriteBits(Icon.GetByteCount(), 9);
            _worldPacket.FlushBits();

            _worldPacket.WriteString(Name);
            _worldPacket.WriteString(Icon);
        }

        public string Icon;
        public string Name;
        public int Tab;
    }

    public class GuildEventTabTextChanged : ServerPacket
    {
        public GuildEventTabTextChanged() : base(ServerOpcodes.GuildEventTabTextChanged) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Tab);
        }

        public int Tab;
    }

    public class GuildEventBankContentsChanged : ServerPacket
    {
        public GuildEventBankContentsChanged() : base(ServerOpcodes.GuildEventBankContentsChanged) { }

        public override void Write() { }
    }

    public class GuildPermissionsQuery : ClientPacket
    {
        public GuildPermissionsQuery(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class GuildPermissionsQueryResults : ServerPacket
    {
        public GuildPermissionsQueryResults() : base(ServerOpcodes.GuildPermissionsQueryResults)
        {
            Tab = new List<GuildRankTabPermissions>();
        }

        public override void Write()
        {
            _worldPacket.WriteUInt32(RankID);
            _worldPacket.WriteInt32(Flags);
            _worldPacket.WriteInt32(WithdrawGoldLimit);
            _worldPacket.WriteInt32(NumTabs);
            _worldPacket.WriteInt32(Tab.Count);

            foreach (GuildRankTabPermissions tab in Tab)
            {
                _worldPacket.WriteInt32(tab.Flags);
                _worldPacket.WriteInt32(tab.WithdrawItemLimit);
            }
        }

        public int NumTabs;
        public int WithdrawGoldLimit;
        public int Flags;
        public uint RankID;
        public List<GuildRankTabPermissions> Tab;

        public struct GuildRankTabPermissions
        {
            public int Flags;
            public int WithdrawItemLimit;
        }
    }

    public class GuildSetRankPermissions : ClientPacket
    {
        public GuildSetRankPermissions(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            RankID = _worldPacket.ReadInt32();
            RankOrder = _worldPacket.ReadInt32();
            Flags = _worldPacket.ReadUInt32();
            WithdrawGoldLimit = _worldPacket.ReadInt32();

            for (byte i = 0; i < GuildConst.MaxBankTabs; i++)
            {
                TabFlags[i] = _worldPacket.ReadUInt32();
                TabWithdrawItemLimit[i] = _worldPacket.ReadInt32();
            }

            OldFlags = _worldPacket.ReadUInt32();

            _worldPacket.ResetBitPos();
            int rankNameLen = _worldPacket.ReadBits<int>(7);

            RankName = _worldPacket.ReadString(rankNameLen);
        }

        public int RankID;
        public int RankOrder;
        public int WithdrawGoldLimit;
        public uint Flags;
        public uint OldFlags;
        public uint[] TabFlags = new uint[GuildConst.MaxBankTabs];
        public int[] TabWithdrawItemLimit = new int[GuildConst.MaxBankTabs];
        public string RankName;
    }

    public class GuildAddRank : ClientPacket
    {
        public GuildAddRank(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int nameLen = _worldPacket.ReadBits<int>(7);
            _worldPacket.ResetBitPos();

            RankOrder = _worldPacket.ReadInt32();
            Name = _worldPacket.ReadString(nameLen);
        }

        public string Name;
        public int RankOrder;
    }

    public class GuildAssignMemberRank : ClientPacket
    {
        public GuildAssignMemberRank(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Member = _worldPacket.ReadPackedGuid();
            RankOrder = _worldPacket.ReadInt32();
        }

        public ObjectGuid Member;
        public int RankOrder;
    }

    public class GuildDeleteRank : ClientPacket
    {
        public GuildDeleteRank(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            RankOrder = _worldPacket.ReadInt32();
        }

        public int RankOrder;
    }

    public class GuildGetRanks : ClientPacket
    {
        public GuildGetRanks(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GuildGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid GuildGUID;
    }

    public class GuildRanks : ServerPacket
    {
        public GuildRanks() : base(ServerOpcodes.GuildRanks)
        {
            Ranks = new List<GuildRankData>();
        }

        public override void Write()
        {
            _worldPacket.WriteInt32(Ranks.Count);

            Ranks.ForEach(p => p.Write(_worldPacket));
        }

        public List<GuildRankData> Ranks;
    }

    public class GuildSendRankChange : ServerPacket
    {
        public GuildSendRankChange() : base(ServerOpcodes.GuildSendRankChange) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Officer);
            _worldPacket.WritePackedGuid(Other);
            _worldPacket.WriteUInt32(RankID);

            _worldPacket.WriteBit(Promote);
            _worldPacket.FlushBits();
        }

        public ObjectGuid Other;
        public ObjectGuid Officer;
        public bool Promote;
        public uint RankID;
    }

    public class GuildShiftRank : ClientPacket
    {
        public GuildShiftRank(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            RankOrder = _worldPacket.ReadInt32();
            ShiftUp = _worldPacket.HasBit();
        }

        public bool ShiftUp;
        public int RankOrder;
    }

    public class GuildUpdateInfoText : ClientPacket
    {
        public GuildUpdateInfoText(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int textLen = _worldPacket.ReadBits<int>(11);
            InfoText = _worldPacket.ReadString(textLen);
        }

        public string InfoText;
    }

    public class GuildSetMemberNote : ClientPacket
    {
        public GuildSetMemberNote(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            NoteeGUID = _worldPacket.ReadPackedGuid();

            int noteLen = _worldPacket.ReadBits<int>(8);
            IsPublic = _worldPacket.HasBit();

            Note = _worldPacket.ReadString(noteLen);
        }

        public ObjectGuid NoteeGUID;
        public bool IsPublic;          // 0 == Officer, 1 == Public
        public string Note;
    }

    public class GuildMemberUpdateNote : ServerPacket
    {
        public GuildMemberUpdateNote() : base(ServerOpcodes.GuildMemberUpdateNote) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Member);

            _worldPacket.WriteBits(Note.GetByteCount(), 8);
            _worldPacket.WriteBit(IsPublic);
            _worldPacket.FlushBits();

            _worldPacket.WriteString(Note);
        }

        public ObjectGuid Member;
        public bool IsPublic;          // 0 == Officer, 1 == Public
        public string Note;
    }

    public class GuildMemberDailyReset : ServerPacket
    {
        public GuildMemberDailyReset() : base(ServerOpcodes.GuildMemberDailyReset) { }

        public override void Write() { }
    }

    public class GuildDelete : ClientPacket
    {
        public GuildDelete(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class GuildDemoteMember : ClientPacket
    {
        public GuildDemoteMember(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Demotee = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Demotee;
    }

    public class GuildPromoteMember : ClientPacket
    {
        public GuildPromoteMember(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Promotee = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Promotee;
    }

    public class GuildOfficerRemoveMember : ClientPacket
    {
        public GuildOfficerRemoveMember(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Removee = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Removee;
    }

    public class GuildLeave : ClientPacket
    {
        public GuildLeave(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class GuildChangeNameRequest : ClientPacket
    {
        public GuildChangeNameRequest(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int nameLen = _worldPacket.ReadBits<int>(7);
            NewName = _worldPacket.ReadString(nameLen);
        }

        public string NewName;
    }

    public class GuildFlaggedForRename : ServerPacket
    {
        public GuildFlaggedForRename() : base(ServerOpcodes.GuildFlaggedForRename) { }

        public override void Write()
        {
            _worldPacket.WriteBit(FlagSet);
        }

        public bool FlagSet;
    }

    public class RequestGuildPartyState : ClientPacket
    {
        public RequestGuildPartyState(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GuildGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid GuildGUID;
    }

    public class GuildPartyState : ServerPacket
    {
        public GuildPartyState() : base(ServerOpcodes.GuildPartyState, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteBit(InGuildParty);
            _worldPacket.FlushBits();

            _worldPacket.WriteInt32(NumMembers);
            _worldPacket.WriteInt32(NumRequired);
            _worldPacket.WriteFloat(GuildXPEarnedMult);
        }

        public float GuildXPEarnedMult = 0.0f;
        public int NumMembers;
        public int NumRequired;
        public bool InGuildParty;
    }

    public class RequestGuildRewardsList : ClientPacket
    {
        public RequestGuildRewardsList(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            CurrentVersion = _worldPacket.ReadInt64();
        }

        public long CurrentVersion;
    }

    public class GuildRewardList : ServerPacket
    {
        public GuildRewardList() : base(ServerOpcodes.GuildRewardList)
        {
            RewardItems = new List<GuildRewardItem>();
        }

        public override void Write()
        {
            _worldPacket.WriteInt64(Version);
            _worldPacket.WriteInt32(RewardItems.Count);

            foreach (var item in RewardItems)
                item.Write(_worldPacket);
        }

        public List<GuildRewardItem> RewardItems;
        public long Version;
    }

    public class GuildBankActivate : ClientPacket
    {
        public GuildBankActivate(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Banker = _worldPacket.ReadPackedGuid();
            FullUpdate = _worldPacket.HasBit();
        }

        public ObjectGuid Banker;
        public bool FullUpdate;
    }

    public class GuildBankBuyTab : ClientPacket
    {
        public GuildBankBuyTab(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Banker = _worldPacket.ReadPackedGuid();
            BankTab = _worldPacket.ReadUInt8();
        }

        public ObjectGuid Banker;
        public byte BankTab;
    }

    public class GuildBankUpdateTab : ClientPacket
    {
        public GuildBankUpdateTab(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Banker = _worldPacket.ReadPackedGuid();
            BankTab = _worldPacket.ReadUInt8();

            _worldPacket.ResetBitPos();
            int nameLen = _worldPacket.ReadBits<int>(7);
            int iconLen = _worldPacket.ReadBits<int>(9);

            Name = _worldPacket.ReadString(nameLen);
            Icon = _worldPacket.ReadString(iconLen);
        }

        public ObjectGuid Banker;
        public byte BankTab;
        public string Name;
        public string Icon;
    }

    public class GuildBankDepositMoney : ClientPacket
    {
        public GuildBankDepositMoney(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Banker = _worldPacket.ReadPackedGuid();
            Money = _worldPacket.ReadInt64();
        }

        public ObjectGuid Banker;
        public long Money;
    }

    public class GuildBankQueryTab : ClientPacket
    {
        public GuildBankQueryTab(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Banker = _worldPacket.ReadPackedGuid();
            Tab = _worldPacket.ReadUInt8();

            FullUpdate = _worldPacket.HasBit();
        }

        public ObjectGuid Banker;
        public byte Tab;
        public bool FullUpdate;
    }

    public class GuildBankRemainingWithdrawMoneyQuery : ClientPacket
    {
        public GuildBankRemainingWithdrawMoneyQuery(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class GuildBankRemainingWithdrawMoney : ServerPacket
    {
        public GuildBankRemainingWithdrawMoney() : base(ServerOpcodes.GuildBankRemainingWithdrawMoney) { }

        public override void Write()
        {
            _worldPacket.WriteInt64(RemainingWithdrawMoney);
        }

        public long RemainingWithdrawMoney;
    }

    public class GuildBankWithdrawMoney : ClientPacket
    {
        public GuildBankWithdrawMoney(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Banker = _worldPacket.ReadPackedGuid();
            Money = _worldPacket.ReadInt64();
        }

        public ObjectGuid Banker;
        public long Money;
    }

    public class GuildBankQueryResults : ServerPacket
    {
        public GuildBankQueryResults() : base(ServerOpcodes.GuildBankQueryResults)
        {
            ItemInfo = new List<GuildBankItemInfo>();
            TabInfo = new List<GuildBankTabInfo>();
        }

        public override void Write()
        {
            _worldPacket.WriteInt64(Money);
            _worldPacket.WriteInt32(Tab);
            _worldPacket.WriteInt32(WithdrawalsRemaining);
            _worldPacket.WriteInt32(TabInfo.Count);
            _worldPacket.WriteInt32(ItemInfo.Count);
            _worldPacket.WriteBit(FullUpdate);
            _worldPacket.FlushBits();

            foreach (GuildBankTabInfo tab in TabInfo)
                tab.Write(_worldPacket);

            foreach (GuildBankItemInfo item in ItemInfo)
                item.Write(_worldPacket);
        }

        public List<GuildBankItemInfo> ItemInfo;
        public List<GuildBankTabInfo> TabInfo;
        public int WithdrawalsRemaining;
        public int Tab;
        public long Money;
        public bool FullUpdate;
    }

    class AutoGuildBankItem : ClientPacket
    {
        public AutoGuildBankItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GI_Items.Read(_worldPacket);
        }

        public GBank_X_Inventory_ItemInfo GI_Items;
    }

    class StoreGuildBankItem : ClientPacket
    {
        public StoreGuildBankItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GI_Items.Read(_worldPacket);
        }

        public GBank_X_Inventory_ItemInfo GI_Items;
    }

    class SwapItemWithGuildBankItem : ClientPacket
    {
        public SwapItemWithGuildBankItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GI_Items.Read(_worldPacket);
        }

        public GBank_X_Inventory_ItemInfo GI_Items;
    }

    class SwapGuildBankItemWithGuildBankItem : ClientPacket
    {
        public SwapGuildBankItemWithGuildBankItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GG_Items.Read(_worldPacket);
        }

        public GBank_X_GBank_ItemInfo GG_Items;
    }

    class MoveGuildBankItem : ClientPacket
    {
        public MoveGuildBankItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GG_Items.Read(_worldPacket);
        }

        public GBank_X_GBank_ItemInfo GG_Items;
    }

    class MergeItemWithGuildBankItem : ClientPacket
    {
        public MergeItemWithGuildBankItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GI_StackItems.Read(_worldPacket);
        }

        public GBank_X_Inventory_ItemStackInfo GI_StackItems;
    }

    class SplitItemToGuildBank : ClientPacket
    {
        public SplitItemToGuildBank(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GI_StackItems.Read(_worldPacket);
        }

        public GBank_X_Inventory_ItemStackInfo GI_StackItems;
    }

    class MergeGuildBankItemWithItem : ClientPacket
    {
        public MergeGuildBankItemWithItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GI_StackItems.Read(_worldPacket);
        }

        public GBank_X_Inventory_ItemStackInfo GI_StackItems;
    }

    class SplitGuildBankItemToInventory : ClientPacket
    {
        public SplitGuildBankItemToInventory(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GI_StackItems.Read(_worldPacket);
        }

        public GBank_X_Inventory_ItemStackInfo GI_StackItems;
    }

    class AutoStoreGuildBankItem : ClientPacket
    {
        public AutoStoreGuildBankItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Banker = _worldPacket.ReadPackedGuid();
            BankTab = _worldPacket.ReadUInt8();
            BankSlot = _worldPacket.ReadUInt8();
        }

        public ObjectGuid Banker;
        public byte BankTab;
        public byte BankSlot;
    }

    class MergeGuildBankItemWithGuildBankItem : ClientPacket
    {
        public MergeGuildBankItemWithGuildBankItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GG_StackItems.Read(_worldPacket);
        }

        public GBank_X_GBank_ItemStackInfo GG_StackItems;
    }

    class SplitGuildBankItem : ClientPacket
    {
        public SplitGuildBankItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GG_StackItems.Read(_worldPacket);
        }

        public GBank_X_GBank_ItemStackInfo GG_StackItems;
    }

    public class GuildBankLogQuery : ClientPacket
    {
        public GuildBankLogQuery(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Tab = _worldPacket.ReadInt32();
        }

        public int Tab;
    }

    public class GuildBankLogQueryResults : ServerPacket
    {
        public GuildBankLogQueryResults() : base(ServerOpcodes.GuildBankLogQueryResults)
        {
            Entry = new List<GuildBankLogEntry>();
        }

        public override void Write()
        {
            _worldPacket.WriteInt32(Tab);
            _worldPacket.WriteInt32(Entry.Count);
            _worldPacket.WriteBit(WeeklyBonusMoney.HasValue);
            _worldPacket.FlushBits();

            foreach (GuildBankLogEntry logEntry in Entry)
            {
                _worldPacket.WritePackedGuid(logEntry.PlayerGUID);
                _worldPacket.WriteUInt32(logEntry.TimeOffset);
                _worldPacket.WriteInt8(logEntry.EntryType);

                _worldPacket.WriteBit(logEntry.Money.HasValue);
                _worldPacket.WriteBit(logEntry.ItemID.HasValue);
                _worldPacket.WriteBit(logEntry.Count.HasValue);
                _worldPacket.WriteBit(logEntry.OtherTab.HasValue);
                _worldPacket.FlushBits();

                if (logEntry.Money.HasValue)
                    _worldPacket.WriteInt64(logEntry.Money.Value);

                if (logEntry.ItemID.HasValue)
                    _worldPacket.WriteInt32(logEntry.ItemID.Value);

                if (logEntry.Count.HasValue)
                    _worldPacket.WriteInt32(logEntry.Count.Value);

                if (logEntry.OtherTab.HasValue)
                    _worldPacket.WriteInt8(logEntry.OtherTab.Value);
            }

            if (WeeklyBonusMoney.HasValue)
                _worldPacket.WriteUInt64(WeeklyBonusMoney.Value);
        }

        public int Tab;
        public List<GuildBankLogEntry> Entry;
        public ulong? WeeklyBonusMoney;
    }

    public class GuildBankTextQuery : ClientPacket
    {
        public GuildBankTextQuery(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Tab = _worldPacket.ReadInt32();
        }

        public int Tab;
    }

    public class GuildBankTextQueryResult : ServerPacket
    {
        public GuildBankTextQueryResult() : base(ServerOpcodes.GuildBankTextQueryResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Tab);

            _worldPacket.WriteBits(Text.GetByteCount(), 14);
            _worldPacket.WriteString(Text);
        }

        public int Tab;
        public string Text;
    }

    public class GuildBankSetTabText : ClientPacket
    {
        public GuildBankSetTabText(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Tab = _worldPacket.ReadInt32();
            TabText = _worldPacket.ReadString(_worldPacket.ReadBits<int>(14));
        }

        public int Tab;
        public string TabText;
    }

    public class GuildQueryNews : ClientPacket
    {
        public GuildQueryNews(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GuildGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid GuildGUID;
    }

    public class GuildNewsPkt : ServerPacket
    {
        public GuildNewsPkt() : base(ServerOpcodes.GuildNews) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(NewsEvents.Count);
            foreach (var newsEvent in NewsEvents)
                newsEvent.Write(_worldPacket);
        }

        public List<GuildNewsEvent> NewsEvents = new();
    }

    public class GuildNewsUpdateSticky : ClientPacket
    {
        public GuildNewsUpdateSticky(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GuildGUID = _worldPacket.ReadPackedGuid();
            NewsID = _worldPacket.ReadInt32();

            Sticky = _worldPacket.HasBit();
        }

        public int NewsID;
        public ObjectGuid GuildGUID;
        public bool Sticky;
    }

    class GuildReplaceGuildMaster : ClientPacket
    {
        public GuildReplaceGuildMaster(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class GuildSetGuildMaster : ClientPacket
    {
        public GuildSetGuildMaster(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int nameLen = _worldPacket.ReadBits<int>(9);
            NewMasterName = _worldPacket.ReadString(nameLen);
        }

        public string NewMasterName;
    }

    public class GuildChallengeUpdateRequest : ClientPacket
    {
        public GuildChallengeUpdateRequest(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class GuildChallengeUpdate : ServerPacket
    {
        public GuildChallengeUpdate() : base(ServerOpcodes.GuildChallengeUpdate) { }

        public override void Write()
        {
            for (int i = 0; i < GuildConst.ChallengesTypes; ++i)
                _worldPacket.WriteInt32(CurrentCount[i]);

            for (int i = 0; i < GuildConst.ChallengesTypes; ++i)
                _worldPacket.WriteInt32(MaxCount[i]);

            for (int i = 0; i < GuildConst.ChallengesTypes; ++i)
                _worldPacket.WriteInt32(MaxLevelGold[i]);

            for (int i = 0; i < GuildConst.ChallengesTypes; ++i)
                _worldPacket.WriteInt32(Gold[i]);
        }

        public int[] CurrentCount = new int[GuildConst.ChallengesTypes];
        public int[] MaxCount = new int[GuildConst.ChallengesTypes];
        public int[] Gold = new int[GuildConst.ChallengesTypes];
        public int[] MaxLevelGold = new int[GuildConst.ChallengesTypes];
    }

    public class SaveGuildEmblem : ClientPacket
    {
        public SaveGuildEmblem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Vendor = _worldPacket.ReadPackedGuid();
            EStyle = _worldPacket.ReadInt32();
            EColor = _worldPacket.ReadInt32();
            BStyle = _worldPacket.ReadInt32();
            BColor = _worldPacket.ReadInt32();
            Bg = _worldPacket.ReadInt32();
        }

        public ObjectGuid Vendor;
        public int BStyle;
        public int EStyle;
        public int BColor;
        public int EColor;
        public int Bg;
    }

    public class PlayerSaveGuildEmblem : ServerPacket
    {
        public PlayerSaveGuildEmblem() : base(ServerOpcodes.PlayerSaveGuildEmblem) { }

        public override void Write()
        {
            _worldPacket.WriteUInt32((uint)Error);
        }

        public GuildEmblemError Error;
    }

    class GuildSetAchievementTracking : ClientPacket
    {
        public GuildSetAchievementTracking(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int count = _worldPacket.ReadInt32();

            for (int i = 0; i < count; ++i)
                AchievementIDs[i] = _worldPacket.ReadInt32();
        }

        public Array<int> AchievementIDs = new(10);
    }

    class GuildNameChanged : ServerPacket
    {
        public GuildNameChanged() : base(ServerOpcodes.GuildNameChanged) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(GuildGUID);
            _worldPacket.WriteBits(GuildName.GetByteCount(), 7);
            _worldPacket.WriteString(GuildName);
        }

        public ObjectGuid GuildGUID;
        public string GuildName;
    }

    //Structs
    public struct GuildRosterProfessionData
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(DbID);
            data.WriteInt32(Rank);
            data.WriteInt32(Step);
        }

        public int DbID;
        public int Rank;
        public int Step;
    }

    public class GuildRosterMemberData
    {
        public void Write(WorldPacket data)
        {
            data.WritePackedGuid(Guid);
            data.WriteInt32(RankID);
            data.WriteInt32(AreaID);
            data.WriteInt32(PersonalAchievementPoints);
            data.WriteInt32(GuildReputation);
            data.WriteFloat(LastSave);

            for (byte i = 0; i < 2; i++)
                Profession[i].Write(data);

            data.WriteUInt32(VirtualRealmAddress);
            data.WriteUInt8(Status);
            data.WriteUInt8(Level);
            data.WriteUInt8(ClassID);
            data.WriteUInt8(Gender);
            data.WriteUInt64(GuildClubMemberID);
            data.WriteUInt8(RaceID);

            data.WriteBits(Name.GetByteCount(), 6);
            data.WriteBits(Note.GetByteCount(), 8);
            data.WriteBits(OfficerNote.GetByteCount(), 8);
            data.WriteBit(Authenticated);
            data.WriteBit(SorEligible);
            data.FlushBits();

            data.WriteString(Name);
            data.WriteString(Note);
            data.WriteString(OfficerNote);
        }

        public ObjectGuid Guid;
        public long WeeklyXP;
        public long TotalXP;
        public int RankID;
        public int AreaID;
        private int PersonalAchievementPoints = -1;
        private int GuildReputation = -1;
        public int GuildRepToCap;
        public float LastSave;
        public string Name;
        public uint VirtualRealmAddress;
        public string Note;
        public string OfficerNote;
        public byte Status;
        public byte Level;
        public byte ClassID;
        public byte Gender;
        public ulong GuildClubMemberID;
        public byte RaceID;
        public bool Authenticated;
        public bool SorEligible;
        public GuildRosterProfessionData[] Profession = new GuildRosterProfessionData[2];
        public DungeonScoreSummary DungeonScore = new();
    }

    public class GuildEventEntry
    {
        public ObjectGuid PlayerGUID;
        public ObjectGuid OtherGUID;
        public byte TransactionType;
        public byte RankID;
        public uint TransactionDate;
    }

    public class GuildRankData
    {
        public void Write(WorldPacket data)
        {
            data.WriteUInt8(RankID);
            data.WriteInt32(RankOrder);
            data.WriteUInt32(Flags);
            data.WriteInt32(WithdrawGoldLimit);

            for (byte i = 0; i < GuildConst.MaxBankTabs; i++)
            {
                data.WriteInt32((int)TabFlags[i]);
                data.WriteInt32(TabWithdrawItemLimit[i]);
            }

            data.WriteBits(RankName.GetByteCount(), 7);
            data.FlushBits();
            data.WriteString(RankName);
        }

        public byte RankID;
        public int RankOrder;
        public uint Flags;
        public int WithdrawGoldLimit;
        public string RankName;
        public GuildBankRights[] TabFlags = new GuildBankRights[GuildConst.MaxBankTabs];
        public int[] TabWithdrawItemLimit = new int[GuildConst.MaxBankTabs];
    }

    public class GuildRewardItem
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(ItemID);
            data.WriteUInt32(Unk4);
            data.WriteInt32(AchievementsRequired.Count);
            data.WriteUInt64((ulong)RaceMask);
            data.WriteInt32(MinGuildLevel);
            data.WriteInt32(MinGuildRep);
            data.WriteInt64(Cost);

            foreach (var achievementId in AchievementsRequired)
                data.WriteInt32(achievementId);
        }
        
        public int ItemID;
        public uint Unk4;
        public List<int> AchievementsRequired = new();
        public RaceMask RaceMask;
        public int MinGuildLevel;
        public int MinGuildRep;
        public long Cost;
    }

    public class GuildBankItemInfo
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(Slot);
            data.WriteInt32(Count);
            data.WriteInt32(EnchantmentID);
            data.WriteInt32(Charges);
            data.WriteInt32(OnUseEnchantmentID);
            data.WriteUInt32(Flags);
            Item.Write(data);

            data.WriteBits(SocketEnchant.Count, 2);
            data.WriteBit(Locked);
            data.FlushBits();

            foreach (ItemGemData socketEnchant in SocketEnchant)
                socketEnchant.Write(data);
        }

        public ItemInstance Item = new();
        public int Slot;
        public int Count;
        public int EnchantmentID;
        public int Charges;
        public int OnUseEnchantmentID;
        public uint Flags;
        public bool Locked;
        public List<ItemGemData> SocketEnchant = new();
    }

    public struct GuildBankTabInfo
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(TabIndex);
            data.WriteBits(Name.GetByteCount(), 7);
            data.WriteBits(Icon.GetByteCount(), 9);

            data.WriteString(Name);
            data.WriteString(Icon);
        }

        public int TabIndex;
        public string Name;
        public string Icon;
    }

    public struct GBank_X_Inventory_ItemStackInfo
    {
        public void Read(WorldPacket data)
        {
            Banker = data.ReadPackedGuid();
            BankTab = data.ReadUInt8();
            BankSlot = data.ReadUInt8(); ;
            ContainerItemSlot = data.ReadUInt8();
            StackCount = data.ReadInt32();

            if (data.HasBit())
                ContainerSlot = data.ReadUInt8();
        }

        public ObjectGuid Banker;
        public byte BankTab;
        public byte BankSlot;
        public byte? ContainerSlot;
        public byte ContainerItemSlot;
        public int StackCount;
    }

    public struct GBank_X_Inventory_ItemInfo
    {
        public void Read(WorldPacket data)
        {
            Banker = data.ReadPackedGuid();
            BankTab = data.ReadUInt8();
            BankSlot = data.ReadUInt8(); ;
            ContainerItemSlot = data.ReadUInt8();

            if (data.HasBit())
                ContainerSlot = data.ReadUInt8();
        }

        public ObjectGuid Banker;
        public byte BankTab;
        public byte BankSlot;
        public byte? ContainerSlot;
        public byte ContainerItemSlot;
    }

    public struct GBank_X_GBank_ItemStackInfo
    {
        public void Read(WorldPacket data)
        {
            Banker = data.ReadPackedGuid();
            BankTab = data.ReadUInt8();
            BankSlot = data.ReadUInt8();
            BankTab1 = data.ReadUInt8();
            BankSlot1 = data.ReadUInt8();
            StackCount = data.ReadInt32();
        }

        public ObjectGuid Banker;
        public byte BankTab;
        public byte BankSlot;
        public byte BankTab1;
        public byte BankSlot1;
        public int StackCount;
    }

    public struct GBank_X_GBank_ItemInfo
    {
        public void Read(WorldPacket data)
        {
            Banker = data.ReadPackedGuid();
            BankTab = data.ReadUInt8();
            BankSlot = data.ReadUInt8();
            BankTab1 = data.ReadUInt8();
            BankSlot1 = data.ReadUInt8();
        }

        public ObjectGuid Banker;
        public byte BankTab;
        public byte BankSlot;
        public byte BankTab1;
        public byte BankSlot1;
    }

    public class GuildBankLogEntry
    {
        public ObjectGuid PlayerGUID;
        public uint TimeOffset;
        public sbyte EntryType;
        public long? Money;
        public int? ItemID;
        public int? Count;
        public sbyte? OtherTab;
    }

    public class GuildNewsEvent
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(Id);
            CompletedDate.Write(data);
            data.WriteInt32(Type);
            data.WriteInt32(Flags);

            for (byte i = 0; i < 2; i++)
                data.WriteInt32(Data[i]);

            data.WritePackedGuid(MemberGuid);
            data.WriteInt32(MemberList.Count);

            foreach (ObjectGuid memberGuid in MemberList)
                data.WritePackedGuid(memberGuid);

            data.WriteBit(Item != null);
            data.FlushBits();

            if (Item != null)
                Item.Write(data);
        }

        public int Id;
        public WowTime CompletedDate;
        public int Type;
        public int Flags;
        public int[] Data = new int[2];
        public ObjectGuid MemberGuid;
        public List<ObjectGuid> MemberList = new();
        public ItemInstance Item;
    }
}