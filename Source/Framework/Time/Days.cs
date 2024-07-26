// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Framework;

public struct Days : IParsable<Days>, IComparable<Days>
{
    public int Ticks;
    public readonly static Days MaxValue = new Days(int.MaxValue);
    public readonly static Days MinValue = new Days(int.MinValue);
    public readonly static Days Zero = new Days(0);

    public Days(int ticks)
    {
        Ticks = ticks;
    }

    public static implicit operator int(Days span)
    {
        return span.Ticks;
    }

    public static explicit operator Days(int ticks)
    {
        return new(ticks);
    }

    public static implicit operator float(Days span)
    {
        return span.Ticks;
    }

    public static implicit operator TimeSpan(Days span)
    {
        return Time.SpanFromDays(span.Ticks);
    }

    public static explicit operator Days(TimeSpan span)
    {
        var ticks = span.ToDays();
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Days(TimeSpan) : out of range value");
        return new((int)ticks);
    }

    public static explicit operator Days(Milliseconds span)
    {
        return new(span.Ticks / Time.DayMS);
    }

    public static explicit operator Days(Seconds span)
    {
        return new(span.Ticks / Time.Day);
    }

    public static explicit operator Days(RelativeTime span)
    {
        return new((int)(span.Milliseconds / Time.DayMS));
    }

    public static explicit operator Days(Minutes span)
    {
        return new(span.Ticks / (Time.MinutesInHour * Time.HoursInDay));
    }

    public static explicit operator Days(Hours span)
    {
        return new(span.Ticks / Time.HoursInDay);
    }

    public static implicit operator Days(Weeks span)
    {
        var ticks = span.Ticks * (long)Time.DaysInWeek;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Days(Weeks) : out of range value");
        return new((int)ticks);
    }

    public static Days operator -(Days right)
    {
        return new(-right.Ticks);
    }

    public static Days operator -(Days left, Days right)
    {
        return new(left.Ticks - right.Ticks);
    }

    public static Days operator +(Days left, Days right)
    {
        return new(left.Ticks + right.Ticks);
    }

    public static Days operator *(Days left, Days right)
    {
        return new(left.Ticks * right.Ticks);
    }

    public static float operator /(Days left, Days right)
    {
        return left.Ticks / (float)right.Ticks;
    }

    public static Days operator %(Days left, Days right)
    {
        return new(left.Ticks % right.Ticks);
    }

    public static bool operator >(Days left, Days right)
    {
        return left.Ticks > right.Ticks;
    }

    public static bool operator <(Days left, Days right)
    {
        return left.Ticks < right.Ticks;
    }

    public static bool operator >=(Days left, Days right)
    {
        return left.Ticks >= right.Ticks;
    }

    public static bool operator <=(Days left, Days right)
    {
        return left.Ticks <= right.Ticks;
    }

    public static bool operator ==(Days left, Days right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Days left, Days right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is Days other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(Days another)
    {
        return Ticks.Equals(another.Ticks);
    }

    public static Days operator -(Days left, TimeSpan right)
    {
        return (Days)((TimeSpan)left - right);
    }

    public static Days operator +(Days left, TimeSpan right)
    {
        return (Days)((TimeSpan)left + right);
    }

    public static Days operator *(Days left, TimeSpan right)
    {
        return (Days)new TimeSpan(((TimeSpan)left).Ticks * right.Ticks);
    }

    public static float operator /(Days left, TimeSpan right)
    {
        return (float)((TimeSpan)left / right);
    }

    public static Days operator %(Days left, TimeSpan right)
    {
        return (Days)new TimeSpan(((TimeSpan)left).Ticks % right.Ticks);
    }

    public static bool operator >(Days left, TimeSpan right)
    {
        return (TimeSpan)left > right;
    }

    public static bool operator <(Days left, TimeSpan right)
    {
        return (TimeSpan)left < right;
    }

    public static bool operator >=(Days left, TimeSpan right)
    {
        return (TimeSpan)left >= right;
    }

    public static bool operator <=(Days left, TimeSpan right)
    {
        return (TimeSpan)left <= right;
    }

    public static bool operator ==(Days left, TimeSpan right)
    {
        return (TimeSpan)left == right;
    }

    public static bool operator !=(Days left, TimeSpan right)
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

    public static Days Parse(string s, IFormatProvider provider = null)
    {
        return new(int.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out Days result)
    {
        bool isSuccessful = int.TryParse(s, provider, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(Days other)
    {
        return Ticks.CompareTo(other.Ticks);
    }
}


