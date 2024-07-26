// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>In Milliseconds</summary>
public struct RelativeTime : IParsable<RelativeTime>, IComparable<RelativeTime>
{
    public uint Milliseconds;
    public readonly static RelativeTime MaxValue = new RelativeTime(uint.MaxValue);
    public readonly static RelativeTime MinValue = new RelativeTime(uint.MinValue);
    public readonly static RelativeTime Zero = new RelativeTime(0);

    public RelativeTime(uint milliseconds)
    {
        Milliseconds = milliseconds;
    }

    public static implicit operator uint(RelativeTime time)
    {
        return time.Milliseconds;
    }

    public static explicit operator RelativeTime(uint milliseconds)
    {
        return new RelativeTime(milliseconds);
    }    

    public static implicit operator float(RelativeTime time)
    {
        return time.Milliseconds;
    }

    public static implicit operator TimeSpan(RelativeTime time)
    {
        return Time.SpanFromMilliseconds(time.Milliseconds);
    }

    public static explicit operator RelativeTime(TimeSpan time)
    {
        long ticks = time.ToMilliseconds();
        if (ticks < uint.MinValue || ticks > uint.MaxValue)
            throw new Exception("explicit operator RelativeTime(TimeSpan) : out of range value");
        return (RelativeTime)ticks;
    }

    public static explicit operator RelativeTime(Seconds time)
    {
        long ticks = time.Ticks * (long)Time.MillisecondsInSecond;
        if (ticks < uint.MinValue || ticks > uint.MaxValue)
            throw new Exception("explicit operator RelativeTime(Seconds) : out of range value");
        return (RelativeTime)ticks;
    }

    public static explicit operator RelativeTime(Milliseconds time)
    {
        int ticks = time.Ticks;
        if (ticks < uint.MinValue)
            throw new Exception("explicit operator RelativeTime(Milliseconds) : out of range value");
        return (RelativeTime)(uint)ticks;
    }

    public static explicit operator RelativeTime(Minutes time)
    {
        long ticks = time.Ticks * (long)Time.MinuteMS;
        if (ticks < uint.MinValue || ticks > uint.MaxValue)
            throw new Exception("explicit operator RelativeTime(Minutes) : out of range value");
        return (RelativeTime)ticks;
    }

    public static explicit operator RelativeTime(Hours time)
    {
        long ticks = time.Ticks * (long)Time.HourMS;
        if (ticks < uint.MinValue || ticks > uint.MaxValue)
            throw new Exception("explicit operator RelativeTime(Hours) : out of range value");
        return (RelativeTime)ticks;
    }

    public static explicit operator RelativeTime(Days time)
    {
        long ticks = time.Ticks * (long)Time.DayMS;
        if (ticks < uint.MinValue || ticks > uint.MaxValue)
            throw new Exception("explicit operator RelativeTime(Days) : out of range value");
        return (RelativeTime)ticks;
    }

    public static explicit operator RelativeTime(Weeks time)
    {
        long ticks = time.Ticks * (long)Time.WeekMS;
        if (ticks < uint.MinValue || ticks > uint.MaxValue)
            throw new Exception("explicit operator RelativeTime(Weeks) : out of range value");
        return (RelativeTime)ticks;
    }

    public static RelativeTime operator -(RelativeTime left, RelativeTime right)
    {
        return new(left.Milliseconds - right.Milliseconds);
    }

    public static RelativeTime operator +(RelativeTime left, RelativeTime right)
    {
        return new(left.Milliseconds + right.Milliseconds);
    }

    public static RelativeTime operator *(RelativeTime left, RelativeTime right)
    {
        return new(left.Milliseconds * right.Milliseconds);
    }

    public static float operator /(RelativeTime left, RelativeTime right)
    {
        return left.Milliseconds / (float)right.Milliseconds;
    }

    public static RelativeTime operator %(RelativeTime left, RelativeTime right)
    {
        return new(left.Milliseconds % right.Milliseconds);
    }

    public static bool operator >(RelativeTime left, RelativeTime right)
    {
        return left.Milliseconds > right.Milliseconds;
    }

    public static bool operator <(RelativeTime left, RelativeTime right)
    {
        return left.Milliseconds < right.Milliseconds;
    }

    public static bool operator >=(RelativeTime left, RelativeTime right)
    {
        return left.Milliseconds >= right.Milliseconds;
    }

    public static bool operator <=(RelativeTime left, RelativeTime right)
    {
        return left.Milliseconds <= right.Milliseconds;
    }

    public static bool operator ==(RelativeTime left, RelativeTime right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RelativeTime left, RelativeTime right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is RelativeTime other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(RelativeTime another)
    {
        return Milliseconds.Equals(another.Milliseconds);
    }

    public static RelativeTime operator -(RelativeTime left, TimeSpan right)
    {
        return (RelativeTime)((TimeSpan)left - right);
    }

    public static RelativeTime operator +(RelativeTime left, TimeSpan right)
    {
        return (RelativeTime)((TimeSpan)left + right);
    }

    public static RelativeTime operator *(RelativeTime left, TimeSpan right)
    {
        return (RelativeTime)new TimeSpan(((TimeSpan)left).Ticks * right.Ticks);
    }

    public static float operator /(RelativeTime left, TimeSpan right)
    {
        return (float)((TimeSpan)left / right);
    }

    public static RelativeTime operator %(RelativeTime left, TimeSpan right)
    {
        return (RelativeTime)new TimeSpan(((TimeSpan)left).Ticks % right.Ticks);
    }

    public static bool operator >(RelativeTime left, TimeSpan right)
    {
        return (TimeSpan)left > right;
    }

    public static bool operator <(RelativeTime left, TimeSpan right)
    {
        return (TimeSpan)left < right;
    }

    public static bool operator >=(RelativeTime left, TimeSpan right)
    {
        return (TimeSpan)left >= right;
    }

    public static bool operator <=(RelativeTime left, TimeSpan right)
    {
        return (TimeSpan)left <= right;
    }

    public static bool operator ==(RelativeTime left, TimeSpan right)
    {
        return (TimeSpan)left == right;
    }

    public static bool operator !=(RelativeTime left, TimeSpan right)
    {
        return (TimeSpan)left != right;
    }

    public override string ToString()
    {
        return Milliseconds.ToString();
    }

    public override int GetHashCode()
    {
        return Milliseconds.GetHashCode();
    }

    public static RelativeTime Parse(string s, IFormatProvider provider = null)
    {
        return new(uint.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out RelativeTime result)
    {
        bool isSuccessful = uint.TryParse(s, provider, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(RelativeTime other)
    {
        return Milliseconds.CompareTo(other.Milliseconds);
    }
}


