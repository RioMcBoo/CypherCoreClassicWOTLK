// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Framework
{
    public class Timezone
    {
        public static readonly TimeSpan MaximalTimeZoneOffset = (Hours)24;
        public static readonly TimeSpan MinimalTimeZoneOffset = (Hours)(-24);
        private static TimeSpan _timeZoneOffset;

        public static bool Initialized { get; private set; }

        public static void Initialize(TimeSpan offsetConfig)
        {
            if (Initialized)
                throw new InvalidOperationException();

            if (offsetConfig > MaximalTimeZoneOffset || offsetConfig < MinimalTimeZoneOffset)
                throw new ArgumentOutOfRangeException();

            TimeZoneOffset = offsetConfig;

            Initialized = true;
        }

        public static void Initialize(RealmZone realmZone)
        {
            if (Initialized)
                throw new InvalidOperationException();

            if (_RealmZoneOffset.TryGetValue(realmZone, out var offset))
                TimeZoneOffset = offset;
            else
                throw new ArgumentOutOfRangeException();

            Initialized = true;
        }

        public static TimeSpan TimeZoneOffset { get { if (!Initialized) throw new Exception(); return _timeZoneOffset; } private set { _timeZoneOffset = value; } }

        static Dictionary<uint, TimeSpan> _timezoneOffsetsByHash = InitTimezoneHashDb();

        static Dictionary<uint, TimeSpan> InitTimezoneHashDb()
        {
            // Generate our hash db to match values sent in client authentication packets
            Dictionary<uint, TimeSpan> hashToOffset = new();
            foreach (var sysInfo in TimeZoneInfo.GetSystemTimeZones())
            {
                TimeSpan offsetMinutes = sysInfo.BaseUtcOffset;
                hashToOffset.TryAdd(offsetMinutes.TotalMinutes.ToString().HashFnv1a(), offsetMinutes);
            }

            return hashToOffset;
        }

        private static Dictionary<RealmZone, TimeSpan> _RealmZoneOffset = new()
        {
            { RealmZone.None, TimeSpan.Zero },
            { RealmZone.Development, TimeSpan.Zero },
            { RealmZone.UnitedStates, Time.SpanFromMinutes(-480) },
            { RealmZone.Oceanic, Time.SpanFromMinutes(600) },
            { RealmZone.LatinAmerica, Time.SpanFromMinutes(-180) },
            { RealmZone.Tournament, TimeSpan.Zero },
            { RealmZone.Korea, Time.SpanFromMinutes(540) },
            { RealmZone.Tournament2, TimeSpan.Zero },
            { RealmZone.English, Time.SpanFromMinutes(0) },
            { RealmZone.German, Time.SpanFromMinutes(60) },
            { RealmZone.French, Time.SpanFromMinutes(60) },
            { RealmZone.Spanish, Time.SpanFromMinutes(60) },
            { RealmZone.Russian, Time.SpanFromMinutes(180) },
            { RealmZone.Tournament3, TimeSpan.Zero },
            { RealmZone.Taiwan, Time.SpanFromMinutes(480) },
            { RealmZone.Tournament4, TimeSpan.Zero },
            { RealmZone.China, Time.SpanFromMinutes(480) },
            { RealmZone.CN1, TimeSpan.Zero },
            { RealmZone.CN2, TimeSpan.Zero },
            { RealmZone.CN3, TimeSpan.Zero },
            { RealmZone.CN4, TimeSpan.Zero },
            { RealmZone.CN5, TimeSpan.Zero },
            { RealmZone.CN6, TimeSpan.Zero },
            { RealmZone.CN7, TimeSpan.Zero },
            { RealmZone.CN8, TimeSpan.Zero },
            { RealmZone.Tournament5, TimeSpan.Zero },
            { RealmZone.TestServer, TimeSpan.Zero },
            { RealmZone.Tournament6, TimeSpan.Zero },
            { RealmZone.QAServer, TimeSpan.Zero },
            { RealmZone.CN9, TimeSpan.Zero },
        };

        private static(TimeSpan, string)[] _clientSupportedTimezones =
        [
            ( Time.SpanFromMinutes(-480), "America/Los_Angeles"),
            ( Time.SpanFromMinutes(-420), "America/Denver" ),
            ( Time.SpanFromMinutes(-360), "America/Chicago" ),
            ( Time.SpanFromMinutes(-300), "America/New_York" ),
            ( Time.SpanFromMinutes(-180), "America/Sao_Paulo" ),
            ( Time.SpanFromMinutes(0), "Etc/UTC" ),
            ( Time.SpanFromMinutes(60), "Europe/Paris" ),
            ( Time.SpanFromMinutes(480), "Asia/Shanghai" ),
            ( Time.SpanFromMinutes(480), "Asia/Taipei" ),
            ( Time.SpanFromMinutes(540), "Asia/Seoul" ),
            ( Time.SpanFromMinutes(600), "Australia/Melbourne" ),
        ];

        public static TimeSpan GetOffsetByHash(uint hash)
        {
            if (_timezoneOffsetsByHash.TryGetValue(hash, out var offset))
                return offset;

            return TimeSpan.Zero;
        }

        public static TimeSpan GetSystemZoneOffsetAt(DateTime date)
        {
            return TimeZoneInfo.Local.GetUtcOffset(date);
        }

        public static TimeSpan GetSystemZoneOffset()
        {
            return DateTimeOffset.Now.Offset;
        }

        public static string GetSystemZoneName()
        {
            return TimeZoneInfo.Local.StandardName;
        }

        public static string FindClosestClientSupportedTimezone(string currentTimezone, TimeSpan currentTimezoneOffset)
        {
            // try exact match
            var pair = _clientSupportedTimezones.FirstOrDefault(tz => tz.Item2 == currentTimezone);
            if (!pair.Item2.IsEmpty())
                return pair.Item2;

            // try closest offset
            pair = _clientSupportedTimezones.MinBy(left => Math.Abs((left.Item1 - currentTimezoneOffset).Ticks));

            return pair.Item2;
        }
    }

    public enum RealmZone
    {
        None = 0,
        /// <summary> any language </summary>
        Development = 1,
        /// <summary> extended-Latin </summary>
        UnitedStates = 2,
        /// <summary> extended-Latin </summary>
        Oceanic = 3,
        /// <summary> extended-Latin </summary>
        LatinAmerica = 4,
        /// <summary> basic-Latin at create, any at login </summary>
        Tournament = 5,
        /// <summary> East-Asian </summary>
        Korea = 6,
        /// <summary> basic-Latin at create, any at login </summary>
        Tournament2 = 7,
        /// <summary> extended-Latin </summary>
        English = 8,
        /// <summary> extended-Latin </summary>
        German = 9,
        /// <summary> extended-Latin </summary>
        French = 10,
        /// <summary> extended-Latin </summary>
        Spanish = 11,
        /// <summary> Cyrillic </summary>
        Russian = 12,
        /// <summary> basic-Latin at create, any at login </summary>
        Tournament3 = 13,
        /// <summary> East-Asian </summary>
        Taiwan = 14,
        /// <summary> basic-Latin at create, any at login </summary>
        Tournament4 = 15,
        /// <summary> East-Asian </summary>
        China = 16,
        /// <summary> basic-Latin at create, any at login </summary>
        CN1 = 17,
        /// <summary> basic-Latin at create, any at login </summary>
        CN2 = 18,
        /// <summary> basic-Latin at create, any at login </summary>
        CN3 = 19,
        /// <summary> basic-Latin at create, any at login </summary>
        CN4 = 20,
        /// <summary> basic-Latin at create, any at login </summary>
        CN5 = 21,
        /// <summary> basic-Latin at create, any at login </summary>
        CN6 = 22,
        /// <summary> basic-Latin at create, any at login </summary>
        CN7 = 23,
        /// <summary> basic-Latin at create, any at login </summary>
        CN8 = 24,
        /// <summary> basic-Latin at create, any at login </summary>
        Tournament5 = 25,
        /// <summary> any language </summary>
        TestServer = 26,
        /// <summary> basic-Latin at create, any at login </summary>
        Tournament6 = 27,
        /// <summary> any language </summary>
        QAServer = 28,
        /// <summary> basic-Latin at create, any at login </summary>
        CN9 = 29,
    }
}
