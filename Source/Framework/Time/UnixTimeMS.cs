// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct UnixTimeMS : IParsable<UnixTimeMS>, IComparable<UnixTimeMS>
{
    public long Milliseconds;
    public static readonly UnixTimeMS Zero = new UnixTimeMS(0);
    public static readonly UnixTimeMS Infinity = new UnixTimeMS(-1);

    public UnixTimeMS(long milliseconds)
    {
        if (milliseconds < 0 && milliseconds != -1)
            throw new Exception("constructor UnixTimeMS(long) : too low value");
        Milliseconds = milliseconds;
    }

    public static implicit operator long(UnixTimeMS time)
    {
        return time.Milliseconds;
    }

    public static explicit operator ulong(UnixTimeMS time)
    {
        return (ulong)time.Milliseconds;
    }

    public static explicit operator UnixTimeMS(long milliseconds)
    {
        if (milliseconds < 0 && milliseconds != -1)
            throw new Exception("explicit operator UnixTimeMS(long) : too low value");
        return new UnixTimeMS(milliseconds);
    }

    public static explicit operator int(UnixTimeMS time)
    {
        if (time.Milliseconds < 0 && time.Milliseconds != -1 || time.Milliseconds > int.MaxValue)
            throw new Exception("explicit operator int(UnixTimeMS) : out of range value");
        return (int)time.Milliseconds;
    }

    public static implicit operator UnixTimeMS(UnixTime time)
    {
        long milliseconds = (long)time.Seconds * Time.MillisecondsInSecond;
        return (UnixTimeMS)milliseconds;
    }

    public static explicit operator UnixTimeMS(UnixTime64 time)
    {
        if (time.Seconds > long.MaxValue / Time.MillisecondsInSecond)
            throw new Exception("explicit operator UnixTimeMS(UnixTime64) : too high value");
        long milliseconds = time.Seconds * Time.MillisecondsInSecond;
        return (UnixTimeMS)milliseconds;
    }

    public static explicit operator DateTime(UnixTimeMS time)
    {
        if (time == Infinity)
            return Time.Infinity;

        if (time == 0)
            return Time.Zero;

        return DateTimeOffset.FromUnixTimeMilliseconds(time).DateTime;
    }

    public static explicit operator UnixTimeMS(DateTime dateTime)
    {
        if (dateTime == Time.Infinity)
            return Infinity;

        if (dateTime == Time.Zero)
            return Zero;

        if (dateTime < DateTimeOffset.UnixEpoch.UtcDateTime)
            throw new Exception("explicit operator UnixTimeMS(DateTime) : too low value");

        return (UnixTimeMS)new DateTimeOffset(dateTime.Ticks, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    public static Milliseconds operator -(UnixTimeMS left, UnixTimeMS right)
    {
        return (Milliseconds)(left.Milliseconds - right.Milliseconds);
    }

    public static UnixTimeMS operator +(UnixTimeMS left, Milliseconds right)
    {
        return (UnixTimeMS)(left.Milliseconds + right.Ticks);
    }

    public static UnixTimeMS operator -(UnixTimeMS left, Milliseconds right)
    {
        return (UnixTimeMS)(left.Milliseconds - right.Ticks);
    }

    public static bool operator >(UnixTimeMS left, UnixTimeMS right)
    {
        return left.Milliseconds > right.Milliseconds;
    }

    public static bool operator <(UnixTimeMS left, UnixTimeMS right)
    {
        return left.Milliseconds < right.Milliseconds;
    }

    public static bool operator >=(UnixTimeMS left, UnixTimeMS right)
    {
        return left.Milliseconds >= right.Milliseconds;
    }

    public static bool operator <=(UnixTimeMS left, UnixTimeMS right)
    {
        return left.Milliseconds <= right.Milliseconds;
    }

    public static bool operator ==(UnixTimeMS left, UnixTimeMS right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(UnixTimeMS left, UnixTimeMS right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is UnixTimeMS other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(UnixTimeMS another)
    {
        return Milliseconds.Equals(another.Milliseconds);
    }

    public override int GetHashCode()
    {
        return Milliseconds.GetHashCode();
    }

    public override string ToString()
    {
        return Milliseconds.ToString();
    }

    public static UnixTimeMS Parse(string s, IFormatProvider provider = null)
    {
        return new(long.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out UnixTimeMS result)
    {
        bool isSuccessful = long.TryParse(s, provider, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(UnixTimeMS other)
    {
        return Milliseconds.CompareTo(other.Milliseconds);
    }

    public DateTime SpecifyAsLocal => DateTime.SpecifyKind((DateTime)this, DateTimeKind.Local);
    public DateTime SpecifyAsUtc => DateTime.SpecifyKind((DateTime)this, DateTimeKind.Utc);
}


