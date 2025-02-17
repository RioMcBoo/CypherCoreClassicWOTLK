﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;
using System;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.CanDuel)]
        void HandleCanDuel(CanDuel packet)
        {
            Player player = Global.ObjAccessor.FindPlayer(packet.TargetGUID);

            if (player == null)
                return;

            CanDuelResult response = new();
            response.TargetGUID = packet.TargetGUID;
            response.Result = player.duel == null;
            SendPacket(response);

            if (response.Result)
            {
                if (GetPlayer().IsMounted())
                    GetPlayer().CastSpell(player, 62875);
                else
                    GetPlayer().CastSpell(player, 7266);
            }
        }

        [WorldPacketHandler(ClientOpcodes.DuelResponse)]
        void HandleDuelResponse(DuelResponse duelResponse)
        {
            if (duelResponse.Accepted && !duelResponse.Forfeited)
                HandleDuelAccepted(duelResponse.ArbiterGUID);
            else
                HandleDuelCancelled();
        }

        void HandleDuelAccepted(ObjectGuid arbiterGuid)
        {
            Player player = GetPlayer();
            if (player.duel == null || player == player.duel.Initiator || player.duel.State != DuelState.Challenged)
                return;

            Player target = player.duel.Opponent;
            if (target.m_playerData.DuelArbiter != arbiterGuid)
                return;

            Log.outDebug(LogFilter.Network, $"Player 1 is: {player.GetGUID()} ({player.GetName()})");
            Log.outDebug(LogFilter.Network, $"Player 2 is: {target.GetGUID()} ({target.GetName()})");

            Seconds duelStartTime = (Seconds)3;
                        
            player.duel.StartTime = target.duel.StartTime = LoopTime.ServerTime + duelStartTime;
            player.duel.State = target.duel.State = DuelState.Countdown;

            DuelCountdown packet = new(duelStartTime);

            player.SendPacket(packet);
            target.SendPacket(packet);

            player.EnablePvpRules();
            target.EnablePvpRules();
        }

        void HandleDuelCancelled()
        {
            Player player = GetPlayer();

            // no duel requested
            if (player.duel == null || player.duel.State == DuelState.Completed)
                return;

            // player surrendered in a duel using /forfeit
            if (player.duel.State == DuelState.InProgress)
            {
                player.CombatStopWithPets(true);
                player.duel.Opponent.CombatStopWithPets(true);

                player.CastSpell(GetPlayer(), 7267, true);    // beg
                player.DuelComplete(DuelCompleteType.Won);
                return;
            }

            player.DuelComplete(DuelCompleteType.Interrupted);
        }
    }
}
