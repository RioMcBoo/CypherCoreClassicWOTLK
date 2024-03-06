// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.Entities;
using Game.Movement;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Networking.Packets
{
    public class ClientPlayerMovement : ClientPacket
    {
        public ClientPlayerMovement(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Status.Read(_worldPacket);
        }

        public MovementInfo Status = new();
    }

    public class MoveUpdate : ServerPacket
    {
        public MoveUpdate() : base(ServerOpcodes.MoveUpdate, ConnectionType.Instance) { }

        public override void Write()
        {
            Status.Write(_worldPacket);
        }

        public MovementInfo Status;
    }

    public class MonsterMove : ServerPacket
    {
        public MonsterMove() : base(ServerOpcodes.OnMonsterMove, ConnectionType.Instance)
        {
            SplineData = new MovementMonsterSpline();
        }

        public void InitializeSplineData(MoveSpline moveSpline)
        {
            SplineData.Id = moveSpline.GetId();
            MovementSpline movementSpline = SplineData.Move;

            MoveSplineFlag splineFlags = moveSpline.splineflags;
            splineFlags.SetUnsetFlag(SplineFlag.Cyclic, moveSpline.IsCyclic());
            movementSpline.Flags = (uint)(splineFlags.Flags & ~SplineFlag.MaskNoMonsterMove);
            movementSpline.Face = moveSpline.facing.type;
            movementSpline.FaceDirection = moveSpline.facing.angle;
            movementSpline.FaceGUID = moveSpline.facing.target;
            movementSpline.FaceSpot = moveSpline.facing.f;

            if (splineFlags.HasFlag(SplineFlag.Animation))
            {
                MonsterSplineAnimTierTransition animTierTransition = new();
                animTierTransition.TierTransitionID = (int)moveSpline.anim_tier.TierTransitionId;
                animTierTransition.StartTime = (uint)moveSpline.effect_start_time;
                animTierTransition.AnimTier = moveSpline.anim_tier.AnimTier;
                movementSpline.AnimTierTransition = animTierTransition;
            }

            movementSpline.MoveTime = (uint)moveSpline.Duration();

            if (splineFlags.HasFlag(SplineFlag.Parabolic) && (moveSpline.spell_effect_extra == null || moveSpline.effect_start_time != 0))
            {
                MonsterSplineJumpExtraData jumpExtraData = new();
                jumpExtraData.JumpGravity = moveSpline.vertical_acceleration;
                jumpExtraData.StartTime = (uint)moveSpline.effect_start_time;
                movementSpline.JumpExtraData = jumpExtraData;
            }

            if (splineFlags.HasFlag(SplineFlag.FadeObject))
                movementSpline.FadeObjectTime = (uint)moveSpline.effect_start_time;

            if (moveSpline.spell_effect_extra != null)
            {
                MonsterSplineSpellEffectExtraData spellEffectExtraData = new();
                spellEffectExtraData.TargetGuid = moveSpline.spell_effect_extra.Target;
                spellEffectExtraData.SpellVisualID = moveSpline.spell_effect_extra.SpellVisualId;
                spellEffectExtraData.ProgressCurveID = moveSpline.spell_effect_extra.ProgressCurveId;
                spellEffectExtraData.ParabolicCurveID = moveSpline.spell_effect_extra.ParabolicCurveId;
                spellEffectExtraData.JumpGravity = moveSpline.vertical_acceleration;
                movementSpline.SpellEffectExtraData = spellEffectExtraData;
            }

            var spline = moveSpline.spline;
            Vector3[] array = spline.GetPoints();

            if (splineFlags.HasFlag(SplineFlag.UncompressedPath))
            {
                if (!splineFlags.HasFlag(SplineFlag.Cyclic))
                {
                    int count = spline.GetPointCount() - 3;
                    for (uint i = 0; i < count; ++i)
                        movementSpline.Points.Add(array[i + 2]);
                }
                else
                {
                    int count = spline.GetPointCount() - 3;
                    movementSpline.Points.Add(array[1]);
                    for (uint i = 0; i < count; ++i)
                        movementSpline.Points.Add(array[i + 1]);
                }
            }
            else
            {
                int lastIdx = spline.GetPointCount() - 3;
                Span<Vector3> realPath = new Span<Vector3>(spline.GetPoints()).Slice(1);

                movementSpline.Points.Add(realPath[lastIdx]);

                if (lastIdx > 1)
                {
                    Vector3 middle = (realPath[0] + realPath[lastIdx]) / 2.0f;

                    // first and last points already appended
                    for (int i = 1; i < lastIdx; ++i)
                        movementSpline.PackedDeltas.Add(middle - realPath[i]);
                }
            }
        }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteVector3(Pos);
            SplineData.Write(_worldPacket);
        }

        public MovementMonsterSpline SplineData;
        public ObjectGuid MoverGUID;
        public Vector3 Pos;
    }

    class FlightSplineSync : ServerPacket
    {
        public FlightSplineSync() : base(ServerOpcodes.FlightSplineSync, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteFloat(SplineDist);
        }

        public ObjectGuid Guid;
        public float SplineDist;
    }

    public class MoveSplineSetSpeed : ServerPacket
    {
        public MoveSplineSetSpeed(ServerOpcodes opcode) : base(opcode, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteFloat(Speed);
        }

        public ObjectGuid MoverGUID;
        public float Speed = 1.0f;
    }

    public class MoveSetSpeed : ServerPacket
    {
        public MoveSetSpeed(ServerOpcodes opcode) : base(opcode, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteUInt32(SequenceIndex);
            _worldPacket.WriteFloat(Speed);
        }

        public ObjectGuid MoverGUID;
        public uint SequenceIndex; // Unit movement packet index, incremented each time
        public float Speed = 1.0f;
    }

    public class MoveUpdateSpeed : ServerPacket
    {
        public MoveUpdateSpeed(ServerOpcodes opcode) : base(opcode, ConnectionType.Instance) { }

        public override void Write()
        {
            Status.Write(_worldPacket);
            _worldPacket.WriteFloat(Speed);
        }

        public MovementInfo Status;
        public float Speed = 1.0f;
    }

    public class MoveSplineSetFlag : ServerPacket
    {
        public MoveSplineSetFlag(ServerOpcodes opcode) : base(opcode, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
        }

        public ObjectGuid MoverGUID;
    }

    public class MoveSetFlag : ServerPacket
    {
        public MoveSetFlag(ServerOpcodes opcode) : base(opcode, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteUInt32(SequenceIndex);
        }

        public ObjectGuid MoverGUID;
        public uint SequenceIndex; // Unit movement packet index, incremented each time
    }

    public class TransferPending : ServerPacket
    {
        public TransferPending() : base(ServerOpcodes.TransferPending) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(MapID);
            _worldPacket.WriteXYZ(OldMapPosition);
            _worldPacket.WriteBit(Ship.HasValue);
            _worldPacket.WriteBit(TransferSpellID.HasValue);
            if (Ship.HasValue)
            {
                _worldPacket.WriteInt32(Ship.Value.Id);
                _worldPacket.WriteInt32(Ship.Value.OriginMapID);
            }

            if (TransferSpellID.HasValue)
                _worldPacket.WriteInt32(TransferSpellID.Value);

            _worldPacket.FlushBits();
        }

        public int MapID = -1;
        public Position OldMapPosition;
        public ShipTransferPending? Ship;
        public int? TransferSpellID;

        public struct ShipTransferPending
        {
            public int Id;              // gameobject_template.entry of the transport the player is teleporting on
            public int OriginMapID;     // Map id the player is currently on (before teleport)
        }
    }

    public class TransferAborted : ServerPacket
    {
        public TransferAborted() : base(ServerOpcodes.TransferAborted) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(MapID);
            _worldPacket.WriteUInt8(Arg);
            _worldPacket.WriteInt32(MapDifficultyXConditionID);
            _worldPacket.WriteBits((uint)TransfertAbort, 6);
            _worldPacket.FlushBits();
        }

        public int MapID;
        public byte Arg;
        public int MapDifficultyXConditionID;
        public TransferAbortReason TransfertAbort;
    }

    public class NewWorld : ServerPacket
    {
        public NewWorld() : base(ServerOpcodes.NewWorld) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(MapID);
            Loc.Write(_worldPacket);
            _worldPacket.WriteUInt32(Reason);
            _worldPacket.WriteXYZ(MovementOffset);
        }

        public int MapID;
        public uint Reason;
        public TeleportLocation Loc = new();
        public Position MovementOffset;    // Adjusts all pending movement events by this offset
    }

    public class WorldPortResponse : ClientPacket
    {
        public WorldPortResponse(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class MoveTeleport : ServerPacket
    {
        public MoveTeleport() : base(ServerOpcodes.MoveTeleport, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteUInt32(SequenceIndex);
            _worldPacket.WriteXYZ(Pos);
            _worldPacket.WriteFloat(Facing);
            _worldPacket.WriteUInt8(PreloadWorld);

            _worldPacket.WriteBit(TransportGUID.HasValue);
            _worldPacket.WriteBit(Vehicle.HasValue);
            _worldPacket.FlushBits();

            if (Vehicle.HasValue)
            {
                _worldPacket.WriteUInt8(Vehicle.Value.VehicleSeatIndex);
                _worldPacket.WriteBit(Vehicle.Value.VehicleExitVoluntary);
                _worldPacket.WriteBit(Vehicle.Value.VehicleExitTeleport);
                _worldPacket.FlushBits();
            }

            if (TransportGUID.HasValue)
                _worldPacket.WritePackedGuid(TransportGUID.Value);
        }

        public Position Pos;
        public VehicleTeleport? Vehicle;
        public uint SequenceIndex;
        public ObjectGuid MoverGUID;
        public ObjectGuid? TransportGUID;
        public float Facing;
        public byte PreloadWorld;
    }

    public class MoveUpdateTeleport : ServerPacket
    {
        public MoveUpdateTeleport() : base(ServerOpcodes.MoveUpdateTeleport) { }

        public override void Write()
        {
            Status.Write(_worldPacket);

            _worldPacket.WriteInt32(MovementForces != null ? MovementForces.Count : 0);
            _worldPacket.WriteBit(WalkSpeed.HasValue);
            _worldPacket.WriteBit(RunSpeed.HasValue);
            _worldPacket.WriteBit(RunBackSpeed.HasValue);
            _worldPacket.WriteBit(SwimSpeed.HasValue);
            _worldPacket.WriteBit(SwimBackSpeed.HasValue);
            _worldPacket.WriteBit(FlightSpeed.HasValue);
            _worldPacket.WriteBit(FlightBackSpeed.HasValue);
            _worldPacket.WriteBit(TurnRate.HasValue);
            _worldPacket.WriteBit(PitchRate.HasValue);
            _worldPacket.FlushBits();

            if (MovementForces != null)
                foreach (MovementForce force in MovementForces)
                    force.Write(_worldPacket);

            if (WalkSpeed.HasValue)
                _worldPacket.WriteFloat(WalkSpeed.Value);

            if (RunSpeed.HasValue)
                _worldPacket.WriteFloat(RunSpeed.Value);

            if (RunBackSpeed.HasValue)
                _worldPacket.WriteFloat(RunBackSpeed.Value);

            if (SwimSpeed.HasValue)
                _worldPacket.WriteFloat(SwimSpeed.Value);

            if (SwimBackSpeed.HasValue)
                _worldPacket.WriteFloat(SwimBackSpeed.Value);

            if (FlightSpeed.HasValue)
                _worldPacket.WriteFloat(FlightSpeed.Value);

            if (FlightBackSpeed.HasValue)
                _worldPacket.WriteFloat(FlightBackSpeed.Value);

            if (TurnRate.HasValue)
                _worldPacket.WriteFloat(TurnRate.Value);

            if (PitchRate.HasValue)
                _worldPacket.WriteFloat(PitchRate.Value);
        }

        public MovementInfo Status;
        public List<MovementForce> MovementForces;
        public float? SwimBackSpeed;
        public float? FlightSpeed;
        public float? SwimSpeed;
        public float? WalkSpeed;
        public float? TurnRate;
        public float? RunSpeed;
        public float? FlightBackSpeed;
        public float? RunBackSpeed;
        public float? PitchRate;
    }

    class MoveApplyMovementForce : ServerPacket
    {
        public MoveApplyMovementForce() : base(ServerOpcodes.MoveApplyMovementForce, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteInt32(SequenceIndex);
            Force.Write(_worldPacket);
        }

        public ObjectGuid MoverGUID;
        public int SequenceIndex;
        public MovementForce Force;
    }

    class MoveApplyMovementForceAck : ClientPacket
    {
        public MoveApplyMovementForceAck(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Ack.Read(_worldPacket);
            Force.Read(_worldPacket);
        }

        public MovementAck Ack = new();
        public MovementForce Force = new();
    }

    class MoveRemoveMovementForce : ServerPacket
    {
        public MoveRemoveMovementForce() : base(ServerOpcodes.MoveRemoveMovementForce, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteInt32(SequenceIndex);
            _worldPacket.WritePackedGuid(ID);
        }

        public ObjectGuid MoverGUID;
        public int SequenceIndex;
        public ObjectGuid ID;
    }

    class MoveRemoveMovementForceAck : ClientPacket
    {
        public MoveRemoveMovementForceAck(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Ack.Read(_worldPacket);
            ID = _worldPacket.ReadPackedGuid();
        }

        public MovementAck Ack = new();
        public ObjectGuid ID;
    }

    class MoveUpdateApplyMovementForce : ServerPacket
    {
        public MoveUpdateApplyMovementForce() : base(ServerOpcodes.MoveUpdateApplyMovementForce) { }

        public override void Write()
        {
            Status.Write(_worldPacket);
            Force.Write(_worldPacket);
        }

        public MovementInfo Status;
        public MovementForce Force;
    }

    class MoveUpdateRemoveMovementForce : ServerPacket
    {
        public MoveUpdateRemoveMovementForce() : base(ServerOpcodes.MoveUpdateRemoveMovementForce) { }

        public override void Write()
        {
            Status.Write(_worldPacket);
            _worldPacket.WritePackedGuid(TriggerGUID);
        }

        public MovementInfo Status;
        public ObjectGuid TriggerGUID;
    }

    class MoveTeleportAck : ClientPacket
    {
        public MoveTeleportAck(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            MoverGUID = _worldPacket.ReadPackedGuid();
            AckIndex = _worldPacket.ReadInt32();
            MoveTime = _worldPacket.ReadInt32();
        }

        public ObjectGuid MoverGUID;
        public int AckIndex;
        public int MoveTime;
    }

    public class MovementAckMessage : ClientPacket
    {
        public MovementAckMessage(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Ack.Read(_worldPacket);
        }

        public MovementAck Ack = new();
    }

    public class MovementSpeedAck : ClientPacket
    {
        public MovementSpeedAck(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Ack.Read(_worldPacket);
            Speed = _worldPacket.ReadFloat();
        }

        public MovementAck Ack = new();
        public float Speed;
    }

    public class SetActiveMover : ClientPacket
    {
        public SetActiveMover(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ActiveMover = _worldPacket.ReadPackedGuid();
        }
        
        public ObjectGuid ActiveMover;
    }

    public class MoveSetActiveMover : ServerPacket
    {
        public MoveSetActiveMover() : base(ServerOpcodes.MoveSetActiveMover) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
        }
        
        public ObjectGuid MoverGUID;
    }

    class MoveKnockBack : ServerPacket
    {
        public MoveKnockBack() : base(ServerOpcodes.MoveKnockBack, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteUInt32(SequenceIndex);
            _worldPacket.WriteVector2(Direction);
            Speeds.Write(_worldPacket);
        }

        public ObjectGuid MoverGUID;
        public Vector2 Direction;
        public MoveKnockBackSpeeds Speeds = new();
        public uint SequenceIndex;
    }

    public class MoveUpdateKnockBack : ServerPacket
    {
        public MoveUpdateKnockBack() : base(ServerOpcodes.MoveUpdateKnockBack) { }

        public override void Write()
        {
            Status.Write(_worldPacket);
        }

        public MovementInfo Status;
    }

    class MoveKnockBackAck : ClientPacket
    {
        public MoveKnockBackAck(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Ack.Read(_worldPacket);
            if (_worldPacket.HasBit())
            {
                Speeds = new();
                Speeds.Value.Read(_worldPacket);
            }
        }

        public MovementAck Ack = new();
        public MoveKnockBackSpeeds? Speeds;
    }

    class MoveSetCollisionHeight : ServerPacket
    {
        public MoveSetCollisionHeight() : base(ServerOpcodes.MoveSetCollisionHeight) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteInt32(SequenceIndex);
            _worldPacket.WriteFloat(Height);
            _worldPacket.WriteFloat(Scale);
            _worldPacket.WriteUInt8((byte)Reason);
            _worldPacket.WriteInt32(MountDisplayID);
            _worldPacket.WriteInt32(ScaleDuration);
        }

        public float Scale = 1.0f;
        public ObjectGuid MoverGUID;
        public int MountDisplayID;
        public UpdateCollisionHeightReason Reason;
        public int SequenceIndex;
        public int ScaleDuration;
        public float Height = 1.0f;
    }

    public class MoveUpdateCollisionHeight : ServerPacket
    {
        public MoveUpdateCollisionHeight() : base(ServerOpcodes.MoveUpdateCollisionHeight) { }

        public override void Write()
        {
            Status.Write(_worldPacket);
            _worldPacket.WriteFloat(Height);
            _worldPacket.WriteFloat(Scale);
        }

        public MovementInfo Status;
        public float Scale = 1.0f;
        public float Height = 1.0f;
    }

    public class MoveSetCollisionHeightAck : ClientPacket
    {
        public MoveSetCollisionHeightAck(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Data.Read(_worldPacket);
            Height = _worldPacket.ReadFloat();
            MountDisplayID = _worldPacket.ReadUInt32();
            Reason = (UpdateCollisionHeightReason)_worldPacket.ReadUInt8();
        }

        public MovementAck Data = new();
        public UpdateCollisionHeightReason Reason;
        public uint MountDisplayID;
        public float Height = 1.0f;
    }

    class MoveTimeSkipped : ClientPacket
    {
        public MoveTimeSkipped(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            MoverGUID = _worldPacket.ReadPackedGuid();
            TimeSkipped = _worldPacket.ReadUInt32();
        }

        public ObjectGuid MoverGUID;
        public uint TimeSkipped;
    }

    class MoveSkipTime : ServerPacket
    {
        public MoveSkipTime() : base(ServerOpcodes.MoveSkipTime, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteUInt32(TimeSkipped);
        }

        public ObjectGuid MoverGUID;
        public uint TimeSkipped;
    }

    class SummonResponse : ClientPacket
    {
        public SummonResponse(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            SummonerGUID = _worldPacket.ReadPackedGuid();
            Accept = _worldPacket.HasBit();
        }

        public bool Accept;
        public ObjectGuid SummonerGUID;
    }

    public class ControlUpdate : ServerPacket
    {
        public ControlUpdate() : base(ServerOpcodes.ControlUpdate) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteBit(On);
            _worldPacket.FlushBits();
        }

        public ObjectGuid Guid;
        public bool On;        
    }

    class MoveSplineDone : ClientPacket
    {
        public MoveSplineDone(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Status.Read(_worldPacket);
            SplineID = _worldPacket.ReadInt32();
        }

        public MovementInfo Status = new();
        public int SplineID;
    }

    class SummonRequest : ServerPacket
    {
        public SummonRequest() : base(ServerOpcodes.SummonRequest, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(SummonerGUID);
            _worldPacket.WriteUInt32(SummonerVirtualRealmAddress);
            _worldPacket.WriteInt32(AreaID);
            _worldPacket.WriteUInt8((byte)Reason);
            _worldPacket.WriteBit(SkipStartingArea);
            _worldPacket.FlushBits();
        }

        public ObjectGuid SummonerGUID;
        public uint SummonerVirtualRealmAddress;
        public int AreaID;
        public SummonReason Reason;
        public bool SkipStartingArea;

        public enum SummonReason : byte
        {
            Spell = 0,
            Scenario = 1
        }
    }

    class SuspendToken : ServerPacket
    {
        public SuspendToken() : base(ServerOpcodes.SuspendToken, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SequenceIndex);
            _worldPacket.WriteBits(Reason, 2);
            _worldPacket.FlushBits();
        }

        public int SequenceIndex = 1;
        public int Reason = 1;
    }

    class SuspendTokenResponse : ClientPacket
    {
        public SuspendTokenResponse(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            SequenceIndex = _worldPacket.ReadInt32();
        }

        public int SequenceIndex;
    }

    class ResumeToken : ServerPacket
    {
        public ResumeToken() : base(ServerOpcodes.ResumeToken, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteUInt32(SequenceIndex);
            _worldPacket.WriteBits(Reason, 2);
            _worldPacket.FlushBits();
        }

        public uint SequenceIndex = 1;
        public uint Reason = 1;
    }

    class MoveSetCompoundState : ServerPacket
    {
        public MoveSetCompoundState() : base(ServerOpcodes.MoveSetCompoundState, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(MoverGUID);
            _worldPacket.WriteInt32(StateChanges.Count);
            foreach (MoveStateChange stateChange in StateChanges)
                stateChange.Write(_worldPacket);
        }

        public ObjectGuid MoverGUID;
        public List<MoveStateChange> StateChanges = new();

        public struct CollisionHeightInfo
        {
            public float Height;
            public float Scale;
            public UpdateCollisionHeightReason Reason;
        }

        public struct KnockBackInfo
        {
            public float HorzSpeed;
            public Vector2 Direction;
            public float InitVertSpeed;
        }

        public class SpeedRange
        {
            public float Min;
            public float Max;
        }

        public class MoveStateChange
        {
            public MoveStateChange(ServerOpcodes messageId, uint sequenceIndex)
            {
                MessageID = messageId;
                SequenceIndex = sequenceIndex;
            }

            public void Write(WorldPacket data)
            {
                data.WriteUInt16((ushort)MessageID);
                data.WriteUInt32(SequenceIndex);
                data.WriteBit(Speed.HasValue);
                data.WriteBit(SpeedRange != null);
                data.WriteBit(KnockBack.HasValue);
                data.WriteBit(VehicleRecID.HasValue);
                data.WriteBit(CollisionHeight.HasValue);
                data.WriteBit(@MovementForce != null);
                data.WriteBit(MovementForceGUID.HasValue);
                data.WriteBit(MovementInertiaID.HasValue);
                data.WriteBit(MovementInertiaLifetimeMs.HasValue);
                data.FlushBits();

                if (@MovementForce != null)
                    @MovementForce.Write(data);

                if (Speed.HasValue)
                    data.WriteFloat(Speed.Value);

                if (SpeedRange != null)
                {
                    data.WriteFloat(SpeedRange.Min);
                    data.WriteFloat(SpeedRange.Max);
                }

                if (KnockBack.HasValue)
                {
                    data.WriteFloat(KnockBack.Value.HorzSpeed);
                    data.WriteVector2(KnockBack.Value.Direction);
                    data.WriteFloat(KnockBack.Value.InitVertSpeed);
                }

                if (VehicleRecID.HasValue)
                    data.WriteInt32(VehicleRecID.Value);

                if (CollisionHeight.HasValue)
                {
                    data.WriteFloat(CollisionHeight.Value.Height);
                    data.WriteFloat(CollisionHeight.Value.Scale);
                    data.WriteBits((byte)CollisionHeight.Value.Reason, 2);
                    data.FlushBits();
                }

                if (MovementForceGUID.HasValue)
                    data.WritePackedGuid(MovementForceGUID.Value);

                if (MovementInertiaID.HasValue)
                    data.WriteInt32(MovementInertiaID.Value);

                if (MovementInertiaLifetimeMs.HasValue)
                    data.WriteUInt32(MovementInertiaLifetimeMs.Value);
            }

            public ServerOpcodes MessageID;
            public uint SequenceIndex;
            public float? Speed;
            public SpeedRange SpeedRange;
            public KnockBackInfo? KnockBack;
            public int? VehicleRecID;
            public CollisionHeightInfo? CollisionHeight;
            public MovementForce MovementForce;
            public ObjectGuid? MovementForceGUID;
            public int? MovementInertiaID;
            public uint? MovementInertiaLifetimeMs;
        }
    }

    class MoveInitActiveMoverComplete : ClientPacket
    {
        public uint Ticks;

        public MoveInitActiveMoverComplete(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Ticks = _worldPacket.ReadUInt32();
        }
    }
    
    //Structs
    public class MovementAck
    {
        public void Read(WorldPacket data)
        {
            Status.Read(data);
            AckIndex = data.ReadInt32();
        }

        public MovementInfo Status = new();
        public int AckIndex;
    }

    public struct MonsterSplineFilterKey
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt16(Idx);
            data.WriteUInt16(Speed);
        }

        public short Idx;
        public ushort Speed;
    }

    public class MonsterSplineFilter
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(FilterKeys.Count);
            data.WriteFloat(BaseSpeed);
            data.WriteInt16(StartOffset);
            data.WriteFloat(DistToPrevFilterKey);
            data.WriteInt16(AddedToStart);

            foreach (var filterKey in FilterKeys)
                filterKey.Write(data);

            data.WriteBits(FilterFlags, 2);
            data.FlushBits();
        }

        public List<MonsterSplineFilterKey> FilterKeys = new();
        public byte FilterFlags;
        public float BaseSpeed;
        public short StartOffset;
        public float DistToPrevFilterKey;
        public short AddedToStart;
    }

    public struct MonsterSplineSpellEffectExtraData
    {
        public void Write(WorldPacket data)
        {
            data.WritePackedGuid(TargetGuid);
            data.WriteUInt32(SpellVisualID);
            data.WriteUInt32(ProgressCurveID);
            data.WriteUInt32(ParabolicCurveID);
            data.WriteFloat(JumpGravity);
        }

        public ObjectGuid TargetGuid;
        public uint SpellVisualID;
        public uint ProgressCurveID;
        public uint ParabolicCurveID;
        public float JumpGravity;
    }

    public struct MonsterSplineJumpExtraData
    {
        public float JumpGravity;
        public uint StartTime;
        public uint Duration;

        public void Write(WorldPacket data)
        {
            data.WriteFloat(JumpGravity);
            data.WriteUInt32(StartTime);
            data.WriteUInt32(Duration);
        }
    }

    public struct MonsterSplineAnimTierTransition
    {
        public int TierTransitionID;
        public uint StartTime;
        public uint EndTime;
        public byte AnimTier;

        public void Write(WorldPacket data)
        {
            data.WriteInt32(TierTransitionID);
            data.WriteUInt32(StartTime);
            data.WriteUInt32(EndTime);
            data.WriteUInt8(AnimTier);
        }
    }

    public class TeleportLocation
    {
        public Position Pos;
        public int Unused901_1 = -1;
        public int Unused901_2 = -1;

        public void Write(WorldPacket data)
        {
            data.WriteXYZO(Pos);
            data.WriteInt32(Unused901_1);
            data.WriteInt32(Unused901_2);
        }
    }

    public class MovementSpline
    {
        public void Write(WorldPacket data)
        {
            data.WriteUInt32(Flags);
            data.WriteInt32(Elapsed);
            data.WriteUInt32(MoveTime);
            data.WriteUInt32(FadeObjectTime);
            data.WriteUInt8(Mode);
            data.WritePackedGuid(TransportGUID);
            data.WriteInt8(VehicleSeat);
            data.WriteBits((byte)Face, 2);
            data.WriteBits(Points.Count, 16);
            data.WriteBit(VehicleExitVoluntary);
            data.WriteBit(Interpolate);
            data.WriteBits(PackedDeltas.Count, 16);
            data.WriteBit(SplineFilter != null);
            data.WriteBit(SpellEffectExtraData.HasValue);
            data.WriteBit(JumpExtraData.HasValue);
            data.WriteBit(AnimTierTransition.HasValue);
            data.FlushBits();

            if (SplineFilter != null)
                SplineFilter.Write(data);

            switch (Face)
            {
                case MonsterMoveType.FacingSpot:
                    data.WriteVector3(FaceSpot);
                    break;
                case MonsterMoveType.FacingTarget:
                    data.WriteFloat(FaceDirection);
                    data.WritePackedGuid(FaceGUID);
                    break;
                case MonsterMoveType.FacingAngle:
                    data.WriteFloat(FaceDirection);
                    break;
            }

            foreach (Vector3 pos in Points)
                data.WriteVector3(pos);

            foreach (Vector3 pos in PackedDeltas)
                data.WritePackXYZ(pos);

            if (SpellEffectExtraData.HasValue)
                SpellEffectExtraData.Value.Write(data);

            if (JumpExtraData.HasValue)
                JumpExtraData.Value.Write(data);

            if (AnimTierTransition.HasValue)
                AnimTierTransition.Value.Write(data);
        }

        public uint Flags; // Spline flags
        public MonsterMoveType Face; // Movement direction (see MonsterMoveType enum)
        public int Elapsed;
        public uint MoveTime;
        public uint FadeObjectTime;
        public List<Vector3> Points = new(); // Spline path
        public byte Mode; // Spline mode - actually always 0 in this packet - Catmullrom mode appears only in SMSG_UPDATE_OBJECT. In this packet it is determined by flags
        public bool VehicleExitVoluntary;
        public bool Interpolate;
        public ObjectGuid TransportGUID;
        public sbyte VehicleSeat = -1;
        public List<Vector3> PackedDeltas = new();
        public MonsterSplineFilter SplineFilter;
        public MonsterSplineSpellEffectExtraData? SpellEffectExtraData;
        public MonsterSplineJumpExtraData? JumpExtraData;
        public MonsterSplineAnimTierTransition? AnimTierTransition;
        public float FaceDirection;
        public ObjectGuid FaceGUID;
        public Vector3 FaceSpot;
    }

    public class MovementMonsterSpline
    {
        public MovementMonsterSpline()
        {
            Move = new MovementSpline();
        }

        public void Write(WorldPacket data)
        {
            data.WriteUInt32(Id);
            data.WriteVector3(Destination);
            data.WriteBit(CrzTeleport);
            data.WriteBits(StopDistanceTolerance, 3);

            Move.Write(data);
        }

        public uint Id;
        public Vector3 Destination;
        public bool CrzTeleport;
        public byte StopDistanceTolerance;    // Determines how far from spline destination the mover is allowed to stop in place 0, 0, 3.0, 2.76, numeric_limits<float>::max, 1.1, float(INT_MAX); default before this field existed was distance 3.0 (index 2)
        public MovementSpline Move;
    }

    public struct VehicleTeleport
    {
        public byte VehicleSeatIndex;
        public bool VehicleExitVoluntary;
        public bool VehicleExitTeleport;
    }

    public struct MoveKnockBackSpeeds
    {
        public void Write(WorldPacket data)
        {
            data.WriteFloat(HorzSpeed);
            data.WriteFloat(VertSpeed);
        }

        public void Read(WorldPacket data)
        {
            HorzSpeed = data.ReadFloat();
            VertSpeed = data.ReadFloat();
        }

        public float HorzSpeed;
        public float VertSpeed;
    }
}
