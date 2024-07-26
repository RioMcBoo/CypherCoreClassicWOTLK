// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

public struct PackedTime : IParsable<PackedTime>, IComparable<PackedTime>
{
    private int _data;

    public int Data
    {
        get => _data;
        set { CheckWithException(value); _data = value; }
    }

    public static readonly PackedTime Zero = new PackedTime(0);
    public static readonly PackedTime Infinity = new PackedTime(-1);

    private static void CheckWithException(int data)
    {
        if (!Check(data))
        {
            throw new Exception();
        }
    }

    public static bool Check(int data)
    {
        if (
        !GetAndCheck(out _, data, _month) ||
        !GetAndCheck(out _, data, _mday) ||
        !GetAndCheck(out _, data, _wday) ||
        !GetAndCheck(out _, data, _hours) ||
        !GetAndCheck(out _, data, _minutes) ||
        !GetAndCheck(out _, data, _flag) ||
        !GetAndCheck(out _, data, _year))
        {
            return false;
        }

        return true;
    }

    public bool Check() => Check(_data);

    public PackedTime(int data)
    {
        CheckWithException(data);
        _data = data;
    }

    public PackedTime(DateTime time)
    {
        if (time == Time.Infinity)
        {
            _data = Infinity._data;
            return;
        }

        if (time == Time.Zero)
        {
            _data = Zero._data;
            return;
        }

        if (!SetAndCheck(time.Year, ref _data, _year))
        {
            throw new ArgumentOutOfRangeException();
        }

        Set(time.Month, ref _data, _month);
        Set(time.Day, ref _data, _mday);
        Set((int)time.DayOfWeek, ref _data, _wday);
        Set(time.Hour, ref _data, _hours);
        Set(time.Minute, ref _data, _minutes);
        //Set((int)time.Kind, ref _data, _flag); //Just a guess
    }

    public DateTime ToDateTime(DateTimeKind kind = DateTimeKind.Unspecified)
    {
        if (this == Infinity)
            return Time.Infinity;

        if (this == Zero)
            return Time.Zero;

        return new DateTime(Year, Month, Day, Hours, Minutes, 0, kind);
    }

    public static explicit operator PackedTime(int data)
    {
        return new(data);
    }

    public static implicit operator int(PackedTime time)
    {
        return time._data;
    }

    private struct Settings
    {
        public Settings(int mask, int offset, int minValue, int maxValue)
        {
            Mask = mask;
            Offset = offset;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public readonly int Mask;
        public readonly int Offset;
        public readonly int MinValue;
        public readonly int MaxValue;
    }

    private static readonly Settings _flag = new(0x3, 29, 0, 2);
    private static readonly Settings _year = new(0x1F, 24, 2000, 2030);
    private static readonly Settings _month = new(0xF, 20, 1, 12);
    private static readonly Settings _mday = new(0x3F, 14, 1, 31);
    private static readonly Settings _wday = new(0x7, 11, 0, 6);
    private static readonly Settings _hours = new(0x1F, 6, 0, 23);
    private static readonly Settings _minutes = new(0x3F, 0, 0, 59);

    private static bool IsValidSet(int value, Settings s)
    {
        return value == -1 || value >= s.MinValue && value <= s.MaxValue;
    }

    private static bool IsValidGet(int value, Settings s)
    {
        return value <= s.MaxValue;
    }

    private static void Set(int value, ref int data, Settings s)
    {
        if (value != -1)
            value -= s.MinValue;

        SetRaw(value, ref data, s.Offset, s.Mask);
    }

    private static bool SetAndCheck(int value, ref int data, Settings s)
    {
        if (!IsValidSet(value, s))
        {
            return false;
        }

        Set(value, ref data, s);

        return true;
    }

    private static int Get(int data, Settings s)
    {
        int value = GetRaw(data, s.Offset, s.Mask);

        if (value == s.Mask)
        {
            return -1;
        }

        return value + s.MinValue;
    }

    private static bool GetAndCheck(out int value, int data, Settings s)
    {
        value = Get(data, s);

        if (!IsValidGet(value, s))
        {
            return false;
        }

        return true;
    }

    private static void SetRaw(int value, ref int data, int offset, int mask)
    {
        data &= ~(mask << offset);                      //clear part of data
        data |= ((value & mask) << offset);             //write part of data
    }

    private static int GetRaw(int data, int offset, int mask)
    {
        return (data >> offset) & mask;                 //read part of data
    }

    /// <summary>
    /// 0 is minimum<br/>
    /// 2 is maximum<br/>
    /// -1 is null<br/>
    /// </summary>
    public int Flags
    {
        get => Get(Data, _flag);
        set { if (!SetAndCheck(value, ref _data, _flag)) throw new ArgumentOutOfRangeException(); }
    }

    /// <summary>
    /// 2000 is minimum<br/>
    /// 2030 is maximum<br/>
    /// -1 is null<br/>
    /// </summary>
    public int Year
    {
        get => Get(_data, _year);
        set { if (!SetAndCheck(value, ref _data, _year)) throw new ArgumentOutOfRangeException(); }
    }

    /// <summary>
    /// 1 is minimum<br/>
    /// 12 is maximum<br/>
    /// -1 is null<br/>
    /// </summary>
    public int Month
    {
        get => Get(_data, _month);
        set { if (!SetAndCheck(value, ref _data, _month)) throw new ArgumentOutOfRangeException(); }
    }

    /// <summary>
    /// 1 is minimum<br/>
    /// 31 is maximum<br/>
    /// -1 is null<br/>
    /// </summary>
    public int Day
    {
        get => Get(_data, _mday);
        set { if (!SetAndCheck(value, ref _data, _mday)) throw new ArgumentOutOfRangeException(); }
    }

    /// <summary>
    /// 0 is minimum<br/>
    /// 6 is maximum<br/>
    /// -1 is null<br/>
    /// </summary>
    public DayOfWeek Weekday
    {
        get => (DayOfWeek)Get(_data, _wday);
        set { if (!SetAndCheck((int)value, ref _data, _wday)) throw new ArgumentOutOfRangeException(); }
    }

    /// <summary>
    /// 0 is minimum<br/>
    /// 23 is maximum<br/>
    /// -1 is null<br/>
    /// </summary>
    public int Hours
    {
        get => Get(_data, _hours);
        set { if (!SetAndCheck(value, ref _data, _hours)) throw new ArgumentOutOfRangeException(); }
    }

    /// <summary>
    /// 0 is minimum<br/>
    /// 59 is maximum<br/>
    /// -1 is null<br/>
    /// </summary>
    public int Minutes
    {
        get => Get(_data, _minutes);
        set { if (!SetAndCheck(value, ref _data, _minutes)) throw new ArgumentOutOfRangeException(); }
    }

    public static PackedTime Parse(string s, IFormatProvider provider = null)
    {
        if (PackedTime.TryParse(s, provider, out var result))
            return result;

        else throw new ArgumentException(s);
    }

    public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, [MaybeNullWhen(false)] out PackedTime result)
    {
        result = new();
        bool isSuccessful = uint.TryParse(s, provider, out var parsed);

        if (isSuccessful)
        {
            isSuccessful = Check((int)parsed);
            if (isSuccessful)
            {
                result._data = (int)parsed;
            }
        }

        return isSuccessful;
    }

    public override int GetHashCode()
    {
        return _data.GetHashCode();
    }

    public override string ToString()
    {
        return ((uint)_data).ToString();
    }

    public static bool operator ==(PackedTime left, PackedTime right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PackedTime left, PackedTime right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        if (obj is PackedTime other)
            return Equals(other);

        throw new ArgumentException();
    }

    public bool Equals(PackedTime another)
    {
        return Data.Equals(another.Data);
    }

    // <summary>
    /// Just for using in collections
    /// </summary>
    public int CompareTo(PackedTime other)
    {
        return Data.CompareTo(other.Data);
    }
}