// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;

namespace Game.DataStorage
{
    public sealed class HeirloomRecord
    {
        public LocalizedString SourceText;
        public uint Id;
        private int _itemID;
        public int LegacyUpgradedItemID;
        public int StaticUpgradedItemID;
        public sbyte SourceTypeEnum;
        public byte Flags;
        public int LegacyItemID;
        public int[] UpgradeItemID = new int[6];
        public ushort[] UpgradeItemBonusListID = new ushort[6];

        #region Properties
        public uint ItemID => (uint)_itemID;
        #endregion
    }

    public sealed class HolidaysRecord
    {
        public uint Id;
        public ushort Region;
        public byte Looping;
        public uint HolidayNameID;
        public uint HolidayDescriptionID;
        public byte Priority;
        public sbyte CalendarFilterType;
        public byte Flags;
        public uint WorldStateExpressionID;
        public ushort[] Duration = new ushort[SharedConst.MaxHolidayDurations];
        /// <summary>
        /// dates in unix time starting at January, 1, 2000
        /// </summary>
        public uint[] Date = new uint[SharedConst.MaxHolidayDates];
        public byte[] CalendarFlags = new byte[SharedConst.MaxHolidayFlags];
        public int[] TextureFileDataID = new int[3];
    }
}
