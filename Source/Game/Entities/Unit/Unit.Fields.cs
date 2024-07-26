// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Game.AI;
using Game.Combat;
using Game.DataStorage;
using Game.Movement;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;

namespace Game.Entities
{
    public abstract partial class Unit
    {
        //AI
        protected Stack<UnitAI> i_AIs = new();
        protected UnitAI i_AI;
        bool m_aiLocked;

        //Movement
        protected float[] m_speed_rate = new float[(int)UnitMoveType.Max];
        List<AbstractFollower> m_followingMe = new();
        public MoveSpline MoveSpline { get; set; }
        MotionMaster i_motionMaster;
        public int m_movementCounter;       //< Incrementing counter used in movement packets
        TimeTracker splineSyncTimer;
        MovementForces _movementForces;
        PositionUpdateInfo _positionUpdateInfo;
        protected Unit m_unitMovedByMe;    // only ever set for players, and only for direct client control
        protected Player m_playerMovingMe; // only set for direct client control (possess effects, vehicles and similar)

        //Combat
        protected List<Unit> attackerList = new();
        Dictionary<ReactiveType, Milliseconds> m_reactiveTimer = new();
        protected float[][] m_weaponDamage = new float[(int)WeaponAttackType.Max][];

        Milliseconds[] m_baseAttackSpeed = new Milliseconds[(int)WeaponAttackType.Max];
        public float[] m_modAttackSpeedPct = new float[(int)WeaponAttackType.Max];
        protected Milliseconds[] m_attackTimer = new Milliseconds[(int)WeaponAttackType.Max];
        bool _isCombatDisallowed;

        // Threat+combat management
        CombatManager m_combatManager;
        ThreatManager m_threatManager;

        protected Unit attacking;

        public float ModMeleeHitChance { get; set; }
        public float ModRangedHitChance { get; set; }
        public float ModSpellHitChance { get; set; }
        public bool m_canDualWield;
        public float BaseSpellCritChance { get; set; }
        public Milliseconds RegenTimer { get; set; }

        int _lastExtraAttackSpell;
        Dictionary<ObjectGuid, int> extraAttacksTargets = new();
        ObjectGuid _lastDamagedTargetGuid;

        //Charm
        public List<Unit> m_Controlled = new();
        List<Player> m_sharedVision = new();
        Unit m_charmer; // Unit that is charming ME
        Unit m_charmed; // Unit that is being charmed BY ME
        CharmInfo m_charmInfo;
        protected bool m_ControlledByPlayer;
        public ObjectGuid LastCharmerGUID { get; set; }

        int _oldFactionId;         // faction before charm
        bool _isWalkingBeforeCharm; // Are we walking before we were charmed?

        //Spells 
        protected Dictionary<CurrentSpellTypes, Spell> m_currentSpells = new((int)CurrentSpellTypes.Max);
        MultiMap<int, int>[] m_spellImmune = new MultiMap<int, int>[(int)SpellImmunity.Max];
        SpellAuraInterruptFlags m_interruptMask;
        SpellAuraInterruptFlags2 m_interruptMask2;
        int m_procDeep;               // tracked for proc system correctness (what spells should proc what)
        int m_procChainLength;        // tracked to protect against infinite proc loops (hard limit, will disallow procs even if they should happen)
        SpellHistory _spellHistory;

        //Auras
        MultiMap<AuraType, AuraEffect> m_modAuras = new();
        List<Aura> m_removedAuras = new();
        List<AuraApplication> m_interruptableAuras = new();             // auras which have interrupt mask applied on unit
        MultiMap<AuraStateType, AuraApplication> m_auraStateAuras = new();        // Used for improve performance of aura state checks on aura apply/remove
        SortedSet<AuraApplication> m_visibleAuras = new(new VisibleAuraSlotCompare());
        SortedSet<AuraApplication> m_visibleAurasToUpdate = new(new VisibleAuraSlotCompare());
        MultiMap<int, AuraApplication> m_appliedAuras = new();
        MultiMap<int, Aura> m_ownedAuras = new();
        List<Aura> m_scAuras = new();
        protected float[][] m_auraFlatModifiersGroup = new float[(int)UnitMods.End][];
        protected float[][] m_auraPctModifiersGroup = new float[(int)UnitMods.End][];
        uint m_removedAurasCount;

        //General  
        public UnitData m_unitData;

        DiminishingReturn[] m_Diminishing = new DiminishingReturn[(int)DiminishingGroup.Max];
        protected List<GameObject> m_gameObj = new();
        List<AreaTrigger> m_areaTrigger = new();
        protected List<DynamicObject> m_dynObj = new();
        protected float[] CreateStats = new float[(int)Stats.Max];
        float[] m_floatStatPosBuff = new float[(int)Stats.Max];
        float[] m_floatStatNegBuff = new float[(int)Stats.Max];
        public ObjectGuid[] m_SummonSlot = new ObjectGuid[7];
        public ObjectGuid[] m_ObjectSlot = new ObjectGuid[4];
        public UnitTypeMask UnitTypeMask { get; set; }
        UnitState m_state;
        ServerTime m_lastManaUseTime;
        protected LiquidTypeRecord _lastLiquid;
        protected DeathState m_deathState;
        public Vehicle m_vehicle { get; set; }
        public Vehicle VehicleKit { get; set; }
        bool canModifyStats;
        public ServerTime LastSanctuaryTime { get; set; }
        int m_transformSpell;
        bool m_cleanupDone; // lock made to not add stuff after cleanup before delete
        bool m_duringRemoveFromWorld; // lock made to not add stuff after begining removing from world
        bool _instantCast;

        bool _playHoverAnim;

        ushort _aiAnimKitId;
        ushort _movementAnimKitId;
        ushort _meleeAnimKitId;

        class ValuesUpdateForPlayerWithMaskSender : IDoWork<Player>
        {
            Unit Owner;
            ObjectFieldData ObjectMask = new();
            UnitData UnitMask = new();

            public ValuesUpdateForPlayerWithMaskSender(Unit owner)
            {
                Owner = owner;
            }

            public void Invoke(Player player)
            {
                UpdateData udata = new(Owner.GetMapId());

                Owner.BuildValuesUpdateForPlayerWithMask(udata, ObjectMask.GetUpdateMask(), UnitMask.GetUpdateMask(), player);

                udata.BuildPacket(out UpdateObject packet);
                player.SendPacket(packet);
            }
        }
    }

    public struct DiminishingReturn
    {
        public DiminishingReturn(RelativeTime hitTime, DiminishingLevels hitCount)
        {
            Stack = 0;
            HitTime = hitTime;
            HitCount = hitCount;
        }

        public void Clear()
        {
            Stack = 0;
            HitTime = default;
            HitCount = DiminishingLevels.Level1;
        }

        public uint Stack;
        public RelativeTime HitTime;
        public DiminishingLevels HitCount;
    }

    public class ProcEventInfo
    {
        public ProcEventInfo(Unit actor, Unit actionTarget, Unit procTarget, ProcFlagsInit typeMask, ProcFlagsSpellType spellTypeMask,
            ProcFlagsSpellPhase spellPhaseMask, ProcFlagsHit hitMask, Spell spell, DamageInfo damageInfo, HealInfo healInfo)
        {
            _actor = actor;
            _actionTarget = actionTarget;
            _procTarget = procTarget;
            _typeMask = typeMask;
            _spellTypeMask = spellTypeMask;
            _spellPhaseMask = spellPhaseMask;
            _hitMask = hitMask;
            _spell = spell;
            _damageInfo = damageInfo;
            _healInfo = healInfo;
        }

        public Unit GetActor() { return _actor; }
        public Unit GetActionTarget() { return _actionTarget; }
        public Unit GetProcTarget() { return _procTarget; }

        public ProcFlagsInit GetTypeMask() { return _typeMask; }
        public ProcFlagsSpellType GetSpellTypeMask() { return _spellTypeMask; }
        public ProcFlagsSpellPhase GetSpellPhaseMask() { return _spellPhaseMask; }
        public ProcFlagsHit GetHitMask() { return _hitMask; }

        public SpellInfo GetSpellInfo()
        {
            if (_spell != null)
                return _spell.GetSpellInfo();
            if (_damageInfo != null)
                return _damageInfo.GetSpellInfo();
            if (_healInfo != null)
                return _healInfo.GetSpellInfo();

            return null;
        }

        public SpellSchoolMask GetSchoolMask()
        {
            if (_spell != null)
                return _spell.GetSpellInfo().GetSchoolMask();
            if (_damageInfo != null)
                return _damageInfo.GetSchoolMask();
            if (_healInfo != null)
                return _healInfo.GetSchoolMask();

            return SpellSchoolMask.None;
        }

        public DamageInfo GetDamageInfo() { return _damageInfo; }
        public HealInfo GetHealInfo() { return _healInfo; }

        public Spell GetProcSpell() { return _spell; }

        Unit _actor;
        Unit _actionTarget;
        Unit _procTarget;
        ProcFlagsInit _typeMask;
        ProcFlagsSpellType _spellTypeMask;
        ProcFlagsSpellPhase _spellPhaseMask;
        ProcFlagsHit _hitMask;
        Spell _spell;
        DamageInfo _damageInfo;
        HealInfo _healInfo;
    }

    public class DamageInfo
    {
        public DamageInfo(Unit attacker, Unit victim, int damage, SpellInfo spellInfo, SpellSchoolMask schoolMask, DamageEffectType damageType, WeaponAttackType attackType)
        {
            m_attacker = attacker;
            m_victim = victim;
            m_damage = damage;
            m_originalDamage = damage;
            m_spellInfo = spellInfo;
            m_schoolMask = schoolMask;
            m_damageType = damageType;
            m_attackType = attackType;
        }

        public DamageInfo(CalcDamageInfo dmgInfo)
        {
            m_attacker = dmgInfo.Attacker;
            m_victim = dmgInfo.Target;
            m_damage = dmgInfo.Damage;
            m_originalDamage = dmgInfo.Damage;
            m_spellInfo = null;
            m_schoolMask = dmgInfo.DamageSchoolMask;
            m_damageType = DamageEffectType.Direct;
            m_attackType = dmgInfo.AttackType;
            m_absorb = dmgInfo.Absorb;
            m_resist = dmgInfo.Resist;
            m_block = dmgInfo.Blocked;

            switch (dmgInfo.TargetState)
            {
                case VictimState.Immune:
                    m_hitMask |= ProcFlagsHit.Immune;
                    break;
                case VictimState.Blocks:
                    m_hitMask |= ProcFlagsHit.FullBlock;
                    break;
            }

            if (dmgInfo.HitInfo.HasAnyFlag(HitInfo.PartialAbsorb | HitInfo.FullAbsorb))
                m_hitMask |= ProcFlagsHit.Absorb;

            if (dmgInfo.HitInfo.HasAnyFlag(HitInfo.FullResist))
                m_hitMask |= ProcFlagsHit.FullResist;

            if (m_block != 0)
                m_hitMask |= ProcFlagsHit.Block;

            bool damageNullified = 
                dmgInfo.HitInfo.HasAnyFlag(HitInfo.FullAbsorb | HitInfo.FullResist) 
                || m_hitMask.HasAnyFlag(ProcFlagsHit.Immune | ProcFlagsHit.FullBlock);

            switch (dmgInfo.HitOutCome)
            {
                case MeleeHitOutcome.Miss:
                    m_hitMask |= ProcFlagsHit.Miss;
                    break;
                case MeleeHitOutcome.Dodge:
                    m_hitMask |= ProcFlagsHit.Dodge;
                    break;
                case MeleeHitOutcome.Parry:
                    m_hitMask |= ProcFlagsHit.Parry;
                    break;
                case MeleeHitOutcome.Evade:
                    m_hitMask |= ProcFlagsHit.Evade;
                    break;
                case MeleeHitOutcome.Block:
                case MeleeHitOutcome.Crushing:
                case MeleeHitOutcome.Glancing:
                case MeleeHitOutcome.Normal:
                    if (!damageNullified)
                        m_hitMask |= ProcFlagsHit.Normal;
                    break;
                case MeleeHitOutcome.Crit:
                    if (!damageNullified)
                        m_hitMask |= ProcFlagsHit.Critical;
                    break;
            }
        }

        public DamageInfo(SpellNonMeleeDamage spellNonMeleeDamage, DamageEffectType damageType, WeaponAttackType attackType, ProcFlagsHit hitMask)
        {
            m_attacker = spellNonMeleeDamage.attacker;
            m_victim = spellNonMeleeDamage.target;
            m_damage = spellNonMeleeDamage.damage;
            m_spellInfo = spellNonMeleeDamage.Spell;
            m_schoolMask = spellNonMeleeDamage.schoolMask;
            m_damageType = damageType;
            m_attackType = attackType;
            m_absorb = spellNonMeleeDamage.absorb;
            m_resist = spellNonMeleeDamage.resist;
            m_block = spellNonMeleeDamage.blocked;
            m_hitMask = hitMask;

            if (spellNonMeleeDamage.blocked != 0)
                m_hitMask |= ProcFlagsHit.Block;
            if (spellNonMeleeDamage.absorb != 0)
                m_hitMask |= ProcFlagsHit.Absorb;
        }

        public void ModifyDamage(int amount)
        {
            amount = Math.Max(amount, -GetDamage());
            m_damage += amount;
        }

        public void AbsorbDamage(int amount)
        {
            amount = Math.Min(amount, GetDamage());
            m_absorb += amount;
            m_damage -= amount;
            m_hitMask |= ProcFlagsHit.Absorb;
        }

        public void ResistDamage(int amount)
        {
            amount = Math.Min(amount, GetDamage());
            m_resist += amount;
            m_damage -= amount;
            if (m_damage == 0)
            { 
                m_hitMask |= ProcFlagsHit.FullResist;
                m_hitMask &= ~(ProcFlagsHit.Normal | ProcFlagsHit.Critical);
            }
        }

        void BlockDamage(int amount)
        {
            amount = Math.Min(amount, GetDamage());
            m_block += amount;
            m_damage -= amount;
            m_hitMask |= ProcFlagsHit.Block;
            if (m_damage == 0)
            { 
                m_hitMask |= ProcFlagsHit.FullBlock;
                m_hitMask &= ~(ProcFlagsHit.Normal | ProcFlagsHit.Critical);
            }
        }

        public Unit GetAttacker() { return m_attacker; }
        public Unit GetVictim() { return m_victim; }
        public SpellInfo GetSpellInfo() { return m_spellInfo; }
        public SpellSchoolMask GetSchoolMask() { return m_schoolMask; }
        DamageEffectType GetDamageType() { return m_damageType; }
        public WeaponAttackType GetAttackType() { return m_attackType; }
        public int GetDamage() { return m_damage; }
        public int GetOriginalDamage() { return m_originalDamage; }
        public int GetAbsorb() { return m_absorb; }
        public int GetResist() { return m_resist; }
        int GetBlock() { return m_block; }
        public ProcFlagsHit GetHitMask() { return m_hitMask; }

        Unit m_attacker;
        Unit m_victim;
        int m_damage;
        int m_originalDamage;
        SpellInfo m_spellInfo;
        SpellSchoolMask m_schoolMask;
        DamageEffectType m_damageType;
        WeaponAttackType m_attackType;
        int m_absorb;
        int m_resist;
        int m_block;
        ProcFlagsHit m_hitMask;
    }

    public class HealInfo
    {
        public HealInfo(Unit healer, Unit target, int heal, SpellInfo spellInfo, SpellSchoolMask schoolMask)
        {
            _healer = healer;
            _target = target;
            _heal = heal;
            _originalHeal = heal;
            _spellInfo = spellInfo;
            _schoolMask = schoolMask;
        }

        public void AbsorbHeal(int amount)
        {
            amount = Math.Min(amount, GetHeal());
            _absorb += amount;
            _heal -= amount;
            amount = Math.Min(amount, GetEffectiveHeal());
            _effectiveHeal -= amount;
            _hitMask |= ProcFlagsHit.Absorb;
        }

        public void SetEffectiveHeal(int amount) { _effectiveHeal = amount; }

        public Unit GetHealer() { return _healer; }
        public Unit GetTarget() { return _target; }
        public int GetHeal() { return _heal; }
        public int GetOriginalHeal() { return _originalHeal; }
        public int GetEffectiveHeal() { return _effectiveHeal; }
        public int GetAbsorb() { return _absorb; }
        public SpellInfo GetSpellInfo() { return _spellInfo; }
        public SpellSchoolMask GetSchoolMask() { return _schoolMask; }
        ProcFlagsHit GetHitMask() { return _hitMask; }

        Unit _healer;
        Unit _target;
        int _heal;
        int _originalHeal;
        int _effectiveHeal;
        int _absorb;
        SpellInfo _spellInfo;
        SpellSchoolMask _schoolMask;
        ProcFlagsHit _hitMask;
    }

    public class CalcDamageInfo
    {
        public Unit Attacker { get; set; }             // Attacker
        public Unit Target { get; set; }               // Target for damage
        public SpellSchoolMask DamageSchoolMask { get; set; }
        public int Damage;
        public int OriginalDamage { get; set; }
        public int Absorb;
        public int Resist { get; set; }
        public int Blocked { get; set; }
        public HitInfo HitInfo { get; set; }
        public VictimState TargetState { get; set; }

        // Helper
        public WeaponAttackType AttackType { get; set; }
        public ProcFlagsInit ProcAttacker { get; set; }
        public ProcFlagsInit ProcVictim { get; set; }
        public int CleanDamage { get; set; }        // Used only for rage calculation
        public MeleeHitOutcome HitOutCome { get; set; }  // TODO: remove this field (need use TargetState)
    }

    public class SpellNonMeleeDamage
    {
        public SpellNonMeleeDamage(Unit _attacker, Unit _target, SpellInfo _spellInfo, SpellCastVisual spellVisual, SpellSchoolMask _schoolMask, ObjectGuid _castId = default)
        {
            target = _target;
            attacker = _attacker;
            Spell = _spellInfo;
            SpellVisual = spellVisual;
            schoolMask = _schoolMask;
            castId = _castId;
            preHitHealth = (int)_target.GetHealth();
        }

        public Unit target;
        public Unit attacker;
        public ObjectGuid castId;
        public SpellInfo Spell;
        public SpellCastVisual SpellVisual;
        public int damage;
        public int originalDamage;
        public SpellSchoolMask schoolMask;
        public int absorb;
        public int resist;
        public bool periodicLog;
        public int blocked;
        public HitInfo HitInfo;
        // Used for help
        public int cleanDamage;
        public bool fullBlock;
        public int preHitHealth;
    }

    public class CleanDamage
    {
        public CleanDamage(int mitigated, int absorbed, WeaponAttackType _attackType, MeleeHitOutcome _hitOutCome)
        {
            absorbed_damage = absorbed;
            mitigated_damage = mitigated;
            attackType = _attackType;
            hitOutCome = _hitOutCome;
        }

        public int absorbed_damage { get; }
        public int mitigated_damage { get; set; }

        public WeaponAttackType attackType { get; }
        public MeleeHitOutcome hitOutCome { get; }
    }

    public class DispelInfo
    {
        public DispelInfo(WorldObject dispeller, int dispellerSpellId, byte chargesRemoved)
        {
            _dispeller = dispeller;
            _dispellerSpell = dispellerSpellId;
            _chargesRemoved = chargesRemoved;
        }

        public WorldObject GetDispeller() { return _dispeller; }
        int GetDispellerSpellId() { return _dispellerSpell; }
        public byte GetRemovedCharges() { return _chargesRemoved; }
        public void SetRemovedCharges(byte amount)
        {
            _chargesRemoved = amount;
        }

        WorldObject _dispeller;
        int _dispellerSpell;
        byte _chargesRemoved;
    }

    public class SpellPeriodicAuraLogInfo
    {
        public SpellPeriodicAuraLogInfo(AuraEffect _auraEff, int _damage, int _originalDamage, int _overDamage, int _absorb, int _resist, float _multiplier, bool _critical)
        {
            auraEff = _auraEff;
            damage = _damage;
            originalDamage = _originalDamage;
            overDamage = _overDamage;
            absorb = _absorb;
            resist = _resist;
            multiplier = _multiplier;
            critical = _critical;
        }

        public AuraEffect auraEff;
        public int damage;
        public int originalDamage;
        public int overDamage;                                      // overkill/overheal
        public int absorb;
        public int resist;
        public float multiplier;
        public bool critical;
    }

    class VisibleAuraSlotCompare : IComparer<AuraApplication>
    {
        public int Compare(AuraApplication x, AuraApplication y)
        {
            return x.GetSlot().CompareTo(y.GetSlot());
        }
    }

    public class DeclinedName
    {
        public StringArray Name = new(SharedConst.MaxDeclinedNameCases);

        public DeclinedName() { }
        public DeclinedName(DeclinedName uf)
        {
            for (var i = 0; i < SharedConst.MaxDeclinedNameCases; ++i)
                Name[i] = uf.Name[i];
        }
}

    struct PositionUpdateInfo
    {
        public bool Relocated;
        public bool Turned;

        public void Reset()
        {
            Relocated = false;
            Turned = false;
        }
    }
}
