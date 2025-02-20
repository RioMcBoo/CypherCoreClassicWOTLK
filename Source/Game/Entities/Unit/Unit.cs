﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.BattleGrounds;
using Game.Chat;
using Game.Combat;
using Game.DataStorage;
using Game.Groups;
using Game.Maps;
using Game.Movement;
using Game.Networking;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entities
{
    public abstract partial class Unit : WorldObject
    {
        public Unit(bool isWorldObject) : base(isWorldObject)
        {
            MoveSpline = new MoveSpline();
            i_motionMaster = new MotionMaster(this);
            m_combatManager = new CombatManager(this);
            m_threatManager = new ThreatManager(this);
            _spellHistory = new SpellHistory(this);

            ObjectTypeId = TypeId.Unit;
            ObjectTypeMask |= TypeMask.Unit;
            m_updateFlag.MovementUpdate = true;

            m_modAttackSpeedPct = [1.0f, 1.0f, 1.0f];
            m_deathState = DeathState.Alive;

            for (byte i = 0; i < (int)SpellImmunity.Max; ++i)
                m_spellImmune[i] = new();

            m_unitStatModManager = new(this);
            m_unitStatModManager.ModifyMult(UnitMods.DamageOffHand, 0.5f, true, UnitModType.TotalPermanent);

            for (byte i = 0; i < (int)WeaponAttackType.Max; ++i)
                m_weaponDamage[i] = [1.0f, 2.0f];

            ModMeleeHitChance = 0.0f;
            ModRangedHitChance = 0.0f;
            ModSpellHitChance = 0.0f;
            BaseSpellCritChance = 5; // integer value to avoid imprecision after modification when it is float (just to be sure)

            for (byte i = 0; i < (int)UnitMoveType.Max; ++i)
                m_speed_rate[i] = 1.0f;

            m_serverSideVisibility.SetValue(ServerSideVisibilityType.Ghost, GhostVisibilityType.Alive);

            splineSyncTimer = new TimeTracker();

            m_unitData = new UnitData();
        }

        public override void Dispose()
        {
            // set current spells as deletable
            for (CurrentSpellTypes i = 0; i < CurrentSpellTypes.Max; ++i)
            {
                if (m_currentSpells.ContainsKey(i))
                {
                    if (m_currentSpells[i] != null)
                    {
                        m_currentSpells[i].SetReferencedFromCurrent(false);
                        m_currentSpells[i] = null;
                    }
                }
            }

            m_Events.KillAllEvents(true);

            _DeleteRemovedAuras();

            //i_motionMaster = null;
            m_charmInfo = null;
            MoveSpline = null;
            _spellHistory = null;

            /*ASSERT(!m_duringRemoveFromWorld);
            ASSERT(!m_attacking);
            ASSERT(m_attackers.empty());
            ASSERT(m_sharedVision.empty());
            ASSERT(m_Controlled.empty());
            ASSERT(m_appliedAuras.empty());
            ASSERT(m_ownedAuras.empty());
            ASSERT(m_removedAuras.empty());
            ASSERT(m_gameObj.empty());
            ASSERT(m_dynObj.empty());
            ASSERT(m_areaTrigger.empty());*/

            base.Dispose();
        }

        public override void Update(TimeSpan diff)
        {
            // WARNING! Order of execution here is important, do not change.
            // Spells must be processed with event system BEFORE they go to _UpdateSpells.
            base.Update(diff);

            if (!IsInWorld)
                return;

            _UpdateSpells((Milliseconds)diff);

            // If this is set during update SetCantProc(false) call is missing somewhere in the code
            // Having this would prevent spells from being proced, so let's crash
            Cypher.Assert(m_procDeep == 0);

            m_combatManager.Update(diff);

            _lastDamagedTargetGuid = ObjectGuid.Empty;
            if (_lastExtraAttackSpell != 0)
            {
                while (!extraAttacksTargets.Empty())
                {
                    var (targetGuid, count) = extraAttacksTargets.FirstOrDefault();
                    extraAttacksTargets.Remove(targetGuid);

                    Unit victim = Global.ObjAccessor.GetUnit(this, targetGuid);
                    if (victim != null)
                        HandleProcExtraAttackFor(victim, count);
                }
                _lastExtraAttackSpell = 0;
            }

            bool spellPausesCombatTimer(CurrentSpellTypes type)
            {
                return GetCurrentSpell(type) != null && GetCurrentSpell(type).GetSpellInfo().HasAttribute(SpellAttr6.DelayCombatTimerDuringCast);
            }

            if (!spellPausesCombatTimer(CurrentSpellTypes.Generic) && !spellPausesCombatTimer(CurrentSpellTypes.Channeled))
            {
                Milliseconds base_att = GetAttackTimer(WeaponAttackType.BaseAttack);
                if (base_att != 0)
                    SetAttackTimer(WeaponAttackType.BaseAttack, diff >= base_att ? Milliseconds.Zero : base_att - diff);

                Milliseconds ranged_att = GetAttackTimer(WeaponAttackType.RangedAttack);
                if (ranged_att != 0)
                    SetAttackTimer(WeaponAttackType.RangedAttack, diff >= ranged_att ? Milliseconds.Zero : ranged_att - diff);

                Milliseconds off_att = GetAttackTimer(WeaponAttackType.OffAttack);
                if (off_att != 0)
                    SetAttackTimer(WeaponAttackType.OffAttack, diff >= off_att ? Milliseconds.Zero : off_att - diff);
            }

            // update abilities available only for fraction of time
            UpdateReactives((Milliseconds)diff);

            if (IsAlive())
            {
                ModifyAuraState(AuraStateType.Wounded20Percent, HealthBelowPct(20));
                ModifyAuraState(AuraStateType.Wounded25Percent, HealthBelowPct(25));
                ModifyAuraState(AuraStateType.Wounded35Percent, HealthBelowPct(35));
                ModifyAuraState(AuraStateType.WoundHealth20_80, HealthBelowPct(20) || HealthAbovePct(80));
                ModifyAuraState(AuraStateType.Healthy75Percent, HealthAbovePct(75));
                ModifyAuraState(AuraStateType.WoundHealth35_80, HealthBelowPct(35) || HealthAbovePct(80));
            }

            UpdateSplineMovement(diff);
            GetMotionMaster().Update(diff);

            // Wait with the aura interrupts until we have updated our movement generators and position
            if (IsPlayer())
                InterruptMovementBasedAuras();
            else if (!MoveSpline.Finalized())
                InterruptMovementBasedAuras();

            // All position info based actions have been executed, reset info
            _positionUpdateInfo.Reset();

            if (HasScheduledAIChange() && (!IsPlayer() || (IsCharmed() && GetCharmerGUID().IsCreature())))
                UpdateCharmAI();

            RefreshAI();
        }

        void _UpdateSpells(Milliseconds diff)
        {
            if (!_spellHistory.IsPaused())
                _spellHistory.Update();

            if (GetCurrentSpell(CurrentSpellTypes.AutoRepeat) != null)
                _UpdateAutoRepeatSpell();

            for (CurrentSpellTypes i = 0; i < CurrentSpellTypes.Max; ++i)
            {
                if (GetCurrentSpell(i) != null && m_currentSpells[i].GetState() == SpellState.Finished)
                {
                    m_currentSpells[i].SetReferencedFromCurrent(false);
                    m_currentSpells[i] = null;
                }
            }

            foreach (var app in GetOwnedAuras())
            {
                Aura i_aura = app.Value;
                if (i_aura == null)
                    continue;

                i_aura.UpdateOwner(diff, this);
            }

            // remove expired auras - do that after updates(used in scripts?)
            foreach (var pair in GetOwnedAuras())
            {
                if (pair.Value != null && pair.Value.IsExpired())
                    RemoveOwnedAura(pair, AuraRemoveMode.Expire);
                else if (pair.Value.GetSpellInfo().IsChanneled() && pair.Value.GetCasterGUID() != GetGUID() && Global.ObjAccessor.GetWorldObject(this, pair.Value.GetCasterGUID()) == null)
                    RemoveOwnedAura(pair, AuraRemoveMode.Cancel); // remove channeled auras when caster is not on the same map
            }

            foreach (var aura in m_visibleAurasToUpdate)
                aura.ClientUpdate();

            m_visibleAurasToUpdate.Clear();

            _DeleteRemovedAuras();

            if (!m_gameObj.Empty())
            {
                for (var i = 0; i < m_gameObj.Count; ++i)
                {
                    GameObject go = m_gameObj[i];
                    if (!go.IsSpawned())
                    {
                        go.SetOwnerGUID(ObjectGuid.Empty);
                        go.SetRespawnTime(TimeSpan.Zero);
                        go.Delete();
                        m_gameObj.Remove(go);
                    }
                }
            }
        }

        public void HandleEmoteCommand(Emote emoteId, Player target = null, int[] spellVisualKitIds = null, int sequenceVariation = 0)
        {
            EmoteMessage packet = new();
            packet.Guid = GetGUID();
            packet.EmoteID = emoteId;

            var emotesEntry = CliDB.EmotesStorage.LookupByKey((int)emoteId);
            if (emotesEntry != null && spellVisualKitIds != null)
            {
                if (emotesEntry.AnimId == Anim.MountSpecial || emotesEntry.AnimId == Anim.MountSelfSpecial)
                    packet.SpellVisualKitIDs.AddRange(spellVisualKitIds);
            }

            packet.SequenceVariation = sequenceVariation;

            if (target != null)
                target.SendPacket(packet);
            else
                SendMessageToSet(packet, true);
        }

        public void SendDurabilityLoss(Player receiver, int percent)
        {
            DurabilityDamageDeath packet = new();
            packet.Percent = percent;
            receiver.SendPacket(packet);
        }

        public bool IsInDisallowedMountForm()
        {
            return IsDisallowedMountForm(GetTransformSpell(), GetShapeshiftForm(), GetDisplayId());
        }

        public bool IsDisallowedMountForm(int spellId, ShapeShiftForm form, int displayId)
        {
            SpellInfo transformSpellInfo = Global.SpellMgr.GetSpellInfo(spellId, GetMap().GetDifficultyID());
            if (transformSpellInfo != null)
                if (transformSpellInfo.HasAttribute(SpellAttr0.AllowWhileMounted))
                    return false;

            if (form != 0)
            {
                SpellShapeshiftFormRecord shapeshift = CliDB.SpellShapeshiftFormStorage.LookupByKey((int)form);
                if (shapeshift == null)
                    return true;

                if (!shapeshift.HasFlag(SpellShapeshiftFormFlags.Stance))
                    return true;
            }
            if (displayId == GetNativeDisplayId())
                return false;

            CreatureDisplayInfoRecord display = CliDB.CreatureDisplayInfoStorage.LookupByKey(displayId);
            if (display == null)
                return true;

            CreatureDisplayInfoExtraRecord displayExtra = CliDB.CreatureDisplayInfoExtraStorage.LookupByKey(display.ExtendedDisplayInfoID);
            if (displayExtra == null)
                return true;

            CreatureModelDataRecord model = CliDB.CreatureModelDataStorage.LookupByKey(display.ModelID);
            ChrRacesRecord race = CliDB.ChrRacesStorage.LookupByKey(displayExtra.DisplayRaceID);

            if (model != null && !model.HasFlag(CreatureModelDataFlags.CanMountWhileTransformedAsThis))
            {
                if (race != null && !race.HasFlag(ChrRacesFlag.CanMount))
                    return true;
            }

            return false;
        }

        public void SendClearTarget()
        {
            BreakTarget breakTarget = new();
            breakTarget.UnitGUID = GetGUID();
            SendMessageToSet(breakTarget, false);
        }

        public virtual bool IsLoading() { return false; }
        public bool IsDuringRemoveFromWorld() { return m_duringRemoveFromWorld; }

        //SharedVision
        public bool HasSharedVision() { return !m_sharedVision.Empty(); }
        public List<Player> GetSharedVisionList() { return m_sharedVision; }

        public void AddPlayerToVision(Player player)
        {
            if (m_sharedVision.Empty())
            {
                SetActive(true);
                SetIsStoredInWorldObjectGridContainer(true);
            }
            m_sharedVision.Add(player);
        }

        // only called in Player.SetSeer
        public void RemovePlayerFromVision(Player player)
        {
            m_sharedVision.Remove(player);
            if (m_sharedVision.Empty())
            {
                SetActive(false);
                SetIsStoredInWorldObjectGridContainer(false);
            }
        }

        public virtual void Talk(string text, ChatMsg msgType, Language language, float textRange, WorldObject target)
        {
            var builder = new CustomChatTextBuilder(this, msgType, text, language, target);
            var localizer = new LocalizedDo(builder);
            var worker = new PlayerDistWorker(this, textRange, localizer);
            Cell.VisitWorldObjects(this, worker, textRange);
        }

        public virtual void Say(string text, Language language, WorldObject target = null)
        {
            Talk(text, ChatMsg.MonsterSay, language, WorldConfig.Values[WorldCfg.ListenRangeSay].Float, target);
        }

        public virtual void Yell(string text, Language language, WorldObject target = null)
        {
            Talk(text, ChatMsg.MonsterYell, language, WorldConfig.Values[WorldCfg.ListenRangeYell].Float, target);
        }

        public virtual void TextEmote(string text, WorldObject target = null, bool isBossEmote = false)
        {
            Talk(text, isBossEmote ? ChatMsg.RaidBossEmote : ChatMsg.MonsterEmote, Language.Universal, WorldConfig.Values[WorldCfg.ListenRangeTextemote].Float, target);
        }

        public virtual void Whisper(string text, Language language, Player target, bool isBossWhisper = false)
        {
            if (target == null)
                return;

            Locale locale = target.GetSession().GetSessionDbLocaleIndex();
            ChatPkt data = new();
            data.Initialize(isBossWhisper ? ChatMsg.RaidBossWhisper : ChatMsg.MonsterWhisper, Language.Universal, this, target, text, 0, "", locale);
            target.SendPacket(data);
        }

        public void Talk(int textId, ChatMsg msgType, float textRange, WorldObject target)
        {
            if (!CliDB.BroadcastTextStorage.ContainsKey(textId))
            {
                Log.outError(LogFilter.Unit,
                    $"Unit.Talk: `broadcast_text` (Id: {textId}) was not found");
                return;
            }

            var builder = new BroadcastTextBuilder(this, msgType, textId, GetGender(), target);
            var localizer = new LocalizedDo(builder);
            var worker = new PlayerDistWorker(this, textRange, localizer);
            Cell.VisitWorldObjects(this, worker, textRange);
        }

        public virtual void Say(int textId, WorldObject target = null)
        {
            Talk(textId, ChatMsg.MonsterSay, WorldConfig.Values[WorldCfg.ListenRangeSay].Float, target);
        }

        public virtual void Yell(int textId, WorldObject target = null)
        {
            Talk(textId, ChatMsg.MonsterYell, WorldConfig.Values[WorldCfg.ListenRangeYell].Float, target);
        }

        public virtual void TextEmote(int textId, WorldObject target = null, bool isBossEmote = false)
        {
            Talk(textId, isBossEmote ? ChatMsg.RaidBossEmote : ChatMsg.MonsterEmote, WorldConfig.Values[WorldCfg.ListenRangeTextemote].Float, target);
        }

        public virtual void Whisper(int textId, Player target, bool isBossWhisper = false)
        {
            if (target == null)
                return;

            BroadcastTextRecord bct = CliDB.BroadcastTextStorage.LookupByKey(textId);
            if (bct == null)
            {
                Log.outError(LogFilter.Unit, 
                    $"Unit.Whisper: `broadcast_text` was not {textId} found");

                return;
            }

            Locale locale = target.GetSession().GetSessionDbLocaleIndex();
            ChatPkt data = new();
            data.Initialize(isBossWhisper ? ChatMsg.RaidBossWhisper : ChatMsg.MonsterWhisper, Language.Universal, this, target, Global.DB2Mgr.GetBroadcastTextValue(bct, locale, GetGender()), 0, "", locale);
            target.SendPacket(data);
        }

        /// <summary>
        /// Clears boss emotes frame
        /// </summary>
        /// <param name="zoneId">Only clears emotes for players in that zone id</param>
        /// <param name="target">Only clears emotes for that player</param>
        public void ClearBossEmotes(int? zoneId, Player target)
        {
            ClearBossEmotes clearBossEmotes = new();

            if (target != null)
            {
                target.SendPacket(clearBossEmotes);
                return;
            }

            foreach (var player in GetMap().GetPlayers())
            {
                if (!zoneId.HasValue || Global.DB2Mgr.IsInArea(player.GetAreaId(), zoneId.Value))
                    player.SendPacket(clearBossEmotes);
            }
        }

        public override void UpdateObjectVisibility(bool forced = true)
        {
            if (!forced)
                AddToNotify(NotifyFlags.VisibilityChanged);
            else
            {
                base.UpdateObjectVisibility(true);
                // call MoveInLineOfSight for nearby creatures
                AIRelocationNotifier notifier = new(this);
                Cell.VisitAllObjects(this, notifier, GetVisibilityRange());
            }
        }

        public override void AddToWorld()
        {
            base.AddToWorld();
            i_motionMaster.AddToWorld();

            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.EnterWorld);
        }

        public override void RemoveFromWorld()
        {
            // cleanup

            if (IsInWorld)
            {
                if (IsAreaSpiritHealer())
                    ToCreature()?.SummonGraveyardTeleporter();

                m_duringRemoveFromWorld = true;
                UnitAI ai = GetAI();
                if (ai != null)
                    ai.OnDespawn();

                if (IsVehicle())
                    RemoveVehicleKit(true);

                RemoveCharmAuras();
                RemoveAurasByType(AuraType.BindSight);
                RemoveNotOwnSingleTargetAuras();
                RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.LeaveWorld);

                RemoveAllGameObjects();
                RemoveAllDynObjects();
                RemoveAllAreaTriggers();

                ExitVehicle();  // Remove applied auras with SPELL_AURA_CONTROL_VEHICLE
                UnsummonAllTotems();
                RemoveAllControlled();

                RemoveAreaAurasDueToLeaveWorld();

                RemoveAllFollowers();

                if (IsCharmed())
                    RemoveCharmedBy(null);

                Cypher.Assert(GetCharmedGUID().IsEmpty(), 
                    $"Unit {GetEntry()} has charmed guid when removed from world");

                Cypher.Assert(GetCharmerGUID().IsEmpty(), 
                    $"Unit {GetEntry()} has charmer guid when removed from world");

                Unit owner = GetOwner();
                if (owner != null)
                {
                    if (owner.m_Controlled.Contains(this))
                    {
                        Log.outFatal(LogFilter.Unit, 
                            $"Unit {GetEntry()} is in controlled list of {owner.GetEntry()} when removed from world");
                    }
                }

                base.RemoveFromWorld();
                m_duringRemoveFromWorld = false;
            }
        }

        public void CleanupBeforeRemoveFromMap(bool finalCleanup)
        {
            // This needs to be before RemoveFromWorld to make GetCaster() return a valid for aura removal
            InterruptNonMeleeSpells(true);

            if (IsInWorld)
                RemoveFromWorld();
            else
            {
                // cleanup that must happen even if not in world
                if (IsVehicle())
                    RemoveVehicleKit(true);
            }

            // A unit may be in removelist and not in world, but it is still in grid
            // and may have some references during delete
            RemoveAllAuras();
            RemoveAllGameObjects();

            if (finalCleanup)
                m_cleanupDone = true;

            CombatStop();
        }

        public override void CleanupsBeforeDelete(bool finalCleanup = true)
        {
            CleanupBeforeRemoveFromMap(finalCleanup);

            base.CleanupsBeforeDelete(finalCleanup);
        }

        public void SetTransformSpell(int spellid) { m_transformSpell = spellid; }
        public int GetTransformSpell() { return m_transformSpell; }

        public Vehicle GetVehicleKit() { return VehicleKit; }
        public Vehicle GetVehicle() { return m_vehicle; }
        public void SetVehicle(Vehicle vehicle) { m_vehicle = vehicle; }

        public Unit GetVehicleBase()
        {
            return m_vehicle != null ? m_vehicle.GetBase() : null;
        }

        Unit GetVehicleRoot()
        {
            Unit vehicleRoot = GetVehicleBase();

            if (vehicleRoot == null)
                return null;

            for (; ; )
            {
                if (vehicleRoot.GetVehicleBase() == null)
                    return vehicleRoot;

                vehicleRoot = vehicleRoot.GetVehicleBase();
            }
        }

        public Creature GetVehicleCreatureBase()
        {
            Unit veh = GetVehicleBase();
            if (veh != null)
            {
                Creature c = veh.ToCreature();
                if (c != null)
                    return c;
            }
            return null;
        }

        public ITransport GetDirectTransport()
        {
            Vehicle veh = GetVehicle();
            if (veh != null)
                return veh;

            return GetTransport();
        }

        public void AtStartOfEncounter(EncounterType type)
        {
            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.StartOfEncounter);

            switch (type)
            {
                case EncounterType.DungeonEncounter:
                    RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.StartOfDungeonEncounter);
                    break;
                case EncounterType.MythicPlusRun:
                    RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.StartOfMythicPlusRun);
                    break;
                default:
                    break;
            }

            if (IsAlive())
            {
                ProcSkillsAndAuras(this, null,
                    new ProcFlagsInit(ProcFlags.EncounterStart),
                    new ProcFlagsInit(),
                    ProcFlagsSpellType.MaskAll, ProcFlagsSpellPhase.None, ProcFlagsHit.None,
                    null, null, null);
            }
        }

        public void AtEndOfEncounter(EncounterType type)
        {
            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.EndOfEncounter);

            switch (type)
            {
                case EncounterType.DungeonEncounter:
                    RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.EndOfDungeonEncounter);
                    break;
                default:
                    break;
            }

            GetSpellHistory().ResetCooldowns(pair =>
            {
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(pair.Key, Difficulty.None);
                return spellInfo.HasAttribute(SpellAttr10.ResetCooldownOnEncounterEnd);
            }, true);
        }

        public void _RegisterDynObject(DynamicObject dynObj)
        {
            m_dynObj.Add(dynObj);
            if (IsTypeId(TypeId.Unit) && IsAIEnabled())
                ToCreature().GetAI().JustRegisteredDynObject(dynObj);
        }

        public void _UnregisterDynObject(DynamicObject dynObj)
        {
            m_dynObj.Remove(dynObj);
            if (IsTypeId(TypeId.Unit) && IsAIEnabled())
                ToCreature().GetAI().JustUnregisteredDynObject(dynObj);
        }

        public DynamicObject GetDynObject(int spellId)
        {
            return GetDynObjects(spellId).FirstOrDefault();
        }

        List<DynamicObject> GetDynObjects(int spellId)
        {
            List<DynamicObject> dynamicobjects = new();
            foreach (var obj in m_dynObj)
                if (obj.GetSpellId() == spellId)
                    dynamicobjects.Add(obj);

            return dynamicobjects;
        }

        public void RemoveDynObject(int spellId)
        {
            for (var i = 0; i < m_dynObj.Count; ++i)
            {
                var dynObj = m_dynObj[i];
                if (dynObj.GetSpellId() == spellId)
                    dynObj.Remove();
            }
        }

        public void RemoveAllDynObjects()
        {
            while (!m_dynObj.Empty())
                m_dynObj.Last().Remove();
        }

        public GameObject GetGameObject(int spellId)
        {
            return GetGameObjects(spellId).FirstOrDefault();
        }

        List<GameObject> GetGameObjects(int spellId)
        {
            List<GameObject> gameobjects = new();
            foreach (var obj in m_gameObj)
            {
                if (obj.GetSpellId() == spellId)
                    gameobjects.Add(obj);
            }

            return gameobjects;
        }

        public void AddGameObject(GameObject gameObj)
        {
            if (gameObj == null || !gameObj.GetOwnerGUID().IsEmpty())
                return;

            m_gameObj.Add(gameObj);
            gameObj.SetOwnerGUID(GetGUID());

            if (gameObj.GetSpellId() != 0)
            {
                SpellInfo createBySpell = 
                    Global.SpellMgr.GetSpellInfo(gameObj.GetSpellId(), GetMap().GetDifficultyID());

                // Need disable spell use for owner
                if (createBySpell != null && createBySpell.IsCooldownStartedOnEvent())
                {
                    // note: item based cooldowns and cooldown spell mods
                    // with charges ignored (unknown existing cases)
                    GetSpellHistory().StartCooldown(createBySpell, 0, null, true);
                }
            }

            if (IsTypeId(TypeId.Unit) && ToCreature().IsAIEnabled())
                ToCreature().GetAI().JustSummonedGameobject(gameObj);
        }

        public void RemoveGameObject(GameObject gameObj, bool del)
        {
            if (gameObj == null || gameObj.GetOwnerGUID() != GetGUID())
                return;

            gameObj.SetOwnerGUID(ObjectGuid.Empty);

            for (byte i = 0; i < SharedConst.MaxGameObjectSlot; ++i)
            {
                if (m_ObjectSlot[i] == gameObj.GetGUID())
                {
                    m_ObjectSlot[i].Clear();
                    break;
                }
            }

            // GO created by some spell
            int spellid = gameObj.GetSpellId();
            if (spellid != 0)
            {
                RemoveAurasDueToSpell(spellid);

                SpellInfo createBySpell = Global.SpellMgr.GetSpellInfo(spellid, GetMap().GetDifficultyID());
                // Need activate spell use for owner
                if (createBySpell != null && createBySpell.IsCooldownStartedOnEvent())
                {
                    // note: item based cooldowns and cooldown spell mods
                    // with charges ignored (unknown existing cases)
                    GetSpellHistory().SendCooldownEvent(createBySpell);
                }
            }

            m_gameObj.Remove(gameObj);

            if (IsTypeId(TypeId.Unit) && ToCreature().IsAIEnabled())
                ToCreature().GetAI().SummonedGameobjectDespawn(gameObj);

            if (del)
            {
                gameObj.SetRespawnTime(TimeSpan.Zero);
                gameObj.Delete();
            }
        }

        public void RemoveGameObject(int spellid, bool del)
        {
            if (m_gameObj.Empty())
                return;

            for (var i = 0; i < m_gameObj.Count; ++i)
            {
                var obj = m_gameObj[i];
                if (spellid == 0 || obj.GetSpellId() == spellid)
                {
                    obj.SetOwnerGUID(ObjectGuid.Empty);
                    if (del)
                    {
                        obj.SetRespawnTime(TimeSpan.Zero);
                        obj.Delete();
                    }

                    m_gameObj.Remove(obj);
                }
            }
        }

        public void RemoveAllGameObjects()
        {
            // remove references to unit
            while (!m_gameObj.Empty())
            {
                var obj = m_gameObj.First();
                obj.SetOwnerGUID(ObjectGuid.Empty);
                obj.SetRespawnTime(TimeSpan.Zero);
                obj.Delete();
                m_gameObj.Remove(obj);
            }
        }

        public void _RegisterAreaTrigger(AreaTrigger areaTrigger)
        {
            m_areaTrigger.Add(areaTrigger);
            if (IsTypeId(TypeId.Unit) && IsAIEnabled())
                ToCreature().GetAI().JustRegisteredAreaTrigger(areaTrigger);
        }

        public void _UnregisterAreaTrigger(AreaTrigger areaTrigger)
        {
            m_areaTrigger.Remove(areaTrigger);
            if (IsTypeId(TypeId.Unit) && IsAIEnabled())
                ToCreature().GetAI().JustUnregisteredAreaTrigger(areaTrigger);
        }

        public AreaTrigger GetAreaTrigger(int spellId)
        {
            List<AreaTrigger> areaTriggers = GetAreaTriggers(spellId);
            return areaTriggers.Empty() ? null : areaTriggers[0];
        }

        public List<AreaTrigger> GetAreaTriggers(int spellId)
        {
            return m_areaTrigger.Where(trigger => trigger.GetSpellId() == spellId).ToList();
        }

        public void RemoveAreaTrigger(int spellId)
        {
            for (var i = 0; i < m_areaTrigger.Count; ++i)
            {
                AreaTrigger areaTrigger = m_areaTrigger[i];
                if (areaTrigger.GetSpellId() == spellId)
                    areaTrigger.Remove();
            }
        }

        public void RemoveAreaTrigger(AuraEffect aurEff)
        {
            foreach (AreaTrigger areaTrigger in m_areaTrigger)
            {
                if (areaTrigger.GetAuraEffect() == aurEff)
                {
                    areaTrigger.Remove();
                    break; // There can only be one AreaTrigger per AuraEffect
                }
            }
        }

        public void RemoveAllAreaTriggers()
        {
            while (!m_areaTrigger.Empty())
                m_areaTrigger.Last()?.Remove();
        }

        public NPCFlags1 GetNpcFlags() { return (NPCFlags1)m_unitData.NpcFlags[0]; }
        public bool HasNpcFlag(NPCFlags1 flags) { return (m_unitData.NpcFlags[0] & (uint)flags) != 0; }
        public void SetNpcFlag(NPCFlags1 flags) { SetUpdateFieldFlagValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.NpcFlags, 0), (uint)flags); }
        public void RemoveNpcFlag(NPCFlags1 flags) { RemoveUpdateFieldFlagValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.NpcFlags, 0), (uint)flags); }
        public void ReplaceAllNpcFlags(NPCFlags1 flags) { SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.NpcFlags, 0), (uint)flags); }

        public NPCFlags2 GetNpcFlags2() { return (NPCFlags2)m_unitData.NpcFlags[1]; }
        public bool HasNpcFlag2(NPCFlags2 flags) { return (m_unitData.NpcFlags[1] & (uint)flags) != 0; }
        public void SetNpcFlag2(NPCFlags2 flags) { SetUpdateFieldFlagValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.NpcFlags, 1), (uint)flags); }
        public void RemoveNpcFlag2(NPCFlags2 flags) { RemoveUpdateFieldFlagValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.NpcFlags, 1), (uint)flags); }
        public void ReplaceAllNpcFlags2(NPCFlags2 flags) { SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.NpcFlags, 1), (uint)flags); }

        public bool IsVendor() { return HasNpcFlag(NPCFlags1.Vendor); }
        public bool IsTrainer() { return HasNpcFlag(NPCFlags1.Trainer); }
        public bool IsQuestGiver() { return HasNpcFlag(NPCFlags1.QuestGiver); }
        public bool IsGossip() { return HasNpcFlag(NPCFlags1.Gossip); }
        public bool IsTaxi() { return HasNpcFlag(NPCFlags1.FlightMaster); }
        public bool IsGuildMaster() { return HasNpcFlag(NPCFlags1.Petitioner); }
        public bool IsBattleMaster() { return HasNpcFlag(NPCFlags1.BattleMaster); }
        public bool IsBanker() { return HasNpcFlag(NPCFlags1.Banker); }
        public bool IsInnkeeper() { return HasNpcFlag(NPCFlags1.Innkeeper); }
        public bool IsSpiritHealer() { return HasNpcFlag(NPCFlags1.SpiritHealer); }
        public bool IsAreaSpiritHealer() { return HasNpcFlag(NPCFlags1.AreaSpiritHealer); }
        public bool IsTabardDesigner() { return HasNpcFlag(NPCFlags1.TabardDesigner); }
        public bool IsAuctioner() { return HasNpcFlag(NPCFlags1.Auctioneer); }
        public bool IsArmorer() { return HasNpcFlag(NPCFlags1.Repair); }
        public bool IsWildBattlePet() { return HasNpcFlag(NPCFlags1.WildBattlePet); }
        
        public bool IsServiceProvider()
        {
            return HasNpcFlag(NPCFlags1.Vendor | NPCFlags1.Trainer | NPCFlags1.FlightMaster |
                NPCFlags1.Petitioner | NPCFlags1.BattleMaster | NPCFlags1.Banker |
                NPCFlags1.Innkeeper | NPCFlags1.SpiritHealer |
                NPCFlags1.AreaSpiritHealer | NPCFlags1.TabardDesigner | NPCFlags1.Auctioneer);
        }

        public bool IsSpiritService() { return HasNpcFlag(NPCFlags1.SpiritHealer | NPCFlags1.AreaSpiritHealer); }
        public bool IsAreaSpiritHealerIndividual() { return HasNpcFlag2(NPCFlags2.AreaSpiritHealerIndividual); }
        public bool IsCritter() { return GetCreatureType() == CreatureType.Critter; }
        public bool IsInFlight() { return HasUnitState(UnitState.InFlight); }

        public bool IsContestedGuard()
        {
            var entry = GetFactionTemplateEntry();
            if (entry != null)
                return entry.IsContestedGuardFaction;

            return false;
        }

        public void SetHoverHeight(float hoverHeight) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.HoverHeight), hoverHeight); 
        }

        public override float GetCollisionHeight()
        {
            float scaleMod = GetObjectScale(); // 99% sure about this

            if (IsMounted())
            {
                var mountDisplayInfo = CliDB.CreatureDisplayInfoStorage.LookupByKey(GetMountDisplayId());
                if (mountDisplayInfo != null)
                {
                    var mountModelData = CliDB.CreatureModelDataStorage.LookupByKey(mountDisplayInfo.ModelID);
                    if (mountModelData != null)
                    {
                        var displayInfo = CliDB.CreatureDisplayInfoStorage.LookupByKey(GetNativeDisplayId());
                        var modelData = CliDB.CreatureModelDataStorage.LookupByKey(displayInfo.ModelID);
                        float collisionHeight = scaleMod * ((mountModelData.MountHeight * mountDisplayInfo.CreatureModelScale) + (modelData.CollisionHeight * modelData.ModelScale * displayInfo.CreatureModelScale * 0.5f));
                        return collisionHeight == 0.0f ? MapConst.DefaultCollesionHeight : collisionHeight;
                    }
                }
            }

            //! Dismounting case - use basic default model data
            var defaultDisplayInfo = CliDB.CreatureDisplayInfoStorage.LookupByKey(GetNativeDisplayId());
            var defaultModelData = CliDB.CreatureModelDataStorage.LookupByKey(defaultDisplayInfo.ModelID);

            float collisionHeight1 = scaleMod * defaultModelData.CollisionHeight * defaultModelData.ModelScale * defaultDisplayInfo.CreatureModelScale;
            return collisionHeight1 == 0.0f ? MapConst.DefaultCollesionHeight : collisionHeight1;
        }

        public override string GetDebugInfo()
        {
            string str = $"{base.GetDebugInfo()}\nIsAIEnabled: {IsAIEnabled()} DeathState: {GetDeathState()} UnitMovementFlags: {GetUnitMovementFlags()} UnitMovementFlags2: {GetUnitMovementFlags2()} Class: {GetClass()}\n" +
                    $" {(MoveSpline != null ? MoveSpline.ToString() : "Movespline: <none>\n")} GetCharmedGUID(): {GetCharmedGUID()}\nGetCharmerGUID(): {GetCharmerGUID()}\n{(GetVehicleKit() != null ? GetVehicleKit().GetDebugInfo() : "No vehicle kit")}\n" +
                    $"m_Controlled size: {m_Controlled.Count}";

            int controlledCount = 0;
            foreach (Unit controlled in m_Controlled)
            {
                ++controlledCount;
                str += $"\nm_Controlled {controlledCount} : {controlled.GetGUID()}";
            }

            return str;
        }

        public Guardian GetGuardianPet()
        {
            ObjectGuid pet_guid = GetPetGUID();
            if (!pet_guid.IsEmpty())
            {
                Creature pet = ObjectAccessor.GetCreatureOrPetOrVehicle(this, pet_guid);
                if (pet != null)
                {
                    if (pet.HasUnitTypeMask(UnitTypeMask.Guardian))
                        return (Guardian)pet;
                }

                Log.outFatal(LogFilter.Unit, $"Unit:GetGuardianPet: Guardian {pet_guid} not exist.");
                SetPetGUID(ObjectGuid.Empty);
            }

            return null;
        }

        public Unit SelectNearbyTarget(Unit exclude = null, float dist = SharedConst.NominalMeleeRange)
        {
            List<Unit> targets = new();
            var u_check = new AnyUnfriendlyUnitInObjectRangeCheck(this, this, dist);
            var searcher = new UnitListSearcher(this, targets, u_check);
            Cell.VisitAllObjects(this, searcher, dist);

            // remove current target
            if (GetVictim() != null)
                targets.Remove(GetVictim());

            if (exclude != null)
                targets.Remove(exclude);

            // remove not LoS targets
            foreach (var unit in targets)
            {
                if (!IsWithinLOSInMap(unit) || unit.IsTotem() || unit.IsSpiritService() || unit.IsCritter())
                    targets.Remove(unit);
            }

            // no appropriate targets
            if (targets.Empty())
                return null;

            // select random
            return targets.SelectRandom();
        }

        public void EnterVehicle(Unit baseUnit, sbyte seatId = -1)
        {
            CastSpellExtraArgs args = new(TriggerCastFlags.IgnoreCasterMountedOrOnVehicle);
            args.AddSpellMod(SpellValueMod.BasePoint0, seatId + 1);
            CastSpell(baseUnit, SharedConst.VehicleSpellRideHardcoded, args);
        }

        public void _EnterVehicle(Vehicle vehicle, sbyte seatId, AuraApplication aurApp)
        {
            // Must be called only from aura handler
            Cypher.Assert(aurApp != null);

            if (!IsAlive() || GetVehicleKit() == vehicle || vehicle.GetBase().IsOnVehicle(this))
                return;

            if (m_vehicle != null)
            {
                if (m_vehicle != vehicle)
                {
                    Log.outDebug(LogFilter.Vehicle, 
                        $"EnterVehicle: {GetEntry()} exit {m_vehicle.GetBase().GetEntry()} " +
                        $"and enter {vehicle.GetBase().GetEntry()}.");

                    ExitVehicle();
                }
                else if (seatId >= 0 && seatId == GetTransSeat())
                    return;
                else
                {
                    //Exit the current vehicle because unit will reenter in a new seat.
                    m_vehicle.GetBase().RemoveAurasByType(AuraType.ControlVehicle, GetGUID(), aurApp.GetBase());
                }
            }

            if (aurApp.HasRemoveMode())
                return;

            Player player = ToPlayer();
            if (player != null)
            {
                if (vehicle.GetBase().IsTypeId(TypeId.Player) && player.IsInCombat())
                {
                    vehicle.GetBase().RemoveAura(aurApp);
                    return;
                }

                if (vehicle.GetBase().IsCreature())
                {
                    // If a player entered a vehicle that is part of a formation, remove it from said formation
                    CreatureGroup creatureGroup = vehicle.GetBase().ToCreature().GetFormation();
                    if (creatureGroup != null)
                        creatureGroup.RemoveMember(vehicle.GetBase().ToCreature());
                }
            }

            Cypher.Assert(m_vehicle == null);
            vehicle.AddVehiclePassenger(this, seatId);
        }

        public void ChangeSeat(sbyte seatId, bool next = true)
        {
            if (m_vehicle == null)
                return;

            // Don't change if current and new seat are identical
            if (seatId == GetTransSeat())
                return;

            var seat = (seatId < 0 ? m_vehicle.GetNextEmptySeat(GetTransSeat(), next) : m_vehicle.Seats.LookupByKey(seatId));
            // The second part of the check will only return true if seatId >= 0. @Vehicle.GetNextEmptySeat makes sure of that.
            if (seat == null || !seat.IsEmpty())
                return;

            AuraEffect rideVehicleEffect = null;
            var vehicleAuras = m_vehicle.GetBase().GetAuraEffectsByType(AuraType.ControlVehicle);
            foreach (var eff in vehicleAuras)
            {
                if (eff.GetCasterGUID() != GetGUID())
                    continue;

                // Make sure there is only one ride vehicle aura on target cast by the unit changing seat
                Cypher.Assert(rideVehicleEffect == null);
                rideVehicleEffect = eff;
            }

            // Unit riding a vehicle must always have control vehicle aura on target
            Cypher.Assert(rideVehicleEffect != null);

            rideVehicleEffect.ChangeAmount((seatId < 0 ? GetTransSeat() : seatId) + 1);
        }

        public virtual void ExitVehicle(Position exitPosition = null)
        {
            //! This function can be called at upper level code to initialize an exit from the passenger's side.
            if (m_vehicle == null)
                return;

            GetVehicleBase().RemoveAurasByType(AuraType.ControlVehicle, GetGUID());
            //! The following call would not even be executed successfully as the
            //! SPELL_AURA_CONTROL_VEHICLE unapply handler already calls _ExitVehicle without
            //! specifying an exitposition. The subsequent call below would return on if (!m_vehicle).

            //! To do:
            //! We need to allow SPELL_AURA_CONTROL_VEHICLE unapply handlers in spellscripts
            //! to specify exit coordinates and either store those per passenger, or we need to
            //! init spline movement based on those coordinates in unapply handlers, and
            //! relocate exiting passengers based on Unit.moveSpline data. Either way,
            //! Coming Soon(TM)
        }

        public void _ExitVehicle(Position exitPosition = null)
        {
            // It's possible m_vehicle is NULL, when this function is called indirectly from @VehicleJoinEvent.Abort.
            // In that case it was not possible to add the passenger to the vehicle. The vehicle aura has already been removed
            // from the target in the aforementioned function and we don't need to do anything else at this point.
            if (m_vehicle == null)
                return;

            // This should be done before dismiss, because there may be some aura removal
            VehicleSeatAddon seatAddon = m_vehicle.GetSeatAddonForSeatOfPassenger(this);
            Vehicle vehicle = (Vehicle)m_vehicle.RemovePassenger(this);

            if (vehicle == null)
            {
                Log.outError(LogFilter.Vehicle,
                    $"RemovePassenger() couldn't remove current unit from vehicle. " +
                    $"Debug info: {GetDebugInfo()}");
                return;
            }

            Player player = ToPlayer();

            // If the player is on mounted duel and exits the mount, he should immediatly lose the duel
            if (player != null && player.duel != null && player.duel.IsMounted)
                player.DuelComplete(DuelCompleteType.Fled);

            SetControlled(false, UnitState.Root);      // SMSG_MOVE_FORCE_UNROOT, ~MOVEMENTFLAG_ROOT

            AddUnitState(UnitState.Move);

            if (player != null)
                player.SetFallInformation(0, GetPositionZ());

            Position pos;
            // If we ask for a specific exit position, use that one. Otherwise allow scripts to modify it
            if (exitPosition != null)
                pos = exitPosition;
            else
            {
                // Set exit position to vehicle position and use the current orientation
                pos = vehicle.GetBase().GetPosition();
                pos.SetOrientation(GetOrientation());

                // Change exit position based on seat entry addon data
                if (seatAddon != null)
                {
                    if (seatAddon.ExitParameter == VehicleExitParameters.VehicleExitParamOffset)
                    {
                        pos.RelocateOffset(
                            new Position(seatAddon.ExitParameterX, seatAddon.ExitParameterY, seatAddon.ExitParameterZ,
                            seatAddon.ExitParameterO));
                    }
                    else if (seatAddon.ExitParameter == VehicleExitParameters.VehicleExitParamDest)
                    {
                        pos.Relocate(
                            new Position(seatAddon.ExitParameterX, seatAddon.ExitParameterY, seatAddon.ExitParameterZ, 
                            seatAddon.ExitParameterO));
                    }
                }
            }

            var initializer = (MoveSplineInit init) =>
            {
                float height = pos.GetPositionZ() + vehicle.GetBase().GetCollisionHeight();

                // Creatures without inhabit Type air should begin falling after exiting the vehicle
                if (IsTypeId(TypeId.Unit) && !CanFly() && height > GetMap().GetWaterOrGroundLevel(GetPhaseShift(), pos.GetPositionX(), pos.GetPositionY(), pos.GetPositionZ() + vehicle.GetBase().GetCollisionHeight(), ref height))
                    init.SetFall();

                init.MoveTo(pos.GetPositionX(), pos.GetPositionY(), height, false);
                init.SetFacing(pos.GetOrientation());
                init.SetTransportExit();
            };

            GetMotionMaster().LaunchMoveSpline(initializer, EventId.VehicleExit, MovementGeneratorPriority.Highest);

            if (player != null)
                player.ResummonPetTemporaryUnSummonedIfAny();

            if (vehicle.GetBase().HasUnitTypeMask(UnitTypeMask.Minion) && vehicle.GetBase().IsTypeId(TypeId.Unit))
                if (((Minion)vehicle.GetBase()).GetOwner() == this)
                    vehicle.GetBase().ToCreature().DespawnOrUnsummon(vehicle.GetDespawnDelay());

            if (HasUnitTypeMask(UnitTypeMask.Accessory))
            {
                // Vehicle just died, we die too
                if (vehicle.GetBase().GetDeathState() == DeathState.JustDied)
                    SetDeathState(DeathState.JustDied);
                // If for other reason we as minion are exiting the vehicle (ejected, master dismounted) - unsummon
                else
                    ToTempSummon().UnSummon((Seconds)2); // Approximation
            }

            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.AbandonVehicle);
        }

        public void SendPlaySpellVisual(Unit target, int spellVisualId, ushort missReason, ushort reflectStatus, float travelSpeed, bool speedAsTime = false, float launchDelay = 0f)
        {
            PlaySpellVisual playSpellVisual = new();
            playSpellVisual.Source = GetGUID();
            playSpellVisual.Target = target.GetGUID();
            playSpellVisual.TargetPosition = target.GetPosition();
            playSpellVisual.SpellVisualID = spellVisualId;
            playSpellVisual.TravelSpeed = travelSpeed;
            playSpellVisual.MissReason = missReason;
            playSpellVisual.ReflectStatus = reflectStatus;
            playSpellVisual.SpeedAsTime = speedAsTime;
            playSpellVisual.LaunchDelay = launchDelay;
            SendMessageToSet(playSpellVisual, true);
        }

        public void SendPlaySpellVisual(Position targetPosition, int spellVisualId, ushort missReason, ushort reflectStatus, float travelSpeed, bool speedAsTime = false, float launchDelay = 0f)
        {
            PlaySpellVisual playSpellVisual = new();
            playSpellVisual.Source = GetGUID();
            playSpellVisual.TargetPosition = targetPosition;
            playSpellVisual.SpellVisualID = spellVisualId;
            playSpellVisual.TravelSpeed = travelSpeed;
            playSpellVisual.MissReason = missReason;
            playSpellVisual.ReflectStatus = reflectStatus;
            playSpellVisual.SpeedAsTime = speedAsTime;
            playSpellVisual.LaunchDelay = launchDelay;
            SendMessageToSet(playSpellVisual, true);
        }

        public void SendCancelSpellVisual(int id)
        {
            CancelSpellVisual cancelSpellVisual = new();
            cancelSpellVisual.Source = GetGUID();
            cancelSpellVisual.SpellVisualID = id;
            SendMessageToSet(cancelSpellVisual, true);
        }

        public void SendPlaySpellVisualKit(int id, int type, uint duration)
        {
            PlaySpellVisualKit playSpellVisualKit = new();
            playSpellVisualKit.Unit = GetGUID();
            playSpellVisualKit.KitRecID = id;
            playSpellVisualKit.KitType = type;
            playSpellVisualKit.Duration = duration;
            SendMessageToSet(playSpellVisualKit, true);
        }

        public void SendCancelSpellVisualKit(int id)
        {
            CancelSpellVisualKit cancelSpellVisualKit = new();
            cancelSpellVisualKit.Source = GetGUID();
            cancelSpellVisualKit.SpellVisualKitID = id;
            SendMessageToSet(cancelSpellVisualKit, true);
        }

        public void CancelSpellMissiles(int spellId, bool reverseMissile = false, bool abortSpell = false)
        {
            bool hasMissile = false;
            if (abortSpell)
            {
                foreach (var pair in m_Events.GetEvents())
                {
                    Spell spell = Spell.ExtractSpellFromEvent(pair.Value);
                    if (spell != null)
                    {
                        if (spell.GetSpellInfo().Id == spellId)
                        {
                            pair.Value.ScheduleAbort();
                            hasMissile = true;
                        }
                    }
                }
            }
            else
                hasMissile = true;

            if (hasMissile)
            {
                MissileCancel packet = new();
                packet.OwnerGUID = GetGUID();
                packet.SpellID = spellId;
                packet.Reverse = reverseMissile;
                SendMessageToSet(packet, false);
            }
        }

        public void UnsummonAllTotems()
        {
            for (byte i = 0; i < SharedConst.MaxSummonSlot; ++i)
            {
                if (m_SummonSlot[i].IsEmpty())
                    continue;

                Creature OldTotem = GetMap().GetCreature(m_SummonSlot[i]);
                if (OldTotem != null)
                    if (OldTotem.IsSummon())
                        OldTotem.ToTempSummon().UnSummon();
            }
        }

        public bool IsOnVehicle(Unit vehicle)
        {
            return m_vehicle != null && m_vehicle == vehicle.GetVehicleKit();
        }

        public bool IsAIEnabled() { return i_AI != null; }

        public virtual UnitAI GetAI() { return i_AI; }

        public virtual T GetAI<T>() where T : UnitAI { return (T)i_AI; }

        public UnitAI GetTopAI() { return i_AIs.Count == 0 ? null : i_AIs.Peek(); }

        public void AIUpdateTick(TimeSpan diff)
        {
            UnitAI ai = GetAI();
            if (ai != null)
            {
                m_aiLocked = true;
                ai.UpdateAI(diff);
                m_aiLocked = false;
            }
        }

        public void PushAI(UnitAI newAI)
        {
            i_AIs.Push(newAI);
        }

        public void SetAI(UnitAI newAI)
        {
            PushAI(newAI);
            RefreshAI();
        }

        public bool PopAI()
        {
            if (i_AIs.Count != 0)
            {
                i_AIs.Pop();
                return true;
            }
            else
                return false;
        }

        public void RefreshAI()
        {
            Cypher.Assert(!m_aiLocked, "Tried to change current AI during UpdateAI()");
            if (i_AIs.Count == 0)
                i_AI = null;
            else
                i_AI = i_AIs.Peek();
        }

        public void ScheduleAIChange()
        {
            bool charmed = IsCharmed();

            if (charmed)
                PushAI(GetScheduledChangeAI());
            else
            {
                RestoreDisabledAI();
                PushAI(GetScheduledChangeAI()); //This could actually be PopAI() to get the previous AI but it's required atm to trigger UpdateCharmAI()
            }
        }

        void RestoreDisabledAI()
        {
            // Keep popping the stack until we either reach the bottom or find a valid AI
            while (PopAI())
            {
                if (GetTopAI() != null && GetTopAI() is not ScheduledChangeAI)
                    return;
            }
        }

        UnitAI GetScheduledChangeAI()
        {
            Creature creature = ToCreature();
            if (creature != null)
                return new ScheduledChangeAI(creature);
            else
                return null;
        }

        bool HasScheduledAIChange()
        {
            UnitAI ai = GetAI();
            if (ai != null)
                return ai is ScheduledChangeAI;
            else
                return true;
        }

        public bool IsPossessedByPlayer()
        {
            return HasUnitState(UnitState.Possessed) && GetCharmerGUID().IsPlayer();
        }

        public bool IsPossessing()
        {
            Unit u = GetCharmed();
            if (u != null)
                return u.IsPossessed();
            else
                return false;
        }

        public bool IsCharmed() { return !GetCharmerGUID().IsEmpty(); }

        public bool IsPossessed() { return HasUnitState(UnitState.Possessed); }

        public virtual void OnPhaseChange() { }

        public int GetModelForForm(ShapeShiftForm form, int spellId)
        {
            // Hardcoded cases
            switch (spellId)
            {
                case 7090: // Bear Form
                    return 29414;
                case 35200: // Roc Form
                    return 4877;
                default:
                    break;
            }

            if (this is Player player)
            {
                ShapeshiftFormModelData formModelData = 
                    Global.DB2Mgr.GetShapeshiftFormModelData(GetRace(), player.GetNativeGender(), form);

                if (formModelData != null)
                {
                    bool useRandom = false;
                    switch (form)
                    {
                        case ShapeShiftForm.CatForm:
                            useRandom = HasAura(210333);
                            break; // Glyph of the Feral Chameleon
                        case ShapeShiftForm.TravelForm:
                            useRandom = HasAura(344336);
                            break; // Glyph of the Swift Chameleon
                        case ShapeShiftForm.AquaticForm:
                            useRandom = HasAura(344338);
                            break; // Glyph of the Aquatic Chameleon
                        case ShapeShiftForm.BearForm:
                            useRandom = HasAura(107059);
                            break; // Glyph of the Ursol Chameleon
                        case ShapeShiftForm.FlightEpicForm:
                        case ShapeShiftForm.FlightForm:
                            useRandom = HasAura(344342);
                            break; // Glyph of the Aerial Chameleon
                        default:
                            break;
                    }

                    if (useRandom)
                    {
                        List<int> displayIds = new();
                        for (var i = 0; i < formModelData.Choices.Count; ++i)
                        {
                            ChrCustomizationDisplayInfoRecord displayInfo = formModelData.Displays[i];
                            if (displayInfo != null)
                            {
                                ChrCustomizationReqRecord choiceReq = 
                                    CliDB.ChrCustomizationReqStorage.LookupByKey(formModelData.Choices[i].ChrCustomizationReqID);

                                if (choiceReq == null || player.GetSession().MeetsChrCustomizationReq(choiceReq, GetRace(), GetClass(), false, player.m_playerData.Customizations))
                                    displayIds.Add(displayInfo.DisplayID);
                            }
                        }

                        if (!displayIds.Empty())
                            return displayIds.SelectRandom();
                    }
                    else
                    {
                        var formChoice = player.GetCustomizationChoice(formModelData.OptionID);
                        if (formChoice != 0)
                        {
                            var choiceIndex = formModelData.Choices.TryFind(out var _, out var index, choice =>
                            {
                                return choice.Id == formChoice;
                            });

                            if (choiceIndex)
                            {
                                ChrCustomizationDisplayInfoRecord displayInfo = formModelData.Displays[index];
                                if (displayInfo != null)
                                    return displayInfo.DisplayID;
                            }
                        }
                    }
                }
                switch (form)
                {
                    case ShapeShiftForm.GhostWolf:
                        if (HasAura(58135)) // Glyph of Spectral Wolf
                            return 60247;
                        break;
                    default:
                        break;
                }
            }

            int modelid = 0;
            SpellShapeshiftFormRecord formEntry = CliDB.SpellShapeshiftFormStorage.LookupByKey((int)form);
            if (formEntry != null && formEntry.CreatureDisplayID[0] != 0)
            {
                // Take the alliance modelid as default
                if (GetTypeId() != TypeId.Player)
                    return formEntry.CreatureDisplayID[0];
                else
                {
                    if (Player.TeamForRace(GetRace()) == Team.Alliance)
                        modelid = formEntry.CreatureDisplayID[0];
                    else
                        modelid = formEntry.CreatureDisplayID[1];

                    // If the player is horde but there are no values for the horde modelid - take the alliance modelid
                    if (modelid == 0 && Player.TeamForRace(GetRace()) == Team.Horde)
                        modelid = formEntry.CreatureDisplayID[0];
                }
            }

            return 0;
        }

        public Totem ToTotem() { return IsTotem() ? (this as Totem) : null; }
        public TempSummon ToTempSummon() { return IsSummon() ? (this as TempSummon) : null; }

        void RemoveAllFollowers()
        {
            while (!m_followingMe.Empty())
                m_followingMe[0].SetTarget(null);
        }

        public virtual void SetDeathState(DeathState s)
        {
            // Death state needs to be updated before RemoveAllAurasOnDeath() is called, to prevent entering combat
            m_deathState = s;

            bool isOnVehicle = GetVehicle() != null;

            if (s != DeathState.Alive && s != DeathState.JustRespawned)
            {
                CombatStop();

                if (IsNonMeleeSpellCast(false))
                    InterruptNonMeleeSpells(false);

                ExitVehicle();                                      // Exit vehicle before calling RemoveAllControlled
                // vehicles use special Type of charm that is not removed by the next function
                // triggering an assert
                UnsummonAllTotems();
                RemoveAllControlled();
                RemoveAllAurasOnDeath();
            }

            if (s == DeathState.JustDied)
            {
                // remove aurastates allowing special moves
                ClearAllReactives();
                m_Diminishing.Clear();

                // Don't clear the movement if the Unit was on a vehicle as we are exiting now
                if (!isOnVehicle)
                {
                    if (GetMotionMaster().StopOnDeath())
                        DisableSpline();
                }

                // without this when removing IncreaseMaxHealth aura player may stuck with 1 hp
                // do not why since in IncreaseMaxHealth currenthealth is checked
                SetHealth(0);
                SetPower(GetPowerType(), 0);
                SetEmoteState(Emote.OneshotNone);
                SetStandState(UnitStandStateType.Stand);

                // players in instance don't have ZoneScript, but they have InstanceScript
                ZoneScript zoneScript = GetZoneScript() != null ? GetZoneScript() : GetInstanceScript();
                if (zoneScript != null)
                    zoneScript.OnUnitDeath(this);
            }
            else if (s == DeathState.JustRespawned)
                RemoveUnitFlag(UnitFlags.Skinnable); // clear skinnable for creature and player (at Battleground)
        }

        public bool IsVisible()
        {
            return m_serverSideVisibility.GetValue(ServerSideVisibilityType.GM) <= (uint)AccountTypes.Player;
        }

        public void SetVisible(bool val)
        {
            if (!val)
                m_serverSideVisibility.SetValue(ServerSideVisibilityType.GM, AccountTypes.GameMaster);
            else
                m_serverSideVisibility.SetValue(ServerSideVisibilityType.GM, AccountTypes.Player);

            UpdateObjectVisibility();
        }

        public bool IsMagnet()
        {
            // Grounding Totem
            if (m_unitData.CreatedBySpell == 8177) /// @todo: find a more generic solution
                return true;

            return false;
        }

        public void SetShapeshiftForm(ShapeShiftForm form)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ShapeshiftForm), (byte)form);
        }

        // creates aura application instance and registers it in lists
        // aura application effects are handled separately to prevent aura list corruption
        public AuraApplication _CreateAuraApplication(Aura aura, uint effMask)
        {
            // can't apply aura on unit which is going to be deleted - to not create a memory leak
            Cypher.Assert(!m_cleanupDone);
            // just return if the aura has been already removed
            // this can happen if OnEffectHitTarget() script hook killed the unit or the aura owner (which can be different)
            if (aura.IsRemoved())
            {
                Log.outError(LogFilter.Spells, 
                    $"Unit::_CreateAuraApplication() called with a removed aura. Check if OnEffectHitTarget() " +
                    $"is triggering any spell with apply aura effect " +
                    $"(that's not allowed!)\nUnit: {GetDebugInfo()}\nAura: {aura.GetDebugInfo()}");
                return null;
            }

            // aura mustn't be already applied on target
            Cypher.Assert(!aura.IsAppliedOnTarget(GetGUID()), "Unit._CreateAuraApplication: aura musn't be applied on target");

            SpellInfo aurSpellInfo = aura.GetSpellInfo();
            var aurId = aurSpellInfo.Id;

            // ghost spell check, allow apply any auras at player loading in ghost mode (will be cleanup after load)
            if (!IsAlive() && !aurSpellInfo.IsDeathPersistent() &&
                (!IsTypeId(TypeId.Player) || !ToPlayer().GetSession().PlayerLoading()))
                return null;

            Unit caster = aura.GetCaster();

            AuraApplication aurApp = new(this, caster, aura, effMask);
            m_appliedAuras.Add(aurId, aurApp);

            if (aurSpellInfo.HasAnyAuraInterruptFlag())
            {
                m_interruptableAuras.Add(aurApp);
                AddInterruptMask(aurSpellInfo.AuraInterruptFlags, aurSpellInfo.AuraInterruptFlags2);
            }

            AuraStateType aState = aura.GetSpellInfo().GetAuraState();
            if (aState != 0)
                m_auraStateAuras.Add(aState, aurApp);

            aura._ApplyForTarget(this, caster, aurApp);
            return aurApp;
        }

        bool HasInterruptFlag(SpellAuraInterruptFlags flags) { return m_interruptMask.HasAnyFlag(flags); }
        bool HasInterruptFlag(SpellAuraInterruptFlags2 flags) { return m_interruptMask2.HasAnyFlag(flags); }

        public void AddInterruptMask(SpellAuraInterruptFlags flags, SpellAuraInterruptFlags2 flags2)
        {
            m_interruptMask |= flags;
            m_interruptMask2 |= flags2;
        }

        void _UpdateAutoRepeatSpell()
        {
            SpellInfo autoRepeatSpellInfo = m_currentSpells[CurrentSpellTypes.AutoRepeat].m_spellInfo;

            // check "realtime" interrupts
            // don't cancel spells which are affected by a SPELL_AURA_CAST_WHILE_WALKING effect
            if ((IsMoving() && GetCurrentSpell(CurrentSpellTypes.AutoRepeat).CheckMovement() != SpellCastResult.SpellCastOk) 
                || IsNonMeleeSpellCast(false, false, true, autoRepeatSpellInfo.Id == 75))
            {
                // cancel wand shoot
                if (autoRepeatSpellInfo.Id != 75)
                    InterruptSpell(CurrentSpellTypes.AutoRepeat);
                return;
            }

            //// apply delay (Auto Shot (spellID 75) not affected)
            //if (m_AutoRepeatFirstCast && GetAttackTimer(WeaponAttackType.RangedAttack) < (Milliseconds)500 && autoRepeatSpellInfo.Id != 75)
            //    SetAttackTimer(WeaponAttackType.RangedAttack, (Milliseconds)500);
            //m_AutoRepeatFirstCast = false;

            // castroutine
            if (IsAttackReady(WeaponAttackType.RangedAttack) 
                && GetCurrentSpell(CurrentSpellTypes.AutoRepeat).GetState() != SpellState.Preparing)
            {
                // Check if able to cast
                SpellCastResult result = m_currentSpells[CurrentSpellTypes.AutoRepeat].CheckCast(true);
                if (result != SpellCastResult.SpellCastOk)
                {
                    if (autoRepeatSpellInfo.Id != 75)
                        InterruptSpell(CurrentSpellTypes.AutoRepeat);
                    else if (GetTypeId() == TypeId.Player)
                    {
                        Spell.SendCastResult(
                            ToPlayer(), autoRepeatSpellInfo, 
                            m_currentSpells[CurrentSpellTypes.AutoRepeat].m_SpellVisual, 
                            m_currentSpells[CurrentSpellTypes.AutoRepeat].m_castId, 
                            result);
                    }

                    return;
                }

                // we want to shoot
                Spell spell = new(this, autoRepeatSpellInfo, TriggerCastFlags.IgnoreGCD);
                spell.Prepare(m_currentSpells[CurrentSpellTypes.AutoRepeat].m_targets);
            }

            // all went good, reset attack
            ResetAttackTimer(WeaponAttackType.RangedAttack);
        }

        public PowerType CalculateDisplayPowerType()
        {
            PowerType displayPower = PowerType.Mana;
            switch (GetShapeshiftForm())
            {
                case ShapeShiftForm.Ghoul:
                case ShapeShiftForm.CatForm:
                    displayPower = PowerType.Energy;
                    break;
                case ShapeShiftForm.BearForm:
                case ShapeShiftForm.DireBearForm:
                    displayPower = PowerType.Rage;
                    break;
                case ShapeShiftForm.TravelForm:
                case ShapeShiftForm.GhostWolf:
                    displayPower = PowerType.Mana;
                    break;
                default:
                {
                    var powerTypeAuras = GetAuraEffectsByType(AuraType.ModPowerDisplay);
                    if (!powerTypeAuras.Empty())
                    {
                        AuraEffect powerTypeAura = powerTypeAuras.First();
                        displayPower = (PowerType)powerTypeAura.GetMiscValue();
                    }
                    else
                    {
                        ChrClassesRecord cEntry = CliDB.ChrClassesStorage.LookupByKey((int)GetClass());
                        if (cEntry != null && cEntry.DisplayPower < PowerType.Max)
                            displayPower = cEntry.DisplayPower;

                        Vehicle vehicle = GetVehicleKit();
                        if (vehicle != null)
                        {
                            PowerDisplayRecord powerDisplay = 
                                CliDB.PowerDisplayStorage.LookupByKey(vehicle.GetVehicleInfo().PowerDisplayID[0]);

                            if (powerDisplay != null)
                                displayPower = (PowerType)powerDisplay.ActualType;
                            else if (GetClass() == Class.Rogue)
                                displayPower = PowerType.Energy;
                        }
                        else
                        {
                            if (this is Pet pet)
                            {
                                if (pet.GetPetType() == PetType.Hunter) // Hunter pets have focus
                                    displayPower = PowerType.Focus;
                                else if (pet.IsPetGhoul() || pet.IsRisenAlly()) // DK pets have energy
                                    displayPower = PowerType.Energy;
                            }
                        }
                    }
                    break;
                }
            }

            return displayPower;
        }

        public void UpdateDisplayPower()
        {
            SetPowerType(CalculateDisplayPowerType());
        }

        public void SetSheath(SheathState sheathed)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.SheatheState), (byte)sheathed);
            if (sheathed == SheathState.Unarmed)
                RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Sheathing);
        }

        public bool IsInFeralForm()
        {
            ShapeShiftForm form = GetShapeshiftForm();

            return IsFeralForm(form);
        }

        public bool IsFeralForm(ShapeShiftForm form)
        {
            return form == ShapeShiftForm.CatForm
                || form == ShapeShiftForm.BearForm
                || form == ShapeShiftForm.DireBearForm;
        }

        public bool IsControlledByPlayer() { return m_ControlledByPlayer; }

        public bool IsCharmedOwnedByPlayerOrPlayer() { return GetCharmerOrOwnerOrOwnGUID().IsPlayer(); }

        public void FollowerAdded(AbstractFollower f) { m_followingMe.Add(f); }
        public void FollowerRemoved(AbstractFollower f) { m_followingMe.Remove(f); }

        public uint GetCreatureTypeMask()
        {
            uint creatureType = (uint)GetCreatureType();
            return (uint)(creatureType >= 1 ? (1 << (int)(creatureType - 1)) : 0);
        }

        public Pet ToPet()
        {
            return IsPet() ? (this as Pet) : null;
        }
        public MotionMaster GetMotionMaster() { return i_motionMaster; }

        public void PlayOneShotAnimKitId(int animKitId)
        {
            if (!CliDB.AnimKitStorage.ContainsKey(animKitId))
            {
                Log.outError(LogFilter.Unit, 
                    $"Unit.PlayOneShotAnimKitId using invalid AnimKit ID: {animKitId}");

                return;
            }

            PlayOneShotAnimKit packet = new();
            packet.Unit = GetGUID();
            packet.AnimKitID = animKitId;
            SendMessageToSet(packet, true);
        }

        public void SetAIAnimKitId(int animKitId)
        {
            if (_aiAnimKitId == animKitId)
                return;

            if (animKitId != 0 && !CliDB.AnimKitStorage.ContainsKey(animKitId))
                return;

            _aiAnimKitId = (ushort)animKitId;

            SetAIAnimKit data = new();
            data.Unit = GetGUID();
            data.AnimKitID = animKitId;
            SendMessageToSet(data, true);
        }

        public override ushort GetAIAnimKitId() { return _aiAnimKitId; }

        public void SetMovementAnimKitId(ushort animKitId)
        {
            if (_movementAnimKitId == animKitId)
                return;

            if (animKitId != 0 && !CliDB.AnimKitStorage.ContainsKey(animKitId))
                return;

            _movementAnimKitId = animKitId;

            SetMovementAnimKit data = new();
            data.Unit = GetGUID();
            data.AnimKitID = animKitId;
            SendMessageToSet(data, true);
        }

        public override ushort GetMovementAnimKitId() { return _movementAnimKitId; }

        public void SetMeleeAnimKitId(ushort animKitId)
        {
            if (_meleeAnimKitId == animKitId)
                return;

            if (animKitId != 0 && !CliDB.AnimKitStorage.ContainsKey(animKitId))
                return;

            _meleeAnimKitId = animKitId;

            SetMeleeAnimKit data = new();
            data.Unit = GetGUID();
            data.AnimKitID = animKitId;
            SendMessageToSet(data, true);
        }

        public override ushort GetMeleeAnimKitId() { return _meleeAnimKitId; }

        public int GetVirtualItemId(int slot)
        {
            if (slot >= SharedConst.MaxEquipmentItems)
                return 0;

            return m_unitData.VirtualItems[slot].ItemID;
        }

        public ushort GetVirtualItemAppearanceMod(int slot)
        {
            if (slot >= SharedConst.MaxEquipmentItems)
                return 0;

            return m_unitData.VirtualItems[slot].ItemAppearanceModID;
        }

        public void SetVirtualItem(int slot, int itemId, ushort appearanceModId = 0, ushort itemVisual = 0)
        {
            if (slot >= SharedConst.MaxEquipmentItems)
                return;

            var virtualItemField = m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.VirtualItems, slot);
            SetUpdateFieldValue(virtualItemField.ModifyValue(virtualItemField.ItemID), itemId);
            SetUpdateFieldValue(virtualItemField.ModifyValue(virtualItemField.ItemAppearanceModID), appearanceModId);
            SetUpdateFieldValue(virtualItemField.ModifyValue(virtualItemField.ItemVisual), itemVisual);
        }

        //Unit
        public void SetLevel(int lvl, bool sendUpdate = true)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Level), lvl);

            if (!sendUpdate)
                return;

            if (ToPlayer() is Player player)
            {
                if (player.GetGroup() != null)
                    player.SetGroupUpdateFlag(GroupUpdateFlags.Level);

                Global.CharacterCacheStorage.UpdateCharacterLevel(GetGUID(), (byte)lvl);
            }
        }

        public int GetLevel() { return m_unitData.Level.GetValue(); }
        public override int GetLevelForTarget(WorldObject target) { return GetLevel(); }

        public Race GetRace() 
        { 
            return (Race)(byte)m_unitData.Race; 
        }

        public void SetRace(Race race) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Race), (byte)race); 
        }

        public Class GetClass() 
        { 
            return (Class)(byte)m_unitData.ClassId; 
        }

        public void SetClass(Class classId) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ClassId), (byte)classId); 
        }        

        public Gender GetGender() 
        {
            return (Gender)(byte)m_unitData.Sex; 
        }

        public void SetGender(Gender sex) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Sex), (byte)sex); 
        }

        public virtual Gender GetNativeGender() { return GetGender(); }
        public virtual void SetNativeGender(Gender gender) { SetGender(gender); }

        public void RecalculateObjectScale()
        {
            int scaleAuras = GetTotalAuraModifier(AuraType.ModScale) + GetTotalAuraModifier(AuraType.ModScale2);
            float scale = GetNativeObjectScale() + MathFunctions.CalculatePct(1.0f, scaleAuras);
            float scaleMin = IsPlayer() ? 0.1f : 0.01f;
            SetObjectScale(Math.Max(scale, scaleMin));
        }

        public virtual float GetNativeObjectScale() { return 1.0f; }

        public float GetDisplayScale() { return m_unitData.DisplayScale; }
        
        public int GetDisplayId() { return m_unitData.DisplayID; }

        public virtual void SetDisplayId(int displayId, bool setNative = false)
        {
            float displayScale = SharedConst.DefaultPlayerDisplayScale;

            if (IsCreature() && !IsPet())
            {
                CreatureModel model = ToCreature().GetCreatureTemplate().GetModelWithDisplayId(displayId);
                if (model != null)
                    displayScale = model.DisplayScale;
            }

            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.DisplayID), displayId);
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.DisplayScale), displayScale);

            if (setNative)
            {
                SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.NativeDisplayID), displayId);
                SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.NativeXDisplayScale), displayScale);
            }

            // Set Gender by ModelInfo
            CreatureModelInfo modelInfo = Global.ObjectMgr.GetCreatureModelInfo(displayId);
            if (modelInfo != null)
                SetGender(modelInfo.gender);

            CalculateHoverHeight();
        }

        public void RestoreDisplayId(bool ignorePositiveAurasPreventingMounting = false)
        {
            AuraEffect handledAura = null;
            // try to receive model from transform auras
            var transforms = GetAuraEffectsByType(AuraType.Transform);
            if (!transforms.Empty())
            {
                // iterate over already applied transform auras - from newest to oldest
                foreach (var eff in transforms)
                {
                    AuraApplication aurApp = eff.GetBase().GetApplicationOfTarget(GetGUID());
                    if (aurApp != null)
                    {
                        if (handledAura == null)
                        {
                            if (!ignorePositiveAurasPreventingMounting)
                                handledAura = eff;
                            else
                            {
                                CreatureTemplate ci = Global.ObjectMgr.GetCreatureTemplate(eff.GetMiscValue());
                                if (ci != null)
                                {
                                    if (!IsDisallowedMountForm(eff.GetId(), ShapeShiftForm.None,
                                        ObjectManager.ChooseDisplayId(ci).CreatureDisplayID))
                                    {
                                        handledAura = eff;
                                    }
                                }
                            }
                        }

                        // prefer negative auras
                        if (!aurApp.IsPositive())
                        {
                            handledAura = eff;
                            break;
                        }
                    }
                }
            }

            var shapeshiftAura = GetAuraEffectsByType(AuraType.ModShapeshift);

            // transform aura was found
            if (handledAura != null)
            {
                handledAura.HandleEffect(this, AuraEffectHandleModes.SendForClient, true);
                return;
            }
            // we've found shapeshift
            else if (!shapeshiftAura.Empty()) // we've found shapeshift
            {
                // only one such aura possible at a time
                int modelId = GetModelForForm(GetShapeshiftForm(), shapeshiftAura[0].GetId());
                if (modelId != 0)
                {
                    if (!ignorePositiveAurasPreventingMounting || !IsDisallowedMountForm(0, GetShapeshiftForm(), modelId))
                        SetDisplayId(modelId);
                    else
                        SetDisplayId(GetNativeDisplayId());
                    return;
                }
            }
            // no auras found - set modelid to default
            SetDisplayId(GetNativeDisplayId());
        }

        public int GetNativeDisplayId() { return m_unitData.NativeDisplayID; }
        public float GetNativeDisplayScale() { return m_unitData.NativeXDisplayScale; }

        public bool IsMounted()
        {
            return HasUnitFlag(UnitFlags.Mount);
        }

        public int GetMountDisplayId() 
        { 
            return m_unitData.MountDisplayID; 
        }

        public void SetMountDisplayId(int mountDisplayId) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MountDisplayID), mountDisplayId); 
        }

        void CalculateHoverHeight()
        {
            float hoverHeight = SharedConst.DefaultPlayerHoverHeight;
            float displayScale = SharedConst.DefaultPlayerDisplayScale;

            int displayId = IsMounted() ? GetMountDisplayId() : GetDisplayId();

            // Get DisplayScale for creatures
            if (IsCreature())
            {
                CreatureModel model = ToCreature().GetCreatureTemplate().GetModelWithDisplayId(displayId);
                if (model != null)
                    displayScale = model.DisplayScale;
            }

            var displayInfo = CliDB.CreatureDisplayInfoStorage.LookupByKey(displayId);
            if (displayInfo != null)
            {
                var modelData = CliDB.CreatureModelDataStorage.LookupByKey(displayInfo.ModelID);
                if (modelData != null)
                    hoverHeight = modelData.HoverHeight * modelData.ModelScale * displayInfo.CreatureModelScale * displayScale;
            }

            SetHoverHeight(hoverHeight != 0 ? hoverHeight : SharedConst.DefaultPlayerHoverHeight);
        }

        public virtual float GetFollowAngle() { return MathFunctions.PiOver2; }

        public override ObjectGuid GetOwnerGUID() { return m_unitData.SummonedBy; }
        
        public void SetOwnerGUID(ObjectGuid owner)
        {
            if (GetOwnerGUID() == owner)
                return;

            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.SummonedBy), owner);
            if (owner.IsEmpty())
                return;

            // Update owner dependent fields
            Player player = Global.ObjAccessor.GetPlayer(this, owner);

            // if player cannot see this unit yet, he will receive needed data with create object
            if (player == null || !player.HaveAtClient(this)) 
                return;

            UpdateData udata = new(GetMapId());
            UpdateObject packet;
            BuildValuesUpdateBlockForPlayerWithFlag(udata, UpdateFieldFlag.Owner, player);
            udata.BuildPacket(out packet);
            player.SendPacket(packet);
        }

        public override ObjectGuid GetCreatorGUID() { return m_unitData.CreatedBy; }
        public void SetCreatorGUID(ObjectGuid creator) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.CreatedBy), creator); }
        public ObjectGuid GetMinionGUID() { return m_unitData.Summon; }
        public void SetMinionGUID(ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Summon), guid); }
        public ObjectGuid GetPetGUID() { return m_SummonSlot[0]; }
        public void SetPetGUID(ObjectGuid guid) { m_SummonSlot[0] = guid; }
        public ObjectGuid GetCritterGUID() { return m_unitData.Critter; }
        public void SetCritterGUID(ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Critter), guid); }
        public ObjectGuid GetBattlePetCompanionGUID() { return m_unitData.BattlePetCompanionGUID; }
        public void SetBattlePetCompanionGUID(ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.BattlePetCompanionGUID), guid); }
        public ObjectGuid GetDemonCreatorGUID() { return m_unitData.DemonCreator; }
        public void SetDemonCreatorGUID(ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.DemonCreator), guid); }
        public Unit GetDemonCreator() => Global.ObjAccessor.GetUnit(this, GetDemonCreatorGUID());
        public Player GetDemonCreatorPlayer() => Global.ObjAccessor.GetPlayer(this, GetDemonCreatorGUID());

        public ObjectGuid GetCharmerGUID() { return m_unitData.CharmedBy; }

        public Unit GetCharmer() { return m_charmer; }

        public ObjectGuid GetCharmedGUID() { return m_unitData.Charm; }

        public Unit GetCharmed() { return m_charmed; }

        public override ObjectGuid GetCharmerOrOwnerGUID()
        {
            return IsCharmed() ? GetCharmerGUID() : GetOwnerGUID();
        }

        Player GetControllingPlayer()
        {
            ObjectGuid guid = GetCharmerOrOwnerGUID();
            if (!guid.IsEmpty())
            {
                Unit master = Global.ObjAccessor.GetUnit(this, guid);
                if (master != null)
                    return master.GetControllingPlayer();

                return null;
            }
            else
                return ToPlayer();
        }

        public override Unit GetCharmerOrOwner()
        {
            return IsCharmed() ? GetCharmer() : GetOwner();
        }

        public ServerTime GetBattlePetCompanionNameTimestamp() { return (ServerTime)m_unitData.BattlePetCompanionNameTimestamp.GetValue(); }
        public void SetBattlePetCompanionNameTimestamp(ServerTime timestamp) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.BattlePetCompanionNameTimestamp), (UnixTime)timestamp); }
        public uint GetWildBattlePetLevel() { return (uint)m_unitData.WildBattlePetLevel.GetValue(); }
        public void SetWildBattlePetLevel(int wildBattlePetLevel) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.WildBattlePetLevel), wildBattlePetLevel); }

        public bool HasUnitFlag(UnitFlags flags) { return (m_unitData.Flags & (uint)flags) != 0; }
        public void SetUnitFlag(UnitFlags flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags), (uint)flags); }
        public void RemoveUnitFlag(UnitFlags flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags), (uint)flags); }
        public void ReplaceAllUnitFlags(UnitFlags flags) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags), (uint)flags); }
        public bool HasUnitFlag2(UnitFlags2 flags) { return (m_unitData.Flags2 & (uint)flags) != 0; }
        public void SetUnitFlag2(UnitFlags2 flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags2), (uint)flags); }
        public void RemoveUnitFlag2(UnitFlags2 flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags2), (uint)flags); }
        public void ReplaceAllUnitFlags2(UnitFlags2 flags) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags2), (uint)flags); }
        public bool HasUnitFlag3(UnitFlags3 flags) { return (m_unitData.Flags3 & (uint)flags) != 0; }
        public void SetUnitFlag3(UnitFlags3 flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags3), (uint)flags); }
        public void RemoveUnitFlag3(UnitFlags3 flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags3), (uint)flags); }
        public void ReplaceAllUnitFlags3(UnitFlags3 flags) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags3), (uint)flags); }

        public UnitDynFlags GetDynamicFlags() { return (UnitDynFlags)(uint)m_objectData.DynamicFlags; }
        public bool HasDynamicFlag(UnitDynFlags flag) { return (m_objectData.DynamicFlags & (uint)flag) != 0; }
        public void SetDynamicFlag(UnitDynFlags flag) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_objectData).ModifyValue(m_objectData.DynamicFlags), (uint)flag); }
        public void RemoveDynamicFlag(UnitDynFlags flag) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_objectData).ModifyValue(m_objectData.DynamicFlags), (uint)flag); }
        public void ReplaceAllDynamicFlags(UnitDynFlags flag) { SetUpdateFieldValue(m_values.ModifyValue(m_objectData).ModifyValue(m_objectData.DynamicFlags), (uint)flag); }

        public void SetCreatedBySpell(int spellId) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.CreatedBySpell), spellId); }

        public Emote GetEmoteState() { return (Emote)(int)m_unitData.EmoteState; }
        public void SetEmoteState(Emote emote) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.EmoteState), (int)emote); }

        public SheathState GetSheath() { return (SheathState)(byte)m_unitData.SheatheState; }

        public UnitPVPStateFlags GetPvpFlags() { return (UnitPVPStateFlags)(byte)m_unitData.PvpFlags; }
        public bool HasPvpFlag(UnitPVPStateFlags flags) { return (m_unitData.PvpFlags & (uint)flags) != 0; }
        public void SetPvpFlag(UnitPVPStateFlags flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PvpFlags), (byte)flags); }
        public void RemovePvpFlag(UnitPVPStateFlags flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PvpFlags), (byte)flags); }
        public void ReplaceAllPvpFlags(UnitPVPStateFlags flags) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PvpFlags), (byte)flags); }
        public bool IsInSanctuary() { return HasPvpFlag(UnitPVPStateFlags.Sanctuary); }
        public bool IsPvP() { return HasPvpFlag(UnitPVPStateFlags.PvP); }
        public bool IsFFAPvP() { return HasPvpFlag(UnitPVPStateFlags.FFAPvp); }

        public UnitPetFlags GetPetFlags()
        {
            return (UnitPetFlags)(byte)m_unitData.PetFlags;
        }
        public bool HasPetFlag(UnitPetFlags flags) { return (m_unitData.PetFlags & (byte)flags) != 0; }
        public void SetPetFlag(UnitPetFlags flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PetFlags), (byte)flags); }
        public void RemovePetFlag(UnitPetFlags flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PetFlags), (byte)flags); }
        public void ReplaceAllPetFlags(UnitPetFlags flags) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PetFlags), (byte)flags); }

        public void SetPetNumberForClient(int petNumber) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PetNumber), petNumber); }
        public void SetPetNameTimestamp(ServerTime timestamp) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.PetNameTimestamp), (UnixTime)timestamp); }

        public ShapeShiftForm GetShapeshiftForm() { return (ShapeShiftForm)(byte)m_unitData.ShapeshiftForm; }
        public CreatureType GetCreatureType()
        {
            if (IsTypeId(TypeId.Player))
            {
                ShapeShiftForm form = GetShapeshiftForm();
                var ssEntry = CliDB.SpellShapeshiftFormStorage.LookupByKey((int)form);
                if (ssEntry != null && ssEntry.CreatureType > 0)
                    return (CreatureType)ssEntry.CreatureType;
                else
                {
                    var raceEntry = CliDB.ChrRacesStorage.LookupByKey((int)GetRace());
                    return (CreatureType)raceEntry.CreatureType;
                }
            }
            else
                return ToCreature().GetCreatureTemplate().CreatureType;
        }

        public void DeMorph()
        {
            SetDisplayId(GetNativeDisplayId());
        }

        public bool HasUnitTypeMask(UnitTypeMask mask) { return Convert.ToBoolean(mask & UnitTypeMask); }
        public void AddUnitTypeMask(UnitTypeMask mask) { UnitTypeMask |= mask; }

        public bool IsAlive() { return m_deathState == DeathState.Alive; }
        public bool IsDying() { return m_deathState == DeathState.JustDied; }
        public bool IsDead() { return (m_deathState == DeathState.Dead || m_deathState == DeathState.Corpse); }
        public bool IsSummon() { return UnitTypeMask.HasAnyFlag(UnitTypeMask.Summon); }
        public bool IsGuardian() { return UnitTypeMask.HasAnyFlag(UnitTypeMask.Guardian); }
        public bool IsPet() { return UnitTypeMask.HasAnyFlag(UnitTypeMask.Pet); }
        public bool IsHunterPet() { return UnitTypeMask.HasAnyFlag(UnitTypeMask.HunterPet); }
        public bool IsTotem() { return UnitTypeMask.HasAnyFlag(UnitTypeMask.Totem); }
        public bool IsVehicle() { return UnitTypeMask.HasAnyFlag(UnitTypeMask.Vehicle); }

        public int GetContentTuning() { return m_unitData.ContentTuningID; }

        public void AddUnitState(UnitState f)
        {
            m_state |= f;
        }

        public bool HasUnitState(UnitState f)
        {
            return m_state.HasAnyFlag(f);
        }

        public void ClearUnitState(UnitState f)
        {
            m_state &= ~f;
        }

        public override bool IsAlwaysVisibleFor(WorldObject seer)
        {
            if (base.IsAlwaysVisibleFor(seer))
                return true;

            // Always seen by owner
            ObjectGuid guid = GetCharmerOrOwnerGUID();
            if (!guid.IsEmpty())
            {
                if (seer.GetGUID() == guid)
                    return true;
            }

            Player seerPlayer = seer.ToPlayer();
            if (seerPlayer != null)
            {
                Unit owner = GetOwner();
                if (owner != null)
                {
                    Player ownerPlayer = owner.ToPlayer();
                    if (ownerPlayer != null)
                    {
                        if (ownerPlayer.IsGroupVisibleFor(seerPlayer))
                            return true;
                    }
                }
            }

            return false;
        }

        public override int GetFaction() { return m_unitData.FactionTemplate; }
        public override void SetFaction(int faction) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.FactionTemplate), faction); }
        public override void SetFaction(FactionTemplates faction) { SetFaction((int)faction); }

        public void RestoreFaction()
        {
            if (HasAuraType(AuraType.ModFaction))
            {
                SetFaction(GetAuraEffectsByType(AuraType.ModFaction).LastOrDefault().GetMiscValue());
                return;
            }

            if (IsTypeId(TypeId.Player))
                ToPlayer().SetFactionForRace(GetRace());
            else
            {
                if (HasUnitTypeMask(UnitTypeMask.Minion))
                {
                    Unit owner = GetOwner();
                    if (owner != null)
                    {
                        SetFaction(owner.GetFaction());
                        return;
                    }
                }
                CreatureTemplate cinfo = ToCreature().GetCreatureTemplate();
                if (cinfo != null)  // normal creature
                    SetFaction(cinfo.Faction);
            }
        }

        public bool IsInPartyWith(Unit unit)
        {
            if (this == unit)
                return true;

            Unit u1 = GetCharmerOrOwnerOrSelf();
            Unit u2 = unit.GetCharmerOrOwnerOrSelf();
            if (u1 == u2)
                return true;

            if (u1.IsTypeId(TypeId.Player) && u2.IsTypeId(TypeId.Player))
                return u1.ToPlayer().IsInSameGroupWith(u2.ToPlayer());
            else if ((u2.IsTypeId(TypeId.Player) && u1.IsTypeId(TypeId.Unit) && u1.ToCreature().HasFlag(CreatureStaticFlags4.TreatAsRaidUnitForHelpfulSpells)) ||
                (u1.IsTypeId(TypeId.Player) && u2.IsTypeId(TypeId.Unit) && u2.ToCreature().HasFlag(CreatureStaticFlags4.TreatAsRaidUnitForHelpfulSpells)))
                return true;

            return u1.GetTypeId() == TypeId.Unit && u2.GetTypeId() == TypeId.Unit && u1.GetFaction() == u2.GetFaction();
        }

        public bool IsInRaidWith(Unit unit)
        {
            if (this == unit)
                return true;

            Unit u1 = GetCharmerOrOwnerOrSelf();
            Unit u2 = unit.GetCharmerOrOwnerOrSelf();
            if (u1 == u2)
                return true;

            if (u1.IsTypeId(TypeId.Player) && u2.IsTypeId(TypeId.Player))
                return u1.ToPlayer().IsInSameRaidWith(u2.ToPlayer());
            else if ((u2.IsTypeId(TypeId.Player) && u1.IsTypeId(TypeId.Unit) && u1.ToCreature().HasFlag(CreatureStaticFlags4.TreatAsRaidUnitForHelpfulSpells)) ||
                    (u1.IsTypeId(TypeId.Player) && u2.IsTypeId(TypeId.Unit) && u2.ToCreature().HasFlag(CreatureStaticFlags4.TreatAsRaidUnitForHelpfulSpells)))
                return true;

            // else u1.GetTypeId() == u2.GetTypeId() == TYPEID_UNIT
            return u1.GetFaction() == u2.GetFaction();
        }

        public UnitStandStateType GetStandState() { return (UnitStandStateType)(byte)m_unitData.StandState; }
        public void SetVisFlag(UnitVisFlags flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.VisFlags), (byte)flags); }
        public void RemoveVisFlag(UnitVisFlags flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.VisFlags), (byte)flags); }
        public void ReplaceAllVisFlags(UnitVisFlags flags) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.VisFlags), (byte)flags); }

        public bool IsSitState()
        {
            UnitStandStateType s = GetStandState();
            return
                s == UnitStandStateType.SitChair || s == UnitStandStateType.SitLowChair ||
                s == UnitStandStateType.SitMediumChair || s == UnitStandStateType.SitHighChair ||
                s == UnitStandStateType.Sit;
        }

        public bool IsStandState()
        {
            UnitStandStateType s = GetStandState();
            return !IsSitState() && s != UnitStandStateType.Sleep && s != UnitStandStateType.Kneel;
        }

        public bool IsPowerRegenInterruptedByMP5Rule()
        {
            return Time.Diff(m_lastManaUseTime, LoopTime.ServerTime) < (Seconds)5;
        }

        public void SetStandState(UnitStandStateType state, int animKitId = 0)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.StandState), (byte)state);

            if (IsStandState())
                RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Standing);

            if (IsTypeId(TypeId.Player))
            {
                StandStateUpdate packet = new(state, animKitId);
                ToPlayer().SendPacket(packet);
            }
        }

        public AnimTier GetAnimTier() { return (AnimTier)(byte)m_unitData.AnimTier; }

        public void SetAnimTier(AnimTier animTier, bool notifyClient = true)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AnimTier), (byte)animTier);

            if (notifyClient)
            {
                SetAnimTier setAnimTier = new();
                setAnimTier.Unit = GetGUID();
                setAnimTier.Tier = (int)animTier;
                SendMessageToSet(setAnimTier, true);
            }
        }

        public int GetChannelSpellId() { return m_unitData.ChannelData.GetValue().SpellID; }
        
        public void SetChannelSpellId(int channelSpellId)
        {
            SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ChannelData)._value.SpellID, channelSpellId);
        }

        public int GetChannelSpellXSpellVisualId() { return m_unitData.ChannelData.GetValue().SpellXSpellVisualID; }
        
        public void SetChannelSpellXSpellVisualId(int spellXSpellVisualId)
        {
            UnitChannel unitChannel = m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ChannelData);
            SetUpdateFieldValue(ref unitChannel.SpellXSpellVisualID, spellXSpellVisualId);
        }

        public void AddChannelObject(ObjectGuid guid) { AddDynamicUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ChannelObjects), guid); }
        public void SetChannelObject(int slot, ObjectGuid guid) { SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ChannelObjects, slot), guid); }
        public void ClearChannelObjects() { ClearDynamicUpdateFieldValues(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ChannelObjects)); }

        public void RemoveChannelObject(ObjectGuid guid)
        {
            int index = m_unitData.ChannelObjects.FindIndex(guid);
            if (index >= 0)
                RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.ChannelObjects), index);
        }

        public static bool IsDamageReducedByArmor(SpellSchoolMask schoolMask, SpellInfo spellInfo = null)
        {
            // only physical spells damage gets reduced by armor
            if ((schoolMask & SpellSchoolMask.Normal) == 0)
                return false;

            return spellInfo == null || !spellInfo.HasAttribute(SpellCustomAttributes.IgnoreArmor);
        }

        public override UpdateFieldFlag GetUpdateFieldFlagsFor(Player target)
        {
            UpdateFieldFlag flags = UpdateFieldFlag.None;
            if (target == this || GetOwnerGUID() == target.GetGUID())
                flags |= UpdateFieldFlag.Owner;

            if (HasDynamicFlag(UnitDynFlags.SpecialInfo))
                if (HasAuraTypeWithCaster(AuraType.Empathy, target.GetGUID()))
                    flags |= UpdateFieldFlag.Empath;

            return flags;
        }

        public override void BuildValuesCreate(WorldPacket data, Player target)
        {
            UpdateFieldFlag flags = GetUpdateFieldFlagsFor(target);
            WorldPacket buffer = new();

            buffer.WriteUInt8((byte)flags);
            m_objectData.WriteCreate(buffer, flags, this, target);
            m_unitData.WriteCreate(buffer, flags, this, target);

            data.WriteUInt32(buffer.GetSize());
            data.WriteBytes(buffer);
        }

        public override void BuildValuesUpdate(WorldPacket data, Player target)
        {
            UpdateFieldFlag flags = GetUpdateFieldFlagsFor(target);
            WorldPacket buffer = new();

            buffer.WriteUInt32(m_values.GetChangedObjectTypeMask());
            if (m_values.HasChanged(TypeId.Object))
                m_objectData.WriteUpdate(buffer, flags, this, target);

            if (m_values.HasChanged(TypeId.Unit))
                m_unitData.WriteUpdate(buffer, flags, this, target);

            data.WriteUInt32(buffer.GetSize());
            data.WriteBytes(buffer);
        }

        public override void BuildValuesUpdateWithFlag(WorldPacket data, UpdateFieldFlag flags, Player target)
        {
            UpdateMask valuesMask = new((int)TypeId.Max);
            valuesMask.Set((int)TypeId.Unit);

            WorldPacket buffer = new();

            UpdateMask mask = m_unitData.GetStaticUpdateMask();
            m_unitData.AppendAllowedFieldsMaskForFlag(mask, flags);
            m_unitData.WriteUpdate(buffer, mask, true, this, target);

            data.WriteUInt32(buffer.GetSize());
            data.WriteUInt32(valuesMask.GetBlock(0));
            data.WriteBytes(buffer);
        }

        public void BuildValuesUpdateForPlayerWithMask(UpdateData data, UpdateMask requestedObjectMask, UpdateMask requestedUnitMask, Player target)
        {
            UpdateFieldFlag flags = GetUpdateFieldFlagsFor(target);
            UpdateMask valuesMask = new((int)TypeId.Max);
            if (requestedObjectMask.IsAnySet())
                valuesMask.Set((int)TypeId.Object);

            m_unitData.FilterDisallowedFieldsMaskForFlag(requestedUnitMask, flags);
            if (requestedUnitMask.IsAnySet())
                valuesMask.Set((int)TypeId.Unit);

            WorldPacket buffer = new();
            buffer.WriteUInt32(valuesMask.GetBlock(0));

            if (valuesMask[(int)TypeId.Object])
                m_objectData.WriteUpdate(buffer, requestedObjectMask, true, this, target);

            if (valuesMask[(int)TypeId.Unit])
                m_unitData.WriteUpdate(buffer, requestedUnitMask, true, this, target);

            WorldPacket buffer1 = new();
            buffer1.WriteUInt8((byte)UpdateType.Values);
            buffer1.WritePackedGuid(GetGUID());
            buffer1.WriteUInt32(buffer.GetSize());
            buffer1.WriteBytes(buffer.GetData());

            data.AddUpdateBlock(buffer1);
        }

        public override void ClearUpdateMask(bool remove)
        {
            m_values.ClearChangesMask(m_unitData);
            base.ClearUpdateMask(remove);
        }

        public override void DestroyForPlayer(Player target)
        {
            Battleground bg = target.GetBattleground();
            if (bg != null)
            {
                if (bg.IsArena())
                {
                    DestroyArenaUnit destroyArenaUnit = new();
                    destroyArenaUnit.Guid = GetGUID();
                    target.SendPacket(destroyArenaUnit);
                }
            }

            base.DestroyForPlayer(target);
        }

        public bool CanDualWield() { return m_canDualWield; }

        public virtual void SetCanDualWield(bool value) { m_canDualWield = value; }

        public DeathState GetDeathState()
        {
            return m_deathState;
        }

        public bool HaveOffhandWeapon()
        {
            if (IsTypeId(TypeId.Player))
                return ToPlayer().GetWeaponForAttack(WeaponAttackType.OffAttack, true) != null;
            else
                return m_canDualWield;
        }

        void StartReactiveTimer(ReactiveType reactive) { m_reactiveTimer[reactive] = (Milliseconds)4000; }

        public static void DealDamageMods(Unit attacker, Unit victim, ref int damage)
        {
            if (victim == null || !victim.IsAlive() || victim.HasUnitState(UnitState.InFlight)
                || (victim.IsTypeId(TypeId.Unit) && victim.ToCreature().IsInEvadeMode()))
            {
                damage = 0;
            }
        }

        public static void DealDamageMods(Unit attacker, Unit victim, ref int damage, ref int absorb)
        {
            if (victim == null || !victim.IsAlive() || victim.HasUnitState(UnitState.InFlight)
                || (victim.IsTypeId(TypeId.Unit) && victim.ToCreature().IsEvadingAttacks()))
            {
                absorb += damage;
                damage = 0;
                return;
            }

            if (attacker != null)
                damage = (int)(damage * attacker.GetDamageMultiplierForTarget(victim));
        }

        public static int DealDamage(Unit attacker, Unit victim, int damage, CleanDamage cleanDamage = null, DamageEffectType damagetype = DamageEffectType.Direct, SpellSchoolMask damageSchoolMask = SpellSchoolMask.Normal, SpellInfo spellProto = null, bool durabilityLoss = true)
        {
            int rage_damage = damage + (cleanDamage != null ? cleanDamage.absorbed_damage : 0);

            var damageDone = damage;
            var damageTaken = damage;
            if (attacker != null)
                damageTaken = (int)(damage / victim.GetHealthMultiplierForTarget(attacker));

            // call script hooks
            {
                var tmpDamage = damageTaken;

                // sparring
                Creature victimCreature = victim.ToCreature();
                if (victimCreature != null)
                    tmpDamage = victimCreature.CalculateDamageForSparring(attacker, tmpDamage);

                victim.GetAI()?.DamageTaken(attacker, ref tmpDamage, damagetype, spellProto);

                attacker?.GetAI()?.DamageDealt(victim, ref tmpDamage, damagetype);

                // Hook for OnDamage Event
                Global.ScriptMgr.OnDamage(attacker, victim, ref tmpDamage);

                // if any script modified damage, we need to also apply the same modification to unscaled damage value
                if (tmpDamage != damageTaken)
                {
                    if (attacker != null)
                        damageDone = (int)(tmpDamage * victim.GetHealthMultiplierForTarget(attacker));
                    else
                        damageDone = tmpDamage;

                    damageTaken = tmpDamage;
                }
            }

            // Signal to pets that their owner was attacked - except when DOT.
            if (attacker != victim && damagetype != DamageEffectType.DOT)
            {
                foreach (Unit controlled in victim.m_Controlled)
                {
                    if (controlled is Creature cControlled)
                    {
                        if (cControlled.GetAI() is CreatureAI controlledAI)
                            controlledAI.OwnerAttackedBy(attacker);
                    }
                }
            }

            Player player = victim.ToPlayer();
            if (player != null && player.GetCommandStatus(PlayerCommandStates.God))
                return 0;

            if (damagetype != DamageEffectType.NoDamage)
            {
                // interrupting auras with SpellAuraInterruptFlags.Damage before checking !damage (absorbed damage breaks that Type of auras)
                if (spellProto != null)
                {
                    if (!spellProto.HasAttribute(SpellAttr4.ReactiveDamageProc))
                        victim.RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Damage, spellProto);
                }
                else
                {
                    victim.RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Damage);
                }

                // interrupt spells with SpellInterruptFlags.DamageAbsorb on absorbed damage (no dots)
                if (damageTaken == 0 && damagetype != DamageEffectType.DOT && cleanDamage != null && cleanDamage.absorbed_damage != 0)
                {
                    if (victim != attacker && victim.IsPlayer())
                    {
                        if (victim.GetCurrentSpell(CurrentSpellTypes.Generic) is Spell spell)
                        {
                            if (spell.GetState() == SpellState.Preparing
                                && spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamageAbsorb))
                            {
                                victim.InterruptNonMeleeSpells(false);
                            }
                        }
                    }
                }

                // We're going to call functions which can modify content of the list during iteration over it's elements
                // Let's copy the list so we can prevent iterator invalidation
                var vCopyDamageCopy = victim.GetAuraEffectsByType(AuraType.ShareDamagePct);
                // copy damage to casters of this aura
                foreach (var aura in vCopyDamageCopy)
                {
                    // Check if aura was removed during iteration - we don't need to work on such auras
                    if (!aura.GetBase().IsAppliedOnTarget(victim.GetGUID()))
                        continue;

                    // check damage school mask
                    if ((aura.GetMiscValue() & (int)damageSchoolMask) == 0)
                        continue;

                    Unit shareDamageTarget = aura.GetCaster();
                    if (shareDamageTarget == null)
                        continue;

                    SpellInfo spell = aura.GetSpellInfo();

                    var share = MathFunctions.CalculatePct(damageDone, aura.GetAmount());

                    // @todo check packets if damage is done by victim, or by attacker of victim
                    DealDamageMods(attacker, shareDamageTarget, ref share);
                    DealDamage(attacker, shareDamageTarget, share, null, DamageEffectType.NoDamage, spell.GetSchoolMask(), spell, false);
                }
            }

            // Rage from Damage made (only from direct weapon damage)
            if (attacker != null && cleanDamage != null && damagetype == DamageEffectType.Direct && attacker != victim && attacker.GetPowerType() == PowerType.Rage)
            {
                if (cleanDamage.attackType == WeaponAttackType.BaseAttack || cleanDamage.attackType == WeaponAttackType.OffAttack)
                {
                    float weaponAttackTypeFactor = 3.5f;

                if (cleanDamage.attackType == WeaponAttackType.OffAttack)
                        weaponAttackTypeFactor /= 2.0f;

                    int weaponSpeedHitFactor = (int)(attacker.GetBaseAttackTime(cleanDamage.attackType) / (float)Time.MillisecondsInSecond * weaponAttackTypeFactor);
                    
                    if (cleanDamage.hitOutCome == MeleeHitOutcome.Crit)
                        weaponSpeedHitFactor *= 2;

                    attacker.RewardRage(rage_damage, weaponSpeedHitFactor, true);
                }
            }

            if (damageDone == 0)
            {
                // Rage from absorbed damage
                if (cleanDamage != null && cleanDamage.absorbed_damage != 0 && victim.GetPowerType() == PowerType.Rage)
                    victim.RewardRage(cleanDamage.absorbed_damage, 0, false);

                return 0;
            }

            int health = (int)victim.GetHealth();

            // duel ends when player has 1 or less hp
            bool duel_hasEnded = false;
            bool duel_wasMounted = false;
            if (victim.IsPlayer() && victim.ToPlayer().duel != null && damageTaken >= (health - 1))
            {
                if (attacker == null)
                    return 0;

                // prevent kill only if killed in duel and killed by opponent or opponent controlled creature
                if (victim.ToPlayer().duel.Opponent == attacker.GetControllingPlayer())
                    damageTaken = health - 1;

                duel_hasEnded = true;
            }
            else if (victim.IsCreature() && victim != attacker 
                && damageTaken >= health && victim.ToCreature().HasFlag(CreatureStaticFlags.Unkillable))
            {
                damageTaken = health - 1;

                // If we had damage (aka health was not 1 already) trigger OnHealthDepleted
                if (damageTaken > 0)
                    victim.ToCreature().GetAI()?.OnHealthDepleted(attacker, false);
            }
            else if (victim.IsVehicle() && damageTaken >= (health - 1) 
                && victim.GetCharmer() != null && victim.GetCharmer().IsTypeId(TypeId.Player))
            {
                Player victimRider = victim.GetCharmer().ToPlayer();
                if (victimRider != null && victimRider.duel != null && victimRider.duel.IsMounted)
                {
                    if (attacker == null)
                        return 0;

                    // prevent kill only if killed in duel and killed by opponent or opponent controlled creature
                    if (victimRider.duel.Opponent == attacker.GetControllingPlayer())
                        damageTaken = health - 1;

                    duel_wasMounted = true;
                    duel_hasEnded = true;
                }
            }

            if (attacker != null && attacker != victim)
            {
                Player killer = attacker.ToPlayer();
                if (killer != null)
                {
                    // in bg, count dmg if victim is also a player
                    if (victim.IsPlayer() && !(spellProto != null 
                        && spellProto.HasAttribute(SpellAttr7.DoNotCountForPvpScoreboard)))
                    {
                        Battleground bg = killer.GetBattleground();
                        if (bg != null)
                            bg.UpdatePlayerScore(killer, ScoreType.DamageDone, damageDone);
                    }

                    killer.UpdateCriteria(CriteriaType.DamageDealt, health > damageDone ? damageDone : health, 0, 0, victim);
                    killer.UpdateCriteria(CriteriaType.HighestDamageDone, damageDone);
                }
            }

            if (victim.IsPlayer())
                victim.ToPlayer().UpdateCriteria(CriteriaType.HighestDamageTaken, damageTaken);

            if (victim.GetTypeId() != TypeId.Player && (!victim.IsControlledByPlayer() || victim.IsVehicle()))
            {
                victim.ToCreature().SetTappedBy(attacker);

                if (attacker == null || attacker.IsControlledByPlayer())
                    victim.ToCreature().LowerPlayerDamageReq(health < damageTaken ? health : damageTaken);
            }

            bool killed = false;
            bool skipSettingDeathState = false;

            if (health <= damageTaken)
            {
                killed = true;

                if (victim.IsPlayer() && victim != attacker)
                    victim.ToPlayer().UpdateCriteria(CriteriaType.TotalDamageTaken, health);

                if (damagetype != DamageEffectType.NoDamage && damagetype != DamageEffectType.Self 
                    && victim.HasAuraType(AuraType.SchoolAbsorbOverkill))
                {
                    var vAbsorbOverkill = victim.GetAuraEffectsByType(AuraType.SchoolAbsorbOverkill);
                    DamageInfo damageInfo = 
                        new(attacker, victim, damageTaken, spellProto, damageSchoolMask, damagetype, 
                        cleanDamage != null ? cleanDamage.attackType : WeaponAttackType.BaseAttack);

                    foreach (var absorbAurEff in vAbsorbOverkill)
                    {
                        Aura baseAura = absorbAurEff.GetBase();
                        AuraApplication aurApp = baseAura.GetApplicationOfTarget(victim.GetGUID());
                        if (aurApp == null)
                            continue;

                        if ((absorbAurEff.GetMiscValue() & (int)damageInfo.GetSchoolMask()) == 0)
                            continue;

                        // cannot absorb over limit
                        if (damageTaken >= victim.CountPctFromMaxHealth(100 + absorbAurEff.GetMiscValueB()))
                            continue;

                        // absorb all damage by default
                        var currentAbsorb = damageInfo.GetDamage();

                        // This aura Type is used both by Spirit of Redemption
                        // (death not really prevented, must grant all credit immediately)
                        // and Cheat Death (death prevented)
                        // repurpose PreventDefaultAction for this
                        bool deathFullyPrevented = false;

                        absorbAurEff.GetBase().CallScriptEffectAbsorbHandlers(
                            absorbAurEff, aurApp, damageInfo, ref currentAbsorb, ref deathFullyPrevented);

                        // absorb must be smaller than the damage itself
                        currentAbsorb = Math.Min(currentAbsorb, damageInfo.GetDamage());

                        // if nothing is absorbed (for example because of a scripted cooldown) then skip this aura and proceed with dying
                        if (currentAbsorb == 0)
                            continue;

                        damageInfo.AbsorbDamage(currentAbsorb);

                        if (deathFullyPrevented)
                            killed = false;

                        skipSettingDeathState = true;

                        if (currentAbsorb != 0)
                        {
                            SpellAbsorbLog absorbLog = new();
                            absorbLog.Attacker = attacker != null ? attacker.GetGUID() : ObjectGuid.Empty;
                            absorbLog.Victim = victim.GetGUID();
                            absorbLog.Caster = baseAura.GetCasterGUID();
                            absorbLog.AbsorbedSpellID = spellProto != null ? spellProto.Id : 0;
                            absorbLog.AbsorbSpellID = baseAura.GetId();
                            absorbLog.Absorbed = currentAbsorb;
                            absorbLog.OriginalDamage = damageInfo.GetOriginalDamage();
                            absorbLog.LogData.Initialize(victim);
                            victim.SendCombatLogMessage(absorbLog);
                        }
                    }

                    damageTaken = damageInfo.GetDamage();
                }
            }

            if (spellProto != null && spellProto.HasAttribute(SpellAttr3.NoDurabilityLoss))
                durabilityLoss = false;

            if (killed)
                Kill(attacker, victim, durabilityLoss, skipSettingDeathState);
            else
            {
                if (victim.IsTypeId(TypeId.Player))
                    victim.ToPlayer().UpdateCriteria(CriteriaType.TotalDamageTaken, damageTaken);

                victim.ModifyHealth(-damageTaken);

                if (damagetype == DamageEffectType.Direct || damagetype == DamageEffectType.SpellDirect)
                    victim.RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.NonPeriodicDamage, spellProto);

                if (!victim.IsTypeId(TypeId.Player))
                {
                    // Part of Evade mechanics. DoT's and Thorns / Retribution Aura do not contribute to this
                    if (damagetype != DamageEffectType.DOT && damageTaken > 0
                        && !victim.GetOwnerGUID().IsPlayer()
                        && (spellProto == null || !spellProto.HasAura(AuraType.DamageShield)))
                    {
                        victim.ToCreature().SetLastDamagedTime(LoopTime.ServerTime + SharedConst.MaxAggroResetTime);
                    }

                    if (attacker != null && (spellProto == null || !spellProto.HasAttribute(SpellAttr4.NoHarmfulThreat)))
                        victim.GetThreatManager().AddThreat(attacker, damageTaken, spellProto);
                }
                else                                                // victim is a player
                {
                    // random durability for items (HIT TAKEN)
                    if (durabilityLoss && WorldConfig.Values[WorldCfg.RateDurabilityLossDamage].Float > RandomHelper.randPercent())
                    {
                        byte slot = (byte)RandomHelper.IRand(0, EquipmentSlot.End - 1);
                        victim.ToPlayer().DurabilityPointLossForEquipSlot(slot);
                    }
                }

                // Rage from damage received
                if (attacker != victim && victim.GetPowerType() == PowerType.Rage)
                {
                    rage_damage = damage + (cleanDamage != null ? cleanDamage.absorbed_damage : 0);
                    victim.RewardRage(rage_damage, 0, false);
                }

                if (attacker != null && attacker.IsPlayer())
                {
                    // random durability for items (HIT DONE)
                    if (durabilityLoss && RandomHelper.randChance(WorldConfig.Values[WorldCfg.RateDurabilityLossDamage].Float))
                    {
                        byte slot = (byte)RandomHelper.IRand(0, EquipmentSlot.End - 1);
                        attacker.ToPlayer().DurabilityPointLossForEquipSlot(slot);
                    }
                }

                if (damagetype != DamageEffectType.NoDamage && damagetype != DamageEffectType.DOT)
                {
                    if (victim != attacker 
                        && (spellProto == null 
                        || !(spellProto.HasAttribute(SpellAttr6.NoPushback) 
                        || spellProto.HasAttribute(SpellAttr7.DontCauseSpellPushback) 
                        || spellProto.HasAttribute(SpellAttr3.TreatAsPeriodic))))
                    {
                        Spell spell = victim.GetCurrentSpell(CurrentSpellTypes.Generic);
                        if (spell != null)
                        {
                            if (spell.GetState() == SpellState.Preparing)
                            {
                                bool isCastInterrupted()
                                {
                                    if (damageTaken == 0)
                                        return spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.ZeroDamageCancels);

                                    if (victim.IsPlayer() 
                                        && spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamageCancelsPlayerOnly))
                                        return true;

                                    if (spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamageCancels))
                                        return true;

                                    return false;
                                };

                                bool isCastDelayed()
                                {
                                    if (damageTaken == 0)
                                        return false;

                                    if (victim.IsPlayer()
                                        && spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamagePushbackPlayerOnly))
                                        return true;

                                    if (spell.m_spellInfo.InterruptFlags.HasAnyFlag(SpellInterruptFlags.DamagePushback))
                                        return true;

                                    return false;
                                }

                                if (isCastInterrupted())
                                    victim.InterruptNonMeleeSpells(false);
                                else if (isCastDelayed())
                                    spell.Delayed();
                            }
                        }
                    }

                    if (damageTaken != 0 && victim.IsPlayer())
                    {
                        Spell spell1 = victim.GetCurrentSpell(CurrentSpellTypes.Channeled);
                        if (spell1 != null)
                        {
                            if (spell1.GetState() == SpellState.Casting
                                && spell1.m_spellInfo.HasChannelInterruptFlag(SpellAuraInterruptFlags.DamageChannelDuration))
                            {
                                spell1.DelayedChannel();
                            }
                        }
                    }
                }

                // last damage from duel opponent
                if (duel_hasEnded)
                {
                    Player he = duel_wasMounted ? victim.GetCharmer().ToPlayer() : victim.ToPlayer();

                    Cypher.Assert(he != null && he.duel != null);

                    if (duel_wasMounted) // In this case victim==mount
                        victim.SetHealth(1);
                    else
                        he.SetHealth(1);

                    he.duel.Opponent.CombatStopWithPets(true);
                    he.CombatStopWithPets(true);

                    he.CastSpell(he, 7267, true);                  // beg
                    he.DuelComplete(DuelCompleteType.Won);
                }
            }

            // make player victims stand up automatically
            if (victim.GetStandState() != 0 && victim.IsPlayer())
                victim.SetStandState(UnitStandStateType.Stand);

            return damageTaken;
        }

        void DealMeleeDamage(CalcDamageInfo damageInfo, bool durabilityLoss)
        {
            Unit victim = damageInfo.Target;

            if (!victim.IsAlive() || victim.HasUnitState(UnitState.InFlight) 
                || (victim.IsTypeId(TypeId.Unit) && victim.ToCreature().IsEvadingAttacks()))
                return;

            if (damageInfo.TargetState == VictimState.Parry &&
                (!victim.IsCreature() || victim.ToCreature().GetCreatureTemplate().FlagsExtra.HasAnyFlag(CreatureFlagsExtra.NoParryHasten)))
            {
                // Get attack timers
                Milliseconds offtime = victim.GetAttackTimer(WeaponAttackType.OffAttack);
                Milliseconds basetime = victim.GetAttackTimer(WeaponAttackType.BaseAttack);

                // Reduce attack time
                if (victim.HaveOffhandWeapon() && offtime < basetime)
                {
                    Milliseconds percent20 = (Milliseconds)(victim.GetBaseAttackTime(WeaponAttackType.OffAttack) * 20 / 100);
                    Milliseconds percent60 = (Milliseconds)(3 * percent20);
                    if (offtime > percent20 && offtime <= percent60)
                        victim.SetAttackTimer(WeaponAttackType.OffAttack, percent20);
                    else if (offtime > percent60)
                    {
                        offtime -= (Milliseconds)(2 * percent20);
                        victim.SetAttackTimer(WeaponAttackType.OffAttack, offtime);
                    }
                }
                else
                {
                    Milliseconds percent20 = (Milliseconds)(victim.GetBaseAttackTime(WeaponAttackType.BaseAttack) * 20 / 100);
                    Milliseconds percent60 = (Milliseconds)(3 * percent20);
                    if (basetime > percent20 && basetime <= percent60)
                        victim.SetAttackTimer(WeaponAttackType.BaseAttack, percent20);
                    else if (basetime > percent60)
                    {
                        basetime -= (Milliseconds)(2 * percent20);
                        victim.SetAttackTimer(WeaponAttackType.BaseAttack, basetime);
                    }
                }
            }

            // Call default DealDamage
            CleanDamage cleanDamage = new(damageInfo.CleanDamage, damageInfo.Absorb, damageInfo.AttackType, damageInfo.HitOutCome);
            DealDamage(this, victim, damageInfo.Damage, cleanDamage, DamageEffectType.Direct, damageInfo.DamageSchoolMask, null, durabilityLoss);

            // If this is a creature and it attacks from behind it has a probability to daze it's victim
            if ((damageInfo.HitOutCome == MeleeHitOutcome.Crit || damageInfo.HitOutCome == MeleeHitOutcome.Crushing || damageInfo.HitOutCome == MeleeHitOutcome.Normal || damageInfo.HitOutCome == MeleeHitOutcome.Glancing) &&
                !IsTypeId(TypeId.Player) && !ToCreature().IsControlledByPlayer() && !victim.HasInArc(MathFunctions.PI, this)
                && (victim.IsTypeId(TypeId.Player) || !victim.ToCreature().IsWorldBoss()) && !victim.IsVehicle())
            {
                // 20% base Chance
                float chance = 20.0f;

                // there is a newbie protection, at level 10 just 7% base Chance; assuming linear function
                if (victim.GetLevel() < 30)
                    chance = 0.65f * victim.GetLevelForTarget(this) + 0.5f;

                uint victimDefense = victim.GetMaxSkillValueForLevel(this);
                uint attackerMeleeSkill = GetMaxSkillValueForLevel();

                chance *= attackerMeleeSkill / (float)victimDefense * 0.16f;

                // -probability is between 0% and 40%
                MathFunctions.RoundToInterval(ref chance, 0.0f, 40.0f);

                if (RandomHelper.randChance(chance))
                    CastSpell(victim, 1604, true);
            }

            if (IsTypeId(TypeId.Player))
            {
                DamageInfo dmgInfo = new(damageInfo);
                ToPlayer().CastItemCombatSpell(dmgInfo);
            }

            // Do effect if any damage done to target
            if (damageInfo.Damage != 0)
            {
                // We're going to call functions which can modify content of the list during iteration over it's elements
                // Let's copy the list so we can prevent iterator invalidation
                var vDamageShieldsCopy = victim.GetAuraEffectsByType(AuraType.DamageShield);
                foreach (var dmgShield in vDamageShieldsCopy)
                {
                    SpellInfo spellInfo = dmgShield.GetSpellInfo();

                    // Damage shield can be resisted...
                    var missInfo = victim.SpellHitResult(this, spellInfo, false);
                    if (missInfo != SpellMissInfo.None)
                    {
                        victim.SendSpellMiss(this, spellInfo.Id, missInfo);
                        continue;
                    }

                    // ...or immuned
                    if (IsImmunedToDamage(spellInfo))
                    {
                        victim.SendSpellDamageImmune(this, spellInfo.Id, false);
                        continue;
                    }

                    var damage = dmgShield.GetAmount();
                    Unit caster = dmgShield.GetCaster();
                    if (caster != null)
                    {
                        damage = caster.SpellDamageBonusDone(
                            this, spellInfo, damage, DamageEffectType.SpellDirect, dmgShield.GetSpellEffectInfo());

                        damage = SpellDamageBonusTaken(
                            caster, spellInfo, damage, DamageEffectType.SpellDirect);
                    }

                    DamageInfo damageInfo1 = 
                        new(this, victim, damage, spellInfo, spellInfo.GetSchoolMask(), 
                        DamageEffectType.SpellDirect, WeaponAttackType.BaseAttack);

                    CalcAbsorbResist(damageInfo1);
                    damage = damageInfo1.GetDamage();

                    DealDamageMods(victim, this, ref damage);

                    SpellDamageShield damageShield = new();
                    damageShield.Attacker = victim.GetGUID();
                    damageShield.Defender = GetGUID();
                    damageShield.SpellID = spellInfo.Id;
                    damageShield.TotalDamage = damage;
                    damageShield.OriginalDamage = damageInfo.OriginalDamage;
                    damageShield.OverKill = (int)Math.Max(damage - GetHealth(), 0);
                    damageShield.SchoolMask = spellInfo.SchoolMask;
                    damageShield.LogAbsorbed = damageInfo1.GetAbsorb();

                    DealDamage(victim, this, damage, null, DamageEffectType.SpellDirect, spellInfo.GetSchoolMask(), spellInfo, true);
                    damageShield.LogData.Initialize(this);

                    victim.SendCombatLogMessage(damageShield);
                }
            }
        }

        public long ModifyHealth(long dVal)
        {
            long gain = 0;

            if (dVal == 0)
                return 0;

            long curHealth = GetHealth();

            long val = dVal + curHealth;
            if (val <= 0)
            {
                SetHealth(0);
                return -curHealth;
            }

            long maxHealth =GetMaxHealth();
            if (val < maxHealth)
            {
                SetHealth(val);
                gain = val - curHealth;
            }
            else if (curHealth != maxHealth)
            {
                SetHealth(maxHealth);
                gain = maxHealth - curHealth;
            }

            if (dVal < 0)
            {
                HealthUpdate packet = new();
                packet.Guid = GetGUID();
                packet.Health = GetHealth();

                Player player = GetCharmerOrOwnerPlayerOrPlayerItself();
                if (player != null)
                    player.SendPacket(packet);
            }

            return gain;
        }

        public long GetHealthGain(long dVal)
        {
            long gain = 0;

            if (dVal == 0)
                return 0;

            long curHealth = GetHealth();

            long val = dVal + curHealth;
            if (val <= 0)
            {
                return -curHealth;
            }

            long maxHealth = GetMaxHealth();

            if (val < maxHealth)
                gain = dVal;
            else if (curHealth != maxHealth)
                gain = maxHealth - curHealth;

            return gain;
        }

        void TriggerOnHealthChangeAuras(long oldVal, long newVal)
        {
            foreach (AuraEffect effect in GetAuraEffectsByType(AuraType.TriggerSpellOnHealthPct))
            {
                var triggerHealthPct = effect.GetAmount();
                var triggerSpell = effect.GetSpellEffectInfo().TriggerSpell;
                var threshold = CountPctFromMaxHealth(triggerHealthPct);

                switch ((AuraTriggerOnHealthChangeDirection)effect.GetMiscValue())
                {
                    case AuraTriggerOnHealthChangeDirection.Above:
                        if (newVal < threshold || oldVal > threshold)
                            continue;
                        break;
                    case AuraTriggerOnHealthChangeDirection.Below:
                        if (newVal > threshold || oldVal < threshold)
                            continue;
                        break;
                    default:
                        break;
                }

                CastSpell(this, triggerSpell, new CastSpellExtraArgs(effect));
            }
        }

        public bool IsImmuneToAll() { return IsImmuneToPC() && IsImmuneToNPC(); }

        public void SetImmuneToAll(bool apply, bool keepCombat)
        {
            if (apply)
            {
                SetUnitFlag(UnitFlags.ImmuneToPc | UnitFlags.ImmuneToNpc);
                ValidateAttackersAndOwnTarget();
                if (!keepCombat)
                    m_combatManager.EndAllCombat();
            }
            else
                RemoveUnitFlag(UnitFlags.ImmuneToPc | UnitFlags.ImmuneToNpc);
        }

        public virtual void SetImmuneToAll(bool apply) { SetImmuneToAll(apply, false); }

        public bool IsImmuneToPC() { return HasUnitFlag(UnitFlags.ImmuneToPc); }

        public void SetImmuneToPC(bool apply, bool keepCombat)
        {
            if (apply)
            {
                SetUnitFlag(UnitFlags.ImmuneToPc);
                ValidateAttackersAndOwnTarget();
                if (!keepCombat)
                {
                    List<CombatReference> toEnd = new();
                    foreach (var pair in m_combatManager.GetPvECombatRefs())
                    {
                        if (pair.Value.GetOther(this).HasUnitFlag(UnitFlags.PlayerControlled))
                            toEnd.Add(pair.Value);
                    }

                    foreach (var pair in m_combatManager.GetPvPCombatRefs())
                    {
                        if (pair.Value.GetOther(this).HasUnitFlag(UnitFlags.PlayerControlled))
                            toEnd.Add(pair.Value);
                    }

                    foreach (CombatReference refe in toEnd)
                        refe.EndCombat();
                }
            }
            else
                RemoveUnitFlag(UnitFlags.ImmuneToPc);
        }

        public virtual void SetImmuneToPC(bool apply) { SetImmuneToPC(apply, false); }

        public bool IsImmuneToNPC() { return HasUnitFlag(UnitFlags.ImmuneToNpc); }

        public void SetImmuneToNPC(bool apply, bool keepCombat)
        {
            if (apply)
            {
                SetUnitFlag(UnitFlags.ImmuneToNpc);
                ValidateAttackersAndOwnTarget();
                if (!keepCombat)
                {
                    List<CombatReference> toEnd = new();
                    foreach (var pair in m_combatManager.GetPvECombatRefs())
                    {
                        if (!pair.Value.GetOther(this).HasUnitFlag(UnitFlags.PlayerControlled))
                            toEnd.Add(pair.Value);
                    }

                    foreach (var pair in m_combatManager.GetPvPCombatRefs())
                    {
                        if (!pair.Value.GetOther(this).HasUnitFlag(UnitFlags.PlayerControlled))
                            toEnd.Add(pair.Value);
                    }

                    foreach (CombatReference refe in toEnd)
                        refe.EndCombat();
                }
            }
            else
                RemoveUnitFlag(UnitFlags.ImmuneToNpc);
        }

        public virtual void SetImmuneToNPC(bool apply) { SetImmuneToNPC(apply, false); }

        public bool IsUninteractible() { return HasUnitFlag(UnitFlags.Uninteractible); }

        public void SetUninteractible(bool apply)
        {
            if (apply)
                SetUnitFlag(UnitFlags.Uninteractible);
            else
                RemoveUnitFlag(UnitFlags.Uninteractible);
        }

        public virtual float GetBlockPercent(int attackerLevel) { return 30.0f; }

        void UpdateReactives(Milliseconds p_time)
        {
            for (ReactiveType reactive = 0; reactive < ReactiveType.Max; ++reactive)
            {
                if (!m_reactiveTimer.ContainsKey(reactive))
                    continue;

                if (m_reactiveTimer[reactive] <= p_time)
                {
                    m_reactiveTimer[reactive] = Milliseconds.Zero;

                    switch (reactive)
                    {
                        case ReactiveType.Defense:
                            if (HasAuraState(AuraStateType.Defensive))
                                ModifyAuraState(AuraStateType.Defensive, false);
                            break;
                        case ReactiveType.Defense2:
                            if (HasAuraState(AuraStateType.Defensive2))
                                ModifyAuraState(AuraStateType.Defensive2, false);
                            break;
                    }
                }
                else
                {
                    m_reactiveTimer[reactive] -= p_time;
                }
            }
        }

        public void RewardRage(int damage, int weaponSpeedHitFactor, bool attacker)
        {
            float addRage;
            int level = GetLevel();

            float rageconversion = (0.0091107836f * level * level) + 3.225598133f * level + 4.2652911f;

            // Unknown if correct, but lineary adjust rage conversion above level 70
            if (level > 70)
                rageconversion += 13.27f * (level - 70);

            if (attacker)
        {
                addRage = (damage / rageconversion * 7.5f + weaponSpeedHitFactor) / 2;

            // talent who gave more rage on attack
            MathFunctions.AddPct(ref addRage, GetTotalAuraModifier(AuraType.ModRageFromDamageDealt));
            }
            else
            {
                addRage = damage / rageconversion * 2.5f;

                // Berserker Rage effect
                if (HasAura(18499))
                    addRage *= 2.0f;
            }

            ModifyPower(PowerType.Rage, (int)(addRage * 10));
        }

        public float GetPPMProcChance(Milliseconds WeaponSpeed, float PPM, SpellInfo spellProto)
        {
            // proc per minute Chance calculation
            if (PPM <= 0)
                return 0.0f;

            // Apply Chance modifer aura
            if (spellProto != null)
            {
                Player modOwner = GetSpellModOwner();
                if (modOwner != null)
                    modOwner.ApplySpellMod(spellProto, SpellModOp.ProcFrequency, ref PPM);
            }

            return (float)Math.Floor(WeaponSpeed * PPM / 600.0f);   // result is Chance in percents (probability = Speed_in_sec * (PPM / 60))
        }

        public Unit GetNextRandomRaidMemberOrPet(float radius)
        {
            Player player = null;
            if (IsTypeId(TypeId.Player))
                player = ToPlayer();
            // Should we enable this also for charmed units?
            else if (IsTypeId(TypeId.Unit) && IsPet())
                player = GetOwner().ToPlayer();

            if (player == null)
                return null;
            Group group = player.GetGroup();
            // When there is no group check pet presence
            if (group == null)
            {
                // We are pet now, return owner
                if (player != this)
                    return IsWithinDistInMap(player, radius) ? player : null;
                Unit pet = GetGuardianPet();
                // No pet, no group, nothing to return
                if (pet == null)
                    return null;
                // We are owner now, return pet
                return IsWithinDistInMap(pet, radius) ? pet : null;
            }

            List<Unit> nearMembers = new();
            // reserve place for players and pets because resizing vector every unit push is unefficient (vector is reallocated then)

            for (GroupReference refe = group.GetFirstMember(); refe != null; refe = refe.Next())
            {
                Player target = refe.GetSource();
                if (target != null)
                {
                    // IsHostileTo check duel and controlled by enemy
                    if (target != this && IsWithinDistInMap(target, radius) && target.IsAlive() && !IsHostileTo(target))
                        nearMembers.Add(target);

                    // Push player's pet to vector
                    Unit pet = target.GetGuardianPet();
                    if (pet != null)
                    {
                        if (pet != this && IsWithinDistInMap(pet, radius) && pet.IsAlive() && !IsHostileTo(pet))
                            nearMembers.Add(pet);
                    }
                }
            }

            if (nearMembers.Empty())
                return null;

            int randTarget = RandomHelper.IRand(0, nearMembers.Count - 1);
            return nearMembers[randTarget];
        }

        public void ClearAllReactives()
        {
            for (ReactiveType i = 0; i < ReactiveType.Max; ++i)
                m_reactiveTimer[i] = Milliseconds.Zero;

            if (HasAuraState(AuraStateType.Defensive))
                ModifyAuraState(AuraStateType.Defensive, false);
            if (HasAuraState(AuraStateType.Defensive2))
                ModifyAuraState(AuraStateType.Defensive2, false);
        }

        public virtual void SetPvP(bool state)
        {
            if (state)
                SetPvpFlag(UnitPVPStateFlags.PvP);
            else
                RemovePvpFlag(UnitPVPStateFlags.PvP);
        }

        static int CalcSpellResistedDamage(Unit attacker, Unit victim, int damage, SpellSchoolMask schoolMask, SpellInfo spellInfo)
        {
            // Magic damage, check for resists
            if (!Convert.ToBoolean(schoolMask & SpellSchoolMask.Magic))
                return 0;

            // Npcs can have holy resistance
            if (schoolMask.HasAnyFlag(SpellSchoolMask.Holy) && victim.GetTypeId() != TypeId.Unit)
                return 0;

            float averageResist = CalculateAverageResistReduction(attacker, schoolMask, victim, spellInfo);

            float[] discreteResistProbability = new float[11];
            if (averageResist <= 0.1f)
            {
                discreteResistProbability[0] = 1.0f - 7.5f * averageResist;
                discreteResistProbability[1] = 5.0f * averageResist;
                discreteResistProbability[2] = 2.5f * averageResist;
            }
            else
            {
                for (uint i = 0; i < 11; ++i)
                    discreteResistProbability[i] = Math.Max(0.5f - 2.5f * Math.Abs(0.1f * i - averageResist), 0.0f);
            }

            float roll = RandomHelper.NextSingle();
            float probabilitySum = 0.0f;

            uint resistance = 0;
            for (; resistance < 11; ++resistance)
            {
                if (roll < (probabilitySum += discreteResistProbability[resistance]))
                    break;
            }

            float damageResisted = damage * resistance / 10f;

            if (damageResisted > 0.0f) // if any damage was resisted
            {
                int ignoredResistance = 0;

                if (attacker != null)
                    ignoredResistance += attacker.GetTotalAuraModifierByMiscMask(AuraType.ModIgnoreTargetResist, (uint)schoolMask);

                ignoredResistance = Math.Min(ignoredResistance, 100);
                MathFunctions.ApplyPct(ref damageResisted, 100 - ignoredResistance);

                // Spells with melee and magic school mask, decide whether resistance or armor absorb is higher
                if (spellInfo != null && spellInfo.HasAttribute(SpellCustomAttributes.SchoolmaskNormalWithMagic))
                {
                    var damageAfterArmor = CalcArmorReducedDamage(attacker, victim, damage, spellInfo, spellInfo.GetAttackType());
                    float armorReduction = damage - damageAfterArmor;

                    // pick the lower one, the weakest resistance counts
                    damageResisted = Math.Min(damageResisted, armorReduction);
                }
            }

            damageResisted = Math.Max(damageResisted, 0.0f);
            return (int)damageResisted;
        }

        static float CalculateAverageResistReduction(WorldObject caster, SpellSchoolMask schoolMask, Unit victim, SpellInfo spellInfo = null)
        {
            float victimResistance = victim.GetResistance(schoolMask);

            if (caster != null)
            {
                // pets inherit 100% of masters penetration
                Player player = caster.GetSpellModOwner();
                if (player != null)
                {
                    victimResistance += player.GetTotalAuraModifierByMiscMask(AuraType.ModTargetResistance, (uint)schoolMask);
                    victimResistance -= player.GetSpellPenetrationItemMod();
                }
                else
                {
                    Unit unitCaster = caster.ToUnit();
                    if (unitCaster != null)
                        victimResistance += unitCaster.GetTotalAuraModifierByMiscMask(AuraType.ModTargetResistance, (uint)schoolMask);
                }
            }

            // holy resistance exists in pve and comes from level difference, ignore template values
            if (schoolMask.HasAnyFlag(SpellSchoolMask.Holy))
                victimResistance = 0.0f;

            // Chaos Bolt exception, ignore all target resistances (unknown attribute?)
            if (spellInfo != null && spellInfo.SpellFamilyName == SpellFamilyNames.Warlock && spellInfo.Id == 116858)
                victimResistance = 0.0f;

            victimResistance = Math.Max(victimResistance, 0.0f);

            // level-based resistance does not apply to binary spells, and cannot be overcome by spell penetration
            // gameobject caster -- should it have level based resistance?
            if (caster != null && !caster.IsGameObject() && (spellInfo == null
                || !spellInfo.HasAttribute(SpellCustomAttributes.BinarySpell)))
            {
                victimResistance += Math.Max((victim.GetLevelForTarget(caster) - (float)caster.GetLevelForTarget(victim)) * 5.0f, 0.0f);
            }

            var bossLevel = 83;
            float bossResistanceConstant = 510.0f;
            var level = caster != null ? victim.GetLevelForTarget(caster) : victim.GetLevel();
            float resistanceConstant;

            if (level == bossLevel)
                resistanceConstant = bossResistanceConstant;
            else
                resistanceConstant = level * 5.0f;

            return victimResistance / (victimResistance + resistanceConstant);
        }

        public static void CalcAbsorbResist(DamageInfo damageInfo, Spell spell = null)
        {
            if (damageInfo.GetVictim() == null || !damageInfo.GetVictim().IsAlive() || damageInfo.GetDamage() == 0)
                return;

            var resistedDamage = CalcSpellResistedDamage(
                damageInfo.GetAttacker(), damageInfo.GetVictim(), damageInfo.GetDamage(), 
                damageInfo.GetSchoolMask(), damageInfo.GetSpellInfo());

            // Ignore Absorption Auras
            float auraAbsorbMod = 0f;

            Unit attacker = damageInfo.GetAttacker();
            if (attacker != null)
            {
                auraAbsorbMod = attacker.GetMaxPositiveAuraModifierByMiscMask(
                    AuraType.ModTargetAbsorbSchool, (uint)damageInfo.GetSchoolMask());
            }

            MathFunctions.RoundToInterval(ref auraAbsorbMod, 0.0f, 100.0f);

            int absorbIgnoringDamage = MathFunctions.CalculatePct(damageInfo.GetDamage(), auraAbsorbMod);
            if (spell != null)
                spell.CallScriptOnResistAbsorbCalculateHandlers(damageInfo, ref resistedDamage, ref absorbIgnoringDamage);

            damageInfo.ResistDamage(resistedDamage);

            // We're going to call functions which can modify content of the list during iteration over it's elements
            // Let's copy the list so we can prevent iterator invalidation
            var vSchoolAbsorbCopy = damageInfo.GetVictim().GetAuraEffectsByType(AuraType.SchoolAbsorb).ToList();
            vSchoolAbsorbCopy.Sort(new AbsorbAuraOrderPred());

            // absorb without mana cost
            for (var i = 0; i < vSchoolAbsorbCopy.Count && (damageInfo.GetDamage() > 0); ++i)
            {
                var absorbAurEff = vSchoolAbsorbCopy[i];

                // Check if aura was removed during iteration - we don't need to work on such auras
                AuraApplication aurApp = absorbAurEff.GetBase().GetApplicationOfTarget(damageInfo.GetVictim().GetGUID());
                if (aurApp == null)
                    continue;
                if ((absorbAurEff.GetMiscValue() & (int)damageInfo.GetSchoolMask()) == 0)
                    continue;

                // get amount which can be still absorbed by the aura
                int currentAbsorb = absorbAurEff.GetAmount();
                // aura with infinite absorb amount - let the scripts handle absorbtion amount, set here to 0 for safety
                if (currentAbsorb < 0)
                    currentAbsorb = 0;

                if (!absorbAurEff.GetSpellInfo().HasAttribute(SpellAttr6.AbsorbCannotBeIgnore))
                    damageInfo.ModifyDamage(-absorbIgnoringDamage);

                var tempAbsorb = currentAbsorb;

                bool defaultPrevented = false;

                absorbAurEff.GetBase().CallScriptEffectAbsorbHandlers(
                    absorbAurEff, aurApp, damageInfo, ref tempAbsorb, ref defaultPrevented);

                currentAbsorb = tempAbsorb;

                if (!defaultPrevented)
                {
                    // absorb must be smaller than the damage itself
                    currentAbsorb = MathFunctions.RoundToInterval(ref currentAbsorb, 0, damageInfo.GetDamage());

                    damageInfo.AbsorbDamage(currentAbsorb);

                    tempAbsorb = currentAbsorb;
                    absorbAurEff.GetBase().CallScriptEffectAfterAbsorbHandlers(absorbAurEff, aurApp, damageInfo, ref tempAbsorb);

                    // Check if our aura is using amount to count heal
                    if (absorbAurEff.GetAmount() >= 0)
                    {
                        // Reduce shield amount
                        absorbAurEff.ChangeAmount(absorbAurEff.GetAmount() - currentAbsorb);
                        // Aura cannot absorb anything more - remove it
                        if (absorbAurEff.GetAmount() <= 0)
                            absorbAurEff.GetBase().Remove(AuraRemoveMode.EnemySpell);
                    }
                }

                if (!absorbAurEff.GetSpellInfo().HasAttribute(SpellAttr6.AbsorbCannotBeIgnore))
                    damageInfo.ModifyDamage(absorbIgnoringDamage);

                if (currentAbsorb != 0)
                {
                    SpellAbsorbLog absorbLog = new();
                    absorbLog.Attacker = damageInfo.GetAttacker() != null ? damageInfo.GetAttacker().GetGUID() : ObjectGuid.Empty;
                    absorbLog.Victim = damageInfo.GetVictim().GetGUID();
                    absorbLog.Caster = absorbAurEff.GetBase().GetCasterGUID();
                    absorbLog.AbsorbedSpellID = damageInfo.GetSpellInfo() != null ? damageInfo.GetSpellInfo().Id : 0;
                    absorbLog.AbsorbSpellID = absorbAurEff.GetId();
                    absorbLog.Absorbed = currentAbsorb;
                    absorbLog.OriginalDamage = damageInfo.GetOriginalDamage();
                    absorbLog.LogData.Initialize(damageInfo.GetVictim());
                    damageInfo.GetVictim().SendCombatLogMessage(absorbLog);
                }
            }

            // absorb by mana cost
            var vManaShieldCopy = damageInfo.GetVictim().GetAuraEffectsByType(AuraType.ManaShield);
            foreach (var absorbAurEff in vManaShieldCopy)
            {
                if (damageInfo.GetDamage() == 0)
                    break;

                // Check if aura was removed during iteration - we don't need to work on such auras
                AuraApplication aurApp = absorbAurEff.GetBase().GetApplicationOfTarget(damageInfo.GetVictim().GetGUID());
                if (aurApp == null)
                    continue;
                // check damage school mask
                if (!Convert.ToBoolean(absorbAurEff.GetMiscValue() & (int)damageInfo.GetSchoolMask()))
                    continue;

                // get amount which can be still absorbed by the aura
                int currentAbsorb = absorbAurEff.GetAmount();
                // aura with infinite absorb amount - let the scripts handle absorbtion amount, set here to 0 for safety
                if (currentAbsorb < 0)
                    currentAbsorb = 0;

                if (!absorbAurEff.GetSpellInfo().HasAttribute(SpellAttr6.AbsorbCannotBeIgnore))
                    damageInfo.ModifyDamage(-absorbIgnoringDamage);

                var tempAbsorb = currentAbsorb;

                bool defaultPrevented = false;

                absorbAurEff.GetBase().CallScriptEffectManaShieldHandlers(
                    absorbAurEff, aurApp, damageInfo, ref tempAbsorb, ref defaultPrevented);

                currentAbsorb = tempAbsorb;

                if (!defaultPrevented)
                {
                    // absorb must be smaller than the damage itself
                    currentAbsorb = MathFunctions.RoundToInterval(ref currentAbsorb, 0, damageInfo.GetDamage());

                    int manaReduction = currentAbsorb;

                    // lower absorb amount by talents
                    float manaMultiplier = absorbAurEff.GetSpellEffectInfo().CalcValueMultiplier(absorbAurEff.GetCaster());
                    if (manaMultiplier != 0)
                        manaReduction = (int)(manaReduction * manaMultiplier);

                    int manaTaken = -damageInfo.GetVictim().ModifyPower(PowerType.Mana, -manaReduction);

                    // take case when mana has ended up into account
                    currentAbsorb = currentAbsorb != 0 ? (currentAbsorb * (manaTaken / manaReduction)) : 0;

                    damageInfo.AbsorbDamage(currentAbsorb);

                    tempAbsorb = currentAbsorb;
                    absorbAurEff.GetBase().CallScriptEffectAfterManaShieldHandlers(absorbAurEff, aurApp, damageInfo, ref tempAbsorb);

                    // Check if our aura is using amount to count damage
                    if (absorbAurEff.GetAmount() >= 0)
                    {
                        absorbAurEff.ChangeAmount(absorbAurEff.GetAmount() - currentAbsorb);
                        if (absorbAurEff.GetAmount() <= 0)
                            absorbAurEff.GetBase().Remove(AuraRemoveMode.EnemySpell);
                    }
                }

                if (!absorbAurEff.GetSpellInfo().HasAttribute(SpellAttr6.AbsorbCannotBeIgnore))
                    damageInfo.ModifyDamage(absorbIgnoringDamage);

                if (currentAbsorb != 0)
                {
                    SpellAbsorbLog absorbLog = new();
                    absorbLog.Attacker = damageInfo.GetAttacker() != null ? damageInfo.GetAttacker().GetGUID() : ObjectGuid.Empty;
                    absorbLog.Victim = damageInfo.GetVictim().GetGUID();
                    absorbLog.Caster = absorbAurEff.GetBase().GetCasterGUID();
                    absorbLog.AbsorbedSpellID = damageInfo.GetSpellInfo() != null ? damageInfo.GetSpellInfo().Id : 0;
                    absorbLog.AbsorbSpellID = absorbAurEff.GetId();
                    absorbLog.Absorbed = currentAbsorb;
                    absorbLog.OriginalDamage = damageInfo.GetOriginalDamage();
                    absorbLog.LogData.Initialize(damageInfo.GetVictim());
                    damageInfo.GetVictim().SendCombatLogMessage(absorbLog);
                }
            }

            // split damage auras - only when not damaging self
            if (damageInfo.GetVictim() != damageInfo.GetAttacker())
            {
                // We're going to call functions which can modify content of the list during iteration over it's elements
                // Let's copy the list so we can prevent iterator invalidation
                var vSplitDamagePctCopy = damageInfo.GetVictim().GetAuraEffectsByType(AuraType.SplitDamagePct);
                foreach (var itr in vSplitDamagePctCopy)
                {
                    if (damageInfo.GetDamage() == 0)
                        break;

                    // Check if aura was removed during iteration - we don't need to work on such auras
                    AuraApplication aurApp = itr.GetBase().GetApplicationOfTarget(damageInfo.GetVictim().GetGUID());
                    if (aurApp == null)
                        continue;

                    // check damage school mask
                    if (!Convert.ToBoolean(itr.GetMiscValue() & (int)damageInfo.GetSchoolMask()))
                        continue;

                    // Damage can be splitted only if aura has an alive caster
                    Unit caster = itr.GetCaster();
                    if (caster == null || (caster == damageInfo.GetVictim()) || !caster.IsInWorld || !caster.IsAlive())
                        continue;

                    var splitDamage = MathFunctions.CalculatePct(damageInfo.GetDamage(), itr.GetAmount());

                    itr.GetBase().CallScriptEffectSplitHandlers(itr, aurApp, damageInfo, splitDamage);

                    // absorb must be smaller than the damage itself
                    splitDamage = MathFunctions.RoundToInterval(ref splitDamage, 0, damageInfo.GetDamage());

                    damageInfo.AbsorbDamage(splitDamage);

                    // check if caster is immune to damage
                    if (caster.IsImmunedToDamage(damageInfo.GetSchoolMask()))
                    {
                        damageInfo.GetVictim().SendSpellMiss(caster, itr.GetSpellInfo().Id, SpellMissInfo.Immune);
                        continue;
                    }

                    var split_absorb = 0;
                    DealDamageMods(damageInfo.GetAttacker(), caster, ref splitDamage, ref split_absorb);

                    // sparring
                    Creature victimCreature = damageInfo.GetVictim().ToCreature();
                    if (victimCreature != null)
                    {
                        if (victimCreature.ShouldFakeDamageFrom(damageInfo.GetAttacker()))
                            damageInfo.ModifyDamage(-damageInfo.GetDamage());
                    }

                    SpellNonMeleeDamage log = 
                        new(damageInfo.GetAttacker(), caster, itr.GetSpellInfo(), 
                        itr.GetBase().GetSpellVisual(), damageInfo.GetSchoolMask(), itr.GetBase().GetCastId());

                    CleanDamage cleanDamage = new(splitDamage, 0, WeaponAttackType.BaseAttack, MeleeHitOutcome.Normal);

                    DealDamage(damageInfo.GetAttacker(), caster, splitDamage, cleanDamage, 
                        DamageEffectType.Direct, damageInfo.GetSchoolMask(), itr.GetSpellInfo(), false);

                    log.damage = splitDamage;
                    log.originalDamage = splitDamage;
                    log.absorb = split_absorb;
                    caster.SendSpellNonMeleeDamageLog(log);

                    // break 'Fear' and similar auras
                    ProcSkillsAndAuras(damageInfo.GetAttacker(), caster, 
                        new ProcFlagsInit(ProcFlags.None), 
                        new ProcFlagsInit(ProcFlags.TakeHarmfulSpell), 
                        ProcFlagsSpellType.Damage, ProcFlagsSpellPhase.Hit, ProcFlagsHit.None, null, damageInfo, null);
                }
            }
        }

        public static void CalcHealAbsorb(HealInfo healInfo)
        {
            if (healInfo.GetHeal() == 0)
                return;

            var vHealAbsorb = new List<AuraEffect>(healInfo.GetTarget().GetAuraEffectsByType(AuraType.SchoolHealAbsorb));
            for (var i = 0; i < vHealAbsorb.Count && healInfo.GetHeal() > 0; ++i)
            {
                AuraEffect absorbAurEff = vHealAbsorb[i];
                // Check if aura was removed during iteration - we don't need to work on such auras
                AuraApplication aurApp = absorbAurEff.GetBase().GetApplicationOfTarget(healInfo.GetTarget().GetGUID());
                if (aurApp == null)
                    continue;

                if ((absorbAurEff.GetMiscValue() & (int)healInfo.GetSchoolMask()) == 0)
                    continue;

                // get amount which can be still absorbed by the aura
                int currentAbsorb = absorbAurEff.GetAmount();
                // aura with infinite absorb amount - let the scripts handle absorbtion amount, set here to 0 for safety
                if (currentAbsorb < 0)
                    currentAbsorb = 0;

                var tempAbsorb = currentAbsorb;

                bool defaultPrevented = false;

                absorbAurEff.GetBase().CallScriptEffectAbsorbHandlers(
                    absorbAurEff, aurApp, healInfo, ref tempAbsorb, ref defaultPrevented);

                currentAbsorb = tempAbsorb;

                if (!defaultPrevented)
                {
                    // absorb must be smaller than the heal itself
                    currentAbsorb = MathFunctions.RoundToInterval(ref currentAbsorb, 0, healInfo.GetHeal());

                    healInfo.AbsorbHeal(currentAbsorb);

                    tempAbsorb = currentAbsorb;
                    absorbAurEff.GetBase().CallScriptEffectAfterAbsorbHandlers(absorbAurEff, aurApp, healInfo, ref tempAbsorb);

                    // Check if our aura is using amount to count heal
                    if (absorbAurEff.GetAmount() >= 0)
                    {
                        // Reduce shield amount
                        absorbAurEff.ChangeAmount(absorbAurEff.GetAmount() - currentAbsorb);
                        // Aura cannot absorb anything more - remove it
                        if (absorbAurEff.GetAmount() <= 0)
                            absorbAurEff.GetBase().Remove(AuraRemoveMode.EnemySpell);
                    }
                }

                if (currentAbsorb != 0)
                {
                    SpellHealAbsorbLog absorbLog = new();
                    absorbLog.Healer = healInfo.GetHealer() != null ? healInfo.GetHealer().GetGUID() : ObjectGuid.Empty;
                    absorbLog.Target = healInfo.GetTarget().GetGUID();
                    absorbLog.AbsorbCaster = absorbAurEff.GetBase().GetCasterGUID();
                    absorbLog.AbsorbedSpellID = healInfo.GetSpellInfo() != null ? healInfo.GetSpellInfo().Id : 0;
                    absorbLog.AbsorbSpellID = absorbAurEff.GetId();
                    absorbLog.Absorbed = currentAbsorb;
                    absorbLog.OriginalHeal = healInfo.GetOriginalHeal();
                    healInfo.GetTarget().SendMessageToSet(absorbLog, true);
                }
            }
        }

        public static int CalcArmorReducedDamage(Unit attacker, Unit victim, int damage, SpellInfo spellInfo, WeaponAttackType attackType = WeaponAttackType.Max, int attackerLevel = 0)
        {
            float armor = victim.GetArmor();

            if (attacker != null)
            {
                armor *= victim.GetArmorMultiplierForTarget(attacker);

                // bypass enemy armor by SPELL_AURA_BYPASS_ARMOR_FOR_CASTER
                int armorBypassPct = 0;
                var reductionAuras = victim.GetAuraEffectsByType(AuraType.BypassArmorForCaster);
                foreach (var eff in reductionAuras)
                {
                    if (eff.GetCasterGUID() == attacker.GetGUID())
                        armorBypassPct += eff.GetAmount();
                }

                armor = MathFunctions.CalculatePct(armor, 100 - Math.Min(armorBypassPct, 100));

                // Ignore enemy armor by SPELL_AURA_MOD_TARGET_RESISTANCE aura
                armor += attacker.GetTotalAuraModifierByMiscMask(AuraType.ModTargetResistance, (int)SpellSchoolMask.Normal);

                if (spellInfo != null)
                {
                    Player modOwner = attacker.GetSpellModOwner();
                    if (modOwner != null)
                        modOwner.ApplySpellMod(spellInfo, SpellModOp.TargetResistance, ref armor);
                }

                var resIgnoreAuras = attacker.GetAuraEffectsByType(AuraType.ModIgnoreTargetResist);
                foreach (var eff in resIgnoreAuras)
                {
                    if (eff.GetMiscValue().HasAnyFlag((int)SpellSchoolMask.Normal) && eff.IsAffectingSpell(spellInfo))
                        armor = (float)Math.Floor(MathFunctions.AddPct(ref armor, -eff.GetAmount()));
                }

                // Apply Player CR_ARMOR_PENETRATION rating
                if (attacker.IsPlayer())
                {
                    float arpPct = attacker.ToPlayer().GetRatingBonusValue(CombatRating.ArmorPenetration);

                    // no more than 100%
                    MathFunctions.RoundToInterval(ref arpPct, 0.0f, 100.0f);

                    float maxArmorPen;
                    if (victim.GetLevelForTarget(attacker) < 60)
                        maxArmorPen = 400 + 85 * victim.GetLevelForTarget(attacker);
                    else
                        maxArmorPen = 400 + 85 * victim.GetLevelForTarget(attacker) + 4.5f * 85 * (victim.GetLevelForTarget(attacker) - 59);

                    // Cap armor penetration to this number
                    maxArmorPen = Math.Min((armor + maxArmorPen) / 3.0f, armor);
                    // Figure out how much armor do we ignore
                    armor -= MathFunctions.CalculatePct(maxArmorPen, arpPct);
                }
            }

            if (MathFunctions.fuzzyLe(armor, 0.0f))
                armor = 0;

            float levelModifier = attacker != null ? attacker.GetLevel() : attackerLevel;
            if (levelModifier > 59.0f)
                levelModifier = levelModifier + 4.5f * (levelModifier - 59.0f);

            float damageReduction = 0.1f * armor / (8.5f * levelModifier + 40.0f);
            damageReduction /= (1.0f + damageReduction);

            MathFunctions.RoundToInterval(ref damageReduction, 0.0f, 0.75f);
            return (int)Math.Ceiling(Math.Max(damage * (1.0f - damageReduction), 0.0f));
        }

        public int MeleeDamageBonusDone(Unit victim, int damage, WeaponAttackType attType, DamageEffectType damagetype, SpellInfo spellProto = null, Mechanics mechanic = default, SpellSchoolMask damageSchoolMask = SpellSchoolMask.Normal, Spell spell = null, AuraEffect aurEff = null)
        {
            if (victim == null || damage == 0)
                return 0;

            uint creatureTypeMask = victim.GetCreatureTypeMask();

            // Done fixed damage bonus auras
            int DoneFlatBenefit = 0;

            // ..done
            DoneFlatBenefit += GetTotalAuraModifierByMiscMask(AuraType.ModDamageDoneCreature, creatureTypeMask);

            // ..done
            // SPELL_AURA_MOD_DAMAGE_DONE included in weapon damage

            // ..done (base at attack power for marked target and base at attack power for creature Type)
            int APbonus = 0;

            if (attType == WeaponAttackType.RangedAttack)
            {
                APbonus += victim.GetTotalAuraModifier(AuraType.RangedAttackPowerAttackerBonus);

                // ..done (base at attack power and creature Type)
                APbonus += GetTotalAuraModifierByMiscMask(AuraType.ModRangedAttackPowerVersus, creatureTypeMask);
            }
            else
            {
                APbonus += victim.GetTotalAuraModifier(AuraType.MeleeAttackPowerAttackerBonus);

                // ..done (base at attack power and creature Type)
                APbonus += GetTotalAuraModifierByMiscMask(AuraType.ModMeleeAttackPowerVersus, creatureTypeMask);
            }

            if (APbonus != 0)                                       // Can be negative
            {
                bool normalized = spellProto != null && spellProto.HasEffect(SpellEffectName.NormalizedWeaponDmg);
                DoneFlatBenefit += (int)(APbonus / 3.5f * GetAPMultiplier(attType, normalized));
            }

            // Done total percent damage auras
            float DoneTotalMod = 1.0f;

            SpellSchoolMask schoolMask = spellProto != null ? spellProto.GetSchoolMask() : damageSchoolMask;

            if ((schoolMask & SpellSchoolMask.Normal) == 0)
            {
                // Some spells don't benefit from pct done mods
                // mods for SPELL_SCHOOL_MASK_NORMAL are already factored in base melee damage calculation
                if (spellProto == null || !spellProto.HasAttribute(SpellAttr6.IgnoreCasterDamageModifiers))
                {
                    float maxModDamagePercentSchool = 0.0f;
                    Player thisPlayer = ToPlayer();
                    if (thisPlayer != null)
                    {
                        for (SpellSchools school = SpellSchools.Holy; school < SpellSchools.Max; school++)
                        {
                            if (schoolMask.HasSchool(school))
                            {
                                maxModDamagePercentSchool = 
                                    Math.Max(maxModDamagePercentSchool, thisPlayer.m_activePlayerData.ModDamageDonePercent[(int)school]);
                            }
                        }
                    }
                    else
                        maxModDamagePercentSchool = GetTotalAuraMultiplierByMiscMask(AuraType.ModDamagePercentDone, (uint)schoolMask);

                    DoneTotalMod *= maxModDamagePercentSchool;
                }
            }

            if (spellProto == null)
            {
                // melee attack
                foreach (AuraEffect autoAttackDamage in GetAuraEffectsByType(AuraType.ModAutoAttackDamage))
                    MathFunctions.AddPct(ref DoneTotalMod, autoAttackDamage.GetAmount());
            }

            DoneTotalMod *= GetTotalAuraMultiplierByMiscMask(AuraType.ModDamageDoneVersus, creatureTypeMask);

            // bonus against aurastate
            DoneTotalMod *= GetTotalAuraMultiplier(AuraType.ModDamageDoneVersusAurastate, aurEff =>
            {
                if (victim.HasAuraState((AuraStateType)aurEff.GetMiscValue()))
                    return true;
                return false;
            });

            // bonus against target aura mechanic
            DoneTotalMod *= GetTotalAuraMultiplier(AuraType.ModDamagePercentDoneByTargetAuraMechanic, aurEff =>
            {
                if (victim.HasAuraWithMechanic(1ul << aurEff.GetMiscValue()))
                    return true;
                return false;
            });

            // Add SPELL_AURA_MOD_DAMAGE_DONE_FOR_MECHANIC percent bonus
            if (mechanic != Mechanics.None)
                MathFunctions.AddPct(ref DoneTotalMod, GetTotalAuraModifierByMiscValue(AuraType.ModDamageDoneForMechanic, (int)mechanic));
            else if (spellProto != null && spellProto.Mechanic != 0)
                MathFunctions.AddPct(ref DoneTotalMod, GetTotalAuraModifierByMiscValue(AuraType.ModDamageDoneForMechanic, (int)spellProto.Mechanic));

            if (spell != null)
                spell.CallScriptCalcDamageHandlers(victim, ref damage, ref DoneFlatBenefit, ref DoneTotalMod);
            else if (aurEff != null)
                aurEff.GetBase().CallScriptCalcDamageAndHealingHandlers(aurEff, aurEff.GetBase().GetApplicationOfTarget(victim.GetGUID()), victim, ref damage, ref DoneFlatBenefit, ref DoneTotalMod);

            float damageF = (damage + DoneFlatBenefit) * DoneTotalMod;

            // apply spellmod to Done damage
            if (spellProto != null)
            {
                Player modOwner = GetSpellModOwner();
                if (modOwner != null)
                    modOwner.ApplySpellMod(spellProto, damagetype == DamageEffectType.DOT ? SpellModOp.PeriodicHealingAndDamage : SpellModOp.HealingAndDamage, ref damageF);
            }

            // bonus result can be negative
            return (int)Math.Max(damageF, 0.0f);
        }

        public int MeleeDamageBonusTaken(Unit attacker, int pdamage, WeaponAttackType attType, DamageEffectType damagetype, SpellInfo spellProto = null, SpellSchoolMask damageSchoolMask = SpellSchoolMask.Normal)
        {
            if (pdamage == 0)
                return 0;

            int TakenFlatBenefit = 0;

            // ..taken
            TakenFlatBenefit += GetTotalAuraModifierByMiscMask(AuraType.ModDamageTaken, (uint)attacker.GetMeleeDamageSchool().GetSpellSchoolMask());

            if (attType != WeaponAttackType.RangedAttack)
                TakenFlatBenefit += GetTotalAuraModifier(AuraType.ModMeleeDamageTaken);
            else
                TakenFlatBenefit += GetTotalAuraModifier(AuraType.ModRangedDamageTaken);

            if ((TakenFlatBenefit < 0) && (pdamage < -TakenFlatBenefit))
                return 0;

            // Taken total percent damage auras
            float TakenTotalMod = 1.0f;

            // ..taken
            TakenTotalMod *= GetTotalAuraMultiplierByMiscMask(
                AuraType.ModDamagePercentTaken, (uint)attacker.GetMeleeDamageSchool().GetSpellSchoolMask());

            // .. taken pct (special attacks)
            if (spellProto != null)
            {
                // From caster spells
                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModSchoolMaskDamageFromCaster, aurEff =>
                {
                    return aurEff.GetCasterGUID() == attacker.GetGUID() && (aurEff.GetMiscValue() & (int)spellProto.GetSchoolMask()) != 0;
                });

                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModSpellDamageFromCaster, aurEff =>
                {
                    return aurEff.GetCasterGUID() == attacker.GetGUID() && aurEff.IsAffectingSpell(spellProto);
                });

                // Mod damage from spell mechanic
                ulong mechanicMask = spellProto.GetAllEffectsMechanicMask();

                // Shred, Maul - "Effects which increase Bleed damage also increase Shred damage"
                if (spellProto.SpellFamilyName == SpellFamilyNames.Druid && spellProto.SpellFamilyFlags[0].HasAnyFlag(0x00008800u))
                    mechanicMask |= (1 << (int)Mechanics.Bleed);

                if (mechanicMask != 0)
                {
                    TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModMechanicDamageTakenPercent, aurEff =>
                    {
                        if ((mechanicMask & (1ul << (aurEff.GetMiscValue()))) != 0)
                            return true;
                        return false;
                    });
                }

                if (damagetype == DamageEffectType.DOT)
                {
                    TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModPeriodicDamageTaken, aurEff =>
                        (aurEff.GetMiscValue() & (uint)spellProto.GetSchoolMask()) != 0
                    );
                }
            }
            else // melee attack
            {
                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModMeleeDamageFromCaster, aurEff =>
                {
                    return aurEff.GetCasterGUID() == attacker.GetGUID();
                });
            }

            AuraEffect cheatDeath = GetAuraEffect(45182, 0);
            if (cheatDeath != null)
                MathFunctions.AddPct(ref TakenTotalMod, cheatDeath.GetAmount());

            if (attType != WeaponAttackType.RangedAttack)
                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModMeleeDamageTakenPct);
            else
                TakenTotalMod *= GetTotalAuraMultiplier(AuraType.ModRangedDamageTakenPct);            

            // Sanctified Wrath (bypass damage reduction)
            if (TakenTotalMod < 1.0f)
            {
                SpellSchoolMask attackSchoolMask = spellProto != null ? spellProto.GetSchoolMask() : damageSchoolMask;

                float damageReduction = 1.0f - TakenTotalMod;
                var casterIgnoreResist = attacker.GetAuraEffectsByType(AuraType.ModIgnoreTargetResist);
                foreach (AuraEffect aurEff in casterIgnoreResist)
                {
                    if ((aurEff.GetMiscValue() & (int)attackSchoolMask) == 0)
                        continue;

                    MathFunctions.AddPct(ref damageReduction, -aurEff.GetAmount());
                }

                TakenTotalMod = 1.0f - damageReduction;
            }

            float tmpDamage = (pdamage + TakenFlatBenefit) * TakenTotalMod;
            return (int)Math.Max(tmpDamage, 0.0f);
        }

        bool IsBlockCritical()
        {
            if (RandomHelper.randChance(GetTotalAuraModifier(AuraType.ModBlockCritChance)))
                return true;
            return false;
        }

        public virtual SpellSchools GetMeleeDamageSchool(WeaponAttackType attackType = WeaponAttackType.BaseAttack) 
        { 
            return SpellSchools.Normal; 
        }

        public virtual void UpdateDamageDoneMods(WeaponAttackType attackType, EnchantmentSlot? skipEnchantSlot = null)
        {
            UnitMods unitMod = attackType switch
            {
                WeaponAttackType.BaseAttack => UnitMods.DamageMainHand,
                WeaponAttackType.OffAttack => UnitMods.DamageOffHand,
                WeaponAttackType.RangedAttack => UnitMods.DamageRanged,
                _ => throw new NotImplementedException(),
            };

            float amount = GetTotalAuraModifier(AuraType.ModDamageDone, aurEff =>
            {
                if ((aurEff.GetMiscValue() & (int)SpellSchoolMask.Normal) == 0)
                    return false;

                return CheckAttackFitToAuraRequirement(attackType, aurEff);
            });

            StatMods.SetFlat(unitMod, (int)amount);
        }

        public void UpdateAllDamageDoneMods()
        {
            for (var attackType = WeaponAttackType.BaseAttack; attackType < WeaponAttackType.Max; ++attackType)
                UpdateDamageDoneMods(attackType);
        }

        public void UpdateDamagePctDoneMods(WeaponAttackType attackType)
        {
            (UnitMods unitMod, float factor) = attackType switch
            {
                WeaponAttackType.BaseAttack => (UnitMods.DamageMainHand, 1.0f),
                WeaponAttackType.OffAttack => (UnitMods.DamageOffHand, 0.5f),
                WeaponAttackType.RangedAttack => (UnitMods.DamageRanged, 1.0f),
                _ => throw new NotImplementedException(),
            };

            factor *= GetTotalAuraMultiplier(AuraType.ModDamagePercentDone, aurEff =>
            {
                if (!aurEff.GetMiscValue().HasAnyFlag((int)SpellSchoolMask.Normal))
                    return false;

                if (CheckAttackFitToAuraRequirement(WeaponAttackType.Any, aurEff))
                    return false;

                return CheckAttackFitToAuraRequirement(attackType, aurEff);
            });

            if (attackType == WeaponAttackType.OffAttack)
            {
                factor *= GetTotalAuraMultiplier(AuraType.ModOffhandDamagePct, auraEffect =>
                            CheckAttackFitToAuraRequirement(attackType, auraEffect)
                            );
            }

            StatMods.SetMult(unitMod, factor, UnitModType.TotalPermanent);
        }

        public void UpdateAllDamagePctDoneMods()
        {
            for (var attackType = WeaponAttackType.BaseAttack; attackType < WeaponAttackType.Max; ++attackType)
                UpdateDamagePctDoneMods(attackType);
        }

        public void AddWorldEffect(int worldEffectId) 
        { 
            AddDynamicUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.WorldEffects), worldEffectId); 
        }

        public void RemoveWorldEffect(int worldEffectId)
        {
            int index = m_unitData.WorldEffects.FindIndex(worldEffectId);
            if (index >= 0)
                RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.WorldEffects), index);
        }

        public void ClearWorldEffects() 
        { 
            ClearDynamicUpdateFieldValues(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.WorldEffects)); 
        }

        public CombatManager GetCombatManager() { return m_combatManager; }

        // Exposes the threat manager directly - be careful when interfacing with this
        // As a general rule of thumb, any unit pointer MUST be null checked BEFORE passing it to threatmanager methods
        // threatmanager will NOT null check your pointers for you - misuse = crash
        public ThreatManager GetThreatManager() { return m_threatManager; }
    }
}
