// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct WowTime : IParsable<WowTime>, IComparable<WowTime>
{
    private PackedTime _data;

    public PackedTime Data
    {
        get => _data;
        set { CheckWithException(value); _data = value; }
    }

    public static readonly WowTime Zero = new WowTime(0);
    public static readonly WowTime Infinity = new WowTime(-1);

    public WowTime(int data)
    {
        PackedTime time = new(data);
        CheckWithException(time);
        _data = time;
    }

    public WowTime(PackedTime time)
    {
        CheckWithException(time);
        _data = time;
    }

    public WowTime(DateTime time)
    {
        _data = new PackedTime(time);
    }

    private static void CheckWithException(PackedTime time)
    {
        if (!Check(time))
        {
            throw new ArgumentException();
        }
    }

    public static bool Check(PackedTime time)
    {
        if (time == PackedTime.Infinity)
            return true;

        if (time.Flags == -1 ||
            time.Year == -1 || time.Month == -1 ||
            time.Day == -1 ||
            //(int)time.Weekday == -1 ||
            time.Hours == -1 ||
            time.Minutes == -1)
            return false;

        return true;
    }

    public bool Check() => Check(_data);

    public int Flags => _data.Flags;
    public int Year => _data.Year;
    public int Month => _data.Month;
    public int Day => _data.Day;
    public DayOfWeek WeekDay => _data.Weekday;
    public int Hours => _data.Hours;
    public int Minutes => _data.Minutes;

    public static explicit operator WowTime(int data)
    {
        return new WowTime(data);
    }

    public static implicit operator int(WowTime time)
    {
        return time._data;
    }

    public static explicit operator WowTime(DateTime time)
    {
        return new WowTime(time);
    }

    public override int GetHashCode()
    {
        return Data.GetHashCode();
    }

    public override string ToString()
    {
        return Data.ToString();
    }

    public static bool operator ==(WowTime left, WowTime right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(WowTime left, WowTime right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is WowTime other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(WowTime another)
    {
        return Data.Equals(another.Data);
    }

    public static WowTime Parse(string s, IFormatProvider provider = null)
    {
        if (WowTime.TryParse(s, provider, out var result))
            return result;

        else throw new ArgumentException(s);
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out WowTime result)
    {
        result = new();
        bool isSuccessful = PackedTime.TryParse(s, provider, out var parsed);

        if (isSuccessful)
        {
            isSuccessful = Check(parsed);
            if (isSuccessful)
            {
                result._data = parsed;
            }
        }

        return isSuccessful;
    }

    /// <summary>
    /// Just for using in collections
    /// </summary>
    public int CompareTo(WowTime other)
    {
        return Data.CompareTo(other.Data);
    }

    public DateTime SpecifyAsLocal => _data.ToDateTime(DateTimeKind.Local);
    public DateTime SpecifyAsUtc => _data.ToDateTime(DateTimeKind.Utc);
}