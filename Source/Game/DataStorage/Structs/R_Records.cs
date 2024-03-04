// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.DataStorage
{
    public sealed class RandPropPointsRecord
    {
        public uint Id;
        public int DamageReplaceStat;
        public uint[] Epic = new uint[5];
        public uint[] Superior = new uint[5];
        public uint[] Good = new uint[5];
    }

    public sealed class RewardPackRecord
    {
        public uint Id;
        public int CharTitleID;
        public uint Money;
        public sbyte ArtifactXPDifficulty;
        public float ArtifactXPMultiplier;
        public byte ArtifactXPCategoryID;
        public uint TreasurePickerID;
    }

    public sealed class RewardPackXCurrencyTypeRecord
    {
        public uint Id;
        public uint CurrencyTypeID;
        public int Quantity;
        public uint RewardPackID;
    }

    public sealed class RewardPackXItemRecord
    {
        public uint Id;
        public int ItemID;
        public int ItemQuantity;
        public uint RewardPackID;
    }
}
