// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.DataStorage
{
    public sealed class RandPropPointsRecord
    {
        public int Id;
        public int DamageReplaceStat;
        public int[] Epic = new int[5];
        public int[] Superior = new int[5];
        public int[] Good = new int[5];
    }

    public sealed class RewardPackRecord
    {
        public int Id;
        public int CharTitleID;
        public uint Money;
        public sbyte ArtifactXPDifficulty;
        public float ArtifactXPMultiplier;
        public byte ArtifactXPCategoryID;
        public int TreasurePickerID;
    }

    public sealed class RewardPackXCurrencyTypeRecord
    {
        public int Id;
        public int CurrencyTypeID;
        public int Quantity;
        public int RewardPackID;
    }

    public sealed class RewardPackXItemRecord
    {
        public int Id;
        public int ItemID;
        public int ItemQuantity;
        public int RewardPackID;
    }
}
