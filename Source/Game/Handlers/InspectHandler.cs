// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Guilds;
using Game.Networking;
using Game.Networking.Packets;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.Inspect, Processing = PacketProcessing.Inplace)]
        void HandleInspect(Inspect inspect)
        {
            Player player = Global.ObjAccessor.GetPlayer(_player, inspect.Target);
            if (player == null)
            {
                Log.outDebug(LogFilter.Network, $"WorldSession.HandleInspectOpcode: Target {inspect.Target} not found.");
                return;
            }

            if (!GetPlayer().IsWithinDistInMap(player, SharedConst.InspectDistance, false))
                return;

            if (GetPlayer().IsValidAttackTarget(player))
                return;

            InspectResult inspectResult = new();
            inspectResult.DisplayInfo.Initialize(player);

            if (GetPlayer().CanBeGameMaster() || WorldConfig.GetIntValue(WorldCfg.TalentsInspecting) + (GetPlayer().GetEffectiveTeam() == player.GetEffectiveTeam() ? 1 : 0) > 1)
            {
                var talents = player.GetPlayerTalents(player.GetActiveTalentGroup());
                foreach (var v in talents)
                {
                    if (v.Value.State != PlayerSpellState.Removed)
                        inspectResult.Talents.Add(v.Key);
                }

                inspectResult.TalentTraits.Level = player.GetLevel();
                inspectResult.TalentTraits.ChrSpecializationID = player.GetPrimarySpecialization();
                TraitConfig traitConfig = player.GetTraitConfig(player.m_activePlayerData.ActiveCombatTraitConfigID);
                if (traitConfig != null)
                    inspectResult.TalentTraits.Config = new TraitConfigPacket(traitConfig);
            }

            Guild guild = Global.GuildMgr.GetGuildById(player.GetGuildId());
            if (guild != null)
            {
                InspectGuildData guildData;
                guildData.GuildGUID = guild.GetGUID();
                guildData.NumGuildMembers = guild.GetMembersCount();
                guildData.AchievementPoints = guild.GetAchievementMgr().GetAchievementPoints();

                inspectResult.GuildData = guildData;
            }

            inspectResult.ItemLevel = player.GetAverageItemLevel();
            inspectResult.LifetimeMaxRank = player.m_activePlayerData.LifetimeMaxRank;
            inspectResult.TodayHK = player.m_activePlayerData.TodayHonorableKills;
            inspectResult.YesterdayHK = player.m_activePlayerData.YesterdayHonorableKills;
            inspectResult.LifetimeHK = player.m_activePlayerData.LifetimeHonorableKills;
            inspectResult.HonorLevel = player.m_playerData.HonorLevel.GetValue();

            SendPacket(inspectResult);
        }

        [WorldPacketHandler(ClientOpcodes.QueryInspectAchievements, Processing = PacketProcessing.Inplace)]
        void HandleQueryInspectAchievements(QueryInspectAchievements inspect)
        {
            Player player = Global.ObjAccessor.GetPlayer(_player, inspect.Guid);
            if (player == null)
            {
                Log.outDebug(LogFilter.Network, 
                    $"WorldSession.HandleQueryInspectAchievements: " +
                    $"[{GetPlayer().GetGUID()}] inspected unknown Player [{inspect.Guid}]");

                return;
            }

            if (!GetPlayer().IsWithinDistInMap(player, SharedConst.InspectDistance, false))
                return;

            if (GetPlayer().IsValidAttackTarget(player))
                return;

            player.SendRespondInspectAchievements(GetPlayer());
        }
    }
}
