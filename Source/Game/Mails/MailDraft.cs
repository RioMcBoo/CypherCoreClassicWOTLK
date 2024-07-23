// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Entities;
using Game.Loots;
using System.Collections.Generic;

namespace Game.Mails
{
    public class MailDraft
    {
        public MailDraft(int mailTemplateId, bool need_items = true)
        {
            m_mailTemplateId = mailTemplateId;
            m_mailTemplateItemsNeed = need_items;
            m_money = 0;
            m_COD = 0;
        }

        public MailDraft(string subject, string body)
        {
            m_mailTemplateId = 0;
            m_mailTemplateItemsNeed = false;
            m_subject = subject;
            m_body = body;
            m_money = 0;
            m_COD = 0;
        }

        public MailDraft AddItem(Item item)
        {
            m_items[item.GetGUID().GetCounter()] = item; 
            return this;
        }

        void PrepareItems(Player receiver, SQLTransaction trans)
        {
            if (m_mailTemplateId == 0 || !m_mailTemplateItemsNeed)
                return;

            m_mailTemplateItemsNeed = false;

            // The mail sent after turning in the quest The Good News and The Bad News contains 100g
            if (m_mailTemplateId == 123)
                m_money = 1000000;

            Loot mailLoot = new(null, ObjectGuid.Empty, LootType.None, null);

            // can be empty
            mailLoot.FillLoot(m_mailTemplateId, LootStorage.Mail, receiver, true, true, LootModes.Default, ItemContext.None);

            for (int i = 0; m_items.Count < SharedConst.MaxMailItems && i < mailLoot.items.Count; ++i)
            {
                LootItem lootitem = mailLoot.LootItemInSlot(i, receiver);
                if (lootitem != null)
                {
                    Item item = Item.CreateItem(lootitem.itemid, lootitem.count, lootitem.context, receiver);
                    if (item != null)
                    {
                        item.SaveToDB(trans);                           // save for prevent lost at next mail load, if send fail then item will deleted
                        AddItem(item);
                    }
                }
            }
        }

        void DeleteIncludedItems(SQLTransaction trans, bool inDB = false)
        {
            foreach (var item in m_items.Values)
            {
                if (inDB)
                    item.DeleteFromDB(trans);
            }

            m_items.Clear();
        }

        public void SendReturnToSender(int senderAcc, long senderGuid, long receiver_guid, SQLTransaction trans)
        {
            ObjectGuid receiverGuid = ObjectGuid.Create(HighGuid.Player, receiver_guid);
            Player receiver = Global.ObjAccessor.FindPlayer(receiverGuid);

            int rc_account = 0;
            if (receiver == null)
                rc_account = Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(receiverGuid);

            if (receiver == null && rc_account == 0)                            // sender not exist
            {
                DeleteIncludedItems(trans, true);
                return;
            }

            // prepare mail and send in other case
            bool needItemDelay = false;

            if (!m_items.Empty())
            {
                // if item send to character at another account, then apply item delivery delay
                needItemDelay = senderAcc != rc_account;

                // set owner to new receiver (to prevent delete item with sender char deleting)
                foreach (var item in m_items.Values)
                {
                    item.SaveToDB(trans);                      // item not in inventory and can be save standalone
                    // owner in data will set at mail receive and item extracting
                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ITEM_OWNER);
                    stmt.SetInt64(0, receiver_guid);
                    stmt.SetInt64(1, item.GetGUID().GetCounter());
                    trans.Append(stmt);
                }
            }

            // If theres is an item, there is a one hour delivery delay.
            uint deliver_delay = needItemDelay ? (uint)WorldConfig.Values[WorldCfg.MailDeliveryDelay].Int32 : 0;

            // will delete item or place to receiver mail list
            SendMailTo(trans, new MailReceiver(receiver, receiver_guid), new MailSender(MailMessageType.Normal, senderGuid), MailCheckFlags.Returned, deliver_delay);
        }

        public void SendMailTo(SQLTransaction trans, Player receiver, MailSender sender, MailCheckFlags checkMask = MailCheckFlags.None, uint deliver_delay = 0)
        {
            SendMailTo(trans, new MailReceiver(receiver), sender, checkMask, deliver_delay);
        }
        public void SendMailTo(SQLTransaction trans, MailReceiver receiver, MailSender sender, MailCheckFlags checkMask = MailCheckFlags.None, uint deliver_delay = 0)
        {
            Player pReceiver = receiver.GetPlayer();               // can be NULL
            Player pSender = sender.GetMailMessageType() == MailMessageType.Normal ? Global.ObjAccessor.FindPlayer(ObjectGuid.Create(HighGuid.Player, sender.GetSenderId())) : null;

            if (pReceiver != null)
                PrepareItems(pReceiver, trans);                            // generate mail template items

            long mailId = Global.ObjectMgr.GenerateMailID();

            long deliver_time = GameTime.GetGameTime() + deliver_delay;

            //expire time if COD 3 days, if no COD 30 days, if auction sale pending 1 hour
            uint expire_delay;

            // auction mail without any items and money
            if (sender.GetMailMessageType() == MailMessageType.Auction && m_items.Empty() && m_money == 0)
                expire_delay = (uint)WorldConfig.Values[WorldCfg.MailDeliveryDelay].Int32;
            // default case: expire time if COD 3 days, if no COD 30 days (or 90 days if sender is a game master)
            else
                if (m_COD != 0)
                expire_delay = 3 * Time.Day;
            else
                expire_delay = (uint)(pSender != null && pSender.IsGameMaster() ? 90 * Time.Day : 30 * Time.Day);

            long expire_time = deliver_time + expire_delay;

            // Add to DB
            byte index = 0;
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_MAIL);
            stmt.SetInt64(index, mailId);
            stmt.SetUInt8(++index, (byte)sender.GetMailMessageType());
            stmt.SetInt8(++index, (sbyte)sender.GetStationery());
            stmt.SetInt32(++index, GetMailTemplateId());
            stmt.SetInt64(++index, sender.GetSenderId());
            stmt.SetInt64(++index, receiver.GetPlayerGUIDLow());
            stmt.SetString(++index, GetSubject());
            stmt.SetString(++index, GetBody());
            stmt.SetBool(++index, !m_items.Empty());
            stmt.SetInt64(++index, expire_time);
            stmt.SetInt64(++index, deliver_time);
            stmt.SetInt64(++index, m_money);
            stmt.SetInt64(++index, m_COD);
            stmt.SetUInt8(++index, (byte)checkMask);
            trans.Append(stmt);

            foreach (var item in m_items.Values)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_MAIL_ITEM);
                stmt.SetInt64(0, mailId);
                stmt.SetInt64(1, item.GetGUID().GetCounter());
                stmt.SetInt64(2, receiver.GetPlayerGUIDLow());
                trans.Append(stmt);
            }

            // For online receiver update in game mail status and data
            if (pReceiver != null)
            {
                pReceiver.AddNewMailDeliverTime(deliver_time);


                Mail m = new();
                m.messageID = mailId;
                m.mailTemplateId = GetMailTemplateId();
                m.subject = GetSubject();
                m.body = GetBody();
                m.money = GetMoney();
                m.COD = GetCOD();

                foreach (var item in m_items.Values)
                    m.AddItem(item.GetGUID().GetCounter(), item.GetEntry());

                m.messageType = sender.GetMailMessageType();
                m.stationery = sender.GetStationery();
                m.sender = sender.GetSenderId();
                m.receiver = receiver.GetPlayerGUIDLow();
                m.expire_time = expire_time;
                m.deliver_time = deliver_time;
                m.checkMask = checkMask;
                m.state = MailState.Unchanged;

                pReceiver.AddMail(m);                           // to insert new mail to beginning of maillist

                if (!m_items.Empty())
                {
                    foreach (var item in m_items.Values)
                        pReceiver.AddMItem(item);
                }
            }
            else if (!m_items.Empty())
                DeleteIncludedItems(null);
        }

        int GetMailTemplateId() { return m_mailTemplateId; }
        string GetSubject() { return m_subject; }
        long GetMoney() { return m_money; }
        long GetCOD() { return m_COD; }
        string GetBody() { return m_body; }

        public MailDraft AddMoney(long money)
        {
            m_money = money;
            return this;
        }

        public MailDraft AddCOD(int COD)
        {
            m_COD = COD;
            return this;
        }

        int m_mailTemplateId;
        bool m_mailTemplateItemsNeed;
        string m_subject;
        string m_body;

        Dictionary<long, Item> m_items = new();

        long m_money;
        long m_COD;
    }
}
