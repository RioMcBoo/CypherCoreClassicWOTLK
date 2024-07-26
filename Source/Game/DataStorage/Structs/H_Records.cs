// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;

namespace Game.DataStorage
{
    public sealed class HeirloomRecord
    {
        public LocalizedString SourceText;
        public int Id;
        public int ItemID;
        public int LegacyUpgradedItemID;
        public int StaticUpgradedItemID;
        public sbyte SourceTypeEnum;
        public byte Flags;
        public int LegacyItemID;
        public int[] UpgradeItemID = new int[6];
        public ushort[] UpgradeItemBonusListID = new ushort[6];
    }

    public sealed class HolidaysRecord
    {
        public int Id;
        public ushort Region;
        private byte _looping;
        public uint HolidayNameID;
        public uint HolidayDescriptionID;
        public byte Priority;
        private sbyte _calendarFilterType;
        public byte Flags;
        public uint WorldStateExpressionID;
        private ushort[] _duration = new ushort[SharedConst.MaxHolidayDurations];
        /// <summary>Wow packed time</summary>
        private int[] _date = new int[SharedConst.MaxHolidayDates];
        public byte[] CalendarFlags = new byte[SharedConst.MaxHolidayFlags];
        public int[] TextureFileDataID = new int[3];

        #region Properties
        public EventPeriodType CalendarFilterType => (EventPeriodType)_calendarFilterType;
        public TimeSpan Duration(int index) => Time.SpanFromHours(_duration[index]);
        /// <summary>Local realm Date time</summary>
        public HolidayTime Date(int index) => (HolidayTime)_date[index];
        public bool IsLooping => _looping != 0 ? true : false;
        #endregion
    }
}
