// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;
using System.Collections.Generic;
using System.Linq;

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
            foreach (var talentInfo in packet.talentInfos)
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
            if (rcEntry == null || !rcEntry.HasFlag(SkillRaceClassInfoFlags.Unlearnable))
                return;

            GetPlayer().SetSkill(packet.SkillLine, 0, 0, 0);
        }

        [WorldPacketHandler(ClientOpcodes.TradeSkillSetFavorite, Processing = PacketProcessing.Inplace)]
        void HandleTradeSkillSetFavorite(TradeSkillSetFavorite tradeSkillSetFavorite)
        {
            if (!_player.HasSpell(tradeSkillSetFavorite.RecipeID))
                return;

            _player.SpellBook.SetFavorite(tradeSkillSetFavorite.RecipeID, tradeSkillSetFavorite.IsFavorite);
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

        [WorldPacketHandler(ClientOpcodes.ShowTradeSkill, Processing = PacketProcessing.ThreadUnsafe)]
        void HandleShowTradeSkill(ShowTradeSkill packet)
        {
            SkillLineRecord skillEntry = CliDB.SkillLineStorage.LookupByKey(packet.SkillId);
            if (skillEntry == null || !skillEntry.CanLink)
                return;

            var spellLearnSkill = Global.SpellMgr.GetSpellLearnSkill(packet.SpellId);

            if (spellLearnSkill.skill != packet.SkillId)
                return;

            ShowTradeSkillResponse response = new()
            {
                SpellId = packet.SpellId,
                CasterGUID = packet.CasterGUID,
                SkillLineId = spellLearnSkill.skill,
                SkillMaxRank = spellLearnSkill.step * PlayerConst.ProfessionSkillPerStep,
            };

            if (Global.ObjAccessor.FindPlayer(packet.CasterGUID) is Player master)
            {
                response.SkillRank = master.GetSkillValue(spellLearnSkill.skill);
                if (response.SkillRank < 1)
                    return;

                response.KnownAbilitySpellIDs = master.SpellBook.GetTradeSkillSpells(packet.SkillId).ToList();
                GetPlayer().SendPacket(response);
            }
            else
            {
                long mastersLowGuid = packet.CasterGUID.GetCounter();
                int skillValue = 0;
                int skillMaxValue = 0;

                PreparedStatement stmt;
                SQLResult result;

                Log.outDebug(LogFilter.Sql, $"Loading current and max values for player's [GUID: {mastersLowGuid}] {packet.SkillId} ...");
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHARACTER_SKILL_VALUES);
                stmt.SetInt64(0, mastersLowGuid);
                stmt.SetInt16(1, (short)packet.SkillId);

                _queryProcessor.AddCallback(DB.Characters.AsyncQuery(stmt).WithChainingCallback((queryCallback, result) =>
                {
                    if (result.IsEmpty())
                        return;

                    skillValue = result.Read<short>(0);
                    skillMaxValue = result.Read<short>(1);
                    result.Close();

                    if (response.SkillMaxRank != skillMaxValue)
                        return;

                    if (skillValue < 1 || skillValue > skillMaxValue)
                        return;

                    response.SkillRank = skillValue;
                    response.SkillMaxRank = skillMaxValue;

                    Log.outDebug(LogFilter.Sql, $"Loading Trade Skill Spells for player's [GUID: {mastersLowGuid}] {packet.SkillId} ...");
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHARACTER_TRADE_SKILL_SPELLS);
                    stmt.SetInt64(0, mastersLowGuid);
                    stmt.SetInt16(1, (short)packet.SkillId);
                    queryCallback.SetNextQuery(DB.Characters.AsyncQuery(stmt));

                }).WithChainingCallback((queryCallback, result) =>
                {
                    if (result.IsEmpty())
                        return;

                    response.KnownAbilitySpellIDs = new();
                    do
                    {
                        response.KnownAbilitySpellIDs.Add(result.Read<int>(0));
                    }
                    while (result.NextRow());

                    GetPlayer().SendPacket(response);
                }));                
            }            
        }
    }
}
