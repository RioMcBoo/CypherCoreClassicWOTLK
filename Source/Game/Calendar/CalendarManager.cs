// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Entities;
using Game.Guilds;
using Game.Mails;
using Game.Networking;
using Game.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public class CalendarManager : Singleton<CalendarManager>
    {
        CalendarManager()
        {
            _events = new List<CalendarEvent>();
            _invites = new MultiMap<long,CalendarInvite>();
        }

        public void LoadFromDB()
        {
            uint count = 0;
            RelativeTime oldMSTime = Time.NowRelative;

            _maxEventId = 0;
            _maxInviteId = 0;

            {
                //                                              0        1      2      3            4          5          6     7      8
                SQLResult result = DB.Characters.Query("SELECT EventID, Owner, Title, Description, EventType, TextureID, Date, Flags, LockDate FROM calendar_events");

                if (!result.IsEmpty())
                {
                    do
                    {
                        long eventID = result.Read<long>(0);
                        ObjectGuid ownerGUID = ObjectGuid.Create(HighGuid.Player, result.Read<long>(1));
                        string title = result.Read<string>(2);
                        string description = result.Read<string>(3);
                        CalendarEventType type = (CalendarEventType)result.Read<byte>(4);
                        int textureID = result.Read<int>(5);
                        RealmTime date = (RealmTime)(UnixTime64)result.Read<long>(6);
                        CalendarFlags flags = (CalendarFlags)result.Read<uint>(7);
                        RealmTime lockDate = (RealmTime)(UnixTime64)result.Read<long>(8);
                        long guildID = 0;

                        if (flags.HasAnyFlag(CalendarFlags.GuildEvent) || flags.HasAnyFlag(CalendarFlags.WithoutInvites))
                            guildID = Global.CharacterCacheStorage.GetCharacterGuildIdByGuid(ownerGUID);

                        CalendarEvent calendarEvent = new(eventID, ownerGUID, guildID, type, textureID, date, flags, title, description, lockDate);
                        _events.Add(calendarEvent);

                        _maxEventId = Math.Max(_maxEventId, eventID);

                        ++count;
                    }
                    while (result.NextRow());
                }
                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} calendar events in {Time.Diff(oldMSTime)} ms.");
            }

            count = 0;
            oldMSTime = Time.NowRelative;

            {
                //                                    0         1        2        3       4       5             6               7
                SQLResult result = DB.Characters.Query("SELECT InviteID, EventID, Invitee, Sender, Status, ResponseTime, ModerationRank, Note FROM calendar_invites");

                if (!result.IsEmpty())
                {
                    do
                    {
                        long inviteId = result.Read<long>(0);
                        long eventId = result.Read<long>(1);
                        ObjectGuid invitee = ObjectGuid.Create(HighGuid.Player, result.Read<long>(2));
                        ObjectGuid senderGUID = ObjectGuid.Create(HighGuid.Player, result.Read<long>(3));
                        CalendarInviteStatus status = (CalendarInviteStatus)result.Read<byte>(4);
                        RealmTime responseTime = (RealmTime)(UnixTime64)result.Read<long>(5);
                        CalendarModerationRank rank = (CalendarModerationRank)result.Read<byte>(6);
                        string note = result.Read<string>(7);

                        CalendarInvite invite = new(inviteId, eventId, invitee, senderGUID, responseTime, status, rank, note);
                        _invites.Add(eventId, invite);

                        _maxInviteId = Math.Max(_maxInviteId, inviteId);

                        ++count;
                    }
                    while (result.NextRow());
                }

                Log.outInfo(LogFilter.ServerLoading, $"Loaded {count} calendar invites in {Time.Diff(oldMSTime)} ms.");
            }

            for (long i = 1; i < _maxEventId; ++i)
            {
                if (GetEvent(i) == null)
                    _freeEventIds.Add(i);
            }

            for (long i = 1; i < _maxInviteId; ++i)
            {
                if (GetInvite(i) == null)
                    _freeInviteIds.Add(i);
            }
        }

        public void AddEvent(CalendarEvent calendarEvent, CalendarSendEventType sendType)
        {
            _events.Add(calendarEvent);
            UpdateEvent(calendarEvent);
            SendCalendarEvent(calendarEvent.OwnerGuid, calendarEvent, sendType);
        }

        public void AddInvite(CalendarEvent calendarEvent, CalendarInvite invite, SQLTransaction trans = null)
        {
            if (!calendarEvent.IsGuildAnnouncement() && calendarEvent.OwnerGuid != invite.InviteeGuid)
                SendCalendarEventInvite(invite);

            if (!calendarEvent.IsGuildEvent() || invite.InviteeGuid == calendarEvent.OwnerGuid)
                SendCalendarEventInviteAlert(calendarEvent, invite);

            if (!calendarEvent.IsGuildAnnouncement())
            {
                _invites.Add(invite.EventId, invite);
                UpdateInvite(invite, trans);
            }
        }

        public void RemoveEvent(long eventId, ObjectGuid remover)
        {
            CalendarEvent calendarEvent = GetEvent(eventId);

            if (calendarEvent == null)
            {
                SendCalendarCommandResult(remover, CalendarError.EventInvalid);
                return;
            }

            RemoveEvent(calendarEvent, remover);
        }

        void RemoveEvent(CalendarEvent calendarEvent, ObjectGuid remover)
        {
            if (calendarEvent == null)
            {
                SendCalendarCommandResult(remover, CalendarError.EventInvalid);
                return;
            }

            SendCalendarEventRemovedAlert(calendarEvent);

            SQLTransaction trans = new();
            PreparedStatement stmt;

            var eventInvites = _invites[calendarEvent.EventId];
            for (int i = 0; i < eventInvites.Count; ++i)
            {
                CalendarInvite invite = eventInvites[i];
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CALENDAR_INVITE);
                stmt.SetInt64(0, invite.InviteId);
                trans.Append(stmt);

                // guild events only? check invite status here?
                // When an event is deleted, all invited (accepted/declined? - verify) guildies are notified via in-game mail. (wowwiki)
                if (!remover.IsEmpty() && invite.InviteeGuid != remover)
                {
                    MailDraft mail = new(calendarEvent.BuildCalendarMailSubject(remover), calendarEvent.BuildCalendarMailBody(Global.ObjAccessor.FindConnectedPlayer(invite.InviteeGuid)));
                    mail.SendMailTo(trans, new MailReceiver(invite.InviteeGuid.GetCounter()), new MailSender(calendarEvent), MailCheckFlags.Copied);
                }
            }

            _invites.Remove(calendarEvent.EventId);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CALENDAR_EVENT);
            stmt.SetInt64(0, calendarEvent.EventId);
            trans.Append(stmt);
            DB.Characters.CommitTransaction(trans);

            _events.Remove(calendarEvent);
        }

        public void RemoveInvite(long inviteId, long eventId, ObjectGuid remover)
        {
            CalendarEvent calendarEvent = GetEvent(eventId);

            if (calendarEvent == null)
                return;

            CalendarInvite calendarInvite = null;
            foreach (var invite in _invites[eventId])
            {
                if (invite.InviteId == inviteId)
                {
                    calendarInvite = invite;
                    break;
                }
            }

            if (calendarInvite == null)
                return;

            SQLTransaction trans = new();
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CALENDAR_INVITE);
            stmt.SetInt64(0, calendarInvite.InviteId);
            trans.Append(stmt);
            DB.Characters.CommitTransaction(trans);

            if (!calendarEvent.IsGuildEvent())
                SendCalendarEventInviteRemoveAlert(calendarInvite.InviteeGuid, calendarEvent, CalendarInviteStatus.Removed);

            SendCalendarEventInviteRemove(calendarEvent, calendarInvite, (uint)calendarEvent.Flags);

            // we need to find out how to use CALENDAR_INVITE_REMOVED_MAIL_SUBJECT to force client to display different mail
            //if (itr._invitee != remover)
            //    MailDraft(calendarEvent.BuildCalendarMailSubject(remover), calendarEvent.BuildCalendarMailBody())
            //        .SendMailTo(trans, MailReceiver(itr.GetInvitee()), calendarEvent, MAIL_CHECK_MASK_COPIED);

            _invites.Remove(eventId, calendarInvite);
        }

        public void UpdateEvent(CalendarEvent calendarEvent)
        {
            SQLTransaction trans = new();
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_CALENDAR_EVENT);
            stmt.SetInt64(0, calendarEvent.EventId);
            stmt.SetInt64(1, calendarEvent.OwnerGuid.GetCounter());
            stmt.SetString(2, calendarEvent.Title);
            stmt.SetString(3, calendarEvent.Description);
            stmt.SetUInt8(4, (byte)calendarEvent.EventType);
            stmt.SetInt32(5, calendarEvent.TextureId);
            stmt.SetInt64(6, (UnixTime64)calendarEvent.Date);
            stmt.SetUInt32(7, (uint)calendarEvent.Flags);
            stmt.SetInt64(8, (UnixTime64)calendarEvent.LockDate);
            trans.Append(stmt);
            DB.Characters.CommitTransaction(trans);
        }

        public void UpdateInvite(CalendarInvite invite, SQLTransaction trans = null)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_CALENDAR_INVITE);
            stmt.SetInt64(0, invite.InviteId);
            stmt.SetInt64(1, invite.EventId);
            stmt.SetInt64(2, invite.InviteeGuid.GetCounter());
            stmt.SetInt64(3, invite.SenderGuid.GetCounter());
            stmt.SetUInt8(4, (byte)invite.Status);
            stmt.SetInt64(5, (UnixTime64)invite.ResponseTime);
            stmt.SetUInt8(6, (byte)invite.Rank);
            stmt.SetString(7, invite.Note);
            DB.Characters.ExecuteOrAppend(trans, stmt);
        }

        public void RemoveAllPlayerEventsAndInvites(ObjectGuid guid)
        {
            foreach (var calendarEvent in _events)
            {
                if (calendarEvent.OwnerGuid == guid)
                    RemoveEvent(calendarEvent.EventId, ObjectGuid.Empty); // don't send mail if removing a character
            }

            List<CalendarInvite> playerInvites = GetPlayerInvites(guid);
            foreach (var calendarInvite in playerInvites)
                RemoveInvite(calendarInvite.InviteId, calendarInvite.EventId, guid);
        }

        public void RemovePlayerGuildEventsAndSignups(ObjectGuid guid, long guildId)
        {
            foreach (var calendarEvent in _events)
            {
                if (calendarEvent.OwnerGuid == guid && (calendarEvent.IsGuildEvent() || calendarEvent.IsGuildAnnouncement()))
                    RemoveEvent(calendarEvent.EventId, guid);
            }

            List<CalendarInvite> playerInvites = GetPlayerInvites(guid);
            foreach (var playerCalendarEvent in playerInvites)
            {
                CalendarEvent calendarEvent = GetEvent(playerCalendarEvent.EventId);
                if (calendarEvent != null)
                {
                    if (calendarEvent.IsGuildEvent() && calendarEvent.GuildId == guildId)
                    {
                        RemoveInvite(playerCalendarEvent.InviteId, playerCalendarEvent.EventId, guid);
                    }
                }
            }
        }

        public CalendarEvent GetEvent(long eventId)
        {
            foreach (var calendarEvent in _events)
            {
                if (calendarEvent.EventId == eventId)
                    return calendarEvent;
            }

            Log.outDebug(LogFilter.Calendar, $"CalendarMgr:GetEvent: {eventId} not found!");
            return null;
        }

        public CalendarInvite GetInvite(long inviteId)
        {
            foreach (var calendarEvent in _invites.Values)
            {
                if (calendarEvent.InviteId == inviteId)
                    return calendarEvent;
            }

            Log.outDebug(LogFilter.Calendar, $"CalendarMgr:GetInvite: {inviteId} not found!");
            return null;
        }

        void FreeEventId(long id)
        {
            if (id == _maxEventId)
                --_maxEventId;
            else
                _freeEventIds.Add(id);
        }

        public long GetFreeEventId()
        {
            if (_freeEventIds.Empty())
                return ++_maxEventId;

            long eventId = _freeEventIds.FirstOrDefault();
            _freeEventIds.RemoveAt(0);
            return eventId;
        }

        public void FreeInviteId(long id)
        {
            if (id == _maxInviteId)
                --_maxInviteId;
            else
                _freeInviteIds.Add(id);
        }

        public long GetFreeInviteId()
        {
            if (_freeInviteIds.Empty())
                return ++_maxInviteId;

            long inviteId = _freeInviteIds.FirstOrDefault();
            _freeInviteIds.RemoveAt(0);
            return inviteId;
        }

        public void DeleteOldEvents()
        {
            RealmTime oldEventsTime = LoopTime.RealmTime - SharedConst.CalendarOldEventsDeletionTime;

            foreach (var calendarEvent in _events)
            {
                if (calendarEvent.Date < oldEventsTime)
                    RemoveEvent(calendarEvent, ObjectGuid.Empty);
            }
        }

        public List<CalendarEvent> GetEventsCreatedBy(ObjectGuid guid, bool includeGuildEvents = false)
        {
            List<CalendarEvent> result = new();
            foreach (var calendarEvent in _events)
            {
                if (calendarEvent.OwnerGuid == guid && (includeGuildEvents || (!calendarEvent.IsGuildEvent() && !calendarEvent.IsGuildAnnouncement())))
                    result.Add(calendarEvent);
            }

            return result;
        }

        public List<CalendarEvent> GetGuildEvents(long guildId)
        {
            List<CalendarEvent> result = new();

            if (guildId == 0)
                return result;

            foreach (var calendarEvent in _events)
            {
                if (calendarEvent.IsGuildEvent() || calendarEvent.IsGuildAnnouncement())
                {
                    if (calendarEvent.GuildId == guildId)
                        result.Add(calendarEvent);
                }
            }

            return result;
        }

        public List<CalendarEvent> GetPlayerEvents(ObjectGuid guid)
        {
            List<CalendarEvent> events = new();

            foreach (var pair in _invites)
            {
                if (pair.Value.InviteeGuid == guid)
                {
                    CalendarEvent Event = GetEvent(pair.Key);
                    if (Event != null) // null check added as attempt to fix #11512
                        events.Add(Event);
                }
            }

            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player?.GetGuildId() != 0)
            {
                foreach (var calendarEvent in _events)
                {
                    if (calendarEvent.GuildId == player.GetGuildId())
                        events.Add(calendarEvent);
                }
            }

            return events;
        }

        public List<CalendarInvite> GetEventInvites(long eventId)
        {
            return _invites[eventId];
        }

        public List<CalendarInvite> GetPlayerInvites(ObjectGuid guid)
        {
            List<CalendarInvite> invites = new();

            foreach (var calendarEvent in _invites.Values)
            {
                if (calendarEvent.InviteeGuid == guid)
                    invites.Add(calendarEvent);
            }

            return invites;
        }

        public uint GetPlayerNumPending(ObjectGuid guid)
        {
            List<CalendarInvite> invites = GetPlayerInvites(guid);

            uint pendingNum = 0;
            foreach (var calendarEvent in invites)
            {
                switch (calendarEvent.Status)
                {
                    case CalendarInviteStatus.Invited:
                    case CalendarInviteStatus.Tentative:
                    case CalendarInviteStatus.NotSignedUp:
                        ++pendingNum;
                        break;
                    default:
                        break;
                }
            }

            return pendingNum;
        }

        public void SendCalendarEventInvite(CalendarInvite invite)
        {
            CalendarEvent calendarEvent = GetEvent(invite.EventId);

            ObjectGuid invitee = invite.InviteeGuid;
            Player player = Global.ObjAccessor.FindPlayer(invitee);

            int level = player != null ? player.GetLevel() : Global.CharacterCacheStorage.GetCharacterLevelByGuid(invitee);

            var packetBuilder = (Player receiver) =>
            {
                CalendarInviteAdded packet = new();
                packet.EventID = calendarEvent != null ? calendarEvent.EventId : 0;
                packet.InviteGuid = invitee;
                packet.InviteID = calendarEvent != null ? invite.InviteId : 0;
                packet.Level = (byte)level;
                packet.ResponseTime = invite.ResponseTime;
                //packet.ResponseTime += receiver.GetSession().GetTimezoneOffset();
                packet.Status = invite.Status;
                packet.Type = (byte)(calendarEvent != null ? calendarEvent.IsGuildEvent() ? 1 : 0 : 0); // Correct ?
                packet.ClearPending = calendarEvent != null ? !calendarEvent.IsGuildEvent() : true; // Correct ?

                receiver.SendPacket(packet);
            };

            if (calendarEvent == null) // Pre-invite
            {
                player = Global.ObjAccessor.FindPlayer(invite.SenderGuid);
                if (player != null)
                    packetBuilder(player);
            }
            else
            {
                if (calendarEvent.OwnerGuid != invite.InviteeGuid) // correct?
                {
                    foreach (Player receiver in GetAllEventRelatives(calendarEvent))
                        packetBuilder(receiver);
                }
            }
        }

        public void SendCalendarEventUpdateAlert(CalendarEvent calendarEvent, RealmTime originalDate)
        {
            var packetBuilder = (Player receiver) =>
            {
                CalendarEventUpdatedAlert packet = new();
                packet.ClearPending = calendarEvent.OwnerGuid == receiver.GetGUID();
                packet.Date = calendarEvent.Date;
                //packet.Date += receiver.GetSession().GetTimezoneOffset();
                packet.Description = calendarEvent.Description;
                packet.EventID = calendarEvent.EventId;
                packet.EventClubID = calendarEvent.GuildId;
                packet.EventName = calendarEvent.Title;
                packet.EventType = calendarEvent.EventType;
                packet.Flags = calendarEvent.Flags;
                packet.LockDate = calendarEvent.LockDate; // Always 0 ?
                if (calendarEvent.LockDate != RealmTime.Zero)
                {
                    //packet.LockDate += receiver.GetSession().GetTimezoneOffset();
                }

                packet.OriginalDate = originalDate;
                //packet.OriginalDate += receiver.GetSession().GetTimezoneOffset();
                packet.TextureID = calendarEvent.TextureId;

                receiver.SendPacket(packet);
            };

            foreach (Player receiver in GetAllEventRelatives(calendarEvent))
                packetBuilder(receiver);
        }

        public void SendCalendarEventStatus(CalendarEvent calendarEvent, CalendarInvite invite)
        {
            var packetBuilder = (Player receiver) =>
            {
                CalendarInviteStatusPacket packet = new();
                packet.ClearPending = invite.InviteeGuid == receiver.GetGUID();
                packet.Date = calendarEvent.Date;
                //packet.Date += receiver.GetSession().GetTimezoneOffset();
                packet.EventID = calendarEvent.EventId;
                packet.Flags = calendarEvent.Flags;
                packet.InviteGuid = invite.InviteeGuid;
                packet.ResponseTime = invite.ResponseTime;
                //packet.ResponseTime += receiver.GetSession().GetTimezoneOffset();
                packet.Status = invite.Status;

                receiver.SendPacket(packet);
            };

            foreach (Player receiver in GetAllEventRelatives(calendarEvent))
                packetBuilder(receiver);
        }

        void SendCalendarEventRemovedAlert(CalendarEvent calendarEvent)
        {
            var packetBuilder = (Player receiver) =>
            {
                CalendarEventRemovedAlert packet = new();
                packet.ClearPending = calendarEvent.OwnerGuid == receiver.GetGUID();
                packet.Date = calendarEvent.Date;
                //packet.Date += receiver.GetSession().GetTimezoneOffset();
                packet.EventID = calendarEvent.EventId;

                receiver.SendPacket(packet);
            };

            foreach (Player receiver in GetAllEventRelatives(calendarEvent))
                packetBuilder(receiver);
        }

        void SendCalendarEventInviteRemove(CalendarEvent calendarEvent, CalendarInvite invite, uint flags)
        {
            CalendarInviteRemoved packet = new();
            packet.ClearPending = true; // FIXME
            packet.EventID = calendarEvent.EventId;
            packet.Flags = flags;
            packet.InviteGuid = invite.InviteeGuid;

            SendPacketToAllEventRelatives(packet, calendarEvent);
        }

        public void SendCalendarEventModeratorStatusAlert(CalendarEvent calendarEvent, CalendarInvite invite)
        {
            CalendarModeratorStatus packet = new();
            packet.ClearPending = true; // FIXME
            packet.EventID = calendarEvent.EventId;
            packet.InviteGuid = invite.InviteeGuid;
            packet.Status = invite.Status;

            SendPacketToAllEventRelatives(packet, calendarEvent);
        }

        void SendCalendarEventInviteAlert(CalendarEvent calendarEvent, CalendarInvite invite)
        {
            var packetBuilder = (Player receiver) =>
            {
                CalendarInviteAlert packet = new();
                packet.Date = calendarEvent.Date;
                //packet.Date += receiver.GetSession().GetTimezoneOffset();
                packet.EventID = calendarEvent.EventId;
                packet.EventName = calendarEvent.Title;
                packet.EventType = calendarEvent.EventType;
                packet.Flags = calendarEvent.Flags;
                packet.InviteID = invite.InviteId;
                packet.InvitedByGuid = invite.SenderGuid;
                packet.ModeratorStatus = invite.Rank;
                packet.OwnerGuid = calendarEvent.OwnerGuid;
                packet.Status = invite.Status;
                packet.TextureID = calendarEvent.TextureId;
                packet.EventClubID = calendarEvent.GuildId;

                receiver.SendPacket(packet);
            };

            if (calendarEvent.IsGuildEvent() || calendarEvent.IsGuildAnnouncement())
            {
                Guild guild = Global.GuildMgr.GetGuildById(calendarEvent.GuildId);
                if (guild != null)
                    guild.BroadcastWorker(packetBuilder);
            }
            else
            {
                Player player = Global.ObjAccessor.FindPlayer(invite.InviteeGuid);
                if (player != null)
                    packetBuilder(player);
            }
        }

        public void SendCalendarEvent(ObjectGuid guid, CalendarEvent calendarEvent, CalendarSendEventType sendType)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player == null)
                return;

            CalendarSendEvent packet = new();
            packet.Date = calendarEvent.Date;
            //packet.Date += player.GetSession().GetTimezoneOffset();
            packet.Description = calendarEvent.Description;
            packet.EventID = calendarEvent.EventId;
            packet.EventName = calendarEvent.Title;
            packet.EventType = sendType;
            packet.Flags = calendarEvent.Flags;
            packet.GetEventType = calendarEvent.EventType;
            packet.LockDate = calendarEvent.LockDate; // Always 0 ?
            if (calendarEvent.LockDate != Time.Zero)
            {
                //packet.LockDate += player.GetSession().GetTimezoneOffset();
            }
            packet.OwnerGuid = calendarEvent.OwnerGuid;
            packet.TextureID = calendarEvent.TextureId;
            packet.EventGuildID = calendarEvent.GuildId;

            List<CalendarInvite> eventInviteeList = _invites[calendarEvent.EventId];
            foreach (var calendarInvite in eventInviteeList)
            {
                ObjectGuid inviteeGuid = calendarInvite.InviteeGuid;
                Player invitee = Global.ObjAccessor.FindPlayer(inviteeGuid);

                int inviteeLevel = invitee != null ? invitee.GetLevel() : Global.CharacterCacheStorage.GetCharacterLevelByGuid(inviteeGuid);
                long inviteeGuildId = invitee != null ? invitee.GetGuildId() : Global.CharacterCacheStorage.GetCharacterGuildIdByGuid(inviteeGuid);

                CalendarEventInviteInfo inviteInfo = new();
                inviteInfo.Guid = inviteeGuid;
                inviteInfo.Level = (byte)inviteeLevel;
                inviteInfo.Status = calendarInvite.Status;
                inviteInfo.Moderator = calendarInvite.Rank;
                inviteInfo.InviteType = (byte)(calendarEvent.IsGuildEvent() && calendarEvent.GuildId == inviteeGuildId ? 1 : 0);
                inviteInfo.InviteID = calendarInvite.InviteId;
                inviteInfo.ResponseTime = calendarInvite.ResponseTime;
                //inviteInfo.ResponseTime += player.GetSession().GetTimezoneOffset();
                inviteInfo.Notes = calendarInvite.Note;

                packet.Invites.Add(inviteInfo);
            }

            player.SendPacket(packet);
        }

        void SendCalendarEventInviteRemoveAlert(ObjectGuid guid, CalendarEvent calendarEvent, CalendarInviteStatus status)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
            {
                CalendarInviteRemovedAlert packet = new();
                packet.Date = calendarEvent.Date;
                //packet.Date += player.GetSession().GetTimezoneOffset();
                packet.EventID = calendarEvent.EventId;
                packet.Flags = calendarEvent.Flags;
                packet.Status = status;

                player.SendPacket(packet);
            }
        }

        public void SendCalendarClearPendingAction(ObjectGuid guid)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
                player.SendPacket(new CalendarClearPendingAction());
        }

        public void SendCalendarCommandResult(ObjectGuid guid, CalendarError err, string param = null)
        {
            Player player = Global.ObjAccessor.FindPlayer(guid);
            if (player != null)
            {
                CalendarCommandResult packet = new();
                packet.Command = 1; // FIXME
                packet.Result = err;

                switch (err)
                {
                    case CalendarError.OtherInvitesExceeded:
                    case CalendarError.AlreadyInvitedToEventS:
                    case CalendarError.IgnoringYouS:
                        packet.Name = param;
                        break;
                }

                player.SendPacket(packet);
            }
        }

        void SendPacketToAllEventRelatives(ServerPacket packet, CalendarEvent calendarEvent)
        {
            foreach (Player player in GetAllEventRelatives(calendarEvent))
                player.SendPacket(packet);
        }

        List<Player> GetAllEventRelatives(CalendarEvent calendarEvent)
        {
            List<Player> relatedPlayers = new();

            // Send packet to all guild members
            if (calendarEvent.IsGuildEvent() || calendarEvent.IsGuildAnnouncement())
            {
                Guild guild = Global.GuildMgr.GetGuildById(calendarEvent.GuildId);
                if (guild != null)
                    guild.BroadcastWorker(relatedPlayers.Add);
            }

            // Send packet to all invitees if event is non-guild, in other case only to non-guild invitees (packet was broadcasted for them)
            var invites = _invites.LookupByKey(calendarEvent.EventId);
            foreach (CalendarInvite invite in invites)
            {
                Player player = Global.ObjAccessor.FindConnectedPlayer(invite.InviteeGuid);
                if (player != null)
                {
                    if (!calendarEvent.IsGuildEvent() || player.GetGuildId() != calendarEvent.GuildId)
                        relatedPlayers.Add(player);
                }
            }

            return relatedPlayers;
        }

        List<CalendarEvent> _events;
        MultiMap<long, CalendarInvite> _invites;

        List<long> _freeEventIds = new();
        List<long> _freeInviteIds = new();
        long _maxEventId;
        long _maxInviteId;
    }

    public class CalendarInvite
    {
        public long InviteId { get; set; }
        public long EventId { get; set; }
        public ObjectGuid InviteeGuid { get; set; }
        public ObjectGuid SenderGuid { get; set; }
        /// <summary>
        /// Save ResponseTime as RealmTime to keep <br/>
        /// <text cref="Framework.Constants.SharedConst.CalendarDefaultResponseTime">the minimal WowTime</text><br/>
        /// from time shifting when converting between RealmTime/ServerTime
        /// </summary>
        public RealmTime ResponseTime { get; set; } 
        public CalendarInviteStatus Status { get; set; }
        public CalendarModerationRank Rank { get; set; }
        public string Note { get; set; }

        public CalendarInvite()
        {
            InviteId = 1;
            ResponseTime = RealmTime.Zero;
            Status = CalendarInviteStatus.Invited;
            Rank = CalendarModerationRank.Player;
            Note = "";
        }

        public CalendarInvite(CalendarInvite calendarInvite, long inviteId, long eventId)
        {
            InviteId = inviteId;
            EventId = eventId;
            InviteeGuid = calendarInvite.InviteeGuid;
            SenderGuid = calendarInvite.SenderGuid;
            ResponseTime = calendarInvite.ResponseTime;
            Status = calendarInvite.Status;
            Rank = calendarInvite.Rank;
            Note = calendarInvite.Note;
        }

        public CalendarInvite(long inviteId, long eventId, ObjectGuid invitee, ObjectGuid senderGUID, RealmTime responseTime, CalendarInviteStatus status, CalendarModerationRank rank, string note)
        {
            InviteId = inviteId;
            EventId = eventId;
            InviteeGuid = invitee;
            SenderGuid = senderGUID;
            ResponseTime = responseTime;

            Status = status;
            Rank = rank;
            Note = note;
        }

        ~CalendarInvite()
        {
            if (InviteId != 0 && EventId != 0)
                Global.CalendarMgr.FreeInviteId(InviteId);
        }
    }

    public class CalendarEvent
    {
        public long EventId { get; set; }
        public ObjectGuid OwnerGuid { get; set; }
        public long GuildId { get; set; }
        public CalendarEventType EventType { get; set; }
        public int TextureId { get; set; }
        public RealmTime Date { get; set; }
        public CalendarFlags Flags { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public RealmTime LockDate { get; set; }

        public CalendarEvent(CalendarEvent calendarEvent, long eventId)
        {
            EventId = eventId;
            OwnerGuid = calendarEvent.OwnerGuid;
            GuildId = calendarEvent.GuildId;
            EventType = calendarEvent.EventType;
            TextureId = calendarEvent.TextureId;
            Date = calendarEvent.Date;
            Flags = calendarEvent.Flags;
            LockDate = calendarEvent.LockDate;
            Title = calendarEvent.Title;
            Description = calendarEvent.Description;
        }

        public CalendarEvent(long eventId, ObjectGuid ownerGuid, long guildId, CalendarEventType type, int textureId, RealmTime date, CalendarFlags flags, string title, string description, RealmTime lockDate)
        {
            EventId = eventId;
            OwnerGuid = ownerGuid;
            GuildId = guildId;
            EventType = type;
            TextureId = textureId;
            Date = date;
            Flags = flags;
            LockDate = lockDate;
            Title = title;
            Description = description;
        }

        public CalendarEvent()
        {
            EventId = 1;
            EventType = CalendarEventType.Other;
            TextureId = -1;
            Title = "";
            Description = "";
        }

        public string BuildCalendarMailSubject(ObjectGuid remover)
        {
            return remover + ":" + Title;
        }

        public string BuildCalendarMailBody(Player invitee)
        {
             RealmTime time = Date;

            if (invitee != null)
            {
                //time = Date + invitee.GetSession().GetTimezoneOffset();
            }

            return ((WowTime)time).ToString();
        }

        public bool IsGuildEvent() { return IsGuildEvent(Flags); }
        public bool IsGuildAnnouncement() { return IsGuildAnnouncement(Flags); }
        public bool IsLocked() { return IsLocked(Flags); }

        public static bool IsGuildEvent(CalendarFlags flags) { return flags.HasAnyFlag(CalendarFlags.GuildEvent); }
        public static bool IsGuildAnnouncement(CalendarFlags flags) { return flags.HasAnyFlag(CalendarFlags.WithoutInvites); }
        public static bool IsLocked(CalendarFlags flags) { return flags.HasAnyFlag(CalendarFlags.InvitesLocked); }
    }
}
