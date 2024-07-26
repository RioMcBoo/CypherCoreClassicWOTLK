// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct HolidayTime : IParsable<HolidayTime>, IComparable<HolidayTime>
{
    private PackedTime _data;

    public PackedTime Data
    {
        get => _data;
        set { CheckWithException(value); _data = value; }
    }

    public static readonly HolidayTime Zero = new HolidayTime(0);
    public static readonly HolidayTime Infinity = new HolidayTime(-1);

    public HolidayTime(int data)
    {
        PackedTime time = new(data);
        CheckWithException(time);
        _data = time;
    }

    public HolidayTime(PackedTime time)
    {
        CheckWithException(time);
        _data = time;
    }

    public HolidayTime(DateTime time)
    {
        _data = new(time);
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
            //time.Year == -1 || time.Month == -1 || || (int)time.Weekday == -1 // can be unused by Calendar
            time.Day == -1 ||
            time.Hours == -1 || time.Minutes == -1)
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

    /// <summary>
    /// Events with fixed date within year have - 1
    /// </summary>
    public bool IsYearly => Year == -1;

    public static explicit operator HolidayTime(int data)
    {
        return new(data);
    }

    public static explicit operator HolidayTime(DateTime time)
    {
        return new(time);
    }

    public override int GetHashCode()
    {
        return Data.GetHashCode();
    }

    public override string ToString()
    {
        return Data.ToString();
    }

    public static bool operator ==(HolidayTime left, HolidayTime right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HolidayTime left, HolidayTime right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is HolidayTime other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(HolidayTime another)
    {
        return Data.Equals(another.Data);
    }

    public static HolidayTime Parse(string s, IFormatProvider provider = null)
    {
        if (HolidayTime.TryParse(s, provider, out var result))
            return result;

        else throw new ArgumentException(s);
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out HolidayTime result)
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
    public int CompareTo(HolidayTime other)
    {
        return Data.CompareTo(other.Data);
    }
}