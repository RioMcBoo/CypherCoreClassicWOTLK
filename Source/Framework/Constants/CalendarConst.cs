// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Framework.Constants
{
    public enum CalendarMailAnswers
    {
        EventRemovedMailSubject = 0,
        InviteRemovedMailSubject = 0x100
    }

    public enum CalendarFlags
    {
        AllAllowed = 0x001,
        InvitesLocked = 0x010,
        WithoutInvites = 0x040,
        GuildEvent = 0x400
    }

    public enum CalendarModerationRank : byte
    {
        Player = 0,
        Moderator = 1,
        Owner = 2
    }

    public enum CalendarSendEventType : byte
    {
        Get = 0,
        Add = 1,
        Copy = 2
    }

    public enum CalendarEventType : byte
    {
        Raid = 0,
        Dungeon = 1,
        Pvp = 2,
        Meeting = 3,
        Other = 4,
        Heroic = 5
    }

    public enum CalendarRepeatType
    {
        Never = 0,
        Weekly = 1,
        Biweekly = 2,
        Monthly = 3
    }

    public enum CalendarInviteStatus : byte
    {
        Invited = 0,
        Accepted = 1,
        Declined = 2,
        Confirmed = 3,
        Out = 4,
        Standby = 5,
        SignedUp = 6,
        NotSignedUp = 7,
        Tentative = 8,
        Removed = 9     // Correct Name?
    }

    public enum CalendarError : byte
    {
        Ok = 0,

        // 1

        /// <summary>
        /// You have reached your limit of 30 created events.
        /// </summary>
        EventsExceeded = 2,
        /// <summary>
        /// Maximum number of invites reached. <br/>
        /// Remove an old event to add an additional one.
        /// </summary>
        SelfInvitesExceeded = 3,
        /// <summary>
        /// { Player } has the maximum number of events.<br/>
        /// They need to remove one to be invited to another.
        /// </summary>
        OtherInvitesExceeded = 4,
        /// <summary>
        /// You don't have permission to do that.
        /// </summary>
        Permissions = 5,
        /// <summary>
        /// Event not found.
        /// </summary>
        EventInvalid = 6,
        /// <summary>
        /// You are not invited to this event.
        /// </summary>
        NotInvited = 7,
        /// <summary>
        /// Internal Calendar error.
        /// </summary>
        Internal = 8,
        /// <summary>
        /// You are not in a guild.
        /// </summary>
        GuildPlayerNotInGuild = 9,

        // 10

        /// <summary>
        /// { Player } has already been invited.
        /// </summary>
        AlreadyInvitedToEventS = 11,
        /// <summary>
        /// Can't find that player.
        /// </summary>
        PlayerInvalid = 12,
        /// <summary>
        /// You cannot invite player from the opposing alliance.
        /// </summary>
        NotAllied = 13,
        /// <summary>
        /// { Player } is ignoring you.
        /// </summary>
        IgnoringYouS = 14,
        /// <summary>
        /// You cannot invite more than 100 players to this event.
        /// </summary>
        InvitesExceeded = 15,

        // 16

        /// <summary>
        /// Enter a valid date.
        /// </summary>
        InvalidDate = 17,
        /// <summary>
        /// Enter a valid time.
        /// </summary>
        InvalidTime = 18,
        /// <summary>
        /// Did not find any players matching the conditions.
        /// </summary>
        PlayerNotFound = 19,
        /// <summary>
        /// Enter a title.
        /// </summary>
        NeedsTitle = 20,
        /// <summary>
        /// This event has already occured.
        /// </summary>
        EventPassed = 21,
        /// <summary>
        /// This event is locked.
        /// </summary>
        EventLocked = 22,
        /// <summary>
        /// You cannot remove the creator of the event.
        /// </summary>
        DeleteCreatorFailed = 23,

        // 24

        /// <summary>
        /// This system is currently disabled.
        /// </summary>
        SystemDisabled = 25,
        /// <summary>
        /// Free Trial accounts cannot perform this action
        /// </summary>
        RestrictedAccount = 26,
        /// <summary>
        /// Your arena team has reached the limit of created events.
        /// </summary>
        ArenaEventsExceeded = 27,
        /// <summary>
        /// You need to have at least a level 20 character on your account.
        /// </summary>
        RestrictedLevel = 28,
        /// <summary>
        /// We have temporarily suspended your chat and mail privileges. <br/>
        /// Check your mail for more details.
        /// </summary>
        UserSquelched = 29,
        /// <summary>
        /// Invite not found.
        /// </summary>
        NoInvite = 30,

        /// <summary>
        /// You cannot create events on this server.
        /// </summary>
        EventWrongServer = 37,
        // 38
        /// <summary>
        /// You cannot sign up to this event.
        /// </summary>
        InvalidSignup = 39,
        /// <summary>
        /// Invites to sign up events are not allowed to be moderators.
        /// </summary>
        NoModerator = 40,
        /// <summary>
        /// Free Trial accounts cannot modify Calendar events.
        /// </summary>
        RestrictedAccount2 = 41, //cannot modify calendar events


        //deprecated?
        NoGuildInvites,
        InviteWrongServer,
        GuildEventsExceeded,
    }
}
