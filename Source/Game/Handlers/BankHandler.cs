// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;
using System.Collections.Generic;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.AutobankItem, Processing = PacketProcessing.Inplace)]
        void HandleAutoBankItem(AutoBankItem packet)
        {
            if (!CanUseBank())
            {
                Log.outDebug(LogFilter.Network, $"WORLD: HandleAutoBankItemOpcode - {_player.PlayerTalkClass.GetInteractionData().SourceGuid} not found or you can't interact with him.");
                return;
            }

            ItemPos itemPos = new(packet.Slot, packet.Bag);
            Item item = GetPlayer().GetItemByPos(itemPos);
            if (!item)
                return;

            InventoryResult msg = GetPlayer().CanBankItem(ItemPos.Undefined, out List<ItemPosCount> dest, item, false);
            if (msg != InventoryResult.Ok)
            {
                GetPlayer().SendEquipError(msg, item);
                return;
            }

            if (dest.Count == 1 && dest[0].Pos == item.InventoryPosition)
            {
                GetPlayer().SendEquipError(InventoryResult.CantSwap, item);
                return;
            }

            GetPlayer().RemoveItem(itemPos, true);
            GetPlayer().ItemRemovedQuestCheck(item.GetEntry(), item.GetCount());
            GetPlayer().BankItem(dest, item, true);
        }

        [WorldPacketHandler(ClientOpcodes.BankerActivate, Processing = PacketProcessing.Inplace)]
        void HandleBankerActivate(Hello packet)
        {
            Creature unit = GetPlayer().GetNPCIfCanInteractWith(packet.Unit, NPCFlags.Banker, NPCFlags2.None);
            if (!unit)
            {
                Log.outError(LogFilter.Network, "HandleBankerActivate: {0} not found or you can not interact with him.", packet.Unit.ToString());
                return;
            }

            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            SendShowBank(packet.Unit);
        }

        [WorldPacketHandler(ClientOpcodes.AutostoreBankItem, Processing = PacketProcessing.Inplace)]
        void HandleAutoStoreBankItem(AutoStoreBankItem packet)
        {
            if (!CanUseBank())
            {
                Log.outDebug(LogFilter.Network, $"WORLD: HandleAutoBankItemOpcode - {_player.PlayerTalkClass.GetInteractionData().SourceGuid} not found or you can't interact with him.");
                return;
            }

            ItemPos itemPos = new(packet.Slot, packet.Bag);
            Item item = GetPlayer().GetItemByPos(itemPos);
            if (!item)
                return;

            if (itemPos.IsBankPos)                 // moving from bank to inventory
            {
                InventoryResult msg = GetPlayer().CanStoreItem(ItemPos.Undefined, Player.ItemStoringRule.IncludePosition, out List<ItemPosCount> dest, item);
                if (msg != InventoryResult.Ok)
                {
                    GetPlayer().SendEquipError(msg, item);
                    return;
                }

                GetPlayer().RemoveItem(itemPos, true);
                Item storedItem = GetPlayer().StoreItem(dest, item, true);
                if (storedItem)
                    GetPlayer().ItemAddedQuestCheck(storedItem.GetEntry(), storedItem.GetCount());
            }
            else                                                    // moving from inventory to bank
            {
                InventoryResult msg = GetPlayer().CanBankItem(ItemPos.Undefined, out List<ItemPosCount> dest, item, false);
                if (msg != InventoryResult.Ok)
                {
                    GetPlayer().SendEquipError(msg, item);
                    return;
                }

                GetPlayer().RemoveItem(itemPos, true);
                GetPlayer().BankItem(dest, item, true);
            }
        }

        [WorldPacketHandler(ClientOpcodes.BuyBankSlot, Processing = PacketProcessing.Inplace)]
        void HandleBuyBankSlot(BuyBankSlot packet)
        {
            if (!CanUseBank(packet.Guid))
            {
                Log.outDebug(LogFilter.Network, "WORLD: HandleBuyBankSlot - {0} not found or you can't interact with him.", packet.Guid.ToString());
                return;
            }

            uint slot = GetPlayer().GetBankBagSlotCount();
            // next slot
            ++slot;

            BankBagSlotPricesRecord slotEntry = CliDB.BankBagSlotPricesStorage.LookupByKey(slot);
            if (slotEntry == null)
                return;

            uint price = slotEntry.Cost;
            if (!GetPlayer().HasEnoughMoney(price))
                return;

            GetPlayer().SetBankBagSlotCount((byte)slot);
            GetPlayer().ModifyMoney(-price);
            GetPlayer().UpdateCriteria(CriteriaType.BankSlotsPurchased);
        }

        public void SendShowBank(ObjectGuid guid)
        {
            _player.PlayerTalkClass.GetInteractionData().Reset();
            _player.PlayerTalkClass.GetInteractionData().SourceGuid = guid;
            ShowBank packet = new();
            packet.Guid = guid;
            SendPacket(packet);
        }
    }
}
