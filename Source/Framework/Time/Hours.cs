// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct Hours : IParsable<Hours>, IComparable<Hours>
{
    public int Ticks;
    public readonly static Hours MaxValue = new Hours(int.MaxValue);
    public readonly static Hours MinValue = new Hours(int.MinValue);
    public readonly static Hours Zero = new Hours(0);

    public Hours(int ticks)
    {
        Ticks = ticks;
    }

    public static implicit operator int(Hours span)
    {
        return span.Ticks;
    }

    public static explicit operator Hours(int ticks)
    {
        return new(ticks);
    }

    public static implicit operator float(Hours span)
    {
        return span.Ticks;
    }

    public static implicit operator TimeSpan(Hours span)
    {
        return Time.SpanFromHours(span.Ticks);
    }

    public static explicit operator Hours(TimeSpan span)
    {
        var ticks = span.ToHours();
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Hours(TimeSpan) : out of range value");
        return new((int)ticks);
    }

    public static explicit operator Hours(Milliseconds span)
    {
        return new(span.Ticks / Time.HourMS);
    }

    public static explicit operator Hours(Seconds span)
    {
        return new(span.Ticks / Time.Hour);
    }

    public static explicit operator Hours(RelativeTime span)
    {
        return new((int)(span.Milliseconds / Time.HourMS));
    }

    public static explicit operator Hours(Minutes span)
    {
        return new(span.Ticks / Time.MinutesInHour);
    }

    public static implicit operator Hours(Days span)
    {
        var ticks = span.Ticks * (long)Time.HoursInDay;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Hours(Days) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Hours(Weeks span)
    {
        var ticks = span.Ticks * (long)Time.DaysInWeek * Time.HoursInDay;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Hours(Weeks) : out of range value");
        return new((int)ticks);
    }

    public static Hours operator -(Hours right)
    {
        return new(-right.Ticks);
    }

    public static Hours operator -(Hours left, Hours right)
    {
        return new(left.Ticks - right.Ticks);
    }

    public static Hours operator +(Hours left, Hours right)
    {
        return new(left.Ticks + right.Ticks);
    }

    public static Hours operator *(Hours left, Hours right)
    {
        return new(left.Ticks * right.Ticks);
    }

    public static float operator /(Hours left, Hours right)
    {
        return left.Ticks / (float)right.Ticks;
    }

    public static Hours operator %(Hours left, Hours right)
    {
        return new(left.Ticks % right.Ticks);
    }

    public static bool operator >(Hours left, Hours right)
    {
        return left.Ticks > right.Ticks;
    }

    public static bool operator <(Hours left, Hours right)
    {
        return left.Ticks < right.Ticks;
    }

    public static bool operator >=(Hours left, Hours right)
    {
        return left.Ticks >= right.Ticks;
    }

    public static bool operator <=(Hours left, Hours right)
    {
        return left.Ticks <= right.Ticks;
    }

    public static bool operator ==(Hours left, Hours right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Hours left, Hours right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is Hours other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(Hours another)
    {
        return Ticks.Equals(another.Ticks);
    }

    public static Hours operator -(Hours left, TimeSpan right)
    {
        return (Hours)((TimeSpan)left - right);
    }

    public static Hours operator +(Hours left, TimeSpan right)
    {
        return (Hours)((TimeSpan)left + right);
    }

    public static Hours operator *(Hours left, TimeSpan right)
    {
        return (Hours)new TimeSpan(((TimeSpan)left).Ticks * right.Ticks);
    }

    public static float operator /(Hours left, TimeSpan right)
    {
        return (float)((TimeSpan)left / right);
    }

    public static Hours operator %(Hours left, TimeSpan right)
    {
        return (Hours)new TimeSpan(((TimeSpan)left).Ticks % right.Ticks);
    }

    public static bool operator >(Hours left, TimeSpan right)
    {
        return (TimeSpan)left > right;
    }

    public static bool operator <(Hours left, TimeSpan right)
    {
        return (TimeSpan)left < right;
    }

    public static bool operator >=(Hours left, TimeSpan right)
    {
        return (TimeSpan)left >= right;
    }

    public static bool operator <=(Hours left, TimeSpan right)
    {
        return (TimeSpan)left <= right;
    }

    public static bool operator ==(Hours left, TimeSpan right)
    {
        return (TimeSpan)left == right;
    }

    public static bool operator !=(Hours left, TimeSpan right)
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

    public static Hours Parse(string s, IFormatProvider provider = null)
    {
        return new(int.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out Hours result)
    {
        bool isSuccessful = int.TryParse(s, provider, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(Hours other)
    {
        return Ticks.CompareTo(other.Ticks);
    }
}


