// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    class BlackMarketOpen : ClientPacket
    {
        public BlackMarketOpen(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Guid = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Guid;
    }

    class BlackMarketRequestItems : ClientPacket
    {
        public BlackMarketRequestItems(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Guid = _worldPacket.ReadPackedGuid();
            LastUpdateID = _worldPacket.ReadInt64();
        }

        public ObjectGuid Guid;
        public long LastUpdateID;
    }

    public class BlackMarketRequestItemsResult : ServerPacket
    {
        public BlackMarketRequestItemsResult() : base(ServerOpcodes.BlackMarketRequestItemsResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt64(LastUpdateID);
            _worldPacket.WriteInt32(Items.Count);

            foreach (BlackMarketItem item in Items)
                item.Write(_worldPacket);
        }

        public long LastUpdateID;
        public List<BlackMarketItem> Items = new();
    }

    class BlackMarketBidOnItem : ClientPacket
    {
        public BlackMarketBidOnItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Guid = _worldPacket.ReadPackedGuid();
            MarketID = _worldPacket.ReadInt32();
            BidAmount = _worldPacket.ReadUInt64();
            Item.Read(_worldPacket);
        }

        public ObjectGuid Guid;
        public int MarketID;
        public ItemInstance Item = new();
        public ulong BidAmount;
    }

    class BlackMarketBidOnItemResult : ServerPacket
    {
        public BlackMarketBidOnItemResult() : base(ServerOpcodes.BlackMarketBidOnItemResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(MarketID);
            _worldPacket.WriteInt32((int)Result);
            Item.Write(_worldPacket);
        }

        public int MarketID;
        public ItemInstance Item;
        public BlackMarketError Result;
    }

    class BlackMarketOutbid : ServerPacket
    {
        public BlackMarketOutbid() : base(ServerOpcodes.BlackMarketOutbid) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(MarketID);
            _worldPacket.WriteInt32(RandomPropertiesID);
            Item.Write(_worldPacket);
        }

        public int MarketID;
        public ItemInstance Item;
        public int RandomPropertiesID;
    }

    class BlackMarketWon : ServerPacket
    {
        public BlackMarketWon() : base(ServerOpcodes.BlackMarketWon) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(MarketID);
            _worldPacket.WriteInt32(RandomPropertiesID);
            Item.Write(_worldPacket);
        }

        public int MarketID;
        public ItemInstance Item;
        public int RandomPropertiesID;
    }

    public struct BlackMarketItem
    {
        public void Read(WorldPacket data)
        {
            MarketID = data.ReadInt32();
            SellerNPC = data.ReadInt32();
            Item.Read(data);
            Quantity = data.ReadUInt32();
            MinBid = data.ReadUInt64();
            MinIncrement = data.ReadUInt64();
            CurrentBid = data.ReadUInt64();
            SecondsRemaining = data.ReadInt32();
            NumBids = data.ReadInt32();
            HighBid = data.HasBit();
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32(MarketID);
            data.WriteInt32(SellerNPC);
            data.WriteUInt32(Quantity);
            data.WriteUInt64(MinBid);
            data.WriteUInt64(MinIncrement);
            data.WriteUInt64(CurrentBid);
            data.WriteInt32(SecondsRemaining);
            data.WriteInt32(NumBids);
            Item.Write(data);
            data.WriteBit(HighBid);
            data.FlushBits();
        }

        public int MarketID;
        public int SellerNPC;
        public ItemInstance Item;
        public uint Quantity;
        public ulong MinBid;
        public ulong MinIncrement;
        public ulong CurrentBid;
        public int SecondsRemaining;
        public int NumBids;
        public bool HighBid;
    }
}
