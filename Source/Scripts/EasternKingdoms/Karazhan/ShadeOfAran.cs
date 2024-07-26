// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Scripts.EasternKingdoms.Karazhan.ShadeOfAran
{
    struct SpellIds
    {
        public const int Frostbolt = 29954;
        public const int Fireball = 29953;
        public const int Arcmissle = 29955;
        public const int Chainsofice = 29991;
        public const int Dragonsbreath = 29964;
        public const int Massslow = 30035;
        public const int FlameWreath = 29946;
        public const int AoeCs = 29961;
        public const int Playerpull = 32265;
        public const int Aexplosion = 29973;
        public const int MassPoly = 29963;
        public const int BlinkCenter = 29967;
        public const int Elementals = 29962;
        public const int Conjure = 29975;
        public const int Drink = 30024;
        public const int Potion = 32453;
        public const int AoePyroblast = 29978;

        public const int CircularBlizzard = 29951;
        public const int Waterbolt = 31012;
        public const int ShadowPyro = 29978;
    }

    struct CreatureIds
    {
        public const int WaterElemental = 17167;
        public const int ShadowOfAran = 18254;
        public const int AranBlizzard = 17161;
    }

    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SayFlamewreath = 1;
        public const int SayBlizzard = 2;
        public const int SayExplosion = 3;
        public const int SayDrink = 4;
        public const int SayElementals = 5;
        public const int SayKill = 6;
        public const int SayTimeover = 7;
        public const int SayDeath = 8;
        public const int SayAtiesh = 9;
    }

    enum SuperSpell
    {
        Flame = 0,
        Blizzard,
        Ae,
    }

    [Script]
    class boss_aran : ScriptedAI
    {
        static int[] AtieshStaves =
        [
            22589, //ItemAtieshMage,
            22630, //ItemAtieshWarlock,
            22631, //ItemAtieshPriest,
            22632 //ItemAtieshDruid,
        ];

        InstanceScript instance;

        TimeSpan SecondarySpellTimer;
        TimeSpan NormalCastTimer;
        TimeSpan SuperCastTimer;
        TimeSpan BerserkTimer;
        TimeSpan CloseDoorTimer;    // Don't close the door right on aggro in case some people are still entering.

        SuperSpell LastSuperSpell;

        TimeSpan FlameWreathTimer;
        TimeSpan FlameWreathCheckTime;
        ObjectGuid[] FlameWreathTarget = new ObjectGuid[3];
        float[] FWTargPosX = new float[3];
        float[] FWTargPosY = new float[3];

        int CurrentNormalSpell;
        TimeSpan ArcaneCooldown;
        TimeSpan FireCooldown;
        TimeSpan FrostCooldown;

        TimeSpan DrinkInterruptTimer;

        bool ElementalsSpawned;
        bool Drinking;
        bool DrinkInturrupted;
        bool SeenAtiesh;

        public boss_aran(Creature creature) : base(creature)
        {
            Initialize();
            instance = creature.GetInstanceScript();
        }

        void Initialize()
        {
            SecondarySpellTimer = (Milliseconds)5000;
            NormalCastTimer = (Milliseconds)0;
            SuperCastTimer = (Milliseconds)35000;
            BerserkTimer = (Milliseconds)720000;
            CloseDoorTimer = (Milliseconds)15000;

            LastSuperSpell = (SuperSpell)(RandomHelper.Rand32() % 3);

            FlameWreathTimer = (Milliseconds)0;
            FlameWreathCheckTime = (Milliseconds)0;

            CurrentNormalSpell = (Milliseconds)0;
            ArcaneCooldown = (Milliseconds)0;
            FireCooldown = (Milliseconds)0;
            FrostCooldown = (Milliseconds)0;

            DrinkInterruptTimer = (Milliseconds)10000;

            ElementalsSpawned = false;
            Drinking = false;
            DrinkInturrupted = false;
        }

        public override void Reset()
        {
            Initialize();
            me.SetCanMelee(true);

            // Not in progress
            instance.SetBossState(DataTypes.Aran, EncounterState.NotStarted);
            instance.HandleGameObject(instance.GetGuidData(DataTypes.GoLibraryDoor), true);
        }

        public override void KilledUnit(Unit victim)
        {
            Talk(TextIds.SayKill);
        }

        public override void JustDied(Unit killer)
        {
            Talk(TextIds.SayDeath);

            instance.SetBossState(DataTypes.Aran, EncounterState.Done);
            instance.HandleGameObject(instance.GetGuidData(DataTypes.GoLibraryDoor), true);
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayAggro);

            instance.SetBossState(DataTypes.Aran, EncounterState.InProgress);
            instance.HandleGameObject(instance.GetGuidData(DataTypes.GoLibraryDoor), false);
        }

        void FlameWreathEffect()
        {
            List<Unit> targets = new();
            //store the threat list in a different container
            foreach (var refe in me.GetThreatManager().GetSortedThreatList())
            {
                Unit target = refe.GetVictim();
                if (refe.GetVictim().IsPlayer() && refe.GetVictim().IsAlive())
                    targets.Add(target);
            }

            //cut down to size if we have more than 3 targets
            targets.RandomResize(3);

            uint i = 0;
            foreach (var unit in targets)
            {
                if (unit != null)
                {
                    FlameWreathTarget[i] = unit.GetGUID();
                    FWTargPosX[i] = unit.GetPositionX();
                    FWTargPosY[i] = unit.GetPositionY();
                    DoCast(unit, SpellIds.FlameWreath, new CastSpellExtraArgs(true));
                    ++i;
                }
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            if (CloseDoorTimer != default)
            {
                if (CloseDoorTimer <= diff)
                {
                    instance.HandleGameObject(instance.GetGuidData(DataTypes.GoLibraryDoor), false);
                    CloseDoorTimer = default;
                }
                else CloseDoorTimer -= diff;
            }

            //Cooldowns for casts
            if (ArcaneCooldown != default)
            {
                if (ArcaneCooldown >= diff)
                    ArcaneCooldown -= diff;
                else ArcaneCooldown = default;
            }

            if (FireCooldown != default)
            {
                if (FireCooldown >= diff)
                    FireCooldown -= diff;
                else FireCooldown = default;
            }

            if (FrostCooldown != default)
            {
                if (FrostCooldown >= diff)
                    FrostCooldown -= diff;
                else FrostCooldown = default;
            }

            if (!Drinking && me.GetMaxPower(PowerType.Mana) != 0 && me.GetPowerPct(PowerType.Mana) < 20.0f)
            {
                Drinking = true;
                me.InterruptNonMeleeSpells(false);

                Talk(TextIds.SayDrink);

                if (!DrinkInturrupted)
                {
                    DoCast(me, SpellIds.MassPoly, new CastSpellExtraArgs(true));
                    DoCast(me, SpellIds.Conjure, new CastSpellExtraArgs(false));
                    DoCast(me, SpellIds.Drink, new CastSpellExtraArgs(false));
                    me.SetStandState(UnitStandStateType.Sit);
                    DrinkInterruptTimer = (Milliseconds)10000;
                }
            }

            //Drink Interrupt
            if (Drinking && DrinkInturrupted)
            {
                Drinking = false;
                me.RemoveAurasDueToSpell(SpellIds.Drink);
                me.SetStandState(UnitStandStateType.Stand);
                me.SetPower(PowerType.Mana, me.GetMaxPower(PowerType.Mana) - 32000);
                DoCast(me, SpellIds.Potion, new CastSpellExtraArgs(false));
            }

            //Drink Interrupt Timer
            if (Drinking && !DrinkInturrupted)
            {
                if (DrinkInterruptTimer >= diff)
                    DrinkInterruptTimer -= diff;
                else
                {
                    me.SetStandState(UnitStandStateType.Stand);
                    DoCast(me, SpellIds.Potion, new CastSpellExtraArgs(true));
                    DoCast(me, SpellIds.AoePyroblast, new CastSpellExtraArgs(false));
                    DrinkInturrupted = true;
                    Drinking = false;
                }
            }

            //Don't execute any more code if we are drinking
            if (Drinking)
                return;

            //Normal casts
            if (NormalCastTimer <= diff)
            {
                if (!me.IsNonMeleeSpellCast(false))
                {
                    Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                    if (target == null)
                        return;

                    int[] Spells = new int[3];
                    byte AvailableSpells = 0;

                    //Check for what spells are not on cooldown
                    if (ArcaneCooldown == default)
                    {
                        Spells[AvailableSpells] = SpellIds.Arcmissle;
                        ++AvailableSpells;
                    }
                    if (FireCooldown == default)
                    {
                        Spells[AvailableSpells] = SpellIds.Fireball;
                        ++AvailableSpells;
                    }
                    if (FrostCooldown == default)
                    {
                        Spells[AvailableSpells] = SpellIds.Frostbolt;
                        ++AvailableSpells;
                    }

                    //If no available spells wait 1 second and try again
                    if (AvailableSpells != 0)
                    {
                        CurrentNormalSpell = Spells[RandomHelper.Rand32() % AvailableSpells];
                        DoCast(target, CurrentNormalSpell);
                    }
                }
                NormalCastTimer = (Milliseconds)1000;
            }
            else NormalCastTimer -= diff;

            if (SecondarySpellTimer <= diff)
            {
                switch (RandomHelper.URand(0, 1))
                {
                    case 0:
                        DoCast(me, SpellIds.AoeCs);
                        break;
                    case 1:
                        Unit target = SelectTarget(SelectTargetMethod.Random, 0, 100, true);
                        if (target != null)
                            DoCast(target, SpellIds.Chainsofice);
                        break;
                }
                SecondarySpellTimer = (Milliseconds)RandomHelper.IRand(5000, 20000);
            }
            else SecondarySpellTimer -= diff;

            if (SuperCastTimer <= diff)
            {
                SuperSpell[] Available = new SuperSpell[2];

                switch (LastSuperSpell)
                {
                    case SuperSpell.Ae:
                        Available[0] = SuperSpell.Flame;
                        Available[1] = SuperSpell.Blizzard;
                        break;
                    case SuperSpell.Flame:
                        Available[0] = SuperSpell.Ae;
                        Available[1] = SuperSpell.Blizzard;
                        break;
                    case SuperSpell.Blizzard:
                        Available[0] = SuperSpell.Flame;
                        Available[1] = SuperSpell.Ae;
                        break;
                    default:
                        Available[0] = 0;
                        Available[1] = 0;
                        break;
                }

                LastSuperSpell = Available[RandomHelper.URand(0, 1)];

                switch (LastSuperSpell)
                {
                    case SuperSpell.Ae:
                        Talk(TextIds.SayExplosion);

                        DoCast(me, SpellIds.BlinkCenter, new CastSpellExtraArgs(true));
                        DoCast(me, SpellIds.Playerpull, new CastSpellExtraArgs(true));
                        DoCast(me, SpellIds.Massslow, new CastSpellExtraArgs(true));
                        DoCast(me, SpellIds.Aexplosion, new CastSpellExtraArgs(false));
                        break;

                    case SuperSpell.Flame:
                        Talk(TextIds.SayFlamewreath);

                        FlameWreathTimer = (Milliseconds)20000;
                        FlameWreathCheckTime = (Milliseconds)500;

                        FlameWreathTarget[0].Clear();
                        FlameWreathTarget[1].Clear();
                        FlameWreathTarget[2].Clear();

                        FlameWreathEffect();
                        break;

                    case SuperSpell.Blizzard:
                        Talk(TextIds.SayBlizzard);

                        Creature pSpawn = me.SummonCreature(CreatureIds.AranBlizzard, 0.0f, 0.0f, 0.0f, 0.0f, TempSummonType.TimedDespawn, (Seconds)25);
                        if (pSpawn != null)
                        {
                            pSpawn.SetFaction(me.GetFaction());
                            pSpawn.CastSpell(pSpawn, SpellIds.CircularBlizzard, false);
                        }
                        break;
                }

                SuperCastTimer = (Milliseconds)RandomHelper.IRand(35000, 40000);
            }
            else SuperCastTimer -= diff;

            if (!ElementalsSpawned && HealthBelowPct(40))
            {
                ElementalsSpawned = true;

                for (uint i = 0; i < 4; ++i)
                {
                    Creature unit = me.SummonCreature(
                        CreatureIds.WaterElemental, 0.0f, 0.0f, 0.0f, 0.0f, 
                        TempSummonType.TimedDespawn, (Seconds)90);

                    if (unit != null)
                    {
                        unit.Attack(me.GetVictim(), true);
                        unit.SetFaction(me.GetFaction());
                    }
                }

                Talk(TextIds.SayElementals);
            }

            if (BerserkTimer <= diff)
            {
                for (uint i = 0; i < 5; ++i)
                {
                    Creature unit = me.SummonCreature(
                        CreatureIds.ShadowOfAran, 0.0f, 0.0f, 0.0f, 0.0f, 
                        TempSummonType.TimedDespawnOutOfCombat, (Seconds)5);

                    if (unit != null)
                    {
                        unit.Attack(me.GetVictim(), true);
                        unit.SetFaction(me.GetFaction());
                    }
                }

                Talk(TextIds.SayTimeover);

                BerserkTimer = (Milliseconds)60000;
            }
            else BerserkTimer -= diff;

            //Flame Wreath check
            if (FlameWreathTimer != default)
            {
                if (FlameWreathTimer >= diff)
                    FlameWreathTimer -= diff;
                else FlameWreathTimer = default;

                if (FlameWreathCheckTime <= diff)
                {
                    for (byte i = 0; i < 3; ++i)
                    {
                        if (FlameWreathTarget[i].IsEmpty())
                            continue;

                        Unit unit = Global.ObjAccessor.GetUnit(me, FlameWreathTarget[i]);
                        if (unit != null && !unit.IsWithinDist2d(FWTargPosX[i], FWTargPosY[i], 3))
                        {
                            unit.CastSpell(unit, 20476, new CastSpellExtraArgs(TriggerCastFlags.FullMask)
                                .SetOriginalCaster(me.GetGUID()));
                            unit.CastSpell(unit, 11027, true);
                            FlameWreathTarget[i].Clear();
                        }
                    }
                    FlameWreathCheckTime = (Milliseconds)500;
                }
                else FlameWreathCheckTime -= diff;
            }

            me.SetCanMelee(ArcaneCooldown != default && FireCooldown != default && FrostCooldown != default);
        }

        public override void DamageTaken(Unit pAttacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (!DrinkInturrupted && Drinking && damage != 0)
                DrinkInturrupted = true;
        }

        public override void SpellHit(WorldObject caster, SpellInfo spellInfo)
        {
            //We only care about interrupt effects and only if they are durring a spell currently being cast
            if (!spellInfo.HasEffect(SpellEffectName.InterruptCast) || !me.IsNonMeleeSpellCast(false))
                return;

            //Interrupt effect
            me.InterruptNonMeleeSpells(false);

            //Normally we would set the cooldown equal to the spell duration
            //but we do not have access to the DurationStore

            switch (CurrentNormalSpell)
            {
                case SpellIds.Arcmissle: ArcaneCooldown = (Milliseconds)5000; break;
                case SpellIds.Fireball: FireCooldown = (Milliseconds)5000; break;
                case SpellIds.Frostbolt: FrostCooldown = (Milliseconds)5000; break;
            }
        }

        public override void MoveInLineOfSight(Unit who)
        {
            base.MoveInLineOfSight(who);

            if (SeenAtiesh || me.IsInCombat() || me.GetDistance2d(who) > me.GetAttackDistance(who) + 10.0f)
                return;

            Player player = who.ToPlayer();
            if (player == null)
                return;

            foreach (uint id in AtieshStaves)
            {
                if (!PlayerHasWeaponEquipped(player, id))
                    continue;

                SeenAtiesh = true;
                Talk(TextIds.SayAtiesh);
                me.SetFacingTo(me.GetAbsoluteAngle(player));
                me.ClearUnitState(UnitState.Moving);
                me.GetMotionMaster().MoveDistract((Milliseconds)7000, me.GetAbsoluteAngle(who));
                break;
            }
        }

        bool PlayerHasWeaponEquipped(Player player, uint itemEntry)
        {
            Item item = player.GetItemByPos(EquipmentSlot.MainHand);
            if (item != null && item.GetEntry() == itemEntry)
                return true;

            return false;
        }
    }

    [Script]
    class water_elemental : ScriptedAI
    {
        public water_elemental(Creature creature) : base(creature) { }

        public override void Reset()
        {
            _scheduler.Schedule(Time.SpanFromMilliseconds(2000 + (RandomHelper.Rand32() % 3000)), task =>
            {
                DoCastVictim(SpellIds.Waterbolt);
                task.Repeat(Time.SpanFromSeconds(2), Time.SpanFromSeconds(5));
            });
        }

        public override void JustEngagedWith(Unit who) { }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }
}