﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Entities;
using System;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    class AuctionBrowseQuery : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int Offset;
        public byte MinLevel = 1;
        public byte MaxLevel = SharedConst.MaxLevel;
        public byte Unused1007_1;
        public byte Unused1007_2;
        public AuctionHouseFilterMask Filters;
        public byte[] KnownPets;
        public sbyte MaxPetLevel;
        public AddOnInfo? TaintedBy;
        public string Name;
        public Array<AuctionListFilterClass> ItemClassFilters = new(7);
        public Array<AuctionSortDef> Sorts = new(2);

        public AuctionBrowseQuery(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            Offset = _worldPacket.ReadInt32();
            MinLevel = _worldPacket.ReadUInt8();
            MaxLevel = _worldPacket.ReadUInt8();
            Unused1007_1 = _worldPacket.ReadUInt8();
            Unused1007_2= _worldPacket.ReadUInt8();
            Filters = (AuctionHouseFilterMask)_worldPacket.ReadUInt32();
            int knownPetSize = _worldPacket.ReadInt32();
            MaxPetLevel = _worldPacket.ReadInt8();

            int sizeLimit = CliDB.BattlePetSpeciesStorage.GetNumRows() / 8 + 1;
            if (knownPetSize >= sizeLimit)
            {
                throw new System.Exception(
                    $"Attempted to read more array elements " +
                    $"from packet {knownPetSize} than allowed {sizeLimit}");
            }

            KnownPets = new byte[knownPetSize];
            for (var i = 0; i < knownPetSize; ++i)
                KnownPets[i] = _worldPacket.ReadUInt8();

            if (_worldPacket.HasBit())
                TaintedBy = new();

            int nameLength = _worldPacket.ReadBits<int>(8);
            int itemClassFilterCount = _worldPacket.ReadBits<int>(3);
            int sortSize = _worldPacket.ReadBits<int>(2);

            for (var i = 0; i < sortSize; ++i)
                Sorts[i] = new AuctionSortDef(_worldPacket);

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);

            Name = _worldPacket.ReadString(nameLength);
            for (var i = 0; i < itemClassFilterCount; ++i)// AuctionListFilterClass filterClass in ItemClassFilters)
                ItemClassFilters[i] = new AuctionListFilterClass(_worldPacket);
        }
    }

    class AuctionCancelCommoditiesPurchase : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public AddOnInfo? TaintedBy;

        public AuctionCancelCommoditiesPurchase(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();

            if (_worldPacket.HasBit())
            {
                TaintedBy = new();
                TaintedBy.Value.Read(_worldPacket);
            }
        }
    }

    class AuctionConfirmCommoditiesPurchase : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int ItemID;
        public int Quantity;
        public AddOnInfo? TaintedBy;

        public AuctionConfirmCommoditiesPurchase(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            ItemID = _worldPacket.ReadInt32();
            Quantity = _worldPacket.ReadInt32();

            if (_worldPacket.HasBit())
            {
                TaintedBy = new();
                TaintedBy.Value.Read(_worldPacket);
            }
        }
    }

    class AuctionHelloRequest : ClientPacket
    {
        public ObjectGuid Guid;

        public AuctionHelloRequest(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Guid = _worldPacket.ReadPackedGuid();
        }
    }

    class AuctionListBiddedItems : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public uint Offset;
        public Array<int> AuctionItemIDs = new(100);
        public Array<AuctionSortDef> Sorts = new(2);
        public AddOnInfo? TaintedBy;

        public AuctionListBiddedItems(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            Offset = _worldPacket.ReadUInt32();
            if (_worldPacket.HasBit())
                TaintedBy = new();

            int auctionIDCount = _worldPacket.ReadBits<int>(7);
            int sortCount = _worldPacket.ReadBits<int>(2);

            for (var i = 0; i < sortCount; ++i)
                Sorts[i] = new AuctionSortDef(_worldPacket);

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);

            for (var i = 0; i < auctionIDCount; ++i)
                AuctionItemIDs[i] = _worldPacket.ReadInt32();
        }
    }

    class AuctionListBucketsByBucketKeys : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public AddOnInfo? TaintedBy;
        public Array<AuctionBucketKey> BucketKeys = new(100);
        public Array<AuctionSortDef> Sorts = new(2);

        public AuctionListBucketsByBucketKeys(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            if (_worldPacket.HasBit())
                TaintedBy = new();

            uint bucketKeysCount = _worldPacket.ReadBits<uint>(7);
            uint sortCount = _worldPacket.ReadBits<uint>(2);

            for (var i = 0; i < sortCount; ++i)
                Sorts[i] = new AuctionSortDef(_worldPacket);

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);

            for (var i = 0; i < bucketKeysCount; ++i)
                BucketKeys[i] = new AuctionBucketKey(_worldPacket);
        }
    }

    class AuctionListItemsByBucketKey : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int Offset;
        public sbyte Unknown830;
        public AddOnInfo? TaintedBy;
        public Array<AuctionSortDef> Sorts = new(2);
        public AuctionBucketKey BucketKey;

        public AuctionListItemsByBucketKey(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            Offset = _worldPacket.ReadInt32();
            Unknown830 = _worldPacket.ReadInt8();
            if (_worldPacket.HasBit())
                TaintedBy = new();

            uint sortCount = _worldPacket.ReadBits<uint>(2);
            for (var i = 0; i < sortCount; ++i)
                Sorts[i] = new AuctionSortDef(_worldPacket);

            BucketKey = new AuctionBucketKey(_worldPacket);

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);
        }
    }

    class AuctionListItemsByItemID : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int ItemID;
        public int SuffixItemNameDescriptionID;
        public int Offset;
        public AddOnInfo? TaintedBy;
        public Array<AuctionSortDef> Sorts = new(2);

        public AuctionListItemsByItemID(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            ItemID = _worldPacket.ReadInt32();
            SuffixItemNameDescriptionID = _worldPacket.ReadInt32();
            Offset = _worldPacket.ReadInt32();

            if (_worldPacket.HasBit())
                TaintedBy = new();

            uint sortCount = _worldPacket.ReadBits<uint>(2);

            for (var i = 0; i < sortCount; ++i)
                Sorts[i] = new AuctionSortDef(_worldPacket);

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);
        }
    }

    class AuctionListOwnedItems : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int Offset;
        public AddOnInfo? TaintedBy;
        public Array<AuctionSortDef> Sorts = new(2);

        public AuctionListOwnedItems(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            Offset = _worldPacket.ReadInt32();
            if (_worldPacket.HasBit())
                TaintedBy = new();

            uint sortCount = _worldPacket.ReadBits<uint>(2);

            for (var i = 0; i < sortCount; ++i)
                Sorts[i] = new AuctionSortDef(_worldPacket);

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);
        }
    }

    class AuctionPlaceBid : ClientPacket
    {   
        public ObjectGuid Auctioneer;
        public long BidAmount;
        public int AuctionID;
        public AddOnInfo? TaintedBy;

        public AuctionPlaceBid(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            AuctionID = _worldPacket.ReadInt32();
            BidAmount = _worldPacket.ReadInt64();

            if (_worldPacket.HasBit())
            {
                TaintedBy = new();
                TaintedBy.Value.Read(_worldPacket);
            }
        }
    }

    class AuctionRemoveItem : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int AuctionID;
        public int ItemID;
        public AddOnInfo? TaintedBy;

        public AuctionRemoveItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            AuctionID = _worldPacket.ReadInt32();
            ItemID = _worldPacket.ReadInt32();

            if (_worldPacket.HasBit())
            {
                TaintedBy = new();
                TaintedBy.Value.Read(_worldPacket);
            }
        }
    }

    class AuctionReplicateItems : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int ChangeNumberGlobal;
        public int ChangeNumberCursor;
        public int ChangeNumberTombstone;
        public int Count;
        public AddOnInfo? TaintedBy;

        public AuctionReplicateItems(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            ChangeNumberGlobal = _worldPacket.ReadInt32();
            ChangeNumberCursor = _worldPacket.ReadInt32();
            ChangeNumberTombstone = _worldPacket.ReadInt32();
            Count = _worldPacket.ReadInt32();

            if (_worldPacket.HasBit())
            {
                TaintedBy = new();
                TaintedBy.Value.Read(_worldPacket);
            }
        }
    }

    class AuctionRequestFavoriteList : ClientPacket
    {
        public AuctionRequestFavoriteList(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }
    
    class AuctionSellCommodity : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public long UnitPrice;
        public TimeSpan RunTime;
        public AddOnInfo? TaintedBy;
        public Array<AuctionItemForSale> Items = new(64);

        public AuctionSellCommodity(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            UnitPrice = _worldPacket.ReadInt64();
            RunTime = (Minutes)_worldPacket.ReadInt32();
            if (_worldPacket.HasBit())
                TaintedBy = new();

            uint itemCount = _worldPacket.ReadBits<uint>(6);

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);

            for (var i = 0; i < itemCount; ++i)
                Items[i] = new AuctionItemForSale(_worldPacket);
        }
    }

    class AuctionSellItem : ClientPacket
    {
        public long BuyoutPrice;
        public ObjectGuid Auctioneer;
        public long MinBid;
        public TimeSpan RunTime;
        public AddOnInfo? TaintedBy;
        public Array<AuctionItemForSale> Items = new(1);

        public AuctionSellItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            MinBid = _worldPacket.ReadInt64();
            BuyoutPrice = _worldPacket.ReadInt64();
            RunTime = (Minutes)_worldPacket.ReadInt32();

            if (_worldPacket.HasBit())
                TaintedBy = new();

            uint itemCount = _worldPacket.ReadBits<uint>(6);

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);

            for (var i = 0; i < itemCount; ++i)
                Items[i] = new AuctionItemForSale(_worldPacket);
        }
    }

    class AuctionSetFavoriteItem : ClientPacket
    {    
        public AuctionFavoriteInfo Item;
        public bool IsNotFavorite = true;

        public AuctionSetFavoriteItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            IsNotFavorite = _worldPacket.HasBit();
            Item = new AuctionFavoriteInfo(_worldPacket);
        }
    }

    class AuctionGetCommodityQuote : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int ItemID;
        public int Quantity;
        public AddOnInfo? TaintedBy;

        public AuctionGetCommodityQuote(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            ItemID = _worldPacket.ReadInt32();
            Quantity = _worldPacket.ReadInt32();

            if (_worldPacket.HasBit())
            {
                TaintedBy = new();
                TaintedBy.Value.Read(_worldPacket);
            }
        }
    }

    class AuctionClosedNotification : ServerPacket
    { 
        public AuctionOwnerNotification Info;
        public TimeSpan ProceedsMailDelay;
        public bool Sold = true;

        public AuctionClosedNotification() : base(ServerOpcodes.AuctionClosedNotification) { }

        public override void Write()
        {
            Info.Write(_worldPacket);
            _worldPacket.WriteFloat((float)ProceedsMailDelay.TotalSeconds);
            _worldPacket.WriteBit(Sold);
            _worldPacket.FlushBits();
        }
    }

    class AuctionCommandResult : ServerPacket
    {
        ///<summary> the id of the auction that triggered this notification</summary>
        public int AuctionID;
        ///<summary> the Type of action that triggered this notification. Possible values are @ref AuctionAction</summary>
        public int Command;
        ///<summary> the error code that was generated when trying to perform the action. Possible values are @ref AuctionError</summary>
        public AuctionResult ErrorCode;
        ///<summary> the bid error. Possible values are @ref AuctionError</summary>
        public InventoryResult BagResult;
        ///<summary> the GUID of the bidder for this auction.</summary>
        public ObjectGuid Guid;
        ///<summary> the sum of outbid is (1% of current bid) * 5, if the bid is too small, then this value is 1 copper.</summary>
        public long MinIncrement;
        ///<summary> the amount of money that the player bid in copper</summary>
        public long Money; 
        public TimeSpan DesiredDelay;

        public AuctionCommandResult() : base(ServerOpcodes.AuctionCommandResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(AuctionID);
            _worldPacket.WriteInt32(Command);
            _worldPacket.WriteInt32((int)ErrorCode);
            _worldPacket.WriteInt32((int)BagResult);
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteInt64(MinIncrement);
            _worldPacket.WriteInt64(Money);
            _worldPacket.WriteInt32((Seconds)DesiredDelay);
        }
    }

    class AuctionGetCommodityQuoteResult : ServerPacket
    {
        public AuctionGetCommodityQuoteResult() : base(ServerOpcodes.AuctionGetCommodityQuoteResult) { }

        public override void Write()
        {
            _worldPacket.WriteBit(TotalPrice.HasValue);
            _worldPacket.WriteBit(Quantity.HasValue);
            _worldPacket.WriteBit(QuoteDuration.HasValue);
            _worldPacket.WriteInt32(ItemID);
            _worldPacket.WriteInt32((Seconds)DesiredDelay);

            if (TotalPrice.HasValue)
                _worldPacket.WriteInt64(TotalPrice.Value);

            if (Quantity.HasValue)
                _worldPacket.WriteInt32(Quantity.Value);

            if (QuoteDuration.HasValue)
                _worldPacket.WriteInt64(QuoteDuration.Value.ToMilliseconds());
        }

        public long? TotalPrice;
        public int? Quantity;
        public TimeSpan? QuoteDuration;
        public int ItemID;
        public TimeSpan DesiredDelay;
    }

    class AuctionHelloResponse : ServerPacket
    {
        public ObjectGuid Guid;
        public uint PurchasedItemDeliveryDelay;
        public uint CancelledItemDeliveryDelay;
        public bool OpenForBusiness = true;

        public AuctionHelloResponse() : base(ServerOpcodes.AuctionHelloResponse) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteUInt32(PurchasedItemDeliveryDelay);
            _worldPacket.WriteUInt32(CancelledItemDeliveryDelay);
            _worldPacket.WriteBit(OpenForBusiness);
            _worldPacket.FlushBits();
        }
    }

    public class AuctionListBiddedItemsResult : ServerPacket
    {   
        public List<AuctionItem> Items = new();
        public uint DesiredDelay;
        public bool HasMoreResults;

        public AuctionListBiddedItemsResult() : base(ServerOpcodes.AuctionListBiddedItemsResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Items.Count);
            _worldPacket.WriteUInt32(DesiredDelay);
            _worldPacket.WriteBit(HasMoreResults);
            _worldPacket.FlushBits();

            foreach (AuctionItem item in Items)
                item.Write(_worldPacket);
        }
    }

    public class AuctionListBucketsResult : ServerPacket
    {  
        public List<BucketInfo> Buckets = new();
        public uint DesiredDelay;
        public int Unknown830_0;
        public int Unknown830_1;
        public AuctionHouseBrowseMode BrowseMode;
        public bool HasMoreResults;

        public AuctionListBucketsResult() : base(ServerOpcodes.AuctionListBucketsResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Buckets.Count);
            _worldPacket.WriteUInt32(DesiredDelay);
            _worldPacket.WriteInt32(Unknown830_0);
            _worldPacket.WriteInt32(Unknown830_1);
            _worldPacket.WriteBits((int)BrowseMode, 1);
            _worldPacket.WriteBit(HasMoreResults);
            _worldPacket.FlushBits();

            foreach (BucketInfo bucketInfo in Buckets)
                bucketInfo.Write(_worldPacket);
        }
    }

    class AuctionFavoriteList : ServerPacket
    {    
        public uint DesiredDelay;
        public List<AuctionFavoriteInfo> Items = new();

        public AuctionFavoriteList() : base(ServerOpcodes.AuctionFavoriteList) { }

        public override void Write()
        {
            _worldPacket.WriteUInt32(DesiredDelay);
            _worldPacket.WriteBits(Items.Count, 7);
            _worldPacket.FlushBits();

            foreach (AuctionFavoriteInfo favoriteInfo in Items)
                favoriteInfo.Write(_worldPacket);
        }
    }

    public class AuctionListItemsResult : ServerPacket
    {
        public List<AuctionItem> Items = new();
        public uint Unknown830;
        public int TotalCount;
        public uint DesiredDelay;
        public AuctionHouseListType ListType;
        public bool HasMoreResults;
        public AuctionBucketKey BucketKey = new();

        public AuctionListItemsResult() : base(ServerOpcodes.AuctionListItemsResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Items.Count);
            _worldPacket.WriteUInt32(Unknown830);
            _worldPacket.WriteInt32(TotalCount);
            _worldPacket.WriteUInt32(DesiredDelay);
            _worldPacket.WriteBits((int)ListType, 2);
            _worldPacket.WriteBit(HasMoreResults);
            _worldPacket.FlushBits();

            BucketKey.Write(_worldPacket);

            foreach (AuctionItem item in Items)
                item.Write(_worldPacket);
        }
    }

    public class AuctionListOwnedItemsResult : ServerPacket
    {   
        public List<AuctionItem> Items = new();
        public List<AuctionItem> SoldItems = new();
        public uint DesiredDelay;
        public bool HasMoreResults;

        public AuctionListOwnedItemsResult() : base(ServerOpcodes.AuctionListOwnedItemsResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Items.Count);
            _worldPacket.WriteInt32(SoldItems.Count);
            _worldPacket.WriteUInt32(DesiredDelay);
            _worldPacket.WriteBit(HasMoreResults);
            _worldPacket.FlushBits();

            foreach (AuctionItem item in Items)
                item.Write(_worldPacket);

            foreach (AuctionItem item in SoldItems)
                item.Write(_worldPacket);
        }
    }

    class AuctionOutbidNotification : ServerPacket
    {    
        public AuctionBidderNotification Info;
        public long BidAmount;
        public long MinIncrement;

        public AuctionOutbidNotification() : base(ServerOpcodes.AuctionOutbidNotification) { }

        public override void Write()
        {
            Info.Write(_worldPacket);
            _worldPacket.WriteInt64(BidAmount);
            _worldPacket.WriteInt64(MinIncrement);
        }
    }

    class AuctionOwnerBidNotification : ServerPacket
    {    
        public AuctionOwnerNotification Info;
        public ObjectGuid Bidder;
        public long MinIncrement;

        public AuctionOwnerBidNotification() : base(ServerOpcodes.AuctionOwnerBidNotification) { }

        public override void Write()
        {
            Info.Write(_worldPacket);
            _worldPacket.WriteInt64(MinIncrement);
            _worldPacket.WritePackedGuid(Bidder);
        }
    }

    public class AuctionReplicateResponse : ServerPacket
    { 
        public int ChangeNumberCursor;
        public int ChangeNumberGlobal;
        public TimeSpan DesiredDelay;
        public int ChangeNumberTombstone;
        public int Result;
        public List<AuctionItem> Items = new();

        public AuctionReplicateResponse() : base(ServerOpcodes.AuctionReplicateResponse) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Result);
            _worldPacket.WriteInt32((Milliseconds)DesiredDelay);
            _worldPacket.WriteInt32(ChangeNumberGlobal);
            _worldPacket.WriteInt32(ChangeNumberCursor);
            _worldPacket.WriteInt32(ChangeNumberTombstone);
            _worldPacket.WriteInt32(Items.Count);

            foreach (AuctionItem item in Items)
                item.Write(_worldPacket);
        }
    }

    class AuctionWonNotification : ServerPacket
    {  
        public AuctionBidderNotification Info;

        public AuctionWonNotification() : base(ServerOpcodes.AuctionWonNotification) { }

        public override void Write()
        {
            Info.Write(_worldPacket);
        }
    }

    //Structs
    public class AuctionBucketKey
    {
        public int ItemID;
        public ushort ItemLevel;
        public ushort? BattlePetSpeciesID;
        public ushort? SuffixItemNameDescriptionID;

        public AuctionBucketKey() { }

        public AuctionBucketKey(AuctionsBucketKey key)
        {
            ItemID = key.ItemId;
            ItemLevel = key.ItemLevel;

            if (key.BattlePetSpeciesId != 0)
                BattlePetSpeciesID = key.BattlePetSpeciesId;

            if (key.SuffixItemNameDescriptionId != 0)
                SuffixItemNameDescriptionID = key.SuffixItemNameDescriptionId;
        }

        public AuctionBucketKey(WorldPacket data)
        {
            data.ResetBitPos();
            ItemID = data.ReadBits<int>(20);
            bool hasBattlePetSpeciesId = data.HasBit();
            ItemLevel = data.ReadBits<ushort>(11);
            bool hasSuffixItemNameDescriptionId = data.HasBit();

            if (hasBattlePetSpeciesId)
                BattlePetSpeciesID = data.ReadUInt16();

            if (hasSuffixItemNameDescriptionId)
                SuffixItemNameDescriptionID = data.ReadUInt16();
        }

        public void Write(WorldPacket data)
        {
            data.WriteBits(ItemID, 20);
            data.WriteBit(BattlePetSpeciesID.HasValue);
            data.WriteBits(ItemLevel, 11);
            data.WriteBit(SuffixItemNameDescriptionID.HasValue);
            data.FlushBits();

            if (BattlePetSpeciesID.HasValue)
                data.WriteUInt16(BattlePetSpeciesID.Value);

            if (SuffixItemNameDescriptionID.HasValue)
                data.WriteUInt16(SuffixItemNameDescriptionID.Value);
        }
    }

    public struct AuctionListFilterSubClass
    {
        public int ItemSubclass;
        public ulong InvTypeMask;

        public AuctionListFilterSubClass(WorldPacket data)
        {
            InvTypeMask = data.ReadUInt64();
            ItemSubclass = data.ReadInt32();
        }
    }

    public class AuctionListFilterClass
    {
        public int ItemClass;
        public Array<AuctionListFilterSubClass> SubClassFilters = new(31);

        public AuctionListFilterClass(WorldPacket data)
        {
            ItemClass = data.ReadInt32();
            uint subClassFilterCount = data.ReadBits<uint>(5);

            for (var i =  0; i < subClassFilterCount; ++i)
                SubClassFilters[i] = new AuctionListFilterSubClass(data);
        }
    }

    public struct AuctionSortDef
    {
        public AuctionHouseSortOrder SortOrder;
        public bool ReverseSort;

        public AuctionSortDef(AuctionHouseSortOrder sortOrder, bool reverseSort)
        {
            SortOrder = sortOrder;
            ReverseSort = reverseSort;
        }

        public AuctionSortDef(WorldPacket data)
        {
            data.ResetBitPos();
            SortOrder = (AuctionHouseSortOrder)data.ReadBits<uint>(4);
            ReverseSort = data.HasBit();
        }
    }

    public struct AuctionItemForSale
    {
        public ObjectGuid Guid;
        public int UseCount;

        public AuctionItemForSale(WorldPacket data)
        {
            Guid = data.ReadPackedGuid();
            UseCount = data.ReadInt32();
        }
    }

    public struct AuctionFavoriteInfo
    {
        public int Order;
        public int ItemID;
        public int ItemLevel;
        public int BattlePetSpeciesID;
        public int SuffixItemNameDescriptionID;

        public AuctionFavoriteInfo(WorldPacket data)
        {
            Order = data.ReadInt32();
            ItemID = data.ReadInt32();
            ItemLevel = data.ReadInt32();
            BattlePetSpeciesID = data.ReadInt32();
            SuffixItemNameDescriptionID = data.ReadInt32();
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32(Order);
            data.WriteInt32(ItemID);
            data.WriteInt32(ItemLevel);
            data.WriteInt32(BattlePetSpeciesID);
            data.WriteInt32(SuffixItemNameDescriptionID);
        }
    }

    public struct AuctionOwnerNotification
    {
        public int AuctionID;
        public long BidAmount;
        public ItemInstance Item;

        public void Initialize(AuctionPosting auction)
        {
            AuctionID = auction.Id;
            Item = new ItemInstance(auction.Items[0]);
            BidAmount = auction.BidAmount;
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32(AuctionID);
            data.WriteInt64(BidAmount);
            Item.Write(data);
        }
    }

    public class BucketInfo
    {
        public AuctionBucketKey Key;
        public int TotalQuantity;
        public long MinPrice;
        public int RequiredLevel;
        public List<int> ItemModifiedAppearanceIDs = new();
        public byte? MaxBattlePetQuality;
        public byte? MaxBattlePetLevel;
        public byte? BattlePetBreedID;
        public uint? Unk901_1;
        public bool ContainsOwnerItem;
        public bool ContainsOnlyCollectedAppearances;

        public void Write(WorldPacket data)
        {
            Key.Write(data);
            data.WriteInt32(TotalQuantity);
            data.WriteInt32(RequiredLevel);
            data.WriteInt64(MinPrice);
            data.WriteInt32(ItemModifiedAppearanceIDs.Count);
            if (!ItemModifiedAppearanceIDs.Empty())
            {
                foreach (int id in ItemModifiedAppearanceIDs)
                    data.WriteInt32(id);
            }

            data.WriteBit(MaxBattlePetQuality.HasValue);
            data.WriteBit(MaxBattlePetLevel.HasValue);
            data.WriteBit(BattlePetBreedID.HasValue);
            data.WriteBit(Unk901_1.HasValue);
            data.WriteBit(ContainsOwnerItem);
            data.WriteBit(ContainsOnlyCollectedAppearances);
            data.FlushBits();

            if (MaxBattlePetQuality.HasValue)
                data.WriteUInt8(MaxBattlePetQuality.Value);

            if (MaxBattlePetLevel.HasValue)
                data.WriteUInt8(MaxBattlePetLevel.Value);

            if (BattlePetBreedID.HasValue)
                data.WriteUInt8(BattlePetBreedID.Value);

            if (Unk901_1.HasValue)
                data.WriteUInt32(Unk901_1.Value);
        }
    }

    public class AuctionItem
    {
        public ItemInstance Item;
        public int Count;
        public int Charges;
        public List<ItemEnchantData> Enchantments = new();
        public uint Flags;
        public int AuctionID;
        public ObjectGuid Owner;
        public long? MinBid;
        public long? MinIncrement;
        public long? BuyoutPrice;
        public long? UnitPrice;
        public TimeSpan DurationLeft;
        public byte DeleteReason;
        public bool CensorServerSideInfo;
        public bool CensorBidInfo;
        public ObjectGuid ItemGuid;
        public ObjectGuid OwnerAccountID;
        public ServerTime EndTime;
        public ObjectGuid? Bidder;
        public long? BidAmount;
        public List<ItemGemData> Gems = new();
        public AuctionBucketKey AuctionBucketKey;
        public ObjectGuid? Creator;

        public void Write(WorldPacket data)
        {
            data.WriteBit(Item != null);
            data.WriteBits(Enchantments.Count, 4);
            data.WriteBits(Gems.Count, 2);
            data.WriteBit(MinBid.HasValue);
            data.WriteBit(MinIncrement.HasValue);
            data.WriteBit(BuyoutPrice.HasValue);
            data.WriteBit(UnitPrice.HasValue);
            data.WriteBit(CensorServerSideInfo);
            data.WriteBit(CensorBidInfo);
            data.WriteBit(AuctionBucketKey != null);
            data.WriteBit(Creator.HasValue);
            if (!CensorBidInfo)
            {
                data.WriteBit(Bidder.HasValue);
                data.WriteBit(BidAmount.HasValue);
            }

            data.FlushBits();

            if (Item != null)
                Item.Write(data);

            data.WriteInt32(Count);
            data.WriteInt32(Charges);
            data.WriteUInt32(Flags);
            data.WriteInt32(AuctionID);
            data.WritePackedGuid(Owner);
            data.WriteInt32((Milliseconds)DurationLeft);
            data.WriteUInt8(DeleteReason);

            foreach (ItemEnchantData enchant in Enchantments)
                enchant.Write(data);

            if (MinBid.HasValue)
                data.WriteInt64(MinBid.Value);

            if (MinIncrement.HasValue)
                data.WriteInt64(MinIncrement.Value);

            if (BuyoutPrice.HasValue)
                data.WriteInt64(BuyoutPrice.Value);

            if (UnitPrice.HasValue)
                data.WriteInt64(UnitPrice.Value);

            if (!CensorServerSideInfo)
            {
                data.WritePackedGuid(ItemGuid);
                data.WritePackedGuid(OwnerAccountID);
                data.WriteInt32((UnixTime)EndTime);
            }

            if (Creator.HasValue)
                data.WritePackedGuid(Creator.Value);

            if (!CensorBidInfo)
            {
                if (Bidder.HasValue)
                    data.WritePackedGuid(Bidder.Value);

                if (BidAmount.HasValue)
                    data.WriteInt64(BidAmount.Value);
            }

            foreach (ItemGemData gem in Gems)
                gem.Write(data);

            if (AuctionBucketKey != null)
                AuctionBucketKey.Write(data);
        }
    }

    struct AuctionBidderNotification
    {
        public int AuctionID;
        public ObjectGuid Bidder;
        public ItemInstance Item;

        public void Initialize(AuctionPosting auction, Item item)
        {
            AuctionID = auction.Id;
            Item = new ItemInstance(item);
            Bidder = auction.Bidder;
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32(AuctionID);
            data.WritePackedGuid(Bidder);
            Item.Write(data);
        }
    }
}
