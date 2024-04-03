// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    public class AcceptTrade : ClientPacket
    {
        public AcceptTrade(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            StateIndex = _worldPacket.ReadUInt32();
        }

        public uint StateIndex;
    }

    public class BeginTrade : ClientPacket
    {
        public BeginTrade(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class BusyTrade : ClientPacket
    {
        public BusyTrade(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class CancelTrade : ClientPacket
    {
        public CancelTrade(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class ClearTradeItem : ClientPacket
    {
        public ClearTradeItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            TradeSlot = _worldPacket.ReadUInt8();
        }

        public byte TradeSlot;
    }

    public class IgnoreTrade : ClientPacket
    {
        public IgnoreTrade(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class InitiateTrade : ClientPacket
    {
        public InitiateTrade(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Guid = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Guid;
    }

    public class SetTradeCurrency : ClientPacket
    {
        public SetTradeCurrency(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Type = _worldPacket.ReadInt32();
            Quantity = _worldPacket.ReadInt32();
        }

        public int Type;
        public int Quantity;
    }

    public class SetTradeGold : ClientPacket
    {
        public SetTradeGold(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Coinage = _worldPacket.ReadInt64();
        }

        public long Coinage;
    }

    public class SetTradeItem : ClientPacket
    {
        public SetTradeItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            TradeSlot = _worldPacket.ReadUInt8();
            PackSlot = _worldPacket.ReadUInt8();
            ItemSlotInPack = _worldPacket.ReadUInt8();
        }

        public byte TradeSlot;
        public byte PackSlot;
        public byte ItemSlotInPack;
    }

    public class UnacceptTrade : ClientPacket
    {
        public UnacceptTrade(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class TradeStatusPkt : ServerPacket
    {
        public TradeStatusPkt() : base(ServerOpcodes.TradeStatus, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteBit(PartnerIsSameBnetAccount);
            _worldPacket.WriteBits((int)Status, 5);
            switch (Status)
            {
                case TradeStatus.Failed:
                    _worldPacket.WriteBit(FailureForYou);
                    _worldPacket.WriteInt32((int)BagResult);
                    _worldPacket.WriteInt32(ItemID);
                    break;
                case TradeStatus.Initiated:
                    _worldPacket.WriteUInt32(Id);
                    break;
                case TradeStatus.Proposed:
                    _worldPacket.WritePackedGuid(Partner);
                    _worldPacket.WritePackedGuid(PartnerAccount);
                    break;
                case TradeStatus.WrongRealm:
                case TradeStatus.NotOnTaplist:
                    _worldPacket.WriteUInt8(TradeSlot);
                    break;
                case TradeStatus.NotEnoughCurrency:
                case TradeStatus.CurrencyNotTradable:
                    _worldPacket.WriteInt32(CurrencyType);
                    _worldPacket.WriteInt32(CurrencyQuantity);
                    break;
                default:
                    _worldPacket.FlushBits();
                    break;
            }
        }

        public TradeStatus Status = TradeStatus.Initiated;
        public byte TradeSlot;
        public ObjectGuid PartnerAccount;
        public ObjectGuid Partner;
        public int CurrencyType;
        public int CurrencyQuantity;
        public bool FailureForYou;
        public InventoryResult BagResult;
        public int ItemID;
        public uint Id;
        public bool PartnerIsSameBnetAccount;
    }

    public class TradeUpdated : ServerPacket
    {
        public TradeUpdated() : base(ServerOpcodes.TradeUpdated, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteUInt8(WhichPlayer);
            _worldPacket.WriteInt32(Id);
            _worldPacket.WriteInt32(ClientStateIndex);
            _worldPacket.WriteInt32(CurrentStateIndex);
            _worldPacket.WriteInt64(Gold);
            _worldPacket.WriteInt32(CurrencyType);
            _worldPacket.WriteInt32(CurrencyQuantity);
            _worldPacket.WriteInt32(ProposedEnchantment);
            _worldPacket.WriteInt32(Items.Count);

            foreach (var item in Items)
                item.Write(_worldPacket);
        }

        public class UnwrappedTradeItem
        {
            public void Write(WorldPacket data)
            {
                data.WriteInt32(EnchantID);
                data.WriteInt32(OnUseEnchantmentID);
                data.WritePackedGuid(Creator);
                data.WriteInt32(Charges);
                data.WriteInt32(MaxDurability);
                data.WriteInt32(Durability);
                data.WriteBits(Gems.Count, 2);
                data.WriteBit(Lock);
                data.FlushBits();

                foreach (var gem in Gems)
                    gem.Write(data);
            }

            public ItemInstance Item;
            public int EnchantID;
            public int OnUseEnchantmentID;
            public ObjectGuid Creator;
            public int Charges;
            public bool Lock;
            public int MaxDurability;
            public int Durability;
            public List<ItemGemData> Gems = new();
        }

        public class TradeItem
        {
            public void Write(WorldPacket data)
            {
                data.WriteUInt8(Slot);
                data.WriteInt32(StackCount);
                data.WritePackedGuid(GiftCreator);
                Item.Write(data);
                data.WriteBit(Unwrapped != null);
                data.FlushBits();

                if (Unwrapped != null)
                    Unwrapped.Write(data);
            }

            public byte Slot;
            public ItemInstance Item = new();
            public int StackCount;
            public ObjectGuid GiftCreator;
            public UnwrappedTradeItem Unwrapped;
        }

        public long Gold;
        public int CurrentStateIndex;
        public byte WhichPlayer;
        public int ClientStateIndex;
        public List<TradeItem> Items = new();
        public int CurrencyType;
        public int Id;
        public int ProposedEnchantment;
        public int CurrencyQuantity;
    }
}