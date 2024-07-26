// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct UnixTime : IParsable<UnixTime>, IComparable<UnixTime>
{
    public int Seconds;
    public static readonly UnixTime Zero = new UnixTime(0);
    public static readonly UnixTime Infinity = new UnixTime(-1);

    public UnixTime(int ticks)
    {
        if (ticks < 0 && ticks != -1)
            throw new Exception("constructor UnixTime(int) : too low value");
        Seconds = ticks;
    }

    public static implicit operator int(UnixTime time)
    {
        return time.Seconds;
    }

    public static explicit operator uint(UnixTime time)
    {
        return (uint)time.Seconds;
    }

    public static explicit operator UnixTime(int seconds)
    {
        if (seconds < 0 && seconds != -1)
            throw new Exception("explicit operator UnixTime(int) : too low value");
        return new UnixTime(seconds);
    }

    public static explicit operator UnixTime(UnixTime64 time)
    {
        if (time.Seconds > int.MaxValue)
            throw new Exception("explicit operator UnixTime(UnixTime64) : too high value");
        return (UnixTime)(int)time.Seconds;
    }

    public static explicit operator UnixTime(UnixTimeMS time)
    {
        var seconds = time.Milliseconds / 1000;
        if (seconds > int.MaxValue)
            throw new Exception("explicit operator UnixTime(UnixTimeMS) : too high value");
        return (UnixTime)(int)seconds;
    }

    public static explicit operator DateTime(UnixTime time)
    {
        if (time == Infinity)
            return Time.Infinity;

        if (time == 0)
            return Time.Zero;
        
        return DateTimeOffset.FromUnixTimeSeconds(time).DateTime;
    }

    public static explicit operator UnixTime(DateTime dateTime)
    {
        if (dateTime == Time.Infinity)
            return Infinity;

        if (dateTime == Time.Zero)
            return Zero;

        if (dateTime < DateTimeOffset.UnixEpoch.UtcDateTime)
            throw new Exception("explicit operator UnixTime(DateTime) : too low value");
                
        long ticks = new DateTimeOffset(dateTime.Ticks, TimeSpan.Zero).ToUnixTimeSeconds();

        if (ticks > int.MaxValue)
            throw new Exception("explicit operator UnixTime(DateTime) : too high value");
        return (UnixTime)(int)ticks;
    }

    public static Seconds operator -(UnixTime left, UnixTime right)
    {
        return (Seconds)(left.Seconds - right.Seconds);
    }

    public static UnixTime operator +(UnixTime left, Seconds right)
    {
        return (UnixTime)(left.Seconds + right.Ticks);
    }

    public static UnixTime operator -(UnixTime left, Seconds right)
    {
        return (UnixTime)(left.Seconds - right.Ticks);
    }

    public static bool operator >(UnixTime left, UnixTime right)
    {
        return left.Seconds > right.Seconds;
    }

    public static bool operator <(UnixTime left, UnixTime right)
    {
        return left.Seconds < right.Seconds;
    }

    public static bool operator >=(UnixTime left, UnixTime right)
    {
        return left.Seconds >= right.Seconds;
    }

    public static bool operator <=(UnixTime left, UnixTime right)
    {
        return left.Seconds <= right.Seconds;
    }

    public static bool operator ==(UnixTime left, UnixTime right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(UnixTime left, UnixTime right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is UnixTime other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(UnixTime another)
    {
        return Seconds.Equals(another.Seconds);
    }

    public override int GetHashCode()
    {
        return Seconds.GetHashCode();
    }

    public override string ToString()
    {
        return Seconds.ToString();
    }

    public static UnixTime Parse(string s, IFormatProvider provider = null)
    {
        return new(int.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out UnixTime result)
    {
        bool isSuccessful = int.TryParse(s, provider, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(UnixTime other)
    {
        return Seconds.CompareTo(other.Seconds);
    }

    public DateTime SpecifyAsLocal => DateTime.SpecifyKind((DateTime)this, DateTimeKind.Local);
    public DateTime SpecifyAsUtc => DateTime.SpecifyKind((DateTime)this, DateTimeKind.Utc);
}


