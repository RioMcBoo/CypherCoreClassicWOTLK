﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.BlackMarket;
using Game.Entities;
using System.Collections.Generic;

namespace Game.Mails
{
    public class Mail
    {
        public void AddItem(long itemGuidLow, int item_template)
        {
            MailItemInfo mii = new();
            mii.item_guid = itemGuidLow;
            mii.item_template = item_template;
            items.Add(mii);
        }

        public bool RemoveItem(long itemGuid)
        {
            foreach (var item in items)
            {
                if (item.item_guid == itemGuid)
                {
                    items.Remove(item);
                    return true;
                }
            }
            return false;
        }

        public bool HasItems() { return !items.Empty(); }

        public long messageID;
        public MailMessageType messageType;
        public MailStationery stationery;
        public int mailTemplateId;
        public long sender;
        public long receiver;
        public string subject;
        public string body;
        public List<MailItemInfo> items = new();
        public List<long> removedItems = new();
        public ServerTime expire_time;
        public ServerTime deliver_time;
        public long money;
        public long COD;
        public MailCheckFlags checkMask;
        public MailState state;
    }

    public class MailItemInfo
    {
        public long item_guid;
        public int item_template;
    }

    public class MailReceiver
    {
        public MailReceiver(long receiver_lowguid)
        {
            m_receiver = null;
            m_receiver_lowguid = receiver_lowguid;
        }

        public MailReceiver(Player receiver)
        {
            m_receiver = receiver;
            m_receiver_lowguid = receiver.GetGUID().GetCounter();            
        }

        public MailReceiver(Player receiver, long receiver_lowguid)
        {
            m_receiver = receiver;
            m_receiver_lowguid = receiver_lowguid;

            Cypher.Assert(receiver == null || receiver.GetGUID().GetCounter() == receiver_lowguid);
        }

        public MailReceiver(Player receiver, ObjectGuid receiverGuid)
        {
            m_receiver = receiver;
            m_receiver_lowguid = receiverGuid.GetCounter();

            Cypher.Assert(receiver == null || receiver.GetGUID() == receiverGuid);
        }

        public Player GetPlayer() { return m_receiver; }
        public long GetPlayerGUIDLow() { return m_receiver_lowguid; }

        Player m_receiver;
        long m_receiver_lowguid;
    }

    public class MailSender
    {
        public MailSender(MailMessageType messageType, long sender_guidlow_or_entry, MailStationery stationery = MailStationery.Default)
        {
            m_messageType = messageType;
            m_senderId = sender_guidlow_or_entry;
            m_stationery = stationery;
        }

        public MailSender(WorldObject sender, MailStationery stationery = MailStationery.Default)
        {
            m_stationery = stationery;
            switch (sender.GetTypeId())
            {
                case TypeId.Unit:
                    m_messageType = MailMessageType.Creature;
                    m_senderId = sender.GetEntry();
                    break;
                case TypeId.GameObject:
                    m_messageType = MailMessageType.Gameobject;
                    m_senderId = sender.GetEntry();
                    break;
                case TypeId.Player:
                    m_messageType = MailMessageType.Normal;
                    m_senderId = sender.GetGUID().GetCounter();
                    break;
                default:
                    m_messageType = MailMessageType.Normal;
                    m_senderId = 0;                                 // will show mail from not existed player

                    Log.outError(LogFilter.Server, 
                        $"MailSender:MailSender - " +
                        $"Mail have unexpected sender typeid ({sender.GetTypeId()})");
                    break;
            }
        }

        public MailSender(CalendarEvent sender)
        {
            m_messageType = MailMessageType.Calendar;
            m_senderId = (uint)sender.EventId;
            m_stationery = MailStationery.Default; 
        }

        public MailSender(AuctionHouseId sender)
        {
            m_messageType = MailMessageType.Auction;
            m_senderId = (long)sender;
            m_stationery = MailStationery.Auction;
        }

        public MailSender(BlackMarketEntry sender)
        {
            m_messageType = MailMessageType.Blackmarket;
            m_senderId = sender.GetTemplate().SellerNPC;
            m_stationery = MailStationery.Auction;
        }

        public MailSender(Player sender)
        {
            m_messageType = MailMessageType.Normal;
            m_stationery = sender.IsGameMaster() ? MailStationery.Gm : MailStationery.Default;
            m_senderId = sender.GetGUID().GetCounter();
        }

        public MailSender(int senderEntry)
        {
            m_messageType = MailMessageType.Creature;
            m_senderId = senderEntry;
            m_stationery = MailStationery.Default;
        }

        public MailMessageType GetMailMessageType() { return m_messageType; }
        public long GetSenderId() { return m_senderId; }
        public MailStationery GetStationery() { return m_stationery; }

        MailMessageType m_messageType;
        long m_senderId;                                  // player low guid or other object entry
        MailStationery m_stationery;
    }
}
