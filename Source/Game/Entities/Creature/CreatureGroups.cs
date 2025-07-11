﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Database;
using Game.Maps;
using System;
using System.Collections.Generic;
using Framework.Constants;
using System.Linq;

namespace Game.Entities
{
    public class FormationMgr
    {
        static Dictionary<long, FormationInfo> _creatureGroupMap = new();

        public static void AddCreatureToGroup(long leaderSpawnId, Creature creature)
        {
            Map map = creature.GetMap();

            var creatureGroup = map.CreatureGroupHolder.LookupByKey(leaderSpawnId);
            if (creatureGroup != null)
            {
                //Add member to an existing group
                Log.outDebug(LogFilter.Unit, 
                    $"Group found: {leaderSpawnId}, inserting creature GUID: " +
                    $"{creature.GetGUID()}, Group InstanceID {creature.GetInstanceId()}");

                // With dynamic spawn the creature may have just respawned
                // we need to find previous instance of creature and delete it from the formation, as it'll be invalidated
                var bounds = map.GetCreatureBySpawnIdStore()[creature.GetSpawnId()];
                foreach (var other in bounds)
                {
                    if (other == creature)
                        continue;

                    if (creatureGroup.HasMember(other))
                        creatureGroup.RemoveMember(other);
                }

                creatureGroup.AddMember(creature);
            }
            else
            {
                //Create new group
                Log.outDebug(LogFilter.Unit, 
                    $"Group not found: {leaderSpawnId}. Creating new group.");

                CreatureGroup group = new(leaderSpawnId);
                map.CreatureGroupHolder[leaderSpawnId] = group;
                group.AddMember(creature);
            }
        }

        public static void RemoveCreatureFromGroup(CreatureGroup group, Creature member)
        {
            Log.outDebug(LogFilter.Unit, 
                $"Deleting member GUID: {group.GetLeaderSpawnId()} from group {member.GetSpawnId()}");

            group.RemoveMember(member);

            if (group.IsEmpty())
            {
                Map map = member.GetMap();

                Log.outDebug(LogFilter.Unit, $"Deleting group with InstanceID {member.GetInstanceId()}");

                Cypher.Assert(map.CreatureGroupHolder.ContainsKey(group.GetLeaderSpawnId()), 
                    $"Not registered group {group.GetLeaderSpawnId()} in map {map.GetId()}");
                map.CreatureGroupHolder.Remove(group.GetLeaderSpawnId());
            }
        }

        public static void LoadCreatureFormations()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            //Get group data
            SQLResult result = DB.World.Query("SELECT leaderGUID, memberGUID, dist, angle, groupAI, point_1, point_2 FROM creature_formations ORDER BY leaderGUID");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 creatures in formations. DB table `creature_formations` is empty!");
                return;
            }

            uint count = 0;
            List<long> leaderSpawnIds = new();
            do
            {
                //Load group member data
                FormationInfo member = new();
                member.LeaderSpawnId = result.Read<long>(0);
                long memberSpawnId = result.Read<long>(1);
                member.FollowDist = 0f;
                member.FollowAngle = 0f;

                //If creature is group leader we may skip loading of dist/angle
                if (member.LeaderSpawnId != memberSpawnId)
                {
                    member.FollowDist = result.Read<float>(2);
                    member.FollowAngle = result.Read<float>(3) * MathFunctions.PI / 180;
                }

                member.GroupAI = result.Read<int>(4);

                for (var i = 0; i < 2; ++i)
                    member.LeaderWaypointIDs[i] = result.Read<ushort>(5 + i);

                // check data correctness
                {
                    if (Global.ObjectMgr.GetCreatureData(member.LeaderSpawnId) == null)
                    {
                        Log.outError(LogFilter.Sql, 
                            $"creature_formations table leader guid {member.LeaderSpawnId} incorrect (not exist)");
                        continue;
                    }

                    if (Global.ObjectMgr.GetCreatureData(memberSpawnId) == null)
                    {
                        Log.outError(LogFilter.Sql,
                            $"creature_formations table member guid {memberSpawnId} incorrect (not exist)");
                        continue;
                    }

                    leaderSpawnIds.Add(member.LeaderSpawnId);
                }

                _creatureGroupMap.Add(memberSpawnId, member);
                ++count;
            }
            while (result.NextRow());

            foreach (var leaderSpawnId in leaderSpawnIds)
            {
                if (!_creatureGroupMap.ContainsKey(leaderSpawnId))
                {
                    Log.outError(LogFilter.Sql, 
                        $"creature_formation contains leader spawn {leaderSpawnId} " +
                        $"which is not included on its formation, removing");

                    foreach (var itr in _creatureGroupMap.ToList())
                    {
                        if (itr.Value.LeaderSpawnId == leaderSpawnId)
                            _creatureGroupMap.Remove(itr.Key);
                    }
                }
            }

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {count} creatures in formations in {Time.Diff(oldMSTime)} ms.");
        }

        public static FormationInfo GetFormationInfo(long spawnId)
        {
            return _creatureGroupMap.LookupByKey(spawnId);
        }

        public static void AddFormationMember(long spawnId, float followAng, float followDist, long leaderSpawnId, int groupAI)
        {
            FormationInfo member = new();
            member.LeaderSpawnId = leaderSpawnId;
            member.FollowDist = followDist;
            member.FollowAngle = followAng;
            member.GroupAI = groupAI;
            for (var i = 0; i < 2; ++i)
                member.LeaderWaypointIDs[i] = 0;

            _creatureGroupMap.Add(spawnId, member);
        }
    }

    public class FormationInfo
    {
        public long LeaderSpawnId;
        public float FollowDist;
        public float FollowAngle;
        public int GroupAI;
        public int[] LeaderWaypointIDs = new int[2];
    }

    public class CreatureGroup
    {
        Creature _leader;
        Dictionary<Creature, FormationInfo> _members = new();

        long _leaderSpawnId;
        bool _formed;
        bool _engaging;

        public CreatureGroup(long leaderSpawnId)
        {
            _leaderSpawnId = leaderSpawnId;
        }

        public void AddMember(Creature member)
        {
            Log.outDebug(LogFilter.Unit, 
                $"CreatureGroup.AddMember: Adding {member.GetGUID()}.");

            //Check if it is a leader
            if (member.GetSpawnId() == _leaderSpawnId)
            {
                Log.outDebug(LogFilter.Unit, 
                    $"{member.GetGUID()} is formation leader. Adding group.");
                _leader = member;
            }

            // formation must be registered at this point
            FormationInfo formationInfo = FormationMgr.GetFormationInfo(member.GetSpawnId());
            _members.Add(member, formationInfo);
            member.SetFormation(this);
        }

        public void RemoveMember(Creature member)
        {
            if (_leader == member)
                _leader = null;

            _members.Remove(member);
            member.SetFormation(null);
        }

        public void MemberEngagingTarget(Creature member, Unit target)
        {
            // used to prevent recursive calls
            if (_engaging)
                return;

            GroupAIFlags groupAI = (GroupAIFlags)FormationMgr.GetFormationInfo(member.GetSpawnId()).GroupAI;
            if (groupAI == 0)
                return;

            if (member == _leader)
            {
                if (!groupAI.HasFlag(GroupAIFlags.MembersAssistLeader))
                    return;
            }
            else if (!groupAI.HasFlag(GroupAIFlags.LeaderAssistsMember))
                return;

            _engaging = true;

            foreach (var pair in _members)
            {
                Creature other = pair.Key;

                // Skip self
                if (other == member)
                    continue;

                if (!other.IsAlive())
                    continue;

                if (((other != _leader && groupAI.HasFlag(GroupAIFlags.MembersAssistLeader)) || (other == _leader && groupAI.HasFlag(GroupAIFlags.LeaderAssistsMember)))
                    && other.IsValidAttackTarget(target))
                {
                    other.EngageWithTarget(target);
                }
            }

            _engaging = false;
        }

        public void FormationReset(bool dismiss)
        {
            foreach (var creature in _members.Keys)
            {
                if (creature != _leader && creature.IsAlive())
                    creature.GetMotionMaster().MoveIdle();
            }

            //_formed = !dismiss;
        }

        public void LeaderStartedMoving()
        {
            if (_leader == null)
                return;

            foreach (var pair in _members)
            {
                Creature member = pair.Key;
                if (member == _leader || !member.IsAlive() || member.IsEngaged()
                    || !pair.Value.GroupAI.HasAnyFlag((int)GroupAIFlags.IdleInFormation))
                {
                    continue;
                }

                float angle = pair.Value.FollowAngle + MathF.PI; // for some reason, someone thought it was a great idea to invert relativ angles...
                float dist = pair.Value.FollowDist;

                if (!member.HasUnitState(UnitState.FollowFormation))
                    member.GetMotionMaster().MoveFormation(_leader, dist, angle, pair.Value.LeaderWaypointIDs[0], pair.Value.LeaderWaypointIDs[1]);
            }
        }

        public bool CanLeaderStartMoving()
        {
            foreach (var pair in _members)
            {
                if (pair.Key != _leader && pair.Key.IsAlive())
                {
                    if (pair.Key.IsEngaged() || pair.Key.IsReturningHome())
                        return false;
                }
            }

            return true;
        }

        public Creature GetLeader() { return _leader; }
        public long GetLeaderSpawnId() { return _leaderSpawnId; }
        public bool IsEmpty() { return _members.Empty(); }
        public bool IsFormed() { return _formed; }
        public bool IsLeader(Creature creature) { return _leader == creature; }

        public bool HasMember(Creature member) { return _members.ContainsKey(member); }
    }
}