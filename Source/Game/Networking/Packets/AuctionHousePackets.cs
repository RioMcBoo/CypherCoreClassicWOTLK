// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Entities;
using System;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    public class AuctionListItems : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int Offset;
        public byte MinLevel;
        public byte MaxLevel;
        public ItemQuality Quality;
        public byte[] KnownPets;
        public sbyte MaxPetLevel;
        public AddOnInfo? TaintedBy;
        public string Name;
        public bool UsableOnly;
        public bool ExactMatch;
        public Array<AuctionListFilterClass> ItemClassFilters = new((int)ItemClass.Max);
        public Array<AuctionSortDef> Sorts = new(8);

        public AuctionListItems(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            Offset = _worldPacket.ReadInt32();
            MinLevel = _worldPacket.ReadUInt8();
            MaxLevel = _worldPacket.ReadUInt8();
            Quality = (ItemQuality)_worldPacket.ReadInt32();
            int sortsCount = _worldPacket.ReadUInt8();
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
            Name = _worldPacket.ReadString(nameLength);

            _worldPacket.ResetBitPos();
            int itemClassFilterCount = _worldPacket.ReadBits<int>(3);
            UsableOnly = _worldPacket.HasBit();
            ExactMatch = _worldPacket.HasBit();

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);

            for (var i = 0; i < itemClassFilterCount; ++i)
                ItemClassFilters[i] = new AuctionListFilterClass(_worldPacket);

            int sortDataSize = _worldPacket.ReadInt32();
            for (var i = 0; i < sortsCount; ++i)
            {
                var current = new AuctionSortDef(_worldPacket);
                Cypher.Assert(!Sorts.Contains(current));
                Sorts.Add(current);
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

    class AuctionHelloResponse : ServerPacket
    {
        public ObjectGuid Guid;
        public Milliseconds PurchasedItemDeliveryDelay;
        public Milliseconds CancelledItemDeliveryDelay;
        public AuctionHouseId AuctionHouseId;
        public bool OpenForBusiness = true;

        public AuctionHelloResponse() : base(ServerOpcodes.AuctionHelloResponse) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteInt32(PurchasedItemDeliveryDelay);
            _worldPacket.WriteInt32(CancelledItemDeliveryDelay);
            _worldPacket.WriteInt32((int)AuctionHouseId);
            _worldPacket.WriteBit(OpenForBusiness);
            _worldPacket.FlushBits();
        }
    }

    class AuctionListBidderItems : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int Offset;
        public Array<int> AuctionItemIDs = new(100);
        public Array<AuctionSortDef> Sorts = new(8);
        public AddOnInfo? TaintedBy;

        public AuctionListBidderItems(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            Offset = _worldPacket.ReadInt32();            

            int auctionIDCount = _worldPacket.ReadBits<int>(7);
            if (_worldPacket.HasBit())
                TaintedBy = new();

            if (TaintedBy.HasValue)
                TaintedBy.Value.Read(_worldPacket);

            for (var i = 0; i < auctionIDCount; ++i)
                AuctionItemIDs[i] = _worldPacket.ReadInt32();
        }
    }

    public class AuctionListBidderItemsResult : ServerPacket
    {
        public List<AuctionItem> Items = new();
        public int TotalCount;
        public Milliseconds DesiredDelay;

        public AuctionListBidderItemsResult() : base(ServerOpcodes.AuctionListBidderItemsResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Items.Count);
            _worldPacket.WriteInt32(TotalCount);
            _worldPacket.WriteInt32(DesiredDelay);

            foreach (AuctionItem item in Items)
                item.Write(_worldPacket);
        }
    }

    class AuctionListOwnerItems : ClientPacket
    {
        public ObjectGuid Auctioneer;
        public int Offset;
        public AddOnInfo? TaintedBy;

        public AuctionListOwnerItems(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Auctioneer = _worldPacket.ReadPackedGuid();
            Offset = _worldPacket.ReadInt32();

            if (_worldPacket.HasBit())
                TaintedBy = new();

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

    public class AuctionListItemsResult : ServerPacket
    {
        public List<AuctionItem> Items = new();
        public int TotalCount;
        public Milliseconds DesiredDelay;
        public bool OnlyUsable;

        public AuctionListItemsResult() : base(ServerOpcodes.AuctionListItemsResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Items.Count);
            _worldPacket.WriteInt32(TotalCount);
            _worldPacket.WriteInt32(DesiredDelay);
            _worldPacket.WriteBit(OnlyUsable);
            _worldPacket.FlushBits();

            foreach (AuctionItem item in Items)
                item.Write(_worldPacket);
        }
    }

    public class AuctionListOwnerItemsResult : ServerPacket
    {
        public List<AuctionItem> Items = new();
        public int TotalCount;
        public Milliseconds DesiredDelay;

        public AuctionListOwnerItemsResult() : base(ServerOpcodes.AuctionListOwnerItemsResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Items.Count);
            _worldPacket.WriteInt32(TotalCount);
            _worldPacket.WriteInt32(DesiredDelay);

            foreach (AuctionItem item in Items)
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

    class AuctionWonBidNotification : ServerPacket
    {  
        public AuctionWonNotification Info;

        public AuctionWonBidNotification() : base(ServerOpcodes.AuctionWonNotification) { }

        public override void Write()
        {
            Info.Write(_worldPacket);
        }
    }

    class AuctionListPendingSales: ClientPacket
    {
        public AuctionListPendingSales(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class AuctionListPendingSalesResult : ServerPacket
    {
        public AuctionListPendingSalesResult() : base(ServerOpcodes.AuctionListPendingSalesResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Mails.Count);
            _worldPacket.WriteInt32(TotalNumRecords);
            foreach (var mail in Mails)
                mail.Write(_worldPacket);
        }

        public int TotalNumRecords;
        public List<MailListEntry> Mails = new();
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
        public ItemSubClass ItemSubclass;
        public InventoryTypeMask InvTypeMask;

        public AuctionListFilterSubClass(WorldPacket data)
        {
            InvTypeMask = (InventoryTypeMask)data.ReadUInt64();
            ItemSubclass = data.ReadInt32();
        }
    }

    public class AuctionListFilterClass
    {
        public ItemClass ItemClass;
        public Array<AuctionListFilterSubClass> SubClassFilters = new(ItemConst.MaxItemSubclassTotal);

        public AuctionListFilterClass(WorldPacket data)
        {
            ItemClass = (ItemClass)data.ReadInt32();
            int subClassFilterCount = data.ReadBits<int>(5);
            Cypher.Assert(subClassFilterCount <= ItemConst.MaxItemSubclassTotal);

            for (var i =  0; i < subClassFilterCount; ++i)
                SubClassFilters[i] = new AuctionListFilterSubClass(data);
        }
    }

    public struct AuctionSortDef
    {
        public AuctionHouseSortOrder Order;
        public AuctionHouseSortDirection Direction;

        public AuctionSortDef(AuctionHouseSortOrder order, AuctionHouseSortDirection direction)
        {
            Order = order;
            Direction = direction;
        }

        public AuctionSortDef(WorldPacket data)
        {
            data.ResetBitPos();
            Order = (AuctionHouseSortOrder)data.ReadUInt8();
            Direction = (AuctionHouseSortDirection)data.ReadUInt8();
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

    public struct AuctionOwnerNotification
    {
        public int AuctionID;
        public long BidAmount;
        public ItemInstance Item;

        public void Initialize(AuctionPosting auction)
        {
            AuctionID = auction.Id;
            Item = new ItemInstance(auction.Item);
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

    struct AuctionWonNotification
    {
        public AuctionHouseId AuctionHouseId;
        public int AuctionId;
        public ObjectGuid Bidder;
        public ItemInstance Item;

        public void Initialize(AuctionHouseId auctionHouseId, AuctionPosting auction, Item item)
        {
            AuctionHouseId = auctionHouseId;
            AuctionId = auction.Id;
            Item = new ItemInstance(item);
            Bidder = auction.Bidder;
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32((int)AuctionHouseId);
            data.WriteInt32(AuctionId);
            data.WritePackedGuid(Bidder);
            Item.Write(data);
        }
    }

    struct AuctionBidderNotification
    {
        public AuctionHouseId AuctionHouseId;
        public int AuctionId;
        public ObjectGuid Bidder;
        public ItemInstance Item;

        public void Initialize(AuctionHouseId auctionHouseId, AuctionPosting auction, Item item)
        {
            AuctionHouseId = auctionHouseId;
            AuctionId = auction.Id;
            Item = new ItemInstance(item);
            Bidder = auction.Bidder;
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32((int)AuctionHouseId);
            data.WriteInt32(AuctionId);
            data.WritePackedGuid(Bidder);
            Item.Write(data);
        }
    }
}
