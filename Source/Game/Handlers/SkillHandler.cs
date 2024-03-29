// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.LearnTalent, Processing = PacketProcessing.Inplace)]
        void HandleLearnTalent(LearnTalent packet)
        {
            if (_player.LearnTalent(packet.TalentID, packet.Rank))
                _player.SendTalentsInfoData();
        }

        [WorldPacketHandler(ClientOpcodes.LearnPreviewTalents, Processing = PacketProcessing.Inplace)]
        void HandleLearnTalentsGroup(LearnPreviewTalents packet)
        {
            foreach(var talentInfo in packet.talentInfos)
            {
                _player.LearnTalent(talentInfo.TalentID, talentInfo.Rank);
            }
            _player.SendTalentsInfoData();
        }

        [WorldPacketHandler(ClientOpcodes.ConfirmRespecWipe)]
        void HandleConfirmRespecWipe(ConfirmRespecWipe confirmRespecWipe)
        {
            Creature unit = GetPlayer().GetNPCIfCanInteractWith(confirmRespecWipe.RespecMaster, NPCFlags1.Trainer, NPCFlags2.None);
            if (unit == null)
            {
                Log.outDebug(LogFilter.Network, "WORLD: HandleTalentWipeConfirm - {0} not found or you can't interact with him.", confirmRespecWipe.RespecMaster.ToString());
                return;
            }

            if (confirmRespecWipe.RespecType != SpecResetType.Talents)
            {
                Log.outDebug(LogFilter.Network, "WORLD: HandleConfirmRespecWipe - reset Type {0} is not implemented.", confirmRespecWipe.RespecType);
                return;
            }

            if (!unit.CanResetTalents(_player))
                return;

            // remove fake death
            if (GetPlayer().HasUnitState(UnitState.Died))
                GetPlayer().RemoveAurasByType(AuraType.FeignDeath);

            if (!GetPlayer().ResetTalents())
                return;

            GetPlayer().SendTalentsInfoData();
            unit.CastSpell(GetPlayer(), 14867, true);                  //spell: "Untalent Visual Effect"
        }

        [WorldPacketHandler(ClientOpcodes.UnlearnSkill, Processing = PacketProcessing.Inplace)]
        void HandleUnlearnSkill(UnlearnSkill packet)
        {
            SkillRaceClassInfoRecord rcEntry = Global.DB2Mgr.GetSkillRaceClassInfo((SkillType)packet.SkillLine, GetPlayer().GetRace(), GetPlayer().GetClass());
            if (rcEntry == null || !rcEntry.Flags.HasAnyFlag(SkillRaceClassInfoFlags.Unlearnable))
                return;

            GetPlayer().SetSkill(packet.SkillLine, 0, 0, 0);
        }

        [WorldPacketHandler(ClientOpcodes.TradeSkillSetFavorite, Processing = PacketProcessing.Inplace)]
        void HandleTradeSkillSetFavorite(TradeSkillSetFavorite tradeSkillSetFavorite)
        {
            if (!_player.HasSpell(tradeSkillSetFavorite.RecipeID))
                return;

            _player.SetSpellFavorite(tradeSkillSetFavorite.RecipeID, tradeSkillSetFavorite.IsFavorite);
        }

        [WorldPacketHandler(ClientOpcodes.RemoveGlyph)]
        void HandleRemoveGlyphOpcode(RemoveGlyph packet)
        {
            if (packet.GlyphSlot >= PlayerConst.MaxGlyphSlotIndex)
            {
                Log.outDebug(LogFilter.Network, $"Client sent wrong glyph slot number in opcode CMSG_REMOVE_GLYPH {packet.GlyphSlot}.");
                return;
            }

            var glyph = _player.GetGlyph(packet.GlyphSlot);
            if (glyph != 0)
            {
                if (CliDB.GlyphPropertiesStorage.TryGetValue(glyph, out GlyphPropertiesRecord gp))
                {
                    _player.RemoveAurasDueToSpell(gp.SpellID);
                    _player.SetGlyph(packet.GlyphSlot, 0);
                    _player.SendTalentsInfoData();
                }
            }
        }
    }
}
