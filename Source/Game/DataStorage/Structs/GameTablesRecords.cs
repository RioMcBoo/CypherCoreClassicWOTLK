// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;

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
        public readonly float[] values = new float[(int)GtClass.Max - 1];
        //public readonly float Rogue;
        //public readonly float Druid;
        //public readonly float Hunter;
        //public readonly float Mage;
        //public readonly float Paladin;
        //public readonly float Priest;
        //public readonly float Shaman;
        //public readonly float Warlock;
        //public readonly float Warrior;
        //public readonly float DeathKnight;
        //public readonly float Monk;
        //public readonly float DemonHunter;
    }

    public sealed class GtBattlePetXPRecord
    {
        public readonly float Wins;
        public readonly float Xp;
    }

    public sealed class GtChallengeModeDamageRecord
    {
        public readonly float ChallengeLevel;
        public readonly float Scalar;
    }

    public sealed class GtChallengeModeHealthRecord
    {
        public readonly float ChallengeLevel;
        public readonly float Scalar;
    }
    public sealed class GtChanceToMeleeCritRecord
    {
        public readonly float[] values = new float[(int)Class.Max - 1];
        //public readonly float Warrior;
        //public readonly float Paladin;
        //public readonly float Hunter;
        //public readonly float Rogue;
        //public readonly float Priest;
        //public readonly float DeathKnight;
        //public readonly float Shaman;
        //public readonly float Mage;
        //public readonly float Warlock;
        //public readonly float Monk;
        //public readonly float Druid;
    }

    public sealed class GtChanceToMeleeCritBaseRecord
    {
        public readonly float[] values = new float[(int)Class.Max - 1];
        //public readonly float Warrior;
        //public readonly float Paladin;
        //public readonly float Hunter;
        //public readonly float Rogue;
        //public readonly float Priest;
        //public readonly float DeathKnight;
        //public readonly float Shaman;
        //public readonly float Mage;
        //public readonly float Warlock;
        //public readonly float Monk;
        //public readonly float Druid;
    }

    public sealed class GtChanceToSpellCritRecord
    {
        public readonly float[] values = new float[(int)Class.Max - 1];
        //public readonly float Warrior;
        //public readonly float Paladin;
        //public readonly float Hunter;
        //public readonly float Rogue;
        //public readonly float Priest;
        //public readonly float DeathKnight;
        //public readonly float Shaman;
        //public readonly float Mage;
        //public readonly float Warlock;
        //public readonly float Monk;
        //public readonly float Druid;
    }

    public sealed class GtChanceToSpellCritBaseRecord
    {
        public readonly float[] values = new float[(int)Class.Max - 1];
        //public readonly float Warrior;
        //public readonly float Paladin;
        //public readonly float Hunter;
        //public readonly float Rogue;
        //public readonly float Priest;
        //public readonly float DeathKnight;
        //public readonly float Shaman;
        //public readonly float Mage;
        //public readonly float Warlock;
        //public readonly float Monk;
        //public readonly float Druid;
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

    public sealed class GtHonorLevelRecord
    {
        public readonly float[] values = new float[33];
        //public readonly float Prestige0;
        //public readonly float Prestige1;
        //public readonly float Prestige2;
        //public readonly float Prestige3;
        //public readonly float Prestige4;
        //public readonly float Prestige5;
        //public readonly float Prestige6;
        //public readonly float Prestige7;
        //public readonly float Prestige8;
        //public readonly float Prestige9;
        //public readonly float Prestige10;
        //public readonly float Prestige11;
        //public readonly float Prestige12;
        //public readonly float Prestige13;
        //public readonly float Prestige14;
        //public readonly float Prestige15;
        //public readonly float Prestige16;
        //public readonly float Prestige17;
        //public readonly float Prestige18;
        //public readonly float Prestige19;
        //public readonly float Prestige20;
        //public readonly float Prestige21;
        //public readonly float Prestige22;
        //public readonly float Prestige23;
        //public readonly float Prestige24;
        //public readonly float Prestige25;
        //public readonly float Prestige26;
        //public readonly float Prestige27;
        //public readonly float Prestige28;
        //public readonly float Prestige29;
        //public readonly float Prestige30;
        //public readonly float Prestige31;
        //public readonly float Prestige32;
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
        public readonly float[] values = new float[(int)Class.Max - 1];
        //public readonly float Warrior;
        //public readonly float Paladin;
        //public readonly float Hunter;
        //public readonly float Rogue;
        //public readonly float Priest;
        //public readonly float DeathKnight;
        //public readonly float Shaman;
        //public readonly float Mage;
        //public readonly float Warlock;
        //public readonly float Monk;
        //public readonly float Druid;
    }

    public sealed class GtOCTRegenMPRecord
    {
        public readonly float[] values = new float[(int)Class.Max - 1];
        //public readonly float Warrior;
        //public readonly float Paladin;
        //public readonly float Hunter;
        //public readonly float Rogue;
        //public readonly float Priest;
        //public readonly float DeathKnight;
        //public readonly float Shaman;
        //public readonly float Mage;
        //public readonly float Warlock;
        //public readonly float Monk;
        //public readonly float Druid;
    }

    public sealed class GtRegenHPPerSptRecord
    {
        public readonly float[] values = new float[(int)Class.Max - 1];
        //public readonly float Warrior;
        //public readonly float Paladin;
        //public readonly float Hunter;
        //public readonly float Rogue;
        //public readonly float Priest;
        //public readonly float DeathKnight;
        //public readonly float Shaman;
        //public readonly float Mage;
        //public readonly float Warlock;
        //public readonly float Monk;
        //public readonly float Druid;
    }

    public sealed class GtRegenMPPerSptRecord
    {
        public readonly float[] values = new float[(int)Class.Max - 1];
        //public readonly float Warrior;
        //public readonly float Paladin;
        //public readonly float Hunter;
        //public readonly float Rogue;
        //public readonly float Priest;
        //public readonly float DeathKnight;
        //public readonly float Shaman;
        //public readonly float Mage;
        //public readonly float Warlock;
        //public readonly float Monk;
        //public readonly float Druid;
    }

    public sealed class GtSandboxScalingRecord
    {
        public readonly float P2P_Healing;
        public readonly float E2P_Damage;
        public readonly float P2E_Damage;
    }

    public sealed class GtShieldBlockRegularRecord
    {
        public readonly float[] values = new float[(int)ItemQuality.Max];
        //public readonly float Poor;
        //public readonly float Standard;
        //public readonly float Good;
        //public readonly float Superior;
        //public readonly float Epic;
        //public readonly float Legendary;
        //public readonly float Artifact;
        //public readonly float ScalingStat;
    };

    public sealed class GtSpellScalingRecord
    {
        public readonly float[] values = new float[(int)GtClass.Max - 1];
        //public readonly float Rogue;
        //public readonly float Druid;
        //public readonly float Hunter;
        //public readonly float Mage;
        //public readonly float Paladin;
        //public readonly float Priest;
        //public readonly float Shaman;
        //public readonly float Warlock;
        //public readonly float Warrior;
        //public readonly float DeathKnight;
        //public readonly float Monk;
        //public readonly float DemonHunter;
        public readonly float Item;
        public readonly float Consumable;
        public readonly float Gem1;
        public readonly float Gem2;
        public readonly float Gem3;
        public readonly float Health;
    }
}
