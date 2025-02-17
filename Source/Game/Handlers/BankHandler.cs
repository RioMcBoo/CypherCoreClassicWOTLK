﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
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

            if (itemPos.IsInventoryPos) // move only from inventory (not equippable slots)
            {
                InventoryResult msg = GetPlayer().CanBankItem(ItemPos.Undefined, out var dest, item);
                if (msg != InventoryResult.Ok)
                {
                    GetPlayer().SendEquipError(msg, item);
                    return;
                }

                GetPlayer().RemoveItem(itemPos, true);
                GetPlayer().ItemRemovedQuestCheck(item.GetEntry(), item.GetCount());
                GetPlayer().BankItem(dest, item, true);
            }
        }

        [WorldPacketHandler(ClientOpcodes.BankerActivate, Processing = PacketProcessing.Inplace)]
        void HandleBankerActivate(Hello packet)
        {
            Creature unit = GetPlayer().GetNPCIfCanInteractWith(packet.Unit, NPCFlags1.Banker, NPCFlags2.None);
            if (unit == null)
            {
                Log.outError(LogFilter.Network, $"HandleBankerActivate: {packet.Unit} not found or you can not interact with him.");
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

            if (itemPos.IsBankPos && !itemPos.IsBagSlotPos) // moving from bank to inventory (not equippable slots)
            {
                InventoryResult msg = GetPlayer().CanStoreItem(ItemPos.Undefined, out var dest, item);
                if (msg != InventoryResult.Ok)
                {
                    GetPlayer().SendEquipError(msg, item);
                    return;
                }

                GetPlayer().RemoveItem(itemPos, true);
                GetPlayer().ItemAddedQuestCheck(item.GetEntry(), item.GetCount());
                GetPlayer().StoreItem(dest, item, true);
                    
            }
        }

        [WorldPacketHandler(ClientOpcodes.BuyBankSlot, Processing = PacketProcessing.Inplace)]
        void HandleBuyBankSlot(BuyBankSlot packet)
        {
            if (!CanUseBank(packet.Guid))
            {
                Log.outDebug(LogFilter.Network, $"WORLD: HandleBuyBankSlot - {packet.Guid} not found or you can't interact with him.");
                return;
            }

            int slot = GetPlayer().GetBankBagSlotCount();
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
            NPCInteractionOpenResult npcInteraction = new();
            npcInteraction.Npc = guid;
            npcInteraction.InteractionType = PlayerInteractionType.Banker;
            npcInteraction.Success = true;
            SendPacket(npcInteraction);
        }
    }
}
