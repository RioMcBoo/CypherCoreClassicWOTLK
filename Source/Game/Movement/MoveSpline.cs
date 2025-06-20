﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Movement
{
    public class MoveSpline
    {
        public MoveSpline()
        {
            m_Id = 0;
            time_passed = Milliseconds.Zero;
            vertical_acceleration = 0.0f;
            initialOrientation = 0.0f;
            effect_start_time = Milliseconds.Zero;
            point_Idx = 0;
            point_Idx_offset = 0;
            onTransport = false;
            splineIsFacingOnly = false;
            splineflags.Flags = SplineFlag.Done;
        }

        public void Initialize(MoveSplineInitArgs args)
        {
            splineflags = args.flags;
            facing = args.facing;
            m_Id = args.splineId;
            point_Idx_offset = args.path_Idx_offset;
            initialOrientation = args.initialOrientation;

            time_passed = Milliseconds.Zero;
            vertical_acceleration = 0.0f;
            effect_start_time = Milliseconds.Zero;
            spell_effect_extra = args.spellEffectExtra;
            anim_tier = args.animTier;
            splineIsFacingOnly = args.path.Count == 2 && args.facing.type != MonsterMoveType.Normal && ((args.path[1] - args.path[0]).Length() < 0.1f);

            velocity = args.velocity;

            // Check if its a stop spline
            if (args.flags.HasFlag(SplineFlag.Done))
            {
                spline.Clear();
                return;
            }

            InitSpline(args);

            // init parabolic / animation
            // spline initialized, duration known and i able to compute parabolic acceleration
            if (args.flags.HasFlag(SplineFlag.Parabolic | SplineFlag.Animation | SplineFlag.FadeObject))
            {
                Milliseconds spline_duration = Duration();
                effect_start_time = (Milliseconds)(spline_duration.Ticks * args.effect_start_time_percent + args.effect_start_time.TotalMilliseconds);
                if (effect_start_time > spline_duration)
                    effect_start_time = spline_duration;

                if (args.flags.HasFlag(SplineFlag.Parabolic) && effect_start_time < spline_duration)
                {
                    if (args.parabolic_amplitude != 0.0f)
                    {
                        float f_duration = MSToSecPrecise(spline_duration - effect_start_time);
                        vertical_acceleration = args.parabolic_amplitude * 8.0f / (f_duration * f_duration);
                    }
                    else if (args.vertical_acceleration != 0.0f)
                    {
                        vertical_acceleration = args.vertical_acceleration;
                    }
                }
            }
        }

        void InitSpline(MoveSplineInitArgs args)
        {
            EvaluationMode[] modes = [EvaluationMode.Linear, EvaluationMode.Catmullrom];
            if (args.flags.HasFlag(SplineFlag.Cyclic))
            {
                int cyclic_point = 0;
                if (splineflags.HasFlag(SplineFlag.EnterCycle))
                    cyclic_point = 1;   // shouldn't be modified, came from client
                spline.InitCyclicSpline(args.path.ToArray(), args.path.Count, modes[Convert.ToInt32(args.flags.IsSmooth())], cyclic_point, args.initialOrientation);
            }
            else
            {
                spline.InitSpline(args.path.ToArray(), args.path.Count, modes[Convert.ToInt32(args.flags.IsSmooth())], args.initialOrientation);
            }

            // init spline timestamps
            if (splineflags.HasFlag(SplineFlag.Falling))
            {
                FallInitializer init = new(spline.GetPoint(spline.First()).Z);
                spline.InitLengths(init);
            }
            else
            {
                CommonInitializer init = new(args.velocity);
                spline.InitLengths(init);
            }

            // TODO: what to do in such cases? problem is in input data (all points are at same coords)
            if (spline.Length() < 1)
            {
                Log.outError(LogFilter.Unit, 
                    "MoveSpline.init_spline: zero length spline, wrong input data?");
                
                spline.Set_length(spline.Last(), (Milliseconds)(spline.IsCyclic() ? 1000 : 1));
            }
            point_Idx = spline.First();
        }

        public int CurrentPathIdx()
        {
            int point = point_Idx_offset + point_Idx - spline.First() + (Finalized() ? 1 : 0);
            if (IsCyclic())
                point %= (spline.Last() - spline.First());
            return point;
        }

        public Vector3[] GetPath() { return spline.GetPoints(); }
        public Milliseconds TimePassed() { return time_passed; }

        public Milliseconds Duration() { return spline.Length(); }
        public int CurrentSplineIdx() { return point_Idx; }
        public int GetId() { return m_Id; }
        public bool Finalized() { return splineflags.HasFlag(SplineFlag.Done); }

        void _Finalize()
        {
            splineflags.SetUnsetFlag(SplineFlag.Done);
            point_Idx = spline.Last() - 1;
            time_passed = Duration();
        }

        public Vector4 ComputePosition(Milliseconds time_point, int point_index)
        {
            float u = 1.0f;
            int seg_time = spline.Length(point_index, point_index + 1);
            if (seg_time > 0)
                u = (time_point - spline.Length(point_index)) / (float)seg_time;

            Vector3 c;
            float orientation = initialOrientation;
            spline.Evaluate_Percent(point_index, u, out c);

            if (splineflags.HasFlag(SplineFlag.Parabolic))
                ComputeParabolicElevation(time_point, ref c.Z);
            else if (splineflags.HasFlag(SplineFlag.Falling))
                ComputeFallElevation(time_point, ref c.Z);

            if (splineflags.HasFlag(SplineFlag.Done) && facing.type != MonsterMoveType.Normal)
            {
                if (facing.type == MonsterMoveType.FacingAngle)
                    orientation = facing.angle;
                else if (facing.type == MonsterMoveType.FacingSpot)
                    orientation = MathF.Atan2(facing.f.Y - c.Y, facing.f.X - c.X);
                //nothing to do for MoveSplineFlag.Final_Target flag
            }
            else
            {
                if (!splineflags.HasFlag(SplineFlag.OrientationFixed | SplineFlag.Falling | SplineFlag.Unknown_0x8))
                {
                    Vector3 hermite;
                    spline.Evaluate_Derivative(point_Idx, u, out hermite);
                    if (hermite.X != 0f || hermite.Y != 0f)
                        orientation = MathF.Atan2(hermite.Y, hermite.X);
                }

                if (splineflags.HasFlag(SplineFlag.Backward))
                    orientation -= MathF.PI;
            }

            return new Vector4(c.X, c.Y, c.Z, orientation);
        }

        public Vector4 ComputePosition()
        {
            return ComputePosition(time_passed, point_Idx);
        }

        public Vector4 ComputePosition(Milliseconds time_offset)
        {
            Milliseconds time_point = time_passed + time_offset;
            if (time_point >= Duration())
                return ComputePosition(Duration(), spline.Last() - 1);
            
            if (time_point <= Milliseconds.Zero)
                return ComputePosition(Milliseconds.Zero, spline.First());

            // find point_index where spline.length(point_index) < time_point < spline.length(point_index + 1)
            int point_index = point_Idx;
            while (time_point >= spline.Length(point_index + 1))
                ++point_index;

            while (time_point < spline.Length(point_index))
                --point_index;

            return ComputePosition(time_point, point_index);
        }

        public void ComputeParabolicElevation(Milliseconds time_point, ref float el)
        {
            if (time_point > effect_start_time)
            {
                float t_passedf = MSToSecPrecise(time_point - effect_start_time);
                float t_durationf = MSToSecPrecise(Duration() - effect_start_time); //client use not modified duration here
                if (spell_effect_extra != null && spell_effect_extra.ParabolicCurveId != 0)
                    t_passedf *= Global.DB2Mgr.GetCurveValueAt(spell_effect_extra.ParabolicCurveId, (float)time_point / (float)Duration());

                el += (t_durationf - t_passedf) * 0.5f * vertical_acceleration * t_passedf;
            }
        }

        public void ComputeFallElevation(Milliseconds time_point, ref float el)
        {
            float z_now = spline.GetPoint(spline.First()).Z - ComputeFallElevation(MSToSecPrecise(time_point), false);
            float final_z = FinalDestination().Z;
            el = Math.Max(z_now, final_z);
        }

        public static float ComputeFallElevation(float t_passed, bool isSafeFall, float start_velocity = 0.0f)
        {
            float termVel;
            float result;

            if (isSafeFall)
                termVel = SharedConst.terminalSafefallVelocity;
            else
                termVel = SharedConst.terminalVelocity;

            if (start_velocity > termVel)
                start_velocity = termVel;

            float terminal_time = (float)((isSafeFall ? SharedConst.terminal_safeFall_fallTime : SharedConst.terminal_fallTime) - start_velocity / SharedConst.gravity); // the time that needed to reach terminalVelocity

            if (t_passed > terminal_time)
            {
                result = termVel * (t_passed - terminal_time) +
                    start_velocity * terminal_time +
                    (float)SharedConst.gravity * terminal_time * terminal_time * 0.5f;
            }
            else
                result = t_passed * (float)(start_velocity + t_passed * SharedConst.gravity * 0.5f);

            return result;
        }

        float MSToSecPrecise(Milliseconds ms)
        {
            return ms.Ticks / (float)Time.MillisecondsInSecond;
        }

        public bool HasStarted()
        {
            return time_passed > Milliseconds.Zero;
        }

        public void Interrupt() { splineflags.SetUnsetFlag(SplineFlag.Done); }
        
        public void UpdateState(Milliseconds difftime)
        {
            do
            {
                UpdateState(ref difftime);
            } while (difftime > Milliseconds.Zero);
        }

        UpdateResult UpdateState(ref Milliseconds ms_time_diff)
        {
            if (Finalized())
            {
                ms_time_diff = Milliseconds.Zero;
                return UpdateResult.Arrived;
            }

            UpdateResult result = UpdateResult.None;
            Milliseconds minimal_diff = Time.Min(ms_time_diff, SegmentTimeElapsed());
            time_passed += minimal_diff;
            ms_time_diff -= minimal_diff;

            if (time_passed >= NextTimestamp())
            {
                ++point_Idx;
                if (point_Idx < spline.Last())
                {
                    result = UpdateResult.NextSegment;
                }
                else
                {
                    if (spline.IsCyclic())
                    {
                        point_Idx = spline.First();
                        time_passed %= Duration();
                        result = UpdateResult.NextCycle;
                        // Remove first point from the path after one full cycle.
                        // That point was the position of the unit prior to entering the cycle and it shouldn't be repeated with continuous cycles.
                        if (splineflags.HasFlag(SplineFlag.EnterCycle))
                        {
                            splineflags.SetUnsetFlag(SplineFlag.EnterCycle, false);

                            MoveSplineInitArgs args = new(spline.GetPointCount());
                            args.path.AddRange(spline.GetPoints().AsSpan().Slice(spline.First() + 1, spline.Last()).ToArray());
                            args.facing = facing;
                            args.flags = splineflags;
                            args.path_Idx_offset = point_Idx_offset;
                            // MoveSplineFlag::Parabolic | MoveSplineFlag::Animation not supported currently
                            //args.parabolic_amplitude = ?;
                            //args.time_perc = ?;
                            args.splineId = m_Id;
                            args.initialOrientation = initialOrientation;
                            args.velocity = (Speed)1.0f; // Calculated below
                            args.HasVelocity = true;
                            args.TransformForTransport = onTransport;
                            if (args.Validate(null))
                            {
                                // New cycle should preserve previous cycle's duration for some weird reason, even though
                                // the path is really different now. Blizzard is weird. Or this was just a simple oversight.
                                // Since our splines precalculate length with velocity in mind, if we want to find the desired
                                // velocity, we have to make a fake spline, calculate its duration and then compare it to the
                                // desired duration, thus finding out how much the velocity has to be increased for them to match.
                                MoveSpline tempSpline = new();
                                tempSpline.Initialize(args);
                                args.velocity = (Speed)((float)tempSpline.Duration() / Duration());

                                if (args.Validate(null))
                                    InitSpline(args);
                            }
                        }
                    }
                    else
                    {
                        _Finalize();
                        ms_time_diff = Milliseconds.Zero;
                        result = UpdateResult.Arrived;
                    }
                }
            }

            return result;
        }

        Milliseconds NextTimestamp() { return spline.Length(point_Idx + 1); }
        Milliseconds SegmentTimeElapsed() { return NextTimestamp() - time_passed; }
        public bool IsCyclic() { return splineflags.HasFlag(SplineFlag.Cyclic); }
        public bool IsFalling() { return splineflags.HasFlag(SplineFlag.Falling); }
        public bool Initialized() { return !spline.Empty(); }
        public Vector3 FinalDestination() { return Initialized() ? spline.GetPoint(spline.Last()) : Vector3.Zero; }
        public Vector3 CurrentDestination() { return Initialized() ? spline.GetPoint(point_Idx + 1) : Vector3.Zero; }

        public AnimTier? GetAnimation() { return anim_tier != null ? (AnimTier)anim_tier.AnimTier : null; }
        
        #region Fields
        public MoveSplineInitArgs InitArgs;
        public Spline<Milliseconds> spline = new();
        public FacingInfo facing;
        public MoveSplineFlag splineflags = new();
        public bool onTransport;
        public bool splineIsFacingOnly;
        public int m_Id;
        public Milliseconds time_passed;
        public float vertical_acceleration;
        public float initialOrientation;
        public Milliseconds effect_start_time;
        public int point_Idx;
        public int point_Idx_offset;
        public Speed velocity;
        public SpellEffectExtraData spell_effect_extra;
        public AnimTierTransition anim_tier;
        #endregion

        public class CommonInitializer : IInitializer<Milliseconds>
        {
            public CommonInitializer(Speed _velocity)
            {
                velocity = _velocity;
                time = (Milliseconds)1;
            }

            public Speed velocity;
            public Milliseconds time;

            public Milliseconds Invoke(Spline<Milliseconds> s, int i)
            {
                time += (Milliseconds)(s.SegLength(i) / velocity.PerMS);
                return time;
            }
        }

        public class FallInitializer : IInitializer<Milliseconds>
        {
            public FallInitializer(float startelevation)
            {
                startElevation = startelevation;
            }
            float startElevation;

            public Milliseconds Invoke(Spline<Milliseconds> s, int i)
            {
                return (Milliseconds)(ComputeFallTime(startElevation - s.GetPoint(i + 1).Z, false) * 1000.0f);
            }

            float ComputeFallTime(float path_length, bool isSafeFall)
            {
                if (path_length < 0.0f)
                    return 0.0f;

                float time;
                if (isSafeFall)
                {
                    if (path_length >= SharedConst.terminal_safeFall_length)
                        time = (path_length - SharedConst.terminal_safeFall_length) / SharedConst.terminalSafefallVelocity + SharedConst.terminal_safeFall_fallTime;
                    else
                        time = (float)Math.Sqrt(2.0f * path_length / SharedConst.gravity);
                }
                else
                {
                    if (path_length >= SharedConst.terminal_length)
                        time = (path_length - SharedConst.terminal_length) / SharedConst.terminalVelocity + SharedConst.terminal_fallTime;
                    else
                        time = (float)Math.Sqrt(2.0f * path_length / SharedConst.gravity);
                }

                return time;
            }
        }
        public enum UpdateResult
        {
            None = 0x01,
            Arrived = 0x02,
            NextCycle = 0x04,
            NextSegment = 0x08
        }
    }

    public interface IInitializer<T>
    {
        Milliseconds Invoke(Spline<T> s, int i);
    }

    public class SplineChainLink
    {
        public List<Vector3> Points = new();
        public TimeSpan ExpectedDuration;
        public TimeSpan TimeToNext;
        public Speed Velocity;

        public SplineChainLink(Vector3[] points, TimeSpan expectedDuration, TimeSpan timeToNext, Speed velocity)
        {
            Points.AddRange(points);
            ExpectedDuration = expectedDuration;
            TimeToNext = timeToNext;
            Velocity = velocity;
        }

        public SplineChainLink(TimeSpan expectedDuration, TimeSpan msToNext, Speed velocity)
        {
            ExpectedDuration = expectedDuration;
            TimeToNext = msToNext;
            Velocity = velocity;
        }
    }

    public class SplineChainResumeInfo
    {
        public SplineChainResumeInfo() { }
        public SplineChainResumeInfo(int id, IReadOnlyList<SplineChainLink> chain, bool walk, byte splineIndex, byte wpIndex, TimeSpan timeToNext)
        {
            PointID = id;
            Chain = chain;
            IsWalkMode = walk;
            SplineIndex = splineIndex;
            PointIndex = wpIndex;
            TimeToNext = timeToNext;
        }

        public bool Empty() { return Chain.Empty(); }

        public int PointID;
        public IReadOnlyList<SplineChainLink> Chain;
        public bool IsWalkMode;
        public byte SplineIndex;
        public byte PointIndex;
        public TimeSpan TimeToNext;
    }
}
