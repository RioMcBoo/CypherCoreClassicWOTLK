// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;

namespace Game.DataStorage
{
    public sealed class QuestFactionRewardRecord
    {
        public int Id;
        public short[] Difficulty = new short[10];
    }

    public sealed class QuestInfoRecord
    {
        public int Id;
        public LocalizedString InfoName;
        public sbyte Type;
        public int Modifiers;
        public ushort Profession;
    }

    public sealed class QuestLineXQuestRecord
    {
        public int Id;
        public int QuestLineID;
        public int QuestID;
        public int OrderIndex;
    }

    public sealed class QuestMoneyRewardRecord
    {
        public int Id;
        public int[] Difficulty = new int[10];
    }

    public sealed class QuestPackageItemRecord
    {
        public int Id;
        public ushort PackageID;
        public int ItemID;
        public int ItemQuantity;
        private byte _displayType;

        #region Helpers
        public QuestPackageFilter DisplayType => (QuestPackageFilter)_displayType;
        #endregion
    }

    public sealed class QuestSortRecord
    {
        public int Id;
        public LocalizedString SortName;
        public sbyte UiOrderIndex;
    }

    public sealed class QuestV2Record
    {
        public int Id;
        public ushort UniqueBitFlag;
    }

    public sealed class QuestXPRecord
    {
        public int Id;
        public ushort[] Difficulty = new ushort[10];
    }
}
