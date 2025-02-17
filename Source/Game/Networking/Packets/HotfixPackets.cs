﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.IO;
using System.Collections.Generic;
using Game.DataStorage;
using System;

namespace Game.Networking.Packets
{
    class DBQueryBulk : ClientPacket
    {
        public DBQueryBulk(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            TableHash = _worldPacket.ReadUInt32();

            int count = _worldPacket.ReadBits<int>(13);
            for (int i = 0; i < count; ++i)
            {
                Queries.Add(new DBQueryRecord(_worldPacket.ReadInt32()));
            }
        }

        public uint TableHash;
        public List<DBQueryRecord> Queries = new();

        public struct DBQueryRecord
        {
            public DBQueryRecord(int recordId)
            {
                RecordID = recordId;
            }

            public int RecordID;
        }
    }

    public class DBReply : ServerPacket
    {
        public DBReply() : base(ServerOpcodes.DbReply) { }

        public override void Write()
        {
            _worldPacket.WriteUInt32(TableHash);
            _worldPacket.WriteInt32(RecordID);
            _worldPacket.WriteInt32((UnixTime)Timestamp);
            _worldPacket.WriteBits((byte)Status, 3);
            _worldPacket.WriteUInt32(Data.GetSize());
            _worldPacket.WriteBytes(Data.GetData());
        }

        public uint TableHash;
        public ServerTime Timestamp;
        public int RecordID;
        public HotfixRecord.Status Status = HotfixRecord.Status.Invalid;

        public ByteBuffer Data = new();
    }

    class AvailableHotfixes : ServerPacket
    {
        public AvailableHotfixes() : base(ServerOpcodes.AvailableHotfixes) { }

        public override void Write()
        {
            _worldPacket.WriteUInt32(VirtualRealmAddress);
            _worldPacket.WriteInt32(Hotfixes.Count);

            foreach (var hotfixId in Hotfixes)
                hotfixId.Write(_worldPacket);
        }

        public uint VirtualRealmAddress;
        public List<HotfixId> Hotfixes = new();
    }

    class HotfixRequest : ClientPacket
    {
        public HotfixRequest(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ClientBuild = _worldPacket.ReadUInt32();
            DataBuild = _worldPacket.ReadUInt32();

            uint hotfixCount = _worldPacket.ReadUInt32();
            Cypher.Assert(DB2Manager.Instance.GetHotfixCount() >= hotfixCount,
                "PacketArrayMaxCapacityException", "HotfixRequest : ClientPacket");
            
            for (var i = 0; i < hotfixCount; ++i)
                Hotfixes.Add(_worldPacket.ReadInt32());
        }

        public uint ClientBuild;
        public uint DataBuild;
        public List<int> Hotfixes = new();
    }

    class HotfixConnect : ServerPacket
    {
        public HotfixConnect() : base(ServerOpcodes.HotfixConnect) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Hotfixes.Count);
            foreach (HotfixData hotfix in Hotfixes)
                hotfix.Write(_worldPacket);

            _worldPacket.WriteUInt32(HotfixContent.GetSize());
            _worldPacket.WriteBytes(HotfixContent);
        }

        public List<HotfixData> Hotfixes = new();
        public ByteBuffer HotfixContent = new();

        public class HotfixData
        {
            public void Write(WorldPacket data)
            {
                Record.Write(data);
                data.WriteUInt32(Size);
                data.WriteBits((byte)Record.HotfixStatus, 3);
                data.FlushBits();
            }

            public HotfixRecord Record = new();
            public uint Size;
        }
    }
}
