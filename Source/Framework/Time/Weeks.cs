// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct Weeks : IParsable<Weeks>, IComparable<Weeks>
{
    public int Ticks;
    public readonly static Weeks MaxValue = new Weeks(int.MaxValue);
    public readonly static Weeks MinValue = new Weeks(int.MinValue);
    public readonly static Weeks Zero = new Weeks(0);

    public Weeks(int ticks)
    {
        Ticks = ticks;
    }

    public static implicit operator int(Weeks span)
    {
        return span.Ticks;
    }

    public static explicit operator Weeks(int ticks)
    {
        return new(ticks);
    }

    public static implicit operator float(Weeks span)
    {
        return span.Ticks;
    }

    public static implicit operator TimeSpan(Weeks span)
    {        
        return Time.SpanFromWeeks(span.Ticks);
    }

    public static explicit operator Weeks(TimeSpan span)
    {
        var ticks = span.ToWeeks();
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Weeks(TimeSpan) : out of range value");
        return new((int)ticks);
    }

    public static explicit operator Weeks(Milliseconds span)
    {
        return new(span.Ticks / Time.WeekMS);
    }

    public static explicit operator Weeks(Seconds span)
    {
        return new(span.Ticks / Time.Week);
    }

    public static explicit operator Weeks(RelativeTime span)
    {
        return new((int)(span.Milliseconds / Time.WeekMS));
    }

    public static explicit operator Weeks(Minutes span)
    {
        return new(span.Ticks / (Time.MinutesInHour * Time.HoursInDay * Time.DaysInWeek));
    }

    public static explicit operator Weeks(Hours span)
    {
        return new(span.Ticks / (Time.HoursInDay * Time.DaysInWeek));
    }

    public static explicit operator Weeks(Days span)
    {
        return new(span.Ticks / Time.DaysInWeek);
    }

    public static Weeks operator -(Weeks right)
    {
        return new(-right.Ticks);
    }

    public static Weeks operator -(Weeks left, Weeks right)
    {
        return new(left.Ticks - right.Ticks);
    }

    public static Weeks operator +(Weeks left, Weeks right)
    {
        return new(left.Ticks + right.Ticks);
    }

    public static Weeks operator *(Weeks left, Weeks right)
    {
        return new(left.Ticks * right.Ticks);
    }

    public static float operator /(Weeks left, Weeks right)
    {
        return left.Ticks / (float)right.Ticks;
    }

    public static Weeks operator %(Weeks left, Weeks right)
    {
        return new(left.Ticks % right.Ticks);
    }

    public static bool operator >(Weeks left, Weeks right)
    {
        return left.Ticks > right.Ticks;
    }

    public static bool operator <(Weeks left, Weeks right)
    {
        return left.Ticks < right.Ticks;
    }

    public static bool operator >=(Weeks left, Weeks right)
    {
        return left.Ticks >= right.Ticks;
    }

    public static bool operator <=(Weeks left, Weeks right)
    {
        return left.Ticks <= right.Ticks;
    }

    public static bool operator ==(Weeks left, Weeks right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Weeks left, Weeks right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is Weeks other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(Weeks another)
    {
        return Ticks.Equals(another.Ticks);
    }

    public static Weeks operator -(Weeks left, TimeSpan right)
    {
        return (Weeks)((TimeSpan)left - right);
    }

    public static Weeks operator +(Weeks left, TimeSpan right)
    {
        return (Weeks)((TimeSpan)left + right);
    }

    public static Weeks operator *(Weeks left, TimeSpan right)
    {
        return (Weeks)new TimeSpan(((TimeSpan)left).Ticks * right.Ticks);
    }

    public static float operator /(Weeks left, TimeSpan right)
    {
        return (float)((TimeSpan)left / right);
    }

    public static Weeks operator %(Weeks left, TimeSpan right)
    {
        return (Weeks)new TimeSpan(((TimeSpan)left).Ticks % right.Ticks);
    }

    public static bool operator >(Weeks left, TimeSpan right)
    {
        return (TimeSpan)left > right;
    }

    public static bool operator <(Weeks left, TimeSpan right)
    {
        return (TimeSpan)left < right;
    }

    public static bool operator >=(Weeks left, TimeSpan right)
    {
        return (TimeSpan)left >= right;
    }

    public static bool operator <=(Weeks left, TimeSpan right)
    {
        return (TimeSpan)left <= right;
    }

    public static bool operator ==(Weeks left, TimeSpan right)
    {
        return (TimeSpan)left == right;
    }

    public static bool operator !=(Weeks left, TimeSpan right)
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

    public static Weeks Parse(string s, IFormatProvider provider = null)
    {
        return new(int.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out Weeks result)
    {
        bool isSuccessful = int.TryParse(s, null, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(Weeks other)
    {
        return Ticks.CompareTo(other.Ticks);
    }
}


