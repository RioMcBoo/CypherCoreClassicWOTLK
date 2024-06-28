// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using Game.Entities;
using Game.Networking;
using Game.Movement;
using System.Numerics;

public static class PacketHandlerExtensions
{
    public static void Write(this EquipmentSetInfo.EquipmentSetData data, WorldPacket _worldPacket)
    {
        _worldPacket.WriteInt32((int)data.Type);
        _worldPacket.WriteInt64(data.Guid);
        _worldPacket.WriteInt32(data.SetID);
        _worldPacket.WriteUInt32(data.IgnoreMask);

        for (int i = 0; i < EquipmentSlot.End; ++i)
        {
            _worldPacket.WritePackedGuid(data.Pieces[i]);
            _worldPacket.WriteInt32(data.Appearances[i]);
        }

        foreach (var id in data.Enchants)
            _worldPacket.WriteInt32(id);

        _worldPacket.WriteInt32(data.SecondaryShoulderApparanceID);
        _worldPacket.WriteInt32(data.SecondaryShoulderSlot);
        _worldPacket.WriteInt32(data.SecondaryWeaponAppearanceID);
        _worldPacket.WriteInt32(data.SecondaryWeaponSlot);

        _worldPacket.WriteBit(data.AssignedSpecIndex != -1);
        _worldPacket.WriteBits(data.SetName.GetByteCount(), 8);
        _worldPacket.WriteBits(data.SetIcon.GetByteCount(), 9);

        if (data.AssignedSpecIndex != -1)
            _worldPacket.WriteInt32(data.AssignedSpecIndex);

        _worldPacket.WriteString(data.SetName);
        _worldPacket.WriteString(data.SetIcon);
    }

    public static void Read(this EquipmentSetInfo.EquipmentSetData data, WorldPacket _worldPacket)
    {
        data.Type = (EquipmentSetInfo.EquipmentSetType)_worldPacket.ReadInt32();
        data.Guid = _worldPacket.ReadInt64();
        data.SetID = _worldPacket.ReadInt32();
        data.IgnoreMask = _worldPacket.ReadUInt32();

        for (byte i = 0; i < EquipmentSlot.End; ++i)
        {
            data.Pieces[i] = _worldPacket.ReadPackedGuid();
            data.Appearances[i] = _worldPacket.ReadInt32();
        }

        data.Enchants[0] = _worldPacket.ReadInt32();
        data.Enchants[1] = _worldPacket.ReadInt32();

        data.SecondaryShoulderApparanceID = _worldPacket.ReadInt32();
        data.SecondaryShoulderSlot = _worldPacket.ReadInt32();
        data.SecondaryWeaponAppearanceID = _worldPacket.ReadInt32();
        data.SecondaryWeaponSlot = _worldPacket.ReadInt32();

        bool hasSpecIndex = _worldPacket.HasBit();

        int setNameLength = _worldPacket.ReadBits<int>(8);
        int setIconLength = _worldPacket.ReadBits<int>(9);

        if (hasSpecIndex)
            data.AssignedSpecIndex = _worldPacket.ReadInt32();

        data.SetName = _worldPacket.ReadString(setNameLength);
        data.SetIcon = _worldPacket.ReadString(setIconLength);
    }

    public static void Read(this MovementInfo.TransportInfo transportInfo, WorldPacket data)
    {
        transportInfo.guid = data.ReadPackedGuid();                 // Transport Guid
        transportInfo.pos.posX = data.ReadFloat();
        transportInfo.pos.posY = data.ReadFloat();
        transportInfo.pos.posZ = data.ReadFloat();
        transportInfo.pos.Orientation = data.ReadFloat();
        transportInfo.seat = data.ReadInt8();                 // VehicleSeatIndex
        transportInfo.time = data.ReadUInt32();                 // MoveTime

        bool hasPrevTime = data.HasBit();
        bool hasVehicleId = data.HasBit();

        if (hasPrevTime)
            transportInfo.prevTime = data.ReadUInt32();         // PrevMoveTime

        if (hasVehicleId)
            transportInfo.vehicleId = data.ReadInt32();        // VehicleRecID
    }

    public static void Write(this MovementInfo.TransportInfo transportInfo, WorldPacket data)
    {
        bool hasPrevTime = transportInfo.prevTime != 0;
        bool hasVehicleId = transportInfo.vehicleId != 0;

        data.WritePackedGuid(transportInfo.guid);                 // Transport Guid
        data.WriteFloat(transportInfo.pos.GetPositionX());
        data.WriteFloat(transportInfo.pos.GetPositionY());
        data.WriteFloat(transportInfo.pos.GetPositionZ());
        data.WriteFloat(transportInfo.pos.GetOrientation());
        data.WriteInt8(transportInfo.seat);                 // VehicleSeatIndex
        data.WriteUInt32(transportInfo.time);                 // MoveTime

        data.WriteBit(hasPrevTime);
        data.WriteBit(hasVehicleId);
        data.FlushBits();

        if (hasPrevTime)
            data.WriteUInt32(transportInfo.prevTime);         // PrevMoveTime

        if (hasVehicleId)
            data.WriteInt32(transportInfo.vehicleId);        // VehicleRecID
    }

    public static void Read(this MovementInfo movementInfo, WorldPacket data)
    {
        movementInfo.Guid = data.ReadPackedGuid();
        movementInfo.SetMovementFlags((MovementFlag)data.ReadUInt32());
        movementInfo.SetMovementFlags2((MovementFlag2)data.ReadUInt32());
        movementInfo.SetExtraMovementFlags2((MovementFlags3)data.ReadUInt32());
        movementInfo.Time = data.ReadUInt32();
        float x = data.ReadFloat();
        float y = data.ReadFloat();
        float z = data.ReadFloat();
        float o = data.ReadFloat();

        movementInfo.Pos.Relocate(x, y, z, o);
        movementInfo.Pitch = data.ReadFloat();
        movementInfo.stepUpStartElevation = data.ReadFloat();

        uint removeMovementForcesCount = data.ReadUInt32();

        uint moveIndex = data.ReadUInt32();

        for (uint i = 0; i < removeMovementForcesCount; ++i)
        {
            data.ReadPackedGuid();
        }

        bool hasStandingOnGameObjectGUID = data.HasBit();
        bool hasTransport = data.HasBit();
        bool hasFall = data.HasBit();
        bool hasSpline = data.HasBit(); // todo 6.x read this infos

        data.ReadBit(); // HeightChangeFailed
        data.ReadBit(); // RemoteTimeValid
        bool hasInertia = data.HasBit();
        bool hasAdvFlying = data.HasBit();

        if (hasTransport)
            movementInfo.transport.Read(data);

        if (hasStandingOnGameObjectGUID)
            movementInfo.standingOnGameObjectGUID = data.ReadPackedGuid();

        if (hasInertia)
        {
            MovementInfo.Inertia inertia = new();
            inertia.id = data.ReadInt32();
            inertia.force = data.ReadPosition();
            inertia.lifetime = data.ReadUInt32();

            movementInfo.inertia = inertia;
        }

        if (hasAdvFlying)
        {
            MovementInfo.AdvFlying advFlying = new();

            advFlying.forwardVelocity = data.ReadFloat();
            advFlying.upVelocity = data.ReadFloat();
            movementInfo.advFlying = advFlying;
        }

        if (hasFall)
        {
            movementInfo.jump.fallTime = data.ReadUInt32();
            movementInfo.jump.zspeed = data.ReadFloat();

            // ResetBitReader

            bool hasFallDirection = data.HasBit();
            if (hasFallDirection)
            {
                movementInfo.jump.sinAngle = data.ReadFloat();
                movementInfo.jump.cosAngle = data.ReadFloat();
                movementInfo.jump.xyspeed = data.ReadFloat();
            }
        }
    }

    public static void Write(this MovementInfo movementInfo, WorldPacket data)
    {
        bool hasTransportData = !movementInfo.transport.guid.IsEmpty();
        bool hasFallDirection = movementInfo.HasMovementFlag(MovementFlag.Falling | MovementFlag.FallingFar);
        bool hasFallData = hasFallDirection || movementInfo.jump.fallTime != 0;
        bool hasSpline = false; // todo 6.x send this infos
        bool hasInertia = movementInfo.inertia.HasValue;
        bool hasAdvFlying = movementInfo.advFlying.HasValue;
        bool hasStandingOnGameObjectGUID = movementInfo.standingOnGameObjectGUID.HasValue;

        data.WritePackedGuid(movementInfo.Guid);
        data.WriteUInt32((uint)movementInfo.GetMovementFlags());
        data.WriteUInt32((uint)movementInfo.GetMovementFlags2());
        data.WriteUInt32((uint)movementInfo.GetExtraMovementFlags2());
        data.WriteUInt32(movementInfo.Time);
        data.WriteFloat(movementInfo.Pos.GetPositionX());
        data.WriteFloat(movementInfo.Pos.GetPositionY());
        data.WriteFloat(movementInfo.Pos.GetPositionZ());
        data.WriteFloat(movementInfo.Pos.GetOrientation());
        data.WriteFloat(movementInfo.Pitch);
        data.WriteFloat(movementInfo.stepUpStartElevation);

        uint removeMovementForcesCount = 0;
        data.WriteUInt32(removeMovementForcesCount);

        uint moveIndex = 0;
        data.WriteUInt32(moveIndex);

        /*for (public uint i = 0; i < removeMovementForcesCount; ++i)
        {
            _worldPacket << ObjectGuid;
        }*/

        data.WriteBit(hasStandingOnGameObjectGUID);
        data.WriteBit(hasTransportData);
        data.WriteBit(hasFallData);
        data.WriteBit(hasSpline);
        data.WriteBit(false); // HeightChangeFailed
        data.WriteBit(false); // RemoteTimeValid
        data.WriteBit(hasInertia);
        data.WriteBit(hasAdvFlying);
        data.FlushBits();

        if (hasTransportData)
            movementInfo.transport.Write(data);

        if (hasStandingOnGameObjectGUID)
            data.WritePackedGuid(movementInfo.standingOnGameObjectGUID.Value);

        if (hasInertia)
        {
            data.WriteInt32(movementInfo.inertia.Value.id);
            data.WriteXYZ(movementInfo.inertia.Value.force);
            data.WriteUInt32(movementInfo.inertia.Value.lifetime);
        }

        if (hasAdvFlying)
        {
            data.WriteFloat(movementInfo.advFlying.Value.forwardVelocity);
            data.WriteFloat(movementInfo.advFlying.Value.upVelocity);
        }

        if (hasFallData)
        {
            data.WriteUInt32(movementInfo.jump.fallTime);
            data.WriteFloat(movementInfo.jump.zspeed);

            data.WriteBit(hasFallDirection);
            data.FlushBits();
            if (hasFallDirection)
            {
                data.WriteFloat(movementInfo.jump.sinAngle);
                data.WriteFloat(movementInfo.jump.cosAngle);
                data.WriteFloat(movementInfo.jump.xyspeed);
            }
        }
    }

    public static void Write(this MoveSpline moveSpline, WorldPacket data)
    {
        data.WriteInt32(moveSpline.GetId());                                         // ID

        if (!moveSpline.IsCyclic())                                                 // Destination
            data.WriteVector3(moveSpline.FinalDestination());
        else
            data.WriteVector3(Vector3.Zero);

        bool hasSplineMove = data.WriteBit(!moveSpline.Finalized() && !moveSpline.splineIsFacingOnly);
        data.FlushBits();

        if (hasSplineMove)
        {
            data.WriteUInt32((uint)moveSpline.splineflags.Flags);   // SplineFlags
            data.WriteInt32(moveSpline.TimePassed());               // Elapsed
            data.WriteInt32(moveSpline.Duration());                // Duration
            data.WriteFloat(1.0f);                                  // DurationModifier
            data.WriteFloat(1.0f);                                  // NextDurationModifier
            data.WriteBits((byte)moveSpline.facing.type, 2);        // Face

            bool hasFadeObjectTime = 
                data.WriteBit(moveSpline.splineflags.HasFlag(SplineFlag.FadeObject) 
                && moveSpline.effect_start_time < moveSpline.Duration());

            data.WriteBits(moveSpline.GetPath().Length, 16);
            data.WriteBit(false);                                       // HasSplineFilter
            data.WriteBit(moveSpline.spell_effect_extra != null);  // HasSpellEffectExtraData

            bool hasJumpExtraData = 
                data.WriteBit(moveSpline.splineflags.HasFlag(SplineFlag.Parabolic) 
                && (moveSpline.spell_effect_extra == null || moveSpline.effect_start_time != 0));

            data.WriteBit(moveSpline.anim_tier != null);                   // HasAnimTierTransition
            data.FlushBits();

            switch (moveSpline.facing.type)
            {
                case MonsterMoveType.FacingSpot:
                    data.WriteVector3(moveSpline.facing.f);         // FaceSpot
                    break;
                case MonsterMoveType.FacingTarget:
                    data.WritePackedGuid(moveSpline.facing.target); // FaceGUID
                    break;
                case MonsterMoveType.FacingAngle:
                    data.WriteFloat(moveSpline.facing.angle);       // FaceDirection
                    break;
            }

            if (hasFadeObjectTime)
                data.WriteInt32(moveSpline.effect_start_time);     // FadeObjectTime

            foreach (var vec in moveSpline.GetPath())
                data.WriteVector3(vec);

            if (moveSpline.spell_effect_extra != null)
            {
                data.WritePackedGuid(moveSpline.spell_effect_extra.Target);
                data.WriteInt32(moveSpline.spell_effect_extra.SpellVisualId);
                data.WriteInt32(moveSpline.spell_effect_extra.ProgressCurveId);
                data.WriteInt32(moveSpline.spell_effect_extra.ParabolicCurveId);
            }

            if (hasJumpExtraData)
            {
                data.WriteFloat(moveSpline.vertical_acceleration);
                data.WriteInt32(moveSpline.effect_start_time);
                data.WriteUInt32(0);                                                  // Duration (override)
            }

            if (moveSpline.anim_tier != null)
            {
                data.WriteInt32(moveSpline.anim_tier.TierTransitionId);
                data.WriteInt32(moveSpline.effect_start_time);
                data.WriteInt32(0);
                data.WriteUInt8(moveSpline.anim_tier.AnimTier);
            }
        }
    }

    public static void Write(this Spline<int> spline, WorldPacket data)
    {
        data.WriteBits(spline.GetPoints().Length, 16);
        foreach (var point in spline.GetPoints())
            data.WriteVector3(point);
    }

    public static void Write(this MovementForce movementForce, WorldPacket data, Position objectPosition = null)
    {
        data.WritePackedGuid(movementForce.ID);
        data.WriteVector3(movementForce.Origin);
        if (movementForce.Type == MovementForceType.Gravity && objectPosition != null) // gravity
        {
            Vector3 direction = Vector3.Zero;
            if (movementForce.Magnitude != 0.0f)
            {
                Position tmp = new(movementForce.Origin.X - objectPosition.GetPositionX(),
                    movementForce.Origin.Y - objectPosition.GetPositionY(),
                    movementForce.Origin.Z - objectPosition.GetPositionZ());
                float lengthSquared = tmp.GetExactDistSq(0.0f, 0.0f, 0.0f);
                
                if (lengthSquared > 0.0f)
                {
                    float mult = 1.0f / (float)Math.Sqrt(lengthSquared) * movementForce.Magnitude;
                    tmp.posX *= mult;
                    tmp.posY *= mult;
                    tmp.posZ *= mult;
                    
                    float minLengthSquared = (tmp.GetPositionX() * tmp.GetPositionX() * 0.04f) +
                        (tmp.GetPositionY() * tmp.GetPositionY() * 0.04f) +
                        (tmp.GetPositionZ() * tmp.GetPositionZ() * 0.04f);
                    
                    if (lengthSquared > minLengthSquared)
                        direction = new Vector3(tmp.posX, tmp.posY, tmp.posZ);
                }
            }

            data.WriteVector3(direction);
        }
        else
            data.WriteVector3(movementForce.Direction);

        data.WriteInt32(movementForce.TransportID);
        data.WriteFloat(movementForce.Magnitude);
        data.WriteInt32(movementForce.Unused910);
        data.WriteBits((byte)movementForce.Type, 2);
        data.FlushBits();
    }

    public static void Read(this MovementForce movementForce, WorldPacket data)
    {
        movementForce.ID = data.ReadPackedGuid();
        movementForce.Origin = data.ReadVector3();
        movementForce.Direction = data.ReadVector3();
        movementForce.TransportID = data.ReadInt32();
        movementForce.Magnitude = data.ReadFloat();
        movementForce.Unused910 = data.ReadInt32();
        movementForce.Type = (MovementForceType)data.ReadBits<byte>(2);
    }

    public static void Write(this ChrCustomizationChoice chrCustomizationChoice, WorldPacket data)
    {
        data.WriteInt32(chrCustomizationChoice.ChrCustomizationOptionID);
        data.WriteInt32(chrCustomizationChoice.ChrCustomizationChoiceID);
    }

    public static void Read(this ChrCustomizationChoice chrCustomizationChoice, WorldPacket data)
    {
        chrCustomizationChoice.ChrCustomizationOptionID = data.ReadInt32();
        chrCustomizationChoice.ChrCustomizationChoiceID = data.ReadInt32();
    }
}