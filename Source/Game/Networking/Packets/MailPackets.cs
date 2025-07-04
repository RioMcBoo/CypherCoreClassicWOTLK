﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Mails;
using System;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    public class MailGetList : ClientPacket
    {
        public MailGetList(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Mailbox = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Mailbox;
    }

    public class MailListResult : ServerPacket
    {
        public MailListResult() : base(ServerOpcodes.MailListResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Mails.Count);
            _worldPacket.WriteInt32(TotalNumRecords);
            foreach(var mail in Mails)
                mail.Write(_worldPacket);
        }

        public int TotalNumRecords;
        public List<MailListEntry> Mails = new();
    }

    public class MailCreateTextItem : ClientPacket
    {
        public MailCreateTextItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Mailbox = _worldPacket.ReadPackedGuid();
            MailID = _worldPacket.ReadInt64();
        }

        public ObjectGuid Mailbox;
        public long MailID;
    }

    public class SendMail : ClientPacket
    {
        public SendMail(WorldPacket packet) : base(packet)
        {
            Info = new StructSendMail();
        }

        public override void Read()
        {
            Info.Mailbox = _worldPacket.ReadPackedGuid();
            Info.StationeryID = _worldPacket.ReadInt32();
            Info.SendMoney = _worldPacket.ReadInt64();
            Info.Cod = _worldPacket.ReadInt64();

            int targetLength = _worldPacket.ReadBits<int>(9);
            int subjectLength = _worldPacket.ReadBits<int>(9);
            int bodyLength = _worldPacket.ReadBits<int>(11);

            int count = _worldPacket.ReadBits<int>(5);

            Info.Target = _worldPacket.ReadString(targetLength);
            Info.Subject = _worldPacket.ReadString(subjectLength);
            Info.Body = _worldPacket.ReadString(bodyLength);

            for (var i = 0; i < count; ++i)
            {
                var att = new StructSendMail.MailAttachment()
                {
                    AttachPosition = _worldPacket.ReadUInt8(),
                    ItemGUID = _worldPacket.ReadPackedGuid()
                };

                Info.Attachments.Add(att);
            }
        }

        public StructSendMail Info;

        public class StructSendMail
        {
            public ObjectGuid Mailbox;
            public int StationeryID;
            public long SendMoney;
            public long Cod;
            public string Target;
            public string Subject;
            public string Body;
            public List<MailAttachment> Attachments = new();

            public struct MailAttachment
            {
                public byte AttachPosition;
                public ObjectGuid ItemGUID;
            }
        }
    }

    public class MailCommandResult : ServerPacket
    {
        public MailCommandResult() : base(ServerOpcodes.MailCommandResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt64(MailID);
            _worldPacket.WriteInt32((int)Command);
            _worldPacket.WriteInt32((int)ErrorCode);
            _worldPacket.WriteInt32((int)BagResult);
            _worldPacket.WriteInt64(AttachID);
            _worldPacket.WriteInt32(QtyInInventory);
        }

        public long MailID;
        public MailResponseType Command;
        public MailResponseResult ErrorCode;
        public InventoryResult BagResult;
        public long AttachID;
        public int QtyInInventory;
    }

    public class MailReturnToSender : ClientPacket
    {
        public MailReturnToSender(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            MailID = _worldPacket.ReadInt64();
            SenderGUID = _worldPacket.ReadPackedGuid();
        }

        public long MailID;
        public ObjectGuid SenderGUID;
    }

    public class MailMarkAsRead : ClientPacket
    {
        public MailMarkAsRead(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Mailbox = _worldPacket.ReadPackedGuid();
            MailID = _worldPacket.ReadInt64();
        }

        public ObjectGuid Mailbox;
        public long MailID;
    }

    public class MailDelete : ClientPacket
    {
        public MailDelete(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            MailID = _worldPacket.ReadInt64();
            DeleteReason = _worldPacket.ReadInt32();
        }

        public long MailID;
        public int DeleteReason;
    }

    public class MailTakeItem : ClientPacket
    {
        public MailTakeItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Mailbox = _worldPacket.ReadPackedGuid();
            MailID = _worldPacket.ReadInt64();
            AttachID = _worldPacket.ReadInt64();
        }

        public ObjectGuid Mailbox;
        public long MailID;
        public long AttachID;
    }

    public class MailTakeMoney : ClientPacket
    {
        public MailTakeMoney(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Mailbox = _worldPacket.ReadPackedGuid();
            MailID = _worldPacket.ReadInt64();
            Money = _worldPacket.ReadInt64();
        }

        public ObjectGuid Mailbox;
        public long MailID;
        public long Money;
    }

    public class MailQueryNextMailTime : ClientPacket
    {
        public MailQueryNextMailTime(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class MailQueryNextTimeResult : ServerPacket
    {
        public MailQueryNextTimeResult() : base(ServerOpcodes.MailQueryNextTimeResult)
        {
            Next = new List<MailNextTimeEntry>();
        }

        public override void Write()
        {
            _worldPacket.WriteFloat(NextMailTime);
            _worldPacket.WriteInt32(Next.Count);

            foreach (var entry in Next)
            {
                _worldPacket.WritePackedGuid(entry.SenderGuid);
                _worldPacket.WriteFloat((float)entry.TimeLeft.TotalSeconds);
                _worldPacket.WriteInt32(entry.AltSenderID);
                _worldPacket.WriteInt8(entry.AltSenderType);
                _worldPacket.WriteInt32(entry.StationeryID);
            }
        }

        public float NextMailTime;
        public List<MailNextTimeEntry> Next;

        public class MailNextTimeEntry
        {
            public MailNextTimeEntry(Mail mail)
            {
                switch (mail.messageType)
                {
                    case MailMessageType.Normal:
                        SenderGuid = ObjectGuid.Create(HighGuid.Player, mail.sender);
                        break;
                    case MailMessageType.Auction:
                    case MailMessageType.Creature:
                    case MailMessageType.Gameobject:
                    case MailMessageType.Calendar:
                    case MailMessageType.Blackmarket:
                    case MailMessageType.CommerceAuction:
                    case MailMessageType.Auction2:
                    case MailMessageType.ArtisansConsortium:
                        AltSenderID = (int)mail.sender;
                        break;
                }

                TimeLeft = mail.deliver_time - LoopTime.ServerTime;
                AltSenderType = (sbyte)mail.messageType;
                StationeryID = (int)mail.stationery;
            }

            public ObjectGuid SenderGuid;
            public TimeSpan TimeLeft;
            public int AltSenderID;
            public sbyte AltSenderType;
            public int StationeryID;
        }
    }

    public class NotifyReceivedMail : ServerPacket
    {
        public NotifyReceivedMail() : base(ServerOpcodes.NotifyReceivedMail) { }

        public override void Write()
        {
            _worldPacket.WriteFloat(Delay);
        }

        public float Delay = 0.0f;
    }

    //Structs
    public class MailAttachedItem
    {
        public MailAttachedItem(Item item, byte pos)
        {
            Position = pos;
            AttachID = item.GetGUID().GetCounter();
            Item = new ItemInstance(item);
            Count = item.GetCount();
            Charges = item.GetSpellCharges();
            MaxDurability = item.m_itemData.MaxDurability;
            Durability = item.m_itemData.Durability;
            Unlocked = !item.IsLocked();

            for (EnchantmentSlot slot = 0; slot < EnchantmentSlot.EnhancementMax; slot++)
            {
                if (item.GetEnchantmentId(slot) == 0)
                    continue;

                Enchants.Add(new ItemEnchantData(item.GetEnchantmentId(slot), item.GetEnchantmentDuration(slot), item.GetEnchantmentCharges(slot), (byte)slot));
            }

            byte i = 0;
            foreach (SocketedGem gemData in item.m_itemData.Gems)
            {
                if (gemData.ItemId != 0)
                {
                    ItemGemData gem = new();
                    gem.Slot = i;
                    gem.Item = new ItemInstance(gemData);
                    Gems.Add(gem);
                }
                ++i;
            }
        }

        public void Write(WorldPacket data)
        {
            data.WriteUInt8(Position);
            data.WriteInt64(AttachID);
            data.WriteInt32(Count);
            data.WriteInt32(Charges);
            data.WriteInt32(MaxDurability);
            data.WriteInt32(Durability);
            Item.Write(data);
            data.WriteBits(Enchants.Count, 4);
            data.WriteBits(Gems.Count, 2);
            data.WriteBit(Unlocked);
            data.FlushBits();

            foreach (ItemGemData gem in Gems)
                gem.Write(data);

            foreach (ItemEnchantData en in Enchants)
                en.Write(data);
        }

        public byte Position;
        public long AttachID;
        public ItemInstance Item;
        public int Count;
        public int Charges;
        public int MaxDurability;
        public int Durability;
        public bool Unlocked;
        List<ItemEnchantData> Enchants = new();
        List<ItemGemData> Gems= new();
    }

    public class MailListEntry
    {
        public MailListEntry(Mail mail, Player player)
        {
            MailID = mail.messageID;
            SenderType = mail.messageType;

            switch (mail.messageType)
            {
                case MailMessageType.Normal:
                    SenderCharacter = ObjectGuid.Create(HighGuid.Player, mail.sender);
                    break;
                case MailMessageType.Creature:
                case MailMessageType.Gameobject:
                case MailMessageType.Auction:
                case MailMessageType.Calendar:
                case MailMessageType.Blackmarket:
                case MailMessageType.CommerceAuction:
                case MailMessageType.Auction2:
                case MailMessageType.ArtisansConsortium:
                    AltSenderID = (int)mail.sender;
                    break;
            }

            Cod = mail.COD;
            StationeryID = (int)mail.stationery;
            SentMoney = mail.money;
            Flags = (int)mail.checkMask;
            DaysLeft = mail.expire_time - LoopTime.ServerTime;
            MailTemplateID = mail.mailTemplateId;
            Subject = mail.subject;
            Body = mail.body;

            for (byte i = 0; i < mail.items.Count; i++)
            {
                Item item = player.GetMItem(mail.items[i].item_guid);
                if (item != null)
                    Attachments.Add(new MailAttachedItem(item, i));
            }
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt64(MailID);
            data.WriteUInt32((uint)SenderType);
            data.WriteInt64(Cod);
            data.WriteInt32(StationeryID);
            data.WriteInt64(SentMoney);
            data.WriteInt32(Flags);
            data.WriteFloat((float)DaysLeft.TotalDays);
            data.WriteInt32(MailTemplateID);
            data.WriteInt32(Attachments.Count);

            switch (SenderType)
            {
                case MailMessageType.Normal:
                    data.WritePackedGuid(SenderCharacter);
                    break;
                case MailMessageType.Auction:
                case MailMessageType.Creature:
                case MailMessageType.Gameobject:
                case MailMessageType.Calendar:
                case MailMessageType.Blackmarket:
                case MailMessageType.CommerceAuction:
                case MailMessageType.Auction2:
                case MailMessageType.ArtisansConsortium:
                    data.WriteInt32(AltSenderID);
                    break;
                default:
                    break;
            }

            data.WriteBits(Subject.GetByteCount(), 8);
            data.WriteBits(Body.GetByteCount(), 13);
            data.FlushBits();

            foreach (var item in Attachments)
                item.Write(data);

            data.WriteString(Subject);
            data.WriteString(Body);
        }

        public long MailID;
        public MailMessageType SenderType;
        public ObjectGuid SenderCharacter;
        public int AltSenderID;
        public long Cod;
        public int StationeryID;
        public long SentMoney;
        public int Flags;
        public TimeSpan DaysLeft;
        public int MailTemplateID;
        public string Subject = string.Empty;
        public string Body = string.Empty;
        public List<MailAttachedItem> Attachments = new();
    }
}
