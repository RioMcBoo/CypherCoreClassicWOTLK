﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Cache;
using Game.DataStorage;
using Game.Entities;
using Game.Guilds;
using Game.Mails;
using Game.Networking;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public partial class WorldSession
    {
        bool CanOpenMailBox(ObjectGuid guid)
        {
            if (guid == GetPlayer().GetGUID())
            {
                if (!HasPermission(RBACPermissions.CommandMailbox))
                {
                    Log.outWarn(LogFilter.ChatSystem, 
                        $"{GetPlayer().GetName()} attempt open mailbox in cheating way.");
                    return false;
                }
            }
            else if (guid.IsGameObject())
            {
                if (GetPlayer().GetGameObjectIfCanInteractWith(guid, GameObjectTypes.Mailbox) == null)
                    return false;
            }
            else if (guid.IsAnyTypeCreature())
            {
                if (GetPlayer().GetNPCIfCanInteractWith(guid, NPCFlags1.Mailbox, NPCFlags2.None) == null)
                    return false;
            }
            else
                return false;

            return true;
        }

        [WorldPacketHandler(ClientOpcodes.SendMail)]
        void HandleSendMail(SendMail sendMail)
        {
            if (sendMail.Info.Attachments.Count > SharedConst.MaxClientMailItems)                      // client limit
            {
                GetPlayer().SendMailResult(0, MailResponseType.Send, MailResponseResult.TooManyAttachments);
                return;
            }

            if (!CanOpenMailBox(sendMail.Info.Mailbox))
                return;

            if (string.IsNullOrEmpty(sendMail.Info.Target))
                return;

            if (!ValidateHyperlinksAndMaybeKick(sendMail.Info.Subject) || !ValidateHyperlinksAndMaybeKick(sendMail.Info.Body))
                return;

            Player player = GetPlayer();
            if (player.GetLevel() < WorldConfig.Values[WorldCfg.MailLevelReq].Int32)
            {
                SendNotification(CypherStrings.MailSenderReq, WorldConfig.Values[WorldCfg.MailLevelReq].Int32);
                return;
            }

            ObjectGuid receiverGuid = ObjectGuid.Empty;
            if (ObjectManager.NormalizePlayerName(ref sendMail.Info.Target))
                receiverGuid = Global.CharacterCacheStorage.GetCharacterGuidByName(sendMail.Info.Target);

            if (receiverGuid.IsEmpty())
            {
                Log.outInfo(LogFilter.Network, 
                    $"Player {GetPlayerInfo()} is sending mail to {sendMail.Info.Target} (GUID: not existed!) " +
                    $"with subject {sendMail.Info.Subject} and body {sendMail.Info.Body} " +
                    $"includes {sendMail.Info.Attachments.Count} items, {sendMail.Info.SendMoney} copper " +
                    $"and {sendMail.Info.Cod} COD copper with StationeryID = {sendMail.Info.StationeryID}");

                player.SendMailResult(0, MailResponseType.Send, MailResponseResult.RecipientNotFound);
                return;
            }

            if (sendMail.Info.SendMoney < 0)
            {
                GetPlayer().SendMailResult(0, MailResponseType.Send, MailResponseResult.InternalError);
                Log.outWarn(LogFilter.Server, 
                    $"Player {GetPlayerInfo()} attempted " +
                    $"to send mail to {sendMail.Info.Target} ({receiverGuid}) " +
                    $"with negative money value (SendMoney: {sendMail.Info.SendMoney})");
                return;
            }

            if (sendMail.Info.Cod < 0)
            {
                GetPlayer().SendMailResult(0, MailResponseType.Send, MailResponseResult.InternalError);
                Log.outWarn(LogFilter.Server, 
                    $"Player {GetPlayerInfo()} attempted " +
                    $"to send mail to {sendMail.Info.Target} ({receiverGuid}) " +
                    $"with negative COD value (Cod: {sendMail.Info.Cod})");
                return;
            }

            Log.outInfo(LogFilter.Network, 
                $"Player {GetPlayerInfo()} is sending mail to {sendMail.Info.Target} ({receiverGuid}) " +
                $"with subject {sendMail.Info.Subject} and body {sendMail.Info.Body}" +
                $"includes {sendMail.Info.Attachments.Count} items, {sendMail.Info.SendMoney} copper " +
                $"and {sendMail.Info.Cod} COD copper with StationeryID = {sendMail.Info.StationeryID}");

            if (player.GetGUID() == receiverGuid)
            {
                player.SendMailResult(0, MailResponseType.Send, MailResponseResult.CannotSendToSelf);
                return;
            }

            uint cost = (uint)(!sendMail.Info.Attachments.Empty() ? 30 * sendMail.Info.Attachments.Count : 30);  // price hardcoded in client

            long reqmoney = cost + sendMail.Info.SendMoney;

            // Check for overflow
            if (reqmoney < sendMail.Info.SendMoney)
            {
                player.SendMailResult(0, MailResponseType.Send, MailResponseResult.NotEnoughMoney);
                return;
            }

            void mailCountCheckContinuation(Team receiverTeam, long mailsCount, int receiverLevel, int receiverAccountId, int receiverBnetAccountId)
            {
                if (_player != player)
                    return;

                if (!player.HasEnoughMoney(reqmoney) && !player.IsGameMaster())
                {
                    player.SendMailResult(0, MailResponseType.Send, MailResponseResult.NotEnoughMoney);
                    return;
                }

                // do not allow to have more than 100 mails in mailbox.. mails count is in opcode uint8!!! - so max can be 255..
                if (mailsCount > 100)
                {
                    player.SendMailResult(0, MailResponseType.Send, MailResponseResult.RecipientCapReached);
                    return;
                }

                // test the receiver's Faction... or all items are account bound
                bool accountBound = !sendMail.Info.Attachments.Empty();
                foreach (var att in sendMail.Info.Attachments)
                {
                    Item item = player.GetItemByGuid(att.ItemGUID);
                    if (item != null)
                    {
                        ItemTemplate itemProto = item.GetTemplate();
                        if (itemProto == null || !itemProto.HasFlag(ItemFlags.IsBoundToAccount))
                        {
                            accountBound = false;
                            break;
                        }
                    }
                }

                if (!accountBound && player.GetTeam() != receiverTeam && !HasPermission(RBACPermissions.TwoSideInteractionMail))
                {
                    player.SendMailResult(0, MailResponseType.Send, MailResponseResult.NotYourTeam);
                    return;
                }

                if (receiverLevel < WorldConfig.Values[WorldCfg.MailLevelReq].Int32)
                {
                    SendNotification(CypherStrings.MailReceiverReq, WorldConfig.Values[WorldCfg.MailLevelReq].Int32);
                    return;
                }

                List<Item> items = new();

                foreach (var att in sendMail.Info.Attachments)
                {
                    if (att.ItemGUID.IsEmpty())
                    {
                        player.SendMailResult(0, MailResponseType.Send, MailResponseResult.MailAttachmentInvalid);
                        return;
                    }

                    Item item = player.GetItemByGuid(att.ItemGUID);

                    // prevent sending bag with items (cheat: can be placed in bag after adding equipped empty bag to mail)
                    if (item == null)
                    {
                        player.SendMailResult(0, MailResponseType.Send, MailResponseResult.MailAttachmentInvalid);
                        return;
                    }

                    // handle empty bag before CanBeTraded, since that func already has that check
                    if (item.IsNotEmptyBag())
                    {
                        player.SendMailResult(0, MailResponseType.Send, MailResponseResult.EquipError, InventoryResult.DestroyNonemptyBag);
                        return;
                    }

                    if (!item.CanBeTraded(true))
                    {
                        player.SendMailResult(0, MailResponseType.Send, MailResponseResult.EquipError, InventoryResult.MailBoundItem);
                        return;
                    }

                    if (item.IsBoundAccountWide() && item.IsSoulBound() && player.GetSession().GetAccountId() != receiverAccountId)
                    {
                        if (!item.IsBattlenetAccountBound() || player.GetSession().GetBattlenetAccountId() == 0 
                            || player.GetSession().GetBattlenetAccountId() != receiverBnetAccountId)
                        {
                            player.SendMailResult(0, MailResponseType.Send, MailResponseResult.EquipError, InventoryResult.NotSameAccount);
                            return;
                        }
                    }

                    if (item.GetTemplate().HasFlag(ItemFlags.Conjured) || item.m_itemData.Expiration != Seconds.Zero)
                    {
                        player.SendMailResult(0, MailResponseType.Send, MailResponseResult.EquipError, InventoryResult.MailBoundItem);
                        return;
                    }

                    if (sendMail.Info.Cod != 0 && item.IsWrapped())
                    {
                        player.SendMailResult(0, MailResponseType.Send, MailResponseResult.CantSendWrappedCod);
                        return;
                    }

                    items.Add(item);
                }

                player.SendMailResult(0, MailResponseType.Send, MailResponseResult.Ok);

                player.ModifyMoney(-reqmoney);
                player.UpdateCriteria(CriteriaType.MoneySpentOnPostage, cost);

                bool needItemDelay = false;

                MailDraft draft = new(sendMail.Info.Subject, sendMail.Info.Body);

                SQLTransaction trans = new SQLTransaction();

                if (!sendMail.Info.Attachments.Empty() || sendMail.Info.SendMoney > 0)
                {
                    bool log = HasPermission(RBACPermissions.LogGmTrade);
                    if (!sendMail.Info.Attachments.Empty())
                    {
                        foreach (var item in items)
                        {
                            if (log)
                            {
                                Log.outCommand(GetAccountId(), 
                                    $"GM {GetPlayerName()} ({_player.GetGUID()}) (Account: {GetAccountId()}) " +
                                    $"mail item: {item.GetTemplate().GetName()} " +
                                    $"(Entry: {item.GetEntry()} Count: {item.GetCount()}) " +
                                    $"to: {sendMail.Info.Target} ({receiverGuid}) (Account: {receiverAccountId})");
                            }

                            item.SetNotRefundable(GetPlayer()); // makes the item no longer refundable
                            player.MoveItemFromInventory(item.InventoryPosition, true);

                            item.DeleteFromInventoryDB(trans);     // deletes item from character's inventory
                            item.SetOwnerGUID(receiverGuid);
                            item.SetState(ItemUpdateState.Changed);
                            item.SaveToDB(trans);                  // recursive and not have transaction guard into self, item not in inventory and can be save standalone

                            draft.AddItem(item);
                        }

                        // if item send to character at another account, then apply item delivery delay
                        needItemDelay = player.GetSession().GetAccountId() != receiverAccountId;
                    }

                    if (log && sendMail.Info.SendMoney > 0)
                    {
                        Log.outCommand(GetAccountId(),
                            $"GM {GetPlayerName()} ({_player.GetGUID()}) (Account: {GetAccountId()}) " +
                            $"mail money: {sendMail.Info.SendMoney} " +
                            $"to: {sendMail.Info.Target} ({receiverGuid}) (Account: {receiverAccountId})");
                    }
                }

                // If theres is an item, there is a one hour delivery delay if sent to another account's character.
                TimeSpan deliver_delay = needItemDelay ? WorldConfig.Values[WorldCfg.MailDeliveryDelay].TimeSpan : TimeSpan.Zero;

                // Mail sent between guild members arrives instantly
                Guild guild = Global.GuildMgr.GetGuildById(player.GetGuildId());
                if (guild != null)
                {
                    if (guild.IsMember(receiverGuid))
                        deliver_delay = TimeSpan.Zero;
                }

                // don't ask for COD if there are no items
                if (sendMail.Info.Attachments.Empty())
                    sendMail.Info.Cod = 0;

                // will delete item or place to receiver mail list
                draft.AddMoney(sendMail.Info.SendMoney)
                    .AddCOD((int)sendMail.Info.Cod)
                    .SendMailTo(trans, 
                        new MailReceiver(Global.ObjAccessor.FindConnectedPlayer(receiverGuid), receiverGuid.GetCounter()), 
                        new MailSender(player), sendMail.Info.Body.IsEmpty() ? MailCheckFlags.Copied : MailCheckFlags.HasBody, deliver_delay);

                player.SaveInventoryAndGoldToDB(trans);
                DB.Characters.CommitTransaction(trans);
            }

            Player receiver = Global.ObjAccessor.FindPlayer(receiverGuid);
            if (receiver != null)
            {
                mailCountCheckContinuation(receiver.GetTeam(), receiver.GetMailSize(), receiver.GetLevel(),
                    receiver.GetSession().GetAccountId(), receiver.GetSession().GetBattlenetAccountId());
            }
            else
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_MAIL_COUNT);
                stmt.SetInt64(0, receiverGuid.GetCounter());

                GetQueryProcessor().AddCallback(DB.Characters.AsyncQuery(stmt).WithChainingCallback((queryCallback, mailCountResult) =>
                {
                    CharacterCacheEntry characterInfo = Global.CharacterCacheStorage.GetCharacterCacheByGuid(receiverGuid);
                    if (characterInfo != null)
                        queryCallback.WithCallback(bnetAccountResult =>
                        {
                            mailCountCheckContinuation(Player.TeamForRace(characterInfo.RaceId), !mailCountResult.IsEmpty() ? mailCountResult.Read<long>(0) : 0,
                                characterInfo.Level, characterInfo.AccountId, !bnetAccountResult.IsEmpty() ? bnetAccountResult.Read<int>(0) : 0);
                        }).SetNextQuery(Global.BNetAccountMgr.GetIdByGameAccountAsync(characterInfo.AccountId));
                }));
            }
        }

        //called when mail is read
        [WorldPacketHandler(ClientOpcodes.MailMarkAsRead)]
        void HandleMailMarkAsRead(MailMarkAsRead markAsRead)
        {
            if (!CanOpenMailBox(markAsRead.Mailbox))
                return;

            Player player = GetPlayer();
            Mail m = player.GetMail(markAsRead.MailID);
            if (m != null && m.state != MailState.Deleted)
            {
                if (player.unReadMails != 0)
                    --player.unReadMails;
                m.checkMask = m.checkMask | MailCheckFlags.Read;
                player.m_mailsUpdated = true;
                m.state = MailState.Changed;
            }
        }

        //called when client deletes mail
        [WorldPacketHandler(ClientOpcodes.MailDelete)]
        void HandleMailDelete(MailDelete mailDelete)
        {
            Mail m = GetPlayer().GetMail(mailDelete.MailID);
            Player player = GetPlayer();
            player.m_mailsUpdated = true;
            if (m != null)
            {
                // delete shouldn't show up for COD mails
                if (m.COD != 0)
                {
                    player.SendMailResult(mailDelete.MailID, MailResponseType.Deleted, MailResponseResult.InternalError);
                    return;
                }

                m.state = MailState.Deleted;
            }
            player.SendMailResult(mailDelete.MailID, MailResponseType.Deleted, MailResponseResult.Ok);
        }

        [WorldPacketHandler(ClientOpcodes.MailReturnToSender)]
        void HandleMailReturnToSender(MailReturnToSender returnToSender)
        {
            if (!CanOpenMailBox(_player.PlayerTalkClass.GetInteractionData().SourceGuid))
                return;

            Player player = GetPlayer();
            Mail m = player.GetMail(returnToSender.MailID);
            if (m == null || m.state == MailState.Deleted || m.deliver_time > LoopTime.ServerTime || m.sender != returnToSender.SenderGUID.GetCounter())
            {
                player.SendMailResult(returnToSender.MailID, MailResponseType.ReturnedToSender, MailResponseResult.InternalError);
                return;
            }
            //we can return mail now, so firstly delete the old one
            SQLTransaction trans = new();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_BY_ID);
            stmt.SetInt64(0, returnToSender.MailID);
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_ITEM_BY_ID);
            stmt.SetInt64(0, returnToSender.MailID);
            trans.Append(stmt);

            player.RemoveMail(returnToSender.MailID);

            // only return mail if the player exists (and delete if not existing)
            if (m.messageType == MailMessageType.Normal && m.sender != 0)
            {
                MailDraft draft = new(m.subject, m.body);
                if (m.mailTemplateId != 0)
                    draft = new MailDraft(m.mailTemplateId, false);     // items already included

                if (m.HasItems())
                {
                    foreach (var itemInfo in m.items)
                    {
                        Item item = player.GetMItem(itemInfo.item_guid);
                        if (item != null)
                            draft.AddItem(item);

                        player.RemoveMItem(itemInfo.item_guid);
                    }
                }
                draft.AddMoney(m.money).SendReturnToSender(GetAccountId(), m.receiver, m.sender, trans);
            }

            DB.Characters.CommitTransaction(trans);

            player.SendMailResult(returnToSender.MailID, MailResponseType.ReturnedToSender, MailResponseResult.Ok);
        }

        //called when player takes item attached in mail
        [WorldPacketHandler(ClientOpcodes.MailTakeItem)]
        void HandleMailTakeItem(MailTakeItem takeItem)
        {
            long AttachID = takeItem.AttachID;

            if (!CanOpenMailBox(takeItem.Mailbox))
                return;

            Player player = GetPlayer();

            Mail m = player.GetMail(takeItem.MailID);
            if (m == null || m.state == MailState.Deleted || m.deliver_time > LoopTime.ServerTime)
            {
                player.SendMailResult(takeItem.MailID, MailResponseType.ItemTaken, MailResponseResult.InternalError);
                return;
            }

            // verify that the mail has the item to avoid cheaters taking COD items without paying
            if (!m.items.Any(p => p.item_guid == AttachID))
            {
                player.SendMailResult(takeItem.MailID, MailResponseType.ItemTaken, MailResponseResult.InternalError);
                return;
            }

            // prevent cheating with skip client money check
            if (!player.HasEnoughMoney(m.COD))
            {
                player.SendMailResult(takeItem.MailID, MailResponseType.ItemTaken, MailResponseResult.NotEnoughMoney);
                return;
            }

            Item it = player.GetMItem(takeItem.AttachID);

            InventoryResult msg = GetPlayer().CanStoreItem(ItemPos.Undefined, out var dest, it);
            if (msg == InventoryResult.Ok)
            {
                SQLTransaction trans = new();
                m.RemoveItem(takeItem.AttachID);
                m.removedItems.Add(takeItem.AttachID);

                if (m.COD > 0)                                     //if there is COD, take COD money from player and send them to sender by mail
                {
                    ObjectGuid sender_guid = ObjectGuid.Create(HighGuid.Player, m.sender);
                    Player receiver = Global.ObjAccessor.FindPlayer(sender_guid);

                    int sender_accId = 0;

                    if (HasPermission(RBACPermissions.LogGmTrade))
                    {
                        string sender_name;
                        if (receiver != null)
                        {
                            sender_accId = receiver.GetSession().GetAccountId();
                            sender_name = receiver.GetName();
                        }
                        else
                        {
                            // can be calculated early
                            sender_accId = Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(sender_guid);

                            if (!Global.CharacterCacheStorage.GetCharacterNameByGuid(sender_guid, out sender_name))
                                sender_name = Global.ObjectMgr.GetCypherString(CypherStrings.Unknown);
                        }

                        Log.outCommand(GetAccountId(), 
                            $"GM {GetPlayerName()} (Account: {GetAccountId()}) " +
                            $"receiver mail item: {it.GetTemplate().GetName()} " +
                            $"(Entry: {it.GetEntry()} Count: {it.GetCount()}) and send COD money: {m.COD} " +
                            $"to player: {sender_name} (Account: {sender_accId})");
                    }
                    else if (receiver == null)
                        sender_accId = Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(sender_guid);

                    // check player existence
                    if (receiver != null || sender_accId != 0)
                    {
                        new MailDraft(m.subject, "")
                            .AddMoney(m.COD)
                            .SendMailTo(trans, new MailReceiver(receiver, m.sender), new MailSender(MailMessageType.Normal, m.receiver), MailCheckFlags.CodPayment);
                    }

                    player.ModifyMoney(-m.COD);
                }

                m.COD = 0;
                m.state = MailState.Changed;
                player.m_mailsUpdated = true;
                player.RemoveMItem(it.GetGUID().GetCounter());

                int count = it.GetCount();                      // save counts before store and possible merge with deleting
                it.SetState(ItemUpdateState.Unchanged);                       // need to set this state, otherwise item cannot be removed later, if neccessary
                player.MoveItemToInventory(dest, it, true);

                player.SaveInventoryAndGoldToDB(trans);
                player._SaveMail(trans);
                DB.Characters.CommitTransaction(trans);

                player.SendMailResult(takeItem.MailID, MailResponseType.ItemTaken, MailResponseResult.Ok, 0, takeItem.AttachID, count);
            }
            else
                player.SendMailResult(takeItem.MailID, MailResponseType.ItemTaken, MailResponseResult.EquipError, msg);
        }

        [WorldPacketHandler(ClientOpcodes.MailTakeMoney)]
        void HandleMailTakeMoney(MailTakeMoney takeMoney)
        {
            if (!CanOpenMailBox(takeMoney.Mailbox))
                return;

            Player player = GetPlayer();

            Mail m = player.GetMail(takeMoney.MailID);
            if (m == null || m.state == MailState.Deleted || m.deliver_time > LoopTime.ServerTime ||
                (takeMoney.Money > 0 && m.money != takeMoney.Money))
            {
                player.SendMailResult(takeMoney.MailID, MailResponseType.MoneyTaken, MailResponseResult.InternalError);
                return;
            }

            if (!player.ModifyMoney(m.money, false))
            {
                player.SendMailResult(takeMoney.MailID, MailResponseType.MoneyTaken, MailResponseResult.EquipError, InventoryResult.TooMuchGold);
                return;
            }

            m.money = 0;
            m.state = MailState.Changed;
            player.m_mailsUpdated = true;

            player.SendMailResult(takeMoney.MailID, MailResponseType.MoneyTaken, MailResponseResult.Ok);

            // save money and mail to prevent cheating
            SQLTransaction trans = new();
            player.SaveInventoryAndGoldToDB(trans);
            player._SaveMail(trans);
            DB.Characters.CommitTransaction(trans);
        }

        //called when player lists his received mails
        [WorldPacketHandler(ClientOpcodes.MailGetList)]
        void HandleGetMailList(MailGetList getList)
        {
            if (!CanOpenMailBox(getList.Mailbox))
                return;

            Player player = GetPlayer();

            var mails = player.GetMails();

            MailListResult response = new();

            foreach (Mail m in mails)
            {
                // skip deleted or not delivered (deliver delay not expired) mails
                if (m.state == MailState.Deleted || LoopTime.ServerTime < m.deliver_time)
                    continue;

                response.TotalNumRecords++;
                // max. 100 mails can be sent
                if (response.Mails.Count < 100)
                    response.Mails.Add(new MailListEntry(m, player));
            }

            player.PlayerTalkClass.GetInteractionData().Reset();
            player.PlayerTalkClass.GetInteractionData().SourceGuid = getList.Mailbox;
            SendPacket(response);

            // recalculate m_nextMailDelivereTime and unReadMails
            GetPlayer().UpdateNextMailTimeAndUnreads();
        }

        //used when player copies mail body to his inventory
        [WorldPacketHandler(ClientOpcodes.MailCreateTextItem)]
        void HandleMailCreateTextItem(MailCreateTextItem createTextItem)
        {
            if (!CanOpenMailBox(createTextItem.Mailbox))
                return;

            Player player = GetPlayer();

            Mail m = player.GetMail(createTextItem.MailID);
            if (m == null || (string.IsNullOrEmpty(m.body) && m.mailTemplateId == 0) || m.state == MailState.Deleted || m.deliver_time > LoopTime.ServerTime)
            {
                player.SendMailResult(createTextItem.MailID, MailResponseType.MadePermanent, MailResponseResult.InternalError);
                return;
            }

            Item bodyItem = Item.CreateItem(8383, 1, ItemContext.None, player);
            if (bodyItem == null)
                return;

            // in mail template case we need create new item text
            if (m.mailTemplateId != 0)
            {
                MailTemplateRecord mailTemplateEntry = CliDB.MailTemplateStorage.LookupByKey(m.mailTemplateId);
                if (mailTemplateEntry == null)
                {
                    player.SendMailResult(createTextItem.MailID, MailResponseType.MadePermanent, MailResponseResult.InternalError);
                    return;
                }

                bodyItem.SetText(mailTemplateEntry.Body[GetSessionDbcLocale()]);
            }
            else
                bodyItem.SetText(m.body);

            if (m.messageType == MailMessageType.Normal)
                bodyItem.SetCreator(ObjectGuid.Create(HighGuid.Player, m.sender));

            bodyItem.SetItemFlag(ItemFieldFlags.Readable);

            Log.outInfo(LogFilter.Network, 
                $"HandleMailCreateTextItem mailid={createTextItem.MailID}");

            InventoryResult msg = GetPlayer().CanStoreItem(ItemPos.Undefined, out var dest, bodyItem);
            if (msg == InventoryResult.Ok)
            {
                m.checkMask = m.checkMask | MailCheckFlags.Copied;
                m.state = MailState.Changed;
                player.m_mailsUpdated = true;

                player.StoreItem(dest, bodyItem, true);
                player.SendMailResult(createTextItem.MailID, MailResponseType.MadePermanent, MailResponseResult.Ok);
            }
            else
                player.SendMailResult(createTextItem.MailID, MailResponseType.MadePermanent, MailResponseResult.EquipError, msg);
        }

        [WorldPacketHandler(ClientOpcodes.QueryNextMailTime)]
        void HandleQueryNextMailTime(MailQueryNextMailTime queryNextMailTime)
        {
            MailQueryNextTimeResult result = new();

            if (GetPlayer().unReadMails > 0)
            {
                result.NextMailTime = 0.0f;

                List<long> sentSenders = new();

                foreach (Mail mail in GetPlayer().GetMails())
                {
                    if (mail.checkMask.HasAnyFlag(MailCheckFlags.Read))
                        continue;

                    // and already delivered
                    if (LoopTime.ServerTime < mail.deliver_time)
                        continue;

                    // only send each mail sender once
                    if (sentSenders.Any(p => p == mail.sender))
                        continue;

                    result.Next.Add(new MailQueryNextTimeResult.MailNextTimeEntry(mail));

                    sentSenders.Add(mail.sender);

                    // do not send more than 2 mails
                    if (sentSenders.Count > 2)
                        break;
                }
            }
            else
                result.NextMailTime = -Time.Day;

            SendPacket(result);
        }

        public void SendShowMailBox(ObjectGuid guid)
        {
            NPCInteractionOpenResult npcInteraction = new();
            npcInteraction.Npc = guid;
            npcInteraction.InteractionType = PlayerInteractionType.MailInfo;
            npcInteraction.Success = true;
            SendPacket(npcInteraction);
        }
    }
}
