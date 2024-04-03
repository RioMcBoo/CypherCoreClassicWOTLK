// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System.Collections.Generic;
using Game.AI;
using Game.Scripting;
using Game.Spells;
using System;
using static Global;

namespace Scripts.Events.ZalazaneFall
{
    struct TextIds
    {
        // Tiger Matriarch Credit
        public const int SayMatriarchAggro = 0;

        // Troll Volunteer
        public const int SayVolunteerStart = 0;
        public const int SayVolunteerEnd = 1;
    }

    struct SpellIds
    {
        // Tiger Matriarch Credit
        public const int SummonMatriarch = 75187;
        public const int NoSummonAura = 75213;
        public const int DetectInvis = 75180;
        public const int SummonZentabraTrigger = 75212;

        // Tiger Matriarch
        public const int Pounce = 61184;
        public const int FuriousBite = 75164;
        public const int SummonZentabra = 75181;
        public const int SpiritOfTheTigerRider = 75166;
        public const int EjectPassengers = 50630;

        // Troll Volunteer
        public const int VolunteerAura = 75076;
        public const int PetactAura = 74071;
        public const int QuestCredit = 75106;
        public const int MountingCheck = 75420;
        public const int Turnin = 73953;
        public const int AoeTurnin = 75107;

        // Vol'jin War Drums
        public const int Motivate1 = 75088;
        public const int Motivate2 = 75086;
    }

    struct CreatureIds
    {
        // Tiger Matriarch Credit
        public const int TigerVehicle = 40305;

        // Troll Volunteer
        public const int Uruzin = 40253;
        public const int Volunteer1 = 40264;
        public const int Volunteer2 = 40260;

        // Vol'jin War Drums
        public const int Citizen1 = 40256;
        public const int Citizen2 = 40257;
    }

    struct Misc
    {
        public const int PointUruzin = 4026400;
    }

    [Script]
    class npc_tiger_matriarch_credit : ScriptedAI
    {
        public npc_tiger_matriarch_credit(Creature creature) : base(creature)
        {
            SetCombatMovement(false);
            _scheduler.Schedule(TimeSpan.FromSeconds(2), task =>
            {
                List<Creature> tigers = me.GetCreatureListWithEntryInGrid(CreatureIds.TigerVehicle, 15.0f);
                if (!tigers.Empty())
                {
                    foreach (var creature in tigers)
                    {
                        if (!creature.IsSummon())
                            continue;

                        Unit summoner = creature.ToTempSummon().GetSummonerUnit();
                        if (summoner != null && !summoner.HasAura(SpellIds.NoSummonAura) && !summoner.HasAura(SpellIds.SummonZentabraTrigger) && !summoner.IsInCombat())
                        {
                            me.AddAura(SpellIds.NoSummonAura, summoner);
                            me.AddAura(SpellIds.DetectInvis, summoner);
                            summoner.CastSpell(summoner, SpellIds.SummonMatriarch, true);
                            Talk(TextIds.SayMatriarchAggro, summoner);
                        }
                    }
                }

                task.Repeat(TimeSpan.FromSeconds(5));
            });
        }

        public override void UpdateAI(uint diff)
        {
            _scheduler.Update(diff);
        }
    }

    [Script]
    class npc_tiger_matriarch : ScriptedAI
    {
        ObjectGuid _tigerGuid;

        public npc_tiger_matriarch(Creature creature) : base(creature) { }

        public override void JustEngagedWith(Unit target)
        {
            _scheduler.CancelAll();
            _scheduler.Schedule(TimeSpan.FromMilliseconds(100), task =>
            {
                DoCastVictim(SpellIds.Pounce);
                task.Repeat(TimeSpan.FromSeconds(30));
            });

            _scheduler.Schedule(TimeSpan.FromSeconds(50), task =>
            {
                Unit tiger = ObjAccessor.GetUnit(me, _tigerGuid);
                if (tiger != null)
                {
                    if (tiger.IsSummon())
                    {
                        Unit vehSummoner = tiger.ToTempSummon().GetSummonerUnit();
                        if (vehSummoner != null)
                            me.AddAura(SpellIds.NoSummonAura, vehSummoner);
                    }
                }
                task.Repeat();
            });
        }

        public override void IsSummonedBy(WorldObject summonerWO)
        {
            Player summoner = summonerWO.ToPlayer();
            if (summoner == null || summoner.GetVehicle() == null)
                return;

            _tigerGuid = summoner.GetVehicle().GetBase().GetGUID();
            Unit tiger = ObjAccessor.GetUnit(me, _tigerGuid);
            if (tiger != null)
            {
                AddThreat(tiger, 500000.0f);
                DoCast(me, SpellIds.FuriousBite);
            }
        }

        public override void KilledUnit(Unit victim)
        {
            if (victim.GetTypeId() != TypeId.Unit || !victim.IsSummon())
                return;

            Unit vehSummoner = victim.ToTempSummon().GetSummonerUnit();
            if (vehSummoner != null)
            {
                vehSummoner.RemoveAurasDueToSpell(SpellIds.NoSummonAura);
                vehSummoner.RemoveAurasDueToSpell(SpellIds.DetectInvis);
                vehSummoner.RemoveAurasDueToSpell(SpellIds.SpiritOfTheTigerRider);
                vehSummoner.RemoveAurasDueToSpell(SpellIds.SummonZentabraTrigger);
            }
            me.DespawnOrUnsummon();
        }

        public override void DamageTaken(Unit attacker, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            if (attacker == null || !attacker.IsSummon())
                return;

            if (HealthBelowPct(20))
            {
                damage = 0;
                me.SetUnitFlag(UnitFlags.NonAttackable);
                Unit vehSummoner = attacker.ToTempSummon().GetSummonerUnit();
                if (vehSummoner != null)
                {
                    vehSummoner.AddAura(SpellIds.SummonZentabraTrigger, vehSummoner);
                    vehSummoner.CastSpell(vehSummoner, SpellIds.SummonZentabra, true);
                    attacker.CastSpell(attacker, SpellIds.EjectPassengers, true);
                    vehSummoner.RemoveAurasDueToSpell(SpellIds.NoSummonAura);
                    vehSummoner.RemoveAurasDueToSpell(SpellIds.DetectInvis);
                    vehSummoner.RemoveAurasDueToSpell(SpellIds.SpiritOfTheTigerRider);
                    vehSummoner.RemoveAurasDueToSpell(SpellIds.SummonZentabraTrigger);
                }

                me.DespawnOrUnsummon();
            }
        }

        public override void UpdateAI(uint diff)
        {
            if (!UpdateVictim())
                return;

            if (_tigerGuid.IsEmpty())
                return;

            _scheduler.Update(diff);
        }
    }

    [Script]
    class npc_troll_volunteer : ScriptedAI
    {
        // These models was found in sniff.
        // @todo generalize these models with race from dbc
        int[] trollmodel =
        {
            11665, 11734, 11750, 12037, 12038, 12042, 12049, 12849, 13529, 14759, 15570, 15701,
            15702, 1882, 1897, 1976, 2025, 27286, 2734, 2735, 4084, 4085, 4087, 4089, 4231, 4357,
            4358, 4360, 4361, 4362, 4363, 4370, 4532, 4537, 4540, 4610, 6839, 7037, 9767, 9768
        };

        int _mountModel;
        bool _complete;

        public npc_troll_volunteer(Creature creature) : base(creature)
        {
            Initialize();
            _mountModel = 0;
        }

        void Initialize()
        {
            _complete = false;
        }

        public override void InitializeAI()
        {
            if (me.IsDead() || me.GetOwner() == null)
                return;

            Reset();

            switch (RandomHelper.URand(0, 3))
            {
                case 0:
                    _mountModel = 6471;
                    break;
                case 1:
                    _mountModel = 6473;
                    break;
                case 2:
                    _mountModel = 6469;
                    break;
                default:
                    _mountModel = 6472;
                    break;
            }
            me.SetDisplayId(trollmodel[RandomHelper.IRand(0, 39)]);
            Player player = me.GetOwner().ToPlayer();
            if (player != null)
                me.GetMotionMaster().MoveFollow(player, 5.0f, (RandomHelper.NextSingle() + 1.0f) * (MathF.PI) / 3.0f * 4.0f);
        }

        public override void Reset()
        {
            Initialize();
            me.AddAura(SpellIds.VolunteerAura, me);
            me.AddAura(SpellIds.MountingCheck, me);
            DoCast(me, SpellIds.PetactAura);
            me.SetReactState(ReactStates.Passive);
            Talk(TextIds.SayVolunteerStart);
        }

        // This is needed for mount check aura to know what mountmodel the npc got stored
        public int GetMountId()
        {
            return _mountModel;
        }

        public override void MovementInform(MovementGeneratorType type, int id)
        {
            if (type != MovementGeneratorType.Point)
                return;
            if (id == Misc.PointUruzin)
                me.DespawnOrUnsummon();
        }

        public override void SpellHit(WorldObject caster, SpellInfo spellInfo)
        {
            if (spellInfo.Id == SpellIds.AoeTurnin && caster.GetEntry() == CreatureIds.Uruzin && !_complete)
            {
                _complete = true;    // Preventing from giving credit twice
                DoCast(me, SpellIds.Turnin);
                DoCast(me, SpellIds.QuestCredit);
                me.RemoveAurasDueToSpell(SpellIds.MountingCheck);
                me.Dismount();
                Talk(TextIds.SayVolunteerEnd);
                me.GetMotionMaster().MovePoint(Misc.PointUruzin, caster.GetPositionX(), caster.GetPositionY(), caster.GetPositionZ());
            }
        }
    }

    [Script] // 75420 - Mounting Check
    class spell_mount_check : AuraScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.MountingCheck);
        }

        void HandleEffectPeriodic(AuraEffect aurEff)
        {
            Unit target = GetTarget();
            Unit owner = target.GetOwner();

            if (owner == null)
                return;

            if (owner.IsMounted() && !target.IsMounted())
            {
                npc_troll_volunteer volunteerAI = target.GetAI<npc_troll_volunteer>();
                if (volunteerAI != null)
                    target.Mount(volunteerAI.GetMountId());
            }
            else if (!owner.IsMounted() && target.IsMounted())
                target.Dismount();

            target.SetSpeedRate(UnitMoveType.Run, owner.GetSpeedRate(UnitMoveType.Run));
            target.SetSpeedRate(UnitMoveType.Walk, owner.GetSpeedRate(UnitMoveType.Walk));
        }

        public override void Register()
        {
            OnEffectPeriodic.Add(new(HandleEffectPeriodic, 0, AuraType.PeriodicDummy));
        }
    }

    [Script] // 75102 - Vol'jin's War Drums
    class spell_voljin_war_drums : SpellScript
    {
        public override bool Validate(SpellInfo spellInfo)
        {
            return ValidateSpellInfo(SpellIds.Motivate1, SpellIds.Motivate2);
        }

        void HandleDummy(int effIndex)
        {
            Unit caster = GetCaster();
            Unit target = GetHitUnit();
            if (target != null)
            {
                int motivate = 0;
                if (target.GetEntry() == CreatureIds.Citizen1)
                    motivate = SpellIds.Motivate1;
                else if (target.GetEntry() == CreatureIds.Citizen2)
                    motivate = SpellIds.Motivate2;
                if (motivate != 0)
                    caster.CastSpell(target, motivate, false);
            }
        }

        public override void Register()
        {
            OnEffectHitTarget.Add(new(HandleDummy, 0, SpellEffectName.Dummy));
        }
    }
}