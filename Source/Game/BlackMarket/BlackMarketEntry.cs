// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Framework.Database;
using Game.Entities;
using Game.Networking.Packets;
using System.Collections.Generic;

namespace Game.BlackMarket
{
    public class BlackMarketTemplate
    {
        public bool LoadFromDB(SQLFields fields)
        {
            MarketID = fields.Read<int>(0);
            SellerNPC = fields.Read<int>(1);
            Item = new ItemInstance();
            Item.ItemID = fields.Read<int>(2);
            Quantity = fields.Read<int>(3);
            MinBid = fields.Read<long>(4);
            Duration = fields.Read<long>(5);
            Chance = fields.Read<float>(6);

            var bonusListIDsTok = new StringArray(fields.Read<string>(7), ' ');
            List<int> bonusListIDs = new();
            if (!bonusListIDsTok.IsEmpty())
            {
                foreach (string token in bonusListIDsTok)
                {
                    if (int.TryParse(token, out int id))
                        bonusListIDs.Add(id);
                }
            }

            if (!bonusListIDs.Empty())
            {
                Item.ItemBonus = new();
                Item.ItemBonus.BonusListIDs = bonusListIDs;
            }

            if (Global.ObjectMgr.GetCreatureTemplate(SellerNPC) == null)
            {
                Log.outError(LogFilter.Misc, 
                    $"Black market template {MarketID} " +
                    $"does not have a valid seller. (Entry: {SellerNPC})");
                return false;
            }

            if (Global.ObjectMgr.GetItemTemplate(Item.ItemID) == null)
            {
                Log.outError(LogFilter.Misc, 
                    $"Black market template {MarketID} " +
                    $"does not have a valid item. (Entry: {Item.ItemID})");
                return false;
            }

            return true;
        }

        public int MarketID;
        public int SellerNPC;
        public int Quantity;
        public long MinBid;
        public long Duration;
        public float Chance;
        public ItemInstance Item;
    }

    public class BlackMarketEntry
    {
        public void Initialize(int marketId, long duration)
        {
            _marketId = marketId;
            _secondsRemaining = duration;
        }

        public void Update(long newTimeOfUpdate)
        {
            _secondsRemaining = _secondsRemaining - (newTimeOfUpdate - Global.BlackMarketMgr.GetLastUpdate());
        }

        public BlackMarketTemplate GetTemplate()
        {
            return Global.BlackMarketMgr.GetTemplateByID(_marketId);
        }

        public int GetSecondsRemaining()
        {
            var secondsRemaining = _secondsRemaining - (GameTime.GetGameTime() - Global.BlackMarketMgr.GetLastUpdate());
            Cypher.Assert(secondsRemaining <= int.MaxValue);
            return (int)secondsRemaining;
        }

        long GetExpirationTime()
        {
            return GameTime.GetGameTime() + GetSecondsRemaining();
        }

        public bool IsCompleted()
        {
            return GetSecondsRemaining() <= 0;
        }

        public bool LoadFromDB(SQLFields fields)
        {
            _marketId = fields.Read<int>(0);

            // Invalid MarketID
            BlackMarketTemplate templ = Global.BlackMarketMgr.GetTemplateByID(_marketId);
            if (templ == null)
            {
                Log.outError(LogFilter.Misc, 
                    $"Black market auction {_marketId} does not have a valid id.");
                return false;
            }

            _currentBid = fields.Read<long>(1);
            _secondsRemaining = (fields.Read<long>(2) - Global.BlackMarketMgr.GetLastUpdate());
            _numBids = fields.Read<int>(3);
            _bidder = fields.Read<long>(4);

            // Either no bidder or existing player
            if (_bidder != 0 && Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(ObjectGuid.Create(HighGuid.Player, _bidder)) == 0) // Probably a better way to check if player exists
            {
                Log.outError(LogFilter.Misc, 
                    $"Black market auction {_marketId} " +
                    $"does not have a valid bidder (GUID: {_bidder}).");
                return false;
            }

            return true;
        }

        public void SaveToDB(SQLTransaction trans)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_BLACKMARKET_AUCTIONS);

            stmt.SetInt32(0, _marketId);
            stmt.SetInt64(1, _currentBid);
            stmt.SetInt64(2, GetExpirationTime());
            stmt.SetInt32(3, _numBids);
            stmt.SetInt64(4, _bidder);

            trans.Append(stmt);
        }

        public void DeleteFromDB(SQLTransaction trans)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_BLACKMARKET_AUCTIONS);
            stmt.SetInt32(0, _marketId);
            trans.Append(stmt);
        }

        public bool ValidateBid(long bid)
        {
            if (bid <= _currentBid)
                return false;

            if (bid < _currentBid + GetMinIncrement())
                return false;

            if (bid >= BlackMarketConst.MaxBid)
                return false;

            return true;
        }

        public void PlaceBid(long bid, Player player, SQLTransaction trans)   //Updated
        {
            if (bid < _currentBid)
                return;

            _currentBid = bid;
            ++_numBids;

            if (GetSecondsRemaining() < 30 * Time.Minute)
                _secondsRemaining += 30 * Time.Minute;

            _bidder = player.GetGUID().GetCounter();

            player.ModifyMoney(-bid);


            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_BLACKMARKET_AUCTIONS);

            stmt.SetInt64(0, _currentBid);
            stmt.SetInt64(1, GetExpirationTime());
            stmt.SetInt32(2, _numBids);
            stmt.SetInt64(3, _bidder);
            stmt.SetInt32(4, _marketId);

            trans.Append(stmt);

            Global.BlackMarketMgr.Update(true);
        }

        public string BuildAuctionMailSubject(BMAHMailAuctionAnswers response)
        {
            return $"{GetTemplate().Item.ItemID}:{0}:{response}:{GetMarketId()}:{GetTemplate().Quantity}";
        }

        public string BuildAuctionMailBody()
        {
            return $"{GetTemplate().SellerNPC}:{_currentBid}";
        }


        public int GetMarketId() { return _marketId; }

        public long GetCurrentBid() { return _currentBid; }
        void SetCurrentBid(long bid) { _currentBid = bid; }

        public int GetNumBids() { return _numBids; }
        void SetNumBids(int numBids) { _numBids = numBids; }

        public long GetBidder() { return _bidder; }
        void SetBidder(long bidder) { _bidder = bidder; }

        public long GetMinIncrement() { return (_currentBid / 20) - ((_currentBid / 20) % MoneyConstants.Gold); } //5% increase every bid (has to be round gold value)

        public void MailSent() { _mailSent = true; } // Set when mail has been sent
        public bool GetMailSent() { return _mailSent; }

        int _marketId;
        long _currentBid;
        int _numBids;
        long _bidder;
        long _secondsRemaining;
        bool _mailSent;
    }
}
