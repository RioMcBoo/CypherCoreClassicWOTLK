// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework;
using System;

/// <summary>
/// Time at the moment of execution of the main program loop
/// </summary>
public static class LoopTime
{
    public static bool Initialized { get; private set; }

    private static ServerTime _serverTime;
    private static RealmTime _realmTime;

    private static WowTime _wowServerTime;
    private static WowTime _wowRealmTime;

    private static UnixTime _unixServerTime;
    private static UnixTime _unixRealmTime;

    private static TimeSpan _upTime;
    private static Milliseconds _uptimeMS;
    private static RelativeTime _relativeTime;

    public static ServerTime ServerTime { get { if (_serverTime == ServerTime.Zero) throw new Exception(); return _serverTime; } private set { _serverTime = value; } }    
    public static RealmTime RealmTime { get { if (_realmTime == RealmTime.Zero) throw new Exception(); return _realmTime; } private set { _realmTime = value; } }

    public static WowTime WowServerTime { get { if (_wowServerTime == WowTime.Zero) throw new Exception(); return _wowServerTime; } private set { _wowServerTime = value; } }
    public static WowTime WowRealmTime { get { if (_wowRealmTime == WowTime.Zero) throw new Exception(); return _wowRealmTime; } private set { _wowRealmTime = value; } }

    public static UnixTime UnixServerTime { get { if (_unixServerTime == UnixTime.Zero) throw new Exception(); return _unixServerTime; } private set { _unixServerTime = value; } }
    public static UnixTime UnixRealmTime { get { if (_unixRealmTime == UnixTime.Zero) throw new Exception(); return _unixRealmTime; } private set { _unixRealmTime = value; } }

    public static TimeSpan UpTime { get { if (_upTime == TimeSpan.Zero) throw new Exception(); return _upTime; } private set { _upTime = value; } }
    public static Milliseconds UptimeMS { get { if (_uptimeMS == Milliseconds.Zero) throw new Exception(); return _uptimeMS; } private set { _uptimeMS = value; } }
    public static RelativeTime RelativeTime { get { if (_relativeTime == RelativeTime.Zero) throw new Exception(); return _relativeTime; } private set { _relativeTime = value; } }

    public static TimeSpan RealmOffset => Timezone.TimeZoneOffset;    

    /// <summary>
    /// Gets the difference to current UTC time.
    /// </summary>
    public static TimeSpan Diff(DateTime oldTime)
    {
        return Diff(oldTime, ServerTime);
    }

    /// <summary>
    /// Gets the difference between two time points.
    /// </summary>
    public static TimeSpan Diff(DateTime oldTime, DateTime newTime)
    {
        return newTime - oldTime;
    }

    /// <summary>
    /// Gets the difference to current RelativeTime in milliseconds.
    /// </summary>
    public static Milliseconds Diff(RelativeTime oldMSTime)
    {
        return Time.Diff(oldMSTime, RelativeTime);
    }

    public static RealmTime ToRealmTime(this DateTime time)
    {
        if (time.Kind == DateTimeKind.Utc)
            return (RealmTime) new DateTime((time + Timezone.TimeZoneOffset).Ticks, DateTimeKind.Local);

        if (time.Kind == DateTimeKind.Unspecified)
            throw new InvalidOperationException();

        return (RealmTime)time;
    }

    public static ServerTime ToServerTime(this DateTime time)
    {
        if (time.Kind == DateTimeKind.Local)
            return (ServerTime)new DateTime((time - Timezone.TimeZoneOffset).Ticks, DateTimeKind.Utc);

        if (time.Kind == DateTimeKind.Unspecified)
            throw new InvalidOperationException();

        return (ServerTime)time;
    }

    public static void Update(ServerTime newUtcTime)
    {
        if (newUtcTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException();

        ServerTime = newUtcTime;
        RealmTime = (RealmTime)ServerTime;

        UnixServerTime = (UnixTime)ServerTime;
        UnixRealmTime = (UnixTime)RealmTime;

        WowServerTime = (WowTime)ServerTime;
        WowRealmTime = (WowTime)RealmTime;

        UpTime = newUtcTime - Time.ApplicationStartTime;
        UptimeMS = (Milliseconds)UpTime;
        RelativeTime = (RelativeTime)UptimeMS;

        Initialized = true;
    }
}