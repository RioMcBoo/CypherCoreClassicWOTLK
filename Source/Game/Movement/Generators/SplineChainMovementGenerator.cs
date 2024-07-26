// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Movement
{
    public class SplineChainMovementGenerator : MovementGenerator
    {
        public SplineChainMovementGenerator(int id, List<SplineChainLink> chain, bool walk = false)
        {
            _id = id;
            _chain = chain;
            _chainSize = (byte)chain.Count;
            _walk = walk;

            Mode = MovementGeneratorMode.Default;
            Priority = MovementGeneratorPriority.Normal;
            Flags = MovementGeneratorFlags.InitializationPending;
            BaseUnitState = UnitState.Roaming;
        }

        public SplineChainMovementGenerator(SplineChainResumeInfo info)
        {
            _id = info.PointID;
            _chain = info.Chain;
            _chainSize = (byte)info.Chain.Count;
            _walk = info.IsWalkMode;
            _nextIndex = info.SplineIndex;
            _nextFirstWP = info.PointIndex;
            _timeToNext = info.TimeToNext;

            Mode = MovementGeneratorMode.Default;
            Priority = MovementGeneratorPriority.Normal;
            Flags = MovementGeneratorFlags.InitializationPending;
            if (info.SplineIndex >= info.Chain.Count)
                AddFlag(MovementGeneratorFlags.Finalized);

            BaseUnitState = UnitState.Roaming;
        }

        public override void Initialize(Unit owner)
        {
            RemoveFlag(MovementGeneratorFlags.InitializationPending | MovementGeneratorFlags.Deactivated);
            AddFlag(MovementGeneratorFlags.Initialized);

            if (_chainSize == 0)
            {
                Log.outError(LogFilter.Movement, 
                    $"SplineChainMovementGenerator::Initialize: " +
                    $"couldn't initialize generator, referenced spline is empty! ({owner.GetGUID()})");

                return;
            }

            if (_nextIndex >= _chainSize)
            {
                Log.outWarn(LogFilter.Movement, 
                    $"SplineChainMovementGenerator::Initialize: " +
                    $"couldn't initialize generator, _nextIndex is >= _chainSize ({owner.GetGUID()})");
                                
                _timeToNext = TimeSpan.Zero;
                return;
            }

            if (_nextFirstWP != 0) // this is a resumed movegen that has to start with a partial spline
            {

                if (HasFlag(MovementGeneratorFlags.Finalized))
                    return;

                SplineChainLink thisLink = _chain[_nextIndex];
                if (_nextFirstWP >= thisLink.Points.Count)
                {
                    Log.outError(LogFilter.Movement, 
                        $"SplineChainMovementGenerator::Initialize: " +
                        $"attempted to resume spline chain from invalid resume state, " +
                        $"_nextFirstWP >= path size (_nextIndex: {_nextIndex}, " +
                        $"_nextFirstWP: {_nextFirstWP}). ({owner.GetGUID()})");

                    _nextFirstWP = (byte)(thisLink.Points.Count - 1);
                }

                owner.AddUnitState(UnitState.RoamingMove);
                Span<Vector3> partial = thisLink.Points.ToArray();
                SendPathSpline(owner, thisLink.Velocity, partial[(_nextFirstWP - 1)..]);

                Log.outDebug(LogFilter.Movement, 
                    $"SplineChainMovementGenerator::Initialize: " +
                    $"resumed spline chain generator from resume state. ({owner.GetGUID()})");

                ++_nextIndex;
                if (_nextIndex >= _chainSize)
                    _timeToNext = TimeSpan.Zero;
                else if (_timeToNext == TimeSpan.Zero)
                    _timeToNext = (Milliseconds)1;
                _nextFirstWP = 0;
            }
            else
            {
                _timeToNext = Time.Max(_chain[_nextIndex].TimeToNext, (Milliseconds)1);
                SendSplineFor(owner, _nextIndex, ref _timeToNext);

                ++_nextIndex;
                if (_nextIndex >= _chainSize)
                    _timeToNext = (Milliseconds)0;
            }
        }

        public override void Reset(Unit owner) 
        {
            RemoveFlag(MovementGeneratorFlags.Deactivated);

            owner.StopMoving();
            Initialize(owner);
        }

        public override bool Update(Unit owner, TimeSpan diff)
        {
            if (owner == null || HasFlag(MovementGeneratorFlags.Finalized))
                return false;

            // _msToNext being zero here means we're on the final spline
            if (_timeToNext == TimeSpan.Zero)
            {
                if (owner.MoveSpline.Finalized())
                {
                    AddFlag(MovementGeneratorFlags.InformEnabled);
                    return false;
                }
                return true;
            }

            if (_timeToNext <= diff)
            {
                // Send next spline
                Log.outDebug(LogFilter.Movement, 
                    $"SplineChainMovementGenerator::Update: " +
                    $"sending spline on index {_nextIndex} ({(Milliseconds)(diff - _timeToNext)} ms late). ({owner.GetGUID()})");

                _timeToNext = Time.Max(_chain[_nextIndex].TimeToNext, (Milliseconds)1);
                SendSplineFor(owner, _nextIndex, ref _timeToNext);
                ++_nextIndex;
                if (_nextIndex >= _chainSize)
                {
                    // We have reached the final spline, once it finalizes we should also finalize the movegen (start checking on next update)
                    _timeToNext = TimeSpan.Zero;
                    return true;
                }
            }
            else
                _timeToNext -= diff;

            return true;
        }

        public override void Deactivate(Unit owner)
        {
            AddFlag(MovementGeneratorFlags.Deactivated);
            owner.ClearUnitState(UnitState.RoamingMove);
        }

        public override void Finalize(Unit owner, bool active, bool movementInform)
        {
            AddFlag(MovementGeneratorFlags.Finalized);

            if (active)
                owner.ClearUnitState(UnitState.RoamingMove);

            if (movementInform && HasFlag(MovementGeneratorFlags.InformEnabled))
            {
                CreatureAI ai = owner.ToCreature().GetAI();
                if (ai != null)
                    ai.MovementInform(MovementGeneratorType.SplineChain, _id);
            }
        }

        Milliseconds SendPathSpline(Unit owner, Speed velocity, Span<Vector3> path)
        {
            int nodeCount = path.Length;
            Cypher.Assert(nodeCount > 1, 
                $"SplineChainMovementGenerator::SendPathSpline: " +
                $"Every path must have source & destination (size > 1)! ({owner.GetGUID()})");


            MoveSplineInit init = new(owner);
            if (nodeCount > 2)
                init.MovebyPath(path.ToArray());
            else
                init.MoveTo(path[1], false, true);

            if (velocity > 0.0f)
                init.SetVelocity(velocity);

            init.SetWalk(_walk);
            return init.Launch();
        }

        void SendSplineFor(Unit owner, int index, ref TimeSpan duration)
        {
            Cypher.Assert(index < _chainSize, 
                $"SplineChainMovementGenerator::SendSplineFor: " +
                $"referenced index ({index}) higher than path size ({_chainSize})!");

            Log.outDebug(LogFilter.Movement, 
                $"SplineChainMovementGenerator::SendSplineFor: " +
                $"sending spline on index: {index}. ({owner.GetGUID()})");

            SplineChainLink thisLink = _chain[index];
            TimeSpan actualDuration = SendPathSpline(owner, thisLink.Velocity, new Span<Vector3>(thisLink.Points.ToArray()));
            if (actualDuration != thisLink.ExpectedDuration)
            {
                Log.outDebug(LogFilter.Movement, 
                    $"SplineChainMovementGenerator::SendSplineFor: sent spline on index: {index}, " +
                    $"duration: {actualDuration}. Expected duration: {thisLink.ExpectedDuration} " +
                    $"(delta {actualDuration - thisLink.ExpectedDuration}). Adjusting. ({owner.GetGUID()})");

                duration = actualDuration / thisLink.ExpectedDuration * duration;
            }
            else
            {
                Log.outDebug(LogFilter.Movement, 
                    $"SplineChainMovementGenerator::SendSplineFor: " +
                    $"sent spline on index {index}, duration: {actualDuration}. ({owner.GetGUID()})");
            }
        }

        SplineChainResumeInfo GetResumeInfo(Unit owner)
        {
            if (_nextIndex == 0)
                return new SplineChainResumeInfo(_id, _chain, _walk, 0, 0, _timeToNext);

            if (owner.MoveSpline.Finalized())
            {
                if (_nextIndex < _chainSize)
                    return new SplineChainResumeInfo(_id, _chain, _walk, _nextIndex, 0, (Milliseconds)1);
                else
                    return new SplineChainResumeInfo();
            }

            return new SplineChainResumeInfo(_id, _chain, _walk, (byte)(_nextIndex - 1), (byte)owner.MoveSpline.CurrentSplineIdx(), _timeToNext);
        }

        public override MovementGeneratorType GetMovementGeneratorType() { return MovementGeneratorType.SplineChain; }

        public int GetId() { return _id; }

        int _id;
        List<SplineChainLink> _chain = new();
        byte _chainSize;
        bool _walk;
        byte _nextIndex;
        byte _nextFirstWP; // only used for resuming
        TimeSpan _timeToNext;
    }
}
