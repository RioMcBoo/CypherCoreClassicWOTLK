// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Cache;
using Game.Entities;
using Game.Guilds;
using Game.Maps;
using Game.Networking;
using Game.Networking.Packets;
using System;

namespace Game
{
    public partial class WorldSession
    {
        [WorldPacketHandler(ClientOpcodes.CalendarGet)]
        void HandleCalendarGetCalendar(CalendarGetCalendar calendarGetCalendar)
        {
            ObjectGuid guid = GetPlayer().GetGUID();

            CalendarSendCalendar packet = new();
            packet.RealmTime = LoopTime.RealmTime;

            var playerInvites = Global.CalendarMgr.GetPlayerInvites(guid);
            foreach (var invite in playerInvites)
            {
                CalendarSendCalendarInviteInfo inviteInfo = new();
                inviteInfo.EventID = invite.EventId;
                inviteInfo.InviteID = invite.InviteId;
                inviteInfo.InviterGuid = invite.SenderGuid;
                inviteInfo.Status = invite.Status;
                inviteInfo.Moderator = invite.Rank;
                CalendarEvent calendarEvent = Global.CalendarMgr.GetEvent(invite.EventId);
                if (calendarEvent != null)
                    inviteInfo.InviteType = (byte)(calendarEvent.IsGuildEvent() && calendarEvent.GuildId == _player.GetGuildId() ? 1 : 0);

                packet.Invites.Add(inviteInfo);
            }

            var playerEvents = Global.CalendarMgr.GetPlayerEvents(guid);
            foreach (var calendarEvent in playerEvents)
            {
                CalendarSendCalendarEventInfo eventInfo = new();
                eventInfo.EventID = calendarEvent.EventId;
                eventInfo.Date = calendarEvent.Date;
                //eventInfo.Date += GetTimezoneOffset();
                eventInfo.EventClubID = calendarEvent.GuildId;
                eventInfo.EventName = calendarEvent.Title;
                eventInfo.EventType = calendarEvent.EventType;
                eventInfo.Flags = calendarEvent.Flags;
                eventInfo.OwnerGuid = calendarEvent.OwnerGuid;
                eventInfo.TextureID = calendarEvent.TextureId;

                packet.Events.Add(eventInfo);
            }
            
            foreach (InstanceLock instanceLock in Global.InstanceLockMgr.GetInstanceLocksForPlayer(_player.GetGUID()))
            {
                CalendarSendCalendarRaidLockoutInfo lockoutInfo = new();
                lockoutInfo.MapID = instanceLock.GetMapId();
                lockoutInfo.DifficultyID = instanceLock.GetDifficultyId();
                lockoutInfo.ExpireTime = Time.Max(instanceLock.GetEffectiveExpiryTime() - LoopTime.ServerTime, TimeSpan.Zero);
                lockoutInfo.InstanceID = instanceLock.GetInstanceId();

                packet.RaidLockouts.Add(lockoutInfo);
            }

            //foreach (InstanceLock instanceLock in Global.InstanceLockMgr.GetInstanceResetsForPlayer(_player.GetGUID()))
            //{
            //    CalendarSendCalendarRaidResetsInfo resetsInfo = new();
            //    lockoutInfo.MapID = instanceLock.GetMapId();
            //    lockoutInfo.DifficultyID = instanceLock.GetDifficultyId();
            //    lockoutInfo.ExpireTime = (Seconds)Time.Max(instanceLock.GetEffectiveExpiryTime() - LoopTime.Current, TimeSpan.Zero);
            //    lockoutInfo.InstanceID = instanceLock.GetInstanceId();

            //    packet.RaidResets.Add(lockoutInfo);
            //}

            SendPacket(packet);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarGetEvent)]
        void HandleCalendarGetEvent(CalendarGetEvent calendarGetEvent)
        {
            CalendarEvent calendarEvent = Global.CalendarMgr.GetEvent(calendarGetEvent.EventID);
            if (calendarEvent != null)
                Global.CalendarMgr.SendCalendarEvent(GetPlayer().GetGUID(), calendarEvent, CalendarSendEventType.Get);
            else
                Global.CalendarMgr.SendCalendarCommandResult(GetPlayer().GetGUID(), CalendarError.EventInvalid);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarCommunityInvite)]
        void HandleCalendarCommunityInvite(CalendarCommunityInviteRequest calendarCommunityInvite)
        {
            Guild guild = Global.GuildMgr.GetGuildById(GetPlayer().GetGuildId());
            if (guild != null)
                guild.MassInviteToEvent(this, calendarCommunityInvite.MinLevel, calendarCommunityInvite.MaxLevel, (GuildRankOrder)calendarCommunityInvite.MaxRankOrder);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarAddEvent)]
        void HandleCalendarAddEvent(CalendarAddEvent calendarAddEvent)
        {
            ObjectGuid guid = GetPlayer().GetGUID();

            //calendarAddEvent.EventInfo.Time -= GetTimezoneOffset();

            // prevent events in the past
            if (calendarAddEvent.EventInfo.GameTime < LoopTime.RealmTime)
            {
                Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventPassed);
                return;
            }

            // If the event is a guild event, check if the player is in a guild
            if (CalendarEvent.IsGuildEvent(calendarAddEvent.EventInfo.Flags) || CalendarEvent.IsGuildAnnouncement(calendarAddEvent.EventInfo.Flags))
            {
                if (_player.GetGuildId() == 0)
                {
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.GuildPlayerNotInGuild);
                    return;
                }
            }

            // Check if the player reached the max number of events allowed to create
            if (CalendarEvent.IsGuildEvent(calendarAddEvent.EventInfo.Flags) || CalendarEvent.IsGuildAnnouncement(calendarAddEvent.EventInfo.Flags))
            {
                if (Global.CalendarMgr.GetGuildEvents(_player.GetGuildId()).Count >= SharedConst.CalendarMaxGuildEvents)
                {
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.GuildEventsExceeded);
                    return;
                }
            }
            else
            {
                if (Global.CalendarMgr.GetEventsCreatedBy(guid).Count >= SharedConst.CalendarMaxEvents)
                {
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventsExceeded);
                    return;
                }
            }

            if (GetCalendarEventCreationCooldown() > LoopTime.ServerTime)
            {
                Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.Internal);
                return;
            }

            SetCalendarEventCreationCooldown(LoopTime.ServerTime + SharedConst.CalendarCreateEventCooldown);

            CalendarEvent calendarEvent = new(Global.CalendarMgr.GetFreeEventId(), guid, 0, calendarAddEvent.EventInfo.EventType, calendarAddEvent.EventInfo.TextureID,
                calendarAddEvent.EventInfo.GameTime, calendarAddEvent.EventInfo.Flags, calendarAddEvent.EventInfo.Title, calendarAddEvent.EventInfo.Description, RealmTime.Zero);

            if (calendarEvent.IsGuildEvent() || calendarEvent.IsGuildAnnouncement())
                calendarEvent.GuildId = _player.GetGuildId();

            if (calendarEvent.IsGuildAnnouncement())
            {
                CalendarInvite invite = new(0, calendarEvent.EventId, ObjectGuid.Empty, guid, (RealmTime)SharedConst.CalendarDefaultResponseTime, CalendarInviteStatus.NotSignedUp, CalendarModerationRank.Player, "");
                // WARNING: By passing pointer to a local variable, the underlying method(s) must NOT perform any kind
                // of storage of the pointer as it will lead to memory corruption
                Global.CalendarMgr.AddInvite(calendarEvent, invite);
            }
            else
            {
                SQLTransaction trans = null;
                if (calendarAddEvent.EventInfo.Invites.Count > 1)
                    trans = new SQLTransaction();

                for (int i = 0; i < calendarAddEvent.EventInfo.Invites.Count; ++i)
                {
                    CalendarInvite invite = new(Global.CalendarMgr.GetFreeInviteId(), calendarEvent.EventId,
                        calendarAddEvent.EventInfo.Invites[i].Guid, guid, (RealmTime)SharedConst.CalendarDefaultResponseTime, (CalendarInviteStatus)calendarAddEvent.EventInfo.Invites[i].Status,
                        (CalendarModerationRank)calendarAddEvent.EventInfo.Invites[i].Moderator, "");
                    Global.CalendarMgr.AddInvite(calendarEvent, invite, trans);
                }

                if (calendarAddEvent.EventInfo.Invites.Count > 1)
                    DB.Characters.CommitTransaction(trans);
            }

            Global.CalendarMgr.AddEvent(calendarEvent, CalendarSendEventType.Add);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarUpdateEvent)]
        void HandleCalendarUpdateEvent(CalendarUpdateEvent calendarUpdateEvent)
        {
            //calendarUpdateEvent.EventInfo.Time -= GetTimezoneOffset();

            // prevent events in the past
            if (calendarUpdateEvent.EventInfo.Time < LoopTime.RealmTime)
                return;

            CalendarEvent calendarEvent = Global.CalendarMgr.GetEvent(calendarUpdateEvent.EventInfo.EventID);
            if (calendarEvent != null)
            {
                RealmTime oldEventTime = calendarEvent.Date;

                calendarEvent.EventType = calendarUpdateEvent.EventInfo.EventType;
                calendarEvent.Flags = calendarUpdateEvent.EventInfo.Flags;
                calendarEvent.Date = calendarUpdateEvent.EventInfo.Time;
                calendarEvent.TextureId = calendarUpdateEvent.EventInfo.TextureID;
                calendarEvent.Title = calendarUpdateEvent.EventInfo.Title;
                calendarEvent.Description = calendarUpdateEvent.EventInfo.Description;

                Global.CalendarMgr.UpdateEvent(calendarEvent);
                Global.CalendarMgr.SendCalendarEventUpdateAlert(calendarEvent, oldEventTime);
            }
            else
                Global.CalendarMgr.SendCalendarCommandResult(_player.GetGUID(), CalendarError.EventInvalid);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarRemoveEvent)]
        void HandleCalendarRemoveEvent(CalendarRemoveEvent calendarRemoveEvent)
        {
            ObjectGuid guid = GetPlayer().GetGUID();
            Global.CalendarMgr.RemoveEvent(calendarRemoveEvent.EventID, guid);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarCopyEvent)]
        void HandleCalendarCopyEvent(CalendarCopyEvent calendarCopyEvent)
        {
            ObjectGuid guid = GetPlayer().GetGUID();

            //calendarCopyEvent.Date -= GetTimezoneOffset();

            // prevent events in the past
            if (calendarCopyEvent.Date < LoopTime.RealmTime)
            {
                Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventPassed);
                return;
            }

            CalendarEvent oldEvent = Global.CalendarMgr.GetEvent(calendarCopyEvent.EventID);
            if (oldEvent != null)
            {
                // Ensure that the player has access to the event
                if (oldEvent.IsGuildEvent() || oldEvent.IsGuildAnnouncement())
                {
                    if (oldEvent.GuildId != _player.GetGuildId())
                    {
                        Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventInvalid);
                        return;
                    }
                }
                else
                {
                    if (oldEvent.OwnerGuid != guid)
                    {
                        Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventInvalid);
                        return;
                    }
                }

                // Check if the player reached the max number of events allowed to create
                if (oldEvent.IsGuildEvent() || oldEvent.IsGuildAnnouncement())
                {
                    if (Global.CalendarMgr.GetGuildEvents(_player.GetGuildId()).Count >= SharedConst.CalendarMaxGuildEvents)
                    {
                        Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.GuildEventsExceeded);
                        return;
                    }
                }
                else
                {
                    if (Global.CalendarMgr.GetEventsCreatedBy(guid).Count >= SharedConst.CalendarMaxEvents)
                    {
                        Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventsExceeded);
                        return;
                    }
                }

                if (GetCalendarEventCreationCooldown() > LoopTime.ServerTime)
                {
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.Internal);
                    return;
                }

                SetCalendarEventCreationCooldown(LoopTime.ServerTime + SharedConst.CalendarCreateEventCooldown);

                CalendarEvent newEvent = new(oldEvent, Global.CalendarMgr.GetFreeEventId());
                newEvent.Date = calendarCopyEvent.Date;
                Global.CalendarMgr.AddEvent(newEvent, CalendarSendEventType.Copy);

                var invites = Global.CalendarMgr.GetEventInvites(calendarCopyEvent.EventID);
                SQLTransaction trans = null;
                if (invites.Count > 1)
                    trans = new SQLTransaction();

                foreach (var invite in invites)
                    Global.CalendarMgr.AddInvite(newEvent, new CalendarInvite(invite, Global.CalendarMgr.GetFreeInviteId(), newEvent.EventId), trans);

                if (invites.Count > 1)
                    DB.Characters.CommitTransaction(trans);
                // should we change owner when somebody makes a copy of event owned by another person?
            }
            else
                Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventInvalid);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarInvite)]
        void HandleCalendarInvite(CalendarInvitePkt calendarInvite)
        {
            ObjectGuid playerGuid = GetPlayer().GetGUID();

            long? eventId = null;
            if (!calendarInvite.Creating)
                eventId = calendarInvite.EventID;

            bool isSignUp = calendarInvite.IsSignUp;

            string inviteeName = calendarInvite.Name;

            if (!ObjectManager.NormalizePlayerName(ref calendarInvite.Name))
                return;

            var createInvite = (ObjectGuid inviteeGuid, Team inviteeTeam, long inviteeGuildId, bool inviteeIsIngoring) =>
            {
                if (_player == null || _player.GetGUID() != playerGuid)
                    return;

                if (_player.GetTeam() != inviteeTeam && !WorldConfig.Values[WorldCfg.AllowTwoSideInteractionCalendar].Bool)
                {
                    Global.CalendarMgr.SendCalendarCommandResult(playerGuid, CalendarError.NotAllied);
                    return;
                }

                if (inviteeIsIngoring)
                {
                    Global.CalendarMgr.SendCalendarCommandResult(playerGuid, CalendarError.IgnoringYouS, inviteeName);
                    return;
                }

                if (eventId.HasValue)
                {
                    CalendarEvent calendarEvent = Global.CalendarMgr.GetEvent(eventId.Value);
                    if (calendarEvent != null)
                    {
                        if (calendarEvent.IsGuildEvent() && calendarEvent.GuildId == inviteeGuildId)
                        {
                            // we can't invite guild members to guild events
                            Global.CalendarMgr.SendCalendarCommandResult(playerGuid, CalendarError.NoGuildInvites);
                            return;
                        }

                        CalendarInvite invite = new CalendarInvite(Global.CalendarMgr.GetFreeInviteId(), eventId.Value, inviteeGuid, playerGuid, (RealmTime)SharedConst.CalendarDefaultResponseTime, CalendarInviteStatus.Invited, CalendarModerationRank.Player, "");
                        Global.CalendarMgr.AddInvite(calendarEvent, invite);
                    }
                    else
                        Global.CalendarMgr.SendCalendarCommandResult(playerGuid, CalendarError.EventInvalid);
                }
                else
                {
                    if (isSignUp && inviteeGuildId == _player.GetGuildId())
                    {
                        Global.CalendarMgr.SendCalendarCommandResult(playerGuid, CalendarError.NoGuildInvites);
                        return;
                    }

                    CalendarInvite invite = new(Global.CalendarMgr.GetFreeInviteId(), 0L, inviteeGuid, playerGuid, (RealmTime)SharedConst.CalendarDefaultResponseTime, CalendarInviteStatus.Invited, CalendarModerationRank.Player, "");
                    Global.CalendarMgr.SendCalendarEventInvite(invite);
                }
            };

            Player player = Global.ObjAccessor.FindConnectedPlayerByName(calendarInvite.Name);
            if (player != null)
            {
                // Invitee is online
                createInvite(player.GetGUID(), player.GetTeam(), player.GetGuildId(), player.GetSocial().HasIgnore(playerGuid, GetAccountGUID()));
            }
            else
            {
                // Invitee offline, get data from storage
                CharacterCacheEntry characterInfo = Global.CharacterCacheStorage.GetCharacterCacheByName(inviteeName);
                if (characterInfo == null)
                {
                    Global.CalendarMgr.SendCalendarCommandResult(playerGuid, CalendarError.PlayerNotFound);
                    return;
                }

                var inviteeGuid = characterInfo.Guid;
                var inviteeTeam = Player.TeamForRace(characterInfo.RaceId);
                var inviteeGuildId = characterInfo.GuildId;
                var continuation = createInvite;
                GetQueryProcessor().AddCallback(DB.Characters.AsyncQuery(new PreparedStatement($"SELECT 1 FROM character_social cs INNER JOIN characters friend_character ON cs.friend = friend_character.guid WHERE cs.guid = {characterInfo.Guid.GetCounter()} AND friend_character.account = {characterInfo.AccountId} AND (cs.flags & {(uint)SocialFlag.Ignored}) <> 0")))
                    .WithCallback(result =>
                    {
                        bool isIgnoring = result != null;
                        continuation(inviteeGuid, inviteeTeam, inviteeGuildId, isIgnoring);
                    });
            }
        }

        [WorldPacketHandler(ClientOpcodes.CalendarEventSignUp)]
        void HandleCalendarEventSignup(CalendarEventSignUp calendarEventSignUp)
        {
            ObjectGuid guid = GetPlayer().GetGUID();

            CalendarEvent calendarEvent = Global.CalendarMgr.GetEvent(calendarEventSignUp.EventID);
            if (calendarEvent != null)
            {
                if (calendarEvent.IsGuildEvent() && calendarEvent.GuildId != GetPlayer().GetGuildId())
                {
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.GuildPlayerNotInGuild);
                    return;
                }

                CalendarInviteStatus status = calendarEventSignUp.Tentative ? CalendarInviteStatus.Tentative : CalendarInviteStatus.SignedUp;
                CalendarInvite invite = new(Global.CalendarMgr.GetFreeInviteId(), calendarEventSignUp.EventID, guid, guid, LoopTime.RealmTime, status, CalendarModerationRank.Player, "");
                Global.CalendarMgr.AddInvite(calendarEvent, invite);
                Global.CalendarMgr.SendCalendarClearPendingAction(guid);
            }
            else
                Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventInvalid);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarRsvp)]
        void HandleCalendarRsvp(HandleCalendarRsvp calendarRSVP)
        {
            ObjectGuid guid = GetPlayer().GetGUID();

            CalendarEvent calendarEvent = Global.CalendarMgr.GetEvent(calendarRSVP.EventID);
            if (calendarEvent != null)
            {
                // i think we still should be able to remove self from locked events
                if (calendarRSVP.Status != CalendarInviteStatus.Removed && calendarEvent.IsLocked())
                {
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventLocked);
                    return;
                }

                CalendarInvite invite = Global.CalendarMgr.GetInvite(calendarRSVP.InviteID);
                if (invite != null)
                {
                    invite.Status = calendarRSVP.Status;
                    invite.ResponseTime = LoopTime.RealmTime;

                    Global.CalendarMgr.UpdateInvite(invite);
                    Global.CalendarMgr.SendCalendarEventStatus(calendarEvent, invite);
                    Global.CalendarMgr.SendCalendarClearPendingAction(guid);
                }
                else
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.NoInvite); // correct?
            }
            else
                Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventInvalid);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarRemoveInvite)]
        void HandleCalendarEventRemoveInvite(CalendarRemoveInvite calendarRemoveInvite)
        {
            ObjectGuid guid = GetPlayer().GetGUID();

            CalendarEvent calendarEvent = Global.CalendarMgr.GetEvent(calendarRemoveInvite.EventID);
            if (calendarEvent != null)
            {
                if (calendarEvent.OwnerGuid == calendarRemoveInvite.Guid)
                {
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.DeleteCreatorFailed);
                    return;
                }

                Global.CalendarMgr.RemoveInvite(calendarRemoveInvite.InviteID, calendarRemoveInvite.EventID, guid);
            }
            else
                Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.NoInvite);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarStatus)]
        void HandleCalendarStatus(CalendarStatus calendarStatus)
        {
            ObjectGuid guid = GetPlayer().GetGUID();

            CalendarEvent calendarEvent = Global.CalendarMgr.GetEvent(calendarStatus.EventID);
            if (calendarEvent != null)
            {
                CalendarInvite invite = Global.CalendarMgr.GetInvite(calendarStatus.InviteID);
                if (invite != null)
                {
                    invite.Status = (CalendarInviteStatus)calendarStatus.Status;

                    Global.CalendarMgr.UpdateInvite(invite);
                    Global.CalendarMgr.SendCalendarEventStatus(calendarEvent, invite);
                    Global.CalendarMgr.SendCalendarClearPendingAction(calendarStatus.Guid);
                }
                else
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.NoInvite); // correct?
            }
            else
                Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventInvalid);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarModeratorStatus)]
        void HandleCalendarModeratorStatus(CalendarModeratorStatusQuery calendarModeratorStatus)
        {
            ObjectGuid guid = GetPlayer().GetGUID();

            CalendarEvent calendarEvent = Global.CalendarMgr.GetEvent(calendarModeratorStatus.EventID);
            if (calendarEvent != null)
            {
                CalendarInvite invite = Global.CalendarMgr.GetInvite(calendarModeratorStatus.InviteID);
                if (invite != null)
                {
                    invite.Rank = (CalendarModerationRank)calendarModeratorStatus.Status;
                    Global.CalendarMgr.UpdateInvite(invite);
                    Global.CalendarMgr.SendCalendarEventModeratorStatusAlert(calendarEvent, invite);
                }
                else
                    Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.NoInvite); // correct?
            }
            else
                Global.CalendarMgr.SendCalendarCommandResult(guid, CalendarError.EventInvalid);
        }

        [WorldPacketHandler(ClientOpcodes.CalendarComplain)]
        void HandleCalendarComplain(CalendarComplain calendarComplain)
        {
            // what to do with complains?
        }

        [WorldPacketHandler(ClientOpcodes.CalendarGetNumPending)]
        void HandleCalendarGetNumPending(CalendarGetNumPending calendarGetNumPending)
        {
            ObjectGuid guid = GetPlayer().GetGUID();
            uint pending = Global.CalendarMgr.GetPlayerNumPending(guid);

            SendPacket(new CalendarSendNumPending(pending));
        }

        [WorldPacketHandler(ClientOpcodes.SetSavedInstanceExtend)]
        void HandleSetSavedInstanceExtend(SetSavedInstanceExtend setSavedInstanceExtend)
        {
            // cannot modify locks currently in use
            if (_player.GetMapId() == setSavedInstanceExtend.MapID)
                return;

            var expiryTimes = Global.InstanceLockMgr.UpdateInstanceLockExtensionForPlayer(_player.GetGUID(), new MapDb2Entries(setSavedInstanceExtend.MapID, setSavedInstanceExtend.DifficultyID), setSavedInstanceExtend.Extend);

            if (expiryTimes.Item1 == DateTime.MinValue)
                return;

            CalendarRaidLockoutUpdated calendarRaidLockoutUpdated = new();
            calendarRaidLockoutUpdated.RealmTime = LoopTime.RealmTime;
            calendarRaidLockoutUpdated.MapID = setSavedInstanceExtend.MapID;
            calendarRaidLockoutUpdated.DifficultyID = setSavedInstanceExtend.DifficultyID;
            calendarRaidLockoutUpdated.OldTimeRemaining = Time.Max(expiryTimes.Item1 - LoopTime.ServerTime, TimeSpan.Zero);
            calendarRaidLockoutUpdated.NewTimeRemaining = Time.Max(expiryTimes.Item2 - LoopTime.ServerTime, TimeSpan.Zero);
            SendPacket(calendarRaidLockoutUpdated);
        }

        public void SendCalendarRaidLockoutAdded(InstanceLock instanceLock)
        {
            CalendarRaidLockoutAdded calendarRaidLockoutAdded = new();
            calendarRaidLockoutAdded.InstanceID = instanceLock.GetInstanceId();
            calendarRaidLockoutAdded.RealmTime = LoopTime.RealmTime;
            calendarRaidLockoutAdded.MapID = instanceLock.GetMapId();
            calendarRaidLockoutAdded.DifficultyID = instanceLock.GetDifficultyId();
            calendarRaidLockoutAdded.TimeRemaining = instanceLock.GetEffectiveExpiryTime() - LoopTime.ServerTime;
            SendPacket(calendarRaidLockoutAdded);
        }

        void SendCalendarRaidLockoutRemoved(InstanceLock instanceLock)
        {
            CalendarRaidLockoutRemoved calendarRaidLockoutRemoved = new();
            calendarRaidLockoutRemoved.InstanceID = instanceLock.GetInstanceId();
            calendarRaidLockoutRemoved.MapID = instanceLock.GetMapId();
            calendarRaidLockoutRemoved.DifficultyID = instanceLock.GetDifficultyId();
            SendPacket(calendarRaidLockoutRemoved);
        }
    }
}
