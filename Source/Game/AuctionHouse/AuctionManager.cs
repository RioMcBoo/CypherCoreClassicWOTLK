// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using Game.Mails;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public readonly struct AuctionHouseFilters : IEquatable<AuctionHouseFilters>
    {
        public readonly bool UsableOnly;
        public readonly bool ExactMatch;
        public readonly ItemQualityMask QualityMask = ItemQualityMask.AllPermanent;
        public readonly AuctionSearchClassFilters ItemFilters = new(false);
        public readonly int MinLevel;
        public readonly int MaxLevel;
        public readonly string Name = string.Empty;

        public static AuctionHouseFilters Empty => new();

        public AuctionHouseFilters() { }

        public AuctionHouseFilters(AuctionListItems packet)
        {
            UsableOnly = packet.UsableOnly;
            ExactMatch = packet.ExactMatch;

            if (packet.Quality != ItemQuality.None)
                QualityMask = packet.Quality.GetItemQualityMask();

            if (!packet.ItemClassFilters.Empty())
            {
                ItemFilters = new(true);

                foreach (var classFilter in packet.ItemClassFilters)
                {
                    ItemFilters.Classes |= classFilter.ItemClass.GetItemClassMask();

                    if (!classFilter.SubClassFilters.Empty())
                    {
                        foreach (var subClassFilter in classFilter.SubClassFilters)
                        {
                            if (classFilter.ItemClass < ItemClass.Max)
                            {
                                ItemFilters.SubClasses[(int)classFilter.ItemClass].SubclassMask |= subClassFilter.ItemSubclass.GetItemSubClassMask();

                                if (subClassFilter.ItemSubclass < ItemConst.MaxItemSubclassTotal)
                                    ItemFilters.SubClasses[(int)classFilter.ItemClass].InvTypes[subClassFilter.ItemSubclass] = subClassFilter.InvTypeMask;
                            }
                        }
                    }
                }
            }
            else
            {
                ItemFilters = new(false);
            }

            MinLevel = packet.MinLevel;
            MaxLevel = packet.MaxLevel;
            if (packet.Name != null)
                Name = packet.Name;
            else
                Name = string.Empty;
        }

        public bool Equals(AuctionHouseFilters other)
        {
            if (!UsableOnly.Equals(other.UsableOnly))
                return false;

            if (!ExactMatch.Equals(other.ExactMatch))
                return false;

            if (!QualityMask.Equals(other.QualityMask))
                return false;

            if (!ItemFilters.Equals(other.ItemFilters))
                return false;

            if (!MinLevel.Equals(other.MinLevel))
                return false;

            if (!MaxLevel.Equals(other.MaxLevel))
                return false;

            if (!Name.Equals(other.Name))
                return false;

            return true;
        }
    }

    public class AuctionManager : Singleton<AuctionManager>
    {
        AuctionHouseObject mAllianceAuctions;
        AuctionHouseObject mHordeAuctions;
        AuctionHouseObject mNeutralAuctions;

        Dictionary<ObjectGuid, Item> _itemsByGuid = new();
        Dictionary<ObjectGuid, PlayerPendingAuctions> _pendingAuctionsByPlayer = new();
        Dictionary<ObjectGuid, PlayerThrottleObject> _playerThrottleObjects = new();

        int _replicateIdGenerator;

        ServerTime _playerThrottleObjectsCleanupTime;

        AuctionManager()
        {
            mHordeAuctions = new AuctionHouseObject(AuctionHouseId.Horde);
            mAllianceAuctions = new AuctionHouseObject(AuctionHouseId.Alliance);
            mNeutralAuctions = new AuctionHouseObject(AuctionHouseId.Neutral);
        }               

        public Item GetAItem(ObjectGuid itemGuid)
        {
            return _itemsByGuid.LookupByKey(itemGuid);
        }        

        public string BuildItemAuctionMailSubject(AuctionMailType type, AuctionPosting auction)
        {
            return BuildAuctionMailSubject(
                auction.Item.GetEntry(),
                auction.Item.GetItemRandomPropertyId(),
                type, 
                auction.Id, 
                auction.Item.GetCount(),
                auction.Item.GetModifier(ItemModifier.BattlePetSpeciesId), 
                auction.Item.GetContext());
        }

        public string BuildAuctionMailSubject(int itemId, int randomPropertyId, AuctionMailType type, int auctionId, int itemCount, int battlePetSpeciesId, ItemContext context)
        {
            return $"{itemId}:{randomPropertyId}:{(int)type}:{auctionId}:{itemCount}:{battlePetSpeciesId}:0:0:0:0:0:{(int)context}";
        }

        public string BuildAuctionWonMailBody(ObjectGuid guid, long bid, long buyout)
        {
            return $"{ObjectGuidInfo.Format(guid)}:{bid}:{buyout}:0";
        }

        public string BuildAuctionSoldMailBody(ObjectGuid guid, long bid, long buyout, long deposit, long consignment)
        {
            return $"{ObjectGuidInfo.Format(guid)}:{bid}:{buyout}:{deposit}:{consignment}:0";
        }

        public string BuildAuctionExpiredMailBody(long bid, long buyout, long deposit, long consignment)
        {
            return $"{ObjectGuidInfo.Format(default)}:{bid}:{buyout}:{deposit}:0:0";
        }

        public string BuildAuctionInvoiceMailBody(ObjectGuid guid, long bid, long buyout, long deposit, long consignment, TimeSpan moneyDelay, RealmTime eta)
        {
            // todo: estimate time doesn't show in the client properly (it is always 12:22), we need sniffs
            return $"{ObjectGuidInfo.Format(guid)}:{bid}:{buyout}:{deposit}:{consignment}:{(Seconds)moneyDelay}:{(WowTime)eta}:0";
        }

        public void LoadAuctions()
        {
            // need to clear in case we are reloading
            _itemsByGuid.Clear();

            // data needs to be at first place for Item.LoadFromDB                
            MultiMap<int, Item> itemsByAuction = new();
            MultiMap<int, ObjectGuid> biddersByAuction = new();

            // perfomance and quantity counters
            var count = 0;
            RelativeTime oldMSTime = Time.NowRelative;

            {
                SQLResult result = DB.Characters.Query(CharacterDatabase.GetPreparedStatement(CharStatements.SEL_AUCTION_ITEMS));
            
                if (result.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, "Loaded 0 auctions. DB table `auctionhouse` is empty.");
                    return;
                }                

                do
                {
                    var itemGuid = result.Read<long>(0);
                    var itemEntry = result.Read<int>(1);

                    ItemTemplate proto = Global.ObjectMgr.GetItemTemplate(itemEntry);
                    if (proto == null)
                    {
                        Log.outError(LogFilter.Misc, 
                            $"AuctionHouseMgr.LoadAuctionItems: " +
                            $"Unknown item (GUID: {itemGuid} item entry: #{itemEntry}) in auction, skipped.");
                        continue;
                    }

                    Item item = Item.NewItemOrBag(proto);
                    if (!item.LoadFromDB(itemGuid, ObjectGuid.Create(HighGuid.Player, result.Read<long>(46)), result.GetFields(), itemEntry))
                    {
                        item.Dispose();
                        continue;
                    }

                    var auctionId = result.Read<int>(47);
                    itemsByAuction.Add(auctionId, item);

                    ++count;
                } while (result.NextRow());

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} auction items in {Time.Diff(oldMSTime)} ms.");
            }            

            oldMSTime = Time.NowRelative;
            count = 0;

            {
                SQLResult result = DB.Characters.Query(CharacterDatabase.GetPreparedStatement(CharStatements.SEL_AUCTION_BIDDERS));
            
                if (!result.IsEmpty())
                {
                    do
                    {
                        biddersByAuction.Add(result.Read<int>(0), ObjectGuid.Create(HighGuid.Player, result.Read<long>(1)));

                    } while (result.NextRow());
                }

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} auction bidders in {Time.Diff(oldMSTime)} ms.");
            }

            oldMSTime = Time.NowRelative;
            count = 0;

            {
                SQLResult result = DB.Characters.Query(CharacterDatabase.GetPreparedStatement(CharStatements.SEL_AUCTIONS));

                if (!result.IsEmpty())
                {
                    SQLTransaction trans = new();
                    do
                    {
                        AuctionPosting auction = new();
                        auction.Id = result.Read<int>(0);

                        AuctionHouseId auctionHouseId = (AuctionHouseId)result.Read<int>(1);
                        var auctionHouse = GetAuctionsById(auctionHouseId);

                        if (auctionHouse == null)
                        {
                            Log.outError(LogFilter.Misc, $"Auction {auction.Id} has wrong auctionHouseId {auctionHouseId}");
                            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_AUCTION);
                            stmt.SetInt32(0, auction.Id);
                            trans.Append(stmt);
                            continue;
                        }

                        if (!itemsByAuction.ContainsKey(auction.Id))
                        {
                            Log.outError(LogFilter.Misc, $"Auction {auction.Id} has no items");
                            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_AUCTION);
                            stmt.SetInt32(0, auction.Id);
                            trans.Append(stmt);
                            continue;
                        }

                        auction.Item = itemsByAuction.Extract(auction.Id)[0];
                        auction.Owner = ObjectGuid.Create(HighGuid.Player, result.Read<long>(2));
                        auction.OwnerAccount = ObjectGuid.Create(HighGuid.WowAccount, Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(auction.Owner));
                        var bidder = result.Read<long>(3);
                        if (bidder != 0)
                            auction.Bidder = ObjectGuid.Create(HighGuid.Player, bidder);

                        auction.MinBid = result.Read<long>(4);
                        auction.BuyoutPrice = result.Read<long>(5);
                        auction.Deposit = result.Read<long>(6);
                        auction.BidAmount = result.Read<long>(7);
                        auction.StartTime = (ServerTime)(UnixTime64)result.Read<long>(8);
                        auction.EndTime = (ServerTime)(UnixTime64)result.Read<long>(9);
                        auction.ServerFlags = (AuctionPostingServerFlag)result.Read<byte>(10);
                        auction.BidderHistory = biddersByAuction.Extract(auction.Id).ToHashSet();

                        auctionHouse.AddAuction(null, auction);

                        ++count;
                    } while (result.NextRow());

                    DB.Characters.CommitTransaction(trans);
                }

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} auctions in {Time.Diff(oldMSTime)} ms.");
            }
        }

        public void AddAItem(Item item)
        {
            Cypher.Assert(item != null);
            Cypher.Assert(!_itemsByGuid.ContainsKey(item.GetGUID()));
            _itemsByGuid[item.GetGUID()] = item;
        }

        public bool RemoveAItem(ObjectGuid guid, bool deleteItem = false, SQLTransaction trans = null)
        {
            var item = _itemsByGuid.LookupByKey(guid);
            if (item == null)
                return false;

            if (deleteItem)
            {
                item.FSetState(ItemUpdateState.Removed);
                item.SaveToDB(trans);
            }

            _itemsByGuid.Remove(guid);
            return true;
        }

        public bool PendingAuctionAdd(Player player, AuctionPosting newAuction)
        {
            if (!_pendingAuctionsByPlayer.ContainsKey(player.GetGUID()))
                _pendingAuctionsByPlayer[player.GetGUID()] = new PlayerPendingAuctions();

            var pendingAuction = _pendingAuctionsByPlayer[player.GetGUID()];
            // Get deposit so far
            long totalDeposit = 0;
            foreach (var auction in pendingAuction.Auctions)
                totalDeposit += auction.Deposit;

            // Add this deposit
            totalDeposit += newAuction.Deposit;

            if (!player.HasEnoughMoney(totalDeposit))
                return false;

            pendingAuction.Auctions.Add(newAuction);
            return true;
        }

        public int PendingAuctionCount(Player player)
        {
            var itr = _pendingAuctionsByPlayer.LookupByKey(player.GetGUID());
            if (itr != null)
                return itr.Auctions.Count;

            return 0;
        }

        public void PendingAuctionProcess(Player player)
        {
            var playerPendingAuctions = _pendingAuctionsByPlayer.LookupByKey(player.GetGUID());
            if (playerPendingAuctions == null)
                return;

            long totaldeposit = 0;
            var auctionIndex = 0;

            for (; auctionIndex < playerPendingAuctions.Auctions.Count; ++auctionIndex)
            {
                var pendingAuction = playerPendingAuctions.Auctions[auctionIndex];
                if (!player.HasEnoughMoney(totaldeposit + pendingAuction.Deposit))
                    break;

                totaldeposit += pendingAuction.Deposit;
            }

            // expire auctions we cannot afford
            if (auctionIndex < playerPendingAuctions.Auctions.Count)
            {
                SQLTransaction trans = new();

                do
                {
                    AuctionPosting auction = playerPendingAuctions.Auctions[auctionIndex];                   
                    auction.EndTime = LoopTime.ServerTime;

                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_AUCTION_EXPIRATION);
                    stmt.SetInt32(0, LoopTime.UnixServerTime);
                    stmt.SetInt32(1, auction.Id);
                    trans.Append(stmt);
                    ++auctionIndex;
                } while (auctionIndex < playerPendingAuctions.Auctions.Count);

                DB.Characters.CommitTransaction(trans);
            }

            _pendingAuctionsByPlayer.Remove(player.GetGUID());
            player.ModifyMoney(-totaldeposit);
        }

        public void UpdatePendingAuctions()
        {
            foreach (var pair in _pendingAuctionsByPlayer)
            {
                ObjectGuid playerGUID = pair.Key;
                Player player = Global.ObjAccessor.FindConnectedPlayer(playerGUID);
                if (player != null)
                {
                    // Check if there were auctions since last update process if not
                    if (PendingAuctionCount(player) == pair.Value.LastAuctionsSize)
                        PendingAuctionProcess(player);
                    else
                        _pendingAuctionsByPlayer[playerGUID].LastAuctionsSize = PendingAuctionCount(player);
                }
                else
                {
                    // Expire any auctions that we couldn't get a deposit for
                    Log.outWarn(LogFilter.Auctionhouse, $"Player {playerGUID} was offline, unable to retrieve deposit!");

                    SQLTransaction trans = new();
                    foreach (var auction in pair.Value.Auctions)
                    {                        
                        auction.EndTime = LoopTime.ServerTime;

                        PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_AUCTION_EXPIRATION);
                        stmt.SetInt32(0, LoopTime.UnixServerTime);
                        stmt.SetInt32(1, auction.Id);
                        trans.Append(stmt);
                    }
                    DB.Characters.CommitTransaction(trans);
                    _pendingAuctionsByPlayer.Remove(playerGUID);
                }
            }
        }

        public void Update()
        {
            ServerTime now = LoopTime.ServerTime;

            mHordeAuctions.Update(now);
            mAllianceAuctions.Update(now);
            mNeutralAuctions.Update(now);

            if (now >= _playerThrottleObjectsCleanupTime)
            {
                foreach (var pair in _playerThrottleObjects.ToList())
                {
                    if (pair.Value.PeriodEnd < now)
                        _playerThrottleObjects.Remove(pair.Key);
                }

                _playerThrottleObjectsCleanupTime = now + (Hours)1;
            }
        }

        public int GenerateReplicationId()
        {
            return ++_replicateIdGenerator;
        }

        public AuctionThrottleResult CheckThrottle(Player player, bool addonTainted, AuctionCommand command = AuctionCommand.SellItem)
        {
            ServerTime now = LoopTime.ServerTime;

            if (!_playerThrottleObjects.ContainsKey(player.GetGUID()))
                _playerThrottleObjects[player.GetGUID()] = new PlayerThrottleObject();

            var throttleObject = _playerThrottleObjects[player.GetGUID()];
            if (now > throttleObject.PeriodEnd)
            {
                throttleObject.PeriodEnd = now + (Minutes)1;
                throttleObject.QueriesRemaining = 100;
            }

            if (throttleObject.QueriesRemaining == 0)
            {
                player.GetSession().SendAuctionCommandResult(0, command, AuctionResult.AuctionHouseBusy, throttleObject.PeriodEnd - now);
                return new AuctionThrottleResult(TimeSpan.Zero, true);
            }

            if ((--throttleObject.QueriesRemaining) == 0)
                return new AuctionThrottleResult(throttleObject.PeriodEnd - now, false);
            else
                return new AuctionThrottleResult(WorldConfig.Values[addonTainted ? WorldCfg.AuctionTaintedSearchDelay : WorldCfg.AuctionSearchDelay].TimeSpan, false);
        }

        public AuctionHouseObject GetAuctionsById(AuctionHouseId auctionHouseId)
        {
            switch (auctionHouseId)
            {
                case AuctionHouseId.Alliance:
                    return mAllianceAuctions;
                case AuctionHouseId.Horde:
                    return mHordeAuctions;
                default:
                    return mNeutralAuctions;
            }
        }

        public AuctionHouseObject GetAuctionsMap(int factionTemplateId)
        {
            if (WorldConfig.Values[WorldCfg.AllowTwoSideInteractionAuction].Bool)
                return mNeutralAuctions;

            // teams have linked auction houses
            FactionTemplateRecord uEntry = CliDB.FactionTemplateStorage.LookupByKey(factionTemplateId);
            if (uEntry == null)
                return mNeutralAuctions;
            else if (uEntry.FactionGroup.HasAnyFlag((byte)FactionMasks.Alliance))
                return mAllianceAuctions;
            else if (uEntry.FactionGroup.HasAnyFlag((byte)FactionMasks.Horde))
                return mHordeAuctions;
            else
                return mNeutralAuctions;
        }

        public AuctionHouseRecord GetAuctionHouseEntry(int factionTemplateId)
        {
            AuctionHouseId houseId = AuctionHouseId.Neutral;
            if (!WorldConfig.Values[WorldCfg.AllowTwoSideInteractionAuction].Bool)
            {
                // FIXME: found way for proper auctionhouse selection by another way
                // AuctionHouse.dbc have faction field with _player_ factions associated with auction house races.
                // but no easy way convert creature faction to player race faction for specific city

                switch (factionTemplateId)
                {
                    case 120:   // Booty bay, Blackwater Auction House
                    case 474:   // Gadgetzan, Blackwater Auction House
                    case 855:   // Everlook, Blackwater Auction House
                        houseId = AuctionHouseId.Neutral;
                        break;
                    default:
                    {
                        FactionTemplateRecord uEntry = CliDB.FactionTemplateStorage.LookupByKey(factionTemplateId);
                        if (uEntry == null)
                            houseId = AuctionHouseId.Neutral; // Auction House
                        else if (uEntry.FactionGroup.HasAnyFlag((byte)FactionMasks.Alliance))
                            houseId = AuctionHouseId.Alliance; // Alliance Auction House
                        else if (uEntry.FactionGroup.HasAnyFlag((byte)FactionMasks.Horde))
                            houseId = AuctionHouseId.Horde; // Horde Auction House
                        else
                            houseId = AuctionHouseId.Neutral; // Auction House
                        break;
                    }
                }
            }

            return CliDB.AuctionHouseStorage.LookupByKey(houseId);
        }

        class PlayerPendingAuctions
        {
            public List<AuctionPosting> Auctions = new();
            public int LastAuctionsSize;
        }

        class PlayerThrottleObject
        {
            public ServerTime PeriodEnd;
            public byte QueriesRemaining = 100;
        }
    }

    public class AuctionHouseObject
    {
        BinarySortedList<AuctionPosting> _itemsByAuctionId = new(BinarySortedListOptions.DisallowDuplicates); // default is sorted by auctionId
        BinarySortedList<AuctionPosting> _removedAuctionsCache = new(comparer: new AuctionPosting.RemovedAuctionSorter());

        MultiMap<ObjectGuid, AuctionPosting> _playerOwnedAuctions = new();
        MultiMap<ObjectGuid, AuctionPosting> _playerBidderAuctions = new();

        // Map of throttled players for GetAll, and throttle expiry time
        // Stored here, rather than player object to maintain persistence after logout
        Dictionary<ObjectGuid, PlayerReplicateThrottleData> _replicateThrottleMap = new();

        Dictionary<ObjectGuid, AuctionSearchSession> _playerLastSearchSession = new();       

        AuctionHouseRecord _auctionHouse;

        static readonly TimeSpan SearchSessionTime = (Minutes)1;
        static readonly TimeSpan MaximalSearchSessionTime = (Minutes)30;

        public AuctionPosting GetAuction(int auctionId)
        {
            int index = _itemsByAuctionId.FindRandomIndex(AuctionPosting.CreateKey(auctionId));
            if (index > -1)
                return _itemsByAuctionId[index];
            else
                return null;
        }

        public void AddAuction(SQLTransaction trans, AuctionPosting auction)
        {
            if (trans != null)
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_AUCTION);
                stmt.SetInt32(0, auction.Id);
                stmt.SetInt32(1, (int)_auctionHouse.Id);
                stmt.SetInt64(2, auction.Owner.GetCounter());
                stmt.SetInt64(3, ObjectGuid.Empty.GetCounter());
                stmt.SetInt64(4, auction.MinBid);
                stmt.SetInt64(5, auction.BuyoutPrice);
                stmt.SetInt64(6, auction.Deposit);
                stmt.SetInt64(7, auction.BidAmount);
                stmt.SetInt64(8, (UnixTime64)auction.StartTime);
                stmt.SetInt64(9, (UnixTime64)auction.EndTime);
                stmt.SetUInt8(10, (byte)auction.ServerFlags);
                trans.Append(stmt);

                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_AUCTION_ITEMS);
                stmt.SetInt32(0, auction.Id);
                stmt.SetInt64(1, auction.Item.GetGUID().GetCounter());
                trans.Append(stmt);
            }

            Global.AuctionHouseMgr.AddAItem(auction.Item);

            _playerOwnedAuctions.Add(auction.Owner, auction);
            foreach (ObjectGuid bidder in auction.BidderHistory)
                _playerBidderAuctions.Add(bidder, auction);

            _itemsByAuctionId.Add(auction);

            Global.ScriptMgr.OnAuctionAdd(this, auction);
        }

        public void UpdateSearchSession(Player player, int auctionId)
        {
            if (_playerLastSearchSession.TryGetValue(player.GetGUID(), out var session))
            {
                var auction = _removedAuctionsCache.FindLast(i => i.Id == auctionId);
                if (auction != null)
                {
                    session.History.Remove(auction);
                }
            }
        }

        public void RemoveAuction(SQLTransaction trans, AuctionPosting auction, AuctionPosting auctionPosting = null)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_AUCTION);
            stmt.SetInt32(0, auction.Id);
            trans.Append(stmt);

            Global.AuctionHouseMgr.RemoveAItem(auction.Item.GetGUID());

            Global.ScriptMgr.OnAuctionRemove(this, auction);

            _playerOwnedAuctions.Remove(auction.Owner, auction);
            foreach (ObjectGuid bidder in auction.BidderHistory)
                _playerBidderAuctions.Remove(bidder, auction);

            _itemsByAuctionId.Remove(auction);

            // Save cache for searching session updates
            auction.EndTime = LoopTime.ServerTime;
            _removedAuctionsCache.Add(auction);
        }

        public void Update(ServerTime now)
        {
            ///- Handle expired auctions

            // Clear expired throttled players
            foreach (var key in _replicateThrottleMap.Keys.ToList())
            {
                if (_replicateThrottleMap[key].NextAllowedReplication <= now)
                    _replicateThrottleMap.Remove(key);
            }

            if (_itemsByAuctionId.Empty())
                return;

            SQLTransaction trans = new();

            ///- filter auctions expired on next update
            var filteredList = _itemsByAuctionId.Where(x => !(x.EndTime > now + (Minutes)1));
            List<AuctionPosting> removeList = new();

            foreach (var auction in filteredList)
            {
                ///- Either cancel the auction if there was no bidder
                if (auction.Bidder.IsEmpty())
                {
                    SendAuctionExpired(auction, trans);
                    Global.ScriptMgr.OnAuctionExpire(this, auction);

                    removeList.Add(auction);
                }
                ///- Or perform the transaction
                else
                {
                    // Copy data before freeing AuctionPosting in auctionHouse->RemoveAuction
                    // Because auctionHouse->SendAuctionWon can unload items if bidder is offline
                    // we need to RemoveAuction before sending mails
                    AuctionPosting copy = auction;
                    removeList.Add(auction);

                    //we should send an "item sold" message if the seller is online
                    //we send the item to the winner
                    //we send the money to the seller
                    SendAuctionSold(copy, null, trans);
                    SendAuctionWon(copy, null, trans);
                    Global.ScriptMgr.OnAuctionSuccessful(this, auction);
                }
            }

            foreach (var auction in removeList)
            {
                RemoveAuction(trans, auction);
            }

            // Remove expired search sessions
            foreach (var pair in _playerLastSearchSession.ToList())
            {
                if (pair.Value.LastBrowseTime < now - SearchSessionTime)
                    _playerLastSearchSession.Remove(pair.Key);

                else if (pair.Value.LastBrowseTime < now - MaximalSearchSessionTime)
                    _playerLastSearchSession.Remove(pair.Key);
            }

            // Remove expired cache
            while (_removedAuctionsCache.Count > 0)
            {
                AuctionPosting auction = _removedAuctionsCache[0];
                if (auction.EndTime > now - MaximalSearchSessionTime - SearchSessionTime)
                    break;

                _removedAuctionsCache.Remove(auction);
            }

            // Run DB changes
            DB.Characters.CommitTransaction(trans);
        }

        static IReadOnlyCollection<AuctionPosting> EmptyPage = new List<AuctionPosting>(0);

        bool TryGetSavedSearchSessionData(Player player, out IEnumerable<AuctionPosting> result, out int totalCount, int offset, ServerTime now)
        {
            result = EmptyPage;
            totalCount = 0;

            if (offset > 0)
            {
                if (_playerLastSearchSession.TryGetValue(player.GetGUID(), out var session))
                {
                    session.LastBrowseTime = now;

                    AuctionPaginator paginator = new AuctionPaginator(session.History, session.Limit);
                    result = paginator.GetPage(offset);
                    totalCount = paginator.GetTotalCount();

                    return true;
                }
            }

            return false;
        }

        void SaveSearchSession(Player player, BinarySortedList<AuctionPosting> searchResult, int limit, ServerTime now)
        {
            _playerLastSearchSession[player.GetGUID()] = new AuctionSearchSession()
            {
                LastBrowseTime = now,
                Limit = limit,
                History = searchResult,
            };
        }

        public void BuildListAuctionItems(AuctionListItemsResult auctionItemsResult, Player player, AuctionHouseFilters filters,
            byte[] knownPetBits, int knownPetBitsCount, byte maxKnownPetLevel, int offset, AuctionSortDef[] sorts, int sortCount)
        {
            ServerTime now = LoopTime.ServerTime;

            /*List<uint> knownAppearanceIds = new();
            BitArray knownPetSpecies = new(knownPetBits);
            // prepare uncollected filter for more efficient searches
            if (filters.HasFlag(AuctionHouseFilterMask.UncollectedOnly))
            {
                knownAppearanceIds = player.GetSession().GetCollectionMgr().GetAppearanceIds();
                //todo fix me
                //if (knownPetSpecies.Length < CliDB.BattlePetSpeciesStorage.GetNumRows())
                //knownPetSpecies.resize(CliDB.BattlePetSpeciesStorage.GetNumRows());
            }*/

            IEnumerable<AuctionPosting> result = EmptyPage;
            int totalCount = 0;
            int limit = -1;

            if (filters.Equals(AuctionHouseFilters.Empty))
            {
                // If filters are not used, we will ignore sorting, because it makes no sense without applying a filter.
                // And then we can directly pass paginated at least all elements at once without caching the search.
                AuctionPaginator paginator = new AuctionPaginator(_itemsByAuctionId);
                result = paginator.GetPage(offset);
                totalCount = paginator.GetTotalCount();
            }
            else if (!TryGetSavedSearchSessionData(player, out result, out totalCount, offset, now))
            {
                var sorter = new AuctionPosting.AuctionSorter(player.GetSession().GetSessionDbcLocale(), sorts, sortCount);
                BinarySortedList<AuctionPosting> searchResult = new(BinarySortedListOptions.DisallowDuplicates, sorter);

                ItemTemplate itemChain = _itemsByAuctionId.First().Item.GetTemplate();

                foreach (var auction in _itemsByAuctionId)
                {
                    ItemTemplate itemTemplate = auction.Item.GetTemplate();
                    if (itemTemplate != itemChain)
                        itemChain = null;

                    // If only one type of item is found with the current filters,
                    // then we should not limit the maximum number since there is no way to narrow the search
                    if (itemChain == null && searchResult.Count >= SharedConst.AuctionBrowseItemsMax)
                    {
                        limit = SharedConst.AuctionBrowseItemsMax;
                        break;
                    }

                    ItemClass itemClass = itemTemplate.GetClass();
                    ItemSubClass itemSubClass = itemTemplate.GetSubClass();
                    InventoryType inventoryType = itemTemplate.GetInventoryType();
                    int requiredLevel = itemTemplate.GetBaseRequiredLevel();

                    if (!filters.Name.IsEmpty())
                    {
                        if (filters.ExactMatch)
                        {
                            if (!itemTemplate.GetName(player.GetSession().GetSessionDbcLocale()).Equals(filters.Name, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        else
                        {
                            if (!itemTemplate.GetName(player.GetSession().GetSessionDbcLocale()).ToUpper().Contains(filters.Name, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                    }

                    if (filters.MinLevel != 0 && requiredLevel < filters.MinLevel)
                        continue;

                    if (filters.MaxLevel != 0 && requiredLevel > filters.MaxLevel)
                        continue;

                    if (!filters.QualityMask.HasQuality(itemTemplate.GetQuality()))
                        continue;

                    if (!filters.ItemFilters.Classes.HasItemClass(itemClass))
                        continue;

                    if (!filters.ItemFilters.SubClasses[(int)itemClass].SubclassMask.HasItemSubClass(itemSubClass))
                        continue;

                    if (!filters.ItemFilters.SubClasses[(int)itemClass].InvTypes[itemSubClass].HasInventoryType(inventoryType))
                        continue;


                    //if (filters.HasFlag(AuctionHouseFilterMask.UncollectedOnly))
                    //{
                    //    // appearances - by ItemAppearanceId, not ItemModifiedAppearanceId
                    //    if (bucketData.InventoryType != (byte)InventoryType.NonEquip && bucketData.InventoryType != (byte)InventoryType.Bag)
                    //    {
                    //        bool hasAll = true;
                    //        foreach (var bucketAppearance in bucketData.ItemModifiedAppearanceId)
                    //        {
                    //            var itemModifiedAppearance = CliDB.ItemModifiedAppearanceStorage.LookupByKey(bucketAppearance.Item1);
                    //            if (itemModifiedAppearance != null)
                    //            {
                    //                if (!knownAppearanceIds.Contains((uint)itemModifiedAppearance.ItemAppearanceID))
                    //                {
                    //                    hasAll = false;
                    //                    break;
                    //                }
                    //            }
                    //        }

                    //        if (hasAll)
                    //            continue;
                    //    }
                    //    // caged pets
                    //    else if (bucket.Key.BattlePetSpeciesId != 0)
                    //    {
                    //        if (knownPetSpecies.Get(bucket.Key.BattlePetSpeciesId))
                    //            continue;
                    //    }
                    //    // toys
                    //    else if (Global.DB2Mgr.IsToyItem(bucket.Key.ItemId))
                    //    {
                    //        if (player.GetSession().GetCollectionMgr().HasToy(bucket.Key.ItemId))
                    //            continue;
                    //    }
                    //    // mounts
                    //    // recipes
                    //    // pet items
                    //    else if (bucketData.ItemClass == (int)ItemClass.Consumable || bucketData.ItemClass == (int)ItemClass.Recipe || bucketData.ItemClass == (int)ItemClass.Miscellaneous)
                    //    {
                    //        ItemTemplate itemTemplate = Global.ObjectMgr.GetItemTemplate(bucket.Key.ItemId);
                    //        if (itemTemplate.Effects.Count >= 2 && (itemTemplate.Effects[0].SpellID == 483 || itemTemplate.Effects[0].SpellID == 55884))
                    //        {
                    //            if (player.HasSpell(itemTemplate.Effects[1].SpellID))
                    //                continue;

                    //            var battlePetSpecies = BattlePetMgr.GetBattlePetSpeciesBySpell(itemTemplate.Effects[1].SpellID);
                    //            if (battlePetSpecies != null)
                    //                if (knownPetSpecies.Get(battlePetSpecies.Id))
                    //                    continue;
                    //        }
                    //    }
                    //}

                    if (filters.UsableOnly)
                    {
                        if (player.CanUseItem(auction.Item) != InventoryResult.Ok)
                            continue;
                    }

                    // TODO: this one needs to access loot history to know highest item level for every inventory Type
                    //if (filters.HasFlag(AuctionHouseFilterMask.UpgradesOnly))
                    //{
                    //}

                    searchResult.Add(auction);
                }

                AuctionPaginator paginator = new AuctionPaginator(searchResult, limit);
                result = paginator.GetPage(offset);                
                totalCount = paginator.GetTotalCount();

                SaveSearchSession(player, searchResult, limit, now);
            }

            foreach (var resultAuction in result)
            {
                AuctionItem auctionItem = new();
                resultAuction.BuildAuctionItem(auctionItem, false, resultAuction.OwnerAccount != player.GetSession().GetAccountGUID(), resultAuction.Bidder.IsEmpty());
                auctionItemsResult.Items.Add(auctionItem);
            }

            auctionItemsResult.OnlyUsable = filters.UsableOnly;
            auctionItemsResult.TotalCount = totalCount;
        }

        public void AddBidder(ObjectGuid bidder, AuctionPosting auction)
        {
            if (!_playerBidderAuctions.Contains(bidder, auction))
                _playerBidderAuctions.Add(bidder, auction);
        }

        public void BuildListBidderItems(AuctionListBidderItemsResult listBidderItemsResult, Player player, int offset)
        {
            var currentTime = LoopTime.ServerTime;

            // always full list
            foreach (var auction in _playerBidderAuctions[player.GetGUID()])
            {
                AuctionItem auctionItem = new();
                auction.BuildAuctionItem(auctionItem, true, true, false);
                listBidderItemsResult.Items.Add(auctionItem);
            }
        }

        public void BuildListOwnerItems(AuctionListOwnerItemsResult listOwnerItemsResult, Player player, int offset)
        {
            var currentTime = LoopTime.ServerTime;
            // always full list
            foreach (var auction in _playerOwnedAuctions[player.GetGUID()])
            {
                AuctionItem auctionItem = new();
                auction.BuildAuctionItem(auctionItem, true, false, false);
                listOwnerItemsResult.Items.Add(auctionItem);
            }
        }

        public void BuildReplicate(AuctionReplicateResponse replicateResponse, Player player, int global, int cursor, int tombstone, int count)
        {
            ServerTime curTime = LoopTime.ServerTime;

            var throttleData = _replicateThrottleMap.LookupByKey(player.GetGUID());
            if (throttleData == null)
            {
                throttleData = new PlayerReplicateThrottleData();
                throttleData.NextAllowedReplication = curTime + WorldConfig.Values[WorldCfg.AuctionReplicateDelay].TimeSpan;
                throttleData.Global = Global.AuctionHouseMgr.GenerateReplicationId();
            }
            else
            {
                if (throttleData.Global != global || throttleData.Cursor != cursor || throttleData.Tombstone != tombstone)
                    return;

                if (!throttleData.IsReplicationInProgress() && throttleData.NextAllowedReplication > curTime)
                    return;
            }

            if (_itemsByAuctionId.Empty() || count == 0)
                return;

            var keyIndex = _itemsByAuctionId.FindRandomIndex(GetAuction(cursor));
            foreach (var auction in _itemsByAuctionId.Skip(keyIndex))
            {
                AuctionItem auctionItem = new();
                auction.BuildAuctionItem(auctionItem, true, true, auction.Bidder.IsEmpty());
                replicateResponse.Items.Add(auctionItem);
                if (--count == 0)
                    break;
            }

            replicateResponse.ChangeNumberGlobal = throttleData.Global;
            replicateResponse.ChangeNumberCursor = throttleData.Cursor = !replicateResponse.Items.Empty() ? replicateResponse.Items.Last().AuctionID : 0;
            replicateResponse.ChangeNumberTombstone = throttleData.Tombstone = count == 0 ? _itemsByAuctionId.First().Id : 0;
            _replicateThrottleMap[player.GetGUID()] = throttleData;
        }

        public long CalculateAuctionHouseCut(long bidAmount)
        {
            return Math.Max((long)(MathFunctions.CalculatePct(bidAmount, _auctionHouse.ConsignmentRate) * WorldConfig.Values[WorldCfg.RateAuctionCut].Float), 0);
        }

        public long GetAuctionDeposit(ItemTemplate item, TimeSpan time, int quantity = 1)
        {
            double multiplier = 0.03 * _auctionHouse.ConsignmentRate;

            long sellPrice = item.GetSellPrice();
            double timeRate = time / SharedConst.MinAuctionTime;

            return (long)(sellPrice * quantity * timeRate * multiplier);
        }

        // this function notified old bidder that his bid is no longer highest
        public void SendAuctionOutbid(AuctionPosting auction, ObjectGuid newBidder, long newBidAmount, SQLTransaction trans)
        {
            Player oldBidder = Global.ObjAccessor.FindConnectedPlayer(auction.Bidder);

            // old bidder exist
            if ((oldBidder != null || Global.CharacterCacheStorage.HasCharacterCacheEntry(auction.Bidder)))// && !sAuctionBotConfig.IsBotChar(auction.Bidder))
            {
                if (oldBidder != null)
                {
                    AuctionOutbidNotification packet = new();
                    packet.BidAmount = newBidAmount;
                    packet.MinIncrement = AuctionPosting.CalculateMinIncrement(newBidAmount);
                    packet.Info.AuctionId = auction.Id;
                    packet.Info.Bidder = newBidder;
                    packet.Info.Item = new ItemInstance(auction.Item);
                    oldBidder.SendPacket(packet);
                }

                new MailDraft(Global.AuctionHouseMgr.BuildItemAuctionMailSubject(AuctionMailType.Outbid, auction), "")
                    .AddMoney(auction.BidAmount)
                    .SendMailTo(trans, new MailReceiver(oldBidder, auction.Bidder), new MailSender(_auctionHouse.Id), MailCheckFlags.Copied);
            }
        }

        public void SendAuctionWon(AuctionPosting auction, Player bidder, SQLTransaction trans)
        {
            int bidderAccId;
            if (bidder == null)
                bidder = Global.ObjAccessor.FindConnectedPlayer(auction.Bidder); // try lookup bidder when called from .Update

            // data for gm.log
            string bidderName = "";
            bool logGmTrade = auction.ServerFlags.HasFlag(AuctionPostingServerFlag.GmLogBuyer);

            if (bidder != null)
            {
                bidderAccId = bidder.GetSession().GetAccountId();
                bidderName = bidder.GetName();
            }
            else
            {
                bidderAccId = Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(auction.Bidder);

                if (logGmTrade && !Global.CharacterCacheStorage.GetCharacterNameByGuid(auction.Bidder, out bidderName))
                    bidderName = Global.ObjectMgr.GetCypherString(CypherStrings.Unknown);
            }

            if (logGmTrade)
            {
                if (!Global.CharacterCacheStorage.GetCharacterNameByGuid(auction.Owner, out string ownerName))
                    ownerName = Global.ObjectMgr.GetCypherString(CypherStrings.Unknown);

                int ownerAccId = Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(auction.Owner);

                Log.outCommand(bidderAccId,
                    $"GM {bidderName} (Account: {bidderAccId}) won item in auction: " +
                    $"{auction.Item.GetName(Global.WorldMgr.GetDefaultDbcLocale())} (Entry: {auction.Item.GetEntry()}" +
                    $" Count: {auction.Item.GetCount()}) and pay money: {auction.BidAmount}. " +
                    $"Original owner {ownerName} (Account: {ownerAccId})");
            }

            // receiver exist
            if ((bidder != null || bidderAccId != 0))// && !sAuctionBotConfig.IsBotChar(auction.Bidder))
            {
                MailDraft mail = new(Global.AuctionHouseMgr.BuildItemAuctionMailSubject(AuctionMailType.Won, auction),
                    Global.AuctionHouseMgr.BuildAuctionWonMailBody(auction.Owner, auction.BidAmount, auction.BuyoutPrice));

                // set owner to bidder (to prevent delete item with sender char deleting)
                // owner in `data` will set at mail receive and item extracting
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ITEM_OWNER);
                stmt.SetInt64(0, auction.Bidder.GetCounter());
                stmt.SetInt64(1, auction.Item.GetGUID().GetCounter());
                trans.Append(stmt);

                mail.AddItem(auction.Item);

                if (bidder != null)
                {
                    AuctionWonBidNotification packet = new();
                    packet.Info.Initialize(_auctionHouse.Id, auction, auction.Item);
                    bidder.SendPacket(packet);

                    // FIXME: for offline player need also
                    bidder.UpdateCriteria(CriteriaType.AuctionsWon, 1);
                }

                mail.SendMailTo(trans, new MailReceiver(bidder, auction.Bidder), new MailSender(_auctionHouse.Id), MailCheckFlags.Copied);
            }
            else
            {
                // bidder doesn't exist, delete the item
                Global.AuctionHouseMgr.RemoveAItem(auction.Item.GetGUID(), true, trans);
            }
        }

        //call this method to send mail to auction owner, when auction is successful, it does not clear ram
        public void SendAuctionSold(AuctionPosting auction, Player owner, SQLTransaction trans)
        {
            if (owner == null)
                owner = Global.ObjAccessor.FindConnectedPlayer(auction.Owner);

            // owner exist
            if ((owner != null || Global.CharacterCacheStorage.HasCharacterCacheEntry(auction.Owner)))// && !sAuctionBotConfig.IsBotChar(auction.Owner))
            {
                long auctionHouseCut = CalculateAuctionHouseCut(auction.BidAmount);
                long profit = auction.BidAmount + auction.Deposit - auctionHouseCut;

                //FIXME: what do if owner offline
                if (owner != null)
                {
                    owner.UpdateCriteria(CriteriaType.MoneyEarnedFromAuctions, profit);
                    owner.UpdateCriteria(CriteriaType.HighestAuctionSale, auction.BidAmount);
                    //send auction owner notification, bidder must be current!
                    owner.GetSession().SendAuctionClosedNotification(auction, WorldConfig.Values[WorldCfg.MailDeliveryDelay].TimeSpan, true);
                }

                new MailDraft(
                    Global.AuctionHouseMgr.BuildItemAuctionMailSubject(AuctionMailType.Sold, auction),
                    Global.AuctionHouseMgr.BuildAuctionSoldMailBody(auction.Bidder, auction.BidAmount, auction.BuyoutPrice, (uint)auction.Deposit, auctionHouseCut))
                    .AddMoney(profit)
                    .SendMailTo(trans,
                        new MailReceiver(owner, auction.Owner),
                        new MailSender(_auctionHouse.Id), MailCheckFlags.Copied, WorldConfig.Values[WorldCfg.MailDeliveryDelay].TimeSpan);
            }
        }

        public void SendAuctionExpired(AuctionPosting auction, SQLTransaction trans)
        {
            Player owner = Global.ObjAccessor.FindConnectedPlayer(auction.Owner);
            // owner exist
            if ((owner != null || Global.CharacterCacheStorage.HasCharacterCacheEntry(auction.Owner)))// && !sAuctionBotConfig.IsBotChar(auction.Owner))
            {
                if (owner != null)
                    owner.GetSession().SendAuctionClosedNotification(auction, TimeSpan.Zero, false);

                MailDraft mail = new(Global.AuctionHouseMgr.BuildItemAuctionMailSubject(AuctionMailType.Expired, auction), "");

                mail.AddItem(auction.Item);

                mail.SendMailTo(trans, new MailReceiver(owner, auction.Owner), new MailSender(_auctionHouse.Id), MailCheckFlags.Copied);
            }
            else
            {
                // owner doesn't exist, delete the item
                Global.AuctionHouseMgr.RemoveAItem(auction.Item.GetGUID(), true, trans);
            }
        }

        public void SendAuctionRemoved(AuctionPosting auction, Player owner, SQLTransaction trans)
        {
            MailDraft draft = new(Global.AuctionHouseMgr.BuildItemAuctionMailSubject(AuctionMailType.Cancelled, auction), "");
            draft.AddItem(auction.Item);
            draft.SendMailTo(trans, owner, new MailSender(_auctionHouse.Id), MailCheckFlags.Copied);
        }

        //this function sends mail, when auction is cancelled to old bidder
        public void SendAuctionCancelledToBidder(AuctionPosting auction, SQLTransaction trans)
        {
            Player bidder = Global.ObjAccessor.FindConnectedPlayer(auction.Bidder);

            // bidder exist
            if ((bidder != null || Global.CharacterCacheStorage.HasCharacterCacheEntry(auction.Bidder)))// && !sAuctionBotConfig.IsBotChar(auction.Bidder))
                new MailDraft(Global.AuctionHouseMgr.BuildItemAuctionMailSubject(AuctionMailType.Removed, auction), "")
                .AddMoney(auction.BidAmount)
                .SendMailTo(trans, new MailReceiver(bidder, auction.Bidder), new MailSender(_auctionHouse.Id), MailCheckFlags.Copied);
        }

        public void SendAuctionInvoice(AuctionPosting auction, Player owner, SQLTransaction trans)
        {
            if (owner == null)
                owner = Global.ObjAccessor.FindConnectedPlayer(auction.Owner);

            // owner exist (online or offline)
            if ((owner != null || Global.CharacterCacheStorage.HasCharacterCacheEntry(auction.Owner)))// && !sAuctionBotConfig.IsBotChar(auction.Owner))
            {
                RealmTime eta = LoopTime.RealmTime;
                eta += WorldConfig.Values[WorldCfg.MailDeliveryDelay].TimeSpan;

                if (owner != null)
                {
                    //eta += owner.GetSession().GetTimezoneOffset();
                }

                new MailDraft(Global.AuctionHouseMgr.BuildItemAuctionMailSubject(AuctionMailType.Invoice, auction),
                    Global.AuctionHouseMgr.BuildAuctionInvoiceMailBody(auction.Bidder, auction.BidAmount, auction.BuyoutPrice, (uint)auction.Deposit,
                        CalculateAuctionHouseCut(auction.BidAmount), WorldConfig.Values[WorldCfg.MailDeliveryDelay].TimeSpan, eta))
                    .SendMailTo(trans, new MailReceiver(owner, auction.Owner), new MailSender(_auctionHouse.Id), MailCheckFlags.Copied);
            }
        }

        class PlayerReplicateThrottleData
        {
            public int Global;
            public int Cursor;
            public int Tombstone;
            public ServerTime NextAllowedReplication = ServerTime.Zero;

            public bool IsReplicationInProgress() { return Cursor != Tombstone && Global != 0; }
        }

        class MailedItemsBatch
        {
            public Item[] Items = new Item[SharedConst.MaxMailItems];
            public long TotalPrice;
            public int Quantity;

            public int ItemsCount;

            public bool IsFull() { return ItemsCount >= Items.Length; }

            public void AddItem(Item item, long unitPrice)
            {
                Items[ItemsCount++] = item;
                Quantity += item.GetCount();
                TotalPrice += unitPrice * item.GetCount();
            }
        }

        public AuctionHouseObject(AuctionHouseId auctionHouseId)
        {
            _auctionHouse = CliDB.AuctionHouseStorage.LookupByKey(auctionHouseId);
        }

        public AuctionHouseId Id => _auctionHouse.Id;
        public int ConsignmentRate => _auctionHouse.ConsignmentRate;
        public int DepositRate => _auctionHouse.DepositRate;

        
    }

    public class AuctionSearchSession
    {
        public ServerTime LastBrowseTime;
        public int Limit;
        public BinarySortedList<AuctionPosting> History = new(BinarySortedListOptions.DisallowDuplicates);
    }

    public class AuctionPosting : IComparable<AuctionPosting>
    {
        public int Id;
        public ObjectGuid Owner;
        public ObjectGuid OwnerAccount;
        public ObjectGuid Bidder;
        public long MinBid;
        public long BuyoutPrice;
        public long Deposit;
        public long BidAmount;
        public ServerTime StartTime;
        public ServerTime EndTime;
        public AuctionPostingServerFlag ServerFlags;
        public Item Item;
        public HashSet<ObjectGuid> BidderHistory;

        public AuctionPosting()
        {
            Item = new Item();
            BidderHistory = new();
            StartTime = ServerTime.Zero;
            EndTime = ServerTime.Zero;
        }

        private AuctionPosting(int id)
        {
            Id = id;
        }

        /// <summary>
        /// Used for sorting and searching by Id
        /// </summary> 
        public static AuctionPosting CreateKey(int id)
        {
            return new(id);
        }

        /// <summary>
        /// Used for sorting and searching by Id
        /// </summary>       
        public int CompareTo(AuctionPosting other)
        {
            return Id.CompareTo(other.Id);
        }

        public void BuildAuctionItem(AuctionItem auctionItem, bool sendKey, bool censorServerInfo, bool censorBidInfo)
        {
            // SMSG_AUCTION_LIST_BIDDER_ITEMS_RESULT, SMSG_AUCTION_LIST_ITEMS_RESULT (if not commodity), SMSG_AUCTION_LIST_OWNER_ITEMS_RESULT, SMSG_AUCTION_REPLICATE_RESPONSE (if not commodity)
            //auctionItem.Item - here to unify comment

            // all (not optional<>)
            auctionItem.Count = Item.GetCount();
            auctionItem.Flags = Item.m_itemData.DynamicFlags;
            auctionItem.AuctionID = Id;
            auctionItem.Owner = Owner;
            auctionItem.Item = new ItemInstance(Item);
            auctionItem.Charges = new[] { Item.GetSpellCharges(0), Item.GetSpellCharges(1), Item.GetSpellCharges(2), Item.GetSpellCharges(3), Item.GetSpellCharges(4) }.Max();
            for (EnchantmentSlot enchantmentSlot = 0; enchantmentSlot < EnchantmentSlot.EnhancementMax; enchantmentSlot++)
            {
                int enchantId = Item.GetEnchantmentId(enchantmentSlot);
                if (enchantId == 0)
                    continue;

                auctionItem.Enchantments.Add(new ItemEnchantData(enchantId, Item.GetEnchantmentDuration(enchantmentSlot), Item.GetEnchantmentCharges(enchantmentSlot), (byte)enchantmentSlot));
            }

            for (byte i = 0; i < Item.m_itemData.Gems.Size(); ++i)
            {
                SocketedGem gemData = Item.m_itemData.Gems[i];
                if (gemData.ItemId != 0)
                {
                    ItemGemData gem = new();
                    gem.Slot = i;
                    gem.Item = new ItemInstance(gemData);
                    auctionItem.Gems.Add(gem);
                }
            }

            if (MinBid != 0)
                auctionItem.MinBid = MinBid;

            long minIncrement = CalculateMinIncrement();
            if (minIncrement != 0)
                auctionItem.MinIncrement = minIncrement;

            if (BuyoutPrice != 0)
                auctionItem.BuyoutPrice = BuyoutPrice;


            // all (not optional<>)
            auctionItem.DurationLeft = Time.Max(EndTime - LoopTime.ServerTime, TimeSpan.Zero);
            auctionItem.DeleteReason = 0;

            // SMSG_AUCTION_LIST_ITEMS_RESULT (only if owned)
            auctionItem.CensorServerSideInfo = censorServerInfo;
            auctionItem.ItemGuid = Item.GetGUID();
            auctionItem.OwnerAccountID = OwnerAccount;
            auctionItem.EndTime = EndTime;

            // SMSG_AUCTION_LIST_BIDDER_ITEMS_RESULT, SMSG_AUCTION_LIST_ITEMS_RESULT (if has bid), SMSG_AUCTION_LIST_OWNER_ITEMS_RESULT, SMSG_AUCTION_REPLICATE_RESPONSE (if has bid)
            auctionItem.CensorBidInfo = censorBidInfo;
            if (!Bidder.IsEmpty())
            {
                auctionItem.Bidder = Bidder;
                auctionItem.BidAmount = BidAmount;
            }

            // SMSG_AUCTION_LIST_BIDDER_ITEMS_RESULT, SMSG_AUCTION_LIST_OWNER_ITEMS_RESULT, SMSG_AUCTION_REPLICATE_RESPONSE (if commodity)
            if (sendKey)
                auctionItem.AuctionBucketKey = new(AuctionsBucketKey.ForItem(Item));

            // all
            if (!Item.m_itemData.Creator._value.IsEmpty())
                auctionItem.Creator = Item.m_itemData.Creator;
        }

        public static long CalculateNextBidAmount(long bidAmount)
        {
            return bidAmount + CalculateMinIncrement(bidAmount);
        }

        public static long CalculateMinIncrement(long bidAmount)
        {
            long increment = MathFunctions.CalculatePct(bidAmount, 5);
            return increment < 1 ? 1 : increment;
        }

        public long CalculateMinIncrement() { return CalculateMinIncrement(BidAmount); }

        public class RemovedAuctionSorter : IComparer<AuctionPosting>
        {
            public RemovedAuctionSorter() { }

            public int Compare(AuctionPosting left, AuctionPosting right)
            {
                return left.EndTime.CompareTo(right.EndTime);
            }
        }


        public class AuctionSorter : IComparer<AuctionPosting>
        {
            public AuctionSorter(Locale locale, AuctionSortDef[] sorts, int sortCount)
            {
                _locale = locale;
                _sorts = sorts;
                _sortCount = sortCount;
            }

            public int Compare(AuctionPosting left, AuctionPosting right)
            {
                for (var i = 0; i < _sortCount; ++i)
                {
                    long ordering = CompareColumns(_sorts[i].Order, left, right);
                    if (ordering != 0)
                    {
                        if (_sorts[i].Direction == AuctionHouseSortDirection.Descending)
                            ordering *= -1;

                        return (int)ordering;
                    }
                }

                // Auctions are processed in LIFO order
                if (left.StartTime != right.StartTime)
                    return left.StartTime.CompareTo(right.StartTime);

                return left.Id.CompareTo(right.Id);
            }

            long CompareColumns(AuctionHouseSortOrder column, AuctionPosting left, AuctionPosting right)
            {
                switch (column)
                {
                    case AuctionHouseSortOrder.Rarity:
                    {
                        return right.Item.GetQuality() - left.Item.GetQuality();
                    }
                    case AuctionHouseSortOrder.RequiredLevel:
                    {
                        return left.Item.GetRequiredLevel() - right.Item.GetRequiredLevel();
                    }
                    case AuctionHouseSortOrder.Seller:
                    {
                        if (!Global.CharacterCacheStorage.GetCharacterNameByGuid(left.Owner, out string leftName))
                            leftName = Global.ObjectMgr.GetCypherString(CypherStrings.Unknown);

                        if (!Global.CharacterCacheStorage.GetCharacterNameByGuid(right.Owner, out string rightName))
                            rightName = Global.ObjectMgr.GetCypherString(CypherStrings.Unknown);

                        return leftName.CompareTo(rightName);
                    }
                    case AuctionHouseSortOrder.TimeLeft:
                    {
                        return (left.EndTime - right.EndTime).Ticks;
                    }
                    case AuctionHouseSortOrder.ItemLevel:
                    {
                        return left.Item.GetTemplate().GetItemLevel() - right.Item.GetTemplate().GetItemLevel();
                    }
                    case AuctionHouseSortOrder.Unk_6:
                    {
                        return 0;
                    }
                    case AuctionHouseSortOrder.Name:
                    {
                        return left.Item.GetName(_locale).CompareTo(right.Item.GetName(_locale));
                    }
                    case AuctionHouseSortOrder.CurrentBidTotal:
                    {
                        double leftBid = (left.BidAmount != 0 ? left.BidAmount : left.MinBid);
                        double rightBid = (right.BidAmount != 0 ? right.BidAmount : right.MinBid);

                        return leftBid.CompareTo(rightBid);
                    }
                    case AuctionHouseSortOrder.CurrentBidPerUnit:
                    {
                        double leftBid = (left.BidAmount != 0 ? left.BidAmount : left.MinBid) / left.Item.GetCount();
                        double rightBid = (right.BidAmount != 0 ? right.BidAmount : right.MinBid) / right.Item.GetCount();

                        return leftBid.CompareTo(rightBid);
                    }
                    case AuctionHouseSortOrder.BuyOutTotal:
                    {
                        double leftPrice = (left.BuyoutPrice != 0 ? left.BuyoutPrice : (left.BidAmount != 0 ? left.BidAmount : left.MinBid));
                        double rightPrice = (right.BuyoutPrice != 0 ? right.BuyoutPrice : (right.BidAmount != 0 ? right.BidAmount : right.MinBid));

                        return leftPrice.CompareTo(rightPrice);
                    }
                    case AuctionHouseSortOrder.BuyOutPerUnit:
                    {
                        double leftPrice = (left.BuyoutPrice != 0 ? left.BuyoutPrice : (left.BidAmount != 0 ? left.BidAmount : left.MinBid)) / left.Item.GetCount();
                        double rightPrice = (right.BuyoutPrice != 0 ? right.BuyoutPrice : (right.BidAmount != 0 ? right.BidAmount : right.MinBid)) / right.Item.GetCount();

                        return leftPrice.CompareTo(rightPrice);
                    }
                    default:
                        break;
                }

                return 0;
            }

            Locale _locale;
            AuctionSortDef[] _sorts;
            int _sortCount;
        }
    }

    public class AuctionThrottleResult
    {
        public TimeSpan DelayUntilNext;
        public bool Throttled;

        public AuctionThrottleResult(TimeSpan delayUntilNext, bool throttled)
        {
            DelayUntilNext = delayUntilNext;
            Throttled = throttled;
        }
    }

    public class AuctionsBucketKey : IComparable<AuctionsBucketKey>
    {
        public int ItemId { get; set; }
        public ushort ItemLevel { get; set; }
        public ushort BattlePetSpeciesId { get; set; }
        public ushort SuffixItemNameDescriptionId { get; set; }

        public AuctionsBucketKey(int itemId, int itemLevel, int battlePetSpeciesId, int suffixItemNameDescriptionId)
        {
            ItemId = itemId;
            ItemLevel = (ushort)itemLevel;
            BattlePetSpeciesId = (ushort)battlePetSpeciesId;
            SuffixItemNameDescriptionId = (ushort)suffixItemNameDescriptionId;
        }

        public AuctionsBucketKey(AuctionBucketKey key)
        {
            ItemId = key.ItemID;
            ItemLevel = key.ItemLevel;
            BattlePetSpeciesId = (ushort)(key.BattlePetSpeciesID.HasValue ? key.BattlePetSpeciesID.Value : 0);
            SuffixItemNameDescriptionId = (ushort)(key.SuffixItemNameDescriptionID.HasValue ? key.SuffixItemNameDescriptionID.Value : 0);
        }

        public int CompareTo(AuctionsBucketKey other)
        {
            return ItemId.CompareTo(other.ItemId);
        }

        public static bool operator ==(AuctionsBucketKey right, AuctionsBucketKey left)
        {
            return right.ItemId == left.ItemId
                && right.ItemLevel == left.ItemLevel
                && right.BattlePetSpeciesId == left.BattlePetSpeciesId
                && right.SuffixItemNameDescriptionId == left.SuffixItemNameDescriptionId;
        }
        public static bool operator !=(AuctionsBucketKey right, AuctionsBucketKey left) { return !(right == left); }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return ItemId.GetHashCode() ^ ItemLevel.GetHashCode() ^ BattlePetSpeciesId.GetHashCode() ^ SuffixItemNameDescriptionId.GetHashCode();
        }

        public static AuctionsBucketKey ForItem(Item item)
        {
            ItemTemplate itemTemplate = item.GetTemplate();
            if (itemTemplate.GetMaxStackSize() == 1)
            {
                return new AuctionsBucketKey(
                    item.GetEntry(),
                    Item.GetItemLevel(itemTemplate, item.GetBonus(), 0, item.GetRequiredLevel(), 0, 0, 0, false),
                    item.GetModifier(ItemModifier.BattlePetSpeciesId),
                    item.GetBonus().Suffix);
            }
            else
                return ForCommodity(itemTemplate);
        }

        public static AuctionsBucketKey ForCommodity(ItemTemplate itemTemplate)
        {
            return new AuctionsBucketKey(itemTemplate.GetId(), (ushort)itemTemplate.GetItemLevel(), 0, 0);
        }
    }

    public class AuctionPaginator
    {
        readonly BinarySortedList<AuctionPosting> List;
        readonly int Limit;

        public AuctionPaginator(BinarySortedList<AuctionPosting> list, int limit = -1)
        {
            List = list;

            limit = limit < 0 ? list.Count : limit;
            limit = limit < list.Count ? limit : list.Count;

            Limit = limit;
        }

        public IEnumerable<AuctionPosting> GetPage(int offset, int count = SharedConst.AuctionListItemsMax)
        {
            int remains = Limit - offset;

            if (remains > 0)
            {
                if (remains > count)
                    remains = count;

                for (int i = offset; i < offset + remains; ++i)
                {
                    yield return List[i];
                }
            }
        }

        public int GetTotalCount()
        {
            return Limit;
        }
    }

    public class AuctionSearchClassFilters : IEquatable<AuctionSearchClassFilters>
    {
        public ItemClassMask Classes;
        public SubclassFilter[] SubClasses = new SubclassFilter[(int)ItemClass.Max];

        public AuctionSearchClassFilters(bool skipAll)
        {
            Classes = skipAll ? ItemClassMask.None : ItemClassMask.AllPermanent;
            for (var i = 0; i < (int)ItemClass.Max; ++i)
                SubClasses[i] = new SubclassFilter(skipAll);
        }

        public bool Equals(AuctionSearchClassFilters other)
        {
            if (!Classes.Equals(other.Classes))
                return false;

            if (SubClasses.Length != other.SubClasses.Length)
                return false;

            for (int i = 0; i < SubClasses.Length; ++i)
            {
                if (!SubClasses[i].Equals(other.SubClasses[i]))
                    return false;
            }

            return true;
        }        

        public class SubclassFilter : IEquatable<SubclassFilter>
        {
            public SubclassFilter(bool skipAll)
            {
                SubclassMask = skipAll ? ItemSubClassMask.None : ItemSubClassMask.AllPermanent;

                for (int i = 0; i < InvTypes.Length; ++i)
                    InvTypes[i] = skipAll ? InventoryTypeMask.None : InventoryTypeMask.AllPermanent;
            }

            public ItemSubClassMask SubclassMask;
            public InventoryTypeMask[] InvTypes = new InventoryTypeMask[ItemSubClass.Max];

            public bool Equals(SubclassFilter other)
            {
                if (!SubclassMask.Equals(other.SubclassMask))
                    return false;

                if (InvTypes.Length != other.InvTypes.Length)
                    return false;

                for (int i = 0; i < InvTypes.Length; ++i)
                {
                    if (!InvTypes[i].Equals(other.InvTypes[i]))
                        return false;
                }

                return true;
            }
        }
    }
}
