// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework;
using System;
using System.Diagnostics.CodeAnalysis;

public readonly struct ServerTime : IParsable<ServerTime>, IComparable<ServerTime>
{
    public readonly DateTime Data;
    public static readonly ServerTime Zero = new ServerTime(DateTime.MinValue);
    public static readonly ServerTime Infinity = new ServerTime(DateTime.MaxValue);

    public ServerTime Date => new(Data.Date);
    public TimeSpan TimeOfDay => Data.TimeOfDay;
    public int Year => Data.Year;
    public int Month => Data.Month;
    public int Day => Data.Day;
    public DayOfWeek DayOfWeek => Data.DayOfWeek;
    public int Hour => Data.Hour;
    public int Minute => Data.Minute;
    public int Second => Data.Second;
    public int Millisecond => Data.Millisecond;
    public DateTimeKind Kind => Data.Kind;

    public string ToShortDateString() => Data.ToShortDateString();
    public string ToShortTimeString() => Data.ToShortTimeString();
    public string ToLongDateString() => Data.ToLongDateString();
    public string ToLongTimeString() => Data.ToLongTimeString();

    public ServerTime(DateTime time)
    {
        // This check should be done first to avoid conversion for Zero and Infinity values
        if (time == DateTime.MinValue || time == DateTime.MaxValue)
        {
            Data = new(time.Ticks, DateTimeKind.Utc);
            return;
        }

        if (time.Kind == DateTimeKind.Utc)
        {
            Data = time;
            return;
        }

        if (time.Kind == DateTimeKind.Local)
        {
            Data = DateTime.SpecifyKind(time - Timezone.TimeZoneOffset, DateTimeKind.Utc);
            return;
        }

        throw new ArgumentException();
    }

    public ServerTime(UnixTime time)
    {
        Data = time.SpecifyAsUtc;
    }

    public ServerTime(UnixTime64 time)
    {
        Data = time.SpecifyAsUtc;
    }

    public ServerTime(UnixTimeMS time)
    {
        Data = time.SpecifyAsUtc;
    }

    public ServerTime(WowTime time)
    {
        Data = time.SpecifyAsUtc;
    }

    public static explicit operator ServerTime(DateTime time)
    {
        return new(time);
    }

    public static explicit operator ServerTime(WowTime time)
    {
        return new(time);
    }

    public static explicit operator ServerTime(RealmTime time)
    {
        return new(time);
    }

    public static explicit operator ServerTime(UnixTime time)
    {
        return new(time);
    }

    public static explicit operator ServerTime(UnixTime64 time)
    {
        return new(time);
    }

    public static explicit operator ServerTime(UnixTimeMS time)
    {
        return new(time);
    }

    public static implicit operator DateTime(ServerTime time)
    {
        return time.Data;
    }

    public static explicit operator UnixTime(ServerTime time)
    {
        return (UnixTime)time.Data;
    }

    public static explicit operator UnixTime64(ServerTime time)
    {
        return (UnixTime64)time.Data;
    }

    public static explicit operator UnixTimeMS(ServerTime time)
    {
        return (UnixTimeMS)time.Data;
    }

    public static explicit operator WowTime(ServerTime time)
    {
        return (WowTime)time.Data;
    }

    public static TimeSpan operator -(ServerTime left, ServerTime right)
    {
        return left.Data - right.Data;
    }

    public static ServerTime operator +(ServerTime left, TimeSpan right)
    {
        return new(left.Data + right);
    }

    public static ServerTime operator -(ServerTime left, TimeSpan right)
    {
        return new(left.Data - right);
    }

    public static bool operator >(ServerTime left, ServerTime right)
    {
        return left.Data > right.Data;
    }

    public static bool operator <(ServerTime left, ServerTime right)
    {
        return left.Data < right.Data;
    }

    public static bool operator >=(ServerTime left, ServerTime right)
    {
        return left.Data >= right.Data;
    }

    public static bool operator <=(ServerTime left, ServerTime right)
    {
        return left.Data <= right.Data;
    }

    public static bool operator ==(ServerTime left, ServerTime right)
    {
        return left.Data == right.Data;
    }

    public static bool operator !=(ServerTime left, ServerTime right)
    {
        return left.Data != right.Data;
    }

    public override bool Equals(object obj)
    {
        return Data.Equals(obj);
    }

    public bool Equals(ServerTime another)
    {
        return Data.Equals(another.Data);
    }

    public override int GetHashCode()
    {
        return Data.GetHashCode();
    }

    public override string ToString()
    {
        return Data.ToString();
    }

    public static ServerTime Parse(string s, IFormatProvider provider = null)
    {
        return new(DateTime.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out ServerTime result)
    {
        bool isSuccessful = DateTime.TryParse(s, null, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public static bool TryParse(string s, out ServerTime result)
    {
        bool isSuccessful = TryParse(s, null, out var parsed);
        result = parsed;

        return isSuccessful;
    }

    public int CompareTo(ServerTime other)
    {
        return Data.CompareTo(other.Data);
    }
}


