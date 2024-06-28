// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Entities;
using Game.Networking;
using Game.Networking.Packets;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.MoveDismissVehicle, Processing = PacketProcessing.ThreadSafe)]
        void HandleMoveDismissVehicle(MoveDismissVehicle moveDismissVehicle)
        {
            ObjectGuid vehicleGUID = GetPlayer().GetCharmedGUID();
            if (vehicleGUID.IsEmpty())                                       // something wrong here...
                return;

            if (moveDismissVehicle.Status.Guid != vehicleGUID)
            {
                Log.outError(LogFilter.Network, 
                    $"Player {GetPlayer().GetGUID()} tried to dismiss a controlled vehicle ({vehicleGUID}) " +
                    $"that he has no control over. Possible cheater or malformed packet.");
                return;
            }

            GetPlayer().ExitVehicle();
        }

        [WorldPacketHandler(ClientOpcodes.RequestVehiclePrevSeat, Processing = PacketProcessing.Inplace)]
        void HandleRequestVehiclePrevSeat(RequestVehiclePrevSeat packet)
        {
            Unit vehicle_base = GetPlayer().GetVehicleBase();
            if (vehicle_base == null)
                return;

            VehicleSeatRecord seat = GetPlayer().GetVehicle().GetSeatForPassenger(GetPlayer());
            if (!seat.CanSwitchFromSeat)
            {
                Log.outError(LogFilter.Network, 
                    $"HandleRequestVehiclePrevSeat: {GetPlayer().GetGUID()} tried to switch seats " +
                    $"but current seatflags {seat.Flags} don't permit that.");
                return;
            }

            GetPlayer().ChangeSeat(-1, false);
        }

        [WorldPacketHandler(ClientOpcodes.RequestVehicleNextSeat, Processing = PacketProcessing.Inplace)]
        void HandleRequestVehicleNextSeat(RequestVehicleNextSeat packet)
        {
            Unit vehicle_base = GetPlayer().GetVehicleBase();
            if (vehicle_base == null)
                return;

            VehicleSeatRecord seat = GetPlayer().GetVehicle().GetSeatForPassenger(GetPlayer());
            if (!seat.CanSwitchFromSeat)
            {
                Log.outError(LogFilter.Network, 
                    $"HandleRequestVehicleNextSeat: {GetPlayer().GetGUID()} tried to switch seats " +
                    $"but current seatflags {seat.Flags} don't permit that.");
                return;
            }

            GetPlayer().ChangeSeat(-1, true);
        }

        [WorldPacketHandler(ClientOpcodes.MoveChangeVehicleSeats, Processing = PacketProcessing.ThreadSafe)]
        void HandleMoveChangeVehicleSeats(MoveChangeVehicleSeats packet)
        {
            Unit vehicle_base = GetPlayer().GetVehicleBase();
            if (vehicle_base == null)
                return;

            VehicleSeatRecord seat = GetPlayer().GetVehicle().GetSeatForPassenger(GetPlayer());
            if (!seat.CanSwitchFromSeat)
            {
                Log.outError(LogFilter.Network, 
                    $"HandleMoveChangeVehicleSeats, {GetPlayer().GetGUID()} tried to switch seats " +
                    $"but current seatflags {seat.Flags} don't permit that.");
                return;
            }

            GetPlayer().ValidateMovementInfo(packet.Status);

            if (vehicle_base.GetGUID() != packet.Status.Guid)
                return;

            vehicle_base.m_movementInfo = packet.Status;

            if (packet.DstVehicle.IsEmpty())
                GetPlayer().ChangeSeat(-1, packet.DstSeatIndex != 255);
            else
            {
                Unit vehUnit = Global.ObjAccessor.GetUnit(GetPlayer(), packet.DstVehicle);
                if (vehUnit != null)
                {
                    Vehicle vehicle = vehUnit.GetVehicleKit();
                    if (vehicle != null)
                    {
                        if (vehicle.HasEmptySeat((sbyte)packet.DstSeatIndex))
                            vehUnit.HandleSpellClick(GetPlayer(), (sbyte)packet.DstSeatIndex);
                }
            }
        }
        }

        [WorldPacketHandler(ClientOpcodes.RequestVehicleSwitchSeat, Processing = PacketProcessing.Inplace)]
        void HandleRequestVehicleSwitchSeat(RequestVehicleSwitchSeat packet)
        {
            Unit vehicle_base = GetPlayer().GetVehicleBase();
            if (vehicle_base == null)
                return;

            VehicleSeatRecord seat = GetPlayer().GetVehicle().GetSeatForPassenger(GetPlayer());
            if (!seat.CanSwitchFromSeat)
            {
                Log.outError(LogFilter.Network, 
                    $"HandleRequestVehicleSwitchSeat: {GetPlayer().GetGUID()} tried to switch seats " +
                    $"but current seatflags {seat.Flags} don't permit that.");
                return;
            }

            if (vehicle_base.GetGUID() == packet.Vehicle)
                GetPlayer().ChangeSeat((sbyte)packet.SeatIndex);
            else
            {
                Unit vehUnit = Global.ObjAccessor.GetUnit(GetPlayer(), packet.Vehicle);
                if (vehUnit != null)
                {
                    Vehicle vehicle = vehUnit.GetVehicleKit();
                    if (vehicle != null)
                    {
                        if (vehicle.HasEmptySeat((sbyte)packet.SeatIndex))
                            vehUnit.HandleSpellClick(GetPlayer(), (sbyte)packet.SeatIndex);
                }
            }
        }
        }

        [WorldPacketHandler(ClientOpcodes.RideVehicleInteract)]
        void HandleRideVehicleInteract(RideVehicleInteract packet)
        {
            Player player = Global.ObjAccessor.GetPlayer(_player, packet.Vehicle);
            if (player != null)
            {
                if (player.GetVehicleKit() == null)
                    return;
                if (!player.IsInRaidWith(GetPlayer()))
                    return;
                if (!player.IsWithinDistInMap(GetPlayer(), SharedConst.InteractionDistance))
                    return;
                // Dont' allow players to enter player vehicle on arena
                if (_player.GetMap() == null || _player.GetMap().IsBattleArena())
                    return;

                GetPlayer().EnterVehicle(player);
            }
        }

        [WorldPacketHandler(ClientOpcodes.EjectPassenger)]
        void HandleEjectPassenger(EjectPassenger packet)
        {
            Vehicle vehicle = GetPlayer().GetVehicleKit();
            if (vehicle == null)
            {
                Log.outError(LogFilter.Network, 
                    $"HandleEjectPassenger: {GetPlayer().GetGUID()} is not in a vehicle!");
                return;
            }

            if (packet.Passenger.IsUnit())
            {
                Unit unit = Global.ObjAccessor.GetUnit(GetPlayer(), packet.Passenger);
                if (unit == null)
                {
                    Log.outError(LogFilter.Network, 
                        $"{GetPlayer().GetGUID()} tried to eject {packet.Passenger} " +
                        $"from vehicle, but the latter was not found in world!");
                    return;
                }

                if (!unit.IsOnVehicle(vehicle.GetBase()))
                {
                    Log.outError(LogFilter.Network, 
                        $"{GetPlayer().GetGUID()} tried to eject {packet.Passenger}," +
                        $" but they are not in the same vehicle");
                    return;
                }

                VehicleSeatRecord seat = vehicle.GetSeatForPassenger(unit);
                Cypher.Assert(seat != null);
                if (seat.IsEjectable)
                    unit.ExitVehicle();
                else
                {
                    Log.outError(LogFilter.Network, 
                        $"{GetPlayer().GetGUID()} attempted to eject {packet.Passenger} from non-ejectable seat.");
            }
            }
            else
            {
                Log.outError(LogFilter.Network,
                    $"HandleEjectPassenger: {GetPlayer().GetGUID()} tried to eject invalid {packet.Passenger}");
            }
        }

        [WorldPacketHandler(ClientOpcodes.RequestVehicleExit, Processing = PacketProcessing.Inplace)]
        void HandleRequestVehicleExit(RequestVehicleExit packet)
        {
            Vehicle vehicle = GetPlayer().GetVehicle();
            if (vehicle != null)
            {
                VehicleSeatRecord seat = vehicle.GetSeatForPassenger(GetPlayer());
                if (seat != null)
                {
                    if (seat.CanEnterOrExit)
                        GetPlayer().ExitVehicle();
                    else
                    {
                        Log.outError(LogFilter.Network,
                            $"{GetPlayer().GetGUID()} tried to exit vehicle, " +
                            $"but seatflags {seat.Flags} (ID: {seat.Id}) don't permit that.");
                    }
                }
            }
        }

        [WorldPacketHandler(ClientOpcodes.MoveSetVehicleRecIdAck)]
        void HandleMoveSetVehicleRecAck(MoveSetVehicleRecIdAck setVehicleRecIdAck)
        {
            GetPlayer().ValidateMovementInfo(setVehicleRecIdAck.Data.Status);
        }
    }
}
