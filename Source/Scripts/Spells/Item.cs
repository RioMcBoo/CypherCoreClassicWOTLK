// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.BattleGrounds;
using Game.DataStorage;
using Game.Entities;
using Game.Loots;
using Game.Miscellaneous;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using static Global;

namespace Scripts.Spells.Azerite
{
    // 23074 Arcanite Dragonling
    [Script("spell_item_arcanite_dragonling", SpellArcaniteDragonling)]
    // 23133 Gnomish Battle Chicken
    [Script("spell_item_gnomish_battle_chicken", SpellBattleChicken)]
    // 23076 Mechanical Dragonling
    [Script("spell_item_mechanical_dragonling", SpellMechanicalDragonling)]
    // 23075 Mithril Mechanical Dragonling
    [Script("spell_item_mithril_mechanical_dragonling", SpellMithrilMechanicalDragonling)]
    class spell_item_trigger_spell_SpellScript : SpellScript
    {
        const int SpellArcaniteDragonling = 19804;
        const int SpellBattleChicken = 13166;
        const int SpellMechanicalDragonling = 4073;
        const int SpellMithrilMechanicalDragonling = 12749;

        int _triggeredSpellId;

        public spell_item_trigger_spell_SpellScript(int triggeredSpellId)
        {
            _triggeredSpellId = triggeredSpellId;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(_triggeredSpellId);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            Item item = GetCastItem();
            if (item != null)
                caster.CastSpell(caster, _triggeredSpellId, item);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 23780 - Aegis of Preservation
    class spell_item_aegis_of_preservation : AuraScript
    {
        const int SpellAegisHeal = 23781;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellAegisHeal);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(GetTarget(), SpellAegisHeal, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 1, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 38554 - Absorb Eye of Grillok (31463: Zezzak's Shard)
    class spell_item_absorb_eye_of_grillok : AuraScript
    {
        const int SpellEyeOfGrillok = 38495;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellEyeOfGrillok);
        }

        void PeriodicTick(AuraEffect aurEff)
        {
            PreventDefaultAction();

            if (GetCaster() == null || GetTarget().GetTypeId() != TypeId.Unit)
                return;

            GetCaster().CastSpell(GetCaster(), SpellEyeOfGrillok, aurEff);
            GetTarget().ToCreature().DespawnOrUnsummon();
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(PeriodicTick, 0, AuraType.PeriodicTriggerSpell));
        }
    }

    [Script] // 37877 - Blessing of Faith
    class spell_item_blessing_of_faith : SpellScript
    {
        const int SpellBlessingOfLowerCityDruid = 37878;
        const int SpellBlessingOfLowerCityPaladin = 37879;
        const int SpellBlessingOfLowerCityPriest = 37880;
        const int SpellBlessingOfLowerCityShaman = 37881;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellBlessingOfLowerCityDruid, SpellBlessingOfLowerCityPaladin, SpellBlessingOfLowerCityPriest, SpellBlessingOfLowerCityShaman);
        }

        void HandleDummy(int effIndex)
        {
            Unit unitTarget = GetHitUnit();
            if (unitTarget != null)
            {
                int spellId;
                switch (unitTarget.GetClass())
                {
                    case Class.Druid:
                        spellId = SpellBlessingOfLowerCityDruid;
                        break;
                    case Class.Paladin:
                        spellId = SpellBlessingOfLowerCityPaladin;
                        break;
                    case Class.Priest:
                        spellId = SpellBlessingOfLowerCityPriest;
                        break;
                    case Class.Shaman:
                        spellId = SpellBlessingOfLowerCityShaman;
                        break;
                    default:
                        return; // ignore for non-healing classes
                }

                Unit caster = GetCaster();
                caster.CastSpell(caster, spellId, true);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // Item - 13503: Alchemist's Stone
    // Item - 35748: Guardian's Alchemist Stone
    // Item - 35749: Sorcerer's Alchemist Stone
    // Item - 35750: Redeemer's Alchemist Stone
    // Item - 35751: AssasMath.Sin's Alchemist Stone
    // Item - 44322: Mercurial Alchemist Stone
    // Item - 44323: Indestructible Alchemist's Stone
    // Item - 44324: Mighty Alchemist's Stone

    [Script] // 17619 - Alchemist Stone
    class spell_item_alchemist_stone : AuraScript
    {
        const int SpellAlchemistStoneExtraHeal = 21399;
        const int SpellAlchemistStoneExtraMana = 21400;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellAlchemistStoneExtraHeal, SpellAlchemistStoneExtraMana);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            return eventInfo.GetDamageInfo().GetSpellInfo().SpellFamilyName == SpellFamilyNames.Potion;
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            int spellId = 0;
            int amount = (int)(eventInfo.GetDamageInfo().GetDamage() * 0.4f);

            if (eventInfo.GetDamageInfo().GetSpellInfo().HasEffect(SpellEffectName.Heal))
                spellId = SpellAlchemistStoneExtraHeal;
            else if (eventInfo.GetDamageInfo().GetSpellInfo().HasEffect(SpellEffectName.Energize))
                spellId = SpellAlchemistStoneExtraMana;

            if (spellId == 0)
                return;

            Unit caster = eventInfo.GetActionTarget();
            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, amount);
            caster.CastSpell(null, spellId, args);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    // Item - 50351: Tiny Abomination in a Jar
    // 71406 - Anger Capacitor

    // Item - 50706: Tiny Abomination in a Jar (Heroic)
    // 71545 - Anger Capacitor
    [Script("spell_item_tiny_abomination_in_a_jar", 8)]
    [Script("spell_item_tiny_abomination_in_a_jar_hero", 7)]
    class spell_item_anger_capacitor_AuraScript : AuraScript
    {
        const int SpellMoteOfAnger = 71432;
        const int SpellManifestAngerMainHand = 71433;
        const int SpellManifestAngerOffHand = 71434;

        int _stacks;

        public spell_item_anger_capacitor_AuraScript(int stacks)
        {
            _stacks = stacks;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellMoteOfAnger, SpellManifestAngerMainHand, SpellManifestAngerOffHand);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetProcTarget();

            caster.CastSpell(null, SpellMoteOfAnger, true);
            Aura motes = caster.GetAura(SpellMoteOfAnger);
            if (motes == null || motes.GetStackAmount() < _stacks)
                return;

            caster.RemoveAurasDueToSpell(SpellMoteOfAnger);
            int spellId = SpellManifestAngerMainHand;
            Player player = caster.ToPlayer();
            if (player != null)
                if (player.GetWeaponForAttack(WeaponAttackType.OffAttack, true) != null && RandomHelper.randChance(50))
                    spellId = SpellManifestAngerOffHand;

            caster.CastSpell(target, spellId, aurEff);
        }

        void OnRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(SpellMoteOfAnger);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
            AfterEffectRemove.Add(new(OnRemove, 0, AuraType.Dummy, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 26400 - Arcane Shroud
    class spell_item_arcane_shroud : AuraScript
    {
        void CalculateAmount(AuraEffect aurEff, ref int amount, ref bool canBeRecalculated)
        {
            int diff = GetUnitOwner().GetLevel() - 60;
            if (diff > 0)
                amount += 2 * diff;
        }

        public override void Register()
        {
            DoEffectCalcAmount.Add(new(CalculateAmount, 0, AuraType.ModThreat));
        }
    }

    // Item - 31859: Darkmoon Card: Madness
    [Script] // 39446 - Aura of Madness
    class spell_item_aura_of_madness : AuraScript
    {
        const int SpellSociopath = 39511; // Sociopath: +35 strength(Paladin, Rogue, Druid, Warrior)
        const int SpellDelusional = 40997; // Delusional: +70 attack power(Rogue, Hunter, Paladin, Warrior, Druid)
        const int SpellKleptomania = 40998; // Kleptomania: +35 agility(Warrior, Rogue, Paladin, Hunter, Druid)
        const int SpellMegalomania = 40999; // Megalomania: +41 damage / healing(Druid, Shaman, Priest, Warlock, Mage, Paladin)
        const int SpellParanoia = 41002; // Paranoia: +35 spell / melee / ranged crit strike rating(All classes)
        const int SpellManic = 41005; // Manic: +35 haste(spell, melee and ranged) (All classes)
        const int SpellNarcissism = 41009; // Narcissism: +35 intellect(Druid, Shaman, Priest, Warlock, Mage, Paladin, Hunter)
        const int SpellMartyrComplex = 41011; // Martyr Complex: +35 stamina(All classes)
        const int SpellDementia = 41404; // Dementia: Every 5 seconds either gives you +5/-5%  damage/healing. (Druid, Shaman, Priest, Warlock, Mage, Paladin)

        const int SayMadness = 21954;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellSociopath, SpellDelusional, SpellKleptomania, SpellMegalomania, SpellParanoia, SpellManic, SpellNarcissism, SpellMartyrComplex, SpellDementia) && CliDB.BroadcastTextStorage.ContainsKey(SayMadness);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            int[][] triggeredSpells =
            {
                //ClassNone
                Array.Empty<int>(),
                //ClassWarrior
                new int[]  { SpellSociopath, SpellDelusional, SpellKleptomania, SpellParanoia, SpellManic, SpellMartyrComplex },
                //ClassPaladin
                new int[]  { SpellSociopath, SpellDelusional, SpellKleptomania, SpellMegalomania, SpellParanoia, SpellManic, SpellNarcissism, SpellMartyrComplex, SpellDementia },
                //ClassHunter
                new int[]  { SpellDelusional, SpellMegalomania, SpellParanoia, SpellManic, SpellNarcissism, SpellMartyrComplex, SpellDementia },
                //ClassRogue
                new int[]  { SpellSociopath, SpellDelusional, SpellKleptomania, SpellParanoia, SpellManic, SpellMartyrComplex },
                //ClassPriest
                new int[]  { SpellMegalomania, SpellParanoia, SpellManic, SpellNarcissism, SpellMartyrComplex, SpellDementia },
                //ClassDeathKnight
                new int[]  { SpellSociopath, SpellDelusional, SpellKleptomania, SpellParanoia, SpellManic, SpellMartyrComplex },
                //ClassShaman
                new int[]  { SpellMegalomania, SpellParanoia, SpellManic, SpellNarcissism, SpellMartyrComplex, SpellDementia },
                //ClassMage
                new int[]  { SpellMegalomania, SpellParanoia, SpellManic, SpellNarcissism, SpellMartyrComplex, SpellDementia },
                //ClassWarlock
                new int[]  { SpellMegalomania, SpellParanoia, SpellManic, SpellNarcissism, SpellMartyrComplex, SpellDementia },
                //ClassUnk
                Array.Empty<int>(),
                //ClassDruid
                new int[]  { SpellSociopath, SpellDelusional, SpellKleptomania, SpellMegalomania, SpellParanoia, SpellManic, SpellNarcissism, SpellMartyrComplex, SpellDementia }
            };


            PreventDefaultAction();
            Unit caster = eventInfo.GetActor();
            int spellId = triggeredSpells[(int)caster.GetClass()].SelectRandom();
            caster.CastSpell(caster, spellId, aurEff);

            if (RandomHelper.randChance(10))
                caster.Say(SayMadness);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 41404 - Dementia
    class spell_item_dementia : AuraScript
    {
        const int SpellDementiaPos = 41406;
        const int SpellDementiaNeg = 41409;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellDementiaPos, SpellDementiaNeg);
        }

        void HandlePeriodicDummy(AuraEffect aurEff)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(GetTarget(), RandomHelper.RAND(SpellDementiaPos, SpellDementiaNeg), aurEff);
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(HandlePeriodicDummy, 0, AuraType.PeriodicDummy));
        }
    }

    [Script] // 24590 - Brittle Armor
    class spell_item_brittle_armor : SpellScript
    {
        const int SpellBrittleArmor = 24575;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellBrittleArmor);
        }

        void HandleScript(int effIndex)
        {
            GetHitUnit().RemoveAuraFromStack(SpellBrittleArmor);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 64411 - Blessing of Ancient Kings (Val'anyr, Hammer of Ancient Kings)
    class spell_item_blessing_of_ancient_kings : AuraScript
    {
        const int SpellProtectionOfAncientKings = 64413;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellProtectionOfAncientKings);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            return eventInfo.GetProcTarget() != null;
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            HealInfo healInfo = eventInfo.GetHealInfo();
            if (healInfo == null || healInfo.GetHeal() == 0)
                return;

            int absorb = MathFunctions.CalculatePct(healInfo.GetHeal(), 15.0f);
            AuraEffect protEff = eventInfo.GetProcTarget().GetAuraEffect(SpellProtectionOfAncientKings, 0, eventInfo.GetActor().GetGUID());
            if (protEff != null)
            {
                // The shield can grow to a maximum size of 20,000 damage absorbtion
                protEff.SetAmount(Math.Min(protEff.GetAmount() + absorb, 20000));

                // Refresh and return to prevent replacing the aura
                protEff.GetBase().RefreshDuration();
            }
            else
            {
                CastSpellExtraArgs args = new(aurEff);
                args.AddSpellMod(SpellValueMod.BasePoint0, absorb);
                GetTarget().CastSpell(eventInfo.GetProcTarget(), SpellProtectionOfAncientKings, args);
            }
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 64415 Val'anyr Hammer of Ancient Kings - Equip Effect
    class spell_item_valanyr_hammer_of_ancient_kings : AuraScript
    {
        bool CheckProc(ProcEventInfo eventInfo)
        {
            return eventInfo.GetHealInfo() != null && eventInfo.GetHealInfo().GetEffectiveHeal() > 0;
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
        }
    }

    [Script] // 71564 - Deadly Precision
    class spell_item_deadly_precision : AuraScript
    {
        void HandleStackDrop(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().RemoveAuraFromStack(GetId(), GetTarget().GetGUID());
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleStackDrop, 0, AuraType.ModRating));
        }
    }

    [Script] // 71563 - Deadly Precision Dummy
    class spell_item_deadly_precision_dummy : SpellScript
    {
        const int SpellDeadlyPrecision = 71564;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellDeadlyPrecision);
        }

        void HandleDummy(int effIndex)
        {
            SpellInfo spellInfo = SpellMgr.GetSpellInfo(SpellDeadlyPrecision, GetCastDifficulty());
            CastSpellExtraArgs args = new(TriggerCastFlags.FullMask);
            args.AddSpellMod(SpellValueMod.AuraStack, spellInfo.StackAmount);
            GetCaster().CastSpell(GetCaster(), spellInfo.Id, args);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.ApplyAura));
        }
    }

    // Item - 50362: Deathbringer's Will
    // 71519 - Item - Icecrown 25 Normal Melee Trinket

    // Item - 50363: Deathbringer's Will
    // 71562 - Item - Icecrown 25 Heroic Melee Trinket
    [Script("spell_item_deathbringers_will_normal", SpellStrengthOfTheTaunka, SpellAgilityOfTheVrykul, SpellPowerOfTheTaunka, SpellAimOfTheIronDwarves, SpellSpeedOfTheVrykul)]
    [Script("spell_item_deathbringers_will_heroic", SpellStrengthOfTheTaunkaHero, SpellAgilityOfTheVrykulHero, SpellPowerOfTheTaunkaHero, SpellAimOfTheIronDwarvesHero, SpellSpeedOfTheVrykulHero)]
    class spell_item_deathbringers_will_AuraScript : AuraScript
    {
        const int SpellStrengthOfTheTaunka = 71484; // +600 Strength
        const int SpellAgilityOfTheVrykul = 71485; // +600 Agility
        const int SpellPowerOfTheTaunka = 71486; // +1200 Attack Power
        const int SpellAimOfTheIronDwarves = 71491; // +600 Critical
        const int SpellSpeedOfTheVrykul = 71492; // +600 Haste

        const int SpellAgilityOfTheVrykulHero = 71556; // +700 Agility
        const int SpellPowerOfTheTaunkaHero = 71558; // +1400 Attack Power
        const int SpellAimOfTheIronDwarvesHero = 71559; // +700 Critical
        const int SpellSpeedOfTheVrykulHero = 71560; // +700 Haste
        const int SpellStrengthOfTheTaunkaHero = 71561;  // +700 Strength

        int _strength;
        int _agility;
        int _attackPower;
        int _critical;
        int _haste;

        public spell_item_deathbringers_will_AuraScript(int strength, int agility, int attackPower, int critical, int haste)
        {
            _strength = strength;
            _agility = agility;
            _attackPower = attackPower;
            _critical = critical;
            _haste = haste;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(_strength, _agility, _attackPower, _critical, _haste);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            int[][] triggeredSpells =
            {
                //ClassNone
                Array.Empty<int>(),
                //ClassWarrior
                new int[]  { _strength, _critical, _haste },
                //ClassPaladin
                new int[] { _strength, _critical, _haste },
                //ClassHunter
                new int[] { _agility, _critical, _attackPower },
                //ClassRogue
                new int[]  { _agility, _haste, _attackPower },
                //ClassPriest
                Array.Empty<int>(),
                //ClassDeathKnight
                new int[]  { _strength, _critical, _haste },
                //ClassShaman
                new int[]  { _agility, _haste, _attackPower },
                //ClassMage
                Array.Empty<int>(),
                //ClassWarlock
                Array.Empty<int>(),
                //ClassUnk
                Array.Empty<int>(),
                //ClassDruid
                new int[]  { _strength, _agility, _haste }
             };


            PreventDefaultAction();
            Unit caster = eventInfo.GetActor();
            var randomSpells = triggeredSpells[(int)caster.GetClass()];
            if (randomSpells.Empty())
                return;

            int spellId = randomSpells.SelectRandom();
            caster.CastSpell(caster, spellId, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 47770 - Roll Dice
    class spell_item_decahedral_dwarven_dice : SpellScript
    {
        const int TextDecahedralDwarvenDice = 26147;

        public override bool Validate(SpellInfo spellInfo)
        {
            if (!CliDB.BroadcastTextStorage.ContainsKey(TextDecahedralDwarvenDice))
                return false;
            return true;
        }

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleScript(int effIndex)
        {
            GetCaster().TextEmote(TextDecahedralDwarvenDice, GetHitUnit());

            int minimum = 1;
            int maximum = 100;

            GetCaster().ToPlayer().DoRandomRoll(minimum, maximum);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 23134 - Goblin Bomb
    class spell_item_goblin_bomb_dispenser : SpellScript
    {
        const int SpellSummonGoblinBomb = 13258;
        const int SpellMalfunctionExplosion = 13261;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellSummonGoblinBomb, SpellMalfunctionExplosion);
        }

        void HandleDummy(int effIndex)
        {
            Item item = GetCastItem();
            if (item != null)
                GetCaster().CastSpell(GetCaster(), RandomHelper.randChance(95) ? SpellSummonGoblinBomb : SpellMalfunctionExplosion, item);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 46203 - Goblin Weather Machine
    class spell_item_goblin_weather_machine : SpellScript
    {
        const int SpellPersonalizedWeather1 = 46740;
        const int SpellPersonalizedWeather2 = 46739;
        const int SpellPersonalizedWeather3 = 46738;
        const int SpellPersonalizedWeather4 = 46736;

        void HandleScript(int effIndex)
        {
            Unit target = GetHitUnit();

            int spellId = RandomHelper.RAND(SpellPersonalizedWeather1, SpellPersonalizedWeather2, SpellPersonalizedWeather3, SpellPersonalizedWeather4);
            target.CastSpell(target, spellId, GetSpell());
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    // 8342  - Defibrillate (Goblin Jumper Cables) have 33% chance on success
    // 22999 - Defibrillate (Goblin Jumper Cables Xl) have 50% chance on success
    // 54732 - Defibrillate (Gnomish Army Knife) have 67% chance on success
    [Script("spell_item_goblin_jumper_cables", 67, SpellGoblinJumperCablesFail)]
    [Script("spell_item_goblin_jumper_cables_xl", 50, SpellGoblinJumperCablesXlFail)]
    [Script("spell_item_gnomish_army_knife", 33, 0u)]
    class spell_item_defibrillate_SpellScript : SpellScript
    {
        const int SpellGoblinJumperCablesFail = 8338;
        const int SpellGoblinJumperCablesXlFail = 23055;

        byte _chance;
        int _failSpell;

        public spell_item_defibrillate_SpellScript(byte chance, int failSpell)
        {
            _chance = chance;
            _failSpell = failSpell;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return _failSpell == 0 || ValidateSpellInfo(_failSpell);
        }

        void HandleScript(int effIndex)
        {
            if (RandomHelper.randChance(_chance))
            {
                PreventHitDefaultEffect(effIndex);
                if (_failSpell != 0)
                    GetCaster().CastSpell(GetCaster(), _failSpell, GetCastItem());
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.Resurrect));
        }
    }

    [Script] // 33896 - Desperate Defense
    class spell_item_desperate_defense : AuraScript
    {
        const int SpellDesperateRage = 33898;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellDesperateRage);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(GetTarget(), SpellDesperateRage, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 2, AuraType.ProcTriggerSpell));
        }
    }

    // http://www.wowhead.com/item=6522 Deviate Fish
    [Script] // 8063 Deviate Fish
    class spell_item_deviate_fish : SpellScript
    {
        const int SpellSleepy = 8064;
        const int SpellInvigorate = 8065;
        const int SpellShrink = 8066;
        const int SpellPartyTime = 8067;
        const int SpellHealthySpirit = 8068;
        const int SpellRejuvenation = 8070;

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellSleepy, SpellInvigorate, SpellShrink, SpellPartyTime, SpellHealthySpirit, SpellRejuvenation);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            int spellId = RandomHelper.RAND(SpellSleepy, SpellInvigorate, SpellShrink, SpellPartyTime, SpellHealthySpirit, SpellRejuvenation);
            caster.CastSpell(caster, spellId, true);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    class PartyTimeEmoteEvent : BasicEvent
    {
        const int SpellPartyTime = 8067;

        Player _player;

        public PartyTimeEmoteEvent(Player player)
        {
            _player = player;
        }

        public override bool Execute(long time, uint diff)
        {
            if (!_player.HasAura(SpellPartyTime))
                return true;

            if (_player.IsMoving())
                _player.HandleEmoteCommand(RandomHelper.RAND(Emote.OneshotApplaud, Emote.OneshotLaugh, Emote.OneshotCheer, Emote.OneshotChicken));
            else
                _player.HandleEmoteCommand(RandomHelper.RAND(Emote.OneshotApplaud, Emote.OneshotDancespecial, Emote.OneshotLaugh, Emote.OneshotCheer, Emote.OneshotChicken));

            _player.m_Events.AddEventAtOffset(this, RandomHelper.RAND(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15)));

            return false; // do not delete re-added event in EventProcessor.Update
        }
    }

    [Script]
    class spell_item_party_time : AuraScript
    {
        void HandleEffectApply(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Player player = GetOwner().ToPlayer();
            if (player == null)
                return;

            player.m_Events.AddEventAtOffset(new PartyTimeEmoteEvent(player), RandomHelper.RAND(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15)));
        }

        public override void Register()
        {
            OnEffectApply.Add(new(HandleEffectApply, 0, AuraType.Dummy, AuraEffectHandleModes.Real));
        }
    }

    struct DireBrewModels
    {
        public const int ClothMale = 25229;
        public const int ClothFemale = 25233;
        public const int LeatherMale = 25230;
        public const int LeatherFemale = 25234;
        public const int MailMale = 25231;
        public const int MailFemale = 25235;
        public const int PlateMale = 25232;
        public const int PlateFemale = 25236;
    }

    [Script] // 51010 - Dire Brew
    class spell_item_dire_brew : AuraScript
    {
        void AfterApply(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Unit target = GetTarget();

            int model = 0;
            Gender gender = target.GetGender();
            ChrClassesRecord chrClass = CliDB.ChrClassesStorage.LookupByKey((int)target.GetClass());
            if ((chrClass.ArmorTypeMask & (1 << (int)ItemSubClassArmor.Plate)) != 0)
                model = gender == Gender.Male ? DireBrewModels.PlateMale : DireBrewModels.PlateFemale;
            else if ((chrClass.ArmorTypeMask & (1 << (int)ItemSubClassArmor.Mail)) != 0)
                model = gender == Gender.Male ? DireBrewModels.MailMale : DireBrewModels.MailFemale;
            else if ((chrClass.ArmorTypeMask & (1 << (int)ItemSubClassArmor.Leather)) != 0)
                model = gender == Gender.Male ? DireBrewModels.LeatherMale : DireBrewModels.LeatherFemale;
            else if ((chrClass.ArmorTypeMask & (1 << (int)ItemSubClassArmor.Cloth)) != 0)
                model = gender == Gender.Male ? DireBrewModels.ClothMale : DireBrewModels.ClothFemale;

            if (model != 0)
                target.SetDisplayId(model);
        }

        public override void Register()
        {
            AfterEffectApply.Add(new(AfterApply, 0, AuraType.Transform, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 59915 - Discerning Eye of the Beast Dummy
    class spell_item_discerning_eye_beast_dummy : AuraScript
    {
        const int SpellDiscerningEyeBeast = 59914;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellDiscerningEyeBeast);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            eventInfo.GetActor().CastSpell(null, SpellDiscerningEyeBeast, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 71610, 71641 - Echoes of Light (Althor's Abacus)
    class spell_item_echoes_of_light : SpellScript
    {
        void FilterTargets(List<WorldObject> targets)
        {
            if (targets.Count < 2)
                return;

            targets.Sort(new HealthPctOrderPred());

            WorldObject target = targets.FirstOrDefault();
            targets.Clear();
            targets.Add(target);
        }

        public override void Register()
        {
            OnObjectAreaTargetSelect.Add(new(FilterTargets, 0, Targets.UnitDestAreaAlly));
        }
    }

    [Script] // 30427 - Extract Gas (23821: Zapthrottle Mote Extractor)
    class spell_item_extract_gas : AuraScript
    {
        void PeriodicTick(AuraEffect aurEff)
        {
            PreventDefaultAction();

            // move loot to player inventory and despawn target
            if (GetCaster() != null && GetCaster().IsPlayer() &&
                GetTarget().GetTypeId() == TypeId.Unit &&
                GetTarget().ToCreature().GetCreatureTemplate().CreatureType == CreatureType.GasCloud)
            {
                Player player = GetCaster().ToPlayer();
                Creature creature = GetTarget().ToCreature();
                CreatureDifficulty creatureDifficulty = creature.GetCreatureDifficulty();
                // missing lootid has been reported on startup - just return
                if (creatureDifficulty.SkinLootID == 0)
                    return;

                player.AutoStoreLoot(creatureDifficulty.SkinLootID, LootStorage.Skinning, ItemContext.None, true);
                creature.DespawnOrUnsummon();
            }
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(PeriodicTick, 0, AuraType.PeriodicTriggerSpell));
        }
    }

    [Script] // 7434 - Fate Rune of Unsurpassed Vigor
    class spell_item_fate_rune_of_unsurpassed_vigor : AuraScript
    {
        const int SpellUnsurpassedVigor = 25733;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellUnsurpassedVigor);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(GetTarget(), SpellUnsurpassedVigor, true);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    //57426 - Fish Feast
    //58465 - Gigantic Feast
    //58474 - Small Feast
    //66476 - Bountiful Feast
    [Script("spell_item_great_feast", TextGreatFeast)]
    [Script("spell_item_fish_feast", TextFishFeast)]
    [Script("spell_item_gigantic_feast", TextGiganticFeast)]
    [Script("spell_item_small_feast", TextSmallFeast)]
    [Script("spell_item_bountiful_feast", TextBountifulFeast)]
    class spell_item_feast : SpellScript
    {
        const int TextGreatFeast = 31843;
        const int TextFishFeast = 31844;
        const int TextGiganticFeast = 31846;
        const int TextSmallFeast = 31845;
        const int TextBountifulFeast = 35153;

        int _text;

        public spell_item_feast(int text)
        {
            _text = text;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return CliDB.BroadcastTextStorage.ContainsKey(_text);
        }

        void HandleScript(int effIndex)
        {
            Unit caster = GetCaster();
            caster.TextEmote(_text, caster, false);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    // http://www.wowhead.com/item=47499 Flask of the North
    [Script] // 67019 Flask of the North
    class spell_item_flask_of_the_north : SpellScript
    {
        const int SpellFlaskOfTheNorthSp = 67016;
        const int SpellFlaskOfTheNorthAp = 67017;
        const int SpellFlaskOfTheNorthStr = 67018;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellFlaskOfTheNorthSp, SpellFlaskOfTheNorthAp, SpellFlaskOfTheNorthStr);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            List<int> possibleSpells = new();
            switch (caster.GetClass())
            {
                case Class.Warlock:
                case Class.Mage:
                case Class.Priest:
                    possibleSpells.Add(SpellFlaskOfTheNorthSp);
                    break;
                case Class.Deathknight:
                case Class.Warrior:
                    possibleSpells.Add(SpellFlaskOfTheNorthStr);
                    break;
                case Class.Rogue:
                case Class.Hunter:
                    possibleSpells.Add(SpellFlaskOfTheNorthAp);
                    break;
                case Class.Druid:
                case Class.Paladin:
                    possibleSpells.Add(SpellFlaskOfTheNorthSp);
                    possibleSpells.Add(SpellFlaskOfTheNorthStr);
                    break;
                case Class.Shaman:
                    possibleSpells.Add(SpellFlaskOfTheNorthSp);
                    possibleSpells.Add(SpellFlaskOfTheNorthAp);
                    break;
            }

            if (possibleSpells.Empty())
            {
                Log.outWarn(LogFilter.Spells, $"Missing spells for class {caster.GetClass()} in script spell_item_flask_of_the_north");
                return;
            }

            caster.CastSpell(caster, possibleSpells.SelectRandom(), true);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // 39372 - Frozen Shadoweave
    [Script] // Frozen Shadoweave set 3p bonus
    class spell_item_frozen_shadoweave : AuraScript
    {
        const int SpellShadowmend = 39373;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellShadowmend);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            DamageInfo damageInfo = eventInfo.GetDamageInfo();
            if (damageInfo == null || damageInfo.GetDamage() == 0)
                return;

            Unit caster = eventInfo.GetActor();
            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, MathFunctions.CalculatePct(damageInfo.GetDamage(), aurEff.GetAmount()));
            caster.CastSpell(null, SpellShadowmend, args);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    // http://www.wowhead.com/item=10645 Gnomish Death Ray
    [Script] // 13280 Gnomish Death Ray
    class spell_item_gnomish_death_ray : SpellScript
    {
        const int SpellGnomishDeathRaySelf = 13493;
        const int SpellGnomishDeathRayTarget = 13279;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellGnomishDeathRaySelf, SpellGnomishDeathRayTarget);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            Unit target = GetHitUnit();
            if (target != null)
            {
                if (RandomHelper.URand(0, 99) < 15)
                    caster.CastSpell(caster, SpellGnomishDeathRaySelf, true);    // failure
                else
                    caster.CastSpell(target, SpellGnomishDeathRayTarget, true);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // Item 10721: Gnomish Harm Prevention Belt
    [Script] // 13234 - Harm Prevention Belt
    class spell_item_harm_prevention_belt : AuraScript
    {
        const int SpellForcefieldCollapse = 13235;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellForcefieldCollapse);
        }

        void HandleProc(ProcEventInfo eventInfo)
        {
            GetTarget().CastSpell(null, SpellForcefieldCollapse, true);
        }

        public override void Register()
        {
            OnProc.Add(new(HandleProc));
        }
    }

    // Item - 49982: Heartpierce
    // 71880 - Item - Icecrown 25 Normal Dagger Proc

    // Item - 50641: Heartpierce (Heroic)
    // 71892 - Item - Icecrown 25 Heroic Dagger Proc
    [Script("spell_item_heartpierce", SpellInvigorationEnergy, SpellInvigorationMana, SpellInvigorationRage, SpellInvigorationRp)]
    [Script("spell_item_heartpierce_hero", SpellInvigorationEnergyHero, SpellInvigorationManaHero, SpellInvigorationRageHero, SpellInvigorationRpHero)]
    class spell_item_heartpierce : AuraScript
    {
        const int SpellInvigorationMana = 71881;
        const int SpellInvigorationEnergy = 71882;
        const int SpellInvigorationRage = 71883;
        const int SpellInvigorationRp = 71884;

        const int SpellInvigorationRpHero = 71885;
        const int SpellInvigorationRageHero = 71886;
        const int SpellInvigorationEnergyHero = 71887;
        const int SpellInvigorationManaHero = 71888;

        int _energy;
        int _mana;
        int _rage;
        int _runicPower;

        public spell_item_heartpierce(int energy, int mana, int rage, int runicPower)
        {
            _energy = energy;
            _mana = mana;
            _rage = rage;
            _runicPower = runicPower;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(_energy, _mana, _rage, _runicPower);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            Unit caster = eventInfo.GetActor();

            int spellId;
            switch (caster.GetPowerType())
            {
                case PowerType.Mana:
                    spellId = _mana;
                    break;
                case PowerType.Energy:
                    spellId = _energy;
                    break;
                case PowerType.Rage:
                    spellId = _rage;
                    break;
                // Death Knights can't use daggers, but oh well
                case PowerType.RunicPower:
                    spellId = _runicPower;
                    break;
                default:
                    return;
            }

            caster.CastSpell(null, spellId, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 23645 - Hourglass Sand
    class spell_item_hourglass_sand : SpellScript
    {
        const int SpellBroodAfflictionBronze = 23170;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellBroodAfflictionBronze);
        }

        void HandleDummy(int effIndex)
        {
            GetCaster().RemoveAurasDueToSpell(SpellBroodAfflictionBronze);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 40971 - Bonus Healing (Crystal Spire of Karabor)
    class spell_item_crystal_spire_of_karabor : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect((spellInfo.Id, 0));
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            int pct = GetSpellInfo().GetEffect(0).CalcValue();
            HealInfo healInfo = eventInfo.GetHealInfo();
            if (healInfo != null)
            {
                Unit healTarget = healInfo.GetTarget();
                if (healTarget != null)
                    if (healTarget.GetHealth() - healInfo.GetEffectiveHeal() <= healTarget.CountPctFromMaxHealth(pct))
                        return true;
            }

            return false;
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
        }
    }

    // http://www.wowhead.com/item=27388 Mr. Pinchy
    [Script] // 33060 Make a Wish
    class spell_item_make_a_wish : SpellScript
    {
        const int SpellMrPinchysBlessing = 33053;
        const int SpellSummonMightyMrPinchy = 33057;
        const int SpellSummonFuriousMrPinchy = 33059;
        const int SpellTinyMagicalCrawdad = 33062;
        const int SpellMrPinchysGift = 33064;

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellMrPinchysBlessing, SpellSummonMightyMrPinchy, SpellSummonFuriousMrPinchy, SpellTinyMagicalCrawdad, SpellMrPinchysGift);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            int spellId = SpellMrPinchysGift;
            switch (RandomHelper.URand(1, 5))
            {
                case 1: spellId = SpellMrPinchysBlessing; break;
                case 2: spellId = SpellSummonMightyMrPinchy; break;
                case 3: spellId = SpellSummonFuriousMrPinchy; break;
                case 4: spellId = SpellTinyMagicalCrawdad; break;
            }
            caster.CastSpell(caster, spellId, true);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // Item - 27920: Mark of Conquest
    // Item - 27921: Mark of Conquest
    [Script] // 33510 - Health Restore
    class spell_item_mark_of_conquest : AuraScript
    {
        const int SpellMarkOfConquestEnergize = 39599;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellMarkOfConquestEnergize);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            if (eventInfo.GetTypeMask().HasFlag(ProcFlags.DealRangedAttack | ProcFlags.DealRangedAbility))
            {
                // in that case, do not cast heal spell
                PreventDefaultAction();
                // but mana instead
                eventInfo.GetActor().CastSpell(null, SpellMarkOfConquestEnergize, aurEff);
            }
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 26465 - Mercurial Shield
    class spell_item_mercurial_shield : SpellScript
    {
        const int SpellMercurialShield = 26464;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellMercurialShield);
        }

        void HandleScript(int effIndex)
        {
            GetHitUnit().RemoveAuraFromStack(SpellMercurialShield);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    // http://www.wowhead.com/item=32686 Mingo's Fortune Giblets
    [Script] // 40802 Mingo's Fortune Generator
    class spell_item_mingos_fortune_generator : SpellScript
    {
        int[] CreateFortuneSpells =
        {
            40804, 40805, 40806, 40807, 40808,
            40809, 40908, 40910, 40911, 40912,
            40913, 40914, 40915, 40916, 40918,
            40919, 40920, 40921, 40922, 40923
        };

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(CreateFortuneSpells);
        }

        void HandleDummy(int effIndex)
        {
            GetCaster().CastSpell(GetCaster(), CreateFortuneSpells.SelectRandom(), true);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 71875, 71877 - Item - Black Bruise: Necrotic Touch Proc
    class spell_item_necrotic_touch : AuraScript
    {
        const int SpellItemNecroticTouchProc = 71879;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellItemNecroticTouchProc);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            return eventInfo.GetProcTarget() != null && eventInfo.GetProcTarget().IsAlive();
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            DamageInfo damageInfo = eventInfo.GetDamageInfo();
            if (damageInfo == null || damageInfo.GetDamage() == 0)
                return;

            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, MathFunctions.CalculatePct(damageInfo.GetDamage(), aurEff.GetAmount()));
            GetTarget().CastSpell(null, SpellItemNecroticTouchProc, args);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    // http://www.wowhead.com/item=10720 Gnomish Net-o-Matic Projector
    [Script] // 13120 Net-o-Matic
    class spell_item_net_o_matic : SpellScript
    {
        const int SpellNetOMaticTriggered1 = 16566;
        const int SpellNetOMaticTriggered2 = 13119;
        const int SpellNetOMaticTriggered3 = 13099;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellNetOMaticTriggered1, SpellNetOMaticTriggered2, SpellNetOMaticTriggered3);
        }

        void HandleDummy(int effIndex)
        {
            Unit target = GetHitUnit();
            if (target != null)
            {
                int spellId = SpellNetOMaticTriggered3;
                int roll = RandomHelper.IRand(0, 99);
                if (roll < 2)                            // 2% for 30 sec self root (off-like chance unknown)
                    spellId = SpellNetOMaticTriggered1;
                else if (roll < 4)                       // 2% for 20 sec root, charge to target (off-like chance unknown)
                    spellId = SpellNetOMaticTriggered2;

                GetCaster().CastSpell(target, spellId, true);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // http://www.wowhead.com/item=8529 Noggenfogger Elixir
    [Script] // 16589 Noggenfogger Elixir
    class spell_item_noggenfogger_elixir : SpellScript
    {
        const int SpellNoggenfoggerElixirTriggered1 = 16595;
        const int SpellNoggenfoggerElixirTriggered2 = 16593;
        const int SpellNoggenfoggerElixirTriggered3 = 16591;

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellNoggenfoggerElixirTriggered1, SpellNoggenfoggerElixirTriggered2, SpellNoggenfoggerElixirTriggered3);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            int spellId = SpellNoggenfoggerElixirTriggered3;
            switch (RandomHelper.URand(1, 3))
            {
                case 1: spellId = SpellNoggenfoggerElixirTriggered1; break;
                case 2: spellId = SpellNoggenfoggerElixirTriggered2; break;
            }

            caster.CastSpell(caster, spellId, true);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 29601 - Enlightenment (Pendant of the Violet Eye)
    class spell_item_pendant_of_the_violet_eye : AuraScript
    {
        bool CheckProc(ProcEventInfo eventInfo)
        {
            Spell spell = eventInfo.GetProcSpell();
            if (spell != null)
            {
                var cost = spell.GetPowerCost();
                var m = cost.Find(cost => cost.Power == PowerType.Mana && cost.Amount > 0);
                if (m != null)
                    return true;
            }

            return false;
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
        }
    }

    [Script] // 26467 - Persistent Shield
    class spell_item_persistent_shield : AuraScript
    {
        const int SpellPersistentShieldTriggered = 26470;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellPersistentShieldTriggered);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            return eventInfo.GetHealInfo() != null && eventInfo.GetHealInfo().GetHeal() != 0;
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetProcTarget();
            int bp0 = MathFunctions.CalculatePct(eventInfo.GetHealInfo().GetHeal(), 15);

            // Scarab Brooch does not replace stronger shields
            AuraEffect shield = target.GetAuraEffect(SpellPersistentShieldTriggered, 0, caster.GetGUID());
            if (shield != null && shield.GetAmount() > bp0)
                return;

            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, bp0);
            caster.CastSpell(target, SpellPersistentShieldTriggered, args);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    // 37381 - Pet Healing
    // Hunter T5 2P Bonus
    [Script] // Warlock T5 2P Bonus
    class spell_item_pet_healing : AuraScript
    {
        const int SpellHealthLink = 37382;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellHealthLink);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            DamageInfo damageInfo = eventInfo.GetDamageInfo();
            if (damageInfo == null || damageInfo.GetDamage() == 0)
                return;

            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, MathFunctions.CalculatePct(damageInfo.GetDamage(), aurEff.GetAmount()));
            eventInfo.GetActor().CastSpell(null, SpellHealthLink, args);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 17512 - Piccolo of the Flaming Fire
    class spell_item_piccolo_of_the_flaming_fire : SpellScript
    {
        void HandleScript(int effIndex)
        {
            PreventHitDefaultEffect(effIndex);
            Player target = GetHitPlayer();
            if (target != null)
                target.HandleEmoteCommand(Emote.StateDance);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 45043 - Power Circle (Shifting Naaru Sliver)
    class spell_item_power_circle : AuraScript
    {
        const int SpellLimitlessPower = 45044;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellLimitlessPower);
        }

        bool CheckCaster(Unit target)
        {
            return target.GetGUID() == GetCasterGUID();
        }

        void OnApply(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            GetTarget().CastSpell(null, SpellLimitlessPower, true);
            Aura buff = GetTarget().GetAura(SpellLimitlessPower);
            buff?.SetDuration(GetDuration());
        }

        void OnRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(SpellLimitlessPower);
        }

        public override void Register()
        {
            DoCheckAreaTarget.Add(new(CheckCaster));

            AfterEffectApply.Add(new(OnApply, 0, AuraType.Dummy, AuraEffectHandleModes.Real));
            AfterEffectRemove.Add(new(OnRemove, 0, AuraType.Dummy, AuraEffectHandleModes.Real));
        }
    }

    // http://www.wowhead.com/item=6657 Savory Deviate Delight
    [Script] // 8213 Savory Deviate Delight
    class spell_item_savory_deviate_delight : SpellScript
    {
        const int SpellFlipOutMale = 8219;
        const int SpellFlipOutFemale = 8220;
        const int SpellYaaarrrrMale = 8221;
        const int SpellYaaarrrrFemale = 8222;

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellFlipOutMale, SpellFlipOutFemale, SpellYaaarrrrMale, SpellYaaarrrrFemale);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            int spellId = 0;
            switch (RandomHelper.URand(1, 2))
            {
                // Flip Out - ninja
                case 1: spellId = (caster.GetNativeGender() == Gender.Male ? SpellFlipOutMale : SpellFlipOutFemale); break;
                // Yaaarrrr - pirate
                case 2: spellId = (caster.GetNativeGender() == Gender.Male ? SpellYaaarrrrMale : SpellYaaarrrrFemale); break;
            }
            caster.CastSpell(caster, spellId, true);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // 48129 - Scroll of Recall
    // 60320 - Scroll of Recall Ii
    [Script] // 60321 - Scroll of Recall Iii
    class spell_item_scroll_of_recall : SpellScript
    {
        const int SpellScrollOfRecallI = 48129;
        const int SpellScrollOfRecallIi = 60320;
        const int SpellScrollOfRecallIii = 60321;
        const int SpellLost = 60444;
        const int SpellScrollOfRecallFailAlliance1 = 60323;
        const int SpellScrollOfRecallFailHorde1 = 60328;

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleScript(int effIndex)
        {
            Unit caster = GetCaster();
            byte maxSafeLevel = 0;
            switch (GetSpellInfo().Id)
            {
                case SpellScrollOfRecallI:  // Scroll of Recall
                    maxSafeLevel = 40;
                    break;
                case SpellScrollOfRecallIi:  // Scroll of Recall Ii
                    maxSafeLevel = 70;
                    break;
                case SpellScrollOfRecallIii:  // Scroll of Recal Iii
                    maxSafeLevel = 80;
                    break;
                default:
                    break;
            }

            if (caster.GetLevel() > maxSafeLevel)
            {
                caster.CastSpell(caster, SpellLost, true);

                // Alliance from 60323 to 60330 - Horde from 60328 to 60335
                int spellId = SpellScrollOfRecallFailAlliance1;
                if (GetCaster().ToPlayer().GetTeam() == Team.Horde)
                    spellId = SpellScrollOfRecallFailHorde1;

                GetCaster().CastSpell(GetCaster(), spellId + RandomHelper.IRand(0, 7), true);

                PreventHitDefaultEffect(effIndex);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.TeleportUnits));
        }
    }

    struct TransporterSpells
    {
        public const int EvilTwin = 23445;
        public const int TransporterMalfunctionFire = 23449;
        public const int TransporterMalfunctionSmaller = 36893;
        public const int TransporterMalfunctionBigger = 36895;
        public const int TransporterMalfunctionChicken = 36940;
        public const int TransformHorde = 36897;
        public const int TransformAlliance = 36899;
        public const int SoulSplitEvil = 36900;
        public const int SoulSplitGood = 36901;
    }

    [Script] // 23442 - Dimensional Ripper - Everlook
    class spell_item_dimensional_ripper_everlook : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(TransporterSpells.TransporterMalfunctionFire, TransporterSpells.EvilTwin);
        }

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleScript(int effIndex)
        {
            int r = RandomHelper.IRand(0, 119);
            if (r <= 70)                               // 7/12 success
                return;

            Unit caster = GetCaster();

            if (r < 100)                              // 4/12 evil twin
                caster.CastSpell(caster, TransporterSpells.EvilTwin, true);
            else                                      // 1/12 fire
                caster.CastSpell(caster, TransporterSpells.TransporterMalfunctionFire, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.TeleportUnits));
        }
    }

    [Script] // 36941 - Ultrasafe Transporter: Toshley's Station
    class spell_item_ultrasafe_transporter : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(TransporterSpells.TransporterMalfunctionSmaller, TransporterSpells.TransporterMalfunctionBigger, TransporterSpells.SoulSplitEvil, TransporterSpells.SoulSplitGood,
                TransporterSpells.TransformHorde, TransporterSpells.TransformAlliance, TransporterSpells.TransporterMalfunctionChicken, TransporterSpells.EvilTwin);
        }

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleScript(int effIndex)
        {
            if (!RandomHelper.randChance(50)) // 50% success
                return;

            Unit caster = GetCaster();

            int spellId = 0;
            switch (RandomHelper.URand(0, 6))
            {
                case 0:
                    spellId = TransporterSpells.TransporterMalfunctionSmaller;
                    break;
                case 1:
                    spellId = TransporterSpells.TransporterMalfunctionBigger;
                    break;
                case 2:
                    spellId = TransporterSpells.SoulSplitEvil;
                    break;
                case 3:
                    spellId = TransporterSpells.SoulSplitGood;
                    break;
                case 4:
                    if (caster.ToPlayer().GetTeam() == Team.Alliance)
                        spellId = TransporterSpells.TransformHorde;
                    else
                        spellId = TransporterSpells.TransformAlliance;
                    break;
                case 5:
                    spellId = TransporterSpells.TransporterMalfunctionChicken;
                    break;
                case 6:
                    spellId = TransporterSpells.EvilTwin;
                    break;
                default:
                    break;
            }

            caster.CastSpell(caster, spellId, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.TeleportUnits));
        }
    }

    [Script] // 36890 - Dimensional Ripper - Area 52
    class spell_item_dimensional_ripper_area52 : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(TransporterSpells.TransporterMalfunctionBigger, TransporterSpells.SoulSplitEvil, TransporterSpells.SoulSplitGood, TransporterSpells.TransformHorde, TransporterSpells.TransformAlliance);
        }

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleScript(int effIndex)
        {
            if (!RandomHelper.randChance(50)) // 50% success
                return;

            Unit caster = GetCaster();

            int spellId = 0;
            switch (RandomHelper.URand(0, 3))
            {
                case 0:
                    spellId = TransporterSpells.TransporterMalfunctionBigger;
                    break;
                case 1:
                    spellId = TransporterSpells.SoulSplitEvil;
                    break;
                case 2:
                    spellId = TransporterSpells.SoulSplitGood;
                    break;
                case 3:
                    if (caster.ToPlayer().GetTeam() == Team.Alliance)
                        spellId = TransporterSpells.TransformHorde;
                    else
                        spellId = TransporterSpells.TransformAlliance;
                    break;
                default:
                    break;
            }

            caster.CastSpell(caster, spellId, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.TeleportUnits));
        }
    }

    [Script] // 71169 - Shadow's Fate (Shadowmourne questline)
    class spell_item_unsated_craving : AuraScript
    {
        const int NpcSindragosa = 36853;

        bool CheckProc(ProcEventInfo procInfo)
        {
            Unit caster = procInfo.GetActor();
            if (caster == null || !caster.IsPlayer())
                return false;

            Unit target = procInfo.GetActionTarget();
            if (target == null || target.GetTypeId() != TypeId.Unit || target.IsCritter() || (target.GetEntry() != NpcSindragosa && target.IsSummon()))
                return false;

            return true;
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
        }
    }

    [Script]
    class spell_item_shadows_fate : AuraScript
    {
        const int SpellSoulFeast = 71203;

        void HandleProc(ProcEventInfo procInfo)
        {
            PreventDefaultAction();

            Unit caster = procInfo.GetActor();
            Unit target = GetCaster();
            if (caster == null || target == null)
                return;

            caster.CastSpell(target, SpellSoulFeast, TriggerCastFlags.FullMask);
        }

        public override void Register()
        {
            OnProc.Add(new(HandleProc));
        }
    }

    [Script] // 71903 - Item - Shadowmourne Legendary
    class spell_item_shadowmourne : AuraScript
    {
        const int SpellShadowmourneChaosBaneDamage = 71904;
        const int SpellShadowmourneSoulFragment = 71905;
        const int SpellShadowmourneChaosBaneBuff = 73422;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellShadowmourneChaosBaneDamage, SpellShadowmourneSoulFragment, SpellShadowmourneChaosBaneBuff);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            if (GetTarget().HasAura(SpellShadowmourneChaosBaneBuff)) // cant collect shards while under effect of Chaos Bane buff
                return false;
            return eventInfo.GetProcTarget() != null && eventInfo.GetProcTarget().IsAlive();
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().CastSpell(GetTarget(), SpellShadowmourneSoulFragment, aurEff);

            // this can't be handled in AuraScript of SoulFragments because we need to know victim
            Aura soulFragments = GetTarget().GetAura(SpellShadowmourneSoulFragment);
            if (soulFragments != null)
            {
                if (soulFragments.GetStackAmount() >= 10)
                {
                    GetTarget().CastSpell(eventInfo.GetProcTarget(), SpellShadowmourneChaosBaneDamage, aurEff);
                    soulFragments.Remove();
                }
            }
        }

        void OnRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(SpellShadowmourneSoulFragment);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
            AfterEffectRemove.Add(new(OnRemove, 0, AuraType.Dummy, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 71905 - Soul Fragment
    class spell_item_shadowmourne_soul_fragment : AuraScript
    {
        const int SpellShadowmourneVisualLow = 72521;
        const int SpellShadowmourneVisualHigh = 72523;
        const int SpellShadowmourneChaosBaneBuff = 73422;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellShadowmourneVisualLow, SpellShadowmourneVisualHigh, SpellShadowmourneChaosBaneBuff);
        }

        void OnStackChange(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Unit target = GetTarget();
            switch (GetStackAmount())
            {
                case 1:
                    target.CastSpell(target, SpellShadowmourneVisualLow, true);
                    break;
                case 6:
                    target.RemoveAurasDueToSpell(SpellShadowmourneVisualLow);
                    target.CastSpell(target, SpellShadowmourneVisualHigh, true);
                    break;
                case 10:
                    target.RemoveAurasDueToSpell(SpellShadowmourneVisualHigh);
                    target.CastSpell(target, SpellShadowmourneChaosBaneBuff, true);
                    break;
                default:
                    break;
            }
        }

        void OnRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            Unit target = GetTarget();
            target.RemoveAurasDueToSpell(SpellShadowmourneVisualLow);
            target.RemoveAurasDueToSpell(SpellShadowmourneVisualHigh);
        }

        public override void Register()
        {
            AfterEffectApply.Add(new(OnStackChange, 0, AuraType.ModStat, (AuraEffectHandleModes.Real | AuraEffectHandleModes.Reapply)));
            AfterEffectRemove.Add(new(OnRemove, 0, AuraType.ModStat, AuraEffectHandleModes.Real));
        }
    }

    // http://www.wowhead.com/item=7734 Six Demon Bag
    [Script] // 14537 Six Demon Bag
    class spell_item_six_demon_bag : SpellScript
    {
        const int SpellFrostbolt = 11538;
        const int SpellPolymorph = 14621;
        const int SpellSummonFelhoundMinion = 14642;
        const int SpellFireball = 15662;
        const int SpellChainLightning = 21179;
        const int SpellEnvelopingWinds = 25189;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellFrostbolt, SpellPolymorph, SpellSummonFelhoundMinion, SpellFireball, SpellChainLightning, SpellEnvelopingWinds);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            Unit target = GetHitUnit();
            if (target != null)
            {
                int spellId;
                int rand = RandomHelper.IRand(0, 99);
                if (rand < 25)                      // Fireball (25% chance)
                    spellId = SpellFireball;
                else if (rand < 50)                 // Frostball (25% chance)
                    spellId = SpellFrostbolt;
                else if (rand < 70)                 // Chain Lighting (20% chance)
                    spellId = SpellChainLightning;
                else if (rand < 80)                 // Polymorph (10% chance)
                {
                    spellId = SpellPolymorph;
                    if (RandomHelper.URand(0, 100) <= 30)        // 30% chance to self-cast
                        target = caster;
                }
                else if (rand < 95)                 // Enveloping Winds (15% chance)
                    spellId = SpellEnvelopingWinds;
                else                                // Summon Felhund minion (5% chance)
                {
                    spellId = SpellSummonFelhoundMinion;
                    target = caster;
                }

                caster.CastSpell(target, spellId, GetCastItem());
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 59906 - Swift Hand of Justice Dummy
    class spell_item_swift_hand_justice_dummy : AuraScript
    {
        const int SpellSwiftHandOfJusticeHeal = 59913;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellSwiftHandOfJusticeHeal);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            Unit caster = eventInfo.GetActor();
            CastSpellExtraArgs args = new(aurEff);
            args.AddSpellMod(SpellValueMod.BasePoint0, (int)caster.CountPctFromMaxHealth(aurEff.GetAmount()));
            caster.CastSpell(null, SpellSwiftHandOfJusticeHeal, args);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script] // 28862 - The Eye of Diminution
    class spell_item_the_eye_of_diminution : AuraScript
    {
        void CalculateAmount(AuraEffect aurEff, ref int amount, ref bool canBeRecalculated)
        {
            int diff = GetUnitOwner().GetLevel() - 60;
            if (diff > 0)
                amount += diff;
        }

        public override void Register()
        {
            DoEffectCalcAmount.Add(new(CalculateAmount, 0, AuraType.ModThreat));
        }
    }

    // http://www.wowhead.com/item=44012 Underbelly Elixir
    [Script] // 59640 Underbelly Elixir
    class spell_item_underbelly_elixir : SpellScript
    {
        const int SpellUnderbellyElixirTriggered1 = 59645;
        const int SpellUnderbellyElixirTriggered2 = 59831;
        const int SpellUnderbellyElixirTriggered3 = 59843;

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellUnderbellyElixirTriggered1, SpellUnderbellyElixirTriggered2, SpellUnderbellyElixirTriggered3);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            int spellId = SpellUnderbellyElixirTriggered3;
            switch (RandomHelper.URand(1, 3))
            {
                case 1: spellId = SpellUnderbellyElixirTriggered1; break;
                case 2: spellId = SpellUnderbellyElixirTriggered2; break;
            }
            caster.CastSpell(caster, spellId, true);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 126755 - Wormhole: Pandaria
    class spell_item_wormhole_pandaria : SpellScript
    {
        int[] WormholeTargetLocations =
        {
            126756, //SpellWormholePandariaIsleOfReckoning
            126757, //SpellWormholePandariaKunlaiUnderwater
            126758, //SpellWormholePandariaSraVess
            126759, //SpellWormholePandariaRikkitunVillage
            126760, //SpellWormholePandariaZanvessTree
            126761, //SpellWormholePandariaAnglersWharf
            126762, //SpellWormholePandariaCraneStatue
            126763, //SpellWormholePandariaEmperorsOmen
            126764 //SpellWormholePandariaWhitepetalLake
        };

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(WormholeTargetLocations);
        }

        void HandleTeleport(int effIndex)
        {
            PreventHitDefaultEffect(effIndex);
            int spellId = WormholeTargetLocations.SelectRandom();
            GetCaster().CastSpell(GetHitUnit(), spellId, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleTeleport, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 47776 - Roll 'dem Bones
    class spell_item_worn_troll_dice : SpellScript
    {
        const int TextWornTrollDice = 26152;

        public override bool Validate(SpellInfo spellInfo)
        {
            if (!CliDB.BroadcastTextStorage.ContainsKey(TextWornTrollDice))
                return false;
            return true;
        }

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleScript(int effIndex)
        {
            GetCaster().TextEmote(TextWornTrollDice, GetHitUnit());

            int minimum = 1;
            int maximum = 6;

            // roll twice
            GetCaster().ToPlayer().DoRandomRoll(minimum, maximum);
            GetCaster().ToPlayer().DoRandomRoll(minimum, maximum);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script]
    class spell_item_red_rider_air_rifle : SpellScript
    {
        const int SpellAirRifleHoldVisual = 65582;
        const int SpellAirRifleShoot = 67532;
        const int SpellAirRifleShootSelf = 65577;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellAirRifleHoldVisual, SpellAirRifleShoot, SpellAirRifleShootSelf);
        }

        void HandleScript(int effIndex)
        {
            PreventHitDefaultEffect(effIndex);
            Unit caster = GetCaster();
            Unit target = GetHitUnit();
            if (target != null)
            {
                caster.CastSpell(caster, SpellAirRifleHoldVisual, true);
                // needed because this spell shares Gcd with its triggered spells (which must not be cast with triggered flag)
                Player player = caster.ToPlayer();
                if (player != null)
                    player.GetSpellHistory().CancelGlobalCooldown(GetSpellInfo());
                if (RandomHelper.URand(0, 4) != 0)
                    caster.CastSpell(target, SpellAirRifleShoot, false);
                else
                    caster.CastSpell(caster, SpellAirRifleShootSelf, false);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script]
    class spell_item_book_of_glyph_mastery : SpellScript
    {
        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        SpellCastResult CheckRequirement()
        {
            if (SkillDiscovery.HasDiscoveredAllSpells(GetSpellInfo().Id, GetCaster().ToPlayer()))
            {
                SetCustomCastResultMessage(SpellCustomErrors.LearnedEverything);
                return SpellCastResult.CustomError;
            }

            return SpellCastResult.SpellCastOk;
        }

        void HandleScript(int effIndex)
        {
            Player caster = GetCaster().ToPlayer();
            int spellId = GetSpellInfo().Id;

            // learn random explicit discovery recipe (if any)
            int discoveredSpellId = SkillDiscovery.GetExplicitDiscoverySpell(spellId, caster);
            if (discoveredSpellId != 0)
                caster.LearnSpell(discoveredSpellId, false);
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckRequirement));
            OnEffectHitTarget.Add(new(HandleScript, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script]
    class spell_item_gift_of_the_harvester : SpellScript
    {
        const int NpcGhoul = 28845;
        const int MaxGhouls = 5;

        SpellCastResult CheckRequirement()
        {
            List<TempSummon> ghouls = new();
            GetCaster().GetAllMinionsByEntry(ghouls, NpcGhoul);
            if (ghouls.Count >= MaxGhouls)
            {
                SetCustomCastResultMessage(SpellCustomErrors.TooManyGhouls);
                return SpellCastResult.CustomError;
            }

            return SpellCastResult.SpellCastOk;
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckRequirement));
        }
    }

    [Script]
    class spell_item_map_of_the_geyser_fields : SpellScript
    {
        const int NpcSouthSinkhole = 25664;
        const int NpcNortheastSinkhole = 25665;
        const int NpcNorthwestSinkhole = 25666;

        SpellCastResult CheckSinkholes()
        {
            Unit caster = GetCaster();
            if (caster.FindNearestCreature(NpcSouthSinkhole, 30.0f, true) != null ||
                caster.FindNearestCreature(NpcNortheastSinkhole, 30.0f, true) != null ||
                caster.FindNearestCreature(NpcNorthwestSinkhole, 30.0f, true) != null)
                return SpellCastResult.SpellCastOk;

            SetCustomCastResultMessage(SpellCustomErrors.MustBeCloseToSinkhole);
            return SpellCastResult.CustomError;
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckSinkholes));
        }
    }

    [Script]
    class spell_item_vanquished_clutches : SpellScript
    {
        const int SpellCrusher = 64982;
        const int SpellConstrictor = 64983;
        const int SpellCorruptor = 64984;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellCrusher, SpellConstrictor, SpellCorruptor);
        }

        void HandleDummy(int effIndex)
        {
            int spellId = RandomHelper.RAND(SpellCrusher, SpellConstrictor, SpellCorruptor);
            Unit caster = GetCaster();
            caster.CastSpell(caster, spellId, true);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_ashbringer : SpellScript
    {
        int[] AshbringerSounds =
        {
            8906,                             // "I was pure once"
            8907,                             // "Fought for righteousness"
            8908,                             // "I was once called Ashbringer"
            8920,                             // "Betrayed by my order"
            8921,                             // "Destroyed by Kel'Thuzad"
            8922,                             // "Made to serve"
            8923,                             // "My son watched me die"
            8924,                             // "Crusades fed his rage"
            8925,                             // "Truth is unknown to him"
            8926,                             // "Scarlet Crusade  is pure no longer"
            8927,                             // "Balnazzar's crusade corrupted my son"
            8928                             // "Kill them all!"
         };

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void OnDummyEffect(int effIndex)
        {
            PreventHitDefaultEffect(effIndex);

            Player player = GetCaster().ToPlayer();
            int sound_id = RandomHelper.RAND(AshbringerSounds);

            // Ashbringers effect (spellID 28441) retriggers every 5 seconds, with a chance of making it say one of the above 12 sounds
            if (RandomHelper.URand(0, 60) < 1)
                player.PlayDirectSound(sound_id, player);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(OnDummyEffect, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 58886 - Food
    class spell_magic_eater_food : AuraScript
    {
        const int SpellWildMagic = 58891;
        const int SpellWellFed1 = 57288;
        const int SpellWellFed2 = 57139;
        const int SpellWellFed3 = 57111;
        const int SpellWellFed4 = 57286;
        const int SpellWellFed5 = 57291;

        void HandleTriggerSpell(AuraEffect aurEff)
        {
            PreventDefaultAction();
            Unit target = GetTarget();
            switch (RandomHelper.IRand(0, 5))
            {
                case 0:
                    target.CastSpell(target, SpellWildMagic, true);
                    break;
                case 1:
                    target.CastSpell(target, SpellWellFed1, true);
                    break;
                case 2:
                    target.CastSpell(target, SpellWellFed2, true);
                    break;
                case 3:
                    target.CastSpell(target, SpellWellFed3, true);
                    break;
                case 4:
                    target.CastSpell(target, SpellWellFed4, true);
                    break;
                case 5:
                    target.CastSpell(target, SpellWellFed5, true);
                    break;
            }
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(HandleTriggerSpell, 1, AuraType.PeriodicTriggerSpell));
        }
    }

    [Script]
    class spell_item_purify_helboar_meat : SpellScript
    {
        const int SpellSummonPurifiedHelboarMeat = 29277;
        const int SpellSummonToxicHelboarMeat = 29278;

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellSummonPurifiedHelboarMeat, SpellSummonToxicHelboarMeat);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            caster.CastSpell(caster, RandomHelper.randChance(50) ? SpellSummonPurifiedHelboarMeat : SpellSummonToxicHelboarMeat, true);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_nigh_invulnerability : SpellScript
    {
        const int SpellNighInvulnerability = 30456;
        const int SpellCompleteVulnerability = 30457;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellNighInvulnerability, SpellCompleteVulnerability);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            Item castItem = GetCastItem();
            if (castItem != null)
            {
                if (RandomHelper.randChance(86))                  // Nigh-Invulnerability   - success
                    caster.CastSpell(caster, SpellNighInvulnerability, castItem);
                else                                    // Complete Vulnerability - backfire in 14% casts
                    caster.CastSpell(caster, SpellCompleteVulnerability, castItem);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_poultryizer : SpellScript
    {
        const int SpellPoultryizerSuccess = 30501;
        const int SpellPoultryizerBackfire = 30504;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellPoultryizerSuccess, SpellPoultryizerBackfire);
        }

        void HandleDummy(int effIndex)
        {
            if (GetCastItem() != null && GetHitUnit() != null)
                GetCaster().CastSpell(GetHitUnit(), RandomHelper.randChance(80) ? SpellPoultryizerSuccess : SpellPoultryizerBackfire, GetCastItem());
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_socrethars_stone : SpellScript
    {
        const int SpellSocretharToSeat = 35743;
        const int SpellSocretharFromSeat = 35744;

        public override bool Load()
        {
            return (GetCaster().GetAreaId() == 3900 || GetCaster().GetAreaId() == 3742);
        }
        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellSocretharToSeat, SpellSocretharFromSeat);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            switch (caster.GetAreaId())
            {
                case 3900:
                    caster.CastSpell(caster, SpellSocretharToSeat, true);
                    break;
                case 3742:
                    caster.CastSpell(caster, SpellSocretharFromSeat, true);
                    break;
                default:
                    return;
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_demon_broiled_surprise : SpellScript
    {
        const int QuestSuperHotStew = 11379;
        const int SpellCreateDemonBroiledSurprise = 43753;
        const int NpcAbyssalFlamebringer = 19973;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellCreateDemonBroiledSurprise) &&
                ObjectMgr.GetCreatureTemplate(NpcAbyssalFlamebringer) != null &&
                ObjectMgr.GetQuestTemplate(QuestSuperHotStew) != null;
        }

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleDummy(int effIndex)
        {
            Unit player = GetCaster();
            player.CastSpell(player, SpellCreateDemonBroiledSurprise, false);
        }

        SpellCastResult CheckRequirement()
        {
            Player player = GetCaster().ToPlayer();
            if (player.GetQuestStatus(QuestSuperHotStew) != QuestStatus.Incomplete)
                return SpellCastResult.CantDoThatRightNow;

            Creature creature = player.FindNearestCreature(NpcAbyssalFlamebringer, 10, false);
            if (creature != null)
                if (creature.IsDead())
                    return SpellCastResult.SpellCastOk;
            return SpellCastResult.NotHere;
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 1, SpellEffectName.Dummy));
            OnCheckCast.Add(new(CheckRequirement));
        }
    }

    [Script]
    class spell_item_complete_raptor_capture : SpellScript
    {
        const int SpellRaptorCaptureCredit = 42337;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellRaptorCaptureCredit);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            if (GetHitCreature() != null)
            {
                GetHitCreature().DespawnOrUnsummon();

                //cast spell Raptor Capture Credit
                caster.CastSpell(caster, SpellRaptorCaptureCredit, true);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_impale_leviroth : SpellScript
    {
        const int NpcLeviroth = 26452;
        const int SpellLevirothSelfImpale = 49882;

        public override bool Validate(SpellInfo spell)
        {
            if (ObjectMgr.GetCreatureTemplate(NpcLeviroth) == null)
                return false;
            return true;
        }

        void HandleDummy(int effIndex)
        {
            Creature target = GetHitCreature();
            if (target != null)
                if (target.GetEntry() == NpcLeviroth && !target.HealthBelowPct(95))
                {
                    target.CastSpell(target, SpellLevirothSelfImpale, true);
                    target.ResetPlayerDamageReq();
                }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 23725 - Gift of Life
    class spell_item_lifegiving_gem : SpellScript
    {
        const int SpellGiftOfLife1 = 23782;
        const int SpellGiftOfLife2 = 23783;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellGiftOfLife1, SpellGiftOfLife2);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            caster.CastSpell(caster, SpellGiftOfLife1, true);
            caster.CastSpell(caster, SpellGiftOfLife2, true);
        }

        public override void Register()
        {
            OnEffectHit.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_nitro_boosts : SpellScript
    {
        const int SpellNitroBoostsSuccess = 54861;
        const int SpellNitroBoostsBackfire = 54621;

        public override bool Load()
        {
            return GetCastItem() != null;
        }

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellNitroBoostsSuccess, SpellNitroBoostsBackfire);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            bool success = true;
            if (!caster.GetMap().IsDungeon())
                success = RandomHelper.randChance(95); // nitro boosts can only fail in flying-enabled locations on 3.3.5
            caster.CastSpell(caster, success ? SpellNitroBoostsSuccess : SpellNitroBoostsBackfire, GetCastItem());
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_nitro_boosts_backfire : AuraScript
    {
        const int SpellNitroBoostsParachute = 54649;

        float lastZ = MapConst.InvalidHeight;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellNitroBoostsParachute);
        }

        void HandleApply(AuraEffect effect, AuraEffectHandleModes mode)
        {
            lastZ = GetTarget().GetPositionZ();
        }

        void HandlePeriodicDummy(AuraEffect effect)
        {
            PreventDefaultAction();
            float curZ = GetTarget().GetPositionZ();
            if (curZ < lastZ)
            {
                if (RandomHelper.randChance(80)) // we don't have enough sniffs to verify this, guesstimate
                    GetTarget().CastSpell(GetTarget(), SpellNitroBoostsParachute, effect);
                GetAura().Remove();
            }
            else
                lastZ = curZ;
        }

        public override void Register()
        {
            OnEffectApply.Add(new(HandleApply, 1, AuraType.PeriodicTriggerSpell, AuraEffectHandleModes.Real));
            OnEffectPeriodic.Add(new(HandlePeriodicDummy, 1, AuraType.PeriodicTriggerSpell));
        }
    }

    [Script]
    class spell_item_rocket_boots : SpellScript
    {
        const int SpellRocketBootsProc = 30452;

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellRocketBootsProc);
        }

        void HandleDummy(int effIndex)
        {
            Player caster = GetCaster().ToPlayer();
            Battleground bg = caster.GetBattleground();
            bg?.EventPlayerDroppedFlag(caster);

            caster.GetSpellHistory().ResetCooldown(SpellRocketBootsProc);
            caster.CastSpell(caster, SpellRocketBootsProc, true);
        }

        SpellCastResult CheckCast()
        {
            if (GetCaster().IsInWater())
                return SpellCastResult.OnlyAbovewater;
            return SpellCastResult.SpellCastOk;
        }

        public override void Register()
        {
            OnCheckCast.Add(new(CheckCast));
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 67489 - Runic Healing Injector
    class spell_item_runic_healing_injector : SpellScript
    {
        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        void HandleHeal(int effIndex)
        {
            Player caster = GetCaster().ToPlayer();
            if (caster != null)
                if (caster.HasSkill(SkillType.Engineering))
                    SetHitHeal((int)(GetHitHeal() * 1.25f));
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleHeal, 0, SpellEffectName.Heal));
        }
    }

    [Script]
    class spell_item_pygmy_oil : SpellScript
    {
        const int SpellPygmyOilPygmyAura = 53806;
        const int SpellPygmyOilSmallerAura = 53805;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellPygmyOilPygmyAura, SpellPygmyOilSmallerAura);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            Aura aura = caster.GetAura(SpellPygmyOilPygmyAura);
            if (aura != null)
                aura.RefreshDuration();
            else
            {
                aura = caster.GetAura(SpellPygmyOilSmallerAura);
                if (aura == null || aura.GetStackAmount() < 5 || !RandomHelper.randChance(50))
                    caster.CastSpell(caster, SpellPygmyOilSmallerAura, true);
                else
                {
                    aura.Remove();
                    caster.CastSpell(caster, SpellPygmyOilPygmyAura, true);
                }
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_unusual_compass : SpellScript
    {
        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            caster.SetFacingTo(RandomHelper.FRand(0.0f, 2.0f * MathF.PI));
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_chicken_cover : SpellScript
    {
        const int SpellChickenNet = 51959;
        const int SpellCaptureChickenEscape = 51037;
        const int QuestChickenParty = 12702;
        const int QuestFlownTheCoop = 12532;

        public override bool Load()
        {
            return GetCaster().IsPlayer();
        }

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellChickenNet, SpellCaptureChickenEscape) &&
                ObjectMgr.GetQuestTemplate(QuestChickenParty) != null &&
                ObjectMgr.GetQuestTemplate(QuestFlownTheCoop) != null;
        }

        void HandleDummy(int effIndex)
        {
            Player caster = GetCaster().ToPlayer();
            Unit target = GetHitUnit();
            if (target != null)
            {
                if (!target.HasAura(SpellChickenNet) && (caster.GetQuestStatus(QuestChickenParty) == QuestStatus.Incomplete || caster.GetQuestStatus(QuestFlownTheCoop) == QuestStatus.Incomplete))
                {
                    caster.CastSpell(caster, SpellCaptureChickenEscape, true);
                    target.KillSelf();
                }
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_muisek_vessel : SpellScript
    {
        void HandleDummy(int effIndex)
        {
            Creature target = GetHitCreature();
            if (target != null)
                if (target.IsDead())
                    target.DespawnOrUnsummon();
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script]
    class spell_item_greatmothers_soulcatcher : SpellScript
    {
        const int SpellForceCastSummonGnomeSoul = 46486;

        void HandleDummy(int effIndex)
        {
            if (GetHitUnit() != null)
                GetCaster().CastSpell(GetCaster(), SpellForceCastSummonGnomeSoul);
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // Item - 49310: Purified Shard of the Scale
    // 69755 - Purified Shard of the Scale - Equip Effect

    // Item - 49488: Shiny Shard of the Scale
    // 69739 - Shiny Shard of the Scale - Equip Effect
    [Script("spell_item_purified_shard_of_the_scale", 69733, 69729)]
    [Script("spell_item_shiny_shard_of_the_scale", 69734, 69730)]
    class spell_item_shard_of_the_scale : AuraScript
    {
        int _healProc;
        int _damageProc;

        public spell_item_shard_of_the_scale(int healProc, int damageProc)
        {
            _healProc = healProc;
            _damageProc = damageProc;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(_healProc, _damageProc);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetProcTarget();

            if (eventInfo.GetTypeMask().HasFlag(ProcFlags.DealHelpfulSpell))
                caster.CastSpell(target, _healProc, aurEff);

            if (eventInfo.GetTypeMask().HasFlag(ProcFlags.DealHarmfulSpell))
                caster.CastSpell(target, _damageProc, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script]
    class spell_item_soul_preserver : AuraScript
    {
        const int SpellSoulPreserverDruid = 60512;
        const int SpellSoulPreserverPaladin = 60513;
        const int SpellSoulPreserverPriest = 60514;
        const int SpellSoulPreserverShaman = 60515;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellSoulPreserverDruid, SpellSoulPreserverPaladin, SpellSoulPreserverPriest, SpellSoulPreserverShaman);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            Unit caster = eventInfo.GetActor();

            switch (caster.GetClass())
            {
                case Class.Druid:
                    caster.CastSpell(caster, SpellSoulPreserverDruid, aurEff);
                    break;
                case Class.Paladin:
                    caster.CastSpell(caster, SpellSoulPreserverPaladin, aurEff);
                    break;
                case Class.Priest:
                    caster.CastSpell(caster, SpellSoulPreserverPriest, aurEff);
                    break;
                case Class.Shaman:
                    caster.CastSpell(caster, SpellSoulPreserverShaman, aurEff);
                    break;
                default:
                    break;
            }
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    // Item - 34678: Shattered Sun Pendant of Acumen
    // 45481 - Sunwell Exalted Caster Neck

    // Item - 34679: Shattered Sun Pendant of Might
    // 45482 - Sunwell Exalted Melee Neck

    // Item - 34680: Shattered Sun Pendant of Resolve
    // 45483 - Sunwell Exalted Tank Neck

    // Item - 34677: Shattered Sun Pendant of Restoration
    // 45484 Sunwell Exalted Healer Neck
    [Script("spell_item_sunwell_exalted_caster_neck", 45479, 45429)] // Light's Wrath if Exalted by Aldor, Arcane Bolt if Exalted by Scryers
    [Script("spell_item_sunwell_exalted_melee_neck", 45480, 45428)] // Light's Strength if Exalted by Aldor, Arcane Strike if Exalted by Scryers
    [Script("spell_item_sunwell_exalted_tank_neck", 45432, 45431)] //Light's Ward if Exalted by Aldor, Arcane Insight if Exalted by Scryers
    [Script("spell_item_sunwell_exalted_healer_neck", 45478, 45430)] //Light's Salvation if Exalted by Aldor, Arcane Surge if Exalted by Scryers
    class spell_item_sunwell_neck : AuraScript
    {
        const int FactionAldor = 932;
        const int FactionScryers = 934;

        int _aldor;
        int _scryers;

        public spell_item_sunwell_neck(int aldor, int scryers)
        {
            _aldor = aldor;
            _scryers = scryers;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(_aldor, _scryers) &&
                CliDB.FactionStorage.ContainsKey(FactionAldor) &&
                CliDB.FactionStorage.ContainsKey(FactionScryers);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            if (!eventInfo.GetActor().IsPlayer())
                return false;
            return true;
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            Player player = eventInfo.GetActor().ToPlayer();
            Unit target = eventInfo.GetProcTarget();

            // Aggression checks are in the spell system... just cast and forget
            if (player.GetReputationRank(FactionAldor) == ReputationRank.Exalted)
                player.CastSpell(target, _aldor, aurEff);

            if (player.GetReputationRank(FactionScryers) == ReputationRank.Exalted)
                player.CastSpell(target, _scryers, aurEff);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.Dummy));
        }
    }

    [Script]
    class spell_item_toy_train_set_pulse : SpellScript
    {
        void HandleDummy(int index)
        {
            Player target = GetHitUnit().ToPlayer();
            if (target != null)
            {
                target.HandleEmoteCommand(Emote.OneshotTrain);
                var soundEntry = DB2Mgr.GetTextSoundEmoteFor((int)TextEmotes.Train, target.GetRace(), target.GetNativeGender(), target.GetClass());
                if (soundEntry != null)
                    target.PlayDistanceSound(soundEntry.SoundId);
            }
        }

        void HandleTargets(List<WorldObject> targetList)
        {
            targetList.RemoveAll(obj => !obj.IsPlayer());
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.ScriptEffect));
            OnObjectAreaTargetSelect.Add(new(HandleTargets, SpellConst.EffectAll, Targets.UnitSrcAreaAlly));
        }
    }

    [Script]
    class spell_item_death_choice : AuraScript
    {
        const int SpellDeathChoiceNormalAura = 67702;
        const int SpellDeathChoiceNormalAgility = 67703;
        const int SpellDeathChoiceNormalStrength = 67708;
        const int SpellDeathChoiceHeroicAura = 67771;
        const int SpellDeathChoiceHeroicAgility = 67772;
        const int SpellDeathChoiceHeroicStrength = 67773;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellDeathChoiceNormalStrength, SpellDeathChoiceNormalAgility, SpellDeathChoiceHeroicStrength, SpellDeathChoiceHeroicAgility);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            Unit caster = eventInfo.GetActor();
            float str = caster.GetStat(Stats.Strength);
            float agi = caster.GetStat(Stats.Agility);

            switch (aurEff.GetId())
            {
                case SpellDeathChoiceNormalAura:
                {
                    if (str > agi)
                        caster.CastSpell(caster, SpellDeathChoiceNormalStrength, aurEff);
                    else
                        caster.CastSpell(caster, SpellDeathChoiceNormalAgility, aurEff);
                    break;
                }
                case SpellDeathChoiceHeroicAura:
                {
                    if (str > agi)
                        caster.CastSpell(caster, SpellDeathChoiceHeroicStrength, aurEff);
                    else
                        caster.CastSpell(caster, SpellDeathChoiceHeroicAgility, aurEff);
                    break;
                }
                default:
                    break;
            }
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script("spell_item_lightning_capacitor", 37658, 37661)] // Lightning Capacitor
    [Script("spell_item_thunder_capacitor", 54842, 54843)] // Thunder Capacitor
    [Script("spell_item_toc25_normal_caster_trinket", 67713, 67714)] // Item - Coliseum 25 Normal Caster Trinket
    [Script("spell_item_toc25_heroic_caster_trinket", 67759, 67760)] // Item - Coliseum 25 Heroic Caster Trinket
    class spell_item_trinket_stack_AuraScript : AuraScript
    {
        int _stackSpell;
        int _triggerSpell;

        public spell_item_trinket_stack_AuraScript(int stackSpell, int triggerSpell)
        {
            _stackSpell = stackSpell;
            _triggerSpell = triggerSpell;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(_stackSpell, _triggerSpell);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            Unit caster = eventInfo.GetActor();

            caster.CastSpell(caster, _stackSpell, aurEff); // cast the stack

            Aura dummy = caster.GetAura(_stackSpell); // retrieve aura

            //dont do anything if it's not the right amount of stacks;
            if (dummy == null || dummy.GetStackAmount() < aurEff.GetAmount())
                return;

            // if right amount, Remove the aura and cast real trigger
            caster.RemoveAurasDueToSpell(_stackSpell);
            Unit target = eventInfo.GetActionTarget();
            if (target != null)
                caster.CastSpell(target, _triggerSpell, aurEff);
        }

        void OnRemove(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(_stackSpell);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
            AfterEffectRemove.Add(new(OnRemove, 0, AuraType.ProcTriggerSpell, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 57345 - Darkmoon Card: Greatness
    class spell_item_darkmoon_card_greatness : AuraScript
    {
        const int SpellDarkmoonCardStrength = 60229;
        const int SpellDarkmoonCardAgility = 60233;
        const int SpellDarkmoonCardIntellect = 60234;
        const int SpellDarkmoonCardVersatility = 60235;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellDarkmoonCardStrength, SpellDarkmoonCardAgility, SpellDarkmoonCardIntellect, SpellDarkmoonCardVersatility);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            Unit caster = eventInfo.GetActor();
            float str = caster.GetStat(Stats.Strength);
            float agi = caster.GetStat(Stats.Agility);
            float intl = caster.GetStat(Stats.Intellect);
            float vers = 0.0f; // caster.GetStat(StatVersatility);
            float stat = 0.0f;

            int spellTrigger = SpellDarkmoonCardStrength;

            if (str > stat)
            {
                spellTrigger = SpellDarkmoonCardStrength;
                stat = str;
            }

            if (agi > stat)
            {
                spellTrigger = SpellDarkmoonCardAgility;
                stat = agi;
            }

            if (intl > stat)
            {
                spellTrigger = SpellDarkmoonCardIntellect;
                stat = intl;
            }

            if (vers > stat)
                spellTrigger = SpellDarkmoonCardVersatility;

            caster.CastSpell(caster, spellTrigger, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 27522,40336 - Mana Drain
    class spell_item_mana_drain : AuraScript
    {
        const int SpellManaDrainEnergize = 29471;
        const int SpellManaDrainLeech = 27526;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellManaDrainEnergize, SpellManaDrainLeech);
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();

            Unit caster = eventInfo.GetActor();
            Unit target = eventInfo.GetActionTarget();

            if (caster.IsAlive())
                caster.CastSpell(caster, SpellManaDrainEnergize, aurEff);

            if (target != null && target.IsAlive())
                caster.CastSpell(target, SpellManaDrainLeech, aurEff);
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 51640 - Taunt Flag Targeting
    class spell_item_taunt_flag_targeting : SpellScript
    {
        const int SpellTauntFlag = 51657;
        const int EmotePlantsFlag = 28008;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellTauntFlag) &&
                CliDB.BroadcastTextStorage.ContainsKey(EmotePlantsFlag);
        }

        void FilterTargets(List<WorldObject> targets)
        {
            targets.RemoveAll(obj => !obj.IsPlayer() && obj.GetTypeId() != TypeId.Corpse);

            if (targets.Empty())
            {
                FinishCast(SpellCastResult.NoValidTargets);
                return;
            }

            targets.RandomResize(1);
        }

        void HandleDummy(int effIndex)
        {
            // we *really* want the unit implementation here
            // it sends a packet like seen on sniff
            GetCaster().TextEmote(EmotePlantsFlag, GetHitUnit(), false);

            GetCaster().CastSpell(GetHitUnit(), SpellTauntFlag, true);
        }

        public override void Register()
        {
            OnObjectAreaTargetSelect.Add(new(FilterTargets, 0, Targets.CorpseSrcAreaEnemy));
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 29830 - Mirren's Drinking Hat
    class spell_item_mirrens_drinking_hat : SpellScript
    {
        const int SpellLochModanLager = 29827;
        const int SpellStouthammerLite = 29828;
        const int SpellAeriePeakPaleAle = 29829;

        void HandleScriptEffect(int effIndex)
        {
            int spellId;
            switch (RandomHelper.URand(1, 6))
            {
                case 1:
                case 2:
                case 3:
                    spellId = SpellLochModanLager; break;
                case 4:
                case 5:
                    spellId = SpellStouthammerLite; break;
                case 6:
                    spellId = SpellAeriePeakPaleAle; break;
                default:
                    return;
            }

            Unit caster = GetCaster();
            caster.CastSpell(caster, spellId, GetSpell());
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScriptEffect, 0, SpellEffectName.ScriptEffect));
        }
    }

    [Script] // 13180 - Gnomish Mind Control Cap
    class spell_item_mind_control_cap : SpellScript
    {
        const int RollChanceDullard = 32;
        const int RollChanceNoBackfire = 95;
        const int SpellGnomishMindControlCap = 13181;
        const int SpellDullard = 67809;

        public override bool Load()
        {
            return GetCastItem() != null;
        }

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellGnomishMindControlCap, SpellDullard);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            Unit target = GetHitUnit();
            if (target != null)
            {
                if (RandomHelper.randChance(RollChanceNoBackfire))
                    caster.CastSpell(target, RandomHelper.randChance(RollChanceDullard) ? SpellDullard : SpellGnomishMindControlCap, GetCastItem());
                else
                    target.CastSpell(caster, SpellGnomishMindControlCap, true); // backfire - 5% chance
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    [Script] // 8344 - Universal Remote (Gnomish Universal Remote)
    class spell_item_universal_remote : SpellScript
    {
        const int SpellControlMachine = 8345;
        const int SpellMobilityMalfunction = 8346;
        const int SpellTargetLock = 8347;

        public override bool Load()
        {
            return GetCastItem() != null;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellControlMachine, SpellMobilityMalfunction, SpellTargetLock);
        }

        void HandleDummy(int effIndex)
        {
            Unit target = GetHitUnit();
            if (target != null)
            {
                uint chance = RandomHelper.URand(0, 99);
                if (chance < 15)
                    GetCaster().CastSpell(target, SpellTargetLock, GetCastItem());
                else if (chance < 25)
                    GetCaster().CastSpell(target, SpellMobilityMalfunction, GetCastItem());
                else
                    GetCaster().CastSpell(target, SpellControlMachine, GetCastItem());
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }

    // Item - 19950: Zandalarian Hero Charm
    // 24658 - Unstable Power
    // Item - 19949: Zandalarian Hero Medallion
    // 24661 - Restless Strength
    [Script("spell_item_unstable_power", 24659)]
    [Script("spell_item_restless_strength", 24662)]
    class spell_item_zandalarian_charm : AuraScript
    {
        int _spellId;

        public spell_item_zandalarian_charm(int SpellId)
        {
            _spellId = SpellId;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(_spellId);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            SpellInfo spellInfo = eventInfo.GetSpellInfo();
            if (spellInfo != null)
                if (spellInfo.Id != m_scriptSpellId)
                    return true;

            return false;
        }

        void HandleStackDrop(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            PreventDefaultAction();
            GetTarget().RemoveAuraFromStack(_spellId);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            OnEffectProc.Add(new(HandleStackDrop, 0, AuraType.Dummy));
        }
    }

    [Script] // 28200 - Ascendance
    class spell_item_talisman_of_ascendance : AuraScript
    {
        const int SpellTalismanOfAscendance = 28200;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellTalismanOfAscendance);
        }

        void OnRemove(AuraEffect effect, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(effect.GetSpellEffectInfo().TriggerSpell);
        }

        public override void Register()
        {
            OnEffectRemove.Add(new(OnRemove, 0, AuraType.ProcTriggerSpell, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 29602 - Jom Gabbar
    class spell_item_jom_gabbar : AuraScript
    {
        const int SpellJomGabbar = 29602;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellJomGabbar);
        }

        void OnRemove(AuraEffect effect, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(effect.GetSpellEffectInfo().TriggerSpell);
        }

        public override void Register()
        {
            OnEffectRemove.Add(new(OnRemove, 0, AuraType.PeriodicTriggerSpell, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 45040 - Battle Trance
    class spell_item_battle_trance : AuraScript
    {
        const int SpellBattleTrance = 45040;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellBattleTrance);
        }

        void OnRemove(AuraEffect effect, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(effect.GetSpellEffectInfo().TriggerSpell);
        }

        public override void Register()
        {
            OnEffectRemove.Add(new(OnRemove, 0, AuraType.ProcTriggerSpell, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 90900 - World-Queller Focus
    class spell_item_world_queller_focus : AuraScript
    {
        const int SpellWorldQuellerFocus = 90900;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellWorldQuellerFocus);
        }

        void OnRemove(AuraEffect effect, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(effect.GetSpellEffectInfo().TriggerSpell);
        }

        public override void Register()
        {
            OnEffectRemove.Add(new(OnRemove, 0, AuraType.ProcTriggerSpell, AuraEffectHandleModes.Real));
        }
    }

    // 118089 - Azure Water Strider
    // 127271 - Crimson Water Strider
    // 127272 - Orange Water Strider
    // 127274 - Jade Water Strider
    [Script] // 127278 - Golden Water Strider
    class spell_item_water_strider : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect((spellInfo.Id, 1));
        }

        void OnRemove(AuraEffect effect, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(GetSpellInfo().GetEffect(1).TriggerSpell);
        }

        public override void Register()
        {
            OnEffectRemove.Add(new(OnRemove, 0, AuraType.Mounted, AuraEffectHandleModes.Real));
        }
    }

    // 144671 - Brutal Kinship
    [Script] // 145738 - Brutal Kinship
    class spell_item_brutal_kinship : AuraScript
    {
        const int SpellBrutalKinship1 = 144671;
        const int SpellBrutalKinship2 = 145738;

        public override bool Validate(SpellInfo spell)
        {
            return ValidateSpellInfo(SpellBrutalKinship1, SpellBrutalKinship2);
        }

        void OnRemove(AuraEffect effect, AuraEffectHandleModes mode)
        {
            GetTarget().RemoveAurasDueToSpell(effect.GetSpellEffectInfo().TriggerSpell);
        }

        public override void Register()
        {
            OnEffectRemove.Add(new(OnRemove, 0, AuraType.ProcTriggerSpell, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 45051 - Mad Alchemist's Potion (34440)
    class spell_item_mad_alchemists_potion : SpellScript
    {
        void SecondaryEffect()
        {
            List<int> availableElixirs = new()
            {
                // Battle Elixirs
                33720, // Onslaught Elixir (28102)
                54452, // Adept's Elixir (28103)
                33726, // Elixir of Mastery (28104)
                28490, // Elixir of Major Strength (22824)
                28491, // Elixir of Healing Power (22825)
                28493, // Elixir of Major Frost Power (22827)
                54494, // Elixir of Major Agility (22831)
                28501, // Elixir of Major Firepower (22833)
                28503,// Elixir of Major Shadow Power (22835)
                38954, // Fel Strength Elixir (31679)
                // Guardian Elixirs
                39625, // Elixir of Major Fortitude (32062)
                39626, // Earthen Elixir (32063)
                39627, // Elixir of Draenic Wisdom (32067)
                39628, // Elixir of Ironskin (32068)
                28502, // Elixir of Major Defense (22834)
                28514, // Elixir of Empowerment (22848)
                // Other
                28489, // Elixir of Camouflage (22823)
                28496  // Elixir of the Searching Eye (22830)
            };

            Unit target = GetCaster();

            if (target.GetPowerType() == PowerType.Mana)
                availableElixirs.Add(28509); // Elixir of Major Mageblood (22840)

            int chosenElixir = availableElixirs.SelectRandom();

            bool useElixir = true;

            SpellGroup chosenSpellGroup = SpellGroup.None;
            if (SpellMgr.IsSpellMemberOfSpellGroup(chosenElixir, SpellGroup.ElixirBattle))
                chosenSpellGroup = SpellGroup.ElixirBattle;
            if (SpellMgr.IsSpellMemberOfSpellGroup(chosenElixir, SpellGroup.ElixirGuardian))
                chosenSpellGroup = SpellGroup.ElixirGuardian;
            // If another spell of the same group is already active the elixir should not be cast
            if (chosenSpellGroup != SpellGroup.None)
            {
                var auraMap = target.GetAppliedAuras();
                foreach (var (_, app) in auraMap)
                {
                    int spellId = app.GetBase().GetId();
                    if (SpellMgr.IsSpellMemberOfSpellGroup(spellId, chosenSpellGroup) && spellId != chosenElixir)
                    {
                        useElixir = false;
                        break;
                    }
                }
            }

            if (useElixir)
                target.CastSpell(target, chosenElixir, GetCastItem());
        }

        public override void Register()
        {
            AfterCast.Add(new(SecondaryEffect));
        }
    }

    [Script] // 53750 - Crazy Alchemist's Potion (40077)
    class spell_item_crazy_alchemists_potion : SpellScript
    {
        void SecondaryEffect()
        {
            List<int> availableElixirs = new()
            {
                43185, // Runic Healing Potion (33447)
                53750, // Crazy Alchemist's Potion (40077)
                53761, // Powerful Rejuvenation Potion (40087)
                53762, // Indestructible Potion (40093)
                53908, // Potion of Speed (40211)
                53909, // Potion of Wild Magic (40212)
                53910, // Mighty Arcane Protection Potion (40213)
                53911, // Mighty Fire Protection Potion (40214)
                53913, // Mighty Frost Protection Potion (40215)
                53914, // Mighty Nature Protection Potion (40216)
                53915  // Mighty Shadow Protection Potion (40217)
            };


            Unit target = GetCaster();

            if (!target.IsInCombat())
                availableElixirs.Add(53753); // Potion of Nightmares (40081)
            if (target.GetPowerType() == PowerType.Mana)
                availableElixirs.Add(43186); // Runic Mana Potion(33448)

            int chosenElixir = availableElixirs.SelectRandom();

            target.CastSpell(target, chosenElixir, GetCastItem());
        }

        public override void Register()
        {
            AfterCast.Add(new(SecondaryEffect));
        }
    }

    [Script] // 21149 - Egg Nog
    class spell_item_eggnog : SpellScript
    {
        const int SpellEggNogReindeer = 21936;
        const int SpellEggNogSnowman = 21980;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellEggNogReindeer, SpellEggNogSnowman);
        }

        void HandleScript(int effIndex)
        {
            if (RandomHelper.randChance(40))
                GetCaster().CastSpell(GetHitUnit(), RandomHelper.randChance(50) ? SpellEggNogReindeer : SpellEggNogSnowman, GetCastItem());
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleScript, 2, SpellEffectName.Inebriate));
        }
    }

    // 208051 - Sephuz's Secret
    // 234867 - Sephuz's Secret
    [Script] // 236763 - Sephuz's Secret
    class spell_item_sephuzs_secret : AuraScript
    {
        const int SpellSephuzsSecretCooldown = 226262;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellSephuzsSecretCooldown);
        }

        bool CheckProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            if (GetUnitOwner().HasAura(SpellSephuzsSecretCooldown))
                return false;

            if (eventInfo.GetHitMask().HasAnyFlag(ProcFlagsHit.Interrupt | ProcFlagsHit.Dispel))
                return true;

            Spell procSpell = eventInfo.GetProcSpell();
            if (procSpell == null)
                return false;

            bool isCrowdControl = procSpell.GetSpellInfo().HasAura(AuraType.ModConfuse)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModFear)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModStun)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModPacify)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModRoot)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModSilence)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModPacifySilence)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModRoot2);

            if (!isCrowdControl)
                return false;

            return true;
        }

        void HandleProc(AuraEffect aurEff, ProcEventInfo procInfo)
        {
            PreventDefaultAction();

            GetUnitOwner().CastSpell(GetUnitOwner(), SpellSephuzsSecretCooldown, TriggerCastFlags.FullMask);
            GetUnitOwner().CastSpell(procInfo.GetProcTarget(), aurEff.GetSpellEffectInfo().TriggerSpell, new CastSpellExtraArgs(aurEff).SetTriggeringSpell(procInfo.GetProcSpell()));
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(CheckProc, 0, AuraType.ProcTriggerSpell));
            OnEffectProc.Add(new(HandleProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 215266 - Fragile Echoes
    class spell_item_amalgams_seventh_spine : AuraScript
    {
        const int SpellFragileEchoesMonk = 225281;
        const int SpellFragileEchoesShaman = 225292;
        const int SpellFragileEchoesPriestDiscipline = 225294;
        const int SpellFragileEchoesPaladin = 225297;
        const int SpellFragileEchoesDruid = 225298;
        const int SpellFragileEchoesPriestHoly = 225366;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellFragileEchoesMonk, SpellFragileEchoesShaman, SpellFragileEchoesPriestDiscipline, SpellFragileEchoesPaladin, SpellFragileEchoesDruid, SpellFragileEchoesPriestHoly);
        }

        void ForcePeriodic(AuraEffect aurEff, ref bool isPeriodic, ref int amplitude)
        {
            // simulate heartbeat timer
            isPeriodic = true;
            amplitude = 5000;
        }

        void UpdateSpecAura(AuraEffect aurEff)
        {
            PreventDefaultAction();
            Player target = GetTarget().ToPlayer();
            if (target == null)
                return;

            void updateAuraIfInCorrectSpec(ChrSpecialization spec, int aura)
            {
                if (target.GetPrimarySpecialization() != spec)
                    target.RemoveAurasDueToSpell(aura);
                else if (!target.HasAura(aura))
                    target.CastSpell(target, aura, aurEff);
            }

            switch (target.GetClass())
            {
                case Class.Monk:
                    updateAuraIfInCorrectSpec(ChrSpecialization.MonkMistweaver, SpellFragileEchoesMonk);
                    break;
                case Class.Shaman:
                    updateAuraIfInCorrectSpec(ChrSpecialization.ShamanRestoration, SpellFragileEchoesShaman);
                    break;
                case Class.Priest:
                    updateAuraIfInCorrectSpec(ChrSpecialization.PriestDiscipline, SpellFragileEchoesPriestDiscipline);
                    updateAuraIfInCorrectSpec(ChrSpecialization.PriestHoly, SpellFragileEchoesPriestHoly);
                    break;
                case Class.Paladin:
                    updateAuraIfInCorrectSpec(ChrSpecialization.PaladinHoly, SpellFragileEchoesPaladin);
                    break;
                case Class.Druid:
                    updateAuraIfInCorrectSpec(ChrSpecialization.DruidRestoration, SpellFragileEchoesDruid);
                    break;
                default:
                    break;
            }
        }

        public override void Register()
        {
            DoEffectCalcPeriodic.Add(new(ForcePeriodic, 0, AuraType.Dummy));
            OnEffectPeriodic.Add(new(UpdateSpecAura, 0, AuraType.Dummy));
        }
    }

    [Script] // 215267 - Fragile Echo
    class spell_item_amalgams_seventh_spine_mana_restore : AuraScript
    {
        const int SpellFragileEchoEnergize = 215270;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellFragileEchoEnergize);
        }

        void TriggerManaRestoration(AuraEffect aurEff, AuraEffectHandleModes mode)
        {
            if (GetTargetApplication().GetRemoveMode() != AuraRemoveMode.Expire)
                return;

            Unit caster = GetCaster();
            if (caster == null)
                return;

            AuraEffect trinketEffect = caster.GetAuraEffect(aurEff.GetSpellEffectInfo().TriggerSpell, 0);
            if (trinketEffect != null)
                caster.CastSpell(caster, SpellFragileEchoEnergize, new CastSpellExtraArgs(aurEff).AddSpellMod(SpellValueMod.BasePoint0, trinketEffect.GetAmount()));
        }

        public override void Register()
        {
            AfterEffectRemove.Add(new(TriggerManaRestoration, 1, AuraType.Dummy, AuraEffectHandleModes.Real));
        }
    }

    [Script] // 228445 - March of the Legion
    class spell_item_set_march_of_the_legion : AuraScript
    {
        bool IsDemon(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            return eventInfo.GetProcTarget() != null && eventInfo.GetProcTarget().GetCreatureType() == CreatureType.Demon;
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(IsDemon, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 234113 - Arrogance (used by item 142171 - Seal of Darkshire Nobility)
    class spell_item_seal_of_darkshire_nobility : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect((spellInfo.Id, 1)) && ValidateSpellInfo(spellInfo.GetEffect(1).TriggerSpell);
        }

        bool CheckCooldownAura(ProcEventInfo eventInfo)
        {
            return eventInfo.GetProcTarget() != null && !eventInfo.GetProcTarget().HasAura(GetEffectInfo(1).TriggerSpell, GetTarget().GetGUID());
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckCooldownAura));
        }
    }

    [Script] // 247625 - March of the Legion
    class spell_item_lightblood_elixir : AuraScript
    {
        bool IsDemon(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            return eventInfo.GetProcTarget() != null && eventInfo.GetProcTarget().GetCreatureType() == CreatureType.Demon;
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(IsDemon, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 253287 - Highfather's Timekeeping
    class spell_item_highfathers_machination : AuraScript
    {
        const int SpellHighfathersTimekeepingHeal = 253288;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellHighfathersTimekeepingHeal);
        }

        bool CheckHealth(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            return eventInfo.GetDamageInfo() != null && GetTarget().HealthBelowPctDamaged(aurEff.GetAmount(), eventInfo.GetDamageInfo().GetDamage());
        }

        void Heal(AuraEffect aurEff, ProcEventInfo procInfo)
        {
            PreventDefaultAction();
            Unit caster = GetCaster();
            if (caster != null)
                caster.CastSpell(GetTarget(), SpellHighfathersTimekeepingHeal, aurEff);
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(CheckHealth, 0, AuraType.Dummy));
            OnEffectProc.Add(new(Heal, 0, AuraType.Dummy));
        }
    }

    [Script] // 253323 - Shadow Strike
    class spell_item_seeping_scourgewing : AuraScript
    {
        const int SpellShadowStrikeAoeCheck = 255861;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellShadowStrikeAoeCheck);
        }

        void TriggerIsolatedStrikeCheck(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            GetTarget().CastSpell(eventInfo.GetProcTarget(), SpellShadowStrikeAoeCheck,
                new CastSpellExtraArgs(aurEff).SetTriggeringSpell(eventInfo.GetProcSpell()));
        }

        public override void Register()
        {
            AfterEffectProc.Add(new(TriggerIsolatedStrikeCheck, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 255861 - Shadow Strike
    class spell_item_seeping_scourgewing_aoe_check : SpellScript
    {
        const int SpellIsolatedStrike = 255609;

        void TriggerAdditionalDamage()
        {
            if (GetUnitTargetCountForEffect(0) > 1)
                return;

            CastSpellExtraArgs args = new(TriggerCastFlags.FullMask)
            {
                OriginalCastId = GetSpell().m_originalCastId
            };
            if (GetSpell().m_castItemLevel >= 0)
                args.OriginalCastItemLevel = GetSpell().m_castItemLevel;

            GetCaster().CastSpell(GetHitUnit(), SpellIsolatedStrike, args);
        }

        public override void Register()
        {
            AfterHit.Add(new(TriggerAdditionalDamage));
        }
    }

    [Script] // 295175 - Spiteful Binding
    class spell_item_grips_of_forsaken_sanity : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellEffect((spellInfo.Id, 1));
        }

        bool CheckHealth(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            return eventInfo.GetActor().GetHealthPct() >= GetEffectInfo(1).CalcValue();
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(CheckHealth, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script] // 302385 - Resurrect Health
    class spell_item_zanjir_scaleguard_greatcloak : AuraScript
    {
        bool CheckProc(AuraEffect aurEff, ProcEventInfo eventInfo)
        {
            return eventInfo.GetSpellInfo() != null && eventInfo.GetSpellInfo().HasEffect(SpellEffectName.Resurrect);
        }

        public override void Register()
        {
            DoCheckEffectProc.Add(new(CheckProc, 0, AuraType.ProcTriggerSpell));
        }
    }

    [Script("spell_item_shiver_venom_crossbow", 303559)]// 303358 Venomous Bolt
    [Script("spell_item_shiver_venom_lance", 303562)]// 303361 Shivering Lance
    class spell_item_shiver_venom_weapon_proc : AuraScript
    {
        const int SpellShiverVenom = 301624;

        int _additionalProcSpellId;

        public spell_item_shiver_venom_weapon_proc(int additionalProcSpellId)
        {
            _additionalProcSpellId = additionalProcSpellId;
        }

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellShiverVenom, _additionalProcSpellId);
        }

        void HandleAdditionalProc(AuraEffect aurEff, ProcEventInfo procInfo)
        {
            if (procInfo.GetProcTarget().HasAura(SpellShiverVenom))
                procInfo.GetActor().CastSpell(procInfo.GetProcTarget(), _additionalProcSpellId, new CastSpellExtraArgs(aurEff)
                    .AddSpellMod(SpellValueMod.BasePoint0, aurEff.GetAmount())
                    .SetTriggeringSpell(procInfo.GetProcSpell()));
        }

        public override void Register()
        {
            OnEffectProc.Add(new(HandleAdditionalProc, 1, AuraType.Dummy));
        }
    }

    [Script] // 302774 - Arcane Tempest
    class spell_item_phial_of_the_arcane_tempest_damage : SpellScript
    {
        void ModifyStacks()
        {
            if (GetUnitTargetCountForEffect(0) != 1 || GetTriggeringSpell() == null)
                return;

            AuraEffect aurEff = GetCaster().GetAuraEffect(GetTriggeringSpell().Id, 0);
            if (aurEff != null)
            {
                aurEff.GetBase().ModStackAmount(1, AuraRemoveMode.None, false);
                aurEff.CalculatePeriodic(GetCaster(), false);
            }
        }

        public override void Register()
        {
            AfterCast.Add(new(ModifyStacks));
        }
    }

    [Script] // 302769 - Arcane Tempest
    class spell_item_phial_of_the_arcane_tempest_periodic : AuraScript
    {
        void CalculatePeriod(AuraEffect aurEff, ref bool isPeriodic, ref int period)
        {
            period -= (GetStackAmount() - 1) * 300;
        }

        public override void Register()
        {
            DoEffectCalcPeriodic.Add(new(CalculatePeriod, 0, AuraType.PeriodicTriggerSpell));
        }
    }

    // 410530 - Mettle
    [Script] // 410964 - Mettle
    class spell_item_infurious_crafted_gear_mettle : AuraScript
    {
        int SpellMettleCooldown = 410532;

        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellMettleCooldown);
        }

        bool CheckProc(ProcEventInfo eventInfo)
        {
            if (GetTarget().HasAura(SpellMettleCooldown))
                return false;

            if (eventInfo.GetHitMask().HasAnyFlag(ProcFlagsHit.Interrupt | ProcFlagsHit.Dispel))
                return true;

            Spell procSpell = eventInfo.GetProcSpell();
            if (procSpell == null)
                return false;

            bool isCrowdControl = procSpell.GetSpellInfo().HasAura(AuraType.ModConfuse)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModFear)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModStun)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModPacify)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModRoot)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModSilence)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModPacifySilence)
                || procSpell.GetSpellInfo().HasAura(AuraType.ModRoot2);

            if (!isCrowdControl)
                return false;

            return eventInfo.GetActionTarget().HasAura(aura => aura.GetCastId() == procSpell.m_castId);
        }

        void TriggerCooldown(ProcEventInfo eventInfo)
        {
            GetTarget().CastSpell(GetTarget(), SpellMettleCooldown, true);
        }

        public override void Register()
        {
            DoCheckProc.Add(new(CheckProc));
            AfterProc.Add(new(TriggerCooldown));
        }
    }
}
