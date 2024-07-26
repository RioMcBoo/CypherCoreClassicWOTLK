// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework;
using System;
using System.Diagnostics.CodeAnalysis;

public readonly struct RealmTime : IParsable<RealmTime>, IComparable<RealmTime>
{
    public readonly DateTime Data;
    public static readonly RealmTime Zero = new RealmTime(DateTime.MinValue);
    public static readonly RealmTime Infinity = new RealmTime(DateTime.MaxValue);

    public RealmTime Date => new(Data.Date);
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

    public RealmTime(DateTime time)
    {
        // This check should be done first to avoid conversion for Zero and Infinity values
        if (time == DateTime.MinValue || time == DateTime.MaxValue)
        {
            Data = new(time.Ticks, DateTimeKind.Local);
            return;
        }

        if (time.Kind == DateTimeKind.Local)
        {
            Data = time;
            return;
        }

        if (time.Kind == DateTimeKind.Utc)
        {
            Data = DateTime.SpecifyKind(time + Timezone.TimeZoneOffset, DateTimeKind.Local);
            return;
        }       

        throw new ArgumentException();
    }

    public RealmTime(UnixTime time)
    {
        Data = time.SpecifyAsLocal;
    }

    public RealmTime(UnixTime64 time)
    {
        Data = time.SpecifyAsLocal;
    }

    public RealmTime(UnixTimeMS time)
    {
        Data = time.SpecifyAsLocal;
    }

    public RealmTime(WowTime time)
    {
        Data = time.SpecifyAsLocal;
    }

    public static explicit operator RealmTime(DateTime time)
    {
        return new(time);
    }

    public static explicit operator RealmTime(WowTime time)
    {
        return new(time);
    }

    public static explicit operator RealmTime(ServerTime time)
    {
        return new(time);
    }

    public static explicit operator RealmTime(UnixTime time)
    {
        return new(time);
    }

    public static explicit operator RealmTime(UnixTime64 time)
    {
        return new(time);
    }

    public static explicit operator RealmTime(UnixTimeMS time)
    {
        return new(time);
    }

    public static implicit operator DateTime(RealmTime time)
    {
        return time.Data;
    }

    public static explicit operator UnixTime(RealmTime time)
    {
        return (UnixTime)time.Data;
    }

    public static explicit operator UnixTime64(RealmTime time)
    {
        return (UnixTime64)time.Data;
    }

    public static explicit operator UnixTimeMS(RealmTime time)
    {
        return (UnixTimeMS)time.Data;
    }

    public static explicit operator WowTime(RealmTime time)
    {
        return (WowTime)time.Data;
    }

    public static TimeSpan operator -(RealmTime left, RealmTime right)
    {
        return left.Data - right.Data;
    }

    public static RealmTime operator +(RealmTime left, TimeSpan right)
    {
        return new(left.Data + right);
    }

    public static RealmTime operator -(RealmTime left, TimeSpan right)
    {
        return new(left.Data - right);
    }

    public static bool operator >(RealmTime left, RealmTime right)
    {
        return left.Data > right.Data;
    }

    public static bool operator <(RealmTime left, RealmTime right)
    {
        return left.Data < right.Data;
    }

    public static bool operator >=(RealmTime left, RealmTime right)
    {
        return left.Data >= right.Data;
    }

    public static bool operator <=(RealmTime left, RealmTime right)
    {
        return left.Data <= right.Data;
    }

    public static bool operator ==(RealmTime left, RealmTime right)
    {
        return left.Data == right.Data;
    }

    public static bool operator !=(RealmTime left, RealmTime right)
    {
        return left.Data != right.Data;
    }

    public override bool Equals(object obj)
    {
        return Data.Equals(obj);
    }

    public bool Equals(RealmTime another)
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

    public static RealmTime Parse(string s, IFormatProvider provider = null)
    {
        return new(DateTime.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out RealmTime result)
    {
        bool isSuccessful = DateTime.TryParse(s, null, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public static bool TryParse(string s, out RealmTime result)
    {
        bool isSuccessful = TryParse(s, null, out var parsed);
        result = parsed;

        return isSuccessful;
    }

    public int CompareTo(RealmTime other)
    {
        return Data.CompareTo(other.Data);
    }
}

