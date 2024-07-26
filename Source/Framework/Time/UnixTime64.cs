// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct UnixTime64 : IParsable<UnixTime64>, IComparable<UnixTime64>
{
    public long Seconds;
    public static readonly UnixTime64 Zero = new UnixTime64(0);
    public static readonly UnixTime64 Infinity = new UnixTime64(-1);

    public UnixTime64(long seconds)
    {
        if (seconds < 0 && seconds != -1)
            throw new Exception("constructor UnixTime64(long) : too low value");
        Seconds = seconds;
    }

    public static implicit operator long(UnixTime64 time)
    {
        return time.Seconds;
    }

    public static explicit operator ulong(UnixTime64 time)
    {
        return (ulong)time.Seconds;
    }

    public static explicit operator UnixTime64(long seconds)
    {
        if (seconds < 0 && seconds != -1)
            throw new Exception("explicit operator UnixTime64(long) : too low value");
        return new UnixTime64(seconds);
    }

    public static explicit operator int(UnixTime64 time)
    {
        if (time.Seconds < 0 && time.Seconds != -1 || time.Seconds > int.MaxValue)
            throw new Exception("explicit operator int(UnixTime64) : out of range value");
        return (int)time.Seconds;
    }

    public static implicit operator UnixTime64(UnixTime time)
    {
        return (UnixTime64)time.Seconds;
    }

    public static explicit operator UnixTime64(UnixTimeMS time)
    {
        var seconds = time.Milliseconds / Time.MillisecondsInSecond;
        return (UnixTime64)seconds;
    }

    public static explicit operator DateTime(UnixTime64 time)
    {
        if (time == Infinity)
            return Time.Infinity;

        if (time == 0)
            return Time.Zero;

        return DateTimeOffset.FromUnixTimeSeconds(time).DateTime;
    }

    public static explicit operator UnixTime64(DateTime dateTime)
    {
        if (dateTime == Time.Infinity)
            return Infinity;

        if (dateTime == Time.Zero)
            return Zero;

        if (dateTime < DateTimeOffset.UnixEpoch.UtcDateTime)
            throw new Exception("explicit operator UnixTime64(DateTime) : too low value");
        
        return (UnixTime64)new DateTimeOffset(dateTime.Ticks, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    public static Seconds operator -(UnixTime64 left, UnixTime64 right)
    {
        return (Seconds)(left.Seconds - right.Seconds);
    }

    public static UnixTime64 operator +(UnixTime64 left, Seconds right)
    {
        return (UnixTime64)(left.Seconds + right.Ticks);
    }

    public static UnixTime64 operator -(UnixTime64 left, Seconds right)
    {
        return (UnixTime64)(left.Seconds - right.Ticks);
    }

    public static bool operator >(UnixTime64 left, UnixTime64 right)
    {
        return left.Seconds > right.Seconds;
    }

    public static bool operator <(UnixTime64 left, UnixTime64 right)
    {
        return left.Seconds < right.Seconds;
    }

    public static bool operator >=(UnixTime64 left, UnixTime64 right)
    {
        return left.Seconds >= right.Seconds;
    }

    public static bool operator <=(UnixTime64 left, UnixTime64 right)
    {
        return left.Seconds <= right.Seconds;
    }

    public static bool operator ==(UnixTime64 left, UnixTime64 right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(UnixTime64 left, UnixTime64 right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is UnixTime64 other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(UnixTime64 another)
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

    public static UnixTime64 Parse(string s, IFormatProvider provider = null)
    {
        return new(long.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out UnixTime64 result)
    {
        bool isSuccessful = long.TryParse(s, provider, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(UnixTime64 other)
    {
        return Seconds.CompareTo(other.Seconds);
    }

    public DateTime SpecifyAsLocal => DateTime.SpecifyKind((DateTime)this, DateTimeKind.Local);
    public DateTime SpecifyAsUtc => DateTime.SpecifyKind((DateTime)this, DateTimeKind.Utc);
}


