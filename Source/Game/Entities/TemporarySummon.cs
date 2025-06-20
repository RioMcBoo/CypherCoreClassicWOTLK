﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Dynamic;
using Game.DataStorage;
using Game.Maps;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public class TempSummon : Creature
    {
        public TempSummon(SummonPropertiesRecord properties, WorldObject owner, bool isWorldObject) : base(isWorldObject)
        {
            m_Properties = properties;
            m_type = TempSummonType.ManualDespawn;

            m_summonerGUID = owner != null ? owner.GetGUID() : ObjectGuid.Empty;
            UnitTypeMask |= UnitTypeMask.Summon;
            m_canFollowOwner = true;
        }

        public WorldObject GetSummoner()
        {
            return !m_summonerGUID.IsEmpty() ? Global.ObjAccessor.GetWorldObject(this, m_summonerGUID) : null;
        }

        public Unit GetSummonerUnit()
        {
            WorldObject summoner = GetSummoner();
            if (summoner != null)
                return summoner.ToUnit();

            return null;
        }

        public Creature GetSummonerCreatureBase()
        {
            return !m_summonerGUID.IsEmpty() ? ObjectAccessor.GetCreature(this, m_summonerGUID) : null;
        }

        public GameObject GetSummonerGameObject()
        {
            WorldObject summoner = GetSummoner();
            if (summoner != null)
                return summoner.ToGameObject();

            return null;
        }

        public override void Update(TimeSpan diff)
        {
            base.Update(diff);

            if (m_deathState == DeathState.Dead)
            {
                UnSummon();
                return;
            }
            
            switch (m_type)
            {
                case TempSummonType.ManualDespawn:
                case TempSummonType.DeadDespawn:
                    break;
                case TempSummonType.TimedDespawn:
                {
                    if (m_timer <= diff)
                    {
                        UnSummon();
                        return;
                    }

                    m_timer -= diff;
                    break;
                }
                case TempSummonType.TimedDespawnOutOfCombat:
                {
                    if (!IsInCombat())
                    {
                        if (m_timer <= diff)
                        {
                            UnSummon();
                            return;
                        }

                        m_timer -= diff;
                    }
                    else if (m_timer != m_lifetime)
                        m_timer = m_lifetime;

                    break;
                }

                case TempSummonType.CorpseTimedDespawn:
                {
                    if (m_deathState == DeathState.Corpse)
                    {
                        if (m_timer <= diff)
                        {
                            UnSummon();
                            return;
                        }

                        m_timer -= diff;
                    }
                    break;
                }
                case TempSummonType.CorpseDespawn:
                {
                    // if m_deathState is DEAD, CORPSE was skipped
                    if (m_deathState == DeathState.Corpse)
                    {
                        UnSummon();
                        return;
                    }

                    break;
                }
                case TempSummonType.TimedOrCorpseDespawn:
                {
                    if (m_deathState == DeathState.Corpse)
                    {
                        UnSummon();
                        return;
                    }

                    if (!IsInCombat())
                    {
                        if (m_timer <= diff)
                        {
                            UnSummon();
                            return;
                        }
                        else
                            m_timer -= diff;
                    }
                    else if (m_timer != m_lifetime)
                        m_timer = m_lifetime;
                    break;
                }
                case TempSummonType.TimedOrDeadDespawn:
                {
                    if (!IsInCombat() && IsAlive())
                    {
                        if (m_timer <= diff)
                        {
                            UnSummon();
                            return;
                        }
                        else
                            m_timer -= diff;
                    }
                    else if (m_timer != m_lifetime)
                        m_timer = m_lifetime;
                    break;
                }
                default:
                    UnSummon();
                    Log.outError(LogFilter.Unit, 
                        $"Temporary summoned creature (entry: {GetEntry()}) " +
                        $"have unknown Type {m_type} of ");
                    break;
            }
        }

        public virtual void InitStats(WorldObject summoner, TimeSpan duration)
        {
            Cypher.Assert(!IsPet());

            m_timer = duration;
            m_lifetime = duration;

            if (m_type == TempSummonType.ManualDespawn)
                m_type = (duration <= TimeSpan.Zero) ? TempSummonType.DeadDespawn : TempSummonType.TimedDespawn;

            if (summoner != null && summoner.IsPlayer())
            {
                if (IsTrigger() && m_spells[0] != 0)
                    m_ControlledByPlayer = true;
            
                CreatureSummonedData summonedData = Global.ObjectMgr.GetCreatureSummonedData(GetEntry());
                if (summonedData != null)
                {
                    m_creatureIdVisibleToSummoner = summonedData.CreatureIDVisibleToSummoner;
                    if (summonedData.CreatureIDVisibleToSummoner.HasValue)
                    {
                        CreatureTemplate creatureTemplateVisibleToSummoner = Global.ObjectMgr.GetCreatureTemplate(summonedData.CreatureIDVisibleToSummoner.Value);
                        m_displayIdVisibleToSummoner = ObjectManager.ChooseDisplayId(creatureTemplateVisibleToSummoner, null).CreatureDisplayID;
                    }
                }
            }

            if (m_Properties == null)
                return;

            Unit unitSummoner = summoner?.ToUnit();
            if (unitSummoner != null)
            {
                int slot = m_Properties.Slot;
                if (slot == (int)SummonSlot.Any)
                    slot = FindUsableTotemSlot(unitSummoner);

                if (slot != 0)
                {
                    if (!unitSummoner.m_SummonSlot[slot].IsEmpty() && unitSummoner.m_SummonSlot[slot] != GetGUID())
                    {
                        Creature oldSummon = GetMap().GetCreature(unitSummoner.m_SummonSlot[slot]);
                        if (oldSummon != null && oldSummon.IsSummon())
                            oldSummon.ToTempSummon().UnSummon();
                    }
                    unitSummoner.m_SummonSlot[slot] = GetGUID();
                }

                if (!m_Properties.HasFlag(SummonPropertiesFlags.UseCreatureLevel))
                    SetLevel(unitSummoner.GetLevel());
            }

            int faction = m_Properties.Faction;
            if (summoner != null && m_Properties.HasFlag(SummonPropertiesFlags.UseSummonerFaction)) // TODO: Determine priority between faction and flag
                faction = summoner.GetFaction();

            if (faction != 0)
                SetFaction(faction);

            if (m_Properties.HasFlag(SummonPropertiesFlags.SummonFromBattlePetJournal))
                RemoveNpcFlag(NPCFlags1.WildBattlePet);
        }

        public virtual void InitSummon(WorldObject summoner)
        {
            if (summoner != null)
            {
                if (summoner.IsCreature())
                    summoner.ToCreature().GetAI()?.JustSummoned(this);
                else if (summoner.IsGameObject())
                    summoner.ToGameObject().GetAI()?.JustSummoned(this);

                if (IsAIEnabled())
                    GetAI().IsSummonedBy(summoner);
            }
        }

        public override void UpdateObjectVisibilityOnCreate()
        {
            List<WorldObject> objectsToUpdate = new();
            objectsToUpdate.Add(this);

            SmoothPhasing smoothPhasing = GetSmoothPhasing();
            if (smoothPhasing != null)
            {
                SmoothPhasingInfo infoForSeer = smoothPhasing.GetInfoForSeer(GetDemonCreatorGUID());
                if (infoForSeer != null && infoForSeer.ReplaceObject.HasValue && smoothPhasing.IsReplacing(infoForSeer.ReplaceObject.Value))
                {
                    WorldObject original = Global.ObjAccessor.GetWorldObject(this, infoForSeer.ReplaceObject.Value);
                    if (original != null)
                        objectsToUpdate.Add(original);
                }
            }

            VisibleChangesNotifier notifier = new(objectsToUpdate);
            Cell.VisitWorldObjects(this, notifier, GetVisibilityRange());
        }

        public override void UpdateObjectVisibilityOnDestroy()
        {
            List<WorldObject> objectsToUpdate = new();
            objectsToUpdate.Add(this);

            WorldObject original = null;
            SmoothPhasing smoothPhasing = GetSmoothPhasing();
            if (smoothPhasing != null)
            {
                SmoothPhasingInfo infoForSeer = smoothPhasing.GetInfoForSeer(GetDemonCreatorGUID());
                if (infoForSeer != null && infoForSeer.ReplaceObject.HasValue && smoothPhasing.IsReplacing(infoForSeer.ReplaceObject.Value))
                    original = Global.ObjAccessor.GetWorldObject(this, infoForSeer.ReplaceObject.Value);

                if (original != null)
                {
                    objectsToUpdate.Add(original);

                    // disable replacement without removing - it is still needed for next step (visibility update)
                    SmoothPhasing originalSmoothPhasing = original.GetSmoothPhasing();
                    if (originalSmoothPhasing != null)
                        originalSmoothPhasing.DisableReplacementForSeer(GetDemonCreatorGUID());
                }
            }

            VisibleChangesNotifier notifier = new(objectsToUpdate);
            Cell.VisitWorldObjects(this, notifier, GetVisibilityRange());

            if (original != null) // original is only != null when it was replaced
            {
                SmoothPhasing originalSmoothPhasing = original.GetSmoothPhasing();
                if (originalSmoothPhasing != null)
                    originalSmoothPhasing.ClearViewerDependentInfo(GetDemonCreatorGUID());
            }
        }

        public void SetTempSummonType(TempSummonType type)
        {
            m_type = type;
        }

        public virtual void UnSummon(TimeSpan time = default)
        {
            if (time != TimeSpan.Zero)
            {
                ForcedUnsummonDelayEvent pEvent = new(this);

                m_Events.AddEvent(pEvent, m_Events.CalculateTime(time));
                return;
            }

            Cypher.Assert(!IsPet());
            if (IsPet())
            {
                ToPet().Remove(PetSaveMode.NotInSlot);
                Cypher.Assert(!IsInWorld);
                return;
            }

            WorldObject owner = GetSummoner();
            if (owner != null)
            {
                if (owner.IsCreature())
                    owner.ToCreature().GetAI()?.SummonedCreatureDespawn(this);
                else if (owner.IsGameObject())
                    owner.ToGameObject().GetAI()?.SummonedCreatureDespawn(this);
            }

            AddObjectToRemoveList();
        }

        public override void RemoveFromWorld()
        {
            if (!IsInWorld)
                return;

            if (m_Properties != null && m_Properties.Slot != 0)
            {
                Unit owner = GetSummonerUnit();
                if (owner != null)
                {
                    foreach (ObjectGuid summonSlot in owner.m_SummonSlot)
                    {
                        if (summonSlot == GetGUID())
                            summonSlot.Clear();
                    }
                }
            }

            if (!GetOwnerGUID().IsEmpty())
            {
                Log.outError(LogFilter.Unit,
                    $"Unit {GetEntry()} has owner guid when removed from world");
            }

            base.RemoveFromWorld();
        }

        public int FindUsableTotemSlot(Unit summoner)
        {
            var list = summoner.m_SummonSlot[new Range((int)SummonSlot.Totem, SharedConst.MaxTotemSlot)].ToList();

            // first try exact guid match
            var totemSlot = list.FindIndex(otherTotemGuid => otherTotemGuid == GetGUID());

            // then a slot that shares totem category with this new summon
            if (totemSlot == -1)
                totemSlot = list.FindIndex(IsSharingTotemSlotWith);

            // any empty slot...?
            if (totemSlot == -1)
                totemSlot = list.FindIndex(otherTotemGuid => otherTotemGuid.IsEmpty());

            // if no usable slot was found, try used slot by a summon with the same creature id
            // we must not despawn unrelated summons
            if (totemSlot == -1)
                totemSlot = list.FindIndex(otherTotemGuid => GetEntry() == otherTotemGuid.GetEntry());

            // if no slot was found, this summon gets no slot and will not be stored in m_SummonSlot
            if (totemSlot == -1)
                return 0;

            return totemSlot;
        }

        bool IsSharingTotemSlotWith(ObjectGuid objectGuid)
        {
            Creature otherSummon = GetMap().GetCreature(objectGuid);
            if (otherSummon == null)
                return false;

            SpellInfo mySummonSpell = 
                Global.SpellMgr.GetSpellInfo(m_unitData.CreatedBySpell, Difficulty.None);

            if (mySummonSpell == null)
                return false;

            SpellInfo otherSummonSpell = 
                Global.SpellMgr.GetSpellInfo(otherSummon.m_unitData.CreatedBySpell, Difficulty.None);
            
            if (otherSummonSpell == null)
                return false;

            foreach (var myTotemCategory in mySummonSpell.TotemCategory)
            {
                if (myTotemCategory != 0)
                {
                    foreach (var otherTotemCategory in otherSummonSpell.TotemCategory)
                    {
                        if (otherTotemCategory != 0 && Global.DB2Mgr.IsTotemCategoryCompatibleWith(myTotemCategory, otherTotemCategory, false))
                            return true;
                    }
                }
            }

            foreach (int myTotemId in mySummonSpell.Totem)
            {
                if (myTotemId != 0)
                {
                    foreach (int otherTotemId in otherSummonSpell.Totem)
                    {
                        if (otherTotemId != 0 && myTotemId == otherTotemId)
                            return true;
                    }
                }
            }

            return false;
        }
        
        public override string GetDebugInfo()
        {
            return $"{base.GetDebugInfo()}\nTempSummonType : {GetSummonType()} Summoner: {GetSummonerGUID()} Timer: {GetTimer()}";
        }
        
        public override void SaveToDB(int mapid, List<Difficulty> spawnDifficulties) { }

        public ObjectGuid GetSummonerGUID() { return m_summonerGUID; }

        TempSummonType GetSummonType() { return m_type; }

        public TimeSpan GetTimer() { return m_timer; }

        public void RefreshTimer() { m_timer = m_lifetime; }

        public void ModifyTimer(TimeSpan mod)
        {
            m_timer += mod;
            m_lifetime += mod;
        }

        public int? GetCreatureIdVisibleToSummoner() { return m_creatureIdVisibleToSummoner; }

        public int? GetDisplayIdVisibleToSummoner() { return m_displayIdVisibleToSummoner; }
        
        public bool CanFollowOwner() { return m_canFollowOwner; }

        public void SetCanFollowOwner(bool can) { m_canFollowOwner = can; }

        public SummonPropertiesRecord m_Properties;
        TempSummonType m_type;
        TimeSpan m_timer;
        TimeSpan m_lifetime;
        ObjectGuid m_summonerGUID;
        int? m_creatureIdVisibleToSummoner;
        int? m_displayIdVisibleToSummoner;
        bool m_canFollowOwner;
    }

    public class Minion : TempSummon
    {
        public Minion(SummonPropertiesRecord properties, Unit owner, bool isWorldObject)
            : base(properties, owner, isWorldObject)
        {
            m_owner = owner;
            Cypher.Assert(m_owner != null);
            UnitTypeMask |= UnitTypeMask.Minion;
            m_followAngle = SharedConst.PetFollowAngle;
            /// @todo: Find correct way
            InitCharmInfo();
        }

        public override void InitStats(WorldObject summoner, TimeSpan duration)
        {
            base.InitStats(summoner, duration);

            SetReactState(ReactStates.Passive);

            SetCreatorGUID(GetOwner().GetGUID());
            SetFaction(GetOwner().GetFaction());// TODO: Is this correct? Overwrite the use of SummonPropertiesFlags::UseSummonerFaction

            GetOwner().SetMinion(this, true);
        }

        public override void RemoveFromWorld()
        {
            if (!IsInWorld)
                return;

            GetOwner().SetMinion(this, false);
            base.RemoveFromWorld();
        }

        public override void SetDeathState(DeathState s)
        {
            base.SetDeathState(s);
            if (s != DeathState.JustDied || !IsGuardianPet())
                return;

            Unit owner = GetOwner();
            if (owner == null || !owner.IsPlayer() || owner.GetMinionGUID() != GetGUID())
                return;

            foreach (Unit controlled in owner.m_Controlled)
            {
                if (controlled.GetEntry() == GetEntry() && controlled.IsAlive())
                {
                    owner.SetMinionGUID(controlled.GetGUID());
                    owner.SetPetGUID(controlled.GetGUID());
                    owner.ToPlayer().CharmSpellInitialize();
                    break;
                }
            }
        }

        public bool IsGuardianPet()
        {
            return IsPet() || (m_Properties != null && m_Properties.Control == SummonCategory.Pet);
        }

        public override string GetDebugInfo()
        {
            return $"{base.GetDebugInfo()}\nOwner: {(GetOwner() != null ? GetOwner().GetGUID() : "")}";
        }

        public override Unit GetOwner() { return m_owner; }

        public override float GetFollowAngle() { return m_followAngle; }

        public void SetFollowAngle(float angle) { m_followAngle = angle; }

        // Warlock pets
        public bool IsPetImp() { return GetEntry() == (uint)PetEntry.Imp; }
        public bool IsPetFelhunter() { return GetEntry() == (uint)PetEntry.FelHunter; }
        public bool IsPetVoidwalker() { return GetEntry() == (uint)PetEntry.VoidWalker; }
        public bool IsPetSayaad() { return GetEntry() == (uint)PetEntry.Succubus || GetEntry() == (uint)PetEntry.Incubus; }
        public bool IsPetDoomguard() { return GetEntry() == (uint)PetEntry.Doomguard; }
        public bool IsPetFelguard() { return GetEntry() == (uint)PetEntry.Felguard; }
        public bool IsWarlockPet() { return IsPetImp() || IsPetFelhunter() || IsPetVoidwalker() || IsPetSayaad() || IsPetDoomguard() || IsPetFelguard(); }

        // Death Knight pets
        public bool IsPetGhoul() { return GetEntry() == (uint)PetEntry.Ghoul; } // Ghoul may be guardian or pet
        public bool IsRisenAlly() { return GetEntry() == (uint)PetEntry.RisenAlly; }

        // Shaman pet
        public bool IsSpiritWolf() { return GetEntry() == (uint)PetEntry.SpiritWolf; } // Spirit wolf from feral spirits

        protected Unit m_owner;
        float m_followAngle;
    }

    public class Guardian : Minion
    {
        public Guardian(SummonPropertiesRecord properties, Unit owner, bool isWorldObject)
            : base(properties, owner, isWorldObject)
        {
            m_bonusSpellDamage = 0;

            UnitTypeMask |= UnitTypeMask.Guardian;
            if (properties != null && (properties.Title == SummonTitle.Pet || properties.Control == SummonCategory.Pet))
            {
                UnitTypeMask |= UnitTypeMask.ControlableGuardian;
                InitCharmInfo();
            }
        }

        public override void InitStats(WorldObject summoner, TimeSpan duration)
        {
            base.InitStats(summoner, duration);

            InitStatsForLevel(GetLevel()); // level is already initialized in TempSummon::InitStats, so use that

            if (GetOwner().IsTypeId(TypeId.Player) && HasUnitTypeMask(UnitTypeMask.ControlableGuardian))
                GetCharmInfo().InitCharmCreateSpells();

            SetReactState(ReactStates.Aggressive);
        }

        public override void InitSummon(WorldObject summoner)
        {
            base.InitSummon(summoner);

            if (GetOwner().IsTypeId(TypeId.Player) && GetOwner().GetMinionGUID() == GetGUID()
                && GetOwner().GetCharmedGUID().IsEmpty())
            {
                GetOwner().ToPlayer().CharmSpellInitialize();
            }
        }

        // @todo Move stat mods code to pet passive auras
        public bool InitStatsForLevel(int petlevel)
        {
            CreatureTemplate cinfo = GetCreatureTemplate();
            Cypher.Assert(cinfo != null);

            SetLevel(petlevel);

            //Determine pet Type
            PetType petType = PetType.Max;
            if (IsPet() && GetOwner() is Player owner)
            {
                if (owner.GetClass() == Class.Warlock
                        || owner.GetClass() == Class.Shaman        // Fire Elemental
                        || owner.GetClass() == Class.DeathKnight) // Risen Ghoul
                {
                    petType = PetType.Summon;
                }
                else if (owner.GetClass() == Class.Hunter)
                {
                    petType = PetType.Hunter;
                    UnitTypeMask |= UnitTypeMask.HunterPet;
                }
                else
                {
                    Log.outError(LogFilter.Unit, 
                        $"Unknown Type pet {GetEntry()} is summoned " +
                        $"by player class {owner.GetClass()}");
                }
            }

            int creature_ID = (petType == PetType.Hunter) ? 1 : cinfo.Entry;

            SetMeleeDamageSchool(cinfo.DmgSchool);

            StatMods.SetFlat(UnitMods.Armor, petlevel * 50, UnitModType.BasePermanent);

            SetBaseAttackTime(WeaponAttackType.BaseAttack, SharedConst.BaseAttackTime);
            SetBaseAttackTime(WeaponAttackType.OffAttack, SharedConst.BaseAttackTime);
            SetBaseAttackTime(WeaponAttackType.RangedAttack, SharedConst.BaseAttackTime);

            //scale
            SetObjectScale(GetNativeObjectScale());

            // Resistance
            // Hunters pet should not inherit resistances from creature_template, they have separate auras for that
            if (!IsHunterPet())
            {
                for (int i = (int)SpellSchools.Holy; i < (int)SpellSchools.Max; ++i)
                    StatMods.SetFlat(UnitMods.ResistanceStart + i, cinfo.Resistance[i], UnitModType.BasePermanent);
            }

            PowerType powerType = CalculateDisplayPowerType();

            // Health, Mana or Power, Armor
            PetLevelInfo pInfo = Global.ObjectMgr.GetPetLevelInfo(creature_ID, petlevel);
            if (pInfo != null)                                      // exist in DB
            {
                SetCreateHealth(pInfo.health);
                SetCreateMana(pInfo.mana);

                StatMods.SetMult(UnitMods.PowerStart + (int)powerType, 1.0f, UnitModType.BasePermanent);

                if (pInfo.armor > 0)
                    StatMods.SetFlat(UnitMods.Armor, pInfo.armor, UnitModType.BasePermanent);

                for (byte stat = 0; stat < (int)Stats.Max; ++stat)
                    SetCreateStat((Stats)stat, pInfo.stats[stat]);
            }
            else                                            // not exist in DB, use some default fake data
            {
                // remove elite bonuses included in DB values
                CreatureBaseStats stats = Global.ObjectMgr.GetCreatureBaseStats(petlevel, cinfo.UnitClass);
                CreatureDifficulty creatureDifficulty = GetCreatureDifficulty();

                float healthmod = GetHealthMod(cinfo.Classification);
                int basehp = stats.GenerateHealth(GetCreatureDifficulty());
                int health = (int)(basehp * healthmod);
                int mana = stats.GenerateMana(creatureDifficulty);

                SetCreateHealth(health);
                SetCreateMana(mana);
                SetCreateStat(Stats.Strength, 22);
                SetCreateStat(Stats.Agility, 22);
                SetCreateStat(Stats.Stamina, 25);
                SetCreateStat(Stats.Intellect, 28);
                SetCreateStat(Stats.Spirit, 27);
            }

            // Power
            SetPowerType(powerType);

            // Damage
            SetBonusDamage(0);
            switch (petType)
            {
                case PetType.Summon:
                {
                    // the damage bonus used for pets is either fire or shadow damage, whatever is higher
                    int fire = GetOwner().ToPlayer().m_activePlayerData.ModDamageDonePos[(int)SpellSchools.Fire];
                    int shadow = GetOwner().ToPlayer().m_activePlayerData.ModDamageDonePos[(int)SpellSchools.Shadow];
                    int val = (fire > shadow) ? fire : shadow;
                    if (val < 0)
                        val = 0;

                    SetBonusDamage((int)(val * 0.15f));

                    SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, petlevel - (petlevel / 4));
                    SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, petlevel + (petlevel / 4));
                    break;
                }
                case PetType.Hunter:
                {
                    ToPet().SetPetNextLevelExperience((int)(Global.ObjectMgr.GetXPForLevel(petlevel) * 0.05f));
                    //these formula may not be correct; however, it is designed to be close to what it should be
                    //this makes dps 0.5 of pets level
                    SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, petlevel - (petlevel / 4));
                    //damage range is then petlevel / 2
                    SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, petlevel + (petlevel / 4));
                    //damage is increased afterwards as strength and pet scaling modify attack power
                    break;
                }
                default:
                {
                    switch (GetEntry())
                    {
                        case 510: // mage Water Elemental
                        {
                            SetBonusDamage((int)(GetOwner().SpellBaseDamageBonusDone(SpellSchoolMask.Frost) * 0.33f));
                            break;
                        }
                        case 1964: //force of nature
                        {
                            if (pInfo == null)
                                SetCreateHealth(30 + 30 * petlevel);

                            float bonusDmg = GetOwner().SpellBaseDamageBonusDone(SpellSchoolMask.Nature) * 0.15f;
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, petlevel * 2.5f - ((float)petlevel / 2) + bonusDmg);
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, petlevel * 2.5f + ((float)petlevel / 2) + bonusDmg);
                            break;
                        }
                        case 15352: //earth elemental 36213
                        {
                            if (pInfo == null)
                                SetCreateHealth(100 + 120 * petlevel);

                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, petlevel - (petlevel / 4));
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, petlevel + (petlevel / 4));
                            break;
                        }
                        case 15438: //fire elemental
                        {
                            if (pInfo == null)
                            {
                                SetCreateHealth(40 * petlevel);
                                SetCreateMana(28 + 10 * petlevel);
                            }
                            SetBonusDamage((int)(GetOwner().SpellBaseDamageBonusDone(SpellSchoolMask.Fire) * 0.5f));
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, petlevel * 4 - petlevel);
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, petlevel * 4 + petlevel);
                            break;
                        }
                        case 19668: // Shadowfiend
                        {
                            if (pInfo == null)
                            {
                                SetCreateMana(28 + 10 * petlevel);
                                SetCreateHealth(28 + 30 * petlevel);
                            }
                            int bonus_dmg = (int)(GetOwner().SpellBaseDamageBonusDone(SpellSchoolMask.Shadow) * 0.3f);
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, (petlevel * 4 - petlevel) + bonus_dmg);
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, (petlevel * 4 + petlevel) + bonus_dmg);
                            break;
                        }
                        case 19833: //Snake Trap - Venomous Snake
                        {
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, (petlevel / 2) - 25);
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, (petlevel / 2) - 18);
                            break;
                        }
                        case 19921: //Snake Trap - Viper
                        {
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, petlevel / 2 - 10);
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, petlevel / 2);
                            break;
                        }
                        case 29264: // Feral Spirit
                        {
                            if (pInfo == null)
                                SetCreateHealth(30 * petlevel);

                            // wolf attack speed is 1.5s
                            SetBaseAttackTime(WeaponAttackType.BaseAttack, cinfo.BaseAttackTime);

                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, (petlevel * 4 - petlevel));
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, (petlevel * 4 + petlevel));

                            StatMods.SetFlat(UnitMods.Armor, (int)(GetOwner().GetArmor() * 0.35f), UnitModType.BasePermanent);  // Bonus Armor (35% of player armor)
                            StatMods.SetFlat(UnitMods.StatStamina, (int)(GetOwner().GetStat(Stats.Stamina) * 0.3f), UnitModType.BasePermanent);  // Bonus Stamina (30% of player stamina)
                            
                            if (!HasAura(58877)) // prevent apply twice for the 2 wolves
                                AddAura(58877, this); // Spirit Hunt, passive, Spirit Wolves' attacks heal them and their master for 150% of damage done.
                            break;
                        }
                        case 31216: // Mirror Image
                        {
                            SetBonusDamage((int)(GetOwner().SpellBaseDamageBonusDone(SpellSchoolMask.Frost) * 0.33f));
                            SetDisplayId(GetOwner().GetDisplayId());
                            if (pInfo == null)
                            {
                                SetCreateMana(28 + 30 * petlevel);
                                SetCreateHealth(28 + 10 * petlevel);
                            }
                            break;
                        }
                        case 27829: // Ebon Gargoyle
                        {
                            if (pInfo == null)
                            {
                                SetCreateMana(28 + 10 * petlevel);
                                SetCreateHealth(28 + 30 * petlevel);
                            }
                            SetBonusDamage((int)(GetOwner().GetTotalAttackPowerValue(WeaponAttackType.BaseAttack) * 0.5f));
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, petlevel - (petlevel / 4));
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, petlevel + (petlevel / 4));
                            break;
                        }
                        case 28017: // Bloodworms
                        {
                            SetCreateHealth(4 * petlevel);
                            SetBonusDamage((int)(GetOwner().GetTotalAttackPowerValue(WeaponAttackType.BaseAttack) * 0.006f));
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, petlevel - 30 - (petlevel / 4));
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, petlevel - 30 + (petlevel / 4));
                            break;
                        }
                        default:
                        {
                            /* ToDo: Check what 5f5d2028 broke/fixed and how much of Creature::UpdateLevelDependantStats()
                             * should be copied here (or moved to another method or if that function should be called here
                             * or not just for this default case)
                             */
                            CreatureBaseStats stats = Global.ObjectMgr.GetCreatureBaseStats(petlevel, cinfo.UnitClass);
                            float basedamage = stats.GenerateBaseDamage(GetCreatureDifficulty());

                            float weaponBaseMinDamage = basedamage;
                            float weaponBaseMaxDamage = basedamage * 1.5f;

                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage, weaponBaseMinDamage);
                            SetBaseWeaponDamage(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage, weaponBaseMaxDamage);
                            break;
                        }
                    }
                    break;
                }
            }

            UpdateAllStats();

            SetFullHealth();
            SetFullPower(PowerType.Mana);
            return true;
        }

        const int ENTRY_IMP = 416;
        const int ENTRY_VOIDWALKER = 1860;
        const int ENTRY_SUCCUBUS = 1863;
        const int ENTRY_FELHUNTER = 417;
        const int ENTRY_FELGUARD = 17252;
        const int ENTRY_WATER_ELEMENTAL = 510;
        const int ENTRY_TREANT = 1964;
        const int ENTRY_FIRE_ELEMENTAL = 15438;
        const int ENTRY_GHOUL = 26125;
        const int ENTRY_BLOODWORM = 28017;

        public override bool UpdateStats(Stats stat, bool skipDependents = false)
        {
            UnitMods unitMod = UnitMods.StatStart + (int)stat;
            UnitModResult statValue = new(GetCreateStat(stat));

            UnitMod statsFromOwner = new(UnitModType.TotalPermanent)
            {
                Flat = new(GetBonusStatFromOwner(stat)),
            };

            StatMods.ApplyModsTo(statValue, unitMod, myTotalPerm: statsFromOwner);

            SetStat(stat, (int)statValue.TotalValue);

            // Update stat buff mods for the client
            UpdateStatBuffModForClient(stat, statValue.ModPos, statValue.ModNeg);

            if (skipDependents)
                return true;            

            switch (stat)
            {
                case Stats.Strength:
                    UpdateMeleeAttackPowerAndDamage();
                    break;
                case Stats.Agility:
                    UpdateArmor();
                    break;
                case Stats.Stamina:
                    UpdateMaxHealth();
                    break;
                case Stats.Intellect:
                    UpdateMaxPower(PowerType.Mana);
                    break;
                case Stats.Spirit:
                default:
                    break;
            }

            return true;
        }

        public override bool UpdateAllStats()
        { 
            for (var i = Stats.Strength; i < Stats.Max; ++i)
                UpdateStats(i, true);

            for (var i = PowerType.Mana; i < PowerType.Max; ++i)
                UpdateMaxPower(i);

            UpdateMaxHealth();
            UpdateAllResistances();
            UpdateArmor(true);
            UpdateMeleeAttackPowerAndDamage(true);
            UpdateDamagePhysical(WeaponAttackType.BaseAttack);
            return true;
        }

        public override void UpdateResistances(SpellSchools school, bool skipDependents = false)
        {
            if (school != SpellSchools.Normal)
            {
                UnitMods unitMod = UnitMods.ResistanceStart + (int)school;
                UnitModResult resistValue = new();
                UnitMod? resistFromOwmer = default;

                // hunter and warlock pets gain 40% of owner's resistance
                if (IsPet())
                {
                    resistFromOwmer = new(UnitModType.TotalPermanent)
                    {
                        Flat = new(MathFunctions.CalculatePct(GetOwner().GetResistance(school), 40)),
                    };
                }

                StatMods.ApplyModsTo(resistValue, unitMod, myTotalPerm: resistFromOwmer);

                SetResistance(school, (int)resistValue.TotalValue);
                UpdateResistanceBuffModForClient(SpellSchools.Normal, resistValue.ModPos, resistValue.ModNeg);
            }
        }

        public override void UpdateArmor(bool skipDependents = false)
        {
            UnitMods unitMod = UnitMods.Armor;
            UnitModResult armorValue = new();

            // Add armor from agility
            UnitMod bonusArmor = new(UnitModType.TotalPermanent)
            {
                Flat = new((int)(GetStat(Stats.Agility) * 2.0f)),
            };

            // hunter and warlock pets gain 35% of owner's armor value
            if (IsPet())
            {
                bonusArmor.Flat.Modify(MathFunctions.CalculatePct(GetOwner().GetArmor(), 35), true);
            }

            StatMods.ApplyModsTo(armorValue, unitMod, myTotalPerm: bonusArmor);

            SetArmor((int)armorValue.TotalValue);
            UpdateResistanceBuffModForClient(SpellSchools.Normal, armorValue.ModPos, armorValue.ModNeg);

            if (skipDependents)
                return;
        }

        public override void UpdateMaxHealth()
        {
            UnitMods unitMod = UnitMods.Health;
            float stamina = GetStat(Stats.Stamina) - GetCreateStat(Stats.Stamina);

            float multiplicator;
            switch (GetEntry())
            {
                case ENTRY_IMP:
                    multiplicator = 8.4f;
                    break;
                case ENTRY_VOIDWALKER:
                    multiplicator = 11.0f;
                    break;
                case ENTRY_SUCCUBUS:
                    multiplicator = 9.1f;
                    break;
                case ENTRY_FELHUNTER:
                    multiplicator = 9.5f;
                    break;
                case ENTRY_FELGUARD:
                    multiplicator = 11.0f;
                    break;
                case ENTRY_BLOODWORM:
                    multiplicator = 1.0f;
                    break;
                default:
                    multiplicator = 10.0f;
                    break;
            }

            UnitModResult healthValue = new(GetCreateHealth());
            UnitMod healthFromStamina = new(UnitModType.TotalPermanent)
            {
                Flat = new((int)(stamina * multiplicator)),
            };

            StatMods.ApplyModsTo(healthValue, unitMod, myTotalPerm: healthFromStamina);

            SetMaxHealth((uint)healthValue.TotalValue);
        }

        public override void UpdateMaxPower(PowerType power)
        {
            if (GetPowerIndex(power) == (uint)PowerType.Max)
                return;

            UnitMods unitMod = UnitMods.PowerStart + (int)power;

            float intellect = (power == PowerType.Mana) ? GetStat(Stats.Intellect) - GetCreateStat(Stats.Intellect) : 0.0f;
            float multiplicator = 15.0f;

            switch (GetEntry())
            {
                case ENTRY_IMP: multiplicator = 4.95f; break;
                case ENTRY_VOIDWALKER:
                case ENTRY_SUCCUBUS:
                case ENTRY_FELHUNTER:
                case ENTRY_FELGUARD: multiplicator = 11.5f; break;
                default: multiplicator = 15.0f; break;
            }
            
            UnitModResult powerValue = new(GetCreatePowerValue(power));
            UnitMod powerFromIntellect = new(UnitModType.TotalPermanent)
            {
                Flat = new((int)(intellect * multiplicator)),
            };

            StatMods.ApplyModsTo(powerValue, unitMod, myTotalPerm: powerFromIntellect);

            SetMaxPower(power, (int)powerValue.TotalValue);
        }

        public override void UpdateMeleeAttackPowerAndDamage(bool skipDependents = false)
        {
            float val;
            float bonusAP = 0.0f;
            UnitMods unitMod = UnitMods.AttackPowerMelee;

            if (GetEntry() == ENTRY_IMP)                                   // imp's attack power
                val = GetStat(Stats.Strength) - 10.0f;
            else
                val = 2 * GetStat(Stats.Strength) - 20.0f;

            Player owner = GetOwner() != null ? GetOwner().ToPlayer() : null;
            if (owner != null)
            {
                if (IsHunterPet())  // hunter pets benefit from owner's attack power
                {
                    float mod = 1.0f;   // Hunter contribution modifier
                    if (IsPet())
                    {
                        // Talent: Wild Hunt [2253:1]^1 = 62758
                        AuraEffect auraEffect = GetAuraEffectOfTalent(2253, 1);
                        if (auraEffect != null)
                            MathFunctions.AddPct(ref mod, auraEffect.GetAmount());
                    }

                    bonusAP = owner.GetTotalAttackPowerValue(WeaponAttackType.RangedAttack) * 0.22f * mod;

                    // Talent: Animal Handler [1799:1]^1 = 34453
                    if (owner.GetAuraEffectOfTalent(1799, 1, owner.GetGUID()) is AuraEffect aurEff) // Animal Handler
                    {
                        MathFunctions.AddPct(ref bonusAP, aurEff.GetAmount());
                        MathFunctions.AddPct(ref val, aurEff.GetAmount());
                    }

                    SetBonusDamage((int)(owner.GetTotalAttackPowerValue(WeaponAttackType.RangedAttack) * 0.1287f * mod));
                }
                else if (IsPetGhoul() || IsRisenAlly()) //ghouls benefit from deathknight's attack power (may be summon pet or not)
                {
                    float ownerAP = owner.GetTotalAttackPowerValue(WeaponAttackType.BaseAttack);

                    bonusAP = ownerAP * 0.22f;
                    SetBonusDamage((int)(ownerAP * 0.1287f));
                }
                else if (IsSpiritWolf()) // wolf benefit from shaman's attack power
                {
                    float ownerAP = owner.GetTotalAttackPowerValue(WeaponAttackType.BaseAttack);

                    float dmg_multiplier = 0.31f; 
                    if (m_owner.GetAuraEffect(63271, 0) is AuraEffect auraEffect) // Glyph of Feral Spirit
                        dmg_multiplier = 0.61f;
                    bonusAP = ownerAP * dmg_multiplier;
                    SetBonusDamage((int)(ownerAP * dmg_multiplier));
                }
                // demons benefit from warlocks shadow or fire damage
                else if (IsPet())
                {
                    int fire = owner.m_activePlayerData.ModDamageDonePos[(int)SpellSchools.Fire] - owner.m_activePlayerData.ModDamageDoneNeg[(int)SpellSchools.Fire];
                    int shadow = owner.m_activePlayerData.ModDamageDonePos[(int)SpellSchools.Shadow] - owner.m_activePlayerData.ModDamageDoneNeg[(int)SpellSchools.Shadow];
                    int maximum = (fire > shadow) ? fire : shadow;
                    if (maximum < 0)
                        maximum = 0;
                    SetBonusDamage((int)(maximum * 0.15f));
                    bonusAP = maximum * 0.57f;
                }
                //water elementals benefit from mage's frost damage
                else if (GetEntry() == ENTRY_WATER_ELEMENTAL)
                {
                    int frost = owner.m_activePlayerData.ModDamageDonePos[(int)SpellSchools.Frost] - owner.m_activePlayerData.ModDamageDoneNeg[(int)SpellSchools.Frost];
                    if (frost < 0)
                        frost = 0;
                    SetBonusDamage((int)(frost * 0.4f));
                }
            }

            StatMods.SetFlat(unitMod, (int)(val + bonusAP), UnitModType.BasePermanent);
            UnitModResult attackPowerValue = new();

            StatMods.ApplyModsTo(attackPowerValue, unitMod);

            SetAttackPower((int)attackPowerValue.NakedValue);
            SetAttackPowerModPos((int)attackPowerValue.ModPos);
            SetAttackPowerModNeg((int)attackPowerValue.ModNeg);
            SetAttackPowerMultiplier(1.0f);

            if (skipDependents)
                return;

            // automatically update weapon damage after attack power modification
            UpdateDamagePhysical(WeaponAttackType.BaseAttack);
        }

        public override void UpdateDamagePhysical(WeaponAttackType attType)
        {
            if (attType > WeaponAttackType.BaseAttack)
                return;

            float bonusDamage = 0.0f;
            Player playerOwner = m_owner.ToPlayer();
            if (playerOwner != null)
            {
                //force of nature
                if (GetEntry() == ENTRY_TREANT)
                {
                    int spellDmg = playerOwner.m_activePlayerData.ModDamageDonePos[(int)SpellSchools.Nature] - playerOwner.m_activePlayerData.ModDamageDoneNeg[(int)SpellSchools.Nature];
                    if (spellDmg > 0)
                        bonusDamage = spellDmg * 0.09f;
                }
                //greater fire elemental
                else if (GetEntry() == ENTRY_FIRE_ELEMENTAL)
                {
                    int spellDmg = playerOwner.m_activePlayerData.ModDamageDonePos[(int)SpellSchools.Fire] - playerOwner.m_activePlayerData.ModDamageDoneNeg[(int)SpellSchools.Fire];
                    if (spellDmg > 0)
                        bonusDamage = spellDmg * 0.4f;
                }
            }

            UnitMods unitMod = UnitMods.DamageMainHand;
            float weapon_mindamage = GetWeaponDamageRange(WeaponAttackType.BaseAttack, WeaponDamageRange.MinDamage);
            float weapon_maxdamage = GetWeaponDamageRange(WeaponAttackType.BaseAttack, WeaponDamageRange.MaxDamage);
            float att_speed = GetBaseAttackTime(WeaponAttackType.BaseAttack) / 1000.0f;

            UnitModResult weaponMinDamage = new(weapon_mindamage);
            UnitModResult weaponMaxDamage = new(weapon_maxdamage);

            UnitMod initialDamageBonus = new(UnitModType.BasePermanent)
            {
                Flat = new((int)(GetTotalAttackPowerValue(attType) / 14.0f * att_speed + bonusDamage))
            };

            // Talent: Cobra Reflexes [2107:0]^1 = 61682
            UnitMod damageMod = new(UnitModType.TotalTemporary);
            if (GetAuraEffectOfTalent(2107, 0) is AuraEffect auraEffect)
            {
                damageMod.Mult.ModifyPercentage(-auraEffect.GetAmount(), true);
            }

            //  Pet's base damage changes depending on happiness
            if (IsHunterPet())
            {                
                switch (ToPet().GetHappinessState())
                {
                    case HappinessState.Happy:
                        // 125% of normal damage
                        damageMod.Mult.Modify(1.25f, true);
                        break;
                    case HappinessState.Content:
                        // 100% of normal damage, nothing to modify
                        break;
                    case HappinessState.Unhappy:
                        // 75% of normal damage
                        damageMod.Mult.Modify(0.75f, true);
                        break;
                }
            }

            // Mods from auras
            damageMod.Modify(StatMods.Get(UnitMods.DamagePhysical), true);

            StatMods.ApplyModsTo(weaponMinDamage, unitMod, myBasePerm: initialDamageBonus, myTotalTemp: damageMod)
                .ReApplyTo(weaponMaxDamage);

            SetUpdateFieldStatValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MinDamage), weaponMinDamage.TotalValue);
            SetUpdateFieldStatValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MaxDamage), weaponMaxDamage.TotalValue);
        }

        public void SetBonusDamage(int damage)
        {
            m_bonusSpellDamage = damage;

            if (GetOwner() is Player playerOwner)
                playerOwner.SetPetSpellPower(damage);
        }

        public int GetBonusDamage() { return m_bonusSpellDamage; }

        public int GetBonusStatFromOwner(Stats stat)
        {
            float ownersBonus = 0.0f;

            Unit owner = GetOwner();

            // Handle Death Knight Glyphs and Talents
            float mod = 0.75f;
            if (IsPetGhoul() && (stat == Stats.Stamina || stat == Stats.Strength))
            {
                switch (stat)
                {
                    case Stats.Stamina:
                        mod = 0.3f;
                        break;                // Default Owner's Stamina scale
                    case Stats.Strength:
                        mod = 0.7f;
                        break;                // Default Owner's Strength scale
                    default: break;
                }

                // Talent: Ravenous Dead [1934:1]^1 = 48965
                AuraEffect aurEff = owner.GetAuraEffectOfTalent(1934, 1);
                if (aurEff != null)
                    MathFunctions.AddPct(ref mod, aurEff.GetAmount());  // Ravenous Dead edits the original scale

                // Glyph of the Ghoul
                aurEff = owner.GetAuraEffect(58686, 0);
                if (aurEff != null)
                    MathFunctions.AddPct(ref mod, aurEff.GetAmount());  // Glyph of the Ghoul adds a flat value to the scale mod


                ownersBonus = owner.GetStat(stat) * mod;
            }
            else if (stat == Stats.Stamina)
            {
                if (owner.GetClass() == Class.Warlock && IsPet())
                {
                    ownersBonus = MathFunctions.CalculatePct(owner.GetStat(Stats.Stamina), 75);
                }
                else
                {
                    mod = 0.45f;

                    if (IsPet())
                    {
                        // Talent: Wild Hunt [2253:0]^1 = 62758
                        AuraEffect auraEffect = GetAuraEffectOfTalent(2253, 0);
                        if (auraEffect != null)
                            MathFunctions.AddPct(ref mod, auraEffect.GetAmount());
                    }

                    ownersBonus = owner.GetStat(stat) * mod;
                }
            }
            //warlock's and mage's pets gain 30% of owner's intellect
            else if (stat == Stats.Intellect)
            {
                if (owner.GetClass() == Class.Warlock || owner.GetClass() == Class.Mage)
                {
                    ownersBonus = MathFunctions.CalculatePct(owner.GetStat(stat), 30);
                }
            }

            return (int)ownersBonus;
        }

        int m_bonusSpellDamage;
    }

    public class Puppet : Minion
    {
        public Puppet(SummonPropertiesRecord properties, Unit owner) : base(properties, owner, false)
        {
            Cypher.Assert(owner.IsTypeId(TypeId.Player));
            UnitTypeMask |= UnitTypeMask.Puppet;
        }

        public override void InitStats(WorldObject summoner, TimeSpan duration)
        {
            base.InitStats(summoner, duration);

            SetLevel(GetOwner().GetLevel());
            SetReactState(ReactStates.Passive);
        }

        public override void InitSummon(WorldObject summoner)
        {
            base.InitSummon(summoner);
            if (!SetCharmedBy(GetOwner(), CharmType.Possess))
                Cypher.Assert(false);
        }

        public override void Update(TimeSpan diff)
        {
            base.Update(diff);
            //check if caster is channelling?
            if (IsInWorld)
            {
                if (!IsAlive())
                {
                    UnSummon();
                    // @todo why long distance .die does not remove it
                }
            }
        }
    }

    public class ForcedUnsummonDelayEvent : BasicEvent
    {
        public ForcedUnsummonDelayEvent(TempSummon owner)
        {
            m_owner = owner;
        }

        public override bool Execute(TimeSpan e_time, TimeSpan p_time)
        {
            m_owner.UnSummon();
            return true;
        }

        TempSummon m_owner;
    }

    public class TempSummonData
    {
        public int entry;        // Entry of summoned creature
        public Position pos;        // Position, where should be creature spawned
        public TempSummonType type; // Summon type, see TempSummonType for available types
        public TimeSpan time;         // Despawn time, usable only with certain temp summon types
    }

    enum PetEntry
    {
        // Warlock pets
        Imp = 416,
        FelHunter = 691,
        VoidWalker = 1860,
        Succubus = 1863,
        Doomguard = 18540,
        Felguard = 30146,
        Incubus = 184600,

        // Death Knight pets
        Ghoul = 26125,
        RisenAlly = 30230,

        // Shaman pet
        SpiritWolf = 29264
    }
}
