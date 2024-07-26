// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct Minutes : IParsable<Minutes>, IComparable<Minutes>
{
    public int Ticks;
    public readonly static Minutes MaxValue = new Minutes(int.MaxValue);
    public readonly static Minutes MinValue = new Minutes(int.MinValue);
    public readonly static Minutes Zero = new Minutes(0);

    public Minutes(int ticks)
    {
        Ticks = ticks;
    }

    public static implicit operator int(Minutes span)
    {
        return span.Ticks;
    }

    public static explicit operator Minutes(int ticks)
    {
        return new(ticks);
    }

    public static implicit operator float(Minutes span)
    {
        return span.Ticks;
    }

    public static implicit operator TimeSpan(Minutes span)
    {
        return Time.SpanFromMinutes(span.Ticks);
    }

    public static explicit operator Minutes(TimeSpan span)
    {
        var ticks = span.ToMinutes();
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Minutes(TimeSpan) : out of range value");
        return new((int)ticks);
    }

    public static explicit operator Minutes(Milliseconds span)
    {
        return new(span.Ticks / Time.MinuteMS);
    }

    public static explicit operator Minutes(Seconds span)
    {
        return new(span.Ticks / Time.Minute);
    }

    public static explicit operator Minutes(RelativeTime span)
    {
        return new((int)(span.Milliseconds / Time.MinuteMS));
    }

    public static implicit operator Minutes(Hours span)
    {
        var ticks = span.Ticks * (long)Time.MinutesInHour;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Minutes(Hours) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Minutes(Days span)
    {
        var ticks = span.Ticks * (long)Time.MinutesInHour * Time.HoursInDay;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Minutes(Days) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Minutes(Weeks span)
    {
        var ticks = span.Ticks * (long)Time.DaysInWeek * Time.HoursInDay * Time.MinutesInHour;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Minutes(Weeks) : out of range value");
        return new((int)ticks);
    }

    public static Minutes operator -(Minutes right)
    {
        return new(-right.Ticks);
    }

    public static Minutes operator -(Minutes left, Minutes right)
    {
        return new(left.Ticks - right.Ticks);
    }

    public static Minutes operator +(Minutes left, Minutes right)
    {
        return new(left.Ticks + right.Ticks);
    }

    public static Minutes operator *(Minutes left, Minutes right)
    {
        return new(left.Ticks * right.Ticks);
    }

    public static float operator /(Minutes left, Minutes right)
    {
        return left.Ticks / (float)right.Ticks;
    }

    public static Minutes operator %(Minutes left, Minutes right)
    {
        return new(left.Ticks % right.Ticks);
    }

    public static bool operator >(Minutes left, Minutes right)
    {
        return left.Ticks > right.Ticks;
    }

    public static bool operator <(Minutes left, Minutes right)
    {
        return left.Ticks < right.Ticks;
    }

    public static bool operator >=(Minutes left, Minutes right)
    {
        return left.Ticks >= right.Ticks;
    }

    public static bool operator <=(Minutes left, Minutes right)
    {
        return left.Ticks <= right.Ticks;
    }

    public static bool operator ==(Minutes left, Minutes right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Minutes left, Minutes right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is Minutes other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(Minutes another)
    {
        return Ticks.Equals(another.Ticks);
    }

    public static Minutes operator -(Minutes left, TimeSpan right)
    {
        return (Minutes)((TimeSpan)left - right);
    }

    public static Minutes operator +(Minutes left, TimeSpan right)
    {
        return (Minutes)((TimeSpan)left + right);
    }

    public static Minutes operator *(Minutes left, TimeSpan right)
    {
        return (Minutes)new TimeSpan(((TimeSpan)left).Ticks * right.Ticks);
    }

    public static float operator /(Minutes left, TimeSpan right)
    {
        return (float)((TimeSpan)left / right);
    }

    public static Minutes operator %(Minutes left, TimeSpan right)
    {
        return (Minutes)new TimeSpan(((TimeSpan)left).Ticks % right.Ticks);
    }

    public static bool operator >(Minutes left, TimeSpan right)
    {
        return (TimeSpan)left > right;
    }

    public static bool operator <(Minutes left, TimeSpan right)
    {
        return (TimeSpan)left < right;
    }

    public static bool operator >=(Minutes left, TimeSpan right)
    {
        return (TimeSpan)left >= right;
    }

    public static bool operator <=(Minutes left, TimeSpan right)
    {
        return (TimeSpan)left <= right;
    }

    public static bool operator ==(Minutes left, TimeSpan right)
    {
        return (TimeSpan)left == right;
    }

    public static bool operator !=(Minutes left, TimeSpan right)
    {
        return (TimeSpan)left != right;
    }

    public override string ToString()
    {
        return Ticks.ToString();
    }

    public override int GetHashCode()
    {
        return Ticks.GetHashCode();
    }

    public static Minutes Parse(string s, IFormatProvider provider = null)
    {
        return new(int.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out Minutes result)
    {
        bool isSuccessful = int.TryParse(s, provider, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(Minutes other)
    {
        return Ticks.CompareTo(other.Ticks);
    }
}


