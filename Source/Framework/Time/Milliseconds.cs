// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct Milliseconds : IParsable<Milliseconds>, IComparable<Milliseconds>
{
    public int Ticks;
    public readonly static Milliseconds MaxValue = new Milliseconds(int.MaxValue);
    public readonly static Milliseconds MinValue = new Milliseconds(int.MinValue);
    public readonly static Milliseconds Zero = new Milliseconds(0);

    public Milliseconds(int ticks)
    {
        Ticks = ticks;
    }

    public static implicit operator int(Milliseconds span)
    {
        return span.Ticks;
    }

    public static explicit operator Milliseconds(int ticks)
    {
        return new(ticks);
    }

    public static implicit operator float(Milliseconds span)
    {
        return span.Ticks;
    }

    public static implicit operator TimeSpan(Milliseconds span)
    {
        return Time.SpanFromMilliseconds(span.Ticks);
    }

    public static explicit operator Milliseconds(TimeSpan span)
    {
        var ticks = span.ToMilliseconds();
        if (ticks > int.MaxValue || ticks < int.MinValue)
            throw new Exception("explicit operator Milliseconds(TimeSpan) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Milliseconds(Seconds span)
    {
        var ticks = span.Ticks * (long)Time.MillisecondsInSecond;
        if (ticks > int.MaxValue || ticks < int.MinValue)
            throw new Exception("explicit operator Milliseconds(Seconds) : out of range value");
        return new Milliseconds((int)ticks);
    }

    public static explicit operator Milliseconds(RelativeTime span)
    {
        var ticks = span.Milliseconds;
        if (ticks > int.MaxValue)
            throw new Exception("explicit operator Milliseconds(RelativeTime) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Milliseconds(Minutes span)
    {
        var ticks = span.Ticks * (long)Time.MinuteMS;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Milliseconds(Minutes) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Milliseconds(Hours span)
    {
        var ticks = span.Ticks * (long)Time.HourMS;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Milliseconds(Hours) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Milliseconds(Days span)
    {
        var ticks = span.Ticks * (long)Time.DayMS;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Milliseconds(Days) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Milliseconds(Weeks span)
    {
        var ticks = span.Ticks * (long)Time.WeekMS;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Milliseconds(Weeks) : out of range value");
        return new((int)ticks);
    }

    public static Milliseconds operator -(Milliseconds right)
    {
        return new(-right.Ticks);
    }

    public static Milliseconds operator -(Milliseconds left, Milliseconds right)
    {
        return new(left.Ticks - right.Ticks);
    }

    public static Milliseconds operator +(Milliseconds left, Milliseconds right)
    {
        return new(left.Ticks + right.Ticks);
    }

    public static Milliseconds operator *(Milliseconds left, Milliseconds right)
    {
        return new(left.Ticks * right.Ticks);
    }

    public static float operator /(Milliseconds left, Milliseconds right)
    {
        return left.Ticks / (float)right.Ticks;
    }

    public static Milliseconds operator %(Milliseconds left, Milliseconds right)
    {
        return new(left.Ticks % right.Ticks);
    }

    public static bool operator >(Milliseconds left, Milliseconds right)
    {
        return left.Ticks > right.Ticks;
    }

    public static bool operator <(Milliseconds left, Milliseconds right)
    {
        return left.Ticks < right.Ticks;
    }

    public static bool operator >=(Milliseconds left, Milliseconds right)
    {
        return left.Ticks >= right.Ticks;
    }

    public static bool operator <=(Milliseconds left, Milliseconds right)
    {
        return left.Ticks <= right.Ticks;
    }

    public static bool operator ==(Milliseconds left, Milliseconds right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Milliseconds left, Milliseconds right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is Milliseconds other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(Milliseconds another)
    {
        return Ticks.Equals(another.Ticks);
    }

    public static Milliseconds operator -(Milliseconds left, TimeSpan right)
    {
        return (Milliseconds)((TimeSpan)left - right);
    }

    public static Milliseconds operator +(Milliseconds left, TimeSpan right)
    {
        return (Milliseconds)((TimeSpan)left + right);
    }

    public static Milliseconds operator *(Milliseconds left, TimeSpan right)
    {
        return (Milliseconds)new TimeSpan(((TimeSpan)left).Ticks * right.Ticks);
    }

    public static float operator /(Milliseconds left, TimeSpan right)
    {
        return (float)((TimeSpan)left / right);
    }

    public static Milliseconds operator %(Milliseconds left, TimeSpan right)
    {
        return (Milliseconds)new TimeSpan(((TimeSpan)left).Ticks % right.Ticks);
    }

    public static bool operator >(Milliseconds left, TimeSpan right)
    {
        return (TimeSpan)left > right;
    }

    public static bool operator <(Milliseconds left, TimeSpan right)
    {
        return (TimeSpan)left < right;
    }

    public static bool operator >=(Milliseconds left, TimeSpan right)
    {
        return (TimeSpan)left >= right;
    }

    public static bool operator <=(Milliseconds left, TimeSpan right)
    {
        return (TimeSpan)left <= right;
    }

    public static bool operator ==(Milliseconds left, TimeSpan right)
    {
        return (TimeSpan)left == right;
    }

    public static bool operator !=(Milliseconds left, TimeSpan right)
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

    public static Milliseconds Parse(string s, IFormatProvider provider = null)
    {
        return new(int.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out Milliseconds result)
    {
        bool isSuccessful = int.TryParse(s, provider, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(Milliseconds other)
    {
        return Ticks.CompareTo(other.Ticks);
    }
}


