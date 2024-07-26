// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Text;

public enum TimeFormat
{
    FullText,       // 1 Days 2 Hours 3 Minutes 4 Seconds
    ShortText,      // 1d 2h 3m 4s
    Numeric         // 1:2:3:4
}

public static class Time
{
    public static readonly DateTime ApplicationStartTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    public static readonly DateTime Zero = DateTime.MinValue;
    public static readonly DateTime Infinity = DateTime.MaxValue;

    public static readonly int MillisecondsInSecond = 1000;
    public static readonly int SecondsInMinute = 60;
    public static readonly int MinutesInHour = 60;
    public static readonly int HoursInDay = 24;
    public static readonly int DaysInWeek = 7;

    public static readonly Seconds Minute = (Seconds)SecondsInMinute;
    public static readonly Seconds Hour = (Seconds)(Minute * MinutesInHour);
    public static readonly Seconds Day = (Seconds)(Hour * HoursInDay);
    public static readonly Seconds Week = (Seconds)(Day * DaysInWeek);

    public static readonly Milliseconds SecondMS = (Milliseconds)MillisecondsInSecond;
    public static readonly Milliseconds MinuteMS = (Milliseconds)(SecondMS * SecondsInMinute);
    public static readonly Milliseconds HourMS = (Milliseconds)(MinuteMS * MinutesInHour);
    public static readonly Milliseconds DayMS = (Milliseconds)(HourMS * HoursInDay);
    public static readonly Milliseconds WeekMS = (Milliseconds)(DayMS * DaysInWeek);

    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    public static DateTime Now => DateTime.UtcNow;

    /// <summary>
    /// Gets the application UpTime.
    /// </summary>
    public static TimeSpan UpTime => Now - ApplicationStartTime;

    /// <summary>
    /// Gets the application time relative to application start time in ms.
    /// </summary>
    public static RelativeTime NowRelative => (RelativeTime)UpTime.ToMilliseconds();

    /// <summary>
    /// Gets the difference to current UTC time.
    /// </summary>
    public static TimeSpan Diff(DateTime oldTime)
    {
        return Diff(oldTime, Now);
    }    

    /// <summary>
    /// Gets the difference to current UpTime.
    /// </summary>
    public static TimeSpan Diff(TimeSpan oldUpTime)
    {
        return UpTime - oldUpTime;
    }

    /// <summary>
    /// Gets the difference to current RelativeTime in milliseconds.
    /// </summary>
    public static Milliseconds Diff(RelativeTime oldMSTime)
    {
        return Diff(oldMSTime, NowRelative);
    }

    /// <summary>
    /// Gets the difference between two time points.
    /// </summary>
    public static TimeSpan Diff(DateTime oldTime, DateTime newTime)
    {
        return newTime - oldTime;
    }

    /// <summary>
    /// Gets the difference between two time spans.
    /// </summary>
    public static TimeSpan Diff(TimeSpan oldTimeSpan, TimeSpan newTimeSpan)
    {
        return newTimeSpan - oldTimeSpan;
    }

    /// <summary>
    /// Gets the difference between two relative to UpTime spans in milliseconds.
    /// </summary>
    public static Milliseconds Diff(RelativeTime oldMSTime, RelativeTime newMSTime)
    {
        if (oldMSTime > newMSTime)
            return (Milliseconds)((RelativeTime)0xFFFFFFFF - oldMSTime + newMSTime);
        else
            return (Milliseconds)(newMSTime - oldMSTime);
    }

    /// <summary>
    /// Gets the difference between relative time span and DateTime in milliseconds.
    /// </summary>
    public static Milliseconds Diff(RelativeTime oldMSTime, DateTime newTime)
    {
        RelativeTime newMSTime = (RelativeTime)(newTime - ApplicationStartTime).ToMilliseconds();
        return Diff(oldMSTime,newMSTime);
    }

    public static string SpanToTimeString(TimeSpan time, TimeFormat timeFormat = TimeFormat.FullText, bool hoursOnly = false)
    {
        int secs = time.Seconds;
        int minutes = time.Minutes;
        int hours = time.Hours;
        int days = time.Days;
        string sing = time < TimeSpan.Zero ? "-" : "";

        if (timeFormat == TimeFormat.Numeric)
        {
            if (days != 0)
                return $"{sing}{days}d:{hours}h:{minutes}m:{secs:D2}s";
            else if (hours != 0)
                return $"{sing}{hours}h:{minutes}m:{secs:D2}s";
            else if (minutes != 0)
                return $"{sing}{minutes}m:{secs:D2}s";
            else
                return $"{sing}{secs:D2}s";
        }

        StringBuilder ss = new();

        ss.Append(sing);

        if (days != 0)
        {
            ss.Append(days);
            switch (timeFormat)
            {
                case TimeFormat.ShortText:
                    ss.Append("d");
                    break;
                case TimeFormat.FullText:
                    if (days == 1)
                        ss.Append(" Day ");
                    else
                        ss.Append(" Days ");
                    break;
                default:
                    return "<Unknown time format>";
            }
        }

        if (hours != 0 || hoursOnly)
        {
            ss.Append(hours);
            switch (timeFormat)
            {

                case TimeFormat.ShortText:
                    ss.Append("h");
                    break;
                case TimeFormat.FullText:

                    if (hours <= 1)
                        ss.Append(" Hour ");
                    else
                        ss.Append(" Hours ");
                    break;
                default:
                    return "<Unknown time format>";
            }
        }

        if (!hoursOnly)
        {
            if (minutes != 0)
            {
                ss.Append(minutes);
                switch (timeFormat)
                {
                    case TimeFormat.ShortText:
                        ss.Append("m");
                        break;
                    case TimeFormat.FullText:
                        if (minutes == 1)
                            ss.Append(" Minute ");
                        else
                            ss.Append(" Minutes ");
                        break;
                    default:
                        return "<Unknown time format>";
                }
            }

            if (secs != 0 || (days == 0 && hours == 0 && minutes == 0))
            {
                ss.Append(secs);
                switch (timeFormat)
                {
                    case TimeFormat.ShortText:
                        ss.Append("s");
                        break;
                    case TimeFormat.FullText:
                        if (secs <= 1)
                            ss.Append(" Second.");
                        else
                            ss.Append(" Seconds.");
                        break;
                    default:
                        return "<Unknown time format>";
                }
            }
        }

        return ss.ToString();
    }

    /// <summary>
    /// pattern : *d*h*m*s (* - digit)
    /// </summary>
    public static TimeSpan StringToSpan(string timestring)
    {
        TimeSpan secs = TimeSpan.Zero;
        int buffer = 0;
        CreateSpanFrom createSpan;

        foreach (var c in timestring)
        {
            if (char.IsDigit(c))
            {
                buffer *= 10;
                buffer += c - '0';
            }
            else
            {
                switch (c)
                {
                    case 'd':
                        createSpan = Time.SpanFromDays;
                        break;
                    case 'h':
                        createSpan = Time.SpanFromHours;
                        break;
                    case 'm':
                        createSpan = Time.SpanFromMinutes;
                        break;
                    case 's':
                        createSpan = Time.SpanFromSeconds;
                        break;
                    default:
                        return TimeSpan.Zero;  //bad format
                }
                secs += createSpan(buffer);
                buffer = 0;
            }
        }

        return secs;
    }

    /// <summary>
    /// pattern : *d*h*m*s (* - digit)
    /// </summary>
    public static Seconds StringToSecs(string timestring)
    {
        return (Seconds)StringToSpan(timestring);
    }

    public static void Profile(string description, int iterations, Action func)
    {
        //Run at highest priority to minimize fluctuations caused by other processes/threads
        System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
        System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;

        // warm up 
        func();

        var watch = new System.Diagnostics.Stopwatch();

        // clean up
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        watch.Start();
        for (int i = 0; i < iterations; i++)
        {
            func();
        }
        watch.Stop();
        Console.Write(description);
        Console.WriteLine(" Time Elapsed {0} ms", watch.Elapsed.TotalMilliseconds);
    }

    public static TimeSpan Min(TimeSpan left, TimeSpan right)
    {
        if (left < right)
            return left;
        else
            return right;
    }

    public static TimeSpan Max(TimeSpan left, TimeSpan right)
    {
        if (left > right)
            return left;
        else
            return right;
    }

    public static DateTime Min(DateTime left, DateTime right)
    {
        if (left < right)
            return left;
        else
            return right;
    }

    public static DateTime Max(DateTime left, DateTime right)
    {
        if (left > right)
            return left;
        else
            return right;
    }

    public static Milliseconds Min(Milliseconds left, Milliseconds right)
    {
        if (left.Ticks < right.Ticks)
            return left;
        else
            return right;
    }

    public static Milliseconds Max(Milliseconds left, Milliseconds right)
    {
        if (left.Ticks > right.Ticks)
            return left;
        else
            return right;
    }

    public static Seconds Min(Seconds left, Seconds right)
    {
        if (left.Ticks < right.Ticks)
            return left;
        else
            return right;
    }

    public static Seconds Max(Seconds left, Seconds right)
    {
        if (left.Ticks > right.Ticks)
            return left;
        else
            return right;
    }

    #region TimeSpanExtensions
    // It is better to use these methods instead of native ones, which use the double as an argument.
    // This helps get rid of the inaccuracies that the double type suffers from.
    public static TimeSpan SpanFromMilliseconds(long Milliseconds)
    {
        return TimeSpan.FromTicks(Milliseconds * TimeSpan.TicksPerMillisecond);
    }

    public static TimeSpan SpanFromSeconds(long Seconds)
    {
        return TimeSpan.FromTicks(Seconds * TimeSpan.TicksPerSecond);
    }

    public static TimeSpan SpanFromMinutes(long Minutes)
    {
        return TimeSpan.FromTicks(Minutes * (long)Time.Minute * TimeSpan.TicksPerSecond);
    }

    public static TimeSpan SpanFromHours(long Hours)
    {
        return TimeSpan.FromTicks(Hours * (long)Time.Hour * TimeSpan.TicksPerSecond);
    }

    public static TimeSpan SpanFromDays(long Days)
    {
        return TimeSpan.FromTicks(Days * (long)Time.Day * TimeSpan.TicksPerSecond);
    }

    public static TimeSpan SpanFromWeeks(long Weeks)
    {
        return TimeSpan.FromTicks(Weeks * (long)Time.Week * TimeSpan.TicksPerSecond);
    }

    // Just for good manners
    public static TimeSpan SpanFromMilliseconds(double Milliseconds)
    {
        return TimeSpan.FromMilliseconds(Milliseconds);
    }

    public static TimeSpan SpanFromSeconds(double Seconds)
    {
        return TimeSpan.FromSeconds(Seconds);
    }

    public static TimeSpan SpanFromMinutes(double Minutes)
    {
        return TimeSpan.FromMinutes(Minutes);
    }

    public static TimeSpan SpanFromHours(double Hours)
    {
        return TimeSpan.FromHours(Hours);
    }

    public static TimeSpan SpanFromDays(double Days)
    {
        return TimeSpan.FromDays(Days);
    }

    public static TimeSpan SpanFromWeeks(double Weeks)
    {
        return TimeSpan.FromDays(Weeks * Time.DaysInWeek);
    }

    // It is better to use these methods instead of native ones, which return the double.
    // This helps get rid of the inaccuracies that the double type suffers from.
    public static long ToMilliseconds(this TimeSpan span)
    {
        return span.Ticks / TimeSpan.TicksPerMillisecond;
    }

    public static long ToSeconds(this TimeSpan span)
    {
        return span.Ticks / TimeSpan.TicksPerSecond;
    }

    public static long ToMinutes(this TimeSpan span)
    {
        return span.Ticks / TimeSpan.TicksPerMinute;
    }

    public static long ToHours(this TimeSpan span)
    {
        return span.Ticks / (Hour * TimeSpan.TicksPerSecond);
    }

    public static long ToDays(this TimeSpan span)
    {
        return span.Ticks / (Day * TimeSpan.TicksPerSecond);
    }

    public static long ToWeeks(this TimeSpan span)
    {
        return span.Ticks / (Week * TimeSpan.TicksPerSecond);
    }
    #endregion

    public static bool IsInRange(DateTime thisTime, DateTime from, DateTime to)
    {
        return thisTime >= from && thisTime < to;
    }

    private delegate TimeSpan CreateSpanFrom(long ticks);
}

public class TimeTracker
{
    public TimeTracker(TimeSpan expiry = default)
    {
        _expiryTime = expiry;
    }

    public void Update(TimeSpan diff)
    {
        _expiryTime -= diff;
    }

    public bool Passed()
    {
        return _expiryTime <= TimeSpan.Zero;
    }

    public void Reset(TimeSpan expiry)
    {
        _expiryTime = expiry;
    }

    public TimeSpan GetExpiry()
    {
        return _expiryTime;
    }

    TimeSpan _expiryTime;
}

public class IntervalTimer
{
    public void Update(TimeSpan diff)
    {
        _current += diff;
        if (_current < TimeSpan.Zero)
            _current = TimeSpan.Zero;
    }

    public bool Passed()
    {
        return _current >= _interval;
    }

    public void Reset()
    {
        if (_current >= _interval)
            _current = new(_current.Ticks % _interval.Ticks);
    }

    public void SetCurrent(TimeSpan current)
    {
        _current = current;
    }

    public void SetInterval(TimeSpan interval)
    {
        _interval = interval;
    }

    public TimeSpan GetInterval()
    {
        return _interval;
    }

    public TimeSpan GetCurrent()
    {
        return _current;
    }

    TimeSpan _interval;
    TimeSpan _current;
}

public class PeriodicTimer
{
    public PeriodicTimer(Milliseconds period, Milliseconds start_time)
    {
        i_period = period;
        i_expireTime = start_time;
    }

    public bool Update(Milliseconds diff)
    {
        if ((i_expireTime -= diff) > 0)
            return false;

        i_expireTime += i_period > diff ? i_period : diff;
        return true;
    }

    public void SetPeriodic(Milliseconds period, Milliseconds start_time)
    {
        i_expireTime = start_time;
        i_period = period;
    }

    // Tracker interface
    public void TUpdate(Milliseconds diff) { i_expireTime -= diff; }
    public bool TPassed() { return i_expireTime <= 0; }
    public void TReset(Milliseconds diff, Milliseconds period) { i_expireTime += period > diff ? period : diff; }

    Milliseconds i_period;
    Milliseconds i_expireTime;
}
