// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;
using System;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.AuctionListItems)]
        void HandleAuctionListItems(AuctionListItems listItems)
        {
            AuctionThrottleResult throttle = Global.AuctionHouseMgr.CheckThrottle(_player, listItems.TaintedBy.HasValue);
            if (throttle.Throttled)
                return;

            Creature creature = GetPlayer().GetNPCIfCanInteractWith(listItems.Auctioneer, NPCFlags1.Auctioneer, NPCFlags2.None);
            if (creature == null)
            {
                Log.outError(LogFilter.Network, $"WORLD: HandleAuctionListItems - {listItems.Auctioneer} not found or you can't interact with him.");
                return;
            }

            // remove fake death
            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            AuctionHouseFilters Filters = new(listItems);

            AuctionHouseObject auctionHouse = Global.AuctionHouseMgr.GetAuctionsMap(creature.GetFaction());

            Log.outDebug(LogFilter.Auctionhouse,
                $"Auctionhouse search ({listItems.Auctioneer}), " +
                $"searchedname: {listItems.Name}, " +
                $"levelmin: {listItems.MinLevel}, " +
                $"levelmax: {listItems.MaxLevel}, " +
                $"filters: {Filters}");


            AuctionListItemsResult listItemsResult = new();

            auctionHouse.BuildListAuctionItems(listItemsResult, _player, Filters, listItems.KnownPets, listItems.KnownPets.Length,
                (byte)listItems.MaxPetLevel, listItems.Offset, listItems.Sorts, listItems.Sorts.Count);

            listItemsResult.DesiredDelay = (Milliseconds)throttle.DelayUntilNext.ToMilliseconds();
            SendPacket(listItemsResult);
        }

        [WorldPacketHandler(ClientOpcodes.AuctionHelloRequest)]
        void HandleAuctionHello(AuctionHelloRequest hello)
        {
            Creature unit = GetPlayer().GetNPCIfCanInteractWith(hello.Guid, NPCFlags1.Auctioneer, NPCFlags2.None);
            if (unit == null)
            {
                Log.outDebug(LogFilter.Network, $"WORLD: HandleAuctionHelloOpcode - {hello.Guid} not found or you can't interact with him.");
                return;
            }

            // remove fake death
            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            SendAuctionHello(hello.Guid, unit);
        }

        [WorldPacketHandler(ClientOpcodes.AuctionListBidderItems)]
        void HandleAuctionListBidderItems(AuctionListBidderItems listBidderItems)
        {
            AuctionThrottleResult throttle = Global.AuctionHouseMgr.CheckThrottle(_player, listBidderItems.TaintedBy.HasValue);
            if (throttle.Throttled)
                return;

            Creature creature = GetPlayer().GetNPCIfCanInteractWith(listBidderItems.Auctioneer, NPCFlags1.Auctioneer, NPCFlags2.None);
            if (creature == null)
            {
                Log.outDebug(LogFilter.Network, $"WORLD: HandleAuctionListBidderItems - {listBidderItems.Auctioneer} not found or you can't interact with him.");
                return;
            }

            // remove fake death
            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            AuctionHouseObject auctionHouse = Global.AuctionHouseMgr.GetAuctionsMap(creature.GetFaction());

            AuctionListBidderItemsResult result = new();

            Player player = GetPlayer();
            auctionHouse.BuildListBidderItems(result, player, listBidderItems.Offset);
            result.DesiredDelay = (Milliseconds)throttle.DelayUntilNext.ToMilliseconds();
            SendPacket(result);
        }

        [WorldPacketHandler(ClientOpcodes.AuctionListOwnerItems)]
        void HandleAuctionListOwnerItems(AuctionListOwnerItems listOwnedItems)
        {
            AuctionThrottleResult throttle = Global.AuctionHouseMgr.CheckThrottle(_player, listOwnedItems.TaintedBy.HasValue);
            if (throttle.Throttled)
                return;

            Creature creature = GetPlayer().GetNPCIfCanInteractWith(listOwnedItems.Auctioneer, NPCFlags1.Auctioneer, NPCFlags2.None);
            if (creature == null)
            {
                Log.outDebug(LogFilter.Network, $"WORLD: HandleAuctionListOwnerItems - {listOwnedItems.Auctioneer} not found or you can't interact with him.");
                return;
            }

            // remove fake death
            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            AuctionHouseObject auctionHouse = Global.AuctionHouseMgr.GetAuctionsMap(creature.GetFaction());

            AuctionListOwnerItemsResult result = new();

            auctionHouse.BuildListOwnerItems(result, _player, listOwnedItems.Offset);
            result.DesiredDelay = (Milliseconds)throttle.DelayUntilNext.ToMilliseconds();
            SendPacket(result);
        }

        [WorldPacketHandler(ClientOpcodes.AuctionPlaceBid)]
        void HandleAuctionPlaceBid(AuctionPlaceBid placeBid)
        {
            AuctionThrottleResult throttle = Global.AuctionHouseMgr.CheckThrottle(_player, placeBid.TaintedBy.HasValue, AuctionCommand.PlaceBid);
            if (throttle.Throttled)
                return;

            Creature creature = GetPlayer().GetNPCIfCanInteractWith(placeBid.Auctioneer, NPCFlags1.Auctioneer, NPCFlags2.None);
            if (creature == null)
            {
                Log.outDebug(LogFilter.Network, $"WORLD: HandleAuctionPlaceBid - {placeBid.Auctioneer} not found or you can't interact with him.");
                return;
            }

            // remove fake death
            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            AuctionHouseObject auctionHouse = Global.AuctionHouseMgr.GetAuctionsMap(creature.GetFaction());

            AuctionPosting auction = auctionHouse.GetAuction(placeBid.AuctionID);
            if (auction == null)
            {
                auctionHouse.UpdateSearchSession(_player, placeBid.AuctionID);
                SendAuctionCommandResult(placeBid.AuctionID, AuctionCommand.PlaceBid, AuctionResult.ItemNotFound, throttle.DelayUntilNext);                
                return;
            }

            Player player = GetPlayer();

            // check auction owner - cannot buy own auctions
            if (auction.Owner == player.GetGUID() || auction.OwnerAccount == GetAccountGUID())
            {
                SendAuctionCommandResult(placeBid.AuctionID, AuctionCommand.PlaceBid, AuctionResult.BidOwn, throttle.DelayUntilNext);
                return;
            }

            bool canBuyout = auction.BuyoutPrice != 0;
            bool isFirstBid = auction.BidAmount == 0;

            long currentBidAmount = isFirstBid ? auction.MinBid : auction.BidAmount;
            long nextBidAmount = isFirstBid ? auction.MinBid : AuctionPosting.CalculateNextBidAmount(auction.BidAmount);

            // cheating attempt
            if (canBuyout && placeBid.BidAmount > auction.BuyoutPrice || placeBid.BidAmount == 0)
            {
                SendAuctionCommandResult(placeBid.AuctionID, AuctionCommand.PlaceBid, AuctionResult.DatabaseError, throttle.DelayUntilNext);
                return;
            }

            if (placeBid.BidAmount < currentBidAmount)
            {
                SendAuctionCommandResult(placeBid.AuctionID, AuctionCommand.PlaceBid, AuctionResult.HigherBid, throttle.DelayUntilNext);
                return;
            }

            if (placeBid.BidAmount < nextBidAmount)
            {
                SendAuctionCommandResult(placeBid.AuctionID, AuctionCommand.PlaceBid, AuctionResult.BidIncrement, throttle.DelayUntilNext);
                return;
            }

            SQLTransaction trans = new();
            long priceToPay = nextBidAmount;
            if (!auction.Bidder.IsEmpty())
            {
                // return money to previous bidder
                if (auction.Bidder != player.GetGUID())
                    auctionHouse.SendAuctionOutbid(auction, player.GetGUID(), placeBid.BidAmount, trans);
                else
                    priceToPay = placeBid.BidAmount - auction.BidAmount;
            }

            // check money
            if (!player.HasEnoughMoney(priceToPay))
            {
                SendAuctionCommandResult(placeBid.AuctionID, AuctionCommand.PlaceBid, AuctionResult.NotEnoughMoney, throttle.DelayUntilNext);
                return;
            }

            player.ModifyMoney(-priceToPay);
            auction.Bidder = player.GetGUID();
            auction.BidAmount = placeBid.BidAmount;
            if (HasPermission(RBACPermissions.LogGmTrade))
                auction.ServerFlags |= AuctionPostingServerFlag.GmLogBuyer;
            else
                auction.ServerFlags &= ~AuctionPostingServerFlag.GmLogBuyer;

            bool auctionSold = canBuyout && placeBid.BidAmount == auction.BuyoutPrice;

            if (auctionSold)
            {
                // buyout
                auctionHouse.SendAuctionSold(auction, null, trans);
                auctionHouse.SendAuctionWon(auction, player, trans);
                auctionHouse.RemoveAuction(trans, auction);
            }
            else
            {
                // place bid
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_AUCTION_BID);
                stmt.SetInt64(0, auction.Bidder.GetCounter());
                stmt.SetInt64(1, auction.BidAmount);
                stmt.SetUInt8(2, (byte)auction.ServerFlags);
                stmt.SetInt32(3, auction.Id);
                trans.Append(stmt);

                if (auction.BidderHistory.Add(player.GetGUID()))
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_AUCTION_BIDDER);
                    stmt.SetInt32(0, auction.Id);
                    stmt.SetInt64(1, player.GetGUID().GetCounter());
                    trans.Append(stmt);
                }

                auctionHouse.AddBidder(auction.Bidder, auction);

                // Not sure if we must send this now.
                Player owner = Global.ObjAccessor.FindConnectedPlayer(auction.Owner);
                if (owner != null)
                    owner.GetSession().SendAuctionOwnerBidNotification(auction);
            }

            player.SaveInventoryAndGoldToDB(trans);
            AddTransactionCallback(DB.Characters.AsyncCommitTransaction(trans)).AfterComplete(success =>
            {
                if (GetPlayer() != null && GetPlayer().GetGUID() == _player.GetGUID())
                {
                    if (success)
                    {
                        GetPlayer().UpdateCriteria(CriteriaType.HighestAuctionBid, placeBid.BidAmount);
                        if (!auctionSold)
                            SendAuctionCommandResult(placeBid.AuctionID, AuctionCommand.PlaceBid, AuctionResult.Ok, throttle.DelayUntilNext);
                    }
                    else
                        SendAuctionCommandResult(placeBid.AuctionID, AuctionCommand.PlaceBid, AuctionResult.DatabaseError, throttle.DelayUntilNext);
                }
            });
        }

        [WorldPacketHandler(ClientOpcodes.AuctionRemoveItem)]
        void HandleAuctionRemoveItem(AuctionRemoveItem removeItem)
        {
            AuctionThrottleResult throttle = Global.AuctionHouseMgr.CheckThrottle(_player, removeItem.TaintedBy.HasValue, AuctionCommand.Cancel);
            if (throttle.Throttled)
                return;

            Creature creature = GetPlayer().GetNPCIfCanInteractWith(removeItem.Auctioneer, NPCFlags1.Auctioneer, NPCFlags2.None);
            if (creature == null)
            {
                Log.outDebug(LogFilter.Network, $"WORLD: HandleAuctionRemoveItem - {removeItem.Auctioneer} not found or you can't interact with him.");
                return;
            }

            // remove fake death
            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            AuctionHouseObject auctionHouse = Global.AuctionHouseMgr.GetAuctionsMap(creature.GetFaction());

            AuctionPosting auction = auctionHouse.GetAuction(removeItem.AuctionID);
            Player player = GetPlayer();

            SQLTransaction trans = new();
            if (auction != null && auction.Owner == player.GetGUID())
            {
                if (!auction.Bidder.IsEmpty())                   // If we have a bidder, we have to send him the money he paid
                {
                    long cancelCost = MathFunctions.CalculatePct(auction.BidAmount, auctionHouse.ConsignmentRate);
                    if (!player.HasEnoughMoney(cancelCost))          //player doesn't have enough money
                    {
                        SendAuctionCommandResult(0, AuctionCommand.Cancel, AuctionResult.NotEnoughMoney, throttle.DelayUntilNext);
                        return;
                    }
                    auctionHouse.SendAuctionCancelledToBidder(auction, trans);
                    player.ModifyMoney(-cancelCost);
                }

                auctionHouse.SendAuctionRemoved(auction, player, trans);
            }
            else
            {
                SendAuctionCommandResult(0, AuctionCommand.Cancel, AuctionResult.DatabaseError, throttle.DelayUntilNext);
                //this code isn't possible ... maybe there should be assert
                Log.outError(LogFilter.Network, $"CHEATER: {player.GetGUID()} tried to cancel auction (id: {removeItem.AuctionID}) of another player or auction is null");
                return;
            }

            // client bug - instead of removing auction in the UI, it only substracts 1 from visible count
            int auctionIdForClient = auction.Id;

            // Now remove the auction
            player.SaveInventoryAndGoldToDB(trans);
            auctionHouse.RemoveAuction(trans, auction);

            AddTransactionCallback(DB.Characters.AsyncCommitTransaction(trans)).AfterComplete(success =>
            {
                if (GetPlayer() != null && GetPlayer().GetGUID() == _player.GetGUID())
                {
                    if (success)
                        SendAuctionCommandResult(auctionIdForClient, AuctionCommand.Cancel, AuctionResult.Ok, throttle.DelayUntilNext);        //inform player, that auction is removed
                    else
                        SendAuctionCommandResult(0, AuctionCommand.Cancel, AuctionResult.DatabaseError, throttle.DelayUntilNext);
                }
            });
        }

        [WorldPacketHandler(ClientOpcodes.AuctionListPendingSales)]
        void HandleAuctionListPendingSales(AuctionListPendingSales pendingSales)
        {
            AuctionListPendingSalesResult response = new();
            SendPacket(response);
        }

        [WorldPacketHandler(ClientOpcodes.AuctionReplicateItems)]
        void HandleAuctionReplicateItems(AuctionReplicateItems replicateItems)
        {
            Creature creature = GetPlayer().GetNPCIfCanInteractWith(replicateItems.Auctioneer, NPCFlags1.Auctioneer, NPCFlags2.None);
            if (creature == null)
            {
                Log.outError(LogFilter.Network, $"WORLD: HandleReplicateItems - {replicateItems.Auctioneer} not found or you can't interact with him.");
                return;
            }

            // remove fake death
            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            AuctionHouseObject auctionHouse = Global.AuctionHouseMgr.GetAuctionsMap(creature.GetFaction());

            AuctionReplicateResponse response = new();

            auctionHouse.BuildReplicate(response, GetPlayer(), replicateItems.ChangeNumberGlobal, replicateItems.ChangeNumberCursor, replicateItems.ChangeNumberTombstone, replicateItems.Count);

            response.DesiredDelay = WorldConfig.Values[WorldCfg.AuctionSearchDelay].TimeSpan * 5;
            response.Result = 0;

            SendPacket(response);
        }

        [WorldPacketHandler(ClientOpcodes.AuctionSellItem)]
        void HandleAuctionSellItem(AuctionSellItem sellItem)
        {
            AuctionThrottleResult throttle = Global.AuctionHouseMgr.CheckThrottle(_player, sellItem.TaintedBy.HasValue, AuctionCommand.SellItem);
            if (throttle.Throttled)
                return;

            if (sellItem.Items.Count != 1)
            {
                SendAuctionCommandResult(0, AuctionCommand.SellItem, AuctionResult.ItemNotFound, throttle.DelayUntilNext);
                return;
            }

            if (sellItem.MinBid == 0)
            {
                SendAuctionCommandResult(0, AuctionCommand.SellItem, AuctionResult.DatabaseError, throttle.DelayUntilNext);
                return;
            }

            if (sellItem.MinBid > PlayerConst.MaxMoneyAmount || sellItem.BuyoutPrice > PlayerConst.MaxMoneyAmount)
            {
                Log.outError(LogFilter.Network,
                    $"WORLD: HandleAuctionSellItem - Player {_player.GetName()} ({_player.GetGUID()}) " +
                    $"attempted to sell item with higher price than max gold amount.");
                SendAuctionCommandResult(0, AuctionCommand.SellItem, AuctionResult.Inventory, throttle.DelayUntilNext, InventoryResult.TooMuchGold);
                return;
            }

            Creature creature = GetPlayer().GetNPCIfCanInteractWith(sellItem.Auctioneer, NPCFlags1.Auctioneer, NPCFlags2.None);
            if (creature == null)
            {
                Log.outError(LogFilter.Network,
                    $"WORLD: HandleAuctionSellItem - Unit ({sellItem.Auctioneer}) " +
                    $"not found or you can't interact with him.");
                return;
            }

            AuctionHouseRecord auctionHouseEntry = Global.AuctionHouseMgr.GetAuctionHouseEntry(creature.GetFaction());
            if (auctionHouseEntry == null)
            {
                Log.outError(LogFilter.Network,
                    $"WORLD: HandleAuctionSellItem - Unit ({sellItem.Auctioneer})" +
                    $" has wrong faction.");
                return;
            }

            if (sellItem.RunTime.Ticks != 1 * SharedConst.MinAuctionTime.Ticks &&
                sellItem.RunTime.Ticks != 2 * SharedConst.MinAuctionTime.Ticks &&
                sellItem.RunTime.Ticks != 4 * SharedConst.MinAuctionTime.Ticks)
            {
                SendAuctionCommandResult(0, AuctionCommand.SellItem, AuctionResult.DatabaseError, throttle.DelayUntilNext);
                return;
            }

            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            Item item = _player.GetItemByGuid(sellItem.Items[0].Guid);
            if (item == null)
            {
                SendAuctionCommandResult(0, AuctionCommand.SellItem, AuctionResult.ItemNotFound, throttle.DelayUntilNext);
                return;
            }

            if (Global.AuctionHouseMgr.GetAItem(item.GetGUID()) != null || !item.CanBeTraded() || item.IsNotEmptyBag() ||
                item.GetTemplate().HasFlag(ItemFlags.Conjured) || item.m_itemData.Expiration != Seconds.Zero)
            {
                SendAuctionCommandResult(0, AuctionCommand.SellItem, AuctionResult.DatabaseError, throttle.DelayUntilNext);
                return;
            }

            TimeSpan auctionTime = sellItem.RunTime * WorldConfig.Values[WorldCfg.RateAuctionTime].Float;
            AuctionHouseObject auctionHouse = Global.AuctionHouseMgr.GetAuctionsMap(creature.GetFaction());

            long deposit = auctionHouse.GetAuctionDeposit(item.GetTemplate(), sellItem.RunTime, sellItem.Items[0].UseCount);
            if (!_player.HasEnoughMoney(deposit))
            {
                SendAuctionCommandResult(0, AuctionCommand.SellItem, AuctionResult.NotEnoughMoney, throttle.DelayUntilNext);
                return;
            }

            int auctionId = Global.ObjectMgr.GenerateAuctionID();

            AuctionPosting auction = new();
            auction.Id = auctionId;
            auction.Owner = _player.GetGUID();
            auction.OwnerAccount = GetAccountGUID();
            auction.MinBid = sellItem.MinBid;
            auction.BuyoutPrice = sellItem.BuyoutPrice;
            auction.Deposit = deposit;
            auction.StartTime = LoopTime.ServerTime;
            auction.EndTime = auction.StartTime + auctionTime;

            if (HasPermission(RBACPermissions.LogGmTrade))
                Log.outCommand(GetAccountId(),
                    $"GM {GetPlayerName()} (Account: {GetAccountId()}) " +
                    $"create auction: {item.GetTemplate().GetName()} " +
                    $"(Entry: {item.GetEntry()} Count: {item.GetCount()})");

            auction.Item = item;

            Log.outInfo(LogFilter.Network,
                $"CMSG_AuctionAction.SellItem: {_player.GetGUID()} {_player.GetName()} " +
                $"is selling item {item.GetGUID()} {item.GetTemplate().GetName()} " +
                $"to auctioneer {creature.GetGUID()} with count {item.GetCount()} " +
                $"with initial bid {sellItem.MinBid} with buyout {sellItem.BuyoutPrice} " +
                $"and with time {(Seconds)auctionTime} (in sec) " +
                $"in auctionhouse {auctionHouse.Id}");

            // Add to pending auctions, or fail with insufficient funds error
            if (!Global.AuctionHouseMgr.PendingAuctionAdd(_player, auction))
            {
                SendAuctionCommandResult(0, AuctionCommand.SellItem, AuctionResult.NotEnoughMoney, throttle.DelayUntilNext);
                return;
            }

            _player.MoveItemFromInventory(item.InventoryPosition, true);

            SQLTransaction trans = new();
            item.DeleteFromInventoryDB(trans);
            item.SaveToDB(trans);

            auctionHouse.AddAuction(trans, auction);
            _player.SaveInventoryAndGoldToDB(trans);

            var auctionPlayerGuid = _player.GetGUID();
            AddTransactionCallback(DB.Characters.AsyncCommitTransaction(trans)).AfterComplete(success =>
            {
                if (GetPlayer() != null && GetPlayer().GetGUID() == auctionPlayerGuid)
                {
                    if (success)
                    {
                        GetPlayer().UpdateCriteria(CriteriaType.ItemsPostedAtAuction, 1);
                        SendAuctionCommandResult(auctionId, AuctionCommand.SellItem, AuctionResult.Ok, throttle.DelayUntilNext);
                    }
                    else
                        SendAuctionCommandResult(0, AuctionCommand.SellItem, AuctionResult.DatabaseError, throttle.DelayUntilNext);
                }
            });
        }

        public void SendAuctionHello(ObjectGuid guid, Creature unit)
        {
            if (GetPlayer().GetLevel() < WorldConfig.Values[WorldCfg.AuctionLevelReq].Int32)
            {
                SendNotification(Global.ObjectMgr.GetCypherString(CypherStrings.AuctionReq), WorldConfig.Values[WorldCfg.AuctionLevelReq].Int32);
                return;
            }

            AuctionHouseRecord ahEntry = Global.AuctionHouseMgr.GetAuctionHouseEntry(unit.GetFaction());
            if (ahEntry == null)
                return;

            AuctionHelloResponse auctionHelloResponse = new();
            auctionHelloResponse.AuctionHouseId = ahEntry.Id;
            auctionHelloResponse.Guid = guid;
            auctionHelloResponse.OpenForBusiness = true;
            SendPacket(auctionHelloResponse);
        }

        public void SendAuctionCommandResult(int auctionId, AuctionCommand command, AuctionResult errorCode, TimeSpan delayForNextAction, InventoryResult bagError = 0)
        {
            AuctionCommandResult auctionCommandResult = new();
            auctionCommandResult.AuctionID = auctionId;
            auctionCommandResult.Command = (int)command;
            auctionCommandResult.ErrorCode = errorCode;
            auctionCommandResult.BagResult = bagError;
            auctionCommandResult.DesiredDelay = delayForNextAction;
            SendPacket(auctionCommandResult);
        }

        public void SendAuctionClosedNotification(AuctionPosting auction, TimeSpan mailDelay, bool sold)
        {
            AuctionClosedNotification packet = new();
            packet.Info.Initialize(auction);
            packet.ProceedsMailDelay = mailDelay;
            packet.Sold = sold;
            SendPacket(packet);
        }

        public void SendAuctionOwnerBidNotification(AuctionPosting auction)
        {
            AuctionOwnerBidNotification packet = new();
            packet.Info.Initialize(auction);
            packet.Bidder = auction.Bidder;
            packet.MinIncrement = auction.CalculateMinIncrement();
            SendPacket(packet);
        }
    }
}
