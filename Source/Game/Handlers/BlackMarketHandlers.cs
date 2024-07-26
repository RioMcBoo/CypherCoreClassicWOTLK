// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.BlackMarket;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;
using System;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.BlackMarketOpen)]
        void HandleBlackMarketOpen(BlackMarketOpen blackMarketOpen)
        {
            Creature unit = GetPlayer().GetNPCIfCanInteractWith(blackMarketOpen.Guid, NPCFlags1.BlackMarket, NPCFlags2.BlackMarketView);
            if (unit == null)
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: HandleBlackMarketHello - " +
                    $"{blackMarketOpen.Guid} not found or you can't interact with him.");
                return;
            }

            // remove fake death
            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            SendBlackMarketOpenResult(blackMarketOpen.Guid, unit);
        }

        void SendBlackMarketOpenResult(ObjectGuid guid, Creature auctioneer)
        {
            NPCInteractionOpenResult npcInteraction = new();
            npcInteraction.Npc = guid;
            npcInteraction.InteractionType = PlayerInteractionType.BlackMarketAuctioneer;
            npcInteraction.Success = Global.BlackMarketMgr.IsEnabled();
            SendPacket(npcInteraction);
        }

        [WorldPacketHandler(ClientOpcodes.BlackMarketRequestItems)]
        void HandleBlackMarketRequestItems(BlackMarketRequestItems blackMarketRequestItems)
        {
            if (!Global.BlackMarketMgr.IsEnabled())
                return;

            Creature unit = GetPlayer().GetNPCIfCanInteractWith(blackMarketRequestItems.Guid, NPCFlags1.BlackMarket, NPCFlags2.BlackMarketView);
            if (unit == null)
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: HandleBlackMarketRequestItems - " +
                    $"{blackMarketRequestItems.Guid} not found or you can't interact with him.");
                return;
            }

            BlackMarketRequestItemsResult result = new();
            Global.BlackMarketMgr.BuildItemsResponse(result, GetPlayer());
            SendPacket(result);
        }

        [WorldPacketHandler(ClientOpcodes.BlackMarketBidOnItem)]
        void HandleBlackMarketBidOnItem(BlackMarketBidOnItem blackMarketBidOnItem)
        {
            if (!Global.BlackMarketMgr.IsEnabled())
                return;

            Player player = GetPlayer();
            Creature unit = player.GetNPCIfCanInteractWith(blackMarketBidOnItem.Guid, NPCFlags1.BlackMarket, NPCFlags2.None);
            if (unit == null)
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: HandleBlackMarketBidOnItem - " +
                    $"{blackMarketBidOnItem.Guid} not found or you can't interact with him.");
                return;
            }

            BlackMarketEntry entry = Global.BlackMarketMgr.GetAuctionByID(blackMarketBidOnItem.MarketID);
            if (entry == null)
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: HandleBlackMarketBidOnItem - {player.GetGUID()} (name: {player.GetName()}) " +
                    $"tried to bid on a nonexistent auction (MarketId: {blackMarketBidOnItem.MarketID}).");
                SendBlackMarketBidOnItemResult(BlackMarketError.ItemNotFound, blackMarketBidOnItem.MarketID, blackMarketBidOnItem.Item);
                return;
            }

            if (entry.GetBidder() == player.GetGUID().GetCounter())
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: HandleBlackMarketBidOnItem - {player.GetGUID()} (name: {player.GetName()}) " +
                    $"tried to place a bid on an item he already bid on. (MarketId: {blackMarketBidOnItem.MarketID}).");
                SendBlackMarketBidOnItemResult(BlackMarketError.AlreadyBid, blackMarketBidOnItem.MarketID, blackMarketBidOnItem.Item);
                return;
            }

            if (!entry.ValidateBid(blackMarketBidOnItem.BidAmount))
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: HandleBlackMarketBidOnItem - {player.GetGUID()} (name: {player.GetName()}) " +
                    $"tried to place an invalid bid. Amount: {blackMarketBidOnItem.BidAmount} (MarketId: {blackMarketBidOnItem.MarketID}).");
                SendBlackMarketBidOnItemResult(BlackMarketError.HigherBid, blackMarketBidOnItem.MarketID, blackMarketBidOnItem.Item);
                return;
            }

            if (!player.HasEnoughMoney(blackMarketBidOnItem.BidAmount))
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: HandleBlackMarketBidOnItem - {player.GetGUID()} (name: {player.GetName()}) " +
                    $"does not have enough money to place bid. (MarketId: {blackMarketBidOnItem.MarketID}).");
                SendBlackMarketBidOnItemResult(BlackMarketError.NotEnoughMoney, blackMarketBidOnItem.MarketID, blackMarketBidOnItem.Item);
                return;
            }

            if (entry.GetSecondsRemaining() <= TimeSpan.Zero)
            {
                Log.outDebug(LogFilter.Network, 
                    $"WORLD: HandleBlackMarketBidOnItem - {player.GetGUID()} (name: {player.GetName()}) " +
                    $"tried to bid on a completed auction. (MarketId: {blackMarketBidOnItem.MarketID}).");
                SendBlackMarketBidOnItemResult(BlackMarketError.DatabaseError, blackMarketBidOnItem.MarketID, blackMarketBidOnItem.Item);
                return;
            }

            SQLTransaction trans = new();

            Global.BlackMarketMgr.SendAuctionOutbidMail(entry, trans);
            entry.PlaceBid(blackMarketBidOnItem.BidAmount, player, trans);

            DB.Characters.CommitTransaction(trans);

            SendBlackMarketBidOnItemResult(BlackMarketError.Ok, blackMarketBidOnItem.MarketID, blackMarketBidOnItem.Item);
        }

        void SendBlackMarketBidOnItemResult(BlackMarketError result, int marketId, ItemInstance item)
        {
            BlackMarketBidOnItemResult packet = new();

            packet.MarketID = marketId;
            packet.Item = item;
            packet.Result = result;

            SendPacket(packet);
        }

        public void SendBlackMarketWonNotification(BlackMarketEntry entry, Item item)
        {
            BlackMarketWon packet = new();

            packet.MarketID = entry.GetMarketId();
            packet.Item = new ItemInstance(item);

            SendPacket(packet);
        }

        public void SendBlackMarketOutbidNotification(BlackMarketTemplate templ)
        {
            BlackMarketOutbid packet = new();

            packet.MarketID = templ.MarketID;
            packet.Item = templ.Item;
            packet.RandomPropertiesID = 0;

            SendPacket(packet);
        }
    }
}
