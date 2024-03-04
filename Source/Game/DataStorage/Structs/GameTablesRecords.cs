// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.DataStorage
{
    public sealed class GtArtifactKnowledgeMultiplierRecord
    {
        public readonly float Multiplier;
    }

    public sealed class GtArtifactLevelXPRecord
    {
        public readonly float XP;
        public readonly float XP2;
    }

    public sealed class GtBarberShopCostBaseRecord
    {
        public readonly float Cost;
    }

    public sealed class GtBaseMPRecord
    {
        public readonly float Rogue;
        public readonly float Druid;
        public readonly float Hunter;
        public readonly float Mage;
        public readonly float Paladin;
        public readonly float Priest;
        public readonly float Shaman;
        public readonly float Warlock;
        public readonly float Warrior;
        public readonly float DeathKnight;
        public readonly float Monk;
        public readonly float DemonHunter;
    }

    public sealed class GtBattlePetXPRecord
    {
        public readonly float Wins;
        public readonly float Xp;
    }

    public sealed class GtCombatRatingsRecord
    {
        public readonly float WeaponSkill;
        public readonly float DefenseSkill;
        public readonly float Dodge;
        public readonly float Parry;
        public readonly float Block;
        public readonly float HitMelee;
        public readonly float HitRanged;
        public readonly float HitSpell;
        public readonly float CritMelee;
        public readonly float CritRanged;
        public readonly float CritSpell;
        public readonly float HitTakenMelee;
        public readonly float HitTakenRanged;
        public readonly float HitTakenSpell;
        public readonly float CritTakenMelee;
        public readonly float CritTakenRanged;
        public readonly float CritTakenSpell;
        public readonly float HasteMelee;
        public readonly float HasteRanged;
        public readonly float HasteSpell;
        public readonly float Unknown0;
        public readonly float Unknown1;
        public readonly float Unknown2;
        public readonly float Unknown3;
        public readonly float Unknown4;
        public readonly float Unknown5;
        public readonly float Unknown6;
        public readonly float Unknown7;
        public readonly float Unknown8;
        public readonly float Unknown9;
        public readonly float Unknown10;
        public readonly float Unknown11;
    }

    public sealed class GtCombatRatingsMultByILvlRecord
    {
        public readonly float ArmorMultiplier;
        public readonly float WeaponMultiplier;
        public readonly float TrinketMultiplier;
        public readonly float JewelryMultiplier;
    }

    public sealed class GtHpPerStaRecord
    {
        public readonly float Health;
    }

    public sealed class GtItemSocketCostPerLevelRecord
    {
        public readonly float SocketCost;
    }

    public sealed class GtNpcManaCostScalerRecord
    {
        public readonly float Scaler;
    }

    public sealed class GtOCTRegenHPRecord
    {
        public readonly float Warrior;
        public readonly float Paladin;
        public readonly float Hunter;
        public readonly float Rogue;
        public readonly float Priest;
        public readonly float DeathKnight;
        public readonly float Shaman;
        public readonly float Mage;
        public readonly float Warlock;
        public readonly float Monk;
        public readonly float Druid;
    }

    public sealed class GtOCTRegenMPRecord
    {
        public readonly float Warrior;
        public readonly float Paladin;
        public readonly float Hunter;
        public readonly float Rogue;
        public readonly float Priest;
        public readonly float DeathKnight;
        public readonly float Shaman;
        public readonly float Mage;
        public readonly float Warlock;
        public readonly float Monk;
        public readonly float Druid;
    }

    public sealed class GtRegenHPPerSptRecord
    {
        public readonly float Warrior;
        public readonly float Paladin;
        public readonly float Hunter;
        public readonly float Rogue;
        public readonly float Priest;
        public readonly float DeathKnight;
        public readonly float Shaman;
        public readonly float Mage;
        public readonly float Warlock;
        public readonly float Monk;
        public readonly float Druid;
    }

    public sealed class GtRegenMPPerSptRecord
    {
        public readonly float Warrior;
        public readonly float Paladin;
        public readonly float Hunter;
        public readonly float Rogue;
        public readonly float Priest;
        public readonly float DeathKnight;
        public readonly float Shaman;
        public readonly float Mage;
        public readonly float Warlock;
        public readonly float Monk;
        public readonly float Druid;
    }

    public sealed class GtShieldBlockRegularRecord
    {
        public readonly float Poor;
        public readonly float Standard;
        public readonly float Good;
        public readonly float Superior;
        public readonly float Epic;
        public readonly float Legendary;
        public readonly float Artifact;
        public readonly float ScalingStat;
    };

    public sealed class GtSpellScalingRecord
    {
        public readonly float Rogue;
        public readonly float Druid;
        public readonly float Hunter;
        public readonly float Mage;
        public readonly float Paladin;
        public readonly float Priest;
        public readonly float Shaman;
        public readonly float Warlock;
        public readonly float Warrior;
        public readonly float DeathKnight;
        public readonly float Monk;
        public readonly float DemonHunter;
        public readonly float Item;
        public readonly float Consumable;
        public readonly float Gem1;
        public readonly float Gem2;
        public readonly float Gem3;
        public readonly float Health;
    }

    public sealed class GtStaminaMultByILvlRecord
    {
        public readonly float ArmorMultiplier;
        public readonly float WeaponMultiplier;
        public readonly float TrinketMultiplier;
        public readonly float JewelryMultiplier;
    };

    public sealed class GtXpRecord
    {
        public readonly float Total;
        public readonly float PerKill;
        public readonly float Junk;
        public readonly float Stats;
        public readonly float Divisor;
    }
}
