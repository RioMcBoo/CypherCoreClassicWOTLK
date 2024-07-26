// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct Seconds : IParsable<Seconds>, IComparable<Seconds>
{
    public int Ticks;
    public readonly static Seconds MaxValue = new Seconds(int.MaxValue);
    public readonly static Seconds MinValue = new Seconds(int.MinValue);
    public readonly static Seconds Zero = new Seconds(0);

    public Seconds(int ticks)
    {
        Ticks = ticks;
    }

    public static implicit operator int(Seconds span)
    {
        return span.Ticks;
    }

    public static explicit operator Seconds(int ticks)
    {
        return new(ticks);
    }

    public static implicit operator float(Seconds span)
    {
        return span.Ticks;
    }

    public static implicit operator TimeSpan(Seconds span)
    {
        return Time.SpanFromSeconds(span.Ticks);
    }

    public static explicit operator Seconds(TimeSpan span)
    {
        var ticks = span.ToSeconds();
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Seconds(TimeSpan) : out of range value");
        return new((int)ticks);
    }

    public static explicit operator Seconds(Milliseconds span)
    {
        return new(span.Ticks / Time.SecondMS);
    }

    public static explicit operator Seconds(RelativeTime span)
    {
        return new((int)(span.Milliseconds / Time.SecondMS));
    }

    public static implicit operator Seconds(Minutes span)
    {
        var ticks = span.Ticks * (long)Time.Minute;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Seconds(Minutes) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Seconds(Hours span)
    {
        var ticks = span.Ticks * (long)Time.Hour;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Seconds(Hours) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Seconds(Days span)
    {
        var ticks = span.Ticks * (long)Time.Day;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Seconds(Days) : out of range value");
        return new((int)ticks);
    }

    public static implicit operator Seconds(Weeks span)
    {
        var ticks = span.Ticks * (long)Time.Week;
        if (ticks < int.MinValue || ticks > int.MaxValue)
            throw new Exception("explicit operator Seconds(Weeks) : out of range value");
        return new((int)ticks);
    }

    public static Seconds operator -(Seconds right)
    {
        return new(-right.Ticks);
    }

    public static Seconds operator -(Seconds left, Seconds right)
    {
        return new(left.Ticks - right.Ticks);
    }

    public static Seconds operator +(Seconds left, Seconds right)
    {
        return new(left.Ticks + right.Ticks);
    }

    public static Seconds operator *(Seconds left, Seconds right)
    {
        return new(left.Ticks * right.Ticks);
    }

    public static float operator /(Seconds left, Seconds right)
    {
        return left.Ticks / (float)right.Ticks;
    }

    public static Seconds operator %(Seconds left, Seconds right)
    {
        return new(left.Ticks % right.Ticks);
    }

    public static bool operator >(Seconds left, Seconds right)
    {
        return left.Ticks > right.Ticks;
    }

    public static bool operator <(Seconds left, Seconds right)
    {
        return left.Ticks < right.Ticks;
    }

    public static bool operator >=(Seconds left, Seconds right)
    {
        return left.Ticks >= right.Ticks;
    }

    public static bool operator <=(Seconds left, Seconds right)
    {
        return left.Ticks <= right.Ticks;
    }

    public static bool operator ==(Seconds left, Seconds right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Seconds left, Seconds right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is Seconds other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(Seconds another)
    {
        return Ticks.Equals(another.Ticks);
    }

    public static Seconds operator -(Seconds left, TimeSpan right)
    {
        return (Seconds)((TimeSpan)left - right);
    }

    public static Seconds operator +(Seconds left, TimeSpan right)
    {
        return (Seconds)((TimeSpan)left + right);
    }

    public static Seconds operator *(Seconds left, TimeSpan right)
    {
        return (Seconds)new TimeSpan(((TimeSpan)left).Ticks * right.Ticks);
    }

    public static float operator /(Seconds left, TimeSpan right)
    {
        return (float)((TimeSpan)left / right);
    }

    public static Seconds operator %(Seconds left, TimeSpan right)
    {
        return (Seconds) new TimeSpan(((TimeSpan)left).Ticks % right.Ticks);
    }

    public static bool operator >(Seconds left, TimeSpan right)
    {
        return (TimeSpan)left > right;
    }

    public static bool operator <(Seconds left, TimeSpan right)
    {
        return (TimeSpan)left < right;
    }

    public static bool operator >=(Seconds left, TimeSpan right)
    {
        return (TimeSpan)left >= right;
    }

    public static bool operator <=(Seconds left, TimeSpan right)
    {
        return (TimeSpan)left <= right;
    }

    public static bool operator ==(Seconds left, TimeSpan right)
    {
        return (TimeSpan)left == right;
    }

    public static bool operator !=(Seconds left, TimeSpan right)
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

    public static Seconds Parse(string s, IFormatProvider provider = null)
    {
        return new(int.Parse(s, provider));
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out Seconds result)
    {
        bool isSuccessful = int.TryParse(s, provider, out var parsed);
        result = new(parsed);
        return isSuccessful;
    }

    public int CompareTo(Seconds other)
    {
        return Ticks.CompareTo(other.Ticks);
    }
}


