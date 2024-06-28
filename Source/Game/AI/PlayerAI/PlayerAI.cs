// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Game.AI
{
    public struct Spells
    {
        /* Generic */
        public const int AutoShot = 75;
        public const int Shoot = 3018;
        public const int Throw = 2764;
        public const int Wand = 5019;

        /* Warrior - Generic */
        public const int BattleStance = 2457;
        public const int BerserkerStance = 2458;
        public const int DefensiveStance = 71;
        public const int Charge = 11578;
        public const int Intercept = 20252;
        public const int EnragedRegen = 55694;
        public const int IntimidatingShout = 5246;
        public const int Pummel = 6552;
        public const int ShieldBash = 72;
        public const int Bloodrage = 2687;

        /* Warrior - Arms */
        public const int SweepingStrikes = 12328;
        public const int MortalStrike = 12294;
        public const int Bladestorm = 46924;
        public const int Rend = 47465;
        public const int Retaliation = 20230;
        public const int ShatteringThrow = 64382;
        public const int ThunderClap = 47502;

        /* Warrior - Fury */
        public const int DeathWish = 12292;
        public const int Bloodthirst = 23881;
        public const int PassiveTitansGrip = 46917;
        public const int DemoShout = 47437;
        public const int Execute = 47471;
        public const int HeroicFury = 60970;
        public const int Recklessness = 1719;
        public const int PiercingHowl = 12323;

        /* Warrior - Protection */
        public const int Vigilance = 50720;
        public const int Devastate = 20243;
        public const int Shockwave = 46968;
        public const int ConcussionBlow = 12809;
        public const int Disarm = 676;
        public const int LastStand = 12975;
        public const int ShieldBlock = 2565;
        public const int ShieldSlam = 47488;
        public const int ShieldWall = 871;
        public const int Reflection = 23920;

        /* Paladin - Generic */
        public const int PalAuraMastery = 31821;
        public const int LayOnHands = 48788;
        public const int BlessingOfMight = 48932;
        public const int AvengingWrath = 31884;
        public const int DivineProtection = 498;
        public const int DivineShield = 642;
        public const int HammerOfJustice = 10308;
        public const int HandOfFreedom = 1044;
        public const int HandOfProtection = 10278;
        public const int HandOfSacrifice = 6940;

        /* Paladin - Holy*/
        public const int PassiveIllumination = 20215;
        public const int HolyShock = 20473;
        public const int BeaconOfLight = 53563;
        public const int Consecration = 48819;
        public const int FlashOfLight = 48785;
        public const int HolyLight = 48782;
        public const int DivineFavor = 20216;
        public const int DivineIllumination = 31842;

        /* Paladin - Protection */
        public const int BlessOfSanc = 20911;
        public const int HolyShield = 20925;
        public const int AvengersShield = 48827;
        public const int DivineSacrifice = 64205;
        public const int HammerOfRighteous = 53595;
        public const int RighteousFury = 25780;
        public const int ShieldOfRighteous = 61411;

        /* Paladin - Retribution */
        public const int SealOfCommand = 20375;
        public const int CrusaderStrike = 35395;
        public const int DivineStorm = 53385;
        public const int Judgement = 20271;
        public const int HammerOfWrath = 48806;

        /* Hunter - Generic */
        public const int Deterrence = 19263;
        public const int ExplosiveTrap = 49067;
        public const int FreezingArrow = 60192;
        public const int RapidFire = 3045;
        public const int KillShot = 61006;
        public const int MultiShot = 49048;
        public const int ViperSting = 3034;

        /* Hunter - Beast Mastery */
        public const int BestialWrath = 19574;
        public const int PassiveBeastWithin = 34692;
        public const int PassiveBeastMastery = 53270;

        /* Hunter - Marksmanship */
        public const int AimedShot = 19434;
        public const int PassiveTrueshotAura = 19506;
        public const int ChimeraShot = 53209;
        public const int ArcaneShot = 49045;
        public const int SteadyShot = 49052;
        public const int Readiness = 23989;
        public const int SilencingShot = 34490;

        /* Hunter - Survival */
        public const int PassiveLockAndLoad = 56344;
        public const int WyvernSting = 19386;
        public const int ExplosiveShot = 53301;
        public const int BlackArrow = 3674;

        /* Rogue - Generic */
        public const int Dismantle = 51722;
        public const int Evasion = 26669;
        public const int Kick = 1766;
        public const int Vanish = 26889;
        public const int Blind = 2094;
        public const int CloakOfShadows = 31224;

        /* Rogue - Assassination */
        public const int ColdBlood = 14177;
        public const int Mutilate = 1329;
        public const int HungerForBlood = 51662;
        public const int Envenom = 57993;

        /* Rogue - Combat */
        public const int SinisterStrike = 48637;
        public const int BladeFlurry = 13877;
        public const int AdrenalineRush = 13750;
        public const int KillingSpree = 51690;
        public const int Eviscerate = 48668;

        /* Rogue - Sublety */
        public const int Hemorrhage = 16511;
        public const int Premeditation = 14183;
        public const int ShadowDance = 51713;
        public const int Preparation = 14185;
        public const int Shadowstep = 36554;

        /* Priest - Generic */
        public const int FearWard = 6346;
        public const int PowerWordFort = 48161;
        public const int DivineSpirit = 48073;
        public const int ShadowProtection = 48169;
        public const int DivineHymn = 64843;
        public const int HymnOfHope = 64901;
        public const int ShadowWordDeath = 48158;
        public const int PsychicScream = 10890;

        /* Priest - Discipline */
        public const int PassiveSoulWarding = 63574;
        public const int PowerInfusion = 10060;
        public const int Penance = 47540;
        public const int PainSuppression = 33206;
        public const int InnerFocus = 14751;
        public const int PowerWordShield = 48066;

        /* Priest - Holy */
        public const int PassiveSpiritRedemption = 20711;
        public const int DesperatePrayer = 19236;
        public const int GuardianSpirit = 47788;
        public const int FlashHeal = 48071;
        public const int Renew = 48068;

        /* Priest - Shadow */
        public const int VampiricEmbrace = 15286;
        public const int Shadowform = 15473;
        public const int VampiricTouch = 34914;
        public const int MindFlay = 15407;
        public const int MindBlast = 48127;
        public const int ShadowWordPain = 48125;
        public const int DevouringPlague = 48300;
        public const int Dispersion = 47585;

        /* Death Knight - Generic */
        public const int DeathGrip = 49576;
        public const int Strangulate = 47476;
        public const int EmpowerRuneWeap = 47568;
        public const int IcebornFortitude = 48792;
        public const int AntiMagicShell = 48707;
        public const int DeathCoilDk = 49895;
        public const int MindFreeze = 47528;
        public const int IcyTouch = 49909;
        public const int AuraFrostFever = 55095;
        public const int PlagueStrike = 49921;
        public const int AuraBloodPlague = 55078;
        public const int Pestilence = 50842;

        /* Death Knight - Blood */
        public const int RuneTap = 48982;
        public const int Hysteria = 49016;
        public const int HeartStrike = 55050;
        public const int DeathStrike = 49924;
        public const int BloodStrike = 49930;
        public const int MarkOfBlood = 49005;
        public const int VampiricBlood = 55233;

        /* Death Knight - Frost */
        public const int PassiveIcyTalons = 50887;
        public const int FrostStrike = 49143;
        public const int HowlingBlast = 49184;
        public const int UnbreakableArmor = 51271;
        public const int Obliterate = 51425;
        public const int Deathchill = 49796;

        /* Death Knight - Unholy */
        public const int PassiveUnholyBlight = 49194;
        public const int PassiveMasterOfGhoul = 52143;
        public const int ScourgeStrike = 55090;
        public const int DeathAndDecay = 49938;
        public const int AntiMagicZone = 51052;
        public const int SummonGargoyle = 49206;

        /* Shaman - Generic */
        public const int Heroism = 32182;
        public const int Bloodlust = 2825;
        public const int GroundingTotem = 8177;

        /* Shaman - Elemental*/
        public const int PassiveElementalFocus = 16164;
        public const int TotemOfWrath = 30706;
        public const int Thunderstorm = 51490;
        public const int LightningBolt = 49238;
        public const int EarthShock = 49231;
        public const int FlameShock = 49233;
        public const int LavaBurst = 60043;
        public const int ChainLightning = 49271;
        public const int ElementalMastery = 16166;

        /* Shaman - Enhancement */
        public const int PassiveSpiritWeapons = 16268;
        public const int LavaLash = 60103;
        public const int FeralSpirit = 51533;
        public const int AuraMaelstromWeapon = 53817;
        public const int Stormstrike = 17364;
        public const int ShamanisticRage = 30823;

        /* Shaman - Restoration*/
        public const int ShaNatureSwift = 591;
        public const int ManaTideTotem = 590;
        public const int EarthShield = 49284;
        public const int Riptide = 61295;
        public const int HealingWave = 49273;
        public const int LesserHealWave = 49276;
        public const int TidalForce = 55198;

        /* Mage - Generic */
        public const int DampenMagic = 43015;
        public const int Evocation = 12051;
        public const int ManaShield = 43020;
        public const int MirrorImage = 55342;
        public const int Spellsteal = 30449;
        public const int Counterspell = 2139;
        public const int IceBlock = 45438;

        /* Mage - Arcane */
        public const int FocusMagic = 54646;
        public const int ArcanePower = 12042;
        public const int ArcaneBarrage = 44425;
        public const int ArcaneBlast = 42897;
        public const int AuraArcaneBlast = 36032;
        public const int ArcaneMissiles = 42846;
        public const int PresenceOfMind = 12043;

        /* Mage - Fire */
        public const int Pyroblast = 11366;
        public const int Combustion = 11129;
        public const int LivingBomb = 44457;
        public const int Fireball = 42833;
        public const int FireBlast = 42873;
        public const int DragonsBreath = 31661;
        public const int BlastWave = 11113;

        /* Mage - Frost */
        public const int IcyVeins = 12472;
        public const int IceBarrier = 11426;
        public const int DeepFreeze = 44572;
        public const int FrostNova = 42917;
        public const int Frostbolt = 42842;
        public const int ColdSnap = 11958;
        public const int IceLance = 42914;

        /* Warlock - Generic */
        public const int Fear = 6215;
        public const int HowlOfTerror = 17928;
        public const int Corruption = 47813;
        public const int DeathCoilW = 47860;
        public const int ShadowBolt = 47809;
        public const int Incinerate = 47838;
        public const int Immolate = 47811;
        public const int SeedOfCorruption = 47836;

        /* Warlock - Affliction */
        public const int PassiveSiphonLife = 63108;
        public const int UnstableAffliction = 30108;
        public const int Haunt = 48181;
        public const int CurseOfAgony = 47864;
        public const int DrainSoul = 47855;

        /* Warlock - Demonology */
        public const int SoulLink = 19028;
        public const int DemonicEmpowerment = 47193;
        public const int Metamorphosis = 59672;
        public const int ImmolationAura = 50589;
        public const int DemonCharge = 54785;
        public const int AuraDecimation = 63167;
        public const int AuraMoltenCore = 71165;
        public const int SoulFire = 47825;

        /* Warlock - Destruction */
        public const int Shadowburn = 17877;
        public const int Conflagrate = 17962;
        public const int ChaosBolt = 50796;
        public const int Shadowfury = 47847;

        /* Druid - Generic */
        public const int Barkskin = 22812;
        public const int Innervate = 29166;

        /* Druid - Balance */
        public const int InsectSwarm = 5570;
        public const int MoonkinForm = 24858;
        public const int Starfall = 48505;
        public const int Typhoon = 61384;
        public const int AuraEclipseLunar = 48518;
        public const int Moonfire = 48463;
        public const int Starfire = 48465;
        public const int Wrath = 48461;

        /* Druid - Feral */
        public const int CatForm = 768;
        public const int SurvivalInstincts = 61336;
        public const int Mangle = 33917;
        public const int Berserk = 50334;
        public const int MangleCat = 48566;
        public const int FeralChargeCat = 49376;
        public const int Rake = 48574;
        public const int Rip = 49800;
        public const int SavageRoar = 52610;
        public const int TigerFury = 50213;
        public const int Claw = 48570;
        public const int Dash = 33357;
        public const int Maim = 49802;

        /* Druid - Restoration */
        public const int Swiftmend = 18562;
        public const int TreeOfLife = 33891;
        public const int WildGrowth = 48438;
        public const int NatureSwiftness = 17116;
        public const int Tranquility = 48447;
        public const int Nourish = 50464;
        public const int HealingTouch = 48378;
        public const int Rejuvenation = 48441;
        public const int Regrowth = 48443;
        public const int Lifebloom = 48451;
    }

    public class PlayerAI : UnitAI
    {
        protected new Player me;
        ChrSpecialization _selfSpec;
        bool _isSelfHealer;
        bool _isSelfRangedAttacker;

        public PlayerAI(Player player) : base(player)
        {
            me = player;
            _selfSpec = player.GetPrimarySpecialization();
            _isSelfHealer = IsPlayerHealer(player);
            _isSelfRangedAttacker = IsPlayerRangedAttacker(player);
        }

        bool IsPlayerHealer(Player who)
        {
            if (who == null)
                return false;

            var chrSpec = who.GetPrimarySpecializationEntry();
            return chrSpec != null && chrSpec.Role == ChrSpecializationRole.Healer;
        }

        bool IsPlayerRangedAttacker(Player who)
        {
            if (who == null)
                return false;

            var chrSpec = who.GetPrimarySpecializationEntry();
            return chrSpec != null && chrSpec.HasFlag(ChrSpecializationFlag.Ranged);
        }

        (Spell, Unit) VerifySpellCast(int spellId, Unit target)
        {
            // Find highest spell rank that we know
            int knownRank, nextRank;
            if (me.HasSpell(spellId))
            {
                // this will save us some lookups if the player has the highest rank (expected case)
                knownRank = spellId;
                nextRank = Global.SpellMgr.GetNextSpellInChain(spellId);
            }
            else
            {
                knownRank = 0;
                nextRank = Global.SpellMgr.GetFirstSpellInChain(spellId);
            }

            while (nextRank != 0 && me.HasSpell(nextRank))
            {
                knownRank = nextRank;
                nextRank = Global.SpellMgr.GetNextSpellInChain(knownRank);
            }

            if (knownRank == 0)
                return default;

            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(knownRank, me.GetMap().GetDifficultyID());
            if (spellInfo == null)
                return default;

            if (me.GetSpellHistory().HasGlobalCooldown(spellInfo))
                return default;

            Spell spell = new(me, spellInfo, TriggerCastFlags.None);
            if (spell.CanAutoCast(target))
                return (spell, target);

            return default;
        }

        public (Spell, Unit) VerifySpellCast(int spellId, SpellTarget target)
        {
            Unit pTarget = null;
            switch (target)
            {
                case SpellTarget.None:
                    break;
                case SpellTarget.Victim:
                    pTarget = me.GetVictim();
                    if (pTarget == null)
                        return default;
                    break;
                case SpellTarget.Charmer:
                    pTarget = me.GetCharmer();
                    if (pTarget == null)
                        return default;
                    break;
                case SpellTarget.Self:
                    pTarget = me;
                    break;
            }

            return VerifySpellCast(spellId, pTarget);
        }

        public (Spell, Unit) SelectSpellCast(List<((Spell, Unit), int)> spells)
        {
            if (spells.Empty())
                return default;

            int totalWeights = 0;
            foreach (var wSpell in spells)
                totalWeights += wSpell.Item2;

            (Spell, Unit) selected = default;
            int randNum = RandomHelper.IRand(0, totalWeights - 1);
            foreach (var wSpell in spells)
            {
                if (selected != default)
                {
                    //delete wSpell.first.first;
                    continue;
                }

                if (randNum < wSpell.Item2)
                    selected = wSpell.Item1;
                else
                {
                    randNum -= wSpell.Item2;
                    //delete wSpell.first.first;
                }
            }

            spells.Clear();
            return selected;
        }

        public void VerifyAndPushSpellCast<T>(List<((Spell, Unit), int)> spells, int spellId, T target, int weight) where T : Unit
        {
            (Spell, Unit) spell = VerifySpellCast(spellId, target);
            if (spell != default)
                spells.Add((spell, weight));
        }

        public void DoCastAtTarget((Spell, Unit) spell)
        {
            SpellCastTargets targets = new();
            targets.SetUnitTarget(spell.Item2);
            spell.Item1.Prepare(targets);
        }

        void DoRangedAttackIfReady()
        {
            if (me.HasUnitState(UnitState.Casting))
                return;

            if (!me.IsAttackReady(WeaponAttackType.RangedAttack))
                return;

            Unit victim = me.GetVictim();
            if (victim == null) 
                return;

            int rangedAttackSpell = 0;

            Item rangedItem = me.GetItemByPos(EquipmentSlot.Ranged);
            ItemTemplate rangedTemplate = rangedItem != null? rangedItem.GetTemplate() : null;
            if (rangedTemplate != null)
            {
                switch (rangedTemplate.GetSubClass().Weapon)
                {
                    case ItemSubClassWeapon.Bow:
                    case ItemSubClassWeapon.Gun:
                    case ItemSubClassWeapon.Crossbow:
                        rangedAttackSpell = Spells.Shoot;
                        break;
                    case ItemSubClassWeapon.Thrown:
                        rangedAttackSpell = Spells.Throw;
                        break;
                    case ItemSubClassWeapon.Wand:
                        rangedAttackSpell = Spells.Wand;
                        break;
                }
            }

            if (rangedAttackSpell == 0)
                return;

            SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(rangedAttackSpell, me.GetMap().GetDifficultyID());
            if (spellInfo == null)
                return;

            Spell spell = new(me, spellInfo, TriggerCastFlags.CastDirectly);
            if (spell.CheckPetCast(victim) != SpellCastResult.SpellCastOk)
                return;

            SpellCastTargets targets = new();
            targets.SetUnitTarget(victim);
            spell.Prepare(targets);

            me.ResetAttackTimer(WeaponAttackType.RangedAttack);
        }

        public void DoAutoAttackIfReady()
        {
            if (IsRangedAttacker())
                DoRangedAttackIfReady();
        }

        public void CancelAllShapeshifts()
        {
            List<AuraEffect> shapeshiftAuras = me.GetAuraEffectsByType(AuraType.ModShapeshift);
            List<Aura> removableShapeshifts = new();
            foreach (AuraEffect auraEff in shapeshiftAuras)
            {
                Aura aura = auraEff.GetBase();
                if (aura == null)
                    continue;
                SpellInfo auraInfo = aura.GetSpellInfo();
                if (auraInfo == null)
                    continue;
                if (auraInfo.HasAttribute(SpellAttr0.NoAuraCancel))
                    continue;
                if (!auraInfo.IsPositive() || auraInfo.IsPassive())
                    continue;
                removableShapeshifts.Add(aura);
            }

            foreach (Aura aura in removableShapeshifts)
                me.RemoveOwnedAura(aura, AuraRemoveMode.Cancel);
        }

        public Creature GetCharmer()
        {
            if (me.GetCharmerGUID().IsCreature())
                return ObjectAccessor.GetCreature(me, me.GetCharmerGUID());
            return null;
        }

        // helper functions to determine player info
        public bool IsHealer(Player who = null)
        {
            return (who == null || who == me) ? _isSelfHealer : IsPlayerHealer(who);
        }
        public bool IsRangedAttacker(Player who = null) { return (who == null || who == me) ? _isSelfRangedAttacker : IsPlayerRangedAttacker(who); }
        public ChrSpecialization GetSpec(Player who = null) { return (who == null || who == me) ? _selfSpec : who.GetPrimarySpecialization(); }
        public void SetIsRangedAttacker(bool state) { _isSelfRangedAttacker = state; } // this allows overriding of the default ranged attacker detection

        public virtual Unit SelectAttackTarget() { return me.GetCharmer() != null ? me.GetCharmer().GetVictim() : null; }

        public enum SpellTarget
        {
            None,
            Victim,
            Charmer,
            Self
        }
    }

    class SimpleCharmedPlayerAI : PlayerAI
    {
        const float CASTER_CHASE_DISTANCE = 28.0f;

        uint _castCheckTimer;
        bool _chaseCloser;
        bool _forceFacing;
        bool _isFollowing;

        public SimpleCharmedPlayerAI(Player player) : base(player)
        {
            _castCheckTimer = 2500;
            _chaseCloser = false;
            _forceFacing = true;
        }

        public override bool CanAIAttack(Unit who)
        {
            if (!me.IsValidAttackTarget(who) || who.HasBreakableByDamageCrowdControlAura())
                return false;

            Unit charmer = me.GetCharmer();
            if (charmer != null)
            {
                if (!charmer.IsValidAttackTarget(who))
                    return false;
            }

            return base.CanAIAttack(who);
        }

        public override Unit SelectAttackTarget()
        {
            Unit charmer = me.GetCharmer();
            if (charmer != null)
            {
                UnitAI charmerAI = charmer.GetAI();
                if (charmerAI != null)
                    return charmerAI.SelectTarget(SelectTargetMethod.Random, 0, new ValidTargetSelectPredicate(this));

                return charmer.GetVictim();
            }

            return null;
        }

        (Spell, Unit) SelectAppropriateCastForSpec()
        {
            List<((Spell, Unit), int)> spells = new();
            /*
            switch (me.getClass())
            {
                case CLASS_WARRIOR:
                    if (!me.IsWithinMeleeRange(me.GetVictim()))
                    {
                        VerifyAndPushSpellCast(spells, SPELL_CHARGE, TARGET_VICTIM, 15);
                        VerifyAndPushSpellCast(spells, SPELL_INTERCEPT, TARGET_VICTIM, 10);
                    }
                    VerifyAndPushSpellCast(spells, SPELL_ENRAGED_REGEN, TARGET_NONE, 3);
                    VerifyAndPushSpellCast(spells, SPELL_INTIMIDATING_SHOUT, TARGET_VICTIM, 4);
                    if (me.GetVictim() && me.GetVictim().HasUnitState(UNIT_STATE_CASTING))
                    {
                        VerifyAndPushSpellCast(spells, SPELL_PUMMEL, TARGET_VICTIM, 15);
                        VerifyAndPushSpellCast(spells, SPELL_SHIELD_BASH, TARGET_VICTIM, 15);
                    }
                    VerifyAndPushSpellCast(spells, SPELL_BLOODRAGE, TARGET_NONE, 5);
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_WARRIOR_PROTECTION:
                            VerifyAndPushSpellCast(spells, SPELL_SHOCKWAVE, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_CONCUSSION_BLOW, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_DISARM, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_LAST_STAND, TARGET_NONE, 5);
                            VerifyAndPushSpellCast(spells, SPELL_SHIELD_BLOCK, TARGET_NONE, 1);
                            VerifyAndPushSpellCast(spells, SPELL_SHIELD_SLAM, TARGET_VICTIM, 4);
                            VerifyAndPushSpellCast(spells, SPELL_SHIELD_WALL, TARGET_NONE, 5);
                            VerifyAndPushSpellCast(spells, SPELL_SPELL_REFLECTION, TARGET_NONE, 3);
                            VerifyAndPushSpellCast(spells, SPELL_DEVASTATE, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_REND, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_THUNDER_CLAP, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_DEMO_SHOUT, TARGET_VICTIM, 1);
                            break;
                        case TALENT_SPEC_WARRIOR_ARMS:
                            VerifyAndPushSpellCast(spells, SPELL_SWEEPING_STRIKES, TARGET_NONE, 2);
                            VerifyAndPushSpellCast(spells, SPELL_MORTAL_STRIKE, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_BLADESTORM, TARGET_NONE, 10);
                            VerifyAndPushSpellCast(spells, SPELL_REND, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_RETALIATION, TARGET_NONE, 3);
                            VerifyAndPushSpellCast(spells, SPELL_SHATTERING_THROW, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_SWEEPING_STRIKES, TARGET_NONE, 5);
                            VerifyAndPushSpellCast(spells, SPELL_THUNDER_CLAP, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_EXECUTE, TARGET_VICTIM, 15);
                            break;
                        case TALENT_SPEC_WARRIOR_FURY:
                            VerifyAndPushSpellCast(spells, SPELL_DEATH_WISH, TARGET_NONE, 10);
                            VerifyAndPushSpellCast(spells, SPELL_BLOODTHIRST, TARGET_VICTIM, 4);
                            VerifyAndPushSpellCast(spells, SPELL_DEMO_SHOUT, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_EXECUTE, TARGET_VICTIM, 15);
                            VerifyAndPushSpellCast(spells, SPELL_HEROIC_FURY, TARGET_NONE, 5);
                            VerifyAndPushSpellCast(spells, SPELL_RECKLESSNESS, TARGET_NONE, 8);
                            VerifyAndPushSpellCast(spells, SPELL_PIERCING_HOWL, TARGET_VICTIM, 2);
                            break;
                    }
                    break;
                case CLASS_PALADIN:
                    VerifyAndPushSpellCast(spells, SPELL_AURA_MASTERY, TARGET_NONE, 3);
                    VerifyAndPushSpellCast(spells, SPELL_LAY_ON_HANDS, TARGET_CHARMER, 8);
                    VerifyAndPushSpellCast(spells, SPELL_BLESSING_OF_MIGHT, TARGET_CHARMER, 8);
                    VerifyAndPushSpellCast(spells, SPELL_AVENGING_WRATH, TARGET_NONE, 5);
                    VerifyAndPushSpellCast(spells, SPELL_DIVINE_PROTECTION, TARGET_NONE, 4);
                    VerifyAndPushSpellCast(spells, SPELL_DIVINE_SHIELD, TARGET_NONE, 2);
                    VerifyAndPushSpellCast(spells, SPELL_HAMMER_OF_JUSTICE, TARGET_VICTIM, 6);
                    VerifyAndPushSpellCast(spells, SPELL_HAND_OF_FREEDOM, TARGET_SELF, 3);
                    VerifyAndPushSpellCast(spells, SPELL_HAND_OF_PROTECTION, TARGET_SELF, 1);
                    if (Creature* creatureCharmer = GetCharmer())
                    {
                        if (creatureCharmer.IsDungeonBoss() || creatureCharmer.isWorldBoss())
                            VerifyAndPushSpellCast(spells, SPELL_HAND_OF_SACRIFICE, creatureCharmer, 10);
                        else
                            VerifyAndPushSpellCast(spells, SPELL_HAND_OF_PROTECTION, creatureCharmer, 3);
                    }

                    switch (GetSpec())
                    {
                        case TALENT_SPEC_PALADIN_PROTECTION:
                            VerifyAndPushSpellCast(spells, SPELL_HAMMER_OF_RIGHTEOUS, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_DIVINE_SACRIFICE, TARGET_NONE, 2);
                            VerifyAndPushSpellCast(spells, SPELL_SHIELD_OF_RIGHTEOUS, TARGET_VICTIM, 4);
                            VerifyAndPushSpellCast(spells, SPELL_JUDGEMENT, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_CONSECRATION, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_HOLY_SHIELD, TARGET_NONE, 1);
                            break;
                        case TALENT_SPEC_PALADIN_HOLY:
                            VerifyAndPushSpellCast(spells, SPELL_HOLY_SHOCK, TARGET_CHARMER, 3);
                            VerifyAndPushSpellCast(spells, SPELL_HOLY_SHOCK, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_FLASH_OF_LIGHT, TARGET_CHARMER, 4);
                            VerifyAndPushSpellCast(spells, SPELL_HOLY_LIGHT, TARGET_CHARMER, 3);
                            VerifyAndPushSpellCast(spells, SPELL_DIVINE_FAVOR, TARGET_NONE, 5);
                            VerifyAndPushSpellCast(spells, SPELL_DIVINE_ILLUMINATION, TARGET_NONE, 3);
                            break;
                        case TALENT_SPEC_PALADIN_RETRIBUTION:
                            VerifyAndPushSpellCast(spells, SPELL_CRUSADER_STRIKE, TARGET_VICTIM, 4);
                            VerifyAndPushSpellCast(spells, SPELL_DIVINE_STORM, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_JUDGEMENT, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_HAMMER_OF_WRATH, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_RIGHTEOUS_FURY, TARGET_NONE, 2);
                            break;
                    }
                    break;
                case CLASS_HUNTER:
                    VerifyAndPushSpellCast(spells, SPELL_DETERRENCE, TARGET_NONE, 3);
                    VerifyAndPushSpellCast(spells, SPELL_EXPLOSIVE_TRAP, TARGET_NONE, 1);
                    VerifyAndPushSpellCast(spells, SPELL_FREEZING_ARROW, TARGET_VICTIM, 2);
                    VerifyAndPushSpellCast(spells, SPELL_RAPID_FIRE, TARGET_NONE, 10);
                    VerifyAndPushSpellCast(spells, SPELL_KILL_SHOT, TARGET_VICTIM, 10);
                    if (me.GetVictim() && me.GetVictim().getPowerType() == POWER_MANA && !me.GetVictim().GetAuraApplicationOfRankedSpell(SPELL_VIPER_STING, me.GetGUID()))
                        VerifyAndPushSpellCast(spells, SPELL_VIPER_STING, TARGET_VICTIM, 5);

                    switch (GetSpec())
                    {
                        case TALENT_SPEC_HUNTER_BEASTMASTER:
                            VerifyAndPushSpellCast(spells, SPELL_AIMED_SHOT, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_ARCANE_SHOT, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_STEADY_SHOT, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_MULTI_SHOT, TARGET_VICTIM, 2);
                            break;
                        case TALENT_SPEC_HUNTER_MARKSMAN:
                            VerifyAndPushSpellCast(spells, SPELL_AIMED_SHOT, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_CHIMERA_SHOT, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_ARCANE_SHOT, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_STEADY_SHOT, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_READINESS, TARGET_NONE, 10);
                            VerifyAndPushSpellCast(spells, SPELL_SILENCING_SHOT, TARGET_VICTIM, 5);
                            break;
                        case TALENT_SPEC_HUNTER_SURVIVAL:
                            VerifyAndPushSpellCast(spells, SPELL_EXPLOSIVE_SHOT, TARGET_VICTIM, 8);
                            VerifyAndPushSpellCast(spells, SPELL_BLACK_ARROW, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_MULTI_SHOT, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_STEADY_SHOT, TARGET_VICTIM, 1);
                            break;
                    }
                    break;
                case CLASS_ROGUE:
                {
                    VerifyAndPushSpellCast(spells, SPELL_DISMANTLE, TARGET_VICTIM, 8);
                    VerifyAndPushSpellCast(spells, SPELL_EVASION, TARGET_NONE, 8);
                    VerifyAndPushSpellCast(spells, SPELL_VANISH, TARGET_NONE, 4);
                    VerifyAndPushSpellCast(spells, SPELL_BLIND, TARGET_VICTIM, 2);
                    VerifyAndPushSpellCast(spells, SPELL_CLOAK_OF_SHADOWS, TARGET_NONE, 2);

                    uint32 builder, finisher;
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_ROGUE_ASSASSINATION:
                            builder = SPELL_MUTILATE, finisher = SPELL_ENVENOM;
                            VerifyAndPushSpellCast(spells, SPELL_COLD_BLOOD, TARGET_NONE, 20);
                            break;
                        case TALENT_SPEC_ROGUE_COMBAT:
                            builder = SPELL_SINISTER_STRIKE, finisher = SPELL_EVISCERATE;
                            VerifyAndPushSpellCast(spells, SPELL_ADRENALINE_RUSH, TARGET_NONE, 6);
                            VerifyAndPushSpellCast(spells, SPELL_BLADE_FLURRY, TARGET_NONE, 5);
                            VerifyAndPushSpellCast(spells, SPELL_KILLING_SPREE, TARGET_NONE, 25);
                            break;
                        case TALENT_SPEC_ROGUE_SUBTLETY:
                            builder = SPELL_HEMORRHAGE, finisher = SPELL_EVISCERATE;
                            VerifyAndPushSpellCast(spells, SPELL_PREPARATION, TARGET_NONE, 10);
                            if (!me.IsWithinMeleeRange(me.GetVictim()))
                                VerifyAndPushSpellCast(spells, SPELL_SHADOWSTEP, TARGET_VICTIM, 25);
                            VerifyAndPushSpellCast(spells, SPELL_SHADOW_DANCE, TARGET_NONE, 10);
                            break;
                    }

                    if (Unit* victim = me.GetVictim())
                    {
                        if (victim.HasUnitState(UNIT_STATE_CASTING))
                            VerifyAndPushSpellCast(spells, SPELL_KICK, TARGET_VICTIM, 25);

                        uint8 const cp = me.GetPower(POWER_COMBO_POINTS);
                        if (cp >= 4)
                            VerifyAndPushSpellCast(spells, finisher, TARGET_VICTIM, 10);
                        if (cp <= 4)
                            VerifyAndPushSpellCast(spells, builder, TARGET_VICTIM, 5);
                    }
                    break;
                }
                case CLASS_PRIEST:
                    VerifyAndPushSpellCast(spells, SPELL_FEAR_WARD, TARGET_SELF, 2);
                    VerifyAndPushSpellCast(spells, SPELL_POWER_WORD_FORT, TARGET_CHARMER, 1);
                    VerifyAndPushSpellCast(spells, SPELL_DIVINE_SPIRIT, TARGET_CHARMER, 1);
                    VerifyAndPushSpellCast(spells, SPELL_SHADOW_PROTECTION, TARGET_CHARMER, 2);
                    VerifyAndPushSpellCast(spells, SPELL_DIVINE_HYMN, TARGET_NONE, 5);
                    VerifyAndPushSpellCast(spells, SPELL_HYMN_OF_HOPE, TARGET_NONE, 5);
                    VerifyAndPushSpellCast(spells, SPELL_SHADOW_WORD_DEATH, TARGET_VICTIM, 1);
                    VerifyAndPushSpellCast(spells, SPELL_PSYCHIC_SCREAM, TARGET_VICTIM, 3);
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_PRIEST_DISCIPLINE:
                            VerifyAndPushSpellCast(spells, SPELL_POWER_WORD_SHIELD, TARGET_CHARMER, 3);
                            VerifyAndPushSpellCast(spells, SPELL_INNER_FOCUS, TARGET_NONE, 3);
                            VerifyAndPushSpellCast(spells, SPELL_PAIN_SUPPRESSION, TARGET_CHARMER, 15);
                            VerifyAndPushSpellCast(spells, SPELL_POWER_INFUSION, TARGET_CHARMER, 10);
                            VerifyAndPushSpellCast(spells, SPELL_PENANCE, TARGET_CHARMER, 3);
                            VerifyAndPushSpellCast(spells, SPELL_FLASH_HEAL, TARGET_CHARMER, 1);
                            break;
                        case TALENT_SPEC_PRIEST_HOLY:
                            VerifyAndPushSpellCast(spells, SPELL_DESPERATE_PRAYER, TARGET_NONE, 3);
                            VerifyAndPushSpellCast(spells, SPELL_GUARDIAN_SPIRIT, TARGET_CHARMER, 5);
                            VerifyAndPushSpellCast(spells, SPELL_FLASH_HEAL, TARGET_CHARMER, 1);
                            VerifyAndPushSpellCast(spells, SPELL_RENEW, TARGET_CHARMER, 3);
                            break;
                        case TALENT_SPEC_PRIEST_SHADOW:
                            if (!me.HasAura(SPELL_SHADOWFORM))
                            {
                                VerifyAndPushSpellCast(spells, SPELL_SHADOWFORM, TARGET_NONE, 100);
                                break;
                            }
                            if (Unit* victim = me.GetVictim())
                            {
                                if (!victim.GetAuraApplicationOfRankedSpell(SPELL_VAMPIRIC_TOUCH, me.GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_VAMPIRIC_TOUCH, TARGET_VICTIM, 4);
                                if (!victim.GetAuraApplicationOfRankedSpell(SPELL_SHADOW_WORD_PAIN, me.GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_SHADOW_WORD_PAIN, TARGET_VICTIM, 3);
                                if (!victim.GetAuraApplicationOfRankedSpell(SPELL_DEVOURING_PLAGUE, me.GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_DEVOURING_PLAGUE, TARGET_VICTIM, 4);
                            }
                            VerifyAndPushSpellCast(spells, SPELL_MIND_BLAST, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_MIND_FLAY, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_DISPERSION, TARGET_NONE, 10);
                            break;
                    }
                    break;
                case CLASS_DEATH_KNIGHT:
                {
                    if (!me.IsWithinMeleeRange(me.GetVictim()))
                        VerifyAndPushSpellCast(spells, SPELL_DEATH_GRIP, TARGET_VICTIM, 25);
                    VerifyAndPushSpellCast(spells, SPELL_STRANGULATE, TARGET_VICTIM, 15);
                    VerifyAndPushSpellCast(spells, SPELL_EMPOWER_RUNE_WEAP, TARGET_NONE, 5);
                    VerifyAndPushSpellCast(spells, SPELL_ICEBORN_FORTITUDE, TARGET_NONE, 15);
                    VerifyAndPushSpellCast(spells, SPELL_ANTI_MAGIC_SHELL, TARGET_NONE, 10);

                    bool hasFF = false, hasBP = false;
                    if (Unit* victim = me.GetVictim())
                    {
                        if (victim.HasUnitState(UNIT_STATE_CASTING))
                            VerifyAndPushSpellCast(spells, SPELL_MIND_FREEZE, TARGET_VICTIM, 25);

                        hasFF = !!victim.GetAuraApplicationOfRankedSpell(AURA_FROST_FEVER, me.GetGUID()), hasBP = !!victim.GetAuraApplicationOfRankedSpell(AURA_BLOOD_PLAGUE, me.GetGUID());
                        if (hasFF && hasBP)
                            VerifyAndPushSpellCast(spells, SPELL_PESTILENCE, TARGET_VICTIM, 3);
                        if (!hasFF)
                            VerifyAndPushSpellCast(spells, SPELL_ICY_TOUCH, TARGET_VICTIM, 4);
                        if (!hasBP)
                            VerifyAndPushSpellCast(spells, SPELL_PLAGUE_STRIKE, TARGET_VICTIM, 4);
                    }
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_DEATHKNIGHT_BLOOD:
                            VerifyAndPushSpellCast(spells, SPELL_RUNE_TAP, TARGET_NONE, 2);
                            VerifyAndPushSpellCast(spells, SPELL_HYSTERIA, TARGET_SELF, 5);
                            if (Creature* creatureCharmer = GetCharmer())
                                if (creature == nullCharmer.IsDungeonBoss() && !creatureCharmer.isWorldBoss())
                                    VerifyAndPushSpellCast(spells, SPELL_HYSTERIA, creatureCharmer, 15);
                            VerifyAndPushSpellCast(spells, SPELL_HEART_STRIKE, TARGET_VICTIM, 2);
                            if (hasFF && hasBP)
                                VerifyAndPushSpellCast(spells, SPELL_DEATH_STRIKE, TARGET_VICTIM, 8);
                            VerifyAndPushSpellCast(spells, SPELL_DEATH_COIL_DK, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_MARK_OF_BLOOD, TARGET_VICTIM, 20);
                            VerifyAndPushSpellCast(spells, SPELL_VAMPIRIC_BLOOD, TARGET_NONE, 10);
                            break;
                        case TALENT_SPEC_DEATHKNIGHT_FROST:
                            if (hasFF && hasBP)
                                VerifyAndPushSpellCast(spells, SPELL_OBLITERATE, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_HOWLING_BLAST, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_UNBREAKABLE_ARMOR, TARGET_NONE, 10);
                            VerifyAndPushSpellCast(spells, SPELL_DEATHCHILL, TARGET_NONE, 10);
                            VerifyAndPushSpellCast(spells, SPELL_FROST_STRIKE, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_BLOOD_STRIKE, TARGET_VICTIM, 1);
                            break;
                        case TALENT_SPEC_DEATHKNIGHT_UNHOLY:
                            if (hasFF && hasBP)
                                VerifyAndPushSpellCast(spells, SPELL_SCOURGE_STRIKE, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_DEATH_AND_DECAY, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_ANTI_MAGIC_ZONE, TARGET_NONE, 8);
                            VerifyAndPushSpellCast(spells, SPELL_SUMMON_GARGOYLE, TARGET_VICTIM, 7);
                            VerifyAndPushSpellCast(spells, SPELL_BLOOD_STRIKE, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_DEATH_COIL_DK, TARGET_VICTIM, 3);
                            break;
                    }
                    break;
                }
                case CLASS_SHAMAN:
                    VerifyAndPushSpellCast(spells, SPELL_HEROISM, TARGET_NONE, 25);
                    VerifyAndPushSpellCast(spells, SPELL_BLOODLUST, TARGET_NONE, 25);
                    VerifyAndPushSpellCast(spells, SPELL_GROUNDING_TOTEM, TARGET_NONE, 2);
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_SHAMAN_RESTORATION:
                            if (Unit* charmer = me.GetCharmer())
                                if (!charmer.GetAuraApplicationOfRankedSpell(SPELL_EARTH_SHIELD, me.GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_EARTH_SHIELD, charmer, 2);
                            if (me.HasAura(SPELL_SHA_NATURE_SWIFT))
                                VerifyAndPushSpellCast(spells, SPELL_HEALING_WAVE, TARGET_CHARMER, 20);
                            else
                                VerifyAndPushSpellCast(spells, SPELL_LESSER_HEAL_WAVE, TARGET_CHARMER, 1);
                            VerifyAndPushSpellCast(spells, SPELL_TIDAL_FORCE, TARGET_NONE, 4);
                            VerifyAndPushSpellCast(spells, SPELL_SHA_NATURE_SWIFT, TARGET_NONE, 4);
                            VerifyAndPushSpellCast(spells, SPELL_MANA_TIDE_TOTEM, TARGET_NONE, 3);
                            break;
                        case TALENT_SPEC_SHAMAN_ELEMENTAL:
                            if (Unit* victim = me.GetVictim())
                            {
                                if (victim.GetAuraOfRankedSpell(SPELL_FLAME_SHOCK, GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_LAVA_BURST, TARGET_VICTIM, 5);
                                else
                                    VerifyAndPushSpellCast(spells, SPELL_FLAME_SHOCK, TARGET_VICTIM, 3);
                            }
                            VerifyAndPushSpellCast(spells, SPELL_CHAIN_LIGHTNING, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_LIGHTNING_BOLT, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_ELEMENTAL_MASTERY, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_THUNDERSTORM, TARGET_NONE, 3);
                            break;
                        case TALENT_SPEC_SHAMAN_ENHANCEMENT:
                            if (Aura const* maelstrom = me.GetAura(AURA_MAELSTROM_WEAPON))
                                if (maelstrom.GetStackAmount() == 5)
                                    VerifyAndPushSpellCast(spells, SPELL_LIGHTNING_BOLT, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_STORMSTRIKE, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_EARTH_SHOCK, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_LAVA_LASH, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_SHAMANISTIC_RAGE, TARGET_NONE, 10);
                            break;
                    }
                    break;
                case CLASS_MAGE:
                    if (me.GetVictim() && me.GetVictim().HasUnitState(UNIT_STATE_CASTING))
                        VerifyAndPushSpellCast(spells, SPELL_COUNTERSPELL, TARGET_VICTIM, 25);
                    VerifyAndPushSpellCast(spells, SPELL_DAMPEN_MAGIC, TARGET_CHARMER, 2);
                    VerifyAndPushSpellCast(spells, SPELL_EVOCATION, TARGET_NONE, 3);
                    VerifyAndPushSpellCast(spells, SPELL_MANA_SHIELD, TARGET_NONE, 1);
                    VerifyAndPushSpellCast(spells, SPELL_MIRROR_IMAGE, TARGET_NONE, 3);
                    VerifyAndPushSpellCast(spells, SPELL_SPELLSTEAL, TARGET_VICTIM, 2);
                    VerifyAndPushSpellCast(spells, SPELL_ICE_BLOCK, TARGET_NONE, 1);
                    VerifyAndPushSpellCast(spells, SPELL_ICY_VEINS, TARGET_NONE, 3);
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_MAGE_ARCANE:
                            if (Aura* abAura = me.GetAura(AURA_ARCANE_BLAST))
                                if (abAura.GetStackAmount() >= 3)
                                    VerifyAndPushSpellCast(spells, SPELL_ARCANE_MISSILES, TARGET_VICTIM, 7);
                            VerifyAndPushSpellCast(spells, SPELL_ARCANE_BLAST, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_ARCANE_BARRAGE, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_ARCANE_POWER, TARGET_NONE, 8);
                            VerifyAndPushSpellCast(spells, SPELL_PRESENCE_OF_MIND, TARGET_NONE, 7);
                            break;
                        case TALENT_SPEC_MAGE_FIRE:
                            if (me.GetVictim() && !me.GetVictim().GetAuraApplicationOfRankedSpell(SPELL_LIVING_BOMB))
                                VerifyAndPushSpellCast(spells, SPELL_LIVING_BOMB, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_COMBUSTION, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_FIREBALL, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_FIRE_BLAST, TARGET_VICTIM, 1);
                            VerifyAndPushSpellCast(spells, SPELL_DRAGONS_BREATH, TARGET_VICTIM, 2);
                            VerifyAndPushSpellCast(spells, SPELL_BLAST_WAVE, TARGET_VICTIM, 1);
                            break;
                        case TALENT_SPEC_MAGE_FROST:
                            VerifyAndPushSpellCast(spells, SPELL_DEEP_FREEZE, TARGET_VICTIM, 10);
                            VerifyAndPushSpellCast(spells, SPELL_FROST_NOVA, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_FROSTBOLT, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_COLD_SNAP, TARGET_VICTIM, 5);
                            if (me.GetVictim() && me.GetVictim().HasAuraState(AURA_STATE_FROZEN, nullptr, me))
                                VerifyAndPushSpellCast(spells, SPELL_ICE_LANCE, TARGET_VICTIM, 5);
                            break;
                    }
                    break;
                case CLASS_WARLOCK:
                    VerifyAndPushSpellCast(spells, SPELL_DEATH_COIL_W, TARGET_VICTIM, 2);
                    VerifyAndPushSpellCast(spells, SPELL_FEAR, TARGET_VICTIM, 2);
                    VerifyAndPushSpellCast(spells, SPELL_SEED_OF_CORRUPTION, TARGET_VICTIM, 4);
                    VerifyAndPushSpellCast(spells, SPELL_HOWL_OF_TERROR, TARGET_NONE, 2);
                    if (me.GetVictim() && !me.GetVictim().GetAuraApplicationOfRankedSpell(SPELL_CORRUPTION, me.GetGUID()))
                        VerifyAndPushSpellCast(spells, SPELL_CORRUPTION, TARGET_VICTIM, 10);
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_WARLOCK_AFFLICTION:
                            if (Unit* victim = me.GetVictim())
                            {
                                VerifyAndPushSpellCast(spells, SPELL_SHADOW_BOLT, TARGET_VICTIM, 7);
                                if (!victim.GetAuraApplicationOfRankedSpell(SPELL_UNSTABLE_AFFLICTION, me.GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_UNSTABLE_AFFLICTION, TARGET_VICTIM, 8);
                                if (!victim.GetAuraApplicationOfRankedSpell(SPELL_HAUNT, me.GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_HAUNT, TARGET_VICTIM, 8);
                                if (!victim.GetAuraApplicationOfRankedSpell(SPELL_CURSE_OF_AGONY, me.GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_CURSE_OF_AGONY, TARGET_VICTIM, 4);
                                if (victim.HealthBelowPct(25))
                                    VerifyAndPushSpellCast(spells, SPELL_DRAIN_SOUL, TARGET_VICTIM, 100);
                            }
                            break;
                        case TALENT_SPEC_WARLOCK_DEMONOLOGY:
                            VerifyAndPushSpellCast(spells, SPELL_METAMORPHOSIS, TARGET_NONE, 15);
                            VerifyAndPushSpellCast(spells, SPELL_SHADOW_BOLT, TARGET_VICTIM, 7);
                            if (me.HasAura(AURA_DECIMATION))
                                VerifyAndPushSpellCast(spells, SPELL_SOUL_FIRE, TARGET_VICTIM, 100);
                            if (me.HasAura(SPELL_METAMORPHOSIS))
                            {
                                VerifyAndPushSpellCast(spells, SPELL_IMMOLATION_AURA, TARGET_NONE, 30);
                                if (!me.IsWithinMeleeRange(me.GetVictim()))
                                    VerifyAndPushSpellCast(spells, SPELL_DEMON_CHARGE, TARGET_VICTIM, 20);
                            }
                            if (me.GetVictim() && !me.GetVictim().GetAuraApplicationOfRankedSpell(SPELL_IMMOLATE, me.GetGUID()))
                                VerifyAndPushSpellCast(spells, SPELL_IMMOLATE, TARGET_VICTIM, 5);
                            if (me.HasAura(AURA_MOLTEN_CORE))
                                VerifyAndPushSpellCast(spells, SPELL_INCINERATE, TARGET_VICTIM, 10);
                            break;
                        case TALENT_SPEC_WARLOCK_DESTRUCTION:
                            if (me.GetVictim() && !me.GetVictim().GetAuraApplicationOfRankedSpell(SPELL_IMMOLATE, me.GetGUID()))
                                VerifyAndPushSpellCast(spells, SPELL_IMMOLATE, TARGET_VICTIM, 8);
                            if (me.GetVictim() && me.GetVictim().GetAuraApplicationOfRankedSpell(SPELL_IMMOLATE, me.GetGUID()))
                                VerifyAndPushSpellCast(spells, SPELL_CONFLAGRATE, TARGET_VICTIM, 8);
                            VerifyAndPushSpellCast(spells, SPELL_SHADOWFURY, TARGET_VICTIM, 5);
                            VerifyAndPushSpellCast(spells, SPELL_CHAOS_BOLT, TARGET_VICTIM, 10);
                            VerifyAndPushSpellCast(spells, SPELL_SHADOWBURN, TARGET_VICTIM, 3);
                            VerifyAndPushSpellCast(spells, SPELL_INCINERATE, TARGET_VICTIM, 7);
                            break;
                    }
                    break;
                case CLASS_MONK:
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_MONK_BREWMASTER:
                        case TALENT_SPEC_MONK_BATTLEDANCER:
                        case TALENT_SPEC_MONK_MISTWEAVER:
                            break;
                    }
                    break;
                case CLASS_DRUID:
                    VerifyAndPushSpellCast(spells, SPELL_INNERVATE, TARGET_CHARMER, 5);
                    VerifyAndPushSpellCast(spells, SPELL_BARKSKIN, TARGET_NONE, 5);
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_DRUID_RESTORATION:
                            if (!me.HasAura(SPELL_TREE_OF_LIFE))
                            {
                                CancelAllShapeshifts();
                                VerifyAndPushSpellCast(spells, SPELL_TREE_OF_LIFE, TARGET_NONE, 100);
                                break;
                            }
                            VerifyAndPushSpellCast(spells, SPELL_TRANQUILITY, TARGET_NONE, 10);
                            VerifyAndPushSpellCast(spells, SPELL_NATURE_SWIFTNESS, TARGET_NONE, 7);
                            if (Creature* creatureCharmer = GetCharmer())
                            {
                                VerifyAndPushSpellCast(spells, SPELL_NOURISH, creatureCharmer, 5);
                                VerifyAndPushSpellCast(spells, SPELL_WILD_GROWTH, creatureCharmer, 5);
                                if (creature == nullCharmer.GetAuraApplicationOfRankedSpell(SPELL_REJUVENATION, me.GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_REJUVENATION, creatureCharmer, 8);
                                if (creature == nullCharmer.GetAuraApplicationOfRankedSpell(SPELL_REGROWTH, me.GetGUID()))
                                    VerifyAndPushSpellCast(spells, SPELL_REGROWTH, creatureCharmer, 8);
                                uint8 lifebloomStacks = 0;
                                if (Aura const* lifebloom = creatureCharmer.GetAura(SPELL_LIFEBLOOM, me.GetGUID()))
                                    lifebloomStacks = lifebloom.GetStackAmount();
                                if (lifebloomStacks < 3)
                                    VerifyAndPushSpellCast(spells, SPELL_LIFEBLOOM, creatureCharmer, 5);
                                if (creatureCharmer.GetAuraApplicationOfRankedSpell(SPELL_REJUVENATION) ||
                                    creatureCharmer.GetAuraApplicationOfRankedSpell(SPELL_REGROWTH))
                                    VerifyAndPushSpellCast(spells, SPELL_SWIFTMEND, creatureCharmer, 10);
                                if (me.HasAura(SPELL_NATURE_SWIFTNESS))
                                    VerifyAndPushSpellCast(spells, SPELL_HEALING_TOUCH, creatureCharmer, 100);
                            }
                            break;
                        case TALENT_SPEC_DRUID_BALANCE:
                        {
                            if (!me.HasAura(SPELL_MOONKIN_FORM))
                            {
                                CancelAllShapeshifts();
                                VerifyAndPushSpellCast(spells, SPELL_MOONKIN_FORM, TARGET_NONE, 100);
                                break;
                            }
                            uint32 const mainAttackSpell = me.HasAura(AURA_ECLIPSE_LUNAR) ? SPELL_STARFIRE : SPELL_WRATH;
                            VerifyAndPushSpellCast(spells, SPELL_STARFALL, TARGET_NONE, 20);
                            VerifyAndPushSpellCast(spells, mainAttackSpell, TARGET_VICTIM, 10);
                            if (me.GetVictim() && !me.GetVictim().GetAuraApplicationOfRankedSpell(SPELL_INSECT_SWARM, me.GetGUID()))
                                VerifyAndPushSpellCast(spells, SPELL_INSECT_SWARM, TARGET_VICTIM, 7);
                            if (me.GetVictim() && !me.GetVictim().GetAuraApplicationOfRankedSpell(SPELL_MOONFIRE, me.GetGUID()))
                                VerifyAndPushSpellCast(spells, SPELL_MOONFIRE, TARGET_VICTIM, 5);
                            if (me.GetVictim() && me.GetVictim().HasUnitState(UNIT_STATE_CASTING))
                                VerifyAndPushSpellCast(spells, SPELL_TYPHOON, TARGET_NONE, 15);
                            break;
                        }
                        case TALENT_SPEC_DRUID_CAT:
                        case TALENT_SPEC_DRUID_BEAR:
                            if (!me.HasAura(SPELL_CAT_FORM))
                            {
                                CancelAllShapeshifts();
                                VerifyAndPushSpellCast(spells, SPELL_CAT_FORM, TARGET_NONE, 100);
                                break;
                            }
                            VerifyAndPushSpellCast(spells, SPELL_BERSERK, TARGET_NONE, 20);
                            VerifyAndPushSpellCast(spells, SPELL_SURVIVAL_INSTINCTS, TARGET_NONE, 15);
                            VerifyAndPushSpellCast(spells, SPELL_TIGER_FURY, TARGET_NONE, 15);
                            VerifyAndPushSpellCast(spells, SPELL_DASH, TARGET_NONE, 5);
                            if (Unit* victim = me.GetVictim())
                            {
                                uint8 const cp = me.GetPower(POWER_COMBO_POINTS);
                                if (victim.HasUnitState(UNIT_STATE_CASTING) && cp >= 1)
                                    VerifyAndPushSpellCast(spells, SPELL_MAIM, TARGET_VICTIM, 25);
                                if (!me.IsWithinMeleeRange(victim))
                                    VerifyAndPushSpellCast(spells, SPELL_FERAL_CHARGE_CAT, TARGET_VICTIM, 25);
                                if (cp >= 4)
                                    VerifyAndPushSpellCast(spells, SPELL_RIP, TARGET_VICTIM, 50);
                                if (cp <= 4)
                                {
                                    VerifyAndPushSpellCast(spells, SPELL_MANGLE_CAT, TARGET_VICTIM, 10);
                                    VerifyAndPushSpellCast(spells, SPELL_CLAW, TARGET_VICTIM, 5);
                                    if (!victim.GetAuraApplicationOfRankedSpell(SPELL_RAKE, me.GetGUID()))
                                        VerifyAndPushSpellCast(spells, SPELL_RAKE, TARGET_VICTIM, 8);
                                    if (!me.HasAura(SPELL_SAVAGE_ROAR))
                                        VerifyAndPushSpellCast(spells, SPELL_SAVAGE_ROAR, TARGET_NONE, 15);
                                }
                            }
                            break;
                    }
                    break;
                case CLASS_DEMON_HUNTER:
                    switch (GetSpec())
                    {
                        case TALENT_SPEC_DEMON_HUNTER_HAVOC:
                        case TALENT_SPEC_DEMON_HUNTER_VENGEANCE:
                            break;
                    }
                    break;
            }
            */
            return SelectSpellCast(spells);
        }

        public override void UpdateAI(uint diff)
        {
            Creature charmer = GetCharmer();
            if (charmer == null)
                return;

            //kill self if charm aura has infinite duration
            if (charmer.IsInEvadeMode())
            {
                var auras = me.GetAuraEffectsByType(AuraType.ModCharm);
                foreach (var effect in auras)
                {
                    if (effect.GetCasterGUID() == charmer.GetGUID() && effect.GetBase().IsPermanent())
                    {
                        me.KillSelf();
                        return;
                    }
                }
            }

            if (charmer.IsEngaged())
            {
                Unit target = me.GetVictim();
                if (target == null || !CanAIAttack(target))
                {
                    target = SelectAttackTarget();
                    if (target == null || !CanAIAttack(target))
                    {
                        if (!_isFollowing)
                        {
                            _isFollowing = true;
                            me.AttackStop();
                            me.CastStop();

                            if (me.HasUnitState(UnitState.Chase))
                                me.GetMotionMaster().Remove(MovementGeneratorType.Chase);

                            me.GetMotionMaster().MoveFollow(charmer, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
                        }
                        return;
                    }
                    _isFollowing = false;

                    if (IsRangedAttacker())
                    {
                        _chaseCloser = !me.IsWithinLOSInMap(target);
                        if (_chaseCloser)
                            AttackStart(target);
                        else
                            AttackStartCaster(target, CASTER_CHASE_DISTANCE);
                    }
                    else
                        AttackStart(target);
                    _forceFacing = true;
                }

                if (me.IsStopped() && !me.HasUnitState(UnitState.CannotTurn))
                {
                    float targetAngle = me.GetAbsoluteAngle(target);
                    if (_forceFacing || Math.Abs(me.GetOrientation() - targetAngle) > 0.4f)
                    {
                        me.SetFacingTo(targetAngle);
                        _forceFacing = false;
                    }
                }

                if (_castCheckTimer <= diff)
                {
                    if (me.HasUnitState(UnitState.Casting))
                        _castCheckTimer = 0;
                    else
                    {
                        if (IsRangedAttacker()) // chase to zero if the target isn't in line of sight
                        {
                            bool inLOS = me.IsWithinLOSInMap(target);
                            if (_chaseCloser != !inLOS)
                            {
                                _chaseCloser = !inLOS;
                                if (_chaseCloser)
                                    AttackStart(target);
                                else
                                    AttackStartCaster(target, CASTER_CHASE_DISTANCE);
                            }
                        }
                        var shouldCast = SelectAppropriateCastForSpec();
                        if (shouldCast != default)
                            DoCastAtTarget(shouldCast);
                        _castCheckTimer = 500;
                    }
                }
                else
                    _castCheckTimer -= diff;

                DoAutoAttackIfReady();
            }
            else if (!_isFollowing)
            {
                _isFollowing = true;
                me.AttackStop();
                me.CastStop();

                if (me.HasUnitState(UnitState.Chase))
                    me.GetMotionMaster().Remove(MovementGeneratorType.Chase);

                me.GetMotionMaster().MoveFollow(charmer, SharedConst.PetFollowDist, SharedConst.PetFollowAngle);
            }
        }

        public override void OnCharmed(bool isNew)
        {
            if (me.IsCharmed())
            {
                me.CastStop();
                me.AttackStop();

                if (me.GetMotionMaster().Size() <= 1) // if there is no current movement (we dont want to erase/overwrite any existing stuff)
                    me.GetMotionMaster().MovePoint(0, me.GetPosition(), false); // force re-sync of current position for all clients
            }
            else
            {
                me.CastStop();
                me.AttackStop();

                me.GetMotionMaster().Clear(MovementGeneratorPriority.Normal);
            }

            base.OnCharmed(isNew);
        }
    }

    struct ValidTargetSelectPredicate : ICheck<Unit>
    {
        UnitAI _ai;

        public ValidTargetSelectPredicate(UnitAI ai)
        {
            _ai = ai;
        }

        public bool Invoke(Unit target)
        {
            return _ai.CanAIAttack(target);
        }
    }
}
