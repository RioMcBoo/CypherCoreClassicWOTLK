// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;

namespace Framework.Constants
{
    public enum AuctionResult
    {
        Ok = 0,
        Inventory = 1,
        DatabaseError = 2,
        NotEnoughMoney = 3,
        ItemNotFound = 4,
        HigherBid = 5,
        BidIncrement = 7,
        BidOwn = 10,
        RestrictedAccountTrial = 13,
        HasRestriction = 17,
        AuctionHouseBusy = 18,
        AuctionHouseUnavailable = 19,
        CommodityPurchaseFailed = 21,
        ItemHasQuote = 23
    }

    public enum AuctionCommand
    {
        SellItem = 0,
        Cancel = 1,
        PlaceBid = 2
    }

    public enum AuctionMailType
    {
        Outbid = 0,
        Won = 1,
        Sold = 2,
        Expired = 3,
        Removed = 4,
        Cancelled = 5,
        Invoice = 6
    }

    public enum AuctionHouseSortOrder
    {
        RequiredLevel = 0,
        Rarity = 1,
        TimeLeft = 3,
        /// <summary>It's not certain</summary>
        ItemLevel = 5,
        Unk_6 = 6,
        Seller = 7,
        CurrentBidTotal = 8,
        /// <summary>It's not certain</summary>
        Name = 9,
        BuyOutPerUnit = 12,
        CurrentBidPerUnit = 13,
        BuyOutTotal = 14,
    }

    public enum AuctionHouseSortDirection : byte
    {
        Ascending = 0,
        Descending = 1,
    }

    public enum AuctionHouseBrowseMode
    {
        Search = 0,
        SpecificKeys = 1
    }

    public enum AuctionHouseListType
    {
        Commodities = 1,
        Items = 2
    }

    public enum AuctionPostingServerFlag
    {
        None = 0x0,
        GmLogBuyer = 0x1  // write transaction to gm log file for buyer (optimization flag - avoids querying database for offline player permissions)
    }

    public enum AuctionHouseId
    {
        Alliance = 2,
        Horde = 6,
        Neutral = 7,
    }
}
